namespace Shared.Controller;

public sealed record ControllerActionRequest
{
    public string? Pin { get; init; }
    public string? PlayerId { get; init; }
    public string? DeviceId {get; init; }
    public string? Action { get; init; }
    public ControllerActionParams? Params { get; init; }
}

public sealed record ControllerActionParams
{
    public string? Direction { get; init; }
}