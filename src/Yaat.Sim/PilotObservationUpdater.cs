using System.Runtime.CompilerServices;
using Microsoft.Extensions.Logging;

namespace Yaat.Sim;

/// <summary>
/// Per-tick evaluator for <see cref="PilotObservation"/>s. Called from the
/// simulation tick alongside <c>FlightPhysics.UpdateCommandQueue</c>; each
/// observation re-checks its condition and either resolves (pilot reports the
/// outcome via <see cref="AircraftState.PendingWarnings"/> for attention-
/// grabbing events like traffic acquisition) or stays pending. Observations
/// referencing aircraft that have left the sim are silently dropped.
/// </summary>
public static class PilotObservationUpdater
{
    private static readonly ILogger Log = SimLog.CreateLogger("PilotObservationUpdater");

    /// <summary>
    /// The acquisition gates already logged for a given pending observation, so a rejection that holds tick after
    /// tick is reported once rather than on every physics sub-tick. Keyed by reference: a fresh RTIS replaces the
    /// observation and starts its own log, and an observation dropped by the run releases its entry.
    /// </summary>
    private static readonly ConditionalWeakTable<TrafficAcquisitionObservation, HashSet<VisualAcquisitionFailure>> LoggedRejections = [];

    public static void Update(
        AircraftState aircraft,
        Func<string, AircraftState?>? aircraftLookup,
        WeatherProfile? weather,
        bool soloTrainingMode,
        bool rpoShowPilotSpeech
    )
    {
        if (aircraft.PendingObservations.Count == 0)
        {
            return;
        }

        List<PilotObservation> observations = aircraft.PendingObservations;
        for (int i = observations.Count - 1; i >= 0; i--)
        {
            PilotObservation obs = observations[i];
            bool resolved = obs switch
            {
                TrafficAcquisitionObservation traffic => TryResolveTraffic(
                    aircraft,
                    traffic,
                    aircraftLookup,
                    weather,
                    soloTrainingMode,
                    rpoShowPilotSpeech
                ),
                FieldAcquisitionObservation => TryResolveField(aircraft, weather, soloTrainingMode, rpoShowPilotSpeech),
                _ => false,
            };

            if (resolved)
            {
                observations.RemoveAt(i);
            }
        }
    }

    /// <summary>
    /// Re-runs visual acquisition for a pending traffic observation. Returns
    /// true if the observation is done (acquired → pilot reports in sight, or
    /// target aircraft gone → silently drop) and should be removed.
    /// </summary>
    private static bool TryResolveTraffic(
        AircraftState aircraft,
        TrafficAcquisitionObservation obs,
        Func<string, AircraftState?>? aircraftLookup,
        WeatherProfile? weather,
        bool soloTrainingMode,
        bool rpoShowPilotSpeech
    )
    {
        if (aircraftLookup is null)
        {
            return false;
        }

        AircraftState? target = aircraftLookup(obs.TargetCallsign);
        if (target is null)
        {
            // Target left the simulation. Silently drop the observation.
            return true;
        }

        VisualAcquisitionResult result = VisualAcquisition.TryAcquireTraffic(aircraft, target, weather);
        if (!result.Acquired)
        {
            LogRejectionOnce(aircraft, obs, result.Reason);
            return false;
        }

        aircraft.Approach.HasReportedTrafficInSight = true;
        aircraft.Approach.LastReportedTrafficCallsign = obs.TargetCallsign.ToUpperInvariant();
        Log.LogDebug(
            "Traffic observation acquired: {Follower} has {Target} in sight at {Distance:F1} nm",
            aircraft.Callsign,
            obs.TargetCallsign,
            result.DistanceNm
        );
        Pilot.PilotResponder.RouteRpoSayReadback(
            aircraft,
            soloTrainingMode,
            rpoShowPilotSpeech,
            Pilot.PilotResponder.BuildTrafficInSight(aircraft, obs.TargetCallsign)
        );
        Log.LogDebug("Traffic in sight readback queued for {Follower}, reporting {Target}", aircraft.Callsign, obs.TargetCallsign);
        return true;
    }

    /// <summary>
    /// Logs the gate holding an acquisition back the first time it rejects a given observation, so a persistent
    /// rejection (a target behind the ownship, out of range, or under a deck) is visible in the log without
    /// drowning it: the gate is named once per observation, not once per tick.
    /// </summary>
    private static void LogRejectionOnce(AircraftState aircraft, TrafficAcquisitionObservation obs, VisualAcquisitionFailure gate)
    {
        if (!Log.IsEnabled(LogLevel.Debug))
        {
            return;
        }

        if (LoggedRejections.GetOrCreateValue(obs).Add(gate))
        {
            Log.LogDebug("Traffic observation: {Follower} cannot yet acquire {Target} — {Gate}", aircraft.Callsign, obs.TargetCallsign, gate);
        }
    }

    /// <summary>
    /// Re-runs visual acquisition for a pending field observation. Returns
    /// true if the observation is done (acquired → pilot reports field in
    /// sight, or the destination is no longer lookupable → silently drop)
    /// and should be removed.
    /// </summary>
    private static bool TryResolveField(AircraftState aircraft, WeatherProfile? weather, bool soloTrainingMode, bool rpoShowPilotSpeech)
    {
        VisualAcquisitionResult? result = VisualAcquisition.TryAcquireAirport(aircraft, weather);
        if (result is null)
        {
            // Destination cleared or no longer in nav database. Silently drop.
            return true;
        }

        if (!result.Value.Acquired)
        {
            return false;
        }

        aircraft.Approach.HasReportedFieldInSight = true;
        Pilot.PilotResponder.RouteRpoSayReadback(aircraft, soloTrainingMode, rpoShowPilotSpeech, Pilot.PilotResponder.BuildFieldInSight(aircraft));
        return true;
    }
}
