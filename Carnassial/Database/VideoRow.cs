using Carnassial.Images;
using System;
using System.Data;
using System.Diagnostics;
using System.IO;
using System.Threading;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace Carnassial.Database
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

        public override BitmapSource LoadBitmap(string imageFolderPath, Nullable<int> desiredWidth)
        {
            string path = this.GetFilePath(imageFolderPath);
            if (!File.Exists(path))
            {
                return Constant.Images.FileNoLongerAvailable.Value;
            }

            for (int renderAttempt = 0; renderAttempt < Constant.ThrottleValues.MaximumRenderAttempts; ++renderAttempt)
            {
                MediaPlayer mediaPlayer = new MediaPlayer();
                mediaPlayer.Volume = 0.0;
                try
                {
                    mediaPlayer.Open(new Uri(path));
                    mediaPlayer.Play();

                    // MediaPlayer is not actually synchronous despite exposing synchronous APIs, so wait for it get the video loaded.  Otherwise
                    // the width and height properties are zero and only black pixels are drawn.  The properties will populate with just a call to
                    // Open() call but without also Play() only black is rendered.  It would be preferable to hook, say, mediaPlayer.MediaOpened
                    // for this purpose but the event doesn't seem to be fired.
                    while ((mediaPlayer.NaturalVideoWidth < 1) || (mediaPlayer.NaturalVideoHeight < 1))
                    {
                        // back off briefly to let MediaPlayer do its loading, which typically takes perhaps 75ms
                        // a brief Sleep() is used rather than Yield() to reduce overhead as 500k to 1M+ yields typically occur
                        Thread.Sleep(Constant.ThrottleValues.PollIntervalForVideoLoad);
                    }

                    // sleep one more time as MediaPlayer has a tendency to still return black frames for a moment after the width and height have populated
                    Thread.Sleep(Constant.ThrottleValues.PollIntervalForVideoLoad);

                    int pixelWidth = mediaPlayer.NaturalVideoWidth;
                    int pixelHeight = mediaPlayer.NaturalVideoHeight;
                    if (desiredWidth.HasValue)
                    {
                        double scaling = (double)desiredWidth.Value / (double)pixelWidth;
                        pixelWidth = (int)(scaling * pixelWidth);
                        pixelHeight = (int)(scaling * pixelHeight);
                    }

                    // set up to render frame from the video
                    mediaPlayer.Pause();
                    mediaPlayer.Position = TimeSpan.FromMilliseconds(1.0);

                    // render and check for black frame
                    // it's assumed the camera doesn't yield all black frames
                    DrawingVisual drawingVisual = new DrawingVisual();
                    for (int blackFrameAttempt = 1; blackFrameAttempt <= Constant.ThrottleValues.MaximumBlackFrameAttempts; ++blackFrameAttempt)
                    {
                        // try render
                        // creating the DrawingContext insie the loop but persisting the DrawingVisual seems to produce the highest success rate.
                        using (DrawingContext drawingContext = drawingVisual.RenderOpen())
                        {
                            drawingContext.DrawVideo(mediaPlayer, new Rect(0, 0, pixelWidth, pixelHeight));
                            RenderTargetBitmap renderBitmap = new RenderTargetBitmap(pixelWidth, pixelHeight, 96, 96, PixelFormats.Default);
                            renderBitmap.Render(drawingVisual);
                            renderBitmap.Freeze();

                            // check if render succeeded
                            // hopefully it did and most of the overhead here is WriteableBitmap conversion though, at 2-3ms for a 1280x720 frame, black 
                            // checking is not an especially expensive operation relative to the O(175ms) cost of this function
                            WriteableBitmap writeableBitmap = renderBitmap.AsWriteable();
                            if (writeableBitmap.IsBlack() == false)
                            {
                                // Debug.Print("Video frame succeeded: render attempt {0}, black frame attempt {1}.", renderAttempt, blackFrameAttempt);
                                // if the media player is closed before Render() only black is rendered
                                mediaPlayer.Close();
                                return writeableBitmap;
                            }
                        }

                        // black frame was rendered; apply linear backoff and try again
                        Thread.Sleep(TimeSpan.FromMilliseconds(Constant.ThrottleValues.RenderingBackoffTime.TotalMilliseconds * renderAttempt));
                    }
                }
                catch (Exception exception)
                {
                    Debug.Fail(String.Format("Loading of {0} failed.", this.FileName), exception.ToString());
                    return Constant.Images.CorruptFile.Value;
                }

                mediaPlayer.Close();
            }

            throw new ApplicationException(String.Format("Limit of {0} render attempts reached.", Constant.ThrottleValues.MaximumRenderAttempts));
        }
    }
}
