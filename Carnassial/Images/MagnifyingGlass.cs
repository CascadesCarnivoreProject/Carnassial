using Carnassial.Control;
using Carnassial.Data;
using System;
using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
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

        private FileDisplayWithMarkers parent;

        private RotateTransform rotation;
        private TranslateTransform translation;

        /// <summary>Gets or sets the diameter of the image shown in the magnifying glass's lens in pixels.</summary>
        /// <remarks>
        /// In a screen sense, the magnification is the lens diameter divided by the field of view (currently 1 - 17x).  Relative to the display image it's
        /// usually several times higher.
        /// </remarks>
        public double FieldOfView { get; set; }

        public MagnifyingGlass(FileDisplayWithMarkers fileDisplay)
        {
            this.FieldOfView = Constant.MarkableCanvas.MagnifyingGlassDefaultFieldOfView;
            this.IsEnabled = false;
            this.IsHitTestVisible = false;
            this.HorizontalAlignment = HorizontalAlignment.Left;
            this.parent = fileDisplay;
            this.VerticalAlignment = VerticalAlignment.Top;
            this.Visibility = Visibility.Collapsed;

            this.lensAngle = 0.0;
            this.magnifyingGlassAngle = 0.0;

            LinearGradientBrush linearGradientBrush = new LinearGradientBrush()
            {
                StartPoint = new Point(0, 0),
                EndPoint = new Point(0, 1),
            };
            Color gray67percent = (Color)ColorConverter.ConvertFromString("#aaaaaa");
            linearGradientBrush.GradientStops.Add(new GradientStop(gray67percent, 0.0));
            Color gray07percent = (Color)ColorConverter.ConvertFromString("#111111");
            linearGradientBrush.GradientStops.Add(new GradientStop(gray07percent, 1.0));

            // create the handle of the magnifying glass
            // In this sense, the magnifying glass is a square area with the handle extending along the first quadrant's diagonal 
            // from the edge of the lens to the corner of the square. Its coordinates are therefore [ sqrt(2)/2 D, sqrt(2)/2 D, D, D]
            // where D is the magnifying glass's diameter. sqrt(2)/2 is rounded down to 0.7 below to ensure the inner end of the 
            // handle isn't visible.
            Line handle = new Line()
            {
                StrokeThickness = 5,
                X1 = 0.7 * Constant.MarkableCanvas.MagnifyingGlassDiameter,
                Y1 = 0.7 * Constant.MarkableCanvas.MagnifyingGlassDiameter,
                X2 = Constant.MarkableCanvas.MagnifyingGlassDiameter,
                Y2 = Constant.MarkableCanvas.MagnifyingGlassDiameter
            };
            handle.Stroke = linearGradientBrush;
            this.Children.Add(handle);

            // Create the lens of the magnifying glass
            this.lensCanvas = new Canvas();
            this.Children.Add(this.lensCanvas);

            // lens has a white backgound
            Ellipse lensBackground = new Ellipse()
            {
                Width = Constant.MarkableCanvas.MagnifyingGlassDiameter,
                Height = Constant.MarkableCanvas.MagnifyingGlassDiameter,
                Fill = Brushes.White
            };
            this.lensCanvas.Children.Add(lensBackground);

            this.magnifierLens = new Ellipse()
            {
                Width = Constant.MarkableCanvas.MagnifyingGlassDiameter,
                Height = Constant.MarkableCanvas.MagnifyingGlassDiameter,
                StrokeThickness = 3
            };

            // fill and outline the lens
            VisualBrush lensFill = new VisualBrush()
            {
                ViewboxUnits = BrushMappingMode.Absolute,
                Viewbox = new Rect(0, 0, 50, 50),
                ViewportUnits = BrushMappingMode.RelativeToBoundingBox,
                Viewport = new Rect(0, 0, 1, 1)
            };
            this.magnifierLens.Fill = lensFill;
            this.magnifierLens.Stroke = linearGradientBrush;
            this.lensCanvas.Children.Add(this.magnifierLens);

            Ellipse lensImage = new Ellipse();
            Canvas.SetLeft(lensImage, 2);
            Canvas.SetTop(lensImage, 2);
            lensImage.StrokeThickness = 4;
            lensImage.Width = Constant.MarkableCanvas.MagnifyingGlassDiameter - 4;
            lensImage.Height = Constant.MarkableCanvas.MagnifyingGlassDiameter - 4;
            this.lensCanvas.Children.Add(lensImage);

            // crosshairs
            Line verticalCrosshair = new Line()
            {
                StrokeThickness = 0.25,
                X1 = 5,
                Y1 = Constant.MarkableCanvas.MagnifyingGlassDiameter / 2,
                X2 = Constant.MarkableCanvas.MagnifyingGlassDiameter - 5,
                Y2 = Constant.MarkableCanvas.MagnifyingGlassDiameter / 2,
                Stroke = Brushes.Black,
                Opacity = 0.5
            };
            this.lensCanvas.Children.Add(verticalCrosshair);

            Line horizontalCrosshair = new Line()
            {
                StrokeThickness = 0.25,
                X1 = Constant.MarkableCanvas.MagnifyingGlassDiameter / 2,
                Y1 = 5,
                X2 = Constant.MarkableCanvas.MagnifyingGlassDiameter / 2,
                Y2 = Constant.MarkableCanvas.MagnifyingGlassDiameter - 5,
                Stroke = Brushes.Black,
                Opacity = 0.5
            };
            this.lensCanvas.Children.Add(horizontalCrosshair);

            // set render transform
            // Rotate the glass before translating it as that ordering means translation calculations don't have to account for rotation.  If this is changed
            // RedrawIfVisible() must be updated.
            this.rotation = new RotateTransform(this.magnifyingGlassAngle, Constant.MarkableCanvas.MagnifyingGlassDiameter, Constant.MarkableCanvas.MagnifyingGlassDiameter);
            this.translation = new TranslateTransform();

            TransformGroup transformGroup = new TransformGroup();
            transformGroup.Children.Add(this.rotation);
            transformGroup.Children.Add(this.translation);
            this.RenderTransform = transformGroup;
        }

        public void Hide()
        {
            this.Visibility = Visibility.Collapsed;
        }

        public void RedrawIfVisible(Canvas canvasToMagnify, Point newTranslation)
        {
            // not visible or nothing to draw
            if ((this.IsEnabled == false) || 
                (this.IsVisible == false) ||
                (this.Visibility != Visibility.Visible))
            {
                return;
            }

            // given the mouse position over the displayed image find the equivalent position in magnified image (which is a different size)
            Point mousePosition = Mouse.GetPosition(this.parent.FileDisplay.Image);
            Point mouseLocationRatio = Marker.ConvertPointToRatio(mousePosition, this.parent.FileDisplay.Image.ActualWidth, this.parent.FileDisplay.Image.ActualHeight);
            Point magnifiedLocation = Marker.ConvertRatioToPoint(mouseLocationRatio, canvasToMagnify.Width, canvasToMagnify.Height);

            // create a brush from the unaltered image in the magnification canvas and use it to fill the magnifying glass
            VisualBrush magnifierBrush = new VisualBrush(canvasToMagnify)
            {
                ViewboxUnits = BrushMappingMode.Absolute,
                ViewportUnits = BrushMappingMode.RelativeToBoundingBox,
                Viewport = new Rect(0, 0, 1, 1),
                Viewbox = new Rect(magnifiedLocation.X - this.FieldOfView / 2.0, magnifiedLocation.Y - this.FieldOfView / 2.0, this.FieldOfView, this.FieldOfView)
            };
            this.magnifierLens.Fill = magnifierBrush;

            // figure out the magnifying glass angle needed
            // Often several angles work so choose a new angle whose difference from the existing angle will cause the least amount of animation 
            double leftEdge = Constant.MarkableCanvas.MagnifyingGlassDiameter;
            double rightEdge = this.parent.FileDisplay.Image.ActualWidth - Constant.MarkableCanvas.MagnifyingGlassDiameter;
            double topEdge = Constant.MarkableCanvas.MagnifyingGlassDiameter;
            double bottomEdge = this.parent.FileDisplay.Image.ActualHeight - Constant.MarkableCanvas.MagnifyingGlassDiameter;

            double newMagnifyingGlassAngle;
            if ((mousePosition.X < leftEdge) && (mousePosition.Y < topEdge))
            {
                newMagnifyingGlassAngle = 180.0;                                              // upper left corner
            }
            else if ((mousePosition.X < leftEdge) && (mousePosition.Y > bottomEdge))
            {
                newMagnifyingGlassAngle = 90.0;                                               // lower left corner
            }
            else if (mousePosition.X < leftEdge)
            {
                newMagnifyingGlassAngle = this.magnifyingGlassAngle == 180.0 ? 180.0 : 90.0;  // middle of left edge
            }
            else if ((mousePosition.X > rightEdge) && (mousePosition.Y < topEdge))
            {
                newMagnifyingGlassAngle = -90.0;                                              // upper right corner
            }
            else if ((mousePosition.X > rightEdge) && (mousePosition.Y > bottomEdge))
            {
                newMagnifyingGlassAngle = 0.0;                                                // lower right corner
            }
            else if (mousePosition.X > rightEdge)
            {
                newMagnifyingGlassAngle = this.magnifyingGlassAngle == 0.0 ? 0.0 : -90.0;     // middle of right edge
            }
            else if (mousePosition.Y < topEdge)
            {
                newMagnifyingGlassAngle = this.magnifyingGlassAngle == 180.0 ? 180.0 : -90.0; // middle of top edge
            }
            else if (mousePosition.Y > bottomEdge)
            {
                newMagnifyingGlassAngle = this.magnifyingGlassAngle == 90.0 ? 90.0 : 0.0;     // middle of bottom edge
            }
            else
            {
                newMagnifyingGlassAngle = this.magnifyingGlassAngle; // far enough from edges the magnifer can be displayed at any angle
            }

            // If the angle has changed, animate the magnifying glass and its contained image to the new angle
            if (this.magnifyingGlassAngle != newMagnifyingGlassAngle)
            {
                Debug.Assert((newMagnifyingGlassAngle == -90.0) || (newMagnifyingGlassAngle == 0.0) || (newMagnifyingGlassAngle == 90.0) || (newMagnifyingGlassAngle == 180.0), String.Format("Unexpected magnifying glass angle {0}.", newMagnifyingGlassAngle));

                // RotateTransform rotates between the start and stop angle specified in the DoubleAnimation passed to it.  The greater the rotation, the
                // more intrusive it is, so 270 degree rotations are converted to 90 degree rotations in the opposite direction.
                double endMangifyingGlassRotationAngle = newMagnifyingGlassAngle;
                if (this.magnifyingGlassAngle - endMangifyingGlassRotationAngle >= 270.0)
                {
                    endMangifyingGlassRotationAngle += 360.0;
                }
                else if (this.magnifyingGlassAngle - endMangifyingGlassRotationAngle <= -270.0)
                {
                    endMangifyingGlassRotationAngle -= 360.0;
                }

                double endLensAngle = -newMagnifyingGlassAngle;
                if (this.lensAngle - endLensAngle >= 270.0)
                {
                    endLensAngle += 360.0;
                }
                else if (this.lensAngle - endLensAngle <= -270.0)
                {
                    endLensAngle -= 360.0;
                }

                // rotate the entire magnifying glass to its new angle
                this.rotation.Angle = this.magnifyingGlassAngle;
                Duration animationDuration = new Duration(Constant.Images.MagnifierRotationTime);
                DoubleAnimation rotateAnimation = new DoubleAnimation(this.magnifyingGlassAngle, endMangifyingGlassRotationAngle, animationDuration);
                this.rotation.BeginAnimation(RotateTransform.AngleProperty, rotateAnimation);

                // rotate the lens within the magnifying glass to compensate for the magnifier's rotation
                DoubleAnimation lensAnimation = new DoubleAnimation(this.lensAngle, endLensAngle, animationDuration);
                RotateTransform rotateTransformLens = new RotateTransform(this.magnifyingGlassAngle, Constant.MarkableCanvas.MagnifyingGlassDiameter / 2.0, Constant.MarkableCanvas.MagnifyingGlassDiameter / 2.0);
                rotateTransformLens.BeginAnimation(RotateTransform.AngleProperty, lensAnimation);
                this.lensCanvas.RenderTransform = rotateTransformLens;

                // update state on the assumption the animation completes before the user moves the pointer far enough to trigger another rotation
                this.magnifyingGlassAngle = newMagnifyingGlassAngle;
                this.lensAngle = endLensAngle;
            }

            // set translation for current mouse location
            this.translation.X = newTranslation.X;
            this.translation.Y = newTranslation.Y;

            Canvas.SetLeft(this, mousePosition.X - Constant.MarkableCanvas.MagnifyingGlassDiameter);
            Canvas.SetTop(this, mousePosition.Y - Constant.MarkableCanvas.MagnifyingGlassDiameter);
        }

        public void Show()
        {
            this.Visibility = Visibility.Visible;
        }
    }
}
