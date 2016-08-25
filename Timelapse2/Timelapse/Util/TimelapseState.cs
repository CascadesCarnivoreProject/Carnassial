using System;
using System.Windows.Input;

namespace Timelapse.Util
{
    // A class that tracks various states and flags.
    public class TimelapseState : TimelapseUserRegistrySettings
    {
        private int keyRepeatCount;
        private KeyEventArgs mostRecentKey;

        public byte DifferenceThreshold { get; set; } // The threshold used for calculating combined differences
        public bool ImageNavigatorSliderDragging { get; set; }
        public bool IsMouseOverCounter { get; set; }
        public DateTime MostRecentDragEvent { get; set; }
        
        public TimelapseState()
        {
            this.keyRepeatCount = 0;
            this.mostRecentKey = null;

            this.DifferenceThreshold = Constants.Images.DifferenceThresholdDefault;
            this.ImageNavigatorSliderDragging = false;
            this.IsMouseOverCounter = false;
            this.MostRecentDragEvent = DateTime.UtcNow - this.Throttles.DesiredIntervalBetweenRenders;
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
