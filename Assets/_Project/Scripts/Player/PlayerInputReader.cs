using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// 输入层：把 Input Actions 的原始输入，翻译成游戏逻辑能直接用的属性。
///
/// 好处是 PlayerController / MeleeAttacker 完全不需要知道输入来自键盘、手柄还是鼠标。
/// </summary>
public class PlayerInputReader : MonoBehaviour
{
    private PlayerInputActions _actions;

    /// <summary>水平/垂直输入方向，范围 -1 ~ 1</summary>
    public Vector2 Move { get; private set; }

    // 「按下」「松开」是瞬时事件，而 Update 和 FixedUpdate 的频率不同步，
    // 直接读 WasPressedThisFrame 可能被 FixedUpdate 整个错过。
    // 所以先锁存下来，等逻辑层主动「取走」—— 保证一次按键不多不少被处理一次。
    private bool _jumpPressedLatch;
    private bool _jumpReleasedLatch;
    private bool _attackPressedLatch;
    private bool _dashPressedLatch;

    private void Awake()
    {
        _actions = new PlayerInputActions();
    }

    private void OnEnable()
    {
        _actions.Player.Enable();

        _actions.Player.Jump.performed   += OnJumpPerformed;
        _actions.Player.Jump.canceled    += OnJumpCanceled;
        _actions.Player.Attack.performed += OnAttackPerformed;
        _actions.Player.Dash.performed   += OnDashPerformed;
    }

    private void OnDisable()
    {
        _actions.Player.Jump.performed   -= OnJumpPerformed;
        _actions.Player.Jump.canceled    -= OnJumpCanceled;
        _actions.Player.Attack.performed -= OnAttackPerformed;
        _actions.Player.Dash.performed   -= OnDashPerformed;

        _actions.Player.Disable();
    }

    private void Update()
    {
        Move = _actions.Player.Move.ReadValue<Vector2>();
    }

    // ---------------- 事件消费 ----------------
    // 「取走」语义：取过一次就没有了。这样 FixedUpdate 连跑两次也不会重复消费同一个按键。

    /// <summary>取走「按下了跳跃」事件</summary>
    public bool ConsumeJumpPressed()
    {
        if (!_jumpPressedLatch) return false;
        _jumpPressedLatch = false;
        return true;
    }

    /// <summary>取走「松开了跳跃」事件</summary>
    public bool ConsumeJumpReleased()
    {
        if (!_jumpReleasedLatch) return false;
        _jumpReleasedLatch = false;
        return true;
    }

    /// <summary>取走「按下了攻击」事件</summary>
    public bool ConsumeAttackPressed()
    {
        if (!_attackPressedLatch) return false;
        _attackPressedLatch = false;
        return true;
    }

    /// <summary>取走「按下了冲刺」事件</summary>
    public bool ConsumeDashPressed()
    {
        if (!_dashPressedLatch) return false;
        _dashPressedLatch = false;
        return true;
    }

    // ---------------- 回调 ----------------

    private void OnJumpPerformed  (InputAction.CallbackContext _) => _jumpPressedLatch  = true;
    private void OnJumpCanceled   (InputAction.CallbackContext _) => _jumpReleasedLatch = true;
    private void OnAttackPerformed(InputAction.CallbackContext _) => _attackPressedLatch = true;
    private void OnDashPerformed  (InputAction.CallbackContext _) => _dashPressedLatch   = true;
}