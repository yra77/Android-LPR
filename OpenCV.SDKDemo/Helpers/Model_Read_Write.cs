
using Android.App;
using Android.Content.Res;
using System.IO;
using Xamarin.Essentials;


namespace OpenCV.SDKDemo.Helpers
{
    class Model_Read_Write : IModel_Read_Write
    {

        private readonly AssetManager _assets;


        public Model_Read_Write() {}

        public Model_Read_Write(Activity activity)
        {
           _assets = activity.Assets;
        }

        public void ReadFromAsset()
        {
            string content = "";
            byte[] content2 = { };
            string contentSymbol = "";
            byte[] contentSymbol2 = { };

            //For Lpr
            using (StreamReader sr = new StreamReader(_assets.Open("Models/lpr_detect.prototxt")))
            {
                content = sr.ReadToEnd();
            }


            using (BinaryReader br = new BinaryReader(_assets.Open("Models/lprdetect.caffemodel")))
            {
                content2 = br.ReadBytes(13000000);
            }

            //For symbol
            using (StreamReader sr = new StreamReader(_assets.Open("Models/Short_9324.prototxt")))
            {
                contentSymbol = sr.ReadToEnd();
            }

            using (BinaryReader br = new BinaryReader(_assets.Open("Models/Short_2000.caffemodel")))
            {
                contentSymbol2 = br.ReadBytes(17500000);
            }

            SaveToFile("lpr_detect.prototxt", content); 
            SaveToFile("lprdetect.caffemodel", content2);
            SaveToFile("Short_9324.prototxt", contentSymbol);
            SaveToFile("Short_2000.caffemodel", contentSymbol2);

        }

        public string SaveToFile(string name, string bitmapImg = null)
        {
            var path = System.IO.Path.Combine(FileSystem.AppDataDirectory, name);
            if (!System.IO.File.Exists(path))
            {
                System.IO.File.WriteAllText(path, bitmapImg);
            }
            return path;
        }

        public string SaveToFile(string name2, byte[] bitmapImg = null)
        {
            var path = System.IO.Path.Combine(FileSystem.AppDataDirectory, name2);
            if (!System.IO.File.Exists(path))
            {
                System.IO.File.WriteAllBytes(path, bitmapImg);
            }
           return path;
        }

    }
}