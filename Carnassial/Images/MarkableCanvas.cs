﻿using Carnassial.Controls;
using Carnassial.Util;
using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;

namespace Carnassial.Images
{
    /// <summary>
    /// MarkableCanvas is a canvas which
    /// - contains an image that can be zoomed and panned by the user with the mouse
    /// - can draw and track markers atop the image
    /// - can show a magnified portion of the image in a magnifying glass
    /// - can save and restore a zoom+pan setting
    /// </summary>
    public class MarkableCanvas : Canvas
    {
        private static readonly SolidColorBrush MarkerFillBrush = new SolidColorBrush(Color.FromArgb(2, 0, 0, 0));

        // bookmarked pan and zoom setting
        private Point bookmarkScale;
        private Point bookmarkTranslation;

        // the canvas to magnify contains both an image and markers so the magnifying glass view matches the display image
        private Canvas canvasToMagnify;

        private MagnifyingGlass magnifyingGlass;
        // increment for increasing or decreasing magnifying glass zoom
        private double magnifyingGlassZoomStep;

        private List<Marker> markers;

        // mouse and position states used to discriminate clicks from drags
        private UIElement mouseDownSender;
        private DateTime mouseDownTime;
        private Point mouseDownLocation;
        private Point previousMousePosition;

        // render transforms
        private ScaleTransform scaleTransform;
        private TransformGroup transformGroup;
        private TranslateTransform translateTransform;

        /// <summary>
        /// Gets the image displayed across the MarkableCanvas for image files
        /// </summary>
        public Image ImageToDisplay { get; private set; }

        /// <summary>
        /// Gets the image displayed in the magnifying glass
        /// </summary>
        public Image ImageToMagnify { get; private set; }

        /// <summary>
        /// Gets the video displayed across the MarkableCanvas for video files
        /// </summary>
        public VideoPlayer VideoToDisplay { get; private set; }

        /// <summary>
        /// Gets or sets the markers on the image
        /// </summary>
        public List<Marker> Markers
        {
            get
            {
                return this.markers;
            }
            set
            {
                this.markers = value;
                this.RedrawMarkers();
            }
        }

        /// <summary>
        /// Gets or sets the maximum zoom of the display image
        /// </summary>
        public double ZoomMaximum { get; set; }

        /// <summary>
        /// Gets or sets the amount we should zoom (scale) the image in the magnifying glass
        /// </summary>
        private double MagnifyingGlassZoom
        {
            get
            {
                return this.magnifyingGlass.Zoom;
            }
            set
            {
                // clamp the value
                if (value < Constant.MarkableCanvas.MagnifyingGlassMinimumZoom)
                {
                    value = Constant.MarkableCanvas.MagnifyingGlassMinimumZoom;
                }
                else if (value > Constant.MarkableCanvas.MagnifyingGlassMaximumZoom)
                {
                    value = Constant.MarkableCanvas.MagnifyingGlassMaximumZoom;
                }
                this.magnifyingGlass.Zoom = value;

                // update magnifier content if there is something to magnify
                if (this.ImageToMagnify.Source != null && this.ImageToDisplay.ActualWidth > 0)
                {
                    this.RedrawMagnifyingGlassIfVisible();
                }
            }
        }

        /// <summary>
        /// Gets or sets a value indicating whether the magnifying glass is generally visible or hidden, and returns its state
        /// </summary>
        public bool MagnifyingGlassEnabled
        {
            get
            {
                return this.magnifyingGlass.IsEnabled;
            }
            set
            {
                this.magnifyingGlass.IsEnabled = value;
                if (value)
                {
                    this.magnifyingGlass.Visibility = Visibility.Visible;
                }
                else
                {
                    this.magnifyingGlass.Hide();
                }
            }
        }

        public event EventHandler<MarkerEventArgs> MarkerEvent;

        private void SendMarkerEvent(MarkerEventArgs e)
        {
            if (this.MarkerEvent != null)
            {
                this.MarkerEvent(this, e);
            }
        }

        public MarkableCanvas()
        {
            // configure self
            this.Background = Brushes.Black;
            this.ClipToBounds = true;
            this.Focusable = true;
            this.ResetMaximumZoom();
            this.SizeChanged += this.MarkableImageCanvas_SizeChanged;

            this.markers = new List<Marker>();

            // initialize render transforms
            // scale transform's center is set during layout once the image size is known
            // default bookmark is default zoomed out, normal pan state
            this.bookmarkScale = new Point();
            this.bookmarkTranslation = new Point();
            this.ResetBookmark();

            this.scaleTransform = new ScaleTransform(1.0, 1.0);
            this.translateTransform = new TranslateTransform(0.0, 0.0);

            this.transformGroup = new TransformGroup();
            this.transformGroup.Children.Add(this.scaleTransform);
            this.transformGroup.Children.Add(this.translateTransform);

            // set up display image
            this.ImageToDisplay = new Image();
            this.ImageToDisplay.HorizontalAlignment = HorizontalAlignment.Left;
            this.ImageToDisplay.MouseDown += this.ImageOrCanvas_MouseDown;
            this.ImageToDisplay.MouseUp += this.ImageOrCanvas_MouseUp;
            this.ImageToDisplay.MouseWheel += this.ImageOrCanvas_MouseWheel;
            this.ImageToDisplay.RenderTransform = this.transformGroup;
            this.ImageToDisplay.SizeChanged += this.ImageToDisplay_SizeChanged;
            this.ImageToDisplay.VerticalAlignment = VerticalAlignment.Top;
            Canvas.SetLeft(this.ImageToDisplay, 0);
            Canvas.SetTop(this.ImageToDisplay, 0);
            this.Children.Add(this.ImageToDisplay);

            // set up display video
            this.VideoToDisplay = new VideoPlayer();
            this.VideoToDisplay.HorizontalAlignment = HorizontalAlignment.Left;
            this.VideoToDisplay.SizeChanged += this.VideoToDisplay_SizeChanged;
            this.VideoToDisplay.VerticalAlignment = VerticalAlignment.Top;
            Canvas.SetLeft(this.VideoToDisplay, 0);
            Canvas.SetTop(this.VideoToDisplay, 0);
            this.Children.Add(this.VideoToDisplay);

            // set up image to magnify
            this.ImageToMagnify = new Image();
            this.ImageToMagnify.HorizontalAlignment = HorizontalAlignment.Left;
            this.ImageToMagnify.VerticalAlignment = VerticalAlignment.Top;
            Canvas.SetLeft(this.ImageToMagnify, 0);
            Canvas.SetTop(this.ImageToMagnify, 0);

            this.canvasToMagnify = new Canvas();
            this.canvasToMagnify.Children.Add(this.ImageToMagnify);

            // set up the magnifying glass
            this.magnifyingGlass = new MagnifyingGlass(this);
            this.magnifyingGlassZoomStep = Constant.MarkableCanvas.MagnifyingGlassZoomIncrement;

            Canvas.SetZIndex(this.magnifyingGlass, 1000); // Should always be in front
            this.Children.Add(this.magnifyingGlass);

            // event handlers for image interaction: keys, mouse handling for markers
            this.MouseLeave += this.ImageOrCanvas_MouseLeave;
            this.MouseMove += this.MarkableCanvas_MouseMove;
            this.PreviewKeyDown += this.MarkableCanvas_PreviewKeyDown;
        }

        // Return to the zoom / pan levels saved as a bookmark
        public void ApplyBookmark()
        {
            this.scaleTransform.ScaleX = this.bookmarkScale.X;
            this.scaleTransform.ScaleY = this.bookmarkScale.Y;
            this.translateTransform.X = this.bookmarkTranslation.X;
            this.translateTransform.Y = this.bookmarkTranslation.Y;
            this.RedrawMarkers();
        }

        private Canvas DrawMarker(Marker marker, Size canvasRenderSize, bool doTransform)
        {
            Canvas markerCanvas = new Canvas();
            markerCanvas.MouseRightButtonUp += new MouseButtonEventHandler(this.Marker_MouseRightButtonUp);
            markerCanvas.MouseWheel += new MouseWheelEventHandler(this.ImageOrCanvas_MouseWheel); // Make the mouse wheel work over marks as well as the image

            if (marker.Tooltip.Trim() == String.Empty)
            {
                markerCanvas.ToolTip = null;
            }
            else
            {
                markerCanvas.ToolTip = marker.Tooltip;
            }
            markerCanvas.Tag = marker;

            // Create a marker
            Ellipse mark = new Ellipse();
            mark.Width = Constant.MarkableCanvas.MarkerDiameter;
            mark.Height = Constant.MarkableCanvas.MarkerDiameter;
            mark.Stroke = marker.Brush;
            mark.StrokeThickness = Constant.MarkableCanvas.MarkerStrokeThickness;
            mark.Fill = MarkableCanvas.MarkerFillBrush;
            markerCanvas.Children.Add(mark);

            // Draw another Ellipse as a black outline around it
            Ellipse blackOutline = new Ellipse();
            blackOutline.Stroke = Brushes.Black;
            blackOutline.Width = mark.Width + 1;
            blackOutline.Height = mark.Height + 1;
            blackOutline.StrokeThickness = 1;
            markerCanvas.Children.Add(blackOutline);

            // And another Ellipse as a white outline around it
            Ellipse whiteOutline = new Ellipse();
            whiteOutline.Stroke = Brushes.White;
            whiteOutline.Width = blackOutline.Width + 1;
            whiteOutline.Height = blackOutline.Height + 1;
            whiteOutline.StrokeThickness = 1;
            markerCanvas.Children.Add(whiteOutline);

            // maybe add emphasis
            double outerDiameter = whiteOutline.Width;
            Ellipse glow = null;
            if (marker.Emphasise)
            {
                glow = new Ellipse();
                glow.Width = whiteOutline.Width + Constant.MarkableCanvas.MarkerGlowDiameterIncrease;
                glow.Height = whiteOutline.Height + Constant.MarkableCanvas.MarkerGlowDiameterIncrease;
                glow.StrokeThickness = Constant.MarkableCanvas.MarkerGlowStrokeThickness;
                glow.Stroke = mark.Stroke;
                glow.Opacity = Constant.MarkableCanvas.MarkerGlowOpacity;
                markerCanvas.Children.Add(glow);

                outerDiameter = glow.Width;
            }

            markerCanvas.Width = outerDiameter;
            markerCanvas.Height = outerDiameter;

            double position = (markerCanvas.Width - mark.Width) / 2.0;
            Canvas.SetLeft(mark, position);
            Canvas.SetTop(mark, position);

            position = (markerCanvas.Width - blackOutline.Width) / 2.0;
            Canvas.SetLeft(blackOutline, position);
            Canvas.SetTop(blackOutline, position);

            position = (markerCanvas.Width - whiteOutline.Width) / 2.0;
            Canvas.SetLeft(whiteOutline, position);
            Canvas.SetTop(whiteOutline, position);

            if (marker.Emphasise)
            {
                position = (markerCanvas.Width - glow.Width) / 2.0;
                Canvas.SetLeft(glow, position);
                Canvas.SetTop(glow, position);
            }

            if (marker.Annotate)
            {
                Label label = new Label();
                label.Content = marker.Tooltip;
                label.Opacity = 0.6;
                label.Background = Brushes.White;
                label.Padding = new Thickness(0, 0, 0, 0);
                label.Margin = new Thickness(0, 0, 0, 0);
                markerCanvas.Children.Add(label);

                position = (markerCanvas.Width / 2.0) + (whiteOutline.Width / 2.0);
                Canvas.SetLeft(label, position);
                Canvas.SetTop(label, markerCanvas.Height / 2);
            }

            // Get the point from the marker, and convert it so that the marker will be in the right place
            Point screenPosition = Marker.ConvertRatioToPoint(marker.Position, canvasRenderSize.Width, canvasRenderSize.Height);
            if (doTransform)
            {
                screenPosition = this.transformGroup.Transform(screenPosition);
            }

            Canvas.SetLeft(markerCanvas, screenPosition.X - markerCanvas.Width / 2.0);
            Canvas.SetTop(markerCanvas, screenPosition.Y - markerCanvas.Height / 2.0);
            Canvas.SetZIndex(markerCanvas, 0);
            markerCanvas.MouseDown += this.ImageOrCanvas_MouseDown;
            markerCanvas.MouseMove += this.MarkableCanvas_MouseMove;
            markerCanvas.MouseUp += this.ImageOrCanvas_MouseUp;
            return markerCanvas;
        }

        private void DrawMarkers(Canvas canvas, Size canvasRenderSize, bool doTransform)
        {
            if (this.Markers != null)
            {
                foreach (Marker marker in this.Markers)
                {
                    Canvas markerCanvas = this.DrawMarker(marker, canvasRenderSize, doTransform);
                    canvas.Children.Add(markerCanvas);
                }
            }
        }

        // Whenever the image size changes, refresh the markers so they appear in the correct place
        private void ImageToDisplay_SizeChanged(object sender, SizeChangedEventArgs e)
        {
            this.RedrawMarkers();
        }

        // On Mouse down, record the location, who sent it, and the time.
        // We will use this information on move and up events to discriminate between 
        // panning/zooming vs. marking. 
        private void ImageOrCanvas_MouseDown(object sender, MouseButtonEventArgs e)
        {
            this.previousMousePosition = e.GetPosition(this);
            if (e.LeftButton == MouseButtonState.Pressed)
            {
                this.mouseDownLocation = e.GetPosition(this.ImageToDisplay);
                this.mouseDownSender = (UIElement)sender;
                this.mouseDownTime = DateTime.Now;
            }
        }

        // Hide the magnifying glass when the mouse cursor leaves the image
        private void ImageOrCanvas_MouseLeave(object sender, MouseEventArgs e)
        {
            this.magnifyingGlass.Hide();
        }

        private void ImageOrCanvas_MouseUp(object sender, MouseButtonEventArgs e)
        {
            // Make sure the cursor reverts to the normal arrow cursor
            this.Cursor = Cursors.Arrow;

            // Get the current position
            Point mouseLocation = e.GetPosition(this.ImageToDisplay);

            // Is this the end of a translate operation, or a marking operation?
            // We decide by checking if the left button has been released, the mouse location is
            // smaller than a given threshold, and less than 200 ms have passed since the original
            // mouse down. i.e., the use has done a rapid click and release on a small location
            if ((e.LeftButton == MouseButtonState.Released) &&
                (sender == this.mouseDownSender) &&
                (this.mouseDownLocation - mouseLocation).Length <= 2.0)
            {
                TimeSpan timeSinceDown = DateTime.Now - this.mouseDownTime;
                if (timeSinceDown.TotalMilliseconds < 200)
                {
                    // Get the current point, and create a marker on it.
                    Point position = e.GetPosition(this.ImageToDisplay);
                    position = Marker.ConvertPointToRatio(position, this.ImageToDisplay.ActualWidth, this.ImageToDisplay.ActualHeight);
                    Marker marker = new Marker(null, position);

                    // don't add marker to the marker list
                    // Main window is responsible for filling in remaining properties and adding it.
                    this.SendMarkerEvent(new MarkerEventArgs(marker, true));
                }
            }

            this.RedrawMagnifyingGlassIfVisible();
        }

        // Use the  mouse wheel to scale the image
        private void ImageOrCanvas_MouseWheel(object sender, MouseWheelEventArgs e)
        {
            lock (this)
            {
                // We will scale around the current point
                Point mousePosition = e.GetPosition(this.ImageToDisplay);
                bool zoomIn = e.Delta > 0; // Zooming in if delta is positive, else zooming out
                this.ScaleImage(mousePosition, zoomIn);
            }
        }

        /// <summary>
        /// Zoom in the magnifying glass image  by the amount defined by the property MagnifierZoomDelta
        /// </summary>
        public void MagnifierZoomIn()
        {
            this.MagnifyingGlassZoom -= this.magnifyingGlassZoomStep;
        }

        /// <summary>
        /// Zoom out the magnifying glass image  by the amount defined by the property MagnifierZoomDelta
        /// </summary>
        public void MagnifierZoomOut()
        {
            this.MagnifyingGlassZoom += this.magnifyingGlassZoomStep;
        }

        // If we move the mouse with the left mouse button press, translate the image
        private void MarkableCanvas_MouseMove(object sender, MouseEventArgs e)
        {
            // The visibility of the magnifying glass depends on whether the mouse is over the image
            // The magnifying glass is visible only if the current mouse position is over the image. 
            // The the actual (transformed) bounds of the image
            Point mousePosition = e.GetPosition(this);
            if (this.magnifyingGlass.IsEnabled)
            {
                Point transformedSize = this.transformGroup.Transform(new Point(this.ImageToDisplay.ActualWidth, this.ImageToDisplay.ActualHeight));
                bool mouseOverImage = (mousePosition.X <= transformedSize.X) && (mousePosition.Y <= transformedSize.Y);
                if (mouseOverImage)
                {
                    this.magnifyingGlass.Visibility = Visibility.Visible;
                }
                else
                {
                    this.magnifyingGlass.Hide();
                }
            }

            // Calculate how much time has passed since the mouse down event?
            TimeSpan timeSinceDown = DateTime.Now - this.mouseDownTime;

            // If at least WAIT_TIME milliseconds has passed
            if (timeSinceDown >= TimeSpan.FromMilliseconds(100))
            {
                // If the left button is pressed, translate (pan) across the image 
                //  Also hide the magnifying glass so it won't be distracting
                if (e.LeftButton == MouseButtonState.Pressed)
                {
                    this.Cursor = Cursors.ScrollAll;    // Change the cursor to a panning cursor
                    this.TranslateImage(mousePosition);
                    this.magnifyingGlass.Hide();
                }
                else
                {
                    this.canvasToMagnify.Width = this.ImageToMagnify.ActualWidth;      // Make sure that the canvas is the same size as the image
                    this.canvasToMagnify.Height = this.ImageToMagnify.ActualHeight;

                    // update the magnifying glass
                    this.RedrawMagnifyingGlassIfVisible();
                }

                this.previousMousePosition = mousePosition;
            }
        }

        // if it's < or > key zoom out or in around the mouse point
        [SuppressMessage("StyleCop.CSharp.ReadabilityRules", "SA1126:PrefixCallsCorrectly", Justification = "StyleCop bug.")]
        private void MarkableCanvas_PreviewKeyDown(object sender, KeyEventArgs e)
        {
            switch (e.Key)
            {
                // zoom in
                case Key.OemPeriod:
                    Rect imageToDisplayBounds = new Rect(0.0, 0.0, this.ImageToDisplay.ActualWidth, this.ImageToDisplay.ActualHeight);
                    Point mousePosition = Mouse.GetPosition(this.ImageToDisplay);
                    if (imageToDisplayBounds.Contains(mousePosition) == false)
                    {
                        break; // ignore if mouse is not on the image
                    }
                    this.ScaleImage(mousePosition, true);
                    break;
                // zoom out
                case Key.OemComma:
                    mousePosition = Mouse.GetPosition(this.ImageToDisplay);
                    this.ScaleImage(mousePosition, false);
                    break;
                // if the current file's a video allow the user to hit the space bar to start or stop playing the video
                case Key.Space:
                    // This is desirable as the play or pause button doesn't necessarily have focus and it saves the user having to click the button with
                    // the mouse.
                    if (this.VideoToDisplay.TryPlayOrPause() == false)
                    {
                        return;
                    }
                    break;
                default:
                    return;
            }

            e.Handled = true;
        }

        // resize content and update transforms when canvas size changes
        private void MarkableImageCanvas_SizeChanged(object sender, SizeChangedEventArgs e)
        {
            this.ImageToDisplay.Width = this.ActualWidth;
            this.ImageToDisplay.Height = this.ActualHeight;

            this.VideoToDisplay.Width = this.ActualWidth;
            this.VideoToDisplay.Height = this.ActualHeight;

            this.scaleTransform.CenterX = 0.5 * this.ActualWidth;
            this.scaleTransform.CenterY = 0.5 * this.ActualHeight;

            // clear the bookmark (if any) as it will no longer be correct
            // if needed, the bookmark could be rescaled instead
            this.ResetBookmark();
        }

        // Remove a marker on a right mouse button up event
        private void Marker_MouseRightButtonUp(object sender, MouseButtonEventArgs e)
        {
            Canvas canvas = (Canvas)sender;
            Marker marker = (Marker)canvas.Tag;
            this.Markers.Remove(marker);
            this.SendMarkerEvent(new MarkerEventArgs(marker, false));
            this.RedrawMarkers();
        }

        private void RedrawMagnifyingGlassIfVisible()
        {
            this.magnifyingGlass.RedrawIfVisible(NativeMethods.GetCursorPos(this),
                                                 NativeMethods.GetCursorPos(this.ImageToDisplay),
                                                 this.ImageToDisplay.ActualWidth,
                                                 this.ImageToDisplay.ActualHeight,
                                                 this.canvasToMagnify);
        }

        /// <summary>
        /// Remove all and then draw all the markers
        /// </summary>
        private void RedrawMarkers()
        {
            this.RemoveMarkers(this);
            this.RemoveMarkers(this.canvasToMagnify);
            if (this.ImageToDisplay != null)
            {
                this.DrawMarkers(this, this.ImageToDisplay.RenderSize, true);
                this.DrawMarkers(this.canvasToMagnify, this.canvasToMagnify.RenderSize, false);
            }
        }

        // remove all markers from the canvas
        private void RemoveMarkers(Canvas canvas)
        {
            for (int index = canvas.Children.Count - 1; index >= 0; index--)
            {
                if (canvas.Children[index] is Canvas && canvas.Children[index] != this.magnifyingGlass)
                {
                    canvas.Children.RemoveAt(index);
                }
            }
        }

        // Clear the current zoom / pan levels as a bookmark, where its set to the zoomed out levels
        private void ResetBookmark()
        {
            this.bookmarkTranslation.X = 0;
            this.bookmarkTranslation.Y = 0;
            this.bookmarkScale.X = 1;
            this.bookmarkScale.Y = 1;
        }

        public void ResetMaximumZoom()
        {
            this.ZoomMaximum = Constant.MarkableCanvas.ZoomMaximum;
        }

        // Scale the image around the given image location point, where we are zooming in if
        // zoomIn is true, and zooming out if zoomIn is false
        public void ScaleImage(Point mousePosition, bool zoomIn)
        {
            // nothing to do if at maximum or minimum scaling value whilst zooming in or out, respectively 
            if ((zoomIn && this.scaleTransform.ScaleX >= this.ZoomMaximum) ||
                (!zoomIn && this.scaleTransform.ScaleX <= Constant.MarkableCanvas.ZoomMinimum))
            {
                return;
            }

            lock (this.ImageToDisplay)
            {
                // update scaling factor, keeping within maximum and minimum bounds
                if (zoomIn)
                {
                    this.scaleTransform.ScaleX *= Constant.MarkableCanvas.MagnifyingGlassZoomIncrement;
                    this.scaleTransform.ScaleX = Math.Min(this.ZoomMaximum, this.scaleTransform.ScaleX);
                }
                else
                {
                    this.scaleTransform.ScaleX /= Constant.MarkableCanvas.MagnifyingGlassZoomIncrement;
                    this.scaleTransform.ScaleX = Math.Max(Constant.MarkableCanvas.ZoomMinimum, this.scaleTransform.ScaleX);
                }
                this.scaleTransform.ScaleY = this.scaleTransform.ScaleX;

                if (this.scaleTransform.ScaleX <= Constant.MarkableCanvas.ZoomMinimum)
                {
                    // no translation needed if no scaling
                    this.translateTransform.X = 0.0;
                    this.translateTransform.Y = 0.0;
                }
                else
                {
                    // update translation so zoom is centered about the point in the image under the cursor, clamping so that the display image
                    // continues to contact its original border on the relevant side(s)
                    // This is imperfect as, if the display image doesn't entirely fill the available area (there's some of the black backround around it),
                    // the available background space goes unused.  Additional logic to detect and use this space is desirable, though not currently trivial
                    // as the markable canvas's size has to change.
                    // Scale transform is centered at the center of the image so translation is also calculated relative to the image center.
                    Point imageCenter = new Point(this.ImageToDisplay.ActualWidth / 2.0, this.ImageToDisplay.ActualHeight / 2.0);
                    Point maximumTranslation = new Point(0.5 * this.scaleTransform.ScaleX * this.ImageToDisplay.ActualWidth - imageCenter.X, 0.5 * this.scaleTransform.ScaleY * this.ImageToDisplay.ActualHeight - imageCenter.Y);
                    Vector unconstrainedTranslation = imageCenter - mousePosition;
                    this.translateTransform.X = Math.Max(-maximumTranslation.X, Math.Min(unconstrainedTranslation.X, maximumTranslation.X));
                    this.translateTransform.Y = Math.Max(-maximumTranslation.Y, Math.Min(unconstrainedTranslation.Y, maximumTranslation.Y));
                }

                this.RedrawMarkers();
            }
        }

        // Save the current zoom / pan levels as a bookmark
        public void SetBookmark()
        {
            // a user may want to flip between completely zoomed out / normal pan settings and a saved zoom / pan setting that focuses in on a particular region
            // To do this, we save / restore the zoom pan settings of a particular view, or return to the default zoom/pan.
            this.bookmarkTranslation.X = this.translateTransform.X;
            this.bookmarkTranslation.Y = this.translateTransform.Y;
            this.bookmarkScale.X = this.scaleTransform.ScaleX;
            this.bookmarkScale.Y = this.scaleTransform.ScaleY;
        }

        /// <summary>
        /// Sets only the display image and leaves markers and the magnifier image unchanged.  Used by the differencing routines to set the difference image.
        /// </summary>
        public void SetDisplayImage(BitmapSource bitmapSource)
        {
            this.ImageToDisplay.Source = bitmapSource;
        }

        /// <summary>
        /// Set a wholly new image.  Clears existing markers and syncs the magnifier image to the display image.
        /// </summary>
        public void SetNewImage(BitmapSource bitmapSource)
        {
            this.Markers = null;
            this.RedrawMarkers();

            this.ImageToDisplay.Source = bitmapSource;
            this.ImageToMagnify.Source = bitmapSource;

            this.ImageToDisplay.Visibility = Visibility.Visible;
            this.VideoToDisplay.Reset();
            this.VideoToDisplay.Visibility = Visibility.Collapsed;
        }

        public void SetNewVideo(FileInfo videoFile)
        {
            if (videoFile.Exists == false)
            {
                this.SetNewImage(Constant.Images.FileNoLongerAvailable);
                return;
            }

            this.VideoToDisplay.SetSource(new Uri(videoFile.FullName));

            this.ImageToDisplay.Visibility = Visibility.Collapsed;
            this.VideoToDisplay.Visibility = Visibility.Visible;
        }

        // Given the mouse location on the image, translate the image
        // This is normally called from a left mouse move event
        private void TranslateImage(Point mousePosition)
        {
            // Get the center point on the image
            Point center = this.PointFromScreen(this.ImageToDisplay.PointToScreen(new Point(this.ImageToDisplay.Width / 2.0, this.ImageToDisplay.Height / 2.0)));

            // Calculate the delta position from the last location relative to the center
            double newX = center.X + mousePosition.X - this.previousMousePosition.X;
            double newY = center.Y + mousePosition.Y - this.previousMousePosition.Y;

            // get the translated image width
            double imageWidth = this.ImageToDisplay.Width * this.scaleTransform.ScaleX;
            double imageHeight = this.ImageToDisplay.Height * this.scaleTransform.ScaleY;

            // Limit the delta position so that the image stays on the screen
            if (newX - imageWidth / 2.0 >= 0.0)
            {
                newX = imageWidth / 2.0;
            }
            else if (newX + imageWidth / 2.0 <= this.ActualWidth)
            {
                newX = this.ActualWidth - imageWidth / 2.0;
            }

            if (newY - imageHeight / 2.0 >= 0.0)
            {
                newY = imageHeight / 2.0;
            }
            else if (newY + imageHeight / 2.0 <= this.ActualHeight)
            {
                newY = this.ActualHeight - imageHeight / 2.0;
            }

            // Translate the canvas and redraw the markers
            this.translateTransform.X += newX - center.X;
            this.translateTransform.Y += newY - center.Y;

            this.RedrawMarkers();
        }

        // Whenever the image size changes, refresh the markers so they appear in the correct place
        private void VideoToDisplay_SizeChanged(object sender, SizeChangedEventArgs e)
        {
            this.RedrawMarkers();
        }

        // Return to the zoomed out level, with no panning
        public void ZoomOutAllTheWay()
        {
            this.scaleTransform.ScaleX = 1;
            this.scaleTransform.ScaleY = 1;
            this.translateTransform.X = 0;
            this.translateTransform.Y = 0;
            this.RedrawMarkers();
        }
    }
}
