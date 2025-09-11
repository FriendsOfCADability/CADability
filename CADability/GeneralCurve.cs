using CADability.Attribute;
using CADability.Curve2D;
using CADability.UserInterface;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Runtime.Serialization;
using MathNet.Numerics.LinearAlgebra;
using MathNet.Numerics.LinearAlgebra.Double;

namespace CADability.GeoObject
{

    public class GeneralCurveException : ApplicationException
    {
        public GeneralCurveException(string Message)
            : base(Message)
        {
        }
    }

    internal class ShowPropertyGeneralCurve : PropertyEntryImpl
    {
        private GeneralCurve generalCurve;
        private IPropertyEntry[] subEntries;
        private IPropertyEntry[] attributeProperties; // Anzeigen für die Attribute (Ebene, Farbe u.s.w)
        public ShowPropertyGeneralCurve(GeneralCurve GeneralCurve, IFrame frame) : base(frame)
        {
            this.generalCurve = GeneralCurve;
            attributeProperties = generalCurve.GetAttributeProperties(Frame);
            base.resourceIdInternal = "General.Curve";
        }
        #region PropertyEntryImpl Overrides
        public override PropertyEntryType Flags
        {
            get
            {
                return PropertyEntryType.GroupTitle | PropertyEntryType.HasSubEntries;
            }
        }
        public override IPropertyEntry[] SubItems
        {
            get
            {
                if (subEntries == null)
                {
                    GeoPointProperty startPointProperty = new GeoPointProperty(this, "StartPoint", "GeneralCurve.StartPoint", Frame, false);
                    startPointProperty.ReadOnly = true;
                    GeoPointProperty endPointProperty = new GeoPointProperty(this, "EndPoint", "GeneralCurve.EndPoint", Frame, false);
                    endPointProperty.ReadOnly = true;

                    IPropertyEntry[] mainProps = {
                                                     startPointProperty,
                                                     endPointProperty
                                                 };
                    subEntries = PropertyEntryImpl.Concat(mainProps, attributeProperties);
                }
                return subEntries;
            }
        }
        public GeoPoint StartPoint
        {
            get
            {
                return generalCurve.StartPoint;
            }
        }
        public GeoPoint EndPoint
        {
            get
            {
                return generalCurve.EndPoint;
            }
        }
        #endregion
    }

    /* GeneralCurve
     * Dient u.a. als Basis für Kanten, die auf zwei Oberflächen basieren.
     */
    /// <summary>
    /// A base implementation of ICurve
    /// </summary>
    public abstract class GeneralCurve : IGeoObjectImpl, IColorDef, ICurve, ILineWidth, ILinePattern
    {
        [Serializable]
        private class ProjectedCurve : GeneralCurve2D, ISerializable
        {
            private ICurve curve;
            private Plane plane;
            public ProjectedCurve(ICurve curve, Plane plane)
            {
                this.curve = curve;
                this.plane = plane;
            }
            protected override void GetTriangulationBasis(out GeoPoint2D[] points, out GeoVector2D[] directions, out double[] parameters)
            {
                parameters = curve.GetSavePositions();
                points = new GeoPoint2D[parameters.Length];
                directions = new GeoVector2D[parameters.Length];
                for (int i = 0; i < parameters.Length; ++i)
                {
                    points[i] = PointAt(parameters[i]);
                    directions[i] = DirectionAt(parameters[i]);
                }
            }

            public override GeoVector2D DirectionAt(double Position)
            {
                GeoVector v = curve.DirectionAt(Position);
                return plane.Project(v);
            }

            public override GeoPoint2D PointAt(double Position)
            {
                GeoPoint p = curve.PointAt(Position);
                return plane.Project(p);
            }
            public override void Reverse()
            {
                curve.Reverse();
                base.ClearTriangulation();
            }

            public override ICurve2D Clone()
            {
                return new ProjectedCurve(curve, plane);
            }

            public override void Copy(ICurve2D toCopyFrom)
            {
                ProjectedCurve pc = toCopyFrom as ProjectedCurve;
                if (pc != null)
                {
                    this.curve = pc.curve;
                    this.plane = pc.plane;
                }
            }
            #region ISerializable Members
            /// <summary>
            /// Constructor required by deserialization
            /// </summary>
            /// <param name="info">SerializationInfo</param>
            /// <param name="context">StreamingContext</param>
            protected ProjectedCurve(SerializationInfo info, StreamingContext context)
            : base(info, context)
            {
                plane = (Plane)info.GetValue("Plane", typeof(Plane));
                curve = (ICurve)info.GetValue("Curve", typeof(ICurve));
            }

            /// <summary>
            /// Implements <see cref="ISerializable.GetObjectData"/>
            /// </summary>
            /// <param name="info">The <see cref="System.Runtime.Serialization.SerializationInfo"/> to populate with data.</param>
            /// <param name="context">The destination (<see cref="System.Runtime.Serialization.StreamingContext"/>) for this serialization.</param>
            public override void GetObjectData(SerializationInfo info, StreamingContext context)
            {
                base.GetObjectData(info, context);
                info.AddValue("Plane", plane);
                info.AddValue("Curve", curve);
            }

            public override bool TryPointDeriv2At(double position, out GeoPoint2D point, out GeoVector2D deriv, out GeoVector2D deriv2)
            {
                if (curve.TryPointDeriv2At(position, out GeoPoint p3d, out GeoVector d1, out GeoVector d2))
                {
                    point = plane.Project(p3d);
                    deriv = plane.Project(d1);
                    deriv2 = plane.Project(d2);
                    return true;
                }
                else
                {
                    point = GeoPoint2D.Origin;
                    deriv = GeoVector2D.NullVector;
                    deriv2 = GeoVector2D.NullVector;
                    return false;
                }
            }

            #endregion

        }
        private ColorDef colorDef; // die Farbe. 
        #region polymorph construction
        public delegate GeneralCurve ConstructionDelegate();
        public static ConstructionDelegate Constructor;
        //public static GeneralCurve Construct()
        //{
        //    if (Constructor != null) return Constructor();
        //    return new GeneralCurve();
        //}
        #endregion
        protected GeneralCurve()
        {
        }
        private GeoPoint[] tetraederBase; // Punkte auf der Kurve
        private double[] tetraederParams; // Parameter zu tetraederBase
        private GeoPoint[] tetraederVertex; // zu jedem tetraederBase (bis auf den letzten) gibt es zwei Punkte, so dass ein Tetraeder aufgespannt wird
        private TetraederHull tetraederHull;
        private BoundingCube extent = BoundingCube.EmptyBoundingCube;
        internal TetraederHull TetraederHull
        {
            get
            {
                if (tetraederHull == null)
                {
                    tetraederHull = new TetraederHull(this);
                }
                return tetraederHull;
            }
        }
        // die Orientierung ist dabei berücksichtigt
        protected abstract double[] GetBasePoints();
        //{
        //    return new double[] { 0.0, 0.1, 0.2, 0.3, 0.4, 0.5, 0.6, 0.7, 0.8, 0.9, 1.0 };
        //}
        private void MakeTetraeder(double from, double to, List<GeoPoint> points, List<double> pars, List<GeoPoint> vertices)
        {
            GeoPoint p1 = PointAt(from);
            GeoPoint p2 = PointAt(to);
            GeoVector v1 = DirectionAt(from);
            GeoVector v2 = DirectionAt(to);
            GeoVector d = p2 - p1;
            GeoVector v1x = d ^ v1;
            GeoVector v2x = d ^ v2;
            if (Precision.IsNullVector(v1x) || Precision.IsNullVector(v2x))
            {   // gerade Verbindung
                pars.Add(from);
                points.Add(p1);
                GeoPoint pm = new GeoPoint(p1, p2);
                vertices.Add(pm);
                vertices.Add(pm);
                return;
            }
            Plane pl1 = new Plane(p1, v1x, v1); // Ebene am Statpunkt tangential zur Kurve
            Plane pl2 = new Plane(p2, v2, v2x); // Ebene am Endpunkt tangential zur Kurve
            Double cos = pl1.Normal * pl2.Normal;
            if (cos < 0.7 && (to - from) > 0.01)
            {   // der Schnittwinkel der Ebene ist zu spitz, also aufteilen
                double m = (from + to) / 2.0;
                MakeTetraeder(from, m, points, pars, vertices);
                MakeTetraeder(m, to, points, pars, vertices);
            }
            else
            {
                Plane pl3 = new Plane(p1, d, v1); // Ebene durch die Verbindung, tangential am Startpunkt
                Plane pl4 = new Plane(p1, d, v2); // Ebene durch die Verbindung, tangential am Endpunkt
                GeoPoint ip;
                GeoVector id;
                if (pl1.Intersect(pl2, out ip, out id))
                {
                    GeoPoint p3 = pl3.Intersect(ip, id);
                    GeoPoint p4 = pl4.Intersect(ip, id);
                    // Orientierung überprüfen
                    pars.Add(from);
                    points.Add(p1);
                    if ((((p2 - p1) ^ (p3 - p1)) * (p4 - p1)) < 0)
                    {
                        vertices.Add(p4);
                        vertices.Add(p3);
                    }
                    else
                    {
                        vertices.Add(p3);
                        vertices.Add(p4);
                    }
                }
                else
                {   // es geht völlig gerade weiter
                    pars.Add(from);
                    points.Add(p1);
                    GeoPoint pm = new GeoPoint(p1, p2);
                    vertices.Add(pm);
                    vertices.Add(pm);
                }
            }
        }
        protected void MakeTetraederHull()
        {
            // 1. Implementierung: Die Dreiecke werden richtig gebildet
            // noch keine Aufteilung, wenn "Wendepunkt" (2. Ableitung der Kurve?)
            double[] pos = GetBasePoints();
            List<GeoPoint> points = new List<GeoPoint>();
            List<double> pars = new List<double>();
            List<GeoPoint> vertices = new List<GeoPoint>();
            for (int i = 0; i < pos.Length - 1; ++i)
            {
                MakeTetraeder(pos[i], pos[i + 1], points, pars, vertices);
            }
            tetraederBase = points.ToArray();
            tetraederParams = pars.ToArray();
            tetraederVertex = vertices.ToArray();

            //GeoObjectList dbg = new GeoObjectList();
            //Face fc1 = Face.MakeFace(p1, p2, p3);
            //dbg.Add(fc1);
            //Face fc2 = Face.MakeFace(p1, p2, p4);
            //dbg.Add(fc2);
            //Face fc3 = Face.MakeFace(p1, p3, p4);
            //dbg.Add(fc3);
            //Face fc4 = Face.MakeFace(p2, p3, p4);
            //dbg.Add(fc4);
        }
        protected virtual void InvalidateSecondaryData()
        {
            tetraederHull = null;
            extent = BoundingCube.EmptyBoundingCube;
        }
        // public abstract void Modify(ModOp m); ist schon abstract
        /// <summary>
        /// Overrides <see cref="CADability.GeoObject.IGeoObjectImpl.PaintTo3D (IPaintTo3D)"/>
        /// </summary>
        /// <param name="paintTo3D"></param>
        public override void PaintTo3D(IPaintTo3D paintTo3D)
        {
            if (paintTo3D.SelectMode)
            {
                // paintTo3D.SetColor(paintTo3D.SelectColor);
            }
            else
            {
                if (colorDef != null) paintTo3D.SetColor(colorDef.Color);
            }
            if (lineWidth != null) paintTo3D.SetLineWidth(lineWidth);
            if (linePattern != null) paintTo3D.SetLinePattern(linePattern);
            try
            {
                ICurve crv = Approximate(true, paintTo3D.Precision);
                (crv as IGeoObject).PaintTo3D(paintTo3D);
            }
            catch (PolylineException) { } // zu kurze Linien, nix machen
        }
        //public abstract IGeoObject Clone() // das ist schon abstract
        //{
        //    return null; // eigentlich abstract
        //    //GeneralCurve gc = Construct();
        //    //gc.SetOcasBuddy(ocasBuddy.Clone());
        //    //return gc;
        //}
        //public abstract void CopyGeometry(IGeoObject ToCopyFrom) // ist schon abstract
        //{
        //    throw new NotImplementedException();
        //}
        /// <summary>
        /// Overrides <see cref="CADability.GeoObject.IGeoObjectImpl.GetExtent (Projection, ExtentPrecision)"/>
        /// </summary>
        /// <param name="projection"></param>
        /// <param name="extentPrecision"></param>
        /// <returns></returns>
        public override BoundingRect GetExtent(Projection projection, ExtentPrecision extentPrecision)
        {
            BoundingRect res = BoundingRect.EmptyBoundingRect;
            res.MinMax(projection.ProjectUnscaled(StartPoint));
            res.MinMax(projection.ProjectUnscaled(EndPoint));
            double[] extrema = TetraederHull.GetExtrema(projection.horizontalAxis);
            for (int i = 0; i < extrema.Length; ++i)
            {
                res.MinMax(projection.ProjectUnscaled(PointAt(extrema[i])));
            }
            extrema = TetraederHull.GetExtrema(projection.verticalAxis);
            for (int i = 0; i < extrema.Length; ++i)
            {
                res.MinMax(projection.ProjectUnscaled(PointAt(extrema[i])));
            }
            return res;
        }
        /// <summary>
        /// Overrides <see cref="CADability.GeoObject.IGeoObjectImpl.GetBoundingCube ()"/>
        /// </summary>
        /// <returns></returns>
        public override BoundingCube GetBoundingCube()
        {
            if (extent.IsEmpty)
            {
                extent.MinMax(StartPoint);
                extent.MinMax(EndPoint);
                double[] extr = TetraederHull.GetExtrema(GeoVector.XAxis);
                for (int i = 0; i < extr.Length; ++i)
                {
                    extent.MinMax(PointAt(extr[i]));
                }
                extr = TetraederHull.GetExtrema(GeoVector.YAxis);
                for (int i = 0; i < extr.Length; ++i)
                {
                    extent.MinMax(PointAt(extr[i]));
                }
                extr = TetraederHull.GetExtrema(GeoVector.ZAxis);
                for (int i = 0; i < extr.Length; ++i)
                {
                    extent.MinMax(PointAt(extr[i]));
                }
            }
            return extent;
        }
        /// <summary>
        /// Overrides <see cref="CADability.GeoObject.IGeoObjectImpl.GetShowProperties (IFrame)"/>
        /// </summary>
        /// <param name="Frame"></param>
        /// <returns></returns>
        public override IPropertyEntry GetShowProperties(IFrame Frame)
        {
            return new ShowPropertyGeneralCurve(this, Frame);
        }
        /// <summary>
        /// Overrides <see cref="CADability.GeoObject.IGeoObjectImpl.GetQuadTreeItem (Projection, ExtentPrecision)"/>
        /// </summary>
        /// <param name="projection"></param>
        /// <param name="extentPrecision"></param>
        /// <returns></returns>
        public override IQuadTreeInsertableZ GetQuadTreeItem(Projection projection, ExtentPrecision extentPrecision)
        {
            throw new Exception("The method or operation is not implemented.");
        }
        /// <summary>
        /// Overrides <see cref="CADability.GeoObject.IGeoObjectImpl.PrepareDisplayList (double)"/>
        /// </summary>
        /// <param name="precision"></param>
        public override void PrepareDisplayList(double precision)
        {
            // throw new Exception("The method or operation is not implemented.");
        }
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
            return TetraederHull.HitTest(cube);
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
            return TetraederHull.HitTest(projection.GetPickSpace(rect), onlyInside);
        }
        /// <summary>
        /// Overrides <see cref="CADability.GeoObject.IGeoObjectImpl.HitTest (Projection.PickArea, bool)"/>
        /// </summary>
        /// <param name="area"></param>
        /// <param name="onlyInside"></param>
        /// <returns></returns>
        public override bool HitTest(Projection.PickArea area, bool onlyInside)
        {
            return TetraederHull.HitTest(area, onlyInside);
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
            // gesucht ist die Z-Position beim Blick von fromHere für die Pickauswahl
            return this.TetraederHull.Position(fromHere, direction, precision);
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

        #region ICurve Members
        public virtual GeoPoint StartPoint
        {
            get
            {
                return PointAt(0.0);
            }
            set
            {
                throw new GeneralCurveException("cant set StartPoint of GeneralCurve");
            }
        }
        public virtual GeoPoint EndPoint
        {
            get
            {
                return PointAt(1.0);
            }
            set
            {
                throw new GeneralCurveException("cant set EndPoint of GeneralCurve");
            }
        }
        public virtual GeoVector StartDirection
        {
            get
            {
                return DirectionAt(0.0);
            }
        }
        public virtual GeoVector EndDirection
        {
            get
            {
                return DirectionAt(1.0);
            }
        }
        /// <summary>
        /// Implements <see cref="CADability.GeoObject.ICurve.DirectionAt (double)"/>
        /// </summary>
        /// <param name="Position"></param>
        /// <returns></returns>
        public abstract GeoVector DirectionAt(double Position);
        /// <summary>
        /// Implements <see cref="CADability.GeoObject.ICurve.PointAt (double)"/>
        /// </summary>
        /// <param name="Position"></param>
        /// <returns></returns>
        public abstract GeoPoint PointAt(double Position);
        /// <summary>
        /// Implements <see cref="CADability.GeoObject.ICurve.PositionOf (GeoPoint)"/>
        /// </summary>
        /// <param name="p"></param>
        /// <returns></returns>
        public virtual double PositionOf(GeoPoint p)
        {
            return this.TetraederHull.PositionOf(p);
        }
        /// <summary>
        /// Implements <see cref="CADability.GeoObject.ICurve.PositionOf (GeoPoint, double)"/>
        /// </summary>
        /// <param name="p"></param>
        /// <param name="prefer"></param>
        /// <returns></returns>
        public virtual double PositionOf(GeoPoint p, double prefer)
        {
            return PositionOf(p);
        }
        /// <summary>
        /// Implements <see cref="CADability.GeoObject.ICurve.PositionOf (GeoPoint, Plane)"/>
        /// </summary>
        /// <param name="p"></param>
        /// <param name="pl"></param>
        /// <returns></returns>
        public virtual double PositionOf(GeoPoint p, Plane pl)
        {
            // TODO:  Add GeneralCurve.PositionOf implementation
            return 0;
        }
        /// <summary>
        /// Implements <see cref="CADability.GeoObject.ICurve.Reverse ()"/>
        /// </summary>
        public abstract void Reverse();
        public virtual double Length
        {
            get
            {
                return this.TetraederHull.GetLength();
            }
        }
        /// <summary>
        /// Implements <see cref="CADability.GeoObject.ICurve.Split (double)"/>
        /// </summary>
        /// <param name="Position"></param>
        /// <returns></returns>
        public abstract ICurve[] Split(double Position);
        /// <summary>
        /// Implements <see cref="CADability.GeoObject.ICurve.Split (double, double)"/>
        /// </summary>
        /// <param name="Position1"></param>
        /// <param name="Position2"></param>
        /// <returns></returns>
        public virtual ICurve[] Split(double Position1, double Position2)
        {
            // TODO:  Add Split implementation
            return null;
        }
        /// <summary>
        /// Implements <see cref="CADability.GeoObject.ICurve.Trim (double, double)"/>
        /// </summary>
        /// <param name="StartPos"></param>
        /// <param name="EndPos"></param>
        public abstract void Trim(double StartPos, double EndPos);
        ICurve ICurve.Clone() { return (ICurve)this.Clone(); }
        /// <summary>
        /// Implements <see cref="CADability.GeoObject.ICurve.CloneModified (ModOp)"/>
        /// </summary>
        /// <param name="m"></param>
        /// <returns></returns>
        public virtual ICurve CloneModified(ModOp m)
        {
            IGeoObject clone = Clone();
            clone.Modify(m);
            return (ICurve)clone;
        }
        public virtual bool IsClosed
        {
            get
            {
                // TODO:  Add GeneralCurve.IsClosed getter implementation
                return false;
            }
        }
        public virtual bool IsSingular
        {
            get
            {
                return false;
            }
        }
        /// <summary>
        /// Implements <see cref="CADability.GeoObject.ICurve.GetPlanarState ()"/>
        /// </summary>
        /// <returns></returns>
        public virtual PlanarState GetPlanarState()
        {
            // TODO:  Add GeneralCurve.GetPlanarState implementation
            return PlanarState.NonPlanar;
        }
        /// <summary>
        /// Implements <see cref="CADability.GeoObject.ICurve.GetPlane ()"/>
        /// </summary>
        /// <returns></returns>
        public virtual Plane GetPlane()
        {
            // TODO:  Add GeneralCurve.GetPlane implementation
            return new Plane();
        }
        /// <summary>
        /// Implements <see cref="CADability.GeoObject.ICurve.IsInPlane (Plane)"/>
        /// </summary>
        /// <param name="p"></param>
        /// <returns></returns>
        public virtual bool IsInPlane(Plane p)
        {
            // TODO:  Add GeneralCurve.IsInPlane implementation
            return false;
        }
        /// <summary>
        /// Implements <see cref="CADability.GeoObject.ICurve.GetProjectedCurve (Plane)"/>
        /// </summary>
        /// <param name="p"></param>
        /// <returns></returns>
        public virtual CADability.Curve2D.ICurve2D GetProjectedCurve(Plane p)
        {
            return new ProjectedCurve(this, p);
        }
        public override string Description
        {
            get
            {
                return StringTable.GetString("GeneralCurve.Description");
            }
        }
        public virtual bool IsComposed
        {
            get { return false; }
        }
        public virtual ICurve[] SubCurves
        {
            get { return new ICurve[0]; }
        }
        /// <summary>
        /// Implements <see cref="CADability.GeoObject.ICurve.Approximate (bool, double)"/>
        /// </summary>
        /// <param name="linesOnly"></param>
        /// <param name="maxError"></param>
        /// <returns></returns>
        public virtual ICurve Approximate(bool linesOnly, double maxError)
        {
            if (linesOnly)
            {
                return this.TetraederHull.Approximate(linesOnly, maxError);
            }
            else
            {
                // hier könnte ArcLineFitting verwendung finden, müsste dann noch getestet werden
                ArcLineFitting3D alf = new ArcLineFitting3D(this, maxError, true, Math.Max(GetBasePoints().Length, 5));
                return alf.Approx;
            }
        }
        /// <summary>
        /// Implements <see cref="CADability.GeoObject.ICurve.TangentPosition (GeoVector)"/>
        /// </summary>
        /// <param name="direction"></param>
        /// <returns></returns>
        public virtual double[] TangentPosition(GeoVector direction)
        {
            return null;
        }
        /// <summary>
        /// Implements <see cref="CADability.GeoObject.ICurve.GetSelfIntersections ()"/>
        /// </summary>
        /// <returns></returns>
        public virtual double[] GetSelfIntersections()
        {
            return new double[0];
        }
        /// <summary>
        /// Implements <see cref="CADability.GeoObject.ICurve.SameGeometry (ICurve, double)"/>
        /// </summary>
        /// <param name="other"></param>
        /// <param name="precision"></param>
        /// <returns></returns>
        public virtual bool SameGeometry(ICurve other, double precision)
        {
            throw new NotImplementedException();
        }
        /// <summary>
        /// Implements <see cref="CADability.GeoObject.ICurve.PositionAtLength (double)"/>
        /// </summary>
        /// <param name="position"></param>
        /// <returns></returns>
        public virtual double PositionAtLength(double position)
        {
            return position / Length;
        }
        public virtual double ParameterToPosition(double parameter)
        {
            return parameter;
        }
        public virtual double PositionToParameter(double position)
        {
            return position;
        }
        bool ICurve.Extend(double atStart, double atEnd)
        {
            return false;
        }
        BoundingCube ICurve.GetExtent()
        {
            if (tetraederBase == null) MakeTetraederHull();
            // int xmin, xmax, ymin, ymax, zmin, zmax; // Indizes der extremen Vertexpunkte
            // noch nicht sauber implementiert: Gesucht werden müssen die lokalen
            // Minima und Maxima in x, y und z.
            BoundingCube res = BoundingCube.EmptyBoundingCube;
            for (int i = 0; i < tetraederBase.Length; ++i)
            {
                res.MinMax(tetraederBase[i]);
            }
            // so war es vorher, das scheint nicht in Ordnung zu sein:
            //for (int i = 0; i < tetraederVertex.Length; ++i)
            //{
            //    res.MinMax(tetraederVertex[i]);
            //}
            //res.MinMax(tetraederBase[0]);
            //res.MinMax(tetraederBase[tetraederBase.Length - 1]);
            return res;
        }
        bool ICurve.HitTest(BoundingCube cube)
        {
            return this.TetraederHull.HitTest(cube);
        }
        double[] ICurve.GetSavePositions()
        {
            return GetBasePoints();
        }
        double[] ICurve.GetExtrema(GeoVector direction)
        {
            return this.TetraederHull.GetExtrema(direction);
        }
        double[] ICurve.GetPlaneIntersection(Plane plane)
        {
            GeoPoint[] ips;
            GeoPoint2D[] uvOnPlane;
            double[] uOnCurve;
            TetraederHull.PlaneIntersection(new PlaneSurface(plane), out ips, out uvOnPlane, out uOnCurve);
            return uOnCurve;
        }
        /// <summary>
        /// Implements <see cref="CADability.GeoObject.ICurve.DistanceTo (GeoPoint)"/>
        /// </summary>
        /// <param name="p"></param>
        /// <returns></returns>
        public virtual double DistanceTo(GeoPoint p)
        {
            double pos = PositionOf(p);
            if (pos > 1e-6 && pos <= 1.0 - 1e-6)
            {   // at the start and enpoint, especially when the curve is an intersection curve with tangential surfaces, we also should consider start- end endpoint
                GeoPoint pCurve = PointAt(pos);
                return pCurve | p;
            }
            else
            {
                if (pos >= 0.0)
                {
                    GeoPoint pCurve = PointAt(pos);
                    return Math.Min(p | StartPoint, pCurve | p);
                }
                else if (pos < 1.0)
                {
                    GeoPoint pCurve = PointAt(pos);
                    return Math.Min(p | EndPoint, pCurve | p);
                }
                else return Math.Min(p | StartPoint, p | EndPoint);
            }
        }
        /// <summary>
        /// Implements <see cref="CADability.GeoObject.ICurve.TryPointDeriv2At (double, out GeoPoint, out GeoVector, out GeoVector)"/>
        /// </summary>
        /// <param name="position"></param>
        /// <param name="point"></param>
        /// <param name="deriv"></param>
        /// <param name="deriv2"></param>
        /// <returns></returns>
        public virtual bool TryPointDeriv2At(double position, out GeoPoint point, out GeoVector deriv, out GeoVector deriv2)
        {
            point = GeoPoint.Origin;
            deriv = deriv2 = GeoVector.NullVector;
            return false;
        }
        #endregion
        /// <summary>
        /// Constructor required by deserialization
        /// </summary>
        /// <param name="info">SerializationInfo</param>
        /// <param name="context">StreamingContext</param>
        protected GeneralCurve(SerializationInfo info, StreamingContext context)
            : base(info, context)
        {
            colorDef = (ColorDef)info.GetValue("ColorDef", typeof(ColorDef));
            lineWidth = LineWidth.Read("LineWidth", info, context);
            linePattern = LinePattern.Read("LinePattern", info, context);
        }
        /// <summary>
        /// Implements <see cref="ISerializable.GetObjectData"/>
        /// </summary>
        /// <param name="info">The <see cref="System.Runtime.Serialization.SerializationInfo"/> to populate with data.</param>
        /// <param name="context">The destination (<see cref="System.Runtime.Serialization.StreamingContext"/>) for this serialization.</param>
        public override void GetObjectData(SerializationInfo info, StreamingContext context)
        {
            base.GetObjectData(info, context);
            info.AddValue("ColorDef", colorDef);
            info.AddValue("LineWidth", lineWidth);
            info.AddValue("LinePattern", linePattern);
        }
        #region ILineWidth Members
        private LineWidth lineWidth;
        public LineWidth LineWidth
        {
            get
            {
                return lineWidth;
            }
            set
            {
                using (new ChangingAttribute(this, "LineWidth", lineWidth))
                {
                    lineWidth = value;
                }
            }
        }
        #endregion

        #region ILinePattern Members
        private LinePattern linePattern;
        public LinePattern LinePattern
        {
            get
            {
                return linePattern;
            }
            set
            {
                using (new ChangingAttribute(this, "LinePattern", linePattern))
                {
                    linePattern = value;
                }
            }
        }
        #endregion
    }
    /// <summary>
    /// Curve given by a surface and a 2d curve on this surface. Used mainly for edges.
    /// </summary>
    [Serializable()]
    internal class CurveOnSurface : GeneralCurve, ISerializable
    {
        private ICurve2D surfaceCurve;
        private ISurface surface;
        protected CurveOnSurface()
        {

        }
        public static CurveOnSurface Construct(ICurve2D surfaceCurve, ISurface surface)
        {
            CurveOnSurface res = new CurveOnSurface();
            res.surfaceCurve = surfaceCurve;
            res.surface = surface;
            return res;
        }
        public override IGeoObject Clone()
        {
            return Construct(surfaceCurve, surface);
        }

        public override void CopyGeometry(IGeoObject ToCopyFrom)
        {
            surface = (ToCopyFrom as CurveOnSurface).surface;
            surfaceCurve = (ToCopyFrom as CurveOnSurface).surfaceCurve;
        }

        public override GeoVector DirectionAt(double Position)
        {
            GeoVector2D d2d = surfaceCurve.DirectionAt(Position);
            GeoPoint2D p2d = surfaceCurve.PointAt(Position);
            GeoPoint location;
            GeoVector du, dv;
            surface.DerivationAt(p2d, out location, out du, out dv);
            return d2d.x * du + d2d.y * dv; // wie ist es mit der Länge?
        }

        public override void Modify(ModOp m)
        {   // it is not possible to modify this kind of curve
            // but when the surface has been modified, we have to recalculate the 3d curve
            surface = surface.GetModified(m);
            InvalidateSecondaryData();
        }

        public override GeoPoint PointAt(double Position)
        {
            return surface.PointAt(surfaceCurve.PointAt(Position));
        }

        public override void Reverse()
        {
            surfaceCurve = surfaceCurve.CloneReverse(true);
        }

        public override ICurve[] Split(double Position)
        {
            ICurve2D[] splitted = surfaceCurve.Split(Position);
            ICurve[] res = new ICurve[splitted.Length];
            for (int i = 0; i < splitted.Length; i++)
            {
                res[i] = Construct(splitted[i], surface);
            }
            return res;
        }

        public override void Trim(double StartPos, double EndPos)
        {
            surfaceCurve = surfaceCurve.Trim(StartPos, EndPos);
        }

        protected override double[] GetBasePoints()
        {
            if (surfaceCurve is GeneralCurve2D)
            {
                // was meist der Fall ist
                GeoPoint2D[] interpol;
                double[] interparam;
                (surfaceCurve as GeneralCurve2D).GetTriangulationPoints(out interpol, out interparam);
                return interparam;
            }
            else
            {   // Notlösung, untersuchen, wann das gebraucht wird
                return new double[] { 0.0, 0.1, 0.2, 0.3, 0.4, 0.5, 0.6, 0.7, 0.8, 0.9, 1.0 };
            }
        }
        /// <summary>
        /// Constructor required by deserialization
        /// </summary>
        /// <param name="info">SerializationInfo</param>
        /// <param name="context">StreamingContext</param>
        protected CurveOnSurface(SerializationInfo info, StreamingContext context)
            : base(info, context)
        {
            surface = (ISurface)info.GetValue("Surface", typeof(ISurface));
            surfaceCurve = (ICurve2D)info.GetValue("SurfaceCurve", typeof(ICurve2D));
        }
        /// <summary>
        /// Implements <see cref="ISerializable.GetObjectData"/>
        /// </summary>
        /// <param name="info">The <see cref="System.Runtime.Serialization.SerializationInfo"/> to populate with data.</param>
        /// <param name="context">The destination (<see cref="System.Runtime.Serialization.StreamingContext"/>) for this serialization.</param>
        public override void GetObjectData(SerializationInfo info, StreamingContext context)
        {
            base.GetObjectData(info, context);
            info.AddValue("Surface", surface);
            info.AddValue("SurfaceCurve", surfaceCurve);
        }
    }

    /// <summary>
    /// Diese Klasse hat den Zweck Methoden des ICurve Interfaces allgemein zu implementieren. Die Kurve wird von
    /// einer Tedraeder Hülle eingegrenzt und ermöglicht somit z.B. Schnitte und Extrempunkte zu bestimmen
    /// </summary>
    internal class TetraederHull
    {
        private ICurve theCurve;
        private GeoPoint[] tetraederBase; // Punkte auf der Kurve
        private double[] tetraederParams; // Parameter zu tetraederBase
        private GeoPoint[] tetraederVertex; // zu jedem tetraederBase (bis auf den letzten) gibt es zwei Punkte, so dass ein Tetraeder aufgespannt wird
        private void MakeTetraeder(double from, double to, List<GeoPoint> points, List<double> pars, List<GeoPoint> vertices, int deepth)
        {
            // deepth nur für Debugger!!!
            if (deepth > 5)
            {
            }
            if (to - from < 1e-8) return; // einfach überspringen
            GeoPoint p1 = theCurve.PointAt(from);
            GeoPoint p2 = theCurve.PointAt(to);
            if (Precision.IsEqual(p1, p2)) return; // überspringen, doppelter Punkt
            GeoVector v1 = theCurve.DirectionAt(from);
            GeoVector v2 = theCurve.DirectionAt(to);
            GeoVector d = p2 - p1;
            // Probleme machen NURBS, die eigentlich Polylines sind und die Eckpunkte genau die Stützpunkte sind
            // Da sind die Richtungen verschieden, aber es ist doch gerade. Deshalb die folgende Abfrage geändert
            // if (Precision.SameDirection(v1, v2, false) && Precision.SameDirection(v1, d, false))
            if (Precision.SameDirection(v2, d, false) || Precision.SameDirection(v1, d, false) || v1.IsNullVector() || v2.IsNullVector())
            {
                // es geht einfach nur geradeaus
                pars.Add(from);
                points.Add(p1);
                GeoPoint pm = new GeoPoint(p1, p2);
                vertices.Add(pm);
                vertices.Add(pm);
            }
            else
            {
                GeoVector v1x = d ^ v1;
                GeoVector v2x = d ^ v2;
                try
                {
                    Plane pl1 = new Plane(p1, v1x, v1); // Ebene am Statpunkt tangential zur Kurve
                    Plane pl2 = new Plane(p2, v2, v2x); // Ebene am Endpunkt tangential zur Kurve
                    Double cos = pl1.Normal * pl2.Normal;
                    GeoVector dbg = pl1.Normal ^ (-pl2.Normal);
                    if (cos < 0.7 && v1x.Length > 1e-2 && v2x.Length > 1e-2 && (to - from) > 0.005) // war vorher Math.Asb(cos), das liefert aber bei gegenläufigen Vekoren (z.B. -0.9) false, und das ist nicht richtig*
                                                                                                    // && (to-from)>0.005: maximal 200 , sonst ist was faul
                    {   // der Schnittwinkel der Ebene ist zu spitz, also aufteilen
                        // wenn v1x oder v2x zu kurz sind, dann stimmt die Richtung nicht
                        double m = (from + to) / 2.0;
                        MakeTetraeder(from, m, points, pars, vertices, deepth + 1);
                        MakeTetraeder(m, to, points, pars, vertices, deepth + 1);
#if DEBUG
                        Line dbgl1 = Line.Construct();
                        dbgl1.SetTwoPoints(p1, p2); // die sekante
                        Line dbgl2 = Line.Construct();
                        dbgl2.SetTwoPoints(p1, p1 + v1); // die Startrichtung
                        Line dbgl3 = Line.Construct();
                        dbgl3.SetTwoPoints(p2, p2 + v2); // die Endrichtung
                        Line dbgl4 = Line.Construct();
                        dbgl4.SetTwoPoints(p1, p1 + v1x); // die senkrechte
                        Line dbgl5 = Line.Construct();
                        dbgl5.SetTwoPoints(p2, p2 + v2x); // die senkrechte
                        DebuggerContainer dc = new DebuggerContainer();
                        dc.Add(dbgl1, 1);
                        dc.Add(dbgl2, 2);
                        dc.Add(dbgl3, 3);
                        dc.Add(dbgl4, 4);
                        dc.Add(dbgl5, 5);
#endif
                    }
                    else
                    {
                        Plane pl3 = new Plane(p1, d, v1); // Ebene durch die Verbindung, tangential am Startpunkt
                        Plane pl4 = new Plane(p2, d, v2); // Ebene durch die Verbindung, tangential am Endpunkt
                        GeoPoint ip;
                        GeoVector id;
                        if (pl1.Intersect(pl2, out ip, out id))
                        {
                            GeoPoint p3 = pl3.Intersect(ip, id);
                            GeoPoint p4 = pl4.Intersect(ip, id);
                            double d12 = (p1 | p2);
                            // if ((p3 | p4) > d12 || (p1 | p3) > d12 || (p1 | p4) > d12 || (p2 | p3) > d12 || (p2 | p4) > d12)
                            
                            //Unreachable code
                            /*
                            if (false)
                            {   // die Sehne p1->p2 muss die längste Seite des Tatraeders sein
                                double m = (from + to) / 2.0;
                                MakeTetraeder(from, m, points, pars, vertices, deepth + 1);
                                MakeTetraeder(m, to, points, pars, vertices, deepth + 1);
                            }
                            else
                            {
                            */

                            // Orientierung überprüfen
                            Plane plt = new Plane(p1, p3, p2); // geht ggf. nach PlaneException
                            pars.Add(from);
                            points.Add(p1);
                            GeoPoint pm = new GeoPoint(p1, p2, p3, p4);
                            GeoPoint p11 = plt.ToLocal(pm);
                            if (p11.z > 0) // war vorher anders so sind die Ebenen alle so orientiert, dass sie nach außen zeigen
                            // aber die Orientierung ist immer noch nicht gut
                            // if ((((p2 - p1) ^ (p3 - p1)) * (p4 - p1)) < 0)
                            {
                                vertices.Add(p4);
                                vertices.Add(p3);
                            }
                            else
                            {
                                vertices.Add(p3);
                                vertices.Add(p4);
                            }
                            //}
                        }
                        else
                        {   // es geht völlig gerade weiter
                            pars.Add(from);
                            points.Add(p1);
                            GeoPoint pm = new GeoPoint(p1, p2);
                            vertices.Add(pm);
                            vertices.Add(pm);
                        }
                    }
                }
                catch (PlaneException)
                {
#if DEBUG
                    Polyline res = Polyline.Construct();
                    int dbglen = 1000;
                    GeoPoint[] vtx = new GeoPoint[dbglen];
                    for (int i = 0; i < dbglen; i++)
                    {
                        vtx[i] = theCurve.PointAt(i * 1.0 / dbglen);
                    }
                    res.SetPoints(vtx, false);
#endif
                    pars.Add(from);
                    points.Add(p1);
                    GeoPoint pm = new GeoPoint(p1, p2);
                    vertices.Add(pm);
                    vertices.Add(pm);
                }
            }
        }
        protected void MakeTetraederHull()
        {
            // 1. Implementierung: Die Dreiecke werden richtig gebildet
            // noch keine Aufteilung, wenn "Wendepunkt" (2. Ableitung der Kurve?)
            if (theCurve.IsSingular)
            {
                tetraederBase = new GeoPoint[] { theCurve.StartPoint };
                tetraederParams = new double[] { 0.0 };
                tetraederVertex = new GeoPoint[0];
                return;
            }
            double[] pos = theCurve.GetSavePositions();
            // DEBUG!!!
            //pos = new double[25];
            //for (int i = 0; i < 25; ++i)
            //{
            //    pos[i] = i / 24.0;
            //}
            List<GeoPoint> points = new List<GeoPoint>();
            List<double> pars = new List<double>();
            List<GeoPoint> vertices = new List<GeoPoint>();
            for (int i = 0; i < pos.Length - 1; ++i)
            {
                MakeTetraeder(pos[i], pos[i + 1], points, pars, vertices, 0);
            }
            points.Add(theCurve.PointAt(pos[pos.Length - 1]));
            pars.Add(pos[pos.Length - 1]);
            tetraederBase = points.ToArray();
            tetraederParams = pars.ToArray();
            tetraederVertex = vertices.ToArray();

        }
#if DEBUG
        static int maxCount = 0;
#endif
        public TetraederHull(ICurve theCurve)
        {
            this.theCurve = theCurve;
            MakeTetraederHull();
#if DEBUG
            if (tetraederVertex.Length > maxCount)
            {
                maxCount = tetraederVertex.Length;
                //System.Diagnostics.Trace.WriteLine("TetraederHull, count: " + maxCount.ToString());
            }
#endif
        }
        public double[] GetExtrema(GeoVector direction)
        {   // kommt ohne die Hülle aus, nur die Stützpunkte genügen
            List<double> res = new List<double>();
            double lastscalar = theCurve.DirectionAt(tetraederParams[0]) * direction;
            for (int i = 1; i < tetraederBase.Length; ++i)
            {
                double d2 = theCurve.DirectionAt(tetraederParams[i]) * direction;
                if (double.IsNaN(d2) || double.IsNaN(lastscalar)) continue;
                if (Math.Sign(lastscalar) != Math.Sign(d2))
                {
                    if (lastscalar == 0.0) res.Add(tetraederParams[i - 1]);
                    else res.Add(BisectPerpPos(lastscalar, d2, tetraederParams[i - 1], tetraederParams[i], direction));
                }
                lastscalar = d2;
            }
            return res.ToArray();
        }
        private double BisectPerpPos(double s1, double s2, double p1, double p2, GeoVector direction)
        {   // Bestimme die Stelle an der die Richtung der Kurve senkrecht zu direction ist
            // hier könnte man vom Mittelpunkt ausgehend das Newtonverfahren nehmen und nur wenn man das Intervall
            // p1,p2 verlässt zur Bisektion zurückkehren
            // hier werden ca 15-16 s verbraten, mal sehen ob es mit einer besseren Aufteilung schneller geht
            int dbgn = 0;
            while (p2 - p1 > 1e-8)
            {
                ++dbgn;
                // double v = Math.Abs(s1 / (s2 - s1));
                // v = v - (v - 0.5) / 4.0; // noch etwas zur Mitte hin korrigieren
                // double p0 = (1-v) * p1 + v * p2;
                double p0 = (p1 + p2) / 2.0;
                double s0 = theCurve.DirectionAt(p0) * direction;
                if (Math.Sign(s0) == Math.Sign(s1))
                {
                    p1 = p0;
                    s1 = s0;
                }
                else if (Math.Sign(s0) == Math.Sign(s2))
                {
                    p2 = p0;
                    s2 = s0;
                }
                else return p0;
            }
            return (p1 + p2) / 2;
        }
        internal static void GetTetraederPoints(GeoPoint p1, GeoPoint p2, GeoVector v1, GeoVector v2, out GeoPoint tv1, out GeoPoint tv2)
        {
            GeoVector d = p2 - p1;
            if (Precision.SameDirection(v1, d, false) || Precision.SameDirection(v2, d, false) || d.IsNullVector())
            {
                GeoPoint pm = new GeoPoint(p1, p2);
                tv1 = pm;
                tv2 = pm;
                return;
            }
            GeoVector v1x = d ^ v1;
            GeoVector v2x = d ^ v2;
            if (v1x.IsNullVector() || v2x.IsNullVector())
            {
                GeoPoint pm = new GeoPoint(p1, p2);
                tv1 = pm;
                tv2 = pm;
                return;
            }
            try
            {
                Plane pl1 = new Plane(p1, v1x, v1); // Ebene am Startpunkt tangential zur Kurve
                Plane pl2 = new Plane(p2, v2, v2x); // Ebene am Endpunkt tangential zur Kurve
                Plane pl3 = new Plane(p1, d, v1); // Ebene durch die Verbindung, tangential am Startpunkt
                Plane pl4 = new Plane(p1, d, v2); // Ebene durch die Verbindung, tangential am Endpunkt
                GeoPoint ip;
                GeoVector id;
                if (pl1.Intersect(pl2, out ip, out id))
                {
                    GeoPoint p3 = pl3.Intersect(ip, id);
                    GeoPoint p4 = pl4.Intersect(ip, id);
                    // Orientierung überprüfen
                    GeoPoint pm = new GeoPoint(p1, p2, p3, p4);
                    Plane plt = new Plane(p1, p3, p2);
                    GeoPoint p11 = plt.ToLocal(pm);
                    double dist = (p1 | p2) * 2;
                    if (dist > (p1 | p3) && dist > (p1 | p4) && dist > (p2 | p3) && dist > (p2 | p4))
                    {
                        if (p11.z > 0) // war vorher anders so sind die Ebenen alle so orientiert, dass sie nach außen zeigen
                                       // if ((((p2 - p1) ^ (p3 - p1)) * (p4 - p1)) < 0)
                        {
                            tv1 = p4;
                            tv2 = p3;
                        }
                        else
                        {
                            tv1 = p3;
                            tv2 = p4;
                        }
                    }
                    else
                    {
                        pm = new GeoPoint(p1, p2);
                        tv1 = pm;
                        tv2 = pm;
                    }
                }
                else
                {   // es geht völlig gerade weiter
                    GeoPoint pm = new GeoPoint(p1, p2);
                    tv1 = pm;
                    tv2 = pm;
                }
            }
            catch (PlaneException)
            {
                GeoPoint pm = new GeoPoint(p1, p2);
                tv1 = pm;
                tv2 = pm;
            }
        }
        private bool SplitTetraeder(GeoPoint p1, GeoPoint p2, double par1, double par2, out GeoPoint pm, out double parm, out GeoPoint tv1, out GeoPoint tv2, out GeoPoint tv3, out GeoPoint tv4)
        {
            // in der Parametermitte aufteilen
            parm = (par1 + par2) / 2.0;
            pm = theCurve.PointAt(parm);
            GeoVector v1 = theCurve.DirectionAt(par1);
            GeoVector v2 = theCurve.DirectionAt(par2);
            if (Precision.SameDirection(v1, v2, false))
            {
                tv1 = tv2 = tv3 = tv4 = pm;
                return false;
            }
            else
            {
                GeoVector vm = theCurve.DirectionAt(parm);
                GetTetraederPoints(p1, pm, v1, vm, out tv1, out tv2);
                GetTetraederPoints(pm, p2, vm, v2, out tv3, out tv4);
                return true;
            }
        }
        public static bool SplitTetraeder(ICurve curve, GeoPoint p1, GeoPoint p2, double par1, double par2, out GeoPoint pm, out double parm, out GeoPoint tv1, out GeoPoint tv2, out GeoPoint tv3, out GeoPoint tv4)
        {
            // in der Parametermitte aufteilen
            parm = (par1 + par2) / 2.0;
            pm = curve.PointAt(parm);
            GeoVector v1 = curve.DirectionAt(par1);
            GeoVector v2 = curve.DirectionAt(par2);
            if (Precision.SameDirection(v1, v2, false))
            {
                tv1 = tv2 = tv3 = tv4 = pm;
                return false;
            }
            else
            {
                GeoVector vm = curve.DirectionAt(parm);
                GetTetraederPoints(p1, pm, v1, vm, out tv1, out tv2);
                GetTetraederPoints(pm, p2, vm, v2, out tv3, out tv4);
                return true;
            }
        }
        public bool HitTest(Projection.PickArea area, bool onlyInside)
        {
            if (onlyInside)
            {
                for (int i = 0; i < tetraederBase.Length; ++i)
                {
                    if (!BoundingCube.UnitBoundingCube.Contains(area.ToUnitBox * tetraederBase[i])) return false;
                }
                for (int i = 0; i < tetraederBase.Length - 1; ++i)
                {
                    if (!BoundingCube.UnitBoundingCube.Contains(area.ToUnitBox * tetraederVertex[2 * i]) ||
                        !BoundingCube.UnitBoundingCube.Contains(area.ToUnitBox * tetraederVertex[2 * i + 1]))
                    {   // wenn nicht das ganze Tetraeder innerhalb ist, dann genauer prüfen
                        if (IsPartOutside(area, tetraederBase[i], tetraederBase[i + 1], tetraederParams[i], tetraederParams[i + 1])) return false;
                    }
                }
                return true;
            }
            else
            {
                for (int i = 0; i < tetraederBase.Length; ++i)
                {
                    if (BoundingCube.UnitBoundingCube.Contains(area.ToUnitBox * tetraederBase[i])) return true;
                }
                for (int i = 0; i < tetraederBase.Length - 1; ++i)
                {
                    if (IsLine(i))
                    {
                        GeoPoint p1 = area.ToUnitBox * tetraederBase[i];
                        GeoPoint p2 = area.ToUnitBox * tetraederBase[i + 1];
                        if (BoundingCube.UnitBoundingCube.Interferes(ref p1, ref p2)) return true;
                    }
                    else if (BoundingCube.UnitBoundingCube.Interferes(area.ToUnitBox * tetraederBase[i], area.ToUnitBox * tetraederBase[i + 1], area.ToUnitBox * tetraederVertex[2 * i], area.ToUnitBox * tetraederVertex[2 * i + 1]))
                    {   // die Basispunkte sind außerhalb, aber das Tetraeder berührt die area
                        if (IsPartInside(area, tetraederBase[i], tetraederBase[i + 1], tetraederParams[i], tetraederParams[i + 1])) return true;
                    }
                }
                return false;
            }
        }
        public bool HitTest(BoundingCube cube)
        {
            for (int i = 0; i < tetraederBase.Length; ++i)
            {
                if (cube.Contains(tetraederBase[i])) return true;
            }
            for (int i = 0; i < tetraederBase.Length - 1; ++i)
            {
                if (IsLine(i))
                {
                    GeoPoint p1 = tetraederBase[i];
                    GeoPoint p2 = tetraederBase[i + 1];
                    if (cube.Interferes(ref p1, ref p2)) return true;
                }
                else if (cube.Interferes(tetraederBase[i], tetraederBase[i + 1], tetraederVertex[2 * i], tetraederVertex[2 * i + 1]))
                {   // die Basispunkte sind außerhalb, aber das Tetraeder berührt die area
                    if (IsPartInside(cube, tetraederBase[i], tetraederBase[i + 1], tetraederParams[i], tetraederParams[i + 1])) return true;
                }
            }
            return false;
        }

        public bool IsLine(int i)
        {
            return (Precision.SameDirection(theCurve.DirectionAt(tetraederParams[i]), theCurve.DirectionAt(tetraederParams[i + 1]), false));
        }
        public bool IsTriangle(int i)
        {   // sollte bereits beim Erzeugen ebenso wie IsLine gespeichert werden
            return tetraederVertex[2 * i] == tetraederVertex[2 * i + 1];
        }
        public bool IsPlanar
        {
            get
            {
                double maxDist;
                bool islinear;
                Plane pln = Plane.FromPoints(tetraederBase, out maxDist, out islinear);
                if (maxDist < Precision.eps)
                {
                    for (int i = 0; i < tetraederBase.Length - 1; ++i)
                    {
                        if (!IsTriangle(i)) return false;
                    }
                    return true;
                }
                return false;
            }
        }
        public Plane Plane
        {
            get
            {
                double maxDist;
                bool islinear;
                Plane pln = Plane.FromPoints(tetraederBase, out maxDist, out islinear);
                return pln;
            }
        }
        public GeoPoint[] TetraederBase
        {
            get
            {
                return tetraederBase;
            }
        }
        public double[] TetraederParams
        {
            get
            {
                return tetraederParams;
            }
        }
        public GeoPoint[] TetraederVertex
        {
            get
            {
                return tetraederVertex;
            }
        }
        private OctTree<CurveTetraeder> octTree;
        private OctTree<CurveTetraeder> OctTree
        {
            get
            {
                if (octTree == null)
                {
                    BoundingCube ext = BoundingCube.EmptyBoundingCube;
                    for (int i = 0; i < tetraederBase.Length - 1; ++i)
                    {
                        ext.MinMax(tetraederBase[i]);
                        ext.MinMax(tetraederVertex[2 * i]);
                        ext.MinMax(tetraederVertex[2 * i + 1]);
                    }
                    ext.MinMax(tetraederBase[tetraederBase.Length - 1]);
                    double d = ext.Size;
                    ext.Expand(d * 1e-6); // to avoid an octtree, which is flat in one dimension
                    ext.Modify(new GeoVector(d * 0.5e-6, d * 0.5e-6, d * 0.5e-6)); // to avoid flat tetraeders lying exactely inbetween cubes of the octtree
                    octTree = new OctTree<CurveTetraeder>(ext, ext.Size * 1e-6);
                    lock (octTree)
                    {
                        for (int i = 0; i < tetraederBase.Length - 1; ++i)
                        {
                            octTree.AddObject(new CurveTetraeder(tetraederBase[i], tetraederBase[i + 1], tetraederVertex[2 * i], tetraederVertex[2 * i + 1], tetraederParams[i], tetraederParams[i + 1], this));
                        }
                    }
                }
                return octTree;
            }
        }

        private class CurveTetraeder : IOctTreeInsertable
        {   // Beschreibung eines Tetraeders mit zugehörigem Kurvenabschnitt
            public GeoPoint t1, t2, t3, t4;
            public double umin, umax;
            private TetraederHull owner;
            public CurveTetraeder(TetraederHull owner)
            {
                this.owner = owner;
            }
            public CurveTetraeder(GeoPoint t1, GeoPoint t2, GeoPoint t3, GeoPoint t4, double umin, double umax, TetraederHull owner)
            {
                this.t1 = t1;
                this.t2 = t2;
                this.t3 = t3;
                this.t4 = t4;
                this.umin = umin;
                this.umax = umax;
                this.owner = owner;
            }
            #region IOctTreeInsertable Members

            BoundingCube IOctTreeInsertable.GetExtent(double precision)
            {
                if (IsFlat)
                {
                    BoundingCube res = new BoundingCube(t1, t2, t3);
                    res.Expand(precision);
                    return res;
                }
                return new BoundingCube(t1, t2, t3, t4);
            }

            bool IOctTreeInsertable.HitTest(ref BoundingCube cube, double precision)
            {
                if (IsFlat)
                {
                    BoundingCube res = new BoundingCube(t1, t2, t3);
                    res.Expand(precision);
                    return cube.Interferes(res);
                }
                return cube.Interferes(t1, t2, t3, t4);
            }

            bool IOctTreeInsertable.HitTest(Projection projection, BoundingRect rect, bool onlyInside)
            {
                throw new Exception("The method or operation is not implemented.");
            }

            bool IOctTreeInsertable.HitTest(Projection.PickArea area, bool onlyInside)
            {
                throw new Exception("The method or operation is not implemented.");
            }

            double IOctTreeInsertable.Position(GeoPoint fromHere, GeoVector direction, double precision)
            {
                throw new Exception("The method or operation is not implemented.");
            }

            #endregion
            public bool IsFlat
            {
                get
                {
                    return t3 == t4;
                }
            }
            public bool IsLinear
            {
                get
                {
                    return (t3 == t4) && (t3 == new GeoPoint(t1, t2));
                }
            }
            private ModOp toUnit;
            public ModOp ToUnit
            {
                get
                {
                    if (toUnit.IsNull)
                    {
                        if (IsFlat)
                        {   // flacher Fall, also nur ein Dreieck
                            // z in der Abbildung wird hier ignoriert
                            if (IsLinear)
                            {
                                // mach das Sinn: die Linie auf (0,0)->(1,0) abbilden
                                toUnit = ModOp.Fit(new GeoPoint[] { t1, t4 }, new GeoPoint[] { GeoPoint.Origin, new GeoPoint(1.0, 0.0, 0.0) }, true);
                            }
                            else
                            {
                                GeoVector v1 = t2 - t1;
                                GeoVector v2 = t4 - t1;
                                GeoVector v3 = v1 ^ v2;
                                Matrix m = (Matrix)DenseMatrix.OfRowArrays(new double[][] { v1, v2, v3 }).Inverse();
                                if (m != null)
                                {
                                    toUnit.SetData(m, t1);
                                }
                                else
                                {
                                    toUnit = ModOp.Fit(new GeoPoint[] { t1, t4 }, new GeoPoint[] { GeoPoint.Origin, new GeoPoint(1.0, 0.0, 0.0) }, true);
                                }
                            }
                        }
                        else
                        {
                            GeoVector v1 = t2 - t1;
                            GeoVector v2 = t3 - t1;
                            GeoVector v3 = t4 - t1;
                            Matrix m = (Matrix)DenseMatrix.OfRowArrays(new double[][] { v1, v2, v3 }).Inverse();
                            GeoPoint trans = m * t1;
                            toUnit.SetData(m, new GeoPoint(-trans.x, -trans.y, -trans.z));
                        }
                    }
                    return toUnit;
                }
            }
            public bool Contains(GeoPoint p)
            {
                if (IsFlat)
                {
                    GeoPoint p1 = ToUnit * p;
                    return ((p1.x + p1.y <= 1.0) && (p1.x >= 0.0) && (p1.x <= 1.0) && (p1.y >= 0.0) && (p1.y <= 1.0) && (Math.Abs(p1.z) < 1e-6));
                }
                else
                {
                    GeoPoint p1 = ToUnit * p;
                    return ((p1.x + p1.y + p1.z <= 1.0) && (p1.x >= 0.0) && (p1.x <= 1.0) && (p1.y >= 0.0) && (p1.y <= 1.0) && (p1.z >= 0.0) && (p1.z <= 1.0));
                }
            }
            private bool TriangleIntersect(GeoPoint start, GeoPoint end)
            {   // schneidet die Linie mit der xy Ebene und liefert true, wenn der Schnittpunkt auf der linie liegt und im Einheitsdreieck
                GeoVector dir = end - start;
                if (Math.Abs(dir.z) < 1e-12) return false;
                double l = -start.z / dir.z;
                if (l < 0.0 || l > 1.0) return false;
                GeoPoint p = start + l * dir;
                return (p.x + p.y < 1.0) && (p.x >= 0.0) && (p.x <= 1.0) && (p.y >= 0.0) && (p.y <= 1.0);
            }
            public bool Interferes(CurveTetraeder t)
            {   // überschneiden sich zwei Tetraeder
                // noch nicht berücksichtig: Tetraeder ist nur eine Linie
                if (IsLinear)
                {
                    if (t.IsLinear)
                    {   // zwei Linien
                        double par1, par2;
                        double d = Geometry.DistLLWrongPar2(t1, t2 - t1, t.t1, t.t2 - t.t1, out par1, out par2);
                        return (d < Precision.eps && par1 >= 0.0 && par1 <= 1.0 && par2 >= 0.0 && par2 <= 1.0);
                    }
                    else
                    {
                        return t.Interferes(this); // da t nicht Linear werden wir nicht endlos rekursiv
                    }
                }
                if (IsFlat && t.IsFlat)
                {   // beide sind flach, aber nicht notwendig in einer Ebene
                    GeoPoint p1 = ToUnit * t.t1;
                    if ((p1.x + p1.y + p1.z <= 1.0) && (p1.x >= 0.0) && (p1.x <= 1.0) && (p1.y >= 0.0) && (p1.y <= 1.0) && (Math.Abs(p1.z) < 1e-6)) return true;
                    GeoPoint p2 = ToUnit * t.t2;
                    if ((p2.x + p2.y + p2.z <= 1.0) && (p2.x >= 0.0) && (p2.x <= 1.0) && (p2.y >= 0.0) && (p2.y <= 1.0) && (Math.Abs(p2.z) < 1e-6)) return true;
                    GeoPoint p3 = ToUnit * t.t3;
                    if ((p3.x + p3.y + p3.z <= 1.0) && (p3.x >= 0.0) && (p3.x <= 1.0) && (p3.y >= 0.0) && (p3.y <= 1.0) && (Math.Abs(p3.z) < 1e-6)) return true;
                    if (Math.Abs(p1.z) < 1e-6 && Math.Abs(p2.z) < 1e-6 && Math.Abs(p3.z) < 1e-6)
                    {   // in einer Ebene
                        // muss noch implementiert werden
                    }
                    else
                    {
                        if (TriangleIntersect(p1, p2)) return true;
                        if (TriangleIntersect(p1, p3)) return true;
                        if (TriangleIntersect(p2, p3)) return true;
                        // und mit vertauschten Rollen:
                        p1 = t.ToUnit * t1;
                        p2 = t.ToUnit * t2;
                        p3 = t.ToUnit * t3;
                        if (TriangleIntersect(p1, p2)) return true;
                        if (TriangleIntersect(p1, p3)) return true;
                        if (TriangleIntersect(p2, p3)) return true;
                        return false;
                    }
                }
                else if (IsFlat)
                {
                    // 1. einer der Dreieckspunkte im Tetraeder
                    if (t.Contains(t1)) return true;
                    if (t.Contains(t2)) return true;
                    if (t.Contains(t3)) return true;
                    // 2. eine der Tetraederlinien schneidet das Dreieck
                    GeoPoint p1 = ToUnit * t.t1;
                    GeoPoint p2 = ToUnit * t.t2;
                    GeoPoint p3 = ToUnit * t.t3;
                    GeoPoint p4 = ToUnit * t.t4;
                    if (TriangleIntersect(p1, p2)) return true;
                    if (TriangleIntersect(p1, p3)) return true;
                    if (TriangleIntersect(p1, p4)) return true;
                    if (TriangleIntersect(p2, p3)) return true;
                    if (TriangleIntersect(p2, p4)) return true;
                    if (TriangleIntersect(p3, p4)) return true;
                    return false;
                }
                else if (t.IsFlat)
                {
                    // Rollen vertauschen
                    // 1. einer der Dreieckspunkte im Tetraeder
                    if (Contains(t.t1)) return true;
                    if (Contains(t.t2)) return true;
                    if (Contains(t.t3)) return true;
                    // 2. eine der Tetraederlinien schneidet das Dreieck
                    GeoPoint p1 = t.ToUnit * t1;
                    GeoPoint p2 = t.ToUnit * t2;
                    GeoPoint p3 = t.ToUnit * t3;
                    GeoPoint p4 = t.ToUnit * t4;
                    if (t.TriangleIntersect(p1, p2)) return true;
                    if (t.TriangleIntersect(p1, p3)) return true;
                    if (t.TriangleIntersect(p1, p4)) return true;
                    if (t.TriangleIntersect(p2, p3)) return true;
                    if (t.TriangleIntersect(p2, p4)) return true;
                    if (t.TriangleIntersect(p3, p4)) return true;
                    return false;
                }
                else
                {
                    GeoPoint p1 = ToUnit * t.t1;
                    if ((p1.x + p1.y + p1.z <= 1.0) && (p1.x >= 0.0) && (p1.x <= 1.0) && (p1.y >= 0.0) && (p1.y <= 1.0) && (p1.z >= 0.0) && (p1.z <= 1.0)) return true;
                    GeoPoint p2 = ToUnit * t.t2;
                    if ((p2.x + p2.y + p2.z <= 1.0) && (p2.x >= 0.0) && (p2.x <= 1.0) && (p2.y >= 0.0) && (p2.y <= 1.0) && (p2.z >= 0.0) && (p2.z <= 1.0)) return true;
                    GeoPoint p3 = ToUnit * t.t3;
                    if ((p3.x + p3.y + p3.z <= 1.0) && (p3.x >= 0.0) && (p3.x <= 1.0) && (p3.y >= 0.0) && (p3.y <= 1.0) && (p3.z >= 0.0) && (p3.z <= 1.0)) return true;
                    GeoPoint p4 = ToUnit * t.t4;
                    if ((p4.x + p4.y + p4.z <= 1.0) && (p4.x >= 0.0) && (p4.x <= 1.0) && (p4.y >= 0.0) && (p4.y <= 1.0) && (p4.z >= 0.0) && (p4.z <= 1.0)) return true;

#if DEBUG
                    //DebuggerContainer dc = new DebuggerContainer();
                    //dc.Add(Make3D.MakeTetraeder(p1, p2, p3, p4));
                    //dc.Add(BoundingCube.UnitBoundingCube.AsBox);
#endif
                    if (!BoundingCube.UnitBoundingCube.Interferes(p1, p2, p3, p4)) return false;
                    // jetzt noch schwieriger Fall: kein Punkt innerhalb, aber auch nicht außerhalb des cubes
                    // eine der 6 Kanten muss schneiden. Wenn man mit dem UnitBoundingCube trimmt, dann muss ein Punkt
                    // durch eine der 3 Standartseiten-Dreieck gehen, wenn es überschneidung gibt
                    if (t.Contains(t1)) return true; // der Fall ganz enthalten
                    GeoPoint start = p1, end = p2;
                    if (BoundingCube.UnitBoundingCube.ClipLine(ref start, ref end))
                    {
                        if ((start.x + start.y + start.z <= 1.0) && (start.x >= 0.0) && (start.x <= 1.0) && (start.y >= 0.0) && (start.y <= 1.0) && (start.z >= 0.0) && (start.z <= 1.0)) return true;
                        if ((end.x + end.y + end.z <= 1.0) && (end.x >= 0.0) && (end.x <= 1.0) && (end.y >= 0.0) && (end.y <= 1.0) && (end.z >= 0.0) && (end.z <= 1.0)) return true;
                    }
                    start = p1; end = p3;
                    if (BoundingCube.UnitBoundingCube.ClipLine(ref start, ref end))
                    {
                        if ((start.x + start.y + start.z <= 1.0) && (start.x >= 0.0) && (start.x <= 1.0) && (start.y >= 0.0) && (start.y <= 1.0) && (start.z >= 0.0) && (start.z <= 1.0)) return true;
                        if ((end.x + end.y + end.z <= 1.0) && (end.x >= 0.0) && (end.x <= 1.0) && (end.y >= 0.0) && (end.y <= 1.0) && (end.z >= 0.0) && (end.z <= 1.0)) return true;
                    }
                    start = p1; end = p4;
                    if (BoundingCube.UnitBoundingCube.ClipLine(ref start, ref end))
                    {
                        if ((start.x + start.y + start.z <= 1.0) && (start.x >= 0.0) && (start.x <= 1.0) && (start.y >= 0.0) && (start.y <= 1.0) && (start.z >= 0.0) && (start.z <= 1.0)) return true;
                        if ((end.x + end.y + end.z <= 1.0) && (end.x >= 0.0) && (end.x <= 1.0) && (end.y >= 0.0) && (end.y <= 1.0) && (end.z >= 0.0) && (end.z <= 1.0)) return true;
                    }
                    start = p2; end = p3;
                    if (BoundingCube.UnitBoundingCube.ClipLine(ref start, ref end))
                    {
                        if ((start.x + start.y + start.z <= 1.0) && (start.x >= 0.0) && (start.x <= 1.0) && (start.y >= 0.0) && (start.y <= 1.0) && (start.z >= 0.0) && (start.z <= 1.0)) return true;
                        if ((end.x + end.y + end.z <= 1.0) && (end.x >= 0.0) && (end.x <= 1.0) && (end.y >= 0.0) && (end.y <= 1.0) && (end.z >= 0.0) && (end.z <= 1.0)) return true;
                    }
                    start = p2; end = p4;
                    if (BoundingCube.UnitBoundingCube.ClipLine(ref start, ref end))
                    {
                        if ((start.x + start.y + start.z <= 1.0) && (start.x >= 0.0) && (start.x <= 1.0) && (start.y >= 0.0) && (start.y <= 1.0) && (start.z >= 0.0) && (start.z <= 1.0)) return true;
                        if ((end.x + end.y + end.z <= 1.0) && (end.x >= 0.0) && (end.x <= 1.0) && (end.y >= 0.0) && (end.y <= 1.0) && (end.z >= 0.0) && (end.z <= 1.0)) return true;
                    }
                    start = p3; end = p4;
                    if (BoundingCube.UnitBoundingCube.ClipLine(ref start, ref end))
                    {
                        if ((start.x + start.y + start.z <= 1.0) && (start.x >= 0.0) && (start.x <= 1.0) && (start.y >= 0.0) && (start.y <= 1.0) && (start.z >= 0.0) && (start.z <= 1.0)) return true;
                        if ((end.x + end.y + end.z <= 1.0) && (end.x >= 0.0) && (end.x <= 1.0) && (end.y >= 0.0) && (end.y <= 1.0) && (end.z >= 0.0) && (end.z <= 1.0)) return true;
                    }
                    // genügt leider immer noch nicht, man muss auch noch andersrum probieren
                    p1 = t.ToUnit * t1;
                    p2 = t.ToUnit * t2;
                    p3 = t.ToUnit * t3;
                    p4 = t.ToUnit * t4;
                    start = p1; end = p2;
                    if (BoundingCube.UnitBoundingCube.ClipLine(ref start, ref end))
                    {
                        if ((start.x + start.y + start.z <= 1.0) && (start.x >= 0.0) && (start.x <= 1.0) && (start.y >= 0.0) && (start.y <= 1.0) && (start.z >= 0.0) && (start.z <= 1.0)) return true;
                        if ((end.x + end.y + end.z <= 1.0) && (end.x >= 0.0) && (end.x <= 1.0) && (end.y >= 0.0) && (end.y <= 1.0) && (end.z >= 0.0) && (end.z <= 1.0)) return true;
                    }
                    start = p1; end = p3;
                    if (BoundingCube.UnitBoundingCube.ClipLine(ref start, ref end))
                    {
                        if ((start.x + start.y + start.z <= 1.0) && (start.x >= 0.0) && (start.x <= 1.0) && (start.y >= 0.0) && (start.y <= 1.0) && (start.z >= 0.0) && (start.z <= 1.0)) return true;
                        if ((end.x + end.y + end.z <= 1.0) && (end.x >= 0.0) && (end.x <= 1.0) && (end.y >= 0.0) && (end.y <= 1.0) && (end.z >= 0.0) && (end.z <= 1.0)) return true;
                    }
                    start = p1; end = p4;
                    if (BoundingCube.UnitBoundingCube.ClipLine(ref start, ref end))
                    {
                        if ((start.x + start.y + start.z <= 1.0) && (start.x >= 0.0) && (start.x <= 1.0) && (start.y >= 0.0) && (start.y <= 1.0) && (start.z >= 0.0) && (start.z <= 1.0)) return true;
                        if ((end.x + end.y + end.z <= 1.0) && (end.x >= 0.0) && (end.x <= 1.0) && (end.y >= 0.0) && (end.y <= 1.0) && (end.z >= 0.0) && (end.z <= 1.0)) return true;
                    }
                    start = p2; end = p3;
                    if (BoundingCube.UnitBoundingCube.ClipLine(ref start, ref end))
                    {
                        if ((start.x + start.y + start.z <= 1.0) && (start.x >= 0.0) && (start.x <= 1.0) && (start.y >= 0.0) && (start.y <= 1.0) && (start.z >= 0.0) && (start.z <= 1.0)) return true;
                        if ((end.x + end.y + end.z <= 1.0) && (end.x >= 0.0) && (end.x <= 1.0) && (end.y >= 0.0) && (end.y <= 1.0) && (end.z >= 0.0) && (end.z <= 1.0)) return true;
                    }
                    start = p2; end = p4;
                    if (BoundingCube.UnitBoundingCube.ClipLine(ref start, ref end))
                    {
                        if ((start.x + start.y + start.z <= 1.0) && (start.x >= 0.0) && (start.x <= 1.0) && (start.y >= 0.0) && (start.y <= 1.0) && (start.z >= 0.0) && (start.z <= 1.0)) return true;
                        if ((end.x + end.y + end.z <= 1.0) && (end.x >= 0.0) && (end.x <= 1.0) && (end.y >= 0.0) && (end.y <= 1.0) && (end.z >= 0.0) && (end.z <= 1.0)) return true;
                    }
                    start = p3; end = p4;
                    if (BoundingCube.UnitBoundingCube.ClipLine(ref start, ref end))
                    {
                        if ((start.x + start.y + start.z <= 1.0) && (start.x >= 0.0) && (start.x <= 1.0) && (start.y >= 0.0) && (start.y <= 1.0) && (start.z >= 0.0) && (start.z <= 1.0)) return true;
                        if ((end.x + end.y + end.z <= 1.0) && (end.x >= 0.0) && (end.x <= 1.0) && (end.y >= 0.0) && (end.y <= 1.0) && (end.z >= 0.0) && (end.z <= 1.0)) return true;
                    }
                    // auch andersherum kein Schnitt, dann also sicher kein Schnitt
                    return false;
                }
                return false;
            }
            public CurveTetraeder[] Split()
            {
                GeoPoint pm;
                double parm;
                GeoPoint tv1;
                GeoPoint tv2;
                GeoPoint tv3;
                GeoPoint tv4;
                owner.SplitTetraeder(t1, t2, umin, umax, out pm, out parm, out tv1, out tv2, out tv3, out tv4);
                return new CurveTetraeder[] { new CurveTetraeder(t1, pm, tv1, tv2, umin, parm, owner), new CurveTetraeder(pm, t2, tv3, tv4, parm, umax, owner) };
            }
#if DEBUG
            Solid AsBox
            {
                get
                {
                    return Make3D.MakeTetraeder(t1, t2, t3, t4);
                }
            }
#endif
        }
        public void PlaneIntersection(PlaneSurface ps, out GeoPoint[] ips, out GeoPoint2D[] uvOnPlane, out double[] uOnCurve)
        {
            List<GeoPoint> lips = new List<GeoPoint>();
            List<GeoPoint2D> luvOnPlane = new List<GeoPoint2D>();
            List<double> luOnCurve = new List<double>();
            List<bool> isOnVertex = new List<bool>();

            for (int i = 0; i < tetraederBase.Length - 1; ++i)
            {
                CurveTetraeder id = new CurveTetraeder(this);
                id.t1 = tetraederBase[i];
                id.t2 = tetraederBase[i + 1];
                id.t3 = tetraederVertex[2 * i];
                id.t4 = tetraederVertex[2 * i + 1];
                id.umin = tetraederParams[i];
                id.umax = tetraederParams[i + 1];
                CheckPlaneIntersection(id, ps, lips, luvOnPlane, luOnCurve, isOnVertex);
            }

            for (int i = isOnVertex.Count - 1; i >= 0; --i)
            {   // a point on a vertex may be almost identical to a point found by newton. Remove this point (the newton point is probably better)
                if (isOnVertex[i])
                {
                    for (int j = 0; j < isOnVertex.Count; j++)
                    {
                        if (!isOnVertex[j] && Math.Abs(luOnCurve[i] - luOnCurve[j]) < 1e-6)
                        {
                            isOnVertex.RemoveAt(i);
                            lips.RemoveAt(i);
                            luvOnPlane.RemoveAt(i);
                            luOnCurve.RemoveAt(i);
                            break;
                        }
                    }
                }
            }
            ips = lips.ToArray();
            uvOnPlane = luvOnPlane.ToArray();
            uOnCurve = luOnCurve.ToArray();
        }
        private void CheckPlaneIntersection(CurveTetraeder id, PlaneSurface ps, List<GeoPoint> ips, List<GeoPoint2D> uvOnPlane, List<double> uOnCurve, List<bool> isOnVertex)
        {
            GeoPoint p0 = ps.ToXYPlane * id.t1;
            GeoPoint p1 = ps.ToXYPlane * id.t2;
            if (p0.z == p1.z) return;
            bool needToCheck = (Math.Sign(p0.z) != Math.Sign(p1.z)) || (Math.Abs(p0.z) < Precision.eps) || (Math.Abs(p1.z) < Precision.eps);
            bool tangential = false;
            double u = (id.umin + id.umax) / 2.0;
            if (needToCheck)
            { // zwei Basispunkte auf verschiedenen Seiten der Ebene, hier kann man schon einen guten Anfang für Newton finden
                u = id.umin + (id.umax - id.umin) * p0.z / (p0.z - p1.z); // verschiedene Vorzeichen, teilt also nie durch 0
            }
            if (needToCheck && id.umax - id.umin < 1e-8)
            {
                // ganz nah am Schnittpunkt und verschiede Vorzeichen, also fertig.
                // das sollte nicht allzu häufig vorkommen, da der Punkt mit Newton gefunden werden sollte
                uOnCurve.Add(u);
                GeoPoint ip = theCurve.PointAt(u);
                ips.Add(ip);
                uvOnPlane.Add(ps.PositionOf(ip));
                isOnVertex.Add(true);
                return;
            }
            if (!needToCheck)
            {
                GeoPoint p2 = ps.ToXYPlane * id.t3;
                GeoPoint p3 = ps.ToXYPlane * id.t4;
                needToCheck = (Math.Sign(p2.z) != Math.Sign(p0.z)) || (Math.Sign(p3.z) != Math.Sign(p0.z)); // damit auch Schnittpunkte ganz am Anfange und Ende gefunden werden
                tangential = true;
            }
            if (needToCheck)
            {
                GeoPoint ip;
                GeoPoint2D uv;
                if (NewtonIntersection(ps, id.umin, id.umax, Math.Min(Math.Abs(p0.z), Math.Abs(p1.z)), out ip, out uv, ref u))
                {
                    ips.Add(ip);
                    uvOnPlane.Add(uv);
                    uOnCurve.Add(u);
                    isOnVertex.Add(false);
                    if (tangential)
                    {   // hier ist nur einer von zwei möglichen Punkten gefunden
                        // wir müssen mit der Tangente vom anderen der beide Punkte starten,
                        // der nicht berücksichtigt wurde
                        GeoVector dir = ps.ToXYPlane * theCurve.DirectionAt(u);
                        double error;
                        if (Math.Sign(dir.z) != Math.Sign(p0.z))
                        {
                            u = id.umax;
                            error = Math.Abs(p1.z);
                        }
                        else
                        {
                            u = id.umin;
                            error = Math.Abs(p0.z);
                        }
                        if (NewtonIntersection(ps, id.umin, id.umax, error, out ip, out uv, ref u))
                        {
                            ips.Add(ip);
                            uvOnPlane.Add(uv);
                            uOnCurve.Add(u);
                            isOnVertex.Add(false);
                        }
                    }
                }
                else
                {   // könnte zwar ein Schnittpunkt sein, aber Newton konvergiert nicht...
                    // könnte auch Schnittpunkt genau am Anfang/Ende des Tetraeder sein, dann fliegt Newton auch raus wg. Bereichsgrenze
                    if (Math.Abs(p0.z) < Math.Abs(p1.z - p0.z) * 1e-6)
                    {
                        ip = id.t1;
                        uv = ps.PositionOf(id.t1);
                        ips.Add(ip);
                        uvOnPlane.Add(uv);
                        uOnCurve.Add(id.umin);
                        isOnVertex.Add(true);
                    }
                    else if (Math.Abs(p1.z) < Math.Abs(p1.z - p0.z) * 1e-6)
                    {
                        ip = id.t2;
                        uv = ps.PositionOf(id.t2);
                        ips.Add(ip);
                        uvOnPlane.Add(uv);
                        uOnCurve.Add(id.umax);
                        isOnVertex.Add(true);
                    }
                    else if (id.umax - id.umin > 1e-6)
                    {
                        GeoPoint pm, tv1, tv2, tv3, tv4;
                        double parm;
                        if (SplitTetraeder(id.t1, id.t2, id.umin, id.umax, out pm, out parm, out tv1, out tv2, out tv3, out tv4))
                        {
                            CurveTetraeder id1 = new CurveTetraeder(this);
                            id1.t1 = id.t1;
                            id1.t2 = pm;
                            id1.t3 = tv1;
                            id1.t4 = tv2;
                            id1.umin = id.umin;
                            id1.umax = parm;
                            CurveTetraeder id2 = new CurveTetraeder(this);
                            id2.t1 = pm;
                            id2.t2 = id.t2;
                            id2.t3 = tv3;
                            id2.t4 = tv4;
                            id2.umin = parm;
                            id2.umax = id.umax;
                            CheckPlaneIntersection(id1, ps, ips, uvOnPlane, uOnCurve, isOnVertex);
                            CheckPlaneIntersection(id2, ps, ips, uvOnPlane, uOnCurve, isOnVertex);
                        }
                    }
                }
            }
        }
        private bool NewtonIntersection(PlaneSurface ps, double umin, double umax, double error, out GeoPoint ip, out GeoPoint2D uv, ref double u)
        {   // bestimmt den Schnittpunkt nach dem Newtonverfahren, wobei der Schnittpunkt zwischen umin und umax liegen muss
            // und das Verfahren konvergieren muss. Wenn nicht kommt false zurück und es wird mit Bisektion weitergearbeitet
            ip = theCurve.PointAt(u);
            uv = GeoPoint2D.Origin; // damit es besetzt ist
            GeoPoint l = ps.ToXYPlane * ip;
            if (error < Math.Abs(l.z)) return false; // Fehler wird nicht kleiner, Newton hier nicht geeignet, zurück und einmal Bisektion
            try
            {
                do
                {   // konvergiert sehr gut, wenn nicht gerade tangential
                    // und auch dann noch besser als Bisektion
                    GeoVector dir = ps.ToXYPlane * theCurve.DirectionAt(u);
                    u = u - l.z / dir.z;
                    if (u < umin || u > umax) return false; // aus dem Intervall gelaufen, Newton hier nicht geeignet, zurück und einmal Bisektion
                    ip = theCurve.PointAt(u);
                    l = ps.ToXYPlane * ip;
                    if (error <= Math.Abs(l.z)) return false; // wird nicht kleiner
                    error = Math.Abs(l.z);
                } while (error > 1e-8); // konfigurierbar?
                ip = theCurve.PointAt(u);
                uv = ps.PositionOf(ip);
                return true;
            }
            catch (ArithmeticException) // durch null oder Überlauf
            {
                return false;
            }
        }
        private bool IsPartInside(Projection.PickArea area, GeoPoint p1, GeoPoint p2, double par1, double par2)
        {
            // ist irgend ein Teilstückchen der Kurve innerhalb der area?
            // Start und Endpunkt sind es nicht, sonst wäre man ja schon fertig.
#if DEBUG
            string[] dbgstack = Environment.StackTrace.Split(new string[] { "IsPartInside" }, StringSplitOptions.None);
            if (dbgstack.Length > 3)
            {
            }
#endif
            if (par2 - par1 < 1e-6) return true; // Abbruch, es ist so oft aufgeteilt, das wird als getroffen gewertet
            // p1 und p2 sind außerhalb, sonst wird hier garnicht aufgerufen, das muss nicht überprüft werden
            GeoPoint pm, tv1, tv2, tv3, tv4;
            double parm;
            SplitTetraeder(p1, p2, par1, par2, out pm, out parm, out tv1, out tv2, out tv3, out tv4);
#if DEBUGT
            DebuggerContainer dc = new DebuggerContainer();
            dc.Add(theCurve as IGeoObject);
            // area kann man dort anschauen
            try
            {
                if (!Precision.IsEqual(tv1, tv2))
                    dc.Add(Make3D.MakeTetraeder(p1, pm, tv1, tv2));
                else
                    dc.Add(Face.MakeFace(p1, pm, tv1));
                if (!Precision.IsEqual(tv3, tv4))
                    dc.Add(Make3D.MakeTetraeder(pm, p2, tv3, tv4));
                else
                    dc.Add(Face.MakeFace(pm, p2, tv3));
            }
            catch (PlaneException) { }
#endif
            if (BoundingCube.UnitBoundingCube.Contains(area.ToUnitBox * pm)) return true; // zwischenpunkt drin, also berührt
            if (BoundingCube.UnitBoundingCube.Interferes(area.ToUnitBox * p1, area.ToUnitBox * pm, area.ToUnitBox * tv1, area.ToUnitBox * tv2))
            {
                // 1. Hälfte nicht eindeutig, muss noch weiter untersucht werden
                if (IsPartInside(area, p1, pm, par1, parm)) return true;
            }
            if (BoundingCube.UnitBoundingCube.Interferes(area.ToUnitBox * pm, area.ToUnitBox * p2, area.ToUnitBox * tv3, area.ToUnitBox * tv4))
            {
                // 2. Hälfte nicht eindeutig, muss noch weiter untersucht werden
                if (IsPartInside(area, pm, p2, parm, par2)) return true;
            }
            return false;
        }
        private bool IsPartInside(BoundingCube cube, GeoPoint p1, GeoPoint p2, double par1, double par2)
        {
            // ist irgend ein Teilstückchen der Kurve innerhalb der area?
            // Start und Endpunkt sind es nicht, sonst wäre man ja schon fertig.
            if (par2 - par1 < 1e-6) return true; // Abbruch, es ist so oft aufgeteilt, das wird als getroffen gewertet
            // p1 und p2 sind außerhalb, sonst wird hier garnicht aufgerufen, das muss nicht überprüft werden
            GeoPoint pm, tv1, tv2, tv3, tv4;
            double parm;
            SplitTetraeder(p1, p2, par1, par2, out pm, out parm, out tv1, out tv2, out tv3, out tv4);
#if DEBUG
            //DebuggerContainer dc = new DebuggerContainer();
            //dc.Add(theCurve as IGeoObject);
            //// area kann man dort anschauen
            //try
            //{
            //    if (!Precision.IsEqual(tv1, tv2))
            //        dc.Add(Make3D.MakeTetraeder(p1, pm, tv1, tv2));
            //    else
            //        dc.Add(Face.MakeFace(p1, pm, tv1));
            //    if (!Precision.IsEqual(tv3, tv4))
            //        dc.Add(Make3D.MakeTetraeder(pm, p2, tv3, tv4));
            //    else
            //        dc.Add(Face.MakeFace(pm, p2, tv3));
            //}
            //catch (PlaneException) { }
            //catch (ModOpException) { }
#endif
            if (cube.Contains(pm)) return true; // zwischenpunkt drin, also berührt
            if (cube.Interferes(p1, pm, tv1, tv2))
            {
                // 1. Hälfte nicht eindeutig, muss noch weiter untersucht werden
                if (IsPartInside(cube, p1, pm, par1, parm)) return true;
            }
            if (cube.Interferes(pm, p2, tv3, tv4))
            {
                // 2. Hälfte nicht eindeutig, muss noch weiter untersucht werden
                if (IsPartInside(cube, pm, p2, parm, par2)) return true;
            }
            return false;
        }
        private bool IsPartOutside(Projection.PickArea area, GeoPoint p1, GeoPoint p2, double par1, double par2)
        {
            if (par2 - par1 < 1e-6) return true; // Abbruch, es ist so oft aufgeteilt, das wird als getroffen gewertet
            // p1 und p2 sind innerhalb, sonst wird hier garnicht aufgerufen, das muss nicht überprüft werden
            GeoPoint pm, tv1, tv2, tv3, tv4;
            double parm;
            SplitTetraeder(p1, p2, par1, par2, out pm, out parm, out tv1, out tv2, out tv3, out tv4);
            if (!BoundingCube.UnitBoundingCube.Contains(area.ToUnitBox * pm)) return true; // zwischenpunkt draußen, also außerhalb
            if (!BoundingCube.UnitBoundingCube.Contains(area.ToUnitBox * tv1) ||
                !BoundingCube.UnitBoundingCube.Contains(area.ToUnitBox * tv2))
            {
                // 1. Hälfte nicht eindeutig, muss noch weiter untersucht werden
                if (IsPartOutside(area, p1, pm, par1, parm)) return true;
            }
            if (!BoundingCube.UnitBoundingCube.Contains(area.ToUnitBox * tv3) ||
                !BoundingCube.UnitBoundingCube.Contains(area.ToUnitBox * tv4))
            {
                // 2. Hälfte nicht eindeutig, muss noch weiter untersucht werden
                if (IsPartOutside(area, pm, p2, parm, par2)) return true;
            }
            return false;
        }
        public static bool IsPointInside(GeoPoint toTest, GeoPoint tetra1, GeoPoint tetra2, GeoPoint tetra3, GeoPoint tetra4)
        {
            if (Precision.IsEqual(tetra3, tetra4))
            {   // ein flaches Dreieck, liefert unsichere Ergebnisse bei Solve..
                // eigentlich sollt vor dem Aufruf IsPointInside schon eon Test stattfinden
                GeoVector v1 = tetra2 - tetra1;
                GeoVector v2 = tetra3 - tetra1;
                GeoVector v3 = v1 ^ v2;
                // v3 ist senkrecht auf beide, d.h. z muss 0 sein
                GeoVector v4 = toTest - tetra1;
                Matrix m = Extensions.RowVector(v1, v2, v3);
                Matrix s = (Matrix)m.Solve(Extensions.RowVector(v4));
                if (s.IsValid())
                {
                    double x = s[0, 0];
                    double y = s[1, 0];
                    double z = s[2, 0];
                    if (x < 0 || x > 1 || y < 0 || y > 1 || Math.Abs(z) > (v3.Length * 1e-9)) return false;
                    return (x + y < 1);
                }
                else
                {
                    return false; // sollte nie vorkommen
                }
            }
            else
            {
                // Überlegung: der Punkt ausgedrückt im Koordinatensystem von 3 aufspannenden Vektoren
                // muss in allen Koordinaten >0 und <1 sein und die Summe muss <1 sein. Stimmt das ?
                GeoVector v1 = tetra2 - tetra1;
                GeoVector v2 = tetra3 - tetra1;
                GeoVector v3 = tetra4 - tetra1;

                GeoVector v4 = toTest - tetra1;
                // x*v1+y*v2+z*v3 = v4; Löse für x,y und z
                Matrix m = Extensions.RowVector(v1, v2, v3);
                Matrix s = (Matrix)m.Solve(Extensions.RowVector(v4));
                if (s.IsValid())
                {
                    double x = s[0, 0];
                    double y = s[1, 0];
                    double z = s[2, 0];
                    if (x < 0 || x > 1 || y < 0 || y > 1 || z < 0 || z > 1) return false;
                    return (x + y + z < 1);
                }
                else
                {
                    return false; // sollte nie vorkommen
                }
            }
        }
#if DEBUG
        internal GeoObjectList Debug
        {
            get
            {
                GeoObjectList dbg = new GeoObjectList();
                for (int i = 0; i < tetraederBase.Length - 1; ++i)
                {
                    GeoPoint p1 = tetraederBase[i];
                    GeoPoint p2 = tetraederBase[i + 1];
                    GeoPoint p3 = tetraederVertex[2 * i];
                    GeoPoint p4 = tetraederVertex[2 * i + 1];
                    GeoPoint pm = new GeoPoint(p1, p2, p3, p4);
                    if (!IsPointInside(pm, p1, p2, p3, p4))
                    {
                    }
                    //Plane pl1 = new Plane(p1, p3, p2);
                    //GeoPoint p11 = pl1.ToLocal(pm);
                    //Plane pl2 = new Plane(p2, p4, p1);
                    //GeoPoint p12 = pl2.ToLocal(pm);
                    //Plane pl3 = new Plane(p3, p1, p4);
                    //GeoPoint p13 = pl3.ToLocal(pm);
                    //Plane pl4 = new Plane(p4, p2, p3);
                    //GeoPoint p14 = pl4.ToLocal(pm);
                    //ColorDef cd;
                    //if (p1.z < 0 || p2.z < 0 || p3.z < 0 || p4.z < 0)
                    //{
                    //}
                    ColorDef cd = new ColorDef("pos", Color.HotPink);
                    ColorDef cd1 = new ColorDef("pos", Color.Blue);
                    ColorDef cd2 = new ColorDef("lin", Color.Green);
                    if ((p3 == p4) && (p3 == new GeoPoint(p1, p2)))
                    {
                        Line line = Line.Construct();
                        line.SetTwoPoints(p1, p2);
                        line.ColorDef = cd;
                        dbg.Add(line);
                    }
                    else
                    {
                        try
                        {
                            Face fc1 = Face.MakeFace(p1, p2, p3);
                            fc1.ColorDef = cd;
                            dbg.Add(fc1);
                        }
                        catch { }
                        try
                        {
                            Face fc2 = Face.MakeFace(p1, p2, p4);
                            fc2.ColorDef = cd;
                            dbg.Add(fc2);
                        }
                        catch { }
                        try
                        {
                            if (!Precision.IsEqual(p3, p4))
                            {
                                Face fc3 = Face.MakeFace(p1, p3, p4);
                                fc3.ColorDef = cd;
                                dbg.Add(fc3);
                                Face fc4 = Face.MakeFace(p2, p3, p4);
                                fc4.ColorDef = cd;
                                dbg.Add(fc4);
                            }
                        }
                        catch { }
                    }
                }
                dbg.Add(theCurve as IGeoObject);
                return dbg;
            }
        }
        internal GeoObjectList DebugDirection
        {
            get
            {
                GeoObjectList dbg = new GeoObjectList();
                ColorDef cd1 = new ColorDef("pos", Color.Blue);
                ColorDef cd2 = new ColorDef("lin", Color.Green);
                GeoPoint[] pnts = new GeoPoint[100];
                GeoVector[] dirs = new GeoVector[100];
                double len = 0.0;
                for (int i = 0; i < 100; i++)
                {
                    pnts[i] = theCurve.PointAt(i / 99.0);
                    dirs[i] = theCurve.DirectionAt(i / 99.0);
                    if (i > 0) len += pnts[i] | pnts[i - 1];
                }
                len /= 150;
                for (int i = 0; i < 100; i++)
                {
                    Line l = Line.TwoPoints(pnts[i], pnts[i] + len * dirs[i].Normalized);
                    l.ColorDef = cd1;
                    dbg.Add(l);
                    if (i > 0)
                    {
                        l = Line.TwoPoints(pnts[i - 1], pnts[i]);
                        l.ColorDef = cd2;
                        dbg.Add(l);
                    }
                }
                return dbg;
            }
        }
#endif
        public double PositionOf(GeoPoint p)
        {
            lock (OctTree)
            {
                if (TetraederBase.Length < 2)
                {
                    double d1 = p | theCurve.StartPoint;
                    double d2 = p | theCurve.EndPoint;
                    if (d1 == d2) return 0.5;
                    if (d1 < d2) return 0.0;
                    else return 1.0;
                }
                CurveTetraeder[] all = OctTree.GetObjectsFromBox(new BoundingCube(p, OctTree.precision)); // we need ...Box, BoundingCube and not ...Point because of linear curves
                if (all.Length == 0)
                {   // Punkt aufblasen bis Tetraeder gefunden werden
                    BoundingCube bc = new BoundingCube(TetraederBase);
                    bc.MinMax(p);
                    double d = bc.Size * 1e-6;
                    do
                    {
                        all = OctTree.GetObjectsFromBox(new BoundingCube(p, d));
                        d *= 2;
                    }
                    while (all.Length == 0 && d < bc.Size * 2);
                }
                double mindist = double.MaxValue;
                double umindist = -1.0;
                if (all.Length > 0)
                {
                    for (int i = 0; i < all.Length; ++i)
                    {
                        GeoPoint pfound;
                        double u;
                        double d = FindClosestPoint(p, all[i].umin, all[i].umax, out pfound, out u);
                        if (d < mindist)
                        {
                            mindist = d;
                            umindist = u;
                        }
                    }
                }
                else
                {
                    return double.MaxValue;
                }
                return umindist;
            }
        }

        private double FindClosestPoint(GeoPoint p, double umin, double umax, out GeoPoint pfound, out double u)
        {
            u = (umin + umax) / 2.0;
            GeoVector dir = theCurve.DirectionAt(u);
            pfound = theCurve.PointAt(u);
            double dist = p | pfound;
            bool clipped = false;
            // there is a problem with a nurbs curve, which is exactely a circle, finding the closest point where p is the center
            int cnt = 0;
            while (dist > Precision.eps && cnt++ < 30)
            {
#if DEBUG
                GeoObjectList dbg = new GeoObjectList();
                dbg.Add(theCurve as IGeoObject);
                dbg.Add(Line.TwoPoints(pfound, pfound + dir));
                dbg.Add(Line.TwoPoints(theCurve.PointAt(umin), theCurve.PointAt(umax)));
#endif
                double d = (dir.x * dir.x + dir.y * dir.y + dir.z * dir.z);
                double lambda = -((pfound.x - p.x) * dir.x + (pfound.y - p.y) * dir.y + (pfound.z - p.z) * dir.z) / d;
                u += lambda;
                if (u < umin)
                {
                    u = umin;
                    if (clipped)
                    {
                        // return double.MaxValue; // konvergiert nicht in diesem Intervall
                        pfound = theCurve.PointAt(u);
                        return pfound | p;
                    }
                    clipped = true;
                }
                else if (u > umax)
                {
                    u = umax;
                    if (clipped)
                    {
                        //return double.MaxValue; // konvergiert nicht in diesem Intervall
                        pfound = theCurve.PointAt(u);
                        return pfound | p;
                    }
                    clipped = true;
                }
                else clipped = false;
                dir = theCurve.DirectionAt(u);
                pfound = theCurve.PointAt(u);
                d = p | pfound;
                if (d >= dist && cnt > 2) return d; // doesn't converge, allow two bad steps at the beginning
                dist = d;
            }
            return dist;
        }

        internal double GetLength()
        {
            double length = 0.0;
            for (int i = 0; i < tetraederBase.Length - 1; ++i)
            {
                if (IsLine(i)) length += tetraederBase[i] | tetraederBase[i + 1];
                else
                {
                    double d1 = Math.Min((tetraederBase[i] | tetraederVertex[2 * i]), (tetraederBase[i + 1] | tetraederVertex[2 * i]));
                    double d2 = Math.Min((tetraederBase[i] | tetraederVertex[2 * i + 1]), (tetraederBase[i + 1] | tetraederVertex[2 * i + 1]));
                    double d3 = tetraederVertex[2 * i] | tetraederVertex[2 * i + 1];
                    // d1+d2+d3 is eine sichere Obergrenze, im Sonderfall Dreieck ist d3 0.0
                    double d = tetraederBase[i] | tetraederBase[i + 1];
                    // d ist eine sichere Untergrenze, bei einer Linie ist d==d1+d2+d3
                    // aber d hat etwas mehr Gewicht
                    // bei einem 60° Kreisbogen ist d==r, d1+d2==1.1547 * r und die echte Länge l==r * pi/3 == r * 1.047
                    // in unserer Formel wird der Wert zu 1.051 statt 1.047
                    length += (2.0 * d + (d1 + d2 + d3)) / 3.0;
                }
            }
            return length;

        }

        internal ICurve Approximate(bool linesOnly, double maxError)
        {
            List<GeoPoint> points = new List<GeoPoint>();
            points.Add(tetraederBase[0]); // Startpunkt
            for (int i = 0; i < tetraederBase.Length - 1; ++i)
            {
                AddAproximation(tetraederBase[i], tetraederBase[i + 1], tetraederParams[i], tetraederParams[i + 1], maxError, points);
            }
            Polyline res = Polyline.Construct();
            res.SetPoints(points.ToArray(), false);
            return res;
        }

        private void AddAproximation(GeoPoint sp, GeoPoint ep, double spar, double epar, double maxError, List<GeoPoint> points)
        {
            double mpar = (spar + epar) / 2.0;
            GeoPoint mp = theCurve.PointAt(mpar);
            if ((sp | ep) < maxError || Geometry.DistPL(mp, sp, ep) < maxError)
            {
                points.Add(ep);
            }
            else
            {
                AddAproximation(sp, mp, spar, mpar, maxError, points);
                AddAproximation(mp, ep, mpar, epar, maxError, points);
            }
        }

        internal double Position(GeoPoint fromHere, GeoVector direction, double precision)
        {   // gesucht die Z-Höhe für das Picken, um verschiedene Objekte nach vorne und hinten unterscheiden zu können
            // ein Octtree um die passenden Tetraeder zu finden, 
            // hier erstmal grobe Annäherung
            double mindist = double.MaxValue;
            double res = double.MaxValue;
            for (int i = 0; i < tetraederBase.Length - 1; ++i)
            {
                double par1, par2;
                double d = Geometry.DistLLWrongPar2(tetraederBase[i], tetraederBase[i + 1] - tetraederBase[i], fromHere, direction, out par1, out par2);
                if (d < mindist)
                {
                    mindist = d;
                    res = par2;
                }
            }
            return res;
        }

        internal int Intersect(TetraederHull th2, out double[] par1, out double[] par2, out GeoPoint[] intersection)
        {
            List<double> lpar1 = new List<double>();
            List<double> lpar2 = new List<double>();
            List<GeoPoint> lip = new List<GeoPoint>();
            lock (OctTree)
            {
                CurveTetraeder[] all2 = th2.OctTree.GetAllObjects();
                for (int i = 0; i < all2.Length; ++i)
                {
                    CurveTetraeder[] all1 = OctTree.GetObjectsCloseTo(all2[i]);
                    for (int j = 0; j < all1.Length; ++j)
                    {
                        if (all1[j].Interferes(all2[i]))
                        {
                            Intersect(all1[j], th2, all2[i], lpar1, lpar2, lip);
                        }
                    }
                }
                par1 = lpar1.ToArray();
                par2 = lpar2.ToArray();
                intersection = lip.ToArray();
                return par1.Length;
            }
        }

        private void Intersect(CurveTetraeder tet1, TetraederHull th2, CurveTetraeder tet2, List<double> lpar1, List<double> lpar2, List<GeoPoint> lip)
        {   // versuche den kürzesten Abstand zu finden zwischen dieser und th2
            // es kann vorkommen, dass die Lösung mehrfach gefunden wird.
            double par1, par2, dist;
            GeoPoint ip;
            if (NewtonFindClosestPoint(theCurve, tet1.umin, tet1.umax, th2.theCurve, tet2.umin, tet2.umax, out par1, out par2, out ip, out dist))
            {   // gefunden, aber ist es auch ein Schnittpunkt oder nur die kürzeste Verbindung?
                if (dist < Precision.eps)
                {
                    lpar1.Add(par1);
                    lpar2.Add(par2);
                    lip.Add(ip);
                }
            }
            else
            {
                if ((tet1.umax - tet1.umin < 1e-6) && (tet2.umax - tet2.umin < 1e-6))
                {
                    par1 = (tet1.umax + tet1.umin) / 2.0;
                    par2 = (tet2.umax + tet2.umin) / 2.0;
                    GeoPoint p1 = theCurve.PointAt(par1);
                    GeoPoint p2 = th2.theCurve.PointAt(par2);
                    if (Precision.IsEqual(p1, p2))
                    {
                        lpar1.Add(par1);
                        lpar2.Add(par2);
                        lip.Add(new GeoPoint(p1, p2));
                    }
                }
                else
                {
                    CurveTetraeder[] sp1 = tet1.Split();
                    CurveTetraeder[] sp2 = tet2.Split();
                    // jetzt 4 mögliche Schnittpunkte
                    if (sp1[0].Interferes(sp2[0])) Intersect(sp1[0], th2, sp2[0], lpar1, lpar2, lip);
                    if (sp1[0].Interferes(sp2[1])) Intersect(sp1[0], th2, sp2[1], lpar1, lpar2, lip);
                    if (sp1[1].Interferes(sp2[0])) Intersect(sp1[1], th2, sp2[0], lpar1, lpar2, lip);
                    if (sp1[1].Interferes(sp2[1])) Intersect(sp1[1], th2, sp2[1], lpar1, lpar2, lip);
                }
            }
        }

        private bool NewtonFindClosestPoint(ICurve curve1, double umin1, double umax1, ICurve curve2, double umin2, double umax2, out double par1, out double par2, out GeoPoint ip, out double dist)
        {
            par1 = (umax1 + umax1) / 2.0;
            par2 = (umax2 + umax2) / 2.0;
            GeoPoint p1 = curve1.PointAt(par1);
            GeoPoint p2 = curve2.PointAt(par2);
            GeoVector dir1 = curve1.DirectionAt(par1);
            GeoVector dir2 = curve2.DirectionAt(par2);
            dist = p1 | p2;
            ip = new GeoPoint(p1, p2);
            int n = 0;
            bool clipped = false;
            while (dist >= Precision.eps)
            {
#if DEBUG
                //DebuggerContainer dc = new DebuggerContainer();
                //dc.Add(curve1 as IGeoObject);
                //dc.Add(curve2 as IGeoObject);
                //Line l1 = Line.Construct();
                //l1.SetTwoPoints(p1, p1 + dir1);
                //dc.Add(l1);
                //Line l2 = Line.Construct();
                //l2.SetTwoPoints(p2, p2 + dir2);
                //dc.Add(l2);
#endif
                GeoVector xdir = dir1 ^ dir2;
                double d = double.MaxValue;
                Matrix m = DenseMatrix.OfColumnArrays(dir1, dir2, xdir);
                Vector b = new DenseVector(p2 - p1);
                Vector x = (Vector)m.Solve(b);
                if (x.IsValid())
                {
                    par1 += x[0];
                    par2 -= x[1]; // das ist richtig so, in DistLL hat par2 falsches Vorzeichen!
                    // der Parameter darf nicht aus dem [0,1] Intervall rauslaufen, denn dort gibt es keine Werte
                    // tut er es trotzdem, dann wird auf das Ende geklippt. Das Newtonverfahren
                    // kann ja hin und her wackeln, aber wenns nach dem Klippen immer noch rausläuft, dann ist aus
                    // dann liegt der Schnittpunkt außerhalb
                    if (par1 < 0.0)
                    {
                        if (clipped) return false; // kein 2. Mal klippen
                        par1 = 0.0;
                        clipped = true;
                    }
                    if (par1 > 1.0)
                    {
                        if (clipped) return false; // kein 2. Mal klippen
                        par1 = 1.0;
                        clipped = true;
                    }
                    if (par2 < 0.0)
                    {
                        if (clipped) return false; // kein 2. Mal klippen
                        par2 = 0.0;
                        clipped = true;
                    }
                    if (par2 > 1.0)
                    {
                        if (clipped) return false; // kein 2. Mal klippen
                        par2 = 1.0;
                        clipped = true;
                    }
                    p1 = curve1.PointAt(par1);
                    p2 = curve2.PointAt(par2);
                    dir1 = curve1.DirectionAt(par1);
                    dir2 = curve2.DirectionAt(par2);
                    d = p1 | p2;
                }
                if (d >= dist) return false; // konvergiert nicht
                dist = d;
                ++n;
                if (n > 40) return false; // konvergiert zu langsam
            }
            ip = new GeoPoint(p1, p2);
            return true;
        }

        internal bool SameGeometry(ICurve other, double precision)
        {
            double d1 = (theCurve.StartPoint | other.StartPoint) + (theCurve.EndPoint | other.EndPoint);
            double d2 = (theCurve.EndPoint | other.StartPoint) + (theCurve.StartPoint | other.EndPoint);
            bool reverse = d1 > d2;
            TetraederHull h2;
            if (reverse)
            {
                if (d2 > precision) return false;
                ICurve cr = other.Clone();
                cr.Reverse();
                h2 = new TetraederHull(cr); // hier sollte es ein Interface geben, welches die Hülle liefert um Mehrfachberechnungen zu vermeiden
            }
            else
            {
                if (d1 > precision) return false;
                h2 = new TetraederHull(other);
            }
            // jetzt überprüfen wir die Positionen der Basispunkte und die dort vorhandenen Richtungen
            for (int i = 0; i < tetraederBase.Length; ++i)
            {
                double u = h2.theCurve.PositionOf(tetraederBase[i]);
                double uu = h2.PositionOf(tetraederBase[i]);
                if ((h2.theCurve.PointAt(uu) | tetraederBase[i]) > precision) return false;
                // jetzt könnte man noch die Richtungen überprüfen, aber es gibt kein gutes Kriterium für die erlaubte Abweichung
            }
            for (int i = 0; i < h2.tetraederBase.Length; ++i)
            {
                double u = theCurve.PositionOf(h2.tetraederBase[i]);
                if ((theCurve.PointAt(u) | h2.tetraederBase[i]) > precision) return false;
                // jetzt könnte man noch die Richtungen überprüfen, aber es gibt kein gutes Kriterium für die erlaubte Abweichung
            }
            // there was this case: the curves only consist of a single tetraeder and start- and endpoints are identical. then we should check points in the middle of each tetraeder.
            for (int i = 0; i < tetraederBase.Length - 1; ++i)
            {
                GeoPoint mp = theCurve.PointAt((tetraederParams[i] + tetraederParams[i + 1]) / 2.0);
                if (other.DistanceTo(mp) > precision) return false;
            }
            for (int i = 0; i < h2.tetraederBase.Length - 1; ++i)
            {
                GeoPoint mp = other.PointAt((h2.tetraederParams[i] + h2.tetraederParams[i + 1]) / 2.0);
                if (theCurve.DistanceTo(mp) > precision) return false;
            }
            return true;
        }

        internal double[] Intersect(Polynom implicitSurface)
        {
            List<double> res = new List<double>();
            double d0 = implicitSurface.Eval(tetraederBase[0]);
            for (int i = 0; i < tetraederBase.Length - 1; ++i)
            {
                double d1 = implicitSurface.Eval(tetraederBase[i + 1]);
                if (Math.Sign(d0) != Math.Sign(d1)) AddSingleIntersection(implicitSurface, TetraederParams[i], TetraederParams[i + 1], res);
                else
                {   // the surface does not intersect the linear connection of tetraederBase[i] and tetraederBase[i+1] or
                    // it intersects an even number of times (a torus might intersect a circle 4 times)
                    // here we look, whether the surface has a minimum distaance to the line in this segment, and if so
                    // we try to find multiple intersection points 
                    GeoPoint startPoint = tetraederBase[i];
                    GeoVector direction = tetraederBase[i + 1] - tetraederBase[i];
                    Polynom toSolve = implicitSurface.Substitute(new Polynom(direction.x, "u", startPoint.x, ""), new Polynom(direction.y, "u", startPoint.y, ""), new Polynom(direction.z, "u", startPoint.z, "")).Derivate(1);
                    double[] roots = toSolve.Roots();
                    // roots are the positions on the line from tetraederBase[i] to tetraederBase[i+1] where the distance to the surface has a minimum
                    // or maximum
                    for (int j = 0; j < roots.Length; j++)
                    {
                        if (roots[j] >= 0 && roots[j] <= 1)
                        {
                            double u = TetraederParams[i] + roots[j] * (TetraederParams[i + 1] - TetraederParams[i]);
                            double du = implicitSurface.Eval(theCurve.PointAt(u));
                            if (Math.Sign(du) != Math.Sign(d0))
                            {
                                AddSingleIntersection(implicitSurface, TetraederParams[i], u, res);
                                AddSingleIntersection(implicitSurface, u, TetraederParams[i + 1], res);
                            }
                        }
                    }
                }
                d0 = d1;
                //tetraederVertex[2 * i]
                //  tetraederVertex[2 * i + 1]
            }
            return res.ToArray();
        }

        private void AddSingleIntersection(Polynom implicitSurface, double startParam, double endParam, List<double> list)
        {
            double u = (startParam + endParam) / 2.0;
            if (theCurve.TryPointDeriv2At(u, out GeoPoint location, out GeoVector deriv1, out GeoVector deriv2))
            {
                // find a*u² + b*u + c, which approximates the curve at u and a, b, c are points/vectors.
                GeoVector a = deriv2 / 2.0;
                GeoVector b = deriv1;
                GeoPoint c = location;
                Polynom toSolve = implicitSurface.Substitute(new Polynom(a.x, "u2", b.x, "u", c.x, ""), new Polynom(a.y, "u2", b.y, "u", c.y, ""), new Polynom(a.z, "u2", b.z, "u", c.z, ""));
                double[] roots = toSolve.Roots();
                // here should be roots, since startParam and endParam are on different sides of the surface (which is not open)
                // if not, we would have to use bisection
            }
        }

        /// <summary>
        /// Computes the curvature circle at a given parameter on a 3D curve.
        /// </summary>
        /// <param name="curve">The curve to evaluate.</param>
        /// <param name="u">The curve parameter.</param>
        /// <returns>
        /// A tuple containing:
        /// - center: the center point of the osculating circle,
        /// - normal: the normal vector of the osculating plane,
        /// - radius: the curvature radius (1 / curvature).
        /// </returns>
        public (GeoPoint center, GeoVector normal, double radius) CurvatureAt(ICurve curve, double u)
        {
            if (!curve.TryPointDeriv2At(u, out GeoPoint point, out GeoVector deriv1, out GeoVector deriv2))
            {
                throw new ArgumentException("Curve does not support second derivative at the given parameter.");
            }

            double deriv1Length = deriv1.Length;
            if (deriv1Length < 1e-12)
            {
                throw new ArgumentException("First derivative is too small to determine curvature.");
            }

            // Tangent vector
            GeoVector T = deriv1 / deriv1Length;

            // Normal component of second derivative
            GeoVector proj = (deriv2 * T) * T;
            GeoVector normalComponent = deriv2 - proj;

            double normalLength = normalComponent.Length;
            if (normalLength < 1e-12)
            {
                // Curve is locally straight (e.g. line)
                return (point, GeoVector.NullVector, double.PositiveInfinity);
            }

            // Unit normal vector (direction to curvature center)
            GeoVector N = normalComponent / normalLength;

            // Curvature and radius
            double curvature = normalLength / (deriv1Length * deriv1Length);
            double radius = 1.0 / curvature;

            // Center of curvature
            GeoPoint center = point + radius * N;

            return (center, (deriv1 ^ deriv2).Normalized, radius);
        }

    }
}
