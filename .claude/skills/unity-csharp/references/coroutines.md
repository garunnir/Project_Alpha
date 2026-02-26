# Coroutines

## Basic Syntax

```csharp
private IEnumerator FadeOut(float duration)
{
    float elapsed = 0f;
    while (elapsed < duration)
    {
        elapsed += Time.deltaTime;
        float alpha = Mathf.Lerp(1f, 0f, elapsed / duration);
        _canvasGroup.alpha = alpha;
        yield return null; // wait one frame
    }
    _canvasGroup.alpha = 0f;
}

// Start
StartCoroutine(FadeOut(1f));

// Stop
StopCoroutine(FadeOut(1f)); // unreliable - use reference instead
```

## Yield Options

```csharp
yield return null;                          // next frame
yield return new WaitForSeconds(2f);        // wait N seconds
yield return new WaitForFixedUpdate();      // next FixedUpdate
yield return new WaitUntil(() => _ready);   // until condition is true
yield return new WaitWhile(() => _loading); // while condition is true
yield return StartCoroutine(Other());       // wait for another coroutine
```

## Stopping Coroutines Safely

```csharp
private Coroutine _spawnRoutine;

private void StartSpawning()
{
    // Stop existing before starting new
    if (_spawnRoutine != null)
        StopCoroutine(_spawnRoutine);

    _spawnRoutine = StartCoroutine(SpawnLoop());
}

private void StopSpawning()
{
    if (_spawnRoutine != null)
    {
        StopCoroutine(_spawnRoutine);
        _spawnRoutine = null;
    }
}
```

## Cache WaitForSeconds (Performance)

```csharp
// BAD - allocates garbage every call
yield return new WaitForSeconds(0.1f);

// GOOD - reuse cached instance
private static readonly WaitForSeconds _wait01 = new WaitForSeconds(0.1f);
yield return _wait01;
```

## Coroutine vs async/await

| | Coroutine | async/await |
|---|---|---|
| Unity integration | Native | Needs UniTask or workaround |
| Stop mid-execution | `StopCoroutine()` | CancellationToken |
| Recommended for | Simple delays, sequences | Complex async logic |

> For complex async flows, consider **UniTask** (free, on GitHub) over built-in coroutines.
