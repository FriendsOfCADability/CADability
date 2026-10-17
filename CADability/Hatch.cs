using CADability.Attribute;
using CADability.Shapes;
using CADability.UserInterface;
using System;
using System.Collections.Generic;
using System.Reflection;
using System.Drawing;
using Point = System.Drawing.Point;
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
        /// <summary>
        /// Signature of the handlers of the <see cref="Constructed"/> event.
        /// </summary>
        /// <param name="justConstructed">The hatch that has just been created</param>
        public delegate void ConstructedDelegate(Hatch justConstructed);
        /// <summary>
        /// Raised from the constructor whenever a hatch is created, by <see cref="Construct"/> as well as when a hatch is
        /// read from a file. The handler gets the new hatch; when it was created by <see cref="Construct"/>, its
        /// <see cref="Plane"/>, <see cref="CompoundShape"/> and <see cref="HatchStyle"/> are not set yet. Lets an application
        /// attach to every hatch without deriving from the class.
        /// </summary>
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
        /// <summary>
        /// Signature of the handler for <see cref="OnRecalc"/>.
        /// </summary>
        /// <param name="toRecalc">The hatch whose contents (hatch lines etc.) are to be computed</param>
        /// <returns>The objects that make up the hatch, or null to let the <see cref="HatchStyle"/> generate them</returns>
        public delegate GeoObjectList RecalcDelegate(Hatch toRecalc);
        /// <summary>
        /// Hook to replace the generation of the hatch contents: when set, <see cref="Recalc"/> calls this delegate and uses
        /// the returned objects as the contents of the hatch instead of what
        /// <see cref="CADability.Attribute.HatchStyle.GenerateContent"/> produces. Returning null falls back to the hatch
        /// style. Applies to all hatches. Null by default.
        /// </summary>
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
        /// <inheritdoc/>
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
        /// <summary>
        /// The 2d shape (outline and holes, in the coordinate system of <see cref="Plane"/>) that is filled by
        /// this hatch. Setting it marks the hatch lines for recalculation.
        /// </summary>
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
        /// <inheritdoc/>
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
        /// <inheritdoc/>
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
        /// <inheritdoc/>
        public override IGeoObject[] OwnedItems
        {
            get
            {
                if (needsRecalc) Recalc();
                return base.OwnedItems;
            }
        }
        /// <inheritdoc/>
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
        /// <summary>
        /// Signature of the handler for <see cref="OnPaintTo3D"/>. Hides <see cref="Block.PaintTo3DDelegate"/> in order to
        /// pass the hatch.
        /// </summary>
        /// <param name="toPaint">The hatch that is about to be painted</param>
        /// <param name="paintTo3D">The paint interface to paint to</param>
        /// <returns>true if the handler has painted the hatch itself and the default painting must be skipped, false to let
        /// the hatch paint its contents as usual</returns>
        public new delegate bool PaintTo3DDelegate(Hatch toPaint, IPaintTo3D paintTo3D);
        /// <summary>
        /// Hook to replace the painting of hatches: when set, <see cref="PaintTo3D"/> calls this delegate and does nothing
        /// more if it returns true. It is not consulted for a solid hatch on a paint interface that can fill paths, which is
        /// painted as a filled path directly. Hides <see cref="Block.OnPaintTo3D"/>, which is not used for hatches. Null by default.
        /// </summary>
        public new static PaintTo3DDelegate OnPaintTo3D;
        /// <inheritdoc/>
        public override void PaintTo3DList(IPaintTo3D paintTo3D, ICategorizedDislayLists lists)
        {
            if (this.compoundShape == null) return;
            if (needsRecalc) Recalc();
            base.PaintTo3DList(paintTo3D, lists);
        }
        /// <inheritdoc/>
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
        /// <inheritdoc/>
        public override BoundingRect GetExtent(Projection projection, ExtentPrecision extentPrecision)
        {
            if (compoundShape == null) return BoundingRect.EmptyBoundingRect;
            return compoundShape.Project(this.plane, projection.ProjectionPlane).GetExtent();
        }
        /// <inheritdoc/>
        public override IQuadTreeInsertableZ GetQuadTreeItem(Projection projection, ExtentPrecision extentPrecision)
        {
            ConditionalRecalc();
            return base.GetQuadTreeItem(projection, extentPrecision);
        }
        /// <inheritdoc/>
        public override Style.EDefaultFor PreferredStyle
        {
            get
            {
                return Style.EDefaultFor.Curves;
            }
        }
        /// <inheritdoc/>
        public override BoundingCube GetBoundingCube()
        {
            ConditionalRecalc();
            return base.GetBoundingCube();
        }
        #endregion
        #region IOctTreeInsertable members
        /// <inheritdoc/>
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
        /// <inheritdoc/>
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
        /// <inheritdoc/>
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
        /// <inheritdoc/>
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
        /// Writes the data of this object for the legacy binary serialization (<see cref="System.Runtime.Serialization.ISerializable"/>).
        /// Files are written in the JSON format (<see cref="IJsonSerialize"/>) now; the binary format is still read.
        /// </summary>
        /// <param name="info">receives the data</param>
        /// <param name="context">the destination of the serialization</param>
        public override void GetObjectData(SerializationInfo info, StreamingContext context)
        {
            ConditionalRecalc();
            base.GetObjectData(info, context);
            info.AddValue("CompoundShape", compoundShape);
            info.AddValue("HatchStyle", hatchStyle);
            info.AddValue("Plane", plane);
        }
        /// <summary>
        /// Implements <see cref="IJsonSerialize.GetObjectData"/>: writes the data of the block
        /// (<see cref="Block.GetObjectData(IJsonWriteData)"/>) followed by the shape, the hatch style and the plane of the
        /// hatch. The contents are brought up to date first. Hides the base method so that the interface is bound to this one.
        /// </summary>
        /// <param name="data">receives the data</param>
        new public void GetObjectData(IJsonWriteData data)
        {
            ConditionalRecalc();
            base.GetObjectData(data);  
            data.AddProperty("CompoundShape", compoundShape);
            data.AddProperty("HatchStyle", hatchStyle);
            data.AddProperty("Plane", plane);
        }

        /// <summary>
        /// Implements <see cref="IJsonSerialize.SetObjectData"/>: reads the data of the block
        /// (<see cref="Block.SetObjectData(IJsonReadData)"/>) followed by the shape, the hatch style and the plane of the
        /// hatch. Hides the base method so that the interface is bound to this one.
        /// </summary>
        /// <param name="data">provides the data</param>
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
