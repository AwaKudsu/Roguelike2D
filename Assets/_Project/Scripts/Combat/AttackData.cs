using UnityEngine;

/// <summary>
/// 一次攻击 / 一个技能的数据定义。
///
/// 这是整个「技能系统」的接入点：
/// 普通攻击是一个 AttackData 资产，战士的冲锋、法师的火球也各是一个 AttackData，
/// 区别只在数值和判定形状。所以第 2 周加技能时，攻击代码一个字都不用改。
/// </summary>
[CreateAssetMenu(fileName = "Attack_", menuName = "Roguelike/Attack Data")]
public class AttackData : ScriptableObject
{
    [Header("标识")]
    public string displayName = "普通攻击";
    public Sprite icon;

    [Header("伤害")]
    [Tooltip("伤害 = 攻击力 × 这个倍率。用倍率而不是固定值，装备变强时技能自动变强")]
    public float damageMultiplier = 1f;

    [Tooltip("击退力度")]
    public float knockbackForce = 7f;

    [Tooltip("技能自带的额外暴击率")]
    [Range(0f, 1f)]
    public float bonusCritChance = 0f;

    [Header("判定范围")]
    [Tooltip("判定框相对角色的偏移，x 会被朝向翻转")]
    public Vector2 hitboxOffset = new Vector2(0.8f, 0f);

    public Vector2 hitboxSize = new Vector2(1.2f, 0.9f);

    [Tooltip("能打到的层。⚠️ 必须设成 Enemy，留空会永远打不到人")]
    public LayerMask targetLayers;

    [Header("节奏")]
    [Tooltip("两次攻击之间的最短间隔")]
    public float cooldown = 0.35f;
}