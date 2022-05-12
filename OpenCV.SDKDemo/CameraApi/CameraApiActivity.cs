

using OpenCV.SDKDemo.CameraApi.Callbacks;
using OpenCV.SDKDemo.CameraApi.Controls;
using OpenCV.SDKDemo.CameraApi.Helpers;
using OpenCV.SDKDemo.DnnMy;

using Android.App;
using Android.OS;
using Android.Runtime;
using Android.Widget;
using Android.Hardware.Camera2;
using Android.Views;
using Android.Util;
using Android.Hardware.Camera2.Params;
using Android.Graphics;
using Android.Media;
using Android.Content.PM;
using AndroidX.Core.Math;

using Java.Util;

using System;
using System.Collections.Generic;
using System.Linq;


namespace OpenCV.SDKDemo.CameraApi
{
    [Activity(Label = "CameraApiActivity", ScreenOrientation = ScreenOrientation.Portrait, ConfigurationChanges = ConfigChanges.KeyboardHidden |
        ConfigChanges.Locale | ConfigChanges.ScreenSize | ConfigChanges.Orientation |
        ConfigChanges.UiMode | ConfigChanges.ScreenLayout | ConfigChanges.SmallestScreenSize, Theme = "@style/MainTheme")]

    public partial class CameraApiActivity : Activity
    {

        private AutoFitTextureView _surfaceTextureView;
        private ImageButton _switchCameraButton;
        private ImageButton _takePictureButton;
        private CameraStateCallback _cameraStateCallback;
        private CaptureStateSessionCallback _captureStateSessionCallback;
        private CameraCaptureCallback _cameraCaptureCallback;
        private CameraManager _manager;
        private IWindowManager _windowManager;
        private ImageAvailableListener _onImageAvailableListener;
        private SparseIntArray _orientations = new SparseIntArray();
        private LensFacing _currentLensFacing = LensFacing.Back;
        private CameraCharacteristics _characteristics;
        private CameraDevice _cameraDevice;
        private ImageReader _imageReader;
        private int _sensorOrientation;
        private Size _previewSize;
        private HandlerThread _backgroundThread;
        private Handler _backgroundHandler;
        private bool _flashSupported;
        private Surface previewSurface;
        private CameraCaptureSession captureSession;
        private CaptureRequest.Builder previewRequestBuilder;
        private CaptureRequest previewRequest;

        //LPR
        private int _period;
        private ISurfaceHolder _holder;
        private SurfaceView _surfaceView;
        private float _scale;
        private Paint _mpaint;
        private Canvas _canvas;
        private Dictionary<Core.Rect, string> _plates;
        private Camera2_LPR _camera2_LPR;


        protected override void OnCreate(Bundle savedInstanceState)
        {
            base.OnCreate(savedInstanceState);

            SetContentView(Resource.Layout.CameraApiPreview);

            _switchCameraButton = FindViewById<ImageButton>(Resource.Id.reverse_camera_button);
            _takePictureButton = FindViewById<ImageButton>(Resource.Id.take_picture_button);
            _surfaceTextureView = FindViewById<AutoFitTextureView>(Resource.Id.surface);
            _surfaceView = FindViewById<SurfaceView>(Resource.Id.surfaceview);

            FindViewById<ImageButton>(Resource.Id.pluse_button).Click += ZoomPluse_Click;
            FindViewById<ImageButton>(Resource.Id.minus_button).Click += ZoomMinus_Click;

            _manager = GetSystemService(CameraService) as CameraManager;
            _windowManager = GetSystemService(WindowService).JavaCast<IWindowManager>();

            _cameraStateCallback = new CameraStateCallback
            {
                Opened = OnOpened,
                Disconnected = OnDisconnected,
                Error = OnError,
            };

            _captureStateSessionCallback = new CaptureStateSessionCallback
            {
                Configured = OnPreviewSessionConfigured,
            };

            _cameraCaptureCallback = new CameraCaptureCallback
            {
                CaptureCompleted = (session, request, result) => ProcessImageCapture(result),
                CaptureProgressed = (session, request, result) => ProcessImageCapture(result),
            };

            _onImageAvailableListener = new ImageAvailableListener
            {
                ImageAvailable = HandleImageCaptured,
            };

            _orientations.Append((int)SurfaceOrientation.Rotation0, 90);
            _orientations.Append((int)SurfaceOrientation.Rotation90, 0);
            _orientations.Append((int)SurfaceOrientation.Rotation180, 270);
            _orientations.Append((int)SurfaceOrientation.Rotation270, 180);


            ////////  LPR  /////////
            _surfaceTextureView.SurfaceTextureUpdated += SurfaceTextureView_SurfaceTextureUpdated;
            _surfaceTextureView.Touch += SurfaceTextureView_Touch;

            _surfaceView.SetZOrderOnTop(true);
            _surfaceView.Holder.SetFormat(Format.Transparent);

            _holder = _surfaceView.Holder;
            _mpaint = new Paint();
            _camera2_LPR = new Camera2_LPR();
            _period = 0;
            _scale = 1f;
        }

        protected override void OnResume()
        {
            base.OnResume();
            _switchCameraButton.Click += SwitchCameraButton_Click;
            _takePictureButton.Click += TakePictureButton_Click;

            StartBackgroundThread();

            if (_surfaceTextureView.IsAvailable)
            {
                ForceResetLensFacing();
            }
            else
            {
                _surfaceTextureView.SurfaceTextureAvailable += SurfaceTextureView_SurfaceTextureAvailable;
            }
        }

        protected override void OnPause()
        {
            base.OnPause();
            _switchCameraButton.Click -= SwitchCameraButton_Click;
            _takePictureButton.Click -= TakePictureButton_Click;
            _surfaceTextureView.SurfaceTextureAvailable -= SurfaceTextureView_SurfaceTextureAvailable;

            CloseCamera();
            StopBackgroundThread();
        }


        #region private Helpers

        private void ZoomMinus_Click(object sender, EventArgs e)
        {
            _scale -= 1f;
            Zoom();
        }

        private void ZoomPluse_Click(object sender, EventArgs e)
        {
            _scale += 1f;
            Zoom();
        }

        private void Zoom()
        {
            Rect mSensorSize = (Rect)_characteristics.Get(CameraCharacteristics.SensorInfoActiveArraySize);

            var newZoom = MathUtils.Clamp(_scale, 1.0f, 6.0f);
            int centerX = mSensorSize.Width() / 2;
            int centerY = mSensorSize.Height() / 2;
            int deltaX = (int)((0.5f * mSensorSize.Width()) / newZoom);
            int deltaY = (int)((0.5f * mSensorSize.Height()) / newZoom);

            Rect zoomRegion = new Rect(centerX - deltaX,
                    centerY - deltaY,
                    centerX + deltaX,
                    centerY + deltaY);

            previewRequestBuilder.Set(CaptureRequest.ScalerCropRegion, zoomRegion);
            captureSession.SetRepeatingRequest(previewRequestBuilder.Build(), _cameraCaptureCallback, _backgroundHandler);
        }

        //LPR every frame
        private void SurfaceTextureView_SurfaceTextureUpdated(object sender, TextureView.SurfaceTextureUpdatedEventArgs e)
        {

            Bitmap bitmap = _surfaceTextureView.GetBitmap(_surfaceTextureView.Width, _surfaceTextureView.Height);
            int[] argb = new int[_surfaceTextureView.Width * _surfaceTextureView.Height];
            // get ARGB pixels and then proccess it with 8UC4 OpenCV convertion
            bitmap.GetPixels(argb, 0, _surfaceTextureView.Width, 0, 0, _surfaceTextureView.Width, _surfaceTextureView.Height);
            Core.Mat frame = new Core.Mat();
            OpenCV.Android.Utils.BitmapToMat(bitmap, frame, true);
            OpenCV.ImgProc.Imgproc.CvtColor(frame, frame, ImgProc.Imgproc.ColorRgba2rgb);

            if (_period == 2 && frame != null)
            {
                _plates = new Dictionary<Core.Rect, string>();

                _camera2_LPR.Detect_LPR(ref frame, ref _plates);

                //define the paintbrush        
                _mpaint.Color = Color.Red;
                _mpaint.TextSize = 45;
                _mpaint.SetStyle(Paint.Style.Stroke);
                _mpaint.StrokeWidth = 3f;

                //draw
                _canvas = _holder.LockCanvas();
                _canvas.DrawColor(Color.Transparent, PorterDuff.Mode.Clear);//clear the paint of last time

                foreach (var plate in _plates)
                {
                    Rect tempRect = new Rect(plate.Key.X, plate.Key.Y + 20, plate.Key.X + plate.Key.Width,
                                             plate.Key.Y + plate.Key.Height - 20);
                    _canvas.DrawRect(tempRect, _mpaint);
                    _canvas.DrawText(plate.Value, plate.Key.X + 5, plate.Key.Y - 5, _mpaint);
                }
                _holder.UnlockCanvasAndPost(_canvas);

                _period = 0;
            }

            _period++;
        }

        private void SurfaceTextureView_Touch(object sender, View.TouchEventArgs e)
        {
            //  var id_list = manager.GetCameraIdList();
            // var characteristics = manager.GetCameraCharacteristics(id_list[0]); 
        }

        private void Surface_FrameAvailable(object sender, SurfaceTexture.FrameAvailableEventArgs e)
        {
            SurfaceTexture surfaceTexture = e.SurfaceTexture;
        }

        private void SurfaceTextureView_SurfaceTextureAvailable(object sender, TextureView.SurfaceTextureAvailableEventArgs e)
        {
            ForceResetLensFacing();
        }

        private void StartBackgroundThread()
        {
            _backgroundThread = new HandlerThread("CameraBackground");
            _backgroundThread.Start();
            _backgroundHandler = new Handler(_backgroundThread.Looper);
        }

        private void SwitchCameraButton_Click(object sender, EventArgs e)
        {
            SetLensFacing(_currentLensFacing == LensFacing.Back ? LensFacing.Front : LensFacing.Back);
        }

        private void CloseCamera()
        {
            try
            {
                if (null != captureSession)
                {
                    captureSession.Close();
                    captureSession = null;
                }
                if (null != _cameraDevice)
                {
                    _cameraDevice.Close();
                    _cameraDevice = null;
                }
                if (null != _imageReader)
                {
                    _imageReader.Close();
                    _imageReader = null;
                }
            }
            catch (Exception e)
            {
                System.Diagnostics.Debug.WriteLine($"{e.Message} {e.StackTrace}");
            }
        }

        private void StopBackgroundThread()
        {
            if (_backgroundThread == null) return;

            _backgroundThread.QuitSafely();
            try
            {
                _backgroundThread.Join();
                _backgroundThread = null;
                _backgroundHandler = null;
            }
            catch (Exception e)
            {
                System.Diagnostics.Debug.WriteLine($"{e.Message} {e.StackTrace}");
            }
        }

        private void ForceResetLensFacing()//change camera front, back
        {
            var targetLensFacing = _currentLensFacing;
            _currentLensFacing = _currentLensFacing == LensFacing.Back ? LensFacing.Front : LensFacing.Back;
            SetLensFacing(targetLensFacing);
        }

        private void SetLensFacing(LensFacing lenseFacing)//change camera front, back
        {
            bool shouldRestartCamera = _currentLensFacing != lenseFacing;
            _currentLensFacing = lenseFacing;
            string cameraId = string.Empty;
            _characteristics = null;

            foreach (string id in _manager.GetCameraIdList())
            {
                cameraId = id;
                _characteristics = _manager.GetCameraCharacteristics(id);

                int face = (int)_characteristics.Get(CameraCharacteristics.LensFacing);

                if (face == (int)_currentLensFacing)
                {
                    break;
                }
            }

            if (_characteristics == null)
                return;

            if (_cameraDevice != null)
            {
                try
                {
                    if (!shouldRestartCamera)
                        return;
                    if (_cameraDevice.Handle != IntPtr.Zero)
                    {
                        CloseCamera();
                    }
                }
                catch (Exception e)
                {
                    //Ignored
                    System.Diagnostics.Debug.WriteLine("CameraApiActivity = " + e);
                }
            }

            SetUpCameraOutputs(cameraId);
            ConfigureTransform(_surfaceTextureView.Width, _surfaceTextureView.Height);
            _manager.OpenCamera(cameraId, _cameraStateCallback, null);
        }

        private void SetUpCameraOutputs(string selectedCameraId)
        {
            var map = (StreamConfigurationMap)_characteristics.Get(CameraCharacteristics.ScalerStreamConfigurationMap);
            if (map == null)
            {
                return;
            }

            // For still image captures, we use the largest available size.
            Size largest = (Size)Collections.Max(Arrays.AsList(map.GetOutputSizes((int)ImageFormatType.Jpeg)),
                             new CompareSizesByArea());

            if (_imageReader == null)
            {
                _imageReader = ImageReader.NewInstance(largest.Width, largest.Height, ImageFormatType.Jpeg, maxImages: 1);
                _imageReader.SetOnImageAvailableListener(_onImageAvailableListener, _backgroundHandler);
            }

            // Find out if we need to swap dimension to get the preview size relative to sensor
            // coordinate.
            var displayRotation = _windowManager.DefaultDisplay.Rotation;
            _sensorOrientation = (int)_characteristics.Get(CameraCharacteristics.SensorOrientation);
            bool swappedDimensions = false;
            switch (displayRotation)
            {
                case SurfaceOrientation.Rotation0:
                case SurfaceOrientation.Rotation180:
                    if (_sensorOrientation == 90 || _sensorOrientation == 270)
                    {
                        swappedDimensions = true;
                    }
                    break;
                case SurfaceOrientation.Rotation90:
                case SurfaceOrientation.Rotation270:
                    if (_sensorOrientation == 0 || _sensorOrientation == 180)
                    {
                        swappedDimensions = true;
                    }
                    break;
                default:
                    System.Diagnostics.Debug.WriteLine($"Display rotation is invalid: {displayRotation}");
                    break;
            }

            Point displaySize = new Point();
            _windowManager.DefaultDisplay.GetSize(displaySize);
            var rotatedPreviewWidth = _surfaceTextureView.Width;
            var rotatedPreviewHeight = _surfaceTextureView.Height;
            var maxPreviewWidth = displaySize.X;
            var maxPreviewHeight = displaySize.Y;

            if (swappedDimensions)
            {
                rotatedPreviewWidth = _surfaceTextureView.Height;
                rotatedPreviewHeight = _surfaceTextureView.Width;
                maxPreviewWidth = displaySize.Y;
                maxPreviewHeight = displaySize.X;
            }

            // Danger, W.R.! Attempting to use too large a preview size could  exceed the camera
            // bus' bandwidth limitation, resulting in gorgeous previews but the storage of
            // garbage capture data.
            _previewSize = ChooseOptimalSize(map.GetOutputSizes(Java.Lang.Class.FromType(typeof(SurfaceTexture))),
                rotatedPreviewWidth, rotatedPreviewHeight, maxPreviewWidth,
                maxPreviewHeight, largest);

            //var orientation = Application.Context.Resources.Configuration.Orientation;
            //if (orientation == global::Android.Content.Res.Orientation.Landscape)
            //{
            //    _surfaceTextureView.SetAspectRatio(_previewSize.Width, _previewSize.Height);
            //}
            //else
            //{

                _surfaceTextureView.SetAspectRatio(_previewSize.Height, _previewSize.Width);
            //}

            // Check if the flash is supported.
            var available = (bool?)_characteristics.Get(CameraCharacteristics.FlashInfoAvailable);
            if (available == null)
            {
                _flashSupported = false;
            }
            else
            {
                _flashSupported = (bool)available;
            }
            return;
        }

        private static Size ChooseOptimalSize(Size[] choices, int textureViewWidth,
                                              int textureViewHeight, int maxWidth, 
                                              int maxHeight, Size aspectRatio)
        {

            var bigEnough = new List<Size>();
            // Collect the supported resolutions that are smaller than the preview Surface
            var notBigEnough = new List<Size>();
            int w = aspectRatio.Width;
            int h = aspectRatio.Height;

            for (var i = 0; i < choices.Length; i++)
            {
                Size option = choices[i];
                if (option.Height == option.Width * h / w)
                {
                    if (option.Width >= textureViewWidth &&
                        option.Height >= textureViewHeight)
                    {
                        bigEnough.Add(option);
                    }
                    else if ((option.Width <= maxWidth) && (option.Height <= maxHeight))
                    {
                        notBigEnough.Add(option);
                    }
                }
            }

            // Pick the smallest of those big enough. If there is no one big enough, pick the
            // largest of those not big enough.
            if (bigEnough.Count > 0)
            {
                return (Size)Collections.Min(bigEnough, new CompareSizesByArea());
            }
            else if (notBigEnough.Count > 0)
            {
                return (Size)Collections.Max(notBigEnough, new CompareSizesByArea());
            }
            else
            {
                System.Diagnostics.Debug.WriteLine("Couldn't find any suitable preview size");
                return choices[0];
            }
        }

        private void OnOpened(CameraDevice cameraDevice)
        {
            this._cameraDevice = cameraDevice;
            _surfaceTextureView.SurfaceTexture.SetDefaultBufferSize(_previewSize.Width, _previewSize.Height);
            previewSurface = new Surface(_surfaceTextureView.SurfaceTexture);

            this._cameraDevice.CreateCaptureSession(new List<Surface> { previewSurface, _imageReader.Surface },
                                                   _captureStateSessionCallback, _backgroundHandler);
        }

        private void OnDisconnected(CameraDevice cameraDevice)
        {
            // In a real application we may need to handle the user disconnecting external devices.
            // Here we're only worrying about built-in cameras
        }

        private void OnError(CameraDevice cameraDevice, CameraError cameraError)
        {
            // In a real application we should handle errors gracefully
        }

        private void OnPreviewSessionConfigured(CameraCaptureSession session)
        {
            captureSession = session;

            previewRequestBuilder = _cameraDevice.CreateCaptureRequest(CameraTemplate.Preview);
            previewRequestBuilder.AddTarget(previewSurface);

            var availableAutoFocusModes = (int[])_characteristics.Get(CameraCharacteristics.ControlAfAvailableModes);
            if (availableAutoFocusModes.Any(afMode => afMode == (int)ControlAFMode.ContinuousPicture))
            {
                previewRequestBuilder.Set(CaptureRequest.ControlAfMode, (int)ControlAFMode.ContinuousPicture);
            }
            SetAutoFlash(previewRequestBuilder);

            //brightness, contrast, focus, 
            previewRequestBuilder.Set(CaptureRequest.ControlCaptureIntent, (int)ControlCaptureIntent.VideoRecord);
            previewRequestBuilder.Set(CaptureRequest.ControlAwbMode, (int)ControlAwbMode.Auto);
            previewRequestBuilder.Set(CaptureRequest.ControlAfMode, (int)ControlAFMode.ContinuousVideo);
            previewRequestBuilder.Set(CaptureRequest.ControlVideoStabilizationMode, (int)ControlVideoStabilizationMode.On);
            ////
            previewRequest = previewRequestBuilder.Build();

            captureSession.SetRepeatingRequest(previewRequest, _cameraCaptureCallback, _backgroundHandler);
        }

        /// For devices with orientation of 90, we simply return our mapping from orientations.
        /// For devices with orientation of 270, we need to rotate 180 degrees. 
        private int GetOrientation(int rotation)
        {
            return (_orientations.Get(rotation) + _sensorOrientation + 270) % 360;
        }

        #endregion


        #region Public helpers

        public void ConfigureTransform(int viewWidth, int viewHeight)// Configures the necessary matrix
        {
            if (null == _surfaceTextureView || null == _previewSize)
            {
                return;
            }
            var rotation = (int)WindowManager.DefaultDisplay.Rotation;
            Matrix matrix = new Matrix();
            RectF viewRect = new RectF(0, 0, viewWidth, viewHeight);
            RectF bufferRect = new RectF(0, 0, _previewSize.Height, _previewSize.Width);
            float centerX = viewRect.CenterX();
            float centerY = viewRect.CenterY();
            if ((int)SurfaceOrientation.Rotation90 == rotation || (int)SurfaceOrientation.Rotation270 == rotation)
            {
                bufferRect.Offset(centerX - bufferRect.CenterX(), centerY - bufferRect.CenterY());
                matrix.SetRectToRect(viewRect, bufferRect, Matrix.ScaleToFit.Fill);
                float scale = Math.Max((float)viewHeight / _previewSize.Height, (float)viewWidth / _previewSize.Width);
                matrix.PostScale(scale, scale, centerX, centerY);
                matrix.PostRotate(90 * (rotation - 2), centerX, centerY);
            }
            else if ((int)SurfaceOrientation.Rotation180 == rotation)
            {
                matrix.PostRotate(180, centerX, centerY);
            }

            //  matrix.SetScale(2f, 2f);

            _surfaceTextureView.SetTransform(matrix);

        }

        public void SetAutoFlash(CaptureRequest.Builder requestBuilder)
        {
            if (_flashSupported)
            {
                requestBuilder.Set(CaptureRequest.ControlAeMode, (int)ControlAEMode.OnAutoFlash);
            }
        }
        #endregion
    }
}