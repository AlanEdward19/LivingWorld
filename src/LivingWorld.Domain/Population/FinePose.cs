namespace LivingWorld.Domain.Population;

/// <summary>AD-197k: posição contínua do corpo conduzido, em milésimos de célula (absolutos na grade do mundo), o rumo em que anda
/// (milésimos, −1000..1000 por eixo; 0, 0 = parado) e o instante (ms de mundo) desta posição. AD-197u: <paramref name="Run"/> diz que
/// o corpo corre nesse rumo (só com fôlego e saúde; omitido enquanto falso). #248: <paramref name="Seq"/> é o número do último comando de
/// andar que o motor aplicou (o cliente reaplica o que passa disso), <paramref name="CmdMs"/> o instante dele (o corpo para se o cliente
/// some) e <paramref name="PumpMs"/> o instante do próximo passo agendado do motor (um só por corpo; omitidos enquanto zero).</summary>
public sealed record FinePose(long X, long Y, int HeadingX, int HeadingY, long AtMs,
    [property: System.Text.Json.Serialization.JsonIgnore(Condition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingDefault)] bool Run = false,
    [property: System.Text.Json.Serialization.JsonIgnore(Condition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingDefault)] int Seq = 0,
    [property: System.Text.Json.Serialization.JsonIgnore(Condition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingDefault)] long CmdMs = 0,
    [property: System.Text.Json.Serialization.JsonIgnore(Condition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingDefault)] long PumpMs = 0)
{
    public const long PerCell = 1000;

    public int CellX => (int)Math.Floor(X / (double)PerCell);

    public int CellY => (int)Math.Floor(Y / (double)PerCell);
}
