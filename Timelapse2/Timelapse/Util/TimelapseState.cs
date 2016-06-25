using System;
using System.Windows;
using System.Windows.Input;
using Timelapse.Database;

namespace Timelapse.Util
{
    // A class that tracks various states and flags.
    public class TimelapseState
    {
        private int keyRepeatCount;
        private KeyEventArgs mostRecentKey;

        public bool AudioFeedback { get; set; }
        public Point ControlWindowSize { get; set; }
        public int DarkPixelThreshold { get; set; }
        public double DarkPixelRatioThreshold { get; set; }
        public ImageFilter ImageFilter { get; set; }
        public bool ImageNavigatorSliderDragging { get; set; }
        public bool IsMouseOverCounter { get; set; }
        public DateTime MostRecentDragEvent { get; set; }
        public MostRecentlyUsedList<string> MostRecentImageSets { get; set; }
        public int RepeatedKeyAcceptanceInterval { get; private set; }
        public bool ShowCsvDialog { get; set; }

        public TimelapseState()
        {
            this.AudioFeedback = false;

            // thresholds used for determining image darkness
            this.DarkPixelThreshold = Constants.Images.DarkPixelThresholdDefault;
            this.DarkPixelRatioThreshold = Constants.Images.DarkPixelRatioThresholdDefault;

            this.ControlWindowSize = new Point(0, 0);
            this.ImageFilter = ImageFilter.All;
            this.ImageNavigatorSliderDragging = false;
            this.IsMouseOverCounter = false;
            this.keyRepeatCount = 0;
            this.MostRecentDragEvent = DateTime.UtcNow - Constants.Throttles.DesiredIntervalBetweenRenders;
            this.MostRecentImageSets = null;
            this.mostRecentKey = null;
            this.RepeatedKeyAcceptanceInterval = (int)(((double)SystemParameters.KeyboardSpeed + 0.5 * Constants.Throttles.DesiredMaximumImageRendersPerSecond) / Constants.Throttles.DesiredMaximumImageRendersPerSecond);
            this.ShowCsvDialog = true;
        }

        public int GetKeyRepeatCount(KeyEventArgs key)
        {
            if (key.IsRepeat && this.mostRecentKey.IsRepeat && (key.Key == this.mostRecentKey.Key))
            {
                ++this.keyRepeatCount;
            }
            else
            {
                this.keyRepeatCount = 0;
            }
            this.mostRecentKey = key;
            return this.keyRepeatCount;
        }
    }
}
