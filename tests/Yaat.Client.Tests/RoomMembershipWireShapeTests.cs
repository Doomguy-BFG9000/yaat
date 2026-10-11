using System.Reflection;
using Xunit;
using Yaat.Client.Services;

namespace Yaat.Client.Tests;

/// <summary>
/// Pins the wire shape of the room-membership DTOs, which yaat and yaat-server declare separately.
/// </summary>
/// <remarks>
/// The two repos never meet at compile time, so a field renamed on one side and not the other
/// costs nothing at build and fails silently at runtime: the JSON simply doesn't bind and the
/// property keeps its default. That is how <c>MemberInitials</c> → <c>Members</c> could have
/// shipped a client that reported every room as empty.
/// <para>
/// yaat-server holds an identical copy of this file. Changing either DTO fails both, and the fix
/// is to update the signature <b>and</b> the sibling repo — not to edit one expectation until it
/// passes. Deliberately scoped to the membership DTOs rather than all ~40 training records; it
/// guards what this pair of repos has actually broken, and extends by adding a case.
/// </para>
/// </remarks>
public class RoomMembershipWireShapeTests
{
    private const string RoomMemberShape = "Cid:String, Initials:String, ArtccId:String, Kind:String, JoinedAtUtc:DateTime, ConnectionId:String";

    private const string RoomInfoShape =
        "RoomId:String, CreatorInitials:String, CreatorArtccId:String, ArtccId:String, ScenarioName:String, Members:List<RoomMemberDto>, "
        + "IsPaused:Boolean, SimRate:Double, ElapsedSeconds:Double, AircraftCount:Int32";

    // RoomStateDto's two copies differ beyond these (the aircraft DTO types, the server-only LoadingBy parameter, the
    // client's optional tail), so only the fields that name the room and its members are pinned, looked up by name.
    private const string RoomStateRoomShape =
        "RoomId:String, CreatorInitials:String, CreatorArtccId:String, ArtccId:String, Members:List<RoomMemberDto>";

    private static readonly string[] RoomStateRoomFields = ["RoomId", "CreatorInitials", "CreatorArtccId", "ArtccId", "Members"];

    [Fact]
    public void RoomMemberDto_MatchesTheAgreedWireShape() => Assert.Equal(RoomMemberShape, DescribePrimaryConstructor(typeof(RoomMemberDto)));

    [Fact]
    public void TrainingRoomInfoDto_MatchesTheAgreedWireShape() =>
        Assert.Equal(RoomInfoShape, DescribePrimaryConstructor(typeof(TrainingRoomInfoDto)));

    [Fact]
    public void RoomStateDto_RoomFields_MatchTheAgreedWireShape() =>
        Assert.Equal(RoomStateRoomShape, DescribeConstructorParameters(typeof(RoomStateDto), RoomStateRoomFields));

    // Constructor parameters, not properties: the client copy adds display-only members
    // (KindLabel, JoinedAtText) that the server has no business carrying, and those must not
    // register as drift.
    private static string DescribePrimaryConstructor(Type type) =>
        string.Join(", ", PrimaryConstructor(type).GetParameters().Select(p => $"{p.Name}:{Describe(p.ParameterType)}"));

    /// <summary>The named constructor parameters, in the order given; a name the constructor lacks reads as missing.</summary>
    private static string DescribeConstructorParameters(Type type, string[] names)
    {
        ParameterInfo[] parameters = PrimaryConstructor(type).GetParameters();
        return string.Join(
            ", ",
            names.Select(name => parameters.SingleOrDefault(p => p.Name == name) is { } p ? $"{name}:{Describe(p.ParameterType)}" : $"{name}:missing")
        );
    }

    private static ConstructorInfo PrimaryConstructor(Type type) =>
        type.GetConstructors(BindingFlags.Public | BindingFlags.Instance).OrderByDescending(c => c.GetParameters().Length).First();

    private static string Describe(Type type)
    {
        Type? underlying = Nullable.GetUnderlyingType(type);
        if (underlying is not null)
        {
            return Describe(underlying);
        }

        if (!type.IsGenericType)
        {
            return type.Name;
        }

        string name = type.Name[..type.Name.IndexOf('`', StringComparison.Ordinal)];
        return $"{name}<{string.Join(",", type.GetGenericArguments().Select(Describe))}>";
    }
}
