
using OpenCV.Core;
using OpenCV.Dnn;
using OpenCV.SDKDemo.Constants;

using System;
using System.Collections.Generic;


namespace OpenCV.SDKDemo.Helpers
{
    class Symbol_Recognition
    {
        public static string SymbolRecognition(Net _netSymbol, Mat plate, ref float confidance)
        {
            double conf = 0;
            string result = "";
            List<Tuple<int, char>> str = new List<Tuple<int, char>>();

            Mat frame = plate.Clone();

            int frameHeight = frame.Rows();
            int frameWidth = frame.Cols();

            Mat inputBlob = OpenCV.Dnn.Dnn.BlobFromImage(frame, 1.0 / 127.5, new Size(500, 500), Scalar.All(127.5), true, false);
            _netSymbol.SetInput(inputBlob);

            Mat detection = new Mat();

            try
            {
                detection = _netSymbol.Forward("detection_out");
            }
            catch (CvException e)
            {
                System.Console.WriteLine(e.Message);
                return "0";
            }

            detection = detection.Reshape(1, (int)detection.Total() / 7);

            for (int i = 0; i < detection.Rows(); i++)
            {
                double confidence = detection.Get(i, 2)[0];

                if (confidence > 0.3)
                {
                    int class_Index = (int)detection.Get(i, 1)[0];

                    int x1 = (int)(detection.Get(i, 3)[0] * frameWidth);
                    int y1 = (int)(detection.Get(i, 4)[0] * frameHeight);
                    int x2 = (int)(detection.Get(i, 5)[0] * frameWidth);
                    int y2 = (int)(detection.Get(i, 6)[0] * frameHeight);

                    Core.Rect t;
                    t = new Core.Rect(new OpenCV.Core.Point(x1, y1), new OpenCV.Core.Point(x2, y2));

                    if (0 <= t.X && 0 <= t.Width && t.X + t.Width <= frame.Cols()
                           && 0 <= t.Y && 0 <= t.Height && t.Y + t.Height <= frame.Rows())
                    {
                        if (class_Index != 0)
                        {
                            str.Add(new Tuple<int, char>(x1, Constant_LPR.CLASSES[class_Index]));
                            conf += confidence;
                        }
                    }
                }
                else
                {
                    break;
                }
            }
            // здесь переставляем символы по порядку слева на право 
            if (str.Count > 0)
            {
                for (int f = 0; f < str.Count; f++)
                {
                    for (int h = f; h < str.Count; h++)
                    {
                        if (str[f].Item1 > str[h].Item1)
                        {
                            int temp = str[f].Item1;
                            char buf = str[f].Item2;
                            str[f] = new Tuple<int, char>(str[h].Item1, str[h].Item2);
                            str[h] = new Tuple<int, char>(temp, buf);
                        }
                    }
                    result += str[f].Item2;
                }
            }

            confidance = ((float)Math.Round(conf * 100f) / 100f) * 10;

            return result;// + " - " + fc.ToString("0.00") + "%";
        }
    }
}