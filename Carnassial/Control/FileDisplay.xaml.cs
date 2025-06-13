using Carnassial.Data;
using Carnassial.Images;
using Carnassial.Util;
using System;
using System.Diagnostics;
using System.IO;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;

namespace Carnassial.Control
{
    public partial class FileDisplay : UserControl
    {
        public FileDisplay()
        {
            this.InitializeComponent();
        }

        public void Clear()
        {
            this.Image.Source = null;
            this.ShowImage();
        }

        public void Display(FileInfo videoFile)
        {
            if (videoFile.Exists)
            {
                this.Video.SetSource(new Uri(videoFile.FullName));
                this.ShowVideo();
            }
            else
            {
                this.Display(Constant.Images.FileNoLongerAvailableMessage);
            }
        }

        public void Display(FileDisplayMessage message)
        {
            this.MessageHeader.Text = message.Header;
            this.MessageMidsection.Text = message.Midsection;
            this.MessageMidsection.FontSize = message.MidsectionFontSize;
            this.MessageDetail.Text = message.Detail;
            this.ShowMessage();
        }

        public void Display(CachedImage image)
        {
            if (image.ImageNotDecodable)
            {
                this.Display(Constant.Images.FileCorruptMessage);
            }
            else if (image.FileNoLongerAvailable)
            {
                this.Display(Constant.Images.FileNoLongerAvailableMessage);
            }
            else
            {
                Debug.Assert(image.Image != null, "Cached images which could not be loaded should be marked corrupt or no longer available.");
                image.Image.SetSource(this.Image);
                this.ShowImage();
            }
        }

        public async Task DisplayAsync(string folderPath, ImageCache imageCache)
        {
            CachedImage? image = imageCache.GetCurrentImage();
            if (image != null)
            {
                this.Display(image);
            }
            else
            {
                await this.DisplayAsync(folderPath, imageCache.Current).ConfigureAwait(true);
            }
        }

        public async Task DisplayAsync(string folderPath, ImageRow? file)
        {
            Debug.Assert(file != null);
            if (file.IsVideo)
            {
                this.Display(file.GetFileInfo(folderPath));
            }
            else
            {
                int expectedDisplayWidthInPixels = this.GetWidthInPixels();
                CachedImage image = await file.TryLoadImageAsync(folderPath, expectedDisplayWidthInPixels).ConfigureAwait(true);
                this.Display(image);
            }
        }

        private void ShowImage()
        {
            this.Image.Visibility = Visibility.Visible;

            if (this.Message.Visibility == Visibility.Visible)
            {
                this.Message.Visibility = Visibility.Collapsed;
            }

            if (this.Video.Visibility == Visibility.Visible)
            {
                this.Video.Pause();
                this.Video.Visibility = Visibility.Collapsed;
            }
        }

        private void ShowMessage()
        {
            if (this.Image.Visibility == Visibility.Visible)
            {
                this.Image.Visibility = Visibility.Collapsed;
            }

            this.Message.Visibility = Visibility.Visible;

            if (this.Video.Visibility == Visibility.Visible)
            {
                this.Video.Pause();
                this.Video.Visibility = Visibility.Collapsed;
            }
        }

        private void ShowVideo()
        {
            if (this.Image.Visibility == Visibility.Visible)
            {
                this.Image.Visibility = Visibility.Collapsed;
            }

            if (this.Message.Visibility == Visibility.Visible)
            {
                this.Message.Visibility = Visibility.Collapsed;
            }

            this.Video.Visibility = Visibility.Visible;
            if (this.ActualWidth > 0.0)
            {
                // override restretch behavior of the VideoPlayer's MediaElement
                // Like the other panels of a FileDisplay, the video player control stretches to fill the FileDisplay by default.
                // However, since FileDisplay's DockPanel lacks content alignment properties, it resizes itself to match its
                // content.  Since MediaElement's width is zero until it finishes loading video in the background, this results in
                // the FileDisplay contracting to match that of the VideoPlayer controls (which, as of Carnassial 2.2.0.3, happens
                // to be 400 pixels) and then expanding to the width of the file display area in Carnassial's main window a few
                // hundred milliseconds later when the video load completes.  The resulting width flicker is visually distracting
                // and unnecessarily complicates analysis when playing or scrolling through image sets with videos.  The usual
                // workaround for such WPF layout issues is to bind to a parent's AcutalWidth which, in this case, cannot be that
                // of FileDisplay's DockPanel (due to its resizing behavior) and must therefore be the width of the FileDisplay 
                // instead.  This could be done with an ancestor binding in XAML but the below line of code is slightly more efficient
                // as the VideoPlayer's width is only updated when the player is displayed.
                this.Video.Width = this.ActualWidth;
            }
        }
    }
}
