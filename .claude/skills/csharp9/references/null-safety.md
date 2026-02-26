# Null Safety

## Enable Nullable Reference Types

```csharp
// In .csproj (recommended for all projects)
<Nullable>enable</Nullable>

// Or per-file
#nullable enable
```

## Null-Conditional Operator ?.

```csharp
string? name = GetName(); // may be null

// BAD
if (name != null) Console.WriteLine(name.Length);

// GOOD
Console.WriteLine(name?.Length);       // returns null if name is null
Console.WriteLine(name?.ToUpper());
```

## Null-Coalescing ?? and ??=

```csharp
string display = name ?? "Anonymous";        // fallback value
name ??= "Default";                          // assign only if null

// Chaining
string result = GetFirst() ?? GetSecond() ?? "fallback";
```

## Null-Forgiving Operator ! (use sparingly)

```csharp
// Tell compiler "I know this isn't null"
string value = MightReturnNull()!;

// Only use when you're certain — suppresses warnings, not errors
```

## Pattern Matching for Null Checks

```csharp
// C# 9 - preferred style
if (obj is not null) { }
if (obj is null) { }

// is with property pattern
if (user is { Name: not null, Age: > 0 }) { }
```

## Throw Helpers

```csharp
public class OrderService
{
    private readonly IRepository _repo;

    public OrderService(IRepository repo)
    {
        // ArgumentNullException.ThrowIfNull (C# 10+, use below for C# 9)
        _repo = repo ?? throw new ArgumentNullException(nameof(repo));
    }
}
```

## Nullable Annotations

```csharp
public string? FindName(int id)   // may return null
public string GetName(int id)     // never returns null (contract)

// Parameters
public void Process(string? input)  // nullable param
public void Process(string input)   // non-null guaranteed
```

## Rules
- Enable `<Nullable>enable</Nullable>` in all new projects
- Never use `!` (null-forgiving) as a shortcut — fix the root cause
- Use `is null` / `is not null` over `== null` for pattern consistency
