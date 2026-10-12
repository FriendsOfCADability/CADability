using CADability.GeoObject;

namespace CADability.Tests
{
    // Validation of NurbsSurface.GetPatchExtent(int uKnotIndex, int vKnotIndex, bool rough):
    // the returned box must be guaranteed to contain the surface patch of the knot span (checked by
    // dense sampling), the tight box must be contained in the rough box and must be close to the
    // smallest enclosing box of the patch.
    [TestClass]
    public class NurbsPatchExtentTests
    {
        public TestContext TestContext { get; set; }

        /// <summary>
        /// Checks all knot spans of the surface: dense samples must be inside both boxes, the tight box
        /// must be inside the rough box and must not exceed the sample box by more than tightness*diagonal.
        /// </summary>
        private static void CheckAllSpans(NurbsSurface ns, double tightness, string name)
        {
            const int samples = 12;
            for (int iu = 0; iu < ns.UKnotSpanCount; ++iu)
            {
                for (int iv = 0; iv < ns.VKnotSpanCount; ++iv)
                {
                    BoundingRect span = ns.GetKnotSpan(iu, iv);
                    BoundingCube rough = ns.GetPatchExtent(iu, iv, true);
                    BoundingCube tight = ns.GetPatchExtent(iu, iv, false);
                    BoundingCube sampleBox = BoundingCube.EmptyBoundingCube;
                    double eps = Math.Max(rough.DiagonalLength, 1.0) * 1e-7;
                    for (int i = 0; i <= samples; ++i)
                    {
                        for (int j = 0; j <= samples; ++j)
                        {
                            GeoPoint2D uv = new GeoPoint2D(
                                span.Left + i * (span.Right - span.Left) / samples,
                                span.Bottom + j * (span.Top - span.Bottom) / samples);
                            GeoPoint p = ns.PointAt(uv);
                            sampleBox.MinMax(p);
                            Assert.IsTrue(tight.Contains(p, eps),
                                name + ": tight box of span (" + iu + ", " + iv + ") does not contain surface point at " + uv);
                            Assert.IsTrue(rough.Contains(p, eps),
                                name + ": rough box of span (" + iu + ", " + iv + ") does not contain surface point at " + uv);
                        }
                    }
                    // tight box is a subset of the rough box (Bézier hulls are contained in the pole hull)
                    BoundingCube roughExp = rough;
                    roughExp.Expand(eps);
                    Assert.IsTrue(roughExp.Contains(tight),
                        name + ": tight box of span (" + iu + ", " + iv + ") exceeds the rough box");
                    // tightness: the tight box may exceed the sample box only by a small fraction of the diagonal
                    double allowed = Math.Max(sampleBox.DiagonalLength, eps) * tightness;
                    BoundingCube sampleExp = sampleBox;
                    sampleExp.Expand(allowed);
                    Assert.IsTrue(sampleExp.Contains(tight),
                        name + ": tight box of span (" + iu + ", " + iv + ") is too loose: " + tight + " vs samples " + sampleBox);
                    // cached second call yields the same result
                    BoundingCube again = ns.GetPatchExtent(iu, iv, false);
                    Assert.AreEqual(tight.Xmin, again.Xmin, name + ": cache returns different box");
                    Assert.AreEqual(tight.Zmax, again.Zmax, name + ": cache returns different box");
                }
            }
        }

        /// <summary>
        /// Checks the BoundingRect based GetPatchExtent (span decomposition): dense samples of the rect must be
        /// inside both boxes, tight must be inside rough, tight must not exceed the sample box by more than
        /// tightness*diagonal. For rects reaching beyond the domain of a periodic parameter the sample
        /// parameters are wrapped into the domain.
        /// </summary>
        private static void CheckRect(NurbsSurface ns, BoundingRect rect, double tightness, string name)
        {
            BoundingCube tight = ns.GetPatchExtent(rect, false);
            BoundingCube rough = ns.GetPatchExtent(rect, true);
            double umin = ns.GetKnotSpan(0, 0).Left, umax = ns.GetKnotSpan(ns.UKnotSpanCount - 1, 0).Right;
            double vmin = ns.GetKnotSpan(0, 0).Bottom, vmax = ns.GetKnotSpan(0, ns.VKnotSpanCount - 1).Top;
            BoundingCube sampleBox = BoundingCube.EmptyBoundingCube;
            double eps = Math.Max(rough.DiagonalLength, 1.0) * 1e-7;
            const int samples = 20;
            for (int i = 0; i <= samples; ++i)
            {
                for (int j = 0; j <= samples; ++j)
                {
                    double u = rect.Left + i * (rect.Right - rect.Left) / samples;
                    double v = rect.Bottom + j * (rect.Top - rect.Bottom) / samples;
                    if ((ns as ISurface).IsUPeriodic) { if (u > umax) u -= umax - umin; if (u < umin) u += umax - umin; }
                    if ((ns as ISurface).IsVPeriodic) { if (v > vmax) v -= vmax - vmin; if (v < vmin) v += vmax - vmin; }
                    GeoPoint p = ns.PointAt(new GeoPoint2D(u, v));
                    sampleBox.MinMax(p);
                    Assert.IsTrue(tight.Contains(p, eps), name + ": tight rect box does not contain surface point at (" + u + ", " + v + ")");
                    Assert.IsTrue(rough.Contains(p, eps), name + ": rough rect box does not contain surface point at (" + u + ", " + v + ")");
                }
            }
            BoundingCube roughExp = rough;
            roughExp.Expand(eps);
            Assert.IsTrue(roughExp.Contains(tight), name + ": tight rect box exceeds the rough rect box");
            BoundingCube sampleExp = sampleBox;
            sampleExp.Expand(Math.Max(sampleBox.DiagonalLength, eps) * tightness);
            Assert.IsTrue(sampleExp.Contains(tight), name + ": tight rect box too loose: " + tight + " vs samples " + sampleBox);
        }

        // ShapeIt also checks a NURBS face with holes from a file (NurbsWithHoles.json), which is written in ShapeIt's own
        // json format (renamed types) and cannot be read here.
        [TestMethod]
        public void patch_extent_finds_inner_extremum_of_bump_patch()
        {
            // a single span bicubic Bézier patch, flat except the four inner poles at z == 1: the patch has an
            // inner maximum z(0.5, 0.5) == 0.5625, well below the pole hull (zmax == 1). The tight box must
            // capture the inner extremum (Newton), the rough box uses the pole hull
            GeoPoint[,] poles = new GeoPoint[4, 4];
            for (int i = 0; i < 4; ++i)
            {
                for (int j = 0; j < 4; ++j)
                {
                    double z = (i > 0 && i < 3 && j > 0 && j < 3) ? 1.0 : 0.0;
                    poles[i, j] = new GeoPoint(i, j, z);
                }
            }
            double[] knots = new double[] { 0, 0, 0, 0, 1, 1, 1, 1 };
            NurbsSurface ns = new NurbsSurface(poles, null, knots, knots, 3, 3, false, false);
            Assert.AreEqual(1, ns.UKnotSpanCount);
            Assert.AreEqual(1, ns.VKnotSpanCount);
            CheckAllSpans(ns, 0.02, "BumpPatch");
            BoundingCube rough = ns.GetPatchExtent(0, 0, true);
            BoundingCube tight = ns.GetPatchExtent(0, 0, false);
            Assert.AreEqual(1.0, rough.Zmax, 1e-12, "rough box should be the pole hull");
            Assert.IsTrue(tight.Zmax >= 0.5625 - 1e-9, "tight box misses the inner maximum");
            Assert.IsTrue(tight.Zmax < 0.6, "tight box too loose at the inner maximum: " + tight.Zmax);
            // a rect strictly inside the single span exercises the sub rectangle Bézier extraction: it
            // contains the inner maximum at (0.5, 0.5), but not the flat border region
            CheckRect(ns, new BoundingRect(0.2, 0.3, 0.6, 0.7), 0.02, "BumpPatch inner rect");
            BoundingCube inner = ns.GetPatchExtent(new BoundingRect(0.2, 0.3, 0.6, 0.7), false);
            Assert.IsTrue(inner.Zmax >= 0.5625 - 1e-9, "sub rect box misses the inner maximum");
            Assert.IsTrue(inner.Zmin > 0.05, "sub rect box should not reach down to the flat border: " + inner.Zmin);
        }

        [TestMethod]
        public void patch_extent_is_tight_for_rational_cylinder_patch()
        {
            // exact rational quadratic 90° arc from 30° to 120°, extruded in z: the patch has a maximum
            // y == r at 90° (inside the span), while the middle pole is at y == r/cos(45°) ≈ 1.414*r.
            // So rough and tight box differ significantly in y
            double r = 10, h = 5;
            double a0 = Math.PI / 6, a1 = 2 * Math.PI / 3, am = (a0 + a1) / 2;
            double w1 = Math.Cos((a1 - a0) / 2);
            GeoPoint[,] poles = new GeoPoint[3, 2];
            double[,] weights = new double[3, 2];
            for (int j = 0; j < 2; ++j)
            {
                double z = j * h;
                poles[0, j] = new GeoPoint(r * Math.Cos(a0), r * Math.Sin(a0), z);
                poles[1, j] = new GeoPoint(r / w1 * Math.Cos(am), r / w1 * Math.Sin(am), z);
                poles[2, j] = new GeoPoint(r * Math.Cos(a1), r * Math.Sin(a1), z);
                weights[0, j] = 1; weights[1, j] = w1; weights[2, j] = 1;
            }
            NurbsSurface ns = new NurbsSurface(poles, weights,
                new double[] { 0, 0, 0, 1, 1, 1 }, new double[] { 0, 0, 1, 1 }, 2, 1, false, false);
            CheckAllSpans(ns, 0.01, "RationalCylinderPatch");
            BoundingCube rough = ns.GetPatchExtent(0, 0, true);
            BoundingCube tight = ns.GetPatchExtent(0, 0, false);
            Assert.AreEqual(r / w1 * Math.Sin(am), rough.Ymax, 1e-9, "rough box should be the pole hull");
            Assert.IsTrue(tight.Ymax >= r - 1e-9, "tight box misses the arc maximum at 90°");
            Assert.IsTrue(tight.Ymax <= r + 0.005 * r, "tight box too loose at the arc maximum: " + tight.Ymax);
            // a rect inside the single span exercises the rational sub rectangle path
            CheckRect(ns, new BoundingRect(0.1, 0.2, 0.7, 0.8), 0.01, "RationalCylinderPatch inner rect");
        }

        [TestMethod]
        public void rect_patch_extent_wraps_around_periodic_seam()
        {
            // a closed tube: straight in u, a smooth closed cubic ring in v (unclamped periodic
            // representation: 8 poles, single knots, vPeriodic == true). A rect crossing the seam at
            // v == vmax must be decomposed into the two parts on both sides of the seam
            double r = 10, h = 5;
            GeoPoint[,] poles = new GeoPoint[2, 8];
            for (int j = 0; j < 8; ++j)
            {
                double a = j * Math.PI / 4;
                for (int i = 0; i < 2; ++i)
                {
                    poles[i, j] = new GeoPoint(r * Math.Cos(a), r * Math.Sin(a), i * h);
                }
            }
            double[] vKnots = new double[9];
            int[] vMults = new int[9];
            for (int j = 0; j < 9; ++j) { vKnots[j] = j / 8.0; vMults[j] = 1; }
            NurbsSurface ns = new NurbsSurface(poles, null, new double[] { 0, 1 }, vKnots,
                new int[] { 2, 2 }, vMults, 1, 3, false, true);
            Assert.IsTrue((ns as ISurface).IsVPeriodic, "surface should be periodic in v");
            double umin = ns.GetKnotSpan(0, 0).Left, umax = ns.GetKnotSpan(ns.UKnotSpanCount - 1, 0).Right;
            double vmin = ns.GetKnotSpan(0, 0).Bottom, vmax = ns.GetKnotSpan(0, ns.VKnotSpanCount - 1).Top;
            double w = 0.15 * (vmax - vmin);
            CheckRect(ns, new BoundingRect(umin + 0.2 * (umax - umin), vmax - w, umin + 0.8 * (umax - umin), vmax + w),
                0.02, "periodic seam rect");
            CheckAllSpans(ns, 0.02, "periodic tube");
        }
    }
}
