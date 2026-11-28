using LivingWorld.Domain.Cities.Buildings;
using LivingWorld.Domain.Objects;
using LivingWorld.Domain.Population;
using LivingWorld.Domain.Shared;
using LivingWorld.Simulation.Core;

namespace LivingWorld.Simulation.Objects;

/// <summary>Uma necessidade do catálogo como a pessoa a sente agora: o que o cliente desenha (barra, cor, se pede atenção).</summary>
public sealed record NeedReading(string Id, string Name, int Value, bool Urgent, string? Color);

/// <summary>A dica da necessidade mais urgente: o alerta do cenário ("Você está com a bexiga cheia") e o que a atende em casa (objeto e uso).</summary>
public sealed record NeedHint(string Need, string Alert, string? Object, string? Use);

/// <summary>#255: as necessidades do catálogo do cenário (higiene, diversão, bexiga...) como mecanismo genérico do motor: o mesmo molde preguiçoso das de
/// sempre (<see cref="LazyNeed"/>, nada escrito por tick), a mesma regra para NPC e jogador, e o que acontece quando estão baixas (<see cref="NeedEffect"/>).
/// O código não conhece nenhuma delas pelo nome: id, taxa, limites e efeitos são dado do cenário (<see cref="HomeLifeLoader"/>).</summary>
public static class HomeLife
{
    public static NeedCatalog? Catalog(WorldState world) => world.ObjectCatalog?.Needs;

    /// <summary>Dá à pessoa as necessidades do catálogo, no nível de partida do cenário, a partir de agora. Idempotente; sem catálogo, não faz nada.</summary>
    public static void Seed(WorldState world, Npc npc)
    {
        if (Catalog(world) is not { } catalog) return;
        long ms = world.CurrentDate.TotalMilliseconds;
        foreach (var def in catalog.Needs)
            if (npc.ExtraNeeds is null || !npc.ExtraNeeds.ContainsKey(def.Id))
                npc.SetExtraNeed(def.Id, new LazyNeed(def.Start, ms, def.DecayPerHour));
    }

    /// <summary>O nível (0–100) de uma necessidade, de sempre ou do catálogo, em <paramref name="ms"/>; nulo se o id não existe.</summary>
    public static double? Value(WorldState world, Npc npc, string id, long ms)
    {
        if (npc.BuiltinNeedAtMs(id, ms) is { } builtin) return builtin;
        if (Catalog(world)?.Find(id) is not { } def) return null;
        return npc.ExtraNeeds is { } extra && extra.TryGetValue(id, out var need) ? need.ValueAtMs(ms) : def.Start;
    }

    /// <summary>Soma <paramref name="delta"/> (pode ser negativo) ao nível da necessidade em <paramref name="ms"/>, limitado a 0–100.</summary>
    public static void Add(WorldState world, Npc npc, string id, double delta, long ms)
    {
        if (Value(world, npc, id, ms) is not { } now) return;
        double next = Math.Clamp(now + delta, 0, 100);
        if (NeedIds.IsBuiltin(id)) { npc.SetBuiltinNeedAtMs(id, next, ms); return; }
        var def = Catalog(world)!.Find(id)!;
        var need = npc.ExtraNeeds is { } extra && extra.TryGetValue(id, out var own) ? own : new LazyNeed(def.Start, ms, def.DecayPerHour);
        npc.SetExtraNeed(id, need.WithValueAtMs(next, ms));
    }

    /// <summary>As necessidades do catálogo da pessoa agora (ordem do cenário); vazio sem catálogo.</summary>
    public static IReadOnlyList<NeedReading> Readings(WorldState world, Npc npc, long ms)
    {
        if (Catalog(world) is not { } catalog) return [];
        var list = new List<NeedReading>(catalog.Needs.Count);
        foreach (var def in catalog.Needs)
        {
            double v = Value(world, npc, def.Id, ms) ?? def.Start;
            list.Add(new NeedReading(def.Id, def.Name, (int)Math.Round(v, MidpointRounding.AwayFromZero), v <= def.UrgentBelow, def.Color));
        }

        return list;
    }

    /// <summary>A necessidade do catálogo mais urgente (a mais abaixo do próprio limite) se já passou dele; nula se nenhuma pede atenção.</summary>
    public static NeedDef? MostUrgent(WorldState world, Npc npc, long ms)
    {
        if (Catalog(world) is not { } catalog) return null;
        NeedDef? worst = null;
        double gap = 0;
        foreach (var def in catalog.Needs)
        {
            double over = def.UrgentBelow - (Value(world, npc, def.Id, ms) ?? def.Start);
            if (over > gap) { gap = over; worst = def; }
        }

        return worst;
    }

    /// <summary>"Você está com a bexiga cheia → Vaso": o alerta do cenário e o primeiro objeto da casa que atende à necessidade.</summary>
    public static NeedHint? Hint(WorldState world, Npc npc, long ms)
    {
        if (MostUrgent(world, npc, ms) is not { } def || world.ObjectCatalog is not { } catalog) return null;
        string? objectName = null, useName = null;
        if (HomeOf(world, npc) is { } building)
        {
            foreach (var id in catalog.FurnishingFor(building.BuildingTypeId))
                if (catalog.Find(id) is { Uses: { } uses } descriptor && uses.FirstOrDefault(u => !u.Hidden && u.Effects.Any(e => e.Need == def.Id && e.PerHour > 0)) is { } use)
                {
                    objectName = descriptor.Name ?? descriptor.Id;
                    useName = use.Name;
                    break;
                }
        }

        return new NeedHint(def.Id, def.Alert ?? def.Name, objectName, useName);
    }

    /// <summary>A casa da pessoa (prédio da moradia), ou nulo.</summary>
    public static Building? HomeOf(WorldState world, Npc npc) =>
        npc.Household is { } h && world.FindHousehold(h) is { Dwelling: { } dwelling } ? world.FindBuilding(dwelling) : null;

    /// <summary>A cada hora de mundo (só para quem tem necessidades do catálogo): o que a necessidade baixa faz — saúde, solidão. Sem estado novo:
    /// a conta sai do valor preguiçoso e do relógio. O peso nos laços (<c>bond</c>) é aplicado a cada conversa (<see cref="BondPenalty"/>).</summary>
    public static void Hourly(WorldState world, Npc npc, long nowMs)
    {
        if (npc.ExtraNeeds is null || Catalog(world) is not { } catalog) return;
        foreach (var def in catalog.Needs)
        {
            if (def.Effects is not { } effects) continue;
            double v = Value(world, npc, def.Id, nowMs) ?? def.Start;
            foreach (var effect in effects)
            {
                if (v >= effect.Below) continue;
                if (effect.Target == NeedEffectTargets.Health)
                    npc.SetHealth(npc.Health + (int)Math.Round(effect.PerHour, MidpointRounding.AwayFromZero));
                else if (effect.Target == NeedEffectTargets.Social)
                    Add(world, npc, "social", effect.PerHour, nowMs);
            }
        }
    }

    /// <summary>Quando a pessoa precisa redecidir por causa do catálogo: o fim do uso de objeto em curso, ou o instante em que a necessidade que ainda decai cruza o
    /// limite de urgência (se já cruzou, uma hora depois: o NPC que não achou como atendê-la tenta de novo sem acordar a cada tick). <c>long.MaxValue</c>: nada.</summary>
    public static long NextWakeMs(WorldState world, Npc npc, long nowMs)
    {
        long next = long.MaxValue;
        if (npc.BodyTask is { Action: ObjectUsage.Action or SocialInteractions.Action } task && task.UntilMs > nowMs) next = task.UntilMs;
        if (npc.BodyTask is null) next = Math.Min(next, SocialInteractions.NextCheckMs(world, npc, nowMs)); // #259: de tempos em tempos quem está só vê se quer companhia
        if (npc.ExtraNeeds is null || Catalog(world) is not { } catalog) return next;
        foreach (var def in catalog.Needs)
        {
            if (!npc.ExtraNeeds.TryGetValue(def.Id, out var need) || need.DecayRatePerHour <= 0) continue;
            double v = need.ValueAtMs(nowMs);
            long at = v <= def.UrgentBelow ? nowMs + WorldMillis.PerHour : nowMs + Math.Max(1L, (long)Math.Ceiling((v - def.UrgentBelow) / need.DecayRatePerHour * WorldMillis.PerHour));
            if (at < next) next = at;
        }

        return next;
    }

    /// <summary>O quanto o estado da pessoa pesa no laço com quem conversa com ela (soma dos <c>bond</c> das necessidades abaixo do limite; 0 = nada).
    /// Negativo: a conversa piora o laço.</summary>
    public static double BondPenalty(WorldState world, Npc npc, long nowMs)
    {
        if (npc.ExtraNeeds is null || Catalog(world) is not { } catalog) return 0;
        double sum = 0;
        foreach (var def in catalog.Needs)
        {
            if (def.Effects is not { } effects) continue;
            double v = Value(world, npc, def.Id, nowMs) ?? def.Start;
            foreach (var effect in effects)
                if (effect.Target == NeedEffectTargets.Bond && v < effect.Below) sum += effect.PerHour;
        }

        return sum;
    }
}
