@echo off
REM T1 da fase 16 (perf test suite): roda a suite INTEIRA sem filtro (inclui Category=Scenario)
REM e mede tempo por classe + uso de CPU. Isso demora HORAS de proposito - e o baseline real.
REM Rode e va fazer outra coisa; quando terminar, me manda os 2 arquivos gerados.

cd /d "C:\Users\Alan-\Desktop\Projetos\Pessoal\LivingWorld"

if exist "TestResults" rmdir /S /Q "TestResults"
if exist "cpu_baseline.csv" del /F /Q "cpu_baseline.csv"

echo Iniciando amostragem de CPU (typeperf, a cada 5s)...
start "cpu-sampler" /MIN typeperf "\Processor(_Total)\%% Processor Time" -si 5 -o "cpu_baseline.csv"

echo.
echo Inicio: %DATE% %TIME%
echo Rodando dotnet test LivingWorld.sln SEM filtro (100 anos, cenarios, tudo)...
echo Isso pode levar varias horas. Nao feche esta janela.
echo.

dotnet test LivingWorld.sln --nologo --results-directory TestResults --logger "trx;LogFileName=baseline.trx"

echo.
echo Fim: %DATE% %TIME%

echo Parando amostragem de CPU...
taskkill /F /IM typeperf.exe >nul 2>&1

echo.
echo ==========================================================
echo Pronto. Me envie estes 2 arquivos:
echo   TestResults\baseline.trx
echo   cpu_baseline.csv
echo ==========================================================
pause
