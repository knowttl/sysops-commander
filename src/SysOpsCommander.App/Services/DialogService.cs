using System.Windows;
using Microsoft.Win32;
using SysOpsCommander.App.Dialogs;
using SysOpsCommander.Core.Enums;
using SysOpsCommander.Core.Interfaces;
using SysOpsCommander.Core.Models;
using SysOpsCommander.ViewModels.Dialogs;

namespace SysOpsCommander.App.Services;

/// <summary>
/// WPF implementation of <see cref="IDialogService"/> using native dialog types.
/// All dialog operations are dispatched to the UI thread.
/// </summary>
public sealed class DialogService : IDialogService
{
    private readonly IActiveDirectoryService _adService;

    /// <summary>
    /// Initializes a new instance of the <see cref="DialogService"/> class.
    /// </summary>
    /// <param name="adService">The Active Directory service for domain selector dialog.</param>
    public DialogService(IActiveDirectoryService adService)
    {
        ArgumentNullException.ThrowIfNull(adService);
        _adService = adService;
    }

    /// <inheritdoc/>
    public Task<DomainConnection?> ShowDomainSelectorAsync()
    {
        var viewModel = new DomainSelectorViewModel(_adService);
        var dialog = new DomainSelectorDialog(viewModel)
        {
            Owner = Application.Current.MainWindow
        };

        bool? dialogResult = dialog.ShowDialog();

        return Task.FromResult(dialogResult == true ? viewModel.Result : null);
    }

    /// <inheritdoc/>
    public Task<CredentialDialogResult?> ShowCredentialDialogAsync(
        string? defaultDomain = null,
        WinRmAuthMethod? defaultAuth = null)
    {
        var viewModel = new CredentialDialogViewModel(defaultDomain, defaultAuth);
        var dialog = new CredentialDialog(viewModel)
        {
            Owner = Application.Current.MainWindow
        };

        bool? dialogResult = dialog.ShowDialog();

        return Task.FromResult(dialogResult == true ? dialog.CredentialResult : null);
    }

    /// <inheritdoc/>
    public Task<string?> ShowSaveFileDialogAsync(string defaultExtension, string filter)
    {
        var dialog = new SaveFileDialog
        {
            DefaultExt = defaultExtension,
            Filter = filter
        };

        bool? result = dialog.ShowDialog(Application.Current.MainWindow);

        return Task.FromResult(result == true ? dialog.FileName : null);
    }

    /// <inheritdoc/>
    public Task<string?> ShowOpenFileDialogAsync(string filter)
    {
        var dialog = new OpenFileDialog
        {
            Filter = filter
        };

        bool? result = dialog.ShowDialog(Application.Current.MainWindow);

        return Task.FromResult(result == true ? dialog.FileName : null);
    }

    /// <inheritdoc/>
    public Task<bool> ShowConfirmationAsync(string title, string message)
    {
        MessageBoxResult result = MessageBox.Show(
            message,
            title,
            MessageBoxButton.YesNo,
            MessageBoxImage.Question);

        return Task.FromResult(result == MessageBoxResult.Yes);
    }

    /// <inheritdoc/>
    public void ShowError(string title, string message) =>
        MessageBox.Show(message, title, MessageBoxButton.OK, MessageBoxImage.Error);

    /// <inheritdoc/>
    public void ShowInfo(string title, string message) =>
        MessageBox.Show(message, title, MessageBoxButton.OK, MessageBoxImage.Information);
}
