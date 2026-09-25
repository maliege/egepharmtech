"""OIML R 22 (1975) alkolometri tablolarından test referansı üretir.

Kullanım:
    python tools/make_oiml_reference.py "<OIML R 22 tablolarının PDF'i>"

Çıktı: PTCalc.Core.Tests/oiml_r22_reference.json

PDF depoda yoktur (OIML yayını). Tablolar taranmış sayfalardan metin olarak okunur; OCR hataları
(yanlış okunan rakam, yanlış satır numarası) hesaplama kodu kullanılmadan, yalnız tablonun kendi iç
tutarlılığıyla ayıklanır:
  * satır başındaki ve sonundaki satır numarası aynı olmalı, aynı anahtar iki kez görünmemeli;
  * her hücre, satırdaki komşularından doğrusal olarak beklenen değerden en çok TOL kadar sapmalı
    (tablolar düzgün fonksiyonlardır; ikinci farklar basım duyarlığı olan 0,01 düzeyindedir).
Bu süzgeçten geçen hücreler, 2 ondalıkla basılmış tablo değerleridir.

Gereksinim: pypdf.
"""
import json
import re
import sys
from pathlib import Path

from pypdf import PdfReader

TOL = 0.025
OUT = Path(__file__).resolve().parent.parent / "PTCalc.Core.Tests" / "oiml_r22_reference.json"

# Tablo -> (PDF sayfaları, satır adımı, sabit sıcaklık ya da None = sütunlar sıcaklık)
TABLES = {
    "I":    (range(16, 28), None),   # ρ(p, t), sütunlar t
    "II":   (range(30, 42), None),   # ρ(q, t), sütunlar t
    "IIIa": (range(44, 46), 20),     # ρ20(p), sütunlar p'nin ondalığı
    "IIIb": (range(48, 50), 20),     # q(p)
    "IVa":  (range(52, 54), 20),     # ρ20(q)
    "IVb":  (range(56, 58), 20),     # p(q)
    "Va":   (range(60, 65), 20),     # p(ρ20)
    "Vb":   (range(66, 71), 20),     # q(ρ20)
}
# Tablo I ve II çok büyük; testte her 5 °C'lik sütun yeterli.
TEMPERATURE_STEP = 5

OCR_FIX = str.maketrans({"o": "0", "O": "0", "l": "1", "I": "1", "S": "5", "s": "5"})
CELL = re.compile(r"^\d+,\d\d$")
INT = re.compile(r"^-?\d+$")


def tokens(line):
    line = line.translate(OCR_FIX)
    line = re.sub(r"(\d)\s*([,.])\s*(\d)", r"\1,\3", line)
    return line.split()


def smooth_mask(values):
    """values: float ya da None listesi -> komşularla tutarlı hücreler için True."""
    n = len(values)
    keep = [False] * n
    for j, v in enumerate(values):
        if v is None:
            continue
        preds = []
        if 0 < j < n - 1 and values[j - 1] is not None and values[j + 1] is not None:
            preds.append((values[j - 1] + values[j + 1]) / 2)
        if j >= 2 and values[j - 1] is not None and values[j - 2] is not None:
            preds.append(2 * values[j - 1] - values[j - 2])
        if j <= n - 3 and values[j + 1] is not None and values[j + 2] is not None:
            preds.append(2 * values[j + 1] - values[j + 2])
        # En az bir tahminle uyuşması yetmez: tüm mevcut tahminlerle uyuşmalı.
        keep[j] = bool(preds) and all(abs(v - p) <= TOL for p in preds)
    return keep


def parse(reader, pages, fixed_t):
    cells = {}
    seen = {}
    for pg in pages:
        text = reader.pages[pg - 1].extract_text() or ""
        columns = None
        for line in text.splitlines():
            raw = line.split()
            if fixed_t is None and columns is None:
                ints = [x for x in raw if INT.match(x)]
                if len(ints) >= 8:
                    start = int(ints[0])
                    columns = list(range(start, start + 11))
                    continue
            tk = tokens(line)
            if len(tk) < 4 or not INT.match(tk[0]) or not INT.match(tk[-1]) or tk[0] != tk[-1]:
                continue
            base = int(tk[0])
            row = tk[1:-1]
            expected = 10 if fixed_t is not None else 11
            if len(row) != expected or (fixed_t is None and columns is None):
                continue
            values = [float(c.replace(",", ".")) if CELL.match(c) else None for c in row]
            mask = smooth_mask(values)
            for j, (v, ok) in enumerate(zip(values, mask)):
                if fixed_t is not None:
                    key = (round(base + j / 10, 1), fixed_t)
                else:
                    key = (float(base), columns[j])
                seen[key] = seen.get(key, 0) + 1
                if ok:
                    cells[key] = v
    return {k: v for k, v in cells.items() if seen[k] == 1}


def main():
    if len(sys.argv) != 2:
        sys.exit(__doc__)
    reader = PdfReader(sys.argv[1])
    out = {
        "source": "OIML R 22 (1975) International Alcoholometric Tables, English edition (BIML)",
        "note": "Printed values to 2 decimals; cells with OCR errors removed by row consistency "
                "(tools/make_oiml_reference.py). Rows: [x, t, value]; x = p (% mass), q (% vol) "
                "or rho20 (kg/m3) depending on the table.",
        "tables": {},
    }
    for name, (pages, fixed_t) in TABLES.items():
        cells = parse(reader, pages, fixed_t)
        if fixed_t is None:
            cells = {k: v for k, v in cells.items() if k[1] % TEMPERATURE_STEP == 0}
        rows = [[k[0], k[1], v] for k, v in sorted(cells.items())]
        out["tables"][name] = rows
        print(f"Tablo {name}: {len(rows)} hücre")
    text = json.dumps(out, ensure_ascii=False, separators=(",", ":"))
    # Her satır bir hücre: fark incelemesi okunur kalsın.
    text = text.replace("],[", "],\n[")
    OUT.write_text(text + "\n", encoding="utf-8", newline="\r\n")
    print(f"Yazıldı: {OUT} ({OUT.stat().st_size // 1024} KB)")


if __name__ == "__main__":
    main()
