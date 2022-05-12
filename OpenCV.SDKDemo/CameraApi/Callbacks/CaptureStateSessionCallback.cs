
using Android.Hardware.Camera2;
using System;


namespace OpenCV.SDKDemo.CameraApi.Callbacks
{
    public class CaptureStateSessionCallback : CameraCaptureSession.StateCallback
    {

        public Action<CameraCaptureSession> Failed;
        public Action<CameraCaptureSession> Configured;

        public override void OnConfigured(CameraCaptureSession session)
        {
            Configured?.Invoke(session);
        }

        public override void OnConfigureFailed(CameraCaptureSession session)
        {
            Failed?.Invoke(session);
        }
    }
}