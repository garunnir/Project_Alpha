# LINQ

## Method Syntax (preferred)

```csharp
var names = people
    .Where(p => p.Age >= 18)
    .OrderBy(p => p.Name)
    .Select(p => p.Name)
    .ToList();
```

## Common Operators

```csharp
// Filtering
.Where(x => x.Active)

// Projection
.Select(x => x.Name)
.Select(x => new { x.Id, x.Name })   // anonymous type

// Ordering
.OrderBy(x => x.Name)
.OrderByDescending(x => x.Age)
.ThenBy(x => x.Name)                 // secondary sort

// Aggregation
.Count()
.Count(x => x.Active)
.Sum(x => x.Price)
.Min(x => x.Age)
.Max(x => x.Score)
.Average(x => x.Score)

// Element
.First()                    // throws if empty
.FirstOrDefault()           // returns null/default if empty
.Single()                   // throws if not exactly one
.SingleOrDefault()

// Existence
.Any()
.Any(x => x.Age > 18)
.All(x => x.Active)
.Contains(item)

// Flattening
.SelectMany(x => x.Tags)    // flatten nested collections

// Grouping
.GroupBy(x => x.Category)

// Set operations
.Distinct()
.Union(other)
.Intersect(other)
.Except(other)
```

## Deferred vs Immediate Execution

```csharp
// Deferred - query not executed yet
var query = people.Where(p => p.Age > 18);

// Immediate - executes now
var list = query.ToList();
var array = query.ToArray();
var count = query.Count();

// IMPORTANT: calling ToList() prevents multiple enumeration
var results = GetExpensiveData().Where(x => x.Active).ToList();
// reuse results multiple times safely
```

## Avoid Common Mistakes

```csharp
// BAD - multiple enumeration
IEnumerable<User> users = GetUsers();
if (users.Any())            // enumerates once
    var first = users.First(); // enumerates again

// GOOD
var users = GetUsers().ToList();
if (users.Count > 0)
    var first = users[0];

// BAD - unnecessary ToList mid-chain
var result = items.ToList().Where(x => x.Active).ToList();

// GOOD - ToList only at the end
var result = items.Where(x => x.Active).ToList();
```
