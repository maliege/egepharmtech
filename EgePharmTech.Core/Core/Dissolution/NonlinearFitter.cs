#nullable enable
using MathNet.Numerics.LinearAlgebra;
using MathNet.Numerics.Optimization;
using EgePharmTech.Core.Localization;

namespace EgePharmTech.Core.Dissolution;

/// <summary>
/// Şartname §4'ün fit iskeleti: <b>doğrusal tohum → F(%)-uzayında nonlineer rafinasyon</b>.
///
/// Çözücü olarak <see cref="LevenbergMarquardtMinimizer"/> denenir (hızlı, düzgün modellerde
/// kesin); başarısız olursa ya da tohumdan daha kötü bir SSR üretirse
/// <see cref="NelderMeadSimplex"/> devreye girer. Nelder-Mead türev istemediği için
/// parçalı modellerde (Tlag öncesi F=0) ve Baker-Lonsdale'in sayısal kök çözümünde daha
/// dayanıklıdır. Sonuçta <b>tohumdan asla daha kötü</b> bir fit döndürülmez.
///
/// LM örüntüsü <c>KinetikAnalysis/DoseResponseAnalyzer.cs</c> ile aynıdır; o dosyanın
/// "kinetik modülle aynı çözücüyü kullanır" notu bu motorla nihayet doğru hale gelir.
/// </summary>
public sealed class NonlinearFitter
{
    private const double Tolerance = 1e-9;   // şartname §4
    private const int MaxIterations = 1000;  // şartname §4

    /// <summary>Çizim eğrisi için ince ızgara nokta sayısı.</summary>
    public int PredictionGridSize { get; init; } = 200;

    public ModelFit Fit(
        IDissolutionModel model,
        IReadOnlyList<double> t,
        IReadOnlyList<double> f,
        Weighting weighting = Weighting.None,
        VariantOptions? options = null)
    {
        if (t.Count != f.Count) throw new ArgumentException(CoreText.T("t/F uzunlukları eşleşmiyor"));
        var opt = options ?? VariantOptions.Default;

        // Modele özgü nokta seçimi (ör. Korsmeyer-Peppas F ≤ 60 filtresi)
        var (ts, fs) = model.SelectPoints(t, f, opt);
        if (ts.Length < 2)
            throw new InvalidOperationException(
                CoreText.T("{0}: fit için yeterli nokta yok ({1}).", model.Name, ts.Length));

        var seed = model.InitialGuess(ts, fs, opt);
        var lower = model.LowerBounds;
        var upper = model.UpperBounds;

        var weights = GoodnessOfFitCalculator.Weights(fs, weighting);

        double Objective(double[] p)
        {
            double ssr = 0;
            for (int i = 0; i < ts.Length; i++)
            {
                double yh = model.Evaluate(ts[i], p, opt);
                if (double.IsNaN(yh) || double.IsInfinity(yh)) return double.MaxValue;
                double e = fs[i] - yh;
                ssr += weights[i] * e * e;
            }
            return ssr;
        }

        var seedClamped = Clamp(seed, lower, upper);
        double bestSsr = Objective(seedClamped);
        double[] best = seedClamped;

        // "Yakınsadı" = en az bir çözücü geçerli (sonlu) bir aday üretebildi.
        // Tohumu İYİLEŞTİRMİŞ olması şart değildir: katsayılarında doğrusal modellerde
        // (Zero-order, Higuchi, Peppas-Sahlin-2) OLS tohumu zaten global
        // optimumdur; çözücünün onu doğrulaması da bir başarıdır, başarısızlık değil.
        bool converged = false;

        // --- 1) Levenberg-Marquardt ---
        var lm = TryLevenbergMarquardt(model, ts, fs, seedClamped, lower, upper, opt);
        if (lm is not null)
        {
            double ssr = Objective(lm);
            if (IsFinite(ssr))
            {
                converged = true;
                if (ssr < bestSsr) { best = lm; bestSsr = ssr; }
            }
        }

        // --- 2) Nelder-Mead (LM başarısızsa ya da iyileştirmediyse) ---
        var nm = TryNelderMead(Objective, seedClamped, lower, upper);
        if (nm is not null)
        {
            double ssr = Objective(nm);
            if (IsFinite(ssr))
            {
                converged = true;
                if (ssr < bestSsr) { best = nm; bestSsr = ssr; }
            }
        }

        return Build(model, ts, fs, best, opt, weighting, converged);
    }

    private static double[]? TryLevenbergMarquardt(
        IDissolutionModel model, double[] ts, double[] fs,
        double[] seed, double[] lower, double[] upper, VariantOptions opt)
    {
        try
        {
            var solver = new LevenbergMarquardtMinimizer(
                gradientTolerance: Tolerance,
                stepTolerance: Tolerance,
                functionTolerance: Tolerance,
                maximumIterations: MaxIterations);

            Func<Vector<double>, Vector<double>, Vector<double>> fn =
                (p, xs) =>
                {
                    var pa = p.ToArray();
                    return Vector<double>.Build.Dense(xs.Count, i =>
                    {
                        double v = model.Evaluate(xs[i], pa, opt);
                        return IsFinite(v) ? v : 1e12;
                    });
                };

            var objective = ObjectiveFunction.NonlinearModel(
                fn,
                Vector<double>.Build.Dense(ts),
                Vector<double>.Build.Dense(fs));

            var result = solver.FindMinimum(
                objective,
                Vector<double>.Build.Dense(seed),
                lowerBound: ToFiniteVector(lower, double.MinValue / 4),
                upperBound: ToFiniteVector(upper, double.MaxValue / 4));

            var p = result.MinimizingPoint.ToArray();
            return p.All(IsFinite) ? p : null;
        }
        catch
        {
            return null;
        }
    }

    private static double[]? TryNelderMead(
        Func<double[], double> objective, double[] seed, double[] lower, double[] upper)
    {
        try
        {
            // Sınırlar penaltı ile uygulanır: Nelder-Mead kısıtsız çalışır.
            var penalized = ObjectiveFunction.Value(v =>
            {
                var p = v.ToArray();
                for (int i = 0; i < p.Length; i++)
                    if (p[i] < lower[i] || p[i] > upper[i])
                        return double.MaxValue;
                return objective(p);
            });

            var result = NelderMeadSimplex.Minimum(
                penalized,
                Vector<double>.Build.Dense(seed),
                Tolerance,
                MaxIterations);

            var mp = result.MinimizingPoint.ToArray();
            return mp.All(IsFinite) ? mp : null;
        }
        catch
        {
            return null;
        }
    }

    private ModelFit Build(
        IDissolutionModel model, double[] ts, double[] fs, double[] p,
        VariantOptions opt, Weighting weighting, bool converged)
    {
        var yHat = ts.Select(ti => model.Evaluate(ti, p, opt)).ToArray();
        var gof = GoodnessOfFitCalculator.Compute(fs, yHat, p.Length, weighting);

        // Model-bağımsız uyarı: nokta sayısı parametre sayısına yakınsa (ör. KP F≤60 filtresi
        // 5 nokta bırakır, F0+Tlag ile 4 parametre) katsayılar veriyi "ezberler"; R²adj
        // çöker ve çözücü çoğu zaman tohumdan kıpırdayamaz. Kullanıcı bunu görmeli.
        var flags = new List<string>(model.Flags(p, opt));
        if (gof.Dof <= 1)
            flags.Add(CoreText.T("UYARI: {0} parametre yalnız {1} noktaya fit edildi (serbestlik derecesi {2}). Katsayılar kararsızdır; daha az varyant seçin ya da daha çok zaman noktası girin.", p.Length, ts.Length, gof.Dof));

        // Fmax, gözlenen en yüksek salımın belirgin üstündeyse veri platoya ulaşmamıştır: Fmax ile
        // hız sabiti birbirini telafi eder (küçük k, büyük Fmax ≈ doğru), Tx değerleri ekstrapolasyondur.
        var parameters = model.Describe(p);
        var fmaxCoef = parameters.FirstOrDefault(c => c.Symbol == "Fmax");
        double fObsMax = fs.Max();
        if (fmaxCoef is not null && fObsMax > 0 && fmaxCoef.Value > 1.25 * fObsMax)
            flags.Add(CoreText.T("UYARI: Fmax = %{0:0.#}, gözlenen en yüksek salım %{1:0.#}. Veri platoya ulaşmamış; Fmax ve ona bağlı T25–T90 değerleri ekstrapolasyondur, güvenilmez.", fmaxCoef.Value, fObsMax));

        // Çizim için ince ızgara
        double tMin = 0, tMax = ts.Max();
        var predT = new double[PredictionGridSize];
        var predV = new double[PredictionGridSize];
        for (int i = 0; i < PredictionGridSize; i++)
        {
            double ti = tMin + (tMax - tMin) * i / (PredictionGridSize - 1.0);
            predT[i] = ti;
            predV[i] = model.Evaluate(ti, p, opt);
        }

        return new ModelFit(
            ModelName: model.Name,
            Equation: model.Equation,
            Parameters: parameters,
            Secondary: model.Secondary(p, opt),
            Gof: gof,
            PredTimes: predT,
            PredValues: predV,
            Flags: flags)
        {
            Converged = converged,
            Predict = ti => model.Evaluate(ti, p, opt)
        };
    }

    private static double[] Clamp(double[] p, double[] lo, double[] hi)
    {
        var r = new double[p.Length];
        for (int i = 0; i < p.Length; i++)
        {
            double v = IsFinite(p[i]) ? p[i] : 0;
            r[i] = Math.Min(Math.Max(v, lo[i]), hi[i]);
        }
        return r;
    }

    private static Vector<double> ToFiniteVector(double[] bounds, double replacement)
        => Vector<double>.Build.Dense(bounds.Select(b =>
            double.IsNegativeInfinity(b) ? replacement
            : double.IsPositiveInfinity(b) ? Math.Abs(replacement)
            : b).ToArray());

    private static bool IsFinite(double v) => !double.IsNaN(v) && !double.IsInfinity(v);
}
