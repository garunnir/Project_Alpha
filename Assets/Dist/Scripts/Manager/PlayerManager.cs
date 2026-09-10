// ============================================================
// PlayerManager — 조종 타겟을 관리하는 매니저
// ============================================================
using UnityEngine;
using Sirenix.OdinInspector;

[InfoBox("플레이어 컨트롤러를 위한 매니저. 플레이어 변경/플레이어 탈것 탑승 등의 확장 가능성이 있어서 사용.", InfoMessageType.Info)]
public class PlayerManager : MonoBehaviour
{
    [SerializeField, Required, ValidateInput(nameof(HasInitialControllable), "IPlayControllable을 구현한 컴포넌트를 할당해야 합니다.")]
    private MonoBehaviour _initialControllable;
    [SerializeField] private CameraFollowTargetDriver _cameraFollowDriver;
    [SerializeField] private PlayerPossessedInputHost _possessedInput;

    private IPlayControllable _playControllable;

    void Awake()
    {
        if (_possessedInput == null)
            TryGetComponent(out _possessedInput);
        if (_possessedInput == null)
            _possessedInput = GetComponentInChildren<PlayerPossessedInputHost>(true);
    }

    void Start(){
        _playControllable = _initialControllable as IPlayControllable;

        if (_playControllable == null)
            _playControllable = FindFirstPlayControllable(includeInactive: true);
        if (_cameraFollowDriver == null)
            _cameraFollowDriver = FindFirstObjectByType<CameraFollowTargetDriver>(FindObjectsInactive.Include);
        if (_cameraFollowDriver == null)
            _cameraFollowDriver = CreateCameraDriver();
        EnsureCameraZoomController(_cameraFollowDriver);
        ChangeControllTarget(_playControllable);
    }

    void Update()
    {
        PlayerPossessSession.EnsureBodyActive();
        if (_possessedInput != null)
            PlayerSightVisionBinder.Sync(_possessedInput);
    }

    public void Possess(GameObject body)
    {
        if (_possessedInput == null)
            Awake();
        if (_possessedInput == null)
        {
            Debug.LogError("[PlayerManager] PlayerPossessedInputHost is required to possess a spawned body.", this);
            return;
        }

        _possessedInput.Bind(body);
        ChangeControllTarget(_possessedInput);
    }

    private bool HasInitialControllable(MonoBehaviour behaviour) => behaviour is IPlayControllable;

    private static IPlayControllable FindFirstPlayControllable(bool includeInactive)
    {
        var behaviours = FindObjectsByType<MonoBehaviour>(
            includeInactive ? FindObjectsInactive.Include : FindObjectsInactive.Exclude,
            FindObjectsSortMode.None);

        foreach (var b in behaviours)
        {
            if (b is IPlayControllable controllable)
                return controllable;
        }

        return null;
    }
    public void ChangeControllTarget(IPlayControllable controllable)
    {
        _playControllable?.SetControlEnabled(false);
        _playControllable = controllable;
        _playControllable?.SetControlEnabled(true);
        UpdateCameraTarget(_playControllable);
    }

    private void UpdateCameraTarget(IPlayControllable controllable)
    {
        if (_cameraFollowDriver == null)
            return;

        if (controllable is PlayerPossessedInputHost host && host.BodyTransform != null)
        {
            _cameraFollowDriver.SetTarget(host.BodyTransform, host.BodyState);
            return;
        }

        if (controllable is Component component)
        {
            CharacterState state = component.GetComponent<CharacterState>();
            _cameraFollowDriver.SetTarget(component.transform, state);
            return;
        }

        _cameraFollowDriver.SetTarget(null, null);
    }

    private static CameraFollowTargetDriver CreateCameraDriver()
    {
        GameObject go = new GameObject("GameplayCinemachineCamera");
        go.AddComponent<CameraZoomController>();
        return go.AddComponent<CameraFollowTargetDriver>();
    }

    private static void EnsureCameraZoomController(CameraFollowTargetDriver driver)
    {
        if (driver == null)
            return;

        if (driver.GetComponent<CameraZoomController>() == null)
            driver.gameObject.AddComponent<CameraZoomController>();
    }
}

