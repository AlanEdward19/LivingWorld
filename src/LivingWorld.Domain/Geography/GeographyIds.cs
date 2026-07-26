namespace LivingWorld.Domain;

/// <summary>Coordenada de célula no grid (task 1). Não é pixel nem posição de render —
/// é índice discreto consultado pelo motor.</summary>
public readonly record struct CellCoord(int X, int Y);

/// <summary>Terreno de uma célula, identificado por id vindo do cenário (task 2). O motor
/// nunca conhece o nome — só o id e o peso de custo associado a ele. <see cref="Unset"/> é o
/// default explícito de uma célula não preenchida, para não virar "planície" por acidente.</summary>
public readonly record struct TerrainType(int Id)
{
    public static readonly TerrainType Unset = new(0);
}

/// <summary>Bioma de uma célula, identificado por id vindo do cenário (task 2).</summary>
public readonly record struct BiomeType(int Id);

/// <summary>Recurso extraível de uma célula, identificado por id vindo do cenário (task 2).</summary>
public readonly record struct ResourceType(int Id);

/// <summary>Identidade de uma região — unidade de agrupamento de células para consulta
/// (task 4).</summary>
public readonly record struct RegionId(int Value);
