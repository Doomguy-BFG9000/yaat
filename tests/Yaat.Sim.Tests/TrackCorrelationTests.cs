using Xunit;

namespace Yaat.Sim.Tests;

/// <summary>
/// Track correlation: before a track exists a target correlates only when it squawks its flight plan's assigned code
/// (7110.65 5-2-1b2 NOTE); once tracked, a code change keeps the association (7110.65 5-4-6e2 NOTE), and a track
/// correlates its target on standby too. The non-discrete VFR code 1200 never starts a correlation on an ERAM scope.
/// </summary>
public class TrackCorrelationTests
{
    private static readonly TrackOwner Sector44 = TrackOwner.CreateEram("ZOA_44_CTR", "ZOA", "44");

    private static AircraftState Ac(bool hasFlightPlan, uint code, uint assignedCode, bool tracked, string mode)
    {
        var ac = new AircraftState
        {
            Callsign = "UAL123",
            AircraftType = "B738",
            Transponder = new AircraftTransponder
            {
                Mode = mode,
                Code = code,
                AssignedCode = assignedCode,
            },
            FlightPlan = new AircraftFlightPlan { HasFlightPlan = hasFlightPlan },
        };
        ac.Track.Owner = tracked ? Sector44 : null;
        return ac;
    }

    [Theory]
    [InlineData(false, 4521u, 4521u, false, false)] // no flight plan
    [InlineData(true, 1200u, 1200u, false, true)] // VFR code 1200, assigned 1200: the code matches
    [InlineData(true, 4521u, 0u, false, false)] // no code assigned
    [InlineData(true, 4521u, 4521u, false, true)] // squawking the assigned code
    [InlineData(true, 4521u, 3456u, false, false)] // wrong code, untracked
    [InlineData(true, 4521u, 3456u, true, false)] // wrong code, tracked
    public void SquawksAssignedCode(bool hasFlightPlan, uint code, uint assignedCode, bool tracked, bool expected) =>
        Assert.Equal(expected, TrackCorrelation.SquawksAssignedCode(Ac(hasFlightPlan, code, assignedCode, tracked, "C")));

    [Theory]
    [InlineData(false, 4521u, 4521u, false, false)] // no flight plan
    [InlineData(false, 4521u, 3456u, true, false)] // no flight plan, tracked
    [InlineData(true, 1200u, 1200u, false, false)] // VFR code 1200 never starts a correlation, even when assigned
    [InlineData(true, 1200u, 3456u, true, true)] // tracked, then changed to 1200: the track keeps the association
    [InlineData(true, 1200u, 1200u, true, true)] // tracked on an assigned 1200: the track correlates it
    [InlineData(true, 4521u, 0u, false, false)] // no code assigned, untracked
    [InlineData(true, 4521u, 4521u, false, true)] // squawking the assigned code
    [InlineData(true, 4521u, 3456u, false, false)] // wrong code, untracked
    [InlineData(true, 4521u, 3456u, true, true)] // wrong code, tracked: the track keeps the association
    public void IsEramCorrelated(bool hasFlightPlan, uint code, uint assignedCode, bool tracked, bool expected) =>
        Assert.Equal(expected, TrackCorrelation.IsEramCorrelated(Ac(hasFlightPlan, code, assignedCode, tracked, "C")));

    [Theory]
    [InlineData(false, false)] // filed, on its assigned code, untracked: standby sends no code to correlate on
    [InlineData(true, true)] // filed and tracked: the track correlates the primary target
    public void IsEramCorrelated_OnStandby(bool tracked, bool expected) =>
        Assert.Equal(expected, TrackCorrelation.IsEramCorrelated(Ac(true, 3456u, 3456u, tracked, "Standby")));
}
