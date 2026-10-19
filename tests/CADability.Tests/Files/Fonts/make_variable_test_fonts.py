# Writes the variable test fonts of VariableFontTests and their reference data with fontTools (pip install fonttools):
#
#   VarTestSans.ttf          TrueType outlines with "gvar" (sparse deltas with IUP, intermediate regions, composite glyphs),
#                            "avar" and "HVAR"
#   VarTestSans-NoHVAR.ttf   the same without "HVAR": the advance widths come from the phantom points of "gvar"
#   VarTestSans.otf          CFF2 outlines (blend, vsindex) with "avar" and "HVAR"
#   VarTestSans.reference.json   the outlines and advance widths as fontTools computes them at a named instance and two
#                                other locations, for a selection of characters (all advance widths and the strikeout line,
#                                which "MVAR" varies, at all named instances)
#
# They are subsets of Source Sans 3 VF (SIL Open Font License 1.1, https://github.com/adobe-fonts/source-sans), taken from
# the npm package "source-sans". "Source" is a Reserved Font Name of these fonts, so the subsets, which are modified versions,
# are renamed to "VarTest Sans" (the three files get different family names, VarTestSans, VarTestSansNoHVAR and
# VarTestSansCFF2, so that a font provider can tell them apart). Run from this directory with the directory of SourceSans3VF-Upright.ttf and .otf:
#   python make_variable_test_fonts.py <package>/VF
import json, os, sys
from fontTools.ttLib import TTFont
from fontTools.subset import Subsetter, Options
from fontTools.pens.basePen import BasePen
from fontTools.varLib import instancer

UNICODES = list(range(0x20, 0x7F)) + [0xC4, 0xD6, 0xDC, 0xE4, 0xF6, 0xFC, 0xE9, 0xC5]
OUTLINES = 'AEOSaegs&@8%ÄéÅ'  # the characters whose outlines are compared, composite glyphs included


class CubicPen(BasePen):
    """Records the outline as moveTo, lineTo, curveTo and closePath; quadratic segments become cubic ones without loss."""

    def __init__(self, glyphSet):
        super().__init__(glyphSet)
        self.value = []

    def _moveTo(self, p):
        self.value.append(['moveTo', list(p)])

    def _lineTo(self, p):
        self.value.append(['lineTo', list(p)])

    def _curveToOne(self, p1, p2, p3):
        self.value.append(['curveTo', list(p1) + list(p2) + list(p3)])

    def _qCurveToOne(self, p1, p2):
        p0 = self._getCurrentPoint()
        c1 = (p0[0] + 2 / 3 * (p1[0] - p0[0]), p0[1] + 2 / 3 * (p1[1] - p0[1]))
        c2 = (p2[0] + 2 / 3 * (p1[0] - p2[0]), p2[1] + 2 / 3 * (p1[1] - p2[1]))
        self.value.append(['curveTo', list(c1) + list(c2) + list(p2)])

    def _closePath(self):
        self.value.append(['closePath', []])

    def _endPath(self):
        self.value.append(['closePath', []])


def make(source, target, family, drop_hvar=False):
    options = Options()
    options.layout_features = []
    options.name_IDs = ['*']
    options.drop_tables += ['DSIG', 'BASE', 'GDEF', 'GPOS', 'GSUB']
    font = TTFont(source)
    subsetter = Subsetter(options)
    subsetter.populate(unicodes=UNICODES)
    subsetter.subset(font)
    if drop_hvar:
        del font['HVAR']
    names = font['name']
    for record in list(names.names):
        if record.nameID == 7:  # the trademark notice of the original name
            names.removeNames(nameID=7)
            continue
        if record.nameID == 0:  # the copyright stays
            continue
        text = record.toUnicode()
        for old, new in (('Source Sans 3', family), ('SourceSans3VF', family), ('SourceSans3', family), ('Source Sans', family)):
            text = text.replace(old, new)
        record.string = text
    remaining = [r.toUnicode() for r in names.names if r.nameID != 0 and 'Source' in r.toUnicode()]
    assert not remaining, remaining
    font.save(target)


def reference(path):
    font = TTFont(path)
    axes = font['fvar'].axes
    instances = [dict(i.coordinates) for i in font['fvar'].instances]
    order = font.getGlyphOrder()
    cmap = font.getBestCmap()
    r = lambda v: round(v, 6)
    result = {'file': os.path.basename(path), 'family': font['name'].getDebugName(1),
              'instances': [font['name'].getDebugName(i.subfamilyNameID) for i in font['fvar'].instances],
              'advances': [], 'locations': []}
    for loc in instances:  # all advance widths and the strikeout line ("MVAR") at all named instances
        glyphSet = font.getGlyphSet(location=loc, normalized=False)
        static = instancer.instantiateVariableFont(TTFont(path), loc)
        result['advances'].append({'coordinates': [loc[a.axisTag] for a in axes],
                                   'widths': [r(glyphSet[cmap[c]].width) for c in UNICODES],
                                   'strikeout': [static['OS/2'].yStrikeoutPosition, static['OS/2'].yStrikeoutSize]})
    result['chars'] = ''.join(chr(c) for c in UNICODES)
    result['glyphs'] = [order.index(cmap[c]) for c in UNICODES]
    for loc in ({'wght': 300.0}, {'wght': 250.0}, {'wght': 777.0}):  # the outlines at a named instance and between them
        glyphSet = font.getGlyphSet(location=loc, normalized=False)
        glyphs = []
        for ch in OUTLINES:
            pen = CubicPen(glyphSet)
            glyphSet[cmap[ord(ch)]].draw(pen)
            glyphs.append({'char': ch, 'commands': [[op, [r(v) for v in args]] for op, args in pen.value]})
        result['locations'].append({'coordinates': [loc[a.axisTag] for a in axes], 'glyphs': glyphs})
    return result


def write_reference():
    data = [reference(f) for f in ('VarTestSans.ttf', 'VarTestSans-NoHVAR.ttf', 'VarTestSans.otf')]
    with open('VarTestSans.reference.json', 'w', encoding='utf-8') as f:
        json.dump(data, f, ensure_ascii=False, separators=(',', ':'))


def main(directory):
    make(os.path.join(directory, 'SourceSans3VF-Upright.ttf'), 'VarTestSans.ttf', 'VarTestSans')
    make(os.path.join(directory, 'SourceSans3VF-Upright.ttf'), 'VarTestSans-NoHVAR.ttf', 'VarTestSansNoHVAR', drop_hvar=True)
    make(os.path.join(directory, 'SourceSans3VF-Upright.otf'), 'VarTestSans.otf', 'VarTestSansCFF2')
    write_reference()


if __name__ == '__main__':
    main(sys.argv[1])
