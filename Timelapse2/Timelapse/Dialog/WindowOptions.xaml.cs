using System.Windows;
using Timelapse.Util;

namespace Timelapse.Dialog
{
    public partial class WindowOptions : Window
    {
        private Images.MarkableImageCanvas markableCanvas;
        private TimelapseWindow mainProgram;
        private Throttles throttle;

        public WindowOptions(TimelapseWindow mainWindow, Images.MarkableImageCanvas mcanvas, Throttles throttle)
        {
            this.InitializeComponent();
            this.throttle = throttle;
            this.Topmost = true;
            this.markableCanvas = mcanvas;
            this.mainProgram = mainWindow;

            // Throttles
            this.sldrMaxThrottle.Minimum = Constants.ThrottleValues.DesiredMaximumImageRendersPerSecondLowerBound;
            this.sldrMaxThrottle.Maximum = Constants.ThrottleValues.DesiredMaximumImageRendersPerSecondUpperBound;
            this.sldrMaxThrottle.Value = this.throttle.DesiredImageRendersPerSecond;
            this.sldrMaxThrottle.ValueChanged += this.SldrMaxThrottle_ValueChanged;
            this.sldrMaxThrottle.ToolTip = this.throttle.DesiredImageRendersPerSecond;

            // The Max Zoom Value
            this.sldrMaxZoom.Value = this.markableCanvas.MaxZoom;
            this.sldrMaxZoom.ToolTip = this.markableCanvas.MaxZoom;
            this.sldrMaxZoom.Maximum = this.markableCanvas.MaxZoomUpperBound;
            this.sldrMaxZoom.Minimum = 2;

            // Image Differencing Thresholds
            this.sldrDifferenceThreshold.Value = this.mainProgram.DifferenceThreshold;
            this.sldrDifferenceThreshold.ToolTip = this.mainProgram.DifferenceThreshold;
            this.sldrDifferenceThreshold.Maximum = Constants.Images.DifferenceThresholdMax;
            this.sldrDifferenceThreshold.Minimum = Constants.Images.DifferenceThresholdMin;
        }

        #region Throttling
        private void SldrMaxThrottle_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            this.throttle.DesiredImageRendersPerSecond = this.sldrMaxThrottle.Value;
            this.sldrMaxThrottle.ToolTip = this.throttle.DesiredImageRendersPerSecond;
        }

        private void BtnResetThrottle_Click(object sender, RoutedEventArgs e)
        {
            this.throttle.SetToSystemDefaults();
            this.sldrMaxThrottle.Value = this.throttle.DesiredImageRendersPerSecond;
            this.sldrMaxThrottle.ToolTip = this.throttle.DesiredImageRendersPerSecond;
        }
        #endregion

        #region Maximum Zoom levels
        // Reset the maximum zoom to the amount specified in Max Zoom;
        private void ResetMaxZoomButton_Click(object sender, RoutedEventArgs e)
        {
            this.markableCanvas.ResetMaxZoom();
            this.sldrMaxZoom.Value = this.markableCanvas.MaxZoom;
            this.sldrMaxZoom.ToolTip = this.markableCanvas.MaxZoom;
        }

        // Callback: The user has changed the maximum zoom value
        private void MaxZoomSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            this.markableCanvas.MaxZoom = (int)this.sldrMaxZoom.Value;
            this.sldrMaxZoom.ToolTip = this.markableCanvas.MaxZoom;
        }
        #endregion

        #region Image Differencing
        private void ResetImageDifferencingButton_Click(object sender, RoutedEventArgs e)
        {
            this.mainProgram.ResetDifferenceThreshold();
            this.sldrDifferenceThreshold.Value = this.mainProgram.DifferenceThreshold;
            this.sldrDifferenceThreshold.ToolTip = this.mainProgram.DifferenceThreshold;
        }

        private void DifferenceThresholdSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            this.mainProgram.DifferenceThreshold = (byte)this.sldrDifferenceThreshold.Value;
            this.sldrDifferenceThreshold.ToolTip = this.mainProgram.DifferenceThreshold;
        }
        #endregion

        private void CloseWindowButton_Click(object sender, RoutedEventArgs e)
        {
            this.Close();
        } 
    }
}
