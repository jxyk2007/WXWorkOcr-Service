namespace WeChatOcr;

internal interface IMmmojoApi
{
    void Initialize();
    void Shutdown();
    IntPtr CreateEnvironment();
    void SetEnvironmentCallback(IntPtr environment, int type, IntPtr callback);
    void SetEnvironmentInitParam(IntPtr environment, int type, IntPtr parameter);
    void AppendSubProcessSwitch(IntPtr environment, IntPtr switchName, IntPtr value);
    void StartEnvironment(IntPtr environment);
    void StopEnvironment(IntPtr environment);
    void RemoveEnvironment(IntPtr environment);
    IntPtr CreateWriteInfo(int method, int sync, uint requestId);
    IntPtr GetWriteInfoRequest(IntPtr writeInfo, uint requestDataSize);
    bool SendWriteInfo(IntPtr environment, IntPtr writeInfo);
    void RemoveWriteInfo(IntPtr writeInfo);
    IntPtr GetReadInfoRequest(IntPtr readInfo, ref uint requestDataSize);
    IntPtr GetReadInfoAttach(IntPtr readInfo, ref uint attachDataSize);
    void RemoveReadInfo(IntPtr readInfo);
}

internal sealed class MmmojoApi : IMmmojoApi
{
    internal static MmmojoApi Instance { get; } = new();

    private MmmojoApi()
    {
    }

    public void Initialize() => MmmojoDll.InitializeMMMojo(0, IntPtr.Zero);

    public void Shutdown() => MmmojoDll.ShutdownMMMojo();

    public IntPtr CreateEnvironment() => MmmojoDll.CreateMMMojoEnvironment();

    public void SetEnvironmentCallback(IntPtr environment, int type, IntPtr callback) =>
        MmmojoDll.SetMMMojoEnvironmentCallbacks(environment, type, callback);

    public void SetEnvironmentInitParam(IntPtr environment, int type, IntPtr parameter) =>
        MmmojoDll.SetMMMojoEnvironmentInitParams(environment, type, parameter);

    public void AppendSubProcessSwitch(IntPtr environment, IntPtr switchName, IntPtr value) =>
        MmmojoDll.AppendMMSubProcessSwitchNative(environment, switchName, value);

    public void StartEnvironment(IntPtr environment) => MmmojoDll.StartMMMojoEnvironment(environment);

    public void StopEnvironment(IntPtr environment) => MmmojoDll.StopMMMojoEnvironment(environment);

    public void RemoveEnvironment(IntPtr environment) => MmmojoDll.RemoveMMMojoEnvironment(environment);

    public IntPtr CreateWriteInfo(int method, int sync, uint requestId) =>
        MmmojoDll.CreateMMMojoWriteInfo(method, sync, requestId);

    public IntPtr GetWriteInfoRequest(IntPtr writeInfo, uint requestDataSize) =>
        MmmojoDll.GetMMMojoWriteInfoRequest(writeInfo, requestDataSize);

    public bool SendWriteInfo(IntPtr environment, IntPtr writeInfo) =>
        MmmojoDll.SendMMMojoWriteInfo(environment, writeInfo);

    public void RemoveWriteInfo(IntPtr writeInfo) => MmmojoDll.RemoveMMMojoWriteInfo(writeInfo);

    public IntPtr GetReadInfoRequest(IntPtr readInfo, ref uint requestDataSize) =>
        MmmojoDll.GetMMMojoReadInfoRequest(readInfo, ref requestDataSize);

    public IntPtr GetReadInfoAttach(IntPtr readInfo, ref uint attachDataSize) =>
        MmmojoDll.GetMMMojoReadInfoAttach(readInfo, ref attachDataSize);

    public void RemoveReadInfo(IntPtr readInfo) => MmmojoDll.RemoveMMMojoReadInfo(readInfo);
}

internal sealed class MmmojoRuntime : IDisposable
{
    private readonly object _syncRoot = new();
    private readonly IMmmojoApi _api;
    private int _leaseCount;
    private bool _initialized;
    private bool _disposed;
    private readonly bool _registeredForProcessExit;

    internal static MmmojoRuntime Shared { get; } = new(MmmojoApi.Instance, registerForProcessExit: true);

    internal MmmojoRuntime(IMmmojoApi api)
        : this(api, registerForProcessExit: false)
    {
    }

    private MmmojoRuntime(IMmmojoApi api, bool registerForProcessExit)
    {
        _api = api;
        _registeredForProcessExit = registerForProcessExit;
        if (registerForProcessExit)
            AppDomain.CurrentDomain.ProcessExit += OnProcessExit;
    }

    internal int LeaseCount
    {
        get
        {
            lock (_syncRoot)
                return _leaseCount;
        }
    }

    internal IDisposable Acquire()
    {
        lock (_syncRoot)
        {
            if (_disposed)
                throw new ObjectDisposedException(nameof(MmmojoRuntime));
            if (!_initialized)
            {
                _api.Initialize();
                _initialized = true;
            }

            _leaseCount++;
            return new RuntimeLease(this);
        }
    }

    private void Release()
    {
        lock (_syncRoot)
        {
            if (_leaseCount == 0)
                return;

            _leaseCount--;
        }
    }

    public void Dispose()
    {
        lock (_syncRoot)
        {
            if (_disposed)
                return;

            _disposed = true;
            if (_initialized)
            {
                _api.Shutdown();
                _initialized = false;
            }
        }

        if (_registeredForProcessExit)
            AppDomain.CurrentDomain.ProcessExit -= OnProcessExit;
    }

    private void OnProcessExit(object? sender, EventArgs e) => Dispose();

    private sealed class RuntimeLease(MmmojoRuntime owner) : IDisposable
    {
        private MmmojoRuntime? _owner = owner;

        public void Dispose() => Interlocked.Exchange(ref _owner, null)?.Release();
    }
}
