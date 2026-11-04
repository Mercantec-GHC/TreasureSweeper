 namespace Domain.Games;

public sealed record PlayerIdentity(int Number, string Color, string Symbol)
{
    private static readonly PlayerIdentity[] All =
    [
        new(1, "#E53935", "★"),
        new(2, "#1E88E5", "●"),
        new(3, "#43A047", "▲"),
        new(4, "#FDD835", "■"),
        new(5, "#8E24AA", "◆"),
        new(6, "#FB8C00", "♥"),
        new(7, "#00ACC1", "♣"),
        new(8, "#D81B60", "♠"),
    ];

    public static PlayerIdentity ForNumber(int number) => All[number - 1];
}