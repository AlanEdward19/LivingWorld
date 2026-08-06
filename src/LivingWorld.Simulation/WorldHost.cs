namespace LivingWorld.Simulation;

/// <summary>Wrapper mutável de mundo canônico (feature "criar mundo"): antes desta classe,
/// `Program.cs` registrava `WorldState`/`WorldClock` como singletons fixos e nada no processo
/// conseguia trocar de mundo em runtime. `Replace` é o único ponto que troca a instância —
/// endpoints e closures devem sempre ler `Current`/`Clock` no momento do uso, nunca guardar a
/// referência antiga.</summary>
public sealed class WorldHost(WorldState initialWorld, WorldClock initialClock)
{
    public WorldState Current { get; private set; } = initialWorld;
    public WorldClock Clock { get; private set; } = initialClock;

    public void Replace(WorldState world, WorldClock clock)
    {
        Current = world;
        Clock = clock;
    }
}
