using Yaat.Sim.Data;

namespace Yaat.Sim.Simulation.Coast;

/// <summary>
/// The stateless checks behind a disconnect coast: whether the en-route radar still sees a track, and whether a
/// surface display sits at the track's destination (a drop rather than a coast). Also the ERAM coverage figures the
/// coverage-loss coast (<see cref="EramCoverageCoast"/>) shares.
/// </summary>
public static class DisconnectCoastRules
{
    /// <summary>
    /// ERAM en-route (ARSR) radar loses low targets sooner than terminal ASR (radar horizon), so the Center coverage
    /// floor sits well above the STARS terminal acquisition floor: field elevation + 1,500 ft AGL. There is no en-route
    /// coverage polygon in the vNAS config, so this is a deliberate fixed-AGL simplification.
    /// </summary>
    public const double EramCoverageFloorAglFt = 1500;

    /// <summary>
    /// Hysteresis band on the ERAM coverage floor: a visible track stays visible until it drops this far below the floor,
    /// while a coasting or absent track acquires only at the full floor, which prevents Normal/Coast flicker.
    /// </summary>
    public const double EramCoverageHysteresisFt = 200;

    /// <summary>
    /// How long an ERAM track coasts after its target drops below coverage: ~2 missed ARSR sweeps (the en-route ARSR-4
    /// rotates ~12 s), matching the reference server (vatsim-server-rs ERAM_VIEW_TIMEOUT_S = 24). One figure with
    /// <see cref="SimScenarioState.EramDisconnectCoastSeconds"/> by design: ERAM cannot tell a coverage loss from a
    /// disconnect, since either way the return simply stops.
    /// </summary>
    public const double EramCoverageCoastSeconds = SimScenarioState.EramDisconnectCoastSeconds;

    /// <summary>
    /// Stateless ERAM visibility: a QH-frozen, QT-coasted or unsupported track is always present; a vehicle or a track on the
    /// ground never is; otherwise the track shows at or above field elevation + <see cref="EramCoverageFloorAglFt"/>.
    /// </summary>
    public static bool IsVisibleOnEram(AircraftState ac, NavigationDatabase navDb)
    {
        if (ac.Ghost.IsUnsupported || ac.Eram.IsFrozen || ac.Eram.IsCoastTrack)
        {
            return true;
        }

        if (ac.Ghost.IsVehicle || ac.IsOnGround)
        {
            return false;
        }

        double fieldElev = FieldElevationResolver.Resolve(ac, navDb);
        return ac.Altitude >= fieldElev + EramCoverageFloorAglFt;
    }

    /// <summary>True when <paramref name="facilityId"/> names the (already normalized) destination airport.</summary>
    public static bool IsDestinationFacility(string normalizedDestination, string facilityId) =>
        !string.IsNullOrEmpty(normalizedDestination)
        && normalizedDestination.Equals(NavigationDatabase.NormalizeAirport(facilityId), StringComparison.OrdinalIgnoreCase);
}
