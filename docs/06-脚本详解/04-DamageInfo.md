# 04 · DamageInfo

## 一、头部信息表

| 项 | 内容 |
|---|---|
| 文件路径 | `D:\Roguelike2D\Assets\_Project\Scripts\Combat\DamageInfo.cs` |
| 所属层 | ② 结算层 |
| 依赖 | 无（只用到 `UnityEngine` 里的 `Vector2` 和 `GameObject`） |
| 被谁依赖 | `IDamageable`、`Health`、`HitReaction`、`MeleeAttacker`、`EnemyController`、`DamageZone` |
| 行数 | 25 行 |
| 最后更新 | Day 4 |

---

## 二、一句话定位

一次伤害的完整描述——把「打掉多少血、从哪里打来、谁打的」打包成一个可以整体传递的值。

---

## 三、为什么需要它

### 3.1 如果不打包，方法签名会失控

先看没有它的时候，`Health.TakeDamage` 会长什么样：

```csharp
// ❌ 参数版：每个信息都是独立参数
public void TakeDamage(float damage, Vector2 source, float knockback, GameObject attacker, bool isCrit)
```

5 个参数，调用的时候是这样：

```csharp
// ❌ 调用方要按顺序记住 5 个东西，而且看不出哪个是哪个
target.TakeDamage(13f, transform.position, 7f, gameObject, false);
```

这段代码你两周后回来看，必须去翻 `TakeDamage` 的定义才知道第三个 `7f` 是击退力度、第五个 `false` 是「没暴击」。**参数位置承载了语义，而位置是没有名字的。**

真正的代价出现在「想加一个新信息」的时候。假设第 3 周要做元素系统，需要给伤害加一个「元素类型」：

```csharp
// ❌ 签名一改，所有地方都要跟着改
public void TakeDamage(float damage, Vector2 source, float knockback,
                       GameObject attacker, bool isCrit, ElementType element)
```

你要改的地方：

| 文件 | 改什么 |
|---|---|
| `Health.cs` | `TakeDamage` 的签名 |
| `MeleeAttacker.cs` | 构造伤害的地方 |
| `EnemyController.cs` | 构造伤害的地方 |
| `DamageZone.cs` | 构造伤害的地方 |

而且这 4 处必须**同时**改对，漏一处就编译不过。编译不过还算好的——如果参数类型恰好相同（比如再加一个 `float`），编译器不会报错，你会得到一个**参数错位**的静默 bug。

用结构体之后，同样的事情只改一个地方：

```csharp
// ✅ 只在这里加一行，所有函数签名纹丝不动
public struct DamageInfo
{
    // ... 已有的 5 个字段 ...
    public ElementType Element;      // ← 新增
}
```

> **这就是「用结构体打包参数」的核心价值：把「加信息」的成本从「改 4 个文件」降到「改 1 行」。**
> 这个思路在 C# 里叫 Parameter Object（参数对象）模式，是很常见的一种重构手法。

### 3.2 为什么是 `struct` 而不是 `class`

这是本文件最值得讲的一个取舍。

**关键事实**：伤害数据是**一次性的、马上用完就丢**的东西。

看它的一生：

```
MeleeAttacker 里 new 出来 → 传给 TakeDamage → Health 读几个字段 → 函数返回 → 再也没人用它
```

存活时间就是一次函数调用。而 `class` 是**引用类型**——每次 `new` 都会在**托管堆**上分配一块内存，然后这块内存要一直等到**垃圾回收器（GC）**来清理。

后果是：

| | `class`（引用类型） | `struct`（值类型） |
|---|---|---|
| 分配在哪 | 托管堆 | 栈上，或内联在宿主对象里 |
| 谁来回收 | GC | 函数返回自动消失 |
| 每次创建的开销 | 堆分配 + 后续 GC 扫描 | 几乎为零 |
| 什么时候会卡 | GC 触发时导致掉帧 | 不会 |

一局 20 分钟的游戏，如果平均每 0.5 秒产生一次伤害，那就是 **2400 次**。看起来不多，但真实情况是：玩家的攻击判定可能一次命中 3 个敌人、地刺每 0.6 秒 tick 一次、Boss 的弹幕一次生成 20 发——**每帧产生几十次伤害是完全可能的**。

而 GC 的可怕之处不在于「慢」，在于**不可预测**：它会在某一帧突然花 15 毫秒清理垃圾，导致画面卡一下。对动作游戏来说，随机掉帧比稳定低帧率更致命——玩家会觉得自己「按了没反应」，把死因归咎于游戏。

**用 `struct` 就是把这个开销提前消掉。**

> **代价也要说清楚**：`struct` 是**值拷贝**。每次把它赋给另一个变量、或当作参数传递，整个结构体（这里是 5 个字段，约 24 字节）都会被复制一份。
>
> 所以这不是「struct 一定比 class 好」，而是**权衡**：
> - 小（字段少、字节少）+ 短命 → 适合 `struct`
> - 大（十几个字段）+ 需要继承 + 需要共享引用 → 适合 `class`
>
> `DamageInfo` 只有 5 个字段，且绝对不会被继承，所以它是 `struct` 的典型适用场景。

### 3.3 「无行为的纯数据」意味着什么

注意这个结构体**一个方法都没有**。它不会「计算最终伤害」，不会「应用减伤」，不会「播放特效」。

那这些事谁做？

| 事情 | 谁做 | 为什么是它 |
|---|---|---|
| 计算伤害数值 | `MeleeAttacker` / `EnemyController` | 它们知道攻击力、暴击率、技能倍率 |
| 应用防御减伤 | `Health` | 只有它知道挨打方的防御力 |
| 决定怎么闪白 | `HitReaction` | 表现层的事 |
| 决定击退方向 | `HitReaction` | 它知道自己的位置 |

**`DamageInfo` 只负责「传递信息」，不负责「处理信息」。**

这个取舍的好处是：它可以被随意创建、复制、缓存、写进日志、塞进数组，**永远不会带来副作用**。你不可能「调用 `DamageInfo` 导致敌人掉血」——它根本没有这种能力。

代价是：想弄清楚「这次伤害最终扣了多少血」，你不能只看 `DamageInfo`，得跟着流程走进 `Health`。**信息被拆到了几个文件里。**

> 这和整个项目的思路是一致的：数据（`ClassData`、`AttackData`）放数据结构，行为（`Health`、`MeleeAttacker`）放 `MonoBehaviour`。
> 想深挖这条约定的理由，去看 `00-总览与阅读指南.md` 第四节。

---

## 四、代码全解

### 4.1 文件头（第 1~2 行）

```csharp
1: using UnityEngine;
2: 
```

| 行 | 代码 | 含义 |
|---|---|---|
| 1 | `using UnityEngine;` | 引入 Unity 引擎的命名空间。这一行是必须的，因为下面用到了 `Vector2` 和 `GameObject`——这两个类型都定义在 `UnityEngine` 里，不写 `using` 就得写成 `UnityEngine.Vector2`，很啰嗦 |
| 2 | （空行） | 分隔「引用」和「类型声明」，是 C# 的通用排版习惯 |

> **`using` 是编译期的东西，不产生任何运行时开销。** 它只是告诉编译器「接下来我写的短名字，去这些命名空间里找」。所以不用纠结 `using` 会不会影响性能——不会。

### 4.2 类型声明与文档注释（第 3~10 行）

```csharp
3: /// <summary>
4: /// 一次伤害的完整描述。
5: ///
6: /// 用结构体而不是一堆方法参数：将来要加「元素类型」「伤害类型」「是否破防」时，
7: /// 只需要在这里加字段，所有攻击方和受击方的函数签名都不用动。
8: /// </summary>
9: public struct DamageInfo
10: {
```

| 行 | 代码 | 含义 |
|---|---|---|
| 3~8 | `/// <summary> ... </summary>` | **XML 文档注释**。三个斜杠（不是两个）是 C# 的特殊语法，编译器会把里面的内容提取出来，Visual Studio / Rider 里鼠标悬停在 `DamageInfo` 上时就会弹出这段文字 |
| 4 | `一次伤害的完整描述。` | 摘要第一行。**这一行会出现在 IDE 的自动补全提示里**，所以要写得短 |
| 5 | （`///` 后面什么都没有） | 文档注释里的空行，用来把摘要和正文分开 |
| 6~7 | 说明设计意图 | 记录「为什么用结构体」。**注释解释为什么，代码解释是什么**——这是本项目的注释约定 |
| 9 | `public struct DamageInfo` | 声明一个**公开的结构体**，类型名 `DamageInfo`。`public` 表示任何脚本都能用它；`struct` 是关键字，明确告诉编译器和读者「这是值类型，不是类」 |
| 10 | `{` | 类型体的开始。C# 用花括号划分作用域，**不需要分号**（这一点和 `class` 一样） |

> **`struct` 和 `class` 在声明语法上只差一个关键字**，但语义完全不同。C# 里没有任何东西能从名字看出一个类型是值类型还是引用类型（不像 C++ 的指针语法），所以**看到类型名一定要去确认它是 `struct` 还是 `class`**。
>
> 一个简单的判断法：如果你能写 `DamageInfo d;` 而不 `new` 它就开始用（`d.Amount` 是 0 而不是报空引用），那它多半是 `struct`。

> **为什么没有构造函数？**
> `struct` 在 C# 里**永远有一个隐式的无参构造函数**，它把所有字段设成默认值（数值是 0，引用是 `null`）。
> 所以 `new DamageInfo()` 永远不会返回 `null`，也不会报错——它返回一个所有字段都是 0 / `null` 的实例。
> 这正是下面「用对象初始化器填写字段」能成立的前提。

### 4.3 五个字段（第 11~24 行）

```csharp
11:     /// <summary>伤害数值</summary>
12:     public float Amount;
13: 
14:     /// <summary>伤害来源的世界坐标，用于计算击退方向</summary>
15:     public Vector2 SourcePosition;
16: 
17:     /// <summary>击退力度，0 表示不击退</summary>
18:     public float KnockbackForce;
19: 
20:     /// <summary>攻击者，用于避免自己打自己，以及将来的伤害统计</summary>
21:     public GameObject Attacker;
22: 
23:     /// <summary>是否暴击（用于飘字颜色等表现）</summary>
24:     public bool IsCritical;
```

| 行 | 代码 | 含义 |
|---|---|---|
| 11 | `/// <summary>伤害数值</summary>` | 单行形式的文档注释。字段的说明写在同一行，比展开成三行更紧凑 |
| 12 | `public float Amount;` | **这次要扣掉的血量**（减伤之前）。注意它是「攻击方算好的数值」，不是「百分比」也不是「已经减伤后的值」——防御减伤发生在 `Health` 里。因为这个字段是 `public` 的**字段**而不是属性，外部可以随意读写 |
| 13 | （空行） | 每个字段之间空一行。这个结构体没有分组注释（`// ---- xxx ----`），因为字段少，空行足够分隔 |
| 14 | `/// <summary>伤害来源的世界坐标...</summary>` | 说明 `SourcePosition` 的用途是「算击退方向」——这个提示很重要，否则读者会以为它只是用来做特效定位的 |
| 15 | `public Vector2 SourcePosition;` | **攻击发生时，攻击者所在的世界坐标**。`Vector2` 是 Unity 提供的二维向量结构体，内部就是两个 `float`（`x` 和 `y`）。它存的是**攻击者**的位置，不是受击者的位置——因为受击者可以用自己的 `transform.position`，而只有攻击方知道攻击是从哪来的 |
| 17 | `/// <summary>击退力度，0 表示不击退</summary>` | `0` 这个约定要写进注释，因为「不击退」和「击退 0 距离」在代码上是一回事，但设计意图不同。比如岩浆的击退就该是 0（不该把人弹开），而地刺可以给一点 |
| 18 | `public float KnockbackForce;` | **击退的力度**，是个速度值（单位：单位/秒）。最终怎么用它由 `HitReaction` 决定——它会乘上方向，直接赋值给刚体的 `linearVelocity`。所以这个数值大致就是「受击瞬间被推开的初速度」 |
| 20 | `/// <summary>攻击者...</summary>` | 注释里写了两个用途：现在的「避免自己打自己」和将来的「伤害统计」。**把「将来要用来干嘛」也写进注释**，是一种很实用的习惯——半年后你会感谢自己 |
| 21 | `public GameObject Attacker;` | **发起攻击的那个 GameObject**。注意类型是 `GameObject` 而不是 `Transform` 或某个具体组件——因为攻击者可能是玩家、敌人、陷阱，它们的组件类型各不相同，`GameObject` 是它们的共同祖先，谁都能赋给它 |
| 23 | `/// <summary>是否暴击...</summary>` | 说明这个字段**目前还没有人读**，是给未来的伤害飘字预留的 |
| 24 | `public bool IsCritical;` | **这一次伤害是不是暴击**。写入方是 `MeleeAttacker`（它掷了骰子）；读取方目前没有——将来伤害飘字用它决定「数字显示成黄色还是白色」。**留着不用的字段不代表没用**，它已经真实地参与了伤害计算，只是表现层还没跟上 |
| 25 | `}` | 类型体结束，同样不需要分号 |

> **为什么这些字段是 `public` 字段，而不是 `{ get; set; }` 属性？**
>
> 一般 C# 规范建议用属性而不是公开字段（属性可以加校验、可以只读）。
> 但**「纯数据载体」类型的惯例是反过来**：只装数据、没有任何逻辑的结构体，用公开字段更简单、更省内存（属性本质是方法），而且 C# 官方文档里的 `struct` 示例也是这么写的。
>
> 什么时候必须改成属性？当你需要「赋值时做校验」的时候。比如想禁止负数伤害：
> ```csharp
> private float _amount;
> public float Amount
> {
>     get => _amount;
>     set => _amount = Mathf.Max(0f, value);   // 负数一律存成 0
> }
> ```
> 现在没这个需求，所以保持最简单的写法。

> **注意这些字段都没有 `[SerializeField]`。**
> `[SerializeField]` 的作用是「让 Unity 把这个私有字段序列化到 Inspector 里」。它只对 `MonoBehaviour` 和 `ScriptableObject` 的子类有效。
> `DamageInfo` 两个都不是——它既不挂在物体上，也不是资产，所以 Inspector 里根本看不到它，自然也不需要 `[SerializeField]`。

> **`GameObject Attacker` 是引用类型，放在 `struct` 里意味着什么？**
> `struct` 里存引用类型字段是**完全合法**的。此时结构体内部存的是一个「指向堆上那个 GameObject 的引用」（可以粗略理解为地址）。
> 所以 `DamageInfo` 的复制是**浅拷贝**：复制后两个副本的 `Attacker` 指向**同一个** GameObject。这在这里完全没问题——我们本来就想知道「是谁打的」，不需要每个副本都有一份自己的 GameObject。
> 真正需要小心的是反过来：如果 `struct` 里放的是**另一个可变 `struct`**，复制后改一个不会影响另一个，那才容易出 bug。

### 4.4 它被怎么创建和使用

`DamageInfo` 自己没有任何逻辑，它的价值体现在**别人怎么用它**。看一次真实的调用（`MeleeAttacker.cs` 里）：

```csharp
target.TakeDamage(new DamageInfo
{
    Amount          = damage,
    SourcePosition  = transform.position,
    KnockbackForce  = data.knockbackForce,
    Attacker        = gameObject,
    IsCritical      = isCrit,
});
```

这里用到了 C# 的**对象初始化器（object initializer）**语法：

| 部分 | 含义 |
|---|---|
| `new DamageInfo` | 创建一个 `DamageInfo`。**括号可以省略**——`struct` 的隐式无参构造函数不接受参数 |
| `{ ... }` | 初始化器块，在对象创建后立刻给字段赋值 |
| `Amount = damage,` | 把局部变量 `damage` 的值赋给 `Amount` 字段 |

> **对象初始化器的关键性质：它是「先创建、后赋值」，不是「构造函数传参」。**
> 所以如果你漏写某个字段，它**不会报错**，只会保持默认值。
> 比如漏了 `Amount = damage`，那 `Amount` 就是 `0f`——伤害是 0，敌人不掉血，Console 干净得像没事发生。
>
> **这是本文件最容易踩的一个坑**（虽然目前还没踩到）：`DamageInfo` 的默认值是「一次 0 伤害、不击退、无来源的攻击」，看起来无害，但会静默地什么都不做。

---

## 五、在 Unity 里怎么配

**本脚本不需要挂在任何物体上，也不会出现在 Inspector 里。**

原因说清楚：

| 类型 | 能不能挂到 GameObject 上 | 能不能作为资产存在 |
|---|---|---|
| `MonoBehaviour` 的子类 | ✅ 能 | ❌ 不能 |
| `ScriptableObject` 的子类 | ❌ 不能 | ✅ 能（`ClassData`、`AttackData` 就是） |
| 普通 `class` / `struct` | ❌ 不能 | ❌ 不能 |

`DamageInfo` 是第三种。它既不是组件也不是资产，**它只在内存里存在**——由攻击方 `new` 出来，传给受击方，然后被 GC 回收（因为是 `struct`，连回收都不需要）。

所以你在 Project 面板里**找不到**它，在 `Add Component` 菜单里也**找不到**它。这是正常的。

**它唯一的「配置」入口在别处**：所有会影响 `DamageInfo` 内容的数值，都在 `AttackData` 资产里填：

| `AttackData` 里的字段 | 最终变成 `DamageInfo` 的 |
|---|---|
| `damageMultiplier` | 参与算出 `Amount` |
| `bonusCritChance` | 参与决定 `IsCritical` |
| `knockbackForce` | 直接赋给 `KnockbackForce` |
| （攻击者的 `transform.position`） | 直接赋给 `SourcePosition` |

---

## 六、踩过的坑

**暂无。**

这个结构体本身没有出过 bug——它没有逻辑，也就没有出错的空间。这正是「纯数据」的价值之一。

> 不过有一条**设计上的历史证据**值得记下来：
> `IsCritical` 这个字段是后加的（最初的 4 个字段是 `Amount`、`SourcePosition`、`KnockbackForce`、`Attacker`）。
> 加它的时候，`Health.TakeDamage`、`MeleeAttacker`、`EnemyController`、`DamageZone` 的**函数签名一个字都没改**，只是多赋值了一个字段。
>
> 这就是这个文件存在的意义，也是它能被写进简历的那句话的由来。

---

## 七、如果要改，改这里

### 7.1 加新字段（最常见的改动）

直接在这个结构体里加。**函数签名永远不用动**，这是它的设计目的。

```csharp
/// <summary>元素类型，0 = 无属性</summary>
public ElementType Element;

/// <summary>是否无视防御（真实伤害）</summary>
public bool IgnoreDefense;

/// <summary>伤害类型，用于区分「近战 / 远程 / 环境」</summary>
public DamageSourceType SourceType;
```

加完之后顺手想想：**有哪些地方需要填它？** 搜一下 `new DamageInfo` 就知道——目前有 3 处（`MeleeAttacker`、`EnemyController`、`DamageZone`）。不填的字段保持默认值（0 / `false`），不会报错，所以**漏填是静默的**。这一点要格外小心。

### 7.2 想强制「必须填全」

如果你担心将来漏填字段，可以加一个带参数的构造函数：

```csharp
public DamageInfo(float amount, Vector2 sourcePosition, float knockbackForce,
                  GameObject attacker, bool isCritical = false)
{
    Amount         = amount;
    SourcePosition = sourcePosition;
    KnockbackForce = knockbackForce;
    Attacker       = attacker;
    IsCritical     = isCritical;
}
```

> ⚠️ **注意**：一旦加了带参构造函数，C# 就**不再自动生成无参构造函数**了。那时候 `new DamageInfo()` 会编译报错，而 `default(DamageInfo)` 仍然可以（它是编译器层面的零值，不走构造函数）。
>
> 而且 `struct` 的构造函数**必须给所有字段赋值**才能编译通过——这刚好是你想要的强制性。

**当前没加**，因为现在只有 3 个调用点，`new DamageInfo { ... }` 的可读性更好（每个字段名都写在脸上）。等调用点多到 10 个以上，再考虑加构造函数。

### 7.3 什么时候该改成 `class`

只有这三种情况：

1. **字段多到十几个**，复制的开销超过了 GC 的开销。经验阈值大概是 16 字节以下适合 `struct`，`DamageInfo` 现在是 5 个字段（`float` + `Vector2` + `float` + 引用 + `bool`）约 24 字节，还算安全
2. **需要继承**（比如 `FireDamageInfo : DamageInfo`）。`struct` 不能被继承，一个都不能
3. **需要「同一个伤害被多处共享修改」**——比如一个伤害被护盾挡了一部分，剩下继续传给本体，两处要看到同一份数据。`struct` 做不到（每次传递都是副本），`class` 可以

> **改成 `class` 的具体操作**：把 `public struct DamageInfo` 改成 `public class DamageInfo`。
> 其余的 `new DamageInfo { ... }` 语法**完全不用改**，照样能编译——因为对象初始化器对两种类型都适用。
> 但行为会变：从此每次创建都会产生 GC 垃圾，而且 `DamageInfo` 变量可以是 `null` 了（现在的代码默认它永远不是 `null`，改成 `class` 之后要检查）。

### 7.4 值语义的注意事项

因为 `DamageInfo` 是 `struct`，**传递时永远是复制**：

```csharp
DamageInfo info = new DamageInfo { Amount = 10f };

ModifyDamage(info);          // ❌ 传进去的是副本

void ModifyDamage(DamageInfo d)
{
    d.Amount *= 2f;          // 改的是副本，外面的 info.Amount 还是 10
}
```

想改就必须传引用：

```csharp
void ModifyDamage(ref DamageInfo d)   // ✅ 加 ref
{
    d.Amount *= 2f;                   // 现在改的是原来那个
}
```

**当前项目里没有这种需求**——`DamageInfo` 从创建到消费，中间没有任何人修改它。如果将来要加「护盾减免」这类需要改伤害的逻辑，记得用 `ref`，或者干脆改成 `class`。

### 7.5 不要在这里加逻辑

想加「伤害计算方法」「元素克制表」之类的东西，**不要加在这个结构体里**。

理由：它现在能被随意复制、缓存、写日志而不产生副作用。一旦有了方法，就可能有人写出 `info.CalculateFinalDamage()` 这种带隐藏副作用的调用，`struct` 的复制语义又会让「改了半天没生效」的问题变得难查。

要加计算，加到 `Health.TakeDamage` 或一个新的 `DamageCalculator` 静态类里。
