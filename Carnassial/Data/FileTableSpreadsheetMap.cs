using Carnassial.Database;
using System;
using System.Collections.Generic;
using System.Globalization;

namespace Carnassial.Data
{
    public class FileTableSpreadsheetMap : FileTableColumnMap
    {
        public int ClassificationSpreadsheetIndex { get; set; }
        public int DateTimeSpreadsheetIndex { get; set; }
        public int DeleteFlagSpreadsheetIndex { get; set; }
        public int FileNameSpreadsheetIndex { get; set; }
        public int RelativePathSpreadsheetIndex { get; set; }
        public int UtcOffsetSpreadsheetIndex { get; set; }

        public List<int> UserCounterSpreadsheetIndices { get; private set; }
        public List<int> UserFlagSpreadsheetIndices { get; private set; }
        public List<int> UserMarkerSpreadsheetIndices { get; private set; }
        public List<int> UserNoteAndChoiceSpreadsheetIndices { get; private set; }

        public FileTableSpreadsheetMap(List<string> columnsFromSpreadsheet, FileTable fileTable)
            : base(fileTable)
        {
            this.ClassificationSpreadsheetIndex = -1;
            this.DateTimeSpreadsheetIndex = -1;
            this.DeleteFlagSpreadsheetIndex = -1;
            this.FileNameSpreadsheetIndex = -1;
            this.RelativePathSpreadsheetIndex = -1;
            this.UtcOffsetSpreadsheetIndex = -1;

            this.UserCounterSpreadsheetIndices = new List<int>(fileTable.UserCounters);
            this.UserFlagSpreadsheetIndices = new List<int>(fileTable.UserFlags);
            this.UserMarkerSpreadsheetIndices = new List<int>(fileTable.UserCounters);
            this.UserNoteAndChoiceSpreadsheetIndices = new List<int>(fileTable.UserNotesAndChoices);

            for (int spreadsheetIndex = 0; spreadsheetIndex < columnsFromSpreadsheet.Count; ++spreadsheetIndex)
            {
                string column = columnsFromSpreadsheet[spreadsheetIndex];
                if (String.Equals(column, Constant.FileColumn.Classification, StringComparison.Ordinal))
                {
                    this.ClassificationSpreadsheetIndex = spreadsheetIndex;
                }
                else if (String.Equals(column, Constant.FileColumn.DateTime, StringComparison.Ordinal))
                {
                    this.DateTimeSpreadsheetIndex = spreadsheetIndex;
                }
                else if (String.Equals(column, Constant.FileColumn.DeleteFlag, StringComparison.Ordinal))
                {
                    this.DeleteFlagSpreadsheetIndex = spreadsheetIndex;
                }
                else if (String.Equals(column, Constant.FileColumn.File, StringComparison.Ordinal))
                {
                    this.FileNameSpreadsheetIndex = spreadsheetIndex;
                }
                else if (String.Equals(column, Constant.FileColumn.RelativePath, StringComparison.Ordinal))
                {
                    this.RelativePathSpreadsheetIndex = spreadsheetIndex;
                }
                else if (String.Equals(column, Constant.FileColumn.UtcOffset, StringComparison.Ordinal))
                {
                    this.UtcOffsetSpreadsheetIndex = spreadsheetIndex;
                }
                else
                {
                    FileTableColumn userColumn = fileTable.UserColumnsByName[column];
                    switch (userColumn.Control.ControlType)
                    {
                        case ControlType.Counter:
                            if (userColumn.DataType == SqlDataType.Integer)
                            {
                                this.UserCounterSpreadsheetIndices.Add(spreadsheetIndex);
                            }
                            else if (userColumn.DataType == SqlDataType.Blob)
                            {
                                this.UserMarkerSpreadsheetIndices.Add(spreadsheetIndex);
                            }
                            else
                            {
                                throw new NotSupportedException(String.Format(CultureInfo.CurrentCulture, "Unhandled data type {0} for column {1}.", userColumn.DataType, userColumn.Control.DataLabel));
                            }
                            break;
                        case ControlType.FixedChoice:
                            this.UserNoteAndChoiceSpreadsheetIndices.Add(spreadsheetIndex);
                            break;
                        case ControlType.Flag:
                            this.UserFlagSpreadsheetIndices.Add(spreadsheetIndex);
                            break;
                        case ControlType.Note:
                            this.UserNoteAndChoiceSpreadsheetIndices.Add(spreadsheetIndex);
                            break;
                        default:
                            throw new NotSupportedException(String.Format(CultureInfo.CurrentCulture, "Unhandled control type {0} for column {1}.", userColumn.Control.ControlType, userColumn.Control.DataLabel));
                    }
                }
            }
        }
    }
}
