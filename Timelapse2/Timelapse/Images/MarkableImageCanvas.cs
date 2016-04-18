using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Shapes;

namespace Timelapse.Images
{
    /// <summary>
    /// MarkableImageCanvas - A canvas that:
    /// - contains an image that can be scaled and translated by the user with the mouse, 
    /// - can draw and track marks atop the image
    /// </summary>
    public class MarkableImageCanvas : Canvas
    {
        private List<MetaTag> metaTags;

        #region Public Properties

        /// <summary>
        /// The image that will be displayed in the MarkableImageCanvas
        /// </summary>
        public Image ImageToDisplay { get; set; }

        /// <summary>
        /// The image that will be displayed in the MarkableImageCanvas's Magnifying Glass
        /// </summary>
        public Image ImageToMagnify { get; set; }

        /// <summary>
        /// The list of MetaTags  we are using to define the marks on the image
        /// </summary>
        public List<MetaTag> MetaTags
        {
            get
            {
                return metaTags;
            }
            set
            {
                metaTags = value;
                MarkersRefresh();
            }
        }

        public static DependencyProperty ZoomProperty = DependencyProperty.Register("Zoom", typeof(double), typeof(MarkableImageCanvas));
        /// <summary>
        /// The amount we should zoom (scale) the image
        /// </summary>
        public double Zoom
        {
            get 
            {
                return (double)GetValue(ZoomProperty); 
            }
            set 
            {
                SetValue(ZoomProperty, value);
                try
                {
                    // Nothing to scale, so get out of here
                    if (this.ImageToDisplay.Source == null) return; 
                    this.ScaleImage(new Point(100, 100), true);
                }
                catch 
                { 
                    Debug.Print ("Catch: In MarkableCanvas:Zoom as image cannot be scaled");
                    SetValue(ZoomProperty, value);
                };
                
            }
        }

        public static DependencyProperty MaxZoomProperty = DependencyProperty.Register("MaxZoom", typeof(double), typeof(MarkableImageCanvas));
        /// <summary>
        /// The maximum level of our zoom (scale) of the image
        /// </summary>
        public double MaxZoom
        {
            get
            {
                return (double)GetValue(MaxZoomProperty);
            }
            set
            {
                SetValue(MaxZoomProperty, value);
            }
        }

        public static DependencyProperty MaxZoomUpperBoundProperty = DependencyProperty.Register("MaxZoomUpperBound", typeof(double), typeof(MarkableImageCanvas));
        /// <summary>
        /// The maximum upper bound of our zoom (scale) of the image. While the MaxZoom can be changed, it can't be larger than this.
        /// </summary>
        public double MaxZoomUpperBound
        {
            get
            {
                return (double)GetValue(MaxZoomUpperBoundProperty);
            }
            set
            {
                SetValue(MaxZoomUpperBoundProperty, value);
            }
        }

        public static DependencyProperty MagnifierZoomRangeProperty = DependencyProperty.Register("MagnifierZoomRange", typeof(Point), typeof(MarkableImageCanvas));
        /// <summary>
        /// The maximum level of our magnifier zoom (scale)
        /// </summary>
        public Point MagnifierZoomRange
        {
            get
            {
                return (Point)GetValue(MagnifierZoomRangeProperty);
            }
            set
            {
                SetValue(MagnifierZoomRangeProperty, value);
                this.magnifyingGlass.ZoomRange = value;
                this.MagnifierZoom = this.MagnifierZoom; // This will reset to MagnifierZoom to ensure its between the minimum and maximum
            }
        }

        public static DependencyProperty MagnifierZoomProperty = DependencyProperty.Register("MagnifierZoom", typeof(double), typeof(MarkableImageCanvas));
        /// <summary>
        /// The amount we should zoom (scale) the image in the magnifying glass
        /// </summary>
        public double MagnifierZoom
        {
            get
            {
                return (double)GetValue(MagnifierZoomProperty);
            }
            set
            {
                // make sure the value is always in range
                double newValue = value;
                if (value > this.MagnifierZoomRange.X) newValue = this.MagnifierZoomRange.X;
                if (value < this.MagnifierZoomRange.Y) newValue = this.MagnifierZoomRange.Y;
                SetValue(MagnifierZoomProperty, newValue); ;
                this.magnifyingGlass.ZoomValue = newValue;

                // Make sure that there is actually something to magnify
                if (this.ImageToMagnify.Source != null && this.ImageToDisplay.ActualWidth > 0) 
                if ( this.ImageToDisplay.ActualWidth > 0) 
                    this.magnifyingGlass.Redraw(Calculations.CorrectGetPosition(this), Calculations.CorrectGetPosition(this.ImageToDisplay), this.ImageToDisplay.ActualWidth, this.ImageToDisplay.ActualHeight, canvasToMagnify);
            }
        }

        public static DependencyProperty MagnifierZoomDeltaProperty = DependencyProperty.Register("MagnifierZoomDelta", typeof(double), typeof(MarkableImageCanvas));
        /// <summary>
        /// The delta value to increase or decrease our magnifying glass zoom
        /// </summary>
        public double MagnifierZoomDelta
        {
            get
            {
                return (double)GetValue(MagnifierZoomDeltaProperty);
            }
            set
            {
                SetValue(MagnifierZoomDeltaProperty, value);
            }
        }

        public static DependencyProperty TranslateXProperty = DependencyProperty.Register("TranslateX", typeof(double), typeof(MarkableImageCanvas));
        /// <summary>
        /// The amount we should translate the image horizontally on the X axis
        /// </summary>
        public double TranslateX
        {
            get
            {
                return (double)GetValue(TranslateXProperty);
            }
            set
            {
                SetValue(TranslateXProperty, value);
                TransformChanged();
                MarkersRefresh();
            }
        }

        public static DependencyProperty TranslateYProperty = DependencyProperty.Register("TranslateY", typeof(double), typeof(MarkableImageCanvas));
        /// <summary>
        /// The amount we should translate the image vertically on the Y axis
        /// </summary>
        public double TranslateY
        {
            get
            {
                return (double)GetValue(TranslateYProperty);
            }
            set
            {
                SetValue(TranslateYProperty, value);
                TransformChanged();
                MarkersRefresh();
            }
        }

        public static DependencyProperty IsMagnifyingGlassVisibleProperty = DependencyProperty.Register("IsMagnifyingGlassVisible", typeof(bool), typeof(MarkableImageCanvas));
        /// <summary>
        /// Sets whether the magnifying glass is generally visible or hidden, and returns its state
        /// </summary>       
        public bool IsMagnifyingGlassVisible
        {
            get
            {
                return (bool)GetValue(IsMagnifyingGlassVisibleProperty);
            }
            set
            {
                SetValue(IsMagnifyingGlassVisibleProperty, value);
                this.magnifyingGlass.isVisibilityDesired = value;
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
        #endregion
        
        #region Private variables and constants

        // Transform variables
        private TransformGroup _trGroup;
        private TranslateTransform _trTranslate;
        private ScaleTransform _trScale;

        // Bookmarked pan and zoom setting (initially its default zoomed out normal pan state)
        private Point bookmarkedTranslatedPoint = new Point(0, 0); 
        private Point bookmarkedScalePoint = new Point(1, 1);

        // Mouse and position states used to discriminate clicks from drags)
        private UIElement _mouseDownSender;
        private DateTime _mouseDownTime;
        private Point _mouseDownLocation;
        private Point _previousLocation; 

        // Marks constants 
        private const int MARK_DIAMETER = 10;
        private const int MARK_STROKETHICKNESS = 2;
        private Brush MARK_STROKEBRUSH = (SolidColorBrush)new BrushConverter().ConvertFromString(Constants.StandardColour);
        private SolidColorBrush MARK_FILLBRUSH = new SolidColorBrush(System.Windows.Media.Color.FromArgb(2, 0, 0, 0));

        // Zooming constants
        private const double ZOOM_MAXIMUM = 10;   // Maximum amount of zoom
        private const double ZOOM_MAXIMUM_UPPERBOUND = 50;   // Maximum amount of zoom
        private const double ZOOM_MINIMUM = 1;   // Minimum amount of zoom
        private const double ZOOM_DELTA = 1.2;   // Amount to scale on each increment

        // The Magnifying Glass  and constants
        private MagnifyingGlass magnifyingGlass = new MagnifyingGlass();
        const double MAGNIFYING_MAXZOOM = 15;  // Max is a smaller number
        const double MAGNIFYING_MINZOOM = 100; // Min is the larger number
        const double MAGNIFYING_DELTA = 2;
        const double MAGNIFYING_VALUE = 60;

        // The canvas to magnify contains both an image and any marks, 
        // so that the magnifying lens will contain all correct contents
        private Canvas canvasToMagnify = new Canvas();
        
        #endregion

        #region MetaTag Event Raising and Handling
        // The RaiseMetaTagEvent handler
        public event EventHandler<MetaTagEventArgs> RaiseMetaTagEvent;

        // Wrap event invocations inside a protected virtual method
        // to allow derived classes to override the event invocation behavior
        protected virtual void OnRaiseMetaTagEvent(MetaTagEventArgs e)
        {
            // Make a temporary copy of the event to avoid possibility of
            // a race condition if the last subscriber unsubscribes
            // immediately after the null check and before the event is raised.
            EventHandler<MetaTagEventArgs> handler = RaiseMetaTagEvent;

            // Event will be null if there are no subscribers. Otherwise, raise the event
            // Also, if we wanted to modify anything in the metatag, we would do it here, e.g.,  e.metaTag.Description = "foo"
            if (handler != null) handler(this, e);
        }
        #endregion
      
        #region Initialization

        /// <summary>
        /// a canvas that can be zoomed and panned, that contains a magnifying glass, and that can be use to add and delete marks
        /// where marks are described by MetaTags.
        /// </summary>
        public MarkableImageCanvas()
        {

            // Initialize the list of MetaTags
            this.MetaTags = new List<MetaTag>();

            // Set up some initial canvas properties and event handlers
            this.Background = Brushes.Black;  
            this.ClipToBounds = true;
            this.Focusable = true;
            this.SizeChanged += new SizeChangedEventHandler(OnMarkableImageCanvas_SizeChanged);
            ResetMaxZoom();
            this.MaxZoomUpperBound = ZOOM_MAXIMUM_UPPERBOUND;
            
            // Set up some initial image properites for the image to magnify
            this.ImageToMagnify = new Image(); 
            this.ImageToMagnify.HorizontalAlignment = HorizontalAlignment.Left;
            this.ImageToMagnify.VerticalAlignment = VerticalAlignment.Top;
            Canvas.SetLeft(ImageToMagnify, 0);
            Canvas.SetTop(ImageToMagnify, 0);

            // add the image to the magnification canvas
            canvasToMagnify.Children.Add(this.ImageToMagnify);

            // Set up some initial image properites and event handlers 
            this.ImageToDisplay = new Image();
            this.ImageToDisplay.HorizontalAlignment = HorizontalAlignment.Left;
            this.ImageToDisplay.VerticalAlignment = VerticalAlignment.Top;
            this.ImageToDisplay.SizeChanged += new SizeChangedEventHandler(OnImgToDisplay_SizeChanged);

            // Position and add the image to the canvas
            Canvas.SetLeft(ImageToDisplay, 0);
            Canvas.SetTop(ImageToDisplay, 0);
            this.Children.Add(ImageToDisplay);

            // Set up the magnifying glass
            this.magnifyingGlass.MarkableCanvasParent = this; // A reference to this so we can access the markable Canvas state
            this.MagnifierZoomRange = new Point(MAGNIFYING_MINZOOM, MAGNIFYING_MAXZOOM);
            this.MagnifierZoomDelta = ZOOM_DELTA;
            this.MagnifierZoom = MAGNIFYING_VALUE;
            
            this.magnifyingGlass.Hide();
            Canvas.SetZIndex(magnifyingGlass, 1000); // Should always be in front
            this.Children.Add(magnifyingGlass);

            // Set up the transforms
            _trTranslate = new TranslateTransform(0, 0);
            _trScale = new ScaleTransform(1, 1);

            _trGroup = new TransformGroup();
            _trGroup.Children.Add(_trScale);
            _trGroup.Children.Add(_trTranslate);

            // These properties hold the scale and x/y translate values
            this.Zoom = 1;
            this.TranslateX = 0;
            this.TranslateY = 0;
            ImageToDisplay.RenderTransform = _trGroup;

            //  Event handlers for image interaction: mouse handling for marking, zooming, panning, scroll wheel, etc.
            this.ImageToDisplay.MouseDown += new MouseButtonEventHandler(OnImage_MouseDown);
            this.MouseMove += new MouseEventHandler(OnImage_MouseMove);

            this.ImageToDisplay.MouseUp += new MouseButtonEventHandler(OnImage_MouseUp);
            this.ImageToDisplay.MouseWheel += new MouseWheelEventHandler(OnImage_MouseWheel);
            this.PreviewKeyDown += new KeyEventHandler(OnImage_PreviewKeyDown);
   
            this.MouseLeave += new MouseEventHandler(OnImage_MouseLeave);
        }

        #endregion

        #region Public methods
        /// <summary>
        /// Add a metatag to the MetaTags list. Also refresh the display to reflect the current contents of the list
        /// </summary>
        /// <param name="mt"></param>
        public void ResetMaxZoom()
        {
            this.MaxZoom = ZOOM_MAXIMUM;
        }
        public void AddMetaTag(MetaTag mt)
        {
            this.MetaTags.Add(mt);
            this.MarkersRefresh();
        }

        /// <summary>
        /// Remove and then redraw all the markers as defined by the MetaTag list 
        /// </summary>
        public void MarkersRefresh()
        {
            MarkersRemove(this);
            MarkersRemove(this.canvasToMagnify);
            if (null != this.ImageToDisplay)
            {
                MarkersDraw(this, this.ImageToDisplay.RenderSize, true);
                MarkersDraw(this.canvasToMagnify, this.canvasToMagnify.RenderSize, false);
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
        #endregion

        #region Public methods: Bookmarking the image's zoom/ pan level

        // A user may want to flip between completely zoomed out / normal pan settings
        // and a saved zoom / pan setting that focuses in on a particular region.
        // To do this, we  save / restore the zoom pan settings of a particular view,
        // or return to the default zoom/pan.

        // Save the current zoom / pan levels as a bookmar
        public void BookmarkSaveZoomPan()
        {
            this.bookmarkedTranslatedPoint.X = _trTranslate.X;
            this.bookmarkedTranslatedPoint.Y = _trTranslate.Y;
            this.bookmarkedScalePoint.X = _trScale.ScaleX;
            this.bookmarkedScalePoint.Y = _trScale.ScaleY;
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
            this._trScale.ScaleX = this.bookmarkedScalePoint.X;
            this._trScale.ScaleY = this.bookmarkedScalePoint.Y;

            this._trTranslate.X = this.bookmarkedTranslatedPoint.X;
            this._trTranslate.Y = this.bookmarkedTranslatedPoint.Y;

            this.MarkersRefresh();
        }

        // Return to the zoomed out level, with no panning
        public void BookmarkZoomOutAllTheWay()
        {
            this._trScale.ScaleX = 1;
            this._trScale.ScaleY = 1;

            this._trTranslate.X = 0;
            this._trTranslate.Y = 0;

            this.MarkersRefresh();
        }
        #endregion 

        #region Event Handlers: Resizing
        // Whenever the canvas size changes, resize the image
        private void OnMarkableImageCanvas_SizeChanged(object sender, SizeChangedEventArgs e)
        {
            this.ImageToDisplay.Width = this.ActualWidth;
            this.ImageToDisplay.Height = this.ActualHeight;
            BookmarkClearZoomPan(); // We clear the bookmark (if any) as it will no longer be correct.
        }

        // Whenever the image size changes, refresh the markers so they appear in the correct place
        private void OnImgToDisplay_SizeChanged(object sender, SizeChangedEventArgs e)
        {
            this.MarkersRefresh();
        }

        #endregion

        #region Event Handlers: Mouse Input

        // On Mouse down, record the location, who sent it, and the time.
        // We will use this information on move and up events to discriminate between 
        // panning/zooming vs. marking. 
        private void OnImage_MouseDown(object sender, MouseButtonEventArgs e)
        {
            this._previousLocation = e.GetPosition(this);
            if (e.LeftButton == MouseButtonState.Pressed)
            {
                this._mouseDownLocation = e.GetPosition(this.ImageToDisplay);
                this._mouseDownSender = (UIElement)sender;
                this._mouseDownTime = DateTime.Now;
            }
        }

        // If we move the mouse with the left mouse button press, translate the image
        private void OnImage_MouseMove(object sender, MouseEventArgs e)
        {
            const int WAIT_TIME = 100;
            // The visibility of the magnifying glass depends on whether the mouse is over the image
            this.SetMagnifyingGlassVisibility(e.GetPosition(this));

            // Calculate how much time has passed since the mouse down event?
            TimeSpan timeSinceDown = DateTime.Now - this._mouseDownTime;

            // If at least WAIT_TIME milliseconds has passed
            if (timeSinceDown.TotalMilliseconds >= WAIT_TIME)
            {
                //Point location = e.GetPosition(this.imgToDisplay);   // Get the current location
                Point location = e.GetPosition(this);   // Get the current location

                // If the left button is pressed, translate (pan) across the image 
                //  Also hide the magnifying glass so it won't be distracting
                if (e.LeftButton == MouseButtonState.Pressed)
                {
                    this.Cursor = Cursors.ScrollAll;    // Change the cursor to a panning cursor
                    TranslateImage(location);
                    this.magnifyingGlass.Hide ();
                }
                else
                {
                    // Update the magnifying glass
                    canvasToMagnify.Width = this.ImageToMagnify.ActualWidth;      // Make sure that the canvas is the same size as the image
                    canvasToMagnify.Height = this.ImageToMagnify.ActualHeight;
                    this.magnifyingGlass.Redraw(Calculations.CorrectGetPosition(this), Calculations.CorrectGetPosition(this.ImageToDisplay), this.ImageToDisplay.ActualWidth, this.ImageToDisplay.ActualHeight, canvasToMagnify);
                }
                this._previousLocation = location;
               
            }
        }

        // The magnifying glass  is visible only if the current mouse position is over the image. 
        private void SetMagnifyingGlassVisibility(Point mousePosition)
        {
            // The the actual (transformed) bounds of the image
            Point transformedSize = this._trGroup.Transform(new Point (this.ImageToDisplay.ActualWidth, this.ImageToDisplay.ActualHeight));
            bool mouseOverImage = (mousePosition.X <= transformedSize.X && mousePosition.Y <= transformedSize.Y);
           if (mouseOverImage) 
           {
               this.magnifyingGlass.ShowIfIsVisibilityDesired();
           } else 
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
            if (e.LeftButton == MouseButtonState.Released
                && (sender == this._mouseDownSender)
                && (this._mouseDownLocation - location).Length <= 2.0)
            {
                
                TimeSpan timeSinceDown = DateTime.Now - this._mouseDownTime;
                if (timeSinceDown.TotalMilliseconds < 200)
                {
                    // Get the current point, and create a marker on it.

                    Point p = e.GetPosition(ImageToDisplay);
                    // Debug.Print(p.ToString());
                    p = Calculations.convertPointToRatio(p, ImageToDisplay.ActualWidth, ImageToDisplay.ActualHeight);
                    MetaTag mt = new MetaTag ();
                    mt.Point = p;
                    //mt.Brush = Brushes.Green;
                    
                    // We don't actually add this MetaTag to our MetaTags list now.
                    // Rather, we raise an event informing the calling application about it.
                    // That application can choose to ignore it (in which case it doesn't get added),
                    // or can adjust its values and then add it to the MetaTags list via a call to AddMetaTag 
                    OnRaiseMetaTagEvent(new MetaTagEventArgs (mt, true));
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
                bool zoomIn = (e.Delta > 0); // Zooming in if delta is positive, else zooming out
                ScaleImage(location, zoomIn);
            }
        }

        // Hide the magnifying glass when the mouse cursor leaves the image
        private void OnImage_MouseLeave(object sender, MouseEventArgs e)
        {
            this.magnifyingGlass.Hide ();
        }

        #endregion  

        #region Event Handlers: Keyboard Shortcuts
        // If its < or > key zoom out or in around the mouse point.
        private void OnImage_PreviewKeyDown(object sender, KeyEventArgs e)
        {
            //if (this.dbData.ImageCount == 0) return; // No images are loaded, so don't try to interpret any keys

            // Depending on the key, take the appropriate action
            switch (e.Key)
            {

                // zoom in
                case Key.OemPeriod:
                    lock (this.ImageToDisplay)
                    {
                        Point location = Mouse.GetPosition(this.ImageToDisplay);
                        if (location.X > this.ImageToDisplay.ActualWidth || location.Y > this.ImageToDisplay.ActualHeight) break; // Ignore points if mouse is off the image
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

        #endregion

        #region Scaling, Transforms and other calculations

        // Given the mouse location on the image, translate the image
        // This is normally called from a left mouse move event
        private void TranslateImage(Point mouse_location)
        {
            // Get the center point on the image
            Point center = this.PointFromScreen(
                this.ImageToDisplay.PointToScreen(new Point(
                    this.ImageToDisplay.Width / 2.0, this.ImageToDisplay.Height / 2.0)));

            // Calculate the delta position from the last location relative to the center
            double newX = center.X + mouse_location.X - this._previousLocation.X;
            double newY = center.Y + mouse_location.Y - this._previousLocation.Y;

            // get the translated image width
            double imageWidth = this.ImageToDisplay.Width * this._trScale.ScaleX;
            double imageHeight = this.ImageToDisplay.Height * this._trScale.ScaleY;

            // Limit the delta position so that the image stays on the screen
            if (newX - imageWidth / 2.0 >= 0.0)
                newX = imageWidth / 2.0;
            else if (newX + imageWidth / 2.0 <= this.ActualWidth)
                newX = this.ActualWidth - imageWidth / 2.0;

            if (newY - imageHeight / 2.0 >= 0.0)
                newY = imageHeight / 2.0;
            else if (newY + imageHeight / 2.0 <= this.ActualHeight)
                newY = this.ActualHeight - imageHeight / 2.0;

            // Translate the canvas and redraw the markers
            this._trTranslate.X += (newX - center.X);
            this._trTranslate.Y += (newY - center.Y);

            this.MarkersRefresh();
        }

        // Scale the image around the given image location point, where we are zooming in if
        // zoomIn is true, and zooming out if zoomIn is false
        public void ScaleImage(Point location, bool zoomIn)
        {
            //if (this.imgToDisplay.Source == null) return; // Nothing to zoom onto, so get out of here

            // Get out of here if we are already at our maximum or minimum scaling values 
            // while zooming in or out respectively 
            if ((zoomIn && this._trScale.ScaleX >= this.MaxZoom) ||
                 (!zoomIn && this._trScale.ScaleX <= ZOOM_MINIMUM)) return;

            // We will scale around the current point
            Point beforeZoom = this.PointFromScreen(this.ImageToDisplay.PointToScreen(location));

            // Calculate the scaling factor during zoom ins or out. Ensure that we keep within our
            // maximum and minimum scaling bounds. 
            if (zoomIn) // We are zooming in
            {
                // Calculate the scaling factor
                this._trScale.ScaleX *= ZOOM_DELTA;   // Calculate the scaling factor
                this._trScale.ScaleY *= ZOOM_DELTA;

                // Make sure we don't scale beyond the maximum scaling factor
                this._trScale.ScaleX = Math.Min(this.MaxZoom, this._trScale.ScaleX);
                this._trScale.ScaleY = Math.Min(this.MaxZoom, this._trScale.ScaleY);
            }
            else  // We are zooming out. 
            {
                // Calculate the scaling factor
                this._trScale.ScaleX /= ZOOM_DELTA;
                this._trScale.ScaleY /= ZOOM_DELTA;

                // Make sure we don't scale beyond the minimum scaling factor
                this._trScale.ScaleX = Math.Max(ZOOM_MINIMUM, this._trScale.ScaleX);
                this._trScale.ScaleY = Math.Max(ZOOM_MINIMUM, this._trScale.ScaleY);

                // if there is no scaling, reset translations
                if (this._trScale.ScaleX == 1.0 && this._trScale.ScaleY == 1.0)
                {
                    this._trTranslate.X = 0.0;
                    this._trTranslate.Y = 0.0;
                }
            }

            Point afterZoom = this.PointFromScreen(this.ImageToDisplay.PointToScreen(location));

            // Scale the image, and at the same time translate it so that the 
            // point in the image under the cursor stays there
            double imageWidth = this.ImageToDisplay.Width * this._trScale.ScaleX;
            double imageHeight = this.ImageToDisplay.Height * this._trScale.ScaleY;

            Point center = this.PointFromScreen(
                this.ImageToDisplay.PointToScreen(new Point(
                    this.ImageToDisplay.Width / 2.0, this.ImageToDisplay.Height / 2.0)));

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

            this._trTranslate.X += (newX - center.X);
            this._trTranslate.Y += (newY - center.Y);
            this.MarkersRefresh();
        }

        private void TransformChanged()
        {
            if (this == null) return;
            try
            {
                _trScale.ScaleX = this.Zoom;
                _trScale.ScaleY = this.Zoom;
                _trTranslate.X = this.TranslateX;
                _trTranslate.Y = this.TranslateY; ;
            }
            catch
            {
                Debug.Print("Catch: In MarkableCanvas:TransformChanged as image cannot be transformed");
            }
        }
        #endregion

        #region Drawing Markers

        // From the list of points, redraw all ellipses
        private void MarkersDraw(Canvas canvas, Size newSize, bool doTransform)
        {
            foreach (MetaTag mTag in this.MetaTags)
            {
                Canvas markerCanvas = MarkerDraw(mTag, newSize, doTransform);
                canvas.Children.Add(markerCanvas);
            }
        }

        // Create a marker of a given size and set its position
        private Canvas MarkerDraw(MetaTag mtag, Size size, bool doTransform)
        {
            double max_diameter = 0;
            Canvas markerCanvas = new Canvas();
            markerCanvas.MouseRightButtonUp += new MouseButtonEventHandler(marker_MouseRightButtonUp);
            markerCanvas.MouseWheel += new MouseWheelEventHandler(OnImage_MouseWheel); // Make the mouse wheel work over marks as well as the image

            if (mtag.Label.Trim() == "")
                markerCanvas.ToolTip = null;
            else
                markerCanvas.ToolTip = mtag.Label;
            markerCanvas.Tag = mtag;

            // Create a marker, using properties as defined in the metatag as needed
            Ellipse ellipse = new Ellipse();
            ellipse.Width = MARK_DIAMETER;
            ellipse.Height = MARK_DIAMETER;
            ellipse.StrokeThickness = MARK_STROKETHICKNESS;
            

            ellipse.Stroke = mtag.Brush;
            ellipse.Fill = MARK_FILLBRUSH;  // Should be a transparent fill so it can get hit events

            // Draw another Ellipse as a black outline around it
            Ellipse ellipse1 = new Ellipse();
           
            ellipse1.Stroke = Brushes.Black;
            ellipse1.Width = MARK_DIAMETER + 1;
            ellipse1.Height = MARK_DIAMETER + 1;
            ellipse1.StrokeThickness = 1;
            max_diameter = ellipse1.Width;

            // And another Ellipse as a white outline around it
            Ellipse outline2 = new Ellipse();
           
            outline2.Stroke = Brushes.Beige;
            outline2.Width = MARK_DIAMETER + 2;
            outline2.Height = MARK_DIAMETER + 2;
            outline2.StrokeThickness = 1;
            max_diameter = ellipse1.Width;

            Ellipse glow = null;
            if (mtag.Emphasise)
            {
                glow = new Ellipse();
                glow.Width = ellipse1.Width + 9;
                glow.Height = ellipse1.Height + 9;
                glow.StrokeThickness = 3;
                glow.Stroke = ellipse.Stroke;
                glow.Opacity = .5;
                max_diameter = glow.Width;  
            }

            Label label = new Label();
            if (mtag.Annotate)
            {
                label.Content = mtag.Label;
                label.Opacity = .6;
                label.Background = Brushes.White;
                label.Padding = new Thickness(0, 0, 0, 0);
                label.Margin = new Thickness(0, 0, 0, 0);
            }

            if (glow != null)
            {
                Canvas.SetLeft(glow, 0);
                Canvas.SetTop(glow, 0);
                markerCanvas.Children.Add(glow);
            }

            markerCanvas.Width = max_diameter;
            markerCanvas.Height = max_diameter;
            markerCanvas.Children.Add(ellipse);
            markerCanvas.Children.Add(ellipse1);
            markerCanvas.Children.Add(outline2);
            if (mtag.Annotate) markerCanvas.Children.Add(label);

            double position;
            position = (markerCanvas.Width - ellipse.Width) / 2; 
            Canvas.SetLeft(ellipse,position);
            Canvas.SetTop(ellipse, position);

            position = (markerCanvas.Width - ellipse1.Width) / 2;
            Canvas.SetLeft(ellipse1, position);
            Canvas.SetTop(ellipse1, position);

            position = (markerCanvas.Width - outline2.Width) / 2;
            Canvas.SetLeft(outline2, position);
            Canvas.SetTop(outline2, position);

            if (mtag.Annotate)
            {
                position = (markerCanvas.Width / 2) + (outline2.Width / 2);
                Canvas.SetLeft(label, position);
                Canvas.SetTop(label, markerCanvas.Height / 2);
            }

            // Get the point from the metatag, and convert it so that the marker will be in the right place
            Point pt = Calculations.convertRatioToPoint(mtag.Point, size.Width, size.Height);
            if (doTransform) pt = _trGroup.Transform(pt);
            Canvas.SetLeft(markerCanvas, pt.X - markerCanvas.Width / 2);
            Canvas.SetTop(markerCanvas, pt.Y - markerCanvas.Height / 2);
            Canvas.SetZIndex(markerCanvas, 0);
            markerCanvas.MouseDown +=new MouseButtonEventHandler(OnImage_MouseDown);
            markerCanvas.MouseMove +=new MouseEventHandler(OnImage_MouseMove);
            markerCanvas.MouseUp += new MouseButtonEventHandler(OnImage_MouseUp);
            return markerCanvas;
        }

        // Remove a marker on a right mouse button up event
        private void marker_MouseRightButtonUp(object sender, MouseButtonEventArgs e)
        {
            Canvas marker = (Canvas)sender;
            MetaTag mtag = (MetaTag)marker.Tag;
            this.MetaTags.Remove(mtag);
            OnRaiseMetaTagEvent(new MetaTagEventArgs(mtag, false));
            this.MarkersRefresh();
        }

        // remove all markers from the canvass
        private void MarkersRemove(Canvas canvas)
        {
            for (int index = canvas.Children.Count - 1; index >= 0; index--)
            {
                if (canvas.Children[index] is Canvas && canvas.Children[index] != this.magnifyingGlass )
                {
                    canvas.Children.RemoveAt(index);
                }
            }
        }


        #endregion
    }

    #region CLASSES: METATAG And MetagEventArgs
    /// <summary>
    /// The MetaTag event argument contains 
    /// - a reference to the MetaTag
    /// - an indication if this is a new just created tag (if true), or if its been deleted (if false)
    /// </summary>
    public class MetaTagEventArgs : EventArgs
    {
        private MetaTag m_Tag;
        private bool m_IsNew;

        /// <summary>
        /// The MetaTag event argument contains 
        /// - a reference to the MetaTag
        /// - an indication if this is a new just created tag (if true), or if its been deleted (if false)
        /// </summary>
        public MetaTagEventArgs(MetaTag tag, bool isNew)
        {
            m_Tag = tag;
            m_IsNew = isNew;
        }

        /// <summary>
        /// - a reference to the MetaTag
        /// </summary>
        public MetaTag metaTag
        {
            get { return m_Tag; }
            set { m_Tag = value; }
        }

        /// <summary>
        /// - an indication if this is a new just created tag (if true), or if its been deleted (if false)
        /// </summary>
        public bool IsNew
        {
            get { return m_IsNew; }
            set { m_IsNew = value; }
        }
    }
    #endregion

    #region INTERNAL CLASS: MagnifyingLens
    internal class MagnifyingGlass:Canvas
    {
        const int HANDLE_START = 200;
        const int HANDLE_END = 250;
        const int LENS_DIAMETER = 250;
        internal double ZoomValue { get; set; }
        internal Point ZoomRange { get; set; }
        internal bool isVisibilityDesired = false;
        internal MarkableImageCanvas MarkableCanvasParent;
        double angleMG = 0;     // current angle of the entire magnifying glass
        double angleLens = 0;   // current angle of the lens only
        private Ellipse magnifierEllipse;
        private Canvas lensCanvas;

        #region Initialization
        internal MagnifyingGlass()
        {
            this.IsHitTestVisible = false;
            this.HorizontalAlignment = HorizontalAlignment.Left;
            this.VerticalAlignment = VerticalAlignment.Top;
            this.Visibility = Visibility.Collapsed;
            this.ZoomValue = 60;

            // Create the handle of the magnifying glass
            Line lineHandle = new Line();
            lineHandle.StrokeThickness = 5;
            lineHandle.X1 = HANDLE_START;
            lineHandle.Y1 = HANDLE_START;
            lineHandle.X2 = HANDLE_END;
            lineHandle.Y2 = HANDLE_END;
            LinearGradientBrush lgb1 = new LinearGradientBrush ();
            lgb1.StartPoint = new Point(0.78786, 1);
            lgb1.EndPoint = new Point(1, 0.78786);
            lgb1.GradientStops.Add (new GradientStop (Colors.DarkGreen, 0));
            lgb1.GradientStops.Add (new GradientStop (Colors.LightGreen, 0.9));
            lgb1.GradientStops.Add (new GradientStop (Colors.Green, 1));
            lineHandle.Stroke = lgb1;
            this.Children.Add(lineHandle);
            
            // Create the lens of the magnifying glass
            lensCanvas = new Canvas ();
            this.Children.Add(lensCanvas);

            // The lens will contain a white backgound
            Ellipse ellipseWhite = new Ellipse ();
            ellipseWhite.Width = LENS_DIAMETER;
            ellipseWhite.Height = LENS_DIAMETER;
            ellipseWhite.Fill = Brushes.White;
            lensCanvas.Children.Add (ellipseWhite);

            magnifierEllipse = new Ellipse ();
            magnifierEllipse.Width = LENS_DIAMETER;
            magnifierEllipse.Height = LENS_DIAMETER;
            magnifierEllipse.StrokeThickness = 3;

            // Fill the Ellipse
            VisualBrush vb = new VisualBrush();
            vb.ViewboxUnits = BrushMappingMode.Absolute;
            vb.Viewbox = new Rect(0, 0, 50, 50);
            vb.ViewportUnits = BrushMappingMode.RelativeToBoundingBox;
            vb.Viewport = new Rect(0, 0, 1, 1);           
            magnifierEllipse.Fill = vb;

            // Outline the Ellipse
            LinearGradientBrush lgb2 = new LinearGradientBrush();
            lgb2.StartPoint = new Point(0,0);
            lgb2.EndPoint = new Point(0,1);
            ColorConverter cc = new ColorConverter();
            lgb2.GradientStops.Add(new GradientStop((Color)cc.ConvertFrom("#AAA"), 0));
            lgb2.GradientStops.Add(new GradientStop((Color)cc.ConvertFrom("#111"), 1));
            magnifierEllipse.Stroke = lgb2;
            lensCanvas.Children.Add(magnifierEllipse);

            Ellipse e3 = new Ellipse();
            Canvas.SetLeft(e3, 2);
            Canvas.SetTop(e3, 2);
            e3.StrokeThickness = 4;
            e3.Width = LENS_DIAMETER - 4;
            e3.Height = LENS_DIAMETER - 4;
            lensCanvas.Children.Add(e3);

            //  The cross-hairs
            Line lineCrosshair1 = new Line();
            lineCrosshair1.StrokeThickness = .25;
            lineCrosshair1.X1 = 5;
            lineCrosshair1.Y1 = LENS_DIAMETER / 2;
            lineCrosshair1.X2 = LENS_DIAMETER - 5;
            lineCrosshair1.Y2 = LENS_DIAMETER / 2;
            lineCrosshair1.Stroke = Brushes.Black;
            lineCrosshair1.Opacity = 0.5;
               
            lensCanvas.Children.Add(lineCrosshair1);

            Line lineCrosshair2 = new Line();
            lineCrosshair2.StrokeThickness = .25;
            lineCrosshair2.X1 = LENS_DIAMETER / 2;
            lineCrosshair2.Y1 = 5 ;
            lineCrosshair2.X2 = LENS_DIAMETER / 2;
            lineCrosshair2.Y2 = LENS_DIAMETER - 5;
            lineCrosshair2.Stroke = Brushes.Black;
            lineCrosshair2.Opacity = 0.5;
            lensCanvas.Children.Add(lineCrosshair2);
        }
        #endregion

        #region Methods
        static bool notYetRedrawn = true;
        internal void Redraw(Point mousePoint, Point imageControlPoint, double actualWidth, double actualHeight, Canvas canvasToMagnify)
        {
            // Abort if we don't have an image to magnify
            if (canvasToMagnify == null) return;
            if (this.MarkableCanvasParent.ImageToMagnify.Source == null) return;
            notYetRedrawn = false;

            // Abort if the magnifying glass visiblity is not visible, as there is no point doing all this work
            if (this.Visibility != Visibility.Visible) return;

            // Given a mouse position over the displayed image, we need to know where the equivalent position is over the magnified image (which is a different size)
            // We do this by calculating the ratio of the point over the displayed image, and then using that to calculate the position over the cached image
            Point ptImgCtl = imageControlPoint;
            Point ptRatioImageCtl = Calculations.convertPointToRatio(ptImgCtl, actualWidth, actualHeight);
            Point ptImageUnaltered = Calculations.convertRatioToPoint(ptRatioImageCtl, canvasToMagnify.Width, canvasToMagnify.Height);
            
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
            viewBox.X = ptImageUnaltered.X - xoffset;
            viewBox.Y = ptImageUnaltered.Y - yoffset;
            vbrush.Viewbox = viewBox;

            // Finally, fill the magnifying glass with this brush
            this.magnifierEllipse.Fill = vbrush;

            // Now, we need to calculate where to put the magnifying glass, and whether we should rotate it 
            // The idea is that we will start rotating when the magnifying glass is near the top and the left of the display
            // The critical distance is size for the Y direction, and somewhat larger than size for the X direction (as we have to start
            // rotating earlier so it doesn't get clipped). xsize is somewhat arbitrary, i.e., determined by trial and error
            const double EDGE_THRESHOLD = 250; // the EDGE boundary where we should rotate the magnifying glass
            double new_angleMG = angleMG;  // the new angle we need to rotate the magnifying glass to
            // positions of edges where we shold change the angle. 

            double left_edge = EDGE_THRESHOLD;
            double right_edge = this.MarkableCanvasParent.ImageToDisplay.ActualWidth - EDGE_THRESHOLD;
            double top_edge = EDGE_THRESHOLD;
            double bottom_edge = this.MarkableCanvasParent.ImageToDisplay.ActualHeight - EDGE_THRESHOLD;
            double canvasheight = this.MarkableCanvasParent.ImageToDisplay.ActualHeight;
            double canvaswidth = this.MarkableCanvasParent.ImageToDisplay.ActualWidth;

             
            // Specify the magnifying glass angle needed
            // In various cases, several angles can work
            // so choose a new angle whose difference from the existing angle  will cause the least amount of animation 
            // BUG: Could improve this. There are cases where it rotates to the  non-optimal angle, but couldn't figure out how to fix it.
            if ((mousePoint.X < left_edge) && (mousePoint.Y < top_edge))  new_angleMG = 180;        // upper left corner
            else if ((mousePoint.X < left_edge) && (mousePoint.Y > bottom_edge) ) new_angleMG = 90; // lower left corner
            else if ((mousePoint.X < left_edge)) new_angleMG = AdjustAngle(angleMG, 90, 180);       // middle left edge 
            else if ((mousePoint.X > right_edge) && (mousePoint.Y < top_edge)) new_angleMG = 270;   // upper right corner
            else if ((mousePoint.X > right_edge) && (mousePoint.Y > bottom_edge)) new_angleMG = 0;  // lower right corner
            else if ((mousePoint.X > right_edge)) new_angleMG = AdjustAngle(angleMG, 270, 0);       // middle right edge
            else if ((mousePoint.Y < top_edge)) new_angleMG = AdjustAngle(angleMG, 270, 180);       // top edge, middle
            else if ((mousePoint.Y > bottom_edge)) new_angleMG = AdjustAngle(angleMG, 0, 90);       // bottom edge, middle
            else new_angleMG = angleMG;                                                             // center; any angle will work

            
            // If the angle has changed, animate the magnifying glass and its contained image to the new angle
            if (angleMG != new_angleMG)
            {
                double new_angleLens;
                double uncorrected_new_angleLens;

                // Correct the rotation in those cases where it would turn the long way around. 
                // Note that the new lens angle correction is hard coded rather than calculated, as it works. 
                // Easier than it out :-) 
                uncorrected_new_angleLens = -new_angleMG;
                if (angleMG == 270 && new_angleMG == 0)
                {
                    angleMG = -90;
                    new_angleLens = -360;//-new_angleMG; // We subtract the rotation that the mag glass is rotating to counter that rotational effect
                }
                else if (angleMG == 0 && new_angleMG == 270)
                {
                    angleMG = 360;
                    new_angleLens = 90; // We subtract the rotation that the mag glass is rotating to counter that rotational effect
                }
                else
                {
                    new_angleLens = uncorrected_new_angleLens; // We subtract the rotation that the mag glass is rotating to counter that rotational effect
                }

                // The time of the animation
                Duration duration = new Duration(new TimeSpan(0, 0, 0, 0, 500)); // allow animations to take a 1/3 second

                // Rotate the lens within the magnifying glass
                RotateTransform rotateTransformLens = new RotateTransform(angleMG, size / 2, size / 2);
                DoubleAnimation animLens = new DoubleAnimation(angleLens, new_angleLens, duration);
                rotateTransformLens.BeginAnimation(RotateTransform.AngleProperty, animLens);
                lensCanvas.RenderTransform = rotateTransformLens;

                // Now rotate and position the entire mag. glass
                RotateTransform rotateTransformMG = new RotateTransform(angleMG, size, size);
                DoubleAnimation animMG = new DoubleAnimation(angleMG, new_angleMG, duration);
                rotateTransformMG.BeginAnimation(RotateTransform.AngleProperty, animMG);
                this.RenderTransform = rotateTransformMG;

                // Save the angle so we can compare it on the next iteration. If any of them are 360, swap it to 0
                if (new_angleMG % 360 == 0) new_angleMG = 0;
                if (new_angleLens % 360 == 0) uncorrected_new_angleLens = 0;
                angleMG = new_angleMG;
                angleLens = uncorrected_new_angleLens; 
            }
            Canvas.SetLeft(this, mousePoint.X - size);
            Canvas.SetTop(this, mousePoint.Y - size);
        }

        // Given the old angle, and up to two desired angles,
        // return the  current angle if it matches one of the desired angle, 
        // or the the desired angle that is closest to the angle in degrees
        private double AdjustAngle(double old_angle, double angle1,double angle2)
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

        //Hiding the magnifying glass does not affect its visibility state
        internal void Hide()
        {
            this.Visibility = Visibility.Collapsed;
        }

        internal void ShowIfIsVisibilityDesired()
        {
            if (this.isVisibilityDesired)
            {
                // Note: a better way would be to invoke the redraw method, but generating the arguments for that is a pain.
                if (notYetRedrawn) return; // On startup, we don't want to show the magnifying glass until there has been at least one Redraw pass in it to dipslay its contents
                this.Visibility = Visibility.Visible;
            }
        }
        #endregion
    }

    #endregion

    #region INTERNAL CLASS: CalculationUtilities
    internal class Calculations
    {
        // Calculate the point as a ratio of its position on the image, so we can locate it regardless of the actual image size
        static internal Point convertPointToRatio(Point p, double width, double height)
        {
            Point ratioPt = new Point((double)p.X / (double)width, (double)p.Y / (double)height);
            return ratioPt;
        }

        // The inverse of the above operation
        static internal Point convertRatioToPoint(System.Windows.Point p, double width, double height)
        {
            Point imagePt = new Point(p.X * width, p.Y * height);
            return imagePt;
        }

        // This purportedly corrects a WPF problem... not sure if its really needed.
        static internal  Point CorrectGetPosition(Visual relativeTo)
        {
            Win32Point w32Mouse = new Win32Point();
            GetCursorPos(ref w32Mouse);
            return relativeTo.PointFromScreen(new System.Windows.Point(w32Mouse.X, w32Mouse.Y));
        }

        [StructLayout(LayoutKind.Sequential)]
        internal struct Win32Point
        {
            public Int32 X;
            public Int32 Y;
        };

        [DllImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        static internal extern bool GetCursorPos(ref Win32Point pt);
    }
    #endregion
}
