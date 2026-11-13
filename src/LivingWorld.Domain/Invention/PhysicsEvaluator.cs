namespace LivingWorld.Domain.Invention;

/// <summary>Por que um design não cumpre a sonda de travessia: inválido (a natureza não dá a peça), afunda, vira, quebra ou não anda.</summary>
public enum FailureMode : byte { None = 0, Invalid = 1, Sinks = 2, Capsizes = 3, Breaks = 4, TooSlow = 5 }

/// <summary>O que o avaliador mediu de um design com uma pessoa a bordo (volátil: recomputável do design).</summary>
public readonly record struct Performance(
    FailureMode Failure, long MassG, long PayloadG, int DraftMm, int MetacentricMm, int SpeedMmS, int BuildMinutes)
{
    public bool Works => Failure == FailureMode.None;
}

/// <summary>Leis físicas simples em aritmética inteira (mm, g, mN; só + − × ÷, raiz e cúbica por busca binária de passos fixos),
/// iguais em toda CPU (AD-108). Nenhum artefato aqui: só massa, empuxo (Arquimedes), estabilidade (altura metacêntrica
/// KB + BM − KG), resistência (viga com carga no meio, M = P·L/8, contra módulo de seção × tensão admissível), arrasto
/// (pressão ½ρCdAv² + atrito de superfície) e propulsão humana (v³ = 2·P·η / (ρ·CdA)). Ordem fixa: partes na ordem do design.</summary>
public static class PhysicsEvaluator
{
    private const long Million = 1_000_000;

    public static Performance Evaluate(IReadOnlyList<DesignPart> parts, InventionRules rules, int riderKg) =>
        Evaluate(parts, rules, rules.Materials, riderKg);

    /// <summary><paramref name="materials"/> permite julgar com a crença (propriedades com erro) em vez da natureza.</summary>
    public static Performance Evaluate(IReadOnlyList<DesignPart> parts, InventionRules rules, IReadOnlyList<MaterialSpec> materials, int riderKg)
    {
        if (!IsBuildable(parts, materials)) return new Performance(FailureMode.Invalid, 0, 0, 0, 0, 0, 0);

        int n = parts.Count;
        var displace = new long[n];
        var mass = new long[n];
        var watertight = new bool[n];
        long structureG = 0;
        int buildMinutes = 0;
        int zMin = int.MaxValue, zMax = int.MinValue;
        for (int i = 0; i < n; i++)
        {
            var p = parts[i];
            var m = Find(materials, p.Material)!;
            var coat = p.Coating > 0 ? Find(materials, p.Coating) : null;
            long outer = OuterVolume(p);
            long cavity = p.IsHollow ? CavityVolume(p) : 0;
            long materialVol = outer - cavity;
            long coatVol = coat is null ? 0 : Surface(p) * rules.CoatingMm;
            long partG = materialVol * m.DensityKgM3 / Million + (coat is null ? 0 : coatVol * coat.DensityKgM3 / Million);
            watertight[i] = !p.IsHollow || m.ImpermeabilityPm >= rules.WatertightPm || (coat?.ImpermeabilityPm ?? 0) >= rules.WatertightPm;
            displace[i] = p.IsHollow && watertight[i] ? outer : materialVol + coatVol;
            mass[i] = partG;
            structureG += partG;
            buildMinutes += (int)Math.Min(int.MaxValue / 4, (materialVol * m.DensityKgM3 / Million) * (m.GatherMinutesPerKg + m.WorkMinutesPerKg) / 1000
                + cavity * m.DensityKgM3 / Million * m.WorkMinutesPerKg / 1000
                + (coat is null ? 0 : coatVol * coat.DensityKgM3 / Million * (coat.GatherMinutesPerKg + coat.WorkMinutesPerKg) / 1000)
                + (i == 0 ? 0 : rules.JointMinutes[(int)p.Joint]));
            zMin = Math.Min(zMin, p.OffsetZMm);
            zMax = Math.Max(zMax, p.Top);
        }

        long riderG = riderKg * 1000L;
        long totalG = structureG + riderG;
        long required = totalG * Million / rules.WaterDensityKgM3; // mm³ de água a deslocar
        int zLimit = zMax;
        for (int i = 0; i < n; i++)
            if (parts[i].IsHollow && watertight[i]) zLimit = Math.Min(zLimit, parts[i].Top - rules.MinFreeboardMm);
        long capacity = Submerged(parts, displace, zLimit);
        long payloadG = capacity * rules.WaterDensityKgM3 / Million - totalG;
        if (capacity < required || zLimit <= zMin) return new Performance(FailureMode.Sinks, structureG, payloadG, 0, 0, 0, buildMinutes);

        int lo = zMin, hi = zLimit;
        for (int step = 0; step < 20 && lo < hi; step++)
        {
            int mid = lo + (hi - lo) / 2;
            if (Submerged(parts, displace, mid) >= required) hi = mid; else lo = mid + 1;
        }
        int waterline = hi;

        int gm = MetacentricHeight(parts, displace, mass, watertight, rules, waterline, riderG, totalG, zMax);
        if (gm < rules.MinMetacentricHeightMm)
            return new Performance(FailureMode.Capsizes, structureG, payloadG, waterline - zMin, gm, 0, buildMinutes);

        if (!Strong(parts, materials, rules, riderG))
            return new Performance(FailureMode.Breaks, structureG, payloadG, waterline - zMin, gm, 0, buildMinutes);

        int speed = Speed(parts, materials, rules, waterline);
        var failure = speed < rules.MinUsefulPaceMmS ? FailureMode.TooSlow : FailureMode.None;
        return new Performance(failure, structureG, payloadG, waterline - zMin, gm, speed, buildMinutes);
    }

    /// <summary>Material bruto por id que a construção consome (g): a receita gerada do design, sem tabela.</summary>
    public static IReadOnlyList<(int MaterialId, long Grams)> Recipe(IReadOnlyList<DesignPart> parts, InventionRules rules)
    {
        var grams = new SortedDictionary<int, long>();
        foreach (var p in parts)
        {
            if (rules.Material(p.Material) is not { } m) continue;
            long outer = OuterVolume(p);
            grams[m.Id] = grams.GetValueOrDefault(m.Id) + outer * m.DensityKgM3 / Million; // a cavidade sai do tronco inteiro
            if (p.Coating > 0 && rules.Material(p.Coating) is { } c)
                grams[c.Id] = grams.GetValueOrDefault(c.Id) + Surface(p) * rules.CoatingMm * c.DensityKgM3 / Million;
        }
        return grams.Select(kv => (kv.Key, kv.Value)).ToList();
    }

    /// <summary>A natureza dá a peça? Medidas positivas, material que existe, dentro do tamanho natural (tronco, feixe, lâmina),
    /// parede possível; mãe sempre antes da filha.</summary>
    public static bool IsBuildable(IReadOnlyList<DesignPart> parts, IReadOnlyList<MaterialSpec> materials)
    {
        if (parts.Count is 0 or > Design.MaxParts) return false;
        for (int i = 0; i < parts.Count; i++)
        {
            var p = parts[i];
            if (Find(materials, p.Material) is not { CoatingOnly: false } m) return false;
            if (p.Coating != 0 && Find(materials, p.Coating) is null) return false;
            if (p.LengthMm <= 0 || p.WidthMm <= 0 || p.HeightMm <= 0 || p.WallMm < 0) return false;
            if (i == 0 ? p.Parent != -1 : p.Parent < 0 || p.Parent >= i) return false;
            if (p.LengthMm > m.MaxPieceMm) return false;
            int cross = Math.Max(p.SpanY, p.SpanZ);
            if (p.IsHollow)
            {
                if (p.WallMm < m.MinWallMm || p.WallMm > m.MaxThicknessMm || 2 * p.WallMm >= Math.Min(p.SpanY, p.SpanZ)) return false;
                if (cross > (m.Formable ? m.MaxPieceMm : m.MaxThicknessMm)) return false;
            }
            else if (Math.Min(p.SpanY, p.SpanZ) > m.MaxThicknessMm || cross > m.MaxPieceMm) return false;
        }
        return true;
    }

    internal static long OuterVolume(DesignPart p) => p.Shape switch
    {
        PartShape.Box => (long)p.LengthMm * p.WidthMm * p.HeightMm,
        PartShape.Cylinder => Cylinder(p.WidthMm, p.LengthMm),
        PartShape.Sphere => Sphere(p.LengthMm),
        _ => p.LengthMm <= p.WidthMm ? Sphere(p.WidthMm) : Cylinder(p.WidthMm, p.LengthMm - p.WidthMm) + Sphere(p.WidthMm),
    };

    private static long CavityVolume(DesignPart p)
    {
        int w = p.WallMm;
        return p.Shape switch
        {
            PartShape.Box => (long)(p.LengthMm - 2 * w) * (p.WidthMm - 2 * w) * (p.HeightMm - w),
            PartShape.Sphere => Sphere(p.LengthMm - 2 * w),
            _ => Cylinder(p.WidthMm - 2 * w, Math.Max(0, p.LengthMm - 2 * w)),
        };
    }

    private static long Surface(DesignPart p) => p.Shape switch
    {
        PartShape.Box => 2L * ((long)p.LengthMm * p.WidthMm + (long)p.LengthMm * p.HeightMm + (long)p.WidthMm * p.HeightMm),
        PartShape.Sphere => 355L * p.LengthMm * p.LengthMm / 113,
        _ => 355L * p.WidthMm * p.LengthMm / 113 + 355L * p.WidthMm * p.WidthMm / 226,
    };

    private static long Cylinder(long diameter, long length) => 355 * diameter * diameter * length / 452;

    private static long Sphere(long diameter) => 355 * diameter * diameter * diameter / 678;

    /// <summary>Volume deslocado abaixo da cota <paramref name="z"/>: caixa linear; redondas pela fração da calota s²(3 − 2s).</summary>
    private static long Submerged(IReadOnlyList<DesignPart> parts, long[] displace, int z)
    {
        long sum = 0;
        for (int i = 0; i < parts.Count; i++)
        {
            var p = parts[i];
            int h = p.SpanZ;
            long t = Math.Clamp(z - p.OffsetZMm, 0, h);
            if (t == 0) continue;
            if (t == h) { sum += displace[i]; continue; }
            sum += p.Shape == PartShape.Box
                ? displace[i] * t / h
                : (long)((Int128)displace[i] * t * t * (3L * h - 2 * t) / ((Int128)h * h * h));
        }
        return sum;
    }

    private static int MetacentricHeight(
        IReadOnlyList<DesignPart> parts, long[] displace, long[] mass, bool[] watertight, InventionRules rules,
        int z, long riderG, long totalG, int zMax)
    {
        Int128 inertia = 0;
        long volume = 0;
        Int128 buoyancyMoment = 0;
        Int128 massMoment = 0;
        int seat = zMax + rules.SeatedCenterOfMassMm;
        for (int i = 0; i < parts.Count; i++)
        {
            var p = parts[i];
            int h = p.SpanZ;
            massMoment += (Int128)mass[i] * (p.OffsetZMm + h / 2);
            if (p.IsHollow && watertight[i]) seat = Math.Min(seat, p.OffsetZMm + p.WallMm + rules.SeatedCenterOfMassMm);
            long t = Math.Clamp(z - p.OffsetZMm, 0, h);
            if (t == 0) continue;
            long v = p.Shape == PartShape.Box || t == h
                ? displace[i] * t / h
                : (long)((Int128)displace[i] * t * t * (3L * h - 2 * t) / ((Int128)h * h * h));
            volume += v;
            buoyancyMoment += (Int128)v * (p.OffsetZMm + t / 2);
            if (t >= h) continue; // inteira submersa: não corta a linha d'água
            long chord = p.Shape == PartShape.Box ? p.WidthMm : 2 * ISqrt(t * (h - t));
            long y = p.OffsetYMm;
            if (p.Shape == PartShape.Sphere)
                inertia += (Int128)355 * chord * chord * chord * chord / (113 * 64) + (Int128)355 * chord * chord / 452 * y * y;
            else
                inertia += (Int128)p.LengthMm * chord * chord * chord / 12 + (Int128)p.LengthMm * chord * y * y;
        }
        if (volume <= 0) return int.MinValue;
        massMoment += (Int128)riderG * seat;
        long kb = (long)(buoyancyMoment / volume);
        long bm = (long)(inertia / volume);
        long kg = (long)(massMoment / totalG);
        return (int)Math.Clamp(kb + bm - kg, int.MinValue / 2, int.MaxValue / 2);
    }

    private static bool Strong(IReadOnlyList<DesignPart> parts, IReadOnlyList<MaterialSpec> materials, InventionRules rules, long riderG)
    {
        int xMin = int.MaxValue, xMax = int.MinValue;
        foreach (var p in parts)
        {
            xMin = Math.Min(xMin, p.OffsetXMm - p.LengthMm / 2);
            xMax = Math.Max(xMax, p.OffsetXMm + p.LengthMm / 2);
        }
        long span = xMax - xMin;
        Int128 moment = (Int128)riderG * rules.GravityMmS2 / 1000 * span / 8; // mN·mm
        Int128 capacity = 0;
        for (int i = 0; i < parts.Count; i++)
        {
            var p = parts[i];
            if (2L * p.LengthMm < span) continue; // parte curta não vence o vão
            var m = Find(materials, p.Material)!;
            Int128 modulus = SectionModulus(p);
            int joint = i == 0 ? 1000 : rules.JointEfficiencyPm[(int)p.Joint];
            capacity += modulus * m.TensileMPa * 1000 * 1000 / rules.SafetyFactorPm * joint / 1000;
        }
        return capacity >= moment;
    }

    private static Int128 SectionModulus(DesignPart p)
    {
        long w = p.WallMm;
        if (p.Shape == PartShape.Box)
        {
            Int128 b = p.WidthMm, h = p.HeightMm;
            if (!p.IsHollow) return b * h * h / 6;
            Int128 bi = b - 2 * w, hi = h - w;
            return (b * h * h * h - bi * hi * hi * hi) / (6 * h);
        }
        Int128 d = p.Shape == PartShape.Sphere ? p.LengthMm : p.WidthMm;
        if (!p.IsHollow) return 355 * d * d * d / (113 * 32);
        Int128 di = d - 2 * w;
        return 355 * (d * d * d * d - di * di * di * di) / (113 * 32 * d);
    }

    private static int Speed(IReadOnlyList<DesignPart> parts, IReadOnlyList<MaterialSpec> materials, InventionRules rules, int z)
    {
        Int128 dragArea = 0; // mm² × ppm
        bool pusher = false;
        for (int i = 0; i < parts.Count; i++)
        {
            var p = parts[i];
            if (i > 0 && p.Shape == PartShape.Box && (p.Joint is JointKind.Hinge or JointKind.Flexible)
                && 8 * Math.Min(p.WidthMm, p.HeightMm) <= Math.Max(p.LengthMm, Math.Max(p.WidthMm, p.HeightMm)))
                pusher = true;
            int h = p.SpanZ;
            long t = Math.Clamp(z - p.OffsetZMm, 0, h);
            if (t == 0) continue;
            long frontal = p.Shape == PartShape.Box
                ? (long)p.WidthMm * t
                : (long)((Int128)355 * p.SpanY * p.SpanY / 452 * t * t * (3L * h - 2 * t) / ((Int128)h * h * h));
            int cd = p.Shape switch
            {
                PartShape.Box => rules.BoxDragPm,
                PartShape.Cylinder => rules.CylinderDragPm,
                PartShape.Sphere => rules.SphereDragPm,
                _ => rules.CapsuleDragPm,
            };
            long wetted = (long)p.LengthMm * (p.SpanY + 2 * t);
            dragArea += (Int128)cd * 1000 * frontal + (Int128)rules.SkinFrictionPpm * wetted;
        }
        if (dragArea <= 0) return 0;
        int efficiency = pusher ? rules.PlateEfficiencyPm : rules.HandEfficiencyPm;
        Int128 cube = (Int128)2 * rules.HumanPowerW * efficiency * 1_000_000_000_000_000 * 1000 / (dragArea * rules.WaterDensityKgM3);
        return (int)Math.Min(int.MaxValue, ICbrt(cube));
    }

    private static MaterialSpec? Find(IReadOnlyList<MaterialSpec> materials, int id)
    {
        foreach (var m in materials) if (m.Id == id) return m;
        return null;
    }

    internal static long ISqrt(long value)
    {
        if (value <= 0) return 0;
        long lo = 0, hi = Math.Min(value, 3_037_000_499);
        while (lo < hi)
        {
            long mid = lo + (hi - lo + 1) / 2;
            if (mid * mid <= value) lo = mid; else hi = mid - 1;
        }
        return lo;
    }

    internal static long ICbrt(Int128 value)
    {
        if (value <= 0) return 0;
        long lo = 0, hi = 2_000_000;
        while (lo < hi)
        {
            long mid = lo + (hi - lo + 1) / 2;
            if ((Int128)mid * mid * mid <= value) lo = mid; else hi = mid - 1;
        }
        return lo;
    }
}
