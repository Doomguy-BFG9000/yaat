using Xunit;

namespace Yaat.Sim.Tests;

/// <summary>
/// Where a flight-plan remark says the arrival parks (YAAT-598): scenario remarks name it as <c>/V/ GATE F14, 28R</c> or
/// <c>/V/ PARKING SIGNATURE, 28R</c>, an area name sometimes followed by the exact stand in parentheses. The rows are the real
/// ZOA scenario strings and the near misses found beside them in the scenario corpus.
/// </summary>
public class FlightPlanParkingTests
{
    [Theory]
    [InlineData("/V/ PARKING SIGNATURE, 28R", "SIGNATURE", null)]
    [InlineData("/v/ Parking Signature (SIG4), 28R", "SIGNATURE", "SIG4")]
    [InlineData("/V/ PARKING PLOT 9, 28R", "PLOT 9", null)]
    [InlineData("/v/ Parking Plot 9 (9-5A), 28R", "PLOT 9", "9-5A")]
    [InlineData("/V/ GATE F14, 28R", null, "F14")]
    [InlineData("/v/ Gate C5, 28R", null, "C5")]
    [InlineData("/v/ Gate G7, 28L", null, "G7")]
    [InlineData("/v/ Gate F14", null, "F14")]
    [InlineData("/v/ Gate GA2", null, "GA2")]
    [InlineData("PARKING @G5", null, "G5")]
    [InlineData("FULL STOP PARKING @SIG1", null, "SIG1")]
    [InlineData("/V/ PARKING SIG5, AUTO GENERATED", null, "SIG5")]
    [InlineData("PARKING AT CUTTER", "CUTTER", null)]
    [InlineData("/V/ PARKING PLOT, 28R", "PLOT", null)]
    [InlineData("/V/ PARKING PLOT9A, 28R", "PLOT9A", null)]
    [InlineData("/V/ PARKING 41-3, 28R", null, "41-3")]
    public void Parse_NamesTheAreaAndStand(string remarks, string? area, string? stand)
    {
        ParkingRemark parsed = Assert.IsType<ParkingRemark>(FlightPlanParking.Parse(remarks));

        Assert.Equal((area, stand), (parsed.Area, parsed.Stand));
    }

    [Theory]
    [InlineData("/V/ GOLDEN GATE")]
    [InlineData("Gate UNKN, 28R")]
    [InlineData("?PPR SIGE9029")]
    [InlineData("?TCAS CARGO")]
    [InlineData("HANGAR SECURITY")]
    [InlineData("/V/ C3")]
    [InlineData("/V/ B10")]
    [InlineData("/V/ SIGNATURE RAMP")]
    [InlineData("MAUI POLICE DEPARTMENT PATROL COASTLINE. TAKEOFF FROM RAMP /V/")]
    [InlineData("/V/")]
    [InlineData("")]
    [InlineData(null)]
    public void Parse_NamesNothing_ForRemarksThatNameNoParking(string? remarks) => Assert.Null(FlightPlanParking.Parse(remarks));
}
