using CADability.GeoObject;
using CADability.UserInterface;
using System;
using System.Runtime.Serialization;

namespace CADability.Attribute
{
    /// <summary>
    /// 
    /// </summary>
    [Serializable]
    public class DimensionStyle : PropertyEntryImpl, ISerializable, INamedAttribute, ICommandHandler
    {
        // die Einstellungen so aus CONDOR Version 4 übernommen:
        [Flags]
        public enum ETypeFlag { DimPoints = 0x1, DimCoord = 0x2, DimAngle = 0x4, DimRadius = 0x8, DimDiameter = 0x10, DimLocation = 0x20, DimAllTypes = 0x3F }; // gültig für diese Typen
        // ETypeFlag muss in der selben Reihenfolge sein wie Dimension.EDimType {DimPoints,DimCoord,DimAngle,DimRadius,DimDiameter};
        public enum ETextFlag
        {
            DimTxtDialog = 0x00000001, // Text soll über Dialog angefragt werden
            DimTxtOutside = 0x00000002, // Text immer außerhalb 
            DimTxtAutomatic = 0x00000004, // Text Lage automatisch bestimmen
            DimTxtRotate90 = 0x00000008, // Text um 90 Grad drehen
            DimTxtRotate60 = 0x00000010, // Text um 60 Grad drehen
            DimTxtRotateAutomatic = 0x00000020, // Text rotieren, wenn zu wenig Platz
            DimTxtSizePaper = 0x00000040, // Text Größe bezieht sich auf Papierkoordinaten
            DimTxtAlt = 0x00000080, // zweiter Text in []
            DimTxtRect = 0x00000100, // Rechteck um den Text
            DimTxtTolerances = 0x00000200, // Toleranzen ( +- gemäß PlusTolerance,MinusTolerance)
            DimTxtLimits = 0x00000400, // Grenzmaß ( +- gemäß PlusTolerance,MinusTolerance)
            DimTxtInsideHor = 0x00000800, // Text immer horizontal wenn innerhalb
            DimTxtOutsideHor = 0x00001000, // Text immer horizontal wenn außerhalb
            DimTxtFractional = 0x00002000, // Text mit Bruchdarstellung
            DimTxtAltFractional = 0x00004000, // zweiter Text mit Bruchdarstellung
            // DimTxtAngle0			=0x00008000, // 3 Flags für Winkel: 0: Grad, dezimal, 1: Grad, Minuten, Sekunden
            // DimTxtAngle1			=0x00010000, // 2: Neugrad (dezimal), 3: Bogenmaß (dezimal)
            DimTxtDinBau = 0x00020000, // mm hochgestellt, cm normal, Meter mit "." 1.00 ist ein Meter
            // DimTxtAngle2			=0x00040000, // [Fortsetzug] 4: Bogenlänge
            DimForceTrailingZero = 0x00080000, // Dezimalstellen sollen immer angezeigt werden
        };
        // Flags für die Textbehandlung
        public enum EPointFlag { DimMultiPoint = 0x1, DimMultiLine = 0x2, DimNoExtLine = 0x4 }; // Flags für Punktbemaßung
        // DimMultiLine und DimMultiPoint findet in CONDOR 4 offensichtlich keine Beachtung
        // deshalb wird es hier so verwendet: wenn DimMultiLine true ist, wird die Koordinaten-
        // Bemaßung mit mehreren Maßlinien implementiert, wenn false nur mit einer
        public enum EAngleFlag { DimAngleFourPoint = 0x1, DimAngleBisection = 0x2 }; // Flags für Winkelbemaßung
        public enum EAngleText { DegreeDecimal, DegreeMinuteSecond, Grade, Radian, ArcLength }; // Text für Winkelmaß, Grade ist Neugrad
        public enum ERadiusFlag { DimRadiusBend = 0x1 }; // Flags für Radienbemaßung
        public enum ESymbolFlag { DimSymbolSizePaper = 0x1 }; // Flags für Symbole
        public enum EGeoFlag
        {
            DimNoExtLine1 = 0x1, DimNoExtLine2 = 0x2, DimTextOutsideForceDimensionLine = 0x4,
            DimNoOutsideSymbols = 0x8, DimBreakDimLine = 0x10, DimFixedDimLine = 0x20, DimNoDimLine = 0x40
        }; // Flags für das allgemeine Aussehen
        public enum ESymbol
        {
            DimOpenArrow, DimClosedArrow, DimFilledArrow, DimCircle, DimFilledCircle,
            DimSlash, DimSymbol
        }; // Bemaßungssymbol (long)
        public enum ESymbolPlacement { DimInside, DimOutside, DimAutomatic };
        public enum EFontFlag { DimFontBold, DimFontItalic, DimFontUnderline, DimFontStrikeOut };

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
        public delegate void PropertyChangedDelegate(DimensionStyle sender, string propertyName, object newValue, object oldValue);
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
                FireDidChange("DimBreakDimLine ", value, OldValue);
            }
        }
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
        public override PropertyEntryType Flags
        {
            get
            {
                PropertyEntryType flags = PropertyEntryType.ContextMenu | PropertyEntryType.Selectable | PropertyEntryType.LabelEditable | PropertyEntryType.GroupTitle | PropertyEntryType.HasSubEntries;
                if (parent.Current == this) flags |= PropertyEntryType.Bold;
                return flags;
            }
        }
        public override string LabelText
        {
            get { return name; }
        }
        /// <summary>
        /// Overrides <see cref="PropertyEntryImpl.EntryType"/>, 
        /// returns <see cref="ShowPropertyEntryType.GroupTitle"/>.
        /// </summary>
        public override MenuWithHandler[] ContextMenu
        {
            get
            {
                    return MenuResource.LoadMenuDefinition("MenuId.DimStyleEntry", false, this);
            }
        }
        /// <summary>
        /// Overrides <see cref="PropertyEntryImpl.SubItems"/>, 
        /// returns the subentries in this property view.
        /// </summary>
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
        /// <summary>
        /// Overrides <see cref="PropertyEntryImpl.Added"/>
        /// </summary>
        /// <param name="propertyTreeView"></param>
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
        public override bool EditTextChanged(string newValue)
        {
            return true;
        }
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
        /// Implements <see cref="ISerializable.GetObjectData"/>
        /// </summary>
        /// <param name="info">The <see cref="System.Runtime.Serialization.SerializationInfo"/> to populate with data.</param>
        /// <param name="context">The destination (<see cref="System.Runtime.Serialization.StreamingContext"/>) for this serialization.</param>
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
