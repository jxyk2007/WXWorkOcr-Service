using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Windows.Media.Imaging;

namespace WeChatOcr.Sample;

public class ImageUtilities
{
    public static byte[] ConvertBitmap2Bytes(Bitmap bitmap, ImageFormat imageFormat)
    {
        using var stream = new MemoryStream();
        using (bitmap)
        {
            bitmap.Save(stream, imageFormat);
        }

        var data = new byte[stream.Length];
        stream.Seek(0, SeekOrigin.Begin);
        stream.Read(data, 0, Convert.ToInt32(stream.Length));
        return data;
    }

    public static byte[] ConvertBitmapSource2Bytes(BitmapSource bitmapSource, BitmapEncoder encoder)
    {
        // 将BitmapSource转换为byte[]
        encoder.Frames.Add(BitmapFrame.Create(bitmapSource));

        using var stream = new MemoryStream();
        encoder.Save(stream);
        return stream.ToArray();
    }

    public static string CountSize(long size)
    {
        var result = "";
        long factSize = 0;
        factSize = size;
        if (factSize < 1024.00)
            result = factSize.ToString("F2") + " Byte";
        else if (factSize >= 1024.00 && factSize < 1048576)
            result = (factSize / 1024.00).ToString("F2") + " KB";
        else if (factSize >= 1048576 && factSize < 1073741824)
            result = (factSize / 1024.00 / 1024.00).ToString("F2") + " MB";
        else if (factSize >= 1073741824)
            result = (factSize / 1024.00 / 1024.00 / 1024.00).ToString("F2") + " GB";
        return result;
    }
}