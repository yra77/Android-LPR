

using OpenCV.SDKDemo.DnnMy;

using android1 = Android;
using Android.App;
using Android.Content;
using Android.OS;
using Android.Widget;
using Android.Graphics;
using Android.Content.PM;

using System;
using OpenCV.SDKDemo.Helpers;
using System.Collections.Generic;
using Android;


namespace OpenCV.SDKDemo
{

    [Activity(ScreenOrientation = ScreenOrientation.Portrait, Theme = "@style/MainTheme", 
        ConfigurationChanges = ConfigChanges.Locale | ConfigChanges.ScreenSize | ConfigChanges.Orientation | 
        ConfigChanges.UiMode | ConfigChanges.ScreenLayout | ConfigChanges.SmallestScreenSize)]

    public class MainActivity : Activity
    {
       
        private IModel_Read_Write _model_Read_Write;
        private LinearLayout _fotoViewLiner;

        protected override void OnActivityResult(int requestCode, Result resultCode, Intent data)
        {
            if ((requestCode == 1000) && (resultCode == Result.Ok) && (data != null))
            {
                android1.Net.Uri uri = data.Data;
                ImageView imageView = FindViewById<ImageView>(Resource.Id.imageView);
                TextView textView = FindViewById<TextView>(Resource.Id.textView);
                //  imageView.SetImageURI(uri);
                Bitmap bitmap = android1.Provider.MediaStore.Images.Media.GetBitmap(this.ContentResolver, uri);

                _ = new ImageLPR_Activity(imageView, textView, bitmap);
            }
        }

        protected override void OnCreate(Bundle bundle)
        {
            base.OnCreate(bundle);

            Xamarin.Essentials.Platform.Init(this, bundle);
            Xamarin.Forms.Forms.Init(this, bundle);

            OpenCV.Android.OpenCVLoader.InitDebug();
    
            SetContentView(Resource.Layout.Main);

            FindViewById<Button>(Resource.Id.fotoBtn).Click += FotoClick;
            FindViewById<Button>(Resource.Id.cameraBtn).Click += CameraClick;
            FindViewById<Button>(Resource.Id.opencvBtn).Click += OpenCvClick;

            _fotoViewLiner = FindViewById<LinearLayout>(Resource.Id.fotoViewLiner);
            _fotoViewLiner.Visibility = android1.Views.ViewStates.Invisible;

            _model_Read_Write = new Model_Read_Write(this);
            _model_Read_Write.ReadFromAsset();
        }

        private void OpenCvClick(object sender, EventArgs e)
        {
            _fotoViewLiner.Visibility = android1.Views.ViewStates.Invisible;
            StartActivity(typeof(CameraLpr));
        }

        private void FotoClick(object s, EventArgs e)
        {
            Intent intent = new Intent(Intent.ActionPick, android1.Provider.MediaStore.Images.Media.ExternalContentUri);
            StartActivityForResult(intent, 1000);//Вызов галереи

            _fotoViewLiner.Visibility = android1.Views.ViewStates.Visible;
        }

        private void CameraClick(object s, EventArgs e)
        {
            _fotoViewLiner.Visibility = android1.Views.ViewStates.Invisible;

              StartActivity(typeof(CameraApi.CameraApiActivity));
        }

    }
}

