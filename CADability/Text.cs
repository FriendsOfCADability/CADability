using CADability.Actions;
using CADability.Attribute;
using CADability.Curve2D;
using CADability.Shapes;
using CADability.UserInterface;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Runtime.Serialization;
using System.Threading;


namespace CADability.GeoObject
{

    /// <summary>
    /// Property entry, a group in the property grid, that shows and edits the font of a <see cref="Text"/>: the font
    /// family, chosen from the families installed on the system, and the bold, italic, underline and strikeout flags.
    /// It is a sub-entry of <see cref="ShowPropertyText"/>.
    /// </summary>
    public class ShowPropertyFont : PropertyEntryImpl
    {
        private readonly Text text;
        private IPropertyEntry[] showProperties;
        /// <summary>
        /// Creates the font entry for the text <paramref name="t"/>.
        /// </summary>
        /// <param name="t">the text whose font is shown</param>
        public ShowPropertyFont(Text t)
        {
            text = t;
            resourceIdInternal = "Text.Font.Format";
        }

        private MultipleChoiceProperty InitFontList()
        {
            string[] choices = FontProvider.Current.GetFamilyNames().OrderBy(n => n, StringComparer.CurrentCultureIgnoreCase).ToArray();
            MultipleChoiceProperty res = new MultipleChoiceProperty("Text.Font", choices, text.Font);
            res.ValueChangedEvent += FontChanged;
            return res;
        }
        #region PropertyEntryImpl Overrides
        /// <inheritdoc/>
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


    /// <summary>
    /// Property entry that shows and edits a <see cref="Text"/> in the property grid: the string, the location, the
    /// alignment, the line and glyph direction, the size, the glyph angle, the column width, the font
    /// (<see cref="ShowPropertyFont"/>) and the attributes. Location, directions and size have hotspots in the drawing
    /// area. It is returned by <see cref="Text.GetShowProperties(IFrame)"/> and provides the context menu of the text.
    /// </summary>
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
        private readonly LengthProperty columnWidthProperty;
        /// <summary>
        /// Creates the property entry for the text <paramref name="t"/>.
        /// </summary>
        /// <param name="t">the text to show</param>
        /// <param name="frame">the frame the property grid belongs to</param>
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
            columnWidthProperty = new LengthProperty(text, "ColumnWidth", "Text.ColumnWidth", frame, false);
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
            columnWidthProperty.LengthChanged();
            res.Add(columnWidthProperty);
            res.Add(new ShowPropertyFont(text));
            return res.ToArray();
        }
        #region IShowPropertyImpl Overrides
        /// <inheritdoc/>
        public override PropertyEntryType Flags => PropertyEntryType.GroupTitle | PropertyEntryType.HasSubEntries | PropertyEntryType.ContextMenu | PropertyEntryType.Selectable;
        /// <inheritdoc/>
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
        /// <inheritdoc/>
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
        /// <inheritdoc/>
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
                    columnWidthProperty.LengthChanged();
                    list.Add(columnWidthProperty);
                    list.Add(new ShowPropertyFont(text));
                    list.AddRange(text.GetAttributeProperties(Frame));

                    showProperties = list.ToArray();
                }
                return showProperties;
            }
        }
        /// <inheritdoc/>
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

        /// <inheritdoc/>
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

        /// <summary>
        /// Raised when the hotspots of this entry (location, directions and size of the text) are to be shown, hidden,
        /// selected or deselected: when the entry is opened or closed and when one of its sub-entries is selected. The
        /// handler gets the hotspot and the kind of change, see <see cref="IDisplayHotSpots"/>.
        /// </summary>
        public event HotspotChangedDelegate HotspotChangedEvent;

        /// <inheritdoc/>
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
            columnWidthProperty.LengthChanged();
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
        /// <inheritdoc/>
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
        /// <inheritdoc/>
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
        /// <inheritdoc/>
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
        // and the font faces used so far, which come from FontProvider.Current (issue #412: no GDI here).
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
            public readonly int c; // a Unicode code point
            public DictKey(string fontName, int fontStyle, int c)
            {
                this.fontName = fontName;
                this.fontStyle = fontStyle;
                this.c = c;
            }
            public override int GetHashCode()
            {
                return fontName.GetHashCode() ^ fontStyle ^ c;
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
        struct DictVal
        {
            public CompoundShape shape; // hat die Splines statt der Polygone
            public IPaintTo3DList list;
            public bool listCreated; // true, when a display list has been created for this glyph with a IPaintTo3D. The list may still be null (space, or the glyph could not be painted at all)
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
        readonly Dictionary<FontKey, FontFace> faces; // the faces of FontProvider.Current, per font name and bold/italic
        readonly Dictionary<FontKey, double> capHeights; // see GetCapHeight
        public FontCache()
        {
            FontPrecision = Settings.GlobalSettings.GetIntValue("Font.Precision", 1); // |grob|mittel|fein
            displayAsPath = Settings.GlobalSettings.GetIntValue("Font.DisplayMode", 1) == 1;
            cache = new Dictionary<DictKey, DictVal>();
            centerLineCache = new Dictionary<DictKey, CenterLineVal>();
            fontCharacteristicsCache = new Dictionary<FontKey, FontCharacteristics>();
            faces = new Dictionary<FontKey, FontFace>();
            capHeights = new Dictionary<FontKey, double>();
        }
        /// <summary>
        /// Returns the face of <see cref="FontProvider.Current"/> for a font name and a font style (the flags of
        /// System.Drawing.FontStyle: 1 bold, 2 italic, 4 underline, 8 strikeout). An unknown font name gives the default face of
        /// the provider.
        /// </summary>
        /// <exception cref="InvalidOperationException">when the provider has no font at all</exception>
        internal FontFace GetFace(string fontName, int fontStyle)
        {
            bool bold = (fontStyle & 1) != 0, italic = (fontStyle & 2) != 0;
            FontKey key = new FontKey(fontName ?? "", fontStyle & 3);
            lock (faces)
            {
                if (faces.TryGetValue(key, out FontFace face)) return face;
                FontProvider provider = FontProvider.Current;
                face = provider.GetFace(fontName, bold, italic) ?? provider.GetDefaultFace(bold, italic);
                if (face == null) throw new InvalidOperationException("No font is available for texts. Install TrueType fonts, add font files to a ManagedFontProvider or set FontProvider.Current.");
                faces[key] = face;
                return face;
            }
        }
        /// <summary>
        /// Like <see cref="GetFace"/>, but returns null instead of throwing when there is no font at all: painting and the
        /// extent of a text leave the text out then, instead of failing.
        /// </summary>
        internal FontFace TryGetFace(string fontName, int fontStyle)
        {
            try
            {
                return GetFace(fontName, fontStyle);
            }
            catch (InvalidOperationException)
            {
                return null;
            }
        }
        /// <summary>
        /// The height of the capital letters of a font in units of the em height, measured at the outline of "H". A DXF or
        /// DWG text height (group 40) is this height, while <see cref="Text.TextSize"/> is the em height (issue #506).
        /// Returns 1.0 when there is no font at all or the font has no "H", then both heights are taken to be the same.
        /// </summary>
        internal double GetCapHeight(string fontName, int fontStyle)
        {
            FontKey key = new FontKey(fontName ?? "", fontStyle & 3);
            lock (this)
            {
                if (capHeights.TryGetValue(key, out double res)) return res;
                res = 1.0;
                FontFace face = TryGetFace(fontName, fontStyle & 3);
                if (face != null && face.EmHeight > 0)
                {
                    try
                    {   // the outline is y downwards with the baseline at CellAscent/EmHeight, see FontFace.GetGlyphPath
                        face.GetGlyphPath('H', false, false, out PointF[] points, out byte[] _);
                        if (points.Length > 0)
                        {
                            double top = points.Min(p => p.Y);
                            double height = (double)face.CellAscent / face.EmHeight - top;
                            if (height > 0.0 && height <= 2.0) res = height;
                        }
                    }
                    catch (Exception ex) when (!(ex is ThreadAbortException))
                    {   // a glyph that cannot be read leaves the heights the same
                    }
                }
                capHeights[key] = res;
                return res;
            }
        }
        // The caches are shared by all threads that measure or paint text, and Clear may empty them at any time: every access
        // goes through lock (this). The lock is reentrant, Get and GetCenterLines call GetOutline2D. The faces are locked
        // separately and always after this, so the two locks are taken in the same order everywhere.
        public Path2D[] GetOutline2D(string fontName, int fontStyle, int c, out double width)
        {
            lock (this)
            {
                FontFace face = GetFace(fontName, fontStyle);
                width = face.GetAdvanceWidth(c);
                face.GetGlyphPath(c, (fontStyle & 4) != 0, (fontStyle & 8) != 0, out PointF[] points, out byte[] types);
                if (points.Length == 0) return new Path2D[0];
                return GlyphShapeBuilder.PathDataToPath2D(points, types, FontPrecision);
            }
        }
        public CompoundShape Get(string font, int fontStyle, int c, out double width)
        {   // soll verschwinden!!
            DictVal found;
            lock (this)
            {
                if (!cache.TryGetValue(new DictKey(font, fontStyle, c), out found) || found.shape == null)
                {
                    found = new DictVal();
                    Path2D[] paths = GetOutline2D(font, fontStyle, c, out width);
                    found.shape = GlyphShapeBuilder.MakeShape(paths).Shape;
                    double extwidth = found.shape.GetExtent().GetWidth();
                    if (width < extwidth * 0.9) width = extwidth * 1.1;
                    found.width = width; // hier könnte man mit TextRenderer.MeasureText arbeiten, wenns so nicht passt
                    cache[new DictKey(font, fontStyle, c)] = found;
                }
            }
            width = found.width;
            return found.shape;
        }
        public double GetKerning(string font, int fontStyle, int a, int b)
        {
            lock (this)
            {
                return GetFace(font, fontStyle).GetKerning(a, b);
            }
        }
        public IPaintTo3DList Get(string font, int fontStyle, int c, out double width, IPaintTo3D paintTo3D)
        {
            bool useLists = true;
            if (paintTo3D != null) useLists = !paintTo3D.IsBitmap;
            DictVal found;
            lock (this)
            {
                if (useLists && cache.TryGetValue(new DictKey(font, fontStyle, c), out found))
                {
                    // listCreated: the list may be null (e.g. space, or a glyph that could not be painted at all),
                    // this result is cached as well, so the failing glyph is not tried again on every repaint
                    if (found.listCreated || paintTo3D == null)
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
                Path2D[] paths = null;
                width = 0.0;
                try
                {
                    try
                    {
                        paths = GetOutline2D(font, fontStyle, c, out width);
                    }
                    catch (Exception ex) when (!(ex is ThreadAbortException))
                    {   // a glyph whose outline cannot be read is left out, it must not take the whole text down
                        paths = new Path2D[0];
                    }
                    if (paintTo3D != null)
                    {
                        bool painted = false;
                        if (!displayAsPath)
                        {
                            try
                            {
                                painted = PaintGlyphFaces(paths, paintTo3D);
                            }
                            catch (Exception ex) when (!(ex is ThreadAbortException))
                            {   // a glyph that cannot be triangulated must not take the whole text down
                                painted = false;
                            }
                        }
                        if (!painted && paths.Length > 0)
                        {   // either displayAsPath is set or no face could be made from the outline (e.g. overlapping or
                            // self intersecting contours): show the outline instead of nothing
                            PaintGlyphOutline(paths, paintTo3D);
                        }
                    }
                }
                finally
                {
                    if (paintTo3D != null && useLists)
                    {   // the list must be closed in any case, even if the outline could not be retrieved
                        found.list = paintTo3D.CloseList(); // kann null werden, bei Leerzeichen z.B.
                        found.listCreated = true;
                        paintTo3D.SelectMode = oldSelectMode;
                    }
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
        /// <summary>
        /// Paints the outline paths of a glyph as curves.
        /// </summary>
        private static void PaintGlyphOutline(Path2D[] paths, IPaintTo3D paintTo3D)
        {
            for (int i = 0; i < paths.Length; ++i)
            {
                try
                {
                    IGeoObject go = paths[i].MakeGeoObject(Plane.XYPlane);
                    go.PaintTo3D(paintTo3D);
                }
                catch (Exception ex) when (!(ex is ThreadAbortException))
                {   // a single bad path must not remove the whole glyph
                }
            }
        }
        /// <summary>
        /// Builds the filled area of the glyph from its outline paths and paints it as triangulated faces.
        /// A part of the glyph that yields no triangles is painted by its outline instead.
        /// </summary>
        /// <returns>true, when something has been painted, false when no face could be made from the paths</returns>
        private static bool PaintGlyphFaces(Path2D[] paths, IPaintTo3D paintTo3D)
        {
            CompoundShape res = GlyphShapeBuilder.MakeShape(paths).Shape;
            if (res == null || res.SimpleShapes.Length == 0) return false;
            PlaneSurface pls = new PlaneSurface(Plane.XYPlane);
            double precision;
            switch (FontPrecision) // this is for triangulation precision, font size is 1
            {
                case 0: // rough
                    precision = 0.2;
                    break;
                case 2: // fine
                    precision = 0.005;
                    break;
                default: // middle
                    precision = 0.05;
                    break;
            }
            double oldprecision = paintTo3D.Precision;
            bool oldpse = paintTo3D.PaintSurfaceEdges;
            // glyphs are rendered flat (unlit): text must appear exactly in its
            // ColorDef color and not be modified by the scene lighting
            IPaintTo3DFlatText flatText = paintTo3D as IPaintTo3DFlatText;
            bool painted = false;
            try
            {
                paintTo3D.Precision = precision;
                paintTo3D.PaintSurfaceEdges = false;
                if (flatText != null) flatText.FlatTextMode = true;
                for (int i = 0; i < res.SimpleShapes.Length; ++i)
                {
                    bool hasTriangles = false;
                    try
                    {
                        Face fc = Face.MakeFace(pls, res.SimpleShapes[i]);
                        fc.ColorDef = null; // no color in the display list!
                        // check the triangulation before painting: some glyphs (e.g. "e" of Bahnschrift Light) yield a face without triangles
                        fc.GetTriangulation(precision, out GeoPoint[] tp, out GeoPoint2D[] tuv, out int[] ti, out BoundingCube te);
                        if (ti != null && ti.Length >= 3)
                        {
                            fc.PaintTo3D(paintTo3D); // uses the triangulation calculated above (same precision)
                            hasTriangles = true;
                        }
                    }
                    catch (Exception ex) when (!(ex is ThreadAbortException))
                    {   // this part of the glyph cannot be triangulated, it is painted as outline below
                    }
                    if (!hasTriangles)
                    {   // show at least the outline of this part
                        List<Path2D> outline = new List<Path2D>();
                        try
                        {
                            outline.Add(res.SimpleShapes[i].Outline.AsPath());
                            for (int j = 0; j < res.SimpleShapes[i].NumHoles; ++j) outline.Add(res.SimpleShapes[i].Holes[j].AsPath());
                        }
                        catch (Exception ex) when (!(ex is ThreadAbortException)) { }
                        if (flatText != null) flatText.FlatTextMode = false;
                        PaintGlyphOutline(outline.ToArray(), paintTo3D);
                        if (flatText != null) flatText.FlatTextMode = true;
                    }
                    painted = true;
                }
            }
            finally
            {
                if (flatText != null) flatText.FlatTextMode = false;
                paintTo3D.Precision = oldprecision;
                paintTo3D.PaintSurfaceEdges = oldpse;
            }
            return painted;
        }
        /// <summary>
        /// Disposes the cached glyph display lists and empties all caches. The glyphs are recorded again with the
        /// <see cref="IPaintTo3D"/> that paints them next.
        /// </summary>
        public void Clear()
        {
            lock (this)
            {
                foreach (DictVal val in cache.Values)
                {
                    val.list?.Dispose();
                }
                cache.Clear();
                centerLineCache.Clear();
                fontCharacteristicsCache.Clear();
                capHeights.Clear();
                lock (faces) faces.Clear();
            }
        }
        public GeoObjectList GetCenterLines(string font, int fontStyle, int c, out double width)
        {
            lock (this)
            {
                return ReadCenterLines(font, fontStyle, c, out width);
            }
        }
        private GeoObjectList ReadCenterLines(string font, int fontStyle, int c, out double width)
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
                CompoundShape ccs = GlyphShapeBuilder.MakeShape(paths).Shape;
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
        private double columnWidth; // 0.0: no automatic word wrap, otherwise the text is wrapped to this width (world units)
        private double lineSpacing; // 0.0: natural line spacing of the font, otherwise baseline distance as factor of the text size
        private string displayString; // cache: textString with the automatic line breaks applied (see GetDisplayString)
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
        private static HashSet<string> fontFamilyNames;
        /// <summary>
        /// The upper case names of the font families of <see cref="FontProvider.Current"/>.
        /// </summary>
        internal static HashSet<string> FontFamilyNames =>
            LazyInitializer.EnsureInitialized(ref fontFamilyNames, () =>
                new HashSet<string>(FontProvider.Current.GetFamilyNames().Select(f => f.ToUpper())));
        internal static void ResetFontFamilyNames()
        {
            fontFamilyNames = null;
        }

        #region polymorph construction
        /// <summary>
        /// Delegate for the construction of a Text.
        /// </summary>
        /// <returns>A Text or Text derived class</returns>
        public delegate Text ConstructionDelegate();
        /// <summary>
        /// Provide a delegate here if you want your Text derived class to be created each time CADability creates a text.
        /// </summary>
        public static ConstructionDelegate Constructor;
        /// <summary>
        /// Releases the display lists of the text glyphs, which are shared by all texts and all views, and empties the
        /// font cache. Call it when the <see cref="IPaintTo3D"/> that recorded the glyphs can no longer be used, i.e. when the
        /// renderer is switched or its graphics device was lost. The glyph lists are disposed with
        /// <see cref="IPaintTo3DList.Dispose"/>, so call it while the old renderer can still handle that (e.g. with its OpenGL
        /// context current), or make its lists ignore a lost device.
        /// The display lists of the models contain the glyph lists, so they must be rebuilt as well, see
        /// <see cref="ProjectedModel.InvalidateDisplayLists"/>.
        /// </summary>
        public static void ClearFontCache()
        {
            FontCache.GlobalFontCache.Clear();
        }
        /// <summary>
        /// The only way to create a text: there are no public constructors. Calls <see cref="Constructor"/> when one is
        /// registered, otherwise creates a <see cref="Text"/>. Set at least <see cref="Font"/>, <see cref="TextString"/>,
        /// <see cref="Location"/> and <see cref="TextSize"/> afterwards.
        /// </summary>
        /// <returns>the new text</returns>
        public static Text Construct()
        {
            if (Constructor != null) return Constructor();
            return new Text();
        }
        #endregion

        /// <summary>
        /// Protected constructor; use <see cref="Construct"/> to create a text. The text starts with the line direction
        /// <see cref="GeoVector.XAxis"/>, the glyph direction <see cref="GeoVector.YAxis"/>, the regular font style and the
        /// alignment <see cref="AlignMode.Baseline"/>. Whether the text is displayed as outline paths or as filled faces
        /// comes from the global setting "Font.DisplayMode".
        /// </summary>
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
                    int fs = (int)fontStyle;
                    FontFace ff = FontCache.GlobalFontCache.GetFace(fontName, fs); // FontProvider.Current, see issue #412
                    int em = ff.EmHeight;
                    int asc = ff.CellAscent;
                    int desc = ff.CellDescent;
                    int lsp = ff.LineSpacing;
                    // asc+desc kann mehr als em sein. ich nehme jedoch an, dass em für die Größe ausschlaggebend ist
                    // wenn man em als 1 annimmt, dann muss man um desc/em nach unten verschieben um auf die Grundlinie zu kommen
                    emSizeFactor = (double)(asc + desc) / (double)em;
                    emAscDescFactor = (double)(desc) / (double)asc;
                    emDescDiff = (double)(em - asc) / (double)em;
                    this.desc = (double)(desc) / (double)em;
                    // the size of the text string with the font size glyphDirection.Length: the advance widths and the kerning
                    // of the characters and the line spacing (this used to be Graphics.MeasureString of GDI+)
                    double width = 0.0;
                    int[] chars = CodePoints(textString);
                    for (int i = 0; i < chars.Length; ++i)
                    {
                        width += ff.GetAdvanceWidth(chars[i]);
                        if (i > 0) width += ff.GetKerning(chars[i - 1], chars[i]);
                    }
                    double size = glyphDirection.Length;
                    sizeExtent = new SizeF((float)(width * size), (float)(size * lsp / em));
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
        /// The characters of <paramref name="s"/> as Unicode code points: a surrogate pair (e.g. an emoji) is one character.
        /// A lone surrogate is kept as the char it is.
        /// </summary>
        internal static int[] CodePoints(string s)
        {
            List<int> res = new List<int>(s.Length);
            for (int i = 0; i < s.Length; ++i)
            {
                if (char.IsHighSurrogate(s[i]) && i + 1 < s.Length && char.IsLowSurrogate(s[i + 1]))
                {
                    res.Add(char.ConvertToUtf32(s[i], s[i + 1]));
                    ++i;
                }
                else res.Add(s[i]);
            }
            return res.ToArray();
        }
        // Returns the string to display: textString, with additional line breaks inserted at word
        // boundaries when ColumnWidth demands wrapping. Character widths are measured with the same
        // FontCache the layout loops use, so the wrapped lines fit the column exactly.
        private string GetDisplayString(int fs)
        {
            if (textString == null) return "";
            if (columnWidth <= 0.0) return textString;
            if (displayString != null) return displayString;
            double scale = lineDirection.Length;
            if (scale < Precision.eps) return textString;
            double normalizedWidth = columnWidth / scale; // FontCache widths are normalized to em size 1
            string res;
            try
            {
                System.Text.StringBuilder wrapped = new System.Text.StringBuilder();
                string[] paragraphs = textString.Replace("\r", "").Split('\n');
                for (int p = 0; p < paragraphs.Length; p++)
                {
                    if (p > 0) wrapped.Append('\n');
                    AppendWrappedParagraph(wrapped, paragraphs[p], normalizedWidth, fs);
                }
                res = wrapped.ToString();
            }
            catch
            {
                res = textString; // if measuring fails, show the text without wrapping
            }
            displayString = res;
            return res;
        }
        private void AppendWrappedParagraph(System.Text.StringBuilder result, string paragraph, double normalizedWidth, int fs)
        {
            double MeasureWord(string word)
            {
                double total = 0.0;
                int[] w = CodePoints(word);
                for (int i = 0; i < w.Length; i++)
                {
                    FontCache.GlobalFontCache.Get(fontName, fs, w[i], out double cw, null);
                    total += cw;
                    if (i > 0) total += FontCache.GlobalFontCache.GetKerning(fontName, fs, w[i - 1], w[i]);
                }
                return total;
            }
            FontCache.GlobalFontCache.Get(fontName, fs, ' ', out double spaceWidth, null);
            string[] words = paragraph.Split(' ');
            double lineWidth = 0.0;
            bool firstOnLine = true;
            foreach (string word in words)
            {
                double wordWidth = MeasureWord(word);
                if (firstOnLine)
                {
                    result.Append(word);
                    lineWidth = wordWidth;
                    firstOnLine = false;
                }
                else if (lineWidth + spaceWidth + wordWidth <= normalizedWidth)
                {
                    result.Append(' ').Append(word);
                    lineWidth += spaceWidth + wordWidth;
                }
                else
                {
                    result.Append('\n').Append(word);
                    lineWidth = wordWidth;
                }
            }
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
            FontFace ff = FontCache.GlobalFontCache.GetFace(fontName, fs); // FontProvider.Current, see issue #412
            int[] txt = CodePoints(GetDisplayString(fs));
            int em = ff.EmHeight;
            int ls = ff.LineSpacing;
            int dc = ff.CellDescent;
            int ac = ff.CellAscent;
            double lsp = ls / em;
            if (lineSpacing > 0.0) lsp = lineSpacing; // explicit baseline distance as factor of the text size
            double dx = 0.0;
            double dy = 0.0;
            double totwidth = 0.0;
            double totwidthMax = 0.0;
            List<double> totwidthArray = new List<double>(); // Liste der Zeilenlängen bei mehrzeiligen Texten
            for (int i = 0; i < txt.Length; ++i)
            {
                double width;
                if (txt[i] == '\r') // linefeed ignorieren
                {
                    continue;
                }
                if (txt[i] == '\n') // carriage return
                {
                    totwidthArray.Add(totwidth); // Länge in die Liste
                    if (totwidth > totwidthMax) { totwidthMax = totwidth; } // Maximum merken für Extents
                    totwidth = 0.0;
                    continue;
                }
                FontCache.GlobalFontCache.Get(fontName, fs, txt[i], out width, null);
                totwidth += width;
                if (i > 0)
                {
                    double krn = FontCache.GlobalFontCache.GetKerning(fontName, fs, txt[i - 1], txt[i]);
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
            if (totwidthArray.Count > 1)
            {   // multi-line text: the vertical alignment refers to the whole text block, not only to the first line
                switch (alignment)
                {
                    case AlignMode.Bottom:
                    case AlignMode.Baseline: dy += (totwidthArray.Count - 1) * lsp; break;
                    case AlignMode.Center: dy += (totwidthArray.Count - 1) * lsp * 0.5; break;
                }
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
            for (int i = 0; i < txt.Length; ++i)
            {
                double width;
                if (txt[i] == '\r') // linefeed ignorieren
                {
                    continue;
                }
                if (txt[i] == '\n') // carriage return, neue Zeile
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
                Path2D[] path2d = FontCache.GlobalFontCache.GetOutline2D(fontName, fs, txt[i], out width);
                if (i > 0)
                {
                    double krn = FontCache.GlobalFontCache.GetKerning(fontName, fs, txt[i - 1], txt[i]);
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
            FontFace ff = FontCache.GlobalFontCache.GetFace(fontName, fs); // FontProvider.Current, see issue #412
            int[] txt = CodePoints(GetDisplayString(fs));
            int em = ff.EmHeight;
            int ls = ff.LineSpacing;
            int dc = ff.CellDescent;
            int ac = ff.CellAscent;
            double lsp = ls / em;
            if (lineSpacing > 0.0) lsp = lineSpacing; // explicit baseline distance as factor of the text size
            double dx = 0.0;
            double dy = 0.0;
            double totwidth = 0.0;
            double totwidthMax = 0.0;
            List<double> totwidthArray = new List<double>(); // Liste der Zeilenlängen bei mehrzeiligen Texten
            for (int i = 0; i < txt.Length; ++i)
            {
                double width;
                if (txt[i] == '\r') // linefeed ignorieren
                {
                    continue;
                }
                if (txt[i] == '\n') // carriage return
                {
                    totwidthArray.Add(totwidth); // Länge in die Liste
                    if (totwidth > totwidthMax) { totwidthMax = totwidth; } // Maximum merken für Extents
                    totwidth = 0.0;
                    continue;
                }
                FontCache.GlobalFontCache.Get(fontName, fs, txt[i], out width, null);
                totwidth += width;
                if (i > 0)
                {
                    double krn = FontCache.GlobalFontCache.GetKerning(fontName, fs, txt[i - 1], txt[i]);
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
            if (totwidthArray.Count > 1)
            {   // multi-line text: the vertical alignment refers to the whole text block, not only to the first line
                switch (alignment)
                {
                    case AlignMode.Bottom:
                    case AlignMode.Baseline: dy += (totwidthArray.Count - 1) * lsp; break;
                    case AlignMode.Center: dy += (totwidthArray.Count - 1) * lsp * 0.5; break;
                }
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
            for (int i = 0; i < txt.Length; ++i)
            {
                double width;
                if (txt[i] == '\r') // linefeed ignorieren
                {
                    continue;
                }
                if (txt[i] == '\n') // carriage return, neue Zeile
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
                CompoundShape cs = FontCache.GlobalFontCache.Get(fontName, fs, txt[i], out width);
                res.Add(cs.GetModified(ModOp2D.Translate(dx, dy)));
                if (i > 0)
                {
                    double krn = FontCache.GlobalFontCache.GetKerning(fontName, fs, txt[i - 1], txt[i]);
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
        /// <summary>
        /// Returns the center lines of the characters of this text: for each character a <see cref="Block"/> containing the
        /// curves that run through the middle of the strokes of the glyph (computed from the outline with
        /// <see cref="ShapeMiddelLine"/>), positioned in world coordinates like the text itself. Suited for single stroke
        /// output such as engraving, where <see cref="GetOutline"/> would give the two sides of each stroke. Not used by
        /// CADability itself.
        /// </summary>
        /// <returns>one block per character in the order of the text; line breaks produce no entry</returns>
        public virtual GeoObjectList GetCenterline()
        {   // koopiert aus Getoutline!
            GeoObjectList res = new GeoObjectList();
            int fs = 0; // das ist regular
            if (Underline) fs |= (int)FontStyle.Underline;
            if (Italic) fs |= (int)FontStyle.Italic;
            if (Bold) fs |= (int)FontStyle.Bold;
            if (Strikeout) fs |= (int)FontStyle.Strikeout;
            PlaneSurface pls = new PlaneSurface(location, lineDirection, glyphDirection, lineDirection ^ glyphDirection);
            FontFace ff = FontCache.GlobalFontCache.GetFace(fontName, fs); // FontProvider.Current, see issue #412
            int[] txt = CodePoints(GetDisplayString(fs));
            int em = ff.EmHeight;
            int ls = ff.LineSpacing;
            int dc = ff.CellDescent;
            int ac = ff.CellAscent;
            double lsp = ls / em;
            if (lineSpacing > 0.0) lsp = lineSpacing; // explicit baseline distance as factor of the text size
            double dx = 0.0;
            double dy = 0.0;
            double totwidth = 0.0;
            double totwidthMax = 0.0;
            List<double> totwidthArray = new List<double>(); // Liste der Zeilenlängen bei mehrzeiligen Texten
            for (int i = 0; i < txt.Length; ++i)
            {
                double width;
                if (txt[i] == '\r') // linefeed ignorieren
                {
                    continue;
                }
                if (txt[i] == '\n') // carriage return
                {
                    totwidthArray.Add(totwidth); // Länge in die Liste
                    if (totwidth > totwidthMax) { totwidthMax = totwidth; } // Maximum merken für Extents
                    totwidth = 0.0;
                    continue;
                }
                FontCache.GlobalFontCache.GetCenterLines(fontName, fs, txt[i], out width);
                totwidth += width;
                if (i > 0)
                {
                    double krn = FontCache.GlobalFontCache.GetKerning(fontName, fs, txt[i - 1], txt[i]);
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
            if (totwidthArray.Count > 1)
            {   // multi-line text: the vertical alignment refers to the whole text block, not only to the first line
                switch (alignment)
                {
                    case AlignMode.Bottom:
                    case AlignMode.Baseline: dy += (totwidthArray.Count - 1) * lsp; break;
                    case AlignMode.Center: dy += (totwidthArray.Count - 1) * lsp * 0.5; break;
                }
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
            for (int i = 0; i < txt.Length; ++i)
            {
                double width;
                if (txt[i] == '\r') // linefeed ignorieren
                {
                    continue;
                }
                if (txt[i] == '\n') // carriage return, neue Zeile
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
                GeoObjectList charlist = FontCache.GlobalFontCache.GetCenterLines(fontName, fs, txt[i], out width);
                if (i > 0)
                {
                    double krn = FontCache.GlobalFontCache.GetKerning(fontName, fs, txt[i - 1], txt[i]);
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
                FontFace ff = FontCache.GlobalFontCache.TryGetFace(fontName, fs); // FontProvider.Current, see issue #412
                if (ff == null) return; // no font at all: nothing to paint or measure
                int[] txt = CodePoints(GetDisplayString(fs));
                int em = ff.EmHeight;
                int ls = ff.LineSpacing;
                int dc = ff.CellDescent;
                int ac = ff.CellAscent;
                double lsp;
                if (em > 0) lsp = (double)ls / em;
                else lsp = 1.0;
                if (lineSpacing > 0.0) lsp = lineSpacing; // explicit baseline distance as factor of the text size
                double dx = 0.0;
                double dy = 0.0;
                double totwidth = 0.0;
                double totwidthMax = 0.0;
                List<double> totwidthArray = new List<double>(); // Liste der Zeilenlängen bei mehrzeiligen Texten
                if (textString == null) textString = "";
                for (int i = 0; i < txt.Length; ++i)
                {
                    double width;
                    if (txt[i] == '\r') // linefeed ignorieren
                    {
                        continue;
                    }
                    if (txt[i] == '\n') // carriage return
                    {
                        totwidthArray.Add(totwidth); // Länge in die Liste
                        if (totwidth > totwidthMax) { totwidthMax = totwidth; } // Maximum merken für Extents
                        totwidth = 0.0;
                        continue;
                    }
                    FontCache.GlobalFontCache.Get(fontName, fs, txt[i], out width, null);
                    totwidth += width;
                    if (i > 0)
                    {
                        double krn = FontCache.GlobalFontCache.GetKerning(fontName, fs, txt[i - 1], txt[i]);
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
                if (totwidthArray.Count > 1)
                {   // multi-line text: the vertical alignment refers to the whole text block, not only to the first line
                    switch (alignment)
                    {
                        case AlignMode.Bottom:
                        case AlignMode.Baseline: dy += (totwidthArray.Count - 1) * lsp; break;
                        case AlignMode.Center: dy += (totwidthArray.Count - 1) * lsp * 0.5; break;
                    }
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
        /// with a <see cref="ModOp.Rotate(GeoPoint, GeoVector, SweepAngle)"/> rotation.
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

        /// <summary>
        /// Sets the line direction and rotates the glyph direction along with it, so that the <see cref="GlyphAngle"/>
        /// between both and the lengths of both (the size and the aspect ratio of the text) stay as they are: the glyph
        /// direction is recomputed by rotating the new line direction about the normal of the text's plane by the glyph
        /// angle. Only the direction of <paramref name="ld"/> is used, its length is ignored; a null vector is ignored
        /// altogether. Raises the change events and is undoable. The property grid uses this method when the line direction
        /// is edited.
        /// </summary>
        /// <param name="ld">the new line direction</param>
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
        /// <summary>
        /// Sets <see cref="LineDirection"/> and <see cref="GlyphDirection"/> at once, without the checks of the individual
        /// properties. The vectors must not be parallel; their lengths define the size and the aspect ratio of the text.
        /// Raises the change events and is undoable.
        /// </summary>
        /// <param name="lineDirection">the direction of the lines of the text</param>
        /// <param name="glyphDirection">the direction of the vertical parts of the characters</param>
        public void SetDirections(GeoVector lineDirection, GeoVector glyphDirection)
        {
            using (new Changing(this))
            {
                this.glyphDirection = glyphDirection;
                this.lineDirection = lineDirection;
            }
        }
        /// <summary>
        /// Gets or sets the <see cref="GlyphDirection"/> without changing its length: only the direction of the value is
        /// used, the size of the text stays as it is. A null vector or a vector parallel to the <see cref="LineDirection"/>
        /// is ignored. Setting it raises the change events and is undoable. The property grid uses this property when the
        /// glyph direction is edited.
        /// </summary>
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
        /// The height of the capital letters of the font of this text as a fraction of <see cref="TextSize"/>, which is the
        /// em height. DXF and DWG give the height of a text as the height of its capital letters, so import and export
        /// convert with this factor (issue #506).
        /// </summary>
        internal double CapHeightFactor
        {
            get
            {
                int fs = 0;
                if (Bold) fs |= (int)FontStyle.Bold;
                if (Italic) fs |= (int)FontStyle.Italic;
                return FontCache.GlobalFontCache.GetCapHeight(fontName, fs);
            }
        }
        /// <summary>
        /// Width of the text column for automatic word wrapping (in world units, e.g. from a DXF MTEXT).
        /// The displayed text is broken into additional lines at word boundaries so that no line gets
        /// wider than this value. 0.0 (the default) means no automatic wrapping, only explicit line
        /// breaks ("\n") are used.
        /// </summary>
        public double ColumnWidth
        {
            get { return columnWidth; }
            set
            {
                if (value != columnWidth)
                {
                    using (new Changing(this, "ColumnWidth"))
                    {
                        columnWidth = value;
                        isValidExtent = false;
                    }
                }
            }
        }
        /// <summary>
        /// Distance between two baselines of a multi-line text as a factor of the text size.
        /// 0.0 (the default) means the natural line spacing of the font is used.
        /// </summary>
        public double LineSpacing
        {
            get { return lineSpacing; }
            set
            {
                if (value != lineSpacing)
                {
                    using (new Changing(this, "LineSpacing"))
                    {
                        lineSpacing = value;
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
        /// <inheritdoc/>
        public override IGeoObject Clone()
        {
            Text res = Construct();
            res.location = location;
            res.lineDirection = lineDirection;
            res.glyphDirection = glyphDirection;
            res.fontName = fontName;
            res.textString = textString;
            res.columnWidth = columnWidth;
            res.lineSpacing = lineSpacing;
            res.fontStyle = fontStyle;
            res.alignment = alignment;
            res.lineAlignment = lineAlignment;
            res.isValidExtent = false; // sonst muss man die anderen Daten auch noch kopieren
            res.sizeExtent = sizeExtent;
            res.emSizeFactor = emSizeFactor;
            res.CopyAttributes(this);
            return res;
        }
        /// <inheritdoc/>
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
                columnWidth = txt.columnWidth;
                lineSpacing = txt.lineSpacing;
                fontStyle = txt.fontStyle;
                alignment = txt.alignment;
                lineAlignment = txt.lineAlignment;
                isValidExtent = txt.isValidExtent;
                sizeExtent = txt.sizeExtent;
                emSizeFactor = txt.emSizeFactor;
            }
        }
        /// <inheritdoc/>
        public override void Modify(ModOp m)
        {
            using (new Changing(this, "ModifyInverse", m))
            {
                location = m * location;
                lineDirection = m * lineDirection;
                glyphDirection = m * glyphDirection;
            }
        }
        /// <inheritdoc/>
        public override IPropertyEntry GetShowProperties(IFrame Frame)
        {
            return new ShowPropertyText(this, Frame);
        }
        /// <inheritdoc/>
        public override IPropertyEntry[] GetAttributeProperties(IFrame Frame)
        {
            List<IPropertyEntry> res = new List<IPropertyEntry>();
            ShowPropertyText spt = new ShowPropertyText(this, Frame);
            // res.AddRange(spt.GetAdditionalTextProperties());
            res.AddRange(base.GetAttributeProperties(Frame));
            return res.ToArray();
        }
        /// <inheritdoc/>
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
        /// <inheritdoc/>
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
        /// <inheritdoc/>
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
                    FontFace ff = FontCache.GlobalFontCache.TryGetFace(fontName, fs); // FontProvider.Current, see issue #412
                    if (ff == null) return; // no font at all: nothing to paint or measure
                    int em = ff.EmHeight;
                    int ls = ff.LineSpacing;
                    int dc = ff.CellDescent;
                    int ac = ff.CellAscent;
                    foreach (int c in CodePoints(textString))
                    {
                        FontCache.GlobalFontCache.Get(fontName, fs, c, out double width, paintTo3D);
                    }
                }
                else
                {
                    paintTo3D.PrepareText(fontName, textString, fontStyle);
                }
            }
        }
        /// <summary>
        /// Handler for <see cref="OnPaintTo3D"/>.
        /// </summary>
        /// <param name="toPaint">the text being painted</param>
        /// <param name="paintTo3D">the paint interface to paint to</param>
        /// <returns>true if the handler painted the text itself and the default painting is to be skipped, false to let the
        /// text paint itself as usual</returns>
        public delegate bool PaintTo3DDelegate(Text toPaint, IPaintTo3D paintTo3D);
        /// <summary>
        /// Hook to replace the painting of texts: when set, <see cref="PaintTo3D"/> calls it first and does nothing more
        /// when it returns true. The hook is static, so it applies to every text of the process.
        /// </summary>
        public static PaintTo3DDelegate OnPaintTo3D;
        /// <inheritdoc/>
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
        /// <inheritdoc/>
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
                FontFace ff = FontCache.GlobalFontCache.TryGetFace(fontName, fs); // FontProvider.Current, see issue #412
                if (ff == null) return; // no font at all: nothing to paint or measure
                int[] txt = CodePoints(GetDisplayString(fs));
                int em = ff.EmHeight;
                int ls = ff.LineSpacing;
                int dc = ff.CellDescent;
                int ac = ff.CellAscent;
                double lsp = ls / em;
                if (lineSpacing > 0.0) lsp = lineSpacing; // explicit baseline distance as factor of the text size
            if (lineSpacing > 0.0) lsp = lineSpacing; // explicit baseline distance as factor of the text size
                double dx = 0.0;
                double dy = 0.0;
                double totwidth = 0.0;
                double totwidthMax = 0.0;
                List<double> totwidthArray = new List<double>(); // Liste der Zeilenlängen bei mehrzeiligen Texten
                for (int i = 0; i < txt.Length; ++i)
                {
                    double width;
                    if (txt[i] == '\r') // linefeed ignorieren
                    {
                        continue;
                    }
                    if (txt[i] == '\n') // carriage return
                    {
                        totwidthArray.Add(totwidth); // Länge in die Liste
                        if (totwidth > totwidthMax) { totwidthMax = totwidth; } // Maximum merken für Extents
                        totwidth = 0.0;
                        continue;
                    }
                    FontCache.GlobalFontCache.Get(fontName, fs, txt[i], out width, null);
                    totwidth += width;
                    if (i > 0)
                    {
                        double krn = FontCache.GlobalFontCache.GetKerning(fontName, fs, txt[i - 1], txt[i]);
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
                if (totwidthArray.Count > 1)
                {   // multi-line text: the vertical alignment refers to the whole text block, not only to the first line
                    switch (alignment)
                    {
                        case AlignMode.Bottom:
                        case AlignMode.Baseline: dy += (totwidthArray.Count - 1) * lsp; break;
                        case AlignMode.Center: dy += (totwidthArray.Count - 1) * lsp * 0.5; break;
                    }
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
                for (int i = 0; i < txt.Length; ++i)
                {
                    double width;
                    if (txt[i] == '\r') // linefeed ignorieren
                    {
                        continue;
                    }
                    if (txt[i] == '\n') // carriage return, neue Zeile
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
                    IPaintTo3DList paintlist = FontCache.GlobalFontCache.Get(fontName, fs, txt[i], out width, null);
                    if (i > 0)
                    {
                        double krn = FontCache.GlobalFontCache.GetKerning(fontName, fs, txt[i - 1], txt[i]);
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
                            FontCache.GlobalFontCache.Get(fontName, fs, txt[i], out width, paintTo3D); // hierbei wird auf paintTo3D ausgegeben
                            paintTo3D.PopModOp();
                        }
                    }
                    dx += width;
                }

            }
            else
            {
                string wrapped = GetDisplayString((int)fontStyle); // apply automatic line wrapping here as well
                paintTo3D.Text(lineDirection, glyphDirection, location, fontName, wrapped, fontStyle, alignment, lineAlignment);
            }
        }
        /// <inheritdoc/>
        public override void PrepareDisplayList(double precision)
        {
            // nichts zu tun
        }
        /// <inheritdoc/>
        public override Style.EDefaultFor PreferredStyle
        {
            get
            {
                return Style.EDefaultFor.Text;
            }
        }
        /// <inheritdoc/>
        public override IQuadTreeInsertableZ GetQuadTreeItem(Projection projection, ExtentPrecision extentPrecision)
        {
            //Text2D t2d = new Text2D(this, textString, fontName, fontStyle, Color.Black, projection.UnscaledProjection.Project(location), projection.UnscaledProjection.Project(lineDirection), projection.UnscaledProjection.Project(glyphDirection));
            //return new QuadTreeText(this, t2d, projection);
            return null;
        }
        #endregion
        #region IOctTreeInsertable members
        /// <inheritdoc/>
        public override BoundingCube GetExtent(double precision)
        {
            return GetBoundingCube();
        }
        /// <inheritdoc/>
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
        /// <inheritdoc/>
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
        /// <inheritdoc/>
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
        /// <inheritdoc/>
        public override double Position(GeoPoint fromHere, GeoVector direction, double precision)
        {
            Plane plane = new Plane(location, lineDirection, glyphDirection);
            GeoPoint p = plane.Intersect(fromHere, direction);
            return Geometry.LinePar(fromHere, direction, p); // nicht getestet ob innerhalb des Textes
        }
        #endregion
        #region IColorDef Members
        /// <summary>
        /// The color of the text, see <see cref="IColorDef.ColorDef"/>. Setting it discards the cached display data, raises
        /// the change events as a change of an attribute and is undoable; setting null is ignored. A color with
        /// <see cref="ColorDef.ColorSource.fromParent"/> is replaced by the corresponding color of the <see cref="Block"/>
        /// the text belongs to.
        /// </summary>
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
            try
            {
                columnWidth = info.GetDouble("ColumnWidth");
                lineSpacing = info.GetDouble("LineSpacing");
            }
            catch (SerializationException)
            {   // older files without these entries
                columnWidth = 0.0;
                lineSpacing = 0.0;
            }
        }

        /// <summary>
        /// Writes the data of this object for the legacy binary serialization (<see cref="System.Runtime.Serialization.ISerializable"/>).
        /// Files are written in the JSON format (<see cref="IJsonSerialize"/>) now; the binary format is still read.
        /// </summary>
        /// <param name="info">receives the data</param>
        /// <param name="context">the destination of the serialization</param>
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
            info.AddValue("ColumnWidth", columnWidth);
            info.AddValue("LineSpacing", lineSpacing);

        }

        /// <inheritdoc/>
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
            data.AddProperty("ColumnWidth", columnWidth);
            data.AddProperty("LineSpacing", lineSpacing);
        }

        /// <inheritdoc/>
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
            columnWidth = data.GetPropertyOrDefault<double>("ColumnWidth");
            lineSpacing = data.GetPropertyOrDefault<double>("LineSpacing");
        }
        #endregion
        internal void InvalidateSecondaryData()
        {
            isValidExtent = false;
            displayString = null;
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
