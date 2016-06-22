using System;
using System.Data;
using System.Diagnostics;
using System.IO;
using System.Threading;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace Timelapse.Database
{
    public class VideoRow : ImageRow
    {
        public VideoRow(DataRow row)
            : base(row)
        {
        }

        public override bool IsVideo
        {
            get { return true; }
        }

        public override BitmapFrame LoadBitmapFrame(string imageFolderPath, Nullable<int> desiredWidth)
        {
            string path = this.GetImagePath(imageFolderPath);
            if (!File.Exists(path))
            {
                return Constants.Images.Missing;
            }

            MediaPlayer mediaPlayer = new MediaPlayer();
            try
            {
                mediaPlayer.Open(new Uri(path));
                mediaPlayer.Play();

                // MediaPlayer is not actually synchronous despite exposing synchronous APIs, so wait for it get the video loaded.  Otherwise
                // the width and height properties are zero and only black pixels are drawn.  The properties will populate with just a call to
                // Open() call but without also Play() only black is rendered
                while ((mediaPlayer.NaturalVideoWidth < 1) || (mediaPlayer.NaturalVideoHeight < 1))
                {
                    Thread.Yield();
                }

                int pixelWidth = mediaPlayer.NaturalVideoWidth;
                int pixelHeight = mediaPlayer.NaturalVideoHeight;
                if (desiredWidth.HasValue)
                {
                    double scaling = (double)desiredWidth.Value / (double)pixelWidth;
                    pixelWidth = (int)(scaling * pixelWidth);
                    pixelHeight = (int)(scaling * pixelHeight);
                }

                mediaPlayer.Pause();
                mediaPlayer.Position = TimeSpan.Zero;

                DrawingVisual drawingVisual = new DrawingVisual();
                using (DrawingContext drawingContext = drawingVisual.RenderOpen())
                {
                    drawingContext.DrawVideo(mediaPlayer, new Rect(0, 0, pixelWidth, pixelHeight));
                }

                RenderTargetBitmap renderBitmap = new RenderTargetBitmap(pixelWidth, pixelHeight, 96, 96, PixelFormats.Default);
                renderBitmap.Render(drawingVisual);

                // if the media player is closed before Render() only black is rendered
                mediaPlayer.Close();

                return BitmapFrame.Create(renderBitmap);
            }
            catch (Exception exception)
            {
                Debug.Assert(false, String.Format("Loading of {0} failed.", this.FileName), exception.ToString());
                return Constants.Images.Corrupt;
            }
        }
    }
}
