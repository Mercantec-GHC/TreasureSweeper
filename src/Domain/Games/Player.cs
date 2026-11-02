namespace Domain.Games;

public sealed record Player(
    string Id,
    int Number,
    string Name,
    string Color,
    string Symbol,
    string? DeviceId
);