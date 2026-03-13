using System.Reflection;
using System.Windows;
using System.Windows.Threading;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Serilog;
using SysOpsCommander.App.DependencyInjection;
using SysOpsCommander.Core.Extensions;
using SysOpsCommander.Core.Interfaces;
using SysOpsCommander.Infrastructure.Database;
using SysOpsCommander.Infrastructure.Logging;
using SysOpsCommander.ViewModels;
using Wpf.Ui.Appearance;

namespace SysOpsCommander.App;

/// <summary>
/// Application composition root. Configures DI, Serilog, and global exception handling.
/// </summary>
public partial class App : Application
{
    private IServiceProvider? _serviceProvider;
    private CorrelationIdEnricher? _correlationIdEnricher;

    /// <inheritdoc/>
    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        _correlationIdEnricher = new CorrelationIdEnricher();
        _ = SerilogConfigurator.Configure(_correlationIdEnricher);

        WireExceptionHandlers();

        string version = Assembly.GetExecutingAssembly()
            .GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion
            ?? "unknown";

        Log.Information("SysOps Commander starting — version {Version}", version);

        IConfiguration configuration = new ConfigurationBuilder()
            .SetBasePath(AppDomain.CurrentDomain.BaseDirectory)
            .AddJsonFile("appsettings.json", optional: false, reloadOnChange: false)
            .Build();

        ServiceCollection services = new();
        _ = services.AddSysOpsInfrastructure(configuration);
        _ = services.AddSysOpsServices();
        _ = services.AddSysOpsViewModels();
        _ = services.AddSingleton<MainWindow>();

        _serviceProvider = services.BuildServiceProvider();

        DatabaseInitializer databaseInitializer = _serviceProvider.GetRequiredService<DatabaseInitializer>();
        databaseInitializer.InitializeAsync().GetAwaiter().GetResult();

        CheckPendingUpdate(_serviceProvider);

        MainWindow mainWindow = _serviceProvider.GetRequiredService<MainWindow>();
        MainWindowViewModel viewModel = _serviceProvider.GetRequiredService<MainWindowViewModel>();
        mainWindow.DataContext = viewModel;
        mainWindow.Show();

        // Apply Mica backdrop and watch for system theme changes
        ApplicationThemeManager.Apply(mainWindow);
        SystemThemeWatcher.Watch(mainWindow);

        // Initialize ViewModel after window is shown to avoid blocking startup
        viewModel.InitializeAsync().SafeFireAndForget();
    }

    /// <summary>
    /// Checks for a staged update and launches the updater if one is ready.
    /// Cleans up orphaned pending-update markers on failure.
    /// </summary>
    private static void CheckPendingUpdate(IServiceProvider serviceProvider)
    {
        try
        {
            IAutoUpdateService updateService = serviceProvider.GetRequiredService<IAutoUpdateService>();
            if (updateService.HasPendingUpdate())
            {
                Log.Information("Pending update detected — launching updater");
                updateService.LaunchUpdaterAndExit();
            }
        }
        catch (Exception ex)
        {
            Log.Warning(ex, "Pending update check failed — continuing normal startup");
        }
    }

    /// <inheritdoc/>
    protected override void OnExit(ExitEventArgs e)
    {
        Log.Information("SysOps Commander shutting down");
        Log.CloseAndFlush();
        base.OnExit(e);
    }

    private void WireExceptionHandlers()
    {
        DispatcherUnhandledException += OnDispatcherUnhandledException;
        AppDomain.CurrentDomain.UnhandledException += OnAppDomainUnhandledException;
        TaskScheduler.UnobservedTaskException += OnUnobservedTaskException;
    }

    private void OnDispatcherUnhandledException(object sender, DispatcherUnhandledExceptionEventArgs e)
    {
        string correlationId = _correlationIdEnricher?.CorrelationId ?? "unknown";

        // WPF-UI FluentWindow may internally apply a Button-targeted style to a ToggleButton
        // during template loading. This is a benign rendering glitch — suppress the MessageBox.
        if (e.Exception is System.Windows.Markup.XamlParseException { InnerException: InvalidOperationException ioe }
            && ioe.Message.Contains("TargetType does not match", StringComparison.Ordinal))
        {
            Log.Warning(e.Exception, "Suppressed benign XAML style mismatch. CorrelationId: {CorrelationId}", correlationId);
            e.Handled = true;
            return;
        }

        // Cancellation and disposal from view navigation are expected — log and suppress
        if (e.Exception is OperationCanceledException or ObjectDisposedException
            or TaskCanceledException)
        {
            Log.Debug(e.Exception, "Suppressed cancellation/disposal exception. CorrelationId: {CorrelationId}", correlationId);
            e.Handled = true;
            return;
        }

        Log.Error(e.Exception,
            "Unhandled dispatcher exception. CorrelationId: {CorrelationId}, ExceptionType: {ExceptionType}",
            correlationId, e.Exception.GetType().FullName);

        _ = MessageBox.Show(
            $"An unexpected error occurred.\n\nCorrelation ID: {correlationId}\n\nPlease report this to IT.",
            "SysOps Commander — Error",
            MessageBoxButton.OK,
            MessageBoxImage.Error);

        e.Handled = true;
    }

    private static void OnAppDomainUnhandledException(object sender, UnhandledExceptionEventArgs e)
    {
        if (e.ExceptionObject is Exception ex)
        {
            Log.Fatal(ex, "Unhandled AppDomain exception (IsTerminating: {IsTerminating})", e.IsTerminating);
        }
    }

    private static void OnUnobservedTaskException(object? sender, UnobservedTaskExceptionEventArgs e)
    {
        // Cancellation from disposed ViewModels is expected during rapid navigation
        if (e.Exception.InnerExceptions.All(
                static ex => ex is OperationCanceledException or ObjectDisposedException or TaskCanceledException))
        {
            Log.Debug(e.Exception, "Suppressed unobserved cancellation/disposal exception");
        }
        else
        {
            Log.Error(e.Exception, "Unobserved task exception ({Count} inner exceptions)",
                e.Exception.InnerExceptions.Count);
        }

        e.SetObserved();
    }
}

