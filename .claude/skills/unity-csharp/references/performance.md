# Performance & Optimization

## Avoid Garbage Allocation

```csharp
// BAD - allocates string every frame
void Update() {
    if (gameObject.tag == "Enemy") { }
}

// GOOD
void Update() {
    if (CompareTag("Enemy")) { }
}

// BAD - allocates WaitForSeconds every call
IEnumerator Loop() {
    while (true) {
        yield return new WaitForSeconds(0.5f);
    }
}

// GOOD - cache it
private static readonly WaitForSeconds _halfSecond = new(0.5f);
IEnumerator Loop() {
    while (true) {
        yield return _halfSecond;
    }
}
```

## Cache Expensive Calls

```csharp
// BAD
void Update() {
    GetComponent<Renderer>().material.color = Color.red;
    GameObject.Find("Player").transform.position;
    Camera.main.transform.position; // Camera.main is expensive
}

// GOOD
private Renderer _renderer;
private Transform _playerTransform;
private Transform _cameraTransform;

void Awake() {
    _renderer = GetComponent<Renderer>();
    _playerTransform = GameObject.Find("Player").transform;
    _cameraTransform = Camera.main.transform;
}
```

## Object Pooling (avoid Instantiate/Destroy)

```csharp
public class ObjectPool<T> where T : MonoBehaviour
{
    private readonly Queue<T> _pool = new();
    private readonly T _prefab;
    private readonly Transform _parent;

    public ObjectPool(T prefab, int initialSize, Transform parent = null)
    {
        _prefab = prefab;
        _parent = parent;
        for (int i = 0; i < initialSize; i++)
            CreateNew();
    }

    public T Get()
    {
        if (_pool.Count == 0) CreateNew();
        var obj = _pool.Dequeue();
        obj.gameObject.SetActive(true);
        return obj;
    }

    public void Return(T obj)
    {
        obj.gameObject.SetActive(false);
        _pool.Enqueue(obj);
    }

    private void CreateNew()
    {
        var obj = Object.Instantiate(_prefab, _parent);
        obj.gameObject.SetActive(false);
        _pool.Enqueue(obj);
    }
}
```

> Unity 2021+ has built-in `UnityEngine.Pool.ObjectPool<T>` — prefer that over custom implementations.

## Physics Tips

```csharp
// Use layers + Physics.IgnoreLayerCollision to reduce collision checks
// Use OverlapCircleNonAlloc instead of OverlapCircle (no allocation)
private Collider2D[] _results = new Collider2D[10]; // reuse buffer

void DetectEnemies() {
    int count = Physics2D.OverlapCircleNonAlloc(transform.position, 5f, _results, enemyLayer);
    for (int i = 0; i < count; i++) {
        // process _results[i]
    }
}
```

## Profiling Checklist
- Use **Unity Profiler** (Window > Analysis > Profiler) before optimizing
- Check **Deep Profile** for script-level breakdown
- Target 60fps = 16.6ms per frame budget
- Watch for GC Alloc spikes in the profiler
