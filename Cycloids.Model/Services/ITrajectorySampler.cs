using Cycloids.Model.Parameters;
using Cycloids.Model.State;

namespace Cycloids.Model.Services;

public interface ITrajectorySampler
{
    /// <summary> Дискретизирует траекторию на интервале [tStart, tEnd], разбивая её на (steps + 1) равноудалённых точек </summary>
    IReadOnlyList<PointState> Sample(CycloidParameters parameters, double tStart, double tEnd, int steps);

    /// <summary> Пример дискретизации <paramref name="periods"/> полных оборотов, начиная с момента времени t = 0 </summary>
    IReadOnlyList<PointState> SamplePeriods(CycloidParameters parameters, double periods, int stepsPerPeriod);
}
