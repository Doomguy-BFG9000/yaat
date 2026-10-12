using System.Text;
using Xunit;
using Yaat.Sim.Simulation.Coast;

namespace Yaat.Sim.Tests.Simulation.Coast;

/// <summary>
/// <see cref="EramSweepGrid.OffsetSeconds"/> hashes the callsign's characters without allocating; for the ASCII callsigns
/// real traffic flies it must give the phase the UTF-8 FNV-1a always gave, or every recording's sweep grid moves.
/// </summary>
public class EramSweepGridTests
{
    [Theory]
    [InlineData("SWP1")]
    [InlineData("SWA5456")]
    [InlineData("AAL1")]
    [InlineData("UAL123")]
    [InlineData("N172SP")]
    [InlineData("DAL2214")]
    [InlineData("SKW5432")]
    [InlineData("RCH871")]
    public void OffsetSeconds_MatchesTheUtf8Fnv1a_ForAsciiCallsigns(string callsign) =>
        Assert.Equal(Utf8Fnv1aOffset(callsign), EramSweepGrid.OffsetSeconds(callsign));

    // The phase as it was first defined: FNV-1a 32-bit over the callsign's UTF-8 bytes, mod the sweep period.
    private static int Utf8Fnv1aOffset(string callsign)
    {
        uint hash = 2166136261;
        foreach (byte b in Encoding.UTF8.GetBytes(callsign))
        {
            hash = unchecked((hash ^ b) * 16777619);
        }

        return (int)(hash % EramSweepGrid.SweepSeconds);
    }
}
