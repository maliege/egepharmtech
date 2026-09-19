namespace EgePharmTech;

/// <summary>
/// Arayüz metinleri için kaynak işaretçisi. Anahtarlar Türkçe kaynak metnin kendisidir; çeviri
/// yalnız <c>Resources/SharedResource.en.resx</c> dosyasında tutulur. Türkçe kültürde ya da çeviri
/// yoksa <c>IStringLocalizer</c> anahtarı (yani Türkçe metni) döndürür; böylece çevrilmemiş bir sayfa
/// hiçbir zaman boş kalmaz.
/// </summary>
public sealed class SharedResource;
