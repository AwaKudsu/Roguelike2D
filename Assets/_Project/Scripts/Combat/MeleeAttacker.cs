using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 执行一次 AttackData 定义的攻击。
///
/// 它只做三件事：读输入 → 画判定框 → 把 DamageInfo 交给所有 IDamageable。
/// 「伤害多少」「打多大范围」全部来自 AttackData，「攻击力多高」来自 PlayerStats，
/// 代码里没有任何硬编码数值。
/// </summary>
[RequireComponent(typeof(PlayerInputReader))]
public class MeleeAttacker : MonoBehaviour
{
    [Header("当前使用的攻击（将来由技能系统切换）")]
    [SerializeField] private AttackData attackData;

    private PlayerInputReader _input;
    private PlayerStats _stats;
    private HitReaction _hitReaction;
    private float _cooldownTimer;
    private int _facing = 1;

    private void Awake()
    {
        _input       = GetComponent<PlayerInputReader>();
        _stats       = GetComponent<PlayerStats>();
        _hitReaction = GetComponent<HitReaction>();
    }

    private void Update()
    {
        if (_cooldownTimer > 0f) _cooldownTimer -= Time.deltaTime;

        // 朝向：有移动输入时更新，没有就保持上一次朝向
        float x = _input.Move.x;
        if (Mathf.Abs(x) > 0.1f) _facing = x > 0f ? 1 : -1;

        // 硬直期间不能攻击
        if (_hitReaction != null && _hitReaction.IsStunned) return;

        if (_input.ConsumeAttackPressed() && _cooldownTimer <= 0f && attackData != null)
        {
            _cooldownTimer = attackData.cooldown;
            Execute(attackData);
        }
    }

    /// <summary>执行一次攻击判定。将来技能系统调用的就是这个方法</summary>
    public void Execute(AttackData data)
    {
        Vector2 center = (Vector2)transform.position
                       + new Vector2(data.hitboxOffset.x * _facing, data.hitboxOffset.y);

        Collider2D[] hits = Physics2D.OverlapBoxAll(center, data.hitboxSize, 0f, data.targetLayers);

        // 同一次挥砍对同一个目标只结算一次
        var alreadyHit = new HashSet<IDamageable>();

        foreach (var hit in hits)
        {
            if (hit.gameObject == gameObject) continue;              // 不打自己
            if (!hit.TryGetComponent(out IDamageable target)) continue;
            if (!target.IsAlive) continue;
            if (!alreadyHit.Add(target)) continue;

            // 从 PlayerStats 取最终数值 —— 装备 / 技能 / 强化的加成都已经算进去了
            float attack     = _stats != null ? _stats.Get(StatType.Attack)         : 10f;
            float critChance = (_stats != null ? _stats.Get(StatType.CritChance)    : 0f)
                             + data.bonusCritChance;
            float critMul    = _stats != null ? _stats.Get(StatType.CritMultiplier) : 1.5f;

            bool isCrit = Random.value < critChance;
            float damage = attack * data.damageMultiplier * (isCrit ? critMul : 1f);

            target.TakeDamage(new DamageInfo
            {
                Amount          = damage,
                SourcePosition  = transform.position,
                KnockbackForce  = data.knockbackForce,
                Attacker        = gameObject,
                IsCritical      = isCrit,
            });
        }
    }

    private void OnDrawGizmosSelected()
    {
        if (attackData == null) return;

        Gizmos.color = new Color(1f, 0.35f, 0.35f, 0.7f);
        Vector2 center = (Vector2)transform.position
                       + new Vector2(attackData.hitboxOffset.x * _facing, attackData.hitboxOffset.y);
        Gizmos.DrawWireCube(center, attackData.hitboxSize);
    }
}