namespace LivingWorld.Domain.Population.Skills;

/// <summary>O que o temperamento e o corpo favorecem num ofício (AD-136): peso de cada um dos 10 traços (na ordem de <see
/// cref="Personality"/>; negativo = o ofício pede o oposto) e da massa muscular. Os pesos são normalizados no uso.</summary>
public sealed record ProfessionProfile(IReadOnlyList<double> TraitWeights, double MuscleWeight);

/// <summary>Como o ofício nasce (AD-136). Ninguém nasce com ofício: na juventude (<see
/// cref="SkillLearningRules.ApprenticeAgeYears"/>) a pessoa escolhe entre os ofícios do cenário pela inclinação (gosto
/// próprio + temperamento e corpo, <see cref="ProfessionProfile"/>), pela exposição (o que já aprendeu em casa), pela
/// oportunidade (vaga que paga) e pela pressão de quem a criou. A pressão é a norma da época (<see cref="ParentalPressure"/>:
/// alta numa vila medieval, baixa num cenário moderno) vezes o quanto quem cria é tradicional e autoritário e tem negócio
/// próprio, e só pesa no tanto em que o filho é obediente. Quem trabalha no que combina com ele rende e aprende mais e
/// valoriza mais o emprego; quem foi empurrado para o que não combina rende menos e troca quando surge vaga.</summary>
public sealed record VocationRules
{
    /// <summary>Força da casa na escolha, norma da época, em [0,1].</summary>
    public double ParentalPressure { get; init; } = 0.7;

    /// <summary>Peso, em desvios-padrão de utilidade, da pressão plena (força 1 sobre filho obediente 1).</summary>
    public double PressureScale { get; init; } = 2.5;

    /// <summary>Multiplicador da força quando quem cria tem negócio ou terra próprios (é dono de um local de trabalho).</summary>
    public double OwnerPressureBoost { get; init; } = 1.4;

    /// <summary>Herdabilidade do gosto por ofício (regressão do filho na média dos pais), em [0,1].</summary>
    public double TasteHeritability { get; init; } = 0.35;

    /// <summary>Peso do temperamento e do corpo na inclinação, relativo ao gosto próprio (peso 1).</summary>
    public double TraitWeight { get; init; } = 0.7;

    /// <summary>Peso da exposição: utilidade por fração do teto de habilidade que a pessoa já tem no ofício.</summary>
    public double ExposureWeight { get; init; } = 4.0;

    /// <summary>Peso da oportunidade: utilidade do ofício com a vaga que mais vale na cidade, relativa à melhor de todas.</summary>
    public double OpportunityWeight { get; init; } = 1.0;

    /// <summary>Acaso da escolha (desvio-padrão de utilidade).</summary>
    public double ChoiceNoise { get; init; } = 0.8;

    /// <summary>Produção a mais (ou a menos) por desvio-padrão de aptidão para o ofício exercido; cortado em ±2 dp.</summary>
    public double FitOutput { get; init; } = 0.05;

    /// <summary>Ganho de habilidade a mais (ou a menos) por desvio-padrão de aptidão.</summary>
    public double FitLearning { get; init; } = 0.12;

    /// <summary>Valor pessoal do emprego a mais (ou a menos) por desvio-padrão de aptidão para o ofício dele.</summary>
    public double FitJobValue { get; init; } = 0.15;

    /// <summary>Perfil de cada ofício por id; ofício sem perfil depende só do gosto próprio.</summary>
    public IReadOnlyDictionary<int, ProfessionProfile> Profiles { get; init; } = MedievalProfiles;

    public bool Enabled { get; init; } = true;

    /// <summary>Lavrador (1): constância, aversão a risco e força; ferreiro (2): força, abertura e ambição, menos aversão a risco.</summary>
    public static IReadOnlyDictionary<int, ProfessionProfile> MedievalProfiles { get; } = new Dictionary<int, ProfessionProfile>
    {
        [1] = new([0, 0, 1.0, 0.5, -0.5, 0, 0, 0, -0.5, 1.0], 0.8),
        [2] = new([0, 0, 0.5, 0, 0.8, 0.8, 0, 0, 0, -0.5], 1.2),
    };

    /// <summary>Vila medieval: a maioria segue o ofício da casa, uma minoria relevante não.</summary>
    public static VocationRules Medieval { get; } = new();

    /// <summary>Cenário moderno: a casa empurra pouco, o gosto e a oportunidade decidem.</summary>
    public static VocationRules Modern { get; } = new() { ParentalPressure = 0.15, OwnerPressureBoost = 1.2 };

    /// <summary>Sem vocação: ninguém escolhe na juventude (o ofício vem do primeiro emprego) e a aptidão não muda nada.</summary>
    public static VocationRules Disabled { get; } = new() { Enabled = false };

    private const double TraitMid = 50, TraitSd = 28.9;
    private const double MuscleMean = 28, MuscleSd = 6;

    /// <summary>Temperamento e corpo para o ofício, em desvios-padrão (0 sem perfil).</summary>
    public double TraitScore(int profession, Personality p, double muscleMass)
    {
        if (!Profiles.TryGetValue(profession, out var profile)) return 0;
        Span<double> z =
        [
            p.Extroversion, p.Agreeableness, p.Conscientiousness, p.EmotionalStability, p.Openness,
            p.Ambition, p.Loyalty, p.Altruism, p.Impulsivity, p.RiskAversion,
        ];
        double sum = 0, norm = 0;
        for (int i = 0; i < z.Length && i < profile.TraitWeights.Count; i++)
        {
            double w = profile.TraitWeights[i];
            sum += w * (z[i] - TraitMid) / TraitSd;
            norm += w * w;
        }
        sum += profile.MuscleWeight * Math.Clamp((muscleMass - MuscleMean) / MuscleSd, -3, 3);
        norm += profile.MuscleWeight * profile.MuscleWeight;
        return norm > 0 ? sum / Math.Sqrt(norm) : 0;
    }

    /// <summary>Aptidão para o ofício, em desvios-padrão: gosto próprio e temperamento/corpo, normalizados.</summary>
    public double Aptitude(int profession, Personality p, double muscleMass, VocationState? state)
    {
        double taste = state?.TasteFor(profession) ?? 0;
        return (taste + TraitWeight * TraitScore(profession, p, muscleMass)) / Math.Sqrt(1 + TraitWeight * TraitWeight);
    }

    /// <summary>Multiplicador de um efeito de ajuste: <c>1 + efeito × aptidão</c>, aptidão cortada em ±2 dp.</summary>
    public static double FitMultiplier(double effectPerSd, double aptitude) => 1.0 + effectPerSd * Math.Clamp(aptitude, -2, 2);

    /// <summary>Força com que a casa empurra o filho para o próprio ofício, em [0,1]: a norma, o quanto quem cria é tradicional
    /// (pouca abertura), cumpridor e autoritário (pouca amabilidade), e o negócio próprio.</summary>
    public double PressureOf(Personality rearer, bool ownsBusiness)
    {
        double authority = ((TraitMid - rearer.Openness) + (rearer.Conscientiousness - TraitMid) + (TraitMid - rearer.Agreeableness)) / 150.0;
        double force = ParentalPressure * (1 + authority) * (ownsBusiness ? OwnerPressureBoost : 1.0);
        return Math.Clamp(force, 0, 1);
    }

    /// <summary>O quanto o filho cede, em [0,1]: amabilidade e constância o fazem obedecer; abertura e ambição, seguir o próprio rumo.</summary>
    public static double Compliance(Personality child) =>
        Math.Clamp(0.5 + ((child.Agreeableness - TraitMid) + (child.Conscientiousness - TraitMid)
            - (child.Openness - TraitMid) - (child.Ambition - TraitMid)) / 200.0, 0, 1);

    public string? Validate()
    {
        if (ParentalPressure is < 0 or > 1) return "Vocation.ParentalPressure: fora de [0,1]";
        if (TasteHeritability is < 0 or > 1) return "Vocation.TasteHeritability: fora de [0,1]";
        if (PressureScale < 0 || OwnerPressureBoost < 0 || TraitWeight < 0 || ExposureWeight < 0 || OpportunityWeight < 0 || ChoiceNoise < 0)
            return "Vocation: pesos devem ser >= 0";
        if (FitOutput is < 0 or >= 0.5 || FitLearning is < 0 or >= 0.5 || FitJobValue is < 0 or >= 0.5)
            return "Vocation.Fit*: fora de [0,0.5)";
        return null;
    }
}
