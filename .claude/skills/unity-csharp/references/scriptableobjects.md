# ScriptableObjects

## Basic Definition

```csharp
[CreateAssetMenu(fileName = "WeaponData", menuName = "Game/Weapon Data")]
public class WeaponData : ScriptableObject
{
    [SerializeField] private string _weaponName;
    [SerializeField] private float _damage;
    [SerializeField] private float _fireRate;
    [SerializeField] private Sprite _icon;

    public string WeaponName => _weaponName;
    public float Damage => _damage;
    public float FireRate => _fireRate;
    public Sprite Icon => _icon;
}
```

## Usage in MonoBehaviour

```csharp
public class Weapon : MonoBehaviour
{
    [SerializeField] private WeaponData _data;

    public void Fire()
    {
        // use _data.Damage, _data.FireRate etc.
    }
}
```

## Common Use Cases

| Use case | Example |
|----------|---------|
| Game config | `GameSettings`, `DifficultyConfig` |
| Item/unit data | `WeaponData`, `EnemyStats`, `ItemDefinition` |
| Global events | `GameEventSO` (event bus pattern) |
| Shared runtime state | `IntVariable`, `FloatVariable` |

## ScriptableObject Event Bus Pattern

```csharp
// Event asset
[CreateAssetMenu(menuName = "Events/Game Event")]
public class GameEventSO : ScriptableObject
{
    private readonly List<GameEventListener> _listeners = new();

    public void Raise()
    {
        for (int i = _listeners.Count - 1; i >= 0; i--)
            _listeners[i].OnEventRaised();
    }

    public void Register(GameEventListener listener) => _listeners.Add(listener);
    public void Unregister(GameEventListener listener) => _listeners.Remove(listener);
}

// Listener component
public class GameEventListener : MonoBehaviour
{
    [SerializeField] private GameEventSO _event;
    [SerializeField] private UnityEvent _response;

    private void OnEnable() => _event.Register(this);
    private void OnDisable() => _event.Unregister(this);
    public void OnEventRaised() => _response?.Invoke();
}
```

## Rules
- ScriptableObjects persist data between scenes (asset-based)
- Do NOT store scene-specific runtime references in ScriptableObjects
- Use `[CreateAssetMenu]` for easy asset creation in Editor
- Great for decoupling systems without singletons
