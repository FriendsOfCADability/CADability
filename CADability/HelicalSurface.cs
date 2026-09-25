using CADability.Curve2D;
using CADability.UserInterface;
using System;
using System.Collections.Generic;
using System.Runtime.Serialization;

namespace CADability.GeoObject
{
    /// <summary>
    /// A helical surface defined by an axis, a curve and a pitch. the curve and the axis must reside in a common plane. The curve is revolved around the axis
    /// while beeing moved in direction of the axis. The distance movement in direction of the aixs is "pitch" for one full turn.
    /// </summary>
    [Serializable()]
    public class HelicalSurface : ISurfaceImpl, ISerializable
    {
        private double curveStartParameter, curveEndParameter;
        private double curveParameterOffset;
        private ModOp toSurface; // diese ModOp modifiziert die 2d Kurve, die um die y-Achse rotiert wird in die 3d Kurve
        private ModOp fromSurface; // invers zu toSurface
        private ICurve2D basisCurve2D; // basiscurve in 2d. It is rotated around the y-axis to form the surface and the modified by toSurface
        private double pitch; // pitch in y direction relative to basisCurve2D

        // ACHTUNG: Achse und Kurve müssen in einer Ebene liegen, damit dieses Objekt mit seinem 
        // OCas Partner kompatibel ist!
        internal HelicalSurface(ICurve2D basisCurve2D, double pitch, ModOp toSurface, double curveStartParameter, double curveEndParameter, double curveParameterOffset) : base()
        {
            this.basisCurve2D = basisCurve2D;
            this.pitch = pitch;
            this.toSurface = toSurface;
            this.fromSurface = toSurface.GetInverse();
            this.curveStartParameter = curveStartParameter;
            this.curveEndParameter = curveEndParameter;
            this.curveParameterOffset = curveParameterOffset;
        }
        public HelicalSurface(ICurve basisCurve, GeoPoint axisLocation, GeoVector axisDirection, double pitch, double curveStartParameter, double curveEndParameter, GeoPoint? axisRefPoint = null) : base()
        {
            this.curveEndParameter = curveEndParameter;
            this.curveStartParameter = curveStartParameter;
            this.pitch = pitch;
            // curveStartParameter und curveEndParameter haben folgenden Sinn: 
            // der Aufrufer werwartet ein bestimmtes u/v System. 
            // in u ist es die Kreisbewegung, in v ist es die Kurve (andersrum als bei SurfaceOfLinearExtrusion)
            // curveStartParameter und curveEndParameter definieren die Lage von v, die Lage von u ist durch die Ebene
            // bestimmt
            // finden der Hauptebene, in der alles stattfindet. Die ist gegeben durch die Achse und die Kurve
            Plane pln;
            if (axisRefPoint.HasValue)
            {
                GeoPoint cnt = Geometry.DropPL(axisRefPoint.Value, axisLocation, axisDirection);
                pln = new Plane(axisLocation, axisRefPoint.Value - cnt, axisDirection);
            }
            else
            {
                double ds = Geometry.DistPL(basisCurve.StartPoint, axisLocation, axisDirection);
                double de = Geometry.DistPL(basisCurve.EndPoint, axisLocation, axisDirection);
                if (ds > de && ds > Precision.eps)
                {
                    GeoPoint cnt = Geometry.DropPL(basisCurve.StartPoint, axisLocation, axisDirection);
                    pln = new Plane(axisLocation, basisCurve.StartPoint - cnt, axisDirection);
                }
                else if (de >= ds && de > Precision.eps)
                {
                    GeoPoint cnt = Geometry.DropPL(basisCurve.EndPoint, axisLocation, axisDirection);
                    pln = new Plane(axisLocation, basisCurve.EndPoint - cnt, axisDirection);
                }
                else if (basisCurve.GetPlanarState() == PlanarState.Planar)
                {
                    Plane ppln = basisCurve.GetPlane();
                    GeoVector dirx = ppln.Normal ^ axisDirection; // richtig rum?
                    pln = new Plane(axisLocation, dirx, axisDirection);
                }
                else
                {   // es könnte sein, dass die ganze Fläche singulär ist
                    // dann kann man nichts machen, sollte aber nicht vorkommen
                    throw new SurfaceOfRevolutionException("Surface is invalid");
                }
            }
            // pln hat jetzt den Ursprung bei axisLocation, die y-Achse ist axisDirection und die x-Achse durch die
            // die Orientierung scheint mir noch fraglich, wierum wird gedreht, wierum läuft u
            // Kurve gegeben
            basisCurve2D = basisCurve.GetProjectedCurve(pln);
            // ACHTUNG "Parameterproblem": durch die Projektion der Kurve verschiebt sich bei Kreisen der Parameterraum
            // da 2D Kreise bzw. Bögen keine Achse haben und sich immer auf die X-Achse beziehen
            if (basisCurve2D is Arc2D)
            {
                curveParameterOffset = (basisCurve2D as Arc2D).StartParameter - this.curveStartParameter;
            }
            else if (basisCurve2D is BSpline2D)
            {   // beim BSpline fängt der Parameter im 3D bei knots[0] an, welches gleich this.curveStartParameter ist
                // curveParameterOffset = this.curveStartParameter;
                // wenn man einen geschlossenen Spline um eine Achse rotieren lässt
                // dann entsteht mit obiger Zeile ein Problem, mit dieser jedoch nicht:
                curveParameterOffset = (basisCurve2D as BSpline2D).Knots[0];
            }
            else
            {
                curveParameterOffset = 0.0;
            }
            toSurface = ModOp.Fit(new GeoPoint[] { GeoPoint.Origin, GeoPoint.Origin + GeoVector.XAxis, GeoPoint.Origin + GeoVector.YAxis },
                                  new GeoPoint[] { axisLocation, axisLocation + pln.DirectionX, axisLocation + pln.DirectionY }, false);
            fromSurface = toSurface.GetInverse();
        }
        /// <summary>
        /// Returns the location of the axis of revolution
        /// </summary>
        public GeoPoint Location
        {
            get
            {
                return toSurface * GeoPoint.Origin;
            }
        }
        /// <summary>
        /// Returns the direction of the axis of revolution
        /// </summary>
        public GeoVector Axis
        {
            get
            {
                return toSurface * GeoVector.YAxis;
            }
        }
        /// <summary>
        /// Returns the 0 position of the revolution
        /// </summary>
        public GeoVector XAxis
        {
            get
            {
                return toSurface * GeoVector.XAxis;
            }
        }
        /// <summary>
        /// Returns the curve that is rotated to form this surface
        /// </summary>
        public ICurve BasisCurve
        {
            get
            {
                IGeoObject go = basisCurve2D.MakeGeoObject(Plane.XYPlane);
                go.Modify(toSurface);
                return go as ICurve;
            }
        }
        public double Pitch
        {
            get
            {
                return pitch;
            }
        }
        #region ISurfaceImpl overrides
        private double GetPos(double v)
        {   // wandelt v in einen Wert zwischen 0 und 1 um
            return (v - curveStartParameter) / (curveEndParameter - curveStartParameter);
        }
        /// <summary>
        /// Overrides <see cref="CADability.GeoObject.ISurfaceImpl.PointAt (GeoPoint2D)"/>
        /// </summary>
        /// <param name="uv"></param>
        /// <returns></returns>
        public override GeoPoint PointAt(GeoPoint2D uv)
        {
            double pos = GetPos(uv.y);
            GeoPoint2D p2d = basisCurve2D.PointAt(pos);
            ModOp rot = ModOp.Rotate(1, (SweepAngle)uv.x);
            ModOp movePitch = ModOp.Translate(0, pitch * uv.x / (Math.PI * 2.0), 0);
            return toSurface * movePitch * rot * p2d;
        }
//        public override GeoPoint2D PositionOf(GeoPoint p)
//        {
//            GeoPoint pUnit = fromSurface * p; // point in a system, where the basiscurve is in xy plane and the rotation axis is the y axis
//            double rotangle = Math.Atan2(pUnit.z, pUnit.x);
//            if (rotangle < 0) rotangle += Math.PI * 2;
//            // on which turn are we here
//            BoundingRect ext2d = basisCurve2D.GetExtent();
//            int n = (int)Math.Round((pUnit.y - rotangle / (2 * Math.PI) * pitch - ext2d.GetCenter().y) / pitch); // number of turns to reache the point
//            double u = n * 2 * Math.PI + rotangle;
//            double v = basisCurve2D.PositionOf(new GeoPoint2D(Math.Sqrt(pUnit.z * pUnit.z + pUnit.x * pUnit.x), pUnit.y - (n + rotangle / (2 * Math.PI)) * pitch));
//#if DEBUG
//            double u1 = (n - 1) * 2 * Math.PI + rotangle;
//            double v1 = basisCurve2D.PositionOf(new GeoPoint2D(Math.Sqrt(pUnit.z * pUnit.z + pUnit.x * pUnit.x), pUnit.y - ((n - 1) + rotangle / (2 * Math.PI)) * pitch));
//            double u2 = n * 2 * Math.PI + rotangle;
//            double v2 = basisCurve2D.PositionOf(new GeoPoint2D(Math.Sqrt(pUnit.z * pUnit.z + pUnit.x * pUnit.x), pUnit.y - (n + rotangle / (2 * Math.PI)) * pitch));
//            double u3 = (n + 1) * 2 * Math.PI + rotangle;
//            double v3 = basisCurve2D.PositionOf(new GeoPoint2D(Math.Sqrt(pUnit.z * pUnit.z + pUnit.x * pUnit.x), pUnit.y - ((n + 1) + rotangle / (2 * Math.PI)) * pitch));

//            double error1 = p | PointAt(new GeoPoint2D(u1, v1));
//            double error2 = p | PointAt(new GeoPoint2D(u2, v2));
//            double error3 = p | PointAt(new GeoPoint2D(u3, v3));
//            double error4 = new GeoPoint2D(u, v) | base.PositionOf(p);
//            double error5 = p | PointAt(base.PositionOf(p));
//#endif
//            return new GeoPoint2D(u, v);
//        }
        /// <summary>
        /// Overrides <see cref="CADability.GeoObject.ISurfaceImpl.UDirection (GeoPoint2D)"/>
        /// </summary>
        /// <param name="uv"></param>
        /// <returns></returns>
        public override GeoVector UDirection(GeoPoint2D uv)
        {   // does not need the derivative of the curve
            GeoPoint2D p2d = basisCurve2D.PointAt(GetPos(uv.y));
            // the rotation contributes a vector proportional to the radius, the pitch a constant one along the axis.
            // So where the curve touches the axis, the u derivative is the pitch alone and only vanishes when the pitch is 0
            return toSurface * new GeoVector(-p2d.x * Math.Sin(uv.x), pitch / (Math.PI * 2.0), -p2d.x * Math.Cos(uv.x));
        }
        /// <summary>
        /// Overrides <see cref="CADability.GeoObject.ISurfaceImpl.VDirection (GeoPoint2D)"/>
        /// </summary>
        /// <param name="uv"></param>
        /// <returns></returns>
        public override GeoVector VDirection(GeoPoint2D uv)
        {
            // the curve is evaluated at GetPos(v), so the derivative of the curve by its position needs the factor
            // d(pos)/dv. The pitch only depends on u and does not contribute to the v derivative
            GeoVector2D dir = (1.0 / (curveEndParameter - curveStartParameter)) * basisCurve2D.DirectionAt(GetPos(uv.y));
            ModOp rot = ModOp.Rotate(1, (SweepAngle)uv.x);
            return toSurface * rot * dir;
        }
        /// <summary>
        /// Overrides <see cref="CADability.GeoObject.ISurfaceImpl.GetNormal (GeoPoint2D)"/>
        /// </summary>
        /// <param name="uv"></param>
        /// <returns></returns>
        public override GeoVector GetNormal(GeoPoint2D uv)
        {
            try
            {
                return UDirection(uv).Normalized ^ VDirection(uv).Normalized;
            }
            catch (GeoVectorException)
            {
                // vermutlixh ein Pol bei uv.y
                double pos = GetPos(uv.y);
                GeoPoint2D p2d = basisCurve2D.PointAt(pos);
                return VDirection(new GeoPoint2D(0.0, uv.y)).Normalized ^ VDirection(new GeoPoint2D(Math.PI / 2.0, uv.y)).Normalized;
            }
        }
        public override void Derivation2At(GeoPoint2D uv, out GeoPoint location, out GeoVector du, out GeoVector dv, out GeoVector duu, out GeoVector dvv, out GeoVector duv)
        {
            double pos = GetPos(uv.y);
            if (!basisCurve2D.TryPointDeriv2At(pos, out GeoPoint2D p2d, out GeoVector2D deriv1, out GeoVector2D deriv2))
            {   // the profile curve has no analytic second derivative: difference its first derivative
                const double h = 1e-6;
                p2d = basisCurve2D.PointAt(pos);
                deriv1 = basisCurve2D.DirectionAt(pos);
                deriv2 = (1.0 / (2 * h)) * (basisCurve2D.DirectionAt(pos + h) - basisCurve2D.DirectionAt(pos - h));
            }
            location = PointAt(uv);
            du = UDirection(uv);
            dv = VDirection(uv);
            // the curve point (x, y) is rotated to (x*cos(u), y, -x*sin(u)) and moved by pitch*u/(2*pi) along the y axis,
            // v enters through pos = GetPos(v)
            double dpos = 1.0 / (curveEndParameter - curveStartParameter);
            double sin = Math.Sin(uv.x);
            double cos = Math.Cos(uv.x);
            duu = toSurface * new GeoVector(-p2d.x * cos, 0.0, p2d.x * sin);
            dvv = (dpos * dpos) * (toSurface * new GeoVector(deriv2.x * cos, deriv2.y, -deriv2.x * sin));
            duv = dpos * (toSurface * new GeoVector(-deriv1.x * sin, 0.0, -deriv1.x * cos));
        }
        /// <summary>
        /// Overrides <see cref="CADability.GeoObject.ISurfaceImpl.GetZMinMax (Projection, double, double, double, double, ref double, ref double)"/>
        /// </summary>
        /// <param name="p"></param>
        /// <param name="umin"></param>
        /// <param name="umax"></param>
        /// <param name="vmin"></param>
        /// <param name="vmax"></param>
        /// <param name="zMin"></param>
        /// <param name="zMax"></param>
        public override void GetZMinMax(Projection p, double umin, double umax, double vmin, double vmax, ref double zMin, ref double zMax)
        {
            // hier wird brutal gerastert, das muss anders gehen!
            // nämlich so: 1. betrachte die 4 Eckpunkte
            // 2. finde u Werte für für das z-Maximum bzw z-Minimum der Rotation
            // 3. bestimme die Meridiankurve in diesen beiden Positionen und projiziere gemäß "p"
            // 4. finde die z-Extrema dieser projizierten Kurve
            for (int i = 0; i <= 10; ++i)
            {
                for (int j = 0; j <= 10; ++j)
                {
                    GeoPoint bp = p.UnscaledProjection * PointAt(new GeoPoint2D(umin + i * (umax - umin) / 10, vmin + j * (vmax - vmin) / 10));
                    zMin = Math.Min(zMin, bp.z);
                    zMax = Math.Max(zMax, bp.z);
                }
            }
        }
        /// <summary>
        /// Overrides <see cref="CADability.GeoObject.ISurfaceImpl.Clone ()"/>
        /// </summary>
        /// <returns></returns>
        public override ISurface Clone()
        {
            HelicalSurface res = new HelicalSurface(basisCurve2D.Clone(), pitch, toSurface, curveStartParameter, curveEndParameter, curveParameterOffset);
            res.usedArea = usedArea;
            return res;
        }
        /// <summary>
        /// Overrides <see cref="CADability.GeoObject.ISurfaceImpl.Modify (ModOp)"/>
        /// </summary>
        /// <param name="m"></param>
        public override void Modify(ModOp m)
        {
            boxedSurfaceEx = null;
            toSurface = m * toSurface;
            fromSurface = toSurface.GetInverse();
        }
        /// <summary>
        /// Overrides <see cref="CADability.GeoObject.ISurfaceImpl.GetModified (ModOp)"/>
        /// </summary>
        /// <param name="m"></param>
        /// <returns></returns>
        public override ISurface GetModified(ModOp m)
        {
            ISurface res = Clone();
            res.Modify(m);
            (res as ISurfaceImpl).usedArea = usedArea;
            return res;
        }
        /// <summary>
        /// Overrides <see cref="CADability.GeoObject.ISurfaceImpl.CopyData (ISurface)"/>
        /// </summary>
        /// <param name="CopyFrom"></param>
        public override void CopyData(ISurface CopyFrom)
        {
            HelicalSurface cc = CopyFrom as HelicalSurface;
            if (cc != null)
            {
                this.basisCurve2D = cc.basisCurve2D.Clone();
                this.pitch = cc.pitch;
                this.toSurface = cc.toSurface;
                this.fromSurface = cc.fromSurface;
                this.curveStartParameter = cc.curveStartParameter;
                this.curveEndParameter = cc.curveEndParameter;
            }
        }
        public override bool IsUPeriodic
        {
            get
            {
                return false;
            }
        }
        public override bool IsVPeriodic
        {
            get
            {
                return false;
            }
        }
        public override double UPeriod
        {
            get
            {
                return 0.0;
            }
        }
        public override double VPeriod
        {
            get
            {
                return 0.0;
            }
        }
        /// <summary>
        /// Overrides <see cref="CADability.GeoObject.ISurfaceImpl.FixedU (double, double, double)"/>
        /// </summary>
        /// <param name="u"></param>
        /// <param name="vmin"></param>
        /// <param name="vmax"></param>
        /// <returns></returns>
        public override ICurve FixedU(double u, double vmin, double vmax)
        {
            ICurve2D btr = basisCurve2D.Trim(GetPos(vmin), GetPos(vmax));
            ICurve b3d = btr.MakeGeoObject(Plane.XYPlane) as ICurve;
            ModOp rot = ModOp.Rotate(1, (SweepAngle)u);
            ModOp movePitch = ModOp.Translate(0, pitch * u / (Math.PI * 2.0), 0);
            return b3d.CloneModified(toSurface * movePitch * rot);
        }
        /// <summary>
        /// Overrides <see cref="CADability.GeoObject.ISurfaceImpl.FixedV (double, double, double)"/>
        /// </summary>
        /// <param name="v"></param>
        /// <param name="umin"></param>
        /// <param name="umax"></param>
        /// <returns></returns>
        public override ICurve FixedV(double v, double umin, double umax)
        {
            GeoPoint2D p2d = basisCurve2D.PointAt(GetPos(v));
            Line2D l2d = new Line2D(new GeoPoint2D(umin, v), new GeoPoint2D(umax, v));
            CurveOnSurface cos = CurveOnSurface.Construct(l2d, this);
            return cos;
            // making an Ellipse as an exact NURBS Ellipse but with the pitch doesn't match the desired curve
            // because the NURBS doesn't run linear with the angle as parameter (also graphically tested)
            //int n = Math.Max(3, (int)Math.Round(Math.Abs((umax - umin)) / Math.PI * 2 * 18));
            //double ustep = (umax - umin) / (n - 1);
            //GeoPoint[] pnts = new GeoPoint[n];
            //for (int i = 0; i < n; i++)
            //{
            //    pnts[i] = PointAt(new GeoPoint2D(umin + i * ustep, v));
            //}
            //BSpline res = BSpline.Construct();
            //res.ThroughPoints(pnts, 3, false);
            //return res as ICurve;
        }
        public override bool Oriented
        {
            get
            {
                return true;
            }
        }
        /// <summary>
        /// Overrides <see cref="CADability.GeoObject.ISurfaceImpl.Orientation (GeoPoint)"/>
        /// </summary>
        /// <param name="p"></param>
        /// <returns></returns>
        public override double Orientation(GeoPoint p)
        {   // soll vermutlich feststellen ob außerhalb oder innerhalb der Fläche im Bezug auf die Achse
            // Richards code verwendet das aufwendige GetLineIntersection.
            // Hier die bessere Version, die das Problem ins zweidimensionale projiziert:
            GeoPoint up = fromSurface * p;
            double x = Math.Sqrt(up.z * up.z + up.x * up.x);
            double y = up.y;
            GeoPoint2D toCeck = new GeoPoint2D(x, y);
            BoundingRect ext = basisCurve2D.GetExtent();
            GeoPoint2D sp, ep;
            if (ext.Left > 0)
            {
                sp = new GeoPoint2D(ext.Left, y);
                ep = new GeoPoint2D(ext.Right, y);
            }
            else
            {
                sp = new GeoPoint2D(ext.Right, y);
                ep = new GeoPoint2D(ext.Left, y);
            }
            GeoPoint2DWithParameter[] isp = basisCurve2D.Intersect(sp, ep);
            // muss die Kurve immer rechts von der Achse liegen? Durch sp und ep sid wir davon unabhängig, es sei denn die Kurve schneidet die Achse
            SortedList<double, GeoPoint2D> par = new SortedList<double, GeoPoint2D>();
            for (int i = 0; i < isp.Length; ++i)
            {
                if (isp[i].par2 >= 0.0 && isp[i].par2 <= 1.0 && isp[i].par1 >= 0.0 && isp[i].par1 <= 1.0)
                {   // nur wenn echter Schnitt
                    par[isp[i].par2] = isp[i].p; // nach Position auf der horizontalen Linie
                }
            }
            if (par.Count == 0) return 0.0; // Seite kann nicht bestimmt werden, da völlig außerhalb. Das Vorzeichen von 0.0
            // ist aber verschieden von +1 und von -1, so dass wenn es andere konkrete Punkte gibt also ein Test gemacht wird
            double ppar = Geometry.LinePar(sp, ep, toCeck);
            for (int i = 0; i < par.Count; ++i)
            {
                if (ppar < par.Keys[i])
                {
                    if ((i & 1) == 0) // gerade, also innerhalb
                        return -(toCeck | par.Values[i]); // innerhalb ist negativ
                    else
                        return (toCeck | par.Values[i]); // außerhalb positiv
                }
            }
            // größer als alle
            if ((par.Count & 1) == 1) // ungerade, also letzer Punkt innerhalb
                return (toCeck | par.Values[par.Count - 1]); // außerhalb positiv
            else
                return -(toCeck | par.Values[par.Count - 1]); // innerhalb ist negativ


            // Richards code:
            //GeoPoint gp = fromSurface * p;
            //double d = gp.y;
            //double e = Math.Abs(d);
            //GeoPoint ac = new GeoPoint(0, d, 0);
            //GeoPoint2D[] sp = GetLineIntersection(p, toSurface * (ac - gp));
            //int n = 0;
            //for (int i = 0; i < sp.Length; ++i)
            //{
            //    double ds = (sp[i] - GeoPoint2D.Origin).Length - e;
            //    if (Math.Abs(ds) > Precision.eps)
            //    {
            //        if (ds < 0)
            //            n++;
            //    }
            //    else return 0;
            //}
            //if (n % 4 == 2)
            //    return (-e);
            //else
            //    return e;
        }
        /// <summary>
        /// Overrides <see cref="CADability.GeoObject.ISurfaceImpl.ReverseOrientation ()"/>
        /// </summary>
        /// <returns></returns>
        public override ModOp2D ReverseOrientation()
        {   // umkehrung der Y-Richtung
            boxedSurfaceEx = null;
            //double tmp = curveStartParameter;
            //curveStartParameter = curveEndParameter;
            //curveEndParameter = tmp;
            basisCurve2D.Reverse(); // damit Helper das richtige erzeugt
            //pitch = -pitch; // ??
            // die Frage ist natürlich: was macht reverse mit dem v-Parameter
            // bei BSpline bleiben sie erhalten
            // die basisCurve2D wird immer mit 0..1 angesprochen, insofern muss der v-Bereich umgeklappt werden
            // curveParameterOffset scheint nur von Bedeutung, wenn das Objekt gerade erzeugt wird, sonst wohl immer 0.0
            return new ModOp2D(1.0, 0.0, 0.0, 0.0, -1.0, curveStartParameter + curveEndParameter);
        }
        /// <summary>
        /// Overrides <see cref="CADability.GeoObject.ISurfaceImpl.GetSafeParameterSteps (double, double, double, double, out double[], out double[])"/>
        /// </summary>
        /// <param name="umin"></param>
        /// <param name="umax"></param>
        /// <param name="vmin"></param>
        /// <param name="vmax"></param>
        /// <param name="intu"></param>
        /// <param name="intv"></param>
        public override void GetSafeParameterSteps(double umin, double umax, double vmin, double vmax, out double[] intu, out double[] intv)
        {   // leider gibt es die entsprechende Methode für die 2D Kurve nicht, erstmal so probieren

            if (basisCurve2D is GeneralCurve2D)
            {
                GeoPoint2D[] pnts;
                (basisCurve2D as GeneralCurve2D).GetTriangulationPoints(out pnts, out intv);
                List<double> usteps = new List<double>();
                List<double> vsteps = new List<double>();
                vsteps.Add(vmin);
                vsteps.Add(vmax);
                for (int i = 0; i < intv.Length; ++i)
                {

                    double d = curveStartParameter + intv[i] * (curveEndParameter - curveStartParameter);
                    if (d > vmin && d < vmax) vsteps.Add(d);
                }
                vsteps.Sort();
                for (int i = vsteps.Count - 1; i > 0; --i)
                {
                    if (vsteps[i] - vsteps[i - 1] < (curveEndParameter - curveStartParameter) * 1e-5)
                    {
                        if (i == 1) vsteps.RemoveAt(1);
                        else vsteps.RemoveAt(i - 1);
                    }
                }
                if (vsteps.Count==1)
                {
                    vsteps.Add(vsteps[0] + 0.5);
                    vsteps.Insert(0, vsteps[0] - 0.5);
                }
                double udiff = umax - umin;
                int n = (int)Math.Ceiling(udiff / (Math.PI / 2.0)) + 1;
                double du = udiff / n;
                for (int i = 0; i < n; i++)
                {
                    usteps.Add(umin + i * du);
                }
                usteps.Add(umax);
                intu = usteps.ToArray();
                intv = vsteps.ToArray();
                return;
            }
            base.GetSafeParameterSteps(0, 2 * Math.PI, curveStartParameter, curveEndParameter, out intu, out intv);
        }
        /// <summary>
        /// Overrides <see cref="CADability.GeoObject.ISurfaceImpl.SameGeometry (BoundingRect, ISurface, BoundingRect, double, out ModOp2D)"/>
        /// </summary>
        /// <param name="thisBounds"></param>
        /// <param name="other"></param>
        /// <param name="otherBounds"></param>
        /// <param name="precision"></param>
        /// <param name="firstToSecond"></param>
        /// <returns></returns>
        public override bool SameGeometry(BoundingRect thisBounds, ISurface other, BoundingRect otherBounds, double precision, out ModOp2D firstToSecond)
        {
            if (other is HelicalSurface)
            {
                firstToSecond = ModOp2D.Null;
                HelicalSurface srother = other as HelicalSurface;
                bool reverse;
                if (!Curves.SameGeometry(BasisCurve, srother.BasisCurve, precision, out reverse)) return false;
                if ((Location | srother.Location) > precision) return false;
                if (Math.Abs(pitch - srother.pitch) > precision) return false;
                GeoPoint2D uv1 = new GeoPoint2D(curveStartParameter, 0.0);
                GeoPoint p1 = PointAt(uv1);
                GeoPoint2D uv2 = srother.PositionOf(p1);
                GeoPoint p2 = srother.PointAt(uv2);
                if ((p1 | p2) > precision) return false;
                uv1 = new GeoPoint2D(curveEndParameter, 0.0);
                p1 = PointAt(uv1);
                uv2 = srother.PositionOf(p1);
                p2 = srother.PointAt(uv2);
                if ((p1 | p2) > precision) return false;
                firstToSecond = ModOp2D.Translate(uv2 - uv1);
                return true;
            }
            return base.SameGeometry(thisBounds, other, otherBounds, precision, out firstToSecond);
        }
        public override ISurface GetOffsetSurface(double offset)
        {
            return base.GetOffsetSurface(offset);
        }
        public override void GetNaturalBounds(out double umin, out double umax, out double vmin, out double vmax)
        {
            umin = double.MinValue;
            umax = double.MaxValue;
            vmin = curveStartParameter;
            vmax = curveEndParameter;
        }
        #endregion
        public override IPropertyEntry GetPropertyEntry(IFrame frame)
        {
            return new GroupProperty("HelicalSurface", new IPropertyEntry[0]);
        }
        #region ISerializable Members
        protected HelicalSurface(SerializationInfo info, StreamingContext context) : base()
        {
            basisCurve2D = info.GetValue("BasisCurve2D", typeof(ICurve2D)) as ICurve2D;
            pitch = (double)info.GetValue("Pitch", typeof(double));
            toSurface = (ModOp)info.GetValue("ToSurface", typeof(ModOp));
            fromSurface = toSurface.GetInverse(); // müsste eigentlich da sein, da nur struct
            curveStartParameter = (double)info.GetValue("CurveStartParameter", typeof(double));
            curveEndParameter = (double)info.GetValue("CurveEndParameter", typeof(double));
            try
            {
                curveParameterOffset = (double)info.GetValue("CurveParameterOffset", typeof(double));
            }
            catch (SerializationException)
            {
                curveParameterOffset = 0;
            }
        }
        void ISerializable.GetObjectData(SerializationInfo info, StreamingContext context)
        {
            info.AddValue("BasisCurve2D", basisCurve2D, typeof(ICurve2D));
            info.AddValue("Pitch", pitch, typeof(double));
            info.AddValue("ToSurface", toSurface, typeof(ModOp));
            info.AddValue("CurveStartParameter", curveStartParameter, typeof(double));
            info.AddValue("CurveEndParameter", curveEndParameter, typeof(double));
            info.AddValue("CurveParameterOffset", curveParameterOffset, typeof(double));
        }
        #endregion
        public BoundingRect GetMaximumExtent()
        {
            return new BoundingRect(0, curveStartParameter, Math.PI * 2.0, curveEndParameter);
        }
#if DEBUG
        public override Face DebugAsFace
        {
            get
            {
                BoundingRect ext = GetMaximumExtent();
                return Face.MakeFace(this, new CADability.Shapes.SimpleShape(ext));
            }
        }
#endif
    }
}
