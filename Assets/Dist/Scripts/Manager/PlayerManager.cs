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

    private IPlayControllable _playControllable;

    void Start(){
        _playControllable = _initialControllable as IPlayControllable;

        if (_playControllable == null)
            _playControllable = FindFirstPlayControllable(includeInactive: true);
        ChangeControllTarget(_playControllable);
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
    }
}
