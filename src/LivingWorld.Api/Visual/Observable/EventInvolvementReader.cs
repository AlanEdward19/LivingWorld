using LivingWorld.Domain.Geography;
using LivingWorld.Domain.History;
using LivingWorld.Domain.Population.Body;
using LivingWorld.Domain.Shared;
using LivingWorld.Simulation.Core;

namespace LivingWorld.Api.Visual.Observable;

/// <summary>Lê os envolvidos de um evento do payload que o motor já grava (formato documentado em <see cref="WorldEventKind"/>).
/// Só leitura; evento sem formato conhecido cai no primeiro campo numérico como sujeito (o mesmo critério da posição).</summary>
public static class EventInvolvementReader
{
    public static EventInvolved Read(WorldState world, WorldEvent evt)
    {
        var f = evt.Payload.Split('|');
        long? L(int i) => i < f.Length && long.TryParse(f[i], out long v) && v >= 0 ? v : null;
        string? S(int i) => i < f.Length && f[i].Length > 0 ? f[i] : null;
        bool ChildVictim(int i) => L(i) is { } id && world.FindNpc(new NpcId(id)) is { } npc
            && world.LifeStageRules.LifeStageOf(npc.AgeYears(world.CurrentDate)) == LifeStage.Child;

        return evt.Kind switch
        {
            WorldEventKind.Birth => new(SubjectId: L(0), RelatedIds: Ids(L(1), L(2)), HouseholdId: L(3)),
            WorldEventKind.Marriage or WorldEventKind.CourtshipStarted or WorldEventKind.AffairStarted
                => new(SubjectId: L(0), OtherId: L(1)),
            WorldEventKind.SupportPaid => new(SubjectId: L(0), OtherId: L(1), Amount: L(3), RelatedIds: Ids(L(2))),
            WorldEventKind.AidGiven => new(SubjectId: L(0), OtherId: L(1), Amount: L(2), Motive: S(4)),
            // AD-183: os motivos do nome que o motor grava no evento
            WorldEventKind.CourtshipRejected => new(SubjectId: L(1), OtherId: L(2), Outcome: S(0),
                Motive: S(0) == "MesmoNome" ? "mesmo-nome" : null),
            WorldEventKind.EstateSettled => new(SubjectId: L(0), OtherId: L(5), Amount: L(6), Motive: L(5) is null ? null : "xara"),
            WorldEventKind.SettlementFounded => new(SubjectId: L(2), CityId: S(0), Outcome: S(3), Motive: L(2) is null ? null : "fundador"),
            WorldEventKind.TheftCommitted => new(AuthorId: L(0), VictimId: L(1), Amount: L(6), Outcome: S(7), CaseId: L(9),
                RecordOnly: ChildVictim(1)),
            WorldEventKind.TheftDiscovered => new(AuthorId: L(0), VictimId: L(1), Amount: L(2), Outcome: S(3)),
            WorldEventKind.Assault => new(AuthorId: L(0), VictimId: L(1), Amount: L(3), Outcome: S(4), CaseId: L(6),
                RecordOnly: ChildVictim(1)),
            WorldEventKind.DomesticViolence => new(AuthorId: L(0), VictimId: L(1), Amount: L(4), Outcome: S(5), CaseId: L(7),
                RecordOnly: true),
            WorldEventKind.SexualAssault => new(AuthorId: L(0), VictimId: L(1), CaseId: L(4), RecordOnly: true),
            WorldEventKind.Kidnapped => new(AuthorId: L(0), VictimId: L(1), Amount: L(4), CaseId: L(6),
                RecordOnly: S(3) == "crianca" || ChildVictim(1)),
            WorldEventKind.KidnappingEnded => new(VictimId: L(0), AuthorId: L(1), Outcome: S(2), Amount: L(4), CaseId: L(5),
                RecordOnly: ChildVictim(0)),
            WorldEventKind.CrimeReported => new(SubjectId: L(0), AuthorId: L(1), CaseId: L(2), Outcome: S(3), Motive: S(5)),
            WorldEventKind.Verdict => new(AuthorId: L(0), VictimId: L(1), CaseId: L(2), Amount: L(6),
                Outcome: S(4) is { } verdict ? $"{verdict}:{S(5)}" : null),
            WorldEventKind.Banished => new(SubjectId: L(0), CaseId: L(1), Outcome: S(3)),
            WorldEventKind.InheritanceDisputed => new(SubjectId: L(0), OtherId: L(1), RelatedIds: Ids(L(2)), Amount: L(3)),
            WorldEventKind.Homicide or WorldEventKind.Executed or WorldEventKind.Death or WorldEventKind.Starvation
                or WorldEventKind.Dehydration or WorldEventKind.Exhaustion or WorldEventKind.Accident or WorldEventKind.OldAge
                => new(SubjectId: L(0)),
            WorldEventKind.Disease => new(SubjectId: L(0), Outcome: S(1)),
            WorldEventKind.UnionEnded => new(SubjectId: L(0), OtherId: L(1), Outcome: S(2), HouseholdId: L(3)),
            WorldEventKind.RentUnpaid => new(HouseholdId: L(0), BuildingId: L(1), Amount: L(2)),
            WorldEventKind.Evicted => new(HouseholdId: L(0), BuildingId: L(1)),
            WorldEventKind.Trade => new(SubjectId: L(0), OtherId: L(1), WorkplaceId: L(2), Amount: L(4)),
            WorldEventKind.DesignPrototyped => new(SubjectId: L(0), DesignId: (int?)L(1)),
            WorldEventKind.DesignLearned => new(SubjectId: L(0), OtherId: L(1), DesignId: (int?)L(2)),
            WorldEventKind.DesignSold => new(SubjectId: L(0), OtherId: L(1), DesignId: (int?)L(2), Amount: L(3)),
            WorldEventKind.LeavingsLeft => new(LeavingId: L(0), SubjectId: L(1), HouseholdId: L(2), Amount: L(6)),
            WorldEventKind.LeavingsCollected => new(LeavingId: L(0), SubjectId: L(1), OtherId: L(2), Amount: L(3), Outcome: S(5)),
            WorldEventKind.Hired or WorldEventKind.Fired => new(SubjectId: L(0), WorkplaceId: L(1)),
            WorldEventKind.OutbreakStarted => new(SubjectId: L(1), CityId: S(2)),
            WorldEventKind.ProfitDrawn or WorldEventKind.WorkplaceBankrupt => new(WorkplaceId: L(0), SubjectId: L(1), Amount: L(2)),
            WorldEventKind.VentureFounded => new(WorkplaceId: L(0), SubjectId: L(1)),
            _ => new(SubjectId: L(0)),
        };
    }

    /// <summary>Célula de um espólio a partir do evento (deixado traz x e y no payload; recolhido aponta para o espólio).</summary>
    public static CellCoord? LeavingCell(WorldState world, WorldEvent evt)
    {
        var f = evt.Payload.Split('|');
        if (evt.Kind == WorldEventKind.LeavingsLeft && f.Length > 5
            && int.TryParse(f[4], out int x) && int.TryParse(f[5], out int y))
            return new CellCoord(x, y);
        return null;
    }

    /// <summary>Onde o evento aconteceu, pelos envolvidos (AD-170): célula do espólio; senão a pessoa principal (sujeito, autor, vítima,
    /// outro) viva; senão a casa; senão o prédio; senão a oficina. Nulo quando nada disso existe mais.</summary>
    public static CellCoord? LocationOf(WorldState world, WorldEvent evt, EventInvolved involved)
    {
        if (LeavingCell(world, evt) is { } cell) return cell;
        // a pessoa do primeiro campo (critério antigo) continua mandando quando ela é um dos envolvidos
        if (long.TryParse(evt.Payload.Split('|')[0], out long first) && People(involved).Contains(first)
            && world.FindNpc(new NpcId(first)) is { } firstNpc)
            return firstNpc.CurrentLocation;
        foreach (var id in new[] { involved.SubjectId, involved.AuthorId, involved.VictimId, involved.OtherId })
            if (id is { } npcId && world.FindNpc(new NpcId(npcId)) is { } npc) return npc.CurrentLocation;
        if (involved.HouseholdId is { } home && world.FindHousehold(new HouseholdId(home)) is { } household) return household.Location;
        if (involved.WorkplaceId is { } shop && world.FindWorkplace(new WorkplaceId(shop)) is { } workplace) return workplace.Location;
        if (involved.BuildingId is { } b && world.FindBuilding(new BuildingId(b)) is { Position: { } position }) return position;
        if (evt.Kind == WorldEventKind.LeavingsCollected && involved.LeavingId is { } leaving
            && world.Invention?.Leavings?.FirstOrDefault(l => l.Id == leaving) is { Id: > 0 } found)
            return new CellCoord(found.X, found.Y);
        return null;
    }

    /// <summary>Ids de pessoa que o evento cita (sujeito, outro, autor, vítima e relacionados).</summary>
    public static IEnumerable<long> People(EventInvolved involved)
    {
        if (involved.SubjectId is { } s) yield return s;
        if (involved.OtherId is { } o) yield return o;
        if (involved.AuthorId is { } a) yield return a;
        if (involved.VictimId is { } v) yield return v;
        if (involved.RelatedIds is { } related)
            foreach (var id in related) yield return id;
    }

    private static IReadOnlyList<long>? Ids(params long?[] ids)
    {
        var present = ids.Where(id => id is not null).Select(id => id!.Value).ToArray();
        return present.Length == 0 ? null : new ValueList<long>(present);
    }
}
