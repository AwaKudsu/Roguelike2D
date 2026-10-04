# 10 · DamageZone

## 一、头部信息表

| 项 | 内容 |
|---|---|
| 文件路径 | `Assets/_Project/Scripts/Combat/DamageZone.cs` |
| 所属层 | ③ 攻击层 |
| 依赖 | `IDamageable`、`DamageInfo` |
| 被谁依赖 | 无 |
| 行数 | 57 行 |
| 最后更新 | Day 4 |

---

## 二、一句话定位

持续伤害区域：地刺、岩浆、毒池都用它。

---

## 三、为什么需要它

### 它证明了一件事：`IDamageable` 是双向的

这个项目里有两个「造成伤害的东西」：

| 谁 | 打谁 |
|---|---|
| `MeleeAttacker` | 玩家 → 敌人 |
| `DamageZone` | 环境 → 玩家 |

**它们是两个完全不同的脚本，写在不同时间，没有任何互相引用。** 但它们的核心那一行是**一模一样的**：

```csharp
// MeleeAttacker.cs 第 96 行
target.TakeDamage(new DamageInfo { ... });

// DamageZone.cs 第 43 行
target.TakeDamage(new DamageInfo { ... });
```

这就是接口的价值。`DamageZone` 不知道踩上来的是玩家还是敌人还是别的什么，它只知道「这个东西能挨打」。

> **如果没有 `IDamageable`**，`DamageZone` 就得写成：
>
> ```csharp
> if (other.TryGetComponent(out PlayerController player)) player.TakeDamage(damagePerTick);
> else if (other.TryGetComponent(out EnemyController enemy)) enemy.TakeDamage(damagePerTick);
> // 每加一种能受伤的东西，回来改一次
> ```
>
> 而有了接口，这一整段变成一行。**而且这句话不是「设计模式课上的说法」——它是这个项目里能跑起来的代码。**

### 顺带它也是「玩家会死」这件事的最小实现

在 `DamageZone` 出现之前，玩家**根本不可能受伤**：唯一的伤害来源 `MeleeAttacker` 是玩家打别人的。没有敌人 AI 之前，玩家永远不会掉血，`RunManager` 的死亡重开逻辑也就无从验证。

`DamageZone` 用 57 行补上了这个缺口，让「打 → 被打 → 死 → 重开」的完整循环**在 Day 3 就闭合了**，不用等敌人 AI。

---

## 四、代码全解

### 4.1 文件头与类型声明（第 1–12 行）

```csharp
1: using System.Collections.Generic;
2: using UnityEngine;
3:
4: /// <summary>
5: /// 持续伤害区域：地刺、岩浆、毒池都用它。
6: ///
7: /// 它顺便证明了 IDamageable 是双向的 ——
8: /// 玩家用这套接口打敌人，环境也用同一套接口打玩家，两边代码一模一样。
9: /// </summary>
10: [RequireComponent(typeof(Collider2D))]
11: public class DamageZone : MonoBehaviour
12: {
```

| 行 | 代码 | 含义 |
|---|---|---|
| 1 | `using System.Collections.Generic;` | 第 26 行的 `Dictionary<TKey, TValue>` 住在这里 |
| 2 | `using UnityEngine;` | `MonoBehaviour`、`Collider2D`、`LayerMask`、`Physics2D` 相关的都在这里 |
| 4–9 | `/// <summary> ... </summary>` | XML 文档注释，不影响运行 |
| 10 | `[RequireComponent(typeof(Collider2D))]` | 强制要求同物体上有 `Collider2D` |
| 11 | `public class DamageZone : MonoBehaviour` | 继承 `MonoBehaviour`——因为它需要 Unity 的碰撞回调（`OnTriggerStay2D`） |
| 12 | `{` | 类体的开始 |

> ### `[RequireComponent(typeof(Collider2D))]` 和 `MeleeAttacker` 那个有什么不同
>
> `MeleeAttacker` 写的是 `typeof(PlayerInputReader)` —— 一个具体类型，Unity 能**自动创建**它。
>
> 这里写的是 `typeof(Collider2D)` —— 一个**抽象基类**。`BoxCollider2D`、`CircleCollider2D`、`CapsuleCollider2D` 都是它的子类。Unity **没法自动创建抽象类**，所以这个特性的实际效果是：
>
> - ❌ 不会自动补碰撞体
> - ✅ **但会阻止你在有 `DamageZone` 的物体上删掉碰撞体**（弹窗提示依赖关系）
>
> 也就是说：**你仍然必须自己手动加一个 `BoxCollider2D`**。这个特性只是提醒你「别忘了」。第 28–32 行的 `Reset()` 会帮你把 `isTrigger` 打开，但碰撞体本身还是要你自己加。

> ### 为什么必须有 `Collider2D`
>
> `OnTriggerStay2D` 是**碰撞回调**——它由 Unity 的物理系统在「两个碰撞体重叠」时发出。**没有碰撞体，这个区域在物理世界里是不存在的**，函数永远不会被调用。
>
> > 这和 Day 1 踩过的那个坑是同一件事：**看得见 ≠ 撞得到**。`SpriteRenderer` 负责渲染，`Collider2D` 负责物理，两者完全独立。

### 4.2 配置字段（第 13–23 行）

```csharp
13:     [Header("伤害")]
14:     [SerializeField] private float damagePerTick = 10f;
15:
16:     [Tooltip("同一个目标每隔多久结算一次。建议略大于玩家的无敌帧(0.5s)")]
17:     [SerializeField] private float tickInterval = 0.6f;
18:
19:     [Tooltip("击退力度。岩浆不该把人弹开，地刺可以给一点")]
20:     [SerializeField] private float knockbackForce = 0f;
21:
22:     [Tooltip("⚠️ 能伤到的层。要打到玩家就选 Player")]
23:     [SerializeField] private LayerMask targetLayers;
```

| 行 | 代码 | 含义 |
|---|---|---|
| 13 | `[Header("伤害")]` | Inspector 分组标题 |
| 14 | `damagePerTick = 10f` | **每一次结算扣多少血**，不是每秒。默认 10 意味着玩家 100 血要踩 10 下才死 |
| 16 | `[Tooltip(...)]` | 悬停提示。**提示里那句话是给自己留的警告**，下面会详细讲 |
| 17 | `tickInterval = 0.6f` | **同一个目标每隔多久被结算一次**，单位秒。0.6 略大于玩家无敌帧 0.5 |
| 19–20 | `knockbackForce = 0f` | 击退力度，**默认 0**。理由见下面的关键决策 |
| 22–23 | `targetLayers` | 能伤到哪些层。**没有默认值，新建时全不勾** ⚠️ |

> ### 🔑 `tickInterval = 0.6f` 和玩家无敌帧 `0.5f` 的关系
>
> **这是本文件最容易配错、症状最迷惑人的一个数字。**
>
> 玩家的 `Health` 组件上有 `invincibilityDuration = 0.5f`：**受伤后 0.5 秒内免疫一切后续伤害**。
>
> `DamageZone` 每 `tickInterval` 秒结算一次。如果这两个数字配成：
>
> | 配置 | 会发生什么 |
> |---|---|
> | `tickInterval = 0.6`（当前） | 每 0.6 秒打一次，每次都落在无敌帧之外 → **每次都生效** ✅ |
> | `tickInterval = 0.5` | **正好卡在边界**。浮点误差会让部分 tick 落在无敌帧内被挡掉 → 掉血时快时慢，看起来像随机 |
> | `tickInterval = 0.2` | 每 0.2 秒尝试打一次，但每 5 次里有 4 次被无敌帧挡掉 → **实际掉血速度和 0.5 秒一次完全一样**，但白白跑了 5 倍的逻辑 |
> | `tickInterval = 1.5` | 明显变慢，玩家能站在地刺上思考人生 |
>
> **规则：`tickInterval` 必须略大于目标的 `invincibilityDuration`。**
>
> 最坑的是第三种情况：**功能上「能用」**（确实在掉血），但你以为「踩得越久掉血越快」这个设计意图完全没有实现——而代码里看不出任何问题。
>
> > **通用原则**：当两个系统的**节流机制**叠加时（这里是 `tickInterval` 和无敌帧），必须确认哪一个真正生效。**真正生效的是「周期更长」的那个。**

> ### 为什么 `knockbackForce` 默认是 0
>
> 因为 `DamageZone` 是一个「通用持续伤害区域」，而不同场景需要完全相反的击退行为：
>
> | 场景 | 击退应该是 | 理由 |
> |---|---|---|
> | **岩浆** | `0` | 岩浆是「站在里面持续掉血」，不应该把人弹出去——否则玩家反而能靠击退逃命 |
> | **地刺** | `3 ~ 5` | 地刺应该有一次明显的「扎一下」的反馈 |
> | **毒池** | `0` | 毒是持续状态，不该有物理位移 |
> | **Boss 的冲击波地面** | `10+` | 应该把人推开 |
>
> **默认值必须选「最保守」的那个。** 如果默认给 `5`，所有做岩浆的人都会忘记改成 0，结果岩浆变成弹射器。
>
> > **默认值设计原则**：默认值应该让「忘记配置」的后果最小。`0`（无效果）比 `5`（错误效果）安全得多。

### 4.3 计时字典（第 25–26 行）

```csharp
25:     // 每个目标各自的计时：同区域里有多个目标时互不干扰
26:     private readonly Dictionary<Collider2D, float> _nextTickTime = new();
```

| 行 | 代码 | 含义 |
|---|---|---|
| 25 | `// ...` | 普通单行注释。解释了「为什么不用一个全局计时器」，下面展开 |
| 26 | `private readonly Dictionary<Collider2D, float> _nextTickTime = new();` | 「碰撞体 → 下一次可以扣血的时刻」的映射表 |

> ### 逐块拆解第 26 行
>
> | 部分 | 含义 |
> |---|---|
> | `private` | 只有本类能访问 |
> | `readonly` | **这个字段只能在声明时或构造函数里赋值，之后不能重新指向别的字典**。注意：`readonly` **不锁内容**——你仍然可以往里 `Add`、`Remove`、改值。它锁的是「这个变量不能再指向一个新字典」 |
> | `Dictionary<Collider2D, float>` | 键是碰撞体，值是 `float`（一个时间戳）。`<K, V>` 是**泛型语法**：尖括号里声明键和值的类型 |
> | `_nextTickTime` | 私有字段，加下划线 |
> | `= new();` | **目标类型 `new` 表达式**（C# 9 引入）。编译器能从「左边的类型」推断出右边要构造什么，所以不用重复写一遍 `new Dictionary<Collider2D, float>()` |
>
> `readonly` 在这里是**防止自己犯错**：如果哪天在 `OnTriggerExit2D` 里手滑写成 `_nextTickTime = new Dictionary<...>()`，编译器会直接报错。**它把「误操作」变成了「编译不过」。**

> ### 🔑 为什么是「每个碰撞体各自计时」，而不是一个全局计时器？
>
> **错误做法**：用一个字段 `private float _nextTickTime;`，所有目标共用。
>
> ```csharp
> // ❌ 反面写法
> if (Time.time < _nextTickTime) return;
> _nextTickTime = Time.time + tickInterval;
> target.TakeDamage(...);
> ```
>
> 问题出在**同一区域里有多个目标**的时候。最典型的场景是：
>
> ```
> 0.0s  玩家踩上地刺  →  扣血，_nextTickTime = 0.6
> 0.1s  玩家召唤的宠物也踩上来  →  检查：Time.time(0.1) < 0.6  →  return，不扣血
> 0.6s  只有「谁先碰到」的那个会触发扣血
> ```
>
> 结果就是：**后来踩上来的目标几乎不掉血**，因为它们的检查总是撞在「别人刚设过的全局冷却」上。
>
> 而且更糟的是——**哪个目标能扣血取决于谁先碰到**，行为不可预测，你调试验证时结果会时好时坏。
>
> **正确做法**：字典让每个碰撞体有**自己独立的下次结算时刻**。
>
> ```csharp
> 玩家     →  自己的 next = 0.6
> 宠物     →  自己的 next = 0.7
> ```
>
> 两者互不干扰，各自按自己的节奏掉血。
>
> > ### 键为什么用 `Collider2D` 而不是 `GameObject` 或 `IDamageable`
> >
> > | 候选项 | 问题 |
> > |---|---|
> > | `GameObject` | 能用，但 `OnTriggerExit2D` 给你的本来就是这个 `other`（`Collider2D`），每次都要 `.gameObject` 转一次 |
> > | `IDamageable` | ❌ **不能用**——接口类型的哈希值在 Unity 里不稳定，而且同一个物体上多个碰撞体可能映射到同一个 `IDamageable`，反而会互相干扰 |
> > | `Collider2D` | ✅ **物理回调天然给你的是它**，直接当键用，零转换 |
> >
> > **原则：字典的键，优先选「你手上现成的那个东西」。** 每多一次转换，就多一个可能出错的地方。

### 4.4 `Reset()`（第 28–32 行）

```csharp
28:     private void Reset()
29:     {
30:         // 组件刚挂上时自动设成触发器，省一步手动操作
31:         if (TryGetComponent(out Collider2D col)) col.isTrigger = true;
32:     }
```

| 行 | 代码 | 含义 |
|---|---|---|
| 28 | `private void Reset()` | **Unity 编辑器专属回调**。它不是运行时方法 |
| 30 | `// ...` | 说明这个方法的用途 |
| 31 | `if (TryGetComponent(out Collider2D col)) col.isTrigger = true;` | 找到同物体上的碰撞体，把 `isTrigger` 设为 `true` |
| 32 | `}` | 方法结束 |

> ### 🔑 `Reset()` 什么时候被调用？
>
> **它只在编辑器里被调用，游戏运行时永远不会调用。** 具体是这两种时机：
>
> 1. **你在 Inspector 里第一次把 `DamageZone` 挂到物体上** —— 挂上的瞬间自动跑一次
> 2. **你在组件右上角的 `⋮` 菜单里点 `Reset`** —— 手动触发
>
> **它不会在**：场景加载时、物体被创建时、`AddComponent` 的代码调用时。
>
> ### 它为什么有用
>
> 因为「地刺应该是触发器」这件事是**必然的**：
>
> - **触发器（`isTrigger = true`）**：物体不阻挡移动，只是重叠时发出事件 → 玩家能「走进」地刺里
> - **实体碰撞体（`isTrigger = false`）**：物体挡住移动 → 玩家会**站在地刺上面**，永远碰不到它
>
> 你手动加 `BoxCollider2D` 时，Unity 默认给的是 `isTrigger = false`（实心）。忘了勾的话，玩家会站在地刺上——**而且看不出哪里错了**，因为地刺的贴图还是画在那里的。
>
> `Reset()` 把这个必然的设置自动化了：**只要你挂了 `DamageZone`，触发器状态就一定是对的。**
>
> > ### 用 `Reset()` 而不是在 `Awake()` 里设，有什么区别
> >
> > 如果在 `Awake()` 里写 `col.isTrigger = true`：
> > - 运行时能生效 ✅
> > - 但**在编辑器里看不出来**——你在 Inspector 里看到的 `Is Trigger` 还是没勾的状态，容易以为配置错了
> >
> > `Reset()` 是**写进 Inspector 的实际值**，所见即所得。**凡是「配置性质」的自动设置，都应该放 `Reset()`；凡是「运行时状态」的初始化，才放 `Awake()`。**

### 4.5 `OnTriggerStay2D()`（第 34–51 行）—— 核心

```csharp
34:     private void OnTriggerStay2D(Collider2D other)
35:     {
36:         if (!other.TryGetComponent(out IDamageable target)) return;
37:         if (!target.IsAlive) return;
38:         if ((targetLayers.value & (1 << other.gameObject.layer)) == 0) return;
39:
40:         if (_nextTickTime.TryGetValue(other, out float next) && Time.time < next) return;
41:         _nextTickTime[other] = Time.time + tickInterval;
42:
43:         target.TakeDamage(new DamageInfo
44:         {
45:             Amount         = damagePerTick,
46:             SourcePosition = transform.position,
47:             KnockbackForce = knockbackForce,
48:             Attacker       = gameObject,
49:             IsCritical     = false,
50:         });
51:     }
```

| 行 | 代码 | 含义 |
|---|---|---|
| 34 | `private void OnTriggerStay2D(Collider2D other)` | **Unity 物理回调**：只要有东西**持续停留**在这个触发器里，就**每物理帧调用一次**（默认 50 次/秒） |
| 35 | `{` | 方法体开始 |
| 36 | `if (!other.TryGetComponent(out IDamageable target)) return;` | 守卫 1：不能挨打的东西直接跳过 |
| 37 | `if (!target.IsAlive) return;` | 守卫 2：已经死了的跳过 |
| 38 | `if ((targetLayers.value & (1 << other.gameObject.layer)) == 0) return;` | 守卫 3：**层过滤** |
| 40 | `if (_nextTickTime.TryGetValue(other, out float next) && Time.time < next) return;` | 检查这个目标的「下次可扣血时刻」到了没 |
| 41 | `_nextTickTime[other] = Time.time + tickInterval;` | 把它的下次时刻推到 0.6 秒后 |
| 43–50 | `target.TakeDamage(new DamageInfo { ... })` | 派发伤害。**和 `MeleeAttacker` 第 96–103 行是同一个写法** |
| 45 | `Amount = damagePerTick` | 注意**每次的伤害是固定的**，和 `tickInterval` 无关。想让 DPS 翻倍，要么把 `damagePerTick` 翻倍，要么把 `tickInterval` 减半——**但后者受无敌帧限制，改不动**，所以应该改 `damagePerTick` |
| 46 | `SourcePosition = transform.position` | **区域自己的位置**，不是受击者的位置。这样击退方向是「从地刺中心向外」 |
| 48 | `Attacker = gameObject` | 地刺自己。将来做「被地刺杀死后统计死因」时会用到 |

> ### 🔑 第 38 行的位运算逐位拆解
>
> ```csharp
> (targetLayers.value & (1 << other.gameObject.layer)) == 0
> ```
>
> 这一行是手动做「这个物体的层，在掩码里被勾选了吗」的判断。`Physics2D` 的查询方法（`OverlapBoxAll` 等）会在内部自动做这件事，但 `OnTriggerStay2D` **是 Unity 主动推给你的回调，没有过滤参数**——所以必须手动过滤。
>
> 逐步拆：
>
> **① `other.gameObject.layer`** —— 一个 0 到 31 的整数，是**层号**。
>
> 你项目的层号：
> ```
> 0  = Default
> 5  = UI
> 6  = Player
> 7  = Enemy
> 8  = Ground
> ```
>
> **② `1 << other.gameObject.layer`** —— `<<` 是**左移位运算符**。把数字 1 的二进制向左移 n 位：
>
> ```
> 玩家站在地刺上 →  layer = 6  →  1 << 6
>
> 1 的二进制：      0000 0000 0000 0000 0000 0000 0000 0001
> 左移 6 位：       0000 0000 0000 0000 0000 0000 0100 0000
> 转成十进制：      64
> ```
>
> **结果是一个「只有第 6 位是 1，其他全是 0」的数。** 这就是「Player 层」在掩码里的表示。
>
> **③ `targetLayers.value`** —— Inspector 里你勾的那些层，底层就是一个 32 位整数。
>
> 假设你在 `Target Layers` 里勾了 `Player`（第 6 位），`.value` 就是 `64`。
>
> **④ `&`（按位与）** —— 两个数逐位比较，**两位都是 1 结果才是 1**：
>
> ```
>   targetLayers.value :  0000 0000 0000 0000 0000 0000 0100 0000   (64)
>   (1 << 6)           :  0000 0000 0000 0000 0000 0000 0100 0000   (64)
>   ─────────────────────────────────────────────────────────────── & 
>   结果                :  0000 0000 0000 0000 0000 0000 0100 0000   (64)
> ```
>
> 结果 `64` ≠ 0 → **第 6 位被勾选了** → 条件 `== 0` 为 `false` → **不 return，继续扣血** ✅
>
> 反例——如果掩码里没勾 `Player`：
>
> ```
>   targetLayers.value :  0000 0000 0000 0000 0000 0000 0000 0000   (0，全不勾)
>   (1 << 6)           :  0000 0000 0000 0000 0000 0000 0100 0000   (64)
>   ─────────────────────────────────────────────────────────────── &
>   结果                :  0000 0000 0000 0000 0000 0000 0000 0000   (0)
> ```
>
> 结果 `0` → 条件为 `true` → **return，不扣血** ❌
>
> > ### 为什么用位运算而不是别的写法
> >
> > Unity **没有**提供 `LayerMask.Contains(layer)` 这样的方法（`LayerMask` 的 API 很薄）。位运算是最直接、最快的方式——**一次按位与 + 一次比较，没有函数调用开销**。
> >
> > 而且它在一个区域内每物理帧对每个目标跑一次，50 Hz 下这个开销是值得在意的：敌人在场时可能有十几个物体同时重叠。
> >
> > **注意 `&` 和 `&&` 的区别**（新手最容易搞混的一对）：
> >
> > | 运算符 | 名称 | 作用 |
> > |---|---|---|
> > | `&&` | 逻辑与 | 两个 `bool` 都要为真 |
> > | `&` | **按位与** | 两个整数**逐位**比较 |
> >
> > 第 38 行必须用 `&`（单个），因为两边都是整数，不是布尔值。

> ### 🔑 第 40 行的短路逻辑：第一次踩上来为什么会立刻扣血
>
> ```csharp
> if (_nextTickTime.TryGetValue(other, out float next) && Time.time < next) return;
> ```
>
> 这一行有一个很巧的地方：**它同时处理了「第一次接触」和「冷却中」两种情况。**
>
> `Dictionary.TryGetValue(key, out value)` 的行为：
>
> | 情况 | 返回值 | `next` 里的内容 |
> |---|---|---|
> | 字典里**有**这个键 | `true` | 对应的值 |
> | 字典里**没有**这个键 | `false` | `float` 的默认值（`0`） |
>
> `&&` 是**短路求值**——左边为 `false` 时，右边**根本不执行**。所以：
>
> **情况 A：玩家第一次踩上来**
>
> ```
> 字典里还没有玩家这个键
>   →  TryGetValue 返回 false
>   →  && 短路，右边的时间比较不执行
>   →  整个条件为 false
>   →  不 return ✅
>   →  执行第 41 行，记录 next = 现在 + 0.6
>   →  执行第 43 行，扣血
> ```
>
> **结果：踩上去的瞬间就掉血**（符合直觉）。
>
> **情况 B：玩家已经踩了 0.3 秒**
>
> ```
> 字典里有这个键，next = 0.6 秒后的某个时刻
>   →  TryGetValue 返回 true
>   →  && 继续判断右边：Time.time(0.3) < next(0.6)  →  true
>   →  整个条件为 true
>   →  return ❌，不扣血
> ```
>
> **情况 C：玩家已经踩了 0.7 秒**
>
> ```
> 字典里有这个键，next = 0.6
>   →  TryGetValue 返回 true
>   →  右边：Time.time(0.7) < 0.6  →  false
>   →  整个条件为 false
>   →  不 return ✅
>   →  第 41 行把 next 推到 1.3
>   →  扣血
> ```
>
> **这一行代码用 5 个 token 完成了「初始化 + 冷却检查」两件事**，而且不需要写「如果是第一次就特殊处理」的分支。

> ### 第 41 行为什么用「现在 + 间隔」而不是「现在」
>
> ```csharp
> _nextTickTime[other] = Time.time + tickInterval;   // ✅
> _nextTickTime[other] = Time.time;                  // ❌
> ```
>
> 如果存 `Time.time`（现在），那么第 40 行的判断就变成 `Time.time < Time.time` —— **永远是 `false`**，于是**每物理帧都扣血**（每秒 50 次），玩家踩上去瞬间暴毙。
>
> ### 为什么用 `Time.time` 而不是自己累加 `Time.deltaTime`
>
> ```csharp
> // ❌ 反面写法
> _timer += Time.deltaTime;
> if (_timer < tickInterval) return;
> _timer = 0f;
> ```
>
> 能用，但有两个问题：
>
> 1. **需要为每个目标维护一个额外的倒计时字段**（字典得存两个值，或者再开一个字典）
> 2. **浮点误差会累积**——每帧加 `0.02f`，几百帧后可能变成 `0.5999999` 或 `0.6000001`，导致某些 tick 早一帧或晚一帧
>
> `Time.time` 是「游戏开始到现在经过的总秒数」，由 Unity 统一维护，**不累积误差、不需要额外状态**。每次比较都是两个绝对时间戳比大小。
>
> > **通用原则**：判断「过了多久」时，优先用**绝对时间戳相减**，而不是**自己累加增量**。这和 `MeleeAttacker` 里用 `_flashUntil = Time.time + 0.15f` 是同一个思路。

### 4.6 `OnTriggerExit2D()`（第 53–56 行）

```csharp
53:     private void OnTriggerExit2D(Collider2D other)
54:     {
55:         _nextTickTime.Remove(other);
56:     }
```

| 行 | 代码 | 含义 |
|---|---|---|
| 53 | `private void OnTriggerExit2D(Collider2D other)` | Unity 回调：有东西**离开**这个触发器时调用一次 |
| 55 | `_nextTickTime.Remove(other);` | 把这个目标的计时记录删掉 |
| 56 | `}` | 方法结束 |

> ### 为什么必须 `Remove`？不删会怎样
>
> 两个后果，一个是内存问题，一个是逻辑问题。
>
> **后果一：内存泄漏（字典无限增长）**
>
> 字典的键是 `Collider2D` 引用。**字典持有引用 = 阻止垃圾回收。**
>
> 具体场景：你在 Day 5 之后做「随机地牢」，玩家进了 50 个房间，每个房间都有地刺，踩过 200 个敌人……**每一个曾经碰过地刺的碰撞体都会永远留在字典里**，即使那些物体早就被 `Destroy` 了。
>
> 内存不会立刻爆，但会缓慢增长，而且 `TryGetValue` 的哈希查找会随着字典变大而变慢。
>
> **后果二：逻辑错误（继承上次的计时）**
>
> 更隐蔽的是这个。假设玩家踩上地刺 → 离开 → 又踩回来：
>
> ```csharp
> // 没有 Remove 的话
> 第一次踩：  next = 0.6       扣血
> 0.2s 离开
> 0.3s 又踩回来：
>            TryGetValue → true, next = 0.6
>            Time.time(0.3) < 0.6  →  true  →  return，不扣血  ❌
> ```
>
> **玩家会发现自己「刚踩上去不掉血，站一会儿才开始掉」**——行为不可预测，而且很难复现（取决于上次踩的时机）。
>
> > ### 这类「因为没清理缓存导致的行为异常」有个通用名字
> >
> > 叫 **stale state（过期状态）**。它比内存泄漏更难查，因为：
> > - 没有报错
> > - 表现时有时无
> > - 单步调试时「看起来数据是对的」（确实有个 `next` 值，只是它是旧的了）
> >
> > **防御方式**：**每一个 `.Add` / `+=` / 赋值，都要能说出它的「对称清理」在哪里。**
> >
> > | 写入 | 对应的清理 |
> > |---|---|
> > | `_nextTickTime[other] = ...` | `OnTriggerExit2D` 里的 `Remove` |
> > | `health.Died += OnPlayerDied` | `OnDisable` 里的 `-=` |
> > | `Instantiate(prefab)` | `Destroy(obj)` 或对象池归还 |
> >
> > **如果找不到对称的清理点，那个写入大概率是有问题的。**
>
> ### `Remove` 的返回值这里没用
>
> `Dictionary.Remove(key)` 也返回 `bool`（删掉了 `true`，键不存在 `false`）。这里忽略它是对的——删一个不存在的键不算错误，这正是「退出时无脑清理」该有的行为。

---

## 五、在 Unity 里怎么配

### 5.1 搭建一片地刺（以「岩浆池」为例）

**① 创建物体**

`GameObject` → `2D Object` → `Sprites` → `Square`，命名 `Spike`

**② 摆放和缩放**

- 位置放在地面上、玩家会走过去的地方
- `Transform → Scale` 大概 `(3, 0.5, 1)` —— 一片横向的浅池
- `Sprite Renderer → Color` 调成**暗红色**（区别于普通地面）

**③ 碰撞体**

如果创建时自带了 `Box Collider 2D` 就留用；**如果没有，必须手动 `Add Component` 加一个**（`[RequireComponent]` 不会自动创建抽象类型）。

**④ 挂 `DamageZone` 组件**

挂上的一瞬间，`Reset()` 会自动把 `Box Collider 2D` 的 `Is Trigger` 勾上。**你可以留意一下这个自动变化**——这就是 `Reset()` 在起作用。

**⑤ 填 Inspector**

| 字段 | 填什么 | 说明 |
|---|---|---|
| `Damage Per Tick` | `10` | 每次扣 10 血 |
| `Tick Interval` | `0.6` | 每 0.6 秒一次。**必须 > 玩家的 `Invincibility Duration`（0.5）** |
| `Knockback Force` | `0`（岩浆）/ `3`（地刺） | 见 4.2 节 |
| **`Target Layers`** | **只勾 `Player`** ⚠️ | 留空的话地刺完全不生效且无报错 |

> ### 让地刺打敌人？勾上 `Enemy` 就行
>
> 这正是 `IDamageable` 双向的另一个体现：**同一片地刺，可以同时伤害玩家和敌人。**
>
> 想做「引敌人走到地刺上」的玩法，只要把 `Target Layers` 勾上 `Player` + `Enemy` 两个层，**不用改一行代码**。

### 5.2 完整依赖清单

```
Spike
├── Sprite Renderer            ← 看得见
├── Box Collider 2D            ← 撞得到（必须 Is Trigger = true）
└── Damage Zone                ← 本脚本
```

| 缺什么 | 后果 |
|---|---|
| `Box Collider 2D` | `OnTriggerStay2D` 永远不触发。**且无报错** |
| `Is Trigger` 没勾 | 玩家**站在**地刺上，碰不到它 |
| `Sprite Renderer` | 地刺隐形，但照样伤人（**更难查的坑**） |

---

## 六、踩过的坑

> **现象**：地刺摆好了，玩家走过去**完全不掉血**。Console 干干净净。
>
> **根因**：`Target Layers` 留空（全不勾）。第 38 行的位运算结果永远为 `0`，直接 `return`。
>
> **解法**：`Target Layers` 勾上 `Player`。
>
> **为什么这条要写进文档**：这是本项目**所有 `LayerMask` 字段的通用陷阱**（`AttackData.targetLayers`、`DamageZone.targetLayers`、`PlayerController.groundLayer`、`EnemyController.playerLayer` 全都一样）。Unity 对「掩码是 0」这件事不给任何提示——它是一个合法的值，只是匹配不到任何东西。
>
> **排查顺序**：先看 Inspector 的层勾选，再看被伤害对象的 `Layer`，最后才怀疑碰撞体配置。

> **现象**：地刺在掉血，但**掉血速度比预期慢很多**，而且感觉时快时慢。
>
> **根因**：`Tick Interval` 配成了 `0.3` 而玩家的 `Invincibility Duration` 是 `0.5`。第 40–41 行每 0.3 秒跑一次扣血，但 `Health.TakeDamage` 里第一道守卫 `if (_invincibilityTimer > 0f) return;` 会把其中一半挡掉。
>
> **实际效果**：掉血节奏变成「每 0.6 秒一次」——**但你会以为它是每 0.3 秒一次**，于是去调 `Damage Per Tick`，越调越乱。
>
> **解法**：把 `Tick Interval` 设为**略大于** `Invincibility Duration`（0.6 > 0.5）。让「真正生效的节流」是 `DamageZone` 自己的，而不是被 `Health` 静默吃掉一半。

> **现象**：把地刺复制了很多份铺满一个房间，运行一会儿之后感觉卡顿。
>
> **根因**：`OnTriggerStay2D` 是**每物理帧**对每个重叠目标调用一次。20 片地刺 × 每秒 50 次 = 每秒 1000 次函数调用 + 1000 次字典查找。单片地刺无所谓，铺满房间就有压力了。
>
> **解法**（还没做，留作后续优化）：给 `DamageZone` 加一个 `[SerializeField] private float tickInterval` 的**全局节流**，或者用一个静态的「本帧已处理过的目标集合」来跳过重复检查。
>
> **更简单的解法**：**别铺那么多片**。一片长条地刺比十片小地刺效果好、性能也好。

---

## 七、如果要改，改这里

### 1. 想做「毒池」：进入后持续掉血，离开后还有几秒的持续伤害

这是 `DamageZone` **做不到**的——它只在物体「留在区域内」时结算。

正确做法是引入一个状态效果系统：

```csharp
// StatusEffect.cs（将来的脚本）
public class PoisonEffect : MonoBehaviour
{
    [SerializeField] private float damagePerSecond = 5f;
    [SerializeField] private float duration = 3f;
    // 每 0.6 秒扣一次血，但计时器在离开区域后继续跑
}
```

`DamageZone` 在 `OnTriggerEnter2D` 时给目标挂上这个效果，而不是在 `OnTriggerStay2D` 里直接扣血。

> ⚠️ 注意：如果挂状态效果，`DamageZone` 就不需要 `_nextTickTime` 字典了——**节流的责任转移到了状态效果自己身上**。别两边都留着。

### 2. 想做「冲刺穿过地刺不掉血」

在 `PlayerController` 或 `HitReaction` 上加一个 `IsInvincible` 属性（冲刺期间为 `true`），然后在 `DamageZone` 里加一条守卫：

```csharp
if (other.TryGetComponent(out PlayerController p) && p.IsInvincible) return;
```

> ⚠️ 更好的做法是让 `Health` 提供一个统一的 `IsInvincible`（把无敌帧和冲刺无敌帧合并），这样**所有伤害来源都自动尊重无敌**，不用每个攻击方各自判断。

### 3. 想做「不同高度不同伤害」的岩浆

同一个物体只能有一个 `DamageZone`。要么分成多个物体，要么加一个「按 Y 坐标插值伤害」的逻辑。

**推荐前者**：分成两层物体，配置直观，不需要新代码。

### 4. 想给地刺加音效 / 粒子

加到 `OnTriggerStay2D` 里**不行**——它每物理帧都跑，会疯狂播放音效。正确位置是**真正扣血的那一次**（第 41 行之后、第 43 行附近）：

```csharp
_nextTickTime[other] = Time.time + tickInterval;
PlayHitEffect();   // ← 这里
target.TakeDamage(...);
```

> ⚠️ 但音效属于表现层。更符合项目架构的做法是让 `HitReaction` 订阅 `Health.Damaged` 事件来播放——**这样不管伤害来自地刺、敌人还是别的什么，音效都会响。**
>
> 判断标准：**如果这段表现逻辑对「所有伤害来源」都成立，它就该在 `HitReaction` 里，不该在这里。**

### 5. 想改「第一次踩上去的延迟」

现在第一次踩是**立刻扣血**（见 4.5 的短路逻辑）。如果想改成「踩上去 0.3 秒后才开始扣」：

```csharp
if (!_nextTickTime.TryGetValue(other, out float next))
{
    _nextTickTime[other] = Time.time + 0.3f;   // 首次不扣血，只开始计时
    return;
}
if (Time.time < next) return;
_nextTickTime[other] = Time.time + tickInterval;
target.TakeDamage(...);
```

> 改完要注意：**这个版本把「第一次接触」的特殊处理从短路里拆出来了**，可读性变差但意图更明确。**当一行代码想表达两种含义时，拆开往往是更好的选择**——哪怕多写几行。

### 6. 别忘了文档

改完代码，同步改这份文档，并把头部信息表的「最后更新」改成新的 Day 编号。
