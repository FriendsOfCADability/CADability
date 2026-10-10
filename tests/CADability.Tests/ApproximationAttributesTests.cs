using CADability.Attribute;
using CADability.GeoObject;
using Path = CADability.GeoObject.Path;

namespace CADability.Tests
{
    /// <summary>
    /// Issue #249: approximating a colored curve gave a path whose new lines kept the default color black, because
    /// CopyAttributes only fills in curves without a color, and a path paints its curves with their own colors.
    /// </summary>
    [TestClass]
    public class ApproximationAttributesTests
    {
        private static IEnumerable<IGeoObject> Curves()
        {
            Line line = Line.TwoPoints(GeoPoint.Origin, new GeoPoint(10, 0, 0));
            Ellipse arc = Ellipse.Construct();
            arc.SetArcPlaneCenterRadiusAngles(Plane.XYPlane, new GeoPoint(10, 5, 0), 5, -Math.PI / 2, Math.PI);
            Path path = Path.Construct();
            path.Set(new ICurve[] { line, arc });
            yield return path;
            BSpline bsp = BSpline.Construct();
            bsp.ThroughPoints(new[] { new GeoPoint(0, 0, 0), new GeoPoint(5, 5, 0), new GeoPoint(10, 0, 0), new GeoPoint(15, 4, 0) }, 3, false);
            yield return bsp;
            Ellipse ellipse = Ellipse.Construct();
            ellipse.SetEllipseArcCenterAxis(GeoPoint.Origin, 8 * GeoVector.XAxis, 3 * GeoVector.YAxis, 0, 2);
            yield return ellipse;
        }

        [TestMethod]
        public void the_approximation_keeps_the_color_of_the_curve()
        {
            Project project = Project.CreateSimpleProject();
            ColorDef red = project.ColorList.CreateOrFind("red", System.Drawing.Color.Red);
            LineWidth thick = new LineWidth("thick", 0.7);
            LinePattern dashed = new LinePattern("dashed", 2.0, 1.0);
            foreach (IGeoObject curve in Curves())
            {
                (curve as IColorDef).ColorDef = red;
                (curve as ILineWidth).LineWidth = thick;
                (curve as ILinePattern).LinePattern = dashed;
                foreach (bool linesOnly in new[] { true, false })
                {
                    string what = curve.GetType().Name + (linesOnly ? ", lines only" : ", lines and arcs");
                    IGeoObject approximation = (IGeoObject)(curve as ICurve).Approximate(linesOnly, 0.01);
                    CADability.GeoObject.Curves.CopyAttributesToApproximation(approximation, curve);
                    Assert.AreSame(red, (approximation as IColorDef).ColorDef, what);
                    IEnumerable<ICurve> parts = approximation is Path p ? p.Curves : new ICurve[] { approximation as ICurve };
                    foreach (ICurve part in parts)
                    {
                        Assert.AreSame(red, (part as IColorDef).ColorDef, what + ": " + part.GetType().Name + " is painted in the color of the curve");
                        Assert.AreSame(thick, (part as ILineWidth).LineWidth, what);
                        Assert.AreSame(dashed, (part as ILinePattern).LinePattern, what);
                    }
                }
            }
        }
    }
}
