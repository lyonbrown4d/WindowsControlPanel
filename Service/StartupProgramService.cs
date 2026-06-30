using System.Management;
using Microsoft.Win32;
using WindowsControlPanel.Models;

namespace WindowsControlPanel.Service;

public interface IStartupProgramService
{
    Task<IReadOnlyList<StartupProgramEntry>> GetStartupProgramsAsync(Action<string>? onLog = null);
}

public sealed class StartupProgramService : IStartupProgramService
{
    private const string RunSubKey = @"SOFTWARE\Microsoft\Windows\CurrentVersion\Run";
    private const string RunOnceSubKey = @"SOFTWARE\Microsoft\Windows\CurrentVersion\RunOnce";

    public Task<IReadOnlyList<StartupProgramEntry>> GetStartupProgramsAsync(Action<string>? onLog = null)
    {
        return Task.Run<IReadOnlyList<StartupProgramEntry>>(() =>
        {
            var entries = new List<StartupProgramEntry>();

            ReadWmiStartupCommands(entries, onLog);
            ReadRegistryStartupCommands(entries, onLog);

            var result = entries
                .Where(entry => !string.IsNullOrWhiteSpace(entry.Name) || !string.IsNullOrWhiteSpace(entry.Command))
                .GroupBy(entry => BuildDeduplicationKey(entry), StringComparer.OrdinalIgnoreCase)
                .Select(group => group.First())
                .OrderBy(entry => entry.Name, StringComparer.CurrentCultureIgnoreCase)
                .ThenBy(entry => entry.Source, StringComparer.CurrentCultureIgnoreCase)
                .ToList();

            onLog?.Invoke($"已读取 {result.Count} 个启动项。");
            return result;
        });
    }

    private static void ReadWmiStartupCommands(ICollection<StartupProgramEntry> entries, Action<string>? onLog)
    {
        try
        {
            using var searcher = new ManagementObjectSearcher(
                "SELECT Name, Location, Command, User FROM Win32_StartupCommand"
            );

            foreach (var item in searcher.Get())
            {
                var name = GetManagementString(item, "Name");
                var location = GetManagementString(item, "Location");
                var command = GetManagementString(item, "Command");
                var user = GetManagementString(item, "User");

                entries.Add(
                    new StartupProgramEntry
                    {
                        Name = string.IsNullOrWhiteSpace(name) ? "(未命名启动项)" : name,
                        Source = "WMI 启动项",
                        Location = string.IsNullOrWhiteSpace(location) ? "Win32_StartupCommand" : location,
                        Command = command,
                        StatusSummary = string.IsNullOrWhiteSpace(user)
                            ? "由系统启动项清单报告。"
                            : $"由系统启动项清单报告，用户: {user}。"
                    }
                );
            }

            onLog?.Invoke("Win32_StartupCommand 读取完成。");
        }
        catch (Exception ex)
        {
            onLog?.Invoke($"Win32_StartupCommand 读取失败: {ex.Message}");
        }
    }

    private static void ReadRegistryStartupCommands(ICollection<StartupProgramEntry> entries, Action<string>? onLog)
    {
        ReadRegistryHive(entries, RegistryHive.CurrentUser, RegistryView.Registry64, "当前用户", onLog);
        ReadRegistryHive(entries, RegistryHive.CurrentUser, RegistryView.Registry32, "当前用户 (32 位视图)", onLog);
        ReadRegistryHive(entries, RegistryHive.LocalMachine, RegistryView.Registry64, "本机", onLog);
        ReadRegistryHive(entries, RegistryHive.LocalMachine, RegistryView.Registry32, "本机 (32 位视图)", onLog);
    }

    private static void ReadRegistryHive(
        ICollection<StartupProgramEntry> entries,
        RegistryHive hive,
        RegistryView view,
        string scope,
        Action<string>? onLog
    )
    {
        ReadRegistryKey(entries, hive, view, RunSubKey, scope, "Run", "每次登录时启动。", onLog);
        ReadRegistryKey(entries, hive, view, RunOnceSubKey, scope, "RunOnce", "下次登录时启动一次。", onLog);
    }

    private static void ReadRegistryKey(
        ICollection<StartupProgramEntry> entries,
        RegistryHive hive,
        RegistryView view,
        string subKeyName,
        string scope,
        string runKind,
        string summary,
        Action<string>? onLog
    )
    {
        try
        {
            using var baseKey = RegistryKey.OpenBaseKey(hive, view);
            using var key = baseKey.OpenSubKey(subKeyName, writable: false);
            if (key is null)
            {
                return;
            }

            var hiveName = hive == RegistryHive.CurrentUser ? "HKCU" : "HKLM";
            var location = $@"{hiveName}\{subKeyName}";
            foreach (var valueName in key.GetValueNames())
            {
                var value = key.GetValue(valueName);
                entries.Add(
                    new StartupProgramEntry
                    {
                        Name = string.IsNullOrWhiteSpace(valueName) ? "(默认)" : valueName,
                        Source = $"注册表 {scope} {runKind}",
                        Location = location,
                        Command = ConvertRegistryValue(value),
                        StatusSummary = summary
                    }
                );
            }

            onLog?.Invoke($"{location} ({scope}) 读取完成。");
        }
        catch (Exception ex)
        {
            onLog?.Invoke($"{scope} {runKind} 读取失败: {ex.Message}");
        }
    }

    private static string GetManagementString(ManagementBaseObject item, string propertyName)
    {
        try
        {
            return item[propertyName]?.ToString() ?? string.Empty;
        }
        catch
        {
            return string.Empty;
        }
    }

    private static string ConvertRegistryValue(object? value)
    {
        return value switch
        {
            null => string.Empty,
            string text => text,
            string[] values => string.Join(" ", values),
            byte[] bytes => BitConverter.ToString(bytes),
            _ => value.ToString() ?? string.Empty
        };
    }

    private static string BuildDeduplicationKey(StartupProgramEntry entry)
    {
        return string.Join(
            "|",
            entry.Name.Trim(),
            entry.Location.Trim(),
            entry.Command.Trim()
        );
    }
}
