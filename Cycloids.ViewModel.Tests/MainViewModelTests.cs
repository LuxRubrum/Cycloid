using System.Text.Json;
using Cycloids.ViewModel.Infrastructure;

namespace Cycloids.ViewModel.Tests;

internal sealed class FakeTimer : IFrameTimer
{
    public event EventHandler? Tick;

    public bool IsRunning { get; private set; }

    public void Start() => IsRunning = true;

    public void Stop() => IsRunning = false;

    public void Raise() => Tick?.Invoke(this, EventArgs.Empty);
}

public class MainViewModelTests
{
    private static MainViewModel Create() => new(new FakeTimer());

    [Fact]
    public void Defaults_MatchVariant7_AndHaveNoError()
    {
        var vm = Create();

        Assert.False(vm.HasError);
        Assert.Equal(10, vm.Omega, 12);                 // ω = v / R = 10 / 1
        Assert.Equal(1, vm.Parameters.Radius, 12);
        Assert.Equal(0.5, vm.Parameters.PointOffset, 12);
        Assert.Equal(2, vm.Markers.Count);
        Assert.Equal(0.4, vm.Markers[0].Time, 12);
        Assert.Equal(0.8, vm.Markers[1].Time, 12);
    }

    [Fact]
    public void StartPoint_IsAtBottom_AtTimeZero()
    {
        var vm = Create();
        var p = vm.Trajectory[0];

        Assert.Equal(0, p.Time, 12);
        Assert.Equal(0, p.X, 9);
        Assert.Equal(0.5, p.Y, 9); // R − d·cos 0 = 1 − 0.5
    }

    [Theory]
    [InlineData(0.4)]
    [InlineData(0.8)]
    public void Marker_Position_MatchesClosedFormula(double t)
    {
        var vm = Create();
        var m = vm.Markers.Single(x => Math.Abs(x.Time - t) < 1e-12);

        const double v = 10, r = 1, d = 0.5, w = v / r;
        Assert.Equal(v * t - d * Math.Sin(w * t), m.Point.X, 9);
        Assert.Equal(r - d * Math.Cos(w * t), m.Point.Y, 9);
    }

    [Fact]
    public void Markers_InstantCenterOfVelocity_IsContactPoint()
    {
        var vm = Create();

        foreach (var m in vm.Markers)
        {
            Assert.True(m.IcsRelativeError < 1e-9);              // |v| = |ω|·|CP|
            Assert.Equal(90.0, m.AngleToRadiusDegrees, 6);       // v ⟂ CP
            Assert.Equal(10 * m.Time, m.ContactX, 9);            // касание в x = v·t
        }

        Assert.True(vm.VerificationOk);
    }

    [Fact]
    public void Markers_NumericSpeed_ConvergesWithSecondOrder()
    {
        var vm = Create();
        var m = vm.Markers[0];

        Assert.True(m.NumericHalfStepError < m.NumericError);
        Assert.InRange(m.ConvergenceRatio, 3.5, 4.5);
    }

    [Fact]
    public void ShortSimulation_HidesMarkersBeyondT()
    {
        var vm = Create();
        vm.SimulationTimeText = "0.5";

        Assert.False(vm.HasError);
        Assert.Single(vm.Markers);
        Assert.Equal(0.4, vm.Markers[0].Time, 12);
    }

    [Fact]
    public void Comma_IsAcceptedAsDecimalSeparator()
    {
        var vm = Create();
        vm.DtText = "0,01";

        Assert.False(vm.HasError);
    }

    [Theory]
    [InlineData("VLinWheelText", "0")]
    [InlineData("VLinWheelText", "abc")]
    [InlineData("VLinWheelText", "")]
    [InlineData("WheelRadiusText", "0")]
    [InlineData("WheelRadiusText", "-1")]
    [InlineData("PointOffsetText", "-0.1")]
    [InlineData("DtText", "0")]
    [InlineData("DtText", "-0.01")]
    [InlineData("SimulationTimeText", "0")]
    [InlineData("DtText", "1e-9")]
    public void InvalidInput_ShowsError_AndKeepsLastPicture(string property, string value)
    {
        var vm = Create();
        int pointsBefore = vm.Trajectory.Count;

        typeof(MainViewModel).GetProperty(property)!.SetValue(vm, value);

        Assert.True(vm.HasError);
        Assert.False(string.IsNullOrWhiteSpace(vm.ErrorMessage));
        Assert.False(vm.CanExport);
        Assert.Equal(pointsBefore, vm.Trajectory.Count);

        vm.VLinWheelText = "10";
        typeof(MainViewModel).GetProperty(property)!.SetValue(vm, property switch
        {
            "WheelRadiusText" => "1",
            "PointOffsetText" => "0.5",
            "DtText" => "0.005",
            "SimulationTimeText" => "1.2",
            _ => "10",
        });
        Assert.False(vm.HasError);
    }

    [Fact]
    public void NegativeVelocity_RollsLeft()
    {
        var vm = Create();
        vm.VLinWheelText = "-10";

        Assert.False(vm.HasError);
        Assert.Equal(-10, vm.Omega, 12);
        Assert.True(vm.Trajectory[^1].X < 0);
    }

    [Fact]
    public void ProlateAndCurtate_AreRecognised()
    {
        var vm = Create();

        vm.PointOffsetText = "1";
        Assert.Equal(Cycloids.Model.Enums.CycloidType.Common, vm.Kind);

        vm.PointOffsetText = "1.5";
        Assert.Equal(Cycloids.Model.Enums.CycloidType.Prolate, vm.Kind);
        Assert.True(vm.Bounds.YMin <= 0);

        vm.PointOffsetText = "0.5";
        Assert.Equal(Cycloids.Model.Enums.CycloidType.Curtate, vm.Kind);
    }

    [Fact]
    public void PlayPause_StartsAndStopsTimer()
    {
        var timer = new FakeTimer();
        var vm = new MainViewModel(timer);

        vm.PlayPauseCommand.Execute(null);
        Assert.True(vm.IsPlaying);
        Assert.True(timer.IsRunning);

        vm.PlayPauseCommand.Execute(null);
        Assert.False(vm.IsPlaying);
        Assert.False(timer.IsRunning);
    }

    [Fact]
    public void Error_StopsAnimation_AndDisablesPlay()
    {
        var timer = new FakeTimer();
        var vm = new MainViewModel(timer);
        vm.PlayPauseCommand.Execute(null);

        vm.VLinWheelText = "x";

        Assert.False(vm.IsPlaying);
        Assert.False(timer.IsRunning);
        Assert.False(vm.PlayPauseCommand.CanExecute(null));
    }

    [Fact]
    public void Reset_SetsTimeToZero()
    {
        var vm = Create();
        vm.CurrentTime = 0.7;

        vm.ResetCommand.Execute(null);

        Assert.Equal(0.0, vm.CurrentTime);
        Assert.Equal(0, vm.CurrentPoint.Time, 12);
    }

    [Fact]
    public void Csv_ContainsMetadata_AndOneRowPerPoint()
    {
        var vm = Create();
        var lines = vm.BuildCsv().Split('\n', StringSplitOptions.RemoveEmptyEntries)
                      .Select(l => l.TrimEnd('\r')).ToList();

        Assert.Contains(lines, l => l.StartsWith("# omega_rad_s: 10"));
        Assert.Contains(lines, l => l == "t_s,x_m,y_m,vx_m_s,vy_m_s,speed_m_s");

        var data = lines.Where(l => !l.StartsWith('#')).Skip(1).ToList();
        Assert.Equal(vm.Trajectory.Count, data.Count);

        var last = data[^1].Split(',');
        Assert.Equal(vm.Trajectory[^1].X, double.Parse(last[1], System.Globalization.CultureInfo.InvariantCulture), 9);
    }

    [Fact]
    public void Json_IsValid_AndRoundTripsKeyValues()
    {
        var vm = Create();
        using var doc = JsonDocument.Parse(vm.BuildJson());
        var root = doc.RootElement;

        Assert.Equal("RollingWheelCycloid", root.GetProperty("experiment").GetString());
        Assert.Equal(10, root.GetProperty("parameters").GetProperty("omega").GetDouble(), 12);
        Assert.Equal(2, root.GetProperty("markers").GetArrayLength());

        var points = root.GetProperty("points");
        Assert.Equal(vm.Trajectory.Count, points.GetArrayLength());
        Assert.Equal(vm.Trajectory[^1].Y, points[points.GetArrayLength() - 1][2].GetDouble(), 12);
    }

    [Fact]
    public void Export_IsRefused_WhenInputIsInvalid()
    {
        var vm = Create();
        vm.DtText = "0";

        Assert.Throws<InvalidOperationException>(() => vm.BuildCsv());
    }
    [Fact]
    public void LargeStep_DoesNotProduceFalseVerificationFailure()
    {
        var vm = Create();
        vm.DtText = "0.2";

        Assert.False(vm.HasError);
        Assert.True(vm.VerificationOk);
    }
}
