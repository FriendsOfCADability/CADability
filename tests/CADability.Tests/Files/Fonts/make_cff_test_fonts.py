# Writes the CFF test fonts of TrueTypeFontTests and their reference data with fontTools (pip install fonttools):
#
#   InterCffTest.otf        name-keyed CFF font
#   InterCidTest.otf        the same glyphs as a CID-keyed CFF font with two font dicts (FDArray, FDSelect)
#   InterCffTest.reference.json   the outlines as fontTools draws them, the same for both fonts
#
# Both are made from a subset of Inter Regular (SIL Open Font License 1.1, https://rsms.me/inter), which uses only the
# general charstring operators and no subroutines. To test the whole Type 2 charstring interpreter, the charstrings are
# rewritten without changing the outlines:
#   - the specialized operators (hvcurveto, vhcurveto, hhcurveto, vvcurveto, hlineto, vlineto, rcurveline, rlinecurve)
#     by fontTools' specializer,
#   - flex for two consecutive curves of some glyphs,
#   - the body of a third of the glyphs moved into a local subroutine, of another third into a global one.
#
# Run from this directory with the path of Inter-Regular.otf: python make_cff_test_fonts.py /usr/share/fonts/opentype/inter/Inter-Regular.otf
import copy, json, sys
from fontTools.ttLib import TTFont
from fontTools.subset import Subsetter, Options
from fontTools.cffLib import specializer, SubrsIndex, FDArrayIndex, FontDict, FDSelect
from fontTools.misc.psCharStrings import T2CharString
from fontTools.pens.recordingPen import RecordingPen

UNICODES = list(range(0x20, 0x7F)) + [0xC4, 0xD6, 0xDC, 0xE4, 0xF6, 0xFC, 0xDF, 0x20AC, 0xE9]


def bias(count):
    return 107 if count < 1240 else 1131 if count < 33900 else 32768


def subset(path):
    options = Options()
    options.layout_features = []
    font = TTFont(path)
    subsetter = Subsetter(options)
    subsetter.populate(unicodes=UNICODES)
    subsetter.subset(font)
    for tag in ('GDEF', 'GPOS', 'GSUB'):  # they name glyphs, which the CID font renames
        if tag in font:
            del font[tag]
    return font


def rewrite(program, index):
    """The program with specialized operators and, for some glyphs, flex instead of two curves."""
    commands = specializer.programToCommands(program)
    if index % 4 == 0:
        for i in range(len(commands) - 1):
            a, b = commands[i], commands[i + 1]
            if a[0] == 'rrcurveto' and b[0] == 'rrcurveto' and len(a[1]) == 6 and len(b[1]) == 6:
                commands[i:i + 2] = [('flex', a[1] + b[1] + [50])]
                break
    return specializer.specializeProgram(specializer.commandsToProgram(commands))


def body(program):
    assert program[-1] == 'endchar'
    return program[:-1]


def draw_all(font):
    glyphs = font.getGlyphSet()
    cmap = font.getBestCmap()
    result = []
    for code in UNICODES:
        name = cmap[code]
        pen = RecordingPen()
        glyphs[name].draw(pen)
        result.append({'char': chr(code), 'glyph': font.getGlyphID(name),
                       'commands': [[op, [c for p in pts for c in p]] for op, pts in pen.value]})
    return result


def name_keyed(font):
    cff = font['CFF '].cff
    top = cff.topDictIndex[0]
    private = top.Private
    private.Subrs = SubrsIndex()
    order = font.getGlyphOrder()
    reference = draw_all(font)  # before rewriting: the outlines must not change
    local, glob = [], []
    for index, name in enumerate(order):
        cs = top.CharStrings[name]
        cs.decompile()
        program = rewrite(cs.program, index)
        if index % 3 == 1:
            local.append(body(program) + ['return'])
            program = [len(local) - 1, 'callsubr', 'endchar']  # bias is added below
        elif index % 3 == 2:
            glob.append(body(program) + ['return'])
            program = [len(glob) - 1, 'callgsubr', 'endchar']
        cs.program = program
    for p in local:
        private.Subrs.append(T2CharString(program=p, private=private, globalSubrs=cff.GlobalSubrs))
    for p in glob:
        cff.GlobalSubrs.append(T2CharString(program=p, private=private, globalSubrs=cff.GlobalSubrs))
    for name in order:  # the subroutine numbers are stored without the bias
        cs = top.CharStrings[name]
        if len(cs.program) == 3 and cs.program[1] in ('callsubr', 'callgsubr'):
            count = len(local) if cs.program[1] == 'callsubr' else len(glob)
            cs.program[0] -= bias(count)
    return reference


def to_cid(font):
    """Converts the name-keyed font to a CID-keyed one with two font dicts: even glyphs use the first, odd glyphs the second,
    each with its own private dict and local subroutines."""
    cmap_tables = font['cmap'].tables  # read the tables that name glyphs before the glyphs are renamed
    metrics = dict(font['hmtx'].metrics)
    cff = font['CFF '].cff
    top = cff.topDictIndex[0]
    order = font.getGlyphOrder()
    new_names = ['.notdef'] + ['cid%05d' % i for i in range(1, len(order))]
    rename = dict(zip(order, new_names))
    programs = []
    for name in order:
        cs = top.CharStrings[name]
        cs.decompile()
        programs.append(copy.copy(cs.program))
    old_private = top.Private
    old_local = [s.program if s.program is not None else (s.decompile() or s.program) for s in old_private.Subrs]
    fd_array = FDArrayIndex()
    privates = []
    for k in range(2):
        fd = FontDict()
        private = copy.copy(old_private)
        private.Subrs = SubrsIndex()
        fd.Private = private
        fd.FontName = 'InterCidTest-%d' % k
        fd_array.append(fd)
        privates.append(private)
    # local subroutines move to the font dict of the glyph that calls them
    local_bias = bias(len(old_local))
    for gid, program in enumerate(programs):
        fd = gid % 2
        if len(program) == 3 and program[1] == 'callsubr':
            subr = old_local[program[0] + local_bias]
            privates[fd].Subrs.append(T2CharString(program=list(subr), private=privates[fd], globalSubrs=cff.GlobalSubrs))
            program[0] = len(privates[fd].Subrs) - 1  # bias added below
    for gid, program in enumerate(programs):
        fd = gid % 2
        if len(program) == 3 and program[1] == 'callsubr':
            program[0] -= bias(len(privates[fd].Subrs))
    top.ROS = ('Adobe', 'Identity', 0)
    top.CIDCount = len(order)
    top.FDArray = fd_array
    select = FDSelect()
    select.format = 3
    select.gidArray = [gid % 2 for gid in range(len(order))]
    top.FDSelect = select
    del top.rawDict['Private']
    del top.Private
    charstrings = top.CharStrings
    new_dict = {}
    for gid, name in enumerate(order):
        cs = T2CharString(program=programs[gid], private=privates[gid % 2], globalSubrs=cff.GlobalSubrs)
        cs.fdSelectIndex = gid % 2
        new_dict[rename[name]] = cs
    charstrings.charStrings = new_dict
    charstrings.charStringsAreIndexed = False
    charstrings.charStringsIndex = None
    top.charset = new_names
    for table in cmap_tables:
        table.cmap = {code: rename[name] for code, name in table.cmap.items()}
    font['hmtx'].metrics = {rename[name]: value for name, value in metrics.items()}
    font.setGlyphOrder(new_names)
    font['post'].formatType = 3.0


def main(path):
    font = subset(path)
    reference = name_keyed(font)
    font.save('InterCffTest.otf')
    check = TTFont('InterCffTest.otf')
    assert draw_all(check) == reference, 'rewriting the charstrings changed the outlines'
    json.dump({'glyphs': reference}, open('InterCffTest.reference.json', 'w', encoding='utf-8'), ensure_ascii=False, separators=(',', ':'))

    font = TTFont('InterCffTest.otf')
    to_cid(font)
    font.save('InterCidTest.otf')
    check = TTFont('InterCidTest.otf')
    top = check['CFF '].cff.topDictIndex[0]
    assert hasattr(top, 'ROS') and len(top.FDArray) == 2, 'not a CID-keyed font with two font dicts'
    assert [g['commands'] for g in draw_all(check)] == [g['commands'] for g in reference], 'the CID font has other outlines'


if __name__ == '__main__':
    main(sys.argv[1])
