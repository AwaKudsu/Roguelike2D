# 15 · PlayerInputActions（Unity 自动生成）—— 不要手改

## 一、头部信息表

| 项 | 内容 |
|---|---|
| 文件路径 | `Assets/_Project/Settings/PlayerInputActions.cs` |
| 所属层 | 不属于任何层——它是**外部生成的胶水代码** |
| 来源 | `Assets/_Project/Settings/PlayerInputActions.inputactions`（一个 JSON 资产） |
| 生成器 | `com.unity.inputsystem:InputActionCodeGenerator` version **1.20.0** |
| 依赖 | `UnityEngine.InputSystem` |
| 被谁依赖 | `PlayerInputReader`（唯一的使用者） |
| 行数 | 532 行（其中约 **450 行是样板代码**） |
| 最后更新 | 自动生成，跟随 `.inputactions` 资产 |

---

## 二、一句话定位

把 `.inputactions` 资产里的 JSON 配置，变成可以在 C# 里点出来的强类型对象。

---

## 三、为什么需要它

### 如果没有这个类

你只能用「字符串」来访问输入动作：

```csharp
// ❌ 没有生成类时的写法
var asset = InputActionAsset.FromJson(File.ReadAllText("PlayerInputActions.inputactions"));
var jump = asset.FindAction("Player/Jump");        // 字符串！
jump.performed += ctx => _jumpPressedLatch = true;
```

**字符串的问题在三个月后才会暴露**：

| 问题 | 后果 |
|---|---|
| `"Player/Jump"` 打错了 → `"Player/Jupm"` | **编译通过**，运行时报 `NullReferenceException`，而且报错位置在 `FindAction` 内部，看不出是哪个动作 |
| 把 Action Map 从 `Player` 改名成 `PlayerActions` | **没有任何编译错误**，只是所有输入静默失效 |
| 忘了给某个 Action 挂回调 | 同上，静默失效 |
| 编辑器里没有自动补全 | 每次都要回去翻 `.inputactions` 看名字怎么拼的 |

### 这个类解决了什么

生成之后，同样的代码变成：

```csharp
// ✅ 用生成类的写法
private PlayerInputActions _actions;

_actions = new PlayerInputActions();
_actions.Player.Jump.performed += OnJumpPerformed;
//        ^^^^^^ ^^^^
//        这两层都是「点出来」的，打错一个字立刻编译报错
```

**三个直接的收益：**

| 收益 | 说明 |
|---|---|
| **编译期检查** | `_actions.Player.Junp` 直接编译不过——错误从运行时提前到写代码时 |
| **IDE 自动补全** | 输入 `.Player.` 会弹出 `Move` / `Jump` / `Dash` / `Attack` 四个选项 |
| **重命名安全** | 在 `.inputactions` 里改了 Action 名字，重新 Apply 后**所有用错的地方都会编译报错**，你会被逐个带到需要改的位置 |

> **本质：它把「字符串寻址」变成了「强类型寻址」。** 这是代码生成器最典型的用途——**在编译期消灭一整类运行时错误。**

---

## ⚠️ 最重要的三条规则

在讲代码之前，先把这三条说清楚，因为它们决定了你该怎么对待这个文件。

### 规则 1：**不要手改**

文件第 7~8 行自己写明了：

```csharp
//     Changes to this file may cause incorrect behavior and will be lost if
//     the code is regenerated.
```

**你在这里写的任何代码，都会在下次点 `Apply` 时被完全抹掉。** 而且不会有任何警告——文件被静默覆盖。

> **想加功能怎么办？**
>
> | 你想做的事 | 该改哪里 |
> |---|---|
> | 加一个按键（比如 Q 放技能） | 改 `.inputactions` 资产 → 重新 Apply |
> | 加一个新的 Action Map（比如 UI 菜单） | 同上 |
> | 封装输入逻辑、加锁存 | **改 `PlayerInputReader.cs`** ← 这才是你写代码的地方 |
> | 想把某个 Action 换个名字 | 在 `.inputactions` 里改 → 重新 Apply |
>
> **生成类只负责「把配置暴露出来」，任何逻辑都不该写在这里。**

### 规则 2：**必须提交到 git，不能 gitignore**

很多人生成代码都会 `gitignore` 掉——**这个文件是例外**。

**理由一：别人拉下项目会编译不过**

`PlayerInputReader.cs` 里写了 `new PlayerInputActions()`。如果 `PlayerInputActions.cs` 没提交，别人 clone 下来：

```
error CS0246: The type or namespace name 'PlayerInputActions' could not be found
```

**而且他没有 `.inputactions` 资产也不行**——但就算有，**Unity 也不会自动重新生成**，必须手动勾 `Generate C# Class` 再点 `Apply`。

**理由二：它和 `.inputactions` 是一对，要一起变**

```
PlayerInputActions.inputactions     ← 你编辑这个
        │  Unity 生成
        ▼
PlayerInputActions.cs               ← 但这个也要提交
```

git diff 的时候，两个文件的改动**必须一起出现**才说明是一致的。如果只提交 `.inputactions`，那么仓库里的状态是「配置改了，但代码还没跟上」——**这是一个编译不过的状态**。

> **`.gitignore` 里应该有这样的例外**（本项目的 `.gitignore` 已经处理了）：
>
> ```gitignore
> # 不忽略 meta，也不忽略 Generated 目录下的东西
> !/[Aa]ssets/**/*.meta
> ```
>
> **验证方法**：
>
> ```powershell
> git check-ignore -v Assets/_Project/Settings/PlayerInputActions.cs
> ```
>
> **没有输出**就说明没被忽略（正确）。

### 规则 3：**改了 `.inputactions` 必须重新 Apply**

这是最容易忘的一步，也是「改了绑定但不生效」的头号原因。流程：

```
双击 .inputactions 打开编辑窗口
        │
        ▼
在窗口里加 Action / 改绑定 / 改名字
        │
        ▼
关闭窗口
        │
        ▼
在 Project 窗口「单击」选中 PlayerInputActions.inputactions
        │
        ▼
Inspector 里确认 ☑ Generate C# Class 已勾选
        │
        ▼
点 Inspector 底部的 【Apply】按钮      ← ★ 最容易忘的一步
        │
        ▼
PlayerInputActions.cs 被重新生成
```

> **怎么确认真的重新生成了？** 打开 `PlayerInputActions.cs`，搜你新加的那个 Action 名字。
>
> - 搜到了 → 成功
> - 搜不到 → `Apply` 没生效，或者 `Generate C# Class` 没勾

---

## 四、代码全解

> **本节不逐行讲 532 行。** 因为其中约 450 行是**纯样板代码**——它会随着你在 `.inputactions` 里加 Action 而**自动变长**，逐行讲没有意义（今天讲完，明天加了技能键就变了）。
>
> **本节按类结构分块讲**，每块说明「它是干什么的」和「你要不要关心它」。

### 整体结构地图

先看全貌，这样你就知道每一块在整张图里的位置：

```
PlayerInputActions.cs（532 行）
│
├── 1~16 行      文件头：生成器签名（自动生成标记）
├── 18~74 行     类文档注释（一大段用法示例）
│
└── 75 行  public partial class @PlayerInputActions : IInputActionCollection2, IDisposable
    │
    ├── 80 行          public InputActionAsset asset { get; }        ← 底层资产
    │
    ├── 85~291 行      构造函数：
    │   │                ├─ 87~284 行  内嵌的 JSON（★ 你改的绑定就在这里面）
    │   │                └─ 285~290 行  按名字查找四个 Action（★ 改名会在这里崩）
    │   │
    ├── 293~296 行     析构函数（检查有没有忘记 Disable）
    ├── 301~304 行     Dispose()（★ PlayerInputReader 目前没调用它）
    │
    ├── 306~366 行     IInputActionCollection2 的一大堆转发属性/方法（纯样板）
    │
    ├── 368~374 行     private readonly InputAction m_Player_Move / _Jump / _Dash / _Attack
    │
    ├── 375~491 行     public struct PlayerActions        ← 你能点出来的那一层
    │   │              ├─ 389~401 行  四个 InputAction 属性（@Move / @Jump / @Dash / @Attack）
    │   │              ├─ 405~415 行  Get() / Enable() / Disable() / enabled
    │   │              └─ 424~490 行  AddCallbacks / UnregisterCallbacks / RemoveCallbacks / SetCallbacks
    │   │
    ├── 495 行         public PlayerActions @Player => new PlayerActions(this);   ← ★ 你用的入口
    │
    └── 501~531 行     public interface IPlayerActions     ← 可选的「接口式」回调方式
```

**你需要关心的只有三处**（图中标了 ★）：
1. 第 87~284 行的 JSON —— 你改的绑定在里面
2. 第 285~290 行的 `FindAction` —— 改了名字会在这里崩
3. 第 495 行和第 389~401 行 —— 你的 `PlayerInputReader` 用的就是这两个

**其余全部是样板。**

---

### 块 1 · 文件头：自动生成签名（第 1~16 行）

```csharp
 1: //------------------------------------------------------------------------------
 2: // <auto-generated>
 3: //     This code was auto-generated by com.unity.inputsystem:InputActionCodeGenerator
 4: //     version 1.20.0
 5: //     from Assets/_Project/Settings/PlayerInputActions.inputactions
 6: //
 7: //     Changes to this file may cause incorrect behavior and will be lost if
 8: //     the code is regenerated.
 9: // </auto-generated>
10: //------------------------------------------------------------------------------
11:
12: using System;
13: using System.Collections;
14: using System.Collections.Generic;
15: using UnityEngine.InputSystem;
16: using UnityEngine.InputSystem.Utilities;
```

| 行 | 代码 | 含义 |
|---|---|---|
| 1-10 | `// <auto-generated> ... </auto-generated>` | **约定的「自动生成」标记块**。`<auto-generated>` 是 .NET 生态里的标准标签 |
| 3 | `com.unity.inputsystem:InputActionCodeGenerator` | 生成器的包名 |
| 4 | `version 1.20.0` | **生成器版本**。跟着 Unity 输入系统包的版本走 |
| 5 | `from Assets/_Project/Settings/PlayerInputActions.inputactions` | **源头文件**。这就是「改了要重新 Apply」的那个资产 |
| 7-8 | `Changes to this file may cause incorrect behavior and will be lost if the code is regenerated.` | **官方警告：手改会丢失**（规则 1 的依据） |
| 12 | `using System;` | 用到了 `IDisposable`、`IEnumerator` |
| 13 | `using System.Collections;` | 用到了非泛型的 `IEnumerator`（第 336 行） |
| 14 | `using System.Collections.Generic;` | 用到了 `IEnumerable<InputAction>`、`List<IPlayerActions>` |
| 15 | `using UnityEngine.InputSystem;` | `InputAction`、`InputActionMap`、`InputActionAsset` 都在这 |
| 16 | `using UnityEngine.InputSystem.Utilities;` | `ReadOnlyArray<T>` 在这 |

> ## `<auto-generated>` 标记有个你可能不知道的作用
>
> 很多 IDE 和工具链会识别这个标记，然后：
>
> - **代码分析器跳过它**——不给你报「命名不符合规范」「方法太长」这类警告
> - **IDE 默认折叠它**——双击文件不会主动展开
> - **代码覆盖率工具排除它**——不把它算进测试覆盖率
>
> **本文件第 75 行的类名 `@PlayerInputActions` 就明显不符合 C# 命名规范**（正常应该叫 `PlayerInputActions`），但没有触发任何警告——就是因为有第 2 行的 `<auto-generated>`。
>
> **如果你手写一个文件然后贴上这个标记，就等于关掉了这个文件的静态检查。** 别这么干。

> ## `version 1.20.0` 这行值得注意
>
> 它记录了**生成这份代码的输入系统版本**。如果以后你升级 Unity 导致输入系统包版本变了，重新生成后这一行会变——**这是唯一能看出「这份代码是不是旧的」的线索**。
>
> 排查「输入行为诡异」时，可以先确认这个版本号和你 `Packages/manifest.json` 里的版本一致。

### 块 2 · 类文档注释与类声明（第 18~80 行）

```csharp
18: /// <summary>
19: /// Provides programmatic access to <see cref="InputActionAsset" />, ... defined in asset "Assets/_Project/Settings/PlayerInputActions.inputactions".
20: /// </summary>
21: /// <remarks>
22: /// This class is source generated and any manual edits will be discarded if the associated asset is reimported or modified.
23: /// </remarks>
24: /// <example>
25: /// <code>
26: /// using namespace UnityEngine;
...
73: /// </code>
74: /// </example>
75: public partial class @PlayerInputActions: IInputActionCollection2, IDisposable
76: {
77:     /// <summary>
78:     /// Provides access to the underlying asset instance.
79:     /// </summary>
80:     public InputActionAsset asset { get; }
```

| 行 | 代码 | 含义 |
|---|---|---|
| 18-20 | `<summary>` | 类的说明：提供对资产的编程访问 |
| 21-23 | `<remarks>` | **再次强调「手改会丢失」**（规则 1 的第二处依据） |
| 24-74 | `<example>` 和 `<code>` | **55 行的用法示例**。这是 Unity 自动生成的文档，不是你写的 |
| 75 | `public partial class @PlayerInputActions: IInputActionCollection2, IDisposable` | 类声明。**这一行信息量很大**，下面逐块拆 |
| 80 | `public InputActionAsset asset { get; }` | 底层资产。**只读属性**（只有 `get`，没有 `set`） |

> ## 第 75 行拆解：三个知识点
>
> ### ① `public` —— 必须公开
>
> 因为 `PlayerInputReader` 在**另一个文件**里 `new` 它。如果这里是 `internal` 或 `private`，就访问不到了。
>
> ### ② `partial class` —— 分部类
>
> `partial` 表示「这个类可以被拆成多个文件写」。编译时 C# 会把所有 `partial class PlayerInputActions` 的片段**合并成一个类**。
>
> **生成器用 `partial` 是留了一个后门**：你可以在另一个文件里写
>
> ```csharp
> // MyInputExtensions.cs
> public partial class PlayerInputActions
> {
>     public bool IsMoving => Player.Move.ReadValue<Vector2>().sqrMagnitude > 0.01f;
> }
> ```
>
> 这样**你加的方法不会在重新生成时丢失**——因为它在另一个文件里。
>
> > ⚠️ **本项目没有用这个技巧**。这里提一下是因为你以后可能会看到别人这么写。真要扩展，更干净的做法是写在 `PlayerInputReader` 里。
>
> ### ③ `@PlayerInputActions` 里的 `@` —— 逐字标识符
>
> `@` 是 C# 的**逐字标识符前缀**。它的作用是：**让一个关键字或者「本来不该做标识符的名字」可以被当作名字使用。**
>
> ```csharp
> int @class = 5;      // 合法：@class 是一个变量名，不是关键字 class
> int @if = 3;         // 合法
> ```
>
> **那这里的 `@PlayerInputActions` 是为什么？**
>
> 因为生成器有一个**通用规则**：**如果 Action Map 或 Action 的名字和 C# 关键字冲突，就自动加 `@`**。
>
> 本项目的 Action Map 叫 `Player`，不冲突。生成的类名刚好和 Action Map 同名——**生成器为了保险，统一给类名和 Map 名都加了 `@`**（你可以在第 378、385、393、495、501 行看到同样的 `@` 用法）。
>
> **`@PlayerInputActions` 和 `PlayerInputActions` 是同一个名字。** `@` 只是给编译器看的，不影响你使用：
>
> ```csharp
> private PlayerInputActions _actions;      // 不带 @，完全正常
> _actions = new PlayerInputActions();      // 不带 @，完全正常
> ```
>
> **本项目 `PlayerInputReader` 就是这么写的**——第 11 行和第 26 行都没有 `@`。**这完全合法。**
>
> > **如果哪天你把 Action Map 改名成 `UI`**（`UI` 不是关键字，没问题），或者改成 `default`（是关键字），生成器就会写成 `@default`。**统一加 `@` 就是为了应付这些情况，不用逐个判断。**

> ## 第 80 行：`{ get; }` 和 `{ get; private set; }` 的区别
>
> ```csharp
> public InputActionAsset asset { get; }        // 只有 get，没有 set
> ```
>
> **这是「只读自动属性」。** 它的 `set` 被彻底删掉了——不是 `private set`，是**根本不存在**。
>
> | 写法 | 外部能读 | 外部能写 | 本类能写 |
> |---|---|---|---|
> | `public X p { get; }` | ✅ | ❌ | ❌（只能构造函数里赋值） |
> | `public X p { get; private set; }` | ✅ | ❌ | ✅ |
> | `public X p;`（字段） | ✅ | ✅ | ✅ |
>
> **唯一的赋值机会是构造函数**——看第 87 行：
>
> ```csharp
> public @PlayerInputActions()
> {
>     asset = InputActionAsset.FromJson(@"...");
>     //  ↑ 这里赋值。因为这是构造函数，{ get; } 允许
> }
> ```
>
> **这是最强的不可变保证**：创建之后**任何人都改不了 `asset`**，包括类自己。
>
> > **对比 `PlayerInputReader` 第 14 行的 `public Vector2 Move { get; private set; }`**——那里需要 `private set`，因为 `Update()` 里要更新它。**判断标准：这个值创建后会不会变？**

### 块 3 · 构造函数与内嵌 JSON（第 82~291 行）

**这是整个文件里最长、也是唯一「有实际内容」的一块。**

```csharp
82:     /// <summary>
83:     /// Constructs a new instance.
84:     /// </summary>
85:     public @PlayerInputActions()
86:     {
87:         asset = InputActionAsset.FromJson(@"{
88:     ""version"": 1,
89:     ""name"": ""PlayerInputActions"",
90:     ""maps"": [
91:         {
92:             ""name"": ""Player"",
93:             ""id"": ""fc0b6129-19e8-4c24-99f7-41f8b922d2c5"",
94:             ""actions"": [
95:                 {
96:                     ""name"": ""Move"",
97:                     ""type"": ""Value"",
98:                     ""id"": ""80547ecb-8ff4-43ad-976d-f274eee7ea93"",
99:                     ""expectedControlType"": ""Vector2"",
100:                    ""processors"": """",
101:                    ""interactions"": """",
102:                    ""initialStateCheck"": true,
103:                    ""priority"": 0
104:                },
...（此处省略第 105~279 行，是其余 Action 和全部绑定，结构相同）...
280:            ]
281:        }
282:    ],
283:    ""controlSchemes"": []
284: }");
285:        // Player
286:        m_Player = asset.FindActionMap("Player", throwIfNotFound: true);
287:        m_Player_Move = m_Player.FindAction("Move", throwIfNotFound: true);
288:        m_Player_Jump = m_Player.FindAction("Jump", throwIfNotFound: true);
289:        m_Player_Dash = m_Player.FindAction("Dash", throwIfNotFound: true);
290:        m_Player_Attack = m_Player.FindAction("Attack", throwIfNotFound: true);
291:    }
```

| 行 | 代码 | 含义 |
|---|---|---|
| 85 | `public @PlayerInputActions()` | **构造函数**，和类同名，没有返回类型。你写 `new PlayerInputActions()` 时执行的就是它 |
| 87 | `asset = InputActionAsset.FromJson(@"` | 从一个 JSON 字符串创建输入资产。**注意 `@"` 开头，`");` 结尾** |
| 88-284 | 内嵌的 JSON | **你的 `.inputactions` 资产的完整拷贝**，逐字节嵌进来了 |
| 285 | `// Player` | 生成器加的注释，标明接下来是 `Player` map 的查找 |
| 286 | `m_Player = asset.FindActionMap("Player", throwIfNotFound: true);` | 按名字找到 `Player` 这个 Action Map |
| 287 | `m_Player_Move = m_Player.FindAction("Move", throwIfNotFound: true);` | 在 `Player` 里找到 `Move` 这个 Action |
| 288-290 | 同理 | `Jump` / `Dash` / `Attack` |
| 291 | `}` | 构造函数结束 |

> ## 第 87 行的 `@"` 是什么？—— 逐字字符串字面量
>
> `@"..."` 是 C# 的**逐字字符串**（verbatim string literal）。它和普通字符串 `"..."` 有两个关键区别：
>
> | | 普通字符串 `"..."` | 逐字字符串 `@"..."`
> |---|---|---|
> | 换行 | 不能直接写换行（要写 `\n`） | **可以直接跨多行** |
> | 双引号 | 直接写 `"` | **要写成 `""`**（两个双引号转义成一个） |
> | 反斜杠 | `\\` 表示一个 `\` | **直接写 `\`**，不转义 |
>
> **这就解释了第 88 行的 `""version""` 为什么是两对引号**：
>
> ```csharp
> ""version"": 1,
> ```
>
> 在逐字字符串里，`""` 表示**一个真正的双引号字符**。所以这段实际的内容是：
>
> ```json
> "version": 1,
> ```
>
> **为什么要用逐字字符串而不是普通字符串？**
>
> 因为 JSON 里有**大量双引号**。如果用普通字符串，每个 `"` 都要写成 `\"`，而且不能跨行——
>
> ```csharp
> // 普通字符串版本（噩梦）
> asset = InputActionAsset.FromJson("{\n    \"version\": 1,\n    \"name\": \"PlayerInputActions\",\n ...");
> ```
>
> **而用 `@"` 之后，内嵌的内容和原始 JSON 几乎一模一样**，可以直接对照 `.inputactions` 文件检查。

> ## 你在第 88~284 行里能看到什么
>
> 虽然不逐行讲，但你要知道几个关键位置，需要时能直接搜：
>
> | 想找什么 | 搜什么 | 大约在第几行 |
> |---|---|---|
> | Action Map 的名字 | `"name": "Player"` | 92 |
> | 四个 Action 的定义 | `"name": "Move"` / `"Jump"` / `"Dash"` / `"Attack"` | 96、106、116、126 |
> | Move 的 WASD 绑定 | `"path": "<Keyboard>/w"` | 约 150~200 |
> | Move 的方向键绑定 | `"path": "<Keyboard>/upArrow"` | 约 205~245 |
> | Jump 的绑定 | `"path": "<Keyboard>/space"` | 250 |
> | Dash 的绑定 | `"path": "<Keyboard>/leftShift"` | 261 |
> | Attack 的绑定 | `"path": "<Keyboard>/j"` | 272 |
>
> **每个绑定是一个 JSON 对象**，长这样：
>
> ```json
> {
>     "name": "",
>     "id": "923e9de8-9f50-4453-9668-e0eb535bd84b",
>     "path": "<Keyboard>/space",
>     "interactions": "",
>     "processors": "",
>     "groups": "",
>     "action": "Jump",
>     "isComposite": false,
>     "isPartOfComposite": false
> }
> ```
>
> | 字段 | 含义 |
> |---|---|
> | `id` | **GUID**，这个绑定的唯一标识。Unity 靠它追踪「同一个绑定」 |
> | `path` | 绑定到哪个物理按键。`<Keyboard>/space` 表示键盘的空格键 |
> | `action` | 属于哪个 Action |
> | `isComposite` | 是不是「复合绑定」（比如 2D Vector） |
> | `isPartOfComposite` | 是不是复合绑定的一部分 |
>
> > **⚠️ 不要手动改这里的 `id`。** GUID 是 Unity 用来追踪绑定的——改了之后，你在 `.inputactions` 编辑器里做的任何修改都可能对不上，导致绑定错乱。
> >
> > **要改绑定，永远去 `.inputactions` 编辑器里改，然后重新 Apply。**

> ## 第 286~290 行：`throwIfNotFound: true` 是什么
>
> ```csharp
> m_Player = asset.FindActionMap("Player", throwIfNotFound: true);
> ```
>
> `FindActionMap(string nameOrId, bool throwIfNotFound = false)` —— **第二个参数有默认值 `false`**。
>
> | `throwIfNotFound` | 找不到时的行为 |
> |---|---|
> | `false`（默认） | 返回 `null`，程序继续跑 → **之后某处崩在莫名其妙的地方** |
> | `true`（生成器用的） | **立刻抛异常**，报错信息直接指出是哪个名字找不到 |
>
> **为什么生成器要选 `true`？**
>
> 因为它把「失败」从「延迟崩溃」变成了「立即崩溃」，而且**崩在构造函数里**——这是最容易被发现的位置。
>
> **具体能防住什么**：
>
> 假设你在 `.inputactions` 里把 Action Map 从 `Player` 改名成 `PlayerActions`，但**忘了重新 Apply**……
>
> 不对，那样 `PlayerInputActions.cs` 根本不会变（因为它没重新生成）。真正会崩的场景是：
>
> | 场景 | 结果 |
> |---|---|
> | 改了 `.inputactions` 但没 Apply | 代码没变，跑起来一切正常（用的是旧配置）——**静默不一致** |
> | 重新生成后，构造函数里的名字是新的 | 正常 |
> | **手动改了这个文件**（违反规则 1） | 下次 Apply 被覆盖；如果 Apply 前跑，可能对不上 |
>
> > **那 `throwIfNotFound: true` 到底什么时候救你？**
> >
> > 最典型的场景是：**你手改了 `.inputactions` 的 JSON**（比如用文本编辑器直接改），或者 Unity 的生成过程出了 bug。这时构造函数的 `FindAction` 就能立刻告诉你「名字对不上」。
> >
> > **报错长这样**：
> >
> > ```
> > ArgumentException: Cannot find action 'Jump' in action map 'Player'
> > ```
> >
> > **看到这个报错的排查顺序**：① `.inputactions` 里 `Player` map 下有没有 `Jump` 这个 Action ② 是不是大小写不一致（`jump` ≠ `Jump`）。

### 块 4 · 析构函数与 Dispose（第 293~304 行）

```csharp
293:     ~@PlayerInputActions()
294:     {
295:         UnityEngine.Debug.Assert(!m_Player.enabled, "This will cause a leak and performance issues, PlayerInputActions.Player.Disable() has not been called.");
296:     }
...
301:     public void Dispose()
302:     {
303:         UnityEngine.Object.Destroy(asset);
304:     }
```

| 行 | 代码 | 含义 |
|---|---|---|
| 293 | `~@PlayerInputActions()` | **析构函数**（也叫终结器）。名字是 `~` + 类名，**没有参数、没有返回类型** |
| 295 | `UnityEngine.Debug.Assert(...)` | 如果 `m_Player` 还处于启用状态就报警告 |
| 301 | `public void Dispose()` | `IDisposable` 接口要求的方法 |
| 303 | `UnityEngine.Object.Destroy(asset);` | 销毁底层的 `InputActionAsset` |

> ## 第 293~296 行：析构函数在做什么
>
> **它没有清理任何东西**——只是**断言**（`Assert`）检查你有没有忘记调用 `Disable()`。
>
> `Debug.Assert(条件, 消息)` 的行为：
> - 条件为 `true` → 什么都不做
> - 条件为 `false` → **在 Console 打一条警告**（在编辑器和 Development Build 里）
>
> **报错消息的意思**：
>
> > 「这会导致泄漏和性能问题：`PlayerInputActions.Player.Disable()` 没有被调用。」
>
> **这是一个「防呆」设计**：输入系统为每个启用的 Action Map 注册了设备监听。如果对象被垃圾回收时 Action Map 还启用着，那些注册就泄漏了。
>
> **本项目为什么不会触发这个警告？**
>
> 因为 `PlayerInputReader` 第 46 行调了 `_actions.Player.Disable()`，而且它在 `OnDisable` 里——**物体销毁时一定会执行**。
>
> > **判断标准**：`Enable()` 和 `Disable()` 必须成对，就像 `+=` 和 `-=` 必须成对一样。
> >
> > 本项目的配对关系：
> >
> > | 启用 | 禁用 | 位置 |
> > |---|---|---|
> > | `_actions.Player.Enable()` | `_actions.Player.Disable()` | `PlayerInputReader` 第 31 / 46 行 |
> > | `Jump.performed += ...` | `Jump.performed -= ...` | `PlayerInputReader` 第 33 / 41 行 |

> ## 第 301~304 行：`Dispose()` 是本文件唯一「该调用但没被调用」的方法
>
> ```csharp
> public void Dispose()
> {
>     UnityEngine.Object.Destroy(asset);
> }
> ```
>
> `InputActionAsset.FromJson(...)` 在构造函数里创建了一个**原生对象**（`UnityEngine.Object` 的子类）。原生对象**不受 C# 垃圾回收管理**——必须显式 `Destroy`。
>
> **`Dispose()` 就是干这个的。**
>
> ### ⚠️ 本项目的 `PlayerInputReader` 没有调用它
>
> 回看 `PlayerInputReader` 的第 24~27 行和第 39~47 行：
>
> ```csharp
> private void Awake()    { _actions = new PlayerInputActions(); }
> private void OnDisable() { ...; _actions.Player.Disable(); }     // 只 Disable，没有 Dispose
> ```
>
> **这是个小遗漏。** 正确的写法应该是：
>
> ```csharp
> private void OnDestroy()
> {
>     _actions?.Dispose();
> }
> ```
>
> ### 为什么「不 Dispose」在当前情况下问题不大
>
> | 场景 | 后果 |
> |---|---|
> | 玩家物体在场景切换时被销毁 | `asset` 变成孤儿对象，**Unity 会在卸载场景时清理它** |
> | 玩家死亡后 `RunManager` 重载场景 | 同上，场景卸载会清理 |
> | 频繁创建销毁玩家物体 | **会积累泄漏** —— 但本项目不会这么做 |
>
> **本项目的玩家物体一局只创建一次**，所以实际影响很小。
>
> ### 什么时候必须补上
>
> 如果以后做**对象池**（敌人、投射物反复创建销毁），或者做**预览场景**（频繁 `Instantiate` 玩家），就必须补 `OnDestroy` 里的 `Dispose()`。
>
> > **值得现在就补上吗？**
> >
> > 值得——**两行代码，零风险**。而且它是 `IDisposable` 接口的约定，补上之后代码更「正确」。
> >
> > ```csharp
> > private void OnDestroy()
> > {
> >     _actions?.Dispose();
> > }
> > ```
> >
> > **`?.` 是空条件运算符**：如果 `_actions` 是 `null` 就不调用 `Dispose()`，直接返回。防止「`Awake` 还没跑完物体就被销毁」这种边界情况。

> ## `IDisposable` 是什么
>
> C# 里「需要释放的资源」的标准接口，只有一个方法：
>
> ```csharp
> public interface IDisposable
> {
>     void Dispose();
> }
> ```
>
> 实现了它之后就能用 **`using` 语句**，保证离开作用域时自动调用：
>
> ```csharp
> using (var actions = new PlayerInputActions())
> {
>     actions.Player.Enable();
>     // ...
> }   // ← 这里自动调用 Dispose()
> ```
>
> **本项目没用 `using`**，因为 `_actions` 的生命周期和 `PlayerInputReader` 一样长，不是「临时用完就扔」的对象。
>
> > **本项目里实现了 `IDisposable` 的还有**：没有别的了。`Health`、`CharacterStats` 那些用的是「事件 + `OnDisable` 退订」的模式，不需要 `IDisposable`。

### 块 5 · `IInputActionCollection2` 的样板转发（第 306~366 行）

```csharp
306:     /// <inheritdoc cref="UnityEngine.InputSystem.InputActionAsset.bindingMask" />
307:     public InputBinding? bindingMask
308:     {
309:         get => asset.bindingMask;
310:         set => asset.bindingMask = value;
311:     }
...
321:     public ReadOnlyArray<InputControlScheme> controlSchemes => asset.controlSchemes;
323:     /// <inheritdoc cref="UnityEngine.InputSystem.InputActionAsset.Contains(InputAction)" />
324:     public bool Contains(InputAction action)
325:     {
326:         return asset.Contains(action);
327:     }
...
341:     /// <inheritdoc cref="UnityEngine.InputSystem.InputActionAsset.Enable()" />
342:     public void Enable()
343:     {
344:         asset.Enable();
345:     }
...
353:     /// <inheritdoc cref="UnityEngine.InputSystem.InputActionAsset.bindings" />
354:     public IEnumerable<InputBinding> bindings => asset.bindings;
356:     /// <inheritdoc cref="UnityEngine.InputSystem.InputActionAsset.FindAction(string, bool)" />
357:     public InputAction FindAction(string actionNameOrId, bool throwIfNotFound = false)
358:     {
359:         return asset.FindAction(actionNameOrId, throwIfNotFound);
360:     }
...
363:     public int FindBinding(InputBinding bindingMask, out InputAction action)
364:     {
365:         return asset.FindBinding(bindingMask, out action);
366:     }
```

| 行 | 代码 | 含义 |
|---|---|---|
| 306~366 | 这一整块 | **`IInputActionCollection2` 接口要求的成员，全部转发给 `asset`** |
| 307-311 | `bindingMask` 属性 | `get => asset.bindingMask` / `set => asset.bindingMask = value` |
| 321 | `controlSchemes` | 控制方案列表（本项目是空的，见 JSON 第 283 行 `"controlSchemes": []`） |
| 324-327 | `Contains(InputAction)` | 判断某个 Action 属不属于这个集合 |
| 342-345 | `Enable()` | 启用整个资产 |
| 348-351 | `Disable()` | 禁用整个资产 |
| 354 | `bindings` | 所有绑定的列表 |
| 357-360 | `FindAction(string, bool)` | 按名字找 Action |
| 363-366 | `FindBinding(...)` | 按绑定条件查找 |

> ## 这一整块的作用：**转发（delegation）**
>
> 每个方法体都只有一行，而且都是 `asset.XXX(...)`。模式完全一样：
>
> ```
> 外部调用 actions.Enable()
>         │
>         ▼
> 转发给 asset.Enable()
>         │
>         ▼
> 真正的实现在 InputActionAsset 里
> ```
>
> **为什么要这样绕一层？**
>
> 因为 `IInputActionCollection2` 是一个**接口**，它规定「任何输入集合都要有这些方法」。`PlayerInputActions` 想实现这个接口，就得提供这些成员——但真正的功能 Unity 已经在 `InputActionAsset` 里写好了。
>
> **所以生成的代码就是「把接口要求的每个成员，转发给内部的 `asset`」**。这叫**适配器模式**（Adapter Pattern）在代码生成里的应用。
>
> > **`IInputActionCollection2` 有什么用？**
> >
> > 它让**任何输入集合都能被统一处理**。比如：
> >
> > ```csharp
> > void SetupInput(IInputActionCollection2 collection)
> > {
> >     collection.Enable();
> >     // ...
> > }
> >
> > SetupInput(playerActions);      // 传 PlayerInputActions
> > SetupInput(uiActions);          // 传另一个生成类
> > ```
> >
> > **不过本项目没用这个接口**——`PlayerInputReader` 直接调具体的方法。这是正常的：**接口的价值在「有多个实现」时才体现**，本项目只有一个输入资产。

> ## `<inheritdoc cref="..."/>` 是什么
>
> 第 306 行的 `/// <inheritdoc cref="UnityEngine.InputSystem.InputActionAsset.bindingMask" />`：
>
> **意思是「这个成员的文档，直接继承/复制 `InputActionAsset.bindingMask` 的文档」。**
>
> 好处是生成器**不用为每个转发成员重写一遍文档**——你鼠标悬停在 `actions.bindingMask` 上时，会看到 `InputActionAsset.bindingMask` 的说明。
>
> **这是「转发代码」保持简洁的标准做法。** 不写 `<inheritdoc>` 的话，就要为这 20 多个成员各写一段一模一样的注释。

> ## 第 307 行的 `InputBinding?` 和 `ReadOnlyArray<InputDevice>?`
>
> ```csharp
> public InputBinding? bindingMask { ... }
> public ReadOnlyArray<InputDevice>? devices { ... }
> ```
>
> **类型后面的 `?` 表示「可空值类型」。**
>
> `InputBinding` 是 `struct`（值类型），值类型**本身不能是 `null`**。加 `?` 之后变成 `Nullable<InputBinding>`，就可以表示「没有绑定掩码」这个状态了。
>
> | 类型 | 能装 `null` 吗 |
> |---|---|
> | `InputBinding`（结构体） | ❌ |
> | `InputBinding?` | ✅ |
> | `InputActionAsset`（类） | ✅（引用类型天生可以） |
>
> > **`?` 在 C# 里有三种用法，别搞混**：
> >
> > | 写法 | 名字 | 含义 |
> > |---|---|---|
> > | `int?` | 可空值类型 | 这个 int 可能是 null |
> > | `obj?.Method()` | 空条件运算符 | obj 为 null 就不调用 |
> > | `_input?.Length ?? 0` | `??` 空合并运算符 | 左边为 null 就用右边 |
> >
> > 本文件第 307 行用的是**第一种**。

> ## 第 330~339 行：显式接口实现（额外的知识点）
>
> ```csharp
> /// <inheritdoc cref="UnityEngine.InputSystem.InputActionAsset.GetEnumerator()" />
> public IEnumerator<InputAction> GetEnumerator()
> {
>     return asset.GetEnumerator();
> }
>
> /// <inheritdoc cref="IEnumerable.GetEnumerator()" />
> IEnumerator IEnumerable.GetEnumerator()
> {
>     return GetEnumerator();
> }
> ```
>
> **注意第 336 行的 `IEnumerator IEnumerable.GetEnumerator()` 前面没有 `public`，而是写了接口名 `IEnumerable`。**
>
> 这叫**显式接口实现**（explicit interface implementation）——意思是「这个方法只能通过 `IEnumerable` 接口来调用，不能直接 `actions.GetEnumerator()` 调」。
>
> **为什么需要两个 `GetEnumerator`？**
>
> 因为 `IEnumerable<T>` 和 `IEnumerable`（非泛型）**都要求一个叫 `GetEnumerator` 的方法**，但返回类型不同：
>
> | 接口 | 返回类型 |
> |---|---|
> | `IEnumerable<InputAction>` | `IEnumerator<InputAction>` |
> | `IEnumerable` | `IEnumerator` |
>
> **C# 不允许两个同名同参数、只有返回类型不同的方法**——所以其中一个必须用「显式接口实现」来区分。
>
> > **这带来的直接好处**：`foreach (var action in playerActions)` 能正常工作，因为编译器会找对那个泛型版本。
> >
> > **本项目没用这个功能**，但它是让 `PlayerInputActions` 可以被 `foreach` 遍历的原因。

### 块 6 · 四个 `InputAction` 字段（第 368~374 行）

```csharp
368:     // Player
369:     private readonly InputActionMap m_Player;
370:     private List<IPlayerActions> m_PlayerActionsCallbackInterfaces = new List<IPlayerActions>();
371:     private readonly InputAction m_Player_Move;
372:     private readonly InputAction m_Player_Jump;
373:     private readonly InputAction m_Player_Dash;
374:     private readonly InputAction m_Player_Attack;
```

| 行 | 代码 | 含义 |
|---|---|---|
| 368 | `// Player` | 生成器注释 |
| 369 | `private readonly InputActionMap m_Player;` | `Player` 这个 Action Map 的引用 |
| 370 | `private List<IPlayerActions> m_PlayerActionsCallbackInterfaces = new List<IPlayerActions>();` | **用接口式回调时，记录谁订阅了** |
| 371 | `private readonly InputAction m_Player_Move;` | `Move` 动作 |
| 372-374 | 同理 | `Jump` / `Dash` / `Attack` |

> ## `readonly` 是什么
>
> **`readonly` 表示「这个字段只能在声明时或构造函数里赋值，之后不能再改」。**
>
> ```csharp
> private readonly InputAction m_Player_Move;   // ✅ 只能在构造函数里赋一次
>
> // 构造函数里（第 287 行）
> m_Player_Move = m_Player.FindAction("Move", throwIfNotFound: true);   // ✅ 可以
>
> // 别的地方
> m_Player_Move = somethingElse;   // ❌ 编译错误
> ```
>
> **为什么要加？** 因为这些引用在构造函数里定好之后就永远不该变。加了 `readonly`，编译器帮你保证这一点。
>
> **和 `const` 的区别**：
>
> | | `const` | `readonly` |
> |---|---|---|
> | 赋值时机 | 声明时（编译期常量） | 声明时或构造函数（运行期） |
> | 能用于引用类型吗 | 只能用于 string 和基元类型 | ✅ 任何类型 |
> | 值存在哪 | 编译进调用方 | 运行期字段 |
>
> 这里必须用 `readonly`——`InputAction` 是引用类型，`const` 用不了。
>
> > ⚠️ **注意第 370 行的 `m_PlayerActionsCallbackInterfaces` 没有 `readonly`**——因为它需要**保持同一个 `List` 实例**（引用不变），但内容会变。加了 `readonly` 其实也行（`readonly` 只禁止重新赋值，不禁止 `List.Add`）……**这是生成器的一个小不一致**，不影响功能。

> ## 第 370 行：`IPlayerActions` 的订阅者列表
>
> 这个 `List` 记录「哪些对象通过 `AddCallbacks()` 订阅了回调」。它的用途是：
>
> - `AddCallbacks()`（第 426 行）先检查 `Contains(instance)`——**避免重复订阅**
> - `SetCallbacks()`（第 486 行）遍历它来退订所有旧的
> - `RemoveCallbacks()`（第 471 行）从里面移除
>
> **⚠️ 本项目没有用这套机制**——`PlayerInputReader` 直接用了 `+=` 订阅具体的 Action（第 33~36 行），**没有走 `AddCallbacks`**。
>
> 两种方式的区别见块 7 的说明。

### 块 7 · `PlayerActions` 结构体（第 375~491 行）

**这是你实际会用到的那一层。**

```csharp
375:     /// <summary>
376:     /// Provides access to input actions defined in input action map "Player".
377:     /// </summary>
378:     public struct PlayerActions
379:     {
380:         private @PlayerInputActions m_Wrapper;
381:
382:         /// <summary>Construct a new instance of the input action map wrapper class.</summary>
383:         /// <param name="wrapper">A reference to the class containing the input action map.</param>
384:         /// <returns>A new instance of the input action map wrapper class.</returns>
385:         public PlayerActions(@PlayerInputActions wrapper) { m_Wrapper = wrapper; }
386:         /// <summary>
387:         /// Provides access to the underlying input action "Player/Move".
388:         /// </summary>
389:         public InputAction @Move => m_Wrapper.m_Player_Move;
390:         /// <summary>
391:         /// Provides access to the underlying input action "Player/Jump".
392:         /// </summary>
393:         public InputAction @Jump => m_Wrapper.m_Player_Jump;
394:         /// <summary>
395:         /// Provides access to the underlying input action "Player/Dash".
396:         /// </summary>
397:         public InputAction @Dash => m_Wrapper.m_Player_Dash;
398:         /// <summary>
399:         /// Provides access to the underlying input action "Player/Attack".
400:         /// </summary>
401:         public InputAction @Attack => m_Wrapper.m_Player_Attack;
402:         /// <summary>Provides access to the underlying input action map instance.</summary>
403:         public InputActionMap Get() { return m_Wrapper.m_Player; }
406:         /// <inheritdoc cref="UnityEngine.InputSystem.InputActionMap.Enable()" />
407:         public void Enable() { Get().Enable(); }
408:         /// <inheritdoc cref="UnityEngine.InputSystem.InputActionMap.Disable()" />
409:         public void Disable() { Get().Disable(); }
410:         /// <inheritdoc cref="UnityEngine.InputSystem.InputActionMap.enabled" />
411:         public bool enabled => Get().enabled;
415:         public static implicit operator InputActionMap(PlayerActions set) { return set.Get(); }
...
424:         public void AddCallbacks(IPlayerActions instance) { ... }
...
449:         private void UnregisterCallbacks(IPlayerActions instance) { ... }
...
469:         public void RemoveCallbacks(IPlayerActions instance) { ... }
...
484:         public void SetCallbacks(IPlayerActions instance) { ... }
491:     }
```

| 行 | 代码 | 含义 |
|---|---|---|
| 378 | `public struct PlayerActions` | **是一个 `struct`（结构体），不是 `class`** |
| 380 | `private @PlayerInputActions m_Wrapper;` | 反向引用：持有外层的 `PlayerInputActions` |
| 385 | `public PlayerActions(@PlayerInputActions wrapper) { m_Wrapper = wrapper; }` | 构造函数，只存一个引用 |
| 389 | `public InputAction @Move => m_Wrapper.m_Player_Move;` | **`Move` 属性**——`@` 前缀的意义同前（统一加，防关键字冲突） |
| 393 | `public InputAction @Jump => m_Wrapper.m_Player_Jump;` | 同上 |
| 397 | `public InputAction @Dash => m_Wrapper.m_Player_Dash;` | 同上 |
| 401 | `public InputAction @Attack => m_Wrapper.m_Player_Attack;` | 同上 |
| 405 | `public InputActionMap Get() { return m_Wrapper.m_Player; }` | 取底层的 Action Map |
| 407 | `public void Enable() { Get().Enable(); }` | 启用这个 map |
| 409 | `public void Disable() { Get().Disable(); }` | 禁用这个 map |
| 411 | `public bool enabled => Get().enabled;` | 查询是否启用 |
| 415 | `public static implicit operator InputActionMap(PlayerActions set) { return set.Get(); }` | **隐式类型转换运算符** |
| 424~440 | `AddCallbacks(IPlayerActions instance)` | 接口式订阅 |
| 449~463 | `UnregisterCallbacks(IPlayerActions instance)` | 接口式退订（私有） |
| 469~473 | `RemoveCallbacks(IPlayerActions instance)` | 接口式退订（公开） |
| 484~490 | `SetCallbacks(IPlayerActions instance)` | 清空并重设订阅 |

> ## 第 378 行：为什么是 `struct` 而不是 `class`
>
> 看第 495 行：
>
> ```csharp
> public PlayerActions @Player => new PlayerActions(this);
> ```
>
> **每访问一次 `_actions.Player`，就 `new` 一个 `PlayerActions`。**
>
> 如果 `PlayerActions` 是 `class`（引用类型），那么：
> - 每次 `_actions.Player` 都会在**堆上分配一个对象**
> - 这些对象很快变成垃圾，**加重 GC 压力**
> - 在 `Update` 里写 `_actions.Player.Move.ReadValue<Vector2>()` 就是**每帧一次堆分配**
>
> 因为它是 `struct`（值类型）：
> - `new PlayerActions(this)` **在栈上创建，不进堆，不产生垃圾**
> - 而且它只有一个字段（一个引用），复制成本极低
> - **JIT 编译器可能把它完全优化掉**
>
> > **一个有用的记忆点**：**`struct` 适合「小、短命、值语义」的东西。** `PlayerActions` 只有 1 个字段、用完即弃，是 `struct` 的完美用例。
> >
> > 反例：`Health`、`EnemyController` 这些是 `class`——它们有生命周期、被多处引用、需要继承 `MonoBehaviour`。

> ## 第 389 行：这是你在 `PlayerInputReader` 里用的那一行
>
> ```csharp
> public InputAction @Move => m_Wrapper.m_Player_Move;
> ```
>
> 展开成完整写法：
>
> ```csharp
> public InputAction Move
> {
>     get { return m_Wrapper.m_Player_Move; }
> }
> ```
>
> **它做的事就是「把外层的私有字段暴露成公开属性」。** 一个纯粹的转发。
>
> 于是 `PlayerInputReader` 第 51 行的这条链：
>
> ```csharp
> Move = _actions.Player.Move.ReadValue<Vector2>();
> //      ^^^^^^^^ ^^^^^^ ^^^^
> //         │        │     └── 第 389 行：转发给 m_Player_Move
> //         │        └──────── 第 495 行：new PlayerActions(this)
> //         └───────────────── 第 11 行：PlayerInputReader 的字段
> ```
>
> **每一层都只是转发，最终到达第 371 行的 `m_Player_Move`**——那才是 Unity 输入系统真正的 `InputAction` 对象。
>
> > **为什么要绕这么多层？**
> >
> > 因为生成器要支持**多个 Action Map**。如果只有 `Player` 一个 map，确实不需要 `PlayerActions` 这层包装——直接 `_actions.Move` 就行。
> >
> > 但假设你有 `Player` 和 `UI` 两个 map，**两个 map 里可能都有叫 `Confirm` 的 Action**。这时包装层的价值就出来了：
> >
> > ```csharp
> > _actions.Player.Confirm    // 玩家 map 里的确认
> > _actions.UI.Confirm        // UI map 里的确认
> > ```
> >
> > **没有命名冲突。** 这就是包装层存在的理由。

> ## 第 415 行：隐式类型转换运算符
>
> ```csharp
> public static implicit operator InputActionMap(PlayerActions set) { return set.Get(); }
> ```
>
> **这一行的意思是：`PlayerActions` 可以被自动当成 `InputActionMap` 使用。**
>
> ```csharp
> InputActionMap map = _actions.Player;   // ✅ 自动转换，不用写 (InputActionMap)_actions.Player
> ```
>
> 拆解语法：
>
> | 部分 | 含义 |
> |---|---|
> | `public static` | 转换运算符必须是静态的 |
> | `implicit` | **隐式**——不需要显式写转换语法 |
> | `operator InputActionMap` | 转换的**目标类型** |
> | `(PlayerActions set)` | 源类型必须是外层类型 |
>
> **`implicit` vs `explicit`**：
>
> | | 写法 | 使用方式 |
> |---|---|---|
> | `implicit` | `implicit operator X(...)` | 自动转换，`X x = value;` |
> | `explicit` | `explicit operator X(...)` | 必须写 `X x = (X)value;` |
>
> **什么时候用 `implicit`**：转换**永远不会失败、不会丢失信息**时。这里 `PlayerActions` 内部本来就持有 `InputActionMap`，转换是安全的，所以能用 `implicit`。
>
> **什么时候用 `explicit`**：可能失败或者可能丢精度时（比如 `double` → `int`）。
>
> > **本项目没用这个功能**——`PlayerInputReader` 从来没把 `PlayerActions` 当 `InputActionMap` 用。
> >
> > **顺带回忆一下**：`PlayerController` 第 109 行用到的 `Collider2D` → `bool` 隐式转换，就是 Unity 用同一套语法定义的。

> ## 第 424~490 行：两种订阅方式
>
> 这一块提供了**「接口式订阅」**的完整实现。对比一下两种风格：
>
> ### 方式 A：本项目用的（`+=` 直接订阅）
>
> ```csharp
> // PlayerInputReader 第 33~36 行
> _actions.Player.Jump.performed   += OnJumpPerformed;
> _actions.Player.Jump.canceled    += OnJumpCanceled;
> _actions.Player.Attack.performed += OnAttackPerformed;
> _actions.Player.Dash.performed   += OnDashPerformed;
> ```
>
> **特点**：
> - 只订阅**需要的事件**（比如 `Move` 根本没订阅，因为它是连续读的）
> - 可以给**同一个 Action 的不同事件**挂**不同的方法**
> - 回调方法可以是**私有的**，不用实现接口
>
> ### 方式 B：接口式（`AddCallbacks`）
>
> ```csharp
> // 让 PlayerInputReader 实现 IPlayerActions 接口
> public class PlayerInputReader : MonoBehaviour, PlayerInputActions.IPlayerActions
> {
>     private void Awake()
>     {
>         _actions = new PlayerInputActions();
>         _actions.Player.AddCallbacks(this);       // ← 一行搞定全部订阅
>     }
>
>     // 必须实现接口的全部四个方法
>     public void OnMove(InputAction.CallbackContext context)   { }
>     public void OnJump(InputAction.CallbackContext context)   { }
>     public void OnDash(InputAction.CallbackContext context)   { }
>     public void OnAttack(InputAction.CallbackContext context) { }
> }
> ```
>
> **特点**：
> - **一行代码订阅全部** Action 的**全部**事件（`started` + `performed` + `canceled`）
> - 代价是必须实现**所有**方法（哪怕 `OnMove` 什么都不做）
> - `AddCallbacks` 内部用第 370 行的 `List` 防止重复订阅
>
> ### 本项目为什么选方式 A
>
> | 原因 | 说明 |
> |---|---|
> | **不需要 `Move` 的事件** | `Move` 是连续量，在 `Update` 里 `ReadValue` 就够了。用方式 B 会白白实现一个空的 `OnMove` |
> | **需要区分 `performed` 和 `canceled`** | 方式 B 把三个事件都塞进**同一个方法**，方法内部还要判断 `context.phase` 才知道是按下还是松开 |
> | **意图更清楚** | `Jump.performed += OnJumpPerformed` 一眼看出「这个函数负责跳跃按下」 |
>
> > **方式 B 的典型用例是「UI 菜单」**——菜单的每个按键都只需要「按下了」这一个事件，用 `AddCallbacks` 一行订阅全部，非常省事。
> >
> > **两种方式没有优劣，看需求。** 本项目是「少量按键 + 需要区分按下/松开」，所以方式 A 更合适。

### 块 8 · `IPlayerActions` 接口（第 492~531 行）

```csharp
492:     /// <summary>
493:     /// Provides a new <see cref="PlayerActions" /> instance referencing this action map.
494:     /// </summary>
495:     public PlayerActions @Player => new PlayerActions(this);
496:     /// <summary>
497:     /// Interface to implement callback methods for all input action callbacks associated with input actions defined by "Player" ...
498:     /// </summary>
501:     public interface IPlayerActions
502:     {
509:         void OnMove(InputAction.CallbackContext context);
516:         void OnJump(InputAction.CallbackContext context);
523:         void OnDash(InputAction.CallbackContext context);
530:         void OnAttack(InputAction.CallbackContext context);
531:     }
532: }
```

| 行 | 代码 | 含义 |
|---|---|---|
| 495 | `public PlayerActions @Player => new PlayerActions(this);` | **★ 最重要的入口**——你在 `PlayerInputReader` 里写的 `_actions.Player` 就是它 |
| 501 | `public interface IPlayerActions` | **嵌套在类里面的接口** |
| 509 | `void OnMove(InputAction.CallbackContext context);` | `Move` 动作的回调方法签名 |
| 516 | `void OnJump(InputAction.CallbackContext context);` | `Jump` 的 |
| 523 | `void OnDash(InputAction.CallbackContext context);` | `Dash` 的 |
| 530 | `void OnAttack(InputAction.CallbackContext context);` | `Attack` 的 |
| 532 | `}` | 类结束 |

> ## 第 495 行：`=> new PlayerActions(this)` 每访问一次就新建一个
>
> ```csharp
> public PlayerActions @Player => new PlayerActions(this);
> ```
>
> **表达式体属性是「每次读取时计算」**，所以：
>
> ```csharp
> var a = _actions.Player;   // 新建一个 PlayerActions
> var b = _actions.Player;   // 又新建一个
> bool same = a.Equals(b);   // false！（struct 的默认 Equals 比较字段……其实会是 true，见下）
> ```
>
> **为什么这样做没问题？**
>
> | 理由 | 说明 |
> |---|---|
> | `PlayerActions` 是 `struct` | 创建在栈上，**不进堆、不产生 GC 垃圾** |
> | 只有一个字段 | 复制成本 = 复制一个引用，几乎为零 |
> | 值语义 | 两个实例的 `m_Wrapper` 指向**同一个** `PlayerInputActions`，所以行为完全一致 |
>
> **所以 `_actions.Player` 重复访问是完全安全的**——`PlayerInputReader` 第 31、33、51 行各访问了一次，完全没问题。
>
> > **对比：如果 `PlayerActions` 是 `class`**
> >
> > ```csharp
> > public PlayerActions @Player => new PlayerActions(this);   // ❌ 每次访问都在堆上分配
> > ```
> >
> > 那 `_actions.Player.Move.ReadValue<Vector2>()` 放在 `Update` 里就是**每帧一次堆分配**——60fps 下每秒 60 个垃圾对象，GC 会周期性地卡顿。
> >
> > **这就是第 378 行用 `struct` 的原因。** 生成器的作者显然想过这件事。

> ## 第 501 行：嵌套接口
>
> `IPlayerActions` 定义在 `PlayerInputActions` **类的内部**，所以完整名字是：
>
> ```csharp
> PlayerInputActions.IPlayerActions
> ```
>
> **使用时要写全名**（除非加了 `using`）：
>
> ```csharp
> public class MyReader : MonoBehaviour, PlayerInputActions.IPlayerActions
> //                                    ^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^
> ```
>
> **为什么要嵌套而不是放外面？**
>
> 因为**每个 Action Map 都会生成一个自己的接口**。如果你有 `Player` 和 `UI` 两个 map，会生成 `IPlayerActions` 和 `IUIActions`。嵌套在类里避免污染全局命名空间。
>
> > **本项目只有一个 map**，所以只有 `IPlayerActions` 一个接口，而且**用不到**（因为选了方式 A）。

---

## 五、在 Unity 里怎么配

### 这个文件不需要「挂」，也不需要「配」

它**不是组件**——不挂到任何物体上，Inspector 里也找不到它。它只是一个被 `new` 出来的普通 C# 类。

**你唯一需要做的配置，是在源头资产上：**

### 步骤 1：打开 `.inputactions` 资产

Project 窗口 → `Assets/_Project/Settings/` → 双击 `PlayerInputActions.inputactions`

### 步骤 2：配置 Action Map 和 Action

| 必须满足 | 值 |
|---|---|
| Action Map 名字 | **`Player`** |
| `Move` | Type = `Value`，Control Type = `Vector2`，两个 2D Vector Composite |
| `Jump` | Type = `Button`，`<Keyboard>/space` |
| `Dash` | Type = `Button`，`<Keyboard>/leftShift` |
| `Attack` | Type = `Button`，`<Keyboard>/j` |

### 步骤 3：开启代码生成 ⚠️ 关键

1. **关闭** `.inputactions` 编辑窗口
2. 在 Project 窗口**单击选中** `PlayerInputActions.inputactions`（不是双击）
3. Inspector 面板拉到**最下面**
4. 找到 **`Generate C# Class`** 复选框 → **勾选**
5. 确认下面的 `C# Class File` 路径是 `Assets/_Project/Settings/PlayerInputActions.cs`
6. 点 **【Apply】** 按钮

### 步骤 4：验证生成成功

| 检查项 | 期望结果 |
|---|---|
| 文件存在 | `Assets/_Project/Settings/PlayerInputActions.cs` 存在 |
| 行数 | 约 532 行（会随 Action 数量变化） |
| 文件头 | 第 2 行是 `// <auto-generated>` |
| 类名 | 第 75 行有 `public partial class @PlayerInputActions` |
| 版本号 | 第 4 行的 `version` 和你的输入系统包版本一致 |

### 步骤 5：确认它被 git 追踪

```powershell
git check-ignore -v Assets/_Project/Settings/PlayerInputActions.cs
```

- **没有输出** → ✅ 正确（没被忽略）
- **有输出**（显示匹配了某条 `.gitignore` 规则）→ ❌ **必须修**，否则别人拉下来编译不过

### 步骤 6：确认工程的输入系统设置

`Edit → Project Settings → Player → Other Settings → Active Input Handling`

必须是 **`Input System Package (New)`** 或 **`Both`**。

> 如果这里是 `Input Manager (Old)`，就算代码全部正确、编译全部通过，**按键也不会有任何反应**，而且 Console 干净。改动这个设置**需要重启 Unity 编辑器**。

---

## 六、踩过的坑

> **现象**：新建了两个 C# 脚本，保存后 Console 报一片红：
>
> ```
> Assets\_Project\Scripts\Player\PlayerInputReader.cs(3,14): error CS0101:
> The namespace '<global namespace>' already contains a definition for 'NewEmptyCSharpScript'
> ```
>
> **根因**：**和本文件无关，但和「生成代码」这件事有关。**
>
> 用 Unity 的 `右键 → Create → C# Script` 新建脚本时，模板里的类名**默认就是 `NewEmptyCSharpScript`**：
>
> ```csharp
> public class NewEmptyCSharpScript : MonoBehaviour   // ← 模板默认
> {
> }
> ```
>
> 如果你新建两个脚本、**都忘了改类名**，那么：
>
> ```csharp
> // PlayerInputReader.cs
> public class NewEmptyCSharpScript : MonoBehaviour { }
>
> // PlayerController.cs
> public class NewEmptyCSharpScript : MonoBehaviour { }   // ❌ 重名了
> ```
>
> **C# 的规则是：同一个命名空间下不能有两个同名类。** 这两个类都没写 `namespace`，所以都在「全局命名空间」（`<global namespace>`）里，直接冲突。
>
> **解法**：把每个脚本的类名改成和文件名一致。
>
> **教训与关联**：
>
> | 教训 | 说明 |
> |---|---|
> | **新建脚本后第一件事是改类名** | 而且要**全选替换**，不能只改一处 |
> | **文件名和类名必须一致** | 这是 Unity 的硬性要求——不一致就无法把脚本挂到物体上 |
> | **`CS0101` 是「定义重复」错误** | 看到它就去搜哪个类名出现两次 |
>
> > **和本文件的关系**：`PlayerInputActions.cs` 里的 `public partial class @PlayerInputActions` **绝对不会**和别人冲突，因为：
> > - 类名加 `@` 前缀（第 75 行），几乎不可能和你手写的类重名
> > - `partial` 允许它被拆成多个文件（虽然这里只有一份）
> >
> > **这也是「生成的代码用奇怪的名字」的一个隐含好处**——降低撞名概率。

> **现象**：在 `.inputactions` 里加了一个新的 `Attack` 按键绑定，运行游戏却完全没反应。
>
> **根因**：**只改了资产，没重新生成 C# 类。**
>
> 这个坑有两种表现，要分清：
>
> | 你改了什么 | 没 Apply 的后果 |
> |---|---|
> | 加/改**绑定**（比如给 Jump 加一个手柄键） | **代码不用变**（生成类不记录具体键位），但…… |
> | 加/改**Action 或 Map 的名字** | 生成类里还是旧名字 → 你的代码按旧名字点，编译通过，但 `.inputactions` 里已经没有那个名字了 |
>
> **第一种情况的真相**：绑定信息是**运行时从内嵌 JSON 读的**。所以理论上改了绑定、没 Apply，游戏里用的还是**旧 JSON**（因为 `PlayerInputActions.cs` 里嵌的是旧副本）——**改了等于没改**。
>
> **解法**：
> 1. 关掉 `.inputactions` 窗口
> 2. 选中 `PlayerInputActions.inputactions`
> 3. 确认 `Generate C# Class` 已勾选
> 4. **点【Apply】**
> 5. 打开 `PlayerInputActions.cs`，**搜那个按键的 path**（比如 `<Gamepad>/buttonSouth`）——搜到了就说明真的重新生成了
>
> **教训**：**`.inputactions` 和 `PlayerInputActions.cs` 是一对，永远一起变。** 判断它们是否同步的最快方法：搜一下你刚加的字符串在 `.cs` 里有没有。

> **现象**：运行时报
>
> ```
> ArgumentException: Cannot find action 'Jump' in action map 'Player'
> ```
>
> **根因**：`.inputactions` 里的 Action 名字和生成类里查的名字对不上。常见于：
> - 手动改了 `.inputactions` 的 JSON
> - 改了 Action 名字但生成过程出错
> - **同一个工程里有两个 `PlayerInputActions` 资产**，Unity 用错了那个
>
> **解法**：打开 `.inputactions`，检查 `Player` map 下确实有 `Jump` 这个 Action，注意**大小写**（`jump` ≠ `Jump`）。然后重新 Apply。
>
> **教训**：这个报错之所以清晰，是因为第 286~290 行都传了 `throwIfNotFound: true`。**如果生成器用了默认的 `false`，你会得到「某个地方莫名其妙 NullReference」，定位难度高十倍。**
>
> > **这是一个可以学的设计**：**「找不到就立刻抛异常」永远比「返回 null 让调用方后面崩」好。** 报错位置离原因越近，修复越快。

> **现象**：Console 出现警告
>
> ```
> This will cause a leak and performance issues, PlayerInputActions.Player.Disable() has not been called.
> ```
>
> **根因**：`PlayerInputActions` 对象被垃圾回收时，它的 Action Map 还处于启用状态。这来自第 293~296 行的析构函数检查。
>
> 本项目出现这个警告的原因通常是：**`PlayerInputReader` 被销毁时，`OnDisable` 没有正确执行**（比如脚本组件被直接移除，而不是物体被销毁）。
>
> **解法**：确认 `PlayerInputReader` 第 39~47 行的 `OnDisable` 正常执行，特别是第 46 行的 `_actions.Player.Disable()` 不能被删掉。
>
> **教训**：`Enable()` 和 `Disable()` 必须成对——**和 `+=` / `-=` 是同一个道理**。

---

## 七、如果要改，改这里

### ⚠️ 首先：这个文件你不该改

任何修改都会在下次 `Apply` 时丢失。**要改的是这三处之一：**

| 你的需求 | 改哪里 |
|---|---|
| 加/改按键绑定 | `.inputactions` 资产 → 重新 Apply |
| 加/改 Action 或 Map 名字 | 同上 |
| 加输入逻辑（锁存、消费、组合键） | **`PlayerInputReader.cs`** |

---

### 想加技能按键（`Q` 放技能 1、`E` 放技能 2）

**完整流程（五步，一步都不能少）：**

**① 打开资产**
双击 `Assets/_Project/Settings/PlayerInputActions.inputactions`

**② 加 Action**
在 `Player` map 的 Actions 列表点 `+`，新建：
- 名字：`Skill1`
- Action Type：`Button`
- 展开后点 `+` 加绑定，Path 选 `<Keyboard>/q`

同样再加一个 `Skill2` → `<Keyboard>/e`

**③ 重新生成**
关窗口 → 选中 `PlayerInputActions.inputactions` → 确认 `Generate C# Class` 已勾 → 点 **【Apply】**

**④ 验证生成类已更新**
打开 `PlayerInputActions.cs`，搜 `Skill1`。应该能在这些位置找到它：
- 内嵌 JSON 里（`"name": "Skill1"`）
- 构造函数里（`m_Player_Skill1 = m_Player.FindAction("Skill1", throwIfNotFound: true);`）
- 字段声明里（`private readonly InputAction m_Player_Skill1;`）
- `PlayerActions` 结构体里（`public InputAction @Skill1 => m_Wrapper.m_Player_Skill1;`）
- `IPlayerActions` 接口里（`void OnSkill1(InputAction.CallbackContext context);`）

**⑤ 在 `PlayerInputReader` 里加四样东西**

照着 `_dashPressedLatch` 那一套复制：

```csharp
// 1. 锁存字段
private bool _skill1PressedLatch;
private bool _skill2PressedLatch;

// 2. OnEnable 里订阅
_actions.Player.Skill1.performed += OnSkill1Performed;
_actions.Player.Skill2.performed += OnSkill2Performed;

// 3. OnDisable 里退订（千万不能忘）
_actions.Player.Skill1.performed -= OnSkill1Performed;
_actions.Player.Skill2.performed -= OnSkill2Performed;

// 4. 消费方法
public bool ConsumeSkill1Pressed()
{
    if (!_skill1PressedLatch) return false;
    _skill1PressedLatch = false;
    return true;
}

// 5. 回调
private void OnSkill1Performed(InputAction.CallbackContext _) => _skill1PressedLatch = true;
```

> **注意第 ④ 步的验证不是可选的。** 如果搜不到 `Skill1`，第 ⑤ 步会编译报错——因为生成类里根本没有这个成员。
>
> **这个报错是好事的**：它说明「编译期检查」在起作用。如果用的是字符串访问，你会等到运行游戏才发现按 Q 没反应。

---

### 想接入手柄

**改资产就行，`PlayerInputActions.cs` 会自动跟着变（重新 Apply 后），`PlayerInputReader` 一行都不用改。**

步骤：
1. 双击 `.inputactions`
2. 选中 `Jump`，展开它，点绑定行的 `+`
3. Path 选 `<Gamepad>/buttonSouth`（Xbox A / PS ✕）
4. 给 `Move` 加 `<Gamepad>/leftStick`，`Attack` 加 `<Gamepad>/buttonWest`
5. 关闭窗口 → 选中资产 → **Apply**

> **为什么 `PlayerInputReader` 不用改？**
>
> 因为它读的永远是 `_actions.Player.Jump` 这个**抽象动作**，而不是「空格键」。**键位到动作的映射完全在资产里**——这正是输入层存在的全部意义。
>
> **这就是生成类的价值**：加设备支持 = 改配置 + 点一下 Apply，**零代码改动**。

---

### 想加一个 UI 菜单的 Action Map

1. 双击 `.inputactions`
2. 左上角 Action Maps 栏点 `+`，命名 `UI`
3. 在里面加 `Navigate`（Value/Vector2）、`Confirm`（Button/`<Keyboard>/enter`）、`Cancel`（Button/`<Keyboard>/escape`）
4. **Apply**
5. 生成类里会多出：
   - `public UIActions @UI => new UIActions(this);`
   - `public interface IUIActions { ... }`
   - 一整套 `m_UI_*` 字段

**然后就能这样切换整套操作方案：**

```csharp
// 打开菜单
_actions.Player.Disable();
_actions.UI.Enable();

// 关闭菜单
_actions.UI.Disable();
_actions.Player.Enable();
```

> **⚠️ 加完之后，`PlayerInputActions.cs` 的行数会明显增加**（每个 map 都会生成一整套样板）。这时本文件的「代码全解」要用**同样的分块思路**去看，而不是被行数吓到——**结构永远是那八个块，只是块 6、7、8 会各多一份。**

---

### 想改生成位置或类名

在 Inspector 里（选中 `.inputactions` 后）：

| 字段 | 作用 |
|---|---|
| `C# Class File` | 生成到哪个路径。默认和 `.inputactions` 同目录 |
| `C# Class Name` | 生成类叫什么名字。**改了这个，所有代码里的 `PlayerInputActions` 都要跟着改** |

> **建议不要改。** 默认值和资产名一致，最省事。改了 `C# Class Name` 之后，`PlayerInputReader` 第 11、26 行会立刻编译报错——**编译器会带你去改，不会漏**。

---

## 相关文档

- [11 · PlayerInputReader](11-PlayerInputReader.md) —— **唯一的消费者**。看它怎么用 `_actions.Player.Jump.performed`，以及为什么需要锁存 + 消费
- [12 · PlayerController](12-PlayerController.md) —— 间接消费者，通过 `PlayerInputReader` 拿输入
- [09 · MeleeAttacker](09-MeleeAttacker.md) —— 另一个间接消费者，只用了 `ConsumeAttackPressed()`
