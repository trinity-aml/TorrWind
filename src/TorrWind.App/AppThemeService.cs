using System.Windows;
using System.Windows.Media;
using Microsoft.Win32;
using MediaColor = System.Windows.Media.Color;
using WpfApplication = System.Windows.Application;
using WpfSystemColors = System.Windows.SystemColors;

namespace TorrWind.App;

public static class AppThemeService
{
    public const string SystemTheme = "system";
    public const string LightTheme = "light";
    public const string DarkTheme = "dark";

    public static string NormalizeTheme(string? theme)
    {
        return theme?.Trim().ToLowerInvariant() switch
        {
            LightTheme => LightTheme,
            DarkTheme => DarkTheme,
            _ => SystemTheme
        };
    }

    public static void Apply(string? theme)
    {
        var normalized = NormalizeTheme(theme);
        ApplyPalette(ResolveDarkMode(normalized));
    }

    private static bool ResolveDarkMode(string theme)
    {
        return theme switch
        {
            LightTheme => false,
            DarkTheme => true,
            _ => IsSystemDarkMode()
        };
    }

    private static bool IsSystemDarkMode()
    {
        if (!OperatingSystem.IsWindows())
        {
            return false;
        }

        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Themes\Personalize");
            return key?.GetValue("AppsUseLightTheme") is int value && value == 0;
        }
        catch
        {
            return false;
        }
    }

    private static void ApplyPalette(bool dark)
    {
        if (WpfApplication.Current is null)
        {
            return;
        }

        if (dark)
        {
            SetBrush("AppBackgroundBrush", MediaColor.FromRgb(15, 20, 26));
            SetBrush("SurfaceBrush", MediaColor.FromRgb(23, 30, 38));
            SetBrush("SidebarBrush", MediaColor.FromRgb(16, 24, 32));
            SetBrush("AccentBrush", MediaColor.FromRgb(24, 116, 93));
            SetBrush("AccentSoftBrush", MediaColor.FromRgb(19, 56, 47));
            SetBrush("BorderBrush", MediaColor.FromRgb(45, 57, 72));
            SetBrush("TextBrush", MediaColor.FromRgb(233, 238, 245));
            SetBrush("MutedBrush", MediaColor.FromRgb(163, 175, 189));
            SetBrush("SidebarTextBrush", MediaColor.FromRgb(245, 247, 250));
            SetBrush("SidebarMutedBrush", MediaColor.FromRgb(184, 197, 212));
            SetBrush("VersionBadgeBrush", MediaColor.FromRgb(40, 68, 95));
            SetBrush("VersionBadgeTextBrush", MediaColor.FromRgb(217, 226, 236));
            SetBrush("ButtonBackgroundBrush", MediaColor.FromRgb(34, 43, 53));
            SetBrush("ButtonHoverBrush", MediaColor.FromRgb(43, 55, 68));
            SetBrush("ButtonPressedBrush", MediaColor.FromRgb(28, 38, 49));
            SetBrush("ButtonBorderBrush", MediaColor.FromRgb(60, 76, 94));
            SetBrush("ButtonForegroundBrush", MediaColor.FromRgb(238, 243, 248));
            SetBrush("ButtonDisabledBackgroundBrush", MediaColor.FromRgb(25, 32, 40));
            SetBrush("ButtonDisabledBorderBrush", MediaColor.FromRgb(42, 52, 63));
            SetBrush("ButtonDisabledForegroundBrush", MediaColor.FromRgb(99, 113, 129));
            SetBrush("PrimaryButtonBackgroundBrush", MediaColor.FromRgb(24, 116, 93));
            SetBrush("PrimaryButtonHoverBrush", MediaColor.FromRgb(29, 135, 108));
            SetBrush("PrimaryButtonPressedBrush", MediaColor.FromRgb(18, 94, 74));
            SetBrush("PrimaryButtonBorderBrush", MediaColor.FromRgb(24, 116, 93));
            SetBrush("PrimaryButtonForegroundBrush", Colors.White);
            SetBrush(WpfSystemColors.HighlightBrushKey, MediaColor.FromRgb(19, 56, 47));
            SetBrush(WpfSystemColors.HighlightTextBrushKey, MediaColor.FromRgb(233, 238, 245));
            SetBrush(WpfSystemColors.InactiveSelectionHighlightBrushKey, MediaColor.FromRgb(19, 56, 47));
            SetBrush(WpfSystemColors.InactiveSelectionHighlightTextBrushKey, MediaColor.FromRgb(233, 238, 245));
            SetBrush(WpfSystemColors.ControlBrushKey, MediaColor.FromRgb(43, 55, 68));
            SetBrush(WpfSystemColors.ControlTextBrushKey, MediaColor.FromRgb(233, 238, 245));
            SetBrush(WpfSystemColors.WindowBrushKey, MediaColor.FromRgb(23, 30, 38));
            SetBrush(WpfSystemColors.WindowTextBrushKey, MediaColor.FromRgb(233, 238, 245));
            return;
        }

        SetBrush("AppBackgroundBrush", MediaColor.FromRgb(246, 247, 249));
        SetBrush("SurfaceBrush", Colors.White);
        SetBrush("SidebarBrush", MediaColor.FromRgb(24, 32, 43));
        SetBrush("AccentBrush", MediaColor.FromRgb(30, 122, 95));
        SetBrush("AccentSoftBrush", MediaColor.FromRgb(232, 243, 239));
        SetBrush("BorderBrush", MediaColor.FromRgb(216, 222, 230));
        SetBrush("TextBrush", MediaColor.FromRgb(23, 32, 42));
        SetBrush("MutedBrush", MediaColor.FromRgb(100, 113, 132));
        SetBrush("SidebarTextBrush", Colors.White);
        SetBrush("SidebarMutedBrush", MediaColor.FromRgb(185, 195, 208));
        SetBrush("VersionBadgeBrush", MediaColor.FromRgb(40, 68, 95));
        SetBrush("VersionBadgeTextBrush", MediaColor.FromRgb(217, 226, 236));
        SetBrush("ButtonBackgroundBrush", Colors.White);
        SetBrush("ButtonHoverBrush", MediaColor.FromRgb(240, 244, 247));
        SetBrush("ButtonPressedBrush", MediaColor.FromRgb(228, 234, 240));
        SetBrush("ButtonBorderBrush", MediaColor.FromRgb(216, 222, 230));
        SetBrush("ButtonForegroundBrush", MediaColor.FromRgb(23, 32, 42));
        SetBrush("ButtonDisabledBackgroundBrush", MediaColor.FromRgb(238, 241, 244));
        SetBrush("ButtonDisabledBorderBrush", MediaColor.FromRgb(216, 222, 230));
        SetBrush("ButtonDisabledForegroundBrush", MediaColor.FromRgb(154, 166, 178));
        SetBrush("PrimaryButtonBackgroundBrush", MediaColor.FromRgb(30, 122, 95));
        SetBrush("PrimaryButtonHoverBrush", MediaColor.FromRgb(23, 107, 82));
        SetBrush("PrimaryButtonPressedBrush", MediaColor.FromRgb(18, 89, 65));
        SetBrush("PrimaryButtonBorderBrush", MediaColor.FromRgb(30, 122, 95));
        SetBrush("PrimaryButtonForegroundBrush", Colors.White);
        SetBrush(WpfSystemColors.HighlightBrushKey, MediaColor.FromRgb(232, 243, 239));
        SetBrush(WpfSystemColors.HighlightTextBrushKey, MediaColor.FromRgb(23, 32, 42));
        SetBrush(WpfSystemColors.InactiveSelectionHighlightBrushKey, MediaColor.FromRgb(232, 243, 239));
        SetBrush(WpfSystemColors.InactiveSelectionHighlightTextBrushKey, MediaColor.FromRgb(23, 32, 42));
        SetBrush(WpfSystemColors.ControlBrushKey, MediaColor.FromRgb(240, 244, 247));
        SetBrush(WpfSystemColors.ControlTextBrushKey, MediaColor.FromRgb(23, 32, 42));
        SetBrush(WpfSystemColors.WindowBrushKey, Colors.White);
        SetBrush(WpfSystemColors.WindowTextBrushKey, MediaColor.FromRgb(23, 32, 42));
    }

    private static void SetBrush(object key, MediaColor color)
    {
        if (WpfApplication.Current.TryFindResource(key) is SolidColorBrush brush && !brush.IsFrozen)
        {
            brush.Color = color;
            return;
        }

        WpfApplication.Current.Resources[key] = new SolidColorBrush(color);
    }
}
