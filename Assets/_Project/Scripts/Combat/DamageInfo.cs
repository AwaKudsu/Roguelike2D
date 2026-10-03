using UnityEngine;

/// <summary>
/// 一次伤害的完整描述。
///
/// 用结构体而不是一堆方法参数：将来要加「元素类型」「伤害类型」「是否破防」时，
/// 只需要在这里加字段，所有攻击方和受击方的函数签名都不用动。
/// </summary>
public struct DamageInfo
{
    /// <summary>伤害数值</summary>
    public float Amount;

    /// <summary>伤害来源的世界坐标，用于计算击退方向</summary>
    public Vector2 SourcePosition;

    /// <summary>击退力度，0 表示不击退</summary>
    public float KnockbackForce;

    /// <summary>攻击者，用于避免自己打自己，以及将来的伤害统计</summary>
    public GameObject Attacker;

    /// <summary>是否暴击（用于飘字颜色等表现）</summary>
    public bool IsCritical;
}