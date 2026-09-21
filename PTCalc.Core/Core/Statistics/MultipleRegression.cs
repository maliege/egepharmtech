#nullable enable
using PTCalc.Core.Localization;
using MathNet.Numerics.Distributions;
using MathNet.Numerics.LinearAlgebra;

namespace PTCalc.Core.Statistics;

/// <summary>Bir regresyon katsayısı: tahmin, standart hata, t, p ve güven aralığı.</summary>
public sealed record RegressionCoefficient(string Name, double Estimate, double Se, double T, double P, double Lower, double Upper);

/// <summary>Yeni bir gözlem için tahmin: ortalama yanıtın güven aralığı ve tek gözlemin öngörü aralığı.</summary>
public sealed record RegressionPrediction(
    IReadOnlyList<double> X, double Fit, double SeFit, double CiLower, double CiUpper, double PiLower, double PiUpper);

/// <summary>Çoklu doğrusal regresyon (OLS) sonucu.</summary>
public sealed class MultipleRegressionResult
{
    public required IReadOnlyList<RegressionCoefficient> Coefficients { get; init; }
    public required int N { get; init; }
    /// <summary>Açıklayıcı değişken sayısı (sabit hariç).</summary>
    public required int K { get; init; }
    public required bool HasIntercept { get; init; }
    public required double R2 { get; init; }
    public required double R2Adj { get; init; }
    public required double Rmse { get; init; }
    public required double F { get; init; }
    public required double FP { get; init; }
    public required double SsRegression { get; init; }
    public required double SsResidual { get; init; }
    public required double SsTotal { get; init; }
    public required int DfRegression { get; init; }
    public required int DfResidual { get; init; }
    public required IReadOnlyList<double> Fitted { get; init; }
    public required IReadOnlyList<double> Residuals { get; init; }
    public required IReadOnlyList<double> StandardizedResiduals { get; init; }
    public required IReadOnlyList<double> Leverage { get; init; }
    public required IReadOnlyList<double> Vif { get; init; }
    public required double DurbinWatson { get; init; }
    public required double Alpha { get; init; }
    public required IReadOnlyList<string> Flags { get; init; }
    /// <summary>(XᵀX)⁻¹; tahmin aralıkları için saklanır.</summary>
    public required Matrix<double> XtXInverse { get; init; }

    public bool Significant => FP < Alpha;

    /// <summary>Denklem metni: y = b0 + b1·x1 + …</summary>
    public string Equation(IFormatProvider? culture = null)
    {
        var parts = new List<string>();
        for (int i = 0; i < Coefficients.Count; i++)
        {
            var c = Coefficients[i];
            string v = c.Estimate.ToString("0.####", culture);
            parts.Add(HasIntercept && i == 0 ? v : $"{v}·{c.Name}");
        }
        return "y = " + string.Join(" + ", parts).Replace("+ -", "− ");
    }

    /// <summary>Yeni x vektörü (sabit hariç, K uzunluklu) için tahmin, GA ve ÖA.</summary>
    public RegressionPrediction Predict(IReadOnlyList<double> x)
    {
        if (x.Count != K) throw new ArgumentException(CoreText.T("Tahmin için {0} açıklayıcı değer girilmeli.", K));
        var row = HasIntercept ? new[] { 1.0 }.Concat(x).ToArray() : x.ToArray();
        var v = Vector<double>.Build.Dense(row);
        double fit = 0;
        for (int i = 0; i < row.Length; i++) fit += row[i] * Coefficients[i].Estimate;
        double lev = v * (XtXInverse * v);
        double seFit = Rmse * Math.Sqrt(Math.Max(0, lev));
        double sePred = Rmse * Math.Sqrt(1 + Math.Max(0, lev));
        double t = DfResidual > 0 ? StudentT.InvCDF(0, 1, DfResidual, 1 - Alpha / 2) : double.NaN;
        return new RegressionPrediction(x, fit, seFit, fit - t * seFit, fit + t * seFit, fit - t * sePred, fit + t * sePred);
    }
}

/// <summary>
/// En küçük kareler ile çoklu doğrusal regresyon. Çözüm QR ayrışımıyla; standart hatalar (XᵀX)⁻¹·MSE'den.
/// Tekil (rank eksik) tasarım matrisi hata verir: bir sütun başka sütunların doğrusal bileşimiyse
/// (ör. sabit sütun, tekrarlanan değişken) katsayılar tanımsızdır.
/// </summary>
public static class MultipleRegression
{
    public static MultipleRegressionResult Fit(
        IReadOnlyList<double[]> x, IReadOnlyList<double> y,
        IReadOnlyList<string>? names = null, double alpha = 0.05, bool intercept = true)
    {
        int n = y.Count;
        if (x.Count != n) throw new ArgumentException(CoreText.T("X ve Y satır sayıları eşleşmiyor."));
        if (n == 0) throw new ArgumentException(CoreText.T("Veri yok."));
        int k = x[0].Length;
        int pCount = k + (intercept ? 1 : 0);
        if (n <= pCount)
            throw new ArgumentException(CoreText.T("Gözlem sayısı ({0}) parametre sayısından ({1}) büyük olmalı.", n, pCount));

        var X = Matrix<double>.Build.Dense(n, pCount, (i, j) =>
            intercept ? (j == 0 ? 1.0 : x[i][j - 1]) : x[i][j]);
        var Y = Vector<double>.Build.Dense(y.ToArray());

        var qr = X.QR();
        var rDiag = Enumerable.Range(0, pCount).Select(i => Math.Abs(qr.R[i, i])).ToArray();
        double tol = 1e-10 * rDiag.Max();
        if (rDiag.Any(d => d < tol))
            throw new ArgumentException(CoreText.T("Tasarım matrisi tekil: bir açıklayıcı değişken diğerlerinin doğrusal bileşimi ya da sabit. Değişkenleri gözden geçirin."));

        var beta = qr.Solve(Y);
        var fitted = X * beta;
        var resid = Y - fitted;

        double ssRes = resid.DotProduct(resid);
        double yMean = Y.Average();
        double ssTot = intercept ? Y.Sum(v => (v - yMean) * (v - yMean)) : Y.DotProduct(Y);
        double ssReg = ssTot - ssRes;
        int dfReg = intercept ? k : pCount, dfRes = n - pCount;
        double mse = ssRes / dfRes;
        double rmse = Math.Sqrt(mse);
        double r2 = ssTot > 0 ? 1 - ssRes / ssTot : 0;
        double r2adj = 1 - (1 - r2) * (n - (intercept ? 1 : 0)) / (double)dfRes;
        double f = dfReg > 0 && mse > 0 ? (ssReg / dfReg) / mse : double.NaN;
        double fp = double.IsNaN(f) ? double.NaN : 1 - FisherSnedecor.CDF(dfReg, dfRes, f);

        var xtxInv = X.TransposeThisAndMultiply(X).Inverse();
        double tCrit = StudentT.InvCDF(0, 1, dfRes, 1 - alpha / 2);
        var coefs = new List<RegressionCoefficient>();
        for (int j = 0; j < pCount; j++)
        {
            int xi = j - (intercept ? 1 : 0);
            string name = intercept && j == 0
                ? CoreText.T("(Sabit)")
                : (names is not null && xi < names.Count && !string.IsNullOrWhiteSpace(names[xi]) ? names[xi] : $"x{xi + 1}");
            double se = Math.Sqrt(Math.Max(0, xtxInv[j, j] * mse));
            double t = se > 0 ? beta[j] / se : double.NaN;
            double p = double.IsNaN(t) ? double.NaN : 2 * (1 - StudentT.CDF(0, 1, dfRes, Math.Abs(t)));
            coefs.Add(new RegressionCoefficient(name, beta[j], se, t, p, beta[j] - tCrit * se, beta[j] + tCrit * se));
        }

        // Kaldıraç h_ii = x_iᵀ (XᵀX)⁻¹ x_i; standartlaştırılmış artık e_i / (s √(1 − h_ii))
        var lev = new double[n];
        var stdRes = new double[n];
        for (int i = 0; i < n; i++)
        {
            var row = X.Row(i);
            lev[i] = row * (xtxInv * row);
            double d = 1 - lev[i];
            stdRes[i] = d > 1e-12 && rmse > 0 ? resid[i] / (rmse * Math.Sqrt(d)) : double.NaN;
        }

        double dw = 0;
        for (int i = 1; i < n; i++) dw += Math.Pow(resid[i] - resid[i - 1], 2);
        dw = ssRes > 0 ? dw / ssRes : double.NaN;

        var vif = k >= 2 ? ComputeVif(x) : Enumerable.Repeat(double.NaN, k).ToArray();

        var flags = new List<string>();
        if (dfRes < 5)
            flags.Add(CoreText.T("Hata serbestlik derecesi çok düşük ({0}); katsayı testleri güçsüz, aralıklar geniştir.", dfRes));
        var big = stdRes.Select((s, i) => (s, i)).Where(t => Math.Abs(t.s) > 3).Select(t => t.i + 1).ToList();
        if (big.Count > 0)
            flags.Add(CoreText.T("Standartlaştırılmış artığı |3|'ü aşan gözlem(ler): {0}. Olası aykırı değer.", string.Join(", ", big)));
        var high = vif.Select((v, i) => (v, i)).Where(t => t.v > 10).ToList();
        if (high.Count > 0)
            flags.Add(CoreText.T("Yüksek çoklu doğrusallık (VIF > 10): {0}. Katsayılar kararsız olabilir.",
                string.Join(", ", high.Select(t => $"{coefs[t.i + (intercept ? 1 : 0)].Name} (VIF {t.v:0.0})"))));
        if (n > 3 && (dw < 1.0 || dw > 3.0))
            flags.Add(CoreText.T("Durbin-Watson = {0:0.00}: artıklar sıralı bağımlılık gösteriyor olabilir.", dw));

        return new MultipleRegressionResult
        {
            Coefficients = coefs, N = n, K = k, HasIntercept = intercept,
            R2 = r2, R2Adj = r2adj, Rmse = rmse, F = f, FP = fp,
            SsRegression = ssReg, SsResidual = ssRes, SsTotal = ssTot,
            DfRegression = dfReg, DfResidual = dfRes,
            Fitted = fitted.ToArray(), Residuals = resid.ToArray(), StandardizedResiduals = stdRes, Leverage = lev,
            Vif = vif, DurbinWatson = dw, Alpha = alpha, Flags = flags, XtXInverse = xtxInv
        };
    }

    /// <summary>Varyans şişme faktörleri: her xⱼ'nin diğer x'lere regresyonundan 1/(1 − R²ⱼ).</summary>
    public static double[] ComputeVif(IReadOnlyList<double[]> x)
    {
        int k = x[0].Length;
        var vif = new double[k];
        for (int j = 0; j < k; j++)
        {
            var yj = x.Select(r => r[j]).ToList();
            var others = x.Select(r => r.Where((_, c) => c != j).ToArray()).ToList();
            try
            {
                var fit = Fit(others, yj, alpha: 0.05, intercept: true);
                vif[j] = fit.R2 < 1 ? 1 / (1 - fit.R2) : double.PositiveInfinity;
            }
            catch { vif[j] = double.NaN; }
        }
        return vif;
    }
}
