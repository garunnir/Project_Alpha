using System;
using IsoTilemap;
using UnityEngine;
using UnityEngine.InputSystem;

public class GridCursor : MonoBehaviour
{
    [SerializeField] TileMapController _controller;
    [SerializeField] TilePlacementState _placementState;
    [SerializeField] GameObject _cursorVisual;
    [SerializeField] Camera _camera;

    private static readonly Plane GroundPlane = new Plane(Vector3.up, Vector3.zero);

    private Vector3Int _cursorGridPos;

    private Vector2 _heldDir;
    private float _holdTimer;
    private float _repeatTimer;

    const float HOLD_THRESHOLD = 1f;
    const float REPEAT_INTERVAL = 0.15f;

    void Start()
    {
        if (_camera == null) _camera = Camera.main;

        var actions = InputManager.Instance.Actions;
        actions.UI.Navigate.started  += OnNavigateStarted;
        actions.UI.Navigate.canceled += OnNavigateCanceled;
        actions.UI.Submit.performed  += OnSubmit;
    }

    void Update()
    {
        UpdateFromPointer();
        UpdateHoldRepeat();

        if (Pointer.current?.press.wasPressedThisFrame ?? false)
            TryPlace();
    }

    // 포인터(마우스)가 이 프레임에 움직였을 때만 커서 위치를 절대 좌표로 갱신한다.
    // 움직이지 않으면 키보드 Navigate 입력이 우선된다.
    void UpdateFromPointer()
    {
        if (Pointer.current == null) return;
        if (Pointer.current.delta.ReadValue() == Vector2.zero) return;

        Vector2 screenPos = Pointer.current.position.ReadValue();
        Ray ray = _camera.ScreenPointToRay(screenPos);
        if (!GroundPlane.Raycast(ray, out float dist)) return;

        Vector3Int newGrid = TileHelper.ConvertWorldToGrid(ray.GetPoint(dist));
        if (newGrid == _cursorGridPos) return;

        _cursorGridPos = newGrid;
        UpdateVisual();
    }

    void UpdateHoldRepeat()
    {
        if (_heldDir == Vector2.zero) return;

        _holdTimer += Time.deltaTime;
        if (_holdTimer < HOLD_THRESHOLD) return;

        _repeatTimer += Time.deltaTime;
        if (_repeatTimer >= REPEAT_INTERVAL)
        {
            MoveCursor(_heldDir);
            _repeatTimer = 0f;
        }
    }

    void OnNavigateStarted(InputAction.CallbackContext ctx)
    {
        Vector2 dir = ctx.ReadValue<Vector2>();
        _heldDir = dir;
        _holdTimer = 0f;
        _repeatTimer = 0f;
        MoveCursor(dir);
    }

    void OnNavigateCanceled(InputAction.CallbackContext ctx)
    {
        _heldDir = Vector2.zero;
        _holdTimer = 0f;
        _repeatTimer = 0f;
    }

    void MoveCursor(Vector2 dir)
    {
        // 아이소메트릭 기준: 입력 x → grid x, 입력 y → grid z
        _cursorGridPos += new Vector3Int(
            Mathf.RoundToInt(dir.x),
            0,
            Mathf.RoundToInt(dir.y)
        );
        UpdateVisual();
    }

    void OnSubmit(InputAction.CallbackContext ctx) => TryPlace();

    void TryPlace()
    {
        if (_placementState.Selected == null) return;

        var def = _placementState.Selected;
        byte placedType = InferTileTypeFromPrefabId(def.prefabId);
        var tileData = new TileData
        {
            tileDefId = Guid.NewGuid(),
            state     = new TileState(),
            identity  = new TileIdentity
            {
                PrefabId  = def.prefabId,
                GridPos   = _cursorGridPos,
                sizeUnit  = Vector3Int.one,
                tileType  = placedType,
                edgeFace  = placedType == (byte)TileView.TileType.EdgeWall ? (byte)0 : TileIdentity.EdgeFaceNone,
            }
        };
        _controller.AddAndFlush(tileData);
    }

    void UpdateVisual()
    {
        UpdateVisual(TileHelper.ConvertGridToWorldPos(_cursorGridPos));
    }
    void UpdateVisual(Vector3 worldPos)
    {
        if (_cursorVisual == null) return;
        _cursorVisual.transform.position = worldPos;
    }

    public void SetActive(bool active)
    {
        enabled = active;
        if (_cursorVisual != null)
            _cursorVisual.SetActive(active);
    }

    static byte InferTileTypeFromPrefabId(string prefabId)
    {
        if (string.IsNullOrEmpty(prefabId)) return 0;
        if (prefabId.StartsWith("SlimWall/", StringComparison.Ordinal))
            return (byte)TileView.TileType.EdgeWall;
        if (prefabId.StartsWith("Floor/", StringComparison.Ordinal))
            return (byte)TileView.TileType.Floor;
        if (prefabId.StartsWith("ThickWall/", StringComparison.Ordinal))
            return (byte)TileView.TileType.Wall;
        return 0;
    }

    void OnDestroy()
    {
        if (InputManager.Instance == null) return;
        var actions = InputManager.Instance.Actions;
        actions.UI.Navigate.started  -= OnNavigateStarted;
        actions.UI.Navigate.canceled -= OnNavigateCanceled;
        actions.UI.Submit.performed  -= OnSubmit;
    }
}
