namespace Yaat.Sim;

/// <summary>
/// Whether a radar target is correlated with its flight plan. Correlation is association, not a per-update code match:
/// before a track exists a target correlates only when it squawks its flight plan's assigned code (7110.65 5-2-1b2 NOTE;
/// the CRC ERAM manual's Uncorrelated Beacon / MCI is "squawking a beacon code that is not correlated to a flight
/// plan"), and once tracked, a code change keeps the association (7110.65 5-4-6e2 NOTE).
/// </summary>
public static class TrackCorrelation
{
    /// <summary>
    /// True when the aircraft has a flight plan that assigned it a beacon code and it is squawking that code: the test
    /// that correlates a target that has no track yet.
    /// </summary>
    public static bool SquawksAssignedCode(AircraftState ac) =>
        ac.FlightPlan.HasFlightPlan && (ac.Transponder.AssignedCode != 0) && (ac.Transponder.Code == ac.Transponder.AssignedCode);

    /// <summary>
    /// True when an ERAM target is correlated: it has a flight plan and is either tracked (on any code, 1200 included, or
    /// on standby) or squawking its assigned code with the transponder on and not 1200 (the non-discrete VFR code never
    /// starts a correlation, AIM 4-1-20).
    /// </summary>
    public static bool IsEramCorrelated(AircraftState ac) =>
        ac.FlightPlan.HasFlightPlan
        && ((ac.Track.Owner is not null) || (!ac.Transponder.IsStandby && !ac.Transponder.IsVfrCode && SquawksAssignedCode(ac)));
}
