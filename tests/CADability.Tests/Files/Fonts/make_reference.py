# Writes LiberationSans-Regular.reference.json, the reference data of TrueTypeFontTests, with fontTools
# (pip install fonttools), an implementation independent of CADability's TrueType reader.
# Run from this directory: python make_reference.py
import json
from fontTools.ttLib import TTFont

font = TTFont('LiberationSans-Regular.ttf')
glyf, cmap, hmtx = font['glyf'], font.getBestCmap(), font['hmtx']
# printable ASCII, composite glyphs (accents, also with negative byte offsets: ÌÎïĪ¼) and some curves
chars = ''.join(chr(c) for c in range(32, 127)) + 'ÄÖÜäöüßéÅ€°±ÌÎïĪ¼'
glyphs = []
for ch in chars:
    name = cmap[ord(ch)]
    coords, endPts, flags = glyf[name].getCoordinates(glyf)
    contours, start = [], 0
    for end in endPts:
        contours.append([[int(round(coords[i][0])), int(round(coords[i][1])), flags[i] & 1] for i in range(start, end + 1)])
        start = end + 1
    glyphs.append({'char': ch, 'glyph': font.getGlyphID(name), 'advance': hmtx[name][0],
                   'composite': glyf[name].isComposite(), 'contours': contours})
kern = font['kern'].kernTables[0].kernTable
pairs = [{'left': a, 'right': b, 'value': kern.get((cmap[ord(a)], cmap[ord(b)]), 0)}
         for a, b in ['AV', 'To', 'LT', 'AT', 'Ya', 'F.']]
names = font['name']
reference = {'family': names.getDebugName(1), 'subfamily': names.getDebugName(2), 'full': names.getDebugName(4),
             'unitsPerEm': font['head'].unitsPerEm, 'ascender': font['hhea'].ascent, 'descender': font['hhea'].descent,
             'lineGap': font['hhea'].lineGap, 'winAscent': font['OS/2'].usWinAscent, 'winDescent': font['OS/2'].usWinDescent,
             'glyphCount': font['maxp'].numGlyphs, 'kerningPairCount': len(kern), 'kerning': pairs, 'glyphs': glyphs}
with open('LiberationSans-Regular.reference.json', 'w', encoding='utf-8') as f:
    json.dump(reference, f, ensure_ascii=False, separators=(',', ':'))
