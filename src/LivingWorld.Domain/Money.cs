namespace LivingWorld.Domain;

/// <summary>Grandeza monetária inteira (unidades, não centavos flutuantes). Nunca negativa.</summary>
public readonly record struct Money
{
    public long Amount { get; }

    public Money(long amount)
    {
        if (amount < 0)
            throw new ArgumentOutOfRangeException(nameof(amount), amount, "Money não pode ser negativo.");
        Amount = amount;
    }

    public static Money Zero => new(0);

    public static Money operator +(Money a, Money b) => new(a.Amount + b.Amount);

    /// <summary>Débito além do saldo retorna Failure e deixa o valor original intacto.</summary>
    public Result<Money> TryDebit(Money amount)
    {
        if (amount.Amount > Amount)
            return Result<Money>.Fail("insufficient_funds");
        return Result<Money>.Ok(new Money(Amount - amount.Amount));
    }

    public override string ToString() => Amount.ToString();
}
