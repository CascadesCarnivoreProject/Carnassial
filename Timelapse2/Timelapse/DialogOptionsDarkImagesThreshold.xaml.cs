using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Timelapse.Database;
using Timelapse.Images;
using Timelapse.Util;

namespace Timelapse
{
    /// <summary>
    /// Interaction logic for DialogOptionsDarkImagesThreshold.xaml
    /// </summary>
    public partial class DialogOptionsDarkImagesThreshold : Window
    {
        private const int MinimumWidth = 12;

        private ImageDatabase database;
        private BitmapFrame bitmap;
        private int darkPixelThreshold = 0; // Default value
        private double darkPixelRatio = 0;  // Default value 
        private double darkPixelRatioFound = 0;
        private bool isColor = false; // Whether the image is color or grey scale
        private TimelapseState state;

        #region Window Initialization and Callbacks
        public DialogOptionsDarkImagesThreshold(ImageDatabase database, TimelapseState state)
        {
            this.InitializeComponent();

            this.database = database;
            this.darkPixelThreshold = state.DarkPixelThreshold;
            this.darkPixelRatio = state.DarkPixelRatioThreshold;
            this.state = state;
        }

        // Display the image and associated details in the UI
        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            // Make sure the title bar of the dialog box is on the screen. For small screens it may default to being off the screen
            if (this.Left < 10 || this.Top < 10)
            {
                this.Left = this.Owner.Left + (this.Owner.Width - this.ActualWidth) / 2; // Center it horizontally
                this.Top = this.Owner.Top + 20; // Offset it from the windows'top by 20 pixels downwards
            }

            this.sldrDarkThreshold.Value = this.state.DarkPixelRatioThreshold;
            this.sldrDarkThreshold.ValueChanged += this.DarkThresholdSlider_ValueChanged;

            this.sldrScrollImages.Minimum = 0;
            this.sldrScrollImages.Maximum = this.database.ImageCount - 1;
            this.sldrScrollImages.Value = this.database.CurrentRow;

            this.SetPreviousNextButtonStates();
            this.SldrScrollImages_ValueChanged(null, null);
            this.sldrScrollImages.ValueChanged += this.SldrScrollImages_ValueChanged;
            this.Focus();               // necessary for the left/right arrow keys to work.
        }

        // If its an arrow key and the textbox doesn't have the focus,
        // navigate left/right image or up/down to look at differenced image
        private void Window_PreviewKeyDown(object sender, KeyEventArgs e)
        {
            // Interpret key as a possible shortcut key. 
            // Depending on the key, take the appropriate action
            switch (e.Key)
            {
                case Key.Right:             // next image
                    this.NextButton_Click(null, null);
                    break;
                case Key.Left:              // previous image
                    this.PreviousButton_Click(null, null);
                    break;
                default:
                    return;
            }
            e.Handled = true;
        }
        #endregion

        #region Image Loading and UI Repainting
        public void Repaint()
        {
            // Color the bar to show the current color given the dark color threshold
            byte greyColor = (byte)Math.Round(255 - (double)255 * this.darkPixelThreshold);
            Brush brush = new SolidColorBrush(Color.FromArgb(255, greyColor, greyColor, greyColor));
            this.RectDarkPixelRatioFound.Fill = brush;
            this.lblGreyColorThreshold.Content = (greyColor + 1).ToString();

            // Size the bar to show how many pixels in the current image are at least as dark as that color
            if (this.isColor)
            {
                // color image
                this.RectDarkPixelRatioFound.Width = MinimumWidth;
            }
            else
            {
                this.RectDarkPixelRatioFound.Width = this.FeedbackCanvas.ActualWidth * this.darkPixelRatioFound;
                if (this.RectDarkPixelRatioFound.Width < MinimumWidth)
                {
                    this.RectDarkPixelRatioFound.Width = MinimumWidth; // Just so something is always visible
                }
            }
            this.RectDarkPixelRatioFound.Height = this.FeedbackCanvas.ActualHeight;

            // Show the location of the %age threshold bar
            this.LineDarkPixelRatio.Height = this.FeedbackCanvas.ActualHeight;
            this.LineDarkPixelRatio.Width = MinimumWidth;
            Canvas.SetLeft(this.LineDarkPixelRatio, (this.FeedbackCanvas.ActualWidth - this.LineDarkPixelRatio.ActualWidth) * this.darkPixelRatio);

            this.UpdateLabels();
        }

        // Load the image with the currentID into the display
        private void LoadImage()
        {
            bool result;

            // Display the image. While we should be on a valid image (our assumption), we can still show a missing or corrupted image if needed
            string path = Path.Combine(this.database.FolderPath, this.database.IDGetFile(out result));
            try
            {
                this.bitmap = BitmapFrame.Create(new Uri(path), BitmapCreateOptions.None, BitmapCacheOption.None);
            }
            catch
            {
                if (!File.Exists(path))
                {
                    this.bitmap = BitmapFrame.Create(new Uri("pack://application:,,/Resources/missing.jpg"));
                }
                else
                {
                    this.bitmap = BitmapFrame.Create(new Uri("pack://application:,,/Resources/corrupted.jpg"));
                }
            }
            this.img.Source = this.bitmap;
        }

        // Update all the labels to show the current values
        private void UpdateLabels()
        {
            this.lblDarkPixelRatio.Content = String.Format("{0,3:##0}%", 100 * this.darkPixelRatio);
            this.lblRatioFound.Content = String.Format("{0,3:##0}", 100 * this.darkPixelRatioFound);

            //// We don't want to update labels if the image is not valid 
            if (this.lblOriginalClassification.Content.ToString() == Constants.ImageQuality.Ok || this.lblOriginalClassification.Content.ToString() == Constants.ImageQuality.Dark)
            {
                if (this.isColor)
                {
                    // color image 
                    this.lblThresholdMessage.Text = "Color image - therefore not dark";
                    this.txtPercent.Visibility = Visibility.Hidden;
                    this.lblRatioFound.Content = String.Empty;
                }
                else
                {
                    this.lblThresholdMessage.Text = "of the image pixels are darker than the threshold";
                    this.txtPercent.Visibility = Visibility.Visible;
                }

                if (this.isColor)
                {
                    this.lblNewClassification.Content = Constants.ImageQuality.Ok;       // Color image
                }
                else if (this.darkPixelRatio <= this.darkPixelRatioFound)
                {
                    this.lblNewClassification.Content = Constants.ImageQuality.Dark;  // Dark grey scale image
                }
                else
                {
                    this.lblNewClassification.Content = Constants.ImageQuality.Ok;   // Light grey scale image
                }
            }
            else
            {
                this.lblNewClassification.Content = "----";
            }
        }

        // Utility routine for calling a typical sequence of UI update actions
        private void DisplayImageAndDetails()
        {
            bool result;
            this.lblImageName.Content = this.database.IDGetFile(out result);
            this.lblOriginalClassification.Content = this.database.IDGetImageQuality(out result); // The original image classification

            this.LoadImage();
            this.Recalculate();
            this.Repaint();
        }
        #endregion

        #region Button and Button Menu Callbacks and related methods
        // /Show/Hide the explanation at the top of the window 
        private void HideTextButton_StateChange(object sender, RoutedEventArgs e)
        {
            this.gridExplanation.Visibility = ((bool)this.btnHideText.IsChecked) ? Visibility.Collapsed : Visibility.Visible;
        }

        // Navigate to the previous image
        private void PreviousButton_Click(object sender, RoutedEventArgs e)
        {
            this.database.ToDataRowPrevious();
            this.sldrScrollImages.Value = this.database.CurrentRow;
        }

        // Navigate to the next image
        private void NextButton_Click(object sender, RoutedEventArgs e)
        {
            this.database.ToDataRowNext();
            this.sldrScrollImages.Value = this.database.CurrentRow;
        }

        private void SetPreviousNextButtonStates()
        {
            this.PrevButton.IsEnabled = (this.database.CurrentRow == 0) ? false : true;
            this.NextButton.IsEnabled = (this.database.CurrentRow < this.database.ImageCount - 1) ? true : false;
        }

        // Update the database if the OK button is clicked
        private void OkButton_Click(object sender, RoutedEventArgs e)
        {
            // Update the Timelapse variables to the current settings
            this.state.DarkPixelThreshold = this.darkPixelThreshold;
            this.state.DarkPixelRatioThreshold = this.darkPixelRatio;

            this.RescanImageQuality();
            this.DisplayImageAndDetails(); // Goes back to the original image
            this.CancelButton.Content = "Done";
        }

        // Cancel  or Done - exists the dialog.
        private void CancelButton_Click(object sender, RoutedEventArgs e)
        {
            this.DialogResult = false;
        }

        // A drop-down menu providing the user with two ways to reset thresholds
        private void ResetButton_Click(object sender, RoutedEventArgs e)
        {
            (sender as Button).ContextMenu.IsEnabled = true;
            (sender as Button).ContextMenu.PlacementTarget = sender as Button;
            (sender as Button).ContextMenu.Placement = System.Windows.Controls.Primitives.PlacementMode.Bottom;
            (sender as Button).ContextMenu.IsOpen = true;
        }

        // Reset the thresholds to their initial settings
        private void MenuItemResetCurrent_Click(object sender, RoutedEventArgs e)
        {
            // Move the thumb to correspond to the original value
            this.darkPixelRatio = this.state.DarkPixelRatioThreshold;
            Canvas.SetLeft(this.LineDarkPixelRatio, this.darkPixelRatio * (this.FeedbackCanvas.ActualWidth - this.LineDarkPixelRatio.ActualWidth));

            // Move the slider to its original position
            this.sldrDarkThreshold.Value = this.state.DarkPixelRatioThreshold;
            this.Recalculate();
            this.Repaint();
        }

        // Reset the thresholds to the Timelapse Default settings
        private void MenuItemResetDefault_Click(object sender, RoutedEventArgs e)
        {
            // Move the thumb to correspond to the original value
            this.darkPixelRatio = Constants.DarkPixelRatioThresholdDefault;
            Canvas.SetLeft(this.LineDarkPixelRatio, this.darkPixelRatio * (this.FeedbackCanvas.ActualWidth - this.LineDarkPixelRatio.ActualWidth));

            // Move the slider to its original position
            this.sldrDarkThreshold.Value = Constants.DarkPixelThresholdDefault;
            this.Recalculate();
            this.Repaint();
        }
        #endregion

        #region Sliders and related Callbacks (including my Thumb slider)
        // Set a new value for the dark pixel threshold and update the UI
        private void DarkThresholdSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            if (null == this.lblDarkPixelRatio)
            {
                return;
            }
            this.darkPixelThreshold = Convert.ToInt32(e.NewValue);

            this.Recalculate();
            this.Repaint();
        }

        // Set a new value for the Dark Pixel Ratio and update the UI
        private void Thumb_DragDelta(object sender, System.Windows.Controls.Primitives.DragDeltaEventArgs e)
        {
            UIElement thumb = e.Source as UIElement;

            if ((Canvas.GetLeft(thumb) + e.HorizontalChange) >= (this.FeedbackCanvas.ActualWidth - this.LineDarkPixelRatio.ActualWidth))
            {
                Canvas.SetLeft(thumb, this.FeedbackCanvas.ActualWidth - this.LineDarkPixelRatio.ActualWidth);
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
            if (null == this.lblDarkPixelRatio)
            {
                return;
            }

            this.Recalculate();
            // We don't repaint, as this will screw up the thumb draggin. So just update the labels instead.
            this.UpdateLabels();
        }

        private void SldrScrollImages_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            this.database.ToDataRowIndex(Convert.ToInt32(this.sldrScrollImages.Value));
            this.DisplayImageAndDetails();
            this.SetPreviousNextButtonStates();
        }
        #endregion

        #region Work Utilities
        /// <summary>
        ///  Recalculate the image darkness classification with the given thresholds and return the ratio of pixels
        ///  at least as dark as the threshold for the current image
        /// </summary>
        private void Recalculate()
        {
            PixelBitmap.IsDark(this.bitmap, this.darkPixelThreshold, this.darkPixelRatio, out this.darkPixelRatioFound, out this.isColor);
        }

        private void RescanImageQuality()
        {
            FileInfo fileInfo;
            ImageQuality imgQuality;  // Collect the image properties for for the 2nd pass...
            List<ImageQuality> imgQualityList = new List<ImageQuality>();

            // TODO: 
            // MAKE DB UPDATE EFFICIENT
            var bgw = new BackgroundWorker() { WorkerReportsProgress = true };
            bgw.DoWork += (ow, ea) =>
            {   // this runs on the background thread; its written as an anonymous delegate
                // We need to invoke this to allow updates on the UI
                this.Dispatcher.Invoke(new Action(() =>
                {
                    // First, change the UIprovide some feedback
                    // this.txtblockFeedback.Text += "Step 1/2: Examining images..." + Environment.NewLine;
                }));
                int count = database.DataTable.Rows.Count;
                int j = 1;
                for (int i = 0; i < count; i++)
                {
                    fileInfo = new FileInfo(System.IO.Path.Combine(database.FolderPath, database.DataTable.Rows[i][Constants.DatabaseElement.File].ToString()));

                    imgQuality = new ImageQuality();                            // We will store the various image properties here
                    imgQuality.FileName = database.DataTable.Rows[i][Constants.DatabaseElement.File].ToString();
                    imgQuality.ID = Int32.Parse(database.DataTable.Rows[i][Constants.Database.ID].ToString());
                    imgQuality.OldImageQuality = database.DataTable.Rows[i][Constants.DatabaseElement.ImageQuality].ToString();

                    // If its not a valid image, say so and go onto the next one.
                    if (!imgQuality.OldImageQuality.Equals(Constants.ImageQuality.Ok) && !imgQuality.OldImageQuality.Equals(Constants.ImageQuality.Dark))
                    {
                        imgQuality.NewImageQuality = String.Empty;
                        imgQuality.Update = false;
                        bgw.ReportProgress(0, imgQuality);
                        continue;
                    }

                    // Ok, we only have valid images at this point
                    try
                    {
                        // Get the image (if its there), get the new dates/times, and add it to the list of images to be updated 
                        // Note that if the image can't be created, we will just to the catch.
                        imgQuality.Bitmap = BitmapFrame.Create(new Uri(fileInfo.FullName), BitmapCreateOptions.None, BitmapCacheOption.None);

                        imgQuality.NewImageQuality = PixelBitmap.IsDark(imgQuality.Bitmap, this.darkPixelThreshold, this.darkPixelRatio, out this.darkPixelRatioFound, out this.isColor) ? Constants.ImageQuality.Dark : Constants.ImageQuality.Ok;
                        imgQuality.IsColor = this.isColor;
                        imgQuality.DarkPixelRatioFound = this.darkPixelRatioFound;
                        if (imgQuality.OldImageQuality.Equals(imgQuality.NewImageQuality))
                        {
                            imgQuality.Update = false;
                        }
                        else
                        {
                            imgQuality.Update = true;
                            imgQualityList.Add(imgQuality);
                            database.RowSetValueFromID(Constants.DatabaseElement.ImageQuality, imgQuality.NewImageQuality, imgQuality.ID);
                        }
                    }
                    catch // Image isn't there
                    {
                        imgQuality.Update = false;
                    }
                    j++;
                    bgw.ReportProgress(0, imgQuality);
                }
            };
            bgw.ProgressChanged += (o, ea) =>
            {
                // this gets called on the UI thread
                ImageQuality iq = (ImageQuality)ea.UserState;
                this.img.Source = iq.Bitmap;

                this.lblImageName.Content = iq.FileName;
                this.lblOriginalClassification.Content = iq.OldImageQuality;
                this.lblNewClassification.Content = iq.NewImageQuality;
                this.lblDarkPixelRatio.Content = String.Format("{0,3:##0}%", 100 * this.darkPixelRatio);
                this.lblRatioFound.Content = String.Format("{0,3:##0}", 100 * iq.DarkPixelRatioFound);

                if (iq.IsColor) // color image 
                {
                    this.lblThresholdMessage.Text = "Color image - therefore not dark";
                    this.txtPercent.Visibility = Visibility.Hidden;
                    this.lblRatioFound.Content = String.Empty;
                }
                else
                {
                    this.lblThresholdMessage.Text = "of the image pixels are darker than the threshold";
                    this.txtPercent.Visibility = Visibility.Visible;
                }

                // Size the bar to show how many pixels in the current image are at least as dark as that color
                this.RectDarkPixelRatioFound.Width = FeedbackCanvas.ActualWidth * iq.DarkPixelRatioFound;
                if (this.RectDarkPixelRatioFound.Width < 6)
                {
                    this.RectDarkPixelRatioFound.Width = 6; // Just so something is always visible
                }
                this.RectDarkPixelRatioFound.Height = FeedbackCanvas.ActualHeight;
            };
            bgw.RunWorkerCompleted += (o, ea) =>
            {
                DisplayImageAndDetails();
                TimelapseWindow owner = this.Owner as TimelapseWindow;
                owner.MenuItemImageCounts_Click(null, null);
            };
            bgw.RunWorkerAsync();
        }
        #endregion
    }
}
