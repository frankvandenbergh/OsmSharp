// OsmSharp - OpenStreetMap (OSM) SDK
// Copyright (C) 2013 Abelshausen Ben
// 
// This file is part of OsmSharp.
// 
// OsmSharp is free software: you can redistribute it and/or modify
// it under the terms of the GNU General Public License as published by
// the Free Software Foundation, either version 2 of the License, or
// (at your option) any later version.
// 
// OsmSharp is distributed in the hope that it will be useful,
// but WITHOUT ANY WARRANTY; without even the implied warranty of
// MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE. See the
// GNU General Public License for more details.
// 
// You should have received a copy of the GNU General Public License
// along with OsmSharp. If not, see <http://www.gnu.org/licenses/>.

using Android.App;
using Android.Content;
using Android.Util;
using Android.Views;
using OsmSharp.Logging;
using OsmSharp.Math.Geo;
using OsmSharp.Math.Geo.Projections;
using OsmSharp.Math.Primitives;
using OsmSharp.UI;
using OsmSharp.UI.Animations;
using OsmSharp.UI.Animations.Invalidation.Triggers;
using OsmSharp.UI.Map;
using OsmSharp.UI.Map.Layers;
using OsmSharp.UI.Renderer;
using OsmSharp.UI.Renderer.Primitives;
using OsmSharp.Units.Angle;
using System;
using System.Collections.Generic;
using System.Threading;

namespace OsmSharp.Android.UI
{
	/// <summary>
	/// Map view surface.
	/// </summary>
	public class MapViewSurface : View, IMapViewSurface,
			ScaleGestureDetector.IOnScaleGestureListener, 
			RotateGestureDetector.IOnRotateGestureListener,
			MoveGestureDetector.IOnMoveGestureListener,
            TapGestureDetector.IOnTapGestureListener,
			global::Android.Views.View.IOnTouchListener,
            IInvalidatableMapSurface
	{
		private bool _invertX = false;
		private bool _invertY = false;

		/// <summary>
		/// Holds the primitives layer.
		/// </summary>
		private LayerPrimitives _makerLayer;
		
		/// <summary>
		/// Holds the scale gesture detector.
		/// </summary>
		private ScaleGestureDetector _scaleGestureDetector;

		/// <summary>
		/// Holds the rotation gesture detector.
		/// </summary>
		private RotateGestureDetector _rotateGestureDetector;

		/// <summary>
		/// Holds the move gesture detector.
		/// </summary>
		private MoveGestureDetector _moveGestureDetector;

        /// <summary>
        /// Holds the tag gesture detector.
        /// </summary>
        private TapGestureDetector _tagGestureDetector;

		/// <summary>
		/// Holds the maplayout.
		/// </summary>
		private MapView _mapView;

		/// <summary>
		/// Initializes a new instance of the <see cref="OsmSharp.Android.UI.MapViewSurface"/> class.
		/// </summary>
		/// <param name="context">Context.</param>
		public MapViewSurface (Context context) :
			base (context)
        {
            // register default invalidation trigger.
            (this as IInvalidatableMapSurface).RegisterListener(new DefaultTrigger(this));

            this.MapAllowPan = true;
            this.MapAllowTilt = true;
            this.MapAllowZoom = true;
		}

		/// <summary>
		/// Initializes a new instance of the <see cref="OsmSharp.Android.UI.MapView"/> class.
		/// </summary>
		/// <param name="context">Context.</param>
		/// <param name="attrs">Attrs.</param>
		public MapViewSurface (Context context, IAttributeSet attrs) :
			base (context, attrs)
        {
            // register default invalidation trigger.
            (this as IInvalidatableMapSurface).RegisterListener(new DefaultTrigger(this));

            this.MapAllowPan = true;
            this.MapAllowTilt = true;
            this.MapAllowZoom = true;
		}

        /// <summary>
        /// Initialize implementation from IMapView.
        /// </summary>
        /// <param name="mapLayout"></param>
        void IMapViewSurface.Initialize(MapView mapLayout)
        {
            _mapView = mapLayout;
            this.SetWillNotDraw(false);

            this.MapMinZoomLevel = 10;
            this.MapMaxZoomLevel = 20;

            _renderer = new MapRenderer<global::Android.Graphics.Canvas>(
                new CanvasRenderer2D());

            // initialize the gesture detection.
            this.SetOnTouchListener(this);
            _scaleGestureDetector = new ScaleGestureDetector(
                this.Context, this);
            _rotateGestureDetector = new RotateGestureDetector(
                this.Context, this);
            _moveGestureDetector = new MoveGestureDetector(
                this.Context, this);
            _tagGestureDetector = new TapGestureDetector(
                this.Context, this);

            _makerLayer = new LayerPrimitives(
                new WebMercator());

            // initialize all the caching stuff.
            _backgroundColor = SimpleColor.FromKnownColor(KnownColor.White).Value;
            _cacheRenderer = new MapRenderer<global::Android.Graphics.Canvas>(
                new CanvasRenderer2D());
        }

		/// <summary>
		/// Holds the off screen buffered image.
		/// </summary>
        private ImageTilted2D _offScreenBuffer;

        /// <summary>
        /// Holds the on screen buffered image.
        /// </summary>
        private ImageTilted2D _onScreenBuffer;

        /// <summary>
        /// Holds the background color.
        /// </summary>
        private int _backgroundColor;

		/// <summary>
		/// Holds the cache renderer.
		/// </summary>
		private MapRenderer<global::Android.Graphics.Canvas> _cacheRenderer;

        /// <summary>
        /// Holds the rendering thread.
        /// </summary>
        private Thread _renderingThread;

        /// <summary>
        /// Holds the extra parameter.
        /// </summary>
        private float _extra = 1.5f;

        /// <summary>
        /// Triggers rendering.
        /// </summary>
        public void TriggerRendering()
        {
            this.TriggerRendering(false);
        }

        /// <summary>
        /// Triggers rendering.
        /// </summary>
        public void TriggerRendering(bool force)
        {
            if (this.SurfaceWidth == 0)
            { // nothing to render yet!
                return;
            }

            // create the view that would be use for rendering.
            View2D view = _cacheRenderer.Create((int)(this.SurfaceWidth * _extra), (int)(this.SurfaceHeight * _extra),
                this.Map, (float)this.Map.Projection.ToZoomFactor(this.MapZoom),
                this.MapCenter, _invertX, _invertY, this.MapTilt);

            // ... and compare to the previous rendered view.
            if (!force && _previouslyRenderedView != null &&
                view.Equals(_previouslyRenderedView))
            {
                return;
            }
            _previouslyRenderedView = view;

            // end existing rendering thread.
            if (_renderingThread != null &&
                _renderingThread.IsAlive)
            {
                if (_cacheRenderer.IsRunning)
                {
                    this.Map.ViewChangedCancel();
                    _cacheRenderer.CancelAndWait();
                }
            }

            // start new rendering thread.
            _renderingThread = new Thread(new ThreadStart(Render));
            _renderingThread.Start();
        }

        /// <summary>
        /// Stops the current rendering if in progress.
        /// </summary>
        internal void StopRendering()
        {
            // stop current rendering.
            if (_renderingThread != null &&
                _renderingThread.IsAlive)
            {
                if (_cacheRenderer.IsRunning)
                {
                    this.Map.ViewChangedCancel();
                    _cacheRenderer.CancelAndWait();
                }
            }
        }

        /// <summary>
        /// Holds the previous rendered zoom.
        /// </summary>
        private View2D _previouslyRenderedView;

        private float _surfaceWidth = 0;

        /// <summary>
        /// Returns the width of this rendering surface.
        /// </summary>
        private float SurfaceWidth
        {
            get
            {
                return _surfaceWidth;
            }
        }

        private float _surfaceHeight = 0;

        /// <summary>
        /// Returns the height of this rendering surface.
        /// </summary>
        private float SurfaceHeight
        {
            get
            {
                return _surfaceHeight;
            }
        }

		/// <summary>
		/// Renders the current complete scene.
		/// </summary>
		private void Render()
		{	
			if (_cacheRenderer.IsRunning) {
				_cacheRenderer.CancelAndWait ();
			}

			// make sure only on thread at the same time is using the renderer.
            lock (_cacheRenderer)
            {
                this.Map.ViewChangedCancel();

				// build the layers list.
				var layers = new List<Layer> ();
				for (int layerIdx = 0; layerIdx < this.Map.LayerCount; layerIdx++) {
					// get the layer.
					layers.Add (this.Map[layerIdx]);
				}

				// add the internal layers.
				layers.Add (_makerLayer);

				// get old image if available.
                global::Android.Graphics.Bitmap image = null;
				if (_offScreenBuffer != null)
                {
                    image = _offScreenBuffer.Tag as global::Android.Graphics.Bitmap;
				}

                if (this.SurfaceHeight == 0)
                {
                    return;
                }

                // resize image if needed.
                float size = System.Math.Max(this.SurfaceHeight, this.SurfaceWidth);
                if (image == null ||
                    image.Width != (int)(size * _extra) ||
                    image.Height != (int)(size * _extra))
                { // create a bitmap and render there.
                    image = global::Android.Graphics.Bitmap.CreateBitmap((int)(size * _extra),
                        (int)(size * _extra),
                        global::Android.Graphics.Bitmap.Config.Argb8888);
                }

				// create and reset the canvas.
				global::Android.Graphics.Canvas canvas = new global::Android.Graphics.Canvas (image);
				canvas.DrawColor (new global::Android.Graphics.Color(
					SimpleColor.FromKnownColor(KnownColor.White).Value));

				// create the view.
                double[] sceneCenter = this.Map.Projection.ToPixel(this.MapCenter.Latitude, this.MapCenter.Longitude);
                float mapZoom = this.MapZoom;
                float sceneZoomFactor = (float)this.Map.Projection.ToZoomFactor(this.MapZoom);

                // create the view for this control.
                View2D view = View2D.CreateFrom((float)sceneCenter[0], (float)sceneCenter[1],
                                         size * _extra, size * _extra, sceneZoomFactor,
                                         _invertX, _invertY, this.MapTilt);

				long before = DateTime.Now.Ticks;

				OsmSharp.Logging.Log.TraceEvent("OsmSharp.Android.UI.MapView", TraceEventType.Information,
				                                "Rendering Start");

				// notify the map that the view has changed.
				this.Map.ViewChanged ((float)this.Map.Projection.ToZoomFactor(this.MapZoom), this.MapCenter, 
				                      view);
				long afterViewChanged = DateTime.Now.Ticks;
				OsmSharp.Logging.Log.TraceEvent("OsmSharp.Android.UI.MapView", TraceEventType.Information,
				                                "View change took: {0}ms @ zoom level {1}",
				                                (new TimeSpan(afterViewChanged - before).TotalMilliseconds), this.MapZoom);

				// does the rendering.
                bool complete = _cacheRenderer.Render(canvas, layers, view, (float)this.Map.Projection.ToZoomFactor(this.MapZoom));

				long afterRendering = DateTime.Now.Ticks;
				OsmSharp.Logging.Log.TraceEvent("OsmSharp.Android.UI.MapView", TraceEventType.Information,
				                                "Rendering took: {0}ms @ zoom level {1}",
				                                (new TimeSpan(afterRendering - afterViewChanged).TotalMilliseconds), this.MapZoom);
				if(complete)
				{ // there was no cancellation, the rendering completely finished.
					// add the result to the scene cache.
                    
                    // add the newly rendered image again.            
                    _offScreenBuffer = new ImageTilted2D(view.Rectangle, new byte[0], float.MinValue, float.MaxValue);
                    _offScreenBuffer.Tag = image;

                    var temp = _onScreenBuffer;
                    _onScreenBuffer = _offScreenBuffer;
                    _offScreenBuffer = temp;
				}

                // notify the the current surface of the new rendering.
                this.PostInvalidate();
				
				long after = DateTime.Now.Ticks;

                if (complete)
                { // report a successfull render to listener.
                    _listener.NotifyRenderSuccess(view, mapZoom, (int)new TimeSpan(after - before).TotalMilliseconds);
                }
			}
		}

        /// <summary>
        /// The map center.
        /// </summary>
        private GeoCoordinate _mapCenter;

        /// <summary>
        /// Gets or sets the center.
        /// </summary>
        /// <value>The center.</value>
        public GeoCoordinate MapCenter
        {
            get 
            { 
                return _mapCenter; 
            }
            set
            {
                _mapCenter = value;

                // report 
                (this.Context as Activity).RunOnUiThread(NotifyMovement);
            }
        }

		/// <summary>
		/// Holds the map.
		/// </summary>
		private Map _map;

		/// <summary>
		/// Gets or sets the map.
		/// </summary>
		/// <value>The map.</value>
		public Map Map
        {
			get { return _map; }
			set { _map = value; }
		}

		/// <summary>
		/// Holds the map tilte angle.
		/// </summary>
		private Degree _mapTilt;

		/// <summary>
		/// Gets or sets the map tilt.
		/// </summary>
		/// <value>The map tilt.</value>
        public Degree MapTilt
        {
			get { return _mapTilt; }
            set
            {
                _mapTilt = value;

                (this.Context as Activity).RunOnUiThread(NotifyMovement);
            }
		}

		/// <summary>
		/// Holds the map zoom level.
		/// </summary>
		private float _mapZoomLevel;

		/// <summary>
		/// Gets or sets the zoom factor.
		/// </summary>
		/// <value>The zoom factor.</value>
		public float MapZoom
        {
			get { return _mapZoomLevel; }
			set { 
				if (this.MapMaxZoomLevel.HasValue &&
                    value > this.MapMaxZoomLevel) {
					_mapZoomLevel = this.MapMaxZoomLevel.Value;
				} else if (this.MapMinZoomLevel.HasValue &&
                    value < this.MapMinZoomLevel) {
					_mapZoomLevel = this.MapMinZoomLevel.Value;
				} else {
					_mapZoomLevel = value;
                }
				
                (this.Context as Activity).RunOnUiThread (NotifyMovement);
			}
		}

		/// <summary>
		/// Gets or sets the map max zoom level.
		/// </summary>
		/// <value>The map max zoom level.</value>
		public float? MapMaxZoomLevel
        {
			get;
			set;
		}

		/// <summary>
		/// Gets or sets the map minimum zoom level.
		/// </summary>
		/// <value>The map minimum zoom level.</value>
		public float? MapMinZoomLevel
        {
			get;
			set;
		}

        /// <summary>
        /// Gets or sets the map tilt flag.
        /// </summary>
        public bool MapAllowTilt
        {
            get;
            set;
        }

        /// <summary>
        /// Gets or sets the map pan flag.
        /// </summary>
        public bool MapAllowPan
        {
            get;
            set;
        }

        /// <summary>
        /// Gets or sets the map zoom flag.
        /// </summary>
        public bool MapAllowZoom
        {
            get;
            set;
        }

		/// <summary>
		/// Holds the renderer.
		/// </summary>
		private MapRenderer<global::Android.Graphics.Canvas> _renderer;

		/// <summary>
		/// Raises the draw event.
		/// </summary>
		/// <param name="canvas">Canvas.</param>
        protected override void OnDraw(global::Android.Graphics.Canvas canvas)
        {
            base.OnDraw(canvas);

            // set the height/width.
            if (_surfaceHeight != canvas.Height ||
                _surfaceWidth != canvas.Width)
            {
                _surfaceHeight = canvas.Height;
                _surfaceWidth = canvas.Width;

                // trigger rendering.
                this.TriggerRendering();
            }

            // render only the cached scene.
            canvas.DrawColor(new global::Android.Graphics.Color(_backgroundColor));
            View2D view = this.CreateView();
            float zoomFactor = (float)this.Map.Projection.ToZoomFactor(this.MapZoom);
            _renderer.SceneRenderer.Render(
                canvas,
                view,
                zoomFactor,
                new Primitive2D[] { _onScreenBuffer });
        }
		
		/// <summary>
		/// Creates a view.
		/// </summary>
		/// <returns></returns>
		public View2D CreateView()
		{
            float height = this.SurfaceHeight;
            float width = this.SurfaceWidth;

			// calculate the center/zoom in scene coordinates.
			double[] sceneCenter = this.Map.Projection.ToPixel(this.MapCenter.Latitude, this.MapCenter.Longitude);
			float sceneZoomFactor = (float)this.Map.Projection.ToZoomFactor(this.MapZoom);

			// create the view for this control.
			return View2D.CreateFrom((float)sceneCenter[0], (float)sceneCenter[1],
			                         width, height, sceneZoomFactor, 
			                         _invertX, _invertY, this.MapTilt);
		}

		/// <summary>
		/// Raises the layout event.
		/// </summary>
		/// <param name="changed">If set to <c>true</c> changed.</param>
		/// <param name="left">Left.</param>
		/// <param name="top">Top.</param>
		/// <param name="right">Right.</param>
		/// <param name="bottom">Bottom.</param>
		protected override void OnLayout (bool changed, int left, int top, int right, int bottom)
		{
			// execute suspended events.
			if (_latestZoomCall != null)
			{ // there was a suspended call.
				this.ZoomToMarkers(
					_latestZoomCall.Markers,
					_latestZoomCall.Percentage);
			}

			if (_onScreenBuffer == null) {
				this.TriggerRendering (); // force a rendering on the first layout-event.
			}
		}

		/// <summary>
		/// Notifies that there was movement.
		/// </summary>
		private void NotifyMovement()
		{
			// invalidate the current view.
			this.Invalidate ();

			// notify map layout of changes.
            if (this.SurfaceWidth > 0 && this.SurfaceHeight > 0)
            {
                // create the current view.
				View2D view = this.CreateView ();

                // notify map change to reposition markers.
                _mapView.NotifyMapChange(this.SurfaceWidth, this.SurfaceHeight, view, this.Map.Projection);

                // notify listener.
                _listener.NotifyChange(view, this.MapZoom);
			}
		}

		private double _deltaScale = 1.0f;
		private double _deltaDegrees = 0.0f;

		private double _deltaX = 0.0f;
		private double _deltaY = 0.0f;

		#region IOnScaleGestureListener implementation

		/// <summary>
		/// Raises the scale event.
		/// </summary>
		/// <param name="detector">Detector.</param>
		public bool OnScale (ScaleGestureDetector detector)
		{
			_deltaScale = detector.ScaleFactor;

			return true;
		}
		
		/// <summary>
		/// Raises the scale begin event.
		/// </summary>
		/// <param name="detector">Detector.</param>
		public bool OnScaleBegin (ScaleGestureDetector detector)
		{
			_deltaScale = 1;
			_deltaDegrees = 0;
			_deltaX = 0;
			_deltaY = 0;

			return true;
		}
		
		/// <summary>
		/// Raises the scale end event.
		/// </summary>
		/// <param name="detector">Detector.</param>
		public void OnScaleEnd (ScaleGestureDetector detector)
        {
            _deltaScale = 1;
		}
		
		#endregion

		#region IOnRotateGestureListener implementation

		public bool OnRotate (RotateGestureDetector detector)
		{
			_deltaDegrees = detector.RotationDegreesDelta;

			return true;
		}

		public bool OnRotateBegin (RotateGestureDetector detector)
		{
			_deltaScale = 1;
			_deltaDegrees = 0;
			_deltaX = 0;
            _deltaY = 0;

			return true;
		}

		public void OnRotateEnd (RotateGestureDetector detector)
        {
            _deltaDegrees = 0;
		}

		#endregion

		#region IOnMoveGestureListener implementation

		public bool OnMove (MoveGestureDetector detector)
		{
			global::Android.Graphics.PointF d = detector.FocusDelta;
			_deltaX = d.X;
			_deltaY = d.Y;

			return true;
		}

		public bool OnMoveBegin (MoveGestureDetector detector)
		{
			_deltaScale = 1;
			_deltaDegrees = 0;
			_deltaX = 0;
			_deltaY = 0;

			return true;
		}

		public void OnMoveEnd (MoveGestureDetector detector)
		{

		}

		#endregion

        #region IOnTapGestureListener implementation

        /// <summary>
        /// Called when a tab is detected.
        /// </summary>
        /// <param name="detector"></param>
        /// <returns></returns>
        public bool OnTap(TapGestureDetector detector)
        {
            // recreate the view.
            View2D view = this.CreateView();

            // calculate the new center in pixels.
            double x = detector.X;
            double y = detector.Y;

            // calculate the new center from the view.
            double[] sceneCenter = view.FromViewPort(this.SurfaceWidth, this.SurfaceHeight,
                                                      x, y);

            // convert to the projected center.
            _mapView.RaiseMapTapEvent(this.Map.Projection.ToGeoCoordinates(sceneCenter[0], sceneCenter[1]));

            return true;
        }

        #endregion
		
		/// <summary>
		/// Raises the touch event event.
		/// </summary>
		/// <param name="e">E.</param>
		public override bool OnTouchEvent (MotionEvent e)
		{
			return true;
		}
		
		#region IOnTouchListener implementation
		
		/// <summary>
		/// Raises the touch event.
		/// </summary>
		/// <param name="v">V.</param>
		/// <param name="e">E.</param>
		public bool OnTouch (global::Android.Views.View v, MotionEvent e)
		{
            _tagGestureDetector.OnTouchEvent (e);
			_scaleGestureDetector.OnTouchEvent (e);
			_rotateGestureDetector.OnTouchEvent (e);
			_moveGestureDetector.OnTouchEvent (e);

            if (_deltaX != 0 || _deltaY != 0 || // was there movement?
                _deltaScale != 1.0 || // was there scale?
                _deltaDegrees != 0)
            { // was there rotation?
                bool movement = false;
                if (this.MapAllowZoom &&
                    _deltaScale != 1.0)
                {
                    // calculate the scale.
                    double zoomFactor = this.Map.Projection.ToZoomFactor(this.MapZoom);
                    zoomFactor = zoomFactor * _deltaScale;
                    this.MapZoom = (float)this.Map.Projection.ToZoomLevel(zoomFactor);

                    movement = true;
                }

                if (this.MapAllowPan)
                {
                    // stop the animation.
                    this.StopCurrentAnimation();

                    // recreate the view.
                    View2D view = this.CreateView();

                    // calculate the new center in pixels.
                    double centerXPixels = this.SurfaceWidth / 2.0f - _deltaX;
                    double centerYPixles = this.SurfaceHeight / 2.0f - _deltaY;

                    // calculate the new center from the view.
                    double[] sceneCenter = view.FromViewPort(this.SurfaceWidth, this.SurfaceHeight,
                                                              centerXPixels, centerYPixles);

                    // convert to the projected center.
                    _mapCenter = this.Map.Projection.ToGeoCoordinates(sceneCenter[0], sceneCenter[1]);

                    movement = true;
                }

                // do the rotation stuff around the new center.
                if (this.MapAllowTilt &&
                    _deltaDegrees != 0)
                {
                    // recreate the view.
                    View2D view = this.CreateView();

                    View2D rotatedView = view.RotateAroundCenter((Degree)(-_deltaDegrees));
                    _mapTilt = (float)((Degree)rotatedView.Rectangle.Angle).Value;

                    movement = true;
                }

                _deltaScale = 1;
                _deltaDegrees = 0;
                _deltaX = 0;
                _deltaY = 0;

                // notify touch.
                if (movement)
                {
                    _mapView.RaiseMapTouched();

                    this.NotifyMovement();
                }
            }
			return true;
		}
		
		#endregion

		/// <summary>
		/// Holds the map view animator.
		/// </summary>
		private MapViewAnimator _mapViewAnimator;

        /// <summary>
        /// Stops the current animation.
        /// </summary>
        private void StopCurrentAnimation()
        {
            if (_mapViewAnimator != null)
            {
                _mapViewAnimator.Stop();
            }
        }

		/// <summary>
		/// Registers the animator.
		/// </summary>
		/// <param name="mapViewAnimator">Map view animator.</param>
		public void RegisterAnimator (MapViewAnimator mapViewAnimator)
		{
			_mapViewAnimator = mapViewAnimator;
		}

		/// <summary>
		/// Sets the map view.
		/// </summary>
		/// <param name="center">Center.</param>
		/// <param name="mapTilt">Map tilt.</param>
		/// <param name="mapZoom">Map zoom.</param>
		public void SetMapView (GeoCoordinate center, Degree mapTilt, float mapZoom)
		{
			_mapCenter = center;
			_mapTilt = mapTilt;
			this.MapZoom = mapZoom;

		    (this.Context as Activity).RunOnUiThread(NotifyMovement);
		}

		/// <summary>
		/// Holds a suspended call to zoom to markers.
		/// </summary>
		private MapViewMarkerZoomEvent _latestZoomCall;

        /// <summary>
        /// Zooms to the given list of markers.
        /// </summary>
        /// <param name="markers"></param>
        /// <param name="percentage"></param>
        public void ZoomToMarkers(List<MapMarker> markers, double percentage)
        {
            float height = this.SurfaceHeight;
            float width = this.SurfaceWidth;
			if (width > 0) {
				PointF2D[] points = new PointF2D[markers.Count];
				for (int idx = 0; idx < markers.Count; idx++) {
					points [idx] = new PointF2D (this.Map.Projection.ToPixel (markers [idx].Location));
				}
				View2D view = this.CreateView ();
				View2D fittedView = view.Fit (points, percentage);

				float zoom = (float)this.Map.Projection.ToZoomLevel (fittedView.CalculateZoom (
					                         width, height));
				GeoCoordinate center = this.Map.Projection.ToGeoCoordinates (
					                                   fittedView.Center [0], fittedView.Center [1]);

                this.SetMapView (center, this.MapTilt, zoom);
			} else {
                _latestZoomCall = new MapViewMarkerZoomEvent()
                {
                    Markers = markers,
                    Percentage = percentage
                };
			}
        }
		
		private class MapViewMarkerZoomEvent
		{
			/// <summary>
			/// Gets or sets the markers.
			/// </summary>
			/// <value>The markers.</value>
			public List<MapMarker> Markers {
				get;
				set;
			}

			/// <summary>
			/// Gets or sets the percentage.
			/// </summary>
			/// <value>The percentage.</value>
			public double Percentage {
				get;
				set;
			}
		}

        int IMapViewSurface.Width
        {
            get { return (int)this.SurfaceWidth; }
        }

        int IMapViewSurface.Height
        {
            get { return (int)this.SurfaceHeight; }
        }

        #region IInvalidatableMapSurface Interface

        /// <summary>
        /// Holds the trigger listener.
        /// </summary>
        private TriggerBase _listener;

		/// <summary>
		/// Returns true if this surface is sure that is going to keep moving.
		/// </summary>
		bool IInvalidatableMapSurface.StillMoving()
		{
			return _mapViewAnimator != null;
		}

        /// <summary>
        /// Triggers the rendering.
        /// </summary>
        void IInvalidatableMapSurface.Render()
        {
            this.TriggerRendering();
        }

        /// <summary>
        /// Cancels the current rendering.
        /// </summary>
        void IInvalidatableMapSurface.CancelRender()
        {
            this.StopRendering();
        }

        /// <summary>
        /// Registers an invalidation listenener.
        /// </summary>
        /// <param name="listener"></param>
        void IInvalidatableMapSurface.RegisterListener(TriggerBase listener)
        {
            _listener = listener;
        }

        /// <summary>
        /// Unregisters the current listener.
        /// </summary>
        void IInvalidatableMapSurface.ResetListener()
        {
            _listener = null;
        }

        #endregion
    }
}