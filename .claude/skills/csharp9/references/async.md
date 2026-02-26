# Async / Await

## Basic Pattern

```csharp
// Always return Task or Task<T>, never void (except event handlers)
public async Task<string> FetchDataAsync(string url)
{
    using var client = new HttpClient();
    return await client.GetStringAsync(url);
}

// Calling
var data = await FetchDataAsync("https://example.com");
```

## Return Types

```csharp
async Task DoSomethingAsync()          // no return value
async Task<int> GetCountAsync()        // returns int
async ValueTask<int> GetCachedAsync()  // lightweight, prefer when often sync
```

## ConfigureAwait

```csharp
// In library code - avoid capturing context
var result = await SomeOperationAsync().ConfigureAwait(false);

// In UI/ASP.NET controller code - omit ConfigureAwait (keep context)
var result = await SomeOperationAsync();
```

## CancellationToken

```csharp
public async Task<List<User>> GetUsersAsync(CancellationToken ct = default)
{
    await Task.Delay(1000, ct);             // respects cancellation
    return await _repo.GetAllAsync(ct);
}

// Caller
var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
var users = await GetUsersAsync(cts.Token);
```

## Parallel Async

```csharp
// BAD - sequential (one at a time)
var a = await GetAAsync();
var b = await GetBAsync();

// GOOD - parallel
var taskA = GetAAsync();
var taskB = GetBAsync();
var (a, b) = (await taskA, await taskB);

// Or with WhenAll
var results = await Task.WhenAll(GetAAsync(), GetBAsync(), GetCAsync());
```

## Exception Handling

```csharp
try
{
    var result = await RiskyOperationAsync();
}
catch (HttpRequestException ex)
{
    // handle specific exception
}
catch (OperationCanceledException)
{
    // handle cancellation separately
}
```

## Rules
- NEVER use `async void` except for event handlers
- NEVER block with `.Result` or `.Wait()` — causes deadlocks
- Always pass `CancellationToken` through the call chain
- Suffix async methods with `Async` (e.g. `GetUserAsync`)
- Use `ValueTask` only when the method frequently completes synchronously
