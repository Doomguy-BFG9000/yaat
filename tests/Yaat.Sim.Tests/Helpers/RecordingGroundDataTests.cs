using Xunit;
using Yaat.Sim.Data.Airport;
using Yaat.Sim.Simulation;

namespace Yaat.Sim.Tests.Helpers;

/// <summary>
/// <see cref="RecordingGroundData.VersionPinned"/> serves the airport layouts an archive carries, by either spelling of the
/// airport ID, falls back to the committed <c>TestData</c> layouts for an airport the archive does not bundle, and decodes
/// each archive once per process. <see cref="RecordingGroundData.Evergreen"/> serves the committed layouts. Both demand a
/// reason.
/// </summary>
public class RecordingGroundDataTests
{
    private const string ArchivePath = "TestData/issue224-oak-taxi-through-lead-recording.zip";
    private const string LayoutWithoutGeoJsonPath = "TestData/c000c2b0afc8.zip";
    private const string NoLayoutArchivePath = "TestData/0ee0513aa9f0.zip";
    private const string Why = "the helper's own layout choice";

    public RecordingGroundDataTests() => TestVnasData.EnsureInitialized();

    [Fact]
    public void VersionPinned_ServesTheArchivesOwnLayout()
    {
        using var archive = RecordingArchive.Open(ArchivePath);
        int archiveNodeCount = archive.ReadLayout("oak").Nodes.Count;
        AirportGroundLayout? live = new TestAirportGroundData().GetLayout("OAK");
        Assert.NotNull(live);

        AirportGroundLayout? bundled = RecordingGroundData.VersionPinned(ArchivePath, Why).GetLayout("oak");

        Assert.NotNull(bundled);
        Assert.Equal(archiveNodeCount, bundled.Nodes.Count);
        Assert.NotSame(live, bundled);
    }

    [Fact]
    public void VersionPinned_ReportsWhichAirportsTheArchiveBundles()
    {
        var groundData = RecordingGroundData.VersionPinned(ArchivePath, Why);

        Assert.True(groundData.Bundles("oak"));
        Assert.True(groundData.Bundles("KOAK"));
        Assert.False(groundData.Bundles("SFO"));
    }

    [Fact]
    public void VersionPinned_ServesTheArchivesGeoJsonForABundledAirport()
    {
        using var archive = RecordingArchive.Open(ArchivePath);
        string? archived = archive.ReadAirportGeoJson("oak");
        Assert.NotNull(archived);

        Assert.Equal(archived, RecordingGroundData.VersionPinned(ArchivePath, Why).GetSourceGeoJson("KOAK"));
    }

    [Fact]
    public void VersionPinned_ServesNoGeoJsonForABundledLayoutWithoutOne()
    {
        var groundData = RecordingGroundData.VersionPinned(LayoutWithoutGeoJsonPath, Why);

        Assert.True(groundData.Bundles("MIA"));
        Assert.Null(groundData.GetSourceGeoJson("MIA"));
    }

    [Fact]
    public void VersionPinned_ServesTheCommittedGeoJsonForAnAirportTheArchiveDoesNotBundle()
    {
        string? committed = new TestAirportGroundData().GetSourceGeoJson("SFO");
        Assert.NotNull(committed);

        Assert.Equal(committed, RecordingGroundData.VersionPinned(ArchivePath, Why).GetSourceGeoJson("SFO"));
    }

    [Fact]
    public void VersionPinned_RejectsAMissingArchive() =>
        Assert.Throws<FileNotFoundException>(() => RecordingGroundData.VersionPinned("TestData/no-such-recording.zip", Why));

    [Fact]
    public void VersionPinned_RejectsAnArchiveWithoutLayouts()
    {
        InvalidOperationException error = Assert.Throws<InvalidOperationException>(() => RecordingGroundData.VersionPinned(NoLayoutArchivePath, Why));

        Assert.Contains("bundles no airport layout", error.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void VersionPinned_FindsTheLayoutByItsIcaoId()
    {
        var groundData = RecordingGroundData.VersionPinned(ArchivePath, Why);

        Assert.Same(groundData.GetLayout("oak"), groundData.GetLayout("KOAK"));
    }

    [Fact]
    public void VersionPinned_FallsBackToTheCommittedLayoutForAnAirportTheArchiveDoesNotBundle()
    {
        AirportGroundLayout? live = new TestAirportGroundData().GetLayout("SFO");
        Assert.NotNull(live);

        Assert.Same(live, RecordingGroundData.VersionPinned(ArchivePath, Why).GetLayout("KSFO"));
    }

    [Fact]
    public void VersionPinned_DecodesEachArchiveOnce()
    {
        AirportGroundLayout? first = RecordingGroundData.VersionPinned(ArchivePath, Why).GetLayout("oak");
        AirportGroundLayout? second = RecordingGroundData.VersionPinned(ArchivePath, Why).GetLayout("oak");

        Assert.NotNull(first);
        Assert.Same(first, second);
    }

    [Fact]
    public void Evergreen_ServesTheCommittedLayout()
    {
        AirportGroundLayout? live = new TestAirportGroundData().GetLayout("OAK");
        Assert.NotNull(live);

        Assert.Same(live, RecordingGroundData.Evergreen(Why).GetLayout("KOAK"));
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void BothChoices_RejectAMissingReason(string why)
    {
        Assert.Throws<ArgumentException>(() => RecordingGroundData.Evergreen(why));
        Assert.Throws<ArgumentException>(() => RecordingGroundData.VersionPinned(ArchivePath, why));
    }
}
