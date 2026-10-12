using Yaat.Sim.Commands;

namespace Yaat.Sim.ControllerAi.Rules;

/// <summary>
/// Watchdog for the radar roles: a handoff to or from this position still pending <see cref="GraceSeconds"/> beyond the
/// room's auto-accept delay. Cab positions do not track, so the rule is a no-op for them. A handoff auto-accept withholds
/// (<see cref="TrackEngine.IsAutoAcceptWithheld"/>: below ERAM coverage or a QT coast track) is pending by design and opens
/// no anomaly however long it waits, unless its receiving position is one the rule treats as accepted by hand: a
/// CRC-attended position (directly or through consolidation, <see cref="TrackEngine.IsHandoffPeerControlledByCrc"/>) or
/// the scenario's student position, in any room. That handoff waits on a human, so it opens the anomaly past the same
/// horizon. Two kinds of handoff never open one, whoever receives them: an aircraft on the ground, which a radar
/// controller takes only once it is airborne and tagged, and a track owned from the live feed
/// (<see cref="AircraftTrack.OwnerFromLiveFeed"/>), whose handoff completes when the feed says so, not on the room's clock.
/// </summary>
public sealed class HandoffUnacceptedRule : IDecisionRule
{
    public const double GraceSeconds = 60.0;

    public string Name => "handoff-unaccepted";

    public void Evaluate(AiRuleScope scope)
    {
        if (scope.Position.Role is not (ControlRole.Approach or ControlRole.Center))
        {
            return;
        }

        var seen = new HashSet<string>(StringComparer.Ordinal);
        double horizon = scope.Tick.AutoAcceptDelaySeconds + GraceSeconds;
        foreach (AircraftState aircraft in scope.Tick.Snapshot)
        {
            AircraftTrack track = aircraft.Track;
            bool involvesMe =
                (track.Owner is { } owner && owner.MatchesPosition(scope.Position.Identity))
                || (track.HandoffPeer is { } peer && peer.MatchesPosition(scope.Position.Identity));
            if (
                !involvesMe
                || !track.OnHandoff
                || track.HandoffAccepted
                || track.HandoffInitiatedAt is not { } initiatedAt
                || track.OwnerFromLiveFeed
                || aircraft.IsOnGround
            )
            {
                continue;
            }

            if (TrackEngine.IsAutoAcceptWithheld(aircraft, scope.Tick.NavDb) && !IsPeerWorkedByHuman(track, scope.Tick))
            {
                continue;
            }

            double pending = scope.Now - initiatedAt;
            if (pending > horizon)
            {
                seen.Add(aircraft.Callsign);
                scope.Tick.Anomalies.Open(
                    AiAnomalyKind.HandoffUnaccepted,
                    scope.Position.PositionId,
                    aircraft.Callsign,
                    scope.Now,
                    $"handoff {track.Owner?.Callsign} → {track.HandoffPeer?.Callsign} pending for {pending:F0}s"
                );
            }
        }

        scope.CloseVanished(AiAnomalyKind.HandoffUnaccepted, seen);
    }

    /// <summary>Whether the rule treats the handoff's receiving position as one a human accepts by hand: CRC-attended, or the student's.</summary>
    private static bool IsPeerWorkedByHuman(AircraftTrack track, AiTickContext tick) =>
        track.HandoffPeer is { } peer
        && (
            TrackEngine.IsHandoffPeerControlledByCrc(peer, tick.Scenario, tick.Attendance, tick.ConsolidationState)
            || (tick.Scenario.StudentPosition is { } student && peer.MatchesPosition(student))
        );
}
