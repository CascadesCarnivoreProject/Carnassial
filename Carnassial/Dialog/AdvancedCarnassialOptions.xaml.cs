using Carnassial.Control;
using Carnassial.Util;
using System.Windows;

namespace Carnassial.Dialog
{
    public partial class AdvancedCarnassialOptions : Window
    {
        private CarnassialState carnassialState;
        private FileDisplayWithMarkers fileDisplay;

        public AdvancedCarnassialOptions(CarnassialState state, FileDisplayWithMarkers fileDisplay, Window owner)
        {
            this.InitializeComponent();
            this.Owner = owner;
            this.carnassialState = state;
            this.fileDisplay = fileDisplay;

            // Throttles
            this.ImageRendersPerSecond.Minimum = Constant.ThrottleValues.DesiredMaximumImageRendersPerSecondLowerBound;
            this.ImageRendersPerSecond.Maximum = Constant.ThrottleValues.DesiredMaximumImageRendersPerSecondUpperBound;
            this.ImageRendersPerSecond.Value = this.carnassialState.Throttles.DesiredImageRendersPerSecond;
            this.ImageRendersPerSecond.ValueChanged += this.ImageRendersPerSecond_ValueChanged;
            this.ImageRendersPerSecond.ToolTip = this.carnassialState.Throttles.DesiredImageRendersPerSecond;

            // The Max Zoom Value
            this.MaxZoom.Value = this.fileDisplay.ZoomMaximum;
            this.MaxZoom.ToolTip = this.fileDisplay.ZoomMaximum;
            this.MaxZoom.Maximum = Constant.MarkableCanvas.ImageZoomMaximumRangeMaximum;
            this.MaxZoom.Minimum = Constant.MarkableCanvas.ImageZoomMaximumRangeMinimum;

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

        private void ResetImageRendersPerSecond_Click(object sender, RoutedEventArgs e)
        {
            this.carnassialState.Throttles.ResetToDefaults();
            this.ImageRendersPerSecond.Value = this.carnassialState.Throttles.DesiredImageRendersPerSecond;
            this.ImageRendersPerSecond.ToolTip = this.carnassialState.Throttles.DesiredImageRendersPerSecond;
        }

        // Reset the maximum zoom to the amount specified in Max Zoom;
        private void ResetMaxZoom_Click(object sender, RoutedEventArgs e)
        {
            this.fileDisplay.ResetMaximumZoom();
            this.MaxZoom.Value = this.fileDisplay.ZoomMaximum;
            this.MaxZoom.ToolTip = this.fileDisplay.ZoomMaximum;
        }

        // Callback: The user has changed the maximum zoom value
        private void MaxZoom_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            this.fileDisplay.ZoomMaximum = (int)this.MaxZoom.Value;
            this.MaxZoom.ToolTip = this.fileDisplay.ZoomMaximum;
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
