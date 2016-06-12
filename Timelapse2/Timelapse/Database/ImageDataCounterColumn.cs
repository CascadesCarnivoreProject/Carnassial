using System;
using System.Data;

namespace Timelapse.Database
{
    public class ImageDataCounterColumn : ImageDataColumn
    {
        public ImageDataCounterColumn(DataRow templateTableRow)
            : base(templateTableRow)
        {
        }

        public override bool IsContentValid(string value)
        {
            long result;
            return Int64.TryParse(value, out result);
        }
    }
}
