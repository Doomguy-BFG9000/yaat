namespace Yaat.Sim.Data.Airport;

/// <summary>
/// The aircraft a taxi start is picked for, and the other aircraft on the ground the pick must not start it through
/// (<see cref="AirportGroundLayout.FindTaxiStartNode"/>): an occupied stand's parking node is never the start, and neither is
/// a node whose free-space leg passes a parked aircraft.
/// </summary>
public sealed record TaxiStartTraffic
{
    /// <summary>The taxiing aircraft's callsign; tells it apart from itself in <see cref="OtherGroundAircraft"/>.</summary>
    public required string Callsign { get; init; }

    /// <summary>The taxiing aircraft's ICAO type designator; sets the half-span its free-space leg keeps clear.</summary>
    public required string AircraftType { get; init; }

    /// <summary>Every aircraft on the ground the caller can see; empty when it sees none.</summary>
    public required IReadOnlyList<NeighbourCandidate> OtherGroundAircraft { get; init; }

    /// <summary>
    /// The other aircraft parked or held (<see cref="GroundConflictDetector.IsParkedOrHeld(bool, string?, double, double?)"/>),
    /// on a stand or off one: each is an obstacle to a free-space start leg where it stands. Whether one still occupies the
    /// stand it names is the layout's call (<see cref="AirportGroundLayout.FindTaxiStartNode"/>): only while it stands at
    /// that stand's parking node.
    /// </summary>
    /// <returns>The parked or held aircraft, in the order the caller listed them.</returns>
    internal IEnumerable<NeighbourCandidate> ParkedOrHeldOthers() =>
        OtherGroundAircraft.Where(other =>
            !string.Equals(other.Callsign, Callsign, StringComparison.OrdinalIgnoreCase)
            && GroundConflictDetector.IsParkedOrHeld(other.IsImmobile, other.PhaseName, other.GroundSpeedKts, other.TargetSpeedKts)
        );

    /// <summary>The traffic a simulated aircraft's taxi start is picked against.</summary>
    /// <param name="aircraft">The taxiing aircraft.</param>
    /// <param name="otherGroundAircraft">Every aircraft on the ground the caller can see.</param>
    /// <returns>The traffic.</returns>
    public static TaxiStartTraffic Of(AircraftState aircraft, IReadOnlyList<NeighbourCandidate> otherGroundAircraft) =>
        new()
        {
            Callsign = aircraft.Callsign,
            AircraftType = aircraft.AircraftType,
            OtherGroundAircraft = otherGroundAircraft,
        };
}
