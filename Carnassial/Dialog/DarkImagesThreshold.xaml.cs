using Carnassial.Database;
using Carnassial.Images;
using Carnassial.Native;
using Carnassial.Util;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;

namespace Carnassial.Dialog
{
    public partial class DarkImagesThreshold : Window, IDisposable
    {
        private const int MinimumDarkColorRectangleHeight = 10;

        private double darkPixelRatioFound;
        private FileDatabase database;
        private bool disposed;
        private int filesProcessed;
        private MemoryImage image;
        private FileTableEnumerator fileEnumerator;
        private bool isColor;
        private bool isProgramaticNavigatiorSliderUpdate;
        private bool recalculateSelectedFilesStarted;
        private bool stop;
        private CarnassialUserRegistrySettings userSettings;

        public DarkImagesThreshold(FileDatabase database, int currentFileIndex, CarnassialUserRegistrySettings state, Window owner)
        {
            this.InitializeComponent();
            this.Owner = owner;

            this.database = database;
            this.darkPixelRatioFound = 0;
            this.disposed = false;
            this.filesProcessed = 0;
            this.fileEnumerator = new FileTableEnumerator(database, currentFileIndex);
            this.isColor = false;
            this.isProgramaticNavigatiorSliderUpdate = false;
            this.recalculateSelectedFilesStarted = false;
            this.stop = false;
            this.userSettings = state;
        }

        private async Task DisplayFileAndDetailsAsync()
        {
            this.image = await this.fileEnumerator.Current.LoadAsync(this.database.FolderPath, (int)this.Width);
            this.image.SetSource(this.Image);
            this.FileName.Content = this.fileEnumerator.Current.FileName;
            this.FileName.ToolTip = this.FileName.Content;
            this.OriginalClassification.Content = this.fileEnumerator.Current.ImageQuality; // The original image classification

            this.RecalculateCurrentFile();
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
                if (this.fileEnumerator != null)
                {
                    this.fileEnumerator.Dispose();
                }
            }

            this.disposed = true;
        }

        private async void ApplyDoneButton_Click(object sender, RoutedEventArgs e)
        {
            // second click - exit
            if (this.recalculateSelectedFilesStarted)
            {
                this.DialogResult = true;
                return;
            }

            // first click - do update
            // update the Carnassial variables to the current settings
            this.userSettings.DarkPixelThreshold = (byte)this.DarkPixelThreshold.Value;
            this.userSettings.DarkPixelRatioThreshold = 0.01 * this.DarkPixelPercentageThreshold.Value;

            this.CancelStopButton.Content = "_Stop";

            this.recalculateSelectedFilesStarted = true;
            await this.UpdateClassificationForAllSelectedFilesAsync();
        }

        // Cancel or Stop - exit the dialog
        private void CancelStopButton_Click(object sender, RoutedEventArgs e)
        {
            if (this.recalculateSelectedFilesStarted)
            {
                this.stop = true;
            }
            this.DialogResult = false;
        }

        // set a new value for the dark pixel threshold and update the UI
        private void DarkPixel_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            this.RecalculateCurrentFile();
        }

        private async void FileNavigatorSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            if (this.isProgramaticNavigatiorSliderUpdate || this.recalculateSelectedFilesStarted)
            {
                return;
            }

            this.fileEnumerator.TryMoveToFile((int)this.FileNavigatorSlider.Value);
            await this.DisplayFileAndDetailsAsync();
        }

        private void MenuResetCurrent_Click(object sender, RoutedEventArgs e)
        {
            this.ResetThresholds(this.userSettings.DarkPixelRatioThreshold, this.userSettings.DarkPixelThreshold);
        }

        private void MenuResetDefault_Click(object sender, RoutedEventArgs e)
        {
            this.ResetThresholds(Constant.Images.DarkPixelRatioThresholdDefault, Constant.Images.DarkPixelThresholdDefault);
        }

        /// <summary>
        /// Redo image quality calculations with current thresholds and return the ratio of pixels at least as dark as the threshold for the current file.
        /// Does not update the database.
        /// </summary>
        private void RecalculateCurrentFile()
        {
            this.image.IsDark((byte)this.DarkPixelThreshold.Value, 0.01 * this.DarkPixelPercentageThreshold.Value, out this.darkPixelRatioFound, out this.isColor);

            FileSelection newClassification = FileSelection.Ok;
            if ((this.isColor == false) && (this.DarkPixelPercentageThreshold.Value <= 100.0 * this.darkPixelRatioFound))
            {
                newClassification = FileSelection.Dark;
            }
            this.UpdateClassificationFeedback((FileSelection)this.OriginalClassification.Content, newClassification, this.darkPixelRatioFound, this.isColor);
        }

        private void ResetThresholds(double darkPixelRatioThreshold, byte darkPixelThreshold)
        {
            this.DarkPixelPercentageThreshold.Value = 100.0 * darkPixelRatioThreshold;
            this.DarkPixelThreshold.Value = darkPixelThreshold;
            this.RecalculateCurrentFile();
        }

        private async Task ShowFileWithoutSliderCallbackAsync(bool forward, ModifierKeys modifiers)
        {
            // determine how far to move and in which direction
            int increment = Utilities.GetIncrement(forward, modifiers);
            int newFileIndex = this.fileEnumerator.CurrentRow + increment;

            await this.ShowFileWithoutSliderCallbackAsync(newFileIndex);
        }

        private async Task ShowFileWithoutSliderCallbackAsync(int newFileIndex)
        {
            // if no change the file is already being displayed
            // For example, the end of the image set has been reached but key repeat means right arrow events are still coming in as the user hasn't
            // reacted yet.
            if (newFileIndex == this.fileEnumerator.CurrentRow)
            {
                return;
            }
            if (newFileIndex >= this.database.CurrentlySelectedFileCount)
            {
                newFileIndex = this.database.CurrentlySelectedFileCount - 1;
            }
            else if (newFileIndex < 0)
            {
                newFileIndex = 0;
            }

            this.fileEnumerator.TryMoveToFile(newFileIndex);
            if (this.FileNavigatorSlider.Value != this.fileEnumerator.CurrentRow)
            {
                this.isProgramaticNavigatiorSliderUpdate = true;
                this.FileNavigatorSlider.Value = this.fileEnumerator.CurrentRow;
                this.isProgramaticNavigatiorSliderUpdate = false;
            }
            await this.DisplayFileAndDetailsAsync();
        }

        /// <summary>
        /// Redo image quality calculations with current thresholds for all files selected.  Updates the database.
        /// </summary>
        private async Task UpdateClassificationForAllSelectedFilesAsync()
        {
            List<ImageRow> selectedFiles = this.database.Files.ToList();
            this.ApplyDoneButton.Content = "_Done";
            this.ApplyDoneButton.IsEnabled = false;
            this.DarkPixelPercentageThreshold.IsEnabled = false;
            this.DarkPixelThreshold.IsEnabled = false;
            this.FileNavigatorSlider.IsEnabled = false;
            this.MenuReset.IsEnabled = false;

            // cache properties for access by non-UI threads
            double darkPixelRatioThreshold = 0.01 * this.DarkPixelPercentageThreshold.Value;
            byte darkPixelThreshold = (byte)this.DarkPixelThreshold.Value;
            IProgress<ImageQuality> updateStatus = new Progress<ImageQuality>(this.UpdateClassificationProgress);
            await Task.Run(() =>
            {
                TimeSpan desiredRenderInterval = TimeSpan.FromSeconds(1.0 / Constant.ThrottleValues.DesiredMaximumImageRendersPerSecondDefault);
                DateTime mostRecentStatusDispatch = DateTime.UtcNow - desiredRenderInterval;
                object renderLock = new object();

                this.filesProcessed = 0;
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
                        Interlocked.Increment(ref this.filesProcessed);
                        ImageQuality imageQuality = new ImageQuality(file);
                        if ((imageQuality.OldImageQuality != FileSelection.Ok) && (imageQuality.OldImageQuality != FileSelection.Dark))
                        {
                            imageQuality.NewImageQuality = null;
                            updateStatus.Report(imageQuality);
                            return;
                        }

                        // find the new image quality and add file to the update list
                        // For consistency full size loading is always used in dark recalculations.
                        // See also remarks in CarnassialWindow.xaml.cs about synchronous loading.  
                        imageQuality.Image = file.LoadAsync(this.database.FolderPath).GetAwaiter().GetResult();
                        imageQuality.NewImageQuality = imageQuality.Image.IsDark(darkPixelThreshold, darkPixelRatioThreshold, out this.darkPixelRatioFound, out this.isColor) ? FileSelection.Dark : FileSelection.Ok;
                        imageQuality.IsColor = this.isColor;
                        imageQuality.DarkPixelRatioFound = this.darkPixelRatioFound;
                        if (imageQuality.NewImageQuality.HasValue && (imageQuality.OldImageQuality != imageQuality.NewImageQuality.Value))
                        {
                            filesToUpdate.Add(new ColumnTuplesWithWhere(new List<ColumnTuple> { new ColumnTuple(Constant.DatabaseColumn.ImageQuality, imageQuality.NewImageQuality.Value.ToString()) }, file.ID));
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

            await this.DisplayFileAndDetailsAsync();
            this.ApplyDoneButton.IsEnabled = true;
            this.CancelStopButton.IsEnabled = false;
        }

        public void UpdateClassificationFeedback(FileSelection originalClassification, Nullable<FileSelection> newClassification, double darkPixelRatioFound, bool isColor)
        {
            this.OriginalClassification.Content = originalClassification;
            if (newClassification.HasValue)
            {
                this.NewClassification.Content = newClassification;
            }
            else
            {
                this.NewClassification.Content = null;
            }

            if ((originalClassification != FileSelection.Ok) && (originalClassification != FileSelection.Dark))
            {
                this.ClassificationInformation.Text = "Classification skipped.";
                return;
            }

            if (isColor)
            {
                this.ClassificationInformation.Text = "File is in color and therefore not dark.";
                return;
            }

            this.ClassificationInformation.Text = String.Format("{0:##0.0}% of pixels are darker than the threshold.", 100.0 * darkPixelRatioFound);
        }

        private void UpdateClassificationProgress(ImageQuality imageQuality)
        {
            imageQuality.Image.SetSource(this.Image);
            this.FileName.Content = imageQuality.FileName;
            this.FileName.ToolTip = this.FileName.Content;

            this.UpdateClassificationFeedback(imageQuality.OldImageQuality, imageQuality.NewImageQuality, imageQuality.DarkPixelRatioFound, imageQuality.IsColor);
            this.NewClassification.Content = imageQuality.NewImageQuality;

            this.FileNavigatorSlider.Value = this.filesProcessed - 1;
        }

        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            // make sure the title bar of the dialog box is on the screen. For small screens it may default to being off the screen
            Utilities.SetDefaultDialogPosition(this);
            Utilities.TryFitWindowInWorkingArea(this);

            this.DarkPixelPercentageThreshold.Value = 100.0 * this.userSettings.DarkPixelRatioThreshold;
            this.DarkPixelPercentageThreshold.ValueChanged += this.DarkPixel_ValueChanged;

            this.DarkPixelThreshold.Value = this.userSettings.DarkPixelThreshold;
            this.DarkPixelThreshold.ValueChanged += this.DarkPixel_ValueChanged;

            this.FileNavigatorSlider.Minimum = 0;
            this.FileNavigatorSlider.Maximum = this.database.CurrentlySelectedFileCount - 1;
            Utilities.ConfigureNavigatorSliderTick(this.FileNavigatorSlider);
            this.FileNavigatorSlider.Value = this.fileEnumerator.CurrentRow;
            this.FileNavigatorSlider_ValueChanged(null, null);
            this.FileNavigatorSlider.ValueChanged += this.FileNavigatorSlider_ValueChanged;

            this.Focus();               // necessary for the left/right arrow keys to work
        }

        private async void Window_PreviewKeyDown(object sender, KeyEventArgs e)
        {
            // if its an arrow key and the textbox doesn't have the focus navigate left/right to the next/previous file
            switch (e.Key)
            {
                case Key.End:
                    e.Handled = true;
                    await this.ShowFileWithoutSliderCallbackAsync(this.database.CurrentlySelectedFileCount - 1);
                    break;
                case Key.Home:
                    e.Handled = true;
                    await this.ShowFileWithoutSliderCallbackAsync(0);
                    break;
                case Key.Left:              // previous file
                    e.Handled = true;
                    await this.ShowFileWithoutSliderCallbackAsync(false, Keyboard.Modifiers);
                    break;
                case Key.Right:             // next file
                    e.Handled = true;
                    await this.ShowFileWithoutSliderCallbackAsync(true, Keyboard.Modifiers);
                    break;
                default:
                    return;
            }
        }
    }
}
