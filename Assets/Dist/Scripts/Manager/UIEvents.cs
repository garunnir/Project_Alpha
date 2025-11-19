public enum UIPopupType
{
    none,
    InteractionHint,
    Chest,
    Dialogue
}


public static class UIEvents
{
    public static event System.Action<UIPopupType, object> OnPopupRequested;

    public static void RequestPopup(UIPopupType type, object data = null)
    {
        OnPopupRequested?.Invoke(type, data);
    }
}