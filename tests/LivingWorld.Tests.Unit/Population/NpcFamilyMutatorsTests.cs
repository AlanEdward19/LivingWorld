using System.Text.Json;
using System.Text.Json.Serialization;
using LivingWorld.Domain;

namespace LivingWorld.Tests.Population;

public class NpcFamilyMutatorsTests
{
    private static readonly WorldCalendar Calendar = new(24, 30, 12);

    private static readonly Personality DefaultPersonality =
        Personality.Create(50, 50, 50, 50, 50, 50, 50, 50, 50, 50).Value!;

    private static Npc MakeNpc(
        double vitality = 50.0, double upbringing = 50.0, NpcId? spouse = null, NpcId? courtingWith = null) => new(
        new NpcId(1), "test", Sex.Female, WorldDate.Epoch(Calendar), new CultureId(1), new CellCoord(0, 0),
        motherId: null, fatherId: null, household: null, health: 100,
        personality: DefaultPersonality, profession: default, currentLocation: new CellCoord(0, 0),
        vitality: vitality, upbringing: upbringing, spouse: spouse, courtingWith: courtingWith);

    // FAM-12: Npc.Marry só seta o próprio campo — chamar duas vezes (uma por cônjuge) é
    // responsabilidade de MarriageSystem, nunca do próprio mutador.
    [Fact]
    public void Marry_sets_only_this_npcs_own_spouse_field()
    {
        var npc = MakeNpc();
        Assert.Null(npc.Spouse);

        npc.Marry(new NpcId(2));

        Assert.Equal(new NpcId(2), npc.Spouse);
    }

    // AD-031/AD-060: viuvez continua legível — Spouse aponta a alguém morto e nunca é limpo por
    // nenhum mutador existente (mesmo espírito de MotherId/FatherId, referência histórica válida).
    [Fact]
    public void Spouse_pointing_to_a_dead_npc_remains_readable_after_other_mutators_run()
    {
        var deceasedSpouseId = new NpcId(2);
        var npc = MakeNpc();
        npc.Marry(deceasedSpouseId);

        npc.SetHealth(0);
        npc.StartCourtship(new NpcId(3));
        npc.EndCourtship();

        Assert.Equal(deceasedSpouseId, npc.Spouse);
    }

    // AD-060: nunca existe mutador de "divorciar" — prova estrutural via reflexão, não apenas
    // ausência observada por acaso.
    [Fact]
    public void Npc_never_exposes_a_divorce_mutator()
    {
        var methodNames = typeof(Npc).GetMethods().Select(m => m.Name);

        Assert.DoesNotContain(methodNames, name => name.Contains("divorce", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void StartCourtship_sets_courting_with()
    {
        var npc = MakeNpc();
        Assert.Null(npc.CourtingWith);

        npc.StartCourtship(new NpcId(9));

        Assert.Equal(new NpcId(9), npc.CourtingWith);
    }

    [Fact]
    public void EndCourtship_clears_courting_with()
    {
        var npc = MakeNpc();
        npc.StartCourtship(new NpcId(9));
        Assert.NotNull(npc.CourtingWith);

        npc.EndCourtship();

        Assert.Null(npc.CourtingWith);
    }

    // Defesa contra valor fora de faixa (mesmo padrão de SetHunger/SetHealth) — Vitality/Upbringing
    // nunca ficam fora de [0,100], mesmo que o chamador passe um valor extremo no construtor.
    [Theory]
    [InlineData(-50.0, 0.0)]
    [InlineData(150.0, 100.0)]
    public void Vitality_and_upbringing_are_clamped_to_0_100_range(double outOfRange, double expectedClamped)
    {
        var npc = MakeNpc(vitality: outOfRange, upbringing: outOfRange);

        Assert.Equal(expectedClamped, npc.Vitality);
        Assert.Equal(expectedClamped, npc.Upbringing);
    }

    // Protege o round-trip de System.Text.Json (AD-026) — os 4 campos novos precisam estar no
    // construtor único usado pelo snapshot, senão desaparecem na rehidratação.
    [Fact]
    public void Round_trip_via_json_preserves_vitality_upbringing_spouse_and_courting_with()
    {
        var options = new JsonSerializerOptions { Converters = { new JsonStringEnumConverter() } };
        var npc = MakeNpc(vitality: 72.5, upbringing: 31.0, spouse: new NpcId(4), courtingWith: new NpcId(5));

        var json = JsonSerializer.Serialize(npc, options);
        var rehydrated = JsonSerializer.Deserialize<Npc>(json, options)!;

        Assert.Equal(npc.Vitality, rehydrated.Vitality);
        Assert.Equal(npc.Upbringing, rehydrated.Upbringing);
        Assert.Equal(npc.Spouse, rehydrated.Spouse);
        Assert.Equal(npc.CourtingWith, rehydrated.CourtingWith);
    }
}
