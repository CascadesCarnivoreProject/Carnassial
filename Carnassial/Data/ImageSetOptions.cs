﻿using System;

namespace Carnassial.Data
{
    [Flags]
    public enum ImageSetOptions
    {
        // for now, value is stored in databases as an integer
        None = 0x0,
        Magnifier = 0x1
    }
}
