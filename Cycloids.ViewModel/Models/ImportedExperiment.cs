using Cycloids.Model.State;

namespace Cycloids.ViewModel.Models;

/// <summary>Параметры, сохранённые в экспортированном JSON (в тех же единицах, что и при экспорте).</summary>
public sealed record ImportedParameters(
    double LinearVelocity,
    double Radius,
    double PointOffset,
    double Omega,
    double InitialPhase,
    double Step,
    double SimulationTime);

/// <summary>
/// Траектория, открытая из ранее сохранённого JSON. В файле нет ускорений, поэтому Ax и Ay равны нулю.
/// </summary>
public sealed record ImportedExperiment(
    string FileName,
    ImportedParameters Parameters,
    IReadOnlyList<PointState> Points);
