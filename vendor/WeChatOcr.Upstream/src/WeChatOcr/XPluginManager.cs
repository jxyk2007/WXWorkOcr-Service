using System.Runtime.InteropServices;

namespace WeChatOcr;

/// <summary>
///     原理参考：<see href="https://bbs.kanxue.com/thread-278161.htm#msg_header_h2_1"/>
/// </summary>
[StructLayout(LayoutKind.Sequential)]
public class XPluginManager : IDisposable
{
    private readonly object _lifecycleLock = new();
    private readonly IMmmojoApi _native;
    private readonly MmmojoRuntime _runtime;
    private bool _isMmmojoEnvInited;
    private bool _disposed;
    private IntPtr _mmmojoEnvironment = IntPtr.Zero;
    private IntPtr _userData;
    private string? _ocrExePath;
    private List<string> _commandLines = [];
    private readonly DefaultCallbacks _defaultCallbacks = new();
    private readonly Dictionary<string, Delegate> _callbacks = [];
    private readonly Dictionary<string, string> _switchNative = [];
    private readonly List<IntPtr> _nativeArguments = [];
    private IDisposable? _runtimeLease;

    public XPluginManager()
        : this(MmmojoApi.Instance, MmmojoRuntime.Shared)
    {
    }

    internal XPluginManager(IMmmojoApi native, MmmojoRuntime runtime)
    {
        _native = native;
        _runtime = runtime;
    }

    ~XPluginManager()
    {
        try
        {
            Dispose(false);
        }
        catch
        {
            // 终结器线程不能传播异常。
        }
    }

    public void SetExePath(string exePath = "")
    {
        ThrowIfDisposed();
        if (string.IsNullOrEmpty(exePath))
            exePath = DataLocation.WeChatOcrData;
        const string ocrExeName = "WeChatOCR.exe";
        if (!exePath.EndsWith(ocrExeName, StringComparison.OrdinalIgnoreCase) && Directory.Exists(exePath))
            exePath = Path.Combine(exePath, ocrExeName);
        if (!File.Exists(exePath)) throw new Exception($"指定的 {ocrExeName} 路径不存在!");

        lock (_lifecycleLock)
        {
            if (_isMmmojoEnvInited)
                throw new InvalidOperationException("MMMojo 环境启动后不能更改可执行文件路径。");
            _ocrExePath = exePath;
        }
    }

    public void AppendSwitchNativeCmdLine(string arg, string value)
    {
        ThrowIfDisposed();
        lock (_lifecycleLock)
        {
            if (_isMmmojoEnvInited)
                throw new InvalidOperationException("MMMojo 环境启动后不能更改启动参数。");
            _switchNative[arg] = value;
        }
    }

    public void SetCommandLine(List<string> cmdline)
    {
        ThrowIfDisposed();
        _commandLines = cmdline;
    }

    public void SetOneCallback(string name, Delegate func)
    {
        ThrowIfDisposed();
        _callbacks[name] = func;
    }

    public void SetCallbacks(Dictionary<string, Delegate> callBacks)
    {
        ThrowIfDisposed();
        foreach (var callback in callBacks) _callbacks[callback.Key] = callback.Value;
    }

    public void SetCallbackUsrData(IntPtr cbUsrData)
    {
        ThrowIfDisposed();
        _userData = cbUsrData;
    }

    public void InitMmMojoEnv()
    {
        ThrowIfDisposed();
        lock (_lifecycleLock)
        {
            if (_isMmmojoEnvInited && _mmmojoEnvironment != IntPtr.Zero)
                return;
            var ocrExePath = _ocrExePath;
            if (string.IsNullOrEmpty(ocrExePath) || !File.Exists(ocrExePath))
                throw new Exception($"给定的 WeChatOcr.exe 路径错误 (m_exe_path): {ocrExePath}");

            IDisposable? runtimeLease = null;
            IntPtr environment = IntPtr.Zero;
            var environmentStarted = false;
            try
            {
                runtimeLease = _runtime.Acquire();
                environment = _native.CreateEnvironment();
                if (environment == IntPtr.Zero)
                    throw new Exception("CreateMMMojoEnvironment 失败!");

                _native.SetEnvironmentCallback(environment, (int)MMMojoCallbackType.kMMUserData, _userData);
                SetDefaultCallbacks(environment);
                _native.SetEnvironmentInitParam(
                    environment,
                    (int)MMMojoEnvironmentInitParamType.kMMHostProcess,
                    new IntPtr(1));

                var exePathPointer = AllocateNativeString(ocrExePath!, unicode: true);
                _native.SetEnvironmentInitParam(
                    environment,
                    (int)MMMojoEnvironmentInitParamType.kMMExePath,
                    exePathPointer);

                foreach (var item in _switchNative)
                {
                    var keyPointer = AllocateNativeString(item.Key, unicode: false);
                    var valuePointer = AllocateNativeString(item.Value, unicode: true);
                    _native.AppendSubProcessSwitch(environment, keyPointer, valuePointer);
                }

                _native.StartEnvironment(environment);
                environmentStarted = true;
                _mmmojoEnvironment = environment;
                _runtimeLease = runtimeLease;
                _isMmmojoEnvInited = true;
                environment = IntPtr.Zero;
                runtimeLease = null;
            }
            catch
            {
                if (environment != IntPtr.Zero)
                {
                    try
                    {
                        if (environmentStarted)
                            _native.StopEnvironment(environment);
                    }
                    finally
                    {
                        _native.RemoveEnvironment(environment);
                    }
                }

                ReleaseNativeArguments();
                runtimeLease?.Dispose();
                throw;
            }
        }
    }

    private void SetDefaultCallbacks(IntPtr environment)
    {
        foreach (MMMojoCallbackType type in Enum.GetValues(typeof(MMMojoCallbackType)))
        {
            if (type == MMMojoCallbackType.kMMUserData) continue;
            try
            {
                var functionName = type.ToString();
                var callback = _defaultCallbacks.callbacks[functionName];
                if (_callbacks.TryGetValue(functionName, out var customCallback)) callback = customCallback;
                var callbackPointer = Marshal.GetFunctionPointerForDelegate(callback);
                _native.SetEnvironmentCallback(environment, (int)type, callbackPointer);
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex);
            }
        }
    }

    public void StopMmMojoEnv()
    {
        lock (_lifecycleLock)
        {
            var environment = _mmmojoEnvironment;
            var runtimeLease = _runtimeLease;
            _mmmojoEnvironment = IntPtr.Zero;
            _runtimeLease = null;
            _isMmmojoEnvInited = false;

            try
            {
                if (environment != IntPtr.Zero)
                {
                    try
                    {
                        _native.StopEnvironment(environment);
                    }
                    finally
                    {
                        _native.RemoveEnvironment(environment);
                    }
                }
            }
            finally
            {
                ReleaseNativeArguments();
                runtimeLease?.Dispose();
            }
        }
    }

    public void SendPbSerializedData(byte[] pbData, int pbSize, int method, int sync, uint requestId)
    {
        ThrowIfDisposed();
        var environment = _mmmojoEnvironment;
        if (!_isMmmojoEnvInited || environment == IntPtr.Zero)
            throw new InvalidOperationException("MMMojo 环境尚未启动。");

        var writeInfo = _native.CreateWriteInfo(method, sync, requestId);
        if (writeInfo == IntPtr.Zero)
            throw new InvalidOperationException("CreateMMMojoWriteInfo 失败。");

        var ownershipTransferred = false;
        try
        {
            var request = _native.GetWriteInfoRequest(writeInfo, (uint)pbSize);
            Marshal.Copy(pbData, 0, request, pbSize);
            ownershipTransferred = _native.SendWriteInfo(environment, writeInfo);
            if (!ownershipTransferred)
                throw new InvalidOperationException("SendMMMojoWriteInfo 失败。");
        }
        finally
        {
            if (!ownershipTransferred)
                _native.RemoveWriteInfo(writeInfo);
        }
    }

    public IntPtr GetPbSerializedData(IntPtr requestInfo, ref uint dataSize) =>
        _native.GetReadInfoRequest(requestInfo, ref dataSize);

    public IntPtr GetReadInfoAttachData(IntPtr requestInfo, ref uint dataSize) =>
        _native.GetReadInfoAttach(requestInfo, ref dataSize);

    public void RemoveReadInfo(IntPtr requestInfo)
    {
        if (requestInfo != IntPtr.Zero)
            _native.RemoveReadInfo(requestInfo);
    }

    public virtual void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }

    protected virtual void Dispose(bool disposing)
    {
        if (_disposed)
            return;

        try
        {
            StopMmMojoEnv();
        }
        finally
        {
            _disposed = true;
            ReleaseNativeArguments();
            _callbacks.Clear();
            _switchNative.Clear();
            _commandLines.Clear();
            _userData = IntPtr.Zero;
            _ocrExePath = null;
        }
    }

    protected bool IsDisposed => _disposed;

    internal int NativeArgumentCount => _nativeArguments.Count;

    protected void ThrowIfDisposed()
    {
        if (_disposed)
            throw new ObjectDisposedException(GetType().Name);
    }

    private IntPtr AllocateNativeString(string value, bool unicode)
    {
        var pointer = unicode ? Marshal.StringToHGlobalUni(value) : Marshal.StringToHGlobalAnsi(value);
        _nativeArguments.Add(pointer);
        return pointer;
    }

    private void ReleaseNativeArguments()
    {
        foreach (var pointer in _nativeArguments)
        {
            if (pointer != IntPtr.Zero)
                Marshal.FreeHGlobal(pointer);
        }

        _nativeArguments.Clear();
    }
}
