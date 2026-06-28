@echo off
REM trioop PLC Monitor — 一键启动
REM 自动解决 Node 版本、端口占用、原生模块编译问题
REM 用法: pnpm launch  或  双击 start.cmd

setlocal enabledelayedexpansion

set "ROOT=%~dp0"
cd /d "%ROOT%"

REM ─── 检测主线还是 worktree ────────────────────────────
set "WORKTREE_NAME="
for /f %%a in ('git rev-parse --show-toplevel 2^>nul') do set "REPO_ROOT=%%a"
if defined REPO_ROOT (
    if exist "%REPO_ROOT%\.git\." (
        REM .git 是目录 → 主线
        set "WORKTREE_TYPE=master"
    ) else if exist "%REPO_ROOT%\.git" (
        REM .git 是文件 → worktree
        for /f %%a in ('git rev-parse --abbrev-ref HEAD 2^>nul') do set "WORKTREE_NAME=%%a"
        set "WORKTREE_TYPE=worktree"
    )
) else (
    REM 不在 git 仓库中
)
echo "%ROOT%" | findstr /i "\.orca\\worktrees" >nul 2>&1
if !errorlevel! equ 0 set "WORKTREE_TYPE=worktree"

echo ========================================
echo   Trioop PLC Monitor
if /i "!WORKTREE_TYPE!"=="worktree" (
    if defined WORKTREE_NAME (
        echo   工作区: Worktree [分支: !WORKTREE_NAME!]
    ) else (
        echo   工作区: Worktree
    )
) else (
    echo   工作区: 主线
)
echo ========================================
echo.

REM 关闭 echo 中的 ! 扩展干扰
set "EXCL="

REM ─── 1. 找 Node.js v22+ ────────────────────────────────
echo [1/4] Checking Node.js version...
set NODE_CMD=node
node --version >nul 2>&1
if !errorlevel! equ 0 (
    for /f "tokens=1-3 delims=v." %%a in ('node --version') do set NODE_MAJOR=%%b
    if !NODE_MAJOR! geq 22 (
        echo   [OK] System Node.js v!NODE_MAJOR! - OK
        goto :NODE_OK
    )
)

REM 尝试 nvs
set NVS_NODE=%LOCALAPPDATA%\nvs\node\22.22.3\x64\node.exe
if exist "%NVS_NODE%" (
    echo   [OK] Using nvs Node 22.22.3
    set "NODE_CMD=%NVS_NODE%"
    goto :NODE_OK
)

echo   [FAIL] Node.js v22+ not found. Install from https://nodejs.org/
echo   Current:
node --version 2>nul
echo.
pause
exit /b 1

:NODE_OK
echo.

REM ─── 2. 找 pnpm ────────────────────────────────────────
REM 直接用系统 PATH 里的 pnpm
pnpm --version >nul 2>&1
if !errorlevel! neq 0 (
    echo   [FAIL] pnpm not found. Run: npm install -g pnpm
    pause
    exit /b 1
)
set "PNPM_CMD=pnpm"
REM 把 nvs Node 目录加到 PATH 最前面，确保子进程也用正确版本
for %%i in ("!NODE_CMD!") do set "NODE_DIR=%%~dpi"
set "PATH=!NODE_DIR!;!PATH!"
echo   Node:  !NODE_CMD!
echo   pnpm:  pnpm (!PNPM_CMD!)
echo.

REM ─── 3. 清理端口文件 ───────────────────────────────────
echo [2/4] Cleaning port file...
REM 删除旧的端口文件，服务端启动时会根据路径 hash 重新分配
if exist "%ROOT%.port.json" del "%ROOT%.port.json"
echo   [OK] Port file cleaned
echo.

REM ─── 4. 检查 better-sqlite3 ────────────────────────────
echo [3/4] Checking native modules...
for /d %%d in ("%ROOT%node_modules\.pnpm\better-sqlite3@*") do (
    if exist "%%d\node_modules\better-sqlite3\build\Release\better_sqlite3.node" (
        set "BS=%%d\node_modules\better-sqlite3\build\Release\better_sqlite3.node"
    )
)
if defined BS (
    REM 简单检查：删除旧模块让 pnpm 下次自动重建
    echo   [OK] Native modules found
) else (
    echo   [WARN] Native modules not found, running install...
    !PNPM_CMD! install
)
echo.

REM ─── 5. 启动 ───────────────────────────────────────────
echo [4/4] Starting dev server...
REM 删除旧端口文件，服务端启动时自动分配
if exist "%ROOT%.port.json" del "%ROOT%.port.json"

REM 先分配端口，写入 .port.json，再启动 dev
echo   Reserving API port...
!PNPM_CMD! tsx server/resolve-port.ts
set "PORT_RESULT="
if exist "%ROOT%.port.json" (
    for /f "tokens=2 delims=:}" %%a in ('type "%ROOT%.port.json"') do set "PORT_RESULT=%%a"
)
if defined PORT_RESULT (
    set "PORT_RESULT=!PORT_RESULT: =!"
    set "PORT_RESULT=!PORT_RESULT:"=!"
) else (
    set "PORT_RESULT=3001"
)
echo.
echo   Frontend: http://localhost:5173
echo   API:      http://localhost:!PORT_RESULT!
echo.
echo   Press Ctrl+C to stop
echo ========================================
echo.

!PNPM_CMD! dev
if !errorlevel! neq 0 (
    echo.
    echo [FAIL] Server exited with code !errorlevel!
    pause
)
