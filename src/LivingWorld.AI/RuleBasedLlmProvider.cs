using System.Globalization;
using System.Text;
using LivingWorld.Domain.Llm;

namespace LivingWorld.AI;

/// <summary>#237: o provider de "jogo sem LLM". Monta a fala do NPC por regra, só a partir do que o motor já sabe
/// (<see cref="NpcDialogueState"/>: necessidade mais urgente, saúde, afeto por quem fala, temperamento; crenças do próprio
/// NPC) e da intenção da fala do jogador (saudação, novidade, pedido de ajuda, oferta, despedida). Determinístico (sem
/// relógio nem Random: a variação vem de um hash estável do contexto), nunca ecoa o jogador, nunca chama rede e não afirma
/// nada que o motor não saiba (rules/llm-boundary.md). Quem tem uma LLM de verdade usa <c>LLM_PROVIDER=ollama</c>.</summary>
public sealed class RuleBasedLlmProvider : ILlmProvider
{
    public Task<LlmResponse> GetResponseAsync(LlmContext context, CancellationToken cancellationToken = default)
    {
        var kind = Classify(context.PlayerUtterance);
        var state = context.State;
        ulong variety = StableHash(context.NpcKnowledgeSummary + context.PlayerUtterance + (context.RelevantMemories?.Count ?? 0) + context.SessionId);

        bool warm = state is not null && (state.Affection >= 40 || (state.Agreeableness >= 70 && (state.Affection is null || state.Affection >= 10)));
        string? need = state?.UrgentNeed;
        bool unwell = state is not null && state.Health < 40;

        string dialogue = kind switch
        {
            Kind.Farewell => Pick(variety, warm
                ? ["Até logo! Passe aqui de novo.", "Vá com cuidado, e volte para conversarmos."]
                : ["Passe bem.", "Até mais, e cuide-se.", "Vá em paz."]),
            Kind.Offer => OfferReply(variety, need, unwell, warm),
            Kind.Help => HelpReply(variety, need, unwell, warm, state?.Altruism ?? 50),
            Kind.News => NewsReply(variety, need, unwell, context.BeliefFacts),
            Kind.Greeting => GreetingReply(variety, need, unwell, warm),
            _ => Pick(variety, ["Hum... não sei bem o que dizer a respeito.", "Não entendi direito. Pode falar de outro jeito?"]),
        };

        string emotion = unwell || need is "hunger" or "thirst" ? "concerned"
            : need == "sleep" ? "annoyed"
            : warm ? "friendly"
            : kind == Kind.News ? "curious"
            : "neutral";
        string intent = Choose(context.AllowedIntents, kind switch
        {
            Kind.Greeting => "greet",
            Kind.Farewell => "farewell",
            _ => "ask",
        });

        return Task.FromResult(new LlmResponse(dialogue, emotion, intent, [], []));
    }

    private enum Kind { Other, Greeting, News, Help, Offer, Farewell }

    private static Kind Classify(string utterance)
    {
        string text = Normalize(utterance);
        if (HasAny(text, "ate logo", "tchau", "adeus", "ate mais", "ate a proxima", "preciso ir")) return Kind.Farewell;
        if (HasAny(text, "posso ajudar", "ajudar em algo", "precisa de ajuda", "quer ajuda", "posso fazer algo")) return Kind.Offer;
        if (HasAny(text, "preciso de ajuda", "me ajude", "me ajuda", "socorro", "ajuda")) return Kind.Help;
        if (HasAny(text, "novidade", "noticia", "o que ha de novo", "o que aconteceu", "como andam as coisas")) return Kind.News;
        if (HasAny(text, "bom dia", "boa tarde", "boa noite", "ola", "oi", "como vai", "tudo bem", "salve")) return Kind.Greeting;
        return Kind.Other;
    }

    private static string GreetingReply(ulong v, string? need, bool unwell, bool warm)
    {
        string hello = Pick(v, warm
            ? ["Bom dia! Que bom te ver por aqui.", "Olá! Fico contente em te encontrar."]
            : ["Bom dia.", "Olá.", "Bom dia. Em que posso ajudar?"]);
        return hello + " " + (NeedLine(need) ?? (unwell ? "Não ando me sentindo bem, mas vou levando." : Pick(v >> 7, warm
            ? ["Por aqui está tudo em ordem.", "Vou bem, obrigado por perguntar."]
            : ["Vou levando.", "Tudo em ordem."])));
    }

    private static string NewsReply(ulong v, string? need, bool unwell, IReadOnlyList<string>? beliefs)
    {
        if (beliefs is { Count: > 0 })
            return "Corre a notícia: " + Sentence(beliefs[(int)(v % (ulong)beliefs.Count)]);
        return Pick(v, ["Nada de novo que eu tenha ouvido.", "Por aqui, o dia segue como sempre."])
            + (NeedLine(need) is { } line ? " " + line : unwell ? " Só que ando meio doente." : "");
    }

    private static string HelpReply(ulong v, string? need, bool unwell, bool warm, int altruism)
    {
        if (need is not null || unwell)
            return (unwell ? "Eu mesmo não ando bem" : "Eu mesmo ando apertado") + ", mas conte o que houve."
                + (NeedLine(need) is { } line ? " " + line : "");
        return warm || altruism >= 60
            ? Pick(v, ["Claro, conte comigo. O que você precisa?", "Pode falar, vou ver o que posso fazer."])
            : Pick(v, ["Depende. O que você precisa?", "Não prometo nada, mas diga o que houve."]);
    }

    private static string OfferReply(ulong v, string? need, bool unwell, bool warm) => need switch
    {
        "hunger" => "Aceito, sim. Estou com fome, se tiver algo de comer eu agradeço.",
        "thirst" => "Aceito, sim. Estou com sede, um pouco de água ajudaria.",
        "sleep" => "Agradeço, mas o que eu queria mesmo era dormir um pouco.",
        "social" => "Agradeço! Na verdade, só a companhia já ajuda.",
        _ when unwell => "Agradeço. Não ando bem, qualquer ajuda vale.",
        _ => warm
            ? Pick(v, ["Que gentileza! Por ora estou bem, mas lembro de você.", "Obrigado por perguntar. Por enquanto dou conta."])
            : Pick(v, ["Obrigado, mas por ora dou conta.", "Agradeço. Se precisar, eu aviso."]),
    };

    /// <summary>A fala da necessidade que aperta, ou null se nenhuma.</summary>
    private static string? NeedLine(string? need) => need switch
    {
        "hunger" => "Estou com tanta fome que mal consigo pensar.",
        "thirst" => "Estou com muita sede.",
        "sleep" => "Estou morrendo de sono.",
        "social" => "Andava mesmo precisando de companhia.",
        _ => null,
    };

    private static string Choose(IReadOnlyList<string> allowed, string wanted) =>
        allowed.Count == 0 || allowed.Contains(wanted) ? wanted : allowed[0];

    private static string Pick(ulong v, string[] options) => options[(int)(v % (ulong)options.Length)];

    private static string Sentence(string fact)
    {
        string text = fact.Trim();
        if (text.Length == 0) return "...";
        text = char.ToUpperInvariant(text[0]) + text[1..];
        return text.EndsWith('.') || text.EndsWith('!') || text.EndsWith('?') ? text : text + ".";
    }

    private static bool HasAny(string text, params string[] needles)
    {
        foreach (var needle in needles)
        {
            int at = text.IndexOf(needle, StringComparison.Ordinal);
            // Palavras curtas (oi, ola) só valem inteiras: "oi" não pode casar com "foice".
            if (at >= 0 && (needle.Length > 3 || WholeWord(text, at, needle.Length))) return true;
        }
        return false;
    }

    private static bool WholeWord(string text, int at, int length) =>
        (at == 0 || !char.IsLetter(text[at - 1])) && (at + length >= text.Length || !char.IsLetter(text[at + length]));

    private static string Normalize(string value)
    {
        var decomposed = value.ToLowerInvariant().Normalize(NormalizationForm.FormD);
        var sb = new StringBuilder(decomposed.Length);
        foreach (var ch in decomposed)
            if (CharUnicodeInfo.GetUnicodeCategory(ch) != UnicodeCategory.NonSpacingMark) sb.Append(ch);
        return sb.ToString();
    }

    private static ulong StableHash(string value)
    {
        ulong hash = 14695981039346656037UL;
        foreach (var ch in value) { hash ^= ch; hash *= 1099511628211UL; }
        return hash;
    }
}
