using PTCalc.Core.Localization;

namespace PTCalc.Core.Alcoholometry;

/// <summary>
/// Su–etanol karışımları için OIML R 22 (1975) "International Alcoholometric Tables" denklemi ve
/// bu denklemden türeyen dönüşümler. Yoğunluklar kg/m³, sıcaklık °C (IPTS-68), alkol oranları yüzde.
/// <para>
/// Genel formül: ρ(w, t) = A₁ + Σₖ₌₂¹² Aₖ·w^(k−1) + Σₖ₌₁⁶ Bₖ·(t−20)^k + Σᵢ₌₁⁵ Σₖ₌₁^mᵢ Cᵢ,ₖ·w^k·(t−20)^i,
/// w = kütlece alkol oranı (0–1). Katsayılar OIML R 22 Ek'indeki değerlerdir.
/// </para>
/// <para>
/// Hacimce derece (φ, %v/v) tanım gereği 20 °C'deki hacimlere göredir. Kütlece orana geçiş
/// w = φ·ρ_E(20)/ρ(w, 20) denklemiyle olur; w için 12. dereceden bir polinom denklemi olduğundan kapalı
/// çözümü yoktur ve sabit nokta iterasyonuyla çözülür (OIML aynı dönüşümü Tablo IIIb'nin tersinden
/// interpolasyonla, Tablo IVb olarak verir).
/// </para>
/// </summary>
public static class OimlAlcoholometry
{
    /// <summary>OIML R 22 formülünün geçerli olduğu en düşük sıcaklık (°C).</summary>
    public const double MinTemperature = -20.0;

    /// <summary>OIML R 22 formülünün geçerli olduğu en yüksek sıcaklık (°C).</summary>
    public const double MaxTemperature = 40.0;

    /// <summary>Hacimce derecenin ve Tablo III–V'in referans sıcaklığı (°C).</summary>
    public const double ReferenceTemperature = 20.0;

    // Yakınsama ölçütü (kütle kesri cinsinden) ve güvenlik için en çok adım sayısı.
    private const double Tolerance = 1e-12;
    private const int MaxIterations = 200;

    // A_k (k = 1..12); dizide indeks 0 => A1.
    private static readonly double[] A =
    {
         998.20123,
        -192.9769495,
         389.1238958,
        -1668.103923,
         13522.15441,
        -88292.78388,
         306287.4042,
        -613838.1234,
         747017.2998,
        -547846.1354,
         223446.0334,
        -39032.85426,
    };

    // B_k (k = 1..6); dizide indeks 0 => B1.
    private static readonly double[] B =
    {
        -0.20618513,
        -0.0052682542,
         3.6130013e-5,
        -3.8957702e-7,
         7.1693540e-9,
        -9.9739231e-11,
    };

    // C_{i,k}: satır i = 1..5, sütun k = 1..m_i (m = 11, 10, 9, 4, 2).
    private static readonly double[][] C =
    {
        new[]
        {
             0.169344346153, -10.46914743455169, 71.96353469546523, -704.7478054272792,
             3924.090430035045, -12101.64659068747, 22486.46550400788, -26055.62982188164,
             18523.73922069467, -7420.201433430137, 1285.617841998974,
        },
        new[]
        {
            -0.0119301300505701, 0.2517399633803461, -2.170575700536993, 13.53034988843029,
            -50.29988758547014, 109.635566657757, -142.2753946421155, 108.043594285623,
            -44.14153236817392, 7.442971530188783,
        },
        new[]
        {
            -0.0006802995733503803, 0.01876837790289664, -0.2002561813734156, 1.02299296671922,
            -2.895696483903638, 4.810060584300675, -4.672147440794683, 2.458043105903461,
            -0.5411227621436812,
        },
        new[]
        {
             4.075376675622027e-6, -8.76305857347111e-6, 6.515031360099368e-6, -1.51578483698721e-6,
        },
        new[]
        {
            -2.788074354782409e-8, 1.345612883493354e-8,
        },
    };

    /// <summary>Saf etanolün 20 °C'deki yoğunluğu (kg/m³); formülden w = 1 için ≈ 789,24.</summary>
    public static double EthanolDensity20 { get; } = Formula(1.0, ReferenceTemperature);

    /// <summary>Saf suyun (havayla doymuş) verilen sıcaklıktaki yoğunluğu (kg/m³); 20 °C'de 998,20.</summary>
    public static double WaterDensity(double temperature)
    {
        CheckTemperature(temperature);
        return Formula(0.0, temperature);
    }

    /// <summary>Kütlece alkol oranı (%w/w) ve sıcaklıktan yoğunluk (kg/m³). OIML Tablo I ve IIIa.</summary>
    public static double DensityFromMassPercent(double massPercent, double temperature)
    {
        CheckPercent(massPercent);
        CheckTemperature(temperature);
        return Formula(massPercent / 100.0, temperature);
    }

    /// <summary>Hacimce alkol derecesi (%v/v, 20 °C) ve sıcaklıktan yoğunluk (kg/m³). OIML Tablo II ve IVa.</summary>
    public static double DensityFromVolumePercent(double volumePercent, double temperature)
        => DensityFromMassPercent(VolumeToMassPercent(volumePercent), temperature);

    /// <summary>
    /// Kütlece orandan hacimce dereceye (OIML Tablo IIIb): φ = w·ρ(w, 20)/ρ_E(20). Doğrudan hesaplanır.
    /// </summary>
    public static double MassToVolumePercent(double massPercent)
    {
        CheckPercent(massPercent);
        double w = massPercent / 100.0;
        return w * Formula(w, ReferenceTemperature) / EthanolDensity20 * 100.0;
    }

    /// <summary>
    /// Hacimce dereceden kütlece orana (OIML Tablo IVb). w = φ·ρ_E(20)/ρ(w, 20) sabit nokta iterasyonuyla
    /// çözülür; başlangıç w₀ = φ. |g′(w)| = w·|dρ/dw|/ρ aralık boyunca 0,4'ün altında kaldığından (en büyük değer w → 1'de ≈ 0,399)
    /// yöntem her adımda hatayı en az yarıya indirir ve her zaman yakınsar.
    /// </summary>
    public static double VolumeToMassPercent(double volumePercent)
    {
        CheckPercent(volumePercent);
        double phi = volumePercent / 100.0;
        double w = phi;
        for (int n = 0; n < MaxIterations; n++)
        {
            double next = phi * EthanolDensity20 / Formula(w, ReferenceTemperature);
            if (Math.Abs(next - w) < Tolerance) return next * 100.0;
            w = next;
        }
        return w * 100.0;
    }

    /// <summary>
    /// Ölçülen yoğunluktan (kg/m³) kütlece alkol oranı. t = 20 °C'de OIML Tablo Va, diğer sıcaklıklarda
    /// Ek I'deki Tablo VI. Yoğunluk −20…40 °C aralığında w arttıkça sürekli azaldığından kök tektir ve
    /// ikiye bölme (bisection) yöntemiyle bulunur. Cam alkolmetre/hidrometre okumaları için gereken cam
    /// genleşme düzeltmesi (Tablo VIII–X) burada yapılmaz; girdi gerçek yoğunluk olmalıdır.
    /// </summary>
    public static double MassPercentFromDensity(double density, double temperature = ReferenceTemperature)
    {
        CheckTemperature(temperature);
        double water = Formula(0.0, temperature);
        double ethanol = Formula(1.0, temperature);
        if (double.IsNaN(density) || density > water || density < ethanol)
        {
            throw new ArgumentException(CoreText.T("Yoğunluk {0} °C'de {1:F2} ile {2:F2} kg/m³ arasında olmalıdır.", temperature, ethanol, water));
        }

        double lo = 0.0, hi = 1.0;
        for (int n = 0; n < MaxIterations && hi - lo > Tolerance; n++)
        {
            double mid = 0.5 * (lo + hi);
            if (Formula(mid, temperature) > density) lo = mid; // yoğunluk hâlâ yüksek: daha çok alkol gerekir
            else hi = mid;
        }
        return 0.5 * (lo + hi) * 100.0;
    }

    /// <summary>
    /// Ölçülen yoğunluktan (kg/m³) hacimce alkol derecesi (%v/v, 20 °C). t = 20 °C'de OIML Tablo Vb,
    /// diğer sıcaklıklarda Tablo VII.
    /// </summary>
    public static double VolumePercentFromDensity(double density, double temperature = ReferenceTemperature)
        => MassToVolumePercent(MassPercentFromDensity(density, temperature));

    /// <summary>OIML R 22 genel formülü; w kütle kesri (0–1), t °C. Girdi denetimi yapmaz.</summary>
    private static double Formula(double w, double t)
    {
        double d = t - ReferenceTemperature;

        double sumA = A[0];
        for (int k = 2; k <= A.Length; k++) sumA += A[k - 1] * Math.Pow(w, k - 1);

        double sumB = 0.0;
        for (int k = 1; k <= B.Length; k++) sumB += B[k - 1] * Math.Pow(d, k);

        double sumC = 0.0;
        for (int i = 1; i <= C.Length; i++)
        {
            double inner = 0.0;
            for (int k = 1; k <= C[i - 1].Length; k++) inner += C[i - 1][k - 1] * Math.Pow(w, k);
            sumC += inner * Math.Pow(d, i);
        }

        return sumA + sumB + sumC;
    }

    private static void CheckPercent(double value)
    {
        if (double.IsNaN(value) || value < 0 || value > 100)
            throw new ArgumentException(CoreText.T("Alkol oranı 0 ile 100 arasında olmalıdır."));
    }

    private static void CheckTemperature(double value)
    {
        if (double.IsNaN(value) || value < MinTemperature || value > MaxTemperature)
            throw new ArgumentException(CoreText.T("Sıcaklık {0} ile {1} °C arasında olmalıdır (OIML R 22 geçerlilik aralığı).", MinTemperature, MaxTemperature));
    }
}
