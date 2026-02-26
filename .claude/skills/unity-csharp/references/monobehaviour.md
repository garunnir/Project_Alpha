# MonoBehaviour Lifecycle

## Execution Order

```
Awake()       → called when object is initialized (even if disabled)
OnEnable()    → called when object becomes active
Start()       → called before first Update(), only if enabled
Update()      → called every frame
LateUpdate()  → called every frame, after all Update() calls
FixedUpdate() → called at fixed physics intervals (default 0.02s)
OnDisable()   → called when object is deactivated
OnDestroy()   → called when object is destroyed
```

## When to Use Each

| Method | Use for |
|--------|---------|
| `Awake()` | Initialize references, self-setup. Runs regardless of enabled state. |
| `Start()` | Setup that depends on other objects being initialized. |
| `Update()` | Input, movement, non-physics logic. |
| `FixedUpdate()` | Rigidbody physics, forces. |
| `LateUpdate()` | Camera follow, anything that must happen after all Updates. |
| `OnEnable/Disable` | Subscribe/unsubscribe events, reset state. |

## Best Practice Template

```csharp
public class PlayerController : MonoBehaviour
{
    [SerializeField] private float _moveSpeed = 5f;
    [SerializeField] private Rigidbody2D _rb;

    private Vector2 _input;

    private void Awake()
    {
        // Cache components here
        if (_rb == null)
            _rb = GetComponent<Rigidbody2D>();
    }

    private void Start()
    {
        // Initialization that depends on other objects
    }

    private void Update()
    {
        _input = new Vector2(Input.GetAxisRaw("Horizontal"), Input.GetAxisRaw("Vertical"));
    }

    private void FixedUpdate()
    {
        _rb.linearVelocity = _input * _moveSpeed;
    }
}
```

## Common Mistakes

```csharp
// BAD - GetComponent every frame
void Update() {
    GetComponent<Rigidbody2D>().AddForce(Vector2.up); // allocates every frame
}

// GOOD - cached in Awake
private Rigidbody2D _rb;
void Awake() { _rb = GetComponent<Rigidbody2D>(); }
void Update() { _rb.AddForce(Vector2.up); }
```
