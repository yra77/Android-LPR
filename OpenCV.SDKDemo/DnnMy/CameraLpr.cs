
using OpenCV.Dnn;
using OpenCV.Android;
using OpenCV.SDKDemo.Utilities;
using OpenCV.Core;
using OpenCV.ImgProc;
using Size = OpenCV.Core.Size;
using OpenCV.SDKDemo.Helpers;

using Android.Util;
using Android.OS;
using Android.Views;
using Android.App;
using Android.Content.PM;

using System;
using System.Collections.Generic;


namespace OpenCV.SDKDemo.DnnMy
{
    [Activity(ScreenOrientation = ScreenOrientation.Landscape, 
        ConfigurationChanges = ConfigChanges.KeyboardHidden | ConfigChanges.Orientation , Theme = "@style/MainTheme")]

    class CameraLpr : CameraActivity, ILoaderCallbackInterface, CameraBridgeViewBase.ICvCameraViewListener, View.IOnTouchListener
    {

        private CameraBridgeViewBase _openCvCameraView;
        private Net _net;
        private Net _netSymbol;
        private int _period = 0;
        private Core.Rect _cutImage;
        private int _xpos;
        private int _ypos;
        private int _xWidth;
        private int _yHeight;
        private int _x = 0;
        private int _y = 0;
        bool _isOneClick = false;
        private int _scale = 1;

        protected override IList<CameraBridgeViewBase> CameraViewList => new List<CameraBridgeViewBase>() { _openCvCameraView };


        #region Private helper

        private void Detect_LPR(ref Mat frame, ref Core.Rect rectLicenseNum)
        {
            int size = 300;
            int frameHeight = frame.Rows();
            int frameWidth = frame.Cols();

            Mat inputBlob = OpenCV.Dnn.Dnn.BlobFromImage(frame, 1.0 / 127.5, new Size(size, size), Scalar.All(127.5), true, false);

            _net.SetInput(inputBlob);

            Mat detection = new Mat();

            try
            {
                detection = _net.Forward("detection_out");
            }
            catch (CvException e)
            {
                System.Console.WriteLine(e.Message);
                return;
            }

            detection = detection.Reshape(1, (int)detection.Total() / 7);

            for (int i = 0; i < detection.Rows(); i++)
            {

                double confidence = detection.Get(i, 2)[0];

                if (confidence > 0.15)
                {

                    int x1 = (int)(detection.Get(i, 3)[0] * frameWidth);
                    int y1 = (int)(detection.Get(i, 4)[0] * frameHeight);
                    int x2 = (int)(detection.Get(i, 5)[0] * frameWidth);
                    int y2 = (int)(detection.Get(i, 6)[0] * frameHeight);

                    Imgproc.Rectangle(frame, new OpenCV.Core.Point(x1, y1), new OpenCV.Core.Point(x2, y2), new Scalar(0, 255, 0), 2);
                    rectLicenseNum = new Core.Rect(new OpenCV.Core.Point(x1, y1), new OpenCV.Core.Point(x2, y2));

                    Core.Rect _cutImage;

                    x1 -= 4;
                    y1 -= 3;

                    _cutImage = new Core.Rect(new OpenCV.Core.Point(x1, y1), new OpenCV.Core.Point(x2 + 7, y2));

                    if (0 <= _cutImage.X && 0 <= _cutImage.Width && _cutImage.X + _cutImage.Width <= frame.Cols()
                        && 0 <= _cutImage.Y && 0 <= _cutImage.Height && _cutImage.Y + _cutImage.Height <= frame.Rows())
                    {
                        float confidance = 0f;
                        string result = Symbol_Recognition.SymbolRecognition(_netSymbol, new Mat(frame, _cutImage), ref confidance);

                        Console.WriteLine("GGGGGGGG " + result + " conf = " + confidance);

                        OpenCV.ImgProc.Imgproc.PutText(frame, result, new OpenCV.Core.Point(x1 + 5, y1 - 5),
                            Imgproc.FontHersheySimplex, 1f, new Scalar(255, 0, 0), 3);
                    }
                }
                else
                    continue;
            }
        }

        private void ReadNetFromCaffe()
        {

            IModel_Read_Write model_Read_Write = new Model_Read_Write();

            _net = Dnn.Dnn.ReadNetFromCaffe(model_Read_Write.SaveToFile(name: "lpr_detect.prototxt"),
                                           model_Read_Write.SaveToFile(name2: "lprdetect.caffemodel"));
            _netSymbol = Dnn.Dnn.ReadNetFromCaffe(model_Read_Write.SaveToFile(name: "Short_9324.prototxt"),
                                                  model_Read_Write.SaveToFile(name2: "Short_2000.caffemodel"));

        }

        #endregion


        #region Interface implamentation

        protected override void OnCreate(Bundle savedInstanceState)
        {
            base.OnCreate(savedInstanceState);

            ReadNetFromCaffe();

            Window.AddFlags(WindowManagerFlags.KeepScreenOn);          
            SetContentView(Resource.Layout.CameraPreview);
            _openCvCameraView = FindViewById<CameraBridgeViewBase>(Resource.Id.surfaceView);
            _openCvCameraView.Visibility = ViewStates.Visible;
            _openCvCameraView.SetCvCameraViewListener(this);

            _openCvCameraView.FocusableInTouchMode = true;
            _openCvCameraView.SetOnTouchListener(this);
        }

        protected override void OnPause()
        {
            base.OnPause();
            if (_openCvCameraView != null)
            {
                _openCvCameraView.DisableView();
            }
        }

        protected override void OnResume()
        {
            base.OnResume();

            if (!OpenCVLoader.InitDebug())
            {
                Log.Debug(ActivityTags.CameraPreview, "Internal OpenCV library not found. Using OpenCV Manager for initialization");
                OpenCVLoader.InitAsync(OpenCVLoader.OpencvVersion300, this, this);
            }
            else
            {
                Log.Debug(ActivityTags.CameraPreview, "OpenCV library found inside package. Using it!");
                OnManagerConnected(LoaderCallbackInterface.Success);
            }
        }

        protected override void OnDestroy()
        {
            base.OnDestroy();
            if (_openCvCameraView != null)
            {
                _openCvCameraView.DisableView();
            }
        }

        public void OnManagerConnected(int p0)
        {
            switch (p0)
            {
                case LoaderCallbackInterface.Success:
                   // Log.Info(ActivityTags.CameraPreview, "OpenCV loaded successfully");
                    _openCvCameraView.EnableView();
                    break;
                default:
                    break;
            }
        }

        public void OnPackageInstall(int p0, IInstallCallbackInterface p1)
        {
        }
        
        public void OnCameraViewStarted(int p0, int p1)
        {
            _xWidth = p0;
            _yHeight = p1;
        }

        public void OnCameraViewStopped()
        {
        }

        public Mat OnCameraFrame(Mat frame)
        {
           
            if (_period == 1)
            {
                Core.Rect rectLicenseNum = new Core.Rect();

                //if (_x != 0 && _y != 0)
                //{
                //    t = new Core.Rect(new OpenCV.Core.Point(_x - 150.0, _y - 150.0), new Core.Point(_x + 150.0, _y + 150.0));
                   
                //    Mat _frame = new Mat(frame, t);

                //    Imgproc.Resize(_frame, _frame, new Size(_frame.Width() * _scale, _frame.Height() * _scale));

                //    OpenCV.ImgProc.Imgproc.CvtColor(_frame, _frame, Imgproc.ColorRgba2rgb);

                //    Detect_LPR(ref _frame, ref tt);

                //    int n = (_x - 150 + tt.X / _frame.Width() * _scale);
                //    int n2 = (_y - 150 + tt.Y / _frame.Height() * _scale);

                //    Imgproc.Rectangle(frame, new Core.Rect(n, n2, tt.Width / _scale, tt.Height / _scale), new Scalar(0, 255, 0), 2);

                //    _isOneClick = false;
                //}
                //else
                //{
                    OpenCV.ImgProc.Imgproc.CvtColor(frame, frame, Imgproc.ColorRgba2rgb);
                    Detect_LPR(ref frame, ref rectLicenseNum);
               // }

                _period = 0;
            
            }

            _period++;

            return frame;
        }


        public bool OnTouch(View v, MotionEvent e)
        {
            //if (!_isOneClick)
            //{
            //    _isOneClick = true;
            //    _scale++;
            //    _openCvCameraView.ScaleX = _scale;
            //    _openCvCameraView.ScaleY = _scale;

            //    v.FocusableInTouchMode = true;

            //    xpos = (v.Width - _xWidth) / 2;
            //    _x = (int)e.GetX() - xpos;

            //    ypos = (v.Height - _yHeight) / 2;
            //    _y = (int)e.GetY() - ypos;

            //    Console.WriteLine("QQQQQQQ" + _x + " " + _y);
            //}
            return true;
        }

        #endregion

    }
}