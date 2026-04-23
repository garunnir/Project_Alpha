using UnityEngine;
using System.Collections.Generic;
using UnityEngine.InputSystem;
using UnityEditorInternal;

/// <summary>
/// Maps player input direction to an animation.
/// Two modes supported:
/// - SpriteSwap: assign eight directional sprite sequences (8-way) and it will cycle frames.
/// - Animator: set parameters on an Animator (int "Direction" and bool "Moving") so a Mecanim controller can drive animations.
///
/// Input comes from the classic Input axes (Horizontal, Vertical). Uses raw axes so direction is crisp.
/// </summary>
[RequireComponent(typeof(CharacterState))]
public class PlayerInputDirectionAnim : MonoBehaviour
{
    public enum Mode { SpriteSwap, Animator }
    [Header("General")]
    public Mode mode = Mode.SpriteSwap;
    [Tooltip("Minimum squared magnitude of input to consider 'moving'")]
    public float moveThreshold = 0.01f;
    [Tooltip("방향 판정 기준 회전 오프셋 (도). 카메라/스프라이트 정렬 보정용")]
    public float angleOffset = 0f;

    [Header("Animator Mode")]
    public Animator animator;

    public string paramDirX = "DirX";

    public string paramDirY = "DirY";
    public string paramMoving = "Moving";

    [Header("SpriteSwap Mode (8 directions)")]
    public SpriteRenderer spriteRenderer;
    [Tooltip("Frames per second when animating sprite frames")]
    public float fps = 8f;

    [System.Serializable]
    public class DirectionFrames { public string name; public Sprite[] frames; }

    [Tooltip("Order: 0 = East (0°), 1 = NorthEast (45°), 2 = North (90°), 3 = NorthWest (135°), 4 = West (180°), 5 = SouthWest (225°), 6 = South (270°), 7 = SouthEast (315°)")]
    public DirectionFrames[] directionFrames = new DirectionFrames[8];

    // runtime
    int currentDirection = 0; // 0..7
    float animTimer = 0f;
    CharacterState _characterState;

    void Awake()
    {
        _characterState = GetComponentInParent<CharacterState>();
        if (_characterState == null) _characterState = GetComponent<CharacterState>();
    }

    void Reset()
    {
        // try to auto-assign common components
        if (spriteRenderer == null) spriteRenderer = GetComponentInChildren<SpriteRenderer>();
        if (animator == null) animator = GetComponent<Animator>();
    }

    void Update()
    {
        Vector3 dir3 = Quaternion.Euler(0f, angleOffset, 0f) * _characterState.GetFacingDir();
        Vector2 dir = new Vector2(dir3.x, dir3.z); // 3D 이동방향은 XZ 평면 → Y는 항상 0
        bool moving = dir.sqrMagnitude > moveThreshold;

        if (mode == Mode.Animator)
        {
            UpdateAnimator(dir, moving);
        }
        else
        {
            UpdateSpriteSwap(dir, moving);
        }
    }

    void UpdateAnimator(Vector2 input, bool moving)
    {
        if (animator == null) return;
        if (!string.IsNullOrEmpty(paramDirX))
        {
            if (moving)
            {
                animator.SetFloat(paramDirX, input.x);
                animator.SetFloat(paramDirY, input.y);
            }
            else
            {

            }
        }
        if (!string.IsNullOrEmpty(paramMoving))
        {
            int MovingHash = Animator.StringToHash(paramMoving);


            animator.SetBool(MovingHash, moving);
        }
    }

    void UpdateSpriteSwap(Vector2 input, bool moving)
    {
        if (spriteRenderer == null) return;
        if (directionFrames == null || directionFrames.Length != 8) return;

        int dir = moving ? AngleTo8Dir(input) : currentDirection; // keep last facing when idle

        if (dir != currentDirection)
        {
            currentDirection = dir;
            animTimer = 0f; // restart frame on direction change
        }

        var frames = (directionFrames[currentDirection] != null) ? directionFrames[currentDirection].frames : null;
        if (frames == null || frames.Length == 0)
        {
            // nothing assigned for this direction
            return;
        }

        if (!moving)
        {
            // idle: show first frame
            spriteRenderer.sprite = frames[0];
            return;
        }

        animTimer += Time.deltaTime;
        int frameIdx = Mathf.FloorToInt(animTimer * fps) % frames.Length;
        if (frameIdx < 0) frameIdx = 0;
        spriteRenderer.sprite = frames[frameIdx];
    }

    // Convert a 2D input vector to an 8-way direction index (0..7)
    // Sector layout: 0 = East (0°), increments counter-clockwise every 45°
    int AngleTo8Dir(Vector2 v)
    {
        if (v.sqrMagnitude < 1e-6f) return currentDirection;
        float angle = Mathf.Atan2(v.y, v.x) * Mathf.Rad2Deg; // -180..180 (0 = +X)
        if (angle < 0) angle += 360f; // 0..360
        // map angle to 0..7 (each sector = 45°)
        int idx = Mathf.RoundToInt(angle / 45f) % 8;
        return idx;
    }
}
