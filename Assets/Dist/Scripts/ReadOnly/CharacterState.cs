using System;
using UnityEngine;

public class CharacterState : MonoBehaviour
{
    public Vector3 FacingDir { get; private set; }=Vector3.zero;

    internal void UpdateState(Vector3 desiredMove)
    {
        FacingDir=desiredMove;
    }
}
