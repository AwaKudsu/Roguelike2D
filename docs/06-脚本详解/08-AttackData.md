# 08 · AttackData

## 一、头部信息表

| 项 | 内容 |
|---|---|
| 文件路径 | `Assets/_Project/Scripts/Combat/AttackData.cs` |
| 所属层 | ③ 攻击层（数据资产） |
| 依赖 | 无（只用 `UnityEngine` 本身的类型） |
| 被谁依赖 | `MeleeAttacker`、`EnemyController`；将来的 `SkillSystem`、`Projectile` |
| 行数 | 40 行 |
| 最后更新 | Day 4 |

---

## 二、一句话定位

一次攻击（或一个技能）的「数据表」，也是整个技能系统的接入点。

---

## 三、为什么需要它

### 如果没有它

攻击的数值就必须写死在代码里。`MeleeAttacker.cs` 里会变成这样：

```csharp
float damage = 20f;              // 写死的伤害
float cooldown = 0.35f;          // 写死的冷却
Vector2 boxSize = new Vector2(1.2f, 0.9f);   // 写死的判定范围
```

这一写死，三件坏事会同时发生。

**第一件：所有攻击只能长一样。**
战士的普通攻击、法师的火球、敌人的挥击，都得用同一个伤害、同一个冷却、同一个判定框。因为代码里只有一个数字。想让火球打得远一点？只能复制一份 `MeleeAttacker`，改掉里面的数字，然后**同样的判定逻辑你就有了两份**。以后修一个 bug 要修两遍。

**第二件：养成系统会废掉技能。**
这是更致命的一条。假设写死「一刀 20 点伤害」，玩家靠装备把攻击力从 13 堆到 130，他砍出去还是 20 点。

> 写死的是**结果**，技能就与角色的成长脱钩了。而你这个游戏的核心卖点之一恰恰是「DNF 式的装备养成」。

**第三件：调数值要改代码、要重新编译。**
你想试试「冷却 0.35 秒」和「冷却 0.25 秒」哪个手感好，得停下来改 `.cs` 文件、等 Unity 编译、再运行。一轮 30 秒，试十轮就是 5 分钟。

### 有了它之后

**数值换成倍率，技能就跟着角色一起变强：**

```
伤害 = 攻击力 × damageMultiplier
```

- 战士攻击力 13，倍率 1 → 一刀 13
- 战士装备成型后攻击力 26，倍率 1 → **一刀自动变成 26，你一行代码都不用改**

**数据抽成资产，加技能就不用写代码：**

`AttackData` 是一个 `ScriptableObject`——也就是说它是一个**磁盘上的文件**，不是挂在物体上的组件。加一个新技能时，你在 Project 窗口右键 → Create → 选 `Attack Data`，填几个数字，就完事了。

> ### `AttackData` 是技能系统的接入点
>
> 现在你只有两个 `AttackData` 资产：玩家的 `Attack_Basic` 和敌人的 `Attack_Enemy`。
>
> 但它们**已经是为将来准备的**。等 Day 5 之后加技能时：
>
> | 技能 | 怎么做 | 要改攻击代码吗 |
> |---|---|---|
> | 战士的冲锋 | 新建一个 `AttackData`，把 `hitboxSize` 调大、`knockbackForce` 调高 | 不用 |
> | 法师的火球 | 新建一个 `AttackData`，再写一个 `Projectile.cs` 读它同样的字段 | 不用 |
> | 三连击 | 新建三个 `AttackData`，依次调用 `Execute()` | 不用 |
>
> 这就是总览里那条架构约定「上层依赖下层，反过来不成立」的具体收益：**加内容 = 加数据，不是改代码。**

---

## 四、代码全解

### 4.1 文件头与类型声明（第 1–11 行）

```csharp
1: using UnityEngine;
2:
3: /// <summary>
4: /// 一次攻击 / 一个技能的数据定义。
5: ///
6: /// 这是整个「技能系统」的接入点：
7: /// 普通攻击是一个 AttackData 资产，战士的冲锋、法师的火球也各是一个 AttackData，
8: /// 区别只在数值和判定形状。所以第 2 周加技能时，攻击代码一个字都不用改。
9: /// </summary>
10: [CreateAssetMenu(fileName = "Attack_", menuName = "Roguelike/Attack Data")]
11: public class AttackData : ScriptableObject
```

| 行 | 代码 | 含义 |
|---|---|---|
| 1 | `using UnityEngine;` | 引入 Unity 的命名空间。后面用到的 `ScriptableObject`、`Sprite`、`Vector2`、`LayerMask`、`Header`、`Tooltip`、`Range`、`CreateAssetMenu` 全都在这里面，不写这一行一个都用不了 |
| 3–9 | `/// <summary> ... </summary>` | C# 的 **XML 文档注释**。三个斜杠（不是两个）是特殊语法，编译器会把它提取出来。效果是在别处写 `new AttackData()` 时，IDE 悬停会弹出这段说明。**它不影响运行，纯粹是给人看的** |
| 10 | `[CreateAssetMenu(...)]` | 一个**特性（Attribute）**，用方括号写在类上面。它不改变代码逻辑，只是给 Unity 编辑器下指令：「在右键 Create 菜单里给我加一个入口」。你要先写这一行，才能在 Project 窗口右键新建出 `AttackData` 文件 |
| 10 | `fileName = "Attack_"` | 新建出来的文件**默认名字**。所以第一次创建时它叫 `Attack_`，你要手动改成 `Attack_Basic` |
| 10 | `menuName = "Roguelike/Attack Data"` | 菜单**路径**。斜杠 `/` 表示子菜单，所以实际路径是：右键 → `Create` → `Roguelike` → `Attack Data` |
| 11 | `public class AttackData : ScriptableObject` | 声明一个公开类，**继承 `ScriptableObject` 而不是 `MonoBehaviour`**。「继承」意思是它自动拥有父类的全部能力 |

> ### 为什么是 `ScriptableObject` 而不是 `MonoBehaviour`？
>
> | | `MonoBehaviour` | `ScriptableObject` |
> |---|---|---|
> | 住在哪 | 必须挂在场景里的 GameObject 上 | 是一个**磁盘上的文件**（`.asset`） |
> | 生命周期 | 场景加载时创建，卸载时销毁 | 一直存在，只在被引用时读进内存 |
> | 适合装什么 | **行为**：每帧要跑的逻辑 | **数据**：一堆配置数值 |
> | 例子 | `Health`、`MeleeAttacker` | `AttackData`、`ClassData` |
>
> `AttackData` 里一个方法都没有，全是字段——它就是一排数字。**它不需要每帧跑，也就不该挂在任何物体上。** 挂上去反而要浪费一个 GameObject。
>
> 判据很简单：**这东西需要「活」在场景里吗？** 不需要，就用 `ScriptableObject`。

### 4.2 标识分组（第 13–15 行）

```csharp
13:     [Header("标识")]
14:     public string displayName = "普通攻击";
15:     public Sprite icon;
```

| 行 | 代码 | 含义 |
|---|---|---|
| 13 | `[Header("标识")]` | Inspector 里的**粗体分组标题**。它把下面几行视觉上圈在一起，纯粹为了好找 |
| 14 | `public string displayName = "普通攻击";` | 显示用的名字。`string` 类型 → Inspector 里是一个单行文本框。`= "普通攻击"` 是**默认值**：新建资产时文本框里预填这个 |
| 15 | `public Sprite icon;` | 技能图标。`Sprite` 类型 → Inspector 里是一个**资产槽**，只能拖图片进去。**没有写默认值**，所以新建时是 `null`（显示为 `None (Sprite)`） |

> **注意这两行都没有 `[SerializeField]`。**
>
> 因为它们是 `public` 的，Unity 会**自动**序列化所有 `public` 字段——不需要额外标注。
>
> `[SerializeField]` 是给 `private` 字段用的：`private` 默认不序列化（Inspector 里看不见），加上这个特性才显示出来。项目里其他脚本用 `[SerializeField] private float x;` 写，是为了「Inspector 能改，但外部代码不能直接改」——封装性更好。
>
> 两种写法都能用，本文件选了更简洁的 `public`，因为它是纯数据、没有封装可言。

### 4.3 伤害分组（第 17–26 行）

```csharp
17:     [Header("伤害")]
18:     [Tooltip("伤害 = 攻击力 × 这个倍率。用倍率而不是固定值，装备变强时技能自动变强")]
19:     public float damageMultiplier = 1f;
20:
21:     [Tooltip("击退力度")]
22:     public float knockbackForce = 7f;
23:
24:     [Tooltip("技能自带的额外暴击率")]
25:     [Range(0f, 1f)]
26:     public float bonusCritChance = 0f;
```

| 行 | 代码 | 含义 |
|---|---|---|
| 17 | `[Header("伤害")]` | 第二个分组标题 |
| 18 | `[Tooltip("...")]` | 鼠标**悬停在字段上**时弹出的说明。它和 `Header` 一样是给编辑器看的，不影响运行 |
| 19 | `public float damageMultiplier = 1f;` | **伤害倍率**，默认 `1`。这是本文件最重要的字段，下面单独展开 |
| 21–22 | `knockbackForce = 7f` | 击退力度。这个数字会被写进 `DamageInfo.KnockbackForce`，最终变成 `HitReaction` 里的一行：`_rb.linearVelocity = new Vector2(away.x * info.KnockbackForce, ...)`。**7 意味着受击者以 7 单位/秒的速度被推开**，而玩家正常移动速度是 8——所以「被打退」和「自己走」的速度量级相当，手感才自然 |
| 24–26 | `bonusCritChance = 0f` | 技能**自带的额外暴击率**。`[Range(0f, 1f)]` 这个特性把输入框变成**滑条**，范围锁死在 0 到 1 之间——因为暴击率超过 100% 没有意义。它最终会**加在** `CharacterStats` 的暴击率上（见 `MeleeAttacker.cs` 第 89–90 行） |

> ### `1f` 里的 `f` 是什么意思
>
> C# 里小数默认是 `double` 类型（双精度）。`1.0` 是 `double`，`1f` 才是 `float`（单精度）。
>
> 不给 `f` 会怎样？赋值给 `float` 变量时编译器会报错：「无法将 double 隐式转换为 float」。所以项目里所有小数都带 `f`：`0f`、`1f`、`0.35f`、`7f`。这是 C# 的硬性规定，不是风格问题。

> ### 🔑 为什么是 `damageMultiplier` 而不是 `damage`？
>
> 这是本文件存在的最核心理由，值得单独讲。
>
> **假设写死伤害值**，字段叫 `public float damage = 20f;`，那么：
>
> ```
> 战士攻击力 13  →  打 20 点      （技能比普攻强，还算合理）
> 战士攻击力 26  →  还是打 20 点  （装备白穿了）
> 战士攻击力 130 →  还是打 20 点  （技能彻底变成废物）
> ```
>
> 技能只有「出生时」强，随着玩家养成会**相对越来越弱**。到后期没人愿意用它。
>
> **换成倍率**，字段叫 `damageMultiplier = 1f`，伤害公式变成：
>
> ```
> 伤害 = CharacterStats 的攻击力 × damageMultiplier
> ```
>
> ```
> 攻击力 13，倍率 1.5  →  19.5
> 攻击力 26，倍率 1.5  →  39      ← 自动翻倍了
> 攻击力 130，倍率 1.5 →  195     ← 始终是普攻的 1.5 倍
> ```
>
> **技能与普攻的相对强度被永久锁定了。** 这是所有长期养成游戏（DNF、暗黑、原神）的通用做法——**写倍率，不写结果**。
>
> 同一个道理在你项目里还出现过两次：
> - `MovementSpeed` 是「每秒移动多少单位」，而不是「每帧移动多少像素」——所以改帧率不影响手感
> - `SetMaxHealth(keepRatio: true)` 是「按比例保持血量」，而不是「保持血量绝对值」——所以换职业时血量百分比不变

### 4.4 判定范围分组（第 28–35 行）

```csharp
28:     [Header("判定范围")]
29:     [Tooltip("判定框相对角色的偏移，x 会被朝向翻转")]
30:     public Vector2 hitboxOffset = new Vector2(0.8f, 0f);
31:
32:     public Vector2 hitboxSize = new Vector2(1.2f, 0.9f);
33:
34:     [Tooltip("能打到的层。⚠️ 必须设成 Enemy，留空会永远打不到人")]
35:     public LayerMask targetLayers;
```

| 行 | 代码 | 含义 |
|---|---|---|
| 28 | `[Header("判定范围")]` | 第三个分组标题 |
| 30 | `public Vector2 hitboxOffset = new Vector2(0.8f, 0f);` | **判定框相对于角色原点的偏移**。`Vector2` 是「两个 float 打包在一起」的结构体，有 `.x` 和 `.y`。这里 `(0.8, 0)` 意思是「往右 0.8 个单位」。Inspector 里会显示成两个并排的输入框 `X` `Y` |
| 30 | 注释里的「x 会被朝向翻转」 | 这是关键。实际使用时是 `data.hitboxOffset.x * _facing`：角色朝右时 `_facing = 1`，框在右边 0.8；朝左时 `_facing = -1`，框变成左边 0.8。**`y` 不乘** —— 因为「向上偏移」不该因为转身而变成「向下」 |
| 32 | `public Vector2 hitboxSize = new Vector2(1.2f, 0.9f);` | **判定框的大小**（宽 1.2、高 0.9）。玩家角色本身是 1×1，所以这个框比角色略宽略矮，正好覆盖「挥出去的那一下」 |
| 35 | `public LayerMask targetLayers;` | **能打到哪些层**。`LayerMask` 是 Unity 的特殊类型，Inspector 里显示成一个下拉面板，让你勾选层名（`Enemy`、`Player`……）。它**没有默认值**，新建时是全不勾 |

> ### `LayerMask` 底层是一个整数
>
> 你勾的是「名字」，但 Unity 存的是一个 **32 位整数**，每一位对应一个层：
>
> ```
> 勾了 Ground（层号 8）  →  二进制 ...0001_0000_0000  →  十进制 256
> 勾了 Enemy（层号 7）   →  二进制 ...0000_1000_0000  →  十进制 128
> 两个都勾               →  二进制 ...0001_1000_0000  →  十进制 384
> ```
>
> 这个整数可以用 `.value` 取出来，`DamageZone.cs` 里就手动用了一次（见 10 号文档）。
>
> `Physics2D.OverlapBoxAll` 收到这个整数后，只会返回「所在层被勾选」的碰撞体——**其他层的东西就算几何上重叠也直接忽略**。
>
> ⚠️ **这就是那个最阴险的坑**：掩码是全 0 时，几何上明明重叠，函数却永远返回空数组。Unity 不会报任何错。

### 4.5 节奏分组（第 37–39 行）

```csharp
37:     [Header("节奏")]
38:     [Tooltip("两次攻击之间的最短间隔")]
39:     public float cooldown = 0.35f;
```

| 行 | 代码 | 含义 |
|---|---|---|
| 37 | `[Header("节奏")]` | 第四个分组标题 |
| 39 | `public float cooldown = 0.35f;` | **两次攻击之间的最短间隔**，单位是秒。默认 `0.35` 意味着「一秒最多打约 3 下」 |

> **注意这里存的是「基础冷却」，不是「最终冷却」。**
>
> `MeleeAttacker.GetCooldown()` 会把它再加工一次：
>
> ```
> 实际冷却 = cooldown ÷ 攻速 × (1 − 冷却缩减)
> ```
>
> 战士攻速 0.95 → `0.35 ÷ 0.95 ≈ 0.368 秒`
> 法师攻速 1.40 → `0.35 ÷ 1.40 = 0.250 秒`
>
> **同一份 `AttackData`，两个职业打出完全不同的节奏。** 这就是数据驱动的威力——你不需要为法师单独做一份攻击数据。

### 4.6 文件到这里就结束了

第 40 行是 `}`，整个文件到此为止。

**没有 `Awake()`、没有 `Update()`、没有任何方法。** 这很正常——`AttackData` 是纯数据容器，它的职责就是「被别的地方读」。

这里你可以建立一个判断标准：

> **一个 `.cs` 文件如果全是字段、没有方法，那它大概率应该继承 `ScriptableObject`，而不是 `MonoBehaviour`。**

---

## 五、在 Unity 里怎么配

`AttackData` 本身不需要挂在任何物体上，但你需要**创建它的实例**。

### 创建入口

在 Project 窗口定位到 `Assets/_Project/Data/Weapons`，在右侧空白处右键 → `Create` → `Roguelike` → `Attack Data`。

> **如果 Create 菜单里没有 `Roguelike` 这一项**：说明 `AttackData.cs` 还没编译完，或者 Console 里有红色报错。等右下角的编译小圈转完再看。

### 资产一：`Attack_Basic`（玩家用）

| 字段 | 填什么 | 为什么 |
|---|---|---|
| `Display Name` | `普通攻击` | 显示名，将来技能栏 UI 会用 |
| `Icon` | 留空 | 还没有美术资源 |
| `Damage Multiplier` | `1` | 普攻作为基准，倍率就是 1；技能才需要调成 1.5、2.0 |
| `Knockback Force` | `7` | 略小于玩家移速 8，把敌人推开但不会推飞 |
| `Bonus Crit Chance` | `0` | 普攻不带额外暴击，暴击率完全由职业决定 |
| `Hitbox Offset` | `(0.8, 0)` | 角色宽 1，往右 0.8 正好在身前一点 |
| `Hitbox Size` | `(1.2, 0.9)` | 比角色略宽，容错好一点 |
| **`Target Layers`** | **只勾 `Enemy`** ⚠️ | 玩家的攻击只打敌人。**留空 = 永远打不到任何东西，且不报错** |
| `Cooldown` | `0.35` | 一秒约 3 下，动作游戏的常见手感 |

### 资产二：`Attack_Enemy`（敌人用）

| 字段 | 填什么 | 和玩家版的差异 |
|---|---|---|
| `Display Name` | `敌人挥击` | |
| `Damage Multiplier` | `1` | 敌人不做倍率设计，保持 1 便于按攻击力直接推算 |
| `Knockback Force` | **`9`** | 比玩家的 7 大。**被敌人打到要有更强的「被打飞」感**，这是战斗节奏的基本礼貌 |
| `Hitbox Offset` | **`(0.7, 0)`** | 敌人比玩家矮（Scale 0.9），手更短 |
| `Hitbox Size` | **`(1.1, 0.9)`** | 略小 |
| **`Target Layers`** | **只勾 `Player`** ⚠️ | 敌人的攻击只打玩家 |
| `Cooldown` | **`1.2`** | 比玩家的 0.35 慢得多。**敌人出手频率必须明显低于玩家**，否则一打二直接被秒 |

> ### 为什么两个资产的 `Target Layers` 必须不同
>
> 如果敌人版也勾了 `Enemy`，会出现这种荒谬情况：**两个敌人贴在一起时互相打**，玩家站在旁边看戏。
>
> 如果玩家版勾了 `Player`，玩家自己会打到自己的判定框……实际上因为 `MeleeAttacker` 里有 `if (hit.gameObject == gameObject) continue;` 挡掉了，但**更糟的是敌人之间会互相伤害**。

### 把资产接上去

| 资产 | 挂到哪 |
|---|---|
| `Attack_Basic` | `Player` → `Melee Attacker` 组件的 `Attack Data` 槽 |
| `Attack_Enemy` | `Enemy` → `Enemy Controller` 组件的 `Attack Data` 槽 |

在 Project 窗口按住资产拖到 Inspector 的槽位上松开即可。

---

## 六、踩过的坑

> **现象**：配好 `Attack_Basic`，按 `J` 攻击，靶子就在眼前，但**完全没反应**。Console 干干净净，一条报错都没有。
>
> **根因**：`AttackData.targetLayers` 留空（全不勾）。`Physics2D.OverlapBoxAll` 收到一个全 0 的掩码，于是**几何上重叠的碰撞体也全部被过滤掉**，返回一个空数组。`foreach` 一次都不循环，什么都不发生。
>
> Unity 对这件事**不给任何提示**——`LayerMask` 是一个合法的值（0 就是「什么层都不选」），编译器没有理由报错。
>
> **解法**：把 `Target Layers` 勾上 `Enemy`，同时确认靶子的 `Layer` 确实是 `Enemy`。
>
> **排查顺序**（这个顺序能省你很多时间）：
>
> 1. **先看 Inspector 的层勾选**，不要先翻代码
> 2. 再看被攻击对象的 `Layer` 是不是目标层
> 3. 最后才怀疑几何——用 Scene 视图里的红色判定框确认位置
>
> **为什么这条要写进文档**：它是本项目所有 `LayerMask` 字段的通用陷阱（`AttackData.targetLayers`、`DamageZone.targetLayers`、`PlayerController.groundLayer`、`EnemyController.playerLayer` 都会中招）。为此项目里所有 `LayerMask` 字段都带 `⚠️` 开头的 `[Tooltip]`。

---

## 七、如果要改，改这里

### 1. 加一个新技能（最常见的操作）

**不用改任何代码。** 在 `Assets/_Project/Data/Weapons` 右键 → Create → `Roguelike` → `Attack Data`，改名字、填数值。

然后让玩家用上它——目前是手动把新资产拖到 `MeleeAttacker` 的 `Attack Data` 槽；等技能系统上线后，由 `SkillSystem` 在按键时调用 `MeleeAttacker.Execute(newSkillData)`。

### 2. 想加「元素类型」（火 / 冰 / 雷）

在 `AttackData` 里加字段：

```csharp
public enum ElementType { None, Fire, Ice, Lightning }

[Header("元素")]
public ElementType element = ElementType.None;
```

然后在 `MeleeAttacker.Execute()` 里把它写进 `DamageInfo`——但那意味着 **`DamageInfo` 也要加一个字段**。这正是 `DamageInfo` 用结构体而不是方法参数的原因：加字段时，所有调用点的签名都不用改。

### 3. 想让攻击带「延迟判定」（对齐动画的挥砍时机）

加一个字段：

```csharp
[Tooltip("发出判定前的延迟，用来对齐攻击动画")]
public float hitDelay = 0f;
```

然后在 `MeleeAttacker` 里用协程延迟调用 `Execute`：

```csharp
if (data.hitDelay > 0f) StartCoroutine(DelayedExecute(data));
else                    Execute(data);
```

> ⚠️ 注意：延迟期间玩家可能被击杀或被打断。延迟结束后要检查 `_hitReaction.IsStunned` 和 `isActiveAndEnabled`，否则会出现「人已经死了还在挥刀」。

### 4. 想做「穿透 / 范围伤害」

`hitboxSize` 调大就行（比如 `(5, 3)`），`MeleeAttacker` 的 `OverlapBoxAll` 本来就返回**所有**命中的目标，不做数量限制。

**注意**：玩家版有一次攻击只打一个目标的写法吗？没有——`MeleeAttacker` 是**全部打**（`foreach` 没有 `break`）。而 `EnemyController` 里加了一个 `break`，让敌人一次只打一个。这个差异是刻意的。

### 5. 想给攻击加音效 / 特效

**不要加到 `AttackData` 里**，那是数据。音效属于表现层，应该写在一个新的 `HitReaction` 订阅者里，或者给 `MeleeAttacker` 加一个 `[SerializeField] private AudioClip swingClip;`。

**判断标准**：`AttackData` 只装「影响伤害判定的数字」。凡是「听起来、看起来怎么样」的，都不属于这里。

### 6. 改完记得同步文档

按 `00-总览与阅读指南.md` 第六节的维护约定：改完代码顺手改这份文档，并把头部信息表的「最后更新」改成新的 Day 编号。
