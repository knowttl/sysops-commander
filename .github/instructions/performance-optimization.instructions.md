---
applyTo: '*'
description: 'The most comprehensive, practical, and engineer-authored performance optimization instructions for all languages, frameworks, and stacks. Covers frontend, backend, and database best practices with actionable guidance, scenario-based checklists, troubleshooting, and pro tips.'
---

# Performance Optimization Best Practices

## General Principles

- **Measure First, Optimize Second:** Always profile and measure before optimizing. Use benchmarks, profilers, and monitoring tools to identify real bottlenecks. Guessing is the enemy of performance.
  - *Pro Tip:* Use tools like dotTrace, Visual Studio Profiler, or PerfView for .NET.
- **Optimize for the Common Case:** Focus on optimizing code paths that are most frequently executed. Don't waste time on rare edge cases unless they're critical.
- **Avoid Premature Optimization:** Write clear, maintainable code first; optimize only when necessary. Premature optimization can make code harder to read and maintain.
- **Minimize Resource Usage:** Use memory, CPU, network, and disk resources efficiently. Always ask: "Can this be done with less?"
- **Prefer Simplicity:** Simple algorithms and data structures are often faster and easier to optimize. Don't over-engineer.
- **Document Performance Assumptions:** Clearly comment on any code that is performance-critical or has non-obvious optimizations.
- **Understand the Platform:** Know the performance characteristics of your language, framework, and runtime.
- **Automate Performance Testing:** Integrate performance tests and benchmarks into your CI/CD pipeline. Catch regressions early.
- **Set Performance Budgets:** Define acceptable limits for load time, memory usage, API latency, etc. Enforce them with automated checks.

## Backend Performance

### Algorithm and Data Structure Optimization
- **Choose the Right Data Structure:** Arrays for sequential access, hash maps for fast lookups, trees for hierarchical data, etc.
- **Efficient Algorithms:** Use binary search, quicksort, or hash-based algorithms where appropriate.
- **Avoid O(n^2) or Worse:** Profile nested loops and recursive calls. Refactor to reduce complexity.
- **Batch Processing:** Process data in batches to reduce overhead (e.g., bulk database inserts).
- **Streaming:** Use streaming APIs for large data sets to avoid loading everything into memory.

### Concurrency and Parallelism
- **Asynchronous I/O:** Use async/await, callbacks, or event loops to avoid blocking threads.
- **Thread/Worker Pools:** Use pools to manage concurrency and avoid resource exhaustion.
- **Avoid Race Conditions:** Use locks, semaphores, or atomic operations where needed.
- **Bulk Operations:** Batch network/database calls to reduce round trips.
- **Backpressure:** Implement backpressure in queues and pipelines to avoid overload.

### Caching
- **Cache Expensive Computations:** Use in-memory caches for hot data.
- **Cache Invalidation:** Use time-based (TTL), event-based, or manual invalidation. Stale cache is worse than no cache.
- **Don't Cache Everything:** Some data is too volatile or sensitive to cache.

### API and Network
- **Minimize Payloads:** Compress responses and avoid sending unnecessary data.
- **Pagination:** Always paginate large result sets.
- **Connection Pooling:** Reuse connections for databases and external services.

### Logging and Monitoring
- **Minimize Logging in Hot Paths:** Excessive logging can slow down critical code.
- **Structured Logging:** Use JSON or key-value logs for easier parsing and analysis.
- **Monitor Everything:** Latency, throughput, error rates, resource usage.

### .NET-Specific Tips
- Use `async/await` for I/O-bound operations.
- Use `Span<T>` and `Memory<T>` for efficient memory access.
- Profile with dotTrace, Visual Studio Profiler, or PerfView.
- Pool objects and connections where appropriate.
- Use `IAsyncEnumerable<T>` for streaming data.
- Use `ConfigureAwait(false)` in library code to avoid deadlocks.
- Use `ValueTask<T>` for high-performance scenarios to reduce allocations.

## Database Performance

### Query Optimization
- **Indexes:** Use indexes on columns that are frequently queried, filtered, or joined. Monitor index usage and drop unused indexes.
- **Avoid SELECT *:** Select only the columns you need.
- **Parameterized Queries:** Prevent injection and improve plan caching.
- **Avoid N+1 Queries:** Use joins or batch queries to avoid repeated queries in loops.
- **Limit Result Sets:** Use LIMIT/OFFSET or cursors for large tables.

### Schema Design
- **Normalization:** Normalize to reduce redundancy, but denormalize for read-heavy workloads if needed.
- **Data Types:** Use the most efficient data types and set appropriate constraints.
- **Archiving:** Regularly archive or purge old data to keep tables small and fast.

### Transactions
- **Short Transactions:** Keep transactions as short as possible to reduce lock contention.
- **Isolation Levels:** Use the lowest isolation level that meets your consistency needs.

## Code Review Checklist for Performance

- [ ] Are there any obvious algorithmic inefficiencies (O(n^2) or worse)?
- [ ] Are data structures appropriate for their use?
- [ ] Are there unnecessary computations or repeated work?
- [ ] Is caching used where appropriate, and is invalidation handled correctly?
- [ ] Are database queries optimized, indexed, and free of N+1 issues?
- [ ] Are large payloads paginated, streamed, or chunked?
- [ ] Are there any memory leaks or unbounded resource usage?
- [ ] Are network requests minimized, batched, and retried on failure?
- [ ] Are there any blocking operations in hot paths?
- [ ] Is logging in hot paths minimized and structured?
- [ ] Are performance-critical code paths documented and tested?
- [ ] Are there automated tests or benchmarks for performance-sensitive code?

## Advanced Topics

### Profiling and Benchmarking
- **Profilers:** Use language-specific profilers to identify bottlenecks.
- **Microbenchmarks:** Write microbenchmarks for critical code paths.
- **Continuous Performance Testing:** Integrate performance tests into CI/CD.

### Memory Management
- **Resource Cleanup:** Always release resources (files, sockets, DB connections) promptly.
- **Object Pooling:** Use for frequently created/destroyed objects.
- **Heap Monitoring:** Monitor heap usage and garbage collection. Tune GC settings for your workload.

### Security and Performance
- **Efficient Crypto:** Use hardware-accelerated and well-maintained cryptographic libraries.
- **Validation:** Validate inputs efficiently; avoid complex regexes in hot paths.
- **Rate Limiting:** Protect against DoS without harming legitimate users.

## References
- [Microsoft .NET Performance Guide](https://learn.microsoft.com/en-us/dotnet/standard/performance/)
- [OWASP: Performance Testing](https://owasp.org/www-project-performance-testing/)
- [Microsoft Performance Best Practices](https://learn.microsoft.com/en-us/azure/architecture/best-practices/performance)
