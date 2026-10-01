# 测试目录

这个目录是空的 —— 但**它是这个项目里对简历最值钱的一个目录**，所以先建好放在这里。

## 为什么空着

Unity Test Framework 要求被测试的代码必须放在 **Assembly Definition（asmdef）** 里。
目前 `Assets/_Project/Scripts/` 下的代码还在默认的 `Assembly-CSharp` 程序集里，而 **asmdef 无法引用 `Assembly-CSharp`**，所以现在写测试是引用不到游戏代码的。

正确顺序是：

```
1. 先给 Scripts/ 加 asmdef        （见 00-行动清单.md 的 P2 加分项）
2. 再回来在这个目录写测试
```

## 该测什么

**只测纯逻辑，不要测 MonoBehaviour。** MonoBehaviour 的测试成本极高、收益极低。真正值得测的是：

| 优先级 | 测试对象 | 例子 |
| --- | --- | --- |
| ⭐⭐⭐ | 伤害计算 | 「基础 10 攻击 + 50% 加成 + 2 倍暴击 = 30 伤害」 |
| ⭐⭐⭐ | 关卡生成可复现性 | 「同一个种子生成两次，房间序列完全一致」 |
| ⭐⭐⭐ | 词条叠加 | 「两个 +20% 攻击词条是 ×1.44 而不是 ×1.4」 |
| ⭐⭐ | 存档序列化 | 「存进去再读出来，数据完全一致」「旧版本存档能正确加载」 |
| ⭐⭐ | 数值边界 | 「血量不会降到负数」「暴击率封顶 100%」 |

**加算 vs 乘算**的测试特别值得写 —— 这是数值系统最容易出 bug 的地方，也是面试时很好的谈资。

## 目录约定

```
Tests/
├── EditMode/     ← 纯逻辑测试，不需要进入 Play 模式，跑得飞快（推荐主放这里）
└── PlayMode/     ← 需要游戏运行时环境的测试（例如物理、协程）
```

## 写完测试后

- Unity 菜单 `Window → General → Test Runner` 里可以看到并运行
- 记得**截图存到 `docs/images/`**，简历和答辩 PPT 用得上
- 简历上可以写：「编写 20 个单元测试覆盖战斗数值计算与关卡生成可复现性」

## 参考

- [Unity Test Framework 官方文档](https://docs.unity3d.com/Packages/com.unity.test-framework@latest)
- 本项目的测试包已装好（`com.unity.test-framework`，见 `Packages/manifest.json`）
