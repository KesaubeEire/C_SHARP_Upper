# WebScada PLC Monitor — 一键启动脚本 (PowerShell)
# 自动解决 Node 版本、端口占用、原生模块编译问题
# 用法: 右键 → 使用 PowerShell 运行 或 .\start.ps1

$ErrorActionPreference = "Stop"
$ROOT = Split-Path -Parent $MyInvocation.MyCommand.Path
Set-Location $ROOT

Write-Host "========================================" -ForegroundColor Cyan
Write-Host "  WebScada PLC Monitor — 启动器" -ForegroundColor Cyan

# ─── 检测主线还是 worktree ────────────────────────────
$gitDir = git rev-parse --git-dir 2>$null
$worktreeType = "master"
if ($gitDir -match 'worktrees') {
    $worktreeType = "worktree"
    $branch = git rev-parse --abbrev-ref HEAD 2>$null
}
if ($PWD.Path -match '\.orca\\worktrees') {
    $worktreeType = "worktree"
}
if ($worktreeType -eq "worktree") {
    if ($branch) {
        Write-Host "  工作区: Worktree [分支: $branch]" -ForegroundColor Yellow
    } else {
        Write-Host "  工作区: Worktree" -ForegroundColor Yellow
    }
} else {
    Write-Host "  工作区: 主线" -ForegroundColor Green
}
Write-Host "========================================" -ForegroundColor Cyan
Write-Host ""

# ─── 1. 找 Node.js v22+ ────────────────────────────────
Write-Host "[1/4] 检测 Node.js 版本..." -ForegroundColor Yellow
$nodeCmd = "node"

try {
    $ver = node --version
    $major = [int]($ver -replace 'v', '' -split '\.')[0]
    if ($major -ge 22) {
        Write-Host "  ✓ 系统 Node.js v$major.*, 版本满足" -ForegroundColor Green
    } else {
        throw "版本过低"
    }
} catch {
    # 尝试 nvs 的 Node 22
    $nvsNode = "$env:LOCALAPPDATA\nvs\node\22.22.3\x64\node.exe"
    if (Test-Path $nvsNode) {
        Write-Host "  ✓ 使用 nvs Node 22.22.3" -ForegroundColor Green
        $nodeCmd = $nvsNode
    } else {
        # 尝试从 nvs 自动切换
        $nvsCmd = Get-Command "nvs.cmd" -ErrorAction SilentlyContinue
        if ($nvsCmd) {
            Write-Host "  ~ 尝试 nvs use 22..." -ForegroundColor Yellow
            & $nvsCmd.Source use 22 2>$null
            $nodeCmd = "node"
            $ver = & node --version
            Write-Host "  ✓ 切换到 Node $ver" -ForegroundColor Green
        } else {
            Write-Host "  ✗ 未找到 Node.js v22+，请安装: https://nodejs.org/" -ForegroundColor Red
            Read-Host "按回车退出"
            exit 1
        }
    }
}
Write-Host ""

# ─── 2. 设置 pnpm ──────────────────────────────────────
# 直接用系统 PATH 里的 pnpm
try {
    $null = Get-Command pnpm -ErrorAction Stop
} catch {
    Write-Host "  ✗ 未找到 pnpm，请运行: npm install -g pnpm" -ForegroundColor Red
    Read-Host "按回车退出"
    exit 1
}
Write-Host "  Node: $nodeCmd" -ForegroundColor Gray
Write-Host "  pnpm: pnpm" -ForegroundColor Gray
Write-Host ""

# ─── 3. 清理端口文件 ──────────────────────────────────
Write-Host "[2/4] 清理端口文件..." -ForegroundColor Yellow
# 删除旧的端口文件，服务端启动时会根据路径 hash 重新分配
$portFile = Join-Path $ROOT ".port.json"
if (Test-Path $portFile) { Remove-Item $portFile -Force }
Write-Host "  ✓ 端口文件已清理" -ForegroundColor Green
Write-Host ""

# ─── 4. 检查 better-sqlite3 是否需要重新编译 ────────────
Write-Host "[3/4] 检查原生模块..." -ForegroundColor Yellow
$betterModule = Get-ChildItem -Path "$ROOT\node_modules\.pnpm" -Filter "better-sqlite3@*" -Directory |
    ForEach-Object { Join-Path $_.FullName "node_modules\better-sqlite3\build\Release\better_sqlite3.node" } |
    Where-Object { Test-Path $_ } |
    Select-Object -First 1

if ($betterModule) {
    # 尝试加载检查版本匹配
    $testJs = "try{require('$($betterModule -replace '\\', '/')');process.exit(0)}catch(e){process.exit(1)}"
    & $nodeCmd -e $testJs 2>$null
    if ($LASTEXITCODE -eq 0) {
        Write-Host "  ✓ better-sqlite3 模块正常" -ForegroundColor Green
    } else {
        Write-Host "  ~ better-sqlite3 需要重新编译..." -ForegroundColor Yellow
        & pnpm rebuild better-sqlite3
        if ($LASTEXITCODE -ne 0) {
            Write-Host "  ⚠ 重新编译失败，尝试安装依赖..." -ForegroundColor DarkYellow
            & pnpm install
        }
        Write-Host "  ✓ 编译完成" -ForegroundColor Green
    }
} else {
    Write-Host "  ~ 未检测到原生模块，运行 pnpm install..." -ForegroundColor Yellow
    & pnpm install
}
Write-Host ""

# 先分配端口（写入 .port.json）
Write-Host "  ~ 预分配 API 端口..." -ForegroundColor Yellow
& pnpm tsx server/resolve-port.ts
$resolvedPort = (Get-Content $portFile -ErrorAction SilentlyContinue | ConvertFrom-Json).port
if (-not $resolvedPort) { $resolvedPort = 3001 }

# ─── 5. 启动开发服务器 ──────────────────────────────────
Write-Host "[4/4] 启动开发服务器..." -ForegroundColor Yellow
Write-Host ""
Write-Host "  前端: http://localhost:5173" -ForegroundColor Cyan
Write-Host "  API:  http://localhost:$resolvedPort" -ForegroundColor Cyan
Write-Host ""
Write-Host "  按 Ctrl+C 停止所有服务" -ForegroundColor Gray
Write-Host "========================================" -ForegroundColor Cyan
Write-Host ""

& pnpm dev
if (-not $?) {
    Write-Host "启动失败，按回车退出" -ForegroundColor Red
    Read-Host
}
