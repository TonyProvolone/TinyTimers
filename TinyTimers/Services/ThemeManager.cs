using System.Linq;
using System.Windows;
using TinyTimers.Models;
using Microsoft.Win32;

namespace TinyTimers.Services;

internal static class ThemeManager
{
    public static void Apply(AppTheme theme)
    {
        var resolved = theme == AppTheme.System ? DetectSystemTheme() : theme;
        var dictUri = resolved == AppTheme.Light
            ? new Uri("Themes/Light.xaml", UriKind.Relative)
            : new Uri("Themes/Dark.xaml", UriKind.Relative);

        var app = System.Windows.Application.Current;
        var existing = app.Resources.MergedDictionaries.FirstOrDefault(d => d.Source is { } src && src.OriginalString.Contains("Themes/"));
        if (existing is not null)
            app.Resources.MergedDictionaries.Remove(existing);

        app.Resources.MergedDictionaries.Add(new ResourceDictionary { Source = dictUri });
    }

    private static AppTheme DetectSystemTheme()
    {
        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Themes\Personalize");
            if (key?.GetValue("AppsUseLightTheme") is int value)
                return value == 0 ? AppTheme.Dark : AppTheme.Light;
        }
        catch (Exception ex) when (ex is UnauthorizedAccessException or System.Security.SecurityException)
        {
        }

        return AppTheme.Dark;
    }
}
