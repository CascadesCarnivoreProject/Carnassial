using System;
using System.Windows;
using System.Windows.Threading;
using Timelapse.Util;

namespace Timelapse
{
    /// <summary>
    /// This popup window will contain a user control that can be added or removed from it at run time.
    /// Its use is to place image data entry controls in a separate rather than within the main Timelapse window.
    /// The window automatically adjusts its height to fit the user controls' height.
    /// </summary>
    public partial class ControlWindow : Window
    {
        private static readonly TimeSpan QuarterSecond = TimeSpan.FromSeconds(0.25);

        // This timer controls how a window size will be reset to fit its content's height as the window size is changed
        // Without the timer, it flickers terribly.
        private DispatcherTimer timer = new DispatcherTimer();
        private TimelapseState state; // We need to access the state so we can post the current window size

        public ControlWindow(TimelapseState state)
        {
            this.timer.Tick += this.Timer_Tick;
            this.timer.Interval = ControlWindow.QuarterSecond;

            // Restore the window size 
            this.state = state;
            this.InitializeComponent();
        }

        #region Public Methods
        /// <summary>Add the user control to this window</summary>
        public void AddControls(Controls myControls)
        {
            this.TopLevelGrid.Children.Add(myControls);
        }

        /// <summary>Remove the user control from this window</summary>
        public void ChildRemove(Controls myControls)
        {
            this.TopLevelGrid.Children.Remove(myControls);
        }

        // This will size the window so its the same as its last size
        public void RestorePreviousSize()
        {
            if (this.state.ControlWindowSize.X != 0 && this.state.ControlWindowSize.Y != 0)
            {
                this.Width = this.state.ControlWindowSize.X;
                this.Height = this.state.ControlWindowSize.Y;
            }
        }
        #endregion

        #region Private Methods
        // Reset the timer so that a timeout isn't triggered until the size change is  complete (or if the user pauses resizing)
        private void Window_SizeChanged(object sender, SizeChangedEventArgs e)
        {
            this.timer.Start();
            this.timer.Interval = ControlWindow.QuarterSecond;
        }

        // Resize the window to fit its content
        private void Timer_Tick(object sender, EventArgs e)
        {
            this.SizeToContent = SizeToContent.Height;
            this.timer.Stop();
            this.state.ControlWindowSize = new Point(this.ActualWidth, this.ActualHeight);
        }
        #endregion
    }
}
