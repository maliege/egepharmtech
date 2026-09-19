#nullable enable
using EgePharmTech.Core.Localization;

namespace EgePharmTech.Core.Dissolution.Models;

/// <summary>Zero-order: F = k0·t (şartname §5.1). Costa &amp; Sousa Lobo Eq. 2.</summary>
public sealed class ZeroOrderModel : DissolutionModelBase
{
    public override string Name => "Zero-order";
    public override string Equation => "F = k0*t";
    public override IReadOnlyList<string> ParamNames => new[] { "k0" };
    public override bool SupportsF0 => true;    // sınırsız → F0 anlamlı
    public override bool SupportsFmax => false; // tavan yok

    public override double[] LowerBounds => new[] { 0.0 };
    public override double[] UpperBounds => new[] { double.PositiveInfinity };

    public override IReadOnlyList<Coefficient> Describe(double[] p) => new[]
    {
        new Coefficient("k0", p[0], CoreText.T("%/zaman"), CoreText.T("Sıfırıncı derece salım hız sabiti"))
    };

    /// <summary>Tohum: y=F, x=t, orijinden → k0 = Σ(t·F)/Σt².</summary>
    public override double[] InitialGuess(IReadOnlyList<double> t, IReadOnlyList<double> f, VariantOptions opt)
        => new[] { SeedRegression.ThroughOrigin(t, f) };

    public override double EvaluateCore(double t, double[] p, double ceiling, VariantOptions opt)
        => p[0] * t;

    public override IReadOnlyList<SecondaryValue> SecondaryCore(double[] p, double ceiling, VariantOptions opt)
        => SolveTargets(ceiling, x => p[0] > 0 ? x / p[0] : double.NaN);
}

/// <summary>First-order: F = 100·(1 − e^(−k1·t)) (şartname §5.2). Costa Eq. 12.</summary>
public sealed class FirstOrderModel : DissolutionModelBase
{
    public override string Name => "First-order";
    public override string Equation => "F = 100*(1-Exp(-k1*t))";
    public override IReadOnlyList<string> ParamNames => new[] { "k1" };
    public override bool SupportsF0 => false;  // doyuma gider → F0 tavanı bozar, Fmax kullan
    public override bool SupportsFmax => true;

    public override double[] LowerBounds => new[] { 0.0 };
    public override double[] UpperBounds => new[] { double.PositiveInfinity };

    public override IReadOnlyList<Coefficient> Describe(double[] p) => new[]
    {
        new Coefficient("k1", p[0], CoreText.T("1/zaman"), CoreText.T("Birinci derece salım hız sabiti"))
    };

    /// <summary>Tohum: y=ln(1 − F/tavan), x=t, orijinden → k1 = −Σxy/Σx².</summary>
    public override double[] InitialGuess(IReadOnlyList<double> t, IReadOnlyList<double> f, VariantOptions opt)
    {
        double ceil = opt.UseFmax ? SeedFmax(f) : DefaultCeiling;
        var (xs, ys) = SeedPoints(t, f, ti => ti, fi => SafeLog(1 - fi / ceil),
            (ti, fi) => fi > 0 && fi < ceil);
        return xs.Count >= 2 ? new[] { -SeedRegression.ThroughOrigin(xs, ys) } : new[] { 0.1 };
    }

    public override double EvaluateCore(double t, double[] p, double ceiling, VariantOptions opt)
        => ceiling * (1 - Math.Exp(-p[0] * t));

    public override IReadOnlyList<SecondaryValue> SecondaryCore(double[] p, double ceiling, VariantOptions opt)
        => SolveTargets(ceiling, x => p[0] > 0 ? -Math.Log(1 - x / ceiling) / p[0] : double.NaN);
}

/// <summary>Higuchi: F = kH·t^0.5 (şartname §5.3). Costa Eq. 22 (basitleştirilmiş Higuchi).</summary>
public sealed class HiguchiModel : DissolutionModelBase
{
    public override string Name => "Higuchi";
    public override string Equation => "F = kH*t^0.5";
    public override IReadOnlyList<string> ParamNames => new[] { "kH" };
    public override bool SupportsF0 => true;    // sınırsız → F0 anlamlı
    public override bool SupportsFmax => false;

    public override double[] LowerBounds => new[] { 0.0 };
    public override double[] UpperBounds => new[] { double.PositiveInfinity };

    public override IReadOnlyList<Coefficient> Describe(double[] p) => new[]
    {
        new Coefficient("kH", p[0], CoreText.T("%·zaman^-0.5"), CoreText.T("Higuchi difüzyon sabiti"))
    };

    /// <summary>Tohum: y=F, x=√t, orijinden → kH = Σxy/Σx².</summary>
    public override double[] InitialGuess(IReadOnlyList<double> t, IReadOnlyList<double> f, VariantOptions opt)
    {
        var (xs, ys) = SeedPoints(t, f, Math.Sqrt, fi => fi, (ti, _) => ti >= 0);
        return new[] { SeedRegression.ThroughOrigin(xs, ys) };
    }

    public override double EvaluateCore(double t, double[] p, double ceiling, VariantOptions opt)
        => t < 0 ? 0 : p[0] * Math.Sqrt(t);

    public override IReadOnlyList<SecondaryValue> SecondaryCore(double[] p, double ceiling, VariantOptions opt)
        => SolveTargets(ceiling, x => p[0] > 0 ? Math.Pow(x / p[0], 2) : double.NaN);
}

/// <summary>
/// Hixson-Crowell: F = 100·(1 − (1 − kHC·t)³) (şartname §5.4). Costa Eq. 25.
/// Küp-kök yasası: çözünen partikülün yüzey/hacim oranı korunur.
/// </summary>
public sealed class HixsonCrowellModel : DissolutionModelBase
{
    public override string Name => "Hixson-Crowell";
    public override string Equation => "F = 100*(1-(1-kHC*t)^3)";
    public override IReadOnlyList<string> ParamNames => new[] { "kHC" };
    public override bool SupportsF0 => false;   // 100'de tavanlı → additive F0 uygun değil
    public override bool SupportsFmax => false;

    public override double[] LowerBounds => new[] { 0.0 };
    public override double[] UpperBounds => new[] { double.PositiveInfinity };

    public override IReadOnlyList<Coefficient> Describe(double[] p) => new[]
    {
        new Coefficient("kHC", p[0], CoreText.T("1/zaman"), CoreText.T("Hixson-Crowell erozyon hız sabiti"))
    };

    /// <summary>Tohum: y = 1 − (1 − F/100)^(1/3), x=t, orijinden.</summary>
    public override double[] InitialGuess(IReadOnlyList<double> t, IReadOnlyList<double> f, VariantOptions opt)
    {
        var (xs, ys) = SeedPoints(t, f, ti => ti,
            fi => 1 - Math.Cbrt(1 - fi / DefaultCeiling),
            (_, fi) => fi < DefaultCeiling);
        return new[] { SeedRegression.ThroughOrigin(xs, ys) };
    }

    public override double EvaluateCore(double t, double[] p, double ceiling, VariantOptions opt)
    {
        double u = 1 - p[0] * t;
        // u < 0: partikül tükenmiş → salım tavanda kalır (aksi halde eğri geri döner)
        if (u <= 0) return ceiling;
        return ceiling * (1 - u * u * u);
    }

    public override IReadOnlyList<SecondaryValue> SecondaryCore(double[] p, double ceiling, VariantOptions opt)
        => SolveTargets(ceiling, x => p[0] > 0
            ? (1 - Math.Cbrt(1 - x / ceiling)) / p[0]
            : double.NaN);
}
