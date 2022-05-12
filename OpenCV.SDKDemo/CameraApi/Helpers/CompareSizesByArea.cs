
using Java.Lang;
using Java.Util;
using static Android.Hardware.Camera;


namespace OpenCV.SDKDemo.CameraApi.Helpers
{
    public class CompareSizesByArea : Object, IComparator
    {
        public int Compare(Object lhs, Object rhs)
        {
            // We cast here to ensure the multiplications won't overflow
            if (lhs is Size lhsSize && rhs is Size rhsSize)
                return Long.Signum((long)lhsSize.Width * lhsSize.Height - (long)rhsSize.Width * rhsSize.Height);
            else
                return 0;
        }
    }
}