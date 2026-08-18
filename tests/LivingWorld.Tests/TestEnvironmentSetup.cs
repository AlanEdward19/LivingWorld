using System.Runtime.CompilerServices;

namespace LivingWorld.Tests;

/// <summary>Bugfix real (usuário, 2026-08-15): testes ficando flaky em lote (RejectedBusy,
/// "Nullable object must have a value", contagens erradas) sempre que rodados depois de
/// <c>run.cmd</c> na MESMA janela do cmd — `set TICK_LOOP_ENABLED=true` do script persiste pro
/// resto da sessão do terminal (diferente de bash, onde `VAR=x comando` só vale pra um comando),
/// e <c>Program.cs</c> lê essa variável de ambiente pra decidir se liga o tick loop em segundo
/// plano. Testes com <c>WebApplicationFactory&lt;Program&gt;</c> herdam essa configuração
/// (environment variables entram na cadeia padrão do host) e o mundo do teste avança sozinho,
/// de forma assíncrona, entre a criação do mundo e os asserts — corrida de fato, não bug de
/// lógica. Um <see cref="ModuleInitializerAttribute"/> roda antes de qualquer teste, garantindo
/// que a suíte nunca dependa (nem seja vítima) do ambiente de quem a invocou.</summary>
internal static class TestEnvironmentSetup
{
    [ModuleInitializer]
    public static void ClearAmbientSimulationToggles()
    {
        Environment.SetEnvironmentVariable("TICK_LOOP_ENABLED", null);
    }
}
