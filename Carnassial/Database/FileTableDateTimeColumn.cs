﻿using Carnassial.Util;
using System;

namespace Carnassial.Database
{
    public class FileTableDateTimeColumn : FileTableColumn
    {
        public FileTableDateTimeColumn(ControlRow control)
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
