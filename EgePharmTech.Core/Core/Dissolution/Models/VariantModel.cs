#nullable enable
using EgePharmTech.Core.Localization;

namespace EgePharmTech.Core.Dissolution.Models;

/// <summary>
/// Taban modeli F0 / Tlag / Fmax varyantlarıyla sarmalayan dekoratör (şartname §7).
/// Taban denklemler tek yerde (taban modelde) kalır; burada yalnız dönüşüm uygulanır.
///
/// Parametre vektörü: <c>[taban katsayılar..., (F0), (Tlag), (Fmax)]</c> — bu sırayla,
/// yalnız etkin olan varyantlar eklenir.
///
/// Kurallar (şartname §7):
///  - <b>Tlag</b> (tüm modeller): t → (t − Tlag); t &lt; Tlag için F = 0; Tx += Tlag.
///  - <b>F0</b> (yalnız tavansız modeller): F → F0 + taban.
///  - <b>Fmax</b> (yalnız doyuma giden modeller): denklemdeki 100 → Fmax; x ≥ Fmax → "Non Calc".
/// </summary>
public sealed class VariantModel : DissolutionModelBase
{
    private readonly DissolutionModelBase _base;
    private readonly bool _f0, _tlag, _fmax;

    public VariantModel(DissolutionModelBase baseModel, VariantOptions opt)
    {
        _base = baseModel;
        _f0 = opt.UseF0;
        _tlag = opt.UseTlag;
        _fmax = opt.UseFmax;

        // Şartname §7 kuralı: doyuma giden bir modelde eksik salımı F0 ile değil Fmax ile
        // modelle. Additive F0 asimptotu 100+F0 > %100 yapar → bilimsel olarak yanlış.
        if (_f0 && !baseModel.SupportsF0)
            throw new ArgumentException(
                CoreText.T("{0}: F0 varyantı desteklenmiyor. Doyuma giden/tavanlı modellerde eksik salım F0 ile değil Fmax ile modellenir (şartname §7); additive F0 asimptotu %100'ün üzerine çıkarır.", baseModel.Name));

        if (_tlag && !baseModel.SupportsTlag)
            throw new ArgumentException(CoreText.T("{0}: Tlag varyantı desteklenmiyor.", baseModel.Name));

        if (_fmax && !baseModel.SupportsFmax)
            throw new ArgumentException(
                CoreText.T("{0}: Fmax varyantı desteklenmiyor (model doyuma gitmiyor ya da Fmax zaten denkleminde).", baseModel.Name));
    }

    // ---- kimlik ----

    public override string Name
    {
        get
        {
            var parts = new List<string>();
            if (_f0) parts.Add("F0");
            if (_tlag) parts.Add("Tlag");
            if (_fmax) parts.Add("Fmax");
            if (parts.Count == 0) return _base.Name;
            // Taban adı zaten parantezliyse ("Gompertz (Fmax)") ikinci parantez açmadan birleştir
            return _base.Name.EndsWith(')')
                ? $"{_base.Name[..^1]},{string.Join(",", parts)})"
                : $"{_base.Name} ({string.Join(",", parts)})";
        }
    }

    public override string Equation
    {
        get
        {
            string eq = _base.Equation;
            if (_fmax) eq = eq.Replace("100*", "Fmax*").Replace("100 *", "Fmax*");
            if (_tlag) eq = eq.Replace("t^", "(t-Tlag)^").Replace("*t", "*(t-Tlag)");
            if (_f0) eq = eq.Replace("F = ", "F = F0 + ");
            return eq;
        }
    }

    // ---- parametre düzeni ----

    private int BaseCount => _base.ParamNames.Count;
    private int IdxF0 => BaseCount;
    private int IdxTlag => BaseCount + (_f0 ? 1 : 0);
    private int IdxFmax => BaseCount + (_f0 ? 1 : 0) + (_tlag ? 1 : 0);

    public override IReadOnlyList<string> ParamNames
    {
        get
        {
            var names = new List<string>(_base.ParamNames);
            if (_f0) names.Add("F0");
            if (_tlag) names.Add("Tlag");
            if (_fmax) names.Add("Fmax");
            return names;
        }
    }

    public override bool SupportsF0 => false;    // zaten uygulanmış
    public override bool SupportsTlag => false;
    public override bool SupportsFmax => false;

    public override double[] LowerBounds
    {
        get
        {
            var b = new List<double>(_base.LowerBounds);
            if (_f0) b.Add(_base.F0LowerBound);            // çoğunda −∞ (Higuchi F0 negatif çıkabilir); KP'de 0
            if (_tlag) b.Add(double.NegativeInfinity);     // şartname §4: Tlag serbest
            if (_fmax) b.Add(0.0);
            return b.ToArray();
        }
    }

    public override double[] UpperBounds
    {
        get
        {
            var b = new List<double>(_base.UpperBounds);
            if (_f0) b.Add(double.PositiveInfinity);
            if (_tlag) b.Add(double.PositiveInfinity);
            if (_fmax) b.Add(double.PositiveInfinity);
            return b.ToArray();
        }
    }

    private double[] BaseParams(double[] p) => p.Take(BaseCount).ToArray();
    private double Ceiling(double[] p) => _fmax ? p[IdxFmax] : DefaultCeiling;

    // ---- davranış ----

    public override double[] InitialGuess(IReadOnlyList<double> t, IReadOnlyList<double> f, VariantOptions opt)
    {
        var seed = new List<double>(_base.InitialGuess(t, f, opt));
        if (_f0) seed.Add(SeedF0(f));
        if (_tlag) seed.Add(SeedTlag(t));
        if (_fmax) seed.Add(SeedFmax(f));
        return seed.ToArray();
    }

    public override double EvaluateCore(double t, double[] p, double ceiling, VariantOptions opt)
    {
        double tEff = _tlag ? t - p[IdxTlag] : t;
        if (tEff < 0) return 0;   // şartname §4: t < Tlag için F = 0

        double v = _base.EvaluateCore(tEff, BaseParams(p), Ceiling(p), opt);
        if (_f0) v += p[IdxF0];
        return v;
    }

    public override IReadOnlyList<SecondaryValue> SecondaryCore(double[] p, double ceiling, VariantOptions opt)
    {
        double lag = _tlag ? p[IdxTlag] : 0;

        // F0 varyantında hedef, taban eğrinin F0 kadar altına kayar: taban(x − F0) çözülür.
        // Taban modelin kapalı-form tersi yalnız sabit hedefler için yazıldığından
        // kaydırılmış hedef sayısal olarak çözülür.
        if (_f0)
        {
            double f0 = p[IdxF0];
            double ceil = Ceiling(p);
            var baseP = BaseParams(p);

            return Targets.Zip(TargetNames, (target, name) =>
            {
                double residual = target - f0;
                if (residual <= 0 || residual >= ceil)
                    return new SecondaryValue(name, null);

                double tx = SolveNumerically(baseP, ceil, residual, opt);
                return new SecondaryValue(name, IsFinite(tx) && tx >= 0 ? tx + lag : null);
            }).ToArray();
        }

        // F0 yoksa taban ikincilleri + Tlag kaydırması yeterli
        var baseSec = _base.SecondaryCore(BaseParams(p), Ceiling(p), opt);
        return baseSec
            .Select(s => new SecondaryValue(s.Symbol, s.Value.HasValue ? s.Value + lag : null))
            .ToArray();
    }

    /// <summary>
    /// F0 varyantında kaydırılmış hedef için taban eğrisinin tersini sayısal olarak bulur
    /// (taban modelin kapalı-form tersi yalnız sabit hedeflerde kullanılabildiği için).
    /// </summary>
    private double SolveNumerically(double[] baseP, double ceiling, double target, VariantOptions opt)
    {
        // Eğri monoton artan varsayımıyla üstel arama + bisection
        double hi = 1.0;
        for (int i = 0; i < 200 && _base.EvaluateCore(hi, baseP, ceiling, opt) < target; i++)
        {
            hi *= 2;
            if (hi > 1e12) return double.NaN;
        }
        double lo = 0;
        for (int i = 0; i < 200; i++)
        {
            double mid = (lo + hi) / 2;
            if (_base.EvaluateCore(mid, baseP, ceiling, opt) < target) lo = mid; else hi = mid;
        }
        return (lo + hi) / 2;
    }

    public override IReadOnlyList<Coefficient> Describe(double[] p)
    {
        var list = new List<Coefficient>(_base.Describe(BaseParams(p)));
        if (_f0) list.Add(new Coefficient("F0", p[IdxF0], "%", CoreText.T("Başlangıç salımı (burst)")));
        if (_tlag) list.Add(new Coefficient("Tlag", p[IdxTlag], CoreText.T("zaman"), CoreText.T("Gecikme süresi")));
        if (_fmax) list.Add(new Coefficient("Fmax", p[IdxFmax], "%", CoreText.T("Ulaşılan maksimum salım (plato)")));
        return list;
    }

    public override (double[] T, double[] F) SelectPoints(
        IReadOnlyList<double> t, IReadOnlyList<double> f, VariantOptions opt)
        => _base.SelectPoints(t, f, opt);

    public override IReadOnlyList<string> Flags(double[] p, VariantOptions opt)
    {
        var flags = new List<string>(_base.Flags(BaseParams(p), opt));

        // Fmax salım yüzdesinin platosudur; %100'ün belirgin üstü fiziksel değildir. Serbest
        // bırakılmış Fmax, ölçek/şekil parametreleriyle birbirini telafi ederek (ör. Weibull
        // b<1 + Fmax=125) daha düşük SS bulabilir — bu iyi bir fit değil, kötü tanımlılıktır.
        if (_fmax && p[IdxFmax] > 100.0 + 1e-9)
            flags.Add(CoreText.T("UYARI: Fmax = %{0:0.#} > %100. Plato fiziksel sınırı aşıyor; parametreler birbirini telafi ediyor olabilir. Fmax'sız hâli ya da farklı bir modeli tercih edin.", p[IdxFmax]));

        // F0 başlangıç salımıdır (Costa Eq. 33). Küçük negatif değerler gecikmeyi taklit edebilir,
        // ama büyük negatif ya da %100 üstü F0 modelin dejenere olduğunu gösterir (ör. KP'de
        // n→0 iken F0→−∞, kKP→+∞ ile logaritmik eğri üretilir).
        if (_f0 && (p[IdxF0] < -20.0 || p[IdxF0] > 100.0))
            flags.Add(CoreText.T("UYARI: F0 = %{0:0.#} fiziksel aralığın dışında. Model bu veride dejenere olmuş görünüyor; katsayılar yorumlanamaz.", p[IdxF0]));

        return flags;
    }
}
