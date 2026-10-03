/// <summary>
/// 所有「能被打的东西」的统一入口。
///
/// 攻击方只需要知道这个接口，完全不需要知道打的是敌人、玩家、木桶还是可破坏地形。
/// 这就是为什么以后加新敌人时，攻击代码一行都不用改。
/// </summary>
public interface IDamageable
{
    /// <summary>是否还活着。已死亡的目标不应再结算伤害</summary>
    bool IsAlive { get; }

    /// <summary>结算一次伤害</summary>
    void TakeDamage(DamageInfo info);
}