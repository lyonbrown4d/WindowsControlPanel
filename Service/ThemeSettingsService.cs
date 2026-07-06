using System.Windows;
using System.Windows.Media;
using Microsoft.EntityFrameworkCore;
using Wpf.Ui.Appearance;
using Wpf.Ui.Controls;
using WindowsControlPanel.Context;

namespace WindowsControlPanel.Service;

public enum AppThemePreference
{
    FollowSystem,
    Dark,
    Light
}

public interface IThemeSettingsService
{
    Task<AppThemePreference> GetThemePreferenceAsync();
    Task SetThemePreferenceAsync(AppThemePreference preference);
    void ApplyTheme(AppThemePreference preference, Window? window = null);
    string FormatThemePreference(AppThemePreference preference);
}

public sealed class ThemeSettingsService : IThemeSettingsService
{
    private const string ThemePreferenceKey = "theme_preference";
    private readonly AppDbContext _dbContext;
    private readonly SemaphoreSlim _dbLock = new(1, 1);
    private Window? _watchedWindow;
    private AppThemePreference _currentPreference = AppThemePreference.FollowSystem;

    public ThemeSettingsService(AppDbContext dbContext)
    {
        _dbContext = dbContext;
        ApplicationThemeManager.Changed += OnApplicationThemeChanged;
    }

    public async Task<AppThemePreference> GetThemePreferenceAsync()
    {
        await _dbLock.WaitAsync();
        try
        {
            var stored = await _dbContext.UserSettings
                .AsNoTracking()
                .FirstOrDefaultAsync(x => x.Key == ThemePreferenceKey);

            return Enum.TryParse<AppThemePreference>(stored?.Value, ignoreCase: true, out var preference)
                ? preference
                : AppThemePreference.FollowSystem;
        }
        catch
        {
            return AppThemePreference.FollowSystem;
        }
        finally
        {
            _dbLock.Release();
        }
    }

    public async Task SetThemePreferenceAsync(AppThemePreference preference)
    {
        await _dbLock.WaitAsync();
        try
        {
            var existing = await _dbContext.UserSettings.FirstOrDefaultAsync(x => x.Key == ThemePreferenceKey);
            if (existing is null)
            {
                _dbContext.UserSettings.Add(new UserSetting { Key = ThemePreferenceKey, Value = preference.ToString() });
            }
            else
            {
                existing.Value = preference.ToString();
            }

            await _dbContext.SaveChangesAsync();
        }
        finally
        {
            _dbLock.Release();
        }
    }

    public void ApplyTheme(AppThemePreference preference, Window? window = null)
    {
        _currentPreference = preference;
        if (window is not null)
        {
            _watchedWindow = window;
        }

        if (_watchedWindow is not null)
        {
            SystemThemeWatcher.UnWatch(_watchedWindow);
        }

        var theme = ResolveTheme(preference);
        ApplicationThemeManager.Apply(theme, WindowBackdropType.Mica, updateAccent: true);
        ApplyCustomPalette(theme);

        if (preference == AppThemePreference.FollowSystem && _watchedWindow is not null)
        {
            SystemThemeWatcher.Watch(_watchedWindow, WindowBackdropType.Mica, updateAccents: true);
        }
    }

    public string FormatThemePreference(AppThemePreference preference)
    {
        return preference switch
        {
            AppThemePreference.FollowSystem => "跟随系统",
            AppThemePreference.Dark => "深色",
            AppThemePreference.Light => "浅色",
            _ => "未知"
        };
    }

    private void OnApplicationThemeChanged(ApplicationTheme currentApplicationTheme, Color systemAccent)
    {
        var theme = _currentPreference == AppThemePreference.FollowSystem
            ? ResolveTheme(AppThemePreference.FollowSystem)
            : currentApplicationTheme;

        ApplyCustomPalette(theme);
    }

    private static ApplicationTheme ResolveTheme(AppThemePreference preference)
    {
        if (preference == AppThemePreference.Dark)
        {
            return ApplicationTheme.Dark;
        }

        if (preference == AppThemePreference.Light)
        {
            return ApplicationTheme.Light;
        }

        return ApplicationThemeManager.GetSystemTheme() switch
        {
            SystemTheme.Light => ApplicationTheme.Light,
            SystemTheme.HCWhite or SystemTheme.HCBlack or SystemTheme.HC1 or SystemTheme.HC2 => ApplicationTheme.HighContrast,
            _ => ApplicationTheme.Dark
        };
    }

    private static void ApplyCustomPalette(ApplicationTheme theme)
    {
        var dark = theme != ApplicationTheme.Light;
        SetBrush("AppBackgroundBrush", dark ? "#330B0F14" : "#66F5F7FA");
        SetBrush("AppSurfaceBrush", dark ? "#A6111820" : "#DFFFFFFF");
        SetBrush("AppSurfaceElevatedBrush", dark ? "#CC17212B" : "#F2FFFFFF");
        SetBrush("AppSurfaceSubtleBrush", dark ? "#8C0F151C" : "#D9EEF2F7");
        SetBrush("AppStrokeBrush", dark ? "#55FFFFFF" : "#99CBD5E1");
        SetBrush("AppStrokeStrongBrush", dark ? "#78FFFFFF" : "#CC94A3B8");
        SetBrush("AppAccentBrush", "#4F9CF9");
        SetBrush("AppAccentSoftBrush", dark ? "#334F9CF9" : "#242563EB");
        SetBrush("AppAccentHoverBrush", dark ? "#65B3FF" : "#2563EB");
        SetBrush("AppButtonHoverBrush", dark ? "#22FFFFFF" : "#FFEAF2FF");
        SetBrush("AppButtonPressedBrush", dark ? "#334F9CF9" : "#FFD8E8FF");
        SetBrush("AppDangerSoftBrush", dark ? "#30EF4444" : "#1FB91C1C");
        SetBrush("AppSuccessBrush", dark ? "#22C55E" : "#15803D");
        SetBrush("AppSuccessSoftBrush", dark ? "#2622C55E" : "#1F15803D");
        SetBrush("AppWarningBrush", dark ? "#F59E0B" : "#B45309");
        SetBrush("AppDangerBrush", dark ? "#EF4444" : "#B91C1C");
        SetBrush("GlassShellBrush", dark ? "#8F0B0F14" : "#CFF7FAFC");
        SetBrush("GlassPanelBrush", dark ? "#A8111820" : "#E6FFFFFF");
        SetBrush("GlassPanelElevatedBrush", dark ? "#C817212B" : "#F2FFFFFF");
        SetBrush("GlassSidebarBrush", dark ? "#9C0F151C" : "#DFF1F5F9");
        SetBrush("GlassInputBrush", dark ? "#BA0D131A" : "#F5FFFFFF");
        SetBrush("GlassStrokeBrush", dark ? "#55FFFFFF" : "#A5CBD5E1");
        SetBrush("GlassStrokeStrongBrush", dark ? "#78FFFFFF" : "#CC94A3B8");
        SetBrush("GlassAmbientBrush", dark ? "#260B0F14" : "#55F8FAFC");
        SetBrush("GlassHighlightBrush", dark ? "#26FFFFFF" : "#A6FFFFFF");
    }

    private static void SetBrush(string key, string color)
    {
        if (Application.Current is null)
        {
            return;
        }

        var parsedColor = (Color)ColorConverter.ConvertFromString(color);
        if (Application.Current.Resources[key] is SolidColorBrush brush && !brush.IsFrozen)
        {
            brush.Color = parsedColor;
            return;
        }

        Application.Current.Resources[key] = new SolidColorBrush(parsedColor);
    }
}
