# TernaryPhaseDiagramApp hesaplama akışı

Bu belge `PTCalc/Pages/Apps/TernaryPhaseDiagramApp.razor` dosyasında yer alan hesaplama mantığını özetler.

## Genel akış

1. Kullanıcı `Hesapla` butonuna basınca `CalculateDiagram` çalışır.
2. Grid verisi `inputGrid.GetDataAsync()` ile alınır.
3. `ConvertGridData` ile grid verisi `List<Ttridata>` tipine dönüştürülür.
4. `TernaryCalculationService.Calculate` çağrılır ve sonuçlar `resultGroups` listesine aktarılır.
5. Tüm veri noktaları ternary düzlemine dönüştürülür ve `calculatedData` içerisine yazılır.
6. Poligonlar `CalculatePolygonPoints` ile oluşturulur.
7. Her grup için poligon merkezi ve alanı `CalculatePolygonCentroidAndArea` ile hesaplanır.

## Veri dönüştürme

`ConvertGridData` şu adımları uygular:

- Grid verisi JSON array olarak beklenir.
- Her satır için `grup`, `siraNo`, `oil`, `surCoSur`, `water` alanları okunur.
- Grup adı `GetOrCreateGroupId` ile sayısal kimliğe dönüştürülür.
- Eksik veya geçersiz satırlar atlanır.

## Ternary koordinat dönüşümü

Her bir veri noktası için:

- `oil`, `surCoSur`, `water` yüzde değerleri 0–1 aralığına çekilir.
- Ternary koordinatlar şu şekilde hesaplanır:
  - `p.X = surf + oil * Cos60`
  - `p.Y = oil * Sin60`

Bu dönüşüm `CalculateDiagram` içinde, `calculatedData` listesi üzerinde yapılır.

## Poligon oluşturma

`CalculatePolygonPoints` grupları sırayla işleyip poligon noktalarını üretir:

- Grup verileri `siraNo` sırasına göre dizilir.
- Her noktanın `p` değeri poligon listesine eklenir.
- `polygonCloseType` ve `edgeType` değerlerine göre kapanış noktaları `CalculateClosingPoints` ile eklenir.

### Kapanış noktaları

`CalculateClosingPoints`:

- `closeType == 1` için minimum değer üzerinden kapanış hattı oluşturur.
- `closeType == 2` için kenar çizgisi üzerinden kapanış hattı oluşturur.
- Başlangıç ve bitiş noktaları, oluşturulan iki çizginin kesişimi ile bulunur.

## Poligon merkez ve alan hesaplama

`CalculatePolygonCentroidAndArea`:

- Shoelace (çokgen alanı) yöntemi kullanılır.
- Alan `area = 0.5 * |aSum|` olarak hesaplanır.
- Merkez koordinatları `xSum / (3 * aSum)` ve `ySum / (3 * aSum)` formülüyle bulunur.
- Degenerate (çok küçük alan) durumda merkez `(0,0)` döner.

## Sonuçların UI’a yansıması

- Hesaplanan gruplar `resultGroups` ile tabloda gösterilir.
- Poligonlar ve noktalar `RenderSvg` içinde çizilir.
- Grup merkezi kırmızı daire ile işaretlenir.
- Bildirim metni `statusMessage` ile kullanıcıya aktarılır.
