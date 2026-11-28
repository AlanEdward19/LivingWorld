using System.Text.Json.Serialization;

namespace LivingWorld.Domain.Objects;

/// <summary>#255: reação de uma necessidade do catálogo quando o nível cai abaixo de <c>Below</c> (0–100). <c>Target</c> é um
/// mecanismo genérico do motor, nunca uma regra de uma necessidade: <c>health</c> (saúde por hora de mundo), <c>social</c> (a solidão cresce
/// mais rápido, por hora) e <c>bond</c> (cada conversa da pessoa com alguém pesa no laço dos dois). <c>PerHour</c> é a magnitude (negativa: piora).</summary>
public sealed record NeedEffect(double Below, string Target, double PerHour);

/// <summary>Alvos de <see cref="NeedEffect"/> que o motor conhece.</summary>
public static class NeedEffectTargets
{
    public const string Health = "health";
    public const string Social = "social";
    public const string Bond = "bond";
    public static readonly IReadOnlyList<string> Known = [Health, Social, Bond];
}

/// <summary>#255: uma necessidade declarada no cenário (higiene, diversão, bexiga...): nome, nível de partida, taxa de queda por hora, abaixo de
/// quanto é urgente (a pessoa a atende e a barra a destaca) e o que acontece quando está baixa. Ids <c>hunger</c>, <c>thirst</c>, <c>sleep</c> e
/// <c>social</c> são as necessidades de sempre do motor (<see cref="NeedIds.Builtin"/>); o resto vive em <c>Npc.ExtraNeeds</c>.</summary>
public sealed record NeedDef(
    string Id, string Name, double Start, double DecayPerHour, double UrgentBelow, string? Color = null,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] IReadOnlyList<NeedEffect>? Effects = null,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] string? Alert = null,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] string? Icon = null);

public static class NeedIds
{
    public static readonly IReadOnlyList<string> Builtin = ["hunger", "thirst", "sleep", "social"];
    public static bool IsBuiltin(string id) => id is "hunger" or "thirst" or "sleep" or "social";
}

/// <summary>As necessidades do cenário. Nulo no catálogo = cenário sem elas (omitido do snapshot e do hash).</summary>
public sealed record NeedCatalog(IReadOnlyList<NeedDef> Needs)
{
    public NeedDef? Find(string id)
    {
        for (int i = 0; i < Needs.Count; i++)
            if (Needs[i].Id == id) return Needs[i];
        return null;
    }
}

/// <summary>#255: o que um uso entrega por hora de mundo a uma necessidade (<c>Need</c>: id do catálogo ou uma das de sempre).</summary>
public sealed record UseEffect(string Need, double PerHour);

/// <summary>#255: o que o uso gasta da despensa da casa, por vez (comida do fogão e da geladeira).</summary>
public sealed record UseConsume(int Resource, long Quantity);

/// <summary>#255: um uso de objeto, tudo em dado: o que a pessoa faz (<c>Name</c>), quanto leva (<c>DurationMin</c> de mundo; com
/// <c>UntilFull</c>, até a necessidade citada encher, no máximo isso), o que entrega (<c>Effects</c>), o que gasta (<c>Consumes</c>), a pose do
/// corpo (nome declarado aqui; sem animação própria o cliente usa <c>PoseFallback</c>, uma de <see cref="ObjectPoses"/>), o lugar onde o corpo fica (<c>Slot</c>: deslocamento x, z em
/// metros a partir do centro da peça, no referencial dela) e para onde olha. <c>Action</c> (id de <c>ActionType</c>) delega o uso à ação do motor
/// (dormir, comer à mesa) depois de o corpo chegar ao lugar; sem ela, a duração e os efeitos daqui valem. <c>Occupies</c>: só uma pessoa por vez. <c>Hidden</c> (#259): uso só de convite (atividade conjunta
/// da <see cref="SocialJoint"/>): fora da roda do objeto e da escolha de quem cuida de si.</summary>
public sealed record ObjectUseDef(
    string Id, string Name, int DurationMin, IReadOnlyList<UseEffect> Effects,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] IReadOnlyList<UseConsume>? Consumes = null,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] string? Pose = null,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] IReadOnlyList<double>? Slot = null,
    double Facing = 0.0,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] int? Action = null,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] string? UntilFull = null,
    bool Occupies = true,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] string? PoseFallback = null,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)] bool Hidden = false);

/// <summary>#255: uma entrada da fila de ações do corpo conduzido: o uso de um objeto de um prédio (índice da peça na mobília).</summary>
public sealed record QueuedUse(long Building, int Piece, string Use);
