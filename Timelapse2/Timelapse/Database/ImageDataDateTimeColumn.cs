using System;
using Timelapse.Util;

namespace Timelapse.Database
{
    public class ImageDataDateTimeColumn : ImageDataColumn
    {
        public ImageDataDateTimeColumn(ControlRow control)
            : base(control)
        {
        }

        public override bool IsContentValid(string value)
        {
            DateTime dateTime;
            return DateTimeHandler.TryParseDatabaseDateTime(value, out dateTime);
        }
    }
}
