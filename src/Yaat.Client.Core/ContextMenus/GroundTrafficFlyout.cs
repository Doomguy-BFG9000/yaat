using System.Text;
using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Controls.Templates;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Layout;
using Avalonia.LogicalTree;
using Avalonia.Media;
using Avalonia.Media.Immutable;
using Avalonia.Styling;
using Avalonia.Threading;

namespace Yaat.Client.ContextMenus;

/// <summary>
/// The Give way to… flyout over the host's ground traffic (<see cref="IMenuHost.GetGroundTrafficRows"/>), one builder for
/// the All Commands submenu and the quick-command strip's flyout: a <see cref="GiveWayTitle"/> title with the mark legend,
/// then one list of the other ground aircraft nearest first, live-traffic shadows left out, each row led by a moving
/// (▶) or parked (■) mark and sending <c>GW</c> with its callsign. The first <see cref="GiveWayShownRows"/> show; a
/// "Show n more" row shows the rest in place and, once they show, becomes "Show fewer", which hides them again; the
/// list falls back to eight rows when the flyout closes. A row highlights its aircraft while it is pointed at or holds
/// keyboard focus, and the highlight clears when it loses both and when the flyout closes. With no other aircraft on the
/// ground the entry is disabled, the reason faint beside its label, and the strip shows its button faded, opening nothing,
/// its label naming the same reason.
/// </summary>
public static class GroundTrafficFlyout
{
    /// <summary>The Give way to… flyout's title.</summary>
    public const string GiveWayTitle = "Nearest first";

    /// <summary>The legend at the right of the Give way to… title.</summary>
    public const string GiveWayLegend = "▶ moving  ■ parked";

    /// <summary>Why a traffic entry offers nothing, as its faded strip button's label says after the entry's name.</summary>
    public const string NoTrafficReason = "no other aircraft on the ground";

    /// <summary>The muted line a traffic flyout shows once its list has emptied while open.</summary>
    public const string NoTrafficText = "No other aircraft on the ground.";

    /// <summary>How many rows the Give way to… list shows before its "Show n more" row.</summary>
    public const int GiveWayShownRows = 8;

    /// <summary>The Follow… filter box's placeholder and accessible name.</summary>
    public const string FollowFilterPlaceholder = "Filter or type a callsign";

    /// <summary>The Follow… hint line while some aircraft matches the filter.</summary>
    public const string FollowTopMatchHint = "Enter sends the top match.";

    /// <summary>The Follow… list's title over the moving aircraft.</summary>
    public const string FollowMovingTitle = "Moving";

    /// <summary>The Follow… list's title over the parked and holding aircraft.</summary>
    public const string FollowParkedTitle = "Parked or holding";

    /// <summary>About how many rows tall the Follow… list is before it scrolls.</summary>
    public const int FollowShownRows = 8;

    /// <summary>The height of one Follow… row, in px.</summary>
    public const double FollowRowHeight = 44;

    /// <summary>The name of the Follow… filter box.</summary>
    public const string FollowFilterBoxName = "FollowFilter";

    /// <summary>The name of the border at the Follow… list's top that shows the section title scrolled past.</summary>
    public const string FollowStickyTitleName = "FollowStickyTitle";

    /// <summary>The name of the Follow… list's scroll viewer.</summary>
    public const string FollowListName = "FollowList";

    private const string GiveWayVerb = "GW";
    private const string FollowVerb = "FOLLOWG";
    private const string MenuBackgroundKey = "MenuFlyoutPresenterBackground";

    /// <summary>The class on the menu row hosting the Follow… list, which keeps its background at rest while a row inside is pointed at.</summary>
    private const string FollowListRowClass = "follow-list";

    /// <summary>The border in a menu row's template that draws its pointed-at and selected background.</summary>
    private const string MenuItemLayoutRootPart = "PART_LayoutRoot";

    private static readonly IImmutableSolidColorBrush SectionTitleBrush = new ImmutableSolidColorBrush(Color.Parse("#CFD4DA"));

    /// <summary>
    /// The Give way to… entry labelled <paramref name="label"/>: a submenu over the other ground aircraft, or, with none,
    /// a disabled item the strip shows as a faded button that opens nothing.
    /// </summary>
    /// <param name="label">The entry's text.</param>
    /// <param name="context">The menu's aircraft and initials.</param>
    /// <param name="host">The host the traffic comes from and the rows send through.</param>
    public static MenuItem BuildGiveWay(string label, MenuContext context, IMenuHost host)
    {
        var entry = new MenuItem { Header = label };
        List<MenuGroundTrafficRow> traffic = GiveWayTraffic(context, host);
        if (traffic.Count == 0)
        {
            return Inert(entry, label);
        }

        var list = new GiveWayList(entry, context, host);
        list.Attach(traffic);
        QuickCommandStrip.FollowInFlyout(entry, list);
        return entry;
    }

    /// <summary>
    /// The Follow… entry labelled <paramref name="label"/>: a submenu holding a filter box, the hint line under it and
    /// one scrolling list, about <see cref="FollowShownRows"/> rows tall, of the other ground aircraft under the sticky
    /// <see cref="FollowMovingTitle"/> and <see cref="FollowParkedTitle"/> titles, nearest first within each, live-traffic
    /// shadows included; or, with no other aircraft on the ground, a disabled item the strip shows faded.
    /// </summary>
    /// <param name="label">The entry's text.</param>
    /// <param name="context">The menu's aircraft and initials.</param>
    /// <param name="host">The host the traffic comes from and the rows send through.</param>
    public static MenuItem BuildFollow(string label, MenuContext context, IMenuHost host)
    {
        var entry = new MenuItem { Header = label };
        List<MenuGroundTrafficRow> traffic = [.. host.GetGroundTrafficRows(context.Callsign).OrderBy(row => row.DistanceFeet)];
        if (traffic.Count == 0)
        {
            return Inert(entry, label);
        }

        var list = new FollowList(entry, context, host);
        list.Fill(traffic);
        QuickCommandStrip.FollowInFlyout(entry, list);
        return entry;
    }

    /// <summary>The hint line's text when the filter matches no aircraft: <c>No aircraft matches; Enter sends FOLLOWG N123 as typed.</c></summary>
    public static string FollowNoMatchHint(string typed) => $"No aircraft matches; Enter sends {FollowVerb} {typed} as typed.";

    /// <summary>
    /// The faded strip button's label for the entry <paramref name="label"/>: <c>Give way to · no other aircraft on the ground</c>.
    /// </summary>
    internal static string InertLabel(string label) => $"{label.TrimEnd('…')} · {NoTrafficReason}";

    /// <summary>The other ground aircraft Give way to… offers, nearest first, live-traffic shadows left out.</summary>
    private static List<MenuGroundTrafficRow> GiveWayTraffic(MenuContext context, IMenuHost host) =>
        [.. host.GetGroundTrafficRows(context.Callsign).Where(row => !row.IsSurfaceShadow).OrderBy(row => row.DistanceFeet)];

    /// <summary>
    /// Disables <paramref name="entry"/>, which All Commands then draws with <see cref="NoTrafficReason"/> faint beside its
    /// label, and has the strip show its button faded, its label naming the same reason.
    /// </summary>
    private static MenuItem Inert(MenuItem entry, string label)
    {
        MenuDisabledReasonRow.Apply(entry, label, NoTrafficReason);
        QuickCommandStrip.MarkInert(entry, InertLabel(label));
        return entry;
    }

    /// <summary>Sends <paramref name="command"/> and closes the strip's flyout and the menu, or the submenu's menu.</summary>
    private static async Task ChooseAsync(string command, MenuItem entry, Action? closeFlyout, MenuContext context, IMenuHost host)
    {
        Task sending = host.SendAsync(context.Callsign, command, context.Initials);
        if (closeFlyout is not null)
        {
            closeFlyout();
        }
        else
        {
            entry.FindLogicalAncestorOfType<ContextMenu>()?.Close();
        }

        await sending;
    }

    private static MenuItem Muted(string text) => new() { Header = text, IsEnabled = false };

    /// <summary>
    /// The aircraft a traffic flyout highlights: the one of the row under the pointer, else the one of the row holding
    /// keyboard focus, else none. The two are tracked apart, so the pointer leaving a row hands the highlight back to the
    /// focused row, and focus passing through another row leaves the pointed row's highlight in place.
    /// </summary>
    private sealed class TrafficHighlight(IMenuHost host)
    {
        private string? _pointed;
        private string? _focused;

        /// <summary>Has <paramref name="row"/>, standing for <paramref name="callsign"/>, highlight it while pointed at or focused.</summary>
        public void Track(InputElement row, string callsign)
        {
            row.PointerEntered += (_, _) => Point(callsign);
            row.PointerExited += (_, _) => Unpoint(callsign);
            row.GotFocus += (_, _) => Focus(callsign);
            row.LostFocus += (_, _) => Blur(callsign);
        }

        /// <summary>Forgets the pointed and the focused row and clears the highlight, as the flyout closes.</summary>
        public void Clear()
        {
            _pointed = null;
            _focused = null;
            Apply();
        }

        private void Point(string callsign)
        {
            _pointed = callsign;
            Apply();
        }

        private void Unpoint(string callsign)
        {
            if (_pointed == callsign)
            {
                _pointed = null;
            }

            Apply();
        }

        private void Focus(string callsign)
        {
            _focused = callsign;
            Apply();
        }

        private void Blur(string callsign)
        {
            if (_focused == callsign)
            {
                _focused = null;
            }

            Apply();
        }

        private void Apply() => host.HighlightAircraft(_pointed ?? _focused);
    }

    /// <summary>
    /// The Give way to… rows over the host's traffic, on whichever surface shows them: filled when built, refilled when
    /// a surface opens and when "Show n more" or "Show fewer" is chosen. An aircraft keeps its row, by callsign, across a
    /// refill; a row no longer listed sends nothing, even on a click already on its way.
    /// </summary>
    private sealed class GiveWayList(MenuItem entry, MenuContext context, IMenuHost host) : IStripFlyoutContent
    {
        private readonly MenuItem _title = new()
        {
            Header = new GroundTrafficFlyoutTitle(GiveWayTitle, GiveWayLegend),
            HeaderTemplate = GroundTrafficTitleTemplate.Instance,
            IsEnabled = false,
        };

        private readonly MenuItem _empty = Muted(NoTrafficText);
        private readonly HashSet<MenuItem> _live = [];
        private readonly TrafficHighlight _highlight = new(host);
        private MenuItem? _more;
        private Dictionary<string, MenuItem> _rows = new(StringComparer.Ordinal);
        private ItemCollection? _flyoutItems;
        private Action? _closeFlyout;
        private bool _expanded;

        /// <summary>Fills the submenu with <paramref name="traffic"/> and folds the list back to eight rows whenever the submenu closes.</summary>
        public void Attach(List<MenuGroundTrafficRow> traffic)
        {
            Show(traffic);
            entry.PropertyChanged += (_, e) =>
            {
                if ((e.Property == MenuItem.IsSubMenuOpenProperty) && !entry.IsSubMenuOpen)
                {
                    _expanded = false;
                    Refill();
                }
            };
        }

        /// <inheritdoc />
        public void Opened(ItemCollection items, Action close)
        {
            _flyoutItems = items;
            _closeFlyout = close;
            Refill();
        }

        /// <inheritdoc />
        public void Closed()
        {
            _closeFlyout = null;
            _expanded = false;
            _highlight.Clear();
            Refill();
        }

        private void Refill() => Show(GiveWayTraffic(context, host));

        private void Show(List<MenuGroundTrafficRow> traffic) => FlyoutItemSync.Sync(_flyoutItems ?? entry.Items, Wanted(traffic));

        /// <summary>The items <paramref name="traffic"/> calls for, reusing the row of every aircraft shown before.</summary>
        private List<Control> Wanted(List<MenuGroundTrafficRow> traffic)
        {
            var rows = new Dictionary<string, MenuItem>(StringComparer.Ordinal);
            _live.Clear();
            if (traffic.Count == 0)
            {
                _rows = rows;
                return [_empty];
            }

            List<Control> wanted = [_title];
            foreach (MenuGroundTrafficRow row in _expanded ? traffic : traffic.Take(GiveWayShownRows))
            {
                MenuItem item =
                    (!rows.ContainsKey(row.Callsign) && _rows.TryGetValue(row.Callsign, out MenuItem? kept)) ? kept : NewRow(row.Callsign);
                rows.TryAdd(row.Callsign, item);
                Present(item, row);
                _live.Add(item);
                wanted.Add(item);
            }

            _rows = rows;
            if (traffic.Count > GiveWayShownRows)
            {
                List<MenuGroundTrafficRow> rest = traffic[GiveWayShownRows..];
                MenuItem more = _more ??= NewMoreRow();
                more.Header = new GroundTrafficMoreRow(rest.Count, rest.Max(row => row.DistanceFeet), _expanded);
                AutomationProperties.SetName(more, more.Header.ToString());
                wanted.Add(more);
            }

            return wanted;
        }

        private MenuItem NewRow(string callsign)
        {
            var item = new MenuItem { HeaderTemplate = GroundTrafficRowTemplate.Instance };
            item.Click += async (_, e) =>
            {
                if (!_live.Contains(item) || (item.Header is not GroundTrafficFlyoutRow row))
                {
                    e.Handled = true;
                    return;
                }

                await ChooseAsync(row.Command, entry, _closeFlyout, context, host);
            };
            _highlight.Track(item, callsign);
            return item;
        }

        private MenuItem NewMoreRow()
        {
            var more = new MenuItem { HeaderTemplate = GroundTrafficMoreRowTemplate.Instance, StaysOpenOnClick = true };
            more.Click += (_, e) =>
            {
                e.Handled = true;
                _expanded = !_expanded;
                Refill();
            };
            return more;
        }

        private static void Present(MenuItem item, MenuGroundTrafficRow traffic)
        {
            var row = new GroundTrafficFlyoutRow(traffic, $"{GiveWayVerb} {traffic.Callsign}", ShowsMark: true);
            item.Header = row;
            MenuCommandText.SetCommand(item, row.Command);
            AutomationProperties.SetName(item, row.ToString());
        }
    }

    /// <summary>
    /// The Follow… flyout's three rows: the filter box, the hint line and the scrolling list. Typing narrows the list to
    /// the callsigns holding the typed text, ignoring case, and hides a section title with no row left; matching nothing
    /// hides every row and title and the hint says what Enter sends. Enter in the box sends the top row, or, with no
    /// row left, <c>FOLLOWG</c> with the typed text upper-cased. Down from the box moves to the first row, Up from the
    /// first row back to the box; Enter or a click on a row sends it. A row highlights its aircraft while pointed at or
    /// focused. The section title scrolled past sticks at the list's top.
    /// </summary>
    private sealed class FollowList : IStripFlyoutContent
    {
        private readonly MenuItem _entry;
        private readonly MenuContext _context;
        private readonly IMenuHost _host;
        private readonly TextBox _box;
        private readonly TextBlock _hint;
        private readonly StackPanel _rows = new();
        private readonly ScrollViewer _scroll;
        private readonly Border _sticky;
        private readonly List<(TextBlock Title, List<Button> Rows)> _sections = [];
        private readonly TrafficHighlight _highlight;
        private Action? _closeFlyout;

        public FollowList(MenuItem entry, MenuContext context, IMenuHost host)
        {
            _entry = entry;
            _context = context;
            _host = host;
            _highlight = new TrafficHighlight(host);
            _box = new TextBox
            {
                Name = FollowFilterBoxName,
                PlaceholderText = FollowFilterPlaceholder,
                MinWidth = 220,
                VerticalAlignment = VerticalAlignment.Center,
            };
            AutomationProperties.SetName(_box, FollowFilterPlaceholder);
            _box.PropertyChanged += (_, e) =>
            {
                if (e.Property == TextBox.TextProperty)
                {
                    ApplyFilter();
                }
            };
            _box.AddHandler(InputElement.KeyDownEvent, OnBoxKeyDown, RoutingStrategies.Tunnel);
            _hint = GroundTrafficLook.Subtle(FollowTopMatchHint, 11);
            _scroll = new ScrollViewer
            {
                Name = FollowListName,
                Content = _rows,
                MaxHeight = FollowShownRows * FollowRowHeight,
                HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled,
            };
            // Raised when the offset, the extent or the viewport changes, so a filter that moves the titles under a scroll
            // offset that stays put still restates the stuck title.
            _scroll.ScrollChanged += (_, _) => UpdateSticky();
            _sticky = new Border
            {
                Name = FollowStickyTitleName,
                Child = SectionTitle(""),
                IsVisible = false,
                IsHitTestVisible = false,
                VerticalAlignment = VerticalAlignment.Top,
            };
            _sticky.Bind(Border.BackgroundProperty, _sticky.GetResourceObservable(MenuBackgroundKey));

            var boxLine = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8 };
            boxLine.Children.Add(QuickCommandStrip.GlyphIcon(QuickCommandGlyphs.For(MenuIds.GroundFollow)!));
            boxLine.Children.Add(_box);
            var boxItem = new MenuItem { Header = boxLine, StaysOpenOnClick = true };
            // The flyout focuses its first row as it opens; handed on to the box, the flyout opens ready for typing.
            FlyoutBoxFocus.HandFocusToTheBox(boxItem);
            entry.Items.Add(KeepClicks(boxItem));
            entry.Items.Add(new MenuItem { Header = _hint, IsEnabled = false });
            // A menu row takes keyboard focus back from whatever inside it gets it; the list's row would take it from its
            // buttons, so it is not focusable, and focus stays on the button Down, Up or a click gave it.
            var listItem = new MenuItem
            {
                Header = new Panel { Children = { _scroll, _sticky } },
                StaysOpenOnClick = true,
                Focusable = false,
            };
            // The menu selects the row under the pointer and paints it; this row is the whole list, so it keeps its background
            // at rest and only the pointed row inside shows the pointer.
            listItem.Classes.Add(FollowListRowClass);
            listItem.Styles.Add(PlainListRowStyle());
            entry.Items.Add(KeepClicks(listItem));
        }

        /// <summary>Lists <paramref name="traffic"/>, nearest first, Moving then Parked or holding, and applies the filter to it.</summary>
        public void Fill(List<MenuGroundTrafficRow> traffic)
        {
            _rows.Children.Clear();
            _sections.Clear();
            AddSection(FollowMovingTitle, [.. traffic.Where(row => row.IsMoving)]);
            AddSection(FollowParkedTitle, [.. traffic.Where(row => !row.IsMoving)]);
            ApplyFilter();
        }

        /// <inheritdoc />
        public void Opened(ItemCollection items, Action close) => _closeFlyout = close;

        /// <inheritdoc />
        public void Closed()
        {
            _closeFlyout = null;
            _box.Text = "";
            _highlight.Clear();
        }

        private static MenuItem KeepClicks(MenuItem item)
        {
            item.Click += (_, e) => e.Handled = true;
            return item;
        }

        /// <summary>Keeps the list's menu row at its resting background while it is selected or under the pointer.</summary>
        private static Style PlainListRowStyle()
        {
            static Selector LayoutRoot(Selector? x, string state) =>
                x.OfType<MenuItem>().Class(FollowListRowClass).Class(state).Template().OfType<Border>().Name(MenuItemLayoutRootPart);

            return new Style(x => Selectors.Or(LayoutRoot(x, ":selected"), LayoutRoot(x, ":pointerover")))
            {
                Setters = { new Setter(Border.BackgroundProperty, Brushes.Transparent) },
            };
        }

        private static TextBlock SectionTitle(string text) =>
            new()
            {
                Text = text,
                FontSize = 12,
                FontWeight = FontWeight.SemiBold,
                Foreground = SectionTitleBrush,
                Padding = new Thickness(8, 4),
            };

        private void AddSection(string title, List<MenuGroundTrafficRow> traffic)
        {
            if (traffic.Count == 0)
            {
                return;
            }

            TextBlock titleBlock = SectionTitle(title);
            _rows.Children.Add(titleBlock);
            List<Button> rows = [.. traffic.Select(NewRow)];
            _rows.Children.AddRange(rows);
            _sections.Add((titleBlock, rows));
        }

        private Button NewRow(MenuGroundTrafficRow traffic)
        {
            var row = new GroundTrafficFlyoutRow(traffic, $"{FollowVerb} {traffic.Callsign}", ShowsMark: false);
            var button = new Button
            {
                Content = row,
                ContentTemplate = GroundTrafficRowTemplate.Instance,
                MinHeight = FollowRowHeight,
                Padding = new Thickness(8, 4),
                Background = Brushes.Transparent,
                HorizontalAlignment = HorizontalAlignment.Stretch,
                HorizontalContentAlignment = HorizontalAlignment.Stretch,
            };
            AutomationProperties.SetName(button, row.ToString());
            button.Click += async (_, e) =>
            {
                e.Handled = true;
                await Choose(row.Command);
            };
            button.AddHandler(InputElement.KeyDownEvent, OnRowKeyDown, RoutingStrategies.Tunnel);
            _highlight.Track(button, traffic.Callsign);
            return button;
        }

        private Task Choose(string command) => ChooseAsync(command, _entry, _closeFlyout, _context, _host);

        private List<Button> VisibleRows() => [.. _sections.SelectMany(section => section.Rows).Where(row => row.IsVisible)];

        private string Typed() => (_box.Text ?? "").Trim();

        /// <summary>The callsign Enter sends when no row matches: the first typed word, upper-cased; null with nothing typed.</summary>
        private string? TypedCallsign() => Typed().Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries).FirstOrDefault()?.ToUpperInvariant();

        private void ApplyFilter()
        {
            string typed = Typed();
            foreach ((TextBlock title, List<Button> rows) in _sections)
            {
                foreach (Button row in rows)
                {
                    row.IsVisible = ((GroundTrafficFlyoutRow)row.Content!).Traffic.Callsign.Contains(typed, StringComparison.OrdinalIgnoreCase);
                }

                title.IsVisible = rows.Any(row => row.IsVisible);
            }

            bool matchesNone = (VisibleRows().Count == 0) && (TypedCallsign() is not null);
            _hint.Text = matchesNone ? FollowNoMatchHint(TypedCallsign()!) : FollowTopMatchHint;
            // The titles take their new places at the next layout pass, which Loaded runs after.
            Dispatcher.UIThread.Post(UpdateSticky, DispatcherPriority.Loaded);
        }

        /// <summary>The command Enter in the box sends: the top row's, else <c>FOLLOWG</c> with the typed callsign, else none.</summary>
        private string? EnterCommand()
        {
            if (VisibleRows().FirstOrDefault()?.Content is GroundTrafficFlyoutRow top)
            {
                return top.Command;
            }

            return (TypedCallsign() is { } callsign) ? $"{FollowVerb} {callsign}" : null;
        }

        /// <summary>Focuses <paramref name="row"/> and scrolls it into view clear of the section title stuck over the list's top.</summary>
        private void FocusRow(Button row)
        {
            row.Focus();
            double titleHeight = _sections.Max(section => section.Title.Bounds.Height);
            row.BringIntoView(new Rect(0, -titleHeight, row.Bounds.Width, row.Bounds.Height + titleHeight));
        }

        private async void OnBoxKeyDown(object? sender, KeyEventArgs e)
        {
            if (e.Key == Key.Down)
            {
                e.Handled = true;
                if (VisibleRows().FirstOrDefault() is { } first)
                {
                    FocusRow(first);
                }
            }
            else if (e.Key == Key.Enter)
            {
                e.Handled = true;
                if (EnterCommand() is { } command)
                {
                    await Choose(command);
                }
            }
        }

        private void OnRowKeyDown(object? sender, KeyEventArgs e)
        {
            if ((e.Key is not (Key.Up or Key.Down)) || (sender is not Button row))
            {
                return;
            }

            e.Handled = true;
            List<Button> visible = VisibleRows();
            int at = visible.IndexOf(row) + ((e.Key == Key.Down) ? 1 : -1);
            if (at < 0)
            {
                _box.Focus();
            }
            else if (at < visible.Count)
            {
                FocusRow(visible[at]);
            }
        }

        /// <summary>Shows the title of the section the list has scrolled into, once its own title has scrolled out of view.</summary>
        private void UpdateSticky()
        {
            string? current = null;
            foreach ((TextBlock title, _) in _sections)
            {
                if (title.IsVisible && (title.Bounds.Y < _scroll.Offset.Y))
                {
                    current = title.Text;
                }
            }

            ((TextBlock)_sticky.Child!).Text = current ?? "";
            _sticky.IsVisible = current is not null;
        }
    }
}

/// <summary>A traffic flyout's title row: the bold title and, at its right, a dim note.</summary>
/// <param name="Title">The title, <c>Nearest first</c>.</param>
/// <param name="Note">The note at the right, <c>▶ moving  ■ parked</c>.</param>
public sealed record GroundTrafficFlyoutTitle(string Title, string Note)
{
    /// <inheritdoc />
    public override string ToString() => $"{Title} · {Note}";
}

/// <summary>
/// One aircraft row of a traffic flyout, drawn by <see cref="GroundTrafficRowTemplate"/>: the moving or parked mark when
/// <paramref name="ShowsMark"/>, the callsign with the type dimmed beside it, the state under them, the distance and the
/// command. Its text form, which the row's automation name uses, reads
/// <c>SWA5286 · B738 · taxiing on W · ahead · ~1,100 ft — GW SWA5286</c>.
/// </summary>
/// <param name="Traffic">The aircraft as the host sees it.</param>
/// <param name="Command">The command the row sends.</param>
/// <param name="ShowsMark">Whether the row leads with the moving (▶) or parked (■) mark.</param>
public sealed record GroundTrafficFlyoutRow(MenuGroundTrafficRow Traffic, string Command, bool ShowsMark)
{
    /// <summary>The distance the row shows, <c>~1,100 ft</c>.</summary>
    public string Distance => $"~{RelativeGeometry.FeetText(Traffic.DistanceFeet)}";

    /// <inheritdoc />
    public override string ToString()
    {
        var text = new StringBuilder(Traffic.Callsign);
        if (Traffic.AircraftType.Length > 0)
        {
            text.Append(" · ").Append(Traffic.AircraftType);
        }

        return text.Append(" · ").Append(Traffic.State).Append(" · ").Append(Distance).Append(" — ").Append(Command).ToString();
    }
}

/// <summary>
/// The row under the first rows of a traffic flyout: <c>Show 5 more · up to ~3,900 ft</c> while the rest is hidden,
/// <c>Show fewer</c> once it shows.
/// </summary>
/// <param name="Count">How many rows are past the first ones.</param>
/// <param name="FarthestFeet">The distance of the farthest of them.</param>
/// <param name="Expanded">Whether they show.</param>
public sealed record GroundTrafficMoreRow(int Count, double FarthestFeet, bool Expanded)
{
    /// <summary>What the row reads before its note: <c>Show 5 more</c> or <c>Show fewer</c>.</summary>
    public string Label => Expanded ? "Show fewer" : $"Show {Count} more";

    /// <summary>The dim note after the label, <c>up to ~3,900 ft</c>, or null once the rest shows.</summary>
    public string? Note => Expanded ? null : $"up to ~{RelativeGeometry.FeetText(FarthestFeet)}";

    /// <inheritdoc />
    public override string ToString() => (Note is { } note) ? $"{Label} · {note}" : Label;
}

/// <summary>The faces and brushes the traffic flyout templates share.</summary>
internal static class GroundTrafficLook
{
    private const string SubtleTextBrushKey = "SubtleTextBrush";

    /// <summary>The moving mark's colour, the ground family's teal.</summary>
    public static readonly IImmutableSolidColorBrush MovingBrush = new ImmutableSolidColorBrush(
        QuickCommandStrip.FamilyColor(QuickCommandGlyphFamily.Ground)
    );

    /// <summary>The parked mark's colour.</summary>
    public static readonly IImmutableSolidColorBrush ParkedBrush = new ImmutableSolidColorBrush(Color.Parse("#8C95A0"));

    /// <summary>A text in the dim face at <paramref name="fontSize"/>.</summary>
    public static TextBlock Subtle(string text, double fontSize)
    {
        var block = new TextBlock
        {
            Text = text,
            FontSize = fontSize,
            VerticalAlignment = VerticalAlignment.Center,
        };
        block.Bind(TextBlock.ForegroundProperty, block.GetResourceObservable(SubtleTextBrushKey));
        return block;
    }

    /// <summary>A command in the dim monospace face, right-aligned.</summary>
    public static TextBlock Command(string command)
    {
        TextBlock block = Subtle(command, 12);
        block.HorizontalAlignment = HorizontalAlignment.Right;
        block.Bind(TextBlock.FontFamilyProperty, block.GetResourceObservable(QuickCommandStrip.MonoFontKey));
        return block;
    }
}

/// <summary>
/// The view of a <see cref="GroundTrafficFlyoutRow"/>: the mark (▶ teal for moving, ■ grey for parked), the callsign in a
/// semibold face with the type dimmed beside it and the state under them, the distance, then the command in the dim
/// monospace font.
/// </summary>
internal sealed class GroundTrafficRowTemplate : FuncDataTemplate<GroundTrafficFlyoutRow>
{
    /// <summary>The one template every traffic row's menu item uses.</summary>
    public static GroundTrafficRowTemplate Instance { get; } = new();

    private GroundTrafficRowTemplate()
        : base((row, _) => Build(row)) { }

    private static Grid Build(GroundTrafficFlyoutRow row)
    {
        var grid = new Grid { ColumnDefinitions = new ColumnDefinitions("Auto,*,Auto,Auto"), ColumnSpacing = 8 };
        if (row.ShowsMark)
        {
            grid.Children.Add(
                new TextBlock
                {
                    Text = row.Traffic.IsMoving ? "▶" : "■",
                    Width = 14,
                    FontSize = 10,
                    Foreground = row.Traffic.IsMoving ? GroundTrafficLook.MovingBrush : GroundTrafficLook.ParkedBrush,
                    VerticalAlignment = VerticalAlignment.Center,
                }
            );
        }
        else
        {
            grid.Children.Add(QuickCommandStrip.GlyphIcon(QuickCommandGlyphs.For(MenuIds.GroundFollow)!));
        }

        var nameLine = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 5 };
        nameLine.Children.Add(new TextBlock { Text = row.Traffic.Callsign, FontWeight = FontWeight.SemiBold });
        if (row.Traffic.AircraftType.Length > 0)
        {
            TextBlock type = GroundTrafficLook.Subtle(row.Traffic.AircraftType, 12);
            type.Opacity = 0.75;
            nameLine.Children.Add(type);
        }

        var nameColumn = new StackPanel { VerticalAlignment = VerticalAlignment.Center, Spacing = 1 };
        nameColumn.Children.Add(nameLine);
        nameColumn.Children.Add(GroundTrafficLook.Subtle(row.Traffic.State, 11));
        Grid.SetColumn(nameColumn, 1);
        grid.Children.Add(nameColumn);

        var distance = new TextBlock { Text = row.Distance, VerticalAlignment = VerticalAlignment.Center };
        Grid.SetColumn(distance, 2);
        grid.Children.Add(distance);

        TextBlock command = GroundTrafficLook.Command(row.Command);
        command.Opacity = 0.7;
        Grid.SetColumn(command, 3);
        grid.Children.Add(command);
        return grid;
    }
}

/// <summary>The view of a <see cref="GroundTrafficFlyoutTitle"/>: the title in a semibold face, the note dim at the right.</summary>
internal sealed class GroundTrafficTitleTemplate : FuncDataTemplate<GroundTrafficFlyoutTitle>
{
    /// <summary>The one template every traffic flyout title uses.</summary>
    public static GroundTrafficTitleTemplate Instance { get; } = new();

    private GroundTrafficTitleTemplate()
        : base((title, _) => Build(title)) { }

    private static Grid Build(GroundTrafficFlyoutTitle title)
    {
        var grid = new Grid { ColumnDefinitions = new ColumnDefinitions("*,Auto"), ColumnSpacing = 12 };
        grid.Children.Add(
            new TextBlock
            {
                Text = title.Title,
                FontSize = 12,
                FontWeight = FontWeight.SemiBold,
            }
        );
        TextBlock note = GroundTrafficLook.Subtle(title.Note, 11);
        Grid.SetColumn(note, 1);
        grid.Children.Add(note);
        return grid;
    }
}

/// <summary>The view of a <see cref="GroundTrafficMoreRow"/>: the label, its dim note, then ▾ (or ▴ once the rest shows) at the right.</summary>
internal sealed class GroundTrafficMoreRowTemplate : FuncDataTemplate<GroundTrafficMoreRow>
{
    /// <summary>The one template every "Show n more" row uses.</summary>
    public static GroundTrafficMoreRowTemplate Instance { get; } = new();

    private GroundTrafficMoreRowTemplate()
        : base((row, _) => Build(row)) { }

    private static Grid Build(GroundTrafficMoreRow row)
    {
        var grid = new Grid { ColumnDefinitions = new ColumnDefinitions("14,*,Auto"), ColumnSpacing = 8 };
        var label = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 5 };
        label.Children.Add(new TextBlock { Text = row.Label, VerticalAlignment = VerticalAlignment.Center });
        if (row.Note is { } note)
        {
            label.Children.Add(GroundTrafficLook.Subtle(note, 12));
        }

        Grid.SetColumn(label, 1);
        grid.Children.Add(label);
        TextBlock chevron = GroundTrafficLook.Subtle(row.Expanded ? "▴" : "▾", 12);
        Grid.SetColumn(chevron, 2);
        grid.Children.Add(chevron);
        return grid;
    }
}
