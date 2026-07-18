using System.Diagnostics;
// 版本号：v1.1
using System.Runtime.InteropServices;

namespace WeChatOcr;

public class ImageOcr : IDisposable
{
    private const uint MaxRetryTimes = 99;
    private OcrManager? _ocrManager;
    private GCHandle _ocrManagerHandle;
    private int _disposeState;

    public ImageOcr(string? path = default)
        : this(string.IsNullOrEmpty(path) ? new OcrManager() : new OcrManager(path))
    {
    }

    public ImageOcr(string componentDirectory, string ocrExecutablePath)
        : this(new OcrManager(componentDirectory, ocrExecutablePath))
    {
    }

    internal ImageOcr(OcrManager ocrManager)
    {
        _ocrManager = ocrManager;
        try
        {
            _ocrManagerHandle = GCHandle.Alloc(ocrManager);
            ocrManager.StartWeChatOcr(GCHandle.ToIntPtr(_ocrManagerHandle));
        }
        catch
        {
            try
            {
                ocrManager.Dispose();
            }
            finally
            {
                if (_ocrManagerHandle.IsAllocated)
                    _ocrManagerHandle.Free();
                _ocrManager = null;
            }

            throw;
        }
    }

    ~ImageOcr() => Dispose(false);

    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }

    public void Run(string imagePath, Action<string, WeChatOcrResult?>? callback)
    {
        var ocrManager = GetOcrManager();
        if (callback != null) ocrManager.SetOcrResultCallback(callback);
        var retryCount = 0;
        while (retryCount <= MaxRetryTimes)
            try
            {
                ocrManager.DoOcrTask(imagePath);
                return;
            }
            catch (OverflowException)
            {
                Thread.Sleep(10);
                retryCount++;
            }
            catch (Exception ex)
            {
                Debug.WriteLine(ex.Message);
                return;
            }
    }

    public void Run(byte[] bytes, Action<string, WeChatOcrResult?>? callback, ImageType imgType = ImageType.Png)
    {
        ThrowIfDisposed();
        var imgPath = Path.Combine(Path.GetTempPath(), $"generate4wechat.{imgType.ToString().ToLower()}");
        Utilities.WriteBytesToFile(imgPath, bytes);
        Run(imgPath, callback);
    }

    internal bool IsManagerHandleAllocated => _ocrManagerHandle.IsAllocated;

    private OcrManager GetOcrManager()
    {
        ThrowIfDisposed();
        return _ocrManager ?? throw new ObjectDisposedException(nameof(ImageOcr));
    }

    private void ThrowIfDisposed()
    {
        if (Volatile.Read(ref _disposeState) != 0)
            throw new ObjectDisposedException(nameof(ImageOcr));
    }

    private void Dispose(bool disposing)
    {
        if (Interlocked.Exchange(ref _disposeState, 1) != 0)
            return;

        try
        {
            _ocrManager?.Dispose();
        }
        catch when (!disposing)
        {
            // 终结器线程不能传播异常。
        }
        finally
        {
            if (_ocrManagerHandle.IsAllocated)
                _ocrManagerHandle.Free();
            _ocrManager = null;
        }
    }
}

public enum ImageType
{
    Png,
    Jpeg,
    Bmp
}
