using System;
using UnityEngine;

/// <summary>
/// 通用血量组件：玩家、敌人、可破坏物都能挂。
/// 无敌帧在这里统一处理，攻击方完全不需要关心。
/// </summary>
public class Health : MonoBehaviour, IDamageable
{
    [Header("血量")]
    [SerializeField] private float maxHealth = 100f;

    [Header("无敌帧")]
    [Tooltip("受击后多久内免疫后续伤害，0 表示不免疫")]
    [SerializeField] private float invincibilityDuration = 0.5f;

    public float Max => maxHealth;
    public float Current { get; private set; }
    public bool IsAlive => Current > 0f;

    /// <summary>受到有效伤害时触发（被无敌帧挡掉的不触发）</summary>
    public event Action<DamageInfo> Damaged;

    /// <summary>血量归零时触发</summary>
    public event Action Died;

    private float _invincibilityTimer;

    private void Awake()
    {
        Current = maxHealth;
    }

    private void Update()
    {
        if (_invincibilityTimer > 0f)
            _invincibilityTimer -= Time.deltaTime;
    }

    public void TakeDamage(DamageInfo info)
    {
        if (!IsAlive) return;                  // 已经死了，不重复结算
        if (_invincibilityTimer > 0f) return;  // 处于无敌帧

        Current = Mathf.Max(0f, Current - info.Amount);   // 防止血量变负
        _invincibilityTimer = invincibilityDuration;

        Damaged?.Invoke(info);

        if (Current <= 0f)
            Died?.Invoke();
    }

    /// <summary>设置最大生命值，可选保持血量百分比 —— 装备改变上限时调用</summary>
    public void SetMaxHealth(float newMax, bool keepRatio = true)
    {
        if (newMax <= 0f) return;

        float ratio = maxHealth > 0f ? Current / maxHealth : 1f;
        maxHealth = newMax;
        Current = keepRatio ? maxHealth * ratio : Mathf.Min(Current, maxHealth);
    }

    /// <summary>回满血 / 重置</summary>
    public void ResetToFull()
    {
        Current = maxHealth;
        _invincibilityTimer = 0f;
    }
}