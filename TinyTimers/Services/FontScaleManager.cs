using System.Linq;
using System.Windows;
using TinyTimers.Models;

namespace TinyTimers.Services;

/// <summary>Pushes timer-row sizing into Application-level DynamicResource values so every open
/// timer row picks up a change immediately (same live-swap approach as ThemeManager, just with
/// plain values instead of a merged dictionary). Every dimension a bigger font could overflow -
/// button size, padding, min-widths - scales together with it, so bumping just the font size
/// setting can't by itself clip content against the card's fixed layout.</summary>
internal static class FontScaleManager
{
    public static void Apply(SizeScale nameSize, SizeScale valueSize, SizeScale buttonSize)
    {
        var buttonFontSize = ButtonFontSize(buttonSize);
        var maxSize = new[] { nameSize, valueSize, buttonSize }.Max();

        var res = System.Windows.Application.Current.Resources;
        res["TimerNameFontSize"] = NameFontSize(nameSize);
        res["TimerLinkIconFontSize"] = LinkIconFontSize(nameSize);
        res["TimerValueFontSize"] = ValueFontSize(valueSize);
        res["TimerButtonFontSize"] = buttonFontSize;
        res["TimerButtonSquareSize"] = buttonFontSize + 12;
        res["TimerToggleMinWidth"] = ToggleMinWidth(buttonSize);
        res["TimerTogglePadding"] = TogglePadding(buttonSize);
        res["TimerButtonSpacing"] = new Thickness(ButtonSpacing(buttonSize), 0, 0, 0);
        res["TimerRowPadding"] = new Thickness(RowPadding(maxSize));
        res["TimerRowGap"] = new Thickness(0, 0, 0, RowGap(maxSize));
    }

    private static double NameFontSize(SizeScale s) => s switch
    {
        SizeScale.Large => 19,
        SizeScale.Giant => 23,
        _ => 16,
    };

    private static double LinkIconFontSize(SizeScale s) => s switch
    {
        SizeScale.Large => 13,
        SizeScale.Giant => 16,
        _ => 11,
    };

    private static double ValueFontSize(SizeScale s) => s switch
    {
        SizeScale.Large => 20,
        SizeScale.Giant => 25,
        _ => 16,
    };

    private static double ButtonFontSize(SizeScale s) => s switch
    {
        SizeScale.Large => 16,
        SizeScale.Giant => 19,
        _ => 14,
    };

    private static double ToggleMinWidth(SizeScale s) => s switch
    {
        SizeScale.Large => 70,
        SizeScale.Giant => 82,
        _ => 60,
    };

    private static Thickness TogglePadding(SizeScale s) => s switch
    {
        SizeScale.Large => new Thickness(12, 5, 12, 5),
        SizeScale.Giant => new Thickness(14, 6, 14, 6),
        _ => new Thickness(10, 4, 10, 4),
    };

    private static double ButtonSpacing(SizeScale s) => s switch
    {
        SizeScale.Large => 6,
        SizeScale.Giant => 8,
        _ => 4,
    };

    private static double RowPadding(SizeScale s) => s switch
    {
        SizeScale.Large => 14,
        SizeScale.Giant => 16,
        _ => 12,
    };

    private static double RowGap(SizeScale s) => s switch
    {
        SizeScale.Large => 7,
        SizeScale.Giant => 8,
        _ => 6,
    };
}
