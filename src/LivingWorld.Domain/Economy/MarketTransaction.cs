using LivingWorld.Domain.Geography;
using LivingWorld.Domain.Shared;

namespace LivingWorld.Domain.Economy;

/// <summary>Estado imutável de uma transação de mercado em andamento — todo campo é struct
/// imutável, então "aplicar sobre uma cópia" é literalmente construir um <see
/// cref="TransactionContext"/> novo a cada passo (ECON-09/10/11/12/13). Nenhuma referência a
/// <c>Npc</c>/<c>Workplace</c> real é tocada até o commit final feito pelo chamador.</summary>
public sealed record TransactionContext(
    Money BuyerWallet,
    Money SellerWallet,
    long SellerStock,
    long BuyerStock,
    ResourceType Resource,
    long UnitPrice,
    long Quantity);

/// <summary>Um passo nomeado da transação — nome aparece na falha de fault-injection (ECON-12),
/// nunca um índice cru.</summary>
public sealed record TransactionStep(string Name, Func<TransactionContext, Result<TransactionContext>> Apply);

/// <summary>Transação atômica dinheiro↔recurso: compõe passos sobre um <see
/// cref="TransactionContext"/> imutável e só expõe um resultado pronto pra commit depois que
/// todos os passos tiverem sucedido — ou nenhum efeito, ou todos (ECON-09/10/11/12/13).</summary>
public static class MarketTransaction
{
    public static readonly IReadOnlyList<TransactionStep> Steps = new[]
    {
        new TransactionStep("Debitar Money do comprador", ctx =>
        {
            var debited = ctx.BuyerWallet.TryDebit(new Money(ctx.UnitPrice * ctx.Quantity));
            return debited.IsSuccess
                ? Result<TransactionContext>.Ok(ctx with { BuyerWallet = debited.Value })
                : Result<TransactionContext>.Fail(debited.Error!);
        }),
        new TransactionStep("Debitar recurso do estoque do vendedor", ctx =>
        {
            if (ctx.SellerStock < ctx.Quantity)
                return Result<TransactionContext>.Fail("insufficient_stock");
            return Result<TransactionContext>.Ok(ctx with { SellerStock = ctx.SellerStock - ctx.Quantity });
        }),
        new TransactionStep("Creditar Money ao vendedor", ctx =>
            Result<TransactionContext>.Ok(ctx with { SellerWallet = ctx.SellerWallet + new Money(ctx.UnitPrice * ctx.Quantity) })),
        new TransactionStep("Creditar recurso ao estoque do comprador", ctx =>
            Result<TransactionContext>.Ok(ctx with { BuyerStock = ctx.BuyerStock + ctx.Quantity })),
    };

    /// <summary>Aplica <see cref="Steps"/> em ordem sobre <paramref name="ctx"/>. Se <paramref
    /// name="failAtStep"/> (1-based) for informado, força <c>Result.Fail</c> nesse passo antes
    /// de aplicá-lo — hook de teste pra fault-injection (ECON-12/13), nunca usado em
    /// produção.</summary>
    public static Result<TransactionContext> Execute(TransactionContext ctx, int? failAtStep = null)
    {
        for (var i = 0; i < Steps.Count; i++)
        {
            if (failAtStep == i + 1)
                return Result<TransactionContext>.Fail($"fault_injected_at_step_{i + 1}_{Steps[i].Name}");

            var result = Steps[i].Apply(ctx);
            if (!result.IsSuccess)
                return Result<TransactionContext>.Fail(result.Error!);
            ctx = result.Value!;
        }

        return Result<TransactionContext>.Ok(ctx);
    }
}
