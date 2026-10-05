using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 角色所有战斗数值的唯一出口。玩家和敌人共用。
///
/// 职业 / 装备 / 技能 / 强化都只是「往这里加修饰符」，
/// 战斗代码只调 Get() 拿最终值，完全不需要知道数值是从哪来的。
/// </summary>
public class CharacterStats : MonoBehaviour
{
    [Header("数据来源")]
    [Tooltip("玩家填职业资产。敌人留空，直接用手填的基础值")]
    [SerializeField] private ClassData classData;

    [Header("基础属性（classData 留空时使用）")]
    [SerializeField] private float baseMaxHealth      = 100f;
    [SerializeField] private float baseAttack         = 10f;
    [SerializeField] private float baseMoveSpeed      = 8f;
    [SerializeField] private float baseAttackSpeed    = 1f;
    [Range(0f, 1f)]
    [SerializeField] private float baseCritChance     = 0.05f;
    [SerializeField] private float baseCritMultiplier = 1.5f;
    [SerializeField] private float baseDefense        = 0f;
    [SerializeField] private float baseCooldownRate   = 0f;
    [SerializeField] private float baseMaxMana        = 100f;
    [SerializeField] private float baseManaRegen      = 6f;

    [Header("联动")]
    [Tooltip("留空则自动找同物体上的 Health，把最大生命同步过去")]
    [SerializeField] private Health health;

    private readonly Dictionary<StatType, float> _baseValues = new();
    private readonly Dictionary<StatType, List<StatModifier>> _modifiers = new();

    /// <summary>属性变化时触发（穿脱装备、强化生效）。血条和 UI 订阅它刷新</summary>
    public event System.Action Changed;

    /// <summary>
    /// 换职业时触发。技能栏、外观、UI 订阅它重新装配自己。
    /// 它和 Changed 分开是有意的：Changed 每捡一件装备都会响，
    /// 而「重新装配技能栏」不需要这么频繁。
    /// </summary>
    public event System.Action<ClassData> ClassChanged;

    /// <summary>当前使用的职业数据，只有玩家才有</summary>
    public ClassData Class => classData;

    private void Awake()
    {
        if (health == null) health = GetComponent<Health>();

        WriteFallbackBaseValues();

        // 职业数据在基础值之后应用，它会覆盖掉手填值
        if (classData != null) ApplyClassData(classData);
    }

    private void Start()
    {
        // 放在 Start 而不是 Awake：要等所有 Health.Awake() 跑完（血量已初始化），
        // 否则 SetMaxHealth 按比例算出来的血量是错的
        SyncMaxHealthToHealth();
    }

    private void OnEnable()  => Changed += SyncMaxHealthToHealth;
    private void OnDisable() => Changed -= SyncMaxHealthToHealth;

    // ---------------- 取值 ----------------

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
                    case ModifierMode.Flat:            flat       += m.Value;       break;
                    case ModifierMode.PercentAdd:      percentAdd += m.Value;       break;
                    case ModifierMode.PercentMultiply: percentMul *= 1f + m.Value;  break;
                }
            }
        }

        // 固定顺序：加算 → 百分比加算 → 百分比乘算
        return flat * (1f + percentAdd) * percentMul;
    }

    // ---------------- 修饰符 ----------------

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

    public void ClearModifiers()
    {
        _modifiers.Clear();
        Changed?.Invoke();
    }

    // ---------------- 职业 ----------------

    /// <summary>应用一个职业：覆盖基础值、清空旧修饰符</summary>
    public void ApplyClass(ClassData data)
    {
        if (data == null) return;

        classData = data;
        ApplyClassData(data);

        // 换职业时，旧职业的装备 / 技能加成必须全部清掉，否则会叠加串味
        ClearModifiers();
        Changed?.Invoke();
        ClassChanged?.Invoke(data);
    }

    private void ApplyClassData(ClassData data)
    {
        _baseValues[StatType.MaxHealth]      = data.maxHealth;
        _baseValues[StatType.Attack]         = data.attack;
        _baseValues[StatType.MoveSpeed]      = data.moveSpeed;
        _baseValues[StatType.AttackSpeed]    = data.attackSpeed;
        _baseValues[StatType.CritChance]     = data.critChance;
        _baseValues[StatType.CritMultiplier] = data.critMultiplier;
        _baseValues[StatType.Defense]        = data.defense;
        _baseValues[StatType.CooldownRate]   = data.cooldownRate;
        _baseValues[StatType.MaxMana]        = data.maxMana;
        _baseValues[StatType.ManaRegen]      = data.manaRegen;
    }

    private void WriteFallbackBaseValues()
    {
        _baseValues[StatType.MaxHealth]      = baseMaxHealth;
        _baseValues[StatType.Attack]         = baseAttack;
        _baseValues[StatType.MoveSpeed]      = baseMoveSpeed;
        _baseValues[StatType.AttackSpeed]    = baseAttackSpeed;
        _baseValues[StatType.CritChance]     = baseCritChance;
        _baseValues[StatType.CritMultiplier] = baseCritMultiplier;
        _baseValues[StatType.Defense]        = baseDefense;
        _baseValues[StatType.CooldownRate]   = baseCooldownRate;
        _baseValues[StatType.MaxMana]        = baseMaxMana;
        _baseValues[StatType.ManaRegen]      = baseManaRegen;
    }

    /// <summary>把最终最大生命同步给 Health 组件</summary>
    private void SyncMaxHealthToHealth()
    {
        if (health != null) health.SetMaxHealth(Get(StatType.MaxHealth));
    }

    /// <summary>直接设基础值</summary>
    public void SetBase(StatType type, float value)
    {
        _baseValues[type] = value;
        Changed?.Invoke();
    }
}