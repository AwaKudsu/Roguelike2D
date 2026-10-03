using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 持续伤害区域：地刺、岩浆、毒池都用它。
///
/// 它顺便证明了 IDamageable 是双向的 ——
/// 玩家用这套接口打敌人，环境也用同一套接口打玩家，两边代码一模一样。
/// </summary>
[RequireComponent(typeof(Collider2D))]
public class DamageZone : MonoBehaviour
{
    [Header("伤害")]
    [SerializeField] private float damagePerTick = 10f;

    [Tooltip("同一个目标每隔多久结算一次。建议略大于玩家的无敌帧(0.5s)")]
    [SerializeField] private float tickInterval = 0.6f;

    [Tooltip("击退力度。岩浆不该把人弹开，地刺可以给一点")]
    [SerializeField] private float knockbackForce = 0f;

    [Tooltip("⚠️ 能伤到的层。要打到玩家就选 Player")]
    [SerializeField] private LayerMask targetLayers;

    // 每个目标各自的计时：同区域里有多个目标时互不干扰
    private readonly Dictionary<Collider2D, float> _nextTickTime = new();

    private void Reset()
    {
        // 组件刚挂上时自动设成触发器，省一步手动操作
        if (TryGetComponent(out Collider2D col)) col.isTrigger = true;
    }

    private void OnTriggerStay2D(Collider2D other)
    {
        if (!other.TryGetComponent(out IDamageable target)) return;
        if (!target.IsAlive) return;
        if ((targetLayers.value & (1 << other.gameObject.layer)) == 0) return;

        if (_nextTickTime.TryGetValue(other, out float next) && Time.time < next) return;
        _nextTickTime[other] = Time.time + tickInterval;

        target.TakeDamage(new DamageInfo
        {
            Amount         = damagePerTick,
            SourcePosition = transform.position,
            KnockbackForce = knockbackForce,
            Attacker       = gameObject,
            IsCritical     = false,
        });
    }

    private void OnTriggerExit2D(Collider2D other)
    {
        _nextTickTime.Remove(other);
    }
}