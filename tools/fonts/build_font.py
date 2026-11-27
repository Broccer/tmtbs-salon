"""
3B salonun yazı tipi: Noto Sans (Latin/Kiril/Yunan) + Noto Sans SC (sık kullanılan Çince + Japonca kana/kanji)
+ Noto Sans KR (sık kullanılan Hangul) + Noto Sans Arabic + Devanagari + Bengali tek bir TTF'de birleşir.

Unity'nin IMGUI/TextMesh yazısı GSUB şekillendirmesi yapmaz. Bu yüzden Arapça "sunum biçimleri"
(U+FB50–FDFF, U+FE70–FEFF) fontun GSUB tablosundan bulunup cmap'e eklenir; oyundaki ArabicShaper
harfleri bu kod noktalarına çevirir. CI'da (GitHub Actions) çalışır: pip install fonttools brotli.

Kullanım: python build_font.py <çıktı.ttf>
"""
import os
import sys
import unicodedata
import urllib.request

from fontTools.merge import Merger, Options as MergeOptions
from fontTools.subset import Options as SubsetOptions, Subsetter
from fontTools.ttLib import TTFont
from fontTools.varLib.instancer import instantiateVariableFont

BASE = "https://github.com/google/fonts/raw/main/ofl/"
FONTS = {
    "latin": "notosans/NotoSans%5Bwdth,wght%5D.ttf",
    "sc": "notosanssc/NotoSansSC%5Bwght%5D.ttf",
    "kr": "notosanskr/NotoSansKR%5Bwght%5D.ttf",
    "arabic": "notosansarabic/NotoSansArabic%5Bwdth,wght%5D.ttf",
    "deva": "notosansdevanagari/NotoSansDevanagari%5Bwdth,wght%5D.ttf",
    "beng": "notosansbengali/NotoSansBengali%5Bwdth,wght%5D.ttf",
    "sym": "notosansmath/NotoSansMath-Regular.ttf",
}

WORK = os.path.join(os.path.dirname(os.path.abspath(__file__)), "_work")


def fetch(key):
    os.makedirs(WORK, exist_ok=True)
    path = os.path.join(WORK, key + ".ttf")
    if not os.path.exists(path):
        print("indiriliyor", FONTS[key])
        urllib.request.urlretrieve(BASE + FONTS[key], path)
    return path


def static(path):
    f = TTFont(path)
    if "fvar" in f:
        axes = {a.axisTag: a.defaultValue for a in f["fvar"].axes}
        axes["wght"] = 400
        if "wdth" in axes:
            axes["wdth"] = 100
        f = instantiateVariableFont(f, axes)
    return f


def rng(a, b):
    return list(range(a, b + 1))


def decode_range(enc, hi, lo):
    out = []
    for h in range(hi[0], hi[1] + 1):
        for l in range(lo[0], lo[1] + 1):
            try:
                ch = bytes([h, l]).decode(enc)
            except UnicodeDecodeError:
                continue
            if len(ch) == 1:
                out.append(ord(ch))
    return out


def subset(font, unicodes, keep_layout=False):
    o = SubsetOptions()
    o.layout_features = ["*"] if keep_layout else []
    o.name_IDs = ["*"]
    o.notdef_outline = True
    o.glyph_names = False
    o.hinting = False
    o.drop_tables += ["GSUB", "GPOS", "GDEF", "STAT", "MVAR", "HVAR", "VVAR", "vhea", "vmtx", "DSIG", "MATH"]
    s = Subsetter(options=o)
    s.populate(unicodes=unicodes)
    s.subset(font)
    return font


def arabic_presentation(font):
    """GSUB'daki init/medi/fina/isol ve lam-elif bağlarından sunum biçimi kod noktalarını cmap'e ekler."""
    cmap = font.getBestCmap()
    gsub = font["GSUB"].table if "GSUB" in font else None
    if gsub is None:
        return {}
    feats = {}
    for fr in gsub.FeatureList.FeatureRecord:
        feats.setdefault(fr.FeatureTag, []).extend(fr.Feature.LookupListIndex)

    def single_map(tag):
        m = {}
        for li in feats.get(tag, []):
            lk = gsub.LookupList.Lookup[li]
            for st in lk.SubTable:
                if lk.LookupType == 7:
                    st = st.ExtSubTable
                if hasattr(st, "mapping"):
                    for k, v in st.mapping.items():
                        m.setdefault(k, v)
        return m

    def liga_map():
        m = {}
        for tag in ("rlig", "liga", "calt"):
            for li in feats.get(tag, []):
                lk = gsub.LookupList.Lookup[li]
                for st in lk.SubTable:
                    if lk.LookupType == 7:
                        st = st.ExtSubTable
                    if hasattr(st, "ligatures"):
                        for first, ligs in st.ligatures.items():
                            for lg in ligs:
                                m.setdefault((first,) + tuple(lg.Component), lg.LigGlyph)
        return m

    forms = {"<isolated>": single_map("isol"), "<final>": single_map("fina"),
             "<initial>": single_map("init"), "<medial>": single_map("medi")}
    ligs = liga_map()
    fina = forms["<final>"]
    added = {}
    for cp in rng(0xFB50, 0xFDFF) + rng(0xFE70, 0xFEFF):
        try:
            dec = unicodedata.decomposition(chr(cp))
        except ValueError:
            continue
        if not dec:
            continue
        parts = dec.split()
        tag, base = parts[0], [int(x, 16) for x in parts[1:]]
        if tag not in forms:
            continue
        if any(b not in cmap for b in base):
            continue
        if len(base) == 1:
            g = cmap[base[0]]
            if tag == "<isolated>":
                added[cp] = forms[tag].get(g, g)
            elif g in forms[tag]:
                added[cp] = forms[tag][g]
        elif len(base) == 2 and base[0] == 0x0644:  # lam + elif bağları
            g1, g2 = cmap[base[0]], cmap[base[1]]
            cands = [(g1, g2)]
            if tag in ("<final>",):
                cands.append((fina.get(g1, g1), fina.get(g2, g2)))
            for k in list(ligs.keys()):
                if len(k) == 2 and k[1] in (g2, fina.get(g2, g2)) and k[0] in (g1, forms["<medial>"].get(g1, g1), forms["<initial>"].get(g1, g1)):
                    want_final = tag == "<final>"
                    is_med = k[0] == forms["<medial>"].get(g1)
                    if want_final == is_med:
                        added[cp] = ligs[k]
                        break
    for table in font["cmap"].tables:
        if table.isUnicode():
            for cp, g in added.items():
                table.cmap.setdefault(cp, g)
    print("arapça sunum biçimi eklendi:", len(added))
    return added


def main(out):
    latin = rng(0x20, 0x7E) + rng(0xA0, 0x24F) + rng(0x250, 0x2AF) + rng(0x2B0, 0x36F) + rng(0x370, 0x3FF) + \
        rng(0x400, 0x52F) + rng(0x1E00, 0x1EFF) + rng(0x2000, 0x206F) + rng(0x20A0, 0x20CF) + rng(0x2100, 0x218F) + \
        rng(0x2190, 0x21FF) + rng(0x2200, 0x22FF) + rng(0x25A0, 0x25FF) + rng(0x2600, 0x26FF) + rng(0x2700, 0x27BF)
    han = decode_range("gb2312", (0xB0, 0xF7), (0xA1, 0xFE))          # GB2312 (sık Çince, 6763)
    jis = decode_range("shift_jis", (0x88, 0x9F), (0x40, 0xFC))         # JIS 1. düzey kanji
    big5 = decode_range("big5", (0xA4, 0xC6), (0x40, 0xFE))             # sık geleneksel Çince
    cjk = han + jis + big5 + rng(0x3000, 0x303F) + rng(0x3040, 0x30FF) + rng(0xFF00, 0xFFEF) + rng(0x31F0, 0x31FF)
    hangul = decode_range("euc_kr", (0xB0, 0xC8), (0xA1, 0xFE)) + rng(0x3130, 0x318F)
    arabic = rng(0x0600, 0x06FF) + rng(0x0750, 0x077F) + rng(0xFB50, 0xFDFF) + rng(0xFE70, 0xFEFF)
    deva = rng(0x0900, 0x097F) + rng(0xA8E0, 0xA8FF) + [0x200C, 0x200D]
    beng = rng(0x0980, 0x09FF) + [0x200C, 0x200D]

    parts = []
    f = subset(static(fetch("latin")), latin)
    parts.append(f)
    sym_ok = False
    # Noto Sans'ta ok (→ ← ↑ ↓) ve geometrik (▲ ▼ ■) glifleri yok: Noto Sans Math'ten al.
    try:
        sym = rng(0x2190, 0x21FF) + rng(0x25A0, 0x25FF) + rng(0x2600, 0x26FF) + rng(0x2700, 0x27BF)
        have = set(f.getBestCmap().keys())
        parts.append(subset(static(fetch("sym")), [c for c in sym if c not in have]))
        sym_ok = True
    except Exception as e:
        print("sembol yazı tipi atlandı:", e)
    for key, cps in (("sc", cjk), ("kr", hangul), ("deva", deva), ("beng", beng)):
        parts.append(subset(static(fetch(key)), cps))
    ar = static(fetch("arabic"))
    ar = subset(ar, arabic, keep_layout=True)
    arabic_presentation(ar)
    ar = subset(ar, arabic + rng(0xFB50, 0xFDFF) + rng(0xFE70, 0xFEFF))
    parts.append(ar)

    paths = []
    for i, p in enumerate(parts):
        for t in ("GSUB", "GPOS", "GDEF"):
            if t in p:
                del p[t]
        if p["head"].unitsPerEm != 1000:
            raise SystemExit("unitsPerEm farklı: %d" % p["head"].unitsPerEm)
        path = os.path.join(WORK, "part%d.ttf" % i)
        p.save(path)
        paths.append(path)

    mo = MergeOptions()
    mo.drop_tables = ["GSUB", "GPOS", "GDEF", "vhea", "vmtx"]
    try:
        merged = Merger(options=mo).merge(paths)
    except Exception as e:
        if not sym_ok:
            raise
        print("sembolle birleştirme başarısız, sembolsüz:", e)
        merged = Merger(options=mo).merge([paths[0]] + paths[2:])
    name = merged["name"]
    for rec in name.names:
        if rec.nameID in (1, 4):
            rec.string = "TMTBS Sans"
        elif rec.nameID == 6:
            rec.string = "TMTBSSans-Regular"
    merged.save(out)
    print("yazdı", out, os.path.getsize(out) // 1024, "KB, glif:", len(merged.getGlyphOrder()))


if __name__ == "__main__":
    main(sys.argv[1])
