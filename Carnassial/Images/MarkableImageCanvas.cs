﻿using Carnassial.Util;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Shapes;

namespace Carnassial.Images
{
    /// <summary>
    /// MarkableImageCanvas - A canvas that:
    /// - contains an image that can be scaled and translated by the user with the mouse
    /// - can draw and track marks atop the image
    /// </summary>
    public class MarkableImageCanvas : Canvas
    {
        private static readonly DependencyProperty IsMagnifyingGlassVisibleProperty = DependencyProperty.Register("IsMagnifyingGlassVisible", typeof(bool), typeof(MarkableImageCanvas));
        private static readonly DependencyProperty MagnifierZoomDeltaProperty = DependencyProperty.Register("MagnifierZoomDelta", typeof(double), typeof(MarkableImageCanvas));
        private static readonly DependencyProperty MagnifierZoomRangeProperty = DependencyProperty.Register("MagnifierZoomRange", typeof(Point), typeof(MarkableImageCanvas));
        private static readonly DependencyProperty MagnifierZoomProperty = DependencyProperty.Register("MagnifierZoom", typeof(double), typeof(MarkableImageCanvas));
        private static readonly DependencyProperty MaxZoomProperty = DependencyProperty.Register("MaxZoom", typeof(double), typeof(MarkableImageCanvas));
        private static readonly DependencyProperty MaxZoomUpperBoundProperty = DependencyProperty.Register("MaxZoomUpperBound", typeof(double), typeof(MarkableImageCanvas));
        private static readonly DependencyProperty TranslateXProperty = DependencyProperty.Register("TranslateX", typeof(double), typeof(MarkableImageCanvas));
        private static readonly DependencyProperty TranslateYProperty = DependencyProperty.Register("TranslateY", typeof(double), typeof(MarkableImageCanvas));
        private static readonly DependencyProperty ZoomProperty = DependencyProperty.Register("Zoom", typeof(double), typeof(MarkableImageCanvas));

        // Bookmarked pan and zoom setting (initially its default zoomed out normal pan state)
        private Point bookmarkedTranslatedPoint = new Point(0, 0);
        private Point bookmarkedScalePoint = new Point(1, 1);

        // The canvas to magnify contains both an image and any marks, 
        // so that the magnifying lens will contain all correct contents
        private Canvas canvasToMagnify = new Canvas();

        private MagnifyingGlass magnifyingGlass = new MagnifyingGlass();
        private Brush markStrokeBrush = (SolidColorBrush)new BrushConverter().ConvertFromString(Constants.StandardColour);
        private SolidColorBrush markFillBrush = new SolidColorBrush(System.Windows.Media.Color.FromArgb(2, 0, 0, 0));
        private List<Marker> markers;

        // Mouse and position states used to discriminate clicks from drags)
        private UIElement mouseDownSender;
        private DateTime mouseDownTime;
        private Point mouseDownLocation;
        private Point previousLocation;

        // Transform variables
        private ScaleTransform scaleTransform;
        private TransformGroup transformGroup;
        private TranslateTransform translateTransform;

        /// <summary>
        /// Gets or sets the image that will be displayed in the MarkableImageCanvas
        /// </summary>
        public Image ImageToDisplay { get; set; }

        /// <summary>
        /// Gets or sets the image that will be displayed in the MarkableImageCanvas's Magnifying Glass
        /// </summary>
        public Image ImageToMagnify { get; set; }

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
                this.MarkersRefresh();
            }
        }

        /// <summary>
        /// Gets or sets the amount we should zoom (scale) the image
        /// </summary>
        public double Zoom
        {
            get
            {
                return (double)this.GetValue(ZoomProperty);
            }
            set
            {
                this.SetValue(ZoomProperty, value);
                try
                {
                    // Nothing to scale, so get out of here
                    if (this.ImageToDisplay.Source == null)
                    {
                        return;
                    }
                    this.ScaleImage(new Point(100, 100), true);
                }
                catch (Exception exception)
                {
                    Debug.Fail("Zoom as image cannot be scaled.", exception.ToString());
                    this.SetValue(ZoomProperty, value);
                }
            }
        }

        /// <summary>
        /// Gets or sets the maximum level of our zoom (scale) of the image
        /// </summary>
        public double MaxZoom
        {
            get { return (double)this.GetValue(MaxZoomProperty); }
            set { this.SetValue(MaxZoomProperty, value); }
        }

        /// <summary>
        /// Gets or sets the maximum upper bound of our zoom (scale) of the image. While the MaxZoom can be changed, it can't be larger than this.
        /// </summary>
        public double MaxZoomUpperBound
        {
            get { return (double)this.GetValue(MaxZoomUpperBoundProperty); }
            set { this.SetValue(MaxZoomUpperBoundProperty, value); }
        }

        /// <summary>
        /// Gets or sets the maximum level of our magnifier zoom (scale)
        /// </summary>
        public Point MagnifierZoomRange
        {
            get
            {
                return (Point)this.GetValue(MagnifierZoomRangeProperty);
            }
            set
            {
                this.SetValue(MagnifierZoomRangeProperty, value);
                this.magnifyingGlass.ZoomRange = value;
                this.MagnifierZoom = this.MagnifierZoom; // This will reset to MagnifierZoom to ensure its between the minimum and maximum
            }
        }

        /// <summary>
        /// Gets or sets the amount we should zoom (scale) the image in the magnifying glass
        /// </summary>
        public double MagnifierZoom
        {
            get
            {
                return (double)this.GetValue(MagnifierZoomProperty);
            }
            set
            {
                // make sure the value is always in range
                double newValue = value;
                if (value > this.MagnifierZoomRange.X)
                {
                    newValue = this.MagnifierZoomRange.X;
                }
                if (value < this.MagnifierZoomRange.Y)
                {
                    newValue = this.MagnifierZoomRange.Y;
                }
                this.SetValue(MagnifierZoomProperty, newValue);
                this.magnifyingGlass.ZoomValue = newValue;

                // Make sure that there is actually something to magnify
                if (this.ImageToMagnify.Source != null && this.ImageToDisplay.ActualWidth > 0)
                {
                    if (this.ImageToDisplay.ActualWidth > 0)
                    {
                        this.magnifyingGlass.Redraw(NativeMethods.CorrectGetPosition(this),
                                                    NativeMethods.CorrectGetPosition(this.ImageToDisplay),
                                                    this.ImageToDisplay.ActualWidth,
                                                    this.ImageToDisplay.ActualHeight,
                                                    this.canvasToMagnify);
                    }
                }
            }
        }

        /// <summary>
        /// Gets or sets the delta value to increase or decrease our magnifying glass zoom
        /// </summary>
        public double MagnifierZoomDelta
        {
            get { return (double)this.GetValue(MagnifierZoomDeltaProperty); }
            set { this.SetValue(MagnifierZoomDeltaProperty, value); }
        }

        /// <summary>
        /// Gets or sets the amount we should translate the image horizontally on the X axis
        /// </summary>
        public double TranslateX
        {
            get
            {
                return (double)this.GetValue(TranslateXProperty);
            }
            set
            {
                this.SetValue(TranslateXProperty, value);
                this.TransformChanged();
                this.MarkersRefresh();
            }
        }

        /// <summary>
        /// Gets or sets the amount we should translate the image vertically on the Y axis
        /// </summary>
        public double TranslateY
        {
            get
            {
                return (double)this.GetValue(TranslateYProperty);
            }
            set
            {
                this.SetValue(TranslateYProperty, value);
                this.TransformChanged();
                this.MarkersRefresh();
            }
        }

        /// <summary>
        /// Gets or sets a value indicating whether the magnifying glass is generally visible or hidden, and returns its state
        /// </summary>       
        public bool IsMagnifyingGlassVisible
        {
            get
            {
                return (bool)GetValue(IsMagnifyingGlassVisibleProperty);
            }
            set
            {
                this.SetValue(IsMagnifyingGlassVisibleProperty, value);
                this.magnifyingGlass.IsVisibilityDesired = value;
                if (value)
                {
                    this.magnifyingGlass.ShowIfIsVisibilityDesired();
                }
                else
                {
                    this.magnifyingGlass.Hide();
                }
            }
        }

        public event EventHandler<MarkerEventArgs> RaiseMarkerEvent;

        // Wrap event invocations inside a protected virtual method
        // to allow derived classes to override the event invocation behavior
        protected virtual void OnRaiseMarkerEvent(MarkerEventArgs e)
        {
            // Make a temporary copy of the event to avoid possibility of
            // a race condition if the last subscriber unsubscribes
            // immediately after the null check and before the event is raised.
            EventHandler<MarkerEventArgs> handler = this.RaiseMarkerEvent;

            // Event will be null if there are no subscribers. Otherwise, raise the event
            if (handler != null)
            {
                handler(this, e);
            }
        }

        public MarkableImageCanvas()
        {
            // Set up some initial canvas properties and event handlers
            this.Background = Brushes.Black;
            this.ClipToBounds = true;
            this.Focusable = true;
            this.Markers = new List<Marker>();
            this.SizeChanged += new SizeChangedEventHandler(this.OnMarkableImageCanvas_SizeChanged);

            this.ResetMaxZoom();
            this.MaxZoomUpperBound = Constants.MarkableCanvas.ZoomMaximumUpperBound;

            // Set up some initial image properites for the image to magnify
            this.ImageToMagnify = new Image();
            this.ImageToMagnify.HorizontalAlignment = HorizontalAlignment.Left;
            this.ImageToMagnify.VerticalAlignment = VerticalAlignment.Top;
            Canvas.SetLeft(this.ImageToMagnify, 0);
            Canvas.SetTop(this.ImageToMagnify, 0);

            // add the image to the magnification canvas
            this.canvasToMagnify.Children.Add(this.ImageToMagnify);

            // Set up some initial image properites and event handlers 
            this.ImageToDisplay = new Image();
            this.ImageToDisplay.HorizontalAlignment = HorizontalAlignment.Left;
            this.ImageToDisplay.VerticalAlignment = VerticalAlignment.Top;
            this.ImageToDisplay.SizeChanged += new SizeChangedEventHandler(this.OnImgToDisplay_SizeChanged);

            // Position and add the image to the canvas
            Canvas.SetLeft(this.ImageToDisplay, 0);
            Canvas.SetTop(this.ImageToDisplay, 0);
            this.Children.Add(this.ImageToDisplay);

            // Set up the magnifying glass
            this.magnifyingGlass.MarkableCanvasParent = this; // A reference to this so we can access the markable Canvas state
            this.MagnifierZoomRange = new Point(Constants.MarkableCanvas.MagnifierMinZoom, Constants.MarkableCanvas.MagnifierMaxZoom);
            this.MagnifierZoomDelta = Constants.MarkableCanvas.ZoomStep;
            this.MagnifierZoom = Constants.MarkableCanvas.MagnifierDefaultZoom;

            this.magnifyingGlass.Hide();
            Canvas.SetZIndex(this.magnifyingGlass, 1000); // Should always be in front
            this.Children.Add(this.magnifyingGlass);

            // Set up the transforms
            this.translateTransform = new TranslateTransform(0, 0);
            this.scaleTransform = new ScaleTransform(1, 1);

            this.transformGroup = new TransformGroup();
            this.transformGroup.Children.Add(this.scaleTransform);
            this.transformGroup.Children.Add(this.translateTransform);

            // These properties hold the scale and x/y translate values
            this.Zoom = 1;
            this.TranslateX = 0;
            this.TranslateY = 0;
            this.ImageToDisplay.RenderTransform = this.transformGroup;

            // Event handlers for image interaction: mouse handling for marking, zooming, panning, scroll wheel, etc.
            this.ImageToDisplay.MouseDown += new MouseButtonEventHandler(this.OnImage_MouseDown);
            this.MouseMove += new MouseEventHandler(this.OnImage_MouseMove);

            this.ImageToDisplay.MouseUp += new MouseButtonEventHandler(this.OnImage_MouseUp);
            this.ImageToDisplay.MouseWheel += new MouseWheelEventHandler(this.OnImage_MouseWheel);
            this.PreviewKeyDown += new KeyEventHandler(this.OnImage_PreviewKeyDown);

            this.MouseLeave += new MouseEventHandler(this.OnImage_MouseLeave);
        }

        public void ResetMaxZoom()
        {
            this.MaxZoom = Constants.MarkableCanvas.ZoomMaximum;
        }

        public void AddMarker(Marker marker)
        {
            this.Markers.Add(marker);
            this.MarkersRefresh();
        }

        /// <summary>
        /// Remove and then redraw all the markers
        /// </summary>
        public void MarkersRefresh()
        {
            this.MarkersRemove(this);
            this.MarkersRemove(this.canvasToMagnify);
            if (this.ImageToDisplay != null)
            {
                this.DrawMarkers(this, this.ImageToDisplay.RenderSize, true);
                this.DrawMarkers(this.canvasToMagnify, this.canvasToMagnify.RenderSize, false);
            }
        }

        /// <summary>
        /// Zoom in the magnifying glass image  by the amount defined by the property MagnifierZoomDelta
        /// </summary>
        public void MagnifierZoomIn()
        {
            this.MagnifierZoom -= this.MagnifierZoomDelta;
        }

        /// <summary>
        /// Zoom out the magnifying glass image  by the amount defined by the property MagnifierZoomDelta
        /// </summary>
        public void MagnifierZoomOut()
        {
            this.MagnifierZoom += this.MagnifierZoomDelta;
        }

        // A user may want to flip between completely zoomed out / normal pan settings
        // and a saved zoom / pan setting that focuses in on a particular region.
        // To do this, we  save / restore the zoom pan settings of a particular view,
        // or return to the default zoom/pan.

        // Save the current zoom / pan levels as a bookmark
        public void BookmarkSaveZoomPan()
        {
            this.bookmarkedTranslatedPoint.X = this.translateTransform.X;
            this.bookmarkedTranslatedPoint.Y = this.translateTransform.Y;
            this.bookmarkedScalePoint.X = this.scaleTransform.ScaleX;
            this.bookmarkedScalePoint.Y = this.scaleTransform.ScaleY;
        }

        // Clear the current zoom / pan levels as a bookmark, where its set to the zoomed out levels
        public void BookmarkClearZoomPan()
        {
            this.bookmarkedTranslatedPoint.X = 0;
            this.bookmarkedTranslatedPoint.Y = 0;
            this.bookmarkedScalePoint.X = 1;
            this.bookmarkedScalePoint.Y = 1;
        }

        // Return to the zoom / pan levels saved as a bookmark
        public void BookmarkSetZoomPan()
        {
            this.scaleTransform.ScaleX = this.bookmarkedScalePoint.X;
            this.scaleTransform.ScaleY = this.bookmarkedScalePoint.Y;

            this.translateTransform.X = this.bookmarkedTranslatedPoint.X;
            this.translateTransform.Y = this.bookmarkedTranslatedPoint.Y;

            this.MarkersRefresh();
        }

        // Return to the zoomed out level, with no panning
        public void BookmarkZoomOutAllTheWay()
        {
            this.scaleTransform.ScaleX = 1;
            this.scaleTransform.ScaleY = 1;

            this.translateTransform.X = 0;
            this.translateTransform.Y = 0;

            this.MarkersRefresh();
        }

        // Whenever the canvas size changes, resize the image
        private void OnMarkableImageCanvas_SizeChanged(object sender, SizeChangedEventArgs e)
        {
            this.ImageToDisplay.Width = this.ActualWidth;
            this.ImageToDisplay.Height = this.ActualHeight;
            this.BookmarkClearZoomPan(); // We clear the bookmark (if any) as it will no longer be correct.
        }

        // Whenever the image size changes, refresh the markers so they appear in the correct place
        private void OnImgToDisplay_SizeChanged(object sender, SizeChangedEventArgs e)
        {
            this.MarkersRefresh();
        }

        // On Mouse down, record the location, who sent it, and the time.
        // We will use this information on move and up events to discriminate between 
        // panning/zooming vs. marking. 
        private void OnImage_MouseDown(object sender, MouseButtonEventArgs e)
        {
            this.previousLocation = e.GetPosition(this);
            if (e.LeftButton == MouseButtonState.Pressed)
            {
                this.mouseDownLocation = e.GetPosition(this.ImageToDisplay);
                this.mouseDownSender = (UIElement)sender;
                this.mouseDownTime = DateTime.Now;
            }
        }

        // If we move the mouse with the left mouse button press, translate the image
        private void OnImage_MouseMove(object sender, MouseEventArgs e)
        {
            // The visibility of the magnifying glass depends on whether the mouse is over the image
            this.SetMagnifyingGlassVisibility(e.GetPosition(this));

            // Calculate how much time has passed since the mouse down event?
            TimeSpan timeSinceDown = DateTime.Now - this.mouseDownTime;

            // If at least WAIT_TIME milliseconds has passed
            if (timeSinceDown >= TimeSpan.FromMilliseconds(100))
            {
                Point location = e.GetPosition(this);   // Get the current location

                // If the left button is pressed, translate (pan) across the image 
                //  Also hide the magnifying glass so it won't be distracting
                if (e.LeftButton == MouseButtonState.Pressed)
                {
                    this.Cursor = Cursors.ScrollAll;    // Change the cursor to a panning cursor
                    this.TranslateImage(location);
                    this.magnifyingGlass.Hide();
                }
                else
                {
                    // Update the magnifying glass
                    this.canvasToMagnify.Width = this.ImageToMagnify.ActualWidth;      // Make sure that the canvas is the same size as the image
                    this.canvasToMagnify.Height = this.ImageToMagnify.ActualHeight;
                    this.magnifyingGlass.Redraw(NativeMethods.CorrectGetPosition(this),
                                                NativeMethods.CorrectGetPosition(this.ImageToDisplay), 
                                                this.ImageToDisplay.ActualWidth, 
                                                this.ImageToDisplay.ActualHeight, 
                                                this.canvasToMagnify);
                }
                this.previousLocation = location;
            }
        }

        // The magnifying glass is visible only if the current mouse position is over the image. 
        private void SetMagnifyingGlassVisibility(Point mousePosition)
        {
            // The the actual (transformed) bounds of the image
            Point transformedSize = this.transformGroup.Transform(new Point(this.ImageToDisplay.ActualWidth, this.ImageToDisplay.ActualHeight));
            bool mouseOverImage = (mousePosition.X <= transformedSize.X) && (mousePosition.Y <= transformedSize.Y);
            if (mouseOverImage)
            {
                this.magnifyingGlass.ShowIfIsVisibilityDesired();
            }
            else
            {
                this.magnifyingGlass.Hide();
            }
        }

        private void OnImage_MouseUp(object sender, MouseButtonEventArgs e)
        {
            // Make sure the cursor reverts to the normal arrow cursor
            this.Cursor = Cursors.Arrow;

            // Get the current position
            Point location = e.GetPosition(this.ImageToDisplay);

            // Is this the end of a translate operation, or a marking operation?
            // We decide by checking if the left button has been released, the mouse location is
            // smaller than a given threshold, and less than 200 ms have passed since the original
            // mouse down. i.e., the use has done a rapid click and release on a small location
            if ((e.LeftButton == MouseButtonState.Released) &&
                (sender == this.mouseDownSender) &&
                (this.mouseDownLocation - location).Length <= 2.0)
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
                    this.OnRaiseMarkerEvent(new MarkerEventArgs(marker, true));
                }
            }
            this.magnifyingGlass.ShowIfIsVisibilityDesired();
        }

        // Use the  mouse wheel to scale the image
        private void OnImage_MouseWheel(object sender, MouseWheelEventArgs e)
        {
            lock (this)
            {
                // We will scale around the current point
                Point location = e.GetPosition(this.ImageToDisplay);
                bool zoomIn = e.Delta > 0; // Zooming in if delta is positive, else zooming out
                this.ScaleImage(location, zoomIn);
            }
        }

        // Hide the magnifying glass when the mouse cursor leaves the image
        private void OnImage_MouseLeave(object sender, MouseEventArgs e)
        {
            this.magnifyingGlass.Hide();
        }

        // If its < or > key zoom out or in around the mouse point.
        private void OnImage_PreviewKeyDown(object sender, KeyEventArgs e)
        {
            // Depending on the key, take the appropriate action
            switch (e.Key)
            {
                // zoom in
                case Key.OemPeriod:
                    lock (this.ImageToDisplay)
                    {
                        Point location = Mouse.GetPosition(this.ImageToDisplay);
                        if (location.X > this.ImageToDisplay.ActualWidth || location.Y > this.ImageToDisplay.ActualHeight)
                        {
                            break; // Ignore points if mouse is off the image
                        }
                        this.ScaleImage(location, true); // Zooming in if delta is positive, else zooming out
                    }
                    break;
                // zoom out
                case Key.OemComma:
                    lock (this.ImageToDisplay)
                    {
                        Point location = Mouse.GetPosition(this.ImageToDisplay);
                        this.ScaleImage(location, false); // Zooming in if delta is positive, else zooming out
                    }
                    break;

                default:
                    return;
            }
            e.Handled = true;
        }

        // Given the mouse location on the image, translate the image
        // This is normally called from a left mouse move event
        private void TranslateImage(Point mouse_location)
        {
            // Get the center point on the image
            Point center = this.PointFromScreen(this.ImageToDisplay.PointToScreen(
                new Point(this.ImageToDisplay.Width / 2.0, this.ImageToDisplay.Height / 2.0)));

            // Calculate the delta position from the last location relative to the center
            double newX = center.X + mouse_location.X - this.previousLocation.X;
            double newY = center.Y + mouse_location.Y - this.previousLocation.Y;

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

            this.MarkersRefresh();
        }

        // Scale the image around the given image location point, where we are zooming in if
        // zoomIn is true, and zooming out if zoomIn is false
        public void ScaleImage(Point location, bool zoomIn)
        {
            // Get out of here if we are already at our maximum or minimum scaling values 
            // while zooming in or out respectively 
            if ((zoomIn && this.scaleTransform.ScaleX >= this.MaxZoom) ||
                (!zoomIn && this.scaleTransform.ScaleX <= Constants.MarkableCanvas.ZoomMinimum))
            {
                return;
            }

            // We will scale around the current point
            Point beforeZoom = this.PointFromScreen(this.ImageToDisplay.PointToScreen(location));

            // Calculate the scaling factor during zoom ins or out. Ensure that we keep within our
            // maximum and minimum scaling bounds. 
            if (zoomIn)
            {
                // We are zooming in
                // Calculate the scaling factor
                this.scaleTransform.ScaleX *= Constants.MarkableCanvas.ZoomStep;   // Calculate the scaling factor
                this.scaleTransform.ScaleY *= Constants.MarkableCanvas.ZoomStep;

                // Make sure we don't scale beyond the maximum scaling factor
                this.scaleTransform.ScaleX = Math.Min(this.MaxZoom, this.scaleTransform.ScaleX);
                this.scaleTransform.ScaleY = Math.Min(this.MaxZoom, this.scaleTransform.ScaleY);
            }
            else
            {
                // We are zooming out. 
                // Calculate the scaling factor
                this.scaleTransform.ScaleX /= Constants.MarkableCanvas.ZoomStep;
                this.scaleTransform.ScaleY /= Constants.MarkableCanvas.ZoomStep;

                // Make sure we don't scale beyond the minimum scaling factor
                this.scaleTransform.ScaleX = Math.Max(Constants.MarkableCanvas.ZoomMinimum, this.scaleTransform.ScaleX);
                this.scaleTransform.ScaleY = Math.Max(Constants.MarkableCanvas.ZoomMinimum, this.scaleTransform.ScaleY);

                // if there is no scaling, reset translations
                if (this.scaleTransform.ScaleX == 1.0 && this.scaleTransform.ScaleY == 1.0)
                {
                    this.translateTransform.X = 0.0;
                    this.translateTransform.Y = 0.0;
                }
            }

            Point afterZoom = this.PointFromScreen(this.ImageToDisplay.PointToScreen(location));

            // Scale the image, and at the same time translate it so that the 
            // point in the image under the cursor stays there
            double imageWidth = this.ImageToDisplay.Width * this.scaleTransform.ScaleX;
            double imageHeight = this.ImageToDisplay.Height * this.scaleTransform.ScaleY;

            Point center = this.PointFromScreen(this.ImageToDisplay.PointToScreen(
                new Point(this.ImageToDisplay.Width / 2.0, this.ImageToDisplay.Height / 2.0)));

            double newX = center.X - (afterZoom.X - beforeZoom.X);
            double newY = center.Y - (afterZoom.Y - beforeZoom.Y);

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

            this.translateTransform.X += newX - center.X;
            this.translateTransform.Y += newY - center.Y;
            this.MarkersRefresh();
        }

        private void TransformChanged()
        {
            if (this == null)
            {
                return;
            }
            try
            {
                this.scaleTransform.ScaleX = this.Zoom;
                this.scaleTransform.ScaleY = this.Zoom;
                this.translateTransform.X = this.TranslateX;
                this.translateTransform.Y = this.TranslateY;
            }
            catch (Exception exception)
            {
                Debug.Fail("TransformChanged as image cannot be transformed", exception.ToString());
            }
        }

        private void DrawMarkers(Canvas canvas, Size canvasRenderSize, bool doTransform)
        {
            foreach (Marker marker in this.Markers)
            {
                Canvas markerCanvas = this.DrawMarker(marker, canvasRenderSize, doTransform);
                canvas.Children.Add(markerCanvas);
            }
        }

        private Canvas DrawMarker(Marker marker, Size canvasRenderSize, bool doTransform)
        {
            Canvas markerCanvas = new Canvas();
            markerCanvas.MouseRightButtonUp += new MouseButtonEventHandler(this.Marker_MouseRightButtonUp);
            markerCanvas.MouseWheel += new MouseWheelEventHandler(this.OnImage_MouseWheel); // Make the mouse wheel work over marks as well as the image

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
            mark.Width = Constants.MarkableCanvas.MarkDiameter;
            mark.Height = Constants.MarkableCanvas.MarkDiameter;
            mark.StrokeThickness = Constants.MarkableCanvas.MarkStrokeThickness;
            mark.Stroke = marker.Brush;
            mark.Fill = this.markFillBrush;  // Should be a transparent fill so it can get hit events
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
                glow.Width = whiteOutline.Width + Constants.MarkableCanvas.MarkGlowDiameterIncrease;
                glow.Height = whiteOutline.Height + Constants.MarkableCanvas.MarkGlowDiameterIncrease;
                glow.StrokeThickness = Constants.MarkableCanvas.MarkGlowStrokeThickness;
                glow.Stroke = mark.Stroke;
                glow.Opacity = Constants.MarkableCanvas.MarkGlowOpacity;
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
            markerCanvas.MouseDown += new MouseButtonEventHandler(this.OnImage_MouseDown);
            markerCanvas.MouseMove += new MouseEventHandler(this.OnImage_MouseMove);
            markerCanvas.MouseUp += new MouseButtonEventHandler(this.OnImage_MouseUp);
            return markerCanvas;
        }

        // Remove a marker on a right mouse button up event
        private void Marker_MouseRightButtonUp(object sender, MouseButtonEventArgs e)
        {
            Canvas canvas = (Canvas)sender;
            Marker marker = (Marker)canvas.Tag;
            this.Markers.Remove(marker);
            this.OnRaiseMarkerEvent(new MarkerEventArgs(marker, false));
            this.MarkersRefresh();
        }

        // remove all markers from the canvass
        private void MarkersRemove(Canvas canvas)
        {
            for (int index = canvas.Children.Count - 1; index >= 0; index--)
            {
                if (canvas.Children[index] is Canvas && canvas.Children[index] != this.magnifyingGlass)
                {
                    canvas.Children.RemoveAt(index);
                }
            }
        }
    }
}
