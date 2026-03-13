using SysOpsCommander.Core.Enums;
using SysOpsCommander.Core.Models;

namespace SysOpsCommander.Core.Interfaces;

/// <summary>
/// Abstracts WPF dialog interactions so ViewModels remain decoupled from UI framework types.
/// </summary>
public interface IDialogService
{
    /// <summary>
    /// Shows the domain selector dialog and returns the selected domain connection, or <see langword="null"/> if cancelled.
    /// </summary>
    /// <returns>The selected <see cref="DomainConnection"/>, or <see langword="null"/> if the user cancelled.</returns>
    Task<DomainConnection?> ShowDomainSelectorAsync();

    /// <summary>
    /// Shows the credential dialog and returns the entered credentials, or <see langword="null"/> if cancelled.
    /// </summary>
    /// <param name="defaultDomain">The default domain to pre-populate.</param>
    /// <param name="defaultAuth">The default authentication method to pre-select.</param>
    /// <returns>A <see cref="CredentialDialogResult"/> containing the credentials, or <see langword="null"/> if the user cancelled.</returns>
    Task<CredentialDialogResult?> ShowCredentialDialogAsync(string? defaultDomain = null, WinRmAuthMethod? defaultAuth = null);

    /// <summary>
    /// Shows a save-file dialog and returns the selected file path, or <see langword="null"/> if cancelled.
    /// </summary>
    /// <param name="defaultExtension">The default file extension (e.g., ".csv").</param>
    /// <param name="filter">The file type filter string (e.g., "CSV files|*.csv").</param>
    /// <returns>The selected file path, or <see langword="null"/> if cancelled.</returns>
    Task<string?> ShowSaveFileDialogAsync(string defaultExtension, string filter);

    /// <summary>
    /// Shows an open-file dialog and returns the selected file path, or <see langword="null"/> if cancelled.
    /// </summary>
    /// <param name="filter">The file type filter string.</param>
    /// <returns>The selected file path, or <see langword="null"/> if cancelled.</returns>
    Task<string?> ShowOpenFileDialogAsync(string filter);

    /// <summary>
    /// Shows a confirmation dialog with Yes/No buttons.
    /// </summary>
    /// <param name="title">The dialog title.</param>
    /// <param name="message">The confirmation message.</param>
    /// <returns><see langword="true"/> if the user confirmed; otherwise, <see langword="false"/>.</returns>
    Task<bool> ShowConfirmationAsync(string title, string message);

    /// <summary>
    /// Shows an error message dialog.
    /// </summary>
    /// <param name="title">The dialog title.</param>
    /// <param name="message">The error message.</param>
    void ShowError(string title, string message);

    /// <summary>
    /// Shows an informational message dialog.
    /// </summary>
    /// <param name="title">The dialog title.</param>
    /// <param name="message">The informational message.</param>
    void ShowInfo(string title, string message);

    /// <summary>
    /// Shows a text input dialog and returns the entered value, or <see langword="null"/> if cancelled.
    /// </summary>
    /// <param name="title">The dialog title.</param>
    /// <param name="prompt">The prompt message displayed to the user.</param>
    /// <param name="defaultValue">The default text value pre-populated in the input field.</param>
    /// <returns>The text entered by the user, or <see langword="null"/> if cancelled.</returns>
    Task<string?> ShowInputDialogAsync(string title, string prompt, string defaultValue = "");

    /// <summary>
    /// Sets text content to the system clipboard.
    /// </summary>
    /// <param name="text">The text to place on the clipboard.</param>
    void SetClipboardText(string text);
}
