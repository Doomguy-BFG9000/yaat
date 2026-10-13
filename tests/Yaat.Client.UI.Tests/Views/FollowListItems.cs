using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.LogicalTree;
using Xunit;
using Yaat.Client.ContextMenus;

namespace Yaat.Client.UI.Tests.Views;

/// <summary>
/// Reads the Follow… entry as All Commands builds it: a filter box row, a hint row, then one row holding the scrolling
/// list of section titles (<see cref="TextBlock"/>s) and aircraft rows (<see cref="Button"/>s showing a
/// <see cref="GroundTrafficFlyoutRow"/>).
/// </summary>
internal static class FollowListItems
{
    /// <summary>The list's scroll viewer, found by its name.</summary>
    public static ScrollViewer Scroll(MenuItem follow) =>
        Assert.Single(follow.GetLogicalDescendants().OfType<ScrollViewer>(), viewer => viewer.Name == GroundTrafficFlyout.FollowListName);

    /// <summary>The list's section titles and aircraft rows, in order, hidden ones included.</summary>
    public static List<Control> Children(MenuItem follow) => [.. ((Panel)Scroll(follow).Content!).Children];

    /// <summary>The list's showing section titles and rows in order: a title's text, a row's one-line text.</summary>
    public static List<string> Entries(MenuItem follow) =>
        [
            .. Children(follow)
                .Where(child => child.IsVisible)
                .Select(child =>
                    child switch
                    {
                        TextBlock title => title.Text!,
                        Button { Content: GroundTrafficFlyoutRow row } => row.ToString(),
                        _ => throw new Xunit.Sdk.XunitException($"unexpected list child {child.GetType().Name}"),
                    }
                ),
        ];

    /// <summary>The callsigns of the list's showing rows, in order.</summary>
    public static List<string> Callsigns(MenuItem follow) =>
        [.. Children(follow).Where(child => child.IsVisible).OfType<Button>().Select(button => RowOf(button).Traffic.Callsign)];

    /// <summary>The list's showing section titles, in order.</summary>
    public static List<string> Titles(MenuItem follow) =>
        [.. Children(follow).Where(child => child.IsVisible).OfType<TextBlock>().Select(title => title.Text!)];

    /// <summary>The row for <paramref name="callsign"/>.</summary>
    public static Button Row(MenuItem follow, string callsign) =>
        Assert.Single(Children(follow).OfType<Button>(), button => RowOf(button).Traffic.Callsign == callsign);

    /// <summary>Clicks <paramref name="row"/> as a pointer or Enter on it does.</summary>
    public static void Click(Button row) => row.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));

    private static GroundTrafficFlyoutRow RowOf(Button button) => (GroundTrafficFlyoutRow)button.Content!;
}
