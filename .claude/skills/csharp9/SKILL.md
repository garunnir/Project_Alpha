---
name: csharp9
description: >
  C# 9 language features, patterns, and best practices. Use this skill when
  the user asks about C# code, syntax, patterns, or language features.
  Triggers on keywords like "C#", "csharp", "class", "record", "interface",
  "LINQ", "async", "await", "pattern matching", "null", "generics", "delegate".
  Do NOT use for Unity-specific scripting (use unity-csharp skill instead).
---

This skill covers idiomatic C# 9 code patterns and best practices.

## When to load references

- Classes, records, structs → read `references/types.md`
- Null handling, null safety → read `references/null-safety.md`
- LINQ queries → read `references/linq.md`
- async/await, Task → read `references/async.md`
- Pattern matching, switch expressions → read `references/pattern-matching.md`

## Core Rules

- Prefer `record` over `class` for immutable data containers
- Use `init` properties for immutable setters
- Always use `?.` and `??` for null-safe access — never assume non-null
- Prefer `is` pattern matching over casting with `as` + null check
- Use `var` when the type is obvious from the right-hand side
- Prefer expression-bodied members for simple single-line methods
- Avoid `public` fields — use properties
- Mark classes `sealed` when inheritance is not intended

## C# 9 Key Features at a Glance

```csharp
// Records
record Person(string Name, int Age);

// Init-only properties
public class Config {
    public string Host { get; init; }
}

// Pattern matching enhancements
if (obj is string { Length: > 0 } s) { }

// Target-typed new
List<string> items = new();

// Top-level statements (entry point)
Console.WriteLine("Hello");
```

## Naming Conventions

- Classes/Records/Interfaces: `PascalCase`
- Private fields: `_camelCase`
- Public properties/methods: `PascalCase`
- Local variables/params: `camelCase`
- Constants: `PascalCase` (not ALL_CAPS in modern C#)
- Interfaces: prefix with `I` (e.g. `IRepository`)
