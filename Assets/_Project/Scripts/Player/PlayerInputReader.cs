using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// 输入层：把 Input Actions 的原始输入，翻译成游戏逻辑能直接用的属性。
/// 好处是 PlayerController 完全不需要知道输入来自键盘、手柄还是鼠标。
/// </summary>
public class PlayerInputReader : MonoBehaviour
{
    // Input Actions 资产勾选 Generate C# Class 后自动生成的类
    private PlayerInputActions _actions;

    /// <summary>水平/垂直输入方向，范围 -1 ~ 1</summary>
    public Vector2 Move { get; private set; }

    private void Awake()
    {
        _actions = new PlayerInputActions();
    }

    // 跟随 GameObject 的启用状态开关输入，避免对象销毁后仍在读输入
    private void OnEnable()  => _actions.Player.Enable();
    private void OnDisable() => _actions.Player.Disable();

    private void Update()
    {
        // 每帧读一次当前方向。按住 A 就是 (-1, 0)，松开就是 (0, 0)
        Move = _actions.Player.Move.ReadValue<Vector2>();
    }
}