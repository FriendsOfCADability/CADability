using CADability.GeoObject;
using MathNet.Numerics.LinearAlgebra;
using MathNet.Numerics.LinearAlgebra.Factorization;
using MathNet.Numerics.LinearAlgebra.Double;
using System;
using System.Collections;
using System.Runtime.Serialization;

namespace CADability
{

    public class PlaneException : System.ApplicationException
    {
        public enum tExceptionType { ConstructorFailed, IntersectionFailed };
        public tExceptionType ExceptionType;
        public PlaneException(tExceptionType ExceptionType)
        {
            this.ExceptionType = ExceptionType;
        }
    }

    /// <summary>
    /// A reference to a plane. The Plane is implemented as a struct, i.e. a value type.
    /// Sometimes it is necessary to have a parameter or member, which designates a plane
    /// but may be null (when the plane is not yet computed or not valid). This class
    /// works as an object replacement of the struct Plane.
    /// </summary>

    public class PlaneRef
    {
        private Plane plane;
        /// <summary>
        /// Standard and only constructor, wrapping the given Plane.
        /// </summary>
        /// <param name="p">The Plane to wrap</param>
        public PlaneRef(Plane p)
        {
            plane = p;
        }
        /// <summary>
        /// Operator to seamlessly use a PlaneRef as a Plane
        /// </summary>
        /// <param name="pr">The PlaneRef to convert</param>
        /// <returns>The resulting Plane</returns>
        public static implicit operator Plane(PlaneRef pr) { return pr.plane; }
        /// <summary>
        /// Operator to seamlessly use a Plane as a PlaneRef 
        /// </summary>
        /// <param name="pr">The Plane to convert</param>
        /// <returns>The resulting PlaneRef</returns>
        public static implicit operator PlaneRef(Plane p) { return new PlaneRef(p); }
        /// <summary>
        /// Yields a common plane of the cloud of points, if there is one.
        /// If there is no common plane the result may be not optimal (residual is not minimal)
        /// </summary>
        /// <param name="Cloud">the cloud of points</param>
        /// <param name="isCommon">yields true, if all points lie on the plane</param>
        /// <returns>the plane, may be null</returns>
        public static bool FindCommonPlane(GeoPoint[] Cloud, out Plane commonPlane)
        {   // für besseren Algorithmus suche nach "Householder" in open cascade
            // Plane muss halt gemacht werden
            commonPlane = new Plane(Plane.StandardPlane.XYPlane, 0.0);
            if (Cloud.Length < 3) return false;
            SortedList sl = new SortedList(Cloud.Length * (Cloud.Length - 1) / 2);
            for (int i = 0; i < Cloud.Length; ++i)
            {
                for (int j = i + 1; j < Cloud.Length; ++j)
                {
                    sl.Add(Geometry.Dist(Cloud[i], Cloud[j]), i * Cloud.Length + j);
                }
            }
            int found = (int)sl.GetByIndex(sl.Count - 1);
            int ip1 = found / Cloud.Length;
            int ip2 = found % Cloud.Length;
            GeoPoint p1 = Cloud[ip1];
            GeoPoint p2 = Cloud[ip2];
            double mindist = 0.0;
            int ip3 = -1;
            for (int i = 0; i < Cloud.Length; ++i)
            {
                double d = Geometry.DistPL(Cloud[i], p1, p2);
                if (d > mindist)
                {
                    d = mindist;
                    ip3 = i;
                }
            }
            if (mindist > 0.0)
            {
                GeoPoint p3 = Cloud[ip3];
                commonPlane = new Plane(p1, p2, p3);
                for (int i = 0; i < Cloud.Length; ++i)
                {
                    if (!Precision.IsPointOnPlane(Cloud[i], commonPlane)) return false;
                }
                return true;
            }
            return false;
        }
    }

    /// <summary>
    /// A simple plane as a value type. Is used e.g. as a drawing plane, an intersection plane etc.
    /// It also serves as a coordinate system (see <see cref="CoordSys"/>).
    /// </summary>
    [Serializable]
    [JsonVersion(serializeAsStruct = true, version = 1)]
    public struct Plane : ISerializable, IJsonSerialize
    {
        // Hauptdaten:
        private CoordSys coordSys; // dieses beschreibt die Ebene durch Location, DirectionX, DirectionY
                                   // redundante Daten (nicht speichern)
        /// <summary>
        /// Creates a new plane with the given parameters. Throws a <see cref="PlaneException"/>, if <paramref name="DirectionX"/>
        /// and <paramref name="DirectionY"/> have the same direction.
		/// </summary>
		/// <param name="Location">location of the plane</param>
		/// <param name="DirectionX">direction of the x-axis</param>
		/// <param name="DirectionY">direction of the y-axis, will be adapted if not perpendicular to the x-axis</param>
        public Plane(GeoPoint Location, GeoVector DirectionX, GeoVector DirectionY)
        {
            try
            {
                coordSys = new CoordSys(Location, DirectionX, DirectionY);
            }
            catch (CoordSysException)
            {
                throw new PlaneException(PlaneException.tExceptionType.ConstructorFailed);
            }
        }
        /// <summary>
        /// Creates a new plane with the given parameters. Throws a <see cref="PlaneException"/>, if 
        /// the three points are colinear
        /// </summary>
        /// <param name="location">location of the plane</param>
        /// <param name="p1">specifies the direction of the x-axis</param>
        /// <param name="p2">specifies the direction of the y-axis, wich will be perpendicular to the x-axis</param>
		public Plane(GeoPoint location, GeoPoint p1, GeoPoint p2)
        {
            try
            {
                coordSys = new CoordSys(location, p1 - location, p2 - location);
            }
            catch (CoordSysException)
            {
                throw new PlaneException(PlaneException.tExceptionType.ConstructorFailed);
            }
        }
        /// <summary>
        /// Creates a new plane. The parameter data is under-determined for the plane, so the x-axis and y-axis
        /// will be determined arbitrarily 
        /// </summary>
        /// <param name="location">location of the plane</param>
        /// <param name="normal">normal vector of the plane</param>
		public Plane(GeoPoint location, GeoVector normal)
        {   // this is arbitrary
            if (normal.IsNullVector()) throw new PlaneException(PlaneException.tExceptionType.ConstructorFailed);
            try
            {
                if (normal.x == 0 && normal.y == 0)
                {   // two very common cases
                    if (normal.z > 0) coordSys = new CoordSys(location, GeoVector.XAxis, GeoVector.YAxis);
                    else coordSys = new CoordSys(location, GeoVector.YAxis, GeoVector.XAxis);
                }
                else
                {
                    GeoVector dx;
                    if (Math.Abs(normal.x) > Math.Abs(normal.y))
                    {
                        if (Math.Abs(normal.y) < Math.Abs(normal.z))
                        {   // y is the smalles component, so use the y axis
                            dx = GeoVector.YAxis;
                        }
                        else
                        {
                            dx = GeoVector.ZAxis;
                        }
                    }
                    else
                    {
                        if (Math.Abs(normal.x) < Math.Abs(normal.z))
                        {   // x is the smalles component, so use the x axis
                            dx = GeoVector.XAxis;
                        }
                        else
                        {
                            dx = GeoVector.ZAxis;
                        }
                    }
                    GeoVector v = dx ^normal;
                    coordSys = new CoordSys(location, v ^ normal, v);
#if DEBUG
                    if (!Precision.SameNotOppositeDirection(coordSys.Normal, normal)) { }
#endif
                }
            }
            catch (CoordSysException)
            {
                throw new PlaneException(PlaneException.tExceptionType.ConstructorFailed);
            }
        }
        public Plane(Plane basePlane, double offset)
        {
            coordSys = new CoordSys(basePlane.Location + offset * basePlane.Normal, basePlane.DirectionX, basePlane.DirectionY);
        }
        public Plane(Plane basePlane)
        {
            coordSys = new CoordSys(basePlane.Location, basePlane.DirectionX, basePlane.DirectionY);
        }
        /// <summary>
        /// Finds a plane that best fits through the given points. Calculates also the maximum distance
        /// of the points from that plane. If <paramref name="MaxDistance"/> is 0.0 or small, the points are coplanar.
        /// </summary>
        /// <param name="Points">points to build the plane from</param>
        /// <param name="MaxDistance">maximum distance of the points from the plane</param>
        /// <returns>the plane</returns>
        public static Plane FromPoints(GeoPoint[] points, out double maxDistance, out bool isLinear)
        {
            maxDistance = double.MaxValue;
            isLinear = false;
            if (points.Length < 3)
            {
                isLinear = true;
                return Plane.XYPlane; // there is no "invalid plane"
            }

            Matrix A = new DenseMatrix(points.Length, 3);
            Vector B = new DenseVector(points.Length);
            for (int i = 0; i < points.Length; i++)
            {
                A[i, 0] = points[i].x; // move the points to (x,y,z)>(1,1,1)
                A[i, 1] = points[i].y;
                A[i, 2] = points[i].z;
                B[i] = -1;
            }
            GeoVector translation = GeoVector.NullVector;
            Matrix AAT = (Matrix)(A.Transpose().Multiply(A));
            if (AAT.Rank() < 3)
            {   // this might be the case when the optimal plane geos through the origin
                // then we need to move the point cloud
                // a goo direction seems to be the axis direction where the extent is smallest
                BoundingCube ext = new BoundingCube(points.ToArray());
                int ind;
                if (ext.XDiff < ext.YDiff && ext.XDiff < ext.ZDiff) ind = 0;
                else if (ext.YDiff < ext.XDiff && ext.YDiff < ext.ZDiff) ind = 1;
                else ind = 2;
                double sz = ext.Size;
                translation[ind] += sz;
                for (int i = 0; i < points.Length; i++)
                {
                    A[i, ind] += sz;
                }
                AAT = (Matrix)(A.Transpose().Multiply(A));
                if (AAT.Rank() < 3)
                {
                    isLinear = true;
                    return Plane.XYPlane;
                }
            }
            Vector res = (Vector)AAT.Solve(A.Transpose().Multiply(B));
            if (res.IsValid())
            {
                //double err = 0.0;
                //if (translation.IsNullVector())
                //{
                //    for (int i = 0; i < points.Length; i++)
                //    {
                //        err += sqr(res[0] * points[i].x + res[1] * points[i].y + res[2] * points[i].z + 1);
                //    }
                //}
                //else
                //{
                //    for (int i = 0; i < points.Length; i++)
                //    {
                //        err += sqr(res[0] * (points[i].x + translation.x) + res[1] * (points[i].y + translation.y) + res[2] * (points[i].z + translation.z) + 1);
                //    }
                //}
                //maxDistance = Math.Sqrt(err);
                double l = -1.0 / (res[0] * res[0] + res[1] * res[1] + res[2] * res[2]);
                GeoPoint axisLocation = new GeoPoint(l * res[0], l * res[1], l * res[2]);
                Plane plane = new Plane(axisLocation - translation, (new GeoVector(res[0], res[1], res[2])).Normalized);
                maxDistance = 0.0;
                for (int i = 0; i < points.Length; i++)
                {
                    double d = Math.Abs(plane.Distance(points[i]));
                    if (d > maxDistance) maxDistance = d;
                }
                return plane;
            }
            isLinear = true;
            return Plane.XYPlane;
        }
        private static double sqr(double d) { return d * d; }
        public static Plane FromPointsOld(GeoPoint[] Points, out double MaxDistance, out bool isLinear)
        {
            // nach http://www.ilikebigbits.com/blog/2015/3/2/plane-from-points
            // nicht unbedingt das optimale Ergebnis :"this method will minimize the squares of the residuals as perpendicular to the main axis, 
            // not the residuals perpendicular to the plane." Sonst wohl besser: https://math.stackexchange.com/questions/99299/best-fitting-plane-given-a-set-of-points
#if DEBUG
            // Plane dbgsvd = FromPointsSVD(Points, out MaxDistance, out isLinear); // bringt nichts!
            // double dbgerr = GaussNewtonMinimizer.PlaneFit(Points, out Plane dbgpln);
            //if (Points.Length > 4)
            //{
            //    bool lmok = BoxedSurfaceExtension.PlaneFit(Points, out GeoPoint lmLoc, out GeoVector lmNormal);
            //}
#endif
            if (Points.Length < 3)
            {
                isLinear = true;
                MaxDistance = double.MaxValue;
                return Plane.XYPlane;
            }
            isLinear = false;
            MaxDistance = double.MaxValue;
            GeoPoint centroid = new GeoPoint(Points);
            BoundingCube ext = new BoundingCube(Points);
            if (ext.Size == 0.0)
            {
                isLinear = true;
                MaxDistance = double.MaxValue;
                return Plane.XYPlane;
            }
            double xx = 0.0; double xy = 0.0; double xz = 0.0;
            double yy = 0.0; double yz = 0.0; double zz = 0.0;

            for (int i = 0; i < Points.Length; i++)
            {
                GeoVector r = Points[i] - centroid;
                xx += r.x * r.x;
                xy += r.x * r.y;
                xz += r.x * r.z;
                yy += r.y * r.y;
                yz += r.y * r.z;
                zz += r.z * r.z;
            }

            double det_x = yy * zz - yz * yz;
            double det_y = xx * zz - xz * xz;
            double det_z = xx * yy - xy * xy;

            double det_max = Math.Max(Math.Max(det_x, det_y), det_z);
            if (det_max < ext.Size * 1e-13) // this is not a good condition
            {
                double prec = Geometry.LineFit(Points, out GeoPoint lpos, out GeoVector ldir);
                if (prec < ext.Size * 1e-6)
                {
                    isLinear = true;
                    return Plane.XYPlane;
                }
                try
                {
                    prec = BoxedSurfaceExtension.LineFit(Points, ext.Size * 1e-6, out lpos, out ldir);
                    // prec = GaussNewtonMinimizer.LineFit(Points.ToIArray(), ext.Size * 1e-6, out lpos, out ldir);
                    if (prec < ext.Size * 1e-6)
                    {
                        isLinear = true;
                        return Plane.XYPlane;
                    }
                }
                catch  { }
                // there must be a better way than this!
                double mindist = double.MaxValue;
                GeoVector dir=GeoVector.NullVector;
                GeoPoint loc=GeoPoint.Origin;
                for (int i = 0; i < Points.Length; i++)
                {
                    for (int j = i+1; j < Points.Length; j++)
                    {
                        GeoVector tdir = Points[i] - Points[j];
                        if (tdir.Length<mindist)
                        {
                            mindist = tdir.Length;
                            loc = Points[j];
                            dir = tdir;
                        }
                    }
                }
                prec = 0;
                for (int i = 0; i < Points.Length; i++)
                {
                    double d = Geometry.DistPL(Points[i], loc, dir);
                    if (d > prec) prec = d;
                }
                if (prec < ext.Size * 1e-4)
                {
                    isLinear = true;
                    return Plane.XYPlane;
                }
            }
            GeoVector normal;
            if (det_x == det_max)
            {
                normal = new GeoVector(1, (xz * yz - xy * zz) / det_x, (xy * yz - xz * yy) / det_x);
            }
            else if (det_max == det_y)
            {
                normal = new GeoVector((yz * xz - xy * zz) / det_y, 1, (xy * xz - yz * xx) / det_y);
            }
            else
            {
                normal = new GeoVector((yz * xy - xz * yy) / det_z, (xz * xy - yz * xx) / det_z, 1);
            }
#if DEBUG
            //MaxDistance = 0.0;
            //for (int i = 0; i < Points.Length; ++i)
            //{
            //    double d = Math.Abs(dbgpln.Distance(Points[i]));
            //    if (d > MaxDistance) MaxDistance = d;
            //}
#endif
            Plane res = new Plane(centroid, normal);
            MaxDistance = 0.0;
            double error = 0.0;
            for (int i = 0; i < Points.Length; ++i)
            {
                double d = Math.Abs(res.Distance(Points[i]));
                if (d > MaxDistance) MaxDistance = d;
                error += d * d;
            }
            return res;
        }
#if DEBUG
        public
#else
        internal 
#endif
        static Plane FromPointsSVD(GeoPoint[] Points, out double MaxDistance, out bool isLinear)
        {
            isLinear = false;
            MaxDistance = double.MaxValue;
            GeoPoint centroid = new GeoPoint(Points);
            Matrix m = DenseMatrix.OfArray(new double[Points.Length, 3]);
            for (int i = 0; i < Points.Length; i++)
            {
                GeoVector v = Points[i] - centroid;
                m[i, 0] = v.x;
                m[i, 1] = v.y;
                m[i, 2] = v.z;
            }
            try
            {
                Svd<double> svd = m.Svd();
                GeoVector normal = new GeoVector(svd.U[0, 0], svd.U[0, 1], svd.U[0, 2]);
                return new Plane(centroid, normal);
            }
            catch
            {
                return Plane.XYPlane;
            }
        }

        /// <summary>
        /// Enumeration of the standard planes
        /// </summary>
		public enum StandardPlane
        {
            /// <summary>
            /// X/Y-Plane
            /// </summary>
            XYPlane,
            /// <summary>
            /// X/Z-Plane
            /// </summary>
            XZPlane,
            /// <summary>
            /// Y/Z-Plane
            /// </summary>
            YZPlane
        }
        /// <summary>
        /// Creates a new plane parallel to a <see cref="StandardPlane"/> with a given offset
        /// </summary>
        /// <param name="std">the standard plane</param>
        /// <param name="offset">the offset to the standard plane</param>
        public Plane(StandardPlane std, double offset)
        {
            switch (std)
            {
                default:
                case StandardPlane.XYPlane:
                    coordSys = new CoordSys(new GeoPoint(0.0, 0.0, offset), new GeoVector(1.0, 0.0, 0.0), new GeoVector(0.0, 1.0, 0.0));
                    break;
                case StandardPlane.XZPlane:
                    coordSys = new CoordSys(new GeoPoint(0.0, offset, 0.0), new GeoVector(1.0, 0.0, 0.0), new GeoVector(0.0, 0.0, 1.0));
                    break;
                case StandardPlane.YZPlane:
                    coordSys = new CoordSys(new GeoPoint(offset, 0.0, 0.0), new GeoVector(0.0, 1.0, 0.0), new GeoVector(0.0, 0.0, 1.0));
                    break;
            }
        }
        /// <summary>
        /// Returns the X/Y plane.
        /// </summary>
		public static readonly Plane XYPlane = new Plane(StandardPlane.XYPlane, 0.0);
        /// <summary>
        /// Returns the X/Z plane.
        /// </summary>
        public static readonly Plane XZPlane = new Plane(StandardPlane.XZPlane, 0.0);
        /// <summary>
        /// Returns the Y/Z plane.
        /// </summary>
        public static readonly Plane YZPlane = new Plane(StandardPlane.YZPlane, 0.0);
        public bool Intersect(GeoPoint LinePoint, GeoVector LineDir, out GeoPoint ip)
        {

            LineDir.Norm();
            //double ll = (coordSys.Normal*(coordSys.Location-LinePoint))/(coordSys.Normal*LineDir);
            //// return LinePoint + l*LineDir;
            ModOp toloc = this.coordSys.GlobalToLocal;
            GeoPoint sp = toloc * LinePoint;
            GeoVector dir = toloc * LineDir;

            if (Math.Abs(dir.z) < dir.Length * 1e-10)
            {
                ip = GeoPoint.Origin;
                return false;
            }
            double l = -sp.z / dir.z; // Exception wenn dir.z==0.0
                                      //GeoPoint dbg = LinePoint + l*LineDir;
            ip = LinePoint + l * LineDir;
            return true;
        }
        /// <summary>
        /// Returns the intersection plane of the line given by the parameters with this plane.
        /// </summary>
        /// <param name="LinePoint">point on the line</param>
        /// <param name="LineDir">direction of the line</param>
        /// <returns></returns>
        public GeoPoint Intersect(GeoPoint LinePoint, GeoVector LineDir)
        {
            LineDir.Norm();
            //double ll = (coordSys.Normal*(coordSys.Location-LinePoint))/(coordSys.Normal*LineDir);
            //// return LinePoint + l*LineDir;
            ModOp toloc = this.coordSys.GlobalToLocal;
            GeoPoint sp = toloc * LinePoint;
            GeoVector dir = toloc * LineDir;
            //if (dir.z == 0.0) throw new DivideByZeroException();
            if (dir.z == 0.0) throw new PlaneException(PlaneException.tExceptionType.IntersectionFailed);
            double l = -sp.z / dir.z; // Exception wenn dir.z==0.0
                                      //GeoPoint dbg = LinePoint + l*LineDir;
            return LinePoint + l * LineDir;
            // siehe http://mathworld.wolfram.com/Line-PlaneIntersection.html
            //			double [,] m1 = new double[4,4];
            //			m1[0,0] = 1;
            //			m1[0,1] = 1;
            //			m1[0,2] = 1;
            //			m1[0,3] = 1;
            //			m1[1,0] = coordSys.Location.x;
            //			m1[1,1] = coordSys.Location.x + coordSys.DirectionX.x;
            //			m1[1,2] = coordSys.Location.x + coordSys.DirectionY.x;
            //			m1[1,3] = LinePoint.x;
            //			m1[2,0] = coordSys.Location.y;
            //			m1[2,1] = coordSys.Location.y + coordSys.DirectionX.y;
            //			m1[2,2] = coordSys.Location.y + coordSys.DirectionY.y;
            //			m1[2,3] = LinePoint.y;
            //			m1[2,0] = coordSys.Location.z;
            //			m1[2,1] = coordSys.Location.z + coordSys.DirectionX.z;
            //			m1[2,2] = coordSys.Location.z + coordSys.DirectionY.z;
            //			m1[2,3] = LinePoint.z;
            //			double [,] m2 = new double[4,4];
            //			m2[0,0] = 1;
            //			m2[0,1] = 1;
            //			m2[0,2] = 1;
            //			m2[0,3] = 0;
            //			m2[1,0] = coordSys.Location.x;
            //			m2[1,1] = coordSys.Location.x + coordSys.DirectionX.x;
            //			m2[1,2] = coordSys.Location.x + coordSys.DirectionY.x;
            //			m2[1,3] = LineDir.x;
            //			m2[2,0] = coordSys.Location.y;
            //			m2[2,1] = coordSys.Location.y + coordSys.DirectionX.y;
            //			m2[2,2] = coordSys.Location.y + coordSys.DirectionY.y;
            //			m2[2,3] = LineDir.y;
            //			m2[2,0] = coordSys.Location.z;
            //			m2[2,1] = coordSys.Location.z + coordSys.DirectionX.z;
            //			m2[2,2] = coordSys.Location.z + coordSys.DirectionY.z;
            //			m2[2,3] = LineDir.z;
            //			double l = -Geometry.Determinant(m1)/Geometry.Determinant(m2);
            //			return LinePoint + l*LineDir;
        }
        public bool Intersect(Plane other, out GeoPoint loc, out GeoVector dir)
        {
            dir = this.Normal ^ other.Normal;
            if (Precision.IsNullVector(dir))
            {
                loc = GeoPoint.Origin;
                dir = GeoVector.XAxis;
                return false;
            }
            GeoVector ldir = dir ^ Normal;
            loc = other.Intersect(this.Location, ldir);
            return true;
        }

        public GeoPoint[] Interfere(GeoPoint sp, GeoPoint ep)
        {
            GeoPoint ip;
            if (Intersect(sp, ep - sp, out ip))
            {
                double d1 = (ep - sp).Length;
                double d2 = (ip - sp).Length;
                double d3 = (ep - ip).Length;
                if (d2 < d1 && d3 < d1)
                {
                    GeoPoint[] erg = new GeoPoint[1];
                    erg[0] = ip;
                    return erg;
                }
            }
            return new GeoPoint[0];
        }

        public bool Elem(GeoPoint g)
        {
            return (Math.Abs(Distance(g)) < Precision.eps);
        }
        /// <summary>
        /// Returns the signed distance of the point from the plane. The direction of the normal vector
        /// of the plane determins the sign of the result.
        /// </summary>
        /// <param name="p">the point</param>
        /// <returns>the distance</returns>
        public double Distance(GeoPoint p)
        {
            return coordSys.Normal * (p - coordSys.Location);
        }
        /// <summary>
        /// Gets or sets the location of this plane
        /// </summary>
        public GeoPoint Location
        {
            get
            {
                return coordSys.Location;
            }
            set
            {
                coordSys.Location = value;
            }
        }
        /// <summary>
        /// Gets or sets the normal vector of this plane. Setting the normal vector results in a 
        /// recalculation of the x-axis and y-axis of this plane
        /// </summary>
		public GeoVector Normal
        {
            get { return coordSys.Normal; }
            set
            {
                GeoVector dx = value ^ coordSys.DirectionY;
                GeoVector dirx, diry;
                if (Precision.IsNullVector(dx))
                {   // directionY ist colinear zu normal
                    diry = value ^ coordSys.DirectionX;
                    dirx = value ^ coordSys.DirectionY;
                }
                else
                {
                    dirx = dx;
                    diry = value ^ coordSys.DirectionX;
                }
                try
                {
                    coordSys = new CoordSys(coordSys.Location, dirx, diry);
                }
                catch (CoordSysException)
                {
                    throw new PlaneException(PlaneException.tExceptionType.ConstructorFailed);
                }
            }
        }
        /// <summary>
        /// Gets or sets the direction of the x-axis of this plane. Setting the x-axis results in a 
        /// reculculation of the y-axis to make the axis perpendicular.
        /// </summary>
		public GeoVector DirectionX
        {
            get
            {
                return coordSys.DirectionX;
            }
            set
            {
                GeoVector dirx = value;
                dirx.Norm();
                GeoVector diry = coordSys.Normal ^ dirx;
                coordSys = new CoordSys(coordSys.Location, dirx, diry);
            }
        }
        /// <summary>
        /// Gets or sets the direction of the y-axis of this plane. Setting the y-axis results in a 
        /// reculculation of the x-axis to make the axis perpendicular.
        /// </summary>
        public GeoVector DirectionY
        {
            get
            {
                return coordSys.DirectionY;
            }
            set
            {   // noch nicht klar, ob die Reihenfolge beim ^ stimmt, oder die Orientierung hier umkehrt (soll sie nicht)
                GeoVector diry = value;
                diry.Norm();
                GeoVector dirx = coordSys.Normal ^ diry;
                coordSys = new CoordSys(coordSys.Location, dirx, diry);
            }
        }
        /// <summary>
        /// An uninitialized plane is invalid, because its normal vector is (0,0,0).
        /// </summary>
        /// <returns></returns>
        public bool IsValid()
        {
            return !coordSys.Normal.IsNullVector();
        }
        /// <summary>
        /// Returns the coordinate system corresponding to this plane. The z-axis of the coordinate system 
        /// is the normal vector of this plane (always right handed)
        /// </summary>
        public CoordSys CoordSys
        {
            get { return coordSys; }
        }
        /// <summary>
        /// Aligns the <see cref="DirectionX"/> and <see cref="DirectionY"/> vectors of this plane
        /// so that the projection of DirectionX of AlignTo and DircetionX of this plane are parallel.
        /// If the two planes are parallel, the DirectionX and DirectionY of both planes will
        /// also be parallel. The plane will not be changed. The <see cref="Location"/> of this
        /// plane will be changed to a point closest to the location of AlignTo, if relocate
        /// is true, otherwise the location remains unchanged.
        /// </summary>
        /// <param name="AlignTo">Plane to align to</param>
        /// <param name="relocate">relocate this plane</param>
        public void Align(Plane alignTo, bool relocate)
        {
            GeoVector x0 = ToGlobal(Project(alignTo.DirectionX));
            if (Precision.IsNullVector(x0))
            {   // die x-Achse von AlignTo steht senkrecht auf dieser Ebene
                x0 = ToGlobal(Project(alignTo.DirectionY)); // das kann nicht auch noch senkrecht sein
            }
            x0.Norm();
            GeoVector y0 = coordSys.Normal ^ x0;
            coordSys = new CoordSys(coordSys.Location, x0, y0);
            if (relocate)
            {
                coordSys.Location = ToGlobal(Project(alignTo.Location));
            }
        }
        /// <summary>
        /// Aligns the <see cref="DirectionX"/> and <see cref="DirectionY"/> vectors of this plane
        /// so that the projection of DirectionX of AlignTo and DircetionX of this plane are parallel.
        /// If the two planes are parallel, the DirectionX and DirectionY of both planes will
        /// also be parallel. The plane will not be changed. The <see cref="Location"/> of this
        /// plane will be changed to a point closest to the location of AlignTo, if relocate
        /// is true, otherwise the location remains unchanged. If <paramref name="flip"/> is true
        /// there is an additional check whether the angle between the two normal vectors is less than
        /// 90°. If not, the y-axis is reversed and the angle between the two normal vectors will be
        /// less than 90°
        /// </summary>
        /// <param name="alignTo">Plane to align to</param>
        /// <param name="relocate">relocate this plane</param>
        /// <param name="flip"></param>
        public void Align(Plane alignTo, bool relocate, bool flip)
        {
            Align(alignTo, relocate);
            if (flip)
            {
                if (new Angle(this.Normal, alignTo.Normal) > Math.PI / 2.0)
                {
                    this.coordSys = new CoordSys(this.Location, this.DirectionX, -this.DirectionY);
                }
            }
        }
        public void Reverse()
        {
            coordSys.Reverse();
        }
        /// <summary>
        /// Projects the given point (perpendicular) onto this plane and returns the two-dimensional
        /// point as expressed in the coordinate system of this plane.
        /// </summary>
        /// <param name="p">the point</param>
        /// <returns>the point in the coordinate system of this plane</returns>
        public GeoPoint2D Project(GeoPoint p)
        {
            return coordSys.GlobalToLocal.Project(p);
        }
        /// <summary>
        /// Projects the given point (perpendicular) onto this plane and returns the two-dimensional
        /// point as expressed in the coordinate system of this plane.
        /// </summary>
        /// <param name="p">the point</param>
        /// <returns>the point in the coordinate system of this plane</returns>
        public GeoPoint2D[] Project(GeoPoint[] p)
        {
            GeoPoint2D[] res = new GeoPoint2D[p.Length];
            for (int i = 0; i < p.Length; i++)
            {
                res[i] = coordSys.GlobalToLocal.Project(p[i]);
            }
            return res;
        }
        /// <summary>
        /// Projects the given vector (perpendicular) onto this plane and returns the two-dimensional
        /// vector as expressed in the coordinate system of this plane.
        /// </summary>
        /// <param name="v">the vector</param>
        /// <returns>the vector in the coordinate system of this plane</returns>
        public GeoVector2D Project(GeoVector v)
        {
            return coordSys.GlobalToLocal.Project(v);
        }
        /// <summary>
        /// Returns the point as expressed in the coordinate system of this plane
        /// </summary>
        /// <param name="p">point (in the global system)</param>
        /// <returns>point in the local system of this plane</returns>
        public GeoPoint ToLocal(GeoPoint p)
        {
            return coordSys.GlobalToLocal * p;
        }
        /// <summary>
        /// Returns the vector as expressed in the coordinate system of this plane
        /// </summary>
        /// <param name="v">vector (in the global system)</param>
        /// <returns>vector in the local system of this plane</returns>
        public GeoVector ToLocal(GeoVector v)
        {
            return coordSys.GlobalToLocal * v;
        }
        /// <summary>
        /// Returns the plane given in parameter p expressed in the coordinate system of this plane
        /// </summary>
        /// <param name="p">plane in global system</param>
        /// <returns>plane in local system</returns>
        public Plane ToLocal(Plane p)
        {
            return new Plane(ToLocal(p.Location), ToLocal(p.DirectionX), ToLocal(p.DirectionY));
        }

        /// <summary>
        /// Inverse to the appropriate <see cref="Project(GeoPoint)"/> method. Returns the point in the 
        /// global coordinate system
        /// </summary>
        /// <param name="p">2d point in the local system of this plane</param>
        /// <returns>3d point in the global coordinate system</returns>
        public GeoPoint ToGlobal(GeoPoint2D p)
        {
            return Location + p.x * DirectionX + p.y * DirectionY;
        }
        /// <summary>
        /// Inverse to the appropriate <see cref="ToLocal(GeoPoint)"/> method. The given point 
        /// is assumed in the coordinate ststem of this plane.
        /// Returns the point in the  global coordinate system.
        /// </summary>
        /// <param name="p">point in the plane coordinate system</param>
        /// <returns>point in the global coordinate system</returns>
        public GeoPoint ToGlobal(GeoPoint p)
        {
            return Location + p.x * DirectionX + p.y * DirectionY + p.z * Normal;
        }
        public ModOp ModOpToGlobal
        {
            get
            {
                return new ModOp(DirectionX, DirectionY, Normal, Location);
            }
        }
        /// <summary>
        /// Inverse to the appropriate <see cref="Project(GeoVector)"/> method. Returns the vector in the 
        /// global coordinate system
        /// </summary>
        /// <param name="v">2d vector in the coordinate system of the plane</param>
        /// <returns>3d vector in the global coordinate system</returns>
		public GeoVector ToGlobal(GeoVector2D v)
        {
            return v.x * DirectionX + v.y * DirectionY;
        }
        /// <summary>
        /// Returns the 3D GeoVector corresponding to the given GeoVector in the coordinate 
        /// system of the plane.
        /// </summary>
        /// <param name="v">a vector in the coordinate system of the plane</param>
        /// <returns>a 3D vector in the global coordinate system</returns>
        public GeoVector ToGlobal(GeoVector v)
        {
            return v.x * DirectionX + v.y * DirectionY + v.z * Normal;
        }
        /// <summary>
        /// Returns a plane parallel to this plane with the given offset
        /// </summary>
        /// <param name="dist">the offset</param>
        /// <returns>the parallel plane</returns>
        public Plane Offset(double dist)
        {
            Plane res = this;
            res.coordSys.Location = coordSys.Location + dist * coordSys.Normal;
            return res;
        }
        /// <summary>
        /// Returns a projection that projects (perpendiccular) from global space to this plane
        /// </summary>
        /// <returns>the projection</returns>
        public Projection GetProjection()
        {
            return new Projection(-coordSys.Normal, coordSys.DirectionY);
        }
        public void Modify(ModOp m)
        {
            coordSys.Modify(m);
        }
#region ISerializable Members
        /// <summary>
        /// Constructor required by deserialization
        /// </summary>
        /// <param name="info">SerializationInfo</param>
        /// <param name="context">StreamingContext</param>
        public Plane(SerializationInfo info, StreamingContext context)
        {
            coordSys = (CoordSys)info.GetValue("CoordSys", typeof(CoordSys));
        }
        void ISerializable.GetObjectData(SerializationInfo info, StreamingContext context)
        {
            info.AddValue("CoordSys", coordSys);
        }
        public Plane(IJsonReadStruct data)
        {
            coordSys = data.GetValue<CoordSys>();
        }
        public void GetObjectData(IJsonWriteData data)
        {
            data.AddValues(coordSys);
        }

        public void SetObjectData(IJsonReadData data)
        {
        }
#endregion


        public void Align(GeoPoint2D c)
        {
            Location = ToGlobal(c);
        }

        internal GeoPoint2D Intersect(Axis SourceBeam)
        {
            return this.Project(Intersect(SourceBeam.Location, SourceBeam.Direction));
        }
        public static bool Intersect3Planes(GeoPoint loc1, GeoVector norm1, GeoPoint loc2, GeoVector norm2, GeoPoint loc3, GeoVector norm3, out GeoPoint ip)
        {
            Matrix m = DenseMatrix.OfRowArrays(norm1, norm2, norm3);
            // Matrix s = (Matrix)m.Solve(DenseMatrix.OfArray(new double[,] { { norm1 * loc1.ToVector() }, { norm2 * loc2.ToVector() }, { norm3 * loc3.ToVector() } }));
            Vector s = (Vector)m.Solve(new DenseVector( new double[] { norm1 * loc1.ToVector() ,  norm2 * loc2.ToVector() ,  norm3 * loc3.ToVector() } ));
            if (s.IsValid())
            {
                ip = new GeoPoint(s);
                return true;
            }
            else
            {
                ip = GeoPoint.Origin;
                return false;
            }
        }

        internal bool SamePlane(Plane p)
        {
            if (Math.Abs(Distance(p.Location)) > Precision.eps) return false;
            if (Precision.SameDirection(p.Normal, Normal, false)) return true;
            return false;
        }

#if DEBUG
        public CADability.GeoObject.Face Debug
        {
            get
            {
                PlaneSurface ps = new PlaneSurface(this);
                return CADability.GeoObject.Face.MakeFace(ps, new Shapes.SimpleShape(CADability.Shapes.Border.MakeRectangle(0, 100, 0, 100)));
            }
        }
#endif
    }
}
