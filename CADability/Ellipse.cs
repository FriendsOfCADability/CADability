using CADability.Attribute;
using CADability.Curve2D;
using CADability.UserInterface;
using System;
using System.Collections.Generic;
#if WEBASSEMBLY
using CADability.WebDrawing;
using Point = CADability.WebDrawing.Point;
#else
using System.Drawing;
using Point = System.Drawing.Point;
#endif
using System.Runtime.Serialization;
using System.Threading;
using MathNet.Numerics.LinearAlgebra.Double;

namespace CADability.GeoObject
{


	public class EllipseException : ApplicationException
	{
		public enum tExceptionType { EllipseIsNotACircle, SetCircle3PointsFailed, SetCirclePlane2PointsFailed, SetStartPointFailed, SetEllipse2PointsDirectionsFailed };
		public tExceptionType ExceptionType;
		public EllipseException(tExceptionType ExceptionType)
		{
			this.ExceptionType = ExceptionType;
		}
	}

	internal class EllipseData2D
	{   // projizierte Daten in der 2D Welt
		public GeoPoint2D center;   // der Mittelpunkt
		public GeoVector2D majax; // 1. Hauptachsenrichtung und Länge
		public GeoVector2D minax; // 2. Hauptachsenrichtung und Länge
		public double majrad, minrad; // die beiden Radien der Hauptachsen
		public Angle majdir; // Winkel der 1. Hauptachse
		public double majaxsin, majaxcos; // Sinus und Cosinus der 1. Hauptachse
		public GeoPoint2D right, left, top, bottom; // die 4 Extrempunkte der Ellipse
		public double startAng; // der Winkel in der 2D Ellipse, bei dem es losgeht
		public double sweepAng; // der Winkel in der 2D Ellipse, der überstrichen wird
		public double startParameter; // der Parameter der 2D Ellipse, bei dem es losgeht
		public double sweepParameter; // der Parameter der 2D Ellipse, der überstrichen wird
		private ICurve2D curve2D; // die 2d Kurve, wird erst bei Bedarf berechnet
		public ICurve2D Curve2D
		{
			get
			{
				if (curve2D == null)
				{
					bool IsArc = (sweepAng != 2.0 * Math.PI && sweepAng != -2.0 * Math.PI) || startParameter != 0.0;
					if (isLine) return new Line2D(lineStart, lineEnd);
					else
					{
						if (Math.Abs(majrad - minrad) < Precision.eps)
						{   // es ist ein Kreis oder Kreisbogen
							if (IsArc)
							{
								// es ist ein Bogen, jedoch müssen wir noch die Richtung der Hauptachse berücksichtigen
								Angle sa = startAng + majdir;
								curve2D = new Arc2D(center, majrad, sa, sweepAng);
							}
							else
							{
								Angle sa = startAng + majdir;
								if (sweepParameter > 0)
									curve2D = new Arc2D(center, majrad, sa, SweepAngle.Full);
								else
									curve2D = new Arc2D(center, majrad, sa, SweepAngle.FullReverse);
								//curve2D = new Circle2D(center, (majrad + minrad) / 2.0);
								//(curve2D as Circle2D).counterClock = sweepAng > 0;
							}
						}
						else
						{   // eine 2d Ellipse
							if (IsArc)
							{
								curve2D = new EllipseArc2D(center, majax, minax, startParameter, sweepParameter, left, right, bottom, top);
							}
							else
							{
								curve2D = new Ellipse2D(center, majax, minax, sweepParameter > 0.0, left, right, bottom, top);
							}
						}
					}
				}
				return curve2D;
			}
		}
		public bool isLine; // die Projektion degeneriert zu einer Linie
		public GeoPoint2D lineStart, lineEnd; // Start- und Endpunkt der Linie
	}

	/// <summary>
	/// 
	/// </summary>
	[Serializable]
	public class Ellipse : IGeoObjectImpl, IColorDef, ILineWidth, ILinePattern, ICurve, ISerializable,
			IExtentedableCurve, ISimpleCurve, IExplicitPCurve3D, IExportStep, IJsonSerialize
	{
		private Plane plane; // die Ebene, in deren Ursprung die Ellipse liegt. Damit ist Mittelpunkt und Achsenwinkel gegeben
		private double majorRadius, minorRadius; // die Radien in Richtung directionX bzw. directionY der Ebene
		private double startParameter, sweepParameter; // Startwinkel und Überstreichnungswinkel
		private BoundingCube extent; // cache für den extent
		private object lockApproximationRecalc;
		private double currentPrecision;
		private GeoPoint[] currentApproximation;
		private Dictionary<Projection, EllipseData2D> projectionData; // die zu einer Projektion gehörenden 2D Daten
		new private class Changing : IGeoObjectImpl.Changing
		{   // TODO: sicherstellen, dass überall using(new Changing ... verwendet wird
			public Changing(Ellipse ellipse)
				: base(ellipse)
			{
				ellipse.projectionData.Clear();
				ellipse.currentApproximation = null;
				ellipse.extent = BoundingCube.EmptyBoundingCube;
			}
			public Changing(Ellipse ellipse, string PropertyName)
				: base(ellipse, PropertyName)
			{
				ellipse.projectionData.Clear();
				ellipse.currentApproximation = null;
				ellipse.extent = BoundingCube.EmptyBoundingCube;
			}
			public Changing(Ellipse ellipse, string MethodOrPropertyName, params object[] Parameters)
				: base(ellipse, MethodOrPropertyName, Parameters)
			{
				ellipse.projectionData.Clear();
				ellipse.currentApproximation = null;
				ellipse.extent = BoundingCube.EmptyBoundingCube;
			}
			public Changing(Ellipse ellipse, Type interfaceForMethod, string MethodOrPropertyName, params object[] Parameters)
				: base(ellipse, interfaceForMethod, MethodOrPropertyName, Parameters)
			{
				ellipse.projectionData.Clear();
				ellipse.currentApproximation = null;
				ellipse.extent = BoundingCube.EmptyBoundingCube;
			}
			public Changing(Ellipse ellipse, bool noUndo, bool onlyAttribute, string MethodOrPropertyName, params object[] Parameters)
				: base(ellipse, noUndo, onlyAttribute, MethodOrPropertyName, Parameters)
			{
				if (!onlyAttribute) ellipse.projectionData.Clear();
				ellipse.currentApproximation = null;
				ellipse.extent = BoundingCube.EmptyBoundingCube;
			}
		}
		internal virtual EllipseData2D GetProjectionData(Projection pr)
		{
			EllipseData2D res;
			if (!projectionData.TryGetValue(pr, out res))
			{
				res = CalculateProjectionData(pr.ProjectionPlane);
				projectionData[pr] = res;
			}
			return res;
			// return CalculateProjectionData(pr.ProjectionPlane);
			// ACHTUNG, hier war ein cache, der aber irgendwie nicht funktionierte
			// vielleicht muss man projectionplane nehmen und ein Plane.GetHashCode und Plane.Equals
			// implementieren
			//EllipseData2D res = (EllipseData2D)projectionData[pr];
			//if (res==null)
			//{ 
			//    res = CalculateProjectionData(pr.ProjectionPlane);
			//    projectionData[pr] = res;
			//}
			//return res;
		}
		internal virtual EllipseData2D CalculateProjectionData(Plane pr)
		{
			EllipseData2D res = new EllipseData2D();

			res.center = pr.Project(plane.Location);
			GeoVector2D majaxorg = pr.Project(majorRadius * plane.DirectionX);
			GeoVector2D minaxorg = pr.Project(minorRadius * plane.DirectionY);

			if (pr.Normal.IsPerpendicular(plane.Normal))
			{   // die Projektion führt auf eine Linie, Ansicht genau von der Seite
				res.isLine = true;
				double u = TangentParameter(pr.Normal);
				// bei u und u+pi sind die Extrempunkte der Ellipse in Bezug zu dieser Projektion
				if (Math.Abs(sweepParameter) == 2.0 * Math.PI)
				{   // volle Ellipse, deshalb gelten die beiden Extrempunkte ohne Ausnahme
					res.lineStart = pr.Project(this.Point(u));
					res.lineEnd = pr.Project(this.Point(u + Math.PI));
				}
				else
				{
					double dmin;
					double dmax;
					GeoVector2D LineDir = pr.Project(plane.Normal ^ pr.Normal);
					// die gesuchte Linie liegt jedenfalls auf der Linie durch den projizierten
					// Mittelpunkt mit der Richtung LineDir. Gesucht sind jetzt die Extrempunkte
					// zusammengesetzt aus Startpunkt, Endpunkt und Tangentenpunkten, wenn innerhalb
					// des Bogens.
					GeoPoint2D startPoint2d = pr.Project(StartPoint);
					GeoPoint2D endPoint2d = pr.Project(EndPoint);
					dmin = dmax = Geometry.LinePar(res.center, LineDir, startPoint2d);
					double d = Geometry.LinePar(res.center, LineDir, endPoint2d);
					List<GeoPoint2D> point2d = new List<GeoPoint2D>();
					point2d.Add(startPoint2d);
					point2d.Add(endPoint2d);
					if (d < dmin) dmin = d;
					if (d > dmax) dmax = d;
					Angle spar = new Angle(startParameter);
					if (spar.Sweeps(new SweepAngle(sweepParameter), new Angle(u)))
					{
						d = Geometry.LinePar(res.center, LineDir, pr.Project(Point(u)));
						if (d < dmin)
						{
							dmin = d;
							point2d.Insert(1, pr.Project(Point(u)));
						}
						if (d > dmax)
						{
							dmax = d;
							point2d.Insert(1, pr.Project(Point(u)));
						}
					}
					if (spar.Sweeps(new SweepAngle(sweepParameter), new Angle(u + Math.PI)))
					{
						d = Geometry.LinePar(res.center, LineDir, pr.Project(Point(u + Math.PI)));
						if (d < dmin)
						{
							dmin = d;
							point2d.Insert(1, pr.Project(Point(u + Math.PI)));
						}
						if (d > dmax)
						{
							dmax = d;
							point2d.Insert(1, pr.Project(Point(u + Math.PI)));
						}
					}
					//res.lineStart = res.center + dmin * LineDir;
					//res.lineEnd = res.center + dmax * LineDir;
					// points wären die Punkte für eine 2d polyline, die den ganzen Bogen überdecken
					// jetzt liefern wir erstmal nur die innere Linie, die zu kurz sein kann. Wichtig ist aber, dass
					// Start- und Endpunkt stimmen, sonst wird ein Pfad wenn nach 2d projiziert eine exception werfen
					// da er nicht zusammenhängt. Mir ist nicht mehr klar, wozu man die ganze Linie braucht.
					// wenn sie gebraucht wird müssen wir mit einer Polylinie arbeiten.
					res.lineStart = startPoint2d;
					res.lineEnd = endPoint2d;
				}
			}
			else
			{   // es entsteht eine Ellipse
				res.isLine = false;
				Geometry.PrincipalAxis(res.center, majaxorg, minaxorg, out res.majax, out res.minax, out res.left, out res.right, out res.bottom, out res.top, true);
				res.majrad = res.majax.Length;
				res.majdir = res.majax.Angle;
				res.minrad = res.minax.Length;
				res.majaxcos = Math.Cos(res.majdir);
				res.majaxsin = Math.Sin(res.majdir);

				GeoVector2D StartDir = pr.Project(StartPoint) - res.center;
				GeoVector2D EndDir = pr.Project(EndPoint) - res.center;
				GeoPoint2D dbgs = pr.Project(StartPoint);
				GeoPoint2D dbge = pr.Project(EndPoint);
				bool ccw = sweepParameter > 0.0;
				GeoVector n = pr.ToLocal(this.plane.Normal);
				if (n.z < 0) ccw = !ccw;
				// GDI+ will wirklich den StartWinkel wissen, also nicht den Ellipsenparameter
				res.startAng = new Angle((double)StartDir.Angle - (double)res.majdir);
				// die Richtung der Ellipse hängt nun einsrseits davon ab, in welche
				// Richtung sweepParameter geht und andererseits, ob man die Ellipse
				// von vorne oder von hinten sieht.
				if (IsArc || this.startParameter != 0.0)
				{
					res.sweepAng = (double)new SweepAngle(StartDir.Angle, EndDir.Angle, ccw);
					if (Math.Abs(res.sweepAng) < 1e-6 && Math.Abs(this.sweepParameter) > Math.PI)
					{
						// Sonderfall: ein fast Vollkreis wird nicht als solcher erkannt und liefert 
						// allerdings exakt 0.0 für sweepAng
						if (ccw) res.sweepAng = Math.Abs(this.sweepParameter);
						else res.sweepAng = -Math.Abs(this.sweepParameter);
					}
				}
				else
				{
					if (ccw) res.sweepAng = SweepAngle.Full;
					else res.sweepAng = SweepAngle.FullReverse;
				}

				// für andere Zwecke braucht man Start- und Endparameter:
				double[,] a = new double[2, 2];
				double[] b = new double[2];
				a[0, 0] = res.majax.x;
				a[0, 1] = res.minax.x;
				a[1, 0] = res.majax.y;
				a[1, 1] = res.minax.y;
				b[0] = StartDir.x;
				b[1] = StartDir.y;
				Vector x = (Vector)DenseMatrix.OfArray(a).Solve(new DenseVector(b));
				if (x.IsValid())
				{
					res.startParameter = Math.Atan2(x[1], x[0]);
				}
				a[0, 0] = res.majax.x;
				a[0, 1] = res.minax.x;
				a[1, 0] = res.majax.y;
				a[1, 1] = res.minax.y;
				b[0] = EndDir.x;
				b[1] = EndDir.y;
				x = (Vector)DenseMatrix.OfArray(a).Solve(new DenseVector(b));
				if (x.IsValid())
				{
					double endpar = Math.Atan2(x[1], x[0]);
					SweepAngle sw;
					// ccw ist doch schon umgedreht, wenn n.z<0 ist. Deshalb hier nicht nochmal umdrehen
					// siehe z.B. CylCoord.cdb, dort ist die aufgeteilte Ellipse sonst nicht mehr pickbar
					//if (n.z < 0)
					//{
					//    sw = new SweepAngle(new Angle(endpar), new Angle(res.startParameter), ccw);
					//    res.startParameter = endpar;
					//}
					//else
					//{
					sw = new SweepAngle(new Angle(res.startParameter), new Angle(endpar), ccw);
					//}
					res.sweepParameter = sw.Radian;
					if (Math.Abs(res.sweepParameter) < 1e-6 && Math.Abs(res.sweepAng) > Math.PI)
					{
						// Sonderfall: ein fast Vollkreis wird nicht als solcher erkannt und liefert 
						// allerdings exakt 0.0 für sweepAng
						if (ccw) res.sweepParameter = Math.Abs(res.sweepAng);
						else res.sweepParameter = -Math.Abs(res.sweepAng);
					}
				}
			}
			return res;
		}

		internal static Ellipse FromFivePoints(GeoPoint[] fp3d, bool isFull)
		{
			if (fp3d.Length != 5) return null;
			Plane pln = Plane.FromPoints(fp3d, out double maxDist, out bool isLinear);
			if (maxDist < Precision.eps && !isLinear)
			{
				GeoPoint2D[] fp2d = new GeoPoint2D[5];
				for (int i = 0; i < 5; i++)
				{
					fp2d[i] = pln.Project(fp3d[i]);
				}
				Ellipse2D e2d = Ellipse2D.FromFivePoints(fp2d);
				if (e2d != null)
				{
					Ellipse elli = e2d.MakeGeoObject(pln) as Ellipse;
					if (elli != null)
					{
						// get the correct orientation
						double mindist = double.MaxValue;
						double p0 = elli.PositionOf(fp3d[0]);
						for (int i = 1; i < 5; i++)
						{
							double p1 = elli.PositionOf(fp3d[i]);
							if (Math.Abs(p1 - p0) < Math.Abs(mindist))
							{
								mindist = p1 - p0;
							}
							p0 = p1;
						}
						if (mindist < 0) (elli as ICurve).Reverse();
						if (!isFull)
						{
							double sp = elli.PositionOf(fp3d[0]);
							double ep = elli.PositionOf(fp3d[4]);
							double mp = elli.PositionOf(fp3d[2]);
							if (sp < ep)
							{
								if (sp < mp && mp < ep) elli.Trim(sp, ep);
								else elli.Trim(ep, sp);
							}
							else
							{   // to be tested!
								if (sp < mp && mp < ep) elli.Trim(ep, sp);
								else elli.Trim(sp, ep);
							}
						}
						return elli;
					}
				}
			}
			return null;
		}

		internal BSpline ToBSpline()
		{
			BSpline bsp = BSpline.Construct();
			double w = Math.Sqrt(2.0) / 2.0;
			bsp.SetData(2, new GeoPoint[]
				{ new GeoPoint(1,0,0),
				new GeoPoint(1,1,0),
				new GeoPoint(0,1,0),
				new GeoPoint(-1,1,0),
				new GeoPoint(-1,0,0),
				new GeoPoint(-1,-1,0),
				new GeoPoint(0,-1,0),
				new GeoPoint(1,-1,0),
				new GeoPoint(1,0,0)
				},
				new double[] { 1, w, 1, w, 1, w, 1, w, 1 },
				new double[] { 0, 0.25, 0.5, 0.75, 1 }, new int[] { 3, 2, 2, 2, 3 }, false);
			if (IsArc)
			{
				if (sweepParameter < 0)
				{
					ModOp reflect = new ModOp(1.0, 0.0, 0.0, 0.0, 0.0, -1.0, 0.0, 0.0, 0.0, 0.0, 1.0, 0.0);
					bsp.Modify(reflect); // Richtung umgederht
				}
				ModOp rotate = ModOp.Rotate(GeoVector.ZAxis, startParameter);
				bsp.Modify(rotate); // Richtung umgederht
				double sp = 0.0;
				double ep = (bsp as ICurve).PositionOf(new GeoPoint(Math.Cos(startParameter + sweepParameter), Math.Sin(startParameter + sweepParameter)));
				(bsp as ICurve).Trim(sp, ep);
			}
			try
			{
				ModOp m = ModOp.Fit(new GeoPoint[] { GeoPoint.Origin, new GeoPoint(1.0, 0.0, 0.0), new GeoPoint(0.0, 1.0, 0.0) },
					new GeoPoint[] { Center, Center + MajorAxis, Center + MinorAxis }, true);
				bsp.Modify(m);
				return bsp;
			}
			catch (ModOpException)
			{
				return null;
			}
		}
		#region polymorph construction
		public delegate Ellipse ConstructionDelegate();
		public static ConstructionDelegate Constructor;
		public static Ellipse Construct()
		{
			if (Constructor != null) return Constructor();
			return new Ellipse();
		}
		public delegate void ConstructedDelegate(Ellipse justConstructed);
		public static event ConstructedDelegate Constructed;
		#endregion
		//redundante Daten
		/*		private double[] m_Rad;
                // Die flogenden Funktionen dienen im Wesentlichen dem Strichlieren von Ellipsenbögen.
                    // Sie sind nicht exakt, aber für die Darstellung genügt es.

                    // man könnte natürlich auch eine Formel nehmen für die Indexumrechnung. Aber so
                    // gehts bestimmt schneller, und obs mehr Platz braucht ist nicht eindeutig zu beantworten
                private static int[] s_Ind = new int[]{
                    0,1,2,3,4,5,6,7,7,6,5,4,3,2,1,0,0,1,2,3,4,5,6,7,7,6,5,4,3,2,1,0,
                    0,1,2,3,4,5,6,7,7,6,5,4,3,2,1,0,0,1,2,3,4,5,6,7,7,6,5,4,3,2,1,0};
                private static double[] s_Sina2 = new double[]{
                    sqr(sin(0*math.pi/16 + pi/32)),
                    sqr(sin(1*pi/16 + pi/32)),
                    sqr(sin(2*pi/16 + pi/32)),
                    sqr(sin(3*pi/16 + pi/32)),
                    sqr(sin(4*pi/16 + pi/32)),
                    sqr(sin(5*pi/16 + pi/32)),
                    sqr(sin(6*pi/16 + pi/32)),
                    sqr(sin(7*pi/16 + pi/32))};
                private static double s_Cosa2 = new double[]{
                    sqr(cos(0*pi/16 + pi/32)),
                    sqr(cos(1*pi/16 + pi/32)),
                    sqr(cos(2*pi/16 + pi/32)),
                    sqr(cos(3*pi/16 + pi/32)),
                    sqr(cos(4*pi/16 + pi/32)),
                    sqr(cos(5*pi/16 + pi/32)),
                    sqr(cos(6*pi/16 + pi/32)),
                    sqr(cos(7*pi/16 + pi/32))};
                internal void SetRad()
                {
                    // der Quadrant wird in 8 Stücke geteilt. Für jedes Stück wird ein Radius bestimmt
                    // so dass die Bogenlänge des Kreises mit diesem Radius ungefähr mit der Bogenlänge
                    // des Ellipsenbogens in diesem Winkelsegment übereinstimmt
                    if (!m_Rad) m_Rad = new double[8];
            double uu = 0.0;
            for (int i=0; i<8; ++i) 
            {
                // double a = i*pi/16 + pi/32;
                m_Rad[i] = s_Sina2[i]*m_MajorRadius + s_Cosa2[i]*m_MinorRadius;
                uu += pi/16*m_Rad[i];
            }
            if (m_MajorRadius==0.0) return;
            if (m_MinorRadius==0.0) return;
            uu *= 4;
            // Nachbesserung: Nach einer Formel wird der exakte Umfang der Ellipse
            // berechnet. Es handelt sich dabei um eine Reihenentwicklung, die umso
            // schlechter konvergiert, je ungleicher das Verhältnis zwischen den
            // Radien ist. (http://www.mathematik-online.de/F57.htm)
            double d = 1.0;
            double a = fmax(m_MajorRadius,m_MinorRadius);
            double b = fmin(m_MajorRadius,m_MinorRadius);
            double e2 = (sqr(a)-sqr(b))/sqr(a); // e^2
            double sum = 1.0;
            double br = 1.0;
            double en = 1.0;
            double diff;
            for (int n=1; n<200; ++n)
            {
                br *= (double)(2*n-1)/(double)(2*n);
                en = en*e2;
                diff = 1.0/(double)(2*n-1)*sqr(br)*en;
                sum = sum - diff;
                if (diff<sum*1e-13) break;
            }
            double u = 2*a*pi*sum;
            double f = u/uu;
            // mit dem Korrekturfaktor werden die 8 Radien multipliziert.
            // Damit wird die Länge für Winkel bei exakten Vielfachen von pi/16 sehr genau
            // und dazwischen können Fehler auftreten
            for (i=0; i<8; ++i) m_Rad[i] *= f;
        }

        internal void UpdatePoints()
        {	// aus den gegebenen Hauptdaten werden die redundanten Daten berechnet
            double s = sin(m_MajorAngle);
            double c = cos(m_MajorAngle);

            m_dxMajor = m_MajorRadius*c;
            m_dyMajor = m_MajorRadius*s;
            m_dxMinor = -m_MinorRadius*s;
            m_dyMinor = m_MinorRadius*c;
            m_Major = GeoPoint(m_Center,m_dxMajor,m_dyMajor);
            m_Minor = GeoPoint(m_Center,m_dxMinor,m_dyMinor);

            if (fabs(s)<1e-13)
            {	// die Ellipse befindet sich in horizontaler Lage
                m_Right = GeoPoint(m_Center.x+m_MajorRadius,m_Center.y);
                m_Left = GeoPoint(m_Center.x-m_MajorRadius,m_Center.y);
                m_Top = GeoPoint(m_Center.x,m_Center.y+m_MinorRadius);
                m_Bottom = GeoPoint(m_Center.x,m_Center.y-m_MinorRadius);
            } 
            else if (fabs(c)<1e-13)
            {	// die Ellipse befindet sich in vertikaler Lage
                m_Right = GeoPoint(m_Center.x+m_MinorRadius,m_Center.y);
                m_Left = GeoPoint(m_Center.x-m_MinorRadius,m_Center.y);
                m_Top = GeoPoint(m_Center.x,m_Center.y+m_MajorRadius);
                m_Bottom = GeoPoint(m_Center.x,m_Center.y-m_MajorRadius);
            } 
            else
            {
                GeoPoint[] p= new GeoPoint[4];
                double m1 = c/s;
                double m2 = -1/m1;
                double a1 = sqrt(sqr(m_MinorRadius) + sqr(m_MajorRadius)*sqr(m1));
                double a2 = sqrt(sqr(m_MinorRadius) + sqr(m_MajorRadius)*sqr(m2));
                p[0].x = m1*sqr(m_MajorRadius)/a1;
                p[0].y = m1*p[0].x - (a1);
                p[1].x = - m1*sqr(m_MajorRadius)/a1;
                p[1].y = m1*p[1].x + (a1);
                p[2].x = m2*sqr(m_MajorRadius)/a2;
                p[2].y = m2*p[2].x - (a2);
                p[3].x = - m2*sqr(m_MajorRadius)/a2;
                p[3].y = m2*p[3].x + (a2);

                double xmin = DBL_MAX;
                double xmax = -DBL_MAX;
                double ymin = DBL_MAX;
                double ymax = -DBL_MAX;
                int left,right,top,bottom;
                left = right = top = bottom = 0;
                for (int i=0; i<4; ++i)
                {
                    p[i] = GeoPoint(m_Center.x+c*p[i].x-s*p[i].y,m_Center.y+s*p[i].x+c*p[i].y);
                    if (p[i].x<xmin)
                    {
                        xmin = p[i].x;
                        left = i;
                    }
                    if (p[i].x>xmax)
                    {
                        xmax = p[i].x;
                        right = i;
                    }
                    if (p[i].y<ymin)
                    {
                        ymin = p[i].y;
                        bottom = i;
                    }
                    if (p[i].y>ymax)
                    {
                        ymax = p[i].y;
                        top = i;
                    }
                }
                m_Right = p[right];
                m_Left = p[left];
                m_Top = p[top];
                m_Bottom = p[bottom];
            }
            SetRad();
        }

        internal void RecalcAxesAndRadius()
        {
            // sog. Hauptachsentransformation: gesucht sind die beiden Hauptachsen, die 
            // senkrecht aufeinander stehen und die größte und kleinste Länge haben
            // die Hauptachsenvektoren
            GeoPoint p1 = new GeoPoint(m_Major.x-m_Center.x,m_Major.y-m_Center.y);
            GeoPoint p2 = new GeoPoint(m_Minor.x-m_Center.x,m_Minor.y-m_Center.y);

            double a,b,g1,g2;
            a = p1.x*p2.x+p1.y*p2.y;
            b = sqr(p2.x)+sqr(p2.y)-sqr(p1.x)-sqr(p1.y);
            g1 = 2*a;
            g2 = b + sqrt(sqr(b)+4*sqr(a));
            if (fabs(g1)+fabs(g2)>0)
            {
                if (fabs(g2)>fabs(g1))
                {
                    a = sqrt(1/(1+sqr(g1/g2)));
                    b = -g1*a/g2;
                } 
                else
                {
                    b = sqrt(1/(1+sqr(g2/g1)));
                    a = -g2*b/g1;
                }
                m_dxMajor = a*p1.x+b*p2.x;
                m_dyMajor = a*p1.y+b*p2.y;
                m_dxMinor = a*p2.x-b*p1.x;
                m_dyMinor = a*p2.y-b*p1.y;
                m_Major.x = m_dxMajor + m_Center.x;
                m_Major.y = m_dyMajor + m_Center.y;
                m_Minor.x = m_dxMinor + m_Center.x;
                m_Minor.y = m_dyMinor + m_Center.y;
            }
            // ansonsten sind die Achsen schon senkrecht
            // es ist aber nicht garantiert, dass m_Major größer ist als m_Minor
            m_MajorRadius = dist(m_Major,m_Center);
            m_MinorRadius = dist(m_Minor,m_Center);
            m_MajorAngle = angle(m_Major,m_Center);
        }
*/
		protected Ellipse()
		{
			lockApproximationRecalc = new object();
			projectionData = new Dictionary<Projection, EllipseData2D>();
			extent = BoundingCube.EmptyBoundingCube;
			plane = new Plane(Plane.StandardPlane.XYPlane, 0.0);
			if (Constructed != null) Constructed(this);
		}

		public void SetPlaneRadius(Plane plane, double majorRadius, double minorRadius)
		{
			using (new Changing(this, "SetPlaneRadius", this.plane, this.majorRadius, this.minorRadius))
			{
				this.plane = plane;
				this.majorRadius = majorRadius;
				this.minorRadius = minorRadius;
			}
		}
		public void SetCirclePlaneCenterRadius(Plane plane, GeoPoint center, double majorRadius)
		{
			using (new Changing(this, "SetCirclePlaneCenterRadius", this.plane, this.plane.Location, this.majorRadius))
			{
				this.plane = plane;
				this.plane.Location = center;
				this.majorRadius = majorRadius;
				this.minorRadius = majorRadius;
				this.startParameter = 0.0;
				this.sweepParameter = 2 * Math.PI;
			}
		}
		public void SetArcPlaneCenterRadius(Plane plane, GeoPoint center, double majorRadius)
		{
			using (new Changing(this, "SetArcPlaneCenterRadius", this.plane, this.plane.Location, this.majorRadius))
			{
				this.plane = plane;
				this.plane.Location = center;
				this.majorRadius = majorRadius;
				this.minorRadius = majorRadius;
			}
		}
		public void SetArcPlaneCenterRadiusAngles(Plane plane, GeoPoint center, double majorRadius, double startParameter, double sweepParameter)
		{
			using (new Changing(this, "SetArcPlaneCenterRadius", this.plane, this.plane.Location, this.majorRadius, this.startParameter, this.sweepParameter))
			{
				this.plane = plane;
				this.plane.Location = center;
				this.majorRadius = majorRadius;
				this.minorRadius = majorRadius;
				this.startParameter = startParameter;
				this.sweepParameter = sweepParameter;
			}
		}
		public void SetArcPlaneCenterStartEndPoint(Plane alignTo, GeoPoint2D center, GeoPoint2D startPoint, GeoPoint2D endPoint, Plane pointsPlane, bool direction)
		{
			using (new Changing(this, "SetArcPlaneCenterRadius", this.plane, this.plane.Location, this.majorRadius, this.startParameter, this.sweepParameter))
			{
				GeoPoint centerGl = pointsPlane.ToGlobal(center);
				GeoPoint startPointGl = pointsPlane.ToGlobal(startPoint);
				GeoPoint endPointGl = pointsPlane.ToGlobal(endPoint);
				pointsPlane.Location = centerGl;
				pointsPlane.Align(alignTo, false, true);  // ausrichten an Drawingplane und gleiche Normalenrichtung
				this.plane = pointsPlane;
				startPoint = pointsPlane.Project(startPointGl);
				endPoint = pointsPlane.Project(endPointGl);
				this.majorRadius = Geometry.Dist(startPoint, GeoPoint2D.Origin);
				this.minorRadius = this.majorRadius;
				this.startParameter = new Angle(startPoint, GeoPoint2D.Origin);
				this.sweepParameter = new SweepAngle((Angle)this.startParameter, new Angle(endPoint, GeoPoint2D.Origin), direction);
			}
		}
		public void SetCircle3Points(GeoPoint circlePoint1, GeoPoint circlePoint2, GeoPoint circlePoint3, Plane alignTo)
		{
			using (new Changing(this, "SetArcPlaneCenterRadius", this.plane, this.plane.Location, this.majorRadius))
			{
				try
				{
					Plane P = new Plane(circlePoint1, circlePoint2, circlePoint3);
					P.Align(alignTo, false, true); // ausrichten an Drawingplane und gleiche Normalenrichtung
					GeoPoint2D circlePoint1_2D = P.Project(circlePoint1);
					GeoPoint2D circlePoint2_2D = P.Project(circlePoint2);
					GeoPoint2D circlePoint3_2D = P.Project(circlePoint3);
					GeoPoint2D center_2D = new GeoPoint2D(0, 0);

					if (Geometry.IntersectLL(new GeoPoint2D(circlePoint1_2D, circlePoint2_2D), (circlePoint1_2D - circlePoint2_2D).ToLeft(), new GeoPoint2D(circlePoint1_2D, circlePoint3_2D), (circlePoint1_2D - circlePoint3_2D).ToLeft(), out center_2D))
					{
						this.plane = P;
						this.plane.Location = P.ToGlobal(center_2D);
						this.majorRadius = Geometry.Dist(circlePoint2, this.Center);
						this.minorRadius = this.majorRadius;
						sweepParameter = 2.0 * Math.PI;

						double pos2 = this.PositionOf(circlePoint2);
						double pos3 = this.PositionOf(circlePoint3);
						if (pos2 > pos3) (this as ICurve).Reverse();

					}
				}
				catch (PlaneException)
				{
					throw new EllipseException(EllipseException.tExceptionType.SetCircle3PointsFailed);
				}
			}
		}
		public void SetArc3Points(GeoPoint circlePoint1, GeoPoint circlePoint2, GeoPoint circlePoint3, Plane alignTo)
		{
			using (new Changing(this, "SetArcPlaneCenterRadius", this.plane, this.plane.Location, this.majorRadius, this.startParameter, this.sweepParameter))
			{
				try
				{
					Plane P = new Plane(circlePoint1, circlePoint2, circlePoint3);
					P.Align(alignTo, false, true); // ausrichten an Drawingplane und gleiche Normalenrichtung
					GeoPoint2D circlePoint1_2D = P.Project(circlePoint1);
					GeoPoint2D circlePoint2_2D = P.Project(circlePoint2);
					GeoPoint2D circlePoint3_2D = P.Project(circlePoint3);
					GeoPoint2D center_2D = new GeoPoint2D(0, 0);

					if (Geometry.IntersectLL(new GeoPoint2D(circlePoint1_2D, circlePoint2_2D), (circlePoint1_2D - circlePoint2_2D).ToLeft(), new GeoPoint2D(circlePoint1_2D, circlePoint3_2D), (circlePoint1_2D - circlePoint3_2D).ToLeft(), out center_2D))
					{
						P.Location = P.ToGlobal(center_2D);
						this.plane = P;
						this.majorRadius = Geometry.Dist(circlePoint2, this.Center);
						this.minorRadius = this.majorRadius;
						this.startParameter = new Angle(circlePoint1_2D, center_2D);
						this.sweepParameter = new SweepAngle((Angle)this.startParameter, new Angle(circlePoint3_2D, center_2D), true);
						if (Geometry.DistPL(circlePoint2_2D, circlePoint1_2D, circlePoint3_2D) > 0.0)
						{
							this.sweepParameter = -(2 * Math.PI - this.sweepParameter);
						}
					}
					else
					{
						throw new EllipseException(EllipseException.tExceptionType.SetCircle3PointsFailed);
					}
				}
				catch (PlaneException)
				{
					throw new EllipseException(EllipseException.tExceptionType.SetCircle3PointsFailed);
				}
			}
		}
		/// <summary>
		/// Kreis aus Ebene und zwei gegenüberliegenden Punkten auf dem Kreis
		/// </summary>
		/// <param name="p">Die Ebene des Kreises (3D-Parameter)</param>
		/// <param name="circlePoint1">1. Punkt des Kreises</param>
		/// <param name="circlePoint2">2. Punkt des Kreises</param>
		public void SetCirclePlane2Points(Plane p, GeoPoint circlePoint1, GeoPoint circlePoint2)
		{
			using (new Changing(this, "SetArcPlaneCenterRadius", this.plane, this.plane.Location, this.majorRadius))
			{
				try
				{
					this.plane = p;
					this.majorRadius = Geometry.Dist(circlePoint1, circlePoint2) / 2.0;
					this.minorRadius = this.majorRadius;
					this.plane.Location = new GeoPoint(circlePoint1, circlePoint2);
				}
				catch (PlaneException)
				{
					throw new EllipseException(EllipseException.tExceptionType.SetCirclePlane2PointsFailed);
				}
			}
		}
		public void SetArcPlane2Points(Plane p, GeoPoint circlePoint1, GeoPoint circlePoint2)
		{
			using (new Changing(this, "SetArcPlaneCenterRadius", this.plane, this.plane.Location, this.majorRadius, this.startParameter, this.sweepParameter))
			{
				try
				{
					this.plane = p;
					this.majorRadius = Geometry.Dist(circlePoint1, circlePoint2) / 2.0;
					this.minorRadius = this.majorRadius;
					this.plane.Location = new GeoPoint(circlePoint1, circlePoint2);
					this.startParameter = new Angle(this.plane.Project(circlePoint1), this.plane.Project(this.Center));
					this.sweepParameter = new SweepAngle(Math.PI);
				}
				catch (PlaneException)
				{
					throw new EllipseException(EllipseException.tExceptionType.SetCirclePlane2PointsFailed);
				}
			}
		}
		public void SetArcPlane2PointsRadiusLocation(Plane p, GeoPoint arcPoint1, GeoPoint arcPoint2, double radius, GeoPoint locationP, int selSol)
		{
			// wegen UNDO
			using (new Changing(this, "SetArcPlaneCenterRadius", this.plane, this.plane.Location, this.majorRadius, this.startParameter, this.sweepParameter))
			{
				try
				{
					this.plane = p;
					this.majorRadius = radius;
					this.minorRadius = this.majorRadius;

					GeoPoint2D arcPoint1_2D = this.plane.Project(arcPoint1);
					GeoPoint2D arcPoint2_2D = this.plane.Project(arcPoint2);
					GeoPoint2D locationP_2D = this.plane.Project(locationP);
					double dist12 = Geometry.Dist(arcPoint1, arcPoint2) / 2.0;
					double distLoc = Math.Abs(Geometry.DistPL(locationP_2D, arcPoint1_2D, arcPoint2_2D));
					double distLoc1 = Geometry.DistPL(locationP_2D, arcPoint1_2D, arcPoint2_2D);
					Circle2D c1 = new Circle2D(arcPoint1_2D, radius);
					Circle2D c2 = new Circle2D(arcPoint2_2D, radius);
					GeoPoint2DWithParameter[] pl = c1.Intersect(c2);
					int centerIndex;
					switch (pl.Length)
					{
						case 0:
							{
								this.majorRadius = dist12;
								this.minorRadius = this.majorRadius;
								this.plane.Location = new GeoPoint(arcPoint1, arcPoint2);
							}
							; break;
						case 1: this.plane.Location = p.ToGlobal(pl[0].p); break;
						case 2:
							{   // ausserhalb nach dem nächsten Mittelpunkt (=Schnittpunkt) sortieren und umgekehrt
								if (Geometry.Dist(p.ToGlobal(pl[0].p), locationP) < Geometry.Dist(p.ToGlobal(pl[1].p), locationP))
								{
									if (distLoc > dist12) // ausserhalb 
										centerIndex = 0;
									else centerIndex = 1;
								}
								else
								{
									if (distLoc > dist12)
										centerIndex = 1;
									else centerIndex = 0;
								}
								// falls eine abweichende Lösong gewünscht ist (es gibt vier), 
								// hier für die selSol-Fälle 1 und drei umkehrung des Mittelpunktes
								// if ((selSol % 2) != 0)
								if ((selSol & 0x1) != 0)
								{
									if (centerIndex == 1) centerIndex = 0;
									else centerIndex = 1;
								}
								this.plane.Location = p.ToGlobal(pl[centerIndex].p);

							}
							; break;
					}
					this.startParameter = new Angle(this.plane.Project(arcPoint1), this.plane.Project(this.Center));
					bool dir = (distLoc1 < 0.0);
					// falls eine abweichende Lösung gewünscht ist (es gibt vier), 
					// hier für die selSol-Fälle zwei und drei umkehrung der Richtung
					// if (((selSol % 4) == 2)||((selSol % 4) == 3))  dir = !dir;
					if ((selSol & 0x2) != 0) dir = !dir;
					this.sweepParameter = new SweepAngle((Angle)this.startParameter, new Angle(this.plane.Project(arcPoint2), this.plane.Project(this.Center)), dir);
				}
				catch (PlaneException)
				{
					throw new EllipseException(EllipseException.tExceptionType.SetCirclePlane2PointsFailed);
				}
			}
		}
		public void SetEllipseCenterPlane(GeoPoint center, Plane plane)
		{
			using (new Changing(this, "SetEllipseCenter", center, plane))
			{
				this.plane = plane;
				this.plane.Location = center;
			}
		}
		public void SetEllipseCenterAxis(GeoPoint Center, GeoVector MajorAxis, GeoVector MinorAxis)
		{
			using (new Changing(this)) // mit allen Parametern bringt auch nichts
			{
				plane = new Plane(Center, MajorAxis, MinorAxis);
				majorRadius = MajorAxis.Length;
				minorRadius = MinorAxis.Length;

				startParameter = 0.0;
				sweepParameter = Math.PI * 2.0; // sweep parameter was 0 previously, but it should be a full ellipse
			}
		}
		internal void SetElliStartEndPoint(GeoPoint p1, GeoPoint p2)
		{
			try
			{
				Plane pln = new Plane(Center, p1 - Center, p2 - Center);
				GeoVector ma = pln.ToGlobal(pln.Project(MajorAxis));
				GeoVector mi = pln.ToGlobal(pln.Project(MinorAxis));
				plane = new Plane(Center, ma, mi);
				GeoPoint2D p11 = plane.Project(p1);
				GeoPoint2D p22 = plane.Project(p2);
				double a = Math.Sqrt(Math.Abs(((p11.y * p11.y * p22.x * p22.x - p11.x * p11.x * p22.y * p22.y) / (p11.y * p11.y - p22.y * p22.y))));
				double b = Math.Sqrt((Math.Abs(-(p11.y * p11.y * p22.x * p22.x - p11.x * p11.x * p22.y * p22.y) / (p11.x * p11.x - p22.x * p22.x))));
				majorRadius = a;
				minorRadius = b;
				double sp = ParameterOf(p1);
				double ep = ParameterOf(p2);

				GeoPoint pp1 = plane.Location + Math.Cos(sp) * majorRadius * plane.DirectionX + Math.Sin(sp) * minorRadius * plane.DirectionY;
				GeoPoint pp2 = plane.Location + Math.Cos(ep) * majorRadius * plane.DirectionX + Math.Sin(ep) * minorRadius * plane.DirectionY;
				GeoVector2D dir = new GeoVector2D(-majorRadius * Math.Sin(sp), minorRadius * Math.Cos(sp));
				GeoVector dir1 = plane.ToGlobal(dir);
				dir = new GeoVector2D(-majorRadius * Math.Sin(ep), minorRadius * Math.Cos(ep));
				GeoVector dir2 = plane.ToGlobal(dir);
				double d1 = Geometry.LinePar(pp1, dir1, p1);
				double d2 = Geometry.LinePar(pp2, dir2, p2);
				sp += d1;
				ep += d2;
				startParameter = sp;
				double sw = ep - sp;
				if (sweepParameter < 0 && sw > 0) sweepParameter = sw - Math.PI * 2;
				else if (sweepParameter > 0 && sw < 0) sweepParameter = sw + Math.PI * 2;
				else sweepParameter = sw;
			}
			catch { }
		}

		public void SetEllipseArcCenterAxis(GeoPoint center, GeoVector majorAxis, GeoVector minorAxis, double startParameter, double sweepParameter)
		{
			using (new Changing(this)) // mit allen Parametern bringt auch nichts
			{
				plane = new Plane(center, majorAxis, minorAxis);
				majorRadius = majorAxis.Length;
				minorRadius = minorAxis.Length;
				this.startParameter = startParameter;
				this.sweepParameter = sweepParameter;
			}
		}
		public void SetEllipseCenterAxis(GeoPoint Center, GeoVector MajorAxis, GeoVector MinorAxis, Plane alignTo)
		{
			using (new Changing(this)) // mit allen Parametern bringt auch nichts
			{
				plane = new Plane(Center, MajorAxis, MinorAxis);
				plane.Align(alignTo, false, true); // ausrichten an Drawingplane und gleiche Normalenrichtung
				majorRadius = MajorAxis.Length;
				minorRadius = MinorAxis.Length;
				startParameter = 0.0; // diese beiden Zeilen wurden notwendig wg. dwg Import
				sweepParameter = Math.PI * 2.0;
			}
		}
		public Boolean SetEllipse2PointsDirections(GeoPoint point1, GeoVector direction1, GeoPoint point2, GeoVector direction2)
		{
			using (new Changing(this)) // mit allen Parametern bringt auch nichts
			{
				try
				{
					Plane planeT = new Plane(point1, point1 + direction1, point2 + direction2);
					if (Precision.IsPointOnPlane(point2, plane))
					{
						//                        planeT.Align(alignTo, false, true); // ausrichten an Drawingplane und gleiche Normalenrichtung
						Ellipse2D e2d = Geometry.Ellipse2P2T(planeT.Project(point1), planeT.Project(point2), planeT.Project(direction1), planeT.Project(direction2));
						if (e2d != null)
						{
							plane = new Plane(planeT.ToGlobal(e2d.center), planeT.ToGlobal(e2d.majorAxis), planeT.ToGlobal(e2d.minorAxis));
							majorRadius = planeT.ToGlobal(e2d.majorAxis).Length;
							minorRadius = planeT.ToGlobal(e2d.minorAxis).Length;
							startParameter = 0.0; // diese beiden Zeilen wurden notwendig wg. dwg Import
							sweepParameter = Math.PI * 2.0;
							return true;
						}
					}
				}
				catch (PlaneException)
				{
					//                    throw new EllipseException(EllipseException.tExceptionType.SetEllipse2PointsDirectionsFailed);
				}
			}
			return false;
		}
		public Boolean SetEllipseArc2PointsDirections(GeoPoint point1, GeoVector direction1, GeoPoint point2, GeoVector direction2, Boolean arcDir, Plane alignTo)
		{
			using (new Changing(this, "SetEllipseArcCenterAxis", this.Center, this.MajorAxis, this.MinorAxis, this.StartParameter, this.sweepParameter))
			{
				try
				{
					Plane planeT = new Plane(point1, point1 + direction1, point2 + direction2);
					if (Precision.IsPointOnPlane(point2, plane))
					{
						planeT.Align(alignTo, false, true); // ausrichten an Drawingplane und gleiche Normalenrichtung
						Ellipse2D e2d = Geometry.Ellipse2P2T(planeT.Project(point1), planeT.Project(point2), planeT.Project(direction1), planeT.Project(direction2));
						if (e2d != null)
						{
							plane = new Plane(planeT.ToGlobal(e2d.center), planeT.ToGlobal(e2d.majorAxis), planeT.ToGlobal(e2d.minorAxis));
							majorRadius = planeT.ToGlobal(e2d.majorAxis).Length;
							minorRadius = planeT.ToGlobal(e2d.minorAxis).Length;
							this.StartPoint = point1;
							this.EndPoint = point2;
							if (arcDir && this.SweepParameter < 0.0) this.SweepParameter += 2 * Math.PI;
							if (!arcDir && this.SweepParameter > 0.0) this.SweepParameter -= 2 * Math.PI;
							return true;
						}
					}
				}
				catch (PlaneException)
				{
					//                   throw new EllipseException(EllipseException.tExceptionType.SetEllipse2PointsDirectionsFailed);
				}
			}
			return false;
		}
		/// <summary>
		/// Tangentenpunkte der Ellipse bei vorgegebener Richtung
		/// </summary>
		/// <param name="Direction">Richtung der Tangente</param>
		public double TangentParameter(GeoVector Direction)
		{
			ModOp Transform = ModOp.Transform(CoordSys.StandardCoordSys, plane.CoordSys);
			GeoVector2D dir = Transform.Project(Direction);
			// betrachtet die Ellipse im Ursprung von 2D gelegen mit den beiden Radien
			GeoPoint dbg = Transform * plane.Location;
			dir.x /= majorRadius;
			dir.y /= minorRadius;
			// dir ist jetzt im Einheitskreis, aber nicht unbedingt Länge 1, ist aber
			// für Atan2 nicht wichtig
			// der Atan2 liefert zu einer Senkrechten zu dir den Winkel, also
			// den Parameter im Sinne der Ellipse. Der 2. Tangentenpunkt hat den 
			// Parameter u+pi (bei Atan2 sind die Parameter vertauscht Atan2(y,x))
			return Math.Atan2(dir.x, -dir.y);
		}
		public GeoPoint Point(double u)
		{
			return (plane.Location + Math.Cos(u) * majorRadius * plane.DirectionX) + Math.Sin(u) * minorRadius * plane.DirectionY;
		}
		/// <summary>
		/// gets or sets the center of this circle or ellipse
		/// </summary>
		public virtual GeoPoint Center
		{
			get
			{
				return plane.Location;
			}
			set
			{
				using (new Changing(this, "Center"))
				{
					plane.Location = value;
					projectionData.Clear();
				}
			}
		}
		public virtual double MajorRadius
		{
			get
			{
				return majorRadius;
			}
			set
			{
				using (new Changing(this, "MajorRadius"))
				{
					majorRadius = value;
					projectionData.Clear();
				}
			}
		}
		public virtual double MinorRadius
		{
			get
			{
				return minorRadius;
			}
			set
			{
				using (new Changing(this, "MinorRadius"))
				{
					minorRadius = value;
					projectionData.Clear();
				}
			}
		}
		public GeoVector MajorAxis
		{
			get { return majorRadius * plane.DirectionX; }
			set
			{
				using (new Changing(this, "MajorAxis"))
				{   // zwei Fälle: neue Achse ist in der gegebenen Ebene
					if (Precision.IsPerpendicular(plane.Normal, value, false))
					{   // die neue Achse liegt in der alten Ebene, also nur Drehung
						// das erlaubt insbesondere, dass die neue majorAxis parallel zur alten minorAxis ist
						plane.DirectionX = value;
						majorRadius = value.Length;
					}
					else
					{   // es entsteht eine neue Ebene, d.h. die andere Achse wird festgehalten.
						plane = new Plane(Center, value, MinorAxis);
						majorRadius = MajorAxis.Length;
					}
				}
			}
		}
		public GeoVector MinorAxis
		{
			get { return minorRadius * plane.DirectionY; }
			set
			{
				using (new Changing(this, "MinorAxis"))
				{   // zwei Fälle: neue Achse ist in der gegebenen Ebene
					if (Precision.IsPerpendicular(plane.Normal, value, false))
					{   // die neue Achse liegt in der alten Ebene, also nur Drehung
						plane.DirectionY = value;
						minorRadius = value.Length;
					}
					else
					{
						plane = new Plane(Center, MajorAxis, value);
						minorRadius = MinorAxis.Length;
					}
				}
			}
		}
		public GeoVector Normal
		{
			get { return plane.Normal; }
			set
			{
				using (new Changing(this, "Normal"))
				{
					plane.Normal = value;
					projectionData.Clear();
				}
			}
		}
		public Plane Plane
		{
			get { return plane; }
			set
			{
				using (new Changing(this, "Plane"))
				{
					plane = value;
					projectionData.Clear();
				}
			}
		}
		private GeoPoint[] GetCashedApproximation(double precision)
		{
			lock (lockApproximationRecalc)
			{
				if (currentApproximation != null && precision >= currentPrecision && currentPrecision != 0.0)
				{
					return currentApproximation;
				}
				else
				{
					currentPrecision = precision;
					if (this.IsCircle)
					{
						double alpha;
						if (Radius > precision)
						{
							alpha = Math.Acos((Radius - precision) / Radius);
						}
						else
						{
							alpha = Math.PI / 2.0; // Viertel
						}
						int n = Math.Max(2, (int)(Math.Abs(sweepParameter) / alpha));
						alpha = sweepParameter / n;
						currentApproximation = new GeoPoint[n + 1];
						for (int i = 0; i < n; ++i)
						{
							double ea = startParameter + i * alpha;
							currentApproximation[i] = plane.Location + Math.Cos(ea) * majorRadius * plane.DirectionX + Math.Sin(ea) * minorRadius * plane.DirectionY;
						}
						currentApproximation[n] = EndPoint;
					}
					else
					{   // dynamische Anpassung bei der Ellipse, geht sicher auch einfacher
						ICurve cv = (this as ICurve).Approximate(true, precision);
						if (cv is Path)
						{
							Path path = cv as Path;
							if (path.CurveCount == 0) return new GeoPoint[0]; // leere Kurve
							currentApproximation = new GeoPoint[path.CurveCount + 1];
							ICurve[] cvs = path.Curves;
							for (int i = 0; i < cvs.Length; ++i)
							{
								currentApproximation[i] = cvs[i].StartPoint;
							}
							currentApproximation[cvs.Length] = cvs[cvs.Length - 1].EndPoint;
						}
					}
					return currentApproximation;
				}
			}
		}
		/// <summary>
		/// Returns the intersectionpoints of this ellipse with the provided plane <paramref name="toIntersectWith"/>.
		/// The result is an array with 0 to 2 <see cref="GeoPoint"/>s.
		/// </summary>
		/// <param name="toIntersectWith">Plane to intersect with</param>
		/// <returns>The intersectionpoints</returns>
		public GeoPoint[] PlaneIntersection(Plane toIntersectWith)
		{
			GeoPoint iploc;
			GeoVector ipdir;
			List<GeoPoint> res = new List<GeoPoint>();
			if (plane.Intersect(toIntersectWith, out iploc, out ipdir))
			{
				ModOp2D toUnit = ModOp2D.Scale(1.0 / majorRadius, 1.0 / minorRadius);
				GeoPoint2D loc2d = plane.Project(iploc);
				loc2d.x /= majorRadius;
				loc2d.y /= minorRadius;
				GeoVector2D dir2d = plane.Project(ipdir);
				dir2d.x /= majorRadius;
				dir2d.y /= minorRadius;
				GeoPoint2D[] ip = Geometry.IntersectLC(loc2d, dir2d, GeoPoint2D.Origin, 1.0);
				for (int i = 0; i < ip.Length; ++i)
				{
					ip[i].x *= majorRadius;
					ip[i].y *= minorRadius;
					GeoPoint p = plane.ToGlobal(ip[i]);
					double par = (this as ICurve).PositionOf(p);
					if (par >= 0.0 && par <= 1.0) res.Add(p);
				}
			}
			return res.ToArray();
		}
		#region IGeoObject
		/// <summary>
		/// Overrides <see cref="CADability.GeoObject.IGeoObjectImpl.CopyGeometry (IGeoObject)"/>
		/// </summary>
		/// <param name="ToCopyFrom"></param>
		public override void CopyGeometry(IGeoObject ToCopyFrom)
		{
			using (new Changing(this, "CopyGeometry", Clone()))
			{
				Ellipse eToCopyFrom = ToCopyFrom as Ellipse;
				plane = eToCopyFrom.plane;
				majorRadius = eToCopyFrom.majorRadius;
				minorRadius = eToCopyFrom.minorRadius;
				startParameter = eToCopyFrom.startParameter;
				sweepParameter = eToCopyFrom.sweepParameter;
				projectionData.Clear();
			}
		}
		/// <summary>
		/// Overrides <see cref="CADability.GeoObject.IGeoObjectImpl.Modify (ModOp)"/>
		/// </summary>
		/// <param name="m"></param>
		public override void Modify(ModOp m)
		{
			using (new Changing(this, "ModifyInverse", m))
			{
				if (m.Mode != ModOp.ModificationMode.Other || m.IsOrthogonal)
				{
					// nur unter der Annahme, dass wir nicht verzerren gitl:
					GeoPoint c = m * plane.Location;
					GeoVector majax = m * (majorRadius * plane.DirectionX);
					GeoVector minax = m * (minorRadius * plane.DirectionY);
					GeoVector dirx = m * plane.DirectionX;
					GeoVector diry = m * plane.DirectionY;
					try
					{
						plane = new Plane(c, dirx, diry);
						majorRadius = majax.Length;
						minorRadius = minax.Length;
					}
					catch (PlaneException) { }  // wenn die Ebene nicht geht, dann also nichts machen
				}
				else
				{
					try
					{
						GeoVector majax = m * MajorAxis;
						GeoVector minax = m * MinorAxis;
						GeoPoint cnt = m * Center;
						GeoPoint stp = m * StartPoint;
						GeoPoint endp = m * EndPoint;

						if (m.IsOrthogonal)
						{
							plane = new Plane(cnt, majax, minax);
							majorRadius = majax.Length;
							minorRadius = minax.Length;
						}
						else
						{

							Plane pln = new Plane(cnt, majax, minax); // geht schief, wenn Ellipse zur Linie degeneriert!
							GeoVector2D majax2d = pln.Project(majax);
							GeoVector2D minax2d = pln.Project(minax);
							GeoVector2D majax2dmain, minax2dmain;
							GeoPoint2D left, right, bottom, top;
							Geometry.PrincipalAxis(GeoPoint2D.Origin, majax2d, minax2d, out majax2dmain, out minax2dmain, out left, out right, out bottom, out top, false);

							plane = new Plane(cnt, pln.ToGlobal(majax2dmain), pln.ToGlobal(minax2dmain));
							majorRadius = majax2dmain.Length;
							minorRadius = minax2dmain.Length;
							// der Startparameter wird auch für Vollkreis/Ellipsen berechnet und ist dort
							// von Bedeutung. Insbesondere ist das wichtig bei Kegeln und Zylindern, die Vollkreise/Ellipsen
							// als Kanten haben, denn bei diesen kanten ist der Start- und Endpunkt wichtig.
							double sp = ParameterOf(stp);
							startParameter = sp;
						}
					}
					catch (PlaneException) { }  // wenn die Ebene nicht geht, dann also nichts machen
				}
				projectionData.Clear();
			}
		}
		/// <summary>
		/// Overrides <see cref="CADability.GeoObject.IGeoObjectImpl.Clone ()"/>
		/// </summary>
		/// <returns></returns>
		public override IGeoObject Clone()
		{
			Ellipse result = Construct();
			// hier nicht CopyGeometry() aufrufen, gibt sonst endlos Rekursion
			result.plane = this.plane;
			result.majorRadius = this.majorRadius;
			result.minorRadius = this.minorRadius;
			result.startParameter = this.startParameter;
			result.sweepParameter = this.sweepParameter;
			result.CopyAttributes(this);
			return result;
		}
		/// <summary>
		/// Overrides <see cref="CADability.GeoObject.IGeoObjectImpl.FindSnapPoint (SnapPointFinder)"/>
		/// </summary>
		/// <param name="spf"></param>
		public override void FindSnapPoint(SnapPointFinder spf)
		{
			if (!spf.Accept(this)) return;
			EllipseData2D d2d = GetProjectionData(spf.Projection); // Projektionsdaten aus dem Cache
			if (spf.SnapToObjectCenter)
			{
				spf.Check(plane.Location, this, SnapPointFinder.DidSnapModes.DidSnapToObjectCenter);
			}
			if (spf.SnapToObjectSnapPoint)
			{   // wer spezifiziert die Fangpunkte der Ellipse, Setting??
				spf.Check(plane.Location + MajorAxis, this, SnapPointFinder.DidSnapModes.DidSnapToObjectSnapPoint);
				spf.Check(plane.Location - MajorAxis, this, SnapPointFinder.DidSnapModes.DidSnapToObjectSnapPoint);
				spf.Check(plane.Location + MinorAxis, this, SnapPointFinder.DidSnapModes.DidSnapToObjectSnapPoint);
				spf.Check(plane.Location - MinorAxis, this, SnapPointFinder.DidSnapModes.DidSnapToObjectSnapPoint);
				spf.Check(StartPoint, this, SnapPointFinder.DidSnapModes.DidSnapToObjectSnapPoint);
				spf.Check(EndPoint, this, SnapPointFinder.DidSnapModes.DidSnapToObjectSnapPoint);
				if (spf.Snap30)
				{
					if (sweepParameter > 0.0)
					{
						for (int i = 0; i < 24; ++i)
						{
							double a = i * Math.PI / 6.0;
							if (a > startParameter && a < startParameter + sweepParameter)
								spf.Check(plane.Location + Math.Cos(a) * MajorAxis + Math.Sin(a) * this.MinorAxis, this, SnapPointFinder.DidSnapModes.DidSnapToObjectSnapPoint);
						}
					}
					else
					{
						for (int i = -12; i < 12; ++i)
						{
							double a = i * Math.PI / 6.0;
							if (a < startParameter && a > startParameter + sweepParameter)
								spf.Check(plane.Location + Math.Cos(a) * MajorAxis + Math.Sin(a) * this.MinorAxis, this, SnapPointFinder.DidSnapModes.DidSnapToObjectSnapPoint);
						}
					}
				}
				if (spf.Snap45)
				{
					if (sweepParameter > 0.0)
					{
						for (int i = 0; i < 16; ++i)
						{
							double a = i * Math.PI / 4.0;
							if (a > startParameter && a < startParameter + sweepParameter)
								spf.Check(plane.Location + Math.Cos(a) * MajorAxis + Math.Sin(a) * this.MinorAxis, this, SnapPointFinder.DidSnapModes.DidSnapToObjectSnapPoint);
						}
					}
					else
					{
						for (int i = -8; i < 8; ++i)
						{
							double a = i * Math.PI / 4.0;
							if (a < startParameter && a > startParameter + sweepParameter)
								spf.Check(plane.Location + Math.Cos(a) * MajorAxis + Math.Sin(a) * this.MinorAxis, this, SnapPointFinder.DidSnapModes.DidSnapToObjectSnapPoint);
						}
					}
				}
			}
			if (spf.SnapToObjectPoint)
			{
				double par = PositionOf(spf.SourcePoint3D, spf.Projection.ProjectionPlane);
				if (par >= 0.0 && par <= 1.0)
				{
					spf.Check(PointAt(par), this, SnapPointFinder.DidSnapModes.DidSnapToObjectPoint);
				}
			}
			if (spf.SnapToDropPoint && spf.BasePointValid)
			{
				GeoPoint2D p = plane.Project(spf.BasePoint);
				Angle a = new Angle(p, GeoPoint2D.Origin);
				GeoPoint2D tst = new GeoPoint2D(Math.Cos(a) * majorRadius, Math.Sin(a) * minorRadius);
				spf.Check(plane.ToGlobal(tst), this, SnapPointFinder.DidSnapModes.DidSnapToDropPoint);
				a = a + SweepAngle.Opposite;
				tst = new GeoPoint2D(Math.Cos(a) * majorRadius, Math.Sin(a) * minorRadius);
				spf.Check(plane.ToGlobal(tst), this, SnapPointFinder.DidSnapModes.DidSnapToDropPoint);
			}
			if (spf.SnapToTangentPoint && spf.BasePointValid)
			{
				GeoPoint2D p = plane.Project(spf.BasePoint);
				p.x /= majorRadius;
				p.y /= minorRadius;
				// p ist im Sinne des Einheitskreises in der Ellipsenebene
				p.x /= 2.0;
				p.y /= 2.0;
				// der Mittelpunkt zwischen Ausgangspunkt und Kreismittelpunkt
				GeoPoint2D[] res = Geometry.IntersectCC(p, Geometry.Dist(p, GeoPoint2D.Origin), GeoPoint2D.Origin, 1.0);
				for (int i = 0; i < res.Length; ++i)
				{
					res[i].x *= majorRadius;
					res[i].y *= minorRadius;
					GeoPoint ToTest = plane.ToGlobal(res[i]);
					spf.Check(ToTest, this, SnapPointFinder.DidSnapModes.DidSnapToTangentPoint);
				}
			}
		}
		/// <summary>
		/// Overrides <see cref="CADability.GeoObject.IGeoObjectImpl.AttributeChanged (INamedAttribute)"/>
		/// </summary>
		/// <param name="attribute"></param>
		/// <returns></returns>
		public override bool AttributeChanged(INamedAttribute attribute)
		{
			if (attribute == colorDef || attribute == Layer)
			{
				using (new Changing(this, true, true, "AttributeChanged", attribute))
				{
					return true;
				}
			}
			return false;
		}
		/// <summary>
		/// Overrides <see cref="CADability.GeoObject.IGeoObjectImpl.HasValidData ()"/>
		/// </summary>
		/// <returns></returns>
		public override bool HasValidData()
		{
			if (majorRadius < Precision.eps) return false;
			if (minorRadius < Precision.eps) return false;
			if (Math.Abs(sweepParameter) < Precision.epsa) return false;
			return true;
		}
		/// <summary>
		/// Overrides <see cref="CADability.GeoObject.IGeoObjectImpl.GetShowProperties (IFrame)"/>
		/// </summary>
		/// <param name="Frame"></param>
		/// <returns></returns>
		public override IPropertyEntry GetShowProperties(IFrame Frame)
		{
			if (IsCircle)
			{
				return new ShowPropertyCircle(this, Frame);
			}
			else
			{
				return new ShowPropertyEllipse(this, Frame);
			}
		}
		/// <summary>
		/// Overrides <see cref="CADability.GeoObject.IGeoObjectImpl.GetBoundingCube ()"/>
		/// </summary>
		/// <returns></returns>
		public override BoundingCube GetBoundingCube()
		{   // es hilft nichts, man muss in 2-dimensionalen 2Mal die BoundingBox bestimmen
			if (extent.IsEmpty)
			{
				ICurve2D c2d = (this as ICurve).GetProjectedCurve(Plane.XYPlane);
				BoundingRect br = c2d.GetExtent();
				extent.Xmin = br.Left;
				extent.Xmax = br.Right;
				extent.Ymin = br.Bottom;
				extent.Ymax = br.Top;
				c2d = (this as ICurve).GetProjectedCurve(Plane.YZPlane);
				br = c2d.GetExtent();
				extent.Ymin = Math.Min(extent.Ymin, br.Left);
				extent.Ymax = Math.Max(extent.Ymax, br.Right);
				extent.Zmin = br.Bottom;
				extent.Zmax = br.Top;
				c2d = (this as ICurve).GetProjectedCurve(Plane.XZPlane);
				br = c2d.GetExtent();
				extent.Xmin = Math.Min(extent.Xmin, br.Left);
				extent.Xmax = Math.Max(extent.Xmax, br.Right);
				extent.Zmin = Math.Min(extent.Zmin, br.Bottom);
				extent.Zmax = Math.Max(extent.Zmax, br.Top);
			}
			return extent;
		}
		public delegate bool PaintTo3DDelegate(Ellipse toPaint, IPaintTo3D paintTo3D);
		public static PaintTo3DDelegate OnPaintTo3D;
		/// <summary>
		/// Overrides <see cref="CADability.GeoObject.IGeoObjectImpl.PaintTo3D (IPaintTo3D)"/>
		/// </summary>
		/// <param name="paintTo3D"></param>
		public override void PaintTo3D(IPaintTo3D paintTo3D)
		{
			if (OnPaintTo3D != null && OnPaintTo3D(this, paintTo3D)) return;
			if (paintTo3D.SelectMode)
			{
				// paintTo3D.SetColor(paintTo3D.SelectColor);
			}
			else
			{
				if (colorDef != null) paintTo3D.SetColor(colorDef.Color);
			}
			if (lineWidth != null) paintTo3D.SetLineWidth(lineWidth);
			if (linePattern != null) paintTo3D.SetLinePattern(linePattern);
			if ((paintTo3D.Capabilities & PaintCapabilities.CanDoArcs) != 0)
			{
				paintTo3D.Arc(Center, MajorAxis, MinorAxis, startParameter, sweepParameter);
			}
			else
			{
				GeoPoint[] points = GetCashedApproximation(paintTo3D.Precision);
				if (points != null && points.Length > 1) paintTo3D.Polyline(points);
				else paintTo3D.Polyline(new GeoPoint[] { StartPoint, EndPoint });
			}
		}
		/// <summary>
		/// Overrides <see cref="CADability.GeoObject.IGeoObjectImpl.PrepareDisplayList (double)"/>
		/// </summary>
		/// <param name="precision"></param>
		public override void PrepareDisplayList(double precision)
		{
			GetCashedApproximation(precision);
		}
		public override Style.EDefaultFor PreferredStyle
		{
			get
			{
				return Style.EDefaultFor.Curves;
			}
		}
		/// <summary>
		/// Overrides <see cref="CADability.GeoObject.IGeoObjectImpl.GetQuadTreeItem (Projection, ExtentPrecision)"/>
		/// </summary>
		/// <param name="projection"></param>
		/// <param name="extentPrecision"></param>
		/// <returns></returns>
		public override IQuadTreeInsertableZ GetQuadTreeItem(Projection projection, ExtentPrecision extentPrecision)
		{
			EllipseData2D d2d = CalculateProjectionData(projection.ProjectionPlane);
			ICurve2D c2d = d2d.Curve2D;
			return new QuadTreeEllipse(this, this.plane, c2d, projection);
		}
		/// <summary>
		/// Overrides <see cref="CADability.GeoObject.IGeoObjectImpl.GetExtent (Projection, ExtentPrecision)"/>
		/// </summary>
		/// <param name="projection"></param>
		/// <param name="extentPrecision"></param>
		/// <returns></returns>
		public override BoundingRect GetExtent(Projection projection, ExtentPrecision extentPrecision)
		{
			ICurve2D c2d = (this as ICurve).GetProjectedCurve(projection.ProjectionPlane);
			return c2d.GetExtent();
		}
		#endregion
		#region IOctTreeInsertable members
		/// <summary>
		/// Overrides <see cref="CADability.GeoObject.IGeoObjectImpl.GetExtent (double)"/>
		/// </summary>
		/// <param name="precision"></param>
		/// <returns></returns>
		public override BoundingCube GetExtent(double precision)
		{
			return GetBoundingCube();
			//GeoPoint[] points = GetCashedApproximation(precision);
			//BoundingCube res = BoundingCube.EmptyBoundingCube;
			//for (int i = 0; i < points.Length; ++i)
			//{
			//    res.MinMax(points[i]);
			//}
			//return res;
		}
		/// <summary>
		/// Overrides <see cref="CADability.GeoObject.IGeoObjectImpl.HitTest (ref BoundingCube, double)"/>
		/// </summary>
		/// <param name="cube"></param>
		/// <param name="precision"></param>
		/// <returns></returns>
		public override bool HitTest(ref BoundingCube cube, double precision)
		{
			return (this as ICurve).HitTest(cube);
		}
		/// <summary>
		/// Overrides <see cref="CADability.GeoObject.IGeoObjectImpl.HitTest (Projection, BoundingRect, bool)"/>
		/// </summary>
		/// <param name="projection"></param>
		/// <param name="rect"></param>
		/// <param name="onlyInside"></param>
		/// <returns></returns>
		public override bool HitTest(Projection projection, BoundingRect rect, bool onlyInside)
		{
			ICurve2D c2d = (this as ICurve).GetProjectedCurve(projection.ProjectionPlane);
			if (onlyInside)
			{
				return c2d.GetExtent() <= rect;
			}
			else
			{
				return c2d.HitTest(ref rect, false);
			}
		}
		/// <summary>
		/// Overrides <see cref="CADability.GeoObject.IGeoObjectImpl.HitTest (Projection.PickArea, bool)"/>
		/// </summary>
		/// <param name="area"></param>
		/// <param name="onlyInside"></param>
		/// <returns></returns>
		public override bool HitTest(Projection.PickArea area, bool onlyInside)
		{
			GeoPoint unitCenter = area.ToUnitBox * Center;
			GeoPoint unitMajAxis = area.ToUnitBox * (Center + MajorAxis);
			GeoPoint unitMinAxis = area.ToUnitBox * (Center + MinorAxis);
			GeoPoint unitsp = area.ToUnitBox * StartPoint;
			// GeoPoint unitep = area.ToUnitBox * EndPoint;
			if (onlyInside)
			{
				// der Startpunkt muss drin sein und es darf keine Schnitte mit den Seitenflächen geben
				if (!BoundingCube.UnitBoundingCube.Contains(unitsp)) return false;
				for (int i = 0; i < area.Bounds.Length; ++i)
				{
					GeoPoint[] ip = PlaneIntersection(area.Bounds[i]);
					if (ip.Length > 0) return false; // es darf keinen Schnitt geben
				}
				return true;
			}
			else
			{
				// entweder der Startpunkt ist drin oder es gibt Schnitte mit den Seitenflächen
				// natürlich reduziert auf die begrenzten Würfelseiten
				if (BoundingCube.UnitBoundingCube.Contains(unitsp)) return true; // ein Punkt drin genügt
																				 // Schnittpunkt mit allen Würfelseiten
				for (int i = 0; i < area.Bounds.Length; ++i)
				{
					GeoPoint[] ip = PlaneIntersection(area.Bounds[i]);
					for (int j = 0; j < ip.Length; ++j)
					{
						GeoPoint p = area.ToUnitBox * ip[j];
						if (BoundingCube.UnitBoundingCube.Contains(p, 1e-6)) return true;
					}
				}
				return false;
			}
		}
		/// <summary>
		/// Overrides <see cref="CADability.GeoObject.IGeoObjectImpl.Position (GeoPoint, GeoVector, double)"/>
		/// </summary>
		/// <param name="fromHere"></param>
		/// <param name="direction"></param>
		/// <param name="precision"></param>
		/// <returns></returns>
		public override double Position(GeoPoint fromHere, GeoVector direction, double precision)
		{   // noch nicht getestet
			try
			{
				if (Plane.Intersect(fromHere, direction, out GeoPoint p))
				{
					return Geometry.LinePar(fromHere, direction, p); // nicht getestet ob Ellipse auch getroffen
				}
				else
				{
					Plane nrm = new Plane(fromHere, direction, Plane.Normal);
					double[] ips = (this as ICurve).GetPlaneIntersection(nrm);
					double res = double.MaxValue;
					for (int i = 0; i < ips.Length; i++)
					{
						GeoPoint ip = (this as ICurve).PointAt(ips[i]);
						double d = Geometry.LinePar(fromHere, direction, ip);
						if (d < res) res = d;
					}
					return res;
				}
			}
			catch (ArithmeticException)
			{
				return double.MaxValue;
			}
		}
		#endregion
		/// <summary>
		/// Returns true if both axis of the ellipse have the same length. It may be a circle or a circular arc.
		/// </summary>
		public bool IsCircle
		{
			get
			{
				return Math.Abs(majorRadius - minorRadius) < Precision.eps;
			}
		}
		/// <summary>
		/// Returns true if it is not a full circle or a full ellipse.
		/// </summary>
		public bool IsArc
		{
			get
			{
				//There are cases with arcs more than 2 * Math.PI, and they should be considered as circles or ellipses.
				return Math.Abs(sweepParameter) < 2.0d * Math.PI - Precision.eps;
			}
		}
		public double Radius
		{
			get
			{
				if (Math.Abs(majorRadius - minorRadius) > Precision.eps) throw new EllipseException(EllipseException.tExceptionType.EllipseIsNotACircle);
				return majorRadius;
			}
			set
			{
				using (new Changing(this, "Radius", majorRadius))
				{
					minorRadius = majorRadius = Math.Abs(value); // hier Abs eingeführt, da sonst zu viele Probleme
				}
			}
		}
		internal Ellipse[] SplitAtZero()
		{
			if (startParameter + sweepParameter > 2.0 * Math.PI)
			{
				Ellipse e1 = this.Clone() as Ellipse;
				Ellipse e2 = this.Clone() as Ellipse;
				e1.sweepParameter = 2.0 * Math.PI - startParameter;
				e2.startParameter = 0.0;
				e2.sweepParameter = startParameter + sweepParameter - 2.0 * Math.PI;
				return new Ellipse[] { e1, e2 };
			}
			else if (startParameter + sweepParameter < 0.0)
			{
				Ellipse e1 = this.Clone() as Ellipse;
				Ellipse e2 = this.Clone() as Ellipse;
				e1.sweepParameter = -startParameter;
				e2.startParameter = 2.0 * Math.PI;
				e2.sweepParameter = startParameter + sweepParameter;
				return new Ellipse[] { e1, e2 };
			}
			return null;
		}
		#region IColorDef Members
		private ColorDef colorDef;
		public ColorDef ColorDef
		{
			get
			{
				return colorDef;
			}
			set
			{
				using (new ChangingAttribute(this, "ColorDef", colorDef))
				{
					colorDef = value;
				}
			}
		}
		void IColorDef.SetTopLevel(ColorDef newValue)
		{
			colorDef = newValue;
		}
		void IColorDef.SetTopLevel(ColorDef newValue, bool overwriteChildNullColor)
		{
			(this as IColorDef).SetTopLevel(newValue);
		}
		#endregion
		#region ILineWidth Members
		private LineWidth lineWidth;
		public LineWidth LineWidth
		{
			get
			{
				return lineWidth;
			}
			set
			{
				using (new ChangingAttribute(this, "LineWidth", lineWidth))
				{
					lineWidth = value;
				}
			}
		}
		#endregion
		#region ILinePattern Members
		private LinePattern linePattern;
		public LinePattern LinePattern
		{
			get
			{
				return linePattern;
			}
			set
			{
				using (new ChangingAttribute(this, "LinePattern", linePattern))
				{
					linePattern = value;
				}
			}
		}
		#endregion
		/// <summary>
		/// Gets or sets the startparameter of this circular or elliptical arc. The startparameter determins the
		/// startpoint of this curve according to the formula: Center + cos(StartParameter)*MajorAxis + sin(StartParameter)*MinorAxis
		/// For elliptical arcs the startparameter is not identical to the angle of the startpoint.
		/// </summary>
		public double StartParameter
		{
			get
			{
				return startParameter;
			}
			set
			{
				using (new Changing(this, "StartParameter", startParameter))
				{
					startParameter = value;
					while (startParameter > 2.0 * Math.PI) startParameter -= 2.0 * Math.PI;
					while (startParameter < -2.0 * Math.PI) startParameter += 2.0 * Math.PI;
				}
			}
		}
		/// <summary>
		/// Gets or sets the sweep amount of this arc. A full circle or ellipse must have a sweepparameter of
		/// either 2.0*Math.PI or -2.0*Math.PI, The sweep parameter of circular or elliptical arcs are
		/// in the range of -2.0*Math.PI &lt; SweepParameter &lt; 2.0*Math.PI. SweepParameter is often used 
		/// in connection with startParameter
		/// </summary>
		public double SweepParameter
		{
			get
			{
				return sweepParameter;
			}
			set
			{
				using (new Changing(this, "SweepParameter", sweepParameter))
				{
					if (value < -Math.PI * 2 - 1e-6) value += Math.PI * 2;
					if (value > Math.PI * 2 + 1e-6) value -= Math.PI * 2;
					sweepParameter = Math.Min(Math.Max(-Math.PI * 2, value), Math.PI * 2);
				}
			}
		}
		public GeoPoint StartPoint
		{
			get
			{
				return plane.Location + Math.Cos(startParameter) * majorRadius * plane.DirectionX + Math.Sin(startParameter) * minorRadius * plane.DirectionY;
			}
			set
			{
				using (new Changing(this, "StartPoint"))
				{
					double a = ParameterOf(value);
					if (IsArc) // Vollkreise/Ellipsen sollen voll bleiben, nur mit verändertem Startpunkt
					{
						GeoPoint e = this.EndPoint;
						startParameter = a;
						this.EndPoint = e;
					}
					else
					{
						startParameter = a;
					}
				}
			}
		}
		public GeoPoint EndPoint
		{
			get
			{
				double ea = startParameter + sweepParameter;
				return plane.Location + Math.Cos(ea) * majorRadius * plane.DirectionX + Math.Sin(ea) * minorRadius * plane.DirectionY;
			}
			set
			{
				if (!IsArc) StartPoint = value; // Vollkreise/Ellipsen sollen voll bleiben, Startpunkt und Endpunkt bleiben identisch
				else
					using (new Changing(this, "EndPoint"))
					{
						double a = ParameterOf(value);
						SweepAngle sw = new SweepAngle(startParameter, a, sweepParameter > 0);
						sweepParameter = sw;
					}
			}
		}
		public bool CounterClockWise
		{
			get
			{
				return sweepParameter > 0.0;
			}
			set
			{
				using (new Changing(this, "CounterClockWise"))
				{
					if (value)
					{
						if (sweepParameter < 0.0)
						{
							sweepParameter = -sweepParameter;
						}
					}
					else
					{
						if (sweepParameter > 0.0)
						{
							sweepParameter = -sweepParameter;
						}
					}
				}
			}
		}
		#region IOcasEdge Members

		#endregion
		#region ICurve Members
		public GeoVector StartDirection
		{
			get
			{
				return DirectionAt(0.0);
				//GeoVector res = StartPoint-Center;
				//if (sweepParameter>0) res = res^Normal;
				//else res = Normal^res; //TODO: noch nicht überprüft
				//res.Norm();
				//return res;
			}
		}
		public GeoVector EndDirection
		{
			get
			{
				return DirectionAt(1.0);
				//GeoVector res = EndPoint - Center;
				//if (sweepParameter>0) res = res^Normal;
				//else res = Normal^res; //TODO: noch nicht überprüft
				//res.Norm();
				//return res;
			}
		}
		/// <summary>
		/// Implements <see cref="CADability.GeoObject.ICurve.DirectionAt (double)"/>
		/// </summary>
		/// <param name="Position"></param>
		/// <returns></returns>
		public GeoVector DirectionAt(double Position)
		{
			Angle a = startParameter + Position * sweepParameter;
			GeoVector2D dir = new GeoVector2D(-majorRadius * Math.Sin(a), minorRadius * Math.Cos(a));
			GeoVector res = plane.ToGlobal(dir);
			// if (!CounterClockWise) res = -res;
			res = sweepParameter * res; // Achtung, wir brauchen eine Ableitung mit echter Länge
										// sonst funktionieren die Newton Algorithmen nicht, z.B. bei RuledSurface!
										// Wie weit das für die echte Ellipse stimmt, ist noch nicht geklärt
										// res.Norm();
			return res;
			//GeoVector res = PointAt(Position) - Center;
			//if (sweepParameter>0) res = res^Normal;
			//else res = Normal^res; //TODO: noch nicht überprüft
			//res.Norm();
			//return res;
		}
		bool ICurve.TryPointDeriv2At(double position, out GeoPoint point, out GeoVector deriv1, out GeoVector deriv2)
		{
			Angle a = startParameter + position * sweepParameter;
			point = plane.Location + Math.Cos(a) * majorRadius * plane.DirectionX + Math.Sin(a) * minorRadius * plane.DirectionY;
			GeoVector2D dir = new GeoVector2D(-majorRadius * Math.Sin(a), minorRadius * Math.Cos(a));
			deriv1 = sweepParameter * plane.ToGlobal(dir); // Achtung, wir brauchen eine Ableitung mit echter Länge
			GeoVector2D dir2 = new GeoVector2D(-majorRadius * Math.Cos(a), -minorRadius * Math.Sin(a));
			deriv2 = sweepParameter * plane.ToGlobal(dir2); // Achtung, wir brauchen eine Ableitung mit echter Länge
			return true;
		}
		/// <summary>
		/// Implements <see cref="CADability.GeoObject.ICurve.PointAt (double)"/>
		/// </summary>
		/// <param name="Position"></param>
		/// <returns></returns>
		public GeoPoint PointAt(double Position)
		{   // TODO: Ellipsenbogen (und Kreisbogen) müssen sich konsistent verhalten
			// bezüglich Parameter und Punkt: StartParameter < EndParameter
			// StartPunkt wirklich bezüglich der Richtung u.s.w.
			double ea = startParameter + Position * sweepParameter;
			return plane.Location + Math.Cos(ea) * majorRadius * plane.DirectionX + Math.Sin(ea) * minorRadius * plane.DirectionY;
		}
		/// <summary>
		/// Returns the point at the provided parameter. 2*pi is the full circle, it starts at the startParameter
		/// </summary>
		/// <param name="param"></param>
		/// <returns></returns>
		public GeoPoint PointAtParam(double param)
		{
			double ea = startParameter + param;
			return plane.Location + Math.Cos(ea) * majorRadius * plane.DirectionX + Math.Sin(ea) * minorRadius * plane.DirectionY;
		}
		/// <summary>
		/// Implements <see cref="CADability.GeoObject.ICurve.PositionOf (GeoPoint)"/>
		/// </summary>
		/// <param name="p"></param>
		/// <returns></returns>
		public double PositionOf(GeoPoint p)
		{
			GeoPoint2D p2d = plane.Project(p);
			// in ihrer eigenen Ebene ist die Ellipse horizontal
			p2d.x /= majorRadius;
			p2d.y /= minorRadius;
			Angle a = new Angle(p2d.x, p2d.y);
			double sp = startParameter;
			if (sp > 2.0 * Math.PI) sp -= 2.0 * Math.PI;
			if (sp < 0.0) sp += 2.0 * Math.PI;
			double a1 = (a.Radian - sp) / sweepParameter;
			double a2 = (a.Radian - sp + 2.0 * Math.PI) / sweepParameter;
			double a3 = (a.Radian - sp - 2.0 * Math.PI) / sweepParameter;
			// welcher der 3 Werte ist näher an 0.5 ?
			double ax = a1;
			if (Math.Abs(a2 - 0.5) < Math.Abs(ax - 0.5)) ax = a2;
			if (Math.Abs(a3 - 0.5) < Math.Abs(ax - 0.5)) ax = a3;
			return ax;
		}
		/// <summary>
		/// Implements <see cref="CADability.GeoObject.ICurve.PositionOf (GeoPoint, double)"/>
		/// </summary>
		/// <param name="p"></param>
		/// <param name="prefer"></param>
		/// <returns></returns>
		public double PositionOf(GeoPoint p, double prefer)
		{
			GeoPoint2D p2d = plane.Project(p);
			// in ihrer eigenen Ebene ist die Ellipse horizontal
			p2d.x /= majorRadius;
			p2d.y /= minorRadius;
			Angle a = new Angle(p2d.x, p2d.y);
			double a1 = (a.Radian - startParameter) / sweepParameter;
			double a2 = (a.Radian - startParameter + 2.0 * Math.PI) / sweepParameter;
			double a3 = (a.Radian - startParameter - 2.0 * Math.PI) / sweepParameter;
			// welcher der 3 Werte ist näher an 0.5 ?
			double ax = a1;
			if (Math.Abs(a2 - prefer) < Math.Abs(ax - prefer)) ax = a2;
			if (Math.Abs(a3 - prefer) < Math.Abs(ax - prefer)) ax = a3;
			return ax;
		}
		/// <summary>
		/// Implements <see cref="CADability.GeoObject.ICurve.PositionOf (GeoPoint, Plane)"/>
		/// </summary>
		/// <param name="p"></param>
		/// <param name="pl"></param>
		/// <returns></returns>
		public double PositionOf(GeoPoint p, Plane pl)
		{   // das müsste stimmen, solange es in 2d ein Kreis- oder Ellipsenbogen ist
			// wenn es in 2d eine Linie ist, dann ist die Funktion zumindest mehrdeutig
			ICurve2D c2d = GetProjectedCurve(pl);
			return c2d.PositionOf(pl.Project(p));
		}
		GeoPoint ICurve.StartPoint
		{   // spezielle Implementierung von StartPoint als Eigenschaft von ICurve:
			// das setzen des Startpunktes muss den Endpunkt festhalten. Der
			// SweepAngle soll gleich bleiben
			get { return this.StartPoint; }
			set
			{   // Endpunkt und Winkel sollen bleiben, Ebene kann sich ändern

				// Diese Überlegungen gelten so nur für den Kreisbogen. Ob das auch für die Ellipse so stimmt?
				// Ziemlich sicher NICHT!!!

				if (Precision.IsEqual(value, this.EndPoint))
				{
					if (!IsArc) return;
					throw new EllipseException(EllipseException.tExceptionType.SetStartPointFailed);
				}
				double paramP = this.PositionOf(value);
				if (Precision.IsEqual(value, this.PointAt(paramP))) // der Punkt liegt auf dem (virtuellen) Kreis
				{   // jetzt nur den Startpunkt ändern, Center beibehalten!
					this.StartPoint = value;
				}
				else
				{
					Plane pln = plane;
					if (!Precision.IsPointOnPlane(value, this.plane))
					{   // die Ebene bleibt nicht erhalten. neue Ebene gegeben durch 
						// Startpunkt, Endpunkt und Richtung senkrecht zum jetzigen Normalenvektor
						GeoVector diry = (EndPoint - value) ^ plane.Normal;
						if (Precision.IsNullVector(diry)) diry = plane.DirectionY;
						pln = new Plane(value, EndPoint - value, diry);
						pln.Align(plane, false);
						if (pln.Normal * plane.Normal < 0)
						{
							pln.Reverse();
							pln.Align(plane, false);
						}
					}
					// die Zielebene ist jetzt klar (und meist gleich der Startebene)
					// gegeben sind jetzt 2 Punkte, der neue Anfangspunkt und der alte
					// Endpunkt. Der Start und der Endwinkel sollen beibehalten werden
					GeoPoint2D sp2 = pln.Project(value);
					GeoPoint2D ep2 = pln.Project(EndPoint);
					GeoPoint2D c0 = new GeoPoint2D(sp2, ep2); // Mitte der Verbindung
					GeoVector2D d0 = (ep2 - sp2).ToLeft(); // senkrecht auf Verbindung sp2, ep2
														   // GeoVector2D d1 = new GeoVector2D(new Angle(startParameter));
					GeoVector2D d1 = new GeoVector2D(new Angle(d0.Angle.Radian - sweepParameter / 2.0));
					GeoPoint2D ip; // Schnittpunkt, neuer Mittelpunkt
					if (!Geometry.IntersectLL(c0, d0, sp2, d1, out ip)) throw new EllipseException(EllipseException.tExceptionType.SetStartPointFailed);
					SetArcPlaneCenterStartEndPoint(plane, ip, sp2, ep2, pln, sweepParameter > 0.0);
				}

			}
		}
		GeoPoint ICurve.EndPoint
		{
			get { return this.EndPoint; }
			set
			{   // Endpunkt und Winkel sollen bleiben, Ebene kann sich ändern

				// Diese Überlegungen gelten so nur für den Kreisbogen. Ob das auch für die Ellipse so stimmt?
				// Ziemlich sicher NICHT!!!
				if (Precision.IsEqual(value, this.StartPoint))
				{
					if (!IsArc) return;
					throw new EllipseException(EllipseException.tExceptionType.SetStartPointFailed);
				}
				if (IsCircle)
				{
					double paramP = this.PositionOf(value);
					if (Precision.IsEqual(value, this.PointAt(paramP))) // der Punkt liegt auf dem (virtuellen) Kreis
					{   // jetzt nur den Endpunkt ändern, Center beibehalten!!
						this.EndPoint = value;
					}
					else
					{
						Plane pln = plane;
						if (!Precision.IsPointOnPlane(value, this.plane))
						{   // die Ebene bleibt nicht erhalten. neue Ebene gegeben durch 
							// Startpunkt, Endpunkt und Richtung senkrecht zum jetzigen Normalenvektor
							GeoVector diry = (StartPoint - value) ^ plane.Normal;
							if (Precision.IsNullVector(diry)) diry = plane.DirectionY;
							pln = new Plane(value, StartPoint - value, diry);
							pln.Align(plane, false);
							if (pln.Normal * plane.Normal < 0)
							{
								pln.Reverse();
								pln.Align(plane, false);
							}
						}
						// die Zielebene ist jetzt klar (und meist gleich der Startebene)
						// gegeben sind jetzt 2 Punkte, der neue Anfangspunkt und der alte
						// Endpunkt. Der Start und der Endwinkel sollen beibehalten werden
						GeoPoint2D ep2 = pln.Project(value);
						GeoPoint2D sp2 = pln.Project(StartPoint);
						GeoPoint2D c0 = new GeoPoint2D(sp2, ep2); // Mitte der Verbindung
						GeoVector2D d0 = (ep2 - sp2).ToLeft(); // senkrecht auf Verbindung sp2, ep2
															   // GeoVector2D d1 = new GeoVector2D(new Angle(startParameter));
						GeoVector2D d1 = new GeoVector2D(new Angle(d0.Angle.Radian - sweepParameter / 2.0));
						GeoPoint2D ip; // Schnittpunkt, neuer Mittelpunkt
						if (!Geometry.IntersectLL(c0, d0, sp2, d1, out ip))
						{
							throw new EllipseException(EllipseException.tExceptionType.SetStartPointFailed);
						}
						SetArcPlaneCenterStartEndPoint(plane, ip, sp2, ep2, pln, sweepParameter > 0.0);
					}
				}
				else
				{
					try
					{
						ModOp m = ModOp.Fit(new GeoPoint[] { this.StartPoint, this.EndPoint }, new GeoPoint[] { this.StartPoint, value }, true);
						this.Modify(m);
					}
					catch (ModOpException)
					{
					}
				}
			}
		}
		/// <summary>
		/// Returns the Parameter of the given point projected into the plane of this ellipse (pp). 
		/// For a circle or arc this is the
		/// radian of the angle of the point. For an ellipse this is the value (a), where
		/// e.x = center.x+majorradius*cos(a), e.y = center.y+minorradius*cos(a) yields a point (e)
		/// on the ellipse so that (pp) coincides with the line (center,e).
		/// </summary>
		/// <param name="p">The point to check</param>
		/// <returns>The parameter value</returns>
		public double ParameterOf(GeoPoint p)
		{
			GeoPoint2D p2d = plane.Project(p);
			p2d.x /= majorRadius;
			p2d.y /= minorRadius;
			Angle a = new Angle(p2d.x, p2d.y);
			return a.Radian;
		}

		private double sqr(double s) { return s * s; }
		private double ParameterOf(GeoPoint p, double startValue)
		{
			double cx = Center.x;
			double cy = Center.z;
			double cz = Center.x;
			double ax = MajorAxis.x;
			double ay = MajorAxis.y;
			double az = MajorAxis.z;
			double bx = MinorAxis.x;
			double by = MinorAxis.y;
			double bz = MinorAxis.z;

			double aa = Geometry.SimpleNewtonApproximation(w =>
			{
				return 2 * (az * Math.Sin(w) - bz * Math.Cos(w)) * (-bz * Math.Sin(w) - az * Math.Cos(w) - cz + p.z) + 2 * (ay * Math.Sin(w) - by * Math.Cos(w)) * (-by * Math.Sin(w) - ay * Math.Cos(w) - cy + p.y) + 2 * (ax * Math.Sin(w) - bx * Math.Cos(w)) * (-bx * Math.Sin(w) - ax * Math.Cos(w) - cx + p.x);
			}, w =>
			{
				return 2 * sqr(az * Math.Sin(w) - bz * Math.Cos(w)) + 2 * sqr(ay * Math.Sin(w) - by * Math.Cos(w)) + 2 * sqr(ax * Math.Sin(w) - bx * Math.Cos(w)) + 2 * (-bz * Math.Sin(w) - az * Math.Cos(w) - cz + p.z) * (bz * Math.Sin(w) + az * Math.Cos(w)) + 2 * (-by * Math.Sin(w) - ay * Math.Cos(w) - cy + p.y) * (by * Math.Sin(w) + ay * Math.Cos(w)) + 2 * (-bx * Math.Sin(w) - ax * Math.Cos(w) - cx + p.x) * (bx * Math.Sin(w) + ax * Math.Cos(w));
			}, startValue);
			return aa;
		}
		public double Length
		{
			get
			{
				// the 2d curves solve this task already, is kind of an overkill
				// but works fine
				// oder: http://ca.geocities.com/web_sketches/ellipse_notes/ellipse_arc_length/ellipse_arc_length.html
				ICurve2D c2d = GetProjectedCurve(this.plane);
				return c2d.Length;
			}
		}
		void ICurve.Reverse()
		{
			using (new Changing(this, typeof(ICurve), "Reverse", new object[] { }))
			{
				if (IsArc)
				{
					startParameter += sweepParameter;
					if (startParameter < 0.0) startParameter += 2.0 * Math.PI;
					if (startParameter > 2.0 * Math.PI) startParameter -= 2.0 * Math.PI;
				}
				sweepParameter = -sweepParameter;
			}
		}
		/// <summary>
		/// Implements <see cref="CADability.GeoObject.ICurve.Split (double)"/>
		/// </summary>
		/// <param name="Position"></param>
		/// <returns></returns>
		public ICurve[] Split(double Position)
		{
			// Wozu war diese Einschränkung mit IsClosed? Sie stört in Face.GetProjectedArea
			// if (IsClosed) return null;
			Ellipse e1 = (Ellipse)this.Clone();
			Ellipse e2 = (Ellipse)this.Clone();
			e1.sweepParameter = this.sweepParameter * Position;
			e2.StartParameter = this.StartParameter + this.sweepParameter * Position;
			e2.sweepParameter = this.sweepParameter * (1.0 - Position);
			return new ICurve[] { e1, e2 };
		}
		/// <summary>
		/// Implements <see cref="CADability.GeoObject.ICurve.Split (double, double)"/>
		/// </summary>
		/// <param name="Position1"></param>
		/// <param name="Position2"></param>
		/// <returns></returns>
		public ICurve[] Split(double Position1, double Position2)
		{
			if (!IsClosed) return null;
			if (Position1 > Position2)
			{
				double tmp = Position2;
				Position2 = Position1;
				Position1 = tmp;
			}
			Ellipse e1 = (Ellipse)this.Clone();
			Ellipse e2 = (Ellipse)this.Clone();
			e1.startParameter = this.StartParameter + this.sweepParameter * Position1;
			e1.sweepParameter = this.sweepParameter * (Position2 - Position1);
			e2.startParameter = this.StartParameter + this.sweepParameter * Position2;
			e2.sweepParameter = this.sweepParameter * (1.0 - (Position2 - Position1));
			return new ICurve[] { e1, e2 };
		}
		/// <summary>
		/// Implements <see cref="CADability.GeoObject.ICurve.Trim (double, double)"/>
		/// </summary>
		/// <param name="StartPos"></param>
		/// <param name="EndPos"></param>
		public void Trim(double StartPos, double EndPos)
		{
			if (StartPos == 0 && EndPos == 1) return; // kommt oft vor
			Angle newStartAngle = startParameter + StartPos * sweepParameter;
			Angle newEndAngle = startParameter + EndPos * sweepParameter;
			SweepAngle newSweepAngle = new SweepAngle(newStartAngle, newEndAngle, sweepParameter > 0.0);
			using (new Changing(this, "CopyGeometry", Clone()))
			{
				startParameter = newStartAngle.Radian;
				sweepParameter = newSweepAngle.Radian;
			}
		}
		ICurve ICurve.Clone() { return (ICurve)this.Clone(); }
		ICurve ICurve.CloneModified(ModOp m)
		{
			IGeoObject clone = Clone();
			clone.Modify(m);
			return (ICurve)clone;
		}
		public bool IsClosed
		{
			get
			{
				return !IsArc;
			}
		}
		public bool IsSingular
		{
			get
			{
				return sweepParameter == 0.0;
			}
		}
		/// <summary>
		/// Implements <see cref="CADability.GeoObject.ICurve.GetPlanarState ()"/>
		/// </summary>
		/// <returns></returns>
		public PlanarState GetPlanarState()
		{
			return PlanarState.Planar;
		}
		/// <summary>
		/// Implements <see cref="CADability.GeoObject.ICurve.GetPlane ()"/>
		/// </summary>
		/// <returns></returns>
		public Plane GetPlane()
		{
			return this.plane;
		}
		/// <summary>
		/// Implements <see cref="CADability.GeoObject.ICurve.IsInPlane (Plane)"/>
		/// </summary>
		/// <param name="p"></param>
		/// <returns></returns>
		public bool IsInPlane(Plane p)
		{
			return Precision.IsEqual(p, this.plane);
		}
		/// <summary>
		/// Implements <see cref="CADability.GeoObject.ICurve.GetProjectedCurve (Plane)"/>
		/// </summary>
		/// <param name="p"></param>
		/// <returns></returns>
		public ICurve2D GetProjectedCurve(Plane p)
		{
			EllipseData2D d2d = CalculateProjectionData(p);
			ICurve2D res = d2d.Curve2D;
			return res;
		}
		public override string Description
		{
			get
			{
				if (this.IsCircle)
				{
					if (this.IsArc) return StringTable.GetString("Arc.Description");
					else return StringTable.GetString("Circle.Description");
				}
				else
				{
					if (this.IsArc) return StringTable.GetString("EllipseArc.Description");
					else return StringTable.GetString("Ellipse.Description");
				}
			}
		}
		bool ICurve.IsComposed
		{
			get { return false; }
		}
		ICurve[] ICurve.SubCurves
		{
			get { return new ICurve[0]; }
		}
		ICurve ICurve.Approximate(bool linesOnly, double maxError)
		{
			if (this.IsCircle && !linesOnly)
			{   // Kreise und -bögen durch Kreisbögen annähern ist Unsinn
				return this.Clone() as ICurve;
			}
			else
			{
				Plane pl = GetPlane();
				ICurve2D c2d = GetProjectedCurve(pl);
				if (linesOnly || this.Length < maxError)
				{
					ICurve2D approx = c2d.Approximate(linesOnly, maxError);
					return approx.MakeGeoObject(pl) as ICurve;
				}
				else
				{
					ArcLineFitting2D alf = new ArcLineFitting2D(c2d, maxError, false, true, 4);
					return alf.Approx.MakeGeoObject(pl) as ICurve;
				}
			}
		}
		double[] ICurve.TangentPosition(GeoVector direction)
		{
			List<double> res = new List<double>();
			if (Precision.IsDirectionInPlane(direction, this.plane))
			{
				double u = TangentParameter(direction);
				GeoPoint p1 = plane.Location + Math.Cos(u) * majorRadius * plane.DirectionX + Math.Sin(u) * minorRadius * plane.DirectionY;
				double u1 = PositionOf(p1);
				if (u1 >= 0.0 && u1 <= 1.0)
				{
					res.Add(u1);
				}
				GeoPoint p2 = plane.Location + Math.Cos(u + Math.PI) * majorRadius * plane.DirectionX + Math.Sin(u + Math.PI) * minorRadius * plane.DirectionY;
				double u2 = PositionOf(p2);
				if (u2 >= 0.0 && u2 <= 1.0)
				{
					res.Add(u2);
				}
			}
			return res.ToArray();
		}
		double[] ICurve.GetSelfIntersections()
		{
			return new double[0];
		}
		bool ICurve.SameGeometry(ICurve other, double precision)
		{
			if (other is Ellipse)
			{
				Ellipse o = other as Ellipse;
				if ((Center | o.Center) < precision)
				{   // geändert wg. Dan Swope 18.9.15. gegeneinadner verderehte Kreise und invers orientierte Bögen sollen gleich sein
					if (IsCircle && o.IsCircle)
					{
						if (this.IsArc)
						{
							if (((this.StartPoint | o.StartPoint) < precision && (this.EndPoint | o.EndPoint) < precision) || (this.StartPoint | o.EndPoint) < precision && (this.EndPoint | o.StartPoint) < precision)
							{
								return (this.PointAt(0.5) | o.PointAt(0.5)) < precision; // Mittelpunkt muss übereinstimmen
							}
							return false;
						}
						else
						{
							return !o.IsArc && (Math.Abs(this.Radius - o.Radius) < precision); // egal wie verdreht
						}
					}
					else if (!IsCircle && !o.IsCircle)
					{
						if (this.IsArc)
						{
							if (((this.StartPoint | o.StartPoint) < precision && (this.EndPoint | o.EndPoint) < precision) || (this.StartPoint | o.EndPoint) < precision && (this.EndPoint | o.StartPoint) < precision)
							{
								return (this.PointAt(0.5) | o.PointAt(0.5)) < precision; // Mittelpunkt muss übereinstimmen
																						 // ist eine Ellipse denkbar, die in Mittelpunkt und 3 Punkten übereinstimmt, aber nicht identisch ist?
							}
							return false;
						}
						else
						{
							if (Precision.SameDirection(MajorAxis, o.MajorAxis, false))
							{
								return !o.IsArc && (Math.Abs(this.majorRadius - o.majorRadius) < precision) && (Math.Abs(this.minorRadius - o.minorRadius) < precision);
							}
							return false;
						}
					}
				}
			}
			else
			{
				TetraederHull th = new TetraederHull(this);
				return th.SameGeometry(other, precision);
			}
			return false;
		}
		double ICurve.PositionAtLength(double position)
		{
			if (IsCircle)
			{
				return position / Length;
			}
			else
			{   // das ist nicht exakt, kann am Ende probleme machen
				// und es ist sehr ineffizient!!
				ICurve aprx = (this as ICurve).Approximate(true, (majorRadius + minorRadius) * 1e-4);
				double par = aprx.PositionAtLength(position);
				return PositionOf(aprx.PointAt(par));
			}
		}
		double ICurve.ParameterToPosition(double parameter)
		{
			return (parameter - startParameter) / sweepParameter;
		}
		double ICurve.PositionToParameter(double position)
		{
			return startParameter + position * sweepParameter;
		}
		bool ICurve.Extend(double atStart, double atEnd)
		{
			if (!IsArc) return false;
			if (IsCircle)
			{   // valid for circular arcs, but not real ellipses
				double ds = atStart / majorRadius;
				double de = atEnd / majorRadius;
				Angle newStartAngle;
				Angle newEndAngle;
				if (sweepParameter > 0)
				{
					newStartAngle = startParameter - ds;
					newEndAngle = startParameter + sweepParameter + de;
				}
				else
				{
					newStartAngle = startParameter + ds;
					newEndAngle = startParameter + sweepParameter - de;
				}
				SweepAngle newSweepAngle = new SweepAngle(newStartAngle, newEndAngle, sweepParameter > 0.0);
				using (new Changing(this, "CopyGeometry", Clone()))
				{
					startParameter = newStartAngle.Radian;
					sweepParameter = newSweepAngle.Radian;
				}
			}
			else
			{
				// this is only an approximate solution
				Ellipse fullEllipse = Ellipse.Construct();
				fullEllipse.SetEllipseCenterAxis(Center, MajorAxis, MinorAxis);
				ICurve aprx = (fullEllipse as ICurve).Approximate(true, (majorRadius + minorRadius) * 1e-4); // a path or polyline
				double lengthAtStart = aprx.PositionToParameter(aprx.PositionOf(this.StartPoint)); // parameter of polyline or path is its length
				double lengthAtEnd = aprx.PositionToParameter(aprx.PositionOf(this.EndPoint)); // parameter of polyline or path is its length
				lengthAtStart -= atStart;
				if (lengthAtStart < 0) lengthAtStart += aprx.Length;
				lengthAtEnd += atEnd;
				if (lengthAtEnd > aprx.Length) lengthAtEnd -= aprx.Length;
				GeoPoint sp = aprx.PointAt(aprx.ParameterToPosition(lengthAtStart));
				GeoPoint ep = aprx.PointAt(aprx.ParameterToPosition(lengthAtEnd));
				fullEllipse.Trim(fullEllipse.PositionOf(sp), fullEllipse.PositionOf(ep));
				using (new Changing(this, "CopyGeometry", Clone()))
				{
					startParameter = fullEllipse.startParameter;
					sweepParameter = fullEllipse.sweepParameter;
				}
				// this probably doesn't take into account the orientation of the ellipse
			}
			return true;
		}

		BoundingCube ICurve.GetExtent()
		{
			return GetExtent(0.0);
		}
		bool ICurve.HitTest(BoundingCube bc)
		{
			if (!bc.Interferes(this.GetBoundingCube()))
				return false;
			if (bc.Contains(StartPoint) || bc.Contains(EndPoint))
				return true;
			// das folgende scheint mir viel zu aufwendig.
			// könnte man nicht die 2d projektionen auf die 3 Seiten betrachten
			// und wenn es jeweils mit dem Rechteck interferiert, dann ist es auch getroffen?
			GeoPoint[] points = bc.Points;
			List<GeoPoint> l = new List<GeoPoint>(6);
			for (int i = 0; i < 8; ++i)
			{
				if (plane.Elem(points[i]))
					l.Add(points[i]);
			}
			int n = l.Count;
			GeoPoint[,] bcl = bc.Lines;
			for (int i = 0; i < 12; ++i)
			{
				bool b = true;
				for (int j = 0; j < n; ++j)
				{
					b = b && bcl[i, 0] != l[j] && bcl[i, 1] != l[j];
				}
				if (b)
					l.AddRange(plane.Interfere(bcl[i, 0], bcl[i, 1]));
			}
			// im Folgenden wird erwartet, dass majorRadius>=minorRadius. Ist das immer der Fall?
			Ellipse2D e = new Ellipse2D(new GeoPoint2D(0, 0),
				majorRadius * GeoVector2D.XAxis, minorRadius * GeoVector2D.YAxis);
			double spar = startParameter;
			SweepAngle sw = new SweepAngle(GeoVector2D.XAxis, e.majorAxis);
			if (sw != 0.0) spar -= sw; // für den Fall, dass die Ellipse nicht in Richtung x-Achse zeigt
			for (int i = 0; i < l.Count - 1; ++i)
			{
				for (int j = i + 1; j < l.Count; ++j)
				{
					GeoPoint2D sp = plane.Project(l[i]);
					GeoPoint2D ep = plane.Project(l[j]);
					double length = (ep - sp).Length;
					GeoPoint2D[] ip = Geometry.IntersectEL(new GeoPoint2D(0, 0), majorRadius, minorRadius, 0, sp, ep);

					for (int k = 0; k < ip.Length; ++k)
					{
						if ((ep - ip[k]).Length < length && (ip[k] - sp).Length < length)
						{
							double dip = e.PositionOf(ip[k]); // 1-D Point between 0 & 1
							for (int m = -1; m < 2; m++)
							{
								double x = (dip + m) * (2 * Math.PI);
								if ((sweepParameter > 0 && spar < x && x < spar + sweepParameter)
									|| (sweepParameter < 0 && spar + sweepParameter < x && x < spar))
									return true;
							}
						}
					}
				}
			}
			return false;
		}
		double[] ICurve.GetSavePositions()
		{
			int n = (int)Math.Max(2, Math.Ceiling(Math.Abs(sweepParameter) / Math.PI * 2.0) + 1);
			double[] res = new double[n];
			for (int i = 0; i < n; i++)
			{
				res[i] = i / (double)(n - 1);
			}
			return res;
			// return new double[] { 0.0, 0.25, 0.5, 0.75, 1.0 };
		}
		double[] ICurve.GetExtrema(GeoVector direction)
		{

			// es sind die beiden Schnittpunkte der Ebene durch den Mittelpunkt die normale und direction
			if (Precision.SameDirection(direction, plane.Normal, false)) return new double[0];
			Plane pln = new Plane(Center, direction, plane.Normal ^ direction);
			ICurve2D c2d = (this as ICurve).GetProjectedCurve(pln);
			double[] tp = c2d.TangentPointsToAngle(GeoVector2D.YAxis); // direction ist ja die x-Achse
			double[] res = new double[tp.Length];
			for (int i = 0; i < tp.Length; ++i)
			{
				res[i] = PositionOf(pln.ToGlobal(c2d.PointAt(tp[i])));
				// Na, hier sieht man die Wichtigkeit eines Punktobjektes, welches unbestimmt bleibt und alles mögliche liefern kann
				// so wie linq in CSharp 3.0. Also: man könnte das Punktobjekt fragen nach PositionOnCurve, PositionOnCurve2D/Plane2D, PositionOnSurface
				// PositionInWorldSpace und dieses Punktobjekt kennt verschiedene daten und andere muss es erst berechnen
			}
			return res;
		}
		double[] ICurve.GetPlaneIntersection(Plane plane)
		{
			return (this as ISimpleCurve).GetPlaneIntersection(plane);
		}
		double ICurve.DistanceTo(GeoPoint p)
		{
			double pos = (this as ICurve).PositionOf(p);
			if (pos >= 0.0 && pos <= 1.0)
			{
				GeoPoint pCurve = (this as ICurve).PointAt(pos);
				return pCurve | p;
			}
			else
			{
				return Math.Min(p | StartPoint, p | EndPoint);
			}
		}
		/// <summary>
		/// Implements <see cref="CADability.GeoObject.ICurve.TryPointDeriv2At (double, out GeoPoint, out GeoVector, out GeoVector)"/>
		/// </summary>
		/// <param name="position"></param>
		/// <param name="point"></param>
		/// <param name="deriv"></param>
		/// <param name="deriv2"></param>
		/// <returns></returns>
		public virtual bool TryPointDeriv2At(double position, out GeoPoint point, out GeoVector deriv, out GeoVector deriv2)
		{
			point = GeoPoint.Origin;
			deriv = deriv2 = GeoVector.NullVector;
			return false;
		}
		#endregion
		#region ISerializable Members
		/// <summary>
		/// Constructor required by deserialization
		/// </summary>
		/// <param name="info">SerializationInfo</param>
		/// <param name="context">StreamingContext</param>
		protected Ellipse(SerializationInfo info, StreamingContext context)
			: base(info, context)
		// : base(context)
		{
			plane = (Plane)info.GetValue("Plane", typeof(Plane));
			majorRadius = (double)info.GetValue("MajorRadius", typeof(double));
			minorRadius = (double)info.GetValue("MinorRadius", typeof(double));
			startParameter = (double)info.GetValue("StartParameter", typeof(double));
			sweepParameter = (double)info.GetValue("SweepParameter", typeof(double));
			colorDef = ColorDef.Read("ColorDef", info, context);
			lineWidth = LineWidth.Read("LineWidth", info, context);
			linePattern = LinePattern.Read("LinePattern", info, context);

			//SerializationInfoEnumerator e = info.GetEnumerator();
			//while (e.MoveNext())
			//{
			//    switch (e.Name)
			//    {
			//        default:
			//            base.SetSerializationValue(e.Name, e.Value);
			//            break;
			//        case "Plane":
			//            plane = (Plane)e.Value;
			//            break;
			//        case "MajorRadius":
			//            majorRadius = (double)e.Value;
			//            break;
			//        case "MinorRadius":
			//            minorRadius = (double)e.Value;
			//            break;
			//        case "StartParameter":
			//            startParameter = (double)e.Value;
			//            break;
			//        case "SweepParameter":
			//            sweepParameter = (double)e.Value;
			//            break;
			//        case "ColorDef":
			//            colorDef = e.Value as ColorDef;
			//            break;
			//        case "LineWidth":
			//            lineWidth = e.Value as LineWidth;
			//            break;
			//        case "LinePattern":
			//            linePattern = e.Value as LinePattern;
			//            break;
			//    }
			//}

			projectionData = new Dictionary<Projection, EllipseData2D>();
			extent = BoundingCube.EmptyBoundingCube;
			lockApproximationRecalc = new object();
			if (Constructed != null) Constructed(this); // ist hoffentlich nicht zu früh hier...
		}

		/// <summary>
		/// Implements <see cref="ISerializable.GetObjectData"/>
		/// </summary>
		/// <param name="info">The <see cref="System.Runtime.Serialization.SerializationInfo"/> to populate with data.</param>
		/// <param name="context">The destination (<see cref="System.Runtime.Serialization.StreamingContext"/>) for this serialization.</param>
		public override void GetObjectData(SerializationInfo info, StreamingContext context)
		{
			base.GetObjectData(info, context);
			info.AddValue("Plane", plane);
			info.AddValue("MajorRadius", majorRadius);
			info.AddValue("MinorRadius", minorRadius);
			info.AddValue("StartParameter", startParameter);
			info.AddValue("SweepParameter", sweepParameter);
			info.AddValue("ColorDef", colorDef);
			info.AddValue("LineWidth", lineWidth);
			info.AddValue("LinePattern", linePattern);
		}

		public override void GetObjectData(IJsonWriteData data)
		{
			base.GetObjectData(data);
			data.AddProperty("Plane", plane);
			data.AddProperty("MajorRadius", majorRadius);
			data.AddProperty("MinorRadius", minorRadius);
			data.AddProperty("StartParameter", startParameter);
			data.AddProperty("SweepParameter", sweepParameter);
			if (colorDef != null) data.AddProperty("ColorDef", colorDef);
			if (lineWidth != null) data.AddProperty("LineWidth", lineWidth);
			if (linePattern != null) data.AddProperty("LinePattern", linePattern);
		}

		public override void SetObjectData(IJsonReadData data)
		{
			base.SetObjectData(data);
			plane = data.GetProperty<Plane>("Plane");
			majorRadius = data.GetProperty<double>("MajorRadius");
			minorRadius = data.GetProperty<double>("MinorRadius");
			startParameter = data.GetProperty<double>("StartParameter");
			sweepParameter = data.GetProperty<double>("SweepParameter");
			colorDef = data.GetPropertyOrDefault<ColorDef>("ColorDef");
			lineWidth = data.GetPropertyOrDefault<LineWidth>("LineWidth");
			linePattern = data.GetPropertyOrDefault<LinePattern>("LinePattern");
		}


		#endregion
		#region ICndHlp3DEdge Members
		#endregion
		#region IExtentedableCurve Members
		IOctTreeInsertable IExtentedableCurve.GetExtendedCurve(ExtentedableCurveDirection direction)
		{
			return new FullEllipse(this);
		}
		#endregion

		#region ISimpleCurve Members

		double[] ISimpleCurve.GetPlaneIntersection(Plane pln)
		{
			GeoPoint[] res = PlaneIntersection(pln);
			double[] par = new double[res.Length];
			for (int i = 0; i < res.Length; ++i)
			{
				par[i] = PositionOf(res[i]);
			}
			return par;
		}
		#endregion
		ExplicitPCurve3D IExplicitPCurve3D.GetExplicitPCurve3D()
		{
			ModOp m = ModOp.Fit(new GeoPoint[] { GeoPoint.Origin, new GeoPoint(1.0, 0.0, 0.0), new GeoPoint(0.0, 1.0, 0.0) },
				new GeoPoint[] { Center, Center + MajorAxis, Center + MinorAxis }, true);
			return ExplicitPCurve3D.MakeCircle(m);
		}

		int IExportStep.Export(ExportStep export, bool topLevel)
		{
			//BSpline bsp = ToBSpline();
			//return (bsp as IExportStep).Export(export, topLevel);
			int ax;
			if (sweepParameter < 0) ax = export.WriteAxis2Placement3d(Center, -Normal, -Plane.DirectionX);
			else ax = export.WriteAxis2Placement3d(Center, Normal, Plane.DirectionX);
			int res;
			if (IsCircle)
			{
				res = export.WriteDefinition("CIRCLE('',#" + ax.ToString() + "," + export.ToString(Radius) + ")");
			}
			else
			{
				res = export.WriteDefinition("ELLIPSE('',#" + ax.ToString() + "," + export.ToString(MajorRadius) + "," + export.ToString(MinorRadius) + ")");
			}
			if (topLevel)
			{
				GeoPoint sp, ep;
				double spar, epar;
				sp = StartPoint;
				ep = EndPoint;
				spar = startParameter;
				epar = startParameter + sweepParameter;
				int nsp = (sp as IExportStep).Export(export, false);
				int nep = (ep as IExportStep).Export(export, false);
				int ntc;
				if (IsArc)
				{
					ntc = export.WriteDefinition("TRIMMED_CURVE('',#" + res.ToString() + ",(#" + nsp.ToString() + ",PARAMETER_VALUE(" + export.ToString(spar) + ")),(#" + nep.ToString() + ",PARAMETER_VALUE(" + export.ToString(epar) + ")),.T.,.CARTESIAN.)");
				}
				else
				{
					ntc = res;
				}
				int gcs = export.WriteDefinition("GEOMETRIC_CURVE_SET('',(#" + ntc.ToString() + "))");
				ColorDef cd = ColorDef;
				if (cd == null) cd = new ColorDef("Black", Color.Black);
				cd.MakeStepStyle(gcs, export);
				int product = export.WriteDefinition("PRODUCT( '','','',(#2))");
				int pdf = export.WriteDefinition("PRODUCT_DEFINITION_FORMATION_WITH_SPECIFIED_SOURCE( ' ', 'NONE', #" + product.ToString() + ", .NOT_KNOWN. )");
				int pd = export.WriteDefinition("PRODUCT_DEFINITION( 'NONE', 'NONE', #" + pdf.ToString() + ", #3 )");
				int pds = export.WriteDefinition("PRODUCT_DEFINITION_SHAPE( 'NONE', 'NONE', #" + pd.ToString() + " )");
				int sr = export.WriteDefinition("SHAPE_REPRESENTATION('', ( #" + gcs.ToString() + "), #4 )");
				export.WriteDefinition("SHAPE_DEFINITION_REPRESENTATION( #" + pds.ToString() + ", #" + sr.ToString() + ")");
				return sr;
			}
			else
			{
				return res;
			}
		}

	}

	internal class QuadTreeEllipse : IQuadTreeInsertableZ
	{
		ICurve2D curve;
		//double fx, fy, c; // beschreibt die Ebene für den Z-Wert
		IGeoObject go;
		Projection projection;
		Plane plane;
		public QuadTreeEllipse(IGeoObject go, Plane plane, ICurve2D curve, Projection projection)
		{
			this.go = go;
			this.curve = curve;
			this.plane = plane;
			this.plane.Modify(projection.UnscaledProjection);
			this.projection = projection;
			// die Berechnung der Matrix dauert zu lange
			//GeoPoint p1 = projection.UnscaledProjection * plane.Location;
			//GeoPoint p2 = projection.UnscaledProjection * (plane.Location + plane.DirectionX);
			//GeoPoint p3 = projection.UnscaledProjection * (plane.Location + plane.DirectionY);
			// die Werte fx, fy und c bestimmen die Z-Position
			//double[,] m = new double[3, 3];
			//m[0, 0] = p1.x;
			//m[0, 1] = p1.y;
			//m[0, 2] = 1.0;
			//m[1, 0] = p2.x;
			//m[1, 1] = p2.y;
			//m[1, 2] = 1.0;
			//m[2, 0] = p3.x;
			//m[2, 1] = p3.y;
			//m[2, 2] = 1.0;
			//double[,] b = new double[,] { { p1.z }, { p2.z }, { p3.z } };
			//LinearAlgebra.Matrix mx = new CADability.LinearAlgebra.Matrix(m);
			//try
			//{
			//    LinearAlgebra.Matrix s = mx.Solve(new CADability.LinearAlgebra.Matrix(b));
			//    fx = s[0, 0];
			//    fy = s[0, 1];
			//    c = s[0, 2];
			//}
			//catch (System.ApplicationException)
			//{   // sollte nie vorkommen, da die Ebene bei der Projektion nicht verschwindet
			//    // Z-Wert ist maximum
			//    fx = 0.0;
			//    fy = 0.0;
			//    c = 0.0;
			//    this.projection = projection; // d.h. es is kein einfacher Zusammenhang (Projektion von der Seite)
			//}
		}
		#region IQuadTreeInsertableZ Members
		public double GetZPosition(GeoPoint2D p)
		{
			// Equation plane.loc + l1*plane.dirx + l2*plane.diry = (p.x,p.y)
			double[,] m = new double[2, 2];
			m[0, 0] = plane.DirectionX.x;
			m[0, 1] = plane.DirectionY.x;
			m[1, 0] = plane.DirectionX.y;
			m[1, 1] = plane.DirectionY.y;
			double[] b = new double[] { p.x - plane.Location.x, p.y - plane.Location.y };
			Matrix mx = DenseMatrix.OfArray(m);
			Vector s = (Vector)mx.Solve(new DenseVector(b));
			if (s.IsValid())
			{
				double l1 = s[0];
				double l2 = s[1];
				return plane.Location.z + l1 * plane.DirectionX.z + l2 * plane.DirectionY.z;
			}

			if (projection != null)
			{   // die Ellipse von der Seite her gesehen (also eine Linie in der Projektion)
				// hier wird umständlich in die Ebene der Ellipse gerechnet und dort der Schnittpunkt des
				// Sehstrahls mit der Ellipse genommen
				GeoPoint pp = projection.ProjectionPlane.ToGlobal(p);
				Plane pl = (go as ICurve).GetPlane();
				GeoPoint2D lstart = pl.Project(pp); // in die Ebene der Ellipse
				GeoVector2D ldir = pl.Project(projection.Direction);
				ICurve2D c2d = (go as ICurve).GetProjectedCurve(pl); // in die eigene Ebene projiziert
				GeoPoint2DWithParameter[] ip = c2d.Intersect(lstart, lstart + ldir);
				double z = double.MinValue;
				for (int i = 0; i < ip.Length; ++i)
				{
					GeoPoint ppp = projection.UnscaledProjection * pl.ToGlobal(ip[i].p);
					z = Math.Max(z, ppp.z);
				}
				return z;
			}

			//Will always return 0 as fx, fy and c always have the default value of 0.0d
			return 0.0d;
			//return fx * p.x + fy * p.y + c;
		}
		#endregion
		#region IQuadTreeInsertable Members
		public BoundingRect GetExtent()
		{
			return curve.GetExtent();
		}
		public bool HitTest(ref BoundingRect rect, bool includeControlPoints)
		{
			return curve.HitTest(ref rect, false);
		}
		public object ReferencedObject
		{
			get { return go; }
		}
		#endregion
	}

	internal class FullEllipse : IOctTreeInsertable
	{
		Ellipse full;
		public FullEllipse(Ellipse arc)
		{
			full = arc.Clone() as Ellipse;
			if (arc.SweepParameter > 0) full.SweepParameter = 2.0 * Math.PI;
			else full.SweepParameter = -2.0 * Math.PI;
		}
		#region IOctTreeInsertable Members

		BoundingCube IOctTreeInsertable.GetExtent(double precision)
		{
			return full.GetExtent(precision);
		}

		bool IOctTreeInsertable.HitTest(ref BoundingCube cube, double precision)
		{
			return full.HitTest(ref cube, precision);
		}

		bool IOctTreeInsertable.HitTest(Projection projection, BoundingRect rect, bool onlyInside)
		{
			return full.HitTest(projection, rect, onlyInside);
		}

		double IOctTreeInsertable.Position(GeoPoint fromHere, GeoVector direction, double precision)
		{
			return full.Position(fromHere, direction, precision);
		}

		bool IOctTreeInsertable.HitTest(Projection.PickArea area, bool onlyInside)
		{
			throw new Exception("The method or operation is not implemented.");
		}

		#endregion
	}
}
