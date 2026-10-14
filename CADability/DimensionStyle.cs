using CADability.GeoObject;
using CADability.UserInterface;
using System;
using System.Runtime.Serialization;

namespace CADability.Attribute
{
    /// <summary>
    /// A named attribute which defines the appearance of <see cref="Dimension"/> objects: the layout of the dimension line,
    /// extension lines and symbols (arrows), the font, size and placement of the dimension text, the number format, units and
    /// tolerances, and which dimension types (points, coordinates, angle, radius, diameter, location) it applies to. Dimension
    /// styles are kept in the <see cref="DimensionStyleList"/> of the project and are presented in the property grid, where they
    /// can be edited.
    /// </summary>
    [Serializable]
    public class DimensionStyle : PropertyEntryImpl, ISerializable, INamedAttribute, ICommandHandler
    {
        // die Einstellungen so aus CONDOR Version 4 übernommen:
        /// <summary>
        /// The dimension types a style may be used for, as a combination of bits; see <see cref="Types"/>. The bits are in the order of
        /// <see cref="Dimension.EDimType"/>, so the bit for a type is <c>1 &lt;&lt; (int)dimType</c>: <c>DimPoints</c> distance between points,
        /// <c>DimCoord</c> coordinate dimensioning, <c>DimAngle</c> angles, <c>DimRadius</c> radii, <c>DimDiameter</c> diameters,
        /// <c>DimLocation</c> labelling a location, <c>DimAllTypes</c> all of them.
        /// </summary>
        [Flags]
        public enum ETypeFlag { DimPoints = 0x1, DimCoord = 0x2, DimAngle = 0x4, DimRadius = 0x8, DimDiameter = 0x10, DimLocation = 0x20, DimAllTypes = 0x3F }; // gültig für diese Typen
        // ETypeFlag muss in der selben Reihenfolge sein wie Dimension.EDimType {DimPoints,DimCoord,DimAngle,DimRadius,DimDiameter};
        /// <summary>
        /// Settings for the dimension text as a combination of bits; see <see cref="TextFlags"/>. Each bit is also available as a boolean
        /// property of the style (<see cref="DimensionStyle.DimTxtOutside"/>, <see cref="DimensionStyle.DimTxtRotate90"/>, ...). Some of the bits date from an older
        /// system and are stored but not evaluated by the current <see cref="Dimension"/> layout; see the individual members.
        /// </summary>
        public enum ETextFlag
        {
            /// <summary>The dimension text should be asked for in a dialog when the dimension is created. Stored only, not evaluated.</summary>
            DimTxtDialog = 0x00000001, // Text soll über Dialog angefragt werden
            /// <summary>The text is always placed outside, beyond the second extension line. See <see cref="DimensionStyle.DimTxtOutside"/>.</summary>
            DimTxtOutside = 0x00000002, // Text immer außerhalb 
            /// <summary>The text is moved outside when it does not fit between the extension lines. See <see cref="DimensionStyle.DimTxtAutomatic"/>.</summary>
            DimTxtAutomatic = 0x00000004, // Text Lage automatisch bestimmen
            /// <summary>The text is rotated by 90 degrees against the dimension line. See <see cref="DimensionStyle.DimTxtRotate90"/>.</summary>
            DimTxtRotate90 = 0x00000008, // Text um 90 Grad drehen
            /// <summary>The text is rotated by 60 degrees against the dimension line. See <see cref="DimensionStyle.DimTxtRotate60"/>.</summary>
            DimTxtRotate60 = 0x00000010, // Text um 60 Grad drehen
            /// <summary>The text is rotated by 90 degrees when it does not fit between the extension lines. See <see cref="DimensionStyle.DimTxtRotateAutomatic"/>.</summary>
            DimTxtRotateAutomatic = 0x00000020, // Text rotieren, wenn zu wenig Platz
            /// <summary>The text size refers to paper coordinates rather than model coordinates. Stored only, not evaluated.</summary>
            DimTxtSizePaper = 0x00000040, // Text Größe bezieht sich auf Papierkoordinaten
            /// <summary>A second (alternate) text in square brackets is shown, scaled by <see cref="ScaleAlt"/>. Stored only, not drawn by the current layout.</summary>
            DimTxtAlt = 0x00000080, // zweiter Text in []
            /// <summary>A rectangle is drawn around the text. See <see cref="DimensionStyle.DimTxtRect"/>.</summary>
            DimTxtRect = 0x00000100, // Rechteck um den Text
            /// <summary>Tolerances are shown as stacked plus/minus texts according to <see cref="PlusTolerance"/> and <see cref="MinusTolerance"/>. See <see cref="DimensionStyle.DimTxtTolerances"/>.</summary>
            DimTxtTolerances = 0x00000200, // Toleranzen ( +- gemäß PlusTolerance,MinusTolerance)
            /// <summary>The value is shown as upper and lower limit computed from <see cref="PlusTolerance"/> and <see cref="MinusTolerance"/>. Stored only, not drawn by the current layout.</summary>
            DimTxtLimits = 0x00000400, // Grenzmaß ( +- gemäß PlusTolerance,MinusTolerance)
            /// <summary>Text placed inside is always horizontal. See <see cref="DimensionStyle.DimTxtInsideHor"/>.</summary>
            DimTxtInsideHor = 0x00000800, // Text immer horizontal wenn innerhalb
            /// <summary>Text placed outside is always horizontal. Stored and exchanged with DXF, not evaluated by the layout.</summary>
            DimTxtOutsideHor = 0x00001000, // Text immer horizontal wenn außerhalb
            /// <summary>The value is written as a fraction; the largest denominator is given by <see cref="Round"/>. See <see cref="DimensionStyle.DimTxtFractional"/>.</summary>
            DimTxtFractional = 0x00002000, // Text mit Bruchdarstellung
            /// <summary>The alternate text is written as a fraction. Stored only, not drawn by the current layout.</summary>
            DimTxtAltFractional = 0x00004000, // zweiter Text mit Bruchdarstellung
            // DimTxtAngle0			=0x00008000, // 3 Flags für Winkel: 0: Grad, dezimal, 1: Grad, Minuten, Sekunden
            // DimTxtAngle1			=0x00010000, // 2: Neugrad (dezimal), 3: Bogenmaß (dezimal)
            /// <summary>German building construction format ("DIN Bau"): millimetres superscript, centimetres normal, metres separated by a point, so that 1.00 is one metre. Selectable in the property page, not implemented in the current layout.</summary>
            DimTxtDinBau = 0x00020000, // mm hochgestellt, cm normal, Meter mit "." 1.00 ist ein Meter
            // DimTxtAngle2			=0x00040000, // [Fortsetzug] 4: Bogenlänge
            /// <summary>Decimal places are always written, padded with zeros up to the precision given by <see cref="Round"/>. See <see cref="DimensionStyle.DimForceTrailingZero"/>.</summary>
            DimForceTrailingZero = 0x00080000, // Dezimalstellen sollen immer angezeigt werden
        };
        // Flags für die Textbehandlung
        /// <summary>
        /// Settings for the dimensioning of points as a combination of bits; see <see cref="PointFlags"/>. <c>DimMultiLine</c> gives each
        /// measured point its own dimension line (see <see cref="DimensionStyle.DimMultiLine"/>), <c>DimNoExtLine</c> suppresses the extension lines
        /// (see <see cref="DimensionStyle.DimNoExtLine"/>). <c>DimMultiPoint</c> is kept from an older system and has no effect.
        /// </summary>
        public enum EPointFlag { DimMultiPoint = 0x1, DimMultiLine = 0x2, DimNoExtLine = 0x4 }; // Flags für Punktbemaßung
        // DimMultiLine und DimMultiPoint findet in CONDOR 4 offensichtlich keine Beachtung
        // deshalb wird es hier so verwendet: wenn DimMultiLine true ist, wird die Koordinaten-
        // Bemaßung mit mehreren Maßlinien implementiert, wenn false nur mit einer
        /// <summary>
        /// Settings for angle dimensioning as a combination of bits; see <see cref="AngleFlags"/>. <c>DimAngleFourPoint</c>: the angle is
        /// defined by two lines of two points each; <c>DimAngleBisection</c>: the half angle is dimensioned. Both are stored and shown in the
        /// property page but not evaluated by the current <see cref="Dimension"/> layout.
        /// </summary>
        public enum EAngleFlag { DimAngleFourPoint = 0x1, DimAngleBisection = 0x2 }; // Flags für Winkelbemaßung
        /// <summary>
        /// The way an angle value is written in the text of an angle dimension; see <see cref="AngleText"/>. <c>DegreeDecimal</c>: decimal
        /// degrees, <c>DegreeMinuteSecond</c>: degrees, minutes and seconds, <c>Grade</c>: gradians (400 per full circle), <c>Radian</c>:
        /// radians, <c>ArcLength</c>: the length of the arc instead of the angle.
        /// </summary>
        public enum EAngleText { DegreeDecimal, DegreeMinuteSecond, Grade, Radian, ArcLength }; // Text für Winkelmaß, Grade ist Neugrad
        /// <summary>
        /// Settings for radius and diameter dimensioning as a combination of bits; see <see cref="RadiusFlags"/>. <c>DimRadiusBend</c>
        /// makes the dimension line bend at the reference point and continue horizontally for the text (see <see cref="DimensionStyle.DimRadiusBend"/>).
        /// </summary>
        public enum ERadiusFlag { DimRadiusBend = 0x1 }; // Flags für Radienbemaßung
        /// <summary>
        /// Settings for the symbols (arrows) as a combination of bits; see <see cref="SymbolFlags"/>. <c>DimSymbolSizePaper</c> means that
        /// <see cref="SymbolSize"/> refers to paper coordinates; it is stored but not evaluated by the current layout.
        /// </summary>
        public enum ESymbolFlag { DimSymbolSizePaper = 0x1 }; // Flags für Symbole
        /// <summary>
        /// Settings for the general appearance of the dimension line and the extension lines as a combination of bits; see
        /// <see cref="GeoFlags"/>. Each bit is also available as a boolean property of the style: <see cref="DimensionStyle.DimNoExtLine1"/>,
        /// <see cref="DimensionStyle.DimNoExtLine2"/>, <see cref="DimensionStyle.DimTextOutsideForceDimensionLine"/>, <see cref="DimensionStyle.DimNoOutsideSymbols"/>,
        /// <see cref="DimensionStyle.DimBreakDimLine"/>, <see cref="DimensionStyle.DimFixedDimLine"/> and <see cref="DimensionStyle.DimNoDimLine"/>.
        /// </summary>
        public enum EGeoFlag
        {
            DimNoExtLine1 = 0x1, DimNoExtLine2 = 0x2, DimTextOutsideForceDimensionLine = 0x4,
            DimNoOutsideSymbols = 0x8, DimBreakDimLine = 0x10, DimFixedDimLine = 0x20, DimNoDimLine = 0x40
        }; // Flags für das allgemeine Aussehen
        /// <summary>
        /// The symbol drawn at the ends of the dimension line; see <see cref="Symbol"/>. <c>DimOpenArrow</c>: two lines forming an
        /// arrow head, <c>DimClosedArrow</c>: outlined arrow head, <c>DimFilledArrow</c>: solid arrow head (a <see cref="Hatch"/> with
        /// <see cref="HatchStyleSolid"/>), <c>DimCircle</c> and <c>DimFilledCircle</c>: a circle with the diameter <see cref="SymbolSize"/>,
        /// <c>DimSlash</c>: a short line crossing the dimension line at 45 degrees, <c>DimSymbol</c>: a user defined symbol, which is not
        /// implemented and draws nothing.
        /// </summary>
        public enum ESymbol
        {
            DimOpenArrow, DimClosedArrow, DimFilledArrow, DimCircle, DimFilledCircle,
            DimSlash, DimSymbol
        }; // Bemaßungssymbol (long)
        /// <summary>
        /// Where the symbols are placed; see <see cref="SymbolPlacement"/>. <c>DimInside</c>: between the extension lines pointing outward,
        /// <c>DimOutside</c>: outside the extension lines pointing inward, <c>DimAutomatic</c>: inside when the distance between the
        /// extension lines is more than twice the symbol width, outside otherwise.
        /// </summary>
        public enum ESymbolPlacement { DimInside, DimOutside, DimAutomatic };
        /// <summary>
        /// Style of the text font as a combination of bits; see <see cref="FontFlags"/> and the boolean properties <see cref="DimensionStyle.DimFontBold"/>,
        /// <see cref="DimensionStyle.DimFontItalic"/>, <see cref="DimensionStyle.DimFontUnderline"/> and <see cref="DimensionStyle.DimFontStrikeOut"/>. The font
        /// style is not applied by the current <see cref="Dimension"/> layout, which only uses <see cref="TextFont"/>, and it is not written to files.
        /// </summary>
        [Flags]
        public enum EFontFlag { DimFontBold = 1, DimFontItalic = 2, DimFontUnderline = 4, DimFontStrikeOut = 8 };

        private string name;
        private string textPrefix;
        private string textPostfix;
        private string textPostfixAlt;
        private EPointFlag pointFlags;
        private EAngleFlag angleFlags;
        private EAngleText angleText;
        private ERadiusFlag radiusFlags;
        private ESymbolFlag symbolFlags;
        private EGeoFlag geoFlags;
        private string textFont; // TextFont für den Maßtext DXF: DIMTXSTY
        private EFontFlag fontFlags; // Bold u.s.w.
        private ColorDef fontColor; // DXF: DIMCLRT
        private ColorDef dimLineColor; // DXF: DIMCLRD Attribute aufgedröselt, oder besser einen Style verwenden? Das wird zyklisch!
        private LineWidth dimLineWidth;
        private ColorDef extLineColor; // DXF: DIMCLRE
        private LineWidth extLineWidth;
        private ETextFlag textFlags; // Flags für die Textbehandlung
        private ETypeFlag types; // gültig für diese Typen (long)
        private double dimLineExtension; // Bemaßungslinien Überstand DXF: DIMDLE
        private double extLineExtension; // Hilfslinien Überstand DXF: DIMEXE
        private double extLineOffset; // Hilfslinien Abstand von Punkt DXF: DIMEXO
        private double lineIncrement; // Abstand aufeinanderfolgender Maßlinien DXF: DIMDLI
        private ESymbol symbol; // Bemaßungssymbol (long)
        // CGeoObject *SymbolLeft,*SymbolRight; // Symbole für linken und rechten "Pfeil", wenn DimSymbol
        private double symbolSize; // Symbolgröße für Standard-Symbole DXF: DIMASZ
        private double scale; // Skalierungsfaktor für den Maßtext DXF: DIMLFAC
        private double round; // Rundungsfaktor, siehe DXF: DIMRND, gibt gleichzeitig Nachkommastellen an
        private double roundAlt; // Rundungsfaktor für alternativ-Text, siehe DXF: DIMALTD, dort nur Anzahl der Nachkommastellen
        // ist DimTxtFractional bzw. DimTxtAltFractional gesetzt, so kann Round die
        // Werte 1,2,4,8,16,32,64 annehmen und bedeuten dann den kleinsten Nenner für den Bruch
        // in DXF ist das nur global über $LUNITS, $LUPREC möglich
        private double textSize; // Textgröße für Bemaßungstext //DXF: DIMTXT
        private double textSizeTol; // Textgröße für Toleranz (hoch/tief gestellt)
        private double textDist; // Text Abstand von der Maßlinie
        private double plusTolerance; // Toleranzwert Plus
        private double minusTolerance; // Toleranzwert Minus
        private double centerMarkSize; // Größe der Zentrierungsmarke DXF: DIMCEN
        private double scaleAlt; // Skalierungsfaktor für Alternativtext DXF: DIMALTF
        private double dimensionLineGap; // Abstand zw. Text un unterbrochener Maßlinie DXF: DIMGAP
        private ESymbolPlacement symbolPlacement; // Flags für die Ausführung
        private ColorDef fillColor; // Füllfarbe für Pfeile und Kreise
        private Layer fixedLayer; // wenn nicht NULL, dann alles in diese Ebene, z.Z. nicht benutzt
        private UserData userData; // UserData sollte auch für die anderen Stile implementiert werden, genutzt für DXF import-export
        /// <summary>
        /// Handler for <see cref="PropertyChangedEvent"/>.
        /// </summary>
        /// <param name="sender">The dimension style whose property changed</param>
        /// <param name="propertyName">The name of the changed property, e.g. "TextSize"</param>
        /// <param name="newValue">The new value of the property (boxed)</param>
        /// <param name="oldValue">The value the property had before (boxed)</param>
        public delegate void PropertyChangedDelegate(DimensionStyle sender, string propertyName, object newValue, object oldValue);
        /// <summary>
        /// Raised by the setter of every property of the style except <see cref="Name"/>, after the value has been changed. The change is
        /// also reported to the <see cref="DimensionStyleList"/> containing the style and from there to the project or the global settings.
        /// Dimensions already drawn with this style are not recalculated automatically; call <see cref="Dimension.Recalc()"/> on them or use
        /// the "update all dimensions" command of the list.
        /// </summary>
        public event PropertyChangedDelegate PropertyChangedEvent;

        private HatchStyleSolid hatchStyleSolid; // nicht persistent, ein HatchStyle für gefüllte Pfeile

        private DimensionStyleList parent; // die Liste, in der dieser DimensionStyle steckt.
        internal new DimensionStyleList Parent
        {
            get => parent;
            set => parent = value;
        }
        IAttributeList INamedAttribute.Parent
        {
            get { return parent; }
            set { parent = value as DimensionStyleList; }
        }
        IPropertyEntry INamedAttribute.GetSelectionProperty(string key, Project project, GeoObjectList geoObjectList)
        {
            return null;
        }
        /// <summary>
        /// Creates a dimension style with no name and all settings unset (null strings, zero sizes, no colors). Since many settings
        /// have no useful zero value, a new style is usually obtained by cloning an existing one (<see cref="Clone"/>), e.g. the
        /// <see cref="DimensionStyleList.Current"/> style of the project's <see cref="Project.DimensionStyleList"/>.
        /// </summary>
        public DimensionStyle()
        {
            // 
            // TODO: Add constructor logic here
            //
        }

        internal static DimensionStyle GetDefault()
        {
            DimensionStyle res = new DimensionStyle();
            res.textPrefix = "";
            res.textPostfix = "";
            res.textPostfixAlt = "";
            res.pointFlags = (EPointFlag)(0);
            res.angleFlags = (EAngleFlag)(0);
            res.radiusFlags = (ERadiusFlag)(0);
            res.symbolFlags = (ESymbolFlag)(0);
            res.geoFlags = (EGeoFlag)0;
            res.fontFlags = (EFontFlag)(0);
            res.textFont = "Arial";
            res.dimLineColor = ColorDef.GetDefault();
            res.dimLineWidth = null;
            res.extLineColor = res.dimLineColor;
            res.fontColor = null;
            res.extLineWidth = null;
            res.textFlags = (ETextFlag)(0);
            res.types = ETypeFlag.DimAllTypes;
            res.dimLineExtension = 0.0;
            res.extLineExtension = 0.0;
            res.extLineOffset = 0.0;
            res.lineIncrement = 0.0;
            res.symbol = ESymbol.DimFilledArrow;
            res.symbolSize = 3;
            res.scale = 1.0;
            res.round = 0.001;
            res.roundAlt = 0.001;
            res.textSize = 5;
            res.textSizeTol = 3;
            res.textDist = 0.0;
            res.plusTolerance = 0.0;
            res.minusTolerance = 0.0;
            res.centerMarkSize = 2;
            res.scaleAlt = 1.0;
            res.dimensionLineGap = 0.0;
            res.symbolPlacement = ESymbolPlacement.DimAutomatic;
            res.fillColor = res.dimLineColor;
            res.fixedLayer = null;
            res.name = StringTable.GetString("DimensionStyle.StandardName");

            return res;
        }
        /// <summary>
        /// Returns a copy of this dimension style with the same name and settings. The copy is not contained in any list, so it can be
        /// renamed and added to a <see cref="DimensionStyleList"/>. The copy shares the color, line width and layer objects with the original.
        /// <see cref="AngleText"/>, <see cref="UserData"/> and <see cref="HatchStyleSolid"/> are not copied: the copy starts with
        /// <see cref="EAngleText.DegreeDecimal"/>, empty user data and a hatch style of its own.
        /// </summary>
        /// <returns>The copy</returns>
        public DimensionStyle Clone()
        {
            DimensionStyle res = new DimensionStyle();
            res.name = name;
            res.textPrefix = textPrefix;
            res.textPostfix = textPostfix;
            res.textPostfixAlt = textPostfixAlt;
            res.pointFlags = pointFlags;
            res.angleFlags = angleFlags;
            res.radiusFlags = radiusFlags;
            res.symbolFlags = symbolFlags;
            res.geoFlags = geoFlags;
            res.textFont = textFont;
            res.fontFlags = fontFlags;
            res.fontColor = fontColor;
            res.dimLineColor = dimLineColor;
            res.dimLineWidth = dimLineWidth;
            res.extLineColor = extLineColor;
            res.extLineWidth = extLineWidth;
            res.textFlags = textFlags;
            res.types = types;
            res.dimLineExtension = dimLineExtension;
            res.extLineExtension = extLineExtension;
            res.extLineOffset = extLineOffset;
            res.lineIncrement = lineIncrement;
            res.symbol = symbol;
            res.symbolSize = symbolSize;
            res.scale = scale;
            res.round = round;
            res.roundAlt = roundAlt;
            res.textSize = textSize;
            res.textSizeTol = textSizeTol;
            res.textDist = textDist;
            res.plusTolerance = plusTolerance;
            res.minusTolerance = minusTolerance;
            res.centerMarkSize = centerMarkSize;
            res.scaleAlt = scaleAlt;
            res.dimensionLineGap = dimensionLineGap;
            res.symbolPlacement = symbolPlacement;
            res.fillColor = fillColor;
            res.fixedLayer = fixedLayer;
            res.angleText = angleText;
            res.userData = userData?.Clone();
            return res;
        }
        internal void Update(bool AddMissingToList)
        {
            if (parent != null && parent.Owner != null)
            {
                ColorList cl = parent.Owner.ColorList;
                if (cl != null)
                {
                    if (fontColor != null)
                    {
                        ColorDef cd = cl.Find(fontColor.Name);
                        if (cd != null)
                            fontColor = cd;
                        else if (AddMissingToList)
                            cl.Add(fontColor);
                    }
                    if (dimLineColor != null)
                    {
                        ColorDef cd = cl.Find(dimLineColor.Name);
                        if (cd != null)
                            dimLineColor = cd;
                        else if (AddMissingToList)
                            cl.Add(dimLineColor);

                    }
                    if (extLineColor != null)
                    {
                        ColorDef cd = cl.Find(extLineColor.Name);
                        if (cd != null)
                            extLineColor = cd;
                        else if (AddMissingToList)
                            cl.Add(extLineColor);
                    }
                    if (fillColor != null)
                    {
                        ColorDef cd = cl.Find(fillColor.Name);
                        if (cd != null)
                            fillColor = cd;
                        else if (AddMissingToList)
                            cl.Add(fillColor);
                    }
                }

                LineWidthList lwl = parent.Owner.LineWidthList;
                if (lwl != null)
                {
                    if (dimLineWidth != null)
                    {
                        LineWidth lw = lwl.Find(dimLineWidth.Name);
                        if (lw != null)
                            dimLineWidth = lw;
                        else if (AddMissingToList)
                            lwl.Add(dimLineWidth);
                    }
                    if (extLineWidth != null)
                    {
                        LineWidth lw = lwl.Find(extLineWidth.Name);
                        if (lw != null)
                            extLineWidth = lw;
                        else if (AddMissingToList)
                            lwl.Add(extLineWidth);
                    }
                }

                LayerList ll = parent.Owner.LayerList;
                if (ll != null)
                {
                    if (fixedLayer != null)
                    {
                        Layer l = ll.Find(fixedLayer.Name);
                        if (l != null)
                            fixedLayer = l;
                        else if (AddMissingToList)
                            ll.Add(fixedLayer);
                    }

                }

                HatchStyleList hsl = parent.Owner.HatchStyleList;
                if (hsl != null)
                {
                    if (HatchStyleSolid != null)
                    {
                        HatchStyleSolid hss = hsl.Find(hatchStyleSolid.Name) as HatchStyleSolid;
                        if (hss != null)
                            hatchStyleSolid = hss;
                        else if (AddMissingToList)
                            hsl.Add(hatchStyleSolid);
                    }
                }
            }
        }

        #region Properties

        private void FireDidChange(string propertyName, object propertyNewValue, object propertyOldValue)
        {
            if (parent != null)
            {
                ReversibleChange change = new ReversibleChange(this, propertyName, propertyOldValue);
                (parent as IAttributeList).AttributeChanged(this, change);
            }
            if (PropertyChangedEvent != null) PropertyChangedEvent(this, propertyName, propertyNewValue, propertyOldValue);
        }
        /// <summary>
        /// The name of the style, which identifies it in its <see cref="DimensionStyleList"/> and in the property grid. When the style is
        /// contained in a list, the name must be unique in that list: setting a name that another style of the list already has throws a
        /// <see cref="NameAlreadyExistsException"/>, otherwise the list is re-keyed. Changing the name does not raise <see cref="PropertyChangedEvent"/>.
        /// </summary>
        public string Name
        {
            get
            {
                return name;
            }
            set
            {
                if (parent != null && !(parent as IAttributeList).MayChangeName(this, value))
                {
                    throw new NameAlreadyExistsException(parent, this, value, name);
                }
                string OldName = name;
                name = value;
                if (parent != null) (parent as IAttributeList).NameChanged(this, OldName);
            }
        }
        /// <summary>
        /// Text written in front of the measured value, e.g. "R" or "Ø". A <see cref="Dimension"/> uses it unless it has a prefix of its own
        /// (see <see cref="Dimension.GetPrefix(int)"/>). Setting it raises <see cref="PropertyChangedEvent"/>.
        /// </summary>
        public string TextPrefix
        {
            get
            {
                return textPrefix;
            }
            set
            {
                string OldValue = TextPrefix;
                textPrefix = value;
                FireDidChange("TextPrefix", value, OldValue);
            }
        }
        /// <summary>
        /// Text appended after the measured value, e.g. a unit. A <see cref="Dimension"/> uses it unless it has a postfix of its own
        /// (see <see cref="Dimension.GetPostfix(int)"/>). Setting it raises <see cref="PropertyChangedEvent"/>.
        /// </summary>
        public string TextPostfix
        {
            get
            {
                return textPostfix;
            }
            set
            {
                string OldValue = TextPostfix;
                textPostfix = value;
                FireDidChange("TextPostfix", value, OldValue);
            }
        }
        /// <summary>
        /// Text appended after the alternate (second) value (see <see cref="DimTxtAlt"/>). A <see cref="Dimension"/> returns it from
        /// <see cref="Dimension.GetPostfixAlt(int)"/> when it has no alternate postfix of its own, but the current layout does not draw the
        /// alternate text. Setting it raises <see cref="PropertyChangedEvent"/>.
        /// </summary>
        public string TextPostfixAlt
        {
            get
            {
                return textPostfixAlt;
            }
            set
            {
                string OldValue = TextPostfixAlt;
                textPostfixAlt = value;
                FireDidChange("TextPostfixAlt", value, OldValue);
            }
        }
        /// <summary>
        /// The settings for point dimensioning as a combination of <see cref="EPointFlag"/> bits. The same bits are available as the
        /// boolean properties <see cref="DimMultiLine"/> and <see cref="DimNoExtLine"/>. Setting it raises <see cref="PropertyChangedEvent"/>.
        /// </summary>
        public EPointFlag PointFlags
        {
            get
            {
                return pointFlags;
            }
            set
            {
                EPointFlag OldValue = PointFlags;
                pointFlags = value;
                FireDidChange("PointFlags", value, OldValue);
            }
        }
        /// <summary>
        /// The settings for angle dimensioning as a combination of <see cref="EAngleFlag"/> bits, also available as
        /// <see cref="DimAngleFourPoint"/> and <see cref="DimAngleBisection"/>. Setting it raises <see cref="PropertyChangedEvent"/>.
        /// </summary>
        public EAngleFlag AngleFlags
        {
            get
            {
                return angleFlags;
            }
            set
            {
                EAngleFlag OldValue = AngleFlags;
                angleFlags = value;
                FireDidChange("AngleFlags", value, OldValue);
            }
        }
        /// <summary>
        /// The settings for radius and diameter dimensioning as a combination of <see cref="ERadiusFlag"/> bits, also available as
        /// <see cref="DimRadiusBend"/>. Setting it raises <see cref="PropertyChangedEvent"/>.
        /// </summary>
        public ERadiusFlag RadiusFlags
        {
            get
            {
                return radiusFlags;
            }
            set
            {
                ERadiusFlag OldValue = RadiusFlags;
                radiusFlags = value;
                FireDidChange("RadiusFlags", value, OldValue);
            }
        }
        /// <summary>
        /// The settings for the symbols as a combination of <see cref="ESymbolFlag"/> bits. Stored only, not evaluated by the current
        /// layout. Setting it raises <see cref="PropertyChangedEvent"/>.
        /// </summary>
        public ESymbolFlag SymbolFlags
        {
            get
            {
                return symbolFlags;
            }
            set
            {
                ESymbolFlag OldValue = SymbolFlags;
                symbolFlags = value;
                FireDidChange("SymbolFlags", value, OldValue);
            }
        }
        /// <summary>
        /// The settings for the dimension line and the extension lines as a combination of <see cref="EGeoFlag"/> bits. The same bits are
        /// available as boolean properties (<see cref="DimNoDimLine"/>, <see cref="DimBreakDimLine"/>, ...). Setting it raises <see cref="PropertyChangedEvent"/>.
        /// </summary>
        public EGeoFlag GeoFlags
        {
            get
            {
                return geoFlags;
            }
            set
            {
                EGeoFlag OldValue = GeoFlags;
                geoFlags = value;
                FireDidChange("GeoFlags", value, OldValue);
            }
        }
        /// <summary>
        /// The style of the text font as a combination of <see cref="EFontFlag"/> values, also available as <see cref="DimFontBold"/>,
        /// <see cref="DimFontItalic"/>, <see cref="DimFontUnderline"/> and <see cref="DimFontStrikeOut"/>. The font style is not applied by
        /// the current <see cref="Dimension"/> layout. Setting it raises <see cref="PropertyChangedEvent"/>.
        /// </summary>
        public EFontFlag FontFlags
        {
            get
            {
                return fontFlags;
            }
            set
            {
                EFontFlag OldValue = FontFlags;
                fontFlags = value;
                FireDidChange("FontFlags", value, OldValue);
            }
        }
        /// <summary>
        /// The name of the font family used for the dimension texts, e.g. "Arial" (the default); corresponds to the DXF text style of a
        /// dimension style. The property page offers the font families installed on the system. Setting it raises <see cref="PropertyChangedEvent"/>.
        /// </summary>
        public string TextFont
        {
            get
            {
                return textFont;
            }
            set
            {
                string OldValue = TextFont;
                textFont = value;
                FireDidChange("TextFont", value, OldValue);
            }
        }
        /// <summary>
        /// The color of the dimension line, of the open and closed arrow symbols, of the text frame (<see cref="DimTxtRect"/>) and of the
        /// pieces of dimension line drawn behind outside symbols. The default is the default color of the <see cref="ColorList"/>. Setting it raises <see cref="PropertyChangedEvent"/>.
        /// </summary>
        public ColorDef DimLineColor
        {
            get
            {
                return dimLineColor;
            }
            set
            {
                ColorDef OldValue = DimLineColor;
                dimLineColor = value;
                FireDidChange("DimLineColor", value, OldValue);
            }
        }
        /// <summary>
        /// The line width of the dimension line and of the symbols. null means the default line width. Setting it raises <see cref="PropertyChangedEvent"/>.
        /// </summary>
        public LineWidth DimLineWidth
        {
            get
            {
                return dimLineWidth;
            }
            set
            {
                LineWidth OldValue = dimLineWidth;
                dimLineWidth = value;
                FireDidChange("DimLineWidth", value, OldValue);
            }
        }
        /// <summary>
        /// The color of the extension lines (the lines from the measured points to the dimension line). Setting it raises <see cref="PropertyChangedEvent"/>.
        /// </summary>
        public ColorDef ExtLineColor
        {
            get
            {
                return extLineColor;
            }
            set
            {
                ColorDef OldValue = ExtLineColor;
                extLineColor = value;
                FireDidChange("ExtLineColor", value, OldValue);
            }
        }
        /// <summary>
        /// The color of the dimension texts. null (the default) means no explicit color: the text takes the color from its parent. Setting it raises <see cref="PropertyChangedEvent"/>.
        /// </summary>
        public ColorDef FontColor
        {
            get
            {
                return fontColor;
            }
            set
            {
                ColorDef OldValue = FontColor;
                fontColor = value;
                FireDidChange("FontColor", value, OldValue);
            }
        }
        /// <summary>
        /// The line width of the extension lines. null means the default line width. Setting it raises <see cref="PropertyChangedEvent"/>.
        /// </summary>
        public LineWidth ExtLineWidth
        {
            get
            {
                return extLineWidth;
            }
            set
            {
                LineWidth OldValue = ExtLineWidth;
                extLineWidth = value;
                FireDidChange("ExtLineWidth", value, OldValue);
            }
        }
        /// <summary>
        /// The settings for the dimension text as a combination of <see cref="ETextFlag"/> bits. The same bits are available as boolean
        /// properties (<see cref="DimTxtOutside"/>, <see cref="DimTxtRotate90"/>, <see cref="DimTxtTolerances"/>, ...). Setting it raises <see cref="PropertyChangedEvent"/>.
        /// </summary>
        public ETextFlag TextFlags
        {
            get
            {
                return textFlags;
            }
            set
            {
                ETextFlag OldValue = TextFlags;
                textFlags = value;
                FireDidChange("TextFlags", value, OldValue);
            }
        }
        /// <summary>
        /// The dimension types this style may be used for, as a combination of <see cref="ETypeFlag"/> bits (bit <c>1 &lt;&lt; (int)dimType</c>
        /// for a <see cref="Dimension.EDimType"/>). The same bits are available as <see cref="DimPoints"/>, <see cref="DimCoord"/>,
        /// <see cref="DimAngle"/>, <see cref="DimRadius"/>, <see cref="DimDiameter"/> and <see cref="DimLocation"/>. The
        /// <see cref="DimensionStyleSelectionProperty"/> only offers styles whose types include the type of the dimension being edited. The default
        /// is <see cref="ETypeFlag.DimAllTypes"/>. Setting it raises <see cref="PropertyChangedEvent"/>.
        /// </summary>
        public ETypeFlag Types
        {
            get
            {
                return types;
            }
            set
            {
                ETypeFlag OldValue = Types;
                types = value;
                FireDidChange("Types", value, OldValue);
            }
        }
        /// <summary>
        /// The length by which the dimension line of a point dimension is extended beyond the outermost extension lines, in model units
        /// (DXF: DIMDLE). 0 means the dimension line ends at the extension lines. Setting it raises <see cref="PropertyChangedEvent"/>.
        /// </summary>
        public double DimLineExtension
        {
            get
            {
                return dimLineExtension;
            }
            set
            {
                double OldValue = DimLineExtension;
                dimLineExtension = value;
                FireDidChange("DimLineExtension", value, OldValue);
            }
        }
        /// <summary>
        /// The length by which an extension line runs past the dimension line, in model units (DXF: DIMEXE). With
        /// <see cref="DimNoExtLine"/> set, only a short piece of this length on both sides of the dimension line is drawn (none when 0). Setting it raises <see cref="PropertyChangedEvent"/>.
        /// </summary>
        public double ExtLineExtension
        {
            get
            {
                return extLineExtension;
            }
            set
            {
                double OldValue = ExtLineExtension;
                extLineExtension = value;
                FireDidChange("ExtLineExtension", value, OldValue);
            }
        }
        /// <summary>
        /// The gap between the measured point and the start of its extension line, in model units (DXF: DIMEXO). Setting it raises <see cref="PropertyChangedEvent"/>.
        /// </summary>
        public double ExtLineOffset
        {
            get
            {
                return extLineOffset;
            }
            set
            {
                double OldValue = ExtLineOffset;
                extLineOffset = value;
                FireDidChange("ExtLineOffset", value, OldValue);
            }
        }
        /// <summary>
        /// The distance between successive dimension lines, in model units, when a dimension with several points is drawn with one
        /// dimension line per point (<see cref="DimMultiLine"/>; DXF: DIMDLI). Multi line dimensioning needs a value greater than 0. Setting it raises <see cref="PropertyChangedEvent"/>.
        /// </summary>
        public double LineIncrement
        {
            get
            {
                return lineIncrement;
            }
            set
            {
                double OldValue = LineIncrement;
                lineIncrement = value;
                FireDidChange("LineIncrement", value, OldValue);
            }
        }
        /// <summary>
        /// The symbol (arrow head, circle, slash) drawn at the ends of the dimension line; see <see cref="ESymbol"/>. The default is
        /// <see cref="ESymbol.DimFilledArrow"/>. Setting it raises <see cref="PropertyChangedEvent"/>.
        /// </summary>
        public ESymbol Symbol
        {
            get
            {
                return symbol;
            }
            set
            {
                ESymbol OldValue = Symbol;
                symbol = value;
                FireDidChange("Symbol", value, OldValue);
            }
        }
        /// <summary>
        /// The size of the symbols in model units: the width of an arrow head, the diameter of a circle, the extent of a slash along the dimension line (DXF: DIMASZ).
        /// Like the text size it is multiplied by the layout factor of the view. Setting it raises <see cref="PropertyChangedEvent"/>.
        /// </summary>
        public double SymbolSize
        {
            get
            {
                return symbolSize;
            }
            set
            {
                double OldValue = SymbolSize;
                symbolSize = value;
                FireDidChange("SymbolSize", value, OldValue);
            }
        }
        /// <summary>
        /// The factor by which a measured length, radius or diameter is multiplied before it is formatted as text (DXF: DIMLFAC), e.g. to
        /// dimension in a unit other than the model unit. The default is 1. Setting it raises <see cref="PropertyChangedEvent"/>.
        /// </summary>
        public double Scale
        {
            get
            {
                return scale;
            }
            set
            {
                double OldValue = Scale;
                scale = value;
                FireDidChange("Scale", value, OldValue);
            }
        }
        /// <summary>
        /// The precision of the dimension text (DXF: DIMRND). 0 means full precision. A value between 0 and 1 (1.0, 0.5, 0.25, 0.1, 0.05,
        /// 0.025, 0.01, 0.001, ...) rounds the value to a multiple of it and shows as many decimals as the value needs (all of them with
        /// <see cref="DimForceTrailingZero"/>). A value greater than 1 (2, 4, 8, 16, 32, 64) writes the value as a fraction with this largest
        /// denominator (see <see cref="DimTxtFractional"/>). -1 selects the "DIN Bau" entry of the property page (see <see cref="DimTxtDinBau"/>).
        /// The default is 0.001. Setting it raises <see cref="PropertyChangedEvent"/>.
        /// </summary>
        public double Round
        {
            get
            {
                return round;
            }
            set
            {
                double OldValue = Round;
                round = value;
                FireDidChange("Round", value, OldValue);
            }
        }
        /// <summary>
        /// The precision of the alternate (second) text, with the same meaning as <see cref="Round"/> (DXF: DIMALTD). Stored and taken
        /// from DXF, but the current layout does not draw the alternate text. Setting it raises <see cref="PropertyChangedEvent"/>.
        /// </summary>
        public double RoundAlt
        {
            get
            {
                return roundAlt;
            }
            set
            {
                double OldValue = RoundAlt;
                roundAlt = value;
                FireDidChange("RoundAlt", value, OldValue);
            }
        }
        /// <summary>
        /// The height of the dimension text in model units (DXF: DIMTXT), multiplied by the layout factor of the view. It is also the
        /// unit of <see cref="TextDist"/>. The default is 5. Setting it raises <see cref="PropertyChangedEvent"/>.
        /// </summary>
        public double TextSize
        {
            get
            {
                return textSize;
            }
            set
            {
                double OldValue = TextSize;
                textSize = value;
                FireDidChange("TextSize", value, OldValue);
            }
        }
        /// <summary>
        /// The height of the tolerance texts (<see cref="DimTxtTolerances"/>) in model units, usually smaller than <see cref="TextSize"/>.
        /// The default is 3. Setting it raises <see cref="PropertyChangedEvent"/>.
        /// </summary>
        public double TextSizeTol
        {
            get
            {
                return textSizeTol;
            }
            set
            {
                double OldValue = TextSizeTol;
                textSizeTol = value;
                FireDidChange("TextSizeTol", value, OldValue);
            }
        }
        /// <summary>
        /// The distance of the text from the dimension line as a multiple of <see cref="TextSize"/>: 0 centers the text on the dimension
        /// line, 1 places it one text height above it (for angle dimensions: outward from the arc). Setting it raises <see cref="PropertyChangedEvent"/>.
        /// </summary>
        public double TextDist
        {
            get
            {
                return textDist;
            }
            set
            {
                double OldValue = TextDist;
                textDist = value;
                FireDidChange("TextDist", value, OldValue);
            }
        }
        /// <summary>
        /// The upper tolerance shown after the value when <see cref="DimTxtTolerances"/> is set, in the unit of the dimension text. It is
        /// written with its sign, a positive value gets a leading "+". Setting it raises <see cref="PropertyChangedEvent"/>.
        /// </summary>
        public double PlusTolerance
        {
            get
            {
                return plusTolerance;
            }
            set
            {
                double OldValue = PlusTolerance;
                plusTolerance = value;
                FireDidChange("PlusTolerance", value, OldValue);
            }
        }
        /// <summary>
        /// The lower tolerance shown after the value when <see cref="DimTxtTolerances"/> is set, in the unit of the dimension text. It is
        /// written with its sign, so a lower tolerance is given as a negative number, e.g. -0.05. Setting it raises <see cref="PropertyChangedEvent"/>.
        /// </summary>
        public double MinusTolerance
        {
            get
            {
                return minusTolerance;
            }
            set
            {
                double OldValue = MinusTolerance;
                minusTolerance = value;
                FireDidChange("MinusTolerance", value, OldValue);
            }
        }
        /// <summary>
        /// The size of the center mark of a radius or diameter dimension in model units (DXF: DIMCEN). Stored and exchanged with DXF, but
        /// the current layout does not draw a center mark. Setting it raises <see cref="PropertyChangedEvent"/>.
        /// </summary>
        public double CenterMarkSize
        {
            get
            {
                return centerMarkSize;
            }
            set
            {
                double OldValue = CenterMarkSize;
                centerMarkSize = value;
                FireDidChange("CenterMarkSize", value, OldValue);
            }
        }
        /// <summary>
        /// The factor applied to the measured value for the alternate (second) text (DXF: DIMALTF), e.g. 25.4 to show inches in
        /// brackets behind millimetres. Stored and taken from DXF, but the current layout does not draw the alternate text. Setting it raises <see cref="PropertyChangedEvent"/>.
        /// </summary>
        public double ScaleAlt
        {
            get
            {
                return scaleAlt;
            }
            set
            {
                double OldValue = ScaleAlt;
                scaleAlt = value;
                FireDidChange("ScaleAlt", value, OldValue);
            }
        }
        /// <summary>
        /// The margin around the text, in model units, that is kept free of the dimension line when it is broken at the text
        /// (<see cref="DimBreakDimLine"/>) and that the text frame (<see cref="DimTxtRect"/>) is drawn with (DXF: DIMGAP). Setting it raises <see cref="PropertyChangedEvent"/>.
        /// </summary>
        public double DimensionLineGap
        {
            get
            {
                return dimensionLineGap;
            }
            set
            {
                double OldValue = DimensionLineGap;
                dimensionLineGap = value;
                FireDidChange("DimensionLineGap", value, OldValue);
            }
        }
        /// <summary>
        /// Whether the symbols are drawn inside or outside the extension lines, or chosen automatically depending on the available space;
        /// see <see cref="ESymbolPlacement"/>. The default is <see cref="ESymbolPlacement.DimAutomatic"/>. Setting it raises <see cref="PropertyChangedEvent"/>.
        /// </summary>
        public ESymbolPlacement SymbolPlacement
        {
            get
            {
                return symbolPlacement;
            }
            set
            {
                ESymbolPlacement OldValue = SymbolPlacement;
                symbolPlacement = value;
                FireDidChange("SymbolPlacement", value, OldValue);
            }
        }
        /// <summary>
        /// The color of the solid symbols (filled arrow, filled circle), of the circle and of the slash symbol. The color of the filled
        /// symbols is captured in <see cref="HatchStyleSolid"/> when that is first used. The default is <see cref="DimLineColor"/>. Setting it raises <see cref="PropertyChangedEvent"/>.
        /// </summary>
        public ColorDef FillColor
        {
            get
            {
                return fillColor;
            }
            set
            {
                ColorDef OldValue = FillColor;
                fillColor = value;
                FireDidChange("FillColor", value, OldValue);
            }
        }
        /// <summary>
        /// A layer onto which all parts of the dimension would be placed, null for no fixed layer. Currently not used by the layout. Setting it raises <see cref="PropertyChangedEvent"/>.
        /// </summary>
        public Layer FixedLayer
        {
            get
            {
                return fixedLayer;
            }
            set
            {
                Layer OldValue = FixedLayer;
                fixedLayer = value;
                FireDidChange("FixedLayer", value, OldValue);
            }
        }
        /// <summary>
        /// A solid hatch style with the <see cref="FillColor"/> of this style, used for the filled arrow heads and circles. It is created on
        /// first access, named after the style with the suffix " [DimensionStyleFillSolid]", and is not persisted. It keeps the fill color
        /// it was created with; a later change of <see cref="FillColor"/> does not update it.
        /// </summary>
        public HatchStyleSolid HatchStyleSolid
        {
            get
            {
                if (hatchStyleSolid == null)
                {
                    hatchStyleSolid = new HatchStyleSolid();
                    hatchStyleSolid.Name = this.name + " [DimensionStyleFillSolid]";
                    hatchStyleSolid.Color = FillColor;
                }
                return hatchStyleSolid;
            }
        }
        /// <summary>
        /// Free user data attached to this style, e.g. by file import or by an application. It is created on first access and saved with
        /// the style, but not copied by <see cref="Clone"/>.
        /// </summary>
        public UserData UserData
        {
            get
            {
                if (userData == null) userData = new UserData();
                return userData;
            }
        }
        /// <summary>
        /// Checks whether this dimensionstyle has the same properties as the other dimension style.
        /// Name equality is not checked.
        /// </summary>
        /// <param name="other">The other dimension style</param>
        /// <returns>true if equal</returns>
        public bool SameData(DimensionStyle other)
        {
            if (textPrefix != other.textPrefix) return false;
            if (textPostfix != other.textPostfix) return false;
            if (textPostfixAlt != other.textPostfixAlt) return false;
            if (pointFlags != other.pointFlags) return false;
            if (angleFlags != other.angleFlags) return false;
            if (angleText != other.angleText) return false;
            if (radiusFlags != other.radiusFlags) return false;
            if (symbolFlags != other.symbolFlags) return false;
            if (geoFlags != other.geoFlags) return false;
            if (textFont != other.textFont) return false;
            if (fontFlags != other.fontFlags) return false;
            if (fontColor != other.fontColor) return false;
            if (dimLineColor != other.dimLineColor) return false;
            if (dimLineWidth != other.dimLineWidth) return false;
            if (extLineColor != other.extLineColor) return false;
            if (extLineWidth != other.extLineWidth) return false;
            if (textFlags != other.textFlags) return false;
            if (types != other.types) return false;
            if (dimLineExtension != other.dimLineExtension) return false;
            if (extLineExtension != other.extLineExtension) return false;
            if (extLineOffset != other.extLineOffset) return false;
            if (lineIncrement != other.lineIncrement) return false;
            if (symbol != other.symbol) return false;
            if (symbolSize != other.symbolSize) return false;
            if (scale != other.scale) return false;
            if (round != other.round) return false;
            if (roundAlt != other.roundAlt) return false;
            if (textSize != other.textSize) return false;
            if (textSizeTol != other.textSizeTol) return false;
            if (textDist != other.textDist) return false;
            if (plusTolerance != other.plusTolerance) return false;
            if (minusTolerance != other.minusTolerance) return false;
            if (centerMarkSize != other.centerMarkSize) return false;
            if (scaleAlt != other.scaleAlt) return false;
            if (dimensionLineGap != other.dimensionLineGap) return false;
            if (symbolPlacement != other.symbolPlacement) return false;
            if (fillColor != other.fillColor) return false;
            if (fixedLayer != other.fixedLayer) return false;
            if (hatchStyleSolid != other.hatchStyleSolid) return false;
            return true;
        }
        #endregion
        #region Flags als boolen properties
        /// <summary>
        /// No extension lines are drawn from the measured points to the dimension line; only a short piece of <see cref="ExtLineExtension"/>
        /// length across the dimension line remains when that is not 0 (<see cref="EPointFlag.DimNoExtLine"/>). Setting it raises <see cref="PropertyChangedEvent"/>.
        /// </summary>
        public bool DimNoExtLine
        {
            get
            {
                return (pointFlags & EPointFlag.DimNoExtLine) != 0;
            }
            set
            {
                bool OldValue = DimNoExtLine;
                if (value) pointFlags |= EPointFlag.DimNoExtLine;
                else pointFlags &= ~EPointFlag.DimNoExtLine;
                FireDidChange("DimNoExtLine", value, OldValue);
            }
        }
        /// <summary>
        /// A point or coordinate dimension with more than two points is drawn with one dimension line per point: the first point is the
        /// reference, every further point gets its own dimension line at a distance of <see cref="LineIncrement"/> from the previous one
        /// (needs <see cref="LineIncrement"/> greater than 0). When false, the points are dimensioned in a single chain on one dimension
        /// line (<see cref="EPointFlag.DimMultiLine"/>). Setting it raises <see cref="PropertyChangedEvent"/>.
        /// </summary>
        public bool DimMultiLine
        {
            get
            {
                return (pointFlags & EPointFlag.DimMultiLine) != 0;
            }
            set
            {
                bool OldValue = DimMultiLine;
                if (value) pointFlags |= EPointFlag.DimMultiLine;
                else pointFlags &= ~EPointFlag.DimMultiLine;
                FireDidChange("DimMultiLine", value, OldValue);
            }
        }
        /// <summary>
        /// The first extension line is suppressed (<see cref="EGeoFlag.DimNoExtLine1"/>, DXF: DIMSE1). Stored and exchanged with DXF, not
        /// evaluated by the current layout, which only knows <see cref="DimNoExtLine"/>. Setting it raises <see cref="PropertyChangedEvent"/>.
        /// </summary>
        public bool DimNoExtLine1
        {
            get
            {
                return (geoFlags & EGeoFlag.DimNoExtLine1) != 0;
            }
            set
            {
                bool OldValue = DimNoExtLine1;
                if (value) geoFlags |= EGeoFlag.DimNoExtLine1;
                else geoFlags &= ~EGeoFlag.DimNoExtLine1;
                FireDidChange("DimNoExtLine1", value, OldValue);
            }
        }
        /// <summary>
        /// The second extension line is suppressed (<see cref="EGeoFlag.DimNoExtLine2"/>, DXF: DIMSE2). Stored and exchanged with DXF, not
        /// evaluated by the current layout, which only knows <see cref="DimNoExtLine"/>. Setting it raises <see cref="PropertyChangedEvent"/>.
        /// </summary>
        public bool DimNoExtLine2
        {
            get
            {
                return (geoFlags & EGeoFlag.DimNoExtLine2) != 0;
            }
            set
            {
                bool OldValue = DimNoExtLine2;
                if (value) geoFlags |= EGeoFlag.DimNoExtLine2;
                else geoFlags &= ~EGeoFlag.DimNoExtLine2;
                FireDidChange("DimNoExtLine2", value, OldValue);
            }
        }
        /// <summary>
        /// When the text is placed outside the extension lines, the dimension line is extended until it reaches the text
        /// (<see cref="EGeoFlag.DimTextOutsideForceDimensionLine"/>). Setting it raises <see cref="PropertyChangedEvent"/>.
        /// </summary>
        public bool DimTextOutsideForceDimensionLine
        {
            get
            {
                return (geoFlags & EGeoFlag.DimTextOutsideForceDimensionLine) != 0;
            }
            set
            {
                bool OldValue = DimTextOutsideForceDimensionLine;
                if (value) geoFlags |= EGeoFlag.DimTextOutsideForceDimensionLine;
                else geoFlags &= ~EGeoFlag.DimTextOutsideForceDimensionLine;
                FireDidChange("DimTextOutsideForceDimensionLine", value, OldValue);
            }
        }
        /// <summary>
        /// No symbols are drawn when they are placed outside the extension lines (<see cref="EGeoFlag.DimNoOutsideSymbols"/>). Stored only,
        /// not evaluated by the current layout. Setting it raises <see cref="PropertyChangedEvent"/>.
        /// </summary>
        public bool DimNoOutsideSymbols
        {
            get
            {
                return (geoFlags & EGeoFlag.DimNoOutsideSymbols) != 0;
            }
            set
            {
                bool OldValue = DimNoOutsideSymbols;
                if (value) geoFlags |= EGeoFlag.DimNoOutsideSymbols;
                else geoFlags &= ~EGeoFlag.DimNoOutsideSymbols;
                FireDidChange("DimNoOutsideSymbols", value, OldValue);
            }
        }
        /// <summary>
        /// The dimension line is interrupted where it crosses the text, leaving a margin of <see cref="DimensionLineGap"/> around it; it
        /// stays continuous when the text is rotated (<see cref="EGeoFlag.DimBreakDimLine"/>). Setting it raises <see cref="PropertyChangedEvent"/>.
        /// </summary>
        public bool DimBreakDimLine
        {
            get
            {
                return (geoFlags & EGeoFlag.DimBreakDimLine) != 0;
            }
            set
            {
                bool OldValue = DimBreakDimLine;
                if (value) geoFlags |= EGeoFlag.DimBreakDimLine;
                else geoFlags &= ~EGeoFlag.DimBreakDimLine;
                FireDidChange("DimBreakDimLine", value, OldValue);
            }
        }
        /// <summary>
        /// The dimension line keeps a fixed position (<see cref="EGeoFlag.DimFixedDimLine"/>). Stored only, not evaluated by the current layout. Setting it raises <see cref="PropertyChangedEvent"/>.
        /// </summary>
        public bool DimFixedDimLine
        {
            get
            {
                return (geoFlags & EGeoFlag.DimFixedDimLine) != 0;
            }
            set
            {
                bool OldValue = DimFixedDimLine;
                if (value) geoFlags |= EGeoFlag.DimFixedDimLine;
                else geoFlags &= ~EGeoFlag.DimFixedDimLine;
                FireDidChange("DimFixedDimLine", value, OldValue);
            }
        }
        /// <summary>
        /// No dimension line is drawn at all, only the extension lines, the symbols and the text (<see cref="EGeoFlag.DimNoDimLine"/>). Setting it raises <see cref="PropertyChangedEvent"/>.
        /// </summary>
        public bool DimNoDimLine
        {
            get
            {
                return (geoFlags & EGeoFlag.DimNoDimLine) != 0;
            }
            set
            {
                bool OldValue = DimNoDimLine;
                if (value) geoFlags |= EGeoFlag.DimNoDimLine;
                else geoFlags &= ~EGeoFlag.DimNoDimLine;
                FireDidChange("DimNoDimLine", value, OldValue);
            }
        }

        /// <summary>
        /// The dimension text should be asked for in a dialog when the dimension is created (<see cref="ETextFlag.DimTxtDialog"/>). Stored
        /// only, not evaluated. Setting it raises <see cref="PropertyChangedEvent"/>.
        /// </summary>
        public bool DimTxtDialog
        {
            get
            {
                return (textFlags & ETextFlag.DimTxtDialog) != 0;
            }
            set
            {
                bool OldValue = DimTxtDialog;
                if (value) textFlags |= ETextFlag.DimTxtDialog;
                else textFlags &= ~ETextFlag.DimTxtDialog;
                FireDidChange("DimTxtDialog", value, OldValue);
            }
        }
        /// <summary>
        /// The text is always placed outside, to the right of the second extension line, instead of between the extension lines
        /// (<see cref="ETextFlag.DimTxtOutside"/>). Setting it raises <see cref="PropertyChangedEvent"/>.
        /// </summary>
        public bool DimTxtOutside
        {
            get
            {
                return (textFlags & ETextFlag.DimTxtOutside) != 0;
            }
            set
            {
                bool OldValue = DimTxtOutside;
                if (value) textFlags |= ETextFlag.DimTxtOutside;
                else textFlags &= ~ETextFlag.DimTxtOutside;
                FireDidChange("DimTxtOutside", value, OldValue);
            }
        }
        /// <summary>
        /// A dimension of two points moves its text outside when it is wider than the distance between the extension lines
        /// (<see cref="ETextFlag.DimTxtAutomatic"/>). Setting it raises <see cref="PropertyChangedEvent"/>.
        /// </summary>
        public bool DimTxtAutomatic
        {
            get
            {
                return (textFlags & ETextFlag.DimTxtAutomatic) != 0;
            }
            set
            {
                bool OldValue = DimTxtAutomatic;
                if (value) textFlags |= ETextFlag.DimTxtAutomatic;
                else textFlags &= ~ETextFlag.DimTxtAutomatic;
                FireDidChange("DimTxtAutomatic", value, OldValue);
            }
        }
        /// <summary>
        /// The text is rotated by 90 degrees against the dimension line (<see cref="ETextFlag.DimTxtRotate90"/>). The three rotation
        /// settings <see cref="DimTxtRotate90"/>, <see cref="DimTxtRotate60"/> and <see cref="DimTxtRotateAutomatic"/> exclude each other:
        /// setting one of them to true clears the other two. Setting it raises <see cref="PropertyChangedEvent"/>.
        /// </summary>
        public bool DimTxtRotate90
        {
            get
            {
                return (textFlags & ETextFlag.DimTxtRotate90) != 0;
            }
            set
            {
                bool OldValue = DimTxtRotate90;
                if (value)
                {
                    textFlags |= ETextFlag.DimTxtRotate90;
                    DimTxtRotate60 = false;
                    DimTxtRotateAutomatic = false;
                }
                else textFlags &= ~ETextFlag.DimTxtRotate90;
                FireDidChange("DimTxtRotate90", value, OldValue);
                // DimTxtRotate60 hat sich u.U. geändert, refresh machen
                if (checkDimTxtRotate60 != null && checkDimTxtRotateAutomatic != null)
                {
                    checkDimTxtRotate60.Update();
                    checkDimTxtRotateAutomatic.Update();
                }
            }
        }
        /// <summary>
        /// The text is rotated by 60 degrees against the dimension line (<see cref="ETextFlag.DimTxtRotate60"/>). Setting it to true clears
        /// <see cref="DimTxtRotate90"/> and <see cref="DimTxtRotateAutomatic"/>. Setting it raises <see cref="PropertyChangedEvent"/>.
        /// </summary>
        public bool DimTxtRotate60
        {
            get
            {
                return (textFlags & ETextFlag.DimTxtRotate60) != 0;
            }
            set
            {
                bool OldValue = DimTxtRotate60;
                if (value)
                {
                    textFlags |= ETextFlag.DimTxtRotate60;
                    DimTxtRotate90 = false;
                    DimTxtRotateAutomatic = false;
                }
                else textFlags &= ~ETextFlag.DimTxtRotate60;
                FireDidChange("DimTxtRotate60", value, OldValue);
                if (checkDimTxtRotate90 != null && checkDimTxtRotateAutomatic != null)
                {
                    checkDimTxtRotate90.Update();
                    checkDimTxtRotateAutomatic.Update();
                }
            }
        }
        /// <summary>
        /// The text is rotated by 90 degrees when it is wider than the distance between the extension lines, otherwise it stays parallel
        /// to the dimension line (<see cref="ETextFlag.DimTxtRotateAutomatic"/>). Setting it to true clears <see cref="DimTxtRotate90"/> and
        /// <see cref="DimTxtRotate60"/>. Setting it raises <see cref="PropertyChangedEvent"/>.
        /// </summary>
        public bool DimTxtRotateAutomatic
        {
            get
            {
                return (textFlags & ETextFlag.DimTxtRotateAutomatic) != 0;
            }
            set
            {
                bool OldValue = DimTxtRotateAutomatic;
                if (value)
                {
                    textFlags |= ETextFlag.DimTxtRotateAutomatic;
                    DimTxtRotate90 = false;
                    DimTxtRotate60 = false;
                }
                else textFlags &= ~ETextFlag.DimTxtRotateAutomatic;
                FireDidChange("DimTxtRotateAutomatic", value, OldValue);
                if (checkDimTxtRotate90 != null && checkDimTxtRotate60 != null)
                {
                    checkDimTxtRotate90.Update();
                    checkDimTxtRotate60.Update();
                }
            }
        }
        /// <summary>
        /// The text size refers to paper coordinates rather than model coordinates (<see cref="ETextFlag.DimTxtSizePaper"/>). Stored only,
        /// not evaluated by the current layout. Setting it raises <see cref="PropertyChangedEvent"/>.
        /// </summary>
        public bool DimTxtSizePaper
        {
            get
            {
                return (textFlags & ETextFlag.DimTxtSizePaper) != 0;
            }
            set
            {
                bool OldValue = DimTxtSizePaper;
                if (value) textFlags |= ETextFlag.DimTxtSizePaper;
                else textFlags &= ~ETextFlag.DimTxtSizePaper;
                FireDidChange("DimTxtSizePaper", value, OldValue);
            }
        }
        /// <summary>
        /// A second (alternate) value, scaled by <see cref="ScaleAlt"/>, rounded by <see cref="RoundAlt"/> and followed by
        /// <see cref="TextPostfixAlt"/>, is shown in square brackets (<see cref="ETextFlag.DimTxtAlt"/>). Stored only, the current layout
        /// does not draw the alternate text. Setting it raises <see cref="PropertyChangedEvent"/>.
        /// </summary>
        public bool DimTxtAlt
        {
            get
            {
                return (textFlags & ETextFlag.DimTxtAlt) != 0;
            }
            set
            {
                bool OldValue = DimTxtAlt;
                if (value) textFlags |= ETextFlag.DimTxtAlt;
                else textFlags &= ~ETextFlag.DimTxtAlt;
                FireDidChange("DimTxtAlt", value, OldValue);
            }
        }
        /// <summary>
        /// A rectangle is drawn around the text of a point dimension, with a margin of <see cref="DimensionLineGap"/>
        /// (<see cref="ETextFlag.DimTxtRect"/>). Setting it raises <see cref="PropertyChangedEvent"/>.
        /// </summary>
        public bool DimTxtRect
        {
            get
            {
                return (textFlags & ETextFlag.DimTxtRect) != 0;
            }
            set
            {
                bool OldValue = DimTxtRect;
                if (value) textFlags |= ETextFlag.DimTxtRect;
                else textFlags &= ~ETextFlag.DimTxtRect;
                FireDidChange("DimTxtRect", value, OldValue);
            }
        }
        /// <summary>
        /// <see cref="PlusTolerance"/> and <see cref="MinusTolerance"/> are shown stacked after the value in the size
        /// <see cref="TextSizeTol"/> (<see cref="ETextFlag.DimTxtTolerances"/>). A <see cref="Dimension"/> with tolerance texts of its own
        /// shows those regardless of this setting. Setting it raises <see cref="PropertyChangedEvent"/>.
        /// </summary>
        public bool DimTxtTolerances
        {
            get
            {
                return (textFlags & ETextFlag.DimTxtTolerances) != 0;
            }
            set
            {
                bool OldValue = DimTxtTolerances;
                if (value) textFlags |= ETextFlag.DimTxtTolerances;
                else textFlags &= ~ETextFlag.DimTxtTolerances;
                FireDidChange("DimTxtTolerances", value, OldValue);
            }
        }
        /// <summary>
        /// The value is shown as upper and lower limit computed from the tolerances instead of value plus tolerances
        /// (<see cref="ETextFlag.DimTxtLimits"/>). The property page offers it as alternative to <see cref="DimTxtTolerances"/>; it is
        /// stored only, the current layout does not draw limits. Setting it raises <see cref="PropertyChangedEvent"/>.
        /// </summary>
        public bool DimTxtLimits
        {
            get
            {
                return (textFlags & ETextFlag.DimTxtLimits) != 0;
            }
            set
            {
                bool OldValue = DimTxtLimits;
                if (value) textFlags |= ETextFlag.DimTxtLimits;
                else textFlags &= ~ETextFlag.DimTxtLimits;
                FireDidChange("DimTxtLimits", value, OldValue);
            }
        }
        /// <summary>
        /// The text of an angle dimension stays horizontal instead of following the direction of the dimension arc
        /// (<see cref="ETextFlag.DimTxtInsideHor"/>, DXF: DIMTIH). Setting it raises <see cref="PropertyChangedEvent"/>.
        /// </summary>
        public bool DimTxtInsideHor
        {
            get
            {
                return (textFlags & ETextFlag.DimTxtInsideHor) != 0;
            }
            set
            {
                bool OldValue = DimTxtInsideHor;
                if (value) textFlags |= ETextFlag.DimTxtInsideHor;
                else textFlags &= ~ETextFlag.DimTxtInsideHor;
                FireDidChange("DimTxtInsideHor", value, OldValue);
            }
        }
        /// <summary>
        /// Text placed outside the extension lines stays horizontal (<see cref="ETextFlag.DimTxtOutsideHor"/>, DXF: DIMTOH). Stored and
        /// exchanged with DXF, not evaluated by the current layout. Setting it raises <see cref="PropertyChangedEvent"/>.
        /// </summary>
        public bool DimTxtOutsideHor
        {
            get
            {
                return (textFlags & ETextFlag.DimTxtOutsideHor) != 0;
            }
            set
            {
                bool OldValue = DimTxtOutsideHor;
                if (value) textFlags |= ETextFlag.DimTxtOutsideHor;
                else textFlags &= ~ETextFlag.DimTxtOutsideHor;
                FireDidChange("DimTxtOutsideHor", value, OldValue);
            }
        }

        /// <summary>
        /// Decimal places are always written, padded with zeros up to the precision given by <see cref="Round"/>, e.g. "12.50" instead of
        /// "12.5" with a precision of 0.01 (<see cref="ETextFlag.DimForceTrailingZero"/>). Setting it raises <see cref="PropertyChangedEvent"/>.
        /// </summary>
        public bool DimForceTrailingZero
        {
            get
            {
                return (textFlags & ETextFlag.DimForceTrailingZero) != 0;
            }
            set
            {
                bool OldValue = DimForceTrailingZero;
                if (value) textFlags |= ETextFlag.DimForceTrailingZero;
                else textFlags &= ~ETextFlag.DimForceTrailingZero;
                FireDidChange("DimForceTrailingZero", value, OldValue);
            }
        }
        /// <summary>
        /// The value is written as a fraction, e.g. "3 1/8" (<see cref="ETextFlag.DimTxtFractional"/>). The formatting itself is controlled
        /// by <see cref="Round"/>: a value greater than 1 (2, 4, 8, 16, 32, 64) is the largest denominator. The property page sets this
        /// flag together with such a precision. Setting it raises <see cref="PropertyChangedEvent"/>.
        /// </summary>
        public bool DimTxtFractional
        {
            get
            {
                return (textFlags & ETextFlag.DimTxtFractional) != 0;
            }
            set
            {
                bool OldValue = DimTxtFractional;
                if (value) textFlags |= ETextFlag.DimTxtFractional;
                else textFlags &= ~ETextFlag.DimTxtFractional;
                FireDidChange("DimTxtFractional", value, OldValue);
            }
        }
        /// <summary>
        /// The alternate (second) text is written as a fraction (<see cref="ETextFlag.DimTxtAltFractional"/>). Stored only, the current
        /// layout does not draw the alternate text. Setting it raises <see cref="PropertyChangedEvent"/>.
        /// </summary>
        public bool DimTxtAltFractional
        {
            get
            {
                return (textFlags & ETextFlag.DimTxtAltFractional) != 0;
            }
            set
            {
                bool OldValue = DimTxtAltFractional;
                if (value) textFlags |= ETextFlag.DimTxtAltFractional;
                else textFlags &= ~ETextFlag.DimTxtAltFractional;
                FireDidChange("DimTxtAltFractional", value, OldValue);
            }
        }
        /// <summary>
        /// The value is written in the German building construction format "DIN Bau": millimetres superscript, centimetres normal, metres
        /// separated by a point (<see cref="ETextFlag.DimTxtDinBau"/>). The property page sets it together with <see cref="Round"/> = -1;
        /// the format is not implemented in the current layout. Setting it raises <see cref="PropertyChangedEvent"/>.
        /// </summary>
        public bool DimTxtDinBau
        {
            get
            {
                return (textFlags & ETextFlag.DimTxtDinBau) != 0;
            }
            set
            {
                bool OldValue = DimTxtDinBau;
                if (value) textFlags |= ETextFlag.DimTxtDinBau;
                else textFlags &= ~ETextFlag.DimTxtDinBau;
                FireDidChange("DimTxtDinBau", value, OldValue);
            }
        }
        /// <summary>
        /// The way the value of an angle dimension is written: decimal degrees, degrees/minutes/seconds, gradians, radians or the length of
        /// the arc; see <see cref="EAngleText"/>. Setting it raises <see cref="PropertyChangedEvent"/>.
        /// </summary>
        public EAngleText AngleText
        {
            get
            {
                return angleText;
            }
            set
            {
                EAngleText OldValue = angleText;
                angleText = value;
                FireDidChange("AngleText", value, OldValue);
            }
        }
        /// <summary>
        /// The style may be used for dimensions of the distance between points (<see cref="Dimension.EDimType.DimPoints"/>,
        /// <see cref="ETypeFlag.DimPoints"/>). Setting it raises <see cref="PropertyChangedEvent"/>.
        /// </summary>
        public bool DimPoints
        {
            get
            {
                return (types & ETypeFlag.DimPoints) != 0;
            }
            set
            {
                bool OldValue = DimPoints;
                if (value) types |= ETypeFlag.DimPoints;
                else types &= ~ETypeFlag.DimPoints;
                FireDidChange("DimPoints", value, OldValue);
            }
        }
        /// <summary>
        /// The style may be used for coordinate dimensions (<see cref="Dimension.EDimType.DimCoord"/>, <see cref="ETypeFlag.DimCoord"/>). Setting it raises <see cref="PropertyChangedEvent"/>.
        /// </summary>
        public bool DimCoord
        {
            get
            {
                return (types & ETypeFlag.DimCoord) != 0;
            }
            set
            {
                bool OldValue = DimCoord;
                if (value) types |= ETypeFlag.DimCoord;
                else types &= ~ETypeFlag.DimCoord;
                FireDidChange("DimCoord", value, OldValue);
            }
        }
        /// <summary>
        /// The style may be used for angle dimensions (<see cref="Dimension.EDimType.DimAngle"/>, <see cref="ETypeFlag.DimAngle"/>). Setting it raises <see cref="PropertyChangedEvent"/>.
        /// </summary>
        public bool DimAngle
        {
            get
            {
                return (types & ETypeFlag.DimAngle) != 0;
            }
            set
            {
                bool OldValue = DimAngle;
                if (value) types |= ETypeFlag.DimAngle;
                else types &= ~ETypeFlag.DimAngle;
                FireDidChange("DimAngle", value, OldValue);
            }
        }
        /// <summary>
        /// The style may be used for radius dimensions (<see cref="Dimension.EDimType.DimRadius"/>, <see cref="ETypeFlag.DimRadius"/>). Setting it raises <see cref="PropertyChangedEvent"/>.
        /// </summary>
        public bool DimRadius
        {
            get
            {
                return (types & ETypeFlag.DimRadius) != 0;
            }
            set
            {
                bool OldValue = DimRadius;
                if (value) types |= ETypeFlag.DimRadius;
                else types &= ~ETypeFlag.DimRadius;
                FireDidChange("DimRadius", value, OldValue);
            }
        }
        /// <summary>
        /// The style may be used for diameter dimensions (<see cref="Dimension.EDimType.DimDiameter"/>, <see cref="ETypeFlag.DimDiameter"/>). Setting it raises <see cref="PropertyChangedEvent"/>.
        /// </summary>
        public bool DimDiameter
        {
            get
            {
                return (types & ETypeFlag.DimDiameter) != 0;
            }
            set
            {
                bool OldValue = DimDiameter;
                if (value) types |= ETypeFlag.DimDiameter;
                else types &= ~ETypeFlag.DimDiameter;
                FireDidChange("DimDiameter", value, OldValue);
            }
        }
        /// <summary>
        /// The style may be used for labelling a location (<see cref="Dimension.EDimType.DimLocation"/>, <see cref="ETypeFlag.DimLocation"/>). Setting it raises <see cref="PropertyChangedEvent"/>.
        /// </summary>
        public bool DimLocation
        {
            get
            {
                return (types & ETypeFlag.DimLocation) != 0;
            }
            set
            {
                bool OldValue = DimLocation;
                if (value) types |= ETypeFlag.DimLocation;
                else types &= ~ETypeFlag.DimLocation;
                FireDidChange("DimLocation", value, OldValue);
            }
        }
        /// <summary>
        /// The angle is defined by two lines of two points each (<see cref="EAngleFlag.DimAngleFourPoint"/>). Stored only, not evaluated
        /// by the current layout. Setting it raises <see cref="PropertyChangedEvent"/>.
        /// </summary>
        public bool DimAngleFourPoint
        {
            get
            {
                return (angleFlags & EAngleFlag.DimAngleFourPoint) != 0;
            }
            set
            {
                bool OldValue = DimAngleFourPoint;
                if (value) angleFlags |= EAngleFlag.DimAngleFourPoint;
                else angleFlags &= ~EAngleFlag.DimAngleFourPoint;
                FireDidChange("DimAngleFourPoint", value, OldValue);
            }
        }
        /// <summary>
        /// The half angle (bisector) is dimensioned (<see cref="EAngleFlag.DimAngleBisection"/>). Shown in the property page, but not
        /// evaluated by the current layout. Setting it raises <see cref="PropertyChangedEvent"/>.
        /// </summary>
        public bool DimAngleBisection
        {
            get
            {
                return (angleFlags & EAngleFlag.DimAngleBisection) != 0;
            }
            set
            {
                bool OldValue = DimAngleBisection;
                if (value) angleFlags |= EAngleFlag.DimAngleBisection;
                else angleFlags &= ~EAngleFlag.DimAngleBisection;
                FireDidChange("DimAngleBisection", value, OldValue);
            }
        }
        /// <summary>
        /// The dimension line of a radius or diameter dimension bends at the reference point and continues horizontally, and the text is
        /// placed on that horizontal part (<see cref="ERadiusFlag.DimRadiusBend"/>). When false, the text sits on the straight dimension line. Setting it raises <see cref="PropertyChangedEvent"/>.
        /// </summary>
        public bool DimRadiusBend
        {
            get
            {
                return (radiusFlags & ERadiusFlag.DimRadiusBend) != 0;
            }
            set
            {
                bool OldValue = DimRadiusBend;
                if (value) radiusFlags |= ERadiusFlag.DimRadiusBend;
                else radiusFlags &= ~ERadiusFlag.DimRadiusBend;
                FireDidChange("DimRadiusBend", value, OldValue);
            }
        }

        /// <summary>
        /// The dimension text is bold (<see cref="EFontFlag.DimFontBold"/>). Stored, but the font style is not applied by the current
        /// layout. Setting it raises <see cref="PropertyChangedEvent"/>.
        /// </summary>
        public bool DimFontBold
        {
            get
            {
                return (fontFlags & EFontFlag.DimFontBold) != 0;
            }
            set
            {
                bool OldValue = DimFontBold;
                if (value) fontFlags |= EFontFlag.DimFontBold;
                else fontFlags &= ~EFontFlag.DimFontBold;
                FireDidChange("DimFontBold", value, OldValue);
            }
        }
        /// <summary>
        /// The dimension text is italic (<see cref="EFontFlag.DimFontItalic"/>). Stored, but the font style is not applied by the current
        /// layout. Setting it raises <see cref="PropertyChangedEvent"/>.
        /// </summary>
        public bool DimFontItalic
        {
            get
            {
                return (fontFlags & EFontFlag.DimFontItalic) != 0;
            }
            set
            {
                bool OldValue = DimFontItalic;
                if (value) fontFlags |= EFontFlag.DimFontItalic;
                else fontFlags &= ~EFontFlag.DimFontItalic;
                FireDidChange("DimFontItalic", value, OldValue);
            }
        }
        /// <summary>
        /// The dimension text is underlined (<see cref="EFontFlag.DimFontUnderline"/>). Stored, but the font style is not applied by the
        /// current layout. Setting it raises <see cref="PropertyChangedEvent"/>.
        /// </summary>
        public bool DimFontUnderline
        {
            get
            {
                return (fontFlags & EFontFlag.DimFontUnderline) != 0;
            }
            set
            {
                bool OldValue = DimFontUnderline;
                if (value) fontFlags |= EFontFlag.DimFontUnderline;
                else fontFlags &= ~EFontFlag.DimFontUnderline;
                FireDidChange("DimFontUnderline", value, OldValue);
            }
        }
        /// <summary>
        /// The dimension text is struck out (<see cref="EFontFlag.DimFontStrikeOut"/>). Stored, but the font style is not applied by the
        /// current layout. Setting it raises <see cref="PropertyChangedEvent"/>.
        /// </summary>
        public bool DimFontStrikeOut
        {
            get
            {
                return (fontFlags & EFontFlag.DimFontStrikeOut) != 0;
            }
            set
            {
                bool OldValue = DimFontStrikeOut;
                if (value) fontFlags |= EFontFlag.DimFontStrikeOut;
                else fontFlags &= ~EFontFlag.DimFontStrikeOut;
                FireDidChange("DimFontStrikeOut", value, OldValue);
            }
        }
        #endregion
        #region IPropertyEntry Members
        private CheckProperty checkDimTxtRotate90;
        private CheckProperty checkDimTxtRotate60;
        private CheckProperty checkDimTxtRotateAutomatic;
        private IPropertyEntry[] subEntries;
        /// <inheritdoc/>
        public override PropertyEntryType Flags
        {
            get
            {
                PropertyEntryType flags = PropertyEntryType.ContextMenu | PropertyEntryType.Selectable | PropertyEntryType.LabelEditable | PropertyEntryType.GroupTitle | PropertyEntryType.HasSubEntries;
                if (parent.Current == this) flags |= PropertyEntryType.Bold;
                return flags;
            }
        }
        /// <inheritdoc/>
        public override string LabelText
        {
            get { return name; }
        }
        /// <summary>
        /// Overrides <see cref="PropertyEntryImpl.ContextMenu"/>, 
        /// returns the context menu with the id "MenuId.DimStyleEntry".
        /// </summary>
        public override MenuWithHandler[] ContextMenu
        {
            get
            {
                    return MenuResource.LoadMenuDefinition("MenuId.DimStyleEntry", false, this);
            }
        }
        /// <inheritdoc/>
        public override IPropertyEntry[] SubItems
        {
            get
            {
                if (subEntries==null)
                {

                    ColorList colorList = null;
                    if (Parent != null) colorList = Parent.Owner.ColorList;
                    if (colorList == null) colorList = base.Frame.Project.ColorList; // im Notfall
                    LineWidthList lineWidthList = null;
                    if (Parent != null) lineWidthList = Parent.Owner.LineWidthList;
                    if (lineWidthList == null) lineWidthList = base.Frame.Project.LineWidthList; // im Notfall

                    subEntries = new IPropertyEntry[9];

                    ShowPropertyGroup geometry = new ShowPropertyGroup("DimensionStyle.Geometry");
                    geometry.AddSubEntry(new DoubleProperty(this, "DimLineExtension", "DimensionStyle.DimLineExtension", base.Frame));
                    geometry.AddSubEntry(new DoubleProperty(this, "ExtLineExtension", "DimensionStyle.ExtLineExtension", base.Frame));
                    geometry.AddSubEntry(new DoubleProperty(this, "ExtLineOffset", "DimensionStyle.ExtLineOffset", base.Frame));
                    geometry.AddSubEntry(new DoubleProperty(this, "LineIncrement", "DimensionStyle.LineIncrement", base.Frame));
                    geometry.AddSubEntry(new DoubleProperty(this, "TextDist", "DimensionStyle.TextDist", base.Frame));
                    geometry.AddSubEntry(new DoubleProperty(this, "DimensionLineGap", "DimensionStyle.DimensionLineGap", base.Frame));
                    geometry.AddSubEntry(new BooleanProperty(this, "DimNoExtLine", "DimensionStyle.DimNoExtLine"));
                    geometry.AddSubEntry(new BooleanProperty(this, "DimNoDimLine", "DimensionStyle.DimNoDimLine"));
                    geometry.AddSubEntry(new BooleanProperty(this, "DimBreakDimLine", "DimensionStyle.DimBreakDimLine"));
                    geometry.AddSubEntry(new BooleanProperty(this, "DimTxtRect", "DimensionStyle.DimTxtRect"));
                    geometry.AddSubEntry(new CheckProperty(this, "DimMultiLine", "DimensionStyle.DimMultiLine"));
                    geometry.AddSubEntry(new CheckProperty(this, "DimTextOutsideForceDimensionLine", "DimensionStyle.DimTextOutsideForceDimensionLine"));
                    ShowPropertyGroup symbol = new ShowPropertyGroup("DimensionStyle.Symbol");
                    MultipleChoiceProperty multipropsymbol = new MultipleChoiceProperty("DimensionStyle.SymbolType", (int)this.symbol);
                    multipropsymbol.ValueChangedEvent += new ValueChangedDelegate(SymbolValueChanged);
                    symbol.AddSubEntry(multipropsymbol);
                    MultipleChoiceProperty multipropplacement = new MultipleChoiceProperty("DimensionStyle.SymbolPlacement", (int)this.symbolPlacement);
                    multipropplacement.ValueChangedEvent += new ValueChangedDelegate(SymbolPlacementValueChanged);
                    symbol.AddSubEntry(multipropplacement);
                    symbol.AddSubEntry(new DoubleProperty(this, "SymbolSize", "DimensionStyle.SymbolSize", base.Frame));

                    ShowPropertyGroup dimtext = new ShowPropertyGroup("DimensionStyle.DimText");
                    MultipleChoiceProperty multipropprecision = new MultipleChoiceProperty("DimensionStyle.Precision", GetPrecisionAsInt(round));
                    multipropprecision.ValueChangedEvent += new ValueChangedDelegate(PrecisionValueChanged);
                    dimtext.AddSubEntry(multipropprecision);
                    dimtext.AddSubEntry(new StringProperty(this, "TextPrefix", "DimensionStyle.TextPrefix"));
                    dimtext.AddSubEntry(new StringProperty(this, "TextPostfix", "DimensionStyle.TextPostfix"));
                    dimtext.AddSubEntry(new DoubleProperty(this, "TextSize", "DimensionStyle.TextSize", base.Frame));
                    dimtext.AddSubEntry(new DoubleProperty(this, "Scale", "DimensionStyle.Scale", base.Frame));
                    dimtext.AddSubEntry(new CheckProperty(this, "DimTxtDialog", "DimensionStyle.DimTxtDialog"));
                    dimtext.AddSubEntry(new CheckProperty(this, "DimTxtOutside", "DimensionStyle.DimTxtOutside"));
                    dimtext.AddSubEntry(new CheckProperty(this, "DimTxtAutomatic", "DimensionStyle.DimTxtAutomatic"));
                    // die beiden folgend showproperties müssen gemerkt werden, da sie sich
                    // gegenseitig beeinflussen und refresht werden müssen
                    checkDimTxtRotate90 = new CheckProperty(this, "DimTxtRotate90", "DimensionStyle.DimTxtRotate90");
                    dimtext.AddSubEntry(checkDimTxtRotate90);
                    checkDimTxtRotate60 = new CheckProperty(this, "DimTxtRotate60", "DimensionStyle.DimTxtRotate60");
                    dimtext.AddSubEntry(checkDimTxtRotate60);
                    checkDimTxtRotateAutomatic = new CheckProperty(this, "DimTxtRotateAutomatic", "DimensionStyle.DimTxtRotateAutomatic");
                    dimtext.AddSubEntry(checkDimTxtRotateAutomatic);
                    dimtext.AddSubEntry(new CheckProperty(this, "DimTxtInsideHor", "DimensionStyle.DimTxtInsideHor"));
                    dimtext.AddSubEntry(new CheckProperty(this, "DimTxtOutsideHor", "DimensionStyle.DimTxtOutsideHor"));
                    dimtext.AddSubEntry(new CheckProperty(this, "DimForceTrailingZero", "DimensionStyle.DimForceTrailingZero"));

                    ShowPropertyGroup secondtext = new ShowPropertyGroup("DimensionStyle.SecondText");
                    secondtext.AddSubEntry(new CheckProperty(this, "DimTxtAlt", "DimensionStyle.DimTxtAlt"));
                    MultipleChoiceProperty multipropaltprecision = new MultipleChoiceProperty("DimensionStyle.Precision", GetPrecisionAsInt(roundAlt));
                    multipropaltprecision.ValueChangedEvent += new ValueChangedDelegate(AltPrecisionValueChanged);
                    secondtext.AddSubEntry(multipropaltprecision);
                    secondtext.AddSubEntry(new StringProperty(this, "TextPostfixAlt", "DimensionStyle.TextPostfixAlt"));
                    secondtext.AddSubEntry(new DoubleProperty(this, "ScaleAlt", "DimensionStyle.ScaleAlt", base.Frame));

                    ShowPropertyGroup tolerance = new ShowPropertyGroup("DimensionStyle.Tolerance");
                    int tol = 0;
                    if (DimTxtTolerances) tol = 1;
                    else if (DimTxtLimits) tol = 2;
                    MultipleChoiceProperty multiproptolerance = new MultipleChoiceProperty("DimensionStyle.ToleranceMode", tol);
                    multiproptolerance.ValueChangedEvent += new ValueChangedDelegate(ToleranceValueChanged);
                    tolerance.AddSubEntry(multiproptolerance);
                    tolerance.AddSubEntry(new DoubleProperty(this, "PlusTolerance", "DimensionStyle.PlusTolerance", base.Frame));
                    tolerance.AddSubEntry(new DoubleProperty(this, "MinusTolerance", "DimensionStyle.MinusTolerance", base.Frame));
                    tolerance.AddSubEntry(new DoubleProperty(this, "TextSizeTol", "DimensionStyle.TextSizeTol", base.Frame));

                    ShowPropertyGroup valid = new ShowPropertyGroup("DimensionStyle.Valid");
                    valid.AddSubEntry(new CheckProperty(this, "DimPoints", "DimensionStyle.DimPoints"));
                    valid.AddSubEntry(new CheckProperty(this, "DimCoord", "DimensionStyle.DimCoord"));
                    valid.AddSubEntry(new CheckProperty(this, "DimAngle", "DimensionStyle.DimAngle"));
                    valid.AddSubEntry(new CheckProperty(this, "DimRadius", "DimensionStyle.DimRadius"));
                    valid.AddSubEntry(new CheckProperty(this, "DimDiameter", "DimensionStyle.DimDiameter"));
                    valid.AddSubEntry(new CheckProperty(this, "DimLocation", "DimensionStyle.DimLocation"));

                    ShowPropertyGroup colors = new ShowPropertyGroup("DimensionStyle.Colors");
                    colors.AddSubEntry(new ColorSelectionProperty(this, "DimLineColor", "DimensionStyle.DimLineColor", colorList, ColorList.StaticFlags.allowFromParent));
                    colors.AddSubEntry(new ColorSelectionProperty(this, "ExtLineColor", "DimensionStyle.ExtLineColor", colorList, ColorList.StaticFlags.allowFromParent));
                    colors.AddSubEntry(new ColorSelectionProperty(this, "FillColor", "DimensionStyle.FillColor", colorList, ColorList.StaticFlags.allowFromParent));
                    LineWidthSelectionProperty dimLineWidthSelection = new LineWidthSelectionProperty("DimensionStyle.DimLineWidth", lineWidthList, dimLineWidth);
                    colors.AddSubEntry(dimLineWidthSelection);
                    dimLineWidthSelection.LineWidthChangedEvent += new CADability.UserInterface.LineWidthSelectionProperty.LineWidthChangedDelegate(DimLineWidthChanged);
                    LineWidthSelectionProperty extLineWidthSelection = new LineWidthSelectionProperty("DimensionStyle.ExtLineWidth", lineWidthList, extLineWidth);
                    colors.AddSubEntry(extLineWidthSelection);
                    extLineWidthSelection.LineWidthChangedEvent += new CADability.UserInterface.LineWidthSelectionProperty.LineWidthChangedDelegate(ExtLineWidthChanged);

                    ShowPropertyGroup options = new ShowPropertyGroup("DimensionStyle.Options");
                    MultipleChoiceProperty multipropangle = new MultipleChoiceProperty("DimensionStyle.DimTxtAngle", (int)AngleText);
                    multipropangle.ValueChangedEvent += new ValueChangedDelegate(AngleValueChanged);
                    options.AddSubEntry(multipropangle);
                    options.AddSubEntry(new CheckProperty(this, "DimAngleBisection", "DimensionStyle.DimAngleBisection"));
                    options.AddSubEntry(new CheckProperty(this, "DimRadiusBend", "DimensionStyle.DimRadiusBend"));

                    ShowPropertyGroup font = new ShowPropertyGroup("DimensionStyle.Font");
                    System.Drawing.FontFamily[] families = System.Drawing.FontFamily.Families;
                    string[] choices = new string[families.Length];
                    for (int i = 0; i < families.Length; i++) choices[i] = families[i].Name;
                    MultipleChoiceProperty multipropfont = new MultipleChoiceProperty("DimensionStyle.TextFont", choices, TextFont);
                    multipropfont.ValueChangedEvent += new ValueChangedDelegate(FontValueChanged);
                    font.AddSubEntry(multipropfont);
                    font.AddSubEntry(new CheckProperty(this, "DimFontBold", "Text.Bold"));
                    font.AddSubEntry(new CheckProperty(this, "DimFontItalic", "Text.Italic"));
                    font.AddSubEntry(new CheckProperty(this, "DimFontUnderline", "Text.Underline"));
                    font.AddSubEntry(new CheckProperty(this, "DimFontStrikeOut", "Text.Strikeout"));
                    font.AddSubEntry(new ColorSelectionProperty(this, "FontColor", "DimensionStyle.FontColor", colorList, ColorList.StaticFlags.allowFromParent));
                    //			font.AddSubEntry(new CheckProperty(this,"DimFontBold","DimensionStyle.DimFontBold"));
                    //			font.AddSubEntry(new CheckProperty(this,"DimFontItalic","DimensionStyle.DimFontItalic"));
                    //			font.AddSubEntry(new CheckProperty(this,"DimFontUnderline","DimensionStyle.DimFontUnderline"));
                    //			font.AddSubEntry(new CheckProperty(this,"DimFontStrikeOut","DimensionStyle.DimFontStrikeOut"));

                    subEntries[0] = geometry;
                    subEntries[1] = symbol;
                    subEntries[2] = dimtext;
                    subEntries[3] = secondtext;
                    subEntries[4] = tolerance;
                    subEntries[5] = valid;
                    subEntries[6] = colors;
                    subEntries[7] = options;
                    subEntries[8] = font;

                }
                return subEntries;
            }
        }
        /// <inheritdoc/>
        public override void Added(IPropertyPage propertyTreeView)
        {	// erfolgt als erster Aufruf, hier werden die subentries erzeugt
            base.Added(propertyTreeView); // schon hier, damit Frame gesetzt ist
            base.resourceIdInternal = "DimensionStyleName";

        }
        /// <summary>
        /// Overrides <see cref="PropertyEntryImpl.Removed"/>
        /// </summary>
        /// <param name="propertyTreeView">the IPropertyTreeView from which it was removed</param>
        public override void Removed(IPropertyPage propertyTreeView)
        {
            subEntries = null; // invers zu Added
            checkDimTxtRotate90 = null;
            checkDimTxtRotate60 = null;
            checkDimTxtRotateAutomatic = null;
            base.Removed(propertyTreeView);
        }
        /// <inheritdoc/>
        public override bool EditTextChanged(string newValue)
        {
            return true;
        }
        /// <inheritdoc/>
        public override void EndEdit(bool aborted, bool modified, string newValue)
        {
            try
            {
                Name = newValue;
            }
            catch (NameAlreadyExistsException)
            {
                propertyPage.Refresh(this);
            }
        }

        private void ExtLineWidthChanged(LineWidth selected)
        {
            ExtLineWidth = selected;
        }
        private void DimLineWidthChanged(LineWidth selected)
        {
            DimLineWidth = selected;
        }
#endregion
#region ISerializable Members
        /// <summary>
        /// Constructor required by deserialization
        /// </summary>
        /// <param name="info">SerializationInfo</param>
        /// <param name="context">StreamingContext</param>
        protected DimensionStyle(SerializationInfo info, StreamingContext context)
        {
            name = (string)info.GetValue("Name", typeof(string));
            textPrefix = (string)info.GetValue("TextPrefix", typeof(string));
            textPostfix = (string)info.GetValue("TextPostfix", typeof(string));
            textPostfixAlt = (string)info.GetValue("TextPostfixAlt", typeof(string));
            pointFlags = (EPointFlag)info.GetValue("PointFlags", typeof(EPointFlag));
            angleFlags = (EAngleFlag)info.GetValue("AngleFlags", typeof(EAngleFlag));
            radiusFlags = (ERadiusFlag)info.GetValue("RadiusFlags", typeof(ERadiusFlag));
            symbolFlags = (ESymbolFlag)info.GetValue("SymbolFlags", typeof(ESymbolFlag));
            geoFlags = (EGeoFlag)info.GetValue("GeoFlags", typeof(EGeoFlag));
            textFont = (string)info.GetValue("TextFont", typeof(string));
            dimLineColor = (ColorDef)info.GetValue("DimLineColor", typeof(ColorDef));
            dimLineWidth = LineWidth.Read("DimLineWidth", info, context);
            extLineColor = (ColorDef)info.GetValue("ExtLineColor", typeof(ColorDef));
            extLineWidth = LineWidth.Read("ExtLineWidth", info, context);
            textFlags = (ETextFlag)info.GetValue("TextFlags", typeof(ETextFlag));
            types = (ETypeFlag)info.GetValue("Types", typeof(ETypeFlag));
            dimLineExtension = (double)info.GetValue("DimLineExtension", typeof(double));
            extLineExtension = (double)info.GetValue("ExtLineExtension", typeof(double));
            extLineOffset = (double)info.GetValue("ExtLineOffset", typeof(double));
            lineIncrement = (double)info.GetValue("LineIncrement", typeof(double));
            symbol = (ESymbol)info.GetValue("Symbol", typeof(ESymbol));
            symbolSize = (double)info.GetValue("SymbolSize", typeof(double));
            scale = (double)info.GetValue("Scale", typeof(double));
            round = (double)info.GetValue("Round", typeof(double));
            roundAlt = (double)info.GetValue("RoundAlt", typeof(double));
            textSize = (double)info.GetValue("TextSize", typeof(double));
            textSizeTol = (double)info.GetValue("TextSizeTol", typeof(double));
            textDist = (double)info.GetValue("TextDist", typeof(double));
            plusTolerance = (double)info.GetValue("PlusTolerance", typeof(double));
            minusTolerance = (double)info.GetValue("MinusTolerance", typeof(double));
            centerMarkSize = (double)info.GetValue("CenterMarkSize", typeof(double));
            scaleAlt = (double)info.GetValue("ScaleAlt", typeof(double));
            dimensionLineGap = (double)info.GetValue("DimensionLineGap", typeof(double));
            symbolPlacement = (ESymbolPlacement)info.GetValue("SymbolPlacement", typeof(ESymbolPlacement));
            fillColor = (ColorDef)info.GetValue("FillColor", typeof(ColorDef));
            fixedLayer = info.GetValue("FixedLayer", typeof(Layer)) as Layer; // kann null sein
            angleText = (EAngleText)info.GetValue("AngleText", typeof(EAngleText));
            fontColor = ColorDef.Read("FontColor", info, context);
            try
            {
                userData = info.GetValue("UserData", typeof(UserData)) as UserData;
            }
            catch (SerializationException)
            {
                userData = new UserData();
            }
        }
        /// <summary>
        /// Writes the data of this object for the legacy binary serialization (<see cref="System.Runtime.Serialization.ISerializable"/>).
        /// Files are written in the JSON format (<see cref="IJsonSerialize"/>) now; the binary format is still read.
        /// </summary>
        /// <param name="info">receives the data</param>
        /// <param name="context">the destination of the serialization</param>
        public void GetObjectData(SerializationInfo info, StreamingContext context)
        {
            info.AddValue("Name", name);
            info.AddValue("TextPrefix", textPrefix);
            info.AddValue("TextPostfix", textPostfix);
            info.AddValue("TextPostfixAlt", textPostfixAlt);
            info.AddValue("PointFlags", pointFlags);
            info.AddValue("AngleFlags", angleFlags);
            info.AddValue("RadiusFlags", radiusFlags);
            info.AddValue("SymbolFlags", symbolFlags);
            info.AddValue("GeoFlags", geoFlags);
            info.AddValue("TextFont", textFont);
            info.AddValue("FontColor", fontColor);
            info.AddValue("DimLineColor", dimLineColor);
            info.AddValue("DimLineWidth", dimLineWidth);
            info.AddValue("ExtLineColor", extLineColor);
            info.AddValue("ExtLineWidth", extLineWidth);
            info.AddValue("TextFlags", textFlags);
            info.AddValue("Types", types);
            info.AddValue("DimLineExtension", dimLineExtension);
            info.AddValue("ExtLineExtension", extLineExtension);
            info.AddValue("ExtLineOffset", extLineOffset);
            info.AddValue("LineIncrement", lineIncrement);
            info.AddValue("Symbol", symbol);
            info.AddValue("SymbolSize", symbolSize);
            info.AddValue("Scale", scale);
            info.AddValue("Round", round);
            info.AddValue("RoundAlt", roundAlt);
            info.AddValue("TextSize", textSize);
            info.AddValue("TextSizeTol", textSizeTol);
            info.AddValue("TextDist", textDist);
            info.AddValue("PlusTolerance", plusTolerance);
            info.AddValue("MinusTolerance", minusTolerance);
            info.AddValue("CenterMarkSize", centerMarkSize);
            info.AddValue("ScaleAlt", scaleAlt);
            info.AddValue("DimensionLineGap", dimensionLineGap);
            info.AddValue("SymbolPlacement", symbolPlacement);
            info.AddValue("FillColor", fillColor);
            info.AddValue("FixedLayer", fixedLayer);
            info.AddValue("AngleText", angleText);
            info.AddValue("UserData", userData, typeof(UserData));
        }

#endregion
#region Event Handler der ShowProperties
        private void SymbolValueChanged(object sender, object NewValue)
        {
            MultipleChoiceProperty mcp = sender as MultipleChoiceProperty;
            symbol = (ESymbol)mcp.ChoiceIndex(NewValue as string);
        }

        private void SymbolPlacementValueChanged(object sender, object NewValue)
        {
            MultipleChoiceProperty mcp = sender as MultipleChoiceProperty;
            symbolPlacement = (ESymbolPlacement)mcp.ChoiceIndex(NewValue as string);
        }

        private int GetPrecisionAsInt(double pr)
        {
            if (pr == 1.0) return 0;
            if (pr == 0.5) return 1;
            if (pr == 0.25) return 2;
            if (pr == 0.1) return 3;
            if (pr == 0.05) return 4;
            if (pr == 0.025) return 5;
            if (pr == 0.01) return 6;
            if (pr == 0.001) return 7;
            if (pr == 0.0001) return 8;
            if (pr == 0.00001) return 9;
            if (pr == 0.0) return 10;
            if (pr == 2) return 11;
            if (pr == 4) return 12;
            if (pr == 8) return 13;
            if (pr == 16) return 14;
            if (pr == 32) return 15;
            if (pr == 64) return 16;
            if (pr == -1) return 17;
            return -1;
        }

        private void PrecisionValueChanged(object sender, object NewValue)
        {
            MultipleChoiceProperty mcp = sender as MultipleChoiceProperty;
            // chioces: 1.0|0.5|0.25|0.1|0.05|0.025|0.01|0.001|0.0001|0.00001|0.0 (=maximal)|1/2|1/4|1/8|1/16|1/32|1/64|DIN Bau
            // round hat folgende Werte:
            // 1.0|0.5|0.25|0.1|0.05|0.025|0.01|0.001|0.0001|0.00001|0.0 normale Genauigkeit
            // 2, 4, 8, 16, 32, 64 für Bruchdarstellung
            // 0.0 bei "DIN Bau"
            int choice = mcp.ChoiceIndex(NewValue as string);
            switch (choice)
            {
                case 0: round = 1.0; break;
                case 1: round = 0.5; break;
                case 2: round = 0.25; break;
                case 3: round = 0.1; break;
                case 4: round = 0.05; break;
                case 5: round = 0.025; break;
                case 6: round = 0.01; break;
                case 7: round = 0.001; break;
                case 8: round = 0.0001; break;
                case 9: round = 0.00001; break;
                case 10: round = 0.0; break;
                case 11: round = 2; break;
                case 12: round = 4; break;
                case 13: round = 8; break;
                case 14: round = 16; break;
                case 15: round = 32; break;
                case 16: round = 64; break;
                case 17: round = -1; break;
            }
            DimTxtFractional = choice >= 12 && choice <= 16;
            DimTxtDinBau = choice == 17;
        }
        private void AltPrecisionValueChanged(object sender, object NewValue)
        {
            MultipleChoiceProperty mcp = sender as MultipleChoiceProperty;
            // chioces: 1.0|0.5|0.25|0.1|0.05|0.025|0.01|0.001|0.0001|0.00001|0.0 (=maximal)|1/2|1/4|1/8|1/16|1/32|1/64|DIN Bau
            // round hat folgende Werte:
            // 1.0|0.5|0.25|0.1|0.05|0.025|0.01|0.001|0.0001|0.00001|0.0 normale Genauigkeit
            // 2, 4, 8, 16, 32, 64 für Bruchdarstellung
            // 0.0 bei "DIN Bau"
            int choice = mcp.ChoiceIndex(NewValue as string);
            switch (choice)
            {
                case 0: roundAlt = 1.0; break;
                case 1: roundAlt = 0.5; break;
                case 2: roundAlt = 0.25; break;
                case 3: roundAlt = 0.1; break;
                case 4: roundAlt = 0.05; break;
                case 5: roundAlt = 0.025; break;
                case 6: roundAlt = 0.01; break;
                case 7: roundAlt = 0.001; break;
                case 8: roundAlt = 0.0001; break;
                case 9: roundAlt = 0.00001; break;
                case 10: roundAlt = 0.0; break;
                case 11: roundAlt = 2; break;
                case 12: roundAlt = 4; break;
                case 13: roundAlt = 8; break;
                case 14: roundAlt = 16; break;
                case 15: roundAlt = 32; break;
                case 16: roundAlt = 64; break;
                case 17: roundAlt = -1; break;
            }
            DimTxtFractional = choice >= 12 && choice <= 16;
            DimTxtDinBau = choice == 17;
        }

        private void ToleranceValueChanged(object sender, object NewValue)
        {
            MultipleChoiceProperty mcp = sender as MultipleChoiceProperty;
            switch (mcp.ChoiceIndex(NewValue as string))
            {
                case 0:
                    DimTxtTolerances = false;
                    DimTxtLimits = false;
                    break;
                case 1:
                    DimTxtTolerances = true;
                    DimTxtLimits = false;
                    break;
                case 2:
                    DimTxtTolerances = false;
                    DimTxtLimits = true;
                    break;
            }
        }
        private void AngleValueChanged(object sender, object NewValue)
        {
            MultipleChoiceProperty mcp = sender as MultipleChoiceProperty;
            AngleText = (EAngleText)mcp.ChoiceIndex(NewValue as string);
        }
        private void FontValueChanged(object sender, object NewValue)
        {
            TextFont = NewValue as string;
        }
#endregion

#region ICommandHandler Members
        bool ICommandHandler.OnCommand(string MenuId)
        {
            switch (MenuId)
            {
                case "MenuId.DimStyleEntry.Clone":
                    if (Parent != null)
                    {
                        DimensionStyle ToAdd = Clone();
                        string newName = ToAdd.Name;
                        int n = 1;
                        while (Parent.Find(newName + n.ToString()) != null) ++n;
                        ToAdd.Name = newName + n.ToString();
                        Parent.Add(ToAdd);
                    }
                    return true;
                case "MenuId.DimStyleEntry.Delete":
                    if (Parent != null)
                    {
                        Parent.Remove(this);
                        if (propertyPage != null) propertyPage.Refresh(parent);
                    }
                    return true;
                case "MenuId.DimStyleEntry.Edit":
                    this.StartEdit(false);
                    return true;
                case "MenuId.DimStyleEntry.Current":
                    if (Parent != null)
                    {
                        parent.Current = this;
                        if (propertyPage != null) propertyPage.Refresh(parent);
                    }
                    return true;
            }
            return false;
        }
        bool ICommandHandler.OnUpdateCommand(string MenuId, CommandState CommandState)
        {
            // TODO:  Add DimensionStyle.OnUpdateCommand implementation
            return false;
        }
        void ICommandHandler.OnSelected(MenuWithHandler selectedMenuItem, bool selected) { }
        #endregion

    }

}
