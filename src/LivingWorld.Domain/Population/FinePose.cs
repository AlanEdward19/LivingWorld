namespace LivingWorld.Domain.Population;

/// <summary>AD-197k: posição contínua do corpo conduzido, em milésimos de célula (absolutos na grade do mundo), o rumo em que anda
/// (milésimos, −1000..1000 por eixo; 0, 0 = parado) e o instante (ms de mundo) desta posição.</summary>
public sealed record FinePose(long X, long Y, int HeadingX, int HeadingY, long AtMs)
{
    public const long PerCell = 1000;

    public int CellX => (int)Math.Floor(X / (double)PerCell);

    public int CellY => (int)Math.Floor(Y / (double)PerCell);
}
