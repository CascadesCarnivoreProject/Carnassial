using System;

namespace Carnassial.Database
{
    public class FileTableFlagColumn : FileTableColumn
    {
        public FileTableFlagColumn(ControlRow control)
            : base(control)
        {
        }

        public override bool IsContentValid(string value)
        {
            return String.Equals(value, Boolean.FalseString, StringComparison.OrdinalIgnoreCase) ||
                   String.Equals(value, Boolean.TrueString, StringComparison.OrdinalIgnoreCase);
        }
    }
}
