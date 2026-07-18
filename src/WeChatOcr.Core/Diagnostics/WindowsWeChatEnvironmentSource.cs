// 版本号：v1.1
using Microsoft.Win32;
using System.Runtime.Versioning;

namespace WeChatOcr.Core.Diagnostics;

[SupportedOSPlatform("windows")]
public sealed class WindowsWeChatEnvironmentSource : IWeChatEnvironmentSource
{
    private static readonly string[] UninstallKeys =
    [
        @"SOFTWARE\WOW6432Node\Microsoft\Windows\CurrentVersion\Uninstall\WeChat",
        @"SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall\WeChat",
        @"SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall\Weixin"
    ];

    public IEnumerable<string> GetCandidateDirectories()
    {
        var roots = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var hive in new[] { Registry.LocalMachine, Registry.CurrentUser })
        foreach (var keyName in UninstallKeys)
        {
            using var key = hive.OpenSubKey(keyName);
            if (key?.GetValue("InstallLocation") is string location && !string.IsNullOrWhiteSpace(location)) roots.Add(location);
        }
        AddIfNotEmpty(roots, Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), "Tencent", "WeChat");
        AddIfNotEmpty(roots, Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86), "Tencent", "WeChat");
        AddIfNotEmpty(roots, Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Tencent", "Weixin");
        foreach (var root in roots)
        {
            yield return root;
            if (!Directory.Exists(root)) continue;
            foreach (var directory in Directory.EnumerateDirectories(root).Where(IsFormalVersionDirectory)) yield return directory;
        }
    }

    public IEnumerable<string> FindOcrExecutables()
    {
        var roots = new[]
        {
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), @"Tencent\WeChat\XPlugin\Plugins\WeChatOCR"),
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), @"Tencent\Weixin\XPlugin\Plugins\WeChatOCR"),
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), @"Tencent\Weixin")
        };
        foreach (var root in roots.Where(Directory.Exists))
        foreach (var file in SafeEnumerateFiles(root, "WeChatOCR.exe")) yield return file;
    }

    public bool DirectoryExists(string path) => Directory.Exists(path);
    public bool FileExists(string path) => File.Exists(path);

    private static void AddIfNotEmpty(ISet<string> paths, string root, params string[] parts)
    {
        if (!string.IsNullOrWhiteSpace(root)) paths.Add(parts.Aggregate(root, Path.Combine));
    }

    private static IEnumerable<string> SafeEnumerateFiles(string root, string pattern)
    {
        try { return Directory.EnumerateFiles(root, pattern, SearchOption.AllDirectories).ToArray(); }
        catch (UnauthorizedAccessException) { return []; }
        catch (IOException) { return []; }
    }

    private static bool IsFormalVersionDirectory(string path)
    {
        var name = Path.GetFileName(path);
        return name.StartsWith('[') && name.EndsWith(']') && !name.EndsWith("_tmp", StringComparison.OrdinalIgnoreCase);
    }
}
