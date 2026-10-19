using Cycloids.ViewModel.Infrastructure;
using Cycloids.ViewModel.Services;

namespace Cycloids.ViewModel.Tests;

public class ImportTests
{
    private static MainViewModel Create() => new(new FakeTimer());

    [Fact]
    public void Parse_RoundTripsOwnExport()
    {
        var vm = Create();

        var data = ExperimentImporter.Parse(vm.BuildJson());

        Assert.Equal(vm.Trajectory.Count, data.Points.Count);
        Assert.Equal(10, data.Parameters.Omega, 12);
        Assert.Equal(vm.Trajectory[^1].X, data.Points[^1].X, 12);
        Assert.Equal(vm.Trajectory[^1].Vy, data.Points[^1].Vy, 12);
    }

    [Theory]
    [InlineData("")]
    [InlineData("not json")]
    [InlineData("[]")]
    [InlineData("{\"experiment\":\"Other\"}")]
    [InlineData("{\"experiment\":\"RollingWheelCycloid\",\"schemaVersion\":99}")]
    public void Parse_RejectsBadFiles(string json)
        => Assert.Throws<FormatException>(() => ExperimentImporter.Parse(json));

    [Fact]
    public void Parse_RejectsPointWithWrongLength()
    {
        string json = Create().BuildJson().Replace("\"points\": [", "\"points\": [\n    [0, 1],");

        var ex = Assert.Throws<FormatException>(() => ExperimentImporter.Parse(json));
        Assert.Contains("Точка №1", ex.Message);
    }

    [Fact]
    public void ImportJson_OwnExport_ReproducesWithinTolerance()
    {
        var vm = Create();

        vm.ImportJson(vm.BuildJson(), "result.json");

        Assert.True(vm.HasImport);
        Assert.Equal("10", vm.VLinWheelText);
        Assert.Equal("1", vm.WheelRadiusText);
        Assert.Equal("0.5", vm.PointOffsetText);
        Assert.Equal("1.2", vm.SimulationTimeText);
        Assert.Contains(vm.ImportLines, l => l.Contains("воспроизводятся"));
        Assert.Contains(vm.ImportLines, l => l.Contains("Параметры совпадают"));
    }

    [Fact]
    public void ImportCsv_UsesImportedPointsForAnimation()
    {
        var vm = Create();
        const string csv = """
            # experiment: RollingWheelCycloid
            # vLinWheel_m_s: 4
            # rWheel_m: 2
            # rDot_m: 0.75
            # omega_rad_s: 2
            # initialPhase_rad: 0
            # dt_s: 1
            # simulationTime_s: 1
            t_s,x_m,y_m,vx_m_s,vy_m_s,speed_m_s
            0,10,20,1,2,3
            1,30,40,5,6,7
            """;

        vm.ImportCsv(csv, "custom.csv");
        vm.CurrentTime = 0.5;

        Assert.Equal("4", vm.VLinWheelText);
        Assert.Equal("2", vm.WheelRadiusText);
        Assert.Equal("0.75", vm.PointOffsetText);
        Assert.Equal(20, vm.CurrentPoint.X, 12);
        Assert.Equal(30, vm.CurrentPoint.Y, 12);
        Assert.Equal(3, vm.CurrentPoint.Vx, 12);
        Assert.Equal(4, vm.CurrentPoint.Vy, 12);
    }

    [Fact]
    public void ImportJson_ThenChangedParameters_ReportsDifference()
    {
        var vm = Create();
        vm.ImportJson(vm.BuildJson(), "result.json");

        vm.PointOffsetText = "0.8";

        Assert.Contains(vm.ImportLines, l => l.StartsWith("Отличаются параметры") && l.Contains("d"));
        Assert.DoesNotContain(vm.ImportLines, l => l.Contains("воспроизводятся"));
    }

    [Fact]
    public void ImportJson_ExtendsSceneBounds_AndClearRestoresThem()
    {
        var vm = Create();
        var before = vm.Bounds;
        string json = vm.BuildJson();

        vm.VLinWheelText = "30";
        var wide = vm.Bounds;
        vm.ImportJson(json, "result.json");
        Assert.Equal(wide, vm.Bounds);          // импорт короче текущего: границы не сужаются

        vm.VLinWheelText = "10";
        vm.ImportJson(vm.BuildJson(), "again.json");
        vm.ClearImportCommand.Execute(null);

        Assert.False(vm.HasImport);
        Assert.Empty(vm.ImportLines);
        Assert.Equal(before, vm.Bounds);
    }

    [Fact]
    public void ImportJson_BadFile_KeepsPreviousImport_AndReportsError()
    {
        var vm = Create();
        vm.ImportJson(vm.BuildJson(), "good.json");

        vm.ImportJson("not json", "bad.json");

        Assert.True(vm.HasImport);
        Assert.Equal("good.json", vm.Imported!.FileName);
        Assert.Contains("Не удалось открыть", vm.StatusMessage);
    }

    [Fact]
    public void ParseCsv_RoundTripsOwnExport()
    {
        var vm = Create();

        var data = ExperimentImporter.ParseCsv(vm.BuildCsv());

        Assert.Equal(vm.Trajectory.Count, data.Points.Count);
        Assert.Equal(10, data.Parameters.LinearVelocity, 12);
        Assert.Equal(0.5, data.Parameters.PointOffset, 12);
        Assert.Equal(vm.Trajectory[^1].X, data.Points[^1].X, 12);
        Assert.Equal(vm.Trajectory[^1].Vx, data.Points[^1].Vx, 12);
    }

    [Theory]
    [InlineData("")]
    [InlineData("a,b,c")]
    [InlineData("# experiment: RollingWheelCycloid\nt_s,x_m,y_m,vx_m_s,vy_m_s,speed_m_s\n0,0,0.5,0,0,0")]
    [InlineData("# experiment: Other\nt_s,x_m,y_m,vx_m_s,vy_m_s,speed_m_s\n0,0,0,0,0,0\n1,1,1,1,1,1")]
    public void ParseCsv_RejectsBadFiles(string csv)
        => Assert.Throws<FormatException>(() => ExperimentImporter.ParseCsv(csv));

    [Fact]
    public void ParseCsv_RejectsDecimalComma_WithHint()
    {
        string csv = Create().BuildCsv().Replace("0,5", "0;5");
        csv = csv.Replace("\n0,", "\n0;");

        var ex = Assert.Throws<FormatException>(() => ExperimentImporter.ParseCsv(csv));
        Assert.Contains("Строка", ex.Message);
    }

    [Fact]
    public void Import_ChoosesParserByExtension()
    {
        var vm = Create();

        vm.Import(vm.BuildCsv(), "result.CSV");
        Assert.Equal("result.CSV", vm.Imported!.FileName);
        Assert.Contains(vm.ImportLines, l => l.Contains("воспроизводятся"));

        vm.ClearImportCommand.Execute(null);
        vm.Import(vm.BuildJson(), "result.json");
        Assert.Equal("result.json", vm.Imported!.FileName);
        Assert.Contains(vm.ImportLines, l => l.Contains("воспроизводятся"));
    }

    [Fact]
    public void Import_BadCsv_ReportsError_AndKeepsState()
    {
        var vm = Create();

        vm.Import("garbage", "broken.csv");

        Assert.False(vm.HasImport);
        Assert.Contains("Не удалось открыть", vm.StatusMessage);
    }
    [Fact]
    public void Parse_RejectsInvalidRadius()
    {
        string json = Create().BuildJson().Replace("\"rWheel\": 1,", "\"rWheel\": 0,");
        Assert.Throws<FormatException>(() => ExperimentImporter.Parse(json));
    }

    [Fact]
    public void ParseCsv_RejectsInvalidRadius()
    {
        string csv = Create().BuildCsv().Replace("# rWheel_m: 1", "# rWheel_m: 0");
        Assert.Throws<FormatException>(() => ExperimentImporter.ParseCsv(csv));
    }

    [Fact]
    public void Parse_RejectsInconsistentOmega()
    {
        string json = Create().BuildJson().Replace("\"omega\": 10,", "\"omega\": 11,");

        var ex = Assert.Throws<FormatException>(() => ExperimentImporter.Parse(json));
        Assert.Contains("?", ex.Message);
    }

    [Fact]
    public void ParseCsv_RejectsInconsistentOmega()
    {
        string csv = Create().BuildCsv().Replace("# omega_rad_s: 10", "# omega_rad_s: 11");

        var ex = Assert.Throws<FormatException>(() => ExperimentImporter.ParseCsv(csv));
        Assert.Contains("?", ex.Message);
    }
}
