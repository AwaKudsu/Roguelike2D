# 11 · PlayerInputReader —— 输入层

## 一、头部信息表

| 项 | 内容 |
|---|---|
| 文件路径 | `Assets/_Project/Scripts/Player/PlayerInputReader.cs` |
| 所属层 | ④ 控制层 |
| 依赖 | `PlayerInputActions`（Unity 从 `.inputactions` 自动生成）、`UnityEngine.InputSystem` |
| 被谁依赖 | `PlayerController`、`MeleeAttacker` |
| 行数 | 95 行 |
| 最后更新 | Day 4 |

---

## 二、一句话定位

把键盘、手柄、鼠标的原始输入，翻译成游戏逻辑能直接读的属性。

---

## 三、为什么需要它

### 如果没有这个脚本

`PlayerController` 里会变成这样：

```csharp
private void FixedUpdate()
{
    if (Keyboard.current.aKey.isPressed) { ... }
    if (Keyboard.current.spaceKey.wasPressedThisFrame) { ... }
}
```

看起来也能跑。但三个月后你想加手柄支持，就得把每一个判断都改成：

```csharp
bool left = Keyboard.current.aKey.isPressed
         || Gamepad.current?.leftStick.left.ReadValue() > 0.5f
         || Gamepad.current?.dpad.left.isPressed == true;
```

**这段代码要写几遍？** 移动、跳跃、冲刺、攻击、技能 1、技能 2……每加一个操作就要改一遍，每个操作都要判断三种设备。

### 这个脚本的解法

把「输入从哪来」和「输入要干什么」彻底拆开：

```
键盘 / 手柄 / 鼠标
        │
        ▼
  PlayerInputActions        ← Unity 生成的中间层，负责「设备 → 动作」
        │
        ▼
  PlayerInputReader         ← 你写的这一层，负责「动作 → 属性」
        │  Move、ConsumeJumpPressed()、ConsumeAttackPressed()
        ▼
  PlayerController / MeleeAttacker   ← 只读属性，不关心设备
```

**收益**：

- 加手柄支持 → 只改 `.inputactions` 资产里绑定的按键，**代码一行不动**
- 加「技能 1」按键 → 在 `.inputactions` 加一个 Action，在 `PlayerInputReader` 加一个锁存字段和回调，**`PlayerController` 一行不动**
- 想写一个「自动演示模式」（AI 接管操作）→ 换掉这个脚本就行，运动逻辑完全不用改

> **这就是「分层」的实际价值**：它不是为了代码好看，是为了让你**少改代码**。

---

## 四、代码全解

### 块 1 · 文件头与类声明（第 1~14 行）

```csharp
 1: using UnityEngine;
 2: using UnityEngine.InputSystem;
 3:
 4: /// <summary>
 5: /// 输入层：把 Input Actions 的原始输入，翻译成游戏逻辑能直接用的属性。
 6: ///
 7: /// 好处是 PlayerController / MeleeAttacker 完全不需要知道输入来自键盘、手柄还是鼠标。
 8: /// </summary>
 9: public class PlayerInputReader : MonoBehaviour
10: {
11:     private PlayerInputActions _actions;
12:
13:     /// <summary>水平/垂直输入方向，范围 -1 ~ 1</summary>
14:     public Vector2 Move { get; private set; }
```

| 行 | 代码 | 含义 |
|---|---|---|
| 1 | `using UnityEngine;` | 引入 Unity 的基础命名空间。`MonoBehaviour`、`Vector2`、`GetComponent` 都在这里面 |
| 2 | `using UnityEngine.InputSystem;` | 引入**新输入系统**的命名空间。`InputAction`、`InputAction.CallbackContext` 在这里面。少了这一行，第 91~94 行的回调函数签名会报「找不到类型」 |
| 4-8 | `/// <summary>...</summary>` | **XML 文档注释**。它不影响运行，但鼠标悬停在类名上时会显示这段文字。这是写给自己三个月后看的 |
| 9 | `public class PlayerInputReader : MonoBehaviour` | 声明一个类，名字必须和文件名 `PlayerInputReader.cs` **完全一致**（Unity 的硬性要求，不一致就无法挂到物体上）。`: MonoBehaviour` 表示它是可以挂在 GameObject 上的组件 |
| 11 | `private PlayerInputActions _actions;` | 声明一个字段，类型是 `PlayerInputActions`——**这是 Unity 从 `.inputactions` 资产自动生成的类**，不是你手写的。`private` 表示外部访问不到。变量名前加下划线是本项目的私有字段命名约定 |
| 14 | `public Vector2 Move { get; private set; }` | 一个**自动实现的属性**（auto-property）。`Vector2` 是二维向量（x、y 两个 float）。`get; private set;` 的含义见下方 |

> **`{ get; private set; }` 是什么意思？**
>
> 它等价于手写这样一个属性：
>
> ```csharp
> private Vector2 _move;
> public Vector2 Move
> {
>     get { return _move; }
>     private set { _move = value; }
> }
> ```
>
> 写成 `{ get; private set; }` 是 C# 提供的简写，编译器自动帮你生成那个隐藏的 `_move` 字段。
>
> **两个访问级别的区别：**
> - `get` 是 `public` —— 任何脚本都能**读** `inputReader.Move`
> - `set` 是 `private` —— 只有 `PlayerInputReader` 自己**能写**
>
> **为什么不直接写 `public Vector2 Move;`？**
> 那样任何脚本都能 `inputReader.Move = Vector2.right;` 去篡改输入，`PlayerController` 就会莫名其妙地朝右走。`private set` 把「谁能改」这件事钉死在编译期——**写错了根本编译不过**。
>
> 这是「封装」在游戏项目里最实用的一个场景：**保护数据只能被合法的地方修改**。

### 块 2 · 四个锁存字段（第 16~22 行）

```csharp
16:     // 「按下」「松开」是瞬时事件，而 Update 和 FixedUpdate 的频率不同步，
17:     // 直接读 WasPressedThisFrame 可能被 FixedUpdate 整个错过。
18:     // 所以先锁存下来，等逻辑层主动「取走」—— 保证一次按键不多不少被处理一次。
19:     private bool _jumpPressedLatch;
20:     private bool _jumpReleasedLatch;
21:     private bool _attackPressedLatch;
22:     private bool _dashPressedLatch;
```

| 行 | 代码 | 含义 |
|---|---|---|
| 16-18 | 三行注释 | **这三行是本文件的核心**，讲的是「为什么要有锁存」。下面第五节会展开 |
| 19 | `private bool _jumpPressedLatch;` | 「跳跃键被按下了」这个事实的**暂存区**。`latch` 是「门闩」的意思——事件发生时就把它闩上，等有人来取才打开 |
| 20 | `private bool _jumpReleasedLatch;` | 「跳跃键被松开了」的暂存区。跳跃需要按下和松开**两个**事件，才能实现「按住跳得高、松手跳得矮」 |
| 21 | `private bool _attackPressedLatch;` | 「攻击键被按下了」的暂存区 |
| 22 | `private bool _dashPressedLatch;` | 「冲刺键被按下了」的暂存区。**注意：这个字段目前还没有任何脚本消费**——冲刺功能还没做，但输入层已经提前备好了 |

> **为什么每个事件都要单独的字段？**
>
> 你可能会想：「用一个 `bool[] pressed` 数组不就行了？」
>
> 不行。因为**跳跃需要两个事件**（按下 + 松开），而攻击和冲刺只需要一个。而且它们被不同的脚本消费——`PlayerController` 取跳跃，`MeleeAttacker` 取攻击。分开写虽然多几行，但**谁读谁的一目了然**，也避免了「A 脚本取走了 B 脚本的事件」这种灾难。

### 块 3 · Awake：创建输入资产对象（第 24~27 行）

```csharp
24:     private void Awake()
25:     {
26:         _actions = new PlayerInputActions();
27:     }
```

| 行 | 代码 | 含义 |
|---|---|---|
| 24 | `private void Awake()` | Unity 生命周期方法。**物体被创建时调用一次**（不管脚本组件是否启用），早于 `Start`、早于 `OnEnable` |
| 26 | `_actions = new PlayerInputActions();` | 用 `new` 关键字创建一个 `PlayerInputActions` 实例 |

> **这里有一个最容易误解的点：`PlayerInputActions` 不是组件，`new` 出来的是普通 C# 对象。**
>
> `MonoBehaviour`（比如 `PlayerInputReader` 自己）必须挂在 GameObject 上，只能用 `AddComponent` 创建，**不能 `new`**——你写 `new PlayerInputReader()` 会得到编译警告并且运行时出各种问题。
>
> 但 `PlayerInputActions` 继承的是 `IInputActionCollection2`，**不是** `MonoBehaviour`。它是一个纯 C# 类，就像 `List<int>` 一样，用 `new` 创建，被垃圾回收器管理。
>
> 这一点很重要，因为它决定了：
> - 它**不显示在 Inspector 里**（不是组件，没有槽位）
> - 它**不需要挂到任何物体上**
> - 它的生命周期由你的代码管理——所以第 293 行有个析构函数、第 301 行有个 `Dispose()`，都是为了防止它泄漏

> **为什么放 `Awake` 而不是 `Start`？**
>
> 因为 `OnEnable` 在 `Awake` 之后、`Start` 之前就会执行（Unity 的顺序是：所有 `Awake` → 所有 `OnEnable` → 所有 `Start`）。
>
> 第 31 行的 `_actions.Player.Enable()` 要用到 `_actions`。如果它在 `Start` 里才创建，`OnEnable` 就会对 `null` 调用方法，报 `NullReferenceException`。
>
> **规律：凡是 `OnEnable` 里要用到的东西，必须在 `Awake` 里初始化。**

### 块 4 · OnEnable：启用输入并订阅事件（第 29~37 行）

```csharp
29:     private void OnEnable()
30:     {
31:         _actions.Player.Enable();
32:
33:         _actions.Player.Jump.performed   += OnJumpPerformed;
34:         _actions.Player.Jump.canceled    += OnJumpCanceled;
35:         _actions.Player.Attack.performed += OnAttackPerformed;
36:         _actions.Player.Dash.performed   += OnDashPerformed;
37:     }
```

| 行 | 代码 | 含义 |
|---|---|---|
| 29 | `private void OnEnable()` | 生命周期方法。**每次组件被启用时调用**——物体从 `SetActive(false)` 变 `true`、或者脚本组件被打勾时都会触发 |
| 31 | `_actions.Player.Enable();` | 启用 `Player` 这个 **Action Map**（动作集合）。**不调用这一句，所有按键都不会触发任何事件**，而且不会有任何报错 |
| 33 | `_actions.Player.Jump.performed += OnJumpPerformed;` | 给 `Jump` 动作的 `performed` 事件挂一个回调函数 |
| 34 | `_actions.Player.Jump.canceled += OnJumpCanceled;` | 给 `Jump` 动作的 `canceled` 事件挂回调 |
| 35 | `_actions.Player.Attack.performed += OnAttackPerformed;` | 攻击键按下时触发 |
| 36 | `_actions.Player.Dash.performed += OnDashPerformed;` | 冲刺键按下时触发 |

> **`+=` 在做什么？**
>
> 这是 C# 的**事件订阅**语法。`InputAction.performed` 是一个**事件**（event），它内部维护一个「回调函数清单」。
>
> `+=` 就是「往清单里加一个函数」，`-=` 是「从清单里删掉一个函数」。
>
> 事件触发时，Unity 会**依次调用清单里的每一个函数**。所以你可以让多个脚本同时监听跳跃键——比如 `PlayerController` 管跳跃、另一个脚本管播放跳跃音效——它们互不干扰。

> **`.Player` 这一层是什么？**
>
> 完整链路是 `_actions`（整个输入资产）→ `.Player`（一个 Action Map）→ `.Jump`（一个 Action）→ `.performed`（一个事件）。
>
> **Action Map 是「一整套操作方案」**。现在只有一个 `Player`，但以后做 UI 菜单时你会加一个 `UI` map（上下左右导航 + 确认 + 取消），做载具时加 `Vehicle` map。
>
> 这样设计的好处是：**打开菜单时把 `Player` map 禁用、`UI` map 启用**，一行代码就能切换整套操作方案，不用担心「菜单打开着角色还在跑」。

> **`performed` 和 `canceled` 的区别**
>
> | 事件 | 什么时候触发 |
> |---|---|
> | `started` | 按钮**刚开始**被按下（还没过触发阈值） |
> | `performed` | 按钮**确认按下**了（对键盘来说，和 `started` 几乎同时） |
> | `canceled` | 按钮**被松开**了 |
>
> 本项目用了 `performed`（按下）和 `canceled`（松开），**两个都需要**——因为跳跃手感四件套里的「可变跳跃高度」要靠「松开的时机」来决定跳多高。
>
> 如果只监听 `performed`，你就只知道「按了」，不知道「按了多久」，只能实现固定高度的跳跃。

### 块 5 · OnDisable：退订并禁用输入（第 39~47 行）

```csharp
39:     private void OnDisable()
40:     {
41:         _actions.Player.Jump.performed   -= OnJumpPerformed;
42:         _actions.Player.Jump.canceled    -= OnJumpCanceled;
43:         _actions.Player.Attack.performed -= OnAttackPerformed;
44:         _actions.Player.Dash.performed   -= OnDashPerformed;
45:
46:         _actions.Player.Disable();
47:     }
```

| 行 | 代码 | 含义 |
|---|---|---|
| 39 | `private void OnDisable()` | 生命周期方法。组件被禁用、物体被销毁、场景被卸载时都会调用 |
| 41-44 | `-=` 四行 | **把块 4 里加进去的四个回调全部删掉**，和 `+=` 一一对应 |
| 46 | `_actions.Player.Disable();` | 禁用 Action Map，停止接收输入 |

> ## ⚠️ 为什么 `+=` 和 `-=` 必须成对出现
>
> 如果只写 `+=` 不写 `-=`，会发生这件事：
>
> 1. 敌人被打死，`Destroy(gameObject)` 销毁了它
> 2. 但某个事件清单里**还留着指向它的回调函数**
> 3. 下一次事件触发，Unity 尝试调用那个函数
> 4. 报错：`MissingReferenceException: The object of type 'EnemyController' has been destroyed but you are still trying to access it.`
>
> 这个错误**很难定位**，因为报错的地方是「事件触发处」（比如 `PlayerInputReader`），而不是真正出问题的脚本。
>
> **为什么用 `OnEnable`/`OnDisable` 而不是 `Start`/`OnDestroy`？**
>
> 因为 `SetActive(false)` 会触发 `OnDisable`，但**不会**触发 `OnDestroy`。如果订阅写在 `Start`、退订写在 `OnDestroy`，那么：
>
> ```
> SetActive(false)  → 物体禁用但没销毁，订阅还在
> SetActive(true)   → OnEnable 又执行一次，订阅了第二遍
> ```
>
> 结果就是**同一次按键触发了两次**。用 `OnEnable`/`OnDisable` 配对就完全没有这个问题——它们天然是一对。

### 块 6 · Update：读取连续输入（第 49~52 行）

```csharp
49:     private void Update()
50:     {
51:         Move = _actions.Player.Move.ReadValue<Vector2>();
52:     }
```

| 行 | 代码 | 含义 |
|---|---|---|
| 49 | `private void Update()` | 生命周期方法。**每渲染帧调用一次**——60fps 时每秒 60 次，144Hz 显示器上每秒 144 次 |
| 51 | `Move = _actions.Player.Move.ReadValue<Vector2>();` | 读取 `Move` 动作**当前的值**，赋给第 14 行定义的属性 |

> **`ReadValue<Vector2>()` 里的尖括号是什么？**
>
> 这是 C# 的**泛型方法调用**语法。`ReadValue<T>` 是一个泛型方法，`T` 是要读出的数据类型。
>
> 因为 `Move` 在 `.inputactions` 里配置的是 `Value` 类型 + `Control Type = Vector2`，还挂了两个 **2D Vector Composite**（WASD 和方向键），所以：
> - 按 `D` → 返回 `(1, 0)`
> - 按 `A` → 返回 `(-1, 0)`
> - 同时按 `D` 和 `W` → 返回 `(0.707, 0.707)`（**被归一化过**，防止斜着走更快）
> - 什么都不按 → 返回 `(0, 0)`
>
> 如果写成 `ReadValue<float>()`，运行时会报类型不匹配的异常。

> **为什么 `Move` 在 `Update` 里读，而不是 `FixedUpdate`？**
>
> 因为这个属性是**连续的**——按住 `D` 的整个过程中，`Update` 每秒读 60 次，`PlayerController` 每秒读 50 次（`FixedUpdate`），每次都读到 `(1, 0)`。**读多少次都一样，不会漏。**
>
> 会漏的只有**瞬时的**事件（按下、松开），那种才需要锁存。这就是下面这一块要解决的问题。

### 块 7 · 四个 Consume 方法：消费事件（第 54~87 行）

```csharp
54:     // ---------------- 事件消费 ----------------
55:     // 「取走」语义：取过一次就没有了。这样 FixedUpdate 连跑两次也不会重复消费同一个按键。
56:
57:     /// <summary>取走「按下了跳跃」事件</summary>
58:     public bool ConsumeJumpPressed()
59:     {
60:         if (!_jumpPressedLatch) return false;
61:         _jumpPressedLatch = false;
62:         return true;
63:     }
64:
65:     /// <summary>取走「松开了跳跃」事件</summary>
66:     public bool ConsumeJumpReleased()
67:     {
68:         if (!_jumpReleasedLatch) return false;
69:         _jumpReleasedLatch = false;
70:         return true;
71:     }
72:
73:     /// <summary>取走「按下了攻击」事件</summary>
74:     public bool ConsumeAttackPressed()
75:     {
76:         if (!_attackPressedLatch) return false;
77:         _attackPressedLatch = false;
78:         return true;
79:     }
80:
81:     /// <summary>取走「按下了冲刺」事件</summary>
82:     public bool ConsumeDashPressed()
83:     {
84:         if (!_dashPressedLatch) return false;
85:         _dashPressedLatch = false;
86:         return true;
87:     }
```

这四个方法**代码结构完全一样**，只有操作的字段不同。下面把第一个逐行讲透，其余三个同理。

| 行 | 代码 | 含义 |
|---|---|---|
| 54-55 | 分节注释 | 说明这一块的职责是「取走事件」，以及为什么要用「取走」而不是「读取」 |
| 57 | `/// <summary>...` | 文档注释。写在方法上面，鼠标悬停时会显示 |
| 58 | `public bool ConsumeJumpPressed()` | 公开方法，返回 `bool`。`Consume`（消费）这个词是刻意的——**它暗示「取走之后就没了」** |
| 60 | `if (!_jumpPressedLatch) return false;` | `!` 是逻辑取反。如果锁存区是 `false`（这次没人按过跳跃键），直接返回 `false`，**不做任何修改** |
| 61 | `_jumpPressedLatch = false;` | **把锁存区清空**。这就是「取走」——事件已经被消费掉了 |
| 62 | `return true;` | 告诉调用者：「是的，这次确实有一次跳跃按下，现在归你了」 |

> ## 为什么必须用「锁存 + 消费」，不能直接读 `WasPressedThisFrame`？
>
> 这是**本文件最重要的一段**，也是整个输入层存在的理由。核心矛盾是：
>
> ```
> Update()       跑在渲染帧率上 —— 60fps、144fps、或者因为突然卡顿掉到 30fps
> FixedUpdate()  跑在固定步长上 —— 每秒精确 50 次（Time.fixedDeltaTime = 0.02）
> ```
>
> **两边的节奏对不上。** 具体会出现两种灾难：
>
> ### 灾难一：按键被完全漏掉
>
> ```
> 时间轴（毫秒）
> 0    5    10   15   20   25   30
> │    │    │    │    │    │    │
> │    ▲ 玩家按下空格，1 毫秒后松开
> │    │
> ├────┴────────────────────────┤  Update 的第 N 帧（16.7ms 后才有下一帧）
>                    │
>                    ▲  FixedUpdate 在这里跑
> ```
>
> 如果 `PlayerController` 在 `FixedUpdate` 里读 `Keyboard.current.spaceKey.wasPressedThisFrame`，它查的是「**这一个渲染帧内**有没有按下」。而 `FixedUpdate` 一秒钟只跑 50 次，渲染帧跑 60~144 次——
>
> **一次只持续几毫秒的快速点击，很可能整个发生在两次 `FixedUpdate` 之间，被完全错过。** 表现就是「偶尔按了跳没反应」，而且你自己测十次可能只复现一次。
>
> ### 灾难二：一次按键被消费两次
>
> 反过来，如果某个渲染帧特别长（比如加载资源卡了 100 毫秒），这一个 `Update` 帧里会**连续跑 5 次 `FixedUpdate`**。如果按键标志位没被清掉，`TryJump()` 就会被调用 5 次——角色瞬间跳 5 倍高度。
>
> ### 解法：把「发生」和「处理」解耦
>
> ```
> 按键真的发生了
>         │
>         ▼
> OnJumpPerformed()  →  _jumpPressedLatch = true        ← 只负责「记下来」
>                                │
>                                │  不管 Update 和 FixedUpdate 怎么错位，
>                                │  这个标记一直躺在那里等着
>                                ▼
> PlayerController.FixedUpdate()
>         │  _input.ConsumeJumpPressed()                ← 主动来「取走」
>         │
>         ▼
>   返回 true 并把标记清成 false
>   → 下一次 FixedUpdate 再问，就是 false 了
> ```
>
> **「取走」这个语义同时解决了两个灾难：**
> - 不会漏：事件发生时立刻闩上，不管多久以后才有人来读，标记都还在
> - 不会重复：读过一次就清空，`FixedUpdate` 连跑五次也只有第一次拿到 `true`
>
> > **一个比喻**：这就像信箱。快递员（`Update` 线程的事件回调）把信塞进信箱就走，不管你人在不在家。你（`FixedUpdate`）回家开信箱取信，**取走了就没了**。不会因为快递员来得太频繁而收到重复的信，也不会因为你回家太晚而错过信。
>
> **这就是为什么输入层不返回「当前按键状态」，而返回「发生过的事件」。**
>
> | 数据类型 | 例子 | 读法 | 会漏吗 | 会重复吗 |
> |---|---|---|---|---|
> | **连续量** | 移动方向 | 随时随地读 `Move` | 不会 | 不会 |
> | **瞬时事件** | 按下了跳跃 | 必须锁存 + 消费 | 不会（锁存保证） | 不会（消费保证） |

> ### 剩余三个方法
>
> `ConsumeJumpReleased()`（66~71）、`ConsumeAttackPressed()`（74~79）、`ConsumeDashPressed()`（82~87）的结构和 `ConsumeJumpPressed()` **一字不差**，只是操作的字段分别是 `_jumpReleasedLatch`、`_attackPressedLatch`、`_dashPressedLatch`。
>
> 唯一的区别在语义：
>
> | 方法 | 消费的事件 | 谁在用 | 用来做什么 |
> |---|---|---|---|
> | `ConsumeJumpPressed()` | 按下跳跃 | `PlayerController.UpdateJumpTimers()` | 填充跳跃缓冲 |
> | `ConsumeJumpReleased()` | 松开跳跃 | `PlayerController.ApplyGravityAndJumpCut()` | 削减上升速度，实现可变跳跃高度 |
> | `ConsumeAttackPressed()` | 按下攻击 | `MeleeAttacker.Update()` | 触发一次攻击 |
> | `ConsumeDashPressed()` | 按下冲刺 | **目前没人用** | 冲刺功能还没做，接口先备着 |

### 块 8 · 四个事件回调（第 89~95 行）

```csharp
89:     // ---------------- 回调 ----------------
90:
91:     private void OnJumpPerformed  (InputAction.CallbackContext _) => _jumpPressedLatch  = true;
92:     private void OnJumpCanceled   (InputAction.CallbackContext _) => _jumpReleasedLatch = true;
93:     private void OnAttackPerformed(InputAction.CallbackContext _) => _attackPressedLatch = true;
94:     private void OnDashPerformed  (InputAction.CallbackContext _) => _dashPressedLatch   = true;
95: }
```

| 行 | 代码 | 含义 |
|---|---|---|
| 89 | 分节注释 | 标明这一块是「被 Unity 回调的函数」 |
| 91 | `private void OnJumpPerformed(InputAction.CallbackContext _) => _jumpPressedLatch = true;` | 跳跃键按下时被 Unity 调用，**只做一件事：把锁存标记设成 `true`** |
| 92 | 同上 | 跳跃键松开时，把 `_jumpReleasedLatch` 设成 `true` |
| 93 | 同上 | 攻击键按下，锁存 |
| 94 | 同上 | 冲刺键按下，锁存 |
| 95 | `}` | 类结束 |

> **`=>` 是什么？**
>
> 这是 C# 的**表达式体成员**（expression-bodied member）语法，是「方法体只有一行」时的简写。
>
> ```csharp
> // 这两种写法完全等价
> private void OnJumpPerformed(InputAction.CallbackContext _) => _jumpPressedLatch = true;
>
> private void OnJumpPerformed(InputAction.CallbackContext _)
> {
>     _jumpPressedLatch = true;
> }
> ```
>
> **什么时候用**：方法体只有一条语句时。超过一条就必须用大括号，否则编译不过。
>
> **注意不是「返回」的意思**：这里 `=>` 后面是**赋值语句**，不是返回值（方法返回类型是 `void`）。`=>` 在读作属性时才是「返回」，比如 `PlayerController` 里的 `private float MaxSpeed => ...;`。**同样的符号，两种含义，靠上下文区分。**

> **参数名 `_` 是什么意思？**
>
> `InputAction.CallbackContext` 是 Unity 传给回调的**上下文对象**，里面包含这次事件的详细信息：按的是哪个键、按了多久、当前值是多少……
>
> 但本脚本**不需要这些信息**——只需要知道「事件发生了」就够了，所以用不着给参数起名字。
>
> **`_` 是 C# 的「丢弃符」（discard）约定**：它告诉编译器，也告诉读代码的人，「我知道这里有个参数，但我故意不用它」。
>
> ```csharp
> // 三种写法都合法，但表达的意思不同
> private void OnJumpPerformed(InputAction.CallbackContext ctx) => _jumpPressedLatch = true;
> // ↑ 起了名字但没用，读代码的人会想「ctx 在哪用了？是不是漏了？」
>
> private void OnJumpPerformed(InputAction.CallbackContext _)   => _jumpPressedLatch = true;
> // ↑ 明确表示「我不用它」，意图清晰 ✅
> ```
>
> **⚠️ 一个真实存在的坑**：在某些 C# 版本里，同一个作用域内**出现两个以上名为 `_` 的参数会编译报错**（因为 `_` 在那时是真正的变量名而不是丢弃符）。不过本文件里四个方法各自独立、互不嵌套，所以完全没问题。
>
> 如果你以后写出 `void F(int _, string _)` 这种在同一参数列表里重复用 `_` 的代码，就会撞上这个错误——那时改成 `_a`、`_b` 或起真名字即可。

---

## 五、在 Unity 里怎么配

### 1. 挂到 Player 上

`Player` 物体 → `添加组件` → 搜 `Player Input Reader`。

> **不需要在 Inspector 里填任何东西**——这个脚本没有任何 `[SerializeField]` 字段。
>
> 因为它不配置数据，只做「翻译」。需要配置的是它依赖的 `.inputactions` 资产。

### 2. 确认依赖的 `.inputactions` 资产存在且配置正确

路径：`Assets/_Project/Settings/PlayerInputActions.inputactions`

必须满足：

| 配置项 | 必须的值 |
|---|---|
| Action Map 名字 | **`Player`**（代码里写死了 `_actions.Player`，改名会 `NullReferenceException`） |
| `Move` | Action Type = `Value`，Control Type = `Vector2`，挂两个 2D Vector Composite（WASD + 方向键） |
| `Jump` | Action Type = `Button`，绑定 `<Keyboard>/space` |
| `Dash` | Action Type = `Button`，绑定 `<Keyboard>/leftShift` |
| `Attack` | Action Type = `Button`，绑定 `<Keyboard>/j` |

### 3. 确认 `PlayerInputActions.cs` 已生成

在 Project 窗口选中 `PlayerInputActions.inputactions` → Inspector 拉到最下面 → 勾选 **`Generate C# Class`** → 点 **`Apply`**。

**验证**：`Assets/_Project/Settings/` 下应该有一个 `PlayerInputActions.cs`（约 532 行）。

> ⚠️ **改了绑定之后必须回来重新 Apply。** 否则生成的 C# 类里没有新改动，代码会报「找不到 Move」之类的错。

### 4. 确认工程启用了新输入系统

`Edit → Project Settings → Player → Other Settings → Active Input Handling`

必须是 **`Input System Package (New)`** 或 **`Both`**。

如果这里是 `Input Manager (Old)`，所有按键都不会响应，而且不会有明确报错。改动这个设置需要**重启 Unity 编辑器**。

### 5. 它会自动连接谁

`PlayerInputReader` 不主动连别人，而是**别人来找它**。同物体上的这两个脚本会自动 `GetComponent` 到它：

| 脚本 | 用它做什么 |
|---|---|
| `PlayerController` | 读 `Move`、消费跳跃按下/松开 |
| `MeleeAttacker` | 消费攻击按下 |

---

## 六、踩过的坑

> **现象**：按 `A`/`D` 完全没反应，Console 没有任何报错。
>
> **根因**：三个可能，按概率排序——
> 1. `.inputactions` 里的 Action Map 名字不是 `Player`（比如手滑改成了 `PlayerActions`）
> 2. 改了绑定但忘了点 `Apply`，生成的 `PlayerInputActions.cs` 还是旧版本
> 3. 工程设置里 `Active Input Handling` 还是 `Input Manager (Old)`
>
> **解法**：`Edit → Project Settings → Player → Other Settings → Active Input Handling` 先确认是新输入系统；然后在 Project 窗口双击 `PlayerInputActions.inputactions`，检查左栏的 Action Map 名字；最后确认 Inspector 里 `Generate C# Class` 已勾选并点过 `Apply`。

> **现象**：偶发「按了跳跃但没反应」，大概每十几次出现一次，无法稳定复现。
>
> **根因**：`PlayerController` 在 `FixedUpdate` 里读输入，而 `Update` 和 `FixedUpdate` 的频率不同步——一次极短的按键可能整个落在两次 `FixedUpdate` 之间，被完全错过。
>
> **解法**：就是本文件的锁存 + 消费模式。把「按下」这件事先存进 `_jumpPressedLatch`，等 `FixedUpdate` 主动来取。
>
> 这个坑的价值在于：**它证明了「按帧读输入」这个看起来最自然的写法是错的**。判断依据很简单——问自己一句「如果这一帧没人读，这个信息会不会永久丢失？」会的话就必须锁存。

> **现象**：一次按键触发了两次跳跃，角色跳到两倍高度。
>
> **根因**：如果 `Consume*` 方法写成「读标记但不清零」：
>
> ```csharp
> public bool JumpPressed() => _jumpPressedLatch;   // ❌ 只读不清
> ```
>
> 那么 `FixedUpdate` 一秒跑 50 次，一次按键会被读到 50 次——`TryJump()` 里的计时器清零逻辑虽然能挡住大部分，但只要缓冲计时器在某一帧被重新充满，就会再跳一次。
>
> **解法**：`Consume` 语义——**读的同时清零**。这也是方法名用 `Consume` 而不是 `Get` 或 `Is` 的原因。

> **现象**：敌人死亡后，Console 刷出一片 `MissingReferenceException`，指向的却是 `PlayerInputReader` 或 `EnemyController` 里毫不相关的一行。
>
> **根因**：`OnEnable` 里 `+=` 订阅了事件，`OnDisable` 里忘了 `-=` 退订。物体被销毁后，事件清单里还留着指向它的回调。
>
> **解法**：所有 `+=` 和 `-=` 必须成对，且必须写在 `OnEnable`/`OnDisable` 里（不能写 `Start`/`OnDestroy`，因为 `SetActive(false)` 不触发 `OnDestroy`）。

---

## 七、如果要改，改这里

### 想加一个新按键（比如 `Q` 放技能 1）

四个步骤，一个都不能少：

1. **改资产**：双击 `Assets/_Project/Settings/PlayerInputActions.inputactions` → 选中左栏 `Player` map → 中栏点 `+` 新建 Action，命名 `Skill1`，Action Type 设 `Button`，绑定 `<Keyboard>/q`
2. **重新生成**：关掉窗口 → 在 Project 窗口选中 `PlayerInputActions.inputactions` → Inspector 里点 **`Apply`**（重新生成 C# 类）
3. **加锁存字段**：在本文件第 22 行下面加 `private bool _skill1PressedLatch;`
4. **加订阅、退订、消费方法、回调**：四处都要加，照着 `_dashPressedLatch` 那套复制一遍

> **第 2 步最容易忘。** 忘了的话第 3 步会报错——因为生成的类里还没有 `Skill1` 这个 Action。

### 想支持手柄

**只改 `.inputactions` 资产，本脚本一行都不用改。**

1. 双击 `.inputactions`，选中 `Jump` 动作
2. 点它左边的 `▶` 展开，点行尾的 `+` 新增一个绑定
3. Path 填 `<Gamepad>/buttonSouth`（Xbox 的 A 键 / PS 的 ✕ 键）
4. 同理给 `Move` 加一个 `<Gamepad>/leftStick` 绑定，`Attack` 加 `<Gamepad>/buttonWest`

**这就是这个脚本存在的全部意义**——它把「设备」这件事挡在了外面。

### 想加「按键重绑定」界面

新输入系统自带这套功能，但需要在本脚本里暴露一点点东西：

```csharp
// 加一个属性，让 UI 脚本能拿到 InputAction 去做重绑定
public InputActionAsset Asset => _actions.asset;
```

然后 UI 脚本用 `action.PerformInteractiveRebinding()`。这部分属于「有余力再做」的加分项。

### 想让「自动演示模式」（AI 接管）也能复用运动逻辑

把 `PlayerController` 里读 `_input.Move` 的地方改成读一个接口：

```csharp
public interface IMoveInput { Vector2 Move { get; } }
```

让 `PlayerInputReader` 和一个未来的 `AIBrain` 都实现它。这样 `PlayerController` 完全不知道操作者是人还是 AI——**这是本文件「分层」思路的下一步延伸**。

### 想把冲刺做出来

`ConsumeDashPressed()`（第 82~87 行）已经写好了，`_dashPressedLatch`、订阅、回调也全都齐了。**输入层已经完全就绪。**

你只需要在 `PlayerController` 里写冲刺逻辑，然后在 `FixedUpdate` 的 `canAct` 分支里调用 `_input.ConsumeDashPressed()`。

> 一个小提醒：冲刺必须也在**硬直期间**消费掉（或者干脆不消费），否则硬直结束后会突然冲一下——和跳跃缓冲的「补跳」问题一模一样。

---

## 相关文档

- [15 · PlayerInputActions（自动生成）](15-PlayerInputActions自动生成.md) —— 本脚本依赖的那个自动生成的类，看它到底提供了什么
- [12 · PlayerController](12-PlayerController.md) —— 主要的消费者，看锁存的事件怎么被用掉
- [09 · MeleeAttacker](09-MeleeAttacker.md) —— 另一个消费者，只用了攻击那一个事件
