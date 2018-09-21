using Carnassial.Control;
using Carnassial.Util;
using System.Windows;

namespace Carnassial.Dialog
{
    public partial class AdvancedCarnassialOptions : WindowWithSystemMenu
    {
        private CarnassialState carnassialState;
        private FileDisplayWithMarkers fileDisplay;

        public AdvancedCarnassialOptions(CarnassialState state, FileDisplayWithMarkers fileDisplay, Window owner)
        {
            this.InitializeComponent();
            this.Owner = owner;
            this.carnassialState = state;
            this.fileDisplay = fileDisplay;

            // throttles
            this.ImageRendersPerSecond.Minimum = Constant.ThrottleValues.DesiredMaximumImageRendersPerSecondLowerBound;
            this.ImageRendersPerSecond.Maximum = Constant.ThrottleValues.DesiredMaximumImageRendersPerSecondUpperBound;
            this.ImageRendersPerSecond.Value = this.carnassialState.Throttles.DesiredImageRendersPerSecond;
            this.ImageRendersPerSecond.ValueChanged += this.ImageRendersPerSecond_ValueChanged;
            this.ImageRendersPerSecond.ToolTip = this.carnassialState.Throttles.DesiredImageRendersPerSecond;

            this.ImageClassificationChangeSlowdown.Minimum = Constant.ThrottleValues.ImageClassificationSlowdownMinimum;
            this.ImageClassificationChangeSlowdown.Maximum = Constant.ThrottleValues.ImageClassificationSlowdownMaximum;
            this.ImageClassificationChangeSlowdown.Value = this.carnassialState.Throttles.ImageClassificationChangeSlowdown;
            this.ImageClassificationChangeSlowdown.ValueChanged += this.ImageClassificationChangeSlowdown_ValueChanged;
            this.ImageClassificationChangeSlowdown.ToolTip = this.carnassialState.Throttles.ImageClassificationChangeSlowdown;

            this.VideoSlowdown.Minimum = Constant.ThrottleValues.VideoSlowdownMinimum;
            this.VideoSlowdown.Maximum = Constant.ThrottleValues.VideoSlowdownMaximum;
            this.VideoSlowdown.Value = this.carnassialState.Throttles.VideoSlowdown;
            this.VideoSlowdown.ValueChanged += this.VideoSlowdown_ValueChanged;
            this.VideoSlowdown.ToolTip = this.carnassialState.Throttles.VideoSlowdown;

            // maixmum zoom
            this.MaxZoom.Value = this.fileDisplay.ZoomMaximum;
            this.MaxZoom.ToolTip = this.fileDisplay.ZoomMaximum;
            this.MaxZoom.Maximum = Constant.MarkableCanvas.ImageZoomMaximumRangeMaximum;
            this.MaxZoom.Minimum = Constant.MarkableCanvas.ImageZoomMaximumRangeMinimum;

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
            this.carnassialState.Throttles.ImageClassificationChangeSlowdown = this.ImageClassificationChangeSlowdown.Value;
            this.ImageClassificationChangeSlowdown.ToolTip = this.carnassialState.Throttles.ImageClassificationChangeSlowdown;
        }

        private void ImageRendersPerSecond_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            this.carnassialState.Throttles.SetDesiredImageRendersPerSecond(this.ImageRendersPerSecond.Value);
            this.ImageRendersPerSecond.ToolTip = this.carnassialState.Throttles.DesiredImageRendersPerSecond;
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
            this.carnassialState.Throttles.ImageClassificationChangeSlowdown = Constant.ThrottleValues.ImageClassificationSlowdownDefault;
            this.ImageClassificationChangeSlowdown.Value = this.carnassialState.Throttles.ImageClassificationChangeSlowdown;
            this.ImageClassificationChangeSlowdown.ToolTip = this.carnassialState.Throttles.ImageClassificationChangeSlowdown;
        }

        private void ResetImageDifferencingThreshold_Click(object sender, RoutedEventArgs e)
        {
            this.carnassialState.DifferenceThreshold = Constant.Images.DifferenceThresholdDefault;
            this.DifferenceThreshold.Value = this.carnassialState.DifferenceThreshold;
            this.DifferenceThreshold.ToolTip = this.carnassialState.DifferenceThreshold;
        }

        private void ResetImageRendersPerSecond_Click(object sender, RoutedEventArgs e)
        {
            this.carnassialState.Throttles.SetDesiredImageRendersPerSecond(Constant.ThrottleValues.DesiredMaximumImageRendersPerSecondDefault);
            this.ImageRendersPerSecond.Value = this.carnassialState.Throttles.DesiredImageRendersPerSecond;
            this.ImageRendersPerSecond.ToolTip = this.carnassialState.Throttles.DesiredImageRendersPerSecond;
        }

        private void ResetMaxZoom_Click(object sender, RoutedEventArgs e)
        {
            this.fileDisplay.ResetMaximumZoom();
            this.MaxZoom.Value = this.fileDisplay.ZoomMaximum;
            this.MaxZoom.ToolTip = this.fileDisplay.ZoomMaximum;
        }

        private void ResetVideoSlowdown_Click(object sender, RoutedEventArgs e)
        {
            this.carnassialState.Throttles.VideoSlowdown = Constant.ThrottleValues.VideoSlowdownDefault;
            this.VideoSlowdown.Value = this.carnassialState.Throttles.VideoSlowdown;
            this.VideoSlowdown.ToolTip = this.carnassialState.Throttles.VideoSlowdown;
        }

        private void VideoSlowdown_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            this.carnassialState.Throttles.VideoSlowdown = this.VideoSlowdown.Value;
            this.VideoSlowdown.ToolTip = this.carnassialState.Throttles.VideoSlowdown;
        }

        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            CommonUserInterface.SetDefaultDialogPosition(this);
            CommonUserInterface.TryFitWindowInWorkingArea(this);
        }
    }
}
