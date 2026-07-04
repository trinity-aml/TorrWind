using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Media3D;
using ButtonBase = System.Windows.Controls.Primitives.ButtonBase;

namespace TorrWind.App.Behaviors;

public static class CalendarHeaderModeBehavior
{
    public static readonly DependencyProperty EnableHeaderModeCyclingProperty =
        DependencyProperty.RegisterAttached(
            "EnableHeaderModeCycling",
            typeof(bool),
            typeof(CalendarHeaderModeBehavior),
            new PropertyMetadata(false, OnEnableHeaderModeCyclingChanged));

    public static void SetEnableHeaderModeCycling(DependencyObject element, bool value)
    {
        element.SetValue(EnableHeaderModeCyclingProperty, value);
    }

    public static bool GetEnableHeaderModeCycling(DependencyObject element)
    {
        return (bool)element.GetValue(EnableHeaderModeCyclingProperty);
    }

    private static void OnEnableHeaderModeCyclingChanged(DependencyObject element, DependencyPropertyChangedEventArgs e)
    {
        if (element is not Calendar calendar)
        {
            return;
        }

        calendar.PreviewMouseLeftButtonDown -= OnPreviewMouseLeftButtonDown;

        if (e.NewValue is true)
        {
            calendar.PreviewMouseLeftButtonDown += OnPreviewMouseLeftButtonDown;
        }
    }

    private static void OnPreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (sender is not Calendar calendar || e.OriginalSource is not DependencyObject source)
        {
            return;
        }

        var headerButton = FindAncestor<ButtonBase>(source);
        if (headerButton is not { Name: "PART_HeaderButton", IsEnabled: true })
        {
            return;
        }

        calendar.DisplayMode = calendar.DisplayMode switch
        {
            CalendarMode.Month => CalendarMode.Year,
            CalendarMode.Year => CalendarMode.Decade,
            _ => CalendarMode.Decade
        };

        e.Handled = true;
    }

    private static T? FindAncestor<T>(DependencyObject source)
        where T : DependencyObject
    {
        var current = source;
        while (current is not null)
        {
            if (current is T match)
            {
                return match;
            }

            current = current is Visual or Visual3D
                ? VisualTreeHelper.GetParent(current)
                : LogicalTreeHelper.GetParent(current);
        }

        return null;
    }
}
