@echo off
REM trioop PLC Monitor — 一键启动
REM 自动解决 Node 版本、端口占用、原生模块编译问题
REM 用法: pnpm launch  或  双击 start.cmd

setlocal enabledelayedexpansion

set "ROOT=%~dp0"
cd /d "%ROOT%"
echo ========================================
echo   Trioop PLC Monitor
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
REM 直接用系统 PATH 里的 pnpm（不用 --version，避免触发 pnpm 网络验证）
where pnpm >nul 2>&1
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

REM ─── 3. 清理端口 ───────────────────────────────────────
echo [2/4] Cleaning ports...
for %%p in (5173 5174 5175 5176 5177 3001) do (
    for /f "tokens=5" %%a in ('netstat -ano ^| findstr ":%%p " 2^>nul') do (
        taskkill /F /PID %%a >nul 2>&1
    )
)
echo   [OK] Ports cleaned
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
    "%NODE_CMD%" "%PNPM_SCRIPT%" install
)
echo.

REM ─── 5. 启动 ───────────────────────────────────────────
echo [4/4] Starting dev server...
echo.
echo   Frontend: http://localhost:5173
echo   API:      http://localhost:3001
echo.
echo   Press Ctrl+C to stop
echo ========================================
echo.

"%NODE_CMD%" "%PNPM_SCRIPT%" dev
if !errorlevel! neq 0 (
    echo.
    echo [FAIL] Server exited with code !errorlevel!
    pause
)
