using LivingWorld.Domain.Behavior;
using LivingWorld.Domain.Health;
using LivingWorld.Domain.Population;
using LivingWorld.Domain.Shared;
using LivingWorld.Simulation.Core;

namespace LivingWorld.Simulation.Health;

/// <summary>O que a doença faz com a rotina (AD-137). Quem está de cama não trabalha, não sai para conversar e fica deitado (come
/// e compra se precisar); quem cuida dele perde parte dos dias de trabalho. O caso leve anda e trabalha, mais fraco (a saúde cai
/// e <c>BodyMechanic</c> tira capacidade). Consultas por busca binária nas infecções: quem não está doente custa O(log doentes).</summary>
public static class SickBehavior
{
    /// <summary>O NPC está de cama agora (doença com sintoma, caso grave).</summary>
    public static bool IsBedridden(WorldState world, NpcId npc)
    {
        var infections = world.Infections;
        if (infections.Count == 0) return false;
        for (int i = world.FirstInfectionIndex(npc); i >= 0 && i < infections.Count && infections[i].Npc == npc; i++)
            if (infections[i] is { Symptomatic: true, Bedridden: true }) return true;
        return false;
    }

    /// <summary>A escolha da hora passa pela doença como passa pelo desenvolvimento: de cama, tudo que não é comer, dormir,
    /// comprar comida ou chorar (o bebê pede de comer) vira ficar deitado; quem cuida de um doente deixa o trabalho em parte dos dias e fica por perto.</summary>
    public static ActionType Constrain(WorldState world, Npc npc, ActionType action, long now)
    {
        if (world.Infections.Count == 0 || !world.DiseaseRules.Enabled) return action;
        if (IsBedridden(world, npc.Id))
        {
            if (action is ActionType.Eat or ActionType.Sleep or ActionType.Buy or ActionType.Cry) return action;
            // fome ou sede urgente vencem a cama (e a fuga que a percepção sugere): o doente come do que tem em casa
            double urgent = 100 - world.NeedsRules.UrgencyThreshold;
            bool hungry = npc.HungerAt(now) <= urgent, thirsty = npc.ThirstAt(now) <= urgent;
            if (!hungry && !thirsty) return ActionType.Sleep;
            return HasAtHome(world, npc, hungry, thirsty) ? ActionType.Eat : ActionType.Buy; // sem nada em casa, vai buscar
        }
        if (action == ActionType.Work && world.IsCaregiver(npc.Id) && IsCareDay(world, npc.Id, now))
            return ActionType.Idle;
        return action;
    }

    private static bool HasAtHome(WorldState world, Npc npc, bool hungry, bool thirsty)
    {
        if (npc.Household is not { } id || world.FindHousehold(id) is not { } home) return false;
        var economy = world.EconomyRules;
        bool food = home.Stock.GetValueOrDefault(new LivingWorld.Domain.Geography.ResourceType(economy.FoodResourceId)) >= 1;
        bool water = home.Stock.GetValueOrDefault(new LivingWorld.Domain.Geography.ResourceType(economy.WaterResourceId)) >= 1;
        return (!hungry || food) && (!thirsty || water);
    }

    /// <summary>Dia em que quem cuida fica em casa: fração <see cref="DiseaseRules.CareWorkShare"/> dos dias, por hash do NPC e do dia.</summary>
    public static bool IsCareDay(WorldState world, NpcId npc, long now)
    {
        long day = now / world.Calendar.HoursPerDay;
        ulong z = unchecked(((ulong)npc.Value * 0x9E3779B97F4A7C15UL) ^ ((ulong)day * 0xBF58476D1CE4E5B9UL) ^ world.Seed);
        z = unchecked((z ^ (z >> 30)) * 0xBF58476D1CE4E5B9UL);
        z = unchecked((z ^ (z >> 27)) * 0x94D049BB133111EBUL);
        z ^= z >> 31;
        return (z >> 11) * (1.0 / (1UL << 53)) < world.DiseaseRules.CareWorkShare;
    }

    /// <summary>Quem da casa cuida do doente: a mãe, se mora junto; senão o cônjuge; senão a primeira mulher da casa com idade para
    /// isso, por id; senão quem tiver idade. Ninguém de cama cuida. Nulo se mora só ou só com crianças.</summary>
    public static NpcId? ChooseCaregiver(WorldState world, DiseaseRules rules, Npc patient, long now)
    {
        if (patient.Household is not { } householdId || world.FindHousehold(householdId) is not { } household) return null;
        long minAgeHours = rules.CaregiverMinAgeYears * world.Calendar.HoursPerYear;
        NpcId? woman = null, anyone = null;
        bool mother = false, spouse = false;
        NpcId? best = null;
        foreach (var id in household.Members)
        {
            if (id == patient.Id || world.FindNpc(id) is not { IsAlive: true } member) continue;
            if (now - member.BirthDate.TotalHours < minAgeHours || IsBedridden(world, id)) continue;
            if (patient.MotherId == id) { best = id; mother = true; }
            else if (!mother && patient.Spouse == id) { best = id; spouse = true; }
            if (member.Sex == Sex.Female) woman ??= id;
            anyone ??= id;
        }

        return mother || spouse ? best : woman ?? anyone;
    }

    /// <summary>Medida do dia (só leitura): dias de trabalho perdidos por quem tem emprego e está de cama, dias de cuidado de quem
    /// tem emprego e trabalhadores com emprego.</summary>
    public static (double Lost, double Care, int Workers) WorkDaysLostToday(WorldState world)
    {
        int workers = 0;
        foreach (var npc in world.AliveNpcIndex.Alive)
            if (npc.Employer is not null) workers++;
        double lost = 0, care = 0;
        long lastBedridden = long.MinValue;
        foreach (var infection in world.Infections)
        {
            if (!infection.Symptomatic) continue;
            if (infection.Bedridden && infection.Npc.Value != lastBedridden
                && world.FindNpc(infection.Npc) is { IsAlive: true, Employer: not null })
            {
                lost += 1.0;
                lastBedridden = infection.Npc.Value;
            }

            if (infection.Bedridden && infection.Caregiver is { } who && world.FindNpc(who) is { IsAlive: true, Employer: not null })
                care += world.DiseaseRules.CareWorkShare;
        }

        return (lost, care, workers);
    }
}
