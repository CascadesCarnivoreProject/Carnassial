using System.Windows;
using Timelapse.Images;
using Timelapse.Util;

namespace Timelapse.Dialog
{
    public partial class AdvancedTimelapseOptions : Window
    {
        private MarkableImageCanvas markableCanvas;
        private TimelapseState timelapseState;

        public AdvancedTimelapseOptions(TimelapseState timelapseState, MarkableImageCanvas markableCanvas, Window owner)
        {
            this.InitializeComponent();
            this.Owner = owner;
            this.markableCanvas = markableCanvas;
            this.timelapseState = timelapseState;

            // Throttles
            this.ImageRendersPerSecond.Minimum = Constants.ThrottleValues.DesiredMaximumImageRendersPerSecondLowerBound;
            this.ImageRendersPerSecond.Maximum = Constants.ThrottleValues.DesiredMaximumImageRendersPerSecondUpperBound;
            this.ImageRendersPerSecond.Value = this.timelapseState.Throttles.DesiredImageRendersPerSecond;
            this.ImageRendersPerSecond.ValueChanged += this.ImageRendersPerSecond_ValueChanged;
            this.ImageRendersPerSecond.ToolTip = this.timelapseState.Throttles.DesiredImageRendersPerSecond;

            // The Max Zoom Value
            this.MaxZoom.Value = this.markableCanvas.MaxZoom;
            this.MaxZoom.ToolTip = this.markableCanvas.MaxZoom;
            this.MaxZoom.Maximum = this.markableCanvas.MaxZoomUpperBound;
            this.MaxZoom.Minimum = 2;

            // Image Differencing Thresholds
            this.DifferenceThreshold.Value = this.timelapseState.DifferenceThreshold;
            this.DifferenceThreshold.ToolTip = this.timelapseState.DifferenceThreshold;
            this.DifferenceThreshold.Maximum = Constants.Images.DifferenceThresholdMax;
            this.DifferenceThreshold.Minimum = Constants.Images.DifferenceThresholdMin;
        }

        private void ImageRendersPerSecond_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            this.timelapseState.Throttles.SetDesiredImageRendersPerSecond(this.ImageRendersPerSecond.Value);
            this.ImageRendersPerSecond.ToolTip = this.timelapseState.Throttles.DesiredImageRendersPerSecond;
        }

        private void ResetThrottle_Click(object sender, RoutedEventArgs e)
        {
            this.timelapseState.Throttles.ResetToDefaults();
            this.ImageRendersPerSecond.Value = this.timelapseState.Throttles.DesiredImageRendersPerSecond;
            this.ImageRendersPerSecond.ToolTip = this.timelapseState.Throttles.DesiredImageRendersPerSecond;
        }

        // Reset the maximum zoom to the amount specified in Max Zoom;
        private void ResetMaxZoom_Click(object sender, RoutedEventArgs e)
        {
            this.markableCanvas.ResetMaxZoom();
            this.MaxZoom.Value = this.markableCanvas.MaxZoom;
            this.MaxZoom.ToolTip = this.markableCanvas.MaxZoom;
        }

        // Callback: The user has changed the maximum zoom value
        private void MaxZoom_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            this.markableCanvas.MaxZoom = (int)this.MaxZoom.Value;
            this.MaxZoom.ToolTip = this.markableCanvas.MaxZoom;
        }

        private void ResetImageDifferencingButton_Click(object sender, RoutedEventArgs e)
        {
            this.timelapseState.DifferenceThreshold = Constants.Images.DifferenceThresholdDefault;
            this.DifferenceThreshold.Value = this.timelapseState.DifferenceThreshold;
            this.DifferenceThreshold.ToolTip = this.timelapseState.DifferenceThreshold;
        }

        private void DifferenceThreshold_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            this.timelapseState.DifferenceThreshold = (byte)this.DifferenceThreshold.Value;
            this.DifferenceThreshold.ToolTip = this.timelapseState.DifferenceThreshold;
        }

        private void OkButton_Click(object sender, RoutedEventArgs e)
        {
            this.DialogResult = true;
        }
    }
}
