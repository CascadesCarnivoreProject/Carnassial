using Carnassial.Util;
using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Shapes;

namespace Carnassial.Images
{
    internal class MagnifyingGlass : Canvas
    {
        private const int HandleStart = 200;
        private const int HandleEnd = 250;
        private const int LensDiameter = 250;

        private double lensAngle = 0;   // current angle of the lens only
        private Canvas lensCanvas;
        private Ellipse magnifierEllipse;
        private double magnifyingGlassAngle = 0;     // current angle of the entire magnifying glass
        private bool notYetRedrawn = true;

        public double ZoomValue { get; set; }
        public Point ZoomRange { get; set; }
        public bool IsVisibilityDesired { get; set; }
        public MarkableImageCanvas MarkableCanvasParent { get; set; }

        public MagnifyingGlass()
        {
            this.IsHitTestVisible = false;
            this.IsVisibilityDesired = false;
            this.HorizontalAlignment = HorizontalAlignment.Left;
            this.VerticalAlignment = VerticalAlignment.Top;
            this.Visibility = Visibility.Collapsed;
            this.ZoomValue = 60;

            // Create the handle of the magnifying glass
            Line lineHandle = new Line();
            lineHandle.StrokeThickness = 5;
            lineHandle.X1 = HandleStart;
            lineHandle.Y1 = HandleStart;
            lineHandle.X2 = HandleEnd;
            lineHandle.Y2 = HandleEnd;
            LinearGradientBrush lgb1 = new LinearGradientBrush();
            lgb1.StartPoint = new Point(0.78786, 1);
            lgb1.EndPoint = new Point(1, 0.78786);
            lgb1.GradientStops.Add(new GradientStop(Colors.DarkGreen, 0));
            lgb1.GradientStops.Add(new GradientStop(Colors.LightGreen, 0.9));
            lgb1.GradientStops.Add(new GradientStop(Colors.Green, 1));
            lineHandle.Stroke = lgb1;
            this.Children.Add(lineHandle);

            // Create the lens of the magnifying glass
            this.lensCanvas = new Canvas();
            this.Children.Add(this.lensCanvas);

            // The lens will contain a white backgound
            Ellipse ellipseWhite = new Ellipse();
            ellipseWhite.Width = LensDiameter;
            ellipseWhite.Height = LensDiameter;
            ellipseWhite.Fill = Brushes.White;
            this.lensCanvas.Children.Add(ellipseWhite);

            this.magnifierEllipse = new Ellipse();
            this.magnifierEllipse.Width = LensDiameter;
            this.magnifierEllipse.Height = LensDiameter;
            this.magnifierEllipse.StrokeThickness = 3;

            // Fill the Ellipse
            VisualBrush vb = new VisualBrush();
            vb.ViewboxUnits = BrushMappingMode.Absolute;
            vb.Viewbox = new Rect(0, 0, 50, 50);
            vb.ViewportUnits = BrushMappingMode.RelativeToBoundingBox;
            vb.Viewport = new Rect(0, 0, 1, 1);
            this.magnifierEllipse.Fill = vb;

            // Outline the Ellipse
            LinearGradientBrush lgb2 = new LinearGradientBrush();
            lgb2.StartPoint = new Point(0, 0);
            lgb2.EndPoint = new Point(0, 1);
            ColorConverter cc = new ColorConverter();
            lgb2.GradientStops.Add(new GradientStop((Color)cc.ConvertFrom("#AAA"), 0));
            lgb2.GradientStops.Add(new GradientStop((Color)cc.ConvertFrom("#111"), 1));
            this.magnifierEllipse.Stroke = lgb2;
            this.lensCanvas.Children.Add(this.magnifierEllipse);

            Ellipse e3 = new Ellipse();
            Canvas.SetLeft(e3, 2);
            Canvas.SetTop(e3, 2);
            e3.StrokeThickness = 4;
            e3.Width = LensDiameter - 4;
            e3.Height = LensDiameter - 4;
            this.lensCanvas.Children.Add(e3);

            // The cross-hairs
            Line lineCrosshair1 = new Line();
            lineCrosshair1.StrokeThickness = .25;
            lineCrosshair1.X1 = 5;
            lineCrosshair1.Y1 = LensDiameter / 2;
            lineCrosshair1.X2 = LensDiameter - 5;
            lineCrosshair1.Y2 = LensDiameter / 2;
            lineCrosshair1.Stroke = Brushes.Black;
            lineCrosshair1.Opacity = 0.5;

            this.lensCanvas.Children.Add(lineCrosshair1);

            Line lineCrosshair2 = new Line();
            lineCrosshair2.StrokeThickness = .25;
            lineCrosshair2.X1 = LensDiameter / 2;
            lineCrosshair2.Y1 = 5;
            lineCrosshair2.X2 = LensDiameter / 2;
            lineCrosshair2.Y2 = LensDiameter - 5;
            lineCrosshair2.Stroke = Brushes.Black;
            lineCrosshair2.Opacity = 0.5;
            this.lensCanvas.Children.Add(lineCrosshair2);
        }

        public void Redraw(Point mousePoint, Point imageControlPoint, double actualWidth, double actualHeight, Canvas canvasToMagnify)
        {
            // Abort if we don't have an image to magnify
            if (canvasToMagnify == null)
            {
                return;
            }
            if (this.MarkableCanvasParent.ImageToMagnify.Source == null)
            {
                return;
            }
            this.notYetRedrawn = false;

            // Abort if the magnifying glass visiblity is not visible, as there is no point doing all this work
            if (this.Visibility != Visibility.Visible)
            {
                return;
            }

            // Given a mouse position over the displayed image, we need to know where the equivalent position is over the magnified image (which is a different size)
            // We do this by calculating the ratio of the point over the displayed image, and then using that to calculate the position over the cached image
            Point ratioImageControlPoint = Utilities.ConvertPointToRatio(imageControlPoint, actualWidth, actualHeight);
            Point imageUnalteredPoint = Utilities.ConvertRatioToPoint(ratioImageControlPoint, canvasToMagnify.Width, canvasToMagnify.Height);

            // Create an Visual brush from the unaltered image in the magnification canvas magCanvas, set its properties, and use it to fill the magnifying glass.
            VisualBrush vbrush = new VisualBrush(canvasToMagnify);
            vbrush.ViewboxUnits = BrushMappingMode.Absolute;
            vbrush.ViewportUnits = BrushMappingMode.RelativeToBoundingBox;
            vbrush.Viewport = new Rect(0, 0, 1, 1);

            // And now calculate the position and zoom of the viewbox within that brush
            double size = this.magnifierEllipse.Width;
            double xsize = size + 200;  // approximate bounding box, kinda hacky
            double ysize = size + 200;

            Rect viewBox = vbrush.Viewbox;
            viewBox.Width = this.ZoomValue;
            viewBox.Height = this.ZoomValue;

            double xoffset = viewBox.Width / 2.0;
            double yoffset = viewBox.Height / 2.0;
            viewBox.X = imageUnalteredPoint.X - xoffset;
            viewBox.Y = imageUnalteredPoint.Y - yoffset;
            vbrush.Viewbox = viewBox;

            // Finally, fill the magnifying glass with this brush
            this.magnifierEllipse.Fill = vbrush;

            // Now, we need to calculate where to put the magnifying glass, and whether we should rotate it 
            // The idea is that we will start rotating when the magnifying glass is near the top and the left of the display
            // The critical distance is size for the Y direction, and somewhat larger than size for the X direction (as we have to start
            // rotating earlier so it doesn't get clipped). xsize is somewhat arbitrary, i.e., determined by trial and error
            const double EdgeThreshold = 250; // the EDGE boundary where we should rotate the magnifying glass
            double new_angleMG = this.magnifyingGlassAngle;  // the new angle we need to rotate the magnifying glass to
            // positions of edges where we shold change the angle. 
            double left_edge = EdgeThreshold;
            double right_edge = this.MarkableCanvasParent.ImageToDisplay.ActualWidth - EdgeThreshold;
            double top_edge = EdgeThreshold;
            double bottom_edge = this.MarkableCanvasParent.ImageToDisplay.ActualHeight - EdgeThreshold;
            double canvasheight = this.MarkableCanvasParent.ImageToDisplay.ActualHeight;
            double canvaswidth = this.MarkableCanvasParent.ImageToDisplay.ActualWidth;

            // Specify the magnifying glass angle needed
            // In various cases, several angles can work
            // so choose a new angle whose difference from the existing angle  will cause the least amount of animation 
            // BUG: Could improve this. There are cases where it rotates to the  non-optimal angle, but couldn't figure out how to fix it.
            if ((mousePoint.X < left_edge) && (mousePoint.Y < top_edge))
            {
                new_angleMG = 180;        // upper left corner
            }
            else if ((mousePoint.X < left_edge) && (mousePoint.Y > bottom_edge))
            {
                new_angleMG = 90; // lower left corner
            }
            else if (mousePoint.X < left_edge)
            {
                new_angleMG = this.AdjustAngle(this.magnifyingGlassAngle, 90, 180);      // middle left edge
            }
            else if ((mousePoint.X > right_edge) && (mousePoint.Y < top_edge))
            {
                new_angleMG = 270;   // upper right corner
            }
            else if ((mousePoint.X > right_edge) && (mousePoint.Y > bottom_edge))
            {
                new_angleMG = 0;  // lower right corner
            }
            else if (mousePoint.X > right_edge)
            {
                new_angleMG = this.AdjustAngle(this.magnifyingGlassAngle, 270, 0);       // middle right edge
            }
            else if (mousePoint.Y < top_edge)
            {
                new_angleMG = this.AdjustAngle(this.magnifyingGlassAngle, 270, 180);     // top edge, middle
            }
            else if (mousePoint.Y > bottom_edge)
            {
                new_angleMG = this.AdjustAngle(this.magnifyingGlassAngle, 0, 90);       // bottom edge, middle
            }
            else
            {
                new_angleMG = this.magnifyingGlassAngle;                           // center; any angle will work
            }

            // If the angle has changed, animate the magnifying glass and its contained image to the new angle
            if (this.magnifyingGlassAngle != new_angleMG)
            {
                double new_angleLens;
                double uncorrected_new_angleLens;

                // Correct the rotation in those cases where it would turn the long way around. 
                // Note that the new lens angle correction is hard coded rather than calculated, as it works. 
                // Easier than it out :-) 
                uncorrected_new_angleLens = -new_angleMG;
                if (this.magnifyingGlassAngle == 270 && new_angleMG == 0)
                {
                    this.magnifyingGlassAngle = -90;
                    new_angleLens = -360; // -new_angleMG; // We subtract the rotation that the mag glass is rotating to counter that rotational effect
                }
                else if (this.magnifyingGlassAngle == 0 && new_angleMG == 270)
                {
                    this.magnifyingGlassAngle = 360;
                    new_angleLens = 90; // We subtract the rotation that the mag glass is rotating to counter that rotational effect
                }
                else
                {
                    new_angleLens = uncorrected_new_angleLens; // We subtract the rotation that the mag glass is rotating to counter that rotational effect
                }

                // The time of the animation
                Duration duration = new Duration(new TimeSpan(0, 0, 0, 0, 500)); // allow animations to take a 1/3 second

                // Rotate the lens within the magnifying glass
                RotateTransform rotateTransformLens = new RotateTransform(this.magnifyingGlassAngle, size / 2, size / 2);
                DoubleAnimation animLens = new DoubleAnimation(this.lensAngle, new_angleLens, duration);
                rotateTransformLens.BeginAnimation(RotateTransform.AngleProperty, animLens);
                this.lensCanvas.RenderTransform = rotateTransformLens;

                // Now rotate and position the entire mag. glass
                RotateTransform rotateTransformMG = new RotateTransform(this.magnifyingGlassAngle, size, size);
                DoubleAnimation animMG = new DoubleAnimation(this.magnifyingGlassAngle, new_angleMG, duration);
                rotateTransformMG.BeginAnimation(RotateTransform.AngleProperty, animMG);
                this.RenderTransform = rotateTransformMG;

                // Save the angle so we can compare it on the next iteration. If any of them are 360, swap it to 0
                if (new_angleMG % 360 == 0)
                {
                    new_angleMG = 0;
                }
                if (new_angleLens % 360 == 0)
                {
                    uncorrected_new_angleLens = 0;
                }
                this.magnifyingGlassAngle = new_angleMG;
                this.lensAngle = uncorrected_new_angleLens;
            }
            Canvas.SetLeft(this, mousePoint.X - size);
            Canvas.SetTop(this, mousePoint.Y - size);
        }

        // Given the old angle, and up to two desired angles,
        // return the  current angle if it matches one of the desired angle, 
        // or the the desired angle that is closest to the angle in degrees
        private double AdjustAngle(double old_angle, double angle1, double angle2)
        {
            double result;

            if (old_angle == angle2)
            {
                result = angle2;
            }
            else if (Math.Abs(old_angle - angle1) > 180)
            {
                result = angle2;
            }
            else
            {
                result = angle1;
            }

            return result;
        }

        // Hiding the magnifying glass does not affect its visibility state
        public void Hide()
        {
            this.Visibility = Visibility.Collapsed;
        }

        public void ShowIfIsVisibilityDesired()
        {
            if (this.IsVisibilityDesired)
            {
                // Note: a better way would be to invoke the redraw method, but generating the arguments for that is a pain.
                if (this.notYetRedrawn)
                {
                    // On startup, we don't want to show the magnifying glass until there has been at least one redraw pass in it to display its contents
                    return;
                }
                this.Visibility = Visibility.Visible;
            }
        }
    }
}
