# Types: Class, Record, Struct

## When to Use What

| Type | Use when |
|------|---------|
| `class` | Mutable objects, identity matters, inheritance needed |
| `record` | Immutable data, value equality, DTOs, domain models |
| `struct` | Small value types, no inheritance, performance-critical |
| `record struct` | Immutable small value types (C# 10+, avoid in C# 9) |

## Records (C# 9)

```csharp
// Positional record - concise
record Point(double X, double Y);

// Usage
var p1 = new Point(1, 2);
var p2 = new Point(1, 2);
bool equal = p1 == p2; // true — value equality

// Non-destructive mutation with 'with'
var p3 = p1 with { X = 99 }; // p1 unchanged, p3 = (99, 2)
```

## Init-Only Properties

```csharp
public class ServerConfig
{
    public string Host { get; init; } = "localhost";
    public int Port { get; init; } = 8080;
}

// Set at construction only
var config = new ServerConfig { Host = "prod.example.com", Port = 443 };
// config.Host = "other"; // compile error
```

## Class Best Practices

```csharp
// Seal classes not meant for inheritance
public sealed class TokenService
{
    private readonly string _secret;

    public TokenService(string secret)
    {
        _secret = secret ?? throw new ArgumentNullException(nameof(secret));
    }

    public string Generate(string userId) => /* ... */ "";
}
```

## Interfaces

```csharp
public interface IRepository<T>
{
    T? GetById(int id);
    IEnumerable<T> GetAll();
    void Save(T entity);
}

// Default interface methods (C# 8+, use sparingly)
public interface ILogger
{
    void Log(string message);
    void LogError(string message) => Log($"[ERROR] {message}"); // default impl
}
```
