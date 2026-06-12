namespace RoofControl.Core.Models;

public enum RoofState
{
    Unknown,
    Open,
    Closed,
    Opening,
    Closing,
    Error
}

public record RoofStatus(
    RoofState State,
    int PositionTicks,
    double PositionPercent,
    double PowerSupplyVoltage,
    bool CloudWatcherRelayClosed,
    bool RoofTotallyOpen,
    bool RoofTotallyClosed,
    int LastActionCode,
    string? LastActionDescription
);
