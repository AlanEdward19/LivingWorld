using LivingWorld.Domain.Population;
using LivingWorld.Domain.Population.Body;
using LivingWorld.Domain.Shared;
using LivingWorld.Simulation.Core;

namespace LivingWorld.Simulation.Population.Body;

/// <summary>Sorteios fixos do corpo de uma pessoa (AD-139), derivados da semente e do id (nada guardado): desvio da idade da
/// puberdade, duração e fase do ciclo, e se ela tem cólica que limita ou tensão pré-menstrual.</summary>
public readonly record struct EndocrineTraits(double PubertyZ, int CycleDays, int PhaseDay, bool Dysmenorrhea, bool Premenstrual);

/// <summary>Hormônios que pesam na decisão (AD-139), já em unidades de utilidade ou fração: cólica (afasta do trabalho, puxa
/// para o descanso), irritação pré-menstrual (afasta do convívio) e o impulso da adolescência (pares, mudar de ideia, descontar
/// as consequências). Padrão = nenhum efeito.</summary>
public readonly record struct HormonalSnapshot(
    float CrampWork, float CrampIdle, float IrritableSocial, float PeerSocial, float Fickleness, float ForesightDiscount);

/// <summary>Hormônios como estado do corpo (AD-139), lidos na hora: puberdade (marcada uma vez pelo crescimento mensal),
/// impulso da adolescência, testosterona, ciclo menstrual (fase, janela fértil, menstruação, amenorreia da fome e da
/// menopausa) e efeitos na decisão. Toda leitura é pura: o mundo reaberto de um snapshot lê o mesmo valor.</summary>
public static class Endocrine
{
    public static EndocrineRules Rules(WorldState world) => world.PopulationRules.Fertility.Endocrine;

    public static bool Enabled(WorldState world) => Rules(world).Enabled;

    public static EndocrineTraits TraitsOf(WorldState world, Npc npc)
    {
        var r = Rules(world);
        // estado do stream sem objeto: lido em toda decisão de quem tem ciclo, não pode alocar
        ulong s = world.Rng.StreamStateFor("endocrine", npc.Id.Value);
        double z = Normal(ref s);
        double cycle = r.CycleMeanDays + Normal(ref s) * r.CycleStdDevDays;
        int days = Math.Clamp((int)Math.Round(cycle), r.CycleMinDays, r.CycleMaxDays);
        int phase = Math.Min(days - 1, (int)(WorldRng.NextDouble(ref s) * days));
        var (cramps, premenstrual) = SymptomsOf(world, npc);
        return new EndocrineTraits(z, days, phase, cramps, premenstrual);
    }

    /// <summary>Só os sintomas do ciclo (stream próprio, barato): quem não tem nenhum nem precisa do ciclo na decisão.</summary>
    public static (bool Cramps, bool Premenstrual) SymptomsOf(WorldState world, Npc npc)
    {
        var r = Rules(world);
        ulong s = world.Rng.StreamStateFor("endocrine-symptoms", npc.Id.Value);
        return (WorldRng.NextDouble(ref s) < r.DysmenorrheaShare, WorldRng.NextDouble(ref s) < r.PremenstrualShare);
    }

    /// <summary>Mesmo sorteio fixo de <c>NatalitySystem.SterilityDraw</c>, sem alocar.</summary>
    public static double SterilityDraw(WorldState world, Npc npc)
    {
        ulong s = world.Rng.StreamStateFor("sterility", npc.Id.Value);
        return WorldRng.NextDouble(ref s);
    }

    private const double Sqrt3 = 1.7320508075688772;

    /// <summary>N(0,1) aproximada (soma de quatro uniformes, Irwin-Hall, ±3,5 dp) sobre um estado sem objeto: só soma e
    /// multiplicação, porque é lida a cada decisão de quem tem ciclo ou está na adolescência.</summary>
    private static double Normal(ref ulong s) =>
        (WorldRng.NextDouble(ref s) + WorldRng.NextDouble(ref s) + WorldRng.NextDouble(ref s) + WorldRng.NextDouble(ref s) - 2.0) * Sqrt3;

    private static double AgeYears(WorldState world, Npc npc, long tick) =>
        (tick - npc.BirthDate.TotalHours) / (double)world.Calendar.HoursPerYear;

    /// <summary>Idade da puberdade bem nutrida (anos), pelo sexo e pelo desvio individual.</summary>
    public static double BasePubertyAge(EndocrineRules r, Sex sex, double z) => Math.Clamp(
        (sex == Sex.Female ? r.FemalePubertyMeanYears : r.MalePubertyMeanYears) + z * r.PubertyStdDevYears,
        r.PubertyMinYears, r.PubertyMaxYears);

    /// <summary>Idade a partir da qual a puberdade já veio para qualquer um (máximo mais o atraso da fome plena).</summary>
    private static double SurelyPubertal(EndocrineRules r) => r.PubertyMaxYears + r.UndernutritionPubertyDelayYears;

    /// <summary>Idade da puberdade (anos): a marcada, ou a que viria hoje pela desnutrição atual.</summary>
    public static double PubertyAgeYears(WorldState world, Npc npc)
    {
        if (npc.PubertyTick is { } tick) return AgeYears(world, npc, tick);
        var r = Rules(world);
        return BasePubertyAge(r, npc.Sex, TraitsOf(world, npc).PubertyZ) + r.UndernutritionPubertyDelayYears * npc.Undernutrition;
    }

    /// <summary>Já passou da puberdade no tick (sem hormônios: sempre, para não mudar o mundo antigo).</summary>
    public static bool IsPubertal(WorldState world, Npc npc, long tick)
    {
        var r = Rules(world);
        if (!r.Enabled) return true;
        if (npc.PubertyTick is { } p) return p <= tick;
        double age = AgeYears(world, npc, tick);
        if (age < r.PubertyMinYears) return false;
        return age >= SurelyPubertal(r) || age >= PubertyAgeYears(world, npc);
    }

    /// <summary>Crescimento mensal: marca a puberdade de quem a alcançou (a fome do mês a adia) e a perda de ferro da
    /// menstruação em quem come mal (anemia: saúde a menos por ciclo).</summary>
    public static void MonthlyUpkeep(WorldState world, Npc npc, long now)
    {
        var r = Rules(world);
        if (!r.Enabled) return;
        double age = AgeYears(world, npc, now);
        if (npc.PubertyTick is null && age >= r.PubertyMinYears && age < SurelyPubertal(r) + 1)
        {
            double due = PubertyAgeYears(world, npc);
            if (age >= due)
                npc.SetPuberty(Math.Min(now, npc.BirthDate.TotalHours + (long)(due * world.Calendar.HoursPerYear)));
        }

        if (npc.Sex == Sex.Female && r.MenstrualHealthLoss > 0 && npc.Undernutrition >= r.AnemiaUndernutrition
            && npc.Health > world.NeedsRules.Physiology.HealthFloor && IsCycling(world, npc, now))
            npc.SetHealth(Math.Max(world.NeedsRules.Physiology.HealthFloor, npc.Health - r.MenstrualHealthLoss));
    }

    /// <summary>Impulso da adolescência (0..1): zero antes da puberdade, sobe até o pico em <c>DriveRiseYears</c> e cai em
    /// linha até a maturidade (~25), quando o controle termina de amadurecer.</summary>
    public static double AdolescentDrive(WorldState world, Npc npc, long tick)
    {
        var r = Rules(world);
        if (!r.Enabled) return 0;
        double age = AgeYears(world, npc, tick);
        if (age < r.PubertyMinYears || age >= r.MaturityAgeYears) return 0;
        double puberty = PubertyAgeYears(world, npc);
        if (age < puberty) return 0;
        double since = age - puberty;
        if (since < r.DriveRiseYears) return since / r.DriveRiseYears;
        double peak = puberty + r.DriveRiseYears;
        return peak >= r.MaturityAgeYears ? 0 : Math.Clamp((r.MaturityAgeYears - age) / (r.MaturityAgeYears - peak), 0, 1);
    }

    /// <summary>Impulsividade com o impulso da adolescência somado.</summary>
    public static double EffectiveImpulsivity(EndocrineRules r, Personality p, double drive) =>
        Math.Clamp(p.Impulsivity + drive * r.AdolescentImpulsivityBoost, 0, 100);

    /// <summary>Aversão a risco com o impulso da adolescência descontado.</summary>
    public static double EffectiveRiskAversion(EndocrineRules r, Personality p, double drive) =>
        Math.Clamp(p.RiskAversion - drive * r.AdolescentRiskAversionDrop, 0, 100);

    /// <summary>Testosterona relativa (0..1) pela idade e pelo sexo; antes da puberdade, a do menino de 10 anos.</summary>
    public static double Testosterone(WorldState world, Npc npc, long tick)
    {
        var r = Rules(world);
        double age = AgeYears(world, npc, tick);
        double level = IsPubertal(world, npc, tick) ? r.MaleTestosterone.At(age) : r.MaleTestosterone.At(Math.Min(age, 10));
        return npc.Sex == Sex.Male ? level : level * r.FemaleTestosteroneShare;
    }

    /// <summary>Multiplicador da agressividade (chance de violência): base + ganho × testosterona. Sem hormônios: 1.</summary>
    public static double AggressionFactor(WorldState world, Npc npc, long tick)
    {
        var r = Rules(world);
        return r.Enabled ? r.AggressionBase + r.AggressionGain * Testosterone(world, npc, tick) : 1.0;
    }

    // ---------------- ciclo ----------------

    /// <summary>Menstrua e ovula: mulher depois da menarca, fora da gravidez e do intervalo infecundo (pós-parto, pós-perda),
    /// sem amenorreia da fome e antes da menopausa (estéril depois dos 40 pelo mesmo sorteio da natalidade).</summary>
    public static bool IsCycling(WorldState world, Npc npc, long tick)
    {
        var r = Rules(world);
        if (!r.Enabled || npc.Sex != Sex.Female || npc.PregnantUntil is not null) return false;
        if (npc.Reproduction is { } history && history.InfecundUntilTick > tick) return false;
        if (npc.Undernutrition >= r.AmenorrheaUndernutrition || !IsPubertal(world, npc, tick)) return false;
        double age = AgeYears(world, npc, tick);
        var fertility = world.PopulationRules.Fertility;
        return age < 40 || SterilityDraw(world, npc) >= fertility.FemaleSterileFraction.At(age);
    }

    /// <summary>Dia do ciclo (0 = primeiro dia da menstruação). O ciclo recomeça no fim do intervalo infecundo.</summary>
    public static int CycleDayAt(WorldState world, Npc npc, in EndocrineTraits traits, long tick)
    {
        long origin = npc.Reproduction?.InfecundUntilTick ?? 0;
        long day = Math.Max(0, tick - origin) / world.Calendar.HoursPerDay;
        return (int)((day + traits.PhaseDay) % traits.CycleDays);
    }

    /// <summary>Ticks de ovulação em [início, início + horas), até dois (ciclo curto num mês longo). Sem ovulação no período
    /// (fora do ciclo): zero.</summary>
    public static int Ovulations(WorldState world, Npc npc, long start, long hours, Span<long> into)
    {
        if (!IsCycling(world, npc, start)) return 0;
        var traits = TraitsOf(world, npc);
        int hpd = world.Calendar.HoursPerDay;
        int ovulationDay = traits.CycleDays - Rules(world).LutealDays;
        int today = CycleDayAt(world, npc, traits, start);
        long dayStart = start - (start - (npc.Reproduction?.InfecundUntilTick ?? 0)) % hpd;
        long next = dayStart + (long)((ovulationDay - today + traits.CycleDays) % traits.CycleDays) * hpd + hpd / 2;
        int count = 0;
        while (next < start + hours && count < into.Length)
        {
            if (next >= start) into[count++] = next;
            next += (long)traits.CycleDays * hpd;
        }
        return count;
    }

    /// <summary>Fração do trabalho do dia que a cólica tira (só nas que a têm, nos primeiros dias da menstruação).</summary>
    public static double CrampWithdrawal(WorldState world, Npc npc, long tick)
    {
        var r = Rules(world);
        if (!r.Enabled || npc.Sex != Sex.Female || r.DysmenorrheaShare <= 0 || !SymptomsOf(world, npc).Cramps) return 0;
        if (!IsCycling(world, npc, tick)) return 0;
        var traits = TraitsOf(world, npc);
        return CycleDayAt(world, npc, traits, tick) < r.CrampDays ? r.CrampWorkWithdrawal : 0;
    }

    /// <summary>Os hormônios do momento para a utilidade: cólica e irritação (ciclo) e impulso da adolescência.</summary>
    public static HormonalSnapshot SnapshotOf(WorldState world, Npc npc, long tick)
    {
        var r = Rules(world);
        if (!r.Enabled) return default;
        double age = AgeYears(world, npc, tick);
        if (age < r.PubertyMinYears) return default;
        double drive = age < r.MaturityAgeYears ? AdolescentDrive(world, npc, tick) : 0;
        double cramps = 0, irritation = 0;
        if (npc.Sex == Sex.Female && age < 55 && (r.DysmenorrheaShare > 0 || r.PremenstrualShare > 0)
            && SymptomsOf(world, npc) is { } symptoms && (symptoms.Cramps || symptoms.Premenstrual))
        {
            var traits = TraitsOf(world, npc);
            if (IsCycling(world, npc, tick))
            {
                int day = CycleDayAt(world, npc, traits, tick);
                if (traits.Dysmenorrhea && day < r.CrampDays) cramps = 1;
                if (traits.Premenstrual && day >= traits.CycleDays - r.PremenstrualDays) irritation = 1;
            }
        }
        return new HormonalSnapshot(
            (float)(cramps * r.CrampWorkPenalty), (float)(cramps * r.CrampIdleBonus), (float)(irritation * r.IrritabilitySocialPenalty),
            (float)(drive * r.PeerSocialBonus), (float)(drive * r.FickleContinuity), (float)(drive * r.ForesightDiscount));
    }
}
