using System.Windows;
using System.Windows.Media;

namespace Carnassial.Control
{
    public static class FrameworkElementExtensions
    {
        public static int GetWidthInPixels(this FrameworkElement element)
        {
            // the first time this method is called it's likely on an image display control which hasn't yet stretched to fully
            // occupy its available space
            // Within Carnassial, FileDisplay is commonly a Grid element with HorizonalAlignment = Center. This grants it infinite 
            // space during initial layout where the message is visible but, as the message is empty, the width used is set by the 
            // empty message's margin.  It's therefore narrow, typically resulting in layout of the FileDisplay as a vertically 
            // oriented rectangle with a high aspect ratio.  Since trail camera images are nearly always horizontally oriented,
            // this results in ActualWidth being set to a value much lower than the width at which the image will be displayed once
            // it's loaded.  There does not appear to be a simple XAML solution to this as DockPanel and Viewbox both lack
            // HorizontalContentAlignment.  As a workaround, estimate the actual image display width in such cases to avoid low
            // resolution image loads which result in low quality display.
            double expectedDeviceIndependentDisplayWidth = element.ActualWidth;
            if (element.ActualHeight > element.ActualWidth)
            {
                expectedDeviceIndependentDisplayWidth = 4.0 / 3.0 * element.ActualHeight; // for now, assume a 4:3 sensor
            }

            return (int)(VisualTreeHelper.GetDpi(element).DpiScaleX * expectedDeviceIndependentDisplayWidth);
        }
    }
}
