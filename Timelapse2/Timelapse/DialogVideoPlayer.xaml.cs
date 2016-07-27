using System;
using System.Windows;
using System.Windows.Input;
using System.Windows.Threading;
using Timelapse.Database;
using Timelapse.Util;

namespace Timelapse
{
    /// <summary>
    /// Interaction logic for DialogVideoPlayer.xaml
    /// </summary>
    public partial class DialogVideoPlayer : Window
    {
        #region Public Properties
        // Set the video to display a file.
        // If its a video file, start it playing
        public ImageRow CurrentRow
        {
            get
            {
                return this.currentRow;
            }
            set
            {
                this.currentRow = value;
                this.VidPlayer.Source = new System.Uri(this.currentRow.GetImagePath(this.folderPath));
                this.fileName = this.currentRow.FileName;
                this.timer.Stop();
                this.SetControlVisibilityAndFeedback();
                if (this.currentRow.IsDisplayable() && this.currentRow.IsVideo)
                {
                    this.sldrPosition.Value = 0;
                    this.ShowPosition();
                    this.Play();
                }
            }
        }
        
        public Uri Source
        {
            get
            {
                return VidPlayer.Source;
            }
            set
            {
                this.VidPlayer.Source = value;
            }
        }
        #endregion

        #region Private Variables
        private DispatcherTimer timer = new DispatcherTimer();
        private string folderPath = String.Empty;
        private string fileName = String.Empty;
        private ImageRow currentRow;
        private const double HALFSECOND = 0.5;
        #endregion

        #region Initializing, loading and unloading
        public DialogVideoPlayer(Window owner, string folderPath)
        {
            if (owner == null)
            {
                throw new ArgumentNullException("owner");
            }
            this.InitializeComponent();
            this.Owner = owner;
            this.folderPath = folderPath;
            this.timer.Interval = TimeSpan.FromSeconds(HALFSECOND);
            this.timer.Tick += this.Timer_Tick;
            this.VidPlayer.MediaEnded += this.VidPlayer_MediaEnded;
        }

        private void VidPlayer_MediaEnded(object sender, RoutedEventArgs e)
        {
            this.Reset();
        }

        // When the video player is loaded, try to start playing the video
        private void VidPlayer_Loaded(object sender, RoutedEventArgs e)
        {
            Utilities.SetDefaultDialogPosition(this);
            Utilities.TryFitWindowInWorkingArea(this);
            this.Play();
        }

        // When the VidPlayer is unloaded, stop updating the display
        private void VidPlayer_Unloaded(object sender, RoutedEventArgs e)
        {
            this.timer.Stop();
        }
        #endregion

        #region Timer, Button and Slider callbacks
        private void Timer_Tick(object sender, EventArgs e)
        {
            if (this.VidPlayer.Source != null)
            {
                this.ShowPosition();
            }
            else
            {
                this.lblStatus.Content = "No Video to Play...";
            }
        }

        private void ToggleButton_Click(object sender, RoutedEventArgs e)
        {
            if (this.btnPlayPause.IsChecked == true)
            {
                this.Play();
            }
            else
            {
                this.Pause();
            }
        }

        // When the user starts moving the slider, we want to pause the video so the two actions don't interfere with each other
        private void SldrPosition_PreviewMouseDown(object sender, MouseButtonEventArgs e)
        {
           this.Pause();
        }

        // Scrub the video to the current slider position
        private void SldrPosition_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            TimeSpan timespan = TimeSpan.FromSeconds(sldrPosition.Value);
            VidPlayer.Position = timespan;
            this.ShowPosition();
        }
        #endregion

        #region Generating User Feedback
        // Show the current play position in the ScrollBar and TextBox, if possible.
        private void ShowPosition()
        {
            this.sldrPosition.Value = this.VidPlayer.Position.TotalSeconds;
            if (this.VidPlayer.NaturalDuration.HasTimeSpan)
            {
                this.sldrPosition.Maximum = this.VidPlayer.NaturalDuration.TimeSpan.TotalSeconds;
                this.lblStatus.Content = String.Format("{0} / {1}", this.VidPlayer.Position.ToString(@"mm\:ss"), this.VidPlayer.NaturalDuration.TimeSpan.ToString(@"mm\:ss"));
            }
        }

        // If the video player can show a video, show the video controls
        // Otherwise hide the controls, including feedback as to why it is not displaying a video.
        private void SetControlVisibilityAndFeedback()
        {
            bool stateCanPlay = this.Source != null && this.CurrentRow.IsVideo && this.currentRow.IsDisplayable();

            if (this.CurrentRow.IsVideo == false)  
            {
                // The file is an image
                this.txtblockNoVideo.Text = this.fileName + Environment.NewLine + "is an image";
                this.Title = "Video Player: " + this.fileName + " is an image";
            }
            else if (this.CurrentRow.IsVideo && (this.Source == null || this.currentRow.IsDisplayable() == false)) 
            {
                // The video is not displayable (i.e., its missing or corrupt)
                this.txtblockNoVideo.Text = this.fileName + Environment.NewLine + "cannot be played as it is" + Environment.NewLine + "marked as missing or corrupt.";
                this.Title = "Video Player: " + this.fileName + " cannot be played";
            }
            else if (stateCanPlay == true)     
            {
                // The video is good to go
                this.Title = "Video Player: " + this.fileName;
            }

            if (stateCanPlay == false)
            {
                this.timer.Stop();
                this.VidPlayer.Stop();
            }
            this.VidPlayer.Visibility = stateCanPlay ? Visibility.Visible : Visibility.Hidden;
            this.EmptyPlayer.Visibility = stateCanPlay ? Visibility.Hidden : Visibility.Visible;
            this.VideoPlayerControls.Visibility = stateCanPlay ? Visibility.Visible : Visibility.Hidden;
        }
        #endregion

        #region Play / Pause / Reset methods
        // Play the video, setting various UI states along the way
        private void Play()
        {
            this.btnPlayPause.IsChecked = true;
            this.SetControlVisibilityAndFeedback();
            this.timer.Start();
            this.VidPlayer.Play();
        }

        // Pause the video, setting various UI states along the way
        private void Pause()
        {
            this.btnPlayPause.IsChecked = false;
            this.SetControlVisibilityAndFeedback();
            this.timer.Stop();
            this.VidPlayer.Pause();
        }

        // Reset the video to the beginning
        private void Reset()
        {
            this.sldrPosition.Value = 0;
            this.Pause();
        }
        #endregion
    }
}