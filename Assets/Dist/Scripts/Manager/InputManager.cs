using System;
using UnityEngine;
using UnityEngine.InputSystem;

public class InputManager : Singleton<InputManager>
{
    public static Func<bool> click;

    public InputActions Actions { get; private set; }

    void Awake()
    {
        click = IsClike;
        Actions = new InputActions();
        Actions.Player.Enable();
    }

    public void SwitchToUI()
    {
        Actions.Player.Disable();
        Actions.UI.Enable();
    }

    public void SwitchToPlayer()
    {
        Actions.UI.Disable();
        Actions.Player.Enable();
    }

    void OnDestroy()
    {
        Actions.Dispose();
    }

    private bool IsClike() { return Pointer.current?.press.wasPressedThisFrame ?? false; }

    public static RaycastHit RayCast() //todo 공통사용가능한 부위로 옮겨야함.
    {
        var screenPos = Pointer.current?.position.ReadValue() ?? Vector2.zero;
        var ray = Camera.main.ScreenPointToRay(screenPos);
        Physics.Raycast(ray, out RaycastHit info);
        if (info.collider != null) Debug.Log(info.collider.name);
        return info;
    }
}
