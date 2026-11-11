using MahApps.Metro.Controls;

using MsSql.Mimic.Ui.ViewModel;

namespace MsSql.Mimic.Ui;

public partial class ConnectionWindow : MetroWindow
{
    public ConnectionWindow(ConnectionWindowViewModel viewModel)
    {
        InitializeComponent();

        DataContext = viewModel;

        Loaded += (_, _) => viewModel.InitViewModel();

        viewModel.CloseRequested += (_, result) => DialogResult = result;
    }
}