using System;
using System.Runtime.Serialization;
using CADability.GeoObject;

namespace CADability
{
    /// <summary>
    /// An angle defined in radians. The value of the angle is a double which is greater or equal to 0 
    /// and less than (and not equal) 2*pi.
    /// Via cast operators the angle seamlessly operates as a double.
    /// </summary>
    [Serializable()]
    public struct Angle : ISerializable
    {
        private double a;
        /// <summary>
        /// Constructs the angle that is enclosed by the two vectors (between 0 and Math.PI)
        /// </summary>
        /// <param name="v1">First vector</param>
        /// <param name="v2">Second vector</param>
        public Angle(GeoVector v1, GeoVector v2)
        {
            double arg = (v1.x * v2.x + v1.y * v2.y + v1.z * v2.z) / ((Math.Sqrt(v1.x * v1.x + v1.y * v1.y + v1.z * v1.z)) * Math.Sqrt(v2.x * v2.x + v2.y * v2.y + v2.z * v2.z));
            // eigentlich ist arg immer zwischen -1 und 1, durch Rundungsfehler kann es jedoch knapp außerhalb liegen
            if (arg > 1.0) arg = 1.0;
            if (arg < -1.0) arg = -1.0;
            a = Math.Acos(arg);
            if (a < 0.0) a += 2.0 * Math.PI;
            if (a >= 2.0 * Math.PI) a -= 2.0 * Math.PI;
        }
        /// <summary>
        /// Constructs the angle that is needed to rotate the first vector counterclockwise to reach the second vector (between 0 and 2.0*Math.PI)
        /// </summary>
        /// <param name="v1">First vector</param>
        /// <param name="v2">Second vector</param>
        public Angle(GeoVector2D v1, GeoVector2D v2)
        {
            a = Math.Acos((v1.x * v2.x + v1.y * v2.y) / ((Math.Sqrt(v1.x * v1.x + v1.y * v1.y)) * Math.Sqrt(v2.x * v2.x + v2.y * v2.y)));
            if (a < 0.0) a += 2.0 * Math.PI;
            if (a >= 2.0 * Math.PI) a -= 2.0 * Math.PI;
        }
        /// <summary>
        /// Constructs the angle
        /// </summary>
        /// <param name="Target"></param>
        /// <param name="Center"></param>
        public Angle(GeoPoint2D Target, GeoPoint2D Center)
        {
            if (Target.x == Center.x && Target.y == Center.y) a = 0.0;
            a = Math.Atan2(Target.y - Center.y, Target.x - Center.x);
            if (a < 0.0) a += 2.0 * Math.PI;
        }
        /// <summary>
        /// Constructs the angle of the given vector. X-axis is 0, counterclockwise.
        /// </summary>
        /// <param name="v">vector to define the angle</param>
        public Angle(GeoVector2D v)
        {
            a = Math.Atan2(v.y, v.x);
            if (a < 0.0) a += 2.0 * Math.PI;
            if (a >= 2.0 * Math.PI) a -= 2.0 * Math.PI;
        }
        /// <summary>
        /// Constructs an angle of a 2d vector (dx,dy) 
        /// </summary>
        /// <param name="dx">x-axis difference</param>
        /// <param name="dy">y-axis difference</param>
        public Angle(double dx, double dy)
        {
            if (dx == 0.0 && dy == 0.0) a = 0.0; // gefällt so nicht!
            a = Math.Atan2(dy, dx);
            if (a < 0.0) a += 2.0 * Math.PI;
        }
        /// <summary>
        /// Constructs an angle from the provided parameter
        /// </summary>
        /// <param name="d">Angle in radians</param>
        public Angle(double d)
        {
            this = (Angle)d;
        }
        /// <value>
        /// Zugriff auf das Bogenmaß
        /// </value>
        public double Radian
        {
            get
            {
                return a;
            }
            set
            {
                if (value < 0.0) value += 2.0 * Math.PI;
                a = Math.IEEERemainder(value, 2.0 * Math.PI);
            }
        }
        /// <value>
        /// Zugriff auf das Gradmaß
        /// </value>
        public double Degree
        {
            get
            {
                return a / Math.PI * 180;
            }
            set
            {
                Radian = value / 180 * Math.PI;
            }
        }
        /// <summary>
        /// Quadrant of the angle. Yields 0 to 3. 0: right top,
        /// 1: left top, 2: left bottom, 3: right bottom
        /// </summary>
        public int Quadrant
        {
            get { return (int)(a / Math.PI * 2.0 + Math.Sign(a) * 1e-13); } // (int)> value is rounded towards zero. +1e-13 war nötig wg. Vollkreis, der bis auf Rundung startwinkel 360° hatte
        }
        /// <summary>
        /// Sets the angle to the angle of the given vector. Gets a unit vector with the angle.
        /// </summary>
        public GeoVector2D Direction
        {
            get
            {
                return new GeoVector2D(Math.Cos(a), Math.Sin(a));
            }
            set
            {
                a = Math.Atan2(value.y, value.x); // NullVector soll ruhig eine Exception geben
                if (a < 0.0) a += 2.0 * Math.PI;
            }
        }
        /// <summary>
        /// Representation of this angle as a string in degrees
        /// </summary>
        /// <returns></returns>
        public override string ToString()
        {
            return Degree.ToString() + "°";
        }
        /// <summary>
        /// Casts a double to an angle. The result will be between 0 and 2*pi
        /// <param name="v">double value</param>
        /// <returns>Angle</returns>
        /// </summary>
        public static implicit operator Angle(double v)
        {
            Angle res;
            res.a = Math.IEEERemainder(v, 2.0 * Math.PI);
            if (res.a < 0.0) res.a += 2.0 * Math.PI;
            return res;
        }
        /// <summary>
        /// Casts the sweep angle to an angle with the same value
        /// </summary>
        /// <param name="sw"></param>
        /// <returns></returns>
        public static implicit operator Angle(SweepAngle sw)
        {
            Angle res;
            res.a = Math.IEEERemainder(sw.Radian, 2.0 * Math.PI);
            if (res.a < 0.0) res.a += 2.0 * Math.PI;
            return res;
        }
        /// <summary>
        /// Casts an angle to a double. The radian of the angle is returned.
        /// <param name="ang">the angle</param>
        /// <returns>the radian</returns>
        /// </summary>
        public static implicit operator double(Angle ang)
        {
            return ang.a;
        }
        /// <summary>
        /// Returns an angle from the parameter interpreted as degrees
        /// </summary>
        /// <param name="d">Degrees of the angle</param>
        /// <returns>The angle</returns>
        public static Angle FromDegree(double d)
        {
            return new Angle(d / 180.0 * Math.PI);
        }

        /// <summary>
        /// Returns true if the radians differ less than 1e-6 or provided tolerance
        /// </summary>
        /// <param name="ang"></param>
        /// <param name="tolerance"></param>
        /// <returns></returns>
        public bool IsCloseTo(Angle ang, double tolerance = 1e-6)
        {
            double d = Math.Abs(ang.a - a);
            if (d > Math.PI) d = Math.Abs(d - 2 * Math.PI);
            return d < tolerance;
        }
        
        /// <summary>
        /// Liefert true, wenn der im Parameter gegebene TestWinkel von diesem Winkel
        /// ausgehend mit dem SweepAngle überstrichen wird.
        /// </summary>
        /// <param name="sa">Überstreichnung</param>
        /// <param name="test">zu Testende Winkelposition</param>
        /// <returns></returns>
        public bool Sweeps(SweepAngle sa, Angle test)
        {
            double t = test;
            if (sa.Radian > 0.0)
            {
                if (t < a) t += 2.0 * Math.PI;
                return t <= a + sa.Radian;
            }
            else
            {
                if (t > a) t -= 2.0 * Math.PI;
                return t >= a + sa.Radian; // sa.Radianist hier ja negativ
            }
        }
        /// <summary>
        /// Returns the angle minus the provided sweep angle
        /// </summary>
        /// <param name="angA">first argument</param>
        /// <param name="angB">subtract this</param>
        /// <returns>the difference</returns>
        public static Angle operator -(Angle angA, SweepAngle angB)
        {
            angA.a -= (double)angB;
            if (angA.a < 0) angA.a += 2.0 * Math.PI;
            if (angA.a > 2.0 * Math.PI) angA.a -= 2.0 * Math.PI;
            return angA;
        }
        /// <summary>
        /// Returns the sweep angle that leads from <paramref name="angB"/> to <paramref name="angA"/>
        /// </summary>
        /// <param name="angA">Target of the rotation</param>
        /// <param name="angB">Source of the rotation</param>
        /// <returns>The difference</returns>
        public static SweepAngle operator -(Angle angA, Angle angB)
        {
            return new SweepAngle(angA.a - angB.a);
        }
        /// <summary>
        /// Adds the sweep angle to the angle
        /// </summary>
        /// <param name="angA">First operand</param>
        /// <param name="angB">Sweep angle to add</param>
        /// <returns>The sum</returns>
        public static Angle operator +(Angle angA, SweepAngle angB)
        {
            angA.a += (double)angB;
            if (angA.a < 0) angA.a += 2.0 * Math.PI;
            if (angA.a >= 2.0 * Math.PI) angA.a -= 2.0 * Math.PI;
            // der Winkel soll niemals exakt 2*pi sein, IEEERemainder verhinder das auch, deshalb hier >=
            return angA;
        }
        /// <summary>
        /// Angle for 0°
        /// </summary>
        public static Angle A0
        {
            get { return new Angle(0.0); }
        }
        /// <summary>
        /// Angle for 30°
        /// </summary>
        public static Angle A30
        {
            get { return new Angle(Math.PI / 6.0); }
        }
        /// <summary>
        /// Angle for 45°
        /// </summary>
        public static Angle A45
        {
            get { return new Angle(Math.PI / 4.0); }
        }
        /// <summary>
        /// Angle for 60°
        /// </summary>
        public static Angle A60
        {
            get { return new Angle(Math.PI / 3.0); }
        }
        /// <summary>
        /// Angle for 90°
        /// </summary>
        public static Angle A90
        {
            get { return new Angle(Math.PI / 2.0); }
        }
        /// <summary>
        /// Angle for 180°
        /// </summary>
        public static Angle A180
        {
            get { return new Angle(Math.PI); }
        }
        /// <summary>
        /// Angle for 270°
        /// </summary>
        public static Angle A270
        {
            get { return new Angle(3.0 * Math.PI / 2.0); }
        }
        /// <summary>
        /// Creates an angle from the provided parameter (in degrees)
        /// </summary>
        /// <param name="deg">Degrees of the angle</param>
        /// <returns>The resulting angle</returns>
        public static Angle Deg(double deg) { return new Angle(deg / 180 * Math.PI); }
        #region ISerializable Members
        /// <summary>
        /// Constructor required by deserialization
        /// </summary>
        /// <param name="info">SerializationInfo</param>
        /// <param name="context">StreamingContext</param>
        Angle(SerializationInfo info, StreamingContext context)
        {
            a = (double)info.GetValue("A", typeof(double));
        }
        /// <summary>
        /// Implements <see cref="ISerializable.GetObjectData"/>
        /// </summary>
        /// <param name="info">The <see cref="System.Runtime.Serialization.SerializationInfo"/> to populate with data.</param>
        /// <param name="context">The destination (<see cref="System.Runtime.Serialization.StreamingContext"/>) for this serialization.</param>
        public void GetObjectData(SerializationInfo info, StreamingContext context)
        {
            info.AddValue("A", a, a.GetType());
        }

        #endregion
    }


    /// <summary>
    /// A sweep angle. Typically the value of this object is between -2*pi and +2*pi.
    /// Used for rotation operations etc.
    /// </summary>
    [Serializable()]
    public struct SweepAngle : ISerializable
    {
        private double a;
        /// <summary>
        /// Constructs the sweep angle as the angle of the vector from Center to Target
        /// </summary>
        /// <param name="Target">Endpoint of the vector</param>
        /// <param name="Center">Startpoint of the vector</param>
        public SweepAngle(GeoPoint2D Target, GeoPoint2D Center)
        {
            a = Math.Atan2(Target.y - Center.y, Target.x - Center.x);
            // a ist schon OK zwischen -2*pi und 2*pi
        }
        /// <summary>
        /// Constructs the sweep angle you need to go from <paramref name="From"/> to <paramref name="To"/>.
        /// </summary>
        /// <param name="From">starting vector</param>
        /// <param name="To">target vector</param>
        public SweepAngle(GeoVector From, GeoVector To)
        {
            double l = From.Length * To.Length;
            if (l == 0.0) a = 0;
            else
            {
                double acosarg = From * To / (From.Length * To.Length);
                if (acosarg > 1.0) a = 0.0;
                else if (acosarg < -1.0) a = Math.PI;
                else a = Math.Acos(acosarg);
            }
        }
        /// <summary>
        /// Constructs a sweep angle from the vector <paramref name="From"/> to the vector <paramref name="To"/>.
        /// </summary>
        /// <param name="From"></param>
        /// <param name="To"></param>
        public SweepAngle(GeoVector2D From, GeoVector2D To)
        {	// noch nicht getestet:
            // manchmal ist das Argument um Rundungsgenauigkeit größer 1.0 bzw. kleine -1.0
            double acosarg = From * To / (From.Length * To.Length);
            if (double.IsNaN(acosarg) || acosarg > 1.0) a = 0.0;
            else if (acosarg < -1.0) a = Math.PI;
            else a = Math.Acos(acosarg);
            if ((From.x * To.y - From.y * To.x) < 0.0) a = -a;
        }
        /// <summary>
        /// Constructs the sweep angle you need to go from <paramref name="From"/> to <paramref name="To"/> in the
        /// direction defined by <paramref name="CounterClockwise"/>
        /// </summary>
        /// <param name="From">starting angle</param>
        /// <param name="To">ending angle</param>
        /// <param name="CounterClockwise">direction</param>
        public SweepAngle(Angle From, Angle To, bool CounterClockwise)
        {
            a = (double)To - (double)From;
            if (CounterClockwise && a < 0.0) a += 2 * Math.PI;
            if (!CounterClockwise && a > 0.0) a -= 2 * Math.PI;
        }
        /// <summary>
        /// Constructs the sweep angle with tan(sweep angle)==dy/dx
        /// </summary>
        /// <param name="dx"></param>
        /// <param name="dy"></param>
        public SweepAngle(double dx, double dy)
        {
            a = Math.Atan2(dy, dx);
        }
        /// <summary>
        /// Constructs the sweep angle with the provided radian (which must be between -2*pi and +2*pi, which is not checked)
        /// </summary>
        /// <param name="d"></param>
        public SweepAngle(double d)
        {
            a = d;
        }
        /// <value>
        /// Zugriff auf das Bogenmaß
        /// </value>
        public double Radian
        {
            get
            {
                return a;
            }
            set
            {
                a = Math.IEEERemainder(value, 4.0 * Math.PI);
                if (a < 2.0 * Math.PI) a += 4.0 * Math.PI;
                if (a > 2.0 * Math.PI) a -= 4.0 * Math.PI;
            }
        }
        /// <value>
        /// Zugriff auf das Gradmaß
        /// </value>
        public double Degree
        {
            get
            {
                return a / Math.PI * 180;
            }
            set
            {
                Radian = value / 180 * Math.PI;
            }
        }
        /// <summary>
        /// Cast Operator, der ein double in einen Winkel umwandelt. Es wird das
        /// modulo 2*pi berechnet
        /// </summary>
        /// <param name="v">double Wert</param>
        /// <returns>Winkel</returns>
        public static implicit operator SweepAngle(double v)
        {
            SweepAngle res;
            // res.Radian = v; // geht nicht, das res noch nicht initialisiert
            res.a = Math.IEEERemainder(v, 4.0 * Math.PI);
            if (res.a < -2.0 * Math.PI) res.a += 4.0 * Math.PI;
            if (res.a > 2.0 * Math.PI) res.a -= 4.0 * Math.PI;
            return res;
        }
        /// <summary>
        /// Casts an angle to a sweep angle with the same value
        /// </summary>
        /// <param name="a"></param>
        /// <returns></returns>
        public static implicit operator SweepAngle(Angle a)
        {
            SweepAngle res;
            // res.Radian = v; // geht nicht, das res noch nicht initialisiert
            res.a = Math.IEEERemainder(a.Radian, 4.0 * Math.PI);
            if (res.a < -2.0 * Math.PI) res.a += 4.0 * Math.PI;
            if (res.a > 2.0 * Math.PI) res.a -= 4.0 * Math.PI;
            return res;
        }
        /// <summary>
        /// Cast Operator, der einen Winkel in ein double umwandelt. Ergebnis ist das Bogenmaß 
        /// des Winkels.
        /// Es werden keine Berechnungen vorgenommen
        /// </summary>
        /// <param name="ang">der Winkel</param>
        /// <returns>double als Ergebnis</returns>
        public static implicit operator double(SweepAngle ang)
        {
            return ang.a;
        }
        /// <summary>
        /// The sweep angle that turns 90° to the left
        /// </summary>
        public static SweepAngle ToLeft = new SweepAngle(Math.PI / 2.0);
        /// <summary>
        /// The sweep angle that turns 90° to the right
        /// </summary>
        public static SweepAngle ToRight = new SweepAngle(-Math.PI / 2.0);
        /// <summary>
        /// The sweep angle that turns to the opposite directio
        /// </summary>
        public static SweepAngle Opposite = new SweepAngle(Math.PI);
        /// <summary>
        /// The sweep angle that turns 360° counterclockwise
        /// </summary>
        public static SweepAngle Full = new SweepAngle(2.0 * Math.PI);
        /// <summary>
        /// The sweep angle that turns 360° clockwise
        /// </summary>
        public static SweepAngle FullReverse = new SweepAngle(-2.0 * Math.PI);
        /// <summary>
        /// Returns the reverse of the provided sweep angle
        /// </summary>
        /// <param name="s"></param>
        /// <returns></returns>
        public static SweepAngle operator -(SweepAngle s)
        {
            return new SweepAngle(-s.a);
        }
        /// <summary>
        /// Returns the multiple of the sweep angle
        /// </summary>
        /// <param name="d">factor</param>
        /// <param name="s"></param>
        /// <returns></returns>
        public static SweepAngle operator *(double d, SweepAngle s)
        {
            return new SweepAngle(d * s.a);
        }
        /// <summary>
        /// Returns a SweepAngle that rotates by the given degrees
        /// </summary>
        /// <param name="d">amount to rotate in degrees (-360&lt;=d&lt;=360)</param>
        /// <returns>A new SweepAngle</returns>
        public static SweepAngle Deg(double d) { return new SweepAngle(d / 180 * Math.PI); }
        /// <summary>
        /// Returns true, if the sweep angles differ by less than 1e-12 (in radian)
        /// </summary>
        /// <param name="ang"></param>
        /// <returns></returns>
        public bool IsCloseTo(SweepAngle ang)
        {	// keine Ahnung, ob des so gut ist
            double d = Math.Abs(ang.a - a);
            if (d > Math.PI) d = Math.Abs(d - 2 * Math.PI);
            return d < 1e-12; // davon geht der sin noch
        }
        /// <summary>
        /// Returns a string representation of this sweep angle in degrees
        /// </summary>
        /// <returns></returns>
        public override string ToString()
        {
            return Degree.ToString(DebugFormat.Angle);
        }
        #region ISerializable Members
        /// <summary>
        /// Constructor required by deserialization
        /// </summary>
        /// <param name="info">SerializationInfo</param>
        /// <param name="context">StreamingContext</param>
        SweepAngle(SerializationInfo info, StreamingContext context)
        {
            a = (double)info.GetValue("A", typeof(double));
        }
        /// <summary>
        /// Implements <see cref="ISerializable.GetObjectData"/>
        /// </summary>
        /// <param name="info">The <see cref="System.Runtime.Serialization.SerializationInfo"/> to populate with data.</param>
        /// <param name="context">The destination (<see cref="System.Runtime.Serialization.StreamingContext"/>) for this serialization.</param>
        public void GetObjectData(SerializationInfo info, StreamingContext context)
        {
            info.AddValue("A", a, a.GetType());
        }

        #endregion
    }

}
