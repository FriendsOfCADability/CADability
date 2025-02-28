using CADability.Curve2D;
using CADability.GeoObject;
using CADability.Shapes;
using CADability.UserInterface;
using System;
using System.Runtime.Serialization;

namespace CADability.Attribute
{
    /// <summary>
    /// An implementation of a <see cref="HatchStyle"/>, that defines a style consisting of
    /// parallel lines.
    /// </summary>
    [Serializable()]
    public class HatchStyleLines : HatchStyle, ISerializable
    {
        private double lineDistance;
        private Angle lineAngle;
        private double marginOffset; // Randabstand
        private int number;
        private SweepAngle offset;
        private bool alternate;
        private LineWidth lineWidth;
        private LinePattern linePattern;
        private ColorDef colorDef;
        private IPropertyEntry[] subItems;
        internal override void Init(Project pr)
        {
            lineDistance = 1.0;
            lineAngle = Angle.A45;
            marginOffset = 0.0;
            number = 1;
            offset = SweepAngle.ToLeft;
            alternate = false;
            lineWidth = pr.LineWidthList.Current;
            linePattern = pr.LinePatternList.Current;
            colorDef = pr.ColorList.Current;
        }
        public HatchStyleLines()
        {
            // 
            // TODO: Add constructor logic here
            //
            resourceIdInternal = "HatchStyleNameLines";
        }
        /// <summary>
        /// This method is used by a <see cref="Hatch"/> object to calculate its contents. It generates a
        /// set of parallel lines according to <see cref="LineDistance"/> and <see cref="LineAngle"/> 
        /// and clips them with the given shape. The lines are projected into the 3D world
        /// by the given plane. <seealso cref="CompoundShape.Clip"/>
        /// </summary>
        /// <param name="shape">shape of the hatch object</param>
        /// <param name="plane">local plane of the hatch object</param>
        /// <returns></returns>
        public override GeoObjectList GenerateContent(CompoundShape shape, Plane plane)
        {
            GeoObjectList res = new GeoObjectList();
            if (shape == null) return res;
            if (marginOffset != 0.0)
            {
                shape.Approximate(false, Settings.GlobalSettings.GetDoubleValue("Approximate.Precision", 0.01));
                shape = shape.Shrink(marginOffset);
            }
            for (int i = 0; i < Math.Max(1, number); ++i)
            {
                double a = lineAngle.Radian + i * (double)offset;
                ModOp2D m = ModOp2D.Rotate(new SweepAngle(-a));
                BoundingRect ext = shape.GetExtent();
                ext.Modify(m); // ext ist jetzt waagrecht, u.U. zu groß, was aber egal ist
                m = ModOp2D.Rotate(new SweepAngle(a)); // die umgekehrte ModOp
                int alt = 0;
                // eine Linie soll durch die Mitte gehen
                double c = (ext.Bottom + ext.Top) / 2.0;
                double num = Math.Ceiling((c - ext.Bottom) / lineDistance);
                ext.Bottom = c - num * lineDistance; // das untere Ende sowei nach unten verschoben, dass eine Linie durch die mitte geht
                // das ist wichtig, weil sonst manche Schraffuren nicht zu picken sind, da keine Linie darin erscheint
                // da eine Schraffur meist nur aus einem SimpleShape besteht, gibt es immer eine Linie durch die Mitte
                for (double y = ext.Bottom; y < ext.Top; y += lineDistance)
                {
                    GeoPoint2D startPoint = m * new GeoPoint2D(ext.Left, y);
                    GeoPoint2D endPoint = m * new GeoPoint2D(ext.Right, y);
                    Line2D l = new Line2D(startPoint, endPoint);
                    if (alternate && ((alt & 0x01) != 0)) l.Reverse();
                    double[] seg = shape.Clip(l, true);
                    for (int j = 0; j < seg.Length; j += 2)
                    {
                        IGeoObject go = l.Trim(seg[j], seg[j + 1]).MakeGeoObject(plane);
                        (go as ILineWidth).LineWidth = lineWidth;
                        (go as ILinePattern).LinePattern = linePattern;
                        (go as IColorDef).ColorDef = colorDef;
                        res.Add(go);
                    }
                    ++alt;
                }
            }
            return res;
        }
        public override IShowProperty GetShowProperty()
        {
            return null;
        }
        public override HatchStyle Clone()
        {
            HatchStyleLines res = new HatchStyleLines();
            res.Name = base.Name;
            res.lineAngle = lineAngle;
            res.lineDistance = lineDistance;
            res.marginOffset = marginOffset;
            res.number = number;
            res.offset = offset;
            res.alternate = alternate;
            res.lineWidth = lineWidth;
            res.linePattern = linePattern;
            res.colorDef = colorDef;
            return res;
        }
        /// <summary>
        /// Sets or gets the line distance for this hatchstyle.
        /// </summary>
        public double LineDistance
        {
            get { return lineDistance; }
            set { lineDistance = value; }
        }
        /// <summary>
        /// Sets or gets the lina angle for this hatch style.
        /// </summary>
        public Angle LineAngle
        {
            get { return lineAngle; }
            set { lineAngle = value; }
        }
        /// <summary>
        /// Sets or gets the distance from the shapes border
        /// </summary>
        public double MarginOffset
        {
            get { return marginOffset; }
            set { marginOffset = value; }
        }
        public int Number
        {
            get
            {
                return number;
            }
            set
            {
                number = value;
            }
        }
        public SweepAngle Offset
        {
            get
            {
                return offset;
            }
            set
            {
                offset = value;
            }
        }
        protected Angle AOffset // we need AOffset to be able to manipulate Offset as an AngleProperty
        {
            get
            {
                return offset;
            }
            set
            {
                offset = value;
            }
        }
        public bool Alternate
        {
            get
            {
                return alternate;
            }
            set
            {
                alternate = value;
            }
        }
        /// <summary>
        /// Gets or sets the line width for the lines of this hatch style
        /// </summary>
        public LineWidth LineWidth
        {
            get
            {
                return lineWidth;
            }
            set
            {
                lineWidth = value;
            }
        }
        /// <summary>
        /// Gets or sets the line pattern for the lines of this hatch style
        /// </summary>
        public LinePattern LinePattern
        {
            get
            {
                return linePattern;
            }
            set
            {
                linePattern = value;
            }
        }
        /// <summary>
        /// Gets or sets the color of the lines of this hatch style
        /// </summary>
        public ColorDef ColorDef
        {
            get
            {
                return colorDef;
            }
            set
            {
                colorDef = value;
            }
        }
        #region IShowProperty Members
        /// <summary>
        /// Overrides <see cref="IShowPropertyImpl.SubEntries"/>, 
        /// returns the subentries in this property view.
        /// </summary>
        public override IPropertyEntry[] SubItems
        {
            get
            {
                if (subItems == null)
                {
                    subItems = new IPropertyEntry[9];
                    AngleProperty ap = new AngleProperty("HatchStyleLines.Angle", base.propertyPage.GetFrame(), false);
                    ap.GetAngleEvent += new AngleProperty.GetAngleDelegate(OnPropertyGetAngle);
                    ap.SetAngleEvent += new AngleProperty.SetAngleDelegate(OnPropertySetAngle);
                    ap.ShowMouseButton = false;
                    ap.AngleChanged();
                    subItems[0] = ap;
                    LengthProperty lp = new LengthProperty("HatchStyleLines.Distance", base.propertyPage.GetFrame(), false);
                    lp.GetLengthEvent += new CADability.UserInterface.LengthProperty.GetLengthDelegate(OnPropertyGetDistance);
                    lp.SetLengthEvent += new CADability.UserInterface.LengthProperty.SetLengthDelegate(OnPropertySetDistance);
                    lp.LengthChanged();
                    lp.ShowMouseButton = false;
                    subItems[1] = lp;
                    LengthProperty mp = new LengthProperty("HatchStyleLines.MarginOffset", base.propertyPage.GetFrame(), false);
                    mp.GetLengthEvent += new CADability.UserInterface.LengthProperty.GetLengthDelegate(OnPropertyGetMarginOffset);
                    mp.SetLengthEvent += new CADability.UserInterface.LengthProperty.SetLengthDelegate(OnPropertySetMarginOffset);
                    mp.Refresh();
                    mp.ShowMouseButton = false;
                    subItems[2] = mp;
                    IntegerProperty on = new IntegerProperty(this, "Number", "HatchStyleLines.OffsetNumber");
                    subItems[3] = on;
                    AngleProperty of = new AngleProperty(this, "AOffset", "HatchStyleLines.Offset", base.propertyPage.GetFrame(), false);
                    subItems[4] = of;
                    BooleanProperty al = new BooleanProperty(this, "Alternate", "HatchStyleLines.Alternate");
                    subItems[5] = al;

                    Project pr = base.propertyPage.GetFrame().Project;
                    LineWidthSelectionProperty lws = new LineWidthSelectionProperty("HatchStyleLines.LineWidth", pr.LineWidthList, this.lineWidth);
                    lws.LineWidthChangedEvent += new CADability.UserInterface.LineWidthSelectionProperty.LineWidthChangedDelegate(OnLineWidthChanged);
                    subItems[6] = lws;
                    LinePatternSelectionProperty lps = new LinePatternSelectionProperty("HatchStyleLines.LinePattern", pr.LinePatternList, this.linePattern);
                    lps.LinePatternChangedEvent += new CADability.UserInterface.LinePatternSelectionProperty.LinePatternChangedDelegate(OnLinePatternChanged);
                    subItems[7] = lps;
                    ColorSelectionProperty csp = new ColorSelectionProperty("HatchStyleLines.Color", pr.ColorList, colorDef, ColorList.StaticFlags.allowUndefined);
                    csp.ShowAllowUndefinedGray = false;
                    csp.ColorDefChangedEvent += new ColorSelectionProperty.ColorDefChangedDelegate(OnColorDefChanged);
                    subItems[8] = csp;
                }
                return subItems;
            }
        }
        /// <summary>
        /// Overrides <see cref="IShowPropertyImpl.Removed"/>
        /// </summary>
        /// <param name="propertyPage">the IPropertyTreeView from which it was removed</param>
        public override void Removed(IPropertyPage pp)
        {
            subItems = null;
            base.Removed(pp);
        }

        #endregion
        internal override void Update(bool AddMissingToList)
        {
            if (Parent != null && Parent.Owner != null)
            {
                ColorList cl = Parent.Owner.ColorList;
                if (cl != null && colorDef != null)
                {
                    ColorDef cd = cl.Find(colorDef.Name);
                    if (cd != null)
                        colorDef = cd;
                    else if (AddMissingToList)
                        cl.Add(colorDef);
                }
                LineWidthList ll = Parent.Owner.LineWidthList;
                if (ll != null && lineWidth != null)
                {
                    LineWidth lw = ll.Find(lineWidth.Name);
                    if (lw != null)
                        lineWidth = lw;
                    else if (AddMissingToList)
                        ll.Add(lineWidth);
                }
                LinePatternList pl = Parent.Owner.LinePatternList;
                if (pl != null && linePattern != null)
                {
                    LinePattern lw = pl.Find(linePattern.Name);
                    if (lw != null)
                        linePattern = lw;
                    else if (AddMissingToList)
                        pl.Add(linePattern);
                }
            }
        }
        private Angle OnPropertyGetAngle()
        {
            return lineAngle;
        }
        private void OnPropertySetAngle(Angle a)
        {
            Angle oldlineAngle = lineAngle;
            lineAngle = a;
            FireDidChange("LineAngle", lineAngle);
        }
        private double OnPropertyGetDistance(LengthProperty sender)
        {
            return lineDistance;
        }
        private void OnPropertySetDistance(LengthProperty sender, double l)
        {
            double oldLineDistance = lineDistance;
            lineDistance = l;
            FireDidChange("LineDistance", oldLineDistance);
        }
        private double OnPropertyGetMarginOffset(LengthProperty sender)
        {
            return marginOffset;
        }
        private void OnPropertySetMarginOffset(LengthProperty sender, double l)
        {
            double oldMarginOffset = marginOffset;
            marginOffset = l;
            FireDidChange("MarginOffset", oldMarginOffset);
        }
        #region ISerializable Members
        /// <summary>
        /// Constructor required by deserialization
        /// </summary>
        /// <param name="info">SerializationInfo</param>
        /// <param name="context">StreamingContext</param>
        protected HatchStyleLines(SerializationInfo info, StreamingContext context)
            : base(info, context)
        {
            lineDistance = (double)info.GetValue("LineDistance", typeof(double));
            lineAngle = (Angle)info.GetValue("LineAngle", typeof(Angle));
            colorDef = ColorDef.Read("ColorDef", info, context);
            lineWidth = LineWidth.Read("LineWidth", info, context);
            linePattern = LinePattern.Read("LinePattern", info, context);
            try
            {
                marginOffset = (double)info.GetValue("MarginOffset", typeof(double));
            }
            catch (SerializationException)
            {
                marginOffset = 0.0;
            }
            try
            {
                number = (int)info.GetValue("NumberOffset", typeof(int));
                offset = (double)info.GetValue("Offset", typeof(double)); // warum ist das double?
                alternate = (bool)info.GetValue("Alternate", typeof(bool));
            }
            catch (SerializationException)
            {
                number = 1;
                offset = SweepAngle.ToLeft;
                alternate = false;
            }
        }
        /// <summary>
        /// Implements <see cref="ISerializable.GetObjectData"/>
        /// </summary>
        /// <param name="info">The <see cref="System.Runtime.Serialization.SerializationInfo"/> to populate with data.</param>
        /// <param name="context">The destination (<see cref="System.Runtime.Serialization.StreamingContext"/>) for this serialization.</param>
        public new void GetObjectData(SerializationInfo info, StreamingContext context)
        {
            base.GetObjectData(info, context);
            info.AddValue("LineDistance", lineDistance);
            info.AddValue("LineAngle", lineAngle, typeof(Angle)); // sonst nimmte er ein double!
            info.AddValue("MarginOffset", marginOffset);
            info.AddValue("NumberOffset", number);
            info.AddValue("Offset", offset); // warum wird das double?
            info.AddValue("Alternate", alternate);
            info.AddValue("ColorDef", colorDef);
            info.AddValue("LineWidth", lineWidth);
            info.AddValue("LinePattern", linePattern);
        }
        #endregion
        private void OnLineWidthChanged(LineWidth selected)
        {
            LineWidth oldLineWidth = lineWidth;
            lineWidth = selected;
            FireDidChange("LineWidth", oldLineWidth);
        }
        private void OnLinePatternChanged(LinePattern selected)
        {
            LinePattern oldLinePattern = linePattern;
            linePattern = selected;
            FireDidChange("LinePattern", oldLinePattern);

        }
        private void OnColorDefChanged(ColorDef selected)
        {
            ColorDef oldColorDef = colorDef;
            colorDef = selected;
            FireDidChange("ColorDef", oldColorDef);
        }
    }
}
