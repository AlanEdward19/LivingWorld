@echo off
REM Sobe a API (.NET) numa janela. O cliente web foi aposentado (ADR-0022: Godot).
setlocal
cd /d "%~dp0"

if not exist "worlds" mkdir "worlds"
set "ConnectionStrings__World=Data Source=%CD%\worlds\livingworld.db"
set "TICK_LOOP_ENABLED=true"

echo [api] abrindo janela...
REM Bugfix real (usuario, 2026-08-13): sem TICK_LOOP_ENABLED=true o relogio da simulacao nunca
REM avanca sozinho fora dos testes (Fase 15.1, T3 desabilita por padrao pra nenhuma
REM WebApplicationFactory de teste ganhar um mundo mudando sozinho embaixo dela) - Play/Resume
REM no cliente nao tinha efeito nenhum, NPCs ficavam sempre parados no lugar onde nasceram.
start "LivingWorld API" cmd /k "dotnet run --project src\LivingWorld.Api --urls http://localhost:5289"

echo.
echo API:  http://localhost:5289
echo Feche a janela aberta para parar.
endlocal
