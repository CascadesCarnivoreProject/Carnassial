using System;
using System.Windows;
using System.Windows.Input;

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
            // check mostRecentKey for null as key delivery is not entirely deterministic
            // it's possible WPF will send the first key as a repeat if the user holds a key down or starts typing while the main window is opening
            if (key.IsRepeat && this.mostRecentKey != null && this.mostRecentKey.IsRepeat && (key.Key == this.mostRecentKey.Key))
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
