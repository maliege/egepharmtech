# Using `PTCalc.Core` as a library

`PTCalc.Core` is a plain .NET 10 class library (MathNet.Numerics is its only dependency) that holds every
computation behind the PTCalc web site. It can be referenced from any .NET project, a C# script (`dotnet run`
file-based app), F# Interactive, or PowerShell 7. Nothing in it touches ASP.NET, Blazor or the browser.

```bash
git clone https://github.com/maliege/ptcalc.git
dotnet add <your-project> reference ptcalc/PTCalc.Core/PTCalc.Core.csproj
```

All messages returned to the user (warnings, flags, exception texts) are Turkish by default and English when
`CultureInfo.CurrentUICulture` is English:

```csharp
CultureInfo.CurrentUICulture = CultureInfo.GetCultureInfo("en-US");
```

## Dissolution kinetics — `PTCalc.Core.Dissolution`

### Fit all models and rank by AIC

```csharp
using PTCalc.Core.Dissolution;

double[] t = [1, 2, 3, 4, 6, 8, 10, 12, 14, 16, 18, 20];   // time (any unit)
double[] f = [8, 24, 38, 48, 58, 66, 73, 78, 84, 88, 92, 95]; // % released

AnalysisResult result = KineticEngine.FitAll(t, f);

foreach (ModelFit fit in result.Fits)                         // sorted by AIC, best first
    Console.WriteLine($"{fit.ModelName,-18} {fit.CoefficientSummary,-40} AIC {fit.Gof.Aic:F2}  " +
                      $"R²adj {fit.Gof.RsqrAdj:F4}  w = {result.AkaikeWeights[fit.ModelName]:F3}");

// Models fitted to a restricted point set (Korsmeyer–Peppas, F ≤ 60 % by default) are reported
// separately because their AIC is not comparable with the full-profile fits:
foreach (var fit in result.MechanismFits)
    Console.WriteLine($"{fit.ModelName}: {fit.CoefficientSummary} (n = {fit.Gof.N})");

// Model-independent profile summary: AUC, dissolution efficiency, mean dissolution time
ProfileSummary p = result.Profile;
```

`ModelFit` exposes `Parameters` (symbol, value, unit, description), `Secondary` (T25…T90, Weibull Td, with an
`IsCalculable` flag), `Gof` (N, dof, R, R², R²adj, MSE, RMSE, SS, WSS, AIC, AICc, MSC), `Flags` (warnings such as
too few degrees of freedom or an extrapolated Fmax), `Converged`, a dense `PredTimes`/`PredValues` grid for
plotting and a `Predict(t)` delegate.

### Options: variants, geometry, weighting, model subset

```csharp
var options = new VariantOptions
{
    UseTlag = true,                       // lag time
    UseF0 = false,                        // burst / initial release
    UseFmax = true,                       // plateau below 100 %
    Geometry = HopfenbergGeometry.Cylinder, // Slab, Cylinder, Sphere, HalfSphere, Triangle
    KpUseAllPoints = false,               // true = DDSolver behaviour (KP on all points)
};

var weighted = KineticEngine.FitAll(t, f, options, Weighting.InvY);   // None, InvY, InvY2

// A single model, with variants the model supports (unsupported ones are dropped silently in FitAll,
// applied as requested in Fit):
ModelFit weibull = KineticEngine.Fit("Weibull", t, f, options with { UseTlag = true });
double at30min = weibull.Predict(30);
```

Model names (`ModelCatalog.ModelNames()`): Zero-order, First-order, Higuchi, Hixson-Crowell, Korsmeyer-Peppas,
Weibull, Hopfenberg, Baker-Lonsdale, Makoid-Banakar, Peppas-Sahlin, Peppas-Sahlin-2, Logistic, Gompertz,
Probit, Logistic (Fmax), Gompertz (Fmax).

### Reading a data table with replicates

`KineticInputReader.Read(object?[][] table)` accepts a grid whose first column is time and whose next columns
are replicate measurements (or a mean and standard deviation), skips invalid rows and returns the profile with
warnings; `ReplicateStats` gives per-row mean and SD. Cells may be numbers or strings with comma or dot decimals.

### Similarity factors and profile metrics

```csharp
double[] reference = [12, 28, 45, 60, 72, 81, 88];
double[] test      = [10, 25, 41, 57, 70, 80, 87];

var (f1, sumR, sumAbsDiff) = SimilarityFactors.F1(reference, test);   // difference factor
double f2 = SimilarityFactors.F2(reference, test);                     // similarity factor (≥ 50 ≈ similar)
int cut = SimilarityFactors.FindCutoffIndex(reference, test, 85);      // first point where both exceed 85 %

double de  = ProfileMetrics.De(t, f);    // dissolution efficiency, %
double mdt = ProfileMetrics.Mdt(t, f);   // mean dissolution time
```

## Statistics — `PTCalc.Core.Statistics`

```csharp
using PTCalc.Core.Statistics;

// One-way ANOVA + Levene + Tukey HSD (Tukey–Kramer for unequal n)
var groups = new IReadOnlyList<double>[]
{
    new[] { 78.2, 80.1, 79.5, 81.0, 77.9, 80.4 },
    new[] { 85.3, 86.1, 84.7, 87.2, 85.9 },
    new[] { 79.0, 82.5, 81.1, 80.3, 83.0, 81.7, 80.9 },
};
OneWayAnovaResult a = OneWayAnova.Analyze(groups, new[] { "F1", "F2", "F3" }, alpha: 0.05);
Console.WriteLine($"F({a.DfBetween},{a.DfWithin}) = {a.F:F3}, p = {a.P:G3}, η² = {a.Eta2:F3}");
foreach (TukeyPair pair in a.Tukey)
    Console.WriteLine($"{pair.A} − {pair.B}: {pair.Diff:F3} [{pair.Lower:F3}, {pair.Upper:F3}] p = {pair.P:F4}");

// Multiple linear regression with prediction
var x = new[] { new[] { 10.0, 2.0 }, new[] { 12.0, 2.0 }, /* … */ };
double[] y = [5.1, 5.9 /* … */];
MultipleRegressionResult m = MultipleRegression.Fit(x, y, new[] { "Pressure", "Binder" });
RegressionPrediction pred = m.Predict(new[] { 15.0, 3.0 });   // fit, CI for the mean, PI for one observation

// Calibration curve, LOD/LOQ (ICH Q2(R2): 3.3·s_y/x / S and 10·s_y/x / S), inverse prediction
double[] conc = [1, 1, 1, 2, 2, 2, 5, 5, 5, 10, 10, 10, 20, 20, 20];
double[] resp = [0.102, 0.098, 0.105, 0.201, 0.197, 0.204, 0.495, 0.503, 0.499, 0.990, 1.004, 0.996, 1.985, 2.010, 1.996];
CalibrationResult c = CalibrationCurve.Fit(conc, resp);
CalibrationUnknown u = c.Estimate(response: 0.75, replicates: 3);   // concentration, s_x0, CI, out-of-range flag

// Studentized range distribution (used by Tukey HSD)
double qCrit = StudentizedRange.InvCdf(0.95, k: 3, nu: 15);
```

`StatisticsInputReader.ReadColumns` and `ReadCompleteRows` convert a spreadsheet-like `object?[][]` into
per-column or per-row numeric data, reporting skipped rows.

## Dose–response — `PTCalc.Core.KinetikAnalysis`

```csharp
using PTCalc.Core.KinetikAnalysis;

double[] dose = [10, 20, 40, 80, 160];
double[] mortalityPct = [5, 20, 50, 80, 95];

double[] dead    = [1, 4, 10, 16, 19];   // responders per dose group
double[] animals = [20, 20, 20, 20, 20];  // group sizes

// Probit / logit GLM on log dose: 100·link⁻¹(β0 + β1·ln x)
ProbitLogitFitResult probit = ProbitLogitAnalyzer.Fit(dose, dead, animals, DoseResponseLink.Probit);
double ld50 = ProbitLogitAnalyzer.InverseDose(50, probit.B0, probit.B1, DoseResponseLink.Probit);

// Four-parameter logistic on percent response
DoseResponseFitResult fourPl = DoseResponseAnalyzer.FitLogistic(dose, mortalityPct);
```

## Pseudo-ternary phase diagram — `PTCalc.Core.Services`

```csharp
using PTCalc.Core.Models;
using PTCalc.Core.Services;

var request = new DiagramCalculationRequest
{
    Data =
    [
        new Ttridata { grup = 1, siraNo = 1, oil = 10, surCoSur = 60, water = 30 },
        new Ttridata { grup = 1, siraNo = 2, oil = 20, surCoSur = 55, water = 25 },
        // … boundary points of one phase region, in order; percentages sum to 100
    ],
    Configuration = new ChartConfiguration { PolygonCloseType = 0 },
};
DiagramCalculationResponse response = new TernaryCalculationService().Calculate(request);
foreach (TGroup g in response.Groups)
    Console.WriteLine($"group {g.GroupIndex}: area = {g.Area:F4} (unit-triangle coordinates), " +
                      $"centroid oil/surfactant/water = {g.A:F1} / {g.B:F1} / {g.C:F1} %, xy = {g.PolygonCenterD}");
```

`PolygonSmoother.Build` returns the centripetal Catmull–Rom outline (`SampleOutline` turns it into points) used for the drawn boundary; area and
centroid are always computed on the measured polygon, not on the smoothed curve.

## Alcoholometry and dilution — `PTCalc.Core.Alcoholometry`

```csharp
using PTCalc.Core.Alcoholometry;

// OIML R 22 density (kg/m³) from strength by mass or by volume (20 °C) and temperature (−20…40 °C)
double rho = OimlAlcoholometry.DensityFromVolumePercent(70, 25);

// % v/v (20 °C) ↔ % w/w; v/v → w/w is solved by fixed-point iteration (no closed form)
double w = OimlAlcoholometry.VolumeToMassPercent(70);      // 62.386…
double q = OimlAlcoholometry.MassToVolumePercent(w);       // 70

// Strength from a measured true density (bisection); OIML Tables Va/Vb at 20 °C, VI/VII otherwise
double p = OimlAlcoholometry.MassPercentFromDensity(885.56, 20);

// 100 mL of 70 % v/v from 96 % v/v at 20 °C: ethanol mass balance, volumes at the preparation temperature
DilutionResult r = AlcoholDilution.ByVolume(finalVolume: 100, targetPercent: 70, stockPercent: 96);
Console.WriteLine($"stock {r.StockVolume:F2} mL, water {r.WaterVolume:F2} mL, contraction {r.Contraction:F2} mL");
```

`AlcoholDilution.ByMass` does the same for % w/w targets and an amount in grams.

## Numerical reference tests

The repository's test project documents the expected numbers: `DdsolverReferenceTests` compares equations,
point rules and goodness-of-fit definitions with DDSolver 1.0 outputs on 37 formulations;
`StatisticsReferenceTests` compares ANOVA, Tukey, regression and calibration results with SciPy 1.17 /
statsmodels 0.14 values produced by `tools/make_stats_reference.py`; `AlcoholometryTests` compares the OIML R 22
formula and its inverses with 6 889 printed table cells (`oiml_r22_reference.json`, `tools/make_oiml_reference.py`). Run them with

```bash
dotnet test PTCalc.Core.Tests
```
