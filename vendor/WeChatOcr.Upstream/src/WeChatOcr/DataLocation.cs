namespace WeChatOcr;

public class DataLocation
{
    private static string _baseDirectory = ".";

    /// <summary>
    ///     内部OCR数据目录
    /// </summary>
    public static string WeChatOcrData => Path.Combine(_baseDirectory, "wco_data");

    /// <summary>
    ///     Mojo DLL 完整路径
    /// </summary>
    public static string MojoDllName
    {
        get
        {
#if WIN32
            return Path.Combine(WeChatOcrData, "mmmojo.dll");
#else
            return Path.Combine(WeChatOcrData, "mmmojo_64.dll");
#endif
        }
    }

    /// <summary>
    ///     设置基础目录，默认为当前目录 "."
    /// </summary>
    /// <param name="baseDirectory">基础目录路径</param>
    public static void SetBaseDirectory(string baseDirectory)
    {
        _baseDirectory = baseDirectory;
    }
}