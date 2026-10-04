# 12 · PlayerController —— 移动与跳跃

## 一、头部信息表

| 项 | 内容 |
|---|---|
| 文件路径 | `Assets/_Project/Scripts/Player/PlayerController.cs` |
| 所属层 | ④ 控制层 |
| 依赖 | `PlayerInputReader`（输入）、`HitReaction`（硬直状态）、`CharacterStats`（移速）、`Rigidbody2D`、`Collider2D` |
| 被谁依赖 | 没有脚本依赖它。它是「操作的终点」——输入到这里变成物理速度 |
| 行数 | 192 行 |
| 最后更新 | Day 4 |

---

## 二、一句话定位

把输入变成速度：水平用加减速过渡，垂直用四件套调出跳跃手感。

---

## 三、为什么需要它

### 如果没有这个脚本

角色不会动。但这不是重点——**重点是「角色会动」和「角色好操作」之间隔着一整套设计**。

大部分教程会告诉你这样写移动：

```csharp
_rb.linearVelocity = new Vector2(input.x * speed, _rb.linearVelocity.y);
```

三行就能让方块跑起来。然后你会遇到这些问题：

| 玩家会抱怨 | 真实原因 |
|---|---|
| 「起步和刹车太生硬，像推箱子」 | 速度是瞬间赋值的，没有加速过程 |
| 「我明明还在平台边上，怎么就跳不了了」 | 离开地面的判定太严格，没有容错 |
| 「我按了跳它不跳」 | 落地前按的键没被记住 |
| 「跳太高了，落点控制不住」 | 跳跃高度是固定的，玩家无法表达意图 |
| 「掉下来太慢，飘」 | 上升和下落用同一个重力 |

**这五条抱怨对应本文件的五组设计。** 它们不是「锦上添花」——少了任何一条，手感就会明显差一档。

### 这个脚本的核心思路

```
               每 1/50 秒执行一次（FixedUpdate）
                          │
      ┌───────────────────┼───────────────────┐
      ▼                   ▼                   ▼
  【读地面】          【算意图】          【改速度】
  CheckGround()       两个计时器          水平：MoveTowards
                      土狼时间           垂直：四件套
                      跳跃缓冲
```

**「跳跃手感四件套」是本文件存在的理由：**

| # | 机制 | 一句话 |
|---|---|---|
| 1 | 可变跳跃高度 | 轻点跳得矮，按住跳得高 |
| 2 | 上升/下落双重力 | 下落更重，落地更干脆 |
| 3 | 土狼时间 | 踏空后 0.1 秒内还能跳 |
| 4 | 跳跃缓冲 | 落地前 0.1 秒按的跳会被记住 |

其中 1 是**表达机制**（让玩家能做更多的事），2、3、4 是**容错机制**（让玩家不易失误）。两类都要有，缺一类手感就会偏。

---

## 四、代码全解

### 块 1 · 类声明与头部注释（第 1~11 行）

```csharp
 1: using UnityEngine;
 2:
 3: /// <summary>
 4: /// 玩家移动与跳跃逻辑。
 5: ///
 6: /// 注：受击硬直期间会主动放弃操作权，但重力和下落限制仍然生效 ——
 7: /// 这样被击退时角色会自然地被打飞、落地，而不是僵在半空。
 8: /// </summary>
 9: [RequireComponent(typeof(Rigidbody2D))]
10: [RequireComponent(typeof(PlayerInputReader))]
11: public class PlayerController : MonoBehaviour
```

| 行 | 代码 | 含义 |
|---|---|---|
| 1 | `using UnityEngine;` | 引入 Unity 基础命名空间。本文件没用到 `InputSystem`，因为所有输入都从 `PlayerInputReader` 拿——**这正说明分层生效了** |
| 3-8 | 文档注释 | 第 6~7 行提前说明了硬直期间的处理策略，因为那是本文件最容易看错的地方（为什么要「部分放弃操作权」而不是「全部 return」） |
| 9 | `[RequireComponent(typeof(Rigidbody2D))]` | **属性（Attribute）**，不是代码逻辑。它告诉 Unity：「这个组件必须和一个 `Rigidbody2D` 共存」 |
| 10 | `[RequireComponent(typeof(PlayerInputReader))]` | 同上，强制要求同物体上有一个 `PlayerInputReader` |
| 11 | `public class PlayerController : MonoBehaviour` | 类声明。名字必须和文件名 `PlayerController.cs` 一致 |

> **`[RequireComponent]` 到底做了什么？**
>
> 三件事：
>
> 1. **在 Inspector 里加组件时自动带上**：你给一个空物体加 `Player Controller`，Unity 会自动把 `Rigidbody 2D` 和 `Player Input Reader` 一起加上
> 2. **不允许删除**：你想删掉那个 `Rigidbody2D`，Unity 会弹窗「该组件被 PlayerController 依赖，不能移除」
> 3. **代码更安全**：因为能保证 `GetComponent<Rigidbody2D>()` 一定不会返回 `null`，所以第 67 行不需要写空判断
>
> **注意它管不了「组件被禁用」**——它只保证组件存在，不保证组件启用。所以 `PlayerController` 里对 `_hitReaction` 仍然做了 `null` 判断（第 79 行），因为 `HitReaction` **没有**被 `RequireComponent` 要求。
>
> **什么时候该用**：当你确定「没有这个组件，我这个脚本就完全不工作时」。如果只是「有更好」，就别加——那会限制别人搭物体的自由。

### 块 2 · 移动参数（第 13~20 行）

```csharp
13:     [Header("移动")]
14:     [SerializeField] private float maxSpeed = 8f;
15:
16:     [Tooltip("按下方向键时的加速度")]
17:     [SerializeField] private float acceleration = 60f;
18:
19:     [Tooltip("松开方向键时的减速度")]
20:     [SerializeField] private float deceleration = 80f;
```

| 行 | 代码 | 含义 |
|---|---|---|
| 13 | `[Header("移动")]` | 在 Inspector 里画一条**粗体分组标题**。纯显示效果，不影响逻辑 |
| 14 | `[SerializeField] private float maxSpeed = 8f;` | 最大水平速度，**8 个单位/秒** |
| 16 | `[Tooltip("按下方向键时的加速度")]` | 鼠标悬停在字段名上时显示的说明 |
| 17 | `[SerializeField] private float acceleration = 60f;` | 加速度：每秒把速度提高 60 个单位/秒。从 0 加速到 8 需要 `8 ÷ 60 ≈ 0.13` 秒 |
| 19-20 | `deceleration = 80f` | 减速度：松手后每秒把速度降低 80。从 8 停下来需要 `8 ÷ 80 = 0.1` 秒 |

> **`[SerializeField] private` 是什么意思？**
>
> 这是一个 Unity 特有的组合，它同时满足两个看似矛盾的需求：
>
> ```csharp
> [SerializeField] private float maxSpeed = 8f;
> ```
>
> - **`private`** —— C# 层面：别的脚本**不能**访问 `playerController.maxSpeed`。保护数据不被外部乱改
> - **`[SerializeField]`** —— Unity 层面：**但 Inspector 要显示它**，让你能在编辑器里调
>
> 如果只写 `private float maxSpeed;`，Inspector 里看不到它，你就只能改代码重新编译才能调手感。
> 如果写成 `public float maxSpeed;`，任何脚本都能改它，容易出乱子。
>
> **本项目的约定：所有需要在 Inspector 里调的字段，一律用 `[SerializeField] private`。** 全项目找不到一个 `public` 字段。

> **为什么 `8f` 后面有个 `f`？**
>
> C# 里小数常量默认是 `double`（双精度）。`8.0` 是 `double`，赋给 `float` 变量需要显式转换，否则编译报错。加 `f` 后缀表示「这是一个 `float` 字面量」。
>
> 写成 `8` 也可以（整数能隐式转成 `float`），但本项目的风格是统一带 `f`，一眼就能看出是浮点数。

> **加减速为什么要分开两个值？**
>
> 因为**起步和刹车的手感需求是相反的**：
>
> - **起步要「有重量感」**——加速度小一点（60），角色像是用力蹬地跑起来
> - **刹车要跟手**——减速度大一点（80），松开按键立刻有反应，不能滑出去老远
>
> 如果共用一个值：设 60，刹车会滑；设 80，起步会突兀。**分开之后两个需求都能满足。**
>
> 你去 Inspector 里把 `deceleration` 从 80 改成 25 再跑一遍，会明显感觉到角色开始「滑冰」——那就是减速度太小的效果。

### 块 3 · 跳跃参数（第 22~37 行）

```csharp
22:     [Header("跳跃")]
23:     [Tooltip("起跳瞬间赋予的垂直速度")]
24:     [SerializeField] private float jumpForce = 13f;
25:
26:     [Tooltip("上升阶段的重力倍率（越小跳得越高、滞空越久）")]
27:     [SerializeField] private float riseGravityScale = 3.5f;
28:
29:     [Tooltip("下落阶段的重力倍率（越大落地越干脆）")]
30:     [SerializeField] private float fallGravityScale = 6f;
31:
32:     [Tooltip("上升途中松开跳跃键，剩余上升速度乘以这个系数 —— 可变跳跃高度")]
33:     [Range(0f, 1f)]
34:     [SerializeField] private float jumpCutMultiplier = 0.45f;
35:
36:     [Tooltip("最大下落速度，防止高速下坠穿透地面")]
37:     [SerializeField] private float maxFallSpeed = 25f;
```

| 行 | 代码 | 含义 |
|---|---|---|
| 23-24 | `jumpForce = 13f` | 起跳瞬间把 `linearVelocity.y` 设成 13。**它决定跳跃能跳多高** |
| 26-27 | `riseGravityScale = 3.5f` | **上升时**的重力倍率。Unity 的 `Rigidbody2D.gravityScale` 会乘在物理重力上，所以实际重力是 `9.81 × 3.5 ≈ 34.3` |
| 29-30 | `fallGravityScale = 6f` | **下落时**的重力倍率，实际重力约 `58.9`。比上升大了 71% |
| 32-34 | `jumpCutMultiplier = 0.45f` | 松手时把剩余上升速度乘以 0.45 |
| 36-37 | `maxFallSpeed = 25f` | 下落速度的绝对值上限 |

> **`[Range(0f, 1f)]` 是什么？**
>
> 它把 Inspector 里的数字输入框**变成滑条**，并限制取值范围是 0 到 1。
>
> 为什么 `jumpCutMultiplier` 要用它？因为这个值**在数学上必须落在 0~1 之间**：
> - 填 `1.0` → 松手不削减速度，等于没有可变跳跃高度
> - 填 `0.0` → 松手瞬间上升速度归零，跳跃像撞到天花板
> - 填 `-0.5` → 松手会**向下加速**，角色会瞬间砸向地面
> - 填 `2.0` → 松手反而跳得更高，完全违背直觉
>
> 用 `[Range]` 把非法值挡在源头，比运行时写 `Mathf.Clamp` 更省事——**在编辑器里就填不出错误的值**。

> **`jumpForce = 13f` 和 `riseGravityScale = 3.5f` 是怎么配合出跳跃高度的？**
>
> 物理公式（只考虑上升段）：
>
> ```
> 上升高度 h = v² / (2g)
> ```
>
> 代入本项目数值：
>
> ```
> v = jumpForce = 13
> g = 9.81 × riseGravityScale = 9.81 × 3.5 ≈ 34.3
>
> h ≈ 13² / (2 × 34.3) = 169 / 68.7 ≈ 2.46 个单位
> ```
>
> 角色高 1 个单位，所以跳跃高度约 **2.5 个身位**。
>
> ⚠️ **这只是近似值**，原因有三个：
> 1. Unity 的物理是**离散**的，按 50Hz 步长积分，实际高度会比解析解略低
> 2. `gravityScale` 是**逐帧切换**的——起跳瞬间速度向上，用的是 `riseGravityScale`；但 `FixedUpdate` 每帧都会重新判断，如果某一帧速度降到 0 以下，那一帧就换成 `fallGravityScale` 了
> 3. 第 165 行的「跳跃削减」如果触发了，实际高度还会进一步降低
>
> **所以这个公式的用途是「调参时估算方向」，不是「精确预测」**：
> - 想跳得更高 → 提高 `jumpForce`（平方关系，效果明显）或降低 `riseGravityScale`
> - 想滞空更久 → 降低 `riseGravityScale`
> - 想落地更干脆 → 提高 `fallGravityScale`

### 块 4 · 手感辅助与地面检测参数（第 39~51 行）

```csharp
39:     [Header("手感辅助")]
40:     [Tooltip("土狼时间：离开平台后仍可起跳的宽限时间")]
41:     [SerializeField] private float coyoteTime = 0.1f;
42:
43:     [Tooltip("跳跃缓冲：落地前提前按跳，落地瞬间自动起跳")]
44:     [SerializeField] private float jumpBufferTime = 0.1f;
45:
46:     [Header("地面检测")]
47:     [Tooltip("检测圆的半径。太大会导致「还没落地就算落地」，与跳跃缓冲叠加会表现为空中起跳")]
48:     [SerializeField] private float groundCheckRadius = 0.05f;
49:
50:     [Tooltip("⚠️ 必须选 Ground 层，留空会导致永远检测不到地面")]
51:     [SerializeField] private LayerMask groundLayer;
```

| 行 | 代码 | 含义 |
|---|---|---|
| 40-41 | `coyoteTime = 0.1f` | 土狼时间：离开地面后 0.1 秒内仍然可以起跳 |
| 43-44 | `jumpBufferTime = 0.1f` | 跳跃缓冲：落地前 0.1 秒内按的跳会被记住 |
| 47-48 | `groundCheckRadius = 0.05f` | 地面检测圆的半径，**只有 0.05 个单位**——刚好贴住脚底 |
| 50-51 | `groundLayer` | `LayerMask` 类型，在 Inspector 里显示成「层选择下拉框」 |

> **「土狼时间」这个名字的来历**
>
> 来自经典卡通《BB鸟与歪心狼》（Road Runner）：歪心狼追着 BB 鸟跑出悬崖，**在空中还能继续跑几步才掉下去**。
>
> 后来《塞尔达传说》《超级马里奥》等游戏把这个机制做进了跳跃——角色离开平台边缘后，给玩家 0.1 秒的宽限时间仍然可以起跳。
>
> **为什么需要它**：玩家在平台边缘起跳时，往往是在**脚已经踏空**的那一帧才按下跳跃键。如果没有宽限，玩家会觉得「我明明还在边上啊」，而这是**人类反应速度的物理限制**，不是玩家的错。
>
> 0.1 秒听起来很短，但在 50Hz 的物理步长下是 **5 个物理帧**——足够覆盖绝大多数「踩线」的操作。

> **跳跃缓冲解决的是相反的问题**
>
> 土狼时间照顾「**跳得太晚**」的玩家，跳跃缓冲照顾「**跳得太早**」的玩家。
>
> 场景：你从高处落下，眼看要落地了，你提前按了跳跃键准备连跳。如果游戏要求「必须踩到地面才能按」，那这 0.1 秒的提前量就白按了，你会觉得「我按了啊，它不跳」。
>
> 缓冲的做法是：**按下时就记一笔（把计时器充满），然后在接下来 0.1 秒内只要踩到地面，就立刻起跳。**
>
> **两个机制合起来，玩家在落地前后的 0.2 秒窗口内按跳都能成功。** 这就是「跟手」的感觉来源——不是游戏反应快，是游戏**不惩罚玩家的微小失误**。

> **`groundCheckRadius = 0.05f` 为什么这么小？**
>
> 因为这个值**直接决定了「什么时候算落地」**。半径越大，`IsGrounded` 越早变成 `true`。
>
> 一个身高 1 单位的角色，半径设 0.15 意味着**脚底离地还有 15% 身高时就算落地了**。放到 1080p 屏幕上（角色约 200 像素高），就是**还差 30 像素**——肉眼非常明显。
>
> 这个坑本项目真实踩过，详见「踩过的坑」一节。当前值 0.05 是收紧后的结果。

> **`LayerMask` 是什么类型？**
>
> Unity 用「层」（Layer）来给物体分类，一共 32 层（编号 0~31），每层用**一个二进制位**表示。
>
> `LayerMask` 就是「哪几层被选中」的位掩码。本项目用到的：
>
> | 层号 | 名字 | 用途 |
> |---|---|---|
> | 6 | `Player` | 玩家碰撞体 |
> | 7 | `Enemy` | 敌人 |
> | 8 | `Ground` | 地面、平台 |
>
> `groundLayer` 在 Inspector 里勾选 `Ground`，内部值是 `1 << 8 = 256`——这个数字会出现在场景文件里（`m_Bits: 256`）。
>
> **⚠️ 留空的后果**：如果 `groundLayer` 什么都不勾，值是 0，`Physics2D.OverlapCircle` 永远返回 `null`，`IsGrounded` 永远是 `false`——**角色能左右移动，但完全跳不起来，而且 Console 一句报错都没有**。

### 块 5 · 私有字段与公开属性（第 53~63 行）

```csharp
53:     private Rigidbody2D _rb;
54:     private Collider2D _col;
55:     private PlayerInputReader _input;
56:     private HitReaction _hitReaction;
57:     private CharacterStats _stats;
58:
59:     private float _coyoteCounter;      // 土狼时间剩余
60:     private float _jumpBufferCounter;  // 跳跃缓冲剩余
61:
62:     /// <summary>本帧是否站在地面上</summary>
63:     public bool IsGrounded { get; private set; }
```

| 行 | 代码 | 含义 |
|---|---|---|
| 53 | `private Rigidbody2D _rb;` | 缓存刚体的引用。**为什么缓存而不是每次 `GetComponent`**：`GetComponent` 内部要做类型查找，虽然不算慢，但在 `FixedUpdate` 里每帧调用是浪费。缓存一次，之后直接用 |
| 54 | `private Collider2D _col;` | 缓存碰撞体。**注意类型是 `Collider2D`（基类）而不是 `CapsuleCollider2D`**——因为本脚本只用到 `bounds` 这个所有碰撞体都有的属性，用基类更通用 |
| 55 | `private PlayerInputReader _input;` | 缓存输入层。**这是本脚本唯一的输入来源**——整个文件里找不到一处 `Keyboard.current` |
| 56 | `private HitReaction _hitReaction;` | 缓存受击表现组件，用来查询「是否处于硬直」 |
| 57 | `private CharacterStats _stats;` | 缓存属性系统，用来读移速。**没有加 `[SerializeField]`，因为它不是配置项** |
| 59 | `private float _coyoteCounter;` | 土狼时间的**剩余量**。`> 0` 表示还能起跳，每物理帧递减 |
| 60 | `private float _jumpBufferCounter;` | 跳跃缓冲的**剩余量**。`> 0` 表示玩家最近按过跳，每物理帧递减 |
| 63 | `public bool IsGrounded { get; private set; }` | 「这一物理帧是否踩着地面」。外部可读，只有本类能写 |

> **为什么 `_rb`、`_col` 这些不加 `[SerializeField]`？**
>
> 因为它们是**运行时引用**，不是**配置数据**。
>
> - `[SerializeField]` 的字段会显示在 Inspector 里，让你**手动拖引用**
> - `_rb`、`_col`、`_input`、`_stats` 都在**同一个 GameObject 上**，用 `GetComponent` 自动拿到就行，不需要手动拖
>
> 手动拖引用有两个缺点：忘了拖就是 `null`；物体结构一变就得重新拖。**同物体上的组件一律用 `GetComponent`，跨物体的才用 `[SerializeField]` 拖。**
>
> 例外是第 56 行的 `_hitReaction`——它也是同物体的，但它**不是必需的**（没有就不做硬直判断），所以代码里做了 `null` 检查。

> **`_coyoteCounter` 和 `coyoteTime` 为什么要分开两个变量？**
>
> 这是**「配置」和「状态」的经典分离**：
>
> | | `coyoteTime` | `_coyoteCounter` |
> |---|---|---|
> | 类型 | `[SerializeField]` 配置 | 私有运行时状态 |
> | 变化 | 你在 Inspector 里设，运行中不变 | 每物理帧递减，落地时重置 |
> | 显示 | Inspector 里能看到 | 外部完全看不到 |
>
> 如果只用一个变量，落地时把它重置成 `coyoteTime`，那递减几次之后你就**永久丢失了原始配置值**，调参都没法调了。

> **`{ get; private set; }` 和 `[SerializeField] private` 的区别**
>
> 两种写法都能让外部**读**到值，但机制不同：
>
> ```csharp
> [SerializeField] private float maxSpeed = 8f;      // 外部读不到！只能 Inspector 改
> public bool IsGrounded { get; private set; }        // 外部能读，不能改
> ```
>
> **`IsGrounded` 为什么必须是属性而不是 `[SerializeField]` 字段？**
>
> 因为它的值是 `CheckGround()` **算出来的**，不是配置的。你要让别的脚本（比如未来做落地音效的脚本）能读到它，但绝不能让别人改它——改成 `true` 会让角色凭空获得一次跳跃机会。

### 块 6 · Awake：缓存组件引用（第 65~72 行）

```csharp
65:     private void Awake()
66:     {
67:         _rb          = GetComponent<Rigidbody2D>();
68:         _col         = GetComponent<Collider2D>();
69:         _input       = GetComponent<PlayerInputReader>();
70:         _hitReaction = GetComponent<HitReaction>();
71:         _stats = GetComponent<CharacterStats>();
72:     }
```

| 行 | 代码 | 含义 |
|---|---|---|
| 65 | `private void Awake()` | 物体创建时调用一次，早于 `OnEnable` 和 `Start` |
| 67 | `GetComponent<Rigidbody2D>()` | 在**同一个 GameObject 上**查找指定类型的组件。因为有第 9 行的 `[RequireComponent]`，返回值一定不是 `null` |
| 68 | `GetComponent<Collider2D>()` | 查找任意碰撞体。Player 上挂的是 `CapsuleCollider2D`（胶囊体），它是 `Collider2D` 的子类，所以能取到 |
| 69 | `GetComponent<PlayerInputReader>()` | 同样有 `[RequireComponent]` 保证存在 |
| 70 | `GetComponent<HitReaction>()` | **可能返回 `null`**——`HitReaction` 没有被 `RequireComponent` 要求。所以第 79 行必须判空 |
| 71 | `GetComponent<CharacterStats>()` | **也可能返回 `null`**——没有 `RequireComponent`。所以第 141 行必须判空 |

> **`GetComponent<T>()` 的泛型参数是什么？**
>
> `<Rigidbody2D>` 里的尖括号是 C# **泛型**语法。`GetComponent<T>` 是一个泛型方法，`T` 是你要找的组件类型。
>
> 好处是**不用强制类型转换**：
>
> ```csharp
> // 泛型写法（本项目用的）
> Rigidbody2D rb = GetComponent<Rigidbody2D>();   // 直接就是 Rigidbody2D 类型
>
> // 老的非泛型写法
> Rigidbody2D rb = (Rigidbody2D)GetComponent(typeof(Rigidbody2D));   // 要手动转型
> ```
>
> 泛型写法还有**编译期类型检查**——写错类型名会直接编译报错，而不是运行时崩溃。

> **为什么第 70、71 行的对齐有问题？**
>
> ```csharp
> 69:         _input       = GetComponent<PlayerInputReader>();
> 70:         _hitReaction = GetComponent<HitReaction>();
> 71:         _stats = GetComponent<CharacterStats>();
> ```
>
> 第 71 行的 `=` 没有像上面几行那样对齐到同一列。这是后来加 `CharacterStats` 时留下的痕迹（原本只有四行，对齐过；加第五行时行长度变了）。
>
> **不影响功能**，但如果要较真，把它改成 `_stats       = GetComponent<CharacterStats>();` 就整齐了。

### 块 7 · FixedUpdate：每物理帧的主循环（第 74~98 行）

**这是本文件的核心。** 建议反复读几遍。

```csharp
74:     private void FixedUpdate()
75:     {
76:         // 顺序有讲究：先知道站没站在地上，才能算土狼时间和能不能起跳
77:         CheckGround();
78:
79:         bool canAct = _hitReaction == null || !_hitReaction.IsStunned;
80:
81:         if (canAct)
82:         {
83:             UpdateJumpTimers();
84:             TryJump();
85:             ApplyHorizontalMovement();
86:         }
87:         else
88:         {
89:             // 硬直期间清空输入与计时器，避免硬直结束后「补跳」一下
90:             _input.ConsumeJumpPressed();
91:             _coyoteCounter     = 0f;
92:             _jumpBufferCounter = 0f;
93:         }
94:
95:         // 重力和下落限制在硬直期间照常生效，否则被打飞的角色会僵在半空
96:         ApplyGravityAndJumpCut();
97:         ClampFallSpeed();
98:     }
```

| 行 | 代码 | 含义 |
|---|---|---|
| 74 | `private void FixedUpdate()` | 生命周期方法。**按固定时间步长调用**——默认 `Time.fixedDeltaTime = 0.02`，也就是每秒精确 50 次，与渲染帧率无关 |
| 76 | 注释 | 解释第 77 行为什么必须放在最前面 |
| 77 | `CheckGround();` | 更新 `IsGrounded`。**必须在所有其他逻辑之前** |
| 79 | `bool canAct = _hitReaction == null \|\| !_hitReaction.IsStunned;` | 算出「玩家现在能不能操作」 |
| 81 | `if (canAct)` | 能操作时，执行三个输入驱动的逻辑 |
| 83 | `UpdateJumpTimers();` | 更新土狼时间和跳跃缓冲两个计时器 |
| 84 | `TryJump();` | 如果两个计时器都还有剩余，就起跳 |
| 85 | `ApplyHorizontalMovement();` | 处理左右移动 |
| 87-93 | `else` 分支 | 硬直期间的处理 |
| 90 | `_input.ConsumeJumpPressed();` | **调用但丢弃返回值**——目的是把锁存里的事件清掉 |
| 91 | `_coyoteCounter = 0f;` | 清零土狼时间 |
| 92 | `_jumpBufferCounter = 0f;` | 清零跳跃缓冲 |
| 95 | 注释 | 解释第 96~97 行为什么在 `if` 外面 |
| 96 | `ApplyGravityAndJumpCut();` | 切换重力倍率 + 处理跳跃削减。**硬直期间也要跑** |
| 97 | `ClampFallSpeed();` | 限制最大下落速度。**硬直期间也要跑** |

> ## 为什么用 `FixedUpdate` 而不是 `Update`？
>
> **这是 Unity 物理相关代码最重要的一条规则。**
>
> | | `Update()` | `FixedUpdate()` |
> |---|---|---|
> | 调用频率 | 跟随渲染帧率 | 固定每秒 50 次 |
> | 典型帧率 | 60 / 120 / 144 fps，会波动 | 恒定 50 Hz |
> | 和物理引擎的关系 | 无关 | 物理引擎按同样的步长推进 |
>
> 如果你在 `Update` 里改速度：
>
> ```
> 144Hz 显示器：每秒改 144 次速度 → 物理引擎推进 50 次
> 60Hz  显示器：每秒改  60 次速度 → 物理引擎推进 50 次
> ```
>
> 每次改速度时，如果用的是「速度 += 加速度」（增量式），那么 **144Hz 的机器上角色会跑得快 2.4 倍**。这是经典的「帧率越高跑得越快」bug。
>
> 用 `FixedUpdate` 就完全没有这个问题——它和物理引擎**同一个节拍**，每次调用的时间间隔恒定。
>
> **本项目用的是 `Mathf.MoveTowards(current, target, rate * Time.fixedDeltaTime)`**——这种写法即使放在 `Update` 里也不会因帧率而变（因为乘了 `fixedDeltaTime`），但**放在 `FixedUpdate` 里仍然是对的**，因为：
> - 改 `linearVelocity` 和 `gravityScale` 是物理操作，应该和物理解算同步
> - 如果在 `Update` 里改，物理引擎可能在两次 `Update` 之间已经算过好几步了，你改的值会「迟到」

> ## `Time.fixedDeltaTime` 和 `Time.deltaTime` 的区别
>
> | | `Time.deltaTime` | `Time.fixedDeltaTime` |
> |---|---|---|
> | 含义 | **上一渲染帧**花了多少秒 | **固定**的物理步长（默认 0.02） |
> | 值会变吗 | 会，卡顿时变大 | 不会，除非你改工程设置 |
> | 配合哪个方法 | `Update` | `FixedUpdate` |
>
> **判断口诀：在哪个方法里，就用哪个。**
>
> - `Update` 里写 `speed * Time.deltaTime`
> - `FixedUpdate` 里写 `speed * Time.fixedDeltaTime`
>
> **为什么必须乘它？** 因为它把「每秒的速率」换算成「这一帧该走多少」。
>
> ```
> 加速度 60（单位/秒²）× 0.02 秒/帧 = 1.2（单位/秒，每帧的变化量）
> ```
>
> 如果忘了乘，`MoveTowards` 每次都会试图把速度**一次性**拉到目标值——那 `MoveTowards` 就退化成直接赋值了，加减速效果完全消失。
>
> **本文件里所有 6 处 `Time.fixedDeltaTime` 都是这个用途**（第 118、122、151 行，以及下文会讲到的几处）。而 `HitReaction`、`Health` 那些在 `Update` 里跑的脚本用的是 `Time.deltaTime`。

> ## `canAct` 这一行为什么写成 `||` 而不是 `&&`？
>
> ```csharp
> bool canAct = _hitReaction == null || !_hitReaction.IsStunned;
> ```
>
> 拆开看，它表达的是「**没有 `HitReaction` 组件，或者 有但它不在硬直中**」：
>
> | `_hitReaction` | `!IsStunned` | 结果 | 含义 |
> |---|---|---|---|
> | `null` | ——（不会求值） | `true` | 没装受击组件，永远能操作 |
> | 有值 | `true`（没硬直） | `true` | 正常状态 |
> | 有值 | `false`（在硬直） | `false` | 失去操作权 |
>
> **`||` 的短路特性在这里很关键**：C# 从左往右求值，`||` 在左边为 `true` 时**不会计算右边**。
>
> 所以当 `_hitReaction == null` 时，右边的 `_hitReaction.IsStunned` **根本不会执行**——避免了对 `null` 调用方法导致的 `NullReferenceException`。
>
> 如果把顺序写反成 `!_hitReaction.IsStunned || _hitReaction == null`，那就一定会崩——先执行的就是对 `null` 取属性。
>
> **这就是「空判断要写在 `||` 左边」这条规则的由来。** 同理，`?.` 空条件运算符也是干这个的：`_hitReaction?.IsStunned ?? false` 效果一样，但可读性差一些。

> ## 硬直期间为什么要「清空输入和计时器」？
>
> 看第 90~92 行。假设没有这三行，会发生什么：
>
> ```
> t = 0.00s   敌人打中玩家 → HitReaction.IsStunned = true
> t = 0.05s   玩家（仍然按着跳跃键乱按）按下跳跃
>             → PlayerInputReader 的 _jumpPressedLatch = true
> t = 0.18s   硬直结束，canAct 变回 true
> t = 0.20s   UpdateJumpTimers() 执行
>             → _input.ConsumeJumpPressed() 返回 true
>             → _jumpBufferCounter = jumpBufferTime = 0.1
>             → 玩家在硬直期间按的那次跳跃，现在才生效！
> ```
>
> **表现就是：玩家被打了，硬直结束的一瞬间角色自己跳了一下**，而玩家当时只是在乱按或者根本没按。
>
> 第 90 行 `_input.ConsumeJumpPressed();` 的作用就是**把锁存里的事件「吃掉」**——调用方法、丢弃返回值，等于告诉输入层「这次按键我不要了」。
>
> 第 91、92 行清零两个计时器，防止的是另一种情况：玩家在被击飞前刚好按过跳跃（缓冲里还留着一半），硬直结束后这个残留的缓冲会立刻触发一次起跳。
>
> > **一个通用规律**：凡是「玩家输入被暂存起来、稍后才生效」的机制，在**角色失去控制权的时候都必须清空暂存**。否则解禁的一瞬间会「补执行」一堆过期指令，手感非常怪。
> >
> > 跳跃缓冲是这种机制，技能排队（输入缓冲后连招）也是。

> ## 为什么重力**不**放在 `if (canAct)` 里面？
>
> 看第 95 行的注释。如果第 96、97 行也放进 `if` 块里：
>
> ```
> 玩家被打飞，速度向上 8 单位/秒
> → 进入硬直，canAct = false
> → ApplyGravityAndJumpCut() 不执行
> → gravityScale 保持上一次的值（也许是 3.5）
> → 更糟的是 ClampFallSpeed() 也不执行
> → 角色会以恒定的速度一直往上飘，直到硬直结束
> ```
>
> **「失去操作权」和「失去物理」是两件事。** 被击飞的角色应该：
>
> - ❌ 不能主动移动、不能起跳 —— 这是**放弃操作权**
> - ✅ 但应该照常受重力、照常落地、照常受下落限速 —— 这是**物理世界继续运转**
>
> 所以 `ApplyGravityAndJumpCut()` 和 `ClampFallSpeed()` 留在 `if` 外面。
>
> **验证方法**：去打一下敌人被反击，你会看到角色被打飞后**自然地划一个抛物线落地**——如果这两行放错位置，角色会僵在半空中横着飘。

### 块 8 · 地面检测（第 100~110 行）

```csharp
100:     // ---------------- 地面检测 ----------------
101:
102:     /// <summary>脚底中心点：取碰撞体包围盒的底边中点</summary>
103:     private Vector2 GroundCheckPoint =>
104:         new Vector2(_col.bounds.center.x, _col.bounds.min.y);
105:
106:     private void CheckGround()
107:     {
108:         // 用圆形重叠检测，而不是 OnCollisionEnter —— 后者会把自己撞墙、撞天花板也算成「落地」
109:         IsGrounded = Physics2D.OverlapCircle(GroundCheckPoint, groundCheckRadius, groundLayer);
110:     }
```

| 行 | 代码 | 含义 |
|---|---|---|
| 100 | 分节注释 | 标明这一块负责地面检测 |
| 102 | 文档注释 | 说明这个属性返回的是「脚底中心点」 |
| 103 | `private Vector2 GroundCheckPoint =>` | **表达式体属性**。`=>` 后面是「每次读取时计算并返回的表达式」 |
| 104 | `new Vector2(_col.bounds.center.x, _col.bounds.min.y);` | 拼出一个二维坐标 |
| 106 | `private void CheckGround()` | 私有方法，在 `FixedUpdate` 里每物理帧调用一次 |
| 108 | 注释 | 说明为什么不用 `OnCollisionEnter2D`（下文展开） |
| 109 | `IsGrounded = Physics2D.OverlapCircle(...);` | 画一个圆做重叠检测，结果赋给 `IsGrounded` |

> ## `_col.bounds` 是什么？
>
> `Collider2D.bounds` 返回一个 `Bounds` 结构体，描述碰撞体在**世界坐标**下的轴对齐包围盒：
>
> ```
>         bounds.center
>               │
>    ┌──────────┼──────────┐  ← bounds.max.y
>    │          │          │
>    │          │          │
>    │          │          │
>    └──────────┼──────────┘  ← bounds.min.y
>               │
> ```
>
> - `bounds.center` → 包围盒的几何中心（`Vector3`）
> - `bounds.min` → 左下角坐标
> - `bounds.max` → 右上角坐标
>
> 第 104 行取的是 `center.x`（水平居中）和 `min.y`（最底部），**合起来就是「脚底正中间那个点」**。
>
> **为什么用 `bounds` 而不是 `transform.position`？**
>
> 因为 `transform.position` 是物体的**原点**，而原点不一定在脚底——`CapsuleCollider2D` 有自己的 `offset`，角色贴图也可能不对称。
>
> 用 `bounds` 的好处是**自动适应任何碰撞体形状和大小**：把胶囊体从 1×1 改成 1×1.5，检测点会自动跟着下移，不用改代码。

> ## `=>` 在属性里的意思是「每次读取时重新计算」
>
> ```csharp
> private Vector2 GroundCheckPoint => new Vector2(_col.bounds.center.x, _col.bounds.min.y);
> ```
>
> 它和下面这段**完全等价**：
>
> ```csharp
> private Vector2 GroundCheckPoint
> {
>     get { return new Vector2(_col.bounds.center.x, _col.bounds.min.y); }
> }
> ```
>
> **关键点：它没有存储任何东西。** 每次读 `GroundCheckPoint`，都会**重新算一遍**。
>
> 这正是我们要的——角色每帧都在移动，脚底位置一直在变，缓存下来反而会错。
>
> **对比第 63 行的 `IsGrounded`**：
>
> ```csharp
> public bool IsGrounded { get; private set; }        // 自动属性，有隐藏字段存储
> private Vector2 GroundCheckPoint => ...;            // 只读属性，每次计算
> ```
>
> `IsGrounded` 是**被赋值**的（在 `CheckGround` 里），需要一个地方存它；`GroundCheckPoint` 是**算出来的**，不需要存。**判断依据：需不需要「记住」一个值。**

> ## `Physics2D.OverlapCircle` 的三个参数
>
> ```csharp
> Physics2D.OverlapCircle(Vector2 point, float radius, int layerMask)
> ```
>
> | 参数 | 本项目传的值 | 含义 |
> |---|---|---|
> | `point` | `GroundCheckPoint` | 圆心，脚底中心 |
> | `radius` | `groundCheckRadius` = 0.05 | 半径。**只有 0.05 个单位** |
> | `layerMask` | `groundLayer` | 只检查这一层上的碰撞体 |
>
> **返回值**：碰到**第一个** `Collider2D`；一个都没碰到返回 `null`。
>
> **`null` 怎么变成 `bool` 的？**
>
> 第 109 行把 `Collider2D` 直接赋给了 `bool` 类型的 `IsGrounded`。这在 C# 里能编译，靠的是 **Unity 为 `UnityEngine.Object` 定义的一个隐式转换运算符**：
>
> ```csharp
> public static implicit operator bool(Object exists);
> ```
>
> 规则是：对象引用不为 `null` **且** 对应的原生对象还没被销毁 → `true`，否则 `false`。
>
> 所以 `IsGrounded = Physics2D.OverlapCircle(...)` 等价于：
>
> ```csharp
> Collider2D hit = Physics2D.OverlapCircle(...);
> IsGrounded = (hit != null);
> ```
>
> > ⚠️ **Unity 的这个「假 null」特性值得单独记住**：一个被 `Destroy()` 的物体，C# 引用不为 `null`，但 `== null` 会返回 `true`。所以判断 Unity 对象是否有效，永远用 `if (obj == null)` 或 `if (obj)`，**不要**用 `ReferenceEquals` 或 `?.`。

> ## 为什么不用 `OnCollisionEnter2D`？
>
> 因为它只告诉你「**撞到了东西**」，不告诉你「**从哪个方向撞的**」。
>
> 角色会遇到三种碰撞：
>
> | 情况 | 应该算落地吗 | `OnCollisionEnter2D` 会触发吗 |
> |---|---|---|
> | 脚踩到地面 | ✅ 算 | 会 |
> | 头撞到天花板 | ❌ 不算 | **也会** |
> | 身体侧面贴到墙 | ❌ 不算 | **也会** |
>
> 如果你用 `OnCollisionEnter2D` 来设置「落地」，那角色**贴在墙上就能无限跳**——因为贴着墙时 `IsGrounded` 一直是真的，按跳跃键就能一直往上爬。
>
> **`Physics2D.OverlapCircle` 的解法**是：只在**脚底一个很小的圆圈**里检查，天然就避开了墙壁和天花板。半径 0.05 确保了这个圆圈只在真正踩到东西时才重叠。
>
> **那 `OnCollisionEnter2D` 该用在哪？**
> - 判断撞击力度（撞到硬物时播放音效、产生伤害）
> - 触发机关（踩到压力板）
> - 这些都不关心方向，只关心「碰到了」

### 块 9 · 两个计时器（第 112~123 行）

```csharp
112:     // ---------------- 计时器 ----------------
113:
114:     private void UpdateJumpTimers()
115:     {
116:         // 土狼时间：站在地上就充满，离开后开始倒数
117:         if (IsGrounded) _coyoteCounter = coyoteTime;
118:         else            _coyoteCounter -= Time.fixedDeltaTime;
119:
120:         // 跳跃缓冲：按下瞬间充满，之后开始倒数
121:         if (_input.ConsumeJumpPressed()) _jumpBufferCounter = jumpBufferTime;
122:         else                             _jumpBufferCounter -= Time.fixedDeltaTime;
123:     }
```

| 行 | 代码 | 含义 |
|---|---|---|
| 112 | 分节注释 | 标明两个计时器的更新逻辑 |
| 114 | `private void UpdateJumpTimers()` | 每物理帧调用一次，在 `CheckGround()` 之后、`TryJump()` 之前 |
| 117 | `if (IsGrounded) _coyoteCounter = coyoteTime;` | **站在地上时把土狼计时器「充满」** |
| 118 | `else _coyoteCounter -= Time.fixedDeltaTime;` | 离开地面后每物理帧减 0.02 秒 |
| 121 | `if (_input.ConsumeJumpPressed()) _jumpBufferCounter = jumpBufferTime;` | 如果这一帧消费到了「按下跳跃」，把缓冲计时器充满 |
| 122 | `else _jumpBufferCounter -= Time.fixedDeltaTime;` | 否则递减 |

> ## 第 117 行的「充满」是土狼时间的**全部秘密**
>
> 注意它写的是 `= coyoteTime`（**赋值**），不是 `-=`（递减）。
>
> ```
> 站在地上的时候：
>     _coyoteCounter = 0.1   ← 每帧都重新充满
>     _coyoteCounter = 0.1
>     _coyoteCounter = 0.1
>     ...
>
> 走两步后踏空（IsGrounded 变 false）：
>     _coyoteCounter = 0.08
>     _coyoteCounter = 0.06
>     _coyoteCounter = 0.04   ← 玩家这时候按跳，还能跳（> 0）
>     _coyoteCounter = 0.02   ← 还能跳
>     _coyoteCounter = 0.00   ← 跳不了了
> ```
>
> **「站在地上就一直满着」这个设计，保证了离开地面的那一刻计时器一定是满的。** 如果写成 `if (IsGrounded) _coyoteCounter -= 0;`（等于什么都不做），那么计时器会在很久以前就耗尽，土狼时间完全失效。
>
> 第 121 行的跳跃缓冲是**同样的套路**，只是「充满」的触发条件从「站在地上」换成了「按下了跳跃键」。

> ## 为什么两个计时器用 `if/else` 而不是 `if` 加独立递减？
>
> 看第 117~118 行：`if (IsGrounded) 充满 else 递减`。
>
> **如果写成这样会怎样？**
>
> ```csharp
> if (IsGrounded) _coyoteCounter = coyoteTime;   // 充满
> _coyoteCounter -= Time.fixedDeltaTime;          // 无条件递减
> ```
>
> 结果是：充满成 0.1，立刻减到 0.08。**每次落地都白扣一帧**，土狼时间实际只有 0.08 秒。虽然差别不大，但这是逻辑错误——「站在地上」的状态下计时器不该被消耗。
>
> **`if/else` 表达的是「两种情况互斥」**：要么重置，要么倒计时，不会同时发生。

> ## 为什么跳跃缓冲放在 `UpdateJumpTimers` 里消费 `ConsumeJumpPressed()`？
>
> 因为**缓冲的本质就是「把按下事件转换成一个会衰减的计时器」**。
>
> 消费的时机很关键：每物理帧**只消费一次**。如果某一帧没有按下，`ConsumeJumpPressed()` 返回 `false`，就走 `else` 分支递减。
>
> 由于 `Consume` 的语义是「取走就没了」，所以**一次按键只会把计时器充满一次**。哪怕玩家狂按跳跃键，也只会让计时器保持在 `jumpBufferTime`，不会叠加成 0.5 秒的超级缓冲。

### 块 10 · 起跳（第 125~137 行）

```csharp
125:     // ---------------- 起跳 ----------------
126:
127:     private void TryJump()
128:     {
129:         // 两个计时器都还有剩余 = 玩家「想跳」且「跳得了」
130:         if (_jumpBufferCounter <= 0f || _coyoteCounter <= 0f) return;
131:
132:         _rb.linearVelocity = new Vector2(_rb.linearVelocity.x, jumpForce);
133:
134:         // 立刻清零，避免同一次按键触发多次起跳
135:         _jumpBufferCounter = 0f;
136:         _coyoteCounter     = 0f;
137:     }
```

| 行 | 代码 | 含义 |
|---|---|---|
| 125 | 分节注释 | 标明起跳逻辑 |
| 127 | `private void TryJump()` | 方法名用 `Try` 前缀，是 C# 的惯例——**表示「尝试做某事，可能失败」**（对比 `ApplyHorizontalMovement` 是必然执行的） |
| 129 | 注释 | **这一行注释点出了整段设计的精髓** |
| 130 | `if (_jumpBufferCounter <= 0f \|\| _coyoteCounter <= 0f) return;` | 任何一个计时器耗尽就不跳 |
| 132 | `_rb.linearVelocity = new Vector2(_rb.linearVelocity.x, jumpForce);` | 保留水平速度，把垂直速度**直接设成** `jumpForce` |
| 135 | `_jumpBufferCounter = 0f;` | 清空缓冲，防止重复起跳 |
| 136 | `_coyoteCounter = 0f;` | 清空土狼时间，防止重复起跳 |

> ## 第 130 行：两个计时器分别代表什么
>
> 这是理解整个跳跃系统最关键的一行：
>
> | 计时器 | 代表 | 谁充满它 |
> |---|---|---|
> | `_jumpBufferCounter` | 玩家**想不想跳** | 按下跳跃键时 |
> | `_coyoteCounter` | 玩家**跳不跳得了** | 站在地上时 |
>
> **两个条件必须同时成立**，用 `||` 表达「任一不成立就不跳」：
>
> | 场景 | 缓冲 | 土狼 | 能跳吗 | 合不合直觉 |
> |---|---|---|---|---|
> | 站在地上按跳 | `> 0` | `> 0` | ✅ 跳 | 应该的 |
> | 踏空后 0.05 秒按跳 | `> 0` | `> 0` | ✅ 跳 | 土狼时间生效 |
> | 空中按跳（早就离地） | `> 0` | `≤ 0` | ❌ 不跳 | 对，不能二段跳 |
> | 落地前 0.05 秒按跳 | `> 0` | `> 0`（刚落地） | ✅ 跳 | 跳跃缓冲生效 |
> | 站着不动没按键 | `≤ 0` | `> 0` | ❌ 不跳 | 对，没按怎么跳 |
>
> > **注意 `<= 0f` 而不是 `< 0f`**：用 `<=` 表示「归零的那一刻就不能跳了」，语义更干净。用 `<` 的话，计时器恰好等于 0 时还能跳一次，边界行为会很怪。

> ## 第 132 行为什么要保留水平速度？
>
> ```csharp
> _rb.linearVelocity = new Vector2(_rb.linearVelocity.x, jumpForce);
> //                                       ^^^^^^^^^^^^^^^^^^^^  保留 x
> //                                                           ^^^^^^^^  覆盖 y
> ```
>
> 如果写成 `_rb.linearVelocity = new Vector2(0, jumpForce);`，角色一按跳就会**立刻停在原地垂直上升**——跑跳（跳跃中保持冲刺惯性）就完全做不出来了。
>
> **保留 `x` 是「跳跃不打断水平移动」这个需求的最简实现。**
>
> **为什么不写成 `_rb.linearVelocity = new Vector2(_rb.linearVelocity.x, 0)` 然后再加力？**
> 因为直接设成 `jumpForce` 的好处是**结果可预测**：不管起跳前垂直速度是多少（正在下落 -20 也好，正在上升 +5 也好），起跳后一定是 `+13`。这样跳跃高度就是稳定的，不会被「从高处落下时起跳会跳得更高」这种物理副作用干扰。

> ## 第 135、136 行：为什么必须清零
>
> `FixedUpdate` 一秒钟跑 **50 次**。如果不清零：
>
> ```
> 帧 N：  缓冲 > 0，土狼 > 0  → 起跳，verticalVelocity = 13
> 帧 N+1：缓冲 > 0（还在 0.1 秒内），土狼…… 
> ```
>
> 第 N+1 帧的土狼时间会被第 117 行重新充满吗？**不会**——因为起跳后角色已经离地，`IsGrounded` 是 `false`，走的是 `else` 递减分支。
>
> 但**缓冲计时器还有 0.08 秒的剩余**！如果不手动清零，接下来 4 个物理帧里，只要 `_coyoteCounter` 还有一点点剩余（比如起跳那一帧刚好落地又立刻离地的抖动），就会再跳一次。
>
> 清零的意义是**让这一次按键彻底作废**，杜绝边界情况。
>
> **这也解释了为什么这两个变量要在 `FixedUpdate`（50Hz）而不是 `Update`（60~144Hz）里递减**——如果递减在 `Update` 里、判断在 `FixedUpdate` 里，两个频率不一致，清零逻辑就会漏掉一些帧。

### 块 11 · 水平移动（第 139~155 行）

```csharp
139:     // ---------------- 水平移动 ----------------
140:     /// <summary>实际最大速度：优先读属性系统（职业差异走这里），没组件就用手填值</summary>
141:     private float MaxSpeed => _stats != null ? _stats.Get(StatType.MoveSpeed) : maxSpeed;
142:
143:     private void ApplyHorizontalMovement()
144:     {
145:         float targetSpeed = _input.Move.x * MaxSpeed; 
146:         float rate = Mathf.Abs(targetSpeed) > 0.01f ? acceleration : deceleration;
147:
148:         float newSpeedX = Mathf.MoveTowards(
149:             _rb.linearVelocity.x,
150:             targetSpeed,
151:             rate * Time.fixedDeltaTime
152:         );
153:
154:         _rb.linearVelocity = new Vector2(newSpeedX, _rb.linearVelocity.y);
155:     }
```

| 行 | 代码 | 含义 |
|---|---|---|
| 139 | 分节注释 | 标明水平移动逻辑 |
| 140 | 文档注释 | 说明 `MaxSpeed` 的来源优先级 |
| 141 | `private float MaxSpeed => _stats != null ? _stats.Get(StatType.MoveSpeed) : maxSpeed;` | 表达式体属性，带**三元运算符** |
| 143 | `private void ApplyHorizontalMovement()` | 每物理帧调用一次 |
| 145 | `float targetSpeed = _input.Move.x * MaxSpeed;` | 目标速度 = 输入方向 × 最大速度 |
| 146 | `float rate = Mathf.Abs(targetSpeed) > 0.01f ? acceleration : deceleration;` | 根据「有没有输入」选择用加速度还是减速度 |
| 148-152 | `Mathf.MoveTowards(...)` | 让当前速度平滑趋近目标速度 |
| 154 | `_rb.linearVelocity = new Vector2(newSpeedX, _rb.linearVelocity.y);` | 把算好的新水平速度写回刚体，保留垂直速度 |

> ## 第 141 行：职业差异是怎么走通的
>
> ```csharp
> private float MaxSpeed => _stats != null ? _stats.Get(StatType.MoveSpeed) : maxSpeed;
> ```
>
> 拆开看这个**三元运算符**（`条件 ? 值A : 值B`）：
>
> | 情况 | 返回 | 说明 |
> |---|---|---|
> | `_stats` 存在 | `_stats.Get(StatType.MoveSpeed)` | 战士 8、法师 7.5，从 `ClassData` 读来的 |
> | `_stats` 是 `null` | `maxSpeed`（第 14 行的手填值 8） | 兜底，保证没挂 `CharacterStats` 时也能动 |
>
> **这一行是整个项目「数据驱动」最直观的例子**：
>
> ```
> Class_Warrior.asset 里 moveSpeed = 8
> Class_Mage.asset    里 moveSpeed = 7.5
>          │
>          ▼
> CharacterStats.ApplyClassData() 把它们写进 _baseValues 字典
>          │
>          ▼
> _stats.Get(StatType.MoveSpeed)  返回 8 或 7.5
>          │
>          ▼
> MaxSpeed 属性返回对应的值
>          │
>          ▼
> 角色跑得快或慢
> ```
>
> **`PlayerController` 完全不知道「战士」和「法师」这两个概念存在**——它只问「移速是多少」。所以：
> - 加第三个职业 → 只新建一个 `.asset`，这个文件一行不动
> - 穿一双「+15% 移速」的鞋 → 装备系统往 `CharacterStats` 加一个修饰符，这个文件还是一行不动
> - 吃一个「移速 -30%」的减速 debuff → 同上
>
> **这就是分层架构的回报。**

> ## 第 145 行：为什么乘的是 `Move.x` 而不是整个 `Move`
>
> `Move` 是 `Vector2`（有 x 和 y）。本项目是**横板游戏**，角色只能左右移动，所以只取水平分量 `.x`。
>
> `Move.y` 在跳跃键不参与移动的情况下永远是 0（因为 `Move` 只绑定了 WASD 和方向键的左右）。但如果你以后想做「八方向移动」或者「蹲下」（按 S 降低碰撞体），`Move.y` 就派上用场了。
>
> **注意 `Move.x` 的取值范围是 -1 到 1**，所以：
> - 按 `D` → `1 * 8 = 8`，向右全速
> - 按 `A` → `-1 * 8 = -8`，向左全速
> - 不按 → `0 * 8 = 0`，目标速度是 0（减速到停）
>
> 因为 `.inputactions` 里的 2D Vector Composite 模式是 **Digital Normalized**，同时按 `A` 和 `D` 会互相抵消得到 0。

> ## 第 146 行：为什么要判断「有没有输入」
>
> ```csharp
> float rate = Mathf.Abs(targetSpeed) > 0.01f ? acceleration : deceleration;
> ```
>
> `Mathf.Abs` 是取绝对值，把 -8 变成 8。然后和 `0.01f` 比较。
>
> | `targetSpeed` | `Mathf.Abs(...) > 0.01` | 用哪个 | 效果 |
> |---|---|---|---|
> | `8`（按着 D） | `true` | `acceleration` = 60 | 加速起步 |
> | `-8`（按着 A） | `true` | `acceleration` = 60 | 加速起步 |
> | `0`（松开） | `false` | `deceleration` = 80 | 减速刹车 |
>
> **为什么要用 `0.01f` 而不是 `== 0f`？**
>
> 因为浮点数**永远不要用 `==` 比较**。`targetSpeed` 是 `_input.Move.x * MaxSpeed` 算出来的，而 `MaxSpeed` 可能是 `7.5` 这种带小数的值。理论上不按任何键时 `Move.x` 精确等于 0，乘法结果也是 0——但万一某个手柄摇杆漂移到 0.0001，`targetSpeed` 就是 0.00075，`== 0f` 会判成 `false`（走加速分支），导致角色用错误的速率减速。
>
> 用 `> 0.01f` 做「死区」判断，把摇杆漂移这种微小噪声过滤掉。**这是处理浮点数的通用做法。**

> ## 第 148 行：`Mathf.MoveTowards` 逐参数讲解
>
> ```csharp
> Mathf.MoveTowards(current, target, maxDelta)
> ```
>
> | 参数 | 本项目传的值 | 含义 |
> |---|---|---|
> | `current` | `_rb.linearVelocity.x` | **当前**水平速度 |
> | `target` | `targetSpeed` | **目标**水平速度 |
> | `maxDelta` | `rate * Time.fixedDeltaTime` | 这一次调用**最多**能变化多少 |
>
> **返回值**：向 `target` 靠近了 `maxDelta` 之后的新值。如果 `current` 和 `target` 的距离小于 `maxDelta`，直接返回 `target`（不会越过）。
>
> **具体数字**：`acceleration = 60`，`Time.fixedDeltaTime = 0.02`，所以 `maxDelta = 1.2`。
>
> ```
> 帧 1：当前 0.0  →  目标 8  →  新值 1.2
> 帧 2：当前 1.2  →  目标 8  →  新值 2.4
> 帧 3：当前 2.4  →  目标 8  →  新值 3.6
> ...
> 帧 7：当前 7.2  →  目标 8  →  新值 8.0（距离只剩 0.8 < 1.2，直接到 8）
> ```
>
> **7 个物理帧 = 0.14 秒**从静止加速到满速。这就是「起步有重量感」的来源。

> ## 为什么用 `MoveTowards` 而不是直接赋值？
>
> ```csharp
> // ❌ 直接赋值
> _rb.linearVelocity = new Vector2(targetSpeed, _rb.linearVelocity.y);
>
> // ✅ 本项目的写法
> float newSpeedX = Mathf.MoveTowards(_rb.linearVelocity.x, targetSpeed, rate * Time.fixedDeltaTime);
> ```
>
> 直接赋值的效果是：**按下 `D` 的那一帧，速度从 0 瞬间变成 8**。松开的那一帧，从 8 瞬间变成 0。
>
> 玩起来的感觉是「角色被瞬移」——像在按键盘控制一个图标，而不是控制一个有质量的角色。**在冰面上推箱子**就是这个手感。
>
> `MoveTowards` 让速度**逐帧逼近**目标值，于是起步有加速过程、刹车有减速过程。**这是「手感」最基础的来源，也是本文件第一个该讲的设计。**
>
> > **一个反直觉的例子**：很多手机游戏的角色是「瞬移式」移动的（手指一按立刻满速），因为触屏操作本身响应就慢，需要游戏来补偿。而键盘和手柄的响应已经很快了，**加上加速过程反而让操作更有质感**。同一个问题在不同平台上答案相反——这是游戏设计里很常见的情况。

> ## 第 154 行：为什么只写回 `x`
>
> ```csharp
> _rb.linearVelocity = new Vector2(newSpeedX, _rb.linearVelocity.y);
> //                                       ^^^^^^^^  ^^^^^^^^^^^^^^^^^^^^
> //                                       新算的      原样保留
> ```
>
> `linearVelocity` 是一个**整体**，赋值时必须给出完整的 `Vector2`。但水平移动的逻辑**不应该碰垂直速度**——垂直速度归重力管。
>
> 如果写成 `new Vector2(newSpeedX, 0)`，角色会**一直悬在空中**——每一物理帧都把垂直速度清零，重力根本积累不起来。
>
> **这个「只改自己负责的分量」的模式在本文件里出现两次**（这里改 x，第 132 行起跳改 y），是物理代码的基本纪律。

### 块 12 · 重力与可变跳跃高度（第 157~172 行）

```csharp
157:     // ---------------- 重力与可变跳跃高度 ----------------
158:
159:     private void ApplyGravityAndJumpCut()
160:     {
161:         // 上升和下落用不同重力：下落更快，落地更利落
162:         _rb.gravityScale = _rb.linearVelocity.y < 0f ? fallGravityScale : riseGravityScale;
163:
164:         // 还在上升时松开跳跃键 → 立刻削掉一部分上升速度，跳得就矮
165:         if (_input.ConsumeJumpReleased() && _rb.linearVelocity.y > 0f)
166:         {
167:             _rb.linearVelocity = new Vector2(
168:                 _rb.linearVelocity.x,
169:                 _rb.linearVelocity.y * jumpCutMultiplier
170:             );
171:         }
172:     }
```

| 行 | 代码 | 含义 |
|---|---|---|
| 157 | 分节注释 | 标明两个功能：切换重力 + 跳跃削减 |
| 159 | `private void ApplyGravityAndJumpCut()` | 每物理帧调用，**包括硬直期间** |
| 161 | 注释 | 说明双重力的目的 |
| 162 | `_rb.gravityScale = _rb.linearVelocity.y < 0f ? fallGravityScale : riseGravityScale;` | 根据当前垂直速度的**正负**切换重力倍率 |
| 164 | 注释 | 说明第 165 行的目的 |
| 165 | `if (_input.ConsumeJumpReleased() && _rb.linearVelocity.y > 0f)` | 松手 **且** 还在上升时，执行削减 |
| 167-170 | `_rb.linearVelocity = new Vector2(_rb.linearVelocity.x, _rb.linearVelocity.y * jumpCutMultiplier);` | 保留水平速度，把垂直速度乘 0.45 |

> ## 第 162 行：为什么用 `linearVelocity.y` 的符号判断，而不是用一个标志位？
>
> 你可能会想自己维护一个 `bool _isRising`，起跳时设 `true`、到最高点设 `false`。但那需要额外的代码去检测「什么时候到最高点」，而且容易和真实物理状态不一致。
>
> **`linearVelocity.y` 的符号本身就是最准确的答案**：
>
> | `linearVelocity.y` | 状态 | 用哪个重力 |
> |---|---|---|
> | `> 0`（正数） | 正在上升 | `riseGravityScale` = 3.5 |
> | `= 0` | 最高点（只有一瞬） | `riseGravityScale` = 3.5 |
> | `< 0`（负数） | 正在下落 | `fallGravityScale` = 6 |
>
> **物理引擎已经把状态算好了，直接读就行**——这叫「不要重复维护状态」。自己加标志位的唯一结果是：某一天标志位和真实速度不一致，产生一个极难复现的 bug。
>
> > **注意 `= 0` 时走的是 `riseGravityScale`**（因为 `< 0f` 是 `false`）。最高点那一帧用较小的重力，让滞空感稍微多一点，这是想要的效果。如果写成 `<= 0f`，最高点会用大重力，角色在顶点附近会有个轻微的「顿挫感」。

> ## 双重力的实际效果
>
> ```
> 上升阶段（gravityScale = 3.5，实际重力 ≈ 34.3）
>     │  ／＼
>     │ ／    ＼
>     │         ＼
>     │           ＼          ← 上升慢
>     │             ＼
>     │               ＼
>     │                 ＼
>     └───────────────────＼─────
>                           ＼    ← 下落快
>                             ＼
>                               ＼
> ```
>
> **为什么要这样？**
>
> | 单重力（上下一样） | 双重力（下落更快） |
> |---|---|
> | 像在水里 | 像在地球上 |
> | 落地要等很久，操作节奏拖沓 | 落地干脆，节奏紧凑 |
> | 跳跃「飘」 | 跳跃「利落」 |
>
> **现实中的重力当然是恒定的**，但游戏不需要模拟现实——它需要**在 0.5 秒内给玩家一个爽快的动作反馈**。几乎所有动作游戏都用了双重力，《超级马里奥》《蔚蓝》《死亡细胞》都是。
>
> **调参方向**：
> - 觉得跳起来太飘 → `riseGravityScale` 调大（比如 4.5）
> - 觉得落地太突然 → `fallGravityScale` 调小（比如 5）
> - **两个值差距越大，跳跃越「有弹性」**

> ## 第 165 行：可变跳跃高度的完整逻辑
>
> 两个条件用 `&&` 连接，**都成立才执行**：
>
> | 条件 | 含义 |
> |---|---|
> | `_input.ConsumeJumpReleased()` | 玩家**松开了**跳跃键 |
> | `_rb.linearVelocity.y > 0f` | 角色**还在上升** |
>
> **第二个条件为什么必须要有？**
>
> 假设玩家按跳后一直按着不松，直到角色开始下落（速度已经是 -5）才松手。如果没有 `> 0f` 这个判断：
>
> ```
> fallGravityScale 已经在下落中加速了 → 速度 -5
> 玩家松手 → 速度被乘 0.45 → -2.25
> 结果：角色下落速度突然变慢，看起来像在空中「刹车」
> ```
>
> **这是完全错误的**——玩家松手不应该影响下落。加上 `> 0f` 就表示「只有在上升途中松手才削减」，下落途中松手什么都不做。
>
> > **那 `ConsumeJumpReleased()` 不就白消费了？**
> >
> > 是的，`&&` 是短路的——如果第一个条件返回 `true` 但第二个是 `false`，事件已经被消费掉了，但没做任何事。
> >
> > **这是故意的**：那个「松开」事件本来就该被丢弃。如果留着它不消费，下一次起跳时它会莫名其妙地把新跳跃削减掉——**一个几秒前的松手动作影响了现在的跳跃**，这才是真正的 bug。

> ## 第 169 行：为什么乘 0.45 而不是设成 0
>
> ```csharp
> _rb.linearVelocity.y * jumpCutMultiplier   // 0.45
> ```
>
> | 写法 | 效果 | 评价 |
> |---|---|---|
> | `= 0`（直接归零） | 松手瞬间上升停止，像**撞到一块玻璃天花板** | ❌ 非常生硬 |
> | `* 0.45` | 上升速度变成 45%，继续上升一小段 | ✅ 自然 |
> | `* 1.0` | 什么都不做，等于没有可变跳跃高度 | ❌ 功能失效 |
> | `* -1` | 松手立刻向下弹 | ❌ 完全违背直觉 |
>
> **为什么高度不是变成 45% 而是更少？**
>
> 因为物理是平方关系：`h = v² / (2g)`。
>
> ```
> 松手后剩余的上升高度 = (0.45v)² / (2g) = 0.2 × v²/(2g) = 20% 的原始高度
> ```
>
> 所以 **`jumpCutMultiplier = 0.45` 对应的高度是原高度的 20%**。
>
> | 系数 | 最小跳跃 = 全高的 | 手感 |
> |---|---|---|
> | `1.0` | 100% | 无效果 |
> | `0.7` | 49% | 差异太小，感觉不到 |
> | **`0.45`** | **20%** | 本项目当前值，差异明显又不突兀 |
> | `0.3` | 9% | 轻点几乎不动，像按键坏了 |
>
> > **想要更大的高度跨度**（比如轻点只跳全高的 8%），把系数降到 `0.28`；**想要更小的跨度**，升到 `0.6`。
> >
> > 调这个值时直接在运行中改、看效果最快——但记得**停止运行后会还原**，要记下满意的值再填一次。

### 块 13 · 下落限速（第 174~178 行）

```csharp
174:     private void ClampFallSpeed()
175:     {
176:         if (_rb.linearVelocity.y < -maxFallSpeed)
177:             _rb.linearVelocity = new Vector2(_rb.linearVelocity.x, -maxFallSpeed);
178:     }
```

| 行 | 代码 | 含义 |
|---|---|---|
| 174 | `private void ClampFallSpeed()` | `Clamp` 是「钳制」的意思——把一个值限制在范围内 |
| 176 | `if (_rb.linearVelocity.y < -maxFallSpeed)` | 如果下落速度超过了上限（`-25`） |
| 177 | `_rb.linearVelocity = new Vector2(_rb.linearVelocity.x, -maxFallSpeed);` | 把垂直速度**钉在** `-25`，水平速度不变 |

> ## `-maxFallSpeed` 前面的负号是什么意思
>
> Unity 2D 里**向上是 y 正方向，向下是 y 负方向**。
>
> ```
>     ↑ +y
>     │
> ────┼────→ +x
>     │
>     ↓ -y
> ```
>
> 所以：
> - 上升时 `linearVelocity.y` 是正数（比如 `+13`）
> - 下落时是负数（比如 `-25`）
> - `maxFallSpeed = 25f` 是**大小**（恒为正），实际比较和赋值时要用 `-25`
>
> **第 177 行把速度「钉死」在 `-25`**，而不是「减去一点」——因为这是**上限**，不是减速度。超过上限时直接截断到上限值。

> ## 为什么需要这个上限？
>
> 角色从很高的地方掉下来，重力会一直加速：
>
> ```
> 1 秒后：down ≈ 34 单位/秒（用 fallGravityScale = 6 估算）
> 2 秒后：down ≈ 69 单位/秒
> 3 秒后：down ≈ 103 单位/秒
> ```
>
> 103 单位/秒意味着**一个物理帧（0.02 秒）移动 2 个单位**——而角色本身只有 1 个单位高。这种情况下，「穿透地面」的风险急剧上升。
>
> **本项目的防护措施有两层**：
>
> | 层 | 措施 | 作用 |
> |---|---|---|
> | 1 | `Rigidbody2D` 的 `Collision Detection` = **Continuous** | 物理引擎用扫掠检测代替离散检测，高速时也不会穿透 |
> | 2 | 本方法限制 `maxFallSpeed = 25` | 从源头把速度压在安全范围内 |
>
> **为什么有了 Continuous 还要限速？**
>
> 因为 Continuous 模式**更耗性能**，而且不是万能的——如果速度极端高（比如被 bug 导致的 1000 单位/秒），仍然可能出问题。
>
> 更重要的是：**下落速度超过某个值，玩家已经看不清角色在哪了**。25 单位/秒是「快但还能跟上」的速度。这是**游戏性**上的限制，不只是技术上的保险。

### 块 14 · 调试可视化（第 180~191 行）

```csharp
180:     // ---------------- 调试可视化 ----------------
181:
182:     // 在 Scene 视图里画出地面检测圆：站在地上是绿色，悬空是红色
183:     private void OnDrawGizmosSelected()
184:     {
185:         var col = _col != null ? _col : GetComponent<Collider2D>();
186:         if (col == null) return;
187:
188:         Vector2 p = new Vector2(col.bounds.center.x, col.bounds.min.y);
189:         Gizmos.color = Application.isPlaying && IsGrounded ? Color.green : Color.red;
190:         Gizmos.DrawWireSphere(p, groundCheckRadius);
191:     }
```

| 行 | 代码 | 含义 |
|---|---|---|
| 180-182 | 注释 | 标明这一块是调试用途，说明颜色含义 |
| 183 | `private void OnDrawGizmosSelected()` | Unity 的**编辑器专用回调**。选中这个物体时，Unity 会在 Scene 视图里调用它 |
| 185 | `var col = _col != null ? _col : GetComponent<Collider2D>();` | **兜底逻辑**：优先用缓存，缓存为空就现找 |
| 186 | `if (col == null) return;` | 连碰撞体都没有就不画（避免下一行 `null` 崩溃） |
| 188 | `Vector2 p = new Vector2(col.bounds.center.x, col.bounds.min.y);` | 算出脚底中心点，**和第 103~104 行是同一套逻辑** |
| 189 | `Gizmos.color = Application.isPlaying && IsGrounded ? Color.green : Color.red;` | 设置画笔颜色 |
| 190 | `Gizmos.DrawWireSphere(p, groundCheckRadius);` | 画一个线框球（在 2D 里看起来是圆） |

> ## `OnDrawGizmosSelected` 是什么？
>
> `Gizmos` 是 Unity 的**场景调试绘图工具**——它画出来的东西**只存在于编辑器的 Scene 视图**，不会出现在游戏画面里，也不会被打包。
>
> 两个相关的回调：
>
> | 回调 | 什么时候画 |
> |---|---|
> | `OnDrawGizmosSelected()` | **只有选中这个物体时**才画 |
> | `OnDrawGizmos()` | 场景里**所有**该脚本的实例都画 |
>
> **本项目统一用 `Selected` 版本**——因为如果一个场景里有 20 个敌人，每个都画三层感知圈，Scene 视图会变成一团糊。选中谁看谁，清爽得多。
>
> > `MeleeAttacker` 是个例外：它两个都用了——`OnDrawGizmosSelected` 画常驻框，`OnDrawGizmos` 画「攻击瞬间闪一下」的框（那个必须在不选中时也能看到）。

> ## 第 185 行：为什么要写这行兜底？
>
> ```csharp
> var col = _col != null ? _col : GetComponent<Collider2D>();
> ```
>
> `_col` 是在 `Awake` 里缓存的。但 `OnDrawGizmosSelected` **在编辑模式下也会被调用**——那时候 `Awake` 还没跑过（`Awake` 只在运行时调用），`_col` 是 `null`。
>
> **如果直接写 `_col.bounds`，你在编辑模式选中 Player 就会疯狂报 `NullReferenceException`**（Scene 视图每重绘一次报一次）。
>
> 所以这里做了兜底：编辑模式下现找一次。**多花一次 `GetComponent` 换来编辑模式可用，很划算。**

> ## 第 189 行：`Application.isPlaying` 的意义
>
> ```csharp
> Gizmos.color = Application.isPlaying && IsGrounded ? Color.green : Color.red;
> ```
>
> `Application.isPlaying` 是 `true` 表示**游戏正在运行**（点了 ▶ 且没停止）。
>
> **为什么需要它？** 因为 `IsGrounded` 是运行时才有的状态——编辑模式下 `CheckGround()` 从没执行过，`IsGrounded` 永远是默认值 `false`。
>
> 如果没有这个判断，你在编辑模式下选中 Player，圆圈永远是**红色**，看起来像是「检测不到地面」的 bug，实际上只是没在运行。
>
> 加上 `Application.isPlaying` 之后：
>
> | 状态 | 颜色 | 含义 |
> |---|---|---|
> | 编辑模式 | 🔴 红 | 「没在运行，这个圈只是给你看位置」 |
> | 运行中 + 站在地上 | 🟢 绿 | 「检测到地面了」 |
> | 运行中 + 悬空 | 🔴 红 | 「没检测到地面」 |
>
> **怎么用这个工具**：点 ▶ 运行，选中 Player，然后让角色走到平台边缘——**圆圈在踏空的那一帧变红，就说明地面检测工作正常**。
>
> 如果角色明明站在地上圆圈却是红的，那就是 `groundLayer` 没勾 `Ground`。

---

## 五、在 Unity 里怎么配

### 1. 挂到 Player 上

`Player` 物体 → `添加组件` → 搜 `Player Controller`。

> 因为脚本头有 `[RequireComponent]`，Unity 会自动把 `Rigidbody 2D` 和 `Player Input Reader` 一起加上（如果还没有的话）。

### 2. Inspector 参数（按分组）

**移动**

| 字段 | 值 | 说明 |
|---|---|---|
| `Max Speed` | `8` | **注意：这个值现在几乎不起作用了**——因为挂了 `CharacterStats`，实际速度读的是职业数据。它只在没有 `CharacterStats` 时作为兜底 |
| `Acceleration` | `60` | 起步加速度 |
| `Deceleration` | `80` | 刹车减速度 |

**跳跃**

| 字段 | 值 | 说明 |
|---|---|---|
| `Jump Force` | `13` | 起跳初速度 |
| `Rise Gravity Scale` | `3.5` | 上升重力倍率 |
| `Fall Gravity Scale` | `6` | 下落重力倍率 |
| `Jump Cut Multiplier` | `0.45` | 松手时保留的上升速度比例 |
| `Max Fall Speed` | `25` | 下落速度上限 |

**手感辅助**

| 字段 | 值 | 说明 |
|---|---|---|
| `Coyote Time` | `0.1` | 土狼时间（秒） |
| `Jump Buffer Time` | `0.1` | 跳跃缓冲（秒） |

**地面检测**

| 字段 | 值 | 说明 |
|---|---|---|
| `Ground Check Radius` | `0.05` | **不要调大**，见「踩过的坑」 |
| `Ground Layer` | 勾 **`Ground`** | ⚠️ **最关键的配置** |

### 3. 三个必须配套的组件设置

**Rigidbody2D**（这是跳跃手感的基础，一个都不能错）

| 参数 | 值 | 为什么 |
|---|---|---|
| `Body Type` | `Dynamic` | 要受重力和力，必须是动态刚体 |
| `Material` | `NoFriction` | 摩擦设成 0。默认 0.4 会让角色**贴墙时粘住掉不下来** |
| `Gravity Scale` | 任意 | **代码每物理帧都会覆盖它**（第 162 行），填什么都不影响 |
| `Collision Detection` | `Continuous` | 高速下落时不穿透地面 |
| `Interpolation` | `Interpolate` | 物理 50Hz 但渲染 60~144Hz，插值让画面平滑不抖 |
| `Constraints` | 勾 `Freeze Rotation Z` | 否则撞墙时角色会旋转 |

**CapsuleCollider2D**

| 参数 | 值 | 为什么 |
|---|---|---|
| `Direction` | `Vertical` | 竖直胶囊 |
| `Size` | `(1, 1)` | 和角色视觉大小一致 |

> **为什么用胶囊而不是方形**：地面通常由多块贴图拼成，每块有自己的碰撞体。方形碰撞体移动时会**卡在相邻碰撞体的接缝上**（角色一顿一顿的）。胶囊的圆角能顺滑滑过接缝。

**CharacterStats**（可选但强烈建议）

挂上之后，`MaxSpeed` 就会从职业数据读取。不挂的话用的是第 14 行手填的 `maxSpeed`。

### 4. 依赖的其它脚本

| 脚本 | 必需吗 | 作用 |
|---|---|---|
| `PlayerInputReader` | ✅ 必需（`RequireComponent`） | 提供输入 |
| `Rigidbody2D` | ✅ 必需（`RequireComponent`） | 物理载体 |
| `Collider2D` | ✅ 实际必需 | 地面检测要靠 `bounds`，没有会崩 |
| `CharacterStats` | ⬜ 可选 | 提供职业移速 |
| `HitReaction` | ⬜ 可选 | 提供硬直状态。没有的话 `canAct` 永远是 `true` |

---

## 六、踩过的坑

> **现象**：角色从空中落地时按跳，**看起来还没碰到地面就弹起来了**。
>
> **根因**：`groundCheckRadius` 一开始设的是 `0.15`。
>
> 这个值直接决定「什么时候算落地」——半径 0.15 意味着地面距离脚底 ≤ 0.15 单位时 `IsGrounded` 就变成 `true`。角色高 1 单位，所以是**提前 15% 身高**。放在 1080p 屏幕上（角色约 200 像素高），就是**脚底离地还有 30 像素**就已经算落地了。
>
> 再叠加跳跃缓冲——`IsGrounded` 一变 `true`，缓冲里记着的那次按键就**当帧立刻起跳**。于是角色在还差 30 像素才碰到地面时窜了上去。
>
> **解法**：收紧到 `0.05`。它仍然远大于 Unity 2D 物理的接触偏移（`defaultContactOffset` 约 0.01），不会漏判；但小到肉眼看不出提前量。
>
> **核心认知（这条比数值本身重要）**：
>
> > **容错应该由「跳跃缓冲」和「土狼时间」提供，不应该由「放宽地面检测」提供。**
>
> 把两者混在一起，`IsGrounded` 的语义就被污染了——它不再回答「我踩到东西了吗」，而是回答「我离地面近吗」。
>
> 前者的用途是唯一的（判断能不能跳），后者含糊不清：离地面 0.15 算近，那 0.16 呢？0.2 呢？一旦放宽，你就再也说不清这个变量代表什么了。
>
> **排查方法**：Scene 视图里选中 Player，看那个 Gizmos 圆圈。**贴着脚底才是对的**——拖出脚底一大截就是提前量。

> **现象**：角色能左右移动，但**完全跳不起来**，而且 Console 一句报错都没有。
>
> **根因**：`Ground Layer` 是空的。
>
> `LayerMask` 的默认值是 `Nothing`（全不勾），内部值是 0。`Physics2D.OverlapCircle(point, radius, 0)` 只检查「编号为 0 的层」——而层 0 是 `Default`，地面不在那一层。所以**永远返回 `null`**，`IsGrounded` 永远是 `false`。
>
> 更糟的是：`_coyoteCounter` 永远通过不了第 130 行的检查，所以 `TryJump()` 一次都不会执行，**Console 干干净净**。
>
> **解法**：Inspector 里把 `Ground Layer` 勾上 `Ground`。验证方法是在场景文件里搜 `m_Bits: 256`（`1 << 8 = 256`，层 8 就是 `Ground`）。
>
> **这条坑属于一个更大的类别**：
>
> > **Unity 里所有 `LayerMask` 字段留空都不会报错，只会静默失效。**
>
> 本项目的 `groundLayer`、`AttackData.targetLayers`、`EnemyController.playerLayer`、`DamageZone.targetLayers` 全都是这种。所以本项目给每一个都加了 `[Tooltip("⚠️ 必须选 XXX 层")]`。
>
> **排查优先级**：遇到「按键没反应 / 打不到人 / 检测不到地面」，**先看 Inspector 的层勾选，再看代码**。

> **现象**：把 `Time.fixedDeltaTime` 错写成 `Time.deltaTime`，角色移动速度随帧率变化——60fps 正常，144Hz 上跑得飞快。
>
> **根因**：`Time.deltaTime` 是**上一渲染帧**的耗时，而且**在 `FixedUpdate` 里读到的是固定值**——不对，实际行为比这更微妙：`Time.deltaTime` 在 `FixedUpdate` 里返回的是 `Time.fixedDeltaTime`，所以直接混用**未必立刻出错**，但语义完全错了，一旦以后把代码挪到别的地方就会爆炸。
>
> **解法**：**在哪个方法里，就用哪个 delta**。`Update` 用 `Time.deltaTime`，`FixedUpdate` 用 `Time.fixedDeltaTime`。
>
> 判断方法：看这一行代码所在的方法名。

---

## 七、如果要改，改这里

### 想加二段跳

需要在「土狼时间」之外再加一个「剩余跳跃次数」：

1. 加字段 `[SerializeField] private int maxJumps = 2;` 和 `private int _jumpsLeft;`
2. 在 `CheckGround()` 之后加：`if (IsGrounded) _jumpsLeft = maxJumps;`
3. 把第 130 行的条件改成：`if (_jumpBufferCounter <= 0f) return; if (_coyoteCounter <= 0f && _jumpsLeft <= 0) return;`
4. 起跳后 `_jumpsLeft--;`

> ⚠️ **注意别破坏可变跳跃高度**：二段跳的第二次跳跃也应该能用松手削减。因为第 165 行的逻辑和起跳是分开的，所以天然支持——**这是把「起跳」和「跳跃高度控制」拆成两个方法的意外收获**。

### 想加冲刺

1. 输入层已经就绪（`PlayerInputReader.ConsumeDashPressed()`）
2. 在本文件加字段 `dashSpeed`、`dashDuration`、`dashCooldown`、`private float _dashTimer;`
3. 在 `FixedUpdate` 的 `canAct` 分支里加 `TryDash()`
4. 冲刺期间把 `ApplyHorizontalMovement()` 跳过（用 `if (_dashTimer <= 0f)` 包住）

> ⚠️ **冲刺通常要带无敌帧**。无敌帧的逻辑在 `Health` 里（`_invincibilityTimer`），所以你需要给 `Health` 加一个 `SetInvincible(float duration)` 公开方法。
>
> ⚠️ **冲刺期间也必须清空跳跃缓冲**——否则冲刺结束时会「补跳」一下，和第 90 行解决的问题一模一样。

### 想加「下落穿平台」（按住 `S` 从单向下落的平台掉下去）

和 `DamageZone` 类似，需要在 `PlatformEffector2D` 上做文章，而不是改本文件。本文件只需要在 `CheckGround` 里**把单向平台的层排除掉**。

### 想让不同职业的跳跃高度不同

`jumpForce` 现在是 `[SerializeField]`，如果要按职业走：

1. 在 `ClassData.cs` 加一个 `jumpForce` 字段
2. 在 `StatType` 枚举加 `JumpForce`
3. 在 `CharacterStats.ApplyClassData()` 里加一行 `_baseValues[StatType.JumpForce] = data.jumpForce;`
4. 本文件第 132 行改成 `_stats.Get(StatType.JumpForce)`

> **这就是「框架优先」的价值**：加一个职业专属属性只需要动 4 个地方，全都是**加一行**，没有一处需要重构。

### 想改跳跃手感

按这张表调，**一次只改一个值**，改完立刻试：

| 你想要的效果 | 改哪个 | 方向 |
|---|---|---|
| 跳得更高 | `jumpForce` | ↑（平方关系，效果最明显） |
| 滞空更久、更飘 | `riseGravityScale` | ↓ |
| 落地更干脆 | `fallGravityScale` | ↑ |
| 轻点/长按的高度差更明显 | `jumpCutMultiplier` | ↓ |
| 边缘补跳更宽容 | `coyoteTime` | ↑（0.1 → 0.15） |
| 落地前预按更宽容 | `jumpBufferTime` | ↑（0.1 → 0.15） |
| 起步更有重量感 | `acceleration` | ↓ |
| 刹车更跟手 | `deceleration` | ↑ |
| 滑行感更强 | `deceleration` | ↓（当前 80，试 25） |

> ⚠️ **Play 模式下调的值，停止运行会全部还原。** 流程是：运行 → 调 → 记下满意的数字 → 停止 → 重新填 → `Ctrl + S`。

---

## 相关文档

- [11 · PlayerInputReader](11-PlayerInputReader.md) —— 输入从哪来，以及锁存 + 消费模式
- [02 · CharacterStats](02-CharacterStats.md) —— 第 141 行的 `MaxSpeed` 从这里取值
- [07 · HitReaction](07-HitReaction.md) —— 第 79 行的 `IsStunned` 从这里来
- [13 · EnemyController](13-EnemyController.md) —— 敌人用了同一套 `FixedUpdate` 结构和同样的「硬直期间 return」写法
