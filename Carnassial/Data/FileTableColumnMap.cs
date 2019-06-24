using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

namespace Carnassial.Data
{
    public class FileTableColumnMap
    {
        public FileTableColumn[] UserCounters { get; private set; }
        public FileTableColumn[] UserFlags { get; private set; }
        public List<string>[] UserNoteAndChoiceValues { get; private set; }
        public FileTableColumn[] UserNotesAndChoices { get; private set; }

        public FileTableColumnMap(FileTable fileTable)
        {
            this.UserCounters = new FileTableColumn[fileTable.UserCounters];
            this.UserFlags = new FileTableColumn[fileTable.UserFlags];
            this.UserNoteAndChoiceValues = new List<string>[fileTable.UserNotesAndChoices];
            this.UserNotesAndChoices = new FileTableColumn[fileTable.UserNotesAndChoices];

            foreach (FileTableColumn userColumn in fileTable.UserColumnsByName.Values)
            {
                switch (userColumn.Control.ControlType)
                {
                    case ControlType.Counter:
                        this.UserCounters[userColumn.DataIndex] = userColumn;
                        break;
                    case ControlType.FixedChoice:
                        this.UserNotesAndChoices[userColumn.DataIndex] = userColumn;

                        List<string> choiceValues = userColumn.Control.GetWellKnownValues();
                        if (choiceValues.Contains(userColumn.Control.DefaultValue, StringComparer.Ordinal) == false)
                        {
                            // back compat: prior to Carnassial 2.2.0.3 the editor didn't require a choice's default value also
                            // be a well known value, so include the default as an acceptable value if it's not a well known value
                            choiceValues.Add(userColumn.Control.DefaultValue);
                        }
                        this.UserNoteAndChoiceValues[userColumn.DataIndex] = choiceValues;
                        break;
                    case ControlType.Flag:
                        this.UserFlags[userColumn.DataIndex] = userColumn;
                        break;
                    case ControlType.Note:
                        this.UserNotesAndChoices[userColumn.DataIndex] = userColumn;
                        this.UserNoteAndChoiceValues[userColumn.DataIndex] = null;
                        break;
                    default:
                        throw new NotSupportedException(String.Format(CultureInfo.CurrentCulture, "Unhandled control type {0} for column {1}.", userColumn.Control.ControlType, userColumn.Control.DataLabel));
                }
            }
        }
    }
}
