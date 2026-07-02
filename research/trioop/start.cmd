@echo off
REM trioop PLC Monitor - one-click start
REM Usage: pnpm launch  or  double-click start.cmd

setlocal enabledelayedexpansion

set "ROOT=%~dp0"
cd /d "%ROOT%"
echo ========================================
echo   Trioop PLC Monitor
echo ========================================
echo.

REM --- 1. Find Node.js v22+ ---
echo [1/4] Checking Node.js version...
set NODE_CMD=node
node --version >nul 2>&1
if "!errorlevel!"=="0" (
    for /f "tokens=1-3 delims=v." %%a in ('node --version') do set NODE_MAJOR=%%b
    if !NODE_MAJOR! geq 22 (
        echo   [OK] System Node.js v!NODE_MAJOR! - OK
        goto :NODE_OK
    )
)

set NVS_NODE=%LOCALAPPDATA%\nvs\node\22.22.3\x64\node.exe
if exist "%NVS_NODE%" (
    echo   [OK] Using nvs Node 22.22.3
    set "NODE_CMD=%NVS_NODE%"
    goto :NODE_OK
)

where node >nul 2>&1
if "!errorlevel!"=="0" (
    for /f "tokens=1-3 delims=v." %%a in ('node --version') do set NODE_MAJOR=%%b
    echo   [OK] System Node.js v!NODE_MAJOR! - OK
    goto :NODE_OK
)

echo   [FAIL] Node.js not found.
pause
exit /b 1

:NODE_OK
echo.

REM --- 2. Find pnpm ---
where pnpm >nul 2>&1
if "!errorlevel!"=="1" (
    echo   [FAIL] pnpm not found. Run: npm install -g pnpm
    pause
    exit /b 1
)

for %%i in ("!NODE_CMD!") do set "NODE_DIR=%%~dpi"
set "PATH=!NODE_DIR!;!PATH!"

echo   Node:  !NODE_CMD!
echo   pnpm:  pnpm
echo.

REM --- 3. Clean ports (一次性 netstat 查所有端口，后台杀不阻塞) ---
echo [2/4] Cleaning ports...
for /f "tokens=5" %%a in ('netstat -ano ^| findstr ":5173 :5174 :5175 :5176 :5177 :3001 " 2^>nul') do (
    start /b taskkill /F /PID %%a >nul 2>&1
)
echo   [OK] Ports cleaning submitted
echo.

REM --- 4. Check better-sqlite3 ---
echo [3/4] Checking native modules...
for /d %%d in ("%ROOT%node_modules\.pnpm\better-sqlite3@*") do (
    if exist "%%d\node_modules\better-sqlite3\build\Release\better_sqlite3.node" (
        set "BS=%%d\node_modules\better-sqlite3\build\Release\better_sqlite3.node"
    )
)
if defined BS (
    echo   [OK] Native modules found
) else (
    echo   [WARN] Native modules not found, running install...
    pnpm install
)
echo.

REM --- 5. Start ---
echo [4/4] Starting dev server...
echo.
echo   Frontend: http://localhost:5173
echo   API:      http://localhost:3001
echo.
echo   Press Ctrl+C to stop
echo ========================================
echo.

pnpm dev
if "!errorlevel!"=="1" (
    echo.
    echo [FAIL] Server exited with code !errorlevel!
    pause
)