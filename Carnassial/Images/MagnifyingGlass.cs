﻿using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Shapes;

namespace Carnassial.Images
{
    internal class MagnifyingGlass : Canvas
    {
        // current angle of the lens only
        private double lensAngle;
        private Canvas lensCanvas;

        private Ellipse magnifierLens;
        // current angle of the entire magnifying glass
        private double magnifyingGlassAngle;

        public new MarkableCanvas Parent { get; set; }
        public double Zoom { get; set; }

        public MagnifyingGlass(MarkableCanvas markableCanvas)
        {
            this.IsEnabled = false;
            this.IsHitTestVisible = false;
            this.HorizontalAlignment = HorizontalAlignment.Left;
            this.Parent = markableCanvas;
            this.VerticalAlignment = VerticalAlignment.Top;
            this.Visibility = Visibility.Collapsed;
            this.Zoom = Constant.MarkableCanvas.MagnifyingGlassDefaultZoom;

            this.lensAngle = 0;
            this.magnifyingGlassAngle = 0;

            // Create the handle of the magnifying glass
            Line handle = new Line();
            handle.StrokeThickness = 5;
            handle.X1 = Constant.MarkableCanvas.MagnifyingGlassHandleStart;
            handle.Y1 = Constant.MarkableCanvas.MagnifyingGlassHandleStart;
            handle.X2 = Constant.MarkableCanvas.MagnifyingGlassHandleEnd;
            handle.Y2 = Constant.MarkableCanvas.MagnifyingGlassHandleEnd;
            LinearGradientBrush handleBrush = new LinearGradientBrush();
            handleBrush.StartPoint = new Point(0.78786, 1);
            handleBrush.EndPoint = new Point(1, 0.78786);
            handleBrush.GradientStops.Add(new GradientStop(Colors.DarkGreen, 0));
            handleBrush.GradientStops.Add(new GradientStop(Colors.LightGreen, 0.9));
            handleBrush.GradientStops.Add(new GradientStop(Colors.Green, 1));
            handle.Stroke = handleBrush;
            this.Children.Add(handle);

            // Create the lens of the magnifying glass
            this.lensCanvas = new Canvas();
            this.Children.Add(this.lensCanvas);

            // lens has a white backgound
            Ellipse lensBackground = new Ellipse();
            lensBackground.Width = Constant.MarkableCanvas.MagnifyingGlassDiameter;
            lensBackground.Height = Constant.MarkableCanvas.MagnifyingGlassDiameter;
            lensBackground.Fill = Brushes.White;
            this.lensCanvas.Children.Add(lensBackground);

            this.magnifierLens = new Ellipse();
            this.magnifierLens.Width = Constant.MarkableCanvas.MagnifyingGlassDiameter;
            this.magnifierLens.Height = Constant.MarkableCanvas.MagnifyingGlassDiameter;
            this.magnifierLens.StrokeThickness = 3;

            // fill the lens
            VisualBrush lensFill = new VisualBrush();
            lensFill.ViewboxUnits = BrushMappingMode.Absolute;
            lensFill.Viewbox = new Rect(0, 0, 50, 50);
            lensFill.ViewportUnits = BrushMappingMode.RelativeToBoundingBox;
            lensFill.Viewport = new Rect(0, 0, 1, 1);
            this.magnifierLens.Fill = lensFill;

            // outline the lens
            LinearGradientBrush outlineBrush = new LinearGradientBrush();
            outlineBrush.StartPoint = new Point(0, 0);
            outlineBrush.EndPoint = new Point(0, 1);
            ColorConverter cc = new ColorConverter();
            outlineBrush.GradientStops.Add(new GradientStop((Color)cc.ConvertFrom("#AAA"), 0));
            outlineBrush.GradientStops.Add(new GradientStop((Color)cc.ConvertFrom("#111"), 1));
            this.magnifierLens.Stroke = outlineBrush;
            this.lensCanvas.Children.Add(this.magnifierLens);

            Ellipse lensImage = new Ellipse();
            Canvas.SetLeft(lensImage, 2);
            Canvas.SetTop(lensImage, 2);
            lensImage.StrokeThickness = 4;
            lensImage.Width = Constant.MarkableCanvas.MagnifyingGlassDiameter - 4;
            lensImage.Height = Constant.MarkableCanvas.MagnifyingGlassDiameter - 4;
            this.lensCanvas.Children.Add(lensImage);

            // crosshairs
            Line verticalCrosshair = new Line();
            verticalCrosshair.StrokeThickness = 0.25;
            verticalCrosshair.X1 = 5;
            verticalCrosshair.Y1 = Constant.MarkableCanvas.MagnifyingGlassDiameter / 2;
            verticalCrosshair.X2 = Constant.MarkableCanvas.MagnifyingGlassDiameter - 5;
            verticalCrosshair.Y2 = Constant.MarkableCanvas.MagnifyingGlassDiameter / 2;
            verticalCrosshair.Stroke = Brushes.Black;
            verticalCrosshair.Opacity = 0.5;
            this.lensCanvas.Children.Add(verticalCrosshair);

            Line horizontalCrosshair = new Line();
            horizontalCrosshair.StrokeThickness = 0.25;
            horizontalCrosshair.X1 = Constant.MarkableCanvas.MagnifyingGlassDiameter / 2;
            horizontalCrosshair.Y1 = 5;
            horizontalCrosshair.X2 = Constant.MarkableCanvas.MagnifyingGlassDiameter / 2;
            horizontalCrosshair.Y2 = Constant.MarkableCanvas.MagnifyingGlassDiameter - 5;
            horizontalCrosshair.Stroke = Brushes.Black;
            horizontalCrosshair.Opacity = 0.5;
            this.lensCanvas.Children.Add(horizontalCrosshair);
        }

        public void RedrawIfVisible(Point mouseLocation, Point imageControlPoint, double actualWidth, double actualHeight, Canvas canvasToMagnify)
        {
            // nothing to draw
            if ((this.IsEnabled == false) || (this.Visibility != Visibility.Visible) || (canvasToMagnify == null) || (this.Parent.ImageToMagnify.Source == null))
            {
                return;
            }

            // Given a mouse position over the displayed image, we need to know where the equivalent position is over the magnified image (which is a different size)
            // We do this by calculating the ratio of the point over the displayed image, and then using that to calculate the position over the cached image
            Point ratioImageControlPoint = Marker.ConvertPointToRatio(imageControlPoint, actualWidth, actualHeight);
            Point imageUnalteredPoint = Marker.ConvertRatioToPoint(ratioImageControlPoint, canvasToMagnify.Width, canvasToMagnify.Height);

            // Create an Visual brush from the unaltered image in the magnification canvas magCanvas, set its properties, and use it to fill the magnifying glass.
            VisualBrush imageBrush = new VisualBrush(canvasToMagnify);
            imageBrush.ViewboxUnits = BrushMappingMode.Absolute;
            imageBrush.ViewportUnits = BrushMappingMode.RelativeToBoundingBox;
            imageBrush.Viewport = new Rect(0, 0, 1, 1);

            // And now calculate the position and zoom of the viewbox within that brush
            double size = this.magnifierLens.Width;

            Rect viewBox = imageBrush.Viewbox;
            viewBox.Width = this.Zoom;
            viewBox.Height = this.Zoom;

            double xoffset = viewBox.Width / 2.0;
            double yoffset = viewBox.Height / 2.0;
            viewBox.X = imageUnalteredPoint.X - xoffset;
            viewBox.Y = imageUnalteredPoint.Y - yoffset;
            imageBrush.Viewbox = viewBox;

            // Finally, fill the magnifying glass with this brush
            this.magnifierLens.Fill = imageBrush;

            // figure out the magnifying glass angle needed
            // The idea is that we will start rotating when the magnifying glass is near the top and the left of the display
            // The critical distance is size for the Y direction, and somewhat larger than size for the X direction (as we have to start
            // rotating earlier so it doesn't get clipped). xsize is somewhat arbitrary, i.e., determined by trial and error
            const double EdgeThreshold = 250; // proximity to an edge where the magnifying glass change angles
            double newMagnifyingGlassAngle = this.magnifyingGlassAngle;  // the new angle to rotate the magnifying glass to
            // positions of edges where angle should change 
            double leftEdge = EdgeThreshold;
            double rightEdge = this.Parent.ImageToDisplay.ActualWidth - EdgeThreshold;
            double topEdge = EdgeThreshold;
            double bottomEdge = this.Parent.ImageToDisplay.ActualHeight - EdgeThreshold;
            double canvasHeight = this.Parent.ImageToDisplay.ActualHeight;
            double canvasWidth = this.Parent.ImageToDisplay.ActualWidth;

            // In various cases, several angles can work so choose a new angle whose difference from the existing angle will cause the least amount of animation 
            // BUG: Could improve this. There are cases where it rotates to a non-optimal angle.
            if ((mouseLocation.X < leftEdge) && (mouseLocation.Y < topEdge))
            {
                newMagnifyingGlassAngle = 180;        // upper left corner
            }
            else if ((mouseLocation.X < leftEdge) && (mouseLocation.Y > bottomEdge))
            {
                newMagnifyingGlassAngle = 90; // lower left corner
            }
            else if (mouseLocation.X < leftEdge)
            {
                newMagnifyingGlassAngle = this.AdjustAngle(this.magnifyingGlassAngle, 90, 180);      // middle left edge
            }
            else if ((mouseLocation.X > rightEdge) && (mouseLocation.Y < topEdge))
            {
                newMagnifyingGlassAngle = 270;   // upper right corner
            }
            else if ((mouseLocation.X > rightEdge) && (mouseLocation.Y > bottomEdge))
            {
                newMagnifyingGlassAngle = 0;  // lower right corner
            }
            else if (mouseLocation.X > rightEdge)
            {
                newMagnifyingGlassAngle = this.AdjustAngle(this.magnifyingGlassAngle, 270, 0);       // middle right edge
            }
            else if (mouseLocation.Y < topEdge)
            {
                newMagnifyingGlassAngle = this.AdjustAngle(this.magnifyingGlassAngle, 270, 180);     // top edge, middle
            }
            else if (mouseLocation.Y > bottomEdge)
            {
                newMagnifyingGlassAngle = this.AdjustAngle(this.magnifyingGlassAngle, 0, 90);       // bottom edge, middle
            }
            else
            {
                newMagnifyingGlassAngle = this.magnifyingGlassAngle;                           // center; any angle will work
            }

            // If the angle has changed, animate the magnifying glass and its contained image to the new angle
            if (this.magnifyingGlassAngle != newMagnifyingGlassAngle)
            {
                // Correct the rotation in those cases where it would turn the long way around. 
                // Note that the new lens angle correction is hard coded rather than calculated, as it works. 
                double newLensAngle;
                double uncorrectedNewLensAngle = -newMagnifyingGlassAngle;
                if (this.magnifyingGlassAngle == 270 && newMagnifyingGlassAngle == 0)
                {
                    this.magnifyingGlassAngle = -90;
                    newLensAngle = -360; // subtract the rotation of the magnifying glass to counter that rotational effect
                }
                else if (this.magnifyingGlassAngle == 0 && newMagnifyingGlassAngle == 270)
                {
                    this.magnifyingGlassAngle = 360;
                    newLensAngle = 90;
                }
                else
                {
                    newLensAngle = uncorrectedNewLensAngle;
                }

                // Rotate the lens within the magnifying glass
                Duration animationDuration = new Duration(new TimeSpan(0, 0, 0, 0, 500));
                DoubleAnimation lensAnimation = new DoubleAnimation(this.lensAngle, newLensAngle, animationDuration);
                RotateTransform rotateTransformLens = new RotateTransform(this.magnifyingGlassAngle, size / 2, size / 2);
                rotateTransformLens.BeginAnimation(RotateTransform.AngleProperty, lensAnimation);
                this.lensCanvas.RenderTransform = rotateTransformLens;

                // Now rotate and position the entire magnifying glass
                RotateTransform rotateTransformMagnifyingGlass = new RotateTransform(this.magnifyingGlassAngle, size, size);
                DoubleAnimation magnifyingGlassAnimation = new DoubleAnimation(this.magnifyingGlassAngle, newMagnifyingGlassAngle, animationDuration);
                rotateTransformMagnifyingGlass.BeginAnimation(RotateTransform.AngleProperty, magnifyingGlassAnimation);
                this.RenderTransform = rotateTransformMagnifyingGlass;

                // Save the angle so we can compare it on the next iteration. If any of them are 360, swap it to 0
                if (newMagnifyingGlassAngle % 360 == 0)
                {
                    newMagnifyingGlassAngle = 0;
                }
                if (newLensAngle % 360 == 0)
                {
                    uncorrectedNewLensAngle = 0;
                }
                this.magnifyingGlassAngle = newMagnifyingGlassAngle;
                this.lensAngle = uncorrectedNewLensAngle;
            }
            Canvas.SetLeft(this, mouseLocation.X - size);
            Canvas.SetTop(this, mouseLocation.Y - size);
        }

        // return the current angle if it matches one of the desired angle, or the the desired angle that is closest to the angle in degrees
        private double AdjustAngle(double currentAngle, double angle1, double angle2)
        {
            if (currentAngle == angle2)
            {
                return angle2;
            }
            else if (Math.Abs(currentAngle - angle1) > 180)
            {
                return angle2;
            }
            return angle1;
        }

        public void Hide()
        {
            this.Visibility = Visibility.Collapsed;
        }
    }
}
