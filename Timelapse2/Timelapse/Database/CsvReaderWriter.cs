using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Windows;
using Timelapse.Util;

namespace Timelapse.Database
{
    /// <summary>
    /// Import and export .csv files.
    /// </summary>
    internal class CsvReaderWriter
    {
        /// <summary>
        /// Export all the database data associated with the filtered view to the CSV file indicated in the file path so that spreadsheet applications (like Excel) can display it.
        /// </summary>
        public void ExportToCsv(ImageDatabase database, string filePath)
        {
            using (TextWriter fileWriter = new StreamWriter(filePath, false))
            {
                // Write the header as defined by the data labels in the template file
                // If the data label is an empty string, we use the label instead.
                // The append sequence results in a trailing comma which is retained when writing the line.
                StringBuilder header = new StringBuilder();
                List<string> dataLabels = database.GetDataLabelsExceptID();
                foreach (string dataLabel in dataLabels)
                {
                    header.Append(this.AddColumnValue(dataLabel));
                }
                fileWriter.WriteLine(header.ToString());

                // For each row in the data table, write out the columns in the same order as the 
                // data labels in the template file
                for (int i = 0; i < database.CurrentlySelectedImageCount; i++)
                {
                    StringBuilder row = new StringBuilder();
                    foreach (string dataLabel in dataLabels)
                    {
                        row.Append(this.AddColumnValue(database.ImageDataTable[i][dataLabel]));
                    }
                    fileWriter.WriteLine(row.ToString());
                }
            }
        }

        public bool TryImportFromCsv(string filePath, ImageDatabase imageDatabase, out List<string> importErrors)
        {
            importErrors = new List<string>();
            
            List<string> dataLabels = imageDatabase.GetDataLabelsExceptID();
            using (FileStream stream = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.Read))
            {
                using (StreamReader csvReader = new StreamReader(stream))
                {
                    // validate CSV file headers against the database
                    List<string> dataLabelsFromHeader = this.ReadAndParseLine(csvReader);
                    List<string> dataLabelsInImageDatabaseButNotInHeader = dataLabels.Except(dataLabelsFromHeader).ToList();
                    foreach (string dataLabel in dataLabelsInImageDatabaseButNotInHeader)
                    {
                        importErrors.Add("- A column with the DataLabel '" + dataLabel + "' is present in the database but nothing matches that in the CSV file." + Environment.NewLine);
                    }
                    List<string> dataLabelsInHeaderButNotImageDatabase = dataLabelsFromHeader.Except(dataLabels).ToList();
                    foreach (string dataLabel in dataLabelsInHeaderButNotImageDatabase)
                    {
                        importErrors.Add("- A column with the DataLabel '" + dataLabel + "' is present in the CSV file but nothing matches that in the database." + Environment.NewLine);
                    }

                    if (importErrors.Count > 0)
                    {
                        return false;
                    }

                    // read image updates from the CSV file
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
                            Debug.Assert(false, String.Format("Expected {0} fields in line {1} but found {2}.", dataLabels.Count, String.Join(",", row), row.Count));
                        }

                        // assemble set of column values to update
                        string imageFileName = null;
                        string folder = null;
                        string relativePath = null;
                        ColumnTuplesWithWhere imageToUpdate = new ColumnTuplesWithWhere();
                        for (int field = 0; field < row.Count; ++field)
                        {
                            string dataLabel = dataLabelsFromHeader[field];
                            string value = row[field];

                            // capture components of image's unique identifier for constructing where clause
                            // at least for now, it's assumed all renames or moves are done through Timelapse and hence file name + folder + relative path form 
                            // an immutable (and unique) ID
                            if (dataLabel == Constants.DatabaseColumn.File)
                            {
                                imageFileName = value;
                            }
                            else if (dataLabel == Constants.DatabaseColumn.Folder)
                            {
                                folder = value;
                            }
                            else if (dataLabel == Constants.DatabaseColumn.RelativePath)
                            {
                                relativePath = value;
                            }
                            else if (dataLabel == Constants.DatabaseColumn.Date ||
                                     dataLabel == Constants.DatabaseColumn.Time)
                            {
                                // also don't update date and time, as Excel will often change the date format when the csv file is opened, changed and saved.
                                continue;
                            }
                            else
                            {
                                // check field value for validity
                                if (imageDatabase.ImageDataColumnsByDataLabel[dataLabel].IsContentValid(value))
                                {
                                    // include column in update query
                                    imageToUpdate.Columns.Add(new ColumnTuple(dataLabel, value));
                                }
                                else
                                {
                                    importErrors.Add(String.Format("Value '{0}' is not valid for the column {1}.", value, dataLabel));
                                }
                            }
                        }

                        // accumulate image
                        Debug.Assert(String.IsNullOrWhiteSpace(imageFileName) == false, "File name was not loaded.");
                        imageToUpdate.SetWhere(folder, relativePath, imageFileName);
                        imagesToUpdate.Add(imageToUpdate);

                        // write current batch of updates to database
                        if (imagesToUpdate.Count >= 100)
                        {
                            imageDatabase.UpdateImages(imagesToUpdate);
                            imagesToUpdate.Clear();
                        }
                    }

                    // perform any remaining updates
                    imageDatabase.UpdateImages(imagesToUpdate);
                    return true;
                }
            }
        }

        // Check if there is any Quotation Mark '"', a Comma ',', a Line Feed \x0A,  or Carriage Return \x0D
        // and escape it as needed
        private string AddColumnValue(string value)
        {
            if (value == null)
            {
                return ",";
            }
            if (value.IndexOfAny("\",\x0A\x0D".ToCharArray()) > -1)
            {
                return "\"" + value.Replace("\"", "\"\"") + "\"" + ",";
            }
            else
            {
                return value + ",";
            }
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
                        // much Timelapse code assumes data table fields can be blindly cast to string and breaks once the data table has been
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
                        if (nextIndex < unparsedLine.Length && unparsedLine[nextIndex] == ',')
                        {
                            // end of escaped field
                            inField = false;
                            isFieldEscaped = false;
                            string field = unparsedLine.Substring(fieldStart, index - fieldStart);
                            parsedLine.Add(field);
                            ++index;
                        }
                    }
                }
            }

            // if the last character is a non-comma add the final (non-empty) field
            // final empty fields are ambiguous at this level and therefore handled by the caller
            if (inField)
            {
                string field = unparsedLine.Substring(fieldStart, unparsedLine.Length - fieldStart); 
                parsedLine.Add(field);
            }

            return parsedLine;
        }
    }
}
