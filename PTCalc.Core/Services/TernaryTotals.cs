namespace PTCalc.Core.Services;

using PTCalc.Core.Models;

/// <summary>
/// Üçgen diyagram satırlarında yağ + sürfaktan + su toplamının 100 olup olmadığını denetler.
/// Çizim yalnız yağ ve sürfaktan yüzdesini kullanır, su "kalan" olarak okunur; bu yüzden toplamı
/// 100 olmayan bir satır hata vermeden yanlış yere düşer. Bu sınıf sapmaları listeler; düzeltme
/// kullanıcıya bırakılır (hesap engellenmez).
/// </summary>
public static class TernaryTotals
{
    /// <summary>Varsayılan tolerans (yüzde puanı). Yuvarlama kaynaklı 0,1-0,3'lük sapmalar uyarı üretmez.</summary>
    public const double DefaultTolerance = 0.5;

    public sealed record Deviation(int Group, int Order, double Oil, double Surfactant, double Water)
    {
        public double Total => Oil + Surfactant + Water;
        /// <summary>Toplam - 100; pozitif ise fazla, negatif ise eksik.</summary>
        public double Delta => Total - 100.0;
    }

    /// <summary>Toplamı 100'den <paramref name="tolerance"/>'tan fazla sapan satırlar, giriş sırasında.</summary>
    public static List<Deviation> Check(IEnumerable<Ttridata> rows, double tolerance = DefaultTolerance)
    {
        if (tolerance < 0) throw new ArgumentOutOfRangeException(nameof(tolerance));
        var list = new List<Deviation>();
        foreach (var r in rows)
        {
            double total = r.oil + r.surCoSur + r.water;
            if (double.IsNaN(total) || Math.Abs(total - 100.0) > tolerance)
                list.Add(new Deviation(r.grup, r.siraNo, r.oil, r.surCoSur, r.water));
        }
        return list;
    }
}
