using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 一根条。血条、蓝条、将来 Boss 的血条都用它。
///
/// 它只认 0~1 的百分比，完全不知道这个数字是血量还是法力 ——
/// 于是「去哪儿取数」和「怎么画出来」就分开了：
///   HUDController 负责取数（每帧读 Health.Normalized），
///   StatBar       负责画（把它变成 fillAmount）。
/// 这样将来加一条「体力条」只需要复制一个 UI 物体，一行代码都不用写。
/// </summary>
public class StatBar : MonoBehaviour
{
    [Header("引用")]
    [Tooltip("要改变长度的那张图。它的 Image Type 必须是 Filled")]
    [SerializeField] private Image fill;

    [Header("动画")]
    [Tooltip("勾上则数值变化时平滑过渡，关掉则瞬间跳变")]
    [SerializeField] private bool smooth = true;

    [Tooltip("每秒追赶多少百分比。4 = 从空到满大约 0.25 秒")]
    [SerializeField] private float catchUpSpeed = 4f;

    private float _shown = 1f;
    private float _target = 1f;

    /// <summary>真实值（0~1），由 HUDController 每帧喂进来</summary>
    public float Target => _target;

    /// <summary>当前画出来的值（0~1）。开了平滑时它会落后于 Target</summary>
    public float Shown => _shown;

    private void Awake()
    {
        if (fill == null) fill = GetComponentInChildren<Image>(true);

        _shown = _target;
        Apply(_shown);
    }

    /// <summary>设置目标值（0~1）。开了平滑的话，条会在几帧内追上它</summary>
    public void SetValue(float normalized)
    {
        _target = Mathf.Clamp01(normalized);

        if (smooth) return;

        _shown = _target;
        Apply(_shown);
    }

    /// <summary>跳过动画，立刻变成目标值。开局同步、换房间时用</summary>
    public void SnapToValue(float normalized)
    {
        _target = Mathf.Clamp01(normalized);
        _shown = _target;
        Apply(_shown);
    }

    private void Update()
    {
        if (!smooth) return;
        if (Mathf.Approximately(_shown, _target)) return;

        _shown = Mathf.MoveTowards(_shown, _target, catchUpSpeed * Time.deltaTime);
        Apply(_shown);
    }

    private void Apply(float value)
    {
        if (fill != null) fill.fillAmount = value;
    }

    /// <summary>
    /// 在 Inspector 里改数值时就会跑。
    ///
    /// 这里主动报警是因为 Image.fillAmount 有个非常安静的陷阱：
    /// 只要 Image Type 不是 Filled，fillAmount 就完全不起作用 ——
    /// 不报错、不警告，条永远停在满格，你会以为是代码没跑。
    /// 与其让你去猜，不如在 Inspector 里就喊出来。
    /// </summary>
    private void OnValidate()
    {
        if (fill == null) return;

        if (fill.type != Image.Type.Filled)
        {
            Debug.LogWarning(
                $"[StatBar] {name} 的 Fill 图 Image Type 是「{fill.type}」，不是 Filled。" +
                "这样 fillAmount 完全无效，条会永远满格而且不报错。" +
                "请把 Image Type 改成 Filled，Fill Method 改成 Horizontal。", this);
        }
    }
}
