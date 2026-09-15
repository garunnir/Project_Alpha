// ============================================================
// UICraftingWindow — 제작 창 3열 (카테고리·레시피·상세)
// ============================================================

using System;
using System.Collections.Generic;
using System.Text;
using Garunnir.Runtime.Gameplay.Data;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

public sealed class UICraftingWindow : MonoBehaviour
{
    [SerializeField] TMP_Text _headerTitle;
    [SerializeField] Button _closeButton;
    [SerializeField] UIWindowDragHandler _windowDragHandler;

    [SerializeField] RectTransform _categoryContent;
    [SerializeField] UICraftingCategoryRow _categoryRowPrefab;

    [SerializeField] TMP_InputField _searchField;
    [SerializeField] Button _gridButton;
    [SerializeField] Button _listButton;
    [SerializeField] ScrollRect _gridScroll;
    [SerializeField] RectTransform _gridViewport;
    [SerializeField] RectTransform _gridContent;
    [SerializeField] UICraftingRecipeCell _gridCellPrefab;
    [SerializeField] ScrollRect _listScroll;
    [SerializeField] RectTransform _listViewport;
    [SerializeField] RectTransform _listContent;
    [SerializeField] UICraftingRecipeListRow _listRowPrefab;

    [SerializeField] Image _resultIcon;
    [SerializeField] TMP_Text _resultName;
    [SerializeField] Button _starButton;
    [SerializeField] Image _starImage;
    [SerializeField] TMP_Text _timeText;
    [SerializeField] Image _bookIcon;
    [SerializeField] TMP_Text _workbenchText;
    [SerializeField] Image _lightIcon;
    [SerializeField] TMP_Text _skillsText;
    [SerializeField] TMP_Text _requiredHeader;
    [SerializeField] RectTransform _ingredientContent;
    [SerializeField] UICraftingIngredientCard _ingredientCardPrefab;
    [SerializeField] Button _craftButton;
    [SerializeField] TMP_Text _craftLabel;
    [SerializeField] TMP_Text _outputsHeader;
    [SerializeField] RectTransform _outputContent;
    [SerializeField] Button _qtyMinusButton;
    [SerializeField] Button _qtyPlusButton;
    [SerializeField] Button _qtyMaxButton;
    [SerializeField] TMP_Text _qtyMaxLabel;
    [SerializeField] TMP_InputField _quantityField;
    [SerializeField] TMP_Text _timeRequiredText;
    [SerializeField] Image _progressFill;

    readonly CraftingFavoritesStore _favorites = new();
    readonly List<RecipeData> _allRecipes = new();
    readonly List<string> _recipeNames = new();
    readonly List<string> _categoryIds = new();
    readonly List<RecipeData> _filtered = new();
    readonly List<UICraftingCategoryRow> _categoryPool = new();
    readonly List<UICraftingRecipeCell> _gridPool = new();
    readonly List<UICraftingRecipeListRow> _listPool = new();
    readonly List<UICraftingIngredientCard> _ingredientPool = new();
    readonly List<UICraftingIngredientCard> _outputPool = new();
    readonly List<int> _componentAltIndices = new();
    readonly List<int> _toolAltIndices = new();
    readonly List<int> _qualityAltIndices = new();
    readonly List<string> _altIdBuffer = new(8);
    readonly List<ContextMenuEntry> _altMenuEntries = new(8);
    readonly StringBuilder _skillsBuilder = new();

    PlayerInventoryRuntime _runtime;
    CraftingMaterialPool _pool;
    Action _onClose;
    RecipeData _selected;
    string _selectedCategoryId = CraftingWindowLabels.CategoryAllId;
    string _toolFilter;
    string _search = string.Empty;
    bool _bound;
    int _quantity = 1;
    bool _syncingQuantity;
    bool _craftRunning;
    float _craftElapsed;
    float _craftDuration;

    public bool IsCraftRunning => _craftRunning;
    public float CraftProgress01 =>
        !_craftRunning || _craftDuration <= 0f
            ? 0f
            : Mathf.Clamp01(_craftElapsed / _craftDuration);
    int _pendingCraftQuantity;
    int _lastDisplayedTimeSeconds = -1;
    bool _lastTimeWasRemaining;

    public RectTransform WindowRect => transform as RectTransform;

    public void Wire(
        TMP_Text headerTitle,
        Button closeButton,
        UIWindowDragHandler dragHandler,
        RectTransform categoryContent,
        UICraftingCategoryRow categoryRowPrefab,
        TMP_InputField searchField,
        Button gridButton,
        Button listButton,
        ScrollRect gridScroll,
        RectTransform gridViewport,
        RectTransform gridContent,
        UICraftingRecipeCell gridCellPrefab,
        ScrollRect listScroll,
        RectTransform listViewport,
        RectTransform listContent,
        UICraftingRecipeListRow listRowPrefab,
        Image resultIcon,
        TMP_Text resultName,
        Button starButton,
        Image starImage,
        TMP_Text timeText,
        Image bookIcon,
        TMP_Text workbenchText,
        Image lightIcon,
        TMP_Text skillsText,
        TMP_Text requiredHeader,
        RectTransform ingredientContent,
        UICraftingIngredientCard ingredientCardPrefab,
        Button craftButton,
        TMP_Text craftLabel)
    {
        _headerTitle = headerTitle;
        _closeButton = closeButton;
        _windowDragHandler = dragHandler;
        _categoryContent = categoryContent;
        _categoryRowPrefab = categoryRowPrefab;
        _searchField = searchField;
        _gridButton = gridButton;
        _listButton = listButton;
        _gridScroll = gridScroll;
        _gridViewport = gridViewport;
        _gridContent = gridContent;
        _gridCellPrefab = gridCellPrefab;
        _listScroll = listScroll;
        _listViewport = listViewport;
        _listContent = listContent;
        _listRowPrefab = listRowPrefab;
        _resultIcon = resultIcon;
        _resultName = resultName;
        _starButton = starButton;
        _starImage = starImage;
        _timeText = timeText;
        _bookIcon = bookIcon;
        _workbenchText = workbenchText;
        _lightIcon = lightIcon;
        _skillsText = skillsText;
        _requiredHeader = requiredHeader;
        _ingredientContent = ingredientContent;
        _ingredientCardPrefab = ingredientCardPrefab;
        _craftButton = craftButton;
        _craftLabel = craftLabel;
    }

    public void ConfigureChrome(Canvas rootCanvas)
    {
        if (_windowDragHandler == null)
            Debug.LogError("[UICraftingWindow] Window drag handler not assigned.", this);

        _windowDragHandler?.Initialize(WindowRect, rootCanvas);

        Vector2 minSize = new(CraftingWindowLayout.MinWidth, CraftingWindowLayout.MinHeight);
        Vector2 maxSize = CraftingWindowLayout.GetMaxSize(rootCanvas);

        if (!TryGetComponent(out UIWindowResizeHandles resizeHandles))
        {
            Debug.LogError("[UICraftingWindow] UIWindowResizeHandles missing on window root.", this);
        }
        else
        {
            resizeHandles.Initialize(WindowRect, rootCanvas, minSize, maxSize);
        }

        if (WindowRect != null && rootCanvas != null)
            WindowRect.sizeDelta = CraftingWindowLayout.ClampSize(WindowRect.sizeDelta, rootCanvas);

        if (!TryGetComponent(out UIOverlayWindow _))
            Debug.LogError("[UICraftingWindow] UIOverlayWindow missing on window prefab root.", this);
    }

    public void Initialize(PlayerInventoryRuntime runtime, Action onClose)
    {
        Unbind();
        _runtime = runtime;
        _onClose = onClose;
        _favorites.Load();
        _bound = true;

        ApplyFonts();
        CacheRecipes();
        RebuildPool();
        HookControls(true);

        if (_lightIcon != null)
            _lightIcon.gameObject.SetActive(false);

        if (_requiredHeader != null)
            _requiredHeader.text = CraftingWindowLabels.RequiredItems;

        if (_outputsHeader != null)
            _outputsHeader.text = CraftingWindowLabels.Outputs;

        if (_qtyMaxLabel != null)
            _qtyMaxLabel.text = CraftingWindowLabels.Max;

        if (_searchField != null)
        {
            _searchField.text = string.Empty;
            if (_searchField.placeholder is TMP_Text placeholder)
            {
                DistUiFont.Apply(placeholder);
                placeholder.text = CraftingWindowLabels.SearchPlaceholder;
            }
        }

        _search = string.Empty;
        _selectedCategoryId = CraftingWindowLabels.CategoryAllId;
        _selected = null;
        _quantity = 1;
        CancelCraft();
        ApplyViewMode();
        Refresh();
    }

    public void Unbind()
    {
        CancelCraft();
        if (_bound)
            HookControls(false);

        _runtime = null;
        _pool = null;
        _onClose = null;
        _selected = null;
        _bound = false;
    }

    public void Refresh()
    {
        if (!_bound)
            return;

        RebuildPool();
        if (_craftRunning)
        {
            BindProgress();
            return;
        }

        ApplyFilter();
        BindCategories();
        RefreshVisibleRecipes();
        RefreshDetail();
    }

    void Update()
    {
        // Rule 6: tick only. TMP string alloc only when displayed second or label mode changes.
        if (!_craftRunning)
            return;

        float dt = WorldClock.DeltaGameMinutes();
        CharacterActionHost actionHost = ResolveActionHost();
        if (actionHost != null)
            dt *= actionHost.ActionTickScale;
        _craftElapsed += dt;
        BindProgress();
        BindCraftTimeDisplay(true);
        if (_craftElapsed >= _craftDuration)
            CompleteCraft();
    }

    void OnDestroy() => Unbind();

    void ApplyFonts()
    {
        DistUiFont.Apply(_headerTitle);
        DistUiFont.Apply(_resultName);
        DistUiFont.Apply(_timeText);
        DistUiFont.Apply(_workbenchText);
        DistUiFont.Apply(_skillsText);
        DistUiFont.Apply(_requiredHeader);
        DistUiFont.Apply(_craftLabel);
        DistUiFont.Apply(_outputsHeader);
        DistUiFont.Apply(_qtyMaxLabel);
        DistUiFont.Apply(_timeRequiredText);
        if (_searchField != null)
            DistUiFont.Apply(_searchField.textComponent);
        if (_quantityField != null)
        {
            DistUiFont.Apply(_quantityField.textComponent);
            if (_quantityField.placeholder is TMP_Text qtyPlaceholder)
                DistUiFont.Apply(qtyPlaceholder);
        }
    }

    void HookControls(bool bind)
    {
        if (bind)
        {
            if (_closeButton != null && GetComponentInChildren<UIWindowChromeBar>(true) == null)
                _closeButton.onClick.AddListener(OnCloseClicked);
            if (_gridButton != null)
                _gridButton.onClick.AddListener(OnGridClicked);
            if (_listButton != null)
                _listButton.onClick.AddListener(OnListClicked);
            if (_starButton != null)
                _starButton.onClick.AddListener(OnStarClicked);
            if (_craftButton != null)
                _craftButton.onClick.AddListener(OnCraftClicked);
            if (_qtyMinusButton != null)
                _qtyMinusButton.onClick.AddListener(OnQtyMinusClicked);
            if (_qtyPlusButton != null)
                _qtyPlusButton.onClick.AddListener(OnQtyPlusClicked);
            if (_qtyMaxButton != null)
                _qtyMaxButton.onClick.AddListener(OnQtyMaxClicked);
            if (_quantityField != null)
                _quantityField.onEndEdit.AddListener(OnQuantityEndEdit);
            if (_searchField != null)
                _searchField.onValueChanged.AddListener(OnSearchChanged);
            if (_gridScroll != null)
                _gridScroll.onValueChanged.AddListener(OnGridScrolled);
            if (_listScroll != null)
                _listScroll.onValueChanged.AddListener(OnListScrolled);
        }
        else
        {
            if (_closeButton != null && GetComponentInChildren<UIWindowChromeBar>(true) == null)
                _closeButton.onClick.RemoveListener(OnCloseClicked);
            if (_gridButton != null)
                _gridButton.onClick.RemoveListener(OnGridClicked);
            if (_listButton != null)
                _listButton.onClick.RemoveListener(OnListClicked);
            if (_starButton != null)
                _starButton.onClick.RemoveListener(OnStarClicked);
            if (_craftButton != null)
                _craftButton.onClick.RemoveListener(OnCraftClicked);
            if (_qtyMinusButton != null)
                _qtyMinusButton.onClick.RemoveListener(OnQtyMinusClicked);
            if (_qtyPlusButton != null)
                _qtyPlusButton.onClick.RemoveListener(OnQtyPlusClicked);
            if (_qtyMaxButton != null)
                _qtyMaxButton.onClick.RemoveListener(OnQtyMaxClicked);
            if (_quantityField != null)
                _quantityField.onEndEdit.RemoveListener(OnQuantityEndEdit);
            if (_searchField != null)
                _searchField.onValueChanged.RemoveListener(OnSearchChanged);
            if (_gridScroll != null)
                _gridScroll.onValueChanged.RemoveListener(OnGridScrolled);
            if (_listScroll != null)
                _listScroll.onValueChanged.RemoveListener(OnListScrolled);
        }
    }

    void CacheRecipes()
    {
        _allRecipes.Clear();
        _recipeNames.Clear();
        _categoryIds.Clear();

        List<RecipeData> all = GameplayData.GetAllRecipes();
        if (all != null)
        {
            for (int i = 0; i < all.Count; i++)
            {
                RecipeData recipe = all[i];
                if (recipe == null || string.IsNullOrEmpty(recipe.result) || recipe.is_uncraft)
                    continue;
                if (!string.IsNullOrEmpty(_toolFilter) &&
                    !CraftingService.RecipeUsesTool(recipe, _toolFilter))
                    continue;

                _allRecipes.Add(recipe);
                _recipeNames.Add(UITextPresenter.GetItemName(recipe.result) ?? string.Empty);
            }
        }

        _categoryIds.Add(CraftingWindowLabels.CategoryAllId);
        _categoryIds.Add(CraftingWindowLabels.CategoryFavouritesId);

        List<string> categories = GameplayData.GetRecipeCategories();
        if (categories == null)
            return;

        for (int i = 0; i < categories.Count; i++)
        {
            string id = categories[i];
            if (!string.IsNullOrEmpty(id))
                _categoryIds.Add(id);
        }
    }

    void RebuildPool()
    {
        InventorySession session = _runtime != null ? _runtime.Session : null;
        IReadOnlyList<InventoryContainer> sources = session != null
            ? session.GetSidebarContainers()
            : Array.Empty<InventoryContainer>();

        _pool = new CraftingMaterialPool(
            sources,
            _runtime != null ? _runtime.IsWorldLootContainer : null,
            PlayerInventoryHost.DefaultInstanceId);
    }

    void ApplyFilter()
    {
        _filtered.Clear();
        for (int i = 0; i < _allRecipes.Count; i++)
        {
            RecipeData recipe = _allRecipes[i];
            if (!MatchesCategory(recipe))
                continue;

            if (_search.Length > 0)
            {
                string name = i < _recipeNames.Count ? _recipeNames[i] : string.Empty;
                if (name.IndexOf(_search, StringComparison.OrdinalIgnoreCase) < 0)
                    continue;
            }

            _filtered.Add(recipe);
        }

        SortCraftableFirst();

        if (_selected != null && !_filtered.Contains(_selected))
            _selected = null;
    }

    void SortCraftableFirst()
    {
        if (_filtered.Count <= 1)
            return;

        _filtered.Sort(CompareCraftableFirst);
    }

    int CompareCraftableFirst(RecipeData a, RecipeData b)
    {
        bool aOk = IsRecipeCraftable(a);
        bool bOk = IsRecipeCraftable(b);
        if (aOk == bOk)
            return 0;
        return aOk ? -1 : 1;
    }

    bool IsRecipeCraftable(RecipeData recipe)
    {
        if (recipe == null || _pool == null)
            return false;

        if (!string.IsNullOrEmpty(RecipeKnowledge.GetFailureReason(recipe, _pool)))
            return false;

        return CraftingService.CanCraft(recipe, _pool);
    }

    bool MatchesCategory(RecipeData recipe)
    {
        if (_selectedCategoryId == CraftingWindowLabels.CategoryAllId)
            return true;

        if (_selectedCategoryId == CraftingWindowLabels.CategoryFavouritesId)
            return _favorites.Contains(RecipeKey(recipe));

        return recipe.category == _selectedCategoryId;
    }

    void BindCategories()
    {
        if (_categoryRowPrefab == null || _categoryContent == null)
            return;

        for (int i = 0; i < _categoryIds.Count; i++)
        {
            UICraftingCategoryRow row = GetPooled(_categoryPool, _categoryRowPrefab, _categoryContent, i);
            string id = _categoryIds[i];
            row.Bind(
                id,
                CraftingWindowLabels.GetCategoryName(id),
                id == _selectedCategoryId,
                OnCategorySelected);
        }

        HideUnused(_categoryPool, _categoryIds.Count);
    }

    void RefreshVisibleRecipes()
    {
        bool grid = _favorites.IsGridMode;
        if (_gridScroll != null)
            _gridScroll.gameObject.SetActive(grid);
        if (_listScroll != null)
            _listScroll.gameObject.SetActive(!grid);

        if (grid)
            RefreshGrid();
        else
            RefreshList();

        SyncViewToggleVisuals();
    }

    void RefreshGrid()
    {
        if (_gridCellPrefab == null || _gridContent == null || _gridViewport == null)
            return;

        Vector2 cell = ResolvePrefabSize(
            _gridCellPrefab.transform as RectTransform,
            CraftingWindowLayout.RecipeCellSize,
            CraftingWindowLayout.RecipeCellSize);

        float viewportW = Mathf.Max(1f, _gridViewport.rect.width);
        int columns = Mathf.Max(1, Mathf.FloorToInt(viewportW / cell.x));
        int rows = (_filtered.Count + columns - 1) / columns;
        _gridContent.sizeDelta = new Vector2(0f, rows * cell.y);

        float scrollY = Mathf.Max(0f, _gridContent.anchoredPosition.y);
        int firstRow = Mathf.Max(0, Mathf.FloorToInt(scrollY / cell.y) - 1);
        int visibleRows = Mathf.CeilToInt(_gridViewport.rect.height / cell.y)
            + (int)CraftingWindowLayout.VisibleRowBuffer
            + 1;
        int firstIndex = firstRow * columns;
        int lastIndex = Mathf.Min(_filtered.Count, firstIndex + visibleRows * columns);
        if (firstIndex >= lastIndex)
        {
            HideUnused(_gridPool, 0);
            return;
        }

        int poolIndex = 0;
        for (int i = firstIndex; i < lastIndex; i++)
        {
            UICraftingRecipeCell cellView = GetPooled(_gridPool, _gridCellPrefab, _gridContent, poolIndex);
            PlaceCell(cellView.transform as RectTransform, i, columns, cell);
            RecipeData recipe = _filtered[i];
            cellView.Bind(recipe, recipe == _selected, IsRecipeCraftable(recipe), OnRecipeSelected);
            poolIndex++;
        }

        HideUnused(_gridPool, poolIndex);
    }

    void RefreshList()
    {
        if (_listRowPrefab == null || _listContent == null || _listViewport == null)
            return;

        Vector2 rowSize = ResolvePrefabSize(
            _listRowPrefab.transform as RectTransform,
            _listViewport.rect.width,
            CraftingWindowLayout.RecipeListRowHeight);

        _listContent.sizeDelta = new Vector2(0f, _filtered.Count * rowSize.y);

        float scrollY = Mathf.Max(0f, _listContent.anchoredPosition.y);
        int firstIndex = Mathf.Max(0, Mathf.FloorToInt(scrollY / rowSize.y) - 1);
        int visible = Mathf.CeilToInt(_listViewport.rect.height / rowSize.y)
            + (int)CraftingWindowLayout.VisibleRowBuffer
            + 1;
        int lastIndex = Mathf.Min(_filtered.Count, firstIndex + visible);
        if (firstIndex >= lastIndex)
        {
            HideUnused(_listPool, 0);
            return;
        }

        int poolIndex = 0;
        for (int i = firstIndex; i < lastIndex; i++)
        {
            UICraftingRecipeListRow row = GetPooled(_listPool, _listRowPrefab, _listContent, poolIndex);
            PlaceListRow(row.transform as RectTransform, i, rowSize);
            RecipeData recipe = _filtered[i];
            row.Bind(recipe, recipe == _selected, IsRecipeCraftable(recipe), OnRecipeSelected);
            poolIndex++;
        }

        HideUnused(_listPool, poolIndex);
    }

    void RefreshDetail()
    {
        bool has = _selected != null;
        if (_resultIcon != null)
        {
            _resultIcon.enabled = has;
            _resultIcon.sprite = has ? ItemVisualPresenter.GetDisplayIcon(_selected.result) : null;
        }

        DistUiFont.Apply(_resultName);
        if (_resultName != null)
            _resultName.text = has ? UITextPresenter.GetItemName(_selected.result) : string.Empty;

        BindHeaderMeta(has);
        BindSkills(has);
        BindIngredients(has);
        BindOutputs(has);
        BindQuantityAndTime(has);
        BindCraftButton(has);
        BindTitle();
    }

    void BindHeaderMeta(bool has)
    {
        string key = has ? RecipeKey(_selected) : string.Empty;
        bool fav = has && _favorites.Contains(key);
        if (_starImage != null)
            _starImage.color = fav ? Color.white : new Color(1f, 1f, 1f, 0.35f);

        if (_starButton != null)
            _starButton.interactable = has;

        if (_timeText != null)
        {
            DistUiFont.Apply(_timeText);
            _timeText.gameObject.SetActive(has);
            if (has)
                _timeText.text = CraftingWindowLabels.FormatTimeMinutes(
                    CraftingService.GetCraftTimeMinutes(_selected, 1));
        }

        bool hasBooks = has && _selected.book_learn != null && _selected.book_learn.Count > 0;
        if (_bookIcon != null)
        {
            _bookIcon.gameObject.SetActive(hasBooks);
            if (hasBooks)
            {
                string knowledge = RecipeKnowledge.GetFailureReason(_selected, _pool);
                _bookIcon.color = string.IsNullOrEmpty(knowledge)
                    ? CraftingWindowLayout.SkillMetColor
                    : CraftingWindowLayout.SkillUnmetColor;
            }
        }

        InventoryContainer bench = FindWorkbench();
        if (_workbenchText != null)
        {
            DistUiFont.Apply(_workbenchText);
            bool show = bench?.Definition != null;
            _workbenchText.gameObject.SetActive(show);
            if (show)
                _workbenchText.text = UITextPresenter.GetContainerName(bench.Definition);
        }

        BindLightIcon();
    }

    void BindLightIcon()
    {
        if (_lightIcon == null)
            return;

        bool needsLight = _selected != null && CraftingLightGate.RequiresLight(_selected);
        _lightIcon.gameObject.SetActive(needsLight);
        if (!needsLight)
            return;

        bool met = CraftingLightGate.MeetsLight(_selected, _pool);
        _lightIcon.color = met
            ? CraftingWindowLayout.SkillMetColor
            : CraftingWindowLayout.SkillUnmetColor;
    }

    public void SetToolFilter(string toolId)
    {
        _toolFilter = string.IsNullOrEmpty(toolId) ? null : toolId;
        if (_runtime == null)
            return;
        CacheRecipes();
        ApplyFilter();
        Refresh();
    }

    void BindTitle()
    {
        DistUiFont.Apply(_headerTitle);
        if (_headerTitle == null)
            return;

        InventoryContainer bench = FindWorkbench();
        if (bench?.Definition != null)
            _headerTitle.text = CraftingWindowLabels.FormatTitleOn(
                UITextPresenter.GetContainerName(bench.Definition));
        else
            _headerTitle.text = CraftingWindowLabels.Title;
    }

    void BindSkills(bool has)
    {
        DistUiFont.Apply(_skillsText);
        if (_skillsText == null)
            return;

        if (!has)
        {
            _skillsText.text = string.Empty;
            return;
        }

        _skillsBuilder.Length = 0;
        AppendSkillLine(_selected.skill_used, _selected.difficulty);
        if (_selected.skills_required != null)
        {
            for (int i = 0; i < _selected.skills_required.Count; i++)
            {
                SkillReq req = _selected.skills_required[i];
                if (req == null || string.IsNullOrEmpty(req.skill))
                    continue;
                AppendSkillLine(req.skill, req.level);
            }
        }

        AppendKnowledgeLine();

        _skillsText.richText = true;
        _skillsText.text = _skillsBuilder.ToString();
    }

    void AppendKnowledgeLine()
    {
        bool hasAuto = _selected.autolearn;
        bool hasBooks = _selected.book_learn != null && _selected.book_learn.Count > 0;
        if (!hasAuto && !hasBooks)
            return;

        string knowledge = RecipeKnowledge.GetFailureReason(_selected, _pool);
        if (string.IsNullOrEmpty(knowledge))
            AppendColoredLine(CraftingWindowLabels.BookKnown, CraftingWindowLayout.SkillMetColor);
        else
            AppendColoredLine(knowledge, CraftingWindowLayout.SkillUnmetColor);
    }

    void AppendSkillLine(string skillId, int need)
    {
        if (string.IsNullOrEmpty(skillId))
            return;

        int have = GameplayData.Stats.GetSkillLevel(skillId);
        bool met = have >= need;
        string hex = ColorUtility.ToHtmlStringRGB(
            met ? CraftingWindowLayout.SkillMetColor : CraftingWindowLayout.SkillUnmetColor);
        string line = CraftingWindowLabels.FormatSkillLine(
            PlayerStatusLabels.GetSkillName(skillId),
            have,
            need);

        if (_skillsBuilder.Length > 0 && _skillsBuilder[_skillsBuilder.Length - 1] != '\n')
            _skillsBuilder.Append('\n');

        _skillsBuilder.Append("<color=#").Append(hex).Append('>').Append(line).Append("</color>");
    }

    void AppendColoredLine(string line, Color color)
    {
        if (string.IsNullOrEmpty(line))
            return;

        string hex = ColorUtility.ToHtmlStringRGB(color);
        if (_skillsBuilder.Length > 0 && _skillsBuilder[_skillsBuilder.Length - 1] != '\n')
            _skillsBuilder.Append('\n');

        _skillsBuilder.Append("<color=#").Append(hex).Append('>').Append(line).Append("</color>");
    }

    void BindIngredients(bool has)
    {
        if (_ingredientCardPrefab == null || _ingredientContent == null)
            return;

        if (!has)
        {
            HideUnused(_ingredientPool, 0);
            return;
        }

        int used = 0;
        if (_selected.components != null)
        {
            for (int i = 0; i < _selected.components.Count; i++)
            {
                ComponentSlot slot = _selected.components[i];
                int altIndex = GetIndex(_componentAltIndices, i);
                ComponentAlt alt = GetComponentAlt(slot, altIndex);
                FillAltIdsFromComponents(slot);
                int have = alt != null ? _pool.CountItem(alt.item) : 0;
                int need = alt != null ? alt.count * CraftQuantity : 0;
                UICraftingIngredientCard card = GetPooled(
                    _ingredientPool, _ingredientCardPrefab, _ingredientContent, used);
                card.Bind(
                    CraftingIngredientKind.Consume,
                    alt != null ? alt.item : string.Empty,
                    alt != null ? UITextPresenter.GetItemName(alt.item) : string.Empty,
                    have,
                    need,
                    0,
                    i,
                    _altIdBuffer,
                    slot?.alternatives != null && slot.alternatives.Count > 1,
                    OpenAltMenu,
                    OnAltDropped);
                used++;
            }
        }

        if (_selected.tools != null)
        {
            for (int i = 0; i < _selected.tools.Count; i++)
            {
                ToolSlot slot = _selected.tools[i];
                int altIndex = GetIndex(_toolAltIndices, i);
                ToolAlt alt = GetToolAlt(slot, altIndex);
                FillAltIdsFromTools(slot);
                bool fuel = alt != null && alt.charges > 0;
                int have = 0;
                int need = 1;
                if (alt != null)
                {
                    if (fuel)
                    {
                        have = _pool.CountToolCharges(alt.tool);
                        need = alt.charges * CraftQuantity;
                    }
                    else
                    {
                        have = _pool.CountItem(alt.tool) > 0 ? 1 : 0;
                    }
                }

                UICraftingIngredientCard card = GetPooled(
                    _ingredientPool, _ingredientCardPrefab, _ingredientContent, used);
                card.Bind(
                    fuel ? CraftingIngredientKind.Fuel : CraftingIngredientKind.Keep,
                    alt != null ? alt.tool : string.Empty,
                    alt != null ? UITextPresenter.GetItemName(alt.tool) : string.Empty,
                    have,
                    need,
                    0,
                    i,
                    _altIdBuffer,
                    slot?.alternatives != null && slot.alternatives.Count > 1,
                    OpenAltMenu,
                    OnAltDropped);
                used++;
            }
        }

        if (_selected.qualities_required != null)
        {
            for (int i = 0; i < _selected.qualities_required.Count; i++)
            {
                QualityEntry quality = _selected.qualities_required[i];
                if (quality == null || string.IsNullOrEmpty(quality.id))
                    continue;

                QualityVisualPresenter.FillItemIds(quality.id, quality.level, _altIdBuffer);
                int altIndex = GetIndex(_qualityAltIndices, i);
                string iconItemId = GetListId(_altIdBuffer, altIndex);
                int have = 0;
                if (PoolHasItem(iconItemId))
                    have = QualityVisualPresenter.GetItemQualityLevel(iconItemId, quality.id);
                UICraftingIngredientCard card = GetPooled(
                    _ingredientPool, _ingredientCardPrefab, _ingredientContent, used);
                card.Bind(
                    CraftingIngredientKind.Quality,
                    iconItemId,
                    UITextPresenter.GetQuality(quality.id),
                    have,
                    quality.level,
                    quality.level,
                    i,
                    _altIdBuffer,
                    _altIdBuffer.Count > 1,
                    OpenAltMenu,
                    OnAltDropped);
                used++;
            }
        }

        HideUnused(_ingredientPool, used);
    }

    void BindOutputs(bool has)
    {
        if (_ingredientCardPrefab == null || _outputContent == null)
            return;

        if (!has)
        {
            HideUnused(_outputPool, 0);
            return;
        }

        int used = 0;
        int qty = CraftQuantity;
        int resultCount = (_selected.result_count > 0 ? _selected.result_count : 1) * qty;
        UICraftingIngredientCard resultCard = GetPooled(
            _outputPool, _ingredientCardPrefab, _outputContent, used);
        resultCard.BindOutput(
            _selected.result,
            UITextPresenter.GetItemName(_selected.result),
            resultCount);
        used++;

        if (_selected.byproducts != null)
        {
            for (int i = 0; i < _selected.byproducts.Count; i++)
            {
                Byproduct bp = _selected.byproducts[i];
                if (bp == null || string.IsNullOrEmpty(bp.item) || bp.count <= 0)
                    continue;

                UICraftingIngredientCard card = GetPooled(
                    _outputPool, _ingredientCardPrefab, _outputContent, used);
                card.BindOutput(
                    bp.item,
                    UITextPresenter.GetItemName(bp.item),
                    bp.count * qty);
                used++;
            }
        }

        HideUnused(_outputPool, used);
    }

    void BindQuantityAndTime(bool has)
    {
        ClampQuantity();
        SyncQuantityField();
        SetQuantityInteractable(has && !_craftRunning);

        DistUiFont.Apply(_timeRequiredText);
        BindCraftTimeDisplay(has);

        BindProgress();
    }

    void BindProgress()
    {
        if (_progressFill == null)
            return;

        if (!_craftRunning || _craftDuration <= 0f)
        {
            _progressFill.fillAmount = _craftRunning ? 1f : 0f;
            return;
        }

        _progressFill.fillAmount = Mathf.Clamp01(_craftElapsed / _craftDuration);
    }

    void BindCraftTimeDisplay(bool has)
    {
        if (_timeRequiredText == null)
            return;

        _timeRequiredText.gameObject.SetActive(has);
        if (!has)
        {
            _lastDisplayedTimeSeconds = -1;
            _lastTimeWasRemaining = false;
            return;
        }

        bool remaining = _craftRunning;
        float gameMinutes;
        if (remaining)
            gameMinutes = Mathf.Max(0f, _craftDuration - _craftElapsed);
        else if (_selected != null)
            gameMinutes = CraftingService.GetCraftTimeMinutes(_selected, CraftQuantity);
        else
            gameMinutes = 0f;
        float displaySeconds = gameMinutes * CraftingWindowLayout.SecondsPerMinute;
        int ceiled = Mathf.Max(0, Mathf.CeilToInt(displaySeconds));
        if (remaining == _lastTimeWasRemaining && ceiled == _lastDisplayedTimeSeconds)
            return;

        _lastTimeWasRemaining = remaining;
        _lastDisplayedTimeSeconds = ceiled;
        _timeRequiredText.text = remaining
            ? CraftingWindowLabels.FormatTimeRemaining(displaySeconds)
            : CraftingWindowLabels.FormatTimeRequired(displaySeconds);
    }

    void BindCraftButton(bool has)
    {
        DistUiFont.Apply(_craftLabel);
        if (_craftButton == null)
            return;

        if (!has || _craftRunning)
        {
            _craftButton.interactable = false;
            if (_craftLabel != null)
                _craftLabel.text = CraftingWindowLabels.Craft;
            return;
        }

        string knowledge = RecipeKnowledge.GetFailureReason(_selected, _pool);
        bool can = string.IsNullOrEmpty(knowledge)
            && ResolveMaxCrafts() >= CraftQuantity;
        _craftButton.interactable = can;
        if (_craftLabel != null)
        {
            _craftLabel.text = can
                ? CraftingWindowLabels.Craft
                : (!string.IsNullOrEmpty(knowledge) ? knowledge : CraftingWindowLabels.CannotCraft);
        }
    }

    void OnCategorySelected(string categoryId)
    {
        if (_craftRunning || _selectedCategoryId == categoryId)
            return;

        _selectedCategoryId = categoryId;
        _selected = null;
        ApplyFilter();
        BindCategories();
        RefreshVisibleRecipes();
        RefreshDetail();
    }

    void OnRecipeSelected(RecipeData recipe)
    {
        if (_craftRunning || _selected == recipe)
            return;

        _selected = recipe;
        ResetAltIndices(recipe);
        _quantity = 1;
        RefreshVisibleRecipes();
        RefreshDetail();
    }

    void ResetAltIndices(RecipeData recipe)
    {
        _componentAltIndices.Clear();
        _toolAltIndices.Clear();
        _qualityAltIndices.Clear();
        if (recipe?.components != null)
        {
            for (int i = 0; i < recipe.components.Count; i++)
                _componentAltIndices.Add(ResolveDefaultComponent(recipe.components[i]));
        }

        if (recipe?.tools != null)
        {
            for (int i = 0; i < recipe.tools.Count; i++)
                _toolAltIndices.Add(ResolveDefaultTool(recipe.tools[i]));
        }

        if (recipe?.qualities_required != null)
        {
            for (int i = 0; i < recipe.qualities_required.Count; i++)
                _qualityAltIndices.Add(ResolveDefaultQuality(recipe.qualities_required[i]));
        }
    }

    int ResolveDefaultComponent(ComponentSlot slot)
    {
        if (slot?.alternatives == null)
            return 0;

        for (int i = 0; i < slot.alternatives.Count; i++)
        {
            ComponentAlt alt = slot.alternatives[i];
            if (alt == null || string.IsNullOrEmpty(alt.item))
                continue;
            if (_pool != null && _pool.CountItem(alt.item) >= alt.count)
                return i;
        }

        return 0;
    }

    int ResolveDefaultTool(ToolSlot slot)
    {
        if (slot?.alternatives == null)
            return 0;

        for (int i = 0; i < slot.alternatives.Count; i++)
        {
            ToolAlt alt = slot.alternatives[i];
            if (alt == null || string.IsNullOrEmpty(alt.tool))
                continue;
            if (_pool == null || _pool.CountItem(alt.tool) <= 0)
                continue;
            if (alt.charges > 0 && _pool.CountToolCharges(alt.tool) < alt.charges)
                continue;
            return i;
        }

        return 0;
    }

    int ResolveDefaultQuality(QualityEntry quality)
    {
        if (quality == null || string.IsNullOrEmpty(quality.id))
            return 0;

        QualityVisualPresenter.FillItemIds(quality.id, quality.level, _altIdBuffer);
        for (int i = 0; i < _altIdBuffer.Count; i++)
        {
            if (PoolHasItem(_altIdBuffer[i]))
                return i;
        }

        return 0;
    }

    void OpenAltMenu(int slotIndex, CraftingIngredientKind kind, Vector2 fallbackScreen)
    {
        if (_selected == null || _craftRunning)
            return;

        _altMenuEntries.Clear();
        Vector2 screen = ReadPointerScreen(fallbackScreen);

        if (kind == CraftingIngredientKind.Quality)
        {
            if (_selected.qualities_required == null ||
                slotIndex < 0 ||
                slotIndex >= _selected.qualities_required.Count)
                return;

            QualityEntry quality = _selected.qualities_required[slotIndex];
            QualityVisualPresenter.FillItemIds(quality?.id, quality != null ? quality.level : 0, _altIdBuffer);
            AddQualityAltEntries(quality, slotIndex, ownedOnly: true);
            AddQualityAltEntries(quality, slotIndex, ownedOnly: false);
        }
        else if (kind == CraftingIngredientKind.Keep || kind == CraftingIngredientKind.Fuel)
        {
            if (_selected.tools == null || slotIndex < 0 || slotIndex >= _selected.tools.Count)
                return;

            AddToolAltEntries(_selected.tools[slotIndex], slotIndex, ownedOnly: true);
            AddToolAltEntries(_selected.tools[slotIndex], slotIndex, ownedOnly: false);
        }
        else
        {
            if (_selected.components == null || slotIndex < 0 || slotIndex >= _selected.components.Count)
                return;

            AddComponentAltEntries(_selected.components[slotIndex], slotIndex, ownedOnly: true);
            AddComponentAltEntries(_selected.components[slotIndex], slotIndex, ownedOnly: false);
        }

        if (_altMenuEntries.Count == 0)
            return;

        UIContextMenuHost.TryShow(new ContextMenuModel(_altMenuEntries), screen);
    }

    void AddComponentAltEntries(ComponentSlot slot, int slotIndex, bool ownedOnly)
    {
        if (slot?.alternatives == null)
            return;

        for (int i = 0; i < slot.alternatives.Count; i++)
        {
            ComponentAlt alt = slot.alternatives[i];
            if (alt == null || string.IsNullOrEmpty(alt.item))
                continue;

            bool owned = PoolHasItem(alt.item);
            if (owned != ownedOnly)
                continue;

            int capturedSlot = slotIndex;
            int capturedAlt = i;
            _altMenuEntries.Add(ContextMenuEntry.Leaf(
                "craft-comp-" + slotIndex + "-" + i,
                UITextPresenter.GetItemName(alt.item),
                new CraftingAltSelectAction(
                    index => SetComponentAlt(capturedSlot, index),
                    capturedAlt,
                    owned),
                ItemVisualPresenter.GetDisplayIcon(alt.item)));
        }
    }

    void AddToolAltEntries(ToolSlot slot, int slotIndex, bool ownedOnly)
    {
        if (slot?.alternatives == null)
            return;

        for (int i = 0; i < slot.alternatives.Count; i++)
        {
            ToolAlt alt = slot.alternatives[i];
            if (alt == null || string.IsNullOrEmpty(alt.tool))
                continue;

            bool owned = PoolHasItem(alt.tool);
            if (owned != ownedOnly)
                continue;

            int capturedSlot = slotIndex;
            int capturedAlt = i;
            _altMenuEntries.Add(ContextMenuEntry.Leaf(
                "craft-tool-" + slotIndex + "-" + i,
                UITextPresenter.GetItemName(alt.tool),
                new CraftingAltSelectAction(
                    index => SetToolAlt(capturedSlot, index),
                    capturedAlt,
                    owned),
                ItemVisualPresenter.GetDisplayIcon(alt.tool)));
        }
    }

    void AddQualityAltEntries(QualityEntry quality, int slotIndex, bool ownedOnly)
    {
        if (quality == null || string.IsNullOrEmpty(quality.id))
            return;

        for (int i = 0; i < _altIdBuffer.Count; i++)
        {
            string itemId = _altIdBuffer[i];
            if (string.IsNullOrEmpty(itemId))
                continue;

            bool owned = PoolHasItem(itemId);
            if (owned != ownedOnly)
                continue;

            int capturedSlot = slotIndex;
            int capturedAlt = i;
            int level = QualityVisualPresenter.GetItemQualityLevel(itemId, quality.id);
            _altMenuEntries.Add(ContextMenuEntry.Leaf(
                "craft-quality-" + slotIndex + "-" + i,
                CraftingWindowLabels.FormatQualityAlt(UITextPresenter.GetItemName(itemId), level),
                new CraftingAltSelectAction(
                    index => SetQualityAlt(capturedSlot, index),
                    capturedAlt,
                    owned),
                ItemVisualPresenter.GetDisplayIcon(itemId)));
        }
    }

    bool PoolHasItem(string itemId)
    {
        return _pool != null && !string.IsNullOrEmpty(itemId) && _pool.CountItem(itemId) > 0;
    }

    void OnAltDropped(int slotIndex, CraftingIngredientKind kind, string itemId)
    {
        if (_selected == null || _craftRunning || string.IsNullOrEmpty(itemId))
            return;

        if (kind == CraftingIngredientKind.Quality)
        {
            QualityEntry quality = _selected.qualities_required != null &&
                slotIndex >= 0 &&
                slotIndex < _selected.qualities_required.Count
                ? _selected.qualities_required[slotIndex]
                : null;
            QualityVisualPresenter.FillItemIds(quality?.id, quality != null ? quality.level : 0, _altIdBuffer);
            for (int i = 0; i < _altIdBuffer.Count; i++)
            {
                if (_altIdBuffer[i] == itemId)
                {
                    SetQualityAlt(slotIndex, i);
                    return;
                }
            }
        }
        else if (kind == CraftingIngredientKind.Keep || kind == CraftingIngredientKind.Fuel)
        {
            ToolSlot slot = _selected.tools != null && slotIndex < _selected.tools.Count
                ? _selected.tools[slotIndex]
                : null;
            if (slot?.alternatives == null)
                return;

            for (int i = 0; i < slot.alternatives.Count; i++)
            {
                if (slot.alternatives[i]?.tool == itemId)
                {
                    SetToolAlt(slotIndex, i);
                    return;
                }
            }
        }
        else
        {
            ComponentSlot slot = _selected.components != null && slotIndex < _selected.components.Count
                ? _selected.components[slotIndex]
                : null;
            if (slot?.alternatives == null)
                return;

            for (int i = 0; i < slot.alternatives.Count; i++)
            {
                if (slot.alternatives[i]?.item == itemId)
                {
                    SetComponentAlt(slotIndex, i);
                    return;
                }
            }
        }
    }

    void SetComponentAlt(int slotIndex, int altIndex)
    {
        EnsureCount(_componentAltIndices, slotIndex + 1);
        _componentAltIndices[slotIndex] = altIndex;
        RefreshDetail();
    }

    void SetToolAlt(int slotIndex, int altIndex)
    {
        EnsureCount(_toolAltIndices, slotIndex + 1);
        _toolAltIndices[slotIndex] = altIndex;
        RefreshDetail();
    }

    void SetQualityAlt(int slotIndex, int altIndex)
    {
        EnsureCount(_qualityAltIndices, slotIndex + 1);
        _qualityAltIndices[slotIndex] = altIndex;
        RefreshDetail();
    }

    void OnCloseClicked() => _onClose?.Invoke();

    void OnGridClicked()
    {
        _favorites.SetGridMode(true);
        ApplyViewMode();
        RefreshVisibleRecipes();
    }

    void OnListClicked()
    {
        _favorites.SetGridMode(false);
        ApplyViewMode();
        RefreshVisibleRecipes();
    }

    void OnStarClicked()
    {
        if (_selected == null)
            return;

        _favorites.Toggle(RecipeKey(_selected));
        if (_selectedCategoryId == CraftingWindowLabels.CategoryFavouritesId)
        {
            ApplyFilter();
            RefreshVisibleRecipes();
        }

        RefreshDetail();
    }

    void OnCraftClicked()
    {
        if (_craftRunning || _selected == null || _runtime?.Session == null || _pool == null)
            return;

        string knowledge = RecipeKnowledge.GetFailureReason(_selected, _pool);
        int qty = CraftQuantity;
        if (!string.IsNullOrEmpty(knowledge) || ResolveMaxCrafts() < qty)
            return;

        CharacterActionHost host = ResolveActionHost();
        if (host != null)
        {
            // Craft는 예약 화이트리스트 — busy면 큐, idle면 즉시 Begin.
            host.TryEnqueue(CharacterActionKind.Craft, TryStartCraftNow);
            return;
        }

        TryStartCraftNow();
    }

    bool TryStartCraftNow()
    {
        if (_craftRunning || _selected == null || _runtime?.Session == null || _pool == null)
            return false;

        string knowledge = RecipeKnowledge.GetFailureReason(_selected, _pool);
        int qty = CraftQuantity;
        if (!string.IsNullOrEmpty(knowledge) || ResolveMaxCrafts() < qty)
            return false;

        _pendingCraftQuantity = qty;
        _craftDuration = CraftingService.GetCraftTimeMinutes(_selected, qty);
        _craftElapsed = 0f;
        _craftRunning = true;
        SetQuantityInteractable(false);
        BindCraftButton(true);
        BindProgress();
        BindCraftTimeDisplay(true);

        if (_craftDuration <= 0f)
            CompleteCraft();
        return true;
    }

    CharacterActionHost ResolveActionHost()
    {
        PlayerInventoryRuntime runtime = _runtime != null ? _runtime : PlayerInventoryRuntime.Active;
        return runtime != null ? runtime.gameObject.GetBodyComponent<CharacterActionHost>() : null;
    }

    int CraftQuantity => Mathf.Max(1, _quantity);

    int ResolveMaxCrafts()
    {
        if (_selected == null || _pool == null)
            return 0;

        if (!string.IsNullOrEmpty(RecipeKnowledge.GetFailureReason(_selected, _pool)))
            return 0;

        return CraftingService.GetMaxCraftCount(
            _selected,
            _pool,
            _componentAltIndices,
            _toolAltIndices,
            CraftingWindowLayout.MaxCraftQuantity);
    }

    void ClampQuantity()
    {
        int max = ResolveMaxCrafts();
        if (max <= 0)
            _quantity = 1;
        else
            _quantity = Mathf.Clamp(_quantity, 1, max);
    }

    void SyncQuantityField()
    {
        if (_quantityField == null || _syncingQuantity)
            return;

        _syncingQuantity = true;
        _quantityField.text = CraftQuantity.ToString();
        _syncingQuantity = false;
    }

    void SetQuantityInteractable(bool interactable)
    {
        if (_qtyMinusButton != null)
            _qtyMinusButton.interactable = interactable;
        if (_qtyPlusButton != null)
            _qtyPlusButton.interactable = interactable;
        if (_qtyMaxButton != null)
            _qtyMaxButton.interactable = interactable;
        if (_quantityField != null)
            _quantityField.interactable = interactable;
    }

    void OnQtyMinusClicked() => ApplyQuantity(CraftQuantity - 1);

    void OnQtyPlusClicked() => ApplyQuantity(CraftQuantity + 1);

    void OnQtyMaxClicked()
    {
        int max = ResolveMaxCrafts();
        ApplyQuantity(max > 0 ? max : 1);
    }

    void OnQuantityEndEdit(string value)
    {
        if (_syncingQuantity || _craftRunning)
            return;

        if (!int.TryParse(value, out int parsed))
            parsed = 1;

        ApplyQuantity(parsed);
    }

    void ApplyQuantity(int value)
    {
        if (_craftRunning)
            return;

        _quantity = value;
        ClampQuantity();
        BindIngredients(_selected != null);
        BindOutputs(_selected != null);
        BindQuantityAndTime(_selected != null);
        BindCraftButton(_selected != null);
    }

    public void CancelRunningCraft() => CancelCraft();

    void CancelCraft()
    {
        _craftRunning = false;
        _craftElapsed = 0f;
        _craftDuration = 0f;
        _pendingCraftQuantity = 0;
        _lastDisplayedTimeSeconds = -1;
        _lastTimeWasRemaining = false;
        if (_progressFill != null)
            _progressFill.fillAmount = 0f;
    }

    void CompleteCraft()
    {
        if (!_craftRunning)
            return;

        int qty = _pendingCraftQuantity;
        RecipeData recipe = _selected;
        CancelCraft();

        if (recipe != null && _pool != null && _runtime?.Session != null && qty > 0)
        {
            CraftingService.TryCraftMany(
                recipe,
                _pool,
                _runtime.Session,
                _componentAltIndices,
                _toolAltIndices,
                qty);
        }

        _quantity = 1;
        RebuildPool();
        ResetAltIndices(_selected);
        ApplyFilter();
        RefreshVisibleRecipes();
        RefreshDetail();
    }

    void OnSearchChanged(string value)
    {
        _search = value ?? string.Empty;
        ApplyFilter();
        RefreshVisibleRecipes();
        RefreshDetail();
    }

    void OnGridScrolled(Vector2 _) => RefreshGrid();

    void OnListScrolled(Vector2 _) => RefreshList();

    void ApplyViewMode()
    {
        bool grid = _favorites.IsGridMode;
        if (_gridScroll != null)
            _gridScroll.gameObject.SetActive(grid);
        if (_listScroll != null)
            _listScroll.gameObject.SetActive(!grid);
        SyncViewToggleVisuals();
    }

    void SyncViewToggleVisuals()
    {
        bool grid = _favorites.IsGridMode;
        SetToggleColor(_gridButton, grid);
        SetToggleColor(_listButton, !grid);
    }

    static void SetToggleColor(Button button, bool active)
    {
        if (button == null)
            return;

        Image image = button.targetGraphic as Image;
        if (image != null)
            image.color = active ? CraftingWindowLayout.SelectedColor : CraftingWindowLayout.ButtonColor;
    }

    InventoryContainer FindWorkbench()
    {
        InventorySession session = _runtime != null ? _runtime.Session : null;
        if (session == null || _runtime == null)
            return null;

        IReadOnlyList<InventoryContainer> sidebar = session.GetSidebarContainers();
        for (int i = 0; i < sidebar.Count; i++)
        {
            InventoryContainer container = sidebar[i];
            if (container == null)
                continue;
            if (container.InstanceId == PlayerInventoryHost.DefaultInstanceId)
                continue;
            if (container.InstanceId == FloorLootHost.DefaultInstanceId)
                continue;
            if (_runtime.IsWorldLootContainer(container.InstanceId))
                return container;
        }

        return null;
    }

    void FillAltIdsFromComponents(ComponentSlot slot)
    {
        _altIdBuffer.Clear();
        if (slot?.alternatives == null)
            return;

        for (int i = 0; i < slot.alternatives.Count; i++)
        {
            string id = slot.alternatives[i]?.item;
            if (!string.IsNullOrEmpty(id))
                _altIdBuffer.Add(id);
        }
    }

    void FillAltIdsFromTools(ToolSlot slot)
    {
        _altIdBuffer.Clear();
        if (slot?.alternatives == null)
            return;

        for (int i = 0; i < slot.alternatives.Count; i++)
        {
            string id = slot.alternatives[i]?.tool;
            if (!string.IsNullOrEmpty(id))
                _altIdBuffer.Add(id);
        }
    }

    static ComponentAlt GetComponentAlt(ComponentSlot slot, int index)
    {
        if (slot?.alternatives == null || index < 0 || index >= slot.alternatives.Count)
            return slot?.alternatives != null && slot.alternatives.Count > 0
                ? slot.alternatives[0]
                : null;
        return slot.alternatives[index];
    }

    static ToolAlt GetToolAlt(ToolSlot slot, int index)
    {
        if (slot?.alternatives == null || index < 0 || index >= slot.alternatives.Count)
            return slot?.alternatives != null && slot.alternatives.Count > 0
                ? slot.alternatives[0]
                : null;
        return slot.alternatives[index];
    }

    static int GetIndex(List<int> list, int index)
    {
        if (list == null || index < 0 || index >= list.Count)
            return 0;
        return list[index];
    }

    static string GetListId(List<string> list, int index)
    {
        if (list == null || list.Count == 0)
            return string.Empty;
        if (index < 0 || index >= list.Count)
            return list[0];
        return list[index] ?? string.Empty;
    }

    static void EnsureCount(List<int> list, int count)
    {
        while (list.Count < count)
            list.Add(0);
    }

    static string RecipeKey(RecipeData recipe)
    {
        if (recipe == null)
            return string.Empty;
        return !string.IsNullOrEmpty(recipe.id) ? recipe.id : recipe.result;
    }

    static Vector2 ReadPointerScreen(Vector2 fallback)
    {
        if (Mouse.current != null)
            return Mouse.current.position.ReadValue();
        return fallback;
    }

    static Vector2 ResolvePrefabSize(RectTransform prefab, float fallbackW, float fallbackH)
    {
        if (prefab != null)
        {
            Vector2 size = prefab.sizeDelta;
            if (size.x > 1f && size.y > 1f)
                return size;

            if (prefab.TryGetComponent(out LayoutElement layout))
            {
                float w = layout.preferredWidth > 1f ? layout.preferredWidth : fallbackW;
                float h = layout.preferredHeight > 1f ? layout.preferredHeight : fallbackH;
                return new Vector2(w, h);
            }
        }

        return new Vector2(fallbackW, fallbackH);
    }

    static void PlaceCell(RectTransform rect, int index, int columns, Vector2 cell)
    {
        if (rect == null)
            return;

        int row = index / columns;
        int col = index % columns;
        rect.anchorMin = new Vector2(0f, 1f);
        rect.anchorMax = new Vector2(0f, 1f);
        rect.pivot = new Vector2(0f, 1f);
        rect.sizeDelta = cell;
        rect.anchoredPosition = new Vector2(col * cell.x, -row * cell.y);
        rect.localScale = Vector3.one;
    }

    static void PlaceListRow(RectTransform rect, int index, Vector2 rowSize)
    {
        if (rect == null)
            return;

        rect.anchorMin = new Vector2(0f, 1f);
        rect.anchorMax = new Vector2(1f, 1f);
        rect.pivot = new Vector2(0.5f, 1f);
        rect.sizeDelta = new Vector2(0f, rowSize.y);
        rect.anchoredPosition = new Vector2(0f, -index * rowSize.y);
        rect.localScale = Vector3.one;
    }

    static T GetPooled<T>(List<T> pool, T prefab, Transform parent, int index) where T : Component
    {
        while (pool.Count <= index)
        {
            T instance = Instantiate(prefab, parent);
            instance.gameObject.SetActive(true);
            pool.Add(instance);
        }

        T item = pool[index];
        if (item.transform.parent != parent)
            item.transform.SetParent(parent, false);

        if (!item.gameObject.activeSelf)
            item.gameObject.SetActive(true);

        return item;
    }

    static void HideUnused<T>(List<T> pool, int used) where T : Component
    {
        for (int i = used; i < pool.Count; i++)
        {
            if (pool[i] != null && pool[i].gameObject.activeSelf)
                pool[i].gameObject.SetActive(false);
        }
    }
}
