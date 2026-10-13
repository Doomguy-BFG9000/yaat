using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.VisualTree;

namespace Yaat.Client.ContextMenus;

/// <summary>Brings a live flyout's or submenu's items to a wanted list in place, so a surviving row keeps its pointer-over and focus.</summary>
internal static class FlyoutItemSync
{
    /// <summary>
    /// Moves <paramref name="items"/> to read <paramref name="wanted"/>, in order, touching only what differs: an item in
    /// both stays where it is unless the order moved it, so a surviving row keeps its pointer-over and its focus.
    /// </summary>
    public static void Sync(ItemCollection items, IReadOnlyList<Control> wanted)
    {
        var keep = new HashSet<object?>(wanted, ReferenceEqualityComparer.Instance);
        for (int i = items.Count - 1; i >= 0; i--)
        {
            if (!keep.Contains(items[i]))
            {
                items.RemoveAt(i);
            }
        }

        for (int i = 0; i < wanted.Count; i++)
        {
            if ((i < items.Count) && ReferenceEquals(items[i], wanted[i]))
            {
                continue;
            }

            int at = items.IndexOf(wanted[i]);
            if (at >= 0)
            {
                items.RemoveAt(at);
            }

            items.Insert(i, wanted[i]);
        }
    }
}

/// <summary>Keyboard focus for a flyout row holding a text box.</summary>
internal static class FlyoutBoxFocus
{
    /// <summary>
    /// Passes keyboard focus that lands on <paramref name="item"/> itself on to the text box in its header. A menu selects
    /// and focuses the row under the pointer or the arrow keys, and focuses it again once a click in its box is over,
    /// which would leave the box unable to take typing; handed on, the caret goes into the box instead.
    /// </summary>
    public static void HandFocusToTheBox(MenuItem item) =>
        item.AddHandler(
            InputElement.GotFocusEvent,
            (_, e) =>
            {
                if (ReferenceEquals(e.Source, item) && (item.GetVisualDescendants().OfType<TextBox>().FirstOrDefault() is { } box))
                {
                    box.Focus(e.NavigationMethod);
                }
            },
            RoutingStrategies.Bubble,
            handledEventsToo: true
        );
}
