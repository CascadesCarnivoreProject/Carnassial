﻿using Carnassial.Controls;
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

        private ZoomBookmark bookmark;

        // the canvas to magnify contains both an image and markers so the magnifying glass view matches the display image
        private Canvas canvasToMagnify;

        // render transforms
        // RedrawMagnifyingGlassIfVisible() must be kept in sync with these.  See comments there.
        private ScaleTransform imageToDisplayScale;
        private TranslateTransform imageToDisplayTranslation;

        private MagnifyingGlass magnifyingGlass;

        private List<Marker> markers;

        // mouse and position states used to discriminate clicks from drags
        private UIElement mouseDownSender;
        private DateTime mouseDownTime;
        private Point mouseDownLocation;
        private Point previousMousePosition;

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
                // update markers
                this.markers = value;
                // render new markers and update display image
                this.RedrawMarkers();
                // update magnifying glass content
                this.RedrawMagnifyingGlassIfVisible();
            }
        }

        /// <summary>
        /// Gets or sets the maximum zoom of the display image
        /// </summary>
        public double ZoomMaximum { get; set; }

        /// <summary>
        /// Gets or sets the amount of ImageToMagnify shown in the magnifying glass.  A higher zoom is a smaller field of view.
        /// </summary>
        private double MagnifyingGlassFieldOfView
        {
            get
            {
                return this.magnifyingGlass.FieldOfView;
            }
            set
            {
                // clamp the value
                if (value < Constant.MarkableCanvas.MagnifyingGlassMinimumFieldOfView)
                {
                    value = Constant.MarkableCanvas.MagnifyingGlassMinimumFieldOfView;
                }
                else if (value > Constant.MarkableCanvas.MagnifyingGlassMaximumFieldOfView)
                {
                    value = Constant.MarkableCanvas.MagnifyingGlassMaximumFieldOfView;
                }
                this.magnifyingGlass.FieldOfView = value;

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
                if (value && this.VideoToDisplay.Visibility != Visibility.Visible)
                {
                    // draw the magnifying glass if it was just enabled and an image is being displayed
                    // Note: the magnifying glass may immediately be hidden again if the mouse isn't over the display image.
                    this.magnifyingGlass.Show();
                    this.RedrawMagnifyingGlassIfVisible();
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
            this.bookmark = new ZoomBookmark();
            this.imageToDisplayScale = new ScaleTransform(this.bookmark.Scale.X, this.bookmark.Scale.Y);
            this.imageToDisplayTranslation = new TranslateTransform(this.bookmark.Translation.X, this.bookmark.Translation.Y);

            TransformGroup transformGroup = new TransformGroup();
            transformGroup.Children.Add(this.imageToDisplayScale);
            transformGroup.Children.Add(this.imageToDisplayTranslation);

            // set up display image
            this.ImageToDisplay = new Image();
            this.ImageToDisplay.HorizontalAlignment = HorizontalAlignment.Left;
            this.ImageToDisplay.MouseDown += this.ImageOrCanvas_MouseDown;
            this.ImageToDisplay.MouseUp += this.ImageOrCanvas_MouseUp;
            this.ImageToDisplay.MouseWheel += this.ImageOrCanvas_MouseWheel;
            this.ImageToDisplay.RenderTransform = transformGroup;
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
            this.ImageToMagnify.SizeChanged += this.ImageToMagnify_SizeChanged;
            this.ImageToMagnify.VerticalAlignment = VerticalAlignment.Top;
            Canvas.SetLeft(this.ImageToMagnify, 0);
            Canvas.SetTop(this.ImageToMagnify, 0);

            this.canvasToMagnify = new Canvas();
            this.canvasToMagnify.SizeChanged += this.CanvasToMagnify_SizeChanged;
            this.canvasToMagnify.Children.Add(this.ImageToMagnify);

            // set up the magnifying glass
            this.magnifyingGlass = new MagnifyingGlass(this);

            Canvas.SetZIndex(this.magnifyingGlass, 1000); // Should always be in front
            this.Children.Add(this.magnifyingGlass);

            // event handlers for image interaction: keys, mouse handling for markers
            // this.mouseDownLocation not initialized as it's set from the display image's mouse down handler
            // this.mouseDownSender left as null as it's set from the display image's mouse down handler
            // this.mouseDownTime not initialized as it's not consumed until after being set from the display image's mouse down handler
            this.MouseLeave += this.ImageOrCanvas_MouseLeave;
            this.MouseMove += this.MarkableCanvas_MouseMove;
            this.PreviewKeyDown += this.MarkableCanvas_PreviewKeyDown;
        }

        // Return to the zoom / pan levels saved as a bookmark
        public void ApplyBookmark()
        {
            this.bookmark.Apply(this.imageToDisplayScale, this.imageToDisplayTranslation);
            this.RedrawMarkers();
        }

        private void CanvasToMagnify_SizeChanged(object sender, SizeChangedEventArgs e)
        {
            // redraw markers so they're in the right place to appear in the magnifying glass
            this.RedrawMagnifierMarkers();

            // update the magnifying glass's contents
            this.RedrawMagnifyingGlassIfVisible();
        }

        private Canvas DrawMarker(Marker marker, Size canvasRenderSize, bool imageToDisplayMarkers)
        {
            Canvas markerCanvas = new Canvas();
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

            if (marker.ShowLabel)
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
            if (imageToDisplayMarkers)
            {
                screenPosition = this.ImageToDisplay.RenderTransform.Transform(screenPosition);
            }

            Canvas.SetLeft(markerCanvas, screenPosition.X - markerCanvas.Width / 2.0);
            Canvas.SetTop(markerCanvas, screenPosition.Y - markerCanvas.Height / 2.0);
            Canvas.SetZIndex(markerCanvas, 0);
            markerCanvas.MouseDown += this.ImageOrCanvas_MouseDown;
            markerCanvas.MouseMove += this.MarkableCanvas_MouseMove;
            markerCanvas.MouseRightButtonUp += this.Marker_MouseRightButtonUp;
            markerCanvas.MouseUp += this.ImageOrCanvas_MouseUp;
            markerCanvas.MouseWheel += this.ImageOrCanvas_MouseWheel; // Make the mouse wheel work over marks as well as the image
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

        // record the location, who sent it, and the time.
        // This information is used on move and up events to discriminate between marker placement and panning. 
        private void ImageOrCanvas_MouseDown(object sender, MouseButtonEventArgs e)
        {
            this.previousMousePosition = e.GetPosition(this);
            if (e.LeftButton == MouseButtonState.Pressed)
            {
                this.mouseDownLocation = e.GetPosition(this.ImageToDisplay);
                this.mouseDownSender = (UIElement)sender;
                this.mouseDownTime = DateTime.UtcNow;
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
            Point mousePosition = e.GetPosition(this.ImageToDisplay);

            // Is this the end of a translate operation or of placing a maker?
            // Create a marker if the left button has been released, the mouse movement is below threshold, and less than 200 ms have passed since the original
            // mouse down.
            if ((e.LeftButton == MouseButtonState.Released) &&
                (sender == this.mouseDownSender) &&
                (this.mouseDownLocation - mousePosition).Length <= 2.0)
            {
                TimeSpan timeSinceDown = DateTime.UtcNow - this.mouseDownTime;
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

            // show the magnifying glass again if it was hidden during a pan
            this.RedrawMagnifyingGlassIfVisible();
        }

        // use the mouse wheel to scale the image
        private void ImageOrCanvas_MouseWheel(object sender, MouseWheelEventArgs e)
        {
            lock (this)
            {
                // use the current mouse position as the zoom center
                Point mousePosition = e.GetPosition(this.ImageToDisplay);
                bool zoomIn = e.Delta > 0; // zoom in if delta is positive, otherwise out
                this.ScaleImage(mousePosition, zoomIn);
            }
        }

        private void ImageToDisplay_SizeChanged(object sender, SizeChangedEventArgs e)
        {
            // when the display image size changes refresh the markers so they appear in the correct place
            this.RedrawDisplayMarkers();
        }

        private void ImageToMagnify_SizeChanged(object sender, SizeChangedEventArgs e)
        {
            // keep the magnifying glass canvas in sync with the magnified image size
            // this update triggers a call to CanvasToMagnify_SizeChanged
            this.canvasToMagnify.Width = this.ImageToMagnify.ActualWidth;
            this.canvasToMagnify.Height = this.ImageToMagnify.ActualHeight;
        }

        /// <summary>
        /// Enlarge the magnifying glass image.
        /// </summary>
        public void MagnifierZoomIn()
        {
            this.MagnifyingGlassFieldOfView /= Constant.MarkableCanvas.MagnifyingGlassFieldOfViewIncrement;
        }

        /// <summary>
        /// Show more area in the magnifying glass image.
        /// </summary>
        public void MagnifierZoomOut()
        {
            this.MagnifyingGlassFieldOfView *= Constant.MarkableCanvas.MagnifyingGlassFieldOfViewIncrement;
        }

        private void MarkableCanvas_MouseMove(object sender, MouseEventArgs e)
        {
            // magnifying glass should be visible only if the current mouse position is over the display image
            if (this.magnifyingGlass.IsEnabled)
            {
                bool mouseOverDisplayImage = false;
                if (this.ImageToDisplay.IsVisible)
                {
                    mouseOverDisplayImage = this.ImageToDisplay.Contains(e.GetPosition(this.ImageToDisplay));
                }

                if (mouseOverDisplayImage)
                {
                    this.magnifyingGlass.Visibility = Visibility.Visible;
                }
                else
                {
                    this.magnifyingGlass.Hide();
                }
            }

            // panning isn't supported on videos
            if (this.ImageToDisplay.IsVisible == false)
            {
                return;
            }

            // pan or update magnifying glass
            // if the left button is pressed pan the image 
            Point mousePosition = e.GetPosition(this);
            if (e.LeftButton == MouseButtonState.Pressed)
            {
                // change to a panning cursor
                this.Cursor = Cursors.ScrollAll;
                // also hide the magnifying glass so it won't be distracting
                this.magnifyingGlass.Hide();
                // pan
                this.TranslateImage(mousePosition);
            }
            else
            {
                // update the magnifying glass
                this.RedrawMagnifyingGlassIfVisible();
            }

            this.previousMousePosition = mousePosition;
        }

        // if it's < or > key zoom out or in around the mouse point
        [SuppressMessage("StyleCop.CSharp.ReadabilityRules", "SA1126:PrefixCallsCorrectly", Justification = "StyleCop bug.")]
        private void MarkableCanvas_PreviewKeyDown(object sender, KeyEventArgs e)
        {
            switch (e.Key)
            {
                case Key.B:
                    if (Keyboard.Modifiers == ModifierKeys.None)
                    {
                        // apply the current bookmark
                        this.ApplyBookmark();
                    }
                    else if (Keyboard.Modifiers == ModifierKeys.Control)
                    {
                        // bookmark (save) the current pan / zoom of the display image
                        this.SetBookmark();
                    }
                    else
                    {
                        return;
                    }
                    break;
                // decrease the magnifing glass zoom
                case Key.D:
                    this.MagnifierZoomOut();
                    break;
                // return to full view of display image
                case Key.D0:
                    if (Keyboard.Modifiers == ModifierKeys.Control)
                    {
                        this.ZoomToFit();
                    }
                    else
                    {
                        return;
                    }
                    break;
                // zoom out
                case Key.OemMinus:
                    Point mousePosition = Mouse.GetPosition(this.ImageToDisplay);
                    this.ScaleImage(mousePosition, false);
                    break;
                // zoom in
                case Key.OemPlus:
                    Rect imageToDisplayBounds = new Rect(0.0, 0.0, this.ImageToDisplay.ActualWidth, this.ImageToDisplay.ActualHeight);
                    mousePosition = Mouse.GetPosition(this.ImageToDisplay);
                    if (imageToDisplayBounds.Contains(mousePosition) == false)
                    {
                        break; // ignore if mouse is not on the image
                    }
                    this.ScaleImage(mousePosition, true);
                    break;
                // if the current file's a video allow the user to hit the space bar to start or stop playing the video
                // This is desirable as the play or pause button doesn't necessarily have focus and it saves the user having to click the button with
                // the mouse.
                case Key.Space:
                    if (this.TryPlayOrPauseVideo() == false)
                    {
                        return;
                    }
                    break;
                // increase the magnifing glass zoom
                case Key.U:
                    this.MagnifierZoomIn();
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

            this.imageToDisplayScale.CenterX = 0.5 * this.ActualWidth;
            this.imageToDisplayScale.CenterY = 0.5 * this.ActualHeight;

            // clear the bookmark (if any) as it will no longer be correct
            // if needed, the bookmark could be rescaled instead
            this.bookmark.Reset();
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
            // nothing to magnify
            if ((this.IsVisible == false) ||
                (this.ImageToMagnify.Source == null))
            {
                this.magnifyingGlass.Hide();
                return;
            }

            // mouse is off of display image and magnifying glass shouldn't be shown
            Point mousePosition = Mouse.GetPosition(this.ImageToDisplay);
            if (this.ImageToDisplay.Contains(mousePosition) == false)
            {
                this.magnifyingGlass.Hide();
                return;
            }

            // determine magnifier translation for current mouse location
            // This has no effect when the image to display has a unity scaling transform but is needed to properly place the glass when the display image
            // zoom is above unity.  The difficulty is the magnifer's overall render scale (not its lens zoom!) remains constant as the display image scale
            // changes, resulting in relative motion between the magnifier render origin at 0,0 on the display image and the display image's effective origin
            // due to its transforms.
            // Calculations here depend on the render transforms applied and their ordering and must be updated if these changes.
            Point scaledMousePosition = new Point(this.imageToDisplayScale.ScaleX * mousePosition.X, this.imageToDisplayScale.ScaleY * mousePosition.Y);
            Point transformedOrigin = this.imageToDisplayTranslation.Transform(this.imageToDisplayScale.Transform(new Point(0, 0)));
            Point magnifierTranslation = new Point(scaledMousePosition.X - mousePosition.X + transformedOrigin.X,
                                                   scaledMousePosition.Y - mousePosition.Y + transformedOrigin.Y);
            this.magnifyingGlass.RedrawIfVisible(this.canvasToMagnify, magnifierTranslation);
        }

        /// <summary>
        /// Remove all and then draw all the markers
        /// </summary>
        private void RedrawMarkers()
        {
            this.RedrawDisplayMarkers();
            this.RedrawMagnifierMarkers();
        }

        private void RedrawDisplayMarkers()
        {
            this.RemoveMarkers(this);
            this.DrawMarkers(this, this.ImageToDisplay.RenderSize, true);
        }

        private void RedrawMagnifierMarkers()
        {
            this.RemoveMarkers(this.canvasToMagnify);
            this.DrawMarkers(this.canvasToMagnify, this.canvasToMagnify.RenderSize, false);
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

        public void ResetMaximumZoom()
        {
            this.ZoomMaximum = Constant.MarkableCanvas.ImageZoomMaximum;
        }

        // scale the image around the given position
        public void ScaleImage(Point mousePosition, bool zoomIn)
        {
            // nothing to do if at maximum or minimum scaling value whilst zooming in or out, respectively 
            if ((zoomIn && this.imageToDisplayScale.ScaleX >= this.ZoomMaximum) ||
                (!zoomIn && this.imageToDisplayScale.ScaleX <= Constant.MarkableCanvas.ImageZoomMinimum))
            {
                return;
            }

            lock (this.ImageToDisplay)
            {
                // update scaling factor, keeping within maximum and minimum bounds
                if (zoomIn)
                {
                    this.imageToDisplayScale.ScaleX *= Constant.MarkableCanvas.MagnifyingGlassFieldOfViewIncrement;
                    this.imageToDisplayScale.ScaleX = Math.Min(this.ZoomMaximum, this.imageToDisplayScale.ScaleX);
                }
                else
                {
                    this.imageToDisplayScale.ScaleX /= Constant.MarkableCanvas.MagnifyingGlassFieldOfViewIncrement;
                    this.imageToDisplayScale.ScaleX = Math.Max(Constant.MarkableCanvas.ImageZoomMinimum, this.imageToDisplayScale.ScaleX);
                }
                this.imageToDisplayScale.ScaleY = this.imageToDisplayScale.ScaleX;

                if (this.imageToDisplayScale.ScaleX <= Constant.MarkableCanvas.ImageZoomMinimum)
                {
                    // no translation needed if no scaling
                    this.imageToDisplayTranslation.X = 0.0;
                    this.imageToDisplayTranslation.Y = 0.0;
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
                    Point maximumTranslation = new Point(0.5 * this.imageToDisplayScale.ScaleX * this.ImageToDisplay.ActualWidth - imageCenter.X, 0.5 * this.imageToDisplayScale.ScaleY * this.ImageToDisplay.ActualHeight - imageCenter.Y);
                    Vector unconstrainedTranslation = imageCenter - mousePosition;
                    this.imageToDisplayTranslation.X = Math.Max(-maximumTranslation.X, Math.Min(unconstrainedTranslation.X, maximumTranslation.X));
                    this.imageToDisplayTranslation.Y = Math.Max(-maximumTranslation.Y, Math.Min(unconstrainedTranslation.Y, maximumTranslation.Y));
                }

                this.RedrawMarkers();
            }
        }

        public void SetBookmark()
        {
            // a user may want to flip between zoom all and a remembered zoom / pan setting that focuses in on a particular region
            this.bookmark.Set(this.imageToDisplayScale, this.imageToDisplayTranslation);
        }

        /// <summary>
        /// Sets only the display image and leaves markers and the magnifier image unchanged.  Used by the differencing routines to set the difference image.
        /// </summary>
        public void SetDisplayImage(BitmapSource bitmapSource)
        {
            this.ImageToDisplay.Source = bitmapSource;
        }

        /// <summary>
        /// Set a wholly new image.  Clears any existing markers and syncs the magnifier image to the display image.
        /// </summary>
        public void SetNewImage(BitmapSource bitmapSource, List<Marker> markers)
        {
            // change to new markres
            this.markers = markers;

            // initate render of new image for display
            this.ImageToDisplay.Source = bitmapSource;

            // initiate render of magnified image
            // The asynchronous chain behind this is not entirely trivial.  The links are
            //   1) ImageToMagnify_SizeChanged fires and updates canvasToMagnify's size to match
            //   2) CanvasToMagnify_SizeChanged fires and redraws the magnified markers since the cavas size is now known and marker positions can update
            //   3) CanvasToMagnify_SizeChanged initiates a render on the magnifying glass to show the new image and marker positions
            //   4) if it's visible the magnifying glass content updates
            // This synchronization to WPF render opertations is necessary as, despite their appearance, properties like Source, Width, and Height are 
            // asynchronous.  Other approaches therefore tend to be subject to race conditions in render order which hide or misplace markers in the 
            // magnified view and also have a proclivity towards leaving incorrect or stale magnifying glass content on screen.
            // 
            // Another race exists as this.Markers can be set during the above rendering, initiating a second, concurrent marker render.  This is unavoidable
            // due to the need to expose a marker property but is mitigated by accepting new markers through this API and performing the set above as 
            // this.markers rather than this.Markers.
            this.ImageToMagnify.Source = bitmapSource;

            // ensure display image is visible and magnifying glass is visible if it's enabled
            this.ImageToDisplay.Visibility = Visibility.Visible;
            if (this.MagnifyingGlassEnabled)
            {
                this.magnifyingGlass.Show();
            }

            // ensure any previous video is stopped and hidden
            if (this.VideoToDisplay.Visibility == Visibility.Visible)
            {
                this.VideoToDisplay.Pause();
                this.VideoToDisplay.Visibility = Visibility.Collapsed;
            }
        }

        public void SetNewVideo(FileInfo videoFile, List<Marker> markers)
        {
            if (videoFile.Exists == false)
            {
                this.SetNewImage(Constant.Images.FileNoLongerAvailable.Value, markers);
                return;
            }

            this.markers = markers;
            this.VideoToDisplay.SetSource(new Uri(videoFile.FullName));

            this.ImageToDisplay.Visibility = Visibility.Collapsed;
            this.magnifyingGlass.Hide();
            this.VideoToDisplay.Visibility = Visibility.Visible;

            // leave the magnifying glass's enabled state unchanged so user doesn't have to constantly keep re-enabling it in hybrid image sets
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
            double imageWidth = this.ImageToDisplay.Width * this.imageToDisplayScale.ScaleX;
            double imageHeight = this.ImageToDisplay.Height * this.imageToDisplayScale.ScaleY;

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
            this.imageToDisplayTranslation.X += newX - center.X;
            this.imageToDisplayTranslation.Y += newY - center.Y;

            this.RedrawMarkers();
        }

        public bool TryPlayOrPauseVideo()
        {
            return this.VideoToDisplay.TryPlayOrPause();
        }

        // Whenever the image size changes, refresh the markers so they appear in the correct place
        private void VideoToDisplay_SizeChanged(object sender, SizeChangedEventArgs e)
        {
            this.RedrawMarkers();
        }

        // Return to the zoomed out level, with no panning
        public void ZoomToFit()
        {
            this.imageToDisplayScale.ScaleX = 1.0;
            this.imageToDisplayScale.ScaleY = 1.0;
            this.imageToDisplayTranslation.X = 0.0;
            this.imageToDisplayTranslation.Y = 0.0;
            this.RedrawMarkers();
        }
    }
}