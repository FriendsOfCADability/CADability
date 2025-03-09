using System;
using System.Collections.Generic;
using System.Runtime.Serialization;
using CADability.Curve2D;
using CADability.UserInterface;

namespace CADability.GeoObject
{
    /// <summary>
    /// Conical surface with a uv system (parametric space) which is not periodic and has no pole. The uv system correspond to a plane through the apex perpendicular to the axis
    /// which contains the projected cone to this plane. Disadvantage is numeric precision when the opening angle is very small. A solution would be an offset and a factor
    /// which only projects the "used area" of this cone to a standard ring
    /// </summary>
    [Serializable()]
    public class ConicalSurfaceNP : ISurfaceImpl, IJsonSerialize, ISerializable, ICone
    {
        GeoPoint location; // the location of the tip of the cone
        GeoVector xAxis, yAxis, zAxis; // the axis build a perpendicular coordinate system, the zAxis is the axis of the cone. Right handed: the axis is inside the cone
        // only the part int the direction of the zAxis is used. the x and y system define the u,v system, the length of the zAxis with respect to the length of x and y axis
        // define the opening angle
        Polynom implicitPolynomial; // cached value for the implicit form of the surface
        public ConicalSurfaceNP(GeoPoint location, GeoVector xAxis, GeoVector yAxis, GeoVector zAxis, double semiAngle)
        {
            this.location = location;
            this.xAxis = xAxis;
            this.yAxis = yAxis;
            this.zAxis = zAxis; // xAxis and yAxis are assumed to have the same length (otherwise opening angle is meaningless)
            this.xAxis.Length = 1.0;
            this.yAxis.Length = 1.0;
            this.zAxis.Length = 1.0 / Math.Tan(semiAngle);
        }
        public ConicalSurfaceNP(GeoPoint location, GeoVector xAxis, GeoVector yAxis, GeoVector zAxis)
        {
            this.location = location;
            double f = 1.0 / xAxis.Length;
            this.xAxis = f * xAxis;
            this.yAxis = f * yAxis;
            this.zAxis = f * zAxis;
        }
        public override ISurface Clone()
        {
            return new ConicalSurfaceNP(location, xAxis, yAxis, zAxis);
        }
        public override void CopyData(ISurface copyFrom)
        {
            if (copyFrom is ConicalSurfaceNP cnp)
            {
                location = cnp.location;
                xAxis = cnp.xAxis;
                yAxis = cnp.yAxis;
                zAxis = cnp.zAxis;
            }
        }
        public override ICurve FixedU(double u, double vMin, double vMax)
        {
            return Make3dCurve(new Line2D(new GeoPoint2D(u, vMin), new GeoPoint2D(u, vMax)));
        }
        public override ICurve FixedV(double v, double uMin, double uMax)
        {
            return Make3dCurve(new Line2D(new GeoPoint2D(uMin, v), new GeoPoint2D(uMax, v)));
        }
        public override ISurface GetModified(ModOp m)
        {   // m must be orthogonal
            return new ConicalSurfaceNP(m * location, m * xAxis, m * yAxis, m * zAxis);
        }
        public override void Modify(ModOp m)
        {
            location = m * location;
            xAxis = m * xAxis;
            yAxis = m * yAxis;
            zAxis = m * zAxis;
            implicitPolynomial = null;
            boxedSurfaceEx = null;
        }
        public override ICurve Make3dCurve(ICurve2D curve2d)
        {
            if (curve2d is Line2D l2d)
            {
                if (Math.Abs(Geometry.DistPL(GeoPoint2D.Origin, l2d.StartPoint, l2d.EndPoint)) < Precision.eps)
                {
                    double p0 = l2d.PositionOf(GeoPoint2D.Origin);
                    if (p0 > 1e-6 && p0 < 1 - 1e-6)
                    {
                        // the line passes through the apex: we have to split this edge and make a polyline
                        return Polyline.FromPoints(new GeoPoint[] { PointAt(l2d.StartPoint), location, PointAt(l2d.EndPoint) });
                    }
                    else
                    {
                        return Line.TwoPoints(PointAt(l2d.StartPoint), PointAt(l2d.EndPoint));
                    }
                }
                else
                {
                    // the result is a parabola, which is exactly represented by a BSpline of degree 2 with 3 poles
                    BSpline parabola = BSpline.Construct();
                    parabola.ThroughPoints(new GeoPoint[] { PointAt(l2d.StartPoint), PointAt(l2d.PointAt(0.5)), PointAt(l2d.EndPoint) }, 2, false);
                    return parabola;
                }
            }
            else if (curve2d is Ellipse2D elli2d)
            {   // includes EllipseArc2D
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
            if (curve is Line line)
            {
                // this must be a line through the apex, otherwise there are no lines on the cone
                // if it is an extremely small line not through the apex, the result will also be a line, which
                // in reverse projection will give a very short ellipse
                return new Line2D(PositionOf(line.StartPoint), PositionOf(line.EndPoint));
            }
            else if (curve is Ellipse elli)
            {
                if (elli.IsCircle)
                {
                    GeoPoint2D sp2d = PositionOf(elli.StartPoint);
                    if (elli.IsArc)
                    { }
                    else
                    {
                        Arc2D a2d = new Arc2D(GeoPoint2D.Origin, sp2d.ToVector().Length, sp2d, sp2d, true);
                        GeoVector sdir = a2d.StartDirection.x * UDirection(sp2d) + a2d.StartDirection.y * VDirection(sp2d);
                        if (sdir * elli.StartDirection < 0) a2d.counterClock = false;
                        return a2d;
                    }
                }
                else
                {
                    // this is an intersection with a plane. 
                    GeoPoint2D[] fp2d = new GeoPoint2D[5];
                    double n = 5.0;
                    if (elli.IsArc) n = 4.0;
                    for (int i = 0; i < 5; i++)
                    {
                        fp2d[i] = PositionOf(elli.PointAt(i / n));
                    }
                    Ellipse2D elli2d = Ellipse2D.FromFivePoints(fp2d, !elli.IsArc); // returns both full ellipse and ellipse arc
                    if (elli2d != null) return elli2d;
                }
            }
            return base.GetProjectedCurve(curve, precision);
        }
        public override ICurve2D[] GetTangentCurves(GeoVector direction, double uMin, double uMax, double vMin, double vMax)
        {
            if (Precision.SameDirection(direction, zAxis, false))
                return Array.Empty<ICurve2D>();
            
            GeoPoint2D uv = PositionOf(location + zAxis + (direction ^ zAxis));
            GeoVector2D dir2d = uv.ToVector();
            dir2d.Length = Math.Max(Math.Max(Math.Abs(uMax), Math.Abs(uMin)), Math.Max(Math.Abs(vMax), Math.Abs(vMin))) * 1.1;
            ClipRect clr = new ClipRect(uMin, uMax, vMin, vMax);
            GeoPoint2D sp = GeoPoint2D.Origin - dir2d;
            GeoPoint2D ep = GeoPoint2D.Origin + dir2d;

            if (clr.ClipLine(ref sp, ref ep)) 
                return new ICurve2D[] { new Line2D(sp, ep) };
            
            return Array.Empty<ICurve2D>();
        }
        public override GeoPoint PointAt(GeoPoint2D uv)
        {
            //double vo = 0.0;
            //double vf = 1.0;
            //double l = Math.Sqrt(sqr(uv.y) + sqr(uv.x));
            //double u = vf * uv.x * (l - vo) / l;
            //double v = vf * uv.y * (l - vo) / l;
            return location + (Math.Sqrt(sqr(uv.y) + sqr(uv.x)) * zAxis) + uv.y * yAxis + uv.x * xAxis;
        }
        public override GeoPoint2D PositionOf(GeoPoint p)
        {
            GeoVector rb = Geometry.ReBase(p - location, xAxis, yAxis, zAxis);
            return new GeoPoint2D(rb.x, rb.y);
        }
        public override GeoVector UDirection(GeoPoint2D uv)
        {
            double u = uv.x;
            double v = uv.y;
            return u * zAxis / Math.Sqrt(sqr(v) + sqr(u)) + xAxis;
        }
        public override GeoVector VDirection(GeoPoint2D uv)
        {
            double u = uv.x;
            double v = uv.y;
            return v * zAxis / Math.Sqrt(sqr(v) + sqr(u)) + yAxis;
        }
        public override void DerivationAt(GeoPoint2D uv, out GeoPoint p, out GeoVector du, out GeoVector dv)
        {
            double u = uv.x;
            double v = uv.y;
            double sc3 = Math.Sqrt(sqr(v) + sqr(u));
            p = location + (sc3 * zAxis) + v * yAxis + u * xAxis;
            du = u * zAxis / sc3 + xAxis;
            dv = v * zAxis / sc3 + yAxis;
        }
        public override void Derivation2At(GeoPoint2D uv, out GeoPoint p, out GeoVector du, out GeoVector dv, out GeoVector duu, out GeoVector dvv, out GeoVector duv)
        {
            double u = uv.x;
            double v = uv.y;
            double sc3 = Math.Sqrt(sqr(v) + sqr(u));
            double sc2 = exp32(sqr(v) + sqr(u));
            GeoVector sc1 = zAxis / sc3;
            p = location + (sc3 * zAxis) + v * yAxis + u * xAxis;
            du = u * zAxis / sc3 + xAxis;
            dv = v * zAxis / sc3 + yAxis;
            duu = sc1 - sqr(u) * zAxis / sc2;
            dvv = sc1 - sqr(v) * zAxis / sc2;
            duv = (-(u * v * zAxis)) / sc2;
        }
        public override ModOp2D ReverseOrientation()
        {   // flip x and y axis, keep z axis, left handed system
            (xAxis, yAxis) = (yAxis, xAxis);
            return new ModOp2D(0, 1, 0, 1, 0, 0);
        }
        public override bool IsUPeriodic => false;
        public override bool IsVPeriodic => false;
        public override bool UvChangesWithModification => true;
        public override bool SameGeometry(BoundingRect thisBounds, ISurface other, BoundingRect otherBounds, double precision, out ModOp2D firstToSecond)
        {
            if (other is ConicalSurfaceNP cnp)
            {
                if ((cnp.location | location) < precision && Precision.SameDirection(zAxis, cnp.zAxis, false)) // SameDirection is not with respect to precision!
                {
                    firstToSecond = ModOp2D.Null;
                    return true;
                }
                firstToSecond = ModOp2D.Null;
                return false;
            }
            return base.SameGeometry(thisBounds, other, otherBounds, precision, out firstToSecond);
        }
        public double OpeningAngle => 2 * Math.Atan2(xAxis.Length, zAxis.Length);

        public override GeoPoint2D[] PerpendicularFoot(GeoPoint fromHere)
        {
            try
            {
                Plane pln = new Plane(location, zAxis, fromHere - location);
                // in this plane the x-axis is the conical axis, the origin is the apex of the cone
                Angle dirA = OpeningAngle / 2.0;
                // this line through origin with angle dirA and -dirA are the envelope lines of the cone
                GeoPoint2D fromHere2d = pln.Project(fromHere);
                GeoPoint2D fp1 = Geometry.DropPL(fromHere2d, GeoPoint2D.Origin, new GeoVector2D(dirA));
                GeoPoint2D fp2 = Geometry.DropPL(fromHere2d, GeoPoint2D.Origin, new GeoVector2D(-dirA));
                return new GeoPoint2D[] { PositionOf(pln.ToGlobal(fp1)), PositionOf(pln.ToGlobal(fp2)) };
            }
            catch
            {   // fromHere is on the axis
                return Array.Empty<GeoPoint2D>();
            }
        }
        public override Polynom GetImplicitPolynomial()
        {
            if (implicitPolynomial == null)
            {
                PolynomVector w = zAxis ^ (location.ToVector() - PolynomVector.xyz); // the distance of a point to the axis
                Polynom l = zAxis * PolynomVector.xyz - zAxis * location.ToVector(); // the distance from location along the axis 
                //(w*w)/(l*l)==(xAxis*xAxis)/(zAxis*zAxis) == tan(half opening angle)
                implicitPolynomial = (w * w) - ((xAxis * xAxis) / (zAxis * zAxis)) * (l * l);
                double d1 = implicitPolynomial.Eval(PointAt(new GeoPoint2D(0.5, 0.5)));
                double d2 = implicitPolynomial.Eval(PointAt(new GeoPoint2D(10, -10)));
                GeoPoint p = PointAt(new GeoPoint2D(1, 0)) + GetNormal(new GeoPoint2D(1, 0)).Normalized; // a point with distance 1 from the cone
                double d = implicitPolynomial.Eval(p); // this should be 1
                implicitPolynomial = (1 / d) * implicitPolynomial; // OK, this makes a good scaling (but it is not the distance)
            }
            return implicitPolynomial;
        }
        public double DebugImplicit(GeoPoint p)
        {
            double sc22 = (2.0 * xAxis.z);
            double sc21 = 2.0 * xAxis.y;
            double sc20 = 2.0 * xAxis.x;
            double sc19 = 2.0 * sqr(zAxis.x);
            double sc18 = sc20 * zAxis.x;
            double sc17 = sc21 * zAxis.y;
            double c = (quad(zAxis.z) + sc22 * cube(zAxis.z) + (2.0 * sqr(zAxis.y) + sc17 + sc19 + sc18 + sqr(xAxis.z)) * sqr(zAxis.z) + (sc22 * sqr(zAxis.y) + sc21 * xAxis.z * zAxis.y + sc22 * sqr(zAxis.x) + sc20 * xAxis.z * zAxis.x) * zAxis.z + quad(zAxis.y) + sc21 * cube(zAxis.y) + (sc19 + sc18 + sqr(xAxis.y)) * sqr(zAxis.y) + (sc21 * sqr(zAxis.x) + sc20 * xAxis.y * zAxis.x) * zAxis.y + quad(zAxis.x) + sc20 * cube(zAxis.x) + sqr(xAxis.x) * sqr(zAxis.x)) / (sqr(zAxis.z) + sc22 * zAxis.z + sqr(zAxis.y) + sc17 + sqr(zAxis.x) + sc18 + sqr(xAxis.z) + sqr(xAxis.y) + sqr(xAxis.x));
            if ((xAxis ^ yAxis) * zAxis < 0) c = -c;

            // z * (-2 * location_z * zAxis_z ^ 2 + (-2 * location_y * zAxis_y - 2 * location_x * zAxis_x) * zAxis_z + 2 * y * zAxis_y * zAxis_z + 2 * x * zAxis_x * zAxis_z + 2 * c * location_z) + z ^ 2 * (zAxis_z ^ 2 - c) + location_z ^ 2 * zAxis_z ^ 2 + y * (-2 * location_z * zAxis_y * zAxis_z - 2 * location_y * zAxis_y ^ 2 + 2 * x * zAxis_x * zAxis_y - 2 * location_x * zAxis_x * zAxis_y + 2 * c * location_y) + x * (-2 * location_z * zAxis_x * zAxis_z - 2 * location_y * zAxis_x * zAxis_y - 2 * location_x * zAxis_x ^ 2 + 2 * c * location_x) + (2 * location_y * location_z * zAxis_y + 2 * location_x * location_z * zAxis_x) * zAxis_z + y ^ 2 * (zAxis_y ^ 2 - c) + location_y ^ 2 * zAxis_y ^ 2 + 2 * location_x * location_y * zAxis_x * zAxis_y + x ^ 2 * (zAxis_x ^ 2 - c) + location_x ^ 2 * zAxis_x ^ 2 - c * location_z ^ 2 - c * location_y ^ 2 - c * location_x ^ 2
            return sqr(zAxis.z * (p.z - location.z) + zAxis.y * (p.y - location.y) + zAxis.x * (p.x - location.x)) - c * (sqr(p.z - location.z) + sqr(p.y - location.y) + sqr(p.x - location.x));
        }
        public override IPropertyEntry GetPropertyEntry(IFrame frame)
        {
            List<IPropertyEntry> se = new List<IPropertyEntry>();
            GeoPointProperty csLocation = new GeoPointProperty(frame,"ConicalSurface.Location");
            csLocation.ReadOnly = true;
            csLocation.OnGetValue = () => this.location;
            se.Add(csLocation);
            GeoVectorProperty dirX = new GeoVectorProperty(frame, "ConicalSurface.DirectionX");
            dirX.ReadOnly = true;
            dirX.IsAngle = false;
            dirX.OnGetValue = () => xAxis;
            se.Add(dirX);
            GeoVectorProperty dirY = new GeoVectorProperty(frame, "ConicalSurface.DirectionY");
            dirY.ReadOnly = true;
            dirY.IsAngle = false;
            dirY.OnGetValue = () => yAxis;
            se.Add(dirY);
            AngleProperty openingAngle = new AngleProperty(frame, "ConicalSurface.OpeningAngle");
            openingAngle.ReadOnly = true;
            openingAngle.OnGetValue = () => OpeningAngle;
            se.Add(openingAngle);
            return new GroupProperty("ConicalSurface", se.ToArray());
        }
        GeoPoint ICone.Apex { get => location; set => throw new NotImplementedException(); }
        GeoVector ICone.Axis { get => zAxis; set => throw new NotImplementedException(); }
        Angle ICone.OpeningAngle { get => OpeningAngle; set => throw new NotImplementedException(); }

        #region IJsonSerialize
        protected ConicalSurfaceNP() { }
        public void GetObjectData(IJsonWriteData data)
        {
            data.AddProperty("Location", location);
            data.AddProperty("XAxis", xAxis);
            data.AddProperty("YAxis", yAxis);
            data.AddProperty("ZAxis", zAxis);
        }
        public void SetObjectData(IJsonReadData data)
        {
            location = data.GetProperty<GeoPoint>("Location");
            xAxis = data.GetProperty<GeoVector>("XAxis");
            yAxis = data.GetProperty<GeoVector>("YAxis");
            zAxis = data.GetProperty<GeoVector>("ZAxis");
        }
        #endregion
        #region ISerializable
        protected ConicalSurfaceNP(SerializationInfo info, StreamingContext context)
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
        #endregion
    }
}
