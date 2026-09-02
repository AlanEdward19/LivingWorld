using System.Text.RegularExpressions;
using LivingWorld.Domain;
using LivingWorld.Domain.Narrative;
using LivingWorld.Domain.Shared;

namespace LivingWorld.Simulation.Narrative;

/// <summary>Reprova <see cref="NarrativeClaim"/>s sem ancoragem e bloqueia nome/número órfão na
/// prosa final (Fase 12, NARR-01..04) — todo texto publicado precisa provar origem em algum
/// evento referenciado por um claim aprovado.</summary>
public static partial class ClaimAnchorValidator
{
    /// <summary>Um claim reprovado e o motivo (NARR-02 exige registro da falha de ancoragem).</summary>
    public sealed record AnchorFailure(NarrativeClaim Claim, string Reason);

    public sealed record ValidationOutcome(
        IReadOnlyList<NarrativeClaim> Approved, IReadOnlyList<AnchorFailure> Rejected);

    /// <summary>NARR-02: descarta claims sem <see cref="NarrativeClaim.EventIds"/> não vazio,
    /// registrando o motivo de cada reprovação. Claims aprovados preservam a ordem de entrada.</summary>
    public static ValidationOutcome ValidateClaims(IEnumerable<NarrativeClaim> claims)
    {
        var approved = new List<NarrativeClaim>();
        var rejected = new List<AnchorFailure>();
        foreach (var claim in claims)
        {
            if (claim.EventIds.Count > 0)
                approved.Add(claim);
            else
                rejected.Add(new AnchorFailure(claim, "claim sem eventIds válidos: ancoragem ausente"));
        }
        return new ValidationOutcome(approved, rejected);
    }

    /// <summary>NARR-03/NARR-04: todo numeral e todo nome próprio (palavra capitalizada fora do
    /// início de frase) presente em <paramref name="prose"/> precisa aparecer no texto de algum
    /// claim aprovado — a única fonte de conteúdo permitida. Retorna a primeira ocorrência órfã
    /// encontrada, se houver.</summary>
    public static Result<Unit> ValidateProse(string prose, IReadOnlyList<NarrativeClaim> approvedClaims)
    {
        string evidence = string.Join(" ", approvedClaims.Select(c => c.Text));
        foreach (var token in ExtractCitableTokens(prose))
        {
            if (!evidence.Contains(token, StringComparison.Ordinal))
                return Result<Unit>.Fail($"nome/número órfão no texto final: '{token}'");
        }
        return Result<Unit>.Ok(Unit.Value);
    }

    /// <summary>Numerais (qualquer sequência de dígitos) e nomes próprios candidatos (palavra
    /// capitalizada que não abre a frase, para não confundir com maiúscula gramatical).</summary>
    internal static IEnumerable<string> ExtractCitableTokens(string prose)
    {
        foreach (Match m in NumeralPattern().Matches(prose))
            yield return m.Value;

        foreach (var sentence in SentenceSplitPattern().Split(prose))
        {
            var words = WordPattern().Matches(sentence);
            bool first = true;
            foreach (Match word in words)
            {
                if (!first && ProperNounPattern().IsMatch(word.Value))
                    yield return word.Value;
                first = false;
            }
        }
    }

    [GeneratedRegex(@"\d+")]
    private static partial Regex NumeralPattern();

    [GeneratedRegex(@"(?<=[.!?])\s+")]
    private static partial Regex SentenceSplitPattern();

    [GeneratedRegex(@"[A-Za-zÀ-ÿ]+")]
    private static partial Regex WordPattern();

    [GeneratedRegex(@"^[A-ZÀ-Ý][a-zà-ÿ]+$")]
    private static partial Regex ProperNounPattern();
}
