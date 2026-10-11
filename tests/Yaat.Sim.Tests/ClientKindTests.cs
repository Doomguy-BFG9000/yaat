using Xunit;

namespace Yaat.Sim.Tests;

/// <summary>
/// Tower Cab 3D is listed in Room Members as its own kind and named in the terminal's join and leave lines, so its kind
/// carries a wire value, a standalone label and a sentence suffix like the other apps.
/// </summary>
public class ClientKindTests
{
    [Fact]
    public void TowerCab_WireValue_IsTowercab() => Assert.Equal("towercab", ClientKind.TowerCab);

    [Fact]
    public void DisplayName_TowerCab_IsTowerCab3D() => Assert.Equal("Tower Cab 3D", ClientKind.DisplayName(ClientKind.TowerCab));

    [Fact]
    public void DisplaySuffix_TowerCab_NamesTowerCab3D() => Assert.Equal(" (Tower Cab 3D)", ClientKind.DisplaySuffix(ClientKind.TowerCab));

    [Fact]
    public void DisplaySuffix_Main_StaysEmpty() => Assert.Equal("", ClientKind.DisplaySuffix(ClientKind.Main));
}
