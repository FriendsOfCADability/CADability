using CADability.Curve2D;
using CADability.GeoObject;
using CADability.Shapes;
using CADability.UserInterface;
using MathNet.Numerics.LinearAlgebra;
using MathNet.Numerics.LinearAlgebra.Double;
using System;
using System.Collections.Generic;
using System.Runtime.Serialization;

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
    public class InterpolatedDualSurfaceCurve : GeneralCurve, IDualSurfaceCurve, IJsonSerialize, IExportStep, IJsonSerializeDone, IDeserializationCallback, IOrientation
    {
        ISurface surface1;
        ISurface surface2;
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

            public GeoPoint2D PointOnSurface(ISurface surface, BoundingRect bounds)
            {
                GeoPoint2D ps = surface.PositionOf(p3d);
                if (surface.IsUPeriodic)
                {
                    double um = (bounds.Left + bounds.Right) / 2;
                    while (Math.Abs(ps.x - um) > Math.Abs(ps.x - surface.UPeriod - um)) ps.x -= surface.UPeriod;
                    while (Math.Abs(ps.x - um) > Math.Abs(ps.x + surface.UPeriod - um)) ps.x += surface.UPeriod;
                }
                if (surface.IsVPeriodic)
                {
                    double vm = (bounds.Bottom + bounds.Top) / 2;
                    while (Math.Abs(ps.y - vm) > Math.Abs(ps.y - surface.VPeriod - vm)) ps.y -= surface.VPeriod;
                    while (Math.Abs(ps.y - vm) > Math.Abs(ps.y + surface.VPeriod - vm)) ps.y += surface.VPeriod;
                }
                return ps;
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
        SurfacePoint[] basePoints; // definiert die Punkte von 0.0 bis 1.0. Mindestens 3 Werte, der Mittelpunkt muss auch bekannt sein, immer ungerade wg. Mittelpunkt
        bool forwardOriented; // das Kreuzprodukt der beiden Normalenvektoren bildet die Richtung (oder Gegenrichtung) der Kurve
        ExplicitPCurve3D approxPolynom; // polynom of degree 3 approximating segments between basePoints
        Dictionary<double, SurfacePoint> hashedPositions;
#if DEBUG
        static int idcnt = 0;
        int id;
#endif
        [Serializable()]
        public class ProjectedCurve : GeneralCurve2D, ISerializable, IJsonSerialize
        {
            InterpolatedDualSurfaceCurve curve3d;
            bool onSurface1;
            bool reversed;
            public ProjectedCurve(InterpolatedDualSurfaceCurve curve3d, bool onSurface1)
            {
                this.curve3d = curve3d;
                this.onSurface1 = onSurface1;
                reversed = false;
                // base.MakeTringulation(); // zuerst mal zum Debuggen, später dynamisch
                // nicht mehr, da ggf. noch periodisch verschoben wird
            }
            public ProjectedCurve(InterpolatedDualSurfaceCurve curve3d, ProjectedCurve toCloneFrom)
            {
                this.curve3d = curve3d;
                this.onSurface1 = toCloneFrom.onSurface1;
                reversed = toCloneFrom.reversed;
            }
            public ProjectedCurve(InterpolatedDualSurfaceCurve curve3d, bool onSurface1, bool reversed)
            {
                this.curve3d = curve3d;
                this.onSurface1 = onSurface1;
                this.reversed = reversed;
            }
            public BSpline2D ToBSpline(double precision)
            {
                BSpline2D res = base.ToBspline(0.0);
                return res;
            }
            protected override void GetTriangulationBasis(out GeoPoint2D[] points, out GeoVector2D[] directions, out double[] parameters)
            {
                List<GeoPoint2D> lpoint = new List<GeoPoint2D>();
                List<GeoVector2D> ldirections = new List<GeoVector2D>();
                List<double> lparameters = new List<double>();
                if (onSurface1)
                {
                    double par;
                    for (int i = 0; i < curve3d.basePoints.Length; ++i)
                    {
                        int ii;
                        if (reversed) ii = curve3d.basePoints.Length - i - 1;
                        else ii = i;
                        GeoPoint2D p = curve3d.basePoints[ii].psurface1;
                        double pm = (double)i / (double)(curve3d.basePoints.Length - 1);
                        if (reversed) par = 1.0 - pm;
                        else par = pm;
                        GeoVector dir = curve3d.DirectionAt(par);
                        if (reversed) dir.Reverse();
                        GeoVector u = curve3d.surface1.UDirection(p);
                        GeoVector v = curve3d.surface1.VDirection(p);
                        GeoVector n = u ^ v;
                        Matrix m = DenseMatrix.OfColumnArrays(u, v, n);
                        Vector s = (Vector)m.Solve(new DenseVector(dir));
                        if (s.IsValid())
                        {
                            if (lpoint.Count > 0)
                            {
                                // bei periodischen darf der Abstand nicht zu groß werden
                                if (curve3d.surface1.IsUPeriodic)
                                {
                                    while (p.x - lpoint[lpoint.Count - 1].x > curve3d.surface1.UPeriod / 2) p.x -= curve3d.surface1.UPeriod;
                                    while (lpoint[lpoint.Count - 1].x - p.x > curve3d.surface1.UPeriod / 2) p.x += curve3d.surface1.UPeriod;
                                }
                                if (curve3d.surface1.IsVPeriodic)
                                {
                                    while (p.y - lpoint[lpoint.Count - 1].y > curve3d.surface1.VPeriod / 2) p.y -= curve3d.surface1.VPeriod;
                                    while (lpoint[lpoint.Count - 1].y - p.y > curve3d.surface1.VPeriod / 2) p.y += curve3d.surface1.VPeriod;
                                }
                            }
                            lpoint.Add(p);
                            if (reversed) lparameters.Add(1.0 - par);
                            else lparameters.Add(par);
                            ldirections.Add(new GeoVector2D(s[0], s[1]));
                        }
                    }
                }
                else
                {
                    double par;
                    for (int i = 0; i < curve3d.basePoints.Length; ++i)
                    {
                        int ii;
                        if (reversed) ii = curve3d.basePoints.Length - i - 1;
                        else ii = i;
                        GeoPoint2D p = curve3d.basePoints[ii].psurface2;
                        double pm = (double)i / (double)(curve3d.basePoints.Length - 1);
                        if (reversed) par = 1.0 - pm;
                        else par = pm;
                        GeoVector dir = curve3d.DirectionAt(par);
                        if (reversed) dir.Reverse();
                        GeoVector u = curve3d.surface2.UDirection(p);
                        GeoVector v = curve3d.surface2.VDirection(p);
                        GeoVector n = u ^ v;
                        Matrix m = DenseMatrix.OfColumnArrays(u, v, n);
                        Vector s = (Vector)m.Solve(new DenseVector(dir));
                        if (s.IsValid())
                        {
                            lpoint.Add(p);
                            if (reversed) lparameters.Add(1.0 - par);
                            else lparameters.Add(par);
                            ldirections.Add(new GeoVector2D(s[0], s[1]));
                        }
                    }
                }
                points = lpoint.ToArray();
                directions = ldirections.ToArray();
                parameters = lparameters.ToArray();
            }
            public override GeoVector2D DirectionAt(double par)
            {
                GeoPoint2D uv1, uv2;
                GeoPoint p;
                if (reversed) par = 1.0 - par;
                curve3d.ApproximatePosition(par, out uv1, out uv2, out p);
                GeoVector dir = curve3d.surface1.GetNormal(uv1).Normalized ^ curve3d.surface2.GetNormal(uv2).Normalized;
                if (dir.Length < 0.001)
                {
                    // an dieser Stelle sind beide Flächen tangential, das gibt keine gute Richtung!
                    // das ist aber eine brutale Lösung, die sollte nur sehr selten vorkommen
                    // Approximate darf man nicht aufrufen, da es wieder DirectionAt aufruft, wenn noch keine Triangulierung existiert
                    if (HasTriangulation())
                    {
                        if (reversed) par = 1.0 - par; // ggf. wieder richtig stellen, Approximate dreht ja bereits um!
                        GeoVector2D res = Approximate(true, 0.0).DirectionAt(par);
                        // if (reversed) return -res;
                        return res;
                    }
                    else
                    {
                        //GeoVector dir3d = curve3d.DirectionAt(par);
                        //if (onSurface1)
                        //{
                        //    GeoVector diru = curve3d.surface1.UDirection(uv1);
                        //    GeoVector dirv = curve3d.surface1.VDirection(uv1);
                        //    GeoVector2D res = Geometry.Dir2D(diru, dirv, dir3d);
                        //    return res; // Richtung hängt von reverse und curve3d.forwardOriented ab
                        //}
                        //else
                        //{
                        //    GeoVector diru = curve3d.surface2.UDirection(uv2);
                        //    GeoVector dirv = curve3d.surface2.VDirection(uv2);
                        //    GeoVector2D res = Geometry.Dir2D(diru, dirv, dir3d);
                        //    return res; // Richtung hängt von reverse und curve3d.forwardOriented ab
                        //}
                    }
                }
                if (!curve3d.forwardOriented) dir.Reverse();
                if (onSurface1)
                {
                    if (dir.Length < 0.001)
                    {
                        dir = curve3d.DirectionAt(par);
                    }
                    if (reversed) dir.Reverse();
                    GeoVector u = curve3d.surface1.UDirection(uv1);
                    GeoVector v = curve3d.surface1.VDirection(uv1);
                    GeoVector n = u ^ v; // geändert, wird bei BRepIntersection12 so gebraucht
                    Matrix m = DenseMatrix.OfColumnArrays(u, v, n);
                    Vector s = (Vector)m.Solve(new DenseVector(dir));
                    int ind = curve3d.SegmentOfParameter(par);
                    double d = curve3d.basePoints[ind].psurface1 | curve3d.basePoints[ind + 1].psurface1; // Abstand der umgebenden Basispunkte
                    double span = 1.0 / (curve3d.basePoints.Length - 1); // Parameterbereich zwischen zwei Basispunkten
                    if (s.IsValid() && s[0] != 0.0 && s[1] != 0.0)
                    {
                        GeoVector dbg = s[0] * u + s[1] * v + s[2] * n;
                        return d / span * (new GeoVector2D(s[0], s[1])).Normalized;
                    }
                    else
                    {
                        GeoVector2D res = curve3d.basePoints[ind + 1].psurface1 - curve3d.basePoints[ind].psurface1; // Abstand der umgebenden Basispunkte
                        if (reversed) res = -res;
                        return d / span * res.Normalized;
                    }
                }
                else
                {
                    if (reversed) dir.Reverse();
                    GeoVector u = curve3d.surface2.UDirection(uv2);
                    GeoVector v = curve3d.surface2.VDirection(uv2);
                    GeoVector n = u ^ v;
                    Matrix m = DenseMatrix.OfColumnArrays(u, v, n);
                    Vector s = (Vector)m.Solve(new DenseVector(dir));
                    int ind = curve3d.SegmentOfParameter(par);
                    double d = curve3d.basePoints[ind].psurface2 | curve3d.basePoints[ind + 1].psurface2; // Abstand der umgebenden Basispunkte
                    double span = 1.0 / (curve3d.basePoints.Length - 1); // Parameterbereich zwischen zwei Basispunkten
                    if (s.IsValid() && s[0] != 0.0 && s[1] != 0.0)
                    {
                        return d / span * (new GeoVector2D(s[0], s[1])).Normalized;
                    }
                    else
                    {
                        GeoVector2D res = curve3d.basePoints[ind + 1].psurface2 - curve3d.basePoints[ind].psurface2; // Abstand der umgebenden Basispunkte
                        if (reversed) res = -res;
                        return d / span * res.Normalized;
                    }
                }
            }
            public override GeoPoint2D PointAt(double par)
            {
                GeoPoint2D uv1, uv2;
                GeoPoint p;
                if (reversed) par = 1.0 - par;
                curve3d.ApproximatePosition(par, out uv1, out uv2, out p);
                if (onSurface1) return uv1;
                else return uv2;
            }
            public override double PositionOf(GeoPoint2D p)
            {   // in die 3d Situation übersetzen, demit die periodischen Flächen keine Probleme machen
                GeoPoint p3d;
                if (onSurface1) p3d = curve3d.surface1.PointAt(p);
                else p3d = curve3d.surface2.PointAt(p);
                double res = curve3d.PositionOf(p3d);
                if (reversed) return 1 - res;
                else return res;
            }
            public override GeoPoint2D StartPoint
            {
                get
                {
                    if (reversed)
                    {
                        if (onSurface1) return curve3d.basePoints[curve3d.basePoints.Length - 1].psurface1;
                        else return curve3d.basePoints[curve3d.basePoints.Length - 1].psurface2;
                    }
                    else
                    {
                        if (onSurface1) return curve3d.basePoints[0].psurface1;
                        else return curve3d.basePoints[0].psurface2;
                    }
                }
                set
                {   // das wird gebraucht, um kleine Lücken in einem Border zu schließen
                    if (reversed)
                    {
                        if (onSurface1) curve3d.basePoints[curve3d.basePoints.Length - 1].psurface1 = value;
                        else curve3d.basePoints[curve3d.basePoints.Length - 1].psurface2 = value;
                    }
                    else
                    {
                        if (onSurface1) curve3d.basePoints[0].psurface1 = value;
                        else curve3d.basePoints[0].psurface2 = value;
                    }
                    base.StartPoint = value;
                }
            }
            public override GeoPoint2D EndPoint
            {
                get
                {
                    if (reversed)
                    {
                        if (onSurface1) return curve3d.basePoints[0].psurface1;
                        else return curve3d.basePoints[0].psurface2;
                    }
                    else
                    {
                        if (onSurface1) return curve3d.basePoints[curve3d.basePoints.Length - 1].psurface1;
                        else return curve3d.basePoints[curve3d.basePoints.Length - 1].psurface2;
                    }
                }
                set
                {
                    if (reversed)
                    {
                        if (onSurface1) curve3d.basePoints[0].psurface1 = value;
                        else curve3d.basePoints[0].psurface2 = value;
                    }
                    else
                    {
                        if (onSurface1) curve3d.basePoints[curve3d.basePoints.Length - 1].psurface1 = value;
                        else curve3d.basePoints[curve3d.basePoints.Length - 1].psurface2 = value;
                    }
                    base.EndPoint = value;
                }
            }
            public override GeoVector2D StartDirection
            {
                get
                {
                    return DirectionAt(0.0);
                }
            }
            public override GeoVector2D EndDirection
            {
                get
                {
                    return DirectionAt(1.0);
                }
            }
            public override ICurve2D Trim(double StartPos, double EndPos)
            {
                double sp = StartPos;
                double ep = EndPos;
                InterpolatedDualSurfaceCurve clone = curve3d.Clone() as InterpolatedDualSurfaceCurve;
                clone.Trim(sp, ep);
                ProjectedCurve res = new ProjectedCurve(clone, onSurface1);
                res.reversed = reversed;
                res.ClearTriangulation();
                return res;
            }
            public override void Reverse()
            {
                reversed = !reversed;
                base.ClearTriangulation();
            }
            public override ICurve2D Clone()
            {
                ProjectedCurve res = new ProjectedCurve(curve3d.Clone() as InterpolatedDualSurfaceCurve, onSurface1);
                res.reversed = reversed;
                res.ClearTriangulation();
                res.UserData.CloneFrom(UserData);
                return res;
            }
            public override ICurve2D CloneReverse(bool reverse)
            {
                ProjectedCurve res = new ProjectedCurve(curve3d.Clone() as InterpolatedDualSurfaceCurve, onSurface1);
                if (reverse) res.reversed = !reversed;
                else res.reversed = reversed;
                res.ClearTriangulation();
                res.UserData.CloneFrom(UserData);
                return res;
            }
            public override ICurve2D GetModified(ModOp2D m)
            {
                // das geht ja eigentlich nicht, denn diese Kurve ist ja gegeben durch die 3d Kurve, und kann nicht einfach woandershin verschoben werden
                // ABER: nach einer Modifikation der Surface stimmen die basePoints der curve3d nicht mehr. Eigentlich müsste die curve3d das mitbekommen.
                // Die Methode ISurface.ReverseOrientation() verändert nämlich die surface. Die curve3d hier upzudaten ist ein Trick, der zwar nicht schadet, es ist aber nicht
                // die richtige Stelle es zu tun. Wir z.Z. nur bei Face.ReverseOrientation verwendet.
                curve3d.ModifySurfacePoints(onSurface1, m);
                ProjectedCurve res = new ProjectedCurve(curve3d, onSurface1);
                // curve3d.PointAt(0.1111111);
                // curve3d nicht clonen!!
                // if (m.Determinant < 0) res.reversed = !reversed;
                // else 
                res.reversed = reversed;
                res.ClearTriangulation();
                res.UserData.CloneFrom(UserData);
                return res;
            }
            public override bool IsClosed
            {
                get
                {
                    return false; // sollte nie geschlossen sein, oder?
                }
            }
            public override void Move(double x, double y)
            {
                ISurface surface;
                if (onSurface1) surface = curve3d.surface1;
                else surface = curve3d.surface2;
                if (x != 0 && surface.UPeriod != 0.0)
                {
                    if (Math.IEEERemainder(Math.Abs(x), surface.UPeriod) != 0.0) throw new ApplicationException("cannot move ProjectedCurve");
                }
                if (y != 0)
                {
                    if (Math.IEEERemainder(Math.Abs(y), surface.VPeriod) != 0.0) throw new ApplicationException("cannot move ProjectedCurve");
                }
                GeoVector2D diff = new GeoVector2D(x, y);
                if (onSurface1)
                {
                    for (int i = 0; i < curve3d.basePoints.Length; i++)
                    {
                        curve3d.basePoints[i].psurface1 += diff;
                    }
                }
                else
                {
                    for (int i = 0; i < curve3d.basePoints.Length; i++)
                    {
                        curve3d.basePoints[i].psurface2 += diff;
                    }
                }
                curve3d.InvalidateSecondaryData();
                base.ClearTriangulation();
            }
            #region ISerializable Members
            protected ProjectedCurve(SerializationInfo info, StreamingContext context)
                : base(info, context)
            {
                curve3d = info.GetValue("Curve3d", typeof(InterpolatedDualSurfaceCurve)) as InterpolatedDualSurfaceCurve;
                onSurface1 = info.GetBoolean("OnSurface1");
                reversed = info.GetBoolean("Reversed");
            }
            void ISerializable.GetObjectData(SerializationInfo info, StreamingContext context)
            {
                base.GetObjectData(info, context);
                info.AddValue("Curve3d", curve3d);
                info.AddValue("OnSurface1", onSurface1);
                info.AddValue("Reversed", reversed);
            }
            protected ProjectedCurve() { } // needed for IJsonSerialize
            public void GetObjectData(IJsonWriteData data)
            {
                base.JSonGetObjectData(data);
                data.AddProperty("Curve3d", curve3d);
                data.AddProperty("OnSurface1", onSurface1);
                data.AddProperty("Reversed", reversed);
            }

            public void SetObjectData(IJsonReadData data)
            {
                base.JSonSetObjectData(data);
                curve3d = data.GetProperty<InterpolatedDualSurfaceCurve>("Curve3d");
                onSurface1 = data.GetProperty<bool>("OnSurface1");
                reversed = data.GetProperty<bool>("Reversed");
            }

            #endregion
#if DEBUG
            GeoObjectList Debug
            {
                get
                {
                    GeoPoint2D[] pnts = new GeoPoint2D[101];
                    for (int i = 0; i < 101; ++i)
                    {
                        pnts[i] = PointAt(i / 100.0);
                    }
                    Polyline2D pl2d = new Polyline2D(pnts);
                    return new GeoObjectList(pl2d.MakeGeoObject(Plane.XYPlane));
                }
            }
#endif

            public override void Copy(ICurve2D toCopyFrom)
            {
                ProjectedCurve pc = toCopyFrom as ProjectedCurve;
                if (pc != null)
                {
                    curve3d = pc.curve3d;
                    onSurface1 = pc.onSurface1;
                    reversed = pc.reversed;
                }
            }

            internal void ReplaceSurface(ISurface oldSurface, ISurface newSurface)
            {
                curve3d.ReplaceSurface(oldSurface, newSurface);
            }
            internal bool IsOnSurface1
            {
                get
                {
                    return onSurface1;
                }
                set
                {
                    onSurface1 = value;
                }
            }
            internal bool IsReversed
            {
                get
                {
                    return reversed;
                }
            }
            internal void SetCurve3d(InterpolatedDualSurfaceCurve c3d)
            {
                if ((c3d.StartPoint | curve3d.StartPoint) + (c3d.EndPoint | curve3d.EndPoint) > (c3d.StartPoint | curve3d.EndPoint) + (c3d.EndPoint | curve3d.StartPoint)) reversed = !reversed;
                curve3d = c3d;
                // es muss sich hier um eine geometrisch identische Kurve handeln (Richtung?)
                ClearTriangulation();
            }

            public override bool TryPointDeriv2At(double position, out GeoPoint2D point, out GeoVector2D deriv, out GeoVector2D deriv2)
            {
                point = GeoPoint2D.Origin;
                deriv = deriv2 = GeoVector2D.NullVector;
                return false;
            }

            internal InterpolatedDualSurfaceCurve Curve3D
            {
                get
                {
                    return curve3d;
                }
            }
        }

        private void ModifySurfacePoints(bool onSurface1, ModOp2D m)
        {
            for (int i = 0; i < basePoints.Length; i++)
            {
                if (onSurface1) basePoints[i].psurface1 = m * basePoints[i].psurface1;
                else basePoints[i].psurface2 = m * basePoints[i].psurface2;
            }
            if (m.Determinant < 0) forwardOriented = !forwardOriented; // die Orientierung der Fläche hat sich umgedreht, damit ist auch das Kreuzprodukt andersrum
            InvalidateSecondaryData();
#if DEBUG
            CheckSurfaceParameters();
#endif
        }

        protected InterpolatedDualSurfaceCurve()
        {
            hashedPositions = new Dictionary<double, SurfacePoint>();
#if DEBUG
            id = idcnt++;
#endif
        }
        internal InterpolatedDualSurfaceCurve(ISurface surface1, ISurface surface2, SurfacePoint[] basePoints, bool forwardOriented, ExplicitPCurve3D approxPolynom = null)
            : this()
        {
            // der 1. und der letzte Punkt müssen exakt sein, die anderen nur Näherungswerte, die aber eindeutig zur Fläche führen
            this.surface1 = surface1;
            this.surface2 = surface2;
            this.basePoints = basePoints;
            this.forwardOriented = forwardOriented;
            this.approxPolynom = approxPolynom;
            CheckSurfaceExtents();
#if DEBUG
            CheckSurfaceParameters();
#endif
        }
        internal InterpolatedDualSurfaceCurve(ISurface surface1, ISurface surface2, SurfacePoint[] basePoints)
            : this()
        {
            // der 1. und der letzte Punkt müssen exakt sein, die anderen nur Näherungswerte, die aber eindeutig zur Fläche führen
            double dbg = basePoints[0].p3d | basePoints[basePoints.Length - 1].p3d;
            this.surface1 = surface1;
            this.surface2 = surface2;
            this.basePoints = basePoints;
            // wierum orientiert?
            // manchmal am Anfang oder Ende tangetial, deshalb besser in der mitte testen
            int n = basePoints.Length / 2; // es müssen mindesten 3 sein
            GeoVector v = surface1.GetNormal(basePoints[n].psurface1) ^ surface2.GetNormal(basePoints[n].psurface2);
            GeoVector v0;
            if (basePoints.Length == 2)
                v0 = basePoints[1].p3d - basePoints[0].p3d;
            else
                v0 = basePoints[n + 1].p3d - basePoints[n - 1].p3d;
            Angle a = new Angle(v, v0);
            forwardOriented = (a.Radian < Math.PI / 2.0);
            CheckSurfaceExtents();
#if DEBUG
            //GeoPoint[] pnts = new GeoPoint[501];
            //for (int i = 0; i < 501; ++i)
            //{
            //    pnts[i] = this.PointAt(i / 500.0);
            //}
            //Polyline pl = Polyline.Construct();
            //pl.SetPoints(pnts, false);
#endif
#if DEBUG
            CheckSurfaceParameters();
#endif
        }
        public InterpolatedDualSurfaceCurve(ISurface surface1, BoundingRect bounds1, ISurface surface2, BoundingRect bounds2, GeoPoint startPoint, GeoPoint endPoint)
            : this()
        {
            // die Bounds dienen dazu bei periodischen Flächen die richtigen Parameterwerte zu finden
            // diese Parameterbereiche sind wichtig, es darf also niemal PositionOf verwendet werden, sonst müssen wir
            // bounds1 und bounds2 speichern, um in den richtigen Bereich zu kommen
            this.surface1 = surface1;
            this.surface2 = surface2;
            List<SurfacePoint> points = new List<SurfacePoint>();
            SurfacePoint sp = new SurfacePoint();
            sp.p3d = startPoint;
            sp.psurface1 = sp.PointOnSurface(surface1, bounds1);
            sp.psurface2 = sp.PointOnSurface(surface2, bounds2);
            points.Add(sp);
            SurfacePoint ep = new SurfacePoint();
            ep.p3d = endPoint;
            ep.psurface1 = ep.PointOnSurface(surface1, bounds1);
            ep.psurface2 = ep.PointOnSurface(surface2, bounds2);
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
                //GeoPoint p = new GeoPoint(points[ind].p3d, points[ind + 1].p3d);
                points.Insert(ind + 1, new SurfacePoint(p, uv1, uv2));
                basePoints = points.ToArray(); // damit basePoints für die nächste Runde zu Verfügung steht
            }
            // wierum orientiert?
            // manchmal am Anfang oder Ende tangetial, deshalb besser in der mitte testen
            int n = basePoints.Length / 2; // es müssen mindesten 3 sein
            GeoVector v = surface1.GetNormal(basePoints[n].psurface1) ^ surface2.GetNormal(basePoints[n].psurface2);
            GeoVector v0 = basePoints[n + 1].p3d - basePoints[n - 1].p3d;
            Angle a = new Angle(v, v0);
            forwardOriented = (a.Radian < Math.PI / 2.0);
            CheckPeriodic();
            CheckSurfaceExtents();
#if DEBUG
            CheckSurfaceParameters();
#endif
        }
        public InterpolatedDualSurfaceCurve(ISurface surface1, BoundingRect bounds1, ISurface surface2, BoundingRect bounds2, GeoPoint[] pts, List<GeoPoint2D> uvpts1 = null, List<GeoPoint2D> uvpts2 = null)
        : this(surface1, bounds1, surface2, bounds2, new List<GeoPoint>(pts), uvpts1, uvpts2)
        {
        }
        public InterpolatedDualSurfaceCurve(ISurface surface1, BoundingRect bounds1, ISurface surface2, BoundingRect bounds2, List<GeoPoint> pts, List<GeoPoint2D> uvpts1 = null, List<GeoPoint2D> uvpts2 = null)
            : this()
        {
            // die Bounds dienen dazu bei periodischen Flächen die richtigen Parameterwerte zu finden
            // diese Parameterbereiche sind wichtig, es darf also niemal PositionOf verwendet werden, sonst müssen wir
            // bounds1 und bounds2 speichern, um in den richtigen Bereich zu kommen
            this.surface1 = surface1;
            this.surface2 = surface2;
            List<SurfacePoint> points = new List<SurfacePoint>();
            for (int i = 0; i < pts.Count; ++i)
            {
                SurfacePoint sp = new SurfacePoint();
                sp.p3d = pts[i];
                if (uvpts1 != null)
                    sp.psurface1 = uvpts1[i];
                else
                    sp.psurface1 = sp.PointOnSurface(surface1, bounds1);
                if (uvpts2 != null)
                    sp.psurface2 = uvpts2[i];
                else
                    sp.psurface2 = sp.PointOnSurface(surface2, bounds2);
                points.Add(sp);
            }
            if (points.Count == 2)
            {   // sometimes we have an ambiguous curve here: a half circle on a rotational surface, which could be either way around.
                // since "ApproximatePosition" doesn't care about the u/v bounds, we try a different approach here: choose a fixed u or v curve
                // in the bounds of such a surface and intersect with the other surface. The old approach was bad, more use of bounds1 and bounds2
                // could help further
                List<GeoPoint> intermediatePoints = new List<GeoPoint>();
                if (surface1.IsUPeriodic && Math.Abs(points[0].psurface1.x - points[1].psurface1.x) > surface1.UPeriod / 3.0)
                {   // the curve spans more than 1/3 of a total period
                    ICurve fixedCurve = surface1.FixedU((points[0].psurface1.x + points[1].psurface1.x) / 2.0, bounds1.Bottom, bounds1.Top);
                    surface2.Intersect(fixedCurve, bounds2, out GeoPoint[] ips, out GeoPoint2D[] uvOn2, out double[] uOnCurve3Ds);
                    intermediatePoints.AddRange(ips);
                }
                if (surface1.IsVPeriodic && Math.Abs(points[0].psurface1.y + points[1].psurface1.y) > surface1.VPeriod / 3.0)
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
                if (surface2.IsVPeriodic && Math.Abs(points[0].psurface2.y + points[1].psurface2.y) > surface2.VPeriod / 3.0)
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
                        sp.psurface2 = surface1.PositionOf(sp.p3d);
                        SurfaceHelper.AdjustPeriodic(surface1, bounds1, ref sp.psurface1);
                        SurfaceHelper.AdjustPeriodic(surface2, bounds2, ref sp.psurface2);
                        points.Insert(1, sp);
                    }
                }
                /* old approach:
                if (!(surface1 is PlaneSurface))
                {
                    ICurve fixedCurve;
                    GeoPoint2D uv0 = points[0].psurface1;
                    GeoPoint2D uv1 = points[1].psurface1;
                    double du = Math.Abs(uv0.x - uv1.x);
                    double dv = Math.Abs(uv0.y - uv1.y);
                    if (du > dv) fixedCurve = surface1.FixedU((uv0.x + uv1.x) / 2.0, bounds1.Bottom, bounds1.Top);
                    else fixedCurve = surface1.FixedV((uv0.y + uv1.y) / 2.0, bounds1.Left, bounds1.Right);
                    surface2.Intersect(fixedCurve, bounds2, out GeoPoint[] ips, out GeoPoint2D[] uvOn2, out double[] uOnCurve3Ds);
                    double mind = double.MaxValue;
                    GeoPoint2D cnt = bounds1.GetCenter();
                    int indFound = -1;
                    for (int i = 0; i < ips.Length; i++)
                    {   // find the best intersection point
                        GeoPoint2D uv = surface1.PositionOf(ips[i]);
                        double d = Math.Abs(uv.x - cnt.x) / bounds1.Width + Math.Abs(uv.y - cnt.y) / bounds1.Height;
                        if (d < mind)
                        {
                            indFound = i;
                            mind = d;
                        }
                    }
                    if (indFound >= 0)
                    {
                        SurfacePoint sp = new SurfacePoint();
                        sp.p3d = ips[indFound];
                        sp.psurface1 = surface1.PositionOf(sp.p3d);
                        sp.psurface2 = uvOn2[indFound];
                        points.Insert(1, sp);
                    }
                }
                else if (!(surface2 is PlaneSurface))
                {
                    ICurve fixedCurve;
                    GeoPoint2D uv0 = points[0].psurface2;
                    GeoPoint2D uv1 = points[1].psurface2;
                    double du = Math.Abs(uv0.x - uv1.x);
                    double dv = Math.Abs(uv0.y - uv1.y);
                    if (du > dv) fixedCurve = surface2.FixedU((uv0.x + uv1.x) / 2.0, bounds2.Bottom, bounds2.Top);
                    else fixedCurve = surface2.FixedV((uv0.y + uv1.y) / 2.0, bounds2.Left, bounds2.Right);
                    surface1.Intersect(fixedCurve, bounds1, out GeoPoint[] ips, out GeoPoint2D[] uvOn1, out double[] uOnCurve3Ds);
                    double mind = double.MaxValue;
                    GeoPoint2D cnt = bounds2.GetCenter();
                    int indFound = -1;
                    for (int i = 0; i < ips.Length; i++)
                    {   // find the best intersection point
                        GeoPoint2D uv = surface2.PositionOf(ips[i]);
                        double d = Math.Abs(uv.x - cnt.x) / bounds2.Width + Math.Abs(uv.y - cnt.y) / bounds2.Height;
                        if (d < mind)
                        {
                            indFound = i;
                            mind = d;
                        }
                    }
                    if (indFound >= 0)
                    {
                        SurfacePoint sp = new SurfacePoint();
                        sp.p3d = ips[indFound];
                        sp.psurface2 = surface2.PositionOf(sp.p3d);
                        sp.psurface1 = uvOn1[indFound];
                        points.Insert(1, sp);
                    }
                } */
            }
            basePoints = points.ToArray();
            // determin the orientation: sometimes both surfaces are tangential at the start or endpoint, so we use an intermedite point when available
            int n = Math.Min(basePoints.Length / 2, basePoints.Length - 2);
            GeoVector v = surface1.GetNormal(basePoints[n].psurface1) ^ surface2.GetNormal(basePoints[n].psurface2);
            GeoVector v0;
            if (n == 0) v0 = basePoints[n + 1].p3d - basePoints[n].p3d; // only two points
            else v0 = basePoints[n + 1].p3d - basePoints[n - 1].p3d;
            Angle a = new Angle(v, v0);
            forwardOriented = (a.Radian < Math.PI / 2.0); // we need this value for ApproximatePosition

            // Recalculate the positions of the inner points, which are sometimes not precise
            for (int i = 1; i < basePoints.Length - 1; ++i)
            {
                GeoPoint2D uv1, uv2; // do not pass out basePoints[i].psurface1 as parameter, since uv1 is manipulated several times inside ApproximatePosition
                ApproximatePosition((double)i / (double)(basePoints.Length - 1), out uv1, out uv2, out basePoints[i].p3d, true);
                basePoints[i].psurface1 = uv1;
                basePoints[i].psurface2 = uv2;
            }
            AdjustPeriodic(bounds1, bounds2);

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
                approxPolynom = null;
                //GeoPoint p = new GeoPoint(points[ind].p3d, points[ind + 1].p3d);
                points.Insert(ind + 1, new SurfacePoint(p, uv1, uv2));
                basePoints = points.ToArray(); // damit basePoints für die nächste Runde zu Verfügung steht
                v = surface1.GetNormal(basePoints[ind + 1].psurface1) ^ surface2.GetNormal(basePoints[ind + 1].psurface2);
                v0 = basePoints[ind + 2].p3d - basePoints[ind].p3d;
                a = new Angle(v, v0);
                forwardOriented = (a.Radian < Math.PI / 2.0); // recalculate, because for exactly half circles the first result is ambiguous
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

            InitApproxPolynom();
            CheckPeriodic(); // erst nach dieser Schleife, denn ApproximatePosition mach die uv-position evtl. falsch
            // maybe we end up in the wron period
            AdjustPeriodic(bounds1, bounds2);
            CheckSurfaceExtents();
#if DEBUG
            GeoPoint2D[] dbgb1 = new GeoPoint2D[basePoints.Length];
            GeoPoint2D[] dbgb2 = new GeoPoint2D[basePoints.Length];
            for (int i = 0; i < basePoints.Length; i++)
            {
                dbgb1[i] = basePoints[i].psurface1;
                dbgb2[i] = basePoints[i].psurface2;
            }
            Polyline2D pl2d1 = new Polyline2D(dbgb1);
            Polyline2D pl2d2 = new Polyline2D(dbgb2);
            CheckSurfaceParameters();
#endif
        }
        private void InitApproxPolynom()
        {   // The ExplicitPCurve3D (a polynom) approximates the intersection curve, passing throught the basepoints with the correct direction in the basepoints
            GeoPoint[] epnts = new GeoPoint[basePoints.Length];
            GeoVector[] edirs = new GeoVector[basePoints.Length];
            BoundingRect ext1 = BoundingRect.EmptyBoundingRect;
            BoundingRect ext2 = BoundingRect.EmptyBoundingRect;
            for (int i = 0; i < basePoints.Length; i++)
            {
                epnts[i] = basePoints[i].p3d;
                ext1.MinMax(basePoints[i].psurface1);
                ext2.MinMax(basePoints[i].psurface2);
            }
            ext1.Inflate(ext1.Size * 0.1);
            ext2.Inflate(ext2.Size * 0.1);
            for (int i = 0; i < basePoints.Length; i++)
            {
                edirs[i] = surface1.GetNormal(basePoints[i].psurface1) ^ surface2.GetNormal(basePoints[i].psurface2);
                if (forwardOriented)
                    edirs[i] = surface1.GetNormal(basePoints[i].psurface1) ^ surface2.GetNormal(basePoints[i].psurface2);
                else
                    edirs[i] = -surface1.GetNormal(basePoints[i].psurface1) ^ surface2.GetNormal(basePoints[i].psurface2);
                double l;
                if (i == 0) l = epnts[1] | epnts[0];
                else if (i == basePoints.Length - 1) l = epnts[basePoints.Length - 1] | epnts[basePoints.Length - 2];
                else l = ((epnts[i] | epnts[i - 1]) + (epnts[i] | epnts[i + 1])) / 2.0;
                if (Precision.IsNullVector(edirs[i]))
                {   // this is a touching point. We assume, that touching points (if there are any) are always basepoints.
                    // To get a good tangential direction of the curve in that point we construct a plane based by the normal vector and the connection to the next basepoint
                    // this plane intersects the surfaces in a curve. We use the tangent verctor of this curve as the tangent vector 
                    GeoVector dir, dir1 = GeoVector.NullVector, dir2 = GeoVector.NullVector;
                    if (i == 0) dir = epnts[i + 1] - epnts[i];
                    else if (i == basePoints.Length - 1) dir = epnts[i] - epnts[i - 1];
                    else dir = (epnts[i + 1] - epnts[i - 1]);
                    Plane pln = new Plane(epnts[i], surface1.GetNormal(basePoints[i].psurface1), dir);
                    IDualSurfaceCurve[] dsc1 = surface1.GetPlaneIntersection(new PlaneSurface(pln), ext1.Left, ext1.Right, ext1.Bottom, ext1.Top, 0.0);
                    pln = new Plane(epnts[i], surface2.GetNormal(basePoints[i].psurface2), dir); // this is almost the same plane as above
                    IDualSurfaceCurve[] dsc2 = surface2.GetPlaneIntersection(new PlaneSurface(pln), ext2.Left, ext2.Right, ext2.Bottom, ext2.Top, 0.0);
                    for (int j = 0; j < dsc1.Length; j++)
                    {
                        double pos = dsc1[j].Curve3D.PositionOf(epnts[i]);
                        if (pos >= -1e-6 && pos <= 1 + 1e-6)
                        {
                            dir1 = dsc1[j].Curve3D.DirectionAt(pos);
                            if (dir1 * dir < 0) dir1 = -dir1;
                        }
                    }
                    for (int j = 0; j < dsc2.Length; j++)
                    {
                        double pos = dsc2[j].Curve3D.PositionOf(epnts[i]);
                        if (pos >= -1e-6 && pos <= 1 + 1e-6)
                        {
                            dir2 = dsc2[j].Curve3D.DirectionAt(pos);
                            if (dir2 * dir < 0) dir2 = -dir2;
                        }
                    }
                    GeoVector dir12 = dir1 + dir2; // dir1 and dir2 should be very similar, if one of them is the nullvector, there is no problem
                    if (!dir12.IsNullVector()) edirs[i] = dir12;
                    else edirs[i] = dir;
                }
                edirs[i].Length = l * basePoints.Length;
            }
            approxPolynom = ExplicitPCurve3D.FromPointsDirections(epnts, edirs);
        }
        internal void CheckSurfaceExtents()
        {
            if (surface1 is ISurfaceImpl simpl1)
            {
                if (simpl1.usedArea.IsEmpty() || simpl1.usedArea.IsInfinite)
                {
                    BoundingRect ext = BoundingRect.EmptyBoundingRect;
                    for (int i = 0; i < basePoints.Length; i++)
                    {
                        ext.MinMax(basePoints[i].psurface1);
                    }
                    simpl1.usedArea = ext;
                }
            }
            if (surface2 is ISurfaceImpl simpl2)
            {
                if (simpl2.usedArea.IsEmpty() || simpl2.usedArea.IsInfinite)
                {
                    BoundingRect ext = BoundingRect.EmptyBoundingRect;
                    for (int i = 0; i < basePoints.Length; i++)
                    {
                        ext.MinMax(basePoints[i].psurface2);
                    }
                    simpl2.usedArea = ext;
                }
            }

        }
#if DEBUG
        internal void CheckSurfaceParameters()
        {   // check auf parameterfehler im 2d
            if ((basePoints[basePoints.Length - 1].p3d | basePoints[basePoints.Length - 2].p3d) == 0.0 || (basePoints[0].p3d | basePoints[1].p3d) == 0.0)
            {

            }
            double d = 0.0;
            for (int i = 1; i < basePoints.Length; i++)
            {
                d += basePoints[i].psurface1 | basePoints[i - 1].psurface1;
            }
            d /= basePoints.Length - 1;
            for (int i = 1; i < basePoints.Length; i++)
            {
                if ((basePoints[i].psurface1 | basePoints[i - 1].psurface1) > 5 * d)
                {
                }
            }
            for (int i = 1; i < basePoints.Length; i++)
            {
                d += basePoints[i].psurface2 | basePoints[i - 1].psurface2;
            }
            d /= basePoints.Length - 1;
            for (int i = 1; i < basePoints.Length; i++)
            {
                if ((basePoints[i].psurface2 | basePoints[i - 1].psurface2) > 5 * d)
                {
                }
            }
            Surface2.GetNaturalBounds(out double umin, out double umax, out double vmin, out double vmax);
            for (int i = 0; i < basePoints.Length; i++)
            {
                if ((surface1.PointAt(basePoints[i].psurface1) | basePoints[i].p3d) > 0.1)
                {
                    return;
                }
                if ((surface2.PointAt(basePoints[i].psurface2) | basePoints[i].p3d) > 0.1)
                {
                    return;
                }
                if (basePoints[i].psurface2.x < umin || basePoints[i].psurface2.x > umax)
                {

                }
            }
        }
#endif
        internal void Repair(BoundingRect bounds1, BoundingRect bounds2)
        {
            BoundingCube ext = BoundingCube.EmptyBoundingCube;
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
                RecalcSurfacePoints(bounds1, bounds2);
            }
        }
        private void CheckPeriodic()
        {
            for (int i = 1; i < basePoints.Length; i++)
            {
                AdjustPeriodic(ref basePoints[i].psurface1, true, i - 1);
                AdjustPeriodic(ref basePoints[i].psurface2, false, i - 1);
            }
            //for (int i = 1; i < basePoints.Length; i++)
            //{
            //    double d = basePoints[i].psurface1 | basePoints[i - 1].psurface1;
            //    if (surface1.IsUPeriodic)
            //    {
            //        GeoPoint2D uv1 = basePoints[i].psurface1;
            //        uv1.x = uv1.x + surface1.UPeriod;
            //        double d1 = uv1 | basePoints[i - 1].psurface1;
            //        if (d1 < d)
            //        {
            //            d = d1;
            //            basePoints[i].psurface1 = uv1;
            //        }
            //        uv1.x = basePoints[i].psurface1.x - surface1.UPeriod;
            //        d1 = uv1 | basePoints[i - 1].psurface1;
            //        if (d1 < d)
            //        {
            //            d = d1;
            //            basePoints[i].psurface1 = uv1;
            //        }
            //    }
            //    if (surface1.IsVPeriodic)
            //    {
            //        GeoPoint2D uv1 = basePoints[i].psurface1;
            //        uv1.y = uv1.y + surface1.VPeriod;
            //        double d1 = uv1 | basePoints[i - 1].psurface1;
            //        if (d1 < d)
            //        {
            //            d = d1;
            //            basePoints[i].psurface1 = uv1;
            //        }
            //        uv1.y = basePoints[i].psurface1.y - surface1.VPeriod;
            //        d1 = uv1 | basePoints[i - 1].psurface1;
            //        if (d1 < d)
            //        {
            //            d = d1;
            //            basePoints[i].psurface1 = uv1;
            //        }
            //    }
            //    d = basePoints[i].psurface2 | basePoints[i - 1].psurface2;
            //    if (surface2.IsUPeriodic)
            //    {
            //        GeoPoint2D uv1 = basePoints[i].psurface2;
            //        uv1.x = uv1.x + surface2.UPeriod;
            //        double d1 = uv1 | basePoints[i - 1].psurface2;
            //        if (d1 < d)
            //        {
            //            d = d1;
            //            basePoints[i].psurface2 = uv1;
            //        }
            //        uv1.x = basePoints[i].psurface2.x - surface2.UPeriod;
            //        d1 = uv1 | basePoints[i - 1].psurface2;
            //        if (d1 < d)
            //        {
            //            d = d1;
            //            basePoints[i].psurface2 = uv1;
            //        }
            //    }
            //    if (surface2.IsVPeriodic)
            //    {
            //        GeoPoint2D uv1 = basePoints[i].psurface2;
            //        uv1.y = uv1.y + surface2.VPeriod;
            //        double d1 = uv1 | basePoints[i - 1].psurface2;
            //        if (d1 < d)
            //        {
            //            d = d1;
            //            basePoints[i].psurface2 = uv1;
            //        }
            //        uv1.y = basePoints[i].psurface2.y - surface2.VPeriod;
            //        d1 = uv1 | basePoints[i - 1].psurface2;
            //        if (d1 < d)
            //        {
            //            d = d1;
            //            basePoints[i].psurface2 = uv1;
            //        }
            //    }
            //}
        }
        internal void AdjustPeriodic(BoundingRect bounds1, BoundingRect bounds2)
        {   // we need to consider the whole curve, not just individual points, because the bounds may be too narrow and some points fall outside
            // we expect that the 2d points are in a row and have no periodic jumps
            GeoPoint2D[] p2d = new GeoPoint2D[basePoints.Length];
            for (int i = 0; i < basePoints.Length; i++) p2d[i] = basePoints[i].psurface1;
            SurfaceHelper.AdjustPeriodic(surface1, bounds1, p2d);
            for (int i = 0; i < basePoints.Length; i++) basePoints[i].psurface1 = p2d[i];
            for (int i = 0; i < basePoints.Length; i++) p2d[i] = basePoints[i].psurface2;
            SurfaceHelper.AdjustPeriodic(surface2, bounds2, p2d);
            for (int i = 0; i < basePoints.Length; i++) basePoints[i].psurface2 = p2d[i];
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
        internal DualSurfaceCurve ToDualSurfaceCurve()
        {
            return new DualSurfaceCurve(this, surface1, new ProjectedCurve(this, true), surface2, new ProjectedCurve(this, false));
        }
        private SurfacePoint GetPoint(GeoPoint fromHere)
        {   // liefert einen Punkt möglichst nahe bei fromHere
            GeoPoint2D uv1 = surface1.PositionOf(fromHere);
            GeoPoint2D uv2 = surface2.PositionOf(fromHere);
            GeoPoint p1 = surface1.PointAt(uv1);
            GeoPoint p2 = surface2.PointAt(uv2);
            while ((p1 | p2) > Precision.eps) // zu unsichere Bedingung, vor allem wenns tangential wird...
            {
                GeoVector normal = surface1.GetNormal(uv1) ^ surface2.GetNormal(uv2);
                GeoPoint location = new GeoPoint(surface1.PointAt(uv1), surface1.PointAt(uv2));
                Plane pln = new Plane(location, normal); // in dieser Ebene suchen wir jetzt den Schnittpunkt
                // nach dem Tangentenverfahren, da wir ja schon ganz nah sind, oder?
                // p1,du1,dv1 und p2,du2,dv2 sind die beiden Tangentialebenen, p3,du3,dv3 die senkerechte Ebene
                // daraus ergeben sich 6 Gleichungen mit 6 unbekannten
                // p1+u1*du1+v1*dv1 = p3+u3*du3+v3*dv3
                // p2+u2*du2+v2*dv2 = p3+u3*du3+v3*dv3
                // umgeformt: (u3 und v3 interessieren nicht, deshalb Vorzeichen egal
                // u1*du1 + v1*dv1 + 0      + 0      - u3*du3 - v3*dv3 = p3-p1
                // 0      + 0      + u2*du2 + v2*dv2 - u3*du3 - v3*dv3 = p3-p1
                GeoVector du1 = surface1.UDirection(uv1);
                GeoVector dv1 = surface1.VDirection(uv1);
                GeoVector du2 = surface2.UDirection(uv2);
                GeoVector dv2 = surface2.VDirection(uv2);
                GeoVector du3 = pln.DirectionX;
                GeoVector dv3 = pln.DirectionY;
#if DEBUG
                PlaneSurface pls1 = new PlaneSurface(new Plane(p1, du1, dv1));
                SimpleShape ss1 = new SimpleShape(new BoundingRect(-1, -1, 1, 1));
                Face fc1 = Face.MakeFace(pls1, ss1);
                PlaneSurface pls2 = new PlaneSurface(new Plane(p2, du2, dv2));
                SimpleShape ss2 = new SimpleShape(new BoundingRect(-1, -1, 1, 1));
                Face fc2 = Face.MakeFace(pls2, ss2);
                PlaneSurface pls3 = new PlaneSurface(new Plane(location, du3, dv3));
                SimpleShape ss3 = new SimpleShape(new BoundingRect(-1, -1, 1, 1));
                Face fc3 = Face.MakeFace(pls3, ss3);
                DebuggerContainer dc = new DebuggerContainer();
                dc.Add(fc1);
                dc.Add(fc2);
                dc.Add(fc3);
#endif
                Matrix m = DenseMatrix.OfArray(new double[,]
                {
                    {du1.x,dv1.x,0,0,du3.x,dv3.x},
                    {du1.y,dv1.y,0,0,du3.y,dv3.y},
                    {du1.z,dv1.z,0,0,du3.z,dv3.z},
                    {0,0,du2.x,dv2.x,du3.x,dv3.x},
                    {0,0,du2.y,dv2.y,du3.y,dv3.y},
                    {0,0,du2.z,dv2.z,du3.z,dv3.z},
                });
                Matrix s = (Matrix)m.Solve(DenseMatrix.OfArray(new double[,] { { location.x - p1.x }, { location.y - p1.y }, { location.z - p1.z } ,
                { location.x - p2.x }, { location.y - p2.y }, { location.z - p2.z } }));
                uv1.x += s[0, 0];
                uv1.y += s[1, 0];
                uv2.x += s[2, 0];
                uv2.y += s[3, 0];
                p1 = surface1.PointAt(uv1);
                p2 = surface2.PointAt(uv2);
            }
            return new SurfacePoint(new GeoPoint(p1, p2), uv1, uv2);
        }
        private SurfacePoint[] Interpolate(SurfacePoint sp1, SurfacePoint sp2)
        {
            double du1 = sp1.psurface1.x - sp2.psurface1.x;
            double dv1 = sp1.psurface1.y - sp2.psurface1.y;
            double du2 = sp1.psurface2.x - sp2.psurface2.x;
            double dv2 = sp1.psurface2.y - sp2.psurface2.y;
            // wir suchen die Fläche, in dessen u/v system die Linie am nächsten zu einer Achse ist
            double f1, f2;
            if (Math.Abs(du1) > Math.Abs(dv1)) f1 = Math.Abs(dv1) / Math.Abs(du1);
            else f1 = Math.Abs(du1) / Math.Abs(dv1);
            if (Math.Abs(du2) > Math.Abs(dv2)) f2 = Math.Abs(dv2) / Math.Abs(du2);
            else f2 = Math.Abs(du2) / Math.Abs(dv2);
            ISurface withCurve, opposite;
            if (f1 < f2)
            {   // erste Fläche ist besser
                withCurve = surface1;
                opposite = surface2;
            }
            else
            {
                withCurve = surface2;
                opposite = surface1;
            }
            return null;
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
            {   // evtl Cache?
                return new ProjectedCurve(this, true);
            }
        }
        public ICurve2D CurveOnSurface2
        {
            get
            {
                return new ProjectedCurve(this, false);
            }
        }
        protected override void InvalidateSecondaryData()
        {
            base.InvalidateSecondaryData();
            hashedPositions.Clear();
            approxPolynom = null;
        }
        internal void ReplaceSurface(ISurface oldSurface, ISurface newSurface)
        {   // die beiden surfaces müssen geometrisch identisch sein
            if (surface1 == oldSurface) surface1 = newSurface;
            if (surface2 == oldSurface) surface2 = newSurface;
        }
        internal void ReplaceSurface(ISurface oldSurface, ISurface newSurface, ModOp2D oldToNew)
        {   // die beiden surfaces müssen geometrisch identisch sein
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
        internal InterpolatedDualSurfaceCurve CloneTrimmed(double startPos, double endPos, ProjectedCurve c1, ProjectedCurve c2, out ICurve2D c1trimmed, out ICurve2D c2trimmed)
        {
            InterpolatedDualSurfaceCurve res = new InterpolatedDualSurfaceCurve(surface1, surface2, basePoints.Clone() as SurfacePoint[], forwardOriented);
            res.Trim(startPos, endPos);
            c1trimmed = new ProjectedCurve(res, c1);
            c2trimmed = new ProjectedCurve(res, c2);
            return res;
        }
        public BSpline ToBSpline(double precision)
        {
            List<GeoPoint> throughPoints = new List<GeoPoint>();
            for (int i = 0; i < basePoints.Length; i++)
            {
                throughPoints.Add(basePoints[i].p3d);
            }
            BSpline res = BSpline.Construct();
            res.ThroughPoints(throughPoints.ToArray(), 3, false);
            // hier noch Genauikeit überprüfen und throughpoints vermehren
            return res;
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
#endif
        #region IGeoObject override
        /// <summary>
        /// Overrides <see cref="CADability.GeoObject.IGeoObjectImpl.GetBoundingCube ()"/>
        /// </summary>
        /// <returns></returns>
        public override BoundingCube GetBoundingCube()
        {
            BoundingCube res = new BoundingCube();
            for (int i = 0; i < basePoints.Length; ++i)
            {
                res.MinMax(basePoints[i].p3d);
            }
            return res;
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
            if (approxPolynom != null) approxPolynom = approxPolynom.GetModified(m);
        }
        /// <summary>
        /// Overrides <see cref="CADability.GeoObject.IGeoObjectImpl.GetExtent (double)"/>
        /// </summary>
        /// <param name="precision"></param>
        /// <returns></returns>
        public override BoundingCube GetExtent(double precision)
        {
            BoundingCube res = BoundingCube.EmptyBoundingCube;
            for (int i = 0; i < basePoints.Length; ++i)
            {
                res.MinMax(basePoints[i].p3d);
            }
            return res;
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
        //public override bool HitTest(ref BoundingCube cube, double precision)
        //{   // soll in GeneralCurve mit TetraederHülle gemacht werden, vorläufig:
        //    for (int i = 0; i < basePoints.Length - 1; ++i)
        //    {
        //        GeoPoint sp = basePoints[i].p3d;
        //        GeoPoint ep = basePoints[i + 1].p3d;
        //        if (cube.Interferes(ref sp, ref ep)) return true;
        //    }
        //    return false;
        //}
        //public override bool HitTest(Projection projection, BoundingRect rect, bool onlyInside)
        //{   // sollte in GeneralCurve gelöst werden, hier erstmal:
        //    if (onlyInside)
        //    {
        //        BoundingRect ext = BoundingRect.EmptyBoundingRect;
        //        for (int i = 0; i < basePoints.Length; ++i)
        //        {
        //            ext.MinMax(projection.ProjectUnscaled(basePoints[i].p3d));
        //        }
        //        return ext <= rect;
        //    }
        //    else
        //    {
        //        ClipRect clr = new ClipRect(ref rect);
        //        for (int i = 0; i < basePoints.Length - 1; ++i)
        //        {
        //            if (clr.LineHitTest(projection.ProjectUnscaled(basePoints[i].p3d), projection.ProjectUnscaled(basePoints[i + 1].p3d)))
        //                return true;
        //        }
        //        return false;
        //    }
        //}
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
            forwardOriented = other.forwardOriented;
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
        private int SegmentOfParameter(double par)
        {
            int ind = (int)Math.Floor(par * (basePoints.Length - 1));
            if (ind >= basePoints.Length - 1) ind = basePoints.Length - 2; // es muss immer ind und ind+1 gültig sein
            if (ind < 0) ind = 0;
            return ind;
        }
        private void ApproximatePosition(double position, out GeoPoint2D uv1, out GeoPoint2D uv2, out GeoPoint p)
        {
            ApproximatePosition(position, out uv1, out uv2, out p, false);
        }
        private void ApproximatePosition(double position, out GeoPoint2D uv1, out GeoPoint2D uv2, out GeoPoint p, bool refineBasePoints)
        {
            lock (hashedPositions)
            {
                // Zuerst nachsehen, ob der Punkt schon bekannt ist
                SurfacePoint found;
                // IGeoObject dbg = this.DebugBasePoints;
                if (hashedPositions.TryGetValue(position, out found))
                {
                    p = found.p3d;
                    uv1 = found.psurface1;
                    uv2 = found.psurface2;
                    return;
                }
                int ind = (int)Math.Floor(position * (basePoints.Length - 1));
                // die mit basepoint übereinstimmenden Punkte nicht ins dictionary nehmen
                if (ind > basePoints.Length - 1)
                {
                    uv1 = basePoints[basePoints.Length - 1].psurface1;
                    uv2 = basePoints[basePoints.Length - 1].psurface2;
                    p = basePoints[basePoints.Length - 1].p3d;
                    return;
                }
                if (ind < 0)
                {
                    uv1 = basePoints[0].psurface1;
                    uv2 = basePoints[0].psurface2;
                    p = basePoints[0].p3d;
                    return;
                }
                if (position * (basePoints.Length - 1) - ind == 0.0 && !refineBasePoints)
                {   // Index genau getroffen
                    uv1 = basePoints[ind].psurface1;
                    uv2 = basePoints[ind].psurface2;
                    p = basePoints[ind].p3d;
                    return;
                }
                // Eine Ebene senkrecht zur Verbindung der beiden Basispunkte. Auf dieser und den beiden Flächen
                // muss der Schnittpunkt liegen.
                double d = position * (basePoints.Length - 1) - ind;
                GeoPoint location;
                if (d > 0.0 && ind < basePoints.Length - 1) location = basePoints[ind].p3d + d * (basePoints[ind + 1].p3d - basePoints[ind].p3d);
                else location = basePoints[ind].p3d;
                if (ind == basePoints.Length - 1)
                {
                    --ind; // wenns genau um den letzten Punkt geht, dann das vorletzte Intervall nehmen
                    d = 1.0;
                }

                GeoVector normal = basePoints[ind + 1].p3d - basePoints[ind].p3d;
                Plane pln = new Plane(location, normal);
                // diese Ebene bleibt fix, es wird jetzt auf den beiden surfaces tangential fortgeschritten, bis
                // ein Schnittpunkt gefunden ist
                // Hier Sonderfälle abhandeln, nämlich eine der beiden Flächen ist eine Ebene, dann nur Schnitte mit Linie berechnen
                // oder vielleicht auch, wenn es eine einfache Schnittkurve gibt
                PlaneSurface pls = null;
                ISurface other = null;
                if (surface1 is PlaneSurface)
                {
                    pls = surface1 as PlaneSurface;
                    other = surface2;
                }
                else if (surface2 is PlaneSurface)
                {
                    pls = surface2 as PlaneSurface;
                    other = surface1;
                }
                //if (pls != null) // noch untersuchen, warum das am Ende manchmal fehlschlägt
                //Unreachable code
                /*if (false)
                {
                    GeoPoint loc;
                    GeoVector dir;
                    if (pln.Intersect(pls.Plane, out loc, out dir))
                    {
                        GeoPoint2D[] ips = other.GetLineIntersection(loc, dir);
                        if (ips.Length > 0)
                        {
                            GeoPoint pfound = other.PointAt(ips[0]);
                            if (ips.Length > 1)
                            {
                                for (int i = 1; i < ips.Length; ++i)
                                {   // bestes Ergebnis suchen
                                    // Das Problem: bei einem großen aber dünnen Torus (oder torusähnlichen NURBS)
                                    // liegen zwei Schnittpunkte eng beieinander. Mit einfachem Abstand von der 3d Linie
                                    // wird hier u.U. der falsche gefunden. In diesem Fall ist es besser nach dem UV-System
                                    // der anderen Fläche zu sortieren.
                                    // Aber wie weiß man, welches Kriterium das richtige ist?
                                    // Der Normalenvektor auf der anderen Fläche sollte so ähnlich sein wie bei den
                                    // Nachbar-Basispunkten
                                    GeoPoint pi = other.PointAt(ips[i]);
                                    if ((pi | pfound) < (basePoints[ind].p3d | basePoints[ind + 1].p3d))
                                    {   // die beiden zu untersuchenden Punkte sind näher beieinander als die beiden
                                        // Basispunkte. Das ist ein schlechter Fall, z.B. der dünne Torus
                                        // wie steht es mit dem uv System
                                        GeoPoint2D uvstart, uvend;
                                        if (other == surface1)
                                        {
                                            uvstart = basePoints[ind].psurface1;
                                            uvend = basePoints[ind + 1].psurface1;
                                        }
                                        else
                                        {
                                            uvstart = basePoints[ind].psurface2;
                                            uvend = basePoints[ind + 1].psurface2;
                                        }
                                        if ((ips[i] | ips[0]) < (uvstart | uvend))
                                        {   // auch im uv System liegen die beiden Punkte zu eng beieinander
                                            GeoVector middlenormal = other.GetNormal(uvstart) + other.GetNormal(uvend);
                                            if (other.GetNormal(ips[i]) * middlenormal > other.GetNormal(ips[0]) * middlenormal)
                                            {   // noch nicht getestet, verschiedene Richtung sollte <0 liefern, oder?
                                                pfound = pi;
                                                ips[0] = ips[i];
                                            }
                                        }
                                        else
                                        {   // im uv-System ist es eindeutig
                                            if (Math.Abs(Geometry.DistPL(ips[i], uvstart, uvend)) < Math.Abs(Geometry.DistPL(ips[0], uvstart, uvend)))
                                            {
                                                // müsste man hier auch nocht testen ob die Punkte zwischen uvstart und uvend liegen?
                                                pfound = pi;
                                                ips[0] = ips[i];
                                            }
                                        }
                                    }
                                    else
                                    {
                                        if (Geometry.DistPL(pi, basePoints[ind].p3d, basePoints[ind + 1].p3d) < Geometry.DistPL(pfound, basePoints[ind].p3d, basePoints[ind + 1].p3d))
                                        {
                                            pfound = pi;
                                            ips[0] = ips[i];
                                        }
                                    }
                                }
                            }
                            if (other == surface1)
                            {
                                uv1 = ips[0];
                                uv2 = pls.PositionOf(pfound);
                                p = pfound;
                            }
                            else
                            {
                                uv2 = ips[0];
                                uv1 = pls.PositionOf(pfound);
                                p = pfound;
                            }
#if DEBUG
                            {
                                double dbgdist = basePoints[ind + 1].p3d | basePoints[ind].p3d;
                                if ((p | basePoints[ind + 1].p3d) > dbgdist || (p | basePoints[ind].p3d) > dbgdist)
                                {   // ein Punkte ist aus dem Ruder gelaufen: der Abstand des neuen Punktes zu einem seiner beiden 
                                    // basePoint Nachbarn sollte nie größer sein als der Abstand der beiden basePoints
                                }
                            }
                            if (hashedPositions.Count > 10000)
                            {

                            }
#endif
                            double ddist = basePoints[ind + 1].p3d | basePoints[ind].p3d;
                            if ((p | basePoints[ind + 1].p3d) > ddist || (p | basePoints[ind].p3d) > ddist)
                            {   // ein Punkte ist aus dem Ruder gelaufen: der Abstand des neuen Punktes zu einem seiner beiden 
                                // basePoint Nachbarn sollte nie größer sein als der Abstand der beiden basePoints
                            }
                            else
                            {
                                SurfacePoint sp0 = new SurfacePoint(p, uv1, uv2);
                                AdjustPeriodic(ref sp0, ind);
                                uv1 = sp0.psurface1;
                                uv2 = sp0.psurface2;
                                hashedPositions[position] = sp0;
                                return;
                            }
                        }
                    }
                }
                */

                if (surface1 is ISurfacePlaneIntersection && surface2 is ISurfacePlaneIntersection)
                {   // Schnittpunkt der beiden einfachen Kurven, die die Ebene mit den surfaces schneidet
                    // geht schneller als die Iteration
                    if (approxPolynom == null) InitApproxPolynom();
                    GeoPoint ppPolynom = GeoPoint.Invalid;
                    if (approxPolynom != null)
                    {
                        GeoPoint pp = ppPolynom = approxPolynom.PointAt(position);
                        pln = new Plane(pp, approxPolynom.DirectionAt(position));
                    }

                    // die folgende extentbestimmung könnte man rausnehmen
                    BoundingRect ext1 = BoundingRect.EmptyBoundingRect;
                    BoundingRect ext2 = BoundingRect.EmptyBoundingRect;
                    for (int i = 0; i < basePoints.Length; i++)
                    {
                        ext1.MinMax(basePoints[i].psurface1);
                        ext2.MinMax(basePoints[i].psurface2);
                    }
                    ICurve2D[] c2d1 = (surface1 as ISurfacePlaneIntersection).GetPlaneIntersection(pln, ext1.Left, ext1.Right, ext1.Bottom, ext1.Top);
                    ICurve2D[] c2d2 = (surface2 as ISurfacePlaneIntersection).GetPlaneIntersection(pln, ext2.Left, ext2.Right, ext2.Bottom, ext2.Top);
                    double md = double.MaxValue;
                    GeoPoint pfound = GeoPoint.Origin;
                    for (int i = 0; i < c2d1.Length; i++)
                    {
                        for (int j = 0; j < c2d2.Length; j++)
                        {
                            //TempTriangulatedCurve2D tt1 = new TempTriangulatedCurve2D(c2d1[i]);
                            //TempTriangulatedCurve2D tt2 = new TempTriangulatedCurve2D(c2d2[j]);
                            //GeoPoint2DWithParameter[] ips = tt1.Intersect(tt2);
                            GeoPoint2DWithParameter[] ips = c2d1[i].Intersect(c2d2[j]);
#if DEBUG
                            DebuggerContainer dc = new DebuggerContainer();
                            dc.Add(c2d1[i], System.Drawing.Color.Red, i);
                            dc.Add(c2d2[j], System.Drawing.Color.Blue, j);
#endif
                            for (int k = 0; k < ips.Length; k++)
                            {
#if DEBUG
                                dc.Add(ips[k].p, System.Drawing.Color.Black, k);
#endif
                                GeoPoint pp = pln.ToGlobal(ips[k].p);
                                double dd = Geometry.DistPL(pp, basePoints[ind].p3d, basePoints[ind + 1].p3d);
                                if (dd < md)
                                {
                                    md = dd;
                                    pfound = pp;
                                }
                            }
                        }
                    }
                    if (md < double.MaxValue && !ppPolynom.IsValid || (ppPolynom | pfound) < (basePoints[ind].p3d | basePoints[ind + 1].p3d))
                    {
                        uv1 = surface1.PositionOf(pfound);
                        uv2 = surface2.PositionOf(pfound);
                        SurfacePoint sp0 = new SurfacePoint(pfound, uv1, uv2);
                        AdjustPeriodic(ref sp0, ind);
                        uv1 = sp0.psurface1;
                        uv2 = sp0.psurface2;
                        hashedPositions[position] = sp0;
                        p = pfound;
                        return;

                    }
                }
                uv1 = basePoints[ind].psurface1 + d * (basePoints[ind + 1].psurface1 - basePoints[ind].psurface1);
                uv2 = basePoints[ind].psurface2 + d * (basePoints[ind + 1].psurface2 - basePoints[ind].psurface2);
                if (approxPolynom == null) InitApproxPolynom();
                if (approxPolynom != null && basePoints.Length > 2)
                {
                    GeoPoint pp = approxPolynom.PointAt(position);
                    uv1 = surface1.PositionOf(pp);
                    AdjustPeriodic(ref uv1, true, ind);
                    uv2 = surface2.PositionOf(pp);
                    AdjustPeriodic(ref uv2, false, ind);
                    pln = new Plane(pp, approxPolynom.DirectionAt(position));
                }
                // pln was calculated as a plane perpendicular to the chord between basePoints[ind] and basePoints[ind + 1] at the offset provided by the parameter
                // there was an attempt to use a better plane by using the approximation polygon defined by the two basepoints and the directions at these points,
                // there were some problems with this approach, now it seems to work
                GeoPoint p1, p2;
                GeoVector du1, dv1, du2, dv2, n1, n2;
                //surface1.DerivationAt(uv1, out p1, out du1, out dv1);
                //surface2.DerivationAt(uv2, out p2, out du2, out dv2);
                n1 = surface1.GetNormal(uv1); // there is a problem with singularities (cone, sphere), but it seems GetNormal is working
                n2 = surface2.GetNormal(uv2);
                n1 = n1.Normalized;
                n2 = n2.Normalized;
                double sn1n2 = Math.Abs(Math.Sin((new SweepAngle(n1, n2)).Radian)); // bei gleicher Richtung, also tangential kleiner wert, bei senkrecht 1
                p1 = surface1.PointAt(uv1);
                p2 = surface2.PointAt(uv2);
                double mindist = double.MaxValue;
                d = (p1 | p2);
                bool didntConvert = false;
                int conversionCounter = 0;
                // Ein großes Problem machen die tangentialen Flächen: dort werden nach dem folgenden Verfahren
                // keine guten Schnittpunkte gefunden. Im tangentialen Fall müsste man mit PositionOf und PointAt
                // und den jeweiligen Mittelpunkten arbeiten. Das konvergiert zwar langsamer, aber es sollte wenigsten konvergieren...
                double prec = sn1n2 * Precision.eps;
                while (d > prec && d < mindist) // zu unsichere Bedingung, vor allem wenns tangential wird...
                {   // bricht auch ab, wenns nicht mehr konvergiert
                    // dann könnte man anderes Verfahren nehmen: die Kurven auf der Ebene pln bstimmen und
                    // die beiden Kurven schneiden
                    mindist = d;
                    // nach dem Tangentenverfahren, da wir ja schon ganz nah sind, oder?
                    // p1,du1,dv1 und p2,du2,dv2 sind die beiden Tangentialebenen, p3,du3,dv3 die senkerechte Ebene
                    // daraus ergeben sich 6 Gleichungen mit 6 unbekannten
                    // p1+u1*du1+v1*dv1 = p3+u3*du3+v3*dv3
                    // p2+u2*du2+v2*dv2 = p3+u3*du3+v3*dv3
                    // umgeformt: (u3 und v3 interessieren nicht, deshalb Vorzeichen egal
                    // u1*du1 + v1*dv1 + 0      + 0      - u3*du3 - v3*dv3 = p3-p1
                    // 0      + 0      + u2*du2 + v2*dv2 - u3*du3 - v3*dv3 = p3-p1
                    du1 = surface1.UDirection(uv1);
                    dv1 = surface1.VDirection(uv1);
                    du2 = surface2.UDirection(uv2);
                    dv2 = surface2.VDirection(uv2);
                    GeoVector du3 = pln.DirectionX;
                    GeoVector dv3 = pln.DirectionY;
#if DEBUG
                    bool doit = false;
                    if (doit)
                    {
                        PlaneSurface pls1 = new PlaneSurface(new Plane(p1, du1, dv1));
                        SimpleShape ss1 = new SimpleShape(new BoundingRect(-1, -1, 1, 1));
                        Face fc1 = Face.MakeFace(pls1, ss1);
                        PlaneSurface pls2 = new PlaneSurface(new Plane(p2, du2, dv2));
                        SimpleShape ss2 = new SimpleShape(new BoundingRect(-1, -1, 1, 1));
                        Face fc2 = Face.MakeFace(pls2, ss2);
                        PlaneSurface pls3 = new PlaneSurface(new Plane(location, du3, dv3));
                        SimpleShape ss3 = new SimpleShape(new BoundingRect(-1, -1, 1, 1));
                        Face fc3 = Face.MakeFace(pls3, ss3);
                        DebuggerContainer dc = new DebuggerContainer();
                        fc1.ColorDef = new CADability.Attribute.ColorDef("clr1", System.Drawing.Color.Red);
                        fc2.ColorDef = new CADability.Attribute.ColorDef("clr2", System.Drawing.Color.Green);
                        fc3.ColorDef = new CADability.Attribute.ColorDef("clr3", System.Drawing.Color.Violet);
                        dc.Add(fc1);
                        dc.Add(fc2);
                        dc.Add(fc3);
                        Line dbgl = Line.Construct();
                        dbgl.SetTwoPoints(p1, p2);
                        dc.Add(dbgl);
                        dbgl = Line.Construct();
                        dbgl.SetTwoPoints(basePoints[ind].p3d, basePoints[ind + 1].p3d);
                        dc.Add(dbgl);
                    }
#endif
                    Matrix m = DenseMatrix.OfArray(new double[,]
                    {
                        {du1.x,dv1.x,0,0,du3.x,dv3.x},
                        {du1.y,dv1.y,0,0,du3.y,dv3.y},
                        {du1.z,dv1.z,0,0,du3.z,dv3.z},
                        {0,0,du2.x,dv2.x,du3.x,dv3.x},
                        {0,0,du2.y,dv2.y,du3.y,dv3.y},
                        {0,0,du2.z,dv2.z,du3.z,dv3.z},
                    });
                    Matrix s = (Matrix)m.Solve(DenseMatrix.OfArray(new double[,] { { location.x - p1.x }, { location.y - p1.y }, { location.z - p1.z } ,
                        { location.x - p2.x }, { location.y - p2.y }, { location.z - p2.z } }));
                    if (s.IsValid())
                    {
                        GeoPoint2D uv1alt = uv1;
                        GeoPoint2D uv2alt = uv2;
                        GeoPoint p1alt = p1;
                        GeoPoint p2alt = p2;
                        uv1.x += s[0, 0];
                        uv1.y += s[1, 0];
                        uv2.x += s[2, 0];
                        uv2.y += s[3, 0];
                        p1 = surface1.PointAt(uv1);
                        p2 = surface2.PointAt(uv2);
                        double d1 = uv1 | uv1alt;
                        double d2 = uv2 | uv2alt;
                        double d3 = p1 | p1alt;
                        double d4 = p2 | p2alt;
                        d = p1 | p2;

                        if (d > mindist || double.IsNaN(d))
                        {   // es ist schlechter geworden
                            conversionCounter += 1;
                            if (conversionCounter < 5 && !double.IsNaN(d))
                            {
                                mindist = Geometry.NextDouble(d);
                            }
                            else
                            {
                                uv1 = uv1alt;
                                uv2 = uv2alt;
                                p1 = p1alt;
                                p2 = p2alt;
                                didntConvert = true;
                            }
                        }
                    }
                    else didntConvert = true;
                }
                p = new GeoPoint(p1, p2);
                if (didntConvert)
                {   // möglicherweise tangentiale Berührung der beiden Flächen, versuchen mit pointAt, position of zu konvergieren
                    if (approxPolynom != null && basePoints.Length > 2)
                    {
                        GeoPoint pp = approxPolynom.PointAt(position);
                        uv1 = surface1.PositionOf(pp);
                        AdjustPeriodic(ref uv1, true, ind);
                        uv2 = surface2.PositionOf(pp);
                        AdjustPeriodic(ref uv2, false, ind);
                        p1 = surface1.PointAt(uv1);
                        p2 = surface2.PointAt(uv2);
                        p = new GeoPoint(p1, p2);
                    }

                    d = p1 | p2;
                    double basedist = basePoints[ind + 1].p3d | basePoints[ind].p3d;
                    while (d > Precision.eps)
                    {
                        GeoPoint cnt = new GeoPoint(p1, p2);
                        GeoPoint2D uv1t = surface1.PositionOf(cnt);
                        GeoPoint2D uv2t = surface2.PositionOf(cnt);
                        GeoPoint p1t = surface1.PointAt(uv1t);
                        GeoPoint p2t = surface2.PointAt(uv2t);
                        double dt = p1t | p2t;
                        if (dt < d)
                        {   // ist allemal besser
                            p1 = p1t;
                            p2 = p2t;
                            uv1 = uv1t;
                            uv2 = uv2t;
                        }
                        if (dt > d * 0.75)
                        {   // konvergiert nicht gescheit
                            break;
                        }
                        d = dt;
                        p = new GeoPoint(p1, p2);
                        if ((p | basePoints[ind + 1].p3d) > basedist || (p | basePoints[ind].p3d) > basedist)
                        {   // ein Punkte ist aus dem Ruder gelaufen: der Abstand des neuen Punktes zu einem seiner beiden 
                            // basePoint Nachbarn sollte nie größer sein als der Abstand der beiden basePoints
                            // hier einfach lineare Interpolation
                            d = position * (basePoints.Length - 1) - ind;
                            uv1 = basePoints[ind].psurface1 + d * (basePoints[ind + 1].psurface1 - basePoints[ind].psurface1);
                            uv2 = basePoints[ind].psurface2 + d * (basePoints[ind + 1].psurface2 - basePoints[ind].psurface2);
                            break;
                        }
                    }
                }
                CheckPeriodic(ref uv1, true, ind);
                CheckPeriodic(ref uv2, false, ind);
                // System.Diagnostics.Trace.WriteLine("Parameter, Punkt: " + position.ToString() + ", " + p.ToString());
                SurfacePoint sp = new SurfacePoint(p, uv1, uv2);
                AdjustPeriodic(ref sp, ind);
                uv1 = sp.psurface1;
                uv2 = sp.psurface2;
                hashedPositions[position] = sp;
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
        private void CheckPeriodic(ref GeoPoint2D uv, bool onSurface1, int index)
        {
            // es wurde ein uv Wert auf einer Oberfläche gefunden
            // wenn diese aber periodisch ist, dann sollte er in der Nähe
            // der basepoint[ind] bzw. basepoint[ind+1] liegen
            GeoPoint2D uv1, uv2;
            double uperiod = 0.0;
            double vperiod = 0.0;
            ISurface surface;
            if (onSurface1)
            {
                surface = surface1;
                uv1 = basePoints[index].psurface1;
                uv2 = basePoints[index + 1].psurface1;
            }
            else
            {
                surface = surface2;
                uv1 = basePoints[index].psurface2;
                uv2 = basePoints[index + 1].psurface2;
            }
            if (surface.IsUPeriodic) uperiod = surface.UPeriod;
            if (surface.IsVPeriodic) vperiod = surface.UPeriod;
            if (uperiod > 0.0)
            {
                double d0 = Math.Abs(uv.x - (uv1.x + uv2.x) / 2);
                double d1 = Math.Abs(uv.x + uperiod - (uv1.x + uv2.x) / 2);
                double d2 = Math.Abs(uv.x - uperiod - (uv1.x + uv2.x) / 2);
                if (d1 < d0) uv.x += uperiod;
                if (d2 < d0) uv.x -= uperiod;
                // ansonsten bleibt er ja unverändert
            }
            if (vperiod > 0.0)
            {
                double d0 = Math.Abs(uv.y - (uv1.y + uv2.y) / 2);
                double d1 = Math.Abs(uv.y + vperiod - (uv1.y + uv2.y) / 2);
                double d2 = Math.Abs(uv.y - vperiod - (uv1.y + uv2.y) / 2);
                if (d1 < d0) uv.y += vperiod;
                if (d2 < d0) uv.y -= vperiod;
            }
        }
        public override GeoVector StartDirection
        {
            get
            {
                GeoVector v = surface1.GetNormal(basePoints[0].psurface1) ^ surface2.GetNormal(basePoints[0].psurface2);
                if (!forwardOriented) v.Reverse();
                return v;
            }
        }
        public override GeoVector EndDirection
        {
            get
            {
                GeoVector v = surface1.GetNormal(basePoints[basePoints.Length - 1].psurface1) ^ surface2.GetNormal(basePoints[basePoints.Length - 1].psurface2);
                if (!forwardOriented) v.Reverse();
                return v;
            }
        }
        public override GeoVector DirectionAt(double Position)
        {
            GeoPoint2D uv1, uv2;
            GeoPoint p;
            ApproximatePosition(Position, out uv1, out uv2, out p);
            GeoVector v = surface1.GetNormal(uv1) ^ surface2.GetNormal(uv2);
            if (!forwardOriented) v.Reverse();
            int ind = SegmentOfParameter(Position);
            if (approxPolynom == null) InitApproxPolynom();
            GeoVector v1 = approxPolynom.DirectionAt(Position);
            if (v.Length > 1e-4)
            {
                v.Length = v1.Length;
                return v;
            }
            else return v1; // the surfaces are tangential so we use the approximating polynom
            //v.Length = v1.Length;
            //double d = basePoints[ind].p3d | basePoints[ind + 1].p3d; // Abstand der umgebenden Basispunkte
            //double span = 1.0 / (basePoints.Length - 1); // Parameterbereich zwischen zwei Basispunkten
            //if (v.Length > 1e-4) // nicht tangentiale Flächen
            //{
            //    return v;
            //}
            //else
            //{
            //    return basePoints[ind + 1].p3d - basePoints[ind].p3d;
            //}
            //// die Länge muss der Änderung im Segment entsprechen, das ist wichtig für die Newtonverfahren
        }
        public override GeoPoint PointAt(double Position)
        {
            GeoPoint2D uv1, uv2;
            GeoPoint p;
#if DEBUG
            // double oldLength = hashedPositionsLength();
#endif
            ApproximatePosition(Position, out uv1, out uv2, out p);
#if DEBUG
            //if (oldLength > 0.0 && hashedPositionsLength() / oldLength > 2)
            //{   // something invalid happened

            //}
#endif
            return p;
        }
#if DEBUG
        double hashedPositionsLength()
        {   // the length of the curve according to the util now calculated points
            SortedDictionary<double, GeoPoint> sortedPoints = new SortedDictionary<double, GeoPoint>();
            for (int i = 0; i < basePoints.Length; i++)
            {
                sortedPoints[(double)(i) / (double)(basePoints.Length - 1)] = basePoints[i].p3d;
                lock (hashedPositions)
                {
                    foreach (KeyValuePair<double, SurfacePoint> item in hashedPositions)
                    {
                        sortedPoints[item.Key] = item.Value.p3d;
                    }
                }
            }
            GeoPoint lastPoint = GeoPoint.Invalid;
            double res = 0.0;
            foreach (GeoPoint item in sortedPoints.Values)
            {
                if (lastPoint.IsValid) res += item | lastPoint;
                lastPoint = item;
            }
            return res;
        }
#endif
        public override double PositionAtLength(double position)
        {
            throw new Exception("The method or operation is not implemented.");
        }
        public override double PositionOf(GeoPoint p)
        {
            double ppos = TetraederHull.PositionOf(p);
            if (approxPolynom == null) InitApproxPolynom();
            double pos1 = approxPolynom.PositionOf(p, out double md);

            if ((pos1 != double.MaxValue) && (PointAt(pos1) | p) < (PointAt(ppos) | p))
                return pos1;

            return ppos;

            //Unreachable code
            /* if (Math.Abs(pos1 - ppos) > 0.1 && md < Precision.eps)
            { }
            return ppos;
            double res = -1.0;
            double maxdist = double.MaxValue;
            if (approxPolynom != null)
            {
                double pos = approxPolynom.PositionOf(p, out maxdist);
                return pos;
            }
            for (int i = 0; i < basePoints.Length - 1; ++i)
            {
                GeoPoint dp = Geometry.DropPL(p, basePoints[i].p3d, basePoints[i + 1].p3d);
                double pos = Geometry.LinePar(basePoints[i].p3d, basePoints[i + 1].p3d - basePoints[i].p3d, dp);
                bool valid = (pos >= 0.0 && pos <= 1.0);
                if (valid)
                {
                    double d = Geometry.DistPL(p, basePoints[i].p3d, basePoints[i + 1].p3d);
                    if (d <= maxdist)
                    {
                        maxdist = d;
                        res = i + pos;
                    }
                }
            }
            // if (res < 0.0) Wenn der punkt fast genau ein basepoint ist
            // kann er trotzdem oben verworfen werden, deshalb hier noch der test auf alle Basepoints
            // und Punkte, die nahe einem Basepoint liegen, aber im ungültigen Winkelbereich
            // werden mit womöglich ganz anderen Linien in Verbindung gebracht
            {   // keine passende Strecke gefunden
                for (int i = 0; i < basePoints.Length; i++)
                {
                    double d = p | basePoints[i].p3d;
                    if (d < maxdist)
                    {
                        maxdist = d;
                        res = i;
                    }
                }
            }
            return res / (basePoints.Length - 1);*/
        }
        public override double PositionOf(GeoPoint p, double prefer)
        {
            throw new Exception("The method or operation is not implemented.");
        }
        public override double PositionOf(GeoPoint p, Plane pl)
        {
            throw new Exception("The method or operation is not implemented.");
        }
        public override double Length
        {
            get
            {   // nur eine grobe Annäherung hier, die nie 0 sein sollte.
                // man müsste irgendwie extrapolieren
                double d = 0.0;
                for (int i = 0; i < basePoints.Length - 1; ++i)
                {
                    d += basePoints[i].p3d | basePoints[i + 1].p3d;
                }
                return d;
            }
        }
        public override ICurve[] Split(double Position)
        {
            List<SurfacePoint> l1 = new List<SurfacePoint>();
            List<SurfacePoint> l2 = new List<SurfacePoint>();
            int ind = (int)Math.Floor(Position * (basePoints.Length - 1));
            if (Position * (basePoints.Length - 1) - ind == 0.0)
            {   // die Split-Position liegt genau auf einem basepoint
                for (int i = 0; i < basePoints.Length; i++)
                {
                    if (i <= ind) l1.Add(basePoints[i]);
                    if (i >= ind) l2.Add(basePoints[i]);
                }
            }
            else
            {   // es wird ein Zwischenpunkt eingefügt
                GeoPoint2D uv1, uv2;
                GeoPoint p;
                ApproximatePosition(Position, out uv1, out uv2, out p);
                SurfacePoint p1 = new SurfacePoint(p, uv1, uv2);
                l2.Add(p1); // die 2. Liste fängt mit dem neuen Punkt an
                for (int i = 0; i < basePoints.Length; i++)
                {
                    if (i <= ind) l1.Add(basePoints[i]);
                    else l2.Add(basePoints[i]);
                }
                l1.Add(p1); // die 1. Liste hört mit dem neuen Punkt auf
            }
            for (int i = l1.Count - 1; i > 0; --i)
            {
                if ((l1[i].p3d | l1[i - 1].p3d) == 0.0) l1.RemoveAt(i);
            }
            for (int i = l2.Count - 1; i > 0; --i)
            {
                if ((l2[i].p3d | l2[i - 1].p3d) == 0.0) l2.RemoveAt(i);
            }
            InterpolatedDualSurfaceCurve dsc1 = new InterpolatedDualSurfaceCurve(surface1.Clone(), surface2.Clone(), l1.ToArray());
            InterpolatedDualSurfaceCurve dsc2 = new InterpolatedDualSurfaceCurve(surface1.Clone(), surface2.Clone(), l2.ToArray());
            return new ICurve[] { dsc1, dsc2 };
        }

        internal void RecalcSurfacePoints(BoundingRect bounds1, BoundingRect bounds2)
        {
            for (int i = 0; i < basePoints.Length; i++)
            {
                basePoints[i].psurface1 = surface1.PositionOf(basePoints[i].p3d);
                SurfaceHelper.AdjustPeriodic(surface1, bounds1, ref basePoints[i].psurface1);
                basePoints[i].psurface2 = surface2.PositionOf(basePoints[i].p3d);
                SurfaceHelper.AdjustPeriodic(surface2, bounds2, ref basePoints[i].psurface2);
            }
            InvalidateSecondaryData();
            int n = basePoints.Length / 2; // es müssen mindesten 3 sein
            GeoVector v = surface1.GetNormal(basePoints[n].psurface1) ^ surface2.GetNormal(basePoints[n].psurface2);
            GeoVector v0 = basePoints[n + 1].p3d - basePoints[n - 1].p3d;
            Angle a = new Angle(v, v0);
            forwardOriented = (a.Radian < Math.PI / 2.0);
            CheckPeriodic();
#if DEBUG
            CheckSurfaceParameters();
#endif
        }

        public override ICurve[] Split(double Position1, double Position2)
        {
            GeoPoint2D uv1, uv2;
            GeoPoint p;
            ApproximatePosition(Position1, out uv1, out uv2, out p);
            SurfacePoint p1 = new SurfacePoint(p, uv1, uv2);
            ApproximatePosition(Position2, out uv1, out uv2, out p);
            SurfacePoint p2 = new SurfacePoint(p, uv1, uv2);
            List<SurfacePoint> l1 = new List<SurfacePoint>();
            List<SurfacePoint> l2 = new List<SurfacePoint>();
            int i1, i2;
            if (Position1 < Position2)
            {
                i1 = (int)Math.Ceiling(Position1 * (basePoints.Length - 1));
                i2 = (int)Math.Ceiling(Position2 * (basePoints.Length - 1));
            }
            else
            {
                i2 = (int)Math.Ceiling(Position1 * (basePoints.Length - 1));
                i1 = (int)Math.Ceiling(Position2 * (basePoints.Length - 1));
            }
            for (int i = i1; i < i2; ++i)
            {
                if (!Precision.IsEqual(basePoints[i].p3d, p1.p3d) && !Precision.IsEqual(basePoints[i].p3d, p2.p3d)) l1.Add(basePoints[i]);
            }
            for (int i = i2; i < basePoints.Length; ++i)
            {
                if (!Precision.IsEqual(basePoints[i].p3d, p1.p3d) && !Precision.IsEqual(basePoints[i].p3d, p2.p3d)) l2.Add(basePoints[i]);
            }
            for (int i = 1; i < i1; ++i) // hier ist ja geschlossen, also ist der erste und der letzte Basepoint identisch
            {
                if (!Precision.IsEqual(basePoints[i].p3d, p1.p3d) && !Precision.IsEqual(basePoints[i].p3d, p2.p3d)) l2.Add(basePoints[i]);
            }
            if (Position1 < Position2)
            {
                l1.Insert(0, p1);
                l1.Add(p2);
                l2.Insert(0, p2);
                l2.Add(p1);
            }
            else
            {
                l1.Insert(0, p2);
                l1.Add(p1);
                l2.Insert(0, p1);
                l2.Add(p2);
            }
            InterpolatedDualSurfaceCurve dsc1 = new InterpolatedDualSurfaceCurve(surface1.Clone(), surface2.Clone(), l1.ToArray());
            InterpolatedDualSurfaceCurve dsc2 = new InterpolatedDualSurfaceCurve(surface1.Clone(), surface2.Clone(), l2.ToArray());
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
            Array.Reverse(basePoints);
            forwardOriented = !forwardOriented;
            InvalidateSecondaryData();
        }
        public override void Trim(double StartPos, double EndPos)
        {
            // if (StartPos <= 0 && EndPos >= 1) return; ; // nichts zu tun
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
                    // keine fast identischen Punkte zufügen, die führen zu Nullvektoren in der Differenz
                    if (pos > StartPos + 1e-3) spl.Add(basePoints[i]);
                }
                for (int i = 1; i < basePoints.Length; ++i)
                {
                    double pos = (double)i / (double)(basePoints.Length - 1);
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
            InvalidateSecondaryData();
        }
        public override IGeoObject Clone()
        {
            // BRepIntersection createNewEdges erwartet, dass die surfaces erhalten bleiben, also nicht gecloned werden
            // wenn das von anderer Seite anders erwartet wird, dann muss eine methode zum Austausch der Surfaces gemacht werden
#if DEBUG
            CheckSurfaceParameters();
#endif
            SurfacePoint[] spnts = new SurfacePoint[basePoints.Length];
            for (int i = 0; i < basePoints.Length; i++)
            {   // we need a deep copy, independant surface points
                spnts[i] = new SurfacePoint(basePoints[i].p3d, basePoints[i].psurface1, basePoints[i].psurface2);
            }
            return new InterpolatedDualSurfaceCurve(surface1.Clone(), surface2.Clone(), spnts, forwardOriented, approxPolynom); // Clone introduced because of independant surfaces for BRep operations
            // return new InterpolatedDualSurfaceCurve(surface1.Clone(), surface2.Clone(), basePoints.Clone() as SurfacePoint[], forwardOriented);
        }
        internal void SetSurfaces(ISurface surface1, ISurface surface2, bool swapped)
        {
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
                        forwardOriented = !forwardOriented; // falls die surfaces getauscht wurden
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
            InterpolatedDualSurfaceCurve ipdsc = new InterpolatedDualSurfaceCurve(surface1.GetModified(m), surface2.GetModified(m), sp, forwardOriented);
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
            throw new Exception("The method or operation is not implemented.");
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
        ICurve2D IDualSurfaceCurve.GetCurveOnSurface(ISurface onThisSurface)
        {
            if (onThisSurface == surface1) return CurveOnSurface1;
            else if (onThisSurface == surface2) return CurveOnSurface2;
            else return null;
        }

        void IDualSurfaceCurve.SwapSurfaces()
        {
            ISurface tmp = surface1;
            surface1 = surface2;
            surface2 = tmp;
            for (int i = 0; i < basePoints.Length; i++)
            {
                GeoPoint2D t = basePoints[i].psurface1;
                basePoints[i].psurface1 = basePoints[i].psurface2;
                basePoints[i].psurface2 = t;
            }
            forwardOriented = !forwardOriented;
            InvalidateSecondaryData();
            //ICurve2D tc = CurveOnSurface1;
            //CurveOnSurface1 ... werden ja jedesmal neu berechnet. oder
        }

        public override ICurve Approximate(bool linesOnly, double maxError)
        {
#if DEBUG
            if (maxError < 0)
            {
                // nur zum Polynomfunktionen Debuggen!
                double[] knots = new double[basePoints.Length];
                for (int i = 0; i < basePoints.Length; i++)
                {
                    knots[i] = (double)i / (basePoints.Length - 1);
                }
                ExplicitPCurve3D dbg = ExplicitPCurve3D.FromCurve(this, knots, 3, true);
                return null;
            }
#endif
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
                // gleiche Richtung
                for (double par = 0.25; par < 1.0; par += 0.25)
                {
                    if ((PointAt(par) | other.PointAt(par)) > precision) return false;
                }
                return true;
            }
            if ((EndPoint | other.StartPoint) < precision && (StartPoint | other.EndPoint) < precision)
            {
                for (double par = 0.25; par < 1.0; par += 0.25)
                {
                    if ((PointAt(par) | other.PointAt(1 - par)) > precision) return false;
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
            hashedPositions = new Dictionary<double, SurfacePoint>();
            try
            {
                forwardOriented = (bool)info.GetValue("ForwardOriented", typeof(bool));
            }
            catch (SerializationException)
            {
                forwardOriented = true; // fehlte früher mit subtilen Folgen
            }
        }

        internal void SurfacesSwapped()
        {
            forwardOriented = !forwardOriented;
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
            info.AddValue("ForwardOriented", forwardOriented);
#if DEBUG
            CheckSurfaceParameters();
#endif
        }
        public override void GetObjectData(IJsonWriteData data)
        {
            base.GetObjectData(data);
            data.AddProperty("Surface1", surface1);
            data.AddProperty("Surface2", surface2);
            data.AddProperty("BasePoints", basePoints);
            data.AddProperty("ForwardOriented", forwardOriented);
        }

        public override void SetObjectData(IJsonReadData data)
        {
            base.SetObjectData(data);
            surface1 = data.GetPropertyOrDefault<ISurface>("Surface1");
            surface2 = data.GetPropertyOrDefault<ISurface>("Surface2");
            basePoints = data.GetPropertyOrDefault<SurfacePoint[]>("BasePoints");
            forwardOriented = (bool)data.GetProperty("ForwardOriented");
            data.RegisterForSerializationDoneCallback(this);
        }
        void IJsonSerializeDone.SerializationDone(JsonSerialize jsonSerialize)
        {
            if (surface1 is ISurfaceImpl simpl1)
            {
                if (simpl1.usedArea.IsEmpty() || simpl1.usedArea.IsInfinite)
                {
                    BoundingRect ext = BoundingRect.EmptyBoundingRect;
                    for (int i = 0; i < basePoints.Length; i++)
                    {
                        ext.MinMax(basePoints[i].psurface1);
                    }
                    simpl1.usedArea = ext;
                }
            }
            if (surface2 is ISurfaceImpl simpl2)
            {
                if (simpl2.usedArea.IsEmpty() || simpl2.usedArea.IsInfinite)
                {
                    BoundingRect ext = BoundingRect.EmptyBoundingRect;
                    for (int i = 0; i < basePoints.Length; i++)
                    {
                        ext.MinMax(basePoints[i].psurface2);
                    }
                    simpl2.usedArea = ext;
                }
            }

        }
        void IDeserializationCallback.OnDeserialization(object sender)
        {
            if (surface1 is ISurfaceImpl simpl1)
            {
                if (simpl1.usedArea.IsEmpty() || simpl1.usedArea.IsInfinite)
                {
                    BoundingRect ext = BoundingRect.EmptyBoundingRect;
                    for (int i = 0; i < basePoints.Length; i++)
                    {
                        ext.MinMax(basePoints[i].psurface1);
                    }
                    simpl1.usedArea = ext;
                }
            }
            if (surface2 is ISurfaceImpl simpl2)
            {
                if (simpl2.usedArea.IsEmpty() || simpl2.usedArea.IsInfinite)
                {
                    BoundingRect ext = BoundingRect.EmptyBoundingRect;
                    for (int i = 0; i < basePoints.Length; i++)
                    {
                        ext.MinMax(basePoints[i].psurface2);
                    }
                    simpl2.usedArea = ext;
                }
            }
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

        #endregion
    }
}
