using Android.App;
using Android.Content;
using Android.OS;
using Android.Runtime;
using Android.Views;
using Android.Widget;
using OpenCV.Dnn;
using OpenCV.SDKDemo.Helpers;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;


namespace OpenCV.SDKDemo.DnnMy
{
    class Camera2_LPR
    {

        private Net _net;
        private Net _netSymbol;

        public Camera2_LPR()
        {
            ReadNetFromCaffe();
        }

        public void Detect_LPR(ref Core.Mat frame, ref Dictionary<Core.Rect, string> rectLicenseNums)//, ref string text)
        {
            int size = 300;
            int frameHeight = frame.Rows();
            int frameWidth = frame.Cols();

            Core.Mat inputBlob = OpenCV.Dnn.Dnn.BlobFromImage(frame, 1.0 / 127.5, new Core.Size(size, size), Core.Scalar.All(127.5), true, false);

            _net.SetInput(inputBlob);

            Core.Mat detection = new Core.Mat();

            try
            {
                detection = _net.Forward("detection_out");
            }
            catch (Core.CvException e)
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

                    Core.Rect _cutImage;

                    x1 -= 4;
                    y1 -= 3;

                    _cutImage = new Core.Rect(new OpenCV.Core.Point(x1, y1), new OpenCV.Core.Point(x2 + 7, y2));

                    if (0 <= _cutImage.X && 0 <= _cutImage.Width && _cutImage.X + _cutImage.Width <= frame.Cols()
                        && 0 <= _cutImage.Y && 0 <= _cutImage.Height && _cutImage.Y + _cutImage.Height <= frame.Rows())
                    {
                        float confidance = 0f;
                        string text = Symbol_Recognition.SymbolRecognition(_netSymbol, new Core.Mat(frame, _cutImage), ref confidance);

                        rectLicenseNums.Add(new Core.Rect(new OpenCV.Core.Point(x1, y1), new OpenCV.Core.Point(x2, y2)), text);
                    }
                }
                else
                {
                    continue;
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

    }
}