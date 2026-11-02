using Domain.Games;

namespace Tests.Domain;

public class GameRegistryTests
{
    [Fact]
    public void Create_WithoutPin_GenerateFourDigitPin()
    {
        var result = new GameRegistry().Create();

        Assert.True(result.Success);
        Assert.Matches(@"^\d{4}$", result.Game!.Pin);
    }

    [Fact]
    public void Create_ManyGames_PinsAreUnique()
    {
        var registry = new GameRegistry();

        var pins = Enumerable.Range(0, 200).Select(_ => registry.Create().Game!.Pin).ToList();

        Assert.Equal(pins.Count, pins.Distinct().Count());
    }

    [Fact]
    public void Create_WithRequestedPin_UsesIt()
    {
        Assert.Equal("1234", new GameRegistry().Create("1234").Game!.Pin);
    }

    [Fact]
    public void Create_RequestedPinWithSpaces_IsTrimmed()
    {
        Assert.Equal("1234", new GameRegistry().Create(" 1234 ").Game!.Pin);
    }

    [Fact]
    public void Create_DuplicatePin_FailsWithPinInUse()
    {
        var registry = new GameRegistry();
        registry.Create("1234");

        Assert.Equal(CreateGameError.PinInUse, registry.Create("1234").Error);
    }

    [Theory]
    [InlineData("123")]
    [InlineData("123456789")]
    [InlineData("12ab")]
    [InlineData("12 34")]
    public void Create_InvalidPin_FailsWithInvalidPin(string pin)
    {
        Assert.Equal(CreateGameError.InvalidPin, new GameRegistry().Create(pin).Error);
    }

    [Fact]
    public void Create_AllFourDigitPinsTaken_FailsWithNoPinAvailable()
    {
        var registry = new GameRegistry();
        for (var pin = 1000; pin <= 9999; pin++)
        {
            registry.Create(pin.ToString());
        }

        Assert.Equal(CreateGameError.NoPinAvailable, registry.Create().Error);
    }

    [Fact]
    public void Find_ReturnsSameGameInstance()
    {
        var registry = new GameRegistry();
        var game = registry.Create("1234").Game;

        Assert.Same(game, registry.Find("1234"));
        Assert.Same(game, registry.Find(" 1234 "));
    }

    [Fact]
    public void Find_UnknownPin_RetrunsNull()
    {
        Assert.Null(new GameRegistry().Find("0000"));
    }
}