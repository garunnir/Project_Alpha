# Pattern Matching (C# 9)

## Type Patterns

```csharp
// is expression
if (obj is string s)
    Console.WriteLine(s.Length);

// is not null (C# 9)
if (obj is not null) { }

// negation pattern
if (obj is not string) { }
```

## Property Patterns

```csharp
if (user is { Name: "Admin", Age: >= 18 })
    GrantAccess();

// Nested
if (order is { Customer: { IsVerified: true }, Total: > 100 })
    ApplyDiscount();
```

## Switch Expressions (C# 8+, common in C# 9)

```csharp
// BAD - old switch statement
string GetLabel(int status) {
    switch (status) {
        case 1: return "Active";
        case 2: return "Inactive";
        default: return "Unknown";
    }
}

// GOOD - switch expression
string GetLabel(int status) => status switch
{
    1 => "Active",
    2 => "Inactive",
    _ => "Unknown"
};

// With type pattern
string Describe(object obj) => obj switch
{
    int n when n > 0 => "positive int",
    int n            => "non-positive int",
    string s         => $"string of length {s.Length}",
    null             => "null",
    _                => "something else"
};
```

## Relational Patterns (C# 9)

```csharp
string ClassifyAge(int age) => age switch
{
    < 13  => "Child",
    < 18  => "Teen",
    < 65  => "Adult",
    >= 65 => "Senior"
};
```

## Logical Patterns (C# 9) — and, or, not

```csharp
bool IsValidPort(int port) => port is >= 1 and <= 65535;

bool IsWeekend(DayOfWeek day) => day is DayOfWeek.Saturday or DayOfWeek.Sunday;

if (value is not (null or ""))
    Process(value);
```

## Tuple Patterns

```csharp
string GetMoveResult(bool isWin, bool isDraw) => (isWin, isDraw) switch
{
    (true, _)  => "Win",
    (_, true)  => "Draw",
    _          => "Loss"
};
```
