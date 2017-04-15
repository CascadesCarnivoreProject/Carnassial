using Carnassial.Database;
using Carnassial.Util;
using OfficeOpenXml;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;

namespace Carnassial.Data
{
    /// <summary>
    /// Import and export .csv and .xlsx files.
    /// </summary>
    public class SpreadsheetReaderWriter
    {
        /// <summary>
        /// Export all data for the selected files to the .csv file indicated.
        /// </summary>
        public void ExportFileDataToCsv(FileDatabase database, string csvFilePath)
        {
            using (TextWriter fileWriter = new StreamWriter(csvFilePath, false))
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

                foreach (ImageRow file in database.Files)
                {
                    StringBuilder csvRow = new StringBuilder();
                    foreach (string dataLabel in dataLabels)
                    {
                        csvRow.Append(this.AddColumnValue(file.GetValueDatabaseString(dataLabel)));
                    }
                    fileWriter.WriteLine(csvRow.ToString());
                }
            }
        }

        /// <summary>
        /// Export all data for the selected files to the .xlsx file indicated.
        /// </summary>
        public void ExportFileDataToXlsx(FileDatabase database, string xlsxFilePath)
        {
            using (ExcelPackage xlsxFile = new ExcelPackage(new FileInfo(xlsxFilePath)))
            {
                List<string> dataLabels = database.GetDataLabelsExceptIDInSpreadsheetOrder();
                ExcelWorksheet worksheet = this.GetOrCreateBlankWorksheet(xlsxFile, Constant.Excel.FileDataWorksheetName, dataLabels);

                int row = 1;
                foreach (ImageRow file in database.Files)
                {
                    int column = 0;
                    ++row;
                    foreach (string dataLabel in dataLabels)
                    {
                        worksheet.Cells[row, ++column].Value = file.GetValueDatabaseString(dataLabel);
                    }
                }

                // match column widths to content
                worksheet.Cells[1, 1, worksheet.Dimension.Rows, worksheet.Dimension.Columns].AutoFitColumns(Constant.Excel.MinimumColumnWidth, Constant.Excel.MaximumColumnWidth);

                xlsxFile.Save();
            }
        }

        private bool TryImportFileData(FileDatabase fileDatabase, Func<List<string>> readLine, string spreadsheetFilePath, out List<string> importErrors)
        {
            List<string> dataLabels = fileDatabase.GetDataLabelsExceptIDInSpreadsheetOrder();
            TimeZoneInfo imageSetTimeZone = fileDatabase.ImageSet.GetTimeZone();
            string spreadsheetFileFolderPath = Path.GetDirectoryName(spreadsheetFilePath);
            importErrors = new List<string>();

            // validate file header against the database
            List<string> dataLabelsFromHeader = readLine.Invoke();
            List<string> dataLabelsInFileDatabaseButNotInHeader = dataLabels.Except(dataLabelsFromHeader).ToList();
            foreach (string dataLabel in dataLabelsInFileDatabaseButNotInHeader)
            {
                importErrors.Add("- A column with the data label '" + dataLabel + "' is present in the image set but not in the file." + Environment.NewLine);
            }
            List<string> dataLabelsInHeaderButNotFileDatabase = dataLabelsFromHeader.Except(dataLabels).ToList();
            foreach (string dataLabel in dataLabelsInHeaderButNotFileDatabase)
            {
                importErrors.Add("- A column with the data label '" + dataLabel + "' is present in the file but not in the image set." + Environment.NewLine);
            }

            if (importErrors.Count > 0)
            {
                return false;
            }

            // read data for file from the .csv file
            List<string> dataLabelsExceptFileNameAndRelativePath = new List<string>(dataLabels);
            dataLabelsExceptFileNameAndRelativePath.Remove(Constant.DatabaseColumn.File);
            dataLabelsExceptFileNameAndRelativePath.Remove(Constant.DatabaseColumn.RelativePath);
            FileTuplesWithID existingFilesToUpdate = new FileTuplesWithID(dataLabelsExceptFileNameAndRelativePath);
            List<ImageRow> newFilesToInsert = new List<ImageRow>();
            FileTuplesWithPath newFilesToUpdate = new FileTuplesWithPath(dataLabelsExceptFileNameAndRelativePath);
            for (List<string> row = readLine.Invoke(); row != null; row = readLine.Invoke())
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
                string fileName = null;
                string relativePath = null;
                List<object> values = new List<object>();
                for (int field = 0; field < row.Count; ++field)
                {
                    string dataLabel = dataLabelsFromHeader[field];
                    string value = row[field];

                    // capture components of file's unique identifier for constructing where clause
                    // at least for now, it's assumed all renames or moves are done through Carnassial and hence relative path + file name form 
                    // an immutable (and unique) ID
                    DateTime dateTime;
                    TimeSpan utcOffset;
                    if (dataLabel == Constant.DatabaseColumn.File)
                    {
                        fileName = value;
                    }
                    else if (dataLabel == Constant.DatabaseColumn.RelativePath)
                    {
                        relativePath = value;
                    }
                    else if (dataLabel == Constant.DatabaseColumn.DateTime && DateTimeHandler.TryParseDatabaseDateTime(value, out dateTime))
                    {
                        // pass DateTime to ColumnTuple rather than the string as ColumnTuple owns validation and formatting
                        values.Add(dateTime);
                    }
                    else if (dataLabel == Constant.DatabaseColumn.UtcOffset && DateTimeHandler.TryParseDatabaseUtcOffsetString(value, out utcOffset))
                    {
                        // as with DateTime, pass parsed UTC offset to ColumnTuple rather than the string as ColumnTuple owns validation and formatting
                        values.Add(utcOffset);
                    }
                    else if (fileDatabase.ControlsByDataLabel[dataLabel].IsValidData(value))
                    {
                        // include column in update query if value is valid
                        values.Add(value);
                    }
                    else
                    {
                        // if value wasn't processed by a previous clause it's invalid (or there's a parsing bug)
                        importErrors.Add(String.Format("Value '{0}' is not valid for the column {1}.", value, dataLabel));
                        values.Add(null);
                    }
                }

                if (String.IsNullOrWhiteSpace(fileName))
                {
                    importErrors.Add(String.Format("No file name found in row {0}.", row));
                    continue;
                }

                // if file's already in the image set prepare to set its fields to those in the .csv
                // if file's not in the image set prepare to add it to the image set
                FileInfo fileInfo = new FileInfo(Path.Combine(spreadsheetFileFolderPath, relativePath, fileName));
                ImageRow file;
                if (fileDatabase.GetOrCreateFile(fileInfo, imageSetTimeZone, out file))
                {
                    existingFilesToUpdate.Add(file.ID, values);
                }
                else
                {
                    // newly created files have only their name and relative path set; populate all other fields with the data from the .csv
                    // Population is done via update as insertion is done with default values.
                    newFilesToInsert.Add(file);
                    newFilesToUpdate.Add(file.RelativePath, file.FileName, values);
                }
            }

            // perform inserts and updates
            // Inserts need to be done first so newly added files can be updated.
            fileDatabase.AddFiles(newFilesToInsert, null);
            fileDatabase.UpdateFiles(existingFilesToUpdate, newFilesToUpdate);
            return true;
        }

        public bool TryImportFileDataFromCsv(string csvFilePath, FileDatabase fileDatabase, out List<string> importErrors)
        {
            using (FileStream stream = new FileStream(csvFilePath, FileMode.Open, FileAccess.Read, FileShare.Read))
            {
                using (StreamReader csvReader = new StreamReader(stream))
                {
                    return this.TryImportFileData(fileDatabase, () => { return this.ReadAndParseCsvLine(csvReader); }, csvFilePath, out importErrors);
                }
            }
        }

        public bool TryImportFileDataFromXlsx(string xlsxFilePath, FileDatabase fileDatabase, out List<string> importErrors)
        {
            using (ExcelPackage xlsxFile = new ExcelPackage(new FileInfo(xlsxFilePath)))
            {
                ExcelWorksheet worksheet = xlsxFile.Workbook.Worksheets.FirstOrDefault(sheet => sheet.Name == Constant.Excel.FileDataWorksheetName);
                if (worksheet == null)
                {
                    importErrors = new List<string>() { String.Format("Worksheet {0} not found.", Constant.Excel.FileDataWorksheetName) };
                    return false;
                }
                int row = 0;
                return this.TryImportFileData(fileDatabase, () => { return this.ReadXlsxRow(worksheet, ++row); }, xlsxFilePath, out importErrors);
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

        protected ExcelWorksheet GetOrCreateBlankWorksheet(ExcelPackage xlsxFile, string worksheetName, List<string> columnHeaders)
        {
            // get empty worksheet
            ExcelWorksheet worksheet = xlsxFile.Workbook.Worksheets.FirstOrDefault(sheet => sheet.Name == worksheetName);
            if (worksheet == null)
            {
                worksheet = xlsxFile.Workbook.Worksheets.Add(worksheetName);
                worksheet.View.FreezePanes(2, 1);
            }
            else
            {
                worksheet.Cells.Clear();
            }

            // write header
            for (int index = 0; index < columnHeaders.Count; ++index)
            {
                worksheet.Cells[1, index + 1].Value = columnHeaders[index];
            }

            ExcelRange headerCells = worksheet.Cells[1, 1, 1, columnHeaders.Count];
            headerCells.AutoFilter = true;
            headerCells.Style.Font.Bold = true;

            return worksheet;
        }

        private List<string> ReadAndParseCsvLine(StreamReader csvReader)
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

        protected List<string> ReadXlsxRow(ExcelWorksheet worksheet, int row)
        {
            if (worksheet.Dimension.Rows < row)
            {
                return null;
            }

            List<string> rowContent = new List<string>(worksheet.Dimension.Columns);
            for (int column = 1; column <= worksheet.Dimension.Columns; ++column)
            {
                ExcelRange cell = worksheet.Cells[row, column];
                if (cell.Value is bool)
                {
                    bool cellValue = (bool)cell.Value;
                    rowContent.Add(cellValue ? Boolean.TrueString : Boolean.FalseString);
                }
                else
                {
                    rowContent.Add(cell.Text);
                }
            }
            return rowContent;
        }
    }
}
