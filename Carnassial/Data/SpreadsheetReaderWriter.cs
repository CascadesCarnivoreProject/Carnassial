using Carnassial.Database;
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

                this.status.BeginExcelWorkbookSave();
                xlsxFile.Save();
                this.status.EndExcelWorkbookSave();
            }
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

            FileImportResult result = new FileImportResult();
            List<string> columnsInDatabaseButNotInHeader = columnsInDatabase.Except(columnsFromFileHeader).ToList();
            foreach (string column in columnsInDatabaseButNotInHeader)
            {
                result.Errors.Add("- The column '" + column + "' is present in the image set but not in the spreadsheet file." + Environment.NewLine);
            }

            List<string> columnsInHeaderButNotDatabase = columnsFromFileHeader.Except(columnsInDatabase).ToList();
            foreach (string column in columnsInHeaderButNotDatabase)
            {
                result.Errors.Add("- The column '" + column + "' is present in the spreadsheet file but not in the image set." + Environment.NewLine);
            }

            int classificationIndex = -1;
            int dateTimeIndex = -1;
            int deleteFlagIndex = -1;
            int fileNameIndex = -1;
            int relativePathIndex = -1;
            int utcOffsetIndex = -1;
            List<ControlRow> userControlsInFileOrder = new List<ControlRow>(columnsFromFileHeader.Count - Constant.Control.StandardControls.Count);
            List<int> userControlIndices = new List<int>(columnsFromFileHeader.Count - Constant.Control.StandardControls.Count);
            for (int columnIndex = 0; columnIndex < columnsFromFileHeader.Count; ++columnIndex)
            {
                string column = columnsFromFileHeader[columnIndex];
                if (String.Equals(column, Constant.FileColumn.Classification, StringComparison.Ordinal))
                {
                    classificationIndex = columnIndex;
                }
                else if (String.Equals(column, Constant.FileColumn.DateTime, StringComparison.Ordinal))
                {
                    dateTimeIndex = columnIndex;
                }
                else if (String.Equals(column, Constant.FileColumn.DeleteFlag, StringComparison.Ordinal))
                {
                    deleteFlagIndex = columnIndex;
                }
                else if (String.Equals(column, Constant.FileColumn.File, StringComparison.Ordinal))
                {
                    fileNameIndex = columnIndex;
                }
                else if (String.Equals(column, Constant.FileColumn.RelativePath, StringComparison.Ordinal))
                {
                    relativePathIndex = columnIndex;
                }
                else if (String.Equals(column, Constant.FileColumn.UtcOffset, StringComparison.Ordinal))
                {
                    utcOffsetIndex = columnIndex;
                }
                else
                {
                    userControlsInFileOrder.Add(controlsByColumn[column]);
                    userControlIndices.Add(columnIndex);
                }
            }

            if (fileNameIndex == -1)
            {
                result.Errors.Add("- The column '" + Constant.FileColumn.File + "' must be present in the spreadsheet file." + Environment.NewLine);
            }
            if (relativePathIndex == -1)
            {
                result.Errors.Add("- The column '" + Constant.FileColumn.RelativePath + "' must be present in the spreadsheet file." + Environment.NewLine);
            }
            if (dateTimeIndex == -1)
            {
                result.Errors.Add("- The column '" + Constant.FileColumn.DateTime + "' must be present in the spreadsheet file." + Environment.NewLine);
            }
            if (utcOffsetIndex == -1)
            {
                result.Errors.Add("- The column '" + Constant.FileColumn.UtcOffset + "' must be present in the spreadsheet file." + Environment.NewLine);
            }

            if (result.Errors.Count > 0)
            {
                return result;
            }

            // read data for file from the .csv or .xlsx file
            Dictionary<string, Dictionary<string, ImageRow>> filesAlreadyInDatabaseByRelativePath = fileDatabase.Files.GetFilesByRelativePathAndName();
            List<ImageRow> filesToInsert = new List<ImageRow>();
            List<ImageRow> filesToUpdate = new List<ImageRow>();
            int filesUnchanged = 0;
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

                // determine whether a new file needs to be added or if this row corresponds to a file already in the image set
                // For now, it's assumed all renames or moves are done through Carnassial and hence relative path + file name form 
                // an immutable (and unique) ID.
                string fileName = row[fileNameIndex];
                if (String.IsNullOrWhiteSpace(fileName))
                {
                    result.Errors.Add(String.Format("No file name found in row {0}.  Row skipped, database will not be updated.", row));
                    continue;
                }

                string relativePath = row[relativePathIndex];
                if (relativePathFromDatabaseToSpreadsheet != null)
                {
                    relativePath = Path.Combine(relativePathFromDatabaseToSpreadsheet, relativePath);
                }

                bool addFile = false;
                if ((filesAlreadyInDatabaseByRelativePath.TryGetValue(relativePath, out Dictionary<string, ImageRow> filesInFolder) == false) ||
                    (filesInFolder.TryGetValue(fileName, out ImageRow file) == false))
                {
                    addFile = true;
                    file = fileDatabase.Files.CreateAndAppendFile(fileName, relativePath);
                }

                // obtain file's date time and UTC offset
                if (DateTimeHandler.TryParseDatabaseDateTime(row[dateTimeIndex], out DateTime dateTime))
                {
                    if (DateTimeHandler.TryParseDatabaseUtcOffsetString(row[utcOffsetIndex], out TimeSpan utcOffset))
                    {
                        file.DateTimeOffset = DateTimeHandler.FromDatabaseDateTimeOffset(dateTime, utcOffset);
                    }
                    else
                    {
                        result.Errors.Add(String.Format("Value '{0}' is not valid for the column {1} of file {2}.  Neither the file's date time nor UTC offset will be updated.", row[classificationIndex], Constant.FileColumn.UtcOffset, fileName));
                    }
                }
                else
                {
                    result.Errors.Add(String.Format("Value '{0}' is not valid for the column {1} of file {2}.  File's UTC offset will be ignored and neither its date time nor UTC offset will be updated.", row[classificationIndex], Constant.FileColumn.DateTime, fileName));
                }

                // remaining standard controls
                if (classificationIndex != -1)
                {
                    if (ImageRow.TryParseFileClassification(row[classificationIndex], out FileClassification classification))
                    {
                        file.Classification = classification;
                    }
                    else
                    {
                        result.Errors.Add(String.Format("Value '{0}' is not valid for the column {1}.", row[classificationIndex], Constant.FileColumn.Classification));
                    }
                }

                if (deleteFlagIndex != -1)
                {
                    if (Boolean.TryParse(row[deleteFlagIndex], out bool deleteFlag))
                    {
                        file.DeleteFlag = deleteFlag;
                    }
                    else
                    {
                        result.Errors.Add(String.Format("Value '{0}' is not valid for the column {1}.", row[deleteFlagIndex], Constant.FileColumn.DeleteFlag));
                    }
                }

                // get values of user defined columns
                for (int userControlIndex = 0; userControlIndex < userControlsInFileOrder.Count; ++userControlIndex)
                {
                    ControlRow userControl = userControlsInFileOrder[userControlIndex];
                    int columnIndex = userControlIndices[userControlIndex];
                    string column = columnsFromFileHeader[columnIndex];
                    string valueAsString = row[columnIndex];

                    if (userControl.IsValidExcelData(valueAsString, out object value))
                    {
                        file[column] = value;
                    }
                    else
                    {
                        // if value wasn't processed by a previous clause it's invalid (or there's a parsing bug)
                        result.Errors.Add(String.Format("Value '{0}' is not valid for the column {1}.", valueAsString, column));
                    }
                }

                if (addFile)
                {
                    filesToInsert.Add(file);
                }
                else if (file.HasChanges)
                {
                    filesToUpdate.Add(file);
                }
                else
                {
                    ++filesUnchanged;
                }
            }

            // perform inserts and updates
            int totalFiles = filesToInsert.Count + filesToUpdate.Count;
            this.status.BeginTransactionCommit(totalFiles);
            if (filesToInsert.Count > 0)
            {
                using (FileTransactionSequence insertFiles = fileDatabase.CreateInsertFileTransaction())
                {
                    insertFiles.AddFiles(filesToInsert);
                    insertFiles.Commit();
                }
                this.status.Report(filesToInsert.Count);
            }
            if (filesToUpdate.Count > 0)
            {
                using (FileTransactionSequence updateFiles = fileDatabase.CreateUpdateFileTransaction())
                {
                    updateFiles.AddFiles(filesToInsert);
                    updateFiles.Commit();
                }
                this.status.Report(totalFiles);
            }

            result.FilesAdded = filesToInsert.Count;
            result.FilesProcessed = filesToInsert.Count + filesToUpdate.Count + filesUnchanged;
            result.FilesUpdated = filesToUpdate.Count;
            return result;
        }

        public FileImportResult TryImportFileData(string spreadsheetFilePath, FileDatabase fileDatabase)
        {
            try
            {
                if (spreadsheetFilePath.EndsWith(Constant.File.ExcelFileExtension, StringComparison.OrdinalIgnoreCase))
                {
                    return this.TryImportFileDataFromXlsx(spreadsheetFilePath, fileDatabase);
                }
                else
                {
                    return this.TryImportFileDataFromCsv(spreadsheetFilePath, fileDatabase);
                }
            }
            catch (IOException ioException)
            {
                return new FileImportResult()
                {
                    Exception = ioException
                };
            }
        }

        private FileImportResult TryImportFileDataFromCsv(string csvFilePath, FileDatabase fileDatabase)
        {
            using (FileStream stream = new FileStream(csvFilePath, FileMode.Open, FileAccess.Read, FileShare.Read))
            {
                using (StreamReader csvReader = new StreamReader(stream))
                {
                    this.status.BeginCsvRead(csvReader.BaseStream.Length);
                    FileImportResult result = this.TryImportFileData(fileDatabase, () => { return this.ReadAndParseCsvLine(csvReader); }, csvFilePath);
                    return result;
                }
            }
        }

        private FileImportResult TryImportFileDataFromXlsx(string xlsxFilePath, FileDatabase fileDatabase)
        {
            try
            {
                using (ExcelPackage xlsxFile = new ExcelPackage(new FileInfo(xlsxFilePath)))
                {
                    this.status.BeginExcelWorksheetLoad();
                    ExcelWorksheet worksheet = xlsxFile.Workbook.Worksheets.FirstOrDefault(sheet => String.Equals(sheet.Name, Constant.Excel.FileDataWorksheetName, StringComparison.Ordinal));
                    if (worksheet == null)
                    {
                        FileImportResult worksheetNotFound = new FileImportResult();
                        worksheetNotFound.Errors.Add(String.Format("Worksheet {0} not found.", Constant.Excel.FileDataWorksheetName));
                        return worksheetNotFound;
                    }

                    // cache worksheet dimensions
                    // Profiling shows EPPlus 4.5.2.1 doesn't cache these values. worksheet.Cells.{Columns, Rows} can't be 
                    // used instead as they indicate much larger values.
                    ExcelAddressBase dimension = worksheet.Dimension;

                    this.status.BeginExcelWorkbookRead(dimension.Rows);
                    int row = 0;
                    FileImportResult result = this.TryImportFileData(fileDatabase, () => { return this.ReadXlsxRow(worksheet, dimension, ++row); }, xlsxFilePath);
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

        protected List<string> ReadXlsxRow(ExcelWorksheet worksheet, ExcelAddressBase dimension, int row)
        {
            if (dimension.Rows < row)
            {
                return null;
            }

            int columns = dimension.Columns;
            List<string> rowContent = new List<string>(columns);
            for (int column = 1; column <= columns; ++column)
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
