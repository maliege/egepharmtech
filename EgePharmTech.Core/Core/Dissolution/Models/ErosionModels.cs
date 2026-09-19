#nullable enable
using MathNet.Numerics.RootFinding;
using EgePharmTech.Core.Localization;

namespace EgePharmTech.Core.Dissolution.Models;

/// <summary>
/// Hopfenberg: F = 100·(1 − (1 − kHB·t)^n) (şartname §5.7). Costa &amp; Sousa Lobo Eq. 39.
///
/// <b>n neden fit edilmiyor?</b> Costa &amp; Sousa Lobo Eq. 39'da n bir <i>geometri sabitidir</i>:
/// "The value of n is 1, 2 and 3 for a slab, cylinder and sphere, respectively."
/// Şartname §5.7/§6.4 n'in serbest bırakılmasını önerir (Zero-order'a çökmesin diye), ancak
/// bu, modeli başka bir dejenerasyona sürükler: n→∞ iken (1−k·t)^n → e^(−n·k·t), yani model
/// <b>First-order'a</b> çöker. DDSolver fikstürü bunu doğruluyor: serbest-n fit'i
/// n = 3054.18, kHB = 4.55e-05 üretir (fiziksel olarak anlamsız) ve SS'i (92.097) neredeyse
/// tam olarak First-order'ın SS'ine (92.023) eşitlenir. Bu yüzden n, kullanıcının seçtiği
/// geometriden gelir; yalnız kHB fit edilir.
/// </summary>
public sealed class HopfenbergModel : DissolutionModelBase
{
    public override string Name => "Hopfenberg";
    public override string Equation => "F = 100*(1-(1-kHB*t)^n)";
    public override IReadOnlyList<string> ParamNames => new[] { "kHB" };
    public override bool SupportsF0 => false;   // 100'de tavanlı
    public override bool SupportsFmax => false;

    public override double[] LowerBounds => new[] { 0.0 };
    public override double[] UpperBounds => new[] { double.PositiveInfinity };

    public override IReadOnlyList<Coefficient> Describe(double[] p) => new[]
    {
        new Coefficient("kHB", p[0], CoreText.T("1/zaman"), CoreText.T("Yüzey erozyonu hız sabiti (k0/(C0·a0))"))
    };

    /// <summary>Tohum: y = 1 − (1 − F/100)^(1/n), x = t, orijinden → kHB.</summary>
    public override double[] InitialGuess(IReadOnlyList<double> t, IReadOnlyList<double> f, VariantOptions opt)
    {
        double n = Exponent(opt.Geometry);
        var (xs, ys) = SeedPoints(t, f,
            ti => ti,
            fi => 1 - Math.Pow(1 - fi / DefaultCeiling, 1.0 / n),
            (_, fi) => fi < DefaultCeiling);
        return new[] { SeedRegression.ThroughOrigin(xs, ys) };
    }

    public override double EvaluateCore(double t, double[] p, double ceiling, VariantOptions opt)
    {
        double n = Exponent(opt.Geometry);
        double u = 1 - p[0] * t;
        // u ≤ 0: matris tükenmiş → tavanda kal (aksi halde eğri geri döner)
        if (u <= 0) return ceiling;
        return ceiling * (1 - Math.Pow(u, n));
    }

    public override IReadOnlyList<SecondaryValue> SecondaryCore(double[] p, double ceiling, VariantOptions opt)
    {
        double n = Exponent(opt.Geometry);
        return SolveTargets(ceiling, x => p[0] > 0
            ? (1 - Math.Pow(1 - x / ceiling, 1.0 / n)) / p[0]
            : double.NaN);
    }

    public override IReadOnlyList<string> Flags(double[] p, VariantOptions opt)
    {
        var flags = new List<string>
        {
            CoreText.T("Geometri: {0} (n = {1:0.#})", GeometryName(opt.Geometry), Exponent(opt.Geometry))
        };

        // n=1'de 100(1-(1-kt)^1) = 100·k·t → matris tükenene kadar Zero-order ile aynı eğri.
        // Fark: Hopfenberg tükenme sonrası %100'de tavanlanır, Zero-order sınırsızdır.
        if (opt.Geometry == HopfenbergGeometry.Slab)
            flags.Add(CoreText.T("NOT: Slab (n=1) geometrisinde Hopfenberg, matris tükenene kadar (t < 1/kHB) Zero-order ile aynı eğridir: F = 100·kHB·t. Tek farkı, tükenmeden sonra %100'de tavanlanmasıdır (Zero-order sınırsızdır). Gerçekten farklı bir mekanizma için silindir (n=2) veya küre (n=3) seçin."));

        // n=3'te 100(1-(1-kt)^3) Hixson-Crowell küp-kök yasasının ta kendisidir (Costa Eq. 25
        // ile Eq. 39, n=3). İki model aynı eğriyi verir, sıralamada aynı SS/AIC ile yan yana
        // durur ve Akaike ağırlığını aralarında bölüşür — kullanıcı bunu bilmeli.
        if (opt.Geometry == HopfenbergGeometry.Sphere)
            flags.Add(CoreText.T("NOT: Küre (n=3) geometrisinde Hopfenberg, Hixson-Crowell ile özdeştir (kHB = kHC). Sıralamada iki model aynı SS ve AIC ile görünür ve Akaike ağırlığını paylaşır; ikisini tek model olarak değerlendirin."));

        if (IsKarasuluGeometry(opt.Geometry))
            flags.Add(CoreText.T("NOT: n = {0:0.#} değeri Karasulu, Ertan & Köse (2000) önerisidir: Katzhendler denklemi farklı geometrili HPMC teofilin tabletlerine uyarlanmıştır. Costa & Sousa Lobo'nun klasik 1/2/3 setinde yoktur; ampirik geometri düzeltmesi olarak okuyun.", Exponent(opt.Geometry)));

        return flags;
    }

    public static string GeometryName(HopfenbergGeometry g) => g switch
    {
        HopfenbergGeometry.Slab => CoreText.T("Slab (ince tabaka)"),
        HopfenbergGeometry.Cylinder => CoreText.T("Silindir"),
        HopfenbergGeometry.Sphere => CoreText.T("Küre"),
        HopfenbergGeometry.HalfSphere => CoreText.T("Yarım küre"),
        HopfenbergGeometry.Triangle => CoreText.T("Üçgen"),
        _ => g.ToString()
    };

    /// <summary>
    /// Geometri üsteli n. 1/2/3 Hopfenberg (1976) ve Costa &amp; Sousa Lobo Eq. 39; 1,5 (yarım küre) ve
    /// 4 (üçgen) Karasulu, Ertan &amp; Köse (2000): Katzhendler ve ark. (1997) denklemi farklı geometrili
    /// HPMC teofilin tabletlerine uygulanmış, "bu n değerleri kinetik programlarda kullanılabilir" denmiş.
    /// </summary>
    public static double Exponent(HopfenbergGeometry g) => g switch
    {
        HopfenbergGeometry.Slab => 1,
        HopfenbergGeometry.HalfSphere => 1.5,
        HopfenbergGeometry.Cylinder => 2,
        HopfenbergGeometry.Sphere => 3,
        HopfenbergGeometry.Triangle => 4,
        _ => throw new ArgumentOutOfRangeException(nameof(g), g, "Bilinmeyen Hopfenberg geometrisi")
    };

    /// <summary>Costa &amp; Sousa Lobo'daki klasik üç geometri dışındakiler (Karasulu ve ark. 2000 önerileri).</summary>
    public static bool IsKarasuluGeometry(HopfenbergGeometry g)
        => g is HopfenbergGeometry.HalfSphere or HopfenbergGeometry.Triangle;
}

/// <summary>
/// Baker-Lonsdale: 3/2·(1 − (1 − F/100)^(2/3)) − F/100 = kBL·t (şartname §5.8). Costa Eq. 38.
/// Küresel matristen kontrollü salım. Kapalı denklem olduğu için F, sayısal kök bulmayla çözülür.
/// </summary>
public sealed class BakerLonsdaleModel : DissolutionModelBase
{
    public override string Name => "Baker-Lonsdale";
    public override string Equation => "3/2*(1-(1-F/100)^(2/3))-F/100 = kBL*t";
    public override IReadOnlyList<string> ParamNames => new[] { "kBL" };
    public override bool SupportsF0 => false;
    public override bool SupportsFmax => false;

    public override double[] LowerBounds => new[] { 0.0 };
    public override double[] UpperBounds => new[] { double.PositiveInfinity };

    /// <summary>g(F) = 3/2·(1 − (1 − F/100)^(2/3)) − F/100. [0,100] üzerinde monoton artan, g(100)=0.5.</summary>
    public static double G(double f)
    {
        double u = 1 - f / 100.0;
        if (u < 0) u = 0;
        return 1.5 * (1 - Math.Pow(u, 2.0 / 3.0)) - f / 100.0;
    }

    /// <summary>g'nin [0,100] aralığındaki üst sınırı: g(100) = 0.5.</summary>
    private const double GMax = 0.5;

    public override IReadOnlyList<Coefficient> Describe(double[] p) => new[]
    {
        new Coefficient("kBL", p[0], CoreText.T("1/zaman"), CoreText.T("Baker-Lonsdale salım hız sabiti"))
    };

    /// <summary>
    /// Tohum: y = g(F), x = t, <b>kesişimli</b> regresyonun eğimi → kBL.
    /// (Şartname "kesişimli/orijinden" diye belirsiz bırakır; DDSolver fikstürü kesişimli
    /// eğimle birebir tutar: 0.01787575341.)
    /// </summary>
    public override double[] InitialGuess(IReadOnlyList<double> t, IReadOnlyList<double> f, VariantOptions opt)
    {
        var (xs, ys) = SeedPoints(t, f, ti => ti, G, (_, fi) => fi >= 0 && fi <= 100);
        if (xs.Count < 2) return new[] { 0.01 };

        var (_, slope) = SeedRegression.WithIntercept(xs, ys);
        return new[] { IsFinite(slope) ? slope : 0.01 };
    }

    /// <summary>g(F) = kBL·t denklemini F ∈ [0,100] aralığında Brent ile çözer (şartname §4).</summary>
    public override double EvaluateCore(double t, double[] p, double ceiling, VariantOptions opt)
    {
        if (t <= 0) return 0;

        double target = p[0] * t;
        if (target <= 0) return 0;
        if (target >= GMax) return 100.0;   // matris tükenmiş

        try
        {
            if (Brent.TryFindRoot(f => G(f) - target, 0, 100, 1e-10, 200, out double root))
                return root;
        }
        catch { /* aşağıdaki bisection'a düş */ }

        // Yedek: basit bisection (g monoton artan olduğu için garanti yakınsar)
        double lo = 0, hi = 100;
        for (int i = 0; i < 200; i++)
        {
            double mid = (lo + hi) / 2;
            if (G(mid) < target) lo = mid; else hi = mid;
        }
        return (lo + hi) / 2;
    }

    /// <summary>Tx = g(x) / kBL.</summary>
    public override IReadOnlyList<SecondaryValue> SecondaryCore(double[] p, double ceiling, VariantOptions opt)
        => SolveTargets(ceiling, x => p[0] > 0 ? G(x) / p[0] : double.NaN);
}
