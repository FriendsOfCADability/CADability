// #define DEBUGCURVE

using System;
using System.Collections;
using System.Collections.Generic;
using CADability.GeoObject;
using System.Runtime.Serialization;
using Wintellect.PowerCollections;
using MathNet.Numerics.Optimization;
using MathNet.Numerics.LinearAlgebra;
using MathNet.Numerics.LinearAlgebra.Double;

namespace CADability.Curve2D
{

    public class CompareGeoPoint2DWithParameter1 : IComparer
    {
        public CompareGeoPoint2DWithParameter1() { }
        #region IComparer Members

        public int Compare(object x, object y)
        {
            GeoPoint2DWithParameter px = (GeoPoint2DWithParameter)x;
            GeoPoint2DWithParameter py = (GeoPoint2DWithParameter)y;
            if (px.par1 < py.par1) return -1;
            if (px.par1 > py.par1) return 1;
            return 0;
        }

        #endregion
    }

    public class ICompareGeoPoint2DWithParameter1 : IComparer<GeoPoint2DWithParameter>
    {
        public ICompareGeoPoint2DWithParameter1() { }
        #region IComparer<GeoPoint2DWithParameter> Members

        int IComparer<GeoPoint2DWithParameter>.Compare(GeoPoint2DWithParameter x, GeoPoint2DWithParameter y)
        {
            if (x.par1 < y.par1) return -1;
            if (x.par1 > y.par1) return 1;
            return 0;
        }

        #endregion
    }

    public class ICompareGeoPoint2DWithParameter2 : IComparer<GeoPoint2DWithParameter>
    {
        public ICompareGeoPoint2DWithParameter2() { }
        #region IComparer<GeoPoint2DWithParameter> Members

        int IComparer<GeoPoint2DWithParameter>.Compare(GeoPoint2DWithParameter x, GeoPoint2DWithParameter y)
        {
            if (x.par2 < y.par2) return -1;
            if (x.par2 > y.par2) return 1;
            return 0;
        }

        #endregion
    }

    public struct GeoPoint2DWithParameter
    {
        public GeoPoint2DWithParameter(GeoPoint2D p, double par1, double par2)
        {
            this.p = p;
            this.par1 = par1;
            this.par2 = par2;
        }
        public GeoPoint2D p; // der Punkt, meist ein Schnittpunkt
        public double par1, par2; // Parameter auf der 1. bzw. 2. beteiligten Kurve
    }


    public class Curve2DException : System.ApplicationException
    {
        public enum Curve2DExceptionType { General, LineIsNull, InternalError };
        public Curve2DExceptionType ExceptionType;
        public Curve2DException(string message, Curve2DExceptionType tp)
            : base(message)
        {
            ExceptionType = tp;
        }
    }

    public enum ParallelFlag { Standard = 0, RoundEdges = 1 }
    /// <summary>
    /// Zum verschneiden von unendlichen Linien, Parabeln, Hyperbeln(, Sinuskurven?), die nicht längenbeschränkt sind,
    /// und nicht unbedingt im Parameter von 0 bis 1 laufen
    /// </summary>
    internal interface I2DIntersectable
    {
        GeoPoint2D[] IntersectWith(I2DIntersectable other);
    }
    /// <summary>
    /// Interface for a 2-dimensional curve.
    /// 2-dimensional curves occur e.g. in the parametric system of surfaces to build the outline of faces or
    /// as projections of 3-dimensional curves on a plane.
    /// There is a normalized parametric system for the curve that starts at 0.0 and ends at 1.0
    /// </summary>

    public interface ICurve2D : IQuadTreeInsertable
    {
        /// <summary>
        /// Sets or gets the startpoint. The endpoint remains unchanged when setting the startpoint.
        /// </summary>
        GeoPoint2D StartPoint { get; set; }
        /// <summary>
        /// Sets or gets the endpoint of the curve. When the endpoint is set, the startpoint remains unchanged.
        /// </summary>
        GeoPoint2D EndPoint { get; set; }
        /// <summary>
        /// Returns the direction of the curve at the startpoint.
        /// </summary>
        GeoVector2D StartDirection { get; }
        /// <summary>
        /// Returns the direction of the curve at the endpoint.
        /// </summary>
        GeoVector2D EndDirection { get; }
        /// <summary>
        /// Returns the direction of the curve at its middle point.
        /// </summary>
        GeoVector2D MiddleDirection { get; }
        /// <summary>
        /// Returns the direction of the curve at the provided normalized position.
        /// </summary>
        /// <param name="Position">Where to get the direction</param>
        /// <returns>The direction</returns>
        GeoVector2D DirectionAt(double Position);
        /// <summary>
        /// Returns the point of the curve at the provided normalized position
        /// </summary>
        /// <param name="Position">Where to get the direction</param>
        /// <returns>The point</returns>
        GeoPoint2D PointAt(double Position);
        /// <summary>
        /// Returns the position of point p on the curve: 0.0 corresponds to the StartPoint
        /// 1.0 to the EndPoint
        /// </summary>
        /// <param name="p"></param>
        /// <returns></returns>
        double PositionOf(GeoPoint2D p);
        double PositionAtLength(double position);
        /// <summary>
        /// Returns the length of the curve
        /// </summary>
        double Length { get; }
        double GetAreaFromPoint(GeoPoint2D p);
        double GetArea();
        double Sweep { get; }
        ICurve2D[] Split(double Position);
        /// <summary>
        /// Returns the distance of the specified point to this curve. If this curve
        /// can be extended (e.g. a line or an arc) this method will return the distance to
        /// the extended curve. If you need the distance to the unextended curve use <see cref="MinDistance"/>.
        /// </summary>
        /// <param name="p">Point to compute the distance to</param>
        /// <returns>the distance</returns>
        double Distance(GeoPoint2D p);
        /// <summary>
        /// Returns the minimal distance between this curve and the other curve given in the parameter.
        /// If the two curves intersect, the minimal distance ist 0.
        /// </summary>
        /// <param name="Other">curve to compute the distance to</param>
        /// <returns></returns>
        double MinDistance(ICurve2D Other);
        /// <summary>
        /// Returns the distance of the point to the curve. In opposite to <see cref="Distance"/>
        /// this method will return the distance to the unextended curve
        /// </summary>
        /// <param name="p">Point to compute the distance to</param>
        /// <returns>the distance</returns>
        double MinDistance(GeoPoint2D p);
        ICurve2D Trim(double StartPos, double EndPos);
        ICurve2D Parallel(double Dist, bool approxSpline, double precision, double roundAngle);
        GeoPoint2DWithParameter[] Intersect(ICurve2D IntersectWith);
        GeoPoint2DWithParameter[] Intersect(GeoPoint2D StartPoint, GeoPoint2D EndPoint);
        /// <summary>
        /// Calculates the foot points of the perpendicular projection of the given point
        /// on the curve. Perpendicular foot points are points, where the direction of the 
        /// curve ist perpendicular to the line that connects foot-point to the given point.
        /// </summary>
        /// <param name="FromHere">from this point</param>
        /// <returns>array of foot points</returns>
        GeoPoint2D[] PerpendicularFoot(GeoPoint2D FromHere);
        /// <summary>
        /// Calculates the points where the direction of the curve is parallel to the direction
        /// from the given point to the calculated point. For circles and ellipses it reveals all
        /// possible solutions, for other curves (e.g. BSpline) it reveals only the closest 
        /// solution to the point "CloseTo".
        /// </summary>
        /// <param name="FromHere">From this point</param>
        /// <param name="CloseTo">Find the solution close to this point</param>
        /// <returns>array of tangential points</returns>
        GeoPoint2D[] TangentPoints(GeoPoint2D FromHere, GeoPoint2D CloseTo);
        /// <summary>
        /// Calculates the points where the direction of the curve is parallel to the direction
        /// of the given angle. For circles and ellipses it reveals all
        /// possible solutions, for other curves (e.g. BSpline) it reveals only the closest 
        /// solution to the point "CloseTo".
        /// </summary>
        /// <param name="ang">the angle of the tangent</param>
        /// <param name="CloseTo">Find the solution close to this point</param>
        /// <returns>array of tangential points</returns>
        GeoPoint2D[] TangentPointsToAngle(Angle ang, GeoPoint2D CloseTo);
        /// <summary>
        /// Calculates the points where the direction of the curve is parallel to the given direction.
        /// Returns all possible position within the curve (not the extended curve)
        /// </summary>
        /// <param name="direction">the direction</param>
        /// <returns>positions found</returns>
        double[] TangentPointsToAngle(GeoVector2D direction);
        /// <summary>
        /// Returns a list of inflection points of this curve. The list might be empty
        /// </summary>
        /// <returns>List of inflection points</returns>
        double[] GetInflectionPoints();
        /// <summary>
        /// Reverses the direction of this curve
        /// </summary>
        void Reverse();
        ICurve2D Clone();
        ICurve2D CloneReverse(bool reverse);
        /// <summary>
        /// Copies the data of the given object to this object. Both objects must be of the same type.
        /// </summary>
        /// <param name="toCopyFrom">Copies the data of this object</param>
        void Copy(ICurve2D toCopyFrom);
        IGeoObject MakeGeoObject(Plane p);
        /// <summary>
        /// The 2d-curve is assumed to reside in plane "fromPlane". It will be projected
        /// perpendicular onto the plane "toPlane".
        /// </summary>
        /// <param name="fromPlane">the containing plane</param>
        /// <param name="toPlane">the projection plane</param>
        /// <returns>the projected curve</returns>
        ICurve2D Project(Plane fromPlane, Plane toPlane);
        void AddToGraphicsPath(System.Drawing.Drawing2D.GraphicsPath path, bool forward);
        /// <summary>
        /// Determines, whether the given parameter denotes a point inside the bounds of the curve.
        /// i.e. the parameter must be greater or equal to 0.0 and less than or equal 1.0.
        /// The actual interval is a little bit extended according to the <see cref="Precision.eps"/> value.
        /// </summary>
        /// <param name="par">the parameter to test</param>
        /// <returns>true if on curve</returns>
        bool IsParameterOnCurve(double par);
        /// <summary>
        /// Determines, whether the given parameter is valid for this curve. Some curves are restricted to a 
        /// parameterrange by their nature.
        /// </summary>
        /// <param name="par">the parameter to test</param>
        /// <returns>true if valid</returns>
        bool IsValidParameter(double par);
        /// <summary>
        /// Returns an IQuadTreeInsertable interface. The HitTest Method of this interface
        /// returns true if the rectangle coincides with the infinite extension of this curve
        /// (unbounded curve). Most curves are not extendable and therefore simply return "this".
        /// </summary>
        /// <returns></returns>
        IQuadTreeInsertable GetExtendedHitTest();
        /// <summary>
        /// Returns pairs of double values. Each pair defines the two parameters of the curve
        /// of a self intersection. The result is either empty or has an even number of double values.
        /// </summary>
        /// <returns>self intersection parameters</returns>
        double[] GetSelfIntersections();
        /// <summary>
        /// Some curves especially Arc and EllipseArc have two possibilities to define
        /// a parameter of a point outside the curve: either as a parameter less than 0
        /// which means ahead of the startpoint or as a parameter greater than 1 behind 
        /// the endpoint. This method brings the given parameter into the other system,
        /// that means a parameter less than 0 will become greater than 1 and vice versa.
        /// Parameters between 0 and 1 will remain unchanged as well as most curves
        /// will return false, because the double interpretation isnt meaningfull.
        /// </summary>
        /// <param name="p">parameter to change</param>
        /// <returns>true if possible, false otherwise</returns>
        bool ReinterpretParameter(ref double p);
        /// <summary>
        /// Approximate this curve and return the approximation. maxError specifies the maximal
        /// allowed error, i.e. the maximal deviation of the returned curve from this curve.
        /// Is linesOnly ist true, only lines are returned, if linesOnly is false, lines
        /// and circular arcs are returned. The result is usually a path consiting of lines and 
        /// arcs or lines only.
        /// </summary>
        /// <param name="linesOnly"></param>
        /// <param name="maxError"></param>
        /// <returns></returns>
        ICurve2D Approximate(bool linesOnly, double maxError);
        /// <summary>
        /// Move the curve by the given offset
        /// </summary>
        /// <param name="x"></param>
        /// <param name="y"></param>
        void Move(double x, double y);
        /// <summary>
        /// Determins wether this curve is a closed curve (e.g. a circle)
        /// </summary>
        bool IsClosed { get; }
        /// <summary>
        /// Returns a ICurve which is this curve modified by m. The curve can be of different type, e.g. a 
        /// Circle may return an Ellpse
        /// </summary>
        /// <param name="m"></param>
        /// <returns></returns>
        ICurve2D GetModified(ModOp2D m);
        UserData UserData { get; }
        /// <summary>
        /// Returnes a new curve of the same type which is the fusion of this curve with the provided curve, e.g. two
        /// overlapping lines build a longer line. Returnes null if the fusion is not possible (e.g. a line and an arc 
        /// or two lines with different direction)
        /// </summary>
        /// <param name="toFuseWith">Curve to fuse with</param>
        /// <param name="precision">the fused curve or null</param>
        /// <returns></returns>
        ICurve2D GetFused(ICurve2D toFuseWith, double precision);
        /// <summary>
        /// Tries to get the point and the first and second derivative of the curve at the specified position. (0..1)
        /// Some curves do not implement the second derivative and hence will return false.
        /// </summary>
        /// <param name="position">Position where to calculate point and derivatives</param>
        /// <param name="point">The point at the required position</param>
        /// <param name="deriv">The first derivative at the provided parameter</param>
        /// <param name="deriv2">The second derivative at the provided parameter</param>
        /// <returns></returns>
        bool TryPointDeriv2At(double position, out GeoPoint2D point, out GeoVector2D deriv1, out GeoVector2D deriv2);
    }

    /// <summary>
    /// This class serves as an implementation helper for the ICurve2D interface.
    /// It cannot be instantiated and there are no methods for public use.
    /// </summary>
    public abstract class GeneralCurve2Dold : ICurve2D, I2DIntersectable
    {
        /* Weg von Opencascade!
         * Diese Klasse auf Triangulierung umstellen und weg von OpenCascade bringen.
         * Schnitt, Tangenten, Absatnd u.s.w. alles ohne OCas.
         * Erzeugt eine Traingulierung und behält Verbesserungen derselben bis zu einem gewissen Grad bei
         */
        protected GeneralCurve2Dold()
        {
            // wird nur durch die static Methoden erzeugt
        }
        internal static void Hide(ref double[] segments, double from, double to)
        {
            List<double> list;
            if (segments == null)
            {
                list = new List<double>(2);
                list.Add(0.0);
                list.Add(1.0);
            }
            else
            {
                if (segments.Length == 0) return; // segment ist sowieso unsichtbar, kann man nichts wegnehmen
                list = new List<double>(segments);
            }
            int startIndex = -1, endIndex = -1; // nach welchem Index kommt from bzw. to
            for (int i = 0; i < list.Count - 1; i++)
            {
                if (from >= list[i] && from < list[i + 1])
                {
                    startIndex = i;
                    break;
                }
            }
            if (startIndex == -1)
            {
                if (from >= list[list.Count - 1]) return; // das wegzunehmende ist größer als alle Indizes
                // startindex bleibt -1, d. h. vor index 0
            }
            for (int i = Math.Max(startIndex, 0); i < list.Count - 1; i++)
            {
                if (to >= list[i] && to < list[i + 1])
                {
                    endIndex = i;
                    break;
                }
            }
            if (endIndex == -1)
            {
                if (to >= list[list.Count - 1]) endIndex = list.Count - 1; // nach dem letzten
                // ansonsten bleibst mit -1 vor dem ersten
            }
            if (startIndex == endIndex)
            {

                if ((startIndex & 0x01) == 0x01)
                    return; // beide fallen in dieselbe Lücke, es gibt nichts zu tun
                list.Add(from);
                list.Add(to);
                list.Sort(); // beide fallen in das selbe Intervall
            }
            else
            {
                // alle wegnehmen, die in dem neuen Intervall liegen
                for (int i = endIndex; i > startIndex; --i)
                {
                    list.RemoveAt(i);
                }
                // alle hinzunehmen, die in einem Intervall lagen
                if ((startIndex & 0x01) == 0x00) list.Add(from);
                if ((endIndex & 0x01) == 0x00) list.Add(to);
                list.Sort();
            }
            for (int i = list.Count - 2; i >= 0; --i)
            {
                if (list[i + 1] - list[i] < 1e-9)
                {   // zwei Punkte, die zu dicht liegen, entfernen. Dabei ist es egal,
                    // ob zwei Intervalle aneinanderstoßen oder ein Intervall ganz kurz ist.
                    list.RemoveAt(i + 1);
                    list.RemoveAt(i);
                    --i;
                }
            }
            segments = list.ToArray();
        }
        internal virtual void GetTriangulationPoints(out GeoPoint2D[] interpol, out double[] interparam)
        {
            int n = 5;  // das ist gur für Kreise und Ellipsen und deren Bögen
            interpol = new GeoPoint2D[n];
            interparam = new double[n];
            for (int i = 0; i < n; ++i)
            {
                interpol[i] = (this as ICurve2D).PointAt(i * 1.0 / (n - 1));
                interparam[i] = (i * 1.0 / (n - 1));
            }
        }

        #region ICurve2D Members
        public virtual GeoPoint2D StartPoint
        {
            get
            {
                throw new NotImplementedException();
            }
            set
            {
                throw new NotImplementedException();
            }
        }
        public virtual GeoPoint2D EndPoint
        {
            get
            {
                throw new NotImplementedException();
            }
            set
            {
                throw new NotImplementedException();
            }
        }
        public virtual GeoVector2D StartDirection
        {
            get
            {
                throw new NotImplementedException();
            }
        }

        public virtual GeoVector2D EndDirection
        {
            get
            {
                throw new NotImplementedException();
            }
            set
            {
                throw new NotImplementedException();
            }
        }


        public virtual GeoVector2D MiddleDirection
        {
            get
            {
                return (this as ICurve2D).DirectionAt(0.5);
                //GeoVector2D res;
                //res = new GeoVector2D(OCasBuddy.GetDirectionAt((OCasBuddy.StartPar + OCasBuddy.EndPar) / 2.0));
                //return res;
            }
        }
        public virtual GeoVector2D DirectionAt(double Position)
        {
            throw new NotImplementedException();
        }
        public virtual GeoPoint2D PointAt(double Position)
        {
            throw new NotImplementedException();
        }
        public virtual double PositionOf(GeoPoint2D p)
        {
            throw new NotImplementedException();
        }

        /// <summary>
        /// Implements <see cref="CADability.Curve2D.ICurve2D.PositionAtLength (double)"/>
        /// </summary>
        /// <param name="position"></param>
        /// <returns></returns>
        public virtual double PositionAtLength(double position)
        {
            return position / Length;
        }
        public virtual double Length
        {
            get
            {
                BoundingRect ext = GetExtent();
                return Math.Sqrt(ext.Width * ext.Width + ext.Height * ext.Height);
                // wird z.Z. nur zur Abschätzung verwendet, hat mit der echten Länge nix zu tun
                // stimmt halt bei der Linie
                // throw new NotImplementedException("GeneralCurve2D.Length");
            }
        }
        public virtual double Sweep { get { return new SweepAngle((this as ICurve2D).StartDirection, (this as ICurve2D).EndDirection); } }
        /// <summary>
        /// Implements <see cref="CADability.Curve2D.ICurve2D.GetAreaFromPoint (GeoPoint2D)"/>
        /// </summary>
        /// <param name="p"></param>
        /// <returns></returns>
        public virtual double GetAreaFromPoint(GeoPoint2D p)
        {
            return GetModified(ModOp2D.Translate(-p.x, -p.y)).GetArea();
        }
        /// <summary>
        /// Implements <see cref="CADability.Curve2D.ICurve2D.GetArea ()"/>
        /// </summary>
        /// <returns></returns>
        public virtual double GetArea()
        {
            // für Flächen des BSplines könnte man folgendes verwenden:
            // http://www2.hs-esslingen.de/~mohr/mathematik/me1/diff-geo.pdf Kapitel 5
            // Dazu knotenweise die Polynome bestimmen und mit der Klasse Polynom das Integral berechnen 
            ICurve2D app = Approximate(this is BSpline2D, Precision.eps);
            return app.GetArea(); // Path, Polyline, Line, Arc und Circle implementieren das schon richtig, nich rationale BSpline2Ds jetzt auch (12.17)
        }
        /// <summary>
        /// Implements <see cref="CADability.Curve2D.ICurve2D.Split (double)"/>
        /// </summary>
        /// <param name="Position"></param>
        /// <returns></returns>
        public abstract ICurve2D[] Split(double Position);
        //{	// TODO: Position muss noch umgerechnet werden
        //    ICurve2D[] res = new ICurve2D[2];
        //    GeneralCurve2D c0 = GeneralCurve2D.FromOcas(OCasBuddy);
        //    GeneralCurve2D c1 = GeneralCurve2D.FromOcas(OCasBuddy);
        //    c0.OCasBuddy.EndParameter = Position;
        //    c1.OCasBuddy.StartParameter = Position;
        //    res[0] = c0;
        //    res[1] = c1;
        //    return res;
        //}
        /// <summary>
        /// Implements <see cref="CADability.Curve2D.ICurve2D.Distance (GeoPoint2D)"/>
        /// </summary>
        /// <param name="p"></param>
        /// <returns></returns>
        public virtual double Distance(GeoPoint2D p)
        {
            GeoPoint2D[] feet = PerpendicularFoot(p);
            double res = double.MaxValue;
            for (int i = 0; i < feet.Length; ++i)
            {
                double d = Geometry.Dist(p, feet[i]);
                if (d < res)
                {
                    if (IsParameterOnCurve((this as ICurve2D).PositionOf(feet[i])))
                        res = d;
                }
            }
            if (feet.Length == 0)
            {
                res = Math.Min(p | (this as ICurve2D).StartPoint, p | (this as ICurve2D).EndPoint);
            }
            return res;
        }
        /// <summary>
        /// Implements <see cref="CADability.Curve2D.ICurve2D.Trim (double, double)"/>
        /// </summary>
        /// <param name="StartPos"></param>
        /// <param name="EndPos"></param>
        /// <returns></returns>
        public virtual ICurve2D Trim(double StartPos, double EndPos)
        {
            throw new NotImplementedException();
        }
        /// <summary>
        /// Implements <see cref="CADability.Curve2D.ICurve2D.MinDistance (ICurve2D)"/>
        /// </summary>
        /// <param name="Other"></param>
        /// <returns></returns>
        public virtual double MinDistance(ICurve2D Other)
        {
            double minDist = Curves2D.SimpleMinimumDistance(this, Other);
            GeneralCurve2Dold g2d = Other as GeneralCurve2Dold;
            if (g2d != null)
            {   // openCascade geht nicht, da u.U. multithreaded
                //if (OCasBuddy.MinPerpendicularDistance(g2d.OCasBuddy, ref minDist))
                //{	// naja, wenn nicht, dann bleibt es halt unverändert
                //}
            }
            return minDist;
        }
        /// <summary>
        /// Implements <see cref="CADability.Curve2D.ICurve2D.MinDistance (GeoPoint2D)"/>
        /// </summary>
        /// <param name="p"></param>
        /// <returns></returns>
        public virtual double MinDistance(GeoPoint2D p)
        {
            GeoPoint2D[] feet = PerpendicularFoot(p);
            double res = double.MaxValue;
            for (int i = 0; i < feet.Length; ++i)
            {
                double par = (this as ICurve2D).PositionOf(feet[i]);
                if (IsParameterOnCurve(par))
                {
                    double d = Geometry.Dist(feet[i], p);
                    if (d < res) res = d;
                }
            }
            double dd = Geometry.Dist((this as ICurve2D).StartPoint, p);
            if (dd < res) res = dd;
            dd = Geometry.Dist((this as ICurve2D).EndPoint, p);
            if (dd < res) res = dd;
            return res;
        }
        /// <summary>
        /// Implements <see cref="CADability.Curve2D.ICurve2D.Parallel (double, bool, double, double)"/>
        /// </summary>
        /// <param name="Dist"></param>
        /// <param name="approxSpline"></param>
        /// <param name="precision"></param>
        /// <param name="roundAngle"></param>
        /// <returns></returns>
        public virtual ICurve2D Parallel(double Dist, bool approxSpline, double precision, double roundAngle)
        {
            ICurve2D c2d = Approximate(true, precision * 10); // das ist i.a. ein Pfad aus Linien und Bögen
            // geändert in nur Linien, da Ellipse mit Äquidistantenfüllung z.B. Probleme macht
            return c2d.Parallel(Dist, approxSpline, precision, roundAngle);
        }
        /// <summary>
        /// Implements <see cref="CADability.Curve2D.ICurve2D.Intersect (ICurve2D)"/>
        /// </summary>
        /// <param name="IntersectWith"></param>
        /// <returns></returns>
        public virtual GeoPoint2DWithParameter[] Intersect(ICurve2D IntersectWith)
        {
            if (IntersectWith is Polyline2D)
            {
                GeoPoint2DWithParameter[] res = IntersectWith.Intersect(this);
                for (int i = 0; i < res.Length; ++i)
                {
                    double tmp = res[i].par1;
                    res[i].par1 = res[i].par2;
                    res[i].par2 = tmp;
                }
                return res;
            }
            if (IntersectWith is Path2D)
            {
                GeoPoint2DWithParameter[] res = IntersectWith.Intersect(this);
                for (int i = 0; i < res.Length; ++i)
                {
                    double tmp = res[i].par1;
                    res[i].par1 = res[i].par2;
                    res[i].par2 = tmp;
                }
                return res;
            }
            if (IntersectWith is GeneralCurve2D)
            {
                GeneralCurve2D t2d = IntersectWith as GeneralCurve2D;
                GeoPoint2DWithParameter[] res = t2d.Intersect(this);
                for (int i = 0; i < res.Length; i++)
                {
                    double tmp = res[i].par1;
                    res[i].par1 = res[i].par2;
                    res[i].par2 = tmp;
                }
                return res;
            }
            //TempTriangulatedCurve2D tt = new TempTriangulatedCurve2D(this);
            //return tt.Intersect(IntersectWith);
            throw new NotImplementedException("GeneralCurve2Dold.Intersect");
        }
        private static GeoPoint2DWithParameter[] CheckTriangleIntersect(GeneralCurve2Dold curve1, GeoPoint2D sp1, GeoPoint2D ep1, double spar1, double epar1, GeoVector2D sd1, GeoVector2D ed1,
            GeneralCurve2Dold curve2, GeoPoint2D sp2, GeoPoint2D ep2, double spar2, double epar2, GeoVector2D sd2, GeoVector2D ed2)
        {
            // testet die beiden gegebenen Dreiecke auf Schnitt.
            // Die Dreiecke hüllen jeweils ein Kurvenstück ein, d.h. wenn die Dreiecke sich schneiden 
            // oder eines im anderen liegt, dann kann es einen Schnittpunkt geben, sonst nicht.
            // wir arbeiten hier mit sicherer Bisektion
            if (Precision.IsEqual(sp1, sp2))
            {
                GeoPoint2DWithParameter res = new GeoPoint2DWithParameter();
                res.p.x = (sp1.x + sp2.x) / 2.0;
                res.p.y = (sp1.y + sp2.y) / 2.0;
                res.par1 = spar1;
                res.par2 = spar2;
                return new GeoPoint2DWithParameter[] { res };
            }
            if (Precision.IsEqual(sp1, ep2))
            {
                GeoPoint2DWithParameter res = new GeoPoint2DWithParameter();
                res.p.x = (sp1.x + ep2.x) / 2.0;
                res.p.y = (sp1.y + ep2.y) / 2.0;
                res.par1 = spar1;
                res.par2 = epar2;
                return new GeoPoint2DWithParameter[] { res };
            }
            if (Precision.IsEqual(ep1, ep2))
            {
                GeoPoint2DWithParameter res = new GeoPoint2DWithParameter();
                res.p.x = (ep1.x + ep2.x) / 2.0;
                res.p.y = (ep1.y + ep2.y) / 2.0;
                res.par1 = epar1;
                res.par2 = epar2;
                return new GeoPoint2DWithParameter[] { res };
            }
            if (Precision.IsEqual(ep1, sp2))
            {
                GeoPoint2DWithParameter res = new GeoPoint2DWithParameter();
                res.p.x = (ep1.x + sp2.x) / 2.0;
                res.p.y = (ep1.y + sp2.y) / 2.0;
                res.par1 = epar1;
                res.par2 = spar2;
                return new GeoPoint2DWithParameter[] { res };
            }
            bool intersects = false;
            intersects = Geometry.SegmentIntersection(sp1, ep1, sp2, ep2); // Schnitt der Grundlinien
            if (!intersects)
            {   // Dreieckspunkt berechnen
                GeoPoint2D ip1;
                bool notlinear1 = Geometry.IntersectLL(sp1, sd1, ep1, ed1, out ip1);
                GeoPoint2D ip2;
                bool notlinear2 = Geometry.IntersectLL(sp2, sd2, ep2, ed2, out ip2);
                if (notlinear1 && notlinear2)
                {   // beides echte Dreiecke
                    intersects =
                        Geometry.SegmentIntersection(sp1, ep1, sp2, ip2) ||
                        Geometry.SegmentIntersection(sp1, ep1, ip2, ep2) ||
                        Geometry.SegmentIntersection(sp1, ip1, sp2, ip2) ||
                        Geometry.SegmentIntersection(sp1, ip1, ip2, ep2) ||
                        Geometry.SegmentIntersection(sp1, ip1, sp2, ep2) ||
                        Geometry.SegmentIntersection(ip1, ep1, sp2, ip2) ||
                        Geometry.SegmentIntersection(ip1, ep1, ip2, ep2) ||
                        Geometry.SegmentIntersection(ip1, ep1, sp2, ep2);
                    if (!intersects)
                    {   // es könnte sein, dass das eine dreieck ganz im anderen liegt
                        // zuerst sp2 testen, das ist so gut wie irgend ein Punkt von Dreieck 2
                        // hier ist es wichtig, dass die Richtungen in Richtung der Kurve sind und die reihenfolge der Punkte stimmt
                        bool l11 = Geometry.OnLeftSide(sp2, sp1, sd1); // links von einer Seite
                        bool l21 = Geometry.OnLeftSide(sp2, ep1, ed1); // links von der anderen Seite
                        bool l31 = Geometry.OnLeftSide(sp2, sp1, ep1 - sp1); // links von der Grundlinie
                        bool l12 = Geometry.OnLeftSide(sp1, sp2, sd2);
                        bool l22 = Geometry.OnLeftSide(sp1, ep2, ed2);
                        bool l32 = Geometry.OnLeftSide(sp1, sp2, ep2 - sp2);
                        intersects = ((l11 == l21) && (l11 != l31) || (l12 == l22) && (l12 != l32));
                        if (((l11 == l21) && (l11 != l31) && (l12 == l22) && (l12 != l32)))
                        {
                        }
                        // d.h. auf der selben Seite der beiden Seiten und af der anderen Seite der Grundlinien
                    }
                }
                else if (notlinear1)
                {   // 2. Dreieck ist nur eine Linie
                    intersects =
                        Geometry.SegmentIntersection(sp1, ip1, sp2, ep2) ||
                        Geometry.SegmentIntersection(ip1, ep1, sp2, ep2);
                    if (!intersects)
                    {
                        bool l11 = Geometry.OnLeftSide(sp2, sp1, sd1); // links von einer Seite
                        bool l21 = Geometry.OnLeftSide(sp2, ep1, ed1); // links von der anderen Seite
                        bool l31 = Geometry.OnLeftSide(sp2, sp1, ep1 - sp1); // links von der Grundlinie
                        intersects = ((l11 == l21) && (l11 != l31));
                        // d.h. auf der selben Seite der beiden Seiten und af der anderen Seite der Grundlinien
                    }
                }
                else if (notlinear2)
                {   // 1. Dreieck ist nur eine Linie
                    intersects =
                        Geometry.SegmentIntersection(sp1, ep1, sp2, ip2) ||
                        Geometry.SegmentIntersection(sp1, ep1, ip2, ep2);
                    if (!intersects)
                    {
                        bool l12 = Geometry.OnLeftSide(sp1, sp2, sd2);
                        bool l22 = Geometry.OnLeftSide(sp1, ep2, ed2);
                        bool l32 = Geometry.OnLeftSide(sp1, sp2, ep2 - sp2);
                        intersects = ((l12 == l22) && (l12 != l32));
                        // d.h. auf der selben Seite der beiden Seiten und af der anderen Seite der Grundlinien
                    }
                }
                // else: beides nur Linien, das ist schon oben abgecheckt
            }
            if (intersects)
            {   // hier muss man mittig aufteilen und die beiden entstehenden Dreiecke zum Schnitt bringen
                if (Precision.IsEqual(sp1, ep1))
                {   // 1. Dreieck nicht mehr aufteilen
                    if (Precision.IsEqual(sp2, ep2))
                    {   // 2. Dreieck nicht mehr aufteilen
                        GeoPoint2DWithParameter res = new GeoPoint2DWithParameter();
                        res.p.x = (sp1.x + ep1.x + sp2.x + ep2.x) / 4.0;
                        res.p.y = (sp1.y + ep1.y + sp2.y + ep2.y) / 4.0;
                        res.par1 = (spar1 + epar1) / 2.0;
                        res.par2 = (spar2 + epar2) / 2.0;
                        return new GeoPoint2DWithParameter[] { res };
                    }
                    else
                    {   // nur 2. Dreieck aufteilen
                        List<GeoPoint2DWithParameter> list = new List<GeoPoint2DWithParameter>();
                        double ipar2 = (spar2 + epar2) / 2.0;
                        GeoPoint2D ip2 = (curve2 as ICurve2D).PointAt(ipar2);
                        GeoVector2D id2 = (curve2 as ICurve2D).DirectionAt(ipar2);
                        list.AddRange(CheckTriangleIntersect(curve1, sp1, ep1, spar1, epar1, sd1, ed1,
                            curve2, sp2, ip2, spar2, ipar2, sd2, id2));
                        if (list.Count < 2)
                        {
                            list.AddRange(CheckTriangleIntersect(curve1, sp1, ep1, spar1, epar1, sd1, ed1,
                                curve2, ip2, ep2, ipar2, epar2, id2, ed2));
                        }
                        return list.ToArray();
                    }
                }
                else
                {
                    double ipar1 = (spar1 + epar1) / 2.0;
                    GeoPoint2D ip1 = (curve1 as ICurve2D).PointAt(ipar1);
                    GeoVector2D id1 = (curve1 as ICurve2D).DirectionAt(ipar1);
                    if (Precision.IsEqual(sp2, ep2))
                    {   // 2. Dreieck nicht mehr aufteilen
                        List<GeoPoint2DWithParameter> list = new List<GeoPoint2DWithParameter>();
                        list.AddRange(CheckTriangleIntersect(curve1, sp1, ip1, spar1, ipar1, sd1, id1,
                            curve2, sp2, ep2, spar2, epar2, sd2, ed2));
                        if (list.Count < 2)
                        {
                            list.AddRange(CheckTriangleIntersect(curve1, ip1, ep1, ipar1, epar1, id1, ed1,
                                curve2, sp2, ep2, spar2, epar2, sd2, ed2));
                        }
                        return list.ToArray();
                    }
                    else
                    {   // beide aufteilen
                        List<GeoPoint2DWithParameter> list = new List<GeoPoint2DWithParameter>();
                        double ipar2 = (spar2 + epar2) / 2.0;
                        GeoPoint2D ip2 = (curve2 as ICurve2D).PointAt(ipar2);
                        GeoVector2D id2 = (curve2 as ICurve2D).DirectionAt(ipar2);
                        list.AddRange(CheckTriangleIntersect(curve1, sp1, ip1, spar1, ipar1, sd1, id1,
                            curve2, sp2, ip2, spar2, ipar2, sd2, id2));
                        if (list.Count < 2)
                        {   // es gibt maximal 2 Schnittpunkte in dieser Methode, da die Kurve keinen Wendepunkt
                            // enthält
                            list.AddRange(CheckTriangleIntersect(curve1, sp1, ip1, spar1, ipar1, sd1, id1,
                                curve2, ip2, ep2, ipar2, epar2, id2, ed2));
                        }
                        if (list.Count < 2)
                        {
                            list.AddRange(CheckTriangleIntersect(curve1, ip1, ep1, ipar1, epar1, id1, ed1,
                                curve2, sp2, ip2, spar2, ipar2, sd2, id2));
                        }
                        if (list.Count < 2)
                        {
                            list.AddRange(CheckTriangleIntersect(curve1, ip1, ep1, ipar1, epar1, id1, ed1,
                                curve2, ip2, ep2, ipar2, epar2, id2, ed2));
                        }
                        return list.ToArray();
                    }
                }
            }
            else
            {   // kein Schnitt
                return new GeoPoint2DWithParameter[0];
            }
        }
        /// <summary>
        /// Implements <see cref="CADability.Curve2D.ICurve2D.Intersect (GeoPoint2D, GeoPoint2D)"/>
        /// </summary>
        /// <param name="StartPoint"></param>
        /// <param name="EndPoint"></param>
        /// <returns></returns>
        public virtual GeoPoint2DWithParameter[] Intersect(GeoPoint2D StartPoint, GeoPoint2D EndPoint)
        {
            throw new NotImplementedException("GeneralCurve2D.Intersect");
        }
        /// <summary>
        /// Implements <see cref="CADability.Curve2D.ICurve2D.PerpendicularFoot (GeoPoint2D)"/>
        /// </summary>
        /// <param name="FromHere"></param>
        /// <returns></returns>
        public virtual GeoPoint2D[] PerpendicularFoot(GeoPoint2D FromHere)
        {
            return new GeoPoint2D[0];
            //			CndOCas.TIntersectionPoint2D[] isp = OCasBuddy.PerpendicularFoot(FromHere.gpPnt2d());
            //			GeoPoint2D[] res = new GeoPoint2D[isp.Length];
            //			for (int i=0; i<isp.Length; ++i)
            //			{
            //				CndOCas.TIntersectionPoint2D op = isp[i];
            //				// der Parameter in op wird hier nicht verwendet, obwohl er berechnet wurde
            //				res[i].x = op.x;
            //				res[i].y = op.y;
            //			}
            //			return res;
        }
        /// <summary>
        /// Implements <see cref="CADability.Curve2D.ICurve2D.TangentPoints (GeoPoint2D, GeoPoint2D)"/>
        /// </summary>
        /// <param name="FromHere"></param>
        /// <param name="CloseTo"></param>
        /// <returns></returns>
        public virtual GeoPoint2D[] TangentPoints(GeoPoint2D FromHere, GeoPoint2D CloseTo)
        {
            return new GeoPoint2D[] { };
        }
        /// <summary>
        /// Implements <see cref="CADability.Curve2D.ICurve2D.TangentPointsToAngle (Angle, GeoPoint2D)"/>
        /// </summary>
        /// <param name="ang"></param>
        /// <param name="CloseTo"></param>
        /// <returns></returns>
        public virtual GeoPoint2D[] TangentPointsToAngle(Angle ang, GeoPoint2D CloseTo)
        {
            return new GeoPoint2D[] { };
        }
        /// <summary>
        /// Implements <see cref="CADability.Curve2D.ICurve2D.TangentPointsToAngle (GeoVector2D)"/>
        /// </summary>
        /// <param name="direction"></param>
        /// <returns></returns>
        public virtual double[] TangentPointsToAngle(GeoVector2D direction)
        {   // noch überschreiben, hier die interpolierende Lösung
            int n = GetMinParts();
            List<double> res = new List<double>();
            double dn = 1.0 / (n - 1);
            for (int i = 0; i < n - 1; i++)
            {
                GeoVector2D d1 = (this as ICurve2D).DirectionAt(i * dn);
                GeoVector2D d2 = (this as ICurve2D).DirectionAt((i + 1) * dn);
                double dd1 = d1.x * direction.y - d1.y * direction.x;
                double dd2 = d2.x * direction.y - d2.y * direction.x;
                if ((dd1 < 0.0 && dd2 >= 0.0) || (dd2 < 0.0 && dd1 >= 0.0))
                {
                    double u1 = i * dn;
                    double u2 = (i + 1) * dn;
                    double u0 = (u1 + u2) / 2.0;
                    for (int j = 0; j < 30; ++j) // sture 30fache Bisection
                    {
                        GeoVector2D d0 = (this as ICurve2D).DirectionAt(u0);
                        double dd0 = d0.x * direction.y - d0.y * direction.x;
                        if ((dd0 < 0.0 && dd2 >= 0.0))
                        {
                            u1 = u0;
                        }
                        else
                        {
                            u2 = u0;
                        }
                    }
                    res.Add(u0);
                }
            }
            return res.ToArray();
        }
        /// <summary>
        /// Implements <see cref="CADability.Curve2D.ICurve2D.Reverse ()"/>
        /// </summary>
        public virtual void Reverse()
        {
            throw new NotImplementedException("GeneralCurve2D.reverse");
        }
        /// <summary>
        /// Implements <see cref="CADability.Curve2D.ICurve2D.Clone ()"/>
        /// </summary>
        /// <returns></returns>
        public abstract ICurve2D Clone();
        //{
        //    ICurve2D res = FromOcas(OCasBuddy);
        //    res.UserData.CloneFrom(this.UserData);
        //    return res;
        //}
        /// <summary>
        /// Implements <see cref="CADability.Curve2D.ICurve2D.CloneReverse (bool)"/>
        /// </summary>
        /// <param name="reverse"></param>
        /// <returns></returns>
        public virtual ICurve2D CloneReverse(bool reverse)
        {
            if (!reverse) return Clone();
            throw new NotImplementedException("GeneralCurve2D.CloneReverse");
        }
        /// <summary>
        /// Implements <see cref="CADability.Curve2D.ICurve2D.MakeGeoObject (Plane)"/>
        /// </summary>
        /// <param name="p"></param>
        /// <returns></returns>
        public virtual IGeoObject MakeGeoObject(Plane p)
        {
            throw new NotImplementedException();
        }
        /// <summary>
        /// Implements <see cref="CADability.Curve2D.ICurve2D.Project (Plane, Plane)"/>
        /// </summary>
        /// <param name="fromPlane"></param>
        /// <param name="toPlane"></param>
        /// <returns></returns>
        public virtual ICurve2D Project(Plane fromPlane, Plane toPlane)
        {
            return null; // TODO: immer überschreiben oder implementieren
        }
        /// <summary>
        /// Implements <see cref="CADability.Curve2D.ICurve2D.AddToGraphicsPath (System.Drawing.Drawing2D.GraphicsPath, bool)"/>
        /// </summary>
        /// <param name="path"></param>
        /// <param name="forward"></param>
        public virtual void AddToGraphicsPath(System.Drawing.Drawing2D.GraphicsPath path, bool forward)
        {
            throw new NotImplementedException("GeneralCurve2D.AddToGraphicsPath");
        }
        /// <summary>
        /// Implements <see cref="CADability.Curve2D.ICurve2D.IsParameterOnCurve (double)"/>
        /// </summary>
        /// <param name="par"></param>
        /// <returns></returns>
        public virtual bool IsParameterOnCurve(double par)
        {	// das geht davon aus, dass die Länge gleichmäßig auf der Kurve verteilt ist
            if (Length <= 0.0) return false;
            double d = Precision.eps / Length;
            return -d <= par && par <= 1.0 + d;
        }
        /// <summary>
        /// Implements <see cref="CADability.Curve2D.ICurve2D.IsValidParameter (double)"/>
        /// </summary>
        /// <param name="par"></param>
        /// <returns></returns>
        public virtual bool IsValidParameter(double par)
        {
            return true;
        }
        /// <summary>
        /// Implements <see cref="CADability.Curve2D.ICurve2D.GetExtendedHitTest ()"/>
        /// </summary>
        /// <returns></returns>
        public virtual IQuadTreeInsertable GetExtendedHitTest()
        {
            return this;
        }
        /// <summary>
        /// Implements <see cref="CADability.Curve2D.ICurve2D.GetSelfIntersections ()"/>
        /// </summary>
        /// <returns></returns>
        public virtual double[] GetSelfIntersections()
        {
            return new double[0];
        }
        /// <summary>
        /// Implements <see cref="CADability.Curve2D.ICurve2D.ReinterpretParameter (ref double)"/>
        /// </summary>
        /// <param name="p"></param>
        /// <returns></returns>
        public virtual bool ReinterpretParameter(ref double p)
        {
            return false;
        }
        /// <summary>
        /// Implements <see cref="CADability.Curve2D.ICurve2D.GetInflectionPoints ()"/>
        /// </summary>
        /// <returns></returns>
        public virtual double[] GetInflectionPoints()
        {
            //TempTriangulatedCurve2D ttc = new TempTriangulatedCurve2D(this);
            //return ttc.GetInflectionPoints();
            throw new ApplicationException("GetInflectionPoints should not be called");
        }
        private bool approxTwoArcs(double par1, double par2, out GeoPoint2D c1, out GeoPoint2D c2, out GeoPoint2D innerPoint)
        {	// gesucht sind zwei Kreisbögen, die an par1 bzw. par2 tangential zu dieser Kurve sind
            // und an einem inneren Punkt tangential ineinander übergehen. 
            // Es wird zuerst eine (willkürliche) innere Tangente bestimmt.
            // der innere Punkt innerPoint wird dann so bestimmt, dass p1, innerPoint und die beiden
            // Richtungen dir1 und innerTangent ein gleichschenkliges Dreieck bilden und ebenso
            // p2, dir2 und innerPoint, innerTangent. Mit dem gleichschenkligen Dreieck ist
            // gewährleistet, dass es jeweils einen Bogen gibt, der durch beide Eckpunkte geht
            // und dort tangential ist. 
            // Die bestimmung der inneren Tangente kann evtl. noch verbessert werden
            GeoVector2D dir1 = (this as ICurve2D).DirectionAt(par1);
            GeoVector2D dir2 = (this as ICurve2D).DirectionAt(par2);
            try
            {
                dir1.Norm();
                dir2.Norm();
                GeoVector2D innerTangent = dir1 + dir2; // die Richtung der Tangente im inneren Schnittpunkt
                if (!Precision.IsNullVector(innerTangent))
                {
                    innerTangent.Norm(); // di1, dir2 und innerTangent sind normiert
                    GeoPoint2D p1 = (this as ICurve2D).PointAt(par1);
                    GeoPoint2D p2 = (this as ICurve2D).PointAt(par2);
                    GeoVector2D sec1 = dir1 + innerTangent;
                    GeoVector2D sec2 = dir2 + innerTangent;
                    if (Geometry.IntersectLL(p1, sec1, p2, sec2, out innerPoint))
                    {
                        double pos = Geometry.LinePar(p1, p2, innerPoint);
                        if (pos > 0.0 && pos < 1.0)
                        {
                            if (Geometry.IntersectLL(p1, dir1.ToLeft(), innerPoint, innerTangent.ToLeft(), out c1))
                            {
                                if (Geometry.IntersectLL(p2, dir2.ToLeft(), innerPoint, innerTangent.ToLeft(), out c2))
                                {
                                    return true;
                                }
                            }
                        }
                    }
                }
            }
            catch (GeoVectorException)
            {
            }
            innerPoint = c1 = c2 = new GeoPoint2D(0.0, 0.0);
            return false;
        }
        internal virtual int GetMinParts()
        {	// blöder Name: von wievielen Teile soll bei der Approximation ausgegangen werden
            return 4;
        }
        private List<ICurve2D> approxLin(double par1, double par2, double maxError)
        {
            GeoPoint2D p1 = (this as ICurve2D).PointAt(par1);
            GeoPoint2D p2 = (this as ICurve2D).PointAt(par2);
            GeoPoint2D pi = new GeoPoint2D(p1, p2);
            GeoPoint2D ps = (this as ICurve2D).PointAt((par1 + par2) / 2.0);
            double dist = Geometry.DistPL(ps, p1, p2);
            double dbg = (ps | pi);
            // die Abfrage war (ps | pi) < maxError, d.h. die Kurve darf nicht beschleunigen oder bremsen
            // das führt bei manchen Splines zu sehr hohen aber unnötigen Auflösungen
            // jetzt wird überprüft, ob der innere Punkt nahe bei der Linie ist, nicht nahe am Mittelpunkt
            if (maxError < 0.0 || Math.Abs(dist) < maxError || (par2 - par1) < 1e-6) // Notbremse bei zu hoher Aufteilung
            {
                Line2D l = new Line2D(p1, p2);
                List<ICurve2D> al = new List<ICurve2D>(1);
                l.UserData.Add("CADability.Approximation.StartParameter", par1);
                l.UserData.Add("CADability.Approximation.EndParameter", par2);
                al.Add(l);
                return al;
            }
            else
            {
                List<ICurve2D> al = new List<ICurve2D>();
                double par = (par1 + par2) / 2.0;
                al.AddRange(approxLin(par1, par, maxError));
                al.AddRange(approxLin(par, par2, maxError));
                return al;
            }
        }
        private List<ICurve2D> approxArc(double par1, double par2, double maxError)
        {
            // maxError<0.0: nur eine einfache Aufteilung, nicht rekursiv
            double par = (par1 + par2) / 2.0;
            GeoPoint2D ps = (this as ICurve2D).PointAt(par);
            GeoPoint2D p1 = (this as ICurve2D).PointAt(par1);
            GeoPoint2D p2 = (this as ICurve2D).PointAt(par2);
            GeoVector2D dir1 = (this as ICurve2D).DirectionAt(par1);
            GeoVector2D dir2 = (this as ICurve2D).DirectionAt(par2);
            if (Geometry.DistPL(ps, p1, p2) < maxError)
            {   // überprüfen, ob eine einfache Linie genügt
                // eine Linie ist weniger als zwei Bögen und sollte deshalb bevorzugt werden
                if (Geometry.DistPL(ps, p1, dir1) < maxError)
                {
                    if (Geometry.DistPL(ps, p2, dir2) < maxError)
                    {
                        List<ICurve2D> al = new List<ICurve2D>(1);
                        Line2D l2d = new Line2D(p1, p2);
                        l2d.UserData.Add("CADability.Approximation.StartParameter", par1);
                        l2d.UserData.Add("CADability.Approximation.EndParameter", par2);
                        al.Add(l2d);
                        return al;
                    }
                }
            }
            GeoPoint2D c1, c2, innerPoint;
            bool ok = approxTwoArcs(par1, par2, out c1, out c2, out innerPoint);
            if (!ok) return approxLin(par1, par2, maxError);
            if (maxError < 0.0 || (innerPoint | ps) < maxError || (par2 - par1) < 1e-6) // letzeres ist Notbremse bei zu hoher Aufteilung
            {
                Arc2D a1 = new Arc2D(c1, Geometry.Dist(c1, innerPoint), p1, innerPoint, !Geometry.OnLeftSide(p1, c1, innerPoint - c1));
                Arc2D a2 = new Arc2D(c2, Geometry.Dist(c2, innerPoint), innerPoint, p2, Geometry.OnLeftSide(p2, c2, innerPoint - c2));
                BoundingRect ext = this.GetExtent();
                if (a1.Radius > ext.Size * 1e+3 || a2.Radius > ext.Size * 1e+3 || Math.Abs(a1.Sweep) < 1e-3 || Math.Abs(a2.Sweep) < 1e-3)
                {   // es sind sehr große oder sehr kurze Bögen entstanden, d.h. es handelt sich fast um eine Linie
                    return approxLin(par1, par2, maxError);
                }
                else
                {
                    List<ICurve2D> al = new List<ICurve2D>(2);
                    a1.UserData.Add("CADability.Approximation.StartParameter", par1);
                    a2.UserData.Add("CADability.Approximation.EndParameter", par2);
                    al.Add(a1);
                    al.Add(a2);
                    return al;
                }
            }
            else
            {
                List<ICurve2D> al = new List<ICurve2D>();
                al.AddRange(approxArc(par1, par, maxError));
                al.AddRange(approxArc(par, par2, maxError));
                return al;
            }
        }

        private Dictionary<double, ICurve2D> approximations; // ein Cache für Approximate
        /// <summary>
        /// Implements <see cref="CADability.Curve2D.ICurve2D.Approximate (bool, double)"/>
        /// </summary>
        /// <param name="linesOnly"></param>
        /// <param name="maxError"></param>
        /// <returns></returns>
        public virtual ICurve2D Approximate(bool linesOnly, double maxError)
        {
            if (!linesOnly && approximations == null)
            {
                approximations = new Dictionary<double, ICurve2D>();
            }
            if (!linesOnly && approximations.ContainsKey(maxError))
            {
                // zum Debuggen ausgeklammert return approximations[maxError]; // aus dem cache. Wird der auch gelöscht, wenn sich das Objekt ändert?
            }
            // zuerst brauchen wir eine Mindestaufteilung des Parameterbereiches, der durch
            // eine virtuelle Methode bestimmt werden muss
            int n;
            if (maxError < 0.0)
            {	// maxError<0.0 bedeutet: genau soviele Abschnitte machen
                n = (int)(-maxError);
                if (n == 0) n = 1;
            }
            else
            {
                n = GetMinParts();
                if (maxError == 0.0) maxError = Precision.eps * 10;
            }
            double d = 1.0 / n;
            List<ICurve2D> segments = new List<ICurve2D>();
            if (linesOnly)
            {
                for (int i = 0; i < n; ++i)
                {
                    segments.AddRange(approxLin(i * d, (i + 1) * d, maxError));
                }
            }
            else
            {
                for (int i = 0; i < n; ++i)
                {
                    segments.AddRange(approxArc(i * d, (i + 1) * d, maxError));
                }
            }
            for (int i = segments.Count - 1; i >= 0; --i)
            {
                if (segments[i].Length == 0.0) segments.RemoveAt(i);
            }
            // vielleicht haben wir so zu viele Punkte, hier wird zurückgerudert
            // durch zuviele anfängliche Stützpunkte werden es oftmals nur Linien, da eine Linie besser ist als zwei Bögen
            // in der folgenden Reduzierung werden manchmal aus 2 Linien zwei Bögen, die in weiteren Durchläufen weiter reduziert werden können
            bool reduced = true;
            while (reduced && maxError > 0.0)
            {
                reduced = false;
                for (int i = segments.Count - 1; i > 0; --i)
                {
                    double par1 = -1.0;
                    double par2 = -1.0;
                    int startind = -1;
                    if (segments[i] is Arc2D)
                    {
                        if (segments[i].UserData.ContainsData("CADability.Approximation.EndParameter"))
                        {
                            par2 = (double)segments[i].UserData.GetData("CADability.Approximation.EndParameter");
                            // jetzt kommt vor diesem Bogen einer mit StartParameter und davor entweder eine Linie oder ein Bogen
                            if (i - 2 >= 0 && segments[i - 2].UserData.ContainsData("CADability.Approximation.StartParameter"))
                            {
                                par1 = (double)segments[i - 2].UserData.GetData("CADability.Approximation.StartParameter");
                                startind = i - 2;
                            }
                            else if (i - 3 >= 0 && segments[i - 3].UserData.ContainsData("CADability.Approximation.StartParameter"))
                            {
                                par1 = (double)segments[i - 3].UserData.GetData("CADability.Approximation.StartParameter");
                                startind = i - 3;
                            }
                        }
                    }
                    else if (segments[i] is Line2D)
                    {
                        if (segments[i].UserData.ContainsData("CADability.Approximation.EndParameter"))
                        {
                            par2 = (double)segments[i].UserData.GetData("CADability.Approximation.EndParameter");
                            // jetzt kommt vor dieser Linie eine Linie mit StartParameter noch eins weiter zurück ein Bogen mit startparameter
                            if (i - 1 >= 0 && segments[i - 1].UserData.ContainsData("CADability.Approximation.StartParameter"))
                            {
                                par1 = (double)segments[i - 1].UserData.GetData("CADability.Approximation.StartParameter");
                                startind = i - 1;
                            }
                            else if (i - 2 >= 0 && segments[i - 2].UserData.ContainsData("CADability.Approximation.StartParameter"))
                            {
                                par1 = (double)segments[i - 2].UserData.GetData("CADability.Approximation.StartParameter");
                                startind = i - 2;
                            }
                        }
                    }
                    if (startind >= 0)
                    {
                        List<ICurve2D> l;
                        if (linesOnly) l = approxLin(par1, par2, maxError);
                        else l = approxArc(par1, par2, maxError);
                        if (l.Count == 1)
                        {
                            reduced = true;
                            segments[startind] = l[0];
                            for (int j = i; j > startind; --j)
                            {
                                segments.RemoveAt(j);
                            }
                            i = startind; // nächste Runde
                        }
                        else if (l.Count == 2 && !linesOnly && l[0] is Arc2D)
                        {   // zwei oder drei Kurven durch 2 Bögen ersetzen
                            // nur so kommen wir aus einer reinen Linienaufteilung wieder zu Bögen (nicht sehr effizient
                            reduced = true;
                            segments[startind] = l[0];
                            segments[startind + 1] = l[1];
                            for (int j = i; j > startind + 1; --j)
                            {
                                segments.RemoveAt(j);
                            }
                            i = startind; // nächste Runde
                        }
                    }
                }
            }
            Path2D res = new Path2D(segments.ToArray(), true);
            if (!linesOnly)
            {
                approximations[maxError] = res;
            }
            return res;
        }
        /// <summary>
        /// Implements <see cref="CADability.Curve2D.ICurve2D.Move (double, double)"/>
        /// </summary>
        /// <param name="x"></param>
        /// <param name="y"></param>
        public virtual void Move(double x, double y)
        {   // muss überschrieben werden
            throw new NotImplementedException("GeneralCurve2D.Move");
        }
        /// <summary>
        /// Implements <see cref="CADability.Curve2D.ICurve2D.GetModified (ModOp2D)"/>
        /// </summary>
        /// <param name="m"></param>
        /// <returns></returns>
        public virtual ICurve2D GetModified(ModOp2D m)
        {   // allgemeine Lösung, wird für Kreis und Ellipse übernommen
            ModOp m3D = ModOp.From2D(m);
            IGeoObject go = (this as ICurve2D).MakeGeoObject(Plane.XYPlane);
            go.Modify(m3D);
            ICurve cv = go as ICurve;
            return cv.GetProjectedCurve(Plane.XYPlane);
        }
        public virtual bool IsClosed
        {
            get
            {
                return Precision.IsEqual((this as ICurve2D).StartPoint, (this as ICurve2D).EndPoint);
            }
        }
        private UserData userData;
        public virtual UserData UserData
        {
            get
            {
                if (userData == null) userData = new UserData();
                return userData;
            }
        }
        /// <summary>
        /// Implements <see cref="CADability.Curve2D.ICurve2D.Copy (ICurve2D)"/>
        /// </summary>
        /// <param name="toCopyFrom"></param>
        public abstract void Copy(ICurve2D toCopyFrom);
        /// <summary>
        /// Implements <see cref="CADability.Curve2D.ICurve2D.GetFused (ICurve2D, double)"/>
        /// </summary>
        /// <param name="toFuseWith"></param>
        /// <param name="precision"></param>
        /// <returns></returns>
        public virtual ICurve2D GetFused(ICurve2D toFuseWith, double precision)
        {
            return null;
        }
        /// <summary>
        /// Implements <see cref="CADability.Curve2D.ICurve2D.TryPointDeriv2At (double, out GeoPoint2D, out GeoVector2D, out GeoVector2D)"/>
        /// </summary>
        /// <param name="position"></param>
        /// <param name="point"></param>
        /// <param name="deriv"></param>
        /// <param name="deriv2"></param>
        /// <returns></returns>
        public virtual bool TryPointDeriv2At(double position, out GeoPoint2D point, out GeoVector2D deriv, out GeoVector2D deriv2)
        {
            point = GeoPoint2D.Origin;
            deriv = deriv2 = GeoVector2D.NullVector;
            return false;
        }
        #endregion

        #region IQuadTreeInsertable Members

        public virtual BoundingRect GetExtent()
        {
            throw new NotImplementedException("GeneralCurve2D.GetExtent");
        }

        public virtual bool HitTest(ref BoundingRect Rect, bool IncludeControlPoints)
        {
            throw new NotImplementedException("GeneralCurve2D.HitTest");
        }

        public virtual object ReferencedObject
        {
            get
            {
                return this;
            }
        }
        #endregion

#if (DEBUG)
        public void Debug(string Title)
        {
            System.Diagnostics.Trace.WriteLine(Title + ": (GeneralCurve2D) (" + (this as ICurve2D).StartPoint.ToString() + ") --> (" + (this as ICurve2D).EndPoint.ToString() + ")");
        }
#endif

        GeoPoint2D[] I2DIntersectable.IntersectWith(I2DIntersectable other)
        {
            if (other is ICurve2D)
            {
                GeoPoint2DWithParameter[] ips = Intersect(other as ICurve2D);
                GeoPoint2D[] res = new GeoPoint2D[ips.Length];
                for (int i = 0; i < ips.Length; i++)
                {
                    res[i] = ips[i].p;
                    return res;
                }
            }
            return other.IntersectWith(this);
            // throw new NotImplementedException("I2DIntersectable.IntersectWith " + other.GetType().Name);
        }
#if DEBUG
        public GeoObjectList debug
        {
            get
            {
                return new GeoObjectList(this.MakeGeoObject(Plane.XYPlane));
            }
        }
#endif

        #region ICndHlp2DBuddy Members
        #endregion
    }

    /// <summary>
    /// Nur temporär, später soll alles von TriangulatedCurve2D abgeleitet sein
    /// </summary>
    //internal class TempTriangulatedCurve2D : TriangulatedCurve2D
    //{
    //    ICurve2D curve;
    //    public TempTriangulatedCurve2D(ICurve2D curve)
    //    {
    //        this.curve = curve;
    //        MakeTringulation();
    //    }
    //    protected override void GetTriangulationBasis(out GeoPoint2D[] points, out GeoVector2D[] directions, out double[] parameters)
    //    {
    //        if (curve is GeneralCurve2D)
    //        {
    //            (curve as GeneralCurve2D).GetTriangulationPoints(out points, out parameters);
    //            directions = new GeoVector2D[parameters.Length];
    //            for (int i = 0; i < parameters.Length; ++i)
    //            {
    //                directions[i] = curve.DirectionAt(parameters[i]);
    //            }
    //        }
    //        else if (curve is Path2D)
    //        {   // Overkill, aber so am schnellsten programmiert
    //            Path2D p2d = (curve as Path2D);
    //            Set<double> pars = new Set<double>();
    //            for (int i = 0; i < p2d.SubCurvesCount; i++)
    //            {
    //                TempTriangulatedCurve2D ttc = new TempTriangulatedCurve2D(p2d.SubCurves[i]);
    //                double[] spars;
    //                ttc.GetTriangulationBasis(out points, out directions, out spars);
    //                for (int j = 0; j < spars.Length; j++)
    //                {
    //                    pars.Add((i + spars[j]) / p2d.SubCurvesCount);
    //                }
    //            }
    //            parameters = pars.ToArray();
    //            Array.Sort(parameters);
    //            directions = new GeoVector2D[parameters.Length];
    //            points = new GeoPoint2D[parameters.Length];
    //            for (int i = 0; i < parameters.Length; ++i)
    //            {
    //                points[i] = curve.PointAt(parameters[i]);
    //                directions[i] = curve.DirectionAt(parameters[i]);
    //            }
    //        }
    //        else
    //        {
    //            parameters = new double[] { 0.0, 0.1, 0.2, 0.3, 0.4, 0.5, 0.6, 0.7, 0.8, 0.9, 1.0 };
    //            directions = new GeoVector2D[parameters.Length];
    //            points = new GeoPoint2D[parameters.Length];
    //            for (int i = 0; i < parameters.Length; ++i)
    //            {
    //                points[i] = curve.PointAt(parameters[i]);
    //                directions[i] = curve.DirectionAt(parameters[i]);
    //            }
    //        }
    //    }

    //    public override GeoVector2D DirectionAt(double Position)
    //    {
    //        return curve.DirectionAt(Position);
    //    }

    //    public override GeoPoint2D PointAt(double Position)
    //    {
    //        return curve.PointAt(Position);
    //    }
    //    public override void Reverse()
    //    {
    //        curve.Reverse();
    //        base.ClearTriangulation();
    //    }

    //    public override ICurve2D Clone()
    //    {
    //        return new TempTriangulatedCurve2D(curve);
    //    }

    //    public override void Copy(ICurve2D toCopyFrom)
    //    {
    //        TempTriangulatedCurve2D tc = toCopyFrom as TempTriangulatedCurve2D;
    //        if (tc != null)
    //        {
    //            curve = tc.curve;
    //        }
    //    }

    //    public override bool TryPointDeriv2At(double position, out GeoPoint2D point, out GeoVector2D deriv, out GeoVector2D deriv2)
    //    {
    //        return this.curve.TryPointDeriv2At(position, out point, out deriv, out deriv2);
    //    }
    //}

    [Serializable()]
    public abstract class GeneralCurve2D : ICurve2D, ISerializable
    {
        // This was called TriangulatedCurve2D in earlier versions
        private GeoPoint2D[] interpol; // gewisse Stützpunkte für jeden Knoten und ggf Zwischenpunkte (Wendepunkte, zu große Dreiecke)
        private GeoVector2D[] interdir; // Richtungen an den Stützpunkten
        private double[] interparam; // Parameterwerte an den Stützpunkten
        private GeoPoint2D[] tringulation; // Dreiecks-Zwischenpunkte (einer weniger als interpol)
        private ICurve2D baseApproximation;
        protected GeneralCurve2D()
        {
        }
        protected double sqr(double d) => d * d;
        protected virtual void GetTriangulationBasis(out GeoPoint2D[] points, out GeoVector2D[] directions, out double[] parameters)
        {
            GetTriangulationPoints(out points, out parameters);
            directions = new GeoVector2D[parameters.Length];
            for (int i = 0; i < parameters.Length; i++)
            {
                directions[i] = DirectionAt(parameters[i]).Normalized;
            }
        }
        internal virtual void GetTriangulationPoints(out GeoPoint2D[] interpol, out double[] interparam)
        {
            interparam = new double[] { 0, 0.25, 0.5, 0.75, 1.0 };
            interpol = new GeoPoint2D[interparam.Length];
            for (int i = 0; i < interparam.Length; i++)
            {
                interpol[i] = PointAt(interparam[i]);
            }
        }
        protected void MakeTriangulation()
        {   // ACHTUNG: Probleme sind hier Singularitäten und doppelte Punkte. Das muss noch überprüft werden
            // am Besten mit bösartigen BSplines (mehrfach identische Pole)
            GeoPoint2D[] points;
            GeoVector2D[] directions;
            double[] parameters;
            GetTriangulationBasis(out points, out directions, out parameters);
#if DEBUG
            DebuggerContainer dc1 = new DebuggerContainer();
            try
            {
                Polyline2D pl2d = new Polyline2D(points);
                dc1.Add(pl2d, System.Drawing.Color.Red, -1);
                for (int i = 0; i < directions.Length; ++i)
                {
                    Line2D l2d = new Line2D(points[i], points[i] + directions[i]);
                    dc1.Add(l2d, System.Drawing.Color.Blue, i);
                }
            }
            catch (Polyline2DException) { }
#endif
            // Bedingung: zwischen zwei Stützpunkten maximal ein Wendepunkt. Muss im Objekt
            // sichergestellt werden
            List<GeoPoint2D> linterpol = new List<GeoPoint2D>();
            List<GeoVector2D> linterdir = new List<GeoVector2D>();
            List<double> linterparam = new List<double>();
            List<GeoPoint2D> ltringulation = new List<GeoPoint2D>();
            linterpol.Add(points[0]);
            try
            {
                if (directions[0].IsNullVector()) linterdir.Add((points[1] - points[0]).Normalized);
                else linterdir.Add(directions[0].Normalized);
            }
            catch (GeoVectorException)
            {
                linterdir.Add(GeoVector2D.XAxis);
            }
            linterparam.Add(parameters[0]);
            if (points.Length > 1)
            {
                GeoVector2D d = points[1] - points[0];
                double z = d.x * directions[0].y - d.y * directions[0].x; // Z-Komponente des Kreuzprodukts
                for (int i = 1; i < points.Length; ++i)
                {
                    d = points[i] - points[i - 1];
                    z = d.x * directions[i - 1].y - d.y * directions[i - 1].x; // Z-Komponente des Kreuzprodukts
                    double z1 = d.x * directions[i].y - d.y * directions[i].x; // Z-Komponente des Kreuzprodukts
                    if (z * z1 > d.Length * 1e-3)
                    {   // hier könnte Wendepunkt sein, aber das funktioniert nicht gut
                        // ist halt kritisch bei fast geraden Kurven

                        double pi = FindInflectionPoint(points[i - 1], points[i], directions[i - 1], directions[i], parameters[i - 1], parameters[i]);
                        GeoPoint2D p0 = PointAt(pi);
                        linterpol.Add(p0);
                        linterparam.Add(pi);
                        GeoVector2D d0 = DirectionAt(pi);
                        linterdir.Add(d0);
                        GeoPoint2D ip;
                        if (!Geometry.IntersectLL(points[i - 1], directions[i - 1], p0, d0, out ip))
                            ip = new GeoPoint2D(points[i], points[i - 1]); // Mittelpunkt
                        ltringulation.Add(ip);

                        linterpol.Add(points[i]);
                        linterparam.Add(parameters[i]);
                        linterdir.Add(directions[i].Normalized);
                        if (!Geometry.IntersectLL(p0, d0, points[i], directions[i], out ip))
                            ip = new GeoPoint2D(points[i], points[i - 1]); // Mittelpunkt
                        ltringulation.Add(ip);
                    }
                    else
                    {   // gekapselt wg. ip
                        if (!directions[i].IsNullVector() || !d.IsNullVector())
                        {
                            GeoPoint2D ip;
                            if (!Geometry.IntersectLL(linterpol[linterpol.Count - 1], linterdir[linterdir.Count - 1], points[i], directions[i], out ip))
                                ip = new GeoPoint2D(points[i], points[i - 1]); // Mittelpunkt
                            ltringulation.Add(ip);
                            linterpol.Add(points[i]);
                            linterparam.Add(parameters[i]);
                            if (directions[i].IsNullVector()) linterdir.Add((points[i] - points[i - 1]).Normalized);
                            else linterdir.Add(directions[i].Normalized);
                        }
                    }
                    z = z1;
                }
            }
            interpol = linterpol.ToArray();
            interdir = linterdir.ToArray();
            interparam = linterparam.ToArray();
            tringulation = ltringulation.ToArray();
#if DEBUGx
            DebuggerContainer dc = new DebuggerContainer();
            Attribute.ColorDef red = new Attribute.ColorDef("red", System.Drawing.Color.Red);
            Attribute.ColorDef green = new Attribute.ColorDef("green", System.Drawing.Color.Green);
            Attribute.ColorDef blue = new Attribute.ColorDef("blue", System.Drawing.Color.Blue);
            for (int i = 1; i < interpol.Length; ++i)
            {
                Line2D l1 = new Line2D(interpol[i - 1], interpol[i]);
                dc.Add(l1, System.Drawing.Color.Red, i);
                Line2D l2 = new Line2D(interpol[i - 1], tringulation[i - 1]);
                dc.Add(l2, System.Drawing.Color.Green, i);
                Line2D l3 = new Line2D(tringulation[i - 1], interpol[i]);
                dc.Add(l3, System.Drawing.Color.Blue, i);
            }
            for (int i = 0; i < 100; ++i)
            {
                Line2D l1 = new Line2D(PointAt(i / 100.0), PointAt((i + 1) / 100.0));
                dc.Add(l1, System.Drawing.Color.Black, i);
            }
            if (baseApproximation == null) baseApproximation = Approximate(false, 0.0); // Annäherung mit Bögen unter Auswertung der Tangenten
            dc.Add(baseApproximation, System.Drawing.Color.Cyan, 0);
#endif
        }
        protected void ClearTriangulation()
        {
            interpol = null;
            interdir = null;
            interparam = null;
            tringulation = null;
            baseApproximation = null;
        }
        protected bool HasTriangulation()
        {
            return interpol != null;
        }

        virtual public void GetTriangulationPoints(out GeoPoint2D[] basisPoints, out GeoPoint2D[] vertices, out GeoVector2D[] directions, out double[] parameters)
        {
            if (interpol == null) MakeTriangulation();
            basisPoints = interpol.Clone() as GeoPoint2D[];
            directions = interdir.Clone() as GeoVector2D[];
            parameters = interparam.Clone() as double[];
            vertices = tringulation.Clone() as GeoPoint2D[];
        }
        private double FindInflectionPoint(GeoPoint2D sp, GeoPoint2D ep, GeoVector2D sdir, GeoVector2D edir, double spar, double epar)
        {
            GeoVector2D dir = ep - sp;
            double z0 = sdir.x * dir.y - sdir.y * dir.x;
            double z3 = edir.x * dir.y - edir.y * dir.x;
            bool up = (z0 + z3) < 0.0;
            // Maximum- bzw Minimumsuche  für l und r
            // geht nur mit 4 Punkten, nicht mit 3 wie bei der Bisektion
            // und deshalb etwas langsamer
            double par0 = spar;
            //double par1 = (spar + epar) / 3.0;
            //double par2 = 2.0 * (spar + epar) / 3.0;
            double par1 = spar + (epar - spar) / 3.0;
            double par2 = epar - (epar - spar) / 3.0;
            double par3 = epar;
            GeoVector2D dir0 = sdir;
            GeoVector2D dir1 = DirectionAt(par1);
            GeoVector2D dir2 = DirectionAt(par2);
            GeoVector2D dir3 = edir;
            double z1 = dir1.x * dir.y - dir1.y * dir.x;
            double z2 = dir2.x * dir.y - dir2.y * dir.x;
            while (par2 - par1 > 1e-7)
            {
                if ((up && z0 < z3) || (!up && z0 > z3))
                {   // z0 eliminieren
                    z0 = z1;
                    dir0 = dir1;
                    par0 = par1;
                }
                else
                {
                    z3 = z2;
                    dir3 = dir2;
                    par3 = par2;
                }
                par1 = par0 + (par3 - par0) / 3.0;
                par2 = par3 - (par3 - par0) / 3.0;
                //par1 = (par0 + par3) / 3.0;
                //par2 = 2.0 * (par0 + par3) / 3.0;
                dir1 = DirectionAt(par1);
                dir2 = DirectionAt(par2);
                z1 = dir1.x * dir.y - dir1.y * dir.x;
                z2 = dir2.x * dir.y - dir2.y * dir.x;
            }
            return (par1 + par2) / 2.0;
        }
#if DEBUG
        internal DebuggerContainer DebugTriangulation
        {
            get
            {
                if (interpol == null) MakeTriangulation();
                DebuggerContainer res = new DebuggerContainer();
                for (int i = 0; i < interpol.Length - 1; ++i)
                {
                    Line2D b = new Line2D(interpol[i + 1], interpol[i]);
                    res.Add(b, System.Drawing.Color.Blue, i);
                    Line2D s1 = new Line2D(interpol[i + 1], tringulation[i]);
                    res.Add(s1, System.Drawing.Color.Green, i);
                    Line2D s2 = new Line2D(interpol[i], tringulation[i]);
                    res.Add(s2, System.Drawing.Color.Green, i);
                }
                // res.Add(this);
                return res;
            }
        }
        public void DebugMakeTriangulation()
        {
            MakeTriangulation();
        }
        internal Polyline2D Debug100Points
        {
            get
            {
                if (interpol == null) MakeTriangulation();
                GeoPoint2D[] pnts = new GeoPoint2D[100];
                for (int i = 0; i < 100; i++) pnts[i] = PointAt(i / 99.0);
                return new Polyline2D(pnts);
            }
        }
        internal DebuggerContainer Debug100DirPoints
        {
            get
            {
                DebuggerContainer res = new DebuggerContainer();
                if (interpol == null) MakeTriangulation();
                GeoPoint2D[] pnts = new GeoPoint2D[100];
                for (int i = 0; i < 100; i++) pnts[i] = PointAt(i / 99.0);
                res.Add(new Polyline2D(pnts), System.Drawing.Color.Red, 0);
                for (int i = 0; i < 10; i++)
                {
                    GeoVector2D dir = DirectionAt(i / 9.0);
                    GeoPoint2D p = PointAt(i / 9.0);
                    res.Add(new Line2D(p, p + 0.1 * dir), System.Drawing.Color.Blue, i);
                }
                return res;
            }
        }
#endif
        #region ICurve2D Members

        public virtual GeoPoint2D StartPoint
        {
            get
            {
                if (interpol == null) MakeTriangulation();
                return interpol[0];
            }
            set
            {
                if (interpol == null) MakeTriangulation();
                interpol[0] = value; // für nur geringe Änderungen ist das wohl ok
            }
        }

        public virtual GeoPoint2D EndPoint
        {
            get
            {
                if (interpol == null) MakeTriangulation();
                return interpol[interpol.Length - 1];
            }
            set
            {
                if (interpol == null) MakeTriangulation();
                interpol[interpol.Length - 1] = value; // für nur geringe Änderungen ist das wohl ok
            }
        }

        public virtual GeoVector2D StartDirection
        {
            get
            {
                if (interpol == null) MakeTriangulation();
                return interdir[0];
            }
        }

        public virtual GeoVector2D EndDirection
        {
            get
            {
                if (interpol == null) MakeTriangulation();
                return interdir[interdir.Length - 1];
            }
        }

        public virtual GeoVector2D MiddleDirection
        {
            get
            {
                return DirectionAt(0.5);
            }
        }

        /// <summary>
        /// Implements <see cref="CADability.Curve2D.ICurve2D.DirectionAt (double)"/>
        /// </summary>
        /// <param name="Position"></param>
        /// <returns></returns>
        public abstract GeoVector2D DirectionAt(double Position);

        /// <summary>
        /// Implements <see cref="CADability.Curve2D.ICurve2D.PointAt (double)"/>
        /// </summary>
        /// <param name="Position"></param>
        /// <returns></returns>
        public abstract GeoPoint2D PointAt(double Position);

        /// <summary>
        /// Implements <see cref="CADability.Curve2D.ICurve2D.PositionOf (GeoPoint2D)"/>
        /// </summary>
        /// <param name="p"></param>
        /// <returns></returns>
        public virtual double PositionOf(GeoPoint2D p)
        {
            double res;
            if (interpol == null) MakeTriangulation();
            // find the best segment to start with
            int ind = -1;
            double bestdist = double.MaxValue;
            for (int i = 0; i < interpol.Length - 1; ++i)
            {
                double pos = Geometry.LinePar(interpol[i], interpol[i + 1], p);
                if (pos > -1e-8 && pos < 1 + 1e-8)
                {
                    double d = Math.Abs(Geometry.DistPL(p, interpol[i], interpol[i + 1]));
                    if (d < bestdist)
                    {
                        bestdist = d;
                        ind = i;
                    }
                }
            }
            if (this is IExplicitPCurve2D explicitP)
            {
                ExplicitPCurve2D explicitPCurve2D = explicitP.GetExplicitPCurve2D();
                if (explicitPCurve2D != null)
                {
                    if (ind >= 0)
                    {
                        double epos = explicitPCurve2D.PositionOf(p, out double dist, interparam[ind], interparam[ind + 1]);
                        if (dist < double.MaxValue) return explicitPCurve2D.NormaliseParameter(epos);
                    }
                    else
                    {
                        double epos = explicitPCurve2D.PositionOf(p, out double dist);
                        if (dist < double.MaxValue) return explicitPCurve2D.NormaliseParameter(epos);
                    }
                }
            }
            bool found = false;
            res = double.MinValue;
            if (ind >= 0)
            {
                found = FindPerpendicularFoot(p, interparam[ind], interparam[ind + 1], interpol[ind], interpol[ind + 1], interdir[ind].ToRight(), interdir[ind + 1].ToRight(), out res);
            }
            if (!found)
            {
                found = FindPerpendicularFoot(p, 0.0, 1.0, StartPoint, EndPoint, StartDirection.ToRight(), EndDirection.ToRight(), out res);
            }
            if (!found)
            {
                double ds = p | StartPoint;
                double de = p | EndPoint;
                if (ds < this.Length * 1e-4) return 0.0;
                if (de < this.Length * 1e-4) return 1.0;
                return double.MinValue;
            }
            return res;
        }
        /// <summary>
        /// Implements <see cref="CADability.Curve2D.ICurve2D.PositionAtLength (double)"/>
        /// </summary>
        /// <param name="position"></param>
        /// <returns></returns>
        public virtual double PositionAtLength(double position)
        {
            return position / Length;
        }
        public virtual bool TryPointDeriv2At(double position, out GeoPoint2D point, out GeoVector2D deriv1, out GeoVector2D deriv2)
        {
            point = PointAt(position);
            deriv1 = DirectionAt(position);
            deriv2 = GeoVector2D.NullVector;
            return false;
        }
        public virtual bool TryPointDeriv3At(double position, out GeoPoint2D point, out GeoVector2D deriv1, out GeoVector2D deriv2, out GeoVector2D deriv3)
        {
            point = PointAt(position);
            deriv1 = DirectionAt(position);
            deriv2 = GeoVector2D.NullVector;
            deriv3 = GeoVector2D.NullVector;
            return false;
        }
        public virtual double Length
        {
            get
            {
                if (baseApproximation == null) baseApproximation = Approximate(false, 0.0); // Annäherung mit Bögen unter Auswertung der Tangenten
                return baseApproximation.Length;
            }
        }

        /// <summary>
        /// Implements <see cref="CADability.Curve2D.ICurve2D.GetAreaFromPoint (GeoPoint2D)"/>
        /// </summary>
        /// <param name="p"></param>
        /// <returns></returns>
        public virtual double GetAreaFromPoint(GeoPoint2D p)
        {
            if (baseApproximation == null) baseApproximation = Approximate(false, 0.0); // Annäherung mit Bögen unter Auswertung der Tangenten
            return baseApproximation.GetAreaFromPoint(p);
        }

        /// <summary>
        /// Implements <see cref="CADability.Curve2D.ICurve2D.GetArea ()"/>
        /// </summary>
        /// <returns></returns>
        public virtual double GetArea()
        {
            if (baseApproximation == null) baseApproximation = Approximate(false, 0.0); // Annäherung mit Bögen unter Auswertung der Tangenten
            return baseApproximation.GetArea();
        }

        public virtual double Sweep
        {
            get
            {
                if (baseApproximation == null) baseApproximation = Approximate(false, 0.0); // Annäherung mit Bögen unter Auswertung der Tangenten
                return baseApproximation.Sweep;
            }
        }

        /// <summary>
        /// Implements <see cref="CADability.Curve2D.ICurve2D.Split (double)"/>
        /// </summary>
        /// <param name="Position"></param>
        /// <returns></returns>
        public virtual ICurve2D[] Split(double Position)
        {
            throw new Exception("The method or operation is not implemented.");
        }

        /// <summary>
        /// Implements <see cref="CADability.Curve2D.ICurve2D.Distance (GeoPoint2D)"/>
        /// </summary>
        /// <param name="p"></param>
        /// <returns></returns>
        public virtual double Distance(GeoPoint2D p)
        {
            double res = double.MaxValue;
            GeoPoint2D[] ft = PerpendicularFoot(p);
            for (int i = 0; i < ft.Length; ++i)
            {
                double d = ft[i] | p;
                if (d < res) res = d;
            }
            return res;
        }

        /// <summary>
        /// Implements <see cref="CADability.Curve2D.ICurve2D.MinDistance (ICurve2D)"/>
        /// </summary>
        /// <param name="Other"></param>
        /// <returns></returns>
        public virtual double MinDistance(ICurve2D Other)
        {   // we should use the triangulation here!
            return Curves2D.SimpleMinimumDistance(this, Other);
        }
        /// <summary>
        /// Implements <see cref="CADability.Curve2D.ICurve2D.MinDistance (GeoPoint2D)"/>
        /// </summary>
        /// <param name="p"></param>
        /// <returns></returns>
        public virtual double MinDistance(GeoPoint2D p)
        {
            double res = Math.Min(p | StartPoint, p | EndPoint);
            GeoPoint2D[] ft = PerpendicularFoot(p);
            for (int i = 0; i < ft.Length; ++i)
            {
                double pos = PositionOf(ft[i]); // we need to check, whether the foot point is inside the curve, not on it's extension
                if (pos >= 0 && pos <= 1)
                {
                    double d = ft[i] | p;
                    if (d < res) res = d;
                }
            }
            return res;
        }

        /// <summary>
        /// Implements <see cref="CADability.Curve2D.ICurve2D.Trim (double, double)"/>
        /// </summary>
        /// <param name="StartPos"></param>
        /// <param name="EndPos"></param>
        /// <returns></returns>
        public virtual ICurve2D Trim(double StartPos, double EndPos)
        {
            // this must be overridden!
            return this;
        }

        /// <summary>
        /// Implements <see cref="CADability.Curve2D.ICurve2D.Parallel (double, bool, double, double)"/>
        /// </summary>
        /// <param name="Dist"></param>
        /// <param name="approxSpline"></param>
        /// <param name="precision"></param>
        /// <param name="roundAngle"></param>
        /// <returns></returns>
        public virtual ICurve2D Parallel(double Dist, bool approxSpline, double precision, double roundAngle)
        {
            if (approxSpline)
            {
                return Approximate(true,precision).Parallel(Dist,false,precision,roundAngle);
            }
            throw new Exception("The method or operation is not implemented.");
        }
        private static GeoPoint2DWithParameter[] CheckTriangleIntersect(ICurve2D curve1, GeoPoint2D sp1, GeoPoint2D ep1, double spar1, double epar1, GeoVector2D sd1, GeoVector2D ed1,
            ICurve2D curve2, GeoPoint2D sp2, GeoPoint2D ep2, double spar2, double epar2, GeoVector2D sd2, GeoVector2D ed2)
        {
#if DEBUGCURVE
            string[] dbgstack = Environment.StackTrace.Split(new string[] { "CheckTriangleIntersect" }, StringSplitOptions.None);
            if (dbgstack.Length > 3)
            {
            }
#endif
            // tested die beiden gegebenen Dreiecke auf Schnitt.
            // Die Dreiecke hüllen jeweils ein Kurvenstück ein, d.h. wenn die Dreiecke sich schneiden 
            // oder eines im anderen liegt, dann kann es einen Schnittpunkt geben, sonst nicht.
            // NEU: mit Newtonverfahren, solange es konvergiert
            if (Precision.IsEqual(sp1, sp2))
            {
                GeoPoint2DWithParameter res = new GeoPoint2DWithParameter();
                res.p.x = (sp1.x + sp2.x) / 2.0;
                res.p.y = (sp1.y + sp2.y) / 2.0;
                res.par1 = spar1;
                res.par2 = spar2;
                return new GeoPoint2DWithParameter[] { res };
            }
            if (Precision.IsEqual(sp1, ep2))
            {
                GeoPoint2DWithParameter res = new GeoPoint2DWithParameter();
                res.p.x = (sp1.x + ep2.x) / 2.0;
                res.p.y = (sp1.y + ep2.y) / 2.0;
                res.par1 = spar1;
                res.par2 = epar2;
                return new GeoPoint2DWithParameter[] { res };
            }
            if (Precision.IsEqual(ep1, ep2))
            {
                GeoPoint2DWithParameter res = new GeoPoint2DWithParameter();
                res.p.x = (ep1.x + ep2.x) / 2.0;
                res.p.y = (ep1.y + ep2.y) / 2.0;
                res.par1 = epar1;
                res.par2 = epar2;
                return new GeoPoint2DWithParameter[] { res };
            }
            if (Precision.IsEqual(ep1, sp2))
            {
                GeoPoint2DWithParameter res = new GeoPoint2DWithParameter();
                res.p.x = (ep1.x + sp2.x) / 2.0;
                res.p.y = (ep1.y + sp2.y) / 2.0;
                res.par1 = epar1;
                res.par2 = spar2;
                return new GeoPoint2DWithParameter[] { res };
            }
            bool intersects = false;
            bool baseLineIntersection = false;
            baseLineIntersection = intersects = Geometry.SegmentIntersection(sp1, ep1, sp2, ep2); // Schnitt der Grundlinien
#if DEBUGCURVE
            DebuggerContainer dc = new DebuggerContainer();
            dc.Add(new Line2D(sp1, ep1), System.Drawing.Color.Red, 1);
            dc.Add(new Line2D(sp2, ep2), System.Drawing.Color.Green, 2);
#endif
            if (!intersects)
            {   // Dreieckspunkt berechnen
                // bool notlinear1 = Geometry.IntersectLL(sp1, sd1, ep1, ed1, out ip1);
                bool notlinear1 = !Precision.SameDirection(sd1, ed1, false);
                GeoPoint2D ip1 = GeoPoint2D.Origin;
                if (notlinear1)
                {
                    if (Geometry.IntersectLL(sp1, sd1, ep1, ed1, out ip1))
                    {
                        // ungünstiger Fall: fast linear und der Schnittpunkt liegt weit weg
                        GeoVector2D perp = (ep1 - sp1).ToLeft();
                        if (Geometry.OnLeftSide(ip1, sp1, perp) == Geometry.OnLeftSide(ip1, ep1, perp))
                        {   // der Schnittpunkt muss eigentlich immer in dem Band liegen, wleches durch die Linie
                            // aufgespannt wird (Senkrechte zu Anfangs und Endpunkt). Liegt er nicht darin
                            // kann es eine fast lineare Fortsetzung sein, oder ein nicht erkannter Wendepunkt.
                            // Jedenfalls kommen wir hier sonst aus dem Tritt
                            notlinear1 = false;
#if DEBUGCURVE
                            dc.Add(new Line2D(sp1, ip1), System.Drawing.Color.Blue, 3);
                            dc.Add(new Line2D(ep1, ip1), System.Drawing.Color.Blue, 4);
#endif
                        }
                        // if (((ip1 | sp1) > (ep1 | sp1) || (ip1 | ep1) > (ep1 | sp1)) && Math.Abs(GeoVector2D.Cos(sd1,ed1))>0.999) notlinear1 = false;
                    }
                    else notlinear1 = false;
                }
                bool notlinear2 = !Precision.SameDirection(sd2, ed2, false);
                // Geometry.IntersectLL(sp2, sd2, ep2, ed2, out ip2);
                GeoPoint2D ip2 = GeoPoint2D.Origin;
                if (notlinear2)
                {
                    if (Geometry.IntersectLL(sp2, sd2, ep2, ed2, out ip2))
                    {
                        //if (((ip2 | sp2) > (ep2 | sp2) || (ip2 | ep2) > (ep2 | sp2)) && Math.Abs(GeoVector2D.Cos(sd2,ed2))>0.999) notlinear1 = false;
                        GeoVector2D perp = (ep2 - sp2).ToLeft();
                        if (Geometry.OnLeftSide(ip2, sp2, perp) == Geometry.OnLeftSide(ip2, ep2, perp))
                        {   // der Schnittpunkt muss eigentlich immer in dem Band liegen, wleches durch die Linie
                            // aufgespannt wird (Senkrechte zu Anfangs und Endpunkt). Liegt er nicht darin
                            // kann es eine fast lineare Fortsetzung sein, oder ein nicht erkannter Wendepunkt.
                            // Jedenfalls kommen wir hier sonst aus dem Tritt
                            notlinear2 = false;
#if DEBUGCURVE
                            dc.Add(new Line2D(sp2, ip2), System.Drawing.Color.Violet, 5);
                            dc.Add(new Line2D(ep2, ip2), System.Drawing.Color.Violet, 6);
#endif
                        }
                    }
                    else notlinear2 = false;
                }
                if (notlinear1 && notlinear2)
                {   // beides echte Dreiecke
                    intersects =
                        Geometry.SegmentIntersection(sp1, ep1, sp2, ip2) ||
                        Geometry.SegmentIntersection(sp1, ep1, ip2, ep2) ||
                        Geometry.SegmentIntersection(sp1, ip1, sp2, ip2) ||
                        Geometry.SegmentIntersection(sp1, ip1, ip2, ep2) ||
                        Geometry.SegmentIntersection(sp1, ip1, sp2, ep2) ||
                        Geometry.SegmentIntersection(ip1, ep1, sp2, ip2) ||
                        Geometry.SegmentIntersection(ip1, ep1, ip2, ep2) ||
                        Geometry.SegmentIntersection(ip1, ep1, sp2, ep2);
                    if (!intersects)
                    {   // es könnte sein, dass das eine dreieck ganz im anderen liegt
                        // zuerst sp2 testen, das ist so gut wie irgend ein Punkt von Dreieck 2
                        // hier ist es wichtig, dass die Richtungen in Richtung der Kurve sind und die reihenfolge der Punkte stimmt
                        bool l11 = Geometry.OnLeftSide(sp2, sp1, ip1 - sp1); // links von einer Seite
                        bool l21 = Geometry.OnLeftSide(sp2, ep1, ep1 - ip1); // links von der anderen Seite
                        bool l31 = Geometry.OnLeftSide(sp2, sp1, ep1 - sp1); // links von der Grundlinie
                        bool l12 = Geometry.OnLeftSide(sp1, sp2, ip2 - sp2);
                        bool l22 = Geometry.OnLeftSide(sp1, ep2, ep2 - ip2);
                        bool l32 = Geometry.OnLeftSide(sp1, sp2, ep2 - sp2);
                        intersects = ((l11 == l21) && (l11 != l31) || (l12 == l22) && (l12 != l32));
                        // d.h. auf der selben Seite der beiden Seiten und auf der anderen Seite der Grundlinien
                    }
                }
                else if (notlinear1)
                {   // 2. Dreieck ist nur eine Linie
                    intersects =
                        Geometry.SegmentIntersection(sp1, ip1, sp2, ep2) ||
                        Geometry.SegmentIntersection(ip1, ep1, sp2, ep2);
                    if (!intersects)
                    {
                        bool l11 = Geometry.OnLeftSide(sp2, sp1, ip1 - sp1); // links von einer Seite (sd1 geht in die falsche Richtung, anders als erwartet)
                        bool l21 = Geometry.OnLeftSide(sp2, ep1, ep1 - ip1); // links von der anderen Seite
                        bool l31 = Geometry.OnLeftSide(sp2, sp1, ep1 - sp1); // links von der Grundlinie
                        intersects = ((l11 == l21) && (l11 != l31));
                        // d.h. auf der selben Seite der beiden Seiten und af der anderen Seite der Grundlinien
                    }
                }
                else if (notlinear2)
                {   // 1. Dreieck ist nur eine Linie
                    intersects =
                        Geometry.SegmentIntersection(sp1, ep1, sp2, ip2) ||
                        Geometry.SegmentIntersection(sp1, ep1, ip2, ep2);
                    if (!intersects)
                    {
                        bool l12 = Geometry.OnLeftSide(sp1, sp2, ip2 - sp2);
                        bool l22 = Geometry.OnLeftSide(sp1, ep2, ep2 - ip2);
                        bool l32 = Geometry.OnLeftSide(sp1, sp2, ep2 - sp2);
                        intersects = ((l12 == l22) && (l12 != l32));
                        // d.h. auf der selben Seite der beiden Seiten und auf der anderen Seite der Grundlinien
                    }
                }
                // else: beides nur Linien, das ist schon oben abgecheckt
            }
            if (intersects)
            {
                if (baseLineIntersection)
                {
                    GeoPoint2D ip;
                    Geometry.IntersectLL(sp1, ep1, sp2, ep2, out ip);
                    double par1 = spar1 + Geometry.LinePar(sp1, ep1, ip) * (epar1 - spar1);
                    double par2 = spar2 + Geometry.LinePar(sp2, ep2, ip) * (epar2 - spar2);
                    // bool dbg = BoxedSurfaceExtension.CurveIntersection2D(curve1, spar1, epar1, curve2, spar2, epar2, ref par1, ref par2, ref ip);
                    if (NewtonApproximation(curve1, spar1, epar1, curve2, spar2, epar2, ref par1, ref par2, ref ip))
                    {
                        GeoPoint2DWithParameter res = new GeoPoint2DWithParameter();
                        res.p = ip;
                        res.par1 = par1;
                        res.par2 = par2;
                        return new GeoPoint2DWithParameter[] { res };
                    }
                    else
                    {
#if DEBUGCURVE
#endif
                    }
                }

                // hier muss man mittig aufteilen und die beiden entstehenden Dreiecke zum Schnitt bringen
                if (Precision.IsEqual(sp1, ep1) || (epar1 - spar1) < 1e-6)
                {   // 1. Dreieck nicht mehr aufteilen
                    if (Precision.IsEqual(sp2, ep2) || (epar2 - spar2) < 1e-6)
                    {   // 2. Dreieck nicht mehr aufteilen
                        GeoPoint2DWithParameter res = new GeoPoint2DWithParameter();
                        res.p.x = (sp1.x + ep1.x + sp2.x + ep2.x) / 4.0;
                        res.p.y = (sp1.y + ep1.y + sp2.y + ep2.y) / 4.0;
                        res.par1 = (spar1 + epar1) / 2.0;
                        res.par2 = (spar2 + epar2) / 2.0;
                        return new GeoPoint2DWithParameter[] { res };
                    }
                    else
                    {   // nur 2. Dreieck aufteilen
                        List<GeoPoint2DWithParameter> list = new List<GeoPoint2DWithParameter>();
                        double ipar2 = (spar2 + epar2) / 2.0;
                        GeoPoint2D ip2 = curve2.PointAt(ipar2);
                        GeoVector2D id2 = curve2.DirectionAt(ipar2);
                        list.AddRange(CheckTriangleIntersect(curve1, sp1, ep1, spar1, epar1, sd1, ed1,
                            curve2, sp2, ip2, spar2, ipar2, sd2, id2));
                        if (list.Count < 2)
                        {
                            list.AddRange(CheckTriangleIntersect(curve1, sp1, ep1, spar1, epar1, sd1, ed1,
                                curve2, ip2, ep2, ipar2, epar2, id2, ed2));
                        }
                        return list.ToArray();
                    }
                }
                else
                {
                    double ipar1 = (spar1 + epar1) / 2.0;
                    GeoPoint2D ip1 = curve1.PointAt(ipar1);
                    GeoVector2D id1 = curve1.DirectionAt(ipar1);
                    if (Precision.IsEqual(sp2, ep2) || (epar2 - spar2) < 1e-6)
                    {   // 2. Dreieck nicht mehr aufteilen
                        List<GeoPoint2DWithParameter> list = new List<GeoPoint2DWithParameter>();
                        list.AddRange(CheckTriangleIntersect(curve1, sp1, ip1, spar1, ipar1, sd1, id1,
                            curve2, sp2, ep2, spar2, epar2, sd2, ed2));
                        if (list.Count < 2)
                        {
                            list.AddRange(CheckTriangleIntersect(curve1, ip1, ep1, ipar1, epar1, id1, ed1,
                                curve2, sp2, ep2, spar2, epar2, sd2, ed2));
                        }
                        return list.ToArray();
                    }
                    else
                    {   // beide aufteilen
                        List<GeoPoint2DWithParameter> list = new List<GeoPoint2DWithParameter>();
                        double ipar2 = (spar2 + epar2) / 2.0;
                        GeoPoint2D ip2 = curve2.PointAt(ipar2);
                        GeoVector2D id2 = curve2.DirectionAt(ipar2);
                        list.AddRange(CheckTriangleIntersect(curve1, sp1, ip1, spar1, ipar1, sd1, id1,
                            curve2, sp2, ip2, spar2, ipar2, sd2, id2));
                        if (list.Count < 2)
                        {   // es gibt maximal 2 Schnittpunkte in dieser Methode, da die Kurve keinen Wendepunkt
                            // enthält
                            list.AddRange(CheckTriangleIntersect(curve1, sp1, ip1, spar1, ipar1, sd1, id1,
                                curve2, ip2, ep2, ipar2, epar2, id2, ed2));
                        }
                        if (list.Count < 2)
                        {
                            list.AddRange(CheckTriangleIntersect(curve1, ip1, ep1, ipar1, epar1, id1, ed1,
                                curve2, sp2, ip2, spar2, ipar2, sd2, id2));
                        }
                        if (list.Count < 2)
                        {
                            list.AddRange(CheckTriangleIntersect(curve1, ip1, ep1, ipar1, epar1, id1, ed1,
                                curve2, ip2, ep2, ipar2, epar2, id2, ed2));
                        }
                        return list.ToArray();
                    }
                }
            }
            else
            {   // kein Schnitt
                return new GeoPoint2DWithParameter[0];
            }
        }

        private static bool NewtonApproximation(ICurve2D curve1, double spar1, double epar1, ICurve2D curve2, double spar2, double epar2, ref double par1, ref double par2, ref GeoPoint2D ip)
        {   // Finde den Schnittpunkt mit Newton
            // der Fehler muss immer kleiner werden und der Parameterbereich der beiden Kurven darf nicht verlassen werden.
            // Dann ist die Approximation sicher
#if DEBUGCURVE
            DebuggerContainer dc = new DebuggerContainer();
            dc.Add(curve1, System.Drawing.Color.Red, 1);
            dc.Add(curve2, System.Drawing.Color.Green, 2);
            dc.Add(curve1.PointAt(spar1), System.Drawing.Color.Blue, 3);
            dc.Add(curve1.PointAt(epar1), System.Drawing.Color.Blue, 4);
            dc.Add(curve2.PointAt(spar2), System.Drawing.Color.DarkGreen, 5);
            dc.Add(curve2.PointAt(epar2), System.Drawing.Color.DarkGreen, 6);
            dc.Add(curve1.PointAt(par1), System.Drawing.Color.Black, 7);
            dc.Add(curve2.PointAt(par2), System.Drawing.Color.Black, 8);
            dc.Add(new Line2D(curve1.PointAt(spar1), curve1.PointAt(spar1) + curve1.DirectionAt(spar1)), System.Drawing.Color.Chocolate, 9);
            dc.Add(new Line2D(curve2.PointAt(spar2), curve2.PointAt(spar2) + curve2.DirectionAt(spar2)), System.Drawing.Color.Chocolate, 10);
            dc.Add(new Line2D(curve1.PointAt(epar1), curve1.PointAt(epar1) + curve1.DirectionAt(epar1)), System.Drawing.Color.Chocolate, 11);
            dc.Add(new Line2D(curve2.PointAt(epar2), curve2.PointAt(epar2) + curve2.DirectionAt(epar2)), System.Drawing.Color.Chocolate, 12);
            dc.Add(new Line2D(curve1.PointAt(par1), curve1.PointAt(par1) + curve1.DirectionAt(par1)), System.Drawing.Color.Cyan, 13);
            dc.Add(new Line2D(curve2.PointAt(par2), curve2.PointAt(par2) + curve2.DirectionAt(par2)), System.Drawing.Color.Cyan, 14);
#endif
            GeoPoint2D p1 = curve1.PointAt(par1);
            GeoPoint2D p2 = curve2.PointAt(par2);
            double error = p1 | p2; // Anfangsfehler
            while (error > Precision.eps)
            {
                GeoVector2D dir1 = curve1.DirectionAt(par1);
                GeoVector2D dir2 = curve2.DirectionAt(par2);
                // TODO: die Länge der Ableitung ist wichtig für alle Kurven überprüfen
                double pos1, pos2;
                Geometry.IntersectLLpar(p1, dir1, p2, dir2, out pos1, out pos2);
                if (double.IsNaN(pos1) || double.IsNaN(pos2)) return false;
                par1 += pos1;
                par2 += pos2;
                if (par1 < spar1 || par1 > epar1 || par2 < spar2 || par2 > epar2) return false; // aus dem Bereich gelaufen
                p1 = curve1.PointAt(par1);
                p2 = curve2.PointAt(par2);
                double e = p1 | p2; // Fehler
                if (e >= error) return false;
                error = e;
            }
            ip = new GeoPoint2D(p1, p2); // die Mitte
            return true;
        }
        //        private static GeoPoint2DWithParameter[] CheckTriangleIntersect(ICurve2D curve1, GeoPoint2D sp1, GeoPoint2D ep1, double spar1, double epar1, GeoVector2D sd1, GeoVector2D ed1,
        //            ICurve2D curve2, GeoPoint2D sp2, GeoPoint2D ep2, double spar2, double epar2, GeoVector2D sd2, GeoVector2D ed2)
        //        {
        //            // tested die beiden gegebenen Dreiecke auf Schnitt.
        //            // Die dreiecke hüllen jeweils ein Kurvenstück ein, d.h. wenn die Dreiecke sich schneiden 
        //            // oder eines im anderen liegt, dann kann es einen Schnittpunkt geben, sonst nicht.
        //            // wir arbeiten hier mit sicherer Bisektion
        //            if (Precision.IsEqual(sp1, sp2))
        //            {
        //                GeoPoint2DWithParameter res = new GeoPoint2DWithParameter();
        //                res.p.x = (sp1.x + sp2.x) / 2.0;
        //                res.p.y = (sp1.y + sp2.y) / 2.0;
        //                res.par1 = spar1;
        //                res.par2 = spar2;
        //                return new GeoPoint2DWithParameter[] { res };
        //            }
        //            if (Precision.IsEqual(sp1, ep2))
        //            {
        //                GeoPoint2DWithParameter res = new GeoPoint2DWithParameter();
        //                res.p.x = (sp1.x + ep2.x) / 2.0;
        //                res.p.y = (sp1.y + ep2.y) / 2.0;
        //                res.par1 = spar1;
        //                res.par2 = epar2;
        //                return new GeoPoint2DWithParameter[] { res };
        //            }
        //            if (Precision.IsEqual(ep1, ep2))
        //            {
        //                GeoPoint2DWithParameter res = new GeoPoint2DWithParameter();
        //                res.p.x = (ep1.x + ep2.x) / 2.0;
        //                res.p.y = (ep1.y + ep2.y) / 2.0;
        //                res.par1 = epar1;
        //                res.par2 = epar2;
        //                return new GeoPoint2DWithParameter[] { res };
        //            }
        //            if (Precision.IsEqual(ep1, sp2))
        //            {
        //                GeoPoint2DWithParameter res = new GeoPoint2DWithParameter();
        //                res.p.x = (ep1.x + sp2.x) / 2.0;
        //                res.p.y = (ep1.y + sp2.y) / 2.0;
        //                res.par1 = epar1;
        //                res.par2 = spar2;
        //                return new GeoPoint2DWithParameter[] { res };
        //            }
        //            bool intersects = false;
        //            intersects = Geometry.SegmentIntersection(sp1, ep1, sp2, ep2); // Schnitt der Grundlinien
        //#if DEBUG
        //            DebuggerContainer dc = new DebuggerContainer();
        //            dc.Add(new Line2D(sp1, ep1), System.Drawing.Color.Red, 1);
        //            dc.Add(new Line2D(sp2, ep2), System.Drawing.Color.Green, 2);
        //#endif
        //            if (!intersects)
        //            {   // Dreieckspunkt berechnen
        //                // bool notlinear1 = Geometry.IntersectLL(sp1, sd1, ep1, ed1, out ip1);
        //                bool notlinear1 = !Precision.SameDirection(sd1, ed1, false);
        //                GeoPoint2D ip1 = GeoPoint2D.Origin;
        //                if (notlinear1)
        //                {
        //                    if (Geometry.IntersectLL(sp1, sd1, ep1, ed1, out ip1))
        //                    {
        //                        // ungünstiger Fall: fast linear und der Schnittpunkt liegt weit weg
        //                        if ((ip1 | sp1) > (ep1 | sp1) || (ip1 | ep1) > (ep1 | sp1)) notlinear1 = false;
        //#if DEBUG
        //                        dc.Add(new Line2D(sp1, ip1), System.Drawing.Color.Blue, 3);
        //                        dc.Add(new Line2D(ep1, ip1), System.Drawing.Color.Blue, 4);
        //#endif
        //                    }
        //                    else notlinear1 = false;
        //                }
        //                bool notlinear2 = !Precision.SameDirection(sd2, ed2, false);
        //                // Geometry.IntersectLL(sp2, sd2, ep2, ed2, out ip2);
        //                GeoPoint2D ip2 = GeoPoint2D.Origin;
        //                if (notlinear2)
        //                {
        //                    if (Geometry.IntersectLL(sp2, sd2, ep2, ed2, out ip2))
        //                    {
        //                        if ((ip2 | sp2) > (ep2 | sp2) || (ip2 | ep2) > (ep2 | sp2)) notlinear1 = false;
        //#if DEBUG
        //                        dc.Add(new Line2D(sp2, ip2), System.Drawing.Color.Violet, 5);
        //                        dc.Add(new Line2D(ep2, ip2), System.Drawing.Color.Violet, 6);
        //#endif
        //                    }
        //                    else notlinear2 = false;
        //                }
        //                if (notlinear1 && notlinear2)
        //                {   // beides echte Dreiecke
        //                    intersects =
        //                        Geometry.SegmentIntersection(sp1, ep1, sp2, ip2) ||
        //                        Geometry.SegmentIntersection(sp1, ep1, ip2, ep2) ||
        //                        Geometry.SegmentIntersection(sp1, ip1, sp2, ip2) ||
        //                        Geometry.SegmentIntersection(sp1, ip1, ip2, ep2) ||
        //                        Geometry.SegmentIntersection(sp1, ip1, sp2, ep2) ||
        //                        Geometry.SegmentIntersection(ip1, ep1, sp2, ip2) ||
        //                        Geometry.SegmentIntersection(ip1, ep1, ip2, ep2) ||
        //                        Geometry.SegmentIntersection(ip1, ep1, sp2, ep2);
        //                    if (!intersects)
        //                    {   // es könnte sein, dass das eine dreieck ganz im anderen liegt
        //                        // zuerst sp2 testen, das ist so gut wie irgend ein Punkt von Dreieck 2
        //                        // hier ist es wichtig, dass die Richtungen in Richtung der Kurve sind und die reihenfolge der Punkte stimmt
        //                        bool l11 = Geometry.OnLeftSide(sp2, sp1, sd1); // links von einer Seite
        //                        bool l21 = Geometry.OnLeftSide(sp2, ep1, ed1); // links von der anderen Seite
        //                        bool l31 = Geometry.OnLeftSide(sp2, sp1, ep1 - sp1); // links von der Grundlinie
        //                        bool l12 = Geometry.OnLeftSide(sp1, sp2, sd2);
        //                        bool l22 = Geometry.OnLeftSide(sp1, ep2, ed2);
        //                        bool l32 = Geometry.OnLeftSide(sp1, sp2, ep2 - sp2);
        //                        intersects = ((l11 == l21) && (l11 != l31) || (l12 == l22) && (l12 != l32));
        //                        if (((l11 == l21) && (l11 != l31) && (l12 == l22) && (l12 != l32)))
        //                        {
        //                        }
        //                        // d.h. auf der selben Seite der beiden Seiten und af der anderen Seite der Grundlinien
        //                    }
        //                }
        //                else if (notlinear1)
        //                {   // 2. Dreieck ist nur eine Linie
        //                    intersects =
        //                        Geometry.SegmentIntersection(sp1, ip1, sp2, ep2) ||
        //                        Geometry.SegmentIntersection(ip1, ep1, sp2, ep2);
        //                    if (!intersects)
        //                    {
        //                        bool l11 = Geometry.OnLeftSide(sp2, sp1, sd1); // links von einer Seite
        //                        bool l21 = Geometry.OnLeftSide(sp2, ep1, ed1); // links von der anderen Seite
        //                        bool l31 = Geometry.OnLeftSide(sp2, sp1, ep1 - sp1); // links von der Grundlinie
        //                        intersects = ((l11 == l21) && (l11 != l31));
        //                        // d.h. auf der selben Seite der beiden Seiten und af der anderen Seite der Grundlinien
        //                    }
        //                }
        //                else if (notlinear2)
        //                {   // 1. Dreieck ist nur eine Linie
        //                    intersects =
        //                        Geometry.SegmentIntersection(sp1, ep1, sp2, ip2) ||
        //                        Geometry.SegmentIntersection(sp1, ep1, ip2, ep2);
        //                    if (!intersects)
        //                    {
        //                        bool l12 = Geometry.OnLeftSide(sp1, sp2, sd2);
        //                        bool l22 = Geometry.OnLeftSide(sp1, ep2, ed2);
        //                        bool l32 = Geometry.OnLeftSide(sp1, sp2, ep2 - sp2);
        //                        intersects = ((l12 == l22) && (l12 != l32));
        //                        // d.h. auf der selben Seite der beiden Seiten und af der anderen Seite der Grundlinien
        //                    }
        //                }
        //                // else: beides nur Linien, das ist schon oben abgecheckt
        //            }
        //            if (intersects)
        //            {   // hier muss man mittig aufteilen und die beiden entstehenden Dreiecke zum Schnitt bringen
        //                if (Precision.IsEqual(sp1, ep1) || (epar1 - spar1) < 1e-6)
        //                {   // 1. Dreieck nicht mehr aufteilen
        //                    if (Precision.IsEqual(sp2, ep2) || (epar2 - spar2) < 1e-6)
        //                    {   // 2. Dreieck nicht mehr aufteilen
        //                        GeoPoint2DWithParameter res = new GeoPoint2DWithParameter();
        //                        res.p.x = (sp1.x + ep1.x + sp2.x + ep2.x) / 4.0;
        //                        res.p.y = (sp1.y + ep1.y + sp2.y + ep2.y) / 4.0;
        //                        res.par1 = (spar1 + epar1) / 2.0;
        //                        res.par2 = (spar2 + epar2) / 2.0;
        //                        return new GeoPoint2DWithParameter[] { res };
        //                    }
        //                    else
        //                    {   // nur 2. Dreieck aufteilen
        //                        List<GeoPoint2DWithParameter> list = new List<GeoPoint2DWithParameter>();
        //                        double ipar2 = (spar2 + epar2) / 2.0;
        //                        GeoPoint2D ip2 = curve2.PointAt(ipar2);
        //                        GeoVector2D id2 = curve2.DirectionAt(ipar2);
        //                        list.AddRange(CheckTriangleIntersect(curve1, sp1, ep1, spar1, epar1, sd1, ed1,
        //                            curve2, sp2, ip2, spar2, ipar2, sd2, id2));
        //                        if (list.Count < 2)
        //                        {
        //                            list.AddRange(CheckTriangleIntersect(curve1, sp1, ep1, spar1, epar1, sd1, ed1,
        //                                curve2, ip2, ep2, ipar2, epar2, id2, ed2));
        //                        }
        //                        return list.ToArray();
        //                    }
        //                }
        //                else
        //                {
        //                    double ipar1 = (spar1 + epar1) / 2.0;
        //                    GeoPoint2D ip1 = curve1.PointAt(ipar1);
        //                    GeoVector2D id1 = curve1.DirectionAt(ipar1);
        //                    if (Precision.IsEqual(sp2, ep2) || (epar2 - spar2) < 1e-6)
        //                    {   // 2. Dreieck nicht mehr aufteilen
        //                        List<GeoPoint2DWithParameter> list = new List<GeoPoint2DWithParameter>();
        //                        list.AddRange(CheckTriangleIntersect(curve1, sp1, ip1, spar1, ipar1, sd1, id1,
        //                            curve2, sp2, ep2, spar2, epar2, sd2, ed2));
        //                        if (list.Count < 2)
        //                        {
        //                            list.AddRange(CheckTriangleIntersect(curve1, ip1, ep1, ipar1, epar1, id1, ed1,
        //                                curve2, sp2, ep2, spar2, epar2, sd2, ed2));
        //                        }
        //                        return list.ToArray();
        //                    }
        //                    else
        //                    {   // beide aufteilen
        //                        List<GeoPoint2DWithParameter> list = new List<GeoPoint2DWithParameter>();
        //                        double ipar2 = (spar2 + epar2) / 2.0;
        //                        GeoPoint2D ip2 = curve2.PointAt(ipar2);
        //                        GeoVector2D id2 = curve2.DirectionAt(ipar2);
        //                        list.AddRange(CheckTriangleIntersect(curve1, sp1, ip1, spar1, ipar1, sd1, id1,
        //                            curve2, sp2, ip2, spar2, ipar2, sd2, id2));
        //                        if (list.Count < 2)
        //                        {   // es gibt maximal 2 Schnittpunkte in dieser Methode, da die Kurve keinen Wendepunkt
        //                            // enthält
        //                            list.AddRange(CheckTriangleIntersect(curve1, sp1, ip1, spar1, ipar1, sd1, id1,
        //                                curve2, ip2, ep2, ipar2, epar2, id2, ed2));
        //                        }
        //                        if (list.Count < 2)
        //                        {
        //                            list.AddRange(CheckTriangleIntersect(curve1, ip1, ep1, ipar1, epar1, id1, ed1,
        //                                curve2, sp2, ip2, spar2, ipar2, sd2, id2));
        //                        }
        //                        if (list.Count < 2)
        //                        {
        //                            list.AddRange(CheckTriangleIntersect(curve1, ip1, ep1, ipar1, epar1, id1, ed1,
        //                                curve2, ip2, ep2, ipar2, epar2, id2, ed2));
        //                        }
        //                        return list.ToArray();
        //                    }
        //                }
        //            }
        //            else
        //            {   // kein Schnitt
        //                return new GeoPoint2DWithParameter[0];
        //            }
        //        }
        /// <summary>
        /// Implements <see cref="CADability.Curve2D.ICurve2D.Intersect (ICurve2D)"/>
        /// </summary>
        /// <param name="IntersectWith"></param>
        /// <returns></returns>
        public virtual GeoPoint2DWithParameter[] Intersect(ICurve2D IntersectWith)
        {
            ICurve2D[] sc = null;
            if (IntersectWith is Path2D pth2d) sc = pth2d.SubCurves;
            else if (IntersectWith is Polyline2D pl2d) sc = pl2d.GetSubCurves();
            if (sc != null)
            {
                List<GeoPoint2DWithParameter> res = new List<GeoPoint2DWithParameter>();
                for (int i = 0; i < sc.Length; i++)
                {
                    GeoPoint2DWithParameter[] p2ds = Intersect(sc[i]);
                    for (int j = 0; j < p2ds.Length; j++)
                    {
                        p2ds[j].par2 = IntersectWith.PositionOf(p2ds[j].p);
                    }
                    res.AddRange(p2ds);
                }
                return res.ToArray();
            }
            else if (IntersectWith is GeneralCurve2D)
            {
                return Intersect(this, IntersectWith as GeneralCurve2D);
            }
            else
            {   // das soll eliminiert werden, indem alles von TriangulatedCurve2D abgeleitet wird.
                //return Intersect(this, new TempTriangulatedCurve2D(IntersectWith));
                throw new ApplicationException("must be a GeneralCurve2D");
            }
        }
        internal static GeoPoint2DWithParameter[] Intersect(GeneralCurve2D curve1, GeneralCurve2D curve2)
        {
            List<GeoPoint2DWithParameter> res = new List<GeoPoint2DWithParameter>();
            GeoPoint2D[] pts1, pts2;
            GeoVector2D[] dirs1, dirs2;
            double[] par1, par2;
            if (curve1.interpol == null) curve1.MakeTriangulation();
            pts1 = curve1.interpol;
            par1 = curve1.interparam;
            dirs1 = curve1.interdir;
            if (curve2.interpol == null) curve2.MakeTriangulation();
            pts2 = curve2.interpol;
            par2 = curve2.interparam;
            dirs2 = curve2.interdir;
#if DEBUG
            DebuggerContainer dc = new DebuggerContainer();
            // dc.Add(curve1);
            for (int i = 0; i < 100; ++i)
            {
                Line2D l2d = new Line2D(curve1.PointAt(i / 100.0), curve1.PointAt((i + 1) / 100.0));
                dc.Add(l2d, System.Drawing.Color.Green, i);
            }
            for (int i = 0; i < 100; ++i)
            {
                Line2D l2d = new Line2D(curve2.PointAt(i / 100.0), curve2.PointAt((i + 1) / 100.0));
                dc.Add(l2d, System.Drawing.Color.HotPink, i);
            }
            // dc.Add(curve2);
            for (int i = 0; i < pts1.Length - 1; ++i)
            {
                Line2D l2d = new Line2D(pts1[i], pts1[i + 1]);
                dc.Add(l2d, System.Drawing.Color.Red, i);
            }
            for (int i = 0; i < pts2.Length - 1; ++i)
            {
                Line2D l2d = new Line2D(pts2[i], pts2[i + 1]);
                dc.Add(l2d, System.Drawing.Color.Blue, i);
            }
#endif
            for (int i = 0; i < pts1.Length - 1; ++i)
            {
                for (int j = 0; j < pts2.Length - 1; ++j)
                {
                    GeoPoint2DWithParameter[] gpwp = CheckTriangleIntersect(curve1, pts1[i], pts1[i + 1], par1[i], par1[i + 1], dirs1[i], dirs1[i + 1],
                        curve2, pts2[j], pts2[j + 1], par2[j], par2[j + 1], dirs2[j], dirs2[j + 1]);
                    for (int k = 0; k < gpwp.Length; ++k)
                    {
                        if (curve1.IsValidParameter(gpwp[k].par1) && curve2.IsValidParameter(gpwp[k].par2))
                        {
                            res.Add(gpwp[k]);
                        }
                    }
                }
            }
            // ggf noch nachbessern wie in GeneralCurve2D

            return res.ToArray();
        }

        /// <summary>
        /// Implements <see cref="CADability.Curve2D.ICurve2D.Intersect (GeoPoint2D, GeoPoint2D)"/>
        /// </summary>
        /// <param name="StartPoint"></param>
        /// <param name="EndPoint"></param>
        /// <returns></returns>
        public virtual GeoPoint2DWithParameter[] Intersect(GeoPoint2D StartPoint, GeoPoint2D EndPoint)
        {
            return Intersect(new Line2D(StartPoint, EndPoint));
        }

        /// <summary>
        /// Implements <see cref="CADability.Curve2D.ICurve2D.PerpendicularFoot (GeoPoint2D)"/>
        /// </summary>
        /// <param name="fromHere"></param>
        /// <returns></returns>
        public virtual GeoPoint2D[] PerpendicularFoot(GeoPoint2D fromHere)
        {
            List<GeoPoint2D> res = new List<GeoPoint2D>();
            if (interpol == null) MakeTriangulation();
            for (int i = 0; i < interpol.Length - 1; ++i)
            {
                GeoVector2D snorm = interdir[i].ToRight();
                GeoVector2D enorm = interdir[i + 1].ToRight();
                bool l1 = Geometry.OnLeftSide(fromHere, interpol[i], snorm);
                bool l2 = Geometry.OnLeftSide(fromHere, interpol[i + 1], enorm);
                // tho point "fromHere" must be on different sides of the normals at the two endpoints of this segment
                // it means the normal on the curve wipes over the point "fromHere" when it moves along this segment
                if (l1 != l2)
                {
                    double par;
                    if (FindPerpendicularFoot(fromHere, interparam[i], interparam[i + 1], interpol[i], interpol[i + 1], snorm, enorm, out par))
                    {
                        res.Add(PointAt(par));
                    }
                }
            }
            return res.ToArray();
        }

        private bool FindPerpendicularFoot(GeoPoint2D fromHere, double spar, double epar, GeoPoint2D sp, GeoPoint2D ep, GeoVector2D snorm, GeoVector2D enorm, out double par)
        {
            double pos = Geometry.LinePar(sp, ep, Geometry.DropPL(fromHere, sp, ep));
            if (-1e-6 <= pos && pos <= 1.0 + 1e-6)
            {   // here we are close enough to try with newton
                par = (spar + epar) / 2.0;
                int iterations = 20;
                if (TryPointDeriv2At(par, out GeoPoint2D point, out GeoVector2D deriv1, out GeoVector2D deriv2))
                {
                    double stepTolerance = (epar - spar) * 1e-8;
                    double functionTolerance = (point | fromHere) * deriv1.Length * 1e-18;
                    par = Geometry.SimpleNewtonApproximation(delegate (double w, out double x, out double dx)
                    {
                        TryPointDeriv2At(w, out point, out deriv1, out deriv2);
                        x = (fromHere - point) * deriv1;
                        dx = (fromHere - point) * deriv2 - deriv1 * deriv1;

                    }, ref iterations, spar - stepTolerance, epar + stepTolerance, functionTolerance, stepTolerance);
                    if (par != double.MaxValue && Math.Abs((PointAt(par) - fromHere) * DirectionAt(par)) < Precision.eps) return true;
                }
                par = (spar + epar) / 2.0;
                if (NewtonPerpendicular(fromHere, ref par) && !double.IsNaN(par) && par != double.MaxValue)
                {
                    return true;
                }
            }
            double mpar = (spar + epar) / 2.0;
            par = mpar;
            bool l1 = Geometry.OnLeftSide(fromHere, sp, snorm);
            bool l2 = Geometry.OnLeftSide(fromHere, ep, enorm);
            GeoPoint2D mp = PointAt(mpar);
            GeoVector2D mnorm = DirectionAt(mpar).ToRight();
            bool lm = Geometry.OnLeftSide(fromHere, mp, mnorm);
            if (Math.Abs(Geometry.DistPL(fromHere, mp, mnorm)) < Precision.eps || (epar - spar) < 1e-6)
            {   // Abbruchbedingung mit Abstand von der Normalen ist bei starker Krümmung natürlich schwer zu erreichen
                // deshalb zusätzlich die Parametergrenze
                return true;
            }
            // die Normalen in den beiden Punkten müssen den Punkt auf verschiedenen Seiten haben, dann kann es einen Lotfußpunkt geben
            // geometrisch bedeutet das, dass die Normale auf der Kurve den Punkt überstreicht
            if (l1 != lm)
            {
                if (FindPerpendicularFoot(fromHere, spar, mpar, sp, mp, snorm, mnorm, out par))
                {
                    return true;
                }
            }
            else if (lm != l2)
            {
                if (FindPerpendicularFoot(fromHere, mpar, epar, mp, ep, mnorm, enorm, out par))
                {
                    return true;
                }
            }
            else
            {
                if ((fromHere | sp) < Precision.eps)
                {
                    par = 0.0;
                    return true;
                }
                if ((fromHere | ep) < Precision.eps)
                {
                    par = 1.0;
                    return true;
                }
            }
            return false;
        }

        private bool NewtonPerpendicular(GeoPoint2D p, ref double par)
        {   // funktioniert nicht gut, wenn zuweit entfernt
            double dp = double.MaxValue;
            int dbg = 0;
            do
            {
                ++dbg;
                GeoPoint2D s = PointAt(par);
                GeoVector2D v = DirectionAt(par);
                double d = (v.x * v.x + v.y * v.y);
                if (d == 0.0) return true; // singulärer Punkt
                double l = -((s.x - p.x) * v.x + (s.y - p.y) * v.y) / d;
                if (Math.Abs(l) > Math.Abs(dp / 2.0)) return false; // konvergiert nicht
                dp = l;
                par += dp;
#if DEBUGCURVE

                DebuggerContainer dc = new DebuggerContainer();
                dc.Add(this);
                Line2D l2d = new Line2D(s, s + v);
                dc.Add(l2d);
                dc.Add(p, System.Drawing.Color.Red, 0);
#endif
            } while (Math.Abs(dp) > 1e-6);
            return true;
        }
        private bool NewtonTangential(GeoVector2D dir, ref double par)
        {
            double dp = double.MaxValue;
            int dbg = 0;
            dir = dir.ToLeft().Normalized; // search for perpendicular
            do
            {
                ++dbg;
                GeoPoint2D s = PointAt(par);
                GeoVector2D v = DirectionAt(par);
                double d = (v.x * v.x + v.y * v.y);
                if (d == 0.0) return true; // null vector: singularity
                double l = (dir * v);
                if (Math.Abs(l) > Math.Abs(dp / 2.0)) return false; // konvergiert nicht
                dp = l;
                par += dp;
#if DEBUGCURVE

                DebuggerContainer dc = new DebuggerContainer();
                dc.Add(this);
                Line2D l2d = new Line2D(s, s + v);
                dc.Add(l2d);
                dc.Add(p, System.Drawing.Color.Red, 0);
#endif
            } while (Math.Abs(dp) > 1e-6);
            return true;
        }

        /// <summary>
        /// Implements <see cref="CADability.Curve2D.ICurve2D.TangentPoints (GeoPoint2D, GeoPoint2D)"/>
        /// </summary>
        /// <param name="FromHere"></param>
        /// <param name="CloseTo"></param>
        /// <returns></returns>
        public virtual GeoPoint2D[] TangentPoints(GeoPoint2D FromHere, GeoPoint2D CloseTo)
        {
            if (tringulation == null) MakeTriangulation();
            List<GeoPoint2D> res = new List<GeoPoint2D>();
            for (int i = 0; i < tringulation.Length; ++i)
            {
                if (Geometry.OnSameSide(interpol[i], interpol[i + 1], FromHere, tringulation[i]))
                {   // notwendige und hinreichende Bedingung für eine tangente. Für die Eckpunkte ggf kritisch
                    // erstmal als Bisektion implementiert. Man könnte auch einen Ellipsenbogen durch die beiden
                    // Punkte und Richtungen legen und die Position der Tangente nehmen...
                    double sparam = interparam[i];
                    double eparam = interparam[i + 1];
                    GeoVector2D sdir = interdir[i];
                    GeoVector2D edir = interdir[i + 1];
                    GeoPoint2D spoint = interpol[i];
                    GeoPoint2D epoint = interpol[i + 1];
                    GeoVector2D tdir = tringulation[i] - FromHere;
                    double sz = sdir.x * tdir.y - sdir.y * tdir.x;
                    double ez = edir.x * tdir.y - edir.y * tdir.x;
                    // sz und ez sind die ZKomponente des Vektorproduktes, d.h. der sin des Winkels
                    // zwischen der angenäherten tangente und der NURBS Kurve. Uns interessiert nur das Vorzeichen
                    GeoPoint2D mpoint;
                    do
                    {
                        double mparam = (sparam + eparam) / 2.0;
                        GeoVector2D mdir;
                        mpoint = PointAt(mparam);
                        mdir = DirectionAt(mparam);
                        tdir = mpoint - FromHere;
                        double mz = mdir.x * tdir.y - mdir.y * tdir.x;
                        if ((mz < 0 && sz < 0) || (mz >= 0 && sz >= 0))
                        {
                            sz = mz;
                            sparam = mparam;
                        }
                        else
                        {
                            ez = mz;
                            eparam = mparam;
                        }
                    }
                    while (Math.Abs(eparam - sparam) > 1e-8); // parameter space if from 0 to 1
                    res.Add(mpoint);
                }
            }
            res.Sort(new GeoPoint2D.CompareCloseTo(CloseTo));
            return res.ToArray();
        }

        /// <summary>
        /// Implements <see cref="CADability.Curve2D.ICurve2D.TangentPointsToAngle (Angle, GeoPoint2D)"/>
        /// </summary>
        /// <param name="ang"></param>
        /// <param name="CloseTo"></param>
        /// <returns></returns>
        public virtual GeoPoint2D[] TangentPointsToAngle(Angle ang, GeoPoint2D CloseTo)
        {
            GeoVector2D direction = new GeoVector2D(ang);
            List<GeoPoint2D> res = new List<GeoPoint2D>();
            for (int i = 0; i < tringulation.Length; ++i)
            {
                if (Geometry.OnSameSide(interpol[i], interpol[i + 1], tringulation[i], direction))
                {   // notwendige und hinreichende Bedingung für eine tangente. Für die Eckpunkte ggf kritisch
                    // erstmal als Bisektion implementiert. Man könnte auch einen Ellipsenbogen durch die beiden
                    // Punkte und Richtungen legen und die Position der Tangente nehmen...
                    double sparam = interparam[i];
                    double eparam = interparam[i + 1];
                    GeoVector2D sdir = interdir[i];
                    GeoVector2D edir = interdir[i + 1];
                    GeoPoint2D spoint = interpol[i];
                    GeoPoint2D epoint = interpol[i + 1];
                    GeoVector2D tdir = direction;
                    double sz = sdir.x * tdir.y - sdir.y * tdir.x;
                    double ez = edir.x * tdir.y - edir.y * tdir.x;
                    // sz und ez sind die ZKomponente des Vektorproduktes, d.h. der sin des Winkels
                    // zwischen der angenäherten tangente und der NURBS Kurve. Uns interessiert nur das Vorzeichen
                    GeoPoint2D mpoint;
                    do
                    {
                        double mparam = (sparam + eparam) / 2.0;
                        GeoVector2D mdir;
                        mpoint = PointAt(mparam);
                        mdir = DirectionAt(mparam);
                        double mz = mdir.x * tdir.y - mdir.y * tdir.x;
                        if ((mz < 0 && sz < 0) || (mz >= 0 && sz >= 0))
                        {
                            sz = mz;
                            sparam = mparam;
                        }
                        else
                        {
                            ez = mz;
                            eparam = mparam;
                        }
                    }
                    while (Math.Abs(eparam - sparam) > 1e-8);
                    res.Add(mpoint);
                }
            }
            res.Sort(new GeoPoint2D.CompareCloseTo(CloseTo));
            return res.ToArray();
        }

        /// <summary>
        /// Implements <see cref="CADability.Curve2D.ICurve2D.TangentPointsToAngle (GeoVector2D)"/>
        /// </summary>
        /// <param name="direction"></param>
        /// <returns></returns>
        public virtual double[] TangentPointsToAngle(GeoVector2D direction)
        {
            List<double> res = new List<double>();
            if (tringulation == null) MakeTriangulation();
            for (int i = 0; i < tringulation.Length; ++i)
            {
                if (Geometry.OnSameSide(interpol[i], interpol[i + 1], tringulation[i], direction))
                {   // notwendige und hinreichende Bedingung für eine tangente. Für die Eckpunkte ggf kritisch
                    // erstmal als Bisektion implementiert. Man könnte auch einen Ellipsenbogen durch die beiden
                    // Punkte und Richtungen legen und die Position der Tangente nehmen...
                    double sparam = interparam[i];
                    double eparam = interparam[i + 1];
                    GeoVector2D sdir = interdir[i];
                    GeoVector2D edir = interdir[i + 1];
                    GeoPoint2D spoint = interpol[i];
                    GeoPoint2D epoint = interpol[i + 1];
                    GeoVector2D tdir = direction;
                    double sz = sdir.x * tdir.y - sdir.y * tdir.x;
                    double ez = edir.x * tdir.y - edir.y * tdir.x;
                    // sz und ez sind die ZKomponente des Vektorproduktes, d.h. der sin des Winkels
                    // zwischen der angenäherten tangente und der NURBS Kurve. Uns interessiert nur das Vorzeichen
                    GeoPoint2D mpoint;
                    double mparam;
                    do
                    {
                        mparam = (sparam + eparam) / 2.0;
                        GeoVector2D mdir;
                        mpoint = PointAt(mparam);
                        mdir = DirectionAt(mparam);
                        double mz = mdir.x * tdir.y - mdir.y * tdir.x;
                        if ((mz < 0 && sz < 0) || (mz >= 0 && sz >= 0))
                        {
                            sz = mz;
                            sparam = mparam;
                        }
                        else
                        {
                            ez = mz;
                            eparam = mparam;
                        }
                    }
                    while (Math.Abs(eparam - sparam) > 1e-8);
                    res.Add(mparam);
                }
            }
            return res.ToArray();
        }
        /// <summary>
        /// Implements <see cref="CADability.Curve2D.ICurve2D.GetInflectionPoints ()"/>
        /// </summary>
        /// <returns></returns>
        public virtual double[] GetInflectionPoints()
        {
            List<double> res = new List<double>();
            if (interpol == null) MakeTriangulation();
            for (int i = 0; i < interpol.Length - 2; ++i)
            {
                if (Precision.IsPointOnLine(tringulation[i], interpol[i], interpol[i + 1])) continue;
                if (Precision.IsPointOnLine(tringulation[i + 1], interpol[i + 1], interpol[i + 2])) continue;
                if (Geometry.OnLeftSide(interpol[i], interpol[i + 1], tringulation[i]) != Geometry.OnLeftSide(interpol[i + 1], interpol[i + 2], tringulation[i + 1]))
                {   // das sind die Wendepunkte
                    res.Add(interparam[i + 1]);
                }
            }
            return res.ToArray();
        }

        /// <summary>
        /// Implements <see cref="CADability.Curve2D.ICurve2D.Reverse ()"/>
        /// </summary>
        public abstract void Reverse();

        /// <summary>
        /// Implements <see cref="CADability.Curve2D.ICurve2D.Clone ()"/>
        /// </summary>
        /// <returns></returns>
        public abstract ICurve2D Clone();

        /// <summary>
        /// Implements <see cref="CADability.Curve2D.ICurve2D.CloneReverse (bool)"/>
        /// </summary>
        /// <param name="reverse"></param>
        /// <returns></returns>
        public virtual ICurve2D CloneReverse(bool reverse)
        {
            ICurve2D res = this.Clone();
            if (reverse) res.Reverse();
            return res;
        }

        /// <summary>
        /// Implements <see cref="CADability.Curve2D.ICurve2D.Copy (ICurve2D)"/>
        /// </summary>
        /// <param name="toCopyFrom"></param>
        public abstract void Copy(ICurve2D toCopyFrom);

        /// <summary>
        /// Implements <see cref="CADability.Curve2D.ICurve2D.MakeGeoObject (Plane)"/>
        /// </summary>
        /// <param name="p"></param>
        /// <returns></returns>
        public virtual IGeoObject MakeGeoObject(Plane p)
        {
            BSpline2D bsp = ToBspline(0.0);
            if (bsp == null)
            {
                Line ln = Line.TwoPoints(p.ToGlobal(StartPoint), p.ToGlobal(EndPoint));
                return ln;
            }
            return bsp.MakeGeoObject(p);
        }

        protected BSpline2D ToBspline(double precision)
        {
            if (interpol == null) MakeTriangulation();
            double len = 0.0;
            for (int i = 1; i < interpol.Length; i++)
            {
                len += interpol[i] | interpol[i - 1];
            }
            if (precision == 0.0)
            {
                List<GeoPoint2D> pnts = new List<GeoPoint2D>();
                for (int i = 0; i < 100; i++)
                {
                    GeoPoint2D p = this.PointAt((double)i / (double)(100 - 1));
                    if (pnts.Count > 0)
                    {
                        if ((p | pnts[pnts.Count - 1]) > len / 100) pnts.Add(p);
                    }
                    else
                    {
                        pnts.Add(p);
                    }
                    if (i == 100 - 1) pnts[pnts.Count - 1] = p;
                }
                if (pnts.Count > 1) return new BSpline2D(pnts.ToArray(), 3, false);
                else return null;
            }
            else
            {
                return null;
            }
        }

        /// <summary>
        /// Implements <see cref="CADability.Curve2D.ICurve2D.Project (Plane, Plane)"/>
        /// </summary>
        /// <param name="fromPlane"></param>
        /// <param name="toPlane"></param>
        /// <returns></returns>
        public virtual ICurve2D Project(Plane fromPlane, Plane toPlane)
        {
            throw new Exception("The method or operation is not implemented.");
        }

        /// <summary>
        /// Implements <see cref="CADability.Curve2D.ICurve2D.AddToGraphicsPath (System.Drawing.Drawing2D.GraphicsPath, bool)"/>
        /// </summary>
        /// <param name="path"></param>
        /// <param name="forward"></param>
        public virtual void AddToGraphicsPath(System.Drawing.Drawing2D.GraphicsPath path, bool forward)
        {
            throw new Exception("The method or operation is not implemented.");
        }

        /// <summary>
        /// Implements <see cref="CADability.Curve2D.ICurve2D.IsParameterOnCurve (double)"/>
        /// </summary>
        /// <param name="par"></param>
        /// <returns></returns>
        public virtual bool IsParameterOnCurve(double par)
        {
            return par >= -1e-8 && par <= 1 + 1e-8;
        }

        /// <summary>
        /// Implements <see cref="CADability.Curve2D.ICurve2D.IsValidParameter (double)"/>
        /// </summary>
        /// <param name="par"></param>
        /// <returns></returns>
        public virtual bool IsValidParameter(double par)
        {
            return true;
        }

        /// <summary>
        /// Implements <see cref="CADability.Curve2D.ICurve2D.GetExtendedHitTest ()"/>
        /// </summary>
        /// <returns></returns>
        public virtual IQuadTreeInsertable GetExtendedHitTest()
        {
            throw new Exception("The method or operation is not implemented.");
        }

        /// <summary>
        /// Implements <see cref="CADability.Curve2D.ICurve2D.GetSelfIntersections ()"/>
        /// </summary>
        /// <returns></returns>
        public virtual double[] GetSelfIntersections()
        {
            //private GeoPoint2D[] interpol; // gewisse Stützpunkte für jeden Knoten und ggf Zwischenpunkte (Wendepunkte, zu große Dreiecke)
            //private GeoVector2D[] interdir; // Richtungen an den Stützpunkten
            //private double[] interparam; // Parameterwerte an den Stützpunkten
            //private GeoPoint2D[] tringulation; // Dreiecks-Zwischenpunkte (einer weniger als interpol)
            List<double> res = new List<double>();
            if (interpol == null) MakeTriangulation();
            for (int i = 0; i < interpol.Length - 2; ++i)
            {
                for (int j = i + 2; j < interpol.Length - 1; ++j)
                {
                    GeoPoint2DWithParameter[] gpwp = CheckTriangleIntersect(this, interpol[i], interpol[i + 1], interparam[i], interparam[i + 1], interdir[i], interdir[i + 1],
                        this, interpol[j], interpol[j + 1], interparam[j], interparam[j + 1], interdir[j], interdir[j + 1]);
                    for (int k = 0; k < gpwp.Length; ++k)
                    {
                        if (IsValidParameter(gpwp[k].par1) && IsValidParameter(gpwp[k].par2))
                        {
                            res.Add(gpwp[k].par1);
                            res.Add(gpwp[k].par2);
                        }
                    }
                }
            }
            return res.ToArray();
        }

        /// <summary>
        /// Implements <see cref="CADability.Curve2D.ICurve2D.ReinterpretParameter (ref double)"/>
        /// </summary>
        /// <param name="p"></param>
        /// <returns></returns>
        public virtual bool ReinterpretParameter(ref double p)
        {
            return false;
            //throw new Exception("The method or operation is not implemented.");
        }

        /// <summary>
        /// Implements <see cref="CADability.Curve2D.ICurve2D.Approximate (bool, double)"/>
        /// </summary>
        /// <param name="linesOnly"></param>
        /// <param name="maxError"></param>
        /// <returns></returns>
        public virtual ICurve2D Approximate(bool linesOnly, double maxError)
        {
            if (interpol == null) MakeTriangulation();
            if (maxError == 0.0)
            {   // nur gemäß Stützpunkten
                if (linesOnly)
                {
                    return new Polyline2D(interpol);
                }
                else
                {
                    List<ICurve2D> curves = new List<ICurve2D>();
                    for (int i = 0; i < interpol.Length - 1; ++i)
                    {
                        ICurve2D[] arcs = Curves2D.ConnectByTwoArcs(interpol[i], interpol[i + 1], interdir[i], interdir[i + 1]);
#if DEBUG
                        if (arcs.Length == 2)
                        {
                            if ((arcs[0] as Arc2D).Radius > 10000 || (arcs[1] as Arc2D).Radius > 10000)
                            {

                            }
                        }
#endif
                        curves.AddRange(arcs);
                    }
                    for (int i = 1; i < curves.Count; ++i)
                    {
                        curves[i].StartPoint = curves[i - 1].EndPoint;
                    }
                    return new Path2D(curves.ToArray(), true);
                }
            }
            else
            {
                List<ICurve2D> segments = new List<ICurve2D>();
                if (linesOnly)
                {
                    for (int i = 0; i < interpol.Length - 1; ++i)
                    {
                        segments.AddRange(approxLin(interparam[i], interparam[i + 1], maxError));
                    }
                }
                else
                {
                    for (int i = 0; i < interpol.Length - 1; ++i)
                    {
                        segments.AddRange(approxArc(interparam[i], interparam[i + 1], maxError));
                    }
                }
                for (int i = segments.Count - 1; i >= 0; --i)
                {
                    if (segments[i].Length == 0.0) segments.RemoveAt(i);
                }
                return new Path2D(segments.ToArray(), true);
            }
        }
        private bool approxTwoArcs(double par1, double par2, out GeoPoint2D c1, out GeoPoint2D c2, out GeoPoint2D innerPoint)
        {	// gesucht sind zwei Kreisbögen, die an par1 bzw. par2 tangential zu dieser Kurve sind
            // und an einem inneren Punkt tangential ineinander übergehen. 
            // Es wird zuerst eine (willkürliche) innere Tangente bestimmt.
            // der innere Punkt innerPoint wird dann so bestimmt, dass p1, innerPoint und die beiden
            // Richtungen dir1 und innerTangent ein gleichschenkliges Dreieck bilden und ebenso
            // p2, dir2 und innerPoint, innerTangent. Mit dem gleichschenkligen Dreieck ist
            // gewährleistet, dass es jeweils einen Bogen gibt, der durch beide Eckpunkte geht
            // und dort tangential ist. 
            // Die bestimmung der inneren Tangente kann evtl. noch verbessert werden
            GeoVector2D dir1 = (this as ICurve2D).DirectionAt(par1);
            GeoVector2D dir2 = (this as ICurve2D).DirectionAt(par2);
            try
            {
                dir1.Norm();
                dir2.Norm();
                GeoVector2D innerTangent = dir1 + dir2; // die Richtung der Tangente im inneren Schnittpunkt
                if (!Precision.IsNullVector(innerTangent))
                {
                    innerTangent.Norm(); // di1, dir2 und innerTangent sind normiert
                    GeoPoint2D p1 = (this as ICurve2D).PointAt(par1);
                    GeoPoint2D p2 = (this as ICurve2D).PointAt(par2);
                    GeoVector2D sec1 = dir1 + innerTangent;
                    GeoVector2D sec2 = dir2 + innerTangent;
                    if (Geometry.IntersectLL(p1, sec1, p2, sec2, out innerPoint))
                    {
                        double pos = Geometry.LinePar(p1, p2, innerPoint);
                        if (pos > 0.0 && pos < 1.0)
                        {
                            if (Geometry.IntersectLL(p1, dir1.ToLeft(), innerPoint, innerTangent.ToLeft(), out c1))
                            {
                                if (Geometry.IntersectLL(p2, dir2.ToLeft(), innerPoint, innerTangent.ToLeft(), out c2))
                                {
                                    return true;
                                }
                            }
                        }
                    }
                }
            }
            catch (GeoVectorException)
            {
            }
            innerPoint = c1 = c2 = new GeoPoint2D(0.0, 0.0);
            return false;
        }
        private List<ICurve2D> approxLin(double par1, double par2, double maxError)
        {
            GeoPoint2D p1 = (this as ICurve2D).PointAt(par1);
            GeoPoint2D p2 = (this as ICurve2D).PointAt(par2);
            GeoPoint2D pi = new GeoPoint2D(p1, p2);
            GeoPoint2D ps = (this as ICurve2D).PointAt((par1 + par2) / 2.0);
            if (maxError < 0.0 || (ps | pi) < maxError || (par2 - par1) < 1e-6) // Notbremse bei zu hoher Aufteilung
            {
                Line2D l = new Line2D(p1, p2);
                List<ICurve2D> al = new List<ICurve2D>(1);
                l.UserData.Add("CADability.Approximation.StartParameter", par1);
                l.UserData.Add("CADability.Approximation.EndParameter", par2);
                al.Add(l);
                return al;
            }
            else
            {
                List<ICurve2D> al = new List<ICurve2D>();
                double par = (par1 + par2) / 2.0;
                al.AddRange(approxLin(par1, par, maxError));
                al.AddRange(approxLin(par, par2, maxError));
                return al;
            }
        }
        private List<ICurve2D> approxArc(double par1, double par2, double maxError)
        {
            // maxError<0.0: nur eine einfache Aufteilung, nicht rekursiv
            double par = (par1 + par2) / 2.0;
            GeoPoint2D ps = (this as ICurve2D).PointAt(par);
            GeoPoint2D p1 = (this as ICurve2D).PointAt(par1);
            GeoPoint2D p2 = (this as ICurve2D).PointAt(par2);
            GeoVector2D dir1 = (this as ICurve2D).DirectionAt(par1);
            GeoVector2D dir2 = (this as ICurve2D).DirectionAt(par2);
            if (Geometry.DistPL(ps, p1, p2) < maxError)
            {   // überprüfen, ob eine einfache Linie genügt
                // eine Linie ist weniger als zwei Bögen und sollte deshalb bevorzugt werden
                if (Geometry.DistPL(ps, p1, dir1) < maxError)
                {
                    if (Geometry.DistPL(ps, p2, dir2) < maxError)
                    {
                        List<ICurve2D> al = new List<ICurve2D>(1);
                        Line2D l2d = new Line2D(p1, p2);
                        l2d.UserData.Add("CADability.Approximation.StartParameter", par1);
                        l2d.UserData.Add("CADability.Approximation.EndParameter", par2);
                        al.Add(l2d);
                        return al;
                    }
                }
            }
            GeoPoint2D c1, c2, innerPoint;
            bool ok = approxTwoArcs(par1, par2, out c1, out c2, out innerPoint);
            if (!ok) return approxLin(par1, par2, maxError);
            if (maxError < 0.0 || (innerPoint | ps) < maxError || (par2 - par1) < 1e-6) // letzeres ist Notbremse bei zu hoher Aufteilung
            {
                Arc2D a1 = new Arc2D(c1, Geometry.Dist(c1, innerPoint), p1, innerPoint, !Geometry.OnLeftSide(p1, c1, innerPoint - c1));
                Arc2D a2 = new Arc2D(c2, Geometry.Dist(c2, innerPoint), innerPoint, p2, Geometry.OnLeftSide(p2, c2, innerPoint - c2));
                BoundingRect ext = this.GetExtent();
                if (a1.Radius > ext.Size * 1e+3 || a2.Radius > ext.Size * 1e+3 || Math.Abs(a1.Sweep) < 1e-3 || Math.Abs(a2.Sweep) < 1e-3)
                {   // es sind sehr große oder sehr kurze Bögen entstanden, d.h. es handelt sich fast um eine Linie
                    return approxLin(par1, par2, maxError);
                }
                else
                {
                    List<ICurve2D> al = new List<ICurve2D>(2);
                    a1.UserData.Add("CADability.Approximation.StartParameter", par1);
                    a2.UserData.Add("CADability.Approximation.EndParameter", par2);
                    al.Add(a1);
                    al.Add(a2);
                    return al;
                }
            }
            else
            {
                List<ICurve2D> al = new List<ICurve2D>();
                al.AddRange(approxArc(par1, par, maxError));
                al.AddRange(approxArc(par, par2, maxError));
                return al;
            }
        }

        /// <summary>
        /// Implements <see cref="CADability.Curve2D.ICurve2D.Move (double, double)"/>
        /// </summary>
        /// <param name="x"></param>
        /// <param name="y"></param>
        public virtual void Move(double x, double y)
        {
            throw new ApplicationException("The method or operation is not implemented.");
        }

        public virtual bool IsClosed
        {
            get
            {
                return Precision.IsEqual(this.StartPoint, this.EndPoint);
            }
        }

        /// <summary>
        /// Implements <see cref="CADability.Curve2D.ICurve2D.GetModified (ModOp2D)"/>
        /// </summary>
        /// <param name="m"></param>
        /// <returns></returns>
        public virtual ICurve2D GetModified(ModOp2D m)
        {
            if (baseApproximation == null) baseApproximation = Approximate(false, 0.0); // Annäherung mit Bögen unter Auswertung der Tangenten
            return baseApproximation.GetModified(m);
            //TriangulatedCurve2D res = this.Clone() as TriangulatedCurve2D;
            //res.baseApproximation = baseApproximation.GetModified(m);
            //    res.interpol = new GeoPoint2D[interpol.Length];
            //    for (int i = 0; i < interpol.Length; i++)
            //    {

            //    }

            //private GeoPoint2D[] interpol; // gewisse Stützpunkte für jeden Knoten und ggf Zwischenpunkte (Wendepunkte, zu große Dreiecke)
            //private GeoVector2D[] interdir; // Richtungen an den Stützpunkten
            //private double[] interparam; // Parameterwerte an den Stützpunkten
            //private GeoPoint2D[] tringulation; // Dreiecks-Zwischenpunkte (einer weniger als interpol)

            //    throw new Exception("The method or operation is not implemented.");
        }

        private UserData userData;
        public virtual UserData UserData
        {
            get
            {
                if (userData == null) userData = new UserData();
                return userData;
            }
        }

        /// <summary>
        /// Implements <see cref="CADability.Curve2D.ICurve2D.GetFused (ICurve2D, double)"/>
        /// </summary>
        /// <param name="toFuseWith"></param>
        /// <param name="precision"></param>
        /// <returns></returns>
        public virtual ICurve2D GetFused(ICurve2D toFuseWith, double precision)
        {
            return null; // should be implemented
            // throw new Exception("The method or operation is not implemented.");
        }

        #endregion

        #region IQuadTreeInsertable Members

        public virtual BoundingRect GetExtent()
        {
            if (interpol == null) MakeTriangulation();
            BoundingRect ext = BoundingRect.EmptyBoundingRect;
            for (int i = 0; i < interpol.Length; ++i)
            {
                ext.MinMax(interpol[i]);
            }
            double eps = ext.Size * 1e-6;
            for (int i = 0; i < interpol.Length - 1; ++i)
            {   // also add points where the curve changes horizontal or vertical direction
                if ((Math.Sign(interdir[i].x) != Math.Sign(interdir[i + 1].x)) && (Math.Abs(interdir[i].x) + Math.Abs(interdir[i + 1].x) > eps))
                {
                    if (BisectPerpendicular(interparam[i], interparam[i + 1], GeoVector2D.XAxis, out double par))
                    {
                        ext.MinMax(PointAt(par));
                    }
                }
                if ((Math.Sign(interdir[i].y) != Math.Sign(interdir[i + 1].y)) && (Math.Abs(interdir[i].y) + Math.Abs(interdir[i + 1].y) > eps))
                {
                    if (BisectPerpendicular(interparam[i], interparam[i + 1], GeoVector2D.YAxis, out double par))
                    {
                        ext.MinMax(PointAt(par));
                    }
                }
            }
            return ext;
        }
        private bool NewtonMaximum(double p1, double p2, bool horizontal, out double par)
        {
            double u = (p1 + p2) / 2.0;
            TryPointDeriv2At(u, out GeoPoint2D p, out GeoVector2D deriv1, out GeoVector2D deriv2);
            while (Math.Abs(deriv1.x) > 1e-6)
            {
                u = u - (deriv1.x / deriv2.x);
                TryPointDeriv2At(u, out p, out deriv1, out deriv2);
            }
            par = u;
            return true;
        }
        private bool BisectPerpendicular(double p1, double p2, GeoVector2D dir, out double par)
        {
            double d1 = DirectionAt(p1) * dir;
            double d2 = DirectionAt(p2) * dir;
            if (d1 == 0.0)
            {
                par = p1;
                return true;
            }
            if (d2 == 0.0)
            {
                par = p2;
                return true;
            }
            if (Math.Sign(d1) == Math.Sign(d2))
            {
                par = 0.0;
                return false;
            }
            par = p1 + Math.Abs(d1) / (Math.Abs(d1) + Math.Abs(d2)) * (p2 - p1);
            return true; // this is a good value without much iteration. This method is only used to find the extend and may be somewhat imprecise
            while (p2 - p1 > 1e-6)
            {
                double p = (p1 + p2) / 2.0;
                double d = DirectionAt(p) * dir;
                if (d == 0.0)
                {
                    par = p;
                    return true;
                }
                else if (Math.Sign(d) == Math.Sign(d1))
                {
                    p1 = p;
                }
                else
                {
                    p2 = p;
                }
            }
            par = (p1 + p2) / 2.0;
            return true;
        }

        private bool TriangleHitTest(ref ClipRect rect, GeoPoint2D sp, GeoPoint2D ep, GeoPoint2D tr, double spar, double epar, GeoVector2D sdir, GeoVector2D edir)
        {
            if (!rect.TriangleHitTest(sp, ep, tr)) return false;
            if (rect.Contains(sp)) return true;
            if (rect.Contains(ep)) return true;
            if (epar - spar < 1e-6) return true;
            // ansonsten aufteilen:
            double mpar = (spar + epar) / 2.0;
            GeoPoint2D mp = PointAt(mpar);
            GeoVector2D mdir = DirectionAt(mpar);
            GeoPoint2D tr1, tr2;
            if (!Geometry.IntersectLL(sp, sdir, mp, mdir, out tr1))
            {
                if (rect.LineHitTest(sp, mp)) return true;
            }
            else
            {   // wenn es fast eine Linie ist, dann kann der Schnittpunkt ausarten
                // Hier wird getestet, ob der Schnittpunkt in dem durch die Linie aufgespannten Band liegt
                // sonst hat man es mit einer annährnden Linie zu tun
                GeoVector2D perp = (mp - sp).ToLeft();
                if (Geometry.OnLeftSide(tr1, mp, perp) == Geometry.OnLeftSide(tr1, sp, perp))
                {
                    if (rect.LineHitTest(sp, mp)) return true;
                }
                else
                {
                    if (TriangleHitTest(ref rect, sp, mp, tr1, spar, mpar, sdir, mdir)) return true;
                }
            }
            if (!Geometry.IntersectLL(mp, mdir, ep, edir, out tr2))
            {
                if (rect.LineHitTest(mp, ep)) return true;
            }
            else
            {
                GeoVector2D perp = (ep - mp).ToLeft();
                if (Geometry.OnLeftSide(tr2, ep, perp) == Geometry.OnLeftSide(tr2, mp, perp))
                {
                    if (rect.LineHitTest(mp, ep)) return true;
                }
                else
                {
                    if (TriangleHitTest(ref rect, mp, ep, tr2, mpar, epar, mdir, edir)) return true;
                }
            }
            return false;
        }
        public virtual bool HitTest(ref BoundingRect rect, bool includeControlPoints)
        {
            ClipRect clr = new ClipRect(ref rect);
            if (interpol == null) MakeTriangulation();
            for (int i = 0; i < interpol.Length - 1; ++i)
            {
                if (TriangleHitTest(ref clr, interpol[i], interpol[i + 1], tringulation[i], interparam[i], interparam[i + 1], interdir[i], interdir[i + 1]))
                {
                    return true;
                }
            }
            return false;
            //if (baseApproximation == null) baseApproximation = Approximate(true, 0.0); // Annäherung mit Bögen unter Auswertung der Tangenten
            //return baseApproximation.HitTest(ref rect, includeControlPoints);
        }

        public virtual object ReferencedObject
        {
            get { throw new Exception("The method or operation is not implemented."); }
        }

        #endregion

        #region ISerializable Members
        protected GeneralCurve2D(SerializationInfo info, StreamingContext context)
        {
            userData = info.GetValue("UserData", typeof(UserData)) as UserData;
        }
        /// <summary>
        /// Implements <see cref="ISerializable.GetObjectData"/>
        /// </summary>
        /// <param name="info">The <see cref="System.Runtime.Serialization.SerializationInfo"/> to populate with data.</param>
        /// <param name="context">The destination (<see cref="System.Runtime.Serialization.StreamingContext"/>) for this serialization.</param>
        public virtual void GetObjectData(SerializationInfo info, StreamingContext context)
        {
            info.AddValue("UserData", userData);
        }
        protected void JSonGetObjectData(IJsonWriteData data)
        {
            if (userData != null) data.AddProperty("UserData", userData);
        }
        protected void JSonSetObjectData(IJsonReadData data)
        {
            userData = data.GetPropertyOrDefault<UserData>("UserData");
        }
        #endregion
    }

    /// <summary>
    /// Interface for non affine parameterspace transformation for 2d curves
    /// </summary>

    public interface ICurveTransformation2D
    {
        GeoPoint2D TransformPoint(GeoPoint2D p);
        GeoPoint2D ReverseTransformPoint(GeoPoint2D p);
        GeoPoint2D TransformPoint(ICurve2D curve, double par);
        GeoVector2D TransformDeriv1(ICurve2D curve, double par);
        GeoVector2D TransformDeriv2(ICurve2D curve, double par);
    }

    /// <summary>
    /// A general 2D Curve which results from a non-affine transformation of the 2D space
    /// </summary>

    public class TransformedCurve2D : GeneralCurve2D
    {
        ICurve2D original;
        ICurveTransformation2D transformation2D;
        public TransformedCurve2D(ICurve2D original, ICurveTransformation2D transformation2D)
        {
            this.original = original;
            this.transformation2D = transformation2D;
        }
        protected override void GetTriangulationBasis(out GeoPoint2D[] points, out GeoVector2D[] directions, out double[] parameters)
        {
            if (original is GeneralCurve2D)
            {
                (original as GeneralCurve2D).GetTriangulationPoints(out points, out parameters);
                directions = new GeoVector2D[points.Length];
                for (int i = 0; i < parameters.Length; ++i)
                {
                    points[i] = transformation2D.TransformPoint(original, parameters[i]);
                    directions[i] = transformation2D.TransformDeriv1(original, parameters[i]);
                }
            }
            else
            {
                parameters = new double[] { 0.0, 0.1, 0.2, 0.3, 0.4, 0.5, 0.6, 0.7, 0.8, 0.9, 1.0 };
                directions = new GeoVector2D[parameters.Length];
                points = new GeoPoint2D[parameters.Length];
                for (int i = 0; i < parameters.Length; ++i)
                {
                    points[i] = transformation2D.TransformPoint(original, parameters[i]);
                    directions[i] = transformation2D.TransformDeriv1(original, parameters[i]);
                }
            }
        }
        internal override void GetTriangulationPoints(out GeoPoint2D[] points, out double[] parameters)
        {
            if (original is GeneralCurve2D)
            {
                (original as GeneralCurve2D).GetTriangulationPoints(out points, out parameters);
                for (int i = 0; i < parameters.Length; ++i)
                {
                    points[i] = transformation2D.TransformPoint(original, parameters[i]);
                }
            }
            else
            {
                parameters = new double[] { 0.0, 0.1, 0.2, 0.3, 0.4, 0.5, 0.6, 0.7, 0.8, 0.9, 1.0 };
                points = new GeoPoint2D[parameters.Length];
                for (int i = 0; i < parameters.Length; ++i)
                {
                    points[i] = transformation2D.TransformPoint(original, parameters[i]);
                }
            }
        }
        public override GeoVector2D DirectionAt(double Position)
        {
            return transformation2D.TransformDeriv1(original, Position);
        }
        public override GeoPoint2D StartPoint
        {
            get
            {
                return transformation2D.TransformPoint(original.StartPoint);
            }
            set
            {
                original.StartPoint = transformation2D.ReverseTransformPoint(value);
            }
        }
        public override GeoPoint2D EndPoint
        {
            get
            {
                return transformation2D.TransformPoint(original.EndPoint);
            }
            set
            {
                original.EndPoint = transformation2D.ReverseTransformPoint(value);
            }
        }
        public override GeoPoint2D PointAt(double Position)
        {
            return transformation2D.TransformPoint(original, Position);
        }
        public override double PositionOf(GeoPoint2D p)
        {
            return original.PositionOf(transformation2D.ReverseTransformPoint(p));
        }

        public override ICurve2D Clone()
        {
            return new TransformedCurve2D(original.Clone(), transformation2D);
        }
        public override ICurve2D CloneReverse(bool reverse)
        {
            return new TransformedCurve2D(original.CloneReverse(reverse), transformation2D);
        }
        public override void Copy(ICurve2D toCopyFrom)
        {
            if (toCopyFrom is TransformedCurve2D)
            {
                this.original = (toCopyFrom as TransformedCurve2D).original;
                this.transformation2D = (toCopyFrom as TransformedCurve2D).transformation2D;
            }
        }
        public override ICurve2D GetModified(ModOp2D m)
        {
            return new TransformedCurve2D(original.GetModified(m), transformation2D);
        }
        public override void Move(double x, double y)
        {
            original.Move(x, y);
        }
        public override void Reverse()
        {
            original.Reverse();
        }
        public override ICurve2D[] Split(double Position)
        {
            ICurve2D[] splitted = original.Split(Position);
            TransformedCurve2D[] res = new TransformedCurve2D[splitted.Length];
            for (int i = 0; i < res.Length; i++)
            {
                res[i] = new TransformedCurve2D(splitted[i], transformation2D);
            }
            return res;
        }
        public override ICurve2D Trim(double StartPos, double EndPos)
        {
            ICurve2D c2d = original.Trim(StartPos, EndPos);
            if (c2d != null) return new TransformedCurve2D(c2d, transformation2D);
            return null;
        }

        public override bool TryPointDeriv2At(double position, out GeoPoint2D point, out GeoVector2D deriv, out GeoVector2D deriv2)
        {
            if (original.TryPointDeriv2At(position, out point, out deriv, out deriv2))
            {
                point = transformation2D.TransformPoint(original, position);
                deriv = transformation2D.TransformDeriv1(original, position);
                deriv2 = transformation2D.TransformDeriv2(original, position);
                return true;
            }
            return false;
        }
    }

    /// <summary>
    /// Könnte man verwenden für nicht parametrierte 2d Kurven, mal sehen
    /// </summary>
    internal interface IImplicitCurve2D
    {
        GeoPoint2D[] Intersect(IImplicitCurve2D other);
    }
}
