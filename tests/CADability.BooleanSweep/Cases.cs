using CADability.GeoObject;

namespace CADability.BooleanSweep
{
    internal enum Operation { AMinusB, BMinusA, Intersect, Union }

    /// <summary>
    /// A pair of solids whose relative position depends on an offset d, with the exact volumes of both and of their common
    /// part as functions of d. d &lt; 0 means overlap (the solids reach into each other by |d|), d &gt; 0 a gap, d == 0 contact.
    /// </summary>
    internal sealed class Variant
    {
        public string Family = "", Name = "";
        public Func<double, Solid> A = null!, B = null!;
        public Func<double, double> VolumeA = null!, VolumeB = null!, Common = null!;
        /// <summary>The solids touch only in a line or a point at d == 0, so their union is not a manifold.</summary>
        public bool LineOrPointContact;
        /// <summary>B lies inside A for d &gt;= 0 and touches its hull at d == 0: A - B would be a cavity touching the hull.</summary>
        public bool BInsideA;
    }

    internal sealed class SweepCase
    {
        public Variant Variant = null!;
        public double Offset;
        public Operation Operation;
        public string Id => $"{Variant.Family}/{Variant.Name}/{Offset.ToString("R", System.Globalization.CultureInfo.InvariantCulture)}/{Operation}";

        /// <summary>The exact volume of the result at offset <paramref name="d"/>.</summary>
        public double Expected(double d)
        {
            double a = Variant.VolumeA(d), b = Variant.VolumeB(d), c = Variant.Common(d);
            return Operation switch
            {
                Operation.AMinusB => a - c,
                Operation.BMinusA => b - c,
                Operation.Intersect => c,
                _ => a + b - c,
            };
        }

        /// <summary>
        /// The accepted deviation from <see cref="Expected"/>. For offsets up to 1e-5, i.e. around and below the precision of the
        /// operation, the solids may also be taken as touching: the result at d == 0 is accepted as well.
        /// </summary>
        public double Tolerance
        {
            get
            {
                double e = Expected(Offset);
                double scale = Math.Max(Variant.VolumeA(Offset), Variant.VolumeB(Offset));
                double tol = 1e-7 * scale + 1e-6 * e + 1e-9;
                if (Math.Abs(Offset) <= 1e-5) tol += Math.Abs(e - Expected(0.0));
                return tol;
            }
        }
    }

    internal static class Cases
    {
        public static readonly double[] Offsets = { -2.5e-3, -1e-5, -1e-6, -3e-7, -1e-7, 0.0, 1e-7, 3e-7, 1e-6, 1e-5, 2.5e-3 };

        public static List<SweepCase> All()
        {
            List<SweepCase> res = new List<SweepCase>();
            foreach (Variant v in Variants())
                foreach (double d in Offsets)
                    foreach (Operation op in Enum.GetValues<Operation>())
                        res.Add(new SweepCase { Variant = v, Offset = d, Operation = op });
            return res;
        }

        static Solid Box(double x, double y, double z, double dx, double dy, double dz)
            => Make3D.MakeBox(new GeoPoint(x, y, z), dx * GeoVector.XAxis, dy * GeoVector.YAxis, dz * GeoVector.ZAxis);

        static Solid Cylinder(GeoPoint baseCentre, GeoVector radius, GeoVector height) => Make3D.MakeCylinder(baseCentre, radius, height);

        static IEnumerable<Variant> Variants()
        {
            Func<double, double> cube10 = d => 1000.0;

            // box/box: B lies on the top face of A (z == 10 + d), 5 high
            yield return new Variant { Family = "box-box", Name = "inside-the-face", A = d => Box(0, 0, 0, 10, 10, 10), B = d => Box(2, 2, 10 + d, 6, 6, 5), VolumeA = cube10, VolumeB = d => 180, Common = d => 36 * Math.Max(0, -d) };
            yield return new Variant { Family = "box-box", Name = "over-an-edge", A = d => Box(0, 0, 0, 10, 10, 10), B = d => Box(5, 2, 10 + d, 10, 6, 5), VolumeA = cube10, VolumeB = d => 300, Common = d => 30 * Math.Max(0, -d) };
            yield return new Variant { Family = "box-box", Name = "flush-sides", A = d => Box(0, 0, 0, 10, 10, 10), B = d => Box(0, 0, 10 + d, 10, 10, 5), VolumeA = cube10, VolumeB = d => 500, Common = d => 100 * Math.Max(0, -d) };

            // a box near the face x == 0 of a long bar (issue #168): its face at x == d, it reaches to x == 20
            yield return new Variant
            {
                Family = "long-bar-box", Name = "face-near-the-bar",
                A = d => Box(0, 0, 0, 40, 3000, 80), B = d => Box(d, -50, 10, 20 - d, 80, 40),
                VolumeA = d => 40 * 3000 * 80, VolumeB = d => (20 - d) * 80 * 40, Common = d => (20 - Math.Max(0, d)) * 30 * 40,
            };

            // cylinders and boxes
            yield return new Variant
            {
                Family = "cylinder-box", Name = "lying-on-the-box", LineOrPointContact = true,
                A = d => Box(0, 0, 0, 10, 10, 10), B = d => Cylinder(new GeoPoint(-5, 5, 15 + d), 5 * GeoVector.ZAxis, 20 * GeoVector.XAxis),
                VolumeA = cube10, VolumeB = d => 25 * Math.PI * 20, Common = d => 10 * Segment(5, -d),
            };
            yield return new Variant
            {
                Family = "cylinder-box", Name = "standing-on-the-box",
                A = d => Box(0, 0, 0, 10, 10, 10), B = d => Cylinder(new GeoPoint(5, 5, 10 + d), 3 * GeoVector.XAxis, 10 * GeoVector.ZAxis),
                VolumeA = cube10, VolumeB = d => 90 * Math.PI, Common = d => 9 * Math.PI * Math.Max(0, -d),
            };
            yield return new Variant
            {
                Family = "cylinder-box", Name = "inside-on-the-bottom", LineOrPointContact = true, BInsideA = true,
                A = d => Box(0, 0, 0, 10, 10, 10), B = d => Cylinder(new GeoPoint(2, 5, 3 + d), 3 * GeoVector.ZAxis, 6 * GeoVector.XAxis),
                VolumeA = cube10, VolumeB = d => 54 * Math.PI, Common = d => 6 * (9 * Math.PI - Segment(3, -d)),
            };

            // holes: a cylinder of radius 2 in the box, the blind one from z == 4 to 10 + d, the through one from -d to 10 + d
            yield return new Variant
            {
                Family = "hole", Name = "blind",
                A = d => Box(0, 0, 0, 10, 10, 10), B = d => Cylinder(new GeoPoint(5, 5, 4), 2 * GeoVector.XAxis, (6 + d) * GeoVector.ZAxis),
                VolumeA = cube10, VolumeB = d => 4 * Math.PI * (6 + d), Common = d => 4 * Math.PI * (Math.Min(10, 10 + d) - 4),
            };
            yield return new Variant
            {
                Family = "hole", Name = "through",
                A = d => Box(0, 0, 0, 10, 10, 10), B = d => Cylinder(new GeoPoint(5, 5, -d), 2 * GeoVector.XAxis, (10 + 2 * d) * GeoVector.ZAxis),
                VolumeA = cube10, VolumeB = d => 4 * Math.PI * (10 + 2 * d), Common = d => 4 * Math.PI * Math.Min(10, 10 + 2 * d),
            };

            // two cylinders of radius 5
            yield return new Variant
            {
                Family = "cylinder-cylinder", Name = "parallel-axes-apart-by-d",
                A = d => Cylinder(GeoPoint.Origin, 5 * GeoVector.XAxis, 10 * GeoVector.ZAxis), B = d => Cylinder(new GeoPoint(d, 0, -5), 5 * GeoVector.XAxis, 20 * GeoVector.ZAxis),
                VolumeA = d => 250 * Math.PI, VolumeB = d => 500 * Math.PI, Common = d => 10 * Lens(5, Math.Abs(d)),
            };
            yield return new Variant
            {
                Family = "cylinder-cylinder", Name = "same-axis-radius-5+d",
                A = d => Cylinder(GeoPoint.Origin, 5 * GeoVector.XAxis, 10 * GeoVector.ZAxis), B = d => Cylinder(new GeoPoint(0, 0, -5), (5 + d) * GeoVector.XAxis, 20 * GeoVector.ZAxis),
                VolumeA = d => 250 * Math.PI, VolumeB = d => 20 * Math.PI * (5 + d) * (5 + d), Common = d => 10 * Math.PI * Math.Pow(Math.Min(5, 5 + d), 2),
            };
            yield return new Variant
            {
                Family = "cylinder-cylinder", Name = "crossing-radius-5+d",
                A = d => Cylinder(new GeoPoint(0, 0, -10), 5 * GeoVector.XAxis, 20 * GeoVector.ZAxis), B = d => Cylinder(new GeoPoint(-10, 0, 0), (5 + d) * GeoVector.YAxis, 20 * GeoVector.XAxis),
                VolumeA = d => 500 * Math.PI, VolumeB = d => 20 * Math.PI * (5 + d) * (5 + d), Common = d => CrossedCylinders(5, 5 + d),
            };

            // spheres and boxes
            yield return new Variant
            {
                Family = "sphere-box", Name = "on-the-box", LineOrPointContact = true,
                A = d => Box(0, 0, 0, 10, 10, 10), B = d => Make3D.MakeSphere(new GeoPoint(5, 5, 15 + d), 5),
                VolumeA = cube10, VolumeB = d => 500 * Math.PI / 3, Common = d => Cap(5, -d),
            };
            yield return new Variant
            {
                Family = "sphere-box", Name = "inside-on-the-bottom", LineOrPointContact = true, BInsideA = true,
                A = d => Box(0, 0, 0, 10, 10, 10), B = d => Make3D.MakeSphere(new GeoPoint(5, 5, 4 + d), 4),
                VolumeA = cube10, VolumeB = d => 256 * Math.PI / 3, Common = d => 256 * Math.PI / 3 - Cap(4, -d),
            };

            // a bar with a rounded edge (radius 3 along y at x == 0, z == 0) and a box whose bottom is at z == d, i.e. near the
            // plane the rounding is tangent to (issue #168)
            double barVolume = 100 * (800 - (9 - 9 * Math.PI / 4));
            yield return new Variant
            {
                Family = "fillet-bar-box", Name = "covering-the-rounding",
                A = d => FilletBar(100), B = d => Box(-1, -50, d, 21, 80, 10),
                VolumeA = d => barVolume, VolumeB = d => 21 * 80 * 10, Common = d => 30 * ProfileArea(-1, 20, d, d + 10),
            };
            yield return new Variant
            {
                Family = "fillet-bar-box", Name = "starting-on-the-rounding",
                A = d => FilletBar(100), B = d => Box(1.5, -50, d, 18.5, 80, 10),
                VolumeA = d => barVolume, VolumeB = d => 18.5 * 80 * 10, Common = d => 30 * ProfileArea(1.5, 20, d, d + 10),
            };
            yield return new Variant
            {
                Family = "fillet-bar-box", Name = "covering-the-rounding-tilted-3e-8",
                A = d => FilletBar(100),
                B = d =>
                {
                    Solid box = Box(-1, -50, d, 21, 80, 10);
                    box.Modify(ModOp.Rotate(new GeoPoint(0, -10, d), GeoVector.XAxis, new SweepAngle(3e-8)));
                    return box;
                },
                VolumeA = d => barVolume, VolumeB = d => 21 * 80 * 10, Common = d => 30 * ProfileArea(-1, 20, d, d + 10),
            };
        }

        /// <summary>A bar of 40 x 20 along y from 0 to <paramref name="length"/>, its edge along y at x == 0, z == 0 rounded with radius 3.</summary>
        static Solid FilletBar(double length)
        {
            const double r = 3.0;
            Plane xz = new Plane(GeoPoint.Origin, GeoVector.XAxis, GeoVector.ZAxis);
            Ellipse arc = Ellipse.Construct();
            arc.SetArc3Points(new GeoPoint(0, 0, r), new GeoPoint(r - r * Math.Sqrt(0.5), 0, r - r * Math.Sqrt(0.5)), new GeoPoint(r, 0, 0), xz);
            GeoObject.Path outline = GeoObject.Path.Construct();
            outline.Set(new ICurve[] {
                Line.TwoPoints(new GeoPoint(r, 0, 0), new GeoPoint(40, 0, 0)),
                Line.TwoPoints(new GeoPoint(40, 0, 0), new GeoPoint(40, 0, 20)),
                Line.TwoPoints(new GeoPoint(40, 0, 20), new GeoPoint(0, 0, 20)),
                Line.TwoPoints(new GeoPoint(0, 0, 20), new GeoPoint(0, 0, r)),
                arc });
            Face profile = Face.MakeFace(new GeoObjectList(outline));
            return (Solid)Make3D.MakePrism(profile, length * GeoVector.YAxis, null);
        }

        /// <summary>The area of the segment of a circle of radius <paramref name="r"/> cut off at the depth <paramref name="h"/>.</summary>
        static double Segment(double r, double h)
        {
            if (h <= 0) return 0;
            if (h >= 2 * r) return Math.PI * r * r;
            return r * r * Math.Acos((r - h) / r) - (r - h) * Math.Sqrt(2 * r * h - h * h);
        }

        /// <summary>The volume of the cap of a sphere of radius <paramref name="r"/> of height <paramref name="h"/>.</summary>
        static double Cap(double r, double h) => h <= 0 ? 0 : Math.PI * h * h * (3 * r - h) / 3;

        /// <summary>The common area of two circles of radius <paramref name="r"/> whose centres are <paramref name="s"/> apart.</summary>
        static double Lens(double r, double s) => s >= 2 * r ? 0 : 2 * r * r * Math.Acos(s / (2 * r)) - s / 2 * Math.Sqrt(4 * r * r - s * s);

        /// <summary>
        /// The common volume of two long cylinders with the radii <paramref name="r1"/> and <paramref name="r2"/> whose axes cross at
        /// right angles: the integral of 4 sqrt(r1² - y²) sqrt(r2² - y²) over |y| &lt; min(r1, r2), with y = m sin t.
        /// </summary>
        static double CrossedCylinders(double r1, double r2)
        {
            double m = Math.Min(r1, r2);
            double f(double t)
            {
                double y = m * Math.Sin(t);
                return 4 * Math.Sqrt(Math.Max(0, r1 * r1 - y * y)) * Math.Sqrt(Math.Max(0, r2 * r2 - y * y)) * m * Math.Cos(t);
            }
            return Simpson(f, -Math.PI / 2, Math.PI / 2, 20000);
        }

        /// <summary>
        /// The area of the profile of <see cref="FilletBar"/> (x in 0..40, z in 0..20, without the corner outside the rounding of radius 3
        /// at the origin) inside the rectangle x in <paramref name="x0"/>..<paramref name="x1"/>, z in <paramref name="z0"/>..<paramref name="z1"/>.
        /// </summary>
        static double ProfileArea(double x0, double x1, double z0, double z1)
        {
            double rect = Math.Max(0, Math.Min(x1, 40) - Math.Max(x0, 0)) * Math.Max(0, Math.Min(z1, 20) - Math.Max(z0, 0));
            // the corner below the arc: x = 3 - 3 cos t, z from 0 to 3 - 3 sin t, t in 0..pi/2
            double ta = Math.Acos(Math.Clamp((3 - Math.Clamp(x0, 0, 3)) / 3, -1, 1));
            double tb = Math.Acos(Math.Clamp((3 - Math.Clamp(x1, 0, 3)) / 3, -1, 1));
            if (tb <= ta) return rect;
            double corner = Simpson(t =>
            {
                double zc = 3 - 3 * Math.Sin(t);
                double len = Math.Max(0, Math.Min(z1, zc) - Math.Max(z0, 0));
                return len * 3 * Math.Sin(t); // dx = 3 sin t dt
            }, ta, tb, 20000);
            return rect - corner;
        }

        static double Simpson(Func<double, double> f, double a, double b, int n)
        {
            double h = (b - a) / n, s = f(a) + f(b);
            for (int i = 1; i < n; i++) s += f(a + i * h) * (i % 2 == 1 ? 4 : 2);
            return s * h / 3;
        }
    }
}
