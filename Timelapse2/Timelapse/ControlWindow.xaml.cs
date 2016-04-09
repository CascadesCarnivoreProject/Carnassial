using System;
using System.Windows;
using System.Windows.Threading;

namespace Timelapse
{
    /// <summary>
    /// This popup window will contain a user control that can be added or removed from it at run time
    /// Its use is to reparent the data controls as a popup
    /// The window automatically adjusts its height to fit the user control's height
    /// </summary>
    public partial class ControlWindow : Window
    {
        #region Private variables
        // This timer controls how a window size will be reset to fit its content's height as the window size is changed
        // Without the timer, it flickers terribly.
        private DispatcherTimer timer = new DispatcherTimer ();
        private const long quarterSecond = 2500000;
        private State state; // We need to access the state so we can post the current window size
        #endregion 

        #region Constructors
        public ControlWindow(State state)
        {
            this.timer.Tick += timer_Tick;
            this.timer.Interval = new System.TimeSpan(quarterSecond);

            // Restore the window size 
            this.state = state;
            InitializeComponent();
        }
        #endregion

        #region Public Methods
        /// <summary> Add the myControl user control to this window  </summary>
        /// <param name="myControls"></param>
        public void AddControls (Controls myControls)
        {
            this.TopLevelGrid.Children.Add (myControls);
        }

        /// <summary> Remove the myControl user control from this window  </summary>
        /// <param name="myControls"></param>
        public void ChildRemove (Controls myControls)
        {
            this.TopLevelGrid.Children.Remove(myControls);
        }

        // This will size the window so its the same as its last size
        public void RestorePreviousSize ()
        {
            if (state.controlWindowSize.X != 0 && state.controlWindowSize.Y != 0)
            {
                this.Width = state.controlWindowSize.X;
                this.Height = state.controlWindowSize.Y;
            }
        }
        
        #endregion 

        #region Private Methods
        // Reset the timer so that a timeout isn't triggered until the size change is  complete (or if the user pauses resizing)
        private void Window_SizeChanged(object sender, SizeChangedEventArgs e)
        {
            timer.Start();
            this.timer.Interval = new System.TimeSpan(quarterSecond);
        }

        // Resize the window to fit its content
        private void timer_Tick(object sender, EventArgs e)
        {
            this.SizeToContent = SizeToContent.Height;
            timer.Stop ();
            state.controlWindowSize = new Point(this.ActualWidth, this.ActualHeight);
        }
        #endregion
    }
}
