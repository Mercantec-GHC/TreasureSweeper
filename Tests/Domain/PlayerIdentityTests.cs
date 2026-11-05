using Domain.Games;

namespace Tests.Domain;

public class PlayerIdentityTests
{
    [Fact]
    public void AllEightNumbers_HaveDistinctColorsAndSymbols()
    {
        var all = Enumerable.Range(1, Game.MAX_PLAYERS).Select(PlayerIdentity.ForNumber).ToList();

        Assert.Equal(8, all.Select(i => i.Color).Distinct().Count());
        Assert.Equal(8, all.Select(i => i.Symbol).Distinct().Count());
    }
}