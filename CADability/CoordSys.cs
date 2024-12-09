using System;
using System.Runtime.Serialization;
using System.Threading;

namespace CADability
{

    /// <summary>
    /// Exception thrown by constructors of <see cref="CoordSys"/> indication a failure
    /// </summary>

    public class CoordSysException : System.ApplicationException
    {
        /// <summary>
        /// Enumeration of different causes of this exception
        /// </summary>
        public enum tExceptionType { ConstructorFailed };
        /// <summary>
        /// Cause of this exception
        /// </summary>
		public tExceptionType ExceptionType;
        internal CoordSysException(tExceptionType ExceptionType)
        {
            this.ExceptionType = ExceptionType;
        }
    }

    /// <summary>
    /// normalized, right handed coordinate system
    /// </summary>
    [Serializable]
    [JsonVersion(serializeAsStruct = true, version = 1)]
    public struct CoordSys : ISerializable, IJsonSerialize
    {
        private GeoPoint location; // Ursprung des Koordinatensystems
        private GeoVector directionX; // Richtung der X-Achse
        private GeoVector directionY; // Richtung der Y-Achse
        private GeoVector directionZ; // Rictung der Z-Achse, Normale, wird nichtgespeichert
        private ModOpRef globalToLocal; // rechnet globale Koordinaten in lokale um, wird nichtgespeichert
        private ModOpRef localToGlobal; // rechnet lokale Koordinaten in globale um, wird nichtgespeichert

        public static CoordSys StandardCoordSys = new CoordSys(new GeoPoint(0.0, 0.0, 0.0), new GeoVector(1.0, 0.0, 0.0), new GeoVector(0.0, 1.0, 0.0));

        public CoordSys(GeoPoint Location, GeoVector DirectionX, GeoVector DirectionY)
        {
            location = Location;
            directionX = DirectionX;
            directionY = DirectionY;
            directionZ = DirectionX ^ DirectionY;
            directionY = directionZ ^ directionX;
            try
            {
                directionX.Norm();
                directionY.Norm();
                directionZ.Norm();
            }
            catch (GeoVectorException)
            {
                throw new CoordSysException(CoordSysException.tExceptionType.ConstructorFailed);
            }

            globalToLocal = null;
            localToGlobal = null;
        }
        public ModOp GlobalToLocal
        {
            get
            {
                if (globalToLocal == null)
                {
                    // globalToLocal = new ModOpRef(ModOp.Transform(this,CoordSys.StandardCoordSys));
                    globalToLocal = new ModOpRef(LocalToGlobal.GetInverse());
                }
                return globalToLocal.ModOp;
            }
        }
        public ModOp LocalToGlobal
        {
            get
            {
                if (localToGlobal == null)
                {
                    // localToGlobal = new ModOpRef(ModOp.Transform(CoordSys.StandardCoordSys,this));
                    localToGlobal = new ModOpRef(new ModOp(directionX, directionY, directionZ, location));
                }
                return localToGlobal.ModOp;
            }
        }
        public GeoPoint Location
        {
            get { return location; }
            set
            {
                location = value;
                globalToLocal = null;
                localToGlobal = null;
            }
        }
        public GeoVector DirectionX
        {
            get { return directionX; }
        }
        public GeoVector DirectionY
        {
            get { return directionY; }
        }
        public GeoVector Normal
        {
            get { return directionZ; }
        }
        public void Modify(ModOp m)
        {
            location = m * location;
            directionX = m * directionX;
            directionX.Norm();
            directionZ = m * directionZ;
            directionZ.Norm();
            directionY = directionZ ^ directionX;
            directionZ = DirectionX ^ DirectionY;
            globalToLocal = null;
            localToGlobal = null;
        }
        public void Reverse()
        {
            directionX = -directionX;
            directionZ = -directionZ;
            globalToLocal = null;
            localToGlobal = null;
        }
        public Projection GetProjection()
        {
            return new Projection(this);
        }
        #region ISerializable Members
        private CoordSys(SerializationInfo info, StreamingContext context)
        {
            location = (GeoPoint)info.GetValue("Location", typeof(GeoPoint));
            directionX = (GeoVector)info.GetValue("DirectionX", typeof(GeoVector));
            directionY = (GeoVector)info.GetValue("DirectionY", typeof(GeoVector));
            directionZ = directionX ^ directionY;
            globalToLocal = null;
            localToGlobal = null;
        }
        /// <summary>
        /// Implements <see cref="ISerializable.GetObjectData"/>
        /// </summary>
        /// <param name="info">The <see cref="System.Runtime.Serialization.SerializationInfo"/> to populate with data.</param>
        /// <param name="context">The destination (<see cref="System.Runtime.Serialization.StreamingContext"/>) for this serialization.</param>
        public void GetObjectData(SerializationInfo info, StreamingContext context)
        {
            info.AddValue("Location", Location, typeof(GeoPoint));
            info.AddValue("DirectionX", DirectionX, typeof(GeoVector));
            info.AddValue("DirectionY", DirectionY, typeof(GeoVector));
        }
        #endregion
        public CoordSys(IJsonReadStruct data)
        {
            location = data.GetValue<GeoPoint>();
            directionX = data.GetValue<GeoVector>();
            directionY = data.GetValue<GeoVector>();
            directionZ = directionX ^ directionY;
            globalToLocal = null;
            localToGlobal = null;
        }

        public void GetObjectData(IJsonWriteData data)
        {
            data.AddValues(Location, DirectionX, DirectionY);
        }

        public void SetObjectData(IJsonReadData data)
        {
        }
    }

}
