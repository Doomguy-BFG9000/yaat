using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.LogicalTree;
using Xunit;
using Yaat.Client.ContextMenus;
using Yaat.Client.Models;
using Yaat.Client.UI.Tests.Fakes;
using Yaat.Client.UI.Tests.Helpers;
using Yaat.Client.ViewModels;
using Yaat.Client.Views;
using Yaat.Sim.Commands;
using Yaat.Sim.Data;

namespace Yaat.Client.UI.Tests.Views;

/// <summary>
/// The four validated input pickers — Squawk…, Temporary altitude…, Cruise… and Speed… — driven through the real input
/// popup: text the picker cannot read sends nothing, keeps the popup open and shows its one-line reason under the box,
/// and every form the picker does read sends the command it should.
/// </summary>
public class MenuInputValidationTests
{
    private const string Callsign = "SWA104";

    private const string BadAltitude = "Not a valid altitude";
    private const string BadSquawk = "Not a valid squawk code (0000-7777)";
    private const string BadSpeed = "Not a valid speed";

    // --- Temporary altitude… and Cruise… --------------------------------------------------------

    [AvaloniaTheory]
    [InlineData("050", "TEMPALT 50")]
    [InlineData("230", "TEMPALT 230")]
    [InlineData("FL230", "TEMPALT 230")]
    [InlineData("fl 230", "TEMPALT 230")]
    [InlineData("5,000", "TEMPALT 50")]
    [InlineData("23,000", "TEMPALT 230")]
    [InlineData("FL999", "TEMPALT 999")]
    [InlineData("99,900", "TEMPALT 999")]
    public void TemporaryAltitude_AcceptedForms_SendTheAltitude(string typed, string command) =>
        AssertSends(MenuIds.DataBlockTempAltitude, typed, command);

    [AvaloniaTheory]
    [InlineData("050", "CRUISE 50")]
    [InlineData("230", "CRUISE 230")]
    [InlineData("FL230", "CRUISE 230")]
    [InlineData("5,000", "CRUISE 50")]
    [InlineData("23,000", "CRUISE 230")]
    public void Cruise_AcceptedForms_SendTheAltitude(string typed, string command) => AssertSends(MenuIds.DataBlockCruise, typed, command);

    [AvaloniaTheory]
    [InlineData(MenuIds.DataBlockTempAltitude)]
    [InlineData(MenuIds.DataBlockCruise)]
    public void Altitude_TextItCannotRead_SendsNothingAndKeepsThePopupOpen(string id) => AssertRefuses(id, "FL", BadAltitude);

    /// <summary>
    /// Text the picker reads as an altitude the sim cannot fly is refused too: past 99,900 ft the hundreds argument would
    /// wrap into a reading of feet, a flight level is at most three digits, and a comma must group a whole thousand.
    /// </summary>
    [AvaloniaTheory]
    [InlineData(MenuIds.DataBlockTempAltitude, "100000")]
    [InlineData(MenuIds.DataBlockTempAltitude, "FL1200")]
    [InlineData(MenuIds.DataBlockTempAltitude, "2,30")]
    [InlineData(MenuIds.DataBlockCruise, "100000")]
    [InlineData(MenuIds.DataBlockCruise, "FL1200")]
    public void Altitude_UnflyableText_SendsNothingAndKeepsThePopupOpen(string id, string typed) => AssertRefuses(id, typed, BadAltitude);

    /// <summary>
    /// An AGL altitude keeps an airport code that starts with FL: the prefix comes off only in front of a flight level, so
    /// FLL reaches the resolver as itself rather than as the airport "L".
    /// </summary>
    [AvaloniaFact]
    public void TemporaryAltitude_AglAtAnAirportStartingWithFl_KeepsItsCode()
    {
        using IDisposable navScope = NavigationDatabase.ScopedOverride(MenuGoldenFixtures.EnsureNavData());
        double elevation =
            NavigationDatabase.Instance.GetAirportElevation("FLL")
            ?? throw new InvalidOperationException("The test navigation data has no elevation for FLL");
        int hundreds = ((int)Math.Round(elevation) + 1000) / 100;

        AssertSends(MenuIds.DataBlockTempAltitude, "FLL+010", $"TEMPALT {hundreds}");
    }

    /// <summary>A refusal leaves the popup open and usable: the next, readable submit sends and closes it.</summary>
    [AvaloniaFact]
    public void TemporaryAltitude_AfterARefusal_AReadableFormStillSends()
    {
        (Window window, Control anchor, SendCapturingHost sent) = Open(MenuIds.DataBlockTempAltitude);
        try
        {
            Submit(anchor, "five thousand");

            Assert.Empty(sent.Sent);
            Assert.Equal(BadAltitude, Message(anchor));

            Submit(anchor, "FL230");

            Assert.Equal([(Callsign, "TEMPALT 230", "AB")], sent.Sent);
            AssertNoPopup(anchor);
        }
        finally
        {
            window.Close();
        }
    }

    // --- Squawk… --------------------------------------------------------------------------------

    [AvaloniaFact]
    public void Squawk_FourOctalDigits_SendsTheCode() => AssertSends(MenuIds.SquawkCode, "1234", "SQ 1234");

    [AvaloniaTheory]
    [InlineData("123")]
    [InlineData("12345")]
    [InlineData("1298")]
    [InlineData("ABCD")]
    public void Squawk_NotFourOctalDigits_SendsNothingAndKeepsThePopupOpen(string typed) => AssertRefuses(MenuIds.SquawkCode, typed, BadSquawk);

    // --- Speed… ---------------------------------------------------------------------------------

    [AvaloniaFact]
    public void Speed_APositiveNumber_SendsIt() => AssertSends(MenuIds.SpeedCustom, "210", "SPD 210");

    [AvaloniaTheory]
    [InlineData("fast")]
    [InlineData("2,10")]
    [InlineData("0")]
    [InlineData("-5")]
    public void Speed_NotAPositiveWholeNumber_SendsNothingAndKeepsThePopupOpen(string typed) => AssertRefuses(MenuIds.SpeedCustom, typed, BadSpeed);

    // --- Fixtures -------------------------------------------------------------------------------

    private static void AssertSends(string id, string typed, string command)
    {
        (Window window, Control anchor, SendCapturingHost sent) = Open(id);
        try
        {
            Submit(anchor, typed);

            Assert.Equal([(Callsign, command, "AB")], sent.Sent);
            AssertNoPopup(anchor);
        }
        finally
        {
            window.Close();
        }
    }

    private static void AssertRefuses(string id, string typed, string message)
    {
        (Window window, Control anchor, SendCapturingHost sent) = Open(id);
        try
        {
            Submit(anchor, typed);

            Assert.Empty(sent.Sent);
            Assert.Equal(message, Message(anchor));
            Assert.True(FindPopup(anchor).IsOpen, "A refused submit should leave the popup open.");
        }
        finally
        {
            window.Close();
        }
    }

    /// <summary>Opens the picker's input popup on a window's anchor, through the real client host under a send capturer.</summary>
    private static (Window Window, Control Anchor, SendCapturingHost Sent) Open(string id)
    {
        var main = new MainViewModel(new FakeFilePickerService());
        var aircraft = new AircraftModel
        {
            Callsign = Callsign,
            AircraftType = "B738",
            FlightRules = "IFR",
            IsOnGround = false,
        };
        main.Aircraft.Add(aircraft);

        var anchor = new Border();
        var window = new Window
        {
            Width = 400,
            Height = 200,
            Content = anchor,
        };
        window.ShowAndRunLayout();

        var sent = new SendCapturingHost(new ClientMenuHost(main, aircraft, anchor), "AB");
        MenuContext context = TestMenuContext.Create(Callsign, "AB", null, false, VfrCommandsForIfr.None);
        MenuItem item = Assert.IsType<MenuItem>(MenuCatalog.Get(id).Build(aircraft, context, sent));
        item.RaiseEvent(new RoutedEventArgs(MenuItem.ClickEvent));
        HeadlessWindowExtensions.PumpDispatcher();
        return (window, anchor, sent);
    }

    private static void Submit(Control anchor, string typed)
    {
        TextBox box = FindTextBox(anchor);
        box.Text = typed;
        box.RaiseEvent(new KeyEventArgs { RoutedEvent = InputElement.KeyDownEvent, Key = Key.Enter });
        HeadlessWindowExtensions.PumpDispatcher();
    }

    /// <summary>The one-line reason the open popup shows under its box.</summary>
    private static string? Message(Control anchor) =>
        FindPopup(anchor).Child?.GetLogicalDescendants().OfType<TextBlock>().LastOrDefault(text => text.IsVisible)?.Text;

    private static Popup FindPopup(Control anchor)
    {
        var overlay = OverlayLayer.GetOverlayLayer(anchor);
        Assert.NotNull(overlay);
        Popup? popup = overlay!.Children.OfType<Popup>().LastOrDefault();
        Assert.NotNull(popup);
        return popup!;
    }

    private static void AssertNoPopup(Control anchor)
    {
        var overlay = OverlayLayer.GetOverlayLayer(anchor);
        Assert.NotNull(overlay);
        Assert.Empty(overlay!.Children.OfType<Popup>());
    }

    private static TextBox FindTextBox(Control anchor)
    {
        TextBox? textBox = FindPopup(anchor).Child?.GetLogicalDescendants().OfType<TextBox>().FirstOrDefault();
        Assert.NotNull(textBox);
        return textBox!;
    }
}
