using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Serilog;
using SysOpsCommander.App.Services;
using SysOpsCommander.Core.Interfaces;
using SysOpsCommander.Core.Models;
using SysOpsCommander.Infrastructure.Database;
using SysOpsCommander.Infrastructure.FileSystem;
using SysOpsCommander.Services;
using SysOpsCommander.Services.Strategies;
using SysOpsCommander.ViewModels;
using SysOpsCommander.ViewModels.Dialogs;

namespace SysOpsCommander.App.DependencyInjection;

/// <summary>
/// Provides extension methods for registering application services in the DI container.
/// </summary>
public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Registers all service interfaces and implementations. <see cref="IHostTargetingService"/> is registered as a singleton.
    /// </summary>
    /// <param name="services">The service collection to add to.</param>
    /// <returns>The same <see cref="IServiceCollection"/> for chaining.</returns>
    public static IServiceCollection AddSysOpsServices(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        _ = services.AddSingleton<IHostTargetingService, HostTargetingService>();
        _ = services.AddSingleton<ISettingsService, SettingsService>();
        _ = services.AddSingleton<IScriptValidationService, ScriptValidationService>();
        _ = services.AddSingleton<IDirectoryAccessor, DirectoryAccessor>();
        _ = services.AddSingleton<IActiveDirectoryService, ActiveDirectoryService>();

        _ = services.AddSingleton<IExecutionStrategy, PowerShellRemoteStrategy>();
        _ = services.AddSingleton<IExecutionStrategy, WmiQueryStrategy>();
        _ = services.AddSingleton<IRemoteExecutionService, RemoteExecutionService>();
        _ = services.AddSingleton<ICredentialService, CredentialService>();
        _ = services.AddSingleton<INotificationService, NotificationService>();
        _ = services.AddSingleton<IAuditLogService, AuditLogService>();
        _ = services.AddSingleton<IScriptLoaderService, ScriptLoaderService>();
        _ = services.AddSingleton<IExportService, ExportService>();

        _ = services.AddSingleton<IAutoUpdateService, AutoUpdateService>();

        _ = services.AddSingleton<IDialogService, DialogService>();

        return services;
    }

    /// <summary>
    /// Registers all ViewModels as transient services.
    /// </summary>
    /// <param name="services">The service collection to add to.</param>
    /// <returns>The same <see cref="IServiceCollection"/> for chaining.</returns>
    public static IServiceCollection AddSysOpsViewModels(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        _ = services.AddTransient<MainWindowViewModel>();
        _ = services.AddTransient<DashboardViewModel>();
        _ = services.AddTransient<AdExplorerViewModel>();
        _ = services.AddTransient<ExecutionViewModel>();
        _ = services.AddTransient<ScriptLibraryViewModel>();
        _ = services.AddTransient<AuditLogViewModel>();
        _ = services.AddTransient<SettingsViewModel>();
        _ = services.AddTransient<DomainSelectorViewModel>();

        return services;
    }

    /// <summary>
    /// Registers infrastructure services including configuration binding, repositories, database, and logging.
    /// </summary>
    /// <param name="services">The service collection to add to.</param>
    /// <param name="configuration">The application configuration root.</param>
    /// <returns>The same <see cref="IServiceCollection"/> for chaining.</returns>
    public static IServiceCollection AddSysOpsInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configuration);

        AppConfiguration appConfig = new();
        configuration.GetSection("SysOpsCommander").Bind(appConfig);
        _ = services.AddSingleton(appConfig);

        _ = services.AddSingleton<DatabaseInitializer>();
        _ = services.AddSingleton<IAuditLogRepository, AuditLogRepository>();
        _ = services.AddSingleton<ISettingsRepository, SettingsRepository>();
        _ = services.AddSingleton<IScriptFileProvider, ScriptFileProvider>();
        _ = services.AddSingleton(Log.Logger);

        return services;
    }
}
