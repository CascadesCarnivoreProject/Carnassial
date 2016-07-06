using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;
using System.Windows.Threading;
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
        public Uri Source
        {
            get
            {
                return VidPlayer.Source;
            }
            set
            {
                this.VidPlayer.Source = value;
                if (value == null)
                {
                    this.Title = "Video Player: No Video Available";
                    this.SetControlVisibilityAndFeedback();
                    return;
                }
                this.fileName = System.IO.Path.GetFileName(value.LocalPath);
                this.SetControlVisibilityAndFeedback();
                if (this.VidPlayer.IsLoaded && this.IsVideo())
                {
                    this.Play();
                    this.Title = "Video Player: " + this.fileName;
                }
                else
                {
                    this.Title = "Video Player: " + this.fileName + " is not a Video";
                }
            }
        }
        #endregion

        #region Private Variables
        private DispatcherTimer timer = new DispatcherTimer();
        private string fileName = String.Empty; 
        #endregion

        #region Initializing, loading and unloading
        public DialogVideoPlayer(Window owner)
        {
            if (owner == null)
            {
                throw new ArgumentNullException("owner");
            }
            this.InitializeComponent();
            this.Owner = owner;
            this.timer.Interval = TimeSpan.FromSeconds(1);
            this.timer.Tick += this.Timer_Tick;
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

        #region Button and Slider callbacks
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
            lblStatus.Content = String.Format("{0} / {1}", VidPlayer.Position.ToString(@"mm\:ss"), VidPlayer.NaturalDuration.TimeSpan.ToString(@"mm\:ss"));
        }
        #endregion

        // Show the current play position in the ScrollBar and TextBox, if possible.
        private void ShowPosition()
        {
            if (this.VidPlayer.NaturalDuration.HasTimeSpan)
            {
                this.sldrPosition.Maximum = this.VidPlayer.NaturalDuration.TimeSpan.TotalSeconds;
                this.sldrPosition.Value = this.VidPlayer.Position.TotalSeconds;
                this.lblStatus.Content = String.Format("{0} / {1}", this.VidPlayer.Position.ToString(@"mm\:ss"), this.VidPlayer.NaturalDuration.TimeSpan.ToString(@"mm\:ss"));
            }
        }

        // If the video player is there with a source video, show the video controls
        // Otherwise hide the controls, and include feedback that there is no video to show.
        private void SetControlVisibilityAndFeedback()
        {
            bool stateCanPlay = this.Source != null && VidPlayer.IsLoaded && this.IsVideo();
            if (this.Source == null)
            {
                this.txtblockNoVideo.Text = "Video, but Unavailable" + Environment.NewLine + this.fileName;
            }
            else if (stateCanPlay == false)     
            {
                this.timer.Stop();
                this.txtblockNoVideo.Text = "Image, not Video" + Environment.NewLine + this.fileName;
            }
            this.VidPlayer.Visibility = stateCanPlay ? Visibility.Visible : Visibility.Hidden;
            this.EmptyPlayer.Visibility = stateCanPlay ? Visibility.Hidden : Visibility.Visible;
            this.VideoPlayerControls.Visibility = stateCanPlay ? Visibility.Visible : Visibility.Hidden;
        }

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

        // Returns if the current image is a video
        private bool IsVideo()
        {
            return !System.IO.Path.GetExtension(this.fileName).Equals(".jpg", StringComparison.CurrentCultureIgnoreCase);
        }
    }
}