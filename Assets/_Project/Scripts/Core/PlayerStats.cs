using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 角色所有战斗数值的唯一出口。
///
/// 职业提供「基础值」，装备 / 技能 / 强化通过「修饰符」叠加，
/// 战斗代码只调 Get() 拿最终值，完全不需要知道数值是从哪来的。
/// </summary>
public class PlayerStats : MonoBehaviour
{
    [Header("基础属性（职业初始值，将来由 ClassData 赋值）")]
    [SerializeField] private float baseMaxHealth      = 100f;
    [SerializeField] private float baseAttack         = 10f;
    [SerializeField] private float baseMoveSpeed      = 8f;
    [SerializeField] private float baseAttackSpeed    = 1f;
    [Range(0f, 1f)]
    [SerializeField] private float baseCritChance     = 0.05f;
    [SerializeField] private float baseCritMultiplier = 1.5f;
    [SerializeField] private float baseDefense        = 0f;
    [SerializeField] private float baseCooldownRate   = 0f;

    private readonly Dictionary<StatType, float> _baseValues = new();
    private readonly Dictionary<StatType, List<StatModifier>> _modifiers = new();

    /// <summary>属性变化时触发（穿脱装备、强化生效时）。UI 和血条订阅它刷新显示</summary>
    public event System.Action Changed;

    private void Awake()
    {
        _baseValues[StatType.MaxHealth]      = baseMaxHealth;
        _baseValues[StatType.Attack]         = baseAttack;
        _baseValues[StatType.MoveSpeed]      = baseMoveSpeed;
        _baseValues[StatType.AttackSpeed]    = baseAttackSpeed;
        _baseValues[StatType.CritChance]     = baseCritChance;
        _baseValues[StatType.CritMultiplier] = baseCritMultiplier;
        _baseValues[StatType.Defense]        = baseDefense;
        _baseValues[StatType.CooldownRate]   = baseCooldownRate;
    }

    /// <summary>取某个属性的最终值</summary>
    public float Get(StatType type)
    {
        float flat       = _baseValues.TryGetValue(type, out var b) ? b : 0f;
        float percentAdd = 0f;
        float percentMul = 1f;

        if (_modifiers.TryGetValue(type, out var list))
        {
            for (int i = 0; i < list.Count; i++)
            {
                var m = list[i];
                switch (m.Mode)
                {
                    case ModifierMode.Flat:            flat       += m.Value;              break;
                    case ModifierMode.PercentAdd:      percentAdd += m.Value;              break;
                    case ModifierMode.PercentMultiply: percentMul *= 1f + m.Value;         break;
                }
            }
        }

        // 固定顺序：加算 → 百分比加算 → 百分比乘算
        return flat * (1f + percentAdd) * percentMul;
    }

    /// <summary>加一条修饰符</summary>
    public void AddModifier(StatModifier modifier, object source = null)
    {
        modifier.Source = source;

        if (!_modifiers.TryGetValue(modifier.Type, out var list))
        {
            list = new List<StatModifier>();
            _modifiers[modifier.Type] = list;
        }

        list.Add(modifier);
        Changed?.Invoke();
    }

    /// <summary>移除某个来源加的全部修饰符 —— 卸下装备时调这一句就够</summary>
    public void RemoveAllFrom(object source)
    {
        if (source == null) return;

        bool removed = false;
        foreach (var list in _modifiers.Values)
        {
            if (list.RemoveAll(m => ReferenceEquals(m.Source, source)) > 0)
                removed = true;
        }

        if (removed) Changed?.Invoke();
    }

    /// <summary>清空所有修饰符（换职业 / 重置时用）</summary>
    public void ClearModifiers()
    {
        _modifiers.Clear();
        Changed?.Invoke();
    }

    /// <summary>直接设基础值（换职业时用）</summary>
    public void SetBase(StatType type, float value)
    {
        _baseValues[type] = value;
        Changed?.Invoke();
    }
}