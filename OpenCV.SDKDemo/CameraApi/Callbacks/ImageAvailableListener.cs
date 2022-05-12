
using Android.Media;
using System;


namespace OpenCV.SDKDemo.CameraApi.Callbacks
{
    public class ImageAvailableListener : Java.Lang.Object, ImageReader.IOnImageAvailableListener
    {
        public Action<ImageReader> ImageAvailable;

        public void OnImageAvailable(ImageReader reader)
        {
            ImageAvailable?.Invoke(reader);
        }
    }
}