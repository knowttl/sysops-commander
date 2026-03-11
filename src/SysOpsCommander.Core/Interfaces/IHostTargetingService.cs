using System.Collections.ObjectModel;
using SysOpsCommander.Core.Models;

namespace SysOpsCommander.Core.Interfaces;

/// <summary>
/// Manages the list of target hosts shared between AD Explorer and Execution views.
/// Registered as a singleton in the DI container.
/// </summary>
public interface IHostTargetingService
{
    /// <summary>
    /// Gets the observable collection of target hosts for UI binding.
    /// </summary>
    ObservableCollection<HostTarget> Targets { get; }

    /// <summary>
    /// Adds hosts from a list of hostname strings, validating each one.
    /// </summary>
    /// <param name="hostnames">The hostnames to add.</param>
    void AddFromHostnames(IEnumerable<string> hostnames);

    /// <summary>
    /// Adds hosts from a CSV file containing hostname entries.
    /// </summary>
    /// <param name="filePath">The path to the CSV file.</param>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    Task AddFromCsvFileAsync(string filePath, CancellationToken cancellationToken);

    /// <summary>
    /// Adds hosts from Active Directory computer search results.
    /// </summary>
    /// <param name="computers">The AD computer objects to add.</param>
    void AddFromAdSearchResults(IEnumerable<AdObject> computers);

    /// <summary>
    /// Checks reachability of all pending targets using the specified connection options.
    /// </summary>
    /// <param name="connectionOptions">The WinRM connection options for TCP port checks.</param>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    Task CheckReachabilityAsync(WinRmConnectionOptions connectionOptions, CancellationToken cancellationToken);

    /// <summary>
    /// Clears all target hosts from the collection.
    /// </summary>
    void ClearTargets();

    /// <summary>
    /// Removes a specific target host by hostname.
    /// </summary>
    /// <param name="hostname">The hostname to remove.</param>
    void RemoveTarget(string hostname);
}
