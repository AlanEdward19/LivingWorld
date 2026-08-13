@echo off
REM Fase 15: sobe API (.NET) e cliente web (Vite) juntos, cada um em sua janela.
setlocal
cd /d "%~dp0"

if not exist "web\node_modules" (
    echo [web] instalando dependencias...
    call npm --prefix web install
)

echo [api] abrindo janela...
REM Bugfix real (usuario, 2026-08-13): sem TICK_LOOP_ENABLED=true o relogio da simulacao nunca
REM avanca sozinho fora dos testes (Fase 15.1, T3 desabilita por padrao pra nenhuma
REM WebApplicationFactory de teste ganhar um mundo mudando sozinho embaixo dela) - Play/Resume
REM no cliente nao tinha efeito nenhum, NPCs ficavam sempre parados no lugar onde nasceram.
start "LivingWorld API" cmd /k "set TICK_LOOP_ENABLED=true&& dotnet run --project src\LivingWorld.Api --urls http://localhost:5289"

echo [web] abrindo janela...
start "LivingWorld Web" cmd /k "npm --prefix web run dev"

echo.
echo API:  http://localhost:5289
echo Web:  http://localhost:5173
echo Feche as duas janelas abertas para parar.
endlocal
