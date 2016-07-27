using System;
using System.Data;

namespace Timelapse.Database
{
    public class ImageDataCounterColumn : ImageDataColumn
    {
        public ImageDataCounterColumn(ControlRow control)
            : base(control)
        {
        }

        public override bool IsContentValid(string value)
        {
            long result;
            return Int64.TryParse(value, out result);
        }
    }
}
