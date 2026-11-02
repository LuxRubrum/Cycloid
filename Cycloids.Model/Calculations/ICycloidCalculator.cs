using Cycloids.Model.Parameters;
using Cycloids.Model.State;

namespace Cycloids.Model.Calculations;

/// <summary> Контракт между моделью и ViewModel: кинематика в произвольный момент времени </summary>
public interface ICycloidCalculator
{
    CycloidParameters Parameters { get; }

    PointState GetPointState(double t);

    WheelState GetWheelState(double t);
}
