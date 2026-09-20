using EgePharmTech.Core.Models;

namespace EgePharmTech.Core.Tests;

/// <summary>Üçgen faz diyagramı belgesi: JSON gidiş-dönüş ve grup stili çözümleme.</summary>
public class TernaryDocumentTests
{
    [Fact]
    public void Document_round_trips_through_json_with_primitive_cells()
    {
        var doc = new TernaryDocument
        {
            Rows = { new object?[] { "Deney 1", 1.0, 8.14, 72.67, 19.19 }, new object?[] { "Deney 1", 2.0, 16.87, 65.43, 17.7 } },
            Settings = new TernarySettings { ChartTitle = "Test", SmoothEdges = true, SmoothTension = 80, PolygonCloseType = 2 },
            Styles = { ["Deney 1"] = new GroupStyle { Fill = "#112233", FillAlpha = 120 } }
        };

        var back = TernaryDocument.FromJson(doc.ToJson());

        Assert.NotNull(back);
        Assert.Equal(2, back!.Rows.Count);
        Assert.Equal("Deney 1", back.Rows[0][0]);          // JsonElement değil, string
        Assert.Equal(8.14, Assert.IsType<double>(back.Rows[0][2]));
        Assert.Equal("Test", back.Settings.ChartTitle);
        Assert.True(back.Settings.SmoothEdges);
        Assert.Equal(80, back.Settings.SmoothTension);
        Assert.Equal(2, back.Settings.PolygonCloseType);
        Assert.Equal("#112233", back.Styles["deney 1"].Fill);   // ada göre, büyük/küçük harf duyarsız
        Assert.Equal(120, back.Styles["Deney 1"].FillAlpha);
        Assert.Null(back.Styles["Deney 1"].Stroke);
    }

    [Fact]
    public void Blank_rows_are_dropped_and_bad_json_yields_null()
    {
        var doc = new TernaryDocument { Rows = { new object?[] { null, null, null }, new object?[] { "A", 1.0, 1, 2, 3 } } };
        var back = TernaryDocument.FromJson(doc.ToJson());
        Assert.Single(back!.Rows);

        Assert.Null(TernaryDocument.FromJson(null));
        Assert.Null(TernaryDocument.FromJson("   "));
        Assert.Null(TernaryDocument.FromJson("{ not json"));
        Assert.Null(TernaryDocument.FromJson("{\"version\": 99}"));   // gelecekten gelen sürüm
    }

    [Fact]
    public void Missing_style_fields_fall_back_to_page_defaults()
    {
        var e = GroupStyles.Resolve(null, "#3b82f6", 80, 180);
        Assert.Equal("#3b82f6", e.Fill);
        Assert.Equal("#3b82f6", e.Stroke);
        Assert.Equal(80 / 255.0, e.FillOpacity, 12);
        Assert.Equal(180 / 255.0, e.StrokeOpacity, 12);
        Assert.Equal(GroupStyles.DefaultStrokeWidth, e.StrokeWidth);

        var partial = GroupStyles.Resolve(new GroupStyle { Stroke = "#FF0000", StrokeWidth = 4 }, "#3b82f6", 80, 180);
        Assert.Equal("#3b82f6", partial.Fill);       // dolgu varsayılan kaldı
        Assert.Equal("#ff0000", partial.Stroke);     // normalize edildi
        Assert.Equal(4, partial.StrokeWidth);
    }

    [Fact]
    public void Invalid_colors_and_out_of_range_values_are_ignored()
    {
        var e = GroupStyles.Resolve(new GroupStyle { Fill = "red", Stroke = "#12", FillAlpha = 999, StrokeAlpha = -5, StrokeWidth = 500 },
                                    "#000000", 80, 180);
        Assert.Equal("#000000", e.Fill);
        Assert.Equal("#000000", e.Stroke);
        Assert.Equal(1.0, e.FillOpacity);
        Assert.Equal(0.0, e.StrokeOpacity);
        Assert.Equal(GroupStyles.DefaultStrokeWidth, e.StrokeWidth);
    }
}
