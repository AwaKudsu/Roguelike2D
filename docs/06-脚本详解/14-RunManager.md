# 14 · RunManager

## 一、头部信息表

| 项 | 内容 |
|---|---|
| 文件路径 | `Assets/_Project/Scripts/Core/RunManager.cs` |
| 所属层 | ⑤ 流程层 |
| 依赖 | `Health`（订阅它的 `Died` 事件）、`UnityEngine.SceneManagement` |
| 被谁依赖 | 无。将来的 UI（死亡结算界面）会读它的 `IsPlayerDead` |
| 行数 | 64 行 |
| 最后更新 | Day 4 |

---

## 二、一句话定位

一局游戏的生命周期管理：玩家死亡 → 停顿一下 → 重开场景。

---

## 三、为什么需要它

### 它填上了循环的最后一环

在它出现之前，游戏的循环是**断的**：

```
打敌人 → 敌人死 → 然后呢？
被打 → 掉血 → 然后呢？
```

玩家血量归零时，`Health` 会触发 `Died` 事件——**但没有人监听它**。角色会顶着 0 血继续站着（`Health` 里的 `IsAlive` 已经变成 `false`，所以不会再掉血、也不会再被打，但也不会消失）。

游戏**卡在一个「你死了但游戏没反应」的状态**。这正是 `RunManager` 要解决的问题。

### 它更是后面所有肉鸽系统的挂载点

现在它只做一件很小的事（重开），但**这个类的名字和位置是为将来选的**。

按 `00-总览与阅读指南.md` 里的规划，后面这些系统**都要挂在这里**：

| 将来的功能 | 为什么挂在这 |
|---|---|
| **随机地牢生成** | 每「一局」开始时要生成新地牢。`RunManager` 正好知道「一局什么时候开始」 |
| **通关三选一强化** | 每打完一个房间要弹强化界面。它需要知道「这一局进行到哪了」 |
| **局内进度重置** | 死亡后要清空「本局捡到的词条」，但保留「局外解锁」。这个区分只有 `RunManager` 清楚 |
| **死亡结算界面** | 要显示「本局走了多远、杀了几个」。得有人统计这些数据 |
| **难度递增** | 第 3 局比第 1 局难——这需要「跨局」的状态，也就是存续时间比场景长的对象 |

> **它现在虽然只有 64 行，但它是「一局」这个概念的唯一载体。** 这个定位比它当前的功能重要得多。

### 一个刻意的设计取舍：用「重载场景」而不是「重置状态」

理想做法是「只把玩家挪回起点、血回满、敌人重生」——干净、快、不用重新加载资源。

但这个项目**故意选了最粗暴的做法**：`SceneManager.LoadScene()` 整个重载。

**为什么**：

| | 重载场景 | 手动重置状态 |
|---|---|---|
| 正确性 | **天然正确**——所有东西回到初始状态，不可能漏 | 容易漏：忘了重置某个冷却、某个词条、某个已开启的门 |
| 代码量 | 1 行 | 要写一个 `ResetAll()`，而且要随着系统增加不断补充 |
| 速度 | 慢一点（几十毫秒） | 快 |
| 适合阶段 | **早期：功能还没定型时** | 后期：功能稳定后做优化 |

**现在功能每周都在变**，任何「手动重置」的代码都要跟着改。这个阶段正确性远比那几十毫秒重要。

代码里第 60–61 行的注释已经把这件事写清楚了：

> 这是最粗暴也最可靠的重开方式 —— 等肉鸽系统上线后再换成「只重置局内状态」。

**这是「先让它对，再让它快」原则的一次具体应用。**

---

## 四、代码全解

### 4.1 文件头（第 1–3 行）

```csharp
1: using System.Collections;
2: using UnityEngine;
3: using UnityEngine.SceneManagement;
```

| 行 | 代码 | 含义 |
|---|---|---|
| 1 | `using System.Collections;` | 引入 .NET 集合命名空间。**这里要它不是为了集合，是为了 `IEnumerator` 接口**（第 56 行用到）。`IEnumerator` 是「迭代器」的祖宗接口，协程机制就是在它上面搭的 |
| 2 | `using UnityEngine;` | `MonoBehaviour`、`GameObject`、`Debug`、`WaitForSeconds` 都在这 |
| 3 | `using UnityEngine.SceneManagement;` | **场景管理是独立命名空间**，不引这一行就没有 `SceneManager`。这是 Unity 新手很常见的一个卡点——明明记得有 `SceneManager`，但编译器说找不到 |

> **逐条辨认 `using` 的意义**：看一个文件引了哪些命名空间，就能大致猜出它干什么。
>
> 比如这个文件引了 `SceneManagement`——不管代码写的是什么，它一定和「场景切换」有关。**读陌生代码时，先看 `using` 是很高效的入口。**

### 4.2 类声明与引用字段（第 5–15 行）

```csharp
5: /// <summary>
6: /// 一局游戏的生命周期管理。
7: ///
8: /// 现在只做一件事：玩家死亡 → 停顿一下 → 重开场景。
9: /// 后面肉鸽的「随机地牢生成」「通关强化」「局内进度重置」都会挂在这里。
10: /// </summary>
11: public class RunManager : MonoBehaviour
12: {
13:     [Header("引用")]
14:     [Tooltip("留空则自动找场景里 Tag 为 Player 的对象")]
15:     [SerializeField] private Health playerHealth;
```

| 行 | 代码 | 含义 |
|---|---|---|
| 5–10 | `/// <summary> ... </summary>` | XML 文档注释。**第 9 行那句「后面肉鸽的……都会挂在这里」是写给三个月后的自己看的**——它是这个类的定位声明 |
| 11 | `public class RunManager : MonoBehaviour` | 继承 `MonoBehaviour`。**它必须挂在场景里的一个 GameObject 上**——因为它要订阅事件、要启动协程 |
| 13 | `[Header("引用")]` | Inspector 分组标题 |
| 15 | `[SerializeField] private Health playerHealth;` | 玩家的 `Health` 组件引用。**留空是允许的**，第 29–33 行会自动找 |

> ### 为什么不用单例（Singleton）
>
> 很多教程会让 `RunManager` 做成单例，让别的地方能 `RunManager.Instance.XXX` 直接访问。
>
> **本项目刻意不做。** 原因：
>
> | 单例的问题 | 具体后果 |
> |---|---|
> | 隐藏依赖 | 任何脚本都能随手 `RunManager.Instance` 拿数据，你**看不出谁依赖谁** |
> | 生命周期混乱 | 场景重载时单例要特殊处理，否则指向已销毁的对象（`MissingReferenceException`） |
> | 测试困难 | 单例是全局状态，没法为每个测试造一个干净的实例 |
>
> 现在这个设计是：**谁需要 `RunManager` 的信息，谁自己拖引用**（或者订阅事件）。依赖关系写在 Inspector 里，看得见。
>
> 将来真的需要一个全局可访问的东西（比如「本局已获得的词条列表」），**优先做成 `ScriptableObject` 资产**——它有单例的便利，没有单例的全局状态问题。

### 4.3 死亡流程字段（第 17–22 行）

```csharp
17:     [Header("死亡流程")]
18:     [Tooltip("死亡后等待多久重开，留一点时间让玩家看清自己是怎么死的")]
19:     [SerializeField] private float restartDelay = 1.5f;
20:
21:     [Tooltip("关掉后只打日志不重开，方便调试")]
22:     [SerializeField] private bool autoRestart = true;
```

| 行 | 代码 | 含义 |
|---|---|---|
| 17 | `[Header("死亡流程")]` | 第二个分组标题 |
| 19 | `restartDelay = 1.5f` | 死亡后等 **1.5 秒**再重开 |
| 22 | `autoRestart = true` | 是否自动重开，默认开 |

> ### 为什么需要 `restartDelay` 这个 1.5 秒
>
> **因为「瞬间闪回重开」会让玩家困惑。**
>
> 如果不延迟，玩家死亡的体验是这样的：
>
> ```
> 被地刺扎死 → （同一帧）画面闪一下 → 已经站在出生点了
> ```
>
> 玩家的感受是「刚刚发生了什么？」——**他不知道自己是怎么死的、在哪死的、还剩多少血**。
>
> 有 1.5 秒缓冲，玩家能：
> 1. 看到自己血量归零
> 2. 看到自己被打飞倒下的位置
> 3. **理解死因**（是踩地刺了？还是被那个紫色方块磨死的？）
>
> **「理解自己为什么失败」是肉鸽游戏的核心体验之一。** 每一局死亡都应该是「我下次要改进哪里」的信息来源——如果玩家连自己怎么死的都不知道，那这一局的失败就只是浪费时间，他不会有「再来一局」的冲动。
>
> 1.5 秒这个数值是经验值：短于 1 秒，玩家来不及看；长于 2.5 秒，等待开始烦躁。
>
> ### `autoRestart` 这个开关为什么值得存在
>
> 调试的时候你会需要它：
>
> - 想检查「玩家死亡后敌人还在做什么」→ 关掉它，游戏停在那里
> - 想测量「从死亡到重开过了多久」→ 关掉它，避免重开干扰
> - 想反复触发死亡事件 → 不关的话场景一重载，你就得重新走到地刺上去
>
> **这类「调试开关」看着像多余的代码，实际上是省时间的工具。** 项目里其他类似的还有 `MeleeAttacker.alwaysShowHitbox`、`MeleeAttacker.debugFlashDuration`。

### 4.4 公开状态属性（第 24–25 行）

```csharp
24:     /// <summary>玩家是否已死亡。将来 UI 和肉鸽结算会读它</summary>
25:     public bool IsPlayerDead { get; private set; }
```

| 行 | 代码 | 含义 |
|---|---|---|
| 24 | `/// <summary>...</summary>` | 文档注释，说明了「将来谁会读它」 |
| 25 | `public bool IsPlayerDead { get; private set; }` | **自动实现属性**：外部能读，但只有本类能写 |

> ### `{ get; private set; }` 是什么意思
>
> 这是一个**只有读取权限对外开放**的属性。拆开看：
>
> | 部分 | 含义 |
> |---|---|
> | `public` | 属性本身**外部可读** |
> | `get;` | 自动生成读取方法（编译器帮你生成一个隐藏的 `_isPlayerDead` 字段） |
> | `private set;` | 写入方法**只有本类能调** |
>
> 效果对比：
>
> ```csharp
> // 外部脚本里
> bool dead = runManager.IsPlayerDead;   // ✅ 能读
> runManager.IsPlayerDead = true;        // ❌ 编译错误：set 访问器不可访问
> ```
>
> **为什么不让外部写**：「玩家死了没」这件事的**唯一真相来源是 `Health.Died` 事件**。如果允许外部随便改这个标记，就会出现「`RunManager` 以为玩家活着，但玩家其实已经死了」这种状态不一致——而且没人知道是谁改坏的。
>
> > ### 「自动实现属性」（auto-property）
> >
> > 你没写字段，但编译器会偷偷生成一个：
> >
> > ```csharp
> > // 你写的
> > public bool IsPlayerDead { get; private set; }
> >
> > // 编译器实际生成的（简化版）
> > private bool _isPlayerDead;
> > public bool get_IsPlayerDead() { return _isPlayerDead; }
> > private void set_IsPlayerDead(bool value) { _isPlayerDead = value; }
> > ```
> >
> > **语法糖**——让你少写十几行样板代码。
>
> ### 反过来，为什么 `playerHealth` 和 `restartDelay` 是字段而不是属性
>
> | | 字段 | 属性 |
> |---|---|---|
> | 写法 | `private float restartDelay = 1.5f;` | `public bool IsPlayerDead { get; private set; }` |
> | 用途 | **配置项**：Inspector 里填一次，代码里读 | **状态**：运行时会变，需要控制读写权限 |
> | 判断标准 | 「这个值是策划配的，还是运行算出来的？」 | |
>
> `restartDelay` 是你在 Inspector 里填的 → 字段。
> `IsPlayerDead` 是运行时因为玩家死亡而改变的 → 属性（而且要限制外部写入）。

### 4.5 `Awake()` —— 自动找玩家（第 27–34 行）

```csharp
27:     private void Awake()
28:     {
29:         if (playerHealth == null)
30:         {
31:             var go = GameObject.FindGameObjectWithTag("Player");
32:             if (go != null) playerHealth = go.GetComponent<Health>();
33:         }
34:     }
```

| 行 | 代码 | 含义 |
|---|---|---|
| 27 | `private void Awake()` | Unity 生命周期方法，物体创建时调用一次 |
| 29 | `if (playerHealth == null)` | **只在 Inspector 里没拖引用时才自动找** |
| 31 | `var go = GameObject.FindGameObjectWithTag("Player");` | 在整个场景里找 Tag 为 `Player` 的物体 |
| 32 | `if (go != null) playerHealth = go.GetComponent<Health>();` | 找到了就取它身上的 `Health` 组件 |

> ### 这是一个「兜底」模式，不是主路径
>
> 第 29 行的 `if` 很重要——**优先用 Inspector 里拖的引用，只有没拖时才自动找。**
>
> 这样设计的原因：
>
> | 场景 | 行为 |
> |---|---|
> | 正常使用 | 你在 Inspector 里拖了 `Player` 的 `Health` 进去 → 用你的 |
> | 忘了拖 / 想省事 | 自动找到唯一一个 Tag 为 `Player` 的对象 |
> | **将来有多个 Player 对象** | 你在 Inspector 里明确指定要哪个 → **不会找错** |
>
> 如果无条件用 `FindGameObjectWithTag`，将来做「双人模式」或者「训练场里有多个假人」时，它就必然找错——**而且改起来要动代码**。
>
> > **通用模式：`[SerializeField]` 拖引用 + 代码自动兜底。** 这是 Unity 里非常常见的写法，你在项目里还会看到（比如 `CharacterStats` 的 `health` 字段）。

> ### ⚠️ `FindGameObjectWithTag` 是慢的
>
> 它的工作方式是**遍历场景里所有的 GameObject，逐个比较 Tag**。
>
> 一个有 500 个物体的场景里，这就是 500 次字符串比较。
>
> **所以它只在 `Awake` 里调一次**，结果存进 `playerHealth` 字段。如果写在 `Update` 里，就是每秒 60 次 × 500 个物体的遍历——**帧率会肉眼可见地掉**。
>
> 更快的替代方案：
>
> | 方案 | 速度 | 代价 |
> |---|---|---|
| `GameObject.FindGameObjectWithTag` | 慢（遍历全部） | 无 |
| `GameObject.FindWithTag` | 同上 | 无 |
| **Inspector 拖引用** | **最快（直接读）** | 要手动拖一次 |
| 静态注册表（物体在 `Awake` 里把自己登记进一个 `static` 列表） | 快 | 要自己维护，容易漏注销 |
>
> **本项目在「方便」和「性能」之间选了中间**：优先拖引用（快），留自动查找当兜底（方便），且只在 `Awake` 跑一次（把成本压到最小）。

### 4.6 事件订阅（第 36–44 行）

```csharp
36:     private void OnEnable()
37:     {
38:         if (playerHealth != null) playerHealth.Died += OnPlayerDied;
39:     }
40:
41:     private void OnDisable()
42:     {
43:         if (playerHealth != null) playerHealth.Died -= OnPlayerDied;
44:     }
```

| 行 | 代码 | 含义 |
|---|---|---|
| 36 | `private void OnEnable()` | 物体**被启用时**调用。第一次创建时在 `Awake` 之后、`Start` 之前 |
| 38 | `playerHealth.Died += OnPlayerDied;` | **订阅** `Health` 的 `Died` 事件，把 `OnPlayerDied` 方法挂上去 |
| 41 | `private void OnDisable()` | 物体**被禁用时**调用。物体销毁时也会调用一次 |
| 43 | `playerHealth.Died -= OnPlayerDied;` | **退订** |

> ### `+=` 和 `-=` 在事件上的含义
>
> `Died` 是一个 **`event Action`**——事件本质上是「一列等待被通知的方法」。`+=` 是「把我的方法加到这一列里」，`-=` 是「从这一列里移除」。
>
> 当 `Health` 里执行 `Died?.Invoke()` 时，**这一列里的每个方法都会被调用一次**。现在这一列里只有 `OnPlayerDied` 一个方法；将来如果加了「死亡音效播放器」「死亡结算 UI」，它们也会各自 `+=` 进来，**`Health` 完全不需要知道它们的存在**。
>
> 这就是总览里说的「事件驱动解耦」。

> ### 🔑 为什么必须是 `OnEnable` / `OnDisable`，而不是 `Start` / `OnDestroy`
>
> 这是本文件里最重要的一条约定（`00-总览` 第四节第 3 条专门讲过）。
>
> 关键差异在**物体被 `SetActive(false)` 的时候**：
>
> | 生命周期方法 | 物体被 `SetActive(false)` 时会调用吗 |
> |---|---|
> | `OnDisable` | ✅ **会** |
> | `OnDestroy` | ❌ **不会**（物体还没被销毁） |
>
> 假设你写在 `Start` / `OnDestroy` 里：
>
> ```
> 场景里 RunManager 正常订阅了 Died
> 某个系统把它 SetActive(false) 暂时隐藏（比如切到暂停菜单时隐藏游戏对象）
>   → OnDestroy 没触发，退订没发生
>   → 但 RunManager 仍然在 Died 的调用列表里
> 玩家死了 → Died?.Invoke()
>   → 调用列表里的 RunManager.OnPlayerDied() 被调用
>   → 但它已经被禁用/销毁了
>   → MissingReferenceException（或者更糟：静默重开了一个不该重开的场景）
> ```
>
> **只加不减的订阅，是 Unity 里最常见的内存泄漏和诡异 bug 来源。**
>
> > ### 记住这条规则
> >
> > ```csharp
> > private void OnEnable()  => xxx.Event += Handler;
> > private void OnDisable() => xxx.Event -= Handler;
> > ```
> >
> > **永远成对写。** 你在项目里会看到同样的模式出现在 `HitReaction`（订阅 `Health.Damaged`）、`CharacterStats`（订阅自己的 `Changed`）里。
>
> ### 第 38 行和 43 行的 `if (playerHealth != null)` 为什么必要
>
> 因为第 29–33 行的自动查找**可能失败**——比如场景里根本没有 Tag 为 `Player` 的物体（你在测试一个空场景）。
>
> 没有这个判断，第 38 行会抛 `NullReferenceException`，**在 `OnEnable` 里抛异常会导致物体启用失败**，后面所有逻辑都不执行。
>
> > ### 对比：`MeleeAttacker` 为什么没写这个判断
> >
> > 因为 `MeleeAttacker` 上有 `[RequireComponent(typeof(PlayerInputReader))]` 保证 `_input` 一定不为空。而 `RunManager` 的 `playerHealth` **是可选的**（可以留空、也可能找不到），所以必须自己判断。
> >
> > **`[RequireComponent]` 能保证的，就不用写 `null` 检查；不能保证的，就必须写。**

### 4.7 `OnPlayerDied()` —— 死亡回调（第 46–54 行）

```csharp
46:     private void OnPlayerDied()
47:     {
48:         if (IsPlayerDead) return;   // 玩家可能同时被多个来源打死，只处理一次
49:         IsPlayerDead = true;
50:
51:         Debug.Log("[RunManager] 玩家死亡");
52:
53:         if (autoRestart) StartCoroutine(RestartRoutine());
54:     }
```

| 行 | 代码 | 含义 |
|---|---|---|
| 46 | `private void OnPlayerDied()` | 这个方法名在第 38 行被订阅到 `Died` 事件上。**它没有参数**，因为 `Died` 是 `event Action`（无参数） |
| 48 | `if (IsPlayerDead) return;` | **重入守卫**：已经处理过就不再处理 |
| 49 | `IsPlayerDead = true;` | 标记为已死亡 |
| 51 | `Debug.Log("[RunManager] 玩家死亡");` | 打日志 |
| 53 | `if (autoRestart) StartCoroutine(RestartRoutine());` | 启动了重开协程 |

> ### 🔑 第 48 行防的是什么
>
> **玩家可能在同一瞬间被多个来源打死，或者 `Died` 事件被触发两次。**
>
> 具体的可能场景：
>
> | 场景 | 会发生什么 |
> |---|---|
| 玩家 10 血，同时踩在地刺上、被两个敌人夹击 | 三个 `TakeDamage` 在同一物理帧结算，最后一个让血量归零 → **`Died` 只触发一次**（`Health` 第 51 行的 `if (Current <= 0f)` 有 `IsAlive` 守卫）。但如果有代码在 `Died` 之外也调了 `TakeDamage`，就可能触发两次 |
| 将来做了「多段判定」的技能 | 同一个技能的第 2 段和第 3 段落在同一帧 |
| 将来做了「同归于尽」机制 | 玩家和 Boss 同时死亡，两边都触发 |
>
> **没有第 48 行会怎样**：
>
> ```
> Died 触发第 1 次  →  IsPlayerDead = true  →  StartCoroutine(RestartRoutine())  →  协程 A 开始等待 1.5 秒
> Died 触发第 2 次  →  StartCoroutine(RestartRoutine())  →  协程 B 也开始等待 1.5 秒
>
> 1.5 秒后：
>   协程 A 执行 LoadScene  →  场景重载
>   协程 B 也执行 LoadScene  →  又重载一次！
> ```
>
> **结果是场景被加载两遍。** 在简单场景里你可能看不出问题（第二次加载覆盖第一次），但一旦有了「局外数据」「物品掉落」「存档写入」，重复执行就会造成**数据被写两次**——比如灵魂点数翻倍。
>
> > ### 这类守卫的通用名字：幂等性（idempotency）
> >
> > 「幂等」的意思是「执行一次和执行多次，结果相同」。
> >
> > 第 48–49 行让 `OnPlayerDied` 变成幂等的：不管你调多少次，**只有第一次真正生效**。
> >
> > **凡是「触发一次就产生副作用」的回调（重开场景、播放音效、加分、存档），都应该加这个守卫。**
> >
> > 项目里同样的模式还有：`EnemyController.OnDied()` 里的 `Destroy`（虽然 `Destroy` 本身对同一对象调两次无害，但如果将来加了「掉落物品」，就必须加守卫）。

> ### 第 51 行的日志为什么要带 `[RunManager]` 前缀
>
> 因为 Unity Console 里**来自不同脚本的日志会混在一起**。当你的游戏跑起来之后，Console 可能同时有：
>
> ```
> [RunManager] 玩家死亡
> 拾取了物品：锋利之刃
> 敌人死亡
> 生成了 3 个敌人
> ```
>
> **带前缀的你能一眼过滤出来**（Console 右上角有搜索框，输入 `[RunManager]` 就只剩这一条）。
>
> 更规范的做法是 Unity 的**富文本日志**，可以给日志上色分类：
>
> ```csharp
> Debug.Log("<color=red>[RunManager]</color> 玩家死亡");
> ```
>
> **约定**：项目里所有 `Debug.Log` 都带 `[类名]` 前缀。将来要找「哪里打了一堆日志」时，这个约定能省很多时间。

### 4.8 `RestartRoutine()` —— 协程（第 56–63 行）

```csharp
56:     private IEnumerator RestartRoutine()
57:     {
58:         yield return new WaitForSeconds(restartDelay);
59:
60:         // 重载当前场景：位置、血量、敌人全部回到初始状态。
61:         // 这是最粗暴也最可靠的重开方式 —— 等肉鸽系统上线后再换成「只重置局内状态」。
62:         SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
63:     }
```

| 行 | 代码 | 含义 |
|---|---|---|
| 56 | `private IEnumerator RestartRoutine()` | **这是一个协程**。返回类型是 `IEnumerator` 而不是 `void`——这是协程的标志 |
| 58 | `yield return new WaitForSeconds(restartDelay);` | **在这里暂停，等 `restartDelay` 秒后从下一行继续** |
| 60–61 | `// ...` | 注释说明设计取舍 |
| 62 | `SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);` | 重载当前场景 |

#### 🔑 协程（Coroutine）是什么

这是你第一次在项目里接触协程，值得完整讲一遍。

**问题**：游戏里需要「**等一段时间，然后做某件事**」。等待期间，游戏不能卡住——敌人要动、动画要播、玩家输入要有响应。

**新手最容易想到的错误做法**：

```csharp
// ❌ 绝对不要这样写
Thread.Sleep(1500);              // 让当前线程睡 1.5 秒
SceneManager.LoadScene(...);
```

`Thread.Sleep` 会让**整个线程停下来**。Unity 的主线程既要跑游戏逻辑又要渲染——**这 1.5 秒里整个游戏会完全卡死**，画面冻结、没有输入、像死机一样。

**正确做法：协程。**

```csharp
private IEnumerator RestartRoutine()
{
    yield return new WaitForSeconds(restartDelay);   // ← 在这里「让出」，1.5 秒后再回来
    SceneManager.LoadScene(...);
}
```

**协程的本质**：把一个方法**切成几段**。遇到 `yield return` 就把控制权交还给 Unity（游戏继续正常跑），等条件满足后再从下一行接着执行。

```
调用 StartCoroutine(RestartRoutine())
        │
        ▼
① 开始执行方法
        │
        ▼
② 碰到 yield return new WaitForSeconds(1.5f)
        │  ┌──────────────────────────────────┐
        │  │  方法「暂停」，控制权交还 Unity    │
        │  │  这 1.5 秒里：                    │
        └─▶│   • 敌人继续移动                   │
           │   • 动画继续播                     │
           │   • 玩家输入照常响应               │
           │   • 渲染照常进行                   │
           │  （就像这个方法从未存在过）         │
           └──────────────────────────────────┘
        │
        ▼
③ 1.5 秒到了，Unity 从下一行继续执行
        │
        ▼
④ SceneManager.LoadScene(...)  →  场景重载，方法结束
```

> ### `IEnumerator` 和 `yield return` 的语法原理
>
> | 语法 | 含义 |
> |---|---|
> | `IEnumerator` | 返回类型。它告诉 C# 编译器：「这个方法里有 `yield`，请把它编译成一个可以分段执行的迭代器」 |
> | `yield return X` | 「现在**产出** X 并暂停，等下次被唤醒时从下一行继续」 |
> | `StartCoroutine(...)` | 告诉 Unity：「帮我盯着这个迭代器，条件满足时唤醒它」 |
> | `WaitForSeconds(1.5f)` | Unity 提供的**等待指令对象**。它代表「等 1.5 秒」这个条件 |
>
> **注意：`yield return` 那行本身不「做」什么。** 它只是把 `WaitForSeconds` 这个对象交出去——**是 Unity 的协程调度器读了这个对象，才知道要等 1.5 秒**。
>
> 换成别的等待指令，行为就完全不同：
>
> | 等待指令 | 什么时候被唤醒 |
> |---|---|
| `new WaitForSeconds(1.5f)` | 1.5 秒后（受 `Time.timeScale` 影响） |
> | `new WaitForSecondsRealtime(1.5f)` | 1.5 秒后（**不受** `Time.timeScale` 影响，暂停时也在走） |
| `null` | 下一帧 |
| `new WaitForFixedUpdate()` | 下一个物理帧 |
| `new WaitUntil(() => condition)` | `condition` 变成 `true` 时 |
> | 另一个 `Coroutine` | 那个协程跑完时 |
>
> ⚠️ **`WaitForSeconds` 受 `Time.timeScale` 影响**：如果你将来做「命中顿帧」把 `timeScale` 设成 0（`MeleeAttacker` 的文档里提到过这个技巧），`WaitForSeconds(1.5f)` 会**永远不结束**。那种场合必须用 `WaitForSecondsRealtime`。
>
> **本项目现在没有顿帧，所以用 `WaitForSeconds` 是对的。** 等加了顿帧之后，这一行要重新检查。

> ### 协程的常见误区
>
> | 误区 | 真相 |
> |---|---|
> | 「协程是多线程」 | ❌ 协程**全在主线程上跑**。它只是「分段执行」，不是并行。所以协程里照样不能做耗时计算 |
> | 「协程会在物体销毁后自动停止」 | ✅ **会**——物体被销毁时，它启动的所有协程都会被停止。这是好事（不会操作已销毁对象的引用），但也意味着**协程可能在 `yield` 之后永远不回来** |
> | 「`StartCoroutine` 会等协程跑完」 | ❌ 它**立刻返回**。第 53 行执行完 `StartCoroutine` 之后，`OnPlayerDied` 立刻就结束了，协程在后台自己跑 |

> ### 第 62 行：为什么用 `buildIndex` 而不是场景名字符串
>
> 两种写法都能重载当前场景：
>
> ```csharp
> SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);   // ✅ 本文件用的
> SceneManager.LoadScene("Game");                                     // ⚠️ 硬编码名字
> ```
>
> | | `buildIndex` | 场景名字符串 |
> |---|---|---|
> | 场景改名后 | **自动跟着变** ✅ | 字符串对不上，运行时静默失败 |
> | 靠什么定位 | Build Settings 列表里的**序号** | 场景文件的名字 |
> | 可读性 | 差（`0` 是什么？） | 好（`"Game"` 一眼看懂） |
>
> 这里选 `buildIndex` 是因为：**它永远指向「当前正在跑的那个场景」**，不管这个场景叫什么、在列表里排第几。
>
> ```csharp
> SceneManager.GetActiveScene()      // 取当前激活的场景
>     .buildIndex                    // 取它的序号
> ```
>
> 两步连起来就是「当前场景的序号」。
>
> 如果换成 `"Game"`，你哪天把场景文件改名成 `Level01`，**代码不会报错，但重开会静默失败**——因为找不到叫 `"Game"` 的场景。这类「改名引发的运行时故障」是最烦人的一类 bug。

---

## 五、在 Unity 里怎么配

### 5.1 ⚠️ 先做这一步：把场景加入 Build Settings

**这是本脚本最容易漏、后果最严重的一步。**

代码用的是 `SceneManager.LoadScene(buildIndex)`。**这个 API 只能加载「Build Settings 列表里」的场景。** 如果 `Game.unity` 不在列表里，运行时会报：

```
Scene 'Game' couldn't be loaded because it has not been added to the build settings
```

**怎么加**：

1. 菜单 `File` → `Build Settings`（Unity 6 里可能叫 `File` → `Build Profiles`）
2. 找到 **`Scenes In Build`** 列表
3. 从 Project 窗口把 `Assets/_Project/Scenes/Game.unity` **拖进这个列表**
4. 关掉窗口

> **为什么在编辑器里点 ▶ 测试时它也必要**：Unity 编辑器在 Play 模式下运行的是一个「临时构建」，它**同样只认 Build Settings 列表里的场景**。所以不是「打包时再管」，而是**现在不管就测不了**。

### 5.2 创建 RunManager 物体

1. 在 Hierarchy 面板**空白处**右键 → `Create Empty`（创建空物体）
2. 命名 `RunManager`
3. 位置随便——**它不参与任何物理或渲染**，放在 `(0, 0, 0)` 就行
4. 在 Inspector 里 `Add Component` → 搜 `Run Manager` → 添加

> **为什么单独建一个物体而不是挂在 Player 上**：
>
> 因为 `RunManager` 管的是「一局」，**它的生命周期比 Player 长**。如果挂在 Player 上，玩家死亡被销毁时它会跟着销毁——那谁来重开场景？
>
> **判断标准**：这个脚本管的东西「比角色活得久」吗？活得久 → 单独建物体。

### 5.3 填 Inspector

| 字段 | 填什么 | 说明 |
|---|---|---|
| **引用** | | |
| `Player Health` | **留空**（推荐）或拖入 Player 的 `Health` | 留空时会自动找 Tag 为 `Player` 的对象。**建议留空**，省一次手动拖拽，将来换 Player 对象也不用重拖 |
| **死亡流程** | | |
| `Restart Delay` | `1.5` | 死亡后等多久重开。1.0~2.5 之间都比较合理 |
| `Auto Restart` | `true` | 调试时可以临时关掉 |

> ### 确认 Player 的 Tag 设对了
>
> 如果 `Player Health` 留空，第 31 行会去找 **Tag 为 `Player`** 的物体。所以：
>
> 1. 在 Hierarchy 里单击 `Player`
> 2. Inspector 最上方确认 **`Tag` 是 `Player`**
>
> **这个 Tag 是 Day 1 配的，应该已经是对的。** 但如果重开会没反应（且 Console 里没有 `[RunManager] 玩家死亡`），第一件事就是来检查这里。
>
> > `Player` 是 Unity 的**内置标签**——不需要自己新建，下拉框里本来就有。Day 1 曾经因为「想新建一个叫 Player 的标签」而卡住过：Unity 会提示 `Tag with "Player" name already exists`。

### 5.4 完整依赖清单

```
RunManager (独立物体)
└── Run Manager 组件

Player (另一个物体)
├── Health                     ← RunManager 订阅它的 Died 事件
├── Tag = "Player"             ← 如果 Player Health 留空，靠这个找到它
└── ...

Build Settings
└── Game.unity                 ← 必须在 Scenes In Build 列表里 ⚠️
```

| 缺什么 | 后果 |
|---|---|
| Build Settings 里没有场景 | 死亡后报错 `Scene 'Game' couldn't be loaded because it has not been added to the build settings` |
| Player 的 Tag 不是 `Player`，且 `Player Health` 留空 | `playerHealth` 为 `null` → 第 38 行不订阅 → **死亡后什么都不发生**，无报错 |
| Player 没有 `Health` 组件 | 同上 |
| `RunManager` 物体被 `SetActive(false)` | `OnDisable` 触发退订 → 玩家死亡不再重开 |

---

## 六、踩过的坑

> **现象**：玩家被地刺扎死，Console 里**确实打印了** `[RunManager] 玩家死亡`，但等 1.5 秒后场景**没有重开**，反而报了一条红字：
>
> ```
> Scene 'Game' couldn't be loaded because it has not been added to the build settings
> ```
>
> **根因**：`Game.unity` 没有加入 Build Settings 的 `Scenes In Build` 列表。`SceneManager.LoadScene(buildIndex)` 只能加载列表里的场景。
>
> **为什么容易漏**：在编辑器里点 ▶ 测试时，很多人以为「场景已经打开了，应该能加载」——**但 Unity 编辑器在 Play 模式下跑的也是一个临时构建，同样受 Build Settings 限制**。
>
> **解法**：`File` → `Build Settings`（Unity 6 里可能叫 `Build Profiles`）→ 把 `Assets/_Project/Scenes/Game.unity` 从 Project 窗口**拖进** `Scenes In Build` 列表。
>
> **验证方法**：列表里出现 `0  Assets/_Project/Scenes/Game.unity` 这一行就对了（前面的 `0` 就是 `buildIndex`）。
>
> > **这条坑的通用教训**：**凡是「开发时靠编辑器状态能跑、打包后跑不了」的功能，都要在开发阶段就验证一次。** 打包相关的东西（场景列表、资源引用、PlayerPrefs 路径、分辨率）都属于这一类。项目计划里第 5 周专门有一条「自己下载下来、在别的电脑上跑一遍」，就是为了在交付前抓住这类问题。

> **现象**：把 `Player Health` 留空，指望自动查找，但玩家死后**什么都没发生**，Console 里连 `[RunManager] 玩家死亡` 都不打印。
>
> **根因**：`Player` 物体的 `Tag` 不是 `Player`（可能是 `Untagged`），导致第 31 行 `FindGameObjectWithTag` 返回 `null`，第 32 行不执行，`playerHealth` 保持 `null`，第 38 行的订阅被 `if` 拦掉了。
>
> **而且没有任何报错**——这是最难受的地方：`if (playerHealth != null)` 这个守卫本意是「防止崩溃」，副作用是**把错误也一起吞掉了**。
>
> **解法**：检查 `Player` 物体的 Tag。
>
> **排查思路**：遇到「事件没反应」，先确认**订阅有没有成功建立**。可以在第 38 行加一句临时日志：
>
> ```csharp
> if (playerHealth != null)
> {
>     playerHealth.Died += OnPlayerDied;
>     Debug.Log("[RunManager] 已订阅玩家死亡事件");
> }
> else
> {
>     Debug.LogWarning("[RunManager] 找不到玩家的 Health，死亡事件不会被处理");
> }
> ```
>
> **加一条 `else` 分支的警告日志**，比让 `if` 静静吞掉错误要有用得多。

---

## 七、如果要改，改这里

### 1. 想加「死亡结算界面」

现在是「等 1.5 秒 → 直接重开」。加界面的做法：

```csharp
private IEnumerator RestartRoutine()
{
    yield return new WaitForSeconds(restartDelay);

    // 先弹结算界面，等玩家点「再来一局」
    deathScreen.Show(runStats);
    yield return new WaitUntil(() => deathScreen.RestartRequested);

    SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
}
```

> ⚠️ 这样改之后，`restartDelay` 的含义变了——它从「等待时长」变成「显示结算界面前的停顿」。建议改名为 `deathPause`，**名字要跟着语义走**。

### 2. 想把「重载场景」换成「只重置局内状态」

这是计划里的事（代码注释第 61 行已经写了）。做法：

```csharp
// 不用 SceneManager.LoadScene，改成：
playerHealth.ResetToFull();
player.transform.position = spawnPoint.position;
enemySpawner.RespawnAll();
inventory.ClearRunItems();        // 清局内的，保留局外的
```

> ⚠️ **每一个系统都要参与重置**，漏一个就会出现「重开后某个东西还是旧的」。所以这件事应该**等到系统都定型了再做**——现在做，每加一个系统就要回来补一次。
>
> **判断时机**：当你发现「重载场景要几百毫秒、玩起来有卡顿感」时，再动手。现在远远没到那个阶段。

### 3. 想把「重开」改成「回到主菜单」

```csharp
SceneManager.LoadScene("MainMenu");   // 但这样又回到硬编码名字的问题
```

**更好的做法**：在 `RunManager` 里加一个 `[SerializeField] private string mainMenuSceneName = "MainMenu";`，让场景名成为可以在 Inspector 里改的配置。

或者更彻底——用**场景序号**：

```csharp
[SerializeField] private int mainMenuSceneIndex = 0;
SceneManager.LoadScene(mainMenuSceneIndex);
```

> 项目约定：**场景之间跳转优先用序号**，理由见 4.8 节。

### 4. 想统计「本局数据」（走了多远、杀了几个、捡了什么）

在 `RunManager` 里加字段和公开方法：

```csharp
public int EnemiesKilled { get; private set; }
public int RoomsCleared { get; private set; }

public void RegisterKill() => EnemiesKilled++;
```

然后让 `EnemyController.OnDied()` 调用 `runManager.RegisterKill()`。

> ⚠️ **更好的做法**：不要反过来让 `EnemyController` 去找 `RunManager`（那会让敌人依赖流程层，违反总览里的依赖方向）。
>
> 正确做法是**让 `RunManager` 订阅 `Health.Died`**——但 `Health` 的 `Died` 事件不带参数，`RunManager` 不知道死的是谁。
>
> **解法是把 `Died` 事件改成带参数**：`public event Action<GameObject> Died;` 或者传入一个「死亡上下文」结构体。这是一个**破坏性改动**（所有订阅者都要改），所以现在不做，等真的需要统计时再一起改。

### 5. 想加「暂停」功能

暂停的正确做法是 `Time.timeScale = 0f`。但要注意：

| 受影响 | 不受影响 |
|---|---|
| `Update`（会继续跑，但 `Time.deltaTime` 变成 0） | `Update` 本身仍然被调用 |
| `FixedUpdate`（**停止调用**） | `WaitForSecondsRealtime` |
| `WaitForSeconds`（**永远不结束**） | `Time.unscaledTime` |
| 物理模拟 | 输入事件 |

> ⚠️ **第 4.8 节提过的坑**：如果加了暂停，`RestartRoutine` 里的 `WaitForSeconds(restartDelay)` 在暂停期间不会走。这在「玩家死亡后按暂停」的场景下可能是想要的（暂停冻结一切），也可能不是。**改之前先想清楚你想要哪种。**

### 6. 别忘了文档

改完代码，同步改这份文档，并把头部信息表的「最后更新」改成新的 Day 编号。
