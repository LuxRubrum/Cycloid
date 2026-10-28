using Cycloids.Model.Parameters;
using Cycloids.Model.State;

namespace Cycloids.ViewModel.Models;

/// <summary>Всё, что нужно для экспорта результатов эксперимента и повторного воспроизведения.</summary>
public sealed record ExperimentSnapshot(
    CycloidParameters Parameters,
    double LinearVelocity,
    double Step,
    double SimulationTime,
    IReadOnlyList<PointState> Points,
    IReadOnlyList<PointReport> Markers);
