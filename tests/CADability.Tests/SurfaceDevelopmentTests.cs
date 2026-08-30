using CADability.Curve2D;
using CADability.GeoObject;
using CADability.Shapes;
using System;

namespace CADability.Tests
{
	/// <summary>
	/// Developing surfaces into flat patterns.
	///
	/// Development is an isometry, so almost every test here is a length: if a curve
	/// keeps its length when it is flattened, the mapping is right, and if it does not,
	/// no amount of the pattern looking plausible will save it.
	/// </summary>
	[TestClass]
	public class SurfaceDevelopmentTests
	{
		private const double Radius = 30.0;
		private const double Height = 200.0;

		private static CylindricalSurface Cylinder(double radius = Radius, double axialScale = 1.0)
		{
			return new CylindricalSurface(new GeoPoint(0, 0, 0),
				radius * GeoVector.XAxis, radius * GeoVector.YAxis, axialScale * GeoVector.ZAxis);
		}

		private static ConicalSurface Cone(double semiAngle)
		{
			return new ConicalSurface(new GeoPoint(0, 0, 0),
				GeoVector.XAxis, GeoVector.YAxis, GeoVector.ZAxis, semiAngle);
		}

		/// <summary>Length of a path through parameter space, measured on the flat pattern.</summary>
		private static double FlatLength(IDevelopableSurface surface, Func<double, GeoPoint2D> uvAt, int steps = 20000)
		{
			double length = 0.0;
			GeoPoint2D previous = surface.Develop(uvAt(0.0));
			for (int i = 1; i <= steps; i++)
			{
				GeoPoint2D current = surface.Develop(uvAt(i / (double)steps));
				length += previous | current;
				previous = current;
			}

			return length;
		}

		[TestMethod]
		public void CylinderDevelopsAndComesBack()
		{
			IDevelopableSurface cylinder = Cylinder();
			Assert.IsTrue(cylinder.IsDevelopable);

			for (int i = 0; i <= 20; i++)
			{
				for (int j = 0; j <= 5; j++)
				{
					GeoPoint2D uv = new GeoPoint2D(i * 2 * Math.PI / 20, -50 + (j * 20.0));
					Assert.AreEqual(0.0, uv | cylinder.DevelopInverse(cylinder.Develop(uv)), 1e-9);
				}
			}
		}

		[TestMethod]
		public void CylinderDevelopmentKeepsLengths()
		{
			IDevelopableSurface cylinder = Cylinder();

			//A full turn is the circumference, whatever the axial position.
			Assert.AreEqual(2 * Math.PI * Radius,
				FlatLength(cylinder, t => new GeoPoint2D(t * 2 * Math.PI, 17.0)), 1e-9);

			//A generator keeps its length too.
			Assert.AreEqual(Height, FlatLength(cylinder, t => new GeoPoint2D(1.1, t * Height)), 1e-9);
		}

		[TestMethod]
		public void CylinderAxialScaleIsNotForgotten()
		{
			//v is the coordinate of the UNIT cylinder, so the flat coordinate is v*|Axis|.
			//Overlooking that yields a pattern wrong by a constant factor, and nothing else
			//in the result looks odd.
			IDevelopableSurface scaled = Cylinder(axialScale: 7.0);

			Assert.AreEqual(70.0, FlatLength(scaled, t => new GeoPoint2D(0.4, t * 10.0)), 1e-9);
		}

		[TestMethod]
		public void EllipticalCylinderIsRefusedRatherThanApproximated()
		{
			//The circumferential arc length of an ellipse is an incomplete elliptic
			//integral of the second kind. Refusing is honest; approximating would not be.
			IDevelopableSurface elliptical = new CylindricalSurface(new GeoPoint(0, 0, 0),
				30.0 * GeoVector.XAxis, 20.0 * GeoVector.YAxis, GeoVector.ZAxis);

			Assert.IsFalse(elliptical.IsDevelopable);
		}

		[TestMethod]
		public void ConeDevelopsAndComesBack()
		{
			IDevelopableSurface cone = Cone(30.0 * Math.PI / 180.0);
			Assert.IsTrue(cone.IsDevelopable);

			for (int i = 0; i <= 20; i++)
			{
				for (int j = 1; j <= 5; j++)
				{
					GeoPoint2D uv = new GeoPoint2D(i * 2 * Math.PI / 20, j * 20.0);
					Assert.AreEqual(0.0, uv | cone.DevelopInverse(cone.Develop(uv)), 1e-9);
				}
			}
		}

		[TestMethod]
		public void ConeDevelopmentKeepsLengths()
		{
			double semiAngle = 30.0 * Math.PI / 180.0;
			IDevelopableSurface cone = Cone(semiAngle);
			const double v = 100.0;

			//The circle at v has 3d radius v*sin(semiAngle), so this is its circumference -
			//and on the flat pattern it is an arc of radius v that opens by less than a full
			//turn. That it comes to the same length is what makes a cone developable.
			Assert.AreEqual(2 * Math.PI * v * Math.Sin(semiAngle),
				FlatLength(cone, t => new GeoPoint2D(t * 2 * Math.PI, v)), 1e-6);

			//A generator runs from the apex outwards and keeps its length.
			Assert.AreEqual(v, FlatLength(cone, t => new GeoPoint2D(0.7, t * v)), 1e-9);
		}

		[TestMethod]
		public void PlaneDevelopsToItself()
		{
			IDevelopableSurface plane = new PlaneSurface(new Plane(new GeoPoint(1, 2, 3), GeoVector.ZAxis));
			Assert.IsTrue(plane.IsDevelopable);

			GeoPoint2D uv = new GeoPoint2D(12, -5);
			Assert.AreEqual(0.0, uv | plane.Develop(uv), 1e-12);
		}

		[TestMethod]
		public void CurveOnACylinderKeepsItsLength()
		{
			CylindricalSurface cylinder = Cylinder();

			Line generator = Line.TwoPoints(cylinder.PointAt(new GeoPoint2D(0.9, 0.0)),
											cylinder.PointAt(new GeoPoint2D(0.9, Height)));
			Assert.AreEqual(Height, SurfaceDevelopment.DevelopCurve(cylinder, generator, 0.01).Length, 1e-6);

			Ellipse circle = Ellipse.Construct();
			circle.SetCirclePlaneCenterRadius(new Plane(new GeoPoint(0, 0, 120), GeoVector.ZAxis),
											  new GeoPoint(0, 0, 120), Radius);
			ICurve2D flat = SurfaceDevelopment.DevelopCurve(cylinder, circle, 0.01);
			Assert.AreEqual(2 * Math.PI * Radius, flat.Length, 1e-6);

			//A circle round the tube unrolls to a straight line across the pattern.
			Assert.AreEqual(flat.StartPoint.y, flat.EndPoint.y, 1e-9);
		}

		[TestMethod]
		public void TheSeamDoesNotChangeACurvesLength()
		{
			//GetProjectedCurve returns a continuous parameter curve and lets u run outside
			//one period where the curve does, so an arc that straddles u = 0 needs no
			//special handling. This pins that: the same arc costs the same either side.
			CylindricalSurface cylinder = Cylinder();
			Plane plane = new Plane(new GeoPoint(0, 0, 120), GeoVector.ZAxis);

			Ellipse acrossTheSeam = Ellipse.Construct();
			acrossTheSeam.SetArcPlaneCenterRadiusAngles(plane, new GeoPoint(0, 0, 120), Radius, -0.3, 0.6);

			Ellipse awayFromIt = Ellipse.Construct();
			awayFromIt.SetArcPlaneCenterRadiusAngles(plane, new GeoPoint(0, 0, 120), Radius, 1.0, 0.6);

			Assert.AreEqual(0.6 * Radius,
				SurfaceDevelopment.DevelopCurve(cylinder, acrossTheSeam, 0.001).Length, 1e-5);
			Assert.AreEqual(0.6 * Radius,
				SurfaceDevelopment.DevelopCurve(cylinder, awayFromIt, 0.001).Length, 1e-5);
		}

		[TestMethod]
		public void PrecisionBoundsTheApproximation()
		{
			//On a cone the development is not affine, so a straight parameter line becomes
			//a curve and the approximation has to earn its tolerance. The deviation must
			//stay inside what was asked for, and asking for less must cost more vertices.
			ConicalSurface cone = Cone(30.0 * Math.PI / 180.0);
			IDevelopableSurface developable = cone;
			Line2D circleInParameters = new Line2D(new GeoPoint2D(0, 100), new GeoPoint2D(2 * Math.PI, 100));

			int previousVertices = 0;
			foreach (double precision in new[] { 1.0, 0.1, 0.01 })
			{
				ICurve2D flat = SurfaceDevelopment.DevelopCurve(developable, circleInParameters, precision);

				double worst = 0.0;
				for (int i = 0; i <= 2000; i++)
				{
					GeoPoint2D truth = developable.Develop(circleInParameters.PointAt(i / 2000.0));
					worst = Math.Max(worst, flat.MinDistance(truth));
				}

				Assert.IsTrue(worst <= precision,
					"deviation " + worst + " exceeds the requested precision " + precision);

				int vertices = flat is Polyline2D polyline ? polyline.Vertex.Length : 2;
				Assert.IsTrue(vertices > previousVertices, "a finer tolerance must be paid for");
				previousVertices = vertices;
			}
		}

		[TestMethod]
		public void AFullCylinderDevelopsToARectangle()
		{
			CylindricalSurface cylinder = Cylinder();
			Face shell = Face.MakeFace(cylinder, new BoundingRect(0, 0, 2 * Math.PI, Height));

			CompoundShape flat = SurfaceDevelopment.DevelopFace(shell, 0.01);
			Assert.IsNotNull(flat);

			BoundingRect extent = flat.GetExtent();
			Assert.AreEqual(2 * Math.PI * Radius, extent.Width, 1e-6);
			Assert.AreEqual(Height, extent.Height, 1e-6);
			Assert.AreEqual(2 * Math.PI * Radius * Height, flat.Area, 1e-6);
		}

		[TestMethod]
		public void AHoleInTheWallStaysAHoleInThePattern()
		{
			CylindricalSurface cylinder = Cylinder();

			Border outline = new Border(new GeoPoint2D[] {
				new GeoPoint2D(0, 0), new GeoPoint2D(2 * Math.PI, 0),
				new GeoPoint2D(2 * Math.PI, Height), new GeoPoint2D(0, Height) });
			Border window = new Border(new GeoPoint2D[] {
				new GeoPoint2D(1.0, 50), new GeoPoint2D(1.0, 90),
				new GeoPoint2D(2.0, 90), new GeoPoint2D(2.0, 50) });

			Face windowed = Face.MakeFace(cylinder, new SimpleShape(outline, window));
			CompoundShape flat = SurfaceDevelopment.DevelopFace(windowed, 0.01);

			SimpleShape developed = flat.SimpleShapes[0];
			Assert.AreEqual(1, developed.Holes.Length);

			//The window spans 1 rad by 40 mm, so r*1 by 40 once flattened.
			Assert.AreEqual((2 * Math.PI * Radius * Height) - (Radius * 40.0), developed.Area, 1e-6);
		}

		[TestMethod]
		public void AConeFaceDevelopsToAnAnnularSector()
		{
			double semiAngle = 30.0 * Math.PI / 180.0;
			ConicalSurface cone = Cone(semiAngle);
			Face band = Face.MakeFace(cone, new BoundingRect(0, 50, 2 * Math.PI, 100));

			CompoundShape flat = SurfaceDevelopment.DevelopFace(band, 0.001);
			Assert.IsNotNull(flat);

			double opening = 2 * Math.PI * Math.Sin(semiAngle);
			Assert.AreEqual(0.5 * opening * ((100.0 * 100.0) - (50.0 * 50.0)), flat.Area, 0.05);
		}

		[TestMethod]
		public void ADoublyCurvedSurfaceIsRefused()
		{
			//A sphere cannot be flattened without stretching. Returning an approximation
			//would be worse than returning nothing, because nothing about the result would
			//say it had been stretched.
			SphericalSurface sphere = new SphericalSurface(new GeoPoint(0, 0, 0),
				50 * GeoVector.XAxis, 50 * GeoVector.YAxis, 50 * GeoVector.ZAxis);

			Assert.IsFalse(sphere is IDevelopableSurface);

			Line anywhere = Line.TwoPoints(new GeoPoint(50, 0, 0), new GeoPoint(0, 50, 0));
			Assert.IsNull(SurfaceDevelopment.DevelopCurve(sphere, anywhere, 0.01));
			Assert.IsNull(SurfaceDevelopment.DevelopFace(
				Face.MakeFace(sphere, new BoundingRect(0, 0, 1, 1)), 0.01));
		}
	}
}
