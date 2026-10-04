using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 执行一次 AttackData 定义的攻击。
///
/// 它只做三件事：读输入 → 画判定框 → 把 DamageInfo 交给所有 IDamageable。
/// 「伤害多少」「打多大范围」来自 AttackData，「攻击力多高」来自 CharacterStats。
/// </summary>
[RequireComponent(typeof(PlayerInputReader))]
public class MeleeAttacker : MonoBehaviour
{
    [Header("当前使用的攻击（将来由技能系统切换）")]
    [SerializeField] private AttackData attackData;

    [Header("调试")]
    [Tooltip("勾上后即使不选中 Player，Scene 视图也会常驻显示判定框位置")]
    [SerializeField] private bool alwaysShowHitbox = false;

    [Tooltip("攻击瞬间的闪框持续时间")]
    [SerializeField] private float debugFlashDuration = 0.15f;

    private PlayerInputReader _input;
    private CharacterStats _stats;
    private HitReaction _hitReaction;
    private float _cooldownTimer;
    private int _facing = 1;

    private Vector2 _flashCenter;
    private Vector2 _flashSize;
    private float _flashUntil;

    private void Awake()
    {
        _input       = GetComponent<PlayerInputReader>();
        _stats       = GetComponent<CharacterStats>();
        _hitReaction = GetComponent<HitReaction>();
    }

    private void Update()
    {
        if (_cooldownTimer > 0f) _cooldownTimer -= Time.deltaTime;

        float x = _input.Move.x;
        if (Mathf.Abs(x) > 0.1f) _facing = x > 0f ? 1 : -1;

        if (_hitReaction != null && _hitReaction.IsStunned) return;

        if (_input.ConsumeAttackPressed() && _cooldownTimer <= 0f && attackData != null)
        {
            _cooldownTimer = GetCooldown(attackData);
            Execute(attackData);
        }
    }

    /// <summary>实际冷却 = 基础冷却 ÷ 攻速，再乘上冷却缩减</summary>
    private float GetCooldown(AttackData data)
    {
        if (_stats == null) return data.cooldown;

        float speed     = Mathf.Max(0.1f, _stats.Get(StatType.AttackSpeed));
        float reduction = Mathf.Clamp(_stats.Get(StatType.CooldownRate), 0f, 0.8f);

        return data.cooldown / speed * (1f - reduction);
    }

    /// <summary>执行一次攻击判定。将来技能系统调用的就是这个方法</summary>
    public void Execute(AttackData data)
    {
        Vector2 center = (Vector2)transform.position
                       + new Vector2(data.hitboxOffset.x * _facing, data.hitboxOffset.y);

        _flashCenter = center;
        _flashSize   = data.hitboxSize;
        _flashUntil  = Time.time + debugFlashDuration;

        Collider2D[] hits = Physics2D.OverlapBoxAll(center, data.hitboxSize, 0f, data.targetLayers);

        var alreadyHit = new HashSet<IDamageable>();

        foreach (var hit in hits)
        {
            if (hit.gameObject == gameObject) continue;
            if (!hit.TryGetComponent(out IDamageable target)) continue;
            if (!target.IsAlive) continue;
            if (!alreadyHit.Add(target)) continue;

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

    // ---------------- 调试可视化 ----------------

    private Vector2 CurrentHitboxCenter
    {
        get
        {
            if (attackData == null) return transform.position;
            return (Vector2)transform.position
                 + new Vector2(attackData.hitboxOffset.x * _facing, attackData.hitboxOffset.y);
        }
    }

    private void OnDrawGizmos()
    {
        if (Application.isPlaying && Time.time < _flashUntil)
        {
            Gizmos.color = new Color(1f, 0.9f, 0.2f, 1f);
            Gizmos.DrawWireCube(_flashCenter, _flashSize);
        }

        if (alwaysShowHitbox && attackData != null)
        {
            Gizmos.color = new Color(1f, 0.35f, 0.35f, 0.45f);
            Gizmos.DrawWireCube(CurrentHitboxCenter, attackData.hitboxSize);
        }
    }

    private void OnDrawGizmosSelected()
    {
        if (attackData == null) return;

        Vector2 center = CurrentHitboxCenter;

        Gizmos.color = new Color(1f, 0.35f, 0.35f, 0.25f);
        Gizmos.DrawCube(center, attackData.hitboxSize);
        Gizmos.color = new Color(1f, 0.35f, 0.35f, 0.8f);
        Gizmos.DrawWireCube(center, attackData.hitboxSize);

        Gizmos.color = new Color(1f, 1f, 1f, 0.5f);
        Gizmos.DrawLine(transform.position, center);
    }
}