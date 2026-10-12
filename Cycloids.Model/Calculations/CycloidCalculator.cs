using Cycloids.Model.Parameters;
using Cycloids.Model.State;

namespace Cycloids.Model.Calculations;

/// <summary>
/// Кинематика точки на колесе без трения и скольжения
/// При α = φ0 + ω·t:
/// x = R·ω·t − d·sin α y = R − d·cos α
/// vx = ω(R − d·cos α) vy = ω·d·sin α
/// ax = ω²·d·sin α ay = ω²·d·cos α
/// </summary>
public sealed class CycloidCalculator : ICycloidCalculator
{
    public CycloidParameters Parameters { get; }

    public CycloidCalculator(CycloidParameters parameters)
    {
        ArgumentNullException.ThrowIfNull(parameters);
        parameters.EnsureValid();
        Parameters = parameters;
    }

    public PointState GetPointState(double t)
    {
        var (r, w, d, phi) = (Parameters.Radius, Parameters.AngularVelocity, Parameters.PointOffset, Parameters.InitialPhase);

        var (sin, cos) = Math.SinCos(phi + w * t);

        return new PointState(
            Time: t,
            X: r * w * t - d * sin,
            Y: r - d * cos,
            Vx: w * (r - d * cos),
            Vy: w * d * sin,
            Ax: w * w * d * sin,
            Ay: w * w * d * cos);
    }

    public WheelState GetWheelState(double t)
    {
        var (r, w, phi) = (Parameters.Radius, Parameters.AngularVelocity, Parameters.InitialPhase);
        return new WheelState(
            Time: t,
            CenterX: r * w * t,
            CenterY: r,
            RotationAngle: w * t,
            PointAngle: phi + w * t);
    }
}
