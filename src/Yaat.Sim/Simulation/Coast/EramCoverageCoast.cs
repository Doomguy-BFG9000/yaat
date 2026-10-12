using Yaat.Sim.Data;

namespace Yaat.Sim.Simulation.Coast;

/// <summary>
/// The ERAM (Center) coverage-loss coast, decoupled from STARS. Coverage floor = field elevation +
/// <see cref="DisconnectCoastRules.EramCoverageFloorAglFt"/>. When a visible track drops below the floor less
/// <see cref="DisconnectCoastRules.EramCoverageHysteresisFt"/>, the target return is lost but the flight-plan track coasts
/// (CST) for <see cref="DisconnectCoastRules.EramCoverageCoastSeconds"/> from the aircraft's last ERAM sweep, the
/// automation's own behaviour (it cannot tell coverage loss from disconnect). A climb back to the full floor before the
/// window ends reacquires the track. A QH-frozen or QT-coasted track is exempt and always shown; a vehicle or a surface
/// target is never shown. Every result lands on <see cref="AircraftEramState"/>, so each run kind computes the same coast.
/// </summary>
public static class EramCoverageCoast
{
    /// <summary>
    /// Advances the aircraft's coverage state to <paramref name="nowSimSeconds"/>, then takes its ERAM sweep when one is
    /// due: on a new <see cref="EramSweepGrid"/> sweep, or the second the track turns visible. The coast is evaluated
    /// before the sweep, so a coast starting on a sweep second anchors at the sweep CRC was already showing.
    /// </summary>
    public static void Evaluate(AircraftState ac, NavigationDatabase navDb, double nowSimSeconds)
    {
        AircraftEramState eram = ac.Eram;
        bool turnedVisible = Advance(ac, eram, navDb, nowSimSeconds);
        if (turnedVisible || IsSweepDue(eram.LastEramSweep, ac.Callsign, nowSimSeconds))
        {
            eram.LastEramSweep = EramSweepSample.FromLive(ac, nowSimSeconds);
        }
    }

    /// <summary>
    /// Whether the aircraft's coverage-hysteresis side is visible at its altitude: the band for a visible track, the full
    /// floor otherwise.
    /// </summary>
    private static bool IsInCoverage(AircraftState ac, bool wasVisible, NavigationDatabase navDb)
    {
        if (ac.Ghost.IsUnsupported)
        {
            return true;
        }

        double floor = FieldElevationResolver.Resolve(ac, navDb) + DisconnectCoastRules.EramCoverageFloorAglFt;
        return wasVisible ? (ac.Altitude > (floor - DisconnectCoastRules.EramCoverageHysteresisFt)) : (ac.Altitude >= floor);
    }

    // Returns whether the track turned visible this second (acquired, reacquired from a coast, or newly frozen).
    private static bool Advance(AircraftState ac, AircraftEramState eram, NavigationDatabase navDb, double nowSimSeconds)
    {
        bool wasVisible = eram.IsVisibleOnEramCoverage == true;

        // Frozen and QT-coasted tracks are unpaired from the target and persist until a track start or a drop.
        if (eram.IsFrozen || eram.IsCoastTrack)
        {
            Show(eram);
            return !wasVisible;
        }

        // Center radar never shows ground vehicles or surface aircraft; they drop at once, no coast.
        if (ac.Ghost.IsVehicle || (ac.IsOnGround && !ac.Ghost.IsUnsupported))
        {
            Hide(eram);
            return false;
        }

        if (IsInCoverage(ac, wasVisible, navDb))
        {
            Show(eram);
            return !wasVisible;
        }

        if (wasVisible)
        {
            StartCoast(ac, eram, nowSimSeconds);
        }
        else if (eram.IsCoverageCoasting)
        {
            ContinueCoast(eram, nowSimSeconds);
        }
        else
        {
            eram.IsVisibleOnEramCoverage = false;
        }

        return false;
    }

    private static void StartCoast(AircraftState ac, AircraftEramState eram, double nowSimSeconds)
    {
        eram.IsVisibleOnEramCoverage = false;
        eram.IsCoverageCoasting = true;
        eram.CoverageCoastStartSeconds = nowSimSeconds;

        // The sweep CRC is showing; live values only before the first sweep, or when the sweep is stamped after now.
        eram.CoverageCoastAnchor =
            ((eram.LastEramSweep is { } sweep) && (sweep.SimSeconds <= nowSimSeconds)) ? sweep : EramSweepSample.FromLive(ac, nowSimSeconds);
    }

    private static void ContinueCoast(AircraftEramState eram, double nowSimSeconds)
    {
        // A start after now means sim time went backward under the coast: drop it cleanly, as an expiry does.
        if (
            (eram.CoverageCoastStartSeconds is not { } start)
            || (nowSimSeconds < start)
            || ((nowSimSeconds - start) >= DisconnectCoastRules.EramCoverageCoastSeconds)
        )
        {
            EndCoast(eram);
        }
    }

    private static void Show(AircraftEramState eram)
    {
        eram.IsVisibleOnEramCoverage = true;
        EndCoast(eram);
    }

    private static void Hide(AircraftEramState eram)
    {
        eram.IsVisibleOnEramCoverage = false;
        EndCoast(eram);
    }

    private static void EndCoast(AircraftEramState eram)
    {
        eram.IsCoverageCoasting = false;
        eram.CoverageCoastStartSeconds = null;
        eram.CoverageCoastAnchor = null;
    }

    private static bool IsSweepDue(EramSweepSample? last, string callsign, double nowSimSeconds)
    {
        if (last is not { } sweep)
        {
            return true;
        }

        int offset = EramSweepGrid.OffsetSeconds(callsign);
        return EramSweepGrid.Index(offset, nowSimSeconds) != EramSweepGrid.Index(offset, sweep.SimSeconds);
    }
}
