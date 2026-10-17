using CADability.GeoObject;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Threading;
using Path = System.IO.Path;

namespace CADability
{
    /// <summary>
    /// Provides the fonts for <see cref="Text"/> and dimensions: the names of the font families, their metrics and the outlines
    /// of the glyphs. The kernel gets all fonts from <see cref="Current"/>, which works without any user interface. The default
    /// is a <see cref="ManagedFontProvider"/>, which reads the TrueType font files of the system itself and therefore works on
    /// every platform. CADability.Forms contains a provider based on GDI (GdiFontProvider) for applications that want the
    /// fonts exactly as Windows renders them.
    /// </summary>
    public abstract class FontProvider
    {
        private static FontProvider current;
        /// <summary>
        /// The font provider the kernel uses. Setting it clears the cached glyphs, so that texts are rebuilt with the new
        /// provider, like <see cref="Text.ClearFontCache"/>: set it before texts are painted, or rebuild the display lists of
        /// the views afterwards (<see cref="ProjectedModel.InvalidateDisplayLists"/>).
        /// </summary>
        public static FontProvider Current
        {
            get => LazyInitializer.EnsureInitialized(ref current, () => new ManagedFontProvider());
            set
            {
                current = value ?? throw new ArgumentNullException(nameof(value));
                FontCache.GlobalFontCache.Clear();
                Text.ResetFontFamilyNames();
            }
        }
        /// <summary>
        /// The names of all font families this provider knows, e.g. for a list to choose from.
        /// </summary>
        public abstract string[] GetFamilyNames();
        /// <summary>
        /// Returns the face of the font family <paramref name="familyName"/> (not case sensitive) that matches the requested
        /// style best, or null if the provider does not know this family.
        /// </summary>
        /// <param name="familyName">The name of the font family, e.g. "Arial"</param>
        /// <param name="bold">Bold requested</param>
        /// <param name="italic">Italic requested</param>
        public abstract FontFace GetFace(string familyName, bool bold, bool italic);
        /// <summary>
        /// Returns the face to use for a family that <see cref="GetFace"/> does not know. The default implementation tries some
        /// common sans serif fonts and then takes the first family. Returns null when there is no font at all.
        /// </summary>
        public virtual FontFace GetDefaultFace(bool bold, bool italic)
        {
            foreach (string name in new[] { "Microsoft Sans Serif", "Arial", "Liberation Sans", "DejaVu Sans", "Helvetica", "Noto Sans", "FreeSans" })
            {
                FontFace face = GetFace(name, bold, italic);
                if (face != null) return face;
            }
            string first = GetFamilyNames().OrderBy(n => n, StringComparer.OrdinalIgnoreCase).FirstOrDefault();
            return first == null ? null : GetFace(first, bold, italic);
        }
    }

    /// <summary>
    /// A face of a font family (e.g. "Arial Bold") as a <see cref="FontProvider"/> provides it. Metrics are in font units,
    /// widths and kerning in units of the em height, and the glyph outlines are Bezier curves, so that all providers produce
    /// the same exact geometry.
    /// </summary>
    public abstract class FontFace
    {
        /// <summary>
        /// The name of the font family.
        /// </summary>
        public abstract string FamilyName { get; }
        /// <summary>
        /// True if this face is bold.
        /// </summary>
        public abstract bool IsBold { get; }
        /// <summary>
        /// True if this face is italic.
        /// </summary>
        public abstract bool IsItalic { get; }
        /// <summary>
        /// The height of the em square in font units (e.g. 2048 for most TrueType fonts).
        /// </summary>
        public abstract int EmHeight { get; }
        /// <summary>
        /// The ascent of the character cell in font units.
        /// </summary>
        public abstract int CellAscent { get; }
        /// <summary>
        /// The descent of the character cell in font units, positive downwards.
        /// </summary>
        public abstract int CellDescent { get; }
        /// <summary>
        /// The distance between the baselines of two lines in font units.
        /// </summary>
        public abstract int LineSpacing { get; }
        /// <summary>
        /// The advance width of the character <paramref name="c"/> in units of the em height.
        /// </summary>
        public abstract double GetAdvanceWidth(char c);
        /// <summary>
        /// The kerning between two characters in units of the em height, usually negative; 0 if there is none.
        /// </summary>
        public abstract double GetKerning(char left, char right);
        /// <summary>
        /// Returns the outline of the character <paramref name="c"/> in the layout of a GDI+ GraphicsPath after
        /// <c>AddString</c> with an em size of 1 and <c>StringFormat.GenericTypographic</c>: in units of the em height, y
        /// downwards, the top of the character cell at 0 and the baseline at <see cref="CellAscent"/>/<see cref="EmHeight"/>.
        /// The point types are those of GDI+ (0 start, 1 line, 3 cubic Bezier, 0x80 closes the figure). Curves must be returned
        /// as Bezier segments and not be flattened. Underline and strikeout are added as rectangles when requested.
        /// </summary>
        public abstract void GetGlyphPath(char c, bool underline, bool strikeout, out PointF[] points, out byte[] types);
    }

    /// <summary>
    /// The default <see cref="FontProvider"/>: reads TrueType and OpenType font files (.ttf, .ttc, .otf, .otc) itself, so it
    /// needs neither GDI nor any other part of the operating system and works on Windows, Linux, macOS and in the browser. By
    /// default it uses the font directories of the system; more files or directories can be added. Bitmap fonts and variable
    /// OpenType fonts with CFF2 outlines are left out. A bold or italic face that a family does not have is not synthesized:
    /// the closest face is used.
    /// </summary>
    public class ManagedFontProvider : FontProvider
    {
        private sealed class Entry
        {
            public string FileName; // or
            public byte[] Data;
            public int Index; // in a collection
            public string Family;
            public bool Bold, Italic;
            public ManagedFontFace Face; // loaded on demand
        }
        private readonly object locker = new object();
        private readonly List<string> directories = new List<string>();
        private readonly List<Entry> added = new List<Entry>();
        private Dictionary<string, List<Entry>> families; // upper case family name -> faces, built on demand

        /// <summary>
        /// Creates a provider for the fonts installed on the system.
        /// </summary>
        public ManagedFontProvider() : this(null, true)
        {
        }
        /// <summary>
        /// Creates a provider for the fonts in <paramref name="fontDirectories"/> (with their subdirectories) and, if
        /// <paramref name="includeSystemFonts"/> is true, for the fonts installed on the system.
        /// </summary>
        public ManagedFontProvider(IEnumerable<string> fontDirectories, bool includeSystemFonts)
        {
            if (fontDirectories != null) directories.AddRange(fontDirectories);
            if (includeSystemFonts) directories.AddRange(SystemFontDirectories());
        }
        /// <summary>
        /// The directories where the fonts of the system are installed on the current platform.
        /// </summary>
        public static IEnumerable<string> SystemFontDirectories()
        {
            string home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
            string fonts = Environment.GetFolderPath(Environment.SpecialFolder.Fonts); // Windows
            if (!string.IsNullOrEmpty(fonts)) yield return fonts;
            string localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
            if (!string.IsNullOrEmpty(localAppData)) yield return Path.Combine(localAppData, "Microsoft", "Windows", "Fonts"); // fonts installed per user
            yield return "/usr/share/fonts";
            yield return "/usr/local/share/fonts";
            yield return "/System/Library/Fonts";
            yield return "/Library/Fonts";
            if (!string.IsNullOrEmpty(home))
            {
                yield return Path.Combine(home, ".fonts");
                yield return Path.Combine(home, ".local", "share", "fonts");
                yield return Path.Combine(home, "Library", "Fonts");
            }
        }
        /// <summary>
        /// Adds the fonts of a font file (.ttf, .ttc, .otf or .otc).
        /// </summary>
        public void AddFontFile(string fileName)
        {
            TrueTypeFont[] fonts = TrueTypeFont.ReadHeaders(fileName);
            lock (locker)
            {
                for (int i = 0; i < fonts.Length; ++i) AddEntry(new Entry { FileName = fileName, Index = i }, fonts[i], added);
                families = null;
            }
        }
        /// <summary>
        /// Adds the fonts of a font file given as its content, e.g. an embedded resource.
        /// </summary>
        public void AddFontData(byte[] data)
        {
            TrueTypeFont[] fonts = TrueTypeFont.Load(data);
            lock (locker)
            {
                for (int i = 0; i < fonts.Length; ++i) AddEntry(new Entry { Data = data, Index = i }, fonts[i], added);
                families = null;
            }
        }
        /// <inheritdoc/>
        public override string[] GetFamilyNames()
        {
            lock (locker)
            {
                return Families().Values.Select(l => l[0].Family).ToArray();
            }
        }
        /// <inheritdoc/>
        public override FontFace GetFace(string familyName, bool bold, bool italic)
        {
            if (string.IsNullOrEmpty(familyName)) return null;
            Entry best;
            lock (locker)
            {
                if (!Families().TryGetValue(familyName.ToUpperInvariant(), out List<Entry> faces)) return null;
                // the exact style, then the same weight, then regular, then any face
                best = faces.FirstOrDefault(f => f.Bold == bold && f.Italic == italic)
                    ?? faces.FirstOrDefault(f => f.Bold == bold)
                    ?? faces.FirstOrDefault(f => !f.Bold && !f.Italic)
                    ?? faces[0];
                if (best.Face == null)
                {
                    TrueTypeFont[] fonts = best.Data != null ? TrueTypeFont.Load(best.Data) : TrueTypeFont.Load(best.FileName);
                    best.Face = new ManagedFontFace(fonts[best.Index]);
                }
            }
            return best.Face;
        }

        private static void AddEntry(Entry entry, TrueTypeFont font, List<Entry> list)
        {
            if (!font.HasOutlines || string.IsNullOrEmpty(font.FamilyName)) return; // bitmap fonts and CFF2 cannot be used
            entry.Family = font.FamilyName;
            entry.Bold = font.IsBold;
            entry.Italic = font.IsItalic;
            list.Add(entry);
        }
        private Dictionary<string, List<Entry>> Families()
        {
            if (families != null) return families;
            List<Entry> all = new List<Entry>();
            HashSet<string> seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (string directory in directories)
            {
                if (!Directory.Exists(directory)) continue;
                IEnumerable<string> files;
                try
                {
                    files = Directory.EnumerateFiles(directory, "*", SearchOption.AllDirectories).ToArray();
                }
                catch (Exception e) when (e is IOException || e is UnauthorizedAccessException)
                {
                    continue;
                }
                foreach (string file in files)
                {
                    string ext = Path.GetExtension(file).ToLowerInvariant();
                    if (ext != ".ttf" && ext != ".ttc" && ext != ".otf" && ext != ".otc") continue;
                    if (!seen.Add(Path.GetFullPath(file))) continue;
                    try
                    {
                        TrueTypeFont[] fonts = TrueTypeFont.ReadHeaders(file);
                        for (int i = 0; i < fonts.Length; ++i) AddEntry(new Entry { FileName = file, Index = i }, fonts[i], all);
                    }
                    catch (Exception e) when (e is IOException || e is UnauthorizedAccessException || e is InvalidDataException || e is IndexOutOfRangeException || e is ArgumentException)
                    {   // a broken or unreadable font file is left out
                    }
                }
            }
            all.AddRange(added);
            families = new Dictionary<string, List<Entry>>();
            foreach (Entry e in all)
            {
                string key = e.Family.ToUpperInvariant();
                if (!families.TryGetValue(key, out List<Entry> list)) families[key] = list = new List<Entry>();
                if (!list.Any(f => f.Bold == e.Bold && f.Italic == e.Italic)) list.Add(e); // the first file of a style wins
            }
            return families;
        }
    }

    /// <summary>
    /// A face read by the <see cref="ManagedFontProvider"/>.
    /// </summary>
    internal sealed class ManagedFontFace : FontFace
    {
        private readonly TrueTypeFont font;
        private readonly object locker = new object();
        public ManagedFontFace(TrueTypeFont font)
        {
            this.font = font;
        }
        internal TrueTypeFont Font => font;
        public override string FamilyName => font.FamilyName;
        public override bool IsBold => font.IsBold;
        public override bool IsItalic => font.IsItalic;
        public override int EmHeight => font.UnitsPerEm;
        public override int CellAscent => font.CellAscent;
        public override int CellDescent => font.CellDescent;
        public override int LineSpacing => font.LineSpacing;
        public override double GetAdvanceWidth(char c)
        {
            return font.GetAdvanceWidth(font.GetGlyphIndex(c)) / (double)font.UnitsPerEm;
        }
        public override double GetKerning(char left, char right)
        {
            lock (locker)
            {   // the kerning pairs are read on the first call
                return font.GetKerning(font.GetGlyphIndex(left), font.GetGlyphIndex(right)) / (double)font.UnitsPerEm;
            }
        }
        public override void GetGlyphPath(char c, bool underline, bool strikeout, out PointF[] points, out byte[] types)
        {
            font.GetPathData(font.GetGlyphIndex(c), underline, strikeout, out points, out types);
        }
    }
}
