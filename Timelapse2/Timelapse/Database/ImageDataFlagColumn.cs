using System;
using System.Data;

namespace Timelapse.Database
{
    public class ImageDataFlagColumn : ImageDataColumn
    {
        public ImageDataFlagColumn(ControlRow control)
            : base(control)
        {
        }

        public override bool IsContentValid(string value)
        {
            return String.Equals(value, Constants.Boolean.False, StringComparison.OrdinalIgnoreCase) ||
                   String.Equals(value, Constants.Boolean.True, StringComparison.OrdinalIgnoreCase);
        }
    }
}
