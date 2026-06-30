namespace WindowsControlPanel.Models;

public sealed class StartupProgramEntry
{
    public string Name { get; init; } = string.Empty;
    public string Source { get; init; } = string.Empty;
    public string Location { get; init; } = string.Empty;
    public string Command { get; init; } = string.Empty;
    public string StatusSummary { get; init; } = string.Empty;
}
