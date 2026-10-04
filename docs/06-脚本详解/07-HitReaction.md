# 07 · HitReaction

## 一、头部信息表

| 项 | 内容 |
|---|---|
| 文件路径 | `D:\Roguelike2D\Assets\_Project\Scripts\Combat\HitReaction.cs` |
| 所属层 | ② 结算层（表现侧） |
| 依赖 | `Health`（**必须同物体**）、`DamageInfo`、`SpriteRenderer`（可选）、`Rigidbody2D`（可选） |
| 被谁依赖 | `PlayerController`、`MeleeAttacker`、`EnemyController` 都读它的 `IsStunned` |
| 行数 | 85 行 |
| 最后更新 | Day 4 |

---

## 二、一句话定位

受击的「表现层」：闪白、击退、硬直——三件事全靠订阅 `Health` 的事件驱动，它自己从不判断「该不该受伤」。

---

## 三、为什么需要它

### 3.1 如果没有它：`Health` 会变成一个什么都管的大杂烩

最直觉的做法是把受击效果直接写进 `Health.TakeDamage`：

```csharp
// ❌ 没有 HitReaction 的 Health
public void TakeDamage(DamageInfo info)
{
    if (!IsAlive) return;
    if (_invincibilityTimer > 0f) return;

    Current = Mathf.Max(0f, Current - info.Amount * (1f - reduction));
    _invincibilityTimer = invincibilityDuration;

    // ↓↓↓ 从这里开始全是「表现」↓↓↓
    _sprite.color = Color.white;                              // 闪白
    _flashTimer = 0.12f;
    _stunTimer = 0.18f;                                       // 硬直
    Vector2 away = transform.position - info.SourcePosition;
    _rb.linearVelocity = new Vector2(away.x * 7f, 7f * 0.4f); // 击退
    _audioSource.PlayOneShot(hitSound);                       // 音效
    _particleSystem.Play();                                   // 粒子
    StartCoroutine(ShakeCamera(0.15f));                       // 屏幕震动
    DamagePopup.Spawn(transform.position, info.Amount);       // 飘字
}
```

**逐条数一下这段代码的代价：**

**代价一：`Health` 被迫依赖整个表现层。**

它现在需要 `SpriteRenderer`、`Rigidbody2D`、`AudioSource`、`ParticleSystem`、相机、一个飘字系统。

而 `Health` 的本职是「记录血量」。**一个血量组件凭什么要知道相机在哪？**

**代价二：木桶会报错。**

木桶有 `Health`（它要能被打碎），但木桶**没有 `Rigidbody2D`**（它不动）。

上面第 3 行的 `_rb.linearVelocity = ...` 会直接抛 `NullReferenceException`。

于是你被迫在 `Health` 里到处加判空：

```csharp
// ❌ 为了兼容各种物体，Health 里塞满了 null 检查
if (_sprite != null) { ... }
if (_rb != null) { ... }
if (_audioSource != null) { ... }
if (_particleSystem != null) { ... }
```

**这些 `if` 每一个都在说同一件事：「我不确定这个物体上有没有表现组件」——而这恰恰是 `Health` 不该关心的问题。**

**代价三：不能有特例。**

想做一个「不会闪白的 Boss」？想在切场景时关掉屏幕震动？想给不同敌人配不同音效？

**每一个需求都要改 `Health`**——改那个所有敌人都依赖的核心组件。风险极高。

### 3.2 拆开之后：一个事件，两边自由

```
    Health（逻辑）                          HitReaction（表现）
┌──────────────────┐                   ┌─────────────────────────┐
│ 扣血              │                   │ 闪白                     │
│ 无敌帧            │─── Damaged 事件 ──▶│ 击退                     │
│ 死亡判定          │                   │ 硬直                     │
│                  │                   │ （将来：音效/粒子/飘字）   │
└──────────────────┘                   └─────────────────────────┘
   只管「发生了什么」                       只管「看起来怎样」
```

连接它们的只有一行——`Health.cs` 第 62 行：

```csharp
Damaged?.Invoke(info);
```

`Health` **不知道**谁订阅了、有几个订阅者、订阅者要做什么。它只是喊一声「我受伤了」。

于是所有关于「表现」的需求变化，都被关在 `HitReaction` 这一个文件里：

| 想做的事 | 改哪里 | `Health` 要改吗 |
|---|---|---|
| 加受击音效 | `HitReaction.OnDamaged` | ❌ |
| 加伤害飘字 | `HitReaction.OnDamaged` | ❌ |
| 加屏幕震动 | `HitReaction.OnDamaged` | ❌ |
| 改伤害公式 | `Health.TakeDamage` | ✅（但 `HitReaction` 不动） |
| 让某个敌人不闪白 | 在那个敌人身上**不挂** `HitReaction` | ❌ |
| 让 Boss 闪红光 | 调那个 Boss 的 `HitReaction.flashColor` | ❌ |

> **判断一个东西该不该放在 `HitReaction` 里，用这个问题问自己：**
> **「如果没有屏幕、没有音箱、没有手柄震动，这段代码还有意义吗？」**
>
> - 「扣 7.2 血」→ 有意义 → `Health`
> - 「播放 hit.wav」→ 没意义 → `HitReaction`

### 3.3 一个反直觉但重要的细节：它不判断「该不该受伤」

注意 `HitReaction` 里**没有任何一行**在问「这次伤害算不算数」。

它没有 `if (info.Amount > 0f)`，没有 `if (!_health.IsAlive)`，没有检查无敌帧。

**因为它收到 `Damaged` 事件这件事本身，就已经意味着那次伤害是有效的。**

回头看 `Health.cs` 的两道闸（第 51、52 行）：

```csharp
if (!IsAlive) return;                  // 已经死了 → 直接退出，不发事件
if (_invincibilityTimer > 0f) return;  // 无敌帧 → 直接退出，不发事件
```

**被挡掉的伤害根本走不到第 62 行，所以 `Damaged` 不会触发，`HitReaction` 也就收不到通知。**

这就是「单一职责」的力量：**「判断该不该受伤」这件事只有一个地方做（`Health`），别的地方一律相信它的结论。**

> ⚠️ **反过来说，这也是一条必须记住的契约**：
> **`Damaged` 不触发 ≠ 没打中。** 被无敌帧挡掉的那次攻击，敌人不会闪白、不会击退。
> 这是**正确的表现**（无敌期间本来就不该有受击反馈），但如果你以后做「命中特效」，要知道它也会一起消失。

---

## 四、代码全解

### 4.1 引用、注释与类声明（第 1~11 行）

```csharp
1: using UnityEngine;
2: 
3: /// <summary>
4: /// 受击的表现层：闪白 + 击退 + 硬直。
5: ///
6: /// 它只订阅 Health 的事件，自己不判断「该不该受伤」—— 逻辑与表现分离。
7: /// 将来要加受击音效、粒子、伤害飘字，都加在这里，战斗逻辑一行不用改。
8: /// </summary>
9: [RequireComponent(typeof(Health))]
10: public class HitReaction : MonoBehaviour
11: {
```

| 行 | 代码 | 含义 |
|---|---|---|
| 1 | `using UnityEngine;` | 只需要 Unity 的命名空间。**注意没有 `using System;`**——因为这个脚本里没有用 `Action`（它只**订阅**事件，不**声明**事件，声明事件才需要 `Action`）。这是一个很好的线索：**看 `using` 就能猜出这个文件大概用了什么** |
| 2 | （空行） | 排版分隔 |
| 3~8 | XML 文档注释 | 说清三件事：① 这个类是「表现层」② 它不判断该不该受伤 ③ 未来的扩展点在哪。**第 7 行是一句面向未来的承诺**：写清「以后加音效加这里」，半年后你就不会去改 `Health` 了 |
| 9 | `[RequireComponent(typeof(Health))]` | **见下方详解** |
| 10 | `public class HitReaction : MonoBehaviour` | 继承 `MonoBehaviour`，能挂到 GameObject 上。**注意这里没有接口列表**——`HitReaction` 不实现任何接口，它是被动的：它只订阅事件，从不被别人调用（除了读 `IsStunned`） |
| 11 | `{` | 类型体开始 |

> #### `[RequireComponent(typeof(Health))]` 详解
>
> **它做三件事：**
>
> **① 自动补组件。** 当你把一个 `HitReaction` 拖到某个 GameObject 上时，如果那个物体上没有 `Health`，Unity 会**自动帮你加上一个**。不需要你手动去 `Add Component → Health`。
>
> **② 阻止删除。** 如果物体上有 `HitReaction`，你想在 Inspector 里把 `Health` 组件删掉——Unity **不允许**（`⋮` 菜单里的 `Remove Component` 会是灰色，或者弹出提示）。
>
> **③ 表达依赖关系。** 这是最重要的一点。它在说：「`HitReaction` **必须**和 `Health` 一起用」。
>
> **为什么必须有 ③？** 看第 37 行：
>
> ```csharp
> _health = GetComponent<Health>();
> ```
>
> 如果物体上没有 `Health`，这行代码返回 `null`。然后第 44 行：
>
> ```csharp
> private void OnEnable() => _health.Damaged += OnDamaged;
> ```
>
> `null.Damaged` → **`NullReferenceException`**。
>
> 没有 `[RequireComponent]` 的话，这个错误**只会在你把组件挂错地方时出现**——一个纯粹的疏忽导致的崩溃。加上它之后，**这种疏忽在 Unity 编辑器层面就不可能发生**。
>
> > **`[RequireComponent]` 是「让非法状态无法被创建」，而不是「出错后再检查」。** 这个思路比在 `Awake` 里写 `if (_health == null) Debug.LogError(...)` 要好得多——因为它把错误提前到了配置阶段，而不是运行阶段。

### 4.2 Inspector 字段（第 12~22 行）

```csharp
12:     [Header("闪白")]
13:     [SerializeField] private Color flashColor = Color.white;
14:     [SerializeField] private float flashDuration = 0.12f;
15: 
16:     [Header("击退与硬直")]
17:     [Tooltip("受击后失去操作权的时间")]
18:     [SerializeField] private float stunDuration = 0.18f;
19: 
20:     [Tooltip("垂直上弹占击退力的比例，让受击者「被打飞」而不是纯水平滑走")]
21:     [Range(0f, 1f)]
22:     [SerializeField] private float verticalRatio = 0.4f;
```

| 行 | 代码 | 含义 |
|---|---|---|
| 12 | `[Header("闪白")]` | Inspector 里的粗体分组标题 |
| 13 | `[SerializeField] private Color flashColor = Color.white;` | **闪白用的颜色。** 类型 `Color` 是 Unity 的结构体，有 r/g/b/a 四个 `float` 分量。**默认值 `Color.white` 就是纯白 `(1,1,1,1)`**——这是「闪光」最常用的颜色，因为它比原色亮，看起来像被照亮了。<br>**为什么默认是白色而不是红色？** 因为白色在暗色背景的 2D 游戏里辨识度最高，而且「变白」在视觉上像是「被光打了一下」。红色更像「着火了」或者 UI 警告。 |
| 14 | `[SerializeField] private float flashDuration = 0.12f;` | **闪白持续 0.12 秒。** 这个值的取值范围很窄，见第 4.6 节的参数详解 |
| 16 | `[Header("击退与硬直")]` | 第二组标题 |
| 17 | `[Tooltip("受击后失去操作权的时间")]` | 悬停提示。「失去操作权」是对 `stunDuration` 最准确的描述——它不是「不能动」，而是「你的输入被忽略了」 |
| 18 | `[SerializeField] private float stunDuration = 0.18f;` | **硬直 0.18 秒**，比闪白的 0.12 秒长 |
| 20 | `[Tooltip("垂直上弹占击退力的比例，让受击者「被打飞」而不是纯水平滑走")]` | 这句话是**本文件里最重要的配置说明**——它解释了第 81 行为什么要那么写 |
| 21 | `[Range(0f, 1f)]` | **滑条特性。** 在 Inspector 里把这个字段渲染成一根 0~1 的滑条，而不是数字输入框。<br>**它的作用不只是好看**：它从 UI 层面**阻止你填出非法值**。想填 `1.5` 或 `-2`？滑条拖不过去。<br>**什么时候该用 `[Range]`？** 当这个数值有明确的合法区间，而且**超出区间会出问题**时。`verticalRatio` 是个比例，超过 1 意味着「垂直上弹比水平击退还大」——那会变成「垂直起飞」，不是「被打飞」。 |
| 22 | `[SerializeField] private float verticalRatio = 0.4f;` | **垂直分量占 40%。** 配合 `AttackData.knockbackForce`（玩家 7 / 敌人 9）用：敌人打玩家时，水平速度 9、垂直速度 `9 × 0.4 = 3.6` |

> #### 三个参数的取值范围与取值理由
>
> | 字段 | 值 | 为什么是这个值 |
> |---|---|---|
> | `flashDuration` | **0.12s** | 人的视觉暂留大约 0.1 秒。**低于 0.08 秒会「看不见」（只是感觉画面抖了一下）；高于 0.25 秒会被识别成「变色」而不是「闪一下」。** 0.12 秒落在「明显看到，但来不及意识到它变了颜色」的窗口里——这正是「闪」的定义。<br>对比：`Health.invincibilityDuration = 0.5s`，是闪白时长的 4 倍——**闪白只占无敌帧的前 24%**，剩下 76% 的时间角色已经恢复正常外观但仍在无敌。这个比例是对的：无敌帧是规则，不该有持续 0.5 秒的视觉噪音。 |
> | `stunDuration` | **0.18s** | **比闪白长 0.06 秒，这是有意的。** 如果硬直和闪白一样长（0.12s），玩家会感觉「看到闪白的时候已经能动了」，硬直**感受不到**。<br>0.18 秒大约是 11 帧（60fps）——足够让玩家意识到「我刚才失去控制了」，但又短到不会觉得「卡住了」。<br>⚠️ **不要超过 0.3 秒**：超过之后动作游戏会变得「粘滞」，玩家会把死因归咎于操作延迟。<br>对比敌人的 `AttackData.cooldown = 1.2s`——硬直只有攻击间隔的 15%，所以硬直不会让战斗卡住。 |
> | `verticalRatio` | **0.4f** | 见第 4.6 节的击退算法详解。简单说：**0 会变成「贴地滑行」，1.0 会变成「垂直起飞」，0.4 是「被打飞」** |

> #### `Color` 是什么类型
> `Color` 是 Unity 的**结构体**（`struct`），四个 `float` 分量 `r`、`g`、`b`、`a`，每个范围 0~1。
> 它在 Inspector 里显示成一个**色板按钮**——点开是一个取色器，带吸管（可以从屏幕任意位置取色）。
>
> ⚠️ **一个容易困惑的点**：Unity 的 `Color` 分量是 **0~1 的浮点数**，不是 0~255 的整数。`Color.white` 等于 `(1, 1, 1, 1)`，不是 `(255, 255, 255, 255)`。
> 代码里要写「半透明红」是 `new Color(1f, 0f, 0f, 0.5f)`。
> 需要 0~255 的场合（比如 HTML 色值 `#FF5500`）用 `ColorUtility.TryParseHtmlString("#FF5500", out Color c)`。

### 4.3 私有字段与 `IsStunned`（第 24~33 行）

```csharp
24:     private Health _health;
25:     private SpriteRenderer _sprite;
26:     private Rigidbody2D _rb;
27:     private Color _originalColor;
28: 
29:     private float _flashTimer;
30:     private float _stunTimer;
31: 
32:     /// <summary>是否处于受击硬直。硬直期间应失去操作权</summary>
33:     public bool IsStunned => _stunTimer > 0f;
```

| 行 | 代码 | 含义 |
|---|---|---|
| 24 | `private Health _health;` | **同物体上 `Health` 组件的缓存。** 它是 `[RequireComponent]` 保证存在的，所以第 37 行拿到的**一定不是 `null`**——这是本文件里唯一一个不需要判空的组件引用 |
| 25 | `private SpriteRenderer _sprite;` | **精灵渲染器。可以不存在**（木桶如果只想被扣血不想闪白就不挂）。所以第 38、41、52、63 行都要判空 |
| 26 | `private Rigidbody2D _rb;` | **刚体，用来改速度。可以不存在**（木桶不动，不需要刚体）。所以第 73 行要判空 |
| 27 | `private Color _originalColor;` | **精灵的「本来颜色」，在 `Awake` 里缓存下来。** 见第 4.5 节的详解——**这是本文件里最容易被忽略但很关键的一个字段** |
| 29 | `private float _flashTimer;` | 闪白倒计时。`> 0` 表示还在闪 |
| 30 | `private float _stunTimer;` | 硬直倒计时。`> 0` 表示还在硬直 |
| 32 | 文档注释 | 「硬直期间应失去操作权」——**这不是给 `HitReaction` 自己看的，是给使用它的三个脚本看的**。它定义了 `IsStunned` 的语义契约 |
| 33 | `public bool IsStunned => _stunTimer > 0f;` | **表达式体属性，只读。** 没有 setter——外部**不能**强制解除或施加硬直，只能「打一下」（`TakeDamage`）然后等它自然结束。<br>**`> 0f` 而不是 `>= 0f`**：`_stunTimer` 归零时应该算「硬直结束」，所以是严格大于。<br>**它是每帧现算的**，不是存在一个 `bool` 字段里——所以**永远不会和 `_stunTimer` 不同步** |

> #### `IsStunned`：一条属性串起三个脚本 ⭐
>
> 这是本文件里最值得讲的设计点。`IsStunned` 只占一行，但它是「受击硬直」这个手感要素的**全部对外接口**。
>
> **三个脚本读它，各自做不同的事：**
>
> | 脚本 | 读它的位置 | 硬直期间的行为 | 效果 |
> |---|---|---|---|
> | `PlayerController` | `FixedUpdate` 开头 | **放弃操作权**——不处理跳跃输入、不执行水平移动 | 击退的速度不会被「按方向键往回走」抵消掉，人能真正被打飞 |
> | `MeleeAttacker` | 攻击前 | **不能攻击** | 被打的时候没法立刻反击，攻防有来有回 |
> | `EnemyController` | `FixedUpdate` 开头 | **不移动、不攻击** | 敌人被打中会有「僵住一下」的反馈，而不是边挨打边推进 |
>
> **为什么这三个判断不能由 `HitReaction` 自己来做？**
>
> 因为它**做不到**：
> - 它不知道玩家按了什么键（那是 `PlayerInputReader` 的事）
> - 它不知道攻击冷却好了没有（那是 `MeleeAttacker` 的事）
> - 它不知道敌人锁定目标了没有（那是 `EnemyController` 的事）
>
> **`HitReaction` 只能提供「事实」（现在处于硬直），不能决定「别人该怎么反应」。**
>
> 反过来说，如果它强行去关掉别的脚本，就会变成：
> ```csharp
> // ❌ 绝对不能这么写
> _playerController.enabled = false;    // HitReaction 依赖了控制层！
> ```
> 这一行会让「表现层」反向依赖「控制层」，架构直接塌掉。而且 `HitReaction` 挂到敌人身上就会报错（没有 `PlayerController`）。
>
> > **一个只读属性 + 三处主动查询，是解耦的标准做法。**
> > 这叫「拉模型（pull）」——需要信息的脚本主动来读；`HitReaction` 不推送。
> > 对比「推模型（push）」：事件就是推模型（`Damaged` 事件主动推给订阅者）。**两种模型都用在了正确的地方**：
> > - `Damaged` 用推：因为订阅者需要**精确知道每一次伤害**（闪白要重置计时器，漏一次就错了）
> > - `IsStunned` 用拉：因为使用者只需要知道**当前状态**（每帧查一次就行，错过中间某次变化无所谓）
>
> **这四个脚本的协作方式，就是你能在答辩上讲的那句「用状态查询解耦表现层与控制层」。**

### 4.4 `Awake`（第 35~42 行）

```csharp
35:     private void Awake()
36:     {
37:         _health = GetComponent<Health>();
38:         _sprite = GetComponentInChildren<SpriteRenderer>();
39:         _rb     = GetComponent<Rigidbody2D>();
40: 
41:         if (_sprite != null) _originalColor = _sprite.color;
42:     }
```

| 行 | 代码 | 含义 |
|---|---|---|
| 35 | `private void Awake()` | 对象创建时调用一次。用来做「找组件、缓存引用」这类一次性工作 |
| 37 | `_health = GetComponent<Health>();` | 在**同一个 GameObject** 上找 `Health`。由 `[RequireComponent]` 保证存在，所以**不需要判空** |
| 38 | `_sprite = GetComponentInChildren<SpriteRenderer>();` | **注意是 `InChildren` 而不是 `GetComponent`**——见下方详解 |
| 39 | `_rb     = GetComponent<Rigidbody2D>();` | 找刚体。**用了对齐空格**（`_rb` 后面的多个空格），让三个等号能竖着对齐——纯粹是排版，方便一眼看出「这三行在做同一件事」 |
| 40 | （空行） | 把「拿引用」和「用它」分开 |
| 41 | `if (_sprite != null) _originalColor = _sprite.color;` | **把精灵当前的（也就是「本来」的）颜色记下来。**<br>• 判空是必须的——没有精灵的物体（比如纯逻辑的触发区域）不该崩<br>• 单语句 `if` 省略花括号<br>**为什么必须缓存，不能在第 53 行直接写 `Color.white`？** 见第 4.5 节 |

> #### 为什么是 `GetComponentInChildren<SpriteRenderer>()` 而不是 `GetComponent<SpriteRenderer>()`
>
> 两个方法的区别：
>
> | 方法 | 搜索范围 |
> |---|---|
> | `GetComponent<T>()` | **只在当前 GameObject 自己身上**找 |
> | `GetComponentInChildren<T>()` | 当前 GameObject **以及它所有的子物体**（递归向下）里找 |
>
> **为什么这里需要 `InChildren`？**
>
> 因为在 Unity 2D 里，**精灵经常不是挂在根物体上的，而是挂在一个子物体上**。这是很常见的组织方式：
>
> ```
> Player                    ← 挂 Rigidbody2D / CapsuleCollider2D / Health / HitReaction
> ├── Sprite               ← 挂 SpriteRenderer（精灵单独一层，方便做翻转、缩放）
> ├── WeaponPoint          ← 武器挂点（攻击判定的起点）
> └── GroundCheck          ← 地面检测点
> ```
>
> **为什么要这样拆？** 因为「碰撞体」和「精灵」经常需要不同的偏移和缩放：
> - 碰撞体要贴合身体（不受动画影响）
> - 精灵可能会随着动画被拉伸、被翻转做朝向
>
> 如果把 `SpriteRenderer` 和 `CapsuleCollider2D` 放在同一个物体上，改精灵的缩放就会连带改变碰撞体——而它们的尺寸需求是独立的。
>
> **用 `InChildren` 的代价**：如果物体上有多个子精灵（比如角色 + 武器 + 影子），`GetComponentInChildren` 只会返回**找到的第一个**，顺序不确定。
> 本项目目前每个角色只有一个精灵，所以没问题。**如果以后要闪白整个角色（身体 + 武器）**，`GetComponentInChildren` 就不够了，需要改成 `GetComponentsInChildren<SpriteRenderer>()`（复数）并遍历。
>
> > **一个折中的写法**（更明确，但代码长一点）：
> > ```csharp
> > _sprite = GetComponent<SpriteRenderer>();                       // 先找自己
> > if (_sprite == null) _sprite = GetComponentInChildren<SpriteRenderer>();  // 再找子物体
> > ```
> > 这样「精灵在自己身上」的情况优先命中，行为更可预测。**当前用的是简单版本**，因为两种布局都能覆盖，而且少三行代码。
> >
> > ⚠️ **注意 `GetComponentInChildren` 默认不包含未激活（inactive）的子物体。** 如果精灵子物体被 `SetActive(false)` 过，这里会找不到。需要的话用 `GetComponentInChildren<SpriteRenderer>(true)`——那个 `true` 参数表示「包含未激活的」。

### 4.5 `OnEnable` / `OnDisable`（第 44~45 行）

```csharp
44:     private void OnEnable()  => _health.Damaged += OnDamaged;
45:     private void OnDisable() => _health.Damaged -= OnDamaged;
```

| 行 | 代码 | 含义 |
|---|---|---|
| 44 | `private void OnEnable()  => _health.Damaged += OnDamaged;` | **表达式体方法（expression-bodied method）。** 整行等价于：<br>`private void OnEnable() { _health.Damaged += OnDamaged; }`<br>**`+=` 是事件订阅**——把 `OnDamaged` 这个方法登记到 `Damaged` 事件的调用列表里。之后 `Health` 调用 `Damaged?.Invoke(info)` 时，`OnDamaged` 就会被调用。<br>（`OnEnable` 后面有两个空格，是为了和下一行的 `OnDisable` 对齐） |
| 45 | `private void OnDisable() => _health.Damaged -= OnDamaged;` | **`-=` 是事件退订**——把 `OnDamaged` 从调用列表里移除。<br>**`OnDisable` 和 `OnEnable` 的执行时机**：物体被启用/停用时、物体被销毁时。**两者必须成对出现。** |

> #### 事件订阅为什么必须「在 `OnEnable` 加、在 `OnDisable` 减」
>
> **先讲结论：只加不减会出什么错。**
>
> 假设只写了第 44 行，忘了第 45 行。现在敌人被打死了，`EnemyController` 调用了 `Destroy(gameObject, 0.3f)`。
>
> 0.3 秒后物体被销毁。但是——**`Health` 组件上的 `Damaged` 事件的调用列表里，仍然登记着那个已经销毁的 `HitReaction.OnDamaged` 方法。**
>
> 下次 `Damaged?.Invoke(info)` 执行时，它会去调用一个**已经不存在的对象上的方法**：
>
> ```
> MissingReferenceException: The object of type 'HitReaction' has been destroyed
> but you are still trying to access it.
> ```
>
> **这个报错的可怕之处在于它的定位难度**：
> - 报错指向 `Health.cs` 第 62 行（`Damaged?.Invoke`），但那一行完全没问题
> - 真正的问题在几百行之外的某个 `Destroy` 调用上
> - 而且**只有当「死掉的敌人刚好又被攻击到」时才复现**——间歇性的
>
> **再讲为什么用 `OnEnable`/`OnDisable` 而不是 `Start`/`OnDestroy`：**
>
> | 生命周期 | 触发时机 |
> |---|---|
> | `Awake` | 对象创建时，**只一次** |
> | `OnEnable` | **每次**被启用时（包括第一次，以及每次 `SetActive(true)` 后） |
> | `Start` | 第一次启用后，**只一次** |
> | `OnDisable` | **每次**被停用时 + 销毁前 |
> | `OnDestroy` | 销毁时，**只一次** |
>
> **关键差别在 `SetActive(false)`：**
>
> ```
> SetActive(false)  →  触发 OnDisable，不触发 OnDestroy
> SetActive(true)   →  触发 OnEnable，不触发 Start（Start 一辈子只跑一次）
> ```
>
> 所以如果用 `Start`/`OnDestroy` 配对：
>
> ```csharp
> // ❌ 有 bug 的写法
> private void Start()     => _health.Damaged += OnDamaged;
> private void OnDestroy() => _health.Damaged -= OnDamaged;
> ```
>
> 场景：把一个敌人 `SetActive(false)` 放进对象池，之后再 `SetActive(true)` 取出来。
>
> | 时刻 | `Damaged` 调用列表 |
> |---|---|
> | 第一次启用 | `[OnDamaged]` ← `Start` 加了一次 |
> | `SetActive(false)` | `[OnDamaged]` ← **`OnDestroy` 没触发，没减掉！** |
> | `SetActive(true)` | `[OnDamaged]` ← `Start` **不会**再跑了，所以没重复加 |
>
> 这个具体场景下碰巧没出问题。但如果对象池的实现方式是「销毁 + 重新实例化」，或者你的代码里手动调过订阅，就会累积。**更糟的是这个 bug 是潜伏的**——你现在不用对象池，所以它不发作；等你第 3 周为了性能引入对象池，它突然爆发，而那时你已经忘了这里。
>
> **用 `OnEnable`/`OnDisable` 就没有这个问题**：它们**永远成对触发**，启用一次加一次，停用一次减一次，无论中间经历多少次开关。
>
> > **一句话记住这个规矩：**
> > **事件的「加」和「减」写在 `OnEnable` / `OnDisable` 里，永远不用 `Start` / `OnDestroy`。**
> >
> > 这条规矩已经写进项目的架构约定（`00-总览与阅读指南.md` 第四节第 3 条）。

### 4.6 `Update`（第 47~58 行）

```csharp
47:     private void Update()
48:     {
49:         if (_flashTimer > 0f)
50:         {
51:             _flashTimer -= Time.deltaTime;
52:             if (_flashTimer <= 0f && _sprite != null)
53:                 _sprite.color = _originalColor;
54:         }
55: 
56:         if (_stunTimer > 0f)
57:             _stunTimer -= Time.deltaTime;
58:     }
```

| 行 | 代码 | 含义 |
|---|---|---|
| 47 | `private void Update()` | 每帧一次。**注意是 `Update` 不是 `FixedUpdate`**——倒计时用的是 `Time.deltaTime`（可变帧长），两者搭配才正确。`FixedUpdate` 配 `Time.fixedDeltaTime`，那是物理用的 |
| 49 | `if (_flashTimer > 0f)` | **外层判断：只有正在闪白时才处理。** 这个 `if` 不只是优化——它是第 52 行那个「还原颜色」动作的**触发器**：只在倒数到 0 的那一帧执行一次 |
| 51 | `_flashTimer -= Time.deltaTime;` | 递减。**先减，后判断**——这个顺序决定了还原发生在「减到 ≤ 0 的那一帧」 |
| 52 | `if (_flashTimer <= 0f && _sprite != null)` | **两个条件用 `&&`（逻辑与）连接。**<br>• `_flashTimer <= 0f`——**用 `<=` 而不是 `==`**：因为 `Time.deltaTime` 是浮点数，递减时几乎不可能正好等于 0，一定会从 `0.003` 直接跳到 `-0.008`。用 `== 0f` 判断**永远不会成立**，颜色就永远还原不了。<br>• `&& _sprite != null`——没有精灵的物体不该崩<br>**`&&` 的短路求值**：如果左边是 `false`，右边**根本不会被计算**。所以当 `_flashTimer` 还大于 0 时，`_sprite != null` 这次判空被跳过了——这是 `&&` 的性能特性，也是为什么判空可以写在后面 |
| 53 | `_sprite.color = _originalColor;` | **还原成 `Awake` 里缓存的那个颜色**（不是 `Color.white`！）。详细理由见下方 |
| 56~57 | `if (_stunTimer > 0f) _stunTimer -= Time.deltaTime;` | 硬直倒计时。**结构比闪白简单**——因为硬直结束时**什么都不用做**（`IsStunned` 会自动变成 `false`，不需要「还原」任何东西）。<br>单语句 `if`，省略花括号 |
| 55、58 | （空行 / `}`） | 两个倒计时块之间的空行，视觉上分隔两件独立的事 |

> #### 第 53 行：为什么是 `_originalColor` 而不是 `Color.white`
>
> **因为颜色可能是你在 Inspector 里调过的，不能硬编码。**
>
> 看 `SpriteRenderer` 组件上的 `Color` 字段（在 Inspector 的 `Sprite Renderer` 分组里）。假设你把它调成了**暗紫色** `(0.6, 0.3, 0.8)`——这是这个敌人的「本来颜色」。
>
> 现在它被打中了：
>
> ```
> 第 65 行：_sprite.color = flashColor;     → 变成白色（闪白）
> 第 53 行：_sprite.color = ???;            → 该还原成什么？
> ```
>
> | 如果第 53 行写 | 结果 |
> |---|---|
> | `Color.white` | ❌ 敌人**永远变成白色**，暗紫色设定丢失。而且下一次被打时「白色 → 白色」，**闪白效果完全看不见了** |
> | `_originalColor` | ✅ 还原成暗紫色，下次闪白仍然对比明显 |
>
> **`_originalColor` 在第 41 行（`Awake`）缓存，此时精灵还是原始状态**——这是唯一一个「确定精灵没被改过色」的时机。
>
> **如果放在 `Update` 里每次读 `_sprite.color` 当原色呢？** 那会读取到**闪白时的白色**，于是原色被污染成白色——和硬编码 `Color.white` 一样的错误，而且更难发现。
>
> > **一般化的教训：任何「临时改一下，然后改回来」的操作，都要在操作之前把原值存下来。**
> > 而且存的时机必须是「确定它还是原值」的时候——通常就是 `Awake`。
> > 这个模式在别处也出现过：`Health.SetMaxHealth` 第 73 行先算 `ratio` 再改 `maxHealth`，是同一个道理（**先记原值，再改**）。

> #### 为什么用 `Update` 递减，不用协程
>
> **协程版本长这样：**
> ```csharp
> // 另一种写法
> private void OnDamaged(DamageInfo info)
> {
>     if (_sprite != null)
>     {
>         _sprite.color = flashColor;
>         StopCoroutine(nameof(FlashRoutine));      // ← 必须先停掉上一个！
>         StartCoroutine(FlashRoutine());
>     }
> }
>
> private IEnumerator FlashRoutine()
> {
>     yield return new WaitForSeconds(flashDuration);
>     _sprite.color = _originalColor;
> }
> ```
>
> **三个问题：**
>
> **① 必须记得 `StopCoroutine`。** 如果 0.12 秒内被打中两次（高攻速敌人 / 多个敌人同时打），会启动两个协程。第一个协程在 0.12 秒后还原颜色——**但它还原的时候，第二次的闪白还没结束**，于是颜色提前变回去了，看起来像「第二次闪白没生效」。这个 bug 只在「短时间内连续受击」时出现，极难复现。
> （上面那行 `StopCoroutine(nameof(FlashRoutine))` 就是补这个洞的——但**你得先记得写它**。）
>
> **② 协程要分配内存。** 每个 `StartCoroutine` 都会创建一个迭代器对象。虽然一次性开销小，但在高攻速战斗里会累积成 GC 压力。（这和 `DamageInfo` 用 `struct` 是同一个考量。）
>
> **③ 状态被拆到了两个地方。** 「正在闪白」这个事实，在 `Update` 版本里是 `_flashTimer > 0f`（一个数字），在协程版本里是「有一个协程正在跑」（不可见的运行时状态）。**不可见的状态更难调试**——你没发在 Inspector 里看到「现在有几个协程在跑」。
>
> **`_stunTimer` 的情况更明显**：`IsStunned` 会被 `PlayerController`、`MeleeAttacker`、`EnemyController` **每帧查询**。如果硬直用协程实现，你需要一个 `bool _isStunned` 字段——**那就多了一份需要和协程同步的状态**（协程被打断时 `bool` 忘了归位，角色就永久失去操作权了）。
>
> **用数字计时器，`IsStunned` 就是现算的，不存在「不同步」这个失败模式。**
>
> > **通用原则：能用「一个数字 + 每帧递减」表达的状态，就不要用协程。**
> > 协程适合的是「有多个阶段的时序」（比如：播动画 → 等动画完 → 停顿 → 播下一个），不是「等 N 秒然后做一件事」。

### 4.7 `OnDamaged`——核心逻辑（第 60~84 行）

```csharp
60:     private void OnDamaged(DamageInfo info)
61:     {
62:         // 1. 闪白
63:         if (_sprite != null)
64:         {
65:             _sprite.color = flashColor;
66:             _flashTimer = flashDuration;
67:         }
68: 
69:         // 2. 硬直
70:         _stunTimer = stunDuration;
71: 
72:         // 3. 击退：水平远离伤害来源，垂直固定给一点上弹
73:         if (_rb != null && info.KnockbackForce > 0f)
74:         {
75:             Vector2 away = (Vector2)transform.position - info.SourcePosition;
76:             if (away.sqrMagnitude < 0.0001f) away = Vector2.right;   // 完全重合时给个默认方向
77:             away.Normalize();
78: 
79:             _rb.linearVelocity = new Vector2(
80:                 away.x * info.KnockbackForce,
81:                 info.KnockbackForce * verticalRatio
82:             );
83:         }
84:     }
```

| 行 | 代码 | 含义 |
|---|---|---|
| 60 | `private void OnDamaged(DamageInfo info)` | **事件处理方法。**<br>• 签名（参数类型和返回值）**必须和 `Damaged` 事件声明的一致**——`Health` 里写的是 `event Action<DamageInfo> Damaged`，所以这里必须是「接受一个 `DamageInfo`、返回 `void`」<br>• **`private`**——它是被事件调用的，不需要外部访问<br>• **`void`**——事件调用方（`Damaged?.Invoke`）不接收返回值 |
| 62 | `// 1. 闪白` | **编号注释。** 这三行注释（`// 1.`、`// 2.`、`// 3.`）把一个方法的作用域清晰地切成三块，让读者一眼知道「这里做了三件事」 |
| 63~67 | 闪白块 | 见下方 |
| 69~70 | 硬直块 | 见下方 |
| 72~83 | 击退块 | 见下方 |
| 84 | `}` | 方法结束。**注意第 83 行的 `}` 是 `if` 的结束，第 84 行是方法的结束**——两个花括号紧挨着，缩进不同 |

#### 第 62~67 行：闪白

| 行 | 代码 | 含义 |
|---|---|---|
| 63 | `if (_sprite != null)` | 判空。**没有精灵就不闪**，但**后面的硬直和击退照常执行**——这个 `if` 只包住闪白，不 `return` |
| 65 | `_sprite.color = flashColor;` | **直接改 `SpriteRenderer` 的 `color` 属性。** 这是最轻量的 2D 闪白实现方式——不改材质、不改 Shader、不生成任何对象，只是改一个颜色值，**性能开销几乎为零** |
| 66 | `_flashTimer = flashDuration;` | **重置倒计时。** 这一行让「连续受击」的行为变得合理：第二次被打中时，倒计时被重新充满，闪白从头开始。<br>**如果没有这一行**（或者只写了第 65 行），闪白会从第一次受击算起 0.12 秒后结束——高攻速下第二次闪白几乎没有视觉时间 |

> **为什么用改颜色，而不是做一张白色贴图或者用 Shader？**
>
> | 方案 | 开销 | 效果 | 适用 |
> |---|---|---|---|
> | **改 `SpriteRenderer.color`** | 几乎为零 | 整个精灵被染成单色，**丢失原来的明暗细节** | ✅ 现在用的 |
> | 换一张白色 Sprite | 要额外的美术资源 | 同上 | — |
> | 自定义 Shader（叠加白色） | 需要写 Shader | 保留明暗，只是「更亮」 | 表现更好，但 Day 4 还不值得 |
>
> **「丢失明暗细节」这个缺点是真实存在的**：一个带花纹的敌人被打中时，0.12 秒内会变成一个纯白的剪影。
> 但对 0.12 秒的时长来说，**这反而是好事**——纯白剪影的视觉冲击更强，而且时间短到玩家来不及注意「花纹没了」。
>
> 将来如果美术要求更高，第 65 行可以改成用 `MaterialPropertyBlock` 或者一个简单的 Shader Graph 来叠加白光。**改动被完全限制在这一行里。**

#### 第 69~70 行：硬直

| 行 | 代码 | 含义 |
|---|---|---|
| 69 | `// 2. 硬直` | 编号注释 |
| 70 | `_stunTimer = stunDuration;` | **一行，没有任何条件判断。**<br>**注意它不在任何 `if` 里**——这意味着：<br>• 有精灵没精灵 → 都进硬直<br>• 有刚体没刚体 → **都进硬直**（木桶也会进硬直，只是它没有 `EnemyController` 去读 `IsStunned`，所以没有实际效果）<br>**为什么硬直不需要判空？** 因为它只改一个 `float` 字段，不依赖任何外部组件。<br>**为什么和闪白一样要「重置」？** 同样的理由：连续受击时硬直应该重新计时，而不是从第一次算起 |

> **这一行是整个「受击手感」的入口。**
> 它把 `IsStunned` 变成 `true`（第 33 行现算），于是三个脚本在各自的下一个 `Update`/`FixedUpdate` 里读到它，自动让路。
> **`HitReaction` 完全不知道有谁在读它——这就是解耦。**

#### 第 72~83 行：击退算法（本文件最复杂的部分）

先看整块在做什么：

```
① 算出「从伤害来源指向我」的方向  →  away
② 如果方向是零向量，给个默认方向   →  防止 NaN
③ 把方向变成单位长度（只留方向）    →  Normalize()
④ 水平 = 方向 × 力度
   垂直 = 力度 × 0.4（固定上弹）
```

**逐行拆解：**

**第 73 行：`if (_rb != null && info.KnockbackForce > 0f)`**

| 部分 | 含义 |
|---|---|
| `_rb != null` | 判空。**没有刚体的物体（木桶）不能击退**——改了 `linearVelocity` 也没用，因为它根本不动 |
| `&&` | 逻辑与，短路求值 |
| `info.KnockbackForce > 0f` | **`DamageInfo` 里的击退力度大于 0 才击退。**<br>这是**攻击方控制「打不打得飞」的开关**。比如岩浆的 `AttackData.knockbackForce` 可以填 0——站在岩浆里会被烧，但不该被弹开。<br>**这个设计的好处**：同一个 `HitReaction` 脚本，不需要任何修改，就能表现「有击退的攻击」和「无击退的攻击」 |

**第 75 行：`Vector2 away = (Vector2)transform.position - info.SourcePosition;`**

这一行有两个知识点。

**知识点一：`(Vector2)` 是什么——显式类型转换**

| 表达式 | 类型 |
|---|---|
| `transform.position` | **`Vector3`**（有 x、y、z 三个分量） |
| `info.SourcePosition` | **`Vector2`**（只有 x、y） |

两个不同类型的向量不能直接相减。所以先把 `transform.position` 转成 `Vector2`：

```csharp
(Vector2)transform.position
```

这个 `(类型)值` 的语法叫**强制类型转换（cast）**。Unity 为 `Vector3` 和 `Vector2` 之间定义了转换运算符：
- `Vector3 → Vector2`：**丢掉 `z`**，保留 x 和 y
- `Vector2 → Vector3`：`z` 补 0

**丢掉 z 是安全的**，因为：
1. 2D 物理（`Physics2D`）**完全忽略 z**——碰撞检测只在 XY 平面上做
2. 本项目的所有物体 z 都是 0（相机在 z = -10）

> ⚠️ 如果哪天你想做「伪 3D」效果（物体按 z 排序做前后遮挡），**这里的 z 仍然会被丢掉**。因为击退只关心 XY 平面上的方向——**这个丢弃是正确的，不是权宜之计**。

**知识点二：这个减法的几何意义**

```
        info.SourcePosition ●━━━━━━━━━━━━━▶ 我（transform.position）
                            ←── away ──→
```

**`我 − 伤害来源 = 从来源指向我的向量`**，也就是**「背离来源」的方向**。

所以：
- 敌人从**左边**打我 → `away` 指向**右边** → 我往**右**飞 ✅
- 敌人从**右边**打我 → `away` 指向**左边** → 我往**左**飞 ✅
- 敌人从**上面**打我 → `away` 指向**下**（`away.y` 是负的）→ 见第 79 行的处理

> **这就是「击退」的全部原理：被打的时候，朝着远离攻击者的方向飞出去。**
> 而「攻击者在哪」这个信息，是攻击方在 `DamageInfo.SourcePosition` 里提供的——**注意 `SourcePosition` 存的是攻击者的位置，不是受击者的位置**。因为受击者可以用自己的 `transform.position`，而只有攻击方知道攻击是从哪来的。

**第 76 行：`if (away.sqrMagnitude < 0.0001f) away = Vector2.right;`**

**`sqrMagnitude` 是什么？**

它是向量的**长度平方**：`x² + y²`。

| 属性/方法 | 计算 | 开销 |
|---|---|---|
| `magnitude` | `√(x² + y²)` | **含一次开方** |
| `sqrMagnitude` | `x² + y²` | **只有乘法和加法** |

**开方（`Mathf.Sqrt`）比乘加慢很多。** 但注意：

> **什么时候可以用 `sqrMagnitude`？**
> 只有当你**不需要知道实际长度，只需要比较长度**的时候。
> - `if (away.sqrMagnitude < 0.0001f)` ✅ —— 比较，可以用
> - `float dist = away.magnitude;` ❌ —— 需要实际值，不能用
>
> **因为平方是单调递增的**（`a < b` 当且仅当 `a² < b²`，对非负数成立），所以比较平方值和比较长度是等价的。

**`0.0001f` 为什么是这个数？**

`0.0001 = 0.01²`。所以这个判断等价于「**`away` 的长度小于 0.01 个单位**」——在 Unity 的尺度里（1 个单位 ≈ 1 米，角色高 1 个单位），0.01 单位是**肉眼不可见的距离**，可以认为是「完全重合」。

**这个兜底在防什么？——NaN，一个会让物体永久消失的灾难。**

假设攻击者和受击者**位置完全重合**（比如地刺的 `SourcePosition` 就是受击者自己的位置），那么：

```
away = (0, 0)
```

然后第 77 行 `away.Normalize()`。`Normalize` 的实现是「每个分量除以长度」：

```
x / 长度 = 0 / 0 = NaN
y / 长度 = 0 / 0 = NaN
```

**`NaN` 是「Not a Number」（非数）**，它有一个可怕的特性：**任何和 `NaN` 的运算结果都是 `NaN`**。

于是第 79 行：

```csharp
_rb.linearVelocity = new Vector2(NaN * 9f, 9f * 0.4f);
//                    →  (NaN, 3.6)
```

刚体的速度变成 `NaN`。接下来：
- Unity 的物理引擎尝试用 `NaN` 更新位置 → 位置变成 `(NaN, NaN)`
- 物体的 `transform.position` 变成 `NaN`，**它就再也不会出现在屏幕上了**
- 它的所有后续物理运算都失效（因为它在一个「不存在的坐标」上）
- **Console 里不会有任何报错**

**这是 Unity 里最难查的一类 bug**：敌人「莫名其妙消失了」，没有任何错误信息，而且你盯着代码看——`Normalize()` 那一行看起来完全正常。

**`away = Vector2.right` 就是给这种情况一个默认方向**（`Vector2.right` 等于 `(1, 0)`，也就是「往右飞」）。

> **为什么选右边而不是左边？**
> 没有理由，纯粹是「总得选一个」。因为这种情况（位置完全重合）本来就极少出现，选哪边都一样。
> **关键是它必须是单位向量**——因为下一行会 `Normalize()`（对单位向量归一化还是它自己，无害），而且第 80 行会直接拿 `away.x` 当方向用。如果这里赋一个非单位向量（比如 `new Vector2(5, 0)`），击退力度就会被意外放大 5 倍。
>
> **用 `Vector2.right` / `Vector2.up` / `Vector2.zero` 这些内置常量比手写 `new Vector2(1, 0)` 更好**——名字自带含义，而且它们都是单位向量（`zero` 除外）。

**第 77 行：`away.Normalize();`**

**把向量变成单位长度（长度为 1），只保留方向。**

```
归一化前：(3, 4)     长度 = 5
归一化后：(0.6, 0.8) 长度 = 1
```

**为什么需要归一化？** 因为下一行要「方向 × 力度」：

```csharp
away.x * info.KnockbackForce
```

如果不归一化，`away` 的长度就是「攻击者和受击者的距离」。那么**站得越远，被打飞得越远**——而敌人通常是在近距离攻击的，距离很小，击退就会变得**几乎察觉不到**。

归一化之后，`away.x` 的范围是 `-1 ~ 1`，**击退力度就完全由 `KnockbackForce` 决定，与距离无关**。这是可预测的行为。

> ⚠️ **`Normalize()` 和 `normalized` 的区别（容易混淆）：**
>
> | 写法 | 效果 |
> |---|---|
> | `away.Normalize();` | **修改 `away` 自己**，返回 `void` |
> | `Vector2 n = away.normalized;` | **不修改 `away`**，返回一个新的单位向量 |
>
> 这里用 `Normalize()`（动词形式）是对的——因为 `away` 是个局部变量，改了它没有副作用。（`Vector2` 是 `struct`，`away` 是值类型变量，`Normalize()` 改的是这个局部变量本身。）
>
> **如果 `away` 是一个字段**（比如 `private Vector2 _away;`），就要小心了——`Normalize()` 会把字段也改掉，可能不是你想要的。

**第 79~82 行：赋速度**

```csharp
_rb.linearVelocity = new Vector2(
    away.x * info.KnockbackForce,
    info.KnockbackForce * verticalRatio
);
```

| 部分 | 含义 |
|---|---|
| `_rb.linearVelocity` | **刚体的速度**，类型是 `Vector2`，单位是「单位/秒」。<br>⚠️ **Unity 6 的新名字**：在 Unity 6 之前这个属性叫 `velocity`，现在改名成 `linearVelocity`（为了和 `angularVelocity` 角速度区分得更清楚）。老教程里看到的 `_rb.velocity` 在 Unity 6 里已经**弃用**了——虽然还能编译（会有警告），但新代码应该用 `linearVelocity` |
| `new Vector2(...)` | 创建一个新的二维向量。**两个参数按顺序是 x 和 y** |
| `away.x * info.KnockbackForce` | **水平速度 = 背离方向 × 力度。** 因为 `away` 已经归一化，`away.x` 在 `-1 ~ 1` 之间：<br>• `away.x = 1`（来源在正左方）→ 水平速度 = `+KnockbackForce`（往右飞）<br>• `away.x = -1`（来源在正右方）→ 水平速度 = `-KnockbackForce`（往左飞）<br>• `away.x ≈ 0`（来源在正上/正下方）→ 水平速度 ≈ 0（原地起跳） |
| `info.KnockbackForce * verticalRatio` | **垂直速度 = **固定** 的力度 × 0.4。**<br>⚠️ **注意这里没有 `away.y`！这是故意的。** 见下方 |

> #### ⭐ 为什么垂直方向不用 `away.y`，而用固定的 `KnockbackForce × verticalRatio`
>
> 这是整个击退算法里**最重要的一个设计决策**。如果写成 `away.y * info.KnockbackForce`（和水平方向对称），会出现三个问题：
>
> **问题一：同水平线上打，完全没有「被打飞」的感觉。**
>
> 2D 横板游戏里，**绝大多数攻击来自正左方或正右方**——`away.y ≈ 0`。
>
> ```
> 敌人 ●━━━━━━━▶ 我         away = (1, 0.02)   ← y 几乎是 0
> ```
>
> 用 `away.y` 的话，垂直速度 ≈ 0。结果是：**角色贴着地面水平滑走**。
>
> 那不是「被打飞」，那是「被推了一把」。观感差别很大：
>
> | | 水平滑走 | 被打飞 |
> |---|---|---|
> | 表现 | 脚不离地，像踩到冰面 | 有一个明显的上抛弧线 |
> | 玩家感受 | 「我被推了」 | 「我被打中了」 |
> | 视觉 | 位移在水平轴上，不显眼 | 位移在垂直轴上，很显眼 |
>
> **问题二：从上面打下来时，人会被砸进地里。**
>
> ```
> 敌人
>   ●
>   │
>   ▼      away = (0.02, -1)   ← y 是负的（向下）
>  我
> ```
>
> 用 `away.y * KnockbackForce` 会得到**负的垂直速度**——角色被**往下压**。而它本来就站在地面上，于是被压进地面、被物理引擎推出来、反复抖动。**看起来很糟糕。**
>
> **问题三：方向语义混乱。**
>
> 玩家会困惑：「为什么从左边打我往右飞，从上面打我往下掉？」——**击退方向不可预测，就没法形成肌肉记忆。**
>
> **固定上弹解决了全部三个问题：**
>
> ```
> 无论从哪个方向打来，受击者都被打得往上弹一下。
> ```
>
> | 攻击方向 | 水平速度 | 垂直速度 | 表现 |
> |---|---|---|---|
> | 从左边打 | `+KnockbackForce` | `+KnockbackForce × 0.4` | 向右上飞 ✅ |
> | 从右边打 | `−KnockbackForce` | `+KnockbackForce × 0.4` | 向左上飞 ✅ |
> | 从上面打 | `≈ 0` | `+KnockbackForce × 0.4` | 往上弹 ✅ |
> | 从下面打 | `≈ 0` | `+KnockbackForce × 0.4` | 往上弹 ✅ |
>
> **注意最后两行**：从上面打和从下面打，结果**一样**（都是往上弹）。这在几何上「不对」，但在**手感上是对的**——因为 2D 横板游戏里很少从正上方攻击，而且「被打得跳起来一下」在两种情况下都说得通。
>
> > **一句话总结**：**水平方向用几何（真实地背离来源），垂直方向用手感（固定上弹）。**
> >
> > 这是一个**故意打破几何正确性来换取打击感**的设计。类似的取舍在动作游戏里到处都是——**真实的不一定好玩**。
>
> **`verticalRatio = 0.4` 这个值的调参感觉：**
>
> | 值 | 效果 |
> |---|---|
> | `0` | **纯水平滑走**——「被推了一把」，没有打击感 |
> | `0.2` | 轻微上弹，几乎看不出来 |
> | **`0.4`** | ✅ **明显的上抛弧线，但落点还在合理范围** |
> | `0.6` | 弹得很高，像被挑飞了 |
> | `1.0` | **垂直起飞**——水平和垂直一样大，45° 斜着飞出去，失去「被击退」的感觉 |
>
> 用敌人打玩家的例子算一下（`Attack_Enemy.knockbackForce = 9`）：
> ```
> 水平速度 = 9 × 1 = 9 单位/秒
> 垂直速度 = 9 × 0.4 = 3.6 单位/秒
> ```
> 结合玩家的重力设定（`riseGravityScale = 3.5`、物理重力 9.81，实际上升加速度 ≈ `3.5 × 9.81 ≈ 34`），垂直速度 3.6 能上升：
> ```
> h = v² / (2a) = 3.6² / (2 × 34) ≈ 0.19 单位
> ```
> 大约是角色身高的 **19%**——**一个刚好能看出来的小跳，但不会让玩家失去位置感。** 这个数值是合理的。

> #### 为什么是 `_rb.linearVelocity = ...` 而不是 `AddForce`
>
> **`AddForce` 的版本会是这样：**
> ```csharp
> // ❌ 不用这个
> _rb.AddForce(new Vector2(away.x, verticalRatio) * knockbackForce, ForceMode2D.Impulse);
> ```
>
> **问题一：会和角色自己的移动输入打架。**
>
> `PlayerController.ApplyHorizontalMovement()`（`PlayerController.cs` 第 106~115 行左右）做的是：
> ```csharp
> float newSpeedX = Mathf.MoveTowards(_rb.linearVelocity.x, targetSpeed, rate * Time.fixedDeltaTime);
> _rb.linearVelocity = new Vector2(newSpeedX, _rb.linearVelocity.y);
> ```
>
> 它**每物理帧都把水平速度往「输入目标速度」推**。如果你用 `AddForce` 给了一个瞬间的力，下一帧 `ApplyHorizontalMovement` 就会开始把它拉回来。
>
> 结果：**击退效果会被角色的移动输入抵消掉大部分**，尤其是玩家正按着反方向键的时候。
>
> **这就是为什么 `PlayerController` 必须查询 `IsStunned` 并放弃操作权**——让路给击退。但如果同时用 `AddForce`，问题会更复杂：你既要让路，又要担心力的方向被物理引擎的其他作用影响。
>
> **问题二：`AddForce` 受质量影响。**
>
> `AddForce` 的加速度是 `力 / 质量`。如果哪天你把玩家的 `Rigidbody2D.mass` 从 1 改成 2（比如想做「重甲战士」），击退距离就变了——**而且不是因为 `KnockbackForce` 变了**，很难察觉。
>
> **`linearVelocity = ...` 是直接赋值，完全绕过质量**——`KnockbackForce` 是多少，速度就是多少，可预测。
>
> **问题三：`AddForce` 是「累加」，`=` 是「替换」。**
>
> 连续受击时，`AddForce` 会把力叠加起来（被打三次飞得更远），`=` 每次都重置成新值（击退距离一致）。
>
> 对动作游戏来说，**一致、可预测的手感更重要**。玩家需要能形成肌肉记忆：「被打一下我会飞多远」——如果这个距离随受击次数累积变化，就没法预判了。
>
> > **代价**：直接赋值 `linearVelocity` 会**抹掉角色当前的所有速度**。如果敌人正在跳跃上升（`velocity.y = 8`），被打中后 `y` 变成 `3.6`——**上升被打断了**。
> >
> > 但这是**想要的**：被打了就该掉下来，不该继续往上飘。**「打断」本身就是打击感的一部分。**

---

## 五、在 Unity 里怎么配

### 5.1 挂到哪些物体上

| 物体 | 挂 `HitReaction` | 需要 `Health` | 需要 `SpriteRenderer` | 需要 `Rigidbody2D` |
|---|---|---|---|---|
| **Player** | ✅ | ✅（必须） | ✅ | ✅ |
| **Enemy** | ✅ | ✅（必须） | ✅ | ✅ |
| **木桶 / 可破坏物** | 可选 | ✅（必须） | 有就闪 | **没有就不击退** |
| 静态陷阱（地刺本体） | ❌ **不要挂** | ❌ | — | — |

> **最后一行值得说明**：地刺是「施加伤害的东西」，它自己不需要受击表现。
> 它挂的是 `DamageZone`（负责对**别人**施加伤害），不是 `Health` + `HitReaction`（负责**自己**承受伤害）。
> **这一对是项目里伤害接口「双向性」的证明**——同一套接口，既能被打，也能打人。

### 5.2 四个字段填什么

| 字段 | 玩家 | 敌人 | 说明 |
|---|---|---|---|
| **Flash Color** | `White`（默认） | `White` | 默认值就是最佳值，一般不用改 |
| **Flash Duration** | `0.12` | `0.12` | 0.08~0.25 之间才有「闪」的感觉 |
| **Stun Duration** | `0.18` | `0.18` | 与闪白同步，但比它长 |
| **Vertical Ratio** | `0.4` | `0.4` | `[Range(0,1)]` 滑条，拖不到非法值 |

> ⚠️ **玩家的 `Stun Duration` 要小心。** 0.18 秒对玩家来说是「失去控制」的时间。如果调成 0.4 秒，玩家在连续受击时会觉得「按键没反应」——**这是最容易被抱怨的手感问题之一**。
> 敌人可以给得长一点（0.25）让打击感更强，因为它们不会抱怨。

### 5.3 关于 `Sprite Renderer` 的颜色 ⭐

**这一条必须理解，否则你会遇到「敌人闪一下之后变成白色了」。**

`SpriteRenderer` 组件上有一个 `Color` 字段（在 Inspector 的 `Sprite Renderer` 分组里，默认是白色）。

**`HitReaction` 在 `Awake` 时会把那个颜色缓存成 `_originalColor`，闪白结束后还原成它。**

所以：

| 你在 `Sprite Renderer` 里填的颜色 | 闪白结束后还原成 |
|---|---|
| 白色（默认） | 白色 |
| 暗紫色 `(0.6, 0.3, 0.8)` | **暗紫色** ✅ |
| 半透明 `Alpha = 0.5` | **半透明** ✅ |

**这个字段是你调敌人外观的地方**，`HitReaction` 会尊重它。

> ⚠️ **不要**在别的地方用代码改 `SpriteRenderer.color`（比如做一个「中毒变绿」的效果）。
> 因为 `HitReaction` 缓存的是 `Awake` 时刻的颜色——如果中毒把它改成绿色，然后被打中闪白再还原，它会还原成**正常颜色**，中毒的绿色消失了。
>
> **正确做法**：如果将来要做「状态染色」，需要把 `HitReaction` 改成「叠一层临时颜色」而不是「替换颜色」，或者引入一个统一的颜色管理器。
> **现在不需要，但要记住这个限制。**

### 5.4 一个完整的检查清单

给一个新物体加受击表现时：

```
1. 有 Health 吗？                ← 没有的话，拖 HitReaction 上去 Unity 会自动补
2. 有 SpriteRenderer 吗？        ← 没有就等于不闪白（不报错）
   └─ 在子物体上也可以（用了 GetComponentInChildren）
3. 有 Rigidbody2D 吗？           ← 没有就等于不击退（不报错）
4. 四个参数填了吗？
5. Sprite Renderer 的初始颜色设对了吗？   ← 决定闪白后还原成什么
6. 想让它「不能动」→ 确认对应脚本读 IsStunned
   ├─ 玩家 → PlayerController ✅ 已实现
   ├─ 敌人 → EnemyController ✅ 已实现
   └─ 玩家攻击 → MeleeAttacker ✅ 已实现
```

> ⚠️ **第 2、3 步的「不报错」是**静默失败**——这是本项目反复出现的一类问题（和 `LayerMask` 留空一样）。
> 如果不闪白，**先看有没有 `SpriteRenderer`，再看代码**。

---

## 六、踩过的坑

**暂无。**

`HitReaction` 本身没有出过 bug——它的逻辑足够简单（三个倒计时 + 一个方向计算），而且所有对它的修改都只发生在 `OnDamaged` 一个方法里。

> **但它有两个相邻的坑，记录在这里备查**（都属于「和它协作的脚本」的问题）：

### 6.1 相邻坑：加了击退，角色却纹丝不动

> **现象**：`OnDamaged` 里明明赋值了 `_rb.linearVelocity`，敌人也配置了 `knockbackForce = 9`。但受击时角色完全没有被推动的迹象。
>
> **根因**：击退赋值发生在 `OnDamaged`（由 `Health` 的事件触发，而事件来自 `TakeDamage`，`TakeDamage` 由 `MeleeAttacker.Update` 调用）。**下一物理帧**，`PlayerController.FixedUpdate` 会执行 `ApplyHorizontalMovement`：
> ```csharp
> float newSpeedX = Mathf.MoveTowards(_rb.linearVelocity.x, targetSpeed, deceleration * Time.fixedDeltaTime);
> ```
> 玩家的 `deceleration = 80`，`Time.fixedDeltaTime = 0.02`，所以**每物理帧能把速度拉回 1.6**。而击退水平速度是 9——**大约 6 个物理帧就被完全抹平了**。
> 视觉上就是「抖了一下，几乎看不出来」。
>
> **解法**：`PlayerController.FixedUpdate` 开头查询 `HitReaction.IsStunned`，硬直期间**跳过 `ApplyHorizontalMovement`**，让击退速度自由衰减。
>
> **这个坑的一般化教训**：
> > **「改一个组件的状态」和「另一个组件每帧在改同一个状态」放在一起时，一定要让后者知道前者正在生效。**
> > 本项目用的方式是「一个只读属性（`IsStunned`）+ 使用者主动查询」，而不是「`HitReaction` 去禁用别的脚本」（那会破坏分层）。
>
> **检查方法**：想看击退到底有没有生效，可以临时把 `deceleration` 调成 0——如果这时候能看到击退，就说明问题是「被移动逻辑抹平了」，而不是「击退没赋值」。

### 6.2 相邻坑：敌人被打时「飘」了一下

> **现象**：敌人受击后垂直弹起的效果看起来太强，像在跳，不像被打。
>
> **根因**：敌人的 `Rigidbody2D.gravityScale` 是 3，而 `HitReaction.verticalRatio` 是 0.4，配合 `Attack_Basic.knockbackForce = 7`：
> ```
> 垂直速度 = 7 × 0.4 = 2.8
> ```
> 上升高度 ≈ `2.8² / (2 × 3 × 9.81)` ≈ **0.13 单位**——其实不高。
>
> 真正让观感变「飘」的是**击退速度把敌人的追击速度完全覆盖了**，而敌人恢复后又要重新加速（`EnemyController` 的 `moveSpeed = 3.2`）。**中间那段时间敌人是「静止悬空」的**，看起来像飘。
>
> **解法（可选）**：把敌人的 `Stun Duration` 稍微调短（0.15），或者把敌人的 `moveSpeed` 调高一点，让它恢复得更快。
>
> **这一条记在这里的意义是**：击退参数不是孤立调的——**它和敌人的移动速度、硬直时长是耦合的**。调一个要想到另一个。

---

## 七、如果要改，改这里

### 7.1 加受击音效 / 粒子 / 屏幕震动 / 伤害飘字 ⭐ 最常见的扩展

**全部加在 `OnDamaged` 里，`Health` 一行都不用改。** 这就是这个脚本存在的理由。

```csharp
private void OnDamaged(DamageInfo info)
{
    // 1. 闪白
    if (_sprite != null) { /* ... 不变 ... */ }

    // 2. 硬直
    _stunTimer = stunDuration;

    // 3. 击退
    // ... 不变 ...

    // 4. 新增：音效
    if (_audioSource != null && hitSound != null)
        _audioSource.PlayOneShot(hitSound);

    // 5. 新增：粒子
    if (_hitParticles != null)
        _hitParticles.Play();

    // 6. 新增：伤害飘字（读 info 里的信息）
    if (damagePopupPrefab != null)
    {
        var popup = Instantiate(damagePopupPrefab, transform.position + Vector3.up, Quaternion.identity);
        popup.Setup(info.Amount, info.IsCritical);      // ← IsCritical 在这里第一次被用上
    }

    // 7. 新增：屏幕震动（只对玩家生效的写法）
    if (compareTag("Player"))
        CinemachineImpulseSource.GenerateImpulse();
}
```

**新增字段：**
```csharp
[Header("音效")]
[SerializeField] private AudioSource _audioSource;
[SerializeField] private AudioClip hitSound;

[Header("特效")]
[SerializeField] private ParticleSystem _hitParticles;
[SerializeField] private GameObject damagePopupPrefab;
```

> **注意第 6 条用到了 `info.IsCritical`。**
> 在 `04-DamageInfo.md` 里说过，这个字段一直是「有人写、没人读」——**它就是为这里预留的**。
> 飘字用它的方式：`info.IsCritical ? Color.yellow : Color.white`，暴击显示黄色大字。
>
> **这是 `DamageInfo` 设计正确性的一个证明**：加 `IsCritical` 的时候，`Health` 和 `HitReaction` 的函数签名一个字都没改。现在要用它，也不需要改任何签名——它已经在 `OnDamaged` 的参数里了。

> ⚠️ **性能提醒**：`Instantiate` 会分配内存。高攻速战斗里每次受击都 `Instantiate` 一个飘字，会带来 GC 压力。
> 到 Day 5 之后如果发现卡顿，把飘字改成**对象池**（预先创建 20 个，循环复用）。
> **现在不需要优化**，但要知道这个点在哪。

### 7.2 让不同敌人闪不同的颜色

**不需要改代码。** 每个敌人身上的 `HitReaction` 组件是独立配置的：

| 敌人 | Flash Color |
|---|---|
| 普通史莱姆 | `White` |
| 火元素 | `new Color(1, 0.6, 0.2)` 橙光 |
| 冰元素 | `new Color(0.6, 0.9, 1)` 蓝光 |
| Boss | `new Color(1, 0.3, 0.3)` 红光 + `flashDuration = 0.2`（更长，更重） |

**这是「组合优于继承」的实际收益**：不需要为每种敌人写一个 `FireHitReaction` 子类。

### 7.3 让受击时的击退方向更真实（用 `away.y`）

**不推荐**，但如果你想试试，把第 81 行改成：

```csharp
away.y * info.KnockbackForce + info.KnockbackForce * verticalRatio
```

这样是「几何方向 + 一点上弹」。**为什么仍然不推荐纯 `away.y`？** 见第 4.7 节的三个问题。

> **如果只是想微调**，改 `verticalRatio` 就够了（滑条在 Inspector 里）。改代码前先问：**我要的效果，是数值调整能解决的，还是需要改算法？** 90% 的情况是前者。

### 7.4 让「硬直」和「无敌帧」对齐

现在：
- `Health.invincibilityDuration = 0.5s`（无敌，但不影响操作）
- `HitReaction.stunDuration = 0.18s`（失去操作权，但仍会受伤）

**这是两个独立的概念，保持独立是对的。** 详见 `06-Health.md` 第 7.4 节。

如果确实想让它们联动，**正确方向是让 `HitReaction` 读 `Health`**（表现层依赖结算层的内部状态），而**不是**反过来。

### 7.5 加「受击方向」的判断（背面伤害加成）

`info.SourcePosition` 已经提供了判断所需的一切：

```csharp
// 判断是不是从背后打来的
Vector2 toAttacker = info.SourcePosition - (Vector2)transform.position;
bool fromBehind = Mathf.Sign(toAttacker.x) == Mathf.Sign(transform.localScale.x);
```

> ⚠️ 注意这里用的是 `transform.localScale.x`（朝向），因为本项目还没有做角色翻转的组件。
> 等你加了「角色朝向」之后（通常通过翻转 `SpriteRenderer.flipX` 或 `localScale.x`），这段判断才有意义。
>
> **这类「背刺加成」应该加在哪？**
> **不要加在 `HitReaction` 里**——因为它是**伤害逻辑**，不是表现。
> 正确位置是 `Health.TakeDamage`（那里能改 `amount`），或者更早——在 `MeleeAttacker` 算出伤害时就把加成算进 `DamageInfo.Amount`。
> **`HitReaction` 唯一该做的，是用 `SourcePosition` 决定「往哪个方向飞」。**

### 7.6 用 Shader 做更好的闪白效果

现在的实现是「把整个精灵染成单色」（第 65 行），会丢失明暗细节。

**升级方案**：用一个 Sprite Shader，加一个 `_FlashAmount` 属性，在片元着色器里把颜色往白色插值：

```hlsl
// SpriteFlash.shader 的核心（示意）
fixed4 frag(v2f i) : SV_Target
{
    fixed4 c = tex2D(_MainTex, i.uv) * i.color;
    c.rgb = lerp(c.rgb, fixed3(1,1,1), _FlashAmount);   // 叠加白光，保留明暗
    return c;
}
```

然后 `HitReaction` 改成用 `MaterialPropertyBlock` 控制每实例的 `_FlashAmount`：

```csharp
// ❌ 不能用 _sprite.material.SetFloat —— 那会创建材质副本，所有同材质物体一起闪
// ✅ 用 MaterialPropertyBlock，每个实例独立
private static readonly int FlashAmountId = Shader.PropertyToID("_FlashAmount");
private MaterialPropertyBlock _mpb;

private void Awake()
{
    _mpb = new MaterialPropertyBlock();
    // ...
}

private void Update()
{
    if (_flashTimer > 0f)
    {
        _flashTimer -= Time.deltaTime;
        float amount = Mathf.Clamp01(_flashTimer / flashDuration);   // 0~1 的强度
        _mpb.SetFloat(FlashAmountId, amount);
        _sprite.SetPropertyBlock(_mpb);
    }
}
```

> **这是 Day 5 之后可以考虑的优化，现在不要做。**
> 理由：它需要写 Shader，会引入「材质 / Shader Graph / URP 兼容性」等一系列新问题。**Day 4 的目标是把玩法跑通，不是把画面做好。**
> 而且升级时改动**完全局限在 `HitReaction` 里**（`Health` 不动）——这正是现在这个结构给你的自由。

### 7.7 不要在这里做的事

| ❌ 不要 | 为什么 | 应该在哪 |
|---|---|---|
| 判断「该不该受伤」 | `Health` 已经判断过了，重复判断会不一致 | `Health.TakeDamage` |
| 修改血量 | 那是结算层的职责 | `Health` |
| 直接禁用别的脚本（`_playerController.enabled = false`） | 会让表现层反向依赖控制层，架构塌掉 | 使用者主动查询 `IsStunned` |
| 播放受击动画（改 Animator） | **可以，但要小心**：动画播放期间如果又被 `SetActive(false)`，状态机会错乱 | 将来加一个专门的 `CharacterAnimator`，订阅同一个 `Damaged` 事件 |
| 处理死亡表现 | `Died` 是另一个事件，`HitReaction` 没有订阅它 | `EnemyController`（它已经订阅了 `Died`） |

> **最后一行说明一个有意思的点**：`HitReaction` **故意没有订阅 `Died`**。
> 因为「死亡看起来怎么样」是每个角色自己决定的——普通敌人「缩小淡出」，Boss「爆炸」，玩家「倒下」。
> 把它放在 `HitReaction` 里会让所有角色被迫共用一套死亡表现。**`EnemyController` 订阅 `Died` 才是正确的位置。**
>
> **判断标准**：这个行为是**所有**受伤者共有的（闪白、击退 → `HitReaction`），还是**某一类**物体特有的（死亡动画 → 各自的控制器）？
