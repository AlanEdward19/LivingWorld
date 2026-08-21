@echo off
REM Mede o teste que era o gargalo (Ten_k_population_ten_years_within_perf_budget)
REM APOS os fixes: (1) BehaviorDecisionSystem.cs - cache de populacao por cidade, 1x por tick;
REM (2) EventScheduler.cs - Schedule fazia Add+Sort completo do bucket a cada chamada (O(k log k),
REM k = eventos no mesmo tick) so pra alimentar um indice que nunca era lido - trocado por insercao
REM ordenada via busca binaria (O(k)). Essa foi a causa real (~95% do custo por tick).
REM Antes de qualquer fix: 7h45min12s. So com o fix (1): 6h43min17s (~13% mais rapido).
REM Amostra de 500 ticks com os 2 fixes: 98.7ms/tick (vs 793ms/tick original) - ~8x mais rapido.
REM Roda ate terminar sozinho - nao depende de sessao minha ficar viva.

setlocal
cd /d "C:\Users\Alan-\Desktop\Projetos\Pessoal\LivingWorld"

if exist "TestResults\hotpath_fixed.trx" del /F /Q "TestResults\hotpath_fixed.trx"

echo Inicio: %DATE% %TIME% > hotpath_fixed_timing.txt
echo Inicio: %DATE% %TIME%

dotnet test LivingWorld.sln --nologo --results-directory TestResults --logger "trx;LogFileName=hotpath_fixed.trx" --filter "FullyQualifiedName~LongRunScaleTests.Ten_k_population_ten_years_within_perf_budget"

echo Fim: %DATE% %TIME% >> hotpath_fixed_timing.txt
echo Fim: %DATE% %TIME%

echo.
echo ==========================================================
echo Pronto. Me envie estes 2 arquivos:
echo   hotpath_fixed_timing.txt
echo   TestResults\hotpath_fixed.trx
echo ==========================================================
pause
