@echo off
REM Pre-push hook to validate branch naming conventions (Windows version)
REM This hook prevents pushing branches that don't follow the naming pattern

setlocal enabledelayedexpansion

REM Get the current branch name
for /f "delims=" %%i in ('git symbolic-ref --short HEAD2^>nul') do set "current_branch=%%i"

REM Check if we got a branch name
if "%current_branch%"=="" (
 echo Warning: Could not determine current branch
 exit /b0
)

REM Initialize valid flag
set "valid=0"

REM Helper to test a regex against the branch name using findstr
:check
necho %current_branch% | findstr /r "%~1" >nul2>&1
if %ERRORLEVEL% equ0 set "valid=1"
ngoto :eof

REM Check allowed patterns
call :check "^main$"
call :check "^develop$"
call :check "^dev$"
call :check "^development$"
call :check "^feature/.*"
call :check "^features/.*"
call :check "^hotfix/.*"
call :check "^hotfixes/.*"

if "%valid%"=="0" (
 echo.
 echo ================================================================
 echo ERROR: INVALID BRANCH NAME
 echo ================================================================
 echo.
 echo Branch name: %current_branch%
 echo.
 echo Valid branch naming patterns:
 echo - main
 echo - develop ^(or dev, development^)
 echo - feature/^<description^> ^(e.g., feature/add-telemetry^)
 echo - features/^<description^> ^(e.g., features/widget/radar^)
 echo - hotfix/^<description^> ^(e.g., hotfix/fix-crash^)
 echo - hotfixes/^<description^> ^(e.g., hotfixes/memory-leak^)
 echo.
 echo Examples:
 echo feature/add-radar-widget
 echo feature/ui/improve-dashboard
 echo hotfix/fix-telemetry-crash
 echo.
 echo To rename your branch:
 echo git branch -m %current_branch% feature/^<new-name^>
 echo.
 exit /b1
)

necho [OK] Branch name is valid: %current_branch%
exit /b0
