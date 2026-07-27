namespace LivingWorld.Domain;

/// <summary>As 13 habilidades de um <c>Npc</c> (Fase 6, task 4) — cada uma um <c>double</c> em
/// <c>[0, cap]</c>. Imutável: todo ganho passa por <see cref="WithGain"/>, que devolve uma nova
/// instância (mesmo espírito de <see cref="Personality"/> como "conjunto de traços imutável").
/// Leitura por <c>switch</c> direto sobre <see cref="SkillType"/>, sem reflexão no hot path
/// (mesmo padrão de <see cref="PersonalityWeighting"/>).</summary>
public sealed class SkillSet
{
    public double Agriculture { get; }
    public double Hunting { get; }
    public double Trade { get; }
    public double Construction { get; }
    public double Medicine { get; }
    public double Combat { get; }
    public double Teaching { get; }
    public double Craft { get; }
    public double Politics { get; }
    public double Leadership { get; }
    public double Research { get; }
    public double Technology { get; }
    public double Magic { get; }

    /// <summary>Público — permite ao <c>System.Text.Json</c> reidratar via construtor único
    /// (mesmo padrão de round-trip usado por <see cref="Npc"/>/<see cref="Personality"/>).</summary>
    public SkillSet(
        double agriculture, double hunting, double trade, double construction, double medicine, double combat,
        double teaching, double craft, double politics, double leadership, double research, double technology,
        double magic)
    {
        Agriculture = agriculture;
        Hunting = hunting;
        Trade = trade;
        Construction = construction;
        Medicine = medicine;
        Combat = combat;
        Teaching = teaching;
        Craft = craft;
        Politics = politics;
        Leadership = leadership;
        Research = research;
        Technology = technology;
        Magic = magic;
    }

    /// <summary>Todas as 13 habilidades no mesmo valor inicial declarado pelo cenário (SKILL-01).
    /// O chamador é responsável por passar um valor dentro de <c>[0,cap]</c> — não há teto aqui
    /// pra checar, o teto só entra em <see cref="WithGain"/>.</summary>
    public static SkillSet Initial(double startingValue) => new(
        startingValue, startingValue, startingValue, startingValue, startingValue, startingValue,
        startingValue, startingValue, startingValue, startingValue, startingValue, startingValue, startingValue);

    public double Get(SkillType type) => type switch
    {
        SkillType.Agriculture => Agriculture,
        SkillType.Hunting => Hunting,
        SkillType.Trade => Trade,
        SkillType.Construction => Construction,
        SkillType.Medicine => Medicine,
        SkillType.Combat => Combat,
        SkillType.Teaching => Teaching,
        SkillType.Craft => Craft,
        SkillType.Politics => Politics,
        SkillType.Leadership => Leadership,
        SkillType.Research => Research,
        SkillType.Technology => Technology,
        SkillType.Magic => Magic,
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
            SkillType.Agriculture => new SkillSet(newValue, Hunting, Trade, Construction, Medicine, Combat, Teaching, Craft, Politics, Leadership, Research, Technology, Magic),
            SkillType.Hunting => new SkillSet(Agriculture, newValue, Trade, Construction, Medicine, Combat, Teaching, Craft, Politics, Leadership, Research, Technology, Magic),
            SkillType.Trade => new SkillSet(Agriculture, Hunting, newValue, Construction, Medicine, Combat, Teaching, Craft, Politics, Leadership, Research, Technology, Magic),
            SkillType.Construction => new SkillSet(Agriculture, Hunting, Trade, newValue, Medicine, Combat, Teaching, Craft, Politics, Leadership, Research, Technology, Magic),
            SkillType.Medicine => new SkillSet(Agriculture, Hunting, Trade, Construction, newValue, Combat, Teaching, Craft, Politics, Leadership, Research, Technology, Magic),
            SkillType.Combat => new SkillSet(Agriculture, Hunting, Trade, Construction, Medicine, newValue, Teaching, Craft, Politics, Leadership, Research, Technology, Magic),
            SkillType.Teaching => new SkillSet(Agriculture, Hunting, Trade, Construction, Medicine, Combat, newValue, Craft, Politics, Leadership, Research, Technology, Magic),
            SkillType.Craft => new SkillSet(Agriculture, Hunting, Trade, Construction, Medicine, Combat, Teaching, newValue, Politics, Leadership, Research, Technology, Magic),
            SkillType.Politics => new SkillSet(Agriculture, Hunting, Trade, Construction, Medicine, Combat, Teaching, Craft, newValue, Leadership, Research, Technology, Magic),
            SkillType.Leadership => new SkillSet(Agriculture, Hunting, Trade, Construction, Medicine, Combat, Teaching, Craft, Politics, newValue, Research, Technology, Magic),
            SkillType.Research => new SkillSet(Agriculture, Hunting, Trade, Construction, Medicine, Combat, Teaching, Craft, Politics, Leadership, newValue, Technology, Magic),
            SkillType.Technology => new SkillSet(Agriculture, Hunting, Trade, Construction, Medicine, Combat, Teaching, Craft, Politics, Leadership, Research, newValue, Magic),
            SkillType.Magic => new SkillSet(Agriculture, Hunting, Trade, Construction, Medicine, Combat, Teaching, Craft, Politics, Leadership, Research, Technology, newValue),
            _ => throw new ArgumentOutOfRangeException(nameof(type), type, "SkillType sem acesso direto declarado"),
        };
    }
}
