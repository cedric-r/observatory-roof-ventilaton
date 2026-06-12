namespace RoofControl.Core.Models;

public enum DecisionAction
{
    None,
    OpenToTarget,
    Close,
    Stop
}

public record DecisionResult(
    DecisionAction Action,
    double? TargetPositionPercent,
    string Reason
);
