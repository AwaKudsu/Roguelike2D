using System;
using UnityEngine;

/// <summary>
/// 法力值组件。和 Health 是同一套路子：
/// 「上限」来自 CharacterStats（可以被职业 / 装备 / 强化改），
/// 「当前值」和回复逻辑放在这里。
///
/// 所以战士和法师在蓝量上的差别，只是 ClassData 上两个数字，不需要两套代码。
/// </summary>
public class ManaPool : MonoBehaviour
{
    [Header("基础法力（CharacterStats 缺席时使用）")]
    [SerializeField] private float baseMaxMana = 100f;
    [SerializeField] private float baseRegen   = 6f;

    [Header("节奏")]
    [Tooltip("消耗法力后多久才开始回复。设成 0 会变成边打边回，资源就没有约束力了")]
    [SerializeField] private float regenDelay = 0.6f;

    [Tooltip("回复速度倍率，留给「战斗中回蓝变慢」这类强化用")]
    [SerializeField] private float regenScale = 1f;

    /// <summary>上限。会随装备 / 强化变化，所以别缓存，用之前读这里</summary>
    public float Max { get; private set; }

    public float Current { get; private set; }

    public bool IsFull => Current >= Max;

    /// <summary>0~1，蓝条 UI 直接用它填进度</summary>
    public float Normalized => Max > 0f ? Current / Max : 0f;

    /// <summary>法力变化时触发（消耗 / 回复 / 上限改变）。蓝条 UI 订阅它刷新</summary>
    public event Action Changed;

    private CharacterStats _stats;
    private float _regenBlockTimer;

    private void Awake()
    {
        _stats = GetComponent<CharacterStats>();

        // 注意：这里绝对不能读 _stats.Get()。
        // 同一个物体上各组件 Awake 的先后顺序是不确定的，CharacterStats.Awake 可能还没跑，
        // 那时它的基础值表是空的，Get() 会返回 0，上限就变成 0 了。
        // 真正的同步放到 Start —— Unity 保证所有 Awake 跑完才开始跑 Start。
        Max     = baseMaxMana;
        Current = Max;
    }

    private void Start()
    {
        SyncMaxMana(refillIfWasFull: false);
        Current = Max;              // 开局满蓝
        Changed?.Invoke();
    }

    private void OnEnable()
    {
        if (_stats != null) _stats.Changed += OnStatsChanged;
    }

    private void OnDisable()
    {
        if (_stats != null) _stats.Changed -= OnStatsChanged;
    }

    private void Update()
    {
        if (_regenBlockTimer > 0f)
        {
            _regenBlockTimer -= Time.deltaTime;
            return;
        }

        if (Current >= Max) return;

        float regen = (_stats != null ? _stats.Get(StatType.ManaRegen) : baseRegen) * regenScale;
        if (regen <= 0f) return;

        Current = Mathf.Min(Max, Current + regen * Time.deltaTime);
        Changed?.Invoke();
    }

    // ---------------- 外部接口 ----------------

    /// <summary>法力够不够。UI 用它把技能图标画成灰色</summary>
    public bool Has(float amount) => Current >= amount;

    /// <summary>
    /// 尝试扣除法力。不够就返回 false，并且一点蓝都不扣。
    /// 「先问再扣」这个顺序很重要 —— 否则会出现「技能没放出来，蓝却没了」。
    /// </summary>
    public bool TrySpend(float amount)
    {
        if (amount <= 0f) return true;          // 不耗蓝的技能永远放得出来
        if (Current < amount) return false;

        Current -= amount;
        _regenBlockTimer = regenDelay;
        Changed?.Invoke();
        return true;
    }

    public void Restore(float amount)
    {
        if (amount <= 0f) return;

        Current = Mathf.Min(Max, Current + amount);
        Changed?.Invoke();
    }

    /// <summary>回满。复活 / 通关奖励 / 进入新房间时用</summary>
    public void Refill()
    {
        Current = Max;
        _regenBlockTimer = 0f;
        Changed?.Invoke();
    }

    // ---------------- 上限同步 ----------------

    private void OnStatsChanged() => SyncMaxMana(refillIfWasFull: true);

    /// <summary>
    /// 属性变了之后重新算上限。
    ///
    /// 满蓝时上限涨了就跟满（否则玩家会觉得「我捡了个加蓝上限的装备，反倒亏了」），
    /// 没满时只做「不超过上限」的截断，不白送法力。
    /// </summary>
    private void SyncMaxMana(bool refillIfWasFull)
    {
        float newMax = _stats != null ? _stats.Get(StatType.MaxMana) : baseMaxMana;
        if (newMax <= 0f) newMax = baseMaxMana;

        bool wasFull = Max > 0f && Current >= Max;

        Max     = newMax;
        Current = (refillIfWasFull && wasFull) ? Max : Mathf.Min(Current, Max);

        Changed?.Invoke();
    }
}
