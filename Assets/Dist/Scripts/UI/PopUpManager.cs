using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Interactions;
namespace UIManagement
{
    public class PopUpManager : Singleton<PopUpManager>
{
    [SerializeField] private List<UI> popups; // 인스펙터에 드래그
    private Dictionary<System.Type, UI> _popupMap;
    [SerializeField] InteractionCommandUI _interactionCommandUI;

    private IInteractable _currentOwner;
            private void OnEnable()
    {
        UIEvents.OnPopupRequested += HandlePopupRequest;
        _interactionCommandUI.gameObject.SetActive(false);
    }

    private void OnDisable()
    {
        UIEvents.OnPopupRequested -= HandlePopupRequest;
    }

    private void HandlePopupRequest(UIPopupType type, object data)
    {
        ShowPopup(type, (Interactable)data);
    }
    
    public void ShowPopup(UIPopupType type, IInteractable data = null)
    {
        switch (type)
        {
            case UIPopupType.none:
                if(_currentOwner == data)
                _interactionCommandUI.Hide();
                break;
            case UIPopupType.InteractionHint:
                ShowInteractionHint(data);
                break;
        }
    }
    private void ShowInteractionHint(IInteractable data)
    {
        // 상호작용 힌트 UI 처리
        if (data == null) return;
        _interactionCommandUI.Show("E", (data as Interactable).HintText);
        _currentOwner = data;
    }
    public T Get<T>() where T : UI
    {
        if (_popupMap.TryGetValue(typeof(T), out var popup))
            return (T)popup;
        return null;
    }

    public T Open<T>() where T : UI
    {
        var p = Get<T>();
        p?.Show();
        return p;
    }

    public void Close<T>() where T : UI
    {
        var p = Get<T>();
        p?.Hide();
    }
}
}

