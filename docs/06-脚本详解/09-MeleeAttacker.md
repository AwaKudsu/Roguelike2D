# 09 · MeleeAttacker

## 一、头部信息表

| 项 | 内容 |
|---|---|
| 文件路径 | `Assets/_Project/Scripts/Combat/MeleeAttacker.cs` |
| 所属层 | ③ 攻击层 |
| 依赖 | `PlayerInputReader`、`CharacterStats`、`HitReaction`、`AttackData`、`DamageInfo`、`IDamageable` |
| 被谁依赖 | 无。将来的 `SkillSystem` 会调用它的公开方法 `Execute()` |
| 行数 | 148 行 |
| 最后更新 | Day 4 |

---

## 二、一句话定位

玩家执行攻击：读输入 → 画判定框 → 把伤害派发给所有能挨打的东西。

---

## 三、为什么需要它

### 如果没有它，按下 `J` 到敌人掉血之间是断的

`PlayerInputReader` 只知道「有人按了攻击键」，它不知道这是什么意思。`Health` 只知道「我的血量减了」，它不知道是谁打的。**中间缺一个翻译官。**

最粗暴的做法是让 `PlayerInputReader` 直接去找敌人：

```csharp
// 反面写法
if (attackPressed)
{
    Collider2D[] hits = Physics2D.OverlapBoxAll(transform.position + Vector3.right, size, 0f);
    foreach (var hit in hits)
    {
        var enemy = hit.GetComponent<EnemyController>();     // ← 知道对方是敌人
        if (enemy != null) enemy.TakeDamage(20);             // ← 知道怎么扣血
        var barrel = hit.GetComponent<Barrel>();             // ← 还得知道木桶
        if (barrel != null) barrel.Break();
        var boss = hit.GetComponent<BossController>();       // ← 还得知道 Boss
        if (boss != null) boss.TakeDamage(20);
    }
}
```

问题不在于「写起来麻烦」，而在于**这段代码会永久地粘住所有东西**：输入层知道了敌人、木桶、Boss 的存在。每加一种能被打的东西，都要回来改这段 `if` 链。

### 它的解法：只认一个接口

```csharp
if (!hit.TryGetComponent(out IDamageable target)) continue;
target.TakeDamage(info);
```

`IDamageable`（「能挨打」）是一个接口。**只要是「能挨打的东西」，不管它是敌人、木桶、Boss 还是可破坏的地形，都走这同一行。**

`MeleeAttacker` 从此**不知道**自己打的是什么。这不是巧合，是刻意设计：

> **它只做三件事：读输入、画判定框、把 `DamageInfo` 交给所有 `IDamageable`。**
> 「打的是谁」是 `IDamageable` 的事，「扣多少血」是 `Health` 的事，「数值从哪来」是 `CharacterStats` 的事。

### 它明确不做的四件事

划清边界比列功能更重要：

| 不做的事 | 归谁管 |
|---|---|
| 伤害怎么算、防御怎么减 | `Health.TakeDamage()` |
| 受击后闪白、击退、硬直 | `HitReaction` |
| 攻击力、暴击率是多少 | `CharacterStats` |
| 攻击范围、冷却、击退力度是多少 | `AttackData` |

所以将来你想加「受击音效」，改 `HitReaction`；想改伤害公式，改 `Health`；**改哪个都不用动 `MeleeAttacker`**。

### 它也是技能系统的入口

它有一个 `public void Execute(AttackData data)` 方法。这个方法**不读输入、不检查冷却**——它只负责「把这个数据执行一次」。

> 所以 Day 5 之后的技能系统，只要调用 `attacker.Execute(skillData)` 就能放技能，**绕过键盘、绕过基础冷却，走一套自己的冷却和蓝耗**。这个方法之所以是 `public`，就是为了这个。

---

## 四、代码全解

### 4.1 文件头（第 1–2 行）

```csharp
1: using System.Collections.Generic;
2: using UnityEngine;
```

| 行 | 代码 | 含义 |
|---|---|---|
| 1 | `using System.Collections.Generic;` | 引入「泛型集合」命名空间。本文件用到的 `HashSet<IDamageable>` 就住在这里。**它属于 .NET 基础库，不属于 Unity**，所以要单独引一行 |
| 2 | `using UnityEngine;` | 引入 Unity 命名空间：`MonoBehaviour`、`Vector2`、`Physics2D`、`Collider2D`、`Gizmos`、`Random`、`Time`、`Mathf` 全在这里 |

> **注意 `Random` 的归属问题。** `UnityEngine.Random` 和 `System.Random` 是两个完全不同的类。本文件第 93 行用的 `Random.value` 是 **Unity 的**（因为第 2 行引了 `UnityEngine`，第 1 行引的 `System.Collections.Generic` 里没有 `Random`）。
>
> 如果同时 `using System;` 和 `using UnityEngine;`，写 `Random` 会**歧义报错**，必须写成 `UnityEngine.Random.value`。本项目没引 `System`，所以不冲突。

### 4.2 类声明与特性（第 4–11 行）

```csharp
4: /// <summary>
5: /// 执行一次 AttackData 定义的攻击。
6: ///
7: /// 它只做三件事：读输入 → 画判定框 → 把 DamageInfo 交给所有 IDamageable。
8: /// 「伤害多少」「打多大范围」来自 AttackData，「攻击力多高」来自 CharacterStats。
9: /// </summary>
10: [RequireComponent(typeof(PlayerInputReader))]
11: public class MeleeAttacker : MonoBehaviour
```

| 行 | 代码 | 含义 |
|---|---|---|
| 4–9 | `/// <summary> ... </summary>` | XML 文档注释，不影响运行，只给 IDE 悬停提示用 |
| 10 | `[RequireComponent(typeof(PlayerInputReader))]` | **强制依赖特性**。它要求「挂了这个脚本的物体上，必须也有 `PlayerInputReader`」 |
| 11 | `public class MeleeAttacker : MonoBehaviour` | 继承 `MonoBehaviour` —— 因为**它有 `Update()`，每帧要跑逻辑**，所以必须挂在 GameObject 上 |

> ### `[RequireComponent]` 帮你挡掉了什么
>
> 它做两件事，都在**编辑器里**、写代码时就生效：
>
> 1. **自动补组件**：你把 `MeleeAttacker` 拖到物体上时，如果这个物体没有 `PlayerInputReader`，Unity **会自动加上去**。你不需要手动加两遍。
> 2. **禁止删除**：反过来，你想删掉 `PlayerInputReader` 时，Unity 会弹窗说「`MeleeAttacker` 依赖它，不能删」。
>
> **为什么需要它**：第 35 行 `_input = GetComponent<PlayerInputReader>();` 如果取不到，`_input` 就是 `null`，第 44 行 `_input.Move.x` 会抛 `NullReferenceException`，而且是在运行时才炸、报错位置指向第 44 行——**你会去查第 44 行，但真正的问题在「组件没挂」**。
>
> `[RequireComponent]` 把这类错误从「运行时崩溃」提前到了「挂组件时就拦住」。

### 4.3 字段区（第 13–31 行）

```csharp
13:     [Header("当前使用的攻击（将来由技能系统切换）")]
14:     [SerializeField] private AttackData attackData;
15:
16:     [Header("调试")]
17:     [Tooltip("勾上后即使不选中 Player，Scene 视图也会常驻显示判定框位置")]
18:     [SerializeField] private bool alwaysShowHitbox = false;
19:
20:     [Tooltip("攻击瞬间的闪框持续时间")]
21:     [SerializeField] private float debugFlashDuration = 0.15f;
22:
23:     private PlayerInputReader _input;
24:     private CharacterStats _stats;
25:     private HitReaction _hitReaction;
26:     private float _cooldownTimer;
27:     private int _facing = 1;
28:
29:     private Vector2 _flashCenter;
30:     private Vector2 _flashSize;
31:     private float _flashUntil;
```

| 行 | 代码 | 含义 |
|---|---|---|
| 13 | `[Header(...)]` | Inspector 分组标题 |
| 14 | `[SerializeField] private AttackData attackData;` | 当前使用的攻击数据。`[SerializeField]` 让 `private` 字段也显示在 Inspector 里，可以拖资产进去。**用 `private` 是为了防止别的脚本随便改它**（只有这个类自己能改） |
| 16 | `[Header("调试")]` | |
| 18 | `alwaysShowHitbox = false` | 常驻显示判定框的开关，默认关。**用 `bool` 类型 → Inspector 里是一个复选框** |
| 21 | `debugFlashDuration = 0.15f` | 攻击瞬间那个亮黄框闪多久。0.15 秒约等于 9 帧（60 FPS），**足够看清但不刺眼** |
| 23 | `private PlayerInputReader _input;` | 缓存输入组件的引用。**注意它没有 `[SerializeField]`** —— 它不需要在 Inspector 里拖，因为第 35 行会用 `GetComponent` 自动找到 |
| 24 | `private CharacterStats _stats;` | 缓存属性组件，用来读攻击力 / 暴击率 / 攻速 |
| 25 | `private HitReaction _hitReaction;` | 缓存受击组件，用来读 `IsStunned`（硬直期间不能攻击） |
| 26 | `private float _cooldownTimer;` | 冷却倒计时。**大于 0 表示还在冷却中，不能攻击**。它每帧在递减 |
| 27 | `private int _facing = 1;` | **朝向**。`1` 表示朝右，`-1` 表示朝左。**初始值必须是 1 而不是 0** —— 如果是 0，开局第一刀会打在角色正中心而不是身前 |
| 29–31 | `_flashCenter` / `_flashSize` / `_flashUntil` | 调试闪框用的三个缓存：框的中心、框的大小、框什么时候消失。它们在 `Execute()` 里被写入，在 `OnDrawGizmos()` 里被读取 |

> ### 命名约定：为什么有的字段带下划线，有的不带
>
> | 写法 | 含义 | 例子 |
> |---|---|---|
> | `_input` | **私有字段**，只能在类内部用 | `private PlayerInputReader _input;` |
> | `attackData` | **序列化字段**，能在 Inspector 里填 | `[SerializeField] private AttackData attackData;` |
> | `GetCooldown` | **方法**，首字母大写 | `private float GetCooldown(...)` |
> | `CurrentHitboxCenter` | **属性**，首字母大写 | `private Vector2 CurrentHitboxCenter` |
>
> C# 官方规范其实**不要求**私有字段加下划线（微软自己的代码里两种都有）。这是本项目的约定：**加了 `[SerializeField]` 的不加下划线**（因为它在 Inspector 里显示，下划线会让 Inspector 上出现 `_attackData` 这种丑名字），**纯私有的加下划线**。
>
> 一致性比选哪个更重要——三个月后回来看，你能一眼分辨「这个字段是配置项还是内部状态」。

> ### `int _facing` 为什么不用 `bool`？
>
> 因为要**直接参与乘法**。第 71 行：
>
> ```csharp
> data.hitboxOffset.x * _facing
> ```
>
> 朝右时 `0.8 × 1 = 0.8`，朝左时 `0.8 × (-1) = -0.8`。**一行完成翻转。**
>
> 用 `bool isFacingRight` 就得写成 `isFacingRight ? 0.8f : -0.8f`——多一个三元表达式，而且将来想做「四方向攻击」时 `bool` 就完全不够用了（`int` 还能表达上下的意思）。

### 4.4 `Awake()`（第 33–38 行）

```csharp
33:     private void Awake()
34:     {
35:         _input       = GetComponent<PlayerInputReader>();
36:         _stats       = GetComponent<CharacterStats>();
37:         _hitReaction = GetComponent<HitReaction>();
38:     }
```

| 行 | 代码 | 含义 |
|---|---|---|
| 33 | `private void Awake()` | Unity 生命周期方法。**物体被创建时调用一次**，比 `Start()` 早，比 `Update()` 早 |
| 35 | `_input = GetComponent<PlayerInputReader>();` | 在**同一个 GameObject 上**找 `PlayerInputReader` 组件并保存引用 |
| 36 | `_stats = GetComponent<CharacterStats>();` | 同上，找属性组件 |
| 37 | `_hitReaction = GetComponent<HitReaction>();` | 同上，找受击组件 |

> ### 为什么在 `Awake` 里缓存，而不是每帧 `GetComponent`？
>
> `GetComponent<T>()` 的工作方式是**遍历这个物体上的所有组件**，逐个比较类型。它不快——大概几十纳秒，但如果你在 `Update` 里每帧调三次，60 FPS 下就是每秒 180 次无意义的查找。
>
> 缓存成字段后，每帧只是读一个变量。**这是 Unity 里最基本的性能习惯。**
>
> ### 为什么用 `GetComponent` 而不是 `[SerializeField]` 拖引用？
>
> 两者都行，区别是：
>
> | | `GetComponent`（本文件） | `[SerializeField]` 拖拽 |
> |---|---|---|
> | 配置成本 | 零，自动 | 每个物体都要手动拖 |
> | 出错风险 | 组件不在就 `null` | 忘了拖也是 `null` |
> | 灵活性 | 只能找同一物体上的 | 可以引用别的物体 |
>
> 本文件三个组件**都必须在同一个物体上**（玩家身上的输入、属性、受击本来就是一套），所以 `GetComponent`「零配置」的优势更大。
>
> **判断标准**：依赖的东西「必然在同一物体上」→ 用 `GetComponent`；「可能在别处」→ 用 `[SerializeField]` 拖。

### 4.5 `Update()` — 主循环（第 40–54 行）

```csharp
40:     private void Update()
41:     {
42:         if (_cooldownTimer > 0f) _cooldownTimer -= Time.deltaTime;
43:
44:         float x = _input.Move.x;
45:         if (Mathf.Abs(x) > 0.1f) _facing = x > 0f ? 1 : -1;
46:
47:         if (_hitReaction != null && _hitReaction.IsStunned) return;
48:
49:         if (_input.ConsumeAttackPressed() && _cooldownTimer <= 0f && attackData != null)
50:         {
51:             _cooldownTimer = GetCooldown(attackData);
52:             Execute(attackData);
53:         }
54:     }
```

| 行 | 代码 | 含义 |
|---|---|---|
| 40 | `private void Update()` | **每渲染帧调用一次**。60 FPS 下每秒 60 次，144 Hz 屏幕上每秒 144 次 |
| 42 | `if (_cooldownTimer > 0f) _cooldownTimer -= Time.deltaTime;` | 冷却倒计时递减。`Time.deltaTime` 是「距离上一帧过了多少秒」，所以 `_cooldownTimer` 减的是**真实时间**，不是帧数——**换台电脑帧率不同，冷却时长也一样**。外面套 `if > 0f` 是为了让它停在 0，不会变成负数 |
| 44 | `float x = _input.Move.x;` | 从输入层取水平方向，范围 -1 到 1。**只取 `.x` 不要 `.y`** —— 横板游戏里上下输入不参与朝向判断 |
| 45 | `if (Mathf.Abs(x) > 0.1f) _facing = x > 0f ? 1 : -1;` | 有明确方向输入时才更新朝向 |
| 47 | `if (_hitReaction != null && _hitReaction.IsStunned) return;` | 硬直期间直接退出，**这帧不能做任何事**。`return` 会跳过后面的全部代码 |
| 49 | `if (_input.ConsumeAttackPressed() && _cooldownTimer <= 0f && attackData != null)` | 三个条件**全部为真**才攻击 |
| 51 | `_cooldownTimer = GetCooldown(attackData);` | 把冷却计时器充满。**注意是先设冷却再执行攻击**，这样即使 `Execute` 里出异常也不会漏掉冷却 |
| 52 | `Execute(attackData);` | 真正干活的一行 |
| 54 | `}` | `Update` 结束 |

> ### `Mathf.Abs(x) > 0.1f` 这个 0.1 是干什么的？
>
> 它叫**死区（dead zone）**。没有它，这一行会变成 `if (x != 0f)`，问题出在：
>
> 1. **手柄摇杆漂移**：手柄用久了，摇杆回中后会稳定输出 `0.03` 这种微小值。没有死区的话，角色会**自己缓慢朝一个方向转身**。
> 2. **键盘的矛盾输入**：玩家同时按住 `A` 和 `D` 时，输入系统的 2D 复合绑定可能输出 `0` 或极小的抖动值。没有死区的话，朝向会在两帧之间**疯狂左右闪烁**。
>
> 0.1 的意思是「输入强度超过 10% 才算你真的想往那边走」。**这是所有动作游戏都会有的一个常数**，只是名字通常叫 `inputDeadZone` 或 `movementThreshold`。
>
> ### 为什么用「输入方向」判断朝向，而不是看精灵有没有翻转？
>
> 因为 `_facing` 要用来算**判定框位置**（第 71 行），而判定框必须在**按键的那一刻**就摆对位置。
>
> 如果改成读 `SpriteRenderer.flipX`，问题在于：精灵翻转通常发生在动画状态机里，**比输入晚一帧甚至几帧**。玩家按下 `A` 又立刻按 `J` 的话，角色可能已经朝左了但精灵还没翻——**判定框会打在右边，打空**。
>
> **原则：凡是影响判定的数据，都要从输入直接推，不要从表现层反推。**

> ### `&&` 的顺序是有意的（第 49 行）
>
> C# 的 `&&` 是**短路求值**：左边为 `false` 时，右边**根本不执行**。
>
> 所以 `_input.ConsumeAttackPressed() && _cooldownTimer <= 0f && attackData != null` 的执行顺序是：
>
> 1. 先问「有按键吗」——没有就直接结束，**后面两个条件不检查**
> 2. 有按键，再问「冷却好了吗」——没好就结束
> 3. 冷却好了，最后问「`attackData` 拖了吗」——没拖就结束
>
> **这个顺序不能换。** `ConsumeAttackPressed()` 有副作用（它会「取走」按键），如果放最后，就会出现「冷却没好但按键已经被吃掉了」——玩家按的那一下**永久丢失**，必须松开重按。
>
> > ### `Consume` 的「取走」语义
> >
> > 看 `PlayerInputReader.cs` 里的实现：
> >
> > ```csharp
> > public bool ConsumeAttackPressed()
> > {
> >     if (!_attackPressedLatch) return false;   // 没有事件，返回 false
> >     _attackPressedLatch = false;              // 有事件：先清掉
> >     return true;                              // 再返回 true
> > }
> > ```
> >
> > **调一次返回 `true`，调第二次就返回 `false` 了。** 就像从信箱里把信拿走——拿过一次就没有了。
> >
> > 为什么需要这样？因为输入事件发生在 `Update`（每秒 144 次），而移动逻辑跑在 `FixedUpdate`（每秒 50 次）。如果只是设一个 `bool` 标记不清掉，`FixedUpdate` 连跑两次就会**消费同一个按键两次**，触发两次攻击。`Consume` 保证「一次按键，不多不少被处理一次」。

> ### 为什么攻击放在 `Update`，而移动放在 `FixedUpdate`？
>
> | | `Update` | `FixedUpdate` |
> |---|---|---|
> | 频率 | 跟渲染帧率（60~144 Hz） | 固定 50 Hz |
> | 适合 | **读输入、计时、判定** | **改物理速度、加力** |
>
> `MeleeAttacker` 干的事是「查一下有没有按键、倒计时减一点、画个框查碰撞」——**它不直接改 `Rigidbody2D` 的速度**（击退是 `HitReaction` 干的，那在 `FixedUpdate` 里）。
>
> 所以放在 `Update` 里响应**更快更跟手**：玩家按下 `J`，最多等一帧（16 毫秒）就出判定；如果放 `FixedUpdate`，最坏情况要等 20 毫秒。
>
> **判断标准：这段代码碰 `Rigidbody2D` 吗？碰 → `FixedUpdate`；不碰 → `Update`。**

### 4.6 `GetCooldown()`（第 56–65 行）

```csharp
56:     /// <summary>实际冷却 = 基础冷却 ÷ 攻速，再乘上冷却缩减</summary>
57:     private float GetCooldown(AttackData data)
58:     {
59:         if (_stats == null) return data.cooldown;
60:
61:         float speed     = Mathf.Max(0.1f, _stats.Get(StatType.AttackSpeed));
62:         float reduction = Mathf.Clamp(_stats.Get(StatType.CooldownRate), 0f, 0.8f);
63:
64:         return data.cooldown / speed * (1f - reduction);
65:     }
```

| 行 | 代码 | 含义 |
|---|---|---|
| 56 | `/// <summary>实际冷却 = ...</summary>` | 文档注释，一句话说清公式 |
| 57 | `private float GetCooldown(AttackData data)` | 私有方法，接收一个 `AttackData`，返回算好的秒数 |
| 59 | `if (_stats == null) return data.cooldown;` | **兜底**：物体上没挂 `CharacterStats` 时，直接用原始冷却，不做任何加工。没有这一行，第 61 行会抛 `NullReferenceException` |
| 61 | `float speed = Mathf.Max(0.1f, _stats.Get(StatType.AttackSpeed));` | 读攻速属性，**并且强制不低于 0.1** |
| 62 | `float reduction = Mathf.Clamp(_stats.Get(StatType.CooldownRate), 0f, 0.8f);` | 读冷却缩减属性，**并强制夹在 0 到 0.8 之间** |
| 64 | `return data.cooldown / speed * (1f - reduction);` | 先除以攻速，再乘上「1 减冷却缩减」 |
| 65 | `}` | 方法结束 |

> ### `data.cooldown / speed` —— 为什么攻速是「除」不是「乘」？
>
> 因为 `AttackSpeed` 的定义是「攻击速度倍率」，攻速**越高，冷却越短**，所以是除法：
>
> ```
> AttackSpeed = 1.0  →  0.35 ÷ 1.0  =  0.350 秒
> AttackSpeed = 1.4  →  0.35 ÷ 1.4  =  0.250 秒   ← 攻速高了，冷却短了
> ```
>
> 如果写成乘法，攻速越高冷却越长，就反了。
>
> ### 用你项目的真实数字算一遍
>
> 同一个 `Attack_Basic`（`cooldown = 0.35`），两个职业：
>
> | 职业 | `AttackSpeed` | 实际冷却 | 每秒可打 | 单次伤害 | **DPS** |
> |---|---|---|---|---|---|
> | 战士 | `0.95` | `0.35 ÷ 0.95 ≈ 0.368 s` | 2.71 下 | 13 | **35.3** |
> | 法师 | `1.40` | `0.35 ÷ 1.40 = 0.250 s` | 4.00 下 | 11 | **44.0** |
>
> **法师 DPS 比战士高约 47%**，代价是血量只有 85（战士 140，少 39%）。
>
> **注意：这 47% 的差异完全来自一个数字。** 你改 `ClassData` 里的 `AttackSpeed` 就能调平衡，不需要碰这行代码。这就是数据驱动的意义。

> ### `Mathf.Max(0.1f, ...)` 防的是什么？
>
> 防的是**除以零**。
>
> 假如将来出了一件「诅咒装备」，效果是「攻击速度 -100%」，那么 `_stats.Get(StatType.AttackSpeed)` 会返回 `0`。第 64 行就变成：
>
> ```
> 0.35 / 0  =  Infinity（无穷大）
> ```
>
> `_cooldownTimer` 变成 `Infinity` 之后，第 42 行 `Infinity -= Time.deltaTime` 还是 `Infinity`，第 49 行 `Infinity <= 0f` 永远为 `false`——**玩家永久无法攻击，而且没有任何报错**。这种 bug 极难查，因为它看起来「一切正常」。
>
> `Mathf.Max(0.1f, x)` 的意思是「取 0.1 和 x 里更大的那个」。攻速最低被压到 0.1，最坏情况冷却变成 `0.35 ÷ 0.1 = 3.5 秒`——**慢得离谱，但至少能打**。
>
> > **通用原则**：凡是用属性值做**除法**，都要给分母兜一个下限。凡是用属性值做**乘法**，都要考虑它会不会变成 0。

> ### `Mathf.Clamp(..., 0f, 0.8f)` 防的是什么？
>
> 防的是**冷却缩减堆到 100% 以上**。
>
> 假如你后期加了「冷却缩减 +30%」的三件装备，总缩减到 `0.9`，那么：
>
> ```
> return 0.35 / 1.0 * (1f - 0.9) = 0.035 秒
> ```
>
> 每秒能打 28 下——**技能系统直接崩坏**，游戏变成「按住一个键看特效」。
>
> 夹到 `0.8` 意味着：**冷却最多缩短 80%，永远保留至少 20% 的冷却时间。** 这是所有有冷却缩减机制的游戏都会设的上限（DNF 里叫「冷却缩减上限」，原神里叫「元素充能效率软上限」）。
>
> `Mathf.Clamp(value, min, max)` 的规则：小于 `min` 返回 `min`，大于 `max` 返回 `max`，中间的原样返回。
>
> **注意它也顺手防了负数**：如果某件装备给了「-10% 冷却缩减」（反向效果），`Clamp` 会把 `-0.1` 变成 `0`，不会出现「冷却变长」这种奇怪结果。

> ### 为什么不把 `_stats.Get(...)` 的结果缓存起来？
>
> 保守的做法是在 `Awake` 里读一次攻速存成字段，攻击时直接读字段。**这个项目刻意不那么做。**
>
> 因为属性会**实时变化**：玩家在局内捡到一个「攻速 +20%」的词条，`CharacterStats` 立刻更新，但如果 `MeleeAttacker` 缓存了旧值，**这件装备要到下一局才生效**——玩家会以为捡到的东西没用。
>
> `Get()` 内部是「字典查找 + 遍历修饰符列表」，在攻击时（每秒最多 4 次）调用完全无压力。**这类开销远小于一次 `GetComponent`，不值得为它引入「缓存过期」这种更难查的 bug。**
>
> **判断标准：如果数据会变，就不要缓存；如果数据不变，缓存收益又不明显，也优先不缓存。缓存是优化手段，不是习惯。**

### 4.7 `Execute()` —— 核心（第 67–105 行）

```csharp
67:     /// <summary>执行一次攻击判定。将来技能系统调用的就是这个方法</summary>
68:     public void Execute(AttackData data)
69:     {
70:         Vector2 center = (Vector2)transform.position
71:                        + new Vector2(data.hitboxOffset.x * _facing, data.hitboxOffset.y);
72:
73:         _flashCenter = center;
74:         _flashSize   = data.hitboxSize;
75:         _flashUntil  = Time.time + debugFlashDuration;
76:
77:         Collider2D[] hits = Physics2D.OverlapBoxAll(center, data.hitboxSize, 0f, data.targetLayers);
78:
79:         var alreadyHit = new HashSet<IDamageable>();
80:
81:         foreach (var hit in hits)
82:         {
83:             if (hit.gameObject == gameObject) continue;
84:             if (!hit.TryGetComponent(out IDamageable target)) continue;
85:             if (!target.IsAlive) continue;
86:             if (!alreadyHit.Add(target)) continue;
87:
88:             float attack     = _stats != null ? _stats.Get(StatType.Attack)         : 10f;
89:             float critChance = (_stats != null ? _stats.Get(StatType.CritChance)    : 0f)
90:                              + data.bonusCritChance;
91:             float critMul    = _stats != null ? _stats.Get(StatType.CritMultiplier) : 1.5f;
92:
93:             bool isCrit = Random.value < critChance;
94:             float damage = attack * data.damageMultiplier * (isCrit ? critMul : 1f);
95:
96:             target.TakeDamage(new DamageInfo
97:             {
98:                 Amount          = damage,
99:                 SourcePosition  = transform.position,
100:                 KnockbackForce  = data.knockbackForce,
101:                 Attacker        = gameObject,
102:                 IsCritical      = isCrit,
103:             });
104:         }
105:     }
```

#### 第 68 行：为什么这个方法必须是 `public`

| 行 | 代码 | 含义 |
|---|---|---|
| 67 | `/// <summary>...</summary>` | 文档注释。**「将来技能系统调用的就是这个方法」这句话是给自己留的说明书** |
| 68 | `public void Execute(AttackData data)` | `public` = 外部可调用；`void` = 不返回值；参数是要执行的攻击数据 |

> `private` 的方法只有本类能调，`public` 的任何脚本都能调。
>
> **这个方法之所以开放，是因为它做了刻意的职责切分**：
>
> | 谁负责 | 内容 |
> |---|---|
> | `Update()` | 读键盘、查基础冷却、判断硬直 → **然后调 `Execute`** |
> | `Execute()` | 只负责「把这个 `AttackData` 执行一次」 |
>
> 所以技能系统要做的是：
>
> ```csharp
> // SkillSystem.cs（Day 5 之后）
> if (skillReady && manaEnough)
> {
>     mana -= skillData.manaCost;
>     attacker.Execute(skillData.attackData);   // ← 直接用，绕过键盘和基础冷却
> }
> ```
>
> 如果 `Execute` 是 `private`，技能系统就只能复制一份判定逻辑——**那就又回到「同样的代码有两份」的老问题了。**

#### 第 70–71 行：判定框的中心点怎么算

```csharp
70:         Vector2 center = (Vector2)transform.position
71:                        + new Vector2(data.hitboxOffset.x * _facing, data.hitboxOffset.y);
```

| 行 | 代码 | 含义 |
|---|---|---|
| 70 | `(Vector2)transform.position` | `transform.position` 是 `Vector3`（有 x/y/z）。**2D 物理只认 `Vector2`**，所以这里做一次显式类型转换，把 z 丢掉 |
| 71 | `new Vector2(data.hitboxOffset.x * _facing, data.hitboxOffset.y)` | 构造偏移向量。**`x` 乘了 `_facing`，`y` 没乘** |
| 70–71 | 两行相加 | 得到判定框在世界坐标里的中心点 |

> ### 为什么 `x` 乘 `_facing` 而 `y` 不乘？
>
> `_facing` 只有两个值：`1`（朝右）和 `-1`（朝左）。
>
> ```
> 朝右时：  x 偏移 = 0.8 × 1  =  0.8   → 框在角色右边
> 朝左时：  x 偏移 = 0.8 × (-1) = -0.8  → 框在角色左边   ✅
> ```
>
> 那 `y` 呢？假设 `hitboxOffset = (0.8, 0.5)`，意思是「往右 0.8、往上 0.5」。
>
> ```
> 如果 y 也乘 _facing：
>   朝右时：  y = 0.5 × 1  =  0.5   → 框在上方   ✅
>   朝左时：  y = 0.5 × (-1) = -0.5  → 框跑到下方了  ❌
> ```
>
> **角色转身改变的是「左右」，不改变「上下」。** 所以只有 `x` 参与翻转。
>
> > 这个 0.8 是**在 `AttackData` 里配的**，不是写死的。你如果想做一个「从头顶劈下来」的技能，把 `Hitbox Offset` 填成 `(0.3, 1.2)` 就行，**不用改这行代码**。

#### 第 73–75 行：记录闪框数据

```csharp
73:         _flashCenter = center;
74:         _flashSize   = data.hitboxSize;
75:         _flashUntil  = Time.time + debugFlashDuration;
```

| 行 | 代码 | 含义 |
|---|---|---|
| 73 | `_flashCenter = center;` | 把这次判定的位置存下来，供 `OnDrawGizmos` 画黄框 |
| 74 | `_flashSize = data.hitboxSize;` | 同上，存大小 |
| 75 | `_flashUntil = Time.time + debugFlashDuration;` | **记录「什么时候该停止画框」**。`Time.time` 是游戏开始到现在经过的秒数，加上 0.15 就是 0.15 秒之后 |

> **为什么记的是「结束时刻」而不是「剩余时长」？**
>
> 记结束时刻（`Time.time + 0.15`）的好处是：画框的时候只需要比较一次 `Time.time < _flashUntil`，**不需要任何倒计时变量，也不需要每帧去减**。
>
> 如果记剩余时长，就得在某个地方每帧递减——多一处状态、多一处可能出错的地方。
>
> **这是「绝对时间戳」相对「相对倒计时」的通用优势**：只要有一个稳定的时钟源，用绝对时间戳表达「什么时候结束」永远更简单。

#### 第 77 行：画框查碰撞

```csharp
77:         Collider2D[] hits = Physics2D.OverlapBoxAll(center, data.hitboxSize, 0f, data.targetLayers);
```

| 参数位 | 值 | 含义 |
|---|---|---|
| 返回值 | `Collider2D[] hits` | 一个**数组**，装着所有碰到的碰撞体。一个都没碰到时是**空数组**（不是 `null`） |
| 1 | `center` | 框的中心点（世界坐标） |
| 2 | `data.hitboxSize` | 框的大小（宽、高） |
| 3 | `0f` | **框的旋转角度（弧度）** |
| 4 | `data.targetLayers` | 只检查这些层上的碰撞体 |

> ### 第 3 个参数 `0f` 是什么？
>
> 是**矩形的旋转角度**，单位是弧度（不是角度）。
>
> `0f` 表示不旋转——横板游戏的攻击判定框都是正正方方的。这个参数存在的意义是给上帝视角游戏用的（比如角色的武器斜着挥，判定框也跟着斜）。
>
> **记住这个位置就行**：`OverlapBoxAll(中心, 大小, 旋转, 层)`。
>
> ### `All` 后缀是什么意思？
>
> Unity 的物理查询有一套命名约定：
>
> | 方法 | 返回 |
> |---|---|
> | `OverlapBox(...)` | 碰到的**第一个** `Collider2D`，或 `null` |
> | `OverlapBoxAll(...)` | 碰到的**全部** `Collider2D`，装在数组里 |
>
> 攻击要一次打到多个敌人，所以用 `All`。
>
> 顺带一提：`OverlapBoxAll` 会**分配一个新数组**，产生垃圾回收压力。每秒最多调 4 次（受冷却限制），在这个规模下完全不值得优化。**不要提前优化——先让它跑起来。**

#### 第 79 行与 86 行：`HashSet` 去重

```csharp
79:         var alreadyHit = new HashSet<IDamageable>();
86:             if (!alreadyHit.Add(target)) continue;
```

| 行 | 代码 | 含义 |
|---|---|---|
| 79 | `var alreadyHit = new HashSet<IDamageable>();` | 新建一个「已命中集合」。`var` 是**类型推断**，编译器从右边自动推出类型是 `HashSet<IDamageable>`，不用手写两遍 |
| 86 | `if (!alreadyHit.Add(target)) continue;` | **这一行同时做两件事**：去重 + 跳过重复 |

> ### 为什么必须去重？
>
> **一个敌人身上可能挂着多个 `Collider2D`。**
>
> 你现在的 `Enemy` 只有一个 `CapsuleCollider2D`，但很快会变成：
>
> ```
> Enemy (GameObject)
> ├── Capsule Collider 2D      ← 身体，用于和地面/玩家碰撞
> ├── Box Collider 2D          ← 受击盒，通常比身体大一点，好打中
> └── Circle Collider 2D       ← 弱点，打中伤害更高（将来）
> ```
>
> 这时 `OverlapBoxAll` 会**把这三个都返回回来**。不去重的话，敌人会**一刀挨三次伤害**——而且这个 bug 只在「给敌人加了第二个碰撞体」的那一刻突然出现，非常难联想到原因。
>
> > ### `HashSet.Add()` 返回 `bool` —— 这是 C# 里很漂亮的一个设计
> >
> > 大多数语言的集合 `Add` 是「void，永远加进去」，要判断重复得先 `Contains` 再 `Add`，**查两次**。
> >
> > C# 的 `HashSet<T>.Add()` 返回一个 `bool`：
> >
> > | 情况 | 返回 | 集合变化 |
> > |---|---|---|
> > | 这个元素**不在**集合里 | `true` | 加进去了 |
> > | 这个元素**已经在**集合里 | `false` | **什么都没发生** |
> >
> > 所以 `alreadyHit.Add(target)` 一次调用同时回答了两个问题：「加成功了吗」和「之前有吗」——**它们是同一个问题。**
> >
> > 而 `!` 是逻辑非：加失败（= 已经打过了）→ `!false = true` → `continue` 跳过。
> >
> > 等价于下面这段，但少了一次查找：
> >
> > ```csharp
> > if (alreadyHit.Contains(target)) continue;   // 查一次
> > alreadyHit.Add(target);                      // 再写一次
> > ```
>
> ### `HashSet` 和 `List` 的区别
>
> | | `List<T>` | `HashSet<T>` |
> |---|---|---|
> | 存东西 | 有序，可重复 | **无序，自动去重** |
> | `Contains` 速度 | 慢（要遍历全部） | 极快（哈希表） |
> | `Add` 返回值 | `void` | **`bool`（是否新增成功）** |
>
> 这里只需要「快速判断某个东西在不在」+「自动去重」，`HashSet` 正好。

#### 第 81–86 行：四次 `continue` 守卫

```csharp
81:         foreach (var hit in hits)
82:         {
83:             if (hit.gameObject == gameObject) continue;
84:             if (!hit.TryGetComponent(out IDamageable target)) continue;
85:             if (!target.IsAlive) continue;
86:             if (!alreadyHit.Add(target)) continue;
```

| 行 | 代码 | 含义 |
|---|---|---|
| 81 | `foreach (var hit in hits)` | 逐个遍历命中的碰撞体。`var` 推断出 `hit` 是 `Collider2D` |
| 83 | `if (hit.gameObject == gameObject) continue;` | **跳过自己**。判定框画在角色身前，很容易把自己身上的碰撞体也框进去 |
| 84 | `if (!hit.TryGetComponent(out IDamageable target)) continue;` | **跳过不能挨打的**。比如地面、装饰物 |
| 85 | `if (!target.IsAlive) continue;` | **跳过已经死的**。尸体在销毁前的几帧还在场上 |
| 86 | `if (!alreadyHit.Add(target)) continue;` | **跳过这一刀已经打过的** |
| 82 / 87 | `{` / 空行 | 循环体的开始与结束 |

> ### `continue` 是什么意思
>
> `continue` 的意思是「**这一轮循环到此为止，直接进入下一轮**」——它后面的代码全部跳过。
>
> 和它容易混的是 `break`：
>
> | 关键字 | 效果 |
> |---|---|
> | `continue` | 跳过**本次**迭代的剩余部分，继续下一轮 |
> | `break` | **彻底结束整个循环** |
| `return` | 结束**整个方法** |
>
> 你项目里两个地方各用了一次，是刻意的：
>
> - `MeleeAttacker` 用 `continue` → **一次挥击打到几个敌人就打几个**（横扫）
> - `EnemyController.Attack()` 用 `break` → **敌人一次只打一个目标**（避免敌人一刀秒掉抱团的玩家……虽然只有一个玩家，但语义上更保守）
>
> ### 为什么这四行要写成「守卫子句」而不是嵌套 `if`
>
> 反面写法：
>
> ```csharp
> if (hit.gameObject != gameObject)
> {
>     if (hit.TryGetComponent(out IDamageable target))
>     {
>         if (target.IsAlive)
>         {
>             if (alreadyHit.Add(target))
>             {
>                 // 真正的逻辑在这里，缩进已经四层了
>             }
>         }
>     }
> }
> ```
>
> 四层缩进之后，**核心逻辑被推到了屏幕右边**，一眼看不出重点。
>
> 「守卫子句」（guard clause）的思路是：**把所有「不满足就滚蛋」的条件平铺在最上面**，一旦通过，下面的代码可以毫无顾虑地执行。这是 C# 里非常推荐的写法，你在 `Health`、`DamageZone`、`RunManager` 里都会看到同样的模式。

> ### `TryGetComponent(out IDamageable target)` 的 `out` 是什么
>
> `out` 是 C# 的**输出参数**关键字。它的意思是「这个方法会往这个变量里塞东西」。
>
> | 写法 | 行为 |
> |---|---|
| `GetComponent<IDamageable>()` | 找不到返回 `null`；**如果找到多个会返回第一个，但仍然可能为 null 且不告诉你** |
| `TryGetComponent(out IDamageable target)` | **返回 `bool` 表示找没找到**；找到时把结果塞进 `target`，没找到时 `target` 为 `null` |
>
> 好处是**一次调用同时拿到「有没有」和「是什么」**，而且名字里的 `Try` 是 C# 的命名惯例——**所有 `Try` 开头的方法都返回 `bool` 并且不抛异常**（对比 `int.Parse` 会抛异常，`int.TryParse` 返回 `false`）。
>
> **泛型参数写成接口 `IDamageable` 而不是具体类型，是这一行的灵魂。** 它意味着：不管命中物是敌人、木桶还是 Boss，只要它实现了 `IDamageable` 接口，这一行就能取到。
>
> ### `IsAlive` 判断为什么必要
>
> 因为敌人死亡时执行的是 `Destroy(gameObject, 0.3f)`——**延迟 0.3 秒才真的删掉**。
>
> 这 0.3 秒里，敌人的碰撞体还在场上。如果不检查 `IsAlive`：
>
> - 玩家对着尸体再砍一刀 → 触发 `TakeDamage` → 血量已经是 0 → `Health.TakeDamage` 里第一行 `if (!IsAlive) return;` 会挡掉
> - **但是** `HitReaction` 依然会闪白、会击退——**尸体会被打得满地乱飞**
>
> 所以两处都要挡：`MeleeAttacker` 这里挡一次（省掉无谓的调用），`Health.TakeDamage` 里再挡一次（防止其他调用方漏挡）。**这叫「防御性编程」——每一层都假设上一层可能出错。**

#### 第 88–94 行：伤害计算

```csharp
88:             float attack     = _stats != null ? _stats.Get(StatType.Attack)         : 10f;
89:             float critChance = (_stats != null ? _stats.Get(StatType.CritChance)    : 0f)
90:                              + data.bonusCritChance;
91:             float critMul    = _stats != null ? _stats.Get(StatType.CritMultiplier) : 1.5f;
92:
93:             bool isCrit = Random.value < critChance;
94:             float damage = attack * data.damageMultiplier * (isCrit ? critMul : 1f);
```

| 行 | 代码 | 含义 |
|---|---|---|
| 88 | `_stats != null ? _stats.Get(StatType.Attack) : 10f` | **三元运算符**：`条件 ? 真时的值 : 假时的值`。有属性组件就读真实攻击力，没有就用兜底值 10 |
| 89–90 | 暴击率 = 职业暴击率 **+ 技能自带暴击率** | 两行加起来是**一个表达式**，括号里是三元的兜底 0，然后 `+ data.bonusCritChance` |
| 91 | `critMul` | 暴击倍率，兜底 1.5（= 暴击打 150% 伤害） |
| 93 | `Random.value < critChance` | 掷骰子判暴击 |
| 94 | `attack * data.damageMultiplier * (isCrit ? critMul : 1f)` | **最终伤害公式** |

> ### 为什么每处 `_stats` 都写 `!= null ? ... : 默认值`？
>
> 因为 `MeleeAttacker` 应该在任何物体上都能工作——**即使那个物体没挂 `CharacterStats`**。
>
> 最典型的场景是**测试和原型**：你想快速搭一个「能砍东西的方块」验证判定逻辑，不想同时配 `CharacterStats`、`ClassData`、`Health` 一整套。有兜底值，这个方块插上 `MeleeAttacker` 加个 `PlayerInputReader` 就能用。
>
> 第 59 行 `GetCooldown` 里也是同一个思路。
>
> ⚠️ **但这不是「随便写写」**——兜底值本身要合理：攻击力兜底 `10f`（和 `CharacterStats` 的默认基础值一致），暴击倍率兜底 `1.5f`（和默认一致）。**兜底值和默认值对齐**，才能保证「挂了组件」和「没挂组件」的行为差异不至于离谱。

> ### 暴击为什么写成「两处相加」（第 89–90 行）
>
> ```
> 最终暴击率 = 职业暴击率 + 技能自带暴击率
> ```
>
> 例：法师职业暴击率 15%，用了一个「自带 10% 额外暴击」的技能 → 25%。
>
> **这是「加算」叠加。** 将来有了装备，可能是这样：
>
> ```
> 最终暴击率 = 职业 + 技能 + 装备（三个来源加算）
> ```
>
> 为什么不设计成乘法？因为暴击率是**线性感知**的数值——玩家觉得「15% + 10% = 25%」是符合直觉的。而伤害加成通常用乘法，因为「堆得越多收益越大」才符合养成游戏的期待。
>
> **这条界限在 `ClassData` 和 `StatModifier` 里体现得更清楚**（`Flat` / `PercentAdd` / `PercentMultiply` 三种叠加模式）。

> ### `Random.value` 为什么能这样判暴击
>
> `Random.value` 返回一个 **0 到 1 之间的浮点数**（可以返回 0，但基本不会返回正好 1）。
>
> ```
> critChance = 0.25（25% 暴击率）
>
> Random.value 落在 [0, 0.25) 的概率 = 25%   →  < 0.25 成立  →  暴击
> Random.value 落在 [0.25, 1) 的概率 = 75%   →  < 0.25 不成立 →  不暴击
> ```
>
> **这是「用均匀分布模拟概率」的标准做法**：把 0~1 的区间按概率切成段，随机点落在哪段就是哪个结果。
>
> > ⚠️ 注意这个 `Random` 是 **`UnityEngine.Random`**（全局随机数生成器）。如果将来要做「固定种子可复现的地牢生成」，**不能用它**——因为全局状态无法保存和还原。那种场景要用 `new System.Random(seed)`，把种子存下来。这是 `00-总览` 里提到的「可复现性」要求的由来。

> ### 伤害公式逐项拆解（第 94 行）
>
> ```csharp
> float damage = attack * data.damageMultiplier * (isCrit ? critMul : 1f);
> ```
>
> 假设战士（攻击力 13）用普攻（倍率 1），暴击倍率 1.5：
>
> | 情况 | 计算 | 结果 |
> |---|---|---|
> | 不暴击 | `13 × 1 × 1` | **13** |
> | 暴击 | `13 × 1 × 1.5` | **19.5** |
>
> 假设法师（攻击力 11）用「火球」（假设倍率 2.0），暴击倍率 1.8：
>
> | 情况 | 计算 | 结果 |
> |---|---|---|
> | 不暴击 | `11 × 2.0 × 1` | **22** |
> | 暴击 | `11 × 2.0 × 1.8` | **39.6** |
>
> **注意这一行里没有任何一个写死的数字**——`attack` 来自 `CharacterStats`（职业 / 装备 / 强化决定），`damageMultiplier` 来自 `AttackData`（技能决定），`critMul` 来自 `CharacterStats`。三个来源在**这一行汇合**。
>
> 想加「元素克制倍率」？在这一行末尾再乘一个系数即可。**这一行是伤害的唯一出口。**

#### 第 96–103 行：打包并派发

```csharp
96:             target.TakeDamage(new DamageInfo
97:             {
98:                 Amount          = damage,
99:                 SourcePosition  = transform.position,
100:                 KnockbackForce  = data.knockbackForce,
101:                 Attacker        = gameObject,
102:                 IsCritical      = isCrit,
103:             });
```

| 行 | 代码 | 含义 |
|---|---|---|
| 96 | `target.TakeDamage(new DamageInfo { ... })` | **只认接口，不认类型**。这一行是整个架构解耦的核心 |
| 96–103 | `new DamageInfo { 字段 = 值, ... }` | **对象初始化器**语法：新建结构体的同时给字段赋值 |
| 98 | `Amount = damage` | 第 94 行算出来的伤害 |
| 99 | `SourcePosition = transform.position` | **攻击者所在位置**。这一项的唯一用途是让 `HitReaction` 算出击退方向 |
| 100 | `KnockbackForce = data.knockbackForce` | 从 `AttackData` 读（玩家 7，敌人 9） |
| 101 | `Attacker = gameObject` | 是谁打的。现在没被用到，但将来要支持「击杀掉落」「仇恨」「伤害统计」都要靠它 |
| 102 | `IsCritical = isCrit` | 是否暴击。现在只存着，将来伤害飘字要用它决定颜色 |

> ### `new DamageInfo { ... }` 这个语法
>
> 叫**对象初始化器**。等价于：
>
> ```csharp
> DamageInfo info = new DamageInfo();
> info.Amount = damage;
> info.SourcePosition = transform.position;
> info.KnockbackForce = data.knockbackForce;
> info.Attacker = gameObject;
> info.IsCritical = isCrit;
> target.TakeDamage(info);
> ```
>
> 7 行变 8 行（还要多一个临时变量名），而且**看不出「这些都是同一个东西的属性」**。
>
> 对象初始化器的价值在于：**它把「构造一个对象」变成一次完整的表达式**，可以当参数直接传，一眼能看出所有字段被赋了什么值。
>
> ⚠️ 注意 `DamageInfo` 是 **`struct`（结构体）**，不是 `class`（类）。结构体是「值类型」——传进 `TakeDamage` 时是**复制一份**，方法里改它不会影响外面的。这对伤害数据是正确的：一次伤害是「一份快照」，不该被后续修改。

> ### `SourcePosition = transform.position` 为什么要单独传位置
>
> 因为 `HitReaction` 要算**击退方向**：
>
> ```csharp
> Vector2 away = (Vector2)transform.position - info.SourcePosition;
> away.Normalize();
> _rb.linearVelocity = new Vector2(away.x * info.KnockbackForce, ...);
> ```
>
> 受击者的位置 **减去** 攻击者的位置 = **从攻击者指向受击者的方向** = 被打飞的方向。
>
> 如果只传 `Attacker`（那个 GameObject），`HitReaction` 也能自己 `Attacker.transform.position` 取到——但那要求攻击者在受击发生时**还活着**。如果打断的瞬间攻击者被销毁了（比如同归于尽），取位置就会抛 `MissingReferenceException`。
>
> **传值比传引用安全**：`DamageInfo` 里存的是那一刻的位置快照，不管之后攻击者怎样了，击退方向都是对的。

### 4.8 调试可视化（第 107–147 行）

#### 4.8.1 `CurrentHitboxCenter` 属性（第 109–117 行）

```csharp
107:     // ---------------- 调试可视化 ----------------
108:
109:     private Vector2 CurrentHitboxCenter
110:     {
111:         get
112:         {
113:             if (attackData == null) return transform.position;
114:             return (Vector2)transform.position
115:                  + new Vector2(attackData.hitboxOffset.x * _facing, attackData.hitboxOffset.y);
116:         }
117:     }
```

| 行 | 代码 | 含义 |
|---|---|---|
| 107 | `// ---------------- 调试可视化 ----------------` | **分节注释**。项目里所有脚本都用这个格式分段，方便快速跳转 |
| 109 | `private Vector2 CurrentHitboxCenter` | 声明一个**属性**（property），只读 |
| 111 | `get` | 属性的**读取访问器**。有人读 `CurrentHitboxCenter` 时，执行 `{ }` 里的代码 |
| 113 | `if (attackData == null) return transform.position;` | 没配攻击数据时，返回角色自身位置，避免空引用 |
| 114–115 | 和 `Execute` 第 70–71 行**完全一样**的算法 | 算出当前朝向下判定框的中心 |

> ### 属性的 `get` 是什么
>
> 属性是「长得像字段，其实是方法」的东西。外部用起来是 `obj.CurrentHitboxCenter`（不写括号），但每次读都会**重新计算**。
>
> 对比一下三种写法：
>
> | 写法 | 调用方式 | 每次读会重算吗 |
> |---|---|---|
> | 字段 `private Vector2 _center;` | `_center` | 不会，读的是存好的值 |
> | 属性 `private Vector2 Center { get { ... } }` | `Center` | **会**，每次读都跑一遍 `get` |
> | 方法 `private Vector2 GetCenter()` | `GetCenter()` | 会，但调用时看得出「这是个动作」 |
>
> 这里选属性而非字段，是因为**朝向随时在变**——存成字段就得在 `Update` 里每帧同步一次，白费性能而且容易忘记同步。写成属性，「永远是最新的」是天然的。
>
> ### 为什么容忍它和 `Execute` 里的算法重复？
>
> **因为两者的使用场景完全不同**：
>
> - `Execute` 第 70–71 行：**每次攻击算一次**，算完立刻用，还要顺手存进 `_flashCenter`
> - `CurrentHitboxCenter`：**每帧被 Gizmos 读一次**（只在编辑器里，且只在选中或开了 `alwaysShowHitbox` 时）
>
> 抽成一个共用方法当然可以，但收益极小，代价是 `Execute` 里那句 `_flashCenter = center;` 会变得别扭（要先调方法拿值，那 `_flashCenter` 存什么？）。
>
> **判断标准：重复的三行代码，如果抽取后让任一方的逻辑变绕，就不抽。** DRY（Don't Repeat Yourself）是手段不是目的——**为消除重复而牺牲可读性，是亏本买卖。**

#### 4.8.2 `OnDrawGizmos()`（第 119–132 行）

```csharp
119:     private void OnDrawGizmos()
120:     {
121:         if (Application.isPlaying && Time.time < _flashUntil)
122:         {
123:             Gizmos.color = new Color(1f, 0.9f, 0.2f, 1f);
124:             Gizmos.DrawWireCube(_flashCenter, _flashSize);
125:         }
126:
127:         if (alwaysShowHitbox && attackData != null)
128:         {
129:             Gizmos.color = new Color(1f, 0.35f, 0.35f, 0.45f);
130:             Gizmos.DrawWireCube(CurrentHitboxCenter, attackData.hitboxSize);
131:         }
132:     }
```

| 行 | 代码 | 含义 |
|---|---|---|
| 119 | `private void OnDrawGizmos()` | **Unity 编辑器专用回调**。每帧在 Scene 视图重绘时调用，**对场景里所有对象都调用**（不管选没选中） |
| 121 | `if (Application.isPlaying && Time.time < _flashUntil)` | 两个条件：**正在运行游戏** 且 **闪框还没过期** |
| 123 | `Gizmos.color = new Color(1f, 0.9f, 0.2f, 1f);` | 设置画线的颜色。四个参数是 **红、绿、蓝、透明度**（都是 0~1）。`(1, 0.9, 0.2, 1)` 是**亮黄色** |
| 124 | `Gizmos.DrawWireCube(_flashCenter, _flashSize);` | 画一个**空心线框矩形**。`Wire` = 线框，对比 `DrawCube` = 实心 |
| 127 | `if (alwaysShowHitbox && attackData != null)` | 常驻显示开关打开了 且 配了攻击数据 |
| 129 | `new Color(1f, 0.35f, 0.35f, 0.45f)` | 暗红色、**半透明**（透明度 0.45）。半透明是为了不挡住角色 |
| 130 | `Gizmos.DrawWireCube(CurrentHitboxCenter, attackData.hitboxSize);` | 在当前朝向的判定框位置画框 |

> ### 🔑 为什么用 `OnDrawGizmos` 而不是 `OnDrawGizmosSelected`？
>
> 这是本文件里最重要的一个设计选择：
>
> | 方法 | 什么时候被调用 |
> |---|---|
> | `OnDrawGizmosSelected()` | **只在物体被选中时** |
> | `OnDrawGizmos()` | **永远**（场景里每个对象每帧都调） |
>
> 攻击闪框必须用 `OnDrawGizmos`，因为**攻击的时候你不可能正好选中着 Player**——你正在操作角色、盯着 Game 视图看效果。用 `OnDrawGizmosSelected` 的话，那个框永远只在你检查配置时出现，**真正需要它的时候它不在**。
>
> **代价**：`OnDrawGizmos` 对场景里所有对象都跑，对象多了会拖慢编辑器。所以第 121 行的第一个条件 `Application.isPlaying` 很重要——**编辑模式下这个框不画**，省掉不必要的开销。
>
> ### `Application.isPlaying` 是什么
>
> 一个 `bool` 静态属性：游戏正在运行时是 `true`，在编辑器里编辑时是 `false`。
>
> 第 121 行需要它是因为：`_flashUntil` 在编辑模式下永远是 0（没人调 `Execute`），`Time.time < 0` 本来就是 `false`。**但加上这个判断更明确**——它表达的是「闪框只在运行时有意义」这个意图。
>
> 第 127 行的 `alwaysShowHitbox` 分支就**没有** `Application.isPlaying` 判断——因为常驻显示在编辑模式下**正需要**（你在摆位置、调 `hitboxOffset` 的时候就要看框）。

> ### 攻击瞬间的亮黄框：它是「真正结算伤害的那一次判定」
>
> 三个字段的配合是：
>
> ```
> Execute() 被调用         →  _flashCenter = 框中心
>                            _flashSize   = 框大小
>                            _flashUntil  = 现在 + 0.15 秒
>
> 之后 0.15 秒内的每一帧   →  OnDrawGizmos 发现 Time.time < _flashUntil
>                            →  在 _flashCenter 画一个 _flashSize 的亮黄框
>
> 0.15 秒后                →  Time.time >= _flashUntil
>                            →  不画了
> ```
>
> **这个框和 `alwaysShowHitbox` 那个暗红框有本质区别：**
>
> | | 亮黄框 | 暗红框 |
> |---|---|---|
> | 什么时候出现 | **攻击真正发生的那一瞬** | 一直显示 |
> | 代表什么 | **这次判定用的框** | 如果现在攻击，框会在哪 |
> | 用途 | 确认「有没有打中」 | 确认「框的位置对不对」 |
>
> 这两个用途是不同的，所以两个都要有。

#### 4.8.3 `OnDrawGizmosSelected()`（第 134–147 行）

```csharp
134:     private void OnDrawGizmosSelected()
135:     {
136:         if (attackData == null) return;
137:
138:         Vector2 center = CurrentHitboxCenter;
139:
140:         Gizmos.color = new Color(1f, 0.35f, 0.35f, 0.25f);
141:         Gizmos.DrawCube(center, attackData.hitboxSize);
142:         Gizmos.color = new Color(1f, 0.35f, 0.35f, 0.8f);
143:         Gizmos.DrawWireCube(center, attackData.hitboxSize);
144:
145:         Gizmos.color = new Color(1f, 1f, 1f, 0.5f);
146:         Gizmos.DrawLine(transform.position, center);
147:     }
```

| 行 | 代码 | 含义 |
|---|---|---|
| 134 | `private void OnDrawGizmosSelected()` | 只在**选中这个物体**时调用 |
| 136 | `if (attackData == null) return;` | 没配数据就不画 |
| 138 | `Vector2 center = CurrentHitboxCenter;` | 取一次当前位置。**存进局部变量是因为下面要连用三次**，属性每次读都会重算 |
| 140 | `new Color(1f, 0.35f, 0.35f, 0.25f)` | 很淡的红（透明度 0.25） |
| 141 | `Gizmos.DrawCube(center, attackData.hitboxSize);` | 画**实心**方块。半透明实心 → 看起来像一层淡淡的色块，能看出框的范围又不太挡视线 |
| 142 | `new Color(1f, 0.35f, 0.35f, 0.8f)` | 同一个红色，但透明度 0.8（接近不透明） |
| 143 | `Gizmos.DrawWireCube(center, attackData.hitboxSize);` | 在同一位置再画一个**线框**。**实心 + 线框叠起来**，边界清晰、内部也有提示 |
| 145 | `new Color(1f, 1f, 1f, 0.5f)` | 半透明白 |
| 146 | `Gizmos.DrawLine(transform.position, center);` | 从角色中心画一条线到判定框中心 |

> ### 第 145–146 行的白线是干什么的
>
> 它让你一眼看出**偏移方向和距离**。
>
> 当你觉得「框的位置偏了」，可能是 `hitboxOffset.x` 太大或太小。有这条线，你能直观判断：
>
> - 线很短 → 框贴着角色，说明 `hitboxOffset.x` 太小
> - 线很长 → 框离得太远，打不到近处的敌人
>
> 没有这条线，你只能看着一个孤立的方框猜它偏了多少。
>
> ### 为什么这个框要用「实心 + 线框」两遍绘制
>
> `Gizmos.DrawCube` 画的是实心方块。如果只画实心，半透明的色块边界会很模糊（尤其是深色背景下）。
> `Gizmos.DrawWireCube` 画的是线框，边界清晰但内部是空的。
>
> **两个叠在一起，就得到了「边界清晰 + 内部有提示」的效果。** 这是 Scene 视图调试可视化的常见手法——你在 `PlayerController` 的地面检测圆、`EnemyController` 的三层感知圈里会看到同样的思路。

---

## 五、在 Unity 里怎么配

### 5.1 挂载位置

挂在 **`Player`** 物体上（和 `PlayerInputReader`、`CharacterStats`、`HitReaction`、`Health` 同一个物体）。

> 因为 `[RequireComponent(typeof(PlayerInputReader))]`，你拖上去时 Unity 会自动补 `PlayerInputReader`（如果还没有的话）。

### 5.2 Inspector 字段

#### 当前使用的攻击

| 字段 | 填什么 | 为什么 |
|---|---|---|
| `Attack Data` | 拖入 `Assets/_Project/Data/Weapons/Attack_Basic` | `Attack_Basic` 的 `Target Layers` 必须是 `Enemy` ⚠️ |

#### 调试

| 字段 | 建议值 | 说明 |
|---|---|---|
| `Always Show Hitbox` | **调参时勾上，平时关掉** | 勾上后即使不选中 Player，Scene 视图也能看到暗红框 |
| `Debug Flash Duration` | `0.15` | 攻击闪框持续时间。觉得看不清可以调到 `0.25` |

### 5.3 怎么用判定框排查问题

这是本脚本最实用的部分。**分三步，顺序不要跳：**

**第一步：确认框能看见**

| 检查项 | 说明 |
|---|---|
| 在 **Scene 视图** | 不是 Game 视图。Gizmos 只在 Scene 里画 |
| 在 Hierarchy 里**单击选中 `Player`** | 选中子物体 `Main Camera` 不行 |
| Scene 视图右上角的 **`Gizmos` 按钮是亮的** | 关掉的话整个场景的辅助线全没了 |
| `Attack Data` 槽**不是空的** | 空了的话第 136 行直接 `return` |

**第二步：用暗红框把位置调对**

1. 勾上 `Always Show Hitbox`
2. 在 Scene 视图里左右移动角色，观察框的翻转
3. 觉得框偏了就改 `Attack_Basic` 的 `Hitbox Offset`；觉得框小了就改 `Hitbox Size`

**第三步：进游戏，看亮黄框**

按 ▶ 运行，按 `J`，观察那个一闪而过的亮黄框：

| 你看到什么 | 说明 | 该改哪 |
|---|---|---|
| 黄框和目标**重叠**，目标有反应 | ✅ 一切正常 | 不用改 |
| 黄框和目标**重叠**，但目标**没反应** | ❌ **Layer 问题** | 去查 `AttackData.Target Layers` 和目标的 `Layer` |
| 黄框和 target **擦肩而过** | ❌ 几何问题 | 改 `Hitbox Size` 或 `Hitbox Offset` |
| 黄框**根本不出现** | ❌ 攻击没触发 | 查输入、查冷却、查 `_hitReaction.IsStunned` |

> ### 🔑 最关键的一条：**黄框重叠却打不动 = Layer 问题，不是尺寸问题**
>
> 这是最容易白忙半天的分叉口。
>
> **判定框是几何问题，Layer 是过滤问题。** 几何正确（框重叠了）但没打中，说明碰撞查询在**返回结果之前**就把目标过滤掉了——那个过滤条件就是 `LayerMask`。
>
> 这时候继续调 `Hitbox Size` 是完全没有意义的：你把框调得再大，被 Layer 过滤掉的东西**依然被过滤掉**。
>
> **看到黄框重叠就说明几何已经对了，立刻停止动尺寸。**

### 5.4 完整依赖清单

`MeleeAttacker` 要正常工作，`Player` 上必须有：

```
Player
├── Rigidbody 2D               ← 有，Day 1 配的
├── Capsule Collider 2D        ← 有
├── Player Input Reader        ← 有
├── Player Controller          ← 有
├── Character Stats            ← 有（Day 4 加的）
├── Health                     ← 有
├── Hit Reaction               ← 有
└── Melee Attacker             ← 本脚本
```

缺任何一个都不会报错，而是**功能性降级**：

| 缺什么 | 后果 |
|---|---|
| `PlayerInputReader` | 挂不上去（`RequireComponent` 会拦） |
| `CharacterStats` | 用兜底值：攻击力 10、暴击率 0、暴击倍率 1.5 |
| `HitReaction` | 第 47 行永远为假，硬直期间**仍然能攻击**（不算错，只是少了打断） |
| `Health` | 能打别人，但自己不会死 |

---

## 六、踩过的坑

> **现象**：按 `J` 打靶子完全没反应，Console 干干净净。
>
> **根因**：`Attack_Basic` 的 `Target Layers` 留空。`Physics2D.OverlapBoxAll` 返回空数组，`foreach` 一次都不循环。
>
> **解法**：`Target Layers` 勾上 `Enemy`，并确认靶子的 `Layer` 也是 `Enemy`。
>
> **排查建议**：先看 Scene 视图里**亮黄框有没有出现**。
> - 黄框不出现 → 问题在「攻击没触发」（输入 / 冷却 / 硬直）
> - 黄框出现但没伤害 → 问题在「Layer 过滤」或「目标不能挨打」
>
> **这一个「看黄框」的动作，把问题域从「整个攻击系统」缩小到了「两个分支之一」。**

> **现象**：给敌人加了第二个 `BoxCollider2D`（受击盒）之后，一刀能打出**两次伤害**。
>
> **根因**：`OverlapBoxAll` 把敌人身上的两个碰撞体都返回了。第 79 行的 `HashSet` 去重正是为这件事准备的——**如果当时没写，这个 bug 会一直潜伏到加受击盒的那一天才爆发**，而且极难联想到原因。
>
> **解法**：第 86 行 `if (!alreadyHit.Add(target)) continue;` 已经把两个碰撞体归并成同一个 `IDamageable`。
>
> **教训**：**去重的对象是 `IDamageable`（逻辑实体），不是 `Collider2D`（物理形状）。** 一个逻辑实体可以有多个物理形状。

---

## 七、如果要改，改这里

### 1. 加「法师火球」（Day 5 计划）

**`MeleeAttacker` 一行都不用改。**

- 新建一个 `AttackData` 资产，比如 `Attack_Fireball`，把 `Hitbox Size` 调小（火球只打一个点）、`Damage Multiplier` 调高
- 新建 `Projectile.cs`，读同样的 `AttackData` 字段，但把判定从「即时矩形」改成「飞行体碰撞」
- 由 `SkillSystem` 决定按哪个键放哪个技能

> 注意 `Projectile` **不该挂在 Player 身上**，它是独立的飞行物预制体。它需要的是自己的 `IDamageable` 检测（用 `OnTriggerEnter2D`），不是 `OverlapBoxAll`。

### 2. 加「多段连击」

在 `Update` 里维护一个连击计数：

```csharp
private int _comboIndex;
private float _comboResetTimer;

// 攻击时
_comboIndex = (_comboIndex + 1) % comboChain.Length;
Execute(comboChain[_comboIndex]);
```

`comboChain` 是一个 `AttackData[]`，在 Inspector 里拖三个不同的 `AttackData` 进去。

> ⚠️ 要额外处理「连击窗口」：`_comboResetTimer` 超时后 `_comboIndex` 要归零，否则玩家慢悠悠点三下也能打出第三段。

### 3. 加「攻击前摇」（对齐动画的挥砍时机）

看 `AttackData` 文档第七节第 3 条。核心是要在延迟期间检查 `_hitReaction.IsStunned` 和 `isActiveAndEnabled`——**人被打断了就不该继续挥刀**。

### 4. 想让攻击带「元素伤害」

1. `AttackData` 加 `element` 字段
2. `DamageInfo` 加 `Element` 字段（结构体加字段不用改任何调用点）
3. `Health.TakeDamage` 里按元素做抗性计算

`MeleeAttacker` 要做的事只有一件：第 96–103 行那个初始化器里加一行 `Element = data.element,`。

### 5. 想改「攻击时角色不能移动」

在 `PlayerController` 里加一个「攻击中」判断，或者给 `MeleeAttacker` 加一个 `IsAttacking` 属性，让 `PlayerController` 读。

> ⚠️ 这会显著改变手感。动作游戏里「攻击时能小幅移动」通常比「完全定住」手感好——**先做完全定住，自己玩十分钟，再决定要不要放开。**

### 6. 别忘了文档

改完代码，同步改这份文档，并把头部信息表的「最后更新」改成新的 Day 编号。
