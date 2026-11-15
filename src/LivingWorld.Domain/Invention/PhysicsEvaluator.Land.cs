namespace LivingWorld.Domain.Invention;

/// <summary>Como a coisa toca o chão ao ser puxada (AD-153): arrasta (raspa), desliza sobre patim ou rola sobre peça redonda com eixo.</summary>
public enum GroundMode : byte { None = 0, Drag = 1, Skid = 2, Roll = 3 }

/// <summary>O que a sonda de terra mediu (AD-153, volátil): como toca o chão, massa própria, a carga que uma pessoa puxa nela (g,
/// limitada pela força sustentada, pelo lugar para a carga e pela resistência da armação, do eixo e da roda), a resistência ao
/// movimento (‰ da carga total) e a força para puxá-la vazia (N).</summary>
public readonly record struct Haulage(
    FailureMode Failure, GroundMode Mode, long MassG, long CapacityG, int ResistancePm, long RoomG, int EmptyPullN, int BuildMinutes)
{
    public bool Works => Failure == FailureMode.None;
}

public static partial class PhysicsEvaluator
{
    public static Haulage Haul(IReadOnlyList<DesignPart> parts, InventionRules rules) => Haul(parts, rules, rules.Materials);

    /// <summary>Sonda "levar por terra" (AD-153), só inteiros. Tocam o chão as partes de base mais baixa. Em cada sentido (X e Y) cada
    /// uma resiste: rola (cilindro ou cápsula de lado, esfera; presa à mãe por eixo) com braço de rolamento ÷ raio mais o atrito do
    /// eixo × raio do eixo ÷ raio; desliza (peça esguia no sentido) com o atrito do patim, menor quanto mais liso o material; ou
    /// arrasta. Vale o sentido que resiste menos. Carga = a menor entre o que a força sustentada puxa (F = μ·m·g), o lugar (oco ou
    /// tabuleiro × altura da pilha × densidade a granel) e o que a armação (viga com carga no meio), o eixo (pino em balanço, do
    /// material e da espessura da mãe) e a roda (seção do disco) aguentam com a sua parte do peso.</summary>
    public static Haulage Haul(IReadOnlyList<DesignPart> parts, InventionRules rules, IReadOnlyList<MaterialSpec> materials)
    {
        if (!IsBuildable(parts, materials)) return new Haulage(FailureMode.Invalid, GroundMode.None, 0, 0, 0, 0, 0, 0);
        long massG = 0, cavityMm3 = 0, deckMm2 = 0;
        int buildMinutes = 0, zMin = int.MaxValue;
        for (int i = 0; i < parts.Count; i++)
        {
            var p = parts[i];
            var m = Find(materials, p.Material)!;
            var coat = p.Coating > 0 ? Find(materials, p.Coating) : null;
            long outer = OuterVolume(p), cavity = p.IsHollow ? CavityVolume(p) : 0, materialVol = outer - cavity;
            long coatVol = coat is null ? 0 : Surface(p) * rules.CoatingMm;
            massG += materialVol * m.DensityKgM3 / Million + (coat is null ? 0 : coatVol * coat.DensityKgM3 / Million);
            buildMinutes += (int)Math.Min(int.MaxValue / 4, (materialVol * m.DensityKgM3 / Million) * (m.GatherMinutesPerKg + m.WorkMinutesPerKg) / 1000
                + cavity * m.DensityKgM3 / Million * m.WorkMinutesPerKg / 1000
                + (coat is null ? 0 : coatVol * coat.DensityKgM3 / Million * (coat.GatherMinutesPerKg + coat.WorkMinutesPerKg) / 1000)
                + (i == 0 ? 0 : rules.JointMinutes[(int)p.Joint]));
            cavityMm3 += cavity;
            if (p.Shape == PartShape.Box && !p.IsHollow) deckMm2 = Math.Max(deckMm2, (long)p.LengthMm * p.WidthMm);
            zMin = Math.Min(zMin, p.OffsetZMm);
        }

        var contacts = new List<int>(parts.Count);
        for (int i = 0; i < parts.Count; i++) if (parts[i].OffsetZMm == zMin) contacts.Add(i);
        var (ppm, alongY) = (Resistance(parts, materials, rules, contacts, alongY: false), false);
        long ppmY = Resistance(parts, materials, rules, contacts, alongY: true);
        if (ppmY < ppm) (ppm, alongY) = (ppmY, true);
        var mode = ModeOf(parts, rules, contacts, alongY);
        int resistancePm = (int)Math.Min(int.MaxValue, ppm / 1000);
        int emptyPullN = (int)Int128.Min(int.MaxValue, (Int128)ppm * massG * rules.GravityMmS2 / 1_000_000_000_000_000);

        long roomG = Math.Max(cavityMm3, deckMm2 * rules.PileHeightMm) * rules.CargoBulkKgM3 / Million;
        long pullG = (long)Int128.Min(long.MaxValue / 4, (Int128)rules.HumanPullN * 1_000_000_000_000 / Math.Max(1, ppm * rules.GravityMmS2)) - massG;
        long unitG = (long)rules.SmallestUnitMl() * rules.CargoBulkKgM3 / 1000;
        if (pullG < unitG) return new Haulage(FailureMode.TooHeavy, mode, massG, 0, resistancePm, roomG, emptyPullN, buildMinutes);
        if (roomG < unitG) return new Haulage(FailureMode.TooSmall, mode, massG, 0, resistancePm, roomG, emptyPullN, buildMinutes);

        // rodas não fazem viga: a armação é o resto; cada roda leva a sua parte do peso no pino e no disco
        var frame = new List<DesignPart>(parts.Count);
        var wheels = new List<int>();
        for (int i = 0; i < parts.Count; i++)
            if (contacts.Contains(i) && Rolls(parts, i, alongY) && !Tips(parts, contacts, alongY)) wheels.Add(i); else frame.Add(parts[i]);
        bool Holds(long load) => Strong(frame, materials, rules, load) && WheelsHold(parts, materials, rules, wheels, (massG + load) / contacts.Count);

        long lo = 0, hi = Math.Min(pullG, roomG);
        for (int step = 0; step < 40 && lo < hi; step++)
        {
            long mid = lo + (hi - lo + 1) / 2;
            if (Holds(mid)) lo = mid; else hi = mid - 1;
        }
        var failure = lo < unitG ? FailureMode.Breaks : FailureMode.None;
        return new Haulage(failure, mode, massG, failure == FailureMode.None ? lo : 0, resistancePm, roomG, emptyPullN, buildMinutes);
    }

    /// <summary>Resistência média das partes que tocam o chão, em ppm da carga, num sentido.</summary>
    private static long Resistance(IReadOnlyList<DesignPart> parts, IReadOnlyList<MaterialSpec> materials, InventionRules rules, List<int> contacts, bool alongY)
    {
        if (Tips(parts, contacts, alongY)) return rules.DragFrictionPm * 1000L;
        long sum = 0;
        foreach (int i in contacts)
        {
            var p = parts[i];
            if (Rolls(parts, i, alongY))
            {
                long radius = Math.Max(1, p.SpanZ / 2);
                long pin = PinDiameter(parts, i);
                sum += rules.RollingResistanceMm * 1_000_000L / radius + rules.AxleFrictionPm * 1000L * (pin / 2) / radius;
            }
            else if (IsRunner(p, rules, alongY))
            {
                var m = Find(materials, p.Material)!;
                int finish = p.Coating > 0 && Find(materials, p.Coating) is { } c ? Math.Max(c.FinishPm, m.FinishPm) : m.FinishPm;
                sum += (rules.SkidFrictionPm - (long)rules.SkidFinishGainPm * finish / 1000) * 1000;
            }
            else sum += rules.DragFrictionPm * 1000L;
        }
        return sum / Math.Max(1, contacts.Count);
    }

    private static GroundMode ModeOf(IReadOnlyList<DesignPart> parts, InventionRules rules, List<int> contacts, bool alongY)
    {
        if (Tips(parts, contacts, alongY)) return GroundMode.Drag;
        bool allRoll = true, allGlide = true;
        foreach (int i in contacts)
        {
            bool roll = Rolls(parts, i, alongY);
            allRoll &= roll;
            allGlide &= roll || IsRunner(parts[i], rules, alongY);
        }
        return allRoll ? GroundMode.Roll : allGlide ? GroundMode.Skid : GroundMode.Drag;
    }

    /// <summary>Tomba: as partes que tocam o chão cobrem, de lado (perpendicular ao movimento), menos da metade da largura da
    /// coisa; ela deita sobre a parte mais larga e arrasta. Uma roda só ou um patim fino sob um tabuleiro largo não seguram.</summary>
    private static bool Tips(IReadOnlyList<DesignPart> parts, List<int> contacts, bool alongY)
    {
        static (int Lo, int Hi) Side(DesignPart p, bool alongY) => alongY
            ? (p.OffsetXMm - p.LengthMm / 2, p.OffsetXMm + p.LengthMm / 2)
            : (p.OffsetYMm - p.SpanY / 2, p.OffsetYMm + p.SpanY / 2);
        int lo = int.MaxValue, hi = int.MinValue, cLo = int.MaxValue, cHi = int.MinValue;
        for (int i = 0; i < parts.Count; i++)
        {
            var (a, b) = Side(parts[i], alongY);
            lo = Math.Min(lo, a); hi = Math.Max(hi, b);
        }
        foreach (int i in contacts)
        {
            var (a, b) = Side(parts[i], alongY);
            cLo = Math.Min(cLo, a); cHi = Math.Max(cHi, b);
        }
        return 2L * (cHi - cLo) < hi - lo;
    }

    /// <summary>Rola no sentido: peça redonda nesse plano (cilindro e cápsula têm o eixo em X, então rolam em Y; esfera rola em
    /// qualquer um) e presa à mãe por eixo. Redonda sem eixo, ou caixa, não rola.</summary>
    private static bool Rolls(IReadOnlyList<DesignPart> parts, int i, bool alongY)
    {
        var p = parts[i];
        if (i == 0 || p.Joint != JointKind.Axle) return false;
        return p.Shape == PartShape.Sphere || (alongY && p.Shape is PartShape.Cylinder or PartShape.Capsule);
    }

    /// <summary>Patim: no sentido do movimento a peça é <see cref="InventionRules.RunnerSlenderness"/> vezes mais comprida que larga.</summary>
    private static bool IsRunner(DesignPart p, InventionRules rules, bool alongY)
    {
        int along = alongY ? p.SpanY : p.LengthMm, across = alongY ? p.LengthMm : p.SpanY;
        return p.Shape != PartShape.Sphere && along >= (long)rules.RunnerSlenderness * across;
    }

    /// <summary>O eixo é um pino da mãe: tão grosso quanto a parte mais fina dela, até metade do diâmetro da roda.</summary>
    private static long PinDiameter(IReadOnlyList<DesignPart> parts, int i)
    {
        var parent = parts[parts[i].Parent];
        int thick = parent.Shape == PartShape.Box ? Math.Min(parent.SpanY, parent.SpanZ) : parent.SpanY;
        return Math.Max(1, Math.Min(thick, parts[i].SpanZ / 2));
    }

    /// <summary>Cada roda com a sua parte do peso: o pino em balanço até o meio da roda (M = P·L) e o disco atravessado (M = P·D/8).</summary>
    private static bool WheelsHold(IReadOnlyList<DesignPart> parts, IReadOnlyList<MaterialSpec> materials, InventionRules rules, List<int> wheels, long shareG)
    {
        Int128 force = (Int128)shareG * rules.GravityMmS2 / 1000; // mN
        foreach (int i in wheels)
        {
            var p = parts[i];
            var pinMaterial = Find(materials, parts[p.Parent].Material)!;
            Int128 d = PinDiameter(parts, i);
            long arm = Math.Max(10, p.LengthMm / 2);
            Int128 pinCapacity = 355 * d * d * d / (113 * 32) * pinMaterial.TensileMPa * 1000 * 1000 / rules.SafetyFactorPm;
            if (pinCapacity < force * arm) return false;
            var m = Find(materials, p.Material)!;
            Int128 b = p.LengthMm, h = p.SpanZ, w = p.WallMm;
            Int128 modulus = p.IsHollow && h > 2 * w ? b * (h * h * h - (h - 2 * w) * (h - 2 * w) * (h - 2 * w)) / (6 * h) : b * h * h / 6;
            if (modulus * m.TensileMPa * 1000 * 1000 / rules.SafetyFactorPm < force * h / 8) return false;
        }
        return true;
    }
}
