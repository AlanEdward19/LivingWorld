using System.Globalization;
using LivingWorld.Domain.Geography;
using LivingWorld.Domain.Shared;
using LivingWorld.Simulation.Core;

namespace LivingWorld.Simulation.Extraordinary.Mechanics;

public sealed record EnvironmentTemperatureAdjustment(RegionId Region, float Delta, long UntilTick);

/// <summary>
/// Clima local: <c>environment.temperature:&lt;região&gt;:&lt;delta&gt;:&lt;duração&gt;</c>
/// ajusta células da região até o tick de expiração, depois o valor volta ao base gerado.
/// </summary>
public sealed class EnvironmentTemperatureMechanic : ExtraordinaryMechanic
{
    public const string TokenPrefix = "environment.temperature:";

    public override string Prefix => "environment.";
    public override ExtraordinaryMechanicKind Kind => ExtraordinaryMechanicKind.Effect;

    public override Result<PreparedMutation?> PrepareEffect(
        ExtraordinaryMechanicContext ctx, string declaration)
    {
        if (!TryParse(declaration, out var region, out float delta, out long durationTicks))
            return Result<PreparedMutation?>.Fail($"Effects: alvo não suportado '{declaration}'");

        if (ctx.World.Map.Regions.All(item => item.Id != region))
            return Result<PreparedMutation?>.Fail($"Effects: alvo não suportado '{declaration}'");

        var world = ctx.World;
        long untilTick = checked(ctx.Tick.CurrentTick + durationTicks);
        return Result<PreparedMutation?>.Ok(new PreparedMutation(declaration, _ =>
            world.AddEnvironmentTemperatureAdjustment(
                new EnvironmentTemperatureAdjustment(region, delta, untilTick))));
    }

    public static float EffectiveTemperature(WorldState world, CellCoord coord, long currentTick) =>
        FieldAt(world, currentTick).At(coord);

    /// <summary>Campo de temperatura do tick: base da célula + soma dos ajustes ativos da região.
    /// Quem varre muitas células no mesmo tick (flora, cultivo) pega o campo uma vez e consulta
    /// <see cref="TemperatureField.At"/> — antes cada consulta varria todos os ajustes sazonais.
    /// A soma por região segue a ordem da lista, então o float é idêntico ao da varredura.</summary>
    internal static TemperatureField FieldAt(WorldState world, long currentTick)
    {
        var field = Fields.GetValue(world, w => new TemperatureField(w));
        field.Refresh(currentTick);
        return field;
    }

    internal sealed class TemperatureField(WorldState world)
    {
        private long _tick = long.MinValue;
        private long _version = -1;
        private long _validFrom = long.MaxValue;
        private long _validUntil = long.MinValue;
        private readonly Dictionary<RegionId, float> _deltas = new();

        /// <summary>Primeiro tick em que a tabela atual deixa de valer (vencimento de um ajuste ativo).</summary>
        internal long ValidUntil => _validUntil;

        /// <summary>A tabela de deltas por região vale enquanto a lista de ajustes não muda e nenhum
        /// ajuste ativo vence — o tick sozinho não a invalida (antes era refeita por tick/região).</summary>
        internal void Refresh(long currentTick)
        {
            _tick = currentTick;
            if (_version == world.TemperatureAdjustmentsVersion && currentTick >= _validFrom && currentTick < _validUntil)
                return;

            _deltas.Clear();
            _version = world.TemperatureAdjustmentsVersion;
            _validFrom = currentTick;
            _validUntil = long.MaxValue;
            // Uma passada, na ordem da lista: a soma por região é a mesma de varrer a lista inteira
            // para cada região (float idêntico), mas custa O(ajustes) em vez de O(regiões × ajustes).
            foreach (var adjustment in world.EnvironmentTemperatureAdjustments)
            {
                if (adjustment.UntilTick <= currentTick)
                    continue;
                _deltas[adjustment.Region] = _deltas.GetValueOrDefault(adjustment.Region) + adjustment.Delta;
                if (adjustment.UntilTick < _validUntil)
                    _validUntil = adjustment.UntilTick;
            }
        }

        internal float At(CellCoord coord)
        {
            if (!world.Map.TryGetBaseClimate(coord, out float baseTemperature, out var region))
                return 0;

            return baseTemperature + _deltas.GetValueOrDefault(region);
        }
    }

    private static readonly System.Runtime.CompilerServices.ConditionalWeakTable<WorldState, TemperatureField> Fields = new();

    internal static bool TryParse(
        string declaration, out RegionId region, out float delta, out long durationTicks)
    {
        region = default;
        delta = 0;
        durationTicks = 0;
        if (!declaration.StartsWith(TokenPrefix, StringComparison.Ordinal))
            return false;

        var parts = declaration[TokenPrefix.Length..].Split(':');
        if (parts.Length != 3)
            return false;
        if (!int.TryParse(parts[0], NumberStyles.Integer, CultureInfo.InvariantCulture, out int regionValue)
            || !parts[0].Equals(regionValue.ToString(CultureInfo.InvariantCulture), StringComparison.Ordinal))
            return false;
        if (!float.TryParse(parts[1], NumberStyles.Float, CultureInfo.InvariantCulture, out delta))
            return false;
        if (!long.TryParse(parts[2], NumberStyles.Integer, CultureInfo.InvariantCulture, out durationTicks)
            || durationTicks <= 0
            || !parts[2].Equals(durationTicks.ToString(CultureInfo.InvariantCulture), StringComparison.Ordinal))
            return false;

        region = new RegionId(regionValue);
        return true;
    }
}
