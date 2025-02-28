using CADability.GeoObject;
using MathNet.Numerics.LinearAlgebra.Double;
using System;
using System.Collections.Generic;
using System.Runtime.Serialization;

namespace CADability
{
    /// <summary>
    /// Represents a bounding cuboid, that is an extent in 3 dimensions
    /// </summary>
    [JsonVersion(serializeAsStruct = true, version = 1)]
    [Serializable()]
    public struct BoundingCube : ISerializable, IJsonSerialize
    {
        public double Xmin, Xmax, Ymin, Ymax, Zmin, Zmax;
        /// <summary>
        /// Gets an array of the 8 vertices of this cube
        /// </summary>
        public GeoPoint[] Points
        {
            get
            {
                GeoPoint[] points = new GeoPoint[8];
                points[0] = new GeoPoint(Xmin, Ymin, Zmin);
                points[1] = new GeoPoint(Xmin, Ymin, Zmax);
                points[2] = new GeoPoint(Xmin, Ymax, Zmin);
                points[3] = new GeoPoint(Xmin, Ymax, Zmax);
                points[4] = new GeoPoint(Xmax, Ymin, Zmin);
                points[5] = new GeoPoint(Xmax, Ymin, Zmax);
                points[6] = new GeoPoint(Xmax, Ymax, Zmin);
                points[7] = new GeoPoint(Xmax, Ymax, Zmax);
                return points;
            }
        }
        /// <summary>
        /// Gets an array[12,2] of pairs of point indices defining the 12 edges
        /// of this cube. Indices are to the <see cref="Points"/>
        /// </summary>
        public int[,] LineNumbers
        {
            get
            {
                int[,] lines = new int[12, 2];
                lines[0, 0] = lines[1, 0] = lines[2, 0] = 0;
                lines[0, 1] = lines[3, 0] = lines[4, 0] = 1;
                lines[1, 1] = lines[5, 0] = lines[6, 0] = 2;
                lines[3, 1] = lines[5, 1] = lines[7, 0] = 3;
                lines[2, 1] = lines[8, 0] = lines[9, 0] = 4;
                lines[4, 1] = lines[8, 1] = lines[10, 0] = 5;
                lines[6, 1] = lines[9, 1] = lines[11, 0] = 6;
                lines[7, 1] = lines[10, 1] = lines[11, 1] = 7;
                return lines;
            }
        }
        /// <summary>
        /// Gets the 12 edges of the cube as a GeoPoint[12, 2] array
        /// </summary>
        public GeoPoint[,] Lines
        {
            get
            {
                GeoPoint[] points = Points;
                GeoPoint[,] lines = new GeoPoint[12, 2];
                lines[0, 0] = lines[1, 0] = lines[2, 0] = points[0];
                lines[0, 1] = lines[3, 0] = lines[4, 0] = points[1];
                lines[1, 1] = lines[5, 0] = lines[6, 0] = points[2];
                lines[3, 1] = lines[5, 1] = lines[7, 0] = points[3];
                lines[2, 1] = lines[8, 0] = lines[9, 0] = points[4];
                lines[4, 1] = lines[8, 1] = lines[10, 0] = points[5];
                lines[6, 1] = lines[9, 1] = lines[11, 0] = points[6];
                lines[7, 1] = lines[10, 1] = lines[11, 1] = points[7];
                return lines;
            }
        }
        /// <summary>
        /// Returns 6 planar faces, the faces of this cube
        /// </summary>
        /// <returns>the faces</returns>
        public Face[] GetSides()
        {
            try
            {
                GeoPoint[] points = Points;
                Face[] faces = new Face[6];
                faces[0] = Face.MakeFace(points[0], points[1], points[3], points[2]);
                faces[1] = Face.MakeFace(points[0], points[4], points[5], points[1]);
                faces[2] = Face.MakeFace(points[0], points[2], points[6], points[4]);
                faces[3] = Face.MakeFace(points[1], points[5], points[7], points[3]);
                faces[4] = Face.MakeFace(points[2], points[3], points[7], points[6]);
                faces[5] = Face.MakeFace(points[4], points[6], points[7], points[5]);
                return faces;
            }
            catch (PlaneException)
            {
                return new Face[0];
            }
        }
        public PlaneSurface[] GetPlanes()
        {
            try
            {
                GeoPoint[] points = Points;
                PlaneSurface[] planes = new PlaneSurface[6];
                planes[0] = new PlaneSurface(points[0], points[1], points[3]);
                planes[1] = new PlaneSurface(points[0], points[4], points[5]);
                planes[2] = new PlaneSurface(points[0], points[2], points[6]);
                planes[3] = new PlaneSurface(points[1], points[5], points[7]);
                planes[4] = new PlaneSurface(points[2], points[3], points[7]);
                planes[5] = new PlaneSurface(points[4], points[6], points[7]);
                return planes;
            }
            catch (PlaneException)
            {
                return new PlaneSurface[0];
            }
        }
        /// <summary>
        /// Returns a solid cube as a <see cref="Solid"/>.
        /// </summary>
        /// <returns>the solid</returns>
        public Solid GetSolid()
        {
            Shell sh = Shell.Construct();
            sh.SetFaces(GetSides());
            Solid so = Solid.Construct();
            so.SetShell(sh);
            return so;
        }
        internal Solid AsBox
        {
            get
            {
                return GetSolid();
            }
        }
        private bool isCube; // wird nicht gespeichert, findet beim QuadTree Verwendung
        /// <summary>
        /// Constructs a BoundingCube from minimum and maximum values
        /// </summary>
        /// <param name="Xmin">Minimum in x-direction</param>
        /// <param name="Xmax">Maximum in x-direction</param>
        /// <param name="Ymin">Minimum in y-direction</param>
        /// <param name="Ymax">Maximum in y-direction</param>
        /// <param name="Zmin">Minimum in z-direction</param>
        /// <param name="Zmax">Maximum in z-direction</param>
        public BoundingCube(double Xmin, double Xmax, double Ymin, double Ymax, double Zmin, double Zmax)
        {
            this.Xmin = Xmin;
            this.Xmax = Xmax;
            this.Ymin = Ymin;
            this.Ymax = Ymax;
            this.Zmin = Zmin;
            this.Zmax = Zmax;
            isCube = false;
        }
        /// <summary>
        /// Constructs a equal sided BoundingCube from a center point and a "radius" (half width)
        /// </summary>
        /// <param name="center">Center point</param>
        /// <param name="halfSize">Half of the width</param>
        public BoundingCube(GeoPoint center, double halfSize)
        {
            this.Xmin = center.x - halfSize;
            this.Xmax = center.x + halfSize;
            this.Ymin = center.y - halfSize;
            this.Ymax = center.y + halfSize;
            this.Zmin = center.z - halfSize;
            this.Zmax = center.z + halfSize;
            isCube = true;
        }
        /// <summary>
        /// Constructs a BoundingCube, that encloses all given points
        /// </summary>
        /// <param name="p">points to enclose</param>
        public BoundingCube(params GeoPoint[] p)
        {
            Xmin = double.MaxValue;
            Xmax = double.MinValue;
            Ymin = double.MaxValue;
            Ymax = double.MinValue;
            Zmin = double.MaxValue;
            Zmax = double.MinValue;
            for (int i = 0; i < p.Length; ++i)
            {
                if (p[i].x < Xmin) Xmin = p[i].x;
                if (p[i].x > Xmax) Xmax = p[i].x;
                if (p[i].y < Ymin) Ymin = p[i].y;
                if (p[i].y > Ymax) Ymax = p[i].y;
                if (p[i].z < Zmin) Zmin = p[i].z;
                if (p[i].z > Zmax) Zmax = p[i].z;
            }
            isCube = false;
        }
        public void Set(double Xmin, double Xmax, double Ymin, double Ymax, double Zmin, double Zmax)
        {
            this.Xmin = Xmin;
            this.Xmax = Xmax;
            this.Ymin = Ymin;
            this.Ymax = Ymax;
            this.Zmin = Zmin;
            this.Zmax = Zmax;
        }

        internal double MaxDistTo(GeoPoint p)
        {
            GeoPoint[] vertices = Points;
            double maxDist = 0.0;
            for (int i = 0; i < vertices.Length; i++)
            {
                maxDist = Math.Max(maxDist, vertices[i] | p);
            }
            return maxDist;
        }
        internal double MinDistTo(GeoPoint p)
        {
            if (Contains(p)) return 0.0;
            GeoPoint[] vertices = Points;
            double minDist = double.MaxValue;
            for (int i = 0; i < vertices.Length; i++)
            {
                minDist = Math.Min(minDist, vertices[i] | p);
            }
            return minDist;
        }

        /// <summary>
        /// Makes this BoundingCube include the provided point. You can start with an <see cref="EmptyBoundingCube"/>
        /// </summary>
        /// <param name="p">Point to be included</param>
        public void MinMax(GeoPoint p)
        {
            if (p.x < Xmin) Xmin = p.x;
            if (p.x > Xmax) Xmax = p.x;
            if (p.y < Ymin) Ymin = p.y;
            if (p.y > Ymax) Ymax = p.y;
            if (p.z < Zmin) Zmin = p.z;
            if (p.z > Zmax) Zmax = p.z;
        }
        /// <summary>
        /// Makes this BoundingCube include the provided BoundingCube. You can start with an <see cref="EmptyBoundingCube"/>
        /// </summary>
        /// <param name="b">Cube to be included</param>
        public void MinMax(BoundingCube b)
        {
            if (b.Xmin < Xmin) Xmin = b.Xmin;
            if (b.Xmax > Xmax) Xmax = b.Xmax;
            if (b.Ymin < Ymin) Ymin = b.Ymin;
            if (b.Ymax > Ymax) Ymax = b.Ymax;
            if (b.Zmin < Zmin) Zmin = b.Zmin;
            if (b.Zmax > Zmax) Zmax = b.Zmax;
        }
        public static BoundingCube operator +(BoundingCube b1, BoundingCube b2)
        {
            return new BoundingCube(Math.Min(b1.Xmin, b2.Xmin), Math.Max(b1.Xmax, b2.Xmax), Math.Min(b1.Ymin, b2.Ymin), Math.Max(b1.Ymax, b2.Ymax), Math.Min(b1.Zmin, b2.Zmin), Math.Max(b1.Zmax, b2.Zmax));
        }
        /// <summary>
        /// BoundingCube defining the interval [0,1] in all directions
        /// </summary>
        static public BoundingCube UnitBoundingCube = new BoundingCube(0, 1, 0, 1, 0, 1);
        /// <summary>
        /// Empty BoundingCube. Defined by the special values <see cref="double.MinValue"/> and <see cref="double.MaxValue"/>.
        /// Often used with the <see cref="MinMax(GeoPoint)"/> or <see cref="MinMax(BoundingCube)"/> Methods.
        /// </summary>
        static public BoundingCube EmptyBoundingCube
        {
            get
            {
                return new BoundingCube(double.MaxValue, double.MinValue, double.MaxValue, double.MinValue, double.MaxValue, double.MinValue);
            }
        }
        /// <summary>
        /// Infinite BoundingCube. Ranging from <see cref="double.MinValue"/> to <see cref="double.MaxValue"/>.
        /// </summary>
        static public BoundingCube InfiniteBoundingCube
        {
            get
            {
                return new BoundingCube(double.MinValue, double.MaxValue, double.MinValue, double.MaxValue, double.MinValue, double.MaxValue);
            }
        }
        /// <summary>
        /// Returns true if the two cubes are disjoint (do not overlap and do not touch)
        /// </summary>
        /// <param name="b1">First cube</param>
        /// <param name="b2">Second cube</param>
        /// <returns>true if disjoint</returns>
        public static bool Disjoint(BoundingCube b1, BoundingCube b2) // keine Überschneidung
        {   // es ist wichtig, dass hier < und nicht <= steht, da sonst Linien, die genau auf einer
            // Seite eines Würfels liegen aus dem QuadTree rausfliegen
            return b1.Xmax < b2.Xmin || b1.Ymax < b2.Ymin || b1.Zmax < b2.Zmin || b2.Xmax < b1.Xmin || b2.Ymax < b1.Ymin || b2.Zmax < b1.Zmin;
        }
        /// <summary>
        /// Gets the sum of width, height and deepth
        /// </summary>
        public double Size
        {
            get
            {
                return ((Xmax - Xmin) + (Ymax - Ymin) + (Zmax - Zmin));
            }
        }
        /// <summary>
        /// Gets the product of width, height and deepth
        /// </summary>
        public double Volume
        {
            get
            {
                return ((Xmax - Xmin) * (Ymax - Ymin) * (Zmax - Zmin));
            }
        }
        /// <summary>
        /// Gets the length of the maximum side
        /// </summary>
        public double MaxSide
        {
            get
            {   // ich hoffe das wird optimiert hier:
                if ((Xmax - Xmin) < (Ymax - Ymin))
                {
                    if ((Zmax - Zmin) < (Ymax - Ymin))
                    {
                        return (Ymax - Ymin);
                    }
                    else
                    {
                        return (Zmax - Zmin);
                    }
                }
                else
                {
                    if ((Zmax - Zmin) < (Xmax - Xmin))
                    {
                        return (Xmax - Xmin);
                    }
                    else
                    {
                        return (Zmax - Zmin);
                    }
                }
            }
        }
        /// <summary>
        /// Gets the extension in x direction
        /// </summary>
        public double XDiff
        {
            get
            {
                return Xmax - Xmin;
            }
        }
        /// <summary>
        /// Gets the extension in y direction
        /// </summary>
        public double YDiff
        {
            get
            {
                return Ymax - Ymin;
            }
        }
        /// <summary>
        /// Gets the extension in z direction
        /// </summary>
        public double ZDiff
        {
            get
            {
                return Zmax - Zmin;
            }
        }
        /// <summary>
        /// Returns the center of this BoundingCube
        /// </summary>
        /// <returns></returns>
        public GeoPoint GetCenter()
        {
            return new GeoPoint((Xmin + Xmax) / 2.0, (Ymin + Ymax) / 2.0, (Zmin + Zmax) / 2.0);
        }
        #region ISerializable Members
        /// <summary>
        /// Constructor required by deserialization
        /// </summary>
        /// <param name="info">SerializationInfo</param>
        /// <param name="context">StreamingContext</param>
        public BoundingCube(SerializationInfo info, StreamingContext context)
        {
            Xmin = (double)info.GetValue("Xmin", typeof(double));
            Xmax = (double)info.GetValue("Xmax", typeof(double));
            Ymin = (double)info.GetValue("Ymin", typeof(double));
            Ymax = (double)info.GetValue("Ymax", typeof(double));
            Zmin = (double)info.GetValue("Zmin", typeof(double));
            Zmax = (double)info.GetValue("Zmax", typeof(double));
            isCube = false;
        }

        /// <summary>
        /// Implements <see cref="ISerializable.GetObjectData"/>
        /// </summary>
        /// <param name="info">The <see cref="System.Runtime.Serialization.SerializationInfo"/> to populate with data.</param>
        /// <param name="context">The destination (<see cref="System.Runtime.Serialization.StreamingContext"/>) for this serialization.</param>
        public void GetObjectData(SerializationInfo info, StreamingContext context)
        {
            info.AddValue("Xmin", Xmin, typeof(double));
            info.AddValue("Xmax", Xmax, typeof(double));
            info.AddValue("Ymin", Ymin, typeof(double));
            info.AddValue("Ymax", Ymax, typeof(double));
            info.AddValue("Zmin", Zmin, typeof(double));
            info.AddValue("Zmax", Zmax, typeof(double));
        }

        #endregion
        /// <summary>
        /// Expands the bounding cube by the given value in all directions.
        /// </summary>
        /// <param name="w">offset to expand with</param>
        public void Expand(double w)
        {
            Xmin -= w;
            Xmax += w;
            Ymin -= w;
            Ymax += w;
            Zmin -= w;
            Zmax += w;
        }
        /// <summary>
        /// Modifies this cube according to the provided modification.
        /// If this modification contains a rotation the resulting cube will contain the 
        /// rotated original cube. BoundingCubes are always axis aligned.
        /// </summary>
        /// <param name="m">Modification</param>
        public void Modify(ModOp m)
        {
            BoundingCube res = EmptyBoundingCube;
            res.MinMax(m * new GeoPoint(Xmin, Ymin, Zmin));
            res.MinMax(m * new GeoPoint(Xmin, Ymin, Zmax));
            res.MinMax(m * new GeoPoint(Xmin, Ymax, Zmin));
            res.MinMax(m * new GeoPoint(Xmin, Ymax, Zmax));
            res.MinMax(m * new GeoPoint(Xmax, Ymin, Zmin));
            res.MinMax(m * new GeoPoint(Xmax, Ymin, Zmax));
            res.MinMax(m * new GeoPoint(Xmax, Ymax, Zmin));
            res.MinMax(m * new GeoPoint(Xmax, Ymax, Zmax));
            this = res;
        }
        /// <summary>
        /// Returns <c>true</c> if this is the special empty cube
        /// </summary>
        public bool IsEmpty
        {
            get
            {
                return Xmin == System.Double.MaxValue &&
                    Xmax == System.Double.MinValue &&
                    Ymin == System.Double.MaxValue &&
                    Ymax == System.Double.MinValue &&
                    Zmin == System.Double.MaxValue &&
                    Zmax == System.Double.MinValue;
            }
        }

        public bool IsValid
        {
            get
            {
                return !(double.IsNaN(Xmax) || double.IsNaN(Xmin) || double.IsNaN(Ymax) || double.IsNaN(Ymin) || double.IsNaN(Zmax) || double.IsNaN(Zmin) ||
                    double.IsInfinity(Xmax) || double.IsInfinity(Xmin) || double.IsInfinity(Ymax) || double.IsInfinity(Ymin) || double.IsInfinity(Zmax) || double.IsInfinity(Zmin));
            }
        }

        public double DiagonalLength
        {
            get
            {
                return Math.Sqrt(XDiff * XDiff + YDiff * YDiff + ZDiff * ZDiff);
            }
        }

        /// <summary>
        /// Returns <c>true</c> if this cube contains the provided cube. (&lt;= will be checked)
        /// </summary>
        /// <param name="extn">Cube to be checked</param>
        /// <returns><c>true</c> if there is containment</returns>
        public bool Contains(BoundingCube extn)
        {
            return Xmin <= extn.Xmin && Xmax >= extn.Xmax &&
            Ymin <= extn.Ymin && Ymax >= extn.Ymax &&
            Zmin <= extn.Zmin && Zmax >= extn.Zmax;
        }
        /// <summary>
        /// Returns true if this cube contains the provided point. (&lt;= will be checked)
        /// </summary>
        /// <param name="p">The Point</param>
        /// <returns><c>true</c> if there is containment</returns>
        public bool Contains(GeoPoint p)
        {
            return
                Xmin <= p.x && Xmax >= p.x &&
                Ymin <= p.y && Ymax >= p.y &&
                Zmin <= p.z && Zmax >= p.z;
        }
        /// <summary>
        /// Returns true if this cube contains the provided point.
        /// Points within the cube extended by <paramref name="prec"/>ision
        /// will also be accepted.
        /// </summary>
        /// <param name="p">The Point</param>
        /// <returns><c>true</c> if there is containment</returns>
        public bool Contains(GeoPoint p, double prec)
        {
            return
                Xmin - prec <= p.x && Xmax + prec >= p.x &&
                Ymin - prec <= p.y && Ymax + prec >= p.y &&
                Zmin - prec <= p.z && Zmax + prec >= p.z;
        }
        /// <summary>
        /// Returns the extent of the projected BoundingCube
        /// </summary>
        /// <param name="pr">the projection</param>
        /// <returns>the resulting 2-dimensional extent</returns>
        public BoundingRect GetExtent(Projection pr)
        {
            BoundingRect res = BoundingRect.EmptyBoundingRect;
            res.MinMax(pr.WorldToProjectionPlane(new GeoPoint(Xmin, Ymin, Zmin)));
            res.MinMax(pr.WorldToProjectionPlane(new GeoPoint(Xmin, Ymin, Zmax)));
            res.MinMax(pr.WorldToProjectionPlane(new GeoPoint(Xmin, Ymax, Zmin)));
            res.MinMax(pr.WorldToProjectionPlane(new GeoPoint(Xmin, Ymax, Zmax)));
            res.MinMax(pr.WorldToProjectionPlane(new GeoPoint(Xmax, Ymin, Zmin)));
            res.MinMax(pr.WorldToProjectionPlane(new GeoPoint(Xmax, Ymin, Zmax)));
            res.MinMax(pr.WorldToProjectionPlane(new GeoPoint(Xmax, Ymax, Zmin)));
            res.MinMax(pr.WorldToProjectionPlane(new GeoPoint(Xmax, Ymax, Zmax)));
            return res;
        }
        /// <summary>
        /// Returns true if the given plane intersects the this bounding cube. Returns false otherwise.
        /// </summary>
        /// <param name="plane">The plane to test with</param>
        /// <returns>true if intersection</returns>
        public bool Interferes(Plane plane)
        {   // das könnte schneller gehen, wenn man die ModOp sich besorgt und dort eine Methode
            // nur für den Z-Wert einführt
            int s = Math.Sign(plane.ToLocal(new GeoPoint(Xmin, Ymin, Zmin)).z);
            if (s != Math.Sign(plane.ToLocal(new GeoPoint(Xmin, Ymin, Zmax)).z)) return true;
            if (s != Math.Sign(plane.ToLocal(new GeoPoint(Xmin, Ymax, Zmin)).z)) return true;
            if (s != Math.Sign(plane.ToLocal(new GeoPoint(Xmin, Ymax, Zmax)).z)) return true;
            if (s != Math.Sign(plane.ToLocal(new GeoPoint(Xmax, Ymin, Zmin)).z)) return true;
            if (s != Math.Sign(plane.ToLocal(new GeoPoint(Xmax, Ymin, Zmax)).z)) return true;
            if (s != Math.Sign(plane.ToLocal(new GeoPoint(Xmax, Ymax, Zmin)).z)) return true;
            if (s != Math.Sign(plane.ToLocal(new GeoPoint(Xmax, Ymax, Zmax)).z)) return true;
            return false;
        }
        /// <summary>
        /// Returns true if the given line intersects the this bounding cube. Returns false otherwise.
        /// </summary>
        /// <param name="start">Startpoint of the line</param>
        /// <param name="dir">Direction of the line</param>
        /// <param name="maxdist">Maximum allowable distance from the line</param>
        /// <param name="onlyForward">Set to true, if only the positive ray should be cinsidered</param>
        /// <returns></returns>
        public bool Interferes(GeoPoint start, GeoVector dir, double maxdist, bool onlyForward)
        {   // zuerst die Hauptrichtung bestimmen, der Strahl wird an den beiden Flächen in der Hauptrichtung geklippt
            // ACHTUNG: maxdist wird hier nicht ausgewertet
            int mainDirection; // 0: x-Richtung, 1: y-Richtung, 2: z-Richtung
            if (isCube)
            {
                if (Math.Abs(dir.x) > Math.Abs(dir.y))
                {
                    if (Math.Abs(dir.x) > Math.Abs(dir.z)) mainDirection = 0;
                    else mainDirection = 2;
                }
                else
                {
                    if (Math.Abs(dir.y) > Math.Abs(dir.z)) mainDirection = 1;
                    else mainDirection = 2;
                }
            }
            else
            {   // normieren auf einen Würfel. In diesem Fall kann es vorkommen, dass weiter unten durch 0.0 geteilt wird (dir.x)
                // wenn der BoundingCube in eine Richtung keine Ausdehnung hat und die Linie in der gleichen Ebene liegt.
                if (Xmin == Xmax) mainDirection = 0;
                else if (Ymax == Ymin) mainDirection = 1;
                else if (Zmax == Zmin) mainDirection = 2;
                else if (Math.Abs(dir.x / (Xmax - Xmin)) > Math.Abs(dir.y / (Ymax - Ymin)))
                {
                    if (Math.Abs(dir.x / (Xmax - Xmin)) > Math.Abs(dir.z)) mainDirection = 0;
                    else mainDirection = 2;
                }
                else
                {
                    if (Math.Abs(dir.y / (Ymax - Ymin)) > Math.Abs(dir.z / (Zmax - Zmin))) mainDirection = 1;
                    else mainDirection = 2;
                }
            }
            double l0, l1;
            switch (mainDirection)
            {
                case 0: // x-Richtung
                    l0 = (Xmin - start.x) / dir.x;
                    l1 = (Xmax - start.x) / dir.x;
                    break;
                case 1: // y-Richtung
                    l0 = (Ymin - start.y) / dir.y;
                    l1 = (Ymax - start.y) / dir.y;
                    break;
                case 2: // z-Richtung
                    l0 = (Zmin - start.z) / dir.z;
                    l1 = (Zmax - start.z) / dir.z;
                    break;
                default: return false; // damit der Compiler zufrieden ist
            }
            // die beiden Punkte sind Schnittpunkte mit zwei Parallelen Flächen des Würfels.
            if (onlyForward)
            {
                if (l0 < 0.0) l0 = 0.0;
                if (l1 < 0.0) l1 = 0.0;
            }
            GeoPoint sp = start + l0 * dir;
            GeoPoint ep = start + l1 * dir;
            // geht das nicht besser als auf das andere Interferes zurückzugreifen? Wir wissen doch schon, dass sp und ep auf zwei Seiten
            // des Würfels liegen?
            return Interferes(ref sp, ref ep);
        }
        const int ClipLeft = 1;
        const int ClipRight = 2;
        const int ClipFront = 4;
        const int ClipBack = 8;
        const int ClipBottom = 16;
        const int ClipTop = 32;
        const int ClipAll = 63; // alle Bits gesetzt
        private int ClipCode(ref GeoPoint p) // ref wg. Geschwindigkeit
        {
            int res = 0;
            if (p.x < Xmin) res = ClipLeft;
            else if (p.x > Xmax) res = ClipRight;
            if (p.y < Ymin) res |= ClipFront;
            else if (p.y > Ymax) res |= ClipBack;
            if (p.z < Zmin) res |= ClipBottom;
            else if (p.z > Zmax) res |= ClipTop;
            return res;
        }
        /// <summary>
        /// Returns true if the given line segment intersects the this bounding cube. Returns false otherwise.
        /// </summary>
        /// <param name="start">Start point of the line</param>
        /// <param name="end">End point of the line</param>
        /// <returns>True if intersection</returns>
        public bool Interferes(ref GeoPoint start, ref GeoPoint end)
        {
            int c1 = ClipCode(ref start);
            int c2 = ClipCode(ref end);
            if ((c1 & c2) != 0) return false; // beide Punkte auf einer Seite
            if (c1 == 0 || c2 == 0) return true; // ein Punkt innerhalb
            return Interferes(start, end, c1, c2);
        }
        private bool Interferes(GeoPoint start, GeoPoint end, int c1, int c2)
        {
            while ((c1 | c2) != 0)
            {
                GeoPoint p = new GeoPoint();
                int c;
                if (c1 != 0) c = c1; else c = c2;
                if ((ClipLeft & c) != 0)
                {
                    double d = (Xmin - start.x) / (end.x - start.x);
                    p.y = start.y + d * (end.y - start.y);
                    p.z = start.z + d * (end.z - start.z);
                    p.x = Xmin;
                }
                else if ((ClipRight & c) != 0)
                {
                    double d = (Xmax - start.x) / (end.x - start.x);
                    p.y = start.y + d * (end.y - start.y);
                    p.z = start.z + d * (end.z - start.z);
                    p.x = Xmax;
                }
                else if ((ClipFront & c) != 0)
                {
                    double d = (Ymin - start.y) / (end.y - start.y);
                    p.x = start.x + d * (end.x - start.x);
                    p.z = start.z + d * (end.z - start.z);
                    p.y = Ymin;
                }
                else if ((ClipBack & c) != 0)
                {
                    double d = (Ymax - start.y) / (end.y - start.y);
                    p.x = start.x + d * (end.x - start.x);
                    p.z = start.z + d * (end.z - start.z);
                    p.y = Ymax;
                }
                else if ((ClipBottom & c) != 0)
                {
                    double d = (Zmin - start.z) / (end.z - start.z);
                    p.x = start.x + d * (end.x - start.x);
                    p.y = start.y + d * (end.y - start.y);
                    p.z = Zmin;
                }
                else if ((ClipTop & c) != 0)
                {
                    double d = (Zmax - start.z) / (end.z - start.z);
                    p.x = start.x + d * (end.x - start.x);
                    p.y = start.y + d * (end.y - start.y);
                    p.z = Zmax;
                }
                else
                {	// nur um den Compiler zufrieden zu stellen
                    throw new ClipRectException();
                }
                if (c == c1)
                {
                    start = p; c1 = ClipCode(ref p);
                }
                else
                {
                    end = p; c2 = ClipCode(ref p);
                }
                if ((c1 & c2) != 0) return false;
                if (!(c1 != 0 && c2 != 0)) return true;
            }
            return true;
        }
        /// <summary>
        /// Checks the interference of this cube with the provided polyline. The array of <paramref name="points"/>
        /// defines a polyline connecting consecutive points.
        /// </summary>
        /// <param name="points">The polyline</param>
        /// <returns><c>true</c> when some part of the polyline interferes with this cube</returns>
        public bool Interferes(GeoPoint[] points)
        {   // Polygon
            int[] cc = new int[points.Length];
            int c = 0x3F; // alle Bits gesetzt
            for (int i = 0; i < points.Length; ++i)
            {
                int ccc = ClipCode(ref points[i]);
                if (ccc == 0)
                {
                    return true;
                }
                c &= ccc;
                cc[i] = ccc;
            }
            if (c != 0)
            {
                return false; // alle Punkte auf einer Seite
            }
            for (int i = 0; i < points.Length - 1; ++i)
            {
                int c1 = cc[i];
                int c2 = cc[i + 1];
                GeoPoint start = points[i]; // Punkte werden möglicherweise verändert
                GeoPoint end = points[i + 1];
                if ((c1 & c2) != 0) continue; // beide Punkte auf einer Seite, nächste Linie
                while ((c1 | c2) != 0)
                {
                    GeoPoint p = new GeoPoint();
                    if (c1 != 0) c = c1; else c = c2;
                    if ((ClipLeft & c) != 0)
                    {
                        double d = (Xmin - start.x) / (end.x - start.x);
                        p.y = start.y + d * (end.y - start.y);
                        p.z = start.z + d * (end.z - start.z);
                        p.x = Xmin;
                    }
                    else if ((ClipRight & c) != 0)
                    {
                        double d = (Xmax - start.x) / (end.x - start.x);
                        p.y = start.y + d * (end.y - start.y);
                        p.z = start.z + d * (end.z - start.z);
                        p.x = Xmax;
                    }
                    else if ((ClipFront & c) != 0)
                    {
                        double d = (Ymin - start.y) / (end.y - start.y);
                        p.x = start.x + d * (end.x - start.x);
                        p.z = start.z + d * (end.z - start.z);
                        p.y = Ymin;
                    }
                    else if ((ClipBack & c) != 0)
                    {
                        double d = (Ymax - start.y) / (end.y - start.y);
                        p.x = start.x + d * (end.x - start.x);
                        p.z = start.z + d * (end.z - start.z);
                        p.y = Ymax;
                    }
                    else if ((ClipBottom & c) != 0)
                    {
                        double d = (Zmin - start.z) / (end.z - start.z);
                        p.x = start.x + d * (end.x - start.x);
                        p.y = start.y + d * (end.y - start.y);
                        p.z = Zmin;
                    }
                    else if ((ClipTop & c) != 0)
                    {
                        double d = (Zmax - start.z) / (end.z - start.z);
                        p.x = start.x + d * (end.x - start.x);
                        p.y = start.y + d * (end.y - start.y);
                        p.z = Zmax;
                    }
                    else
                    {	// nur um den Compiler zufrieden zu stellen
                        throw new ClipRectException();
                    }
                    if (c == c1)
                    {
                        start = p; c1 = ClipCode(ref p);
                    }
                    else
                    {
                        end = p; c2 = ClipCode(ref p);
                    }
                    if ((c1 & c2) != 0) break; // eigentlich continue der for schleife, geht aber hier nicht, da innerhalb der while schleife
                    if (!(c1 != 0 && c2 != 0))
                    {
                        return true;
                    }
                }
                if ((c1 & c2) != 0) continue; // beide auf einer Seite, nächste Linie
                return true;
            }
            return false; // keiner hat getroffen
        }
        /// <summary>
        /// Returns true when the triangle defined by <paramref name="tri1"/>, <paramref name="tri2"/> and <paramref name="tri3"/>
        /// (including the inside of the triangle) and this BoundingCube interfere.
        /// </summary>
        /// <param name="tri1">First point of triangle</param>
        /// <param name="tri2">Second point of triangle</param>
        /// <param name="tri3">Third point of triangle</param>
        /// <returns>true if interference, false otherwise</returns>
        public bool Interferes(ref GeoPoint tri1, ref GeoPoint tri2, ref GeoPoint tri3)
        {
            int c1 = ClipCode(ref tri1);
            int c2 = ClipCode(ref tri2);
            int c3 = ClipCode(ref tri3);
            if ((c1 & c2 & c3) != 0) return false; // alle drei Punkte auf einer Seite
            if (c1 == 0 || c2 == 0 || c3 == 0) return true; // ein Punkt innerhalb
            return Interferes(ref tri1, ref tri2, ref tri3, c1, c2, c3);
        }
        private bool Interferes(ref GeoPoint tri1, ref GeoPoint tri2, ref GeoPoint tri3, int c1, int c2, int c3)
        {
            // alle auf einer Seite oder einer inerhalb ist hier schon getestet
            // Test, ob eine Dreiecksseite den Würfel schneidetschneidet
            if ((c1 & c2) == 0 && Interferes(tri1, tri2, c1, c2)) return true;
            if ((c2 & c3) == 0 && Interferes(tri2, tri3, c2, c3)) return true;
            if ((c3 & c1) == 0 && Interferes(tri3, tri1, c3, c1)) return true;
            // Jetzt kann noch der Würfel ganz innerhalb des Dreiecks liegen. Dazu werden die Positionen
            // der 8 Eckpunkte bezüglich des vom Dreieck aufgespannten Koordinatensystems betrachtet
            GeoVector dir1 = tri2 - tri1;
            GeoVector dir2 = tri3 - tri1;
            GeoVector dir3 = dir1 ^ dir2;
            // Gleichung tri1 + l*dir1 + m*dir2 + n*dir3 = Eckpunkt vom cube
            Matrix m = DenseMatrix.OfRowArrays(dir1, dir2, dir3);
            Matrix b = DenseMatrix.OfRowArrays(new GeoPoint(Xmin, Ymin, Zmin) - tri1,
                new GeoPoint(Xmax, Ymin, Zmin) - tri1,
                new GeoPoint(Xmin, Ymax, Zmin) - tri1,
                new GeoPoint(Xmax, Ymax, Zmin) - tri1,
                new GeoPoint(Xmin, Ymin, Zmax) - tri1,
                new GeoPoint(Xmax, Ymin, Zmax) - tri1,
                new GeoPoint(Xmin, Ymax, Zmax) - tri1,
                new GeoPoint(Xmax, Ymax, Zmax) - tri1
                );
            Matrix res = (Matrix)m.Transpose().Solve(b.Transpose());
            // wenn zwei Eckpunkte verschiedenes Z haben (im Sinne des Dreiecks, dann könnte die verbindungslinie durchs Dreieck gehen
            if (res.IsValid())
            {
                res = (Matrix)res.Transpose();
                int s1 = Math.Sign(res[0, 2]);
                for (int i = 1; i < 8; ++i)
                {
                    if (Math.Sign(res[i, 2]) != s1)
                    {   // die Verbindung von Eckpunkt 0 mit Eckpunkt i haben verschiedene Vorzeichen, durchqueren also
                        // die Ebene des Dreiecks
                        dir3 = new GeoPoint(b[i, 0], b[i, 1], b[i, 2]) - new GeoPoint(b[0, 0], b[0, 1], b[0, 2]);
                        // hier endet die Schleife, so oder so, die Variablen m, b, res können also recyclet werden
                        m = DenseMatrix.OfColumnArrays(dir1, dir2, dir3);
                        Vector v = new DenseVector(new GeoVector(b[0, 0], b[0, 1], b[0, 2]));
                        Vector resv = (Vector)m.Solve(v);
                        if (resv[0] >= 0.0 && resv[0] <= 1.0 && resv[1] >= 0.0 && resv[1] <= 1.0 && (resv[0] + resv[1]) <= 1.0) return true;
                        return false; // die Linie geht nicht durchs Dreieck, dann sicher Dreieck außerhalb vom cube
                    }
                }
            }
            return false;
        }
        public bool OnSameSide(params GeoPoint[] pnts)
        {
            int c = ClipAll;
            for (int i = 0; i < pnts.Length; i++)
            {
                c &= ClipCode(ref pnts[i]);
                if (c == 0) return false;
            }
            return true;
        }
        /// <summary>
        /// Returns true if the provided tetrahedron (given by the four points tetra1..tetra4) and this
        /// BoundingCube interfere
        /// </summary>
        /// <param name="tetra1">1st tetrahedron vertex</param>
        /// <param name="tetra2">2nd tetrahedron vertex</param>
        /// <param name="tetra3">3rd tetrahedron vertex</param>
        /// <param name="tetra4">4th tetrahedron vertex</param>
        /// <returns>True if objects interfere</returns>
        public bool Interferes(GeoPoint tetra1, GeoPoint tetra2, GeoPoint tetra3, GeoPoint tetra4)
        {
            int c1 = ClipCode(ref tetra1);
            int c2 = ClipCode(ref tetra2);
            int c3 = ClipCode(ref tetra3);
            int c4 = ClipCode(ref tetra4);
            if ((c1 & c2 & c3 & c4) != 0) return false; // alle vier Punkte auf einer Seite
            if (c1 == 0 || c2 == 0 || c3 == 0 || c4 == 0) return true; // ein Punkt innerhalb
            return Interferes(ref tetra1, ref tetra2, ref tetra3, ref tetra4, c1, c2, c3, c4);
        }
        private bool Interferes(ref GeoPoint tetra1, ref GeoPoint tetra2, ref GeoPoint tetra3, ref GeoPoint tetra4, int c1, int c2, int c3, int c4)
        {
            // die 4 Dreiecke testen
            if (Interferes(ref tetra1, ref tetra2, ref tetra3, c1, c2, c3)) return true;
            if (Interferes(ref tetra1, ref tetra2, ref tetra4, c1, c2, c4)) return true;
            if (Interferes(ref tetra1, ref tetra3, ref tetra4, c1, c3, c4)) return true;
            if (Interferes(ref tetra2, ref tetra3, ref tetra4, c2, c3, c4)) return true;
            // bleibt noch die Möglichkeit, dass der Würfel ganz innerhalb liegt
            if (TetraederHull.IsPointInside(new GeoPoint(Xmax, Ymin, Zmin), tetra1, tetra2, tetra3, tetra4)) return true;
            return false;
        }
        /// <summary>
        /// Returns true, if the provided rectangle <paramref name="rect"/> and this BoundingCube interfere with the provided <paramref name="projection"/>.
        /// </summary>
        /// <param name="projection">The projection</param>
        /// <param name="rect">The rectangle</param>
        /// <returns>ture if interference, false otherwise</returns>
        public bool Interferes(Projection projection, BoundingRect rect)
        {
            ClipRect clr = new ClipRect(rect);
            GeoPoint2D[] points2d = new GeoPoint2D[]{
                projection.ProjectUnscaled(new GeoPoint(Xmin, Ymin, Zmin)),
                projection.ProjectUnscaled(new GeoPoint(Xmax, Ymin, Zmin)),
                projection.ProjectUnscaled(new GeoPoint(Xmin, Ymax, Zmin)),
                projection.ProjectUnscaled(new GeoPoint(Xmax, Ymax, Zmin)),
                projection.ProjectUnscaled(new GeoPoint(Xmin, Ymin, Zmax)),
                projection.ProjectUnscaled(new GeoPoint(Xmax, Ymin, Zmax)),
                projection.ProjectUnscaled(new GeoPoint(Xmin, Ymax, Zmax)),
                projection.ProjectUnscaled(new GeoPoint(Xmax, Ymax, Zmax))};
            // DEBUG:
            //GeoObjectList res = new GeoObjectList();
            //Line l = Line.Construct();
            //l.SetTwoPoints(new GeoPoint(Xmin, Ymin, Zmin), new GeoPoint(Xmax, Ymin, Zmin));
            //res.Add(l);
            //l = Line.Construct();
            //l.SetTwoPoints(new GeoPoint(Xmax, Ymin, Zmin), new GeoPoint(Xmax, Ymax, Zmin));
            //res.Add(l);
            //l = Line.Construct();
            //l.SetTwoPoints(new GeoPoint(Xmax, Ymax, Zmin), new GeoPoint(Xmin, Ymax, Zmin));
            //res.Add(l);
            //l = Line.Construct();
            //l.SetTwoPoints(new GeoPoint(Xmin, Ymax, Zmin), new GeoPoint(Xmin, Ymin, Zmin));
            //res.Add(l);
            //l = Line.Construct();
            //l.SetTwoPoints(new GeoPoint(Xmin, Ymin, Zmax), new GeoPoint(Xmax, Ymin, Zmax));
            //res.Add(l);
            //l = Line.Construct();
            //l.SetTwoPoints(new GeoPoint(Xmax, Ymin, Zmax), new GeoPoint(Xmax, Ymax, Zmax));
            //res.Add(l);
            //l = Line.Construct();
            //l.SetTwoPoints(new GeoPoint(Xmax, Ymax, Zmax), new GeoPoint(Xmin, Ymax, Zmax));
            //res.Add(l);
            //l = Line.Construct();
            //l.SetTwoPoints(new GeoPoint(Xmin, Ymax, Zmax), new GeoPoint(Xmin, Ymin, Zmax));
            //res.Add(l);
            //l = Line.Construct();
            //l.SetTwoPoints(new GeoPoint(Xmin, Ymin, Zmin), new GeoPoint(Xmin, Ymin, Zmax));
            //res.Add(l);
            //l = Line.Construct();
            //l.SetTwoPoints(new GeoPoint(Xmax, Ymin, Zmin), new GeoPoint(Xmax, Ymin, Zmax));
            //res.Add(l);
            //l = Line.Construct();
            //l.SetTwoPoints(new GeoPoint(Xmin, Ymax, Zmin), new GeoPoint(Xmin, Ymax, Zmax));
            //res.Add(l);
            //l = Line.Construct();
            //l.SetTwoPoints(new GeoPoint(Xmax, Ymax, Zmin), new GeoPoint(Xmax, Ymax, Zmax));
            //res.Add(l);
            //GeoPoint p1 = projection.UnProjectUnscaled(rect.GetLowerLeft());
            //GeoPoint p2 = projection.UnProjectUnscaled(rect.GetLowerRight());
            //GeoPoint p3 = projection.UnProjectUnscaled(rect.GetUpperLeft());
            //GeoPoint p4 = projection.UnProjectUnscaled(rect.GetUpperRight());
            //l = Line.Construct();
            //l.SetTwoPoints(p1, p2);
            //res.Add(l);
            //l = Line.Construct();
            //l.SetTwoPoints(p2, p3);
            //res.Add(l);
            //l = Line.Construct();
            //l.SetTwoPoints(p3, p4);
            //res.Add(l);
            //l = Line.Construct();
            //l.SetTwoPoints(p4, p1);
            //res.Add(l);
            //p1 = p1 + 100 * projection.Direction;
            //p2 = p2 + 100 * projection.Direction;
            //p3 = p3 + 100 * projection.Direction;
            //p4 = p4 + 100 * projection.Direction;
            //l = Line.Construct();
            //l.SetTwoPoints(p1, p2);
            //res.Add(l);
            //l = Line.Construct();
            //l.SetTwoPoints(p2, p3);
            //res.Add(l);
            //l = Line.Construct();
            //l.SetTwoPoints(p3, p4);
            //res.Add(l);
            //l = Line.Construct();
            //l.SetTwoPoints(p4, p1);
            //res.Add(l);


            // das Rechteck mit der Projektion beschreiben einen "Schacht", ein rechteckiges unendlich langes
            // Prisma. 
            switch (clr.PointsHitTest(points2d))
            {
                case 0: return false; // der Würfel liegt ganz außerhalb des Schachtes
                case 1: return true; // ein Eckpunkt des Würfels liegt innerhalb des Schachtes
                default:
                    // hier bleibt noch: der Schacht geht durch den Würfel, ohne eine Ecke zu beinhalten
                    // oder der Schacht liegt außerhalb, aber so, dass der "0-Test" nicht angeschlagen hat
                    // if (Interferes(projection.UnProjectUnscaled(rect.GetCenter()), projection.Direction, 0.0)) return true;
                    if (Interferes(projection.UnProjectUnscaled(rect.GetLowerLeft()), projection.Direction, 0.0, false)) return true;
                    if (Interferes(projection.UnProjectUnscaled(rect.GetLowerRight()), projection.Direction, 0.0, false)) return true;
                    if (Interferes(projection.UnProjectUnscaled(rect.GetUpperLeft()), projection.Direction, 0.0, false)) return true;
                    if (Interferes(projection.UnProjectUnscaled(rect.GetUpperRight()), projection.Direction, 0.0, false)) return true;
                    // Jetzt gibt es noch den seltenen Fall, dass Schact und Würfel sich zwar berühren, aber kein Eckpunkt
                    // des einen (in 2D) innerhalb der Grenzen des anderen liegt. Deshalb hier noch zusätzlich:
                    BoundingRect ext = new BoundingRect(points2d);
                    if (rect.Width > ext.Width || rect.Height > ext.Height)
                    {
                        BoundingRect common = BoundingRect.Common(rect, ext);
                        if (Interferes(projection.UnProjectUnscaled(common.GetCenter()), projection.Direction, 0.0, false)) return true;
                    }
                    return false;
            }

        }
        /// <summary>
        /// Checks the interference of this cube with the provided <paramref name="area"/>. The pickarea
        /// is either a rectangular prism of infinite length or a frustum.
        /// </summary>
        /// <param name="area"></param>
        /// <returns></returns>
        public bool Interferes(Projection.PickArea area)
        {
            GeoPoint[] points = Points;
            for (int i = 0; i < points.Length; ++i)
            {
                points[i] = area.ToUnitBox * points[i];
            }
            //DEBUG!!!
#if DEBUG
            //for (int i = 0; i < area.Debug.Count; i++)
            //{
            //    Vertex[] vertices = (area.Debug[i] as Face).Vertices;
            //    for (int j = 0; j < vertices.Length; j++)
            //    {
            //        GeoPoint dbg = area.ToUnitBox * vertices[j].Position; // muss nur 0 und 1 enthalten
            //    }
            //    GeoPoint center = new GeoPoint(vertices[0].Position, vertices[1].Position, vertices[2].Position, vertices[3].Position);
            //    GeoPoint dbg1 = area.ToUnitBox * center;
            //}
#endif
            // alle Punkte im unit system
            int co = 63; // alle bits gesetzt
            for (int i = 0; i < points.Length; ++i)
            {
                int pat = 0;
                if (points[i].x < 0) pat |= 1;
                if (points[i].x > 1) pat |= 2;
                if (points[i].y < 0) pat |= 4;
                if (points[i].y > 1) pat |= 8;
                if (points[i].z < 0) pat |= 16;
                if (points[i].z > 1) pat |= 32;
                if (pat == 0) return true; // ein Punkt ganz innerhalb
                co &= pat;
            }
            // die Z-Koordinate ist oft nicht gut, wenn knapp am Rand, deshalb nicht berücksichtigen
            if ((co & 0x000F) != 0) return false; // alle auf einer Seite
            // Fall: die Punkte Points liegen auf allen Seiten, da area mittendurch geht
            // und auch keine Seiten schneidet
            if (this.Interferes(area.FrontCenter, area.Direction, 0.0, true)) return true;
            // schwieriger Fall: nicht alle innerhalb aber auch nicht alle auf einer Seite:
            // hier muss man die Kanten testen
            BoundingCube bc = BoundingCube.UnitBoundingCube;
            int[,] ln = LineNumbers;
            if (bc.Interferes(ref points[ln[0, 0]], ref points[ln[0, 1]])) return true;
            if (bc.Interferes(ref points[ln[1, 0]], ref points[ln[1, 1]])) return true;
            if (bc.Interferes(ref points[ln[2, 0]], ref points[ln[2, 1]])) return true;
            if (bc.Interferes(ref points[ln[3, 0]], ref points[ln[3, 1]])) return true;
            if (bc.Interferes(ref points[ln[4, 0]], ref points[ln[4, 1]])) return true;
            if (bc.Interferes(ref points[ln[5, 0]], ref points[ln[5, 1]])) return true;
            if (bc.Interferes(ref points[ln[6, 0]], ref points[ln[6, 1]])) return true;
            if (bc.Interferes(ref points[ln[7, 0]], ref points[ln[7, 1]])) return true;
            if (bc.Interferes(ref points[ln[8, 0]], ref points[ln[8, 1]])) return true;
            if (bc.Interferes(ref points[ln[9, 0]], ref points[ln[9, 1]])) return true;
            if (bc.Interferes(ref points[ln[10, 0]], ref points[ln[10, 1]])) return true;
            if (bc.Interferes(ref points[ln[11, 0]], ref points[ln[11, 1]])) return true;
            points = bc.Points;
            Matrix4 fromUnitBox = area.ToUnitBox.GetInverse();
            for (int i = 0; i < points.Length; ++i)
            {
                points[i] = fromUnitBox * points[i];
            }
            if (this.Interferes(ref points[ln[0, 0]], ref points[ln[0, 1]])) return true;
            if (this.Interferes(ref points[ln[1, 0]], ref points[ln[1, 1]])) return true;
            if (this.Interferes(ref points[ln[2, 0]], ref points[ln[2, 1]])) return true;
            if (this.Interferes(ref points[ln[3, 0]], ref points[ln[3, 1]])) return true;
            if (this.Interferes(ref points[ln[4, 0]], ref points[ln[4, 1]])) return true;
            if (this.Interferes(ref points[ln[5, 0]], ref points[ln[5, 1]])) return true;
            if (this.Interferes(ref points[ln[6, 0]], ref points[ln[6, 1]])) return true;
            if (this.Interferes(ref points[ln[7, 0]], ref points[ln[7, 1]])) return true;
            if (this.Interferes(ref points[ln[8, 0]], ref points[ln[8, 1]])) return true;
            if (this.Interferes(ref points[ln[9, 0]], ref points[ln[9, 1]])) return true;
            if (this.Interferes(ref points[ln[10, 0]], ref points[ln[10, 1]])) return true;
            if (this.Interferes(ref points[ln[11, 0]], ref points[ln[11, 1]])) return true;
            return false;
        }
        /// <summary>
        /// Checks the interference of this cube with the provided triangles. The triangles are defined
        /// by three points each. Each triple of indices in <paramref name="triangleIndex"/> defines one
        /// triangle. Not only the edges of the triangle are checked but also the inner surface.
        /// </summary>
        /// <param name="trianglePoint">Array of vertives of the triangles</param>
        /// <param name="triangleIndex">Indices to <paramref name="trianglePoint"/>, each triple defines one triangle</param>
        /// <returns><c>true</c> if any of the provides triangles interferes with this cube</returns>
        public bool Interferes(GeoPoint[] trianglePoint, int[] triangleIndex)
        {
            int[] cc = new int[trianglePoint.Length];
            int c = 0x3F; // alle Bits gesetzt
            for (int i = 0; i < trianglePoint.Length; ++i)
            {
                int ccc = ClipCode(ref trianglePoint[i]);
                if (ccc == 0)
                {
                    return true; // ein Eckpunkt innerhalb des Würfels
                }
                c &= ccc;
                cc[i] = ccc;
            }
            if (c != 0)
            {
                return false; // alle Punkte auf einer Seite des Würfels
            }
            for (int i = 0; i < triangleIndex.Length; i += 3)
            {

                int i0 = triangleIndex[i];
                int i1 = triangleIndex[i + 1];
                int i2 = triangleIndex[i + 2];
                if ((cc[i0] & cc[i1] & cc[i2]) == 0)
                {   // wenn nicht alle auf einer Seite
                    if (Interferes(ref trianglePoint[i0], ref trianglePoint[i1], ref trianglePoint[i2], cc[i0], cc[i1], cc[i2]))
                    {
                        return true;
                    }
                }
            }
            return false;
        }
        /// <summary>
        /// Checks the interference of this cube with the provided parallelepiped (the affine projection of a cube)
        /// </summary>
        /// <param name="loc"></param>
        /// <param name="dirx"></param>
        /// <param name="diry"></param>
        /// <param name="dirz"></param>
        /// <returns></returns>
        public bool Interferes(GeoPoint loc, GeoVector dirx, GeoVector diry, GeoVector dirz)
        {
            int[] cc = new int[8];
            int call = ClipAll; // alle bits gesetzt
            GeoPoint locz = loc + dirz;
            GeoPoint[] pp = new GeoPoint[] { loc, loc + dirx, loc + diry, loc + dirx + diry, locz, locz + dirx, locz + diry, locz + dirx + diry };
            for (int i = 0; i < 8; ++i)
            {
                int c = ClipCode(ref pp[i]);
                if (c == 0) return true; // Punkt innerhalb
                call &= c;
                cc[i] = c;
            }
            if (call != 0) return false; // alle auf einer Seite
            // hier also nicht alle auf einer Seite und keiner innerhalb

            //// Dreieckstest für die Seiten evtl. zu aufwendig
            //// also testen wir alle Dreiecke, die Seiten sind
            //// 0132, 4576, 0154, 1375, 2376, 0264
            //int i0, i1, i2, i3;
            //i0 = 0; i1 = 1; i2 = 2; i3 = 3;
            //if ((cc[i0] & cc[i1] & cc[i2]) == 0 && Interferes(pp[i0], pp[i1], pp[i2], cc[i0], cc[i1], cc[i2])) return true;
            //if ((cc[i3] & cc[i1] & cc[i2]) == 0 && Interferes(pp[i3], pp[i1], pp[i2], cc[i3], cc[i1], cc[i2])) return true;
            //i0 = 4; i1 = 5; i2 = 6; i3 = 7;
            //if ((cc[i0] & cc[i1] & cc[i2]) == 0 && Interferes(pp[i0], pp[i1], pp[i2], cc[i0], cc[i1], cc[i2])) return true;
            //if ((cc[i3] & cc[i1] & cc[i2]) == 0 && Interferes(pp[i3], pp[i1], pp[i2], cc[i3], cc[i1], cc[i2])) return true;
            //i0 = 0; i1 = 1; i2 = 4; i3 = 5;
            //if ((cc[i0] & cc[i1] & cc[i2]) == 0 && Interferes(pp[i0], pp[i1], pp[i2], cc[i0], cc[i1], cc[i2])) return true;
            //if ((cc[i3] & cc[i1] & cc[i2]) == 0 && Interferes(pp[i3], pp[i1], pp[i2], cc[i3], cc[i1], cc[i2])) return true;
            //i0 = 1; i1 = 3; i2 = 5; i3 = 7;
            //if ((cc[i0] & cc[i1] & cc[i2]) == 0 && Interferes(pp[i0], pp[i1], pp[i2], cc[i0], cc[i1], cc[i2])) return true;
            //if ((cc[i3] & cc[i1] & cc[i2]) == 0 && Interferes(pp[i3], pp[i1], pp[i2], cc[i3], cc[i1], cc[i2])) return true;
            //i0 = 2; i1 = 3; i2 = 6; i3 = 7;
            //if ((cc[i0] & cc[i1] & cc[i2]) == 0 && Interferes(pp[i0], pp[i1], pp[i2], cc[i0], cc[i1], cc[i2])) return true;
            //if ((cc[i3] & cc[i1] & cc[i2]) == 0 && Interferes(pp[i3], pp[i1], pp[i2], cc[i3], cc[i1], cc[i2])) return true;
            //i0 = 0; i1 = 2; i2 = 4; i3 = 6;
            //if ((cc[i0] & cc[i1] & cc[i2]) == 0 && Interferes(pp[i0], pp[i1], pp[i2], cc[i0], cc[i1], cc[i2])) return true;
            //if ((cc[i3] & cc[i1] & cc[i2]) == 0 && Interferes(pp[i3], pp[i1], pp[i2], cc[i3], cc[i1], cc[i2])) return true;

            // die 12 Seiten testen:
            if ((cc[0] & cc[1]) == 0 && Interferes(pp[0], pp[1], cc[0], cc[1])) return true;
            if ((cc[0] & cc[2]) == 0 && Interferes(pp[0], pp[2], cc[0], cc[2])) return true;
            if ((cc[1] & cc[3]) == 0 && Interferes(pp[1], pp[3], cc[1], cc[3])) return true;
            if ((cc[2] & cc[3]) == 0 && Interferes(pp[2], pp[3], cc[2], cc[3])) return true;
            if ((cc[0] & cc[4]) == 0 && Interferes(pp[0], pp[4], cc[0], cc[4])) return true;
            if ((cc[1] & cc[5]) == 0 && Interferes(pp[1], pp[5], cc[1], cc[5])) return true;
            if ((cc[2] & cc[6]) == 0 && Interferes(pp[2], pp[6], cc[2], cc[6])) return true;
            if ((cc[3] & cc[7]) == 0 && Interferes(pp[3], pp[7], cc[3], cc[7])) return true;
            if ((cc[4] & cc[5]) == 0 && Interferes(pp[4], pp[5], cc[4], cc[5])) return true;
            if ((cc[4] & cc[6]) == 0 && Interferes(pp[4], pp[6], cc[4], cc[6])) return true;
            if ((cc[5] & cc[7]) == 0 && Interferes(pp[5], pp[7], cc[5], cc[7])) return true;
            if ((cc[6] & cc[7]) == 0 && Interferes(pp[6], pp[7], cc[6], cc[7])) return true;

            // nicht alle auf einer Seite, keine Kante schneidet
            // wir drehen den also Spieß um (und achten auf die gleiche Reihenfolge der Punkte)
            Matrix m = DenseMatrix.OfColumnArrays(dirx, diry, dirz);
            Matrix s = (Matrix)m.Solve(DenseMatrix.OfColumnArrays(
                new GeoPoint(Xmin, Ymin, Zmin) - loc,
                new GeoPoint(Xmax, Ymin, Zmin) - loc,
                new GeoPoint(Xmin, Ymax, Zmin) - loc,
                new GeoPoint(Xmax, Ymax, Zmin) - loc,
                new GeoPoint(Xmin, Ymin, Zmax) - loc,
                new GeoPoint(Xmax, Ymin, Zmax) - loc,
                new GeoPoint(Xmin, Ymax, Zmax) - loc,
                new GeoPoint(Xmax, Ymax, Zmax) - loc));
            if (s.IsValid())
            {   // sonst hat das parallelepiped keine z-Ausdehnung
                call = ClipAll; // alle bits gesetzt
                for (int i = 0; i < 8; ++i)
                {
                    pp[i] = new GeoPoint(s[0, i], s[1, i], s[2, i]);
                    int c = UnitBoundingCube.ClipCode(ref pp[i]);
                    if (c == 0) return true; // Punkt innerhalb
                    call &= c;
                    cc[i] = c;
                }
                if (call != 0) return false; // alle auf einer Seite

                // die 12 Seiten testen 
                if ((cc[0] & cc[1]) == 0 && UnitBoundingCube.Interferes(pp[0], pp[1], cc[0], cc[1])) return true;
                if ((cc[0] & cc[2]) == 0 && UnitBoundingCube.Interferes(pp[0], pp[2], cc[0], cc[2])) return true;
                if ((cc[1] & cc[3]) == 0 && UnitBoundingCube.Interferes(pp[1], pp[3], cc[1], cc[3])) return true;
                if ((cc[2] & cc[3]) == 0 && UnitBoundingCube.Interferes(pp[2], pp[3], cc[2], cc[3])) return true;
                if ((cc[0] & cc[4]) == 0 && UnitBoundingCube.Interferes(pp[0], pp[4], cc[0], cc[4])) return true;
                if ((cc[1] & cc[5]) == 0 && UnitBoundingCube.Interferes(pp[1], pp[5], cc[1], cc[5])) return true;
                if ((cc[2] & cc[6]) == 0 && UnitBoundingCube.Interferes(pp[2], pp[6], cc[2], cc[6])) return true;
                if ((cc[3] & cc[7]) == 0 && UnitBoundingCube.Interferes(pp[3], pp[7], cc[3], cc[7])) return true;
                if ((cc[4] & cc[5]) == 0 && UnitBoundingCube.Interferes(pp[4], pp[5], cc[4], cc[5])) return true;
                if ((cc[4] & cc[6]) == 0 && UnitBoundingCube.Interferes(pp[4], pp[6], cc[4], cc[6])) return true;
                if ((cc[5] & cc[7]) == 0 && UnitBoundingCube.Interferes(pp[5], pp[7], cc[5], cc[7])) return true;
            }
            // es gibt in der tat Fälle bei denen sich cube und parallelepiped nicht durchdringen
            // und nicht alle eckpunkte auf einer seite liegen in beiden Betrachtungen. Dann kommen wir hierhin
            return false;
        }
        /// <summary>
        /// Checks the interference of this cube with the other provided cube
        /// </summary>
        /// <param name="bc">the other cube to test with</param>
        /// <returns>true if the two cubes interfere (overlap)</returns>
        public bool Interferes(BoundingCube bc)
        {
            return ((Xmin <= bc.Xmin && Xmax >= bc.Xmin || Xmin > bc.Xmin && Xmin <= bc.Xmax)
                    && (Ymin <= bc.Ymin && Ymax >= bc.Ymin || Ymin > bc.Ymin && Ymin <= bc.Ymax)
                    && (Zmin <= bc.Zmin && Zmax >= bc.Zmin || Zmin > bc.Zmin && Zmin <= bc.Zmax));
        }
        public bool ClipAxis(Axis axis, out GeoPoint startPoint, out GeoPoint endPoint)
        {
            GeoPoint cnt = Geometry.DropPL(this.GetCenter(), axis.Location, axis.Direction);
            startPoint = cnt - this.Size * axis.Direction.Normalized;
            endPoint = cnt + this.Size * axis.Direction.Normalized;
            return ClipLine(ref startPoint, ref endPoint);
        }
        /// <summary>
        /// Clips the provided line (defined by <paramref name="start"/> and <paramref name="end"/>) by this cube.
        /// Returns true if the line interferes with this boundingcube, modifies the start and endpoint in the parameters if clipping occures
        /// </summary>
        /// <param name="start">Startpoint of the line, may be changed upon return</param>
        /// <param name="end">Endpoint of the line, may be changed upon return</param>
        /// <returns>true if the line and this cube interfere</returns>
        public bool ClipLine(ref GeoPoint start, ref GeoPoint end)
        {
            int c1 = ClipCode(ref start);
            int c2 = ClipCode(ref end);
            if ((c1 & c2) != 0) return false; // beide Punkte auf einer Seite
            if (c1 == 0 || c2 == 0) return true; // ein Punkt innerhalb
            while ((c1 | c2) != 0)
            {
                GeoPoint p = new GeoPoint();
                int c;
                if (c1 != 0) c = c1; else c = c2;
                if ((ClipLeft & c) != 0)
                {
                    double d = (Xmin - start.x) / (end.x - start.x);
                    p.y = start.y + d * (end.y - start.y);
                    p.z = start.z + d * (end.z - start.z);
                    p.x = Xmin;
                }
                else if ((ClipRight & c) != 0)
                {
                    double d = (Xmax - start.x) / (end.x - start.x);
                    p.y = start.y + d * (end.y - start.y);
                    p.z = start.z + d * (end.z - start.z);
                    p.x = Xmax;
                }
                else if ((ClipFront & c) != 0)
                {
                    double d = (Ymin - start.y) / (end.y - start.y);
                    p.x = start.x + d * (end.x - start.x);
                    p.z = start.z + d * (end.z - start.z);
                    p.y = Ymin;
                }
                else if ((ClipBack & c) != 0)
                {
                    double d = (Ymax - start.y) / (end.y - start.y);
                    p.x = start.x + d * (end.x - start.x);
                    p.z = start.z + d * (end.z - start.z);
                    p.y = Ymax;
                }
                else if ((ClipBottom & c) != 0)
                {
                    double d = (Zmin - start.z) / (end.z - start.z);
                    p.x = start.x + d * (end.x - start.x);
                    p.y = start.y + d * (end.y - start.y);
                    p.z = Zmin;
                }
                else if ((ClipTop & c) != 0)
                {
                    double d = (Zmax - start.z) / (end.z - start.z);
                    p.x = start.x + d * (end.x - start.x);
                    p.y = start.y + d * (end.y - start.y);
                    p.z = Zmax;
                }
                else
                {	// nur um den Compiler zufrieden zu stellen
                    throw new ClipRectException();
                }
                if (c == c1)
                {
                    start = p; c1 = ClipCode(ref p);
                }
                else
                {
                    end = p; c2 = ClipCode(ref p);
                }
                if ((c1 & c2) != 0) return false;
                if (!(c1 != 0 && c2 != 0)) return true;
            }
            return true;
        }
        /// <summary>
        /// Tests whether the provided point <paramref name="toTest"/> falls on the bounds of this
        /// cube with respect to <paramref name="precision"/>.
        /// </summary>
        /// <param name="toTest">Point to test</param>
        /// <param name="precision">Precision for the test</param>
        /// <returns>True if on bounds, false otherwise</returns>
        public bool IsOnBounds(GeoPoint toTest, double precision)
        {
            return (Xmin - precision <= toTest.x && Xmax + precision >= toTest.x &&
            Ymin - precision <= toTest.y && Ymax + precision >= toTest.y &&
            Zmin - precision <= toTest.z && Zmax + precision >= toTest.z) &&
            !(Xmin + precision < toTest.x && Xmax - precision > toTest.x &&
            Ymin + precision < toTest.y && Ymax - precision > toTest.y &&
            Zmin + precision < toTest.z && Zmax - precision > toTest.z);
        }
        /// <summary>
        /// Returns a <see cref="System.String"/> that represents this instance.
        /// </summary>
        /// <returns>
        /// A <see cref="System.String"/> that represents this instance.
        /// </returns>
        public override string ToString()
        {
            return Xmin.ToString() + "<->" + Xmax.ToString() + ", " + Ymin.ToString() + "<->" + Ymax.ToString() + ", " + Zmin.ToString() + "<->" + Zmax.ToString();

        }
        /// <summary>
        /// Determines whether the specified <see cref="System.Object"/> is equal to this instance.
        /// </summary>
        /// <param name="obj">The <see cref="System.Object"/> to compare with this instance.</param>
        /// <returns>
        /// 	<c>true</c> if the specified <see cref="System.Object"/> is equal to this instance; otherwise, <c>false</c>.
        /// </returns>
        public override bool Equals(object obj)
        {
            if (!(obj is BoundingCube)) return false;
            if (((BoundingCube)obj).Xmin != Xmin) return false;
            if (((BoundingCube)obj).Xmax != Xmax) return false;
            if (((BoundingCube)obj).Ymin != Ymin) return false;
            if (((BoundingCube)obj).Ymax != Ymax) return false;
            if (((BoundingCube)obj).Zmin != Zmin) return false;
            if (((BoundingCube)obj).Zmax != Zmax) return false;
            return true;
        }
        public override int GetHashCode()
        {
            return base.GetHashCode();
        }
        internal BoundingRect Project(Projection projection)
        {
            BoundingRect res = BoundingRect.EmptyBoundingRect;
            res.MinMax(projection.ProjectUnscaled(new GeoPoint(Xmin, Ymin, Zmin)));
            res.MinMax(projection.ProjectUnscaled(new GeoPoint(Xmin, Ymin, Zmax)));
            res.MinMax(projection.ProjectUnscaled(new GeoPoint(Xmin, Ymax, Zmin)));
            res.MinMax(projection.ProjectUnscaled(new GeoPoint(Xmin, Ymax, Zmax)));
            res.MinMax(projection.ProjectUnscaled(new GeoPoint(Xmax, Ymin, Zmin)));
            res.MinMax(projection.ProjectUnscaled(new GeoPoint(Xmax, Ymin, Zmax)));
            res.MinMax(projection.ProjectUnscaled(new GeoPoint(Xmax, Ymax, Zmin)));
            res.MinMax(projection.ProjectUnscaled(new GeoPoint(Xmax, Ymax, Zmax)));
            return res;
        }
        internal BoundingCube Modify(GeoVector translate)
        {
            return new BoundingCube(Xmin + translate.x, Xmax + translate.x, Ymin + translate.y, Ymax + translate.y, Zmin + translate.z, Zmax + translate.z);
        }
        public ICurve[] Clip(ICurve toClip)
        {
            List<double> intersectionParameters = new List<double>();
            intersectionParameters.AddRange(toClip.GetPlaneIntersection(new Plane(Plane.XYPlane, Zmin)));
            intersectionParameters.AddRange(toClip.GetPlaneIntersection(new Plane(Plane.XYPlane, Zmax)));
            intersectionParameters.AddRange(toClip.GetPlaneIntersection(new Plane(Plane.XZPlane, -Ymin))); // the normal of the XZ plane shows in negative y direction (0,-1,0)
            intersectionParameters.AddRange(toClip.GetPlaneIntersection(new Plane(Plane.XZPlane, -Ymax)));
            intersectionParameters.AddRange(toClip.GetPlaneIntersection(new Plane(Plane.YZPlane, Xmin)));
            intersectionParameters.AddRange(toClip.GetPlaneIntersection(new Plane(Plane.YZPlane, Xmax)));
            intersectionParameters.Add(0.0);
            intersectionParameters.Add(1.0);
            intersectionParameters.Sort();
            List<ICurve> res = new List<ICurve>();
            for (int i = 0; i < intersectionParameters.Count - 1; i++)
            {
                if (intersectionParameters[i] >= 0.0 && intersectionParameters[i + 1] <= 1.0 && (intersectionParameters[i + 1] - intersectionParameters[i]) > 1e-6)
                {
                    if (Contains(toClip.PointAt((intersectionParameters[i] + intersectionParameters[i + 1]) / 2.0)))
                    {
                        ICurve trimmed = toClip.Clone();
                        trimmed.Trim(intersectionParameters[i], intersectionParameters[i + 1]);
                        res.Add(trimmed);
                    }
                }
            }
            return res.ToArray();

        }
        internal BoundingCube(IJsonReadStruct data)
        {
            Xmin = data.GetValue<double>();
            Xmax = data.GetValue<double>();
            Ymin = data.GetValue<double>();
            Ymax = data.GetValue<double>();
            Zmin = data.GetValue<double>();
            Zmax = data.GetValue<double>();
            isCube = false;
        }
        public void GetObjectData(IJsonWriteData data)
        {
            data.AddValues(Xmin, Xmax, Ymin, Ymax, Zmin, Zmax);
        }

        public void SetObjectData(IJsonReadData data)
        {
        }
    }
}
