# Git 使用手册（Unity 项目专用）

> 目标：让你的 Git 提交历史**看起来像一个专业开发者**，而不是「最后一天把整个工程压缩包传上去」。

---

## 一、一次性配置（今天做完，以后不用管）

### 1.1 基础身份与习惯

```powershell
# 身份（你机器上已经配好了，这里仅作记录）
git config --global user.name  "Awasaki"
git config --global user.email "2140823449@qq.com"

# 默认分支名用 main（GitHub 现在的默认，避免 master/main 混乱）
git config --global init.defaultBranch main

# 【重要】不要让 Git 自动转换换行符，全部交给 .gitattributes 管
# 否则 Windows 上每个文件都显示「改了几千行」，diff 完全没法看
git config --global core.autocrlf false
git config --global core.safecrlf false

# 拉取时用变基而不是合并，历史是一条直线，更好看
git config --global pull.rebase true

# 本地删掉的分支，fetch 时远程也清掉
git config --global fetch.prune true

# 记住凭据，避免每次 push 都输密码
git config --global credential.helper manager
```

### 1.2 Unity SmartMerge（本手册最重要的部分）

**为什么需要它**：Unity 的 `.unity` 场景和 `.prefab` 预制体是 YAML 文本。Git 默认的逐行合并会产出一个「语法上合法、语义上完全错误」的文件——你打开工程会发现场景全白、组件全部丢失、引用变成 `Missing`。这是 Unity + Git 最经典的翻车现场。

Unity 自带了一个专门用来合并这类文件的工具 `UnityYAMLMerge.exe`（俗称 SmartMerge），要手动告诉 Git 去用它：

```powershell
$merge = 'C:/Program Files/Unity/Hub/Editor/6000.3.22f1/Editor/Data/Tools/UnityYAMLMerge.exe'

git config --global merge.unityyamlmerge.name  "Unity SmartMerge"
git config --global merge.unityyamlmerge.driver "`"$merge`" merge -p %O %B %A %A"
git config --global merge.unityyamlmerge.recursive binary
```

参数含义：`%O` = 共同祖先，`%B` = 对方版本，`%A` = 你的版本（同时作为输出文件）。

配合仓库里的 `.gitattributes`：

```
*.unity   merge=unityyamlmerge eol=lf
*.prefab  merge=unityyamlmerge eol=lf
*.asset   merge=unityyamlmerge eol=lf
*.meta    merge=unityyamlmerge eol=lf
```

**验证是否生效**：

```powershell
git config --global --get merge.unityyamlmerge.driver
```

**⚠️ 重要**：SmartMerge 只是**减少**冲突，不能消除。真正的保险是：

1. **两个人不要同时改同一个场景/预制体。** 约定「谁改场景，先在群里说一声」。
2. 每次开始工作前先 `git pull`。
3. 场景改完立刻提交，不要攒。
4. 场景/预制体一旦冲突且 SmartMerge 也处理不了，**从版本历史里恢复一份，重做改动**，比手工修 YAML 快得多：

```powershell
git checkout --theirs Assets/_Project/Scenes/Game.unity   # 用对方的版本
git checkout --ours   Assets/_Project/Scenes/Game.unity   # 用自己的版本
```

### 1.3 Git LFS（大文件存储）

**为什么需要它**：普通 Git 每次提交都存一份完整的文件副本。一张 2 MB 的贴图改 20 次，仓库就多 40 MB。一个学期的项目会轻易堆到几 GB，最后 `git push` 直接失败，而且**没法简单补救**（要重写历史）。

Git LFS 的做法是：仓库里只存一个指针文件，真身放在 LFS 存储区。

```powershell
git lfs install        # 每台机器执行一次

# 追踪所有二进制素材类型
git lfs track "*.png" "*.jpg" "*.jpeg" "*.psd" "*.tga" "*.tif" "*.bmp" "*.gif" "*.exr" "*.hdr"
git lfs track "*.wav" "*.mp3" "*.ogg" "*.aiff"
git lfs track "*.fbx" "*.obj" "*.blend" "*.max" "*.ma" "*.mb"
git lfs track "*.ttf" "*.otf"
git lfs track "*.mp4" "*.mov" "*.webm"
git lfs track "*.cubemap" "*.unitypackage"

git add .gitattributes     # 追踪规则写在 .gitattributes 里，必须提交
```

**⚠️ 必须注意的三件事**：

1. **必须在第一次提交大文件之前做。** 已经用普通 Git 提交过的文件，改成 LFS 需要重写历史（`git lfs migrate`），很麻烦。
2. **`git clone` 之后必须 `git lfs pull`**，否则所有美术资源都是 1 KB 的指针文件。
3. **GitHub 免费额度是 1 GB 存储 / 1 GB 月流量。** 个人项目通常够用，但如果要放大量高清素材，注意别超。

```powershell
git lfs ls-files        # 查看哪些文件已经被 LFS 接管
git lfs status          # 查看 LFS 状态
git lfs env             # 排查问题用
```

---

## 二、日常提交流程

### 2.1 黄金循环

```powershell
git pull                      # 1. 先同步，避免冲突
# ... 写代码 ...
git status                    # 2. 看看到底改了什么（这一步别跳过！）
git diff                      # 3. 具体看改动内容
git add Assets/_Project/Scripts/Player/PlayerController.cs
git add Assets/_Project/Scripts/Player/PlayerController.cs.meta   # ⚠️ .meta 必须一起提交
git commit -m "feat(player): 实现二段跳与土狼时间"
git push
```

**关于 `.meta`**：Unity 为每个资源生成一个 `.meta` 文件，里面存着这个资源的 **GUID**，所有引用都靠 GUID 找资源。新增/删除/移动资源时，`.meta` 必须跟资源一起提交。漏了它，别人的工程打开就是一堆 `Missing Reference`。

> 小技巧：用 `git add Assets/_Project/Scripts/` 加整个目录，就不会漏 `.meta`。

### 2.2 提交信息规范（Conventional Commits）

格式：`<类型>(<范围>): <描述>`

| 类型 | 用途 | 示例 |
| --- | --- | --- |
| `feat` | 新功能 | `feat(combat): 添加三段连击` |
| `fix` | 修 bug | `fix(player): 修复贴墙时无法跳跃` |
| `refactor` | 重构（不改行为） | `refactor(ai): 敌人状态机改为可配置` |
| `perf` | 性能优化 | `perf(vfx): 用对象池复用命中特效` |
| `docs` | 文档 | `docs: 补充关卡生成算法说明` |
| `test` | 测试 | `test(roguelike): 增加种子可复现性测试` |
| `chore` | 构建/配置 | `chore: 配置 Unity SmartMerge` |
| `art` | 美术资源 | `art(player): 替换为正式角色贴图` |
| `balance` | 数值调整 | `balance(relic): 削弱吸血词条` |

**为什么值得遵守**：老师/面试官看你的 commit 历史时，一眼就能看出你在什么时候做了什么、有没有工程习惯。**这条规范本身就是加分项。**

反面例子（大多数学生作业长这样）：
```
update
修改
111
最终版
最终版2
真的最终版
```
> 如果只有一条 `init` 提交，面试官会认为你的项目是抄的或是一次性生成的。**让提交历史讲出你 5 周的工作过程**，这是简历项目最有说服力的证据之一。

配置提交模板，省得每次想格式：

```powershell
git config --global commit.template .gitmessage
```

### 2.3 分支策略（个人开发版，别搞太复杂）

个人项目**不需要** Git Flow。推荐：

```
main          ← 永远是「能跑通」的版本，随时可以打包
 └── feat/room-generation     ← 一个功能一个分支
 └── feat/relic-system
 └── fix/jump-buffer
```

```powershell
git switch -c feat/relic-system    # 开新功能
# ... 开发、提交 ...
git switch main
git merge --no-ff feat/relic-system   # 合并（--no-ff 保留分支痕迹，历史更清楚）
git branch -d feat/relic-system       # 删掉已合并分支
git push
```

**为什么个人项目也要用分支**：因为你可以随时 `git switch main` 回到一个能跑的版本。发现自己把项目改崩了的时候，这个能力值千金。

### 2.4 每周五冻结（防止最后一周爆炸）

```powershell
git switch main
git pull
# 完整玩 3 局，确认没有致命 bug
git tag -a v0.2 -m "第 2 周：肉鸽骨架完成"
git push origin v0.2
```

5 周下来你会有 `v0.1` ~ `v1.0` 五个 tag。**这些 tag 就是你进度的铁证**，答辩时直接打开 GitHub 的 Releases 页面给老师看。

---

## 三、救命命令（出事时先来这里）

```powershell
# 改崩了，想丢掉所有未提交的改动（⚠️ 不可恢复）
git restore .

# 只想丢掉某一个文件的改动
git restore Assets/_Project/Scripts/Player/PlayerController.cs

# 已经 git add 了，想撤出暂存区（保留改动）
git restore --staged Assets/_Project/Scripts/Player/PlayerController.cs

# 提交信息写错了（还没 push）
git commit --amend -m "feat(player): 正确的信息"

# 提交漏了一个文件（还没 push）
git add 漏掉的文件
git commit --amend --no-edit

# 误删了一个文件，想找回来
git checkout HEAD -- 被删的文件路径

# 想看某一行代码是谁什么时候改的（排 bug 神器）
git log -p -- Assets/_Project/Scripts/Combat/DamageSystem.cs

# 想找回「被我删掉的分支 / 误 reset 的提交」
git reflog            # 列出所有 HEAD 移动记录，找到目标 commit 后：
git switch -c rescue <commit-hash>

# 已经 push 了但想撤销某次提交（用新提交抵消，不改历史，安全）
git revert <commit-hash>
```

**⚠️ 危险操作（个人项目里基本用不到，别碰）**：
- `git push --force` —— 会覆盖远程历史。真要强制推，用 `--force-with-lease`（更安全，会检查远程有没有别人的新提交）
- `git reset --hard` —— 丢弃所有未提交改动且不可恢复
- `git filter-branch` / `git rebase -i` 改已推送的历史

---

## 四、连接 GitHub

```powershell
# 建好仓库后（不要勾选自动生成 README/.gitignore/LICENSE，你本地已经有了）
git remote add origin https://github.com/<你的用户名>/Roguelike2D.git
git branch -M main
git push -u origin main
git push origin --tags
```

**仓库必须设为 Public**：这是简历项目，面试官打不开就等于不存在。

如果不想暴露个人信息，用 `.gitignore` 排除个人笔记就好，**不要**把整个仓库设为私有。

---

## 五、`.gitignore` 里为什么这些必须忽略

| 目录 | 大小 | 为什么必须忽略 |
| --- | --- | --- |
| `Library/` | 几 GB | Unity 的资源导入缓存，包含本机绝对路径，且可完整重建 |
| `Temp/` | 不定 | 临时文件，编辑器关闭时清空 |
| `Logs/` | 小 | 日志，每次运行都变，提交了就是噪音 |
| `UserSettings/` | 小 | 只属于你本机的窗口布局、上次打开的场景 |
| `Build/` | 大 | 打包产物，应该发到 GitHub Releases 而不是塞进仓库 |
| `*.csproj` `*.sln` | 小 | Unity 自动生成，每台机器路径不同，提交了必然冲突 |

**反过来，这两个绝对不能忽略**：
- `ProjectSettings/`　—— 工程设置，别人克隆下来靠它还原项目
- `Assets/**/*.meta`　—— 资源的 GUID 索引，丢了引用全断

---

## 六、自检清单

提交第一个版本前，逐条确认：

```powershell
git status                          # 不应该出现 Library/ Temp/ Logs/ UserSettings/
git lfs ls-files                    # 应列出你的 png/wav 等素材
git config --global --get merge.unityyamlmerge.driver   # 应有输出

# 检查忽略规则是否真的生效（注意 --no-index，见第七节坑 5）
git check-ignore --no-index -v "Library/"
git check-ignore --no-index -v "UserSettings/"
```

也可以一条命令跑完检查，仓库里的 `Tools/setup-git.ps1` 已经内置了这套自检：

```powershell
powershell -ExecutionPolicy Bypass -File Tools\setup-git.ps1
```

`git ls-files | Measure-Object -Line` 看一下仓库追踪了多少文件——**正常的 Unity 项目应该在几百个以内**。如果是几千个，八成是 `Library/` 被提交了。

真提交了 `Library/` 怎么办：

```powershell
git rm -r --cached Library        # 从索引里移除，但保留本地文件
git commit -m "chore: 移除误提交的 Library 目录"
```

---

## 七、五个真实踩坑记录

下面这些都是**配置本项目时实际踩到并修复的**坑。它们的共同点是：**报错信息完全指向不了真正的原因**，所以特别值得记住。

### 坑 1：`.gitignore` 不支持行尾注释 ⭐ 最坑

```gitignore
# ❌ 错误写法
/[Ll]ibrary/          # 资源导入缓存
```

这行**完全不会生效**。Git 会把整行当成要匹配的字面量 `Library/          # 资源导入缓存`，永远匹配不到任何文件。

**`#` 只有在行首才是注释。** 正确写法：

```gitignore
# ✅ 正确写法：说明单独占一行
# 资源导入缓存
/[Ll]ibrary/
```

**为什么这个坑特别危险**：`git status` 看起来一切正常（`Library/` 还没生成时什么都不显示），等你发现时 `Library/` 已经被提交了几百 MB 进去。

**怎么发现**：用 `git check-ignore` 逐个验证，别靠肉眼看。

### 坑 2：PowerShell 脚本在 PS 5.1 下需要 UTF-8 BOM

Windows PowerShell 5.1 读取 `.ps1` 文件时，**如果文件没有 BOM，会按系统 ANSI 编码（中文系统上是 GBK）解码**。结果就是脚本里的中文全变乱码，而且乱码字节会破坏字符串引号配对，报出一堆莫名其妙的语法错误：

```
The '<' operator is reserved for future use.
The string is missing the terminator: '.
Unexpected token '鏈厤缃?' in expression or statement.
```

**解决办法**：把 `.ps1` 存成 **UTF-8 with BOM**。

```powershell
$p = 'Tools\setup-git.ps1'
$text = [System.IO.File]::ReadAllText($p)
[System.IO.File]::WriteAllText($p, $text, (New-Object System.Text.UTF8Encoding($true)))  # $true = 带 BOM
```

或者干脆用 PowerShell 7（`pwsh`）运行，它默认按 UTF-8 读取，没有这个问题。

> 📌 本仓库的 `Tools/setup-git.ps1` **必须保留 BOM**，用编辑器改完记得确认没被去掉。

### 坑 3：`.NET` 方法用的是「进程工作目录」，不是 PowerShell 的位置

```powershell
Set-Location 'D:\Roguelike2D'
[System.IO.File]::ReadAllText('ProjectSettings\EditorSettings.asset')   # ❌ 报「找不到路径」
Select-String -Path 'ProjectSettings\EditorSettings.asset'              # ✅ 正常
```

PowerShell 的 cmdlet（`Select-String`、`Get-Content`）跟随 `Set-Location`，但 `.NET` 静态方法跟随的是**进程启动时的工作目录**（`[Environment]::CurrentDirectory`）。

**解决办法**：给 `.NET` 方法传**绝对路径**。

```powershell
$root = 'D:\Roguelike2D'
[System.IO.File]::ReadAllText((Join-Path $root 'ProjectSettings\EditorSettings.asset'))   # ✅
```

### 坑 4：Unity 的 YAML 文件不能带 BOM

`ProjectSettings/*.asset`、`.unity`、`.prefab`、`.meta` 都是 Unity 的 YAML 文件，**必须无 BOM**。用 PowerShell 改这些文件时要注意：

```powershell
# ❌ 某些环境下 Set-Content -Encoding UTF8 会写入 BOM
(Get-Content $f -Raw) -replace 'a','b' | Set-Content $f -NoNewline -Encoding UTF8

# ✅ 显式指定「无 BOM」
$noBom = New-Object System.Text.UTF8Encoding($false)   # $false = 不带 BOM
[System.IO.File]::WriteAllText($f, $content, $noBom)
```

**怎么检查**：

```powershell
$b = [System.IO.File]::ReadAllBytes('ProjectSettings\ProjectSettings.asset')
$b[0..2] -join ','      # 应该是 37,89,65（即 "%YA"）
                        # 如果是 239,187,191，就是混进了 BOM
```

### 坑 5：`git check-ignore` 检查目录要加 `--no-index`

```powershell
git check-ignore -v Library        # ❌ Library 还不存在时，可能匹配不到目录规则
git check-ignore --no-index -v "Library/"   # ✅ 不查索引，纯按规则匹配
```

`.gitignore` 里 `/[Ll]ibrary/` 结尾的斜杠表示**只匹配目录**。如果这个目录在磁盘上还不存在，也不加 `--no-index`，Git 无法判断它是目录，就会报告「未被忽略」——**这是误报，规则其实是对的**。

**最可靠的验证方式**是直接看 Git 会不会真的收录这个文件：

```powershell
git status --ignored --short | Select-String 'Library'
```
