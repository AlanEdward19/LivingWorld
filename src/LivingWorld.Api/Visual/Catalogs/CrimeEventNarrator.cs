using LivingWorld.Domain.History;
using LivingWorld.Domain.Shared;
using LivingWorld.Simulation.Core;

namespace LivingWorld.Api.Visual.Catalogs;

/// <summary>Crime, violência e justiça no cliente como acontecem (AD-150; o dono pediu "totalmente explícito"): autor, vítima,
/// motivo, bem, desfecho e punição, com o nome das pessoas. O observador é onisciente: o furto que ninguém viu aparece como
/// tal, e o julgamento diz se o condenado era mesmo o culpado. Nulo para os outros eventos (ficam com o rótulo genérico).</summary>
public static class CrimeEventNarrator
{
    public static string? Describe(WorldState world, WorldEvent evt)
    {
        var f = evt.Payload.Split('|');
        string At(int i) => i < f.Length ? f[i] : "";
        string Who(int i) => long.TryParse(At(i), out long id) && id >= 0
            ? world.FindNpc(new NpcId(id))?.Name ?? $"#{id}" : "a cidade";
        return evt.Kind switch
        {
            WorldEventKind.TheftCommitted => Theft(At, Who),
            WorldEventKind.Assault =>
                $"{Who(0)} agrediu {Who(1)} ({Motive(At(2))}): {(At(4) == "morto" ? "morreu" : $"ferido, perdeu {At(3)} de saúde")}; {Seen(At(5))}",
            WorldEventKind.CrimeReported =>
                $"{Who(0)} denunciou {Who(1)} por {At(3)}{(At(4) == "0" ? " (acusação falsa: não foi ele)" : "")}",
            WorldEventKind.Verdict => Verdict(At, Who),
            WorldEventKind.Banished => At(3) == "fuga"
                ? $"{Who(0)} fugiu da vila antes do julgamento por {At(2)}"
                : $"{Who(0)} foi banido da vila por {At(2)}",
            WorldEventKind.Executed => $"{Who(0)} foi enforcado por sentença",
            WorldEventKind.Homicide => $"{Who(0)} foi morto numa agressão",
            WorldEventKind.InheritanceDisputed => $"{Who(0)} e {Who(1)} brigaram pela herança de {Who(2)} ({At(3)} moedas)",
            WorldEventKind.TheftDiscovered => $"{Who(1)} soube que {Who(0)} o furtou",
            WorldEventKind.DomesticViolence =>
                $"{Who(0)} agrediu em casa {Bond(At(2))} {Who(1)} ({Motive(At(3))}): {(At(5) == "morto" ? "morreu" : $"ferido, perdeu {At(4)} de saúde")}",
            WorldEventKind.Kidnapped => $"{Who(0)} sequestrou {Who(1)} ({Motive(At(2))}){(At(4) is "" or "0" ? "" : $", pediu {At(4)} moedas de resgate")}; {Seen(At(5))}",
            WorldEventKind.KidnappingEnded => Freed(At, Who),
            // limite da fatia (AD-154): só o nome do crime, autor e vítima; nenhum detalhe do ato
            WorldEventKind.SexualAssault => At(2) == "crianca"
                ? $"{Who(0)} cometeu estupro de vulnerável contra a criança {Who(1)}"
                : $"{Who(0)} estuprou {Who(1)}",
            _ => null,
        };
    }

    private static string Theft(Func<int, string> at, Func<int, string> who)
    {
        string from = at(2) switch
        {
            "pessoa" => $"de {who(1)}",
            "casa" => $"da casa de {who(1)}",
            "banca" => $"da oficina de {who(1)}",
            _ => $"da caixa da oficina de {who(1)}",
        };
        string what = at(4) == "moeda" ? $"{at(5)} moedas" : $"{at(5)} de {at(4)}";
        return at(7) == "levou"
            ? $"{who(0)} furtou {what} {from} ({Motive(at(3))}, valor {at(6)}); {Seen(at(8))}"
            : $"{who(0)} tentou furtar {from} ({Motive(at(3))}) e falhou; {Seen(at(8))}";
    }

    private static string Verdict(Func<int, string> at, Func<int, string> who)
    {
        string truth = at(7) == "1" ? "" : " — inocente de verdade";
        if (at(4) != "culpado") return $"{who(0)} foi julgado por {at(3)} e inocentado{(at(7) == "1" ? " — mas tinha sido ele" : "")}";
        string sentence = at(5) switch
        {
            "multa" => $"multa e restituição de {at(6)} moedas",
            "multa_castigo" => $"pagou {at(6)} moedas e foi ao pelourinho",
            "castigo" => "açoite no pelourinho (não tinha como pagar)",
            "banimento" => "banimento",
            "forca" => "forca",
            _ => at(5),
        };
        return $"{who(0)} foi condenado por {at(3)}: {sentence}{truth}";
    }

    private static string Bond(string bond) => bond switch
    {
        "conjuge" => "o cônjuge",
        "filho" => "o filho",
        "pai" => "o pai ou a mãe",
        _ => "outro morador,",
    };

    private static string Freed(Func<int, string> at, Func<int, string> who) => at(2) switch
    {
        "resgate" => $"{who(0)} voltou para casa depois de {at(3)} dias: a família pagou {at(4)} moedas a {who(1)}",
        "fuga" => $"{who(0)} fugiu do cativeiro de {who(1)} depois de {at(3)} dias",
        "achada" => $"{who(0)} foi achado pela família no cativeiro de {who(1)} depois de {at(3)} dias",
        "morte" => $"{who(0)} morreu no cativeiro de {who(1)}",
        "autor_saiu" => $"{who(0)} saiu do cativeiro: {who(1)} não está mais na vila",
        _ => $"{who(1)} soltou {who(0)} depois de {at(3)} dias",
    };

    private static string Motive(string motive) => motive switch
    {
        "temperamento" => "por temperamento",
        "desemprego" => "sem trabalho",
        "resgate" => "por resgate",
        "posse" => "para ficar com a pessoa",
        "sequestro" => "por ter sido sequestrado",
        "violencia_sexual" => "por ter sido estuprado",
        "fome" => "por fome",
        "miseria" => "por miséria",
        "oportunidade" => "por oportunidade",
        "ofensa" => "por ofensa",
        "ciume" => "por ciúme",
        "traicao" => "por traição",
        "heranca" => "por herança",
        "denuncia" => "por ter sido denunciado",
        "vinganca" => "por vingança",
        "flagrante" => "pego em flagrante",
        "agressao" => "por ter apanhado antes",
        "furto" => "por ter sido furtado",
        _ => motive,
    };

    private static string Seen(string seers) => seers is "0" or "" ? "ninguém viu" : $"{seers} viram";
}
