
using Android.Content;
using Android.Graphics;
using Android.Hardware.Camera2;
using Android.Media;
using android = Android;

using System;
using System.IO;


namespace OpenCV.SDKDemo.CameraApi
{
    public partial class CameraApiActivity
    {

        private MediaCaptorState state = MediaCaptorState.Preview;

        private enum MediaCaptorState
        {
            Preview,
            WaitingLock,
            WaitingPrecapture,
            WaitingNonPrecapture,
            PictureTaken,
        }

        private void TakePictureButton_Click(object sender, EventArgs e)
        {
            LockFocus();
        }
  
        private void LockFocus()// Lock the focus as the first step for a still image capture.
        {
            try
            {
                int[] availableAutoFocusModes = (int[])_characteristics.Get(CameraCharacteristics.ControlAfAvailableModes);

                // Set autofocus if supported
                //if (availableAutoFocusModes.Any(afMode => afMode != (int)ControlAFMode.Off))
               // {
                    previewRequestBuilder.Set(CaptureRequest.ControlAfTrigger, (int)ControlAFTrigger.Start);
                    state = MediaCaptorState.WaitingLock;
                    // Tell cameraCaptureCallback to wait for the lock.
                    captureSession.Capture(previewRequestBuilder.Build(), _cameraCaptureCallback,
                            _backgroundHandler);
               // }
               // else
               // {
                    CaptureStillPicture();
               // }
            }
            catch (CameraAccessException e)
            {
                Console.WriteLine("CreateFoto error lock focus picture");
                e.PrintStackTrace();
            }
        }

        private void ProcessImageCapture(CaptureResult result)
        {
            switch (state)
            {
                case MediaCaptorState.WaitingLock:
                    {
                        int? afState = (int?)result.Get(CaptureResult.ControlAfState);

                        if (afState == null)
                        {
                            CaptureStillPicture();
                        }
                        else if ((((int)ControlAFState.FocusedLocked) == afState.Value) ||
                                   (((int)ControlAFState.NotFocusedLocked) == afState.Value))
                        {
                            int? aeState = (int?)result.Get(CaptureResult.ControlAeState);

                            if (aeState == null || aeState.Value == ((int)ControlAEState.Converged))
                            {
                                state = MediaCaptorState.PictureTaken;
                                CaptureStillPicture();
                            }
                            else
                            {
                                RunPrecaptureSequence();
                            }
                        }
                        break;
                    }
                case MediaCaptorState.WaitingPrecapture:
                    {
                        int? aeState = (int?)result.Get(CaptureResult.ControlAeState);

                        if (aeState == null ||
                                aeState.Value == ((int)ControlAEState.Precapture) ||
                                aeState.Value == ((int)ControlAEState.FlashRequired))
                        {
                            state = MediaCaptorState.WaitingNonPrecapture;
                        }
                        break;
                    }
                case MediaCaptorState.WaitingNonPrecapture:
                    {
                        int? aeState = (int?)result.Get(CaptureResult.ControlAeState);

                        if (aeState == null || aeState.Value != ((int)ControlAEState.Precapture))
                        {
                            state = MediaCaptorState.PictureTaken;
                            CaptureStillPicture();
                        }
                        break;
                    }

            }
        }

        public void RunPrecaptureSequence()
        {
            try
            {
                // This is how to tell the camera to trigger.
                previewRequestBuilder.Set(CaptureRequest.ControlAePrecaptureTrigger, (int)ControlAEPrecaptureTrigger.Start);
                // Tell captureCallback to wait for the precapture sequence to be set.
                state = MediaCaptorState.WaitingPrecapture;
                captureSession.Capture(previewRequestBuilder.Build(), _cameraCaptureCallback, _backgroundHandler);
            }
            catch (CameraAccessException e)
            {
                e.PrintStackTrace();
            }
        }

        public void CaptureStillPicture()
        {
            try
            {
                if (null == _cameraDevice)
                {
                    return;
                }

                // This is the CaptureRequest.Builder that we use to take a picture.
                CaptureRequest.Builder stillCaptureBuilder = _cameraDevice.CreateCaptureRequest(CameraTemplate.StillCapture);

                stillCaptureBuilder.AddTarget(_imageReader.Surface);

                // Use the same AE and AF modes as the preview.
                stillCaptureBuilder.Set(CaptureRequest.ControlAfMode, (int)ControlAFMode.ContinuousPicture);
                SetAutoFlash(stillCaptureBuilder);

                // Orientation
                int rotation = (int)WindowManager.DefaultDisplay.Rotation;
                int orientation = GetOrientation(rotation);

                stillCaptureBuilder.Set(CaptureRequest.JpegOrientation, orientation);

                captureSession.StopRepeating();
                captureSession.AbortCaptures();
                captureSession.Capture(stillCaptureBuilder.Build(), _cameraCaptureCallback, null);

                // Play shutter sound to alert user that image was captured
                AudioManager am = (AudioManager)GetSystemService(AudioService);

                if (am != null && am.RingerMode == RingerMode.Normal)
                {
                    MediaActionSound cameraSound = new MediaActionSound();
                    cameraSound.Load(MediaActionSoundType.ShutterClick);
                    cameraSound.Play(MediaActionSoundType.ShutterClick);
                }
            }
            catch (CameraAccessException e)
            {
                Console.WriteLine("CaptureStillPicture error");
                e.PrintStackTrace();
            }
        }

        private void HandleImageCaptured(ImageReader imageReader)
        {
            Java.IO.FileOutputStream fos = null;
            Java.IO.File imageFile = null;
            bool photoSaved = false;

            try
            {
                Image image = imageReader.AcquireLatestImage();
                Java.Nio.ByteBuffer buffer = image.GetPlanes()[0].Buffer;
                byte[] data = new byte[buffer.Remaining()];
                buffer.Get(data);
                Bitmap bitmap = BitmapFactory.DecodeByteArray(data, 0, data.Length);
                bool widthGreaterThanHeight = bitmap.Width > bitmap.Height;
                image.Close();

                string imageFileName = Guid.NewGuid().ToString();
                Java.IO.File storageDir = android.OS.Environment.GetExternalStoragePublicDirectory(android.OS.Environment.DirectoryPictures);

                string storageFilePath = storageDir + Java.IO.File.Separator + "OpenCV.SDKDemo.OpenCV.SDKDemo" + Java.IO.File.Separator + "Photos";
                Java.IO.File folder = new Java.IO.File(storageFilePath);

                if (!folder.Exists())
                {
                    folder.Mkdirs();
                }

                imageFile = new Java.IO.File(storageFilePath + Java.IO.File.Separator + imageFileName + ".jpg");

                if (imageFile.Exists())
                {
                    imageFile.Delete();
                }

                if (imageFile.CreateNewFile())
                {
                    fos = new Java.IO.FileOutputStream(imageFile);
                    using (MemoryStream stream = new MemoryStream())
                    {
                        if (bitmap.Compress(Bitmap.CompressFormat.Jpeg, 100, stream))
                        {
                            //We set the data array to the rotated bitmap. 
                            data = stream.ToArray();
                            fos.Write(data);
                        }
                        else
                        {
                            //something went wrong, let's just save the bitmap without rotation.
                            fos.Write(data);
                        }

                        stream.Close();
                        photoSaved = true;
                    }
                }
            }
            catch (Exception)
            {
                // In a real application we would handle this gracefully, likely alerting the user to the error
            }
            finally
            {
                if (fos != null)
                {
                    fos.Close();
                }

                RunOnUiThread(UnlockFocus);
            }

            // Request that Android display our image if we successfully saved it
            if (imageFile != null && photoSaved)
            {
                Intent intent = new Intent(Intent.ActionView);
                android.Net.Uri imageUri = android.Net.Uri.Parse("file://" + imageFile.AbsolutePath);
                intent.SetDataAndType(imageUri, "image/*");

                StartActivity(intent);
            }
        }

        private void UnlockFocus()
        {
            try
            {
                // Reset the auto-focus trigger
                previewRequestBuilder.Set(CaptureRequest.ControlAfTrigger, (int)ControlAFTrigger.Cancel);
                SetAutoFlash(previewRequestBuilder);
                captureSession.Capture(previewRequestBuilder.Build(), _cameraCaptureCallback,
                        _backgroundHandler);
                // After this, the camera will go back to the normal state of preview.
                state = MediaCaptorState.Preview;
                captureSession.SetRepeatingRequest(previewRequest, _cameraCaptureCallback,
                        _backgroundHandler);
            }
            catch (CameraAccessException e)
            {
                e.PrintStackTrace();
            }
        }
    }
}