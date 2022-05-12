

namespace OpenCV.SDKDemo.Helpers
{
    public interface IModel_Read_Write
    {
        void ReadFromAsset();
        string SaveToFile(string name, byte[] bitmapImg = null);
        string SaveToFile(string name2, string bitmapImg = null);
    }
}