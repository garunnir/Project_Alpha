# Events & Delegates

## C# Events (Recommended)

```csharp
// Publisher
public class PlayerHealth : MonoBehaviour
{
    public event Action OnDeath;
    public event Action<int> OnHealthChanged; // passes value

    private int _health = 100;

    public void TakeDamage(int amount)
    {
        _health -= amount;
        OnHealthChanged?.Invoke(_health);

        if (_health <= 0)
            OnDeath?.Invoke();
    }
}

// Subscriber
public class UIManager : MonoBehaviour
{
    [SerializeField] private PlayerHealth _player;

    private void OnEnable()
    {
        _player.OnHealthChanged += UpdateHealthBar;
        _player.OnDeath += ShowGameOver;
    }

    private void OnDisable()
    {
        // ALWAYS unsubscribe to avoid memory leaks
        _player.OnHealthChanged -= UpdateHealthBar;
        _player.OnDeath -= ShowGameOver;
    }

    private void UpdateHealthBar(int newHealth) { /* ... */ }
    private void ShowGameOver() { /* ... */ }
}
```

## UnityEvent (Inspector-friendly)

```csharp
using UnityEngine.Events;

public class Button : MonoBehaviour
{
    public UnityEvent OnClick; // assignable in Inspector

    private void OnMouseDown()
    {
        OnClick?.Invoke();
    }
}
```

## Common Delegate Types

```csharp
Action           // no params, no return
Action<T>        // one param, no return
Action<T1, T2>   // two params, no return
Func<T>          // no params, returns T
Func<T1, T2>     // one param, returns T2
Predicate<T>     // one param, returns bool
```

## Static Events (Global)

```csharp
// For game-wide events (use sparingly)
public static class GameEvents
{
    public static event Action OnGameStart;
    public static event Action OnGameOver;
    public static event Action<int> OnScoreChanged;

    public static void TriggerGameStart() => OnGameStart?.Invoke();
    public static void TriggerGameOver() => OnGameOver?.Invoke();
    public static void TriggerScoreChanged(int score) => OnScoreChanged?.Invoke(score);
}
```

## Rules
- ALWAYS unsubscribe in `OnDisable()` or `OnDestroy()`
- Use `?.Invoke()` null-safe syntax
- Prefer C# events over UnityEvent for code-only logic
- Prefer UnityEvent when designers need to wire up connections in Inspector
