# 05 · IDamageable

## 一、头部信息表

| 项 | 内容 |
|---|---|
| 文件路径 | `D:\Roguelike2D\Assets\_Project\Scripts\Combat\IDamageable.cs` |
| 所属层 | ② 结算层 |
| 依赖 | `DamageInfo`（作为方法参数的类型） |
| 被谁依赖 | `MeleeAttacker`、`EnemyController`、`DamageZone` 都在调用它；`Health` 实现它 |
| 行数 | 14 行 |
| 最后更新 | Day 4 |

---

## 二、一句话定位

「能挨打」的统一接口——让攻击方只需要知道「这东西能挨打」，而不需要知道它是什么。

---

## 三、为什么需要它

**这是本份文档最重要的一节。** 14 行代码改变的是整个战斗系统的形状，值得花时间讲透。

### 3.1 没有接口会怎样：一条会不断生长的 `if` 链

假设删掉 `IDamageable`，`MeleeAttacker.Execute()` 里那段循环就必须写成这样：

```csharp
// ❌ 没有接口的写法
foreach (var hit in hits)
{
    if (hit.TryGetComponent(out EnemyController enemy))
        enemy.TakeDamage(damage, transform.position, knockback, gameObject, isCrit);
    else if (hit.TryGetComponent(out PlayerController player))
        player.TakeDamage(damage, transform.position, knockback, gameObject, isCrit);
    else if (hit.TryGetComponent(out Barrel barrel))
        barrel.Break();
    else if (hit.TryGetComponent(out DestructibleWall wall))
        wall.Damage(damage);
    else if (hit.TryGetComponent(out TrainingDummy dummy))
        dummy.Hit(damage);
    // ...每加一种能被打的东西，就在这里加一个 else if
}
```

现在逐条数一下这段代码的代价。

### 3.2 代价一：攻击代码变成「公共垃圾场」

`MeleeAttacker` 是**所有攻击**的必经之路。玩家挥剑打到的任何东西，都要经过这个 `if` 链。

于是每一次「加一种可破坏物」的需求，都要回到这个文件里改：

| 第几周 | 需求 | 要改的文件 |
|---|---|---|
| Day 4 | 加第一个敌人 | `MeleeAttacker` |
| Day 5 | 加木桶 | `MeleeAttacker` |
| Day 6 | 加可破坏的墙 | `MeleeAttacker` |
| Day 8 | 加会反击的训练假人 | `MeleeAttacker` + `EnemyController`（敌人也要能打它） |
| Day 12 | 加 Boss 的护盾发生器 | `MeleeAttacker` + `EnemyController` |

**每加一种东西，都要动战斗代码。** 而且敌人（`EnemyController`）里有一份几乎一样的 `if` 链，要改两遍。

这个文件会变成整个项目里最常被修改、最容易冲突、最不敢碰的文件。**这是「高耦合」最典型的症状：一个地方的需求变化，逼着另一个不相关的地方跟着改。**

### 3.3 代价二：依赖方向反了，架构塌了

看项目的分层图（在 `00-总览与阅读指南.md` 第一节）：

```
攻击层  ──依赖──▶  结算层
```

**攻击层应该依赖结算层**，方向是往下的。

但上面那段 `if` 链里，`MeleeAttacker`（攻击层）不得不 `using` 或者至少知道 `EnemyController`、`PlayerController`、`Barrel` 这些**具体类型**。而这些类型里：

- `EnemyController` 是**控制层**的——比攻击层还高一层的
- `Barrel` 可能是**流程层**或**表现层**的

结果就是：**攻击层反向依赖了控制层和表现层**。

```
❌ 错误的依赖方向
攻击层  ──依赖──▶  结算层
   │
   └──依赖──▶  控制层（EnemyController）
   └──依赖──▶  表现层（Barrel）
```

一旦依赖成环，改任何一层都可能连锁影响。而且这时候你想「把战斗系统单独抽出来测试」已经不可能了——它拖着大半个项目。

### 3.4 代价三：有些东西根本没有「伤害」这个概念

木桶在 `if` 链里调用的是 `barrel.Break()`，不是 `TakeDamage`。因为它没有血量，它只有「完整 / 碎了」两种状态。

于是攻击方要理解：

- 敌人要传 5 个参数
- 木桶只要传 0 个参数
- 墙要传一个 `damage` 但不要击退和暴击

**攻击方被迫学习每一种被攻击物的独特接口。** 这违背了「攻击方只需要知道怎么打」这个本分。

### 3.5 接口把什么抽出来了

`IDamageable` 做的事情，是把「**能挨打**」这一个特征，从「**它到底是什么**」里剥离出来：

```
             ┌─────────────────────────────┐
   敌人  ────▶│                             │
   玩家  ────▶│       IDamageable           │◀──── MeleeAttacker
   木桶  ────▶│  IsAlive / TakeDamage       │◀──── EnemyController
   墙    ────▶│                             │◀──── DamageZone
   盾    ────▶└─────────────────────────────┘
```

攻击方看到的只有右侧那个接口。它**不知道也不需要知道**左边有几种东西。

于是 `MeleeAttacker` 里的代码变成：

```csharp
// ✅ 有接口的写法
foreach (var hit in hits)
{
    if (!hit.TryGetComponent(out IDamageable target)) continue;
    if (!target.IsAlive) continue;

    target.TakeDamage(new DamageInfo { ... });
}
```

**这段代码从写下的那天起，就再也不需要改了。** 加多少种敌人、多少种可破坏物，它都原样工作。

### 3.6 为什么是接口，不是抽象基类

C# 里「抽出一个共同特征」有两条路：`interface` 和 `abstract class`。这里必须选接口，理由是**硬性的**：

**C# 是单继承语言——一个类只能有一个父类。**

看 `Health` 的声明：

```csharp
public class Health : MonoBehaviour, IDamageable
{
```

它已经继承了 `MonoBehaviour`。如果 `IDamageable` 是抽象类，就会变成：

```csharp
// ❌ 编译错误：C# 不允许一个类继承两个基类
public class Health : MonoBehaviour, Damageable
```

**而 `MonoBehaviour` 是不能放弃的**——放弃它，`Health` 就不能挂到 GameObject 上，也就没法用 `Update`、`GetComponent`、Inspector 序列化。整个项目的基础设施都建立在它上面。

接口没有这个限制：

| | `abstract class` | `interface` |
|---|---|---|
| 一个类能继承几个 | **1 个** | **任意多个** |
| 能包含字段吗 | 能 | 不能（只能有属性、方法、事件的签名） |
| 能包含实现吗 | 能（虚方法、抽象方法混用） | 传统上不能，C# 8 之后可以写默认实现 |
| 能不能被 `ScriptableObject` 实现 | 不能（它也只能有一个基类） | 能 |

最后一行很关键：将来如果想让 `EquipmentData`（一个 `ScriptableObject`）也能被打碎，接口仍然是唯一的选择。

> **判断法**：当你抽出的特征是「**它是什么**」时，用抽象基类（`Dog` 和 `Cat` 都是 `Animal`）；当特征是「**它能做什么**」时，用接口（`Dog` 和 `Car` 都能 `IDamageable`？——不，更好的例子是 `Bird` 和 `Airplane` 都能 `IFlyable`）。
>
> 这里抽的是「能挨打」这个**能力**，所以是接口。

### 3.7 `IsAlive` 为什么必须在接口里

这是最容易漏掉的一环。看它的必要性：

`MeleeAttacker` 打出一个判定框，可能同时碰到了 3 个敌人，其中 1 个已经死了（刚被上一刀打死，还没来得及销毁）。

**如果没有 `IsAlive`**，攻击方要自己判断「它死没死」：

```csharp
// ❌ 又把具体类型暴露出来了
if (!hit.TryGetComponent(out IDamageable target)) continue;
if (hit.TryGetComponent(out Health h) && !h.IsAlive) continue;   // 倒退回了具体类型
target.TakeDamage(info);
```

这一行把 `Health` 这个**具体实现**重新拉进了攻击方的视野，接口的隔离效果被打了个洞。而且如果将来有第二种 `IDamageable` 实现（比如没有 `Health` 的纯逻辑可破坏物），这段判断就漏掉了它。

**有 `IsAlive` 之后**：

```csharp
// ✅ 接口内部消化了这件事
if (!hit.TryGetComponent(out IDamageable target)) continue;
if (!target.IsAlive) continue;
target.TakeDamage(info);
```

攻击方只需要相信接口的承诺。

> **更深一层的理由：「怎么算活着」是血量自己的事。**
>
> 现在的实现是 `Current > 0f`。但你可能想改成「有复活被动时，血量 0 也算活着」，或者「Boss 在 30% 血以下进入不死状态」。
>
> 如果这个判断散落在每个攻击方那里，你要改 3 个文件；放在 `Health` 里，改一处，所有攻击方自动跟着变。
>
> **把「判断」放在拥有数据的那一方**，是面向对象里最基本的一条纪律。

### 3.8 为什么 `TryGetComponent` 能用在接口上

这是 Unity 里一个不太为人知但非常好用的特性。

```csharp
if (!hit.TryGetComponent(out IDamageable target)) continue;
```

`IDamageable` 是接口，不是 `MonoBehaviour`。Unity 允许你这样查——底层做的事情是：

1. 遍历 `hit` 这个 GameObject 上的所有组件
2. 对每一个组件问一句「你实现了 `IDamageable` 吗」
3. 找到第一个说「是」的，转成 `IDamageable` 返回

> **注意第 3 步的措辞：找到「第一个」。**
> 如果一个 GameObject 上挂了两个都实现 `IDamageable` 的组件，你只会拿到其中一个，而且是顺序不确定的那个。
> 本项目里每个物体只挂一个 `Health`，所以不会遇到这个问题。但如果你以后想给 Boss 挂「本体血量 + 护盾血量」两个组件，**不要指望 `TryGetComponent` 能分别拿到它们**——那时应该显式地 `GetComponent<Health>()` 和 `GetComponent<Shield>()`。

**`TryGetComponent` 和 `GetComponent` 的区别**：

| | `GetComponent<T>()` | `TryGetComponent<T>(out T result)` |
|---|---|---|
| 找到时 | 返回组件 | 返回 `true`，`result` 是组件 |
| 没找到时 | 返回 `null` | 返回 `false`，`result` 是 `null` |
| 会不会产生 GC 垃圾 | 早期版本会（因为内部装箱），现在多数情况不会 | **不会** |
| 你容易忘的事 | **忘判 `null`**，然后下一行空引用崩溃 | 不太可能——`false` 分支必须先处理 |

性能上两者现在差别已经很小。**选 `TryGetComponent` 的真正理由是它的用法逼着你处理「没找到」的情况**。

对比一下：

```csharp
// ❌ 忘了判空就是运行时崩溃
var target = hit.GetComponent<IDamageable>();
target.TakeDamage(info);          // 如果没找到，这里 NullReferenceException

// ✅ 语法上就必须处理
if (!hit.TryGetComponent(out IDamageable target)) continue;
target.TakeDamage(info);
```

> 这个模式在整个项目里反复出现：`Health`、`HitReaction`、`MeleeAttacker`、`EnemyController` 里所有「可能不存在的组件」都用 `TryGetComponent`。

### 3.9 谁实现了它，谁在用

**实现者（目前只有一个）**

| 脚本 | 说明 |
|---|---|
| `Health.cs` | `public class Health : MonoBehaviour, IDamageable` |

`Health` 实现它是必然的——它是「有血量的东西」的通用组件。玩家、敌人、木桶、可破坏的墙，只要挂上 `Health` 就自动变成 `IDamageable`。

**使用者（三个）**

| 脚本 | 用在哪 | 打的是谁 |
|---|---|---|
| `MeleeAttacker.Execute()` | 玩家攻击 | 由 `AttackData.targetLayers` 决定（勾了 `Enemy` 就打敌人） |
| `EnemyController.Attack()` | 敌人攻击 | 由 `AttackData.targetLayers` 决定（勾了 `Player` 就打玩家） |
| `DamageZone.OnTriggerStay2D()` | 环境持续伤害 | 由 `DamageZone.targetLayers` 决定（勾了 `Player` 就烧玩家） |

**这三个使用者里的代码，除了「怎么算出伤害」不同，中间的调用路径是逐字相同的**：

```csharp
if (!other.TryGetComponent(out IDamageable target)) return;
if (!target.IsAlive) return;
target.TakeDamage(new DamageInfo { ... });
```

> **这带来一个很值得注意的性质：伤害接口是双向的。**
>
> `MeleeAttacker` 里没有任何一行写着「玩家」，`DamageZone` 里也没有任何一行写着「敌人」。
> 它们只是在打「某个 layer 上的、能挨打的东西」。
>
> 所以把 `MeleeAttacker` 的 `targetLayers` 改成 `Player`，**玩家就会挥剑砍自己**——代码一行都不用改。
> 这不是设计缺陷，恰恰是接口抽象的证明：**它不知道也不关心打的是谁。**

---

## 四、代码全解

### 4.1 文档注释（第 1~6 行）

```csharp
1: /// <summary>
2: /// 所有「能被打的东西」的统一入口。
3: ///
4: /// 攻击方只需要知道这个接口，完全不需要知道打的是敌人、玩家、木桶还是可破坏地形。
5: /// 这就是为什么以后加新敌人时，攻击代码一行都不用改。
6: /// </summary>
```

| 行 | 代码 | 含义 |
|---|---|---|
| 1 | `/// <summary>` | XML 文档注释开始。三个斜杠是 C# 的专用语法，IDE 会把内容提取成悬停提示 |
| 2 | `所有「能被打的东西」的统一入口。` | **摘要第一行**。这句话会出现在你写代码时的自动补全提示里，所以它要能独立表达意思 |
| 3 | `///`（空行） | 摘要与正文的分隔 |
| 4 | 说明接口的隔离范围 | 「不需要知道是敌人、玩家、木桶还是可破坏地形」——把当时能想到的四类都列出来了，这是注释应有的具体程度 |
| 5 | 说明**收益** | 「加新敌人时，攻击代码一行都不用改」。注释里写清收益，是给未来的自己一个「不要乱改」的警告 |
| 6 | `/// </summary>` | 文档注释结束 |

> **这份注释本身就是一个设计文档。** 半年后你想「顺手在攻击代码里加个 `if (hit.CompareTag("Boss"))`」时，读到第 5 行就会停下来。

### 4.2 接口声明（第 7~8 行）

```csharp
7: public interface IDamageable
8: {
```

| 行 | 代码 | 含义 |
|---|---|---|
| 7 | `public interface IDamageable` | 声明一个**公开的接口**。三个部分：<br>• `public`——任何脚本都能引用它<br>• `interface`——关键字，声明这是接口而不是类<br>• `IDamageable`——类型名。**`I` 前缀是 C# 的命名惯例**，一眼就能看出这是个接口（`IDisposable`、`IEnumerable`、`IComparable` 都是这个规矩） |
| 8 | `{` | 接口体的开始。注意**接口没有基类列表**——它不继承 `MonoBehaviour`，也不继承 `object`（虽然所有类型最终都是 `object`，但接口不写出来） |

> **为什么叫 `IDamageable` 而不是 `IHurtable`、`ICanBeHit`？**
> `-able` 后缀在 C# 里是接口的惯用命名法，表示「具备某种能力」：`IDamageable` = 能承受伤害（damageable），`IComparable` = 能比较，`IDisposable` = 能释放。
> 这个命名让调用方一眼读懂语义，比 `IHurt` 之类的更符合语言习惯。

> **接口不能有字段。**
> 这是接口和抽象类最实际的差别：接口里只能放**方法、属性、事件、索引器**这四种成员的**签名**（C# 8 之后还允许静态方法和默认实现，但本项目用不到）。
> 你没法在接口里写 `float health = 100f;`——因为接口不描述「有什么数据」，只描述「能做什么」。

### 4.3 `IsAlive` 属性签名（第 9~10 行）

```csharp
9:     /// <summary>是否还活着。已死亡的目标不应再结算伤害</summary>
10:     bool IsAlive { get; }
```

| 行 | 代码 | 含义 |
|---|---|---|
| 9 | `/// <summary>是否还活着。已死亡的目标不应再结算伤害</summary>` | 单行文档注释。**第二句话是给调用方的行为规范**：不只是「告诉你活没活」，而是「死了就别再打了」 |
| 10 | `bool IsAlive { get; }` | 一个**只有 getter 的属性签名**。逐部分拆解：<br>• `bool`——返回类型，真/假<br>• `IsAlive`——属性名，`Is` 开头是布尔属性的惯例（读起来像一句英文：`if (target.IsAlive)`）<br>• `{ get; }`——**只有读，没有写**<br>• **行尾没有分号**——属性本身的花括号就是它的边界 |

**关于 `{ get; }` 这个写法**，有三点值得说清楚：

**第一，接口里没有 `public` 关键字。**
你可能注意到这里少了 `public`，而 `DamageInfo` 的字段都写了。原因是：**接口成员默认就是 `public` 的**，写不写都一样。这里省略是主流写法。

**第二，它比「一个 `bool` 字段」给出了更强的约束。**

如果接口写成这样：

```csharp
// ❌ 这样写也能编译，但表达力弱得多
bool IsAlive;
```

那就是在说「实现方要提供一个可读可写的字段」。而 `{ get; }` 说的是「**实现方要提供一个只读的属性**」——

- 对**调用方**：承诺「你能读到它」
- 对**实现方**：承诺「你不需要提供 setter」

`Health` 里的实现是 `public bool IsAlive => Current > 0f;`——一个计算出来的属性，**根本没有存储空间可以写**。如果用字段版本，`Health` 就没法实现这个接口（除非改成每次血量变化都同步一个 `bool` 字段，那就多了一份可能不同步的状态）。

**第三，「接口只承诺读」是有设计意图的。**

攻击方凭什么改别人的生死状态？它唯一的合法动作是「打一下」（`TakeDamage`），至于打完死没死，那是血量自己算出来的结果。**把 setter 藏起来，就从语法上杜绝了「攻击方直接把敌人设成死亡」这种越权操作。**

> **一个类比**：`IsAlive` 像体温计，谁都能看，但只有身体自己能改。给攻击方一个 `set`，就像给每个路人一支能改体温的笔。

### 4.4 `TakeDamage` 方法签名（第 12~13 行）

```csharp
12:     /// <summary>结算一次伤害</summary>
13:     void TakeDamage(DamageInfo info);
```

| 行 | 代码 | 含义 |
|---|---|---|
| 12 | `/// <summary>结算一次伤害</summary>` | 文档注释。用词是「**结算**」而不是「扣血」——因为实现方要做的不只是扣血（还要处理无敌帧、防御减伤、触发事件、判断死亡） |
| 13 | `void TakeDamage(DamageInfo info);` | 一个**没有实现的方法签名**。逐部分拆解：<br>• `void`——不返回任何值。**这是有意的**：调用方不需要知道「打没打中」「扣了多少」，它只负责「打」，结果由受击方自己处理<br>• `TakeDamage`——方法名，动词开头<br>• `(DamageInfo info)`——参数。类型是 `DamageInfo` 结构体（见 `04-DamageInfo.md`）<br>• **行尾有分号**——方法签名必须用分号结尾，**这是它和属性签名（`{ get; }`）在语法上的分界** |

> **为什么返回 `void` 而不是 `bool`（表示「是否真的打中了」）？**
>
> 考虑一个真实场景：你打了一个处于无敌帧的敌人，`TakeDamage` 内部 `return` 了，什么都没发生。调用方要不要知道？
>
> 目前**不需要**。理由：
> 1. 攻击方不做后续处理——不会因为「没打中」就退冷却、或者播不同的特效
> 2. 攻击方可能一次打中多个目标（`OverlapBoxAll` 返回数组），返回单个 `bool` 表达不了「打中 3 个、其中 1 个无敌」
> 3. 如果将来确实需要（比如「命中才回血」），更合适的做法是**加一个事件**（`Health.Damaged` 已经有了），让关心的人订阅，而不是改签名
>
> **什么时候该改成返回 `bool`？** 当「打中了没有」会直接改变调用方**紧接着要执行的逻辑**时。目前没有这种情况。

> **为什么参数是 `DamageInfo` 而不是直接传 `float damage`？**
> 这是 `04-DamageInfo.md` 整篇在讲的事情，简单说：参数对象模式能把「加信息」的成本从「改 4 个文件」降到「改 1 行」。这里不重复展开。

### 4.5 接口结束（第 14 行）

```csharp
14: }
```

| 行 | 代码 | 含义 |
|---|---|---|
| 14 | `}` | 接口体结束。和 `class`、`struct` 一样，**不需要分号** |

> **注意整个文件没有 `using`。**
> 因为 `DamageInfo` 定义在**全局命名空间**（它没有 `namespace` 包裹），而 `bool`、`void` 是 C# 关键字，不需要任何 `using`。
> 这是全局命名空间带来的便利，也是小型项目的常见做法。项目变大之后通常会统一加 `namespace Roguelike2D.Combat { ... }`，那时这里就需要 `using` 了。

---

## 五、在 Unity 里怎么配

**本脚本不需要挂在任何物体上。**

而且有一个会让新手困惑的点：**它不会出现在 `Add Component` 菜单里。**

原因：Unity 的 `Add Component` 菜单只列出可以「挂」的东西，也就是 `MonoBehaviour` 和 `ScriptableObject` 的子类。接口两个都不是，所以 Unity 不给它入口。同理，Project 面板里也找不到它——它不是一个资产，只是一个类型定义。

**所以你「配置 `IDamageable`」的方式，其实是配置实现它的脚本：**

### 步骤一：给物体挂上 `Health`

`Health` 是唯一的实现者（见 `06-Health.md`）。挂上它，这个物体就自动变成了 `IDamageable`——不需要任何额外操作。

```csharp
public class Health : MonoBehaviour, IDamageable     // ← 这个接口列表就是全部的「配置」
```

### 步骤二：设置物体的 Layer ⚠️ 关键

**这才是真正决定「攻击方能不能打到它」的东西。**

攻击方（`MeleeAttacker`、`EnemyController`、`DamageZone`）都是用 `LayerMask` 过滤目标的：

```csharp
Collider2D[] hits = Physics2D.OverlapBoxAll(center, data.hitboxSize, 0f, data.targetLayers);
```

如果物体的 Layer 不在 `targetLayers` 里，`OverlapBoxAll` 根本不会把它放进返回数组，`TryGetComponent<IDamageable>` 也就**一次都不会被调用**。

按照本项目的约定：

| 物体 | Layer |
|---|---|
| 玩家 | `Player` |
| 敌人 | `Enemy` |
| 木桶、可破坏物 | 需要新建一个层，比如 `Destructible` |

### 步骤三：确认碰撞体存在

`OverlapBoxAll` 找的是 **`Collider2D`**，不是 `SpriteRenderer`。

一个物体「看得见但打不到」的最常见原因就是**只挂了 `SpriteRenderer`，忘了 `Collider2D`**——这在 Day 1 做地面时真实踩过（见 `07-HitReaction.md` 的相邻坑，以及 `docs/05-开发日志.md`）。

---

## 六、踩过的坑

### 6.1 `LayerMask` 留空导致的静默失败 ⭐

这是本项目里最阴险的一类 bug，而且它和 `IDamageable` 直接相关。

> **现象**：摆好了敌人，按下 `J`，什么都没发生。敌人不掉血、不闪白、不击退。**Console 里一条报错都没有。**
>
> **根因**：`Attack_Basic` 这个 `AttackData` 资产里的 `targetLayers` 字段没勾任何层（默认值是 `Nothing`）。
>
> 于是 `Physics2D.OverlapBoxAll(center, size, 0f, Nothing)` 返回一个**空数组**。
> `foreach` 一次都不执行，`TryGetComponent<IDamageable>` 根本没机会被调用。
> 从代码上看，一切正常——循环体写对了，接口用对了，伤害算对了。**唯一的错是「没有东西进入这个循环」。**
>
> **解法**：在 `AttackData` 资产的 Inspector 里，把 `Target Layers` 勾上 `Enemy`，并确认敌人的 Layer 也是 `Enemy`。**两个必须对上**：`AttackData` 说「我要打 Enemy 层」，敌人的 Layer 说「我是 Enemy 层」——少一边都打不到。

**为什么这个坑值得单独记**：

| 一般的 bug | 这个 bug |
|---|---|
| Console 报红色错误 | Console 完全干净 |
| 报错信息里有文件名和行号 | 没有任何提示 |
| 去看报错指向的那一行就懂了 | 只能靠「从判定框开始一层层往外查」 |

**排查顺序应该是**（这也是本项目后来定下的规矩）：

```
1. 选中攻击者，看 Scene 视图里的判定框在不在目标身上
      ↓ 不在 → 调 hitboxOffset / hitboxSize
      ↓ 在，但没反应 → 继续往下
2. 看 AttackData 的 targetLayers 勾了吗
      ↓ 没勾 → 这就是原因
      ↓ 勾了 → 继续往下
3. 看目标的 Layer 是不是 targetLayers 里那一层
      ↓ 不是 → 这就是原因
      ↓ 是 → 继续往下
4. 看目标身上有没有 Collider2D
5. 看目标身上有没有 Health（也就是有没有实现 IDamageable）
```

> **第 1 步能省掉大量时间。** 判定框是几何问题（框在不在），Layer 是过滤问题（框找到的东西进不进循环）——**这是两个完全不同的失败原因，用 Gizmos 一眼就能分辨**。
> 所以本项目所有攻击脚本都保留了 `OnDrawGizmosSelected`，这不是没删干净的调试代码，是长期工具。

---

## 七、如果要改，改这里

### 7.1 往接口里加成员 ⚠️ 谨慎

接口最需要小心的特性是：**加一个成员，所有实现者都必须跟着实现。**

现在只有 `Health` 一个实现者，所以加东西的成本很低。但要知道这个成本会随实现者数量线性增长：

| 实现者数量 | 加一个接口成员要改几个文件 |
|---|---|
| 1 个（现在） | 1 个 |
| 3 个 | 3 个 |
| 10 个 | 10 个 |

**所以：接口应该保持最小。** 只放「所有实现者都一定做得到」的成员。

假设你想加一个治疗：

```csharp
// ❌ 加进 IDamageable 会让所有实现者都被迫实现治疗
public interface IDamageable
{
    bool IsAlive { get; }
    void TakeDamage(DamageInfo info);
    void Heal(float amount);          // ← 木桶需要治疗吗？不需要，但它被迫实现
}
```

**正确做法是拆一个新接口**：

```csharp
// ✅ 谁需要治疗谁实现
public interface IHealable
{
    void Heal(float amount);
}

// 玩家和敌人两个都实现
public class Health : MonoBehaviour, IDamageable, IHealable
```

> 这叫**接口隔离原则（Interface Segregation Principle）**：不应该强迫实现者依赖它用不到的成员。
> 一个直观的判断法：**如果你能想象出「实现了这个接口但某个成员只能写成空方法」的情况，就说明该拆了。**

### 7.2 想加「护盾」「减伤」「免疫」等概念

**不要加进接口。** 这些是 `Health` 内部的实现细节，攻击方不需要知道。

攻击方的契约只有一句：「调 `TakeDamage`，剩下的我不管」。

具体的做法：在 `Health.TakeDamage` 内部处理。比如加护盾：

```csharp
// Health.cs 内部
float remaining = amount;
if (_shield > 0f)
{
    float absorbed = Mathf.Min(_shield, remaining);
    _shield -= absorbed;
    remaining -= absorbed;
}
Current = Mathf.Max(0f, Current - remaining);
```

**接口一个字都不用改。** 这就是「接口稳定、实现可变」的价值。

### 7.3 想区分「打中了但被免疫」和「打中了并造成伤害」

目前 `TakeDamage` 返回 `void`，调用方无法区分这两种情况。

有两种改法，**推荐第二种**：

```csharp
// 改法一：改签名（不推荐）
bool TakeDamage(DamageInfo info);   // 返回「是否真的造成了伤害」
```
代价：所有调用点都要处理返回值；而且「一次打中多个目标」时单个 `bool` 表达不了。

```csharp
// 改法二：加事件（推荐）
public interface IDamageable
{
    bool IsAlive { get; }
    void TakeDamage(DamageInfo info);

    /// <summary>伤害被无敌帧 / 护盾完全挡下时触发</summary>
    event Action<DamageInfo> DamageBlocked;
}
```
代价：要给 `Health` 加一个配套事件。好处：关心的脚本订阅，不关心的完全不受影响——**和 `Health.Damaged` 是同一套思路**。

### 7.4 什么时候该拆出更多接口

按「能力」拆，而不是按「对象」拆。对照表：

| 能力 | 接口 | 谁会实现 |
|---|---|---|
| 能挨打 | `IDamageable` | 玩家、敌人、木桶 |
| 能治疗 | `IHealable` | 玩家、敌人 |
| 能被打断 | `IInterruptible` | 正在蓄力的敌人 |
| 能被击退 | `IKnockbackable` | 玩家、敌人（木桶不该被击退） |
| 能被拾取 | `IPickupable` | 掉落物 |

> 注意「能被击退」这一条——**它其实已经埋了个伏笔**。
> `HitReaction` 里的击退代码是包在 `if (_rb != null && info.KnockbackForce > 0f)` 里的。
> 也就是说：**没有 `Rigidbody2D` 的物体自动不会被击退**，不需要额外判断。
> 如果将来这个「看组件在不在」的判断变得复杂（比如要区分「轻击退 / 重击退免疫」），再考虑抽 `IKnockbackable`。**现在不抽，因为还没有第二个理由。**

### 7.5 不要给接口加 Unity 特有的东西

这是硬性禁忌：

```csharp
// ❌ 接口绝对不能继承 MonoBehaviour
public interface IDamageable : MonoBehaviour { }     // 编译错误

// ❌ 接口里不能声明 [SerializeField]
public interface IDamageable
{
    [SerializeField] float hp;                        // 编译错误
}
```

**理由**：接口的干净是它最大的价值。一旦它拖上 Unity 的依赖，它就不再是「纯能力描述」，而变成了「Unity 专用组件」——那样它和抽象基类就没区别了，也就失去了「任意类型都能实现」的优势。

保持接口只依赖 BCL（.NET 基础类库）里的东西。本项目里 `IDamageable` 唯一的自定义依赖是 `DamageInfo`（一个 `struct`），这是合理的——因为「挨打」这件事本来就需要知道「挨的是什么打」。
