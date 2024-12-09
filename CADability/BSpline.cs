using CADability.Attribute;
using CADability.Curve2D;
using CADability.UserInterface;
using System;
using System.Collections.Generic;
#if WEBASSEMBLY
using CADability.WebDrawing;
using Point = CADability.WebDrawing.Point;
#else
using System.Drawing;
using Point = System.Drawing.Point;
#endif
using System.Runtime.Serialization;
using System.Threading;
using Wintellect.PowerCollections;
using MathNet.Numerics.LinearAlgebra.Double;

namespace CADability.GeoObject
{

    public class BSplineException : ApplicationException
    {
        public BSplineException(string msg)
            : base(msg)
        {
        }
    }

    /* NURBS aus OpenCascade übernehmen:
     * BSplCLib::D0 in BSplCLib_CurveComputation.gxx ist die gesuchte Funktion. Verzeichnis C:\OpenCASCADE5.2\ros\inc
     * in C:\OpenCASCADE5.2\ros\src\BSplCLib befinden sich offensichtlich die wichtigen Unterfunktionen
     */

    /// <summary>
    /// A BSpline is a smooth curve defined by a set of control points. It is implemented as a NURBS - non uniform rational b-spline.
    /// </summary>
    [Serializable]
    public class BSpline : IGeoObjectImpl, IColorDef, ILineWidth, ILinePattern, ISerializable, ICurve, IExplicitPCurve3D, IExportStep
    {
        // im folgenden die wesentlichen Daten zur Darstellung:
        private GeoPoint[] poles; // die Kontrollpunkte, deren Anzahl bestimmt die Größe der anderen Arrays
        private double[] weights; // kann leer sein oder genausogroß wie poles (letzerer Fall ist rational)
        private double[] knots; // die Knotenwerte (für non uniform)
        private int[] multiplicities; // wie oft wird jeder Knotenwert verwendet
        // die Summe der multiplicities ist (Anzahl poles)-degree-1 (offen) bzw.
        // die Summe der multiplicities ohne die letzte ist (Anzahl poles) (geschlossen)
        internal int degree; // Grad (muss private sein, nur zum Debuggen internal gemacht)
        private bool periodic; // geschlossen
        private double startParam; // geht hier los
        private double endParam; // und endet da
        // im folgenden die erzeugenden Daten, die aber nicht vorhanden sein müssen
        // wenn vorhanden, werden sie trotzdem mit abgespeichert.
        private int maxDegree; // diese Angabe soll beim Erzeugen aus den Punkten verwendet werden
        // Im 3d-Fall handelt es sich um eine Kurve, die nicht (notwendig) in einer Ebene liegt.
        private GeoPoint[] throughPoints3d; // geht durch diese Punkte
        private GeoVector[] direction3D; // hat optional diese Richtungen
        private double[] throughPointsParam; // die Parameter der throughPoints3d
        // alternativ dazu liegen alle Punkte in einer Ebene
        // TODO: wieder wegmachen, wozu eigentlich?
        // Plane hat eine Methode aus vielen Punkten die Ebene zu finden
        private Plane? plane; // liegt in dieser Ebene, wenn spezifiziert
#if NET_45
        private WeakReference<ExplicitPCurve3D> explicitPCurve3D;
#else 
        private WeakReference explicitPCurve3D;
#endif
        // NurbsHelper Daten
        private bool nurbsHelper;
        // nur eine der 4 folgenden Variablen ist gültig, wenn nurbsHelper true ist (sonst keine)
        // es ist allemal günstiger diese 4 zu halten als alles auf 3d und homogen zu rechnen
        private Nurbs<GeoPoint, GeoPointPole> nubs3d;
        private Nurbs<GeoPointH, GeoPointHPole> nurbs3d;
        private Nurbs<GeoPoint2D, GeoPoint2DPole> nubs2d;
        private Nurbs<GeoPoint2DH, GeoPoint2DHPole> nurbs2d;
        private GeoPoint[] interpol; // Interpolation mit einer gewissen Genauigkeit
        private GeoVector[] interdir; // Interpolation mit einer gewissen Genauigkeit
        private double[] interparam; // die Parameter zur Interpolation
        private BoundingCube extent;
        private TetraederHull tetraederHull;
        private GeoPoint[] approximation; // Interpolation mit der Genauigkeit der Auflösung
        private double approxPrecision; // Genauigkeit zu approximation
        private readonly object lockApproximationRecalc = new object();
        private readonly object planeLock = new object();
        private readonly object bSplineLock = new object();
        private WeakReference extrema;
        private GeoPoint[] GetCashedApproximation(double precision)
        {
            lock (lockApproximationRecalc)
            {
                if (((precision > 0) && (approxPrecision > precision)) || approximation == null)
                {
                    approxPrecision = precision;
                    ICurve cv = (this as ICurve).Approximate(true, precision);
                    if (cv is Path)
                    {
                        Path path = (cv as Path);
                        approximation = new GeoPoint[path.CurveCount + 1];
                        for (int i = 0; i < path.CurveCount; ++i)
                        {
                            approximation[i] = path.Curve(i).StartPoint;
                        }
                        approximation[path.CurveCount] = path.Curve(path.CurveCount - 1).EndPoint;
                    }
                    else if (cv is Polyline)
                    {
                        approximation = (cv as Polyline).Vertices;
                    }
                    else if (cv is Line)
                    {
                        approximation = new GeoPoint[2];
                        approximation[0] = cv.StartPoint;
                        approximation[1] = cv.EndPoint;
                    }
                    else
                    {
                        throw new ApplicationException("internal error BSpline.GetApproximation");
                    }
                }
                return approximation;
            }
        }

        public void GetData(out GeoPoint[] poles, out double[] weights, out double[] knots, out int degree)
        {   // nur intern für OpenGL Test
            poles = (GeoPoint[])this.poles.Clone();
            weights = (double[])this.weights.Clone();
            List<double> lknots = new List<double>();
            for (int i = 0; i < multiplicities.Length; ++i)
            {
                for (int j = 0; j < multiplicities[i]; ++j)
                {
                    lknots.Add(this.knots[i]);
                }
            }
            knots = lknots.ToArray();
            degree = this.degree;
        }
        public bool IsSingular
        {
            get
            {
                for (int i = 1; i < poles.Length; ++i)
                {
                    if (!Precision.IsEqual(poles[0], poles[i])) return false;
                }
                return true;
            }
        }
        private void MakeNurbsHelper()
        {
            lock (bSplineLock)
            {
                if (nurbsHelper) return; // has already been calculated
                // Knotenliste ist von allem anderen unabhängig
                if (periodic)
                {   // folgendes wurde notwendig wg. "Ele_matrice.stp". Dort gibt es Splines vom Grad 9 und periodic
                    // denen ein Pol fehlt. Aber durch zufügen des ersten Pols werden Sie nicht ganz richtig.
                    int msum = 0;
                    for (int i = 0; i < multiplicities.Length; i++)
                    {
                        msum += multiplicities[i];
                    }
                    while (msum - degree - 1 < poles.Length)
                    {
                        if (multiplicities[0] < multiplicities[multiplicities.Length - 1])
                        {
                            ++multiplicities[0];
                        }
                        else
                        {
                            ++multiplicities[multiplicities.Length - 1];
                        }
                        ++msum;
                    }
                    if (msum - degree - 1 < poles.Length)
                    {
                        List<GeoPoint> lpoles = new List<GeoPoint>(poles);
                        lpoles.Add(poles[0]);
                        poles = lpoles.ToArray();
                        if (weights != null)
                        {
                            List<double> lweights = new List<double>(weights);
                            lweights.Add(weights[0]);
                            weights = lweights.ToArray();
                        }
                    }
                }
                List<double> knotslist = new List<double>();
                for (int i = 0; i < knots.Length; ++i)
                {
                    for (int j = 0; j < multiplicities[i]; ++j)
                    {
                        knotslist.Add(knots[i]);
                    }
                }
                if (periodic && poles.Length > 2)
                {
                    double dknot = knots[knots.Length - 1] - knots[0];
                    // letztlich ist es komisch, dass zwei knoten vornedran müssen
                    //for (int i = 0; i < 1; ++i) // 
                    //{
                    //    knotslist.Insert(0, knotslist[knotslist.Count - degree - i] - dknot);
                    //}
                    //for (int i = 0; i < 2 * degree - 2; ++i)
                    //{
                    //    knotslist.Add(knotslist[2 * (degree - 1) + i] + dknot);
                    //}
                    // neue Idee: 
                    // 1. es werden immer "degree" poles hinten angehängt
                    // 2. der 1. Knoten muss immer degree+1 mal vorkommen ( siehe STEP/piece0: dort sind alle Knoten 4-fach
                    //    bei degree=5 und FindSpan muss immer eine Stelle finden, an der es gerade wechselt
                    // 3. es werden soviele Knoten hinten angehängt, dass "knotslist.Length-degree-1 ==  poles.length" gilt
                    //for (int i = 0; i < 1; ++i) // 
                    //{
                    //    knotslist.Insert(0, knotslist[knotslist.Count - degree - i] - dknot);
                    //}
                    int secondknotindex = multiplicities[0];
                    for (int i = 0; i <= degree - multiplicities[0]; ++i)
                    {
                        knotslist.Insert(0, knotslist[knotslist.Count - degree - i] - dknot);
                        ++secondknotindex;
                    }
                    while (knotslist.Count - degree - 1 < poles.Length + degree)
                    {
                        knotslist.Add(knotslist[secondknotindex] + dknot);
                        ++secondknotindex;
                    }
                }
                // PlanarState.UnderDetermined makes sometimes problems
                if ((this as ICurve).GetPlanarState() == PlanarState.Planar || (this as ICurve).GetPlanarState() == PlanarState.UnderDetermined)
                {   // in Wirklichkeit ein 2d spline, nur im Raum gelegen
                    if ((this as ICurve).GetPlanarState() == PlanarState.UnderDetermined)
                    {
                        if (plane == null) plane = getPlane();
                        //GeoVector nrm = poles[poles.Length - 1] - poles[0];
                        //if (nrm.IsNullVector()) nrm = poles[poles.Length - 2] - poles[0];
                        //Plane tmp = new Plane(poles[0], nrm);
                        //this.plane = new Plane(tmp.Location, tmp.DirectionX, tmp.Normal);
                    }
                    if (weights == null)
                    {
                        GeoPoint2D[] npoles;
                        if (periodic)
                        {
                            npoles = new GeoPoint2D[poles.Length + degree];
                            for (int i = 0; i < poles.Length; ++i)
                            {
                                npoles[i] = plane.Value.Project(poles[i]);
                            }
                            for (int i = 0; i < degree; ++i)
                            {
                                npoles[poles.Length + i] = plane.Value.Project(poles[i]);
                            }

                        }
                        else
                        {
                            npoles = new GeoPoint2D[poles.Length];
                            for (int i = 0; i < poles.Length; ++i)
                            {
                                npoles[i] = plane.Value.Project(poles[i]);
                            }
                        }
                        this.nubs2d = new Nurbs<GeoPoint2D, GeoPoint2DPole>(degree, npoles, knotslist.ToArray());
                        nubs2d.InitDeriv1();
                    }
                    else
                    {
                        GeoPoint2DH[] npoles;
                        if (plane == null) plane = getPlane();
                        if (periodic && poles.Length > 2)
                        {
                            npoles = new GeoPoint2DH[poles.Length + degree];
                            for (int i = 0; i < poles.Length; ++i)
                            {
                                npoles[i] = new GeoPoint2DH(plane.Value.Project(poles[i]), weights[i]);
                            }
                            for (int i = 0; i < degree; ++i)
                            {
                                npoles[poles.Length + i] = new GeoPoint2DH(plane.Value.Project(poles[i]), weights[i]);
                            }
                        }
                        else
                        {
                            npoles = new GeoPoint2DH[poles.Length];
                            for (int i = 0; i < poles.Length; ++i)
                            {
                                npoles[i] = new GeoPoint2DH(plane.Value.Project(poles[i]), weights[i]);
                            }
                        }
                        this.nurbs2d = new Nurbs<GeoPoint2DH, GeoPoint2DHPole>(degree, npoles, knotslist.ToArray());
                        //int dbg = nurbs2d.CurveKnotIns(knotslist[0], degree, out double[] newkn, out GeoPoint2DH[] newpo);
                        nurbs2d.InitDeriv1();
                    }
                }
                else
                {   // echte 3d Kurve
                    if (weights == null)
                    {
                        GeoPoint[] npoles;
                        if (periodic && poles.Length > 2)
                        {
                            npoles = new GeoPoint[poles.Length + 2 * degree - 2];
                            for (int i = 0; i < poles.Length; ++i)
                            {
                                npoles[i] = poles[i];
                            }
                            for (int i = 0; i < 2 * degree - 2; ++i)
                            {
                                npoles[poles.Length + i] = poles[i];
                            }
                        }
                        else
                        {
                            npoles = new GeoPoint[poles.Length];
                            for (int i = 0; i < poles.Length; ++i)
                            {
                                npoles[i] = poles[i];
                            }
                        }
                        this.nubs3d = new Nurbs<GeoPoint, GeoPointPole>(degree, npoles, knotslist.ToArray());
                        nubs3d.InitDeriv1();
                    }
                    else
                    {
                        GeoPointH[] npoles;
                        if (periodic && poles.Length > 2)
                        {
                            npoles = new GeoPointH[poles.Length + degree];
                            for (int i = 0; i < poles.Length; ++i)
                            {
                                npoles[i] = new GeoPointH(poles[i], weights[i]);
                            }
                            for (int i = 0; i < degree; ++i)
                            {
                                npoles[poles.Length + i] = new GeoPointH(poles[i], weights[i]);
                            }
                            // war vorher so, gab aber mitunter falsch Anzahl von Poles zu knots
                            //npoles = new GeoPointH[poles.Length + 2 * degree - 2];
                            //for (int i = 0; i < poles.Length; ++i)
                            //{
                            //    npoles[i] = new GeoPointH(poles[i], weights[i]);
                            //}
                            //for (int i = 0; i < 2 * degree - 2; ++i)
                            //{
                            //    npoles[poles.Length + i] = new GeoPointH(poles[i], weights[i]);
                            //}
                        }
                        else
                        {
                            npoles = new GeoPointH[poles.Length];
                            for (int i = 0; i < poles.Length; ++i)
                            {
                                npoles[i] = new GeoPointH(poles[i], weights[i]);
                            }
                        }
                        this.nurbs3d = new Nurbs<GeoPointH, GeoPointHPole>(degree, npoles, knotslist.ToArray());
                        nurbs3d.InitDeriv1();
                    }
                }
                nurbsHelper = true;
            }
        }
        private void MakeInterpol()
        {   // die Interpolation geht mindestens durch die Knotenpunkte
            // die Abweichung von der Kurve wird erstmal auf ein Verhältnis zur Gesamtgröße festgelegt
            BoundingCube ext = new BoundingCube(poles);
            double maxError = Math.Max(ext.Size / 100.0, Precision.eps * 100); // testweise 1/100 der gesamten Ausdehnung
            // Ein Problem bleibt bestehen. Der Fehlertest in der Mitte des Segments ist unsicher, denn bei einer
            // Art Wendepunkt kann das Segment die Sehne schneiden und so fälschlicherweise einen zu kleinen
            // Fehler melden. Man müsste auch noch die mittlere Richtung betrachten, aber mit welcher Abweichung?
            List<GeoPoint> pointList = new List<GeoPoint>();
            List<GeoVector> dirList = new List<GeoVector>();
            List<double> paramList = new List<double>();
            GeoPoint sp;
            GeoVector sd;
            PointDirAtParam(startParam, out sp, out sd);
            pointList.Add(sp);
            dirList.Add(sd);
            paramList.Add(startParam);
            for (int i = 0; i < knots.Length - 1; ++i)
            {
                InsertInterpol(knots[i], knots[i + 1], pointList, dirList, paramList, maxError);
            }
            interpol = pointList.ToArray();
            interdir = dirList.ToArray();
            interparam = paramList.ToArray();
        }
        private void InsertInterpol(double sp, double ep, List<GeoPoint> pointList, List<GeoVector> dirList, List<double> paramList, double maxError)
        {
            // Der Punkt am Anfang ist schon drin
            GeoPoint point;
            GeoVector dir;
            PointDirAtParam(ep, out point, out dir);
            // jetzt überprüfen, ob der Fehler klein genug
            Plane pln;
            GeoVector dirs = dirList[dirList.Count - 1];
            GeoPoint spoint = pointList[pointList.Count - 1];
            if (plane.HasValue) pln = plane.Value;
            else
            {   // die durch die beiden Vektoren aufgespannte Ebene
                if (Precision.SameDirection(dirs, dir, false))
                {   // vermutlich eine Linie
                    if (Precision.SameDirection(dirs, GeoVector.ZAxis, false))
                    {
                        pln = new Plane(GeoPoint.Origin, dirs, GeoVector.XAxis);
                    }
                    else
                    {
                        pln = new Plane(GeoPoint.Origin, dirs, GeoVector.ZAxis);
                    }
                }
                else
                {
                    try
                    {
                        pln = new Plane(GeoPoint.Origin, dirs, dir);
                    }
                    catch (PlaneException)
                    {
                        try
                        {
                            if (Precision.SameDirection(dirs, GeoVector.ZAxis, false))
                            {
                                pln = new Plane(GeoPoint.Origin, dirs, GeoVector.XAxis);
                            }
                            else
                            {
                                pln = new Plane(GeoPoint.Origin, dirs, GeoVector.ZAxis);
                            }
                        }
                        catch (PlaneException)
                        {
                            pln = Plane.XYPlane;
                        }
                    }
                }
            }
            // jetzt in der Ebene testen
            GeoPoint mpoint;
            GeoVector mdir;
            PointDirAtParam((sp + ep) / 2.0, out mpoint, out mdir);
            double merror = Geometry.DistPL(mpoint, spoint, point);
            if (merror > maxError)
            {
                InsertInterpol(sp, (sp + ep) / 2.0, pointList, dirList, paramList, maxError);
                InsertInterpol((sp + ep) / 2.0, ep, pointList, dirList, paramList, maxError);
            }
            else
            {
                GeoVector2D dirs2d = pln.Project(dirs);
                GeoVector2D dirm2d = pln.Project(mdir);
                GeoVector2D dire2d = pln.Project(dir);
                // wenn dirm in dem von dirs und dire aufgespannten Bereich liegt, dann soll es gut sein
                // liese sich auch durch ein 2x2 lineares system lösen
                //SweepAngle sa = new SweepAngle(dirs2d, dire2d);
                //SweepAngle sm = new SweepAngle(dirs2d, dirm2d);
                //bool ok = false;
                //if (sa >= 0 && sm >= 0) ok = sm <= sa;
                //if (sa < 0 && sm < 0) ok = sm >= sa;
                // Die Frage ist, wie groß ist der Fehler. Man kann nicht einfach in der Mitte testen, 
                // da der Verlauf der Kurve nicht bekannt ist, insbesondere ein Wendepunkt kann zu Fehlern
                // führen. So testen wir hier jeweils noch die 1/4 und 3/4 Punkte um einigerm´ßen sicher
                // zu gehen
                GeoPoint tmppoint = PointAtParam(sp + (ep - sp) * 0.25);
                double error1 = Geometry.DistPL(tmppoint, spoint, point);
                tmppoint = PointAtParam(sp + (ep - sp) * 0.75);
                double error2 = Geometry.DistPL(tmppoint, spoint, point);
                // bool ok = error1 <= merror && error2 <= merror; // führt zu endlosrekursion
                bool ok = error1 <= maxError && error2 <= maxError;
                if (ok)
                {
                    pointList.Add(point);
                    dirList.Add(dir);
                    paramList.Add(ep);
                }
                else
                {
                    InsertInterpol(sp, (sp + ep) / 2.0, pointList, dirList, paramList, maxError);
                    InsertInterpol((sp + ep) / 2.0, ep, pointList, dirList, paramList, maxError);
                }
            }
        }
        // private CndOCas.Edge oCasBuddy; in GeneralCurve implementiert.
        // Wenn alles von GeneralCurve implementiert ist, dann kann man diese Klasse
        // auch überspringen und IGeoObjectImpl als Basis nehmen. Hier also erstmal
        // die quick-and-dirty Implementierung.
        #region polymorph construction
        public delegate BSpline ConstructionDelegate();
        public static ConstructionDelegate Constructor;
        public static BSpline Construct()
        {
            if (Constructor != null) return Constructor();
            return new BSpline();
        }
        public delegate void ConstructedDelegate(BSpline justConstructed);
        public static event ConstructedDelegate Constructed;
        #endregion
        protected BSpline()
            : base()
        {            
            if (Constructed != null) Constructed(this);
            extent = BoundingCube.EmptyBoundingCube;
        }
        private void InvalidateSecondaryData()
        {
            lock (bSplineLock)
            {
                nurbsHelper = false;
                nubs3d = null;
                nurbs3d = null;
                nubs2d = null;
                nurbs2d = null;
                plane = null;
                interpol = null;
                interdir = null;
                interparam = null;
                approximation = null;
                extent = BoundingCube.EmptyBoundingCube;
                tetraederHull = null;
                extrema = null;
            }
        }
        private TetraederHull TetraederHull
        {   // alle Kurven sollte zusätzlich ein Interface implementieren ITetraederHull, die genau diese Property überschreibt
            // dann kann man immer ungeachtet der Kurvenart auf die Hülle zugreifen
            get
            {
                if (tetraederHull == null)
                {
                    tetraederHull = new TetraederHull(this);
                }
                return tetraederHull;
            }
        }
#if DEBUG
#endif
        #region Methoden zum Initialisieren/Setzen
        new private class Changing : IGeoObjectImpl.Changing
        {
            private BSpline bSpline;
            public Changing(BSpline bSpline, bool keepNurbs)
                : base(bSpline, "CopyGeometry", bSpline.Clone())
            {
                lock (bSpline)
                {
                    bSpline.nurbsHelper = false;
                    bSpline.plane = null;
                    bSpline.interpol = null;
                    bSpline.interdir = null;
                    bSpline.interparam = null;
                    if (!keepNurbs)
                    {
                        bSpline.nubs3d = null;
                        bSpline.nurbs3d = null;
                        bSpline.nubs2d = null;
                        bSpline.nurbs2d = null;
                    }
                    this.bSpline = bSpline;
                }
            }
            public Changing(BSpline bSpline)
                : base(bSpline, "CopyGeometry", bSpline.Clone())
            {
                bSpline.InvalidateSecondaryData();
                this.bSpline = bSpline;
            }
            public Changing(BSpline bSpline, string PropertyName)
                : base(bSpline, PropertyName)
            {
                bSpline.InvalidateSecondaryData();
                this.bSpline = bSpline;
            }
            public Changing(BSpline bSpline, string MethodOrPropertyName, params object[] Parameters)
                : base(bSpline, MethodOrPropertyName, Parameters)
            {
                bSpline.InvalidateSecondaryData();
                this.bSpline = bSpline;
            }
            public Changing(BSpline bSpline, Type interfaceForMethod, string MethodOrPropertyName, params object[] Parameters)
                : base(bSpline, interfaceForMethod, MethodOrPropertyName, Parameters)
            {
                bSpline.InvalidateSecondaryData();
                this.bSpline = bSpline;
            }
            public override void Dispose()
            {
                base.Dispose();
#if DEBUG
                if (bSpline.knots.Length == 2 && bSpline.knots[0] == bSpline.knots[1])
                {
                }
#endif
            }

        }
        /// <summary>
        /// Makes this BSpline go through the given points. Previous data of this BSpline
        /// (if any) is discarded. The BSpline remembers both this points and the calculated
        /// poles, multiplicities, knots and weights values. If all points lie in a single plane
        /// it is better to use the appropriate ThroughPoints method.
        /// </summary>
        /// <param name="points">List of points to pass through</param>
        /// <param name="maxDegree">maximum degree for the BSpline. Must be between 3 an 25</param>
        /// <param name="closed">true if the resulting BSpline should be closed</param>
        /// <returns>success</returns>
        public bool ThroughPoints(GeoPoint[] points, int maxDegree, bool closed)
        {
            try
            {
                if (points.Length < 2) return false;
                if (points.Length == 2 && (points[0] | points[1]) < Precision.eps) return false;

                maxDegree = Math.Min(maxDegree, points.Length - 1); // bei 2 Punkten nur 1. Grad, also Linie, u.s.w
                Nurbs<GeoPoint, GeoPointPole> tmp = new Nurbs<GeoPoint, GeoPointPole>(maxDegree, points, (points.Length > 2) && closed, out throughPointsParam, true); // Hennings neue Methode
                // kann in der Zeile vorher rausfliegen, also nubs3d dort noch nicht überschreiben
                using (new Changing(this))
                {
                    nubs3d = tmp;
                    throughPoints3d = points;
                    poles = nubs3d.Poles;
                    double[] flatknots = nubs3d.UKnots;
                    List<double> hknots = new List<double>();
                    List<int> hmult = new List<int>();
                    for (int i = 0; i < flatknots.Length; ++i)
                    {
                        if (i == 0)
                        {
                            hknots.Add(flatknots[i]);
                            hmult.Add(1);
                        }
                        else
                        {
                            if (flatknots[i] == hknots[hknots.Count - 1])
                            {
                                ++hmult[hmult.Count - 1];
                            }
                            else
                            {
                                hknots.Add(flatknots[i]);
                                hmult.Add(1);
                            }
                        }
                    }
                    // weights wieder rausschmeißen, aber z.Z. gibt es noch Probleme, wenn null
                    weights = new double[poles.Length];
                    for (int i = 0; i < weights.Length; ++i)
                    {
                        weights[i] = 1.0;
                    }
                    knots = hknots.ToArray();
                    multiplicities = hmult.ToArray();
                    this.degree = maxDegree;
                    this.periodic = closed;
                    this.startParam = knots[0];
                    this.endParam = knots[knots.Length - 1];
                    nubs3d.InitDeriv1();
                    //// DEBUG: wo sind die durchgangspunkte?
                    //for (int i = 0; i < throughPointsParam.Length; ++i)
                    //{
                    //    GeoPoint p = PointAtParam(throughPointsParam[i]);
                    //}
                    //// END DEBUG
                    return true;
                }
            }
            catch (NurbsException)
            {
                return false;
            }
        }
        //		public bool ThroughPoints(Plane p, GeoPoint2D [] points, int maxDegree, bool closed)
        //		{
        //			double [] pointarray = new double[points.Length*2];
        //			for (int i=0; i<points.Length; ++i)
        //			{
        //				pointarray[2*i] = points[i].x;
        //				pointarray[2*i+1] = points[i].y;
        //			}
        //			try
        //			{
        //				CndOCas.GeomCurve2DClass curve2d = new CndOCas.GeomCurve2DClass();
        //				curve2d.MakeBSpline(points.Length,ref pointarray[0],8);
        //				CndOCas.Edge edg = curve2d.MakeEdge(p.OCasBuddy);
        //				bSplineEdge = edg.GetBSplineEdge();
        //				int np = bSplineEdge.NumPoles;
        //				int nk = bSplineEdge.NumKnots;
        //				// hier angekommen kann man davon ausgehen, dass nichts mehr schief geht
        //				// die Erzeugungsdaten behalten, obwohl sie ja nicht von Bedeutung für die
        //				// Darstellung sind.
        //				GeoObjectChangeEvent ce = MakeChangeAllEvent();
        //				projectionData.Clear();
        //				FireWillChange(ce);
        //				plane = p;
        //				throughPoints2d = points;
        //				throughPoints3d = null;
        //
        //				gp.Pnt [] tpoles = new gp.Pnt[np];
        //				bSplineEdge.GetPoles(ref tpoles[0]);
        //				poles = new GeoPoint [np];
        //				for (int i=0; i<np; ++i) poles[i] = new GeoPoint(tpoles[i]);
        //				weights = new double[np];
        //				bSplineEdge.GetWeights(ref weights[0]);
        //
        //				knots = new double [nk];
        //				bSplineEdge.GetKnots(ref knots[0]);
        //				multiplicities = new int [nk];
        //				bSplineEdge.GetMultiplicities(ref multiplicities[0]);
        //				degree = bSplineEdge.Degree;
        //				periodic = bSplineEdge.IsPeriodic!=0;
        //				startParam = bSplineEdge.StartParameter;
        //				endParam = bSplineEdge.EndParameter;
        //				SetOcasBuddy(edg.GetGeneralEdge());
        //				FireDidChange(ce);
        //				return true;
        //			} 
        //			catch (OpenCascade.Exception)
        //			{
        //				return false;
        //			}
        //		}
        public bool ThroughPoints(GeoPoint[] points, GeoVector[] directions, int maxDegree, bool closed)
        {
            try
            {
                if (points.Length < 2) return false;

                maxDegree = Math.Min(maxDegree, 2 * points.Length - 1); // bei 2 Punkten nur 1. Grad, also Linie, u.s.w
                GeoPoint[] dirs = new GeoPoint[directions.Length]; // wir brauchen Punkte statt vektoren, weil Nurbs keine Vektoren kennt
                for (int i = 0; i < dirs.Length; ++i)
                {
                    dirs[i] = GeoPoint.Origin + directions[i];
                }
                Nurbs<GeoPoint, GeoPointPole> tmp = new Nurbs<GeoPoint, GeoPointPole>(maxDegree, points, dirs, closed);
                // kann in der Zeile vorher rausfliegen, also nubs3d dort noch nicht überschreiben
                using (new Changing(this))
                {
                    nubs3d = tmp;
                    throughPoints3d = points;
                    poles = nubs3d.Poles;
                    double[] flatknots = nubs3d.UKnots;
                    List<double> hknots = new List<double>();
                    List<int> hmult = new List<int>();
                    for (int i = 0; i < flatknots.Length; ++i)
                    {
                        if (i == 0)
                        {
                            hknots.Add(flatknots[i]);
                            hmult.Add(1);
                        }
                        else
                        {
                            if (flatknots[i] == hknots[hknots.Count - 1])
                            {
                                ++hmult[hmult.Count - 1];
                            }
                            else
                            {
                                hknots.Add(flatknots[i]);
                                hmult.Add(1);
                            }
                        }
                    }
                    // weights wieder rausschmeißen, aber z.Z. gibt es noch Probleme, wenn null
                    weights = new double[poles.Length];
                    for (int i = 0; i < weights.Length; ++i)
                    {
                        weights[i] = 1.0;
                    }
                    knots = hknots.ToArray();
                    multiplicities = hmult.ToArray();
                    this.degree = maxDegree;
                    this.periodic = closed;
                    this.startParam = knots[0];
                    this.endParam = knots[knots.Length - 1];
                    nubs3d.InitDeriv1();
                    return true;
                }

                //if (points.Length < 2) return false;
                //CndHlp3D.GeoPoint3D[] hlppoints = new CndHlp3D.GeoPoint3D[points.Length];
                //for (int i = 0; i < points.Length; ++i)
                //{
                //    hlppoints[i] = (CndHlp3D.GeoPoint3D)points[i].ToCndHlp();
                //}
                //CndHlp3D.GeoVector3D[] hlpdirs = new CndHlp3D.GeoVector3D[directions.Length];
                //for (int i = 0; i < directions.Length; ++i)
                //{
                //    hlpdirs[i] = (CndHlp3D.GeoVector3D)directions[i].ToCndHlp();
                //}
                //// doppelte Punkte sollen möglich sein, die machen ggf in dem Spline einen Knick
                //using (new Changing(this))
                //{
                //    CndHlp3D.BSpline3D hlp3d = new CndHlp3D.BSpline3D(hlppoints, hlpdirs, maxDegree, closed);
                //    (this as ICndHlp3DEdge).Edge = hlp3d;
                //    throughPoints3d = points;
                //    this.maxDegree = maxDegree;
                //    this.periodic = closed;
                //    if (poles == null) return false;

                //    //					double MaxDist;
                //    //					Plane pl = Plane.FromPoints(poles,out MaxDist);
                //    //					if (MaxDist<Precision.eps) plane = pl;

                //    return true;
                //}
            }
            catch (Exception)
            {
                return false;
            }
        }
        public bool ThroughPoints(Plane p, GeoPoint2D[] points, GeoVector2D[] directions, int maxDegree, bool closed)
        {
            return false;
        }
        // not a good implementaion:
        //public BSpline Extend(double atStart=0.1, double atEnd=0.1)
        //{
        //    BSpline res = BSpline.Construct();
        //    this.PointAtParam(-0.1);
        //    List<GeoPoint> newPoles = new List<GeoPoint>(Poles);
        //    List<double> newKnots = new List<double>(Knots);
        //    List<double> newWeights = null;
        //    if (weights!=null) newWeights = new List<double>(weights);
        //    List<int> newMultiplicities = new List<int>(Multiplicities);
        //    if (atStart > 0.0)
        //    {
        //        GeoPoint spole = (this as ICurve).StartPoint - atStart * (this as ICurve).StartDirection;
        //        newPoles.Insert(0, spole);
        //        newKnots.Insert(0, -atStart);
        //        newMultiplicities.Insert(0, 1);
        //        //newMultiplicities[1] = 1;
        //        if (newWeights != null) newWeights.Insert(0, 1);
        //    }
        //    if (atEnd > 0.0)
        //    {
        //        GeoPoint epole = (this as ICurve).EndPoint + atEnd * (this as ICurve).EndDirection;
        //        newPoles.Add(epole);
        //        newKnots.Add(1.0+atEnd);
        //        newMultiplicities.Add(1);
        //        //newMultiplicities[newMultiplicities.Count - 2] = 1;
        //        if (newWeights != null) newWeights.Add(1);
        //    }
        //    res.SetData(degree, newPoles.ToArray(), newWeights != null ? newWeights.ToArray() : null, newKnots.ToArray(), newMultiplicities.ToArray(), false);
        //    res.TrimParam(0.001,0.999);
        //    return res;
        //}
        /// <summary>
        /// Modifies the value of a pole. The Index must be between 0 and PoleCount.
        /// </summary>
        /// <param name="Index">Index of the pole</param>
        /// <param name="ThePoint">The new value</param>
        public void SetPole(int Index, GeoPoint ThePoint)
        {
            using (new Changing(this, "SetPole", Index, poles[Index]))
            {
                poles[Index] = ThePoint;
                // RecalcOcasBuddies();
                throughPoints3d = null; // Durchgangspunkte werden ungültig!
            }
        }
        public GeoPoint GetPole(int Index)
        {
            return poles[Index];
        }
        public int PoleCount
        {
            get { return poles.Length; }
        }
        public GeoPoint[] Poles
        {
            get
            {
                return poles;
            }
        }
        public double[] Knots
        {
            get
            {
                return knots;
            }
        }
        public int[] Multiplicities
        {
            get
            {
                return multiplicities;
            }
        }
        public double[] Weights
        {
            get
            {
                return weights;
            }
        }
        public int Degree { get => degree; }
        public void GetData(out int degree, out GeoPoint[] poles, out double[] weights, out double[] knots, out int[] multiplicities)
        {
            degree = this.degree;
            poles = (GeoPoint[])this.poles.Clone();
            weights = (double[])this.weights.Clone();
            knots = (double[])this.knots.Clone();
            multiplicities = (int[])this.multiplicities.Clone();
        }
        public bool ThroughPoints3dExist
        {
            get { return throughPoints3d != null; }
        }
        public bool SetData(int degree, GeoPoint[] poles, double[] weights, double[] knots, int[] multiplicities, bool periodic)
        {
            return SetData(degree, poles, weights, knots, multiplicities, periodic, knots[0], knots[knots.Length - 1]);

        }
        public bool SetData(int degree, GeoPoint[] poles, double[] weights, double[] knots, int[] multiplicities, bool periodic, double startParam, double endParam)
        {
            for (int i = 1; i < knots.Length; ++i)
            {
                if (knots[i] < knots[i - 1]) throw new ApplicationException("wrong order of knots");
            }
            InvalidateSecondaryData();
            // Bedingungen
            //-	Degree is in the range 1 to Geom_BSplineCurve::MaxDegree(),
            //-	the Poles and Weights arrays have the same dimension and this dimension is greater than or equal to 2,
            //-	the Knots and Multiplicities arrays have the same dimension and this dimension is greater than or equal to 2,
            //-	the knots sequence is in ascending order, i.e. Knots(i) is less than Knots(i+1),
            //-	the multiplicity coefficients are in the range 1 to Degree.  However, on a non-periodic curve, the first and last multiplicities 
            //  may be Degree + 1 (this is recommended if you want the curve to start and finish on the first and last poles),
            //-	on a periodic curve the first and last multiplicities must be the same,
            //-	on a non-periodic curve, the number of poles is equal to the sum of the multiplicity coefficients, minus Degree, minus 1,
            //-	on a periodic curve, the number of poles is equal to the sum of knot multiplicities, excluding the last knot.
            if (weights == null || weights.Length != poles.Length)
            {
                weights = new double[poles.Length];
                for (int i = 0; i < poles.Length; ++i)
                {
                    weights[i] = 1.0;
                }
            }
            if (multiplicities == null || multiplicities.Length != knots.Length)
            {
                List<double> knlist = new List<double>();
                List<int> mulist = new List<int>();
                for (int i = 0; i < knots.Length; ++i)
                {
                    if (i == 0 || knots[i] != knots[i - 1])
                    {
                        knlist.Add(knots[i]);
                        mulist.Add(1);
                    }
                    else
                    {
                        mulist[mulist.Count - 1] += 1;
                    }
                }

                // siehe weiter unten "if (multiplicities.Length>1)"
                multiplicities = mulist.ToArray();
                knots = knlist.ToArray();
                // leider kommen in DWG periodische mit multiplicities[0] > degree vor. Die sind in OCas nicht erlaubt
                if (multiplicities[0] > degree) periodic = false;
            }

            if (periodic)
            {
#if DEBUG
                //List<double> fknot = new List<double>();
                //for (int i = 0; i < knots.Length; i++)
                //{
                //    for (int j = 0; j < multiplicities[i]; j++)
                //    {
                //        fknot.Add(knots[i]);
                //    }
                //}
                //Nurbs<GeoPoint, GeoPointPole> nbs = new Nurbs<GeoPoint, GeoPointPole>(true, degree, poles, fknot.ToArray());
                //GeoPoint dbg = nbs.CurvePoint(1.0);
                //FromNurbs(nbs, startParam, endParam);
                //return true;
#endif
            }
            else
            {
                // es kommen manchmal zu kurze knotenvektoern vor. hier wird korrigiert
                int sum = 0;
                for (int i = 0; i < multiplicities.Length; ++i) sum += multiplicities[i];
                if (degree > poles.Length) degree = poles.Length;
                int diff = poles.Length + degree + 1 - sum;
                if (diff != 0)
                {
                    int d1 = diff / 2;
                    int d2 = diff - d1;
                    multiplicities[0] += d2;
                    multiplicities[multiplicities.Length - 1] += d1;
                }
            }
            if (startParam < knots[0]) startParam = knots[0];
            if (endParam > knots[knots.Length - 1]) endParam = knots[knots.Length - 1];
            //while (multiplicities[0] <= degree)
            //{
            //    ++multiplicities[0];
            //    for (int i = 1; i < multiplicities.Length - 1; i++)
            //    {
            //        if (multiplicities[i] > 1)
            //        {
            //            --multiplicities[i];
            //            break;
            //        }
            //    }
            //}
            //while (multiplicities[multiplicities.Length - 1] <= degree)
            //{
            //    ++multiplicities[multiplicities.Length - 1];
            //    for (int i = 1; i < multiplicities.Length - 1; i++)
            //    {
            //        if (multiplicities[i] > 1)
            //        {
            //            --multiplicities[i];
            //            break;
            //        }
            //    }
            //}
            {
                int sum = 0;
                for (int i = 0; i < multiplicities.Length; ++i) sum += multiplicities[i];
                if (degree > poles.Length) degree = poles.Length;
                int diff = -(poles.Length + degree + 1 - sum);
                if (diff > 0)
                {
                    List<GeoPoint> lpoles = new List<GeoPoint>(poles);
                    List<double> lweights = new List<double>(weights);
                    for (int i = 0; i < diff; i++)
                    {
                        if ((i & 0x1) != 0) lpoles.Insert(0, lpoles[0]);
                        else lpoles.Add(lpoles[lpoles.Count - 1]);
                        if ((i & 0x1) != 0) lweights.Insert(0, 1);
                        else lweights.Add(1);
                    }
                    poles = lpoles.ToArray();
                    weights = lweights.ToArray();
                }

            }

            this.degree = degree;
            this.poles = poles;
            this.weights = weights;
            this.knots = knots;
            this.multiplicities = multiplicities;
            this.periodic = periodic;
            this.startParam = startParam;
            this.endParam = endParam;

            throughPoints3d = null; // Durchgangspunkte werden ungültig!

            {
                int sum = 0;
                for (int i = 0; i < multiplicities.Length; ++i) sum += multiplicities[i];
                if (poles.Length + degree + 1 != sum) return false;
            }
            if (startParam > knots[0] || endParam < knots[knots.Length-1])
            {
                BSpline trimmed = TrimParam(startParam, endParam);
                CopyGeometry(trimmed);
            }
            // DEBUG:
            for (int i = 1; i < knots.Length; ++i)
            {
                if (knots[i] < knots[i - 1]) throw new ApplicationException("wrong order of knots");
            }
            return true;
        }
        public void SetThroughPoint(int Index, GeoPoint NewValue)
        {
            GeoPoint[] copy = (GeoPoint[])throughPoints3d.Clone();
            copy[Index] = NewValue;
            ThroughPoints(copy, degree, periodic);
            //			try
            //			{
            //				CndOCas.MakeEdge me = new CndOCas.MakeEdge();
            //				gp.Pnt [] tpoints = new gp.Pnt[throughPoints3d.Length];
            //				for (int i=0; i<throughPoints3d.Length; ++i)
            //				{	// hier throughPoints3d noch nicht verändern
            //					if (i==Index) tpoints[i] = NewValue.gpPnt();
            //					tpoints[i] = throughPoints3d[i].gpPnt();
            //				}
            //				CndOCas.Edge edg = me.MakeBSplineThrough(tpoints,8,periodic);
            //				// bis hierher ist der Spline noch unverändert wg. exception
            //				GeoPoint [] tmp = throughPoints3d;
            //				using (new Changing(this,"SetThroughPoint",Index,throughPoints3d[Index]))
            //				{
            //					throughPoints3d = tmp;
            //					throughPoints3d[Index] = NewValue;
            //					RecalcFromEdge(edg);
            //				}
            //			} 
            //			catch (OpenCascade.Exception)
            //			{
            //			}
        }
        public bool Approximate(GeoPoint[] vertex, double precision)
        {
            Set<int> selectedPoints = new Set<int>();
            selectedPoints.Add(0);
            selectedPoints.Add(vertex.Length - 1);
            bool closed = (vertex[0] | vertex[vertex.Length - 1]) < precision / 10.0;
            if (closed) selectedPoints.Add(vertex.Length / 2);
            if ((closed && vertex.Length < 3) || vertex.Length < 2) return false;
            // hier müsste man die "Wendepunkte" im Punktarray finden, ist ja nicht so schwer!
            while (selectedPoints.Count < vertex.Length)
            {
                GeoPoint[] tp = new GeoPoint[selectedPoints.Count];
                int i = 0;
                int[] indices = selectedPoints.ToArray();
                Array.Sort(indices);
                foreach (int k in indices) // Set<T>: The items are enumerated in sorted order. Stimmt aber nicht!
                {
                    tp[i] = vertex[k];
                    ++i;
                }
                ThroughPoints(tp, 3, closed);
                int lastk = -1;
                bool indexAdded = false;
                foreach (int k in indices)
                {
                    if (lastk < 0)
                    {
                        lastk = k;
                    }
                    else
                    {
                        i = (k + lastk) / 2;
                        bool added = false;
                        if (!selectedPoints.Contains(i))
                        {   // der Zwischenpunkt in einem Intervall
                            if ((this as ICurve).DistanceTo(vertex[i]) > precision)
                            {
                                selectedPoints.Add(i);
                                added = true;
                                indexAdded = true;
                            }
                        }
                        // es kann natürlich sein, dass der Zwischenpunkt gut ist, ein anderer aber schlecht
                        // deshalb wenigstens noch der Test ob i+1 auch ok ist
                        if (!added)
                        {
                            i = i + 1;
                            if (!selectedPoints.Contains(i))
                            {   // der andere Zwischenpunkt in einem Intervall
                                if ((this as ICurve).DistanceTo(vertex[i]) > precision)
                                {
                                    selectedPoints.Add(i);
                                    indexAdded = true;
                                }
                            }
                        }
                        lastk = k;
                    }
                }
                if (!indexAdded) return true;
            }
            return false; // darf nicht drankommen
        }
        public void ReducePoles(double precision)
        {
            GeoPoint[] vertices;
            if (ThroughPoints3dExist) vertices = throughPoints3d.Clone() as GeoPoint[];
            else vertices = KnotPoints;
            Approximate(vertices, precision);
        }
        internal GeoPoint[] CreateThroughPoints()
        {
            // Versuchsstadium
            // es soll wohl soviele Durchgangspunkte wie Pole geben
            // knoten gibt es allerdings, abhängig vom Grad, mehr.
            // sicherlich gibt es mehrere Lösungen
            double di = (double)(knots.Length - 1) / (double)(poles.Length - 1);
            GeoPoint[] res = new GeoPoint[poles.Length];
            for (int i = 0; i < res.Length; i++)
            {
                int i0 = (int)Math.Floor(i * di);
                double r = i * di - i0;
                double par;
                if (i0 == knots.Length - 1) par = knots[i0];
                else par = knots[i0] + r * (knots[i0 + 1] - knots[i0]);
                res[i] = PointAtParam(par);
            }
            return res;
        }
        public GeoPoint GetThroughPoint(int Index)
        {
            return throughPoints3d[Index];
        }
        public int ThroughPointCount
        {
            get
            {
                if (throughPoints3d == null) return 0;
                return throughPoints3d.Length;
            }
        }
        public GeoPoint[] ThroughPoint
        {
            get
            {
                return throughPoints3d;
            }
        }
        public GeoPoint[] KnotPoints
        {
            get
            {
                GeoPoint[] res = new GeoPoint[knots.Length];
                for (int i = 0; i < knots.Length; ++i)
                {
                    res[i] = this.PointAtParam(knots[i]);
                }
                return res;
            }
        }
        public bool IsClosed
        {
            get { return periodic; }
            set
            {
                if (periodic != value)
                {
                    using (new Changing(this, "IsClosed"))
                    {
                        bool done = false;
                        if (ThroughPoints3dExist)
                        {
                            done = ThroughPoints((GeoPoint[])throughPoints3d.Clone(), this.degree, value);
                        }
                        else
                        {   // öffnen macht ja nur Sinn, wenn Throughpoints vorhanden waren, wo sonst sollte man öffnen
                            done = ThroughPoints(CreateThroughPoints(), this.degree, value);
                        }
                        if (!done)
                        {
                            if (multiplicities.Length > 1)
                            {	// ob das so ganz allgemein gilt, muss noch überprüft werden, 
                                // die Bedinungen sind so:
                                // 1.	the multiplicity coefficients are in the range 1 to Degree.  
                                // However, on a non-periodic curve, the first and last multiplicities may be 
                                // Degree + 1 (this is recommended if you want the curve to start 
                                // and finish on the first and last poles),
                                // 2.	on a periodic curve the first and last multiplicities must be the same,
                                // 3.	on a non-periodic curve, the number of poles is equal to the sum of the 
                                // multiplicity coefficients, minus Degree, minus 1,
                                // 4.	on a periodic curve, the number of poles is equal to the sum of knot 
                                // multiplicities, excluding the last knot.							
                                if (value)
                                {	// es wird geschlossen
                                    // 1. Bedingung
                                    if (multiplicities[0] >= degree) multiplicities[0] = degree - 1;
                                    // 2. Bedingung:
                                    multiplicities[multiplicities.Length - 1] = multiplicities[0];
                                    // 4. Bedingung:
                                    int sum = 0;
                                    for (int i = 0; i < multiplicities.Length - 1; ++i) sum += multiplicities[i];
                                    int j = 1;
                                    while (sum > poles.Length)
                                    {
                                        if (multiplicities[j] > 1)
                                        {
                                            --sum;
                                            --multiplicities[j];
                                        }
                                        if (sum > poles.Length)
                                        {	// symmetrisch am anderen Ende
                                            if (multiplicities[multiplicities.Length - 1 - j] > 1)
                                            {
                                                --sum;
                                                --multiplicities[multiplicities.Length - j];
                                            }
                                        }
                                        ++j;
                                        if (j > multiplicities.Length / 2) j = 0;
                                    }
                                    while (sum < poles.Length)
                                    {
                                        ++sum;
                                        ++multiplicities[j];
                                        if (sum < poles.Length - 1)
                                        {	// symmetrisch am anderen Ende
                                            ++sum;
                                            ++multiplicities[multiplicities.Length - j];
                                        }
                                        ++j;
                                        if (j > multiplicities.Length / 2) j = 1;
                                    }
                                    // 2. Bedingung (schon oben)
                                    multiplicities[multiplicities.Length - 1] = multiplicities[0];
                                }
                                else
                                {	// es wird geöffnet
                                    // die 3. Bedingung muss erfüllt werden
                                    int sum = 0;
                                    for (int i = 0; i < multiplicities.Length; ++i) sum += multiplicities[i];
                                    // 3.	on a non-periodic curve, the number of poles is equal to the sum of the 
                                    // multiplicity coefficients, minus Degree, minus 1,
                                    int dif = poles.Length - (sum - degree - 1);
                                    int j = 0; // zuerst die beiden äußeren erhöhen
                                    while (dif > 0)
                                    {
                                        --dif;
                                        ++multiplicities[j];
                                        if (dif > 0)
                                        {
                                            --dif;
                                            ++multiplicities[multiplicities.Length - 1 - j];
                                        }
                                        ++j;
                                        if (j > multiplicities.Length / 2) j = 1; // nicht mehr auf die beiden äußeren
                                    }
                                    j = 1;
                                    while (dif < 0)
                                    {
                                        if (multiplicities[j] > 1)
                                        {
                                            ++dif;
                                            --multiplicities[j];
                                        }
                                        if (dif < 0)
                                        {
                                            if (multiplicities[multiplicities.Length - 1 - j] > 1)
                                            {
                                                ++dif;
                                                --multiplicities[multiplicities.Length - 1 - j];
                                            }
                                        }
                                        ++j;
                                        if (j > multiplicities.Length / 2) j = 0; // jetzt auch die beiden äußeren
                                    }
                                }
                            }
                        }
                        periodic = value;
                    }
                }
            }
        }
        internal double[] ThroughPointsParam
        {
            get
            {
                return throughPointsParam;
            }
        }
        internal void FromNurbs(Nurbs<GeoPoint, GeoPointPole> nbs, double startParam, double endParam)
        {
            nubs3d = nbs;
            FromNurbs(Plane.XYPlane);
            this.startParam = startParam;
            this.endParam = endParam;
        }
        internal void FromNurbs(Nurbs<GeoPointH, GeoPointHPole> nbs, double startParam, double endParam)
        {
            nurbs3d = nbs;
            FromNurbs(Plane.XYPlane);
            this.startParam = startParam;
            this.endParam = endParam;
        }
        private void FromNurbs(BSpline toCopy)
        {
            if (toCopy.nubs3d != null) nubs3d = toCopy.nubs3d;
            if (toCopy.nurbs3d != null) nurbs3d = toCopy.nurbs3d;
            FromNurbs(Plane.XYPlane);
            startParam = toCopy.startParam; ;
            endParam = toCopy.endParam;
        }
        private void FromNurbs(Plane pl)
        {
            // das alles gilt nicht mehr:
            interpol = null;
            interdir = null;
            interparam = null;
            throughPoints3d = null;

            if (nubs3d != null)
            {
                poles = nubs3d.Poles;
                double[] flatknots = nubs3d.UKnots;
                List<double> hknots = new List<double>();
                List<int> hmult = new List<int>();
                for (int i = 0; i < flatknots.Length; ++i)
                {
                    if (i == 0)
                    {
                        hknots.Add(flatknots[i]);
                        hmult.Add(1);
                    }
                    else
                    {
                        if (flatknots[i] == hknots[hknots.Count - 1])
                        {
                            ++hmult[hmult.Count - 1];
                        }
                        else
                        {
                            hknots.Add(flatknots[i]);
                            hmult.Add(1);
                        }
                    }
                }
                // weights wieder rausschmeißen, aber z.Z. gibt es noch Probleme, wenn null
                weights = new double[poles.Length];
                for (int i = 0; i < weights.Length; ++i)
                {
                    weights[i] = 1.0;
                }
                knots = hknots.ToArray();
                multiplicities = hmult.ToArray();
                this.degree = nubs3d.UDegree;
                this.periodic = false; // woher nehmen?
                this.startParam = knots[0];
                this.endParam = knots[knots.Length - 1];
                nubs3d.InitDeriv1();
            }
            else if (nurbs3d != null)
            {
                poles = new GeoPoint[nurbs3d.Poles.Length];
                for (int i = 0; i < poles.Length; ++i)
                {
                    poles[i] = (GeoPoint)nurbs3d.Poles[i];
                }
                weights = new double[poles.Length];
                for (int i = 0; i < weights.Length; ++i)
                {
                    weights[i] = nurbs3d.Poles[i].w;
                }
                double[] flatknots = nurbs3d.UKnots;
                List<double> hknots = new List<double>();
                List<int> hmult = new List<int>();
                for (int i = 0; i < flatknots.Length; ++i)
                {
                    if (i == 0)
                    {
                        hknots.Add(flatknots[i]);
                        hmult.Add(1);
                    }
                    else
                    {
                        if (flatknots[i] == hknots[hknots.Count - 1])
                        {
                            ++hmult[hmult.Count - 1];
                        }
                        else
                        {
                            hknots.Add(flatknots[i]);
                            hmult.Add(1);
                        }
                    }
                }
                knots = hknots.ToArray();
                multiplicities = hmult.ToArray();
                this.degree = nurbs3d.UDegree;
                this.periodic = false; // woher nehmen?
                this.startParam = knots[0];
                this.endParam = knots[knots.Length - 1];
                nurbs3d.InitDeriv1();
            }
            else if (nubs2d != null)
            {
                // Plane pl = (this as ICurve).GetPlane();
                poles = new GeoPoint[nubs2d.Poles.Length];
                for (int i = 0; i < poles.Length; ++i)
                {
                    poles[i] = pl.ToGlobal(nubs2d.Poles[i]);
                }
                weights = new double[poles.Length];
                for (int i = 0; i < weights.Length; ++i)
                {
                    weights[i] = 1.0;
                }
                double[] flatknots = nubs2d.UKnots;
                List<double> hknots = new List<double>();
                List<int> hmult = new List<int>();
                for (int i = 0; i < flatknots.Length; ++i)
                {
                    if (i == 0)
                    {
                        hknots.Add(flatknots[i]);
                        hmult.Add(1);
                    }
                    else
                    {
                        if (flatknots[i] == hknots[hknots.Count - 1])
                        {
                            ++hmult[hmult.Count - 1];
                        }
                        else
                        {
                            hknots.Add(flatknots[i]);
                            hmult.Add(1);
                        }
                    }
                }
                knots = hknots.ToArray();
                multiplicities = hmult.ToArray();
                this.degree = nubs2d.UDegree;
                this.periodic = false; // woher nehmen?
                this.startParam = knots[0];
                this.endParam = knots[knots.Length - 1];
                nurbs3d.InitDeriv1();
            }
            else if (nurbs2d != null)
            {
                // Plane pl = (this as ICurve).GetPlane();
                poles = new GeoPoint[nurbs2d.Poles.Length];
                for (int i = 0; i < poles.Length; ++i)
                {
                    poles[i] = pl.ToGlobal((GeoPoint2D)nurbs2d.Poles[i]);
                }
                weights = new double[poles.Length];
                for (int i = 0; i < weights.Length; ++i)
                {
                    weights[i] = nurbs2d.Poles[i].w;
                }
                double[] flatknots = nurbs2d.UKnots;
                List<double> hknots = new List<double>();
                List<int> hmult = new List<int>();
                for (int i = 0; i < flatknots.Length; ++i)
                {
                    if (i == 0)
                    {
                        hknots.Add(flatknots[i]);
                        hmult.Add(1);
                    }
                    else
                    {
                        if (flatknots[i] == hknots[hknots.Count - 1])
                        {
                            ++hmult[hmult.Count - 1];
                        }
                        else
                        {
                            hknots.Add(flatknots[i]);
                            hmult.Add(1);
                        }
                    }
                }
                knots = hknots.ToArray();
                multiplicities = hmult.ToArray();
                this.degree = nurbs2d.UDegree;
                this.periodic = false; // woher nehmen?
                this.startParam = knots[0];
                this.endParam = knots[knots.Length - 1];
                nurbs2d.InitDeriv1();
            }

            // DEBUG:
            for (int i = 1; i < knots.Length; ++i)
            {
                if (knots[i] < knots[i - 1])
                {
                    knots[i] = knots[i - 1];
                    // throw new ApplicationException("wrong order of knots");
                }
            }
        }
#if DEBUG
        public void DebugTest()
        {
            ExplicitPCurve3D exp3d = (this as IExplicitPCurve3D).GetExplicitPCurve3D();
            for (int i = 0; i < 10000; i++)
            {
                double tst = (i * (1.0 / 10000.0)) * (knots[knots.Length - 1] - knots[0]) + knots[0];
                GeoPoint p = PointAtParam(tst);
                double pos = exp3d.PositionOf(p, tst, out double dist);
            }
        }
#endif

        #endregion
        private enum DisplayMode { showThroughPoints = 0x01, showPoles = 0x02 };
        private DisplayMode displayMode;
        public void ShowPoints(bool showThroughPoints, bool showPoles)
        {
            displayMode = (DisplayMode)0;
            if (showThroughPoints)
            {
                displayMode |= DisplayMode.showThroughPoints;
            }
            if (showPoles)
            {
                displayMode |= DisplayMode.showPoles;
            }
        }
        #region IGeoObject Members
        /// <summary>
        /// Overrides <see cref="CADability.GeoObject.IGeoObjectImpl.Modify (ModOp)"/>
        /// </summary>
        /// <param name="m"></param>
        public override void Modify(ModOp m)
        {
            using (new Changing(this, "ModifyInverse", m))
            {
                for (int i = 0; i < poles.Length; ++i)
                {
                    poles[i] = m * poles[i];
                }
                if (ThroughPoints3dExist)
                {
                    for (int i = 0; i < throughPoints3d.Length; ++i)
                    {
                        throughPoints3d[i] = m * throughPoints3d[i];
                    }
                }
            }
        }
        /// <summary>
        /// Overrides <see cref="CADability.GeoObject.IGeoObjectImpl.Clone ()"/>
        /// </summary>
        /// <returns></returns>
        public override IGeoObject Clone()
        {
            if (poles == null) return null; // es kommt ein Clone von einem noch nicht fertig initialisierten BSpline
            BSpline res = Construct();
            ++res.isChanging; // damit kine endlos rekursion entsteht
            res.poles = (GeoPoint[])this.poles.Clone(); // neues array, alte Werte
            res.weights = (double[])this.weights.Clone();
            res.knots = (double[])this.knots.Clone();
            res.multiplicities = (int[])this.multiplicities.Clone();
            res.degree = this.degree;
            res.periodic = this.periodic;
            res.startParam = this.startParam;
            res.endParam = this.endParam;
            res.maxDegree = this.maxDegree;
            if (this.throughPoints3d != null) res.throughPoints3d = (GeoPoint[])this.throughPoints3d.Clone();
            if (this.direction3D != null) res.direction3D = (GeoVector[])this.direction3D.Clone();
            res.CopyAttributes(this);
            --res.isChanging;
            return res;
        }
        /// <summary>
        /// Overrides <see cref="CADability.GeoObject.IGeoObjectImpl.CopyGeometry (IGeoObject)"/>
        /// </summary>
        /// <param name="ToCopyFrom"></param>
        public override void CopyGeometry(IGeoObject ToCopyFrom)
        {
            using (new Changing(this))
            {
                BSpline cpy = ToCopyFrom as BSpline; // muss gehen!
                poles = (GeoPoint[])cpy.poles.Clone(); // neues array, alte Werte
                weights = (double[])cpy.weights.Clone();
                knots = (double[])cpy.knots.Clone();
                multiplicities = (int[])cpy.multiplicities.Clone();
                degree = cpy.degree;
                periodic = cpy.periodic;
                startParam = cpy.startParam;
                endParam = cpy.endParam;
                maxDegree = cpy.maxDegree;
                if (cpy.throughPoints3d != null) throughPoints3d = (GeoPoint[])cpy.throughPoints3d.Clone();
                if (cpy.direction3D != null) direction3D = (GeoVector[])cpy.direction3D.Clone();
            }
        }
        /// <summary>
        /// Overrides <see cref="CADability.GeoObject.IGeoObjectImpl.GetShowProperties (IFrame)"/>
        /// </summary>
        /// <param name="Frame"></param>
        /// <returns></returns>
        public override IPropertyEntry GetShowProperties(IFrame Frame)
        {
            return new ShowPropertyBSpline(this, Frame);
        }
        /// <summary>
        /// Overrides <see cref="CADability.GeoObject.IGeoObjectImpl.CopyAttributes (IGeoObject)"/>
        /// </summary>
        /// <param name="ToCopyFrom"></param>
        public override void CopyAttributes(IGeoObject ToCopyFrom)
        {
            base.CopyAttributes(ToCopyFrom);
        }
        /// <summary>
        /// Overrides <see cref="CADability.GeoObject.IGeoObjectImpl.FindSnapPoint (SnapPointFinder)"/>
        /// </summary>
        /// <param name="spf"></param>
        public override void FindSnapPoint(SnapPointFinder spf)
        {
            if (!spf.Accept(this)) return;
            ICurve cv = this as ICurve;
            if (spf.SnapToObjectCenter)
            {
                GeoPoint Center = cv.PointAt(0.5);
                spf.Check(Center, this, SnapPointFinder.DidSnapModes.DidSnapToObjectCenter);
            }
            if (spf.SnapToObjectSnapPoint)
            {
                spf.Check(cv.StartPoint, this, SnapPointFinder.DidSnapModes.DidSnapToObjectSnapPoint);
                spf.Check(cv.EndPoint, this, SnapPointFinder.DidSnapModes.DidSnapToObjectSnapPoint);
            }
            if (spf.SnapToDropPoint && spf.BasePointValid)
            {
                if ((this as ICurve).GetPlanarState() == PlanarState.Planar)
                {
                    Plane pl = (this as ICurve).GetPlane();
                    if (Precision.IsPointOnPlane(spf.BasePoint, pl))
                    {
                        ICurve2D prcv = cv.GetProjectedCurve(pl);
                        GeoPoint2D[] fp = prcv.PerpendicularFoot(pl.Project(spf.BasePoint));
                        for (int i = 0; i < fp.Length; ++i)
                        {
                            GeoPoint toTest = pl.ToGlobal(fp[i]);
                            spf.Check(toTest, this, SnapPointFinder.DidSnapModes.DidSnapToDropPoint);
                        }
                    }
                }
            }
            if (spf.SnapToObjectPoint)
            {
                double pos = (this as ICurve).PositionOf(spf.SourcePoint3D);
                if (pos >= 0.0 && pos <= 1.0)
                    spf.Check((this as ICurve).PointAt(pos), this, SnapPointFinder.DidSnapModes.DidSnapToObjectPoint);
                //				if (plane!=null)
                //				{
                //					Plane pl = plane;
                //					ICurve2D prcv = cv.GetProjectedCurve(plane);
                //					GeoPoint2D p2d = pl.Project(spf.SourcePoint3D);
                //					double par = prcv.PositionOf(p2d);
                //					if (prcv.IsParameterOnCurve(par))
                //					{
                //						GeoPoint toTest = pl.ToGlobal(prcv.PointAt(par));
                //						spf.Check(toTest,this,SnapPointFinder.DidSnapModes.DidSnapToObjectPoint);
                //					}
                //				}
            }
            if (spf.SnapToTangentPoint && spf.BasePointValid)
            {
                if ((this as ICurve).GetPlanarState() == PlanarState.Planar)
                {
                    Plane pl = (this as ICurve).GetPlane();
                    if (Precision.IsPointOnPlane(spf.BasePoint, pl))
                    {
                        ICurve2D prcv = cv.GetProjectedCurve(plane.Value);
                        GeoPoint2D[] fp = prcv.TangentPoints(pl.Project(spf.BasePoint), spf.SourcePoint);
                        for (int i = 0; i < fp.Length; ++i)
                        {
                            GeoPoint toTest = pl.ToGlobal(fp[i]);
                            spf.Check(toTest, this, SnapPointFinder.DidSnapModes.DidSnapToTangentPoint);
                        }
                    }
                }
            }
        }
        /// <summary>
        /// Overrides <see cref="CADability.GeoObject.IGeoObjectImpl.GetBoundingCube ()"/>
        /// </summary>
        /// <returns></returns>
        public override BoundingCube GetBoundingCube()
        {
            if (extent.IsEmpty && poles != null)
            {
                extent = BoundingCube.EmptyBoundingCube;
                double[] extx = (this as ICurve).GetExtrema(GeoVector.XAxis);
                double[] exty = (this as ICurve).GetExtrema(GeoVector.YAxis);
                double[] extz = (this as ICurve).GetExtrema(GeoVector.ZAxis);
                BoundingCube res = BoundingCube.EmptyBoundingCube;
                for (int i = 0; i < extx.Length; ++i)
                {
                    extent.MinMax((this as ICurve).PointAt(extx[i]));
                }
                for (int i = 0; i < exty.Length; ++i)
                {
                    extent.MinMax((this as ICurve).PointAt(exty[i]));
                }
                for (int i = 0; i < extz.Length; ++i)
                {
                    extent.MinMax((this as ICurve).PointAt(extz[i]));
                }
                extent.MinMax((this as ICurve).StartPoint);
                extent.MinMax((this as ICurve).EndPoint);

                //ICurve2D xycurve = (this as ICurve).GetProjectedCurve(Plane.XYPlane);
                //BoundingRect extxy = xycurve.GetExtent();
                //ICurve2D xzcurve = (this as ICurve).GetProjectedCurve(Plane.XZPlane);
                //BoundingRect extxz = xzcurve.GetExtent();
                //extent.Xmin = extxy.Left;
                //extent.Xmax = extxy.Right;
                //extent.Ymin = extxy.Bottom;
                //extent.Ymax = extxy.Top;
                //extent.Zmin = extxz.Bottom;
                //extent.Zmax = extxz.Top;
            }
            return extent;
        }
        public delegate bool PaintTo3DDelegate(BSpline toPaint, IPaintTo3D paintTo3D);
        public static PaintTo3DDelegate OnPaintTo3D;
        /// <summary>
        /// Overrides <see cref="CADability.GeoObject.IGeoObjectImpl.PaintTo3D (IPaintTo3D)"/>
        /// </summary>
        /// <param name="paintTo3D"></param>
        public override void PaintTo3D(IPaintTo3D paintTo3D)
        {
            if (poles == null) return; // gibts noch nicht
            if (OnPaintTo3D != null && OnPaintTo3D(this, paintTo3D)) return;
            if (!paintTo3D.SelectMode)
            {
                if (colorDef != null) paintTo3D.SetColor(colorDef.Color);
            }
            if (lineWidth != null) paintTo3D.SetLineWidth(lineWidth);
            if (linePattern != null) paintTo3D.SetLinePattern(linePattern);
            try
            {
                GeoPoint[] points = GetCashedApproximation(paintTo3D.Precision);
                paintTo3D.Polyline(points);
            }
            catch (ApplicationException) { }
        }
        /// <summary>
        /// Overrides <see cref="CADability.GeoObject.IGeoObjectImpl.PrepareDisplayList (double)"/>
        /// </summary>
        /// <param name="precision"></param>
        public override void PrepareDisplayList(double precision)
        {
            GetCashedApproximation(precision);
        }
        /// <summary>
        /// Overrides <see cref="CADability.GeoObject.IGeoObjectImpl.GetQuadTreeItem (Projection, ExtentPrecision)"/>
        /// </summary>
        /// <param name="projection"></param>
        /// <param name="extentPrecision"></param>
        /// <returns></returns>
        public override IQuadTreeInsertableZ GetQuadTreeItem(Projection projection, ExtentPrecision extentPrecision)
        {
            return new QuadTreeBSpline(this, (this as ICurve).GetProjectedCurve(projection.ProjectionPlane) as BSpline2D, projection);
        }
        public override Style.EDefaultFor PreferredStyle
        {
            get
            {
                return Style.EDefaultFor.Curves;
            }
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
            return this.TetraederHull.HitTest(cube);
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
            ICurve2D c2d = (this as ICurve).GetProjectedCurve(projection.ProjectionPlane);
            if (onlyInside) return c2d.GetExtent() <= rect;
            else return c2d.HitTest(ref rect, false);
        }
        /// <summary>
        /// Overrides <see cref="CADability.GeoObject.IGeoObjectImpl.HitTest (Projection.PickArea, bool)"/>
        /// </summary>
        /// <param name="area"></param>
        /// <param name="onlyInside"></param>
        /// <returns></returns>
        public override bool HitTest(Projection.PickArea area, bool onlyInside)
        {
            return this.TetraederHull.HitTest(area, onlyInside);
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
            if ((this as ICurve).GetPlanarState() == PlanarState.Planar)
            {
                Plane plane = (this as ICurve).GetPlane();
                if (plane.Intersect(fromHere, direction, out GeoPoint p))
                {
                    return Geometry.LinePar(fromHere, direction, p); // nicht getestet ob BSpline auch getroffen
                }
                else
                {
                    Plane nrm = new Plane(fromHere, direction, plane.Normal);
                    double[] ips = (this as ICurve).GetPlaneIntersection(nrm);
                    double res = double.MaxValue;
                    for (int i = 0; i < ips.Length; i++)
                    {
                        GeoPoint ip = (this as ICurve).PointAt(ips[i]);
                        double d = Geometry.LinePar(fromHere, direction, ip);
                        if (d < res) res = d;
                    }
                    return res;
                }
            }
            else
            {
                GeoPoint[] vertex = GetCashedApproximation(precision);
                double res = double.MaxValue;
                for (int i = 0; i < vertex.Length - 1; ++i)
                {
                    double pos1, pos2;
                    double d = Geometry.DistLL(vertex[i], vertex[i + 1] - vertex[i], fromHere, direction, out pos1, out pos2);
                    if (pos1 >= 0.0 && pos1 <= 1.0 && pos2 < res) res = pos2;
                }
                return res;
            }
        }
        #endregion
        #region ISerializable Members
        /// <summary>
        /// Constructor required by deserialization
        /// </summary>
        /// <param name="info">SerializationInfo</param>
        /// <param name="context">StreamingContext</param>
        protected BSpline(SerializationInfo info, StreamingContext context)
            : base(context)
        {
            SerializationInfoEnumerator e = info.GetEnumerator();
            while (e.MoveNext())
            {
                switch (e.Name)
                {
                    default:
                        base.SetSerializationValue(e.Name, e.Value);
                        break;
                    case "Poles":
                        poles = (GeoPoint[])e.Value;
                        break;
                    case "Weights":
                        weights = e.Value as double[];
                        break;
                    case "Knots":
                        knots = e.Value as double[];
                        break;
                    case "Multiplicities":
                        multiplicities = e.Value as int[];
                        break;
                    case "Degree":
                        degree = info.GetInt32(e.Name);
                        break;
                    case "Periodic":
                        periodic = InfoReader.ReadBool(e.Value);
                        break;
                    case "StartParam":
                        startParam = info.GetDouble(e.Name);
                        break;
                    case "EndParam":
                        endParam = info.GetDouble(e.Name);
                        break;
                    case "ThroughPoints3d":
                        throughPoints3d = e.Value as GeoPoint[];
                        break;
                    case "Direction3D":
                        direction3D = e.Value as GeoVector[];
                        break;
                    case "ThroughPointsParam":
                        throughPointsParam = e.Value as double[];
                        break;
                    case "Plane":
                        plane = (Plane)e.Value;
                        break;
                    case "ColorDef":
                        colorDef = e.Value as ColorDef;
                        break;
                    case "LineWidth":
                        lineWidth = e.Value as LineWidth;
                        break;
                    case "LinePattern":
                        linePattern = e.Value as LinePattern;
                        break;
                }
            }
            extent = BoundingCube.EmptyBoundingCube;            
            if (Constructed != null) Constructed(this);
        }
        /// <summary>
        /// Implements <see cref="ISerializable.GetObjectData"/>
        /// </summary>
        /// <param name="info">The <see cref="System.Runtime.Serialization.SerializationInfo"/> to populate with data.</param>
        /// <param name="context">The destination (<see cref="System.Runtime.Serialization.StreamingContext"/>) for this serialization.</param>
        public override void GetObjectData(SerializationInfo info, StreamingContext context)
        {
            base.GetObjectData(info, context);
            // zuerst die Hauptwerte
            info.AddValue("Poles", poles);
            info.AddValue("Weights", weights);
            info.AddValue("Knots", knots);
            info.AddValue("Multiplicities", multiplicities);
            info.AddValue("Degree", degree);
            info.AddValue("Periodic", periodic);
            info.AddValue("StartParam", startParam);
            info.AddValue("EndParam", endParam);
            if (throughPoints3d != null)
            {
                info.AddValue("ThroughPoints3d", throughPoints3d);
                info.AddValue("Direction3D", direction3D);
                info.AddValue("ThroughPointsParam", throughPointsParam);
            }
            //			if (throughPoints2d!=null)
            //			{
            //				info.AddValue("Plane",plane);
            //				info.AddValue("ThroughPoints2d",throughPoints2d);
            //				info.AddValue("Direction2D",direction2D);
            //			}
            info.AddValue("ColorDef", colorDef);
            info.AddValue("LineWidth", lineWidth);
            info.AddValue("LinePattern", linePattern);
        }
        #endregion
        #region ICurve Members
        /// <summary>
        /// Returns the point at the provided parameter. Unlike the <see cref="ICurve"/> method <see cref="ICurve.PointAt"/>
        /// this Method takes a parameter in the natural space of the BSpline.
        /// </summary>
        /// <param name="param">Parameter to get the point for</param>
        /// <returns>The resulting point</returns>
        public GeoPoint PointAtParam(double param)
        {
            if (param == knots[0]) return poles[0];
            if (param == knots[knots.Length - 1]) return poles[poles.Length - 1];
            lock (this)
            {
                if (!nurbsHelper) MakeNurbsHelper();
                if (plane.HasValue && (nubs2d != null | nurbs2d != null))
                {
                    if (nubs2d != null)
                    {
                        return plane.Value.ToGlobal(nubs2d.CurvePoint(param));
                    }
                    else
                    {
                        return plane.Value.ToGlobal((GeoPoint2D)nurbs2d.CurvePoint(param));
                    }
                }
                else
                {
                    if (nubs3d != null)
                    {
                        return nubs3d.CurvePoint(param);
                    }
                    else
                    {
                        return nurbs3d.CurvePoint(param);
                    }
                }
            }
        }
        private void PointDirAtParam(double param, out GeoPoint point, out GeoVector dir)
        {
            lock (bSplineLock)
            {
                if (!nurbsHelper) MakeNurbsHelper();
                if (plane.HasValue && (nubs2d != null | nurbs2d != null))
                {
                    if (nubs2d != null)
                    {
                        GeoPoint2D point2d, dir2d;
                        nubs2d.CurveDeriv1(param, out point2d, out dir2d);
                        point = plane.Value.ToGlobal(point2d);
                        dir = plane.Value.ToGlobal(dir2d.ToVector());
                        return;
                    }
                    else
                    {
                        GeoPoint2DH point2d, dir2d;
                        nurbs2d.CurveDeriv1(param, out point2d, out dir2d);
                        point = plane.Value.ToGlobal((GeoPoint2D)point2d);
                        dir = plane.Value.ToGlobal((GeoVector2D)dir2d);
                        return;
                    }
                }
                else
                {
                    if (nubs3d != null)
                    {
                        GeoPoint pdir;
                        nubs3d.CurveDeriv1(param, out point, out pdir);
                        dir = pdir.ToVector();
                        return;
                    }
                    else
                    {
                        GeoPointH pointh, dirh;
                        nurbs3d.CurveDeriv1(param, out pointh, out dirh);
                        point = (GeoPoint)pointh;
                        dir = (GeoVector)dirh;
                        return;
                    }
                }
            }
        }
        GeoPoint ICurve.StartPoint
        {
            get
            {
                return PointAtParam(startParam);
            }
            set
            {
                using (new Changing(this, typeof(ICurve), "StartPoint", poles[0]))
                {
                    poles[0] = value; // davon ausgehend, dass das auch immer der Startpunkt ist, sollte aber auch immer so sein
                }
            }
        }
        GeoPoint ICurve.EndPoint
        {
            get
            {
                return PointAtParam(endParam);
            }
            set
            {
                using (new Changing(this, typeof(ICurve), "EndPoint", poles[poles.Length - 1]))
                {
                    poles[poles.Length - 1] = value;
                }
            }
        }
        GeoVector ICurve.StartDirection
        {
            get
            {
                GeoPoint p;
                GeoVector v;
                PointDirAtParam(startParam, out p, out v);
                return v;
            }
        }
        GeoVector ICurve.EndDirection
        {
            get
            {
                GeoPoint p;
                GeoVector v;
                PointDirAtParam(endParam, out p, out v);
                return v;
            }
        }
        GeoVector ICurve.DirectionAt(double Position)
        {
            GeoPoint p;
            GeoVector v;
            PointDirAtParam(startParam + Position * (endParam - startParam), out p, out v);
            return (endParam - startParam) * v; // Länge des Vektors auf eine Änderung von 1 im normierten Parameterraum (0..1)
        }
        GeoPoint ICurve.PointAt(double Position)
        {
            return PointAtParam(startParam + Position * (endParam - startParam));
        }
        double ICurve.PositionOf(GeoPoint p, Plane pl)
        {
            BSpline2D bsp2d = (this as ICurve).GetProjectedCurve(pl) as BSpline2D;
            return bsp2d.PositionOf(pl.Project(p));
        }
        public double PositionOfThroughPoint(int ind)
        {
            if (throughPointsParam != null) return (throughPointsParam[ind] - startParam) / (endParam - startParam);
            return 0.0;
        }
        double[] MultiParameterOf(GeoPoint p)
        {
            List<double> res = new List<double>();

            if (this.IsSingular) return res.ToArray();
            if (interpol == null) MakeInterpol();
            // zuerst die Sehnen der Interpolation verwenden
            for (int i = 0; i < interpol.Length - 1; ++i)
            {
                GeoPoint dr = Geometry.DropPL(p, interpol[i], interpol[i + 1]);
                double par = Geometry.LinePar(interpol[i], interpol[i + 1] - interpol[i], dr);
                if (par > -0.2 && par < 1.2)
                {
                    int notConverging = 0;
                    double pos = interparam[i] + par * (interparam[i + 1] - interparam[i]);
                    if (pos < startParam || pos > endParam) continue;
                    // pos ist jetzt ein guter Anfangswert für die Suche
                    GeoPoint pp;
                    GeoVector pdir;
                    PointDirAtParam(pos, out pp, out pdir);
                    double dist = Geometry.Dist(p, pp);
                    if (dist == 0.0)
                    {   // hit the first time exactely
                        res.Add(pos);
                    }
                    while (dist > 0.0)
                    {
                        par = Geometry.LinePar(pp, pdir, p);
                        if (Math.Abs(par) < (endParam - startParam) * 1e-10)
                        {
                            res.Add(pos);
                            break;
                        }
                        pos += par;
                        if (pos < startParam || pos > endParam) break; // rausgelaufen
                        GeoPoint ppp;
                        GeoVector ppdir;
                        PointDirAtParam(pos, out ppp, out ppdir);
                        double dd = Geometry.Dist(p, ppp);
                        if (dd >= dist)
                        {   // hier abbrechen, da nicht mehr konvergent
                            if (Math.Abs(par) < (endParam - startParam) * 1e-10)
                            {
                                res.Add(pos);
                                break;
                            }
                            else
                            {
                                if (notConverging > 3) break;
                                else
                                {
                                    ++notConverging;
                                    pdir = ppdir;
                                    pp = ppp;
                                    dist = dd;
                                }
                            }
                        }
                        else
                        {
                            pdir = ppdir;
                            pp = ppp;
                            dist = dd;
                        }
                        if (dist == 0.0)
                        {   // sonst bricht das oben ab und bestdist wird nicht gesetzt
                            res.Add(pos);
                            break;
                        }
                    }
                }
            }
            if (Geometry.Dist((this as ICurve).StartPoint, p) < Precision.eps)
            {
                res.Add(startParam);
            }
            if (Geometry.Dist((this as ICurve).EndPoint, p) < Precision.eps)
            {
                res.Add(endParam);
            }

            return res.ToArray();
        }
        double ICurve.PositionOf(GeoPoint p)
        {
            double tpos = TetraederHull.PositionOf(p);
            if (tpos != double.MaxValue) return tpos;
            if (this.IsSingular)
            {
                return Geometry.LinePar(poles[0], poles[poles.Length - 1], p);
                // return 0.0;
            }
            if (interpol == null) MakeInterpol();
            // zuerst die Sehnen der Interpolation verwenden
            double bestdist = double.MaxValue;
            double bestpar = 0.0;
            for (int i = 0; i < interpol.Length - 1; ++i)
            {
                GeoPoint dr = Geometry.DropPL(p, interpol[i], interpol[i + 1]);
                double par = Geometry.LinePar(interpol[i], interpol[i + 1] - interpol[i], dr);
                if (par > -0.2 && par < 1.2)
                {
                    double pos = interparam[i] + par * (interparam[i + 1] - interparam[i]);
                    if (pos < startParam || pos > endParam) continue;
                    // pos ist jetzt ein guter Anfangswert für die Suche

                    // even with a cached ExplicitPCurve3D the simple Newton here is faster than the polynomial root calculation by a factor of 4!!!
                    // pos = (this as IExplicitPCurve3D).GetExplicitPCurve3D().PositionOf(p, pos, out double pdist);
                    //if (pos != double.MaxValue)
                    //{
                    //    if (pdist < bestdist)
                    //    {
                    //        bestdist = pdist;
                    //        bestpar = pos;
                    //    }
                    //}
                    //else
                    {
                        GeoPoint pp;
                        GeoVector pdir;
                        PointDirAtParam(pos, out pp, out pdir);
                        double dist = Geometry.Dist(p, pp);
                        if (dist < bestdist)
                        {
                            bestdist = dist;
                            bestpar = pos;
                        }
                        while (dist > 0.0)
                        {
                            par = Geometry.LinePar(pp, pdir, p);
                            if (Math.Abs(par) < (endParam - startParam) * 1e-10)
                            {
                                if (dist < bestdist)
                                {
                                    bestdist = dist;
                                    bestpar = pos;
                                }
                                break;
                            }
                            pos += par;
                            if (pos < startParam || pos > endParam) break; // rausgelaufen
                            GeoPoint ppp;
                            GeoVector ppdir;
                            PointDirAtParam(pos, out ppp, out ppdir);
                            double dd = Geometry.Dist(p, ppp);
                            if (dd >= dist)
                            {   // hier abbrechen, da nicht mehr konvergent
                                if (bestdist == double.MaxValue || dist > bestdist * 1.01)
                                {   // not converging, try polynomial, this is more save
                                    double pos1 = (this as IExplicitPCurve3D).GetExplicitPCurve3D().PositionOf(p, pos - par, out double dist1);
                                    if (pos1 != double.MaxValue)
                                    {
                                        pos = pos1;
                                        dist = dist1;
                                    }
                                }
                                if (dist < bestdist)
                                {
                                    bestdist = dist;
                                    bestpar = pos;
                                }
                                break;
                            }
                            else
                            {
                                pdir = ppdir;
                                pp = ppp;
                                dist = dd;
                            }
                            if (dist == 0.0)
                            {   // sonst bricht das oben ab und bestdist wird nicht gesetzt
                                bestdist = dist;
                                bestpar = pos;
                                break;
                            }
                        }
                    }
                }
            }
            if (Geometry.Dist((this as ICurve).StartPoint, p) < bestdist)
            {
                bestdist = Geometry.Dist((this as ICurve).StartPoint, p);
                bestpar = startParam;
            }
            if (Geometry.Dist((this as ICurve).EndPoint, p) < bestdist)
            {
                bestdist = Geometry.Dist((this as ICurve).EndPoint, p);
                bestpar = endParam;
            }
            if (bestdist < double.MaxValue)
            {
                return (bestpar - startParam) / (endParam - startParam);
            }
            else
            {	// geht nur, wenn der Punkt echt innerhalb. Sonst ist die
                // PolyLine aus den poles ein recht gutes Maß
                Polyline pol = Polyline.Construct();
                pol.SetPoints(poles, periodic);
                return pol.PositionOf(p);
            }
        }
        double ICurve.PositionOf(GeoPoint p, double prefer)
        {
            double[] pars = MultiParameterOf(p);
            double closest = double.MaxValue;
            double res = double.MaxValue;
            for (int i = 0; i < pars.Length; i++)
            {
                double pos = (pars[i] - startParam) / (endParam - startParam); // parameter to position
                double d = Math.Abs(pos - prefer);
                if (d < closest)
                {
                    closest = d;
                    res = pos;
                }
            }
            return res;
        }
        internal BSpline TrimOverlapping(GeoPoint here)
        {
            double sp = startParam;
            double ep = endParam;
            double[] pars = MultiParameterOf(here);
            double closestSp = double.MaxValue;
            double closestEp = double.MaxValue;
            for (int i = 0; i < pars.Length; i++)
            {
                double d = Math.Abs(pars[i] - startParam);
                if (d < closestSp)
                {
                    closestSp = d;
                    sp = pars[i];
                }
                d = Math.Abs(pars[i] - endParam);
                if (d < closestEp)
                {
                    closestEp = d;
                    ep = pars[i];
                }
            }
            if (sp == ep) return this.Clone() as BSpline;
            return TrimParam(sp, ep);
        }
        double ICurve.Length
        {
            get
            {
                if ((this as ICurve).GetPlanarState() == PlanarState.Planar)
                {
                    Plane pl = (this as ICurve).GetPlane();
                    ICurve2D c2d = (this as ICurve).GetProjectedCurve(pl);
                    if (c2d == null)
                    {
                        c2d = (this as ICurve).GetProjectedCurve(pl);
                        return 0.0;
                    }
                    return c2d.Length;
                }
                ICurve aprox = (this as ICurve).Approximate(true, Math.Max(GetBoundingCube().Size / 1000, Precision.eps));
                return aprox.Length;
            }
        }
        private void MakeStartEndKnotsClean()
        {
            double dkn = knots[knots.Length - 1] - knots[0];
            while (knots[1] - knots[0] < dkn * 1e-5)
            {
                List<double> knl = new List<double>(knots);
                knl.RemoveAt(1);
                knots = knl.ToArray();
                multiplicities[0] += 1;
            }
            while (knots[knots.Length - 1] - knots[knots.Length - 2] < dkn * 1e-5)
            {
                List<double> knl = new List<double>(knots);
                knl.RemoveAt(knots.Length - 2);
                knots = knl.ToArray();
                multiplicities[multiplicities.Length - 1] += 1;
            }
        }
        void ICurve.Reverse()
        {
            using (new Changing(this, typeof(ICurve), "Reverse", new object[0]))
            {
                Array.Reverse(poles);
                Array.Reverse(weights);
                double lastknot = knots[knots.Length - 1];
                for (int i = 0; i < knots.Length; ++i)
                {
                    knots[i] = lastknot - knots[i];
                }
                double tmp = this.startParam;
                this.startParam = lastknot - this.endParam;
                this.endParam = lastknot - tmp;
                Array.Reverse(knots);
                Array.Reverse(multiplicities);
                if (throughPoints3d != null) Array.Reverse(throughPoints3d);
            }
        }

        internal void Clamp()
        {
            while (multiplicities[0] <= degree) InsertKnot(knots[0]);
        }
        private void InsertKnot(double u)
        {
            // u = knots[0] + (knots[1] - knots[0]) / 2.0; // erstmal...
            if (u <= knots[0] || u >= knots[knots.Length - 1])
            {
                return;
            }
            else
            {
                if (!nurbsHelper) MakeNurbsHelper();
                double[] newknots;
                double[] newweights;
                GeoPoint[] newpoles;
                int indtosplit;
                if (nubs2d != null)
                {
                    GeoPoint2D[] newpoles2d;
                    indtosplit = nubs2d.CurveKnotIns(u, 1, out newknots, out newpoles2d);
                    newpoles = new GeoPoint[newpoles2d.Length];
                    newweights = new double[newpoles.Length];
                    for (int i = 0; i < newpoles.Length; ++i)
                    {
                        newweights[i] = 1.0;
                        newpoles[i] = plane.Value.ToGlobal(newpoles2d[i]);
                    }
                }
                else if (nurbs2d != null)
                {
                    GeoPoint2DH[] newpolestmp;
                    indtosplit = nurbs2d.CurveKnotIns(u, 1, out newknots, out newpolestmp);
                    newpoles = new GeoPoint[newpolestmp.Length];
                    newweights = new double[newpolestmp.Length];
                    for (int i = 0; i < newpoles.Length; ++i)
                    {
                        newpoles[i] = plane.Value.ToGlobal((GeoPoint2D)newpolestmp[i]);
                        newweights[i] = newpolestmp[i].w;
                    }
                }
                else if (nubs3d != null)
                {
                    indtosplit = nubs3d.CurveKnotIns(u, 1, out newknots, out newpoles);
                    newweights = new double[newpoles.Length];
                    for (int i = 0; i < newpoles.Length; ++i)
                    {
                        newweights[i] = 1.0;
                    }
                }
                else //if (nubrs3d != null) immer
                {
                    GeoPointH[] newpolestmp;
                    indtosplit = nurbs3d.CurveKnotIns(u, 1, out newknots, out newpolestmp);
                    newpoles = new GeoPoint[newpolestmp.Length];
                    newweights = new double[newpolestmp.Length];
                    for (int i = 0; i < newpoles.Length; ++i)
                    {
                        newpoles[i] = (GeoPoint)newpolestmp[i];
                        newweights[i] = newpolestmp[i].w;
                    }
                }
                BSpline res1 = BSpline.Construct();
                List<double> knots1 = new List<double>();
                List<int> mults1 = new List<int>();
                knots1.Add(newknots[0]);
                mults1.Add(1);
                for (int i = 1; i < newknots.Length; ++i)
                {
                    if (knots1[knots1.Count - 1] == newknots[i]) ++mults1[mults1.Count - 1];
                    else
                    {
                        knots1.Add(newknots[i]);
                        mults1.Add(1);
                    }
                }

                interpol = null;
                interdir = null;
                interparam = null;

                this.SetData(degree, newpoles, newweights, knots1.ToArray(), mults1.ToArray(), IsClosed);
            }
        }
        private int FindIndex(double u)
        {
            if (nubs2d != null)
            {
                return nubs2d.FindIndex(u);
            }
            else if (nurbs2d != null)
            {
                return nurbs2d.FindIndex(u);
            }
            else if (nubs3d != null)
            {
                return nubs3d.FindIndex(u);
            }
            else //if (nubrs3d != null) immer
            {
                return nurbs3d.FindIndex(u);
            }
        }
        internal GeoPoint InsertPole(int index, bool insertAfter)
        {   // liefert den neuen Pol, aber darum herumliegende Pole werden auch beeinträchtigt
            // 1. das Kontenintervall feststellen, in dem ein Knoten eingefügt werden soll
            // poles == knots-degree-1, also echte knots mit multiplicities aufgelöst
            if (!nurbsHelper) MakeNurbsHelper();
            if (!insertAfter) --index; // immer insertAfter
            for (int i = 0; i < knots.Length - 1; i++)
            {
                int ind = FindIndex((knots[i] + knots[i + 1]) / 2.0) - degree + 2;
                if (ind >= index)
                {
                    using (new Changing(this))
                    {
                        InsertKnot((knots[i] + knots[i + 1]) / 2.0);
                        return poles[index + 1];
                    }
                }
            }
            // sollte nicht hier hin kommen
            using (new Changing(this))
            {
                InsertKnot((knots[knots.Length - 1] + knots[knots.Length - 2]) / 2.0);
                return poles[index + 1];
            }
        }
        private BSpline[] SplitParam(double u)
        {   // noch nicht getestet, sieht aber gut aus
            // siehe aber auch Split, dort wirds von der allgemeinen NurbsKlasse gemacht.
            if (u <= knots[0] || u >= knots[knots.Length - 1])
            {
                return new BSpline[] { Clone() as BSpline };
            }
            else
            {
                if (!nurbsHelper) MakeNurbsHelper();
                double[] newknots;
                double[] newweights;
                GeoPoint[] newpoles;
                int indtosplit;
                if (nubs2d != null)
                {
                    GeoPoint2D[] newpoles2d;
                    indtosplit = nubs2d.CurveKnotIns(u, degree, out newknots, out newpoles2d);
                    newpoles = new GeoPoint[newpoles2d.Length];
                    newweights = new double[newpoles.Length];
                    for (int i = 0; i < newpoles.Length; ++i)
                    {
                        newweights[i] = 1.0;
                        newpoles[i] = plane.Value.ToGlobal(newpoles2d[i]);
                    }
                }
                else if (nurbs2d != null)
                {
                    GeoPoint2DH[] newpolestmp;
                    indtosplit = nurbs2d.CurveKnotIns(u, degree, out newknots, out newpolestmp);
                    newpoles = new GeoPoint[newpolestmp.Length];
                    newweights = new double[newpolestmp.Length];
                    for (int i = 0; i < newpoles.Length; ++i)
                    {
                        newpoles[i] = plane.Value.ToGlobal((GeoPoint2D)newpolestmp[i]);
                        newweights[i] = newpolestmp[i].w;
                    }
                }
                else if (nubs3d != null)
                {
                    indtosplit = nubs3d.CurveKnotIns(u, degree, out newknots, out newpoles);
                    newweights = new double[newpoles.Length];
                    for (int i = 0; i < newpoles.Length; ++i)
                    {
                        newweights[i] = 1.0;
                    }
                }
                else //if (nubrs3d != null) immer
                {
                    GeoPointH[] newpolestmp;
                    indtosplit = nurbs3d.CurveKnotIns(u, degree, out newknots, out newpolestmp);
                    newpoles = new GeoPoint[newpolestmp.Length];
                    newweights = new double[newpolestmp.Length];
                    for (int i = 0; i < newpoles.Length; ++i)
                    {
                        newpoles[i] = (GeoPoint)newpolestmp[i];
                        newweights[i] = newpolestmp[i].w;
                    }
                }
                List<double> knots1 = new List<double>();
                List<int> mults1 = new List<int>();
                List<double> knots2 = new List<double>();
                List<int> mults2 = new List<int>();
                List<GeoPoint> poles1 = new List<GeoPoint>();
                List<GeoPoint> poles2 = new List<GeoPoint>();
                List<double> weights1 = new List<double>();
                List<double> weights2 = new List<double>();
                knots1.Add(newknots[0]);
                mults1.Add(1);
                knots2.Add(u);
                mults2.Add(1);
                int kn1 = 0;
                for (int i = 1; i < newknots.Length; ++i)
                {
                    if (newknots[i] <= u)
                    {
                        if (knots1[knots1.Count - 1] == newknots[i]) ++mults1[mults1.Count - 1];
                        else
                        {
                            knots1.Add(newknots[i]);
                            mults1.Add(1);
                        }
                        ++kn1;
                    }
                    if (newknots[i] >= u)
                    {
                        if (knots2[knots2.Count - 1] == newknots[i]) ++mults2[mults2.Count - 1];
                        else
                        {
                            knots2.Add(newknots[i]);
                            mults2.Add(1);
                        }
                    }
                }
                ++mults1[mults1.Count - 1]; // der fehlt halt noch;
                indtosplit = kn1 - degree; // kleine Nachbesserung, manchmal Probleme, wenn genau auf einem Knoten geschnitten wird
                for (int i = 0; i < newpoles.Length; ++i)
                {
                    if (i <= indtosplit)
                    {
                        poles1.Add(newpoles[i]);
                        weights1.Add(newweights[i]);
                    }
                    if (i >= indtosplit)
                    {
                        poles2.Add(newpoles[i]);
                        weights2.Add(newweights[i]);
                    }
                }
                BSpline res1 = BSpline.Construct();
                BSpline res2 = BSpline.Construct();
                res1.SetData(degree, poles1.ToArray(), weights1.ToArray(), knots1.ToArray(), mults1.ToArray(), false);
                res2.SetData(degree, poles2.ToArray(), weights2.ToArray(), knots2.ToArray(), mults2.ToArray(), false);
                return new BSpline[] { res1, res2 };
            }
        }
        ICurve[] ICurve.Split(double Position)
        {
            if (!nurbsHelper) MakeNurbsHelper();
            BSpline res1 = this.Clone() as BSpline;
            BSpline res2 = this.Clone() as BSpline;
            bool ok = false;
            double p = startParam + Position * (endParam - startParam);
            if (nubs3d != null)
            {
                Nurbs<GeoPoint, GeoPointPole>[] res = nubs3d.Split(p);
                if (res.Length == 2)
                {
                    res1.nubs3d = res[0];
                    res2.nubs3d = res[1];
                    ok = true;
                }
            }
            else if (nurbs3d != null)
            {
                Nurbs<GeoPointH, GeoPointHPole>[] res = nurbs3d.Split(p);
                if (res.Length == 2)
                {
                    res1.nurbs3d = res[0];
                    res2.nurbs3d = res[1];
                    ok = true;
                }
            }
            else if (nubs2d != null)
            {
                Nurbs<GeoPoint2D, GeoPoint2DPole>[] res = nubs2d.Split(p);
                if (res.Length == 2)
                {
                    res1.nubs2d = res[0];
                    res2.nubs2d = res[1];
                    ok = true;
                }
            }
            else if (nurbs2d != null)
            {
                Nurbs<GeoPoint2DH, GeoPoint2DHPole> tmp = nurbs2d.Trim(startParam, endParam);
                Nurbs<GeoPoint2DH, GeoPoint2DHPole>[] res = tmp.Split(p);
                if (res.Length == 2)
                {
                    res1.nurbs2d = res[0];
                    res2.nurbs2d = res[1];
                    ok = true;
                }
            }
            if (ok)
            {
                res1.FromNurbs(getPlane());
                res2.FromNurbs(getPlane());
                return new ICurve[] { res1, res2 };
            }
            else
            {
                return new ICurve[] { this.Clone() as BSpline };
            }

            //if (bSplineEdge == null) RecalcOcasBuddies();
            //CndOCas.BSplineEdge [] bes = bSplineEdge.Split(p);
            //// BSplines mit weniger als 2 Punkten machen Probleme (bei OpenCascade)
            //// die werden deshalb ausgefiltert. Hoffentlich können die Aufrufer damit umgehen
            //// dass es weniger als 2 Teilergebnisse sind
            //ArrayList a = new ArrayList();
            //for (int i=0; i<bes.Length; ++i)
            //{
            //    if (bes[i].NumPoles >= 2)
            //    {
            //        BSpline bsp = new BSpline();
            //        bsp.RecalcFromEdge(bes[i]);
            //        a.Add(bsp);
            //    }
            //}
            //return (ICurve[])a.ToArray(typeof(ICurve));
        }
        ICurve[] ICurve.Split(double Position1, double Position2)
        {
            GeoPoint[] newPoles = new GeoPoint[poles.Length * 2 - 1];
            double[] newWeights = new double[weights.Length * 2 - 1];
            double[] newKnots = new double[knots.Length * 2 - 1];
            int[] newMultiplicities = new int[multiplicities.Length * 2 - 1];
            for (int i = 0; i < poles.Length; i++)
            {
                newPoles[i] = poles[i];
                newPoles[i + poles.Length - 1] = poles[i];
            }
            for (int i = 0; i < weights.Length; i++)
            {
                newWeights[i] = weights[i];
                newWeights[i + weights.Length - 1] = weights[i];
            }
            for (int i = 0; i < multiplicities.Length; i++)
            {
                newMultiplicities[i] = multiplicities[i];
                newMultiplicities[i + multiplicities.Length - 1] = multiplicities[i];
            }
            newMultiplicities[multiplicities.Length - 1] -= 1; // necessary, because tmp.SetData would throw an exception otherwise
            double dk = knots[knots.Length - 1] - knots[0];
            for (int i = 0; i < knots.Length; i++)
            {
                newKnots[i] = knots[i];
                newKnots[i + knots.Length - 1] = knots[i] + dk;
            }
            BSpline tmp = BSpline.Construct(); // nochmal der gleiche hintendrangehängt
            tmp.SetData(degree, newPoles, newWeights, newKnots, newMultiplicities, true);
            ICurve part1 = tmp.Clone() as ICurve;
            part1.Trim(Position1 / 2.0, Position2 / 2.0);
            (tmp as ICurve).Trim(Position2 / 2.0, Position1 / 2.0 + 0.5);
            return new ICurve[] { part1, tmp as ICurve };

        }
        void ICurve.Trim(double StartPos, double EndPos)
        {
            Plane pln = getPlane(); // we need this for FromNurbs
            if (!nurbsHelper) MakeNurbsHelper();
            if (StartPos < 0.0) StartPos = 0.0;
            if (EndPos > 1.0) EndPos = 1.0;
            if (StartPos == 0.0 && EndPos == 1.0) return;
            if (EndPos > StartPos)
            {
                double p1 = startParam + StartPos * (endParam - startParam);
                double p2 = startParam + EndPos * (endParam - startParam);
                double du = (knots[knots.Length - 1] - knots[0]) * 1e-7;
                for (int i = 0; i < knots.Length; i++)
                {
                    if (Math.Abs(knots[i] - p1) < du) p1 = knots[i];
                    if (Math.Abs(knots[i] - p2) < du) p2 = knots[i];
                }
                if (nubs3d != null) nubs3d = nubs3d.Trim(p1, p2);
                else if (nurbs3d != null) nurbs3d = nurbs3d.Trim(p1, p2);
                else if (nubs2d != null) nubs2d = nubs2d.Trim(p1, p2);
                else if (nurbs2d != null) nurbs2d = nurbs2d.Trim(p1, p2);
                using (new Changing(this, true))
                {
                    FromNurbs(pln);
                }
            }
            else
            {
                ICurve[] parts = (this as ICurve).Split(EndPos, StartPos); // liefert als 2. den Übergang über "0"
                using (new Changing(this, true))
                {
                    FromNurbs(parts[1] as BSpline);
                }
            }
        }
        private Plane getPlane()
        {
            return (this as ICurve).GetPlane();
        }
        public BSpline TrimParam(double spar, double epar)
        {
            BSpline clone = BSpline.Construct();
            bool reverse = false;
            if (spar > epar)
            {
                double tmp = spar;
                spar = epar;
                epar = tmp;
                reverse = true;
            }
            MakeNurbsHelper();
            if (nubs3d != null) clone.nubs3d = nubs3d.Trim(spar, epar);
            else if (nurbs3d != null) clone.nurbs3d = nurbs3d.Trim(spar, epar);
            else if (nubs2d != null) clone.nubs2d = nubs2d.Trim(spar, epar);
            else if (nurbs2d != null) clone.nurbs2d = nurbs2d.Trim(spar, epar);
            clone.FromNurbs(getPlane());
            if (reverse) (clone as ICurve).Reverse();
            return clone;
        }
        ICurve ICurve.Clone() { return (ICurve)this.Clone(); }
        ICurve ICurve.CloneModified(ModOp m)
        {
            IGeoObject clone = Clone();
            clone.Modify(m);
            return (ICurve)clone;
        }
        bool ICurve.IsClosed
        {
            get
            {
                return IsClosed;
            }
        }
        PlanarState ICurve.GetPlanarState()
        {
            lock (planeLock)
            {
                if (this.IsSingular) return PlanarState.UnderDetermined;
                double MaxDist;
                bool isLinear;
                Plane pln = Plane.FromPoints(poles, out MaxDist, out isLinear);
                if (isLinear) return PlanarState.UnderDetermined;
                if (MaxDist < Precision.eps)
                {
                    plane = pln;
                    return PlanarState.Planar;
                }
                else
                {
                    plane = null;
                    if (poles.Length == 2) return PlanarState.UnderDetermined;
                    return PlanarState.NonPlanar;
                    // es gäbe noch den Spezialfall, dass alle poles auf einer Linie liegen
                    // es aber mehr als zwei poles gibt. Dann wäre man hier auch UnderDetermined
                    // es wird aber NonPlanar geliefert.
                    // Wichtig ist es hier nicht auf nurbs3d oder so zurückzugreifen, da MakeNurbsHelper
                    // auf Planarstate sich bezieht
                }
            }
        }
                
        Plane ICurve.GetPlane()
        {
			if (plane.HasValue) 
                return plane.Value;

			lock (planeLock)
            {
				if (plane.HasValue)
					return plane.Value;

				double MaxDist;
                bool isLinear;
                Plane res = Plane.FromPoints(poles, out MaxDist, out isLinear);
                if (MaxDist < Precision.eps)
                {
                    plane = res;
                    return res;
                }
                else if (isLinear)
                {
                    GeoVector nrm = poles[poles.Length - 1] - poles[0];
                    if (nrm.IsNullVector())
                    {
                        for (int i = 0; i < poles.Length - 1; i++)
                        {
                            nrm = poles[poles.Length - i - 2] - poles[0];
                            if (!nrm.IsNullVector()) break;
                        }
                    }
                    if (nrm.IsNullVector()) return Plane.XYPlane; // allpoles identical, singular object!
                    Plane tmp = new Plane(poles[0], nrm);
                    this.plane = new Plane(tmp.Location, tmp.DirectionX, tmp.Normal);
                    return plane.Value;

                    // don't use any methods that require the nurbs object to be already created, because this make an infinite recursion
                    // as in this old code:
                    //GeoVector v = (this as ICurve).DirectionAt(0.5);
                    //if (Precision.IsNullVector(v))
                    //{   // Länge ist 0.0
                    //    return new Plane(poles[0], GeoVector.ZAxis);
                    //}
                    //Angle xy = new Angle(v, GeoVector.ZAxis);
                    //Angle xz = new Angle(v, GeoVector.YAxis);
                    //Angle yz = new Angle(v, GeoVector.XAxis);
                    //xy = Math.Min(xy, Math.PI - xy);
                    //xz = Math.Min(xz, Math.PI - xz);
                    //yz = Math.Min(yz, Math.PI - yz);
                    //if (xy < xz)
                    //{
                    //    if (xy < yz) return new Plane(poles[0], GeoVector.XAxis, v);
                    //    else return new Plane(poles[0], GeoVector.YAxis, v);
                    //}
                    //else
                    //{
                    //    if (xz < yz) return new Plane(poles[0], GeoVector.XAxis, v);
                    //    else return new Plane(poles[0], GeoVector.YAxis, v);
                    //}
                }
                else
                {
                    // there is no plane, so this should not be called, we return some arbitrary plane
                    GeoVector nrm = poles[poles.Length - 1] - poles[0];
                    if (nrm.IsNullVector())
                    {
                        for (int i = 0; i < poles.Length - 1; i++)
                        {
                            nrm = poles[poles.Length - i - 2] - poles[0];
                            if (!nrm.IsNullVector()) break;
                        }
                    }
                    if (nrm.IsNullVector()) return Plane.XYPlane; // allpoles identical, singular object!
                    Plane tmp = new Plane(poles[0], nrm);
                    this.plane = new Plane(tmp.Location, tmp.DirectionX, tmp.Normal);
                    return plane.Value;
                }
            }
        }
        bool ICurve.IsInPlane(Plane p)
        {
            for (int i = 0; i < poles.Length; i++)
            {
                if (Math.Abs(p.Distance(poles[i])) > Precision.eps) return false;
            }
            return true;
            // alter Text, der macht Probleme wenn der Spline eine Linie ist
            // if ((this as ICurve).GetPlanarState() != PlanarState.Planar) return false;
            // return Precision.IsEqual(plane.Value, p);
        }
        CADability.Curve2D.ICurve2D ICurve.GetProjectedCurve(Plane p)
        {
            if (poles == null || poles.Length == 0) return null;
            GeoPoint2D[] poles2d = new GeoPoint2D[poles.Length];
            for (int i = 0; i < poles.Length; ++i) poles2d[i] = p.Project(poles[i]);
            // gehe hier zunächst mal davon aus, dass der 2d BSpline mit den selben Parametern
            // gemacht wird wie der 3d BSpline, lediglich die Punkte werden in die Ebene projiziert.
            // Stimmt das?
            // Ja, das scheint zu stimmen, steht jedenfalls so im NURBS Buch für affine Transformationen
            // zumindest, wenn keine identischen poles entstehen. Dann benimmt sich der 2d BSpline nämlich blöde, DirectionAt kann 0 werden
            bool identicalPoles = false;
            for (int i = 0; i < poles.Length - 1; i++)
            {
                if (Precision.IsEqual(poles2d[i], poles2d[i + 1]))
                {
                    identicalPoles = true;
                    break;
                }
            }
            if (!identicalPoles)
            {
                BSpline2D bsp2d = new BSpline2D(poles2d, weights, knots, multiplicities, degree, false, startParam, endParam);
                // closed auf false gesetzt, damit nicht initperiodic drankommt
                bsp2d.SetZValues(this, p); // sollte vielleicht in das IVisibleSegments Interface um dort die Werte zu setzen
                return bsp2d;
            }
            else
            {
                List<GeoPoint2D> throughPoints2d = new List<GeoPoint2D>();
                if (ThroughPoints3dExist)
                {
                    for (int i = 0; i < throughPoints3d.Length; i++)
                    {
                        GeoPoint2D p2d = p.Project(poles[i]);
                        if (throughPoints2d.Count > 0)
                        {
                            if (!Precision.IsEqual(throughPoints2d[throughPoints2d.Count - 1], p2d)) throughPoints2d.Add(p2d);
                        }
                        else
                        {
                            throughPoints2d.Add(p2d);
                        }
                    }
                }
                else
                {
                    double[] spos = (this as ICurve).GetSavePositions();
                    for (int i = 0; i < spos.Length; i++)
                    {
                        GeoPoint2D p2d = p.Project((this as ICurve).PointAt(spos[i]));
                        if (throughPoints2d.Count > 0)
                        {
                            if (!Precision.IsEqual(throughPoints2d[throughPoints2d.Count - 1], p2d)) throughPoints2d.Add(p2d);
                        }
                        else
                        {
                            throughPoints2d.Add(p2d);
                        }
                    }
                    if (throughPoints2d.Count < 2)
                    {
                        return new Line2D(p.Project((this as ICurve).StartPoint), p.Project((this as ICurve).EndPoint));
                        return null;
                        double prec = this.GetExtent(Precision.eps).Size * 1e-4;
                        ICurve approx = (this as ICurve).Approximate(false, prec);
                        ICurve2D res = approx.GetProjectedCurve(p);
                        if (res is Path2D)
                        {
                            Path2D p2d = res as Path2D;
                            p2d.Reduce(prec);   // vereinfacht den Pfad selbst, also res
                            if (p2d.SubCurvesCount == 1) return p2d.SubCurves[0];
                        }
                        return res;
                    }
                }
                try
                {
                    BSpline2D bsp2d = new BSpline2D(throughPoints2d.ToArray(), degree, this.periodic);
                    return bsp2d;
                }
                catch
                {
                    return null; // kommt vor, wenn nicht genügend poles gefunden werden
                }
            }
        }
        string ICurve.Description
        {
            get
            {
                // TODO:  Add BSpline.Description getter implementation
                return null;
            }
        }
        bool ICurve.IsComposed
        {
            get { return false; }
        }
        bool ICurve.IsSingular
        {
            get
            {
                if (poles == null) return true;
                return poles.Length == 0;
            }
        }
        ICurve[] ICurve.SubCurves
        {
            get { return new ICurve[0]; }
        }
        ICurve ICurve.Approximate(bool linesOnly, double maxError)
        {
#if DEBUG
            //for (int i = 0; i < poles.Length - 1; i++)
            //{
            //    if (Precision.IsEqual(poles[i], poles[i + 1]))
            //    {
            //    }
            //}
            //for (int i = 0; i < knots.Length - 1; i++)
            //{
            //    if (knots[i + 1] - knots[i] < Precision.eps)
            //    {
            //    }
            //}
            //double[] dbgpos = (this as ICurve).GetSavePositions();
            //GeoVector[] dirs = new GeoVector[dbgpos.Length];

            //for (int i = 0; i < dbgpos.Length; i++)
            //{
            //    dirs[i] = (this as ICurve).DirectionAt(dbgpos[i]);
            //}
            //Polyline dbgpl = Polyline.Construct();
            //dbgpl.SetPoints(poles, false);
            //TetraederHull dbgTetraederHull = this.TetraederHull;
            //GeoObjectList dd = dbgTetraederHull.Debug;
#endif
            PlanarState ps = (this as ICurve).GetPlanarState();
            if (PlanarState.Planar == ps || PlanarState.UnderDetermined == ps)
            {
                Plane pl = (this as ICurve).GetPlane();
                ICurve2D c2d = (this as ICurve).GetProjectedCurve(pl);
                if (c2d == null || c2d.Length < maxError)
                {   // zu kurze machen u.U. Probleme
                    Line line = Line.Construct();
                    line.SetTwoPoints((this as ICurve).StartPoint, (this as ICurve).EndPoint);
                    return line;
                }
                if (linesOnly)
                {
                    ICurve2D approx = c2d.Approximate(linesOnly, maxError);
                    return approx.MakeGeoObject(pl) as ICurve;
                }
                else
                {
                    try
                    {
                        ArcLineFitting2D alf = new ArcLineFitting2D(c2d, maxError, false, true, poles.Length + degree);
                        return alf.Approx.MakeGeoObject(pl) as ICurve;
                    }
                    catch (Exception e)
                    {
                        if (e is ThreadAbortException) throw (e);
                        ICurve2D approx = c2d.Approximate(false, maxError);
                        return approx.MakeGeoObject(pl) as ICurve;

                    }
                }
            }
            else
            {
                if (linesOnly)
                {
                    //double[] pos = new double[knots.Length];
                    //for (int i = 0; i < pos.Length; ++i)
                    //{
                    //    pos[i] = (knots[i] - startParam) / (endParam - startParam);
                    //}
                    //return Curves.ApproximateLinear(this, pos, maxError);
                    List<double> pos = new List<double>();
                    pos.Add(0.0); // Startpunkt
                    GeoPoint lastPoint = (this as ICurve).StartPoint;
                    for (int i = 1; i < knots.Length - 1; ++i)
                    {
                        double pi = (knots[i] - startParam) / (endParam - startParam);
                        GeoPoint current = (this as ICurve).PointAt(pi);
                        double pm = (pi + pos[pos.Count - 1]) / 2.0;
                        GeoPoint middlePoint = (this as ICurve).PointAt(pm);
                        if (Geometry.DistPL(middlePoint, current, lastPoint) > maxError)
                        {
                            pos.Add(pi);
                            lastPoint = current;
                        }
                    }
                    pos.Add(1.0);
                    return Curves.ApproximateLinear(this, pos.ToArray(), maxError);
                }
                else
                {
                    try
                    {
                        ArcLineFitting3D alf = new ArcLineFitting3D(this, maxError, true, PoleCount + degree);
                        return alf.Approx;
                    }
                    catch (Exception e)
                    {
                        if (e is ThreadAbortException) throw (e);
                        return TetraederHull.Approximate(false, maxError);
                        //double[] pos = new double[knots.Length];
                        //for (int i = 0; i < pos.Length; ++i)
                        //{
                        //    pos[i] = (knots[i] - startParam) / (endParam - startParam);
                        //}
                        //return Curves.ApproximateLinear(this, pos, maxError);
                    }
                }
            }
        }

        internal BSpline SetCyclicalStartPoint(double pos)
        {
            double par = startParam + pos * (endParam - startParam);
            BSpline[] parts = SplitParam(par);
            List<GeoPoint> tp = new List<GeoPoint>();
            if (parts.Length == 2)
            {
                List<double> sknots = new List<double>();
                List<GeoPoint> spoles = new List<GeoPoint>();
                List<double> sweights = new List<double>();
                List<int> smults = new List<int>();
                sknots.AddRange(parts[1].knots);
                spoles.AddRange(parts[1].poles);
                smults.AddRange(parts[1].multiplicities);
                smults[smults.Count - 1] -= 1;
                if (parts[1].weights != null) sweights.AddRange(parts[1].weights);
                double lkn = sknots[sknots.Count - 1];
                for (int i = 1; i < parts[0].knots.Length; i++)
                {
                    sknots.Add(lkn + parts[0].knots[i] - parts[0].knots[0]);
                    smults.Add(parts[0].multiplicities[i]);
                }
                for (int i = 1; i < parts[0].poles.Length; i++)
                {
                    spoles.Add(parts[0].poles[i]);
                    if (parts[0].weights != null) sweights.Add(parts[0].weights[i]);
                }
                BSpline res = BSpline.Construct();
                if (sweights.Count > 0) res.SetData(this.degree, spoles.ToArray(), sweights.ToArray(), sknots.ToArray(), smults.ToArray(), false);
                else res.SetData(this.degree, spoles.ToArray(), null, sknots.ToArray(), smults.ToArray(), false);
#if DEBUG
                GeoPoint pm = (res as ICurve).PointAt(0.001);
                ICurve dbg = (res as ICurve).Approximate(true, 0.1);
#endif
                return res;
            }
            return this.Clone() as BSpline;
        }

        double[] ICurve.TangentPosition(GeoVector direction)
        {
            // hier hilft nur interpolieren
            // oder die Tangenten in einer Projektion bestimmen, wobei die Ebene durch direction und eine
            // zweite gute Wahl gegeben ist. Die gefundenen Punkte gilt es in 3D dann zu überprüfen.
            if ((this as ICurve).GetPlanarState() == PlanarState.Planar)
            {
                Plane pl = (this as ICurve).GetPlane();
                if (Precision.IsDirectionInPlane(direction, pl))
                {
                    ICurve2D c2d = (this as ICurve).GetProjectedCurve(pl);
                    double[] res = c2d.TangentPointsToAngle(pl.Project(direction));
                    return res;
                }
                return null;
            }
            else
            {   // finde eine gute Ebene durch direction und die Kurve
                // folgendes ist sehr primitiv, bessere Lösung ist noch gesucht:
                GeoPoint p1 = (this as ICurve).PointAt(0.25);
                GeoPoint p2 = (this as ICurve).PointAt(0.75);
                try
                {
                    Plane pl = new Plane(p1, direction, p2 - p1);
                    ICurve2D c2d = (this as ICurve).GetProjectedCurve(pl);
                    double[] res = c2d.TangentPointsToAngle(pl.Project(direction));
                    List<double> resres = new List<double>();
                    for (int i = 0; i < res.Length; i++)
                    {
                        if (Precision.SameDirection((this as ICurve).DirectionAt(res[i]), direction, false))
                        {
                            resres.Add(res[i]);
                        }
                    }
                    return resres.ToArray();
                }
                catch (PlaneException)
                {
                    return null;
                }
            }
        }
        double[] ICurve.GetSelfIntersections()
        {
            if ((this as ICurve).GetPlanarState() == PlanarState.Planar)
            {
                return (this as ICurve).GetProjectedCurve((this as ICurve).GetPlane()).GetSelfIntersections();
            }
            else
            {
                double maxDist;
                bool isLinear;
                Plane pln = Plane.FromPoints(poles, out maxDist, out isLinear);
                if (!isLinear)
                {
                    return (this as ICurve).GetProjectedCurve(pln).GetSelfIntersections();
                }
            }
            return new double[0];
        }
        bool ICurve.SameGeometry(ICurve other, double precision)
        {
            if (other is BSpline)
            {
                BSpline c2 = other as BSpline;
                if (c2.poles.Length == poles.Length && c2.degree == degree)
                {
                    double df = (c2.poles[0] | poles[0]) + (c2.poles[c2.poles.Length - 1] | poles[poles.Length - 1]);
                    double dr = (c2.poles[0] | poles[poles.Length - 1]) + (c2.poles[c2.poles.Length - 1] | poles[0]);
                    bool reverse = df > dr;
                    if (df<precision && dr<precision)
                    {
                        reverse = (this as ICurve).StartDirection * (c2 as ICurve).StartDirection < 0;
                    }
                    if (!reverse)
                    {
                        for (int i = 0; i < poles.Length; i++)
                        {
                            if ((c2.poles[i] | poles[i]) > precision) return false;
                        }
                    }
                    else
                    {
                        for (int i = 0; i < poles.Length; i++)
                        {
                            if ((c2.poles[i] | poles[poles.Length - i - 1]) > precision) return false;
                        }
                    }
                    return true; // genaugenommen müsste man noch die Knoten überprüfen
                }
                for (int i = 0; i < KnotPoints.Length; i++)
                {
                    if ((c2 as ICurve).DistanceTo(KnotPoints[i]) > precision) return false;
                }
                for (int i = 0; i < c2.KnotPoints.Length; i++)
                {
                    if ((this as ICurve).DistanceTo(c2.KnotPoints[i]) > precision) return false;
                }
                if (KnotPoints.Length<=2 && c2.KnotPoints.Length<=2)
                {   // maybe same start and endpoint
                    if ((c2 as ICurve).DistanceTo((this as ICurve).PointAt(0.5)) > precision) return false;
                    if ((this as ICurve).DistanceTo((c2 as ICurve).PointAt(0.5)) > precision) return false;
                }
                return true;
            }
            return this.TetraederHull.SameGeometry(other, precision);
        }
        double ICurve.PositionAtLength(double position)
        {   // sehr ineffizient:
            ICurve aprox = (this as ICurve).Approximate(true, GetBoundingCube().Size / 1000);
            GeoPoint p = aprox.PointAt(aprox.PositionAtLength(aprox.Length / (this as ICurve).Length * position));
            return (this as ICurve).PositionOf(p);
        }
        double ICurve.ParameterToPosition(double parameter)
        {
            return (parameter - startParam) / (endParam - startParam);
        }
        double ICurve.PositionToParameter(double position)
        {
            return startParam + position * (endParam - startParam);
        }
        bool ICurve.Extend(double atStart, double atEnd)
        {
            return false;
        }
        BoundingCube ICurve.GetExtent()
        {
            return GetExtent(0.0);
        }
        bool ICurve.HitTest(BoundingCube cube)
        {
            return this.TetraederHull.HitTest(cube);
        }
        double[] ICurve.GetSavePositions()
        {
            // der Versuch ist noch nicht abgeschlossen, deshalb vorläufig ausklammern:
            //List<int> usplit = new List<int>();
            //GeoVector n0 = GeoVector.NullVector;
            //for (int i = 0; i < poles.Length-2; i++)
            //{
            //    if (i == 0)
            //    {
            //        n0 = (poles[i + 1] - poles[i]) ^ (poles[i + 2] - poles[i + 1]);
            //    }
            //    else
            //    {
            //        GeoVector n1 = (poles[i + 1] - poles[i]) ^ (poles[i + 2] - poles[i + 1]);
            //        if (Math.Sign(n0 * n1)<0) usplit.Add(i);
            //        n0 = n1;
            //    }
            //}

            //List<double> lres = new List<double>();
            //lres.Add(knots[0]);
            //for (int i = 0; i < usplit.Count; i++)
            //{
            //    // lineare Aufteilung der Pole auf die Knoten, ist das OK? Wenn man flatknots berücksichtigen will, muss man auch span verwenden
            //    double pos = (usplit[i]) / (double)poles.Length * knots.Length;
            //    int ind = (int)Math.Floor(pos);
            //    double rem = pos - ind;
            //    double u = knots[ind] + (knots[ind + 1] - knots[ind]) * rem;
            //    lres.Add(u);
            //}
            //lres.Add(knots[knots.Length - 1]);
            //for (int i = 0; i < lres.Count; i++)
            //{
            //    lres[i] = (lres[i] - knots[0]) / (knots[knots.Length - 1] - knots[0]);
            //}
            //for (int i = lres.Count - 1; i > 0; --i)
            //{
            //    if (lres[i] == lres[i - 1]) lres.RemoveAt(i);
            //}
            //return lres.ToArray();

            double[] res = new double[knots.Length];
            for (int i = 0; i < knots.Length; ++i)
            {
                // res[i] = (knots[i] - startParam) / (endParam - startParam); bringt u.U. negative Werte, fatal!
                res[i] = (knots[i] - knots[0]) / (knots[knots.Length - 1] - knots[0]);
            }
            return res;
        }
        double[] ICurve.GetExtrema(GeoVector direction)
        {
            Dictionary<GeoVector, double[]> dict = null;
            if (extrema == null)
            {
                extrema = new WeakReference(null);
            }
            try
            {
                if (extrema.Target != null)
                {
                    dict = extrema.Target as Dictionary<GeoVector, double[]>;
                }
            }
            catch (InvalidOperationException) { }
            if (dict == null) dict = new Dictionary<GeoVector, double[]>();
            double[] res;
            if (dict.TryGetValue(direction, out res))
            {
                return res;
            }
            res = TetraederHull.GetExtrema(direction);
            dict[direction] = res;
            extrema.Target = dict;
            return res;
        }
        double[] ICurve.GetPlaneIntersection(Plane plane)
        {
            GeoPoint[] ips;
            GeoPoint2D[] uvOnPlane;
            double[] uOnCurve;
            TetraederHull.PlaneIntersection(new PlaneSurface(plane), out ips, out uvOnPlane, out uOnCurve);
            return uOnCurve;
        }
        double ICurve.DistanceTo(GeoPoint p)
        {
            double pos = (this as ICurve).PositionOf(p);
            if (pos >= 0.0 && pos <= 1.0)
            {
                GeoPoint pCurve = (this as ICurve).PointAt(pos);
                return pCurve | p;
            }
            else
            {
                return Math.Min(p | (this as ICurve).StartPoint, p | (this as ICurve).EndPoint);
            }
        }
        bool ICurve.TryPointDeriv2At(double position, out GeoPoint point, out GeoVector deriv1, out GeoVector deriv2)
        {
            double param = startParam + position * (endParam - startParam);
            if (nubs3d != null)
            {
                GeoPoint ndir1, ndir2;
                nubs3d.CurveDeriv2(param, out point, out ndir1, out ndir2);
                deriv1 = ndir1.ToVector();
                deriv2 = ndir2.ToVector();
            }
            else if (nurbs3d != null)
            {
                GeoPointH npoint, ndir1, ndir2;
                nurbs3d.CurveDeriv2(param, out npoint, out ndir1, out ndir2);
                point = npoint;
                deriv1 = (GeoVector)ndir1;
                deriv2 = (GeoVector)ndir2;
            }
            else if (nubs2d != null)
            {
                GeoPoint2D ndir1, ndir2, point2d;
                nubs2d.CurveDeriv2(param, out point2d, out ndir1, out ndir2);
                deriv1 = plane.Value.ToGlobal(ndir1.ToVector());
                deriv2 = plane.Value.ToGlobal(ndir2.ToVector());
                point = plane.Value.ToGlobal(point2d);
            }
            else
            {
                GeoPoint2DH ndir1, ndir2, point2d;
                nurbs2d.CurveDeriv2(param, out point2d, out ndir1, out ndir2);
                deriv1 = plane.Value.ToGlobal((GeoVector2D)ndir1);
                deriv2 = plane.Value.ToGlobal((GeoVector2D)ndir2);
                point = plane.Value.ToGlobal((GeoPoint2D)point2d);
            }
            return true;
        }
        #endregion
        #region IOcasEdge Members
        #endregion
        #region IColorDef Members
        private ColorDef colorDef;
        public ColorDef ColorDef
        {
            get
            {
                return colorDef;
            }
            set
            {
                using (new ChangingAttribute(this, "ColorDef", colorDef))
                {
                    colorDef = value;
                }
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
        #region ICndHlp3DEdge Members
#if DEBUG
#endif
        private void GetPeriodicPoles(out GeoPoint[] ppoles, out double[] pweights, out double[] pknots, out int[] pmultiplicities)
        {
            List<double> knotslist = new List<double>();
            for (int i = 0; i < knots.Length; ++i)
            {
                for (int j = 0; j < multiplicities[i]; ++j)
                {
                    knotslist.Add(knots[i]);
                }
            }
            double dknot = knots[knots.Length - 1] - knots[0];
            int secondknotindex = multiplicities[0];
            for (int i = 0; i <= degree - multiplicities[0]; ++i)
            {
                knotslist.Insert(0, knotslist[knotslist.Count - degree - i] - dknot);
                ++secondknotindex;
            }
            while (knotslist.Count - degree - 1 < poles.Length + degree)
            {
                knotslist.Add(knotslist[secondknotindex] + dknot);
                ++secondknotindex;
            }
            List<double> lpknots = new List<double>();
            lpknots.Add(knotslist[0]);
            List<int> lmult = new List<int>();
            lmult.Add(1);
            for (int i = 1; i < knotslist.Count; ++i)
            {
                if (knotslist[i] == lpknots[lpknots.Count - 1])
                {
                    ++lmult[lmult.Count - 1];
                }
                else
                {
                    lpknots.Add(knotslist[i]);
                    lmult.Add(1);
                }
            }
            pknots = lpknots.ToArray();
            pmultiplicities = lmult.ToArray();
            ppoles = new GeoPoint[poles.Length + degree];
            pweights = new double[poles.Length + degree];
            for (int i = 0; i < poles.Length; ++i)
            {
                ppoles[i] = poles[i];
                if (weights != null) pweights[i] = weights[i];
                else pweights[i] = 1.0;
            }
            for (int i = 0; i < degree; ++i)
            {
                ppoles[poles.Length + i] = poles[i];
                if (weights != null) pweights[poles.Length + i] = weights[i];
                else pweights[i] = 1.0;
            }
        }
        #endregion
        internal bool GetSimpleCurve(double precision, out ICurve simpleCurve)
        {
            simpleCurve = null;
            if (degree == 1)
            {
                if (this.poles.Length == 2)
                {
                    Line l = Line.Construct();
                    l.SetTwoPoints((this as ICurve).StartPoint, (this as ICurve).EndPoint);
                    simpleCurve = l;
                    return true;
                }
                else if (poles.Length > 2)
                {
                    bool isLine = true;
                    for (int i = 1; i < poles.Length - 1; i++)
                    {
                        if (Geometry.DistPL(poles[i], (this as ICurve).StartPoint, (this as ICurve).EndPoint) > precision)
                        {
                            isLine = false;
                            break;
                        }
                    }
                    if (isLine)
                    {
                        Line l = Line.Construct();
                        l.SetTwoPoints((this as ICurve).StartPoint, (this as ICurve).EndPoint);
                        simpleCurve = l;
                        return true;
                    }
                    else
                    {
                        try
                        {
                            Polyline pl = Polyline.Construct();
                            pl.SetPoints(poles, false);
                            simpleCurve = pl;
                            return true;
                        }
                        catch (PolylineException) { }
                    }
                }
            }
            GeoPoint p1 = (this as ICurve).StartPoint;
            GeoPoint p2 = (this as ICurve).PointAt(1.0 / 3.0);
            GeoPoint p3 = (this as ICurve).PointAt(2.0 / 3.0);
            GeoPoint p4 = (this as ICurve).EndPoint;
            GeoVector v1 = (this as ICurve).StartDirection;
            GeoVector v2 = (this as ICurve).DirectionAt(1.0 / 3.0);
            GeoVector v3 = (this as ICurve).DirectionAt(2.0 / 3.0);
            GeoVector v4 = (this as ICurve).EndDirection;
            if (Precision.SameDirection(v1, v2, false) && Precision.SameDirection(v3, v4, false))
            {   // Kreis- und Ellipse(nbogen) sind hier ausgeschlossen, möglicherweise Linie
                if (Geometry.DistPL(p2, p1, p4) < Precision.eps && Geometry.DistPL(p3, p1, p4) < Precision.eps)
                {
                    bool ok = true;
                    int n = Math.Max(poles.Length - degree + 2, 4); // mindestens 4 Punkte, weil ggf aus 3 entstanden, die ja schon stimmen
                    double du = 1.0 / n;
                    for (int i = 1; i < n; i++)
                    {
                        GeoPoint pp = (this as ICurve).PointAt(i * du);
                        if (Geometry.DistPL(pp, p1, p4) > Precision.eps)
                        {
                            ok = false;
                            break;
                        }
                    }
                    if (ok)
                    {
                        Line l = Line.Construct();
                        l.SetTwoPoints(p1, p4);
                        simpleCurve = l;
                        return true;
                    }
                }
            }
            Ellipse e = Ellipse.Construct();
            try
            {
                if (Geometry.Dist(p1, p4) < Geometry.Dist(p2, p3))
                {
                    e.SetCircle3Points(p1, p2, p3, Plane.XYPlane);
                }
                else
                {
                    e.SetCircle3Points(p1, p2, p4, Plane.XYPlane);
                }
                // jetzt ist ein Kreis bestimmt. Wie gut ist dieser?
                bool ok = true;
                int n = Math.Max(poles.Length - degree + 2, 5); // mindestens 4 Punkte, weil ggf aus 3 entstanden, die ja schon stimmen
                double du = 1.0 / n;
                for (int i = 1; i < n; i++)
                {
                    GeoPoint pp = (this as ICurve).PointAt(i * du);
                    if (Math.Abs(Geometry.Dist(pp, e.Center) - e.Radius) > precision)
                    {
                        ok = false;
                        break;
                    }
                }
                if (ok)
                {
                    if (Precision.IsEqual(p1, p4))
                    {   // der Kreis muss auch beim Startpunkt der Kurve beginnen
                        GeoPoint2D sp = e.Plane.Project(p1);
                        GeoVector2D sdir = sp - GeoPoint2D.Origin;
                        Plane pln = new Plane(e.Center, e.Plane.ToGlobal(sdir), e.Plane.ToGlobal(sdir.ToLeft()));
                        e.Plane = pln;
                        if (e.StartDirection * v1 < 0.0)
                        {
                            pln = new Plane(e.Center, e.Plane.DirectionX, -e.Plane.DirectionY);
                            e.Plane = pln;
                        }
                        // bool dbg = Precision.SameNotOppositeDirection(e.StartDirection, v1);
                        simpleCurve = e;
                        return true;
                    }
                    else
                    {
                        e.SetArc3Points(p1, (this as ICurve).PointAt(0.5), p4, Plane.XYPlane);
                        if (Math.Abs(e.SweepParameter) > 0.01)
                        {   // wenn der Bogen weniger als 5° hat, ist die Berechnung zu gewagt
                            simpleCurve = e;
                            return true;
                        }
                    }
                }
            }
            catch (EllipseException)
            {
                // ist doch kein Kreis
            }
            // ein anderer Test, möglicherweise sind die Tangenten nicht ok
            // noch 3 weitere Punkte
            {
                GeoPoint p5 = (this as ICurve).PointAt(1.0 / 6.0);
                GeoPoint p6 = (this as ICurve).PointAt(3.0 / 6.0);
                GeoPoint p7 = (this as ICurve).PointAt(5.0 / 6.0);
                double md;
                bool isLine;
                Plane pln = Plane.FromPoints(new GeoPoint[] { p1, p2, p3, p4, p5, p6, p7 }, out md, out isLine);
                if (md < precision)
                {
                    GeoPoint2D[] pp2d = new GeoPoint2D[7];
                    pp2d[0] = pln.Project(p1);
                    pp2d[1] = pln.Project(p2);
                    pp2d[2] = pln.Project(p3);
                    pp2d[3] = pln.Project(p4);
                    pp2d[4] = pln.Project(p5);
                    pp2d[5] = pln.Project(p6);
                    pp2d[6] = pln.Project(p7);
                    GeoPoint2D center;
                    double radius;
                    double dd = Geometry.CircleFitLs(pp2d, out center, out radius) / pp2d.Length;
                    // dd = Math.Sqrt(dd / pp2d.Length);
                    if (dd < precision)
                    {
                        e = Ellipse.Construct();
                        if ((p1 | p4) < precision)
                        {
                            e.SetCirclePlaneCenterRadius(pln, pln.ToGlobal(center), radius);
                        }
                        else
                        {
                            GeoPoint c3d = pln.ToGlobal(center);
                            GeoVector cross = pln.ToLocal((p1 - c3d) ^ (p4 - c3d));
                            e.SetArcPlaneCenterStartEndPoint(pln, center, pp2d[0], pp2d[3], pln, cross.z > 0); // direction muss noch getestet werden
#if DEBUG
                            try
                            {
                                GeoObjectList dbg = new GeoObjectList(e);
                                Polyline pl3d = Polyline.Construct();
                                pl3d.SetPoints(new GeoPoint[] { p1, p5, p2, p6, p3, p7, p4 }, false);
                                dbg.Add(pl3d);
                            }
                            catch { }
#endif
                        }
                        simpleCurve = e;
                        return true;
                    }
                }

            }
            return false;
        }
        internal void MoveParameterSpace(double offset)
        {   // wird benötigt um den Parameterraum für Opencascade passend herzustellen
            startParam += offset;
            endParam += offset;
            for (int i = 0; i < knots.Length; i++)
            {
                knots[i] += offset;
            }
            InvalidateSecondaryData();
        }
        internal void reparametrize(double s, double e)
        {
            double f = (e - s) / (knots[knots.Length - 1] - knots[0]);
            double k0 = knots[0];
            for (int i = 0; i < knots.Length; i++)
            {
                knots[i] = s + (knots[i] - k0) * f;
            }
            startParam = s;
            endParam = e;
            InvalidateSecondaryData();
            MakeNurbsHelper();
        }

        internal double CurvatureAt(double pos)
        {
            GeoPoint point;
            GeoVector deriv1;
            GeoVector deriv2;
            if ((this as ICurve).TryPointDeriv2At(pos, out point, out deriv1, out deriv2))
            {
                double d1l = deriv1.Length;
                return (deriv1 ^ deriv2).Length / (d1l * d1l * d1l);
            }
            return 0;
        }
        internal BoundingCube GetIntervalExtent(double pmin, double pmax)
        {   // liefert die Ausdehnung eines Abschnitts der Kurve in 3D
            BoundingCube res = BoundingCube.EmptyBoundingCube;
            res.MinMax(PointAtParam(pmin));
            res.MinMax(PointAtParam(pmax));
            foreach (GeoVector dir in GeoVector.MainAxis)
            {
                double[] par = (this as ICurve).GetExtrema(dir);
                for (int i = 0; i < par.Length; ++i)
                {
                    double p = startParam + par[i] * (endParam - startParam);
                    if (p > pmin && p < pmax) res.MinMax(PointAtParam(p));
                }
            }
            return res;
        }

        ExplicitPCurve3D IExplicitPCurve3D.GetExplicitPCurve3D()
        {
            ExplicitPCurve3D res = null;
            //if (explicitPCurve3D != null && explicitPCurve3D.TryGetTarget(out res))
            if (explicitPCurve3D != null && explicitPCurve3D.IsAlive)
            {
                res = explicitPCurve3D.Target as ExplicitPCurve3D;
                if (res != null) return res;
            }
            if (nurbs3d != null)
            {
                Polynom[] px = new Polynom[knots.Length - 1];
                Polynom[] py = new Polynom[knots.Length - 1];
                Polynom[] pz = new Polynom[knots.Length - 1];
                Polynom[] pw = new Polynom[knots.Length - 1];
                for (int i = 0; i < knots.Length - 1; i++)
                {
                    Polynom[] pn = nurbs3d.CurvePointPolynom((knots[i] + knots[i + 1]) / 2.0);
                    // das sind 4 Polynome, px,py,pz,pw
                    px[i] = pn[0];
                    py[i] = pn[1];
                    pz[i] = pn[2];
                    pw[i] = pn[3];
                }
                bool isRational = false;
                for (int i = 0; i < weights.Length; i++)
                {
                    if (weights[i] != 1.0)
                    {
                        isRational = true;
                        break;
                    }
                }
                if (isRational) res = new ExplicitPCurve3D(px, py, pz, pw, knots);
                else res = new ExplicitPCurve3D(px, py, pz, null, knots);
            }
            if (nubs3d != null)
            {
                Polynom[] px = new Polynom[knots.Length - 1];
                Polynom[] py = new Polynom[knots.Length - 1];
                Polynom[] pz = new Polynom[knots.Length - 1];
                for (int i = 0; i < knots.Length - 1; i++)
                {
                    Polynom[] pn = nubs3d.CurvePointPolynom((knots[i] + knots[i + 1]) / 2.0);
                    // das sind 4 Polynome, px,py,pz,pw
                    px[i] = pn[0];
                    py[i] = pn[1];
                    pz[i] = pn[2];
                }
                res = new ExplicitPCurve3D(px, py, pz, null, knots);
            }
            if (nurbs2d != null)
            {
                Polynom[] px = new Polynom[knots.Length - 1];
                Polynom[] py = new Polynom[knots.Length - 1];
                Polynom[] pz = new Polynom[knots.Length - 1];
                Polynom[] pw = new Polynom[knots.Length - 1];
                for (int i = 0; i < knots.Length - 1; i++)
                {
                    Polynom[] pn = nurbs2d.CurvePointPolynom((knots[i] + knots[i + 1]) / 2.0);
                    // das sind 4 Polynome, px,py,pw
                    px[i] = pn[0];
                    py[i] = pn[1];
                    pz[i] = new Polynom(0.0, 1);
                    pw[i] = pn[2];
                }
                bool isRational = false;
                for (int i = 0; i < weights.Length; i++)
                {
                    if (weights[i] != 1.0)
                    {
                        isRational = true;
                        break;
                    }
                }
                if (isRational) res = new ExplicitPCurve3D(px, py, pz, pw, knots);
                else res = new ExplicitPCurve3D(px, py, pz, null, knots);
                res = res.GetModified((this as ICurve).GetPlane().ModOpToGlobal);
            }
            if (nubs2d != null)
            {
                Polynom[] px = new Polynom[knots.Length - 1];
                Polynom[] py = new Polynom[knots.Length - 1];
                Polynom[] pz = new Polynom[knots.Length - 1];
                for (int i = 0; i < knots.Length - 1; i++)
                {
                    Polynom[] pn = nubs2d.CurvePointPolynom((knots[i] + knots[i + 1]) / 2.0);
                    // das sind 4 Polynome, px,py,pz,pw
                    px[i] = pn[0];
                    py[i] = pn[1];
                    pz[i] = new Polynom(0.0, 1);
                }
                res = new ExplicitPCurve3D(px, py, pz, null, knots);
                res = res.GetModified((this as ICurve).GetPlane().ModOpToGlobal);
            }
            explicitPCurve3D = new WeakReference(res);
            return res;
        }

        public override BoundingRect GetExtent(Projection projection, ExtentPrecision extentPrecision)
        {
            return (this as ICurve).GetProjectedCurve(projection.ProjectionPlane).GetExtent();
        }

        public bool HasWeights
        {
            get
            {
                if (weights != null)
                {
                    for (int i = 0; i < weights.Length; i++)
                    {
                        if (weights[i] != 1.0) return true;
                    }
                }
                return false;
            }
        }

        int IExportStep.Export(ExportStep export, bool topLevel)
        {
            //#12410=(BOUNDED_CURVE() B_SPLINE_CURVE(4,(#12360,#12370,#12380,#12390,#12400),.UNSPECIFIED.,.F.,.F.) B_SPLINE_CURVE_WITH_KNOTS((5,5),(0.,
            //            0.70616683187357),.UNSPECIFIED.) CURVE() GEOMETRIC_REPRESENTATION_ITEM() RATIONAL_B_SPLINE_CURVE((1., 1., 0.99999757396074, 1., 1.)) REPRESENTATION_ITEM(''));
            string spoles = export.Write(poles);
            int bsp;
            if (HasWeights)
            {
                bsp = export.WriteDefinition("(BOUNDED_CURVE() B_SPLINE_CURVE(" + degree.ToString() + ",(" + spoles + "),.UNSPECIFIED.,.F.,.F.) B_SPLINE_CURVE_WITH_KNOTS(("
                    + export.ToString(multiplicities, false) + "),(" + export.ToString(knots) + "),.UNSPECIFIED.) CURVE() GEOMETRIC_REPRESENTATION_ITEM() RATIONAL_B_SPLINE_CURVE(("
                    + export.ToString(weights) + ")) REPRESENTATION_ITEM(''))");
            }
            else
            {
                //#113 = B_SPLINE_CURVE_WITH_KNOTS('',4,(#114,#115,#116,#117,#118,#119,
                //#120,#121,#122,#123,#124,#125,#126,#127,#128,#129,#130,#131,#132,
                //#133,#134,#135,#136,#137,#138,#139),.UNSPECIFIED.,.F.,.F.,(5,3,3,3,3
                //,3,3,3,5),(0.E + 000, 7.638112305017E-002, 0.1527622461, 0.229143369151,
                //0.305524492201, 0.381905615251, 0.458286738301, 0.534667861351,
                //0.611048984401),.UNSPECIFIED.);
                bsp = export.WriteDefinition("B_SPLINE_CURVE_WITH_KNOTS(''," + degree.ToString() + ",(" + spoles + "),.UNSPECIFIED.,.F.,.F.,("
                    + export.ToString(multiplicities, false) + "),(" + export.ToString(knots) + "),.UNSPECIFIED.)");
            }
            if (topLevel)
            {
                int gcs = export.WriteDefinition("GEOMETRIC_CURVE_SET('',(#" + bsp.ToString() + "))");
                ColorDef cd = ColorDef;
                if (cd == null) cd = new ColorDef("Black", Color.Black);
                cd.MakeStepStyle(gcs, export);
                int product = export.WriteDefinition("PRODUCT( '','','',(#2))");
                int pdf = export.WriteDefinition("PRODUCT_DEFINITION_FORMATION_WITH_SPECIFIED_SOURCE( ' ', 'NONE', #" + product.ToString() + ", .NOT_KNOWN. )");
                int pd = export.WriteDefinition("PRODUCT_DEFINITION( 'NONE', 'NONE', #" + pdf.ToString() + ", #3 )");
                int pds = export.WriteDefinition("PRODUCT_DEFINITION_SHAPE( 'NONE', 'NONE', #" + pd.ToString() + " )");
                int sr = export.WriteDefinition("SHAPE_REPRESENTATION('', ( #" + gcs.ToString() + "), #4 )");
                export.WriteDefinition("SHAPE_DEFINITION_REPRESENTATION( #" + pds.ToString() + ", #" + sr.ToString() + ")");
                return sr;
            }
            else
            {
                return bsp;
            }

        }
#if DEBUG
        internal Polyline DebugSegments
        {
            get
            {
                int dbglen = 1000;
                List<GeoPoint> vtx = new List<GeoPoint>();
                for (int i = 0; i < dbglen; i++)
                {
                    GeoPoint v = (this as ICurve).PointAt(i * 1.0 / dbglen);
                    if (vtx.Count > 0)
                    {
                        if ((vtx[vtx.Count - 1] | v) > Precision.eps) vtx.Add(v);
                    }
                    else
                    {
                        vtx.Add(v);
                    }
                }
                if (vtx.Count > 0)
                {
                    Polyline res = Polyline.Construct();
                    res.SetPoints(vtx.ToArray(), false);
                    return res;
                }
                else return null;
            }
        }
#endif
    }

    internal class QuadTreeBSpline : IQuadTreeInsertableZ
    {
        BSpline bspline;
        BSpline2D bspline2d;
        Projection projection; // wenn gesetzt, dann nicht eben
        double fx, fy, c;
        bool zInitialized;
        public QuadTreeBSpline(BSpline bspline, BSpline2D bspline2d, Projection projection)
        {
            this.bspline = bspline;
            this.bspline2d = bspline2d;
            zInitialized = false;
            this.projection = projection;
        }
        #region IQuadTreeInsertableZ Members

        double IQuadTreeInsertableZ.GetZPosition(GeoPoint2D p)
        {
            if (!zInitialized)
            {
                if ((bspline as ICurve).GetPlanarState() == PlanarState.Planar)
                {
                    Plane plane = (bspline as ICurve).GetPlane();
                    GeoPoint p1 = projection.UnscaledProjection * plane.Location;
                    GeoPoint p2 = projection.UnscaledProjection * (plane.Location + plane.DirectionX);
                    GeoPoint p3 = projection.UnscaledProjection * (plane.Location + plane.DirectionY);
                    // die Werte fx, fy und c bestimmen die Z-Position
                    double[,] m = new double[3, 3];
                    m[0, 0] = p1.x;
                    m[0, 1] = p1.y;
                    m[0, 2] = 1.0;
                    m[1, 0] = p2.x;
                    m[1, 1] = p2.y;
                    m[1, 2] = 1.0;
                    m[2, 0] = p3.x;
                    m[2, 1] = p3.y;
                    m[2, 2] = 1.0;
                    double[] b = new double[] {  p1.z ,  p2.z ,  p3.z  };
                    Matrix mx = DenseMatrix.OfArray(m);
                    Vector s = (Vector)mx.Solve(new DenseVector(b));
                    if (s != null)
                    {
                        fx = s[0];
                        fy = s[1];
                        c = s[2];
                        this.projection = null; // da es geklappt hat
                    }
                }
                zInitialized = true;
            }
            if (projection != null)
            {
                double pos = bspline2d.PositionOf(p);
                GeoPoint p3d = (bspline as ICurve).PointAt(pos);
                GeoPoint p1 = projection.UnscaledProjection * p3d;
                return p3d.z;
            }
            else
            {
                return fx * p.x + fy * p.y + c;
            }
        }

        #endregion

        #region IQuadTreeInsertable Members

        BoundingRect IQuadTreeInsertable.GetExtent()
        {
            return bspline2d.GetExtent();
        }

        bool IQuadTreeInsertable.HitTest(ref BoundingRect rect, bool includeControlPoints)
        {
            return bspline2d.HitTest(ref rect, includeControlPoints);
        }

        object IQuadTreeInsertable.ReferencedObject
        {
            get { return bspline; }
        }

        #endregion
    }
}
