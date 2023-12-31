using Carnassial.Control;
using Carnassial.Util;
using System.Windows;

namespace Carnassial.Dialog
{
    public partial class AdvancedCarnassialOptions : WindowWithSystemMenu
    {
        private readonly CarnassialState carnassialState;
        private readonly FileDisplayWithMarkers fileDisplay;

        public AdvancedCarnassialOptions(CarnassialState state, FileDisplayWithMarkers fileDisplay, Window owner)
        {
            this.InitializeComponent();
            this.Message.SetVisibility();
            this.Owner = owner;
            this.carnassialState = state;
            this.fileDisplay = fileDisplay;

            // throttles
            this.ImageRendersPerSecond.Minimum = Constant.ThrottleValues.DesiredMaximumImageRendersPerSecondLowerBound;
            this.ImageRendersPerSecond.Maximum = Constant.ThrottleValues.DesiredMaximumImageRendersPerSecondUpperBound;
            this.ImageRendersPerSecond.Value = CarnassialSettings.Default.DesiredImageRendersPerSecond;
            this.ImageRendersPerSecond.ValueChanged += this.ImageRendersPerSecond_ValueChanged;
            this.ImageRendersPerSecond.ToolTip = CarnassialSettings.Default.DesiredImageRendersPerSecond;

            this.ImageClassificationChangeSlowdown.Minimum = Constant.ThrottleValues.ImageClassificationSlowdownMinimum;
            this.ImageClassificationChangeSlowdown.Maximum = Constant.ThrottleValues.ImageClassificationSlowdownMaximum;
            this.ImageClassificationChangeSlowdown.Value = CarnassialSettings.Default.ImageClassificationChangeSlowdown;
            this.ImageClassificationChangeSlowdown.ValueChanged += this.ImageClassificationChangeSlowdown_ValueChanged;
            this.ImageClassificationChangeSlowdown.ToolTip = CarnassialSettings.Default.ImageClassificationChangeSlowdown;

            this.VideoSlowdown.Minimum = Constant.ThrottleValues.VideoSlowdownMinimum;
            this.VideoSlowdown.Maximum = Constant.ThrottleValues.VideoSlowdownMaximum;
            this.VideoSlowdown.Value = CarnassialSettings.Default.VideoSlowdown;
            this.VideoSlowdown.ValueChanged += this.VideoSlowdown_ValueChanged;
            this.VideoSlowdown.ToolTip = CarnassialSettings.Default.VideoSlowdown;

            // maixmum zoom
            this.MaxZoom.Value = this.fileDisplay.ZoomMaximum;
            this.MaxZoom.ToolTip = this.fileDisplay.ZoomMaximum;
            this.MaxZoom.Maximum = Constant.ImageDisplay.ImageZoomMaximumRangeMaximum;
            this.MaxZoom.Minimum = Constant.ImageDisplay.ImageZoomMaximumRangeMinimum;

            // image differencing
            this.DifferenceThreshold.Value = this.carnassialState.DifferenceThreshold;
            this.DifferenceThreshold.ToolTip = this.carnassialState.DifferenceThreshold;
            this.DifferenceThreshold.Maximum = Constant.Images.DifferenceThresholdMax;
            this.DifferenceThreshold.Minimum = Constant.Images.DifferenceThresholdMin;
        }

        private void DifferenceThreshold_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            this.carnassialState.DifferenceThreshold = (byte)this.DifferenceThreshold.Value;
            this.DifferenceThreshold.ToolTip = this.carnassialState.DifferenceThreshold;
        }

        private void ImageClassificationChangeSlowdown_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            CarnassialSettings.Default.ImageClassificationChangeSlowdown = (float)this.ImageClassificationChangeSlowdown.Value;
            this.ImageClassificationChangeSlowdown.ToolTip = CarnassialSettings.Default.ImageClassificationChangeSlowdown;
        }

        private void ImageRendersPerSecond_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            this.carnassialState.Throttles.SetDesiredImageRendersPerSecond((float)this.ImageRendersPerSecond.Value);
            this.ImageRendersPerSecond.ToolTip = CarnassialSettings.Default.DesiredImageRendersPerSecond;
        }

        private void MaxZoom_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            this.fileDisplay.ZoomMaximum = (int)this.MaxZoom.Value;
            this.MaxZoom.ToolTip = this.fileDisplay.ZoomMaximum;
        }

        private void OkButton_Click(object sender, RoutedEventArgs e)
        {
            this.DialogResult = true;
        }

        private void ResetImageClassificationChangeSlowdown_Click(object sender, RoutedEventArgs e)
        {
            float imageClassificationChangeSlowdown = Constant.ThrottleValues.ImageClassificationSlowdownDefault;
            CarnassialSettings.Default.ImageClassificationChangeSlowdown = imageClassificationChangeSlowdown;
            this.ImageClassificationChangeSlowdown.Value = imageClassificationChangeSlowdown;
            this.ImageClassificationChangeSlowdown.ToolTip = imageClassificationChangeSlowdown;
        }

        private void ResetImageDifferencingThreshold_Click(object sender, RoutedEventArgs e)
        {
            this.carnassialState.DifferenceThreshold = Constant.Images.DifferenceThresholdDefault;
            this.DifferenceThreshold.Value = this.carnassialState.DifferenceThreshold;
            this.DifferenceThreshold.ToolTip = this.carnassialState.DifferenceThreshold;
        }

        private void ResetImageRendersPerSecond_Click(object sender, RoutedEventArgs e)
        {
            float desiredMaximumImageRendersPerSecondDefault = Constant.ThrottleValues.DesiredMaximumImageRendersPerSecondDefault;
            this.carnassialState.Throttles.SetDesiredImageRendersPerSecond(desiredMaximumImageRendersPerSecondDefault);
            this.ImageRendersPerSecond.Value = desiredMaximumImageRendersPerSecondDefault;
            this.ImageRendersPerSecond.ToolTip = desiredMaximumImageRendersPerSecondDefault;
        }

        private void ResetMaxZoom_Click(object sender, RoutedEventArgs e)
        {
            this.fileDisplay.ResetMaximumZoom();
            this.MaxZoom.Value = this.fileDisplay.ZoomMaximum;
            this.MaxZoom.ToolTip = this.fileDisplay.ZoomMaximum;
        }

        private void ResetVideoSlowdown_Click(object sender, RoutedEventArgs e)
        {
            float videoSlowdown = Constant.ThrottleValues.VideoSlowdownDefault;
            this.VideoSlowdown.Value = videoSlowdown;
            this.VideoSlowdown.ToolTip = videoSlowdown;
        }

        private void VideoSlowdown_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            float videoSlowdown = (float)this.VideoSlowdown.Value;
            CarnassialSettings.Default.VideoSlowdown = videoSlowdown;
            this.VideoSlowdown.ToolTip = videoSlowdown;
        }

        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            CommonUserInterface.SetDefaultDialogPosition(this);
            CommonUserInterface.TryFitWindowInWorkingArea(this);
        }
    }
}
