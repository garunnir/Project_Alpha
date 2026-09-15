// ============================================================
// CharacterRuntimeDebugWindow — Play 모드 캐릭터 런타임 상태 Odin 디버그 창
// ============================================================

using System.Collections.Generic;
using Sirenix.OdinInspector;
using Sirenix.OdinInspector.Editor;
using UnityEditor;
using UnityEngine;

public sealed class CharacterRuntimeDebugWindow : OdinEditorWindow
{
    const double RepaintIntervalSeconds = 1.0d;
    const float WindowMinWidth = 560f;
    const float WindowMinHeight = 640f;
    const float TargetDropdownWidthFraction = 0.72f;
    const int PingButtonWidth = 52;
    const int FocusButtonWidth = 64;

    [MenuItem("Tools/Character Runtime Debug")]
    static void Open() => GetWindow<CharacterRuntimeDebugWindow>("Character Runtime Debug");

    [SerializeField, HideInInspector]
    CharacterRuntimeDebugModel _model = new CharacterRuntimeDebugModel();

    [SerializeField, HideInInspector]
    int _selectedInstanceId;

    CharacterBodyHost _selectedHost;
    double _nextRepaintTime;
    int _lastLiveHostCount = -1;
    readonly List<CharacterBodyHost> _liveHosts = new List<CharacterBodyHost>(16);

    protected override void OnEnable()
    {
        base.OnEnable();
        minSize = new Vector2(WindowMinWidth, WindowMinHeight);
        if (_model == null)
            _model = new CharacterRuntimeDebugModel();

        EditorApplication.playModeStateChanged += OnPlayModeChanged;
        EditorApplication.update += OnEditorUpdate;
        Selection.selectionChanged += OnSelectionChanged;
        TryResolveSelectionOrFallback();
    }

    protected override void OnDisable()
    {
        EditorApplication.playModeStateChanged -= OnPlayModeChanged;
        EditorApplication.update -= OnEditorUpdate;
        Selection.selectionChanged -= OnSelectionChanged;
        base.OnDisable();
    }

    void OnPlayModeChanged(PlayModeStateChange state)
    {
        if (state == PlayModeStateChange.EnteredPlayMode)
            TryResolveSelectionOrFallback();
        else if (state == PlayModeStateChange.ExitingPlayMode
                 || state == PlayModeStateChange.EnteredEditMode)
            SetSelectedHost(null);

        Repaint();
    }

    void OnEditorUpdate()
    {
        if (!Application.isPlaying)
            return;

        RebuildLiveHosts();
        if (_selectedHost == null)
            SyncFromLiveHostsFallback();
        else if (!IsHostLive(_selectedHost))
            SyncFromLiveHostsFallback();
        else if (_model != null && _model.BodyHost != _selectedHost)
            _model.Bind(_selectedHost);

        if (EditorGUIUtility.editingTextField)
            return;

        int liveCount = _liveHosts.Count;
        bool liveCountChanged = liveCount != _lastLiveHostCount;
        if (liveCountChanged)
            _lastLiveHostCount = liveCount;

        double now = EditorApplication.timeSinceStartup;
        bool intervalElapsed = now >= _nextRepaintTime;
        if (!liveCountChanged && !intervalElapsed)
            return;

        if (intervalElapsed)
            _nextRepaintTime = now + RepaintIntervalSeconds;
        Repaint();
    }

    void OnSelectionChanged()
    {
        if (!Application.isPlaying)
            return;
        SyncFromHierarchySelection();
    }

    string HeaderTitle => "Character Runtime Debug";

    string HeaderSubtitle =>
        _selectedHost != null ? ResolveLabel(_selectedHost, -1) : "No live CharacterBodyHost";

    bool HasBoundHost => _selectedHost != null;

    bool ShowEditModeWarning => !Application.isPlaying;

    [Title("$HeaderTitle", "$HeaderSubtitle")]
    [InfoBox(
        "Play mode only. Edit writes are disabled outside Play.",
        SdfIconType.ExclamationTriangleFill,
        nameof(ShowEditModeWarning))]
    [ShowInInspector, HideLabel]
    [DisplayAsString(EnableRichText = true, Overflow = false)]
    [GUIColor("@Application.isPlaying ? \"white\" : \"orange\"")]
    [PropertyOrder(-20)]
    string LiveHostStatus
    {
        get
        {
            if (!Application.isPlaying)
                return "<b>Edit mode</b>  —  writes disabled";

            RebuildLiveHosts();
            bool canWrite = _model != null && _model.CanWrite;
            string write = canWrite
                ? "<color=#88dd88><b>Writable</b></color>"
                : "<color=#ffcc66><b>Read-only</b></color>";
            return "Live hosts <b>" + _liveHosts.Count + "</b>  ·  BodyHost.Active "
                   + CharacterBodyHost.ActiveCount + "  ·  " + write;
        }
    }

    [HorizontalGroup("Pick", Width = TargetDropdownWidthFraction)]
    [ShowInInspector]
    [ValueDropdown(nameof(BuildLiveHostDropdown))]
    [LabelText("Target", SdfIconType.PeopleFill)]
    [PropertyOrder(-10)]
    int SelectedHostInstanceId
    {
        get => _selectedInstanceId;
        set
        {
            if (_selectedInstanceId == value && _selectedHost != null)
                return;
            _selectedInstanceId = value;
            SetSelectedHost(FindHostByInstanceId(value));
        }
    }

    [HorizontalGroup("Pick", Width = PingButtonWidth)]
    [Button(SdfIconType.GeoAltFill, "Ping")]
    [EnableIf(nameof(HasBoundHost))]
    [PropertyOrder(-9)]
    void PingBoundHost()
    {
        if (_selectedHost == null)
            return;
        EditorGUIUtility.PingObject(_selectedHost.gameObject);
    }

    [HorizontalGroup("Pick", Width = FocusButtonWidth)]
    [Button(SdfIconType.BoxArrowUpRight, "Focus")]
    [EnableIf(nameof(HasBoundHost))]
    [PropertyOrder(-8)]
    void FocusBoundHost()
    {
        if (_selectedHost == null)
            return;
        Selection.activeGameObject = _selectedHost.gameObject;
        EditorGUIUtility.PingObject(_selectedHost.gameObject);
    }

    [ShowInInspector, HideLabel, InlineProperty]
    [HideReferenceObjectPicker]
    [PropertyOrder(0)]
    CharacterRuntimeDebugModel Model
    {
        get
        {
            if (_model == null)
                _model = new CharacterRuntimeDebugModel();
            return _model;
        }
        set
        {
            if (value != null)
                _model = value;
        }
    }

    ValueDropdownList<int> BuildLiveHostDropdown()
    {
        var list = new ValueDropdownList<int>();
        RebuildLiveHosts();
        for (int i = 0; i < _liveHosts.Count; i++)
        {
            CharacterBodyHost host = _liveHosts[i];
            if (host == null)
                continue;
            // Labels must be unique — Odin collapses duplicate dropdown labels.
            list.Add(ResolveLabel(host, i), host.GetInstanceID());
        }

        return list;
    }

    void TryResolveSelectionOrFallback()
    {
        if (!Application.isPlaying)
            return;
        if (!SyncFromHierarchySelection())
            SyncFromLiveHostsFallback();
    }

    bool SyncFromHierarchySelection()
    {
        GameObject go = Selection.activeGameObject;
        if (go == null)
            return false;

        CharacterBodyHost host = go.GetBodyModule<CharacterBodyHost>();
        if (host == null)
            return false;

        SetSelectedHost(host);
        Repaint();
        return true;
    }

    void SyncFromLiveHostsFallback()
    {
        RebuildLiveHosts();
        if (_liveHosts.Count <= 0)
        {
            SetSelectedHost(null);
            return;
        }

        if (_selectedHost != null && IsHostLive(_selectedHost))
        {
            _model.Bind(_selectedHost);
            return;
        }

        CharacterBodyHost match = FindHostByInstanceId(_selectedInstanceId);
        if (match != null)
        {
            SetSelectedHost(match);
            return;
        }

        SetSelectedHost(_liveHosts[0]);
    }

    void SetSelectedHost(CharacterBodyHost host)
    {
        _selectedHost = host;
        _selectedInstanceId = host != null ? host.GetInstanceID() : 0;
        if (_model == null)
            _model = new CharacterRuntimeDebugModel();
        _model.Bind(host);
    }

    void RebuildLiveHosts()
    {
        _liveHosts.Clear();
        if (!Application.isPlaying)
            return;

        for (int i = 0; i < CharacterBodyHost.ActiveCount; i++)
        {
            CharacterBodyHost host = CharacterBodyHost.GetActive(i);
            AddLiveHost(host);
        }

        _liveHosts.Sort(CompareHosts);
    }

    void AddLiveHost(CharacterBodyHost host)
    {
        if (host == null || !host.isActiveAndEnabled)
            return;
        for (int i = 0; i < _liveHosts.Count; i++)
        {
            if (_liveHosts[i] == host)
                return;
        }

        _liveHosts.Add(host);
    }

    bool IsHostLive(CharacterBodyHost host)
    {
        if (host == null)
            return false;
        for (int i = 0; i < _liveHosts.Count; i++)
        {
            if (_liveHosts[i] == host)
                return true;
        }

        return false;
    }

    CharacterBodyHost FindHostByInstanceId(int instanceId)
    {
        if (instanceId == 0)
            return null;

        RebuildLiveHosts();
        for (int i = 0; i < _liveHosts.Count; i++)
        {
            CharacterBodyHost host = _liveHosts[i];
            if (host != null && host.GetInstanceID() == instanceId)
                return host;
        }

        return null;
    }

    static int CompareHosts(CharacterBodyHost a, CharacterBodyHost b)
    {
        bool aPossessed = IsPossessed(a);
        bool bPossessed = IsPossessed(b);
        if (aPossessed != bPossessed)
            return aPossessed ? -1 : 1;
        return a.GetInstanceID().CompareTo(b.GetInstanceID());
    }

    static bool IsPossessed(CharacterBodyHost host)
    {
        return host != null
               && host.BodyRefs != null
               && host.BodyRefs.Motor != null
               && host.BodyRefs.Motor.IsPossessed;
    }

    static string ResolveLabel(CharacterBodyHost host, int index)
    {
        if (host == null)
            return "(null)";

        string prefix = index >= 0 ? (index + 1) + ". " : "";
        string possessed = IsPossessed(host) ? " [P]" : "";
        string display = host.name;
        CharacterAppearanceHost appearance = host.BodyRefs != null
            ? host.BodyRefs.Appearance
            : CharacterBodyResolve.GetModule<CharacterAppearanceHost>(host);
        if (appearance != null)
        {
            string name = appearance.ResolveDisplayName();
            if (!string.IsNullOrEmpty(name))
                display = name + " — " + host.name;
        }

        // Instance id keeps Odin ValueDropdown entries unique when GO names match.
        return prefix + display + possessed + "  #" + host.GetInstanceID();
    }
}
