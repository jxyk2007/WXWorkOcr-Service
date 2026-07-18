using System.Runtime.InteropServices;

namespace WeChatOcr;

public enum MMMojoInfoMethod
{
    kMMNone = 0,
    kMMPush,
    kMMPullReq,
    kMMPullResp,
    kMMShared
}

public enum MMMojoCallbackType
{
    kMMUserData = 0,
    kMMReadPush,
    kMMReadPull,
    kMMReadShared,
    kMMRemoteConnect,
    kMMRemoteDisconnect,
    kMMRemoteProcessLaunched,
    kMMRemoteProcessLaunchFailed,
    kMMRemoteMojoError
}

public enum MMMojoEnvironmentInitParamType
{
    kMMHostProcess = 0,
    kMMLoopStartThread,
    kMMExePath,
    kMMLogPath,
    kMMLogToStderr,
    kMMAddNumMessagepipe,
    kMMSetDisconnectHandlers,
    kMMDisableDefaultPolicy = 1000,
    kMMElevated,
    kMMCompatible
}

public partial class MmmojoDll
{
    private const string LibraryName = "mmmojo";

    static MmmojoDll()
    {
#if NET5_0_OR_GREATER
        // .NET 5+ 使用 NativeLibrary
        NativeLibrary.SetDllImportResolver(typeof(MmmojoDll).Assembly, DllImportResolver);
#else
        // .NET Framework 使用 LoadLibrary 预加载
        LoadLibraryManually();
#endif
    }

#if NET5_0_OR_GREATER
    private static IntPtr DllImportResolver(string libraryName, System.Reflection.Assembly assembly, DllImportSearchPath? searchPath)
    {
        if (libraryName == LibraryName)
        {
            if (NativeLibrary.TryLoad(DataLocation.MojoDllName, out var handle))
                return handle;
        }
        return IntPtr.Zero;
    }
#else
    // .NET Framework 的手动加载
    [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern IntPtr LoadLibrary(string lpFileName);

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern bool SetDllDirectory(string lpPathName);

    private static void LoadLibraryManually()
    {
        var dllPath = Path.GetFullPath(DataLocation.MojoDllName);
        var directory = Path.GetDirectoryName(dllPath);
        
        // 设置 DLL 搜索目录
        if (!string.IsNullOrEmpty(directory))
        {
            SetDllDirectory(directory);
        }
        
        // 预加载 DLL
        var handle = LoadLibrary(dllPath);
        if (handle == IntPtr.Zero)
        {
            var error = Marshal.GetLastWin32Error();
            throw new System.ComponentModel.Win32Exception(error, 
                $"无法加载 DLL: {dllPath}");
        }
    }
#endif

    // P/Invoke 声明 - 使用常量字符串
    [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl)]
    public static extern void InitializeMMMojo(int argc, IntPtr argv);

    [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl)]
    public static extern void ShutdownMMMojo();

    [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl)]
    public static extern IntPtr CreateMMMojoEnvironment();

    [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl)]
    public static extern void SetMMMojoEnvironmentCallbacks(IntPtr mmmojo_env, int type, IntPtr callback);

    [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl, CharSet = CharSet.Ansi)]
    public static extern void SetMMMojoEnvironmentInitParams(IntPtr mmmojo_env, int type, IntPtr param);

    [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl, CharSet = CharSet.Ansi)]
    public static extern void AppendMMSubProcessSwitchNative(IntPtr mmmojo_env, IntPtr switchStringPtr, IntPtr valuePtr);

    [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl)]
    public static extern void StartMMMojoEnvironment(IntPtr mmmojo_env);

    [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl)]
    public static extern void StopMMMojoEnvironment(IntPtr mmmojo_env);

    [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl)]
    public static extern void RemoveMMMojoEnvironment(IntPtr mmmojo_env);

    [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl)]
    public static extern IntPtr GetMMMojoReadInfoRequest(IntPtr mmmojo_readinfo, ref uint requestDataSize);

    [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl)]
    public static extern IntPtr GetMMMojoReadInfoAttach(IntPtr mmmojo_readinfo, ref uint attachDataSize);

    [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl)]
    public static extern void RemoveMMMojoReadInfo(IntPtr mmmojo_readinfo);

    [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl)]
    public static extern int GetMMMojoReadInfoMethod(IntPtr mmmojo_readinfo);

    [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl)]
    [return: MarshalAs(UnmanagedType.I1)]
    public static extern bool GetMMMojoReadInfoSync(IntPtr mmmojo_readinfo);

    [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl)]
    public static extern IntPtr CreateMMMojoWriteInfo(int method, int sync, uint requestId);

    [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl)]
    public static extern IntPtr GetMMMojoWriteInfoRequest(IntPtr mmmojo_writeinfo, uint requestDataSize);

    [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl)]
    public static extern void RemoveMMMojoWriteInfo(IntPtr mmmojo_writeinfo);

    [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl)]
    public static extern IntPtr GetMMMojoWriteInfoAttach(IntPtr mmmojo_writeinfo, uint attachDataSize);

    [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl)]
    public static extern void SetMMMojoWriteInfoMessagePipe(IntPtr mmmojo_writeinfo, int numOfMessagePipe);

    [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl)]
    public static extern void SetMMMojoWriteInfoResponseSync(IntPtr mmmojo_writeinfo, ref IntPtr mmmojo_readinfo);

    [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl)]
    [return: MarshalAs(UnmanagedType.I1)]
    public static extern bool SendMMMojoWriteInfo(IntPtr mmmojo_env, IntPtr mmmojo_writeinfo);

    [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl)]
    [return: MarshalAs(UnmanagedType.I1)]
    public static extern bool SwapMMMojoWriteInfoCallback(IntPtr mmmojo_writeinfo, IntPtr mmmojo_readinfo);

    [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl)]
    [return: MarshalAs(UnmanagedType.I1)]
    public static extern bool SwapMMMojoWriteInfoMessage(IntPtr mmmojo_writeinfo, IntPtr mmmojo_readinfo);
}
