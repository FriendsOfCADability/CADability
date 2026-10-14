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
        public enum tExceptionType
        {
            /// <summary>
            /// The constructor could not build a coordinate system from the given directions, because one of them is
            /// the nullvector or both are parallel.
            /// </summary>
            ConstructorFailed
        };
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

        /// <summary>
        /// The global coordinate system: origin (0, 0, 0), x-axis (1, 0, 0), y-axis (0, 1, 0) and hence the normal (0, 0, 1).
        /// This is a static field rather than a property for speed; do not assign to it.
        /// </summary>
        public static CoordSys StandardCoordSys = new CoordSys(new GeoPoint(0.0, 0.0, 0.0), new GeoVector(1.0, 0.0, 0.0), new GeoVector(0.0, 1.0, 0.0));

        /// <summary>
        /// Creates a normalized, right handed coordinate system with the given origin and axes. <paramref name="DirectionX"/>
        /// is kept as the direction of the x-axis, the normal is the cross product of the two directions, and the y-axis is
        /// recomputed perpendicular to the x-axis and the normal; all three are normalized, so the given vectors need not be
        /// unit vectors or exactly perpendicular. Throws a <see cref="CoordSysException"/> if one of the directions is the
        /// nullvector or both are parallel.
        /// </summary>
        /// <param name="Location">the origin</param>
        /// <param name="DirectionX">direction of the x-axis</param>
        /// <param name="DirectionY">direction of the y-axis, adapted to be perpendicular to the x-axis</param>
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
        /// <summary>
        /// The transformation from global coordinates into the coordinates of this system, the inverse of
        /// <see cref="LocalToGlobal"/>. It is computed on first access and cached until the system changes.
        /// </summary>
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
        /// <summary>
        /// The transformation from the coordinates of this system into global coordinates: it maps the origin to
        /// <see cref="Location"/> and the unit vectors of the axes to <see cref="DirectionX"/>, <see cref="DirectionY"/> and
        /// <see cref="Normal"/>. It is computed on first access and cached until the system changes.
        /// </summary>
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
        /// <summary>
        /// The origin of the coordinate system in global coordinates. Setting it moves the system without changing its
        /// orientation and discards the cached transformations. CoordSys is a value type, so setting it affects only this copy.
        /// </summary>
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
        /// <summary>
        /// The unit vector of the x-axis in global coordinates. Read-only; the orientation is set by the constructor and
        /// changed with <see cref="Modify(ModOp)"/> or <see cref="Reverse"/>.
        /// </summary>
        public GeoVector DirectionX
        {
            get { return directionX; }
        }
        /// <summary>
        /// The unit vector of the y-axis in global coordinates, perpendicular to <see cref="DirectionX"/>. Read-only; the
        /// orientation is set by the constructor and changed with <see cref="Modify(ModOp)"/> or <see cref="Reverse"/>.
        /// </summary>
        public GeoVector DirectionY
        {
            get { return directionY; }
        }
        /// <summary>
        /// The unit vector of the z-axis in global coordinates, the cross product of <see cref="DirectionX"/> and
        /// <see cref="DirectionY"/> (the system is right handed). Read-only.
        /// </summary>
        public GeoVector Normal
        {
            get { return directionZ; }
        }
        /// <summary>
        /// Transforms this coordinate system by the given modification: the origin, the x-direction and the normal are
        /// transformed, the directions are normalized and the y-direction is recomputed from them, so the system stays
        /// normalized and right handed even if <paramref name="m"/> contains scaling or a reflection (in the latter case the
        /// y-direction is the opposite of the transformed old y-direction). The cached transformations are discarded.
        /// </summary>
        /// <param name="m">the modification to apply</param>
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
        /// <summary>
        /// Reverses the x-direction and the normal, keeping the origin and the y-direction. The result is this system rotated
        /// by 180 degrees about its y-axis, still right handed, with the opposite normal. The cached transformations are discarded.
        /// </summary>
        public void Reverse()
        {
            directionX = -directionX;
            directionZ = -directionZ;
            globalToLocal = null;
            localToGlobal = null;
        }
        /// <summary>
        /// Returns a parallel <see cref="Projection"/> that looks against the <see cref="Normal"/> onto the x/y plane of this
        /// system, with the x- and y-axes of this system as the horizontal and vertical directions. Used e.g. to compute the
        /// 2-dimensional extent of objects in the plane of this system.
        /// </summary>
        /// <returns>the projection onto this coordinate system</returns>
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
        /// Writes the data of this object for the legacy binary serialization (<see cref="System.Runtime.Serialization.ISerializable"/>).
        /// Files are written in the JSON format (<see cref="IJsonSerialize"/>) now; the binary format is still read.
        /// </summary>
        /// <param name="info">receives the data</param>
        /// <param name="context">the destination of the serialization</param>
        public void GetObjectData(SerializationInfo info, StreamingContext context)
        {
            info.AddValue("Location", Location, typeof(GeoPoint));
            info.AddValue("DirectionX", DirectionX, typeof(GeoVector));
            info.AddValue("DirectionY", DirectionY, typeof(GeoVector));
        }
        #endregion
        /// <summary>
        /// Constructor for the JSON deserialization (<see cref="IJsonSerialize"/>): reads the origin, the x-direction and the
        /// y-direction in the order <see cref="GetObjectData(IJsonWriteData)"/> wrote them and recomputes the normal. It is
        /// found by reflection, there is no need to call it directly.
        /// </summary>
        /// <param name="data">supplies the values</param>
        public CoordSys(IJsonReadStruct data)
        {
            location = data.GetValue<GeoPoint>();
            directionX = data.GetValue<GeoVector>();
            directionY = data.GetValue<GeoVector>();
            directionZ = directionX ^ directionY;
            globalToLocal = null;
            localToGlobal = null;
        }

        /// <summary>
        /// Writes the origin, the x-direction and the y-direction for the JSON serialization (<see cref="IJsonSerialize"/>).
        /// The normal and the cached transformations are not stored, they are recomputed when the struct is read back by the
        /// constructor taking an <see cref="IJsonReadStruct"/>.
        /// </summary>
        /// <param name="data">receives the values</param>
        public void GetObjectData(IJsonWriteData data)
        {
            data.AddValues(Location, DirectionX, DirectionY);
        }

        /// <summary>
        /// Does nothing. Value types serialized inline are restored by the constructor taking an <see cref="IJsonReadStruct"/>;
        /// this method only exists to satisfy <see cref="IJsonSerialize"/>.
        /// </summary>
        /// <param name="data">not used</param>
        public void SetObjectData(IJsonReadData data)
        {
        }
    }

}
