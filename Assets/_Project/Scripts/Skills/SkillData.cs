using UnityEngine;

/// <summary>
/// 技能的施放方式。想加一种新方式（比如「朝天上召唤一道雷」），
/// 就是在这里加一项 + 在 SkillCaster.Cast() 里加一个 case。
/// </summary>
public enum SkillCastType
{
    /// <summary>在角色身边打一个判定框，命中范围内的所有敌人。旋风斩、冲击波</summary>
    Melee,

    /// <summary>发射投射物。火球、箭、飞刀</summary>
    Projectile,
}

/// <summary>
/// 一个技能的全部配置。
///
/// 和 AttackData 的分工：
///   AttackData 管「打出去长什么样」—— 伤害倍率、击退、判定框多大、能打哪些层
///   SkillData  管「什么时候能打、花多少代价」—— 法力、冷却、怎么发射、发几发
///
/// 所以一个技能 = 一个 SkillData 资产 + 一个 AttackData 资产，都是右键新建。
/// 将来战士的 3 个技能、法师的 3 个技能，全都只是 12 个资产文件，一行代码都不用写。
/// </summary>
[CreateAssetMenu(fileName = "Skill_", menuName = "Roguelike/Skill Data")]
public class SkillData : ScriptableObject
{
    [Header("标识")]
    [Tooltip("唯一 id，技能树和存档靠它认人。发布之后不要改，否则已解锁的记录会对不上")]
    public string skillId = "new_skill";

    public string displayName = "新技能";

    public Sprite icon;

    [TextArea(2, 4)]
    public string description;

    [Header("消耗与冷却")]
    [Tooltip("放一次要多少法力。0 = 不耗蓝")]
    public float manaCost = 10f;

    [Tooltip("技能自身的冷却（秒）。只吃「冷却缩减」，不吃「攻速」")]
    public float cooldown = 3f;

    [Header("施放方式")]
    public SkillCastType castType = SkillCastType.Melee;

    [Header("伤害与判定")]
    [Tooltip("伤害倍率 / 击退 / 判定框尺寸 / 目标层都在这个资产里")]
    public AttackData attack;

    [Header("投射物（castType = Projectile 时使用）")]
    public GameObject projectilePrefab;

    [Tooltip("一次发几发。强化系统做「多重射击」就是加这个数字")]
    [Range(1, 8)]
    public int projectileCount = 1;

    [Tooltip("发多发时的扇形张角（度）。3 发 + 10 度 = -10° / 0° / +10°")]
    public float spreadAngle = 10f;

    /// <summary>
    /// 实际冷却。
    ///
    /// 技能只吃「冷却缩减」，**不吃攻速** —— 如果堆攻速也能刷技能，
    /// 那攻速就会变成唯一解，装备和强化就没有别的选择可言了。
    /// 攻速只影响普通攻击的间隔（见 MeleeAttacker.GetCooldown）。
    /// </summary>
    public float GetCooldown(CharacterStats stats)
    {
        if (stats == null) return cooldown;

        float reduction = Mathf.Clamp(stats.Get(StatType.CooldownRate), 0f, 0.8f);
        return cooldown * (1f - reduction);
    }

    /// <summary>法力够不够放这一个技能</summary>
    public bool CanAfford(ManaPool mana) => mana == null || mana.Has(manaCost);
}
