# 06 · Health

## 一、头部信息表

| 项 | 内容 |
|---|---|
| 文件路径 | `D:\Roguelike2D\Assets\_Project\Scripts\Combat\Health.cs` |
| 所属层 | ② 结算层 |
| 依赖 | `DamageInfo`、`IDamageable`、`CharacterStats`（**可选**，没有也能工作）、`StatType` |
| 被谁依赖 | `HitReaction`（订阅 `Damaged`）、`EnemyController`（读 `IsAlive`、订阅 `Died`）、`RunManager`（订阅 `Died`）、将来的 `HealthBarUI`（读 `Normalized`） |
| 行数 | 84 行 |
| 最后更新 | Day 4 |

---

## 二、一句话定位

血量、无敌帧、防御减伤、死亡事件的统一实现——玩家和敌人共用同一份，谁都不用自己写扣血逻辑。

---

## 三、为什么需要它

### 3.1 如果没有它：每个能挨打的东西都要自己写一遍

玩家要掉血，敌人要掉血。最直觉的做法是各写各的：

```csharp
// ❌ 玩家脚本里
public void TakeDamage(float damage)
{
    if (_isDead) return;
    if (_invincibleTimer > 0f) return;
    _hp -= damage;
    _invincibleTimer = 0.5f;
    StartCoroutine(FlashWhite());
    if (_hp <= 0f) { _isDead = true; GameOver(); }
}

// ❌ 敌人脚本里（几乎一样，但有几处不一样）
public void TakeDamage(float damage)
{
    if (_hp <= 0f) return;
    _hp -= damage;
    FlashRed();
    if (_hp <= 0f) { _isDead = true; Destroy(gameObject, 0.3f); }
    // ⚠️ 忘了写无敌帧
    // ⚠️ 闪的是红色不是白色
    // ⚠️ 死亡后的处理完全不同
}
```

这两段代码的**相似度 80%，但那 20% 的差异会不断制造 bug**：

| 问题 | 后果 |
|---|---|
| 敌人忘了写无敌帧 | 玩家一次挥剑打出 3 个判定帧，敌人瞬间掉 3 倍血 |
| 玩家忘了判 `_isDead` | 死亡后血条继续变负，UI 画反 |
| 想加「防御减伤」 | 两个文件都要改，容易只改一个 |
| 想加第三、第四种可破坏物 | 再复制一遍，bug 跟着复制 |

**更根本的问题是：「怎么算受伤」这件事，在整个项目里没有一个唯一的地方。** 你想知道「这个游戏的伤害是怎么结算的」，得翻遍所有能挨打的脚本。

### 3.2 `Health` 抽出了什么

`Health` 把「挨打」拆成三个层次，各自放在正确的位置：

```
① 数据层：CharacterStats    防御力 = 0.1
        │
        ▼  在第 55 行读取
② 结算层：Health            实际掉血 = 攻击力 × (1 - 0.1)
        │
        ▼  在第 62 行广播事件
③ 表现层：HitReaction       闪白 0.12s、击退、硬直 0.18s
```

**所有「怎么算」的逻辑集中在第 49~66 行的 `TakeDamage` 里**，一共 18 行。想知道伤害公式，只看这一个方法。

想加护盾、想加元素抗性、想加「受伤时反弹」，**全都是在这 18 行里加**。玩家和敌人同时生效，不会漏。

### 3.3 敌人和玩家用同一个组件，参数在 Inspector 里分

那「敌人闪红色、玩家闪白色」怎么办？

答案是：**`Health` 不管颜色**。颜色是 `HitReaction` 的事（见 `07-HitReaction.md`）。

`Health` 里唯一有差异的参数是 `maxHealth` 和 `invincibilityDuration`——而你甚至不需要为敌人和玩家填不同的 `maxHealth`：

| 物体 | `Max Health` 填什么 | 实际生效的值 |
|---|---|---|
| 玩家 | 随便（100 就行） | 由 `CharacterStats` 的 `ClassData` 覆盖（战士 140 / 法师 85） |
| 敌人 | 随便（100 就行） | 由 `CharacterStats` 手填的基础值决定（40） |
| 木桶 / 可破坏物 | **这就是真值** | 填多少就是多少（比如 30），因为没挂 `CharacterStats` |

> **这个设计带来一个很实际的便利：加一种新敌人时，只要调 `CharacterStats` 上的数字，`Health` 完全不用碰。**

### 3.4 它是「数据层」和「表现层」唯一的交汇点

这是本文件最值得在答辩上讲的一点。

看项目的分层约定：**上层依赖下层，下层不知道上层存在**。

- `CharacterStats`（数据层）**不知道**伤害是怎么算的
- `HitReaction`（表现层）**不知道**自己为什么闪白

而 `Health` 同时知道两边：

- 它**读** `CharacterStats`（第 55 行）——这是它唯一一次碰数据层
- 它**广播** `Damaged` 事件（第 62 行）——让表现层自己来接

> **注意第 55 行是「读」，不是「被推」。**
> `Health` 主动去问 `CharacterStats`「你的防御是多少」，而不是 `CharacterStats` 把防御推给 `Health`。
> 这个方向很重要：它意味着**没有 `CharacterStats` 的物体也不会有任何问题**（第 55 行的 `_stats != null` 就是处理这种情况的）。
> 木桶不需要属性系统，它挂一个 `Health` 就能用。

---

## 四、代码全解

### 4.1 引用与类声明（第 1~11 行）

```csharp
1: using System;
2: using UnityEngine;
3: 
4: /// <summary>
5: /// 通用血量组件：玩家、敌人、可破坏物都能挂。
6: /// 无敌帧在这里统一处理，攻击方完全不需要关心。
7: ///
8: /// 如果同一个物体上有 CharacterStats，防御属性会在这里生效 ——
9: /// 这是「数值层」和「结算层」的唯一交汇点。
10: /// </summary>
11: public class Health : MonoBehaviour, IDamageable
12: {
```

| 行 | 代码 | 含义 |
|---|---|---|
| 1 | `using System;` | 引入 .NET 基础类库的 `System` 命名空间。**这一行的存在只为了一个东西：`Action`**（第 28、31 行用到）。`Action` 不是 Unity 的类型，是 .NET 的，在 `System` 里 |
| 2 | `using UnityEngine;` | 引入 Unity 的命名空间，为了 `MonoBehaviour`、`SerializeField`、`Mathf`、`Time`、`GetComponent` 等 |
| 3 | （空行） | 分隔引用与声明 |
| 4~10 | XML 文档注释 | 说清三件事：① 谁能挂（玩家/敌人/可破坏物）② 无敌帧在这里处理 ③ 它是「数值层」和「结算层」的交汇点。第 9 行的破折号 `——` 是本项目注释里常用的强调手法 |
| 11 | `public class Health : MonoBehaviour, IDamageable` | **本行是整份文档信息密度最高的一行。** 逐部分拆解：<br>• `public`——其他脚本能访问<br>• `class`——引用类型<br>• `Health`——类名<br>• `:`——C# 里「继承 / 实现」都用这个冒号<br>• `MonoBehaviour`——**基类**。继承它才能挂到 GameObject 上、才能用 `Awake`/`Update`、才能 `GetComponent`<br>• `,`——逗号分隔<br>• `IDamageable`——**接口**。实现它，`Health` 才「能挨打」<br>**C# 的规则是：最多一个基类（写在最前），后面可以跟任意多个接口。** |
| 12 | `{` | 类型体开始 |

> **为什么 `MonoBehaviour` 必须写在前面？**
> 因为 C# 的语法规定：**基类必须放在列表的第一位**。写成 `public class Health : IDamageable, MonoBehaviour` 会直接编译报错。
>
> **为什么不能继承两个类？**
> C# 是单继承语言。这也是 `IDamageable` 必须是接口而不能是抽象类的原因——`Health` 的位置已经被 `MonoBehaviour` 占了。详见 `05-IDamageable.md` 第 3.6 节。

### 4.2 Inspector 字段（第 13~18 行）

```csharp
13:     [Header("血量")]
14:     [SerializeField] private float maxHealth = 100f;
15: 
16:     [Header("无敌帧")]
17:     [Tooltip("受击后多久内免疫后续伤害，0 表示不免疫")]
18:     [SerializeField] private float invincibilityDuration = 0.5f;
```

| 行 | 代码 | 含义 |
|---|---|---|
| 13 | `[Header("血量")]` | **特性（Attribute）**。方括号语法在 C# 里表示「给下面那个成员附加元数据」。`Header` 是 Unity 提供的，作用是在 Inspector 里画一条**粗体分组标题**。它不改变任何运行逻辑，纯粹是排版。**注意它对「下面那一个字段」生效**，所以第 14 行属于「血量」组 |
| 14 | `[SerializeField] private float maxHealth = 100f;` | 三个部分：<br>• `[SerializeField]`——**让私有字段也能在 Inspector 里显示和编辑**。没有它，`private` 字段 Unity 会忽略<br>• `private float maxHealth`——私有字段，只有本类能改。**这正是我们要的**：外部想改上限必须走第 69 行的 `SetMaxHealth`<br>• `= 100f`——初始值。**如果在 Inspector 里改过，这个 100 就失效了**（Inspector 里存的值优先） |
| 15 | （空行） | 空行分组 |
| 16 | `[Header("无敌帧")]` | 第二组的标题 |
| 17 | `[Tooltip("受击后多久内免疫后续伤害，0 表示不免疫")]` | **鼠标悬停在这个字段上时弹出的说明文字。** 注意它解释了 `0` 这个特殊值的含义——这是配置约定，必须写下来 |
| 18 | `[SerializeField] private float invincibilityDuration = 0.5f;` | **无敌帧时长，默认半天半（0.5 秒）。** 这个值的意义见第 4.7 节的详解 |

> **`[SerializeField] private` 这个组合是本项目最常用的写法，值得单独记下来。**
>
> | 写法 | Inspector 可见 | 外部脚本可改 | 推荐度 |
> |---|---|---|---|
> | `public float x;` | ✅ | ✅ | ❌ 太开放 |
> | `[SerializeField] private float x;` | ✅ | ❌ | ✅ **推荐** |
> | `private float x;` | ❌ | ❌ | 仅内部用 |
>
> 好处是：**让你（在 Inspector 里）能调，让代码（别的脚本）不能乱改。** 数值配置用第一种能力，数据安全用第二种能力，两者不冲突。
>
> ⚠️ **一个要注意的副作用**：改成 `[SerializeField] private` 之后，**已经存在于场景里的物体不会自动更新初始值**。Unity 把序列化的值存在场景文件里，字段重命名或类型改变时它会保留旧值。遇到「我明明把默认值改成 200 了，Inspector 里还是 100」，就是这个原因——需要在 Inspector 里手动改，或者用组件右上角 `⋮` 菜单里的 `Reset`。

### 4.3 公开只读属性（第 20~25 行）

```csharp
20:     public float Max => maxHealth;
21:     public float Current { get; private set; }
22:     public bool IsAlive => Current > 0f;
23: 
24:     /// <summary>血量百分比 0~1，血条 UI 直接用它</summary>
25:     public float Normalized => maxHealth > 0f ? Current / maxHealth : 0f;
```

| 行 | 代码 | 含义 |
|---|---|---|
| 20 | `public float Max => maxHealth;` | **表达式体属性（expression-bodied property）**。`=>` 读作「返回」，整行等价于：<br>`public float Max { get { return maxHealth; } }`<br>它把私有的 `maxHealth` 字段**以只读方式暴露出去**。外部能读 `health.Max`，但**没有 setter，改不了** |
| 21 | `public float Current { get; private set; }` | **自动实现属性 + `private set`**。见下方详解 |
| 22 | `public bool IsAlive => Current > 0f;` | **`IsAlive` 是算出来的，不是存下来的。** 每次读它都现场比较一次 `Current` 和 `0`。所以它**永远不会和血量不同步**——这是「单一数据源」的体现。`0f` 后面的 `f` 是 C# 的 `float` 字面量后缀，不加的话 `0` 会被当成 `double`，赋值给 `float` 要隐式转换（虽然能编译，但不够严谨） |
| 24 | 文档注释 | 明确写出**用途**：「血条 UI 直接用它」。注释里点名使用者，是给未来自己的提示 |
| 25 | `public float Normalized => maxHealth > 0f ? Current / maxHealth : 0f;` | **血量百分比**，拆开看：<br>• `maxHealth > 0f`——条件<br>• `?`——三元运算符「如果」<br>• `Current / maxHealth`——条件为真时的结果，范围 0~1<br>• `:`——「否则」<br>• `0f`——条件为假时的结果<br>**为什么要判 `maxHealth > 0f`？** 因为除法里分母是 0 会得到 `NaN`（非数）或 `Infinity`。一个 `NaN` 传给 UI 的 `Image.fillAmount`，血条会变成完全不可预测的样子，而且 Console 不报错。**这是防御性编程**：即使 `maxHealth` 理论上不该是 0，也先挡一下 |

> #### 关于第 21 行 `{ get; private set; }` ——本文件最值得理解的一行
>
> 这是**自动实现属性（auto-implemented property）**。编译器在背后帮你生成了一个隐藏的字段（名字叫 `<Current>k__BackingField`），以及一个 getter 和一个 setter。
>
> 访问级别是这样分的：
>
> | 谁能做什么 | 语法 | 例子 |
> |---|---|---|
> | 任何脚本**能读** | `public` getter | `other.Current` ✅ |
> | **只有本类能写** | `private` setter | `Current = ...`（只有 `Health` 内部可以） |
> | 其他脚本**想写** | — | `other.Current = 50f;` ❌ **编译错误** |
>
> **这就是「封装」在 C# 里最具体的形态。**
>
> 它带来的实际好处：**`Current` 的值只可能由 3 个地方改变**——第 40 行的 `Awake`、第 59 行的 `TakeDamage`、第 75 行的 `SetMaxHealth`、第 81 行的 `ResetToFull`（4 个，全在本文件里）。
>
> 想知道「血量在什么情况下会变」，你 `Ctrl+F` 搜 `Current =` 就够了，**不用去别的 20 个脚本里找有没有人偷偷改过它**。
>
> 反过来看，如果写成 `public float Current;`（公开字段）：
> ```csharp
> // ❌ 任何脚本都能这么干，而且编译器不管
> someEnemy.Current = 0f;                    // 秒杀，无敌帧没走、Damaged 没触发、Died 没触发
> someEnemy.Current *= 2f;                   // 血量翻倍，但 Died 事件的状态机全乱了
> ```
> **这些 bug 不会报错，只会表现为「敌人有时会突然死」「血条显示不对」——极难定位。**
>
> 把 setter 设成 `private`，就从**语法上**杜绝了这一整类问题。
>
> > **注意一个细节**：`private set` 表示「只有 `Health` 类自己（包括它的所有实例）能写」。C# 里同一个类的不同实例之间可以互相访问私有成员，所以敌人 A 的代码理论上能改敌人 B 的 `Current`——但**没有脚本会这么写**，因为要拿到对方的具体类型引用，比直接调 `TakeDamage` 麻烦得多。

> #### 关于第 4 行的 `///` 与第 24 行的 `///`
> 你可能注意到第 20~22 行**没有**文档注释，第 24 行有。这不是疏漏：
> - `Max`、`Current`、`IsAlive` 的名字和代码本身已经足够自解释
> - `Normalized` 需要解释「0~1」这个范围和「给谁用」
>
> **注释应该补充代码没说的事，而不是复述代码说了的事。**

### 4.4 两个事件（第 27~31 行）

```csharp
27:     /// <summary>受到有效伤害时触发（被无敌帧挡掉的不触发）</summary>
28:     public event Action<DamageInfo> Damaged;
29: 
30:     /// <summary>血量归零时触发</summary>
31:     public event Action Died;
```

| 行 | 代码 | 含义 |
|---|---|---|
| 27 | 文档注释 | **括号里的那句是关键**：「被无敌帧挡掉的不触发」。这是订阅者必须知道的契约——否则 `HitReaction` 会以为「没收到事件 = 没被打」 |
| 28 | `public event Action<DamageInfo> Damaged;` | 逐部分拆解：<br>• `public`——任何脚本都能订阅<br>• `event`——**关键字，声明这是一个事件而不是普通委托字段**<br>• `Action<DamageInfo>`——**委托类型**。`Action<T>` 是 .NET 内置的泛型委托，表示「一个接受一个 `T` 参数、不返回值的方法」。所以 `Action<DamageInfo>` = 「接受一个 `DamageInfo` 参数、返回 `void` 的方法」<br>• `Damaged`——事件名，用**过去式**（已经发生了）<br>• `;`——**事件声明以分号结尾，没有花括号和实现** |
| 30 | 文档注释 | 「血量归零时触发」 |
| 31 | `public event Action Died;` | **无参版本**。`Action`（不带 `<>`）表示「不接受参数、不返回值的方法」。**为什么 `Died` 不需要参数？** 见下方 |

> #### `event` 关键字到底做了什么
>
> 如果不写 `event`，只写 `public Action<DamageInfo> Damaged;`，它就是一个**普通的委托字段**，任何脚本都能：
>
> ```csharp
> // ❌ 没有 event 关键字时，这些都能编译
> health.Damaged = null;                    // 把所有订阅者一次性清空！
> health.Damaged = MyMethod;                // 覆盖掉别人的订阅！
> health.Damaged.Invoke(info);              // 外部脚本冒充 Health 广播事件！
> ```
>
> 加上 `event` 之后，**从类外部只能做两件事**：
>
> ```csharp
> health.Damaged += OnDamaged;              // ✅ 订阅
> health.Damaged -= OnDamaged;              // ✅ 退订
> ```
>
> `=`、`Invoke()`、`()` 调用在类外部**全部变成编译错误**。
>
> **这就是 `event` 的全部意义：把「广播的权力」锁在 `Health` 内部，只把「订阅的权力」发出去。**
>
> 类比一下：`event` 像「只能订阅、不能自己发推送的公众号」——读者能关注和取关，但没法冒充公众号发消息。

> #### 为什么 `Damaged` 带参数，`Died` 不带
>
> | 事件 | 参数 | 理由 |
> |---|---|---|
> | `Damaged` | `DamageInfo info` | 订阅者**必须**知道：掉了多少（做飘字）、从哪来（做击退）、是不是暴击（做颜色）。没有这些信息，`HitReaction` 就做不了击退 |
> | `Died` | 无 | 订阅者**不需要**知道「怎么死的」。`EnemyController` 只管播放死亡动画并销毁；`RunManager` 只管重开场景。**死因不是它们的业务** |
>
> **事件参数应该只带「订阅者真正需要的信息」。** 带得太多，等于把内部实现细节泄露给订阅者，将来想改都改不动。
> 如果将来真有人需要「死因」（比如「被火杀死 vs 被剑杀死」要做不同掉落），那时候再加一个 `event Action<DamageInfo> Died`（注意：那样就不能和现在这个同名了，得改名或替换）。

> #### `Action<T>` 和自定义委托的取舍
>
> 你也可以定义一个专门的委托类型：
> ```csharp
> public delegate void DamageHandler(DamageInfo info);
> public event DamageHandler Damaged;
> ```
> 效果完全一样。**用 `Action<T>` 的理由是：不用为每种签名单独定义一个类型。** .NET 已经内置了 `Action`（0 参）、`Action<T>`（1 参）、`Action<T1,T2>`（2 参）……一直到 16 个参数，够用了。
>
> 只有当签名特别长、或者你想给它一个有意义的名字时，才值得自定义委托。

### 4.5 私有字段（第 33~34 行）

```csharp
33:     private CharacterStats _stats;
34:     private float _invincibilityTimer;
```

| 行 | 代码 | 含义 |
|---|---|---|
| 33 | `private CharacterStats _stats;` | **对同物体上 `CharacterStats` 组件的缓存引用。** 下划线前缀 `_` 是本项目的私有字段命名约定（Unity 社区最通用的写法），一眼能区分「字段」和「局部变量」。**注意它没有 `[SerializeField]`**——因为它不需要在 Inspector 里配，它是由 `Awake` 自动找的 |
| 34 | `private float _invincibilityTimer;` | **无敌帧倒计时**，单位秒。`> 0` 表示还在无敌中。**它没有初始值**，所以默认是 `0f`——也就是「开局不是无敌状态」，这是正确的默认 |

> **为什么 `_stats` 要缓存，而不是每次 `TakeDamage` 时 `GetComponent`？**
>
> `GetComponent` 不是免费的——它要在 GameObject 的组件列表里查找。`TakeDamage` 可能每帧被调用好几次（多个敌人同时打你），每次查一遍是浪费。
>
> **在 `Awake` 里查一次，存进字段，之后直接用**——这是 Unity 里最基础也最重要的性能习惯。

### 4.6 生命周期：`Awake` 与 `Update`（第 36~47 行）

```csharp
36:     private void Awake()
37:     {
38:         _stats = GetComponent<CharacterStats>();
39: 
40:         Current = maxHealth;
41:     }
42: 
43:     private void Update()
44:     {
45:         if (_invincibilityTimer > 0f)
46:             _invincibilityTimer -= Time.deltaTime;
47:     }
```

| 行 | 代码 | 含义 |
|---|---|---|
| 36 | `private void Awake()` | **Unity 的生命周期方法**。在一个对象被创建时调用一次（场景加载时、或 `Instantiate` 时），**早于 `Start`**。用 `private` 是因为**Unity 是通过反射调用它的，不需要它是 `public`**——写成 `public void Awake()` 也能工作，但没必要扩大可见性 |
| 38 | `_stats = GetComponent<CharacterStats>();` | 在同一个 GameObject 上找 `CharacterStats` 组件。**找不到时返回 `null`，不报错**——这是完全正常的情况（木桶就没有），所以第 55 行必须处理 `null` |
| 39 | （空行） | 把「找组件」和「设初始值」分开，可读性更好 |
| 40 | `Current = maxHealth;` | **开局满血。** 这里是 `Current` 的 setter 在本类内被使用（所以能通过编译）。**为什么放在 `Awake` 而不是直接在字段声明处写 `= maxHealth`？** 因为字段初始化器的执行顺序依赖于声明顺序，容易出错；而且 `Current` 是带 `private set` 的属性，不能在字段初始化器里赋值。放在 `Awake` 里，时机明确、只有一次 |
| 43 | `private void Update()` | **每帧调用一次**（不是固定间隔，帧率越高调用越频繁） |
| 45~46 | `if (_invincibilityTimer > 0f) _invincibilityTimer -= Time.deltaTime;` | 倒计时。两个部分：<br>• `if (_invincibilityTimer > 0f)`——**只在还大于 0 时递减**，避免它一直减到负数（那样虽然逻辑上也没错，但会变成一个越来越小的数字，调试时看着奇怪）<br>• `_invincibilityTimer -= Time.deltaTime;`——`Time.deltaTime` 是「上一帧到这一帧经过的秒数」。**用它递减才和帧率无关**：60fps 时每帧减 0.0167，30fps 时每帧减 0.0333，两种情况下 0.5 秒都是 0.5 秒。如果写 `-= 0.016f`，高帧率下无敌时间就会变短 |
| 46 | （没有花括号） | C# 允许 `if` 后面只有一条语句时省略花括号。本项目在**单语句且短**的情况下这么写；一旦要加第二行，**必须补上花括号**（否则第二行会无条件执行，是经典 bug） |

> #### 为什么用 `Update` 而不是协程（`Coroutine`）
>
> 用协程也能实现：
> ```csharp
> // 另一种写法
> private void TakeDamage(DamageInfo info)
> {
>     // ...
>     _invincibilityTimer = invincibilityDuration;
>     StartCoroutine(ResetInvincibility(invincibilityDuration));
> }
>
> private IEnumerator ResetInvincibility(float duration)
> {
>     yield return new WaitForSeconds(duration);
>     _invincibilityTimer = 0f;
> }
> ```
>
> 协程版本有三个问题：
>
> 1. **重复启动**：如果 `TakeDamage` 在无敌帧期间被调用（理论上被第 52 行挡住了，但万一以后改动破坏了这个假设），会启动第二个协程，两个协程互相覆盖，无敌时间变得不可预测
> 2. **`WaitForSeconds` 受 `Time.timeScale` 影响**：游戏暂停（`timeScale = 0`）时它不会走。而 `Time.deltaTime` 同样会变 0——两者在这个问题上等价，但协程更难察觉
> 3. **必须记着 `StopCoroutine`**：物体被销毁时协程自动停止，不算问题；但如果想在 `ResetToFull` 时立刻取消无敌，协程版本要额外记住 `Coroutine` 句柄
>
> **`Update` 里递减不存在这些问题**：`_invincibilityTimer` 只是一个数字，赋新值就自然重置了，没有「上一次的定时器」这种残留状态。**能用简单状态表达的东西，就不要引入协程。**

### 4.7 `TakeDamage`——本文件的核心（第 49~66 行）

```csharp
49:     public void TakeDamage(DamageInfo info)
50:     {
51:         if (!IsAlive) return;                  // 已经死了，不重复结算
52:         if (_invincibilityTimer > 0f) return;  // 处于无敌帧
53: 
54:         // 防御按比例减伤，上限 80% —— 不设上限的话高防御角色会完全免疫
55:         float defense   = _stats != null ? _stats.Get(StatType.Defense) : 0f;
56:         float reduction = Mathf.Clamp(defense, 0f, 0.8f);
57:         float amount    = info.Amount * (1f - reduction);
58: 
59:         Current = Mathf.Max(0f, Current - amount);   // 防止血量变负
60:         _invincibilityTimer = invincibilityDuration;
61: 
62:         Damaged?.Invoke(info);
63: 
64:         if (Current <= 0f)
65:             Died?.Invoke();
66:     }
```

| 行 | 代码 | 含义 |
|---|---|---|
| 49 | `public void TakeDamage(DamageInfo info)` | **实现 `IDamageable` 接口的唯一方法。** `public` 是必须的——接口成员默认是公开的契约，实现时**不能用更低的可见性**（写成 `private` 会编译报错）。返回 `void`，和接口签名一致 |
| 51 | `if (!IsAlive) return;` | **第一道闸：已经死了就不重复结算。** `!` 是逻辑非。拆开读：`IsAlive` 会现场算 `Current > 0f`；如果已经不活了，直接 `return`（退出方法，什么都不做）<br>**为什么必须有？** 死掉的敌人在 `deathDelay`（0.3 秒）之后才被 `Destroy`。这 0.3 秒里如果又被打中，会再触发一次 `Damaged`（闪白、击退）和一次 `Died`——`Died` 被触发两次，`RunManager` 就会排队两次重开场景 |
| 52 | `if (_invincibilityTimer > 0f) return;` | **第二道闸：无敌帧。** 见下方详解 |
| 54 | 注释 | 记录了**设计理由**：「不设上限的话高防御角色会完全免疫」。这是给未来的自己一个警告 |
| 55 | `float defense = _stats != null ? _stats.Get(StatType.Defense) : 0f;` | **本文件里唯一一次访问数据层。** 拆开：<br>• `_stats != null`——**必须在前面判空**。木桶、可破坏的墙这些没挂 `CharacterStats` 的物体，`_stats` 就是 `null`<br>• `?`——三元「如果」<br>• `_stats.Get(StatType.Defense)`——向属性系统查询「防御」这个属性的最终值（基础值 + 所有装备/职业修饰符，见 `02-CharacterStats.md`）<br>• `:`——否则<br>• `0f`——**没挂 `CharacterStats` 的物体防御为 0**，也就是「照单全收」<br>**如果漏了判空会怎样？** `NullReferenceException`，而且只在木桶上出现——玩家和敌人都好好的。这种「只在特定物体上崩溃」的 bug 最难查 |
| 56 | `float reduction = Mathf.Clamp(defense, 0f, 0.8f);` | **`Mathf.Clamp(值, 最小, 最大)` 把值夹在区间内。** 三种情况：<br>• `defense = 0.1` → 还是 `0.1`<br>• `defense = -0.5`（被诅咒了）→ 夹成 `0`（**防止负防御变成「加血」**）<br>• `defense = 1.5`（填错了 / 装备叠爆了）→ 夹成 `0.8`<br>**0.8 这个上限的意义**：见下方「关键决策」 |
| 57 | `float amount = info.Amount * (1f - reduction);` | **最终伤害 = 攻击方给的数值 × (1 - 减伤比例)。** `1f - 0.1 = 0.9`，所以防御 0.1 表示「受到 90% 的伤害」。<br>**为什么用乘法而不是减法？** 乘法让「防御」变成一个**相对比例**，无论敌人攻击是 8 还是 800 都减 10%；如果用减法（`amount = info.Amount - defense`），低攻击的敌人会被完全免疫，而且数值需要随游戏进度膨胀。**这是 RPG 里最常见的减伤公式** |
| 59 | `Current = Mathf.Max(0f, Current - amount);` | **扣血，且不允许为负。** `Mathf.Max(0f, ...)` 保证结果不小于 0。**为什么不能直接 `Current -= amount`？** 见下方「关键决策」 |
| 60 | `_invincibilityTimer = invincibilityDuration;` | **扣完血才启动无敌帧。** 顺序很重要：如果放在第 52 行之前，这次伤害自己就会被挡掉 |
| 62 | `Damaged?.Invoke(info);` | **广播「我受伤了」。** 拆开：<br>• `Damaged?`——**空条件运算符（null-conditional operator）**。如果 `Damaged` 是 `null`（**没有任何人订阅**），整个表达式返回 `null` 并且**不执行后面的 `.Invoke`**<br>• `.Invoke(info)`——调用所有订阅者的方法，把 `info` 传给他们<br>**为什么必须写 `?`？** 如果不写：`Damaged.Invoke(info);` 在没人订阅时会抛 `NullReferenceException`。而「没人订阅」是完全正常的情况——比如一个纯木桶上没挂 `HitReaction`。<br>**这一行是整份文件里最容易漏掉 `?` 的地方**，而且它是**静默的失败**：只要有一个物体没人订阅就直接崩 |
| 64~65 | `if (Current <= 0f) Died?.Invoke();` | **死亡判定。** 注意两点：<br>① 用的是 `<=`，因为第 59 行已经把值下限到 0，所以等价于 `== 0`；写 `<=` 更保险<br>② `Died` **放在 `Damaged` 之后**。所以「致命一击」的事件顺序是：先 `Damaged`（闪白、击退），再 `Died`（销毁）。如果反过来，`EnemyController` 可能已经在 `Died` 里把物体销毁了，`HitReaction` 就再也收不到 `Damaged` |
| 65 | （缩进） | 这一行属于 `if`，但没写花括号（单语句省略）。**注意它和第 64 行是同一个 `if` 的两个部分**，缩进表达了从属关系 |

#### 关键决策一：无敌帧为什么必须在最前面挡掉

第 52 行放在**所有计算之前**，这个位置是有讲究的。

**无敌帧要解决什么问题？**

看一个真实场景：玩家挥剑，`MeleeAttacker` 用 `OverlapBoxAll` 找目标。这个判定框**不是只检测一次**——`Update` 每帧都可能检查一次（只要攻击键还按着、冷却已经好了）。

更典型的是**持续伤害**：站在地刺上，`DamageZone.OnTriggerStay2D` **每帧都会被调用**（`Stay` 的含义就是「还待在里面」）。

如果没有无敌帧，站在地刺上 1 秒钟（60 帧），你会被扣 60 次血。

**无敌帧的作用**：受击后锁定 0.5 秒，这期间所有后续伤害全部无效。于是地刺的伤害就被限制成「每 0.5 秒最多一次」。

**为什么必须写在最前面？**

因为如果把它放在第 59 行之前、但第 55 行之后，那么每次被挡掉的伤害**仍然会执行第 55~57 行的属性查询和计算**——白算一遍。放在最前面是「尽早返回（early return）」的写法，也最不容易看错。

> **「尽早返回」是一个通用的代码风格**：把「不满足条件就退出」的判断全部堆在方法开头，中间是正常逻辑。好处是**不需要嵌套 `if`**——否则整个方法体会被包在一层层花括号里，缩进越来越深。

#### 关键决策二：0.5 秒和 `DamageZone.tickInterval = 0.6f` 的关系 ⭐

这是本项目里一个**必须记住的配置耦合**。

**背景**：`DamageZone`（地刺、岩浆）用 `tickInterval` 控制扣血节奏，当前值 **0.6 秒**。

假设把 `tickInterval` 设成 **0.4 秒**（比无敌帧 0.5 秒短），会发生什么：

```
t = 0.0s   地刺 tick → 扣血，无敌帧启动到 0.5s   ✅ 生效
t = 0.4s   地刺 tick → 还在无敌帧内，被第 52 行挡掉   ❌ 静默吞掉
t = 0.8s   地刺 tick → 无敌帧已过（0.5 < 0.8）→ 扣血，无敌帧启动到 1.3s   ✅ 生效
t = 1.2s   地刺 tick → 还在无敌帧内（1.2 < 1.3）   ❌ 静默吞掉
t = 1.6s   地刺 tick → 生效   ✅
t = 2.0s   地刺 tick → 被挡   ❌
```

**结果：你配置的是「每 0.4 秒扣一次」，实际变成「每 0.8 秒扣一次」——一半的 tick 被静默吃掉。**

注意「静默」这两个字：被挡掉时**没有任何报错、任何日志**。你在 Editor 里看着地刺每 0.4 秒闪一下，玩家却每 0.8 秒才掉一次血。**这种不一致极难发现**，因为两边看起来都在正常工作。

**现在的配置为什么是对的**：

| 值 | 数值 | 关系 |
|---|---|---|
| `Health.invincibilityDuration` | 0.5s | 无敌窗口 |
| `DamageZone.tickInterval` | **0.6s** | **必须 > 0.5s** |

因为 `0.6 > 0.5`，每一次 tick 发生时，上一次的无敌帧都已经过期了，**所有伤害都能稳定生效**。

> **这条规则的通用形式**：
> **持续伤害的 tick 间隔，必须严格大于目标的无敌帧时长。**
>
> **如果将来要改其中一个，必须同时检查另一个。** 比如想「让地刺更痛」而把 `tickInterval` 从 0.6 改成 0.3——那你就得同时把玩家的 `invincibilityDuration` 改成比 0.3 更小（比如 0.25），否则伤害不会变痛，反而会变得**更不规律**。
>
> 更好的做法是**改伤害数值**而不是 tick 间隔：把 `DamageInfo.Amount` 翻倍，节奏不变，伤害翻倍。这样不会碰到无敌帧的耦合。

#### 关键决策三：防御上限为什么必须是 0.8

第 56 行的 `Mathf.Clamp(defense, 0f, 0.8f)`，**如果去掉上限会怎样？**

假设某件装备让 `defense` 加起来到了 `1.0`：

```
reduction = 1.0
amount = info.Amount × (1 - 1.0) = info.Amount × 0 = 0
```

**任何攻击都造成 0 伤害。** 游戏瞬间失去全部挑战性——你可以站着让 Boss 打十分钟。

更糟的是 `defense > 1.0` 的情况（比如 1.5）：

```
amount = info.Amount × (1 - 1.5) = info.Amount × (-0.5)
```

**伤害变成负数**——第 59 行的 `Mathf.Max(0f, Current - (-5))` 会把它变成 `Current + 5`，也就是**攻击敌人反而给你回血**。（`Mathf.Max` 在这里救了你，但它也把问题藏起来了。）

所以上限 0.8 的意思是：**无论怎么堆防御，至少承受 20% 的伤害。**

| defense 填的值 | Clamp 之后 | 实际承受 |
|---|---|---|
| 0 | 0 | 100% |
| 0.1 | 0.1 | 90% |
| 0.5 | 0.5 | 50% |
| 0.8 | 0.8 | 20% |
| **1.0** | **0.8** | **20%**（被夹住） |
| **2.0** | **0.8** | **20%**（被夹住） |

**具体算例**（用项目里真实的数值）：

| 攻击方 | 攻击力 | 受击方 | defense | 实际掉血 |
|---|---|---|---|---|
| 敌人 | 8 | **战士** | 0.1 | `8 × (1 - 0.1) = ` **7.2** |
| 敌人 | 8 | **法师** | 0 | `8 × (1 - 0) = ` **8**（掉满） |
| 玩家（战士） | 13 | 敌人 | 0（敌人没配防御） | `13 × 1 = ` **13** |

> 于是战士的 140 血可以扛 `140 ÷ 7.2 ≈ 19` 下，法师的 85 血只能扛 `85 ÷ 8 ≈ 10.6` 下。
> **这是「战士肉、法师脆」在代码层面的实现方式——只是一个 Clamp 和一个乘法。**

#### 关键决策四：血量为什么不能变负

第 59 行的 `Mathf.Max(0f, ...)`。

如果不加，`Current` 可以变成 `-37`。后果：

| 用到 `Current` 的地方 | 变负后的错误表现 |
|---|---|
| `IsAlive => Current > 0f` | 仍然返回 `false`，✅ 看不出问题 |
| `Normalized => Current / maxHealth` | 返回 `-0.26`。传给血条 UI 的 `Image.fillAmount`——**血条会从左边反向画出一截** |
| 第 64 行 `Current <= 0f` | 仍然为真，✅ 看不出问题 |
| 你自己的调试日志 | 打出一堆负数，让人怀疑是 bug |
| `SetMaxHealth(newMax, keepRatio: true)` | **比例会算错**：`-37 / 100 = -0.37`，切职业后新血量变成负数 |

**最后一行是最危险的**：`keepRatio` 的计算依赖 `Current / maxHealth` 是个合理的 0~1 的值。一旦是负数，切职业会直接把角色变成负血状态。

**这就是「防御性编程」的价值：在数据产生的地方保证它合法，而不是在每个使用的地方加检查。** 第 59 行的 `Mathf.Max` 只写了一次，却保护了 5 个使用点。

#### 关键决策五：`_stats` 为 `null` 是完全正常的

第 55 行的 `_stats != null ? ... : 0f`，不是「以防万一」的保险，而是**必须**处理的核心情况。

| 物体 | 有 `CharacterStats` 吗 | 需求 |
|---|---|---|
| 玩家 | ✅ 有（提供职业属性） | 防御来自 `ClassData` |
| 敌人 | ✅ 有（提供攻击力、移速） | 防御可以手填，也可以不填 |
| **木桶** | ❌ **没有** | 它不需要攻击力、移速、暴击率——挂了反而是噪音 |
| **可破坏的墙** | ❌ **没有** | 同上 |
| **训练假人** | ❌ **没有** | 同上 |

**木桶挂 `CharacterStats` 是没意义的**：它不移动（不需要 `MoveSpeed`）、不攻击（不需要 `Attack`）、不掉装备（不需要 `CritChance`）。为了一个「防御力」去挂一个装满 8 个用不上的属性的组件，是设计上的浪费。

所以 `Health` 必须优雅地处理「没有属性系统」的情况——`0f` 就是「不减伤」。

> **这也是「组合优于继承」的一个体现**：`Health` **不要求**同物体上有 `CharacterStats`，它只是「如果有就用上」。
> 这种「可选依赖」的模式在 Unity 里非常常见，写法就是：`Awake` 里 `GetComponent`（找不到是 `null`）+ 使用处判空。

#### 关键决策六：两个事件的语义差别

这是**必须写进文档**的一条契约，因为订阅者会依赖它：

| | `Damaged` | `Died` |
|---|---|---|
| 触发时机 | 第 62 行，扣血之后 | 第 65 行，血量为 0 时 |
| 被无敌帧挡掉时 | **❌ 不触发** | ❌ 不触发 |
| 被 `!IsAlive` 挡掉时 | **❌ 不触发** | ❌ 不触发 |
| 一次攻击可能触发几次 | 最多 1 次 | **最多 1 次**（因为 `IsAlive` 变成了 `false`，后续伤害全部在第 51 行退出） |
| 参数 | `DamageInfo`（伤害的全部信息） | 无 |
| 典型订阅者 | `HitReaction`、将来的伤害飘字 | `EnemyController`、`RunManager` |
| 触发顺序 | **先** | **后** |

> ⚠️ **`Died` 只会触发一次，这是有保证的——但保证来自第 51 行。**
> 如果有人把第 51 行的 `if (!IsAlive) return;` 删掉，`Died` 就会每次受击都触发一次，`RunManager` 会排队重开好几次场景。**改这段代码时要记得这两个判断是互相依赖的。**

> ⚠️ **`Damaged` 不触发 ≠ 没打中。**
> `HitReaction` 的闪白完全依赖 `Damaged`。所以**如果这次攻击被无敌帧挡掉了，敌人不会闪白**——这是正确的表现（无敌帧期间本来就不该有受击反馈），但如果你以后做「命中特效」，要知道它也会一起消失。

### 4.8 `SetMaxHealth`（第 68~76 行）

```csharp
68:     /// <summary>设置最大生命值，可选保持血量百分比 —— 职业 / 装备改变上限时调用</summary>
69:     public void SetMaxHealth(float newMax, bool keepRatio = true)
70:     {
71:         if (newMax <= 0f) return;
72: 
73:         float ratio = maxHealth > 0f ? Current / maxHealth : 1f;
74:         maxHealth = newMax;
75:         Current = keepRatio ? maxHealth * ratio : Mathf.Min(Current, maxHealth);
76:     }
```

| 行 | 代码 | 含义 |
|---|---|---|
| 68 | 文档注释 | 点明**调用时机**：「职业 / 装备改变上限时」。这句话让读者知道这个方法不是给自己随便玩的 |
| 69 | `public void SetMaxHealth(float newMax, bool keepRatio = true)` | **`bool keepRatio = true` 是默认参数（optional parameter）。** 意思是：调用时可以只传一个参数 `SetMaxHealth(140f)`，此时 `keepRatio` 自动等于 `true`。也可以显式传 `SetMaxHealth(140f, false)` 覆盖它。<br>**默认参数的好处**：90% 的调用场景想保持血量比例，所以默认 `true`；剩下 10% 想强制回满或截断时再显式写 `false`。**这让常见情况写起来最短，特殊情况也不丢失表达能力** |
| 71 | `if (newMax <= 0f) return;` | **拒绝非法输入。** 如果允许 `maxHealth = 0`，那么：<br>① 第 25 行的 `Normalized` 会走到 `0f` 分支，血条永远显示空<br>② 第 73 行的 `ratio` 会走到 `1f` 分支<br>③ 角色瞬间变成「一击必死」（`Current = 0`）<br>**一个 0 上限的血量组件是个逻辑陷阱**，不如直接拒绝——静默 `return`（不报错、不修改）。<br>⚠️ 注意这是**静默失败**：调用方不会知道自己的请求被拒绝了。如果将来因为配置错误传了 0，你会看到「换职业后血量没变」，但没有任何提示 |
| 73 | `float ratio = maxHealth > 0f ? Current / maxHealth : 1f;` | **先记录「当前血量占上限的百分比」**。这一步**必须在第 74 行改掉 `maxHealth` 之前做**——一旦 `maxHealth` 变了，旧比例就再也算不出来了。<br>**这是整个方法里顺序最关键的一行。**<br>• `Current / maxHealth`——比如 70/100 = `0.7`<br>• 三元里的 `: 1f`——如果旧上限是 0（理论上第 71 行挡住了，但第一次调用前 `maxHealth` 可能有非预期值），按 1 处理，也就是「当作满血」 |
| 74 | `maxHealth = newMax;` | 更新上限 |
| 75 | `Current = keepRatio ? maxHealth * ratio : Mathf.Min(Current, maxHealth);` | **按 `keepRatio` 分两种情况：**<br>• **`true`（默认）**：`maxHealth * ratio` = `140 × 0.7 = 98`。也就是「**血量百分比不变**」——70% 血切职业后还是 70% 血<br>• **`false`**：`Mathf.Min(Current, maxHealth)`——**保留当前血量的绝对值，但不允许超过新上限**。比如 70 血切到法师（上限 85），仍然是 70 血（`min(70, 85) = 70`）；但如果是 120 血切到 85 上限，会变成 85（`min(120, 85) = 85`）<br>**`Mathf.Min` 那一半是必须的**：没有它，120 血的战士切到 85 血的法师，会变成「85 上限、120 当前」——血条超过 100% 画到框外面去 |

> #### 这个方法在职业切换里怎么用
>
> 玩家在 Inspector 里把 `CharacterStats` 的 `Class Data` 从 `Class_Warrior` 换成 `Class_Mage` 时，`CharacterStats` 会算出新的 `MaxHealth`（140 → 85），然后调用：
>
> ```csharp
> _health.SetMaxHealth(stats.Get(StatType.MaxHealth));    // keepRatio 用默认的 true
> ```
>
> **在战斗中切职业的表现**：假设现在是战士 140 上限、70 血（50%）。切到法师：
> ```
> ratio = 70 / 140 = 0.5
> maxHealth = 85
> Current = 85 × 0.5 = 42.5
> ```
> **42.5 血，仍然是 50%。** 这就是 `keepRatio: true` 的语义——**切职业不会让你突然满血或突然暴毙**。
>
> `keepRatio: false` 用在哪？用在**开局初始化**或**死亡重生**这类「不需要保留战损」的场合，配合 `ResetToFull` 一起用。
>
> > ⚠️ **时序陷阱**：这个方法在 `CharacterStats.Start()` 里被调用，而不是 `Awake()`——理由见第六节。

### 4.9 `ResetToFull`（第 78~84 行）

```csharp
78:     /// <summary>回满血 / 重置</summary>
79:     public void ResetToFull()
80:     {
81:         Current = maxHealth;
82:         _invincibilityTimer = 0f;
83:     }
84: }
```

| 行 | 代码 | 含义 |
|---|---|---|
| 78 | 文档注释 | 「回满血 / 重置」——两个斜杠分隔的两个用途，都是这个方法支持的场景 |
| 79 | `public void ResetToFull()` | 无参、无返回值。**语义很纯：把血量恢复到初始状态** |
| 81 | `Current = maxHealth;` | 直接赋成上限值。注意这里**没有再走 `SetMaxHealth`**，因为上限本身没变 |
| 82 | `_invincibilityTimer = 0f;` | **这一行容易被忽略，但很重要。** 如果重生时无敌帧还在倒数（比如死前刚被打过），复活后会有最多 0.5 秒的无敌——在某些情况下这反而是好事，但在「重开一局」的语境下应该是干净的状态。<br>**把它清零 = 明确地重置到干净状态**，不依赖「反正是新场景」这种隐含假设 |
| 84 | `}` | 类结束。**注意这个花括号在第 84 行，而第 12 行的类开始花括号还没有对应的闭合——它们是一对** |

> **`ResetToFull` 现在被谁调用？**
> 主要是 `RunManager` 在重开一局后（场景重新加载，`Awake` 会重新跑，所以其实不需要显式调用）。
> 它也预留给「回血泉水」「复活道具」这类 Day 5 之后的内容。

---

## 五、在 Unity 里怎么配

### 5.1 挂到哪些物体上

| 物体 | 挂 `Health` | 同时挂 `CharacterStats` | `Max Health` 填什么 |
|---|---|---|---|
| **Player** | ✅ | ✅ 必须（提供职业属性） | 随便填（100 就行），**会被 `ClassData` 覆盖** |
| **Enemy** | ✅ | ✅ 必须（提供攻击力、移速） | 随便填，**会被 `CharacterStats` 的基础值覆盖** |
| **木桶 / 可破坏物** | ✅ | ❌ 不要挂 | ⚠️ **这里的值就是真值**，填 30 就是 30 血 |

> **「会被覆盖」这件事必须理解清楚**，否则你会遇到「我明明填了 200，怎么还是 140」的困惑。
>
> 覆盖发生在 `CharacterStats.Start()`：
> ```csharp
> // CharacterStats.cs 里
> _health.SetMaxHealth(Get(StatType.MaxHealth));   // 用属性系统算出来的值覆盖 Inspector 里填的
> ```
>
> 所以**玩家和敌人的 `Max Health` 字段是一个「占位值」**——它在 `Awake` 到 `Start` 之间的极短时间内是生效的，然后立刻被替换。填什么都不影响最终结果。
>
> 但**木桶没有 `CharacterStats`，所以没人来覆盖它**——Inspector 里填多少就是多少。这是唯一需要认真对待这个字段的场合。

### 5.2 两个字段该填什么

| 字段 | 玩家 | 敌人 | 木桶 | 说明 |
|---|---|---|---|---|
| **Max Health** | 100（占位） | 100（占位） | **30（真值）** | 见上表 |
| **Invincibility Duration** | `0.5` | `0.5` | **`0`** | 见下 |

**关于 `Invincibility Duration`**：

| 值 | 效果 | 什么时候用 |
|---|---|---|
| `0.5` | 受击后 0.5 秒内免疫后续伤害 | 玩家、普通敌人（默认） |
| `0` | **完全不免疫**，每次伤害都生效 | 可破坏物（木桶不该有「无敌 0.5 秒」的设定，否则砍起来一顿一顿的） |
| `0.1` | 只防住「同一帧内的多次判定」 | 想要「高攻速也能全额生效」的敌人 |

> ⚠️ **如果给敌人填 0.5，要注意一个副作用**：玩家的攻速如果快过无敌帧，多出来的攻击会被完全吞掉。
> 战士的 `AttackSpeed` 是 0.95，基础冷却 `0.35 ÷ 0.95 ≈ 0.37 秒`——**比 0.5 秒的无敌帧短**。
> 也就是说战士连续攻击同一个敌人时，**大约每两次攻击只有一次能造成伤害**。
>
> 这不一定是 bug（它让「高频攻击」在单体上收益打折，是一种隐性的平衡），但**你要知道自己配置出了这个效果**。想让每一刀都生效，就把敌人的 `Invincibility Duration` 调成 `0` 或 `0.3`。
>
> **Day 4 追加的第一个敌人目前用的是默认值 0.5**——如果你觉得打起来「有时不掉血」，先检查这个字段。

### 5.3 同物体上还需要什么（不是本脚本要求的，但相关）

`Health` 自己只需要自己。但**同物体上通常还有**：

| 组件 | 谁需要它 | 缺了会怎样 |
|---|---|---|
| `SpriteRenderer` | `HitReaction`（闪白） | 闪白静默跳过，其他正常 |
| `Rigidbody2D` | `HitReaction`（击退） | 击退静默跳过，硬直仍然生效 |
| `Collider2D` | 攻击方（`OverlapBoxAll` 要找到它） | ⚠️ **完全打不到**，而且没有任何报错 |

> **最后一行是 Day 1 真实踩过的坑**：三块地面只挂了 `SpriteRenderer`，没挂 `BoxCollider2D`，角色直接穿过去。
> **`SpriteRenderer` 决定「看得见」，`Collider2D` 决定「撞得到」——两者没有任何关系，Unity 不会因为你挂了前者就自动补上后者。**
>
> 详细记录见 `D:\Roguelike2D\docs\05-开发日志.md`。

### 5.4 一个完整的配置检查清单

给一个新的可受伤物体做配置时，按顺序检查：

```
1. Layer 设对了吗？（Player / Enemy / 可破坏物自己的层）
2. 有 Collider2D 吗？            ← 没有就完全打不到
3. 有 Health 吗？                ← 没有就不是 IDamageable
4. Max Health 填了吗？
5. Invincibility Duration 填了吗？（想要「每刀都生效」就填 0 或很小的值）
6. 想有闪白/击退 → 加上 HitReaction
7. 想有闪白 → 确认有 SpriteRenderer
8. 想有击退 → 确认有 Rigidbody2D
9. 攻击方的 AttackData.targetLayers 勾了这一层吗？   ← 最容易忘的一步
```

---

## 六、踩过的坑

### 6.1 `CharacterStats` 必须在 `Start()` 而不是 `Awake()` 里调用 `SetMaxHealth` ⭐

这是本项目里和时序（execution order）相关的最典型的一个坑。

> **现象**：加了职业系统之后，一切换职业（在 Inspector 里改 `Class Data`）角色就以 **0 血**开局——一进游戏就死，或者血条是空的。
>
> **根因**：`CharacterStats` 一开始把「同步最大生命值给 `Health`」这件事写在了自己的 `Awake()` 里。
>
> 而 `SetMaxHealth` 内部（第 73 行）要算一个比例：
> ```csharp
> float ratio = maxHealth > 0f ? Current / maxHealth : 1f;
> ```
> 它依赖 `Current` 已经被初始化过。
>
> 但 `Health.Awake()`（第 40 行）才负责 `Current = maxHealth`。**如果 `CharacterStats.Awake()` 先执行，此时 `Current` 还是 `0f`**，于是：
> ```
> ratio = 0 / 100 = 0
> maxHealth = 140
> Current = 140 × 0 = 0        ← 0 血开局
> ```
>
> **为什么两个 `Awake` 的顺序不确定？** Unity **不保证**同一个 GameObject 上多个组件之间的 `Awake` 执行顺序（它按组件的添加顺序，但那个顺序在 Inspector 里不可见、也可能因为重新挂载而变化）。
>
> **解法**：把「同步最大生命值」搬到 `CharacterStats.Start()`。
>
> **Unity 的生命周期保证是**：
>
> ```
> 所有组件的 Awake()   ← 顺序不定
>         ↓
> 所有组件的 OnEnable() （顺序不定）
>         ↓
> 所有组件的 Start()   ← 顺序不定，但一定在所有 Awake 之后
> ```
>
> **所以「需要读别的组件状态」的初始化，一律放在 `Start()` 里。** 这样能保证所有 `Awake`（包括 `Health` 的第 40 行）都已经跑完。
>
> **一句话总结这条规矩**：
> > **`Awake` 里只初始化「自己」；需要用到别人状态的事情，放 `Start`。**

**这个坑的一般化教训**：Unity 里凡是「A 组件的初始化依赖 B 组件已初始化」的情况，都在 `Start` 里做。玩家和敌人都会遇到，木桶不会（它没有 `CharacterStats`，所以从来不会触发这个问题——这也是为什么它只在你加职业系统之后才暴露出来）。

### 6.2 无敌帧和持续伤害 tick 的间隔撞车

> **现象**：站在地刺上，扣血的节奏和地刺闪动的节奏对不上——地刺明显每 0.4 秒闪一次，人物却要将近 1 秒才掉一次血。而且掉血的间隔看起来还不稳定。
>
> **根因**：`DamageZone.tickInterval` 配置得**小于**玩家的 `invincibilityDuration`。
> 每一轮「够快」的 tick 都落在上一轮的 0.5 秒无敌窗口里，`TakeDamage` 在第 52 行直接 `return`，**伤害被静默吞掉**——不报错、不触发 `Damaged`、不掉血、不闪白。
>
> 结果就是：配置 0.4 秒一次，实际 0.8 秒一次，中间一半的伤害凭空消失。
>
> **解法**：`tickInterval` 改成 **0.6 秒**，严格大于无敌帧的 0.5 秒。这样每一次 tick 都发生在上一次无敌窗口关闭之后，所有伤害稳定生效。
>
> **记下来的规矩**：**持续伤害的 tick 间隔必须严格大于目标的无敌帧时长。** 改其中一个时必须检查另一个。

---

## 七、如果要改，改这里

### 7.1 加护盾（先扣盾再扣血）

在 `Health` 内部加，**不要动接口**。位置在第 57 行和第 59 行之间：

```csharp
// 在 amount 算出来之后插入
if (_shield > 0f)
{
    float absorbed = Mathf.Min(_shield, amount);
    _shield -= absorbed;
    amount  -= absorbed;
}
```

需要新增一个 `_shield` 字段和一个 `AddShield(float)` 公开方法。

> ⚠️ 注意：如果盾把伤害**全部吸掉**（`amount` 变成 0），第 59 行不会改变血量，但第 60 行仍然会启动无敌帧、第 62 行仍然会触发 `Damaged`。
> **要不要这样取决于你的设计**：如果希望「被打在盾上也算受击（有闪白和击退）」，保持现状；如果希望「盾没破就不该有受击反馈」，就在第 57 行后加 `if (amount <= 0f) return;`。

### 7.2 加「伤害类型」抗性

`DamageInfo` 先加字段（见 `04-DamageInfo.md` 第 7.1 节）：

```csharp
public ElementType Element;   // 火、冰、雷、无
```

然后在 `Health` 里按类型查表减免。**推荐做法**是在 `CharacterStats` 里加一组属性（`FireResistance` 等），`Health` 只负责读——保持「数值在数据层、结算在结算层」的分工。

> ⚠️ **不要**在 `Health` 里写 `switch (info.Element) { case ElementType.Fire: ... }` 这样的硬编码表。那会让「加一种元素」变成「改战斗代码」，正是 `IDamageable` 想避免的事。

### 7.3 加治疗 / 回血

**新增方法，不要复用 `TakeDamage`：**

```csharp
public void Heal(float amount)
{
    if (!IsAlive) return;                                  // 死了不能治
    if (amount <= 0f) return;                              // 拒绝负数（否则变成伤害）

    Current = Mathf.Min(maxHealth, Current + amount);      // 不能超过上限
    // 注意：不触发 Damaged（那是受伤事件），要不要加 Healed 事件看需求
}
```

> **`Mathf.Min(maxHealth, ...)` 是这里的 `Mathf.Max(0f, ...)`**——一个防下限，一个防上限，位置对称。
>
> 如果想让血条 UI 知道血量变了，可以加 `public event Action<float> Healed;`，或者更通用的 `public event Action Changed;`（受伤和治疗都触发）。加 `Changed` 需要同时改第 62 行——**改的时候记得两个事件都要发**。

### 7.4 无敌帧改成「和受击硬直同步」

现在 `Health._invincibilityTimer`（0.5s）和 `HitReaction._stunTimer`（0.18s）是两个独立的计时器。

如果想让它们完全同步（受击硬直期间天然无敌），有两种做法：

| 做法 | 优点 | 缺点 |
|---|---|---|
| 把 `_invincibilityTimer` 的赋值改成读 `HitReaction` 的时长 | 一个来源 | `Health` 要依赖 `HitReaction`——**破坏分层**（结算层依赖表现层） |
| 反过来，让 `HitReaction` 读 `Health` 的无敌时长 | 依赖方向正确 | 表现层本来就没必要知道无敌帧 |

> **目前保持独立是正确的。** 它们本来就是两个概念：
> - **无敌帧**是「不再受伤」（规则）
> - **硬直**是「不能操作」（手感）
>
> 概念不同，就不该强行合并。哪天需要「受击后 1 秒无敌但只有 0.18 秒硬直」，现在的结构直接改数字就行，合并了就改不动了。

### 7.5 让 `Died` 带上死因

现在 `Died` 是无参的。如果将来要做「被火杀死 → 掉落火系词条」，需要改成：

```csharp
public event Action<DamageInfo> Died;      // 注意：签名变了
```

⚠️ **这是一个破坏性改动**，订阅方都必须跟着改：

| 订阅者 | 现在的写法 | 改后 |
|---|---|---|
| `EnemyController` | `void OnDied()` | `void OnDied(DamageInfo info)` |
| `RunManager` | `void OnPlayerDied()` | `void OnPlayerDied(DamageInfo info)` |

**C# 编译器会帮你抓出所有漏改的地方**（因为方法签名不匹配，订阅语句编译不过）——这是用 `event` 而不是自己维护一个回调列表的好处之一。

> 如果不想改现有订阅者，可以**新增**一个事件而不改旧的：
> ```csharp
> public event Action Died;
> public event Action<DamageInfo> Killed;   // 新增，带上死因
> ```
> 两个都在第 65 行触发。代价是多了一个事件要维护，好处是零破坏性改动。**在项目已经跑通、不想引入风险的时候，这是更稳的选择。**

### 7.6 血条 UI

**已经预留好了**：第 25 行的 `Normalized` 就是给血条用的。

```csharp
// HealthBarUI.cs（Day 5 之后要写）
_health.Damaged += _ => Refresh();
_health.Died    += () => Hide();

void Refresh() => _fill.fillAmount = _health.Normalized;
```

⚠️ **注意治疗的情况**：上面这样写，治疗不会刷新血条（因为 `Heal` 不触发 `Damaged`）。
如果第 7.3 节加了治疗，血条要订阅 `Healed` 或新增的 `Changed` 事件。

> **关于订阅时机**：按项目的约定（见 `00-总览与阅读指南.md` 第四节第 3 条），**订阅写在 `OnEnable`，退订写在 `OnDisable`**，不要用 `Start` / `OnDestroy`。
> 原因是物体被 `SetActive(false)` 时 `OnDestroy` 不触发但 `OnDisable` 会，只加不减会导致「已停用的对象仍被事件调用」，报 `MissingReferenceException`。

### 7.7 不要在这里加表现代码

想在受击时加音效、粒子、屏幕震动、伤害飘字？

**全部加到 `HitReaction.OnDamaged` 里，`Health` 一行都不动。**

这就是第 3.4 节说的「逻辑与表现分离」。判断标准很简单：

> **这段代码在「没有屏幕、没有声音」的纯逻辑测试里还有意义吗？**
> - 「扣 7.2 血」有意义 → 属于 `Health`
> - 「播放 hit.wav」没意义 → 属于 `HitReaction`

具体的扩展方式见 `07-HitReaction.md` 第七节。
