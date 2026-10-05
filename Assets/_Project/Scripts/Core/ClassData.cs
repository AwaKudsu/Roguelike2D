using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 一个职业的全部配置。
///
/// 它是纯数据 —— 没有任何逻辑，所以新增职业不需要写一行代码，
/// 只要在 Project 窗口右键 Create 一个新资产。
/// </summary>
[CreateAssetMenu(fileName = "Class_", menuName = "Roguelike/Class Data")]
public class ClassData : ScriptableObject
{
    [Header("展示")]
    public string className = "新职业";
    public Sprite icon;

    [TextArea(2, 4)]
    public string description;

    [Header("基础属性")]
    public float maxHealth      = 100f;
    public float attack         = 10f;
    public float moveSpeed      = 8f;

    [Tooltip("攻击速度倍率。1 = 正常，1.4 = 冷却缩短到 71%")]
    public float attackSpeed    = 1f;

    [Range(0f, 1f)]
    public float critChance     = 0.05f;

    public float critMultiplier = 1.5f;

    [Tooltip("按比例减伤，0.1 = 减少 10% 伤害")]
    [Range(0f, 0.8f)]
    public float defense        = 0f;

    [Range(0f, 0.8f)]
    public float cooldownRate   = 0f;

    [Header("法力")]
    [Tooltip("最大法力。法师靠它放技能，战士也有 —— 只是数字不一样")]
    public float maxMana        = 100f;

    [Tooltip("每秒回蓝。消耗后会有短暂延迟才开始回，见 ManaPool")]
    public float manaRegen      = 6f;

    [Header("初始配置")]
    public AttackData basicAttack;

    [Tooltip("开局就带的技能，按顺序对应 K / L / U / I 四个键")]
    public List<SkillData> startingSkills = new();
}