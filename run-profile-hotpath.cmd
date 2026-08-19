@echo off
REM T2 da fase 16 (perf test suite): perfila so o metodo que domina o baseline
REM (Ten_k_population_ten_years_within_perf_budget = 7h45min de 8h03min da suite inteira).
REM Nao deixa rodar as 7h45 - coleta so 2 minutos de amostragem de CPU (representativo,
REM o custo por tick e estavel ao longo do teste) e mata o processo depois.

setlocal
cd /d "C:\Users\Alan-\Desktop\Projetos\Pessoal\LivingWorld"

where dotnet-trace >nul 2>&1
if errorlevel 1 (
    echo Instalando dotnet-trace...
    dotnet tool install --global dotnet-trace
)
set "DOTNET_TRACE=dotnet-trace"
where dotnet-trace >nul 2>&1
if errorlevel 1 set "DOTNET_TRACE=%USERPROFILE%\.dotnet\tools\dotnet-trace.exe"

echo Subindo o teste (so o metodo lento) em background...
start "hotpath-test" /MIN dotnet test LivingWorld.sln --nologo --filter "FullyQualifiedName~LongRunScaleTests.Ten_k_population_ten_years_within_perf_budget"

echo Aguardando 45s pro testhost subir e o JIT aquecer...
timeout /T 45 /NOBREAK >nul

echo Procurando o processo testhost.exe...
for /f "tokens=2 delims=," %%P in ('powershell -NoProfile -Command "Get-Process testhost -ErrorAction SilentlyContinue | Sort-Object CPU -Descending | Select-Object -First 1 | ConvertTo-Csv -NoTypeInformation | Select-Object -Skip 1"') do set "PID=%%~P"

if "%PID%"=="" (
    echo NAO achei testhost.exe rodando. O teste pode ja ter passado da fase de setup
    echo ou nao subiu a tempo. Feche as janelas manualmente e me avise.
    goto :end
)

echo Testhost PID=%PID%. Coletando 2 minutos de CPU sampling...
"%DOTNET_TRACE%" collect --process-id %PID% --duration 00:00:02:00 --profile cpu-sampling -o longrun_trace.nettrace

echo Convertendo para speedscope (JSON legivel)...
"%DOTNET_TRACE%" convert --format speedscope longrun_trace.nettrace

echo Matando o teste (nao precisamos das 7h45 completarem, ja temos o trace)...
taskkill /F /IM testhost.exe /T >nul 2>&1
taskkill /F /IM dotnet.exe /FI "WINDOWTITLE eq hotpath-test*" /T >nul 2>&1

:end
echo.
echo ==========================================================
echo Pronto. Me envie o arquivo:
echo   longrun_trace.speedscope.json
echo ==========================================================
pause
