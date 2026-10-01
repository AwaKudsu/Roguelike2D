# ============================================================
#  Roguelike2D - 开发环境一键配置脚本
#
#  用途：在新机器上克隆仓库后，一条命令配好 Git / LFS / Unity SmartMerge。
#        脚本是幂等的，可重复执行。
#
#  用法（必须在工程根目录，用 -ExecutionPolicy Bypass 绕过执行策略限制）：
#      powershell -ExecutionPolicy Bypass -File Tools\setup-git.ps1
#
#  注意：本文件必须保存为「UTF-8 带 BOM」。
#        Windows PowerShell 5.1 在没有 BOM 时会按系统 ANSI(GBK) 解码，
#        中文会变成乱码并导致语法错误。
# ============================================================

# 不使用 Stop：git 这类原生命令会往 stderr 写正常信息，Stop 会误判为错误
$ErrorActionPreference = 'Continue'

function Write-Step  { param($m) Write-Host $m -ForegroundColor Cyan }
function Write-Ok    { param($m) Write-Host "      [OK]   $m" -ForegroundColor Green }
function Write-Warn2 { param($m) Write-Host "      [警告] $m" -ForegroundColor Yellow }
function Write-Bad   { param($m) Write-Host "      [错误] $m" -ForegroundColor Red }

Write-Host ""
Write-Step "=== Roguelike2D 开发环境配置 ==="
Write-Host ""

# ---------- 1. 定位工程根目录 ----------
$repoRoot = Split-Path -Parent $PSScriptRoot
if (-not (Test-Path (Join-Path $repoRoot 'ProjectSettings\ProjectVersion.txt'))) {
    Write-Bad "找不到 ProjectSettings\ProjectVersion.txt，请在 Unity 工程根目录下运行本脚本"
    exit 1
}
Write-Step "[1/5] 工程根目录：$repoRoot"

# ---------- 2. 读取 Unity 版本，定位 SmartMerge ----------
$versionFile  = Join-Path $repoRoot 'ProjectSettings\ProjectVersion.txt'
$versionLine  = Select-String -Path $versionFile -Pattern '^m_EditorVersion:\s*(.+)$'
$unityVersion = $versionLine.Matches[0].Groups[1].Value.Trim()
Write-Step "[2/5] 工程锁定的 Unity 版本：$unityVersion"

$mergeExe = "C:\Program Files\Unity\Hub\Editor\$unityVersion\Editor\Data\Tools\UnityYAMLMerge.exe"
if (-not (Test-Path $mergeExe)) {
    Write-Warn2 "未找到 SmartMerge：$mergeExe"
    Write-Warn2 "场景合并冲突将无法自动处理，请确认 Unity $unityVersion 已安装"
    $mergeExe = $null
} else {
    Write-Ok "SmartMerge：$mergeExe"
}

# ---------- 3. Git 基础配置 ----------
Write-Step "[3/5] 配置 Git 基础项..."
$gitSettings = @{
    'init.defaultBranch' = 'main'
    'core.autocrlf'      = 'false'   # 换行符交给 .gitattributes，避免整文件 diff
    'core.safecrlf'      = 'false'
    'pull.rebase'        = 'true'    # 拉取用变基，历史保持直线
    'fetch.prune'        = 'true'
    'push.default'       = 'simple'
    'credential.helper'  = 'manager'
    'core.quotepath'     = 'false'   # 中文文件名不转义成 \xxx
    'commit.template'    = (Join-Path $repoRoot '.gitmessage')
}
foreach ($k in $gitSettings.Keys) {
    git config --global $k $gitSettings[$k] 2>&1 | Out-Null
}
Write-Ok "已应用 $($gitSettings.Count) 项 Git 配置"

# ---------- 4. Unity SmartMerge ----------
Write-Step "[4/5] 配置 Unity SmartMerge..."
if ($mergeExe) {
    $mergePath = $mergeExe -replace '\\', '/'
    $driver    = '"' + $mergePath + '" merge -p %O %B %A %A'
    git config --global merge.unityyamlmerge.name      "Unity SmartMerge" 2>&1 | Out-Null
    git config --global merge.unityyamlmerge.driver    $driver            2>&1 | Out-Null
    git config --global merge.unityyamlmerge.recursive binary             2>&1 | Out-Null
    Write-Ok "已注册合并驱动（.gitattributes 中的 *.unity / *.prefab 将走此工具）"
}

# ---------- 5. Git LFS ----------
Write-Step "[5/5] 配置 Git LFS..."
if (-not (Get-Command git-lfs -ErrorAction SilentlyContinue)) {
    Write-Warn2 "未安装 git-lfs，请先安装：https://git-lfs.com/"
} else {
    git lfs install 2>&1 | Out-Null
    $patterns = @(
        '*.png', '*.jpg', '*.jpeg', '*.psd', '*.tga', '*.tif', '*.tiff', '*.bmp', '*.gif', '*.exr', '*.hdr',
        '*.wav', '*.mp3', '*.ogg', '*.aiff',
        '*.fbx', '*.obj', '*.blend', '*.max', '*.ma', '*.mb',
        '*.ttf', '*.otf',
        '*.mp4', '*.mov', '*.webm',
        '*.cubemap', '*.unitypackage'
    )
    Push-Location $repoRoot
    foreach ($p in $patterns) { git lfs track $p 2>&1 | Out-Null }
    Pop-Location
    Write-Ok "已追踪 $($patterns.Count) 种二进制素材类型"
}

# ---------- 自检 ----------
Write-Host ""
Write-Step "=== 自检 ==="
Write-Host ""
Push-Location $repoRoot

Write-Host ("Git 身份   : {0} [{1}]" -f (git config user.name), (git config user.email))

$drv = git config --global --get merge.unityyamlmerge.driver
Write-Host ("SmartMerge : {0}" -f $(if ($drv) { '已配置' } else { '未配置' }))

if (Get-Command git-lfs -ErrorAction SilentlyContinue) {
    Write-Host ("Git LFS    : {0}" -f (git lfs version))
} else {
    Write-Host "Git LFS    : 未安装"
}

Write-Host ""
Write-Host "忽略规则检查（用 git check-ignore --no-index 判断，不依赖文件是否真实存在）："
foreach ($d in @('Library/', 'Temp/', 'Logs/', 'UserSettings/', 'Build/', 'obj/')) {
    git check-ignore --no-index -q $d 2>&1 | Out-Null
    if ($LASTEXITCODE -eq 0) { Write-Ok "$d 已忽略" }
    else { Write-Bad "$d 未被忽略，请检查 .gitignore" }
}
foreach ($d in @('ProjectSettings/', 'Assets/', 'Packages/')) {
    git check-ignore --no-index -q $d 2>&1 | Out-Null
    if ($LASTEXITCODE -eq 0) { Write-Bad "$d 被忽略了！它必须提交" }
    else { Write-Ok "$d 会被提交" }
}

Pop-Location
Write-Host ""
Write-Step "配置完成。"
Write-Host ""
