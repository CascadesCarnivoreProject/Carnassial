using System.Windows;
using System.Windows.Controls;

namespace Timelapse
{
    /// <summary>
    /// Interaction logic for OptionsWindow.xaml
    /// </summary>
    public partial class OptionsWindow : Window
    {
        private Timelapse.Images.MarkableImageCanvas markableCanvas;
        private TimelapseWindow mainProgram;

        public OptionsWindow(TimelapseWindow mainWindow, Timelapse.Images.MarkableImageCanvas mcanvas)
        {
            this.InitializeComponent();
            this.Topmost = true;
            this.markableCanvas = mcanvas;
            this.mainProgram = mainWindow;

            // The Max Zoom Value
            sldrMaxZoom.Value = this.markableCanvas.MaxZoom;
            sldrMaxZoom.ToolTip = this.markableCanvas.MaxZoom;
            sldrMaxZoom.Maximum = this.markableCanvas.MaxZoomUpperBound;
            sldrMaxZoom.Minimum = 1;

            // Image Differencing Thresholds
            sldrDifferenceThreshold.Value = this.mainProgram.DifferenceThreshold;
            sldrDifferenceThreshold.ToolTip = this.mainProgram.DifferenceThreshold;
            sldrDifferenceThreshold.Maximum = Constants.Images.DifferenceThresholdMax;
            sldrDifferenceThreshold.Minimum = Constants.Images.DifferenceThresholdMin;
        }

        #region Maximum Zoom levels
        // Reset the maximum zoom to the amount specified in Max Zoom;
        private void ResetMaxZoomButton_Click(object sender, RoutedEventArgs e)
        {
            this.markableCanvas.ResetMaxZoom();
            sldrMaxZoom.Value = this.markableCanvas.MaxZoom;
            sldrMaxZoom.ToolTip = this.markableCanvas.MaxZoom;
        }

        // Callback: The user has changed the maximum zoom value
        private void MaxZoomSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            this.markableCanvas.MaxZoom = (int)sldrMaxZoom.Value;
            sldrMaxZoom.ToolTip = this.markableCanvas.MaxZoom;
        }
        #endregion

        #region Image Differencing
        private void ResetImageDifferencingButton_Click(object sender, RoutedEventArgs e)
        {
            this.mainProgram.ResetDifferenceThreshold();
            sldrDifferenceThreshold.Value = this.mainProgram.DifferenceThreshold;
            sldrDifferenceThreshold.ToolTip = this.mainProgram.DifferenceThreshold;
        }

        private void DifferenceThresholdSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            this.mainProgram.DifferenceThreshold = (byte)sldrDifferenceThreshold.Value;
            sldrDifferenceThreshold.ToolTip = this.mainProgram.DifferenceThreshold;
        }
        #endregion
        private void CloseWindowButton_Click(object sender, RoutedEventArgs e)
        {
            this.Close();
        }
    }
}
