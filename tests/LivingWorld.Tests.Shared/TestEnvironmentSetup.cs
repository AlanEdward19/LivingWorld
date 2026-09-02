using System.Runtime.CompilerServices;

namespace LivingWorld.Tests;

/// <summary>Bugfix real (usuário, 2026-08-15 / 2026-08-25): testes de API flaky em lote
/// quando rodados depois de <c>run.cmd</c> (ou na janela filha do API) — o script exporta
/// <c>TICK_LOOP_ENABLED=true</c> e <c>ConnectionStrings__World=…/worlds/livingworld.db</c>.
/// Em cmd isso persiste na sessão (diferente de bash, onde <c>VAR=x cmd</c> só vale pra um
/// comando). <c>WebApplicationFactory&lt;Program&gt;</c> herda o ambiente: tick loop avança o
/// mundo sozinho entre asserts, e o SQLite em disco é compartilhado entre factories (LoadLatest
/// traz o mundo do jogador; POST /periods colide com ids já gravados; /npcs/0 404). Um
/// <see cref="ModuleInitializerAttribute"/> limpa os dois antes de qualquer teste.</summary>
internal static class TestEnvironmentSetup
{
    [ModuleInitializer]
    public static void ClearAmbientSimulationToggles()
    {
        Environment.SetEnvironmentVariable("TICK_LOOP_ENABLED", null);
        Environment.SetEnvironmentVariable("ConnectionStrings__World", null);
    }
}
