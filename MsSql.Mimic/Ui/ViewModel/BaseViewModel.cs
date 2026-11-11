using CommunityToolkit.Mvvm.ComponentModel;

using MahApps.Metro.Controls.Dialogs;

using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Media;
using System.Windows.Threading;

namespace MsSql.Mimic.Ui.ViewModel;

/// <summary>
/// Provides the base functions of a view model
/// </summary>
public abstract partial class BaseViewModel : ObservableObject
{
    /// <summary>
    /// The brush used to display default information text.
    /// </summary>
    private readonly SolidColorBrush _infoTextColorDefault = new(Colors.Black);

    /// <summary>
    /// The brush used to display success information text.
    /// </summary>
    private readonly SolidColorBrush _infoTextColorSuccess = new(Colors.Green);

    /// <summary>
    /// The brush used to display failure information text.
    /// </summary>
    private readonly SolidColorBrush _infoTextColorFailure = new(Colors.Red);
    
    /// <summary>
    /// The different error types
    /// </summary>
    protected enum ErrorMessageType
    {
        /// <summary>
        /// General error
        /// </summary>
        General,
 
        /// <summary>
        /// Error while saving
        /// </summary>
        Load,
 
        /// <summary>
        /// Error while loading
        /// </summary>
        Save,
        
        //Error while deleting.
        Delete
    }

    /// <summary>
    /// Defines the visual category of informational text displayed by a view model.
    /// </summary>
    protected enum InfoTextType
    {
        /// <summary>
        /// Standard informational text.
        /// </summary>
        Default,

        /// <summary>
        /// Indicates informational text that represents a successful operation.
        /// </summary>
        Success,

        /// <summary>
        /// Indicates informational text that represents a failed operation.
        /// </summary>
        Failure
    }

    /// <summary>
    /// The instance of the mah apps dialog coordinator
    /// </summary>
    private readonly IDialogCoordinator _dialogCoordinator;

    /// <summary>
    /// The timer that controls how long informational text remains visible
    /// </summary>
    private readonly DispatcherTimer _infoTextTimer = new();

    /// <summary>
    /// Gets the informational message text displayed by the view model.
    /// </summary>
    [ObservableProperty]
    public partial string InfoText { get; private set; } = string.Empty;

    /// <summary>
    /// Gets the brush used to display informational text.
    /// </summary>
    [ObservableProperty]
    public partial SolidColorBrush InfoTextColor { get; private set; } = new(Colors.Black);
 
    /// <summary>
    /// Creates a new instance of the <see cref="BaseViewModel"/>
    /// </summary>
    protected BaseViewModel()
    {
        _dialogCoordinator = DialogCoordinator.Instance;
        
        _infoTextTimer.Interval = TimeSpan.FromSeconds(5);
        _infoTextTimer.Tick += (_, _) =>
        {
            InfoText = string.Empty;
            _infoTextTimer.Stop();
        };
    }
 
    /// <summary>
    /// Shows a message dialog
    /// </summary>
    /// <param name="title">The title of the dialog</param>
    /// <param name="message">The message of the dialog</param>
    /// <returns>The awaitable task</returns>
    protected async Task ShowMessageAsync(string title, string message)
    {
        await _dialogCoordinator.ShowMessageAsync(this, title, message);
    }
 
    /// <summary>
    /// Shows a question dialog with two buttons
    /// </summary>
    /// <param name="title">The title of the dialog</param>
    /// <param name="message">The message of the dialog</param>
    /// <param name="okButtonText">The text of the ok button (optional)</param>
    /// <param name="cancelButtonText">The text of the cancel button (optional)</param>
    /// <returns>The dialog result</returns>
    protected async Task<MessageDialogResult> ShowQuestionAsync(string title, string message, string okButtonText = "OK",
        string cancelButtonText = "Cancel")
    {
        var result = await _dialogCoordinator.ShowMessageAsync(this, title, message,
            MessageDialogStyle.AffirmativeAndNegative, new MetroDialogSettings
            {
                AffirmativeButtonText = okButtonText,
                NegativeButtonText = cancelButtonText
            });
 
        return result;
    }
 
    /// <summary>
    /// Shows an error message and logs the exception
    /// </summary>
    /// <param name="ex">The exception which was thrown</param>
    /// <param name="messageType">The desired message type</param>
    /// <param name="caller">The name of the method, which calls this method. Value will be filled automatically</param>
    /// <returns>The awaitable task</returns>
    protected async Task ShowErrorAsync(Exception ex, ErrorMessageType messageType,
        [CallerMemberName] string caller = "")
    {
        LogError(ex, caller);
 
        var message = messageType switch
        {
            ErrorMessageType.Load => "An error has occurred while loading the data.",
            ErrorMessageType.Save => "An error has occurred while saving the data.",
            ErrorMessageType.Delete => "AN error has occurred while deleting the data.",
            _ => "An error has occurred."
        };
 
        await _dialogCoordinator.ShowMessageAsync(this, "Error", message);
    }
 
    /// <summary>
    /// Logs an error
    /// </summary>
    /// <param name="ex">The exception which was thrown</param>
    /// <param name="caller">The name of the method, which calls this method. Value will be filled automatically</param>
    protected static void LogError(Exception ex, [CallerMemberName] string caller = "")
    {
        // Here your logging function like the following (needs Serilog)
        // Log.Error(ex, "An error has occurred. Caller: {caller}", caller);
    }
 
    /// <summary>
    /// Shows a progress dialog
    /// </summary>
    /// <param name="title">The title of the dialog</param>
    /// <param name="message">The message of the dialog</param>
    /// <param name="ctSource">The cancellation token source (optional)</param>
    /// <returns>The dialog controller</returns>
    protected async Task<ProgressDialogController> ShowProgressAsync(string title, string message, CancellationTokenSource? ctSource = default)
    {
        var controller = await _dialogCoordinator.ShowProgressAsync(this, title, message, ctSource != null);
        controller.SetIndeterminate();
 
        if (ctSource != null)
        {
            controller.Canceled += (_, _) => ctSource.Cancel();
        }
 
        return controller;
    }
 
    /// <summary>
    /// Copies the content to the clipboard
    /// </summary>
    /// <param name="content">The content which should be copied</param>
    protected static void CopyToClipboard(string content)
    {
        Clipboard.SetText(content);
    }

    /// <summary>
    /// Displays informational text using the specified message type and restarts the visibility timer.
    /// </summary>
    /// <param name="text">The informational text to display.</param>
    /// <param name="infoTextType">The type of informational text to display.</param>
    protected void ShowInfoText(string text, InfoTextType infoTextType = InfoTextType.Default)
    {
        InfoText = text;

        InfoTextColor = infoTextType switch
        {
            InfoTextType.Success => _infoTextColorSuccess,
            InfoTextType.Failure => _infoTextColorFailure,
            _ => _infoTextColorDefault
        };
        
        // Stop the timer, if it's running.
        _infoTextTimer.Stop();
        
        // Starts the timer
        _infoTextTimer.Start();
    }
}