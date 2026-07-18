using Google.Protobuf;
using OcrProtobuf;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using WeChatOcr;
using Xunit;

[assembly: CollectionBehavior(DisableTestParallelization = true)]

namespace WeChatOcr.Tests;

public sealed class ResourceOwnershipTests : IDisposable
{
    private readonly string _testRoot;

    public ResourceOwnershipTests()
    {
        _testRoot = Path.Combine(Path.GetTempPath(), "WeChatOcr.Tests", Guid.NewGuid().ToString("N"));
        var dataDirectory = Path.Combine(_testRoot, "wco_data");
        Directory.CreateDirectory(dataDirectory);
        File.WriteAllBytes(Path.Combine(dataDirectory, "WeChatOCR.exe"), [0]);
        DataLocation.SetBaseDirectory(_testRoot);
    }

    [Fact]
    public void Dispose_ReleasesManagerHandle_AndIsIdempotent()
    {
        var api = new FakeMmmojoApi();
        var runtime = new MmmojoRuntime(api);
        var manager = new OcrManager(null, api, runtime);
        var imageOcr = new ImageOcr(manager);

        Assert.True(imageOcr.IsManagerHandleAllocated);
        imageOcr.Dispose();
        imageOcr.Dispose();

        Assert.False(imageOcr.IsManagerHandleAllocated);
        Assert.Equal(1, api.InitializeCount);
        Assert.Equal(1, api.StopEnvironmentCount);
        Assert.Equal(1, api.RemoveEnvironmentCount);
        Assert.Equal(0, api.ShutdownCount);
        Assert.Equal(0, runtime.LeaseCount);
        Assert.Throws<ObjectDisposedException>(() => imageOcr.Run("unused.png", null));

        runtime.Dispose();
        Assert.Equal(1, api.ShutdownCount);
    }

    [Fact]
    public void Dispose_AllowsManagerToBeCollected()
    {
        var weakReference = CreateAndDisposeImageOcr();

        ForceFullCollection();

        Assert.False(weakReference.IsAlive);
    }

    [Fact]
    public void ConstructorFailure_RollsBackAllResources()
    {
        var api = new FakeMmmojoApi { ThrowOnStart = true };
        var runtime = new MmmojoRuntime(api);
        var manager = new OcrManager(null, api, runtime);

        Assert.Throws<InvalidOperationException>(() => new ImageOcr(manager));

        Assert.Equal(1, api.InitializeCount);
        Assert.Equal(0, api.StopEnvironmentCount);
        Assert.Equal(1, api.RemoveEnvironmentCount);
        Assert.Equal(0, api.ShutdownCount);
        Assert.Equal(0, manager.NativeArgumentCount);
        Assert.Equal(0, runtime.LeaseCount);

        runtime.Dispose();
        Assert.Equal(1, api.ShutdownCount);
    }

    [Fact]
    public void MultipleManagers_ShareGlobalRuntimeUntilLastDispose()
    {
        var api = new FakeMmmojoApi();
        var runtime = new MmmojoRuntime(api);
        var first = new ImageOcr(new OcrManager(null, api, runtime));
        var second = new ImageOcr(new OcrManager(null, api, runtime));

        Assert.Equal(1, api.InitializeCount);
        Assert.Equal(2, runtime.LeaseCount);

        first.Dispose();
        Assert.Equal(0, api.ShutdownCount);
        Assert.Equal(1, runtime.LeaseCount);

        second.Dispose();
        Assert.Equal(0, api.ShutdownCount);
        Assert.Equal(0, runtime.LeaseCount);

        runtime.Dispose();
        Assert.Equal(1, api.ShutdownCount);
    }

    [Fact]
    public void ReadPush_AlwaysRemovesShortReadInfo()
    {
        var api = new FakeMmmojoApi { ReadData = new byte[10] };
        using var manager = new OcrManager(null, api, new MmmojoRuntime(api));

        manager.OCRReadOnPush(1, new IntPtr(123), new IntPtr(456));

        Assert.Equal(1, api.RemoveReadInfoCount);
    }

    [Fact]
    public void CallbackFailure_ReleasesReadInfoAndTaskState()
    {
        var response = new OcrResponse
        {
            TaskId = 0,
            OcrResult = new OcrResponse.Types.OcrResult()
        };
        response.OcrResult.SingleResult.Add(new OcrResponse.Types.OcrResult.Types.SingleResult
        {
            SingleStrUtf8 = ByteString.CopyFromUtf8(new string('A', 32))
        });

        var api = new FakeMmmojoApi { ReadData = response.ToByteArray() };
        using var manager = new OcrManager(null, api, new MmmojoRuntime(api));
        using var imageOcr = new ImageOcr(manager);
        manager.SendOcrTask(0, "test.png");
        manager.SetOcrResultCallback((_, _) => throw new InvalidOperationException("callback failed"));

        var exception = Record.Exception(() =>
            manager.OCRReadOnPush(1, new IntPtr(123), new IntPtr(456)));

        Assert.Null(exception);
        Assert.Equal(1, api.RemoveReadInfoCount);
        Assert.Equal(0, manager.TrackedImageCount);
    }

    public void Dispose()
    {
        DataLocation.SetBaseDirectory(".");
        if (Directory.Exists(_testRoot))
            Directory.Delete(_testRoot, recursive: true);
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static WeakReference CreateAndDisposeImageOcr()
    {
        var api = new FakeMmmojoApi();
        var runtime = new MmmojoRuntime(api);
        var manager = new OcrManager(null, api, runtime);
        var weakReference = new WeakReference(manager);
        var imageOcr = new ImageOcr(manager);
        imageOcr.Dispose();
        return weakReference;
    }

    private static void ForceFullCollection()
    {
        GC.Collect();
        GC.WaitForPendingFinalizers();
        GC.Collect();
    }

    private sealed class FakeMmmojoApi : IMmmojoApi
    {
        private int _nextHandle = 10;
        private readonly Dictionary<IntPtr, IntPtr> _writeBuffers = [];
        private readonly Dictionary<IntPtr, IntPtr> _readBuffers = [];

        internal bool ThrowOnStart { get; init; }
        internal byte[] ReadData { get; init; } = [];
        internal int InitializeCount { get; private set; }
        internal int ShutdownCount { get; private set; }
        internal int StopEnvironmentCount { get; private set; }
        internal int RemoveEnvironmentCount { get; private set; }
        internal int RemoveReadInfoCount { get; private set; }

        public void Initialize() => InitializeCount++;

        public void Shutdown() => ShutdownCount++;

        public IntPtr CreateEnvironment() => NextHandle();

        public void SetEnvironmentCallback(IntPtr environment, int type, IntPtr callback)
        {
        }

        public void SetEnvironmentInitParam(IntPtr environment, int type, IntPtr parameter)
        {
        }

        public void AppendSubProcessSwitch(IntPtr environment, IntPtr switchName, IntPtr value)
        {
        }

        public void StartEnvironment(IntPtr environment)
        {
            if (ThrowOnStart)
                throw new InvalidOperationException("start failed");
        }

        public void StopEnvironment(IntPtr environment) => StopEnvironmentCount++;

        public void RemoveEnvironment(IntPtr environment) => RemoveEnvironmentCount++;

        public IntPtr CreateWriteInfo(int method, int sync, uint requestId) => NextHandle();

        public IntPtr GetWriteInfoRequest(IntPtr writeInfo, uint requestDataSize)
        {
            var buffer = Marshal.AllocHGlobal(checked((int)requestDataSize));
            _writeBuffers[writeInfo] = buffer;
            return buffer;
        }

        public bool SendWriteInfo(IntPtr environment, IntPtr writeInfo)
        {
            FreeBuffer(_writeBuffers, writeInfo);
            return true;
        }

        public void RemoveWriteInfo(IntPtr writeInfo) => FreeBuffer(_writeBuffers, writeInfo);

        public IntPtr GetReadInfoRequest(IntPtr readInfo, ref uint requestDataSize)
        {
            requestDataSize = (uint)ReadData.Length;
            var buffer = Marshal.AllocHGlobal(Math.Max(ReadData.Length, 1));
            if (ReadData.Length > 0)
                Marshal.Copy(ReadData, 0, buffer, ReadData.Length);
            _readBuffers[readInfo] = buffer;
            return buffer;
        }

        public IntPtr GetReadInfoAttach(IntPtr readInfo, ref uint attachDataSize)
        {
            attachDataSize = 0;
            return IntPtr.Zero;
        }

        public void RemoveReadInfo(IntPtr readInfo)
        {
            RemoveReadInfoCount++;
            FreeBuffer(_readBuffers, readInfo);
        }

        private IntPtr NextHandle() => new(Interlocked.Increment(ref _nextHandle));

        private static void FreeBuffer(Dictionary<IntPtr, IntPtr> buffers, IntPtr key)
        {
            if (!buffers.Remove(key, out var buffer))
                return;
            Marshal.FreeHGlobal(buffer);
        }
    }
}
