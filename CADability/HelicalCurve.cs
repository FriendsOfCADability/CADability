using CADability.Attribute;
using System;
using System.Collections.Generic;
using System.Runtime.Serialization;

namespace CADability.GeoObject
{
    /// <summary>
    /// A helical curve (helix, screw line) with a constant radius and a constant pitch.
    /// <para>
    /// The curve is defined in the local coordinate system of a <see cref="Plane"/>: the origin of that plane is a point
    /// on the axis, the normal of the plane is the direction of the axis and the x-axis of the plane is the reference
    /// direction for the angle 0. In this local system the curve is
    /// </para>
    /// <code>
    /// p(a) = (radius * cos(a), radius * sin(a), pitch * a / (2*PI))
    /// </code>
    /// <para>
    /// where the angle <c>a</c> runs from <see cref="StartParameter"/> to <c>StartParameter + </c><see cref="SweepParameter"/>
    /// while the position of the curve (as used by <see cref="ICurve.PointAt(double)"/>) runs from 0 to 1. A positive
    /// <see cref="Pitch"/> yields a right-handed helix, a negative one a left-handed helix. With a pitch of 0 the curve
    /// degenerates to a circular arc, with a radius of 0 it degenerates to a straight line along the axis.
    /// </para>
    /// </summary>
    [Serializable()]
    [JsonVersion(1)]
    public class HelicalCurve : GeneralCurve, ISerializable, IJsonSerialize, IExportStep
    {
        private Plane plane; // location: point on the axis, normal: direction of the axis, x-axis: reference for the angle 0
        private double radius; // distance from the axis, always >= 0
        private double pitch; // advance along the axis for one full turn, may be negative (left-handed helix)
        private double startParameter; // angle (in radian) at position 0.0
        private double sweepParameter; // swept angle (in radian) from position 0.0 to position 1.0, may be negative

        #region polymorph construction
        /// <summary>
        /// Delegate for the construction of a HelicalCurve.
        /// </summary>
        /// <returns>A HelicalCurve or a HelicalCurve derived class</returns>
        public new delegate HelicalCurve ConstructionDelegate();
        /// <summary>
        /// Provide a delegate here if you want your HelicalCurve derived class to be created each time
        /// CADability creates a helical curve.
        /// </summary>
        public new static ConstructionDelegate Constructor;
        /// <summary>
        /// The only way to create a HelicalCurve. There are no public constructors to assure that this is
        /// the only way to construct a helical curve.
        /// </summary>
        /// <returns>the newly created curve</returns>
        public static HelicalCurve Construct()
        {
            if (Constructor != null) return Constructor();
            return new HelicalCurve();
        }
        public delegate void ConstructedDelegate(HelicalCurve justConstructed);
        public static event ConstructedDelegate Constructed;
        #endregion

        protected HelicalCurve()
        {
            plane = Plane.XYPlane;
            radius = 1.0;
            pitch = 1.0;
            startParameter = 0.0;
            sweepParameter = 2.0 * Math.PI;
            if (Constructed != null) Constructed(this);
        }

        #region construction helpers
        /// <summary>
        /// Sets all defining data of this helical curve. The origin of <paramref name="plane"/> is a point on the axis,
        /// the normal of the plane is the direction of the axis and the x-axis of the plane defines the angle 0.
        /// </summary>
        /// <param name="plane">the coordinate system of the helix</param>
        /// <param name="radius">the (constant) distance from the axis</param>
        /// <param name="pitch">the advance along the axis for one full turn, negative for a left-handed helix</param>
        /// <param name="startParameter">the angle (in radian) at position 0.0</param>
        /// <param name="sweepParameter">the swept angle (in radian), negative for a clockwise rotation</param>
        public void SetHelix(Plane plane, double radius, double pitch, double startParameter, double sweepParameter)
        {
            using (new Changing(this, "CopyGeometry", Clone()))
            {
                this.plane = plane;
                this.radius = Math.Abs(radius);
                this.pitch = pitch;
                this.startParameter = startParameter;
                this.sweepParameter = sweepParameter;
                InvalidateSecondaryData();
            }
        }
        /// <summary>
        /// Sets all defining data of this helical curve. The curve starts at <paramref name="startPoint"/> and turns
        /// <paramref name="numberOfTurns"/> times around the axis given by <paramref name="axisLocation"/> and
        /// <paramref name="axisDirection"/>. A negative number of turns rotates clockwise (seen in the direction of the axis).
        /// </summary>
        /// <param name="axisLocation">a point on the axis</param>
        /// <param name="axisDirection">the direction of the axis</param>
        /// <param name="startPoint">the start point of the curve, its distance from the axis is the radius</param>
        /// <param name="pitch">the advance along the axis for one full turn, negative for a left-handed helix</param>
        /// <param name="numberOfTurns">the number of turns, may be fractional and negative</param>
        public void SetAxisStartPoint(GeoPoint axisLocation, GeoVector axisDirection, GeoPoint startPoint, double pitch, double numberOfTurns)
        {
            if (axisDirection.IsNullVector()) throw new GeneralCurveException("HelicalCurve: the axis direction must not be a null vector");
            GeoPoint foot = Geometry.DropPL(startPoint, axisLocation, axisDirection);
            GeoVector toStart = startPoint - foot;
            Plane pln;
            if (toStart.Length < Precision.eps)
            {   // the start point is on the axis: the curve degenerates to a line, any perpendicular direction will do
                pln = new Plane(foot, axisDirection);
            }
            else
            {
                pln = new Plane(foot, toStart, axisDirection ^ toStart);
            }
            SetHelix(pln, toStart.Length, pitch, 0.0, numberOfTurns * 2.0 * Math.PI);
        }
        /// <summary>
        /// Creates a helical curve which starts at <paramref name="startPoint"/> and turns <paramref name="numberOfTurns"/>
        /// times around the given axis. See <see cref="SetAxisStartPoint"/>.
        /// </summary>
        public static HelicalCurve FromAxisStartPoint(GeoPoint axisLocation, GeoVector axisDirection, GeoPoint startPoint, double pitch, double numberOfTurns)
        {
            HelicalCurve res = Construct();
            res.SetAxisStartPoint(axisLocation, axisDirection, startPoint, pitch, numberOfTurns);
            return res;
        }
        #endregion

        #region properties
        /// <summary>
        /// The coordinate system of this helix: the origin is a point on the axis, the normal is the direction of the
        /// axis and the x-axis defines the angle 0.
        /// </summary>
        public Plane Plane
        {
            get { return plane; }
            set
            {
                using (new Changing(this, "CopyGeometry", Clone()))
                {
                    plane = value;
                    InvalidateSecondaryData();
                }
            }
        }
        /// <summary>
        /// The point on the axis which corresponds to the angle 0 and the height 0.
        /// </summary>
        public GeoPoint Location
        {
            get { return plane.Location; }
        }
        /// <summary>
        /// The (normalized) direction of the axis.
        /// </summary>
        public GeoVector AxisDirection
        {
            get { return plane.Normal; }
        }
        /// <summary>
        /// The (normalized) reference direction for the angle 0.
        /// </summary>
        public GeoVector XAxis
        {
            get { return plane.DirectionX; }
        }
        /// <summary>
        /// The distance of the curve from the axis.
        /// </summary>
        public double Radius
        {
            get { return radius; }
            set
            {
                using (new Changing(this, "CopyGeometry", Clone()))
                {
                    radius = Math.Abs(value);
                    InvalidateSecondaryData();
                }
            }
        }
        /// <summary>
        /// The advance along the axis for one full turn. Positive for a right-handed, negative for a left-handed helix.
        /// </summary>
        public double Pitch
        {
            get { return pitch; }
            set
            {
                using (new Changing(this, "CopyGeometry", Clone()))
                {
                    pitch = value;
                    InvalidateSecondaryData();
                }
            }
        }
        /// <summary>
        /// The angle (in radian) at position 0.0.
        /// </summary>
        public double StartParameter
        {
            get { return startParameter; }
            set
            {
                using (new Changing(this, "CopyGeometry", Clone()))
                {
                    startParameter = value;
                    InvalidateSecondaryData();
                }
            }
        }
        /// <summary>
        /// The swept angle (in radian) from position 0.0 to position 1.0. Negative values rotate clockwise
        /// (seen in the direction of the axis).
        /// </summary>
        public double SweepParameter
        {
            get { return sweepParameter; }
            set
            {
                using (new Changing(this, "CopyGeometry", Clone()))
                {
                    sweepParameter = value;
                    InvalidateSecondaryData();
                }
            }
        }
        /// <summary>
        /// The number of turns of this curve, may be fractional and negative.
        /// </summary>
        public double NumberOfTurns
        {
            get { return sweepParameter / (2.0 * Math.PI); }
            set { SweepParameter = value * 2.0 * Math.PI; }
        }
        /// <summary>
        /// The signed distance which the curve advances in the direction of the axis from the start- to the endpoint.
        /// </summary>
        public double Height
        {
            get { return AxialFactor * sweepParameter; }
        }
        /// <summary>
        /// True, when this is a left-handed helix (i.e. the <see cref="Pitch"/> is negative).
        /// </summary>
        public bool IsLeftHanded
        {
            get { return pitch < 0.0; }
        }
        #endregion

        #region internal computation
        /// <summary>
        /// The advance along the axis per radian.
        /// </summary>
        private double AxialFactor
        {
            get { return pitch / (2.0 * Math.PI); }
        }
        private double ParameterAt(double position)
        {
            return startParameter + position * sweepParameter;
        }
        private GeoPoint LocalPointAt(double angle)
        {
            return new GeoPoint(radius * Math.Cos(angle), radius * Math.Sin(angle), AxialFactor * angle);
        }
        /// <summary>
        /// Newton iteration for the stationary points of |p(a)-localPoint|². With
        /// p(a) = (r*cos(a), r*sin(a), h*a) the derivative of that squared distance is
        /// f(a) = r*(px*sin(a) - py*cos(a)) + h²*a - h*pz, which is what is used here.
        /// </summary>
        private double RefineAngle(double angle, GeoPoint localPoint)
        {
            double h = AxialFactor;
            double start = angle;
            for (int i = 0; i < 20; ++i)
            {
                double sin = Math.Sin(angle);
                double cos = Math.Cos(angle);
                double f = radius * (localPoint.x * sin - localPoint.y * cos) + h * h * angle - h * localPoint.z;
                double df = radius * (localPoint.x * cos + localPoint.y * sin) + h * h;
                if (Math.Abs(df) < 1e-13) break; // no usable slope, keep what we have
                double step = f / df;
                angle -= step;
                if (Math.Abs(step) < 1e-13) break;
            }
            // the iteration must stay in the vicinity of the starting angle, otherwise it converged to
            // a different turn of the helix or to a maximum of the distance
            if (Math.Abs(angle - start) > Math.PI) return start;
            if ((LocalPointAt(angle) | localPoint) > (LocalPointAt(start) | localPoint)) return start;
            return angle;
        }
        /// <summary>
        /// Adds all positions which correspond to the given angle (modulo 2*PI) and are inside the parameter range.
        /// </summary>
        private void AddPositionsForAngle(double angle, List<double> positions)
        {
            if (Math.Abs(sweepParameter) < 1e-13) return;
            double aMin = Math.Min(startParameter, startParameter + sweepParameter);
            double aMax = Math.Max(startParameter, startParameter + sweepParameter);
            int kMin = (int)Math.Floor((aMin - angle) / (2.0 * Math.PI));
            int kMax = (int)Math.Ceiling((aMax - angle) / (2.0 * Math.PI));
            for (int k = kMin; k <= kMax; ++k)
            {
                double a = angle + k * 2.0 * Math.PI;
                if (a < aMin || a > aMax) continue;
                double pos = (a - startParameter) / sweepParameter;
                if (pos >= 0.0 && pos <= 1.0) positions.Add(pos);
            }
        }
        #endregion

        #region IGeoObjectImpl overrides
        public override IGeoObject Clone()
        {
            HelicalCurve res = Construct();
            res.plane = plane;
            res.radius = radius;
            res.pitch = pitch;
            res.startParameter = startParameter;
            res.sweepParameter = sweepParameter;
            res.CopyAttributes(this);
            return res;
        }
        public override void CopyGeometry(IGeoObject ToCopyFrom)
        {
            HelicalCurve other = ToCopyFrom as HelicalCurve;
            if (other == null) return;
            using (new Changing(this, "CopyGeometry", Clone()))
            {
                plane = other.plane;
                radius = other.radius;
                pitch = other.pitch;
                startParameter = other.startParameter;
                sweepParameter = other.sweepParameter;
                InvalidateSecondaryData();
            }
        }
        /// <summary>
        /// Overrides <see cref="CADability.GeoObject.IGeoObjectImpl.Modify (ModOp)"/>.
        /// Only similarity transformations (rigid motions, uniform scaling and mirroring) map a helix onto a helix.
        /// For other modifications the radius is taken from the scaling of the reference direction and the pitch from
        /// the scaling of the axis, which is still exact when scaling along the axis only.
        /// </summary>
        /// <param name="m"></param>
        public override void Modify(ModOp m)
        {
            using (new Changing(this, "ModifyInverse", m))
            {
                GeoPoint newLocation = m * plane.Location;
                GeoVector newDirX = m * plane.DirectionX;
                GeoVector newDirY = m * plane.DirectionY;
                GeoVector newAxis = m * plane.Normal;
                try
                {
                    Plane newPlane = new Plane(newLocation, newDirX, newDirY);
                    double axisFactor = newAxis.Length;
                    // a mirroring inverts the handedness: the transformed axis points to the opposite side of the new plane
                    if (newPlane.Normal * newAxis < 0.0) axisFactor = -axisFactor;
                    radius *= newDirX.Length;
                    pitch *= axisFactor;
                    plane = newPlane;
                }
                catch (PlaneException)
                {   // the modification degenerates the coordinate system, leave the curve unchanged
                }
                InvalidateSecondaryData();
            }
        }
        #endregion

        #region ICurve implementation
        /// <summary>
        /// Implements <see cref="CADability.GeoObject.ICurve.PointAt (double)"/>
        /// </summary>
        public override GeoPoint PointAt(double Position)
        {
            return plane.ToGlobal(LocalPointAt(ParameterAt(Position)));
        }
        /// <summary>
        /// Implements <see cref="CADability.GeoObject.ICurve.DirectionAt (double)"/>
        /// </summary>
        public override GeoVector DirectionAt(double Position)
        {
            double a = ParameterAt(Position);
            return sweepParameter * plane.ToGlobal(new GeoVector(-radius * Math.Sin(a), radius * Math.Cos(a), AxialFactor));
        }
        /// <summary>
        /// Implements <see cref="CADability.GeoObject.ICurve.Reverse ()"/>
        /// </summary>
        public override void Reverse()
        {
            using (new Changing(this, typeof(ICurve), "Reverse", new object[0]))
            {
                startParameter += sweepParameter;
                sweepParameter = -sweepParameter;
                InvalidateSecondaryData();
            }
        }
        /// <summary>
        /// Implements <see cref="CADability.GeoObject.ICurve.Split (double)"/>
        /// </summary>
        public override ICurve[] Split(double Position)
        {
            if (Position <= 0.0 || Position >= 1.0) return new ICurve[] { (ICurve)Clone() };
            HelicalCurve h1 = (HelicalCurve)Clone();
            HelicalCurve h2 = (HelicalCurve)Clone();
            h1.sweepParameter = sweepParameter * Position;
            h2.startParameter = startParameter + sweepParameter * Position;
            h2.sweepParameter = sweepParameter * (1.0 - Position);
            return new ICurve[] { h1, h2 };
        }
        /// <summary>
        /// Implements <see cref="CADability.GeoObject.ICurve.Trim (double, double)"/>
        /// </summary>
        public override void Trim(double StartPos, double EndPos)
        {
            if (StartPos == EndPos) return;
            using (new Changing(this, "CopyGeometry", Clone()))
            {
                double a0 = ParameterAt(StartPos);
                double a1 = ParameterAt(EndPos);
                startParameter = a0;
                sweepParameter = a1 - a0;
                InvalidateSecondaryData();
            }
        }
        /// <summary>
        /// Implements <see cref="CADability.GeoObject.ICurve.Length"/>. The arc length of a helix is proportional to the
        /// swept angle, so it can be calculated exactly.
        /// </summary>
        public override double Length
        {
            get
            {
                double h = AxialFactor;
                return Math.Abs(sweepParameter) * Math.Sqrt(radius * radius + h * h);
            }
        }
        /// <summary>
        /// Implements <see cref="CADability.GeoObject.ICurve.PositionOf (GeoPoint)"/>
        /// </summary>
        public override double PositionOf(GeoPoint p)
        {
            return PositionOf(p, double.NaN);
        }
        /// <summary>
        /// Implements <see cref="CADability.GeoObject.ICurve.PositionOf (GeoPoint, double)"/>.
        /// A helix passes the same angle once per turn, so there may be several foot points. The one closest to
        /// <paramref name="prefer"/> is returned, when several foot points have (almost) the same distance.
        /// </summary>
        public override double PositionOf(GeoPoint p, double prefer)
        {
            if (Math.Abs(sweepParameter) < 1e-13) return 0.0;
            GeoPoint lp = plane.ToLocal(p);
            double baseAngle = Math.Atan2(lp.y, lp.x); // in [-PI, PI]
            double aMin = Math.Min(startParameter, startParameter + sweepParameter);
            double aMax = Math.Max(startParameter, startParameter + sweepParameter);
            // all angles congruent to baseAngle which are inside (or half a turn beyond) the parameter range are candidates
            int kMin = (int)Math.Floor((aMin - Math.PI - baseAngle) / (2.0 * Math.PI));
            int kMax = (int)Math.Ceiling((aMax + Math.PI - baseAngle) / (2.0 * Math.PI));
            if (kMax - kMin > 10000) kMax = kMin + 10000; // safety against absurd parameter ranges
            double bestAngle = startParameter;
            double bestDist = double.MaxValue;
            List<double> candidates = new List<double>();
            for (int k = kMin; k <= kMax; ++k)
            {
                double a = RefineAngle(baseAngle + k * 2.0 * Math.PI, lp);
                double d = LocalPointAt(a) | lp;
                candidates.Add(a);
                if (d < bestDist)
                {
                    bestDist = d;
                    bestAngle = a;
                }
            }
            if (!double.IsNaN(prefer))
            {   // among the equally good foot points take the one closest to the preferred position
                double bestDelta = Math.Abs((bestAngle - startParameter) / sweepParameter - prefer);
                for (int i = 0; i < candidates.Count; ++i)
                {
                    if ((LocalPointAt(candidates[i]) | lp) > bestDist + Precision.eps) continue;
                    double delta = Math.Abs((candidates[i] - startParameter) / sweepParameter - prefer);
                    if (delta < bestDelta)
                    {
                        bestDelta = delta;
                        bestAngle = candidates[i];
                    }
                }
            }
            return (bestAngle - startParameter) / sweepParameter;
        }
        /// <summary>
        /// Implements <see cref="CADability.GeoObject.ICurve.IsClosed"/>
        /// </summary>
        public override bool IsClosed
        {
            get
            {
                if (Math.Abs(sweepParameter) < Precision.eps) return false;
                return Precision.IsEqual(StartPoint, EndPoint);
            }
        }
        public override bool IsSingular
        {
            get { return Length < Precision.eps; }
        }
        /// <summary>
        /// Implements <see cref="CADability.GeoObject.ICurve.GetPlanarState ()"/>
        /// </summary>
        public override PlanarState GetPlanarState()
        {
            if (radius < Precision.eps) return PlanarState.UnderDetermined; // a straight line along the axis or a single point
            if (Math.Abs(Height) < Precision.eps) return PlanarState.Planar; // no advance along the axis: a circular arc
            return PlanarState.NonPlanar;
        }
        /// <summary>
        /// Implements <see cref="CADability.GeoObject.ICurve.GetPlane ()"/>
        /// </summary>
        public override Plane GetPlane()
        {
            if (GetPlanarState() != PlanarState.Planar) return base.GetPlane();
            // the plane through the middle of the (negligible) height of the curve
            double zMiddle = AxialFactor * (startParameter + sweepParameter / 2.0);
            return new Plane(plane.ToGlobal(new GeoPoint(0.0, 0.0, zMiddle)), plane.DirectionX, plane.DirectionY);
        }
        /// <summary>
        /// Implements <see cref="CADability.GeoObject.ICurve.IsInPlane (Plane)"/>
        /// </summary>
        public override bool IsInPlane(Plane p)
        {
            switch (GetPlanarState())
            {
                case PlanarState.Planar:
                    Plane own = GetPlane();
                    return Precision.SameDirection(own.Normal, p.Normal, true) && Math.Abs(p.Distance(own.Location)) < Precision.eps;
                case PlanarState.UnderDetermined:
                    return Math.Abs(p.Distance(StartPoint)) < Precision.eps && Math.Abs(p.Distance(EndPoint)) < Precision.eps;
                default:
                    return false;
            }
        }
        /// <summary>
        /// Implements <see cref="CADability.GeoObject.ICurve.TangentPosition (GeoVector)"/>. The tangent of a helix
        /// encloses a constant angle with the axis, hence there is at most one matching angle per turn.
        /// </summary>
        public override double[] TangentPosition(GeoVector direction)
        {
            List<double> res = new List<double>();
            GeoVector ld = plane.ToLocal(direction);
            if (ld.IsNullVector()) return res.ToArray();
            ld.Norm();
            double h = AxialFactor;
            double tangentLength = Math.Sqrt(radius * radius + h * h);
            if (tangentLength < Precision.eps) return res.ToArray(); // a single point, no tangent at all
            double cz = h / tangentLength; // the axial component of the normalized tangent, constant along the curve
            for (int i = 0; i < 2; ++i)
            {
                double sign = (i == 0) ? 1.0 : -1.0; // the direction is not oriented, so both orientations count
                if (Math.Abs(sign * ld.z - cz) > Precision.eps) continue;
                if (radius < Precision.eps)
                {   // a straight line along the axis: the tangent is the same everywhere
                    res.Add(0.5);
                    break;
                }
                // sin(a) = -sign*ld.x*tangentLength/radius, cos(a) = sign*ld.y*tangentLength/radius
                double angle = Math.Atan2(-sign * ld.x, sign * ld.y);
                AddPositionsForAngle(angle, res);
            }
            res.Sort();
            return res.ToArray();
        }
        /// <summary>
        /// Implements <see cref="CADability.GeoObject.ICurve.PositionAtLength (double)"/>. The arc length is
        /// proportional to the position, so this is exact.
        /// </summary>
        public override double PositionAtLength(double position)
        {
            double length = Length;
            if (length < Precision.eps) return 0.0;
            return position / length;
        }
        /// <summary>
        /// Implements <see cref="CADability.GeoObject.ICurve.ParameterToPosition (double)"/>. The parameter of a
        /// helical curve is the angle in radian.
        /// </summary>
        public override double ParameterToPosition(double parameter)
        {
            if (Math.Abs(sweepParameter) < 1e-13) return 0.0;
            return (parameter - startParameter) / sweepParameter;
        }
        /// <summary>
        /// Implements <see cref="CADability.GeoObject.ICurve.PositionToParameter (double)"/>. The parameter of a
        /// helical curve is the angle in radian.
        /// </summary>
        public override double PositionToParameter(double position)
        {
            return ParameterAt(position);
        }
        /// <summary>
        /// Implements <see cref="CADability.GeoObject.ICurve.TryPointDeriv2At (double, out GeoPoint, out GeoVector, out GeoVector)"/>
        /// </summary>
        public override bool TryPointDeriv2At(double u, out GeoPoint point, out GeoVector deriv, out GeoVector deriv2)
        {
            double a = ParameterAt(u);
            double sin = Math.Sin(a);
            double cos = Math.Cos(a);
            point = plane.ToGlobal(new GeoPoint(radius * cos, radius * sin, AxialFactor * a));
            deriv = sweepParameter * plane.ToGlobal(new GeoVector(-radius * sin, radius * cos, AxialFactor));
            deriv2 = sweepParameter * sweepParameter * plane.ToGlobal(new GeoVector(-radius * cos, -radius * sin, 0.0));
            return true;
        }
        /// <summary>
        /// Overrides <see cref="GeneralCurve.PointAndDerivativesAt(double, int)"/>. All derivatives of a helix are
        /// available in closed form.
        /// </summary>
        public override IReadOnlyList<GeoVector> PointAndDerivativesAt(double position, int grad)
        {
            List<GeoVector> res = new List<GeoVector>(Math.Max(grad + 1, 1));
            double a = ParameterAt(position);
            double h = AxialFactor;
            for (int n = 0; n <= grad; ++n)
            {
                // the n-th derivative of cos(a(t)) is sweepParameter^n * cos(a + n*PI/2), same for sin
                double factor = Math.Pow(sweepParameter, n);
                double x = radius * factor * Math.Cos(a + n * Math.PI / 2.0);
                double y = radius * factor * Math.Sin(a + n * Math.PI / 2.0);
                if (n == 0) res.Add(plane.ToGlobal(new GeoPoint(x, y, h * a)).ToVector());
                else if (n == 1) res.Add(plane.ToGlobal(new GeoVector(x, y, h * sweepParameter)));
                else res.Add(plane.ToGlobal(new GeoVector(x, y, 0.0)));
            }
            return res;
        }
        /// <summary>
        /// Implements <see cref="GeneralCurve.GetBasePoints"/>. The curve is subdivided into segments of at most 45°.
        /// </summary>
        protected override double[] GetBasePoints()
        {
            int n = (int)Math.Ceiling(Math.Abs(sweepParameter) / (Math.PI / 4.0));
            if (n < 4) n = 4;
            if (n > 1000) n = 1000;
            double[] res = new double[n + 1];
            for (int i = 0; i <= n; ++i) res[i] = (double)i / n;
            return res;
        }
        #endregion

        #region IExportStep Members
        /// <summary>
        /// Approximates this helical curve by a BSpline with the given precision. A circular helix cannot be
        /// represented exactly by a (rational) BSpline, so an approximation is needed, e.g. for the STEP export.
        /// </summary>
        /// <param name="precision">the maximum deviation of the BSpline from this curve</param>
        /// <returns>the approximating BSpline</returns>
        public BSpline ToBSpline(double precision)
        {
            if (precision <= 0.0) precision = Precision.eps;
            // the interpolation error of a cubic spline through the points of a circle with the radius r and the
            // angular step d behaves like r*d^4/c. The theoretical c=384 holds for a knot vector proportional to
            // the angle; ThroughPoints uses a different one, so c=20 was determined empirically (it keeps the
            // deviation at about half the requested precision). The upper limit for n avoids absurd BSplines for
            // very long helices, there the precision may not be reached.
            int n = 8;
            if (radius > Precision.eps)
            {
                double step = Math.Pow(20 * precision / radius, 0.25);
                if (step > 0.0) n = (int)Math.Ceiling(Math.Abs(sweepParameter) / step);
            }
            n = Math.Min(Math.Max(n, 8), 8000);
            GeoPoint[] points = new GeoPoint[n + 1];
            for (int i = 0; i <= n; ++i) points[i] = PointAt((double)i / n);
            BSpline res = BSpline.Construct();
            if (!res.ThroughPoints(points, 3, false)) res.ThroughPoints(points, 1, false);
            return res;
        }
        int IExportStep.Export(ExportStep export, bool topLevel)
        {   // STEP has no entity for a helix, so it is exported as an approximating BSpline
            return (ToBSpline(export.Precision) as IExportStep).Export(export, topLevel);
        }
        #endregion

        #region ISerializable Members
        /// <summary>
        /// Constructor required by deserialization
        /// </summary>
        /// <param name="info">SerializationInfo</param>
        /// <param name="context">StreamingContext</param>
        protected HelicalCurve(SerializationInfo info, StreamingContext context)
            : base(info, context)
        {
            plane = (Plane)info.GetValue("Plane", typeof(Plane));
            radius = (double)info.GetValue("Radius", typeof(double));
            pitch = (double)info.GetValue("Pitch", typeof(double));
            startParameter = (double)info.GetValue("StartParameter", typeof(double));
            sweepParameter = (double)info.GetValue("SweepParameter", typeof(double));
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
            info.AddValue("Radius", radius);
            info.AddValue("Pitch", pitch);
            info.AddValue("StartParameter", startParameter);
            info.AddValue("SweepParameter", sweepParameter);
        }
        #endregion

        #region IJsonSerialize Members
        public void GetObjectData(IJsonWriteData data)
        {
            data.AddProperty("Plane", plane);
            data.AddProperty("Radius", radius);
            data.AddProperty("Pitch", pitch);
            data.AddProperty("StartParameter", startParameter);
            data.AddProperty("SweepParameter", sweepParameter);
            // the attributes of GeneralCurve are private there, so they have to be written on this level
            if (ColorDef != null) data.AddProperty("ColorDef", ColorDef);
            if (LineWidth != null) data.AddProperty("LineWidth", LineWidth);
            if (LinePattern != null) data.AddProperty("LinePattern", LinePattern);
        }
        public void SetObjectData(IJsonReadData data)
        {
            plane = data.GetProperty<Plane>("Plane");
            radius = data.GetProperty<double>("Radius");
            pitch = data.GetProperty<double>("Pitch");
            startParameter = data.GetProperty<double>("StartParameter");
            sweepParameter = data.GetProperty<double>("SweepParameter");
            ColorDef cd = data.GetPropertyOrDefault<ColorDef>("ColorDef");
            if (cd != null) (this as IColorDef).SetTopLevel(cd);
            LineWidth lw = data.GetPropertyOrDefault<LineWidth>("LineWidth");
            if (lw != null) LineWidth = lw;
            LinePattern lp = data.GetPropertyOrDefault<LinePattern>("LinePattern");
            if (lp != null) LinePattern = lp;
        }
        #endregion
    }
}
