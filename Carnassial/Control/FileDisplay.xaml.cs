using Carnassial.Data;
using Carnassial.Images;
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
            CachedImage image = imageCache.GetCurrentImage();
            if (image != null)
            {
                this.Display(image);
            }
            else
            {
                await this.DisplayAsync(folderPath, imageCache.Current);
            }
        }

        public async Task DisplayAsync(string folderPath, ImageRow file)
        {
            if (file.IsVideo)
            {
                this.Display(file.GetFileInfo(folderPath));
            }
            else
            {
                // the first time this method is called it's likely this control hasn't stretched to full occupy its available space
                // Within Carnassial, FileDisplay is commonly a Grid element with HorizonalAlignment = Center. This grants it infinite 
                // space during initial layout where the message is visible but, as the message is empty, the width used is set by the 
                // empty message's margin.  It's therefore narrow, typically resulting in layout of the FileDisplay as a vertically 
                // oriented rectangle with a high aspect ratio.  Since trail camera images are nearly always horizontally oriented,
                // this results in ActualWidth being set to a value much lower than the width at which the image will be displayed once
                // it's loaded.  There does not appear to be a simple XAML solution to this as DockPanel and Viewbox both lack
                // HorizontalContentAlignment.  As a workaround, estimate the actual image display width in such cases to avoid low
                // resolution image loads which result in low quality display.
                int expectedDisplayWidth = (int)this.ActualWidth;
                if (this.ActualHeight > this.ActualWidth)
                {
                    expectedDisplayWidth = (int)(4.0 / 3.0 * this.ActualHeight);
                }

                using (CachedImage image = await file.TryLoadImageAsync(folderPath, expectedDisplayWidth))
                {
                    this.Display(image);
                }
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
        }
    }
}
