using CADability.Attribute;
using CADability.Curve2D;
using CADability.GeoObject;
using CADability.UserInterface;
using MathNet.Numerics.LinearAlgebra.Double;
using System;
using System.Collections.Generic;
using System.Runtime.Serialization;

namespace CADability
{
    /* Noch sehr am Anfang: Eine Fläche soll gegeben werden durch die Bewegung einer Kurve im Raum.
     * Die Bewegung kann auch Drehungen beinhalten, ist also eine ModOp Abhängig von "u".
     * Problem ist noch die verschiedene Verwendung der Parameter: Kurven sollten neben
     * PointAt auch PointAtParam, DirectionAtParam u.s.w. haben. ParamOf.
     * Wie sehen die 2. Ableitungen aus?
     */

    public interface IMovement
    {
        ModOp GetPosition(double u);
        ICurve GetCurve(GeoPoint startHere, double umin, double umax);
        GeoVector StartDirection { get; }
        bool IsPeriodic { get; }
    }

    public class ModifiedMovement : IMovement
    {
        ModOp append;
        ModOp inverse;
        IMovement original;
        public ModifiedMovement(ModOp toAppend, IMovement original)
        {
            if (original is ModifiedMovement)
            {
                this.append = toAppend * (original as ModifiedMovement).append;
                this.original = (original as ModifiedMovement).original;
            }
            else
            {
                this.append = toAppend;
                this.original = original;
            }
            this.inverse = this.append.GetInverse();
        }
        #region IMovement Members
        ModOp IMovement.GetPosition(double u)
        {
            return append * original.GetPosition(u);
        }
        ICurve IMovement.GetCurve(GeoPoint startHere, double umin, double umax)
        {
            return original.GetCurve(inverse * startHere, umin, umax).CloneModified(append);
        }
        GeoVector IMovement.StartDirection
        {
            get
            {
                return append * original.StartDirection;
            }
        }
        bool IMovement.IsPeriodic
        {
            get
            {
                return original.IsPeriodic;
            }
        }
        #endregion
    }

    public class LinearMovement : IMovement
    {
        GeoPoint startPoint;
        GeoPoint endPoint;
        GeoVector dir;
        public LinearMovement(GeoPoint startPoint, GeoPoint endPoint)
        {
            this.startPoint = startPoint;
            this.endPoint = endPoint;
            this.dir = (endPoint - startPoint).Normalized;
        }
        #region IMovement Members
        ModOp IMovement.GetPosition(double u)
        {
            return ModOp.Translate(u * dir);
        }
        ICurve IMovement.GetCurve(GeoPoint startHere, double umin, double umax)
        {
            Line res = Line.Construct();
            res.SetTwoPoints((this as IMovement).GetPosition(umin) * startHere, (this as IMovement).GetPosition(umax) * startHere);
            return res;
        }
        GeoVector IMovement.StartDirection
        {
            get
            {
                return dir;
            }
        }
        bool IMovement.IsPeriodic
        {
            get
            {
                return false;
            }
        }
        #endregion
    }

    public class RotationMovement : IMovement
    {
        Axis axis;
        public RotationMovement(Axis axis)
        {
            this.axis = axis;
        }
        public RotationMovement(GeoPoint axisLocation, GeoVector axisDir)
        {
            this.axis = new Axis(axisLocation, axisDir);
        }

        #region IMovement Members
        ModOp IMovement.GetPosition(double u)
        {
            return ModOp.Rotate(axis.Location, axis.Direction, new SweepAngle(u));
        }
        ICurve IMovement.GetCurve(GeoPoint startHere, double umin, double umax)
        {
            Ellipse res = Ellipse.Construct();
            GeoPoint center = Geometry.DropPL(startHere, axis.Location, axis.Direction);
            GeoVector dirx = startHere - center;
            GeoVector diry = axis.Direction ^ dirx; // hoffentlich richtigrum
            Plane pln = new Plane(center, dirx, diry);
            res.SetArcPlaneCenterRadiusAngles(pln, startHere, startHere | center, umin, umax - umin);
            return res;
        }
        GeoVector IMovement.StartDirection
        {
            get
            {
                return GeoVector.NullVector;
            }
        }
        bool IMovement.IsPeriodic
        {
            get
            {
                return true;
            }
        }
        #endregion
    }

    [Serializable]
    public class CurveMovement : IMovement, ISerializable
    {
        class CurveMovementCurve : GeneralCurve
        {
            CurveMovement parent;
            GeoPoint toMove;
            double umin, umax;
            public CurveMovementCurve(CurveMovement parent, GeoPoint toMove, double umin, double umax)
            {
                this.parent = parent;
                this.toMove = toMove;
                this.umin = umin;
                this.umax = umax;
            }

            public override IGeoObject Clone()
            {
                return new CurveMovementCurve(parent, toMove, umin, umax);
            }

            public override void CopyGeometry(IGeoObject ToCopyFrom)
            {
                CurveMovementCurve other = ToCopyFrom as CurveMovementCurve;
                this.parent = other.parent;
                this.toMove = other.toMove;
                this.umin = other.umin;
                this.umax = other.umax;
            }
            double getParameter(double u)
            {
                return umin + (umax - umin) * u;
            }
            public override GeoVector DirectionAt(double u)
            {
                return parent.DirectionAt(getParameter(u));
            }

            public override void Modify(ModOp m)
            {
                ModifiedMovement mm = new CADability.ModifiedMovement(m, parent);
                (mm as IMovement).GetCurve(m * toMove, umin, umax);
            }

            public override GeoPoint PointAt(double u)
            {
                return (parent as IMovement).GetPosition(getParameter(u)) * toMove;
            }

            public override void Reverse()
            {
                double tmp = umin;
                umin = umax;
                umax = tmp;
            }

            public override ICurve[] Split(double u)
            {
                double usplit = getParameter(u);
                if (u <= umin || u >= umax) return new ICurve[] { this.Clone() as ICurve };
                return new ICurve[] { new CurveMovementCurve(parent, toMove, umin, u), new CurveMovementCurve(parent, toMove, u, umax) };
            }

            public override void Trim(double StartPos, double EndPos)
            {
                double us = getParameter(StartPos);
                double ue = getParameter(EndPos);
                umin = us;
                umax = ue;
            }

            protected override double[] GetBasePoints()
            {
                double[] ips = parent.c2d.GetInflectionPoints();
                SortedSet<double> res = new SortedSet<double>();
                for (int i = 0; i < ips.Length; i++)
                {
                    if (ips[i] > umin && ips[i] < umax) res.Add((ips[i] - umin) / (umax - umin));
                }
                res.Add(0.0);
                res.Add(1.0);
                if (res.Count <= 2) res.Add(0.5);
                ips = new double[res.Count];
                res.CopyTo(ips);
                return ips;
            }
        }

        private GeoVector DirectionAt(double u)
        {
            return (this as IMovement).GetPosition(u) * (this as IMovement).StartDirection;
        }

        ICurve2D c2d;
        ISurface surface;
        GeoPoint startPos;
        GeoVector startX, startY, startZ;
        ModOp toUnit;
        public CurveMovement(ICurve2D c2d, ISurface surface)
        {
            this.c2d = c2d;
            this.surface = surface;
            GeoVector du, dv;
            surface.DerivationAt(c2d.StartPoint, out startPos, out du, out dv);
            GeoVector2D dir2d = c2d.StartDirection;
            GeoVector2D dir2dr = dir2d.ToRight();
            startX = dir2d.x * du + dir2d.y * dv;
            startZ = du ^ dv;
            startY = startX ^ startZ;
            toUnit = (new ModOp(startX, startY, startZ, startPos)).GetInverse(); // in diesem System startet die Kurve im Ursprung in X-Richtung
        }
#if DEBUG
#endif
        #region IMovement Members
        ModOp IMovement.GetPosition(double u)
        {
            GeoVector du, dv;
            GeoPoint loc;
            surface.DerivationAt(c2d.PointAt(u), out loc, out du, out dv);
            GeoVector2D dir2d = c2d.DirectionAt(u);
            GeoVector ux = dir2d.x * du + dir2d.y * dv;
            GeoVector uz = du ^ dv;
            GeoVector uy = ux ^ uz;
            ModOp res = ModOp.Translate(loc - GeoPoint.Origin) * ModOp.Fit(new GeoVector[] { startX.Normalized, startY.Normalized, startZ.Normalized }, new GeoVector[] { ux.Normalized, uy.Normalized, uz.Normalized }) * ModOp.Translate(GeoPoint.Origin - startPos);
            return res;
            //return new ModOp(toUnit * ux, toUnit * uy, toUnit * uz, toUnit * loc);
        }
        ICurve IMovement.GetCurve(GeoPoint startHere, double umin, double umax)
        {
            return new CurveMovementCurve(this, startHere, umin, umax);
        }
        GeoVector IMovement.StartDirection
        {
            get
            {
                GeoVector du, dv;
                GeoPoint loc;
                surface.DerivationAt(c2d.PointAt(0.0), out loc, out du, out dv);
                GeoVector2D dir2d = c2d.DirectionAt(0.0);
                return dir2d.x * du + dir2d.y * dv;
            }
        }
        bool IMovement.IsPeriodic
        {
            get
            {
                return c2d.IsClosed;
            }
        }
        #endregion
#if DEBUG
        GeoObjectList Curve2DWithPerpDir
        {
            get
            {
                GeoObjectList res = new GeoObjectList();
                res.Add(c2d.MakeGeoObject(Plane.XYPlane));
                BoundingRect ext = c2d.GetExtent();
                for (int i = 0; i < 100; i++)
                {
                    double u = i / 100.0;
                    GeoVector du, dv;
                    GeoPoint loc;
                    surface.DerivationAt(c2d.PointAt(u), out loc, out du, out dv);
                    GeoPoint2D loc2d = c2d.PointAt(u);
                    GeoVector2D dir2d = c2d.DirectionAt(u);
                    GeoVector2D dir2dr = dir2d.ToRight();
                    Line2D l2d = new Line2D(loc2d, loc2d + ext.Size / 10 * dir2dr.Normalized);
                    res.Add(l2d.MakeGeoObject(Plane.XYPlane));
                }
                return res;
            }
        }
        GeoObjectList Curve3DWithPerpDir
        {
            get
            {
                GeoObjectList res = new GeoObjectList();
                ICurve c3d = surface.Make3dCurve(c2d);
                res.Add(c3d as IGeoObject);
                BoundingCube ext = c3d.GetExtent();
                for (int i = 0; i < 100; i++)
                {
                    double u = i / 100.0;
                    GeoVector du, dv;
                    GeoPoint loc;
                    surface.DerivationAt(c2d.PointAt(u), out loc, out du, out dv);
                    GeoPoint2D loc2d = c2d.PointAt(u);
                    GeoVector2D dir2d = c2d.DirectionAt(u);
                    GeoVector2D dir2dr = dir2d.ToRight();
                    GeoVector ux = dir2d.x * du + dir2d.y * dv;
                    GeoVector uz = du ^ dv;
                    GeoVector uy = ux ^ uz;
                    Line l = Line.TwoPoints(loc, loc + ext.Size / 10 * uy.Normalized);
                    res.Add(l);
                }
                return res;
            }
        }
#endif
        #region ISerializable Members
        protected CurveMovement(SerializationInfo info, StreamingContext context)
        {
            c2d = (ICurve2D)info.GetValue("C2d", typeof(ICurve2D));
            surface = (ISurface)info.GetValue("Surface", typeof(ISurface));
            startPos = (GeoPoint)info.GetValue("StartPos", typeof(GeoPoint));
            startX = (GeoVector)info.GetValue("StartX", typeof(GeoVector));
            startY = (GeoVector)info.GetValue("StartY", typeof(GeoVector));
            startZ = (GeoVector)info.GetValue("StartZ", typeof(GeoVector));
        }
        void ISerializable.GetObjectData(SerializationInfo info, StreamingContext context)
        {
            info.AddValue("C2d", c2d, c2d.GetType());
            info.AddValue("Surface", surface, surface.GetType());
            info.AddValue("StartPos", startPos, startPos.GetType());
            info.AddValue("StartX", startX, startX.GetType());
            info.AddValue("StartY", startY, startY.GetType());
            info.AddValue("StartZ", startZ, startZ.GetType());
        }
        #endregion
    }
    /// <summary>
    /// A general surface defined by a curve "toSweep", which is swept along a curve "along"
    /// The u parameter moves along the curve "toSweep", the v parameter moves along the curve "along"
    /// The curve "along" must implement IOrientation: the coordinate system is defined by the direction of "along" at parameter u, which is the z-axis, while the 
    /// OrientationAt(u) is the x-axis
    /// </summary>
    [Serializable]
    public class 
        GeneralSweptCurve : ISurfaceImpl, ISerializable
    {   // erstmal nur zum internen berechnen verwenden, kein Helper
        /*
         * gegeben: 2d Kurve "c" und surface "s" für das Bewegungssystem, das ist die v-Richtung
         * für jeden Punkt "p0" auf "toSweep" gibt es eine fixedU Kurve, die sich so bestimmt:
         * p0 im Koordinatensystem, gegeben durch Richtung der Kurve s(c(t)) Normale auf die Fläche in diesem Punkt und Kreuzprodukt
         * p0s. Das ist konstant und wird am Anfang bestimmt (p0sx, p0sy, p0sz)
         * Das Koordinatensystem an einem beliebigen Punkt t ist gegeben durch 
         * sdu(c(t))*cx'(t)+sdv(c(t))*cy'(t), sdu(c(t))*cy'(t)-sdv(c(t))*cx'(t) und dem Kreuzprodukt der beiden, in Komponentenschreibweise:
         * 
         * sxdu(c(t))*cx'(t)+sxdv(c(t))*cy'(t)
         * sydu(c(t))*cx'(t)+sydv(c(t))*cy'(t)
         * szdu(c(t))*cx'(t)+szdv(c(t))*cy'(t)
         * 
         * sxdu(c(t))*cy'(t)-sxdv(c(t))*cx'(t)
         * sydu(c(t))*cy'(t)-sydv(c(t))*cx'(t)
         * szdu(c(t))*cy'(t)-szdv(c(t))*cx'(t)
         *
         * (sydu(c(t))*cx'(t)+sydv(c(t))*cy'(t))*(szdu(c(t))*cy'(t)-szdv(c(t))*cx'(t)) - (szdu(c(t))*cx'(t)+szdv(c(t))*cy'(t))*(sydu(c(t))*cy'(t)-sydv(c(t))*cx'(t))
         *  --"-- analog
         * 
         * Diese 3 Vektoren bestimmen als Basissystem mit dem Punkt (p0sx, p0sy, p0sz) den Zielpunkt
         * Diesen müsste man jetzt nach t ableiten, aber wie soll das gehen mit sxdu(c(t)) abgeleitet? (Der Rest sind ja nur Summen und Produkte, wobei sicher auch
         * die 2. Ableitungen von cx(t), cy(t) vorkommen würden)
         * 
         * 
         * Anderer Gedanke: gegeben eine 3d Kurve und ein Vektor ("Normalen"vektor), der immer beibehalten werden soll (der darf nie in der Richtung der Kurve liegen)
         * 
         * [cx(v), cy(v), cz(v)] ist die Kurve und [nx, ny, nz] der "Normalen"vektor.
         * 
         * Das Koordinatensystem ist gegeben durch bx=c'(v), by=n^bx, und bz = bx^by  und ein Ausgangspunkt l
         * bx0, by0 und bz0 ist das System am Kurvenanfang, px, py, pz der zu ziehende Punkt in diesem System
         * 
         * Der Punkt bei v ergibt sich dann zu lx(v) + px*bx(v), ly(v) + py*by(v), lz(v) + pz*bz(v)
         * Die Ableitung bei v ergibt sich dann zu lx'(v) + px*bx'(v), ly'(v) + py*by'(v), lz'(v) + pz*bz'(v), wobei
         * l(v) = c(v)
         * bx'(v) = c''(v);
         * by'(v) = [(ny*cz'(v)-nz*cy'(v))', ...y, z analog...] = [ny*cz''(v)-nz*cy''(v), ...y, z analog...]
         * bz'(v) = [0,0,0], wenn konstant, ansonsten Kuddelmuddel, aber nur Produkte und Summen
         * Vermutlich muss die schwierige Lösung verwendet werden, da sonst die Koordinatensystem in den verschiedenen v-Positionen eine Verzerrung enthalten also
         * by(v) = [(ny*cz'(v)-nz*cy'(v)), (nx*cz'(v)-nz*cx'(v)), (ny*cx'(v)-nx*cy'(v))]
         * bz(v) = bx(v)^by(v) = [(nx*cz'(v)-nz*cx'(v))*cz'(v)-(ny*cx'(v)-nx*cy'(v))*cy'(v), ...], mit der Produktregel ergibt sich damit:
         * bz'(v) = [(nx*cz'(v)-nz*cx'(v))*cz''(v)+(nx*cz''(v)-nz*cx''(v))*cz'(v)-((ny*cx'(v)-nx*cy'(v))*cy''(v)+(ny*cx''(v)-nx*cy''(v))*cy'(v)), ...analog]
         */
        ICurve toSweep;
        ICurve along; // letztlich liefert das IMovement eine solche
        double vmin;
        double vmax;
        GeoVector normal; // diese Richtung soll immer erhalten bleiben

        // sekundäre Daten
        NurbsSurface forDerivation;
        GeoPoint baseLoc;
        GeoVector baseDirX, baseDirY, baseDirZ; // das Koordinatensystem am Anfang von "along"
        ModOp toStartPos;

        /// <summary>
        /// Creates a surface by sweeping the curve <paramref name="toSweep"/> along the curve <paramref name="along"/> and keeping the vector <paramref name="normal"/>
        /// unchanged
        /// </summary>
        /// <param name="toSweep"></param>
        /// <param name="along"></param>
        /// <param name="normal"></param>
        /// <param name="usedArea"></param>
        public GeneralSweptCurve(ICurve toSweep, ICurve along, GeoVector normal) : base()
        {
            if (normal.IsNullVector())
            {
                GeoPoint[] pnts = new GeoPoint[7];
                for (int i = 0; i < pnts.Length; i++)
                {
                    pnts[i] = along.PointAt(i / (double)(pnts.Length));
                }
                double maxDist;
                bool isLinear;
                Plane pln = Plane.FromPoints(pnts, out maxDist, out isLinear);
                if (isLinear)
                {
                    GeoVector n1 = along.StartDirection ^ GeoVector.XAxis;
                    GeoVector n2 = along.StartDirection ^ GeoVector.YAxis;
                    GeoVector n3 = along.StartDirection ^ GeoVector.ZAxis;
                    if (n1.Length > n2.Length)
                    {
                        if (n1.Length > n3.Length) normal = n1;
                        else normal = n3;
                    }
                    else
                    {
                        if (n2.Length > n3.Length) normal = n2;
                        else normal = n3;
                    }
                }
                else normal = pln.Normal;
            }
            this.toSweep = toSweep;
            this.along = along;
            this.vmin = 0;
            this.vmax = 1.0;
            this.normal = normal;
            initSecondaryData();
            initNurbsSurface();
        }
        /// <summary>
        /// Creates a surface by sweeping the curve <paramref name="toSweep"/> along the curve <paramref name="along"/>, which must also implement <see cref="IOrientation"/>
        /// </summary>
        /// <param name="toSweep"></param>
        /// <param name="along"></param>
        public GeneralSweptCurve(ICurve toSweep, ICurve along)
        {
            this.toSweep = toSweep;
            this.along = along;
            this.vmin = 0;
            this.vmax = 1.0;
            this.normal = GeoVector.Invalid;
            initSecondaryData();
            initNurbsSurface();
        }
        private void initSecondaryData()
        {
            GeoPoint loc = along.PointAt(0.0);
            GeoVector deriv1 = along.DirectionAt(0.0);
            baseLoc = loc;
            baseDirZ = deriv1;
            if (normal.IsValid()) baseDirX = normal;
            else baseDirX = (along as IOrientation).OrientationAt(0);
            baseDirY = baseDirX ^ baseDirZ;
            toStartPos = ModOp.Fit(new GeoVector[] { baseDirX, baseDirY, baseDirZ }, new GeoVector[] { GeoVector.XAxis, GeoVector.YAxis, GeoVector.ZAxis }) * ModOp.Translate(GeoPoint.Origin - loc);
        }
        private void initNurbsSurface()
        {
            // die NurbsSurface brauchen wir hier für die Ableitungen. Die sind nur schwer aus der Kurvendefinition herauszukriegen
            // Hier also erstmal mit NURBS. Besser wäre es, aus der eigentlichen Definition, da muss man aber Gehirnschmalz reinstecken
            // die NURBS Fläche läuft von 0 bis 1, also synchron zu PointAt. PointAt ist aber genauer
            GeoPoint[,] pnts = new CADability.GeoPoint[8, 8];
            double[] uknots = new double[pnts.GetLength(0)];
            double[] vknots = new double[pnts.GetLength(1)];
            for (int i = 0; i < uknots.Length; i++)
            {
                uknots[i] = (double)i / (uknots.Length - 1);
            }
            for (int i = 0; i < vknots.Length; i++)
            {
                vknots[i] = (double)i / (vknots.Length - 1);
            }
            for (int i = 0; i < pnts.GetLength(0); i++)
            {
                for (int j = 0; j < pnts.GetLength(1); j++)
                {
                    pnts[i, j] = PointAt(new GeoPoint2D(uknots[i], vknots[j]));
                }
            }
            try
            {
                forDerivation = new NurbsSurface(pnts, 3, 3, uknots, vknots, false, false);
                forDerivation.ScaleKnots(0, 1, 0, 1);
#if DEBUG
                GeoObjectList dbg = forDerivation.DebugGrid;
#endif
            }
            catch (NurbsException) { }
        }
        /// <summary>
        /// Returns the projected curve. The curve "toProject" goes in direction of v, i.e. for each v there is 
        /// a value for u. It starts at v=0.0 and ends at v=1.0 - or reverse. The curve "toSweep" of this surface must be planar.
        /// </summary>
        /// <param name="toProject"></param>
        /// <returns></returns>
        internal ICurve2D GetProjectedCurveAlongV(ICurve toProject)
        {
            int n = 8;
            List<GeoPoint2D> pnts = new List<GeoPoint2D>();
            ModOp m;
            ICurve swept;
            for (int i = 1; i < n - 1; i++)
            {
                double v = (double)i / (n - 1);
                m = modOpAt(v);
                swept = toSweep.CloneModified(m);
                double[] ips = toProject.GetPlaneIntersection(swept.GetPlane());
                // eigentlich muss es immer genau einen Schnittpunkt geben
                for (int j = 0; j < ips.Length; j++)
                {
                    if (ips[j] > -1e-6 && ips[j] < 1 + 1e-6)
                    {
                        GeoPoint p = toProject.PointAt(ips[j]);
                        double u = swept.PositionOf(p);
                        pnts.Add(new GeoPoint2D(u, v));
                        break;
                    }
                }
            }
            m = modOpAt(0);
            swept = toSweep.CloneModified(m);
            Plane pln = swept.GetPlane();
            bool forward = pln.Distance(toProject.StartPoint) < pln.Distance(toProject.EndPoint);
            if (forward)
            {
                pnts.Insert(0, new GeoPoint2D(swept.PositionOf(toProject.StartPoint), 0));
            }
            else
            {
                pnts.Insert(0, new GeoPoint2D(swept.PositionOf(toProject.EndPoint), 0));
            }
            m = modOpAt(1);
            swept = toSweep.CloneModified(m);
            if (forward)
            {
                pnts.Add(new GeoPoint2D(swept.PositionOf(toProject.EndPoint), 1));
            }
            else
            {
                pnts.Add(new GeoPoint2D(swept.PositionOf(toProject.StartPoint), 1));
            }
            BSpline2D res = new BSpline2D(pnts.ToArray(), 3, false);
            // hier noch Genauigkeit testen und ggf. verfeinern
            if ((PointAt(res.StartPoint) | toProject.StartPoint) + (PointAt(res.EndPoint) | toProject.EndPoint) > (PointAt(res.StartPoint) | toProject.EndPoint) + (PointAt(res.EndPoint) | toProject.StartPoint))
            {
                res.Reverse();
            }
            return res;
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
            return new FixedUCurve(this, u, vmin, vmax);
        }
        /// <summary>
        /// Overrides <see cref="CADability.GeoObject.ISurfaceImpl.FixedV (double, double, double)"/>
        /// </summary>
        /// <param name="u"></param>
        /// <param name="umin"></param>
        /// <param name="umax"></param>
        /// <returns></returns>
        public override ICurve FixedV(double u, double umin, double umax)
        {
            ICurve res = toSweep.CloneModified(modOpAt(u));
            res.Trim(umin, umax);
            return res;
        }
        /// <summary>
        /// Overrides <see cref="CADability.GeoObject.ISurfaceImpl.GetModified (ModOp)"/>
        /// </summary>
        /// <param name="m"></param>
        /// <returns></returns>
        public override ISurface GetModified(ModOp m)
        {
            return new GeneralSweptCurve(toSweep.CloneModified(m), along.CloneModified(m), m * normal);
        }

        public override GeoVector UDirection(GeoPoint2D uv)
        {
            GeoVector deriv1 = along.DirectionAt(uv.y);
            GeoVector dirX = deriv1;
            GeoVector dirY = normal ^ dirX;
            GeoVector dirZ = dirX ^ dirY;
            GeoVector udir = toStartPos * toSweep.DirectionAt(uv.x); // Richtung im Anfangssystem
            udir = udir.x * dirX + udir.y * dirY + udir.z * dirZ;
            GeoVector dbg = modOpAt(uv.y) * toSweep.DirectionAt(uv.x);
            return udir;
        }
        public override GeoVector VDirection(GeoPoint2D uv)
        {
            /* p0 ist der Punkt, der verschoben wird (liegt irgendwo auf toSweep) und die Kurve in vdirection erzeugt
            * Der Punkt bei v ergibt sich dann zu p(v) = l(v) + p0x * bx(v), ly(v) + p0y * by(v), lz(v) + p0z * bz(v)
            * gesucht ist jetzt p'(v) = l'(v) + p0x*bx'(v) + p0y*by'(v) + p0z*bz'(v)
            * Die Ableitung bei v ergibt sich dann zu lx'(v) + px*bx'(v), ly'(v) + py*by'(v), lz'(v) + pz*bz'(v), wobei
            * l(v) = c(v)
            * l'(v) = c'(v)
            * bx'(v) = c''(v);
            * by'(v) = [(ny*cz'(v) - nz * cy'(v))', ...y, z analog...] = [ny * cz''(v) - nz * cy''(v), ...y, z analog...]
            * bz'(v) = s.u.
            * Vermutlich muss die schwierige Lösung verwendet werden, da sonst die Koordinatensystem in den verschiedenen v - Positionen eine Verzerrung enthalten also
            * bx(v) = [cx'(v), cy'(v), cz'(v)]
            * by(v) = n ^ bx(v) = [(ny * cz'(v)-nz*cy'(v)), (nz * cx'(v)-nx*cz'(v)), (ny * cz'(v)-nz*cy'(v))]
            * bz(v) = bx(v) ^ by(v) = [
            *    cy'(v)*(nx * cy'(v)-ny*cx'(v)) - cz'(v)*(nz * cx'(v)-nx*cz'(v)),
            *    cz'(v)*(ny * cz'(v)-nz*cy'(v)) - cx'(v)*(ny * cz'(v)-nz*cy'(v)),
            *    cx'(v)*(nz * cx'(v)-nx*cz'(v)) - cy'(v)*(ny * cz'(v)-nz*cy'(v))]
            
            * der Produktregel ergibt sich damit:
            * bz'(v) = [
            *    cy''(v)*(nx * cy'(v)-ny*cx'(v)) + cy'(v)*(nx * cy''(v)-ny*cx''(v)) - (cz''(v)*(nz * cx'(v)-nx*cz'(v)) + cz'(v)*(nz * cx''(v)-nx*cz''(v))),
            *    cz''(v)*(ny * cz'(v)-nz*cy'(v)) + cz'(v)*(ny * cz''(v)-nz*cy''(v)) - (cx''(v)*(nx * cy'(v)-ny*cx'(v)) + cx'(v)*(nx * cy''(v)-ny*cx''(v))),
            *    cx''(v)*(nz * cx'(v)-nx*cz'(v)) + cx'(v)*(nz * cx''(v)-nx*cz''(v)) - (cy''(v)*(ny * cz'(v)-nz*cy'(v)) + cy'(v)*(ny * cz''(v)-nz*cy''(v)))
            *    
            */
            GeoPoint2D p1 = new GeoPoint2D(uv.x, Math.Max(0, uv.y - 0.01));
            GeoPoint2D p2 = new GeoPoint2D(uv.x, Math.Min(1, uv.y + 0.01));
            double f = 1 / (p2.y - p1.y);
            return f * (PointAt(p2) - PointAt(p1)); // das ist einfach ein kleines umgebendes Intervall diskret abgeleitet (bis ich eine bessere Lösung habe)
            // forDerivation wird nicht benötigt

            //Unreachable code
            /*            
            GeoPoint loc;
            GeoVector deriv1, deriv2;
            if (along.TryPointDeriv2At(uv.y, out loc, out deriv1, out deriv2))
            {
                GeoVector dirX = deriv1;
                GeoVector dirY = normal ^ dirX;
                GeoVector dirZ = dirX ^ dirY;
                GeoPoint p = toStartPos * toSweep.PointAt(uv.x); // Punkt im Anfangssystem
                // GeoVector p = toSweep.PointAt(uv.x) - baseLoc;

                GeoPoint dbg1 = modOpAt(uv.y) * toSweep.PointAt(uv.x);
                Matrix m = DenseMatrix.OfColumnArrays(dirX, dirY, dirZ);
                Vector b = new DenseVector(dbg1 - baseLoc);
                Vector x = (Vector)m.Solve(b);
                if (x.IsValid())
                {
                    GeoPoint dbg2 = baseLoc + x[0] * dirX + x[1] * dirY + x[2] * dirZ;
                    p.x = x[0];
                    p.y = x[1];
                    p.z = x[2];
                }

                GeoVector ddirx = deriv2;
                GeoVector ddiry = normal ^ ddirx;
                GeoVector ddirz = new GeoVector(
                    deriv2.y * (normal.x * deriv1.y - normal.y * deriv1.x) + deriv1.y * (normal.x * deriv2.y - normal.y * deriv2.x) - (deriv2.z * (normal.z * deriv1.x - normal.x * deriv1.z) + deriv1.z * (normal.z * deriv2.x - normal.x * deriv2.z)),
                    deriv2.z * (normal.y * deriv1.z - normal.z * deriv1.y) + deriv1.z * (normal.y * deriv2.z - normal.z * deriv2.y) - (deriv2.x * (normal.x * deriv1.y - normal.y * deriv1.x) + deriv1.x * (normal.x * deriv2.y - normal.y * deriv2.x)),
                    deriv2.x * (normal.z * deriv1.x - normal.x * deriv1.z) + deriv1.x * (normal.z * deriv2.x - normal.x * deriv2.z) - (deriv2.y * (normal.y * deriv1.z - normal.z * deriv1.y) + deriv1.y * (normal.y * deriv2.z - normal.z * deriv2.y))
                    );
                GeoVector dbg = ddirx ^ ddiry;
                GeoVector res = deriv1 + p.x * ddirx + p.y * ddiry + p.z * ddirz;
                return res;
            }
            return GeoVector.NullVector; // da muss nocht die NURBS Hilsfläche gemacht werden
            */
        }
        private ModOp modOpAt(double v)
        {
            GeoPoint loc = along.PointAt(v);
            GeoVector deriv1 = along.DirectionAt(v);
            GeoVector dirX = (along as IOrientation).OrientationAt(v);
            GeoVector dirZ = deriv1;
            GeoVector dirY = dirZ ^ dirX;
            ModOp res = ModOp.Translate(loc - GeoPoint.Origin) * ModOp.Fit(new GeoVector[] { baseDirX.Normalized, baseDirY.Normalized, baseDirZ.Normalized }, new GeoVector[] { dirX.Normalized, dirY.Normalized, dirZ.Normalized }) * ModOp.Translate(GeoPoint.Origin - baseLoc);
            bool dbgort = res.IsOrthogonal;
            return res;

        }
        public override GeoPoint PointAt(GeoPoint2D uv)
        {
            return modOpAt(uv.y) * toSweep.PointAt(uv.x);
        }
        public override void DerivationAt(GeoPoint2D uv, out GeoPoint location, out GeoVector du, out GeoVector dv)
        {
            location = PointAt(uv);
            du = UDirection(uv);
            dv = VDirection(uv);
        }
        public override void Derivation2At(GeoPoint2D uv, out GeoPoint location, out GeoVector du, out GeoVector dv, out GeoVector duu, out GeoVector dvv, out GeoVector duv)
        {
            throw new NotImplementedException("Derivation2At must be implemented");
        }
        public override void GetNaturalBounds(out double umin, out double umax, out double vmin, out double vmax)
        {
            umin = 0.0;
            umax = 1.0;
            vmin = 0.0;
            vmax = 1.0; 
        }
        public override bool IsUPeriodic
        {
            get
            {
                return toSweep.IsClosed;
            }
        }
        public override bool IsVPeriodic
        {
            get
            {
                return along.IsClosed;
            }
        }
        public override double UPeriod
        {
            get
            {
                return 1.0;
            }
        }
        public override double VPeriod
        {
            get
            {
                return 1.0;
            }
        }
        public override ICurve Make3dCurve(ICurve2D curve2d)
        {
            if (curve2d is Line2D && Math.Abs(curve2d.StartPoint.y - curve2d.EndPoint.y) < Precision.eps)
            {
                ICurve res = toSweep.CloneModified(modOpAt(curve2d.StartPoint.y));
                if (curve2d.StartPoint.x < curve2d.EndPoint.x) res.Trim(curve2d.StartPoint.x, curve2d.EndPoint.x);
                else
                {
                    res.Trim(curve2d.EndPoint.x, curve2d.StartPoint.x);
                    res.Reverse();
                }
                return res;
            }
            if (curve2d is ProjectedCurve pc)
            {
                if (pc.Surface is GeneralSweptCurve)
                {
                    BoundingRect otherBounds = new BoundingRect(PositionOf(pc.Surface.PointAt(pc.StartPoint)), PositionOf(pc.Surface.PointAt(pc.EndPoint)));
                    if (pc.Surface.SameGeometry(pc.GetExtent(), this, otherBounds, Precision.eps, out ModOp2D notneeded))
                    {
                        return pc.Curve3DFromParams; // if trimmed or reversed still returns the correct 3d curve (but trimmed and/or reversed)
                    }
                }
            }
            return base.Make3dCurve(curve2d);
        }
        public override ModOp2D ReverseOrientation()
        {
            boxedSurfaceEx = null;
            along.Reverse();
            return ModOp2D.Translate(1, 0) * ModOp2D.Scale(-1, 1);
        }
        public override IPropertyEntry GetPropertyEntry(IFrame frame)
        {
            return new GroupProperty("GeneralSweptCurve", new IPropertyEntry[0]);
        }

        protected GeneralSweptCurve(SerializationInfo info, StreamingContext context)
        {
            toSweep = (ICurve)info.GetValue("ToSweep", typeof(ICurve));
            along = (ICurve)info.GetValue("Along", typeof(ICurve));
            normal = (GeoVector)info.GetValue("Normal", typeof(GeoVector));
            vmin = (double)info.GetValue("Vmin", typeof(double));
            vmax = (double)info.GetValue("Vmax", typeof(double));
            // initNurbsSurface();
        }
        void ISerializable.GetObjectData(SerializationInfo info, StreamingContext context)
        {
            info.AddValue("ToSweep", toSweep, toSweep.GetType());
            info.AddValue("Along", along, along.GetType());
            info.AddValue("Normal", normal, normal.GetType());
            info.AddValue("Vmin", vmin, typeof(double));
            info.AddValue("Vmax", vmax, typeof(double));
        }
#if DEBUG
        internal GeoObjectList DebugAlong
        {
            get
            {
                GeoObjectList res = new GeoObjectList();
                res.Add(along as IGeoObject);
                BoundingCube ext = along.GetExtent();
                double length = ext.Size / 200;
                ColorDef cdx = new ColorDef("dirx", System.Drawing.Color.Red);
                ColorDef cdy = new ColorDef("diry", System.Drawing.Color.LawnGreen);
                ColorDef cdz = new ColorDef("dirz", System.Drawing.Color.BlueViolet);
                for (int i = 0; i < 100; i++)
                {
                    double v = i / 99.0;
                    GeoPoint loc = along.PointAt(v);
                    GeoVector deriv1 = along.DirectionAt(v);

                    GeoVector dirX = deriv1;
                    GeoVector dirY = normal ^ dirX;
                    GeoVector dirZ = dirX ^ dirY;

                    Line lx = Line.TwoPoints(loc, loc + length * dirX.Normalized);
                    lx.ColorDef = cdx;
                    res.Add(lx);
                    Line ly = Line.TwoPoints(loc, loc + length * dirY.Normalized);
                    ly.ColorDef = cdy;
                    res.Add(ly);
                    Line lz = Line.TwoPoints(loc, loc + length * dirZ.Normalized);
                    lz.ColorDef = cdz;
                    res.Add(lz);
                }
                return res;
            }
        }
        internal GeoObjectList DebugForDerivation
        {
            get
            {
                return forDerivation.DebugGrid;
            }
        }
#endif
        [Serializable]
        public class FixedUCurve : GeneralCurve, ISerializable
        {
            private double u;
            private double vmax;
            private double vmin;
            private GeneralSweptCurve parent;

            public FixedUCurve(GeneralSweptCurve parent, double u, double vmin, double vmax)
            {
                this.vmin = vmin;
                this.vmax = vmax;
                this.u = u;
                this.parent = parent;
            }
            public override IGeoObject Clone()
            {
                throw new NotImplementedException();
            }

            public override void CopyGeometry(IGeoObject ToCopyFrom)
            {
                throw new NotImplementedException();
            }

            public override GeoVector DirectionAt(double Position)
            {
                return parent.VDirection(new GeoPoint2D(u, Position));
            }

            public override void Modify(ModOp m)
            {
                throw new NotImplementedException();
            }
            double pos(double pos)
            {
                return vmin + pos * (vmax - vmin);
            }
            public override GeoPoint PointAt(double Position)
            {
                return parent.modOpAt(pos(Position)) * parent.toSweep.PointAt(u);
            }

            public override void Reverse()
            {
                double tmp = vmax;
                vmax = vmin;
                vmin = tmp;
            }

            public override ICurve[] Split(double Position)
            {
                throw new NotImplementedException();
            }

            public override void Trim(double StartPos, double EndPos)
            {
                throw new NotImplementedException();
            }

            protected override double[] GetBasePoints()
            {

                return (parent.along as ICurve).GetSavePositions();
            }

            protected FixedUCurve(SerializationInfo info, StreamingContext context) : base(info, context)
            {
                parent = (GeneralSweptCurve)info.GetValue("Parent", typeof(GeneralSweptCurve));
                u = (double)info.GetValue("U", typeof(double));
                vmin = (double)info.GetValue("Vmin", typeof(double));
                vmax = (double)info.GetValue("Vmax", typeof(double));
            }
            void ISerializable.GetObjectData(SerializationInfo info, StreamingContext context)
            {
                base.GetObjectData(info, context);
                info.AddValue("Parent", parent, parent.GetType());
                info.AddValue("U", u, typeof(double));
                info.AddValue("Vmin", vmin, typeof(double));
                info.AddValue("Vmax", vmax, typeof(double));
            }

        }
    }
    public class SweptArc : GeneralSweptCurve
    {
        public SweptArc(Ellipse toSweep, ICurve along) : base(toSweep, along)
        {

        }
        public static SweptArc Debug(ICurve along)
        {
            Plane pln = new Plane(along.StartPoint, (along as IOrientation).OrientationAt(0.0), along.StartDirection ^ (along as IOrientation).OrientationAt(0.0));
            Ellipse arc = Ellipse.Construct();
            arc.SetArcPlaneCenterRadiusAngles(pln, pln.Location, 10, -Math.PI / 2.0, Math.PI / 2.0);
            return new SweptArc(arc, along);
        }
    }
}
