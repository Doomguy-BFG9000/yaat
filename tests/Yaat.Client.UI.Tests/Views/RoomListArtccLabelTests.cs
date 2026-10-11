using Avalonia.Controls;
using Avalonia.Controls.Documents;
using Avalonia.Headless.XUnit;
using Avalonia.VisualTree;
using Xunit;
using Yaat.Client.Services;
using Yaat.Client.UI.Tests.Helpers;
using Yaat.Client.ViewModels;
using Yaat.Client.Views;

namespace Yaat.Client.UI.Tests.Views;

/// <summary>
/// The Join Room list labels each room with the ARTCC the server lists it under (<see cref="TrainingRoomInfoDto.ArtccId"/>:
/// its scenario's ARTCC, else its creator's), the label the feed's room list gives it, not the ARTCC it was created under.
/// </summary>
public class RoomListArtccLabelTests
{
    [AvaloniaFact]
    public void RoomRow_ShowsTheRoomsListedArtcc_NotTheCreatorsArtcc()
    {
        (MainWindow main, MainViewModel vm) = MainWindowHost.Boot();
        try
        {
            vm.ActiveRooms.Add(
                new TrainingRoomInfoDto(
                    RoomId: "ROOM-A",
                    CreatorInitials: "CX",
                    CreatorArtccId: "ZOA",
                    ArtccId: "ZLA",
                    ScenarioName: null,
                    Members: [],
                    IsPaused: true,
                    SimRate: 1.0,
                    ElapsedSeconds: 0,
                    AircraftCount: 0
                )
            );
            vm.ShowRoomList = true;
            MainWindowHost.Pump();

            string[] roomTitles =
            [
                .. main.GetVisualDescendants().OfType<TextBlock>().Select(TextOf).Where(t => t.EndsWith("'s Room", StringComparison.Ordinal)),
            ];

            string title = Assert.Single(roomTitles);
            Assert.Contains("ZLA", title, StringComparison.Ordinal);
            Assert.DoesNotContain("ZOA", title, StringComparison.Ordinal);
        }
        finally
        {
            MainWindowHost.CloseAll(main);
        }
    }

    private static string TextOf(TextBlock block) =>
        block.Inlines is { Count: > 0 } inlines ? string.Concat(inlines.OfType<Run>().Select(run => run.Text)) : block.Text ?? "";
}
