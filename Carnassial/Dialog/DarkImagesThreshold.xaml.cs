using Carnassial.Data;
using Carnassial.Images;
using Carnassial.Native;
using Carnassial.Util;
using System;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;

namespace Carnassial.Dialog
{
    public partial class DarkImagesThreshold : Window, IDisposable
    {
        private const int MinimumDarkColorRectangleHeight = 10;

        private bool disposed;
        private FileDatabase fileDatabase;
        private FileTableEnumerator fileEnumerator;
        private ImageProperties imageProperties;
        private bool isProgramaticNavigatiorSliderUpdate;
        private ImageRow previousFile;
        private DarkImagesIOComputeTransaction reclassification;
        private CarnassialUserRegistrySettings userSettings;

        public DarkImagesThreshold(FileDatabase database, int currentFileIndex, CarnassialUserRegistrySettings state, Window owner)
        {
            this.InitializeComponent();
            this.Owner = owner;

            this.fileDatabase = database;
            this.disposed = false;
            this.fileEnumerator = new FileTableEnumerator(database, currentFileIndex);
            this.imageProperties = null;
            this.isProgramaticNavigatiorSliderUpdate = false;
            this.previousFile = null;
            this.reclassification = null;
            this.userSettings = state;
        }

        /// <summary>
        /// Display classification of current file with the current threshold settings. Does not update the database.
        /// </summary>
        private void ClassifyCurrentFile()
        {
            FileClassification newClassification = this.imageProperties.EvaluateNewClassification(0.01 * this.DarkLuminosityThresholdPercent.Value);
            this.DisplayClassification(this.fileEnumerator.Current, this.imageProperties, newClassification);
        }

        private void DisplayClassification(ImageRow file, ImageProperties imageProperties, FileClassification newClassificationToDisplay)
        {
            this.OriginalClassification.Content = file.Classification;
            this.NewClassification.Content = newClassificationToDisplay;

            if ((file.Classification == FileClassification.Corrupt) ||
                (file.Classification == FileClassification.NoLongerAvailable))
            {
                this.ClassificationInformation.Text = "File could not be loaded.  Classification skipped.";
            }
            else
            {
                this.ClassificationInformation.Text = imageProperties.GetClassificationDescription();
            }
        }

        private async Task DisplayFileAndClassificationAsync()
        {
            if (Object.ReferenceEquals(this.fileEnumerator.Current, this.previousFile))
            {
                // no change in the file displayed, so nothing to do
                return;
            }

            using (MemoryImage image = await this.fileEnumerator.Current.LoadAsync(this.fileDatabase.FolderPath, (int)this.Width))
            {
                image.SetSource(this.Image);
            }
            this.FileName.Content = this.fileEnumerator.Current.FileName;
            this.FileName.ToolTip = this.FileName.Content;
            this.imageProperties = this.fileEnumerator.Current.TryGetThumbnailProperties(this.fileDatabase.FolderPath);

            this.ClassifyCurrentFile();
            this.previousFile = this.fileEnumerator.Current;
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
            // second click - done
            if (this.reclassification != null)
            {
                this.DialogResult = true;
                return;
            }

            // first click - apply new threshold to selected files
            this.ApplyDoneButton.Content = "_Done";
            this.ApplyDoneButton.IsEnabled = false;
            this.CancelStopButton.Content = "_Stop";
            this.DarkLuminosityThresholdPercent.IsEnabled = false;
            this.FileNavigatorSlider.IsEnabled = false;
            this.MenuReset.IsEnabled = false;
            this.userSettings.DarkLuminosityThreshold = 0.01 * this.DarkLuminosityThresholdPercent.Value;

            using (this.reclassification = new DarkImagesIOComputeTransaction(this.UpdateClassificationStatus, this.userSettings.Throttles.GetDesiredIntervalBetweenFileLoadProgress()))
            {
                await reclassification.ReclassifyFilesAsync(this.fileDatabase, 0.01 * this.userSettings.DarkLuminosityThreshold, (int)this.ActualWidth);
            }

            await this.DisplayFileAndClassificationAsync();
            this.ApplyDoneButton.IsEnabled = true;
            this.CancelStopButton.IsEnabled = false;
        }

        // Cancel or Stop - exit the dialog
        private void CancelStopButton_Click(object sender, RoutedEventArgs e)
        {
            if (this.reclassification != null)
            {
                this.reclassification.ShouldExitCurrentIteration = true;
            }
            this.DialogResult = false;
        }

        private async void FileNavigatorSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            if (this.isProgramaticNavigatiorSliderUpdate)
            {
                return;
            }

            this.fileEnumerator.TryMoveToFile((int)this.FileNavigatorSlider.Value);
            await this.DisplayFileAndClassificationAsync();
        }

        private void MenuResetCurrent_Click(object sender, RoutedEventArgs e)
        {
            this.ResetDarkLuminosityThreshold(this.userSettings.DarkLuminosityThreshold);
        }

        private void MenuResetDefault_Click(object sender, RoutedEventArgs e)
        {
            this.ResetDarkLuminosityThreshold(Constant.Images.DarkLuminosityThresholdDefault);
        }

        private void ResetDarkLuminosityThreshold(double darkLuminosityThreshold)
        {
            this.DarkLuminosityThresholdPercent.Value = 100.0 * darkLuminosityThreshold;
            this.ClassifyCurrentFile();
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
            if (newFileIndex >= this.fileDatabase.CurrentlySelectedFileCount)
            {
                newFileIndex = this.fileDatabase.CurrentlySelectedFileCount - 1;
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
            await this.DisplayFileAndClassificationAsync();
        }

        private void UpdateClassificationStatus(ReclassifyStatus status)
        {
            if (status.File != null)
            {
                this.FileName.Content = status.File.FileName;
                this.FileName.ToolTip = this.FileName.Content;
                this.DisplayClassification(status.File, status.ImageProperties, status.ClassificationToDisplay);
            }
            if (status.Image != null)
            {
                status.Image.SetSource(this.Image);
            }

            this.isProgramaticNavigatiorSliderUpdate = true;
            this.FileNavigatorSlider.Value = status.CurrentFileIndex - 1;
            this.isProgramaticNavigatiorSliderUpdate = false;
        }

        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            // make sure the title bar of the dialog box is on the screen. For small screens it may default to being off the screen
            Utilities.SetDefaultDialogPosition(this);
            Utilities.TryFitWindowInWorkingArea(this);

            this.DarkLuminosityThresholdPercent.Value = 100.0 * this.userSettings.DarkLuminosityThreshold;

            this.FileNavigatorSlider.Minimum = 0;
            this.FileNavigatorSlider.Maximum = this.fileDatabase.CurrentlySelectedFileCount - 1;
            Utilities.ConfigureNavigatorSliderTick(this.FileNavigatorSlider);
            this.FileNavigatorSlider.Value = this.fileEnumerator.CurrentRow;
            this.FileNavigatorSlider_ValueChanged(this, null);
            this.FileNavigatorSlider.ValueChanged += this.FileNavigatorSlider_ValueChanged;

            this.Focus(); // necessary for the left/right arrow keys to work
        }

        private async void Window_PreviewKeyDown(object sender, KeyEventArgs e)
        {
            switch (e.Key)
            {
                case Key.End:
                    e.Handled = true;
                    await this.ShowFileWithoutSliderCallbackAsync(this.fileDatabase.CurrentlySelectedFileCount - 1);
                    break;
                case Key.Home:
                    e.Handled = true;
                    await this.ShowFileWithoutSliderCallbackAsync(0);
                    break;
                case Key.Left:  // previous file
                    e.Handled = true;
                    await this.ShowFileWithoutSliderCallbackAsync(false, Keyboard.Modifiers);
                    break;
                case Key.Right: // next file
                    e.Handled = true;
                    await this.ShowFileWithoutSliderCallbackAsync(true, Keyboard.Modifiers);
                    break;
                default:
                    return;
            }
        }
    }
}
