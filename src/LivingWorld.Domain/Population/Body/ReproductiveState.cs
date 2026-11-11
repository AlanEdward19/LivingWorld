namespace LivingWorld.Domain.Population.Body;

/// <summary>Gravidez em curso (AD-130): concebida em <see cref="ConceivedTick"/>, termina em <see cref="EndTick"/> (parto ou
/// perda). O sexo de cada feto (<c>F</c>/<c>M</c>, um caractere por bebê; dois = gêmeos) é decidido na concepção, pelo
/// cromossomo do pai. <see cref="EndsInLoss"/> = aborto espontâneo no <see cref="EndTick"/>.</summary>
public sealed record PregnancyState(
    long ConceivedTick, long EndTick, long FatherId, string Sexes, int GestationDays, bool EndsInLoss);

/// <summary>Histórico reprodutivo da mulher: partos vivos ou não (<see cref="Parity"/>), última concepção e último parto, fim do
/// período infecundo (pós-parto ou pós-perda) e a gravidez em curso. Nulo no NPC que nunca concebeu. Ganchos para a fatia de
/// ciclo e puberdade: datas da última concepção e do último parto.</summary>
public sealed record ReproductiveState(
    int Parity = 0, long? LastConceptionTick = null, long? LastBirthTick = null, long InfecundUntilTick = 0,
    PregnancyState? Pregnancy = null);
