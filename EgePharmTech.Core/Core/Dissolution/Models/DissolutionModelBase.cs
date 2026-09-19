#nullable enable
namespace EgePharmTech.Core.Dissolution.Models;

/// <summary>
/// Somut modeller için ortak taban.
///
/// <b>Tavan (ceiling) mekanizması:</b> Doyuma giden modellerde denklemdeki sabit 100,
/// Fmax varyantında Fmax ile değişir (şartname §7). Denklemi iki kez yazmamak için
/// taban modeller <see cref="EvaluateCore"/>'u tavanı parametre alacak şekilde yazar;
/// çıplak model 100 kullanır, <see cref="VariantModel"/> ise fit edilen Fmax'ı geçirir.
/// Tavansız modeller (Zero-order, Higuchi, KP…) tavanı yok sayar.
/// </summary>
public abstract class DissolutionModelBase : IDissolutionModel
{
    /// <summary>Tavansız modellerin varsayılan tavanı; anlamsız olduğu yerde yok sayılır.</summary>
    public const double DefaultCeiling = 100.0;

    public abstract string Name { get; }
    public abstract string Equation { get; }
    public abstract IReadOnlyList<string> ParamNames { get; }
    public abstract IReadOnlyList<Coefficient> Describe(double[] p);

    public virtual bool SupportsF0 => false;
    public virtual bool SupportsTlag => true;   // şartname §7: Tlag TÜM modeller için geçerli
    public virtual bool SupportsFmax => false;

    public abstract double[] LowerBounds { get; }

    /// <summary>
    /// F0 (burst) varyantının alt sınırı. Varsayılan sınırsız: Zero-order/Higuchi'de küçük negatif
    /// F0 gecikmeyi taklit eden meşru bir fittir. Güç yasasında ise F0→−∞, kKP→+∞, n→0 üçlüsü
    /// logaritmik bir eğri üretip SS'i düşürür (dejenere) — KP bunu 0 ile kapatır.
    /// </summary>
    public virtual double F0LowerBound => double.NegativeInfinity;
    public abstract double[] UpperBounds { get; }

    public abstract double[] InitialGuess(IReadOnlyList<double> t, IReadOnlyList<double> f, VariantOptions opt);

    /// <summary>Taban denklem. <paramref name="ceiling"/> yalnız doyuma giden modellerde anlamlıdır.</summary>
    public abstract double EvaluateCore(double t, double[] p, double ceiling, VariantOptions opt);

    /// <summary>Taban ikincil parametreler. <paramref name="ceiling"/> ≤ hedef ise "Non Calc".</summary>
    public abstract IReadOnlyList<SecondaryValue> SecondaryCore(double[] p, double ceiling, VariantOptions opt);

    public double Evaluate(double t, double[] p, VariantOptions opt)
        => EvaluateCore(t, p, DefaultCeiling, opt);

    public IReadOnlyList<SecondaryValue> Secondary(double[] p, VariantOptions opt)
        => SecondaryCore(p, DefaultCeiling, opt);

    public virtual (double[] T, double[] F) SelectPoints(
        IReadOnlyList<double> t, IReadOnlyList<double> f, VariantOptions opt)
        => (t.ToArray(), f.ToArray());

    public virtual IReadOnlyList<string> Flags(double[] p, VariantOptions opt)
        => Array.Empty<string>();

    // ---------------- ortak yardımcılar ----------------

    /// <summary>Şartname §5'in standart ikincil hedefleri.</summary>
    protected static readonly double[] Targets = { 25, 50, 75, 80, 90 };
    protected static readonly string[] TargetNames = { "T25", "T50", "T75", "T80", "T90" };

    /// <summary>
    /// Hedef listesini bir ters fonksiyonla çözer. Tavan aşılırsa ya da sonuç geçersizse
    /// "Non Calc" (null) döner (şartname §4).
    /// </summary>
    protected static IReadOnlyList<SecondaryValue> SolveTargets(
        double ceiling, Func<double, double> inverse)
    {
        var res = new List<SecondaryValue>(Targets.Length);
        for (int i = 0; i < Targets.Length; i++)
        {
            double x = Targets[i];
            double? v = null;
            if (x < ceiling)   // şartname §7: x ≥ Fmax → ulaşılamaz
            {
                double t = inverse(x);
                if (IsFinite(t) && t >= 0) v = t;
            }
            res.Add(new SecondaryValue(TargetNames[i], v));
        }
        return res;
    }

    /// <summary>Tümü "Non Calc" (ör. Makoid-Banakar: kapalı-form ters yok, eğri tepe yapar).</summary>
    protected static IReadOnlyList<SecondaryValue> AllNonCalc()
        => TargetNames.Select(n => new SecondaryValue(n, null)).ToArray();

    protected static bool IsFinite(double v) => !double.IsNaN(v) && !double.IsInfinity(v);

    /// <summary>ln için güvenli: pozitif olmayan girdide NaN döner.</summary>
    protected static double SafeLog(double v) => v > 0 ? Math.Log(v) : double.NaN;

    /// <summary>Doğrusallaştırma tohumu için geçerli noktaları süzer (şartname §4.1).</summary>
    protected static (List<double> X, List<double> Y) SeedPoints(
        IReadOnlyList<double> t, IReadOnlyList<double> f,
        Func<double, double> fx, Func<double, double> fy,
        Func<double, double, bool>? valid = null)
    {
        var xs = new List<double>();
        var ys = new List<double>();
        for (int i = 0; i < t.Count; i++)
        {
            if (valid is not null && !valid(t[i], f[i])) continue;
            double x = fx(t[i]), y = fy(f[i]);
            if (IsFinite(x) && IsFinite(y)) { xs.Add(x); ys.Add(y); }
        }
        return (xs, ys);
    }

    /// <summary>Fmax tohumu: 1.05 × max(F) (DDSolver ile uyumlu, fikstürden çözüldü).</summary>
    protected static double SeedFmax(IReadOnlyList<double> f) => 1.05 * f.Max();

    /// <summary>Tlag tohumu: 0.4 × min(t) (DDSolver ile uyumlu).</summary>
    protected static double SeedTlag(IReadOnlyList<double> t) => 0.4 * t.Min();

    /// <summary>F0 tohumu: 0.4 × min(F) (DDSolver ile uyumlu).</summary>
    protected static double SeedF0(IReadOnlyList<double> f) => 0.4 * f.Min();
}
