using LivingWorld.Domain;

namespace LivingWorld.Api.Visual;

/// <summary>Fronteira pública do log: converte kinds canônicos em linguagem segura e nunca
/// encaminha o payload técnico, que pode conter ids, causas ou texto de verdade histórica.</summary>
public static class LivingEventPresentationCatalog
{
    private static readonly IReadOnlyDictionary<WorldEventKind, string> Labels =
        new Dictionary<WorldEventKind, string>
        {
            [WorldEventKind.Birth] = "Um novo habitante nasceu",
            [WorldEventKind.Death] = "Um habitante faleceu",
            [WorldEventKind.Starvation] = "A fome causou uma morte",
            [WorldEventKind.Hired] = "Um habitante começou um novo trabalho",
            [WorldEventKind.Fired] = "Um vínculo de trabalho terminou",
            [WorldEventKind.WageUnpaid] = "Um pagamento de salário ficou pendente",
            [WorldEventKind.ResourceLost] = "Recursos foram perdidos",
            [WorldEventKind.Minted] = "Novas moedas entraram em circulação",
            [WorldEventKind.Destroyed] = "Moedas saíram de circulação",
            [WorldEventKind.Marriage] = "Um casamento foi celebrado",
            [WorldEventKind.CourtshipStarted] = "Um cortejo começou",
            [WorldEventKind.CourtshipRejected] = "Um cortejo não foi correspondido",
            [WorldEventKind.CourtshipSucceeded] = "Um cortejo foi correspondido",
            [WorldEventKind.MaternalDeath] = "Uma mãe faleceu durante o parto",
            [WorldEventKind.StillBirth] = "Uma gestação terminou sem nascimento vivo",
            [WorldEventKind.FactRecorded] = "Um acontecimento entrou para a história",
            [WorldEventKind.ReportConverted] = "Um relato começou a circular",
            [WorldEventKind.BookLost] = "Um livro foi perdido",
            [WorldEventKind.BookRediscovered] = "Um livro perdido foi reencontrado",
            [WorldEventKind.CompensatingCorrection] = "Uma versão da história foi corrigida",
            [WorldEventKind.SettlementFounded] = "Um novo assentamento foi fundado",
            [WorldEventKind.CityMerged] = "Dois assentamentos adjacentes se unificaram",
            [WorldEventKind.ExtraordinaryUseAttempted] = "Uma capacidade extraordinária foi acionada",
            [WorldEventKind.ExtraordinaryCostPaid] = "O uso extraordinário cobrou seu custo",
            [WorldEventKind.ExtraordinaryEffectApplied] = "Um efeito extraordinário alterou o mundo",
            [WorldEventKind.ExtraordinaryUseFailed] = "Um uso extraordinário falhou",
            [WorldEventKind.ExtraordinaryAcquired] = "Um habitante adquiriu uma capacidade extraordinária",
            [WorldEventKind.ExtraordinaryAcquisitionFailed] = "Uma aquisição extraordinária não ocorreu",
            [WorldEventKind.ExtraordinaryManifested] = "Uma manifestação extraordinária surgiu",
            [WorldEventKind.ExtraordinaryDormant] = "Uma manifestação extraordinária ficou dormente",
            [WorldEventKind.ExtraordinaryCulturalReaction] = "Uma cultura reagiu ao extraordinário",
            [WorldEventKind.ExtraordinaryConstructCreated] = "Um construto extraordinário surgiu",
            [WorldEventKind.ExtraordinaryConstructDamaged] = "Um construto extraordinário foi danificado",
            [WorldEventKind.ExtraordinaryConstructRemoved] = "Um construto extraordinário se desfez",
            [WorldEventKind.ExtraordinaryRevoked] = "Uma capacidade extraordinária foi revogada",
            [WorldEventKind.AuthoringCommandApplied] = "O operador interveio no mundo",
            [WorldEventKind.AuthoringCommandRejected] = "Uma intervenção do operador foi recusada",
            [WorldEventKind.ExtraordinaryFailureApplied] = "Uma capacidade extraordinária produziu uma consequência adversa",
            [WorldEventKind.CombatResolved] = "Dois habitantes travaram um confronto",
            [WorldEventKind.NpcInstantiated] = "Uma nova pessoa surgiu por capacidade extraordinária",
            [WorldEventKind.IdentityChanged] = "A identidade observável de um habitante mudou",
        };

    public static string Describe(WorldEventKind kind) =>
        Labels.GetValueOrDefault(kind, "Um acontecimento foi registrado");

    public static IReadOnlyCollection<WorldEventKind> MappedKinds { get; } = [.. Labels.Keys];
}
