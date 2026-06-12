using RoofControl.Core.Models;

namespace RoofControl.Core.Interfaces;

public interface IDecisionEngine
{
    DecisionResult Decide(WeatherData weather, RoofStatus roof, DateTime now);
}
