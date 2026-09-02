using LivingWorld.Domain.Geography;
using LivingWorld.Domain.Population.Family;
using LivingWorld.Domain.Shared;

namespace LivingWorld.Domain.Economy;

/// <summary>Clamp de capacidade + perda reportada (Fase 5, T18) fatorado de
/// <see cref="Workplace"/> (T4) pra ser reusado por <see cref="Household"/> — os dois guardam
/// estoque como <c>Dictionary&lt;ResourceType, long&gt;</c> e precisam da mesma garantia:
/// <see cref="Deposit"/> nunca excede a capacidade declarada (excedente é perda **registrada**,
/// devolvida ao chamador — nunca sumiço silencioso, ECON-02); <see cref="Withdraw"/> nunca deixa
/// o estoque negativo.</summary>
public static class ResourceStock
{
    public static long Deposit(Dictionary<ResourceType, long> stock, ResourceType resource, long amount, long capacity)
    {
        long current = stock.GetValueOrDefault(resource);
        long total = current + amount;
        long accepted = Math.Min(total, capacity);

        stock[resource] = accepted;
        return total - accepted;
    }

    public static Result<long> Withdraw(Dictionary<ResourceType, long> stock, ResourceType resource, long amount)
    {
        long current = stock.GetValueOrDefault(resource);
        if (amount > current)
            return Result<long>.Fail($"Stock[{resource}]: insuficiente");

        stock[resource] = current - amount;
        return Result<long>.Ok(amount);
    }
}
