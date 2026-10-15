using Cycloids.ViewModel;

namespace Cycloids.View;

internal static class Program
{
    [STAThread]
    static void Main()
    {
        ApplicationConfiguration.Initialize();

        // По шаблону MVVM создаём ViewModel и передаём её в форму View
        var viewModel = new MainViewModel();
        Application.Run(new MainForm(viewModel));
    }
}