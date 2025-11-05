@echo off
REM Pre-push hook to validate branch naming conventions (Windows version)
REM This hook prevents pushing branches that don't follow the naming pattern

setlocal enabledelayedexpansion

REM Get the current branch name
for /f "delims=" %%i in ('git symbolic-ref --short HEAD 2^>nul') do set current_branch=%%i

REM Check if we got a branch name
if "%current_branch%"=="" (
    echo Warning: Could not determine current branch
    exit /b 0
)

REM Define valid branch patterns and check
set valid=0

REM Check main
echo %current_branch% | findstr /r "^main$" >nul 2>&1
if !errorlevel! equ 0 set valid=1

REM Check develop variants
echo %current_branch% | findstr /r "^develop$" >nul 2>&1
if !errorlevel! equ 0 set valid=1
echo %current_branch% | findstr /r "^dev$" >nul 2>&1
if !errorlevel! equ 0 set valid=1
echo %current_branch% | findstr /r "^development$" >nul 2>&1
if !errorlevel! equ 0 set valid=1

REM Check feature branches
echo %current_branch% | findstr /r "^feature/" >nul 2>&1
if !errorlevel! equ 0 set valid=1
echo %current_branch% | findstr /r "^features/" >nul 2>&1
if !errorlevel! equ 0 set valid=1

REM Check hotfix branches
echo %current_branch% | findstr /r "^hotfix/" >nul 2>&1
if !errorlevel! equ 0 set valid=1
echo %current_branch% | findstr /r "^hotfixes/" >nul 2>&1
if !errorlevel! equ 0 set valid=1

if !valid! equ 0 (
    echo.
    echo ================================================================
    echo   ERROR: INVALID BRANCH NAME
    echo ================================================================
    echo.
    echo Branch name: %current_branch%
    echo.
    echo Valid branch naming patterns:
    echo   - main
    echo   - develop ^(or dev, development^)
    echo   - feature/^<description^>  ^(e.g., feature/add-telemetry^)
    echo   - features/^<description^> ^(e.g., features/widget/radar^)
    echo   - hotfix/^<description^>   ^(e.g., hotfix/fix-crash^)
    echo   - hotfixes/^<description^> ^(e.g., hotfixes/memory-leak^)
    echo.
    echo Examples:
    echo   feature/add-radar-widget
    echo   feature/ui/improve-dashboard
    echo   hotfix/fix-telemetry-crash
    echo.
    echo To rename your branch:
    echo   git branch -m %current_branch% feature/^<new-name^>
    echo.
    exit /b 1
)

echo [OK] Branch name is valid: %current_branch%
exit /b 0
