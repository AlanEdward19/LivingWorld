using LivingWorld.Domain.Geography;
using LivingWorld.Domain.Population;
using LivingWorld.Domain.Population.Body;
using LivingWorld.Simulation.Core;

namespace LivingWorld.Simulation.Players;

/// <summary>Quem mora com o corpo e o laço dele: <c>spouse</c>, <c>child</c> (dependente), <c>parent</c> ou <c>housemate</c>.</summary>
public sealed record StartMember(long Id, string Name, string Relation);

/// <summary>A casa do corpo (#238): onde fica, quem mora nela e o que a despensa tem (recurso e quantidade).</summary>
public sealed record StartHome(int X, int Y, int Distance, bool Alone, IReadOnlyList<StartMember> Members, IReadOnlyList<InteractionResource> Pantry);

/// <summary>Um lugar para ir: a célula do mundo e a distância em células.</summary>
public sealed record StartPlace(int X, int Y, int Distance, int LocationType = 0);

/// <summary>O ponto de partida do jogador (#238), lido do estado do motor sob pedido (<c>GET /players/{id}/goals?start=true</c>): se o corpo é
/// criança, se tem casa e família, onde é o emprego (ou a vaga mais perto), a mata onde dá para coletar e o que mais aperta agora. Só
/// lê, sem sorteio e sem texto: o cliente escreve o guia a partir destes fatos. <c>Pressing</c>: <c>hunger</c>, <c>thirst</c>, <c>sleep</c>,
/// <c>money</c> ou <c>none</c>.</summary>
public sealed record PlayerStart(
    bool Child, bool Homeless, StartHome? Home, StartPlace? Job, StartPlace? Vacancy, StartPlace? Wild, string Pressing, long Money);

public static class PlayerStartQuery
{
    /// <summary>Raio máximo (células) em que se procura a mata mais perto.</summary>
    public const int WildRadius = 60;

    public static PlayerStart Of(WorldState world, Npc npc)
    {
        var pose = PlayerGround.PoseOf(world, npc);
        int Dist(CellCoord c) => Math.Max(Math.Abs(c.X - pose.X), Math.Abs(c.Y - pose.Y));
        var household = npc.Household is { } h ? world.FindHousehold(h) : null;
        bool adult = world.LifeStageRules.LifeStageOf(npc.AgeYears(world.CurrentDate)) == LifeStage.Adult;

        StartHome? home = null;
        if (household is { IsHomeless: false })
        {
            var members = household.Members.Where(m => m != npc.Id).OrderBy(m => m.Value).Select(world.FindNpc).OfType<Npc>().Where(m => m.IsAlive)
                .Select(m => new StartMember(m.Id.Value, m.Name, RelationOf(world, npc, m))).ToList();
            var pantry = household.Stock.Where(kv => kv.Value > 0).OrderBy(kv => kv.Key.Id)
                .Select(kv => new InteractionResource(kv.Key.Id, kv.Value, 0)).ToList();
            home = new StartHome(household.Location.X, household.Location.Y, Dist(household.Location), members.Count == 0, members, pantry);
        }

        StartPlace? job = null, vacancy = null;
        if (npc.Employer is { } e && world.FindWorkplace(e) is { } w)
            job = new StartPlace(w.Location.X, w.Location.Y, Dist(w.Location), w.LocationType.Id);
        else if (adult)
        {
            var open = world.Workplaces.Where(p => p.City == npc.City && !p.Closed && p.Employees.Count < p.MaxVacancies)
                .OrderBy(p => Dist(p.Location)).ThenBy(p => p.Id.Value).FirstOrDefault();
            if (open is not null) vacancy = new StartPlace(open.Location.X, open.Location.Y, Dist(open.Location), open.LocationType.Id);
        }

        return new PlayerStart(!adult, household is null || household.IsHomeless, home, job, vacancy, NearestWild(world, pose), Pressing(world, npc, adult),
            npc.Wallet.Amount);
    }

    private static string RelationOf(WorldState world, Npc self, Npc other) =>
        self.Spouse == other.Id ? "spouse"
        : PlayerOrders.DependentChild(world, self, other) ? "child"
        : other.Id == self.MotherId || other.Id == self.FatherId ? "parent"
        : "housemate";

    /// <summary>O que mais aperta: a maior falta entre fome, sede e sono (a partir da mesma linha de meta de <see cref="PlayerGoals"/>);
    /// sem nenhuma, carteira zerada de adulto vira <c>money</c>.</summary>
    private static string Pressing(WorldState world, Npc npc, bool adult)
    {
        long now = world.CurrentDate.TotalHours;
        var (kind, deficit) = new[]
        {
            ("hunger", PlayerGoals.Deficit(npc.HungerAt(now))),
            ("thirst", PlayerGoals.Deficit(npc.ThirstAt(now))),
            ("sleep", PlayerGoals.Deficit(npc.SleepAt(now))),
        }.MaxBy(t => t.Item2);
        if (deficit >= PlayerGoals.MinNeedDeficit) return kind;
        return adult && npc.Wallet.Amount == 0 && world.EconomyRules.Enabled ? "money" : "none";
    }

    /// <summary>A mata mais perto (mesma regra da oferta <c>nature</c>/<c>gather</c>), em anéis crescentes; nula sem coleta no mundo.</summary>
    private static StartPlace? NearestWild(WorldState world, CellCoord pose)
    {
        var rules = world.StreetRules;
        if (!world.StreetsOn || rules.HouseGatherHours <= 0 || rules.HouseMaterialUnits <= 0) return null;
        var resource = new ResourceType(rules.HouseMaterialResourceId);
        for (int r = 1; r <= WildRadius; r++)
            for (int dy = -r; dy <= r; dy++)
                for (int dx = -r; dx <= r; dx++)
                {
                    if (Math.Max(Math.Abs(dx), Math.Abs(dy)) != r) continue;
                    var cell = new CellCoord(pose.X + dx, pose.Y + dy);
                    if (world.Map.TryGetCell(cell, out var found) && PlayerNature.IsWild(world, found, resource))
                        return new StartPlace(cell.X, cell.Y, r);
                }
        return null;
    }
}
