using System.Windows;
using WindowsControlPanel.Service;

namespace WindowsControlPanel.ViewModels;

internal static class OperationConfirmation
{
    public static bool Confirm(OperationSafetyProfile profile)
    {
        if (profile.RiskLevel is OperationRiskLevel.Low)
        {
            return true;
        }

        var message =
            $"{profile.Summary}{Environment.NewLine}{Environment.NewLine}" +
            $"风险级别: {FormatRiskLevel(profile.RiskLevel)}{Environment.NewLine}" +
            $"需要管理员权限: {FormatBoolean(profile.RequiresAdministrator)}{Environment.NewLine}" +
            $"需要重启: {FormatBoolean(profile.RequiresRestart)}{Environment.NewLine}" +
            $"影响范围: {profile.AffectedArea}{Environment.NewLine}{Environment.NewLine}" +
            "确认继续执行吗？";

        var icon = profile.RiskLevel is OperationRiskLevel.High
            ? MessageBoxImage.Warning
            : MessageBoxImage.Question;

        return MessageBox.Show(
            message,
            profile.Title,
            MessageBoxButton.OKCancel,
            icon
        ) == MessageBoxResult.OK;
    }

    private static string FormatRiskLevel(OperationRiskLevel riskLevel)
    {
        return riskLevel switch
        {
            OperationRiskLevel.Low => "低",
            OperationRiskLevel.Medium => "中",
            OperationRiskLevel.High => "高",
            _ => "未知"
        };
    }

    private static string FormatBoolean(bool value)
    {
        return value ? "是" : "否";
    }
}
