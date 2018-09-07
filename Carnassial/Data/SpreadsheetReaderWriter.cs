using Carnassial.Interop;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Spreadsheet;
using OfficeOpenXml;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Xml;

namespace Carnassial.Data
{
    /// <summary>
    /// Import and export .csv and .xlsx files.
    /// </summary>
    // Approximate performance, Carnassial 2.2.0.3 @ 65k rows, 16 cells/row:
    // import .csv  - 240k rows/second
    //        .xlsx - 33k rows/second
    // export .csv  - 125k rows/second
    //        .xlsx - 5.7k rows/second
    public class SpreadsheetReaderWriter
    {
        private readonly List<string> currentRow;
        private readonly SpreadsheetReadWriteStatus status;

        public SpreadsheetReaderWriter(Action<SpreadsheetReadWriteStatus> onProgressUpdate, TimeSpan desiredProgressUpdateInterval)
        {
            // a pre-populated row list avoids calling into GC on each row and calling List<>.Add() for each column
            // Multiple rows can be used for thread safety if multithreaded reads become supported.
            this.currentRow = new List<string>();
            this.status = new SpreadsheetReadWriteStatus(onProgressUpdate, desiredProgressUpdateInterval);
        }

        private unsafe string EscapeForCsv(string value)
        {
            if (value == null)
            {
                return null;
            }

            fixed (char* valueCharacters = value)
            {
                bool escape = false;
                bool replaceQuotes = false;
                for (int index = 0; index < value.Length; ++index)
                {
                    // commas, double quotation marks, line feeds (\n), and carriage returns (\r) require leading and ending double quotation marks be added
                    // double quotation marks within the field also have to be escaped as double quotes
                    // Carriage returns and line feeds are rare and often not supported in .csv code.
                    char character = *(valueCharacters + index);
                    escape |= (character == '"') || (character == ',') || (character == '\n') || (character == '\r');
                    replaceQuotes |= character == '"';
                    if (replaceQuotes)
                    {
                        // in most cases escaping is not required and this loop will run to the full length of the string
                        // There's therefore little to negative value in more complicated logic which would allow breaking on non-quote
                        // characters.
                        break;
                    }
                }

                if (replaceQuotes)
                {
                    value = value.Replace("\"", "\"\"");
                }
                if (escape)
                {
                    return "\"" + value + "\"";
                }
                return value;
            }
        }

        /// <summary>
        /// Export all data for the selected files to the .csv file indicated.
        /// </summary>
        public void ExportFileDataToCsv(FileDatabase database, string csvFilePath)
        {
            this.status.BeginWrite(database.Files.RowCount);

            using (TextWriter fileWriter = new StreamWriter(csvFilePath, false, Encoding.UTF8))
            {
                // write the header as defined by the data labels in the template file
                // The append sequence results in a trailing comma which is retained when writing the line.
                List<string> columns = new List<string>(database.Controls.RowCount);
                foreach (ControlRow control in database.Controls.InSpreadsheetOrder())
                {
                    columns.Add(control.DataLabel);
                    fileWriter.Write(this.EscapeForCsv(control.DataLabel));
                    fileWriter.Write(',');

                    if (control.Type == ControlType.Counter)
                    {
                        string markerColumn = FileTable.GetMarkerPositionColumnName(control.DataLabel);
                        columns.Add(markerColumn);
                        fileWriter.Write(this.EscapeForCsv(markerColumn));
                        fileWriter.Write(',');
                    }
                }
                fileWriter.Write(fileWriter.NewLine);

                for (int fileIndex = 0, mostRecentReportCheck = 0; fileIndex < database.Files.RowCount; ++fileIndex)
                {
                    ImageRow file = database.Files[fileIndex];
                    foreach (string dataLabel in columns)
                    {
                        fileWriter.Write(this.EscapeForCsv(file.GetExcelString(dataLabel)));
                        fileWriter.Write(',');
                    }
                    fileWriter.Write(fileWriter.NewLine);

                    if (fileIndex - mostRecentReportCheck > Constant.File.RowsBetweenStatusReportChecks)
                    {
                        if (this.status.ShouldReport())
                        {
                            this.status.Report(fileIndex);
                        }
                        mostRecentReportCheck = fileIndex;
                    }
                }
            }

            this.status.Report(database.Files.RowCount);
        }

        /// <summary>
        /// Export all data for the selected files to the .xlsx file indicated.
        /// </summary>
        public void ExportFileDataToXlsx(FileDatabase fileDatabase, string xlsxFilePath)
        {
            this.status.BeginExcelLoad(0);
            using (ExcelPackage xlsxFile = new ExcelPackage(new FileInfo(xlsxFilePath)))
            {
                List<string> columns = new List<string>(fileDatabase.Controls.RowCount);
                foreach (ControlRow control in fileDatabase.Controls.InSpreadsheetOrder())
                {
                    columns.Add(control.DataLabel);
                    if (control.Type == ControlType.Counter)
                    {
                        columns.Add(FileTable.GetMarkerPositionColumnName(control.DataLabel));
                    }
                }

                using (ExcelWorksheet worksheet = this.GetOrCreateTrimmedWorksheet(xlsxFile, Constant.Excel.FileDataWorksheetName, fileDatabase.Files.RowCount, columns))
                {
                    this.status.BeginWrite(fileDatabase.Files.RowCount);
                    for (int fileIndex = 0; fileIndex < fileDatabase.Files.RowCount; ++fileIndex)
                    {
                        ImageRow file = fileDatabase.Files[fileIndex];
                        for (int columnIndex = 0; columnIndex < columns.Count; ++columnIndex)
                        {
                            string column = columns[columnIndex];
                            // Profiling shows comparable speed for ExcelWorksheet.Cells[] and ExcelWorksheet.SetValue() but suggests
                            // .Cells may be somewhat preferable.  Code review of the two paths is ambiguous so use .Cells for now.
                            worksheet.Cells[fileIndex + 2, columnIndex + 1].Value = file.GetExcelString(column);
                            // worksheet.SetValue(fileIndex + 2, columnIndex + 1, file.GetExcelString(column));
                        }

                        if (this.status.ShouldReport())
                        {
                            this.status.Report(fileIndex);
                        }
                    }

                    // make a reasonable effort at matching column widths to content
                    // For performance, the number of rows included in autofitting is restricted.  More intelligent algorithms can be
                    // adopted if needed; see https://github.com/JanKallman/EPPlus/issues/191.
                    ExcelAddressBase dimension = worksheet.Dimension;
                    int rowsToAutoFit = Math.Min(dimension.Rows, Constant.Excel.MaximumRowsToIncludeInAutoFit);
                    worksheet.Cells[1, 1, rowsToAutoFit, dimension.Columns].AutoFitColumns(Constant.Excel.MinimumColumnWidth, Constant.Excel.MaximumColumnWidth);

                    this.status.BeginExcelSave();
                    xlsxFile.Save();
                    this.status.EndExcelWorkbookSave();
                }
            }
        }

        private int GetExcelColumnIndex(string cellReference)
        {
            Debug.Assert(cellReference.Length > 1, "Cell references must contain at least two characters.");
            int index = cellReference[0] - 'A';
            if ((cellReference[1] > '9') || (cellReference[1] < '0'))
            {
                Debug.Assert(cellReference.Length > 2, "Cell references beyond column Z must contain at least three characters.");
                index = 26 * index + cellReference[1] - 'A';
                if ((cellReference[2] > '9') && (cellReference[2] < '0'))
                {
                    // as of Excel 2017, the maximum column is XFD
                    // So no need to check more than the first three characters of the cell reference.  Within
                    // Carnassial's scope it's unlikely to exceed column Z.  Column ZZ is column 676 and even less
                    // likely to be exceeded.
                    Debug.Assert(cellReference.Length > 3, "Cell references beyond column ZZ must contain at least three characters.");
                    index = 26 * index + cellReference[2] - 'A';
                }
            }
            return index;
        }

        protected ExcelWorksheet GetOrCreateTrimmedWorksheet(ExcelPackage xlsxFile, string worksheetName, int dataRows, List<string> columnHeaders)
        {
            ExcelWorksheet worksheet = xlsxFile.Workbook.Worksheets[worksheetName];
            if (worksheet == null)
            {
                // create new worksheet
                worksheet = xlsxFile.Workbook.Worksheets.Add(worksheetName);
                worksheet.View.FreezePanes(2, 1);
            }
            else
            {
                // if needed, trim existing worksheet to output size
                // Existing data will be overwritten and therefore existing cells don't need to be cleared.  Avoiding such clearing
                // meaningfully reduces overhead when writing updates to large, existing spreadsheets.
                // - Carnassial 2.2.0.3 @ 65k rows: +45% overall write performance from not clearing
                ExcelAddressBase dimension = worksheet.Dimension;
                if (dimension.Columns > columnHeaders.Count)
                {
                    // remove unneeded columns
                    worksheet.DeleteColumn(columnHeaders.Count + 1, dimension.Columns - columnHeaders.Count);
                }

                int totalRows = dataRows + 1;
                if (dimension.Rows > totalRows)
                {
                    // remove unneeded rows
                    worksheet.DeleteRow(totalRows + 1, dimension.Rows - totalRows);
                }
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

        private bool ReadAndParseCsvLine(StreamReader csvReader)
        {
            string unparsedLine = csvReader.ReadLine();
            if (unparsedLine == null)
            {
                return false;
            }

            this.currentRow.Clear();
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
                        this.currentRow.Add(String.Empty);
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
                        this.currentRow.Add(field);
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
                                this.currentRow.Add(field);
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
                this.currentRow.Add(field);
            }

            return true;
        }

        protected bool ReadXlsxRow(XmlReader worksheetReader, Stream worksheetStream, List<string> sharedStrings)
        {
            while (worksheetReader.EOF == false)
            {
                if (worksheetReader.IsStartElement(Constant.OpenXml.Element.Row))
                {
                    // new row encountered; clear any existing data in row
                    for (int index = 0; index < this.currentRow.Count; ++index)
                    {
                        this.currentRow[index] = String.Empty;
                    }

                    using (XmlReader rowReader = worksheetReader.ReadSubtree())
                    {
                        while (rowReader.EOF == false)
                        {
                            if (rowReader.NodeType != XmlNodeType.Element)
                            {
                                rowReader.Read();
                            }
                            else if (String.Equals(rowReader.Name, Constant.OpenXml.Element.Cell, StringComparison.Ordinal))
                            {
                                string cellReference = rowReader.GetAttribute(Constant.OpenXml.Attribute.CellReference);
                                Debug.Assert(cellReference.Length > 1, "Cell references must have at least two characters.");

                                // get cell's column
                                // The XML is sparse in the sense empty cells are omitted, so this is required to correctly output
                                // rows.
                                int column = this.GetExcelColumnIndex(cellReference);

                                // get cell's value
                                bool isSharedString = String.Equals(rowReader.GetAttribute(Constant.OpenXml.Attribute.CellType), Constant.OpenXml.AttributeValue.SharedStringAttribute, StringComparison.Ordinal);
                                rowReader.ReadToDescendant(Constant.OpenXml.Element.CellValue);
                                string value = rowReader.ReadElementContentAsString();

                                if (isSharedString)
                                {
                                    int sharedStringIndex = 0;
                                    for (int index = 0; index < value.Length; ++index)
                                    {
                                        char character = value[index];
                                        if ((character > '9') || (character < '0'))
                                        {
                                            throw new FormatException("Shared string index '" + value + "' is not an integer greater than or equal to zero.");
                                        }
                                        sharedStringIndex = 10 * sharedStringIndex + character - '0';
                                    }
                                    value = sharedStrings[sharedStringIndex];
                                }

                                // add cell's value to row
                                this.currentRow[column] = value;
                                rowReader.ReadEndElement();
                            }
                            else
                            {
                                rowReader.Read();
                            }
                        }
                    }
                    worksheetReader.ReadEndElement();
                    return true;
                }
                else
                {
                    worksheetReader.Read();
                }
            }
            return false;
        }

        private FileImportResult TryImportFileData(FileDatabase fileDatabase, Func<bool> readLine, Func<long> getPosition, string spreadsheetFilePath)
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
            readLine.Invoke();
            List<string> columnsFromFileHeader = new List<string>(this.currentRow);
            List<string> columnsInDatabase = new List<string>(fileDatabase.Controls.RowCount);
            foreach (ControlRow control in fileDatabase.Controls)
            {
                columnsInDatabase.Add(control.DataLabel);

                if (control.Type == ControlType.Counter)
                {
                    string markerColumn = FileTable.GetMarkerPositionColumnName(control.DataLabel);
                    columnsInDatabase.Add(markerColumn);
                }
            }

            FileImportResult result = new FileImportResult();
            List<string> columnsInDatabaseButNotInHeader = columnsInDatabase.Except(columnsFromFileHeader).ToList();
            foreach (string column in columnsInDatabaseButNotInHeader)
            {
                result.Errors.Add("- The column '" + column + "' is present in the image set but not in the spreadsheet file.");
            }

            List<string> columnsInHeaderButNotDatabase = columnsFromFileHeader.Except(columnsInDatabase).ToList();
            foreach (string column in columnsInHeaderButNotDatabase)
            {
                result.Errors.Add("- The column '" + column + "' is present in the spreadsheet file but not in the image set.");
            }

            if (result.Errors.Count > 0)
            {
                return result;
            }

            FileTableSpreadsheetMap spreadsheetMap = fileDatabase.Files.IndexSpreadsheetColumns(columnsFromFileHeader);
            if (spreadsheetMap.FileNameIndex == -1)
            {
                result.Errors.Add("- The column '" + Constant.FileColumn.File + "' must be present in the spreadsheet file.");
            }
            if (spreadsheetMap.RelativePathIndex == -1)
            {
                result.Errors.Add("- The column '" + Constant.FileColumn.RelativePath + "' must be present in the spreadsheet file.");
            }
            if (spreadsheetMap.DateTimeIndex == -1)
            {
                result.Errors.Add("- The column '" + Constant.FileColumn.DateTime + "' must be present in the spreadsheet file.");
            }
            if (spreadsheetMap.UtcOffsetIndex == -1)
            {
                result.Errors.Add("- The column '" + Constant.FileColumn.UtcOffset + "' must be present in the spreadsheet file.");
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
            int mostRecentReportCheck = 0;
            int rowsWritten = 0;
            this.status.Report(0);
            while (readLine.Invoke())
            {
                if (this.currentRow.Count == columnsInDatabase.Count - 1)
                {
                    // .csv files are ambiguous in the sense a trailing comma may or may not be present at the end of the line
                    // if the final field has a value this case isn't a concern, but if the final field has no value then there's
                    // no way for the parser to know the exact number of fields in the line
                    this.currentRow.Add(String.Empty);
                }
                else if (this.currentRow.Count != columnsInDatabase.Count)
                {
                    result.Errors.Add(String.Format("Expected {0} fields in row '{1}' but found {2}.  Row skipped, database will not be updated for this file.", columnsInDatabase.Count, String.Join(",", this.currentRow), this.currentRow.Count));
                    continue;
                }

                // determine whether a new file needs to be added or if this row corresponds to a file already in the image set
                // For now, it's assumed all renames or moves are done through Carnassial and hence relative path + file name form 
                // an immutable (and unique) ID.
                string fileName = this.currentRow[spreadsheetMap.FileNameIndex];
                if (String.IsNullOrWhiteSpace(fileName))
                {
                    result.Errors.Add(String.Format("No file name found in row {0}.  Row skipped, database will not be updated for this file.", filesToInsert.Count + filesToUpdate.Count + filesUnchanged + 1));
                    continue;
                }

                string relativePath = this.currentRow[spreadsheetMap.RelativePathIndex];
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
                Debug.Assert(addFile || (file.HasChanges == false), "Existing file unexpectedly has changes.");

                // move row data into file
                file.SetValuesFromSpreadsheet(spreadsheetMap, this.currentRow, result);

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

                ++rowsWritten;
                if (rowsWritten - mostRecentReportCheck > Constant.File.RowsBetweenStatusReportChecks)
                {
                    if (this.status.ShouldReport())
                    {
                        this.status.Report(getPosition.Invoke());
                    }
                    mostRecentReportCheck = rowsWritten;
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
                    updateFiles.AddFiles(filesToUpdate);
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
                    this.status.BeginRead(csvReader.BaseStream.Length);
                    FileImportResult result = this.TryImportFileData(fileDatabase, 
                        () => { return this.ReadAndParseCsvLine(csvReader); }, 
                        () => { return stream.Position; },
                        csvFilePath);
                    return result;
                }
            }
        }

        private FileImportResult TryImportFileDataFromXlsx(string xlsxFilePath, FileDatabase fileDatabase)
        {
            try
            {
                using (FileStream xlsxStream = new FileStream(xlsxFilePath, FileMode.Open, FileAccess.Read, FileShare.Read))
                {
                    using (SpreadsheetDocument xlsx = SpreadsheetDocument.Open(xlsxStream, false))
                    {
                        // find worksheet
                        WorkbookPart workbook = xlsx.WorkbookPart;
                        Sheet worksheetInfo = workbook.Workbook.Sheets.Elements<Sheet>().FirstOrDefault(sheet => String.Equals(sheet.Name, Constant.Excel.FileDataWorksheetName, StringComparison.Ordinal));
                        if (worksheetInfo == null)
                        {
                            FileImportResult worksheetNotFound = new FileImportResult();
                            worksheetNotFound.Errors.Add(String.Format("Worksheet {0} not found.", Constant.Excel.FileDataWorksheetName));
                            return worksheetNotFound;
                        }
                        WorksheetPart worksheet = (WorksheetPart)workbook.GetPartById(worksheetInfo.Id);

                        // load shared strings
                        List<string> sharedStrings = new List<string>();
                        using (Stream sharedStringStream = workbook.SharedStringTablePart.GetStream())
                        {
                            using (XmlReader reader = XmlReader.Create(sharedStringStream))
                            {
                                reader.MoveToContent();
                                int sharedStringCount = Int32.Parse(reader.GetAttribute(Constant.OpenXml.Attribute.CountAttribute), NumberStyles.None, CultureInfo.InvariantCulture);
                                this.status.BeginExcelLoad(sharedStringCount);

                                while (reader.EOF == false)
                                {
                                    if (reader.NodeType != XmlNodeType.Element)
                                    {
                                        reader.Read();
                                    }
                                    else if (String.Equals(reader.Name, Constant.OpenXml.Element.SharedString, StringComparison.Ordinal))
                                    {
                                        reader.ReadToDescendant(Constant.OpenXml.Element.SharedStringText);
                                        sharedStrings.Add(reader.ReadElementContentAsString());
                                        reader.ReadEndElement();

                                        if (this.status.ShouldReport())
                                        {
                                            this.status.Report(sharedStrings.Count);
                                        }
                                    }
                                    else
                                    {
                                        reader.Read();
                                    }
                                }
                            }
                        }

                        // read data from worksheet
                        using (Stream worksheetStream = worksheet.GetStream())
                        {
                            this.status.BeginRead(worksheetStream.Length);
                            using (XmlReader reader = XmlReader.Create(worksheetStream))
                            {
                                // match the length of the pre-populated Excel row to the current worksheet
                                reader.MoveToContent();
                                reader.ReadToDescendant(Constant.OpenXml.Element.Dimension);
                                string dimension = reader.GetAttribute(Constant.OpenXml.Attribute.Reference);
                                string[] range = dimension.Split(':');
                                int maximumColumnIndex = this.GetExcelColumnIndex(range[1]);

                                if (this.currentRow.Count <= maximumColumnIndex)
                                {
                                    for (int index = this.currentRow.Count; index <= maximumColumnIndex; ++index)
                                    {
                                        this.currentRow.Add(String.Empty);
                                    }
                                }
                                else if (this.currentRow.Count > maximumColumnIndex + 1)
                                {
                                    this.currentRow.RemoveRange(maximumColumnIndex + 1, this.currentRow.Count - maximumColumnIndex - 1);
                                }

                                FileImportResult result = this.TryImportFileData(fileDatabase,
                                    () => { return this.ReadXlsxRow(reader, worksheetStream, sharedStrings); },
                                    () => { return worksheetStream.Position; },
                                    xlsxFilePath);
                                return result;
                            }
                        }
                    }
                }
            }
            catch (IOException ioException)
            {
                FileImportResult result = new FileImportResult();
                result.Errors.Add(ioException.ToString());
                return result;
            }
        }
    }
}
