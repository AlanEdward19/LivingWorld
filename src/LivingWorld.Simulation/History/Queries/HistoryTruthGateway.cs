using System.Security.Cryptography;
using System.Text;
using LivingWorld.Domain.Shared;
using LivingWorld.Simulation.Core;

namespace LivingWorld.Simulation.History.Queries;

/// <summary>Esqueleto Tier A de um fato, como a consulta de verdade o expõe (sem distorção).</summary>
public sealed record TruthFactView(
    long Id, long Tick, string Kind, IReadOnlyList<long> Participants, Guid? Location, double Significance, string Payload);

/// <summary>Porta única da consulta de verdade para a borda HTTP: só responde com um
/// <see cref="EngineScope"/> válido. A API nunca referencia <see cref="HistoryTruthQuery"/> direto
/// (guarda <c>HistoryQuerySeparationGuard</c>); o jogo lê crença, só o motor/autor lê o fato.</summary>
public static class HistoryTruthGateway
{
    public const string ForbiddenError = "forbidden: consulta de verdade exige escopo de motor";

    public static Result<IReadOnlyList<TruthFactView>> Skeleton(WorldState world, EngineScope? scope, long fromTick)
    {
        if (scope is null)
            return Result<IReadOnlyList<TruthFactView>>.Fail(ForbiddenError);

        var facts = new List<TruthFactView>();
        foreach (var fact in world.Facts.OrderBy(f => f.Id.Value))
        {
            if (fact.Tick < fromTick) continue;
            var found = HistoryTruthQuery.GetFact(world, fact.Id);
            if (!found.IsSuccess) continue;
            var f = found.Value!;
            facts.Add(new TruthFactView(
                f.Id.Value, f.Tick, f.Kind.ToString(), f.Participants.Select(p => p.Value).ToList(),
                f.Location?.Value, f.Significance, f.Payload));
        }
        return Result<IReadOnlyList<TruthFactView>>.Ok(facts);
    }
}

/// <summary>Credencial de motor: só existe se o token apresentado bate com o configurado.
/// Sem token configurado, nenhum escopo é concedido.</summary>
public sealed class EngineScope
{
    private EngineScope() { }

    public static EngineScope? TryGrant(string? presented, string? configured)
    {
        if (string.IsNullOrEmpty(presented) || string.IsNullOrEmpty(configured)) return null;
        bool equal = CryptographicOperations.FixedTimeEquals(
            Encoding.UTF8.GetBytes(presented), Encoding.UTF8.GetBytes(configured));
        return equal ? new EngineScope() : null;
    }
}
