using System.Globalization;
using System.Text;
using Cycloids.ViewModel.Models;

namespace Cycloids.ViewModel.Services;

/// <summary>
/// Экспорт результатов в CSV и JSON вместе с метаданными (параметры, шаг, версия модели).
/// Числа записываются в инвариантной культуре с точкой и без потери точности ("R").
/// </summary>
public static class ExperimentExporter
{
    public const string ExperimentName = "RollingWheelCycloid";
    public const string ModelVersion = "1.0.0";
    public const int SchemaVersion = 1;

    public static string ToCsv(ExperimentSnapshot s)
    {
        ArgumentNullException.ThrowIfNull(s);
        var p = s.Parameters;
        var sb = new StringBuilder();

        sb.Append("# experiment: ").AppendLine(ExperimentName);
        sb.Append("# modelVersion: ").AppendLine(ModelVersion);
        sb.AppendLine("# solver: analytic (closed form), Cycloids.Model");
        sb.Append("# cycloidType: ").AppendLine(p.Kind.ToString());
        sb.Append("# vLinWheel_m_s: ").AppendLine(Num(s.LinearVelocity));
        sb.Append("# rWheel_m: ").AppendLine(Num(p.Radius));
        sb.Append("# rDot_m: ").AppendLine(Num(p.PointOffset));
        sb.Append("# omega_rad_s: ").AppendLine(Num(p.AngularVelocity));
        sb.Append("# initialPhase_rad: ").AppendLine(Num(p.InitialPhase));
        sb.Append("# dt_s: ").AppendLine(Num(s.Step));
        sb.Append("# simulationTime_s: ").AppendLine(Num(s.SimulationTime));
        sb.AppendLine("t_s,x_m,y_m,vx_m_s,vy_m_s,speed_m_s");

        foreach (var pt in s.Points)
        {
            sb.Append(Num(pt.Time)).Append(',')
              .Append(Num(pt.X)).Append(',')
              .Append(Num(pt.Y)).Append(',')
              .Append(Num(pt.Vx)).Append(',')
              .Append(Num(pt.Vy)).Append(',')
              .AppendLine(Num(pt.Speed));
        }

        return sb.ToString();
    }

    public static string ToJson(ExperimentSnapshot s)
    {
        ArgumentNullException.ThrowIfNull(s);
        var p = s.Parameters;
        var sb = new StringBuilder();

        sb.AppendLine("{");
        sb.AppendLine($"  \"schemaVersion\": {SchemaVersion},");
        sb.AppendLine($"  \"experiment\": \"{ExperimentName}\",");
        sb.AppendLine($"  \"modelVersion\": \"{ModelVersion}\",");
        sb.AppendLine("  \"solver\": \"analytic (closed form), Cycloids.Model\",");
        sb.AppendLine($"  \"cycloidType\": \"{p.Kind}\",");
        sb.AppendLine("  \"units\": { \"time\": \"s\", \"length\": \"m\", \"velocity\": \"m/s\", \"angle\": \"rad\" },");
        sb.AppendLine("  \"parameters\": {");
        sb.AppendLine($"    \"vLinWheel\": {Num(s.LinearVelocity)},");
        sb.AppendLine($"    \"rWheel\": {Num(p.Radius)},");
        sb.AppendLine($"    \"rDot\": {Num(p.PointOffset)},");
        sb.AppendLine($"    \"omega\": {Num(p.AngularVelocity)},");
        sb.AppendLine($"    \"initialPhase\": {Num(p.InitialPhase)},");
        sb.AppendLine($"    \"dt\": {Num(s.Step)},");
        sb.AppendLine($"    \"simulationTime\": {Num(s.SimulationTime)}");
        sb.AppendLine("  },");

        sb.AppendLine("  \"markers\": [");
        for (int i = 0; i < s.Markers.Count; i++)
        {
            var m = s.Markers[i];
            sb.Append("    { ")
              .Append($"\"t\": {Num(m.Time)}, \"x\": {Num(m.Point.X)}, \"y\": {Num(m.Point.Y)}, ")
              .Append($"\"vx\": {Num(m.Point.Vx)}, \"vy\": {Num(m.Point.Vy)}, \"speed\": {Num(m.Point.Speed)}, ")
              .Append($"\"contactX\": {Num(m.ContactX)}")
              .Append(" }")
              .AppendLine(i < s.Markers.Count - 1 ? "," : string.Empty);
        }
        sb.AppendLine("  ],");

        sb.AppendLine("  \"columns\": [\"t\", \"x\", \"y\", \"vx\", \"vy\", \"speed\"],");
        sb.AppendLine("  \"points\": [");
        for (int i = 0; i < s.Points.Count; i++)
        {
            var pt = s.Points[i];
            sb.Append("    [")
              .Append(Num(pt.Time)).Append(", ")
              .Append(Num(pt.X)).Append(", ")
              .Append(Num(pt.Y)).Append(", ")
              .Append(Num(pt.Vx)).Append(", ")
              .Append(Num(pt.Vy)).Append(", ")
              .Append(Num(pt.Speed))
              .AppendLine(i < s.Points.Count - 1 ? "]," : "]");
        }
        sb.AppendLine("  ]");
        sb.AppendLine("}");

        return sb.ToString();
    }

    private static string Num(double value) => value.ToString("R", CultureInfo.InvariantCulture);
}
