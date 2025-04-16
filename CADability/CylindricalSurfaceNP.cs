using CADability.Curve2D;
using CADability.Shapes;
using CADability.UserInterface;
using System;
using System.Collections.Generic;
using System.Runtime.Serialization;

namespace CADability.GeoObject
{
	/// <summary>
	/// A cylindrical surface, defined by a point (location), and axis (zAxis). The axis (zAxis) and the two vectors xAxis and yAxis build an orthogonal coordinate system.
	/// xAxis and yAxis have the same length, which define the radius of the cylinder. The length of the zAxis is the domain of the cylindrical surface. points outside this
	/// domain are defined, but not used. The 2d u/v system is defined by the plane through "location" which is spanned by xAxis and yAxis. For each point on the surface, there 
	/// is a point in this plane, which is found by intersecting the line from (location-zAxis) to the surface point with the u/v plane. Thus all points of the domain on the surface
	/// are within an annulus with the cylinder radius and a hole of half the cylinder radius. The u/v system is not periodic and has no poles (except of the 2d origin, 
	/// which is at the infinite end of the axis). When the orthogonal system of xAxis, yAxis and zAxis is right handed, the normal vectors point to the outside, otherwise 
	/// they point to the inside. Circles, ellipses and lines in 3d correspond to circles, ellipses and lines in 2d. 3d ellipses with their center at (location-zAxis)
	/// degenerate to lines in 2d.
	/// </summary>
	[Serializable]
	public class CylindricalSurfaceNP : ISurfaceImpl, IJsonSerialize, ISerializable, IRestrictedDomain, ICylinder
	{
		GeoPoint location; // the location at the lowest (in the direction of zAxis) usable position of the cylinder
		GeoVector xAxis, yAxis, zAxis; // the system at the "bottom" of the cylinder. Should all have the same length
									   // The projection goes from the point (location-zAxis) to the plane (location, xAxis, yAxis). the u/v system is the plane
		Polynom implicitPolynomial; // will be calculated when needed
#if DEBUG
		int id;
		static int idcnt = 0;
#endif
		public CylindricalSurfaceNP(GeoPoint location, GeoVector xAxis, GeoVector yAxis, GeoVector zAxis)
		{
#if DEBUG
			id = ++idcnt;
#endif
			this.location = location;
			this.xAxis = xAxis;
			this.yAxis = yAxis;
			this.zAxis = zAxis;
		}
		public CylindricalSurfaceNP(GeoPoint center, double radius, GeoVector axis, bool outwardOriented, ICurve[] orientedCurves)
		{
#if DEBUG
			id = ++idcnt;
#endif
			double minpos = double.MaxValue;
			double maxpos = double.MinValue;
			for (int i = 0; i < orientedCurves.Length; i++)
			{
				double[] extr = orientedCurves[i].GetExtrema(axis);
				double lp;
				for (int j = 0; j < extr.Length; j++)
				{
					lp = Geometry.LinePar(center, axis, orientedCurves[i].PointAt(extr[j]));
					minpos = Math.Min(minpos, lp);
					maxpos = Math.Max(maxpos, lp);
				}
				lp = Geometry.LinePar(center, axis, orientedCurves[i].StartPoint);
				minpos = Math.Min(minpos, lp);
				maxpos = Math.Max(maxpos, lp);
				lp = Geometry.LinePar(center, axis, orientedCurves[i].EndPoint);
				minpos = Math.Min(minpos, lp);
				maxpos = Math.Max(maxpos, lp);
			}
			location = Geometry.LinePos(center, axis, minpos);
			axis.ArbitraryNormals(out xAxis, out yAxis);
			if (!outwardOriented) xAxis = -xAxis;
			zAxis = axis;
			xAxis.Length = radius;
			yAxis.Length = radius;
			zAxis.Length = (maxpos - minpos) * axis.Length;
		}

		public override ICurve FixedU(double u, double vmin, double vmax)
		{
			if (Math.Abs(u) < xAxis.Length * 1e-6)
			{
				GeoPoint sp = PointAt(new GeoPoint2D(u, vmin));
				GeoPoint ep = PointAt(new GeoPoint2D(u, vmax));
				return Line.TwoPoints(sp, ep);
			}
			else
			{
				GeoPoint majax = PointAt(new GeoPoint2D(u, 0));
				Ellipse e = Ellipse.Construct();
				GeoPoint center = location - zAxis;
				e.SetEllipseCenterAxis(center, majax - center, yAxis);
				GeoPoint sp = PointAt(new GeoPoint2D(u, vmin));
				GeoPoint ep = PointAt(new GeoPoint2D(u, vmax));
				double spos = e.PositionOf(sp);
				double epos = e.PositionOf(ep);
				e.Trim(spos, epos);
				return e;
			}
		}

		public override ICurve FixedV(double v, double umin, double umax)
		{
			Line2D l2d = new Line2D(new GeoPoint2D(umin, v), new GeoPoint2D(umax, v));
			double[] cl = Clip(l2d);
			if (cl.Length == 2)
			{
				umin = l2d.PointAt(cl[0]).x;
				umax = l2d.PointAt(cl[1]).x;
			}
			if (Math.Abs(v) < xAxis.Length * 1e-8)
			{
				GeoPoint sp = PointAt(new GeoPoint2D(umin, v));
				GeoPoint ep = PointAt(new GeoPoint2D(umax, v));
				return Line.TwoPoints(sp, ep);
			}
			else
			{
				GeoPoint majax = PointAt(new GeoPoint2D(0, v));
				Ellipse e = Ellipse.Construct();
				GeoPoint center = location - zAxis;
				e.SetEllipseCenterAxis(center, majax - center, xAxis);
				GeoPoint sp = PointAt(new GeoPoint2D(umin, v));
				GeoPoint ep = PointAt(new GeoPoint2D(umax, v));
				double spos = e.PositionOf(sp);
				double epos = e.PositionOf(ep);
				e.Trim(spos, epos);
				return e;
			}
		}
		public override ICurve Make3dCurve(ICurve2D curve2d)
		{
			if (curve2d is Line2D l2d)
			{
				if (Math.Abs(Geometry.DistPL(GeoPoint2D.Origin, l2d.StartPoint, l2d.EndPoint)) < Precision.eps)
				{
					// a line through the center: this is a line on the surface
					return Line.TwoPoints(PointAt(l2d.StartPoint), PointAt(l2d.EndPoint));
				}
				else
				{
					// an ellipse (this is a very rare case)
					// this can certainly be optimized: take 5 points of the line, find the 3d positions, find a plane through the points
					// and calculate the ellipse from the points
					GeoPoint[] fp3d = new GeoPoint[5];
					for (int i = 0; i < 5; i++)
					{
						fp3d[i] = PointAt(l2d.PointAt(i / 4.0));
					}
					Ellipse elli = Ellipse.FromFivePoints(fp3d, false);
					if (elli != null) return elli;
				}
			}
			else if (curve2d is Ellipse2D elli2d)
			{   // includes EllipseArc2D
				if (Precision.IsEqual(elli2d.center, GeoPoint2D.Origin))
				{
					// this is an ellipse in 3d
					GeoPoint[] fp3d = new GeoPoint[5];
					double n = 5.0;
					if (elli2d is EllipseArc2D) n = 4.0;
					for (int i = 0; i < 5; i++)
					{
						fp3d[i] = PointAt(elli2d.PointAt(i / n));
					}
					Ellipse elli = Ellipse.FromFivePoints(fp3d, !(elli2d is EllipseArc2D));
					if (elli != null) return elli;
				}
			}
			else if (curve2d is Circle2D circle2d)
			{   // includes Arc2D
				if (Precision.IsEqual(circle2d.Center, GeoPoint2D.Origin))
				{
					// this is an ellipse in 3d
					GeoPoint[] fp3d = new GeoPoint[5];
					double n = 5.0;
					if (circle2d is Arc2D) n = 4.0;
					for (int i = 0; i < 5; i++)
					{
						fp3d[i] = PointAt(circle2d.PointAt(i / n));
					}
					Ellipse elli = Ellipse.FromFivePoints(fp3d, !(circle2d is Arc2D));
					if (elli != null) return elli;
				}
			}
			return base.Make3dCurve(curve2d);
		}
		public override ICurve2D GetProjectedCurve(ICurve curve, double precision)
		{
#if DEBUG
			if (id == 135 || id == 414)
			{ }
#endif
			if (curve is Line line)
			{
				// this must be a line parallel to the axis, otherwise there are no lines on the cylinder
				// if it is an extremely small line not parallel to the axis, the result will also be a line, which
				// in reverse projection will give a very short ellipse
				return new Line2D(PositionOf(line.StartPoint), PositionOf(line.EndPoint));
			}
			else if (curve is Ellipse elli)
			{
				// the ellipse must be above the projection center of the 2d system
				// otherwise it is probably a hyperbola
				GeoPoint p1 = Geometry.DropPL(elli.Center + elli.MajorAxis, location, zAxis);
				GeoPoint p2 = Geometry.DropPL(elli.Center - elli.MajorAxis, location, zAxis);
				double pos1 = Geometry.LinePar(location - zAxis, zAxis, p1);
				double pos2 = Geometry.LinePar(location - zAxis, zAxis, p2);
				if (pos1 < 0 || pos2 < 0) return base.GetProjectedCurve(curve, precision);
				double d = Geometry.DistPL(elli.Center, location, zAxis);
				double prec = (elli.MajorRadius + elli.MinorRadius) * 1e-6;
				// FromFivePoints for this case
				GeoPoint2D[] fp2d = new GeoPoint2D[5];
				double n = 5.0;
				if (elli.IsArc) n = 4;
				for (int i = 0; i < 5; i++)
				{
					fp2d[i] = PositionOf(elli.PointAt(i / n));
				}
				Ellipse2D elli2d = Ellipse2D.FromFivePoints(fp2d, !elli.IsArc); // returns both full ellipse and ellipse arc
				if (elli2d != null)
				{
					if (elli.IsArc)
					{
						double spar = elli2d.PositionOf(PositionOf(elli.StartPoint));
						double epar = elli2d.PositionOf(PositionOf(elli.EndPoint));
						EllipseArc2D elliarc2d = elli2d.Trim(spar, epar) as EllipseArc2D;
						// there must be a more sophisticated way to calculate the orientation and which part to use, but the following works:
						double pm = elliarc2d.PositionOf(PositionOf(elli.PointAt(0.5)));
						if (pm < 0 || pm > 1) elliarc2d = elliarc2d.GetComplement();
						pm = elliarc2d.PositionOf(PositionOf(elli.PointAt(0.1)));
						if (pm > 0.5) elliarc2d.Reverse();
						return elliarc2d;
					}
					else
					{   // get the correct orientation
						double pos = elli2d.PositionOf(PositionOf(elli.PointAt(0.1)));
						if (pos > 0.5) elli2d.Reverse();
						return elli2d;
					}
				}
			}
			return base.GetProjectedCurve(curve, precision);
		}

		public override ISurface GetModified(ModOp m)
		{
			return new CylindricalSurfaceNP(m * location, m * xAxis, m * yAxis, m * zAxis);
		}
		public override void Modify(ModOp m)
		{
			boxedSurfaceEx = null;
			implicitPolynomial = null;
			location = m * location;
			xAxis = m * xAxis;
			yAxis = m * yAxis;
			zAxis = m * zAxis;
		}
		public override GeoPoint PointAt(GeoPoint2D uv)
		{
			double l = Math.Sqrt(uv.x * uv.x + uv.y * uv.y);
			if (l == 0.0) return location + zAxis; // this is invalid and should never be used. 
			double r = xAxis.Length;
			// l must be between r/2 and r
			// trying to clip here:
#if DEBUG
			if (l < r / 2 * 0.99) { }
#endif
			if (l < r / 2) l = r / 2;
			if (l > r) l = r;
			double z = (r - l) / l;
			double a = Math.Atan2(uv.y, uv.x);
			return location + Math.Cos(a) * xAxis + Math.Sin(a) * yAxis + z * zAxis;
		}
		public override double MaxDist(GeoPoint2D sp, GeoPoint2D ep, out GeoPoint2D mp)
		{
			GeoPoint sp3d = PointAt(sp);
			GeoPoint ep3d = PointAt(ep);
			double d = Geometry.DistLL(location, zAxis, sp3d, ep3d - sp3d, out double u1, out double u2);
			mp = PositionOf(sp3d + u2 * (ep3d - sp3d));
			return xAxis.Length - d;
		}
		public override GeoVector UDirection(GeoPoint2D uv)
		{
			double l2 = uv.x * uv.x + uv.y * uv.y;
			if (l2 == 0.0) return GeoVector.NullVector;
			double l = Math.Sqrt(l2);
			double l32 = Math.Sqrt(l2 * l2 * l2);
			double r = xAxis.Length;
			return (-(uv.x * (r - l)) / l32) * zAxis - (uv.x) / l2 * zAxis - (uv.x * uv.y) / l32 * yAxis + 1 / l * xAxis - (uv.x * uv.x) / l32 * xAxis;
		}

		public override GeoVector VDirection(GeoPoint2D uv)
		{
			double l2 = uv.x * uv.x + uv.y * uv.y;
			if (l2 == 0.0) return GeoVector.NullVector;
			double l = Math.Sqrt(l2);
			double l32 = Math.Sqrt(l2 * l2 * l2);
			double r = xAxis.Length;
			return (-(uv.y * (r - l)) / l32) * zAxis - (uv.y) / l2 * zAxis + 1 / l * yAxis - (uv.y * uv.y) / l32 * yAxis - (uv.x * uv.y) / l32 * xAxis;
		}
		public override void Derivation2At(GeoPoint2D uv, out GeoPoint p, out GeoVector du, out GeoVector dv, out GeoVector duu, out GeoVector dvv, out GeoVector duv)
		{   // from maxima and common sub expression
			double u = uv.x;
			double v = uv.y;
			double r = xAxis.Length;
			double uv52 = exp52(sqr(v) + sqr(u));
			double uv32 = exp32(sqr(v) + sqr(u));
			GeoVector zl = 1 / (sqr(v) + sqr(u)) * zAxis;
			double uvl = r - Math.Sqrt(sqr(v) + sqr(u));
			GeoVector sc1 = (-(uvl * zAxis)) / uv32;
			p = location + uvl / Math.Sqrt(sqr(v) + sqr(u)) * zAxis + u / Math.Sqrt(sqr(v) + sqr(u)) * yAxis + v / Math.Sqrt(sqr(v) + sqr(u)) * xAxis;
			du = (-(u * uvl * zAxis)) / uv32 - u * zAxis / (sqr(v) + sqr(u)) + yAxis / Math.Sqrt(sqr(v) + sqr(u)) - sqr(u) * yAxis / uv32 - u * v * xAxis / uv32;
			dv = (-(v * uvl * zAxis)) / uv32 - v * zAxis / (sqr(v) + sqr(u)) - u * v * yAxis / uv32 + xAxis / Math.Sqrt(sqr(v) + sqr(u)) - sqr(v) * xAxis / uv32;
			duu = sc1 + 3.0 * sqr(u) * uvl * zAxis / uv52 - zl + 3.0 * sqr(u) * zAxis / sqr(sqr(v) + sqr(u)) - 3.0 * u * yAxis / uv32 + 3.0 * cube(u) * yAxis / uv52 - v * xAxis / uv32 + 3.0 * sqr(u) * v * xAxis / uv52;
			dvv = sc1 + 3.0 * sqr(v) * uvl * zAxis / uv52 - zl + 3.0 * sqr(v) * zAxis / sqr(sqr(v) + sqr(u)) - u * yAxis / uv32 + 3.0 * u * sqr(v) * yAxis / uv52 - 3.0 * v * xAxis / uv32 + 3.0 * cube(v) * xAxis / uv52;
			duv = 3.0 * u * v * uvl * zAxis / uv52 + 3.0 * u * v * zAxis / sqr(sqr(v) + sqr(u)) - v * yAxis / uv32 + 3.0 * sqr(u) * v * yAxis / uv52 - u * xAxis / uv32 + 3.0 * u * sqr(v) * xAxis / uv52;
		}
		public override GeoPoint2D PositionOf(GeoPoint p)
		{
			double r = xAxis.Length;
			GeoVector v = Geometry.ReBase(p - (location - zAxis), xAxis, yAxis, zAxis);
			GeoVector2D v2d = new GeoVector2D(v.x, v.y);
			v2d.Length = r / v.z;
			return new GeoPoint2D(v2d.x, v2d.y);
		}
		public override GeoPoint2D[] PerpendicularFoot(GeoPoint fromHere)
		{
			GeoPoint onAxis = Geometry.DropPL(fromHere, location, zAxis);
			GeoVector toHere = fromHere - onAxis;
			toHere.Length = xAxis.Length;
			return new GeoPoint2D[] { PositionOf(onAxis + toHere), PositionOf(onAxis - toHere) };
		}
		public override ModOp2D ReverseOrientation()
		{   // flip x and y axis, keep z axis, left handed system
			GeoVector tmp = xAxis;
			xAxis = yAxis;
			yAxis = tmp;
			return new ModOp2D(0, 1, 0, 1, 0, 0);

		}
		public override bool IsUPeriodic => false;
		public override bool IsVPeriodic => false;
		public override bool SameGeometry(BoundingRect thisBounds, ISurface other, BoundingRect otherBounds, double precision, out ModOp2D firstToSecond)
		{
			if (other is CylindricalSurfaceNP cnp)
			{
				if (Geometry.DistPL(cnp.location, location, zAxis) < precision && Geometry.DistPL(cnp.location + cnp.zAxis, location, zAxis) < precision)
				{
					firstToSecond = ModOp2D.Null;
					return true;
				}
				firstToSecond = ModOp2D.Null;
				return false;
			}
			return base.SameGeometry(thisBounds, other, otherBounds, precision, out firstToSecond);
		}
		public override double IsParallel(BoundingRect thisBounds, ISurface other, BoundingRect otherBounds)
		{
			if (other is ICylinder c)
			{
				if (c.Axis.SameGeometry((this as ICylinder).Axis))
				{
					return Math.Abs(Math.Abs((this as ICylinder).Radius) - Math.Abs(c.Radius));
				}
			}
			return double.MaxValue;
		}

		public override Polynom GetImplicitPolynomial()
		{
			if (implicitPolynomial == null)
			{
				GeoVector zNormed = zAxis.Normalized;
				PolynomVector x = zNormed ^ (new GeoVector(location.x, location.y, location.z) - PolynomVector.xyz);
				implicitPolynomial = (x * x) - xAxis * xAxis;
				// we need to scale the implicit polynomial so that it yields the true distance to the surface
				GeoPoint p = location + xAxis + xAxis.Normalized; // a point outside the cylinder with distance 1
				double d = implicitPolynomial.Eval(p); // this should be 1 when the polynomial is normalized
				if ((xAxis ^ yAxis) * zAxis < 0) d = -d; // inverse oriented cylinder
				implicitPolynomial = (1 / d) * implicitPolynomial; // normalize the polynomial
			}
			return implicitPolynomial;
		}

		public void GetObjectData(IJsonWriteData data)
		{
			data.AddProperty("Location", location);
			data.AddProperty("XAxis", xAxis);
			data.AddProperty("YAxis", yAxis);
			data.AddProperty("ZAxis", zAxis);
		}
		protected CylindricalSurfaceNP() { } // for JSON
		public void SetObjectData(IJsonReadData data)
		{
			location = data.GetProperty<GeoPoint>("Location");
			xAxis = data.GetProperty<GeoVector>("XAxis");
			yAxis = data.GetProperty<GeoVector>("YAxis");
			zAxis = data.GetProperty<GeoVector>("ZAxis");
		}
		protected CylindricalSurfaceNP(SerializationInfo info, StreamingContext context)
		{
			location = (GeoPoint)info.GetValue("Location", typeof(GeoPoint));
			xAxis = (GeoVector)info.GetValue("XAxis", typeof(GeoVector));
			yAxis = (GeoVector)info.GetValue("YAxis", typeof(GeoVector));
			zAxis = (GeoVector)info.GetValue("ZAxis", typeof(GeoVector));
		}
		void ISerializable.GetObjectData(SerializationInfo info, StreamingContext context)
		{
			info.AddValue("Location", location);
			info.AddValue("XAxis", xAxis);
			info.AddValue("YAxis", yAxis);
			info.AddValue("ZAxis", zAxis);
		}
		public override void GetSafeParameterSteps(double umin, double umax, double vmin, double vmax, out double[] intu, out double[] intv)
		{   // here we want to provide parameter steps so that there are invalid patches, which are completely outside the annulus.
			// The boxed surface ignores patches which return false for "IsInside" for all its vertices. And with this segmentation, 
			// these pathological patches are avoided.
			double r = xAxis.Length;
			List<double> usteps = new List<double>();
			List<double> vsteps = new List<double>();
			// a annulus from -r to r with a hole of radius r/2: the four corner patches and the central patch are totally outside of the annulus
			// The central patch may contain an infinite pole
			if (umin > -r) usteps.Add(umin);
			if (vmin > -r) vsteps.Add(vmin);
			for (int i = 0; i < 8; i++)
			{
				double d = i * 2 * r / 7.0 - r;
				if (d >= umin && d <= umax) usteps.Add(d);
				if (d >= vmin && d <= vmax) vsteps.Add(d);
			}
			if (umax < r) usteps.Add(umax);
			if (vmax < r) vsteps.Add(vmax);
			intu = usteps.ToArray();
			intv = vsteps.ToArray();
		}
		public override IDualSurfaceCurve[] GetPlaneIntersection(PlaneSurface pl, double umin, double umax, double vmin, double vmax, double precision)
		{
			if (Precision.IsPerpendicular(pl.Normal, zAxis, false))
			{
				// two lines along the cylinder axis
				Plane lower = new Plane(location, zAxis);
				GeoPoint2D sp2d = lower.Project(pl.Location);
				GeoVector2D dir2d = lower.Project(pl.Normal).ToLeft();
				GeoPoint2D[] ips = Geometry.IntersectLC(sp2d, dir2d, GeoPoint2D.Origin, xAxis.Length);
				IDualSurfaceCurve[] res = new IDualSurfaceCurve[ips.Length];
				for (int i = 0; i < ips.Length; i++)
				{
					GeoPoint p = lower.ToGlobal(ips[i]);
					Line l3d = Line.TwoPoints(p, p + zAxis);
					res[i] = new DualSurfaceCurve(l3d, this, new Line2D(this.PositionOf(l3d.StartPoint), this.PositionOf(l3d.EndPoint)), pl, new Line2D(pl.PositionOf(l3d.StartPoint), pl.PositionOf(l3d.EndPoint)));
				}
				return res;
			}
			else
			{
				// an ellipse
				GeoPoint2D[] cnts = pl.GetLineIntersection(location, zAxis);
				if (cnts.Length == 1)
				{   // there must be exactly one intersection
					GeoPoint cnt = pl.PointAt(cnts[0]);
					GeoVector minorAxis = pl.Normal ^ zAxis;
					minorAxis.Length = xAxis.Length;
					GeoVector majorAxis = minorAxis ^ pl.Normal;
					Polynom impl = GetImplicitPolynomial();
					Polynom toSolve = impl.Substitute(new Polynom(majorAxis.x, "u", cnt.x, ""), new Polynom(majorAxis.y, "u", cnt.y, ""), new Polynom(majorAxis.z, "u", cnt.z, ""));
					double[] roots = toSolve.Roots();
					// there must be two roots 
					majorAxis = roots[0] * majorAxis;

					Ellipse elli = Ellipse.Construct();
					elli.SetEllipseCenterAxis(cnt, majorAxis, minorAxis);

					GeoPoint2D[] fpnts = new GeoPoint2D[5];
					for (int i = 0; i < 5; i++)
					{
						fpnts[i] = PositionOf(elli.PointAt(i / 6.0));
					}
					Ellipse2D e2d = Ellipse2D.FromFivePoints(fpnts); // there should be a better way to calculate the 2d ellipse, but the following is wrong:
																	 // Ellipse2D e2d = new Ellipse2D(GeoPoint2D.Origin, PositionOf(cnt + majorAxis).ToVector(), PositionOf(cnt + minorAxis).ToVector());
																	 // and principal axis doesn't yield the correct result either
																	 // Geometry.PrincipalAxis(PositionOf(cnt + majorAxis).ToVector(), PositionOf(cnt + minorAxis).ToVector(), out GeoVector2D maj, out GeoVector2D min);
					return new IDualSurfaceCurve[] { new DualSurfaceCurve(elli, this, e2d, pl, pl.GetProjectedCurve(elli, 0.0)) };
				}
			}
			return new IDualSurfaceCurve[0];
		}
		public override bool IsVanishingProjection(Projection p, double umin, double umax, double vmin, double vmax)
		{
			return Precision.SameDirection(p.Direction, zAxis, false);
		}
		public override ICurve2D[] GetTangentCurves(GeoVector direction, double umin, double umax, double vmin, double vmax)
		{
			if (Precision.SameDirection(direction, zAxis, false))
			{
				return new ICurve2D[] { };
			}
			GeoPoint2D uv = PositionOf(location + (direction ^ zAxis));
			GeoVector2D dir2d = uv.ToVector();
			dir2d.Length = xAxis.Length * 1.1;
			Line2D l2d = new Line2D(GeoPoint2D.Origin - dir2d, GeoPoint2D.Origin + dir2d);
			double[] parts = Clip(l2d);
			List<ICurve2D> res = new List<ICurve2D>(2);
			for (int i = 0; i < parts.Length; i += 2)
			{
				res.Add(l2d.Trim(parts[i], parts[i + 1]));
			}
			return res.ToArray();
		}
		public ISurface AdaptToCurves(IEnumerable<ICurve> curves)
		{
			return new CylindricalSurfaceNP(this.location, xAxis.Length, zAxis, this.OutwardOriented, curves.ToArray());
		}

		public bool IsInside(GeoPoint2D uv)
		{
			double r = xAxis.Length;
			double l = Math.Sqrt(uv.x * uv.x + uv.y * uv.y);
			return l <= r && l >= r / 2;
		}

		public double[] Clip(ICurve2D curve)
		{
			SimpleShape ss = new SimpleShape(Border.MakeCircle(GeoPoint2D.Origin, xAxis.Length), Border.MakeCircle(GeoPoint2D.Origin, xAxis.Length / 2.0));
			return ss.Clip(curve, true);
		}
		public override void GetNaturalBounds(out double umin, out double umax, out double vmin, out double vmax)
		{
			double r = xAxis.Length;
			umin = vmin = -r;
			umax = vmax = r;
		}
		public override ISurface Clone()
		{
			return new CylindricalSurfaceNP(location, xAxis, yAxis, zAxis);
		}
		public override void CopyData(ISurface copyFrom)
		{
			if (copyFrom is CylindricalSurfaceNP cnp)
			{
				location = cnp.location;
				xAxis = cnp.xAxis;
				yAxis = cnp.yAxis;
				zAxis = cnp.zAxis;
			}
		}
		public override bool UvChangesWithModification => true;
		public override ICurve[] Intersect(BoundingRect thisBounds, ISurface other, BoundingRect otherBounds)
		{
			if (other is PlaneSurface ps)
			{
				IDualSurfaceCurve[] dsc = GetPlaneIntersection(ps, otherBounds.Left, otherBounds.Right, otherBounds.Bottom, otherBounds.Top, 0.0);
				List<ICurve> res = new List<ICurve>();
				foreach (var t in dsc)
				{
					res.Add(t.Curve3D);
				}
				return res.ToArray();
			}
			if (other is ICylinder cylo)
			{
				Surfaces.Intersect(this as ICylinder, cylo);
			}
			return base.Intersect(thisBounds, other, otherBounds);
		}
		Axis ICylinder.Axis
		{
			get => new Axis(location, zAxis);
			set
			{
				double r = xAxis.Length;
				yAxis = value.Direction ^ xAxis;
				yAxis.Length = r;
				xAxis = yAxis ^ value.Direction;
				xAxis.Length = r;
				r = zAxis.Length;
				zAxis = value.Direction;
				zAxis.Length = r;
				location = value.Location;
			}
		}

		double ISurfaceWithRadius.Radius
		{
			get => xAxis.Length;
			set
			{
				xAxis.Length = value;
				yAxis.Length = value;
				// don't change the zAxis: it is the overall length of the cylinder, and this should not change here
				// still it is not well-defined what it means when the radius changes for the usable part of the cylinder
			}
		}
		bool ISurfaceWithRadius.IsModifiable => true;

		public bool OutwardOriented => (xAxis ^ yAxis) * zAxis > 0; // left-handed system

		public override IPropertyEntry GetPropertyEntry(IFrame frame)
		{
			List<IPropertyEntry> se = new List<IPropertyEntry>();
			GeoPointProperty loc = new GeoPointProperty(frame, "CylindricalSurface.Location");
			loc.ReadOnly = true;
			loc.OnGetValue = () => this.location;
			se.Add(loc);
			GeoVectorProperty dirX = new GeoVectorProperty(frame, "CylindricalSurface.DirectionX");
			dirX.ReadOnly = true;
			dirX.IsAngle = false;
			dirX.OnGetValue = () => this.xAxis;
			se.Add(dirX);
			GeoVectorProperty dirY = new GeoVectorProperty(frame, "CylindricalSurface.DirectionY");
			dirY.ReadOnly = true;
			dirY.IsAngle = false;
			dirY.OnGetValue = () => this.yAxis;
			se.Add(dirY);
			if (Precision.IsEqual(xAxis.Length, yAxis.Length))
			{
				DoubleProperty radius = new DoubleProperty(frame, "CylindricalSurface.Radius");
				radius.ReadOnly = true;
				radius.SetDouble(xAxis.Length);
				radius.OnGetValue = () => this.xAxis.Length;
				se.Add(radius);
			}
			return new GroupProperty("CylindricalSurface", se.ToArray());
		}

		public GeoPoint RestrictedPointAt(GeoPoint2D uv)
		{
			double l = Math.Sqrt(uv.x * uv.x + uv.y * uv.y);
			if (l == 0.0) return location + zAxis; // this is invalid and should never be used. 
			double r = xAxis.Length;
			// l must be between r/2 and r
			// here we have to clip:
			if (l < r / 2) l = r / 2;
			if (l > r) l = r;
			double z = (r - l) / l;
			double a = Math.Atan2(uv.y, uv.x);
			return location + Math.Cos(a) * xAxis + Math.Sin(a) * yAxis + z * zAxis;
		}
	}
}
