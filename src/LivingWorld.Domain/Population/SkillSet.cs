namespace LivingWorld.Domain;

/// <summary>As 13 habilidades de um <c>Npc</c> (Fase 6, task 4) — cada uma um <c>double</c> em
/// <c>[0, cap]</c>. Imutável: todo ganho passa por <see cref="WithGain"/>, que devolve uma nova
/// instância (mesmo espírito de <see cref="Personality"/> como "conjunto de traços imutável").
/// Leitura por <c>switch</c> direto sobre <see cref="SkillType"/>, sem reflexão no hot path
/// (mesmo padrão de <see cref="PersonalityWeighting"/>).</summary>
public sealed class SkillSet
{
    private readonly double _agriculture;
    private readonly double _hunting;
    private readonly double _trade;
    private readonly double _construction;
    private readonly double _medicine;
    private readonly double _combat;
    private readonly double _teaching;
    private readonly double _craft;
    private readonly double _politics;
    private readonly double _leadership;
    private readonly double _research;
    private readonly double _technology;
    private readonly double _magic;

    private SkillSet(
        double agriculture, double hunting, double trade, double construction, double medicine, double combat,
        double teaching, double craft, double politics, double leadership, double research, double technology,
        double magic)
    {
        _agriculture = agriculture;
        _hunting = hunting;
        _trade = trade;
        _construction = construction;
        _medicine = medicine;
        _combat = combat;
        _teaching = teaching;
        _craft = craft;
        _politics = politics;
        _leadership = leadership;
        _research = research;
        _technology = technology;
        _magic = magic;
    }

    /// <summary>Todas as 13 habilidades no mesmo valor inicial declarado pelo cenário (SKILL-01).
    /// O chamador é responsável por passar um valor dentro de <c>[0,cap]</c> — não há teto aqui
    /// pra checar, o teto só entra em <see cref="WithGain"/>.</summary>
    public static SkillSet Initial(double startingValue) => new(
        startingValue, startingValue, startingValue, startingValue, startingValue, startingValue,
        startingValue, startingValue, startingValue, startingValue, startingValue, startingValue, startingValue);

    public double Get(SkillType type) => type switch
    {
        SkillType.Agriculture => _agriculture,
        SkillType.Hunting => _hunting,
        SkillType.Trade => _trade,
        SkillType.Construction => _construction,
        SkillType.Medicine => _medicine,
        SkillType.Combat => _combat,
        SkillType.Teaching => _teaching,
        SkillType.Craft => _craft,
        SkillType.Politics => _politics,
        SkillType.Leadership => _leadership,
        SkillType.Research => _research,
        SkillType.Technology => _technology,
        SkillType.Magic => _magic,
        _ => throw new ArgumentOutOfRangeException(nameof(type), type, "SkillType sem acesso direto declarado"),
    };

    /// <summary>Aplica <paramref name="delta"/> à habilidade <paramref name="type"/>, clampado em
    /// <c>[0, cap]</c> — ganho no teto é absorvido sem exceção e sem efeito colateral em outro
    /// campo (SKILL-12). Devolve uma nova instância; as demais 12 habilidades são preservadas.</summary>
    public SkillSet WithGain(SkillType type, double delta, double cap)
    {
        double newValue = Math.Clamp(Get(type) + delta, 0.0, cap);

        return type switch
        {
            SkillType.Agriculture => new SkillSet(newValue, _hunting, _trade, _construction, _medicine, _combat, _teaching, _craft, _politics, _leadership, _research, _technology, _magic),
            SkillType.Hunting => new SkillSet(_agriculture, newValue, _trade, _construction, _medicine, _combat, _teaching, _craft, _politics, _leadership, _research, _technology, _magic),
            SkillType.Trade => new SkillSet(_agriculture, _hunting, newValue, _construction, _medicine, _combat, _teaching, _craft, _politics, _leadership, _research, _technology, _magic),
            SkillType.Construction => new SkillSet(_agriculture, _hunting, _trade, newValue, _medicine, _combat, _teaching, _craft, _politics, _leadership, _research, _technology, _magic),
            SkillType.Medicine => new SkillSet(_agriculture, _hunting, _trade, _construction, newValue, _combat, _teaching, _craft, _politics, _leadership, _research, _technology, _magic),
            SkillType.Combat => new SkillSet(_agriculture, _hunting, _trade, _construction, _medicine, newValue, _teaching, _craft, _politics, _leadership, _research, _technology, _magic),
            SkillType.Teaching => new SkillSet(_agriculture, _hunting, _trade, _construction, _medicine, _combat, newValue, _craft, _politics, _leadership, _research, _technology, _magic),
            SkillType.Craft => new SkillSet(_agriculture, _hunting, _trade, _construction, _medicine, _combat, _teaching, newValue, _politics, _leadership, _research, _technology, _magic),
            SkillType.Politics => new SkillSet(_agriculture, _hunting, _trade, _construction, _medicine, _combat, _teaching, _craft, newValue, _leadership, _research, _technology, _magic),
            SkillType.Leadership => new SkillSet(_agriculture, _hunting, _trade, _construction, _medicine, _combat, _teaching, _craft, _politics, newValue, _research, _technology, _magic),
            SkillType.Research => new SkillSet(_agriculture, _hunting, _trade, _construction, _medicine, _combat, _teaching, _craft, _politics, _leadership, newValue, _technology, _magic),
            SkillType.Technology => new SkillSet(_agriculture, _hunting, _trade, _construction, _medicine, _combat, _teaching, _craft, _politics, _leadership, _research, newValue, _magic),
            SkillType.Magic => new SkillSet(_agriculture, _hunting, _trade, _construction, _medicine, _combat, _teaching, _craft, _politics, _leadership, _research, _technology, newValue),
            _ => throw new ArgumentOutOfRangeException(nameof(type), type, "SkillType sem acesso direto declarado"),
        };
    }
}
