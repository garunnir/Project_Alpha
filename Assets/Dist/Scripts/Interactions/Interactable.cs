using UnityEngine;
namespace Interactions
{
public interface IInteractable
{
    Transform InteractTransform { get; } // UI용 위치 등
    bool CanInteract(GameObject interactor);
    void Interact(GameObject interactor);
    void OnFocus(GameObject interactor);   // 조준 시작
    void OnUnfocus(GameObject interactor); // 조준 종료
}

public abstract class Interactable : MonoBehaviour, IInteractable
{
    [SerializeField] protected string displayName;
    [SerializeField] protected string hintText;   // "E 키: 문 열기" 이런 거
    [SerializeField] protected SpriteRenderer _outlineSpriteRenderer;

    public virtual Transform InteractTransform => transform;
    protected virtual void Awake()
    {
        _outlineSpriteRenderer= GetComponentInChildren<SpriteRenderer>();
    }
    public virtual bool CanInteract(GameObject interactor) => true;

    public abstract void Interact(GameObject interactor);

    public virtual void OnFocus(GameObject interactor)
    {
        UIEvents.RequestPopup(UIPopupType.InteractionHint, this);
        // 하이라이트, 아웃라인, UI 표시 등'
        if(_outlineSpriteRenderer!=null)
        _outlineSpriteRenderer.color = Color.green;
    }

    public virtual void OnUnfocus(GameObject interactor)
    {
        // 하이라이트 해제, UI 숨김 등
               UIEvents.RequestPopup(UIPopupType.none, this);
        if(_outlineSpriteRenderer!=null)
        _outlineSpriteRenderer.color = Color.white;
    }

    public string DisplayName => displayName;
    public string HintText => hintText;
}

}
