using Avalonia.Controls;
using WMSApp.ViewModels;

namespace WMSApp.Views;

public partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();
    }

    protected override void OnClosing(WindowClosingEventArgs e)
    {
        if (DataContext is MainViewModel vm)
        {
            vm.CleanupForExitAsync().GetAwaiter().GetResult();
        }

        base.OnClosing(e);
    }
}