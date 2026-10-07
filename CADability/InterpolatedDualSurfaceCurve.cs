using CADability.Curve2D;
using CADability.GeoObject;
using CADability.Shapes;
using CADability.UserInterface;
using MathNet.Numerics.LinearAlgebra;
using MathNet.Numerics.LinearAlgebra.Double;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.Serialization;
using static CADability.InterpolatedDualSurfaceCurve.SurfacePoint;

namespace CADability
{
    /* Projekt: InterpolatedDualSurfaceCurve für Schnitte zwischen Ebene, Zylinder, Kugel, Kegel, Torus, SurfaceOfRevolution (einfach Form), SurfaceOfLinearExtrusion (einfache Form).
     * Die Idee: Nehmen wir Torus/Zylinder: die FixedU und FixedV Kurven diese Flächen sind Linien oder Kreise.
     * Wenn man jetzt die Kurvenschaar zu FixedU bzw. FixedV betrachtet, so sind die jeweiligen Schnitte mit der anderen Fläche einfach zu berechnen (Torus: naja...)
     * Aber leider gibt es die Fälle, wo diese Kurven tangential zur anderen Fläche liegen. An diesen Stellen wäre die jeweils andere FixedUV Kurve natürlich besser.
     * Es gilt also diese Tangentialpunkte zu bestimmen und damit die Bereiche festzulegen, in denen mit FixedU bzw. mit FixedV gearbeitet werden muss.
     * Die idealen Intervallgrenzen wären die, wo FixedU und FixedV mit dem gleichen Winkel zur Ebene im Schnittpunkt stehen. Die sind nicht unbedingt leicht zu finden.
     * Vielleicht genügt es ja, Zwischenpunkte zwischen den Tangentialpunkten zu nehmen. Das sollte recht unkritisch sein.
     * Man müsste von InterpolatedDualSurfaceCurve ableiten. Die Punktbestimmung läuft eigentlich ziemlich genau wie in ApproximatePosition.
     * Zusätzlich zu den BasePoints gibt es für jeden Abschnitt (es sind immer geschlossene Kurven) noch die Information, ob mit FixedU oder mit FixedV gearbeitet wird
     * und das Intervall des nicht festen Parameters für diesen Abschnitt. ApproximatePosition würde dann nicht mehr iterativ arbeiten, sondern direkt den Punkt finden.
     * PositionOf
     */

    /// <summary>
    /// Internal: ein Kante, gegeben durch zwei Oberflächen und ein Array von 3d/2d/2d Punkten
    /// </summary>
    [Serializable()]
    [JsonVersion(1)]
    public class InterpolatedDualSurfaceCurve : GeneralCurve, IDualSurfaceCurve, IJsonSerialize, IExportStep, IJsonSerializeDone, IOrientation
    {
        ISurface surface1; // the two surfaces
        ISurface surface2;
#if DEBUG
        internal
#endif
        SurfacePoint[] basePoints; // some points, especially start and endpoint, of the curve, that have been calculated
        bool isTangential = false; // we need a different point approximation for curves which describe the tangential intersection of two surfaces
        BSpline approxBSpline; // BSpline for approximation
        SortedList<double, SurfacePoint> hashedPositions; // already calculated points on the curve

        [Serializable()]
        [JsonVersion(serializeAsStruct = true, version = 1)]
        internal struct SurfacePoint : ISerializable, IJsonSerialize
        {
            public SurfacePoint(GeoPoint p3d, GeoPoint2D psurface1, GeoPoint2D psurface2)
            {
                this.p3d = p3d;
                this.psurface1 = psurface1;
                this.psurface2 = psurface2;
            }
            public GeoPoint p3d;
            public GeoPoint2D psurface1;
            public GeoPoint2D psurface2;

            static bool SnapToNearestPeriod(ref double curr, double prev, bool isPeriodic, double period)
            {
                if (!isPeriodic || period <= 0) return false;

                double delta = curr - prev;
                double snappedDelta = period * Math.Round(delta / period);

                if (snappedDelta != 0.0)
                {
                    curr -= snappedDelta;
                    return true;
                }
                return false;
            }
            internal static bool FixSurfacePoint2D(ref GeoPoint2D curr, in GeoPoint2D prev, bool isUPeriodic, double uPeriod, bool isVPeriodic, double vPeriod)
            {
                bool changed = false;
                changed |= SnapToNearestPeriod(ref curr.x, prev.x, isUPeriodic, uPeriod);
                changed |= SnapToNearestPeriod(ref curr.y, prev.y, isVPeriodic, vPeriod);
                return changed;
            }

            [Flags]
            internal enum SurfaceFixFlags
            {
                None = 0,
                Surface1 = 1,
                Surface2 = 2
            }
            internal SurfaceFixFlags FixAgainstNeighbour(in SurfacePoint prev, ISurface s1, ISurface s2)
            {
                SurfaceFixFlags changed = SurfaceFixFlags.None;
                if (FixSurfacePoint2D(ref psurface1, prev.psurface1, s1.IsUPeriodic, s1.UPeriod, s1.IsVPeriodic, s1.VPeriod)) changed |= SurfaceFixFlags.Surface1;
                if (FixSurfacePoint2D(ref psurface2, prev.psurface2, s2.IsUPeriodic, s2.UPeriod, s2.IsVPeriodic, s2.VPeriod)) changed |= SurfaceFixFlags.Surface2;
                return changed;
            }

            #region ISerializable Members
            public SurfacePoint(SerializationInfo info, StreamingContext context)
            {
                p3d = (GeoPoint)info.GetValue("P3d", typeof(GeoPoint));
                psurface1 = (GeoPoint2D)info.GetValue("Surface1", typeof(GeoPoint2D));
                psurface2 = (GeoPoint2D)info.GetValue("Surface2", typeof(GeoPoint2D));
            }
            void ISerializable.GetObjectData(SerializationInfo info, StreamingContext context)
            {
                info.AddValue("P3d", p3d);
                info.AddValue("Surface1", psurface1);
                info.AddValue("Surface2", psurface2);
            }
            public SurfacePoint(IJsonReadStruct data)
            {
                p3d = data.GetValue<GeoPoint>();
                psurface1 = data.GetValue<GeoPoint2D>();
                psurface2 = data.GetValue<GeoPoint2D>();
            }

            public void GetObjectData(IJsonWriteData data)
            {
                data.AddValues(p3d, psurface1, psurface2);
            }

            public void SetObjectData(IJsonReadData data)
            {
            }

            #endregion
        }
#if DEBUG
        static int idcnt = 0;
        int id;
#endif
        /// <summary>
        /// The first or the second surface has been reparametrized in place by <paramref name="m"/>, see
        /// <see cref="ISurface.ReverseOrientation"/>: the uv values stored for it follow.
        /// </summary>
        internal void SurfaceReparametrized(bool onSurface1, ModOp2D m)
        {
            ModifySurfacePoints(onSurface1, m);
        }
        private void ModifySurfacePoints(bool onSurface1, ModOp2D m)
        {
            DualSurfaceCurveDiagnostics.Count("IDSC.ModifySurfacePoints" + (m.Determinant < 0 ? ", orientation reversed" : ""));
            for (int i = 0; i < basePoints.Length; i++)
            {
                if (onSurface1) basePoints[i].psurface1 = m * basePoints[i].psurface1;
                else basePoints[i].psurface2 = m * basePoints[i].psurface2;
            }
            InvalidateSecondaryData();
        }

        protected InterpolatedDualSurfaceCurve()
        {
            hashedPositions = new SortedList<double, SurfacePoint>();
#if DEBUG
            id = idcnt++;
#endif
        }
        internal InterpolatedDualSurfaceCurve(ISurface surface1, ISurface surface2, SurfacePoint[] basePoints, bool isTangential = false)
            : this()
        {
            DualSurfaceCurveDiagnostics.ConstructionProbe probe = DualSurfaceCurveDiagnostics.BeginConstruction();
            // der 1. und der letzte Punkt müssen exakt sein, die anderen nur Näherungswerte, die aber eindeutig zur Fläche führen
            double dbg = basePoints[0].p3d | basePoints[basePoints.Length - 1].p3d;
            this.surface1 = surface1;
            this.surface2 = surface2;
            this.basePoints = basePoints;
            this.isTangential = isTangential;
            if (basePoints.Length == 2) RefineBasePoints();
            AnchorBasePoints();
            BSpline toUpdateBasepoints = ApproxBSpline;
            DualSurfaceCurveDiagnostics.EndConstruction(probe, this.surface1, this.surface2, this.basePoints, this.isTangential);
        }
        private void Init()
        {
            if (basePoints.Length == 2) RefineBasePoints();
            AnchorBasePoints();
            BSpline toUpdateBasepoints = ApproxBSpline;
        }
        /// <summary>
        /// <see cref="Init"/> for a curve read from a file. The uv values stay in the periods they were written in and are
        /// only made continuous: the 2d curves on the faces refer to these periods, see the periods a
        /// <see cref="ProjectedCurve"/> of an intersection is moved by. Moving them to where PositionOf puts them now would
        /// depend on the domain of the surface, which not every kind of surface writes (a cylinder writes none), and would
        /// shift those 2d curves by a period.
        /// </summary>
        private void InitAfterReading()
        {
            if (basePoints.Length == 2) RefineBasePoints();
            AnchorBasePoints(surface1, true, false);
            AnchorBasePoints(surface2, false, false);
            BSpline toUpdateBasepoints = ApproxBSpline;
        }
        /// <summary>
        /// The intersection curve of two surfaces from <paramref name="startPoint"/> to <paramref name="endPoint"/>.
        /// <paramref name="bounds1"/> and <paramref name="bounds2"/> are not used: the uv values follow from
        /// <see cref="ISurface.PositionOf"/>, which honours the domain of each surface.
        /// </summary>
        public InterpolatedDualSurfaceCurve(ISurface surface1, BoundingRect bounds1, ISurface surface2, BoundingRect bounds2, GeoPoint startPoint, GeoPoint endPoint, bool isTangential = false)
            : this()
        {
            DualSurfaceCurveDiagnostics.ConstructionProbe probe = DualSurfaceCurveDiagnostics.BeginConstruction();
            this.surface1 = surface1;
            this.surface2 = surface2;
            this.isTangential = isTangential;
            List<SurfacePoint> points = new List<SurfacePoint>();
            SurfacePoint sp = new SurfacePoint(startPoint, surface1.PositionOf(startPoint), surface2.PositionOf(startPoint));
            points.Add(sp);
            SurfacePoint ep = new SurfacePoint(endPoint, surface1.PositionOf(endPoint), surface2.PositionOf(endPoint));
            ep.FixAgainstNeighbour(sp, surface1, surface2);
            points.Add(ep);
            basePoints = points.ToArray();
            CheckPeriodic();
            points.Clear();
            points.AddRange(basePoints); // damit die periodic Änderungen auch dort wirksam sind
            while (points.Count < 9)
            {
                double maxdist = double.MinValue;
                int ind = -1;
                for (int i = 0; i < points.Count - 1; ++i)
                {
                    double d = points[i].p3d | points[i + 1].p3d;
                    if (d > maxdist)
                    {
                        maxdist = d;
                        ind = i;
                    }
                }
                GeoPoint2D uv1, uv2;
                GeoPoint p;
                ApproximatePosition((ind + 0.5) / (basePoints.Length - 1), out uv1, out uv2, out p);
                points.Insert(ind + 1, new SurfacePoint(p, uv1, uv2));
                basePoints = points.ToArray(); // damit basePoints für die nächste Runde zu Verfügung steht
            }
            AnchorBasePoints();
            BSpline bsp = ApproxBSpline; // make sure it is created and the basepoints are refined
            hashedPositions.Clear();
            CheckPeriodic();
            DualSurfaceCurveDiagnostics.EndConstruction(probe, this.surface1, this.surface2, this.basePoints, this.isTangential);
        }
        /// <summary>
        /// The intersection curve of two surfaces through <paramref name="pts"/>, see the constructor with a list of points.
        /// </summary>
        public InterpolatedDualSurfaceCurve(ISurface surface1, BoundingRect bounds1, ISurface surface2, BoundingRect bounds2, GeoPoint[] pts, List<GeoPoint2D> uvpts1 = null, List<GeoPoint2D> uvpts2 = null, bool isTangential = false, BSpline approxBSpline = null)
        : this(surface1, bounds1, surface2, bounds2, pts.ToList(), uvpts1, uvpts2, isTangential, approxBSpline)
        {
        }
        /// <summary>
        /// The intersection curve of two surfaces through <paramref name="pts"/>, where the first and the last point must be
        /// exact and the inner ones are refined onto both surfaces. <paramref name="uvpts1"/> and <paramref name="uvpts2"/>
        /// may give the uv values of the points, otherwise they follow from <see cref="ISurface.PositionOf"/>, which honours
        /// the domain of each surface. <paramref name="bounds1"/> and <paramref name="bounds2"/> are only used when there are
        /// just two points on a periodic surface: as the range in which an intermediate point is searched.
        /// </summary>
        public InterpolatedDualSurfaceCurve(ISurface surface1, BoundingRect bounds1, ISurface surface2, BoundingRect bounds2, List<GeoPoint> pts, List<GeoPoint2D> uvpts1 = null, List<GeoPoint2D> uvpts2 = null, bool isTangential = false, BSpline approxBSpline = null)
            : this()
        {
            DualSurfaceCurveDiagnostics.ConstructionProbe probe = DualSurfaceCurveDiagnostics.BeginConstruction();
            this.surface1 = surface1;
            this.surface2 = surface2;
            this.isTangential = isTangential;
            List<SurfacePoint> points = new List<SurfacePoint>();
            for (int i = 0; i < pts.Count; ++i)
            {
                SurfacePoint sp = new SurfacePoint();
                sp.p3d = pts[i];
                // without given uv values PositionOf, which honours the domain, and each point next to its predecessor
                if (uvpts1 != null)
                    sp.psurface1 = uvpts1[i];
                else
                {
                    sp.psurface1 = surface1.PositionOf(sp.p3d);
                    if (i > 0) SurfacePoint.FixSurfacePoint2D(ref sp.psurface1, points[i - 1].psurface1, surface1.IsUPeriodic, surface1.UPeriod, surface1.IsVPeriodic, surface1.VPeriod);
                }
                if (uvpts2 != null)
                    sp.psurface2 = uvpts2[i];
                else
                {
                    sp.psurface2 = surface2.PositionOf(sp.p3d);
                    if (i > 0) SurfacePoint.FixSurfacePoint2D(ref sp.psurface2, points[i - 1].psurface2, surface2.IsUPeriodic, surface2.UPeriod, surface2.IsVPeriodic, surface2.VPeriod);
                }
                points.Add(sp);
            }
            if (points.Count == 2)
            {   // sometimes we have an ambiguous curve here: a half circle on a rotational surface, which could be either way around.
                // since "ApproximatePosition" doesn't care about the u/v bounds, we try a different approach here: choose a fixed u or v curve
                // in the bounds of such a surface and intersect with the other surface. bounds1 and bounds2 are the parameters of this
                // constructor, the range in which the caller expects the curve
                List<GeoPoint> intermediatePoints = new List<GeoPoint>();
                if (surface1.IsUPeriodic && Math.Abs(points[0].psurface1.x - points[1].psurface1.x) > surface1.UPeriod / 3.0)
                {   // the curve spans more than 1/3 of a total period
                    ICurve fixedCurve = surface1.FixedU((points[0].psurface1.x + points[1].psurface1.x) / 2.0, bounds1.Bottom, bounds1.Top);
                    surface2.Intersect(fixedCurve, bounds2, out GeoPoint[] ips, out GeoPoint2D[] uvOn2, out double[] uOnCurve3Ds);
                    intermediatePoints.AddRange(ips);
                }
                if (surface1.IsVPeriodic && Math.Abs(points[0].psurface1.y - points[1].psurface1.y) > surface1.VPeriod / 3.0)
                {   // the curve spans more than 1/3 of a total period
                    ICurve fixedCurve = surface1.FixedV((points[0].psurface1.y + points[1].psurface1.y) / 2.0, bounds1.Left, bounds1.Right);
                    surface2.Intersect(fixedCurve, bounds2, out GeoPoint[] ips, out GeoPoint2D[] uvOn2, out double[] uOnCurve3Ds);
                    intermediatePoints.AddRange(ips);
                }
                if (surface2.IsUPeriodic && Math.Abs(points[0].psurface2.x - points[1].psurface2.x) > surface2.UPeriod / 3.0)
                {   // the curve spans more than 1/3 of a total period
                    ICurve fixedCurve = surface2.FixedU((points[0].psurface2.x + points[1].psurface2.x) / 2.0, bounds2.Bottom, bounds2.Top);
                    surface1.Intersect(fixedCurve, bounds1, out GeoPoint[] ips, out GeoPoint2D[] uvOn2, out double[] uOnCurve3Ds);
                    intermediatePoints.AddRange(ips);
                }
                if (surface2.IsVPeriodic && Math.Abs(points[0].psurface2.y - points[1].psurface2.y) > surface2.VPeriod / 3.0)
                {   // the curve spans more than 1/3 of a total period
                    ICurve fixedCurve = surface2.FixedV((points[0].psurface2.y + points[1].psurface2.y) / 2.0, bounds2.Left, bounds2.Right);
                    surface1.Intersect(fixedCurve, bounds1, out GeoPoint[] ips, out GeoPoint2D[] uvOn2, out double[] uOnCurve3Ds);
                    intermediatePoints.AddRange(ips);
                }
                if (intermediatePoints.Count > 0)
                {
                    double mindist = double.MaxValue;
                    int ind = -1;
                    for (int i = 0; i < intermediatePoints.Count; i++)
                    {
                        double d = (intermediatePoints[i] | points[0].p3d) + (intermediatePoints[i] | points[1].p3d);
                        if (d < mindist)
                        {
                            mindist = d;
                            ind = i;
                        }
                    }
                    if (ind >= 0)
                    {
                        SurfacePoint sp = new SurfacePoint();
                        sp.p3d = intermediatePoints[ind];
                        sp.psurface1 = surface1.PositionOf(sp.p3d);
                        sp.psurface2 = surface2.PositionOf(sp.p3d);
                        sp.FixAgainstNeighbour(points[0], surface1, surface2);
                        points.Insert(1, sp);
                    }
                }
            }
            basePoints = points.ToArray();

            // Recalculate the positions of the inner points, which are sometimes not precise
            for (int i = 1; i < basePoints.Length - 1; ++i)
            {
                GeoPoint2D uv1, uv2; // do not pass out basePoints[i].psurface1 as parameter, since uv1 is manipulated several times inside ApproximatePosition
                ApproximatePosition((double)i / (double)(basePoints.Length - 1), out uv1, out uv2, out basePoints[i].p3d);
                basePoints[i].psurface1 = uv1;
                basePoints[i].psurface2 = uv2;
                hashedPositions.Clear(); // die Werte hier sind unnütz, da die basePoints sich ja immer noch ändern
            }
            AnchorBasePoints();

            points.Clear();
            points.AddRange(basePoints); // damit die periodic Änderungen auch dort wirksam sind
            while (points.Count < 9)
            {
                double maxdist = double.MinValue;
                int ind = -1;
                for (int i = 0; i < points.Count - 1; ++i)
                {
                    double d = points[i].p3d | points[i + 1].p3d;
                    if (d > maxdist)
                    {
                        maxdist = d;
                        ind = i;
                    }
                }
                GeoPoint2D uv1, uv2;
                GeoPoint p;
                ApproximatePosition((ind + 0.5) / (basePoints.Length - 1), out uv1, out uv2, out p);
                hashedPositions.Clear(); // die Werte hier sind unnütz, da die basePoints sich ja immer noch ändern
                points.Insert(ind + 1, new SurfacePoint(p, uv1, uv2));
                basePoints = points.ToArray(); // damit basePoints für die nächste Runde zu Verfügung steht
            }
            double baseLength = 0.0;
            double minLength = double.MaxValue;
            int mlInd = -1;
            for (int i = 1; i < points.Count; i++)
            {
                double d = points[i].p3d | points[i - 1].p3d;
                if (d < minLength)
                {
                    minLength = d;
                    mlInd = i;
                }
                baseLength += d;
            }
            // remove basepoints, which are too close
            double avgLength = baseLength / (points.Count - 1);
            while (minLength < avgLength * 0.1 && (points.Count > 9 || minLength < Precision.eps))
            {
                int toRemove = mlInd;
                if (toRemove == points.Count - 1) --toRemove;
                else if (toRemove == 0) toRemove = 1;
                else if (toRemove > 1 && toRemove < points.Count - 2)
                {
                    double d1 = points[mlInd].p3d | points[mlInd + 1].p3d;
                    double d2 = points[mlInd - 1].p3d | points[mlInd - 2].p3d;
                    if (d1 > d2) --toRemove;
                }
                points.RemoveAt(toRemove);
                minLength = double.MaxValue;
                mlInd = -1;
                for (int i = 1; i < points.Count; i++)
                {
                    double d = points[i].p3d | points[i - 1].p3d;
                    if (d < minLength)
                    {
                        minLength = d;
                        mlInd = i;
                    }
                }
            }
            basePoints = points.ToArray();

            AnchorBasePoints();
            this.approxBSpline = approxBSpline; // may be null, the it will be calculated in the next line
            BSpline bsp = ApproxBSpline; // make sure it is created and the basepoints are refined
            CheckPeriodic(); // erst nach dieser Schleife, denn ApproximatePosition mach die uv-position evtl. falsch
            AnchorBasePoints();

            DualSurfaceCurveDiagnostics.EndConstruction(probe, this.surface1, this.surface2, this.basePoints, this.isTangential);
        }
        internal void Repair()
        {
            DualSurfaceCurveDiagnostics.Count("IDSC.Repair");
            BoundingBox ext = BoundingBox.EmptyBoundingCube;
            for (int i = 0; i < basePoints.Length; i++) ext.MinMax(basePoints[i].p3d);
            double eps = ext.Size * 1e-5;
            bool needsRepair = false;
            for (int i = 0; i < basePoints.Length; i++)
            {
                if ((surface1.PointAt(basePoints[i].psurface1) | basePoints[i].p3d) > eps)
                {
                    needsRepair = true;
                    break;
                }
                if ((surface2.PointAt(basePoints[i].psurface2) | basePoints[i].p3d) > eps)
                {
                    needsRepair = true;
                    break;
                }
            }
            if (needsRepair)
            {
                DualSurfaceCurveDiagnostics.Count("IDSC.Repair: needed a repair");
                RecalcSurfacePoints();
            }
        }
        private void CheckPeriodic()
        {
            for (int i = 1; i < basePoints.Length; i++)
            {
                AdjustPeriodic(ref basePoints[i].psurface1, true, i - 1);
                AdjustPeriodic(ref basePoints[i].psurface2, false, i - 1);
            }
        }
        /// <summary>
        /// Puts the uv values of the base points into their periods, without bounds: every point next to its
        /// predecessor, so that they run on continuously, and the whole row by whole periods so that it starts where
        /// PositionOf puts its first point - in the domain of the surface. The first point which is at a pole, where
        /// PositionOf may return another parameter for the same point, does not decide; the next one does.
        /// <para>
        /// This used to be done with the bounds of the curve. Measured over the whole test suite before they were
        /// dropped, both ways gave the same uv values at every base point of every curve, except for two closed curves
        /// on cylinders without a domain, which the bounds had put a period or two outside of themselves.
        /// </para>
        /// </summary>
        internal void AnchorBasePoints()
        {
            AnchorBasePoints(surface1, true);
            AnchorBasePoints(surface2, false);
        }
        private void AnchorBasePoints(ISurface surface, bool onSurface1, bool toDomain = true)
        {
            if (!surface.IsUPeriodic && !surface.IsVPeriodic) return;
            double uPeriod = surface.IsUPeriodic ? surface.UPeriod : 0.0, vPeriod = surface.IsVPeriodic ? surface.VPeriod : 0.0;
            for (int i = 1; i < basePoints.Length; i++)
            {
                if (onSurface1) SurfacePoint.FixSurfacePoint2D(ref basePoints[i].psurface1, basePoints[i - 1].psurface1, surface.IsUPeriodic, uPeriod, surface.IsVPeriodic, vPeriod);
                else SurfacePoint.FixSurfacePoint2D(ref basePoints[i].psurface2, basePoints[i - 1].psurface2, surface.IsUPeriodic, uPeriod, surface.IsVPeriodic, vPeriod);
            }
            if (!toDomain) return;
            for (int i = 0; i < basePoints.Length; i++)
            {
                GeoPoint2D stored = onSurface1 ? basePoints[i].psurface1 : basePoints[i].psurface2;
                GeoPoint2D reference = surface.PositionOf(basePoints[i].p3d);
                double du = uPeriod > 0.0 ? uPeriod * Math.Round((reference.x - stored.x) / uPeriod) : 0.0;
                double dv = vPeriod > 0.0 ? vPeriod * Math.Round((reference.y - stored.y) / vPeriod) : 0.0;
                if (Math.Abs(reference.x - stored.x - du) > 1e-6 * Math.Max(1.0, uPeriod)) continue; // a pole or a stored value off the point
                if (Math.Abs(reference.y - stored.y - dv) > 1e-6 * Math.Max(1.0, vPeriod)) continue;
                if (du == 0.0 && dv == 0.0) return;
                GeoVector2D shift = new GeoVector2D(du, dv);
                for (int j = 0; j < basePoints.Length; j++)
                {
                    if (onSurface1) basePoints[j].psurface1 += shift;
                    else basePoints[j].psurface2 += shift;
                }
                return;
            }
        }
        /// <summary>
        /// Moves <paramref name="uv1"/> and <paramref name="uv2"/>, the parameters of a point at <paramref name="position"/>,
        /// by whole periods next to the base point nearest to that position. While the curve has no approximating spline
        /// yet, base point i is at i/(n-1).
        /// </summary>
        private void AnchorToNearestBasePoint(double position, ref GeoPoint2D uv1, ref GeoPoint2D uv2)
        {
            int nearest;
            if (approxBSpline == null) nearest = Math.Max(0, Math.Min(basePoints.Length - 1, (int)Math.Round(position * (basePoints.Length - 1))));
            else nearest = NearestIndex(ChordFractions(), position);
            SurfacePoint.FixSurfacePoint2D(ref uv1, basePoints[nearest].psurface1, surface1.IsUPeriodic, surface1.UPeriod, surface1.IsVPeriodic, surface1.VPeriod);
            SurfacePoint.FixSurfacePoint2D(ref uv2, basePoints[nearest].psurface2, surface2.IsUPeriodic, surface2.UPeriod, surface2.IsVPeriodic, surface2.VPeriod);
        }
        private void AdjustPeriodic(ref SurfacePoint toAdjust, int ind)
        {
            AdjustPeriodic(ref toAdjust.psurface1, true, ind);
            AdjustPeriodic(ref toAdjust.psurface2, false, ind);
        }
        private void AdjustPeriodic(ref GeoPoint2D toAdjust, bool onSurface1, int ind)
        {
            if (onSurface1)
            {
                if (surface1.IsUPeriodic)
                {
                    while (toAdjust.x - basePoints[ind].psurface1.x > surface1.UPeriod / 2) toAdjust.x -= surface1.UPeriod;
                    while (basePoints[ind].psurface1.x - toAdjust.x > surface1.UPeriod / 2) toAdjust.x += surface1.UPeriod;
                }
                if (surface1.IsVPeriodic)
                {
                    while (toAdjust.y - basePoints[ind].psurface1.y > surface1.VPeriod / 2) toAdjust.y -= surface1.VPeriod;
                    while (basePoints[ind].psurface1.y - toAdjust.y > surface1.VPeriod / 2) toAdjust.y += surface1.VPeriod;
                }
            }
            else
            {
                if (surface2.IsUPeriodic)
                {
                    while (toAdjust.x - basePoints[ind].psurface2.x > surface2.UPeriod / 2) toAdjust.x -= surface2.UPeriod;
                    while (basePoints[ind].psurface2.x - toAdjust.x > surface2.UPeriod / 2) toAdjust.x += surface2.UPeriod;
                }
                if (surface2.IsVPeriodic)
                {
                    while (toAdjust.y - basePoints[ind].psurface2.y > surface2.VPeriod / 2) toAdjust.y -= surface2.VPeriod;
                    while (basePoints[ind].psurface2.y - toAdjust.y > surface2.VPeriod / 2) toAdjust.y += surface2.VPeriod;
                }
            }
        }
        /// <summary>
        /// The uv value on the first or on the second surface of the point at <paramref name="position"/>, in the periods of
        /// the uv value stored at the base point next to that position. The 2d curves on the two surfaces run through these
        /// values, see <see cref="CADability.ProjectedCurve.IsCurveOfIntersection"/>.
        /// </summary>
        internal GeoPoint2D UvInStoredPeriods(bool onSurface1, double position)
        {
            ISurface surface = onSurface1 ? surface1 : surface2;
            GeoPoint2D uv = surface.PositionOf(PointAt(position));
            SurfacePoint nearest = basePoints[NearestIndex(ChordFractions(), position)];
            SurfacePoint.FixSurfacePoint2D(ref uv, onSurface1 ? nearest.psurface1 : nearest.psurface2, surface.IsUPeriodic, surface.UPeriod, surface.IsVPeriodic, surface.VPeriod);
            return uv;
        }
        /// <summary>The uv value on the first or on the second surface stored at the start or at the end of this curve.</summary>
        internal GeoPoint2D StoredUvAtEnd(bool onSurface1, bool atEnd)
        {
            SurfacePoint sp = basePoints[atEnd ? basePoints.Length - 1 : 0];
            return onSurface1 ? sp.psurface1 : sp.psurface2;
        }
        internal DualSurfaceCurve ToDualSurfaceCurve()
        {
            return new DualSurfaceCurve(this, surface1, new CADability.ProjectedCurve(this, true), surface2, new CADability.ProjectedCurve(this, false));
        }
        public ISurface Surface1
        {
            get
            {
                return surface1;
            }
            internal set
            {   // es muss sich um einen Clone handeln
                surface1 = value;
            }
        }
        public ISurface Surface2
        {
            get
            {
                return surface2;
            }
            internal set
            {   // es muss sich um einen Clone handeln
                surface2 = value;
            }
        }
        public ICurve2D CurveOnSurface1
        {
            get
            {
                return new CADability.ProjectedCurve(this, true);
            }
        }
        public ICurve2D CurveOnSurface2
        {
            get
            {
                return new CADability.ProjectedCurve(this, false);
            }
        }
        protected override void InvalidateSecondaryData()
        {
            base.InvalidateSecondaryData();
            hashedPositions.Clear();
            approxBSpline = null;
        }
        internal void ReplaceSurface(ISurface oldSurface, ISurface newSurface)
        {   // die beiden surfaces müssen geometrisch identisch sein
            if (surface1 == oldSurface) surface1 = newSurface;
            if (surface2 == oldSurface) surface2 = newSurface;
        }
        internal void ReplaceSurface(ISurface oldSurface, ISurface newSurface, ModOp2D oldToNew)
        {   // die beiden surfaces müssen geometrisch identisch sein
            DualSurfaceCurveDiagnostics.Count("IDSC.ReplaceSurface with ModOp2D");
            if (surface1 == oldSurface)
            {
                surface1 = newSurface;
                ModifySurfacePoints(true, oldToNew);
            }
            else if (surface2 == oldSurface)
            {
                surface2 = newSurface;
                ModifySurfacePoints(false, oldToNew);
            }
            else if (surface1.SameGeometry((surface1 as ISurfaceImpl).usedArea, oldSurface, (oldSurface as ISurfaceImpl).usedArea, Precision.eps, out ModOp2D dumy))
            {
                surface1 = newSurface;
                ModifySurfacePoints(true, oldToNew);
            }
            else if (surface2.SameGeometry((surface2 as ISurfaceImpl).usedArea, oldSurface, (oldSurface as ISurfaceImpl).usedArea, Precision.eps, out dumy))
            {
                surface2 = newSurface;
                ModifySurfacePoints(false, oldToNew);
            }
            else
            {

            }
        }
        /// <summary>
        /// A trimmed copy of this curve, for an edge split into parts, together with the curves on the two surfaces along
        /// it, which stay in the periods and in the directions of <paramref name="c1"/> and <paramref name="c2"/>.
        /// </summary>
        internal InterpolatedDualSurfaceCurve CloneTrimmed(double startPos, double endPos, CADability.ProjectedCurve c1, CADability.ProjectedCurve c2, out ICurve2D c1trimmed, out ICurve2D c2trimmed)
        {
            // this used to pass forwardOriented, which bound to the parameter isTangential
            InterpolatedDualSurfaceCurve res = new InterpolatedDualSurfaceCurve(surface1, surface2, basePoints.Clone() as SurfacePoint[], isTangential);
            res.Trim(startPos, endPos);
            c1trimmed = c1.OnTrimmedCurve(res);
            c2trimmed = c2.OnTrimmedCurve(res);
            return res;
        }
        public BSpline ToBSpline(double precision)
        {   // better than through the base points. A copy: the spline is shared with the clones of this curve, and the
            // caller may modify what it gets - Edge.UpdateInterpolatedDualSurfaceCurve for instance makes it the curve of an edge.
            return ApproxBSpline.Clone() as BSpline;
        }
        internal GeoPoint[] BasePoints
        {
            get
            {
                GeoPoint[] res = new GeoPoint[basePoints.Length];
                for (int i = 0; i < basePoints.Length; i++)
                {
                    res[i] = basePoints[i].p3d;
                }
                return res;
            }
        }

#if DEBUG
        new DebuggerContainer Debug
        {
            get
            {
                DebuggerContainer res = new DebuggerContainer();
                Polyline pl = Polyline.Construct();
                GeoPoint[] pnts = new GeoPoint[basePoints.Length];
                for (int i = 0; i < basePoints.Length; ++i)
                {
                    pnts[i] = basePoints[i].p3d;
                    GeoVector u = surface1.UDirection(basePoints[i].psurface1);
                    GeoVector v = surface1.VDirection(basePoints[i].psurface1);
                    PlaneSurface pls = new PlaneSurface(pnts[i], u, v, u ^ v);
                    Face fc = Face.MakeFace(pls, new SimpleShape(new BoundingRect(-1, -1, 1, 1)));
                    fc.ColorDef = new CADability.Attribute.ColorDef("Surface1", System.Drawing.Color.Red);
                    res.Add(fc);
                    u = surface2.UDirection(basePoints[i].psurface2);
                    v = surface2.VDirection(basePoints[i].psurface2);
                    pls = new PlaneSurface(pnts[i], u, v, u ^ v);
                    fc = Face.MakeFace(pls, new SimpleShape(new BoundingRect(-1, -1, 1, 1)));
                    fc.ColorDef = new CADability.Attribute.ColorDef("Surface1", System.Drawing.Color.Green);
                    res.Add(fc);
                }
                pl.SetPoints(pnts, false);
                double size = pl.GetBoundingCube().Size / 10;
                res.Add(pl);
                return res;
            }
        }
        internal IGeoObject Debug100Points
        {
            get
            {
                GeoPoint[] dbgpnts = new CADability.GeoPoint[100];
                for (int i = 0; i < dbgpnts.Length; i++)
                {
                    dbgpnts[i] = PointAt(i / (double)(dbgpnts.Length - 1));
                }
                Polyline dbgpl = Polyline.Construct();
                dbgpl.SetPoints(dbgpnts, false);
                return dbgpl;
            }
        }
        internal GeoObjectList DebugOrientation
        {
            get
            {
                GeoObjectList res = new GeoObjectList();
                Polyline pl = Debug100Points as Polyline;
                res.Add(pl);
                double l = pl.Length / 50;
                for (int i = 0; i < 100; i++)
                {
                    GeoVector dir = (this as IOrientation).OrientationAt(i / 99.0);
                    Line line = Line.TwoPoints(pl.GetPoint(i), pl.GetPoint(i) + l * dir.Normalized);
                    res.Add(line);
                }
                return res;
            }
        }
        GeoObjectList DebugBasePoints
        {
            get
            {
                GeoObjectList res = new GeoObjectList();
                Polyline pl = Polyline.Construct();
                GeoPoint[] pnts = new GeoPoint[basePoints.Length];
                for (int i = 0; i < basePoints.Length; ++i)
                {
                    pnts[i] = basePoints[i].p3d;
                }
                pl.SetPoints(pnts, false);
                pl.ColorDef = new Attribute.ColorDef("org", System.Drawing.Color.Red);
                res.Add(pl);
                pl = Polyline.Construct();
                pnts = new GeoPoint[basePoints.Length];
                for (int i = 0; i < basePoints.Length; ++i)
                {
                    pnts[i] = surface1.PointAt(basePoints[i].psurface1);
                }
                pl.SetPoints(pnts, false);
                pl.ColorDef = new Attribute.ColorDef("surf1", System.Drawing.Color.Green);
                res.Add(pl);
                pl = Polyline.Construct();
                pnts = new GeoPoint[basePoints.Length];
                for (int i = 0; i < basePoints.Length; ++i)
                {
                    pnts[i] = surface2.PointAt(basePoints[i].psurface2);
                }
                pl.SetPoints(pnts, false);
                pl.ColorDef = new Attribute.ColorDef("surf1", System.Drawing.Color.Blue);
                res.Add(pl);
                return res;
            }
        }
        IGeoObject DebugHashedCurve1
        {
            get
            {
                SortedList<double, SurfacePoint> sl = new SortedList<double, SurfacePoint>(hashedPositions);
                List<GeoPoint2D> pnts = new List<GeoPoint2D>();
                foreach (SurfacePoint sp in sl.Values)
                {
                    pnts.Add(sp.psurface1);
                }
                Polyline2D pl2d = new Polyline2D(pnts.ToArray());
                return pl2d.MakeGeoObject(Plane.XYPlane);
            }
        }
        IGeoObject DebugHashedCurve2
        {
            get
            {
                SortedList<double, SurfacePoint> sl = new SortedList<double, SurfacePoint>(hashedPositions);
                List<GeoPoint2D> pnts = new List<GeoPoint2D>();
                foreach (SurfacePoint sp in sl.Values)
                {
                    pnts.Add(sp.psurface2);
                }
                Polyline2D pl2d = new Polyline2D(pnts.ToArray());
                return pl2d.MakeGeoObject(Plane.XYPlane);
            }
        }
        GeoObjectList DebugSurface
        {
            get
            {
                GeoObjectList res = new GeoObjectList();
                BoundingRect bnd = BoundingRect.EmptyBoundingRect;
                for (int i = 0; i < basePoints.Length; i++)
                {
                    bnd.MinMax(basePoints[i].psurface1);
                }
                bnd.Inflate(1.0);
                res.Add(Face.MakeFace(surface1, new SimpleShape(Border.MakeRectangle(bnd))));
                bnd = BoundingRect.EmptyBoundingRect;
                for (int i = 0; i < basePoints.Length; i++)
                {
                    bnd.MinMax(basePoints[i].psurface2);
                }
                bnd.Inflate(1.0);
                res.Add(Face.MakeFace(surface2, new SimpleShape(Border.MakeRectangle(bnd))));
                return res;
            }
        }
        public DebuggerContainer DebugFaces
        {
            get
            {
                DebuggerContainer res = new DebuggerContainer();
                res.Add(Face.MakeFace(surface1, Domain1), System.Drawing.Color.MediumVioletRed);
                res.Add(Face.MakeFace(surface2, Domain2), System.Drawing.Color.SeaShell);
                return res;
            }
        }
#endif
        #region IGeoObject override
        /// <summary>
        /// Overrides <see cref="CADability.GeoObject.IGeoObjectImpl.GetBoundingCube ()"/>
        /// </summary>
        /// <returns></returns>
        public override BoundingBox GetBoundingCube()
        {   // not the base points: the curve bulges out between them. PointAt is the approximating spline, so its extent is exact
            return ApproxBSpline.GetBoundingCube();
        }
        /// <summary>
        /// Overrides <see cref="CADability.GeoObject.IGeoObjectImpl.Modify (ModOp)"/>
        /// </summary>
        /// <param name="m"></param>
        public override void Modify(ModOp m)
        {
            surface1 = surface1.GetModified(m);
            surface2 = surface2.GetModified(m);
            // nicht:
            // surface2.Modify(m);
            // denn man weiß nicht von wem die surface noch verwendet wird
            for (int i = 0; i < basePoints.Length; ++i)
            {
                basePoints[i].p3d = m * basePoints[i].p3d;
            }
            InvalidateSecondaryData();
        }
        /// <summary>
        /// Overrides <see cref="CADability.GeoObject.IGeoObjectImpl.GetExtent (double)"/>
        /// </summary>
        /// <param name="precision"></param>
        /// <returns></returns>
        public override BoundingBox GetExtent(double precision)
        {
            return ApproxBSpline.GetBoundingCube();
        }
        public BoundingRect GetBoundingRect(bool onSurface1)
        {
            BoundingRect res = BoundingRect.EmptyBoundingRect;
            if (onSurface1)
            {
                for (int i = 0; i < basePoints.Length; i++)
                {
                    res.MinMax(basePoints[i].psurface1);
                }
            }
            else
            {
                for (int i = 0; i < basePoints.Length; i++)
                {
                    res.MinMax(basePoints[i].psurface2);
                }
            }
            return res;
        }
        /// <summary>
        /// Overrides <see cref="CADability.GeoObject.IGeoObjectImpl.Position (GeoPoint, GeoVector, double)"/>
        /// </summary>
        /// <param name="fromHere"></param>
        /// <param name="direction"></param>
        /// <param name="precision"></param>
        /// <returns></returns>
        public override double Position(GeoPoint fromHere, GeoVector direction, double precision)
        {   // vorläufig mal auf die Polylinien beziehen
            double res = double.MaxValue;
            for (int i = 0; i < basePoints.Length - 1; ++i)
            {
                double pos1, pos2;
                double d = Geometry.DistLL(basePoints[i].p3d, basePoints[i + 1].p3d - basePoints[i].p3d, fromHere, direction, out pos1, out pos2);
                if (pos1 >= 0.0 && pos1 <= 1.0 && pos2 < res) res = pos2;
            }
            return res;
        }
        /// <summary>
        /// Overrides <see cref="CADability.GeoObject.IGeoObjectImpl.PaintTo3D (IPaintTo3D)"/>
        /// </summary>
        /// <param name="paintTo3D"></param>
        public override void PaintTo3D(IPaintTo3D paintTo3D)
        {
            base.PaintTo3D(paintTo3D);
        }
        /// <summary>
        /// Overrides <see cref="CADability.GeoObject.IGeoObjectImpl.CopyGeometry (IGeoObject)"/>
        /// </summary>
        /// <param name="ToCopyFrom"></param>
        public override void CopyGeometry(IGeoObject ToCopyFrom)
        {
            InterpolatedDualSurfaceCurve other = ToCopyFrom as InterpolatedDualSurfaceCurve;
            basePoints = other.basePoints.Clone() as SurfacePoint[];
            surface1 = other.surface1;
            surface2 = other.surface2;
            InvalidateSecondaryData();
        }
        public override void FindSnapPoint(SnapPointFinder spf)
        {
            if (!spf.Accept(this)) return;
            if (spf.SnapToObjectCenter)
            {
                GeoPoint Center = (this as ICurve).PointAt(0.5);
                spf.Check(Center, this, SnapPointFinder.DidSnapModes.DidSnapToObjectCenter);
            }
            if (spf.SnapToObjectSnapPoint)
            {
                spf.Check(StartPoint, this, SnapPointFinder.DidSnapModes.DidSnapToObjectSnapPoint);
                spf.Check(EndPoint, this, SnapPointFinder.DidSnapModes.DidSnapToObjectSnapPoint);
            }
            if (spf.SnapToDropPoint && spf.BasePointValid)
            {
                //GeoPoint toTest = Geometry.DropPL(spf.BasePoint, startPoint, endPoint);
                //spf.Check(toTest, this, SnapPointFinder.DidSnapModes.DidSnapToDropPoint);
            }
            if (spf.SnapToObjectPoint)
            {
                double par = PositionOf(spf.SourcePoint3D, spf.Projection.ProjectionPlane);
                // TODO: hier ist eigentlich gefragt der nächste punkt auf der Linie im Sinne des Projektionsstrahls
                if (par >= 0.0 && par <= 1.0)
                {
                    spf.Check(PointAt(par), this, SnapPointFinder.DidSnapModes.DidSnapToObjectPoint);
                }
            }
        }
        #endregion
        #region ICurve Members
        public override GeoPoint StartPoint
        {
            get
            {
                return basePoints[0].p3d;
            }
            set
            {   // es darf hier nur um minimale Änderungen gehen, nicht um trimmen
                // wird nur von BRepOperation verwendet
                SurfacePoint sp = new SurfacePoint(value, surface1.PositionOf(value), surface2.PositionOf(value));
                AdjustPeriodic(ref sp, 0);
                basePoints[0] = sp;
                InvalidateSecondaryData();
            }
        }
        public override GeoPoint EndPoint
        {
            get
            {
                return basePoints[basePoints.Length - 1].p3d;
            }
            set
            {
                SurfacePoint sp = new SurfacePoint(value, surface1.PositionOf(value), surface2.PositionOf(value));
                AdjustPeriodic(ref sp, basePoints.Length - 1);
                basePoints[basePoints.Length - 1] = sp;
                InvalidateSecondaryData();
            }
        }
        /// <summary>
        /// Refines the collection of base points by adjusting their distribution to ensure a more uniform spacing.
        /// </summary>
        /// <remarks>This method iteratively evaluates the distances between consecutive base points and
        /// adjusts the collection  by either adding or removing points based on their relative spacing. Points that are
        /// too far apart will  have new points inserted between them, while points that are too close together will
        /// have one of them removed.  The process continues until no further adjustments are needed.  This operation
        /// invalidates any cached approximations or secondary data that depend on the base points.</remarks>
        private void RefineBasePoints()
        {
            bool changed = true;
            while (changed)
            {
                changed = false;
                double length = 0.0;
                for (int i = 0; i < basePoints.Length - 1; i++)
                {
                    length += basePoints[i + 1].p3d | basePoints[i].p3d;
                }
                length /= (basePoints.Length - 1);
                for (int i = 0; i < basePoints.Length - 1; i++)
                {
                    double d = basePoints[i + 1].p3d | basePoints[i].p3d;
                    if (d > 1.5 * length || basePoints.Length == 2)
                    {
                        changed = true;
                        approxBSpline = null;
                        hashedPositions.Clear();
                        ApproximatePosition((i + 0.5) / (basePoints.Length - 1), out GeoPoint2D uv1, out GeoPoint2D uv2, out GeoPoint p);
                        List<SurfacePoint> bpl = new List<SurfacePoint>(basePoints);
                        bpl.Insert(i + 1, new SurfacePoint(p, uv1, uv2));
                        basePoints = bpl.ToArray();
                        break; // nur einen Punkt pro Schleifendurchlauf einfügen
                    }
                    if (d < 0.5 * length && basePoints.Length > 2)
                    {
                        changed = true;
                        approxBSpline = null;
                        hashedPositions.Clear();
                        List<SurfacePoint> bpl = new List<SurfacePoint>(basePoints);
                        bpl.RemoveAt(i + 1);
                        basePoints = bpl.ToArray();
                        break; // nur einen Punkt pro Schleifendurchlauf entfernen
                    }
                }
            }
            InvalidateSecondaryData();
        }

        private BSpline ApproxBSpline
        {
            get
            {
                if (approxBSpline != null) return approxBSpline;
                BSpline bsp = BSpline.Construct();
                bsp.ThroughPoints(BasePoints, 3, false);
                // BasePoints are unevenly distributed. The parameter of bsp is running much more evenly
                // since the knots are calculated according to the chord length
                // setting approxBSpline changes the "speed" of the parameter, makes it more even
                approxBSpline = bsp;
                hashedPositions.Clear(); // don't use hased positions, they are no more correct
                Func<double, GeoPoint> curve = (pos) => // input parameter for BSpline.Approximate
                {
                    ApproximatePosition(pos, out GeoPoint2D uv1, out GeoPoint2D uv2, out GeoPoint p);
                    return p;
                };
                approxBSpline = BSpline.Approximate(curve);
                hashedPositions.Clear(); // don't use hashed positions, they are no more correct
                return approxBSpline;
            }
        }

        /// <summary>
        /// The minimizer also reports success when it stalls somewhere without having found a common point of the two
        /// surfaces and the plane. And near a node of the intersection it may converge to the other branch, whose tangent
        /// crosses the plane at a large angle. <paramref name="planeFromSpline"/>: the plane is perpendicular to the
        /// approximating spline, so its normal is a good estimate of the tangent. A plane perpendicular to a chord
        /// between two base points is not, so the tangent is not checked then.
        /// </summary>
        private bool IsPlausibleIntersection(GeoPoint2D uv1, GeoPoint2D uv2, Plane plane, bool planeFromSpline)
        {
            GeoPoint p1 = surface1.PointAt(uv1), p2 = surface2.PointAt(uv2);
            double tolerance = 100 * Precision.eps;
            if ((p1 | p2) > tolerance) return false;
            if (Math.Abs(plane.Distance(p1)) > tolerance) return false;
            if (!planeFromSpline) return true;
            GeoVector n1 = surface1.GetNormal(uv1), n2 = surface2.GetNormal(uv2);
            if (n1.IsNullVector() || n2.IsNullVector()) return true;
            GeoVector tangent = n1.Normalized ^ n2.Normalized;
            if (tangent.Length < 1e-2) return true; // the surfaces (almost) touch here, n1 x n2 is no usable tangent
            return Math.Abs(tangent.Normalized * plane.Normal.Normalized) >= Math.Cos(30.0 / 180.0 * Math.PI);
        }
        private void ApproximatePosition(double position, out GeoPoint2D uv1, out GeoPoint2D uv2, out GeoPoint p)
        {
            lock (hashedPositions)
            {
                // Zuerst nachsehen, ob der Punkt schon bekannt ist
                (bool hasLower, double lowerKey, SurfacePoint lowerValue, bool hasUpper, double upperKey, SurfacePoint upperValue, bool exact, int exactIndex) = hashedPositions.Neighbors(position);
                if (exact)
                {
                    SurfacePoint found = hashedPositions.Values[exactIndex];
                    p = found.p3d;
                    uv1 = found.psurface1;
                    uv2 = found.psurface2;
                    DualSurfaceCurveDiagnostics.RecordCacheHit();
                    return;
                }
                Plane normalPlane; // Plane normal tu the BSpline or segment-polyline at position
                if (approxBSpline == null)
                {   // in the constructor we need to calculate a few basepoints before we can build the BSpline

                    int ind = (int)Math.Floor(position * (basePoints.Length - 1));
                    if (ind < 0) ind = 0;
                    if (ind > basePoints.Length - 1) ind = basePoints.Length - 1;
                    double d = position * (basePoints.Length - 1) - ind;
                    GeoPoint location;
                    if (d > 0.0 && ind < basePoints.Length - 1) location = basePoints[ind].p3d + d * (basePoints[ind + 1].p3d - basePoints[ind].p3d);
                    else location = basePoints[ind].p3d;
                    if (ind == basePoints.Length - 1) --ind;
                    GeoVector normal = basePoints[ind + 1].p3d - basePoints[ind].p3d;
                    normalPlane = new Plane(location, normal);
                }
                else
                {
                    GeoVector normal = (approxBSpline as ICurve).DirectionAt(position);
                    normalPlane = new Plane((approxBSpline as ICurve).PointAt(position), normal);
                }
                if (isTangential)
                {
                    GeoPoint2D uv1s, uv2s;
                    if (hasLower && hasUpper)
                    {
                        double d1 = (position - lowerKey) / (upperKey - lowerKey);
                        double d2 = (upperKey - position) / (upperKey - lowerKey);
                        uv1s = new GeoPoint2D((1.0 - d1) * lowerValue.psurface1.x + d1 * upperValue.psurface1.x, (1.0 - d1) * lowerValue.psurface1.y + d1 * upperValue.psurface1.y);
                        uv2s = new GeoPoint2D((1.0 - d1) * lowerValue.psurface2.x + d1 * upperValue.psurface2.x, (1.0 - d1) * lowerValue.psurface2.y + d1 * upperValue.psurface2.y);
                    }
                    else if (hasLower)
                    {
                        uv1s = lowerValue.psurface1;
                        uv2s = lowerValue.psurface2;
                    }
                    else if (hasUpper)
                    {
                        uv1s = upperValue.psurface1;
                        uv2s = upperValue.psurface2;
                    }
                    else
                    {
                        uv1s = surface1.PositionOf(normalPlane.Location);
                        uv2s = surface2.PositionOf(normalPlane.Location);
                    }

                    if (BoxedSurfaceExtension.FindTangentialIntersectionPoint(normalPlane.Location, normalPlane.Normal, surface1, surface2, out uv1, out uv2, uv1s, uv2s))
                    {
                        // if (BoxedSurfaceExtension.FindTangentialIntersectionPointJ(normalPlane.Location, normalPlane.Normal, surface1, surface2, out uv1, out uv2))
                        // FindTangentialIntersectionPointJ is maybe faster, but we will have to check its reliability
                        p = new GeoPoint(surface1.PointAt(uv1), surface2.PointAt(uv2));
                        SurfacePoint spt = new SurfacePoint(p, uv1, uv2);
                        if (hasLower && hasUpper)
                        {
                            if (position - lowerKey < upperKey - position) spt.FixAgainstNeighbour(lowerValue, surface1, surface2);
                            else spt.FixAgainstNeighbour(upperValue, surface1, surface2);
                        }
                        else if (hasLower) spt.FixAgainstNeighbour(lowerValue, surface1, surface2);
                        else if (hasUpper) spt.FixAgainstNeighbour(upperValue, surface1, surface2);
                        else AnchorToNearestBasePoint(position, ref spt.psurface1, ref spt.psurface2);
                        hashedPositions[position] = spt;
                        uv1 = spt.psurface1;
                        uv2 = spt.psurface2;
                        if (DualSurfaceCurveDiagnostics.Enabled) DualSurfaceCurveDiagnostics.RecordRefinement("tangential solver", false,
                            approxBSpline != null, isTangential, surface1, uv1, surface2, uv2, normalPlane, p);
                        return;
                    }
                }
                else
                {
                    PlaneSurface ps = new PlaneSurface(normalPlane);
                    p = normalPlane.Location;
                    GeoPoint2D uvplane = GeoPoint2D.Origin;
                    uv1 = surface1.PositionOf(normalPlane.Location);
                    uv2 = surface2.PositionOf(normalPlane.Location);
                    AnchorToNearestBasePoint(position, ref uv1, ref uv2);
                    if (BoxedSurfaceExtension.SurfacesIntersectionLM(ps, surface1, surface2, ref uvplane, ref uv1, ref uv2, ref p)
                        && IsPlausibleIntersection(uv1, uv2, normalPlane, approxBSpline != null))
                    {
                        SurfacePoint spt = new SurfacePoint(p, uv1, uv2);
                        if (hasLower && hasUpper)
                        {
                            if (position - lowerKey < upperKey - position) spt.FixAgainstNeighbour(lowerValue, surface1, surface2);
                            else spt.FixAgainstNeighbour(upperValue, surface1, surface2);
                        }
                        else if (hasLower) spt.FixAgainstNeighbour(lowerValue, surface1, surface2);
                        else if (hasUpper) spt.FixAgainstNeighbour(upperValue, surface1, surface2);
                        else AnchorToNearestBasePoint(position, ref spt.psurface1, ref spt.psurface2);
                        hashedPositions[position] = spt;
                        uv1 = spt.psurface1;
                        uv2 = spt.psurface2;
                        if (DualSurfaceCurveDiagnostics.Enabled) DualSurfaceCurveDiagnostics.RecordRefinement("transversal solver (LM)", false,
                            approxBSpline != null, isTangential, surface1, uv1, surface2, uv2, normalPlane, p);
                        return;
                    }
                }
                // we should not reach this point. It could be there is an inner point, which is tangential or the curve is tangential but isTangential is false
                {
                    p = normalPlane.Location;
                    uv1 = surface1.PositionOf(normalPlane.Location);
                    uv2 = surface2.PositionOf(normalPlane.Location);
                    SurfacePoint spt = new SurfacePoint(p, uv1, uv2);
                    if (hasLower && hasUpper)
                    {
                        if (position - lowerKey < upperKey - position) spt.FixAgainstNeighbour(lowerValue, surface1, surface2);
                        else spt.FixAgainstNeighbour(upperValue, surface1, surface2);
                    }
                    else if (hasLower) spt.FixAgainstNeighbour(lowerValue, surface1, surface2);
                    else if (hasUpper) spt.FixAgainstNeighbour(upperValue, surface1, surface2);
                    else AnchorToNearestBasePoint(position, ref spt.psurface1, ref spt.psurface2);
                    uv1 = spt.psurface1;
                    uv2 = spt.psurface2;
                    hashedPositions[position] = spt;
                    if (DualSurfaceCurveDiagnostics.Enabled) DualSurfaceCurveDiagnostics.RecordRefinement(
                        isTangential ? "fallback after the tangential solver failed" : "fallback after the transversal solver failed", true,
                        approxBSpline != null, isTangential, surface1, uv1, surface2, uv2, normalPlane, p);
                }
            }
        }
        internal BoundingRect Domain1
        {
            get
            {
                BoundingRect res = BoundingRect.EmptyBoundingRect;
                for (int i = 0; i < basePoints.Length; i++)
                {
                    res.MinMax(basePoints[i].psurface1);
                }
                return res;
            }
        }
        internal BoundingRect Domain2
        {
            get
            {
                BoundingRect res = BoundingRect.EmptyBoundingRect;
                for (int i = 0; i < basePoints.Length; i++)
                {
                    res.MinMax(basePoints[i].psurface2);
                }
                return res;
            }
        }
        public override GeoVector StartDirection
        {
            get
            {
                GeoVector adir = (ApproxBSpline as ICurve).StartDirection;
                if (isTangential)
                {
                    return adir;
                }
                else
                {
                    GeoVector v = surface1.GetNormal(basePoints[0].psurface1) ^ surface2.GetNormal(basePoints[0].psurface2);
                    if (v.Length < 1e-5) return adir; // if the two surfaces are tangential at the start point, then the cross product is zero and we take the direction of the approximating BSpline)
                    if (v * adir < 0.0) v.Reverse(); // n1 x n2 is the tangent up to its sign, the spline tells which way the curve runs
                    v.Length = adir.Length; // make the same lengt as the approximating BSpline would have. This is very close
                    return v;
                }
            }
        }
        public override GeoVector EndDirection
        {
            get
            {
                GeoVector adir = (ApproxBSpline as ICurve).EndDirection;
                if (isTangential)
                {
                    return adir;
                }
                else
                {
                    GeoVector v = surface1.GetNormal(basePoints[basePoints.Length - 1].psurface1) ^ surface2.GetNormal(basePoints[basePoints.Length - 1].psurface2);
                    if (v.Length < 1e-5) return adir; // if the two surfaces are tangential at the end point, then the cross product is zero and we take the direction of the approximating BSpline
                    if (v * adir < 0.0) v.Reverse(); // n1 x n2 is the tangent up to its sign, the spline tells which way the curve runs
                    v.Length = adir.Length; // make the same lengt as the approximating BSpline would have. This is very close to the factual length
                    return v;
                }
            }
        }
        public override GeoVector DirectionAt(double Position)
        {
            return (ApproxBSpline as ICurve).DirectionAt(Position);
        }
        public override GeoPoint PointAt(double Position)
        {
            return (ApproxBSpline as ICurve).PointAt(Position);
        }
        public override double PositionAtLength(double position)
        {
            return (ApproxBSpline as ICurve).PositionAtLength(position);
        }
        public override double PositionOf(GeoPoint p)
        {
            double ppos = TetraederHull.PositionOf(p);
            double pos1 = (ApproxBSpline as ICurve).PositionOf(p);

            if ((pos1 != double.MaxValue) && (PointAt(pos1) | p) < (PointAt(ppos) | p))
                return pos1;

            return ppos;
        }
        public override double PositionOf(GeoPoint p, double prefer)
        {
            return (ApproxBSpline as ICurve).PositionOf(p, prefer);
        }
        public override double PositionOf(GeoPoint p, Plane pl)
        {   // FindSnapPoint uses this
            return (ApproxBSpline as ICurve).PositionOf(p, pl);
        }
        /// <summary>
        /// The length for <see cref="GeneralCurve.Length"/>, which caches it. <see cref="PointAt"/> evaluates the approximating
        /// spline, so the length is measured with points which lie exactly on both surfaces: <see cref="ApproximatePosition"/>
        /// moves the point of the spline onto both surfaces in its normal plane, so it has the parametrization of the spline,
        /// and the base points are where <see cref="GetBasePoints"/> says. The sum of the distances of the base points, which
        /// ShapeIt still uses, is too short (0.65% for a bore of radius 5 through a tube of radius 30).
        /// </summary>
        protected override double ComputeLength()
        {
            return ArcLength.FromPoints(position =>
            {
                ApproximatePosition(position, out GeoPoint2D _, out GeoPoint2D _, out GeoPoint p);
                return p;
            }, 0.0, 1.0, GetBasePoints(), 1e-9); // the points are refined to about 1e-10, a tighter tolerance only adds levels
        }
        /// <summary>
        /// The positions of the base points on this curve. They come from the approximating spline, which is
        /// parametrized by the chord length of the base points: base point i is in general not at i/(n-1).
        /// </summary>
        /// <summary>
        /// The fractions of the length of the base polygon at the base points, 0 at the first and 1 at the last. The
        /// approximating spline is parametrized by the chord length of the base points, so this estimates their
        /// positions well enough to find the base point next to a position - without a PositionOf. For a closed curve
        /// it tells the first base point from the last, which are the same point in space.
        /// </summary>
        internal double[] ChordFractions()
        {
            double[] res = new double[basePoints.Length];
            for (int i = 1; i < res.Length; i++) res[i] = res[i - 1] + (basePoints[i].p3d | basePoints[i - 1].p3d);
            double total = res[res.Length - 1];
            for (int i = 1; i < res.Length; i++) res[i] = total > 0.0 ? res[i] / total : (double)i / (res.Length - 1);
            res[res.Length - 1] = 1.0;
            return res;
        }
        /// <summary>The index of the value in the ascending <paramref name="values"/> which is nearest to <paramref name="value"/>.</summary>
        internal static int NearestIndex(double[] values, double value)
        {
            int index = Array.BinarySearch(values, value);
            if (index >= 0) return index;
            index = ~index; // the first value greater than value
            if (index == 0) return 0;
            if (index == values.Length) return values.Length - 1;
            return value - values[index - 1] <= values[index] - value ? index - 1 : index;
        }
        private double[] BasePointPositions()
        {
            double[] res = new double[basePoints.Length];
            for (int i = 1; i < basePoints.Length - 1; i++) res[i] = PositionOf(basePoints[i].p3d);
            res[basePoints.Length - 1] = 1.0;
            return res;
        }
        public override ICurve[] Split(double Position)
        {
            double[] positions = BasePointPositions();
            SurfacePoint splitPoint;
            int atBasePoint = Array.FindIndex(positions, pos => Math.Abs(pos - Position) < 1e-9);
            if (atBasePoint >= 0) splitPoint = basePoints[atBasePoint]; // keep the exact point
            else
            {
                ApproximatePosition(Position, out GeoPoint2D uv1, out GeoPoint2D uv2, out GeoPoint p);
                splitPoint = new SurfacePoint(p, uv1, uv2);
            }
            List<SurfacePoint> l1 = new List<SurfacePoint> { basePoints[0] };
            List<SurfacePoint> l2 = new List<SurfacePoint>();
            for (int i = 1; i < basePoints.Length - 1; i++)
            {   // base points very close to the split position would make a tiny segment, they are left out (as in Trim)
                if (positions[i] < Position - 1e-3) l1.Add(basePoints[i]);
                else if (positions[i] > Position + 1e-3) l2.Add(basePoints[i]);
            }
            l2.Add(basePoints[basePoints.Length - 1]);
            l1.Add(splitPoint);
            l2.Insert(0, splitPoint);
            for (int i = l1.Count - 1; i > 0; --i)
            {
                if ((l1[i].p3d | l1[i - 1].p3d) == 0.0) l1.RemoveAt(i);
            }
            for (int i = l2.Count - 1; i > 0; --i)
            {
                if ((l2[i].p3d | l2[i - 1].p3d) == 0.0) l2.RemoveAt(i);
            }
            InterpolatedDualSurfaceCurve dsc1 = new InterpolatedDualSurfaceCurve(surface1.Clone(), surface2.Clone(), l1.ToArray(), isTangential);
            InterpolatedDualSurfaceCurve dsc2 = new InterpolatedDualSurfaceCurve(surface1.Clone(), surface2.Clone(), l2.ToArray(), isTangential);
            return new ICurve[] { dsc1, dsc2 };
        }

        /// <summary>
        /// Computes the uv values of the base points anew from their 3d points, e.g. after the domain of a surface was
        /// set: PositionOf puts them into that domain.
        /// </summary>
        internal void RecalcSurfacePoints()
        {
            DualSurfaceCurveDiagnostics.Count("IDSC.RecalcSurfacePoints");
            for (int i = 0; i < basePoints.Length; i++)
            {
                basePoints[i].psurface1 = surface1.PositionOf(basePoints[i].p3d);
                basePoints[i].psurface2 = surface2.PositionOf(basePoints[i].p3d);
            }
            InvalidateSecondaryData();
            AnchorBasePoints();
        }

        public override ICurve[] Split(double Position1, double Position2)
        {   // only for closed curves: one part from the smaller to the bigger position, the other one across the start point
            if (Position2 < Position1) (Position1, Position2) = (Position2, Position1);
            double[] positions = BasePointPositions();
            ApproximatePosition(Position1, out GeoPoint2D uv1, out GeoPoint2D uv2, out GeoPoint p);
            SurfacePoint p1 = new SurfacePoint(p, uv1, uv2);
            ApproximatePosition(Position2, out uv1, out uv2, out p);
            SurfacePoint p2 = new SurfacePoint(p, uv1, uv2);
            List<SurfacePoint> l1 = new List<SurfacePoint> { p1 };
            List<SurfacePoint> l2 = new List<SurfacePoint> { p2 };
            // the last base point of a closed curve is the first one again, so it is not used here
            for (int i = 0; i < basePoints.Length - 1; i++)
            {
                if (positions[i] > Position1 + 1e-3 && positions[i] < Position2 - 1e-3) l1.Add(basePoints[i]);
            }
            for (int i = 0; i < basePoints.Length - 1; i++)
            {
                if (positions[i] > Position2 + 1e-3) l2.Add(basePoints[i]);
            }
            for (int i = 0; i < basePoints.Length - 1; i++)
            {
                if (positions[i] < Position1 - 1e-3) l2.Add(basePoints[i]);
            }
            l1.Add(p2);
            l2.Add(p1);
            InterpolatedDualSurfaceCurve dsc1 = new InterpolatedDualSurfaceCurve(surface1.Clone(), surface2.Clone(), l1.ToArray(), isTangential);
            InterpolatedDualSurfaceCurve dsc2 = new InterpolatedDualSurfaceCurve(surface1.Clone(), surface2.Clone(), l2.ToArray(), isTangential);
            return new ICurve[] { dsc1, dsc2 };
        }
        public override bool IsClosed
        {
            get
            {
                return Precision.IsEqual(StartPoint, EndPoint);
            }
        }
        public override void Reverse()
        {
            DualSurfaceCurveDiagnostics.Count("IDSC.Reverse");
            Array.Reverse(basePoints);
            InvalidateSecondaryData();
        }
        public override void Trim(double StartPos, double EndPos)
        {
            if (StartPos <= Precision.eps && EndPos >= 1 - Precision.eps) return; // trim from start to end, nothing to do
            DualSurfaceCurveDiagnostics.Count("IDSC.Trim");
            List<SurfacePoint> spl = new List<SurfacePoint>();
            GeoPoint2D uv1, uv2;
            GeoPoint p;
            ApproximatePosition(StartPos, out uv1, out uv2, out p);
            spl.Add(new SurfacePoint(p, uv1, uv2));
            if (StartPos > EndPos && IsClosed)
            {
                for (int i = 0; i < basePoints.Length; ++i)
                {
                    double pos = (double)i / (double)(basePoints.Length - 1);
                    pos = PositionOf(basePoints[i].p3d);
                    // keine fast identischen Punkte zufügen, die führen zu Nullvektoren in der Differenz
                    if (pos > StartPos + 1e-3) spl.Add(basePoints[i]);
                }
                for (int i = 1; i < basePoints.Length; ++i)
                {
                    double pos = (double)i / (double)(basePoints.Length - 1);
                    pos = PositionOf(basePoints[i].p3d);
                    // keine fast identischen Punkte zufügen, die führen zu Nullvektoren in der Differenz
                    if (pos < EndPos - 1e-3) spl.Add(basePoints[i]);
                    else break;
                }
            }
            else
            {
                for (int i = 0; i < basePoints.Length; ++i)
                {
                    double pos = (double)i / (double)(basePoints.Length - 1);
                    pos = PositionOf(basePoints[i].p3d);
                    // keine fast identischen Punkte zufügen, die führen zu Nullvektoren in der Differenz
                    if (pos > StartPos + 1e-3 && pos < EndPos - 1e-3) spl.Add(basePoints[i]);
                }
                if (spl.Count == 1)
                {   // es müssen mindesten 3 basepoints vorhanden sein
                    double pos = (EndPos + StartPos) / 2.0;
                    ApproximatePosition(pos, out uv1, out uv2, out p);
                    spl.Add(new SurfacePoint(p, uv1, uv2));
                }
            }
            ApproximatePosition(EndPos, out uv1, out uv2, out p);
            spl.Add(new SurfacePoint(p, uv1, uv2));
            basePoints = spl.ToArray();
            MakeTrimmedPointsContinuous(spl.Count > 2 ? 1 : 0);
            InvalidateSecondaryData();
            BSpline init = ApproxBSpline;
        }
        /// <summary>
        /// The new end points of a trimmed curve come from <see cref="ApproximatePosition"/>, which takes their uv values from
        /// PositionOf. A point on the seam of a periodic surface may then lie one period away from its neighbour, and the 2d
        /// curves get a jump. So the uv values are made continuous, starting at the base point <paramref name="keep"/>, which
        /// is one of the original base points and stays in its period.
        /// </summary>
        private void MakeTrimmedPointsContinuous(int keep)
        {
            double u1 = surface1.IsUPeriodic ? surface1.UPeriod : 0.0, v1 = surface1.IsVPeriodic ? surface1.VPeriod : 0.0;
            double u2 = surface2.IsUPeriodic ? surface2.UPeriod : 0.0, v2 = surface2.IsVPeriodic ? surface2.VPeriod : 0.0;
            for (int i = keep + 1; i < basePoints.Length; i++)
            {
                SurfacePoint.FixSurfacePoint2D(ref basePoints[i].psurface1, basePoints[i - 1].psurface1, surface1.IsUPeriodic, u1, surface1.IsVPeriodic, v1);
                SurfacePoint.FixSurfacePoint2D(ref basePoints[i].psurface2, basePoints[i - 1].psurface2, surface2.IsUPeriodic, u2, surface2.IsVPeriodic, v2);
            }
            for (int i = keep - 1; i >= 0; i--)
            {
                SurfacePoint.FixSurfacePoint2D(ref basePoints[i].psurface1, basePoints[i + 1].psurface1, surface1.IsUPeriodic, u1, surface1.IsVPeriodic, v1);
                SurfacePoint.FixSurfacePoint2D(ref basePoints[i].psurface2, basePoints[i + 1].psurface2, surface2.IsUPeriodic, u2, surface2.IsVPeriodic, v2);
            }
        }
        public override IGeoObject Clone()
        {
            return new InterpolatedDualSurfaceCurve(this);
        }
        /// <summary>
        /// The copy <see cref="Clone"/> makes: the same base points and flag, and the same approximating
        /// spline, which is never modified in place, only replaced. The surfaces are cloned, because BRep operations
        /// need independent surfaces.
        /// <para>
        /// Clone used to rebuild the curve with the constructor, which refined the inner base points again, filled
        /// them up to nine and removed close ones. So a clone was a slightly different curve than its original, and
        /// most constructions of this class are clones.
        /// </para>
        /// </summary>
        private InterpolatedDualSurfaceCurve(InterpolatedDualSurfaceCurve toCopy)
            : this()
        {
            DualSurfaceCurveDiagnostics.ConstructionProbe probe = DualSurfaceCurveDiagnostics.BeginConstruction();
            surface1 = toCopy.surface1.Clone();
            surface2 = toCopy.surface2.Clone();
            basePoints = toCopy.basePoints.Clone() as SurfacePoint[];
            isTangential = toCopy.isTangential;
            approxBSpline = toCopy.approxBSpline;
            DualSurfaceCurveDiagnostics.EndConstruction(probe, surface1, surface2, basePoints, isTangential);
        }
        internal void SetSurfaces(ISurface surface1, ISurface surface2, bool swapped)
        {
            DualSurfaceCurveDiagnostics.Count("IDSC.SetSurfaces");
#if DEBUG
            bool ok = swapped == surface1.SameGeometry(BoundingRect.UnitBoundingRect, this.surface2, BoundingRect.UnitBoundingRect, 1e-6, out ModOp2D dumy);
#endif
            bool ok1 = surface1.SameGeometry(BoundingRect.UnitBoundingRect, this.surface1, BoundingRect.UnitBoundingRect, Precision.eps, out ModOp2D dumy1);
            bool ok2 = surface2.SameGeometry(BoundingRect.UnitBoundingRect, this.surface2, BoundingRect.UnitBoundingRect, Precision.eps, out ModOp2D dumy2);
            if (ok1 && ok2)
            {
                this.surface1 = surface1;
                this.surface2 = surface2;
                return;
            }
            else
            {
                ok1 = surface1.SameGeometry(BoundingRect.UnitBoundingRect, this.surface2, BoundingRect.UnitBoundingRect, Precision.eps, out dumy1);
                ok2 = surface2.SameGeometry(BoundingRect.UnitBoundingRect, this.surface1, BoundingRect.UnitBoundingRect, Precision.eps, out dumy2);
                if (ok1 && ok2)
                {
                    this.surface1 = surface1;
                    this.surface2 = surface2;
                    // if (swapped)
                    {
                        if (basePoints != null)
                        {
                            for (int i = 0; i < basePoints.Length; i++)
                            {
                                GeoPoint2D tmp = basePoints[i].psurface1;
                                basePoints[i].psurface1 = basePoints[i].psurface2;
                                basePoints[i].psurface2 = tmp;
                            }
                        }
                        hashedPositions.Clear();
                    }
                    return;
                }
                else throw new ApplicationException("Wrong surfaces in InterpolatedDualSurfaceCurve.SetSurfaces");
            }
        }
        public override ICurve CloneModified(ModOp m)
        {
            SurfacePoint[] sp = basePoints.Clone() as SurfacePoint[];
            for (int i = 0; i < sp.Length; ++i)
            {
                sp[i].p3d = m * sp[i].p3d;
            }
            InterpolatedDualSurfaceCurve ipdsc = new InterpolatedDualSurfaceCurve(surface1.GetModified(m), surface2.GetModified(m), sp, IsTangential);
            return ipdsc;
        }
        public override PlanarState GetPlanarState()
        {
            GeoPoint[] bp = new GeoPoint[basePoints.Length];
            for (int i = 0; i < bp.Length; i++)
            {
                bp[i] = basePoints[i].p3d;
            }
            double maxDist;
            bool isLinear;
            Plane.FromPoints(bp, out maxDist, out isLinear);
            if (isLinear) return PlanarState.UnderDetermined;
            if (maxDist < Precision.eps) return PlanarState.Planar;
            return PlanarState.NonPlanar;
        }
        public override Plane GetPlane()
        {
            if (surface1 is PlaneSurface) return (surface1 as PlaneSurface).Plane;
            if (surface2 is PlaneSurface) return (surface2 as PlaneSurface).Plane;
            GeoPoint[] bp = new GeoPoint[basePoints.Length];
            for (int i = 0; i < bp.Length; i++)
            {
                bp[i] = basePoints[i].p3d;
            }
            double maxDist;
            bool isLinear;
            return Plane.FromPoints(bp, out maxDist, out isLinear);
        }
        public override bool IsInPlane(Plane p)
        {
            if (surface1 is PlaneSurface) return (surface1 as PlaneSurface).Plane.SamePlane(p);
            if (surface2 is PlaneSurface) return (surface2 as PlaneSurface).Plane.SamePlane(p);
            for (int i = 0; i < basePoints.Length; i++)
            {
                if (Math.Abs(p.Distance(basePoints[i].p3d)) > Precision.eps) return false;
            }
            return true;
        }
        public override CADability.Curve2D.ICurve2D GetProjectedCurve(Plane p)
        {
            return base.GetProjectedCurve(p);
        }
        public override string Description
        {
            get
            {
                return StringTable.GetString("GeneralCurve.Description");
            }
        }
        public override bool IsComposed
        {
            get
            {
                return false;
            }
        }
        public override ICurve[] SubCurves
        {
            get { throw new Exception("The method or operation is not implemented."); }
        }

        ICurve IDualSurfaceCurve.Curve3D
        {
            get
            {
                return this;
            }
        }

        ISurface IDualSurfaceCurve.Surface1
        {
            get
            {
                return surface1;
            }
        }

        ICurve2D IDualSurfaceCurve.Curve2D1
        {
            get
            {
                return CurveOnSurface1;
            }
        }

        ISurface IDualSurfaceCurve.Surface2
        {
            get
            {
                return surface2;
            }
        }

        ICurve2D IDualSurfaceCurve.Curve2D2
        {
            get
            {
                return CurveOnSurface2;
            }
        }

        public bool IsTangential => isTangential;

        /// <summary>
        /// True when the two surfaces clearly intersect at every inner base point, i.e. the curve is certainly no
        /// tangential intersection. The end points are not considered, a curve may end where the surfaces touch.
        /// </summary>
        private bool IsTransversalAtAllInnerPoints()
        {
            if (basePoints.Length < 3) return false;
            for (int i = 1; i < basePoints.Length - 1; i++)
            {
                GeoVector n1 = surface1.GetNormal(basePoints[i].psurface1);
                GeoVector n2 = surface2.GetNormal(basePoints[i].psurface2);
                if (n1.IsNullVector() || n2.IsNullVector()) return false;
                if ((n1.Normalized ^ n2.Normalized).Length < 1e-2) return false;
            }
            return true;
        }

        ICurve2D IDualSurfaceCurve.GetCurveOnSurface(ISurface onThisSurface)
        {
            if (onThisSurface == surface1) return CurveOnSurface1;
            else if (onThisSurface == surface2) return CurveOnSurface2;
            else return null;
        }

        void IDualSurfaceCurve.SwapSurfaces()
        {
            DualSurfaceCurveDiagnostics.Count("IDSC.SwapSurfaces");
            (surface1, surface2) = (surface2, surface1);
            for (int i = 0; i < basePoints.Length; i++)
            {
                GeoPoint2D t = basePoints[i].psurface1;
                basePoints[i].psurface1 = basePoints[i].psurface2;
                basePoints[i].psurface2 = t;
            }
            InvalidateSecondaryData();
        }

        public override ICurve Approximate(bool linesOnly, double maxError)
        {
            if (linesOnly)
            {
                return Curves.ApproximateLinear(this, maxError);
            }
            else
            {
                ArcLineFitting3D alf = new ArcLineFitting3D(this, maxError, true, Math.Max(GetBasePoints().Length, 5));
                return alf.Approx;
            }
        }
        public override double[] TangentPosition(GeoVector direction)
        {
            throw new Exception("The method or operation is not implemented.");
        }
        public override double[] GetSelfIntersections()
        {
            throw new Exception("The method or operation is not implemented.");
        }
        public override bool SameGeometry(ICurve other, double precision)
        {
            if ((StartPoint | other.StartPoint) < precision && (EndPoint | other.EndPoint) < precision)
            {
                // same direction
                for (double par = 0.25; par < 1.0; par += 0.25)
                {
                    if ((PointAt(par) | other.PointAt(par)) > precision)
                    {   // the curves may run with different "speed", so we need to test the distance (which is more expensive)
                        if (other.DistanceTo(PointAt(par))>precision) return false;
                    }
                }
                return true;
            }
            if ((EndPoint | other.StartPoint) < precision && (StartPoint | other.EndPoint) < precision)
            {
                for (double par = 0.25; par < 1.0; par += 0.25)
                {
                    if ((PointAt(par) | other.PointAt(1 - par)) > precision) 
                    {   // the curves may run with different "speed", so we need to test the distance (which is more expensive)
                        if (other.DistanceTo(PointAt(par)) > precision) return false;
                    }
                }
                return true;
            }
            return false;
        }
        protected override double[] GetBasePoints()
        {
            double[] res = new double[basePoints.Length];
            for (int i = 0; i < res.Length; ++i)
            {
                res[i] = (double)i / (double)(basePoints.Length - 1);
            }
            return res;
        }
        #endregion
        #region ISerializable members
        protected InterpolatedDualSurfaceCurve(SerializationInfo info, StreamingContext context)
            : base(info, context)
        {
            surface1 = info.GetValue("Surface1", typeof(ISurface)) as ISurface;
            surface2 = info.GetValue("Surface2", typeof(ISurface)) as ISurface;
            basePoints = info.GetValue("BasePoints", typeof(SurfacePoint[])) as SurfacePoint[];
            hashedPositions = new SortedList<double, SurfacePoint>();
            // "ForwardOriented" is no longer read, see ForwardOrientedForOlderVersions
        }

        /// <summary>
        /// Older versions stored whether n1 x n2 runs along the curve ("ForwardOriented") and need it to read a file.
        /// The curve no longer keeps it: <see cref="StartDirection"/> and <see cref="EndDirection"/> take the sign from
        /// the approximating spline. So it is computed here the way those versions computed it, at an inner base point,
        /// because the surfaces may touch at the ends. Only surface normals are used, no spline is built while writing.
        /// </summary>
        /// <summary>
        /// Older versions read "Bounds1" and "Bounds2" and put the uv values into their periods with them. The curve has
        /// no bounds any more, it writes what those versions took when there were none: the domain of the surface, and
        /// where it has none, the extent of the uv values of the base points.
        /// </summary>
        private BoundingRect BoundsForOlderVersions(bool onSurface1)
        {
            ISurface surface = onSurface1 ? surface1 : surface2;
            if (surface is ISurfaceImpl simpl && !simpl.usedArea.IsEmpty() && !simpl.usedArea.IsInfinite) return simpl.usedArea;
            return GetBoundingRect(onSurface1);
        }
        private bool ForwardOrientedForOlderVersions()
        {
            int n = Math.Min(basePoints.Length / 2, basePoints.Length - 2);
            GeoVector v = surface1.GetNormal(basePoints[n].psurface1) ^ surface2.GetNormal(basePoints[n].psurface2);
            GeoVector chord = n == 0 ? basePoints[1].p3d - basePoints[0].p3d : basePoints[n + 1].p3d - basePoints[n - 1].p3d;
            return v * chord >= 0.0;
        }

        /// <summary>
        /// Implements <see cref="ISerializable.GetObjectData"/>
        /// </summary>
        /// <param name="info">The <see cref="System.Runtime.Serialization.SerializationInfo"/> to populate with data.</param>
        /// <param name="context">The destination (<see cref="System.Runtime.Serialization.StreamingContext"/>) for this serialization.</param>
        public override void GetObjectData(SerializationInfo info, StreamingContext context)
        {
            base.GetObjectData(info, context);
            info.AddValue("Surface1", surface1);
            info.AddValue("Surface2", surface2);
            info.AddValue("BasePoints", basePoints);
            info.AddValue("ForwardOriented", ForwardOrientedForOlderVersions());
        }
        public override void GetObjectData(IJsonWriteData data)
        {
            base.GetObjectData(data);
            data.AddProperty("Surface1", surface1);
            data.AddProperty("Surface2", surface2);
            data.AddProperty("BasePoints", basePoints);
            data.AddProperty("ForwardOriented", ForwardOrientedForOlderVersions());
            data.AddProperty("IsTangential", isTangential);
            data.AddProperty("Bounds1", BoundsForOlderVersions(true));
            data.AddProperty("Bounds2", BoundsForOlderVersions(false));
        }

        public override void SetObjectData(IJsonReadData data)
        {
            base.SetObjectData(data);
            surface1 = data.GetPropertyOrDefault<ISurface>("Surface1");
            surface2 = data.GetPropertyOrDefault<ISurface>("Surface2");
            basePoints = data.GetPropertyOrDefault<SurfacePoint[]>("BasePoints");
            // "ForwardOriented" is no longer read, see ForwardOrientedForOlderVersions
            if (data.Version >= 1)
            {
                isTangential = data.GetPropertyOrDefault<bool>("IsTangential");
                // "Bounds1" and "Bounds2" are no longer read, see BoundsForOlderVersions
            }
            data.RegisterForSerializationDoneCallback(this);
        }
        /// <summary>
        /// A file may contain inner base points which are not on the surfaces, or whose uv values do not describe
        /// them. UniteBug17 has such curves: they were written while CloneTrimmed handed a wrong isTangential, so their
        /// points were computed by the solver for touching surfaces, which failed, and the unrefined points were
        /// stored. Clone used to rebuild every curve and refined these points on the way. Clone copies now, so they
        /// are refined here, once, when the file is read. The end points are left alone, they belong to vertices.
        /// </summary>
        private void RefineInconsistentInnerPoints()
        {
            BoundingBox ext = BoundingBox.EmptyBoundingCube;
            for (int i = 0; i < basePoints.Length; i++) ext.MinMax(basePoints[i].p3d);
            double tolerance = Math.Max(100 * Precision.eps, 1e-6 * ext.Size);
            for (int i = 1; i < basePoints.Length - 1; i++)
            {
                if ((surface1.PointAt(basePoints[i].psurface1) | basePoints[i].p3d) <= tolerance
                    && (surface2.PointAt(basePoints[i].psurface2) | basePoints[i].p3d) <= tolerance) continue;
                DualSurfaceCurveDiagnostics.Count("IDSC read: an inner base point was not on the surfaces and is refined");
                // without a spline the plane goes through the base point itself, perpendicular to the base polygon
                approxBSpline = null;
                ApproximatePosition((double)i / (basePoints.Length - 1), out GeoPoint2D uv1, out GeoPoint2D uv2, out basePoints[i].p3d);
                basePoints[i].psurface1 = uv1;
                basePoints[i].psurface2 = uv2;
                hashedPositions.Clear();
            }
        }
        void IJsonSerializeDone.SerializationDone(JsonSerialize jsonSerialize)
        {
            if (jsonSerialize.GetTypeVersion(this.GetType()) < 1)
            {
                // Parameter isTangential was introduced in version 1
                // we have to determine it
                isTangential = true;
                for (int i = 1; i < basePoints.Length - 1; i++)
                {
                    GeoVector n1 = surface1.GetNormal(basePoints[i].psurface1).Normalized;
                    GeoVector n2 = surface2.GetNormal(basePoints[i].psurface2).Normalized;
                    if ((n1 ^ n2).Length > 1e-4)
                    {
                        isTangential = false;
                        break;

                    }
                }
            }
            jsonSerialize.InvokeSerializationDoneCallback(surface1);
            jsonSerialize.InvokeSerializationDoneCallback(surface2);
            // Files written while CloneTrimmed handed forwardOriented to the parameter isTangential may contain transversal
            // curves marked as tangential. Their points would be computed with the solver for touching surfaces, which
            // fails for them, so every point would silently remain unrefined.
            if (isTangential && IsTransversalAtAllInnerPoints()) isTangential = false;
            DualSurfaceCurveDiagnostics.ConstructionProbe probe = DualSurfaceCurveDiagnostics.BeginConstruction();
            RefineInconsistentInnerPoints();
            InitAfterReading();
            DualSurfaceCurveDiagnostics.EndConstruction(probe, surface1, surface2, basePoints, isTangential,
                "JSON deserialization, type version " + jsonSerialize.GetTypeVersion(this.GetType()).ToString());
        }
        int IExportStep.Export(ExportStep export, bool topLevel)
        {
            return (ToBSpline(export.Precision) as IExportStep).Export(export, topLevel);
        }

        IDualSurfaceCurve[] IDualSurfaceCurve.Split(double v)
        {
            ICurve[] crvs = (this as ICurve).Split(v);
            IDualSurfaceCurve[] res = new IDualSurfaceCurve[crvs.Length];
            for (int i = 0; i < crvs.Length; i++)
            {
                res[i] = crvs[i] as IDualSurfaceCurve;
            }
            return res;
        }

        GeoVector IOrientation.OrientationAt(double u)
        {
            GeoPoint2D uv1, uv2;
            GeoPoint p;
            ApproximatePosition(u, out uv1, out uv2, out p);
            GeoVector v = surface1.GetNormal(uv1).Normalized + surface2.GetNormal(uv2).Normalized;
            return v;
        }

        void IDualSurfaceCurve.Trim(GeoPoint startPoint, GeoPoint endPoint)
        {
            if (Precision.IsEqual(startPoint, StartPoint) && Precision.IsEqual(endPoint, EndPoint)) return;
            double posSp = PositionOf(startPoint);
            double posEp = PositionOf(endPoint);
            Trim(posSp, posEp);
        }

        #endregion
    }
}
