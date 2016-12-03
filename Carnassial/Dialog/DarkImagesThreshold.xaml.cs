using Carnassial.Database;
using Carnassial.Images;
using Carnassial.Util;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace Carnassial.Dialog
{
    public partial class DarkImagesThreshold : Window, IDisposable
    {
        private const int MinimumRectangleWidth = 12;

        private WriteableBitmap bitmap;
        private int darkPixelThreshold;
        private double darkPixelRatio; 
        private double darkPixelRatioFound;
        private FileDatabase database;
        private bool disposed;
        private FileTableEnumerator imageEnumerator;
        private bool isColor;
        private bool updateImageQualityForAllSelectedImagesStarted;
        private bool stop;
        private CarnassialUserRegistrySettings userSettings;

        public DarkImagesThreshold(FileDatabase database, int currentImageIndex, CarnassialUserRegistrySettings state, Window owner)
        {
            this.InitializeComponent();
            this.Owner = owner;

            this.database = database;
            this.imageEnumerator = new FileTableEnumerator(database, currentImageIndex);
            this.darkPixelThreshold = state.DarkPixelThreshold;
            this.darkPixelRatio = state.DarkPixelRatioThreshold;
            this.darkPixelRatioFound = 0;
            this.disposed = false;
            this.isColor = false;
            this.updateImageQualityForAllSelectedImagesStarted = false;
            this.stop = false;
            this.userSettings = state;
        }

        // display the image and associated details in the UI
        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            // make sure the title bar of the dialog box is on the screen. For small screens it may default to being off the screen
            Utilities.SetDefaultDialogPosition(this);
            Utilities.TryFitWindowInWorkingArea(this);

            this.DarkThreshold.Value = this.userSettings.DarkPixelThreshold;
            this.DarkThreshold.ValueChanged += this.DarkThresholdSlider_ValueChanged;

            this.ScrollImages.Minimum = 0;
            this.ScrollImages.Maximum = this.database.CurrentlySelectedFileCount - 1;
            this.ScrollImages.Value = this.imageEnumerator.CurrentRow;

            this.SetPreviousNextButtonStates();
            this.ScrollImages_ValueChanged(null, null);
            this.ScrollImages.ValueChanged += this.ScrollImages_ValueChanged;
            this.Focus();               // necessary for the left/right arrow keys to work.
        }

        private void Window_PreviewKeyDown(object sender, KeyEventArgs e)
        {
            // if its an arrow key and the textbox doesn't have the focus navigate left/right to the next/previous file
            switch (e.Key)
            {
                case Key.Right:             // next file
                    this.NextButton_Click(null, null);
                    break;
                case Key.Left:              // previous file
                    this.PreviousButton_Click(null, null);
                    break;
                default:
                    return;
            }
            e.Handled = true;
        }

        public void Repaint()
        {
            // color the bar to show the current color given the dark color threshold
            byte greyColor = (byte)Math.Round(255 - (double)255 * this.darkPixelThreshold);
            Brush brush = new SolidColorBrush(Color.FromArgb(255, greyColor, greyColor, greyColor));
            this.RectDarkPixelRatioFound.Fill = brush;
            this.GreyColorThreshold.Content = (greyColor + 1).ToString();

            // size the bar to show how many pixels in the current image are at least as dark as that color
            if (this.isColor)
            {
                // color image
                this.RectDarkPixelRatioFound.Width = MinimumRectangleWidth;
            }
            else
            {
                this.RectDarkPixelRatioFound.Width = this.FeedbackCanvas.ActualWidth * this.darkPixelRatioFound;
                if (this.RectDarkPixelRatioFound.Width < MinimumRectangleWidth)
                {
                    this.RectDarkPixelRatioFound.Width = MinimumRectangleWidth; // just so something is always visible
                }
            }
            this.RectDarkPixelRatioFound.Height = this.FeedbackCanvas.ActualHeight;

            // show the location of the %age threshold bar
            this.DarkPixelRatioThumb.Height = this.FeedbackCanvas.ActualHeight;
            this.DarkPixelRatioThumb.Width = MinimumRectangleWidth;
            Canvas.SetLeft(this.DarkPixelRatioThumb, (this.FeedbackCanvas.ActualWidth - this.DarkPixelRatioThumb.ActualWidth) * this.darkPixelRatio);

            this.UpdateLabels();
        }

        // update all the labels to show the current values
        private void UpdateLabels()
        {
            this.DarkPixelRatio.Content = String.Format("{0,3:##0}%", 100 * this.darkPixelRatio);
            this.RatioFound.Content = String.Format("{0,3:##0}", 100 * this.darkPixelRatioFound);

            // don't update labels if the file isn't valid 
            if (this.OriginalClassification.Content.ToString() == Constant.ImageQuality.Ok || this.OriginalClassification.Content.ToString() == Constant.ImageQuality.Dark)
            {
                if (this.isColor)
                {
                    // color image 
                    this.ThresholdMessage.Text = "Color - therefore not dark";
                    this.RatioFound.Content = String.Empty;
                }
                else
                {
                    this.ThresholdMessage.Text = "% of pixels are darker than the threshold";
                }

                if (this.isColor)
                {
                    this.NewClassification.Content = Constant.ImageQuality.Ok;
                }
                else if (this.darkPixelRatio <= this.darkPixelRatioFound)
                {
                    this.NewClassification.Content = Constant.ImageQuality.Dark; // dark grey scale image
                }
                else
                {
                    this.NewClassification.Content = Constant.ImageQuality.Ok;   // light grey scale image
                }
            }
            else
            {
                this.NewClassification.Content = "----";
            }
        }

        private async Task DisplayImageAndDetailsAsync()
        {
            this.bitmap = (await this.imageEnumerator.Current.LoadBitmapAsync(this.database.FolderPath)).AsWriteable();
            this.Image.Source = this.bitmap;
            this.FileName.Content = this.imageEnumerator.Current.FileName;
            this.FileName.ToolTip = this.FileName.Content;
            this.OriginalClassification.Content = this.imageEnumerator.Current.ImageQuality.ToString(); // The original image classification

            this.RecalculateImageQualityForCurrentImage();
            this.Repaint();
        }

        // navigate to the previous file
        private void PreviousButton_Click(object sender, RoutedEventArgs e)
        {
            this.imageEnumerator.MovePrevious();
            this.ScrollImages.Value = this.imageEnumerator.CurrentRow;
        }

        // navigate to the next file
        private void NextButton_Click(object sender, RoutedEventArgs e)
        {
            this.imageEnumerator.MoveNext();
            this.ScrollImages.Value = this.imageEnumerator.CurrentRow;
        }

        private void SetPreviousNextButtonStates()
        {
            this.PreviousFile.IsEnabled = (this.imageEnumerator.CurrentRow == 0) ? false : true;
            this.NextFile.IsEnabled = (this.imageEnumerator.CurrentRow < this.database.CurrentlySelectedFileCount - 1) ? true : false;
        }

        private async void ApplyDoneButton_Click(object sender, RoutedEventArgs e)
        {
            // second click - exit
            if (this.updateImageQualityForAllSelectedImagesStarted)
            {
                this.DialogResult = true;
                return;
            }

            // first click - do update
            // update the Carnassial variables to the current settings
            this.userSettings.DarkPixelThreshold = this.darkPixelThreshold;
            this.userSettings.DarkPixelRatioThreshold = this.darkPixelRatio;

            this.CancelStopButton.Content = "_Stop";

            this.updateImageQualityForAllSelectedImagesStarted = true;
            await this.BeginUpdateImageQualityForAllSelectedImagesAsync();
            await this.DisplayImageAndDetailsAsync(); // Goes back to the original image
        }

        // Cancel or Stop - exit the dialog
        private void CancelStopButton_Click(object sender, RoutedEventArgs e)
        {
            if (this.updateImageQualityForAllSelectedImagesStarted)
            {
                this.stop = true;
            }
            this.DialogResult = false;
        }

        // a dropdown menu providing the user with two ways to reset thresholds
        private void ResetButton_Click(object sender, RoutedEventArgs e)
        {
            Button resetButton = (Button)sender;
            resetButton.ContextMenu.IsEnabled = true;
            resetButton.ContextMenu.PlacementTarget = sender as Button;
            resetButton.ContextMenu.Placement = System.Windows.Controls.Primitives.PlacementMode.Bottom;
            resetButton.ContextMenu.IsOpen = true;
        }

        // reset the thresholds to their initial settings
        private void MenuItemResetCurrent_Click(object sender, RoutedEventArgs e)
        {
            // move the thumb to correspond to the original value
            this.darkPixelRatio = this.userSettings.DarkPixelRatioThreshold;
            Canvas.SetLeft(this.DarkPixelRatioThumb, this.darkPixelRatio * (this.FeedbackCanvas.ActualWidth - this.DarkPixelRatioThumb.ActualWidth));

            // move the slider to its original position
            this.DarkThreshold.Value = this.userSettings.DarkPixelRatioThreshold;
            this.RecalculateImageQualityForCurrentImage();
            this.Repaint();
        }

        // reset the thresholds to default settings
        private void MenuItemResetDefault_Click(object sender, RoutedEventArgs e)
        {
            // move the thumb to correspond to the original value
            this.darkPixelRatio = Constant.Images.DarkPixelRatioThresholdDefault;
            Canvas.SetLeft(this.DarkPixelRatioThumb, this.darkPixelRatio * (this.FeedbackCanvas.ActualWidth - this.DarkPixelRatioThumb.ActualWidth));

            // move the slider to its original position
            this.DarkThreshold.Value = Constant.Images.DarkPixelThresholdDefault;
            this.RecalculateImageQualityForCurrentImage();
            this.Repaint();
        }

        // set a new value for the dark pixel threshold and update the UI
        private void DarkThresholdSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            if (this.DarkPixelRatio == null)
            {
                return;
            }
            this.darkPixelThreshold = Convert.ToInt32(e.NewValue);

            this.RecalculateImageQualityForCurrentImage();
            this.Repaint();
        }

        // set a new value for the dark pixel ratio and update the UI
        private void Thumb_DragDelta(object sender, System.Windows.Controls.Primitives.DragDeltaEventArgs e)
        {
            UIElement thumb = e.Source as UIElement;

            if ((Canvas.GetLeft(thumb) + e.HorizontalChange) >= (this.FeedbackCanvas.ActualWidth - this.DarkPixelRatioThumb.ActualWidth))
            {
                Canvas.SetLeft(thumb, this.FeedbackCanvas.ActualWidth - this.DarkPixelRatioThumb.ActualWidth);
                this.darkPixelRatio = 1;
            }
            else if ((Canvas.GetLeft(thumb) + e.HorizontalChange) <= 0)
            {
                Canvas.SetLeft(thumb, 0);
                this.darkPixelRatio = 0;
            }
            else
            {
                Canvas.SetLeft(thumb, Canvas.GetLeft(thumb) + e.HorizontalChange);
                this.darkPixelRatio = (Canvas.GetLeft(thumb) + e.HorizontalChange) / this.FeedbackCanvas.ActualWidth;
            }
            if (this.DarkPixelRatio == null)
            {
                return;
            }

            this.RecalculateImageQualityForCurrentImage();
            // don't repaint as it would interfere with the thumb drag; just update the labels instead.
            this.UpdateLabels();
        }

        private async void ScrollImages_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            if (this.updateImageQualityForAllSelectedImagesStarted)
            {
                return;
            }

            this.imageEnumerator.TryMoveToFile(Convert.ToInt32(this.ScrollImages.Value));
            await this.DisplayImageAndDetailsAsync();
            this.SetPreviousNextButtonStates();
        }

        public void Dispose()
        {
            this.Dispose(true);
            GC.SuppressFinalize(this);
        }

        protected virtual void Dispose(bool disposing)
        {
            if (this.disposed)
            {
                return;
            }

            if (disposing)
            {
                if (this.imageEnumerator != null)
                {
                    this.imageEnumerator.Dispose();
                }
            }

            this.disposed = true;
        }

        /// <summary>
        /// Redo image quality calculations with current thresholds and return the ratio of pixels at least as dark as the threshold for the current image.
        /// Does not update the database.
        /// </summary>
        private void RecalculateImageQualityForCurrentImage()
        {
            this.bitmap.IsDark(this.darkPixelThreshold, this.darkPixelRatio, out this.darkPixelRatioFound, out this.isColor);
        }

        /// <summary>
        /// Redo image quality calculations with current thresholds for all images selected.  Updates the database.
        /// </summary>
        private async Task BeginUpdateImageQualityForAllSelectedImagesAsync()
        {
            List<ImageRow> selectedFiles = this.database.Files.ToList();
            this.ApplyDoneButton.Content = "_Done";
            this.ApplyDoneButton.IsEnabled = false;
            this.DarkPixelRatioThumb.IsEnabled = false;
            this.DarkThreshold.IsEnabled = false;
            this.PreviousFile.IsEnabled = false;
            this.NextFile.IsEnabled = false;
            this.ScrollImages.IsEnabled = false;
            this.ResetButton.IsEnabled = false;

            IProgress<ImageQuality> updateStatus = new Progress<ImageQuality>(this.ReportProgress);
            await Task.Run(() =>
            {
                TimeSpan desiredRenderInterval = TimeSpan.FromSeconds(1.0 / Constant.ThrottleValues.DesiredMaximumImageRendersPerSecondDefault);
                DateTime mostRecentStatusDispatch = DateTime.UtcNow - desiredRenderInterval;
                object renderLock = new object();

                int fileIndex = 0;
                List<ColumnTuplesWithWhere> filesToUpdate = new List<ColumnTuplesWithWhere>();
                Parallel.ForEach(
                    new SequentialPartitioner<ImageRow>(selectedFiles),
                    Utilities.GetParallelOptions(Environment.ProcessorCount), 
                    (ImageRow file, ParallelLoopState loopState) =>
                    {
                        if (this.stop)
                        {
                            loopState.Break();
                        }

                        // if it's not a valid image, say so and go onto the next one.
                        int currentFileIndex = Interlocked.Increment(ref fileIndex);
                        ImageQuality imageQuality = new ImageQuality(file);
                        if ((imageQuality.OldImageQuality != FileSelection.Ok) && (imageQuality.OldImageQuality != FileSelection.Dark))
                        {
                            imageQuality.FileIndex = currentFileIndex;
                            imageQuality.NewImageQuality = null;
                            updateStatus.Report(imageQuality);
                            return;
                        }

                        try
                        {
                            // find the new image quality and add file to the update list
                            // See remarks in CarnassialWindow.xaml.cs about synchronous loading.
                            imageQuality.Bitmap = file.LoadBitmapAsync(this.database.FolderPath).GetAwaiter().GetResult().AsWriteable();
                            imageQuality.FileIndex = currentFileIndex;
                            imageQuality.NewImageQuality = imageQuality.Bitmap.IsDark(this.darkPixelThreshold, this.darkPixelRatio, out this.darkPixelRatioFound, out this.isColor);
                            imageQuality.IsColor = this.isColor;
                            imageQuality.DarkPixelRatioFound = this.darkPixelRatioFound;
                            if (imageQuality.OldImageQuality != imageQuality.NewImageQuality.Value)
                            {
                                filesToUpdate.Add(new ColumnTuplesWithWhere(new List<ColumnTuple> { new ColumnTuple(Constant.DatabaseColumn.ImageQuality, imageQuality.NewImageQuality.Value.ToString()) }, file.ID));
                            }
                        }
                        catch (Exception exception)
                        {
                            // file isn't there?
                            Debug.Fail("Exception while assessing image quality.", exception.ToString());
                        }

                        DateTime utcNow = DateTime.UtcNow;
                        if (utcNow - mostRecentStatusDispatch > desiredRenderInterval)
                        {
                            lock (renderLock)
                            {
                                if (utcNow - mostRecentStatusDispatch > desiredRenderInterval)
                                {
                                    mostRecentStatusDispatch = utcNow;
                                    updateStatus.Report(imageQuality);
                                }
                            }
                        }
                    });

                    this.database.UpdateFiles(filesToUpdate);
                });

            await this.DisplayImageAndDetailsAsync();
            this.ApplyDoneButton.IsEnabled = true;
            this.CancelStopButton.IsEnabled = false;
        }

        private void ReportProgress(ImageQuality imageQuality)
        {
            this.Image.Source = imageQuality.Bitmap;
            this.FileName.Content = imageQuality.FileName;
            this.FileName.ToolTip = this.FileName.Content;

            this.OriginalClassification.Content = imageQuality.OldImageQuality;
            this.NewClassification.Content = imageQuality.NewImageQuality;
            this.DarkPixelRatio.Content = String.Format("{0,3:##0}%", 100.0 * this.darkPixelRatio);
            this.RatioFound.Content = String.Format("{0,3:##0}", 100.0 * imageQuality.DarkPixelRatioFound);

            if (imageQuality.IsColor)
            {
                this.ThresholdMessage.Text = "Color - therefore not dark";
                this.RatioFound.Content = String.Empty;
            }
            else
            {
                this.ThresholdMessage.Text = "% of pixels are darker than the threshold";
            }

            // size the bar to show how many pixels in the current image are at least as dark as that color
            this.RectDarkPixelRatioFound.Width = this.FeedbackCanvas.ActualWidth * imageQuality.DarkPixelRatioFound;
            if (this.RectDarkPixelRatioFound.Width < 6)
            {
                this.RectDarkPixelRatioFound.Width = 6; // Just so something is always visible
            }
            this.RectDarkPixelRatioFound.Height = this.FeedbackCanvas.ActualHeight;

            // update file scroll bar position
            this.ScrollImages.Value = imageQuality.FileIndex - 1;
        }
    }
}
