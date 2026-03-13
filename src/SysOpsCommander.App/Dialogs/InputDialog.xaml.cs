using System.Windows;

namespace SysOpsCommander.App.Dialogs;

/// <summary>
/// A simple text input dialog for prompting the user for a string value.
/// </summary>
public partial class InputDialog : Window
{
    /// <summary>
    /// Gets the text entered by the user.
    /// </summary>
    public string InputText => InputTextBox.Text;

    /// <summary>
    /// Initializes a new instance of the <see cref="InputDialog"/> class.
    /// </summary>
    /// <param name="title">The dialog title.</param>
    /// <param name="prompt">The prompt message.</param>
    /// <param name="defaultValue">The default text value.</param>
    public InputDialog(string title, string prompt, string defaultValue)
    {
        InitializeComponent();
        Title = title;
        PromptText.Text = prompt;
        InputTextBox.Text = defaultValue;
        Loaded += (_, _) =>
        {
            _ = InputTextBox.Focus();
            InputTextBox.SelectAll();
        };
    }

    private void OkButton_Click(object sender, RoutedEventArgs e) =>
        DialogResult = true;
}
