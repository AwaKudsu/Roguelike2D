# 01 · StatTypes

## 一、头部信息表

| 项 | 内容 |
|---|---|
| 文件路径 | `D:\Roguelike2D\Assets\_Project\Scripts\Core\StatTypes.cs` |
| 所属层 | ① 数据层 |
| 依赖 | 无（纯 C#，不依赖任何其他脚本，也不依赖 Unity 引擎） |
| 被谁依赖 | `CharacterStats`；将来还有 `EquipmentData`、`SkillData`、`UpgradeData` |
| 行数 | 54 行 |
| 最后更新 | Day 4 |

---

## 二、一句话定位

定义「游戏里有哪些属性」和「属性怎么叠加」的两张清单。

---

## 三、为什么需要它

假设没有这个文件。你想让角色有一个攻击力，最直接的写法是在 `CharacterStats` 里放一个字段：

```csharp
public float attack = 10f;
```

然后装备要加 5 点攻击力，你得再写一个方法：

```csharp
public void AddAttack(float v) { attack += v; }
```

到这里还撑得住。但接下来：暴击率、移动速度、最大生命、防御、冷却缩减……**每加一个属性，就要多写一个字段、一个 `Add` 方法、一个 `Remove` 方法**。八个属性就是 24 个成员。

更麻烦的是「怎么叠加」这件事。装备写「+5 攻击力」，技能写「+20% 攻击力」，强化写「×1.15 攻击力」——**这是三种完全不同的数学运算**。如果不在类型层面把它们区分开，你就只能约定「所有加成都是加法」，那「+20%」就退化成了「+0.2 点攻击力」，构筑系统直接失去意义。

`StatTypes` 用两个 `enum` 加一个 `struct` 把这两件事一次说清：

| 类型 | 回答的问题 |
|---|---|
| `StatType` | 改**哪个**属性？ |
| `ModifierMode` | **怎么**改？ |
| `StatModifier` | 一条完整的加成记录（哪个属性 + 怎么改 + 改多少 + 谁加的） |

有了这三样，`CharacterStats` 才能把「收集全部加成、算出最终值」写成一个**通用方法**——以后加属性、加装备、加技能，它一行都不用改。

---

## 四、代码全解

### 块 1 · 文件头与 `StatType` 枚举（第 1–17 行）

```csharp
1: using System;
2: 
3: /// <summary>
4: /// 所有可被职业 / 装备 / 技能 / 强化修改的数值。
5: /// 新增一种数值只需要在这里加一项，其他系统一行都不用改。
6: /// </summary>
7: public enum StatType
8: {
9:     MaxHealth,        // 最大生命
10:     Attack,           // 攻击力
11:     MoveSpeed,        // 移动速度
12:     AttackSpeed,      // 攻击速度（1 = 正常）
13:     CritChance,       // 暴击率 0~1
14:     CritMultiplier,   // 暴击伤害倍率，1.5 = 150%
15:     Defense,          // 防御（按比例减伤）
16:     CooldownRate,     // 冷却缩减，0.2 = 冷却减少 20%
17: }
```

| 行 | 代码 | 含义 |
|---|---|---|
| 1 | `using System;` | 引入 `System` 命名空间。这个文件里只用到它的两个特性（Attribute，写在方括号里的注解）：第 37 行的 `[Serializable]` 和第 45 行的 `[NonSerialized]`。不写这行，编译器找不到这两个名字 |
| 3–6 | `/// <summary>...</summary>` | XML 文档注释。三个斜杠不是普通注释——IDE 会把它提取成这个类型的说明文字。效果是：你在别的脚本里输入 `StatType` 时，弹出的提示框里会显示这段话 |
| 7 | `public enum StatType` | 声明一个叫 `StatType` 的**枚举**。`public` 表示整个项目里任何脚本都能用它。枚举本质上就是**一组起了名字的整数** |
| 8 | `{` | 枚举体的开始 |
| 9 | `MaxHealth,` | 第一个枚举成员，注释说明它代表「最大生命」。成员之间用逗号分隔 |
| 10 | `Attack,` | 攻击力。所有伤害计算的基数，`MeleeAttacker` 会读它 |
| 11 | `MoveSpeed,` | 移动速度。玩家和敌人的水平最大速度，`PlayerController` 和 `EnemyController` 都会读 |
| 12 | `AttackSpeed,` | 攻击速度**倍率**，不是攻击次数。它是除法因子：`1.4` 表示冷却缩短到原来的 `1 ÷ 1.4 ≈ 0.714`，也就是快了 40% |
| 13 | `CritChance,` | 暴击率，用 `0~1` 的小数表示概率。`0.15` 就是 15% |
| 14 | `CritMultiplier,` | 暴击时的伤害倍率。`1.5` 表示暴击打出 150% 伤害 |
| 15 | `Defense,` | 防御，按**比例**减伤。`0.1` 表示每次受击少受 10% 伤害 |
| 16 | `CooldownRate,` | 冷却缩减。`0.2` 表示技能冷却时间减少 20% |
| 17 | `}` | 枚举体结束。**注意结尾没有分号**——枚举和类一样是「块状声明」，不需要 `;` |

> **枚举成员其实是有整数值的**
>
> 编译器会按书写顺序从 0 开始分配：`MaxHealth` 是 0，`Attack` 是 1，`MoveSpeed` 是 2……一直到 `CooldownRate` 是 7。
>
> 平时你不需要关心这些数字，但有一条**必须记住的规矩**：
>
> **新属性一律加在末尾。** 如果你在 `MoveSpeed` 和 `AttackSpeed` 中间插入一个新属性，它后面所有成员的整数值都会往后挪一位。
>
> 更要紧的是：**绝对不要把 `StatType` 转成整数存进存档**（比如写 `statType: 3`）。以后插一个属性，存档里所有的 3 就都指错了。要存就存名字（`"CritChance"`）。

> **为什么用 `enum`，而不是字符串或整数当属性键？**
>
> 三种写法摆在一起对比：
>
> ```csharp
> stats.Get("Attack");          // ① 字符串
> stats.Get(1);                 // ② 整数
> stats.Get(StatType.Attack);   // ③ 枚举
> ```
>
> | 写法 | 拼错时 | 自动补全 | 改名时 |
> |---|---|---|---|
> | ① 字符串 | **编译器不报错**，运行时返回 0。你会在几小时后才发现攻击力少了一半 | 无 | 全局搜索替换，必漏 |
> | ② 整数 | 能编译，但 `1` 是什么？三个月后你自己都不记得 | 无 | 无法追踪 |
> | ③ 枚举 | **编译期就报错**，根本跑不起来 | 输入 `StatType.` 自动列出全部 8 个 | IDE 重命名功能一次改掉所有引用，一个不漏 |
>
> 第①种最危险，因为它的错误**不在编译期暴露**。这类 bug 的表现是「功能莫名其妙不对」，而你会先去怀疑伤害公式，绕一大圈才找到是一个拼错的字符串。

---

### 块 2 · `ModifierMode` 枚举（第 19–34 行）

```csharp
19: /// <summary>
20: /// 修饰符的叠加方式。三种模式的计算顺序是固定的：
21: /// 先加算 → 再百分比加算求和 → 最后百分比乘算连乘。
22: /// 顺序固定，结果才可预测，玩家才能「算得清」自己的构筑。
23: /// </summary>
24: public enum ModifierMode
25: {
26:     /// <summary>加算：+10 攻击力</summary>
27:     Flat,
28: 
29:     /// <summary>百分比加算：0.2 = +20%，多个同类先求和再作用</summary>
30:     PercentAdd,
31: 
32:     /// <summary>百分比乘算：0.2 = ×1.2，多个同类连乘 —— 这是构筑深度的来源</summary>
33:     PercentMultiply,
34: }
```

| 行 | 代码 | 含义 |
|---|---|---|
| 19–23 | `/// <summary>...</summary>` | 类型级文档注释。**这四行是整份文件里最重要的设计说明**——它规定了三种模式的计算顺序是固定的，理由写在最后一句 |
| 24 | `public enum ModifierMode` | 第二个枚举。它和 `StatType` 是**正交**的两件事：`StatType` 说「改哪个属性」，`ModifierMode` 说「怎么改」 |
| 26 | `/// <summary>加算：+10 攻击力</summary>` | **成员级**文档注释。鼠标悬停在 `ModifierMode.Flat` 上时会显示这段文字 |
| 27 | `Flat,` | **加算**。数值直接加到基础值上。多个 `Flat` 之间就是普通的连加 |
| 29 | `/// <summary>百分比加算：0.2 = +20%...` | 注释里「多个同类先求和再作用」这一句是这个模式与下一个模式的**唯一区别** |
| 30 | `PercentAdd,` | **百分比加算**。三个 `+20%` 加起来是 `+60%`，作用一次，结果是 `1.6` 倍 |
| 32 | `/// <summary>百分比乘算：0.2 = ×1.2，多个同类连乘...` | 「连乘」和「构筑深度的来源」这两句点明了它的用途 |
| 33 | `PercentMultiply,` | **百分比乘算**。三个 `+20%` 各自乘一次：`1.2 × 1.2 × 1.2 = 1.728` 倍 |
| 34 | `}` | 枚举体结束 |

> **为什么三种模式的计算顺序必须固定？**
>
> 假设玩家同时有「攻击力 +10」和「攻击力 ×1.2」两个加成，基础攻击是 100：
>
> ```
> 先加后乘：(100 + 10) × 1.2 = 132
> 先乘后加：100 × 1.2 + 10 = 130
> ```
>
> **同样两个加成，顺序不同结果就不同。**
>
> 如果顺序不固定——比如取决于你先捡到哪件装备——玩家永远算不清自己这局有多强。而「算得清」正是构筑类游戏的核心乐趣：玩家要能心算「再拿一个 +20% 我就够伤害了」。
>
> 所以 `CharacterStats.Get()` 把顺序**写死**成：
>
> ```
> flat → (1 + percentAdd) → × (1 + percentMultiply)
> ```
>
> 这个顺序与「捡装备的先后」完全无关，同一套装备永远得到同一个数字。

> **`PercentAdd` 和 `PercentMultiply` 到底差在哪？这是整套系统里最值钱的一个区分。**
>
> 假设有 3 个「攻击力 +20%」的加成：
>
> | 全部用哪种模式 | 算式 | 结果 |
> |---|---|---|
> | `PercentAdd` | `1 + 0.2 + 0.2 + 0.2` | **1.6 倍** |
> | `PercentMultiply` | `1.2 × 1.2 × 1.2` | **1.728 倍** |
>
> 数量越多，差距越大。5 个的时候是 `2.0` 倍对 `2.49` 倍。
>
> 这给了你一个设计工具：
>
> - **普通词条用 `PercentAdd`** —— 越多收益越平缓，不会失控
> - **稀有词条用 `PercentMultiply`** —— 越多收益越爆炸，值得玩家为它改变打法
>
> 「构筑深度」听起来很玄，技术上的来源就是这么一行 `*=` 和一行 `+=` 的区别。玩家会为了凑齐同类乘算的词条，放弃更高单件数值的加算词条——**这就是选择，而选择就是策略。**

---

### 块 3 · `StatModifier` 结构体（第 36–54 行）

```csharp
36: /// <summary>一条属性修饰符</summary>
37: [Serializable]
38: public struct StatModifier
39: {
40:     public StatType Type;
41:     public ModifierMode Mode;
42:     public float Value;
43: 
44:     /// <summary>来源标记：卸下装备时靠它精确移除自己加过的所有修饰符</summary>
45:     [NonSerialized] public object Source;
46: 
47:     public StatModifier(StatType type, ModifierMode mode, float value)
48:     {
49:         Type = type;
50:         Mode = mode;
51:         Value = value;
52:         Source = null;
53:     }
54: }
```

| 行 | 代码 | 含义 |
|---|---|---|
| 36 | `/// <summary>一条属性修饰符</summary>` | 类型级文档注释 |
| 37 | `[Serializable]` | 特性。**告诉序列化系统「这个类型可以被保存和还原」**。Unity 只序列化它认识的类型，自定义的 `struct` 必须打上这个标记，才有可能出现在 Inspector 里或被存进 `.asset` 文件 |
| 38 | `public struct StatModifier` | 声明一个**结构体**。注意是 `struct` 不是 `class`——这个选择下面单独讲 |
| 39 | `{` | 结构体体的开始 |
| 40 | `public StatType Type;` | 这条修饰符改的是**哪个属性** |
| 41 | `public ModifierMode Mode;` | 用**什么方式**改 |
| 42 | `public float Value;` | 改**多少**。含义随 `Mode` 变化：`Flat` 时是绝对值，两个百分比模式时是小数（`0.2` = 20%） |
| 43 | （空行） | 空行把「数据」和「来源标记」分成两组，纯粹为了可读性 |
| 44 | `/// <summary>来源标记...` | 说明 `Source` 的用途。这句话直接对应 `CharacterStats.RemoveAllFrom()` 的实现 |
| 45 | `[NonSerialized] public object Source;` | 来源标记。`object` 表示「可以是任何东西」；`[NonSerialized]` 告诉 Unity **不要尝试保存这个字段**。原因见下方 |
| 47 | `public StatModifier(StatType type, ModifierMode mode, float value)` | **构造函数**。名字和结构体同名，没有返回类型。调用 `new StatModifier(StatType.Attack, ModifierMode.Flat, 5f)` 就会执行它 |
| 49 | `Type = type;` | 把参数 `type` 赋给字段 `Type`。**大小写不同不是笔误**——参数小写、字段大写，这样编译器和你都能分清谁是谁 |
| 50 | `Mode = mode;` | 同上 |
| 51 | `Value = value;` | 同上 |
| 52 | `Source = null;` | 构造函数**不接收**来源，统一先置空。来源由 `CharacterStats.AddModifier()` 的第二个参数单独指定 |
| 53 | `}` | 构造函数结束 |
| 54 | `}` | 结构体结束 |

> **为什么是 `struct` 而不是 `class`？**
>
> 三个理由，按重要性排：
>
> **① 它只是三个数字。** 没有行为、没有继承、不需要多态。这正是 `struct` 的典型适用场景。C# 的官方建议是：如果类型满足「逻辑上表示一个单一值」「实例很小」「不需要继承」，就优先用 `struct`。
>
> **② 值语义更安全。** `struct` 赋值时是**复制**。你把一个 `StatModifier` 传给方法，方法内部怎么改都不会影响你手上这一个。用 `class` 的话传的是引用，方法内部一改，你这边也跟着变——这种「隔空改数据」的 bug 很难查。
>
> **③ 性能。** `List<StatModifier>` 存 `struct` 时，所有数据是**连续排列**在内存里的，遍历时 CPU 缓存命中率高。用 `class` 的话每个元素都是堆上独立分配的对象，遍历要跳内存，还会产生垃圾回收（GC）压力。而 `CharacterStats.Get()` 是每帧都会被调用多次的热路径。
>
> **代价你得知道：** `struct` 是值类型，所以下面这种写法**改的是副本，无效**：
>
> ```csharp
> _modifiers[type][0].Source = something;   // ❌ 改的是临时副本
> ```
>
> 这就是为什么 `CharacterStats.AddModifier()` 里必须先 `modifier.Source = source;` **再** `list.Add(modifier);`——顺序反了，来源标记就丢了。

> **`[NonSerialized]` 为什么省不掉？**
>
> `Source` 的类型是 `object`，它可以指向任何东西：一个装备组件、一个 `ScriptableObject`、一个字符串。
>
> **Unity 的序列化器不支持 `object`。** 它面对一个 `object` 引用时不知道该把这个引用存成什么格式——是存成本地文件 ID？还是存成 GUID？还是存成一串数字？它没有答案。
>
> 加 `[NonSerialized]` 是明确声明：**这个字段只活在运行时的内存里，不要尝试保存它。**
>
> 后果是：游戏一退出，所有 `Source` 全部变成 `null`。这完全没问题——修饰符本来就该在每次进入游戏时重新加上（装备重新穿、技能重新学），本来就不需要跨存档保存。

> **构造函数为什么不接收 `source` 参数？**
>
> 因为「构造一条修饰符」和「指定它的来源」是两件独立的事。
>
> 构造说的是「攻击力 +5，用加算」——这是修饰符**自己**的属性。
> 来源说的是「这条是铁剑加的」——这是修饰符**被谁使用**的上下文。
>
> 分开之后，同一把剑可以加好几条修饰符、共用一个来源：
>
> ```csharp
> var sword = this;   // 假装这是铁剑的组件
> stats.AddModifier(new StatModifier(StatType.Attack,     ModifierMode.Flat, 5f),   sword);
> stats.AddModifier(new StatModifier(StatType.CritChance, ModifierMode.Flat, 0.1f), sword);
> ```
>
> 卸下剑的时候，一句 `stats.RemoveAllFrom(sword)` 就能把这两条一起清掉——不需要记住「我加过几条、分别是什么」。

---

## 五、在 Unity 里怎么配

**本脚本不需要挂在任何物体上。**

它没有继承 `MonoBehaviour`，只是三个纯 C# 类型的定义（两个 `enum` + 一个 `struct`）。打开 Unity 时它会被编译进 `Assembly-CSharp.dll`，但你在 Hierarchy 面板和 Inspector 面板里**永远找不到它**——因为它不是组件，只是一种「用来描述数据的形状」。

**怎么验证它正常工作了**：随便打开一个脚本，输入 `StatType.`，IDE 应该弹出 8 个候选（`MaxHealth`、`Attack`……）。如果弹不出来，说明这个文件有编译错误，去 Console 面板看红字。

---

## 六、踩过的坑

暂无。

这个文件里全是类型定义，没有任何运行时行为——它不执行、不计算、不访问其他对象，所以它本身不会「运行出错」。它的错误只会在**编译期**暴露（比如少写了逗号、用了不存在的类型名），而编译错误是挡在运行之前的，你根本进不去游戏。

真正会出问题的是**使用它的代码**，那些坑记在 `02-CharacterStats.md`。

---

## 七、如果要改，改这里

### ① 加一个新属性

比如要加「幸运值（Luck）」，完整步骤是**四步**，但只有第一步在这个文件里：

**第 1 步（本文件）**：在 `StatType` 的**末尾**加一项——

```csharp
    CooldownRate,     // 冷却缩减，0.2 = 冷却减少 20%
    Luck,             // 幸运值，影响掉落品质
}
```

⚠️ **一定要加在末尾，不要插在中间。** 插在中间会让后面所有成员的整数值往后挪一位（见块 1 的警告）。

**第 2 步**：`CharacterStats.WriteFallbackBaseValues()` 里加一行，给敌人用的手填值一个默认：

```csharp
_baseValues[StatType.Luck] = baseLuck;
```

（这需要在 `CharacterStats` 里也加一个 `baseLuck` 字段。）

**第 3 步**：`CharacterStats.ApplyClassData()` 里加一行，让职业能配置它：

```csharp
_baseValues[StatType.Luck] = data.luck;
```

（这需要在 `ClassData` 里也加一个 `public float luck;` 字段。）

**第 4 步**：**真正去读它**。这一步最容易被忘——

```csharp
float luck = stats.Get(StatType.Luck);
```

> ⚠️ **在枚举里加一个属性，只是「允许别人设置它」。要让它真正生效，还必须有一个消费者去读它。**
>
> 这个项目上真的发生过一次：`StatType.Defense` 第一天就在枚举里了，`Class_Warrior` 的 `Defense` 也填了 `0.1`，但一开始没有任何代码读它——战士挨打时掉的血和法师一模一样。直到在 `Health.TakeDamage()` 里加上三行读 `Defense` 的代码，防御才真正生效。
>
> 好消息是第 3 步和第 4 步漏了都不会报错，只是功能静默失效。所以加完属性之后，**手动去游戏里验证一次**。

### ② 改叠加公式

公式不在这个文件里，在 `CharacterStats.Get()` 的第 84 行：

```csharp
return flat * (1f + percentAdd) * percentMul;
```

常见的改法：

| 想做的事 | 怎么改 |
|---|---|
| 让百分比加算也变成连乘 | 把第 77 行改成 `percentMul *= 1f + m.Value;`，同时删掉 `percentAdd` 变量 |
| 加一个「最终伤害倍率」的全局修正 | 在第 84 行末尾再乘一个系数 |
| 给某个属性设上下限 | 在 `return` 之前套一层 `Mathf.Clamp`（注意：暴击率必须限在 0~1，否则会出现「必然暴击」） |

### ③ 加一种新的叠加模式

比如要加「先加算、但乘算只作用一次」之类的第四种模式：

1. 在 `ModifierMode` 末尾加一项，比如 `Override`（直接覆盖）
2. **必须**去 `CharacterStats.Get()` 的 `switch`（第 74–79 行）里加一个 `case`，否则新的模式会被静默忽略——`switch` 里没有匹配的 `case` 时什么都不做，不报错

> 这类「加了枚举值但忘了加 `case`」的问题，编译器在 C# 里**不会**警告（除非你用上了分析器）。养成习惯：**改完 `enum`，全局搜索这个枚举类型名，看有哪几个 `switch` 需要跟着改。**

### ④ 让 `StatModifier` 支持「条件触发」的加成

比如「血量低于 30% 时攻击力 +20%」这类词条。

**不要在 `StatModifier` 里加条件判断**——它应该是纯数据，一旦塞进逻辑，`Get()` 就会变成一个又大又慢的判断机器，而且每帧都要重新求值。

正确做法是：**用一个单独的系统在条件满足时 `AddModifier`，条件不满足时 `RemoveAllFrom`**。这样 `StatModifier` 和 `CharacterStats` 一行都不用改。

（这也正是 `Source` 参数存在的意义——那个「条件系统」把自己当作 source，随时能干净地撤回自己加过的所有加成。）
