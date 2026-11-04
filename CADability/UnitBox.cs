using System;

namespace CADability.GeoObject
{
    /// <summary>
    /// An axis parallel box, transformed into the system of a unit surface (the toUnit of CylindricalSurface, SphericalSurface,
    /// ConicalSurface, ToroidalSurface), where it is a parallelepiped. The helpers for the HitTest(BoundingCube, out GeoPoint2D)
    /// of these surfaces, which run very often when faces are sorted into an octtree (Face.HitBoundingCube): nothing here
    /// allocates, the corners are kept in fixed buffers.
    /// <para>
    /// Corner i is at the maximum in x, y or z of the box, when bit 0, 1 or 2 of i is set. An edge is a pair of corners which
    /// differ in exactly one bit.
    /// </para>
    /// </summary>
    internal unsafe struct UnitBox
    {
        public fixed double X[8];
        public fixed double Y[8];
        public fixed double Z[8];
        /// <summary>The edges of the box in the unit system, along the x, y and z axis of the world.</summary>
        public GeoVector Ex, Ey, Ez;
        /// <summary>The center of the box in the unit system.</summary>
        public GeoPoint Center;
        /// <summary>The radius of a sphere around <see cref="Center"/> which contains the box in the unit system.</summary>
        public double Radius;
        /// <summary><see cref="Precision.eps"/> of the world in the unit system (an upper bound).</summary>
        public double Tolerance;

        public UnitBox(ModOp toUnit, BoundingCube bc)
        {
            double wx = bc.Xmax - bc.Xmin, wy = bc.Ymax - bc.Ymin, wz = bc.Zmax - bc.Zmin;
            GeoVector ux = toUnit * GeoVector.XAxis, uy = toUnit * GeoVector.YAxis, uz = toUnit * GeoVector.ZAxis;
            // how much a distance can grow: for orthogonal columns (a rotation with a scaling, the usual case) the length of the
            // longest column, otherwise the Frobenius norm of the linear part of toUnit, which is an upper bound
            double lx = ux * ux, ly = uy * uy, lz = uz * uz;
            double f;
            double orthogonality = 1e-12 * (lx + ly + lz);
            if (Math.Abs(ux * uy) < orthogonality && Math.Abs(ux * uz) < orthogonality && Math.Abs(uy * uz) < orthogonality)
                f = Math.Sqrt(Math.Max(lx, Math.Max(ly, lz)));
            else f = Math.Sqrt(lx + ly + lz);
            Tolerance = Precision.eps * f;
            Radius = f * Math.Sqrt(wx * wx + wy * wy + wz * wz) / 2.0;
            Center = toUnit * new GeoPoint((bc.Xmin + bc.Xmax) / 2.0, (bc.Ymin + bc.Ymax) / 2.0, (bc.Zmin + bc.Zmax) / 2.0);
            Ex = wx * ux;
            Ey = wy * uy;
            Ez = wz * uz;
            GeoPoint c0 = toUnit * new GeoPoint(bc.Xmin, bc.Ymin, bc.Zmin);
            for (int i = 0; i < 8; ++i)
            {
                double x = c0.x, y = c0.y, z = c0.z;
                if ((i & 1) != 0) { x += Ex.x; y += Ex.y; z += Ex.z; }
                if ((i & 2) != 0) { x += Ey.x; y += Ey.y; z += Ey.z; }
                if ((i & 4) != 0) { x += Ez.x; y += Ez.y; z += Ez.z; }
                X[i] = x; Y[i] = y; Z[i] = z;
            }
        }

        public GeoPoint Corner(int i) => new GeoPoint(X[i], Y[i], Z[i]);

        /// <summary>The edge vector from corner i to corner i | bit.</summary>
        public GeoVector Edge(int bit) => bit == 1 ? Ex : bit == 2 ? Ey : Ez;

        /// <summary>
        /// The point of the parallelepiped closest to the origin. Minimizing the convex |p|^2 over the box: the minimum is inside,
        /// in the interior of a face or on an edge (which includes the corners), so it is the best of these candidates.
        /// </summary>
        public GeoPoint ClosestToOrigin()
        {
            GeoPoint c0 = Corner(0);
            GeoVector p0 = c0 - GeoPoint.Origin;
            // inside: c0 + s*Ex + t*Ey + w*Ez == 0 with s, t, w in [0, 1] (Cramer's rule)
            double det = Ex * (Ey ^ Ez);
            if (det != 0.0)
            {
                double s = -(p0 * (Ey ^ Ez)) / det;
                double t = -(Ex * (p0 ^ Ez)) / det;
                double w = -(Ex * (Ey ^ p0)) / det;
                if (s >= 0.0 && s <= 1.0 && t >= 0.0 && t <= 1.0 && w >= 0.0 && w <= 1.0) return GeoPoint.Origin;
            }
            GeoPoint best = c0;
            double bestD2 = p0 * p0;
            // the six faces: one direction fixed at 0 or 1, the other two free
            for (int fixedBit = 1; fixedBit < 8; fixedBit <<= 1)
            {
                GeoVector a = fixedBit == 1 ? Ey : Ex;
                GeoVector b = fixedBit == 4 ? Ey : Ez;
                for (int side = 0; side < 2; ++side)
                {
                    GeoVector q = side == 0 ? p0 : p0 + Edge(fixedBit);
                    // minimize |q + s*a + t*b|^2: the normal equations
                    double aa = a * a, ab = a * b, bb = b * b, aq = a * q, bq = b * q;
                    double d = aa * bb - ab * ab;
                    if (d <= 0.0) continue;
                    double s = (-aq * bb + bq * ab) / d;
                    double t = (-bq * aa + aq * ab) / d;
                    if (s < 0.0 || s > 1.0 || t < 0.0 || t > 1.0) continue;
                    GeoVector p = q + s * a + t * b;
                    double d2 = p * p;
                    if (d2 < bestD2)
                    {
                        bestD2 = d2;
                        best = GeoPoint.Origin + p;
                    }
                }
            }
            // the twelve edges
            for (int i = 0; i < 8; ++i)
            {
                for (int bit = 1; bit < 8; bit <<= 1)
                {
                    if ((i & bit) != 0) continue;
                    GeoVector e = Edge(bit);
                    double ee = e * e;
                    GeoVector q = Corner(i) - GeoPoint.Origin;
                    double t = ee > 0.0 ? -(q * e) / ee : 0.0;
                    if (t < 0.0) t = 0.0;
                    else if (t > 1.0) t = 1.0;
                    GeoVector p = q + t * e;
                    double d2 = p * p;
                    if (d2 < bestD2)
                    {
                        bestD2 = d2;
                        best = GeoPoint.Origin + p;
                    }
                }
            }
            return best;
        }

        /// <summary>
        /// The parameter in [0, 1], where A*t^2 + 2*B*t + C changes its sign from C (at t == 0, not positive) to A + 2*B + C (at
        /// t == 1, not negative). This is where a segment from a point inside to a point outside crosses a quadric. Values beyond
        /// the expected signs (within a tolerance) give the nearer end.
        /// </summary>
        public static double Crossing(double A, double B, double C)
        {
            double f1 = A + 2.0 * B + C;
            if (C >= 0.0) return 0.0;
            if (f1 <= 0.0) return 1.0;
            double t;
            if (Math.Abs(A) <= 1e-14 * (Math.Abs(B) + Math.Abs(C))) t = -C / (2.0 * B); // linear
            else
            {
                double discriminant = B * B - A * C;
                if (discriminant < 0.0) discriminant = 0.0;
                double q = -(B + (B >= 0.0 ? 1.0 : -1.0) * Math.Sqrt(discriminant)); // the stable form of the two roots
                double t1 = q / A;
                double t2 = q != 0.0 ? C / q : t1;
                // f(0) < 0 < f(1): exactly one root is in [0, 1]
                t = (t1 >= 0.0 && t1 <= 1.0) ? t1 : t2;
            }
            if (t < 0.0) t = 0.0;
            else if (t > 1.0) t = 1.0;
            return t;
        }

        /// <summary>
        /// Whether the line <paramref name="loc"/> + s*<paramref name="dir"/> passes through <paramref name="bc"/>, enlarged by
        /// <paramref name="eps"/>. <paramref name="s"/> is then the parameter of a point of the line inside the box. The box is
        /// axis parallel, so this is the slab method: the parameter interval of the line inside each slab.
        /// </summary>
        public static bool LinePassesThrough(GeoPoint loc, GeoVector dir, BoundingCube bc, double eps, out double s)
        {
            double sMin = double.NegativeInfinity, sMax = double.PositiveInfinity;
            s = 0.0;
            if (!Slab(loc.x, dir.x, bc.Xmin - eps, bc.Xmax + eps, ref sMin, ref sMax)) return false;
            if (!Slab(loc.y, dir.y, bc.Ymin - eps, bc.Ymax + eps, ref sMin, ref sMax)) return false;
            if (!Slab(loc.z, dir.z, bc.Zmin - eps, bc.Zmax + eps, ref sMin, ref sMax)) return false;
            s = (sMin + sMax) / 2.0;
            return true;
        }
        private static bool Slab(double origin, double direction, double min, double max, ref double sMin, ref double sMax)
        {
            if (direction == 0.0) return origin >= min && origin <= max;
            double s1 = (min - origin) / direction, s2 = (max - origin) / direction;
            if (s1 > s2) { double tmp = s1; s1 = s2; s2 = tmp; }
            if (s1 > sMin) sMin = s1;
            if (s2 < sMax) sMax = s2;
            return sMin <= sMax;
        }
    }
}
