using UnityEngine;

public interface IPlayControllable
{
    void SetControlEnabled(bool enabled);
}

public interface IMovable
{
    void SetControllEnabled(bool enabled);
    Vector2 GetDirection();
}
