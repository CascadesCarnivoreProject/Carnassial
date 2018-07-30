﻿using Carnassial.Database;
using Carnassial.Interop;
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
        private SpreadsheetReadWriteStatus status;

        public SpreadsheetReaderWriter(Action<SpreadsheetReadWriteStatus> onProgressUpdate, TimeSpan desiredProgressUpdateInterval)
        {
            this.status = new SpreadsheetReadWriteStatus(onProgressUpdate, desiredProgressUpdateInterval);
        }

        /// <summary>
        /// Export all data for the selected files to the .csv file indicated.
        /// </summary>
        public void ExportFileDataToCsv(FileDatabase database, string csvFilePath)
        {
            this.status.BeginWrite(database.Files.RowCount);

            using (TextWriter fileWriter = new StreamWriter(csvFilePath, false))
            {
                // write the header as defined by the data labels in the template file
                // The append sequence results in a trailing comma which is retained when writing the line.
                StringBuilder header = new StringBuilder();
                List<string> columns = new List<string>(database.Controls.RowCount);
                foreach (ControlRow control in database.Controls.InSpreadsheetOrder())
                {
                    columns.Add(control.DataLabel);
                    header.Append(this.AddColumnValue(control.DataLabel));

                    if (control.Type == ControlType.Counter)
                    {
                        string markerColumn = FileTable.GetMarkerPositionColumnName(control.DataLabel);
                        columns.Add(markerColumn);
                        header.Append(this.AddColumnValue(markerColumn));
                    }
                }
                fileWriter.WriteLine(header.ToString());

                for (int fileIndex = 0; fileIndex < database.Files.RowCount; ++fileIndex)
                {
                    ImageRow file = database.Files[fileIndex];
                    StringBuilder csvRow = new StringBuilder();
                    foreach (string dataLabel in columns)
                    {
                        csvRow.Append(this.AddColumnValue(file.GetExcelString(dataLabel)));
                    }
                    fileWriter.WriteLine(csvRow.ToString());

                    if (this.status.ShouldReport())
                    {
                        this.status.Report(fileIndex);
                    }
                }
            }

            this.status.Report(database.Files.RowCount);
        }

        /// <summary>
        /// Export all data for the selected files to the .xlsx file indicated.
        /// </summary>
        public void ExportFileDataToXlsx(FileDatabase database, string xlsxFilePath)
        {
            this.status.BeginWrite(database.Files.RowCount);

            using (ExcelPackage xlsxFile = new ExcelPackage(new FileInfo(xlsxFilePath)))
            {
                List<string> columns = new List<string>(database.Controls.RowCount);
                foreach (ControlRow control in database.Controls.InSpreadsheetOrder())
                {
                    columns.Add(control.DataLabel);
                    if (control.Type == ControlType.Counter)
                    {
                        columns.Add(FileTable.GetMarkerPositionColumnName(control.DataLabel));
                    }
                }

                ExcelWorksheet worksheet = this.GetOrCreateBlankWorksheet(xlsxFile, Constant.Excel.FileDataWorksheetName, columns);
                for (int fileIndex = 0; fileIndex < database.Files.RowCount; ++fileIndex)
                {
                    ImageRow file = database.Files[fileIndex];
                    for (int columnIndex = 0; columnIndex < columns.Count; ++columnIndex)
                    {
                        string column = columns[columnIndex];
                        worksheet.Cells[fileIndex + 2, columnIndex + 1].Value = file.GetExcelString(column);
                    }

                    if (this.status.ShouldReport())
                    {
                        this.status.Report(fileIndex);
                    }
                }

                // match column widths to content
                worksheet.Cells[1, 1, worksheet.Dimension.Rows, worksheet.Dimension.Columns].AutoFitColumns(Constant.Excel.MinimumColumnWidth, Constant.Excel.MaximumColumnWidth);

                xlsxFile.Save();
            }

            this.status.Report(database.Files.RowCount);
        }

        private FileImportResult TryImportFileData(FileDatabase fileDatabase, Func<List<string>> readLine, string spreadsheetFilePath)
        {
            if (fileDatabase.ImageSet.FileSelection != FileSelection.All)
            {
                throw new ArgumentOutOfRangeException(nameof(fileDatabase), "Database doesn't have all files selected.  Checking for files already added to the image set would fail.");
            }

            string relativePathFromDatabaseToSpreadsheet = NativeMethods.GetRelativePathFromDirectoryToDirectory(Path.GetDirectoryName(fileDatabase.FilePath), Path.GetDirectoryName(spreadsheetFilePath));
            if (String.Equals(relativePathFromDatabaseToSpreadsheet, ".", StringComparison.Ordinal))
            {
                relativePathFromDatabaseToSpreadsheet = null;
            }
            else if (relativePathFromDatabaseToSpreadsheet.IndexOf("..", StringComparison.Ordinal) != -1)
            {
                throw new NotSupportedException(String.Format("Canonicalization of relative path from database to spreadsheet '{0}' is not currently supported.", relativePathFromDatabaseToSpreadsheet));
            }

            // validate file header against the database
            List<string> columnsFromFileHeader = readLine.Invoke();
            List<string> columnsInDatabase = new List<string>(fileDatabase.Controls.RowCount);
            Dictionary<string, ControlRow> controlsByColumn = new Dictionary<string, ControlRow>();
            foreach (ControlRow control in fileDatabase.Controls)
            {
                controlsByColumn.Add(control.DataLabel, control);
                columnsInDatabase.Add(control.DataLabel);

                if (control.Type == ControlType.Counter)
                {
                    string markerColumn = FileTable.GetMarkerPositionColumnName(control.DataLabel);
                    controlsByColumn.Add(markerColumn, control);
                    columnsInDatabase.Add(markerColumn);
                }
            }

            List<string> columnsInDatabaseButNotInHeader = columnsInDatabase.Except(columnsFromFileHeader).ToList();
            FileImportResult result = new FileImportResult();
            foreach (string column in columnsInDatabaseButNotInHeader)
            {
                result.Errors.Add("- The column '" + column + "' is present in the image set but not in the file." + Environment.NewLine);
            }

            List<string> columnsInHeaderButNotDatabase = columnsFromFileHeader.Except(columnsInDatabase).ToList();
            foreach (string column in columnsInHeaderButNotDatabase)
            {
                result.Errors.Add("- The column '" + column + "' is present in the file but not in the image set." + Environment.NewLine);
            }

            if (result.Errors.Count > 0)
            {
                return result;
            }

            // read data for file from the .csv or .xlsx file
            List<ControlRow> controlsInFileOrder = new List<ControlRow>(columnsFromFileHeader.Count);
            foreach (string column in columnsFromFileHeader)
            {
                controlsInFileOrder.Add(controlsByColumn[column]);
            }

            List<string> dataLabelsForUpdate = new List<string>(columnsInDatabase.Count);
            dataLabelsForUpdate.AddRange(columnsFromFileHeader.Where(column => (String.Equals(column, Constant.FileColumn.File, StringComparison.Ordinal) == false) &&
                                                                              (String.Equals(column, Constant.FileColumn.RelativePath, StringComparison.Ordinal) == false)));
            List<string> dataLabelsForInsert = new List<string>(dataLabelsForUpdate)
            {
                Constant.FileColumn.File,
                Constant.FileColumn.RelativePath
            };

            Dictionary<string, HashSet<string>> filesAlreadyInDatabaseByRelativePath = fileDatabase.Files.HashFileNamesByRelativePath();
            ColumnTuplesForInsert filesToInsert = new ColumnTuplesForInsert(Constant.DatabaseTable.Files, dataLabelsForInsert);
            FileTuplesWithID filesToUpdate = new FileTuplesWithID(dataLabelsForUpdate);
            this.status.Report(0);
            for (List<string> row = readLine.Invoke(); row != null; row = readLine.Invoke())
            {
                if (row.Count == columnsInDatabase.Count - 1)
                {
                    // .csv files are ambiguous in the sense a trailing comma may or may not be present at the end of the line
                    // if the final field has a value this case isn't a concern, but if the final field has no value then there's
                    // no way for the parser to know the exact number of fields in the line
                    row.Add(String.Empty);
                }
                else if (row.Count != columnsInDatabase.Count)
                {
                    Debug.Fail(String.Format("Expected {0} fields in line {1} but found {2}.", columnsInDatabase.Count, String.Join(",", row), row.Count));
                }

                // assemble set of column values to update
                string fileName = null;
                string relativePathFromSpreadsheetDirectoryToFileDirectory = null;
                List<object> values = new List<object>(dataLabelsForUpdate.Count);
                for (int columnIndex = 0; columnIndex < row.Count; ++columnIndex)
                {
                    string dataLabel = columnsFromFileHeader[columnIndex];
                    string value = row[columnIndex];

                    // capture components of file's unique identifier for constructing where clause
                    // at least for now, it's assumed all renames or moves are done through Carnassial and hence relative path + file name form 
                    // an immutable (and unique) ID
                    if (String.Equals(dataLabel, Constant.FileColumn.File, StringComparison.Ordinal))
                    {
                        fileName = value;
                    }
                    else if (String.Equals(dataLabel, Constant.FileColumn.RelativePath, StringComparison.Ordinal))
                    {
                        relativePathFromSpreadsheetDirectoryToFileDirectory = value;
                    }
                    else if (String.Equals(dataLabel, Constant.FileColumn.Classification, StringComparison.Ordinal) && ImageRow.TryParseFileClassification(value, out FileClassification classification))
                    {
                        values.Add((int)classification);
                    }
                    else if (String.Equals(dataLabel, Constant.FileColumn.DateTime, StringComparison.Ordinal) && DateTimeHandler.TryParseDatabaseDateTime(value, out DateTime dateTime))
                    {
                        values.Add(dateTime);
                    }
                    else if (String.Equals(dataLabel, Constant.FileColumn.UtcOffset, StringComparison.Ordinal) && DateTimeHandler.TryParseDatabaseUtcOffsetString(value, out TimeSpan utcOffset))
                    {
                        // offset needs to be converted to a double for database insert or update
                        values.Add(DateTimeHandler.ToDatabaseUtcOffset(utcOffset));
                    }
                    else
                    {
                        ControlRow control = controlsInFileOrder[columnIndex];
                        if (control.IsValidExcelData(value))
                        {
                            if (control.Type == ControlType.Counter)
                            {
                                if (String.IsNullOrEmpty(value))
                                {
                                    values.Add(null);
                                }
                                else if (Int32.TryParse(value, out int valueAsInt))
                                {
                                    values.Add(valueAsInt);
                                }
                                else
                                {
                                    values.Add(MarkersForCounter.MarkerPositionsFromExcelString(value));
                                }
                            }
                            else if (control.Type == ControlType.Flag)
                            {
                                values.Add(Boolean.Parse(value));
                            }
                            else
                            {
                                // for all other columns, include a string value in update query if value is valid
                                values.Add(value);
                            }
                        }
                        else
                        {
                            // if value wasn't processed by a previous clause it's invalid (or there's a parsing bug)
                            result.Errors.Add(String.Format("Value '{0}' is not valid for the column {1}.", value, dataLabel));
                            values.Add(null);
                        }
                    }
                }

                if (String.IsNullOrWhiteSpace(fileName))
                {
                    result.Errors.Add(String.Format("No file name found in row {0}.", row));
                    continue;
                }

                // if file's already in the image set prepare to set its fields to those in the .csv
                // if file's not in the image set prepare to add it to the image set
                string relativePath = relativePathFromSpreadsheetDirectoryToFileDirectory;
                if (relativePathFromDatabaseToSpreadsheet != null)
                {
                    relativePath = Path.Combine(relativePathFromDatabaseToSpreadsheet, relativePathFromSpreadsheetDirectoryToFileDirectory);
                }
                ImageRow file = null;
                if (filesAlreadyInDatabaseByRelativePath.TryGetValue(relativePath, out HashSet<string> filesInFolder))
                {
                    if (filesInFolder.Contains(fileName))
                    {
                        file = fileDatabase.Files.Single(fileName, relativePath);
                    }
                }

                if (file == null)
                {
                    // newly created files have only their name and relative path set; populate all other fields with .csv data
                    // Population is done via update as insertion is done with default values.
                    values.Add(fileName);
                    values.Add(relativePath);
                    filesToInsert.Add(values);
                }
                else
                {
                    filesToUpdate.Add(file.ID, values);
                }
            }

            // perform inserts and updates
            fileDatabase.InsertFiles(filesToInsert);
            fileDatabase.UpdateFiles(filesToUpdate);

            result.FilesAdded = filesToInsert.RowCount;
            result.FilesUpdated = filesToUpdate.RowCount;
            return result;
        }

        public FileImportResult TryImportFileDataFromCsv(string csvFilePath, FileDatabase fileDatabase)
        {
            using (FileStream stream = new FileStream(csvFilePath, FileMode.Open, FileAccess.Read, FileShare.Read))
            {
                using (StreamReader csvReader = new StreamReader(stream))
                {
                    this.status.BeginCsvRead(csvReader.BaseStream.Length);
                    FileImportResult result = this.TryImportFileData(fileDatabase, () => { return this.ReadAndParseCsvLine(csvReader); }, csvFilePath);
                    this.status.Report(csvReader.BaseStream.Position);
                    return result;
                }
            }
        }

        public FileImportResult TryImportFileDataFromXlsx(string xlsxFilePath, FileDatabase fileDatabase)
        {
            try
            {
                using (ExcelPackage xlsxFile = new ExcelPackage(new FileInfo(xlsxFilePath)))
                {
                    ExcelWorksheet worksheet = xlsxFile.Workbook.Worksheets.FirstOrDefault(sheet => String.Equals(sheet.Name, Constant.Excel.FileDataWorksheetName, StringComparison.Ordinal));
                    if (worksheet == null)
                    {
                        FileImportResult worksheetNotFound = new FileImportResult();
                        worksheetNotFound.Errors.Add(String.Format("Worksheet {0} not found.", Constant.Excel.FileDataWorksheetName));
                        return worksheetNotFound;
                    }

                    this.status.BeginExcelRead(worksheet.Dimension.Rows);
                    int row = 0;
                    FileImportResult result = this.TryImportFileData(fileDatabase, () => { return this.ReadXlsxRow(worksheet, ++row); }, xlsxFilePath);
                    this.status.Report(worksheet.Dimension.Rows);
                    return result;
                }
            }
            catch (IOException ioException)
            {
                FileImportResult result = new FileImportResult();
                result.Errors.Add(ioException.ToString());
                return result;
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

            if (this.status.ShouldReport())
            {
                this.status.Report(csvReader.BaseStream.Position);
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
                if (cell.Value is bool cellValue)
                {
                    rowContent.Add(cellValue ? Boolean.TrueString : Boolean.FalseString);
                }
                else
                {
                    rowContent.Add(cell.Text);
                }
            }

            if (this.status.ShouldReport())
            {
                this.status.Report(row);
            }
            return rowContent;
        }
    }
}
