﻿using Carnassial.Util;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;

namespace Carnassial.Database
{
    /// <summary>
    /// Import and export .csv files.
    /// </summary>
    internal class CsvReaderWriter
    {
        /// <summary>
        /// Export all the database data associated with the selection to the .csv file indicated in the file path so that spreadsheet applications (like Excel) can display it.
        /// </summary>
        public void ExportToCsv(FileDatabase database, string filePath)
        {
            using (TextWriter fileWriter = new StreamWriter(filePath, false))
            {
                // write the header as defined by the data labels in the template file
                // The append sequence results in a trailing comma which is retained when writing the line.
                StringBuilder header = new StringBuilder();
                List<string> dataLabels = database.GetDataLabelsExceptIDInSpreadsheetOrder();
                foreach (string dataLabel in dataLabels)
                {
                    header.Append(this.AddColumnValue(dataLabel));
                }
                fileWriter.WriteLine(header.ToString());

                // for each row in the data table, write out the columns in the same order as the data labels in the template file
                for (int row = 0; row < database.CurrentlySelectedFileCount; row++)
                {
                    StringBuilder csvRow = new StringBuilder();
                    ImageRow image = database.Files[row];
                    foreach (string dataLabel in dataLabels)
                    {
                        csvRow.Append(this.AddColumnValue(image.GetValueDatabaseString(dataLabel)));
                    }
                    fileWriter.WriteLine(csvRow.ToString());
                }
            }
        }

        public bool TryImportFromCsv(string filePath, FileDatabase fileDatabase, out List<string> importErrors)
        {
            importErrors = new List<string>();
            
            List<string> dataLabels = fileDatabase.GetDataLabelsExceptIDInSpreadsheetOrder();
            using (FileStream stream = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.Read))
            {
                using (StreamReader csvReader = new StreamReader(stream))
                {
                    // validate .csv file headers against the database
                    List<string> dataLabelsFromHeader = this.ReadAndParseLine(csvReader);
                    List<string> dataLabelsInFileDatabaseButNotInHeader = dataLabels.Except(dataLabelsFromHeader).ToList();
                    foreach (string dataLabel in dataLabelsInFileDatabaseButNotInHeader)
                    {
                        importErrors.Add("- A column with the DataLabel '" + dataLabel + "' is present in the database but nothing matches that in the .csv file." + Environment.NewLine);
                    }
                    List<string> dataLabelsInHeaderButNotFileDatabase = dataLabelsFromHeader.Except(dataLabels).ToList();
                    foreach (string dataLabel in dataLabelsInHeaderButNotFileDatabase)
                    {
                        importErrors.Add("- A column with the DataLabel '" + dataLabel + "' is present in the .csv file but nothing matches that in the database." + Environment.NewLine);
                    }

                    if (importErrors.Count > 0)
                    {
                        return false;
                    }

                    // read image updates from the .csv file
                    List<ColumnTuplesWithWhere> imagesToUpdate = new List<ColumnTuplesWithWhere>();
                    for (List<string> row = this.ReadAndParseLine(csvReader); row != null; row = this.ReadAndParseLine(csvReader))
                    {
                        if (row.Count == dataLabels.Count - 1)
                        {
                            // .csv files are ambiguous in the sense a trailing comma may or may not be present at the end of the line
                            // if the final field has a value this case isn't a concern, but if the final field has no value then there's
                            // no way for the parser to know the exact number of fields in the line
                            row.Add(String.Empty);
                        }
                        else if (row.Count != dataLabels.Count)
                        {
                            Debug.Fail(String.Format("Expected {0} fields in line {1} but found {2}.", dataLabels.Count, String.Join(",", row), row.Count));
                        }

                        // assemble set of column values to update
                        string imageFileName = null;
                        string relativePath = null;
                        ColumnTuplesWithWhere imageToUpdate = new ColumnTuplesWithWhere();
                        for (int field = 0; field < row.Count; ++field)
                        {
                            string dataLabel = dataLabelsFromHeader[field];
                            string value = row[field];

                            // capture components of image's unique identifier for constructing where clause
                            // at least for now, it's assumed all renames or moves are done through Carnassial and hence relative path + file name form 
                            // an immutable (and unique) ID
                            DateTime dateTime;
                            TimeSpan utcOffset;
                            if (dataLabel == Constant.DatabaseColumn.File)
                            {
                                imageFileName = value;
                            }
                            else if (dataLabel == Constant.DatabaseColumn.RelativePath)
                            {
                                relativePath = value;
                            }
                            else if (dataLabel == Constant.DatabaseColumn.DateTime && DateTimeHandler.TryParseDatabaseDateTime(value, out dateTime))
                            {
                                // pass DateTime to ColumnTuple rather than the string as ColumnTuple owns validation and formatting
                                imageToUpdate.Columns.Add(new ColumnTuple(dataLabel, dateTime));
                            }
                            else if (dataLabel == Constant.DatabaseColumn.UtcOffset && DateTimeHandler.TryParseDatabaseUtcOffsetString(value, out utcOffset))
                            {
                                // as with DateTime, pass parsed UTC offset to ColumnTuple rather than the string as ColumnTuple owns validation and formatting
                                imageToUpdate.Columns.Add(new ColumnTuple(dataLabel, utcOffset));
                            }
                            else if (fileDatabase.FileTableColumnsByDataLabel[dataLabel].IsContentValid(value))
                            {
                                // include column in update query if value is valid
                                imageToUpdate.Columns.Add(new ColumnTuple(dataLabel, value));
                            }
                            else
                            {
                                // if value wasn't processed by a previous clause it's invalid (or there's a parsing bug)
                                importErrors.Add(String.Format("Value '{0}' is not valid for the column {1}.", value, dataLabel));
                            }
                        }

                        // accumulate image
                        Debug.Assert(String.IsNullOrWhiteSpace(imageFileName) == false, "File name was not loaded.");
                        imageToUpdate.SetWhere(relativePath, imageFileName);
                        imagesToUpdate.Add(imageToUpdate);

                        // write current batch of updates to database
                        if (imagesToUpdate.Count >= 100)
                        {
                            fileDatabase.UpdateFiles(imagesToUpdate);
                            imagesToUpdate.Clear();
                        }
                    }

                    // perform any remaining updates
                    fileDatabase.UpdateFiles(imagesToUpdate);
                    return true;
                }
            }
        }

        private string AddColumnValue(string value)
        {
            if (value == null)
            {
                return ",";
            }
            if (value.IndexOfAny("\",\x0A\x0D".ToCharArray()) > -1)
            {
                // commas, double quotation marks, line feeds (\x0A), and carriage returns (\x0D) require leading and ending double quotation marks be added
                // double quotation marks within the field also have to be escaped as double quotes
                return "\"" + value.Replace("\"", "\"\"") + "\"" + ",";
            }

            return value + ",";
        }

        private List<string> ReadAndParseLine(StreamReader csvReader)
        {
            string unparsedLine = csvReader.ReadLine();
            if (unparsedLine == null)
            {
                return null;
            }

            List<string> parsedLine = new List<string>();
            bool isFieldEscaped = false;
            int fieldStart = 0;
            bool inField = false;
            for (int index = 0; index < unparsedLine.Length; ++index)
            {
                char currentCharacter = unparsedLine[index];
                if (inField == false)
                {
                    if (currentCharacter == '\"')
                    {
                        // start of escaped field
                        isFieldEscaped = true;
                        fieldStart = index + 1;
                    }
                    else if (currentCharacter == ',')
                    {
                        // empty field
                        // promote null values to empty values to prevent the presence of SQNull objects in data tables
                        // much Carnassial code assumes data table fields can be blindly cast to string and breaks once the data table has been
                        // refreshed after null values are inserted
                        parsedLine.Add(String.Empty);
                        continue;
                    }
                    else
                    {
                        // start of unescaped field
                        fieldStart = index;
                    }

                    inField = true;
                }
                else
                {
                    if (currentCharacter == ',' && isFieldEscaped == false)
                    {
                        // end of unescaped field
                        inField = false;
                        string field = unparsedLine.Substring(fieldStart, index - fieldStart);
                        parsedLine.Add(field);
                    }
                    else if (currentCharacter == '\"' && isFieldEscaped)
                    {
                        // escaped character encountered; check for end of escaped field
                        int nextIndex = index + 1;
                        if (nextIndex < unparsedLine.Length)
                        {
                            if (unparsedLine[nextIndex] == ',')
                            {
                                // end of escaped field
                                // note: Whilst this implementation supports escaping of carriage returns and line feeds on export it does not support them on
                                // import.  This is common in .csv parsers and can be addressed if needed by appending the next line to unparsedLine and 
                                // continuing parsing rather than terminating the field.
                                inField = false;
                                isFieldEscaped = false;
                                string field = unparsedLine.Substring(fieldStart, index - fieldStart);
                                field = field.Replace("\"\"", "\"");
                                parsedLine.Add(field);
                                ++index;
                            }
                            else if (unparsedLine[nextIndex] == '"')
                            {
                                // escaped double quotation mark
                                // just move next to skip over the second quotation mark as replacement back to one quotation mark is done in field extraction
                                ++index;
                            }
                        }
                    }
                }
            }

            // if the last character is a non-comma add the final (non-empty) field
            // final empty fields are ambiguous at this level and therefore handled by the caller
            if (inField)
            {
                string field = unparsedLine.Substring(fieldStart, unparsedLine.Length - fieldStart);
                if (isFieldEscaped)
                {
                    field = field.Replace("\"\"", "\"");
                }
                parsedLine.Add(field);
            }

            return parsedLine;
        }
    }
}
