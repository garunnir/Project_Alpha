using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;
using System.Collections.Generic;
using IsoTilemap;

public class UIConstruction : MonoBehaviour
{
    [SerializeField] Button prevBtn;
    [SerializeField] Button nextBtn;
    [SerializeField] Button closeBtn;
    [SerializeField] LayoutGroup content;
    [SerializeField] TileMapManager _tileManager;
    [SerializeField] TilePlacementState _placementState;
    [SerializeField] GridCursor _gridCursor;

    private List<string> _categories = new();
    private Dictionary<string, List<TileDefinition>> _grouped = new();
    private int _categoryIndex;
    private Button _selectedButton;

    void Start()
    {
        if(prevBtn == null)         Debug.LogError("prevBtn is null");
        if(nextBtn == null)         Debug.LogError("nextBtn is null");
        if(closeBtn == null)        Debug.LogError("closeBtn is null");
        if(content == null)         Debug.LogError("content is null");
        if(_tileManager == null)    Debug.LogError("tileManager is null");
        if(_placementState == null) Debug.LogError("placementState is null");
        if(_gridCursor == null)     Debug.LogError("gridCursor is null");

        prevBtn.onClick.AddListener(Prev);
        nextBtn.onClick.AddListener(Next);
        closeBtn.onClick.AddListener(Close);

        InputManager.Instance.Actions.UI.Pagination.performed += OnPagination;

        BuildGroups();
        ShowCategory(_categoryIndex);
    }

    void BuildGroups()
    {
        _grouped.Clear();
        _categories.Clear();

        foreach (var entry in _tileManager.PrefabDB.entries)
        {
            if (entry == null) continue;
            string cat = string.IsNullOrEmpty(entry.category) ? "기타" : entry.category;
            if (!_grouped.ContainsKey(cat))
            {
                _grouped[cat] = new List<TileDefinition>();
                _categories.Add(cat);
            }
            _grouped[cat].Add(entry);
        }
    }

    void ShowCategory(int index)
    {
        foreach (Transform child in content.transform)
            Destroy(child.gameObject);

        _selectedButton = null;

        if (_categories.Count == 0) return;

        string cat = _categories[index];
        foreach (var def in _grouped[cat])
            CreateButton(def);

        prevBtn.interactable = index > 0;
        nextBtn.interactable = index < _categories.Count - 1;
    }

    void CreateButton(TileDefinition def)
    {
        var go = new GameObject(def.prefabId, typeof(RectTransform), typeof(Image), typeof(Button));
        go.transform.SetParent(content.transform, false);

        var img = go.GetComponent<Image>();
        if (def.thumbnail != null)
            img.sprite = def.thumbnail;

        var btn = go.GetComponent<Button>();
        btn.onClick.AddListener(() => OnTileButtonClicked(def, btn));
    }

    void OnTileButtonClicked(TileDefinition def, Button btn)
    {
        if (_selectedButton != null)
            _selectedButton.image.color = Color.white;

        _selectedButton = btn;
        btn.image.color = Color.yellow;

        _placementState.Select(def);
    }

    void OnPagination(InputAction.CallbackContext context)
    {
        float dir = context.ReadValue<float>();
        if (dir < 0) Prev();
        else if (dir > 0) Next();
    }

    void Prev()
    {
        if (_categoryIndex <= 0) return;
        _categoryIndex--;
        ShowCategory(_categoryIndex);
    }

    void Next()
    {
        if (_categoryIndex >= _categories.Count - 1) return;
        _categoryIndex++;
        ShowCategory(_categoryIndex);
    }

    void Close()
    {
        _placementState.Clear();
        _gridCursor.SetActive(false);
        InputManager.Instance.SwitchToPlayer();
        gameObject.SetActive(false);
    }

    public void Open()
    {
        gameObject.SetActive(true);
        InputManager.Instance.SwitchToUI();
        _gridCursor.SetActive(true);
    }
    private void OnEnable()
    {
        Open();
    }
    private void OnDisable()
    {
        Close();
    }

    void OnDestroy()
    {
        if (InputManager.Instance == null) return;
        InputManager.Instance.Actions.UI.Pagination.performed -= OnPagination;
    }
}
