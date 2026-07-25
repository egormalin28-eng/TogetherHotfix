@echo off
chcp 65001 >nul
setlocal enableextensions
cd /d "%~dp0"
echo ============================================
echo    Сборка заплатки CMS21-Together-Hotfix
echo ============================================
echo Папка: %~dp0
echo.
set "DOTNET_CLI_TELEMETRY_OPTOUT=1"
set "DOTNET_NOLOGO=1"

rem --- ПРОВЕРКА: архив распакован? ---
if not exist "%~dp0CMS21-Together-Hotfix.csproj" goto NOEXTRACT
if not exist "%~dp0Libs\CMS21-Together.dll" goto NOEXTRACT

rem --- 1) есть ли dotnet в системе? ---
where dotnet >nul 2>nul
if not errorlevel 1 (
  echo .NET SDK уже есть в системе.
  set "DOTNET=dotnet"
  goto BUILD
)

rem --- 2) может, уже скачан локально раньше? ---
if exist "%~dp0.dotnet\dotnet.exe" (
  echo Найден локальный .NET SDK в папке .dotnet.
  set "DOTNET=%~dp0.dotnet\dotnet.exe"
  goto BUILD
)

rem --- 3) скачиваем SDK локально (без админа) ---
echo .NET SDK не найден. Скачиваю его локально (нужен интернет)...
echo Это один раз и может занять несколько минут, подожди.
echo.
powershell -NoProfile -ExecutionPolicy Bypass -Command "[Net.ServicePointManager]::SecurityProtocol=[Net.SecurityProtocolType]::Tls12; Invoke-WebRequest -UseBasicParsing -Uri https://dot.net/v1/dotnet-install.ps1 -OutFile dotnet-install.ps1"
if not exist "dotnet-install.ps1" goto NOINTERNET
powershell -NoProfile -ExecutionPolicy Bypass -File dotnet-install.ps1 -Channel 8.0 -InstallDir "%~dp0.dotnet"
if not exist "%~dp0.dotnet\dotnet.exe" goto NOINTERNET
set "DOTNET=%~dp0.dotnet\dotnet.exe"
echo.
echo .NET SDK скачан локально. Продолжаю.

:BUILD
echo.
echo Собираю... подожди 1-3 минуты.
echo.
set "DOTNET_ROOT=%~dp0.dotnet"
"%DOTNET%" build "%~dp0CMS21-Together-Hotfix.csproj" -c Release -o "%~dp0out"
echo.
if exist "%~dp0out\CMS21-Together-Hotfix.dll" goto OK
goto FAIL

:NOEXTRACT
echo [АРХИВ НЕ РАСПАКОВАН]
echo Рядом с этим файлом нет проекта (CMS21-Together-Hotfix.csproj) или папки Libs.
echo.
echo Ты, скорее всего, запустил батник ПРЯМО ИЗ АРХИВА (zip).
echo Надо СНАЧАЛА распаковать:
echo   1. Правая кнопка по TogetherHotfix.zip -^> "Извлечь всё..." (Extract All).
echo   2. Открой распакованную папку TogetherHotfix.
echo   3. Запусти BUILD-DLL.bat уже ОТТУДА (двойной клик).
goto END

:NOINTERNET
echo [ОШИБКА ЗАГРУЗКИ]
echo Не получилось скачать .NET SDK (нет интернета или блокировка).
echo Проверь интернет и запусти файл снова.
goto END

:OK
echo ============================================
echo    ГОТОВО! Файл лежит здесь:
echo    %~dp0out\CMS21-Together-Hotfix.dll
echo.
echo Скопируй этот файл в папку Mods игры (РЯДОМ с CMS21-Together.dll
echo и TogetherFixes.dll, НИЧЕГО не удаляя). И так у ОБОИХ игроков.
echo ============================================
goto END

:FAIL
echo [ОШИБКА] Сборка не удалась.
echo Сфотографируй текст выше и пришли мне — помогу разобраться.
goto END

:END
echo.
echo Окно не закроется само. Нажми любую клавишу, чтобы выйти.
pause >nul
