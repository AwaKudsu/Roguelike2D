# 02 · CharacterStats

## 一、头部信息表

| 项 | 内容 |
|---|---|
| 文件路径 | `D:\Roguelike2D\Assets\_Project\Scripts\Core\CharacterStats.cs` |
| 所属层 | ① 数据层 |
| 依赖 | `ClassData`（职业配置）、`Health`（同步最大生命）、`StatTypes`（`StatType` / `ModifierMode` / `StatModifier`） |
| 被谁依赖 | `PlayerController`（读移速）、`MeleeAttacker`（读攻击/暴击/攻速/冷却缩减）、`EnemyController`（读移速/攻击）、`Health`（读防御） |
| 行数 | 175 行 |
| 最后更新 | Day 4 |

> **历史**：这个文件原名 `PlayerStats.cs`，Day 4 做敌人时改名为 `CharacterStats.cs`。改名的理由见第 6 节第 2 条。

---

## 二、一句话定位

所有战斗数值的唯一出口——基础值经过修饰符，算出一个最终数字。

---

## 三、为什么需要它

假设没有这个文件。攻击力只能写在攻击代码里：

```csharp
float damage = 10f;   // 玩家攻击力
```

那么「战士攻击 13、法师攻击 11」就得改成：

```csharp
float damage = isMage ? 11f : 13f;
```

再加上装备的「+5 攻击力」、技能的「+20% 攻击力」、强化的「×1.15」—— 这段代码会迅速长成一棵没人敢碰的判断树。

**而这棵树还不止一棵。** 每个需要读数值的地方都要重复一遍：

| 谁要读 | 读什么 |
|---|---|
| `PlayerController` | 移动速度 |
| `MeleeAttacker` | 攻击力、暴击率、暴击倍率、攻击速度 |
| `Health` | 防御 |
| （将来）血条 UI | 最大生命 |

四个地方、四棵树。改一次数值公式要改四遍，而且必然漏掉一处。

`CharacterStats` 把这件事收成一个问题：

```csharp
stats.Get(StatType.Attack)
```

这个 `13` 是职业给的、装备给的还是强化给的，**调用方根本不需要知道**。它也不关心你后面加了什么新系统——只要那个系统调 `AddModifier`，数值就会自动出现在所有读取点。

### 它在整个架构里的位置

```
   职业 ClassData        ┐
   装备 EquipmentData    ├──▶ CharacterStats ──▶ Get(StatType) ──▶ 24.84
   技能 SkillData        │      基础值 + 修饰符
   强化 UpgradeData      ┘
```

**左边四个加在一起，也只是「往同一个容器里塞数据」。** 这就是为什么这个文件是数据层的核心——它不是「玩家的属性」，而是「角色的属性」，玩家和敌人共用同一套（`EnemyController` 也读它的 `MoveSpeed` 和 `Attack`）。

---

## 四、代码全解

### 块 1 · 文件头与字段区（第 1–38 行）

```csharp
1: using System.Collections.Generic;
2: using UnityEngine;
3: 
4: /// <summary>
5: /// 角色所有战斗数值的唯一出口。玩家和敌人共用。
6: ///
7: /// 职业 / 装备 / 技能 / 强化都只是「往这里加修饰符」，
8: /// 战斗代码只调 Get() 拿最终值，完全不需要知道数值是从哪来的。
9: /// </summary>
10: public class CharacterStats : MonoBehaviour
11: {
12:     [Header("数据来源")]
13:     [Tooltip("玩家填职业资产。敌人留空，直接用手填的基础值")]
14:     [SerializeField] private ClassData classData;
15: 
16:     [Header("基础属性（classData 留空时使用）")]
17:     [SerializeField] private float baseMaxHealth      = 100f;
18:     [SerializeField] private float baseAttack         = 10f;
19:     [SerializeField] private float baseMoveSpeed      = 8f;
20:     [SerializeField] private float baseAttackSpeed    = 1f;
21:     [Range(0f, 1f)]
22:     [SerializeField] private float baseCritChance     = 0.05f;
23:     [SerializeField] private float baseCritMultiplier = 1.5f;
24:     [SerializeField] private float baseDefense        = 0f;
25:     [SerializeField] private float baseCooldownRate   = 0f;
26: 
27:     [Header("联动")]
28:     [Tooltip("留空则自动找同物体上的 Health，把最大生命同步过去")]
29:     [SerializeField] private Health health;
30: 
31:     private readonly Dictionary<StatType, float> _baseValues = new();
32:     private readonly Dictionary<StatType, List<StatModifier>> _modifiers = new();
33: 
34:     /// <summary>属性变化时触发（穿脱装备、强化生效）。血条和 UI 订阅它刷新</summary>
35:     public event System.Action Changed;
36: 
37:     /// <summary>当前使用的职业数据，只有玩家才有</summary>
38:     public ClassData Class => classData;
```

| 行 | 代码 | 含义 |
|---|---|---|
| 1 | `using System.Collections.Generic;` | 引入泛型集合命名空间。这个文件要用 `Dictionary<>` 和 `List<>`，它们都定义在这里 |
| 2 | `using UnityEngine;` | 引入 Unity 引擎命名空间。要用到 `MonoBehaviour`、`SerializeField`、`Header`、`Tooltip`、`Range`、`GetComponent` |
| 4–9 | `/// <summary>...</summary>` | 类型级文档注释。**第 5 行那句「玩家和敌人共用」解释了为什么它不叫 `PlayerStats`** |
| 10 | `public class CharacterStats : MonoBehaviour` | 冒号表示**继承**。`MonoBehaviour` 是所有「能挂到 GameObject 上」的组件的基类。继承它才能用 `Awake` / `Start` / `OnEnable` 这些生命周期方法，才能出现在 `Add Component` 菜单里 |
| 11 | `{` | 类体开始 |
| 12 | `[Header("数据来源")]` | Inspector 里画一条**粗体分组标题**。纯装饰，不影响任何逻辑 |
| 13 | `[Tooltip("玩家填职业资产。敌人留空...")]` | 鼠标悬停在下一个字段上时，弹出的说明文字。**这个 Tooltip 是这个文件里最重要的一句使用说明**——它一句话讲清了玩家和敌人的区别 |
| 14 | `[SerializeField] private ClassData classData;` | 职业数据引用。三个部分拆开看：`private` 是 C# 的访问修饰符，只有本类内部能访问；`[SerializeField]` 是 Unity 的特性，意思是「虽然它是 private，也请显示在 Inspector 里并且保存它」；`ClassData` 是类型，表示这里只能放职业资产 |
| 16 | `[Header("基础属性（classData 留空时使用）")]` | 第二条分组标题。括号里说明了这 8 个字段的**生效条件**——只有 `classData` 为空时才用它们 |
| 17 | `baseMaxHealth = 100f` | 手填的最大生命。只在敌人身上生效 |
| 18 | `baseAttack = 10f` | 手填的攻击力 |
| 19 | `baseMoveSpeed = 8f` | 手填的移动速度。**注意是 8 而不是敌人实际用的 3.2**——这个默认值是按玩家调的，敌人要在 Inspector 里手动改小 |
| 20 | `baseAttackSpeed = 1f` | 手填的攻击速度倍率。`1` 表示不加速也不减速 |
| 21 | `[Range(0f, 1f)]` | Unity 特性。把下一个 `float` 在 Inspector 里显示成**滑条**，拖动范围限制在 0~1。用在这里是因为暴击率是概率，不可能超过 100% |
| 22 | `baseCritChance = 0.05f` | 手填的暴击率。`0.05` = 5% |
| 23 | `baseCritMultiplier = 1.5f` | 手填的暴击伤害倍率。`1.5` = 暴击打出 150% 伤害 |
| 24 | `baseDefense = 0f` | 手填的防御。默认 0，表示不减伤 |
| 25 | `baseCooldownRate = 0f` | 手填的冷却缩减。默认 0 |
| 27 | `[Header("联动")]` | 第三条分组标题 |
| 28 | `[Tooltip("留空则自动找同物体上的 Health...")]` | 说明这个字段**可以不填**，代码会自动补 |
| 29 | `[SerializeField] private Health health;` | 对同物体上 `Health` 组件的引用。`CharacterStats` 算出最大生命之后，要通过这个引用把它告诉 `Health` |
| 31 | `private readonly Dictionary<StatType, float> _baseValues = new();` | 见下方逐块拆解 |
| 32 | `private readonly Dictionary<StatType, List<StatModifier>> _modifiers = new();` | 见下方逐块拆解 |
| 34 | `/// <summary>属性变化时触发...` | 说明事件的用途和订阅者 |
| 35 | `public event System.Action Changed;` | 见下方逐块拆解 |
| 37–38 | `public ClassData Class => classData;` | 见下方逐块拆解 |

#### 逐块拆解：第 31 行

```csharp
private readonly Dictionary<StatType, float> _baseValues = new();
```

| 部分 | 含义 |
|---|---|
| `private` | 只有本类内部能访问。外部想读基础值只能通过 `Get()` |
| `readonly` | **只能在声明时或构造函数里赋值。** 这里在声明时就 `= new()` 创建了，之后永远不能写 `_baseValues = 另一个字典`。⚠️ 但 `readonly` 锁的是「引用」不是「内容」——`_baseValues[StatType.Attack] = 5f` 这样的**修改内容是允许的** |
| `Dictionary<StatType, float>` | 字典。尖括号里是「键, 值」：键是 `StatType`（哪个属性），值是 `float`（那个属性的基础值） |
| `_baseValues` | 字段名。下划线前缀是 C# 社区的约定，一眼看出「这是私有字段」 |
| `= new()` | C# 9 的**目标类型 new** 语法。因为左边已经写明了类型，右边可以省略，等价于 `= new Dictionary<StatType, float>()` |

#### 逐块拆解：第 32 行

```csharp
private readonly Dictionary<StatType, List<StatModifier>> _modifiers = new();
```

结构和上一行一样，只是**值变成了 `List<StatModifier>`**——每个属性上都挂着**一串**修饰符，而不是一个数字。

整体读作：「**属性 → 该属性上挂着的所有修饰符**」。

> **为什么用两层字典 + 列表，而不是一个大的 `List<StatModifier>`？**
>
> 如果只有一个大列表，每次 `Get(StatType.Attack)` 都要把**全部**修饰符遍历一遍，逐个判断 `m.Type == type`。玩家有 30 个词条时，读一次攻击力要过 30 个元素。
>
> 分桶之后，所有「攻击力」的修饰符集中在同一个列表里，读攻击力只看攻击力那个桶。
>
> 这个优化的意义在于 `Get()` 是热路径——每帧会被调用多次（移速每物理帧读一次，攻击每次结算读三次）。

#### 逐块拆解：第 35 行

```csharp
public event System.Action Changed;
```

| 部分 | 含义 |
|---|---|
| `public` | 外部可以订阅 |
| `event` | C# 关键字。它把委托**包装成事件**，外部只能 `+=`（订阅）和 `-=`（退订），**不能用 `=` 覆盖** |
| `System.Action` | .NET 内置的委托类型，表示「**无参数、无返回值**的方法」。这里写全名是因为文件顶部只 `using` 了 `System.Collections.Generic`，没有 `using System;` |
| `Changed` | 事件名 |

> **`event` 关键字省掉会怎样？**
>
> 如果写成普通的公开字段：
>
> ```csharp
> public System.Action Changed;   // ❌ 没有 event
> ```
>
> 任何一个脚本都能写 `stats.Changed = null;`——**这一句会把所有订阅者全部清空**，而且不报错。之后你改了装备，血条 UI 一动不动，你会去查 UI 代码，查半天。
>
> `event` 让 `=` 变成编译错误，从语言层面堵住这条路。

#### 逐块拆解：第 38 行

```csharp
public ClassData Class => classData;
```

| 部分 | 含义 |
|---|---|
| `public` | 外部可读 |
| `ClassData` | 返回类型 |
| `Class` | 属性名。首字母大写，是公开成员的命名约定 |
| `=>` | **表达式主体成员**（expression-bodied member）。等价于 `{ get { return classData; } }`，只是写得更短 |
| `classData` | 返回的私有字段 |

这是一个**只读属性**：外部能读到「当前是什么职业」，但改不了。要改必须走 `ApplyClass()`，那里会顺带把基础值和修饰符都处理干净。

> 目前还没有任何脚本读 `Class`——它是为将来的「职业切换 UI」「技能树界面」准备的。**留一个只读出口是低成本的**，等真要用的时候不用回头改这个文件。

> **为什么不用一堆 `float` 字段，而要用字典？**
>
> 假设写成：
>
> ```csharp
> private float _maxHealth;
> private float _attack;
> private float _moveSpeed;
> private float _attackSpeed;
> private float _critChance;
> private float _critMultiplier;
> private float _defense;
> private float _cooldownRate;
> ```
>
> 那么 `Get()` 就没法写成通用方法了，只能写成一个大 `switch`：
>
> ```csharp
> switch (type)
> {
>     case StatType.MaxHealth: return _maxHealth;
>     case StatType.Attack:    return _attack;
>     // ...还有 6 个
> }
> ```
>
> **每加一个属性，都要回来改这个 `switch`。** 而字典方案下 `Get()` 一行都不用动——新属性天然就能用，因为它只是字典里的一个新键。
>
> 这就是「属性是按需扩展的」这个事实，在代码结构上的体现。

> **`[SerializeField] private` 和 `public` 的区别（和 `ClassData` 对比着看）**
>
> `CharacterStats` 里所有字段都是 `[SerializeField] private`，而 `ClassData` 里全是 `public`。这不是风格不统一，是有原因的：
>
> | | `CharacterStats`（组件） | `ClassData`（数据资产） |
> |---|---|---|
> | 写法 | `[SerializeField] private` | `public` |
> | 谁需要读 | 战斗代码要在**运行时**读，但只该通过 `Get()` 读 | 只有 `CharacterStats.ApplyClassData()` 读，一次性 |
> | 为什么这样写 | 保证外部无法绕过 `Get()` 直接拿到基础值——否则别人读到的会是「没算加成的原始值」，那是错的 | 它只是一张数据表，字段本身就是它的全部意义，包一层 getter 没有价值 |
>
> 一句话：**有逻辑的类，字段藏起来；纯数据的类，字段敞开。**

---

### 块 2 · 生命周期（第 40–58 行）

```csharp
40:     private void Awake()
41:     {
42:         if (health == null) health = GetComponent<Health>();
43: 
44:         WriteFallbackBaseValues();
45: 
46:         // 职业数据在基础值之后应用，它会覆盖掉手填值
47:         if (classData != null) ApplyClassData(classData);
48:     }
49: 
50:     private void Start()
51:     {
52:         // 放在 Start 而不是 Awake：要等所有 Health.Awake() 跑完（血量已初始化）
53:         // 否则 SetMaxHealth 按比例算出来的血量是错的
54:         SyncMaxHealthToHealth();
55:     }
56: 
57:     private void OnEnable()  => Changed += SyncMaxHealthToHealth;
58:     private void OnDisable() => Changed -= SyncMaxHealthToHealth;
```

| 行 | 代码 | 含义 |
|---|---|---|
| 40 | `private void Awake()` | Unity 生命周期方法。对象被创建时调用**一次**，且早于 `Start` |
| 42 | `if (health == null) health = GetComponent<Health>();` | 如果 Inspector 里没手动拖引用，就在**同一个 GameObject 上**找 `Health` 组件。`GetComponent<T>()` 找不到时返回 `null`。这种「留空就自动补」的写法很常见——能省一步手动配置，同时也允许高级用法（比如把引用指向别的物体） |
| 44 | `WriteFallbackBaseValues();` | 先把 8 个手填值写进 `_baseValues` 字典 |
| 46 | `// 职业数据在基础值之后应用，它会覆盖掉手填值` | 这行注释解释了接下来三行的**顺序意义** |
| 47 | `if (classData != null) ApplyClassData(classData);` | 只有填了职业资产才执行。**它在第 44 行之后**——如果反过来写，手填值会覆盖职业值，你把 `Class Data` 拖成战士（血量 140）结果还是 100 |
| 50 | `private void Start()` | 生命周期方法。**Unity 保证所有对象的 `Awake()` 全部执行完之后，才开始执行任何 `Start()`** |
| 54 | `SyncMaxHealthToHealth();` | 把算好的最大生命告诉 `Health`。放这里的理由见下方 |
| 57 | `private void OnEnable()  => Changed += SyncMaxHealthToHealth;` | 物体启用时**订阅**自己的 `Changed` 事件。写法上用了 `=>` 表达式主体，等价于 `{ Changed += SyncMaxHealthToHealth; }` |
| 58 | `private void OnDisable() => Changed -= SyncMaxHealthToHealth;` | 物体停用时**退订**。`-=` 的参数和第 57 行必须**完全一致**，否则退订不掉 |

> **为什么 `SyncMaxHealthToHealth()` 放在 `Start()` 而不是 `Awake()`？**
>
> 这是整个项目里最隐蔽的一个时序坑，值得单独讲清楚。
>
> `Health.SetMaxHealth()` 内部要按「当前血量 ÷ 旧上限」算出比例，这样才能在最大生命变化时保持血量百分比：
>
> ```csharp
> float ratio = maxHealth > 0f ? Current / maxHealth : 1f;
> ```
>
> `Health.Current` 是在 `Health.Awake()` 里初始化的（`Current = maxHealth`）。
>
> **Unity 不保证同一帧内不同组件的 `Awake` 谁先谁后。** 如果 `CharacterStats.Awake` 恰好比 `Health.Awake` 先跑，那么上面那个 `Current` 还是初始值 `0`，比例算出来是 `0`：
>
> ```
> SetMaxHealth(140)
>   → ratio = 0 / 100 = 0
>   → Current = 140 × 0 = 0
> ```
>
> **战士会以 0 血开局，一出生就死。** 而且这个 bug 是**概率性**的——换了机器、改了组件顺序，它可能就不出现了，极难复现。
>
> 放在 `Start()` 就绝对安全，因为 Unity 对 `Start` 有明确的保证：**所有 `Awake` 跑完，`Start` 才开始**。
>
> > **记住这条判断规则**：跨组件的初始化，只要需要读「别的组件在它自己的 `Awake` 里才建好的状态」，就放到 `Start`。

> **为什么订阅要用 `OnEnable` / `OnDisable`，而不是 `Start` / `OnDestroy`？**
>
> 因为物体被 `SetActive(false)` 时，**`OnDestroy` 不会触发，但 `OnDisable` 会**。
>
> 只 `+=` 不 `-=` 的后果是：物体停用或被销毁之后，`Changed` 事件里仍然留着它的引用。下次有人调 `stats.AddModifier()`，Unity 会去调用一个已经不存在的方法——报错是 `MissingReferenceException`，而且它在 Console 里显示的调用栈指向的是「谁触发了事件」，不是「谁没退订」，非常难定位。
>
> **凡是写了 `+=`，就必须在对称的位置写 `-=`。** 在这行代码的正下方写，不要拖到文件末尾。

---

### 块 3 · 取值 `Get()`（第 60–85 行）

```csharp
60:     // ---------------- 取值 ----------------
61: 
62:     /// <summary>取某个属性的最终值</summary>
63:     public float Get(StatType type)
64:     {
65:         float flat       = _baseValues.TryGetValue(type, out var b) ? b : 0f;
66:         float percentAdd = 0f;
67:         float percentMul = 1f;
68: 
69:         if (_modifiers.TryGetValue(type, out var list))
70:         {
71:             for (int i = 0; i < list.Count; i++)
72:             {
73:                 var m = list[i];
74:                 switch (m.Mode)
75:                 {
76:                     case ModifierMode.Flat:            flat       += m.Value;       break;
77:                     case ModifierMode.PercentAdd:      percentAdd += m.Value;       break;
78:                     case ModifierMode.PercentMultiply: percentMul *= 1f + m.Value;  break;
79:                 }
80:             }
81:         }
82: 
83:         // 固定顺序：加算 → 百分比加算 → 百分比乘算
84:         return flat * (1f + percentAdd) * percentMul;
85:     }
```

| 行 | 代码 | 含义 |
|---|---|---|
| 60 | `// ---------------- 取值 ----------------` | 分节注释。本项目用这种长横线注释把文件切成几块，方便快速定位 |
| 62–63 | `public float Get(StatType type)` | 公开方法。传入一个属性名，返回它的**最终值**（已经算上所有加成） |
| 65 | `float flat = _baseValues.TryGetValue(type, out var b) ? b : 0f;` | 见下方逐块拆解 |
| 66 | `float percentAdd = 0f;` | 百分比加算的**累加器**，初始 `0`。因为加法的单位元是 0（加 0 不变） |
| 67 | `float percentMul = 1f;` | 百分比乘算的**累乘器**，初始 `1`。因为乘法的单位元是 1（乘 1 不变）。⚠️ 写成 `0f` 会让结果永远是 0 |
| 69 | `if (_modifiers.TryGetValue(type, out var list))` | 从修饰符字典里取这个属性的桶。`out var list` 的类型是 `List<StatModifier>`。没取到（这个属性还没有任何加成）就跳过整个 `if` |
| 71 | `for (int i = 0; i < list.Count; i++)` | 遍历这个桶。用 `for` 而不是 `foreach` 是热路径的习惯性选择——`foreach` 遍历 `List<T>` 其实也不会产生 GC（编译器会用结构体枚举器），但 `for` 更直接、开销更小 |
| 73 | `var m = list[i];` | 把当前元素**复制**到局部变量。`StatModifier` 是 `struct`（值类型），这里是值复制，不是引用 |
| 74 | `switch (m.Mode)` | 按叠加方式分三路处理 |
| 76 | `case ModifierMode.Flat: flat += m.Value; break;` | **加算**：直接累加到 `flat` 上 |
| 77 | `case ModifierMode.PercentAdd: percentAdd += m.Value; break;` | **百分比加算**：只累加，**不立刻作用**。这就是「多个同类先求和」的实现方式 |
| 78 | `case ModifierMode.PercentMultiply: percentMul *= 1f + m.Value; break;` | **百分比乘算**：立刻连乘。`m.Value` 是 `0.2`（+20%），所以乘的系数是 `1 + 0.2 = 1.2` |
| 79 | `}` | `switch` 结束 |
| 80 | `}` | `for` 结束 |
| 81 | `}` | `if` 结束 |
| 83 | `// 固定顺序：加算 → 百分比加算 → 百分比乘算` | 把顺序写死在注释里，改公式的人一眼能看到约束 |
| 84 | `return flat * (1f + percentAdd) * percentMul;` | **最终公式。** 注意括号：`1f + percentAdd` 必须先算，所以两个 `+20%` 是 `1 + 0.4 = 1.4`，不是 `1.2 × 1.2` |

#### 逐块拆解：第 65 行

```csharp
float flat = _baseValues.TryGetValue(type, out var b) ? b : 0f;
```

| 部分 | 含义 |
|---|---|
| `TryGetValue(type, out var b)` | 字典的「安全取数」方法。它**返回一个 `bool`** 表示「找到没找到」，同时把找到的值通过 `out` 参数递出来 |
| `out var b` | `out` 表示「这个参数是用来**输出**的」（进方法前不用赋值，出方法时一定有值）；`var` 让编译器自己推断类型（这里推断成 `float`） |
| `? b : 0f` | **三元运算符**。`条件 ? 成立时的值 : 不成立时的值`。找到了就用 `b`，没找到就用 `0f` |

> **为什么不用 `_baseValues[type]` 这种更短的写法？**
>
> 因为**方括号索引器在键不存在时会抛异常**（`KeyNotFoundException`），游戏直接崩。
>
> `TryGetValue` 找不到就返回 `false`，不会抛异常。这里的 `: 0f` 是一个**兜底**——万一某个属性忘了写基础值，它会安静地退化成 0，而不是让游戏崩溃。
>
> 这在数据驱动的系统里很重要：**漏配一个数字是常态，它不该导致崩溃。**

#### 具体算例：三种模式合在一起

假设角色是战士，身上有三份加成：

| 来源 | 内容 | 对应代码 |
|---|---|---|
| 职业（基础值） | 攻击力 13 | `_baseValues[StatType.Attack]` |
| 装备「铁剑」 | `Attack, Flat, +5` | 第 76 行 |
| 技能「战意」 | `Attack, PercentAdd, +0.20` | 第 77 行 |
| 强化「狂怒」 | `Attack, PercentMultiply, +0.15` | 第 78 行 |

代入 `Get(StatType.Attack)` 逐行演算：

```
第 65 行： flat = 13
第 66 行： percentAdd = 0
第 67 行： percentMul = 1

遍历到铁剑：   第 76 行 → flat = 13 + 5 = 18
遍历到战意：   第 77 行 → percentAdd = 0 + 0.20 = 0.20
遍历到狂怒：   第 78 行 → percentMul = 1 × (1 + 0.15) = 1.15

第 84 行： return 18 × (1 + 0.20) × 1.15
                = 18 × 1.20 × 1.15
                = 24.84
```

**最终攻击力是 24.84。**

> **如果不区分三种模式、全部当加算会怎样？**
>
> ```
> 13 + 5 + 0.20 + 0.15 = 18.35
> ```
>
> 「战意 +20%」被当成了「+0.2 点攻击力」，「狂怒 ×1.15」被当成了「+0.15 点攻击力」。两个百分比加成的实际贡献从 `+6.84` 缩水到了 `+0.35`。
>
> **构筑系统彻底失去意义**——玩家捡到一个「攻击力 +20%」的传说词条，实际效果还不如一把 +1 攻击力的白板剑。
>
> 这就是 `ModifierMode` 存在的全部理由。

> **`percentMul` 初始值必须是 `1f`，不能是 `0f`**
>
> 这是累加/累乘代码里最经典的错误。
>
> 如果写成 `float percentMul = 0f;`：
> - 没有乘算修饰符时：`flat × 1.2 × 0 = 0`
> - 有乘算修饰符时：`flat × 0 = 0`
>
> **两种情况都是 0。角色会彻底打不出伤害，而且看起来完全不像代码错误**——你会先去怀疑装备没生效、技能没触发，绕一大圈才发现是一个初始化值。
>
> 判断方法：**做累加就初始化为 0，做累乘就初始化为 1。** 这是「单位元」的概念——加上它不变的那个数是 0，乘上它不变的那个数是 1。

---

### 块 4 · 修饰符管理（第 87–122 行）

```csharp
87:     // ---------------- 修饰符 ----------------
88: 
89:     public void AddModifier(StatModifier modifier, object source = null)
90:     {
91:         modifier.Source = source;
92: 
93:         if (!_modifiers.TryGetValue(modifier.Type, out var list))
94:         {
95:             list = new List<StatModifier>();
96:             _modifiers[modifier.Type] = list;
97:         }
98: 
99:         list.Add(modifier);
100:         Changed?.Invoke();
101:     }
102: 
103:     /// <summary>移除某个来源加的全部修饰符 —— 卸下装备时调这一句就够</summary>
104:     public void RemoveAllFrom(object source)
105:     {
106:         if (source == null) return;
107: 
108:         bool removed = false;
109:         foreach (var list in _modifiers.Values)
110:         {
111:             if (list.RemoveAll(m => ReferenceEquals(m.Source, source)) > 0)
112:                 removed = true;
113:         }
114: 
115:         if (removed) Changed?.Invoke();
116:     }
117: 
118:     public void ClearModifiers()
119:     {
120:         _modifiers.Clear();
121:         Changed?.Invoke();
122:     }
```

| 行 | 代码 | 含义 |
|---|---|---|
| 87 | `// ---------------- 修饰符 ----------------` | 分节注释 |
| 89 | `public void AddModifier(StatModifier modifier, object source = null)` | 加一条修饰符。第二个参数 `object source = null` 是**带默认值的参数**——调用时可以只写 `AddModifier(mod)`，这时 `source` 就是 `null` |
| 91 | `modifier.Source = source;` | 把来源写进**参数副本**。必须在第 99 行之前做，理由见下方 |
| 93 | `if (!_modifiers.TryGetValue(modifier.Type, out var list))` | 看看这个属性有没有现成的桶。`!` 表示取反——「如果**没**找到」 |
| 95 | `list = new List<StatModifier>();` | 没找到就新建一个空列表 |
| 96 | `_modifiers[modifier.Type] = list;` | 放进字典。**这里用索引器而不是 `.Add()`**——因为 `.Add()` 在键已存在时会抛异常（`ArgumentException`），而索引器是「有就覆盖，没有就新增」，永远不会抛 |
| 99 | `list.Add(modifier);` | 把修饰符加进桶里。此时它已经是第 91 行改过的那个版本 |
| 100 | `Changed?.Invoke();` | 广播「属性变了」 |
| 103 | `/// <summary>移除某个来源加的全部修饰符...` | 说明这个方法的用法——**「卸下装备时调这一句就够」** |
| 104 | `public void RemoveAllFrom(object source)` | 参数是之前 `AddModifier` 传进去的那个来源 |
| 106 | `if (source == null) return;` | **防御性编程。** 如果传 `null` 进来，第 111 行的 `ReferenceEquals(m.Source, null)` 会把**所有** `Source` 为 `null` 的修饰符全部删掉——那些是没有标记来源的加成。这是一场灾难，所以直接返回 |
| 108 | `bool removed = false;` | 记录「到底有没有删掉东西」。用来决定最后要不要广播 |
| 109 | `foreach (var list in _modifiers.Values)` | 遍历字典里**所有的值**（所有属性的桶）。因为同一个来源可能加了好几条不同属性的修饰符 |
| 111 | `if (list.RemoveAll(m => ReferenceEquals(m.Source, source)) > 0)` | 见下方逐块拆解 |
| 112 | `removed = true;` | 删掉过东西，标记一下 |
| 115 | `if (removed) Changed?.Invoke();` | **只有真的删掉了才广播。** 如果什么都没删还广播，会白白让所有 UI 刷新一次 |
| 118–122 | `ClearModifiers()` | `.Clear()` 清空整个字典。注意这里**没有** `if (removed)` 判断——调用方明确要求清空，就无条件广播一次 |

#### 逐块拆解：第 111 行

```csharp
if (list.RemoveAll(m => ReferenceEquals(m.Source, source)) > 0)
```

| 部分 | 含义 |
|---|---|
| `list.RemoveAll(...)` | `List<T>` 的方法。删除**所有**满足条件的元素，**返回删掉的个数**（`int`） |
| `m => ...` | **Lambda 表达式**。`m` 代表列表里的每个元素，箭头右边是判断条件（必须返回 `bool`）。等价于「对于每个 m，判断它该不该删」 |
| `ReferenceEquals(m.Source, source)` | **引用相等**比较：判断两个变量是不是指向**同一个对象** |
| `> 0` | 删掉的个数大于 0 |

> **为什么必须用 `ReferenceEquals`，不能用 `==`？**
>
> `==` 对 `object` 类型调用的是 `Equals()` 方法，而很多类都**重写**了 `Equals` 让它比较「内容」而不是「身份」。最典型的例子是字符串：
>
> ```csharp
> string a = "剑";
> string b = "剑";
> a == b          // true —— 内容相同
> ReferenceEquals(a, b)   // false —— 不是同一个对象
> ```
>
> 如果你的 `source` 传的是字符串 ID，用 `==` 会把「另一把同名的剑」的修饰符也一起删掉。
>
> **`ReferenceEquals` 判断的是「是不是同一个对象」，这才是「这个来源加的东西」的准确含义。** 这个方法定义在 `object` 上，任何类型都能调。

#### 逐块拆解：第 100 行的 `?.`

```csharp
Changed?.Invoke();
```

| 部分 | 含义 |
|---|---|
| `?.` | **空条件运算符**（null-conditional）。意思是「如果左边不是 `null`，才继续往后走」 |
| `Invoke()` | 触发事件的固定写法 |

> **为什么要 `?.`？**
>
> 如果没有任何人订阅过 `Changed`，它就是 `null`。直接写 `Changed()` 会抛 `NullReferenceException`。
>
> `?.` 把「非空检查 + 调用」压缩成两个字符。等价于：
>
> ```csharp
> if (Changed != null) Changed.Invoke();
> ```
>
> 顺带一提，`Changed?.Invoke()` 还有一种更短的写法 `Changed?.()`，效果完全一样。本项目统一用 `Invoke()` 是因为更明确——一眼能看出这是在触发事件。

> **`AddModifier` 为什么必须先赋值再 `Add`？**
>
> `StatModifier` 是 `struct`（值类型）。`list.Add(modifier)` 存进去的是参数的**一份副本**。
>
> 所以必须**在 `Add` 之前**把 `Source` 设好。加进去之后再改参数，改的是副本，列表里那份不受影响。
>
> 如果以后有人想反过来重构：
>
> ```csharp
> list.Add(modifier);
> list[list.Count - 1].Source = source;   // ❌ 编译错误
> ```
>
> 会**直接编译失败**，因为 `list[i]` 返回的是一个临时副本，C# 不允许给临时值的字段赋值。
>
> **这个编译错误其实在保护你**——它逼你把顺序写对。

> **`RemoveAllFrom` 为什么要把所有桶都遍历一遍？**
>
> 因为同一个来源可以加多条不同属性的修饰符：
>
> ```csharp
> var sword = 铁剑组件;
> stats.AddModifier(new StatModifier(StatType.Attack,     ModifierMode.Flat, 5f),   sword);
> stats.AddModifier(new StatModifier(StatType.CritChance, ModifierMode.Flat, 0.1f), sword);
> ```
>
> 卸下剑时，你要**一句** `stats.RemoveAllFrom(sword)` 把这两条一起删掉。
>
> 这就是 `Source` 字段存在的全部意义。没有它，你只能靠「删掉第 0 条和第 2 条」这种按位置删除的方式——装备一多，索引必然算错，而且删错了不会报错，只是数值莫名其妙不对。

---

### 块 5 · 职业（第 124–161 行）

```csharp
124:     // ---------------- 职业 ----------------
125: 
126:     /// <summary>应用一个职业：覆盖基础值、清空旧修饰符</summary>
127:     public void ApplyClass(ClassData data)
128:     {
129:         if (data == null) return;
130: 
131:         classData = data;
132:         ApplyClassData(data);
133: 
134:         // 换职业时，旧职业的装备 / 技能加成必须全部清掉，否则会叠加串味
135:         ClearModifiers();
136:         Changed?.Invoke();
137:     }
138: 
139:     private void ApplyClassData(ClassData data)
140:     {
141:         _baseValues[StatType.MaxHealth]      = data.maxHealth;
142:         _baseValues[StatType.Attack]         = data.attack;
143:         _baseValues[StatType.MoveSpeed]      = data.moveSpeed;
144:         _baseValues[StatType.AttackSpeed]    = data.attackSpeed;
145:         _baseValues[StatType.CritChance]     = data.critChance;
146:         _baseValues[StatType.CritMultiplier] = data.critMultiplier;
147:         _baseValues[StatType.Defense]        = data.defense;
148:         _baseValues[StatType.CooldownRate]   = data.cooldownRate;
149:     }
150: 
151:     private void WriteFallbackBaseValues()
152:     {
153:         _baseValues[StatType.MaxHealth]      = baseMaxHealth;
154:         _baseValues[StatType.Attack]         = baseAttack;
155:         _baseValues[StatType.MoveSpeed]      = baseMoveSpeed;
156:         _baseValues[StatType.AttackSpeed]    = baseAttackSpeed;
157:         _baseValues[StatType.CritChance]     = baseCritChance;
158:         _baseValues[StatType.CritMultiplier] = baseCritMultiplier;
159:         _baseValues[StatType.Defense]        = baseDefense;
160:         _baseValues[StatType.CooldownRate]   = baseCooldownRate;
161:     }
```

| 行 | 代码 | 含义 |
|---|---|---|
| 124 | `// ---------------- 职业 ----------------` | 分节注释 |
| 127 | `public void ApplyClass(ClassData data)` | 公开方法。**切职业时调用它**——目前还没做切换 UI，但接口先留好了 |
| 129 | `if (data == null) return;` | 空保护。传 `null` 进来什么也不做 |
| 131 | `classData = data;` | 记住当前职业。这样 `Class` 属性能和第 47 行的 `Awake` 读到一致的值 |
| 132 | `ApplyClassData(data);` | 把职业里的 8 个数字写进 `_baseValues`（见下方第 139 行） |
| 134 | `// 换职业时，旧职业的装备 / 技能加成必须全部清掉，否则会叠加串味` | 这行注释解释了为什么第 135 行不能省 |
| 135 | `ClearModifiers();` | 清空所有修饰符。理由见下方 |
| 136 | `Changed?.Invoke();` | 广播。第 135 行的 `ClearModifiers()` 内部已经广播过一次，这里再广播一次是**兜底**——保证即使一个修饰符都没有（`ClearModifiers` 也会广播，所以其实是为了代码意图清晰：切职业一定通知外界） |
| 139 | `private void ApplyClassData(ClassData data)` | 私有方法。只被第 47 行和第 132 行调用 |
| 141–148 | `_baseValues[StatType.X] = data.x;` | 逐条把职业字段搬进字典。左边是**索引器赋值**——键存在就覆盖，不存在就新增。这就是字典方案的好处：不需要预先在字典里放好 8 个默认值 |
| 151 | `private void WriteFallbackBaseValues()` | 私有方法。只被第 44 行调用 |
| 153–160 | `_baseValues[StatType.X] = baseX;` | 结构和上面完全一样，只是右边换成了 Inspector 里的手填字段 |

> **`ApplyClass` 里为什么必须调 `ClearModifiers()`？**
>
> 假设玩家是战士，装备了一把剑（攻击力 +5）。然后切职业到法师：
>
> ```csharp
> stats.ApplyClass(mageData);
> ```
>
> 如果没有第 135 行：
>
> | | 结果 |
> |---|---|
> | `_baseValues[Attack]` | 被改成法师的 11 ✅ |
> | 剑那条 `+5` 修饰符 | **还在字典里** ❌ |
>
> 法师会带着战士的剑继续打，而界面上根本没有这把剑。
>
> 更糟的是**反复切换**：如果每次都 `AddModifier` 而不清，修饰符会越叠越多。切 10 次职业，就有 10 条 `+5`。
>
> **现在还没有装备系统，所以这个 bug 不会出现。但等第 3 周做完背包，如果这行漏了，它会以「切职业后伤害莫名其妙变高」的形式暴露出来，而且极难定位**——因为你会先怀疑职业数据配错了。
>
> 所以现在就写上。**这类「提前把清理逻辑写对」的代码，成本是 1 行，收益是将来省半天。**

> **`Awake()` 里第 44 行和第 47 行的顺序为什么不能反？**
>
> ```csharp
> WriteFallbackBaseValues();                          // ① 先写手填值
> if (classData != null) ApplyClassData(classData);   // ② 再用职业覆盖
> ```
>
> 倒过来写，「先写职业值、再写手填值」，**手填值会覆盖职业值**——你把 `Class Data` 拖成战士（血量 140），结果实际血量还是手填的 100。
>
> 这个顺序体现的设计是：**手填值是「兜底」，职业值是「优先」。**
>
> 敌人身上不放 `classData`（第 14 行的字段为 `null`），第 ② 步直接跳过，走的就是纯手填值。**同一个组件，两种用法，靠的就是这两行的顺序。**

> **第 139 行和 151 行这两段代码长得几乎一样，为什么不合并？**
>
> | | `ApplyClassData` | `WriteFallbackBaseValues` |
> |---|---|---|
> | 数据来源 | `ClassData data` 的字段 | `this` 的手填字段 |
> | 调用时机 | `Awake`（有条件）/ `ApplyClass` | `Awake`（无条件） |
>
> 合并需要把两者的来源抽象成一个共同接口（比如让 `ClassData` 也实现 `IStatSource`），但那样做：
>
> 1. 要新增一个接口和 8 个属性（用来统一访问 `data.maxHealth` 和 `this.baseMaxHealth`）
> 2. 代码总量反而变多
> 3. 两处的**语义不同**——一个是「应用配置」，一个是「写入默认」，强行合并后调用点会变得难读
>
> **这种「两段相似但不相同的赋值」在数据层很常见，不要为了消除重复而过度抽象。** 判断标准是：如果抽象之后代码更短、更清楚，就抽；如果只是为了「看起来没有重复」，就留着。

---

### 块 6 · 联动与 `SetBase`（第 163–175 行）

```csharp
163:     /// <summary>把最终最大生命同步给 Health 组件</summary>
164:     private void SyncMaxHealthToHealth()
165:     {
166:         if (health != null) health.SetMaxHealth(Get(StatType.MaxHealth));
167:     }
168: 
169:     /// <summary>直接设基础值</summary>
170:     public void SetBase(StatType type, float value)
171:     {
172:         _baseValues[type] = value;
173:         Changed?.Invoke();
174:     }
175: }
```

| 行 | 代码 | 含义 |
|---|---|---|
| 163 | `/// <summary>把最终最大生命同步给 Health 组件</summary>` | 说明方法用途 |
| 164 | `private void SyncMaxHealthToHealth()` | 私有方法。被 `Start()`（第 54 行）和 `Changed` 事件（第 57 行）调用 |
| 166 | `if (health != null) health.SetMaxHealth(Get(StatType.MaxHealth));` | 见下方逐块拆解 |
| 169 | `/// <summary>直接设基础值</summary>` | 说明用途 |
| 170 | `public void SetBase(StatType type, float value)` | 公开方法。**绕过职业数据直接改基础值** |
| 172 | `_baseValues[type] = value;` | 覆盖字典里的基础值 |
| 173 | `Changed?.Invoke();` | 广播，让血条等订阅者刷新 |
| 175 | `}` | 类结束 |

#### 逐块拆解：第 166 行

```csharp
if (health != null) health.SetMaxHealth(Get(StatType.MaxHealth));
```

| 部分 | 含义 |
|---|---|
| `if (health != null)` | 空保护。如果这个物体上没有 `Health` 组件（比如某些纯配置物体），什么都不做 |
| `Get(StatType.MaxHealth)` | ⚠️ **这里调的是 `Get()`，不是 `_baseValues[StatType.MaxHealth]`** |
| `.SetMaxHealth(...)` | `Health` 上的方法。第二个参数 `keepRatio` 用默认值 `true` |

> **为什么调 `Get()` 而不是直接读 `_baseValues`？**
>
> 因为 `Get()` 会把**修饰符也算进去**。
>
> 如果玩家装备了一件「最大生命 +50」的装备，那么同步给 `Health` 的应该是：
>
> ```
> 基础 140 + 装备 50 = 190
> ```
>
> 而不是基础的 140。如果这里写成 `_baseValues[...]`，装备加的血量上限**永远不生效**，玩家会看到「装备上写着 +50 生命，但血条上限没变」。
>
> **凡是「把属性值交给别的系统」的地方，都要用 `Get()`。** `_baseValues` 只在 `ApplyClassData` 和 `WriteFallbackBaseValues` 这两个「写入」的地方出现。
>
> 顺带一提，`SetMaxHealth` 的 `keepRatio` 默认是 `true`，所以它会保持血量百分比——穿上加血装备时不会突然满血，也不会突然空血。这个行为是合理的：**装备提升上限不该顺便回血，那是治疗药水的职责。**

> **`SetBase` 目前还没有人调用**
>
> 它是为将来的场景预留的：比如「肉鸽通关后的临时强化」如果选择直接改基础值而不是加修饰符，就走这个方法。
>
> 不过更推荐用 `AddModifier`——因为修饰符可以按 `source` 精确撤回，而 `SetBase` 是覆盖式的，撤回时你得记得原来的值是多少。**`SetBase` 更适合「换职业」这种全量替换的场景**，而 `ApplyClass` 已经把那个场景包好了。

---

## 五、在 Unity 里怎么配

### 玩家身上的配置

1. Hierarchy 面板里选中 `Player`
2. 确认有 **`Character Stats`** 组件（`Add Component` → 搜 `Character Stats`）
3. **`Class Data`** 槽：拖入 `Class_Warrior` 或 `Class_Mage`
4. **`Health`** 槽：**留空**（第 42 行会自动在同物体上找）
5. 下面 8 个 **`Base XXX`** 字段：**不用管**。因为 `classData` 不为空，它们会在第 47 行被职业数据整体覆盖

### 敌人身上的配置

1. 选中 `Enemy`
2. 加 `Character Stats` 组件
3. **`Class Data`**：**必须留空**（这是敌人和玩家唯一的配置区别）
4. 手填下面这些：

| 字段 | 值 | 说明 |
|---|---|---|
| `Base Max Health` | **40** | 玩家打 3~4 下能打死 |
| `Base Attack` | **8** | 比玩家低，但不至于毫无威胁 |
| `Base Move Speed` | **3.2** | 明显慢于玩家（8），所以玩家永远能跑掉 |
| `Base Attack Speed` | 1 | 保持默认 |
| `Base Crit Chance` | 0 | 敌人不暴击，让玩家的暴击更有对比感 |
| `Base Crit Multiplier` | 1.5 | 默认，因为暴击率为 0 所以不生效 |
| `Base Defense` | 0 | 默认 |
| `Base Cooldown Rate` | 0 | 默认 |

5. `Health` 槽：**留空**

### 谁在读它的什么

| 脚本 | 读哪个属性 | 在哪读 |
|---|---|---|
| `PlayerController` | `MoveSpeed` | `ApplyHorizontalMovement()` 里的 `MaxSpeed` 属性 |
| `MeleeAttacker` | `Attack` / `CritChance` / `CritMultiplier` | `Execute()` |
| `MeleeAttacker` | `AttackSpeed` / `CooldownRate` | `GetCooldown()` |
| `EnemyController` | `MoveSpeed` | `Chase()` 里的 `MoveSpeed` 属性 |
| `EnemyController` | `Attack` | `Attack()` |
| `Health` | `Defense` | `TakeDamage()` |
| （将来）血条 UI | `MaxHealth` | 订阅 `Changed` 事件 |

> **注意这张表里没有一处直接访问 `_baseValues`。** 所有外部读取都走 `Get()`。这是这个文件最重要的使用约定——**只要有一处绕过 `Get()` 去读基础值，那个数字就是错的**（它没算加成）。

---

## 六、踩过的坑

### 坑 1 · `SyncMaxHealthToHealth()` 放在 `Awake` 里会导致角色以 0 血开局

> **现象**：把手填初始化的时机从 `Start` 挪到 `Awake` 之后，角色一进游戏就是 0 血、直接死亡；换回 `Start` 就正常。
>
> **根因**：`Health.SetMaxHealth()` 要按「当前血量 ÷ 旧上限」算血量保持比例：
>
> ```csharp
> // Health.cs 内部
> float ratio = maxHealth > 0f ? Current / maxHealth : 1f;
> ```
>
> 而 `Health.Current` 是在 `Health.Awake()` 里才初始化的（`Current = maxHealth`）。
>
> **Unity 不保证同一帧内不同组件的 `Awake` 执行顺序。** 如果 `CharacterStats.Awake` 排在 `Health.Awake` 前面，那么执行到这里时 `Current` 还是 `0`：
>
> ```
> ratio   = 0 / 100 = 0
> Current = 140 × 0 = 0
> ```
>
> 角色出生即死。而且因为顺序不固定，这个 bug **可能在某些运行下不出现**，是最难查的那一类。
>
> **解法**：把 `SyncMaxHealthToHealth()` 从 `Awake()` 移到 `Start()`。
>
> Unity 对 `Start` 有明确保证：**所有对象的 `Awake()` 全部执行完毕，才会开始执行任何 `Start()`**。所以放在 `Start` 里，`Health.Current` 一定已经初始化好了。
>
> **总结成一条规则**：跨组件的初始化，只要需要读「别的组件在它自己的 `Awake` 里才建好的状态」，就放到 `Start`。

### 坑 2 · Unity 要求文件名和类名完全一致

> **现象**：Day 4 把这个文件从 `PlayerStats.cs` 改名为 `CharacterStats.cs` 的过程中，Unity 短暂报错；改完文件名的瞬间报错消失。
>
> **根因**：Unity 要求 **MonoBehaviour 的文件名必须和里面的类名完全一致**——它靠这个规则在编译时把脚本文件和组件对应起来。只改一边，另一边就找不到对方。
>
> **解法**：**两件事必须都做**：
>
> 1. 改文件里的 `public class XXX`（第 10 行）
> 2. 在 Project 窗口里按 `F2` 改文件名
>
> 顺序无所谓，中间必然有一个短暂的报错状态，改完两边就恢复。
>
> > **关键：不用担心场景里的引用断掉。**
> >
> > Unity 靠同名的 `.meta` 文件里的 **GUID** 记录「Player 身上的这个组件是哪个脚本」。重命名文件**不会改变 GUID**，所以场景和预制体里的引用会原样保留。
> >
> > 真正会让引用断掉的操作是「删除旧文件 + 新建一个文件」——那样会生成新的 GUID，Player 身上的组件会变成 `Missing (Mono Script)`。**所以改名要用重命名，不要用删了重建。**
>
> **为什么值得改这个名**：Day 4 做敌人时发现，敌人也需要攻击力、移动速度、最大生命——它需要的东西和玩家**一模一样**。如果留着 `PlayerStats` 这个名字，就只能在「给敌人复制一份 `EnemyStats`（同样的代码写两遍）」和「让敌人硬编码数值（后面加精英怪、Boss 时全都要重写）」之间二选一。
>
> **第二个使用者出现时，就是把抽象提到正确名字的时机。** 当时项目里只有 `PlayerController` 和 `MeleeAttacker` 两处引用，改起来 2 分钟；等到有 3 种敌人 + 1 个 Boss 时，就是 2 小时。

---

## 七、如果要改，改这里

### ① 让装备能给角色加属性（第 3 周要做的事）

接口已经全部准备好了，你只需要在装备的代码里调两个方法：

```csharp
// 穿上装备时
public void Equip(CharacterStats stats)
{
    stats.AddModifier(new StatModifier(StatType.Attack, ModifierMode.Flat, 5f), this);
    stats.AddModifier(new StatModifier(StatType.MaxHealth, ModifierMode.PercentAdd, 0.1f), this);
}

// 卸下装备时 —— 一句就够
public void Unequip(CharacterStats stats)
{
    stats.RemoveAllFrom(this);
}
```

**注意二点：**

1. **`this` 就是来源。** 把装备组件自己传进去，卸载时就能精确撤回。
2. **不要在装备里自己算数值。** 装备只负责「声明自己加什么」，算总账是 `CharacterStats` 的事。

### ② 加一个新的「联动」

目前 `CharacterStats` 只会把最大生命同步给 `Health`。将来可能还需要：

- 移速变化 → 通知 `PlayerController` 更新（其实不用，`PlayerController` 每次读 `Get()` 都拿最新值）
- 攻击力变化 → 刷新 UI 上的数字

做法是照抄 `SyncMaxHealthToHealth` 的模式：

```csharp
private void OnEnable()
{
    Changed += SyncMaxHealthToHealth;
    Changed += RefreshSomethingElse;   // ← 加这一行
}

private void OnDisable()
{
    Changed -= SyncMaxHealthToHealth;
    Changed -= RefreshSomethingElse;   // ← 和上面严格对应
}
```

⚠️ **`OnEnable` 和 `OnDisable` 里加的行必须一一对应**，漏一个就会出现「物体停用后仍被调用」的 `MissingReferenceException`。

### ③ 敌人也想用职业数据（做精英怪 / Boss 时）

现在的设计是「敌人留空 `classData`，走手填值」。如果将来敌人变多了，手填容易配错，可以考虑：

**方案 A（改动最小）**：给敌人也做一个 `ClassData` 资产，比如 `Enemy_Elite.asset`。字段名虽然叫 `className`，但填「精英怪」也没问题——`ClassData` 本质是「一份属性配置表」，不强制是玩家职业。

**方案 B（更正规）**：把 `ClassData` 改名成 `CharacterData`，`className` 改名成 `displayName`，然后玩家和敌人都用它。这样第 14 行的字段名和 Tooltip 也要跟着改。

> **建议**：先走方案 A。它零改动，而且如果将来发现不合适，再走方案 B 也就是一次重命名（照第 6 节坑 2 的方法改，引用不会断）。

### ④ 想做「换职业」功能

`ApplyClass()` 已经写好了，直接调：

```csharp
stats.ApplyClass(newClassData);
```

它会自动完成三件事：改基础值、清空旧修饰符、广播事件（血条会跟着刷新）。

> **注意 `ApplyClass` 会清掉所有修饰符。** 如果换职业时你想保留某些加成（比如「永久强化」类的），那些修饰符的 `source` 需要单独处理——要么换职业后重新加一遍，要么给它们一个「免疫清除」的标记。
>
> 目前没有这种需求，所以保持「全清」最简单也最不容易出错。

### ⑤ 想让某个属性「不算加成」

比如「血条 UI 要显示基础最大生命，不显示装备加成后的」——**不建议这么做。**

血条应该显示 `Get(StatType.MaxHealth)`，也就是实际生效的上限。否则玩家会看到「血条满了但实际还能再加血」这种矛盾画面。

如果确实需要「原始值」，在 `Get()` 旁边加一个只读的 `GetBase(StatType type)`：

```csharp
public float GetBase(StatType type) => _baseValues.TryGetValue(type, out var b) ? b : 0f;
```

但用之前先问自己一句：**这个「原始值」在游戏里有玩家能感知的意义吗？** 如果没有，就不要暴露它——多一个出口，就多一条别人能用错的路。
