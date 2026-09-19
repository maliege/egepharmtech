#nullable enable
using EgePharmTech.Core.Localization;

namespace EgePharmTech.Core.Dissolution.Models;

/// <summary>
/// Korsmeyer-Peppas (Power Law): F = kKP·t^n (şartname §5.5). Costa Eq. 26/30.
///
/// <b>F ≤ 60 kısıtı:</b> Costa &amp; Sousa Lobo — "n'in belirlenmesinde yalnızca eğrinin
/// Mt/M∞ &lt; 0.6 olan kısmı kullanılmalıdır." Bu kısıt varsayılan olarak uygulanır;
/// <see cref="VariantOptions.KpUseAllPoints"/> ile kapatılabilir.
/// Not: DDSolver fikstürleri bu filtreyi UYGULAMAZ, dolayısıyla varsayılan ayarda
/// KP beklentileri bilinçli olarak tutmaz (bkz. BIRIM_TEST_README "Bilinen sapmalar").
/// </summary>
public sealed class KorsmeyerPeppasModel : DissolutionModelBase
{
    /// <summary>Costa &amp; Sousa Lobo: n yalnız F &lt; %60 bölgesinden belirlenmelidir.</summary>
    public const double MaxReleaseForFit = 60.0;

    public override string Name => "Korsmeyer-Peppas";
    public override string Equation => "F = kKP*t^n";
    public override IReadOnlyList<string> ParamNames => new[] { "kKP", "n" };
    public override bool SupportsF0 => true;    // sınırsız güç yasası → F0 (burst) anlamlı, Costa Eq. 33
    public override bool SupportsFmax => false;

    /// <summary>
    /// F0 ≥ 0. Sınırsız bırakılırsa çözücü F0 = −430, kKP = 438, n = 0.06 gibi bir "çözüm" bulur:
    /// F0 + kKP·t^n ≈ (F0 + kKP) + kKP·n·ln t, yani logaritmik eğri. SS düşer ama katsayıların
    /// hiçbiri anlamlı değildir (DDSolver referans verisinde gözlendi). Negatif burst fiziksel değildir.
    /// </summary>
    public override double F0LowerBound => 0.0;
    public override double[] LowerBounds => new[] { 0.0, 0.0 };
    public override double[] UpperBounds => new[] { double.PositiveInfinity, double.PositiveInfinity };

    public override IReadOnlyList<Coefficient> Describe(double[] p) => new[]
    {
        new Coefficient("kKP", p[0], CoreText.T("%/zaman^n"), CoreText.T("Salım hız sabiti (yapısal/geometrik)")),
        new Coefficient("n", p[1], "-", CoreText.T("Salım üsteli (difüzyon mekanizması göstergesi)"))
    };

    /// <summary>Costa &amp; Sousa Lobo Tablo 1 uyarınca n'in mekanizma yorumu.</summary>
    public static string InterpretN(double n, HopfenbergGeometry geometry = HopfenbergGeometry.Slab)
    {
        // Costa & Sousa Lobo Tablo 1 — geometriye göre Fickian / Case-II eşikleri:
        //   ince tabaka (slab) 0.50 / 1.00, silindir 0.45 / 0.89, küre 0.43 / 0.85.
        // UI varsayılanı küre olduğu için küre satırının eksik olması, KP yorumunun
        // sessizce slab eşikleriyle yapılmasına yol açıyordu.
        var (fick, caseII) = geometry switch
        {
            HopfenbergGeometry.Cylinder => (0.45, 0.89),
            HopfenbergGeometry.Sphere => (0.43, 0.85),
            _ => (0.5, 1.0)
        };
        const double tol = 0.02;

        if (Math.Abs(n - fick) <= tol) return CoreText.T("Fickian difüzyon (n≈{0})", fick);
        if (n < fick) return CoreText.T("Yarı-Fickian / n < {0}", fick);
        if (Math.Abs(n - caseII) <= tol) return CoreText.T("Case-II taşınım (n≈{0}, sıfırıncı derece salım)", caseII);
        if (n > caseII) return CoreText.T("Süper Case-II taşınım (n > {0})", caseII);
        return CoreText.T("Anormal (anomalous) taşınım ({0} < n < {1})", fick, caseII);
    }

    /// <summary>Tohum: y=ln F, x=ln t, kesişimli → n = eğim, kKP = e^(kesişim).</summary>
    public override double[] InitialGuess(IReadOnlyList<double> t, IReadOnlyList<double> f, VariantOptions opt)
    {
        var (xs, ys) = SeedPoints(t, f, SafeLog, SafeLog, (ti, fi) => ti > 0 && fi > 0);
        if (xs.Count < 2) return new[] { 10.0, 0.5 };

        var (intercept, slope) = SeedRegression.WithIntercept(xs, ys);
        double kkp = Math.Exp(intercept);
        return new[] { IsFinite(kkp) ? kkp : 10.0, IsFinite(slope) ? slope : 0.5 };
    }

    /// <summary>Costa &amp; Sousa Lobo: yalnız F ≤ 60 bölgesi (varsayılan).</summary>
    public override (double[] T, double[] F) SelectPoints(
        IReadOnlyList<double> t, IReadOnlyList<double> f, VariantOptions opt)
    {
        if (opt.KpUseAllPoints) return (t.ToArray(), f.ToArray());

        var ts = new List<double>();
        var fs = new List<double>();
        for (int i = 0; i < t.Count; i++)
            if (f[i] <= MaxReleaseForFit) { ts.Add(t[i]); fs.Add(f[i]); }

        // Filtre 2 noktadan az bırakırsa uygulanamaz → tüm noktalara düş
        return ts.Count >= 2 ? (ts.ToArray(), fs.ToArray()) : (t.ToArray(), f.ToArray());
    }

    public override double EvaluateCore(double t, double[] p, double ceiling, VariantOptions opt)
        => t <= 0 ? 0 : p[0] * Math.Pow(t, p[1]);

    public override IReadOnlyList<SecondaryValue> SecondaryCore(double[] p, double ceiling, VariantOptions opt)
        => SolveTargets(ceiling, x => p[0] > 0 && p[1] > 0
            ? Math.Pow(x / p[0], 1.0 / p[1])
            : double.NaN);

    public override IReadOnlyList<string> Flags(double[] p, VariantOptions opt)
    {
        // Costa & Sousa Lobo Tablo 1 yalnız slab/silindir/küre eşiği verir; yarım küre ve üçgen
        // (Karasulu geometrileri) için slab eşikleri kullanılır ve bu açıkça söylenir.
        var thresholdNote = HopfenbergModel.IsKarasuluGeometry(opt.Geometry)
            ? CoreText.T("eşikler slab (ince tabaka) geometrisine göre — {0} için Costa & Sousa Lobo Tablo 1'de eşik yok", HopfenbergModel.GeometryName(opt.Geometry).ToLowerInvariant())
            : CoreText.T("eşikler {0} geometrisine göre (Costa & Sousa Lobo, Tablo 1)", HopfenbergModel.GeometryName(opt.Geometry).ToLowerInvariant());
        var flags = new List<string> { $"{InterpretN(p[1], opt.Geometry)} — {thresholdNote}" };
        if (p[1] < 0.1)
            flags.Add(CoreText.T("UYARI: n = {0:0.###} ≈ 0 — güç yasası dejenere: eğri neredeyse sabit (ya da F0 varyantında logaritmik). Mekanizma yorumu geçersizdir.", p[1]));
        if (opt.KpUseAllPoints)
            flags.Add(CoreText.T("UYARI: Tüm noktalarla fit edildi. Costa & Sousa Lobo, n'in yalnız F<%60 bölgesinden belirlenmesini şart koşar; n mekanizma yorumu güvenilmez olabilir."));
        return flags;
    }
}

/// <summary>
/// Peppas-Sahlin: F = k1·t^m + k2·t^(2m) (şartname §5.10).
/// k1 = Fick difüzyonu katkısı, k2 = Case-II relaksasyon katkısı.
/// </summary>
public sealed class PeppasSahlinModel : DissolutionModelBase
{
    public override string Name => "Peppas-Sahlin";
    public override string Equation => "F = k1*t^m + k2*t^(2m)";
    public override IReadOnlyList<string> ParamNames => new[] { "k1", "k2", "m" };
    public override bool SupportsF0 => false;   // sınırsız toplam; F0 yerine Tlag
    public override bool SupportsFmax => false;

    // k1/k2 serbest (k2 negatif olabilir: relaksasyon katkısı ters işaretli çıkabilir), m ≥ 0
    public override double[] LowerBounds => new[] { double.NegativeInfinity, double.NegativeInfinity, 0.0 };
    public override double[] UpperBounds => new[] { double.PositiveInfinity, double.PositiveInfinity, double.PositiveInfinity };

    public override IReadOnlyList<Coefficient> Describe(double[] p) => new[]
    {
        new Coefficient("k1", p[0], CoreText.T("%/zaman^m"), CoreText.T("Fick difüzyonu katkısı")),
        new Coefficient("k2", p[1], CoreText.T("%/zaman^2m"), CoreText.T("Case-II relaksasyon katkısı")),
        new Coefficient("m", p[2], "-", CoreText.T("Fick difüzyon üsteli"))
    };

    /// <summary>Tohum: m = 0.45 sabit; F ~ [t^m, t^(2m)] kesişimsiz çoklu regresyon.</summary>
    public override double[] InitialGuess(IReadOnlyList<double> t, IReadOnlyList<double> f, VariantOptions opt)
    {
        const double m = 0.45;
        var c1 = t.Select(ti => ti > 0 ? Math.Pow(ti, m) : 0).ToArray();
        var c2 = t.Select(ti => ti > 0 ? Math.Pow(ti, 2 * m) : 0).ToArray();
        var k = SeedRegression.Multi(new[] { c1, c2 }, f, withIntercept: false);
        return new[] { k[0], k[1], m };
    }

    public override double EvaluateCore(double t, double[] p, double ceiling, VariantOptions opt)
    {
        if (t <= 0) return 0;
        return p[0] * Math.Pow(t, p[2]) + p[1] * Math.Pow(t, 2 * p[2]);
    }

    public override IReadOnlyList<string> Flags(double[] p, VariantOptions opt)
    {
        var flags = new List<string>();
        // KP'deki n≈0 dejenerasyonunun aynısı: t^m ve t^2m → 1'e yaklaşır, k1 ve k2 birbirini telafi eder.
        if (p[2] < 0.1)
            flags.Add(CoreText.T("UYARI: m = {0:0.###} ≈ 0 — Peppas-Sahlin dejenere: t^m ve t^(2m) terimleri ayrışmıyor, k1/k2 birbirini telafi ediyor. Difüzyon/relaksasyon oranı yorumlanamaz.", p[2]));
        if (p[0] < 0 && p[1] < 0)
            flags.Add(CoreText.T("UYARI: k1 < 0 ve k2 < 0 — eğri azalıyor; model bu veriye uygun değil."));
        return flags;
    }

    /// <summary>Tx = ((−k1 + √(k1² + 4·k2·x)) / (2·k2))^(1/m); k2→0 iken saf güç yasasına düşer.</summary>
    public override IReadOnlyList<SecondaryValue> SecondaryCore(double[] p, double ceiling, VariantOptions opt)
        => SolveTargets(ceiling, x =>
        {
            double k1 = p[0], k2 = p[1], m = p[2];
            if (m <= 0) return double.NaN;

            if (Math.Abs(k2) < 1e-12)
                return k1 > 0 ? Math.Pow(x / k1, 1.0 / m) : double.NaN;

            double disc = k1 * k1 + 4 * k2 * x;
            if (disc < 0) return double.NaN;

            double u = (-k1 + Math.Sqrt(disc)) / (2 * k2);
            return u > 0 ? Math.Pow(u, 1.0 / m) : double.NaN;
        });
}

/// <summary>Peppas-Sahlin-2: F = k1·t^0.5 + k2·t (şartname §5.11). m = 0.5 sabitlenmiş hâli.</summary>
public sealed class PeppasSahlin2Model : DissolutionModelBase
{
    public override string Name => "Peppas-Sahlin-2";
    public override string Equation => "F = k1*t^0.5 + k2*t";
    public override IReadOnlyList<string> ParamNames => new[] { "k1", "k2" };
    public override bool SupportsF0 => false;
    public override bool SupportsFmax => false;

    public override double[] LowerBounds => new[] { double.NegativeInfinity, double.NegativeInfinity };
    public override double[] UpperBounds => new[] { double.PositiveInfinity, double.PositiveInfinity };

    public override IReadOnlyList<Coefficient> Describe(double[] p) => new[]
    {
        new Coefficient("k1", p[0], CoreText.T("%/zaman^0.5"), CoreText.T("Fick difüzyonu katkısı")),
        new Coefficient("k2", p[1], CoreText.T("%/zaman"), CoreText.T("Case-II relaksasyon katkısı"))
    };

    /// <summary>Tohum: F ~ [√t, t] kesişimsiz. Model katsayılarda doğrusal → OLS global optimumdur.</summary>
    public override double[] InitialGuess(IReadOnlyList<double> t, IReadOnlyList<double> f, VariantOptions opt)
    {
        var c1 = t.Select(ti => ti > 0 ? Math.Sqrt(ti) : 0).ToArray();
        var c2 = t.ToArray();
        var k = SeedRegression.Multi(new[] { c1, c2 }, f, withIntercept: false);
        return new[] { k[0], k[1] };
    }

    public override double EvaluateCore(double t, double[] p, double ceiling, VariantOptions opt)
        => t <= 0 ? 0 : p[0] * Math.Sqrt(t) + p[1] * t;

    /// <summary>Tx = ((−k1 + √(k1² + 4·k2·x)) / (2·k2))².</summary>
    public override IReadOnlyList<SecondaryValue> SecondaryCore(double[] p, double ceiling, VariantOptions opt)
        => SolveTargets(ceiling, x =>
        {
            double k1 = p[0], k2 = p[1];
            if (Math.Abs(k2) < 1e-12)
                return k1 > 0 ? Math.Pow(x / k1, 2) : double.NaN;

            double disc = k1 * k1 + 4 * k2 * x;
            if (disc < 0) return double.NaN;

            double u = (-k1 + Math.Sqrt(disc)) / (2 * k2);
            return u > 0 ? u * u : double.NaN;
        });
}


/// <summary>
/// Makoid-Banakar: F = kMB·t^n·e^(−k·t) (şartname §5.9).
/// Eğri tepe yapıp düşebildiği için kapalı-form ters yoktur → T25..T90 "Non Calc".
/// </summary>
public sealed class MakoidBanakarModel : DissolutionModelBase
{
    public override string Name => "Makoid-Banakar";
    public override string Equation => "F = kMB*t^n*Exp(-k*t)";
    public override IReadOnlyList<string> ParamNames => new[] { "kMB", "n", "k" };
    public override bool SupportsF0 => false;   // tepe yapan eğri
    public override bool SupportsFmax => false;

    public override double[] LowerBounds => new[] { 0.0, 0.0, 0.0 };
    public override double[] UpperBounds => new[] { double.PositiveInfinity, double.PositiveInfinity, double.PositiveInfinity };

    public override IReadOnlyList<Coefficient> Describe(double[] p) => new[]
    {
        new Coefficient("kMB", p[0], CoreText.T("%/zaman^n"), CoreText.T("Makoid-Banakar hız sabiti")),
        new Coefficient("n", p[1], "-", CoreText.T("Salım üsteli")),
        new Coefficient("k", p[2], CoreText.T("1/zaman"), CoreText.T("Sönüm (decay) sabiti"))
    };

    /// <summary>Tohum: ln F ~ [ln t, t] + sabit → kMB = e^sabit, n = katsayı(ln t), k = −katsayı(t).</summary>
    public override double[] InitialGuess(IReadOnlyList<double> t, IReadOnlyList<double> f, VariantOptions opt)
    {
        var lnT = new List<double>();
        var tt = new List<double>();
        var lnF = new List<double>();
        for (int i = 0; i < t.Count; i++)
        {
            if (t[i] <= 0 || f[i] <= 0) continue;
            lnT.Add(Math.Log(t[i]));
            tt.Add(t[i]);
            lnF.Add(Math.Log(f[i]));
        }
        if (lnT.Count < 3) return new[] { 10.0, 1.0, 0.01 };

        var c = SeedRegression.Multi(new[] { lnT.ToArray(), tt.ToArray() }, lnF, withIntercept: true);
        double kmb = Math.Exp(c[2]);
        double n = c[0];
        double k = -c[1];
        return new[]
        {
            IsFinite(kmb) ? kmb : 10.0,
            IsFinite(n) ? Math.Max(0, n) : 1.0,
            IsFinite(k) ? Math.Max(0, k) : 0.01
        };
    }

    public override double EvaluateCore(double t, double[] p, double ceiling, VariantOptions opt)
        => t <= 0 ? 0 : p[0] * Math.Pow(t, p[1]) * Math.Exp(-p[2] * t);

    /// <summary>
    /// Kapalı-form ters yok; DDSolver gibi sayısal çözülür. Eğri t* = n/k'de tepe yapar
    /// (F* = kMB·t*^n·e^(−n)); hedef tepe değerini aşıyorsa "Non Calc", aksi halde [0, t*]
    /// yükselen dalında ikiye bölme. k ≤ 0 ise eğri monoton artar; aralık genişletilerek çözülür.
    /// </summary>
    public override IReadOnlyList<SecondaryValue> SecondaryCore(double[] p, double ceiling, VariantOptions opt)
        => SolveTargets(ceiling, x =>
        {
            double kmb = p[0], n = p[1], k = p[2];
            if (kmb <= 0 || n <= 0) return double.NaN;
            double F(double t) => EvaluateCore(t, p, ceiling, opt);
            double hi;
            if (k > 0)
            {
                hi = n / k;                       // tepe
                if (F(hi) < x) return double.NaN; // hedefe hiç ulaşılmıyor
            }
            else
            {
                hi = 1;
                for (int i = 0; i < 200 && F(hi) < x; i++) hi *= 2;
                if (F(hi) < x) return double.NaN;
            }
            double lo = 0;
            for (int i = 0; i < 200; i++)
            {
                double mid = 0.5 * (lo + hi);
                if (F(mid) < x) lo = mid; else hi = mid;
            }
            return 0.5 * (lo + hi);
        });

    public override IReadOnlyList<string> Flags(double[] p, VariantOptions opt)
    {
        var flags = new List<string>();
        // k→0 iken e^(−k·t)→1: model Korsmeyer-Peppas'a çöker; fazladan parametre yalnız AIC'i kötüleştirir.
        if (p[2] < 1e-6)
            flags.Add(CoreText.T("NOT: k ≈ 0 — sönüm terimi etkisiz; Makoid-Banakar bu veride Korsmeyer-Peppas'a (tüm noktalar) indirgendi. Eğride tepe/düşüş yoksa bu modelin katkısı yoktur."));
        else
            flags.Add(CoreText.T("Tepe: t* = n/k = {0:G4}, F* = %{1:0.#} — sonrasında eğri düşer (salım verisinde fiziksel değil; ölçüm artefaktı olabilir).", p[1] / p[2], EvaluateCore(p[1] / p[2], p, DefaultCeiling, opt)));
        return flags;
    }
}
