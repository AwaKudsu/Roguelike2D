using System;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 技能栏：4 个格子，显示技能图标 + 冷却遮罩。
///
/// 和 HUDController 一样每帧轮询 —— 冷却本来就是一个每帧都在变的数字，
/// 让它每帧发一次事件反而是浪费。
///
/// 格子顺序固定对应 K / L / U / I（见 PlayerInputReader.SkillSlotCount）。
/// </summary>
public class SkillBarUI : MonoBehaviour
{
    /// <summary>一个技能格。里面装的两张图都是可选的，只放图标也能用</summary>
    [Serializable]
    public class Slot
    {
        [Tooltip("技能图标。没有技能的格子会被整格隐藏")]
        public Image icon;

        [Tooltip("盖在图标上的冷却遮罩（一般是一张半透明黑图）。Image Type 必须是 Filled / Radial 360")]
        public Image cooldownMask;
    }

    [Header("数据源（留空则自动找 Tag 为 Player 的对象）")]
    [SerializeField] private SkillCaster caster;

    [Header("格子（顺序对应 K / L / U / I）")]
    [Tooltip("长度建议就是 4，和 SkillCaster.MaxSlots 一致")]
    [SerializeField] private Slot[] slots = new Slot[SkillCaster.MaxSlots];

    [Header("配色")]
    [Tooltip("冷却好了、蓝也够 —— 可以放")]
    [SerializeField] private Color readyColor = Color.white;

    [Tooltip("冷却中或者蓝不够 —— 放不出来")]
    [SerializeField] private Color notReadyColor = new Color(0.42f, 0.42f, 0.48f, 1f);

    private void Awake()
    {
        if (caster == null)
        {
            var go = GameObject.FindGameObjectWithTag("Player");
            if (go != null) caster = go.GetComponent<SkillCaster>();
        }
    }

    private void Update()
    {
        if (caster == null) return;

        for (int i = 0; i < slots.Length; i++)
        {
            Slot slot = slots[i];
            if (slot == null || slot.icon == null) continue;

            SkillData skill = caster.GetSkill(i);

            // 这个格子没技能（比如战士只有 1 个技能，后面 3 格是空的）→ 整格隐藏
            if (skill == null)
            {
                SetSlotVisible(slot, false);
                continue;
            }

            SetSlotVisible(slot, true);

            // 比较一下再赋值：给 Image.sprite 每帧赋同样的值会触发 UI 重建，
            // 4 个格子看着不多，但这是纯浪费，而且以后格子变多会明显卡
            if (slot.icon.sprite != skill.icon) slot.icon.sprite = skill.icon;

            // IsReady 同时看三件事：有技能、冷却好了、蓝够
            slot.icon.color = caster.IsReady(i) ? readyColor : notReadyColor;

            // GetCooldownRatio：1 = 刚放完（遮罩盖满），0 = 好了（遮罩全退）
            if (slot.cooldownMask != null)
                slot.cooldownMask.fillAmount = caster.GetCooldownRatio(i);
        }
    }

    private static void SetSlotVisible(Slot slot, bool visible)
    {
        if (slot.icon != null && slot.icon.gameObject.activeSelf != visible)
            slot.icon.gameObject.SetActive(visible);

        // 遮罩通常是图标的子物体，图标关掉它也就跟着关了；
        // 但万一是兄弟节点（比如想让它盖住整格边框），这里也得管一下
        if (slot.cooldownMask != null && slot.cooldownMask.gameObject.activeSelf != visible)
            slot.cooldownMask.gameObject.SetActive(visible);
    }

    /// <summary>
    /// 和 StatBar 一样，在 Inspector 里就把「冷却遮罩不会动」这个坑喊出来。
    /// Image Type 不是 Filled 时 fillAmount 完全无效，而且一点报错都没有。
    /// </summary>
    private void OnValidate()
    {
        if (slots == null) return;

        for (int i = 0; i < slots.Length; i++)
        {
            Slot slot = slots[i];
            if (slot?.cooldownMask == null) continue;

            if (slot.cooldownMask.type != Image.Type.Filled)
            {
                Debug.LogWarning(
                    $"[SkillBarUI] 第 {i + 1} 格的冷却遮罩 Image Type 是「{slot.cooldownMask.type}」，不是 Filled。" +
                    "这样 fillAmount 完全无效，冷却遮罩永远不会出现也不会有报错。" +
                    "请把 Image Type 改成 Filled，Fill Method 改成 Radial 360。", this);
            }
        }
    }
}
