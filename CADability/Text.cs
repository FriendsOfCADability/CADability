using CADability.Actions;
using CADability.Attribute;
using CADability.Curve2D;
using CADability.Shapes;
using CADability.UserInterface;
using System;
using System.Collections.Generic;
// some work to do to implement text for webassembly
//#if WEBASSEMBLY
//using CADability.WebDrawing;
//using Point = CADability.WebDrawing.Point;
//#else
//using System.Drawing;
//using Point = System.Drawing.Point;
//#endif
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Runtime.Serialization;
using System.Threading;

using Wintellect.PowerCollections;

namespace CADability.GeoObject
{

    public class ShowPropertyFont : PropertyEntryImpl
    {
        private readonly Text text;
        private IPropertyEntry[] showProperties;
        public ShowPropertyFont(Text t)
        {
            text = t;
            resourceIdInternal = "Text.Font.Format";
        }

        private MultipleChoiceProperty InitFontList()
        {
            FontFamily[] families = FontFamily.Families;
            string[] choices = new string[families.Length];
            for (int i = 0; i < families.Length; i++)
            {
                choices[i] = families[i].Name;
                int emS = families[i].GetEmHeight(FontStyle.Regular);
                int asc = families[i].GetCellAscent(FontStyle.Regular);
                int desc = families[i].GetCellDescent(FontStyle.Regular);
            }
            MultipleChoiceProperty res = new MultipleChoiceProperty("Text.Font", choices, text.Font);
            res.ValueChangedEvent += FontChanged;
            return res;
        }
        #region PropertyEntryImpl Overrides
        public override IPropertyEntry[] SubItems
        {
            get
            {
                if (showProperties == null)
                {
                    showProperties = new IPropertyEntry[5];
                    showProperties[0] = InitFontList();
                    showProperties[1] = new BooleanProperty(text, "Bold", "Text.Bold", "Text.Bold.Values");
                    showProperties[2] = new BooleanProperty(text, "Italic", "Text.Italic", "Text.Italic.Values");
                    showProperties[3] = new BooleanProperty(text, "Underline", "Text.Underline", "Text.Underline.Values");
                    showProperties[4] = new BooleanProperty(text, "Strikeout", "Text.Strikeout", "Text.Strikeout.Values");
                }
                return showProperties;
            }
        }
        /// <summary>
        /// Overrides <see cref="IShowPropertyImpl.EntryType"/>, 
        /// returns <see cref="ShowPropertyEntryType.GroupTitle"/>.
        /// </summary>
        public override PropertyEntryType Flags => PropertyEntryType.GroupTitle | PropertyEntryType.HasSubEntries;

        #endregion
        private void FontChanged(object sender, object newValue)
        {
            text.Font = newValue as string;
        }
    }


    public class ShowPropertyText : PropertyEntryImpl, IDisplayHotSpots, ICommandHandler, IGeoObjectShowProperty
    {
        private readonly Text text;
        private IPropertyEntry[] showProperties;
        private readonly GeoPointProperty locationProperty;
        private readonly GeoVectorProperty glyphDirectionProperty;
        private readonly GeoVectorHotSpot glyphDirHotspot;
        private readonly AngleProperty glyphAngleProp;
        private readonly StringProperty stringProperty;
        private readonly GeoVectorProperty lineDirectionProperty;
        private readonly GeoVectorHotSpot lineDirHotspot; // Hotspot für Richtung
        private readonly LengthProperty sizeProp;
        private readonly LengthHotSpot sizeHotspot;
        public ShowPropertyText(Text t, IFrame frame) : base(frame)
        {
            text = t;
            resourceIdInternal = "Text";

            locationProperty = new GeoPointProperty(text, "Location", "Text.Location", frame, true);

            glyphDirectionProperty = new GeoVectorProperty(text, "GlyphDirectionKeepSize", "Text.GlyphDirection", frame, true);
            glyphDirHotspot = new GeoVectorHotSpot(glyphDirectionProperty);
            GeoVector vg = text.GlyphDirection;
            GeoPoint gp = text.Location;
            glyphDirHotspot.Position = new GeoPoint(gp, vg.x, vg.y, vg.z);

            lineDirectionProperty = new GeoVectorProperty(frame, "Text.LineDirection");
            lineDirectionProperty.IsNormedVector = true;
            lineDirHotspot = new GeoVectorHotSpot(lineDirectionProperty);
            GeoVector vl = text.LineDirection;
            vl.Length = text.Length;
            lineDirHotspot.Position = new GeoPoint(gp, vl.x, vl.y, vl.z);

            sizeProp = new LengthProperty(text, "TextSize", "Text.TextSize", frame, true);
            sizeHotspot = new LengthHotSpot(sizeProp);
            vl.Length = text.Length / 2.0;
            sizeHotspot.Position = new GeoPoint(gp, vg.x + vl.x, vg.y + vl.y, vg.z + vl.z);

            glyphAngleProp = new AngleProperty(text, "GlyphAngle", "Text.GlyphAngle", frame, true);
            glyphAngleProp.PreferNegativeValues = true;
            stringProperty = new StringProperty(text.TextString, "Text.TextString");
            stringProperty.OnGetValue = () => text.TextString;
            stringProperty.OnSetValue = (string val) => text.TextString = val;
        }

        internal string[] GetChoicesFromResource(string resId)
        {
            string[] res = null;
            string valuesStr = StringTable.GetString(resId + ".Values");
            char sep = valuesStr[0];
            string[] split = valuesStr.Split(new char[] { sep });
            if (split.Length > 1)
            {
                //the first one is empty
                res = new string[split.Length - 1];
                for (int i = 1; i < split.Length; i++)
                    res[i - 1] = split[i];
            }
            return res;
        }
        internal MultipleChoiceProperty InitializeAlignmentProp(bool isLineAlignment)
        {
            string[] choices;
            string selectedStr;
            MultipleChoiceProperty res;
            if (isLineAlignment)
            {
                choices = GetChoicesFromResource("Text.LineAlignment");
                selectedStr = choices[(int)text.LineAlignment];
                res = new MultipleChoiceProperty("Text.LineAlignment", choices, selectedStr);
                res.ValueChangedEvent += LineAlignmentChanged;
            }
            else
            {
                choices = GetChoicesFromResource("Text.Alignment");
                selectedStr = choices[(int)text.Alignment];
                res = new MultipleChoiceProperty("Text.Alignment", choices, selectedStr);
                res.ValueChangedEvent += AlignmentChanged;
            }
            return res;
        }
        internal IPropertyEntry[] GetAdditionalTextProperties()
        {
            List<IPropertyEntry> res = new List<IPropertyEntry>();
            res.Add(InitializeAlignmentProp(false));
            res.Add(InitializeAlignmentProp(true));
            res.Add(lineDirectionProperty);
            res.Add(glyphDirectionProperty);
            // Umständlich, da DoubleProperty uninitialisiert kommt
            sizeProp.LengthChanged();
            res.Add(sizeProp);
            res.Add(glyphAngleProp);
            res.Add(new ShowPropertyFont(text));
            return res.ToArray();
        }
        #region IShowPropertyImpl Overrides
        public override PropertyEntryType Flags => PropertyEntryType.GroupTitle | PropertyEntryType.HasSubEntries | PropertyEntryType.ContextMenu | PropertyEntryType.Selectable;
        /// <summary>
        /// Overrides <see cref="IShowPropertyImpl.Added"/>
        /// </summary>
        /// <param name="propertyTreeView"></param>
        public override void Added(IPropertyPage propertyTreeView)
        {
            base.Added(propertyTreeView);
            locationProperty.ModifyWithMouse += locationProperty_ModifyWithMouse;
            glyphDirectionProperty.ModifyWithMouse += DirPropModifyWithMouse;
            lineDirectionProperty.OnGetValue = () => text.LineDirection;
            lineDirectionProperty.OnSetValue = v => text.SetLineDirKeepGlyphAngle(v);
            lineDirectionProperty.ModifyWithMouse += DirPropModifyWithMouse;
            sizeProp.ModifyWithMouse += SizePropModifyWithMouse;
            text.DidChangeEvent += TextDidChange;
        }
        /// <summary>
        /// Overrides <see cref="CADability.UserInterface.IShowPropertyImpl.Removed (IPropertyTreeView)"/>
        /// </summary>
        /// <param name="propertyTreeView"></param>
        public override void Removed(IPropertyPage propertyTreeView)
        {
            base.Removed(propertyTreeView);
            locationProperty.ModifyWithMouse -= locationProperty_ModifyWithMouse;
            glyphDirectionProperty.ModifyWithMouse -= DirPropModifyWithMouse;
            lineDirectionProperty.OnGetValue = () => text.LineDirection;
            lineDirectionProperty.OnSetValue = v => text.SetLineDirKeepGlyphAngle(v);
            lineDirectionProperty.ModifyWithMouse -= DirPropModifyWithMouse;
            sizeProp.ModifyWithMouse -= SizePropModifyWithMouse;
            text.DidChangeEvent -= TextDidChange;
        }
        public override IPropertyEntry[] SubItems
        {
            get
            {
                if (showProperties == null)
                {
                    List<IPropertyEntry> list = new List<IPropertyEntry>();
                    list.Add(stringProperty);
                    list.Add(locationProperty);
                    list.Add(InitializeAlignmentProp(false));
                    list.Add(InitializeAlignmentProp(true));
                    list.Add(lineDirectionProperty);
                    list.Add(glyphDirectionProperty);
                    // Umständlich, da DoubleProperty uninitialisiert kommt
                    sizeProp.LengthChanged();
                    list.Add(sizeProp);
                    list.Add(glyphAngleProp);
                    list.Add(new ShowPropertyFont(text));
                    list.AddRange(text.GetAttributeProperties(Frame));

                    showProperties = list.ToArray();
                }
                return showProperties;
            }
        }
        /// <summary>
        /// Overrides <see cref="CADability.UserInterface.IShowPropertyImpl.Opened (bool)"/>
        /// </summary>
        /// <param name="isOpen"></param>
        public override void Opened(bool isOpen)
        {
            if (HotspotChangedEvent != null)
            {
                if (IsOpen)
                {
                    HotspotChangedEvent(locationProperty, HotspotChangeMode.Visible);
                    HotspotChangedEvent(glyphDirHotspot, HotspotChangeMode.Visible);
                    HotspotChangedEvent(lineDirHotspot, HotspotChangeMode.Visible);
                    HotspotChangedEvent(sizeHotspot, HotspotChangeMode.Visible);
                }
                else
                {
                    HotspotChangedEvent(locationProperty, HotspotChangeMode.Invisible);
                    HotspotChangedEvent(glyphDirHotspot, HotspotChangeMode.Invisible);
                    HotspotChangedEvent(lineDirHotspot, HotspotChangeMode.Invisible);
                    HotspotChangedEvent(sizeHotspot, HotspotChangeMode.Invisible);
                }
            }
            base.Opened(IsOpen);
        }

        public override MenuWithHandler[] ContextMenu
        {
            get
            {
                List<MenuWithHandler> items = new List<MenuWithHandler>(MenuResource.LoadMenuDefinition("MenuId.Object.Text", false, this));
                CreateContextMenueEvent?.Invoke(this, items);
                text.GetAdditionalContextMenue(this, Frame, items);
                return items.ToArray();
            }
        }
        #endregion
        private void LineAlignmentChanged(object sender, object newValue)
        {
            MultipleChoiceProperty mcp = sender as MultipleChoiceProperty;
            text.LineAlignment = (Text.LineAlignMode)mcp.CurrentIndex;
        }
        private void AlignmentChanged(object sender, object newValue)
        {
            MultipleChoiceProperty mcp = sender as MultipleChoiceProperty;
            text.Alignment = (Text.AlignMode)mcp.CurrentIndex;
        }
        #region IDisplayHotSpots Members

        public event HotspotChangedDelegate HotspotChangedEvent;

        /// <summary>
        /// Implements <see cref="CADability.IDisplayHotSpots.ReloadProperties ()"/>
        /// </summary>
        public void ReloadProperties()
        {
            // TODO:  Add ShowPropertyText.ReloadProperties implementation
        }

        #endregion
        private void TextDidChange(IGeoObject sender, GeoObjectChange change)
        {
            GeoPoint gp = text.Location;
            GeoVector vg = text.GlyphDirection;
            glyphDirHotspot.Position = new GeoPoint(gp, vg.x, vg.y, vg.z);
            GeoVector vl = text.LineDirection;
            vl.Length = text.Length;
            lineDirHotspot.Position = new GeoPoint(gp, vl.x, vl.y, vl.z);

            vl.Length = text.Length / 2.0;
            GeoVector vs = vg + vl;
            sizeHotspot.Position = new GeoPoint(gp, vs.x, vs.y, vs.z);
            glyphAngleProp.AngleChanged();
            sizeProp.LengthChanged();
            locationProperty.Refresh();
            glyphDirectionProperty.Refresh();
            stringProperty.Refresh();
            lineDirectionProperty.Refresh();
        }
        private void locationProperty_ModifyWithMouse(IPropertyEntry sender, bool startModifying)
        {
            GeneralGeoPointAction gpa = new GeneralGeoPointAction(locationProperty, text);
            Frame.SetAction(gpa);
        }
        private void DirPropModifyWithMouse(IPropertyEntry sender, bool startModifying)
        {
            GeneralGeoVectorAction gpa = new GeneralGeoVectorAction(sender as GeoVectorProperty, text.Location);
            Frame.SetAction(gpa);
        }
        private void SizePropModifyWithMouse(IPropertyEntry sender, bool startModifying)
        {
            GeoVector vl = text.LineDirection;
            GeoVector vg = text.GlyphDirection;
            GeoPoint gp = text.Location;
            vl.Length = text.Length / 2.0;
            vl += vg;
            GeneralLengthAction gla = new GeneralLengthAction(sizeProp, new GeoPoint(gp, vl.x, vl.y, vl.z));
            Frame.SetAction(gla);
        }

        #region ICommandHandler Members
        /// <summary>
        /// Implements <see cref="CADability.UserInterface.ICommandHandler.OnCommand (string)"/>
        /// </summary>
        /// <param name="menuId"></param>
        /// <returns></returns>
        public virtual bool OnCommand(string menuId)
        {
            switch (menuId)
            {
                case "MenuId.Outline":
                    if (Frame.ActiveAction is SelectObjectsAction)
                    {
                        using (Frame.Project.Undo.UndoFrame)
                        {
                            IGeoObjectOwner addTo = text.Owner ?? Frame.ActiveView.Model;
                            GeoObjectList list = text.GetOutline();
                            foreach (var t in list)
                            {
                                t.CopyAttributes(text);
                                addTo.Add(t);
                            }
                            addTo.Remove(text);
                            SelectObjectsAction soa = Frame.ActiveAction as SelectObjectsAction;
                            soa.SetSelectedObjects(list); // alle Teilobjekte markieren
                        }
                    }
                    return true;
            }
            return false;
        }
        /// <summary>
        /// Implements <see cref="CADability.UserInterface.ICommandHandler.OnUpdateCommand (string, CommandState)"/>
        /// </summary>
        /// <param name="menuId"></param>
        /// <param name="commandState"></param>
        /// <returns></returns>
        public virtual bool OnUpdateCommand(string menuId, CommandState commandState)
        {
            switch (menuId)
            {
                case "MenuId.Outline":
                    commandState.Enabled = true;
                    return true;
            }
            return false;
        }
        void ICommandHandler.OnSelected(MenuWithHandler selectedMenuItem, bool selected) { }
        #endregion
        #region IGeoObjectShowProperty Members
        public event CreateContextMenueDelegate CreateContextMenueEvent;
        IGeoObject IGeoObjectShowProperty.GetGeoObject()
        {
            return text;
        }
        string IGeoObjectShowProperty.GetContextMenuId()
        {
            return "MenuId.Object.Text";
        }
        #endregion
    }

    internal class FontCache
    {
        // Holds CompoundShapes for all characters requested so far  
        // and a kerning table for all fonts used so far  
        // Maintains a DeviceContext throughout its lifetime, where only one font is switched.  
        // The unit of the font is size 1.0, allowing easy work without scaling.  
        // However, the faces are only generated when a string is created.  
        // It might also be possible to keep a list of faces for each character in different sizes,  
        // as there are often only a few different sizes in a model.
        public static FontCache GlobalFontCache = new FontCache();
        static int FontPrecision;
        static bool displayAsPath = false;
        struct DictKey
        {
            public readonly string fontName;
            public readonly int fontStyle;
            public readonly char c;
            public DictKey(string fontName, int fontStyle, char c)
            {
                this.fontName = fontName;
                this.fontStyle = fontStyle;
                this.c = c;
            }
            public override int GetHashCode()
            {
                return fontName.GetHashCode() ^ fontStyle ^ (int)c;
            }
            public override bool Equals(object obj)
            {
                if (obj is DictKey)
                {
                    DictKey other = (DictKey)obj;
                    return other.fontName == fontName && other.fontStyle == fontStyle && other.c == c;
                }
                return false;
            }
        }
        struct FontKey
        {
            public readonly string fontName;
            public readonly int fontStyle;
            public FontKey(string fontName, int fontStyle)
            {
                this.fontName = fontName;
                this.fontStyle = fontStyle;
            }
            public override int GetHashCode()
            {
                return fontName.GetHashCode() ^ fontStyle;
            }
            public override bool Equals(object obj)
            {
                if (obj is FontKey)
                {
                    FontKey other = (FontKey)obj;
                    return other.fontName == fontName && other.fontStyle == fontStyle;
                }
                return false;
            }
        }
        struct KerningKey
        {
            public readonly string fontName;
            public readonly int fontStyle;
            public KerningKey(string fontName, int fontStyle)
            {
                this.fontName = fontName;
                this.fontStyle = fontStyle;
            }
            public override int GetHashCode()
            {
                return fontName.GetHashCode() ^ fontStyle;
            }
            public override bool Equals(object obj)
            {
                if (obj is KerningKey other)
                {
                    return other.fontName == fontName && other.fontStyle == fontStyle;
                }
                return false;
            }
        }
        struct DictVal
        {
            public CompoundShape shape; // hat die Splines statt der Polygone
            public IPaintTo3DList list;
            public double width;
        }
        struct CenterLineVal
        {
            public GeoObjectList list;
            public double width;
        }
        struct FontCharacteristics
        {
            // in diesen Y-Bereichen darf keine Linie enden (für ShapeMiddelLine)
            public double baseLineMin, baseLineMax;
            public double middleLineMin, middleLineMax;
            public double topLineMin, topLineMax;
            public double strokeWidth;
        }

        readonly Dictionary<DictKey, DictVal> cache; // Cache von CompundShapes und Breiten für einzelne zeichen
        readonly Dictionary<DictKey, CenterLineVal> centerLineCache; // Cache für die CenterLines, Größe 1
        readonly Dictionary<FontKey, FontCharacteristics> fontCharacteristicsCache; // Cache für die FontCharacteristics
        readonly Dictionary<KerningKey, Dictionary<Pair<char, char>, double>> kerning; // Kerningtabellen
        readonly IntPtr hDC; // fester DeviceContext (für die ganze Lebensdauer)
        public FontCache()
        {
            FontPrecision = Settings.GlobalSettings.GetIntValue("Font.Precision", 1); // |grob|mittel|fein
            displayAsPath = Settings.GlobalSettings.GetIntValue("Font.DisplayMode", 1) == 1;
            cache = new Dictionary<DictKey, DictVal>();
            centerLineCache = new Dictionary<DictKey, CenterLineVal>();
            fontCharacteristicsCache = new Dictionary<FontKey, FontCharacteristics>();
            kerning = new Dictionary<KerningKey, Dictionary<Pair<char, char>, double>>();
            hDC = Gdi.CreateCompatibleDC(IntPtr.Zero);
        }
        ~FontCache()
        {
            Gdi.DeleteDC(hDC);
        }
        private void AddToPath2D(List<ICurve2D> addto, List<GeoPoint2D> points, bool spline, bool close)
        {
            if (spline)
            {
                for (int i = 0; i < points.Count - 3; i += 3)
                {

                    double[] knots = new double[2];
                    int[] multiplicities = new int[2];
                    knots[0] = 0;
                    knots[1] = 1;
                    multiplicities[0] = 4;
                    multiplicities[1] = 4;
                    GeoPoint2D[] pp = new GeoPoint2D[4];
                    points.CopyTo(i, pp, 0, 4);
                    BSpline2D bsp = new BSpline2D(pp, null, knots, multiplicities, 3, false, 0.0, 1.0);
                    // addto.Add(bsp);
                    switch (FontPrecision)
                    {
                        case 0: // grob
                            addto.Add(bsp.Approximate(true, 0.2));
                            break;
                        case 1: // mittel
                            addto.Add(bsp.Approximate(true, 0.05));
                            break;
                        case 2:
                            // addto.Add(bsp.Approximate(true, 0.005));
                            addto.Add(bsp); // wenn man den BSpline selbst zufügt, dann könnte man auch mit dynamischer Auflösung arbeiten
                            break;
                    }
                }
            }
            else
            {
                try
                {
                    if (points.Count > 1)
                    {
                        if (points.Count > 2 || points[0] != points[1])
                        {   // zwei identische Punkte gibt exception und ist langsam. Deshalb hier ausschließen
                            Polyline2D pl = new Polyline2D(points.ToArray());
                            addto.Add(pl);
                        }
                    }
                }
                catch (Polyline2DException) { } // nur zwei identische Punkte
            }
            points.RemoveRange(0, points.Count - 1); // den letzten als ersten drinlassen
        }
        public Path2D[] GetOutline2D(string fontName, int fontStyle, char c, out double width)
        {
            GraphicsPath path = new GraphicsPath();
            FontFamily ff;
            if (Text.FontFamilyNames.Contains(fontName.ToUpper()))
            {
                ff = new FontFamily(fontName);
            }
            else
            {
                ff = new FontFamily(System.Drawing.Text.GenericFontFamilies.SansSerif);
            }
            StringFormat sf = StringFormat.GenericTypographic.Clone() as StringFormat;
            sf.LineAlignment = StringAlignment.Near;
            sf.Alignment = StringAlignment.Near;
            if (!ff.IsStyleAvailable((FontStyle)fontStyle))
            {
                if (ff.IsStyleAvailable(FontStyle.Regular)) fontStyle = (int)FontStyle.Regular;
                if (ff.IsStyleAvailable(FontStyle.Bold)) fontStyle = (int)FontStyle.Bold;
                if (ff.IsStyleAvailable(FontStyle.Italic)) fontStyle = (int)FontStyle.Italic;
                if (ff.IsStyleAvailable(FontStyle.Strikeout)) fontStyle = (int)FontStyle.Strikeout;
                if (ff.IsStyleAvailable(FontStyle.Underline)) fontStyle = (int)FontStyle.Underline;
            }
            int fs = fontStyle;
            int em = ff.GetEmHeight((FontStyle)fs);
            if (em == 0) em = 1000;
            Font font = new Font(ff, em, (FontStyle)fs, GraphicsUnit.Pixel);
            IntPtr hfont = font.ToHfont();
            IntPtr oldfont = Gdi.SelectObject(hDC, hfont);
            Gdi.ABC[] abc = new Gdi.ABC[1];
            Gdi.GetCharABCWidths(hDC, (uint)c, (uint)c, abc);
            //int[] widths = new int[1]; // liefert das selbe wie GetCharABCWidths, nur die Summe halt.
            //Gdi.GetCharWidth32(hDC, (uint)c, (uint)c, widths);
            if (!kerning.ContainsKey(new KerningKey(fontName, fontStyle)))
            {
                KerningKey kk = new KerningKey(fontName, fontStyle);
                Dictionary<Pair<char, char>, double> pairs = new Dictionary<Pair<char, char>, double>();
                kerning[kk] = pairs;
                int num = Gdi.GetKerningPairs(hDC, 0, null);
                if (num > 0)
                {
                    Gdi.KERNINGPAIR[] kp = new Gdi.KERNINGPAIR[num];
                    int ok = Gdi.GetKerningPairs(hDC, num, kp);
                    for (int i = 0; i < kp.Length; ++i)
                    {
                        pairs[new Pair<char, char>((char)kp[i].wFirst, (char)kp[i].wSecond)] = kp[i].iKernAmount / (double)em;
                    }
                }
            }
            Gdi.SelectObject(hDC, oldfont);
            Gdi.DeleteObject(hfont);

            width = (abc[0].abcA + abc[0].abcB + abc[0].abcC) / (double)em;

            path.AddString(c.ToString(), ff, fs, 1.0f, new PointF(0.0f, 0.0f), sf);
            List<Path2D> res = new List<Path2D>();
            if (path.PointCount > 0)
            {
                List<PointF> pp = new List<PointF>(path.PathPoints);
                List<byte> pt = new List<byte>(path.PathTypes);
                int last0 = -1;
                for (int i = 0; i < pt.Count; ++i)
                {
                    pp[i] = new PointF(pp[i].X, (float)(1.0f - pp[i].Y));
                    if ((pt[i] & 0x01) == 0) last0 = i;
                    if ((pt[i] & 0x80) != 0 && last0 >= 0)
                    {
                        pt[i] = (byte)(pt[i] & 0x7F);
                        pt.Insert(i + 1, (byte)(pt[last0] | 0x81));
                        pp.Insert(i + 1, pp[last0]);
                        ++i;
                        last0 = -1;
                    }

                }
                if (pp == null || pp.Count == 0)
                {
                    return res.ToArray();
                }
                List<GeoPoint2D> current = new List<GeoPoint2D>();
                int mode = 0; // 0 noch nicht bekannt, 1: Linie, 3 Spline
                GeoPoint2D startPoint = GeoPoint2D.Origin;
                List<ICurve2D> segment = new List<ICurve2D>();
                bool close = false;
                for (int i = 0; i < pp.Count; ++i)
                {
                    switch (pt[i] & 0x03)
                    {
                        case 0: // neuer Anfang
                            if (current.Count > 1)
                            {
                                AddToPath2D(segment, current, mode == 3, close);
                                res.Add(new Path2D(segment.ToArray()));
                            }
                            segment.Clear();
                            current.Clear();
                            current.Add(new GeoPoint2D(pp[i]));
                            mode = 0;
                            break;
                        case 1:
                            if (mode == 3)
                            {   // Spline beenden, polylinie anfangen
                                AddToPath2D(segment, current, true, false);
                            }
                            current.Add(new GeoPoint2D(pp[i]));
                            mode = 1;
                            break;
                        case 2:
                        case 3:
                            if (mode == 1)
                            {
                                AddToPath2D(segment, current, false, false);
                            }
                            current.Add(new GeoPoint2D(pp[i]));
                            mode = 3;
                            break;
                    }
                    close = (pt[i] & 0x80) != 0;
                }

                if (current.Count > 1)
                {
                    AddToPath2D(segment, current, mode == 3, close);
                    res.Add(new Path2D(segment.ToArray()));
                }
            }
            //BoundingRect ext = BoundingRect.EmptyBoundingRect;
            //ext.MinMax(GeoPoint2D.Origin); // Ursprung mit einbezogen, ist das OK?
            //for (int i = 0; i < res.Count; ++i)
            //{
            //    ext.MinMax(res[i].GetExtent());
            //}
            //ll = ext.GetLowerLeft();
            //lr = ext.GetLowerRight();
            //ul = ext.GetUpperLeft();
            //ModOp2D m = ModOp2D.Scale(1.0 / em);
            //if (this.lineAlignment != LineAlignMode.Left || this.alignment != AlignMode.Bottom)
            //{
            //    int ls = ff.GetLineSpacing((FontStyle)fs);
            //    int dc = ff.GetCellDescent((FontStyle)fs);
            //    double dx = 0.0;
            //    double dy = 0.0;
            //    switch (lineAlignment)
            //    {
            //        case LineAlignMode.Center: dx = -ext.Width / 2.0; break;
            //        case LineAlignMode.Right: dx = -ext.Width; break;
            //    }
            //    switch (alignment)
            //    {
            //        case AlignMode.Baseline: dy = -dc; break;
            //        case AlignMode.Center: dy = -em / 2.0; break;
            //        case AlignMode.Top: dy = -ls; break;
            //    }
            //    for (int i = 0; i < res.Count; ++i)
            //    {
            //        res[i].Move(dx, dy);
            //    }
            //    ll += new GeoVector2D(dx, dy);
            //    lr += new GeoVector2D(dx, dy);
            //    ul += new GeoVector2D(dx, dy);
            //}
            //for (int i = 0; i < res.Count; ++i)
            //{
            //    res[i] = (res[i] as ICurve2D).GetModified(m) as Path2D;
            //}
            //ll = m * ll;
            //lr = m * lr;
            //ul = m * ul;
            return res.ToArray();
        }
        public CompoundShape Get(string font, int fontStyle, char c, out double width)
        {   // soll verschwinden!!
            DictVal found;
            lock (this)
            {
                if (!cache.TryGetValue(new DictKey(font, fontStyle, c), out found) || found.shape == null)
                {
                    found = new DictVal();
                    Path2D[] paths = GetOutline2D(font, fontStyle, c, out width);
                    OrderedMultiDictionary<BoundingRect, SimpleShape> sortedshapes = new OrderedMultiDictionary<BoundingRect, SimpleShape>(true);
                    for (int i = 0; i < paths.Length; ++i)
                    {
                        if (paths[i].IsClosed)
                        {
                            SimpleShape ss = new SimpleShape(paths[i].MakeBorder());
                            sortedshapes.Add(ss.GetExtent(), ss);
                        }
                    }
                    List<SimpleShape> sortedList = sortedshapes.SortedValues;
                    CompoundShape res = new CompoundShape(); // leer
                    while (sortedList.Count > 0)
                    {
                        SimpleShape ss = sortedList[sortedList.Count - 1]; // das größte
                        sortedList.RemoveAt(sortedList.Count - 1); // raus aus der Liste
                        CompoundShape cs = new CompoundShape(ss);
                        for (int i = sortedList.Count - 1; i >= 0; --i)
                        {
                            switch (SimpleShape.GetPosition(ss, sortedList[i]))
                            {
                                case SimpleShape.Position.firstcontainscecond:
                                    cs = cs - new CompoundShape(sortedList[i]);
                                    sortedList.RemoveAt(i);
                                    break;
                                default:
                                    // alle anderen werden nicht verwendet. Beim Überschneiden müsste man noch überlegen
                                    // z.B. Font raumTalk
                                    break;
                            }
                        }
                        res = res + cs;
                    }
                    found.shape = res;
                    double extwidth = found.shape.GetExtent().GetWidth();
                    if (width < extwidth * 0.9) width = extwidth * 1.1;
                    found.width = width; // hier könnte man mit TextRenderer.MeasureText arbeiten, wenns so nicht passt
                    cache[new DictKey(font, fontStyle, c)] = found;
                }
            }
            width = found.width;
            return found.shape;
        }
        public double GetKerning(string font, int fontStyle, char a, char b)
        {
            Dictionary<Pair<char, char>, double> pairs;
            double res = 0.0;
            if (kerning.TryGetValue(new KerningKey(font, fontStyle), out pairs))
            {
                pairs.TryGetValue(new Pair<char, char>(a, b), out res);
            }
            return res;
        }
        public IPaintTo3DList Get(string font, int fontStyle, char c, out double width, IPaintTo3D paintTo3D)
        {
            bool useLists = true;
            if (paintTo3D != null) useLists = !paintTo3D.IsBitmap;
            DictVal found;
            lock (this)
            {
                if (useLists && cache.TryGetValue(new DictKey(font, fontStyle, c), out found))
                {
                    if (found.list != null || paintTo3D == null)
                    {
                        width = found.width;
                        return found.list;
                    }
                }
                // no list found for this character in the cache or don't use display list
                bool oldSelectMode = false;
                if (paintTo3D != null && useLists)
                {
                    oldSelectMode = paintTo3D.SelectMode;
                    // paintTo3D.SelectMode = true; // no, all Text will be in select color if we set SelectMode
                    paintTo3D.OpenList(font + ":" + c);
                }
                found = new DictVal();
                Path2D[] paths = GetOutline2D(font, fontStyle, c, out width);
                if (paintTo3D != null)
                {
                    if (displayAsPath)
                    {
                        for (int i = 0; i < paths.Length; ++i)
                        {
                            IGeoObject go = paths[i].MakeGeoObject(Plane.XYPlane);
                            go.PaintTo3D(paintTo3D);
                        }
                    }
                    else
                    {
                        OrderedMultiDictionary<BoundingRect, SimpleShape> sortedshapes = new OrderedMultiDictionary<BoundingRect, SimpleShape>(true);
                        for (int i = 0; i < paths.Length; ++i)
                        {
                            if (paths[i].IsClosed)
                            {
                                SimpleShape ss = new SimpleShape(paths[i].MakeBorder());
                                sortedshapes.Add(ss.GetExtent(), ss);
                            }
                        }
                        List<SimpleShape> sortedList = sortedshapes.SortedValues;
                        CompoundShape res = new CompoundShape(); // empty
                        while (sortedList.Count > 0)
                        {
                            SimpleShape ss = sortedList[sortedList.Count - 1]; // the biggest shape
                            sortedList.RemoveAt(sortedList.Count - 1); // remove it from the list
                            CompoundShape cs = new CompoundShape(ss);
                            for (int i = sortedList.Count - 1; i >= 0; --i)
                            {
                                switch (SimpleShape.GetPosition(ss, sortedList[i]))
                                {
                                    case SimpleShape.Position.firstcontainscecond:
                                        cs = cs - new CompoundShape(sortedList[i]);
                                        sortedList.RemoveAt(i);
                                        break;
                                    default:
                                        // all shapes must be contained in the outer shape, there should not be overlapping shapes
                                        // but there is one example with overlapping shapes: Font raumTalk
                                        break;
                                }
                            }
                            res = res + cs;
                        }
                        PlaneSurface pls = new PlaneSurface(Plane.XYPlane);
                        for (int i = 0; i < res.SimpleShapes.Length; ++i)
                        {
                            Face fc = Face.MakeFace(pls, res.SimpleShapes[i]);
                            fc.ColorDef = null; // no color in the display list!
                            double oldprecision = paintTo3D.Precision;
                            switch (FontPrecision) // this is for triangulation precision, font size is 1
                            {
                                case 0: // rough
                                    paintTo3D.Precision = 0.2;
                                    break;
                                case 1: // middle
                                    paintTo3D.Precision = 0.05;
                                    break;
                                case 2: // fine
                                    paintTo3D.Precision = 0.005;
                                    break;
                            }
                            bool oldpse = paintTo3D.PaintSurfaceEdges;
                            paintTo3D.PaintSurfaceEdges = false;
                            fc.PaintTo3D(paintTo3D); // will be triangulated according to paintTo3D.Precision 
                            paintTo3D.Precision = oldprecision;
                            paintTo3D.PaintSurfaceEdges = oldpse;
                        }
                    }
                    if (useLists)
                    {
                        found.list = paintTo3D.CloseList(); // kann null werden, bei Leerzeichen z.B.
                    }
                    paintTo3D.SelectMode = oldSelectMode;
                }
                BoundingRect ext = BoundingRect.EmptyBoundingRect;
                for (int i = 0; i < paths.Length; ++i)
                {
                    ext.MinMax(paths[i].GetExtent());
                }
                double extwidth = ext.Width;
                // if (width < extwidth * 0.9) width = extwidth * 1.1; warum diese Zeile? Stört bei Commercial Script
                found.width = width; // hier könnte man mit TextRenderer.MeasureText arbeiten, wenns so nicht passt
                if (useLists)
                {
                    cache[new DictKey(font, fontStyle, c)] = found;
                }
            }
            width = found.width;
            if (useLists) return found.list;
            else return null;
        }
        public void Clear()
        {
            cache.Clear();
        }
        public GeoObjectList GetCenterLines(string font, int fontStyle, char c, out double width)
        {
            CenterLineVal found;
            if (!centerLineCache.TryGetValue(new DictKey(font, fontStyle, c), out found))
            {
                found = new CenterLineVal();
                Path2D[] paths = GetOutline2D(font, fontStyle, c, out width);
#if DEBUG
                DebuggerContainer dc = new DebuggerContainer();
                for (int i = 0; i < paths.Length; i++)
                {
                    dc.Add(paths[i], Color.Red, i);
                }
#endif
                OrderedMultiDictionary<BoundingRect, SimpleShape> sortedshapes = new OrderedMultiDictionary<BoundingRect, SimpleShape>(true);
                for (int i = 0; i < paths.Length; ++i)
                {
                    if (paths[i].IsClosed)
                    {
                        SimpleShape ss = new SimpleShape(paths[i].MakeBorder());
                        sortedshapes.Add(ss.GetExtent(), ss);
                    }
                }
                List<SimpleShape> sortedList = sortedshapes.SortedValues;
                CompoundShape ccs = new CompoundShape(); // leer
                while (sortedList.Count > 0)
                {
                    SimpleShape ss = sortedList[sortedList.Count - 1]; // das größte
                    sortedList.RemoveAt(sortedList.Count - 1); // raus aus der Liste
                    CompoundShape cs = new CompoundShape(ss);
                    for (int i = sortedList.Count - 1; i >= 0; --i)
                    {
                        switch (SimpleShape.GetPosition(ss, sortedList[i]))
                        {
                            case SimpleShape.Position.firstcontainscecond:
                                cs = cs - new CompoundShape(sortedList[i]);
                                sortedList.RemoveAt(i);
                                ss = cs.SimpleShapes[0]; // das wird ja nur für den Test verwendet. Problem bei "©", der äußere Ring enthält auch das "c"
                                // cs sollte ja auch immer nur einfach sein, denn es werden nur ganz enthaltene SimpleShapes abgezogen
                                break;
                            default:
                                // alle anderen werden nicht verwendet. Beim Überschneiden müsste man noch überlegen
                                // z.B. Font raumTalk
                                break;
                        }
                    }
                    ccs = ccs + cs;
                }
                ShapeMiddelLine sm = new ShapeMiddelLine(ccs); // sm könnte hier noch einige Eigenschaften bekommen, die Größe des Zeichens ist 1!
                sm.pointSize = 0.2; // Gesamtgröße ist 1
                FontCharacteristics fc;
                if (!fontCharacteristicsCache.TryGetValue(new FontKey(font, fontStyle), out fc))
                {
                    fc = new FontCharacteristics();
                    double w;
                    double strokeWidth = 0.0;
                    int strokeCount = 0;
                    fontCharacteristicsCache[new FontKey(font, fontStyle)] = fc; // schon mal setzen, damit folgendes nicht endlos wird:
                    GeoObjectList lo = GetCenterLines(font, fontStyle, 'o', out w);
                    BoundingCube ext = lo.GetExtent();
                    double bmax = ext.Ymin;
                    double mmin = ext.Ymax;
                    Path2D[] po = GetOutline2D(font, fontStyle, 'o', out w);
                    BoundingRect br = BoundingRect.EmptyBoundingRect;
                    for (int i = 0; i < po.Length; i++)
                    {
                        br.MinMax(po[i].GetExtent());
                    }
                    double bmin = br.Bottom;
                    double mmax = br.Top;
                    // eine vernünftige Strichbreite für alle Zeichen finden;
                    // horizontale und vertikale Linie gegen die Form des Zeichens clippen
                    CompoundShape cs = CompoundShape.CreateFromList(po, 0.01);
                    if (cs != null)
                    {
                        Line2D l2d = new Line2D(br.GetMiddleLeft(), br.GetMiddleRight());
                        double[] inside = cs.Clip(l2d, true);
                        for (int i = 0; i < inside.Length - 1; i += 2)
                        {
                            strokeWidth += l2d.PointAt(inside[i + 1]) | l2d.PointAt(inside[i]);
                            ++strokeCount;
                        }
                        l2d = new Line2D(br.GetLowerMiddle(), br.GetUpperMiddle());
                        inside = cs.Clip(l2d, true);
                        for (int i = 0; i < inside.Length - 1; i += 2)
                        {
                            strokeWidth += l2d.PointAt(inside[i + 1]) | l2d.PointAt(inside[i]);
                            ++strokeCount;
                        }
                    }
                    lo = GetCenterLines(font, fontStyle, 'O', out w);
                    ext = lo.GetExtent();
                    double tmin = ext.Ymax;
                    po = GetOutline2D(font, fontStyle, 'O', out w);
                    br = BoundingRect.EmptyBoundingRect;
                    for (int i = 0; i < po.Length; i++)
                    {
                        br.MinMax(po[i].GetExtent());
                    }
                    double tmax = br.Top;
                    cs = CompoundShape.CreateFromList(po, 0.01);
                    if (cs != null)
                    {
                        Line2D l2d = new Line2D(br.GetMiddleLeft(), br.GetMiddleRight());
                        double[] inside = cs.Clip(l2d, true);
                        for (int i = 0; i < inside.Length - 1; i += 2)
                        {
                            strokeWidth += l2d.PointAt(inside[i + 1]) | l2d.PointAt(inside[i]);
                            ++strokeCount;
                        }
                        l2d = new Line2D(br.GetLowerMiddle(), br.GetUpperMiddle());
                        inside = cs.Clip(l2d, true);
                        for (int i = 0; i < inside.Length - 1; i += 2)
                        {
                            strokeWidth += l2d.PointAt(inside[i + 1]) | l2d.PointAt(inside[i]);
                            ++strokeCount;
                        }
                    }
                    fc.baseLineMin = bmin - 0.01;
                    fc.baseLineMax = bmax;
                    fc.middleLineMin = mmin;
                    fc.middleLineMax = mmax + 0.01;
                    fc.topLineMin = tmin;
                    fc.topLineMax = tmax + 0.01;
                    if (strokeCount > 0) fc.strokeWidth = strokeWidth / strokeCount;
                    fontCharacteristicsCache[new FontKey(font, fontStyle)] = fc; // nochmal setzen, ist struct
                }
                sm.setForbiddenBands(fc.baseLineMin, fc.baseLineMax, fc.middleLineMin, fc.middleLineMax, fc.topLineMin, fc.topLineMax, fc.strokeWidth);
                found.list = sm.calculate(Plane.XYPlane);
                found.width = width;
                centerLineCache[new DictKey(font, fontStyle, c)] = found;
            }
            width = found.width;
            return found.list;
        }
    }
    /// <summary>
    /// Simple Text object, Implements <see cref="IGeoObject"/>.
    /// </summary>
    [Serializable()]
    public class Text : IGeoObjectImpl, IColorDef, ISerializable, IJsonSerialize
    {
        // Eine Cachetabelle, die für Fonts und Zeichen ein CompoundShape liefert und eine Breite, wo das nächste Zeichen hinkommt
        // das CompundShape ist auf (0,0) bezogen. Kerning ist nicht berücksichtigt
        static bool displayAsPath = false; // geht nicht: Settings.GlobalSettings.GetIntValue("Font.DisplayMode", 1) == 1;
        /// <summary>
        /// Vertical alignement of the text.
        /// </summary>
        public enum AlignMode
        {
            /// <summary>
            /// <see cref="Text.Location"/> specifies the position of the baseline of the text.
            /// </summary>
            Baseline = 0,
            /// <summary>
            /// <see cref="Text.Location"/> specifies the bottom position of the textbox.
            /// </summary>
            Bottom,
            /// <summary>
            /// <see cref="Text.Location"/> specifies the top position of the textbox.
            /// </summary>
            Top,
            /// <summary>
            /// <see cref="Text.Location"/> specifies the center position of the textbox.
            /// </summary>
            Center
        };
        /// <summary>
        /// Horizontal alignement of the text
        /// </summary>
        public enum LineAlignMode
        {
            /// <summary>
            /// <see cref="Text.Location"/> specifies the left position of the textbox.
            /// </summary>
            Left = 0,
            /// <summary>
            /// <see cref="Text.Location"/> specifies the horizontal center position of the textbox.
            /// </summary>
            Center,
            /// <summary>
            /// <see cref="Text.Location"/> specifies the right position of the textbox.
            /// </summary>
            Right
        };
        private ColorDef colorDef; // die Farbe. 
        //private Plane plane;
        private GeoVector lineDirection;
        private GeoVector glyphDirection;
        private bool isReflected; // lineDirection^glyphDirection is the normal of the objects plane. if isReflected is true, we use glyphDirection^lineDirection instead
        private GeoPoint location;
        private string fontName;
        private string textString;
        private FontStyle fontStyle;
        private SizeF sizeExtent;
        private double emSizeFactor;
        private double emAscDescFactor;
        private double emDescDiff;
        private double desc; // Anteil von Descent zur Gesamtgröße des Zeichens
        private bool isValidExtent;
        private GeoPoint lowerLeft, lowerRight, upperLeft; // die drei characteristischen Punkte
        private AlignMode alignment;
        private LineAlignMode lineAlignment;
        // private IGeoObject[] cachedDisplayItems;
        private static Set<string> fontFamilyNames;
        internal static Set<string> FontFamilyNames
        {
            get
            {
                if (fontFamilyNames == null)
                {
                    FontFamily[] ff = FontFamily.Families;
                    fontFamilyNames = new Set<string>();
                    for (int i = 0; i < ff.Length; i++)
                    {
                        fontFamilyNames.Add(ff[i].Name.ToUpper());
                    }
                }
                return fontFamilyNames;
            }
        }
        #region polymorph construction
        public delegate Text ConstructionDelegate();
        public static ConstructionDelegate Constructor;
        public static Text Construct()
        {
            if (Constructor != null) return Constructor();
            return new Text();
        }
        #endregion
        protected Text()
        {
            displayAsPath = Settings.GlobalSettings.GetIntValue("Font.DisplayMode", 1) == 1; // müsste nur einmal geladen werden, macht aber direkt bei der static Deklaration Probleme
            isValidExtent = false;
            lineDirection = new GeoVector(1.0, 0.0, 0.0);
            glyphDirection = new GeoVector(0.0, 1.0, 0.0);
            alignment = 0;
            fontStyle = FontStyle.Regular;
        }
        private void calcExtent()
        {
            if (!isValidExtent)
            {
                try
                {
                    FontFamily ff;
                    if (FontFamilyNames.Contains(fontName.ToUpper()))
                    {
                        ff = new FontFamily(fontName);
                    }
                    else
                    {
                        ff = new FontFamily(System.Drawing.Text.GenericFontFamilies.SansSerif);
                    }
                    FontStyle fs = fontStyle;
                    if (!ff.IsStyleAvailable(fs))
                    {
                        if (ff.IsStyleAvailable(FontStyle.Regular)) fs = FontStyle.Regular;
                        else if (ff.IsStyleAvailable(FontStyle.Bold)) fs = FontStyle.Bold;
                        else if (ff.IsStyleAvailable(FontStyle.Italic)) fs = FontStyle.Italic;
                        else if (ff.IsStyleAvailable(FontStyle.Strikeout)) fs = FontStyle.Strikeout;
                        else if (ff.IsStyleAvailable(FontStyle.Underline)) fs = FontStyle.Underline; // irgend einen muss es ja wohl geben, oder?
                    }
                    int em = ff.GetEmHeight(fs);
                    int asc = ff.GetCellAscent(fs);
                    int desc = ff.GetCellDescent(fs);
                    int lsp = ff.GetLineSpacing(fs);
                    double dbg = (double)(asc + desc) / (double)lsp;
                    // asc+desc kann mehr als em sein. ich nehme jedoch an, dass em für die Größe ausschlaggebend ist
                    // wenn man em als 1 annimmt, dann muss man um desc/em nach unten verschieben um auf die Grundlinie zu kommen
                    emSizeFactor = (double)(asc + desc) / (double)em;
                    emAscDescFactor = (double)(desc) / (double)asc;
                    emDescDiff = (double)(em - asc) / (double)em;
                    this.desc = (double)(desc) / (double)em;
                    using (Graphics graphics = Graphics.FromImage(new Bitmap(1000, 100)))
                    {
                        using (Font font = new Font(fontName, (float)(glyphDirection.Length), GraphicsUnit.Pixel))
                        {
                            sizeExtent = graphics.MeasureString(textString, font);
                        }
                    }
                    isValidExtent = true;
                }
                catch (Exception e)
                {
                    if (e is ThreadAbortException) throw (e);
                    // System.Diagnostics.Debug.WriteLine("Text calcExtent failed");
                }
            }
        }
        /// <summary>
        /// Modifies the direction, size and aspect ratio of the text so that the text will fit
        /// inside the parallelogram defined by the provided parameters.
        /// </summary>
        /// <param name="ll">Point for the lower left position of the text</param>
        /// <param name="lr">Point for the lower right position of the text</param>
        /// <param name="ul">Point for the upper left position of the text</param>
        public void SetBounds(GeoPoint ll, GeoPoint lr, GeoPoint ul)
        {
            using (new Changing(this))
            {
                glyphDirection = ul - ll;
                lineDirection = lr - ll;
                lineDirection.Length = glyphDirection.Length;
                CalcExtent();
                ModOp mop = ModOp.Fit(new GeoPoint[] { lowerLeft, lowerRight, upperLeft }, new GeoPoint[] { ll, lr, ul }, true);
#if DEBUG
                Polyline pl = Polyline.Construct();
                pl.SetPoints(new GeoPoint[] { lowerLeft, lowerRight, upperLeft }, true);
#endif
                lineDirection = mop * lineDirection;
                glyphDirection = mop * glyphDirection;
                location = mop * location;
                //isValidExtent = false;
                //calcExtent();
                //lineDirection.Length = sizeExtent.Width;
                //location = new GeoPoint(0.0, 0.0, 0.0);
                //GeoPoint fromUL = location + glyphDirection;
                //GeoPoint fromLR = location + lineDirection;
                //lineDirection.Length = glyphDirection.Length;
                //ModOp mop = ModOp.Fit(new GeoPoint[] { location, fromLR, fromUL }, new GeoPoint[] { ll, lr, ul }, true);
                //Modify(mop);
                //isValidExtent = false;
                //calcExtent();
                //switch (lineAlignment)
                //{
                //    case LineAlignMode.Left:
                //        break;
                //    case LineAlignMode.Center:
                //        location.x += sizeExtent.Width / 2.0;
                //        break;
                //    case LineAlignMode.Right:
                //        location.x += sizeExtent.Width;
                //        break;
                //}
                //switch (alignment)
                //{
                //    case AlignMode.Baseline://Grundlinie

                //        location.y -= glyphDirection.Length * emAscDescFactor * emSizeFactor;
                //        break;
                //    case AlignMode.Bottom://unten
                //        location.y -= glyphDirection.Length * emDescDiff * emSizeFactor;
                //        break;
                //    case AlignMode.Center://zentriet
                //        location.y += glyphDirection.Length * emSizeFactor / 2.0;
                //        break;
                //    case AlignMode.Top:
                //        location.y += glyphDirection.Length * emSizeFactor;
                //        break;
                //}
            }
        }
        private void AddToPath(Path path, List<GeoPoint> points, bool spline, bool close)
        {
            //if (close && path.CurveCount > 0) points.Add(path.StartPoint);
            if (spline)
            {
                for (int i = 0; i < points.Count - 3; i += 3)
                {
                    BSpline bsp = BSpline.Construct();
                    double[] knots = new double[2];
                    int[] multiplicities = new int[2];
                    knots[0] = 0;
                    knots[1] = 1;
                    multiplicities[0] = 4;
                    multiplicities[1] = 4;
                    GeoPoint[] pp = new GeoPoint[4];
                    points.CopyTo(i, pp, 0, 4);
                    bsp.SetData(3, pp, null, knots, multiplicities, false);
                    path.Add(bsp);
                }
            }
            else
            {
                try
                {
                    Polyline pl = Polyline.Construct();
                    pl.SetPoints(points.ToArray(), close && path.CurveCount == 0);
                    path.Add(pl);
                }
                catch (PolylineException) { } // nur zwei identische Punkte
            }
            points.RemoveRange(0, points.Count - 1); // den letzten als ersten drinlassen
        }
        private void AddToPath2D(List<ICurve2D> addto, List<GeoPoint2D> points, bool spline, bool close)
        {
            //if (close && path.CurveCount > 0) points.Add(path.StartPoint);
            if (spline)
            {
                for (int i = 0; i < points.Count - 3; i += 3)
                {

                    double[] knots = new double[2];
                    int[] multiplicities = new int[2];
                    knots[0] = 0;
                    knots[1] = 1;
                    multiplicities[0] = 4;
                    multiplicities[1] = 4;
                    GeoPoint2D[] pp = new GeoPoint2D[4];
                    points.CopyTo(i, pp, 0, 4);
                    BSpline2D bsp = new BSpline2D(pp, null, knots, multiplicities, 3, false, 0.0, 1.0);
                    addto.Add(bsp);
                }
            }
            else
            {
                try
                {
                    Polyline2D pl = new Polyline2D(points.ToArray());
                    addto.Add(pl);
                }
                catch (Polyline2DException) { } // nur zwei identische Punkte
            }
            points.RemoveRange(0, points.Count - 1); // den letzten als ersten drinlassen
        }
        /// <summary>
        /// Returns a list of objects that define the outline (and holes) of this text. The global setting "Font.Precision"
        /// defines the precision of the outline, if the precision is 3 (fine) then splines can occur in the outline.
        /// If the precision is 1 or 2 (raw, middle) the splines are approximated.
        /// </summary>
        /// <returns>List of curves describing the outline of this text object</returns>
        public virtual GeoObjectList GetOutline()
        {
            GeoObjectList res = new GeoObjectList();
            int fs = 0; // das ist regular
            if (Underline) fs |= (int)FontStyle.Underline;
            if (Italic) fs |= (int)FontStyle.Italic;
            if (Bold) fs |= (int)FontStyle.Bold;
            if (Strikeout) fs |= (int)FontStyle.Strikeout;
            PlaneSurface pls = new PlaneSurface(location, lineDirection, glyphDirection, lineDirection ^ glyphDirection);
            FontFamily ff;
            if (FontFamilyNames.Contains(fontName.ToUpper()))
            {
                ff = new FontFamily(fontName);
            }
            else
            {
                ff = new FontFamily(System.Drawing.Text.GenericFontFamilies.SansSerif);
            }
            if (!ff.IsStyleAvailable((FontStyle)fs))
            {
                if (ff.IsStyleAvailable(FontStyle.Regular)) fs = (int)FontStyle.Regular;
                else if (ff.IsStyleAvailable(FontStyle.Bold)) fs = (int)FontStyle.Bold;
                else if (ff.IsStyleAvailable(FontStyle.Italic)) fs = (int)FontStyle.Italic;
                else if (ff.IsStyleAvailable(FontStyle.Strikeout)) fs = (int)FontStyle.Strikeout;
                else if (ff.IsStyleAvailable(FontStyle.Underline)) fs = (int)FontStyle.Underline; // irgend einen muss es ja wohl geben, oder?
            }
            int em = ff.GetEmHeight((FontStyle)fs);
            int ls = ff.GetLineSpacing((FontStyle)fs);
            int dc = ff.GetCellDescent((FontStyle)fs);
            int ac = ff.GetCellAscent((FontStyle)fs);
            double lsp = ls / em;
            double dx = 0.0;
            double dy = 0.0;
            double totwidth = 0.0;
            double totwidthMax = 0.0;
            List<double> totwidthArray = new List<double>(); // Liste der Zeilenlängen bei mehrzeiligen Texten
            for (int i = 0; i < textString.Length; ++i)
            {
                double width;
                if (textString[i] == '\r') // linefeed ignorieren
                {
                    continue;
                }
                if (textString[i] == '\n') // carriage return
                {
                    totwidthArray.Add(totwidth); // Länge in die Liste
                    if (totwidth > totwidthMax) { totwidthMax = totwidth; } // Maximum merken für Extents
                    totwidth = 0.0;
                    continue;
                }
                FontCache.GlobalFontCache.Get(fontName, fs, textString[i], out width, null);
                totwidth += width;
                if (i > 0)
                {
                    double krn = FontCache.GlobalFontCache.GetKerning(fontName, fs, textString[i - 1], textString[i]);
                    totwidth += krn; // das ist negativ
                }
            }
            totwidthArray.Add(totwidth); // den letzten Wert dazu
            if (totwidth > totwidthMax) { totwidthMax = totwidth; }
            if (lineAlignment != LineAlignMode.Left) // Extremwert berechnen
            {
                switch (lineAlignment)
                {
                    case LineAlignMode.Center: dx = -totwidthMax / 2.0; break;
                    case LineAlignMode.Right: dx = -totwidthMax; break;
                }
            }
            switch (alignment)
            {
                case AlignMode.Bottom: dy = (ac + dc - em) / (double)em; break;
                case AlignMode.Baseline: dy = -(em - ac) / (double)em; break;
                case AlignMode.Center: dy = (ac / 2 + dc / 2 - em) / (double)em; break;
                case AlignMode.Top: dy = -1.0; break;
            }
            lowerLeft = pls.PointAt(new GeoPoint2D(dx, dy - (totwidthArray.Count - 1) * lsp));
            lowerRight = pls.PointAt(new GeoPoint2D(dx + totwidthMax, dy - (totwidthArray.Count - 1) * lsp));
            upperLeft = pls.PointAt(new GeoPoint2D(dx, dy + 1.0));
            // Der Text wird mit Größe 1 zusammengesetzt und an (0,0) platziert, jedoch unter Berücksichtigung
            // der Alignments
            ModOp toWorld = ModOp.Translate(location.x, location.y, location.z) * ModOp.Fit(new GeoVector[] { GeoVector.XAxis, GeoVector.YAxis, GeoVector.ZAxis }, new GeoVector[] { lineDirection, glyphDirection, lineDirection ^ glyphDirection });
            int lfcount = 1; // der Zähler für mehrzeiligen Text
            // den ersten x-Wert setzen:
            dx = 0.0;
            if (lineAlignment != LineAlignMode.Left)
            {
                switch (lineAlignment)
                {
                    case LineAlignMode.Center: dx = -totwidthArray[0] / 2.0; break;
                    case LineAlignMode.Right: dx = -totwidthArray[0]; break;
                }
            }
            for (int i = 0; i < textString.Length; ++i)
            {
                double width;
                if (textString[i] == '\r') // linefeed ignorieren
                {
                    continue;
                }
                if (textString[i] == '\n') // carriage return, neue Zeile
                {
                    dy = dy - lsp; // eins runter im normierten Text
                    // den nächsten x-Wert setzen:
                    dx = 0.0;
                    if (lineAlignment != LineAlignMode.Left)
                    {
                        switch (lineAlignment)
                        {
                            case LineAlignMode.Center: dx = -totwidthArray[lfcount] / 2.0; break;
                            case LineAlignMode.Right: dx = -totwidthArray[lfcount]; break;
                        }
                        lfcount++;
                    }
                    continue;
                }
                Path2D[] path2d = FontCache.GlobalFontCache.GetOutline2D(fontName, fs, textString[i], out width);
                if (i > 0)
                {
                    double krn = FontCache.GlobalFontCache.GetKerning(fontName, fs, textString[i - 1], textString[i]);
                    dx += krn; // das ist negativ
                }
                if (path2d != null)
                {
                    for (int j = 0; j < path2d.Length; ++j)
                    {
                        IGeoObject go = path2d[j].MakeGeoObject(Plane.XYPlane);
                        go.Modify(toWorld * ModOp.Translate(dx, dy, 0));
                        res.Add(go);
                    }
                }
                dx += width;
            }
            return res;

            // alt:
            //GeoPoint2D ll, lr, ul;
            //Path2D[] paths2D = GetOutline2D(out ll, out lr, out ul);
            //ModOp m = ModOp.Fit(GeoPoint.Origin, new GeoVector[] { GeoVector.XAxis, GeoVector.YAxis, GeoVector.ZAxis }, location, new GeoVector[] { lineDirection, glyphDirection, lineDirection ^ glyphDirection });
            //GeoObjectList res = new GeoObjectList();
            //for (int i = 0; i < paths2D.Length; ++i)
            //{
            //    IGeoObject go = paths2D[i].MakeGeoObject(Plane.XYPlane);
            //    go.Modify(m);
            //    go.CopyAttributes(this);
            //    res.Add(go);
            //}
            //return res;
        }
        /// <summary>
        /// Returns the shapes of all characters of this text. The characters are appropriatly spaced, but the text size and glyphdirection is not taken into account
        /// </summary>
        /// <returns></returns>
        public CompoundShape[] GetShapes()
        {
            List<CompoundShape> res = new List<CompoundShape>();
            int fs = 0; // das ist regular
            if (Underline) fs |= (int)FontStyle.Underline;
            if (Italic) fs |= (int)FontStyle.Italic;
            if (Bold) fs |= (int)FontStyle.Bold;
            if (Strikeout) fs |= (int)FontStyle.Strikeout;
            PlaneSurface pls = new PlaneSurface(location, lineDirection, glyphDirection, lineDirection ^ glyphDirection);
            FontFamily ff;
            if (FontFamilyNames.Contains(fontName.ToUpper()))
            {
                ff = new FontFamily(fontName);
            }
            else
            {
                ff = new FontFamily(System.Drawing.Text.GenericFontFamilies.SansSerif);
            }
            if (!ff.IsStyleAvailable((FontStyle)fs))
            {
                if (ff.IsStyleAvailable(FontStyle.Regular)) fs = (int)FontStyle.Regular;
                else if (ff.IsStyleAvailable(FontStyle.Bold)) fs = (int)FontStyle.Bold;
                else if (ff.IsStyleAvailable(FontStyle.Italic)) fs = (int)FontStyle.Italic;
                else if (ff.IsStyleAvailable(FontStyle.Strikeout)) fs = (int)FontStyle.Strikeout;
                else if (ff.IsStyleAvailable(FontStyle.Underline)) fs = (int)FontStyle.Underline; // irgend einen muss es ja wohl geben, oder?
            }
            int em = ff.GetEmHeight((FontStyle)fs);
            int ls = ff.GetLineSpacing((FontStyle)fs);
            int dc = ff.GetCellDescent((FontStyle)fs);
            int ac = ff.GetCellAscent((FontStyle)fs);
            double lsp = ls / em;
            double dx = 0.0;
            double dy = 0.0;
            double totwidth = 0.0;
            double totwidthMax = 0.0;
            List<double> totwidthArray = new List<double>(); // Liste der Zeilenlängen bei mehrzeiligen Texten
            for (int i = 0; i < textString.Length; ++i)
            {
                double width;
                if (textString[i] == '\r') // linefeed ignorieren
                {
                    continue;
                }
                if (textString[i] == '\n') // carriage return
                {
                    totwidthArray.Add(totwidth); // Länge in die Liste
                    if (totwidth > totwidthMax) { totwidthMax = totwidth; } // Maximum merken für Extents
                    totwidth = 0.0;
                    continue;
                }
                FontCache.GlobalFontCache.Get(fontName, fs, textString[i], out width, null);
                totwidth += width;
                if (i > 0)
                {
                    double krn = FontCache.GlobalFontCache.GetKerning(fontName, fs, textString[i - 1], textString[i]);
                    totwidth += krn; // das ist negativ
                }
            }
            totwidthArray.Add(totwidth); // den letzten Wert dazu
            if (totwidth > totwidthMax) { totwidthMax = totwidth; }
            if (lineAlignment != LineAlignMode.Left) // Extremwert berechnen
            {
                switch (lineAlignment)
                {
                    case LineAlignMode.Center: dx = -totwidthMax / 2.0; break;
                    case LineAlignMode.Right: dx = -totwidthMax; break;
                }
            }
            switch (alignment)
            {
                case AlignMode.Bottom: dy = (ac + dc - em) / (double)em; break;
                case AlignMode.Baseline: dy = -(em - ac) / (double)em; break;
                case AlignMode.Center: dy = (ac / 2 + dc / 2 - em) / (double)em; break;
                case AlignMode.Top: dy = -1.0; break;
            }
            lowerLeft = pls.PointAt(new GeoPoint2D(dx, dy - (totwidthArray.Count - 1) * lsp));
            lowerRight = pls.PointAt(new GeoPoint2D(dx + totwidthMax, dy - (totwidthArray.Count - 1) * lsp));
            upperLeft = pls.PointAt(new GeoPoint2D(dx, dy + 1.0));
            // Der Text wird mit Größe 1 zusammengesetzt und an (0,0) platziert, jedoch unter Berücksichtigung
            // der Alignments
            ModOp toWorld = ModOp.Translate(location.x, location.y, location.z) * ModOp.Fit(new GeoVector[] { GeoVector.XAxis, GeoVector.YAxis, GeoVector.ZAxis }, new GeoVector[] { lineDirection, glyphDirection, lineDirection ^ glyphDirection });
            int lfcount = 1; // der Zähler für mehrzeiligen Text
            // den ersten x-Wert setzen:
            dx = 0.0;
            if (lineAlignment != LineAlignMode.Left)
            {
                switch (lineAlignment)
                {
                    case LineAlignMode.Center: dx = -totwidthArray[0] / 2.0; break;
                    case LineAlignMode.Right: dx = -totwidthArray[0]; break;
                }
            }
            for (int i = 0; i < textString.Length; ++i)
            {
                double width;
                if (textString[i] == '\r') // linefeed ignorieren
                {
                    continue;
                }
                if (textString[i] == '\n') // carriage return, neue Zeile
                {
                    dy = dy - lsp; // eins runter im normierten Text
                    // den nächsten x-Wert setzen:
                    dx = 0.0;
                    if (lineAlignment != LineAlignMode.Left)
                    {
                        switch (lineAlignment)
                        {
                            case LineAlignMode.Center: dx = -totwidthArray[lfcount] / 2.0; break;
                            case LineAlignMode.Right: dx = -totwidthArray[lfcount]; break;
                        }
                        lfcount++;
                    }
                    continue;
                }
                CompoundShape cs = FontCache.GlobalFontCache.Get(fontName, fs, textString[i], out width);
                res.Add(cs.GetModified(ModOp2D.Translate(dx, dy)));
                if (i > 0)
                {
                    double krn = FontCache.GlobalFontCache.GetKerning(fontName, fs, textString[i - 1], textString[i]);
                    dx += krn; // das ist negativ
                }
                dx += width;
            }
            return res.ToArray();
        }
        internal ModOp GlyphToWorld
        {
            get { return ModOp.Translate(location.x, location.y, location.z) * ModOp.Fit(new GeoVector[] { GeoVector.XAxis, GeoVector.YAxis, GeoVector.ZAxis }, new GeoVector[] { lineDirection, glyphDirection, lineDirection ^ glyphDirection }); }
        }
        public virtual GeoObjectList GetCenterline()
        {   // koopiert aus Getoutline!
            GeoObjectList res = new GeoObjectList();
            int fs = 0; // das ist regular
            if (Underline) fs |= (int)FontStyle.Underline;
            if (Italic) fs |= (int)FontStyle.Italic;
            if (Bold) fs |= (int)FontStyle.Bold;
            if (Strikeout) fs |= (int)FontStyle.Strikeout;
            PlaneSurface pls = new PlaneSurface(location, lineDirection, glyphDirection, lineDirection ^ glyphDirection);
            FontFamily ff;
            if (FontFamilyNames.Contains(fontName.ToUpper()))
            {
                ff = new FontFamily(fontName);
            }
            else
            {
                ff = new FontFamily(System.Drawing.Text.GenericFontFamilies.SansSerif);
            }
            if (!ff.IsStyleAvailable((FontStyle)fs))
            {
                if (ff.IsStyleAvailable(FontStyle.Regular)) fs = (int)FontStyle.Regular;
                else if (ff.IsStyleAvailable(FontStyle.Bold)) fs = (int)FontStyle.Bold;
                else if (ff.IsStyleAvailable(FontStyle.Italic)) fs = (int)FontStyle.Italic;
                else if (ff.IsStyleAvailable(FontStyle.Strikeout)) fs = (int)FontStyle.Strikeout;
                else if (ff.IsStyleAvailable(FontStyle.Underline)) fs = (int)FontStyle.Underline; // irgend einen muss es ja wohl geben, oder?
            }
            int em = ff.GetEmHeight((FontStyle)fs);
            int ls = ff.GetLineSpacing((FontStyle)fs);
            int dc = ff.GetCellDescent((FontStyle)fs);
            int ac = ff.GetCellAscent((FontStyle)fs);
            double lsp = ls / em;
            double dx = 0.0;
            double dy = 0.0;
            double totwidth = 0.0;
            double totwidthMax = 0.0;
            List<double> totwidthArray = new List<double>(); // Liste der Zeilenlängen bei mehrzeiligen Texten
            for (int i = 0; i < textString.Length; ++i)
            {
                double width;
                if (textString[i] == '\r') // linefeed ignorieren
                {
                    continue;
                }
                if (textString[i] == '\n') // carriage return
                {
                    totwidthArray.Add(totwidth); // Länge in die Liste
                    if (totwidth > totwidthMax) { totwidthMax = totwidth; } // Maximum merken für Extents
                    totwidth = 0.0;
                    continue;
                }
                FontCache.GlobalFontCache.GetCenterLines(fontName, fs, textString[i], out width);
                totwidth += width;
                if (i > 0)
                {
                    double krn = FontCache.GlobalFontCache.GetKerning(fontName, fs, textString[i - 1], textString[i]);
                    totwidth += krn; // das ist negativ
                }
            }
            totwidthArray.Add(totwidth); // den letzten Wert dazu
            if (totwidth > totwidthMax) { totwidthMax = totwidth; }
            if (lineAlignment != LineAlignMode.Left) // Extremwert berechnen
            {
                switch (lineAlignment)
                {
                    case LineAlignMode.Center: dx = -totwidthMax / 2.0; break;
                    case LineAlignMode.Right: dx = -totwidthMax; break;
                }
            }
            switch (alignment)
            {
                case AlignMode.Bottom: dy = (ac + dc - em) / (double)em; break;
                case AlignMode.Baseline: dy = -(em - ac) / (double)em; break;
                case AlignMode.Center: dy = (ac / 2 + dc / 2 - em) / (double)em; break;
                case AlignMode.Top: dy = -1.0; break;
            }
            lowerLeft = pls.PointAt(new GeoPoint2D(dx, dy - (totwidthArray.Count - 1) * lsp));
            lowerRight = pls.PointAt(new GeoPoint2D(dx + totwidthMax, dy - (totwidthArray.Count - 1) * lsp));
            upperLeft = pls.PointAt(new GeoPoint2D(dx, dy + 1.0));
            // Der Text wird mit Größe 1 zusammengesetzt und an (0,0) platziert, jedoch unter Berücksichtigung
            // der Alignments
            ModOp toWorld = ModOp.Translate(location.x, location.y, location.z) * ModOp.Fit(new GeoVector[] { GeoVector.XAxis, GeoVector.YAxis, GeoVector.ZAxis }, new GeoVector[] { lineDirection, glyphDirection, lineDirection ^ glyphDirection });
            int lfcount = 1; // der Zähler für mehrzeiligen Text
            // den ersten x-Wert setzen:
            dx = 0.0;
            if (lineAlignment != LineAlignMode.Left)
            {
                switch (lineAlignment)
                {
                    case LineAlignMode.Center: dx = -totwidthArray[0] / 2.0; break;
                    case LineAlignMode.Right: dx = -totwidthArray[0]; break;
                }
            }
            for (int i = 0; i < textString.Length; ++i)
            {
                double width;
                if (textString[i] == '\r') // linefeed ignorieren
                {
                    continue;
                }
                if (textString[i] == '\n') // carriage return, neue Zeile
                {
                    dy = dy - lsp; // eins runter im normierten Text
                    // den nächsten x-Wert setzen:
                    dx = 0.0;
                    if (lineAlignment != LineAlignMode.Left)
                    {
                        switch (lineAlignment)
                        {
                            case LineAlignMode.Center: dx = -totwidthArray[lfcount] / 2.0; break;
                            case LineAlignMode.Right: dx = -totwidthArray[lfcount]; break;
                        }
                        lfcount++;
                    }
                    continue;
                }
                GeoObjectList charlist = FontCache.GlobalFontCache.GetCenterLines(fontName, fs, textString[i], out width);
                if (i > 0)
                {
                    double krn = FontCache.GlobalFontCache.GetKerning(fontName, fs, textString[i - 1], textString[i]);
                    dx += krn; // das ist negativ
                }
                if (charlist != null)
                {
                    GeoObjectList toAdd = charlist.CloneObjects();
                    Block block = Block.Construct();
                    block.Set(toAdd);
                    block.Modify(toWorld * ModOp.Translate(dx, dy, 0));
                    res.Add(block);
                }
                dx += width;
            }
            return res;
        }

        //private Path2D[] GetOutline2D(out GeoPoint2D ll, out GeoPoint2D lr, out GeoPoint2D ul)
        //{
        //    GraphicsPath path = new GraphicsPath();
        //    FontFamily ff;
        //    try
        //    {
        //        ff = new FontFamily(fontName);
        //    }
        //    catch (System.ArgumentException ae)
        //    {
        //        ff = new FontFamily(System.Drawing.Text.GenericFontFamilies.SansSerif);
        //    }
        //    StringFormat sf = StringFormat.GenericTypographic.Clone() as StringFormat;
        //    sf.LineAlignment = StringAlignment.Far;
        //    sf.Alignment = StringAlignment.Near;
        //    int fs = 0; // das ist regular
        //    if (Underline) fs |= (int)FontStyle.Underline;
        //    if (Italic) fs |= (int)FontStyle.Italic;
        //    if (Bold) fs |= (int)FontStyle.Bold;
        //    if (Strikeout) fs |= (int)FontStyle.Strikeout;
        //    int em = ff.GetEmHeight((FontStyle)fs);

        //    path.AddString(textString, ff, fs, (float)(em), new PointF(0.0f, 0.0f), sf);
        //    List<Path2D> res = new List<Path2D>();
        //    ll = GeoPoint2D.Origin;
        //    lr = GeoPoint2D.Origin;
        //    ul = GeoPoint2D.Origin;
        //    if (path.PointCount > 0)
        //    {
        //        List<PointF> pp = new List<PointF>(path.PathPoints);
        //        List<byte> pt = new List<byte>(path.PathTypes);
        //        int last0 = -1;
        //        for (int i = 0; i < pt.Count; ++i)
        //        {
        //            pp[i] = new PointF(pp[i].X, (float)(1.0f - pp[i].Y));
        //            if ((pt[i] & 0x01) == 0) last0 = i;
        //            if ((pt[i] & 0x80) != 0 && last0 >= 0)
        //            {
        //                pt[i] = (byte)(pt[i] & 0x7F);
        //                pt.Insert(i + 1, (byte)(pt[last0] | 0x81));
        //                pp.Insert(i + 1, pp[last0]);
        //                ++i;
        //                last0 = -1;
        //            }

        //        }
        //        if (pp == null || pp.Count == 0)
        //        {
        //            return res.ToArray();
        //        }
        //        List<GeoPoint2D> current = new List<GeoPoint2D>();
        //        int mode = 0; // 0 noch nicht bekannt, 1: Linie, 3 Spline
        //        GeoPoint2D startPoint = GeoPoint2D.Origin;
        //        List<ICurve2D> segment = new List<ICurve2D>();
        //        bool close = false;
        //        for (int i = 0; i < pp.Count; ++i)
        //        {
        //            switch (pt[i] & 0x03)
        //            {
        //                case 0: // neuer Anfang
        //                    if (current.Count > 1)
        //                    {
        //                        AddToPath2D(segment, current, mode == 3, close);
        //                        res.Add(new Path2D(segment.ToArray()));
        //                    }
        //                    segment.Clear();
        //                    current.Clear();
        //                    current.Add(new GeoPoint2D(pp[i]));
        //                    mode = 0;
        //                    break;
        //                case 1:
        //                    if (mode == 3)
        //                    {   // Spline beenden, polylinie anfangen
        //                        AddToPath2D(segment, current, true, false);
        //                    }
        //                    current.Add(new GeoPoint2D(pp[i]));
        //                    mode = 1;
        //                    break;
        //                case 2:
        //                case 3:
        //                    if (mode == 1)
        //                    {
        //                        AddToPath2D(segment, current, false, false);
        //                    }
        //                    current.Add(new GeoPoint2D(pp[i]));
        //                    mode = 3;
        //                    break;
        //            }
        //            close = (pt[i] & 0x80) != 0;
        //        }

        //        if (current.Count > 1)
        //        {
        //            AddToPath2D(segment, current, mode == 3, close);
        //            res.Add(new Path2D(segment.ToArray()));
        //        }
        //    }
        //    BoundingRect ext = BoundingRect.EmptyBoundingRect;
        //    ext.MinMax(GeoPoint2D.Origin); // Ursprung mit einbezogen, ist das OK?
        //    for (int i = 0; i < res.Count; ++i)
        //    {
        //        ext.MinMax(res[i].GetExtent());
        //    }
        //    ll = ext.GetLowerLeft();
        //    lr = ext.GetLowerRight();
        //    ul = ext.GetUpperLeft();
        //    ModOp2D m = ModOp2D.Scale(1.0 / em);
        //    if (this.lineAlignment != LineAlignMode.Left || this.alignment != AlignMode.Bottom)
        //    {
        //        int ls = ff.GetLineSpacing((FontStyle)fs);
        //        int dc = ff.GetCellDescent((FontStyle)fs);
        //        double dx = 0.0;
        //        double dy = 0.0;
        //        switch (lineAlignment)
        //        {
        //            case LineAlignMode.Center: dx = -ext.Width / 2.0; break;
        //            case LineAlignMode.Right: dx = -ext.Width; break;
        //        }
        //        switch (alignment)
        //        {
        //            case AlignMode.Baseline: dy = -dc; break;
        //            case AlignMode.Center: dy = -em / 2.0; break;
        //            case AlignMode.Top: dy = -ls; break;
        //        }
        //        for (int i = 0; i < res.Count; ++i)
        //        {
        //            res[i].Move(dx, dy);
        //        }
        //        ll += new GeoVector2D(dx, dy);
        //        lr += new GeoVector2D(dx, dy);
        //        ul += new GeoVector2D(dx, dy);
        //    }
        //    for (int i = 0; i < res.Count; ++i)
        //    {
        //        res[i] = (res[i] as ICurve2D).GetModified(m) as Path2D;
        //    }
        //    ll = m * ll;
        //    lr = m * lr;
        //    ul = m * ul;
        //    return res.ToArray();
        //}
        //internal IGeoObject[] CachedDisplayItems
        //{
        //    get
        //    {
        //        if (cachedDisplayItems == null)
        //        {
        //            int fs = 0; // das ist regular
        //            if (Underline) fs |= (int)FontStyle.Underline;
        //            if (Italic) fs |= (int)FontStyle.Italic;
        //            if (Bold) fs |= (int)FontStyle.Bold;
        //            if (Strikeout) fs |= (int)FontStyle.Strikeout;
        //            List<IGeoObject> res = new List<IGeoObject>();
        //            PlaneSurface pls = new PlaneSurface(location, lineDirection, glyphDirection, lineDirection ^ glyphDirection);
        //            FontFamily ff;
        //            try
        //            {
        //                ff = new FontFamily(fontName);
        //            }
        //            catch (System.ArgumentException)
        //            {
        //                ff = new FontFamily(System.Drawing.Text.GenericFontFamilies.SansSerif);
        //            }
        //            int em = ff.GetEmHeight((FontStyle)fs);
        //            int ls = ff.GetLineSpacing((FontStyle)fs);
        //            int dc = ff.GetCellDescent((FontStyle)fs);
        //            int ac = ff.GetCellAscent((FontStyle)fs);
        //            double dx = 0.0;
        //            double dy = 0.0;
        //            double totwidth = 0.0;
        //            for (int i = 0; i < textString.Length; ++i)
        //            {
        //                double width;
        //                FontCache.GlobalFontCache.Get(fontName, fs, textString[i], out width);
        //                totwidth += width;
        //                if (i > 0)
        //                {
        //                    double krn = FontCache.GlobalFontCache.GetKerning(fontName, fs, textString[i - 1], textString[i]);
        //                    totwidth += krn; // das ist negativ
        //                }
        //            }
        //            if (lineAlignment != LineAlignMode.Left)
        //            {
        //                switch (lineAlignment)
        //                {
        //                    case LineAlignMode.Center: dx = -totwidth / 2.0; break;
        //                    case LineAlignMode.Right: dx = -totwidth; break;
        //                }
        //            }
        //            switch (alignment)
        //            {
        //                case AlignMode.Bottom: dy = (ac + dc - em) / (double)em; break;
        //                case AlignMode.Baseline: dy = -(em - ac) / (double)em; break;
        //                case AlignMode.Center: dy = (ac/2 + dc/2 - em ) / (double)em; break;
        //                case AlignMode.Top: dy = -1.0; break;
        //            }
        //            lowerLeft = pls.PointAt(new GeoPoint2D(dx, dy));
        //            lowerRight = pls.PointAt(new GeoPoint2D(dx+totwidth, dy));
        //            upperLeft = pls.PointAt(new GeoPoint2D(dx, dy + 1.0));
        //            // Der Text wird mit Größe 1 zusammengesetzt und an (0,0) platziert, jedoch unter Berücksichtigung
        //            // der Alignments
        //            for (int i = 0; i < textString.Length; ++i)
        //            {
        //                double width;
        //                CompoundShape cs = FontCache.GlobalFontCache.Get(fontName, fs, textString[i], out width).Clone();
        //                if (i > 0)
        //                {
        //                    double krn = FontCache.GlobalFontCache.GetKerning(fontName, fs, textString[i - 1], textString[i]);
        //                    dx += krn; // das ist negativ
        //                }
        //                cs.Move(dx, dy);
        //                for (int j = 0; j < cs.SimpleShapes.Length; ++j)
        //                {
        //                    if (displayAsPath)
        //                    {
        //                        Border bdr = cs.SimpleShapes[j].Outline;
        //                        for (int k = 0; k < bdr.Count; ++k)
        //                        {
        //                            IGeoObject go = pls.Make3dCurve(bdr.Segments[k]) as IGeoObject;
        //                            if (go != null)
        //                            {
        //                                res.Add(go);
        //                                go.CopyAttributes(this); // Farbe, Layer
        //                            }
        //                        }
        //                        for (int l = 0; l < cs.SimpleShapes[j].NumHoles; ++l)
        //                        {
        //                            bdr = cs.SimpleShapes[j].Hole(l);
        //                            for (int k = 0; k < bdr.Count; ++k)
        //                            {
        //                                IGeoObject go = pls.Make3dCurve(bdr.Segments[k]) as IGeoObject;
        //                                if (go != null)
        //                                {
        //                                    res.Add(go);
        //                                    go.CopyAttributes(this); // Farbe, Layer
        //                                }
        //                            }
        //                        }
        //                    }
        //                    else
        //                    {
        //                        Face fc = Face.MakeFace(pls, cs.SimpleShapes[j]);
        //                        fc.CopyAttributes(this); // Farbe, Layer
        //                        res.Add(fc);
        //                    }
        //                }
        //                dx += width;
        //            }
        //            cachedDisplayItems = res.ToArray();
        //        }
        //        return cachedDisplayItems;
        //    }
        //}
        private void CalcExtent()
        {
#if DEBUG
#endif
            if (!isValidExtent)
            {
                int fs = 0; // das ist regular
                if (Underline) fs |= (int)FontStyle.Underline;
                if (Italic) fs |= (int)FontStyle.Italic;
                if (Bold) fs |= (int)FontStyle.Bold;
                if (Strikeout) fs |= (int)FontStyle.Strikeout;
                List<IGeoObject> res = new List<IGeoObject>();
                PlaneSurface pls = null;
                try
                {
                    pls = new PlaneSurface(location, lineDirection, glyphDirection, lineDirection ^ glyphDirection);
                }
                catch (ModOpException)
                {   // bei sehr kleinen lineDirection u.s.w. gibts hier ein Problem
                    pls = new PlaneSurface(location, GeoVector.XAxis, GeoVector.YAxis, GeoVector.ZAxis);
                }
                FontFamily ff;
                if (FontFamilyNames.Contains(fontName.ToUpper()))
                {
                    ff = new FontFamily(fontName);
                }
                else
                {
                    ff = new FontFamily(System.Drawing.Text.GenericFontFamilies.SansSerif);
                }
                if (!ff.IsStyleAvailable((FontStyle)fs))
                {
                    if (ff.IsStyleAvailable(FontStyle.Regular)) fs = (int)FontStyle.Regular;
                    else if (ff.IsStyleAvailable(FontStyle.Bold)) fs = (int)FontStyle.Bold;
                    else if (ff.IsStyleAvailable(FontStyle.Italic)) fs = (int)FontStyle.Italic;
                    else if (ff.IsStyleAvailable(FontStyle.Strikeout)) fs = (int)FontStyle.Strikeout;
                    else if (ff.IsStyleAvailable(FontStyle.Underline)) fs = (int)FontStyle.Underline; // irgend einen muss es ja wohl geben, oder?
                }
                int em = ff.GetEmHeight((FontStyle)fs);
                int ls = ff.GetLineSpacing((FontStyle)fs);
                int dc = ff.GetCellDescent((FontStyle)fs);
                int ac = ff.GetCellAscent((FontStyle)fs);
                double lsp;
                if (em > 0) lsp = (double)ls / em;
                else lsp = 1.0;
                double dx = 0.0;
                double dy = 0.0;
                double totwidth = 0.0;
                double totwidthMax = 0.0;
                List<double> totwidthArray = new List<double>(); // Liste der Zeilenlängen bei mehrzeiligen Texten
                if (textString == null) textString = "";
                for (int i = 0; i < textString.Length; ++i)
                {
                    double width;
                    if (textString[i] == '\r') // linefeed ignorieren
                    {
                        continue;
                    }
                    if (textString[i] == '\n') // carriage return
                    {
                        totwidthArray.Add(totwidth); // Länge in die Liste
                        if (totwidth > totwidthMax) { totwidthMax = totwidth; } // Maximum merken für Extents
                        totwidth = 0.0;
                        continue;
                    }
                    FontCache.GlobalFontCache.Get(fontName, fs, textString[i], out width, null);
                    totwidth += width;
                    if (i > 0)
                    {
                        double krn = FontCache.GlobalFontCache.GetKerning(fontName, fs, textString[i - 1], textString[i]);
                        totwidth += krn; // das ist negativ
                    }
                }
                totwidthArray.Add(totwidth); // den letzten Wert dazu
                if (totwidth > totwidthMax) { totwidthMax = totwidth; }
                if (lineAlignment != LineAlignMode.Left) // Extremwert berechnen
                {
                    switch (lineAlignment)
                    {
                        case LineAlignMode.Center: dx = -totwidthMax / 2.0; break;
                        case LineAlignMode.Right: dx = -totwidthMax; break;
                    }
                }
                switch (alignment)
                {
                    case AlignMode.Bottom: dy = (ac + dc - em) / (double)em; break;
                    case AlignMode.Baseline: dy = -(em - ac) / (double)em; break;
                    case AlignMode.Center: dy = (ac / 2 + dc / 2 - em) / (double)em; break; // em / 2 statt em
                    case AlignMode.Top: dy = -1.0; break;
                        //case AlignMode.Bottom: dy = 0.0; break;
                        //case AlignMode.Baseline: dy = -ac / (double)(ac+dc); break;
                        //case AlignMode.Center: dy = -0.5; break;
                        //case AlignMode.Top: dy = -1.0; break;
                }
                lowerLeft = pls.PointAt(new GeoPoint2D(dx, dy - (totwidthArray.Count - 1) * lsp));
                lowerRight = pls.PointAt(new GeoPoint2D(dx + totwidthMax, dy - (totwidthArray.Count - 1) * lsp));
                upperLeft = pls.PointAt(new GeoPoint2D(dx, dy + 1.0));
                isValidExtent = true;
            }
        }
        #region Properties
        private new class Changing : IGeoObjectImpl.Changing
        {
            private Text text;
            public Changing(Text text)
                : base(text, "CopyGeometry", text.Clone())
            {
                text.InvalidateSecondaryData();
                this.text = text;
            }
            public Changing(Text text, string PropertyName)
                : base(text, PropertyName)
            {
                text.InvalidateSecondaryData();
                this.text = text;
            }
            public Changing(Text text, string MethodOrPropertyName, params object[] Parameters)
                : base(text, MethodOrPropertyName, Parameters)
            {
                text.InvalidateSecondaryData();
                this.text = text;
            }
            public Changing(Text text, Type interfaceForMethod, string MethodOrPropertyName, params object[] Parameters)
                : base(text, interfaceForMethod, MethodOrPropertyName, Parameters)
            {
                text.InvalidateSecondaryData();
                this.text = text;
            }
            public override void Dispose()
            {
                base.Dispose();
            }

        }
        /// <summary>
        /// Sets or gets the horizontal (or in direction of <see cref="LineDirection"/>) alignement of the text.
        /// </summary>
        public AlignMode Alignment
        {
            get { return alignment; }
            set
            {
                using (new Changing(this, "Alignment"))
                {
                    alignment = value;
                    isValidExtent = false;
                }
            }
        }
        /// <summary>
        /// Sets or gets the vertical alignement of the text.
        /// </summary>
        public LineAlignMode LineAlignment
        {
            get { return lineAlignment; }
            set
            {
                using (new Changing(this, "LineAlignment"))
                {
                    lineAlignment = value;
                    isValidExtent = false;
                }
            }
        }
        /// <summary>
        /// Gets or sets the line direction of the text. Default is <see cref="GeoVector.XAxis"/>. Setting the line
        /// direction does not modify the <see cref="GlyphDirection"/> of the text, so you usually will have to change both.
        /// It also affects the aspect ratio of the text, which is defined by the length of the line direction
        /// and the length of the glyph direction. To simply rotate a text better use <see cref="IGeoObject.Modify"/>
        /// with a <see cref="ModOp.Rotate"/> rotation.
        /// </summary>
        public GeoVector LineDirection
        {
            get { return lineDirection; }
            set
            {
                // siehe Location!
                using (new Changing(this, "LineDirection"))
                {
                    if (value != lineDirection)
                    {
                        lineDirection = value;
                        isValidExtent = false;
                    }
                }
            }
        }

        public void SetLineDirKeepGlyphAngle(GeoVector ld)
        {
            if (ld.IsNullVector())
                return;

            using (new Changing(this))
            {
                Angle a = GlyphAngle;
                Plane pln = Plane;
                ld.Length = LineDirection.Length;
                lineDirection = ld;
                double len = glyphDirection.Length;
                glyphDirection = ModOp.Rotate(pln.Normal, new SweepAngle(a)) * lineDirection;
                glyphDirection.Length = len;
                isValidExtent = false;
            }
        }

        /// <summary>
        /// Gets or sets the direction of the vertical character parts. This direction is usually perpendicular to the
        /// <see cref="LineDirection"/>, but can be modified to produce italic characters of any angle.
        /// The <see cref="Italic"/> property in contrary uses the typefont italic capabilities which are more sophisticated.
        /// </summary>
        public GeoVector GlyphDirection
        {
            get { return glyphDirection; }
            set
            {
                using (new Changing(this, "GlyphDirection")) // muss vorher kommen, damit das Undo System mitspielt
                {
                    if (value != glyphDirection && !Precision.SameDirection(lineDirection, value, false) && !value.IsNullVector())
                    {
                        Plane pln = Plane;
                        glyphDirection = value;
                        Plane plnnew = Plane;
                        if (plnnew.Normal * pln.Normal < 0.0) isReflected = !isReflected;
                        isValidExtent = false;
                    }
                }
            }
        }
        public void SetDirections(GeoVector lineDirection, GeoVector glyphDirection)
        {
            using (new Changing(this))
            {
                this.glyphDirection = glyphDirection;
                this.lineDirection = lineDirection;
            }
        }
        public GeoVector GlyphDirectionKeepSize
        {
            get { return glyphDirection; }
            set
            {
                using (new Changing(this, "GlyphDirectionKeepSize")) // muss vorher kommen, damit das Undo System mitspielt
                {
                    if (value != glyphDirection && !Precision.SameDirection(lineDirection, value, false) && !value.IsNullVector())
                    {
                        double len = glyphDirection.Length;
                        glyphDirection = value;
                        glyphDirection.Length = len;
                        isValidExtent = false;
                    }
                }
            }
        }
        /// <summary>
        /// Gets or sets the location of the text. The value of this point is interpreted according to the values of the
        /// <see cref="Alignment"/>and <see cref="LineAlignment"/> properties.
        /// </summary>
        public GeoPoint Location
        {
            get { return location; }
            set
            {   // wir dürfen hier nicht auf Verschiedenheit prüfen, denn folgendes Szenario funktioniert sonst nicht:
                // Verschiebe einen Text mit Location-Hotspot und eingeschaltetem Raster. 
                // Hierbei ist oft location==vlaue und deshalb gibt es kein changing. Aber wir brauchen das Changing für die UndoListe
                // sonst gibt es keinen Eintrag (während continuouschange gibt es nämlich keinen Eintrag)
                // Das Changing kommt jetzt außerhalb, damit ist sicher gestellt, dass das UndoSystem seinen ersten Eintrag bekommt, auch wenn (wg. Raster)
                // nichts verändert wird
                using (new Changing(this, "Location"))
                {
                    if (location != value)
                    {
                        location = value;
                        isValidExtent = false;
                    }
                }
            }
        }
        /// <summary>
        /// Gets or sets the bold property of the text font.
        /// </summary>
        public bool Bold
        {
            get { return (fontStyle & FontStyle.Bold) != 0; }
            set
            {
                if ((value && (fontStyle & FontStyle.Bold) == 0) || (!value && (fontStyle & FontStyle.Bold) != 0))
                {
                    using (new Changing(this, "Bold"))
                    {
                        if (value)
                            fontStyle |= FontStyle.Bold;
                        else
                            fontStyle ^= FontStyle.Bold;
                        isValidExtent = false;
                    }
                }
            }
        }
        /// <summary>
        /// Gets or sets the italic property of the text font.
        /// </summary>
        public bool Italic
        {
            get { return (fontStyle & FontStyle.Italic) != 0; }
            set
            {
                if ((value && (fontStyle & FontStyle.Italic) == 0) || (!value && (fontStyle & FontStyle.Italic) != 0))
                {
                    using (new Changing(this, "Italic"))
                    {
                        if (value)
                            fontStyle |= FontStyle.Italic;
                        else
                            fontStyle ^= FontStyle.Italic;
                        isValidExtent = false;
                    }
                }
            }
        }
        /// <summary>
        /// Gets or sets the underline property of the text font.
        /// </summary>
        public bool Underline
        {
            get { return (fontStyle & FontStyle.Underline) != 0; }
            set
            {
                if ((value && (fontStyle & FontStyle.Underline) == 0) || (!value && (fontStyle & FontStyle.Underline) != 0))
                {
                    using (new Changing(this, "Underline"))
                    {
                        if (value)
                            fontStyle |= FontStyle.Underline;
                        else
                            fontStyle ^= FontStyle.Underline;
                        isValidExtent = false;
                    }
                }
            }
        }
        /// <summary>
        /// Gets or sets the strikeout property of the text font.
        /// </summary>
        public bool Strikeout
        {
            get { return (fontStyle & FontStyle.Strikeout) != 0; }
            set
            {
                if ((value && (fontStyle & FontStyle.Strikeout) == 0) || (!value && (fontStyle & FontStyle.Strikeout) != 0))
                {
                    using (new Changing(this, "Strikeout"))
                    {
                        if (value)
                            fontStyle |= FontStyle.Strikeout;
                        else
                            fontStyle ^= FontStyle.Strikeout;
                        isValidExtent = false;
                    }
                }
            }
        }
        /// <summary>
        /// Gets or sets the text string that is displayed with this object.
        /// </summary>
        public string TextString
        {
            get { return textString; }
            set
            {
                if (value != textString)
                {
                    using (new Changing(this, "TextString"))
                    {
                        textString = value;
                        isValidExtent = false;
                    }
                }
            }
        }
        /// <summary>
        /// Gets or sets the font used for this text object
        /// </summary>
        public string Font
        {
            get { return fontName; }
            set
            {
                if (value != fontName)
                {
                    using (new Changing(this, "Font"))
                    {
                        fontName = value;
                        isValidExtent = false;
                    }
                }
            }
        }
        /// <summary>
        /// Gets or sets the size of this text object.
        /// </summary>
        public double TextSize
        {
            get { return glyphDirection.Length; }
            set
            {
                // siehe Location!
                using (new Changing(this, "TextSize"))
                {
                    if (value != glyphDirection.Length && value > 0.0)
                    {
                        double f = glyphDirection.Length / lineDirection.Length;
                        glyphDirection.Length = value;
                        lineDirection.Length = value / f;
                        isValidExtent = false;
                    }
                }
            }
        }
        /// <summary>
        /// Gets or sets the angle of the characters vertical direction in respect to the horizontal direction.
        /// The default value is 90°, different angles create different italic fonts.
        /// </summary>
        public Angle GlyphAngle
        {
            get
            {
                if (isReflected) return -(new Angle(lineDirection, glyphDirection));
                else return new Angle(lineDirection, glyphDirection);
            }
            set
            {
                if (Math.Abs(value.Degree) < 1 || Math.Abs(180 - value.Degree) < 1) return; // no 1° or less GlyphAngle, because the Plane would become instable
                using (new Changing(this, "GlyphAngle"))
                {
                    Plane pln = Plane;
                    double len = glyphDirection.Length;
                    glyphDirection = ModOp.Rotate(pln.Normal, new SweepAngle(value)) * lineDirection;
                    glyphDirection.Length = len;
                    isReflected = value < 0 || value > Math.PI;
                    isValidExtent = false;
                }
            }
        }
        internal double Length
        {
            get
            {
                //calcExtent();
                //return sizeExtent.Width;
                CalcExtent();
                return lowerLeft | lowerRight;
            }
        }
        internal void Set(GeoVector lineDirection, GeoVector glyphDirection, GeoPoint location, string fontName, string textString, FontStyle fontStyle, AlignMode alignment, LineAlignMode lineAlignment)
        {   // ohne changing, nur intern benutzen
            this.lineDirection = lineDirection;
            this.glyphDirection = glyphDirection;
            this.location = location;
            this.fontName = fontName;
            this.textString = textString;
            this.fontStyle = fontStyle;
            this.alignment = alignment;
            this.lineAlignment = lineAlignment;
            isValidExtent = false;
        }
        internal GeoPoint[] FourPoints
        {
            get
            {
                CalcExtent();
                return new GeoPoint[] { lowerLeft, lowerRight, lowerRight + (upperLeft - lowerLeft), upperLeft };
            }
        }
        internal Plane Plane
        {
            get
            {
                try
                {
                    if (isReflected) return new Plane(location, lineDirection, -glyphDirection);
                    else return new Plane(location, lineDirection, glyphDirection);
                }
                catch (PlaneException)
                {
                    return Plane.XYPlane;
                }
            }
        }
        #endregion
        #region Overrides IGeoObjectImpl
        /// <summary>
        /// Overrides <see cref="CADability.GeoObject.IGeoObjectImpl.Clone ()"/>
        /// </summary>
        /// <returns></returns>
        public override IGeoObject Clone()
        {
            Text res = Construct();
            res.location = location;
            res.lineDirection = lineDirection;
            res.glyphDirection = glyphDirection;
            res.fontName = fontName;
            res.textString = textString;
            res.fontStyle = fontStyle;
            res.alignment = alignment;
            res.lineAlignment = lineAlignment;
            res.isValidExtent = false; // sonst muss man die anderen Daten auch noch kopieren
            res.sizeExtent = sizeExtent;
            res.emSizeFactor = emSizeFactor;
            res.CopyAttributes(this);
            return res;
        }
        /// <summary>
        /// Overrides <see cref="CADability.GeoObject.IGeoObjectImpl.CopyGeometry (IGeoObject)"/>
        /// </summary>
        /// <param name="ToCopyFrom"></param>
        public override void CopyGeometry(IGeoObject ToCopyFrom)
        {
            using (new Changing(this))
            {
                Text txt = ToCopyFrom as Text;
                location = txt.location;
                lineDirection = txt.lineDirection;
                glyphDirection = txt.glyphDirection;
                fontName = txt.fontName;
                textString = txt.textString;
                fontStyle = txt.fontStyle;
                alignment = txt.alignment;
                lineAlignment = txt.lineAlignment;
                isValidExtent = txt.isValidExtent;
                sizeExtent = txt.sizeExtent;
                emSizeFactor = txt.emSizeFactor;
            }
        }
        /// <summary>
        /// Overrides <see cref="CADability.GeoObject.IGeoObjectImpl.Modify (ModOp)"/>
        /// </summary>
        /// <param name="m"></param>
        public override void Modify(ModOp m)
        {
            using (new Changing(this, "ModifyInverse", m))
            {
                location = m * location;
                lineDirection = m * lineDirection;
                glyphDirection = m * glyphDirection;
            }
        }
        /// <summary>
        /// Overrides <see cref="CADability.GeoObject.IGeoObjectImpl.GetShowProperties (IFrame)"/>
        /// </summary>
        /// <param name="Frame"></param>
        /// <returns></returns>
        public override IPropertyEntry GetShowProperties(IFrame Frame)
        {
            return new ShowPropertyText(this, Frame);
        }
        /// <summary>
        /// Overrides <see cref="CADability.GeoObject.IGeoObjectImpl.GetAttributeProperties (IFrame)"/>
        /// </summary>
        /// <param name="Frame"></param>
        /// <returns></returns>
        public override IPropertyEntry[] GetAttributeProperties(IFrame Frame)
        {
            List<IPropertyEntry> res = new List<IPropertyEntry>();
            ShowPropertyText spt = new ShowPropertyText(this, Frame);
            // res.AddRange(spt.GetAdditionalTextProperties());
            res.AddRange(base.GetAttributeProperties(Frame));
            return res.ToArray();
        }
        /// <summary>
        /// Overrides <see cref="CADability.GeoObject.IGeoObjectImpl.GetExtent (Projection, ExtentPrecision)"/>
        /// </summary>
        /// <param name="projection"></param>
        /// <param name="extentPrecision"></param>
        /// <returns></returns>
        public override BoundingRect GetExtent(Projection projection, ExtentPrecision extentPrecision)
        {
            BoundingRect res = BoundingRect.EmptyBoundingRect;
            CalcExtent();
            res.MinMax(projection.ProjectUnscaled(location));
            res.MinMax(projection.ProjectUnscaled(lowerLeft));
            res.MinMax(projection.ProjectUnscaled(lowerRight));
            res.MinMax(projection.ProjectUnscaled(upperLeft));
            res.MinMax(projection.ProjectUnscaled(lowerRight + (upperLeft - lowerLeft)));
            return res;
        }
        /// <summary>
        /// Overrides <see cref="CADability.GeoObject.IGeoObjectImpl.GetBoundingCube ()"/>
        /// </summary>
        /// <returns></returns>
        public override BoundingCube GetBoundingCube()
        {
            BoundingCube res = BoundingCube.EmptyBoundingCube;
            CalcExtent();
            res.MinMax(location);
            res.MinMax(lowerLeft);
            res.MinMax(lowerRight);
            res.MinMax(upperLeft);
            res.MinMax(lowerRight + (upperLeft - lowerLeft));
            return res;
        }
        /// <summary>
        /// Overrides <see cref="CADability.GeoObject.IGeoObjectImpl.PrePaintTo3D (IPaintTo3D)"/>
        /// </summary>
        /// <param name="paintTo3D"></param>
        public override void PrePaintTo3D(IPaintTo3D paintTo3D)
        {
            if (!paintTo3D.IsBitmap)
            {
                if (paintTo3D.TriangulateText)
                {   // generates a display list for each character and saves the display list in a cache
                    int fs = 0; // this is regular
                    if (Underline) fs |= (int)FontStyle.Underline;
                    if (Italic) fs |= (int)FontStyle.Italic;
                    if (Bold) fs |= (int)FontStyle.Bold;
                    if (Strikeout) fs |= (int)FontStyle.Strikeout;
                    List<IGeoObject> res = new List<IGeoObject>();
                    PlaneSurface pls = new PlaneSurface(location, lineDirection, glyphDirection, lineDirection ^ glyphDirection);
                    FontFamily ff;
                    if (FontFamilyNames.Contains(fontName.ToUpper()))
                    {
                        ff = new FontFamily(fontName);
                    }
                    else
                    {
                        ff = new FontFamily(System.Drawing.Text.GenericFontFamilies.SansSerif);
                    }
                    if (!ff.IsStyleAvailable((FontStyle)fs))
                    {
                        if (ff.IsStyleAvailable(FontStyle.Regular)) fs = (int)FontStyle.Regular;
                        else if (ff.IsStyleAvailable(FontStyle.Bold)) fs = (int)FontStyle.Bold;
                        else if (ff.IsStyleAvailable(FontStyle.Italic)) fs = (int)FontStyle.Italic;
                        else if (ff.IsStyleAvailable(FontStyle.Strikeout)) fs = (int)FontStyle.Strikeout;
                        else if (ff.IsStyleAvailable(FontStyle.Underline)) fs = (int)FontStyle.Underline; // there must be one
                    }
                    int em = ff.GetEmHeight((FontStyle)fs);
                    int ls = ff.GetLineSpacing((FontStyle)fs);
                    int dc = ff.GetCellDescent((FontStyle)fs);
                    int ac = ff.GetCellAscent((FontStyle)fs);
                    for (int i = 0; i < textString.Length; ++i)
                    {
                        double width;
                        FontCache.GlobalFontCache.Get(fontName, fs, textString[i], out width, paintTo3D);
                    }
                }
                else
                {
#if WEBASSEMBLY
                    paintTo3D.PrepareText(fontName, textString, CADability.WebDrawing.FontStyle.Regular);
#else
                    paintTo3D.PrepareText(fontName, textString, fontStyle);
#endif
                }
            }
        }
        public delegate bool PaintTo3DDelegate(Text toPaint, IPaintTo3D paintTo3D);
        public static PaintTo3DDelegate OnPaintTo3D;
        /// <summary>
        /// Overrides <see cref="CADability.GeoObject.IGeoObjectImpl.PaintTo3DList (IPaintTo3D, ICategorizedDislayLists)"/>
        /// </summary>
        /// <param name="paintTo3D"></param>
        /// <param name="lists"></param>
        public override void PaintTo3DList(IPaintTo3D paintTo3D, ICategorizedDislayLists lists)
        {
            if (paintTo3D.TriangulateText)
            {
                lists.Add(Layer, true, false, this); // nur Faces, wenn auch linienartige Schriften kommen, dann ist blöd
            }
            else
            {
                base.PaintTo3DList(paintTo3D, lists);
            }
        }
        /// <summary>
        /// Overrides <see cref="CADability.GeoObject.IGeoObjectImpl.PaintTo3D (IPaintTo3D)"/>
        /// </summary>
        /// <param name="paintTo3D"></param>
        public override void PaintTo3D(IPaintTo3D paintTo3D)
        {
            if (OnPaintTo3D != null && OnPaintTo3D(this, paintTo3D)) return;
            if (!paintTo3D.SelectMode)
            {
                if (colorDef != null) paintTo3D.SetColor(colorDef.Color);
            }
            if (paintTo3D.TriangulateText)
            {
                int fs = 0; // das ist regular
                if (Underline) fs |= (int)FontStyle.Underline;
                if (Italic) fs |= (int)FontStyle.Italic;
                if (Bold) fs |= (int)FontStyle.Bold;
                if (Strikeout) fs |= (int)FontStyle.Strikeout;
                List<IGeoObject> res = new List<IGeoObject>();
                PlaneSurface pls = new PlaneSurface(location, lineDirection, glyphDirection, lineDirection ^ glyphDirection);
                FontFamily ff;
                if (FontFamilyNames.Contains(fontName.ToUpper()))
                {
                    ff = new FontFamily(fontName);
                }
                else
                {
                    ff = new FontFamily(System.Drawing.Text.GenericFontFamilies.SansSerif);
                }
                if (!ff.IsStyleAvailable((FontStyle)fs))
                {
                    if (ff.IsStyleAvailable(FontStyle.Regular)) fs = (int)FontStyle.Regular;
                    else if (ff.IsStyleAvailable(FontStyle.Bold)) fs = (int)FontStyle.Bold;
                    else if (ff.IsStyleAvailable(FontStyle.Italic)) fs = (int)FontStyle.Italic;
                    else if (ff.IsStyleAvailable(FontStyle.Strikeout)) fs = (int)FontStyle.Strikeout;
                    else if (ff.IsStyleAvailable(FontStyle.Underline)) fs = (int)FontStyle.Underline; // irgend einen muss es ja wohl geben, oder?
                }
                int em = ff.GetEmHeight((FontStyle)fs);
                int ls = ff.GetLineSpacing((FontStyle)fs);
                int dc = ff.GetCellDescent((FontStyle)fs);
                int ac = ff.GetCellAscent((FontStyle)fs);
                double lsp = ls / em;
                double dx = 0.0;
                double dy = 0.0;
                double totwidth = 0.0;
                double totwidthMax = 0.0;
                List<double> totwidthArray = new List<double>(); // Liste der Zeilenlängen bei mehrzeiligen Texten
                for (int i = 0; i < textString.Length; ++i)
                {
                    double width;
                    if (textString[i] == '\r') // linefeed ignorieren
                    {
                        continue;
                    }
                    if (textString[i] == '\n') // carriage return
                    {
                        totwidthArray.Add(totwidth); // Länge in die Liste
                        if (totwidth > totwidthMax) { totwidthMax = totwidth; } // Maximum merken für Extents
                        totwidth = 0.0;
                        continue;
                    }
                    FontCache.GlobalFontCache.Get(fontName, fs, textString[i], out width, null);
                    totwidth += width;
                    if (i > 0)
                    {
                        double krn = FontCache.GlobalFontCache.GetKerning(fontName, fs, textString[i - 1], textString[i]);
                        totwidth += krn; // das ist negativ
                    }
                }
                totwidthArray.Add(totwidth); // den letzten Wert dazu
                if (totwidth > totwidthMax) { totwidthMax = totwidth; }
                if (lineAlignment != LineAlignMode.Left) // Extremwert berechnen
                {
                    switch (lineAlignment)
                    {
                        case LineAlignMode.Center: dx = -totwidthMax / 2.0; break;
                        case LineAlignMode.Right: dx = -totwidthMax; break;
                    }
                }
                switch (alignment)
                {
                    case AlignMode.Bottom: dy = (ac + dc - em) / (double)em; break;
                    case AlignMode.Baseline: dy = -(em - ac) / (double)em; break;
                    case AlignMode.Center: dy = (ac / 2 + dc / 2 - em) / (double)em; break;
                    case AlignMode.Top: dy = -1.0; break;
                }
                lowerLeft = pls.PointAt(new GeoPoint2D(dx, dy - (totwidthArray.Count - 1) * lsp));
                lowerRight = pls.PointAt(new GeoPoint2D(dx + totwidthMax, dy - (totwidthArray.Count - 1) * lsp));
                upperLeft = pls.PointAt(new GeoPoint2D(dx, dy + 1.0));

                // Der Text wird mit Größe 1 zusammengesetzt und an (0,0) platziert, jedoch unter Berücksichtigung
                // der Alignments
                ModOp toWorld = ModOp.Translate(location.x, location.y, location.z) * ModOp.Fit(new GeoVector[] { GeoVector.XAxis, GeoVector.YAxis, GeoVector.ZAxis }, new GeoVector[] { lineDirection, glyphDirection, lineDirection ^ glyphDirection });
                bool useLists = true;
                if (paintTo3D != null) useLists = !paintTo3D.IsBitmap;
                int lfcount = 1; // der Zähler für mehrzeiligen Text
                // den ersten x-Wert setzen:
                dx = 0.0;
                if (lineAlignment != LineAlignMode.Left)
                {
                    switch (lineAlignment)
                    {
                        case LineAlignMode.Center: dx = -totwidthArray[0] / 2.0; break;
                        case LineAlignMode.Right: dx = -totwidthArray[0]; break;
                    }
                }
                for (int i = 0; i < textString.Length; ++i)
                {
                    double width;
                    if (textString[i] == '\r') // linefeed ignorieren
                    {
                        continue;
                    }
                    if (textString[i] == '\n') // carriage return, neue Zeile
                    {
                        dy = dy - lsp; // eins runter im normierten Text
                        // den nächsten x-Wert setzen:
                        dx = 0.0;
                        if (lineAlignment != LineAlignMode.Left)
                        {
                            switch (lineAlignment)
                            {
                                case LineAlignMode.Center: dx = -totwidthArray[lfcount] / 2.0; break;
                                case LineAlignMode.Right: dx = -totwidthArray[lfcount]; break;
                            }
                            lfcount++;
                        }
                        continue;
                    }
                    IPaintTo3DList paintlist = FontCache.GlobalFontCache.Get(fontName, fs, textString[i], out width, null);
                    if (i > 0)
                    {
                        double krn = FontCache.GlobalFontCache.GetKerning(fontName, fs, textString[i - 1], textString[i]);
                        dx += krn; // das ist negativ
                    }
                    if (paintlist != null && useLists)
                    {
                        paintTo3D.PushMultModOp(toWorld * ModOp.Translate(dx, dy, 0));
                        paintTo3D.List(paintlist);
                        paintTo3D.PopModOp();
                    }
                    else
                    {
                        if (!useLists)
                        {
                            paintTo3D.PushMultModOp(toWorld * ModOp.Translate(dx, dy, 0));
                            FontCache.GlobalFontCache.Get(fontName, fs, textString[i], out width, paintTo3D); // hierbei wird auf paintTo3D ausgegeben
                            paintTo3D.PopModOp();
                        }
                    }
                    dx += width;
                }

            }
            else
            {
#if WEBASSEMBLY
                paintTo3D.Text(lineDirection, glyphDirection, location, fontName, textString, CADability.WebDrawing.FontStyle.Regular, alignment, lineAlignment);
#else
                paintTo3D.Text(lineDirection, glyphDirection, location, fontName, textString, fontStyle, alignment, lineAlignment);
#endif
            }
        }
        /// <summary>
        /// Overrides <see cref="CADability.GeoObject.IGeoObjectImpl.PrepareDisplayList (double)"/>
        /// </summary>
        /// <param name="precision"></param>
        public override void PrepareDisplayList(double precision)
        {
            // nichts zu tun
        }
        public override Style.EDefaultFor PreferredStyle
        {
            get
            {
                return Style.EDefaultFor.Text;
            }
        }
        /// <summary>
        /// Overrides <see cref="CADability.GeoObject.IGeoObjectImpl.GetQuadTreeItem (Projection, ExtentPrecision)"/>
        /// </summary>
        /// <param name="projection"></param>
        /// <param name="extentPrecision"></param>
        /// <returns></returns>
        public override IQuadTreeInsertableZ GetQuadTreeItem(Projection projection, ExtentPrecision extentPrecision)
        {
            //Text2D t2d = new Text2D(this, textString, fontName, fontStyle, Color.Black, projection.UnscaledProjection.Project(location), projection.UnscaledProjection.Project(lineDirection), projection.UnscaledProjection.Project(glyphDirection));
            //return new QuadTreeText(this, t2d, projection);
            return null;
        }
        #endregion
        #region IOctTreeInsertable members
        /// <summary>
        /// Overrides <see cref="CADability.GeoObject.IGeoObjectImpl.GetExtent (double)"/>
        /// </summary>
        /// <param name="precision"></param>
        /// <returns></returns>
        public override BoundingCube GetExtent(double precision)
        {
            return GetBoundingCube();
        }
        /// <summary>
        /// Overrides <see cref="CADability.GeoObject.IGeoObjectImpl.HitTest (ref BoundingCube, double)"/>
        /// </summary>
        /// <param name="cube"></param>
        /// <param name="precision"></param>
        /// <returns></returns>
        public override bool HitTest(ref BoundingCube cube, double precision)
        {
            // nicht sicher, ob die folgenden noch skaliert werden müssen:
            GeoPoint p1 = lowerLeft;
            GeoPoint p2 = lowerRight;
            GeoPoint p3 = lowerRight + (upperLeft - lowerLeft);
            GeoPoint p4 = upperLeft;
            if (cube.Interferes(ref p1, ref p2, ref p3)) return true;
            if (cube.Interferes(ref p4, ref p3, ref p2)) return true;
            return false;
        }
        /// <summary>
        /// Overrides <see cref="CADability.GeoObject.IGeoObjectImpl.HitTest (Projection, BoundingRect, bool)"/>
        /// </summary>
        /// <param name="projection"></param>
        /// <param name="rect"></param>
        /// <param name="onlyInside"></param>
        /// <returns></returns>
        public override bool HitTest(Projection projection, BoundingRect rect, bool onlyInside)
        {
            if (onlyInside)
            {
                if (projection.ProjectUnscaled(location) > rect) return false;
                if (projection.ProjectUnscaled(lowerLeft) > rect) return false;
                if (projection.ProjectUnscaled(lowerRight) > rect) return false;
                if (projection.ProjectUnscaled(upperLeft) > rect) return false;
                if (projection.ProjectUnscaled(lowerRight + (upperLeft - lowerLeft)) > rect) return false;
                return true;
            }
            else
            {
                ClipRect clr = new ClipRect(rect);
                // nicht sicher, object die folgenden noch skaliert werden müssen:
                return clr.ParallelogramHitTest(projection.ProjectUnscaled(lowerLeft), projection.ProjectUnscaled(lowerRight - lowerLeft), projection.ProjectUnscaled(upperLeft - lowerLeft));
            }
        }
        /// <summary>
        /// Overrides <see cref="CADability.GeoObject.IGeoObjectImpl.HitTest (Projection.PickArea, bool)"/>
        /// </summary>
        /// <param name="area"></param>
        /// <param name="onlyInside"></param>
        /// <returns></returns>
        public override bool HitTest(Projection.PickArea area, bool onlyInside)
        {
            if (onlyInside)
            {
                if (!BoundingCube.UnitBoundingCube.Contains(area.ToUnitBox * location)) return false;
                if (!BoundingCube.UnitBoundingCube.Contains(area.ToUnitBox * lowerLeft)) return false;
                if (!BoundingCube.UnitBoundingCube.Contains(area.ToUnitBox * lowerRight)) return false;
                if (!BoundingCube.UnitBoundingCube.Contains(area.ToUnitBox * upperLeft)) return false;
                if (!BoundingCube.UnitBoundingCube.Contains(area.ToUnitBox * (lowerRight + (upperLeft - lowerLeft)))) return false;
                return true;
            }
            else
            {
                GeoPoint p1 = area.ToUnitBox * lowerLeft;
                GeoPoint p2 = area.ToUnitBox * lowerRight;
                GeoPoint p3 = area.ToUnitBox * upperLeft;
                GeoPoint p4 = area.ToUnitBox * (lowerRight + (upperLeft - lowerLeft));
                if (BoundingCube.UnitBoundingCube.Interferes(ref p1, ref p2, ref p4)) return true;
                if (BoundingCube.UnitBoundingCube.Interferes(ref p1, ref p4, ref p3)) return true;
                return false;
            }

        }
        /// <summary>
        /// Overrides <see cref="CADability.GeoObject.IGeoObjectImpl.Position (GeoPoint, GeoVector, double)"/>
        /// </summary>
        /// <param name="fromHere"></param>
        /// <param name="direction"></param>
        /// <param name="precision"></param>
        /// <returns></returns>
        public override double Position(GeoPoint fromHere, GeoVector direction, double precision)
        {
            Plane plane = new Plane(location, lineDirection, glyphDirection);
            GeoPoint p = plane.Intersect(fromHere, direction);
            return Geometry.LinePar(fromHere, direction, p); // nicht getestet ob innerhalb des Textes
        }
        #endregion
        #region IColorDef Members
        public ColorDef ColorDef
        {
            get
            {
                return colorDef;
            }
            set
            {
                InvalidateSecondaryData();
                SetColorDef(ref colorDef, value);
            }
        }
        void IColorDef.SetTopLevel(ColorDef newValue)
        {
            colorDef = newValue;
        }
        void IColorDef.SetTopLevel(ColorDef newValue, bool overwriteChildNullColor)
        {
            (this as IColorDef).SetTopLevel(newValue);
        }
        #endregion
        #region ISerializable Members
        /// <summary>
        /// Constructor required by deserialization
        /// </summary>
        /// <param name="info">SerializationInfo</param>
        /// <param name="context">StreamingContext</param>
        protected Text(SerializationInfo info, StreamingContext context)
            : base(info, context)
        {
            textString = (string)info.GetValue("TextString", typeof(string));
            lineDirection = (GeoVector)info.GetValue("LineDirection", typeof(GeoVector));
            glyphDirection = (GeoVector)info.GetValue("GlyphDirection", typeof(GeoVector));
            location = (GeoPoint)info.GetValue("Location", typeof(GeoPoint));
            colorDef = (ColorDef)info.GetValue("ColorDef", typeof(ColorDef));
            fontName = (string)info.GetValue("FontName", typeof(string));
            // Serialisieren von Type FontStyle gibt Probleme
            // deshalb cast auf int
            int avoidFontStyle = (int)info.GetValue("FontStyle", typeof(int));
            fontStyle = (FontStyle)avoidFontStyle;
            alignment = (AlignMode)info.GetValue("Alignment", typeof(AlignMode));
            lineAlignment = (LineAlignMode)info.GetValue("LineAlignment", typeof(LineAlignMode));
            if (info.MemberCount > 14) // to avoid exceptions
            {
                try
                {
                    isReflected = info.GetBoolean("IsReflected");
                }
                catch (SerializationException)
                {
                    isReflected = false;
                }
            }
        }

        /// <summary>
        /// Implements <see cref="ISerializable.GetObjectData"/>
        /// </summary>
        /// <param name="info">The <see cref="System.Runtime.Serialization.SerializationInfo"/> to populate with data.</param>
        /// <param name="context">The destination (<see cref="System.Runtime.Serialization.StreamingContext"/>) for this serialization.</param>
        public override void GetObjectData(SerializationInfo info, StreamingContext context)
        {
            base.GetObjectData(info, context);
            info.AddValue("TextString", textString);
            info.AddValue("LineDirection", lineDirection);
            info.AddValue("GlyphDirection", glyphDirection);
            info.AddValue("Location", location);
            info.AddValue("ColorDef", colorDef);
            info.AddValue("FontName", fontName);
            int avoidFontStyle = (int)fontStyle;
            info.AddValue("FontStyle", avoidFontStyle);
            info.AddValue("Alignment", alignment);
            info.AddValue("LineAlignment", lineAlignment);
            info.AddValue("IsReflected", isReflected);

        }

        public override void GetObjectData(IJsonWriteData data)
        {
            base.GetObjectData(data);
            data.AddProperty("TextString", textString);
            data.AddProperty("LineDirection", lineDirection);
            data.AddProperty("GlyphDirection", glyphDirection);
            data.AddProperty("Location", location);
            data.AddProperty("ColorDef", colorDef);
            data.AddProperty("FontName", fontName);
            data.AddProperty("FontStyle", fontStyle);
            data.AddProperty("Alignment", alignment);
            data.AddProperty("LineAlignment", lineAlignment);
            data.AddProperty("IsReflected", isReflected);
        }

        public override void SetObjectData(IJsonReadData data)
        {
            base.SetObjectData(data);
            textString = data.GetProperty<string>("TextString");
            lineDirection = data.GetProperty<GeoVector>("LineDirection");
            glyphDirection = data.GetProperty<GeoVector>("GlyphDirection");
            location = data.GetProperty<GeoPoint>("Location");
            colorDef = data.GetProperty<ColorDef>("ColorDef");
            fontName = data.GetProperty<string>("FontName");
            fontStyle = data.GetProperty<FontStyle>("FontStyle");
            alignment = data.GetProperty<AlignMode>("Alignment");
            lineAlignment = data.GetProperty<LineAlignMode>("LineAlignment");
            isReflected = data.GetProperty<bool>("IsReflected");
        }
        #endregion
        internal void InvalidateSecondaryData()
        {
            isValidExtent = false;
        }
    }

    //internal class QuadTreeText : IQuadTreeInsertableZ
    //{
    //    Text text;
    //    Text2D text2d;
    //    double fx, fy, c;
    //    public QuadTreeText(Text text, Text2D text2d, Projection projection)
    //    {
    //        this.text = text;
    //        this.text2d = text2d;
    //        Plane plane = new Plane(text.Location, text.LineDirection, text.GlyphDirection);
    //        GeoPoint p1 = projection.UnscaledProjection * plane.Location;
    //        GeoPoint p2 = projection.UnscaledProjection * (plane.Location + plane.DirectionX);
    //        GeoPoint p3 = projection.UnscaledProjection * (plane.Location + plane.DirectionY);
    //        // die Werte fx, fy und c bestimmen die Z-Position
    //        double[,] m = new double[3, 3];
    //        m[0, 0] = p1.x;
    //        m[0, 1] = p1.y;
    //        m[0, 2] = 1.0;
    //        m[1, 0] = p2.x;
    //        m[1, 1] = p2.y;
    //        m[1, 2] = 1.0;
    //        m[2, 0] = p3.x;
    //        m[2, 1] = p3.y;
    //        m[2, 2] = 1.0;
    //        double[,] b = new double[,] { { p1.z }, { p2.z }, { p3.z } };
    //        LinearAlgebra.Matrix mx = new CADability.LinearAlgebra.Matrix(m);
    //        LinearAlgebra.Matrix s = mx.SaveSolve(new CADability.LinearAlgebra.Matrix(b));
    //        if (s != null)
    //        {
    //            fx = s[0, 0];
    //            fy = s[1, 0];
    //            c = s[2, 0];
    //        }
    //        else
    //        {   // von der Kante her betrachtet
    //            // Z-Wert ist location
    //            GeoPoint p = projection.UnscaledProjection * text.Location;
    //            fx = 0.0;
    //            fy = 0.0;
    //            c = p.z;
    //        }
    //    }
    //    #region IQuadTreeInsertableZ Members
    //    double IQuadTreeInsertableZ.GetZPosition(GeoPoint2D p)
    //    {
    //        return fx * p.x + fy * p.y + c;
    //    }

    //    #endregion

    //    #region IQuadTreeInsertable Members

    //    BoundingRect IQuadTreeInsertable.GetExtent()
    //    {
    //        return text2d.GetExtent();
    //    }

    //    bool IQuadTreeInsertable.HitTest(ref BoundingRect rect, bool includeControlPoints)
    //    {
    //        return text2d.HitTest(ref rect, includeControlPoints);
    //    }

    //    object IQuadTreeInsertable.ReferencedObject
    //    {
    //        get { return text; }
    //    }

    //    #endregion
    //}
}
