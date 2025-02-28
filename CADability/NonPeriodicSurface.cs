using CADability.Curve2D;
using CADability.Shapes;
using CADability.UserInterface;
using System;
using System.Collections.Generic;
using System.Runtime.Serialization;

namespace CADability.GeoObject
{
    /// <summary>
    /// Interface for surfaces with non rectangular definition space. Normally surfaces are either defined for all 2d values (infinite)
    /// or they are restricted to a rectangular domain (e.g. <see cref="NurbsSurface"/>). The <see cref="NonPeriodicSurface"/> is only defined on a
    /// circular area or on a ring. Maybe other surfaces with irregular domains will follow. 
    /// </summary>
    public interface IRestrictedDomain
    {
        /// <summary>
        /// True, if the provided <paramref name="uv"/> value is inside the definition domain
        /// </summary>
        /// <param name="uv"></param>
        /// <returns></returns>
        bool IsInside(GeoPoint2D uv);
        /// <summary>
        /// Same as <see cref="ISurface.PointAt(GeoPoint2D)"/>, but when uv is outside the definition domain, the resulting point is forced inside the domain
        /// </summary>
        /// <param name="uv"></param>
        /// <returns></returns>
        GeoPoint RestrictedPointAt(GeoPoint2D uv);
        /// <summary>
        /// Clip the provided <paramref name="curve"/> at the bounds of the domain. The result are pairs of parameters of the curve, which define segments, that are inside the domain.
        /// </summary>
        /// <param name="curve"></param>
        /// <returns></returns>
        double[] Clip(ICurve2D curve);
        ISurface AdaptToCurves(IEnumerable<ICurve> curves);
    }
    /// <summary>
    /// Non-periodic surface made from an underlying periodic surface. The underlying periodic surface must be periodic in u or in v and may have a pole 
    /// at the minimum or maximum of the non periodic parameter. The underlying periodic surface may also be non periodic but have one singularity, which will be removed.
    /// </summary>
    [Serializable]
    public class NonPeriodicSurface : ISurfaceImpl, ISerializable, IDeserializationCallback, IRestrictedDomain, IJsonSerialize, IJsonSerializeDone
    {
        ISurface periodicSurface;
        BoundingRect periodicBounds;
        bool hasPole, fullPeriod;
        ModOp2D toNonPeriodicBounds, toPeriodicBounds;
        /// <summary>
        /// </summary>
        /// <param name="periodicSurface"></param>
        /// <param name="vmin"></param>
        /// <param name="vmax"></param>
        public NonPeriodicSurface(ISurface periodicSurface, BoundingRect bounds)
        {
            this.periodicSurface = periodicSurface;
            this.periodicBounds = bounds;
            bool uperiodic;
            if (periodicSurface.IsUPeriodic && periodicSurface.IsVPeriodic)
            {
                if (bounds.Width / periodicSurface.UPeriod < bounds.Height / periodicSurface.VPeriod)
                {
                    periodicBounds.Bottom = 0.0;
                    periodicBounds.Top = periodicSurface.VPeriod;
                    uperiodic = false;
                }
                else
                {
                    periodicBounds.Left = 0.0;
                    periodicBounds.Right = periodicSurface.UPeriod;
                    uperiodic = true;
                }
            }
            else if (periodicSurface.IsUPeriodic)
            {
                periodicBounds.Left = 0.0;
                periodicBounds.Right = periodicSurface.UPeriod;
                uperiodic = true;
            }
            else if (periodicSurface.IsVPeriodic)
            {
                periodicBounds.Bottom = 0.0;
                periodicBounds.Top = periodicSurface.VPeriod;
                uperiodic = false;
            }
            else uperiodic = false; // must be assigned
            Init(uperiodic);
        }
        private void Init(bool uPeriodic)
        {
            hasPole = false;
            fullPeriod = false;
            if (uPeriodic)
            {
                fullPeriod = true;
                double[] sv = periodicSurface.GetVSingularities();
                if (sv != null && sv.Length > 1)
                {
                    List<double> lsv = new List<double>(sv);
                    for (int i = lsv.Count - 1; i >= 0; --i)
                    {
                        if (lsv[i] < periodicBounds.Bottom || lsv[i] > periodicBounds.Top) lsv.RemoveAt(i);
                    }
                    sv = lsv.ToArray();
                }
                if (sv != null && sv.Length > 0)
                {
                    if (sv.Length == 1)
                    {
                        hasPole = true;
                        if (sv[0] == periodicBounds.Bottom)
                        {
                            // bounds.Bottom => 0.0, bounds.Top => 1.0
                            // bounds.Left => 0, bountd.Right => 2*pi
                            double fvu = 2.0 * Math.PI / periodicBounds.Width;
                            double fuv = 1.0 / periodicBounds.Height;
                            toNonPeriodicBounds = new ModOp2D(fvu, 0, -fvu * periodicBounds.Left, 0, fuv, -fuv * periodicBounds.Bottom);
                        }
                        else if (sv[0] == periodicBounds.Top)
                        {
                            // bounds.Bottom => 0.0, bounds.Top => 1.0
                            // bounds.Left => 2*pi, bountd.Right => 0
                            double fvu = 2.0 * Math.PI / periodicBounds.Width;
                            double fuv = 1.0 / periodicBounds.Height;
                            toNonPeriodicBounds = new ModOp2D(-fvu, 0, fvu * periodicBounds.Right, 0, fuv, -fuv * periodicBounds.Bottom);
                        }
                        else throw new ApplicationException("pole must be on border");
                    }
                    else throw new ApplicationException("more than one pole");
                }
                else
                {
                    // bounds.Bottom => 0.5, bounds.Top => 1.5
                    // bounds.Left => 0, bountd.Right => 2*pi
                    double fvu = -2.0 * Math.PI / periodicBounds.Width; // "-", because toroidal surface needs it for correct orientation. What about NURBS?
                    double fuv = 1.0 / periodicBounds.Height;
                    toNonPeriodicBounds = new ModOp2D(fvu, 0, -fvu * periodicBounds.Left - Math.PI, 0, fuv, -fuv * periodicBounds.Bottom + 0.5);
                }
            }
            else if (periodicSurface.IsVPeriodic)
            {
                fullPeriod = true;
                double[] su = periodicSurface.GetUSingularities();
                if (su != null && su.Length > 1)
                {
                    List<double> lsu = new List<double>(su);
                    for (int i = lsu.Count - 1; i >= 0; --i)
                    {
                        if (lsu[i] < periodicBounds.Left || lsu[i] > periodicBounds.Right) lsu.RemoveAt(i);
                    }
                    su = lsu.ToArray();
                }
                if (su != null && su.Length > 0)
                {
                    if (su.Length == 1)
                    {
                        hasPole = true;
                        if (su[0] == periodicBounds.Left)
                        {
                            double fvu = 2.0 * Math.PI / periodicBounds.Height;
                            double fuv = 1.0 / periodicBounds.Width;
                            toNonPeriodicBounds = new ModOp2D(0, fvu, -fvu * periodicBounds.Bottom, fuv, 0, -fuv * periodicBounds.Left);
                        }
                        else if (su[0] == periodicBounds.Right)
                        {
                            double fvu = 2.0 * Math.PI / periodicBounds.Height;
                            double fuv = 1.0 / periodicBounds.Width;
                            toNonPeriodicBounds = new ModOp2D(0, -fvu, fvu * periodicBounds.Top, fuv, 0, -fuv * periodicBounds.Left);
                        }
                        else throw new ApplicationException("pole must be on border");
                    }
                    else throw new ApplicationException("more than one pole");
                }
                else
                {
                    double fvu = 2.0 * Math.PI / periodicBounds.Height;
                    double fuv = 1.0 / periodicBounds.Width;
                    toNonPeriodicBounds = new ModOp2D(0, fvu, -fvu * periodicBounds.Bottom, fuv, 0, -fuv * periodicBounds.Left + 0.5);
                }
            }
            else
            {   // not periodic, only pole removal
                // map the "periodic" parameter (where there is a singularity) onto [0..pi/2], the other parameter to [0..1]
                bool done = false;
                double[] su = periodicSurface.GetUSingularities();
                if (su != null && su.Length == 1)
                {   // the "period" to be resolved is in v, when u==su[0] you can change v and the point stays fixed
                    hasPole = true;
                    if (Math.Abs(su[0] - periodicBounds.Left) < periodicBounds.Width * 1e-6)
                    {   // we have to map the v interval of the periodic surface onto [0..pi/2] and the u interval onto [0..1]
                        // and we also have to exchange u and v, so the non periodic bounds are [0..pi/2] in u and [0..1] in v
                        double fvu = Math.PI / 2.0 / periodicBounds.Height; // tested: ok
                        double fuv = 1.0 / periodicBounds.Width;
                        toNonPeriodicBounds = new ModOp2D(0, fvu, -fvu * periodicBounds.Bottom, fuv, 0, -fuv * periodicBounds.Left);
                    }
                    else if (Math.Abs(su[0] - periodicBounds.Right) < periodicBounds.Width * 1e-6)
                    {   // here we additionally have tor reverse the v interval
                        double fvu = Math.PI / 2.0 / periodicBounds.Height; // tested: ok
                        double fuv = 1.0 / periodicBounds.Width;
                        toNonPeriodicBounds = new ModOp2D(0, -fvu, fvu * periodicBounds.Top, -fuv, 0, fuv * periodicBounds.Right);
                    }
                    else throw new ApplicationException("the pole must be on the border of the bounds");
                    done = true;
                }
                if (!done)
                {
                    double[] sv = periodicSurface.GetVSingularities();
                    if (sv != null && sv.Length == 1)
                    {   // the "period" to be resolved is in u
                        hasPole = true;
                        if (Math.Abs(sv[0] - periodicBounds.Bottom) < periodicBounds.Height * 1e-6)
                        {   // we have to map the u interval of the periodic surface onto [0..pi/2] and the v interval onto [0..1]
                            double fvu = Math.PI / 2.0 / periodicBounds.Width; // tested: ok
                            double fuv = 1.0 / periodicBounds.Height;
                            toNonPeriodicBounds = new ModOp2D(-fvu, 0, fvu * periodicBounds.Right, 0, fuv, -fuv * periodicBounds.Bottom);
                        }
                        else if (Math.Abs(sv[0] - periodicBounds.Top) < periodicBounds.Height * 1e-6)
                        {
                            double fvu = Math.PI / 2.0 / periodicBounds.Width; // tested: ok
                            double fuv = 1.0 / periodicBounds.Height;
                            toNonPeriodicBounds = new ModOp2D(fvu, 0, -fvu * periodicBounds.Left, 0, -fuv, fuv * periodicBounds.Top);
                        }
                        else throw new ApplicationException("the pole must be on the border of the bounds");
                        done = true;
                    }
                }
                if (!done) throw new ApplicationException("there must be a single pole on the border of the bounds");
            }
            toPeriodicBounds = toNonPeriodicBounds.GetInverse();
#if DEBUG
            //GeoObjectList dbg = this.DebugGrid;
            //GeoObjectList dbgp = (this.periodicSurface as ISurfaceImpl).DebugGrid;
#endif
        }
        public override GeoVector UDirection(GeoPoint2D uv)
        {
            periodicSurface.DerivationAt(toPeriodic(uv), out GeoPoint ploc, out GeoVector pdu, out GeoVector pdv);
            double l = uv.x * uv.x + uv.y * uv.y;
            double sl = Math.Sqrt(l);
            double dsdu, dtdu;
            if (l > 0)
            {
                dsdu = toPeriodicBounds[0, 1] * uv.x / sl - toPeriodicBounds[0, 0] * uv.y / l;
                dtdu = toPeriodicBounds[1, 1] * uv.x / sl - toPeriodicBounds[1, 0] * uv.y / l;
                return dsdu * pdu + dtdu * pdv;
            }
            else
            {   // toPeriodic at (0,0) did return toPeriodicBounds * (0,0), but we also need toPeriodicBounds * (pi/2,0), because at (0,0) there is the pole
                GeoPoint2D pole1 = toPeriodicBounds * new GeoPoint2D(Math.PI / 2.0, 0.0);
                periodicSurface.DerivationAt(pole1, out GeoPoint ploc1, out GeoVector pdu1, out GeoVector pdv1);
                dsdu = toPeriodicBounds[0, 1] - toPeriodicBounds[0, 0];
                dtdu = toPeriodicBounds[1, 1] - toPeriodicBounds[1, 0];
                return dsdu * pdu + dtdu * pdv;
            }
        }
        public override GeoVector VDirection(GeoPoint2D uv)
        {
            double l = uv.x * uv.x + uv.y * uv.y;
            double sl = Math.Sqrt(l);
            double dsdv, dtdv;
            if (l > 0)
            {
                periodicSurface.DerivationAt(toPeriodic(uv), out GeoPoint ploc, out GeoVector pdu, out GeoVector pdv);
                dsdv = toPeriodicBounds[0, 1] * uv.y / sl + toPeriodicBounds[0, 0] * uv.x / l;
                dtdv = toPeriodicBounds[1, 1] * uv.y / sl + toPeriodicBounds[1, 0] * uv.x / l;
                return dsdv * pdu + dtdv * pdv;
            }
            else
            {   // toPeriodic at (0,0) did return toPeriodicBounds * (0,0), but we also need toPeriodicBounds * (pi/2,0), because at (0,0) there is the pole
                GeoPoint2D pole1 = toPeriodicBounds * new GeoPoint2D(Math.PI / 2.0, 0.0);
                periodicSurface.DerivationAt(pole1, out GeoPoint ploc1, out GeoVector pdu1, out GeoVector pdv1);
                dsdv = toPeriodicBounds[0, 1] + toPeriodicBounds[0, 0];
                dtdv = toPeriodicBounds[1, 1] + toPeriodicBounds[1, 0];
                return dsdv * pdu1 + dtdv * pdv1;
            }
        }

        public override void DerivationAt(GeoPoint2D uv, out GeoPoint location, out GeoVector du, out GeoVector dv)
        {
            periodicSurface.DerivationAt(toPeriodic(uv), out GeoPoint ploc, out GeoVector pdu, out GeoVector pdv);
            location = ploc;
            double l = uv.x * uv.x + uv.y * uv.y;
            double sl = Math.Sqrt(l);
            double dsdu, dsdv, dtdu, dtdv;
            if (l > 0)
            {
                dsdu = toPeriodicBounds[0, 1] * uv.x / sl - toPeriodicBounds[0, 0] * uv.y / l;
                dsdv = toPeriodicBounds[0, 1] * uv.y / sl + toPeriodicBounds[0, 0] * uv.x / l;
                dtdu = toPeriodicBounds[1, 1] * uv.x / sl - toPeriodicBounds[1, 0] * uv.y / l;
                dtdv = toPeriodicBounds[1, 1] * uv.y / sl + toPeriodicBounds[1, 0] * uv.x / l;
                du = dsdu * pdu + dtdu * pdv;
                dv = dsdv * pdu + dtdv * pdv;
            }
            else
            {   // toPeriodic at (0,0) did return toPeriodicBounds * (0,0), but we also need toPeriodicBounds * (pi/2,0), because at (0,0) there is the pole
                GeoPoint2D pole1 = toPeriodicBounds * new GeoPoint2D(Math.PI / 2.0, 0.0);
                periodicSurface.DerivationAt(pole1, out GeoPoint ploc1, out GeoVector pdu1, out GeoVector pdv1);
                dsdu = toPeriodicBounds[0, 1] - toPeriodicBounds[0, 0];
                dsdv = toPeriodicBounds[0, 1] + toPeriodicBounds[0, 0];
                dtdu = toPeriodicBounds[1, 1] - toPeriodicBounds[1, 0];
                dtdv = toPeriodicBounds[1, 1] + toPeriodicBounds[1, 0];
                du = dsdu * pdu + dtdu * pdv;
                dv = dsdv * pdu1 + dtdv * pdv1;
            }

        }
        public override GeoPoint PointAt(GeoPoint2D uv)
        {
            return periodicSurface.PointAt(toPeriodic(uv));
        }
        public override void Derivation2At(GeoPoint2D uv, out GeoPoint location, out GeoVector du, out GeoVector dv, out GeoVector duu, out GeoVector dvv, out GeoVector duv)
        {
            periodicSurface.Derivation2At(toPeriodic(uv), out GeoPoint ploc, out GeoVector pdu, out GeoVector pdv, out GeoVector pduu, out GeoVector pdvv, out GeoVector pduv);
            location = ploc;
            // toPeriodic 
            // s := m00*atan2(v, u)+m01*sqrt(u^2 + v^2)+m02;
            // t := m10*atan2(v, u)+m11*sqrt(u^2 + v^2)+m12;
            // ds/du: (m01*u)/sqrt(v^2+u^2)-(m00*v)/(v^2+u^2)
            // ds/dv: (m01*v)/sqrt(v^2+u^2)+(m00*u)/(v^2+u^2)
            // dt/du: (m11*u)/sqrt(v^2+u^2)-(m10*v)/(v^2+u^2)
            // dt/dv: (m11*v)/sqrt(v^2+u^2)+(m10*u)/(v^2+u^2)
            // g(u,v): f(m00*atan2(v, u)+m01*sqrt(u^2 + v^2)+m02,m10*atan2(v, u)+m11*sqrt(u^2 + v^2)+m12) == f(s,t)
            // dg(u,v)/du: ds/du * df/ds + dt/du * df/dt;
            // dg(u,v)/dudu: ds/dudu * df/ds + ds/du* d(df/ds)/du    +    dt/dudu * df/dt + dt/du * d(df/dt)/du
            // == ds/dudu * df/ds + ds/du* (ds/du*(df/dsds) + dt/du*(df/dsdt)
            //  + dt/dudu * df/dt + dt/du * (ds/du*df/dtds) + dt/du*(df/dtdt))
            double l = uv.x * uv.x + uv.y * uv.y;
            double sl = Math.Sqrt(l);

            double dsdu = toPeriodicBounds[0, 1] * uv.x / sl - toPeriodicBounds[0, 0] * uv.y / l;
            double dsdv = toPeriodicBounds[0, 1] * uv.y / sl + toPeriodicBounds[0, 0] * uv.x / l;
            double dsdudu = toPeriodicBounds[0, 1] / sl - (toPeriodicBounds[0, 1] * uv.x * uv.x) / exp32(l) + (2 * toPeriodicBounds[0, 0] * uv.x * uv.y) / (l * l);
            double dsdvdv = toPeriodicBounds[0, 1] / sl - (toPeriodicBounds[0, 1] * uv.y * uv.y) / exp32(l) + (2 * toPeriodicBounds[0, 0] * uv.x * uv.y) / (l * l);
            double dsdudv = -toPeriodicBounds[0, 0] / (l) - (toPeriodicBounds[0, 1] * uv.x * uv.y) / exp32(l) + (2 * toPeriodicBounds[0, 0] * uv.y * uv.y) / (l * l);

            double dtdu = toPeriodicBounds[1, 1] * uv.x / sl - toPeriodicBounds[1, 0] * uv.y / l;
            double dtdv = toPeriodicBounds[1, 1] * uv.y / sl + toPeriodicBounds[1, 0] * uv.x / l;
            double dtdudu = toPeriodicBounds[1, 1] / sl - (toPeriodicBounds[1, 1] * uv.x * uv.x) / exp32(l) + (2 * toPeriodicBounds[1, 0] * uv.x * uv.y) / (l * l);
            double dtdvdv = toPeriodicBounds[1, 1] / sl - (toPeriodicBounds[1, 1] * uv.y * uv.y) / exp32(l) + (2 * toPeriodicBounds[1, 0] * uv.x * uv.y) / (l * l);
            double dtdudv = -toPeriodicBounds[1, 0] / (l) - (toPeriodicBounds[1, 1] * uv.x * uv.y) / exp32(l) + (2 * toPeriodicBounds[1, 0] * uv.y * uv.y) / (l * l);

            du = dsdu * pdu + dtdu * pdv;
            dv = dsdv * pdu + dtdv * pdv;
            duu = dsdudu * pdu + dsdu * (dsdu * pduu + dtdu * pduv) + dtdudu * pdv + dtdu * (dsdu * pduv + dtdu * pdvv);
            dvv = dsdvdv * pdu + dsdv * (dsdv * pduu + dtdv * pduv) + dtdvdv * pdv + dtdv * (dsdv * pduv + dtdv * pdvv);
            duv = dsdudv * pdu + dsdu * (dsdv * pduu + dtdv * pduv) + dtdudv * pdv + dtdu * (dsdu * pduv + dtdu * pdvv);
        }
        public override ICurve FixedU(double u, double vmin, double vmax)
        {
            Line2D l2d = new Line2D(new GeoPoint2D(u, vmin), new GeoPoint2D(u, vmax));
            return new SurfaceCurve(this, l2d);
        }
        public override ICurve FixedV(double v, double umin, double umax)
        {
            Line2D l2d = new Line2D(new GeoPoint2D(umin, v), new GeoPoint2D(umax, v));
            return new SurfaceCurve(this, l2d);
        }
        public override ISurface GetModified(ModOp m)
        {
            return new NonPeriodicSurface(periodicSurface.GetModified(m), periodicBounds);
        }
        public override void GetSafeParameterSteps(double umin, double umax, double vmin, double vmax, out double[] intu, out double[] intv)
        {   // here we want to provide parameter steps so that there are invalid patches, which are completely outside the annulus or circle.
            // The boxed surface ignores patches which return false for "IsInside" for all its vertices. And with this segmentation, 
            // these pathological patches are avoided.
            List<double> usteps = new List<double>();
            List<double> vsteps = new List<double>();
            if (hasPole)
            {
                // a unit circle: the four corner patches are outside of the circular area
                if (umin > -1.0) usteps.Add(umin);
                if (vmin > -1.0) vsteps.Add(vmin);
                for (int i = 0; i < 8; i++)
                {
                    double d = i * 2 / 7.0 - 1.0;
                    if (d >= umin && d <= umax) usteps.Add(d);
                    if (d >= vmin && d <= vmax) vsteps.Add(d);
                }
                if (umax < 1.0) usteps.Add(umax);
                if (vmax < 1.0) vsteps.Add(vmax);
                intu = usteps.ToArray();
                intv = vsteps.ToArray();
            }
            else
            {
                // a annulus from -1.5 to 1.5: the four corner patches and the central patch are totally outside of the annulus
                // The central patch may contain an infinite pole
                if (umin > -1.5) usteps.Add(umin);
                if (vmin > -1.5) vsteps.Add(vmin);
                for (int i = 0; i < 8; i++)
                {
                    double d = i * 3 / 7.0 - 1.5;
                    if (d >= umin && d <= umax) usteps.Add(d);
                    if (d >= vmin && d <= vmax) vsteps.Add(d);
                }
                if (umax < 1.5) usteps.Add(umax);
                if (vmax < 1.5) vsteps.Add(vmax);
                intu = usteps.ToArray();
                intv = vsteps.ToArray();
            }
        }
        public override void GetNaturalBounds(out double umin, out double umax, out double vmin, out double vmax)
        {   // The periodic surface (or the surface with a pole) comes with the periodic bounds: typically in the periodic direction this is [0..2*pi] or [0..1]
            // and in the non periodic direction it is some min and max value.
            // In this surface, this area is mapped onto the unit circle (when there is a pole) or onto a ring (no pole, ring radius 0.5 to 1.5). 
            // The ring or circle is full when fullPeriod is true, or only the 1. quadrant when fullPeriod is false.
            if (fullPeriod)
            {
                if (hasPole)
                {
                    umin = -1; umax = 1;
                    vmin = -1; vmax = 1;
                }
                else
                {
                    umin = -1.5; umax = 1.5;
                    vmin = -1.5; vmax = 1.5;
                }
            }
            else
            {
                if (hasPole)
                {
                    umin = 0; umax = 1;
                    vmin = 0; vmax = 1;
                }
                else
                {
                    umin = 0; umax = 1.5;
                    vmin = 0; vmax = 1.5;
                }
            }
        }
#if DEBUG
        public override GeoObjectList DebugGrid
        {
            get
            {
                GetNaturalBounds(out double umin, out double umax, out double vmin, out double vmax);
                BoundingCube ext = BoundingCube.EmptyBoundingCube;
                ext.MinMax(PointAt(new GeoPoint2D(umin, vmin)));
                ext.MinMax(PointAt(new GeoPoint2D(umax, vmin)));
                ext.MinMax(PointAt(new GeoPoint2D(umin, vmax)));
                ext.MinMax(PointAt(new GeoPoint2D(umax, vmax)));
                double precision = ext.Size * 1e-4;
                GeoObjectList res = new GeoObjectList();
                int n = 25;
                for (int i = 0; i <= n; i++)
                {
                    Line2D hor = new Line2D(new GeoPoint2D(umin, vmin + i * (vmax - vmin) / n), new GeoPoint2D(umax, vmin + i * (vmax - vmin) / n));
                    Line2D ver = new Line2D(new GeoPoint2D(umin + i * (umax - umin) / n, vmin), new GeoPoint2D(umin + i * (umax - umin) / n, vmax));
                    double[] hc = Clip(hor);
                    for (int j = 0; j < hc.Length; j += 2)
                    {
                        ICurve2D c2d = hor.Trim(hc[j], hc[j + 1]);
                        res.Add(this.Make3dCurve(c2d).Approximate(true, precision) as IGeoObject);
                    }
                    double[] vc = Clip(hor);
                    for (int j = 0; j < vc.Length; j += 2)
                    {
                        ICurve2D c2d = ver.Trim(vc[j], vc[j + 1]);
                        res.Add(this.Make3dCurve(c2d).Approximate(true, precision) as IGeoObject);
                    }
                    GeoPoint2D p0 = new GeoPoint2D(umin + i * (umax - umin) / n, vmin + i * (vmax - vmin) / n);
                    GeoPoint2D p1 = new GeoPoint2D(umin + (i + 1) * (umax - umin) / n, vmin + (i + 1) * (vmax - vmin) / n);
                    if (IsInside(p0) && IsInside(p1))
                    {
                        res.Add(Line.TwoPoints(PointAt(p0), PointAt(p1)));
                    }
                }
                return res;
            }
        }
        public override GeoObjectList DebugDirectionsGrid
        {
            get
            {
                GetNaturalBounds(out double umin, out double umax, out double vmin, out double vmax);
                GeoObjectList res = new GeoObjectList();
                int n = 25;
                double length = 0.0;
                GeoPoint2D p0 = new GeoPoint2D(umin, vmin);
                GeoPoint2D p1 = new GeoPoint2D(umax, vmax);
                Line2D diag = new Line2D(p0, p1);
                double[] hc = Clip(diag);
                ICurve2D c2d = diag.Trim(hc[0], hc[1]);
                length = this.Make3dCurve(c2d).Length / 30;
                Attribute.ColorDef cdu = new Attribute.ColorDef("diru", System.Drawing.Color.Red);
                Attribute.ColorDef cdv = new Attribute.ColorDef("dirv", System.Drawing.Color.Green);
                for (int i = 0; i <= n; i++)
                {
                    for (int j = 0; j <= n; j++)
                    {
                        GeoPoint2D p2d = new GeoPoint2D(umin + j * (umax - umin) / n, vmin + i * (vmax - vmin) / n);
                        if (IsInside(p2d))
                        {
                            DerivationAt(p2d, out GeoPoint loc, out GeoVector diru, out GeoVector dirv);
                            if ((loc | PointAt(p2d)) > length)
                            {
                                loc = PointAt(p2d);
                            }
                            if (diru.Length > 0.0)
                            {
                                Line l1 = Line.TwoPoints(loc, loc + length * diru.Normalized);
                                l1.ColorDef = cdu;
                                res.Add(l1);
                            }
                            if (dirv.Length > 0.0)
                            {
                                Line l2 = Line.TwoPoints(loc, loc + length * dirv.Normalized);
                                l2.ColorDef = cdv;
                                res.Add(l2);
                            }
                        }
                    }
                }

                return res;
            }
        }
#endif
        public override bool IsUPeriodic => false;
        public override bool IsVPeriodic => false;
        public override ModOp2D ReverseOrientation()
        {
            GeoPoint2D[] from = new GeoPoint2D[3];
            from[0] = new GeoPoint2D(0.5, 0.5);
            from[1] = new GeoPoint2D(0.5, 0.7);
            from[2] = new GeoPoint2D(0.7, 0.5);
            GeoPoint[] from3d = new GeoPoint[3];
            for (int i = 0; i < 3; i++)
            {
                from3d[i] = PointAt(from[i]);
            }
            periodicSurface.ReverseOrientation();
            Init(periodicSurface.IsUPeriodic);
            //toNonPeriodicBounds = toNonPeriodicBounds * ModOp2D.Scale(-1, 1);
            //toPeriodicBounds = toNonPeriodicBounds.GetInverse();
            GeoPoint2D[] to = new GeoPoint2D[3];
            for (int i = 0; i < 3; i++)
            {
                to[i] = PositionOf(from3d[i]);
            }
            return ModOp2D.Fit(from, to, true);
        }
        public override bool SameGeometry(BoundingRect thisBounds, ISurface other, BoundingRect otherBounds, double precision, out ModOp2D firstToSecond)
        {
            if (other is NonPeriodicSurface nps)
            {
                if (nps.periodicSurface.SameGeometry(nps.periodicBounds, periodicSurface, periodicBounds, precision, out ModOp2D periodicFirstToSecond))
                {
                    firstToSecond = ModOp2D.Null;
                    return true;
                }
                firstToSecond = ModOp2D.Null;
                return false;
            }
            return base.SameGeometry(thisBounds, other, otherBounds, precision, out firstToSecond);
        }
        public override IPropertyEntry GetPropertyEntry(IFrame frame)
        {
            return periodicSurface.GetPropertyEntry(frame);
        }
        private GeoPoint2D toPeriodic(GeoPoint2D uv)
        {
            return toPeriodicBounds * new GeoPoint2D(Math.Atan2(uv.y, uv.x), Math.Sqrt(uv.x * uv.x + uv.y * uv.y));
        }
        private GeoPoint2D fromPeriodic(GeoPoint2D uv)
        {
            GeoPoint2D np = toNonPeriodicBounds * uv;
            return new GeoPoint2D(np.y * Math.Cos(np.x), np.y * Math.Sin(np.x));
        }
        private GeoVector2D toPeriodic(GeoVector2D uv)
        {
            return toPeriodicBounds * new GeoVector2D(Math.Atan2(uv.y, uv.x), Math.Sqrt(uv.x * uv.x + uv.y * uv.y));
        }
        private GeoVector2D fromPeriodic(GeoVector2D uv)
        {
            GeoVector2D np = toNonPeriodicBounds * uv;
            return new GeoVector2D(np.y * Math.Cos(np.x), np.y * Math.Sin(np.x));
        }
        public override ISurface Clone()
        {
            NonPeriodicSurface res = new NonPeriodicSurface();
            res.periodicSurface = periodicSurface;
            res.periodicBounds = periodicBounds;
            res.hasPole = hasPole;
            res.fullPeriod = fullPeriod;
            res.toNonPeriodicBounds = toNonPeriodicBounds;
            res.toPeriodicBounds = toPeriodicBounds;
            return res;
        }
        public override void CopyData(ISurface CopyFrom)
        {
            NonPeriodicSurface nps = CopyFrom as NonPeriodicSurface;
            if (nps != null)
            {
                periodicSurface = nps.periodicSurface;
                periodicBounds = nps.periodicBounds;
                hasPole = nps.hasPole;
                fullPeriod = nps.fullPeriod;
                toNonPeriodicBounds = nps.toNonPeriodicBounds;
                toPeriodicBounds = nps.toPeriodicBounds;
            }
        }
        public override GeoPoint2D PositionOf(GeoPoint p)
        {
            GeoPoint2D uvp = periodicSurface.PositionOf(p);
            SurfaceHelper.AdjustPeriodic(periodicSurface, periodicBounds, ref uvp);
            return fromPeriodic(uvp);
        }
        void ISerializable.GetObjectData(SerializationInfo info, StreamingContext context)
        {
            info.AddValue("PeriodicSurface", periodicSurface);
            info.AddValue("PeriodicBounds", periodicBounds);
        }
        protected NonPeriodicSurface(SerializationInfo info, StreamingContext context)
        {
            periodicSurface = info.GetValue("PeriodicSurface", typeof(ISurface)) as ISurface;
            periodicBounds = (BoundingRect)info.GetValue("PeriodicBounds", typeof(BoundingRect));
        }
        void IDeserializationCallback.OnDeserialization(object sender)
        {
            bool uperiodic;
            if (periodicSurface.IsUPeriodic && periodicSurface.IsVPeriodic)
            {
                uperiodic = periodicBounds.Width == periodicSurface.UPeriod;
            }
            else
            {
                uperiodic = periodicSurface.IsUPeriodic;
            }
            Init(uperiodic);
        }
        public ISurface AdaptToCurves(IEnumerable<ICurve> curves)
        {
            BoundingRect domain = BoundingRect.EmptyBoundingRect;
            foreach (ICurve curve in curves)
            {
                ICurve2D c2d = periodicSurface.GetProjectedCurve(curve, 0.0);
                if (domain.IsEmpty()) domain = c2d.GetExtent();
                else
                {
                    SurfaceHelper.AdjustPeriodic(periodicSurface, domain, c2d);
                    domain.MinMax(c2d.GetExtent());
                }
            }
            return new NonPeriodicSurface(periodicSurface, domain);
        }
        public bool IsInside(GeoPoint2D uv)
        {
            double l = (uv - GeoPoint2D.Origin).Length;
            if (fullPeriod)
            {
                if (hasPole) return l >= 1.0; // shouldn't it be <=1.0?
                else return l >= 0.5 && l <= 1.5;
            }
            else
            {
                double a = Math.Atan2(uv.y, uv.x);
                return l <= 1.0 && a >= 0.0 && a <= Math.PI / 2.0;
            }
        }
        public GeoPoint RestrictedPointAt(GeoPoint2D uv)
        {
            double l = (uv - GeoPoint2D.Origin).Length;
            double a = Math.Atan2(uv.y, uv.x);
            if (fullPeriod)
            {
                if (hasPole) l = Math.Max(1.0, l);
                else
                {
                    if (l < 0.5) l = 0.5;
                    else if (l > 1.5) l = 1.5;
                }
            }
            else
            {
                if (l > 1.0) l = 1.0;
                if (a < 0.0) a = 0.0;
                if (a > Math.PI / 2.0) a = Math.PI / 2.0;
            }
            return periodicSurface.PointAt(toPeriodicBounds * new GeoPoint2D(a, l));
        }
        public double[] Clip(ICurve2D curve)
        {
            SimpleShape ss;
            if (fullPeriod)
            {
                if (hasPole) ss = new SimpleShape(Border.MakeCircle(GeoPoint2D.Origin, 1.0));
                else ss = new SimpleShape(Border.MakeCircle(GeoPoint2D.Origin, 1.5), Border.MakeCircle(GeoPoint2D.Origin, 0.5));
            }
            else
            {
                ss = new SimpleShape(new Border(new ICurve2D[] { new Line2D(GeoPoint2D.Origin, new GeoPoint2D(1, 0)), new Arc2D(GeoPoint2D.Origin, 1, 0, Math.PI / 2.0), new Line2D(new GeoPoint2D(0, 1), new GeoPoint2D(0, 0)) }));
            }
            return ss.Clip(curve, true);
        }
        /// <summary>
        /// Empty constructor for JSON serialization
        /// </summary>
        protected NonPeriodicSurface() { }
        public void GetObjectData(IJsonWriteData data)
        {
            data.AddProperty("PeriodicSurface", periodicSurface);
            data.AddProperty("PeriodicBounds", periodicBounds);
        }
        public void SetObjectData(IJsonReadData data)
        {
            periodicSurface = data.GetProperty<ISurface>("PeriodicSurface");
            periodicBounds = data.GetProperty<BoundingRect>("PeriodicBounds");
            data.RegisterForSerializationDoneCallback(this);
        }
        void IJsonSerializeDone.SerializationDone(JsonSerialize jsonSerialize)
        {
            bool uperiodic;
            if (periodicSurface.IsUPeriodic && periodicSurface.IsVPeriodic)
            {
                uperiodic = periodicBounds.Width == periodicSurface.UPeriod;
            }
            else
            {
                uperiodic = periodicSurface.IsUPeriodic;
            }
            Init(uperiodic);
        }

    }
}
