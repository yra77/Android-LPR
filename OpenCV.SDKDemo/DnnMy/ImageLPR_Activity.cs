
using OpenCV.Dnn;
using OpenCV.Core;
using OpenCV.ImgProc;
using Size = OpenCV.Core.Size;
using OpenCV.SDKDemo.Helpers;

using android1 = Android;
using android = Android.App;
using Android.Widget;
using Android.Graphics;

using System;
using System.Collections.Generic;


namespace OpenCV.SDKDemo.DnnMy
{
    class ImageLPR_Activity : android.Activity
    {

        private Net _net;
        private Net _netSymbol;


        public ImageLPR_Activity(ImageView imageView, TextView textView, Bitmap bitmap)
        {
            ReadNetFromCaffe();
            Start(imageView, textView, bitmap);
        }


        private void Start(ImageView imageView, TextView textView, Bitmap bitmap1)
        {

            List<Tuple<OpenCV.Core.Point, Mat>> plate = new List<Tuple<OpenCV.Core.Point, Mat>>();

            Mat frame = new Mat();
            OpenCV.Android.Utils.BitmapToMat(bitmap1, frame, true);
            OpenCV.ImgProc.Imgproc.CvtColor(frame, frame, Imgproc.ColorRgba2rgb);

            if (frame != null)
            {
                Detecting_LPR(ref _net, ref frame, ref plate);

                Bitmap bitmap = Bitmap.CreateBitmap(frame.Width(), frame.Height(), Bitmap.Config.Rgb565);
                OpenCV.ImgProc.Imgproc.CvtColor(frame, frame, Imgproc.ColorRgb2rgba);
                OpenCV.Android.Utils.MatToBitmap(frame, bitmap);

                imageView.SetImageDrawable(new android1.Graphics.Drawables.BitmapDrawable(bitmap));

                if (plate.Count > 0)
                {
                    string temp = "";

                    for (int i = 0; i < plate.Count; i++)
                    {
                        float confidence = 0f;
                        temp += Symbol_Recognition.SymbolRecognition(_netSymbol, plate[i].Item2, ref confidence);
                        temp += " - " + confidence.ToString("0.00") + "%\n";
                    }

                    textView.Text = temp;
                }
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

        private void Detecting_LPR(ref Net net, ref Mat frame3, ref List<Tuple<OpenCV.Core.Point, Mat>> plate)
        {

            Mat frame = new Mat();
            int siZe = 300;
            float w = siZe / (float)frame3.Width();
            float h = siZe / (float)frame3.Height();

            if (frame3.Width() < siZe || frame3.Height() < siZe)
            {
                Size sizeCV = new Size((float)frame3.Width() * w, (float)frame3.Height() * h);
                OpenCV.ImgProc.Imgproc.Resize(frame3, frame, sizeCV);
            }
            else
            {
                frame = frame3;
            }

            Mat tempMat = frame.Clone();
            int frameHeight = frame.Height();
            int frameWidth = frame.Width();

            Mat inputBlob = OpenCV.Dnn.Dnn.BlobFromImage(frame, 1.0 / 127.5, new Size(siZe, siZe), Scalar.All(127.5), true, false);

            net.SetInput(inputBlob);

            Mat detection = new Mat();

            try 
            {
                detection = net.Forward("detection_out");
            }
            catch (CvException e)
            {
                System.Console.WriteLine(e.Message);
                return;
            }

            detection = detection.Reshape(1, (int)detection.Total() / 7);

            for (int i = 0; i < detection.Rows(); i++)
            {
                
                var confidence = detection.Get(i, 2)[0];

                if (confidence > 0.15)
                {

                    int x1 = (int)(detection.Get(i, 3)[0] * frameWidth);
                    int y1 = (int)(detection.Get(i, 4)[0] * frameHeight);
                    int x2 = (int)(detection.Get(i, 5)[0] * frameWidth);
                    int y2 = (int)(detection.Get(i, 6)[0] * frameHeight);

                    Imgproc.Rectangle(frame, new OpenCV.Core.Point(x1, y1), new OpenCV.Core.Point(x2, y2), new Scalar(0, 255, 0), 3);

                    Core.Rect _cutImage;

                    x1 -= 4;
                    y1 -= 3;

                    _cutImage = new Core.Rect(new OpenCV.Core.Point(x1, y1), new OpenCV.Core.Point(x2 + 7, y2));

                    if (0 <= _cutImage.X && 0 <= _cutImage.Width && _cutImage.X + _cutImage.Width <= frame.Cols()
                        && 0 <= _cutImage.Y && 0 <= _cutImage.Height && _cutImage.Y + _cutImage.Height <= frame.Rows())
                    {
                        Mat temp = new Mat(tempMat, _cutImage);
                        plate.Add(new Tuple<OpenCV.Core.Point, Mat>(new OpenCV.Core.Point(_cutImage.X, _cutImage.Y), temp));
                    }
                }
            }

            frame3 = frame;
        }

    }
}