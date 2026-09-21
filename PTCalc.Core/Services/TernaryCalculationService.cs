using PTCalc.Core.Localization;

namespace PTCalc.Core.Services;

using PTCalc.Core.Models;

public class TernaryCalculationService
{
    private const double Sin60 = 0.86602540378443864676372317075294;
    private const double Cos60 = 0.5;
    private const double Tan30 = 0.57735026918962576450914878050196;

    private double _lastArea;

    /// <summary>
    /// Ternary koordinatlarını (a, b, c) kartezyen koordinatlara (x, y) dönüştürür
    /// </summary>
    public PointD Abc2Xy(double k1, double k2, int tip)
    {
        var result = new PointD();
        
        switch (tip)
        {
            case 32:
                result.X = (1 - k1 + k2) / 2;
                result.Y = (1 - k1 - k2) * Sin60;
                break;
            case 31:
                result.X = (1 - k1) - Cos60 * k2;
                result.Y = k2 * Sin60;
                break;
            case 21:
                result.X = k1 + Cos60 * k2;
                result.Y = k2 * Sin60;
                break;
        }
        
        return result;
    }

    /// <summary>
    /// Kartezyen koordinatları (x, y) ternary koordinatlara dönüştürür
    /// </summary>
    public double Xy2Abc(double xk, double yk, int tip)
    {
        return tip switch
        {
            1 => 1 - (yk * Tan30 + xk),  // Oil (a)
            2 => xk - yk * Tan30,         // Surfactant (b)
            3 => yk / Sin60,              // Water (c)
            _ => 0
        };
    }

    /// <summary>
    /// İki çizginin kesişim noktasını hesaplar
    /// </summary>
    public PointD IntersectionPoint(LineD l1, LineD l2)
    {
        double xk1 = l1.U1!.X, yk1 = l1.U1.Y;
        double xk2 = l1.U2!.X, yk2 = l1.U2.Y;
        double xk3 = l2.U1!.X, yk3 = l2.U1.Y;
        double xk4 = l2.U2!.X, yk4 = l2.U2.Y;

        double uau = (xk4 - xk3) * (yk1 - yk3) - (yk4 - yk3) * (xk1 - xk3);
        double uaa = (yk4 - yk3) * (xk2 - xk1) - (xk4 - xk3) * (yk2 - yk1);
        double ua = uau / uaa;

        return new PointD
        {
            X = xk1 + ua * (xk2 - xk1),
            Y = yk1 + ua * (yk2 - yk1)
        };
    }

    /// <summary>
    /// Polygon'un merkez noktasını ve alanını hesaplar
    /// </summary>
    public PointD PolygonCentroid(List<PointD> polygon, out double area)
    {
        if (polygon.Count < 3)
            throw new ArgumentException("Polygon must have at least 3 points");

        double aSum = 0, xSum = 0, ySum = 0;

        for (int i = 0; i < polygon.Count - 1; i++)
        {
            int j = i + 1;
            double term = polygon[i].X * polygon[j].Y - polygon[j].X * polygon[i].Y;
            aSum += term;
            xSum += (polygon[j].X + polygon[i].X) * term;
            ySum += (polygon[j].Y + polygon[i].Y) * term;
        }

        area = 0.5 * Math.Abs(aSum);
        _lastArea = area;

        if (Math.Abs(aSum) < double.Epsilon)
            throw new InvalidOperationException("Polygon has zero area");

        return new PointD
        {
            X = xSum / (3.0 * aSum),
            Y = ySum / (3.0 * aSum)
        };
    }

    /// <summary>
    /// Veri noktalarını XY koordinatlarına dönüştürür
    /// </summary>
    public void ConvertDataToXY(List<Ttridata> data)
    {
        foreach (var item in data)
        {
            // Oil, Sur/CoSur, Water değerlerini normalize et (0-1 aralığına)
            double oil = item.oil / 100.0;
            double sur = item.surCoSur / 100.0;
            double water = item.water / 100.0;

            // Ternary koordinatları XY'ye dönüştür
            item.p = Abc2Xy(water, oil, 21);
        }
    }

    /// <summary>
    /// Grupları hesaplar ve polygon merkezlerini bulur
    /// </summary>
    public List<TGroup> CalculateGroups(List<Ttridata> data, int polygonCloseType, int edgeType)
    {
        var groups = new List<TGroup>();

        // Önce verileri XY koordinatlarına dönüştür
        ConvertDataToXY(data);

        // Grupları bul
        var groupNumbers = data.Select(d => d.grup).Distinct().OrderBy(g => g).ToList();

        foreach (var groupNum in groupNumbers)
        {
            var groupData = data.Where(d => d.grup == groupNum).OrderBy(d => d.siraNo).ToList();

            // Tek satırlı gruplar da sonuç listesine girer: sayfada nokta olarak çizilir ve
            // tabloda görünür. Eskiden < 2 satır atlanıyor, kullanıcı o grubu hiç görmüyordu.
            var validPoints = groupData.Where(d => d.p != null).Select(d => d.p!).ToList();
            if (validPoints.Count == 0)
                continue;

            var group = new TGroup
            {
                GroupIndex = groupNum,
                N = groupNum,
                PolygonType = polygonCloseType,
                EdgeType = edgeType
            };

            try
            {
                // 1–2 nokta: bölge yok. Merkez nokta ya da orta nokta, alan 0. Kapatma noktası
                // eklenmez; iki ölçümden dörtgen bölge çıkarmak yanıltıcı olurdu.
                if (validPoints.Count < 3)
                {
                    var c = validPoints.Count == 1
                        ? validPoints[0]
                        : new PointD { X = (validPoints[0].X + validPoints[1].X) / 2, Y = (validPoints[0].Y + validPoints[1].Y) / 2 };
                    group.PolygonCenterD = c;
                    group.Area = 0;
                    group.A = Xy2Abc(c.X, c.Y, 1) * 100;
                    group.B = Xy2Abc(c.X, c.Y, 2) * 100;
                    group.C = Xy2Abc(c.X, c.Y, 3) * 100;
                    groups.Add(group);
                    continue;
                }

                // Polygon noktalarını oluştur
                var polygon = CreatePolygon(groupData, polygonCloseType, edgeType);
                
                if (polygon.Count >= 3)
                {
                    // Merkez ve alan hesapla
                    var center = PolygonCentroid(polygon, out double area);
                    
                    group.PolygonCenterD = center;
                    group.Area = area * 10000; // 100x100 ölçeğine çevir
                    
                    // ABC koordinatlarını hesapla
                    group.A = Xy2Abc(center.X, center.Y, 1) * 100;
                    group.B = Xy2Abc(center.X, center.Y, 2) * 100;
                    group.C = Xy2Abc(center.X, center.Y, 3) * 100;
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Group {groupNum} calculation error: {ex.Message}");
            }

            groups.Add(group);
        }

        return groups;
    }

    /// <summary>
    /// Grup verilerinden polygon oluşturur
    /// </summary>
    private List<PointD> CreatePolygon(List<Ttridata> groupData, int polygonCloseType, int edgeType)
    {
        var polygon = new List<PointD>();

        // İlk nokta (kapatma için placeholder)
        polygon.Add(new PointD { X = 0, Y = 0 });

        // Veri noktalarını ekle
        foreach (var item in groupData)
        {
            if (item.p != null)
            {
                polygon.Add(new PointD { X = item.p.X, Y = item.p.Y });
            }
        }

        // Son nokta (kapatma için placeholder)
        polygon.Add(new PointD { X = 0, Y = 0 });

        // Polygon'u kapat
        ClosePolygon(polygon, groupData, polygonCloseType, edgeType);

        return polygon;
    }

    /// <summary>
    /// Polygon'u belirtilen tipe göre kapatır
    /// </summary>
    private void ClosePolygon(List<PointD> polygon, List<Ttridata> groupData, int polygonCloseType, int edgeType)
    {
        if (polygon.Count < 3)
            return;

        var c1 = new LineD { U1 = new PointD(), U2 = new PointD() };
        var c2 = new LineD { U1 = new PointD(), U2 = new PointD() };

        // Close Min Value (polygonCloseType == 1)
        if (polygonCloseType == 1)
        {
            double minValue;
            switch (edgeType)
            {
                case 0: // Oil
                    minValue = groupData.Min(d => d.oil) / 100.0;
                    c1.U1 = Abc2Xy(0, minValue, 21);
                    c1.U2 = Abc2Xy(0, minValue, 31);
                    break;
                case 1: // Surfactant
                    minValue = groupData.Min(d => d.surCoSur) / 100.0;
                    c1.U1 = Abc2Xy(minValue, 0, 21);
                    c1.U2 = Abc2Xy(0, minValue, 32);
                    break;
                case 2: // Water
                    minValue = groupData.Min(d => d.water) / 100.0;
                    c1.U1 = Abc2Xy(minValue, 0, 31);
                    c1.U2 = Abc2Xy(minValue, 0, 32);
                    break;
            }
        }
        // Close Edge (polygonCloseType == 2)
        else if (polygonCloseType == 2)
        {
            switch (edgeType)
            {
                case 0: // Oil
                    c1.U1 = Abc2Xy(0, 0, 21);
                    c1.U2 = Abc2Xy(0, 0, 31);
                    break;
                case 1: // Surfactant
                    c1.U1 = Abc2Xy(0, 0, 32);
                    c1.U2 = Abc2Xy(0, 0, 21);
                    break;
                case 2: // Water
                    c1.U1 = Abc2Xy(0, 0, 31);
                    c1.U2 = Abc2Xy(0, 0, 32);
                    break;
            }
        }

        // Polygon kapatma noktalarını hesapla
        if (polygonCloseType >= 1)
        {
            // Edge köşe noktası
            switch (edgeType)
            {
                case 0: // Oil (üst köşe)
                    c2.U1 = new PointD { X = 0.5, Y = Sin60 };
                    break;
                case 1: // Surfactant (sağ alt köşe)
                    c2.U1 = new PointD { X = 1, Y = 0 };
                    break;
                case 2: // Water (sol alt köşe)
                    c2.U1 = new PointD { X = 0, Y = 0 };
                    break;
            }

            // İlk veri noktasıyla kesişim
            if (groupData.Count > 0 && groupData[0].p != null)
            {
                c2.U2 = groupData[0].p;
                var intersection = IntersectionPoint(c1, c2);
                polygon[0] = intersection;
                polygon[^1] = intersection; // Son nokta da aynı
            }

            // Son veri noktasıyla kesişim
            if (groupData.Count > 0 && groupData[^1].p != null)
            {
                c2.U2 = groupData[^1].p;
                var intersection = IntersectionPoint(c1, c2);
                polygon[^2] = intersection;
            }
        }
        // Close Polygon (polygonCloseType == 0) - Son nokta ilk noktaya bağlanır
        else
        {
            if (groupData.Count > 0 && groupData[^1].p != null)
            {
                polygon[0] = new PointD { X = groupData[^1].p!.X, Y = groupData[^1].p!.Y };
            }
        }
    }

    /// <summary>
    /// Tam hesaplama yapar ve sonuçları döndürür
    /// </summary>
    public DiagramCalculationResponse Calculate(DiagramCalculationRequest request)
    {
        var response = new DiagramCalculationResponse();

        try
        {
            if (request.Data == null || request.Data.Count == 0)
            {
                response.Success = false;
                response.Message = CoreText.T("Veri bulunamadı");
                return response;
            }

            var groups = CalculateGroups(
                request.Data,
                request.Configuration.PolygonCloseType,
                request.Configuration.PolygonCloseEdge
            );

            response.Groups = groups;
            response.Success = true;
            response.Message = CoreText.T("{0} grup hesaplandı", groups.Count);
        }
        catch (Exception ex)
        {
            response.Success = false;
            response.Message = CoreText.T("Hesaplama hatası: {0}", ex.Message);
        }

        return response;
    }
}
