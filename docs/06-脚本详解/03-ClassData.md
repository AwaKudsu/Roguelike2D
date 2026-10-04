# 03 · ClassData

## 一、头部信息表

| 项 | 内容 |
|---|---|
| 文件路径 | `D:\Roguelike2D\Assets\_Project\Scripts\Core\ClassData.cs` |
| 所属层 | ① 数据层 |
| 依赖 | `AttackData`（第 40 行的 `basicAttack` 字段） |
| 被谁依赖 | `CharacterStats`（`Awake()` 和 `ApplyClass()` 两处读它填充基础属性） |
| 行数 | 41 行 |
| 最后更新 | Day 4 |

---

## 二、一句话定位

一个职业的全部数值配置，以数据文件的形式存在磁盘上。

---

## 三、为什么需要它

假设没有这个文件。战士和法师的区别只能写在代码里：

```csharp
public class PlayerController : MonoBehaviour
{
    public bool isMage;

    private float MaxHealth => isMage ? 85f  : 140f;
    private float Attack    => isMage ? 11f  : 13f;
    private float MoveSpeed => isMage ? 7.5f : 8f;
    // ...还有 5 个属性要这样写两遍
}
```

三个问题，一个比一个严重：

**① 加第三个职业时，每个 `?:` 都要改成三路判断。**

五个属性 × 三路判断，然后是四个职业、五个职业。等你做到第三个职业，这段代码已经没人愿意碰了。

**② 调数值要改代码、重新编译、重进游戏。**

你觉得战士血量 140 太肉，想试试 120——改代码、切回 Unity、等它编译（几秒到几十秒）、进游戏测。一轮 30 秒，而调平衡往往要试十几轮。**开发效率直接腰斩。**

**③ 数值和逻辑混在一起，「职业」这个概念根本看不出来。**

答辩时老师问「你的职业系统是怎么设计的」，你指着一堆 `?:` 说不清楚。

`ClassData` 把职业抽成一份**数据文件**：

```
Class_Warrior.asset  →  { maxHealth: 140, attack: 13, moveSpeed: 8, ... }
Class_Mage.asset     →  { maxHealth: 85,  attack: 11, moveSpeed: 7.5, ... }
```

于是：

| 想做的事 | 没有 `ClassData` | 有 `ClassData` |
|---|---|---|
| 加第三个职业 | 改 5 处三路判断 + 编译 | 右键新建一个文件 + 填数字 |
| 把战士血量从 140 调到 120 | 改代码 + 编译 + 重进游戏 | Inspector 里改一个数字，**运行中就能看到效果** |
| 让老师看「职业系统在哪」 | 指着 `if/else` 解释 | 打开 `Data/Classes` 文件夹，两个文件一目了然 |

**这就是「数据驱动」（data-driven）在游戏开发里的具体含义**——也是简历上那条「数据驱动的配置系统」的真实所指。

### 它在整个架构里的位置

```
Class_Warrior.asset  ┐
Class_Mage.asset     ├──▶ CharacterStats.ApplyClassData() ──▶ _baseValues 字典 ──▶ Get()
Class_???（将来）     ┘
```

`ClassData` **只负责「说」**（我是多少血、多少攻击），`CharacterStats` **负责「做」**（把数字存起来、算加成）。这份文件里没有一行 `if`、没有一个循环，是纯粹的数据。

---

## 四、代码全解

因为这份文件只有 41 行，而且大部分是字段声明，下面按代码里的 `[Header]` 分节，分成四块讲。

### 块 1 · 文件头与类声明（第 1–11 行）

```csharp
1: using UnityEngine;
2: 
3: /// <summary>
4: /// 一个职业的全部配置。
5: ///
6: /// 它是纯数据 —— 没有任何逻辑，所以新增职业不需要写一行代码，
7: /// 只要在 Project 窗口右键 Create 一个新资产。
8: /// </summary>
9: [CreateAssetMenu(fileName = "Class_", menuName = "Roguelike/Class Data")]
10: public class ClassData : ScriptableObject
11: {
```

| 行 | 代码 | 含义 |
|---|---|---|
| 1 | `using UnityEngine;` | 引入 Unity 引擎命名空间。要用到 `ScriptableObject`、`Sprite`、`CreateAssetMenu`、`Header`、`Tooltip`、`Range`、`TextArea` |
| 3–8 | `/// <summary>...</summary>` | 类型级文档注释。**第 6–7 行是整份文件的核心说明**——「新增职业不需要写一行代码」，这句话就是 `ClassData` 存在的全部理由 |
| 9 | `[CreateAssetMenu(fileName = "Class_", menuName = "Roguelike/Class Data")]` | 见下方逐块拆解 |
| 10 | `public class ClassData : ScriptableObject` | 冒号表示**继承**。见下方逐块拆解 |
| 11 | `{` | 类体开始 |

#### 逐块拆解：第 9 行

```csharp
[CreateAssetMenu(fileName = "Class_", menuName = "Roguelike/Class Data")]
```

这是一个**特性**（Attribute），写在方括号里的注解。它挂在类上，告诉 Unity 编辑器一件事：**「在右键 Create 菜单里，给我加一个入口。」**

| 参数 | 值 | 作用 |
|---|---|---|
| `fileName` | `"Class_"` | 新建出来时**默认的文件名前缀**。所以刚创建好时它叫 `Class_`，需要你按 `F2` 改名 |
| `menuName` | `"Roguelike/Class Data"` | 菜单路径。**斜杠 `/` 表示子菜单**——右键 Create → `Roguelike` → `Class Data` |

> **这一行是怎么凭空造出一个菜单项的？**
>
> 你在 Project 窗口右键时，Unity 编辑器会**扫描项目里所有的脚本**，找出所有标了 `[CreateAssetMenu]` 的类，把它们注册进 Create 菜单。
>
> 所以菜单里**不会**有 `Roguelike` 这一项，直到：
>
> 1. 这个 `.cs` 文件存在
> 2. **并且 Unity 编译完了它**
>
> **编译没完成之前，菜单里什么都看不到。** 这是新手最容易卡住的地方——你以为 Unity 出问题了，其实它还在编译。判断方法：Unity 编辑器右下角有个小圆圈，转完才表示编译结束；或者打开 Console 面板看有没有红色报错。

#### 逐块拆解：第 10 行

```csharp
public class ClassData : ScriptableObject
```

| 部分 | 含义 |
|---|---|
| `public` | 整个项目里任何脚本都能用 |
| `class ClassData` | 类名是 `ClassData`。**它必须和文件名完全一致**（`ClassData.cs`），这是 Unity 对脚本的硬性要求 |
| `: ScriptableObject` | 继承自 `ScriptableObject` |

> **`ScriptableObject` 到底是什么？**
>
> 它是 Unity 提供的一种基类，用来做「**存在磁盘上的数据容器**」。和它对比的是 `MonoBehaviour`：
>
> | | `MonoBehaviour` | `ScriptableObject` |
> |---|---|---|
> | 住在哪 | **挂在 Hierarchy 里的物体上** | **躺在 Project 窗口里，是一个文件** |
> | 有几个实例 | 场景里有几个物体就有几份 | 项目里有几个文件就有几份 |
> | 有 `Transform` 吗 | 有（位置、旋转、缩放） | **没有**——它不是一个「东西」，是一张表 |
> | 能写 `Awake`/`Update` 吗 | 能 | **不能**（没有生命周期） |
> | 例子 | `CharacterStats`、`Health` | `ClassData`、`AttackData` |
>
> **一句话区分**：`MonoBehaviour` 描述「一个东西是什么样」，`ScriptableObject` 描述「一份配置写了什么」。
>
> 因为 `ClassData` 只是一张表，所以它**不需要挂在任何物体上**——你在 Hierarchy 里永远找不到 `Class_Warrior`。它躺在 `Assets/_Project/Data/Classes/Class_Warrior.asset` 这个文件里，`Player` 身上的组件只是**引用**了它。

---

### 块 2 · 展示信息（第 12–17 行）

```csharp
12:     [Header("展示")]
13:     public string className = "新职业";
14:     public Sprite icon;
15: 
16:     [TextArea(2, 4)]
17:     public string description;
```

| 行 | 代码 | 含义 |
|---|---|---|
| 12 | `[Header("展示")]` | 在 Inspector 里画一条**粗体分组标题**，写着「展示」。纯装饰，不影响任何逻辑，只是让面板更好读 |
| 13 | `public string className = "新职业";` | 职业的**显示名**。`= "新职业"` 是**默认值**——新建资产时这个框里会预填「新职业」，提醒你要改 |
| 14 | `public Sprite icon;` | 职业图标。类型是 `Sprite`（2D 图片），所以 Inspector 里会显示成一个**资产槽**，等着你从 Project 窗口拖一张图进去。现在还没有美术资源，**留空即可** |
| 16 | `[TextArea(2, 4)]` | 把下一个 `string` 在 Inspector 里显示成**多行文本框**，最少 2 行高、最多 4 行高（超过 4 行会出现滚动条）。不加这个特性的话，`string` 只是一个窄窄的单行框，写不下职业介绍 |
| 17 | `public string description;` | 职业描述。给玩家看的文字，目前还没有 UI 显示它，但先把字段留着——**加字段的成本是 1 行，以后补字段要改代码** |

> **`className` 和文件名要区分开**
>
> | | 例子 | 给谁看 |
> |---|---|---|
> | `className`（第 13 行） | `战士` | **玩家**（将来显示在选人界面） |
> | 文件名 | `Class_Warrior` | **文件系统和 git**（英文，无空格，方便在代码和路径里引用） |
>
> 两者可以不一致，也**建议**不一致——中文名会随语言调整（说不定要出英文版），而文件名一旦改了就会在 git 里留下重命名记录，越晚改越麻烦。
>
> **所以：文件名在创建时就定好，永不再改；显示名随便调。**

---

### 块 3 · 基础属性（第 19–37 行）

```csharp
19:     [Header("基础属性")]
20:     public float maxHealth      = 100f;
21:     public float attack         = 10f;
22:     public float moveSpeed      = 8f;
23: 
24:     [Tooltip("攻击速度倍率。1 = 正常，1.4 = 冷却缩短到 71%")]
25:     public float attackSpeed    = 1f;
26: 
27:     [Range(0f, 1f)]
28:     public float critChance     = 0.05f;
29: 
30:     public float critMultiplier = 1.5f;
31: 
32:     [Tooltip("按比例减伤，0.1 = 减少 10% 伤害")]
33:     [Range(0f, 0.8f)]
34:     public float defense        = 0f;
35: 
36:     [Range(0f, 0.8f)]
37:     public float cooldownRate   = 0f;
```

| 行 | 代码 | 含义 |
|---|---|---|
| 19 | `[Header("基础属性")]` | 第二条分组标题。**上面是「给人看的」，这里开始是「真正影响战斗的」** |
| 20 | `maxHealth = 100f` | 最大生命。会被 `CharacterStats` 同步给 `Health` 组件 |
| 21 | `attack = 10f` | 攻击力。`MeleeAttacker` 伤害公式的基数 |
| 22 | `moveSpeed = 8f` | 移动速度。`PlayerController` 读它作为水平最大速度 |
| 24 | `[Tooltip("攻击速度倍率。1 = 正常，1.4 = 冷却缩短到 71%")]` | 鼠标悬停提示。**这个 Tooltip 特别重要**——`attackSpeed` 的语义反直觉，必须写清楚 |
| 25 | `attackSpeed = 1f` | 攻击速度**倍率**。见下方专项说明 |
| 27 | `[Range(0f, 1f)]` | 显示成**滑条**，拖动范围 0~1。因为暴击率是概率，超过 100% 没有意义 |
| 28 | `critChance = 0.05f` | 暴击率。`0.05` = 5% |
| 30 | `critMultiplier = 1.5f` | 暴击伤害倍率。`1.5` = 暴击造成 150% 伤害。**没有加 `[Range]`**，因为倍率理论上可以无限高（有些游戏有 ×10 的暴击） |
| 32 | `[Tooltip("按比例减伤，0.1 = 减少 10% 伤害")]` | 悬停提示。用具体数字说明「按比例」是什么意思 |
| 33 | `[Range(0f, 0.8f)]` | 滑条，范围 **0~0.8**。上限是 0.8 而不是 1，理由见下方 |
| 34 | `defense = 0f` | 防御。默认 0，表示不减伤 |
| 36 | `[Range(0f, 0.8f)]` | 同样的上限 |
| 37 | `cooldownRate = 0f` | 冷却缩减。默认 0 |

#### `attackSpeed` 的取值含义

这个字段最容易配错，单独说清楚。

它**不是「每秒攻击几次」**，而是一个**除法因子**。实际冷却时间的算法在 `MeleeAttacker.GetCooldown()`：

```
实际冷却 = 基础冷却 ÷ attackSpeed × (1 - cooldownRate)
```

| `attackSpeed` | 实际冷却 | 说明 |
|---|---|---|
| `1` | 基础冷却 × 1 | 正常速度 |
| `1.4` | 基础冷却 × `1/1.4 ≈ 0.714` | **缩短到 71%，也就是快了 40%** |
| `0.95` | 基础冷却 × `1/0.95 ≈ 1.053` | 比正常**慢** 5% |

> **为什么用除法而不是直接写「冷却倍率」？**
>
> 因为「攻击速度 +40%」是玩家的直觉说法。玩家看到「攻速 +40%」，期待的是「打得更快了」，而不是「冷却数字变了」。
>
> 用 `attackSpeed` 当分子，`+40%` 就是填 `1.4`——**和玩家看到的一致**。如果反过来存「冷却倍率 0.714」，装备写「攻速 +40%」时你得填 `0.714`，每次都要心算一次倒数，迟早填错。

#### `[Range(0f, 0.8f)]` 的上限为什么是 0.8

`defense` 和 `cooldownRate` 都是「按比例减少」的量，如果上限是 `1`：

| 情况 | 后果 |
|---|---|
| `defense = 1` | 减少 100% 伤害 → **完全免疫**，游戏失去挑战 |
| `cooldownRate = 1` | 冷却减少 100% → **技能无冷却**，可以无限连发 |

两者都会让游戏直接失去平衡。0.8 意味着**最多减伤 80%、最多减冷却 80%**——强力但不至于破坏游戏。

> ⚠️ **`[Range]` 只限制 Inspector 里的输入，不限制代码。**
>
> 代码里仍然可以写 `classData.defense = 5f;`，编译器不会拦你。所以 `Health.TakeDamage()` 里还要再夹一层：
>
> ```csharp
> float reduction = Mathf.Clamp(_stats.Get(StatType.Defense), 0f, 0.8f);
> ```
>
> **两道防线**：Inspector 的 `[Range]` 防「手滑填错」，代码里的 `Mathf.Clamp` 防「逻辑算出来的值超界」。数值系统里，凡是「比例」都要在消费端再做一次夹取——因为你无法保证所有写入路径都守规矩。

---

### 块 4 · 初始配置（第 39–41 行）

```csharp
39:     [Header("初始配置（技能系统上线后启用）")]
40:     public AttackData basicAttack;
41: }
```

| 行 | 代码 | 含义 |
|---|---|---|
| 39 | `[Header("初始配置（技能系统上线后启用）")]` | 分组标题。**括号里明确标注了「还没启用」**——这是给未来的自己看的提示，避免看到空槽位以为配漏了 |
| 40 | `public AttackData basicAttack;` | 职业的初始攻击方式。类型是 `AttackData`（也是一个 `ScriptableObject`），所以 Inspector 里会显示成一个**只接受 `AttackData` 的资产槽** |
| 41 | `}` | 类体结束 |

> **`basicAttack` 现在是「占位」，还没有任何代码读它。**
>
> 目前的攻击流程是：`MeleeAttacker` 上有一个 `attackData` 字段，直接在 Inspector 里拖了 `Attack_Basic`。**也就是说，现在战士和法师用的是同一套攻击数据**——只有数值不同。
>
> 等第 5 周做技能系统时，`basicAttack` 才会真正生效，流程会变成：
>
> ```
> CharacterStats.Class.basicAttack  →  MeleeAttacker 读取当前职业的初始攻击
> ```
>
> 到那时候，法师的 `basicAttack` 可以指向一个「远程火球」的 `AttackData`，而战士指向「近战挥砍」——**两个职业的攻击方式才会真正长得不一样**。
>
> **先把字段留着的价值**：等做技能系统时，你不需要回来改 `ClassData`、重新创建两个职业资产、重新填一遍数值。**加一个字段是 1 行代码，漏一个字段是返工一整套流程。**

> **为什么这个文件里全是 `public`，而 `CharacterStats` 里全是 `[SerializeField] private`？**
>
> 这不是风格不统一，是有原因的：
>
> | | `ClassData`（数据资产） | `CharacterStats`（组件） |
> |---|---|---|
> | 写法 | `public float maxHealth;` | `[SerializeField] private float baseMaxHealth;` |
> | 谁需要访问 | 只有 `CharacterStats.ApplyClassData()`，一次性读取 | 战斗代码要在运行时反复读，**而且必须走 `Get()`** |
> | 为什么 | 它**本身就是一张数据表**，字段就是它的全部意义。包一层 getter 没有任何价值，只会让代码变长 | 如果把基础值暴露出去，别人可能直接读到「**没算加成的原始值**」——那是错的。藏起来，逼所有人走 `Get()` |
>
> **判断标准是「这个字段如果被别人直接改，会不会出事」：**
>
> - `ClassData.maxHealth = 200` —— 出事吗？不出事，这就是「改配置」，本来就该允许
> - `CharacterStats.baseAttack = 200` —— 出事。它绕过了修饰符系统，还会和 `classData` 里的职业值打架，而且不会触发 `Changed` 事件（血条不会刷新）
>
> 一句话：**纯数据的类字段敞开，带逻辑的类字段藏起来。**

---

## 五、在 Unity 里怎么配

这一节是实操步骤。如果你对 Unity 界面还不熟，按顺序做即可。

### 前置检查：确认菜单项已经出现

`[CreateAssetMenu]` 那一行（第 9 行）之所以能让右键菜单多出一项，靠的是 Unity 编辑器**扫描所有脚本**。所以必须先确认编译完成：

1. 打开 **Console 面板**（`窗口 → 常规 → 控制台`，快捷键 `Ctrl+Shift+C`）
2. 确认**没有红色报错**
3. 看编辑器**右下角**的小圆圈——还在转就说明正在编译，等它转完

❌ 如果 Console 有红色报错 → 先修报错，菜单不会出现
❌ 如果右键菜单里找不到 `Roguelike` → 99% 是编译没完成

### 第 1 步：建文件夹

1. **Project 面板** → 展开 `Assets` → `_Project` → `Data`
2. 在**右侧空白区**右键（不是左边的文件夹树上右键）
3. `创建` / `Create` → `文件夹` / `Folder`
4. 文件夹名会高亮，直接输入 `Classes`，回车

> 如果 `Data` 文件夹不存在，就先在 `Assets/_Project` 下建 `Data`，再进去建 `Classes`。

### 第 2 步：新建职业资产

1. 单击 `Classes` 进入它
2. 右键空白处 → `创建` / `Create`
3. 找到 **`Roguelike`** → 鼠标悬停展开子菜单 → 点 **`Class Data`**
4. 创建出来的文件默认叫 **`Class_`**（第 9 行的 `fileName` 决定的）
5. 按 `F2` 改名为 **`Class_Warrior`**，回车

> ⚠️ **一创建就改名。** 等它被 `Player` 引用之后再改名，虽然引用不会断（Unity 靠 `.meta` 里的 GUID 记录引用），但 git 里会多出一堆重命名记录。

### 第 3 步：填 `Class_Warrior` 的数值

单击 `Class_Warrior`，右侧 Inspector 按下面填：

| Inspector 里的字段 | 填 | 对应代码行 |
|---|---|---|
| `Class Name` | `战士` | 第 13 行 |
| `Icon` | 留空 | 第 14 行 |
| `Description` | `高血量高防御的近战职业，攻击慢但一击很重` | 第 17 行 |
| `Max Health` | **140** | 第 20 行 |
| `Attack` | **13** | 第 21 行 |
| `Move Speed` | **8** | 第 22 行 |
| `Attack Speed` | **0.95** | 第 25 行 |
| `Crit Chance` | **0.05** | 第 28 行 |
| `Crit Multiplier` | **1.5**（默认值，不用改） | 第 30 行 |
| `Defense` | **0.1** | 第 34 行 |
| `Cooldown Rate` | **0**（默认值，不用改） | 第 37 行 |
| `Basic Attack` | 拖入 `Attack_Basic` | 第 40 行 |

**填完不需要点保存**——Unity 会自动写进 `.asset` 文件。

### 第 4 步：新建并填 `Class_Mage`

重复第 2~3 步，文件名 `Class_Mage`，数值换成：

| Inspector 里的字段 | 填 |
|---|---|
| `Class Name` | `法师` |
| `Description` | `低血量高攻速的远程职业，靠暴击和冷却缩减压制敌人` |
| `Max Health` | **85** |
| `Attack` | **11** |
| `Move Speed` | **7.5** |
| `Attack Speed` | **1.4** |
| `Crit Chance` | **0.15** |
| `Crit Multiplier` | **1.8** |
| `Defense` | **0** |
| `Cooldown Rate` | **0** |
| `Basic Attack` | 拖入 `Attack_Basic` |

### 第 5 步：挂到 Player 身上

这一步和前面**性质不同**——前面是「资产填资产」，这一步是「**资产填进场景里的组件**」：

1. **Hierarchy 面板**里单击 `Player`
2. Inspector 里往下滚，找到 **`Character Stats`** 组件
3. 把 `Class_Warrior` 从 Project 面板**拖到** `Class Data` 槽

> **想验证职业系统真的生效**：把 `Class Data` 从 `Class_Warrior` 换成 `Class_Mage`，重新运行，玩家血量上限应该从 **140** 变成 **85**。这就是为什么切职业只需要改一个槽位——`CharacterStats` 会在 `Awake()` 里读它。

### Inspector 控件对照表

填的时候你会看到各种不同形状的控件，它们和代码的对应关系是：

| 代码里写的 | Inspector 里显示成 | 本文件里的例子 |
|---|---|---|
| `public float x;` | 数字输入框 | `maxHealth`（第 20 行） |
| `public string x;` | 单行文本框 | `className`（第 13 行） |
| `public Sprite x;` | 资产槽（写着 `None (Sprite)`） | `icon`（第 14 行） |
| `public AttackData x;` | 资产槽（**只接受 `AttackData`**） | `basicAttack`（第 40 行） |
| `[TextArea(2, 4)]` + `string` | **多行**文本框 | `description`（第 17 行） |
| `[Range(a, b)]` + `float` | **滑条** | `critChance` / `defense` / `cooldownRate` |
| `[Header("...")]` | 粗体分组标题 | 4 处 |
| `[Tooltip("...")]` | 鼠标**悬停**时的提示 | `attackSpeed` / `defense` |

> **第 5 行那种槽位是有类型的。** 你把一张 PNG 图片拖到 `Basic Attack` 槽上，Unity **不会让你放**（鼠标显示禁止符号）。这是编辑器级别的类型安全——你不可能把一个 `Sprite` 当成攻击数据用。

### 验证做对了

| 方法 | 应该看到 |
|---|---|
| Project 面板 | `Assets/_Project/Data/Classes` 下有两个文件：`Class_Warrior`、`Class_Mage` |
| 资源管理器 | 右键资产 → `在资源管理器中显示`，能看到 `.asset` **和 `.asset.meta`** 两个文件 |
| Git | `git status --short` 显示 **4 个**新文件（两个 `.asset` + 两个 `.meta`） |

> ⚠️ **`.meta` 必须一起提交。** 它是 Unity 用来记录「这个文件的 GUID」的身份证。只提交 `.asset` 不提交 `.meta`，别人拉下来项目后，`Player` 身上的 `Class Data` 槽会变成 `Missing`。

---

## 六、踩过的坑

### 坑 1 · 运行中改的数值，停止运行后会全部还原

> **现象**：点着 ▶ 运行游戏，在 Inspector 里把 `Class_Warrior` 的 `Attack` 从 13 改成 30，手感立刻变好。停止运行，再点开 `Class_Warrior`——`Attack` 又变回了 13。改了三次都是这样，以为 Unity 没保存。
>
> **根因**：Unity 允许你在运行中修改 Inspector 的值，**但这是「调试模式」的修改**。停止运行时，Unity 会用「进入运行模式那一刻的快照」把所有资产和组件的值**恢复回去**。这是设计行为，不是 bug——否则你调试时随手改的十几次数值会永久污染项目文件。
>
> **解法**：把「运行中试」和「停下来改」分成两步：
>
> 1. 运行游戏，在 Inspector 里试各种数值，找到手感最好的那个
> 2. **记下数字**（拍照或者写便签都行）
> 3. **停止运行**
> 4. 把记下的数字填回去
>
> > **为什么 `ScriptableObject` 也是这个行为？**
> >
> > 因为 `ClassData` 是一个**项目资产**，运行中改它会直接写到磁盘文件上。如果不回滚，你调试一次就永久改了配置。Unity 对资产和组件一视同仁地回滚。
> >
> > **例外**：如果你确实想让运行中的修改保留下来，可以在运行中右键组件标题 → `Copy Component`，停止后 → `Paste Component Values`。但对 `ScriptableObject` 资产，最省事的还是「记下来再填一遍」。

### 坑 2 · 右键菜单里没有 `Roguelike` 这一项

> **现象**：按 `_写作规范.md` 的步骤右键 `Create`，翻遍整个菜单也找不到 `Roguelike` 子菜单，以为代码写错了。
>
> **根因**：`[CreateAssetMenu]` 生成的菜单项，**必须等 Unity 编译完这个脚本之后才会注册**。新建一个 `.cs` 文件之后，Unity 需要几秒钟编译。在这几秒内，菜单里什么都没有——而且**不会有任何提示**告诉你「还在编译」。
>
> 如果 Console 里有**红色报错**，编译根本没成功，菜单项永远不会出现。
>
> **解法**：右键之前先做两步检查：
>
> 1. 打开 **Console 面板**（`Ctrl+Shift+C`），确认没有红色报错
> 2. 看 Unity 编辑器**右下角**的小圆圈——还在转就说明正在编译，等它转完
>
> > **更根本的排查顺序**：菜单项不存在时，**先看 Console，再看代码**。
> >
> > 因为「代码写错了」和「还没编译完」的表现**完全一样**（都是菜单里没有），而前者要花几分钟找，后者只需要等三秒。**先排除成本低的可能**，这个习惯在调试里非常值钱。

---

## 七、如果要改，改这里

### ① 加一个新职业（零行代码）

这是 `ClassData` 存在的全部意义。完整步骤：

1. Project 面板进入 `Assets/_Project/Data/Classes`
2. 右键 → `Create` → `Roguelike` → `Class Data`
3. `F2` 改名为 `Class_Rogue`（或其他）
4. Inspector 里填数值
5. 想做「切换职业」功能时，把新资产拖到 `CharacterStats` 的 `Class Data` 槽

**不用改任何 `.cs` 文件，不用重新编译。**

### ② 给职业加一个新字段

比如要加「幸运值」，让不同职业的掉落品质不同：

**第 1 步（本文件）**：在块 3 的末尾加一行——

```csharp
    [Header("特殊")]
    [Range(0f, 1f)]
    public float luck = 0f;
```

**第 2 步（`CharacterStats.cs`）**：在 `ApplyClassData()` 里加一行，把它搬进属性字典——

```csharp
_baseValues[StatType.Luck] = data.luck;
```

⚠️ 但 `StatType` 里必须**先有** `Luck` 这一项（见 `01-StatTypes.md` 的第 7 节）。

**第 3 步**：**去读它**。没有消费者，这个字段就只是 Inspector 里一个好看的数字。

> **顺序很重要**：先加 `StatType` 枚举项 → 再加 `ClassData` 字段 → 再加 `ApplyClassData` 搬运 → 最后写消费者。漏掉中间任何一步，都是「填了数值但不生效」的静默失败。

### ③ 启用 `basicAttack`（做技能系统时）

目前 `MeleeAttacker` 直接读自己身上的 `attackData` 字段（第 40 行的 `basicAttack` 没被任何人读）。要让它生效，改 `MeleeAttacker`：

```csharp
// 原来是：[SerializeField] private AttackData attackData;
// 改成从职业里取：
private AttackData CurrentAttack =>
    _stats != null && _stats.Class != null && _stats.Class.basicAttack != null
        ? _stats.Class.basicAttack
        : attackData;   // 敌人没有职业，回退到 Inspector 里拖的那个
```

这样：
- **玩家** → 用当前职业的 `basicAttack`（战士挥砍 / 法师火球）
- **敌人** → `Class` 是 `null`，回退到手拖的 `attackData`

> 这就是 `CharacterStats.Class` 那个只读属性（`public ClassData Class => classData;`）准备好的用途——**它一直没人用，就是为了这一天**。

### ④ 改 `defense` / `cooldownRate` 的 0.8 上限

两个地方要一起改：

1. **本文件**第 33 行和第 36 行的 `[Range(0f, 0.8f)]` → 改上限
2. **消费端**的 `Mathf.Clamp`：
   - `Health.TakeDamage()` 里的 `Mathf.Clamp(_stats.Get(StatType.Defense), 0f, 0.8f)`
   - `MeleeAttacker.GetCooldown()` 里的 `Mathf.Clamp(_stats.Get(StatType.CooldownRate), 0f, 0.8f)`

⚠️ **只改 `[Range]` 不改 `Clamp`，上限就是假的**——因为 `Clamp` 会把超界的值夹回去，你在 Inspector 里把滑条拉到 1.0，代码里还是按 0.8 算。

**建议不要改成 1.0。** 完全免疫伤害 / 零冷却都会让游戏失去挑战，而且玩家一旦凑到那个程度，后面所有关卡都没有意义了。**上限的作用不是「限制玩家」，是「保护游戏后期的可玩性」。**

### ⑤ 给职业加技能列表（技能系统上线时）

在文件末尾加：

```csharp
    [Header("技能")]
    public SkillData[] skills;
```

用数组是为了「这个职业有几个技能，由数据决定」——战士可能 3 个，法师可能 4 个，代码不用管。

⚠️ **注意职业资产是「共享」的。** 所有玩家共用同一份 `Class_Warrior.asset`。如果游戏里出现「同一个职业的两个角色，其中一个学了额外技能」，那就不能改这个资产——得用「每个角色一份技能列表」的方式。**目前是单角色，不构成问题，但做多角色/存档时要想清楚。**

### ⑥ 把 `ClassData` 改名成 `CharacterData`（敌人也想用职业配置时）

现在敌人是「留空 `classData`，手填基础值」。如果敌人变多、手填容易配错，可以让敌人也用 `ClassData`——它本质是「一份属性配置表」，不强制是玩家职业。

改动清单：

| 改什么 | 注意 |
|---|---|
| `ClassData.cs` → `CharacterData.cs`（**文件名和类名一起改**） | 靠 `.meta` 的 GUID，引用不会断。**用重命名，不要删了重建** |
| `className` → `displayName` | 只是语义更准确，功能不变 |
| 第 9 行的 `menuName` | 可以改成 `"Roguelike/Character Data"` |
| `CharacterStats` 里的类型引用 | IDE 重命名功能一次改完 |

> **建议先不要改。** 现在只有玩家用 `ClassData`，语义完全准确。**改名是有成本的操作**（要重新验证所有引用、重新创建资产），等真的需要了再改——而且真需要的时候，你会发现这件事只需要 5 分钟。
