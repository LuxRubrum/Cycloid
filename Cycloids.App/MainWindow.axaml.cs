using System.Text;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Platform.Storage;
using Cycloids.ViewModel;

namespace Cycloids.App;

public partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();
        DataContext = new MainViewModel(new AvaloniaFrameTimer());
    }

    private MainViewModel Vm => (MainViewModel)DataContext!;

    private async void OnExportCsv(object? sender, RoutedEventArgs e)
        => await SaveTextAsync("Сохранить траекторию (CSV)", "cycloid-trajectory.csv", "csv", "CSV", "*.csv", Vm.BuildCsv);

    private async void OnExportJson(object? sender, RoutedEventArgs e)
        => await SaveTextAsync("Сохранить результаты (JSON)", "cycloid-result.json", "json", "JSON", "*.json", Vm.BuildJson);

    private async void OnImportFile(object? sender, RoutedEventArgs e)
    {
        try
        {
            var files = await StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
            {
                Title = "Открыть результаты (JSON или CSV)",
                AllowMultiple = false,
                FileTypeFilter = new[] { new FilePickerFileType("Результаты (JSON, CSV)") { Patterns = new[] { "*.json", "*.csv" } } },
            });

            if (files.Count == 0)
                return; // пользователь отменил выбор

            await using var stream = await files[0].OpenReadAsync();
            using var reader = new StreamReader(stream, Encoding.UTF8);
            string text = await reader.ReadToEndAsync();

            Vm.Import(text, files[0].Name);
        }
        catch (Exception ex)
        {
            Vm.ReportStatus($"Не удалось прочитать файл: {ex.Message}");
        }
    }

    private async Task SaveTextAsync(
        string title, string suggestedName, string extension, string typeName, string pattern, Func<string> build)
    {
        try
        {
            var file = await StorageProvider.SaveFilePickerAsync(new FilePickerSaveOptions
            {
                Title = title,
                SuggestedFileName = suggestedName,
                DefaultExtension = extension,
                FileTypeChoices = new[] { new FilePickerFileType(typeName) { Patterns = new[] { pattern } } },
            });

            if (file is null)
                return; // пользователь отменил выбор

            string text = build();
            await using var stream = await file.OpenWriteAsync();
            stream.SetLength(0);
            await using var writer = new StreamWriter(stream, new UTF8Encoding(false));
            await writer.WriteAsync(text);

            Vm.ReportStatus($"Сохранено: {file.Name}");
        }
        catch (Exception ex)
        {
            Vm.ReportStatus($"Не удалось сохранить файл: {ex.Message}");
        }
    }
}
