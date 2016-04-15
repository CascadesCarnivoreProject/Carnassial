﻿using System;
using System.Collections.Generic;
using System.Windows;

namespace Timelapse
{
    // A class that tracks various states and flags.
    public class TimelapseState
    {
        public bool AudioFeedback { get; set; }
        public Point ControlWindowSize { get; set; }
        public int DarkPixelThreshold { get; set; }
        public double DarkPixelRatioThreshold { get; set; }
        public ImageQualityFilter ImageFilter { get; set; }
        public bool ImmediateExit { get; set; }
        public bool IsContentChanged { get; set; }
        public bool IsContentValueChangedFromOutside { get; set; }
        public bool IsDateTimeOrder { get; set; }
        public string IsMouseOverCounter { get; set; }
        public MostRecentlyUsedList<string> MostRecentDatabasePaths { get; set; }
        public bool ShowCsvDialog { get; set; }

        public TimelapseState()
        {
            this.AudioFeedback = false;

            // thresholds used for determining image darkness
            this.DarkPixelThreshold = Constants.DarkPixelThresholdDefault;
            this.DarkPixelRatioThreshold = Constants.DarkPixelRatioThresholdDefault;

            this.ControlWindowSize = new Point(0, 0);
            this.ImageFilter = ImageQualityFilter.All;
            this.ImmediateExit = false;
            this.IsContentChanged = false;
            this.IsMouseOverCounter = String.Empty;
            this.IsDateTimeOrder = true;
            this.IsContentValueChangedFromOutside = false;
            this.MostRecentDatabasePaths = null;
            this.ShowCsvDialog = true;
        }
    }
}
