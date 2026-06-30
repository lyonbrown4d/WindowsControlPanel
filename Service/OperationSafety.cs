namespace WindowsControlPanel.Service;

public enum OperationRiskLevel
{
    Low,
    Medium,
    High
}

public sealed class OperationSafetyProfile
{
    public string Title { get; init; } = string.Empty;
    public string Summary { get; init; } = string.Empty;
    public OperationRiskLevel RiskLevel { get; init; }
    public bool RequiresAdministrator { get; init; }
    public bool RequiresRestart { get; init; }
    public string AffectedArea { get; init; } = string.Empty;
}

public interface IOperationSafetyService
{
    OperationSafetyProfile CreateModeProfile(OptimizationMode mode, bool autoReboot);
    OperationSafetyProfile CreateOptionalFeatureProfile(string featureName, bool enable);
    OperationSafetyProfile CreateAdvancedActionProfile(AdvancedAction action);
}

public sealed class OperationSafetyService : IOperationSafetyService
{
    public OperationSafetyProfile CreateModeProfile(OptimizationMode mode, bool autoReboot)
    {
        return mode switch
        {
            OptimizationMode.Development => new OperationSafetyProfile
            {
                Title = "应用开发模式",
                Summary = "将启用 Hyper-V、WSL、Virtual Machine Platform、VBS 与 HVCI，适合容器、WSL 和虚拟化开发场景。" +
                          FormatRebootSummary(autoReboot),
                RiskLevel = OperationRiskLevel.High,
                RequiresAdministrator = true,
                RequiresRestart = true,
                AffectedArea = "Windows 可选功能、启动配置、Device Guard 注册表"
            },
            OptimizationMode.Gaming => new OperationSafetyProfile
            {
                Title = "应用游戏模式",
                Summary = "将关闭 Hyper-V、WSL、Virtual Machine Platform、VBS 与 HVCI，可能影响 WSL、Docker、模拟器和虚拟机。" +
                          FormatRebootSummary(autoReboot),
                RiskLevel = OperationRiskLevel.High,
                RequiresAdministrator = true,
                RequiresRestart = true,
                AffectedArea = "Windows 可选功能、启动配置、Device Guard 注册表"
            },
            _ => CreateUnknownProfile("应用模式预设")
        };
    }

    public OperationSafetyProfile CreateOptionalFeatureProfile(string featureName, bool enable)
    {
        var displayName = GetFeatureDisplayName(featureName);
        return new OperationSafetyProfile
        {
            Title = $"{(enable ? "启用" : "禁用")} {displayName}",
            Summary = $"{(enable ? "启用" : "禁用")} Windows 可选功能 {displayName}。该操作会调用 DISM，完成后通常需要重启。",
            RiskLevel = OperationRiskLevel.High,
            RequiresAdministrator = true,
            RequiresRestart = true,
            AffectedArea = "Windows 可选功能"
        };
    }

    public OperationSafetyProfile CreateAdvancedActionProfile(AdvancedAction action)
    {
        return action switch
        {
            AdvancedAction.EnableDeveloperMode => new OperationSafetyProfile
            {
                Title = "启用开发者模式",
                Summary = "将写入系统注册表以允许开发者模式，适合本机开发和旁加载调试。",
                RiskLevel = OperationRiskLevel.Medium,
                RequiresAdministrator = true,
                RequiresRestart = false,
                AffectedArea = "HKLM AppModelUnlock 注册表"
            },
            AdvancedAction.DisableDeveloperMode => new OperationSafetyProfile
            {
                Title = "禁用开发者模式",
                Summary = "将写入系统注册表以关闭开发者模式，可能影响旁加载和调试工作流。",
                RiskLevel = OperationRiskLevel.Medium,
                RequiresAdministrator = true,
                RequiresRestart = false,
                AffectedArea = "HKLM AppModelUnlock 注册表"
            },
            AdvancedAction.EnableSudo => new OperationSafetyProfile
            {
                Title = "启用 Windows Sudo",
                Summary = "将启用 Windows Sudo normal 模式，后续终端命令可通过 sudo 发起提权。",
                RiskLevel = OperationRiskLevel.Medium,
                RequiresAdministrator = true,
                RequiresRestart = false,
                AffectedArea = "Windows Sudo 配置"
            },
            AdvancedAction.DisableSudo => new OperationSafetyProfile
            {
                Title = "禁用 Windows Sudo",
                Summary = "将关闭 Windows Sudo，可能影响依赖 sudo 的开发脚本。",
                RiskLevel = OperationRiskLevel.Medium,
                RequiresAdministrator = true,
                RequiresRestart = false,
                AffectedArea = "Windows Sudo 配置"
            },
            AdvancedAction.ExportWingetPackages => new OperationSafetyProfile
            {
                Title = "导出 winget 清单",
                Summary = "只读取已安装包信息并导出 JSON 清单，不修改系统配置。",
                RiskLevel = OperationRiskLevel.Low,
                RequiresAdministrator = false,
                RequiresRestart = false,
                AffectedArea = "用户文档目录"
            },
            AdvancedAction.OpenGraphicsSettings or
                AdvancedAction.OpenGameModeSettings or
                AdvancedAction.OpenGameBarSettings => new OperationSafetyProfile
                {
                    Title = "打开系统设置",
                    Summary = "只打开对应 Windows 设置页面，不直接修改系统配置。",
                    RiskLevel = OperationRiskLevel.Low,
                    RequiresAdministrator = false,
                    RequiresRestart = false,
                    AffectedArea = "Windows 设置入口"
                },
            _ => CreateUnknownProfile("执行高级动作")
        };
    }

    private static OperationSafetyProfile CreateUnknownProfile(string title)
    {
        return new OperationSafetyProfile
        {
            Title = title,
            Summary = "该操作会修改系统配置，请确认你了解影响后继续。",
            RiskLevel = OperationRiskLevel.High,
            RequiresAdministrator = true,
            RequiresRestart = true,
            AffectedArea = "系统配置"
        };
    }

    private static string GetFeatureDisplayName(string featureName)
    {
        return featureName switch
        {
            "Microsoft-Hyper-V-All" => "Hyper-V",
            "Microsoft-Windows-Subsystem-Linux" => "WSL",
            "VirtualMachinePlatform" => "Virtual Machine Platform",
            "Containers-DisposableClientVM" => "Windows Sandbox",
            _ => featureName
        };
    }

    private static string FormatRebootSummary(bool autoReboot)
    {
        return autoReboot
            ? " 当前设置为应用后自动重启。"
            : " 操作完成后需要手动重启才能完全生效。";
    }
}
