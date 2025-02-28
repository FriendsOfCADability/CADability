using CADability.Curve2D;
using MathNet.Numerics.LinearAlgebra.Double;
using CADability.UserInterface;
using System;
using System.Collections.Generic;
using System.Runtime.Serialization;
using System.Text;
using System.Threading;
using Wintellect.PowerCollections;
using MathNet.Numerics.LinearAlgebra.Factorization;

namespace CADability.GeoObject
{
    /// <summary>
    /// A NURBS surface implementing <see cref="ISurface"/>. 
    /// </summary>
    [Serializable()]
    public class NurbsSurface : ISurfaceImpl, ISerializable, IDeserializationCallback, IExportStep
    {
        private GeoPoint[,] poles;
        private double[,] weights;
        private double[] uKnots;
        private double[] vKnots;
        private int[] uMults;
        private int[] vMults;
        private int uDegree;
        private int vDegree;
        private bool uPeriodic;
        private bool vPeriodic;
        private int upoles, vpoles;
        private double uMinRestrict = 0.0, uMaxRestrict = 0.0, vMinRestrict = 0.0, vMaxRestrict = 0.0; // restriction for periodic surfaces
        // nur einer von beiden NURBS Helfern ist besetzt
        private Nurbs<GeoPoint, GeoPointPole> nubs;
        private Nurbs<GeoPointH, GeoPointHPole> nurbs;

        private ISurface simpleSurface = null;
        private ModOp2D toSimpleSurface;
        private bool simpleSurfaceChecked = false;
        // Kurven  mit festem u bzw. v, die schon mal berechnet wurden
        private WeakReference fixedUCurves;
        private WeakReference fixedVCurves;
        private WeakReference cubeHull;
        private WeakReference uSingularities, vSingularities;
        private BoxedSurface boxedSurface
        {
            get
            {
                BoundingRect extent = new BoundingRect();
                GetNaturalBounds(out extent.Left, out extent.Right, out extent.Bottom, out extent.Top);
                if (cubeHull == null) cubeHull = new WeakReference(new BoxedSurface(this, extent));
                BoxedSurface ch = null;
                try
                {
                    if (cubeHull.Target != null)
                    {
                        ch = cubeHull.Target as BoxedSurface;
                    }
                }
                catch (InvalidOperationException) { }
                if (ch == null)
                {   // wurde bereits gelöscht
                    ch = new BoxedSurface(this, extent);
                    cubeHull.Target = ch;
                }
#if DEBUG
                BoxedSurfaceEx bex = new BoxedSurfaceEx(this, extent);
#endif
                return ch;
            }
        }
        private void InvalidateSecondaryData()
        {
            fixedUCurves = null;
            fixedVCurves = null;
            cubeHull = null;
            nubs = null;
            nurbs = null;
            simpleSurface = null;
            simpleSurfaceChecked = false;
            boxedSurfaceEx = null;
            uSingularities = null;
            vSingularities = null;
        }

        private bool hasSimpleSurface
        {
            get
            {
                if (!simpleSurfaceChecked)
                {
                    BoundingCube polesext = BoundingCube.EmptyBoundingCube;
                    foreach (GeoPoint point in poles)
                    {
                        polesext.MinMax(point);
                    }
                    double precision = polesext.Size * 1e-6;
                    if (GetSimpleSurface(precision, out simpleSurface, out toSimpleSurface))
                    {
                    }
                    else
                    {
                        simpleSurface = null;
                    }
                    simpleSurfaceChecked = true;
                }
                return simpleSurface != null;
            }
        }

        /// <summary>
        /// Creates a new NURBS surface with the given data.
        /// </summary>
        /// <param name="poles">the poles</param>
        /// <param name="weights">the weight of the poles</param>
        /// <param name="uKnots">the knots in u direction (no duplicate values)</param>
        /// <param name="vKnots">the knots in v direction (no duplicate values)</param>
        /// <param name="uMults">the multiplicities in u direction</param>
        /// <param name="vMults">the multiplicities in v direction</param>
        /// <param name="uDegree">the degree in u direction</param>
        /// <param name="vDegree">the degree in v direction</param>
        /// <param name="uPeriodic">closed in u direction</param>
        /// <param name="vPeriodic">closed in v direction</param>
        public NurbsSurface(GeoPoint[,] poles, double[,] weights, double[] uKnots, double[] vKnots,
            int[] uMults, int[] vMults, int uDegree, int vDegree, bool uPeriodic, bool vPeriodic)
        {
            this.poles = (GeoPoint[,])poles.Clone();
            if (weights != null) this.weights = (double[,])weights.Clone();
            else
            {
                weights = null;
            }
            this.uKnots = (double[])uKnots.Clone();
            this.vKnots = (double[])vKnots.Clone();
            this.uMults = (int[])uMults.Clone();
            this.vMults = (int[])vMults.Clone();
            this.uDegree = uDegree;
            this.vDegree = vDegree;
            this.uPeriodic = uPeriodic;
            this.vPeriodic = vPeriodic;
            InvalidateSecondaryData();
            Init();
        }
        /// <summary>
        /// Creates a new NURBS surface with the given data.
        /// </summary>
        /// <param name="poles">the poles</param>
        /// <param name="weights">the weight of the poles</param>
        /// <param name="uKnots">the knots in u direction (multiple knots may have the same value)</param>
        /// <param name="vKnots">the knots in v direction (multiple knots may have the same value)</param>
        /// <param name="uDegree">the degree in u direction</param>
        /// <param name="vDegree">the degree in v direction</param>
        /// <param name="uPeriodic">closed in u direction</param>
        /// <param name="vPeriodic">closed in v direction</param>
        public NurbsSurface(GeoPoint[,] poles, double[,] weights, double[] uKnots, double[] vKnots,
            int uDegree, int vDegree, bool uPeriodic, bool vPeriodic)
        {
            this.poles = (GeoPoint[,])poles.Clone();
            if (weights != null) this.weights = (double[,])weights.Clone();
            else this.weights = null;
            // flache knotenlisten zusammenschrumpfen
            List<double> uk = new List<double>();
            List<int> um = new List<int>();
            uk.Add(uKnots[0]);
            um.Add(1);
            for (int i = 1; i < uKnots.Length; ++i)
            {
                int lastind = uk.Count - 1;
                if (uKnots[i] == uk[lastind])
                {
                    um[lastind] += 1;
                }
                else
                {
                    uk.Add(uKnots[i]);
                    um.Add(1);
                }
            }
            List<double> vk = new List<double>();
            List<int> vm = new List<int>();
            vk.Add(vKnots[0]);
            vm.Add(1);
            for (int i = 1; i < vKnots.Length; ++i)
            {
                int lastind = vk.Count - 1;
                if (vKnots[i] == vk[lastind])
                {
                    vm[lastind] += 1;
                }
                else
                {
                    vk.Add(vKnots[i]);
                    vm.Add(1);
                }
            }
            this.uKnots = uk.ToArray();
            this.vKnots = vk.ToArray();
            this.uMults = um.ToArray();
            this.vMults = vm.ToArray();
            this.uDegree = uDegree;
            this.vDegree = vDegree;
            this.uPeriodic = uPeriodic;
            this.vPeriodic = vPeriodic;
            InvalidateSecondaryData();
            Init();
        }
        /// <summary>
        /// Creates a new NURBS surface which interpolates the provided points. The surface will contain the provided points exactely
        /// and smoothly interpolated inbetween. The points are organized in a two dimensional array resembling the u and v direction
        /// (first and second index) of the NURBS surface
        /// </summary>
        /// <param name="throughPoints">Points to be interpolated</param>
        /// <param name="maxUDegree">The maximum degree in respect of the first index</param>
        /// <param name="maxVDegree">The maximum degree in respect of the second index</param>
        /// <param name="uPeriodic">Periodicy in respect of the first index</param>
        /// <param name="vPeriodic">Periodicy in respect of the second index</param>
        public NurbsSurface(GeoPoint[,] throughPoints, int maxUDegree, int maxVDegree, bool uPeriodic, bool vPeriodic)
        {   // 1. Index ist u, 2. Index ist V
            int ulength = throughPoints.GetLength(0);
            int vlength = throughPoints.GetLength(1);
            int udegree = Math.Min(maxUDegree, ulength - 1);
            int vdegree = Math.Min(maxVDegree, vlength - 1);
            double[] ku = new double[ulength - 1];
            double[] kv = new double[vlength - 1];
            double[] totuchordlength = new double[vlength];
            for (int i = 0; i < vlength; ++i)
            {
                totuchordlength[i] = 0.0;
                for (int j = 0; j < ulength - 1; ++j)
                {
                    totuchordlength[i] += throughPoints[j, i] | throughPoints[j + 1, i];
                }
                if (totuchordlength[i] == 0) totuchordlength[i] = 1.0; // das ist eine Singularität
            }
            double[] totvchordlength = new double[ulength];
            for (int i = 0; i < ulength; ++i)
            {
                totvchordlength[i] = 0.0;
                for (int j = 0; j < vlength - 1; ++j)
                {
                    totvchordlength[i] += throughPoints[i, j] | throughPoints[i, j + 1];
                }
                if (totvchordlength[i] == 0) totvchordlength[i] = 1.0; // das ist eine Singularität
            }
            for (int i = 0; i < ulength - 1; ++i)
            {
                double s = 0.0;
                for (int j = 0; j < vlength; ++j)
                {
                    s += (throughPoints[i, j] | throughPoints[i + 1, j]) / totuchordlength[j];
                }
                ku[i] = s / vlength;
                if (i > 0) ku[i] += ku[i - 1];
            }
            for (int i = 0; i < vlength - 1; ++i)
            {
                double s = 0.0;
                for (int j = 0; j < ulength; ++j)
                {
                    s += (throughPoints[j, i] | throughPoints[j, i + 1]) / totvchordlength[j];
                }
                kv[i] = s / ulength;
                if (i > 0) kv[i] += kv[i - 1];
            }
            // im Buch S 376: zuerst die Pole R[i,j] erzeugen, indem man in U interpoliert
            // dann durch diese Pole in V-Richtung interpolieren, das liefert die endgültigen Pole.
#if DEBUG
            DebuggerContainer dc = new DebuggerContainer();
#endif
            int qulength = ulength;
            int qvlength = vlength;
            if (uPeriodic) qulength += 2 * (udegree / 2); // hier wird auf gerade abgerunget, Formel entstand durch ausprobieren
            if (vPeriodic) qvlength += 2 * (vdegree / 2);
            GeoPoint[,] Q = new GeoPoint[qulength, qvlength]; // im Falle von periodic stimmt die Größe von Q nicht!!!
            for (int i = 0; i < vlength; ++i)
            {

                GeoPoint[] tpu = new GeoPoint[ulength];
                for (int j = 0; j < ulength; ++j)
                {
                    tpu[j] = throughPoints[j, i];
                }
                // DEBUG:
                //for (int j = 0; j < ku.Length; j++)
                //{
                //    ku[j] = (double)j / (ku.Length - 1);
                //}
                Nurbs<GeoPoint, GeoPointPole> qnurbs = new Nurbs<GeoPoint, GeoPointPole>(udegree, tpu, ku, uPeriodic);
#if DEBUG
                BSpline bsp = BSpline.Construct();
                bsp.FromNurbs(qnurbs, qnurbs.UKnots[0], qnurbs.UKnots[qnurbs.UKnots.Length - 1]);
                dc.Add(bsp);
                GeoPoint dbgs = (bsp as ICurve).PointAt(0.0);
                GeoPoint dbge = (bsp as ICurve).PointAt(1.0);
#endif
                if (i == 0)
                {   // die Ergebnisse sind in allen Durchläufen identisch
                    List<double> uk = new List<double>();
                    List<int> um = new List<int>();
                    uk.Add(qnurbs.UKnots[0]);
                    um.Add(1);
                    for (int j = 1; j < qnurbs.UKnots.Length; ++j)
                    {
                        int lastind = uk.Count - 1;
                        if (qnurbs.UKnots[j] == uk[lastind])
                        {
                            um[lastind] += 1;
                        }
                        else
                        {
                            uk.Add(qnurbs.UKnots[j]);
                            um.Add(1);
                        }
                    }
                    this.uKnots = uk.ToArray();
                    this.uMults = um.ToArray();
                }
                for (int j = 0; j < qulength; ++j)
                {
                    Q[j, i] = qnurbs.Poles[j];
                }
            }
            this.poles = new GeoPoint[qulength, qvlength];
            for (int i = 0; i < qulength; ++i)
            {

                GeoPoint[] tpv = new GeoPoint[qvlength];
                for (int j = 0; j < qvlength; ++j)
                {
                    tpv[j] = Q[i, j];
                }
                Nurbs<GeoPoint, GeoPointPole> qnurbs = new Nurbs<GeoPoint, GeoPointPole>(vdegree, tpv, kv, vPeriodic);
#if DEBUG
                BSpline bsp = BSpline.Construct();
                bsp.FromNurbs(qnurbs, qnurbs.UKnots[0], qnurbs.UKnots[qnurbs.UKnots.Length - 1]);
                dc.Add(bsp);
#endif
                if (i == 0)
                {   // die Ergebnisse sind in allen Durchläufen identisch
                    List<double> vk = new List<double>();
                    List<int> vm = new List<int>();
                    vk.Add(qnurbs.UKnots[0]);
                    vm.Add(1);
                    for (int j = 1; j < qnurbs.UKnots.Length; ++j)
                    {
                        int lastind = vk.Count - 1;
                        if (qnurbs.UKnots[j] == vk[lastind])
                        {
                            vm[lastind] += 1;
                        }
                        else
                        {
                            vk.Add(qnurbs.UKnots[j]);
                            vm.Add(1);
                        }
                    }
                    this.vKnots = vk.ToArray();
                    this.vMults = vm.ToArray();
                }
                for (int j = 0; j < qvlength; ++j)
                {
                    this.poles[i, j] = qnurbs.Poles[j];
                }
            }
            this.uDegree = udegree;
            this.vDegree = vdegree;
            this.uPeriodic = uPeriodic;
            this.vPeriodic = vPeriodic;
            InvalidateSecondaryData();
            Init(); // im Falle von Periodic stimmt die größe der Poles nicht
#if DEBUG
            GeoPoint dbg = PointAt(new GeoPoint2D(this.uKnots[0], this.vKnots[0]));
#endif
        }
        internal NurbsSurface(Ellipse[] throughEllis)
        {
            GeoPoint[,] rawpoles = new GeoPoint[9, throughEllis.Length];
#if DEBUG
            DebuggerContainer dc = new DebuggerContainer();
#endif
            for (int i = 0; i < throughEllis.Length; i++)
            {
                BSpline bsp = throughEllis[i].ToBSpline();
                GeoPoint[] epoles;
                double[] eweights;
                double[] eknots;
                int edegree;
                bsp.GetData(out epoles, out eweights, out eknots, out edegree);
#if DEBUG
                Polyline pl = Polyline.Construct();
                pl.SetPoints(epoles, false);
                dc.Add(pl);
#endif
                if (i == 0) uKnots = eknots; // nur einmal
                for (int j = 0; j < 9; j++)
                {
                    rawpoles[j, i] = epoles[j];
                }
            }

            poles = new GeoPoint[9, throughEllis.Length];
            for (int i = 0; i < 9; i++)
            {
                GeoPoint[] tp = new GeoPoint[throughEllis.Length];
                for (int j = 0; j < tp.Length; j++)
                {
                    tp[j] = rawpoles[i, j];
                }
                BSpline diru = BSpline.Construct();
                diru.ThroughPoints(tp, 3, false);
                GeoPoint[] upoles;
                double[] uweights;
                double[] uknots;
                int udegree;
                diru.GetData(out upoles, out uweights, out uknots, out udegree);
                double uknlen = uknots[uknots.Length - 1] - uknots[0];
                if (i == 0)
                {
                    vKnots = uknots; // nur einmal
                }
                for (int j = 0; j < upoles.Length; j++)
                {
                    poles[i, j] = upoles[j];
                }
            }
            double w = Math.Sqrt(2.0) / 2.0;
            weights = new double[9, throughEllis.Length];
            for (int i = 0; i < 9; i++)
            {
                for (int j = 0; j < throughEllis.Length; j++)
                {
                    if ((i & 0x01) != 0) weights[i, j] = w;
                    else weights[i, j] = 1.0;
                }
            }
            this.uDegree = 2;
            this.vDegree = 3;
            List<double> uk = new List<double>();
            List<int> um = new List<int>();
            List<double> vk = new List<double>();
            List<int> vm = new List<int>();
            um.Add(1);
            uk.Add(uKnots[0]);
            vm.Add(1);
            vk.Add(uKnots[0]);
            for (int j = 1; j < uKnots.Length; ++j)
            {
                int lastind = uk.Count - 1;
                if (uKnots[j] == uk[lastind])
                {
                    um[lastind] += 1;
                }
                else
                {
                    uk.Add(uKnots[j]);
                    um.Add(1);
                }
            }
            for (int j = 1; j < vKnots.Length; ++j)
            {
                int lastind = vk.Count - 1;
                if (vKnots[j] == vk[lastind])
                {
                    vm[lastind] += 1;
                }
                else
                {
                    vk.Add(vKnots[j]);
                    vm.Add(1);
                }
            }
            this.uKnots = uk.ToArray();
            this.uMults = um.ToArray();
            this.vKnots = vk.ToArray();
            this.vMults = vm.ToArray();
            this.uPeriodic = true;

            Init();
        }
#if DEBUG
        public
#else
        internal 
#endif
        static NurbsSurface MakeHelicoidX(Axis axis, BSpline curveToRotate, double pitch, double interval)
        {
            int n = (int)(Math.Round(interval / (Math.PI / 10.0)) + 1); // 18° steps
            double diff = interval / n;
            ModOp r = ModOp.Translate((pitch * diff / (Math.PI * 2)) * axis.Direction.Normalized) * ModOp.Rotate(axis.Location, axis.Direction, diff);
            ModOp rinv = r.GetInverse();
            rinv = ModOp.Translate((-pitch * diff / (Math.PI * 2)) * axis.Direction.Normalized) * ModOp.Rotate(axis.Location, axis.Direction, -diff);
            GeoPoint[,] poles = new GeoPoint[curveToRotate.PoleCount, n + 3];
            double[,] weights = new double[curveToRotate.PoleCount, n + 3];
            GeoPoint[] upoles;
            double[] uweights;
            double[] uknots;
            int udegree;
            curveToRotate.GetData(out upoles, out uweights, out uknots, out udegree);
            double[] vknots = null;
            for (int i = 0; i < curveToRotate.PoleCount; i++)
            {
                GeoPoint p = rinv * rinv * curveToRotate.GetPole(i); // two steps back, will be trimmed later
                GeoPoint[] throughPoles = new GeoPoint[n + 4];
                for (int j = -2; j < n + 2; j++)
                {
                    throughPoles[j + 2] = p;
                    p = r * p;
                }
                BSpline bsp = BSpline.Construct();
                bsp.ThroughPoints(throughPoles, 3, false);
                double sp = bsp.ThroughPointsParam[2];
                double ep = bsp.ThroughPointsParam[throughPoles.Length - 2];
                BSpline bspt = bsp.TrimParam(sp, ep);
                for (int j = 0; j < bspt.PoleCount; j++)
                {
                    poles[i, j] = bspt.GetPole(j);
                    weights[i, j] = uweights[i];
                }

                if (vknots == null)
                {
                    GeoPoint[] vpoles;
                    double[] vweights;
                    int vdegree;
                    bspt.GetData(out vpoles, out vweights, out vknots, out vdegree);
                }
                // Liefert gute Ergebnisse
                // zum Ein- und Ausklingen könnte man noch einen Punkt spezifizieren, auf den die CurveToRotate zusammenschrumpfen soll
                // d.h. alle poles der kurve nähern sich linear diesem Punkt.
            }
            return new NurbsSurface(poles, weights, uknots, vknots, udegree, 3, false, false);
        }

        public NurbsSurface(GeoPoint[,] throughPoints, int maxUDegree, int maxVDegree, double[] uKnots, double[] vKnots, bool uPeriodic, bool vPeriodic)
        {   // 1. Index ist u, 2. Index ist V
            int ulength = throughPoints.GetLength(0);
            int vlength = throughPoints.GetLength(1);
            int udegree = Math.Min(maxUDegree, ulength - 1);
            int vdegree = Math.Min(maxVDegree, vlength - 1);
            double[] ku = new double[ulength - 1];
            double[] kv = new double[vlength - 1];
            double[] totuchordlength = new double[vlength];
            for (int i = 0; i < vlength; ++i)
            {
                totuchordlength[i] = 0.0;
                for (int j = 0; j < ulength - 1; ++j)
                {
                    totuchordlength[i] += throughPoints[j, i] | throughPoints[j + 1, i];
                }
                if (totuchordlength[i] == 0) totuchordlength[i] = 1.0; // das ist eine Singularität
            }
            double[] totvchordlength = new double[ulength];
            for (int i = 0; i < ulength; ++i)
            {
                totvchordlength[i] = 0.0;
                for (int j = 0; j < vlength - 1; ++j)
                {
                    totvchordlength[i] += throughPoints[i, j] | throughPoints[i, j + 1];
                }
                if (totvchordlength[i] == 0) totvchordlength[i] = 1.0; // das ist eine Singularität
            }
            for (int i = 0; i < ulength - 1; ++i)
            {
                double s = 0.0;
                for (int j = 0; j < vlength; ++j)
                {
                    s += (throughPoints[i, j] | throughPoints[i + 1, j]) / totuchordlength[j];
                }
                ku[i] = s / vlength;
                if (i > 0) ku[i] += ku[i - 1];
            }
            for (int i = 0; i < vlength - 1; ++i)
            {
                double s = 0.0;
                for (int j = 0; j < ulength; ++j)
                {
                    s += (throughPoints[j, i] | throughPoints[j, i + 1]) / totvchordlength[j];
                }
                kv[i] = s / ulength;
                if (i > 0) kv[i] += kv[i - 1];
            }
            // im Buch S 376: zuerst die Pole R[i,j] erzeugen, indem man in U interpoliert
            // dann durch diese Pole in V-Richtung interpolieren, das liefert die endgültigen Pole.
#if DEBUG
            DebuggerContainer dc = new DebuggerContainer();
#endif
            GeoPoint[,] Q = new GeoPoint[ulength, vlength]; // im Falle von periodic stimmt die Größe von Q nicht!!!
            for (int i = 0; i < vlength; ++i)
            {

                GeoPoint[] tpu = new GeoPoint[ulength];
                for (int j = 0; j < ulength; ++j)
                {
                    tpu[j] = throughPoints[j, i];
                }
                // DEBUG:
                //for (int j = 0; j < ku.Length; j++)
                //{
                //    ku[j] = (double)j / (ku.Length - 1);
                //}
                Nurbs<GeoPoint, GeoPointPole> qnurbs = new Nurbs<GeoPoint, GeoPointPole>(udegree, tpu, ku, uPeriodic);
#if DEBUG
                BSpline bsp = BSpline.Construct();
                bsp.FromNurbs(qnurbs, qnurbs.UKnots[0], qnurbs.UKnots[qnurbs.UKnots.Length - 1]);
                dc.Add(bsp);
#endif
                if (i == 0)
                {   // die Ergebnisse sind in allen Durchläufen identisch
                    List<double> uk = new List<double>();
                    List<int> um = new List<int>();
                    uk.Add(qnurbs.UKnots[0]);
                    um.Add(1);
                    for (int j = 1; j < qnurbs.UKnots.Length; ++j)
                    {
                        int lastind = uk.Count - 1;
                        if (qnurbs.UKnots[j] == uk[lastind])
                        {
                            um[lastind] += 1;
                        }
                        else
                        {
                            uk.Add(qnurbs.UKnots[j]);
                            um.Add(1);
                        }
                    }
                    this.uKnots = uk.ToArray();
                    this.uMults = um.ToArray();
                    if (uPeriodic)
                    {
                        Q = new GeoPoint[qnurbs.Poles.Length, vlength];
                    }
                }
                for (int j = 0; j < qnurbs.Poles.Length; ++j)
                {
                    Q[j, i] = qnurbs.Poles[j];
                }
            }
            this.poles = new GeoPoint[Q.GetLength(0), vlength];
            for (int i = 0; i < ulength; ++i)
            {

                GeoPoint[] tpv = new GeoPoint[vlength];
                for (int j = 0; j < vlength; ++j)
                {
                    tpv[j] = Q[i, j];
                }
                Nurbs<GeoPoint, GeoPointPole> qnurbs = new Nurbs<GeoPoint, GeoPointPole>(vdegree, tpv, kv, vPeriodic);
#if DEBUG
                BSpline bsp = BSpline.Construct();
                bsp.FromNurbs(qnurbs, qnurbs.UKnots[0], qnurbs.UKnots[qnurbs.UKnots.Length - 1]);
                dc.Add(bsp);
#endif
                if (i == 0)
                {   // die Ergebnisse sind in allen Durchläufen identisch
                    List<double> vk = new List<double>();
                    List<int> vm = new List<int>();
                    vk.Add(qnurbs.UKnots[0]);
                    vm.Add(1);
                    for (int j = 1; j < qnurbs.UKnots.Length; ++j)
                    {
                        int lastind = vk.Count - 1;
                        if (qnurbs.UKnots[j] == vk[lastind])
                        {
                            vm[lastind] += 1;
                        }
                        else
                        {
                            vk.Add(qnurbs.UKnots[j]);
                            vm.Add(1);
                        }
                    }
                    this.vKnots = vk.ToArray();
                    this.vMults = vm.ToArray();
                    if (vPeriodic)
                    {
                        this.poles = new GeoPoint[Q.GetLength(0), qnurbs.Poles.Length];
                    }
                }
                for (int j = 0; j < qnurbs.Poles.Length; ++j)
                {
                    this.poles[i, j] = qnurbs.Poles[j];
                }
            }
            this.uDegree = udegree;
            this.vDegree = vdegree;
            this.uPeriodic = uPeriodic;
            this.vPeriodic = vPeriodic;
            InvalidateSecondaryData();
            Init(); // im Falle von Periodic stimmt die größe der Poles nicht
#if DEBUG
            GeoPoint dbg = PointAt(new GeoPoint2D(this.uKnots[0], this.vKnots[0]));
#endif
        }
        internal Nurbs<GeoPoint, GeoPointPole> GetNubs()
        {
            return nubs;
        }
        private NurbsSurface(Nurbs<GeoPoint, GeoPointPole> nubs)
        {
            this.nubs = nubs;

            poles = new GeoPoint[nubs.numUPoles, nubs.numVPoles];
            for (int i = 0; i < nubs.numUPoles; i++)
            {
                for (int j = 0; j < nubs.numVPoles; j++)
                    poles[i, j] = nubs.poles[i + nubs.numUPoles * j];
            }
            weights = null;
            List<double> uk = new List<double>();
            List<int> um = new List<int>();
            for (int i = 0; i < nubs.uknots.Length; i++)
            {
                if (i == 0 || uk[uk.Count - 1] != nubs.uknots[i])
                {
                    uk.Add(nubs.uknots[i]);
                    um.Add(1);
                }
                else
                {
                    ++um[um.Count - 1];
                }
            }
            uKnots = uk.ToArray();
            uMults = um.ToArray();
            List<double> vk = new List<double>();
            List<int> vm = new List<int>();
            for (int i = 0; i < nubs.vknots.Length; i++)
            {
                if (i == 0 || vk[vk.Count - 1] != nubs.vknots[i])
                {
                    vk.Add(nubs.vknots[i]);
                    vm.Add(1);
                }
                else
                {
                    ++vm[vm.Count - 1];
                }
            }
            vKnots = vk.ToArray();
            vMults = vm.ToArray();

            uDegree = nubs.UDegree;
            vDegree = nubs.VDegree;
            uPeriodic = false;
            vPeriodic = false;
            uMinRestrict = uMaxRestrict = vMinRestrict = vMaxRestrict = 0.0;

            nubs.InitDerivS();
        }

        private NurbsSurface(Nurbs<GeoPointH, GeoPointHPole> nurbs)
        {
            this.nurbs = nurbs;

            poles = new GeoPoint[nurbs.numUPoles, nurbs.numVPoles];
            for (int i = 0; i < nurbs.numUPoles; i++)
            {
                for (int j = 0; j < nurbs.numVPoles; j++)
                    poles[i, j] = nurbs.poles[i + nurbs.numUPoles * j];
            }
            weights = new double[nurbs.numUPoles, nurbs.numVPoles];
            for (int i = 0; i < nurbs.numUPoles; i++)
            {
                for (int j = 0; j < nurbs.numVPoles; j++)
                    weights[i, j] = nurbs.poles[i + nurbs.numUPoles * j].w;
            }
            List<double> uk = new List<double>();
            List<int> um = new List<int>();
            for (int i = 0; i < nurbs.uknots.Length; i++)
            {
                if (i == 0 || uk[uk.Count - 1] != nurbs.uknots[i])
                {
                    uk.Add(nurbs.uknots[i]);
                    um.Add(1);
                }
                else
                {
                    ++um[um.Count - 1];
                }
            }
            uKnots = uk.ToArray();
            uMults = um.ToArray();
            List<double> vk = new List<double>();
            List<int> vm = new List<int>();
            for (int i = 0; i < nurbs.vknots.Length; i++)
            {
                if (i == 0 || vk[vk.Count - 1] != nurbs.vknots[i])
                {
                    vk.Add(nurbs.vknots[i]);
                    vm.Add(1);
                }
                else
                {
                    ++vm[vm.Count - 1];
                }
            }
            vKnots = vk.ToArray();
            vMults = vm.ToArray();

            uDegree = nurbs.UDegree;
            vDegree = nurbs.VDegree;
            uPeriodic = false;
            vPeriodic = false;
            uMinRestrict = uMaxRestrict = vMinRestrict = vMaxRestrict = 0.0;
            nurbs.InitDerivS();
        }



        internal Nurbs<GeoPointH, GeoPointHPole> GetNurbs()
        {
            return nurbs;
        }
        internal void SetPeriodic(bool uperiodic, bool vperiodic)
        {   // es scheint so, als sollte man so mit den NURBS umgehen: sie sind immer "clamped", periodic zeigt nur, dass der Parameter
            // ggf. um die Periode versetzt werden muss, um den richtigen Wert zu liefern.
            // diese Methode wird nur nach dem Konstruktor aufgerufen
            uPeriodic = uperiodic;
            vPeriodic = vperiodic;
        }
        internal void SetPeriodicRestriction(bool uperiodic, bool vperiodic, double uMinRestrict, double uMaxRestrict, double vMinRestrict, double vMaxRestrict)
        {
            this.uMinRestrict = uMinRestrict;
            this.uMaxRestrict = uMaxRestrict;
            this.vMinRestrict = vMinRestrict;
            this.vMaxRestrict = vMaxRestrict;
            uPeriodic = uperiodic;
            vPeriodic = vperiodic;
        }
        int induv(int u, int v) { return u + upoles * v; }
        private void Init()
        {
            List<double> uknotslist = new List<double>();
            for (int i = 0; i < uKnots.Length; ++i)
            {
                for (int j = 0; j < uMults[i]; ++j)
                {
                    uknotslist.Add(uKnots[i]);
                }
            }
            if (uPeriodic)
            {
                double dknot = uKnots[uKnots.Length - 1] - uKnots[0];
                // letztlich ist es komisch, dass zwei knoten vornedran müssen
                // ACHTUNG auf 1 geändert, warum ist das bei Surface 1 und bei Curve 2
                // das gibt keine Logik!!!
                //for (int i = 0; i < 1; ++i) 
                //{
                //    uknotslist.Insert(0, uknotslist[uknotslist.Count - uDegree - i] - dknot);
                //}
                //for (int i = 0; i < 2 * uDegree - 2; ++i)
                //{
                //    // hier "-1" zu gefügt wg Datei "prova filo.igs"
                //    uknotslist.Add(uknotslist[2 * (uDegree - 1) -1 + i] + dknot);
                //}
                // Neu:
                int secondknotindex = uMults[0];
                for (int i = 0; i <= uDegree - uMults[0]; ++i)
                {
                    uknotslist.Insert(0, uknotslist[uknotslist.Count - uDegree - i] - dknot);
                    ++secondknotindex;
                }
                while (uknotslist.Count - uDegree - 1 < poles.GetLength(0) + uDegree)
                {
                    uknotslist.Add(uknotslist[secondknotindex] + dknot);
                    ++secondknotindex;
                }
            }
            List<double> vknotslist = new List<double>();
            for (int i = 0; i < vKnots.Length; ++i)
            {
                for (int j = 0; j < vMults[i]; ++j)
                {
                    vknotslist.Add(vKnots[i]);
                }
            }
            if (vPeriodic)
            {
                double dknot = vKnots[vKnots.Length - 1] - vKnots[0];
                // letztlich ist es komisch, dass zwei knoten vornedran müssen
                //for (int i = 0; i < 1; ++i) // 
                //{
                //    vknotslist.Insert(0, vknotslist[vknotslist.Count - vDegree - i] - dknot);
                //}
                //for (int i = 0; i < 2 * vDegree - 2; ++i)
                //{
                //    // hier "-1" zu gefügt wg Datei "prova filo.igs"
                //    vknotslist.Add(vknotslist[2 * (vDegree - 1) -1 + i] + dknot);
                //}
                int secondknotindex = vMults[0];
                for (int i = 0; i <= vDegree - vMults[0]; ++i)
                {
                    vknotslist.Insert(0, vknotslist[vknotslist.Count - vDegree - i] - dknot);
                    ++secondknotindex;
                }
                while (vknotslist.Count - vDegree - 1 < poles.GetLength(1) + vDegree)
                {
                    vknotslist.Add(vknotslist[secondknotindex] + dknot);
                    ++secondknotindex;
                }
            }
            if (weights == null || !isRational)
            {
                GeoPoint[] npoles;
                if (uPeriodic && vPeriodic)
                {
                    upoles = poles.GetLength(0) + uDegree;
                    vpoles = poles.GetLength(1) + vDegree;
                    npoles = new GeoPoint[upoles * vpoles];
                    for (int i = 0; i < poles.GetLength(0); ++i)
                    {
                        for (int j = 0; j < poles.GetLength(1); ++j)
                        {
                            npoles[induv(i, j)] = poles[i, j];
                        }
                        for (int j = 0; j < vDegree; ++j)
                        {
                            npoles[induv(i, poles.GetLength(1) + j)] = poles[i, j];
                        }
                    }
                    for (int i = 0; i < uDegree; ++i)
                    {
                        for (int j = 0; j < poles.GetLength(1); ++j)
                        {
                            npoles[induv(poles.GetLength(0) + i, j)] = poles[i, j];
                        }
                        for (int j = 0; j < vDegree; ++j)
                        {
                            npoles[induv(poles.GetLength(0) + i, poles.GetLength(1) + j)] = poles[i, j];
                        }
                    }
                }
                else if (uPeriodic)
                {
                    //upoles = poles.GetLength(0) + 2 * uDegree - 2;
                    //vpoles = poles.GetLength(1);
                    //npoles = new GeoPoint[upoles * vpoles];
                    //for (int j = 0; j < poles.GetLength(1); ++j)
                    //{
                    //    for (int i = 0; i < poles.GetLength(0); ++i)
                    //    {
                    //        npoles[induv(i, j)] = poles[i, j];
                    //    }
                    //    for (int i = 0; i < 2 * uDegree - 2; ++i)
                    //    {
                    //        npoles[induv(poles.GetLength(0) + i, j)] = poles[i, j];
                    //    }
                    //}
                    // Jetzt besser:
                    upoles = poles.GetLength(0) + uDegree;
                    vpoles = poles.GetLength(1);
                    npoles = new GeoPoint[upoles * vpoles];
                    for (int j = 0; j < poles.GetLength(1); ++j)
                    {
                        for (int i = 0; i < poles.GetLength(0); ++i)
                        {
                            npoles[induv(i, j)] = poles[i, j];
                        }
                        for (int i = 0; i < uDegree; ++i)
                        {
                            npoles[induv(poles.GetLength(0) + i, j)] = poles[i, j];
                        }
                    }
                }
                else if (vPeriodic)
                {
                    upoles = poles.GetLength(0);
                    vpoles = poles.GetLength(1) + vDegree;
                    npoles = new GeoPoint[upoles * vpoles];
                    for (int i = 0; i < poles.GetLength(0); ++i)
                    {
                        for (int j = 0; j < poles.GetLength(1); ++j)
                        {
                            npoles[induv(i, j)] = poles[i, j];
                        }
                        for (int j = 0; j < vDegree; ++j)
                        {
                            npoles[induv(i, poles.GetLength(1) + j)] = poles[i, j];
                        }
                    }
                }
                else
                {
                    upoles = poles.GetLength(0);
                    vpoles = poles.GetLength(1);
                    npoles = new GeoPoint[poles.GetLength(0) * poles.GetLength(1)];
                    for (int i = 0; i < poles.GetLength(0); ++i)
                        for (int j = 0; j < poles.GetLength(1); ++j)
                        {
                            npoles[induv(i, j)] = poles[i, j];
                        }
                }
                this.nubs = new Nurbs<GeoPoint, GeoPointPole>(uDegree, vDegree, npoles, upoles, vpoles, uknotslist.ToArray(), vknotslist.ToArray());
                nubs.InitDerivS();
            }
            else
            {
                GeoPointH[] npoles;
                if (uPeriodic && vPeriodic)
                {
                    upoles = poles.GetLength(0) + uDegree;
                    vpoles = poles.GetLength(1) + vDegree;
                    npoles = new GeoPointH[upoles * vpoles];
                    for (int i = 0; i < poles.GetLength(0); ++i)
                    {
                        for (int j = 0; j < poles.GetLength(1); ++j)
                        {
                            npoles[induv(i, j)] = new GeoPointH(poles[i, j], weights[i, j]);
                        }
                        for (int j = 0; j < vDegree; ++j)
                        {
                            npoles[induv(i, poles.GetLength(1) + j)] = new GeoPointH(poles[i, j], weights[i, j]);
                        }
                    }
                    for (int i = 0; i < uDegree; ++i)
                    {
                        for (int j = 0; j < poles.GetLength(1); ++j)
                        {
                            npoles[induv(poles.GetLength(0) + i, j)] = new GeoPointH(poles[i, j], weights[i, j]);
                        }
                        for (int j = 0; j < vDegree; ++j)
                        {
                            npoles[induv(poles.GetLength(0) + i, poles.GetLength(1) + j)] = new GeoPointH(poles[i, j], weights[i, j]);
                        }
                    }
                }
                else if (uPeriodic)
                {
                    //upoles = poles.GetLength(0) + 2 * uDegree - 2;
                    //vpoles = poles.GetLength(1);
                    //npoles = new GeoPointH[upoles * vpoles];
                    //for (int j = 0; j < poles.GetLength(1); ++j)
                    //{
                    //    for (int i = 0; i < poles.GetLength(0); ++i)
                    //    {
                    //        npoles[induv(i, j)] = new GeoPointH(poles[i, j], weights[i, j]);
                    //    }
                    //    for (int i = 0; i < 2 * uDegree - 2; ++i)
                    //    {
                    //        npoles[induv(poles.GetLength(0) + i, j)] = new GeoPointH(poles[i, j], weights[i, j]);
                    //    }
                    //}
                    // Test für neues System
                    upoles = poles.GetLength(0) + uDegree;
                    vpoles = poles.GetLength(1);
                    npoles = new GeoPointH[upoles * vpoles];
                    for (int j = 0; j < poles.GetLength(1); ++j)
                    {
                        for (int i = 0; i < poles.GetLength(0); ++i)
                        {
                            npoles[induv(i, j)] = new GeoPointH(poles[i, j], weights[i, j]);
                        }
                        for (int i = 0; i < uDegree; ++i)
                        {
                            npoles[induv(poles.GetLength(0) + i, j)] = new GeoPointH(poles[i, j], weights[i, j]);
                        }
                    }
                }
                else if (vPeriodic)
                {
                    //upoles = poles.GetLength(0);
                    //vpoles = poles.GetLength(1) + 2 * vDegree - 2;
                    //npoles = new GeoPointH[upoles * vpoles];
                    //for (int i = 0; i < poles.GetLength(0); ++i)
                    //{
                    //    for (int j = 0; j < poles.GetLength(1); ++j)
                    //    {
                    //        npoles[induv(i, j)] = new GeoPointH(poles[i, j], weights[i, j]);
                    //    }
                    //    for (int j = 0; j < 2 * vDegree - 2; ++j)
                    //    {
                    //        npoles[induv(i, poles.GetLength(1) + j)] = new GeoPointH(poles[i, j], weights[i, j]);
                    //    }
                    //}
                    upoles = poles.GetLength(0);
                    vpoles = poles.GetLength(1) + vDegree;
                    npoles = new GeoPointH[upoles * vpoles];
                    for (int i = 0; i < poles.GetLength(0); ++i)
                    {
                        for (int j = 0; j < poles.GetLength(1); ++j)
                        {
                            npoles[induv(i, j)] = new GeoPointH(poles[i, j], weights[i, j]);
                        }
                        for (int j = 0; j < vDegree; ++j)
                        {
                            npoles[induv(i, poles.GetLength(1) + j)] = new GeoPointH(poles[i, j], weights[i, j]);
                        }
                    }
                }
                else
                {
                    upoles = poles.GetLength(0);
                    vpoles = poles.GetLength(1);
                    npoles = new GeoPointH[poles.GetLength(0) * poles.GetLength(1)];
                    for (int i = 0; i < poles.GetLength(0); ++i)
                        for (int j = 0; j < poles.GetLength(1); ++j)
                        {
                            npoles[induv(i, j)] = new GeoPointH(poles[i, j], weights[i, j]);
                        }
                }
                this.nurbs = new Nurbs<GeoPointH, GeoPointHPole>(uDegree, vDegree, npoles, upoles, vpoles, uknotslist.ToArray(), vknotslist.ToArray());
                nurbs.InitDerivS();
            }
#if DEBUG
            if (vKnots.Length == 1 || vKnots[0] == vKnots[vKnots.Length - 1])
            { }
#endif
        }

        private bool IsLine(IArray<GeoPoint> pnts, double precision)
        {
            for (int i = 1; i < pnts.Length - 1; i++)
            {
                if (Geometry.DistPL(pnts[i], pnts[0], pnts[pnts.Length - 1]) > precision) return false;
            }
            return true;
        }

        private bool IsCircle(IArray<GeoPoint> pnts, double precision, out GeoPoint center, out double radius)
        {
            Plane pln = Plane.FromPoints(pnts.ToArray(), out double maxerror, out bool islinear);
            //// double maxerror = PlaneFit(points, precision, out Plane pln);
            if (islinear || maxerror > precision)
            {
                center = GeoPoint.Invalid;
                radius = 0.0;
                return false;
            }

            GeoPoint2D[] points2d = new GeoPoint2D[pnts.Length];
            for (int i = 0; i < points2d.Length; i++)
            {
                points2d[i] = pln.Project(pnts[i]);
            }
            GeoPoint2D center2d;
            if (Geometry.IntersectLL(new GeoPoint2D(points2d[0], points2d[1]), (points2d[0] - points2d[1]).ToLeft(), new GeoPoint2D(points2d[2], points2d[1]), (points2d[2] - points2d[1]).ToLeft(), out GeoPoint2D ip))
            {
                center2d = ip;
                radius = points2d[0] | ip;
            }
            else
            {
                BoundingRect ext = new BoundingRect(points2d);
                center2d = ext.GetCenter();
                radius = ext.Size / 4.0;
            }
            double error = Circle2D.Fit(points2d, ref center2d, ref radius);
            if (error < precision)
            {
                center = pln.ToGlobal(center2d);
                return true;
            }
            else
            {
                center = GeoPoint.Invalid;
                return false;
            }
        }
        public override ISurface GetCanonicalForm(double precision, BoundingRect? bounds)
        {
            double umin, umax, vmin, vmax;
            GetNaturalBounds(out umin, out umax, out vmin, out vmax);
            double[] us = GetUSingularities();
            double[] vs = GetVSingularities();
            double[] upars = GetPars(umin, umax, IsUPeriodic, us, 5);
            double[] vpars = GetPars(vmin, vmax, IsVPeriodic, vs, 5);
            // upars, vpars describe an almost evenly spaced 5x5 grid while singularities and seams (of closed surface) are avoided
            // now lets see, whether in the middle we have a line or circular arc

            precision = Math.Max(precision, PolesExtent.Size * 1e-3);

            GeoPoint[,] samples = new GeoPoint[5, 5];
            for (int i = 0; i < 5; i++)
            {
                samples[i, 2] = PointAt(new GeoPoint2D(upars[i], vpars[2]));
                if (i != 2) samples[2, i] = PointAt(new GeoPoint2D(upars[2], vpars[i]));
            }
            bool uIsLine = false, uIsCircle = false, vIsLine = false, vIsCircle = false;
            GeoPoint cnt;
            double rad;
            GeoPoint[] ucnt = null, vcnt = null; // centers of the circles in u resp. v
            double[] urad = null, vrad = null; //radii of the circles in u resp. v
            if (IsLine(samples.Row(2), precision)) uIsLine = true;
            else if (IsCircle(samples.Row(2), precision, out cnt, out rad))
            {
                uIsCircle = true;
                ucnt = new GeoPoint[5];
                urad = new double[5];
                ucnt[2] = cnt;
                urad[2] = rad;
            }
            if (IsLine(samples.Column(2), precision)) vIsLine = true;
            else if (IsCircle(samples.Column(2), precision, out cnt, out rad))
            {
                vIsCircle = true;
                vcnt = new GeoPoint[5];
                vrad = new double[5];
                vcnt[2] = cnt;
                vrad[2] = rad;
            }
            if ((uIsLine || uIsCircle) && (vIsLine || vIsCircle))
            {   // we can try to create a plane, cylinder, cone, sphere or torus
                for (int i = 0; i < 5; i++) for (int j = 0; j < 5; j++)
                    {
                        //if (i == 2 || j == 2) continue; // already calculated
                        samples[i, j] = PointAt(new GeoPoint2D(upars[i], vpars[j]));
                    }
                bool failed = false;
                foreach (int i in new int[] { 0, 4 })
                {
                    if (failed) break;
                    if (uIsCircle)
                    {
                        if (IsCircle(samples.Row(i), precision, out cnt, out rad))
                        {
                            ucnt[i] = cnt;
                            urad[i] = rad;
                        }
                        else failed = true;
                    }
                    if (uIsLine) if (!IsLine(samples.Row(i), precision)) failed = true;
                    if (vIsCircle)
                    {
                        if (IsCircle(samples.Column(i), precision, out cnt, out rad))
                        {
                            vcnt[i] = cnt;
                            vrad[i] = rad;
                        }
                        else failed = true;
                    }
                    if (vIsLine) if (!IsLine(samples.Column(i), precision)) failed = true;
                }
                if (!failed)
                {
                    ISurface found = null;
                    // now in each direction there are either 3 circles or 3 lines
                    if (uIsLine && vIsLine)
                    {   // a plane or a hyperboloid
                        //double minerror = GaussNewtonMinimizer.PlaneFit(samples.Linear(), precision, out Plane pln);
                        //if (minerror < precision)
                        //{
                        //    found = new PlaneSurface(pln);
                        //}
                        //else
                        //{
                        Plane plfp = Plane.FromPoints(samples.Linear().ToArray(), out double maxdist1, out bool islin1);
                        if (maxdist1 < precision) found = new PlaneSurface(plfp); // GaussNewtonMinimizer not well implemented
                        //}

                    }
                    else if (uIsCircle && vIsCircle)
                    {   // a sphere or a torus
                        // which circles are the longitude and which are the latitudes (latitudes differ in radius)
                        double du = Math.Abs(urad[0] - urad[2]) + Math.Abs(urad[2] - urad[4]);
                        double dv = Math.Abs(vrad[0] - vrad[2]) + Math.Abs(vrad[2] - vrad[4]);
                        GeoPoint[] latcnt, loncnt;
                        double[] latrad, lonrad;
                        if (du < dv)
                        {
                            loncnt = ucnt;
                            latcnt = vcnt;
                            lonrad = urad;
                            latrad = vrad;
                        }
                        else
                        {
                            loncnt = vcnt;
                            latcnt = ucnt;
                            lonrad = vrad;
                            latrad = urad;
                        }
                        double c = (loncnt[0] | loncnt[2]) + (loncnt[2] | loncnt[4]);
                        if (c < 2 * precision)
                        {   // this should be a sphere, because all longitudes have the same center
                            double minerror = GaussNewtonMinimizer.SphereFit(samples.Linear(), new GeoPoint(loncnt[0], loncnt[2], loncnt[4]), (lonrad[0] + lonrad[2] + lonrad[4]) / 3, precision, out SphericalSurface ss);
                            if (minerror < precision) found = ss;
                            else
                            {

                            }
                        }
                        else
                        {   // this could be a torus
                            // the axis should be the longest connection of the three latitude centers (two could be identical)
                            double d0 = latcnt[0] | latcnt[2];
                            double d2 = latcnt[2] | latcnt[4];
                            double d4 = latcnt[4] | latcnt[0];
                            GeoVector axis;
                            if (d0 < d2)
                            {
                                if (d2 < d4) axis = latcnt[4] - latcnt[0];
                                else axis = latcnt[2] - latcnt[4];
                            }
                            else
                            {
                                if (d0 < d4) axis = latcnt[4] - latcnt[0];
                                else axis = latcnt[0] - latcnt[2];
                            }
                            GeoPoint center = Geometry.DropPL(loncnt[0], latcnt[0], axis);
                            axis.Length = center | loncnt[0];
                            double minerror = BoxedSurfaceExtension.TorusFit(samples.Linear(), center, axis, lonrad[0], precision, out ToroidalSurface ts);
                            if (minerror < precision)
                            {
                                if (ts.MinorRadius > ts.XAxis.Length * 10)
                                {   // almost a sphere
                                    minerror = GaussNewtonMinimizer.SphereFit(samples.Linear(), ts.Location, ts.MinorRadius, precision, out SphericalSurface ss);
                                    if (minerror < precision) found = ss;
                                }
                                else
                                {
                                    found = ts; // no torus with pole, this is usually meant to be a sphere
                                    foreach (GeoPoint point in samples)
                                    {
                                        if (ts.GetDistance(point) > precision)
                                        {
                                            found = null;
                                            break;
                                        }
                                    }
                                }
                            }
                        }
                    }
                    else // mixed case: a line and a circle
                    {   // a cylinder or a cone
                        GeoPoint[] centers = uIsCircle ? ucnt : vcnt;
                        double[] radii = uIsCircle ? urad : vrad;
                        if (Math.Abs(radii[0] - radii[2]) < precision && Math.Abs(radii[4] - radii[2]) < precision)
                        {   // could be a cylinder
                            GeoVector axis = (centers[4] - centers[0]).Normalized;
                            // GaussNewtonMinimizer.CylinderFit not working with 6.stp
                            //double minerror = GaussNewtonMinimizer.CylinderFit(samples.Linear(), centers[2], axis, radii[0], precision, out CylindricalSurface cs);
                            //if (minerror < precision) found = cs;
                            //else
                            //{

                            //}
                        }
                        else
                        {   // could be a cone
                            IArray<GeoPoint> l1 = uIsLine ? samples.Row(1) : samples.Column(1);
                            IArray<GeoPoint> l2 = uIsLine ? samples.Row(3) : samples.Column(3);
                            if (IsLine(l1, precision) && IsLine(l2, precision))
                            {
                                try
                                {
                                    GeoPoint apex = Geometry.IntersectLL(l1.First, l1.Last - l1.First, l2.First, l2.Last - l2.First); // they are not parallel
                                    GeoVector axis = centers[4] - centers[0];
                                    double a = axis.Length;
                                    double x = a * radii[0] / (radii[0] - radii[4]);
                                    GeoPoint a1 = centers[0] + x * axis.Normalized;
                                    double openingAngle = Math.Atan2(Math.Abs(radii[0] - radii[4]), centers[0] | centers[4]);
                                    double minerror = BoxedSurfaceExtension.ConeFit(samples.Linear(), a1, axis, openingAngle, precision, out ConicalSurface cs);
                                    if (minerror < precision) found = cs;
                                    if (Math.Abs(radii[0] - radii[4]) < 10 * precision)
                                    {   // could still be a cylinder
                                        double minerrorcyl = GaussNewtonMinimizer.CylinderFit(samples.Linear(), centers[2], axis, radii[0], precision, out CylindricalSurface cyls);
                                        if (minerrorcyl < precision && minerrorcyl < minerror) found = cyls;
                                    }
                                }
                                catch (Geometry.GeometryException) { }
                            }
                        }
                    }
                    if (found != null)
                    {
                        // test the orientation:
                        GeoPoint2D uvcnt = new GeoPoint2D((umin + umax) / 2.0, (vmin + vmax) / 2.0);
                        GeoVector normalnrbs = GetNormal(uvcnt);
                        GeoVector normalts = found.GetNormal(found.PositionOf(PointAt(uvcnt)));
                        if (normalts * normalnrbs < 0) found.ReverseOrientation();
                        return found;
                    }
                }
            }



            return null;
        }
        public ISurface GetCanonicalFormOld(double precision, BoundingRect? bounds)
        {
            double umin, umax, vmin, vmax;
            if (bounds.HasValue)
            {
                umin = bounds.Value.Left;
                umax = bounds.Value.Right;
                vmin = bounds.Value.Bottom;
                vmax = bounds.Value.Top;
            }
            else
            {
                GetNaturalBounds(out umin, out umax, out vmin, out vmax);
            }
            // fixedu, fixedv Ebenen finden
            double[] us = GetUSingularities();
            double[] vs = GetVSingularities();
            double[] upars = GetPars(umin, umax, IsUPeriodic, us, 3);
            double[] vpars = GetPars(vmin, vmax, IsVPeriodic, vs, 3);
            int numLines = 0;
            int numCircles = 0;
            List<ICurve> curves = new List<ICurve>();
#if DEBUG
            DebuggerContainer dc = new DebuggerContainer();
#endif
            for (int i = 0; i < 3; ++i)
            {
                BSpline bsp = FixedU(upars[i]);
                ICurve simpleCurve;
                if (bsp.GetSimpleCurve(precision, out simpleCurve))
                {
                    if (simpleCurve is Ellipse)
                    {
                        if (Math.Abs((simpleCurve as Ellipse).MajorRadius - (simpleCurve as Ellipse).MinorRadius) < precision)
                        {
                            ++numCircles;
                            curves.Add(simpleCurve);
                        }
                    }
                    else if (simpleCurve is Line)
                    {
                        ++numLines;
                        curves.Add(simpleCurve);
                    }
                    else return null;
                }
#if DEBUG
                dc.Add(bsp);
#endif
            }
            for (int i = 0; i < 3; ++i)
            {
                BSpline bsp = FixedV(vpars[i]);
                ICurve simpleCurve;
                if (bsp.GetSimpleCurve(precision, out simpleCurve))
                {
                    if (simpleCurve is Ellipse)
                    {
                        if (Math.Abs((simpleCurve as Ellipse).MajorRadius - (simpleCurve as Ellipse).MinorRadius) < precision)
                        {
                            ++numCircles;
                            curves.Add(simpleCurve);
                        }
                    }
                    else if (simpleCurve is Line)
                    {
                        ++numLines;
                        curves.Add(simpleCurve);
                    }
                    else return null;
                }
#if DEBUG
                dc.Add(bsp);
#endif
            }
            GeoPoint[] samples = new GeoPoint[9];
            GeoVector[] normals = new GeoVector[9];
            for (int i = 0; i < 3; i++)
            {
                for (int j = 0; j < 3; j++)
                {
                    samples[i * 3 + j] = PointAt(new GeoPoint2D(upars[i], vpars[j]));
                    normals[i * 3 + j] = GetNormal(new GeoPoint2D(upars[i], vpars[j]));
                }
            }
            if (numCircles == 6)
            {   // maybe a sphere or a torus
                samples = new GeoPoint[25]; // 25 evenly spread points
                double ustep = (umax - umin) / 4;
                if (IsUPeriodic) ustep = (umax - umin) / 5;
                double vstep = (vmax - vmin) / 4;
                if (IsVPeriodic) vstep = (vmax - vmin) / 5;
                for (int i = 0; i < 5; i++)
                {
                    for (int j = 0; j < 5; j++)
                    {
                        samples[5 * i + j] = PointAt(new GeoPoint2D(umin + i * ustep, vmin + j * vstep));
                    }
                }
                double d1 = Math.Abs((curves[0] as Ellipse).MajorRadius - (curves[1] as Ellipse).MajorRadius) +
                    Math.Abs((curves[1] as Ellipse).MajorRadius - (curves[2] as Ellipse).MajorRadius) +
                    Math.Abs((curves[2] as Ellipse).MajorRadius - (curves[0] as Ellipse).MajorRadius);
                double d2 = Math.Abs((curves[3] as Ellipse).MajorRadius - (curves[4] as Ellipse).MajorRadius) +
                    Math.Abs((curves[4] as Ellipse).MajorRadius - (curves[5] as Ellipse).MajorRadius) +
                    Math.Abs((curves[5] as Ellipse).MajorRadius - (curves[3] as Ellipse).MajorRadius);
                Axis ax = new Axis();

                if (d2 < d1) curves.Reverse(); // now the first 3 circles are longitudes (same radius) and the other 3 circles are latitudes
                GaussNewtonMinimizer.LineFit(new GeoPoint[] { (curves[3] as Ellipse).Center, (curves[4] as Ellipse).Center, (curves[5] as Ellipse).Center }.ToIArray(), precision, out ax.Location, out ax.Direction);
                double minrad = ((curves[0] as Ellipse).MajorRadius + (curves[1] as Ellipse).MajorRadius + (curves[2] as Ellipse).MajorRadius) / 3.0;
                double majrad = (Geometry.DistPL((curves[0] as Ellipse).Center, ax) + Geometry.DistPL((curves[1] as Ellipse).Center, ax) + Geometry.DistPL((curves[2] as Ellipse).Center, ax)) / 3.0;
                double maxError;
                ISurface approx = null;
                if (majrad < precision * 10)
                {
                    maxError = GaussNewtonMinimizer.SphereFit(samples.ToIArray(), (curves[0] as Ellipse).Center, minrad, precision, out SphericalSurface ss);
                    if (maxError < precision)
                    {
                        approx = ss;
                    }
                }
                if (approx == null)
                {
                    maxError = GaussNewtonMinimizer.TorusFit(samples.ToIArray(), ax.Location, majrad * ax.Direction.Normalized, minrad, precision, out ToroidalSurface ts);
                    if (maxError < precision)
                    {
                        approx = ts;
                    }
                }
                if (approx != null)
                {
                    // test the orientation:
                    GeoPoint2D uvcnt = new GeoPoint2D((umin + umax) / 2.0, (vmin + vmax) / 2.0);
                    GeoVector normalnrbs = GetNormal(uvcnt);
                    GeoVector normalts = approx.GetNormal(approx.PositionOf(PointAt(uvcnt)));
                    if (normalts * normalnrbs < 0) approx.ReverseOrientation();
#if DEBUG
                    double dd = 0.0;
                    for (int i = 0; i < 4; i++)
                    {
                        for (int j = 0; j < 4; j++)
                        {
                            GeoPoint p0 = PointAt(new GeoPoint2D(umin + ustep / 2 + i * ustep, vmin + vstep / 2 + j * vstep));
                            double d = p0 | approx.PointAt(approx.PositionOf(p0));
                            dd += d;
                        }
                    }
                    DebuggerContainer dtc = new DebuggerContainer();
                    dc.Add(Face.MakeFace(approx, new BoundingRect(0, 0, Math.PI, Math.PI)));
                    dc.Add(Face.MakeFace(approx, new BoundingRect(0, Math.PI, Math.PI, 2 * Math.PI)));
                    dc.Add(Face.MakeFace(approx, new BoundingRect(Math.PI, 0, 2 * Math.PI, Math.PI)));
                    dc.Add(Face.MakeFace(approx, new BoundingRect(Math.PI, Math.PI, 2 * Math.PI, 2 * Math.PI)));
                    dc.Add(this.DebugGrid);
#endif
                    return approx;
                }
            }
            else if (numCircles == 3 && numLines == 3)
            {
                // 3 circles, 3 lines:
                double maxError = findBestFitCylinder(samples, normals, out GeoPoint location, out GeoVector direction, out double radius);
                if (maxError < precision)
                {
                    direction.ArbitraryNormals(out GeoVector dirx, out GeoVector diry);
                    CylindricalSurface approx = new CylindricalSurface(location, radius * dirx.Normalized, radius * diry.Normalized, direction);
                    if (TestIntermediatePoints(approx, upars, vpars, precision)) return approx;
                }

                samples = new GeoPoint[25]; // 25 evenly spread points
                double ustep = (umax - umin) / 4;
                if (IsUPeriodic) ustep = (umax - umin) / 5;
                double vstep = (vmax - vmin) / 4;
                if (IsVPeriodic) vstep = (vmax - vmin) / 5;
                for (int i = 0; i < 5; i++)
                {
                    for (int j = 0; j < 5; j++)
                    {
                        samples[5 * i + j] = PointAt(new GeoPoint2D(umin + i * ustep, vmin + j * vstep));
                    }
                }
                int ci = 0;
                if (curves[0] is Line) ci = 3;
                Axis ax = new Axis();
                GaussNewtonMinimizer.LineFit(new GeoPoint[] { (curves[ci] as Ellipse).Center, (curves[ci + 1] as Ellipse).Center, (curves[ci + 2] as Ellipse).Center }.ToIArray(), precision, out ax.Location, out ax.Direction);
                double dr = (curves[ci + 1] as Ellipse).Radius - (curves[ci] as Ellipse).Radius;
                GeoVector dir = (curves[ci + 1] as Ellipse).Center - (curves[ci] as Ellipse).Center;
                double theta = Math.Atan2(dr, dir.Length);
                GeoPoint c0 = Geometry.IntersectLL(curves[3 - ci].StartPoint, curves[3 - ci].StartDirection, curves[4 - ci].StartPoint, curves[4 - ci].StartDirection);
                maxError = GaussNewtonMinimizer.ConeFit(samples.ToIArray(), c0, ax.Direction.Normalized, theta, Precision.eps, out ConicalSurface cs);
                if (maxError < precision && cs.OpeningAngle > 0.0)
                {
                    GeoPoint2D uvcnt = new GeoPoint2D((umin + umax) / 2.0, (vmin + vmax) / 2.0);
                    GeoVector normalnrbs = GetNormal(uvcnt);
                    GeoVector normalts = cs.GetNormal(cs.PositionOf(PointAt(uvcnt)));
                    if (normalts * normalnrbs < 0) cs.ReverseOrientation();
#if DEBUG
                    double dd = 0.0;
                    for (int i = 0; i < 4; i++)
                    {
                        for (int j = 0; j < 4; j++)
                        {
                            GeoPoint p0 = PointAt(new GeoPoint2D(umin + ustep / 2 + i * ustep, vmin + vstep / 2 + j * vstep));
                            double d = p0 | cs.PointAt(cs.PositionOf(p0));
                            dd += d;
                        }
                    }
                    DebuggerContainer dtc = new DebuggerContainer();
                    dtc.Add(Face.MakeFace(cs, new BoundingRect(0, 0.1, Math.PI, 0.2)));
                    dtc.Add(Face.MakeFace(cs, new BoundingRect(Math.PI, 0.1, 2 * Math.PI, 0.2)));
                    Face.MakeFace(cs, new BoundingRect(Math.PI, 0.1, 2 * Math.PI, 0.2)).ForceTriangulation(0.001);
                    dtc.Add(this.DebugGrid);
#endif
                    return cs;
                }
            }
            else if (numLines == 6)
            {
                // 6 lines: a plane or a hyperboloid
                Plane pln = Plane.FromPoints(samples, out double maxdist, out bool isLinear);
                if (!isLinear && maxdist < precision)
                {
                    PlaneSurface approx = new PlaneSurface(pln);
                    if (TestIntermediatePoints(approx, upars, vpars, precision)) return approx;
                }
            }
            return null;
        }

        private bool TestIntermediatePoints(ISurface approx, double[] upars, double[] vpars, double precision)
        {
            GetNaturalBounds(out double umin, out double umax, out double vmin, out double vmax);
            for (int i = 0; i <= upars.Length; i++)
            {
                double u0, u1;
                if (i > 0) u0 = upars[i - 1];
                else u0 = umin;
                if (i < upars.Length) u1 = upars[i];
                else u1 = umax;
                if (u0 == u1) continue;
                double u = (u0 + u1) / 2.0;
                for (int j = 0; j <= vpars.Length; j++)
                {
                    double v0, v1;
                    if (i > 0) v0 = vpars[i - 1];
                    else v0 = vmin;
                    if (i < vpars.Length) v1 = vpars[i];
                    else v1 = vmax;
                    if (v0 == v1) continue;
                    double v = (v0 + v1) / 2.0;
                    GeoPoint testPoint = PointAt(new GeoPoint2D(u, v));
                    GeoPoint onSphere = approx.PointAt(approx.PositionOf(testPoint));
                    if ((testPoint | onSphere) > precision) return false;
                }
            }
            GeoPoint orientTestPoint = PointAt(new GeoPoint2D(upars[1], vpars[1]));
            GeoVector orientTestDirection = GetNormal(new GeoPoint2D(upars[1], vpars[1]));
            GeoPoint2D uvOnApprox = approx.PositionOf(orientTestPoint);
            if (approx.GetNormal(uvOnApprox) * orientTestDirection < 0)
            {
                approx.ReverseOrientation();
            }
            return true;
        }

        public NurbsSurface TrimmU(double u0, double u1)
        {
            if (nubs != null)
            {
                Nurbs<GeoPoint, GeoPointPole> trimmed = nubs.TrimU(u0, u1);
                NurbsSurface res = new NurbsSurface(trimmed);
                return res;
            }
            else if (nurbs != null)
            {
                Nurbs<GeoPointH, GeoPointHPole> trimmed = nurbs.TrimU(u0, u1);
                NurbsSurface res = new NurbsSurface(trimmed);
                return res;
            }
            return null;
        }
        public NurbsSurface TrimmV(double v0, double v1)
        {   // it is easier to exchange u and v and use TrimmU than implementing Nurbs.TrimmV
            GeoPoint[,] rpoles = new GeoPoint[poles.GetLength(1), poles.GetLength(0)];
            for (int i = 0; i < poles.GetLength(0); i++)
            {
                for (int j = 0; j < poles.GetLength(1); j++)
                {
                    rpoles[j, i] = poles[i, j];
                }
            }
            double[,] rweights = null;
            if (weights != null)
            {
                rweights = new double[weights.GetLength(1), weights.GetLength(0)];
                for (int i = 0; i < weights.GetLength(0); i++)
                {
                    for (int j = 0; j < weights.GetLength(1); j++)
                    {
                        rweights[j, i] = weights[i, j];
                    }
                }
            }
            NurbsSurface rsurf = new NurbsSurface(rpoles, rweights, VKnots, UKnots, vDegree, UDegree, vPeriodic, uPeriodic);
            rsurf = rsurf.TrimmU(v0, v1);
            rpoles = new GeoPoint[rsurf.poles.GetLength(1), rsurf.poles.GetLength(0)];
            for (int i = 0; i < rsurf.poles.GetLength(0); i++)
            {
                for (int j = 0; j < rsurf.poles.GetLength(1); j++)
                {
                    rpoles[j, i] = rsurf.poles[i, j];
                }
            }
            rweights = null;
            if (rsurf.weights != null)
            {
                rweights = new double[rsurf.weights.GetLength(1), rsurf.weights.GetLength(0)];
                for (int i = 0; i < rsurf.weights.GetLength(0); i++)
                {
                    for (int j = 0; j < rsurf.weights.GetLength(1); j++)
                    {
                        rweights[j, i] = rsurf.weights[i, j];
                    }
                }
            }
            return new NurbsSurface(rpoles, rweights, rsurf.VKnots, rsurf.UKnots, rsurf.vDegree, rsurf.UDegree, rsurf.vPeriodic, rsurf.uPeriodic);
        }
#if DEBUG
        public
#endif
            bool GetSimpleSurfaceX(double precision, out ISurface simpleSurface, out ModOp2D reparametrisation)
        {
            reparametrisation = ModOp2D.Identity;
            simpleSurface = null; // damit alles gesetzt ist
            if (precision == 0.0)
            {
                precision = PolesExtent.Size * 1e-3;
            }

            double umin, umax, vmin, vmax;
            this.GetNaturalBounds(out umin, out umax, out vmin, out vmax);
            if (Math.Abs(umax - umin) < Precision.eps || Math.Abs(vmax - vmin) < Precision.eps) return false;

            // 1. Test: simpel, wenn alle Grade 1 sind:
            if (uDegree == 1 && vDegree == 1)
            {
                // eine Ebene, gehe hier davon aus, dass der uv Parameterraum linear läuft
                // das könnte man sicherlich mit knots überprüfen
                GeoPoint loc = PointAt(new GeoPoint2D(umin, vmin));
                GeoPoint posx = PointAt(new GeoPoint2D(umax, vmin));
                GeoPoint posy = PointAt(new GeoPoint2D(umin, vmax));
                try
                {
                    Plane pl = new Plane(loc, posx - loc, posy - loc);
                    pl.Location = loc + pl.ToGlobal(new GeoVector2D(-umin, -vmin)); // verschiebe so, dass der Nullpunkt passt
                    reparametrisation = ModOp2D.Fit(new GeoPoint2D[] { new GeoPoint2D(umin, vmin), new GeoPoint2D(umax, vmin), new GeoPoint2D(umin, vmax) },
                        new GeoPoint2D[] { pl.Project(loc), pl.Project(posx), pl.Project(posy) }, true);
                    simpleSurface = new PlaneSurface(pl);
                    return true;
                }
                catch (ModOpException)
                {
                    return false;
                }
                catch (PlaneException)
                {
                    return false;
                }
            }
            // jetzt werden 5 fixparameter Kurven bestimmt und analysiert
            BSpline[] ufix = new BSpline[5];
            BSpline[] vfix = new BSpline[5];
            ICurve[] usimp = new ICurve[5];
            ICurve[] vsimp = new ICurve[5];
            double[] us = this.GetUSingularities();
            double[] vs = this.GetVSingularities();
            double du = (umax - umin) / 4;
            double dv = (vmax - vmin) / 4;
            ufix[2] = FixedU(umin + 2 * du);
            vfix[2] = FixedV(vmin + 2 * dv);
            if (ufix[2].GetSimpleCurve(precision, out usimp[2]) && vfix[2].GetSimpleCurve(precision, out vsimp[2]))
            {
                if ((usimp[2] is Line) && (vsimp[2] is Line))
                {   // Ebene oder Hyerbelflächen, letzteres gibts in CADability nicht
                    if (isPlanarSurface(precision, out simpleSurface, out reparametrisation)) return true;
                }
                bool uIsCircle = usimp[2] is Ellipse;
                bool vIsCircle = vsimp[2] is Ellipse;
                // es gibt ja nur Ellipse oder Linie als einfache Kurve
                // die verbleibenden 4 Kurven erzeugen
                for (int i = 0; i < 5; i++)
                {
                    if (i == 2) continue;
                    double u = umin + i * du;
                    double v = vmin + i * dv;
                    if (i == 0)
                    {   // nicht genau den Anfang nehmen
                        u = umin + 0.2 * du;
                        v = vmin + 0.2 * dv;
                    }
                    if (i == 4)
                    {   // nicht genau das Ende nehmen
                        u = umin + 3.8 * du;
                        v = vmin + 3.8 * dv;
                    }
                    if (!ufix[i].GetSimpleCurve(precision, out usimp[i])) return false;
                    if (!vfix[i].GetSimpleCurve(precision, out vsimp[i])) return false;
                    if (uIsCircle && !(usimp[i] is Ellipse)) return false;
                    if (vIsCircle && !(vsimp[i] is Ellipse)) return false;
                    if (!uIsCircle && !(usimp[i] is Line)) return false; // damit sind auch andere Kurven ausgeschlossen
                    if (!vIsCircle && !(vsimp[i] is Line)) return false;
                }
                // // Linien oder Kreise in u und v
                if (uIsCircle && vIsCircle)
                {   // Torus oder Kugel
                    GeoPoint[] ucnt = new GeoPoint[5];
                    GeoPoint[] vcnt = new GeoPoint[5];
                    for (int i = 0; i < 5; i++)
                    {
                        ucnt[i] = (usimp[i] as Ellipse).Center;
                        vcnt[i] = (vsimp[i] as Ellipse).Center;
                    }
                    GeoPoint loc; GeoVector dir;
                    GeoPoint uvcenter = PointAt(new GeoPoint2D((umin + umax) / 2, (vmin + vmax) / 2));
                    BoundingCube bc = new BoundingCube(ucnt);
                    if (bc.Size < precision && Geometry.LineFit(vcnt, out loc, out dir) < precision)
                    {   // Kugel, Mittelpunkt: bc, Achse: dir, v sind die Breitengrade
                        GeoPoint center = bc.GetCenter();
                        GeoVector dirx = uvcenter - center;
                        double radius = dirx.Length;
                        GeoVector diry = dirx ^ dir;
                        diry.Length = radius;
                        dirx = -(diry ^ dir); // andere Rechtung, damit die Naht nicht im Patch liegt
                        dirx.Length = radius;
                        dir.Length = radius;
                        simpleSurface = new SphericalSurface(center, dirx, diry, dir);
                    }
                    else
                    {
                        bc = new BoundingCube(vcnt);
                        if (bc.Size < precision && Geometry.LineFit(ucnt, out loc, out dir) < precision)
                        {   // Kugel, Mittelpunkt: bc, Achse: dir, u sind die Breitengrade (wie gewöhnlich)
                            GeoPoint center = bc.GetCenter();
                            GeoVector dirx = uvcenter - center;
                            double radius = dirx.Length;
                            GeoVector diry = dirx ^ dir;
                            diry.Length = radius;
                            dirx = -(diry ^ dir); // andere Rechtung, damit die Naht nicht im Patch liegt
                            dirx.Length = radius;
                            dir.Length = radius;
                            simpleSurface = new SphericalSurface(center, dirx, diry, dir);
                        }
                    }
                    if (simpleSurface == null)
                    {   // beide Kreisschaaren nicht zentriert: Torus
                        if (Geometry.LineFit(ucnt, out loc, out dir) < precision)
                        {   // die u-Kreis-Mittelpunkte liegen auf einer Linie, sind also Großkreise
                            double mdist; bool isLinear;
                            Plane pln = Plane.FromPoints(vcnt, out mdist, out isLinear);
                            if (mdist < precision && !isLinear)
                            {
                                GeoPoint2D[] p2d = new GeoPoint2D[5];
                                for (int i = 0; i < 5; i++)
                                {
                                    p2d[i] = pln.Project(vcnt[i]);
                                }
                                GeoPoint2D c; double r;
                                if (Geometry.CircleFitLs(p2d, out c, out r) < precision)
                                {   // liegen alle auf einem Kreis mit Radius r
                                    // hier große Gefahr wg. Zyklen...
                                    //simpleSurface =new ToroidalSurface (
                                }
                            }
                        }
                        else if (Geometry.LineFit(vcnt, out loc, out dir) < precision)
                        {   // analog mit u und v Rollentausch
                        }

                    }
                }
                // Zylinder oder Kegel
                // Ebene (nur Linien)
                if (simpleSurface != null)
                {
                    GeoPoint2D[,] src = new GeoPoint2D[5, 5];
                    GeoPoint2D[,] dst = new GeoPoint2D[5, 5];
                    for (int i = 0; i < 5; i++)
                    {
                        for (int j = 0; j < 5; j++)
                        {
                            double u = umin + i * du;
                            double v = vmin + j * dv;
                            if (i == 0)
                            {   // nicht genau den Anfang nehmen
                                u = umin + 0.2 * du;
                            }
                            if (i == 4)
                            {   // nicht genau das Ende nehmen
                                u = umin + 3.8 * du;
                            }
                            if (j == 0)
                            {   // nicht genau den Anfang nehmen
                                v = vmin + 0.2 * dv;
                            }
                            if (j == 4)
                            {   // nicht genau das Ende nehmen
                                v = vmin + 3.8 * dv;
                            }
                            src[i, +j] = new GeoPoint2D(u, v);
                            dst[i, +j] = simpleSurface.PositionOf(PointAt(src[i, +j]));
                        }
                    }
                    // src sind gleichmäßig zunehmend, bei dst kann die Periode Probleme machen
                    // hier wird nur die einfache Überschreitung der Periode überprüft, mehrfach ist selten und macht mehr Aufwand
                    if (simpleSurface.IsUPeriodic)
                    {
                        int up = 0;
                        for (int i = 1; i < 5; i++)
                        {
                            if (dst[i, 0].x > dst[i - 1, 0].x) ++up;
                            else --up;
                        }
                        if (up > 0)
                        {   // u nimmt zu
                            for (int i = 1; i < 5; i++)
                            {
                                for (int j = 0; j < 5; j++)
                                {
                                    if (dst[i, j].x < dst[i - 1, j].x)
                                    {
                                        dst[i, j] = new GeoPoint2D(dst[i, j].x + simpleSurface.UPeriod, dst[i, j].y);
                                    }
                                }
                            }
                        }
                        else
                        {
                            for (int i = 1; i < 5; i++)
                            {
                                for (int j = 0; j < 5; j++)
                                {
                                    if (dst[i, j].x > dst[i - 1, j].x)
                                    {
                                        dst[i, j] = new GeoPoint2D(dst[i, j].x - simpleSurface.UPeriod, dst[i, j].y);
                                    }
                                }
                            }
                        }
                    }
                    if (simpleSurface.IsVPeriodic)
                    {
                        int up = 0;
                        for (int i = 1; i < 5; i++)
                        {
                            if (dst[0, i].y > dst[0, i - 1].y) ++up;
                            else --up;
                        }
                        if (up > 0)
                        {   // v nimmt zu
                            for (int i = 1; i < 5; i++)
                            {
                                for (int j = 0; j < 5; j++)
                                {
                                    if (dst[i, j].y < dst[i - 1, j].y)
                                    {
                                        dst[i, j] = new GeoPoint2D(dst[i, j].x, dst[i, j].y + simpleSurface.VPeriod);
                                    }
                                }
                            }
                        }
                        else
                        {
                            for (int i = 1; i < 5; i++)
                            {
                                for (int j = 0; j < 5; j++)
                                {
                                    if (dst[i, j].y > dst[i - 1, j].y)
                                    {
                                        dst[i, j] = new GeoPoint2D(dst[i, j].x, dst[i, j].y - simpleSurface.VPeriod);
                                    }
                                }
                            }
                        }
                    }
                    // jetzt haben wir 25 Punktepaare und suchen eine gute ModOp2D, welche das abbildet
                    GeoPoint2D[] srcf = new GeoPoint2D[5 * 5];
                    GeoPoint2D[] dstf = new GeoPoint2D[5 * 5];
                    for (int i = 0; i < 5; i++)
                    {
                        for (int j = 0; j < 5; j++)
                        {
                            srcf[i * 5 + j] = src[i, j];
                            dstf[i * 5 + j] = dst[i, j];
                        }
                    }
                    try
                    {
                        reparametrisation = ModOp2D.Fit(srcf, dstf, true);
                    }
                    catch (ModOpException)
                    {
                        return false;
                    }
                    // testen, ob die ModOp ok ist
                    for (int i = 0; i < srcf.Length; i++)
                    {
                        double d = dstf[i] | reparametrisation * srcf[i];
                        if (d > precision) return false;
                    }
                    // testen, ob die Punkte ok sind
                    for (int i = 0; i < srcf.Length; i++)
                    {
                        double d = simpleSurface.PointAt(dstf[i]) | this.PointAt(srcf[i]);
                        if (d > precision) return false;
                    }
                    return true;
                }
            }
            return false;
        }
        /// <summary>
        /// Returns true if this NurbsSurface can be represented as a simpler surface. Simple surfaces have
        /// better performances. 
        /// </summary>
        /// <param name="precision">The precision within the simpler surface must approximate this surface (0.0: use global Precision)</param>
        /// <param name="simpleSurface">the found surface or null</param>
        /// <param name="reparametrisation">the needed reparametrisation of the uv space from this surface to the surface found</param>
        /// <returns>true, if simpler form exists</returns>
        public bool GetSimpleSurface(double precision, out ISurface simpleSurface, out ModOp2D reparametrisation)
        {
            reparametrisation = ModOp2D.Identity;
            simpleSurface = null; // damit alles gesetzt ist
            if (precision == 0.0)
            {
                precision = PolesExtent.Size * 1e-3;
            }

            double umin, umax, vmin, vmax;
            this.GetNaturalBounds(out umin, out umax, out vmin, out vmax);
            if (Math.Abs(umax - umin) < Precision.eps || Math.Abs(vmax - vmin) < Precision.eps) return false;

            if (uDegree == 1 && vDegree == 1)
            {
                // eine Ebene, gehe hier davon aus, dass der uv Parameterraum linear läuft
                // das könnte man sicherlich mit knots überprüfen
                GeoPoint loc = PointAt(new GeoPoint2D(umin, vmin));
                GeoPoint posx = PointAt(new GeoPoint2D(umax, vmin));
                GeoPoint posy = PointAt(new GeoPoint2D(umin, vmax));
                try
                {
                    Plane pl = new Plane(loc, posx - loc, posy - loc);
                    pl.Location = loc + pl.ToGlobal(new GeoVector2D(-umin, -vmin)); // verschiebe so, dass der Nullpunkt passt
                    reparametrisation = ModOp2D.Fit(new GeoPoint2D[] { new GeoPoint2D(umin, vmin), new GeoPoint2D(umax, vmin), new GeoPoint2D(umin, vmax) },
                        new GeoPoint2D[] { pl.Project(loc), pl.Project(posx), pl.Project(posy) }, true);
                    simpleSurface = new PlaneSurface(pl);
                    return true;
                }
                catch (ModOpException)
                {
                    return false;
                }
                catch (PlaneException)
                {
                    return false;
                }
            }
            // ein 2. Test auf Ebene:
            {
                if (isPlanarSurface(precision, out simpleSurface, out reparametrisation)) return true;
            }
            if (uDegree == 2 && vDegree == 1)
            {
                GeoPoint p1, p2, p3;
                GeoVector v1, v2, v3;
                double du;
                if (IsUPeriodic)
                {
                    du = (umax - umin) / 3.0;
                }
                else
                {
                    du = (umax - umin) / 2.0;
                }
                p1 = PointAt(new GeoPoint2D(umin, vmin));
                p2 = PointAt(new GeoPoint2D(umin + du, vmin));
                p3 = PointAt(new GeoPoint2D(umin + 2.0 * du, vmin));
                v1 = GetNormal(new GeoPoint2D(umin, vmin));
                v2 = GetNormal(new GeoPoint2D(umin + du, vmin));
                v3 = GetNormal(new GeoPoint2D(umin + 2.0 * du, vmin));
                Plane pl1;
                double r1;
                if (TestCircle(p1, v1, p2, v2, p3, v3, out pl1, out r1))
                {
                    p1 = PointAt(new GeoPoint2D(umin, vmax));
                    p2 = PointAt(new GeoPoint2D(umin + du, vmax));
                    p3 = PointAt(new GeoPoint2D(umin + 2.0 * du, vmax));
                    v1 = GetNormal(new GeoPoint2D(umin, vmax));
                    v2 = GetNormal(new GeoPoint2D(umin + du, vmax));
                    v3 = GetNormal(new GeoPoint2D(umin + 2.0 * du, vmax));
                    Plane pl2;
                    double r2;
                    if (TestCircle(p1, v1, p2, v2, p3, v3, out pl2, out r2))
                    {
                        if (Math.Abs(r1 - r2) < Precision.eps)
                        {
                            if (Geometry.DistPL(pl2.Location, pl1.Location, pl1.Normal) < Precision.eps)
                            {
                                // ein Zylinder, da zwei gleich große Kreise auf einer Achse liegen
                                // ober er auch linkshändig sein kann, also -Normal für die Z-Richtung
                                GeoVector axis = pl1.Normal;
                                CylindricalSurface cs = new CylindricalSurface(pl1.Location, r1 * pl1.DirectionX, r1 * pl1.DirectionY, axis);
                                // cs ist jetzt der richtige Zylinder, jedoch laufen u.U. die Parameter noch falsch
                                // das wird im Folgenden korrigiert:
                                GeoPoint2D uv0 = new GeoPoint2D((umin + umax) / 2.0, (vmin + vmax) / 2.0);
                                GeoPoint puv = PointAt(uv0);
                                GeoPoint2D uv1 = cs.PositionOf(puv);
                                // uv1 müsste jetzt uv0 sein,
                                // der u-Unterschied macht eine Drehung um die Achse nötig, der v-unterschied eine
                                // Verschiebung auf der Achse
                                ModOp translat = ModOp.Translate((uv0.y - uv1.y) * axis);
                                cs = cs.GetModified(translat) as CylindricalSurface;
                                ModOp rot = ModOp.Rotate(cs.Location, cs.Axis, uv0.x - uv1.x);
                                cs = cs.GetModified(rot) as CylindricalSurface;
                                double d0 = Geometry.Dist(this.PointAt(new GeoPoint2D(umin, vmin)), cs.PointAt(new GeoPoint2D(umin, vmin)));
                                double d1 = Geometry.Dist(this.PointAt(new GeoPoint2D(umin, vmax)), cs.PointAt(new GeoPoint2D(umin, vmax)));
                                double d2 = Geometry.Dist(this.PointAt(new GeoPoint2D(umax, vmin)), cs.PointAt(new GeoPoint2D(umax, vmin)));
                                double d3 = Geometry.Dist(this.PointAt(new GeoPoint2D(umax, vmax)), cs.PointAt(new GeoPoint2D(umax, vmax)));
                                if (d0 < precision && d1 < precision && d2 < precision && d3 < precision)
                                {
                                    simpleSurface = cs;
                                    reparametrisation = ModOp2D.Identity;
                                    return true;
                                }
                            }
                        }
                    }
                }
                // Zylinder und Kegelflächen kommen oft so, natürlich andersrum auch noch testen
            }
            if (uDegree == 2 && vDegree == 2)
            {   // Test aut Kugel oder Torus
                double[] us = GetUSingularities();
                double[] vs = GetVSingularities();
                GeoPoint[,] points = new GeoPoint[3, 3];
                GeoVector[,] normals = new GeoVector[3, 3];
                double[] upars = GetPars(umin, umax, IsUPeriodic, us, 3);
                double[] vpars = GetPars(vmin, vmax, IsVPeriodic, vs, 3);
                GeoPoint[] cnt = new GeoPoint[6];
                double[] rad = new double[6];
                bool ok = true;
                int curvesok = 0;
#if DEBUG
                DebuggerContainer dc = new DebuggerContainer();
#endif
                for (int i = 0; i < 3; ++i)
                {
                    BSpline bsp = FixedU(upars[i]);
                    ICurve simpleCurve;
                    if (bsp.GetSimpleCurve(precision, out simpleCurve))
                    {
                        if (simpleCurve is Ellipse)
                        {
                            if (Math.Abs((simpleCurve as Ellipse).MajorRadius - (simpleCurve as Ellipse).MinorRadius) < precision)
                            {
                                cnt[i] = (simpleCurve as Ellipse).Center;
                                rad[i] = (simpleCurve as Ellipse).MajorRadius;
                                ++curvesok;
                            }
                        }
                    }
#if DEBUG
                    dc.Add(bsp);
#endif
                }
                for (int i = 0; i < 3; ++i)
                {
                    BSpline bsp = FixedV(vpars[i]);
                    ICurve simpleCurve;
                    if (bsp.GetSimpleCurve(precision, out simpleCurve))
                    {
                        if (simpleCurve is Ellipse)
                        {
                            if (Math.Abs((simpleCurve as Ellipse).MajorRadius - (simpleCurve as Ellipse).MinorRadius) < precision)
                            {
                                cnt[i + 3] = (simpleCurve as Ellipse).Center;
                                rad[i + 3] = (simpleCurve as Ellipse).MajorRadius;
                                ++curvesok;
                            }
                        }
                    }
#if DEBUG
                    dc.Add(bsp);
#endif
                }
                if (curvesok == 6)
                {   // 6 kreise Gefunden, wo ist u, wo ist v
                    double d1 = (cnt[0] | cnt[1]) + (cnt[1] | cnt[2]) + (cnt[2] | cnt[0]);
                    double d2 = (cnt[3] | cnt[4]) + (cnt[4] | cnt[5]) + (cnt[5] | cnt[3]);
                    double r1 = Math.Abs(rad[0] - rad[1]) + Math.Abs(rad[1] - rad[2]) + Math.Abs(rad[2] - rad[0]);
                    double r2 = Math.Abs(rad[3] - rad[4]) + Math.Abs(rad[4] - rad[5]) + Math.Abs(rad[5] - rad[3]);
                    ToroidalSurface ts = null;
                    if ((d1 > d2) && (r1 < r2))
                    {   // die ersten 3 Kreise sin "v" Kreise
                        Plane pln = new Plane(cnt[0], cnt[1], cnt[2]); // Ebene der Mittelpunkte der 3 kleinen Kreise
                                                                       // die Location der Ebene stimmt noch nicht
                        GeoPoint tcnt = new GeoPoint(pln.ToGlobal(pln.Project(cnt[3])), pln.ToGlobal(pln.Project(cnt[4])), pln.ToGlobal(pln.Project(cnt[5])));
                        // GeoPoint tcnt = new GeoPoint(cnt[0], cnt[1], cnt[2]);
                        pln = new Plane(tcnt, cnt[0], cnt[2]);

                        //GeoPoint tcnt = new GeoPoint(cnt[3], cnt[4], cnt[5]);
                        //GeoVector dirx = cnt[0] - tcnt;
                        //GeoVector diry = cnt[2] - tcnt;
                        //Plane pln = new Plane(tcnt, cnt[0], cnt[2]);
                        ts = new ToroidalSurface(tcnt, pln.DirectionX, pln.DirectionY, pln.Normal, ((cnt[0] | tcnt) + (cnt[1] | tcnt) + (cnt[2] | tcnt)) / 3, (rad[0] + rad[1] + rad[2]) / 3);
                    }
                    else if ((d1 < d2) && (r1 > r2) && r2 < precision)
                    {
                        Plane pln = new Plane(cnt[3], cnt[4], cnt[5]); // Ebene der Mittelpunkte der 3 kleinen Kreise
                                                                       // die Location der Ebene stimmt noch nicht
                        GeoPoint tcnt = new GeoPoint(pln.ToGlobal(pln.Project(cnt[0])), pln.ToGlobal(pln.Project(cnt[1])), pln.ToGlobal(pln.Project(cnt[2])));
                        // GeoPoint tcnt = new GeoPoint(cnt[0], cnt[1], cnt[2]);
                        pln = new Plane(tcnt, cnt[3], cnt[5]);

                        ts = new ToroidalSurface(tcnt, pln.DirectionX, pln.DirectionY, pln.Normal, ((cnt[3] | tcnt) + (cnt[4] | tcnt) + (cnt[5] | tcnt)) / 3, (rad[3] + rad[4] + rad[5]) / 3);
                    }
                    if (ts != null)
                    {
                        GeoPoint2D[] s = new GeoPoint2D[3];
                        GeoPoint2D[] d = new GeoPoint2D[3];
                        s[0] = new GeoPoint2D(umin, vmin);
                        s[1] = new GeoPoint2D((umin + umax) / 2, vmin);
                        s[2] = new GeoPoint2D(umin, (vmin + vmax) / 2);
                        d[0] = ts.PositionOf(PointAt(s[0]));
                        d[1] = ts.PositionOf(PointAt(s[1]));
                        d[2] = ts.PositionOf(PointAt(s[2]));
                        reparametrisation = ModOp2D.Fit(s, d, true);
                        simpleSurface = ts;
                        return true;
                    }
                }
                // folgendes wird nicht mehr benötigt
                for (int i = 0; i < 3; i++)
                {
                    for (int j = 0; j < 3; j++)
                    {
                        points[i, j] = PointAt(new GeoPoint2D(upars[i], vpars[j]));
                        normals[i, j] = GetNormal(new GeoPoint2D(upars[i], vpars[j]));
                    }
                }
                GeoPoint[] center = new GeoPoint[3];
                for (int i = 0; i < 3; i++)
                {
                    if (!TestSameIntersection(points[0, i], normals[0, i], points[1, i], normals[1, i], points[2, i], normals[2, i], out center[i]))
                    {
                        ok = false;
                        break;
                    }
                }
                // Im Falle eines Kreises liegen alle center gleich, beim Torus liegen sie auf einer Linie, der Achse
                // jetzt könnte man noch die V-s prüfen, ist aber eigentlich nicht nötig
                GeoPoint c = GeoPoint.Center(center);
                if (ok && Precision.IsEqual(c, center))
                {
                    // Mittelwert aus drei verschiedenen Kreisen
                    double r = (Geometry.Dist(c, points[0, 0]) + Geometry.Dist(c, points[1, 1]) + Geometry.Dist(c, points[2, 2])) / 3.0;
                    SphericalSurface sph = new SphericalSurface(c, r * GeoVector.XAxis, r * GeoVector.YAxis, r * GeoVector.ZAxis);
                    double u2 = (umin + umax) / 2.0;
                    double v2 = (vmin + vmax) / 2.0;
                    GeoPoint2D uv = sph.PositionOf(PointAt(new GeoPoint2D(u2, v2)));
                    // uv gibt jetzt die Drehung an
                    ModOp rot = ModOp.Rotate(c, GeoVector.ZAxis, uv.x - u2);
                    sph = sph.GetModified(rot) as SphericalSurface;
                    uv = sph.PositionOf(PointAt(new GeoPoint2D(u2, v2)));
                    // u müsste jetzt stimmen, v muss noch korrigiert werden, ist mir heute zu kompliziert
                    simpleSurface = sph;
                    GeoPoint2D[] src = new GeoPoint2D[3];
                    GeoPoint2D[] dst = new GeoPoint2D[3];
                    src[0] = new GeoPoint2D(umin, vmin);
                    src[1] = new GeoPoint2D(umax, vmin);
                    src[2] = new GeoPoint2D(umin, vmax);
                    dst[0] = sph.PositionOf(PointAt(src[0]));
                    dst[1] = sph.PositionOf(PointAt(src[1]));
                    dst[2] = sph.PositionOf(PointAt(src[2]));
                    reparametrisation = ModOp2D.Fit(src, dst, true);
                    if (reparametrisation.Determinant == 0.0)
                    {
                        double umin1 = umin + 0.25 * (umax - umin);
                        double umax1 = umin + 0.75 * (umax - umin);
                        double vmin1 = vmin + 0.25 * (vmax - vmin);
                        double vmax1 = vmin + 0.75 * (vmax - vmin);
                        src[0] = new GeoPoint2D(umin1, vmin1);
                        src[1] = new GeoPoint2D(umax1, vmin1);
                        src[2] = new GeoPoint2D(umin1, vmax1);
                        dst[0] = sph.PositionOf(PointAt(src[0]));
                        dst[1] = sph.PositionOf(PointAt(src[1]));
                        dst[2] = sph.PositionOf(PointAt(src[2]));
                        reparametrisation = ModOp2D.Fit(src, dst, true);
                    }
                    return true;
                }
            }
            {
                double[] us = GetUSingularities();
                double[] vs = GetVSingularities();
                GeoPoint[,] points = new GeoPoint[3, 3];
                GeoVector[,] normals = new GeoVector[3, 3];
                double[] upars = GetPars(umin, umax, IsUPeriodic, us, 3);
                double[] vpars = GetPars(vmin, vmax, IsVPeriodic, vs, 3);
                ICurve[] ucurves = new ICurve[3];
                ICurve[] vcurves = new ICurve[3];
                int numcurves = 0;
#if DEBUG
                GeoPoint[] dbgpoints = new GeoPoint[upars.Length * vpars.Length];
                GeoVector[] dbgnormals = new GeoVector[upars.Length * vpars.Length];
                int ii = 0;
                for (int i = 0; i < upars.Length; i++)
                {
                    for (int j = 0; j < vpars.Length; j++)
                    {
                        dbgpoints[ii] = PointAt(new GeoPoint2D(upars[i], vpars[j]));
                        dbgnormals[ii] = GetNormal(new GeoPoint2D(upars[i], vpars[j]));
                        ++ii;
                    }
                }
                GeoPoint location;
                GeoVector direction;
                double radius;

                double cylerr = findBestFitCylinder(dbgpoints, dbgnormals, out location, out direction, out radius);
                DebuggerContainer dc = new DebuggerContainer();
#endif
                for (int i = 0; i < 3; ++i)
                {
                    BSpline bsp = FixedU(upars[i]);
                    ICurve simpleCurve;
                    if (bsp.GetSimpleCurve(precision, out simpleCurve))
                    {
                        ucurves[i] = simpleCurve;
                        ++numcurves;
                    }
#if DEBUG
                    dc.Add(bsp);
#endif
                }
                for (int i = 0; i < 3; ++i)
                {
                    BSpline bsp = FixedV(vpars[i]);
                    ICurve simpleCurve;
                    if (bsp.GetSimpleCurve(precision, out simpleCurve))
                    {
                        vcurves[i] = simpleCurve;
                        ++numcurves;
                    }
#if DEBUG
                    dc.Add(bsp);
#endif
                }

                if (numcurves == 6)
                {   // also alles sind entweder Linien oder Kreise
                    if (ucurves[0] is Line && ucurves[1] is Line && ucurves[2] is Line)
                    {
                        if (vcurves[0] is Line && vcurves[1] is Line && vcurves[2] is Line)
                        {   // möglicherweise Ebene
                            // kann aber auch eine Hyperbelfläche sein
                            // oder eine verzerrende Ebene
                            // für eine unverzerrte Ebene müssen die Längen der Linien gleich sein
                            if (Math.Abs(ucurves[0].Length - ucurves[1].Length) < precision &&
                                Math.Abs(ucurves[1].Length - ucurves[2].Length) < precision &&
                                Math.Abs(ucurves[2].Length - ucurves[0].Length) < precision &&
                                Math.Abs(vcurves[0].Length - vcurves[1].Length) < precision &&
                                Math.Abs(vcurves[1].Length - vcurves[2].Length) < precision &&
                                Math.Abs(vcurves[2].Length - vcurves[0].Length) < precision)
                            {
                                Plane pln = new Plane(ucurves[0].StartPoint, (ucurves[0].StartDirection + ucurves[1].StartDirection + ucurves[2].StartDirection), (vcurves[0].StartDirection + vcurves[1].StartDirection + vcurves[2].StartDirection));
                                PlaneSurface ps = new PlaneSurface(pln);
                                if (SameSurface(ps, precision, out reparametrisation))
                                {
                                    simpleSurface = ps;
                                    return true;
                                }
                            }
                            // hier auf RuledSurface testen
                        }
                        else if (vcurves[0] is Ellipse && vcurves[1] is Ellipse && vcurves[2] is Ellipse)
                        {   // Zylinder oder Kegel
                            Ellipse e0 = vcurves[0] as Ellipse;
                            Ellipse e1 = vcurves[1] as Ellipse;
                            Ellipse e2 = vcurves[2] as Ellipse;
                            simpleSurface = CylinderOrCone(e0, e1, e2, precision);
                            if (simpleSurface != null && SameSurface(simpleSurface, precision, out reparametrisation))
                            {
                                return true;
                            }
                        }
                    }
                    else if (ucurves[0] is Ellipse && ucurves[1] is Ellipse && ucurves[2] is Ellipse)
                    {
                        if (vcurves[0] is Line && vcurves[1] is Line && vcurves[2] is Line)
                        {
                            Ellipse e0 = ucurves[0] as Ellipse;
                            Ellipse e1 = ucurves[1] as Ellipse;
                            Ellipse e2 = ucurves[2] as Ellipse;
                            simpleSurface = CylinderOrCone(e0, e1, e2, precision);
                            if (simpleSurface != null && SameSurface(simpleSurface, precision, out reparametrisation))
                            {
                                GeoPoint2D dbg1 = new GeoPoint2D((umin + umax) / 2, (vmin + vmax) / 2);
                                GeoPoint pp1 = PointAt(dbg1);
                                GeoPoint pp2 = simpleSurface.PointAt(reparametrisation * dbg1);

                                return true;
                            }
                        }
                    }
                }
            }
            return false;
        }
        private ISurface CylinderOrCone(Ellipse e0, Ellipse e1, Ellipse e2, double precision)
        {
            if (e0.IsCircle && e1.IsCircle && e2.IsCircle)
            {
                if (Math.Abs(e0.Radius - e1.Radius) < precision &&
                    Math.Abs(e1.Radius - e2.Radius) < precision &&
                    Math.Abs(e2.Radius - e0.Radius) < precision)
                {   // Zylinder
                    return new CylindricalSurface(e0.Center, e0.MajorAxis, e0.MinorAxis, e0.Normal);
                }
                else
                {
#if DEBUG
                    DebuggerContainer dc = new DebuggerContainer();
                    dc.Add(e0);
                    dc.Add(e1);
                    dc.Add(e2);
                    Plane pld = e0.Plane;
                    dc.Add(e0.GetProjectedCurve(pld), System.Drawing.Color.Green, 0);
                    dc.Add(e1.GetProjectedCurve(pld), System.Drawing.Color.Green, 0);
                    dc.Add(e2.GetProjectedCurve(pld), System.Drawing.Color.Green, 0);
#endif
                    Plane pln = new Plane(e0.Center, e0.MajorAxis, e0.Normal); // Ebene durch die Kegel-Achse

                    // es wird erwartet, dass alle Kreise mit Majorradius in der selben Ebene liegen, oder?
                    GeoPoint2D p0 = pln.Project(e0.Center + e0.MajorAxis);
                    GeoPoint2D p1 = pln.Project(e0.Center - e0.MajorAxis);
                    GeoPoint2D p2 = pln.Project(e2.Center + e2.MajorAxis);
                    GeoPoint2D p3 = pln.Project(e2.Center - e2.MajorAxis);
                    GeoPoint2D ip;
                    if (Geometry.IntersectLL(p0, p2, p1, p3, out ip))
                    {
                        GeoPoint apex = pln.ToGlobal(ip);
                        SweepAngle sw = new SweepAngle(p1 - p3, p0 - p2);
                        try
                        {
                            ModOp toCone = ModOp.Fit(new GeoPoint[] { GeoPoint.Origin, new GeoPoint(1, 0, 1), new GeoPoint(0, 1, 1), new GeoPoint(-1, 0, 1) }, new GeoPoint[] { apex, e2.Center + e2.MajorAxis, e2.Center + e2.MinorAxis, e2.Center - e2.MajorAxis }, true);
                            return new ConicalSurface(toCone);
                        }
                        catch (ModOpException) { }
                        // return new ConicalSurface(apex, e0.MajorAxis, e0.MinorAxis, e0.Normal, Math.PI / 2.0 - sw.Radian / 2, 0.0);
                    }
                    //GeoPoint2D p0 = new GeoPoint2D(e0.Radius, 0.0);
                    //GeoPoint2D p1 = pln.Project(e2.Center); // muss auf der Y-Achse liegen
                    //if (Math.Abs(p1.x) > precision) return null;
                    //p1.x = e2.Radius;
                    //GeoPoint2D ip;
                    //if (Geometry.IntersectLL(GeoPoint2D.Origin, GeoPoint2D.Origin + GeoVector2D.YAxis, p0, p1, out ip))
                    //{
                    //    GeoPoint apex = pln.ToGlobal(ip);
                    //    SweepAngle sw = new SweepAngle(GeoVector2D.YAxis, p0 - p1);
                    //    return new ConicalSurface(apex, e0.MajorAxis, e0.MinorAxis, e0.Normal, sw.Radian, 0.0);
                    //}
                }
            }
            // elliptische Zylinder und Kegel noch nicht implementiert
            return null;
        }
        private static double xkykzk(GeoPoint p, int ind)
        {   // Buchstaben gemäß: http://mathworld.wolfram.com/QuadraticSurface.html
            //  ax^2+by^2+cz^2+2fyz+2gzx+2hxy+2px+2qy+2rz+d=0. 
            switch (ind)
            {
                // d wird zu 1 angenommen
                case 0: return p.x; // 2p
                case 1: return p.y; // 2q
                case 2: return p.z; // 2r
                case 3: return p.x * p.z; // 2g
                case 4: return p.y * p.z; // 2f
                case 5: return p.x * p.y; // 2h
                case 6: return p.x * p.x; // a
                case 7: return p.y * p.y; // b
                case 8: return p.z * p.z; // c
            }
            return 0.0; // kommt nicht vor
        }

#if DEBUG
        public
#else
        private
#endif
        static ModOp findBestFitQuadric(GeoPoint[] samples)
        {   // nach https://de.scribd.com/doc/14819165/Regressions-coniques-quadriques-circulaire-spherique
            //// geht leider nicht
            //Matrix m = new Matrix(9, 9, 0.0);
            //Matrix b = new Matrix(9, 1, 0.0);
            //for (int i = 0; i < samples.Length; i++)
            //{
            //    for (int j = 0; j < 9; j++)
            //    {
            //        b[j, 0] += xkykzk(samples[i], j);
            //    }
            //    for (int j = 0; j < 9; j++)
            //    {
            //        for (int k = 0; k < 9; k++)
            //            m[j, k] += xkykzk(samples[i], j) * xkykzk(samples[i], k);
            //    }
            //}
            //Matrix x = m.SaveSolve(b);
            //if (x != null)
            //{
            //    double[] res = new double[9];
            //    for (int i = 0; i < 9; i++)
            //    {
            //        res[i] = x[i, 0];
            //    }

            //    Matrix h = new DenseMatrix(3, 3);
            //    h[0, 0] = res[6] * 2;
            //    h[1, 1] = res[7] * 2;
            //    h[2, 2] = res[8] * 2;
            //    h[0, 1] = h[1, 0] = res[5];
            //    h[1, 2] = h[2, 1] = res[4];
            //    h[0, 2] = h[2, 0] = res[3];
            //    Vector p = new DenseVector(3);
            //    p[0] = res[0];
            //    p[1] = res[1];
            //    p[2] = res[2];
            //    Vector t = (Vector)h.Solve(p); // t ist das negative des Zentrums
            //    Matrix hinv = (Matrix)h.Inverse();
            //    Vector tt = (Vector)t.Clone();
            //    tt.Transpose();
            //    EigenvalueDecomposition evd = h.Eigen();
            //    Matrix ev = evd.EigenVectors;
            //    Matrix evt = Matrix.Transpose(ev); // ist gleichzeitig die Inverse
            //    Matrix normal = evt * h * ev;
            //    if (Math.Abs(normal[0, 0]) < Math.Abs(normal[1, 1]) && Math.Abs(normal[0, 0]) < Math.Abs(normal[2, 2]))
            //    {   // 1. Index ist der kleinset Eigenwert
            //        Matrix.SwapColumns(ev, 0, 2);
            //    }
            //    else if (Math.Abs(normal[1, 1]) < Math.Abs(normal[0, 0]) && Math.Abs(normal[0, 0]) < Math.Abs(normal[2, 2]))
            //    {   // 2. Index ist der kleinset Eigenwert
            //        Matrix.SwapColumns(ev, 1, 2);
            //    }
            //    if (ev.Determinant() < 0.0)
            //    {
            //        Matrix.SwapColumns(ev, 1, 0);
            //    }
            //    evt = Matrix.Transpose(ev);
            //    ModOp toNormal = new ModOp(evt) * ModOp.Translate(t[0, 0], t[1, 0], t[2, 0]);
            //    normal = evt * h * ev; // nochmal mit vertauschten Zeilen/Spalten
            //    double coeffx = normal[0, 0];
            //    double coeffy = normal[1, 1];
            //    double coeffz = normal[2, 2];
            //    // diese 3 Koeffizienten besagen ob es Kugel, Zylinder oder Kegel ist
            //    // toNormal gibt eine ModOp in den Ursprung und die Hauptachse zur Z-Achse

            //    Matrix evi = ev.Inverse();
            //    Matrix evit = evi.Clone();
            //    evit.Transpose();
            //    Matrix dbg1 = h * ev;
            //    Matrix dbg2 = h * evt;
            //    Matrix dbg3 = evi - evt; // ist gleich
            //    Matrix dbg4 = pt * hinv * p;
            //    Matrix dbg8 = pt * h * p;
            //    Matrix dbg5 = evit * h * evi;
            //    Matrix dbg6 = evt * h * ev; // dasi ist die Diagonale mit den x², y² und z² Koeffizienten
            //    Matrix dbg7 = evi * h * evit;
            //    double r = 1 - dbg4[0, 0] / 2;
            //    double r1 = 1 - dbg8[0, 0] / 2;
            //    double dh = h.Determinant();
            //    double de = ev.Determinant();
            //    double d6 = dbg6.Determinant();
            //    return toNormal;
            //}
            return ModOp.Identity;
        }
        private bool SameSurface(ISurface surface, double precision, out ModOp2D reparametrisation)
        {
            reparametrisation = ModOp2D.Null;
            double[] upar, vpar;
            GetSafeParameterSteps(uKnots[0], uKnots[uKnots.Length - 1], vKnots[0], vKnots[vKnots.Length - 1], out upar, out vpar);
            List<GeoPoint2D> uvs = new List<GeoPoint2D>(upar.Length * vpar.Length); // zusammengehörige uv Werte
            List<GeoPoint2D> uvd = new List<GeoPoint2D>(upar.Length * vpar.Length);
#if DEBUG
            //GeoObjectList dbgl = new GeoObjectList();
            //for (int i = 0; i < upar.Length; ++i)
            //{
            //    ICurve cv = surface.FixedU(upar[i], vpar[0], vpar[vpar.Length - 1]);
            //    dbgl.Add(cv as IGeoObject);
            //}
            //for (int i = 0; i < vpar.Length; ++i)
            //{
            //    ICurve cv = surface.FixedV(vpar[i], upar[0], upar[upar.Length - 1]);
            //    dbgl.Add(cv as IGeoObject);
            //}
#endif
            for (int i = 0; i < upar.Length; ++i)
            {
                for (int j = 0; j < vpar.Length; ++j)
                {
                    GeoPoint2D uv0 = new GeoPoint2D(upar[i], vpar[j]);
                    uvs.Add(uv0);
                    GeoPoint p0 = PointAt(uv0);
                    GeoPoint2D uv1 = surface.PositionOf(p0);
                    uvd.Add(uv1);
                    GeoPoint p1 = surface.PointAt(uv1); // surface ist kein NURBS, PositionOf ist meist schnell
                    if ((p0 | p1) > precision) return false;

                    if (i > 0) uv0.x = (upar[i] + upar[i - 1]) / 2.0;
                    if (j > 0) uv0.y = (vpar[j] + vpar[j - 1]) / 2.0;
                    if (i > 0 || j > 0)
                    {   // noch einen Zwischenpunkt checken
                        p0 = PointAt(uv0);
                        p1 = surface.PointAt(surface.PositionOf(p0)); // surface ist kein NURBS, PositionOf ist meist schnell
                        if ((p0 | p1) > precision) return false;
                    }
                }
            }
            // reparametrisation bestimmen: hier könnte man aus uvs und uvd ein überbestimmtes Gleichungssytem lösen
            // Probleme machen bei geschlossenen Flächen die um die periode versetzen uv Parameter
            // Man muss also die periodischen Flächen so erzeugen, dass die "Naht" zwischen dem ersten und letzten
            // uv-Wert liegt, bzw. wenn die NURBS Fläche geschlossen ist, den ersten und letzten Punkt weglassen.
            // surface ist gewöhnlich unbegrenzt
            // zuerst mal mit 3 Punkten hier arbeiten
            GeoPoint2D[] src = new GeoPoint2D[3];
            GeoPoint2D[] dst = new GeoPoint2D[3];
            //src[0] = new GeoPoint2D(upar[0], vpar[0]);
            //src[1] = new GeoPoint2D(upar[upar.Length - 1], vpar[0]);
            //src[2] = new GeoPoint2D(upar[0], vpar[vpar.Length - 1]);
            //if (IsUPeriodic)
            //{
            //    src[0].x = (upar[0] + upar[1]) / 2.0;
            //    src[1].x = (upar[upar.Length - 1] + upar[upar.Length - 2]) / 2.0;
            //    src[2].x = (upar[0] + upar[1]) / 2.0;
            //}
            //if (IsVPeriodic)
            //{
            //    src[0].y = (vpar[0] + vpar[1]) / 2.0;
            //    src[1].y = (vpar[0] + vpar[1]) / 2.0;
            //    src[2].y = (vpar[vpar.Length - 1] + vpar[vpar.Length - 2]) / 2.0;
            //}
            double umin = uKnots[0];
            double umax = uKnots[uKnots.Length - 1];
            double vmin = vKnots[0];
            double vmax = vKnots[vKnots.Length - 1];
            double u0 = umin + (umax - umin) * 0.15;
            double u1 = umin + (umax - umin) * 0.85;
            double v0 = vmin + (vmax - vmin) * 0.15;
            double v1 = vmin + (vmax - vmin) * 0.85;
            src[0] = new GeoPoint2D(u0, v0);
            src[1] = new GeoPoint2D(u1, v0);
            src[2] = new GeoPoint2D(u0, v1);
            for (int i = 0; i < 3; ++i)
            {
                dst[i] = surface.PositionOf(PointAt(src[i]));
            }
            reparametrisation = ModOp2D.Fit(src, dst, true);
#if DEBUG
            GeoPoint2D dbg = reparametrisation * src[0];
            double dbgd = dbg | dst[0];
            dbg = reparametrisation * src[1];
            dbgd = dbg | dst[1];
            dbg = reparametrisation * src[2];
            dbgd = dbg | dst[2];
            for (int i = 0; i < 3; i++)
            {
                GeoPoint porg = PointAt(src[i]);
                GeoPoint psim = surface.PointAt(dst[i]);
                double dist = porg | psim;
            }
#endif
            return true;
        }
        private double[] GetPars(double min, double max, bool isClosed, double[] singularities, int numRes)
        {
            double[] res = new double[numRes];
            double d;
            if (isClosed)
            {
                d = (max - min) / numRes;
            }
            else
            {
                d = (max - min) / (numRes - 1);
            }
            double par = min;
            int ind = 0;
            while (ind < numRes)
            {
                bool singular = false;
                for (int i = 0; i < singularities.Length; i++)
                {
                    if (Math.Abs(par - singularities[i]) < 1e-6)
                    {
                        singular = true;
                        break;
                    }
                }
                if (isClosed && Math.Abs(par - max) < 1e-6)
                {   // bei geschlossen nicht den Endpunkt nehmen
                    singular = true;
                }
                if (!singular)
                {
                    res[ind] = par;
                    ind++;
                }
                par += d;
                if (par > max)
                {
                    par -= (max - min);
                    par += d / Math.E; // inkommensurabel, man kommt nicht auf die gleichen Punkte und ungefähr um die Hälfte versetzt
                }
            }
            Array.Sort(res);
            return res;
        }
        private bool TestCircle(GeoPoint p1, GeoVector v1, GeoPoint p2, GeoVector v2, GeoPoint p3, GeoVector v3, out Plane plane, out double radius)
        {
            try
            {
                plane = new Plane(p1, v1, v2); // v1 darf nicht entgegengesetzt zu v2 sein
                if (Precision.IsPointOnPlane(p2, plane) &&
                    Precision.IsPointOnPlane(p3, plane) &&
                    Precision.IsDirectionInPlane(v3, plane))
                {
                    // wir haben eine Ebene
                    GeoPoint2D c1, c2, c3;
                    if (Geometry.IntersectLL(plane.Project(p1), plane.Project(v1), plane.Project(p2), plane.Project(v2), out c1) &&
                        Geometry.IntersectLL(plane.Project(p1), plane.Project(v1), plane.Project(p3), plane.Project(v3), out c2) &&
                        Geometry.IntersectLL(plane.Project(p2), plane.Project(v2), plane.Project(p3), plane.Project(v3), out c3))
                    {
                        GeoPoint2D c = GeoPoint2D.Center(c1, c2, c3);
                        if (Precision.IsEqual(c, c1, c2, c3))
                        {
                            radius = (Geometry.Dist(c, plane.Project(p1)) + Geometry.Dist(c, plane.Project(p2)) + Geometry.Dist(c, plane.Project(p3))) / 3.0;
                            plane.Align(c);
                            return true;
                        }
                    }
                }
            }
            catch (PlaneException)
            {
            }
            plane = Plane.XYPlane;
            radius = 0.0;
            return false;
        }
        private bool TestSameIntersection(GeoPoint p1, GeoVector v1, GeoPoint p2, GeoVector v2, GeoPoint p3, GeoVector v3, out GeoPoint ip)
        {
            ip = GeoPoint.Origin; // muss gesetzt sein!
            try
            {
                GeoPoint ip1, ip2, ip3;
                GeoPoint2D ip2d;
                Plane pl = new Plane(p1, v1, v2); // v1 darf nicht entgegengesetzt zu v2 sein
                if (!Precision.IsPointOnPlane(p2, pl)) return false;
                if (!Geometry.IntersectLL(pl.Project(p1), pl.Project(v1), pl.Project(p2), pl.Project(v2), out ip2d))
                {
                    return false;
                }
                ip1 = pl.ToGlobal(ip2d);
                pl = new Plane(p1, v1, v3); // v1 darf nicht entgegengesetzt zu v2 sein
                if (!Precision.IsPointOnPlane(p3, pl)) return false;
                if (!Geometry.IntersectLL(pl.Project(p1), pl.Project(v1), pl.Project(p3), pl.Project(v3), out ip2d))
                {
                    return false;
                }
                ip2 = pl.ToGlobal(ip2d);
                pl = new Plane(p2, v2, v3); // v1 darf nicht entgegengesetzt zu v2 sein
                if (!Precision.IsPointOnPlane(p3, pl)) return false;
                if (!Geometry.IntersectLL(pl.Project(p2), pl.Project(v2), pl.Project(p3), pl.Project(v3), out ip2d))
                {
                    return false;
                }
                ip3 = pl.ToGlobal(ip2d);
                ip = GeoPoint.Center(ip1, ip2, ip3);
                return Precision.IsEqual(ip, ip1, ip2, ip3);
            }
            catch (PlaneException)
            {
            }
            return false;
        }
        private bool isPlanarSurface(double precision, out ISurface simpleSurface, out ModOp2D reparametrisation)
        {
            // basiert nur auf Punkten, nicht auf Ableitungen. Manche NURBS (hohen Grades) habe so schlechte Ableitungen, dass man damit nicht verlässlich arbeiten kann
            simpleSurface = null;
            reparametrisation = ModOp2D.Identity;
            int n = 5;
            GeoPoint[] samples = new GeoPoint[n * n];
            double udiff = (uKnots[uKnots.Length - 1] - uKnots[0]) / (n - 1);
            double vdiff = (vKnots[vKnots.Length - 1] - vKnots[0]) / (n - 1);
            for (int i = 0; i < n; i++)
            {
                for (int j = 0; j < n; j++)
                {
                    samples[i * n + j] = PointAt(new GeoPoint2D(uKnots[0] + i * udiff, vKnots[0] + j * vdiff));
                }
            }
            double dist;
            bool isLine;
            bool isPlane = false;
            Plane pln = Plane.FromPoints(samples, out dist, out isLine);
            if (dist < precision && !isLine && !Precision.IsNullVector(pln.Normal))
            {
                isPlane = true;
                GeoPoint2D[] src = new GeoPoint2D[3];
                GeoPoint2D[] dst = new GeoPoint2D[3];
                src[0] = new GeoPoint2D(uKnots[0] + 0 * udiff, vKnots[0] + 0 * vdiff);
                src[1] = new GeoPoint2D(uKnots[0] + 0 * udiff, vKnots[0] + (n - 1) * vdiff);
                src[2] = new GeoPoint2D(uKnots[0] + (n - 1) * udiff, vKnots[0] + 0 * vdiff);
                dst[0] = pln.Project(samples[0 * n + 0]);
                dst[1] = pln.Project(samples[0 * n + n - 1]);
                dst[2] = pln.Project(samples[(n - 1) * n + 0]);
                try
                {
                    reparametrisation = ModOp2D.Fit(src, dst, true);
                    ModOp2D inv = reparametrisation.GetInverse();
                    for (int i = 0; i < n; i++)
                    {
                        for (int j = 0; j < n; j++)
                        {
                            double d = (inv * pln.Project(samples[i * n + j])) | new GeoPoint2D(uKnots[0] + i * udiff, vKnots[0] + j * vdiff);
                            if (d > precision)
                            {
                                isPlane = false;
                                break;
                            }
                        }
                        if (!isPlane) break;
                    }
                }
                catch (ModOpException)
                {
                    isPlane = false;
                }
                if (isPlane)
                {
                    simpleSurface = new PlaneSurface(pln);
                    return true;
                }
            }
            return false;
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
            if (IsVPeriodic && VPeriod > 0)
            {
                adjustVPeriod(ref vmin);
                adjustVPeriod(ref vmax);
                if (vmin >= vmax)
                {
                    if (Math.Abs(vmin - VPeriod - vKnots[0]) < Math.Abs(vmax + VPeriod - vKnots[vKnots.Length - 1]))
                    {
                        vmin = vKnots[0];
                    }
                    else
                    {
                        vmax = vKnots[vKnots.Length - 1];
                    }
                }
            }
            if (vmax > vKnots[vKnots.Length - 1]) vmax = vKnots[vKnots.Length - 1];
            if (vmin < vKnots[0]) vmin = vKnots[0];
            if ((vmax - vmin) < (vKnots[vKnots.Length - 1] - vKnots[0]) * 1e-8) return null;
            adjustUPeriod(ref u);
            double[] us = GetUSingularities();
            if (us != null)
            {
                for (int i = 0; i < us.Length; i++)
                {
                    if (us[i] == u) return null;
                }
            }
            return FixedU(u).TrimParam(vmin, vmax);
        }
        private void adjustUPeriod(ref double u)
        {
            if (IsUPeriodic && UPeriod > 0)
            {
                while (u < uKnots[0]) u += UPeriod;
                while (u > uKnots[uKnots.Length - 1]) u -= UPeriod; // u >= in u> geändert, sonst Fehler bei Trimm 0 bis UPeriod wird 0 bis 0
            }
        }
        private void adjustVPeriod(ref double v)
        {
            if (IsVPeriodic && VPeriod > 0)
            {
                while (v < vKnots[0]) v += VPeriod;
                while (v > vKnots[vKnots.Length - 1]) v -= VPeriod; // >= in > umgewandelt s.o.
            }
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
            if (IsUPeriodic && UPeriod > 0)
            {
                adjustUPeriod(ref umin);
                adjustUPeriod(ref umax);
                if (umin >= umax)
                {   // was ist besser: umin oder umax verändern?
                    if (Math.Abs(umin - UPeriod - uKnots[0]) < Math.Abs(umax + UPeriod - uKnots[uKnots.Length - 1]))
                    {
                        umin = uKnots[0];
                    }
                    else
                    {
                        umax = uKnots[uKnots.Length - 1];
                    }
                }
            }
            if (umax > uKnots[uKnots.Length - 1]) umax = uKnots[uKnots.Length - 1];
            if (umin < uKnots[0]) umin = uKnots[0];
            if ((umax - umin) < (uKnots[uKnots.Length - 1] - uKnots[0]) * 1e-8) return null;
            adjustVPeriod(ref v);
            double[] vs = GetVSingularities();
            if (vs != null)
            {
                for (int i = 0; i < vs.Length; i++)
                {
                    if (vs[i] == v) return null;
                }
            }
            return FixedV(v).TrimParam(umin, umax);
        }
        private BSpline FixedU(double u)
        {   // liefert einen BSpline mit festem U-Wert. Die Kurven werden in einem Dictionary gespeichert
            // können aber wg. Weakreference verloren gehen
            Dictionary<double, BSpline> dict = null;
            if (fixedUCurves == null) fixedUCurves = new WeakReference(null);
            try
            {
                if (fixedUCurves.Target != null)
                {
                    dict = fixedUCurves.Target as Dictionary<double, BSpline>;
                }
            }
            catch (InvalidOperationException) { }
            BSpline res = null;
            if (dict != null)
            {
                if (dict.TryGetValue(u, out res)) return res;
            }
            else
            {
                dict = new Dictionary<double, BSpline>();
            }
            if (nurbs != null)
            {
                res = BSpline.Construct();
                Nurbs<GeoPointH, GeoPointHPole> tmp = nurbs.FixedU(u);
                res.FromNurbs(tmp, vKnots[0], vKnots[vKnots.Length - 1]);
            }
            else if (nubs != null)
            {
                res = BSpline.Construct();
                Nurbs<GeoPoint, GeoPointPole> tmp = nubs.FixedU(u);
                res.FromNurbs(tmp, vKnots[0], vKnots[vKnots.Length - 1]);
            }
            dict[u] = res;
            fixedUCurves.Target = dict;
            return res;
        }
        private BSpline FixedV(double v)
        {   // liefert einen BSpline mit festem U-Wert. Die Kurven werden in einem Dictionary gespeichert
            // können aber wg. Weakreference verloren gehen
            Dictionary<double, BSpline> dict = null;
            if (fixedVCurves == null) fixedVCurves = new WeakReference(null);
            try
            {
                if (fixedVCurves.Target != null)
                {
                    dict = fixedVCurves.Target as Dictionary<double, BSpline>;
                }
            }
            catch (InvalidOperationException) { }
            BSpline res = null;
            if (dict != null)
            {
                if (dict.TryGetValue(v, out res)) return res;
            }
            else
            {
                dict = new Dictionary<double, BSpline>();
            }
            if (nurbs != null)
            {
                res = BSpline.Construct();
                Nurbs<GeoPointH, GeoPointHPole> tmp = nurbs.FixedV(v);
                res.FromNurbs(tmp, uKnots[0], uKnots[uKnots.Length - 1]);
            }
            else if (nubs != null)
            {
                res = BSpline.Construct();
                Nurbs<GeoPoint, GeoPointPole> tmp = nubs.FixedV(v);
                res.FromNurbs(tmp, uKnots[0], uKnots[uKnots.Length - 1]);
            }
            dict[v] = res;
            fixedVCurves.Target = dict;
            return res;
        }
        internal BoundingCube GetPatchExtent(BoundingRect uvPatch)
        {
            BSpline u1 = FixedU(uvPatch.Left);
            BSpline u2 = FixedU(uvPatch.Right);
            BSpline v1 = FixedV(uvPatch.Bottom);
            BSpline v2 = FixedV(uvPatch.Top);
            BoundingCube res = u1.GetIntervalExtent(uvPatch.Bottom, uvPatch.Top);
            res.MinMax(u2.GetIntervalExtent(uvPatch.Bottom, uvPatch.Top));
            res.MinMax(v1.GetIntervalExtent(uvPatch.Left, uvPatch.Right));
            res.MinMax(v2.GetIntervalExtent(uvPatch.Left, uvPatch.Right));
            return res;
        }
        public GeoPoint[,] Poles
        {
            get
            {
                return poles;
            }
        }
        public double[,] Weights
        {
            get
            {
                return weights;
            }
        }
        public double[] UKnots
        {
            get
            {
                List<double> uknotslist = new List<double>();
                for (int i = 0; i < uKnots.Length; ++i)
                {
                    for (int j = 0; j < uMults[i]; ++j)
                    {
                        uknotslist.Add(uKnots[i]);
                    }
                }
                return uknotslist.ToArray();
            }
        }
        public double[] VKnots
        {
            get
            {
                List<double> vknotslist = new List<double>();
                for (int i = 0; i < vKnots.Length; ++i)
                {
                    for (int j = 0; j < vMults[i]; ++j)
                    {
                        vknotslist.Add(vKnots[i]);
                    }
                }
                return vknotslist.ToArray();
            }
        }
        public int UDegree
        {
            get
            {
                return uDegree;
            }
        }
        public int VDegree
        {
            get
            {
                return vDegree;
            }
        }
        private double uSpan
        {
            get
            {
                return uKnots[uKnots.Length - 1] - uKnots[0];
            }
        }
        private double vSpan
        {
            get
            {
                return vKnots[vKnots.Length - 1] - vKnots[0];
            }
        }
        internal void ScaleKnots(double minu, double maxu, double minv, double maxv)
        {
            double u0 = uKnots[0];
            double ufkt = (maxu - minu) / (uKnots[uKnots.Length - 1] - uKnots[0]);
            for (int i = 0; i < uKnots.Length; i++)
            {
                uKnots[i] = (uKnots[i] - u0) * ufkt + minu;
            }
            double v0 = vKnots[0];
            double vfkt = (maxv - minv) / (vKnots[vKnots.Length - 1] - vKnots[0]);
            for (int i = 0; i < vKnots.Length; i++)
            {
                vKnots[i] = (vKnots[i] - v0) * vfkt + minv;
            }
            InvalidateSecondaryData();
            Init();
        }
        public BoundingRect GetMaximumExtent()
        {
            return new BoundingRect(uKnots[0], vKnots[0], uKnots[uKnots.Length - 1], vKnots[vKnots.Length - 1]);
        }
        /*internal bool findTangentNewton(GeoPoint2D sp, GeoPoint2D ep, GeoPoint loc, GeoVector norm, out GeoPoint2D result)
        {
            // finde den u/v Wert zwischen sp und ep, wo die Tangentialebene parallel zur Sekante liegt
            // Die Funktion f(u, v) = u²*d2u + v²*d2v + u * v * duv + u * du + v * dv + p0 nähert die Fläche in einem Punkt an
            // die Funktione u = a*t, v = b*t
            // g(t) = a²*t²*d2u + b²*t²*d2v + a*b*t²*duv + a*t*du + b*t*dv + p0 ist die angenäherte Kurve von sp nach ep
            // g'(t) = t*a²*duu + t*b²*dvv +t*a*b*duv +a*du + b*dv
            // mit der Formel aus http://mathworld.wolfram.com/Point-LineDistance3-Dimensional.html
            // transponiere das Segment so, dass PointAt(sp) = (0,0,0) und PointAt(ep) = (1,0,0)
            // duu u.s.w. seien alle in diesem System
            // | g(t) ^ (1,0,0) | soll maximal sein
            // | (0, g(t).z, -g(t).y | maximal, g(t).z² +g(t).y² maximal
            // g(t) = t²*(a²*d2u+b²*d2v+a*b*duv) + t*(a*du+b*dv) + p0
            // (p0y+a*duy*t+b*dvy*t+a^2*d2uy*t^2+b^2*d2vy*t^2+a*b*duvy*t^2) + (p0z+a*duz*t+b*dvz*t+a^2*d2uz*t^2+b^2*d2vz*t^2+a*b*duvz*t^2)
        }*/
#if DEBUG
        private new DebuggerContainer Debug
        {
            get
            {
                DebuggerContainer res = new DebuggerContainer();
                for (int i = 0; i < poles.GetLength(0); i++)
                {
                    Polyline pl = Polyline.Construct();
                    GeoPoint[] pnts = new GeoPoint[poles.GetLength(1)];
                    for (int j = 0; j < poles.GetLength(1); j++)
                    {
                        pnts[j] = poles[i, j];
                    }
                    pl.SetPoints(pnts, false);
                    pl.ColorDef = new CADability.Attribute.ColorDef("j", System.Drawing.Color.Green);
                    res.Add(pl);
                }
                for (int i = 0; i < poles.GetLength(1); i++)
                {
                    Polyline pl = Polyline.Construct();
                    GeoPoint[] pnts = new GeoPoint[poles.GetLength(0)];
                    for (int j = 0; j < poles.GetLength(0); j++)
                    {
                        pnts[j] = poles[j, i];
                    }
                    pl.SetPoints(pnts, false);
                    pl.ColorDef = new CADability.Attribute.ColorDef("j", System.Drawing.Color.Blue);
                    res.Add(pl);
                }
                BoundingRect ext = GetMaximumExtent();
                GeoPoint2D cnt = ext.GetCenter();
                GeoPoint p0 = PointAt(cnt);
                GeoVector u = UDirection(cnt);
                GeoVector v = VDirection(cnt);
                Line l = Line.Construct();
                l.SetTwoPoints(p0, p0 + u);
                l.ColorDef = new CADability.Attribute.ColorDef("u", System.Drawing.Color.Green);
                res.Add(l);
                l = Line.Construct();
                l.SetTwoPoints(p0, p0 + v);
                l.ColorDef = new CADability.Attribute.ColorDef("v", System.Drawing.Color.Blue);
                res.Add(l);
                return res;
            }
        }
        DebuggerContainer DebugKnots
        {
            get
            {
                DebuggerContainer res = new DebuggerContainer();
                for (int i = 0; i < uKnots.Length; i++)
                {
                    BSpline bsp = FixedU(uKnots[i]);
                    res.Add(bsp);
                }
                for (int i = 0; i < vKnots.Length; i++)
                {
                    BSpline bsp = FixedV(vKnots[i]);
                    res.Add(bsp);
                }
                return res;
            }
        }
        public GeoObjectList DebugPoles
        {
            get
            {
                GeoObjectList res = new GeoObjectList();
                for (int i = 0; i < poles.GetLength(0); i++)
                {
                    GeoPoint[] sub = new GeoPoint[poles.GetLength(1)];
                    for (int j = 0; j < poles.GetLength(1); j++)
                    {
                        sub[j] = poles[i, j];
                    }
                    if (Precision.IsEqual(sub))
                    {
                        Point point = Point.Construct();
                        point.Location = sub[0];
                        point.Symbol = PointSymbol.Cross;
                        res.Add(point);
                    }
                    else
                    {
                        Polyline plu = Polyline.Construct();
                        plu.SetPoints(sub, false);
                        res.Add(plu);
                    }
                }
                return res;
            }
        }
        override public GeoObjectList DebugGrid
        {
            get
            {
                GeoObjectList res = new GeoObjectList();
                double umin = uKnots[0];
                double umax = uKnots[uKnots.Length - 1];
                double vmin = vKnots[0];
                double vmax = vKnots[vKnots.Length - 1];
                int n = 50;
                for (int i = 0; i <= n; i++)
                {   // über die Diagonale
                    GeoPoint[] pu = new GeoPoint[n + 1];
                    GeoPoint[] pv = new GeoPoint[n + 1];
                    for (int j = 0; j <= n; j++)
                    {
                        pu[j] = PointAt(new GeoPoint2D(umin + j * (umax - umin) / n, vmin + i * (vmax - vmin) / n));
                        pv[j] = PointAt(new GeoPoint2D(umin + i * (umax - umin) / n, vmin + j * (vmax - vmin) / n));
                    }
                    try
                    {
                        Polyline plu = Polyline.Construct();
                        plu.SetPoints(pu, false);
                        res.Add(plu);
                    }
                    catch (PolylineException)
                    {
                        Point pntu = Point.Construct();
                        pntu.Location = pu[0];
                        pntu.Symbol = PointSymbol.Cross;
                        res.Add(pntu);
                    }
                    try
                    {
                        Polyline plv = Polyline.Construct();
                        plv.SetPoints(pv, false);
                        res.Add(plv);
                    }
                    catch (PolylineException)
                    {
                        Point pntv = Point.Construct();
                        pntv.Location = pv[0];
                        pntv.Symbol = PointSymbol.Cross;
                        res.Add(pntv);
                    }
                }
                return res;
            }
        }
        override public Face DebugAsFace
        {
            get
            {
                BoundingRect ext = GetMaximumExtent();
                return Face.MakeFace(this, new CADability.Shapes.SimpleShape(ext));
            }
        }
        public void DebugTest()
        {
            BoxedSurfaceEx bse = this.BoxedSurfaceEx;
            double[] usteps, vsteps;
            double umin = uKnots[0];
            double umax = uKnots[uKnots.Length - 1];
            double vmin = vKnots[0];
            double vmax = vKnots[vKnots.Length - 1];
            GetSafeParameterSteps(umin, umax, vmin, vmax, out usteps, out vsteps);
            DebuggerContainer dc = new DebuggerContainer();
            for (int i = 0; i < usteps.Length; i++)
            {
                dc.Add(FixedU(usteps[i], vmin, vmax) as IGeoObject);
            }
            for (int i = 0; i < vsteps.Length; i++)
            {
                dc.Add(FixedV(vsteps[i], umin, umax) as IGeoObject);
            }
        }
#endif
        #region ISurfaceImpl Overrides
        public override ISurface GetNonPeriodicSurface(ICurve[] orientedCurves)
        {
            BoundingRect ext = BoundingRect.EmptyBoundingRect;
            for (int i = 0; i < orientedCurves.Length; i++)
            {
                ext.MinMax(this.GetProjectedCurve(orientedCurves[i], 0.0).GetExtent());
            }
            if (ext.Height < vSpan * 1e-6)
            {
                ext.Bottom -= vSpan * 1e-4;
                ext.Top += vSpan * 1e-4;
            }
            BoundingRect ext1 = ext;
            ext1.InflateRelative(1.01);
            bool ok = false; // when there are poles, we only need to make it non-periodic, when the pole is inside the extent
            double[] us = GetUSingularities();
            double[] vs = GetVSingularities();
            if (us.Length > 0)
            {
                for (int i = 0; i < us.Length; i++)
                {
                    if ((us[i] >= ext1.Left && us[i] <= ext1.Right) || !uPeriodic)
                    {
                        ok = true;
                        if (us[i] < ext.Left) ext.Left = us[i];
                        if (us[i] > ext.Right) ext.Right = us[i];
                        if (us[i] > ext.Left && us[i] < ext.Right)
                        {   // the pole must be on the border, maybe we have to correct the extent here
                            if (ext.Right - us[i] < us[i] - ext.Left) ext.Right = us[i];
                            else ext.Left = us[i];
                        }
                        break;
                    }
                }
            }
            if (!ok && vs.Length > 0)
            {
                for (int i = 0; i < vs.Length; i++)
                {
                    if ((vs[i] >= ext1.Bottom && vs[i] <= ext1.Top) || !vPeriodic) // e.g. a cone-like surface with a single bound
                    {
                        ok = true;
                        if (vs[i] < ext.Bottom) ext.Bottom = vs[i];
                        if (vs[i] > ext.Top) ext.Top = vs[i];
                        if (vs[i] > ext.Bottom && vs[i] < ext.Top)
                        {   // the pole must be on the border, maybe we have to correct the extent here
                            if (ext.Top - vs[i] < vs[i] - ext.Bottom) ext.Top = vs[i];
                            else ext.Bottom = vs[i];
                        }
                        break;
                    }
                }
            }
            if (ok || (IsUPeriodic && ext.Width > UPeriod * 0.9) || (IsVPeriodic && ext.Height > VPeriod * 0.9))
            {
                return new NonPeriodicSurface(this, ext);
            }
            return null;
        }
        /// <summary>
        /// Implements <see cref="ISurface.GetModified"/>.
        /// </summary>
        /// <param name="m">how to modify</param>
        /// <returns>modified surface</returns>
        public override ISurface GetModified(ModOp m)
        {
            NurbsSurface res = new NurbsSurface(poles, weights, uKnots, vKnots,
                uMults, vMults, uDegree, vDegree, uPeriodic, vPeriodic);
            // nur die poles verändern, oder?
            for (int i = 0; i < poles.GetLength(0); i++)
            {
                for (int j = 0; j < poles.GetLength(1); j++)
                {
                    res.poles[i, j] = m * poles[i, j];
                }
            }
            res.InvalidateSecondaryData();
            res.Init();
            res.usedArea = usedArea;
            return res;
        }
        /// <summary>
        /// Overrides <see cref="CADability.GeoObject.ISurfaceImpl.IsVanishingProjection (Projection, double, double, double, double)"/>
        /// </summary>
        /// <param name="p"></param>
        /// <param name="umin"></param>
        /// <param name="umax"></param>
        /// <param name="vmin"></param>
        /// <param name="vmax"></param>
        /// <returns></returns>
        public override bool IsVanishingProjection(Projection p, double umin, double umax, double vmin, double vmax)
        {
            // Eine NURBS Fläche verschwindet nur dann, wenn sie in einer Richtung linear ist und das genau die Beobachtungsrichtung
            // ist. Oder wenn es eine Ebene ist, aber dann sollte es als Ebene existieren und nicht als NURBS!
            if (!Precision.IsPerpendicular(p.Direction, GetNormal(new GeoPoint2D(umin, vmin)), false)) return false;
            if (!Precision.IsPerpendicular(p.Direction, GetNormal(new GeoPoint2D(umin, vmax)), false)) return false;
            if (!Precision.IsPerpendicular(p.Direction, GetNormal(new GeoPoint2D(umax, vmin)), false)) return false;
            if (!Precision.IsPerpendicular(p.Direction, GetNormal(new GeoPoint2D(umax, vmax)), false)) return false;
            if (!Precision.IsPerpendicular(p.Direction, GetNormal(new GeoPoint2D((umin + umax) / 2.0, (vmin + vmax) / 2.0)), false)) return false;
            return true;
            // das ist ein bisschen zu einfach, es werden nur die 4 Eckpunkte überprüft
            // man müsste vermutlich für jeden Knoten überprüfen, aber wie sind die u/v Werte am Knoten
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
            // zuerst die Eckpunkte einschließen
            CheckZMinMax(p, umin, vmin, ref zMin, ref zMax);
            CheckZMinMax(p, umin, vmax, ref zMin, ref zMax);
            CheckZMinMax(p, umax, vmin, ref zMin, ref zMax);
            CheckZMinMax(p, umax, vmax, ref zMin, ref zMax);
            // poles machen eine Konvexe Hülle, also gibt das mit Sicherheit genug
            // man müsste wissen, welche poles man ausschließen kann, aber wie soll das gehen?
            foreach (GeoPoint pole in poles)
            {
                GeoPoint pp = p.UnscaledProjection * pole;
                if (pp.z < zMin) zMin = pp.z;
                if (pp.z > zMax) zMax = pp.z;
            }
        }
        /// <summary>
        /// Overrides <see cref="CADability.GeoObject.ISurfaceImpl.Clone ()"/>
        /// </summary>
        /// <returns></returns>
        public override ISurface Clone()
        {
            NurbsSurface res = new NurbsSurface(poles, weights, uKnots, vKnots, uMults, vMults, uDegree, vDegree, uPeriodic, vPeriodic);
            if (vMinRestrict != vMaxRestrict)
            {
                res.vMinRestrict = vMinRestrict;
                res.vMaxRestrict = vMaxRestrict;
            }
            if (uMinRestrict != uMaxRestrict)
            {
                res.uMinRestrict = uMinRestrict;
                res.uMaxRestrict = uMaxRestrict;
            }
            return res;
        }
        /// <summary>
        /// Overrides <see cref="CADability.GeoObject.ISurfaceImpl.Modify (ModOp)"/>
        /// </summary>
        /// <param name="m"></param>
        public override void Modify(ModOp m)
        {
            for (int i = 0; i < poles.GetLength(0); i++)
            {
                for (int j = 0; j < poles.GetLength(1); j++)
                {
                    poles[i, j] = m * poles[i, j];
                }
            }
            InvalidateSecondaryData();
            Init();
        }
        /// <summary>
        /// Overrides <see cref="CADability.GeoObject.ISurfaceImpl.PointAt (GeoPoint2D)"/>
        /// </summary>
        /// <param name="uv"></param>
        /// <returns></returns>
        public override GeoPoint PointAt(GeoPoint2D uv)
        {
            if (nubs == null && nurbs == null) Init(); // manchmal nötig, da währen des deserialisierens nich nicht initialisiert
            // Versuchsweise auch für Werte außerhalb
            //if (uv.x < uKnots[0]) uv.x = uKnots[0];
            //if (uv.x > uKnots[uKnots.Length - 1]) uv.x = uKnots[uKnots.Length - 1];
            //if (uv.y < vKnots[0]) uv.y = vKnots[0];
            //if (uv.y > vKnots[vKnots.Length - 1]) uv.y = vKnots[vKnots.Length - 1];
            if (IsUPeriodic && UPeriod > 0)
            {
                while (uv.x < uKnots[0]) uv.x += UPeriod;
                while (uv.x > uKnots[uKnots.Length - 1]) uv.x -= UPeriod;
            }
            if (IsVPeriodic && VPeriod > 0)
            {
                while (uv.y < vKnots[0]) uv.y += VPeriod;
                while (uv.y > vKnots[vKnots.Length - 1]) uv.y -= VPeriod;
            }
            if (nubs != null)
            {
                return nubs.SurfacePoint(uv.x, uv.y);
            }
            else
            {
                return (GeoPoint)nurbs.SurfacePoint(uv.x, uv.y);
            }
        }
        /// <summary>
        /// Overrides <see cref="CADability.GeoObject.ISurfaceImpl.DerivationAt (GeoPoint2D, out GeoPoint, out GeoVector, out GeoVector)"/>
        /// </summary>
        /// <param name="uv"></param>
        /// <param name="location"></param>
        /// <param name="du"></param>
        /// <param name="dv"></param>
        public override void DerivationAt(GeoPoint2D uv, out GeoPoint location, out GeoVector du, out GeoVector dv)
        {
            if (nubs == null && nurbs == null) Init(); // sometimes necessary
            if (IsUPeriodic && UPeriod > 0)
            {
                while (uv.x < uKnots[0]) uv.x += UPeriod;
                while (uv.x > uKnots[uKnots.Length - 1]) uv.x -= UPeriod;
            }
            if (IsVPeriodic && VPeriod > 0)
            {
                while (uv.y < vKnots[0]) uv.y += VPeriod;
                while (uv.y > vKnots[vKnots.Length - 1]) uv.y -= VPeriod;
            }
            if (nubs != null)
            {
                GeoPoint[,] der = nubs.SurfaceDeriv(uv.x, uv.y, 1);
                location = der[0, 0];
                du = new GeoVector(der[1, 0].x, der[1, 0].y, der[1, 0].z);
                dv = new GeoVector(der[0, 1].x, der[0, 1].y, der[0, 1].z);
            }
            else
            {
                GeoPointH[,] der = nurbs.SurfaceDeriv(uv.x, uv.y, 1);
                location = (GeoPoint)der[0, 0];
                du = (GeoVector)der[1, 0];
                dv = (GeoVector)der[0, 1];
            }

            // base.DerivationAt(uv, out location, out du, out dv);
        }
        public override double VPeriod
        {
            get
            {
                if (vMinRestrict != vMaxRestrict) return vMaxRestrict - vMinRestrict;
                return vKnots[vKnots.Length - 1] - vKnots[0]; // das muss so sein, sonst macht periodisch keinen Sinn, oder?
            }
        }
        public override double UPeriod
        {
            get
            {
                if (uMinRestrict != uMaxRestrict) return uMaxRestrict - uMinRestrict;
                return uKnots[uKnots.Length - 1] - uKnots[0]; // das muss so sein, sonst macht periodisch keinen Sinn, oder?
            }
        }
        /// <summary>
        /// Overrides <see cref="CADability.GeoObject.ISurfaceImpl.UDirection (GeoPoint2D)"/>
        /// </summary>
        /// <param name="uv"></param>
        /// <returns></returns>
        public override GeoVector UDirection(GeoPoint2D uv)
        {
            if (nubs == null && nurbs == null) Init(); // manchmal nötig, da währen des deserialisierens nich nicht initialisiert
            // Versuchsweise auch für Werte außerhalb
            //if (uv.x < uKnots[0]) uv.x = uKnots[0];
            //if (uv.x > uKnots[uKnots.Length - 1]) uv.x = uKnots[uKnots.Length - 1];
            //if (uv.y < vKnots[0]) uv.y = vKnots[0];
            //if (uv.y > vKnots[vKnots.Length - 1]) uv.y = vKnots[vKnots.Length - 1];
            if (IsUPeriodic && UPeriod > 0)
            {
                while (uv.x < uKnots[0]) uv.x += UPeriod;
                while (uv.x > uKnots[uKnots.Length - 1]) uv.x -= UPeriod;
            }
            if (IsVPeriodic && VPeriod > 0)
            {
                while (uv.y < vKnots[0]) uv.y += VPeriod;
                while (uv.y > vKnots[vKnots.Length - 1]) uv.y -= VPeriod;
            }
            if (nubs != null)
            {
                GeoPoint pnt, derivu, derivv;
                nubs.SurfaceDeriv1(uv.x, uv.y, out pnt, out derivu, out derivv);
                return derivu.ToVector();
            }
            else
            {
                GeoPointH pnt, derivu, derivv;
                nurbs.SurfaceDeriv1(uv.x, uv.y, out pnt, out derivu, out derivv);
                return (GeoVector)derivu;
            }
        }
        /// <summary>
        /// Overrides <see cref="CADability.GeoObject.ISurfaceImpl.VDirection (GeoPoint2D)"/>
        /// </summary>
        /// <param name="uv"></param>
        /// <returns></returns>
        public override GeoVector VDirection(GeoPoint2D uv)
        {
            if (nubs == null && nurbs == null) Init(); // manchmal nötig, da währen des deserialisierens nich nicht initialisiert
            // Versuchsweise auch für Werte außerhalb
            //if (uv.x < uKnots[0]) uv.x = uKnots[0];
            //if (uv.x > uKnots[uKnots.Length - 1]) uv.x = uKnots[uKnots.Length - 1];
            //if (uv.y < vKnots[0]) uv.y = vKnots[0];
            //if (uv.y > vKnots[vKnots.Length - 1]) uv.y = vKnots[vKnots.Length - 1];
            if (IsUPeriodic && UPeriod > 0)
            {
                while (uv.x < uKnots[0]) uv.x += UPeriod;
                while (uv.x > uKnots[uKnots.Length - 1]) uv.x -= UPeriod;
            }
            if (IsVPeriodic && VPeriod > 0)
            {
                while (uv.y < vKnots[0]) uv.y += VPeriod;
                while (uv.y > vKnots[vKnots.Length - 1]) uv.y -= VPeriod;
            }
            if (nubs != null)
            {
                GeoPoint pnt, derivu, derivv;
                nubs.SurfaceDeriv1(uv.x, uv.y, out pnt, out derivu, out derivv);
                return derivv.ToVector();
            }
            else
            {
                GeoPointH pnt, derivu, derivv;
                nurbs.SurfaceDeriv1(uv.x, uv.y, out pnt, out derivu, out derivv);
                return (GeoVector)derivv;
            }
        }
        /// <summary>
        /// Overrides <see cref="CADability.GeoObject.ISurfaceImpl.GetNormal (GeoPoint2D)"/>
        /// </summary>
        /// <param name="uv"></param>
        /// <returns></returns>
        public override GeoVector GetNormal(GeoPoint2D uv)
        {
            if (nubs == null && nurbs == null) Init(); // should always be initialized here, but who knows
            if (IsUPeriodic && UPeriod > 0)
            {
                while (uv.x < uKnots[0]) uv.x += UPeriod;
                while (uv.x > uKnots[uKnots.Length - 1]) uv.x -= UPeriod;
            }
            if (IsVPeriodic && VPeriod > 0)
            {
                while (uv.y < vKnots[0]) uv.y += VPeriod;
                while (uv.y > vKnots[vKnots.Length - 1]) uv.y -= VPeriod;
            }
            if (nubs != null)
            {
                GeoPoint pnt, derivu, derivv;
                nubs.SurfaceDeriv1(uv.x, uv.y, out pnt, out derivu, out derivv);
                GeoVector normal = derivu.ToVector() ^ derivv.ToVector();
                double udiff = uKnots[uKnots.Length - 1] - uKnots[0];
                double vdiff = vKnots[vKnots.Length - 1] - vKnots[0];
                if ((derivu - GeoPoint.Origin).Length < udiff * 1e-6)
                {   // singulär in Richtung u, d.h. keine Änderung bei Änderung von u, d.h. für umin und umax die Richtung berechnen
                    // die hoffentlich nicht gleich sind
                    GeoPoint derivu1, derivv1, derivu2, derivv2;
                    nubs.SurfaceDeriv1(uKnots[0], uv.y, out pnt, out derivu1, out derivv1);
                    nubs.SurfaceDeriv1(uKnots[uKnots.Length - 1], uv.y, out pnt, out derivu2, out derivv2);
                    normal = derivv2.ToVector() ^ derivv1.ToVector(); // die Reihenfolge scheint relevant zu sein
                }
                else if ((derivv - GeoPoint.Origin).Length < vdiff * 1e-6)
                {
                    GeoPoint derivu1, derivv1, derivu2, derivv2;
                    nubs.SurfaceDeriv1(uv.x, vKnots[0], out pnt, out derivu1, out derivv1);
                    nubs.SurfaceDeriv1(uv.x, vKnots[vKnots.Length - 1], out pnt, out derivu2, out derivv2);
                    normal = derivu1.ToVector() ^ derivu2.ToVector(); // die Reihenfolge scheint relevant zu sein
                }
                if (normal.IsNullVector())
                {   // die beiden aufspannenden Kurven des Nurbs gehen tangential ineinander über. beide Ableitungen haben die gleiche
                    // Richtung. Trotzdem gibt es einen Normalenvektor. Wenn die Fläche nicht komplett entartet ist, dann sollte es
                    // in der Nähe gültige Normalenvektoren geben
                    // kommt bei programm.cdb vor
                    double um = (uKnots[0] + uKnots[uKnots.Length - 1]) / 2.0;
                    double vm = (vKnots[0] + vKnots[vKnots.Length - 1]) / 2.0;
                    double du = uKnots[uKnots.Length - 1] - uKnots[0];
                    double dv = vKnots[vKnots.Length - 1] - vKnots[0];
                    GeoPoint2D uuvv = uv;
                    if (uv.x > um) uuvv.x -= du / 1000;
                    else uuvv.x += du / 1000;
                    if (uv.y > vm) uuvv.y -= dv / 1000;
                    else uuvv.y += dv / 1000;
                    return GetNormal(uuvv);
                }
                normal.NormIfNotNull();
                return normal;
            }
            else
            {
                GeoPointH pnt, derivu, derivv;
                nurbs.SurfaceDeriv1(uv.x, uv.y, out pnt, out derivu, out derivv);
                GeoVector normal = (GeoVector)derivu ^ (GeoVector)derivv;
                double udiff = uKnots[uKnots.Length - 1] - uKnots[0];
                double vdiff = vKnots[vKnots.Length - 1] - vKnots[0];
                GeoVector n0 = normal;
                if (((GeoVector)derivu).Length < 1e-6 / udiff)
                {   // singulär in Richtung u, d.h. keine Änderung bei Änderung von u, d.h. für umin und umax die Richtung berechnen
                    // die hoffentlich nicht gleich sind
                    GeoPointH derivu1, derivv1, derivu2, derivv2;
                    nurbs.SurfaceDeriv1(uKnots[0], uv.y, out pnt, out derivu1, out derivv1);
                    nurbs.SurfaceDeriv1(uKnots[uKnots.Length - 1], uv.y, out pnt, out derivu2, out derivv2);
                    normal = (GeoVector)derivv2 ^ (GeoVector)derivv1; // die Reihenfolge scheint relevant zu sein
                }
                else if (((GeoVector)derivv).Length < 1e-6 / vdiff)
                {
                    GeoPointH derivu1, derivv1, derivu2, derivv2;
                    nurbs.SurfaceDeriv1(uv.x, vKnots[0], out pnt, out derivu1, out derivv1);
                    nurbs.SurfaceDeriv1(uv.x, vKnots[vKnots.Length - 1], out pnt, out derivu2, out derivv2);
                    normal = (GeoVector)derivu1 ^ (GeoVector)derivu2; // die Reihenfolge scheint relevant zu sein
                }
                if (normal.IsNullVector()) normal = n0;
                if (normal.IsNullVector())
                {   // die beiden aufspannenden Kurven des Nurbs gehen tangential ineinander über. beide Ableitungen haben die gleiche
                    // Richtung. Trotzdem gibt es einen Normalenvektor. Wenn die Fläche nicht komplett entartet ist, dann sollte es
                    // in der Nähe gültige Normalenvektoren geben
                    // kommt bei programm.cdb vor
                    double um = (uKnots[0] + uKnots[uKnots.Length - 1]) / 2.0;
                    double vm = (vKnots[0] + vKnots[vKnots.Length - 1]) / 2.0;
                    double du = uKnots[uKnots.Length - 1] - uKnots[0];
                    double dv = vKnots[vKnots.Length - 1] - vKnots[0];
                    GeoPoint2D uuvv = uv;
                    if (uv.x > um) uuvv.x -= du / 1000;
                    else uuvv.x += du / 1000;
                    if (uv.y > vm) uuvv.y -= dv / 1000;
                    else uuvv.y += dv / 1000;
                    return GetNormal(uuvv);
                }
                normal.NormIfNotNull();
                return normal;
            }
        }
        /// <summary>
        /// Overrides <see cref="CADability.GeoObject.ISurfaceImpl.ReverseOrientation ()"/>
        /// </summary>
        /// <returns></returns>
        public override ModOp2D ReverseOrientation()
        {   // die U-Richtung umdrehen, dann ist die Oberfläche andersrum orientiert
            int unum = poles.GetLength(0);
            for (int j = 0; j < poles.GetLength(1); ++j)
            {
                for (int i = 0; i < unum / 2; ++i)
                {
                    GeoPoint p = poles[i, j];
                    poles[i, j] = poles[unum - i - 1, j];
                    poles[unum - i - 1, j] = p;
                    if (weights != null)
                    {
                        double w = weights[i, j];
                        weights[i, j] = weights[unum - i - 1, j];
                        weights[unum - i - 1, j] = w;
                    }
                }
            }
            double umove = 2 * (uKnots[0] + uKnots[uKnots.Length - 1]) / 2; // die Verschiebung
            if (uKnots != null)
            {
                double[] newknots = new double[uKnots.Length];
                newknots[0] = uKnots[0]; // Anfang ist egal, der Bereich wird in sich selbst gespiegelt
                for (int i = 1; i < uKnots.Length; ++i)
                {
                    newknots[i] = newknots[i - 1] + (uKnots[uKnots.Length - i] - uKnots[uKnots.Length - i - 1]);
                }
                uKnots = newknots;
            }
            if (uMults != null) Array.Reverse(uMults);
            double ustartn = uKnots[0];
            if (uMinRestrict != uMaxRestrict)
            {
                double tmp = umove - uMaxRestrict;
                uMaxRestrict = umove - uMinRestrict;
                uMinRestrict = tmp;
            }
            InvalidateSecondaryData();
            // Init is fiddeling around with the knots, inserting extra knots when periodic. But here we already are ok with the knots
            // so we pretend not to be periodic and reset it afterwards
            bool wasUPeriodic = uPeriodic;
            bool wasVPeriodic = vPeriodic;
            if (uMults[0] > UDegree && vMults[0] > VDegree && uMults[uMults.Length - 1] > UDegree && vMults[vMults.Length - 1] > VDegree)
            {
                uPeriodic = vPeriodic = false;
            }
            Init();
            uPeriodic = wasUPeriodic;
            vPeriodic = wasVPeriodic;
            return new ModOp2D(-1, 0, umove, 0, 1, 0);

        }
        /// <summary>
        /// Overrides <see cref="CADability.GeoObject.ISurfaceImpl.Make3dCurve (ICurve2D)"/>
        /// </summary>
        /// <param name="curve2d"></param>
        /// <returns></returns>
        public override ICurve Make3dCurve(CADability.Curve2D.ICurve2D curve2d)
        {
            if (curve2d is Curve2DAspect)
            {
                ICurve res = (curve2d as Curve2DAspect).Get3DCurve(this);
                if (res != null) return res;
            }
            if (curve2d is ProjectedCurve pc)
            {
                if (pc.Surface is NurbsSurface)
                {
                    BoundingRect otherBounds = new BoundingRect(PositionOf(pc.Surface.PointAt(pc.StartPoint)), PositionOf(pc.Surface.PointAt(pc.EndPoint)));
                    if (pc.Surface.SameGeometry(pc.GetExtent(), this, otherBounds, Precision.eps, out ModOp2D notneeded))
                    {
                        return pc.Curve3DFromParams; // if trimmed or reversed still returns the correct 3d curve (but trimmed and/or reversed)
                    }
                }
            }
            if (curve2d is Line2D)
            {
                Line2D l2d = curve2d as Line2D;
                if (Math.Abs(l2d.StartPoint.x - l2d.EndPoint.x) < uSpan * 1e-6)
                {   // senkrechte Linie
                    if (Math.Abs(l2d.StartPoint.y - l2d.EndPoint.y) < vSpan * 1e-6) return null; // extrem kurze Linie
                    BSpline res = null;
                    double xx = l2d.StartPoint.x;
                    if (IsUPeriodic)
                    {
                        while (xx > uKnots[uKnots.Length - 1]) xx -= this.UPeriod;
                        while (xx < uKnots[0]) xx += this.UPeriod;
                    }
                    if (nurbs != null)
                    {
                        res = BSpline.Construct();
                        Nurbs<GeoPointH, GeoPointHPole> tmp = nurbs.FixedU(xx);
                        res.FromNurbs(tmp, vKnots[0], vKnots[vKnots.Length - 1]);
                    }
                    else if (nubs != null)
                    {
                        res = BSpline.Construct();
                        Nurbs<GeoPoint, GeoPointPole> tmp = nubs.FixedU(xx);
                        res.FromNurbs(tmp, vKnots[0], vKnots[vKnots.Length - 1]);
                    }
                    if (res.IsSingular) return null;
                    double vlen = vKnots[vKnots.Length - 1] - vKnots[0];
                    bool reverse = l2d.StartPoint.y > l2d.EndPoint.y;
                    double y0 = l2d.StartPoint.y;
                    double y1 = l2d.EndPoint.y;
                    if (this.IsVPeriodic)
                    {
                        while (y0 > vKnots[vKnots.Length - 1]) y0 -= this.VPeriod;
                        while (y1 > vKnots[vKnots.Length - 1]) y1 -= this.VPeriod;
                        while (y0 < vKnots[0]) y0 += this.VPeriod;
                        while (y1 < vKnots[0]) y1 += this.VPeriod;
                        // es kann sein, dass die Kurve über den Rand geht
                        if ((y1 - y0) * (l2d.EndPoint.y - l2d.StartPoint.y) < 0)
                        {   // über den Rand
                            y0 = l2d.StartPoint.y;
                            y1 = l2d.EndPoint.y;
                            int npoles = (int)Math.Ceiling(Math.Abs(y1 - y0) / vlen * vKnots.Length);
                            if (npoles < 3) npoles = 3;
                            double d = (y1 - y0) / (npoles - 1);
                            GeoPoint[] tp = new GeoPoint[npoles];
                            for (int i = 0; i < npoles; i++)
                            {
                                double y = y0 + i * d;
                                while (y > vKnots[vKnots.Length - 1]) y -= this.VPeriod;
                                while (y < vKnots[0]) y += this.VPeriod;
                                tp[i] = this.PointAt(new GeoPoint2D(l2d.StartPoint.x, y));
                            }
                            res.ThroughPoints(tp, this.vDegree, false);
                            return res;
                        }
                        else
                        {   // force inside the valid area
                            double ym = (l2d.StartPoint.y + l2d.EndPoint.y) / 2.0;
                            while (ym > vKnots[vKnots.Length - 1]) ym -= this.VPeriod;
                            while (ym < vKnots[0]) ym += this.VPeriod;
                            double dy = ym - (l2d.StartPoint.y + l2d.EndPoint.y) / 2.0;
                            y0 = Math.Min(Math.Max(l2d.StartPoint.y + dy, vKnots[0]), vKnots[vKnots.Length - 1]);
                            y1 = Math.Min(Math.Max(l2d.EndPoint.y + dy, vKnots[0]), vKnots[vKnots.Length - 1]);
                        }
                    }
                    if (reverse)
                    {
                        if (y0 == y1) return null;
                        (res as ICurve).Trim((y1 - vKnots[0]) / vlen, (y0 - vKnots[0]) / vlen);
                        (res as ICurve).Reverse();
                    }
                    else
                    {
                        if (y0 > y1)
                        {   // periodisch, die Kurve geht über die Naht
                        }
                        if (y0 == y1) return null;
                        (res as ICurve).Trim((y0 - vKnots[0]) / vlen, (y1 - vKnots[0]) / vlen);
                    }
                    if (res.IsSingular) return null;
                    if (res.degree == 1 && res.KnotPoints.Length == 2)
                    {   // besser eine Line, BSpline wird manchmal geklippt und Opencascade kann mit einem 
                        // geklpiiten BSpline mit degree1 und mehr als 2 Polen nicht umgehen
                        Line l = Line.Construct();
                        l.StartPoint = (res as ICurve).StartPoint;
                        l.EndPoint = (res as ICurve).EndPoint;
                        return l;
                    }
                    return res;
                }
                if (Math.Abs(l2d.StartPoint.y - l2d.EndPoint.y) < vSpan * 1e-6)
                {   // waagrechte Linie
                    BSpline res = null;
                    double yy = l2d.StartPoint.y;
                    if (IsVPeriodic)
                    {
                        while (yy > vKnots[vKnots.Length - 1]) yy -= this.VPeriod;
                        while (yy < vKnots[0]) yy += this.VPeriod;
                    }
                    if (nurbs != null)
                    {
                        res = BSpline.Construct();
                        Nurbs<GeoPointH, GeoPointHPole> tmp = nurbs.FixedV(yy);
                        res.FromNurbs(tmp, uKnots[0], uKnots[uKnots.Length - 1]);
                    }
                    else if (nubs != null)
                    {
                        res = BSpline.Construct();
                        Nurbs<GeoPoint, GeoPointPole> tmp = nubs.FixedV(yy);
                        res.FromNurbs(tmp, uKnots[0], uKnots[uKnots.Length - 1]);
                    }
                    if (res.IsSingular) return null;
                    double ulen = uKnots[uKnots.Length - 1] - uKnots[0];
                    bool reverse = l2d.StartPoint.x > l2d.EndPoint.x;
                    try
                    {
                        // ein Problem ist immer noch die volle Kurve bei periodischen NURBS:
                        // hier wird x0 und x1 identisch. Stattdessen müsste man den ganzen Bereich nehmen
                        // es ist noch schlimmer: Kurven dürfen nicht über die periodische Grenze gehen
                        // sonst müsste man zwei Kurven machen
                        double x0 = l2d.StartPoint.x;
                        double x1 = l2d.EndPoint.x;
                        if (this.IsUPeriodic)
                        {
                            while (x0 > uKnots[uKnots.Length - 1]) x0 -= this.UPeriod;
                            while (x1 > uKnots[uKnots.Length - 1]) x1 -= this.UPeriod;
                            while (x0 < uKnots[0]) x0 += this.UPeriod;
                            while (x1 < uKnots[0]) x1 += this.UPeriod;
                            // es kann sein, dass die Kurve über den Rand geht
                            if ((x1 - x0) * (l2d.EndPoint.x - l2d.StartPoint.x) < 0)
                            {   // über den Rand
                                x0 = l2d.StartPoint.x;
                                x1 = l2d.EndPoint.x;
                                int npoles = (int)Math.Ceiling(Math.Abs(x1 - x0) / ulen * uKnots.Length);
                                if (npoles < 3) npoles = 3;
                                double d = (x1 - x0) / (npoles - 1);
                                GeoPoint[] tp = new GeoPoint[npoles];
                                for (int i = 0; i < npoles; i++)
                                {
                                    double x = x0 + i * d;
                                    while (x > uKnots[uKnots.Length - 1]) x -= this.UPeriod;
                                    while (x < uKnots[0]) x += this.UPeriod;
                                    tp[i] = this.PointAt(new GeoPoint2D(x, l2d.StartPoint.y));
                                }
                                res.ThroughPoints(tp, this.uDegree, false);
                                return res;
                            }
                            else
                            {   // force inside the valid area
                                double xm = (l2d.StartPoint.x + l2d.EndPoint.x) / 2.0;
                                while (xm > uKnots[uKnots.Length - 1]) xm -= this.UPeriod;
                                while (xm > uKnots[uKnots.Length - 1]) xm -= this.UPeriod;
                                double dx = xm - (l2d.StartPoint.x + l2d.EndPoint.x) / 2.0;
                                x0 = Math.Min(Math.Max(l2d.StartPoint.x + dx, uKnots[0]), uKnots[uKnots.Length - 1]);
                                x1 = Math.Min(Math.Max(l2d.EndPoint.x + dx, uKnots[0]), uKnots[uKnots.Length - 1]);
                            }
                        }
                        if (reverse)
                        {
                            if (x1 < x0) (res as ICurve).Trim((x1 - uKnots[0]) / ulen, (x0 - uKnots[0]) / ulen);
                            (res as ICurve).Reverse();
                        }
                        else
                        {
                            if (x1 > x0) (res as ICurve).Trim((x0 - uKnots[0]) / ulen, (x1 - uKnots[0]) / ulen);
                        }
                    }
                    catch (Exception e)
                    {
                        if (e is ThreadAbortException) throw (e);
                        return null; // Notbremse, wenn die Linie außerhalb der Nurbsfläche in u/v liegt
                    }
                    if (res.IsSingular) return null; // es gibt flächen, die sind nur stückweise singulär
                    if (res.degree == 1 && res.KnotPoints.Length == 2)
                    {   // besser eine Line, BSpline wird manchmal geklippt und Opencascade kann mit einem 
                        // geklpiiten BSplint mit degree1 und mehr als 2 Polsen nicht umgehen
                        Line l = Line.Construct();
                        l.StartPoint = (res as ICurve).StartPoint;
                        l.EndPoint = (res as ICurve).EndPoint;
                        return l;
                    }
                    // z.B. in MatriceB37Modello.stp
                    return res;
                }
                else
                {   // schräge Linie: alle uknots und vknots Schnittpunkte finden und aus den Durchgangspunkten
                    // (mit vorgegebenem Knotenvektor aus uv Abständen) einen BSpline machen
                    // Wird aber nicht gut, die Längen der Richtungen sind wohl ein Problem und bei rationalem BSplne
                    // stimmts sowieso nicht
                    int numpnts = 4;
                    while (true) // Abbruch innerhalb
                    {
                        GeoPoint[] pnts = new GeoPoint[numpnts];
                        for (int i = 0; i < numpnts; ++i)
                        {
                            pnts[i] = PointAt(l2d.PointAt(i * 1.0 / (numpnts - 1)));
                        }
                        BSpline bsp = BSpline.Construct();
                        bsp.ThroughPoints(pnts, Math.Max(uDegree, VDegree), false);
                        double error = 0.0;
                        for (int i = 0; i < numpnts - 1; ++i)
                        {
                            GeoPoint pbsp = (bsp as ICurve).PointAt((i + 0.5) * 1.0 / (numpnts - 1));
                            error = pbsp | PointAt(PositionOf(pbsp));
                            if (error > Precision.eps * 1000)
                            {
                                numpnts *= 2;
                                break;
                            }
                        }
                        if (error > Precision.eps * 1000 && numpnts < 1000) continue;
                        return bsp;
                    }
                    //List<GeoPoint2D> uvpnts = new List<GeoPoint2D>();
                    //double ulen = uKnots[uKnots.Length - 1] - uKnots[0];
                    //double vlen = vKnots[vKnots.Length - 1] - vKnots[0];
                    //uvpnts.Add(l2d.StartPoint);
                    //uvpnts.Add(l2d.EndPoint);
                    //for (int i = 0; i < uKnots.Length; ++i)
                    //{
                    //    if (inbetween(uKnots[i], l2d.StartPoint.x, l2d.EndPoint.x))
                    //    {
                    //        uvpnts.Add(new GeoPoint2D(uKnots[i], vKnots[0] + (uKnots[i] - l2d.StartPoint.x) / ulen * vlen));
                    //    }
                    //}
                    //for (int i = 0; i < vKnots.Length; ++i)
                    //{
                    //    if (inbetween(vKnots[i], l2d.StartPoint.y, l2d.EndPoint.y))
                    //    {
                    //        uvpnts.Add(new GeoPoint2D(uKnots[0] + (vKnots[i] - l2d.StartPoint.y) / vlen * ulen, vKnots[i]));
                    //    }
                    //}
                    //if (l2d.StartPoint.x < l2d.EndPoint.x)
                    //{
                    //    uvpnts.Sort(delegate(GeoPoint2D first, GeoPoint2D second) { return first.x.CompareTo(second.x); });
                    //}
                    //else
                    //{
                    //    uvpnts.Sort(delegate(GeoPoint2D first, GeoPoint2D second) { return second.x.CompareTo(first.x); });
                    //}
                    //for (int i = uvpnts.Count - 2; i >= 0; --i)
                    //{
                    //    if ((uvpnts[i] | uvpnts[i + 1]) < (ulen + vlen) * 1e-6) uvpnts.RemoveAt(i + 1);
                    //}
                    //if (uvpnts.Count < 2) return null;
                    //BSpline bsp = BSpline.Construct();
                    //GeoPoint[] tp = new GeoPoint[uvpnts.Count];
                    //GeoVector[] dirs = new GeoVector[uvpnts.Count];
                    //double dx = (l2d.EndPoint.x - l2d.StartPoint.x);
                    //double dy = (l2d.EndPoint.y - l2d.StartPoint.y);
                    //for (int i = 0; i < tp.Length; ++i)
                    //{
                    //    tp[i] = PointAt(uvpnts[i]);
                    //    GeoVector udir = UDirection(uvpnts[i]);
                    //    GeoVector vdir = VDirection(uvpnts[i]);
                    //    dirs[i] = dx / ulen * udir + dy / vlen * vdir;
                    //    //dirs[i].Norm();
                    //}
                    //bsp.ThroughPoints(tp, dirs, Math.Max(uDegree, VDegree), false);
                    //return bsp;
                }
            }
            if (curve2d is Polyline2D)
            {   // eine Polylinie ist eckig, das Ergebnis muss auch eckig sein, zuvor wurde hier ein BSpline erzeugt, was 
                // komische Kanten machte
                Polyline2D p2d = (curve2d as Polyline2D);
                Path res = Path.Construct();
                List<ICurve> curves = new List<ICurve>();
                for (int i = 0; i < p2d.VertexCount - 1; ++i)
                {
                    ICurve crv = Make3dCurve(new Line2D(p2d.GetVertex(i), p2d.GetVertex(i + 1)));
                    if (crv != null) curves.Add(crv);
                }
                res.Set(curves.ToArray());
                return res;
            }
            if (curve2d is BSpline2D)
            {
                BSpline2D b2d = (curve2d as BSpline2D);
                BSpline res = BSpline.Construct();
                // GeoPoint[] points = new GeoPoint[b2d.Knots.Length];
                List<GeoPoint> points = new List<GeoPoint>(b2d.Knots.Length);
                //GeoPoint2D[] dbg = new GeoPoint2D[b2d.Knots.Length];
                //GeoVector[] dirs = new GeoVector[b2d.Knots.Length];
                for (int i = 0; i < b2d.Knots.Length; ++i)
                {
                    GeoPoint2D pos2d;
                    GeoVector2D dir2d;
                    b2d.PointDerAt(b2d.Knots[i], out pos2d, out dir2d);
                    //dbg[i] = pos2d;
                    GeoPoint p = PointAt(pos2d);
                    if (i == 0 || !Precision.IsEqual(points[points.Count - 1], p)) points.Add(p);
                    //dirs[i] = dir2d.x * UDirection(pos2d) + dir2d.y * VDirection(pos2d);
                }
                // mit Richtungen wird nicht gut
                // bei hohem Grad wirds hier schlecht, siehe "piece0.stp"
                // Hier muss unbedingt mit Genauigkeit gearbeitet werden
                // Adaptive Methode wäre gefragt, oder Start- und Endrichtung
                double len = b2d.Length;
                bool closed = (PointAt(b2d.StartPoint) | PointAt(b2d.EndPoint)) < len * 1e-3;
                if (res.ThroughPoints(points.ToArray(), 3, closed)) // 3 und false ist noch willkürlich
                {
                    // zumindest in MatriceB37Modello.stp gibts probleme wenn man OCas machen lässt (das return res war auskommentiert)
                    return res;
                }
            }
            return base.Make3dCurve(curve2d);
        }
        private bool inbetween(double toTest, double b1, double b2)
        {
            if (b1 < b2) return toTest > b1 && toTest < b2;
            else return toTest < b1 && toTest > b2;
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
        {
            // An den "Wendepunkten" muss geteilt werden.
            // Die Idee: betrachte die Reihe der Pole zu einem festem u bzw. v
            // Ist die "konvex", dann ist alles ok, geht es aber "rauf und runter", dann muss geteilt werden
            // Betrachtet man nuun 4 Pole, dann bestimmen die ersten 3 zwei Vektoren, deren Kreuzprodukt in eine gewisse Richtung zeigt
            // die letzten 3 ebenso. Wenn diese Pole einen Wendepunkt darstellen, dann zeigen die Kreuzprodukte in verschiedene Richtung
            // ansonsten in doe gleiche Richtung (Vorzeichen des Skalarproduktes). 
            //List<int> usplit = new List<int>();
            //List<int> vsplit = new List<int>();
            //for (int j = 0; j < poles.GetLength(1); j++)
            //{
            //    GeoVector n0 = GeoVector.NullVector;
            //    int sgn0 = 0;
            //    for (int i = 0; i < poles.GetLength(0); i++)
            //    {
            //        if (i == 0)
            //        {
            //            n0 = (poles[i + 1, j] - poles[i, j]) ^ (poles[i + 2, j] - poles[i + 1, j]);
            //        }
            //        else
            //        {
            //            GeoVector n1 = (poles[i + 1, j] - poles[i, j]) ^ (poles[i + 2, j] - poles[i + 1, j]);
            //            int sgn1 = Math.Sign(n0 * n1);
            //            if (i > 1 && sgn1 != sgn0) usplit.Add(i);
            //            n0 = n1;
            //            sgn0 = sgn1;
            //        }
            //    }
            //}
            double uprec = (umax - umin) * 1e-6;
            double vprec = (vmax - vmin) * 1e-6;
            List<double> uSteps = new List<double>();
            List<double> vSteps = new List<double>();
            uSteps.Add(umin);
            for (int i = 0; i < uKnots.Length; ++i)
            {
                if (uKnots[i] > umin + uprec && uKnots[i] < umax - uprec) uSteps.Add(uKnots[i]);
            }
            uSteps.Add(umax);
            if (uSteps.Count == 2 && uDegree > 1) uSteps.Insert(1, (uSteps[0] + uSteps[1]) / 2.0);
            if (uSteps.Count <= 4 && uPeriodic)
            {
                uSteps.Clear();
                uSteps.Add(umin);
                uSteps.Add(umin + (umax - umin) * 0.25);
                uSteps.Add(umin + (umax - umin) * 0.5);
                uSteps.Add(umin + (umax - umin) * 0.75);
                uSteps.Add(umax);
            }
            vSteps.Add(vmin);
            for (int i = 0; i < vKnots.Length; ++i)
            {
                if (vKnots[i] > vmin + vprec && vKnots[i] < vmax - vprec) vSteps.Add(vKnots[i]);
            }
            vSteps.Add(vmax);
            if (vSteps.Count == 2 && vDegree > 1) vSteps.Insert(1, (vSteps[0] + vSteps[1]) / 2.0);
            if (vSteps.Count <= 4 && vPeriodic)
            {
                vSteps.Clear();
                vSteps.Add(vmin);
                vSteps.Add(vmin + (vmax - vmin) * 0.25);
                vSteps.Add(vmin + (vmax - vmin) * 0.5);
                vSteps.Add(vmin + (vmax - vmin) * 0.75);
                vSteps.Add(vmax);
            }
            intu = uSteps.ToArray();
            intv = vSteps.ToArray();
        }
        public override BoundingCube GetPatchExtent(BoundingRect uvPatch, bool rough)
        {
            if (rough && uvPatch.Left == UKnots[0] && uvPatch.Right == UKnots[UKnots.Length - 1] && uvPatch.Bottom == VKnots[0] && uvPatch.Top == VKnots[VKnots.Length - 1])
            {   // die Pole geben eine Hülle vor
                BoundingCube res = BoundingCube.EmptyBoundingCube;
                for (int i = 0; i < poles.GetLength(0); i++)
                {
                    for (int j = 0; j < poles.GetLength(1); j++)
                    {
                        res.MinMax(poles[i, j]);
                    }
                }
                return res;
            }
            return base.GetPatchExtent(uvPatch, rough);
        }
        /// <summary>
        /// Overrides <see cref="CADability.GeoObject.ISurfaceImpl.CopyData (ISurface)"/>
        /// </summary>
        /// <param name="CopyFrom"></param>
        public override void CopyData(ISurface CopyFrom)
        {
            NurbsSurface cc = CopyFrom as NurbsSurface;
            if (cc != null)
            {
                this.poles = (GeoPoint[,])cc.poles.Clone();
                if (cc.weights != null) this.weights = (double[,])cc.weights.Clone();
                else this.weights = null;
                this.uKnots = (double[])cc.uKnots.Clone();
                this.vKnots = (double[])cc.vKnots.Clone();
                this.uMults = (int[])cc.uMults.Clone();
                this.vMults = (int[])cc.vMults.Clone();
                this.uDegree = cc.uDegree;
                this.vDegree = cc.vDegree;
                this.uPeriodic = cc.uPeriodic;
                this.vPeriodic = cc.vPeriodic;
                this.upoles = cc.upoles;
                this.vpoles = cc.vpoles;
                nubs = null;
                nurbs = null;
                InvalidateSecondaryData();
                Init();
            }
        }
        public override bool IsUPeriodic
        {
            get
            {
                return uPeriodic;
            }
        }
        public override bool IsVPeriodic
        {
            get
            {
                return vPeriodic;
            }
        }
        /// <summary>
        /// Overrides <see cref="CADability.GeoObject.ISurfaceImpl.GetUSingularities ()"/>
        /// </summary>
        /// <returns></returns>
        public override double[] GetUSingularities()
        {
            // we only look for singularities at the Endpoints of the u/v mesh. In theory there could be singularities in between,
            // we could find them by intersecting fixed v curves in 3d
            double[] us = null;
            double precision = PolesExtent.Size * 1e-6; // there was a problem with a nurbs surface having singularities depending on scaling
            try
            {
                if (uSingularities != null)
                {
                    us = uSingularities.Target as double[];
                    if (us != null) return us;
                }
            }
            catch { }
            int umax = poles.GetLength(0);
            int vmax = poles.GetLength(1);
            List<double> res = new List<double>();
            bool equal = true;
            for (int j = 0; j < vmax - 1; j++)
            {
                if ((poles[0, j] | poles[0, j + 1]) > precision)
                {
                    equal = false;
                    break;
                }
            }
            if (equal) res.Add(uKnots[0]);
            equal = true;
            for (int j = 0; j < vmax - 1; j++)
            {
                if ((poles[umax - 1, j] | poles[umax - 1, j + 1]) > precision)
                {
                    equal = false;
                    break;
                }
            }
            if (equal) res.Add(uKnots[uKnots.Length - 1]);
            us = res.ToArray();
            uSingularities = new WeakReference(us);
            return us;
        }
        /// <summary>
        /// Overrides <see cref="CADability.GeoObject.ISurfaceImpl.GetVSingularities ()"/>
        /// </summary>
        /// <returns></returns>
        public override double[] GetVSingularities()
        {
            double[] vs = null;
            double precision = PolesExtent.Size * 1e-6; // there was a problem with a nurbs surface having singularities depending on scaling
            try
            {
                if (vSingularities != null)
                {
                    vs = vSingularities.Target as double[];
                    if (vs != null) return vs;
                }
            }
            catch { }
            int umax = poles.GetLength(0);
            int vmax = poles.GetLength(1);
            List<double> res = new List<double>();
            bool equal = true;
            for (int i = 0; i < umax - 1; i++)
            {
                if ((poles[i, 0] | poles[i + 1, 0]) > precision)
                {
                    equal = false;
                    break;
                }
            }
            if (equal) res.Add(vKnots[0]);
            equal = true;
            for (int i = 0; i < umax - 1; i++)
            {
                if ((poles[i, vmax - 1] | poles[i + 1, vmax - 1]) > precision)
                {
                    equal = false;
                    break;
                }
            }
            if (equal) res.Add(vKnots[vKnots.Length - 1]);
            vs = res.ToArray();
            vSingularities = new WeakReference(vs);
            return vs;
        }
        /// <summary>
        /// Overrides <see cref="CADability.GeoObject.ISurfaceImpl.GetPlaneIntersection (PlaneSurface, double, double, double, double, double)"/>
        /// </summary>
        /// <param name="pl"></param>
        /// <param name="umin"></param>
        /// <param name="umax"></param>
        /// <param name="vmin"></param>
        /// <param name="vmax"></param>
        /// <param name="precision"></param>
        /// <returns></returns>
        public override IDualSurfaceCurve[] GetPlaneIntersection(PlaneSurface pl, double umin, double umax, double vmin, double vmax, double precision)
        {   // das muss später nach ISurfaceImpl...
#if DEBUG
            DebuggerContainer dc = new DebuggerContainer();
            for (int i = 0; i < uKnots.Length; ++i)
            {
                dc.Add(FixedU(uKnots[i]));
            }
            for (int i = 0; i < vKnots.Length; ++i)
            {
                dc.Add(FixedV(vKnots[i]));
            }
#endif
            IDualSurfaceCurve[] res = BoxedSurfaceEx.GetPlaneIntersection(pl, umin, umax, vmin, vmax, precision);
            //IDualSurfaceCurve[] res = boxedSurface.GetPlaneIntersection(pl, umin, umax, vmin, vmax, precision);
            return res;
        }
        /// <summary>
        /// Overrides <see cref="CADability.GeoObject.ISurfaceImpl.GetLineIntersection (GeoPoint, GeoVector)"/>
        /// </summary>
        /// <param name="startPoint"></param>
        /// <param name="direction"></param>
        /// <returns></returns>
        public override GeoPoint2D[] GetLineIntersection(GeoPoint startPoint, GeoVector direction)
        {
            GeoPoint2D[] res = BoxedSurfaceEx.GetLineIntersection(startPoint, direction);
            return res;
        }
        /// <summary>
        /// Overrides <see cref="CADability.GeoObject.ISurfaceImpl.GetNaturalBounds (out double, out double, out double, out double)"/>
        /// </summary>
        /// <param name="umin"></param>
        /// <param name="umax"></param>
        /// <param name="vmin"></param>
        /// <param name="vmax"></param>
        public override void GetNaturalBounds(out double umin, out double umax, out double vmin, out double vmax)
        {
            umin = uKnots[0];
            umax = uKnots[uKnots.Length - 1];
            vmin = vKnots[0];
            vmax = vKnots[vKnots.Length - 1];
            // uMinRestrict etc: this comes from ImportStep: when nurbs surfaces are closed in u or v, they are 
            // (sometimes, always?) overlapping. Parts at the overlapping ends should not be used
            if (uMinRestrict != uMaxRestrict)
            {
                umin = uMinRestrict;
                umax = uMaxRestrict;
            }
            if (vMinRestrict != vMaxRestrict)
            {
                vmin = vMinRestrict;
                vmax = vMaxRestrict;
            }
        }
        /// <summary>
        /// Overrides <see cref="CADability.GeoObject.ISurfaceImpl.GetSaveUSteps ()"/>
        /// </summary>
        /// <returns></returns>
        protected override double[] GetSaveUSteps()
        {
            return uKnots;
        }
        /// <summary>
        /// Overrides <see cref="CADability.GeoObject.ISurfaceImpl.GetSaveVSteps ()"/>
        /// </summary>
        /// <returns></returns>
        protected override double[] GetSaveVSteps()
        {
            return vKnots;
        }
        class UVPair
        {
            public double u, v;
            public UVPair()
            {
                v = u = double.MaxValue;
            }
        }
        /// <summary>
        /// Overrides <see cref="CADability.GeoObject.ISurfaceImpl.GetExtrema ()"/>
        /// </summary>
        /// <returns></returns>
        public override GeoPoint2D[] GetExtrema()
        {
            List<GeoPoint2D> res = new List<GeoPoint2D>();
            // Einfach das durch FixedU und FixedV gegebene Netz verwenden. Die Kurven werden 
            // mit einer WeakReference gehalten
            if (nubs == null && nurbs == null) Init(); // manchmal nötig, da währen des deserialisierens nich nicht initialisiert
            double[] us = GetUSingularities();
            double[] vs = GetVSingularities();
            foreach (GeoVector dir in GeoVector.MainAxis)
            {
                Dictionary<Pair<int, int>, UVPair> mesh = new Dictionary<Pair<int, int>, UVPair>();
                // Dieses Dictinary enthält als key den Index einer Masche, als value den u und v Wert, bei dem sich ein Maximum befindet
                // Möglicherweise wird der u bzw v Wert auch einmal überschrieben, das sollte aber keine Rolle spielen
                for (int i = 0; i < vKnots.Length; ++i)
                {
                    bool isSingular = false;
                    for (int ii = 0; ii < vs.Length; ii++)
                    {
                        if (vs[ii] == vKnots[i]) // singularities are always knots
                        {
                            isSingular = true;
                            break;
                        }
                    }
                    if (isSingular) continue;
                    double[] ex = (FixedV(vKnots[i]) as ICurve).GetExtrema(dir);
                    for (int k = 0; k < ex.Length; ++k)
                    {
                        double u = uKnots[0] + ex[k] * (uKnots[uKnots.Length - 1] - uKnots[0]);
                        int ui = -1;
                        for (int j = 0; j < uKnots.Length; ++j)
                        {
                            if (uKnots[j] > u)
                            {
                                ui = j - 1;
                                break;
                            }
                        }
                        if (ui >= 0)
                        {
                            if (!mesh.ContainsKey(new Pair<int, int>(ui, i)))
                            {
                                mesh[new Pair<int, int>(ui, i)] = new UVPair();
                            }
                            mesh[new Pair<int, int>(ui, i)].u = u;
                            if (i > 0)
                            {
                                if (!mesh.ContainsKey(new Pair<int, int>(ui, i - 1)))
                                {
                                    mesh[new Pair<int, int>(ui, i - 1)] = new UVPair();
                                }
                                mesh[new Pair<int, int>(ui, i - 1)].u = u;
                            }
                        }
                    }
                }
                for (int i = 0; i < uKnots.Length; ++i)
                {
                    bool isSingular = false;
                    for (int ii = 0; ii < us.Length; ii++)
                    {
                        if (us[ii] == uKnots[i])
                        {
                            isSingular = true;
                            break;
                        }
                    }
                    if (isSingular) continue;
                    double[] ex = (FixedU(uKnots[i]) as ICurve).GetExtrema(dir);
                    for (int k = 0; k < ex.Length; ++k)
                    {
                        double v = vKnots[0] + ex[k] * (vKnots[vKnots.Length - 1] - vKnots[0]);
                        int vi = -1;
                        for (int j = 0; j < vKnots.Length; ++j)
                        {
                            if (vKnots[j] > v)
                            {
                                vi = j - 1;
                                break;
                            }
                        }
                        if (vi >= 0)
                        {
                            if (mesh.ContainsKey(new Pair<int, int>(i, vi)))
                            {   // wenns noch keinen Eintrag mit festem v gibt, interessiert auch kein u-Eintrag
                                mesh[new Pair<int, int>(i, vi)].v = v;
                            }
                            if (i > 0)
                            {
                                if (mesh.ContainsKey(new Pair<int, int>(i - 1, vi)))
                                {
                                    mesh[new Pair<int, int>(i - 1, vi)].v = v;
                                }
                            }
                        }
                    }
                }
                foreach (KeyValuePair<Pair<int, int>, UVPair> kv in mesh)
                {
                    if (kv.Value.v != double.MaxValue)
                    {
                        // Iterieren um ein Maximum in der Masche zu finden
                        GeoPoint2D extr;
                        if (FindExtremum(dir, uKnots[kv.Key.First], uKnots[kv.Key.First + 1], vKnots[kv.Key.Second], vKnots[kv.Key.Second + 1], kv.Value.u, kv.Value.v, out extr))
                        {
                            res.Add(extr);
                        }
                    }
                }
            }
            return res.ToArray();
        }
        /// <summary>
        /// Overrides <see cref="CADability.GeoObject.ISurfaceImpl.HitTest (BoundingCube, out GeoPoint2D)"/>
        /// </summary>
        /// <param name="cube"></param>
        /// <param name="uv"></param>
        /// <returns></returns>
        public override bool HitTest(BoundingCube cube, out GeoPoint2D uv)
        {
            return BoxedSurfaceEx.HitTest(cube, out uv);
        }
        private bool FindExtremum(GeoVector dir, double umin, double umax, double vmin, double vmax, double u, double v, out GeoPoint2D extr)
        {   // u und v liegen innerhalb der Masche umin,vmin...umax,vmax
            // bei u hatte eine fixedv Kurve ein Extremum, bei v analog
            //GeoVector n1 = GetNormal(new GeoPoint2D(umin, vmin));
            //GeoVector n2 = GetNormal(new GeoPoint2D(umin, vmax));
            //GeoVector n3 = GetNormal(new GeoPoint2D(umax, vmin));
            //GeoVector n4 = GetNormal(new GeoPoint2D(umax, vmax));
            //bool ok = false;
            //GeoVector v1 = Geometry.ReBaseX(dir, n2, n3, n4);
            //if (v1.x > 0 && v1.y > 0 && v1.z > 0 && v1.x < 1 && v1.y < 1 && v1.z < 1) ok = true;
            //else
            //{
            //    GeoVector v2 = Geometry.ReBaseX(dir, n1, n3, n4);
            //    if (v2.x > 0 && v2.y > 0 && v2.z > 0 && v2.x < 1 && v2.y < 1 && v2.z < 1) ok = true;
            //    else
            //    {
            //        GeoVector v3 = Geometry.ReBaseX(dir, n1, n2, n4);
            //        if (v3.x > 0 && v3.y > 0 && v3.z > 0 && v3.x < 1 && v3.y < 1 && v3.z < 1) ok = true;
            //        else
            //        {
            //            GeoVector v4 = Geometry.ReBaseX(dir, n1, n2, n3);
            //            if (v4.x > 0 && v4.y > 0 && v4.z > 0 && v4.x < 1 && v4.y < 1 && v4.z < 1) ok = true;
            //        }
            //    }
            //}
            //// we can exclude those surface patches, where the required dir, where we try to find a maximum or minimum, is not in the quadrant
            //// of three of the four normal vectors. At least well behaving surfaces will follow this rule
            //if (!ok)
            //{
            //    extr = GeoPoint2D.Invalid;
            //    return false;
            //}
            extr = new GeoPoint2D(u, v);
            double error = GaussNewtonMinimizer.SurfaceExtrema(this, new BoundingRect(umin, vmin, umax, vmax), dir, ref extr);
            return error < 1e-6;

//            extr = GeoPoint2D.Origin;
//            GeoPoint p1 = PointAt(new GeoPoint2D(u, v));
//            GeoPoint p2 = p1;
//            double mindist = double.MaxValue;
//            int dbgn = 0;
//            while (mindist > Precision.eps)
//            {
//                if (dbgn > 1000) return false; // muss noch genauer untersucht werden
//                ++dbgn;
//                bool foundU = false;
//                bool foundV = false;
//                BSpline fixedu = FixedU(u);
//                BSpline fixedv = FixedV(v);
//                if (!fixedv.IsSingular)
//                {
//                    double[] exu = (fixedv as ICurve).GetExtrema(dir);
//                    for (int k = 0; k < exu.Length; ++k)
//                    {
//                        double uex = uKnots[0] + exu[k] * (uKnots[uKnots.Length - 1] - uKnots[0]);
//                        if (uex > umin && uex < umax)
//                        {
//                            p1 = fixedv.PointAtParam(uex);
//                            u = uex;
//                            foundU = true;
//                            break;
//                        }
//                    }
//                }
//                if (!fixedu.IsSingular)
//                {
//                    double[] exv = (fixedu as ICurve).GetExtrema(dir);
//                    for (int k = 0; k < exv.Length; ++k)
//                    {
//                        double vex = vKnots[0] + exv[k] * (vKnots[vKnots.Length - 1] - vKnots[0]);
//                        if (vex > vmin && vex < vmax)
//                        {
//                            p2 = fixedu.PointAtParam(vex);
//                            v = vex;
//                            foundV = true;
//                            break;
//                        }
//                    }
//                }
//                //if ((!foundU || !foundV) && exv.Length > 0 && exu.Length > 0)
//                //{
//                //    // das ist der Fall, dass man beim Iterieren aus dem Patch hinausläuft
//                //    // noch kein solcher Fall bekannt
//                //    // man müsste entscheiden, ob man solche Werte auch zulässt und die Bedingung müsste
//                //    // nicht lauten ob innerhalb von min und max sondern die nächstgelegene Lösung
//                //}
//                if (!foundU || !foundV) return false; // kein passendes Extremum gefunden
//                double d = p1 | p2;
//                if (d >= mindist) return false; // konvergiert nicht
//                mindist = d;
//            }
//            extr = new GeoPoint2D(u, v);
//#if DEBUG
//            double ddd = extr | uvse;
//            if (ddd > 1e-6) { }
//#endif
//            GeoPoint dbg = PointAt(extr);
//            return true;
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
            // man müsste zuerst kanonische Formen probieren, denn ebene Flächen und extrusions oder rotationsflächen
            // können auch identisch sein wenn sie gegeneinander verschoben sind
            if (other is NurbsSurface)
            {
                NurbsSurface nother = other as NurbsSurface;
                // zuerst der wahrscheinliche Fall, dass gleiche Pole u.s.w. vorhanden sind
                bool same = true;
                if (uDegree == nother.uDegree && vDegree == nother.vDegree && poles.GetLength(0) == nother.poles.GetLength(0) && poles.GetLength(1) == nother.poles.GetLength(1))
                {
                    // gleicher Grad und gleiche Polzahl, kann jetzt noch verschiedene Richtung haben
                    int imax = poles.GetLength(0);
                    int jmax = poles.GetLength(1);
                    for (int i = 0; i < imax; i++)
                    {
                        for (int j = 0; j < jmax; j++)
                        {
                            if ((poles[i, j] | nother.poles[i, j]) > precision)
                            {
                                same = false;
                                break;
                            }
                        }
                        if (!same) break;
                    }
                    if (same)
                    {
                        firstToSecond = ModOp2D.Identity;
                        return true;
                    }
                    same = true; // 2. Versuch: verdrehtes v
                    for (int i = 0; i < poles.GetLength(0); i++)
                    {
                        for (int j = 0; j < poles.GetLength(1); j++)
                        {
                            if (!Precision.IsEqual(poles[i, j], nother.poles[i, jmax - 1 - j]))
                            {
                                same = false;
                                break;
                            }
                        }
                        if (!same) break;
                    }
                    if (same)
                    {
                        firstToSecond = new ModOp2D(1, 0, 0, 0, -1, vKnots[vKnots.Length - 1] - vKnots[0]); // noch nicht getestet
                        return true;
                    }
                    same = true; // 2. Versuch: verdrehtes u
                    for (int i = 0; i < poles.GetLength(0); i++)
                    {
                        for (int j = 0; j < poles.GetLength(1); j++)
                        {
                            if (!Precision.IsEqual(poles[i, j], nother.poles[imax - 1 - i, j]))
                            {
                                same = false;
                                break;
                            }
                        }
                        if (!same) break;
                    }
                    if (same)
                    {
                        firstToSecond = new ModOp2D(-1, 0, uKnots[uKnots.Length - 1] - uKnots[0], 0, 1, 0); // noch nicht getestet
                        return true;
                    }
                }

                // the following doesnt return false, when the surfaces share a common edge
                //firstToSecond = ModOp2D.Null;
                //GeoPoint pt = PointAt(thisBounds.GetLowerLeft());
                //GeoPoint po = other.PointAt(other.PositionOf(pt));
                //if ((pt | po) > precision) return false;
                //pt = PointAt(thisBounds.GetLowerRight());
                //po = other.PointAt(other.PositionOf(pt));
                //if ((pt | po) > precision) return false;
                //pt = PointAt(thisBounds.GetUpperLeft());
                //po = other.PointAt(other.PositionOf(pt));
                //if ((pt | po) > precision) return false;
                //pt = PointAt(thisBounds.GetUpperRight());
                //po = other.PointAt(other.PositionOf(pt));
                //if ((pt | po) > precision) return false;

                //po = other.PointAt(otherBounds.GetLowerLeft());
                //pt = PointAt(PositionOf(po));
                //if ((pt | po) > precision) return false;
                //po = other.PointAt(otherBounds.GetLowerRight());
                //pt = PointAt(PositionOf(po));
                //if ((pt | po) > precision) return false;
                //po = other.PointAt(otherBounds.GetUpperLeft());
                //pt = PointAt(PositionOf(po));
                //if ((pt | po) > precision) return false;
                //po = other.PointAt(otherBounds.GetUpperRight());
                //pt = PointAt(PositionOf(po));
                //if ((pt | po) > precision) return false;

                // keine Übereinstimmung der Pole
                firstToSecond = ModOp2D.Null;
                return false;
            }
            // vielleicht liegt getrimmte Fläche oder so vor
            return base.SameGeometry(thisBounds, other, otherBounds, precision, out firstToSecond);
        }
        /// <summary>
        /// Overrides <see cref="CADability.GeoObject.ISurfaceImpl.Intersect (BoundingRect, ISurface, BoundingRect)"/>
        /// </summary>
        /// <param name="thisBounds"></param>
        /// <param name="other"></param>
        /// <param name="otherBounds"></param>
        /// <returns></returns>
        public override ICurve[] Intersect(BoundingRect thisBounds, ISurface other, BoundingRect otherBounds)
        {
            // testweise:
            IDualSurfaceCurve[] dsc = BoxedSurfaceEx.GetSurfaceIntersection(other, otherBounds.Left, otherBounds.Right, otherBounds.Bottom, otherBounds.Top, Precision.eps);
            ICurve[] res = new ICurve[dsc.Length];
            for (int i = 0; i < res.Length; i++)
            {
                res[i] = dsc[i].Curve3D;
            }
            return res;
        }
        /// <summary>
        /// Overrides <see cref="CADability.GeoObject.ISurfaceImpl.Intersect (ICurve, BoundingRect, out GeoPoint[], out GeoPoint2D[], out double[])"/>
        /// </summary>
        /// <param name="curve"></param>
        /// <param name="uvExtent"></param>
        /// <param name="ips"></param>
        /// <param name="uvOnFaces"></param>
        /// <param name="uOnCurve3Ds"></param>
        public override void Intersect(ICurve curve, BoundingRect uvExtent, out GeoPoint[] ips, out GeoPoint2D[] uvOnFaces, out double[] uOnCurve3Ds)
        {
            //// Problemfall: tangentiale Kurve, sehr nahe am Rand der Fläche
            //// führt bei BoxedSurface zu großen Problemen
            //// suche einige Punkte der Kurve und deren Positionen auf der Fläche
            //// Wenn diese alle auf einer Seite der Fläche liegen, dann gibt es keinen Schnitt
            //double[] u = curve.GetSavePositions();
            //if (u.Length < 10)
            //{
            //    u = new double[11];
            //    for (int i = 0; i < u.Length; i++)
            //    {
            //        u[i] = i / 10.0;
            //    }
            //}
            //GeoVector[] cross = new GeoVector[u.Length];
            //GeoPoint[] rpos = new GeoPoint[u.Length];
            //double[] scl = new double[u.Length];
            //for (int i = 0; i < u.Length; i++)
            //{
            //    GeoPoint p = curve.PointAt(u[i]);
            //    GeoPoint2D uv = this.PositionOf(p);
            //    Plane pln = new Plane(this.PointAt(uv), UDirection(uv), VDirection(uv));
            //    rpos[i] = pln.ToLocal(p);
            //    cross[i] = curve.DirectionAt(u[i]) ^ this.GetNormal(uv);
            //    scl[i] = curve.DirectionAt(u[i]) * this.GetNormal(uv);
            //}
            //try
            //{
            //    CndHlp3D.Surface sf = Helper;
            //    CndHlp3D.GeoPoint3D[] ip3d = sf.GetCurveIntersection((curve as ICndHlp3DEdge).Edge);
            //    ips = GeoPoint.FromCndHlp(ip3d);
            //}
            //catch (OpenCascade.Exception)
            //{
            //    ips = new GeoPoint[0];
            //}
            //uvOnFaces = new GeoPoint2D[ips.Length];
            //uOnCurve3Ds = new double[ips.Length];
            //for (int i = 0; i < ips.Length; ++i)
            //{
            //    uvOnFaces[i] = this.PositionOf(ips[i]);
            //    uOnCurve3Ds[i] = curve.PositionOf(ips[i]);
            //}
#if DEBUG
            //if (curve is IExplicitPCurve3D)
            //{
            //    ExplicitPCurve3D ec3d = (curve as IExplicitPCurve3D).GetExplicitPCurve3D();
            //    Polynom[] srfpol;
            //    if (nubs != null) srfpol = nubs.SurfacePointPolynom((UKnots[0] + UKnots[UKnots.Length - 1]) / 2, (VKnots[0] + VKnots[VKnots.Length - 1]) / 2);
            //    else srfpol = nurbs.SurfacePointPolynom((UKnots[0] + UKnots[UKnots.Length - 1]) / 2, (VKnots[0] + VKnots[VKnots.Length - 1]) / 2);
            //    Polynom[] equations = new Polynom[3];
            //    equations[0] = srfpol[0].Substitute(new Polynom(new int[] { 1, 0, 0 }, 1.0), new Polynom(new int[] { 0, 1, 0 }, 1.0)) - ec3d.px[0].Substitute(new Polynom(new int[] { 0, 0, 1 }, 1.0));
            //    equations[1] = srfpol[1].Substitute(new Polynom(new int[] { 1, 0, 0 }, 1.0), new Polynom(new int[] { 0, 1, 0 }, 1.0)) - ec3d.py[0].Substitute(new Polynom(new int[] { 0, 0, 1 }, 1.0));
            //    equations[2] = srfpol[2].Substitute(new Polynom(new int[] { 1, 0, 0 }, 1.0), new Polynom(new int[] { 0, 1, 0 }, 1.0)) - ec3d.pz[0].Substitute(new Polynom(new int[] { 0, 0, 1 }, 1.0));
            //    List<double[]> sol = Polynom.Solve(equations, new(double min, double max)[] { (UKnots[0], UKnots[UKnots.Length - 1]), (VKnots[0], VKnots[VKnots.Length - 1]), (0.0, 1.0) });
            //}
#endif
            //Unreachable code
            /*
            if (false)
            // if (curve is IExplicitPCurve3D) // keine guten Ergebnisse!
            {
                List<GeoPoint> lips = new List<GeoPoint>();
                List<GeoPoint2D> luvOnFaces = new List<GeoPoint2D>();
                List<double> luOnCurve3Ds = new List<double>();
                ExplicitPCurve3D ec3d = (curve as IExplicitPCurve3D).GetExplicitPCurve3D();
                // uKnots, vKnots sind die einfachen
                double prec = BoxedSurfaceEx.GetRawExtent().Size * 1e-7;
                if (implicitSurface == null)
                {
                    implicitSurface = new CADability.ImplicitPSurface[uKnots.Length - 1, vKnots.Length - 1];
                }
                bool failed = false;
                for (int i = 0; i < uKnots.Length - 1; i++)
                {
                    if (uvExtent.Left <= uKnots[i + 1] || uvExtent.Right >= uKnots[i])
                    {
                        for (int j = 0; j < vKnots.Length - 1; j++)
                        {
                            if (uvExtent.Bottom <= vKnots[j + 1] || uvExtent.Top >= vKnots[j])
                            {
                                BoundingRect patch = new BoundingRect(uKnots[i], vKnots[j], uKnots[i + 1], vKnots[j + 1]);
                                if (implicitSurface[i, j] == null)
                                {
                                    implicitSurface[i, j] = new ImplicitPSurface(this, patch, 7, prec);
                                }
                                if (implicitSurface[i, j].error < prec)
                                {
                                    double[] eu;
                                    GeoPoint[] iips = implicitSurface[i, j].Intersect(ec3d, out eu);
                                    if (iips != null)
                                    {
                                        for (int k = 0; k < iips.Length; k++)
                                        {
                                            GeoPoint2D uv = PositionOf(iips[k]);
                                            if (patch.Contains(uv))
                                            {
                                                lips.Add(iips[k]);
                                                luvOnFaces.Add(uv);
                                                luOnCurve3Ds.Add(curve.PositionOf(iips[k]));
                                            }
                                        }
                                    }
                                }
                                else
                                {
                                    failed = true;
                                    break;
                                }
                            }
                        }
                    }
                    if (failed) break;
                }
                if (!failed)
                {
                    ips = lips.ToArray();
                    uvOnFaces = luvOnFaces.ToArray();
                    uOnCurve3Ds = luOnCurve3Ds.ToArray();
                    return;
                }
            }
            */
            base.Intersect(curve, uvExtent, out ips, out uvOnFaces, out uOnCurve3Ds);
        }
        /// <summary>
        /// Overrides <see cref="CADability.GeoObject.ISurfaceImpl.Derivation2At (GeoPoint2D, out GeoPoint, out GeoVector, out GeoVector, out GeoVector, out GeoVector, out GeoVector)"/>
        /// </summary>
        /// <param name="uv"></param>
        /// <param name="location"></param>
        /// <param name="du"></param>
        /// <param name="dv"></param>
        /// <param name="duu"></param>
        /// <param name="dvv"></param>
        /// <param name="duv"></param>
        public override void Derivation2At(GeoPoint2D uv, out GeoPoint location, out GeoVector du, out GeoVector dv, out GeoVector duu, out GeoVector dvv, out GeoVector duv)
        {
            if (nubs == null && nurbs == null) Init();
            if (nubs != null)
            {
                GeoPoint[,] der = nubs.SurfaceDeriv(uv.x, uv.y, 2);
                location = der[0, 0];
                du = new GeoVector(der[1, 0].x, der[1, 0].y, der[1, 0].z);
                dv = new GeoVector(der[0, 1].x, der[0, 1].y, der[0, 1].z);
                duu = new GeoVector(der[2, 0].x, der[2, 0].y, der[2, 0].z);
                dvv = new GeoVector(der[0, 2].x, der[0, 2].y, der[0, 2].z);
                duv = new GeoVector(der[1, 1].x, der[1, 1].y, der[1, 1].z);
            }
            else if (nurbs != null)
            {
                GeoPointH[,] der = nurbs.SurfaceDeriv(uv.x, uv.y, 2);
                location = (GeoPoint)der[0, 0];
                du = new GeoVector(der[1, 0].x, der[1, 0].y, der[1, 0].z);
                dv = new GeoVector(der[0, 1].x, der[0, 1].y, der[0, 1].z);
                duu = new GeoVector(der[2, 0].x, der[2, 0].y, der[2, 0].z);
                dvv = new GeoVector(der[0, 2].x, der[0, 2].y, der[0, 2].z);
                duv = new GeoVector(der[1, 1].x, der[1, 1].y, der[1, 1].z);
            }
            else throw new ApplicationException("NURBS not initialized"); // sollte nie vorkommen, beruhigt den compiler
        }
        /// <summary>
        /// Overrides <see cref="CADability.GeoObject.ISurfaceImpl.HasDiscontinuousDerivative (out ICurve2D[])"/>
        /// </summary>
        /// <param name="discontinuities"></param>
        /// <returns></returns>
        public override bool HasDiscontinuousDerivative(out ICurve2D[] discontinuities)
        {
            if (uDegree > 1 && vDegree > 1)
            {
                discontinuities = null;
                return false;
            }
            List<ICurve2D> dsc = new List<ICurve2D>();
            if (uDegree == 1)
            {
                // an den inneren knoten v-Linien erzeugen (etwas länger, damit SimpleShape.Split funktioniert
                double dv = (vKnots[vKnots.Length - 1] - vKnots[0]) / 1000;
                for (int i = 1; i < uKnots.Length - 1; i++)
                {
                    Line2D l2d = new Line2D(new GeoPoint2D(uKnots[i], vKnots[0] - dv), new GeoPoint2D(uKnots[i], vKnots[vKnots.Length - 1] + dv));
                    dsc.Add(l2d);
                }
            }
            if (vDegree == 1)
            {
                // an den inneren knoten u-Linien erzeugen
                double du = (uKnots[uKnots.Length - 1] - uKnots[0]) / 1000;
                for (int i = 1; i < vKnots.Length - 1; i++)
                {
                    Line2D l2d = new Line2D(new GeoPoint2D(uKnots[0] - du, vKnots[i]), new GeoPoint2D(uKnots[uKnots.Length - 1] + du, vKnots[i]));
                    dsc.Add(l2d);
                }
            }
            discontinuities = dsc.ToArray();
            return discontinuities.Length > 0;
        }
        public override CADability.GeoObject.RuledSurfaceMode IsRuled
        {
            get
            {
                if (uDegree == 1 && vDegree > 1)
                    return RuledSurfaceMode.ruledInU;
                if (vDegree == 1 && uDegree > 1)
                    return RuledSurfaceMode.ruledInV;
                return RuledSurfaceMode.notRuled;
            }
        }
        public override ICurve2D GetProjectedCurve(ICurve curve, double precision)
        {
            if (hasSimpleSurface && simpleSurface is PlaneSurface)
            {   // macht Probleme bei periodischen!
                ICurve2D res = simpleSurface.GetProjectedCurve(curve, precision);
                return res.GetModified(toSimpleSurface.GetInverse());
            }
            else
            {
                if (curve is BSpline)
                {
                    // special case to get a horinzontal or vertical 2d line when the curve is close to a fixed curve of the NURBS surface
                    // this is often the case with STEP import
                    Line2D testLine = null;
                    if (curve.IsClosed || (curve.StartPoint | curve.EndPoint) < Precision.eps)
                    {
                        GeoPoint2D uv = PositionOf(curve.StartPoint);
                        if (IsUPeriodic && IsVPeriodic)
                        {

                        }
                        else if (IsUPeriodic)
                        {
                            if (uv.x > UPeriod / 2.0)
                                testLine = new Line2D(uv, uv - UPeriod * GeoVector2D.XAxis);
                            else
                                testLine = new Line2D(uv, uv + UPeriod * GeoVector2D.XAxis);
                        }
                        else if (IsVPeriodic)
                        {
                            if (uv.y > VPeriod / 2.0)
                                testLine = new Line2D(uv, uv - VPeriod * GeoVector2D.YAxis);
                            else
                                testLine = new Line2D(uv, uv + VPeriod * GeoVector2D.YAxis);
                        }
                    }
                    else
                    {
                        GeoPoint2D sp = PositionOf(curve.StartPoint);
                        GeoPoint2D ep = PositionOf(curve.EndPoint);
                        double[] us = GetUSingularities();
                        double[] vs = GetVSingularities();
                        double eps = precision;
                        if (eps == 0) eps = Precision.eps;
                        if (us.Length > 0)
                        {
                            double v = vKnots[0];
                            for (int i = 0; i < us.Length; i++)
                            {
                                GeoPoint pole = PointAt(new GeoPoint2D(us[i], v));
                                if ((pole | curve.StartPoint) < 10 * eps)
                                {
                                    GeoPoint2D mp = PositionOf(curve.PointAt(0.5));
                                    sp.y = mp.y;
                                }
                                if ((pole | curve.EndPoint) < 10 * eps)
                                {
                                    GeoPoint2D mp = PositionOf(curve.PointAt(0.5));
                                    ep.y = mp.y;
                                }
                            }
                        }
                        if (vs.Length > 0)
                        {
                            double u = uKnots[0];
                            for (int i = 0; i < vs.Length; i++)
                            {
                                GeoPoint pole = PointAt(new GeoPoint2D(u, vs[i]));
                                if ((pole | curve.StartPoint) < 10 * eps)
                                {
                                    GeoPoint2D mp = PositionOf(curve.PointAt(0.5));
                                    sp.x = mp.x;
                                }
                                if ((pole | curve.EndPoint) < 10 * eps)
                                {
                                    GeoPoint2D mp = PositionOf(curve.PointAt(0.5));
                                    ep.x = mp.x;
                                }
                            }
                        }
                        if (Math.Abs(sp.x - ep.x) < uSpan * 1e-5 || Math.Abs(sp.y - ep.y) < vSpan * 1e-5) testLine = new Line2D(sp, ep);
                    }
                    if (testLine != null && testLine.StartPoint.x >= uKnots[0] - uSpan * 1e-5 && testLine.StartPoint.x <= uKnots[uKnots.Length - 1] + uSpan * 1e-5 && testLine.StartPoint.y >= vKnots[0] - vSpan * 1e-5 && testLine.StartPoint.y <= vKnots[vKnots.Length - 1] + vSpan * 1e-5)
                    {
                        ICurve crv = Make3dCurve(testLine);
                        if (crv != null)
                        {
#if DEBUG
                            //if (curve is BSpline)
                            //{
                            //    Polyline pl = (curve as BSpline).DebugSegments;
                            //    (curve as BSpline).GetData(out GeoPoint[] pls, out double[] weights, out double[] knots, out int degree);
                            //}
                            //TetraederHull dbgTetraederHull = new TetraederHull(curve);
                            //GeoVector sdir = curve.DirectionAt(0.0001);
#endif
                            if (curve.IsClosed || (curve.StartPoint | curve.EndPoint) < Precision.eps)
                            {   // with closed curves, the direction is ambiguous
                                //if (crv.StartDirection * curve.StartDirection < 0)
                                if (crv.DirectionAt(0.5) * curve.DirectionAt(0.5) < 0)
                                {
                                    crv.Reverse();
                                    testLine.Reverse();
                                }
                            }
                            double error = (crv.StartPoint | curve.StartPoint) + (crv.EndPoint | curve.EndPoint);
                            // if the curve hoovers above the surface, then we never can be exact
                            double prec = curve.Length * 1e-3;
                            if (precision > 0) prec = Math.Max(precision, prec);
                            else prec = Math.Max(Precision.eps, prec);
                            double[] svpos = crv.GetSavePositions();
                            bool ok = true;
                            for (int i = 0; i < svpos.Length; i++)
                            {
                                double d = curve.DistanceTo(crv.PointAt(svpos[i]));
                                if (d > prec)
                                {
                                    ok = false;
                                    break;
                                }
                                if (ok && i > 0)
                                {
                                    d = curve.DistanceTo(crv.PointAt((svpos[i] + svpos[i - 1]) / 2));
                                    if (d > prec)
                                    {
                                        ok = false;
                                        break;
                                    }
                                }
                            }
                            if (ok) return testLine;
                        }
                    }
                }
                else if (curve is Polyline)
                {   // eine Polyline wird in eine Polyline2D mit glichen Eckpunkten projiziert.
                    // hier ist der Ausgangspunkt (z.B. in in 12119702_P29.stp) dass es sich in 3d auch um eine angenäherte Kurve handelt
                    // und man übernimmt einfach die Annäherung in 2D
                    // Eine Linie in 3D kann zwar im Prinzip eine krumme Kurve in 2D bedeuten, aber das ist praktisch nie der Fall
                    Polyline pl = curve as Polyline;
                    GeoPoint2D[] vtx = new GeoPoint2D[pl.Vertices.Length];
                    for (int i = 0; i < vtx.Length; i++)
                    {
                        vtx[i] = PositionOf(pl.Vertices[i]);
                    }
                    if (IsUPeriodic)
                    {
                        double sx = vtx[0].x;
                        for (int i = 1; i < vtx.Length; i++)
                        {
                            while (Math.Abs(vtx[i].x - vtx[i - 1].x) > UPeriod / 2)
                            {
                                if (vtx[i].x > vtx[i - 1].x) vtx[i].x -= UPeriod;
                                else vtx[i].x += UPeriod;
                            }
                            sx += vtx[i].x;
                        }
                        sx /= vtx.Length;
                        if (sx < uKnots[0])
                        {
                            for (int i = 0; i < vtx.Length; i++) vtx[i].x += UPeriod;
                        }
                        if (sx > uKnots[uKnots.Length - 1])
                        {
                            for (int i = 0; i < vtx.Length; i++) vtx[i].x -= UPeriod;
                        }
                    }
                    if (IsVPeriodic)
                    {
                        double sy = vtx[0].y;
                        for (int i = 1; i < vtx.Length; i++)
                        {
                            while (Math.Abs(vtx[i].y - vtx[i - 1].y) > VPeriod / 2)
                            {
                                if (vtx[i].y > vtx[i - 1].y) vtx[i].y -= VPeriod;
                                else vtx[i].y += VPeriod;
                            }
                            sy += vtx[i].y;
                        }
                        sy /= vtx.Length;
                        if (sy < vKnots[0])
                        {
                            for (int i = 0; i < vtx.Length; i++) vtx[i].y += VPeriod;
                        }
                        if (sy > vKnots[vKnots.Length - 1])
                        {
                            for (int i = 0; i < vtx.Length; i++) vtx[i].y -= VPeriod;
                        }
                    }
                    try
                    {
                        return new Polyline2D(vtx);
                    }
                    catch (Polyline2DException)
                    {
                        return null;
                    }
                }
                else if (curve is Line)
                {
                    GeoPoint2D sp = PositionOf(curve.StartPoint);
                    GeoPoint2D ep = PositionOf(curve.EndPoint);
                    if (Math.Abs(sp.x - ep.x) < uSpan * 1e-5 || Math.Abs(sp.y - ep.y) < vSpan * 1e-5) return new Line2D(sp, ep);
                }
            }
            return base.GetProjectedCurve(curve, precision);
        }
        public override double MaxDist(GeoPoint2D sp, GeoPoint2D ep, out GeoPoint2D mp)
        {
            if (hasSimpleSurface)
            {
                double res = simpleSurface.MaxDist(toSimpleSurface * sp, toSimpleSurface * ep, out mp);
                mp = toSimpleSurface.GetInverse() * mp;
                return res;
            }
            return base.MaxDist(sp, ep, out mp);
        }
        public override ISurface GetOffsetSurface(double offset)
        {
            // Idee: finde die Parameter, die den Polen am besten entsprechen und verschiebe die Pole
            GeoPoint[,] newPoles = poles.Clone() as GeoPoint[,];
            double du = (double)(uKnots.Length - 1) / (double)(poles.GetLength(0) - 1);
            double dv = (double)(vKnots.Length - 1) / (double)(poles.GetLength(1) - 1);
            for (int i = 0; i < poles.GetLength(0); i++)
            {
                for (int j = 0; j < poles.GetLength(1); j++)
                {
                    int ki = (int)Math.Floor(i * du);
                    double dki = i * du - ki;
                    int kj = (int)Math.Floor(j * dv);
                    double dkj = j * dv - kj;
                    double u = uKnots[ki];
                    double v = vKnots[kj];
                    if (ki < uKnots.Length - 1) u += dki * (uKnots[ki + 1] - uKnots[ki]);
                    if (kj < vKnots.Length - 1) v += dkj * (vKnots[kj + 1] - vKnots[kj]);
                    GeoVector n = GetNormal(new GeoPoint2D(u, v));
                    newPoles[i, j] = newPoles[i, j] + offset * n.Normalized;
                }
            }
            double[,] newWeights = null;
            if (weights != null)
            {
                newWeights = weights.Clone() as double[,];
            }
            NurbsSurface res = new NurbsSurface(newPoles, newWeights, uKnots.Clone() as double[], vKnots.Clone() as double[], uMults.Clone() as int[], vMults.Clone() as int[], uDegree, vDegree, IsUPeriodic, IsVPeriodic);
            return res;
            // return base.GetOffsetSurface(offset);
        }
        //public override ISurface GetNonPeriodicSurface(ICurve[] orientedCurves)
        //{
        //    if (IsUPeriodic || IsVPeriodic || GetUSingularities().Length > 0 || GetVSingularities().Length > 0)
        //    {
        //        BoundingRect bounds = BoundingRect.EmptyBoundingRect;
        //        for (int i = 0; i < orientedCurves.Length; i++)
        //        {
        //            bounds.MinMax(this.GetProjectedCurve(orientedCurves[i], 0).GetExtent());
        //        }
        //        this.GetNaturalBounds(out bounds.Left, out bounds.Right, out bounds.Bottom, out bounds.Top);
        //        return new NonPeriodicSurface(this, bounds);
        //    }
        //    return null;
        //}
        #endregion
        #region ISerializable Members
        // constructor for serialization
        protected NurbsSurface(SerializationInfo info, StreamingContext context)
        {
            poles = (GeoPoint[,])info.GetValue("Poles", typeof(GeoPoint[,]));
            weights = (double[,])info.GetValue("Weights", typeof(double[,]));
            uKnots = (double[])info.GetValue("UKnots", typeof(double[]));
            vKnots = (double[])info.GetValue("VKnots", typeof(double[]));
            uMults = (int[])info.GetValue("UMults", typeof(int[]));
            vMults = (int[])info.GetValue("VMults", typeof(int[]));
            uDegree = (int)info.GetValue("UDegree", typeof(int));
            vDegree = (int)info.GetValue("VDegree", typeof(int));
            uPeriodic = (bool)info.GetValue("UPeriodic", typeof(bool));
            vPeriodic = (bool)info.GetValue("VPeriodic", typeof(bool));
            if (info.MemberCount > 10) // to avoid exceptions
            {
                try
                {
                    uMinRestrict = info.GetDouble("UMinRestrict");
                    uMaxRestrict = info.GetDouble("UMaxRestrict");
                    vMinRestrict = info.GetDouble("VMinRestrict");
                    vMaxRestrict = info.GetDouble("VMaxRestrict");
                }
                catch (SerializationException)
                {
                    uMinRestrict = uMaxRestrict = vMinRestrict = vMaxRestrict = 0.0;
                }
            }
        }

        void ISerializable.GetObjectData(SerializationInfo info, StreamingContext context)
        {
            info.AddValue("Poles", poles, typeof(GeoPoint[,]));
            info.AddValue("Weights", weights, typeof(double[,]));
            info.AddValue("UKnots", uKnots, typeof(double[]));
            info.AddValue("VKnots", vKnots, typeof(double[]));
            info.AddValue("UMults", uMults, typeof(int[]));
            info.AddValue("VMults", vMults, typeof(int[]));
            info.AddValue("UDegree", uDegree, typeof(int));
            info.AddValue("VDegree", vDegree, typeof(int));
            info.AddValue("UPeriodic", uPeriodic, typeof(bool));
            info.AddValue("VPeriodic", vPeriodic, typeof(bool));
            info.AddValue("UMinRestrict", uMinRestrict, typeof(double));
            info.AddValue("UMaxRestrict", uMaxRestrict, typeof(double));
            info.AddValue("VMinRestrict", vMinRestrict, typeof(double));
            info.AddValue("VMaxRestrict", vMaxRestrict, typeof(double));

        }

        #endregion
        public override IPropertyEntry GetPropertyEntry(IFrame frame)
        {
            // to implement:
            return new GroupProperty("NurbsSurface", new IPropertyEntry[0]);
        }
        #region IDeserializationCallback Members

        void IDeserializationCallback.OnDeserialization(object sender)
        {
            Init();
        }

        #endregion
        internal static NurbsSurface Concat(List<NurbsSurface> surfaces)
        {
            return null;
        }

        public bool isRational
        {
            get
            {
                if (weights == null) return false;
                for (int i = 0; i < weights.GetLength(0); i++)
                {
                    for (int j = 0; j < weights.GetLength(1); j++)
                    {
                        if (weights[i, j] != 1.0) return true;
                    }
                }
                return false;
            }
        }

        private BoundingCube PolesExtent
        {
            get
            {
                BoundingCube res = BoundingCube.EmptyBoundingCube;
                for (int i = 0; i < poles.GetLength(0); i++)
                {
                    for (int j = 0; j < poles.GetLength(1); j++)
                    {
                        res.MinMax(poles[i, j]);
                    }

                }
                return res;
            }
        }

        internal PlaneSurface ConvertToPlane(double precision)
        {
            GeoPoint[] samples = new GeoPoint[9];
            GeoVector[] normals = new GeoVector[9];
            double umin, umax, vmin, vmax;
            GetNaturalBounds(out umin, out umax, out vmin, out vmax);
            double du = (umax - umin) / 2.0;
            double dv = (vmax - vmin) / 2.0;
            if (IsUPeriodic && umax - umin >= UPeriod * 0.9) du = (umax - umin) / 3.0;
            if (IsVPeriodic && vmax - vmin >= VPeriod * 0.9) dv = (vmax - vmin) / 3.0;
            for (int i = 0; i < 3; i++)
            {
                for (int j = 0; j < 3; j++)
                {
                    GeoPoint2D uv = new GeoPoint2D(umin + i * du, vmin + j * dv);
                    samples[i * 3 + j] = PointAt(uv);
                    normals[i * 3 + j] = GetNormal(uv);
                }
            }
            double maxDist;
            bool isLinear;
            Plane pln = Plane.FromPoints(samples, out maxDist, out isLinear);
            if (!isLinear && maxDist < precision)
            {
                PlaneSurface ps = new PlaneSurface(pln);
                return ps;
            }
            return null;
        }

        internal ConicalSurface ConvertToCone(double precision)
        {
            GeoPoint[] samples = new GeoPoint[25];
            GeoVector[] normals = new GeoVector[25];
            double umin, umax, vmin, vmax;
            GetNaturalBounds(out umin, out umax, out vmin, out vmax);
            double du = (umax - umin) / 4.0;
            double dv = (vmax - vmin) / 4.0;
            if (IsUPeriodic && umax - umin >= UPeriod * 0.9) du = (umax - umin) / 5.0;
            if (IsVPeriodic && vmax - vmin >= VPeriod * 0.9) dv = (vmax - vmin) / 5.0;
            for (int i = 0; i < 5; i++)
            {
                for (int j = 0; j < 5; j++)
                {
                    GeoPoint2D uv = new GeoPoint2D(umin + i * du, vmin + j * dv);
                    samples[i * 5 + j] = PointAt(uv);
                    normals[i * 5 + j] = GetNormal(uv);
                }
            }
            GeoPoint location = new GeoPoint(samples); // center of all points
            GeoVector direction = GeoVector.NullVector;
            for (int i = 0; i < normals.Length - 1; i++)
            {
                direction += normals[i] ^ normals[i + 1];
            }
            direction.Norm();
            double halfAngle = -Math.PI / 4.0;
            if (GaussNewtonMinimizer.ConeFitNew(samples.ToIArray(), location, direction, halfAngle, Precision.eps, out ConicalSurface cs) < Precision.eps)
            {
                return cs;
            }
            // old method:
            double maxerror = findBestFitCone(samples, normals, out location, out direction, out halfAngle);
            direction.Norm();
            if (maxerror <= precision)
            {
                GeoVector dirx, diry;
                if (Math.Abs(direction.x) < Math.Abs(direction.y))
                {
                    dirx = direction ^ GeoVector.XAxis;
                    diry = direction ^ dirx;
                }
                else
                {
                    dirx = direction ^ GeoVector.YAxis;
                    diry = direction ^ dirx;
                }
                ConicalSurface res = new ConicalSurface(location, dirx, diry, direction, -(Math.PI / 2 - halfAngle), 0.0);
                return res;
            }
            return null;
        }
#if DEBUG
        public
#else
        private
#endif
        static double findBestFitCone(GeoPoint[] samples, GeoVector[] normals, out GeoPoint location, out GeoVector direction, out double halfAngle)
        {
            for (int i = 0; i < normals.Length; i++)
            {
                normals[i].NormIfNotNull();
            }
            // alle Normalenvektoren normiert, dir*normals == a, a und dir sind unbekannt
            // dirx*nx+diry*ny+dirz*nz-a*1==0
            // dirx+diry+dirz==1
            // 2. Gleichungssystem:
            // normals*(samles-c)==0
            // nx*sx-nx*cx + ny*sy-ny*cy + nz*sz-nz*cz == 0
            // nx*cx+ny*cy+nz*cz == nx*sx+ny*sy+nz*sz

            Matrix a = new DenseMatrix(normals.Length + 1, 4); // mit 0 vorbesetzt
            Vector b = new DenseVector(normals.Length + 1);

            for (int i = 0; i < normals.Length; i++)
            {
                a[i, 0] = normals[i].x;
                a[i, 1] = normals[i].y;
                a[i, 2] = normals[i].z;
                a[i, 3] = 1;
            }
            a[normals.Length, 0] = 1.0; // die Summe der Komponenten der Lösung ist 1, sonst wäre (0,0,0) auch eine lösung
            a[normals.Length, 1] = 1.0;
            a[normals.Length, 2] = 1.0;
            a[normals.Length, 3] = 0.0;
            b[normals.Length] = 1.0;
            QR<double> qrd = a.QR();
            if (qrd.IsFullRank)
            {
                Vector x = (Vector)qrd.Solve(b);
                direction = new GeoVector(x[0], x[1], x[2]);

                a = new DenseMatrix(normals.Length + 1, 3); // mit 0 vorbesetzt
                b = new DenseVector(normals.Length + 1);

                for (int i = 0; i < normals.Length; i++)
                {
                    a[i, 0] = normals[i].x;
                    a[i, 1] = normals[i].y;
                    a[i, 2] = normals[i].z;
                    b[i] = normals[i].x * samples[i].x + normals[i].y * samples[i].y + normals[i].z * samples[i].z;
                }
                qrd = a.QR();
                if (qrd.IsFullRank)
                {
                    x = (Vector)qrd.Solve(b);
                    location = new GeoPoint(x[0], x[1], x[2]);
                    halfAngle = 0.0; // muss noch berechnet werden
                    int n = 0;
                    double sum = 0.0;
                    for (int i = 0; i < samples.Length; i++)
                    {
                        if (!Precision.IsEqual(samples[i], location))
                        {
                            Angle ha = new Angle(direction, samples[i] - location);
                            ++n;
                            sum += ha.Radian;
                        }
                    }
                    halfAngle = sum / n;
                    return 0.0; // precision muss noch berechnet werden
                }

            }
            location = GeoPoint.Origin;
            direction = GeoVector.NullVector;
            halfAngle = 0.0;
            return double.MaxValue;
        }
#if DEBUG
        public
#else
        private
#endif
        static double findBestFitSphere(GeoPoint[] samples, GeoVector[] normals, out GeoPoint center, out double radius)
        {
            double maxError = double.MaxValue;
            // alle Normalenvektoren normiert, (samples-c)*normals == r², c und r unbekannt
            // (sx-cx)*nx+(sy-cy)*ny+(sz-cz)*nz+rr==0
            // cx*nx+cy*ny+cz*nz+rr*1 == sx*sn+sy*ny+sz*ns
            for (int i = 0; i < normals.Length; i++)
            {
                normals[i].NormIfNotNull();
            }
            Matrix a = new DenseMatrix(normals.Length, 4); // mit 0 vorbesetzt
            Vector b = new DenseVector(normals.Length);
            for (int i = 0; i < normals.Length; i++)
            {
                a[i, 0] = normals[i].x;
                a[i, 1] = normals[i].y;
                a[i, 2] = normals[i].z;
                a[i, 3] = 1.0;
                b[i] = normals[i].x * samples[i].x + normals[i].y * samples[i].y + normals[i].z * samples[i].z;
            }
            QR<double> qrd = a.QR();
            if (qrd.IsFullRank)
            {
                Vector x = (Vector)qrd.Solve(b);
                if (x[3] != 0.0)
                {
                    center = new GeoPoint(x[0], x[1], x[2]);
                    radius = Math.Abs(x[3]);
                    maxError = 0.0;
                    for (int i = 0; i < samples.Length; i++)
                    {
                        maxError = Math.Max(maxError, Math.Abs((samples[i] | center) - radius));
                    }
                    return maxError;
                }
            }
            center = GeoPoint.Origin;
            radius = 0.0;
            return maxError;
        }
        /// <summary>
        /// Finds the torus that interpolates the provided points and their normals with the smalles error
        /// </summary>
        /// <param name="samples"></param>
        /// <param name="normals"></param>
        /// <param name="location"></param>
        /// <param name="direction"></param>
        /// <param name="radius1"></param>
        /// <param name="radius2"></param>
        /// <returns></returns>
#if DEBUG
        public
#else
        private
#endif
        static double findBestFitTorus(GeoPoint[] samples, GeoVector[] normals, out GeoPoint location, out GeoVector direction, out double radius1, out double radius2)
        {
            // Kreuzprodukt normal ^ dir (Achse, unbekannt) ist Normale auf Ebene durch center (Mittelpunkt, unbekannt), in der auch sample liegen muss
            // das führt zu (n^d)*(s-c) == 0, mit Maxima zu:
            // dx*ny*sz-dy*nx*sz-dx*nz*sy+dz*nx*sy+dy*nz*sx-dz*ny*sx-cx*dy*nz+cy*dx*nz+cx*dz*ny-cz*dx*ny-cy*dz*nx+cz*dy*nx
            // -cx*dy*nz+cy*dx*nz+cx*dz*ny-cz*dx*ny-cy*dz*nx+cz*dy*nx
            // dx*(ny*sz-nz*sy) + dy*(nz*sx-nx*sz) + dz*(nx*sy-ny*sz) + cx*dy*(-nz) + cy*dx*(nz) + cz*dx*(-ny) + cx*dz*(ny) + cy*dz*(-nx) + cz*dy*(nx)
            // Das sind 9 Unbekannte, wobei die auch zusammenhängenden cx*dy u.s.w. vorkommen. Es muss auch noch dx+dy+dz==1 dazugenommen werden
            // aber c ist nicht eindeutig, denn jeder Punkt auf der Achse erfüllt c
            for (int i = 0; i < normals.Length; i++)
            {
                normals[i].Norm();
            }
            Matrix a = new DenseMatrix(normals.Length + 1, 9); // mit 0 vorbesetzt
            Vector b = new DenseVector(normals.Length + 1);

            for (int i = 0; i < normals.Length; i++)
            {
                a[i, 0] = normals[i].y * samples[i].z - normals[i].z * samples[i].y; // dx
                a[i, 1] = normals[i].z * samples[i].x - normals[i].x * samples[i].z; // dy
                a[i, 2] = normals[i].x * samples[i].y - normals[i].y * samples[i].x; // dz
                a[i, 3] = -normals[i].z; //cx*dy*(-nz)
                a[i, 4] = normals[i].z; // cy*dx*(nz)
                a[i, 5] = -normals[i].y; // cz*dx*(-ny) 
                a[i, 6] = normals[i].y; // cx*dz*(ny) 
                a[i, 7] = -normals[i].x; // cy*dz*(-nx) 
                a[i, 8] = normals[i].x; // cz*dy*(nx)
            }
            a[normals.Length, 0] = 1.0; // dx+dy+dz==1, sonst wäre (0,0,0) auch eine Lösung
            a[normals.Length, 1] = 1.0;
            a[normals.Length, 2] = 1.0;
            b[normals.Length] = 1.0;
            QR<double> qrd = a.QR();
            if (qrd.IsFullRank)
            {
                Vector x = (Vector)qrd.Solve(b);
                direction = new GeoVector(x[0], x[1], x[2]);
                // cx, cy, cz aus vorigem System ist ein beliebiger Punkt auf der Achse. Somit gilt
                // x[3,0]/direction.y + a*direction.x == c.x oder
                // c.x*direction.y - a*direction.x*direction.y== -x[3,0]. Vielleicht wird damit das folgende System einfacher, d.h. man 
                // bräuchte nur die 2. Hälfte. a als neue Unbekannte, r2 müsste man aber anders finden
                //a = new Matrix(normals.Length+6, 4, 0.0); // mit 0 vorbesetzt
                //b = new Matrix(normals.Length +6, 1, 0.0);
                //for (int i = 0; i < normals.Length; i++)
                //{
                //    a[i, 0] = direction.y * normals[i].z - direction.z * normals[i].y;
                //    a[i, 1] = direction.z * normals[i].x - direction.x * normals[i].z;
                //    a[i, 2] = direction.x * normals[i].y - direction.y * normals[i].x;
                //    a[i, 3] = 0.0;
                //    b[i, 0] = -(-direction.x * normals[i].y * samples[i].z + direction.y * normals[i].x * samples[i].z + direction.x * normals[i].z * samples[i].y - direction.z * normals[i].x * samples[i].y - direction.y * normals[i].z * samples[i].x + direction.z * normals[i].y * samples[i].x);
                //}
                //a[normals.Length + 0, 0] = direction.y;
                //a[normals.Length + 0, 3] = direction.x*direction.y;
                //b[normals.Length + 0, 0] = -x[3, 0];

                //a[normals.Length + 1, 0] = direction.x;
                //a[normals.Length + 1, 3] = direction.y * direction.x;
                //b[normals.Length + 1, 0] = -x[4, 0];

                //a[normals.Length + 2, 0] = direction.x;
                //a[normals.Length + 2, 3] = direction.z * direction.x;
                //b[normals.Length + 2, 0] = -x[5, 0];

                //a[normals.Length + 3, 0] = direction.z;
                //a[normals.Length + 3, 3] = direction.x * direction.z;
                //b[normals.Length + 3, 0] = -x[6, 0];

                //a[normals.Length + 4, 0] = direction.z;
                //a[normals.Length + 4, 3] = direction.y * direction.z;
                //b[normals.Length + 4, 0] = -x[7, 0];

                //a[normals.Length + 5, 0] = direction.y;
                //a[normals.Length + 5, 3] = direction.z * direction.y;
                //b[normals.Length + 5, 0] = -x[8, 0];

                //qrd = a.QRD();
                //if (qrd.FullRank)
                //{
                //    x = qrd.Solve(b);
                //}
                // das System
                // (direction^normals)*(c-samples)==0 (jeder Punkt auf der Achse erfüllt c)
                // - dx * ny * sz + dy * nx * sz + dx * nz * sy - dz * nx * sy - dy * nz * sx + dz * ny * sx + cx * dy * nz - cy * dx * nz - cx * dz * ny + cz * dx * ny + cy * dz * nx - cz * dy * nx
                // cx*(dy * nz - dz * ny) + cy*(dz * nx - dx * nz) + cz*(dx * ny - dy * nx) + ...
                // und das System
                // (s-r2*n-c)*d==0 liefert c und r2, wobei jeder Punkt in der Ebene c erfüllt
                // dz* sz + dy * sy + dx * sx - dz * nz * r - dy * ny * r - dx * nx * r - cz * dz - cy * dy - cx * dx ==0
                // mit beiden Systemen wird c dingfest gemacht
                a = new DenseMatrix(normals.Length * 2, 4); // mit 0 vorbesetzt
                b = new DenseVector(normals.Length * 2);
                for (int i = 0; i < normals.Length; i++)
                {
                    a[i, 0] = direction.x;
                    a[i, 1] = direction.y;
                    a[i, 2] = direction.z;
                    a[i, 3] = direction.x * normals[i].x + direction.y * normals[i].y + direction.z * normals[i].z;
                    b[i] = direction.x * samples[i].x + direction.y * samples[i].y + direction.z * samples[i].z;
                    a[normals.Length + i, 0] = direction.y * normals[i].z - direction.z * normals[i].y;
                    a[normals.Length + i, 1] = direction.z * normals[i].x - direction.x * normals[i].z;
                    a[normals.Length + i, 2] = direction.x * normals[i].y - direction.y * normals[i].x;
                    a[normals.Length + i, 3] = 0.0;
                    b[normals.Length + i] = -(-direction.x * normals[i].y * samples[i].z + direction.y * normals[i].x * samples[i].z + direction.x * normals[i].z * samples[i].y - direction.z * normals[i].x * samples[i].y - direction.y * normals[i].z * samples[i].x + direction.z * normals[i].y * samples[i].x);
                }
                qrd = a.QR();
                if (qrd.IsFullRank)
                {
                    //Matrix QT = qrd.Q.Clone();
                    //QT.Transpose();
                    //Matrix err =  QT * b;
                    x = (Vector)qrd.Solve(b);
                    location = new GeoPoint(x[0], x[1], x[2]);
                    radius2 = x[3];
                    radius1 = 0.0;
                    double minrad = double.MaxValue;
                    double maxrad = 0.0;
                    for (int i = 0; i < normals.Length; i++)
                    {
                        double d = (samples[i] - radius2 * normals[i]) | location;
                        radius1 += d;
                        if (d < minrad) minrad = d;
                        if (d > maxrad) maxrad = d;
                    }
                    radius1 /= normals.Length;
                    return Math.Max(radius1 - minrad, maxrad - radius1);
                }
            }
            location = GeoPoint.Origin;
            direction = GeoVector.NullVector;
            radius1 = radius2 = 0.0;
            return double.MaxValue;
        }

#if DEBUG
        public
#else
        private
#endif
        static double findBestFitCylinder(GeoPoint[] samples, GeoVector[] normals, out GeoPoint location, out GeoVector direction, out double radius)
        {
            // finde zuerst die Achsenrichtung
            // ax*nx+ay*ny+az*nz=0
            // mindestens 3 Normalenvektoren
            Matrix a = new DenseMatrix(normals.Length + 1, 3); // mit 0 vorbesetzt
            Vector b = new DenseVector(normals.Length + 1);

            for (int i = 0; i < normals.Length; i++)
            {
                a[i, 0] = normals[i].x;
                a[i, 1] = normals[i].y;
                a[i, 2] = normals[i].z;
            }
            a[normals.Length, 0] = 1.0; // die Summe der Komponenten der Lösung ist 1, sonst wäre (0,0,0) auch eine lösung
            a[normals.Length, 1] = 1.0;
            a[normals.Length, 2] = 1.0;
            b[normals.Length] = 1.0;
            QR<double> qrd = a.QR();
            if (qrd.IsFullRank)
            {
                Vector x = (Vector)qrd.Solve(b);
                direction = new GeoVector(x[0], x[1], x[2]);
                Plane pln = new Plane(GeoPoint.Origin, direction);
                GeoPoint2D[] samples2d = new GeoPoint2D[samples.Length];
                for (int i = 0; i < samples.Length; i++)
                {
                    samples2d[i] = pln.Project(samples[i]);
                }
                GeoPoint2D center;
                double maxError = Geometry.CircleFitLs(samples2d, out center, out radius);
                location = pln.ToGlobal(center);
                // das Ergebnis sieht gut aus
                return Math.Sqrt(maxError / samples.Length);
            }
            radius = 0.0;
            location = GeoPoint.Origin;
            direction = GeoVector.NullVector;
            return double.MaxValue;
        }

        int IExportStep.Export(ExportStep export, bool topLevel)
        {
            //#28884 = B_SPLINE_SURFACE_WITH_KNOTS( '', 4, 5, ( ( #58103, #58104, #58105, #58106, #58107, #58108 ), ( #58109, #58110, #58111, #58112, #58113, #58114 ), ( #58115, #58116, #58117, #58118, #58119, #58120 ), ( #58121, #58122, #58123, #58124, #58125, #58126 ), ( #58127, #58128, #58129, #58130, #58131, #58132 ) ), .UNSPECIFIED., .F., .F., .F., ( 5, 5 ), ( 6, 6 ), ( 0.000000000000000, 2.84002372450000 ), ( 0.000000000000000, 0.979961490352000 ), .UNSPECIFIED. );
            StringBuilder spoles = new StringBuilder();
            for (int i = 0; i < poles.GetLength(0); ++i)
            {
                if (i == 0) spoles.Append(",((");
                else spoles.Append("),(");
                for (int j = 0; j < poles.GetLength(1); ++j)
                {
                    if (j > 0) spoles.Append(",#");
                    else spoles.Append("#");
                    int n = (poles[i, j] as IExportStep).Export(export, false);
                    spoles.Append(n.ToString());
                }
            }
            string closed;
            if (uPeriodic && vPeriodic) closed = ".T.,.T.";
            else if (uPeriodic) closed = ".T.,.F.";
            else if (vPeriodic) closed = ".F.,.T.";
            else closed = ".F.,.F.";
            if (isRational)
            {
                //#1292=(
                //BOUNDED_SURFACE()
                //B_SPLINE_SURFACE(1, 2, ((#1390,#1391,#1392),(#1393,#1394,#1395)),
                // .UNSPECIFIED.,.F.,.F.,.F.)
                //B_SPLINE_SURFACE_WITH_KNOTS((2, 2), (3, 3), (0., 6016.00000000098), (-1383.94689145077,
                //-1143.94689145077),.UNSPECIFIED.)
                //GEOMETRIC_REPRESENTATION_ITEM()
                //RATIONAL_B_SPLINE_SURFACE(((1., 1., 1.), (1., 1., 1.)))
                //REPRESENTATION_ITEM('')
                //SURFACE()
                //);
                StringBuilder sweights = new StringBuilder();
                for (int i = 0; i < weights.GetLength(0); ++i)
                {
                    if (i > 0) sweights.Append("),(");
                    for (int j = 0; j < weights.GetLength(1); ++j)
                    {
                        if (j > 0) sweights.Append(",");
                        sweights.Append(export.ToString(weights[i, j]));
                    }
                }
                return export.WriteDefinition("(BOUNDED_SURFACE()B_SPLINE_SURFACE(" + uDegree.ToString() + "," + vDegree.ToString() + spoles.ToString() + ")),.UNSPECIFIED.," + closed + ",.F.)B_SPLINE_SURFACE_WITH_KNOTS(("
                    + export.ToString(uMults, false) + "),(" + export.ToString(vMults, false) + "),(" + export.ToString(uKnots) + "),(" + export.ToString(vKnots) + "),.UNSPECIFIED.)GEOMETRIC_REPRESENTATION_ITEM()RATIONAL_B_SPLINE_SURFACE((("
                    + sweights + ")))REPRESENTATION_ITEM('')SURFACE())");
            }
            else
            {
                return export.WriteDefinition("B_SPLINE_SURFACE_WITH_KNOTS(''," + uDegree.ToString() + "," + vDegree.ToString() + spoles.ToString() + ")),.UNSPECIFIED.," + closed + ",.F.,("
                    + export.ToString(uMults, false) + "),(" + export.ToString(vMults, false) + "),(" + export.ToString(uKnots) + "),(" + export.ToString(vKnots) + "),.UNSPECIFIED.)");
            }
        }
    }
}
