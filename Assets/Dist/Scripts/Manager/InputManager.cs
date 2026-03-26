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

    private bool IsClike() { return Input.GetMouseButtonDown(0); }

    public static RaycastHit RayCast() //todo 공통사용가능한 부위로 옮겨야함.
    {
        var ray = Camera.main.ScreenPointToRay(Input.mousePosition);
        RaycastHit info;
        Physics.Raycast(ray, out info);
        if (info.collider != null) Debug.Log(info.collider.name);
        return info;
    }
}
