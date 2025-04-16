using CADability.Attribute;
using CADability.Shapes;
using CADability.UserInterface;
using System;
using System.Collections.Generic;
using System.Reflection;
#if WEBASSEMBLY
using CADability.WebDrawing;
using Point = CADability.WebDrawing.Point;
#else
using System.Drawing;
using Point = System.Drawing.Point;
#endif
using System.Runtime.Serialization;

namespace CADability.GeoObject
{
    /// <summary>
    /// A GeoObject that represents a flat (2d) shape filled by a <see cref="CADability.Attribute.HatchStyle"/>. 
    /// It also acts as a <see cref="Block"/> presenting e.g. the hatch lines as its 
    /// containing child objects.
    /// To fully define a Hatch object you have to set these three properties: <see cref="Plane"/>, 
    /// <see cref="CompoundShape"/> and <see cref="HatchStyle"/>. If you omit one of these properties
    /// you wont see the Hatch object as it is not fully defined. No defaults are assumed.
    /// </summary>
    [Serializable()]
    public class Hatch : Block, ISerializable, IHatchStyle, IJsonSerialize
    {
        private CompoundShape compoundShape; // die Umrandung
        private HatchStyle hatchStyle; // die Schraffurart
        private Plane plane; // die Ebene
        private bool needsRecalc; // muss neu berechnet werden
        #region polymorph construction
        /// <summary>
        /// Delegate definition for <see cref="Constructor"/>
        /// </summary>
        /// <returns>your derived Hatch Object</returns>
        public new delegate Hatch ConstructionDelegate();
        /// <summary>
        /// Factory mechanism for constructing Hatch objects
        /// If you have a class derivec from this class (Hatch) and everytime CADability creates a hatch
        /// object your object should be created, you have to register your static construct method here.
        /// </summary>
        public new static ConstructionDelegate Constructor;
        /// <summary>
        /// The only way to construct a hatch object. If a <see cref="Constructor"/> is registered,
        /// this constructor will be used, if not a Hatch object is created.
        /// </summary>
        /// <returns></returns>
        public new static Hatch Construct()
        {
            if (Constructor != null) return Constructor();
            return new Hatch();
        }
        public delegate void ConstructedDelegate(Hatch justConstructed);
        public static event ConstructedDelegate Constructed;
        #endregion
        /// <summary>
        /// Empty constructor, must be called when a derived object is constructed.
        /// </summary>
        protected Hatch()
        {
            needsRecalc = false;
            if (Constructed != null) Constructed(this);
        }
        public delegate GeoObjectList RecalcDelegate(Hatch toRecalc);
        public static RecalcDelegate OnRecalc;
        /// <summary>
        /// After the <see cref="HatchStyle"/>, the <see cref="CompoundShape"/> or the <see cref="HatchStyle"/> has been modified
        /// this method must be called to calculate the new contents.
        /// </summary>
        public void Recalc()
        {
            needsRecalc = false;
            GeoObjectList l = null;
            if (OnRecalc != null) l = OnRecalc(this);
            if (compoundShape == null) return;
            if (l == null)
            {
                l = hatchStyle.GenerateContent(compoundShape, plane);
            }
            for (int i = 0; i < l.Count; ++i)
            {
                if (l[i].Layer == null)
                {
                    l[i].Layer = this.Layer;
                }
                if (l[i] is IColorDef && (l[i] as IColorDef).ColorDef == null)
                {
                    (l[i] as IColorDef).ColorDef = this.ColorDef;
                }
            }
            base.ReplaceContent(l); // es werden keine change-events gefeuert!
        }
        internal void ConditionalRecalc()
        {
            if (needsRecalc) Recalc();
        }
        internal void Update()
        {
            if (hatchStyle == null) return;
            using (new Changing(this, "HatchStyle"))
            {
                Recalc();
            }
        }
        public override ColorDef ColorDef
        {
            get
            {
                return base.ColorDef;
            }
            set
            {
                if (base.ColorDef != value)
                {
                    base.ColorDef = value;
                    Update();
                }
            }
        }
        /// <value>
        /// The shape (<see cref="CADability.Shapes.CompoundShape"/>, bounding curves and holes) which is handled by this object
        /// </value>
        public CompoundShape CompoundShape
        {
            get { return compoundShape; }
            set { compoundShape = value; needsRecalc = true; }
        }
        /// <summary>
        /// The <see cref="CADability.Attribute.HatchStyle"/> that defines the interior of this object
        /// </summary>
        public HatchStyle HatchStyle
        {
            get { return hatchStyle; }
            set
            {
                // OnlyAttribute wichtig für ImportDwg, sonst dort HatchStyle gesondert abfangen
                // Die Farge bleibt, wer wertet OnlyAttribute eigentlich aus und wozu?
                // hier ändert ja der HatchStyle die Geometrie
                // using (new Changing(this, false, true, "HatchStyle", new object[] { this.hatchStyle }))
                using (new Changing(this, "HatchStyle"))
                {
                    hatchStyle = value;
                    needsRecalc = true;
                }
            }
        }
        public override Layer Layer
        {
            get
            {
                return base.Layer;
            }
            set
            {
                using (new Changing(this, "Layer")) // default wird ChangingAttribute verwendet und das wird vom OctTree
                // ignoriert, hier aber wird u.U. die Geometrie geändert bzw. die enthaltenen Objekte neu bestimmt
                {
                    base.Layer = value;
                    needsRecalc = true; // die einzelnen Linien u.s.w. müssen auch auf diesen Layer gesetzt werden
                }
            }
        }
        public override Style Style
        {
            get
            {
                return base.Style;
            }
            set
            {
                using (new Changing(this, "HatchStyle")) // default wird ChangingAttribute verwendet und das wird vom OctTree
                // ignoriert, hier aber wird u.U. die Geometrie geändert bzw. die enthaltenen Objekte neu bestimmt
                {
                    base.Style = value;
                }
            }
        }
        /// <summary>
        /// The <see cref="CADability.Plane"/> which defines the 3d position of the <see cref="CompoundShape"/>.
        /// </summary>
        public Plane Plane
        {
            get { return plane; }
            set { plane = value; needsRecalc = true; }
        }
        public override IGeoObject[] OwnedItems
        {
            get
            {
                if (needsRecalc) Recalc();
                return base.OwnedItems;
            }
        }
        /// <summary>
        /// Overrides <see cref="CADability.GeoObject.IGeoObjectImpl.Decompose ()"/>
        /// </summary>
        /// <returns></returns>
        public override GeoObjectList Decompose()
        {
            if (needsRecalc) Recalc();
            GeoObjectList tmp = new GeoObjectList(OwnedItems);
            return tmp.CloneObjects();
        }
        #region IGeoObject overrides
        /// <summary>
        /// Overrides IGeoObjectImpl.<see cref="IGeoObjectImpl.Modify"/> and implements IGeoObject.<see cref="IGeoObject.Modify"/>.
        /// </summary>
        /// <param name="m">see <see cref="IGeoObject.Modify"/></param>
        public override void Modify(ModOp m)
        {
            using (new Changing(this, "ModifyInverse", m))
            {
                // die ModOp wird in zwei Komponente aufgeteilt:
                // eine Skalierungsfreie, die auf die Ebene angewendet wird
                // und eine Skalierung, die nochmal auf die compoundShape wirkt
                try
                {
                    Plane newPlane = new Plane(m * plane.Location, m * plane.DirectionX, m * plane.DirectionY);
                    // die Ebene verändert sich gemäß der Matrix m, wie verändert sich die Umrandung im 2D?
                    // Die brutale Methode: aus den bekannten Veränderungen von 3 Punkten die Matrix bestimmen
                    // das funktioniert, alles andere hat bislang nicht geklappt
                    GeoPoint2D[] src = new GeoPoint2D[3];
                    GeoPoint2D[] dst = new GeoPoint2D[3];
                    src[0] = GeoPoint2D.Origin;
                    src[1] = GeoPoint2D.Origin + GeoVector2D.XAxis;
                    src[2] = GeoPoint2D.Origin + GeoVector2D.YAxis;
                    dst[0] = newPlane.Project(m * plane.ToGlobal(src[0]));
                    dst[1] = newPlane.Project(m * plane.ToGlobal(src[1]));
                    dst[2] = newPlane.Project(m * plane.ToGlobal(src[2]));
                    ModOp2D m2d = ModOp2D.Fit(src, dst, true);
                    compoundShape = compoundShape.GetModified(m2d);
                    plane = newPlane;
                    if (m.Mode == ModOp.ModificationMode.Translation && base.Count > 0)
                    {
                        containedObjects.Modify(m);
                    }
                    else
                    {
                        needsRecalc = true;
                    }
                }
                catch (PlaneException) { } // neue Ebene enthält Nullvector
            }
        }
        /// <summary>
        /// Overrides IGeoObjectImpl.<see cref="IGeoObjectImpl.Clone"/> and implements IGeoObject.<see cref="IGeoObject.Clone"/>.
        /// </summary>
        /// <returns>see <see cref="IGeoObject.Clone"/></returns>
        public override IGeoObject Clone()
        {
            if (needsRecalc) Recalc();
            Hatch res = Construct();
            res.CopyAttributes(this); // am Anfang, damit nicht neu berechnet wird
            res.compoundShape = compoundShape.Clone();
            res.hatchStyle = hatchStyle;
            res.Plane = plane;
            if (base.Count > 0)
            {
                res.ReplaceContent(base.containedObjects.CloneObjects());
            }
            return res;
        }
        /// <summary>
        /// Overrides IGeoObjectImpl.<see cref="IGeoObjectImpl.CopyGeometry"/> and implements IGeoObject.<see cref="IGeoObject.CopyGeometry"/>.
        /// </summary>
        /// <param name="ToCopyFrom">see <see cref="IGeoObject.CopyGeometry"/></param>
        public override void CopyGeometry(IGeoObject ToCopyFrom)
        {
            Hatch toCopyFrom = ToCopyFrom as Hatch;
            toCopyFrom.ConditionalRecalc();
            compoundShape = toCopyFrom.compoundShape.Clone();
            hatchStyle = toCopyFrom.hatchStyle;
            Plane = toCopyFrom.plane;
            if (toCopyFrom.Count > 0)
            {
                ReplaceContent(toCopyFrom.containedObjects.CloneObjects());
            }
        }
        /// <summary>
        /// Overrides IGeoObjectImpl.<see cref="IGeoObjectImpl.GetAttributeProperties"/> and implements IGeoObject.<see cref="IGeoObject.GetAttributeProperties"/>.
        /// </summary>
        /// <param name="Frame">see <see cref="IGeoObject.GetAttributeProperties"/></param>
        /// <returns>see <see cref="IGeoObject.GetAttributeProperties"/></returns>
        public override IPropertyEntry[] GetAttributeProperties(IFrame Frame)
        {
            List<IPropertyEntry> res = new List<IPropertyEntry>(base.GetAttributeProperties(Frame));
            // die HatchStyleSelectionProperty wird nach der farbe und dem Stil eingefügt, sieht besser aus
            // im Zusammenhang mit PFOCAD
            res.Insert(4, new HatchStyleSelectionProperty("HatchStyle.Selection", Frame.Project.HatchStyleList, this as IHatchStyle, false));
            return res.ToArray();
        }
        /// <summary>
        /// Overrides IGeoObjectImpl.<see cref="IGeoObjectImpl.GetShowProperties"/> and implements IGeoObject.<see cref="IGeoObject.GetShowProperties"/>.
        /// </summary>
        /// <param name="Frame">see <see cref="IGeoObject.GetShowProperties"/></param>
        /// <returns>see <see cref="IGeoObject.GetShowProperties"/></returns>
        public override IPropertyEntry GetShowProperties(IFrame Frame)
        {
            return new ShowPropertyHatch(this, Frame);
        }
        /// <summary>
        /// Overrides IGeoObjectImpl.<see cref="IGeoObjectImpl.IsAttributeUsed"/> and implements IGeoObject.<see cref="IGeoObject.IsAttributeUsed"/>.
        /// </summary>
        /// <param name="Attribute">see <see cref="IGeoObject.IsAttributeUsed"/></param>
        /// <returns>see <see cref="IGeoObject.IsAttributeUsed"/></returns>
        public override bool IsAttributeUsed(object Attribute)
        {
            if (Attribute == hatchStyle) return true;
            return base.IsAttributeUsed(Attribute);
        }
        public new delegate bool PaintTo3DDelegate(Hatch toPaint, IPaintTo3D paintTo3D);
        public new static PaintTo3DDelegate OnPaintTo3D;
        /// <summary>
        /// Overrides <see cref="CADability.GeoObject.IGeoObjectImpl.PaintTo3DList (IPaintTo3D, ICategorizedDislayLists)"/>
        /// </summary>
        /// <param name="paintTo3D"></param>
        /// <param name="lists"></param>
        public override void PaintTo3DList(IPaintTo3D paintTo3D, ICategorizedDislayLists lists)
        {
            if (this.compoundShape == null) return;
            if (needsRecalc) Recalc();
            base.PaintTo3DList(paintTo3D, lists);
        }
        /// <summary>
        /// Overrides <see cref="CADability.GeoObject.IGeoObjectImpl.PaintTo3D (IPaintTo3D)"/>
        /// </summary>
        /// <param name="paintTo3D"></param>
        public override void PaintTo3D(IPaintTo3D paintTo3D)
        {
            if (this.compoundShape == null) return;
            if ((paintTo3D.Capabilities & PaintCapabilities.CanFillPaths) != 0 && hatchStyle is HatchStyleSolid)
            {
                paintTo3D.OpenPath();
                Path[] paths = compoundShape.MakePaths(this.plane);
                for (int i = 0; i < paths.Length; i++)
                {
                    paths[i].PaintTo3D(paintTo3D);
                    paintTo3D.CloseFigure();
                }
                Color clr;
                if (paintTo3D.SelectMode)
                {
                    clr = paintTo3D.SelectColor;
                }
                else
                {
                    if ((hatchStyle as HatchStyleSolid).Color == null && this.ColorDef != null)
                        clr = this.ColorDef.Color;
                    else
                        clr = (hatchStyle as HatchStyleSolid).Color.Color;
                }
                paintTo3D.ClosePath(clr);
            }
            else
            {
                if (needsRecalc) Recalc();
                if (OnPaintTo3D != null && OnPaintTo3D(this, paintTo3D)) return;
                bool ps = paintTo3D.PaintSurfaces;
                paintTo3D.PaintFaces(CADability.PaintTo3D.PaintMode.All); // kommt immer mit CurvesOnly...
                for (int i = 0; i < base.Count; ++i)
                {
                    (Child(i) as IGeoObjectImpl).PaintTo3D(paintTo3D);
                }
                if (!ps) paintTo3D.PaintFaces(CADability.PaintTo3D.PaintMode.CurvesOnly);
            }
        }
        /// <summary>
        /// Overrides <see cref="CADability.GeoObject.IGeoObjectImpl.GetExtent (Projection, ExtentPrecision)"/>
        /// </summary>
        /// <param name="projection"></param>
        /// <param name="extentPrecision"></param>
        /// <returns></returns>
        public override BoundingRect GetExtent(Projection projection, ExtentPrecision extentPrecision)
        {
            if (compoundShape == null) return BoundingRect.EmptyBoundingRect;
            return compoundShape.Project(this.plane, projection.ProjectionPlane).GetExtent();
        }
        /// <summary>
        /// Overrides <see cref="CADability.GeoObject.IGeoObjectImpl.GetQuadTreeItem (Projection, ExtentPrecision)"/>
        /// </summary>
        /// <param name="projection"></param>
        /// <param name="extentPrecision"></param>
        /// <returns></returns>
        public override IQuadTreeInsertableZ GetQuadTreeItem(Projection projection, ExtentPrecision extentPrecision)
        {
            ConditionalRecalc();
            return base.GetQuadTreeItem(projection, extentPrecision);
        }
        public override Style.EDefaultFor PreferredStyle
        {
            get
            {
                return Style.EDefaultFor.Curves;
            }
        }
        /// <summary>
        /// Overrides <see cref="CADability.GeoObject.IGeoObjectImpl.GetBoundingCube ()"/>
        /// </summary>
        /// <returns></returns>
        public override BoundingCube GetBoundingCube()
        {
            ConditionalRecalc();
            return base.GetBoundingCube();
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
            BoundingCube res = BoundingCube.EmptyBoundingCube;
            if (compoundShape != null)
            {
                for (int i = 0; i < compoundShape.SimpleShapes.Length; ++i)
                {
                    SimpleShape ss = compoundShape.SimpleShapes[i];
                    Border bdr = ss.Outline;
                    res.MinMax(bdr.AsPath().MakeGeoObject(plane).GetExtent(precision));
                }
            }
            return res;
        }
        /// <summary>
        /// Overrides <see cref="CADability.GeoObject.IGeoObjectImpl.HitTest (ref BoundingCube, double)"/>
        /// </summary>
        /// <param name="cube"></param>
        /// <param name="precision"></param>
        /// <returns></returns>
        public override bool HitTest(ref BoundingCube cube, double precision)
        {
            if (base.Count == 0) Recalc();
            if (base.Count > 0)
            {   // LinienSchraffur oder so
                return base.HitTest(ref cube, precision);
            }
            else
            {   // SolidSchraffur
                //for (int i = 0; i < compoundShape.SimpleShapes.Length; ++i)
                //{
                //    Face fc = Face.MakeFace(new PlaneSurface(plane), compoundShape.SimpleShapes[i]);
                //    if (fc.HitTest(ref cube, precision)) return true;
                //}
                return false;
            }
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
            CompoundShape cs = compoundShape.Project(plane, projection.ProjectionPlane);
            if (onlyInside)
            {
                return cs.GetExtent() <= rect;
            }
            else
            {
                if (base.Count > 0)
                {   // LinienSchraffur oder so
                    return base.HitTest(projection, rect, onlyInside);
                }
                else
                {   // SolidSchraffur
                    return cs.HitTest(ref rect);
                }
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
            GeoPoint p = plane.Intersect(fromHere, direction);
            return Geometry.LinePar(fromHere, direction, p); // nicht getestet ob Schraffur auch getroffen
        }
        #endregion
        #region ISerializable Members
        /// <summary>
        /// Constructor required by deserialization
        /// </summary>
        /// <param name="info">SerializationInfo</param>
        /// <param name="context">StreamingContext</param>
        protected Hatch(SerializationInfo info, StreamingContext context)
            : base(info, context)
        {
            compoundShape = (CompoundShape)info.GetValue("CompoundShape", typeof(CompoundShape));
            hatchStyle = (HatchStyle)info.GetValue("HatchStyle", typeof(HatchStyle));
            plane = (Plane)info.GetValue("Plane", typeof(Plane));
            // needsRecalc = true;
            if (Constructed != null) Constructed(this);
        }

        /// <summary>
        /// Implements ISerializable:GetObjectData
        /// </summary>
        /// <param name="info"></param>
        /// <param name="context"></param>
        public override void GetObjectData(SerializationInfo info, StreamingContext context)
        {
            ConditionalRecalc();
            base.GetObjectData(info, context);
            info.AddValue("CompoundShape", compoundShape);
            info.AddValue("HatchStyle", hatchStyle);
            info.AddValue("Plane", plane);
        }
        new public void GetObjectData(IJsonWriteData data)
        {
            ConditionalRecalc();
            base.GetObjectData(data);  
            data.AddProperty("CompoundShape", compoundShape);
            data.AddProperty("HatchStyle", hatchStyle);
            data.AddProperty("Plane", plane);
        }

        new public void SetObjectData(IJsonReadData data)
        {
            base.SetObjectData(data);
            compoundShape = data.GetProperty<CompoundShape>("CompoundShape");
            hatchStyle = data.GetProperty<HatchStyle>("HatchStyle");
            plane = data.GetProperty<Plane>("Plane");
        }
        #endregion
        #region IHatchStyle Members
        HatchStyle IHatchStyle.HatchStyle
        {
            get
            {
                return HatchStyle;
            }
            set
            {
                HatchStyle = value;
            }
        }
        #endregion

    }
}
