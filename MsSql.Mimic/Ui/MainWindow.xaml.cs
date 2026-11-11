using ICSharpCode.AvalonEdit.Highlighting;
using ICSharpCode.AvalonEdit.Highlighting.Xshd;

using MahApps.Metro.Controls;

using MsSql.Mimic.Ui.ViewModel;

using System.IO;
using System.Windows.Media;
using System.Xml;

namespace MsSql.Mimic.Ui;

public partial class MainWindow : MetroWindow
{
    /// <summary>
    /// Represents the main application window that initializes the user interface, binds the main view model, and starts window-specific setup when loaded.
    /// </summary>
    public MainWindow(MainWindowViewModel viewModel)
    {
        InitializeComponent();

        DataContext = viewModel;

        Loaded += (_, _) => InitTextEditor();
        ContentRendered += (_, _) => viewModel.InitViewModel();
    }

    /// <summary>
    /// Initializes the text editor's visual styling, including background, foreground, line number, link text, and caret colors.
    /// </summary>
    private void InitTextEditor()
    {
        TextEditor.Background = GetColor(30, 30, 30);
        TextEditor.Foreground = GetColor(212, 212, 212);
        TextEditor.LineNumbersForeground = GetColor(133, 133, 133);
        TextEditor.TextArea.TextView.LinkTextForegroundBrush = GetColor(86, 156, 214);
        TextEditor.TextArea.Caret.CaretBrush = Brushes.White;
        
        // Load the scheme
        using var stream = File.OpenRead(Path.Combine(AppContext.BaseDirectory, "Assets", "VsCodeDark.xshd"));
        using var reader = new XmlTextReader(stream);

        TextEditor.SyntaxHighlighting = HighlightingLoader.Load(reader, HighlightingManager.Instance);
    }

    /// <summary>
    /// Creates a solid color brush from the specified RGB color components.
    /// </summary>
    /// <param name="r">The red color component.</param>
    /// <param name="g">The green color component.</param>
    /// <param name="b">The blue color component.</param>
    /// <returns>A solid color brush initialized with the specified RGB color.</returns>
    private static SolidColorBrush GetColor(byte r, byte g, byte b)
    {
        return new SolidColorBrush(Color.FromRgb(r, g, b));
    }
}