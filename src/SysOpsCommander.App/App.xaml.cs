using System.Reflection;
using System.Windows;
using System.Windows.Threading;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Serilog;
using SysOpsCommander.App.DependencyInjection;
using SysOpsCommander.Infrastructure.Database;
using SysOpsCommander.Infrastructure.Logging;
using SysOpsCommander.ViewModels;

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

        MainWindow mainWindow = _serviceProvider.GetRequiredService<MainWindow>();
        MainWindowViewModel viewModel = _serviceProvider.GetRequiredService<MainWindowViewModel>();
        mainWindow.DataContext = viewModel;
        mainWindow.Show();
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
        Log.Error(e.Exception, "Unhandled dispatcher exception. CorrelationId: {CorrelationId}", correlationId);

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
        Log.Error(e.Exception, "Unobserved task exception");
        e.SetObserved();
    }
}

