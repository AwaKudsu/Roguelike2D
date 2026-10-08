using UnityEngine;

/// <summary>
/// 玩家的 HUD：血条 + 蓝条。
///
/// ★ 它每帧去「问」Health / ManaPool 现在是多少，而不是等它们发事件通知。
///   这是刻意的取舍，不是偷懒：
///     - 逻辑层（伤害结算、死亡判定、技能冷却）用事件，因为它们必须精确地「发生一次」；
///     - 显示层用轮询，因为它只是把当前状态画出来，早一帧晚一帧无所谓。
///   反过来做的代价更大：漏订阅一个事件，血条就永远不动，而且不报错。
///   本项目已经吃过一次「静默失效」的亏（LayerMask 留空），所以显示层一律轮询。
///
///   代价是每帧读两个 float，可以忽略不计。
/// </summary>
public class HUDController : MonoBehaviour
{
    [Header("数据源（留空则自动找 Tag 为 Player 的对象）")]
    [SerializeField] private Health playerHealth;
    [SerializeField] private ManaPool playerMana;

    [Header("显示")]
    [Tooltip("血条。它的 StatBar 会把 Health.Normalized 画出来")]
    [SerializeField] private StatBar healthBar;

    [Tooltip("蓝条。法师特别需要它，战士也不能没有")]
    [SerializeField] private StatBar manaBar;

    [Header("规则")]
    [Tooltip("玩家死亡时把血条蓝条藏起来")]
    [SerializeField] private bool hideBarsOnDeath = true;

    /// <summary>自动找玩家。只在需要的时候找，避免每帧都调用 FindGameObjectWithTag</summary>
    private void ResolveReferences()
    {
        if (playerHealth != null && playerMana != null) return;

        var go = GameObject.FindGameObjectWithTag("Player");
        if (go == null) return;

        if (playerHealth == null) playerHealth = go.GetComponent<Health>();
        if (playerMana == null) playerMana = go.GetComponent<ManaPool>();
    }

    private void Awake()
    {
        ResolveReferences();
    }

    private void Start()
    {
        // 开局先硬同步一次，否则第一帧会闪一下 Inspector 里的占位数值
        SnapBars();
    }

    private void Update()
    {
        // 玩家是场景里的对象，正常不会中途消失；但换场景 / 重开时会短暂找不到，
        // 所以留一条重找的后路，免得血条从此再也不动
        if (playerHealth == null || playerMana == null) ResolveReferences();

        bool alive = playerHealth == null || playerHealth.IsAlive;

        if (healthBar != null)
        {
            if (playerHealth != null) healthBar.SetValue(playerHealth.Normalized);
            SetBarVisible(healthBar, alive || !hideBarsOnDeath);
        }

        if (manaBar != null)
        {
            if (playerMana != null) manaBar.SetValue(playerMana.Normalized);
            SetBarVisible(manaBar, alive || !hideBarsOnDeath);
        }
    }

    private void SnapBars()
    {
        if (healthBar != null && playerHealth != null) healthBar.SnapToValue(playerHealth.Normalized);
        if (manaBar != null && playerMana != null) manaBar.SnapToValue(playerMana.Normalized);
        SetBarVisible(healthBar, true);
        SetBarVisible(manaBar, true);
    }

    private static void SetBarVisible(StatBar bar, bool visible)
    {
        if (bar == null) return;
        if (bar.gameObject.activeSelf != visible) bar.gameObject.SetActive(visible);
    }
}
