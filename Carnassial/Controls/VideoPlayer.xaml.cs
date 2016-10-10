using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Threading;

namespace Carnassial.Controls
{
    public partial class VideoPlayer : UserControl
    {
        private bool isProgrammaticUpdate;
        private DispatcherTimer timer;

        public VideoPlayer()
        {
            this.InitializeComponent();
            this.isProgrammaticUpdate = false;
            this.timer = new DispatcherTimer();
            this.timer.Interval = TimeSpan.FromMilliseconds(500.0);
            this.timer.Tick += this.Timer_Tick;
        }

        private void Pause()
        {
            this.PlayOrPause.IsChecked = false;
            this.timer.Stop();
            this.Video.Pause();
        }

        private void Play()
        {
            this.PlayOrPause.IsChecked = true;

            this.timer.Start();
            this.Video.Play();
        }

        private void PlayOrPause_Click(object sender, RoutedEventArgs e)
        {
            if (this.PlayOrPause.IsChecked == true)
            {
                this.Play();
            }
            else
            {
                this.Pause();
            }
        }

        // Reset the video to the beginning
        public void Reset()
        {
            this.VideoPosition.Value = 0;
            this.Pause();
        }

        public void SetSource(Uri source)
        {
            this.Video.Source = source;

            // MediaElement seems only deterministic about displaying the first frame when LoadedBehaviour is set to Pause, which isn't helpful as calls to
            // Play() then have no effect.  This is a well known issue with various folks getting results.  The below combination of Play(), Pause() and Position
            // seems to work, though neither Pause() or Position is sufficent on its own and black frames still get rendered if Position is set to zero or
            // an especially small value.
            double originalVolume = this.Video.Volume;
            this.Video.Volume = 0.0;
            this.Video.Play();
            this.Video.Pause();
            this.Video.Position = TimeSpan.FromMilliseconds(1.0);
            this.Video.Volume = originalVolume;
        }

        // Show the current play position in the ScrollBar and TextBox, if possible.
        private void ShowPosition()
        {
            this.isProgrammaticUpdate = true;
            if (this.Video.NaturalDuration.HasTimeSpan)
            {
                this.VideoPosition.Maximum = this.Video.NaturalDuration.TimeSpan.TotalSeconds;
            }
            this.VideoPosition.Value = this.Video.Position.TotalSeconds;
            this.isProgrammaticUpdate = false;
        }

        private void Timer_Tick(object sender, EventArgs e)
        {
            if (this.Video.Source != null)
            {
                this.ShowPosition();
            }
        }

        public bool TryPlayOrPause()
        {
            if (this.Visibility != Visibility.Visible)
            {
                return false;
            }

            // WPF doesn't offer a way to fire a toggle button's click event programatically (ToggleButtonAutomationPeer -> IToggleProvider -> Toggle()
            // changes the state of the button but fails to trigger the click event) so do the equivalent in code
            this.PlayOrPause.IsChecked = !this.PlayOrPause.IsChecked;
            this.PlayOrPause_Click(this, null);
            return true;
        }

        private void Video_MediaEnded(object sender, RoutedEventArgs e)
        {
            this.Reset();
        }

        private void Video_Unloaded(object sender, RoutedEventArgs e)
        {
            this.timer.Stop();
        }

        // When the user starts moving the slider, we want to pause the video so the two actions don't interfere with each other
        private void VideoPosition_PreviewMouseDown(object sender, MouseButtonEventArgs e)
        {
           this.Pause();
        }

        // Scrub the video to the current slider position
        private void VideoPosition_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            if (this.isProgrammaticUpdate)
            {
                return;
            }

            TimeSpan videoPosition = TimeSpan.FromSeconds(this.VideoPosition.Value);
            this.Video.Position = videoPosition;
            this.ShowPosition();
        }
    }
}