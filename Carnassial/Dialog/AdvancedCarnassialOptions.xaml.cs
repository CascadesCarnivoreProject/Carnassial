using Carnassial.Images;
using Carnassial.Util;
using System.Windows;

namespace Carnassial.Dialog
{
    public partial class AdvancedCarnassialOptions : Window
    {
        private CarnassialState carnassialState;
        private MarkableImageCanvas markableCanvas;

        public AdvancedCarnassialOptions(CarnassialState state, MarkableImageCanvas markableCanvas, Window owner)
        {
            this.InitializeComponent();
            this.Owner = owner;
            this.carnassialState = state;
            this.markableCanvas = markableCanvas;

            // Throttles
            this.ImageRendersPerSecond.Minimum = Constant.ThrottleValues.DesiredMaximumImageRendersPerSecondLowerBound;
            this.ImageRendersPerSecond.Maximum = Constant.ThrottleValues.DesiredMaximumImageRendersPerSecondUpperBound;
            this.ImageRendersPerSecond.Value = this.carnassialState.Throttles.DesiredImageRendersPerSecond;
            this.ImageRendersPerSecond.ValueChanged += this.ImageRendersPerSecond_ValueChanged;
            this.ImageRendersPerSecond.ToolTip = this.carnassialState.Throttles.DesiredImageRendersPerSecond;

            // The Max Zoom Value
            this.MaxZoom.Value = this.markableCanvas.MaxZoom;
            this.MaxZoom.ToolTip = this.markableCanvas.MaxZoom;
            this.MaxZoom.Maximum = this.markableCanvas.MaxZoomUpperBound;
            this.MaxZoom.Minimum = 2;

            // Image Differencing Thresholds
            this.DifferenceThreshold.Value = this.carnassialState.DifferenceThreshold;
            this.DifferenceThreshold.ToolTip = this.carnassialState.DifferenceThreshold;
            this.DifferenceThreshold.Maximum = Constant.Images.DifferenceThresholdMax;
            this.DifferenceThreshold.Minimum = Constant.Images.DifferenceThresholdMin;
        }

        private void ImageRendersPerSecond_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            this.carnassialState.Throttles.SetDesiredImageRendersPerSecond(this.ImageRendersPerSecond.Value);
            this.ImageRendersPerSecond.ToolTip = this.carnassialState.Throttles.DesiredImageRendersPerSecond;
        }

        private void ResetThrottle_Click(object sender, RoutedEventArgs e)
        {
            this.carnassialState.Throttles.ResetToDefaults();
            this.ImageRendersPerSecond.Value = this.carnassialState.Throttles.DesiredImageRendersPerSecond;
            this.ImageRendersPerSecond.ToolTip = this.carnassialState.Throttles.DesiredImageRendersPerSecond;
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
            this.carnassialState.DifferenceThreshold = Constant.Images.DifferenceThresholdDefault;
            this.DifferenceThreshold.Value = this.carnassialState.DifferenceThreshold;
            this.DifferenceThreshold.ToolTip = this.carnassialState.DifferenceThreshold;
        }

        private void DifferenceThreshold_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            this.carnassialState.DifferenceThreshold = (byte)this.DifferenceThreshold.Value;
            this.DifferenceThreshold.ToolTip = this.carnassialState.DifferenceThreshold;
        }

        private void OkButton_Click(object sender, RoutedEventArgs e)
        {
            this.DialogResult = true;
        }
    }
}
