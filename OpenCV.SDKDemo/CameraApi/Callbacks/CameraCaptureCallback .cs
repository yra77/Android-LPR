
using Android.Hardware.Camera2;
using System;


namespace OpenCV.SDKDemo.CameraApi.Callbacks
{
    public class CameraCaptureCallback : CameraCaptureSession.CaptureCallback
    {

        public Action<CameraCaptureSession, CaptureRequest, TotalCaptureResult> CaptureCompleted;

        public Action<CameraCaptureSession, CaptureRequest, CaptureResult> CaptureProgressed;

        public override void OnCaptureCompleted(CameraCaptureSession session, CaptureRequest request, TotalCaptureResult result)
        {
            CaptureCompleted?.Invoke(session, request, result);
        }

        public override void OnCaptureProgressed(CameraCaptureSession session, CaptureRequest request, CaptureResult partialResult)
        {
            CaptureProgressed?.Invoke(session, request, partialResult);
        }
    }
}