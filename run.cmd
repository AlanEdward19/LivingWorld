@echo off
REM Fase 15: sobe API (.NET) e cliente web (Vite) juntos, cada um em sua janela.
setlocal
cd /d "%~dp0"

if not exist "web\node_modules" (
    echo [web] instalando dependencias...
    call npm --prefix web install
)

echo [api] abrindo janela...
start "LivingWorld API" cmd /k "dotnet run --project src\LivingWorld.Api --urls http://localhost:5289"

echo [web] abrindo janela...
start "LivingWorld Web" cmd /k "npm --prefix web run dev"

echo.
echo API:  http://localhost:5289
echo Web:  http://localhost:5173
echo Feche as duas janelas abertas para parar.
endlocal
