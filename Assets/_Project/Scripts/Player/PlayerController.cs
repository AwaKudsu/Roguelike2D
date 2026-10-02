using UnityEngine;

/// <summary>
/// 玩家移动逻辑。Day 1 只做水平移动，跳跃在 Day 2。
/// </summary>
[RequireComponent(typeof(Rigidbody2D))]
[RequireComponent(typeof(PlayerInputReader))]
public class PlayerController : MonoBehaviour
{
    [Header("移动参数")]

    [Tooltip("最大水平移动速度")]
    [SerializeField] private float maxSpeed = 8f;

    [Tooltip("按下方向键时的加速度（越大起步越快）")]
    [SerializeField] private float acceleration = 60f;

    [Tooltip("松开方向键时的减速度（一般比加速度大，刹车更干脆）")]
    [SerializeField] private float deceleration = 80f;

    private Rigidbody2D _rb;
    private PlayerInputReader _input;

    private void Awake()
    {
        _rb    = GetComponent<Rigidbody2D>();
        _input = GetComponent<PlayerInputReader>();
    }

    // 物理相关的操作一律放 FixedUpdate，不要放 Update
    private void FixedUpdate()
    {
        ApplyHorizontalMovement();
    }

    private void ApplyHorizontalMovement()
    {
        // 1. 目标速度：输入方向 × 最大速度
        float targetSpeed = _input.Move.x * maxSpeed;

        // 2. 有输入时用加速度，无输入时用减速度
        float rate = Mathf.Abs(targetSpeed) > 0.01f ? acceleration : deceleration;

        // 3. 关键：让当前速度「平滑趋近」目标速度，而不是直接赋值
        float newSpeedX = Mathf.MoveTowards(
            _rb.linearVelocity.x,
            targetSpeed,
            rate * Time.fixedDeltaTime
        );

        // 4. 只改 x，保留 y（否则会抹掉重力，角色悬在空中）
        _rb.linearVelocity = new Vector2(newSpeedX, _rb.linearVelocity.y);
    }
}