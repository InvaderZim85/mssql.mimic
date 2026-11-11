using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

using MsSql.Mimic.Application.Services;
using MsSql.Mimic.Domain.Dtos;
using MsSql.Mimic.Domain.Models;
using MsSql.Mimic.Infrastructure.MsSql;

using System.Collections.ObjectModel;

namespace MsSql.Mimic.Ui.ViewModel;

public sealed partial class ConnectionWindowViewModel : BaseViewModel
{
    public event EventHandler<bool?>? CloseRequested; 
    
    private readonly SqlConnectionFactory? _connectionFactory;

    private readonly ConnectionHistoryService? _connectionHistoryService;

    private bool _connectionEstablished;
    
    private ConnectionProfile? _originalConnection;

    public ConnectionViewModel Connection { get; set; } = new();
    
    [ObservableProperty]
    public partial string Title { get; private set; } = "New connection";
    
    [ObservableProperty]
    public partial bool ButtonSaveEnabled { get; private set; }

    public ConnectionWindowViewModel() { }

    public ConnectionWindowViewModel(SqlConnectionFactory connectionFactory, ConnectionHistoryService connectionHistoryService)
    {
        _connectionFactory = connectionFactory;
        _connectionHistoryService = connectionHistoryService;
    }

    [ObservableProperty]
    public partial ObservableCollection<IdValueDto> ApplicationIntents { get; private set; } = [];

    public void LoadConnection(ConnectionProfile connection)
    {
        _originalConnection = connection;

        Title = "Edit connection";

        Connection = ConnectionViewModel.FromProfile(connection);
        OnPropertyChanged(nameof(Connection));
    }
    
    public void InitViewModel()
    {
        foreach (var intent in Enum.GetValues<ConnectionIntent>())
            ApplicationIntents.Add(new IdValueDto((int)intent, intent.ToString()));

        Connection.ApplicationIntent = ApplicationIntents[0];
    }

    [RelayCommand]
    private async Task TryConnect()
    {
        if (_connectionFactory is null)
            return;

        var result = await _connectionFactory.CanConnect(Connection.ToProfile());

        if (result.Success)
        {
            ButtonSaveEnabled = true;
            _connectionEstablished = true;

            await ShowMessageAsync("Connection",
                $"Connection to '{Connection.Server}' / '{Connection.Database}' successfully established.)");
        }
        else
        {
            ButtonSaveEnabled = false;
            _connectionEstablished = false;
            
            await ShowMessageAsync("Connection failed.",
                $"""
                 Can't establish connection to server '{Connection.Server}' / database '{Connection.Database}'.

                 Message:
                 {result.ErrorMessage}
                 """);
        }
    }

    [RelayCommand]
    private async Task SaveConnection()
    {
        if (_connectionFactory is null || _connectionHistoryService is null || !_connectionEstablished)
            return;

        var connectionProfile = Connection.ToProfile();
        
        try
        {
            // Check if we need to delete the original connection
            if (_originalConnection is not null && _originalConnection.IsConnectionKeyChanged(connectionProfile))
                await _connectionHistoryService.DeleteConnectionAsync(_originalConnection);
            
            await _connectionHistoryService.SaveConnectionAsync(connectionProfile);
        }
        catch (Exception ex)
        {
            await ShowErrorAsync(ex, ErrorMessageType.Save);
        }
    }

    [RelayCommand]
    private void Cancel()
    {
        CloseRequested?.Invoke(this, false);
    }
}