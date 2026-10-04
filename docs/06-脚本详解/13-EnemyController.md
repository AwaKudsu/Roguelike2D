# 13 · EnemyController —— 最简敌人 AI

## 一、头部信息表

| 项 | 内容 |
|---|---|
| 文件路径 | `Assets/_Project/Scripts/Enemies/EnemyController.cs` |
| 所属层 | ④ 控制层 |
| 依赖 | `Rigidbody2D`、`CharacterStats`（移速/攻击）、`Health`（存活与死亡事件）、`HitReaction`（硬直）、`AttackData`（攻击数据） |
| 被谁依赖 | 没有脚本依赖它。不过 `MeleeAttacker` 通过 `IDamageable` 接口间接打到它身上的 `Health` |
| 行数 | 190 行 |
| 最后更新 | Day 4 |

---

## 二、一句话定位

看见你就追，追到就打——只用三个分支搞定的敌人 AI。

---

## 三、为什么需要它

### 如果没有这个脚本

敌人就只是一个「会挨打的方块」——玩家可以站在它面前反复砍它，它毫无反应。这个游戏也就不成立了：**肉鸽的核心压力来自「敌人会主动找你麻烦」**。

### 为什么没有用状态机

这是本文件**最重要的一个决策**，也是答辩时最可能被问到的点。

课本上教的标准答案是「敌人 AI 用状态机（FSM）或行为树（Behavior Tree）」。所以很多同学一上来就写：

```csharp
enum EnemyState { Idle, Patrol, Alert, Chase, Attack, Retreat }
private EnemyState _state;
private void Update()
{
    switch (_state)
    {
        case EnemyState.Idle:   ... break;
        case EnemyState.Patrol: ... break;
        case EnemyState.Alert:  ... break;
        // 还有三个 case
    }
}
```

**但本项目没这么做。** 理由：

| 状态机的成本 | 三状态敌人的实际需要 |
|---|---|
| 要定义状态枚举 | 只有「没目标 / 追 / 打」三种情况 |
| 要写状态切换函数 `ChangeState()` | 状态切换就是第 85~86 行的 `if/else` |
| 要维护「进入状态」「退出状态」的钩子 | 没有进入/退出时需要做的事 |
| 要防止非法状态转换 | 不存在非法转换——距离决定一切 |
| 调试时要先看当前在哪个状态 | 直接看距离就够了 |

**状态机的价值在于「管理 N 个状态之间的转换复杂性」。** 当 N=3 且转换关系是一条直线（没目标 → 追 → 打 → 追）时，状态机带来的复杂度**大于**它消除的复杂度。

> **这就是「过度设计」。** 它比「设计不足」更隐蔽——因为代码看起来更「专业」，而且确实能跑。但代价是：
> - 每次调 AI 都要在 6 个 `case` 之间跳来跳去
> - 加一个新行为要改状态枚举、加 `case`、加转换条件
> - 一个「追击中播放音效」的需求，都要先想「这属于哪个状态」
>
> **判断标准很简单：如果一件事用 `if/else` 表达得更短、更容易看懂，就用 `if/else`。**

### 什么时候该升级成状态机

代码注释里已经写明了门槛：

> 等敌人有 **5 个以上状态**（巡逻 / 警觉 / 追击 / 攻击 / 撤退），再引入正式的状态机。

到那时候会出现的**新问题**（这些是三状态时不存在、五状态时才出现的）：

- 同一种状态可能由多种原因进入（「警觉」可能是因为听到声音，也可能是因为被打了）
- 撤退到一半血量回满了，要不要回去追？
- 攻击被打断后应该回「追击」还是回「警觉」？
- 状态之间不再是直线关系，而是一张图

**那时状态机的「转换表」才真正开始省事。** 现在提前引入，只是在为一个还不存在的问题付利息。

> **答辩时怎么说**：
>
> 「敌人 AI 我刻意没有用状态机。因为它只有三个状态，而且状态转换完全由距离决定，是一条直线关系。用状态机管理三个状态的转换，复杂度比 `if/else` 更高。代码注释里我标了升级门槛：**等有 5 个以上状态、并且状态之间的关系不再是直线时，再引入状态机**。这是一个权衡后的决定，不是没考虑过。」

---

## 四、代码全解

### 块 1 · 类声明与头部注释（第 1~11 行）

```csharp
 1: using UnityEngine;
 2:
 3: /// <summary>
 4: /// 最基础的敌人：看见你 → 走过去 → 贴近了就打。
 5: ///
 6: /// 它没有用协程也没有用行为树，只是一个每帧重新判断的分支 ——
 7: /// 因为对三个状态的东西来说，状态机本身就是过度设计。
 8: /// 等敌人有 5 个以上状态（巡逻 / 警觉 / 追击 / 攻击 / 撤退），再引入正式的状态机。
 9: /// </summary>
10: [RequireComponent(typeof(Rigidbody2D))]
11: public class EnemyController : MonoBehaviour
```

| 行 | 代码 | 含义 |
|---|---|---|
| 1 | `using UnityEngine;` | 引入 Unity 基础命名空间 |
| 3-9 | 文档注释 | **六行注释里有四行在解释「为什么不做什么」**——这是把架构决策写进代码的例子。三个月后你回来看，会庆幸当时写了 |
| 10 | `[RequireComponent(typeof(Rigidbody2D))]` | 强制要求同物体上有 `Rigidbody2D`。加了它，第 45 行的 `GetComponent<Rigidbody2D>()` 就一定不会返回 `null` |
| 11 | `public class EnemyController : MonoBehaviour` | 类声明。名字必须和文件名 `EnemyController.cs` 一致 |

> **注意它只 `RequireComponent` 了 `Rigidbody2D`，没有要求 `CharacterStats`、`Health`、`HitReaction`。**
>
> 为什么标准不一样？
>
> | 组件 | 必需吗 | 代码怎么处理 |
> |---|---|---|
> | `Rigidbody2D` | ✅ 绝对必需 | 没有它敌人根本不会动，直接 `RequireComponent` 挡住 |
> | `Health` | ✅ 实际上必需 | 第 51、52、58 行直接用 `_health`，没判空——**没挂 `Health` 会崩** |
> | `CharacterStats` | ⬜ 可选 | 第 106 行用 `_stats != null ? ... : 3f` 兜底 |
> | `HitReaction` | ⬜ 可选 | 第 61 行用 `_hitReaction != null && ...` 兜底 |
> | `AttackData` | ⬜ 可选 | 第 129 行用 `if (attackData == null) return;` 兜底 |
>
> **这是一个可以改进的点**：`_health` 既然实际必需，就应该也加上 `[RequireComponent(typeof(Health))]`，让错误出现在「加组件时」而不是「运行时崩」。
>
> 现在的写法只在 `Awake` 里 `GetComponent` 而不判空，如果 `Health` 没挂，会在第 51 行 `OnEnable` 里立刻报 `NullReferenceException`——**好在报错位置很靠前，容易定位**。

### 块 2 · 感知参数（第 13~24 行）

```csharp
13:     [Header("感知")]
14:     [Tooltip("发现玩家的距离")]
15:     [SerializeField] private float detectRange = 9f;
16:
17:     [Tooltip("走进这个距离就开始攻击")]
18:     [SerializeField] private float attackRange = 1.3f;
19:
20:     [Tooltip("⚠️ 玩家所在的层，必须选 Player")]
21:     [SerializeField] private LayerMask playerLayer;
22:
23:     [Tooltip("超出这个距离就放弃追击（比 detectRange 大，避免在边界反复横跳）")]
24:     [SerializeField] private float loseTargetRange = 14f;
```

| 行 | 代码 | 含义 |
|---|---|---|
| 13 | `[Header("感知")]` | Inspector 里的粗体分组标题 |
| 15 | `detectRange = 9f` | **发现**玩家的距离。玩家进入这个圆圈，敌人开始追 |
| 18 | `attackRange = 1.3f` | **攻击**距离。进入这个范围就挥击 |
| 20-21 | `playerLayer` | 只在这一层上找玩家。⚠️ 留空会永远找不到目标 |
| 23-24 | `loseTargetRange = 14f` | **放弃追击**的距离 |

> ## `detectRange` 和 `loseTargetRange` 为什么要分成两个值？
>
> **这是本文件第二个重要的设计决策。**
>
> 假设只用一个阈值（比如 9），会发生什么：
>
> ```
> 玩家在距离 8.9 → 敌人在追
> 玩家后遇一步到 9.1 → 距离 > 9 → 放弃，敌人停下
> 玩家走回来到 8.9 → 距离 < 9 → 又重新开始追
> 玩家再退到 9.1 → 又放弃
> ...
> ```
>
> **表现就是：玩家在边界附近来回走，敌人会疯狂地「起步—停下—起步—停下」抖动**，看起来像个抽风的机器。
>
> 这个现象叫**「抖动」（jitter / oscillation）**，是所有「用单个阈值控制状态切换」的系统都会遇到的问题。
>
> **解法是「双阈值」**（也叫**迟滞 / hysteresis**）：
>
> ```
>                 ┌──── 锁定区间 ────┐
>                 │                  │
>      detectRange = 9      loseTargetRange = 14
>        发现你               放弃你
>                 │                  │
>   ──────────────┴──────────────────┴──────────→ 距离
>        太远，不追        追！       追得太远，放弃
>
>     中间的 9~14 是「缓冲带」：
>     已经锁定的目标，退到 9 也不放；要到 14 才放
> ```
>
> **具体行为对照**：
>
> | 玩家距离 | 敌人当前无目标 | 敌人当前已锁定 |
> |---|---|---|
> | 5 | 发现并锁定 | 继续追（≤ 1.3 就打） |
> | 12 | **不锁定**（> 9） | **继续追**（< 14） |
> | 15 | 不锁定 | 放弃追击 |
>
> 所以玩家想甩掉敌人，必须跑到 14 以外；但想**被发现**，得跑到 9 以内。**中间 5 个单位的差值给了玩家「脱战」和「入战」两套不同的操作空间**，也彻底消除了边界抖动。
>
> > **这个设计模式到处都是**：
> > - 空调：低于 24 度停，高于 26 度开（不会在 25 度反复启动）
> > - 相机的自动对焦：轻微移动镜头不重新对焦（防止画面呼吸）
> > - 血条的低血量警告：低于 30% 变红，回升到 40% 才变回绿色
> >
> > **凡是「阈值 + 状态切换」的地方，都该想想要不要双阈值。**

> ## `playerLayer` 的 ⚠️ 提示
>
> 这个 `[Tooltip]` 是刻意的。`LayerMask` 类型在 Inspector 里显示成一个多选下拉框，默认**什么都没勾**（值是 0）。
>
> 值为 0 时，第 95 行的 `Physics2D.OverlapCircle(..., playerLayer)` 永远返回 `null`，于是：
> - 敌人永远不会发现玩家
> - 敌人会一直站在原地（第 67 行 `Brake()` 停住）
> - **Console 一句报错都没有**
>
> **这是本项目最阴险的一类 bug**，所以全项目的 `LayerMask` 字段都带 ⚠️ 提示。

> ## 为什么 `detectRange` 用 9 这个值
>
> 和 `attackRange = 1.3` 配合出「反应时间」：
>
> ```
> 玩家进入 9 格范围
>         │
>         ▼
> 敌人开始追（移速 3.2）
>         │
>         │  需要走 9 - 1.3 = 7.7 格
>         │  耗时 7.7 ÷ 3.2 ≈ 2.4 秒
>         ▼
> 进入攻击范围
> ```
>
> **2.4 秒的预警时间**——足够玩家反应、决定打还是跑。如果 `detectRange` 设成 3，敌人会突然出现在攻击范围里，玩家来不及反应；设成 20，敌人从屏幕外就开始追，玩家会觉得莫名其妙。
>
> **这是「感知范围」和「攻击距离」共同定义的游戏节奏**，不是随便填的数字。

### 块 3 · 移动、攻击、死亡参数（第 26~33 行）

```csharp
26:     [Header("移动")]
27:     [SerializeField] private float acceleration = 30f;
28:
29:     [Header("攻击")]
30:     [SerializeField] private AttackData attackData;
31:
32:     [Header("死亡")]
33:     [SerializeField] private float deathDelay = 0.3f;
```

| 行 | 代码 | 含义 |
|---|---|---|
| 27 | `acceleration = 30f` | 敌人的加速度。**没有单独的 `deceleration`**（对比 `PlayerController` 有两个），因为敌人不需要那么精细的刹车手感 |
| 30 | `attackData` | `AttackData` 类型，是一个 **`ScriptableObject` 资产的引用**——在 Inspector 里拖 `Attack_Enemy` 进来 |
| 33 | `deathDelay = 0.3f` | 死亡后多久销毁物体（秒） |

> ## 第 27 行：为什么敌人只用 30，而玩家用 60？
>
> | | 玩家 | 敌人 |
> |---|---|---|
> | 加速度 | `60` | `30` |
> | 加减速分开吗 | ✅ 分开（60 / 80） | ❌ 共用 30 |
> | 理由 | 手感是玩家的核心体验 | 敌人不需要精细操作感 |
>
> **数值上的差异会直接变成「敌我辨识度」**：敌人起步更慢、转向更迟钝，玩家一眼就能看出「这个在动的东西是敌人不是我」。
>
> 这引出一条通用规律：**敌我数值拉开差距不只体现在血量攻击上，操作响应速度也是重要一环。** 玩家 = 灵活，敌人 = 笨重，这是动作游戏最基本的对比。

> ## 第 30 行：`AttackData` 是什么
>
> 它是在 `Assets/_Project/Data/Weapons/Attack_Enemy.asset` 里的一个数据资产，包含：
>
> | 字段 | 敌人的值 | 含义 |
> |---|---|---|
> | `damageMultiplier` | —— | 伤害倍数，乘在 `CharacterStats` 的攻击力上 |
> | `hitboxOffset` | —— | 判定框相对于敌人中心的位置 |
> | `hitboxSize` | —— | 判定框大小 |
> | `targetLayers` | `Player` | 能打到哪一层 |
> | `knockbackForce` | `9` | 击退力度 |
> | `cooldown` | `1.2` | 攻击间隔（秒） |
>
> **为什么做成 `ScriptableObject` 而不是直接在本文件写死这些值？**
>
> 因为玩家和敌人**共用同一个 `AttackData` 类型**——玩家的 `Attack_Basic` 和敌人的 `Attack_Enemy` 是同一个类的两个实例。
>
> 好处：调敌人攻击间隔不用改代码、不用重新编译，在 Inspector 里把 `cooldown` 从 1.2 改成 2.0 立刻生效。**这是「数据放 `ScriptableObject`」这一约定的直接收益。**

### 块 4 · 私有字段（第 35~41 行）

```csharp
35:     private Rigidbody2D _rb;
36:     private CharacterStats _stats;
37:     private Health _health;
38:     private HitReaction _hitReaction;
39:     private Transform _target;
40:     private float _attackCooldownTimer;
41:     private int _facing = 1;
```

| 行 | 代码 | 含义 |
|---|---|---|
| 35 | `private Rigidbody2D _rb;` | 刚体引用，用来读写速度 |
| 36 | `private CharacterStats _stats;` | 属性系统，用来读移速和攻击力 |
| 37 | `private Health _health;` | 生命值组件，用来查「还活着吗」和订阅死亡事件 |
| 38 | `private HitReaction _hitReaction;` | 受击表现，用来查「是否在硬直」 |
| 39 | `private Transform _target;` | **当前锁定的目标**。用 `Transform` 而不是 `GameObject`，因为后面只需要读它的 `position`（第 71 行） |
| 40 | `private float _attackCooldownTimer;` | 攻击冷却剩余时间，每物理帧递减 |
| 41 | `private int _facing = 1;` | **朝向**。`1` = 朝右，`-1` = 朝左。**初始值 1** 很重要（见下） |

> ## 第 39 行：为什么用 `Transform` 而不是 `GameObject`
>
> | 类型 | 能做什么 | 这里需要什么 |
> |---|---|---|
> | `GameObject` | 能拿到 `transform`、`GetComponent`、`SetActive`…… | 太重了 |
> | `Transform` | 能拿到 `position` | ✅ 刚好 |
>
> **代码只用到 `_target.position`**（第 71、82 行），所以缓存的类型精确到 `Transform` 就够了。
>
> **「用能满足需求的最小类型」是一条好习惯**：
> - 意图更清楚（读代码的人一看就知道「这个目标只会被读位置」）
> - 不会被误用（拿 `Transform` 就没法 `SetActive(false)` 把玩家关掉）
>
> > 注意：`MeleeAttacker` 里用的是 `IDamageable`——因为那里需要「能挨打」这个行为，而不只是位置。

> ## 第 41 行：`_facing` 为什么必须有初始值 1
>
> 因为第 134~135 行算判定框中心时会用它：
>
> ```csharp
> Vector2 center = (Vector2)transform.position
>                + new Vector2(attackData.hitboxOffset.x * _facing, attackData.hitboxOffset.y);
> ```
>
> `int` 字段的**默认值是 0**（C# 的规则）。如果写 `private int _facing;`（不给初始值），那么在敌人第一次发现玩家之前，`_facing` 就是 **0**——判定框的 x 偏移乘 0 变成 0，**判定框会落在敌人身体正中间，而不是朝向前方**。
>
> 而第 83 行 `_facing = dir;` 只在锁定目标后才执行。所以「敌人在发现玩家前就被打了」这种边界情况下，就会出现判定框位置错误。
>
> > **规律：表示「方向」或「倍率」的字段，一定不要留默认的 0。** 0 在乘法里会把一切归零。角色朝向习惯上默认 `1`（朝右，和 Unity 2D 的贴图朝向一致）。

### 块 5 · Awake：缓存组件（第 43~49 行）

```csharp
43:     private void Awake()
44:     {
45:         _rb          = GetComponent<Rigidbody2D>();
46:         _stats       = GetComponent<CharacterStats>();
47:         _health      = GetComponent<Health>();
48:         _hitReaction = GetComponent<HitReaction>();
49:     }
```

| 行 | 代码 | 含义 |
|---|---|---|
| 43 | `private void Awake()` | 物体创建时调用一次 |
| 45 | `GetComponent<Rigidbody2D>()` | 取刚体。有 `[RequireComponent]` 保证不为空 |
| 46 | `GetComponent<CharacterStats>()` | 取属性系统。**可能为空**——所以第 106、146 行要判空 |
| 47 | `GetComponent<Health>()` | 取生命值。**没判空**——所以 `Health` 实际是必需的 |
| 48 | `GetComponent<HitReaction>()` | 取受击表现。可能为空，第 61 行判空 |

> **为什么这几行没有像 `PlayerController.Awake` 那样加空判断？**
>
> 因为设计意图不同：
>
> | 脚本 | `CharacterStats` 的地位 |
> |---|---|
> | `PlayerController` | 有它 → 用职业数据；没它 → 退回手填值。**两种都合法** |
> | `EnemyController` | 有它 → 用职业数据；没它 → 退回 3.0。**同样两种都合法** |
>
> 第 106 行的 `_stats != null ? _stats.Get(StatType.MoveSpeed) : 3f` 就是这个兜底。**这个兜底让「敌人」可以是一个简单到没挂属性系统的对象**，方便快速搭测试场景。

### 块 6 · 订阅死亡事件（第 51~52 行）

```csharp
51:     private void OnEnable()  => _health.Died += OnDied;
52:     private void OnDisable() => _health.Died -= OnDied;
```

| 行 | 代码 | 含义 |
|---|---|---|
| 51 | `private void OnEnable() => _health.Died += OnDied;` | 组件启用时，把自己写的 `OnDied()` 挂到 `Health` 的 `Died` 事件上 |
| 52 | `private void OnDisable() => _health.Died -= OnDied;` | 组件禁用时，把回调摘下来 |

> ## 这是本项目最短也最漂亮的一段代码
>
> 两行，用表达式体（`=>`）写完了订阅和退订。
>
> **它实现了一个非常重要的解耦**：
>
> ```
> Health（结算层）
>    │  只知道「我没血了」，不知道谁会关心
>    │  Died?.Invoke()
>    ▼
>  死了
>    │
>    ├──▶ EnemyController.OnDied()   → 停物理 + 延时销毁
>    ├──▶ （未来）掉落系统          → 掉装备
>    ├──▶ （未来）计分系统          → 加分
>    └──▶ （未来）成就系统          → 解锁成就
> ```
>
> **`Health` 一行都不用改**，加新功能只需要在新脚本里也写一句 `_health.Died += ...`。
>
> 对比一下没有事件的写法：
>
> ```csharp
> // ❌ Health.cs 里要认识所有人
> if (Current <= 0)
> {
>     GetComponent<EnemyController>()?.OnDied();
>     GetComponent<LootDropper>()?.Drop();
>     ScoreManager.Instance.Add(10);
>     AchievementSystem.Unlock("first_kill");
> }
> ```
>
> **结算层彻底变成了一个「什么都知道」的上帝对象**——这违背了总览里那条「依赖方向只有一个：上层依赖下层」的规矩。
>
> > **这就是「观察者模式」（Observer Pattern）**，C# 用 `event` 关键字原生支持。它是本项目用得最多的解耦手段，`Health` 有两个事件（`Damaged`、`Died`），`CharacterStats` 有一个（`Changed`）。

> ## ⚠️ `OnEnable` / `OnDisable` 必须成对
>
> **不成对的后果**：
>
> 1. 敌人被打死 → `Destroy(gameObject)` 销毁
> 2. 但 `Health.Died` 的事件清单里**还留着指向这个已销毁对象的 `OnDied`**
> 3. 未来某个时刻事件再次触发 → Unity 尝试调用那个函数
> 4. 报错：`MissingReferenceException: The object of type 'EnemyController' has been destroyed but you are still trying to access it.`
>
> **为什么不用 `Start` / `OnDestroy`**：`SetActive(false)` 会触发 `OnDisable`，但**不会**触发 `OnDestroy`。如果订阅写在 `Start`、退订写在 `OnDestroy`，那么物体被「禁用再启用」一次，就会**订阅两遍**——`OnDied` 会被调用两次，敌人被 `Destroy` 两次。
>
> **判断依据**：凡是在 `OnEnable` 里加的东西（事件订阅、注册到管理器、启用输入），都必须在 `OnDisable` 里减掉。**它们是一对。**

### 块 7 · FixedUpdate：每物理帧重新判断（第 54~87 行）

**这是本文件的骨架。**

```csharp
54:     private void FixedUpdate()
55:     {
56:         if (_attackCooldownTimer > 0f) _attackCooldownTimer -= Time.fixedDeltaTime;
57:
58:         if (!_health.IsAlive) return;
59:
60:         // 受击硬直期间不移动也不攻击 —— 否则敌人会顶着击退往前走，打击感全无
61:         if (_hitReaction != null && _hitReaction.IsStunned) return;
62:
63:         AcquireTarget();
64:
65:         if (_target == null)
66:         {
67:             Brake();
68:             return;
69:         }
70:
71:         float distance = Vector2.Distance(transform.position, _target.position);
72:
73:         // 追丢了：距离超过 loseTargetRange 就放弃，而不是 detectRange —— 
74:         // 两个阈值分开可以避免敌人在边界上反复「锁定 / 丢失」
75:         if (distance > loseTargetRange)
76:         {
77:             _target = null;
78:             Brake();
79:             return;
80:         }
81:
82:         int dir = _target.position.x > transform.position.x ? 1 : -1;
83:         _facing = dir;
84:
85:         if (distance <= attackRange) Attack();
86:         else                         Chase(dir);
87:     }
```

逐块拆解，**顺序是有讲究的**：

| 行 | 代码 | 含义 |
|---|---|---|
| 56 | `if (_attackCooldownTimer > 0f) _attackCooldownTimer -= Time.fixedDeltaTime;` | **先递减攻击冷却**。放在最前面，因为它是纯计时器，和别的逻辑无关 |
| 58 | `if (!_health.IsAlive) return;` | 死掉的敌人不再行动 |
| 61 | `if (_hitReaction != null && _hitReaction.IsStunned) return;` | 硬直期间不做任何事 |
| 63 | `AcquireTarget();` | 尝试获取目标（内部会判断「已经有目标就不换」） |
| 65-69 | `if (_target == null) { Brake(); return; }` | 没目标就停下 |
| 71 | `float distance = Vector2.Distance(...);` | 算和玩家的距离 |
| 75-80 | `if (distance > loseTargetRange) { _target = null; Brake(); return; }` | 追丢了，放弃 |
| 82 | `int dir = _target.position.x > transform.position.x ? 1 : -1;` | 算出朝向：玩家在右边就是 1，在左边是 -1 |
| 83 | `_facing = dir;` | **记录下来**，供判定框和 Gizmos 用 |
| 85 | `if (distance <= attackRange) Attack();` | 够近就打 |
| 86 | `else Chase(dir);` | 否则追 |

> ## 第 56 行：为什么冷却递减放在最前面
>
> 因为它必须在**所有 `return` 之前**执行。
>
> 假如把它放到第 58 行后面：
>
> ```csharp
> if (!_health.IsAlive) return;
> if (_attackCooldownTimer > 0f) _attackCooldownTimer -= Time.fixedDeltaTime;   // ❌
> ```
>
> 那么**敌人死后冷却就不再递减**了。虽然这里影响不大（死都死了），但这是一个通用的坏习惯——**「每帧都该推进的计时器」不应该被任何分支跳过**。
>
> **判断方法**：问自己「如果这一帧因为某个 `return` 跳过了这行，会不会产生永久性的错误状态？」
>
> - 冷却计时器：会（永远卡在某个值）
> - 移动逻辑：不会（下一帧还能动）
>
> **前者必须放最前面。**

> ## 第 58 行：`!_health.IsAlive` 是防御性的吗？
>
> 不完全。`OnDied()`（第 163 行）确实会执行 `Destroy(gameObject, deathDelay)`，但**有 0.3 秒的延时**。
>
> 这 0.3 秒里，敌人还是活着的 GameObject，`FixedUpdate` 还会继续跑 **15 次**（0.3 ÷ 0.02）。
>
> 如果没有第 58 行：
> - 尸体在滑行的 0.3 秒里还会继续追玩家、继续攻击
> - 更糟的是 `AcquireTarget()` 会继续锁定，`Attack()` 会继续打出伤害
> - **玩家打死敌人后还会被尸体打一下**
>
> **所以第 58 行不是防御，是必需的**。它和 `OnDied()` 里第 166 行的 `_rb.simulated = false` 一起，保证尸体「看起来还在，但已经完全失去行动能力」。

> ## 第 61 行：硬直期间 `return` 的理由
>
> 代码注释写得很直白：**「否则敌人会顶着击退往前走，打击感全无」**。
>
> 具体发生了什么：
>
> ```
> t = 0.00s   玩家第 3 下攻击命中，敌人被击退
>             HitReaction: linearVelocity = 击退方向 × 9，IsStunned = true（持续 0.18 秒）
> t = 0.02s   EnemyController.FixedUpdate 执行
>             如果没有第 61 行：
>               Chase(dir) 算出 targetSpeed = +3.2
>               Mathf.MoveTowards(当前速度 -9 或 +9, +3.2, 30 × 0.02)
>               → 击退速度被立刻拉回 +3.2
>             → 击退效果完全看不见
> ```
>
> **`Mathf.MoveTowards` 是「无情的」**——它不管当前速度是怎么来的，只会一步步把速度拉向目标值。所以只要 `Chase()` 还在跑，击退就会被冲掉。
>
> 用 `return` 让 `Chase()` 完全不执行，击退速度就能保持 9 个单位/秒，**敌人被打飞出去一段距离**——这就是打击感的全部来源。
>
> > **和 `PlayerController` 的对比**（这是理解硬直设计的钥匙）：
> >
> > | | `PlayerController`（第 79~97 行） | `EnemyController`（第 61 行） |
> > |---|---|---|
> > | 硬直期间 | **部分**放弃：不算跳跃和移动，但重力照常 | **全部**放弃：直接 `return` |
> > | 为什么不同 | 玩家会被打到空中，需要重力让它落下来 | 敌人一直在地面上，不需要空中处理 |
> > | 清空输入 | ✅ 要（第 90~92 行） | ❌ 不用（敌人没有输入缓冲） |
> >
> > **同样是「硬直」，两个脚本的处理粒度不同，因为需求不同。** 这不是不一致，而是各自按需设计。

> ## 第 71 行：`Vector2.Distance` 是什么
>
> ```csharp
> float distance = Vector2.Distance(transform.position, _target.position);
> ```
>
> 它计算两个点之间的**欧几里得距离**（直线距离）：
>
> ```
> distance = √((x₂-x₁)² + (y₂-y₁)²)
> ```
>
> **`transform.position` 是 `Vector3`，`_target.position` 也是 `Vector3`**，但 `Vector2.Distance` 接受它们——因为 Unity 为 `Vector3` → `Vector2` 定义了**隐式转换**（丢掉 z 分量）。
>
> > **性能提示**：`Vector2.Distance` 内部要算开平方（`Mathf.Sqrt`），比 `sqrMagnitude` 慢。如果一帧要算几千次，应该改成：
> >
> > ```csharp
> > float sqrDist = (a - b).sqrMagnitude;
> > if (sqrDist > loseTargetRange * loseTargetRange) { ... }
> > ```
> >
> > **但本游戏一帧最多几十个敌人，完全不需要这个优化。** 过早优化会让代码难读——**先写清楚的，卡了再优化。**

> ## 第 82 行：朝右还是朝左是怎么判断的
>
> ```csharp
> int dir = _target.position.x > transform.position.x ? 1 : -1;
> ```
>
> | 情况 | `目标.x > 自己.x` | `dir` | 含义 |
> |---|---|---|---|
> | 玩家在右 | `true` | `1` | 朝右追 |
> | 玩家在左 | `false` | `-1` | 朝左追 |
> | 玩家和自己在同一 x | `false` | `-1` | 默认朝左 |
>
> > **⚠️ 同一个 x 时朝左，这个行为可以接受吗？**
> >
> > 极少数情况下（玩家和敌人中心 x 完全相等，且距离 ≤ 1.3），敌人会朝左攻击。但因为距离已经很近（≤ 1.3），判定框无论朝哪边都大概率覆盖到玩家，所以不会出问题。
> >
> > 如果以后判定框变大或者敌人变瘦，可能需要改成「保持上一次的朝向」：
> >
> > ```csharp
> > if (Mathf.Abs(_target.position.x - transform.position.x) > 0.01f)
> >     _facing = _target.position.x > transform.position.x ? 1 : -1;
> > ```
> >
> > **这里用 `> 0.01f` 而不是 `!= 0f`，还是浮点数比较不能直接用等号那条规则。**

> ## 第 83 行：为什么要单独存一个 `_facing`
>
> 因为 `dir` 是**局部变量**，只在这一次 `FixedUpdate` 里有效。而判定框和 Gizmos 需要在**别的时刻**知道朝向：
>
> | 使用方 | 时机 | 需要 `_facing` 吗 |
> |---|---|---|
> | `Attack()`（第 135 行） | 攻击瞬间 | ✅ |
> | `OnDrawGizmosSelected()`（第 187 行） | **编辑器里，可能不在运行** | ✅ |
>
> **Gizmos 是必须单独存朝向的原因**——它要在编辑模式下画出「攻击判定框在哪」，那时 `FixedUpdate` 根本没跑过，`dir` 这个局部变量根本不存在。
>
> > **这就是「局部变量」和「字段」的选择标准：需不需要跨方法、跨帧保留？**
> > - 只在这一次调用里用 → 局部变量（`distance`、`dir`）
> > - 要留给别的方法/别的帧用 → 字段（`_facing`）

### 块 8 · 感知：寻找目标（第 89~102 行）

```csharp
 89:     // ---------------- 感知 ----------------
 90:
 91:     private void AcquireTarget()
 92:     {
 93:         if (_target != null) return;   // 已有目标就不换
 94:
 95:         Collider2D hit = Physics2D.OverlapCircle(transform.position, detectRange, playerLayer);
 96:         if (hit == null) return;
 97:
 98:         // 不打已经死掉的玩家
 99:         if (hit.TryGetComponent(out IDamageable d) && !d.IsAlive) return;
100:
101:         _target = hit.transform;
102:     }
```

| 行 | 代码 | 含义 |
|---|---|---|
| 89-91 | 分节标题 + 方法声明 | 寻找目标的全部逻辑 |
| 93 | `if (_target != null) return;` | **已经有目标就什么都不做** |
| 95 | `Collider2D hit = Physics2D.OverlapCircle(transform.position, detectRange, playerLayer);` | 以自己的位置为圆心，画半径 `detectRange` 的圆，只找 `playerLayer` 层上的碰撞体 |
| 96 | `if (hit == null) return;` | 没找到就结束 |
| 99 | `if (hit.TryGetComponent(out IDamageable d) && !d.IsAlive) return;` | 找到了但那是个死掉的玩家 → 不要 |
| 101 | `_target = hit.transform;` | 锁定目标的 Transform |

> ## 第 93 行：为什么不换目标
>
> **三重理由：**
>
> **① 避免目标抖动**
>
> 如果每帧都重新找，当检测圈里有多个碰撞体时，`OverlapCircle` 返回哪个是**不确定的**（取决于物理引擎内部的迭代顺序）。结果就是敌人在两个目标之间疯狂切换，每个目标都追一半。
>
> **② 节省性能**
>
> `Physics2D.OverlapCircle` 要遍历空间划分树。一帧一次、十个敌人就是十次。有目标时直接 `return`，省下这些调用。
>
> **③ 语义更清楚**
>
> 「锁定」在游戏里本来就是一个**持续状态**，不是每帧重新做的决定。玩家会观察到「敌人认准我了」——这个可预测性很重要。
>
> > **那怎么解除锁定？** 由 `FixedUpdate` 第 75~80 行负责——距离超过 `loseTargetRange` 时把 `_target` 设成 `null`。**下一次 `AcquireTarget()` 就会重新找一个。**
> >
> > 这是很干净的职责划分：
> > - `AcquireTarget()` 只管「**怎么获得**目标」
> > - `FixedUpdate()` 只管「**什么时候放弃**目标」

> ## 第 95 行：`Physics2D.OverlapCircle` 只返回一个碰撞体
>
> 它的返回类型是 `Collider2D`（**单个**），不是数组。
>
> | 方法 | 返回 | 什么时候用 |
> |---|---|---|
> | `Physics2D.OverlapCircle(...)` | `Collider2D`（第一个） | **只要找到一个就够**——比如这里 |
> | `Physics2D.OverlapCircleAll(...)` | `Collider2D[]`（全部） | 需要处理范围内所有对象——比如 `MeleeAttacker` 的群伤 |
> | `Physics2D.OverlapCircleNonAlloc(...)` | `int`（数量，填进预分配数组） | 高性能场景，避免每帧产生垃圾 |
>
> **本项目用前两个**（`EnemyController` 用 `OverlapCircle`，`MeleeAttacker` 用 `OverlapBoxAll`），因为敌人和攻击的调用频率都很低，**不值得为了省一点 GC 牺牲可读性**。
>
> > 「第一个」具体是哪一个？**Unity 没有保证**。它取决于碰撞体的内部顺序。所以任何依赖「返回的一定是某某」的代码都是不可靠的——这也是第 93 行「不换目标」的另一个理由。

> ## 第 99 行：一行代码里藏了三个 C# 知识点
>
> ```csharp
> if (hit.TryGetComponent(out IDamageable d) && !d.IsAlive) return;
> ```
>
> **知识点 1：`TryGetComponent<T>(out T component)`**
>
> 它尝试在 `hit` 上找 `IDamageable` 组件：
> - 找到了 → 返回 `true`，并把组件赋给 `out` 参数 `d`
> - 没找到 → 返回 `false`，`d` 是 `null`
>
> **`out` 关键字**表示这个参数是「**输出参数**」——方法内部会给它赋值，调用方之后能读到。这是 C# 里「一个方法返回多个值」的标准做法。
>
> **它和 `GetComponent` 的区别**：
>
> ```csharp
> // 老写法：两步，还要判空
> var c = hit.GetComponent<IDamageable>();
> if (c != null) { ... }
>
> // TryGetComponent：一步搞定
> if (hit.TryGetComponent<IDamageable>(out var c)) { ... }
> ```
>
> **更重要的区别**：`TryGetComponent` **不会产生垃圾**。老的 `GetComponent` 在找不到组件时会分配内存，在 `Update`/`FixedUpdate` 里调用会持续加重 GC。所以 Unity 官方推荐统一用 `TryGetComponent`。
>
> **知识点 2：`&&` 的短路保护**
>
> `&&` 从左往右求值，左边为 `false` 时**不计算右边**。
>
> 所以当 `TryGetComponent` 返回 `false`（`d` 是 `null`）时，`!d.IsAlive` **根本不会执行**——**避免了对 `null` 访问属性导致的 `NullReferenceException`**。
>
> 如果顺序写反成 `!d.IsAlive && hit.TryGetComponent(out IDamageable d)`，会**编译报错**——因为 `d` 在使用时还没声明。C# 的编译器在这里帮了忙。
>
> **知识点 3：`IDamageable` 接口**
>
> 注意泛型参数是**接口类型**，不是具体的类。`TryGetComponent<IDamageable>` 会找到**任何实现了这个接口的组件**——不管是 `Health`、还是未来的 `BreakableBarrel`、`DestructibleWall`。
>
> **这就是「面向接口编程」**：
> - 如果写成 `TryGetComponent<Health>(out var h)`，敌人就只能识别挂了 `Health` 的东西
> - 用接口，敌人**不需要知道玩家是什么类型**，只关心「它能不能挨打」和「它还活着吗」
>
> > **为什么这行需要判断死亡？**
> >
> > 因为 `Health.Died` 事件触发后，玩家物体**不会立刻销毁**——`RunManager` 要等 1.5 秒才重载场景。这 1.5 秒里玩家物体还在场景里，还在 `playerLayer` 上，`OverlapCircle` 还能找到它。
> >
> > 如果没有这行，敌人会在玩家死后继续追着尸体打。**「不打已经死掉的玩家」这个注释就是这个意思。**

### 块 9 · 移动：Chase 和 Brake（第 104~121 行）

```csharp
104:     // ---------------- 移动 ----------------
105:
106:     private float MoveSpeed => _stats != null ? _stats.Get(StatType.MoveSpeed) : 3f;
107:
108:     private void Chase(int dir)
109:     {
110:         float targetSpeed = dir * MoveSpeed;
111:         float newX = Mathf.MoveTowards(_rb.linearVelocity.x, targetSpeed,
112:                                        acceleration * Time.fixedDeltaTime);
113:         _rb.linearVelocity = new Vector2(newX, _rb.linearVelocity.y);
114:     }
115:
116:     private void Brake()
117:     {
118:         float newX = Mathf.MoveTowards(_rb.linearVelocity.x, 0f,
119:                                        acceleration * Time.fixedDeltaTime);
120:         _rb.linearVelocity = new Vector2(newX, _rb.linearVelocity.y);
121:     }
```

| 行 | 代码 | 含义 |
|---|---|---|
| 106 | `private float MoveSpeed => _stats != null ? _stats.Get(StatType.MoveSpeed) : 3f;` | 读属性系统的移速，没有就用 3.0 |
| 110 | `float targetSpeed = dir * MoveSpeed;` | 目标速度 = 方向 × 移速 |
| 111-112 | `Mathf.MoveTowards(_rb.linearVelocity.x, targetSpeed, acceleration * Time.fixedDeltaTime)` | 平滑逼近目标速度 |
| 113 | `_rb.linearVelocity = new Vector2(newX, _rb.linearVelocity.y);` | 写回刚体，保留垂直速度 |
| 118-119 | `Mathf.MoveTowards(_rb.linearVelocity.x, 0f, ...)` | 目标速度是 **0**，平滑减速到停 |

> ## 第 106 行：和 `PlayerController.MaxSpeed` 是同一套路
>
> ```csharp
> // PlayerController 第 141 行
> private float MaxSpeed => _stats != null ? _stats.Get(StatType.MoveSpeed) : maxSpeed;
>
> // EnemyController 第 106 行
> private float MoveSpeed => _stats != null ? _stats.Get(StatType.MoveSpeed) : 3f;
> ```
>
> **结构完全一样**：有 `CharacterStats` 就读属性，没有就用兜底值。
>
> 区别在兜底值不同（玩家的 `maxSpeed` 是 `[SerializeField]` 可以在 Inspector 调，敌人的写死 `3f`），以及**敌人**的 `Base Move Speed` 是直接在 `CharacterStats` 里手填的 `3.2`（因为敌人没有 `ClassData`）。
>
> > **这个「同一套路」是刻意的设计**——`CharacterStats` 是**玩家和敌人共用**的数据出口。总览里的架构图把它放在「① 数据层」，就是因为它服务于两个上层。
> >
> > 所以 `EnemyController` 和 `PlayerController` 虽然一个追人一个被操作，**读数值的方式却完全一致**。加一个「敌人移速 +20% 的精英怪」修饰符时，两个脚本都不用改。

> ## 第 108~121 行：`Chase` 和 `Brake` 是同一段代码
>
> 把它们并排看：
>
> ```csharp
> private void Chase(int dir)                      private void Brake()
> {                                                {
>     float targetSpeed = dir * MoveSpeed;             float targetSpeed = 0f;
>     float newX = Mathf.MoveTowards(                   float newX = Mathf.MoveTowards(
>         _rb.linearVelocity.x, targetSpeed,                _rb.linearVelocity.x, 0f,
>         acceleration * Time.fixedDeltaTime);              acceleration * Time.fixedDeltaTime);
>     _rb.linearVelocity = new Vector2(newX, ...);      _rb.linearVelocity = new Vector2(newX, ...);
> }                                                }
> ```
>
> **唯一的区别就是目标速度：`dir * MoveSpeed` vs `0f`。**
>
> ### 能不能合并？
>
> 能，两种写法：
>
> ```csharp
> // 写法 A：直接传目标速度
> private void MoveTo(float targetSpeed)
> {
>     float newX = Mathf.MoveTowards(_rb.linearVelocity.x, targetSpeed,
>                                    acceleration * Time.fixedDeltaTime);
>     _rb.linearVelocity = new Vector2(newX, _rb.linearVelocity.y);
> }
> // 调用处：MoveTo(dir * MoveSpeed);   MoveTo(0f);
>
> // 写法 B：传方向，0 表示停
> private void Move(int dir) => MoveTo(dir * MoveSpeed);
> ```
>
> ### 为什么**没有**合并
>
> | 合并的好处 | 不合并的好处 |
> |---|---|
> | 少 6 行代码 | `Chase(dir)` 和 `Brake()` 在调用处**一眼看懂意图** |
> | 改一处两处都生效 | `Brake()` 不依赖 `_facing`/`dir`，语义独立 |
>
> 看 `FixedUpdate` 里的调用：
>
> ```csharp
> if (_target == null)          { Brake(); return; }   // 读起来：「刹车，结束」
> if (distance > loseTargetRange) { _target = null; Brake(); return; }
> ...
> else Chase(dir);                                     // 读起来：「追」
> ```
>
> **`Brake()` 这三个字本身就是注释。** 如果合并成 `MoveTo(0f)`，读代码的人要多想一步「0 是什么意思？」。
>
> > **一条实用的判断标准**：**如果两个函数只是参数不同、但调用处的语义完全不同，就不要合并。** 重复 6 行代码换来的可读性，比 DRY（Don't Repeat Yourself）原则更重要。
> >
> > DRY 是「不要重复**知识**」，不是「不要重复**代码**」。这里的知识是「用 `MoveTowards` 平滑改速度」——它只有一处（第 111~113 行和第 118~120 行各一处，但各自独立完整）。而 `Chase` 和 `Brake` 表达的是**两个不同的游戏行为**。

> ## 第 113 行：又见「只改自己负责的分量」
>
> ```csharp
> _rb.linearVelocity = new Vector2(newX, _rb.linearVelocity.y);
> ```
>
> 和 `PlayerController` 第 154 行完全一样——**水平逻辑不碰垂直速度**。
>
> 如果写成 `new Vector2(newX, 0f)`，敌人会**浮在半空**（重力积不起来）。

### 块 10 · 攻击（第 123~159 行）

```csharp
123:     // ---------------- 攻击 ----------------
124:
125:     private void Attack()
126:     {
127:         Brake();
128:
129:         if (attackData == null) return;
130:         if (_attackCooldownTimer > 0f) return;
131:
132:         _attackCooldownTimer = attackData.cooldown;
133:
134:         Vector2 center = (Vector2)transform.position
135:                        + new Vector2(attackData.hitboxOffset.x * _facing, attackData.hitboxOffset.y);
136:
137:         Collider2D[] hits = Physics2D.OverlapBoxAll(center, attackData.hitboxSize, 0f,
138:                                                     attackData.targetLayers);
139:
140:         foreach (var hit in hits)
141:         {
142:             if (hit.gameObject == gameObject) continue;
143:             if (!hit.TryGetComponent(out IDamageable target)) continue;
144:             if (!target.IsAlive) continue;
145:
146:             float attack = _stats != null ? _stats.Get(StatType.Attack) : 5f;
147:
148:             target.TakeDamage(new DamageInfo
149:             {
150:                 Amount         = attack * attackData.damageMultiplier,
151:                 SourcePosition = transform.position,
152:                 KnockbackForce = attackData.knockbackForce,
153:                 Attacker       = gameObject,
154:                 IsCritical     = false,
155:             });
156:
157:             break;   // 一次挥击只打一个目标
158:         }
159:     }
```

| 行 | 代码 | 含义 |
|---|---|---|
| 127 | `Brake();` | **先停下来**再攻击 |
| 129 | `if (attackData == null) return;` | 没配攻击数据就不打（防御性检查） |
| 130 | `if (_attackCooldownTimer > 0f) return;` | 冷却没好就不打 |
| 132 | `_attackCooldownTimer = attackData.cooldown;` | 重置冷却计时器 |
| 134-135 | `Vector2 center = ...` | 算出判定框的中心位置 |
| 137-138 | `Collider2D[] hits = Physics2D.OverlapBoxAll(...)` | 画出判定框，**收集所有**碰到的碰撞体 |
| 140 | `foreach (var hit in hits)` | 遍历它们 |
| 142 | `if (hit.gameObject == gameObject) continue;` | 跳过自己 |
| 143 | `if (!hit.TryGetComponent(out IDamageable target)) continue;` | 跳过不能挨打的东西 |
| 144 | `if (!target.IsAlive) continue;` | 跳过已经死掉的目标 |
| 146 | `float attack = _stats != null ? _stats.Get(StatType.Attack) : 5f;` | 读攻击力，兜底 5 |
| 148-155 | `target.TakeDamage(new DamageInfo { ... });` | 派发伤害 |
| 157 | `break;` | **打完一个就跳出循环** |

> ## 第 127 行：为什么攻击前先 `Brake()`
>
> 因为这是个**近战敌人**。如果不刹车：
>
> ```
> 敌人以 3.2 的速度冲向玩家
> → 进入攻击范围，打出伤害
> → 但速度还是 3.2，继续往前冲
> → 冲过玩家头顶，跑到玩家另一边
> → 下一帧距离又变成 1.5，朝向反转，再冲回来
> → 在玩家身边来回横跳
> ```
>
> **`Brake()` 让敌人在攻击时「站定」**，玩家能清楚看到「它停下来打我了」，而不是糊成一团。
>
> > ⚠️ **注意 `Brake()` 是「平滑减速」不是「立刻停」**——用 30 的加速度从 3.2 减到 0 需要约 0.107 秒（5 个物理帧）。所以敌人是在攻击过程中逐渐停下的，看起来比「急刹」自然。
> >
> > 而且 `Brake()` 在**每次 `Attack()` 调用时都会执行**（第 127 行在冷却检查之前），所以冷却期间敌人会持续保持「速度归零」的状态——**它会站在原地连续攻击，直到玩家跑出攻击范围。**

> ## 第 134~135 行：判定框中心的计算
>
> ```csharp
> Vector2 center = (Vector2)transform.position
>                + new Vector2(attackData.hitboxOffset.x * _facing, attackData.hitboxOffset.y);
> ```
>
> 拆解：
>
> | 部分 | 含义 |
> |---|---|
> | `(Vector2)transform.position` | 敌人的位置。**强制类型转换**把 `Vector3` 变成 `Vector2`（丢掉 z） |
> | `attackData.hitboxOffset.x * _facing` | 判定框的前后偏移，**乘朝向**——朝右时是 `+offset`，朝左时翻转成 `-offset` |
> | `attackData.hitboxOffset.y` | 上下偏移，**不乘朝向**——上下的方向不会因为是朝左而翻转 |
>
> **`* _facing` 是这一行的灵魂。** 如果忘了乘：
>
> - 敌人朝左时，判定框仍然落在**右边**（它的背后）
> - 表现就是「敌人背对着我，我却被打到了」
>
> > **为什么用 `_facing`（已存的字段）而不是 `dir`？**
> >
> > 因为 `Attack()` 是**独立的方法**，拿不到 `FixedUpdate` 里的局部变量 `dir`。第 83 行的 `_facing = dir;` 就是为了这里。
> >
> > 注意 `_facing` 有可能是 `1`（初始值）而从未被 `FixedUpdate` 更新过——那时判定框会默认朝右。这比朝 0（判定框居中）好得多，是第 41 行给它初始值的原因。

> ## 第 137 行：`Physics2D.OverlapBoxAll` 和 `MeleeAttacker` 用的是同一套算法
>
> ```csharp
> Physics2D.OverlapBoxAll(point, size, angle, layerMask)
> ```
>
> | 参数 | 本项目的值 | 含义 |
> |---|---|---|
> | `point` | `center` | 矩形中心 |
> | `size` | `attackData.hitboxSize` | 矩形的宽高 |
> | `angle` | `0f` | **旋转角度**，0 表示不旋转（轴对齐） |
> | `layerMask` | `attackData.targetLayers` | 只检查这一层 |
>
> **返回 `Collider2D[]`（全部）。** 和 `EnemyController.AcquireTarget` 用的 `OverlapCircle`（返回一个）不同——**这里必须用 `All` 版本**，因为要处理「判定框里同时有多个物体」的情况。
>
> > **为什么 `angle` 传 0？**
> >
> > 因为 `hitboxOffset.x * _facing` 已经用「翻转偏移」的方式处理了朝向，判定框本身不需要旋转。
> >
> > 另一种做法是保持偏移不变、把判定框旋转 180°——但那样 `hitboxSize` 的宽高会在旋转后含义混乱。**「翻转偏移，不旋转形状」是更简单的方案。**
> >
> > **`MeleeAttacker` 用的是完全相同的四个参数**——这就是「玩家和敌人共用一套战斗算法」的体现。两者的区别只在「谁提供 center」（玩家用朝向，敌人也用朝向）和「打几个目标」。

> ## 第 142 行：为什么要跳过自己
>
> ```csharp
> if (hit.gameObject == gameObject) continue;
> ```
>
> 因为敌人的判定框偏移可能很小，或者敌人的碰撞体很大——**判定框可能覆盖到自己的碰撞体**。
>
> 如果不跳过：敌人会 `TryGetComponent<IDamageable>` 找到自己的 `Health`，然后**打自己**。表现就是敌人站在原地掉血直到死亡。
>
> > **`hit.gameObject == gameObject` 比较的是什么？**
> >
> > `hit` 是判定框碰到的 `Collider2D`，`hit.gameObject` 是它所属的物体。`gameObject`（没有前缀）是 `MonoBehaviour` 继承来的属性，指向**本脚本所在的那个 GameObject**。
> >
> > 所以这行是在问：「碰到的这个碰撞体，是不是我自己身上的？」
> >
> > > **更严谨的写法**是比较 `hit.transform.root`（如果敌人有子物体挂碰撞体），但本项目的敌人没有子物体，直接比 `gameObject` 就够了。

> ## 第 143~144 行：两个连续的 `continue` 过滤
>
> ```csharp
> if (!hit.TryGetComponent(out IDamageable target)) continue;   // 不能挨打 → 跳过
> if (!target.IsAlive) continue;                                 // 死了 → 跳过
> ```
>
> **`continue`** 是 C# 的循环控制关键字，意思是「跳过本次循环剩下的部分，直接进入下一次迭代」。
>
> 这两行是「**卫语句**（guard clause）」风格的过滤：
>
> ```
> 候选碰撞体
>     │
>     ├─ 是自己？        → continue
>     ├─ 不能挨打？      → continue
>     ├─ 已经死了？      → continue
>     │
>     ▼
> 真的可以打 → 执行伤害
> ```
>
> **对比嵌套写法**：
>
> ```csharp
> // ❌ 嵌套写法
> foreach (var hit in hits)
> {
>     if (hit.gameObject != gameObject)
>     {
>         if (hit.TryGetComponent(out IDamageable target))
>         {
>             if (target.IsAlive)
>             {
>                 // 真正的逻辑缩进到这里
>             }
>         }
>     }
> }
> ```
>
> 四层缩进之后，**真正的逻辑被推到了屏幕右边**。卫语句把「不满足条件的情况」提前踢掉，让主逻辑留在最外层。**层级越浅，读起来越快。**

> ## 第 146 行：为什么在循环**内部**读攻击力
>
> ```csharp
> foreach (var hit in hits)
> {
>     ...
>     float attack = _stats != null ? _stats.Get(StatType.Attack) : 5f;
>     ...
>     break;   // ← 反正只打一个
> }
> ```
>
> **因为第 157 行有 `break`，这个循环最多执行一次。** 所以放里面放外面性能上没区别。
>
> 但如果以后去掉 `break`（改成群伤），把它放在循环内就会**每个目标都读一次属性**——`Get(StatType.Attack)` 内部要遍历修饰符列表，是有开销的。
>
> > **可以改进的点**：把第 146 行提到 `foreach` 之前，是**无条件更好**的写法（无论将来改不改群伤）。不过当前代码不算错。

> ## 第 148~155 行：`new DamageInfo { ... }` 是什么语法
>
> 这是 C# 的**对象初始化器**（object initializer）：
>
> ```csharp
> target.TakeDamage(new DamageInfo
> {
>     Amount         = attack * attackData.damageMultiplier,
>     SourcePosition = transform.position,
>     KnockbackForce = attackData.knockbackForce,
>     Attacker       = gameObject,
>     IsCritical     = false,
> });
> ```
>
> 它等价于：
>
> ```csharp
> var info = new DamageInfo();
> info.Amount         = attack * attackData.damageMultiplier;
> info.SourcePosition = transform.position;
> info.KnockbackForce = attackData.knockbackForce;
> info.Attacker       = gameObject;
> info.IsCritical     = false;
> target.TakeDamage(info);
> ```
>
> **为什么用初始化器**：五个字段一次填完，读代码的人能**一眼看到这次伤害的完整描述**——而不是分成六行去找。
>
> > ⚠️ **注意 `DamageInfo` 是 `struct`（结构体）不是 `class`。** 所以 `new DamageInfo { ... }` 是在栈上创建的，**不产生垃圾**。这是 `DamageInfo` 设计成结构体的原因之一——它在战斗中被高频创建。
>
> ### 五个字段逐条说明
>
> | 字段 | 值 | 含义 |
> |---|---|---|
> | `Amount` | `attack * attackData.damageMultiplier` | 最终伤害值。`attack` 来自 `CharacterStats`（敌人手填的 `Base Attack` = 8），`damageMultiplier` 来自 `Attack_Enemy.asset` |
> | `SourcePosition` | `transform.position` | 伤害来源位置。`HitReaction` 用它算击退方向（往哪边飞） |
> | `KnockbackForce` | `attackData.knockbackForce` | 击退力度（`Attack_Enemy` 里是 9） |
> | `Attacker` | `gameObject` | 谁打的。用于未来做「仇恨值」「反伤」「击杀归属」 |
> | `IsCritical` | `false`（写死） | 这次攻击**不是暴击** |
>
> ## 为什么敌人的 `IsCritical` 写死 `false`？
>
> 因为**敌人不做暴击**。三个理由：
>
> **① 玩家感受**：被敌人打死应该是「我操作失误」或者「我血量不够」，而不是「运气不好」。暴击会让玩家死得莫名其妙，产生**不公平感**。
>
> **② 数值不可控**：敌人暴击意味着它的 DPS 有方差。设计关卡难度时，你要按「最坏情况」算还是「平均情况」算？按最坏算会打得过松，按平均算会偶尔暴毙。
>
> **③ 代码简单**：`IsCritical = false` 让它彻底绕开暴击逻辑，`HitReaction` 不用处理「敌人暴击时闪红」这种特例。
>
> > **玩家的暴击则保留**（在 `MeleeAttacker` 里算）。因为**玩家的随机性带来的是「爽」**——打出一个大数字，玩家会开心。敌方的随机性带来的是「挫败」。
> >
> > **这是游戏设计里的一条通用原则：随机性给玩家，确定性给敌人。**

> ## 第 157 行：`break` —— 一次挥击只打一个目标
>
> ```csharp
> break;   // 一次挥击只打一个目标
> ```
>
> **`break`** 立即终止整个 `foreach` 循环。
>
> **这是和玩家 `MeleeAttacker` 最大的区别：**
>
> | | `EnemyController.Attack()` | `MeleeAttacker.Execute()` |
> |---|---|---|
> | 循环结尾 | `break;`（打一个就走） | 循环自然结束（打全部） |
> | 原因 | 敌人攻击应该是**单体**的 | 玩家攻击是**范围**的（砍一刀命中一圈） |
> | 玩法效果 | 被一群敌人围住时不会瞬间蒸发 | 玩家能一刀清一群小怪，有爽感 |
>
> > **这个差异是刻意设计的**，而且它**不需要任何额外代码**——只是 `break` 的有无。
> >
> > 「玩家强、敌人弱」这个整体平衡，一部分就是靠这种细微的机制差异实现的：**玩家有群伤，敌人没有。**

### 块 11 · 死亡（第 161~169 行）

```csharp
161:     // ---------------- 死亡 ----------------
162:
163:     private void OnDied()
164:     {
165:         _rb.linearVelocity = Vector2.zero;
166:         _rb.simulated = false;         // 停掉物理，尸体不会继续滑
167:
168:         Destroy(gameObject, deathDelay);
169:     }
```

| 行 | 代码 | 含义 |
|---|---|---|
| 163 | `private void OnDied()` | 由 `Health.Died` 事件触发（第 51 行订阅的） |
| 165 | `_rb.linearVelocity = Vector2.zero;` | 把速度清零。`Vector2.zero` 就是 `new Vector2(0, 0)` |
| 166 | `_rb.simulated = false;` | **关掉物理模拟** |
| 168 | `Destroy(gameObject, deathDelay);` | 0.3 秒后销毁整个物体 |

> ## 第 165 和 166 行：为什么要做两件事
>
> | 行 | 作用 | 只做这一件会怎样 |
> |---|---|---|
> | 165 | 速度**立即**归零 | —— |
> | 166 | 物理**从此**不再模拟 | —— |
>
> **看起来第 166 行就够了？** 不完全。
>
> `simulated = false` 会让刚体**立即停止参与物理计算**——包括施加在其上的速度。但 Unity 内部的速度值**不会自动清零**。所以：
>
> - 如果只做 166：物体停止移动，但 `linearVelocity` 里还留着旧值。如果你之后写代码读它（比如做个「死亡时按残存速度抛飞」的效果），会读到过期数据
> - 如果只做 165：物体停在原地，但物理仍在模拟——**如果有别的物体撞上来，尸体会被推走**
>
> **两行一起做，才是干净的「死亡」状态**：速度是 0，物理是关的。
>
> > **一个更隐蔽的理由**：第 166 行还避免了尸体和玩家碰撞。`simulated = false` 之后，这个碰撞体从物理世界里被移除了——玩家可以**直接走过敌人的尸体**。
> >
> > 如果不关物理，玩家会被尸体**挡住**——打死敌人后反而被尸体卡住，非常难受。**这是这一行最重要的实际效果。**

> ## 第 168 行：`Destroy(gameObject, deathDelay)` 的第二个参数
>
> | 写法 | 效果 |
> |---|---|
> | `Destroy(gameObject);` | **立即**销毁（实际上是这一帧结束时） |
> | `Destroy(gameObject, 0.3f);` | **0.3 秒后**销毁 |
>
> **第二个参数是延时的秒数。**
>
> ## 为什么要延时 0.3 秒？
>
> **因为「直接消失」太突兀了。** 战斗的节奏是这样的：
>
> ```
> 玩家第 3 下攻击命中
>         │
>         ▼
> 敌人掉血到 0 → Health.Died 触发
>         │
>         ├──▶ HitReaction.OnDamaged()   ← 闪白（0.12 秒）
>         │      玩家看到「打中了」的反馈
>         │
>         └──▶ EnemyController.OnDied()
>                 │
>                 │  ← 这 0.3 秒里，敌人静止不动，还是白色的
>                 │     玩家的眼睛能跟上：哦，它死了
>                 │
>                 ▼
>              Destroy → 物体消失
> ```
>
> **0.3 秒是「死亡反馈」的最小可感知时长。** 少于 0.15 秒玩家会觉得「怎么突然没了」；多于 0.5 秒会拖慢战斗节奏。
>
> > **⚠️ 一个必须知道的副作用**：这 0.3 秒里，敌人**仍然是场景里的活物体**。所以：
> > - `FixedUpdate` 还会跑 15 次 → 所以第 58 行必须 `if (!_health.IsAlive) return;`
> > - 玩家的攻击判定框还能打到它 → 所以 `MeleeAttacker` 里也要检查 `IsAlive`
> > - 如果敌人会掉落物品，掉落应该在这一刻触发（而不是 `Destroy` 时）
>
> **「延时销毁」的代价就是「延时期间它还在」。** 想清楚哪些代码需要判活，是这个模式的关键。

> ## 为什么用事件而不是在 `Health` 里直接 `Destroy`
>
> 因为 `Health` 是**结算层**，它不知道「尸体应该停多久才消失」——那是**控制层**的事。
>
> 而且玩家也有 `Health`。如果 `Health` 自己 `Destroy(gameObject)`，玩家死掉时整个玩家物体会被销毁——`RunManager` 就再也拿不到它、播不了死亡动画了。
>
> > **记住这条**：`Health` 只广播「我死了」，**怎么死、死成什么样、死完干嘛，都是别人的事。**

### 块 12 · 调试可视化（第 171~189 行）

```csharp
171:     // ---------------- 调试可视化 ----------------
172:
173:     private void OnDrawGizmosSelected()
174:     {
175:         Gizmos.color = new Color(1f, 0.8f, 0.2f, 0.4f);
176:         Gizmos.DrawWireSphere(transform.position, detectRange);
177:
178:         Gizmos.color = new Color(1f, 0.5f, 0.2f, 0.3f);
179:         Gizmos.DrawWireSphere(transform.position, loseTargetRange);
180:
181:         Gizmos.color = new Color(1f, 0.3f, 0.3f, 0.9f);
182:         Gizmos.DrawWireSphere(transform.position, attackRange);
183:
184:         if (attackData == null) return;
185:         Gizmos.color = new Color(1f, 0.35f, 0.35f, 0.6f);
186:         Vector2 center = (Vector2)transform.position
187:                        + new Vector2(attackData.hitboxOffset.x * _facing, attackData.hitboxOffset.y);
188:         Gizmos.DrawWireCube(center, attackData.hitboxSize);
189:     }
```

| 行 | 代码 | 含义 |
|---|---|---|
| 173 | `private void OnDrawGizmosSelected()` | 选中这个敌人时，在 Scene 视图里画调试图形 |
| 175 | `Gizmos.color = new Color(1f, 0.8f, 0.2f, 0.4f);` | 设置画笔颜色为**半透明黄** |
| 176 | `Gizmos.DrawWireSphere(transform.position, detectRange);` | 画**发现圈**（半径 9） |
| 178-179 | 同上 | 画**放弃追击圈**（半径 14），半透明橙 |
| 181-182 | 同上 | 画**攻击圈**（半径 1.3），不透明红 |
| 184 | `if (attackData == null) return;` | 没配攻击数据就不画判定框 |
| 185-188 | 和 `Attack()` 第 134~135 行**同一套算法** | 算出判定框中心 |
| 188 | `Gizmos.DrawWireCube(center, attackData.hitboxSize);` | 画判定框（线框矩形） |

> ## `new Color(r, g, b, a)` 的四个参数
>
> | 参数 | 范围 | 含义 |
> |---|---|---|
> | `r` | 0~1 | 红色分量 |
> | `g` | 0~1 | 绿色分量 |
> | `b` | 0~1 | 蓝色分量 |
> | `a` | 0~1 | **透明度**（alpha）。0 = 全透明，1 = 完全不透明 |
>
> **注意是 0~1 而不是 0~255**——Unity 的 `Color` 用浮点数，`255` 的红色是 `(1, 0, 0)`。
>
> 三个圈的颜色从**外到内越来越不透明**：
>
> | 圈 | 颜色 | alpha | 视觉重量 |
> |---|---|---|---|
> | 发现（9） | 半透明黄 | 0.4 | 最轻 |
> | 放弃（14） | 更透明橙 | 0.3 | 最轻 |
> | 攻击（1.3） | 红 | 0.9 | 最重 |
> | 判定框 | 半透明红 | 0.6 | 中等 |
>
> **透明度是分层的视觉语言**：重要的（攻击范围）用高不透明度，次要的（发现范围）用低不透明度。这样四个图形叠在一起时不会乱成一团。

> ## 怎么用这个 Gizmos 排查问题
>
> **这是本文件最实用的部分。** 选中任意一个敌人，Scene 视图里会出现四个图形：
>
> ```
>         ╭───────────────╮
>        ╱  放弃追击圈 14   ╲
>       │   ╭───────────╮   │
>       │  ╱  发现圈 9    ╲  │
>       │ │    ╭─────╮    │ │
>       │ │   ╱ 攻击 1.3 ╲  │ │
>       │ │  │    ▣     │  │ │   ← 判定框
>       │ │   ╲         ╱  │ │
>       │ │    ╰─────╯    │ │
>       │  ╲             ╱  │
>       │   ╰───────────╯   │
>        ╲                 ╱
>         ╰───────────────╯
> ```
>
> ### 排查场景一：敌人不追我
>
> **看**：玩家有没有进**黄色发现圈**（半径 9）？
>
> | 观察 | 结论 |
> |---|---|
> | 玩家在黄圈**外** | 正常。走近一点就追了 |
> | 玩家在黄圈**内**，敌人不追 | → **`playerLayer` 没勾 `Player`**（最可能），或者玩家的碰撞体不在 Player 层上 |
>
> ### 排查场景二：敌人追到一半不追了
>
> **看**：玩家有没有超出**橙色放弃圈**（半径 14）？
>
> | 观察 | 结论 |
> |---|---|
> | 玩家在橙圈外 | 正常，这是设计（双阈值） |
> | 玩家在橙圈内，敌人却停住了 | → 检查是不是玩家死了（第 99 行会拒绝死掉的玩家） |
>
> ### 排查场景三：敌人打不到我
>
> **看**：**红色判定框**有没有覆盖到玩家？
>
> | 观察 | 结论 |
> |---|---|
> | 红框和玩家**重叠**，却不掉血 | → **`Attack_Enemy.asset` 的 `targetLayers` 没勾 `Player`** |
> | 红框和玩家**不重叠** | → 攻击距离或 `hitboxSize` 太小 |
> | 红框位置**跑到敌人身后** | → `_facing` 有问题（朝向翻转没生效） |
> | **没有红框** | → `attackData` 槽是空的（第 184 行直接 `return` 了） |
>
> > **「红框重叠却不掉血」这种情况，永远先看 Layer，再看代码。** 这是本项目第 4 条设计约定（见总览）：`LayerMask` 留空不报错，是最阴险的 bug。

> ## 第 186~188 行为什么和第 134~135 行写得一模一样
>
> 对比：
>
> ```csharp
> // Attack() 第 134~135 行
> Vector2 center = (Vector2)transform.position
>                + new Vector2(attackData.hitboxOffset.x * _facing, attackData.hitboxOffset.y);
>
> // OnDrawGizmosSelected() 第 186~187 行
> Vector2 center = (Vector2)transform.position
>                + new Vector2(attackData.hitboxOffset.x * _facing, attackData.hitboxOffset.y);
> ```
>
> **一字不差。**
>
> ### 这是重复代码，但它是「好的重复」
>
> **理由**：这两处**必须保持一致**——Gizmos 画出来的框，必须就是实际打出去的框。如果不一致，调试工具就在撒谎。
>
> 那为什么不抽成一个方法？
>
> ```csharp
> private Vector2 GetHitboxCenter()
> {
>     return (Vector2)transform.position
>          + new Vector2(attackData.hitboxOffset.x * _facing, attackData.hitboxOffset.y);
> }
> ```
>
> **这确实更好，而且应该这么改。** 当前写法的风险是：某天你改了 `Attack()` 里的偏移算法（比如加上「攻击时向前小步突进」），忘了同步改 Gizmos——**调试工具从此开始骗你**，而这种错误极难发现。
>
> > **`MeleeAttacker` 里也有同样的重复**（`Execute()` 和 `OnDrawGizmos` 各算一次判定框中心）。
> >
> > **这属于「值得重构但优先级不高」的项**：功能正确，只是维护性稍差。等哪天真要改偏移算法时，一起抽出来即可。
> >
> > **判断「重复代码」好坏的标准**：如果两处重复**必须同步变化**，就该抽成方法；如果只是碰巧长得像、但会独立演化，就该留着。

---

## 五、在 Unity 里怎么配

### 第 1 步：创建物体

1. Hierarchy 右键 → `2D Object` → `Sprites` → `Square`
2. 命名 `Enemy`
3. **`Layer` 设成 `Enemy`** ⚠️ 不设的话玩家**打不到它**（`Attack_Basic.asset` 的 `targetLayers` 只勾了 `Enemy`）
4. `Transform` → `Scale` 设成 `(0.9, 1.1, 1)`——比玩家略瘦略高，视觉上更容易区分
5. `Sprite Renderer` → `Color` 调成暗紫或暗红（和玩家的白色区分开）

### 第 2 步：加组件（顺序很重要）

**Rigidbody2D**

| 参数 | 值 | 为什么 |
|---|---|---|
| `Body Type` | `Dynamic` | 要受重力，必须动态 |
| `Material` | `NoFriction` | 摩擦 0，否则贴墙会粘住 |
| `Gravity Scale` | `3` | 和玩家一致的下落速度 |
| `Collision Detection` | `Continuous` | 防止高速时穿透 |
| `Interpolation` | `Interpolate` | 画面平滑 |
| `Constraints` | 勾 `Freeze Rotation Z` | 否则撞墙会打转 |

**CapsuleCollider2D**

| 参数 | 值 |
|---|---|
| `Direction` | `Vertical` |
| `Size` | `(1, 1)` |

> **⚠️ 顺序很重要**：`Rigidbody2D` 和碰撞体必须在 `Enemy Controller` **之前**加。因为 `EnemyController` 有 `[RequireComponent(typeof(Rigidbody2D))]`——反过来的话 Unity 会自动补一个默认参数的 `Rigidbody2D`（`Gravity Scale` 是 1），你还得手动改。

**CharacterStats**

| 字段 | 值 | 说明 |
|---|---|---|
| `Class Data` | **留空** | 敌人没有职业 |
| `Base Max Health` | `40` | 手填的基础值 |
| `Base Attack` | `8` | 手填 |
| `Base Move Speed` | `3.2` | **必须手填**，否则第 106 行会退回兜底的 `3f` |
| 其它 | `0` 或默认 | 敌人不暴击、没防御 |

> **敌人为什么要手填 `Base Move Speed`？**
>
> 因为 `ClassData` 是「玩家职业」专用的数据资产。敌人没有那么复杂的配置，直接在 `CharacterStats` 里手填基础值就够了。
>
> **这正好演示了 `CharacterStats` 的通用性**——同一个组件，玩家用「职业资产」喂它，敌人用手填值喂它，上层代码完全不用区分。

**Health**

| 字段 | 值 |
|---|---|
| `Max Health` | `40`（和 `CharacterStats` 的基础值一致） |
| `Invincibility Duration` | `0.5`（默认） |

> ⚠️ **`Health.maxHealth` 和 `CharacterStats` 的 `Base Max Health` 要填一样的值。** 因为 `CharacterStats` 会在 `Start()` 里调 `Health.SetMaxHealth()` 覆盖它——**填不一致的话，Inspector 里看到的会是错的**，调参时会困惑。

**HitReaction** — 直接添加，用默认值即可。

**EnemyController**

| 字段 | 值 | ⚠️ |
|---|---|---|
| `Detect Range` | `9` | |
| `Attack Range` | `1.3` | |
| `Player Layer` | 勾 **`Player`** | ⚠️ **不勾就永远发现不了玩家，且无报错** |
| `Lose Target Range` | `14` | |
| `Acceleration` | `30` | |
| `Attack Data` | 拖入 `Attack_Enemy` | ⚠️ 不拖就不会攻击 |
| `Death Delay` | `0.3` | |

### 第 3 步：配置 `Attack_Enemy.asset`

在 `Assets/_Project/Data/Weapons/` 右键 → `Create` → `Roguelike` → `Attack Data`，命名 `Attack_Enemy`，然后填：

| 字段 | 值 | ⚠️ |
|---|---|---|
| `Display Name` | `敌人挥击` | |
| `Damage Multiplier` | `1` | 伤害 = 攻击力 8 × 1 = 8 |
| `Knockback Force` | `9` | 比玩家的 7 略大，让玩家能感到「被打飞了」 |
| `Bonus Crit Chance` | `0` | 敌人不暴击 |
| `Hitbox Offset` | `(0.8, 0)` | 朝前方 0.8 格 |
| `Hitbox Size` | `(1.2, 1)` | |
| `Target Layers` | 勾 **`Player`** | ⚠️ **不勾就打不到玩家，且无报错** |
| `Cooldown` | `1.2` | 每秒攻击不到一次，给玩家反应时间 |

### 第 4 步：验证

点 ▶ 运行，把玩家走到敌人附近：

| 应该发生 | 如果没发生 |
|---|---|
| 敌人转头朝你走来 | `Player Layer` 没勾 |
| 靠近后停下并挥击 | `Attack Data` 是空的 |
| 你掉血 + 闪白 + 被击退 | `Attack_Enemy` 的 `Target Layers` 没勾 `Player` |
| 你砍它时它被击退（不顶着击退走） | `Hit Reaction` 组件没加 |
| 血量归 0 后它静止 0.3 秒然后消失 | `Health` 组件没加，或者 `Death Delay` 是 0 |

---

## 六、踩过的坑

> **现象**：敌人完全不动，也不攻击，站在场景里像块石头。Console 干净。
>
> **根因**：`Player Layer` 没勾 `Player`。`LayerMask` 留空时值是 0，第 95 行的 `Physics2D.OverlapCircle(..., 0)` 只检查「层 0（Default）」，而玩家在层 6（`Player`）。所以永远返回 `null`，`_target` 永远是 `null`，第 67 行一直执行 `Brake()`。
>
> **解法**：Inspector 把 `Player Layer` 勾上 `Player`。
>
> **教训**：这是本项目所有 `LayerMask` 字段的通病——**留空不报错，只静默失效**。所以全项目的 `LayerMask` 字段都加了 `[Tooltip("⚠️ 必须选 XXX 层")]`。
>
> **排查优先级**：遇到「没反应」，**先看 Inspector 的层勾选，再看代码。**

> **现象**：敌人打不死。血条空了（如果做了血条），但敌人还在动。
>
> **根因**：`Attack_Enemy.asset` 的 `Target Layers` 勾错了层（勾成了 `Enemy` 而不是 `Player`）。
>
> **解法**：改成勾 `Player`。
>
> **教训**：第 137 行的 `Physics2D.OverlapBoxAll(center, size, 0f, attackData.targetLayers)` 里，`targetLayers` 是**攻击方武器数据**里的字段，不是敌人自己的配置。所以**同一个 `AttackData` 资产被多个物体引用时，它们打的是同一批层**。玩家和敌人之所以能互相打到，是因为它们各自用了不同的 `.asset`（`Attack_Basic` 勾 `Enemy`，`Attack_Enemy` 勾 `Player`）。

> **现象**：敌人被打的时候，一边被击退一边还在往前走，**击退完全看不出来**。
>
> **根因**：`FixedUpdate` 里没有第 61 行的硬直判断。`Chase()` 每帧调用 `Mathf.MoveTowards`，把 `HitReaction` 设置的击退速度**立刻拉回正常移速**。
>
> **解法**：加第 61 行 `if (_hitReaction != null && _hitReaction.IsStunned) return;`。
>
> **教训**：**任何「每帧强制设置速度」的逻辑都会冲掉外力效果。** 击退、爆炸推力、弹簧板、传送带——这些机制全都需要「短暂放弃控制权」才能生效。
>
> 这条在 `PlayerController` 里也踩过同样的坑（那边用了部分放弃 + 保留重力）。

> **现象**：敌人死后，玩家走过去被尸体**挡住**了。
>
> **根因**：`OnDied()` 只写了 `Destroy(gameObject, deathDelay)`，没有 `_rb.simulated = false`。那 0.3 秒里尸体的碰撞体还在物理世界里，玩家会撞上去。
>
> **解法**：加第 166 行 `_rb.simulated = false;`。
>
> **教训**：**「延时销毁」的这 0.3 秒里，物体是完整活着的**——碰撞、`FixedUpdate`、被攻击判定，全都在。想清楚这 0.3 秒里它还能做什么，是使用延时销毁必须回答的问题。

> **现象**：打死敌人后，玩家还会被敌人打一下。
>
> **根因**：同样是那 0.3 秒。`FixedUpdate` 还跑 15 次，`Attack()` 还能执行。
>
> **解法**：第 58 行 `if (!_health.IsAlive) return;`。
>
> **教训**：和上一条是同一个问题的另一面。**「尸体不能行动」需要两处防护：物理（`simulated = false`）+ 逻辑（`IsAlive` 判断）。** 少了任何一处都会漏。

---

## 七、如果要改，改这里

### 想加巡逻行为

在 `FixedUpdate` 第 65 行「没目标」的分支里，把 `Brake()` 换成巡逻逻辑：

```csharp
if (_target == null)
{
    Patrol();     // 让敌人在出生点附近左右来回走
    return;
}
```

需要额外加字段 `_spawnPoint`、`_patrolDirection`、`patrolDistance`。

> ⚠️ 这时候状态就变成 4 个了（巡逻 / 追击 / 攻击 / 死亡）。**再加一个「撤退」就该考虑上状态机了**——这正是注释里说的门槛。

### 想加多个敌人类型

**当前设计已经支持了，不需要改代码。** 做法：

1. 复制现有的 `Enemy` 物体（`Ctrl + D`）
2. 改 `Scale`、`Color`
3. 改 `CharacterStats` 的基础值（比如 `Base Max Health` 60、`Base Attack` 12、`Base Move Speed` 2.5）
4. 改 `EnemyController` 的 `Detect Range`、`Attack Range`
5. 换一个新配的 `AttackData`

**得到一个「重甲慢速高伤」的敌人，一行代码都没写。** 这就是数据驱动的价值。

### 想让敌人会远程攻击

1. 新建 `Projectile.cs`（攻击层）——一个会飞的物体，碰到东西就 `TakeDamage`
2. 在 `EnemyController` 加一个 `bool isRanged` 开关
3. 在 `Attack()` 里分支：近战走现在的 `OverlapBoxAll`，远程 `Instantiate` 一个投射物

> ⚠️ **注意别把 `Attack()` 撑太大**。如果分支超过两个，应该抽成 `MeleeAttack()` 和 `RangedAttack()` 两个方法——**一个方法只做一件事**。

### 想让敌人有「发现玩家时的警觉动画」

在第 101 行 `_target = hit.transform;` 后面加：

```csharp
_target = hit.transform;
// 在这里播放警觉动画 / 音效
```

**但更好的做法是加一个事件**：

```csharp
public event System.Action AcquiredTarget;
// ...
_target = hit.transform;
AcquiredTarget?.Invoke();
```

这样表现层（动画、音效）可以订阅，而 `EnemyController` **不需要知道有音效这回事**——**和 `Health.Died` 是同一个模式。**

### 想改敌人的强度手感

| 你想要的效果 | 改哪个 | 在哪改 |
|---|---|---|
| 更肉 | `Base Max Health` | `CharacterStats`（资产或手填） |
| 更疼 | `Base Attack` 或 `damageMultiplier` | `CharacterStats` / `Attack_Enemy` |
| 跑得更快 | `Base Move Speed` | `CharacterStats` |
| 更早发现你 | `Detect Range` | `EnemyController` |
| 更难甩掉 | `Lose Target Range` | `EnemyController` |
| 攻速更快 | `Cooldown` | `Attack_Enemy` |
| 更迟钝 | `Acceleration` | `EnemyController`（降到 15 试试） |
| 尸体留更久 | `Death Delay` | `EnemyController` |

> **全部都是改 Inspector 数值，没有一处需要改代码。**

### 想在敌人身上加血条

新建 `HealthBarUI.cs`，订阅 `Health.Damaged` 事件更新显示。**`EnemyController` 一行都不用改。**

这正是总览里那条「解耦点」的又一处体现：

| 环节 | 解耦了什么 |
|---|---|
| `event Damaged` | 扣血 vs 受击表现 |
| `event Died` | 死亡 vs 死亡后发生什么 |

---

## 相关文档

- [06 · Health](06-Health.md) —— `_health.IsAlive` 和 `Died` 事件的来源
- [07 · HitReaction](07-HitReaction.md) —— 第 61 行 `IsStunned` 的来源
- [02 · CharacterStats](02-CharacterStats.md) —— 第 106、146 行读数值的地方
- [08 · AttackData](08-AttackData.md) —— 第 130~138 行用到的所有字段
- [12 · PlayerController](12-PlayerController.md) —— 同样是 `FixedUpdate` 结构，对比两者硬直处理的差异
