using CADability.Curve2D;
using CADability.GeoObject;
using System;
using System.Runtime.Serialization;

namespace CADability
{
    /// <summary>
    /// A 2d curve as a projection of a 3d curve onto a surface. Sometimes it is easier to calculate points in 3d than in 2d. Then we use this as a more
    /// exact for of the curve than we get, when we approximate the curve in 2d.
    /// <para>
    /// On a periodic surface the uv values of a point are only defined up to whole periods. The curve runs on
    /// continuously from an anchor: the uv value it has at one parameter of the 3d curve, fixed when the curve is made.
    /// Every other point is PositionOf of its 3d point, moved by whole periods next to its neighbours on the curve.
    /// The anchor is absolute, so it stays valid when the domain of the surface is set or changed later, which changes
    /// what PositionOf returns. <see cref="Move"/> moves the anchor.
    /// </para>
    /// <para>
    /// It is also the 2d curve of an <see cref="InterpolatedDualSurfaceCurve"/> on one of its two surfaces, see
    /// <see cref="IsCurveOfIntersection"/>. Such a curve has no anchor of its own: it lies in the periods of the uv values
    /// its 3d curve stores, moved by the whole periods <see cref="Move"/> added.
    /// </para>
    /// </summary>
    [Serializable()]
    public class ProjectedCurve : GeneralCurve2D, ISerializable, IJsonSerialize
    {
        private double startParam; // start parameter on the 3d curve, together wit endParam also specifies the orientation
        private double endParam; // on the 3d curve
        ICurve curve3D;
        ISurface surfaceOfOtherCurves; // see surface
        /// <summary>
        /// The surface this curve lies on. A curve of an intersection takes it from its <see cref="InterpolatedDualSurfaceCurve"/>
        /// every time: the surfaces of that curve are replaced by clones or by geometrically identical ones, and a face
        /// reparametrizes its surface in place, see <see cref="Reparametrized"/>.
        /// </summary>
        private ISurface surface
        {
            get
            {
                if (surfaceOfIntersection != 0 && curve3D is InterpolatedDualSurfaceCurve idsc) return surfaceOfIntersection == 1 ? idsc.Surface1 : idsc.Surface2;
                return surfaceOfOtherCurves;
            }
            set => surfaceOfOtherCurves = value;
        }
        GeoPoint2D anchorUv; // the uv value of this curve at the parameter anchor3d of the 3d curve
        double anchor3d;
        bool hasAnchor; // false only for a curve read from a file written before the anchor existed
        BoundingRect windowOfAnOlderFile; // such a file has the domain the curve used, it gives the anchor on first use
        /// <summary>
        /// 1 or 2 for a curve an <see cref="InterpolatedDualSurfaceCurve"/> made for its first or its second surface, see
        /// <see cref="IsCurveOfIntersection"/>, 0 for the other projected curves.
        /// </summary>
        int surfaceOfIntersection;
        private bool ofIntersection => surfaceOfIntersection != 0;
        /// <summary>
        /// For a curve of an intersection: the whole periods it lies beyond the uv values its <see cref="InterpolatedDualSurfaceCurve"/>
        /// stores, added by <see cref="Move"/>. Those uv values belong to the 3d curve, which keeps them up to date when a
        /// surface is replaced or reparametrized, and the 2d curves on that surface follow them, see <see cref="Reparametrized"/>.
        /// </summary>
        GeoVector2D periodsBeyondStoredUv;
        GeoPoint2D? startPointSet, endPointSet; // see ExactEnd
        /// <summary>The number of points of the chain which carries the anchor along the curve, see <see cref="ContinuousChain"/>.</summary>
        private const int chainLength = 17;
#if DEBUG
        static int debugCounter = 0;
        private int debugCount; // to identify instance when debugging
#endif
        private BSpline2D approxBSpline2D = null;
        public BSpline2D ApproxBSpline2D
        {
            get
            {
                if (approxBSpline2D == null)
                {
                    double prec = UvPrecision();
                    if (ofIntersection) approxBSpline2D = ApproximateCurveOfIntersection(prec);
                    else
                    {
                        GeoPoint2D[] chain = AnchoredChain();
                        approxBSpline2D = BSpline2D.Approximate(pos => ContinuousUv(pos, chain), prec);
                    }
                }
                return approxBSpline2D;
            }
        }
        /// <summary>
        /// How close the approximation has to follow the curve, in uv. The approximation measures its error in the
        /// parameter plane, while the precision which matters is a distance in space: on a big cylinder one radian is a
        /// long way, on a small one it is not. So the precision in space - a fraction of the length of the curve, at
        /// least Precision.eps - is divided by how far a step in uv moves a point on the surface.
        /// <para>
        /// Both kinds of projected curve use this since stage 7. Before, a curve of an intersection was approximated to
        /// Precision.eps in uv and every other one to a fraction of the length of its 3d curve, also in uv, which is a
        /// length in space and does not belong there.
        /// </para>
        /// </summary>
        private double UvPrecision()
        {
            double tolerance3d = Math.Max(curve3D.Length * 1e-6, Precision.eps);
            double scale = 0.0;
            for (int i = 0; i <= 4; i++)
            {
                GeoPoint2D uv = surface.PositionOf(curve3D.PointAt(Get3dParameter(i / 4.0)));
                scale = Math.Max(scale, Math.Max(surface.UDirection(uv).Length, surface.VDirection(uv).Length));
            }
            return scale > Precision.eps ? tolerance3d / scale : tolerance3d;
        }
        /// <summary>
        /// The approximation of a curve of an intersection to <paramref name="precision"/>: the uv values of its points in
        /// the periods of the uv values the 3d curve stores, moved by <see cref="periodsBeyondStoredUv"/>. The whole curve
        /// is approximated in the direction of the 3d curve and reversed, if need be, as the class
        /// InterpolatedDualSurfaceCurve.ProjectedCurve did.
        /// </summary>
        private BSpline2D ApproximateCurveOfIntersection(double precision)
        {
            InterpolatedDualSurfaceCurve idsc = curve3D as InterpolatedDualSurfaceCurve;
            bool onSurface1 = IsOnSurface1;
            GeoVector2D periods = periodsBeyondStoredUv;
            Func<double, GeoPoint2D> uv = position => idsc.UvInStoredPeriods(onSurface1, position) + periods;
            if (startParam == 0.0 && endParam == 1.0) return BSpline2D.Approximate(uv, precision, 0, 1);
            if (startParam == 1.0 && endParam == 0.0)
            {
                BSpline2D res = BSpline2D.Approximate(uv, precision, 0, 1);
                res.Reverse();
                return res;
            }
            return BSpline2D.Approximate(pos => uv(Get3dParameter(pos)), precision);
        }
        /// <summary>
        /// The projection of <paramref name="curve3D"/> onto <paramref name="surface"/>. On a periodic surface, where the curve
        /// may lie in any period, <paramref name="domain"/> tells which: the curve is moved by whole periods as close as
        /// possible to it. An empty domain leaves it where PositionOf puts its start, in the domain of the surface.
        /// </summary>
        public ProjectedCurve(ICurve curve3D, ISurface surface, bool forward, BoundingRect domain)
        {
#if DEBUG
            debugCount = debugCounter++;
#endif
            this.curve3D = curve3D; // keep in mind, the curve is not cloned, curve3D should not be modified after this
            this.surface = surface;
            if (forward)
            {
                startParam = 0.0;
                endParam = 1.0;
            }
            else
            {
                startParam = 1.0;
                endParam = 0.0;
            }
            SetAnchor(domain);
#if DEBUG
            this.MakeTriangulation();
#endif
        }
        /// <summary>A copy or a part of another projected curve, with an anchor taken from it, so that both lie in the same periods.</summary>
        private ProjectedCurve(ICurve curve3D, ISurface surface, double startParam, double endParam, double anchor3d, GeoPoint2D anchorUv, ProjectedCurve kindOf)
        {
#if DEBUG
            debugCount = debugCounter++;
#endif
            this.curve3D = curve3D;
            this.surface = surface;
            this.startParam = startParam;
            this.endParam = endParam;
            this.anchor3d = anchor3d;
            this.anchorUv = anchorUv;
            surfaceOfIntersection = kindOf.surfaceOfIntersection;
            periodsBeyondStoredUv = kindOf.periodsBeyondStoredUv;
            hasAnchor = true;
        }
        /// <summary>
        /// The curve of <paramref name="curve3d"/> on its first or on its second surface, running backwards when
        /// <paramref name="reverse"/>, in the periods of the uv values <paramref name="curve3d"/> stores.
        /// </summary>
        internal ProjectedCurve(InterpolatedDualSurfaceCurve curve3d, bool onSurface1, bool reverse = false)
        {
#if DEBUG
            debugCount = debugCounter++;
#endif
            curve3D = curve3d;
            surfaceOfIntersection = onSurface1 ? 1 : 2;
            startParam = reverse ? 1.0 : 0.0;
            endParam = reverse ? 0.0 : 1.0;
            hasAnchor = true; // there is none, see periodsBeyondStoredUv
        }
        /// <summary>Needed to read a file, see <see cref="SetObjectData"/>.</summary>
        protected ProjectedCurve()
        {
#if DEBUG
            debugCount = debugCounter++;
#endif
        }
        /// <summary>
        /// A curve of an intersection as files written before hold it, when it was the class
        /// InterpolatedDualSurfaceCurve.ProjectedCurve: the 3d curve, which of its two surfaces, reversed or not, and the
        /// periods it was moved by. The 3d curve may not be complete while it is read, the surface is taken from it on use.
        /// </summary>
        private void InitFromOlderFile(InterpolatedDualSurfaceCurve curve3d, bool onSurface1, bool reversed, GeoVector2D offset)
        {
            curve3D = curve3d;
            surfaceOfIntersection = onSurface1 ? 1 : 2;
            startParam = reversed ? 1.0 : 0.0;
            endParam = reversed ? 0.0 : 1.0;
            periodsBeyondStoredUv = offset;
            hasAnchor = true; // there is none
        }
        /// <summary>
        /// True for the 2d curve an <see cref="InterpolatedDualSurfaceCurve"/> makes for one of its own two surfaces. Such
        /// a curve lies in the periods of the uv values its 3d curve stores, and it takes its area, extent, sweep,
        /// directions and derivatives from its approximation, as the class InterpolatedDualSurfaceCurve.ProjectedCurve
        /// did, which it replaces. The other projected curves take those from GeneralCurve2D, which measures them on the
        /// arcs through the triangulation. The precision of the approximation and the length are the same for both since
        /// stage 7, see <see cref="UvPrecision"/>.
        /// <para>
        /// Both ways of measuring the length were held against the curve itself, densely sampled: the arc length of the
        /// approximation is exact to better than 1e-7, the arcs are off by up to 2e-6. The more accurate one is not used
        /// because it splits one of the two mirror symmetric fillets of ShellOffsetTests and not the other - the two
        /// differ by 2e-6 and a threshold in SweptCircleSurface.OuterShell decides between them. That threshold is what
        /// has to be looked at before the length can follow the approximation as well.
        /// </para>
        /// </summary>
        internal bool IsCurveOfIntersection => ofIntersection;
        /// <summary>
        /// A projected curve which is not the curve of an intersection, see <see cref="IsCurveOfIntersection"/>. The places
        /// which ask this make a 2d curve anew from the surface and the 3d curve. That is right for a projected curve, but
        /// not for the curve of an intersection: that one is made by its <see cref="InterpolatedDualSurfaceCurve"/>, which
        /// keeps the curves on both of its surfaces in step.
        /// </summary>
        internal static bool IsPlain(ICurve2D curve) => curve is ProjectedCurve pc && !pc.ofIntersection;
        /// <summary>The <see cref="InterpolatedDualSurfaceCurve"/> this is a curve of, null for the other projected curves.</summary>
        internal InterpolatedDualSurfaceCurve IntersectionCurve => ofIntersection ? curve3D as InterpolatedDualSurfaceCurve : null;
        /// <summary>Whether this curve of an intersection is on the first of its two surfaces.</summary>
        internal bool IsOnSurface1 => surfaceOfIntersection == 1;
        /// <summary>
        /// Replaces the 3d curve by <paramref name="c3d"/>, which is geometrically identical to the piece of the 3d curve
        /// this curve runs along, e.g. the trimmed copy made for an edge. This curve keeps its direction and its periods.
        /// </summary>
        internal void SetCurve3D(ICurve c3d)
        {
            EnsureAnchor();
            GeoPoint sp = curve3D.PointAt(startParam), ep = curve3D.PointAt(endParam);
            bool sameDirection = (c3d.StartPoint | sp) + (c3d.EndPoint | ep) <= (c3d.StartPoint | ep) + (c3d.EndPoint | sp);
            if (!ofIntersection) anchor3d = Math.Max(0.0, Math.Min(1.0, c3d.PositionOf(curve3D.PointAt(anchor3d))));
            GeoPoint2D startBefore = ofIntersection ? StartPoint : GeoPoint2D.Origin;
            startParam = sameDirection ? 0.0 : 1.0;
            endParam = sameDirection ? 1.0 : 0.0;
            curve3D = c3d;
            startPointSet = endPointSet = null; // they were set for the old 3d curve
            InvalidateSecondaryData();
            if (ofIntersection)
            {   // The new 3d curve, typically a trimmed copy of the old one, stores its uv values in periods of its own: a piece
                // which runs over the start of a closed curve gets them one period away. This curve must stay where it was, so the
                // periods it is moved by are adjusted to the new 3d curve.
                GeoPoint2D startAfter = StartPoint;
                double dx = 0.0, dy = 0.0;
                if (surface.IsUPeriodic) dx = Math.Round((startBefore.x - startAfter.x) / surface.UPeriod) * surface.UPeriod;
                if (surface.IsVPeriodic) dy = Math.Round((startBefore.y - startAfter.y) / surface.VPeriod) * surface.VPeriod;
                if (dx != 0.0 || dy != 0.0)
                {
                    periodsBeyondStoredUv = periodsBeyondStoredUv + new GeoVector2D(dx, dy);
                    InvalidateSecondaryData();
                }
            }
        }
        /// <summary>
        /// The curve on the same surface along <paramref name="trimmed"/>, a trimmed copy of the 3d curve of this curve of an
        /// intersection, in the same direction and moved by the same periods as this curve.
        /// </summary>
        internal ProjectedCurve OnTrimmedCurve(InterpolatedDualSurfaceCurve trimmed)
        {
            ProjectedCurve res = new ProjectedCurve(trimmed, IsOnSurface1, IsReverse);
            res.periodsBeyondStoredUv = periodsBeyondStoredUv;
            return res;
        }
        /// <summary>
        /// This curve of an intersection after its surface has been reparametrized in place by <paramref name="m"/>, as
        /// <see cref="ISurface.ReverseOrientation"/> does: the 3d curve converts the uv values it stores for that surface,
        /// and the new curve lies in their periods, moved by the converted periods of this curve. For
        /// <see cref="Edge.ModifyCurve2D"/>, which gets the new parametrization of the surface of a face.
        /// </summary>
        internal ProjectedCurve Reparametrized(ModOp2D m)
        {
            IntersectionCurve.SurfaceReparametrized(IsOnSurface1, m);
            ProjectedCurve res = new ProjectedCurve(curve3D, surface, startParam, endParam, anchor3d, anchorUv, this);
            res.periodsBeyondStoredUv = m * periodsBeyondStoredUv; // the linear part
            if (startPointSet.HasValue) res.startPointSet = m * startPointSet.Value;
            if (endPointSet.HasValue) res.endPointSet = m * endPointSet.Value;
            res.UserData.CloneFrom(UserData);
            return res;
        }
        /// <summary>
        /// Replaces a surface by a geometrically identical one in the 3d curve of an intersection; this curve takes its
        /// surface from there.
        /// </summary>
        internal void ReplaceSurface(ISurface oldSurface, ISurface newSurface)
        {
            if (curve3D is InterpolatedDualSurfaceCurve idsc) idsc.ReplaceSurface(oldSurface, newSurface);
        }
        /// <summary>
        /// Fixes the anchor: at the first point of the chain which is not at a pole, with the whole chain moved by periods
        /// as close as possible to <paramref name="window"/>, as SurfaceHelper.UnwrapPeriodic does.
        /// </summary>
        private void SetAnchor(BoundingRect window)
        {
            GeoPoint2D[] chain = ContinuousChain(out int first);
            first = Math.Max(first, 0);
            anchor3d = Get3dParameter(first / (double)(chain.Length - 1));
            anchorUv = chain[first] + WindowShift(chain, window);
            hasAnchor = true;
        }
        private void EnsureAnchor()
        {
            if (!hasAnchor) SetAnchor(windowOfAnOlderFile);
        }
        /// <summary>
        /// The uv values of the curve at <see cref="chainLength"/> parameters: from PositionOf, which honours the domain of
        /// the surface, each moved by whole periods next to its predecessor - the curve as a continuous row in the
        /// parameter plane. A point at a pole, where the periodic parameter may take any value, takes the value of its
        /// neighbour and does not anchor the next one. <paramref name="first"/> is the first point which is not at a pole,
        /// -1 when there is none.
        /// </summary>
        private GeoPoint2D[] ContinuousChain(out int first)
        {
            GeoPoint2D[] res = new GeoPoint2D[chainLength];
            bool[] pole = new bool[chainLength];
            for (int i = 0; i < chainLength; i++)
            {
                res[i] = surface.PositionOf(curve3D.PointAt(Get3dParameter(i / (double)(chainLength - 1))));
                pole[i] = IsAtPole(res[i]);
            }
            first = Array.IndexOf(pole, false);
            if (first < 0) return res; // nothing but a pole
            for (int i = first + 1; i < chainLength; i++)
            {
                if (pole[i]) res[i] = res[i - 1];
                else MoveNextTo(ref res[i], res[i - 1]);
            }
            for (int i = first - 1; i >= 0; i--) res[i] = res[i + 1];
            return res;
        }
        /// <summary>The <see cref="ContinuousChain"/> moved by whole periods to the anchor.</summary>
        private GeoPoint2D[] AnchoredChain()
        {
            EnsureAnchor();
            GeoPoint2D[] chain = ContinuousChain(out _);
            int k = Math.Max(0, Math.Min(chain.Length - 1, (int)Math.Round(Get2dParameter(anchor3d) * (chain.Length - 1))));
            GeoPoint2D anchored = chain[k];
            MoveNextTo(ref anchored, anchorUv);
            GeoVector2D shift = anchored - chain[k];
            if (shift.x != 0.0 || shift.y != 0.0)
            {
                for (int i = 0; i < chain.Length; i++) chain[i] += shift;
            }
            return chain;
        }
        /// <summary>At a pole a periodic parameter may take any value without moving the point.</summary>
        private bool IsAtPole(GeoPoint2D uv)
        {
            GeoPoint p = surface.PointAt(uv);
            if (surface.IsUPeriodic && (surface.PointAt(new GeoPoint2D(uv.x + surface.UPeriod / 4, uv.y)) | p) < Precision.eps) return true;
            if (surface.IsVPeriodic && (surface.PointAt(new GeoPoint2D(uv.x, uv.y + surface.VPeriod / 4)) | p) < Precision.eps) return true;
            return false;
        }
        /// <summary>Moves <paramref name="uv"/> by whole periods next to <paramref name="next"/>.</summary>
        private void MoveNextTo(ref GeoPoint2D uv, GeoPoint2D next)
        {
            if (surface.IsUPeriodic && surface.UPeriod > 0.0) uv.x -= Math.Round((uv.x - next.x) / surface.UPeriod) * surface.UPeriod;
            if (surface.IsVPeriodic && surface.VPeriod > 0.0) uv.y -= Math.Round((uv.y - next.y) / surface.VPeriod) * surface.VPeriod;
        }
        /// <summary>The uv value at the 2d parameter <paramref name="pos"/>, continuous with the anchored <paramref name="chain"/>.</summary>
        private GeoPoint2D ContinuousUv(double pos, GeoPoint2D[] chain)
        {
            // Get3dParameter also reflects orientation
            GeoPoint2D uv = surface.PositionOf(curve3D.PointAt(Get3dParameter(pos)));
            int k = Math.Max(0, Math.Min(chain.Length - 1, (int)Math.Round(pos * (chain.Length - 1))));
            MoveNextTo(ref uv, chain[k]);
            return uv;
        }
        /// <summary>
        /// The whole periods which move the <paramref name="chain"/> as close as possible to <paramref name="window"/>: the
        /// center of its extent next to the center of the window. Nothing for an empty or unbounded window.
        /// </summary>
        private GeoVector2D WindowShift(GeoPoint2D[] chain, BoundingRect window)
        {
            if (window.IsEmpty()) return GeoVector2D.NullVector;
            BoundingRect ext = BoundingRect.EmptyBoundingRect;
            foreach (GeoPoint2D uv in chain) ext.MinMax(uv);
            double du = 0.0, dv = 0.0;
            if (surface.IsUPeriodic && surface.UPeriod > 0.0 && window.Left > double.MinValue && window.Right < double.MaxValue)
                du = -Math.Round(((ext.Left + ext.Right) / 2 - (window.Left + window.Right) / 2) / surface.UPeriod) * surface.UPeriod;
            if (surface.IsVPeriodic && surface.VPeriod > 0.0 && window.Bottom > double.MinValue && window.Top < double.MaxValue)
                dv = -Math.Round(((ext.Bottom + ext.Top) / 2 - (window.Bottom + window.Top) / 2) / surface.VPeriod) * surface.VPeriod;
            return new GeoVector2D(du, dv);
        }
        internal override void GetTriangulationPoints(out GeoPoint2D[] interpol, out double[] interparam)
        {
            if (ofIntersection) base.GetTriangulationPoints(out interpol, out interparam);
            else ApproxBSpline2D.GetTriangulationPoints(out interpol, out interparam);
        }
        protected override void GetTriangulationBasis(out GeoPoint2D[] points, out GeoVector2D[] directions, out double[] parameters)
        {
            if (!ofIntersection)
            {
                base.GetTriangulationBasis(out points, out directions, out parameters);
                return;
            }
            // a couple of points, which may miss some inflection points
            int n = 12;
            parameters = new double[n + 1];
            points = new GeoPoint2D[n + 1];
            directions = new GeoVector2D[n + 1];
            for (int i = 0; i < n + 1; i++)
            {
                parameters[i] = i / (double)n;
                points[i] = PointAt(parameters[i]);
                directions[i] = DirectionAt(parameters[i]);
            }
        }
        public override double[] GetInflectionPoints()
        {
            if (ofIntersection) return base.GetInflectionPoints();
            return ApproxBSpline2D.GetInflectionPoints();
        }
        public override double GetArea() => ofIntersection ? ApproxBSpline2D.GetArea() : base.GetArea();
        public override double GetAreaFromPoint(GeoPoint2D p) => ofIntersection ? ApproxBSpline2D.GetAreaFromPoint(p) : base.GetAreaFromPoint(p);
        public override BoundingRect GetExtent() => ofIntersection ? ApproxBSpline2D.GetExtent() : base.GetExtent();
        public override double Sweep => ofIntersection ? ApproxBSpline2D.Sweep : base.Sweep;
        public override GeoVector2D StartDirection => ofIntersection ? DirectionAt(0.0) : base.StartDirection;
        public override GeoVector2D EndDirection => ofIntersection ? DirectionAt(1.0) : base.EndDirection;
        public override bool IsClosed => !ofIntersection && base.IsClosed; // a curve of an intersection never was
#if DEBUG
        public void DebugTest()
        {
            GeoPoint2D[] points;
            GeoVector2D[] directions;
            double[] parameters;
            GetTriangulationBasis(out points, out directions, out parameters);
            GeoPoint2D sp = this.StartPoint;
            GeoPoint2D ep = this.EndPoint;
        }
#endif
        public ICurve Curve3DFromParams
        {
            get
            {
                ICurve res;
                res = curve3D.Clone();
                if (IsReverse)
                {
                    res.Reverse();
                    if (startParam == 0.0 && endParam == 1.0) return res;
                    else
                    {
                        res.Trim(1 - startParam, 1 - endParam);
                        return res;
                    }
                }
                else
                {
                    if (startParam == 0.0 && endParam == 1.0) return res;
                    else
                    {
                        res.Trim(startParam, endParam);
                        return res;
                    }
                }
            }
        }
        public ICurve Curve3D
        {
            get
            {
                return curve3D;
            }
        }
        public ISurface Surface
        {
            get
            {
                return surface;
            }
        }
        public bool IsReverse
        {
            get => endParam < startParam;
            internal set
            {
                if (value != IsReverse)
                {
                    double tmp = startParam;
                    startParam = endParam;
                    endParam = tmp;
                    InvalidateSecondaryData();
                }
            }
        }
        #region ICurve2D Members
        private double Get3dParameter(double par)
        {
            return startParam + par * (endParam - startParam);
        }
        private double Get2dParameter(double pos)
        {
            return (pos - startParam) / (endParam - startParam);
        }
        private void PointDirAt(double pos, out GeoPoint2D uv, out GeoVector2D dir)
        {
            ApproxBSpline2D.PointDirAt(pos, out uv, out dir); // ApproxBSpline2D is always parametrized from 0 to 1
        }

        internal void ReflectModification(ISurface surface, ICurve curve3d)
        {   // a face has been modified, in 2d there are no changes, but the surface and the 3d curve must be adopted
            this.surface = surface;
            this.curve3D = curve3d;
        }

        public override GeoVector2D DirectionAt(double Position)
        {
            if (ofIntersection) return ApproxBSpline2D.DirectionAt(Position); // the derivative, not normalized
            GeoPoint2D loc;
            GeoVector2D res;
            PointDirAt(Position, out loc, out res);
            return res;
        }
        public override GeoPoint2D PointAt(double Position)
        {
            return ApproxBSpline2D.PointAt(Position); // it is synchronous with parameters too!
        }
        /// <summary>
        /// The position of <paramref name="p"/> on this curve, found on the 3d curve: on a periodic surface a point may
        /// be given in another period than the one this curve runs through, e.g. a point in the domain of a face when
        /// the curve crosses the seam of that domain.
        /// </summary>
        public override double PositionOf(GeoPoint2D p)
        {
            return Get2dParameter(curve3D.PositionOf(surface.PointAt(p)));
        }
        /// <summary>
        /// The uv value at the start or at the end of a curve of an intersection which runs along the whole 3d curve: the value
        /// the 3d curve stores there, where the two surfaces meet exactly, moved by the periods of this curve. The end of the
        /// approximation is PositionOf of that point, which may differ a little, and the curves of the edges of a face must
        /// meet at the uv values of their vertices. A value set from outside - <see cref="Border"/> closes small gaps this
        /// way - is kept by this curve and does not change the 3d curve, which the other curves share. Null for the other
        /// projected curves and for a part of the 3d curve, whose ends are the ends of the approximation.
        /// </summary>
        private GeoPoint2D? ExactEnd(bool atEnd)
        {
            GeoPoint2D? set = atEnd ? endPointSet : startPointSet;
            if (set.HasValue) return set;
            if (!ofIntersection || Math.Min(startParam, endParam) != 0.0 || Math.Max(startParam, endParam) != 1.0) return null;
            return IntersectionCurve.StoredUvAtEnd(IsOnSurface1, atEnd ^ IsReverse) + periodsBeyondStoredUv;
        }
        public override GeoPoint2D StartPoint
        {
            get => ExactEnd(false) ?? base.StartPoint;
            set
            {
                if (ofIntersection) startPointSet = value;
                base.StartPoint = value;
            }
        }
        public override GeoPoint2D EndPoint
        {
            get => ExactEnd(true) ?? base.EndPoint;
            set
            {
                if (ofIntersection) endPointSet = value;
                base.EndPoint = value;
            }
        }
        public override void Reverse()
        {
            (startParam, endParam) = (endParam, startParam);
            (startPointSet, endPointSet) = (endPointSet, startPointSet);
            InvalidateSecondaryData();
        }
        public override ICurve2D Clone()
        {
            EnsureAnchor();
            ProjectedCurve res = new ProjectedCurve(curve3D, surface, startParam, endParam, anchor3d, anchorUv, this);
            res.startPointSet = startPointSet;
            res.endPointSet = endPointSet;
            res.UserData.CloneFrom(this.UserData);
            return res;
        }
        public override void Copy(ICurve2D toCopyFrom)
        {
            ProjectedCurve pc = toCopyFrom as ProjectedCurve;
            if (pc != null)
            {
                pc.EnsureAnchor();
                startParam = pc.startParam;
                endParam = pc.endParam;
                curve3D = pc.curve3D;
                surfaceOfOtherCurves = pc.surfaceOfOtherCurves;
                anchor3d = pc.anchor3d;
                anchorUv = pc.anchorUv;
                hasAnchor = true;
                surfaceOfIntersection = pc.surfaceOfIntersection;
                periodsBeyondStoredUv = pc.periodsBeyondStoredUv;
                startPointSet = pc.startPointSet;
                endPointSet = pc.endPointSet;
                InvalidateSecondaryData();
            }
        }
        /// <summary>
        /// The parts keep the periods of this curve: each gets an anchor from the middle of this curve's piece, which is
        /// never at a pole unless the whole piece is. A part of a curve of an intersection runs along a part of the same
        /// 3d curve.
        /// </summary>
        public override ICurve2D Trim(double StartPos, double EndPos)
        {
            EnsureAnchor();
            if (StartPos < EndPos)
            {
                double sp3d = Get3dParameter(StartPos);
                double ep3d = Get3dParameter(EndPos);
                double middle = (StartPos + EndPos) / 2.0;
                return new ProjectedCurve(curve3D, surface, sp3d, ep3d, Get3dParameter(middle), PointAt(middle), this);
            }
            else
            {
                // es geht bei einer geschlossenen Kurve über den Nahtpunkt
                double sp3d = Get3dParameter(StartPos);
                double ep3d = Get3dParameter(EndPos);
                ICurve c3d = curve3D.Clone();
                c3d.Trim(sp3d, ep3d);
                double middle = (StartPos + (EndPos + 1.0 - StartPos) / 2.0) % 1.0; // the middle of the piece across the seam
                return new ProjectedCurve(c3d, surface, 0, 1, 0.5, PointAt(middle), this);
            }
        }
        public override ICurve2D[] Split(double Position)
        {
            EnsureAnchor();
            double sp3d = Get3dParameter(Position);
            if (Math.Abs(sp3d - startParam) < 1e-6 || Math.Abs(sp3d - endParam) < 1e-6) return new ICurve2D[] { Clone() };
            double middle1 = Position / 2.0, middle2 = (Position + 1.0) / 2.0;
            return new ICurve2D[] {
                new ProjectedCurve(curve3D, surface, startParam, sp3d, Get3dParameter(middle1), PointAt(middle1), this),
                new ProjectedCurve(curve3D, surface, sp3d, endParam, Get3dParameter(middle2), PointAt(middle2), this) };
        }
        public override void Move(double x, double y)
        {
            bool ok = true; // move the anchor by the period
            if (surface.IsUPeriodic)
            {
                double dx = x / surface.UPeriod;
                ok &= (Math.Abs(dx - Math.Round(dx)) < 1e-10);
            }
            else
            {
                ok &= x == 0.0;
            }
            if (surface.IsVPeriodic)
            {
                double dy = y / surface.VPeriod;
                ok &= (Math.Abs(dy - Math.Round(dy)) < 1e-10);
            }
            else
            {
                ok &= y == 0.0;
            }
            if (ok)
            {
                EnsureAnchor();
                GeoVector2D offset = new GeoVector2D(x, y);
                if (ofIntersection) periodsBeyondStoredUv = periodsBeyondStoredUv + offset;
                else anchorUv = anchorUv + offset;
                if (startPointSet.HasValue) startPointSet = startPointSet.Value + offset;
                if (endPointSet.HasValue) endPointSet = endPointSet.Value + offset;
                InvalidateSecondaryData();
            }
            else throw new ApplicationException("cannot move ProjectedCurve");
        }
        /// <summary>
        /// A projected curve depends on its 3d curve and its surface and cannot be moved in the parameter plane at will: a
        /// modification which only turns it round gives a reversed copy, anything else an approximation. The new
        /// parametrization of the surface of a curve of an intersection is <see cref="Reparametrized"/>.
        /// </summary>
        public override ICurve2D GetModified(ModOp2D m)
        {
            if (m.IsIdentity) return Clone();
            GeoPoint2D sp = m * StartPoint;
            GeoPoint2D ep = m * EndPoint;
            if (Precision.IsEqual(sp, EndPoint) && Precision.IsEqual(ep, StartPoint))
            {
                return this.CloneReverse(true);
            }
            return base.GetModified(m);
        }
        #endregion
        #region ISerializable Members
        protected ProjectedCurve(SerializationInfo info, StreamingContext context)
            : base(info, context)
        {
#if DEBUG
            debugCount = debugCounter++;
#endif
            ReadValues(info.GetValue, name => HasValue(info, name));
        }
        /// <summary>Whether <paramref name="info"/> has an entry <paramref name="name"/>.</summary>
        private static bool HasValue(SerializationInfo info, string name)
        {
            foreach (SerializationEntry entry in info)
            {
                if (entry.Name == name) return true;
            }
            return false;
        }
        /// <summary>
        /// Reads what <see cref="AddValues"/> wrote, in this or in an older version, and also a curve of an intersection
        /// as the class InterpolatedDualSurfaceCurve.ProjectedCurve wrote it, which this class replaces, see
        /// <see cref="RenamedTypes"/>. For <see cref="ISerializable"/> and for <see cref="SetObjectData"/>.
        /// </summary>
        private void ReadValues(Func<string, Type, object> get, Func<string, bool> has)
        {
            if (has("OnSurface1"))
            {
                InitFromOlderFile(get("Curve3d", typeof(InterpolatedDualSurfaceCurve)) as InterpolatedDualSurfaceCurve,
                    (bool)get("OnSurface1", typeof(bool)), (bool)get("Reversed", typeof(bool)),
                    has("Offset") ? (GeoVector2D)get("Offset", typeof(GeoVector2D)) : GeoVector2D.NullVector);
                return;
            }
            curve3D = get("Curve3D", typeof(ICurve)) as ICurve;
            surface = get("Surface", typeof(ISurface)) as ISurface;
            startParam = (double)get("StartParam", typeof(double));
            endParam = (double)get("EndParam", typeof(double));
            if (has("SurfaceOfIntersection")) surfaceOfIntersection = (int)get("SurfaceOfIntersection", typeof(int));
            if (ofIntersection)
            {
                periodsBeyondStoredUv = (GeoVector2D)get("PeriodsBeyondStoredUv", typeof(GeoVector2D));
                hasAnchor = true; // there is none
            }
            else if (has("AnchorUv"))
            {
                anchorUv = (GeoPoint2D)get("AnchorUv", typeof(GeoPoint2D));
                anchor3d = (double)get("Anchor3d", typeof(double));
                hasAnchor = true;
            }
            else
            {   // written before the anchor existed: the anchor comes from the domain the curve was written with, on first
                // use - the 3d curve and the surface may not be complete while they are being read
                if (has("PeriodicDomain")) windowOfAnOlderFile = (BoundingRect)get("PeriodicDomain", typeof(BoundingRect));
                else windowOfAnOlderFile = BoundingRect.EmptyBoundingRect;
            }
        }
        /// <summary>Adds the data of this curve, without the data of the base class. For both formats.</summary>
        private void AddValues(Action<string, object> add)
        {
            add("Curve3D", curve3D);
            add("Surface", surface);
            add("StartParam", startParam);
            add("EndParam", endParam);
            if (ofIntersection)
            {
                add("SurfaceOfIntersection", surfaceOfIntersection);
                add("PeriodsBeyondStoredUv", periodsBeyondStoredUv);
            }
            else
            {
                EnsureAnchor();
                add("AnchorUv", anchorUv);
                add("Anchor3d", anchor3d);
            }
            // older versions move every point next to the center of this domain; the extent of the curve puts it where it is
            add("PeriodicDomain", GetExtent());
        }
        /// <summary>
        /// Implements <see cref="ISerializable.GetObjectData"/>
        /// </summary>
        /// <param name="info">The <see cref="System.Runtime.Serialization.SerializationInfo"/> to populate with data.</param>
        /// <param name="context">The destination (<see cref="System.Runtime.Serialization.StreamingContext"/>) for this serialization.</param>
        public override void GetObjectData(SerializationInfo info, StreamingContext context)
        {
            base.GetObjectData(info, context);
            AddValues(info.AddValue);
        }

        public override bool TryPointDeriv2At(double position, out GeoPoint2D point, out GeoVector2D deriv, out GeoVector2D deriv2)
        {
            if (ofIntersection) return ApproxBSpline2D.TryPointDeriv2At(position, out point, out deriv, out deriv2);
            point = GeoPoint2D.Origin;
            deriv = deriv2 = GeoVector2D.NullVector;
            return false;
        }

        public void GetObjectData(IJsonWriteData data)
        {
            JSonGetObjectData(data);
            AddValues(data.AddProperty);
        }

        public void SetObjectData(IJsonReadData data)
        {
            JSonSetObjectData(data);
            ReadValues(data.GetProperty, data.HasProperty);
        }

        internal void InvalidateSecondaryData()
        {
            approxBSpline2D = null;
            ClearTriangulation();
        }

        #endregion
#if DEBUG
        public Polyline DebugPolyLine
        {
            get
            {
                GeoPoint[] pnts = new GeoPoint[100];
                for (int i = 0; i < pnts.Length; i++)
                {
                    pnts[i] = Plane.XYPlane.ToGlobal(PointAt(i / 99.0));

                }
                Polyline res = Polyline.Construct();
                res.SetPoints(pnts, false);
                return res;
            }
        }
        public ICurve DebugReprojectedCurve3D
        {
            get
            {
                GeoPoint[] pnts = new GeoPoint[100];
                for (int i = 0; i < pnts.Length; i++)
                {
                    pnts[i] = surface.PointAt(PointAt(i / 99.0));

                }
                Polyline res = Polyline.Construct();
                res.SetPoints(pnts, false);
                return res;
            }
        }
#endif
    }
}
