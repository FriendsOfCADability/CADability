using System;
using System.Collections.Generic;
using System.Runtime.Serialization;

namespace CADability
{
    /// <summary>
    /// The page settings of a <see cref="Project"/> or a <see cref="Layout"/> for printing: the paper, its orientation, the
    /// margins and the printer. This is what project files contain instead of the GDI+ <c>PageSettings</c>, so that they
    /// can be written and read on every platform (issue #412). All lengths are in millimeters.
    /// </summary>
    [Serializable]
    public sealed class PageSetup : ISerializable
    {
        /// <summary>
        /// The name of the paper, e.g. "A4" or "Letter"; null if the printer's default paper is used.
        /// </summary>
        public string PaperName { get; set; }
        /// <summary>
        /// The paper kind of the printing system (the DMPAPER number of Windows, e.g. 9 for A4), 0 for a custom paper or
        /// if it is not known. It only helps to find the same paper on a printer again.
        /// </summary>
        public int PaperKind { get; set; }
        /// <summary>
        /// The width of the paper in portrait orientation in mm; 0 if the printer's default paper is used.
        /// </summary>
        public double PaperWidth { get; set; }
        /// <summary>
        /// The height of the paper in portrait orientation in mm; 0 if the printer's default paper is used.
        /// </summary>
        public double PaperHeight { get; set; }
        /// <summary>
        /// True if the page is printed in landscape orientation.
        /// </summary>
        public bool Landscape { get; set; }
        /// <summary>
        /// The margins in mm.
        /// </summary>
        public double MarginLeft { get; set; }
        /// <summary>The right margin in mm.</summary>
        public double MarginRight { get; set; }
        /// <summary>The top margin in mm.</summary>
        public double MarginTop { get; set; }
        /// <summary>The bottom margin in mm.</summary>
        public double MarginBottom { get; set; }
        /// <summary>
        /// The name of the printer; null for the default printer.
        /// </summary>
        public string PrinterName { get; set; }

        /// <summary>
        /// Creates page settings for the default paper of the default printer, in portrait orientation, without margins.
        /// </summary>
        public PageSetup()
        {
        }
        /// <summary>
        /// Returns a copy of these page settings.
        /// </summary>
        public PageSetup Clone()
        {
            return (PageSetup)MemberwiseClone();
        }

        // GDI+ measures paper and margins in hundredths of an inch
        private const double HundredthInch = 0.254;

        #region ISerializable
        private PageSetup(SerializationInfo info, StreamingContext context)
        {
            foreach (SerializationEntry entry in info)
            {
                switch (entry.Name)
                {
                    case "PaperName": PaperName = entry.Value as string; break;
                    case "PaperKind": PaperKind = Convert.ToInt32(entry.Value); break;
                    case "PaperWidth": PaperWidth = Convert.ToDouble(entry.Value); break;
                    case "PaperHeight": PaperHeight = Convert.ToDouble(entry.Value); break;
                    case "Landscape": Landscape = Convert.ToBoolean(entry.Value); break;
                    case "MarginLeft": MarginLeft = Convert.ToDouble(entry.Value); break;
                    case "MarginRight": MarginRight = Convert.ToDouble(entry.Value); break;
                    case "MarginTop": MarginTop = Convert.ToDouble(entry.Value); break;
                    case "MarginBottom": MarginBottom = Convert.ToDouble(entry.Value); break;
                    case "PrinterName": PrinterName = entry.Value as string; break;
                }
            }
        }
        void ISerializable.GetObjectData(SerializationInfo info, StreamingContext context)
        {
            info.AddValue("PaperName", PaperName);
            info.AddValue("PaperKind", PaperKind);
            info.AddValue("PaperWidth", PaperWidth);
            info.AddValue("PaperHeight", PaperHeight);
            info.AddValue("Landscape", Landscape);
            info.AddValue("MarginLeft", MarginLeft);
            info.AddValue("MarginRight", MarginRight);
            info.AddValue("MarginTop", MarginTop);
            info.AddValue("MarginBottom", MarginBottom);
            info.AddValue("PrinterName", PrinterName);
        }
        #endregion

        /// <summary>
        /// The page settings of a project file of CADability 1.x, which contains a serialized GDI+ <c>PageSettings</c>: either
        /// the <see cref="LegacyPrintingData"/> it is read as, or a <c>PageSettings</c> object itself. Null if there are none
        /// or they cannot be understood.
        /// </summary>
        internal static PageSetup FromSerialized(object value)
        {
            try
            {
                if (value is LegacyPrintingData legacy) return FromLegacy(legacy);
                if (value is System.Drawing.Printing.PageSettings pageSettings) return FromPageSettings(pageSettings);
            }
            catch (Exception e) when (!(e is OutOfMemoryException) && !(e is System.Threading.ThreadAbortException))
            {
                System.Diagnostics.Trace.TraceWarning("The page settings of the project file cannot be read: " + e.Message);
            }
            return null;
        }
        /// <summary>
        /// Reads the fields of a serialized <c>PageSettings</c>. The JSON format of 1.x has the public properties (e.g.
        /// "Landscape", "PaperSize" with "Width", "PaperName" and "RawKind"), the binary format of .NET Framework the private
        /// fields (e.g. "landscape" as a TriState, "paperSize" with "width", "name" and "kind").
        /// </summary>
        private static PageSetup FromLegacy(LegacyPrintingData data)
        {
            PageSetup res = new PageSetup();
            res.Landscape = AsBool(data.Get("Landscape", "landscape"));
            if (data.Get("PaperSize", "paperSize") is LegacyPrintingData paper)
            {
                res.PaperName = paper.Get("PaperName", "name") as string;
                res.PaperKind = AsInt(paper.Get("RawKind", "kind"));
                res.PaperWidth = AsInt(paper.Get("Width", "width")) * HundredthInch;
                res.PaperHeight = AsInt(paper.Get("Height", "height")) * HundredthInch;
            }
            if (data.Get("Margins", "margins") is LegacyPrintingData margins)
            {
                res.MarginLeft = AsInt(margins.Get("Left", "left")) * HundredthInch;
                res.MarginRight = AsInt(margins.Get("Right", "right")) * HundredthInch;
                res.MarginTop = AsInt(margins.Get("Top", "top")) * HundredthInch;
                res.MarginBottom = AsInt(margins.Get("Bottom", "bottom")) * HundredthInch;
            }
            if (data.Get("PrinterSettings", "printerSettings") is LegacyPrintingData printer)
            {
                string name = printer.Get("PrinterName", "printerName") as string;
                res.PrinterName = string.IsNullOrEmpty(name) ? null : name; // empty: the default printer
            }
            return res;
        }
        private static bool AsBool(object value)
        {
            if (value is bool b) return b;
            // the internal TriState of System.Drawing.Printing: 0 is the printer's default, 1 false, 2 true
            if (value is LegacyPrintingData triState) return AsInt(triState.Get("value")) == 2;
            return false;
        }
        private static int AsInt(object value)
        {
            if (value is LegacyPrintingData enumValue) value = enumValue.Get("value__"); // an enum, e.g. PaperKind
            if (value == null) return 0;
            try
            {
                return Convert.ToInt32(value);
            }
            catch (Exception e) when (e is FormatException || e is InvalidCastException || e is OverflowException)
            {
                return 0;
            }
        }

        /// <summary>
        /// The page settings of a GDI+ <c>PageSettings</c> (Windows only).
        /// </summary>
        internal static PageSetup FromPageSettings(System.Drawing.Printing.PageSettings pageSettings)
        {
            if (pageSettings == null) return null;
            PageSetup res = new PageSetup();
            res.Landscape = pageSettings.Landscape;
            System.Drawing.Printing.PaperSize paper = pageSettings.PaperSize;
            if (paper != null)
            {
                res.PaperName = paper.PaperName;
                res.PaperKind = paper.RawKind;
                res.PaperWidth = paper.Width * HundredthInch;
                res.PaperHeight = paper.Height * HundredthInch;
            }
            System.Drawing.Printing.Margins margins = pageSettings.Margins;
            if (margins != null)
            {
                res.MarginLeft = margins.Left * HundredthInch;
                res.MarginRight = margins.Right * HundredthInch;
                res.MarginTop = margins.Top * HundredthInch;
                res.MarginBottom = margins.Bottom * HundredthInch;
            }
            string printer = pageSettings.PrinterSettings?.PrinterName;
            res.PrinterName = string.IsNullOrEmpty(printer) ? null : printer;
            return res;
        }

        /// <summary>
        /// Applies these page settings to a GDI+ print document (Windows only): the printer, if it is installed, the paper
        /// of that printer with the same kind, name or size (or a custom paper of this size), the orientation and the margins.
        /// </summary>
        internal void ApplyTo(System.Drawing.Printing.PrintDocument printDocument)
        {
            System.Drawing.Printing.PrinterSettings printer = printDocument.PrinterSettings;
            if (!string.IsNullOrEmpty(PrinterName))
            {
                foreach (string installed in System.Drawing.Printing.PrinterSettings.InstalledPrinters)
                {
                    if (installed == PrinterName)
                    {
                        printer.PrinterName = PrinterName;
                        break;
                    }
                }
            }
            System.Drawing.Printing.PageSettings pageSettings = printDocument.DefaultPageSettings;
            if (PaperWidth > 0 && PaperHeight > 0)
            {
                int width = (int)Math.Round(PaperWidth / HundredthInch), height = (int)Math.Round(PaperHeight / HundredthInch);
                System.Drawing.Printing.PaperSize found = null;
                foreach (System.Drawing.Printing.PaperSize size in printer.PaperSizes)
                {
                    bool sameSize = Math.Abs(size.Width - width) <= 1 && Math.Abs(size.Height - height) <= 1;
                    if (PaperKind != 0 && size.RawKind == PaperKind && sameSize) { found = size; break; }
                    if (found == null && sameSize && (PaperName == null || size.PaperName == PaperName)) found = size;
                }
                pageSettings.PaperSize = found ?? new System.Drawing.Printing.PaperSize(PaperName ?? "Custom", width, height);
            }
            pageSettings.Landscape = Landscape;
            pageSettings.Margins = new System.Drawing.Printing.Margins((int)Math.Round(MarginLeft / HundredthInch), (int)Math.Round(MarginRight / HundredthInch),
                (int)Math.Round(MarginTop / HundredthInch), (int)Math.Round(MarginBottom / HundredthInch));
        }
    }

    /// <summary>
    /// Stands for an object of <c>System.Drawing.Printing</c> (<c>PageSettings</c>, <c>PaperSize</c>, <c>Margins</c>,
    /// <c>PrinterSettings</c> and their enums) in a project file written by CADability 1.x. It only collects the serialized
    /// values, so that the file opens without GDI+ printing, and <see cref="PageSetup"/> takes the page settings from it
    /// (issue #412).
    /// </summary>
    [Serializable]
    internal sealed class LegacyPrintingData : ISerializable
    {
        private readonly Dictionary<string, object> values = new Dictionary<string, object>();

        private LegacyPrintingData(SerializationInfo info, StreamingContext context)
        {
            foreach (SerializationEntry entry in info) values[entry.Name] = entry.Value;
        }
        void ISerializable.GetObjectData(SerializationInfo info, StreamingContext context)
        {
            foreach (KeyValuePair<string, object> value in values) info.AddValue(value.Key, value.Value);
        }
        /// <summary>
        /// The first of the values with these names, null if there is none.
        /// </summary>
        public object Get(params string[] names)
        {
            foreach (string name in names) if (values.TryGetValue(name, out object value)) return value;
            return null;
        }
        /// <summary>
        /// True for the types of <c>System.Drawing.Printing</c>, which CADability 1.x wrote into project files with the page
        /// settings.
        /// </summary>
        public static bool IsPrintingType(string typeName)
        {
            return typeName != null && typeName.StartsWith("System.Drawing.Printing.");
        }
    }
}
