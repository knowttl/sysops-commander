---
description: 'Guidelines for GitHub Copilot to write comments to achieve self-explanatory code with less comments. Examples are in JavaScript but it should work on any language that has comments.'
applyTo: '**'
---

# Self-explanatory Code Commenting Instructions

## Core Principle
**Write code that speaks for itself. Comment only when necessary to explain WHY, not WHAT.**
We do not need comments most of the time.

## Commenting Guidelines

### AVOID These Comment Types

**Obvious Comments**
```csharp
// Bad: States the obvious
int counter = 0; // Initialize counter to zero
counter++;  // Increment counter by one
```

**Redundant Comments**
```csharp
// Bad: Comment repeats the code
public string GetUserName()
{
    return user.Name;  // Return the user's name
}
```

**Outdated Comments**
```csharp
// Bad: Comment doesn't match the code
// Calculate tax at 5% rate
decimal tax = price * 0.08m;  // Actually 8%
```

### WRITE These Comment Types

**Complex Business Logic**
```csharp
// Good: Explains WHY this specific calculation
// Apply progressive tax brackets: 10% up to 10k, 20% above
var tax = CalculateProgressiveTax(income, new[] { 0.10m, 0.20m }, new[] { 10000m });
```

**Non-obvious Algorithms**
```csharp
// Good: Explains the algorithm choice
// Using Floyd-Warshall for all-pairs shortest paths
// because we need distances between all nodes
```

**API Constraints or Gotchas**
```csharp
// Good: Explains external constraint
// WinRM has a 5-minute default timeout; setting explicit timeout
// to prevent hanging connections on unreachable hosts
```

## Decision Framework

Before writing a comment, ask:
1. **Is the code self-explanatory?** → No comment needed
2. **Would a better variable/function name eliminate the need?** → Refactor instead
3. **Does this explain WHY, not WHAT?** → Good comment
4. **Will this help future maintainers?** → Good comment

## Special Cases for Comments

### Public APIs
```csharp
/// <summary>
/// Validates the hostname against NetBIOS, FQDN, and IPv4 formats.
/// Rejects strings containing injection characters.
/// </summary>
/// <param name="hostname">The hostname to validate.</param>
/// <returns>A <see cref="ValidationResult"/> indicating success or failure with error details.</returns>
public ValidationResult ValidateHostname(string hostname)
{
    // ... implementation
}
```

### Configuration and Constants
```csharp
// Good: Explains the source or reasoning
public const int MaxInMemoryResultBytes = 10 * 1024 * 1024;  // 10MB — switch to disk streaming above this
public const int ReachabilityCheckParallelism = 20;  // Empirically determined safe limit for TCP connect checks
```

### Annotations
```csharp
// TODO: Replace with proper user authentication after security review
// FIXME: Memory leak in production - investigate connection pooling
// HACK: Workaround for bug in library v2.1.0 - remove after upgrade
// NOTE: This implementation assumes UTC timezone for all calculations
// WARNING: This function modifies the original collection instead of creating a copy
// PERF: Consider caching this result if called frequently in hot path
// SECURITY: Validate input to prevent injection before using in query
```

## Anti-Patterns to Avoid

### Dead Code Comments
```csharp
// Bad: Don't comment out code — use version control
// var oldFunction = OldMethod();
var newFunction = NewMethod();
```

### Changelog Comments
```csharp
// Bad: Don't maintain history in comments — use git log
// Modified by John on 2023-01-15
// Fixed bug reported by Sarah on 2023-02-03
```

### Divider Comments
```csharp
// Bad: Don't use decorative comments
//=====================================
// UTILITY FUNCTIONS
//=====================================
```

## Quality Checklist

Before committing, ensure your comments:
- [ ] Explain WHY, not WHAT
- [ ] Are grammatically correct and clear
- [ ] Will remain accurate as code evolves
- [ ] Add genuine value to code understanding
- [ ] Are placed appropriately (above the code they describe)
- [ ] Use proper spelling and professional language

## Summary

Remember: **The best comment is the one you don't need to write because the code is self-documenting.**
