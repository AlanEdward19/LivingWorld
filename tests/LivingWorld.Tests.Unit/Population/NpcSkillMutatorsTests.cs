using System.Text.Json;
using System.Text.Json.Serialization;
using LivingWorld.Domain;

namespace LivingWorld.Tests.Population;

public class NpcSkillMutatorsTests
{
    private static readonly WorldCalendar Calendar = new(24, 30, 12);

    private static readonly Personality DefaultPersonality =
        Personality.Create(50, 50, 50, 50, 50, 50, 50, 50, 50, 50).Value!;

    private static Npc MakeNpc(ProfessionType profession = default, SkillSet? skills = null, RateGene? rateGene = null, NpcId? mentor = null) => new(
        new NpcId(1), "test", Sex.Female, WorldDate.Epoch(Calendar), new CultureId(1), new CellCoord(0, 0),
        motherId: null, fatherId: null, household: null, health: 100,
        personality: DefaultPersonality, profession: profession, currentLocation: new CellCoord(0, 0),
        skills: skills, rateGene: rateGene, mentor: mentor);

    // SKILL-14: troca de profissão nunca toca Skills — estagnação por ausência de ganho, não reset.
    [Fact]
    public void SwitchProfession_changes_profession_and_leaves_skills_byte_identical()
    {
        var skills = SkillSet.Empty.WithGain(new SkillType(0), 5, cap: 100);
        var npc = MakeNpc(profession: new ProfessionType(1), skills: skills);

        npc.SwitchProfession(new ProfessionType(2));

        Assert.Equal(new ProfessionType(2), npc.Profession);
        Assert.Equal(skills.Get(new SkillType(0)), npc.Skills.Get(new SkillType(0)));
        Assert.Equal(skills.Get(new SkillType(7)), npc.Skills.Get(new SkillType(7)));
        Assert.Equal(skills.Get(new SkillType(12)), npc.Skills.Get(new SkillType(12)));
    }

    [Fact]
    public void AssignMentor_sets_mentor()
    {
        var npc = MakeNpc();
        Assert.Null(npc.Mentor);

        npc.AssignMentor(new NpcId(42));

        Assert.Equal(new NpcId(42), npc.Mentor);
    }

    // Edge Case da spec: mestre morto no meio da tutoria — vínculo encerrado sem exceção,
    // mesmo padrão de LeaveHousehold limpando a referência.
    [Fact]
    public void ClearMentor_clears_mentor()
    {
        var npc = MakeNpc();
        npc.AssignMentor(new NpcId(42));
        Assert.NotNull(npc.Mentor);

        npc.ClearMentor();

        Assert.Null(npc.Mentor);
    }

    [Fact]
    public void Constructor_round_trip_exposes_skills_rategene_and_mentor_unchanged()
    {
        var skills = SkillSet.Empty.WithGain(new SkillType(7), 15, cap: 100);
        var rateGene = new RateGene(1.4);
        var npc = MakeNpc(skills: skills, rateGene: rateGene, mentor: new NpcId(9));

        Assert.Equal(skills.Get(new SkillType(7)), npc.Skills.Get(new SkillType(7)));
        Assert.Equal(skills.Get(new SkillType(0)), npc.Skills.Get(new SkillType(0)));
        Assert.Equal(rateGene, npc.RateGene);
        Assert.Equal(new NpcId(9), npc.Mentor);
    }

    // Protege o round-trip de System.Text.Json (AD-026) — nenhum campo novo pode ficar de fora
    // do construtor único usado pelo snapshot.
    [Fact]
    public void Round_trip_via_json_preserves_skills_rategene_and_mentor()
    {
        var options = new JsonSerializerOptions { Converters = { new JsonStringEnumConverter() } };
        var skills = SkillSet.Empty.WithGain(new SkillType(4), 7, cap: 100);
        var npc = new Npc(
            new NpcId(7), "round-trip", Sex.Male, WorldDate.Epoch(Calendar), new CultureId(2), new CellCoord(1, 1),
            motherId: null, fatherId: null, household: null, health: 80,
            personality: DefaultPersonality, profession: new ProfessionType(9),
            currentLocation: new CellCoord(5, 6),
            skills: skills, rateGene: new RateGene(1.2), mentor: new NpcId(3));

        var json = JsonSerializer.Serialize(npc, options);
        var rehydrated = JsonSerializer.Deserialize<Npc>(json, options)!;

        Assert.Equal(npc.Skills.Get(new SkillType(4)), rehydrated.Skills.Get(new SkillType(4)));
        Assert.Equal(npc.RateGene, rehydrated.RateGene);
        Assert.Equal(npc.Mentor, rehydrated.Mentor);
    }
}
