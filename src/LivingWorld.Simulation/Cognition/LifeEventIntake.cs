using System.Globalization;
using LivingWorld.Domain.Cognition;
using LivingWorld.Domain.History;
using LivingWorld.Domain.Population;
using LivingWorld.Domain.Population.Family;
using LivingWorld.Domain.Shared;
using LivingWorld.Simulation.Core;

namespace LivingWorld.Simulation.Cognition;

/// <summary>Consumidor do log de eventos (AD-138): cada evento de vida que o motor já emite vira lembrança e emoção em quem o
/// viveu, sem tocar quem emite e sem varredura por tick. Chamado por <c>TickContext.LogEvent</c> no mesmo instante do evento;
/// eventos que não marcam ninguém saem no primeiro <c>switch</c>. Quem sente o quê e quanto vem de <see cref="PsycheRules"/>
/// e da proximidade do vínculo.</summary>
public static class LifeEventIntake
{
    private const string DeathSource = "NpcDeath";
    private const string BirthSource = "NatalitySystem";

    public static void OnEvent(WorldState world, WorldEventKind kind, string payload, string sourceSystem, long tick)
    {
        switch (kind)
        {
            case WorldEventKind.Death or WorldEventKind.Starvation or WorldEventKind.Dehydration or WorldEventKind.Exhaustion
                or WorldEventKind.Disease or WorldEventKind.MaternalDeath when sourceSystem == DeathSource:
                if (FindAt(world, payload, 0) is { } dead) Mourn(world, dead, tick);
                return;
            case WorldEventKind.Birth when sourceSystem == BirthSource:
                {
                    long baby = IdAt(payload, 0);
                    Experience(world, IdAt(payload, 1), LifeEventKind.ChildBorn, baby, tick);
                    Experience(world, IdAt(payload, 2), LifeEventKind.ChildBorn, baby, tick);
                    return;
                }
            case WorldEventKind.StillBirth:
                {
                    double scale = Field(payload, 2).Length > 0 ? 0.6 : 1.0; // aborto espontâneo marca menos que natimorto
                    Experience(world, IdAt(payload, 0), LifeEventKind.StillBirth, 0, tick, scale);
                    Experience(world, IdAt(payload, 1), LifeEventKind.StillBirth, 0, tick, 0.7 * scale);
                    return;
                }
            case WorldEventKind.Marriage:
                Experience(world, IdAt(payload, 0), LifeEventKind.Married, IdAt(payload, 1), tick);
                Experience(world, IdAt(payload, 1), LifeEventKind.Married, IdAt(payload, 0), tick);
                return;
            case WorldEventKind.Hired:
                Experience(world, IdAt(payload, 0), LifeEventKind.Hired, 0, tick);
                return;
            case WorldEventKind.Fired:
                Experience(world, IdAt(payload, 0), LifeEventKind.Fired, 0, tick);
                return;
            case WorldEventKind.AidGiven:
                {
                    string helper = Field(payload, 0);
                    long helperId = helper.StartsWith("city-", StringComparison.Ordinal) ? Nobody : IdAt(payload, 0);
                    long recipient = IdAt(payload, 1);
                    Experience(world, recipient, LifeEventKind.AidReceived, Math.Max(0, helperId), tick);
                    if (helperId != Nobody) Experience(world, helperId, LifeEventKind.AidGiven, recipient, tick);
                    return;
                }
            case WorldEventKind.TheftDiscovered:
                {
                    long thief = IdAt(payload, 0), victim = IdAt(payload, 1);
                    Experience(world, victim, LifeEventKind.Robbed, thief, tick);
                    Experience(world, thief, LifeEventKind.CaughtStealing, victim, tick);
                    return;
                }
            case WorldEventKind.UnionEnded:
                {
                    long leaver = IdAt(payload, 0), stayer = IdAt(payload, 1);
                    bool abandoned = Field(payload, 2) == "abandono";
                    Experience(world, leaver, LifeEventKind.Separated, stayer, tick, 0.8);
                    Experience(world, stayer, abandoned ? LifeEventKind.Abandoned : LifeEventKind.Separated, leaver, tick);
                    return;
                }
            case WorldEventKind.AffairDiscovered:
                {
                    long betrayed = IdAt(payload, 0), unfaithful = IdAt(payload, 1);
                    Experience(world, betrayed, LifeEventKind.Betrayed, unfaithful, tick);
                    Experience(world, unfaithful, LifeEventKind.BetrayedSpouse, betrayed, tick);
                    if (Field(payload, 3).StartsWith("violencia", StringComparison.Ordinal))
                        Experience(world, unfaithful, LifeEventKind.Violence, betrayed, tick);
                    return;
                }
            case WorldEventKind.Evicted:
                {
                    long householdId = IdAt(payload, 0);
                    if (world.FindHousehold(new HouseholdId(householdId)) is not { } household) return;
                    foreach (var member in household.Members.OrderBy(m => m.Value))
                        Experience(world, member.Value, LifeEventKind.Evicted, 0, tick);
                    return;
                }
            case WorldEventKind.UnwedPregnancy:
                {
                    // AD-139: a vergonha da vila na mãe solteira e nos pais dela; deixada pelo pai, também o abandono
                    long motherId = IdAt(payload, 0), fatherId = IdAt(payload, 1);
                    string outcome = Field(payload, 2);
                    bool married = outcome == "casamento";
                    Experience(world, motherId, LifeEventKind.UnwedPregnancy, fatherId, tick, married ? 0.3 : 1.0);
                    if (outcome == "abandono") Experience(world, motherId, LifeEventKind.Abandoned, fatherId, tick, 0.8);
                    if (world.FindNpc(new NpcId(motherId)) is not { } mother) return;
                    double parents = world.FamilyRules.Unions.Intimacy.ParentShameScale * (married ? 0.4 : 1.0);
                    if (mother.MotherId is { } gm) Experience(world, gm.Value, LifeEventKind.ChildUnwedPregnancy, motherId, tick, parents);
                    if (mother.FatherId is { } gf) Experience(world, gf.Value, LifeEventKind.ChildUnwedPregnancy, motherId, tick, parents);
                    return;
                }
            case WorldEventKind.OrphanTakenIn:
                Experience(world, IdAt(payload, 0), LifeEventKind.TakenIn, IdAt(payload, 1), tick);
                return;
            case WorldEventKind.DesignPrototyped when world.InventionRules is { } invention && Field(payload, 2) is "0" or "3":
                Experience(world, IdAt(payload, 0), LifeEventKind.InventionWorked, 0, tick, 1.0, invention.SuccessImpact);
                return;
            case WorldEventKind.PrototypeFailed when world.InventionRules is { } invention:
                Experience(world, IdAt(payload, 0), LifeEventKind.InventionFailed, 0, tick, Field(payload, 2) == "1" ? 0.6 : 1.0, invention.FailureImpact);
                return;
        }
    }

    /// <summary>Gancho para a fatia de doença: chamar quando alguém adoece (memória e medo) — também marca quem mora com ele.</summary>
    public static void OnFellIll(WorldState world, Npc sick, long tick)
    {
        if (!world.NeedsRules.Psyche.Enabled) return;
        PsycheMechanic.Experience(world, sick, LifeEventKind.FellIll, 0, tick);
        if (sick.Household is not { } id || world.FindHousehold(id) is not { } home) return;
        foreach (var member in home.Members.OrderBy(m => m.Value))
            if (member != sick.Id && world.FindNpc(member) is { IsAlive: true } other)
                PsycheMechanic.Experience(world, other, LifeEventKind.HouseholdMemberIll, sick.Id.Value, tick);
    }

    /// <summary>Fim de um jejum (fome em zero): se durou o bastante vira lembrança de fome, mais forte quanto mais longo.</summary>
    public static void OnHungerEpisodeEnded(WorldState world, Npc npc, long zeroSinceTick, long tick)
    {
        var rules = world.NeedsRules.Psyche;
        if (!rules.Enabled) return;
        long hours = tick - zeroSinceTick;
        if (hours < rules.FamineMinHours) return;
        double days = hours / (double)world.Calendar.HoursPerDay;
        PsycheMechanic.Experience(world, npc, LifeEventKind.Famine, 0, tick, Math.Min(1.6, 0.6 + 0.1 * days));
    }

    /// <summary>Quem morreu enluta cônjuge, pais, filhos, irmãos e amigos, cada um na medida do vínculo. Filhos e irmãos são
    /// achados numa passada pelos vivos: a morte é rara, então isso custa pouco por ano mesmo a milhares de NPCs.</summary>
    private static void Mourn(WorldState world, Npc dead, long tick)
    {
        var rules = world.NeedsRules.Psyche;
        if (!rules.Enabled) return;
        long deadId = dead.Id.Value;
        var mourned = new SortedDictionary<long, (LifeEventKind Kind, double Scale)>();

        void Offer(Npc? who, LifeEventKind kind, double scale)
        {
            if (who is not { IsAlive: true } || who.Id == dead.Id) return;
            double current = mourned.TryGetValue(who.Id.Value, out var had) ? rules.ImpactOf(had.Kind).Salience * had.Scale : -1;
            if (rules.ImpactOf(kind).Salience * scale > current) mourned[who.Id.Value] = (kind, scale);
        }

        if (dead.Spouse is { } spouseId && world.FindNpc(spouseId) is { } spouse && spouse.Spouse == dead.Id)
            Offer(spouse, LifeEventKind.SpouseDied, 1.0);
        if (dead.MotherId is { } m) Offer(world.FindNpc(m), LifeEventKind.ChildDied, 1.0);
        if (dead.FatherId is { } f) Offer(world.FindNpc(f), LifeEventKind.ChildDied, 1.0);

        foreach (var other in world.AliveNpcIndex.Alive)
        {
            if (other.MotherId == dead.Id || other.FatherId == dead.Id)
                Offer(other, LifeEventKind.ParentDied, 1.0);
            else if ((dead.MotherId is { } dm && other.MotherId == dm) || (dead.FatherId is { } df && other.FatherId == df))
                Offer(other, LifeEventKind.SiblingDied, 1.0);
        }

        var targets = world.RelationshipTargetsOf(dead.Id);
        for (int i = 0; i < targets.Count; i++)
        {
            if (!world.Relationships.TryGetValue(new RelationshipKey(targets[i], dead.Id), out var bond)) continue;
            if (bond.Affection < rules.FriendAffectionForGrief) continue;
            Offer(world.FindNpc(targets[i]), LifeEventKind.FriendDied, bond.Affection / 100.0);
        }

        foreach (var (id, (kind, scale)) in mourned)
            if (world.FindNpc(new NpcId(id)) is { } mourner)
                PsycheMechanic.Experience(world, mourner, kind, deadId, tick, scale);
    }

    private static void Experience(WorldState world, long npcId, LifeEventKind kind, long other, long tick, double scale = 1.0,
        LifeEventImpact? impact = null)
    {
        // AD-143: a pessoa 0 existe (os ids começam em 0); "ninguém" é campo ausente ou ilegível (Nobody = -1)
        if (npcId < 0 || !world.NeedsRules.Psyche.Enabled) return;
        if (world.FindNpc(new NpcId(npcId)) is { IsAlive: true } npc)
            PsycheMechanic.Experience(world, npc, kind, other, tick, scale, impact);
    }

    private static Npc? FindAt(WorldState world, string payload, int index)
    {
        long id = IdAt(payload, index);
        return id >= 0 ? world.FindNpc(new NpcId(id)) : null;
    }

    /// <summary>"Ninguém" no payload: campo ausente ou que não é número (antes era 0, e a pessoa 0 ficava sem lembrança).</summary>
    private const long Nobody = -1;

    private static long IdAt(string payload, int index) =>
        long.TryParse(Field(payload, index), NumberStyles.Integer, CultureInfo.InvariantCulture, out long id) && id >= 0 ? id : Nobody;

    private static string Field(string payload, int index)
    {
        int start = 0;
        for (int i = 0; i < index; i++)
        {
            int bar = payload.IndexOf('|', start);
            if (bar < 0) return "";
            start = bar + 1;
        }
        int end = payload.IndexOf('|', start);
        return end < 0 ? payload[start..] : payload[start..end];
    }
}
