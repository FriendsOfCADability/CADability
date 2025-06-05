using CADability.Attribute;
using CADability.UserInterface;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Runtime.Serialization;

namespace CADability.GeoObject
{
    /// <summary>
    /// Flags definig the symbol for the presentation of a <see cref="Point"/> object.
    /// The flag may contain one of the values Empty=0x0,Dot=0x1,Plus=0x2,Cross=0x3,Line=0x4 
    /// combined with Square=0x8 or Circle=0x10.
    /// </summary>
    [Flags]
    public enum PointSymbol
    {
        /// <summary>
        /// Don't display.
        /// </summary>
        Empty = 0x0,
        /// <summary>
        /// Display as a one pixel sized dot.
        /// </summary>
        Dot = 0x1,
        /// <summary>
        /// Display as a small "+"-shaped cross
        /// </summary>
        Plus = 0x2,
        /// <summary>
        /// Display as a small "X"-shaped cross
        /// </summary>
        Cross = 0x3,
        /// <summary>
        /// Display as a small vertical line
        /// </summary>
        Line = 0x4, // war 0x8, aber das funktionierte nicht richtig
        /// <summary>
        /// Display an additional square around the dot, cross or line
        /// </summary>
        Square = 0x10,
        /// <summary>
        /// Display an additional circle around the dot, cross or line
        /// </summary>
        Circle = 0x20,
        /// <summary>
        /// Do not use, internally used to show selected points
        /// </summary>
        Select = 0x40
    }
    /// <summary>
    /// Implements a point as a <see cref="IGeoObject"/>. 
    /// </summary>
    [Serializable()]
    public class Point : IGeoObjectImpl, IColorDef, ISerializable, IJsonSerialize
    {
        private GeoPoint location; // der Ort
        private PointSymbol symbol; // die Darstellung
        private double size; // die Größe (in Pixel?)
        private ColorDef colorDef;
        private string name;
        #region polymorph construction
        public delegate Point ConstructionDelegate();
        public static ConstructionDelegate Constructor;
        public static Point Construct()
        {
            if (Constructor != null) return Constructor();
            return new Point();
        }
        public delegate void ConstructedDelegate(Point justConstructed);
        public static event ConstructedDelegate Constructed;
        #endregion
        protected Point()
        {
            size = 3.0;
            if (Constructed != null) Constructed(this);
        }
        public GeoPoint Location
        {
            get { return location; }
            set
            {
                using (new Changing(this, "Location", location))
                {
                    location = value;
                }
            }
        }
        public PointSymbol Symbol
        {
            get { return symbol; }
            set
            {
                using (new Changing(this, "Symbol", symbol))
                {
                    symbol = value;
                }
            }
        }
        public double Size
        {
            get { return size; }
            set
            {
                using (new Changing(this, "Size", size))
                {
                    size = value;
                }
            }
        }
        public string Name
        {
            get
            {
                return name;
            }
            set
            {
                using (new Changing(this, false, true, "Name", name))
                {
                    name = value;
                }
            }
        }
        #region IGeoObjectImpl overrides
        /// <summary>
        /// Overrides <see cref="CADability.GeoObject.IGeoObjectImpl.Clone ()"/>
        /// </summary>
        /// <returns></returns>
        public override IGeoObject Clone()
        {
            Point res = Construct();
            res.location = location;
            res.symbol = symbol;
            res.size = size;
            res.colorDef = colorDef;
            res.name = name;
            res.CopyAttributes(this);
            return res;
        }
        /// <summary>
        /// Implements the <see cref="IGeoObjectImpl.CopyGeometry"/> method.
        /// </summary>
        /// <param name="ToCopyFrom"></param>
        public override void CopyGeometry(IGeoObject ToCopyFrom)
        {
            using (new Changing(this))
            {
                this.location = ((Point)ToCopyFrom).location;
                this.symbol = ((Point)ToCopyFrom).symbol;
                this.size = ((Point)ToCopyFrom).size;
                this.colorDef = ((Point)ToCopyFrom).colorDef;
            }
        }
        /// <summary>
        /// Overrides <see cref="CADability.GeoObject.IGeoObjectImpl.FindSnapPoint (SnapPointFinder)"/>
        /// </summary>
        /// <param name="spf"></param>
        public override void FindSnapPoint(SnapPointFinder spf)
        {
            if (!spf.Accept(this)) return;
            if (spf.SnapToObjectSnapPoint)
            {
                spf.Check(location, this, SnapPointFinder.DidSnapModes.DidSnapToObjectSnapPoint);
            }
        }
        /// <summary>
        /// Overrides <see cref="CADability.GeoObject.IGeoObjectImpl.GetBoundingCube ()"/>
        /// </summary>
        /// <returns></returns>
        public override BoundingCube GetBoundingCube()
        {
            BoundingCube res = BoundingCube.EmptyBoundingCube;
            res.MinMax(location);
            return res;
        }
        //		public override BoundingRect GetExtent(Projection projection, bool Use2DWorld, bool RegardLineWidth)
        //		{
        //			BoundingRect result = BoundingRect.EmptyBoundingRect;
        //			result.MinMax(projection.ProjectUnscaled(location));
        //			return result;
        //		}
        /// <summary>
        /// Overrides <see cref="CADability.GeoObject.IGeoObjectImpl.GetShowProperties (IFrame)"/>
        /// </summary>
        /// <param name="Frame"></param>
        /// <returns></returns>
        public override IPropertyEntry GetShowProperties(IFrame Frame)
        {
            return new ShowPropertyPoint(this, Frame);
        }
        //		public override bool HitTest(ref BoundingRect Rect, Projection projection, bool IncludeControlPoints)
        //		{
        //			GeoPoint2D l = projection.ProjectUnscaled(location);
        //			return l<=Rect;
        //		}
        //		public override bool HitTest(ref BoundingRect Rect, Projection projection, bool IncludeControlPoints, ref ModOp ApplyBeforeTest)
        //		{
        //			GeoPoint2D l = projection.ProjectUnscaled(ApplyBeforeTest*location);
        //			return l<=Rect;
        //		}
        //
        /// <summary>
        /// Overrides <see cref="CADability.GeoObject.IGeoObjectImpl.Modify (ModOp)"/>
        /// </summary>
        /// <param name="m"></param>
        public override void Modify(ModOp m)
        {
            using (new Changing(this, "Location", location))
            {
                location = m * location;
            }
        }
        //		public override void Paint(PaintToGDI PaintTo)
        //		{
        //			if (!PaintTo.AcceptLayer(this.Layer)) return;
        //			System.Drawing.Color color;
        //			if (colorDef!=null) color = colorDef.Color;
        //			else color = System.Drawing.Color.Black;
        //			PaintTo.DisplaySymbolW(location,size,symbol,color);
        //		}
        /// <summary>
        /// Overrides <see cref="CADability.GeoObject.IGeoObjectImpl.GetAttributeProperties (IFrame)"/>
        /// </summary>
        /// <param name="Frame"></param>
        /// <returns></returns>
        public override IPropertyEntry[] GetAttributeProperties(IFrame Frame)
        {
            List<IPropertyEntry> res = new List<IPropertyEntry>();
            res.AddRange(base.GetAttributeProperties(Frame));
            res.Add(new PointSymbolSelectionProperty(this, "Point.PointSymbol"));
            return res.ToArray();
        }
        public delegate bool PaintTo3DDelegate(Point toPaint, IPaintTo3D paintTo3D);
        public static PaintTo3DDelegate OnPaintTo3D;
        /// <summary>
        /// Overrides <see cref="CADability.GeoObject.IGeoObjectImpl.PrePaintTo3D (IPaintTo3D)"/>
        /// </summary>
        /// <param name="paintTo3D"></param>
        public override void PrePaintTo3D(IPaintTo3D paintTo3D)
        {
            paintTo3D.PreparePointSymbol(this.symbol);
            paintTo3D.PreparePointSymbol(this.symbol | PointSymbol.Select);
        }
        /// <summary>
        /// Overrides <see cref="CADability.GeoObject.IGeoObjectImpl.PaintTo3D (IPaintTo3D)"/>
        /// </summary>
        /// <param name="paintTo3D"></param>
        public override void PaintTo3D(IPaintTo3D paintTo3D)
        {
            if (!paintTo3D.SelectMode)
            {
                if (colorDef != null) paintTo3D.SetColor(colorDef.Color);
            }
            if (OnPaintTo3D != null && OnPaintTo3D(this, paintTo3D)) return;
            if (paintTo3D.SelectMode)
            {
                // paintTo3D.Points(new GeoPoint[] { this.location }, (float)this.size);
                // im Selectmode gibts Abstürze bei "0901_06_01_0.dxf", wenn man eine Bemaßung markiert
                // und die normale Darstellung verwendet
                paintTo3D.Points(new GeoPoint[] { location }, (float)size, this.symbol | PointSymbol.Select);
            }
            else
            {
                paintTo3D.Points(new GeoPoint[] { location }, 1.0f, symbol);
                // folgendes geht viel schneller:
                //paintTo3D.Polyline(new GeoPoint[] { location - size * GeoVector.XAxis, location + size * GeoVector.XAxis });
                //paintTo3D.Polyline(new GeoPoint[] { location - size * GeoVector.YAxis, location + size * GeoVector.YAxis });
                //paintTo3D.Polyline(new GeoPoint[] { location - size * GeoVector.ZAxis, location + size * GeoVector.ZAxis });
            }
        }
        /// <summary>
        /// Overrides <see cref="CADability.GeoObject.IGeoObjectImpl.PrepareDisplayList (double)"/>
        /// </summary>
        /// <param name="precision"></param>
        public override void PrepareDisplayList(double precision)
        {
            // nichts zu tun
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
            return new QuadTreePoint(this, projection);
        }
        public override string[] CustomAttributeKeys
        {
            get
            {
                List<string> res = new List<string>(base.CustomAttributeKeys);
                res.Add("PointSymbol");
                return res.ToArray();
            }
        }
        /// <summary>
        /// Overrides <see cref="CADability.GeoObject.IGeoObjectImpl.GetNamedAttribute (string)"/>
        /// </summary>
        /// <param name="name"></param>
        /// <returns></returns>
        public override INamedAttribute GetNamedAttribute(string name)
        {
            if (name == "PointSymbol") return new PointSymbolAttribute();
            return base.GetNamedAttribute(name);
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
        }
        /// <summary>
        /// Overrides <see cref="CADability.GeoObject.IGeoObjectImpl.HitTest (ref BoundingCube, double)"/>
        /// </summary>
        /// <param name="cube"></param>
        /// <param name="precision"></param>
        /// <returns></returns>
        public override bool HitTest(ref BoundingCube cube, double precision)
        {
            return cube.Contains(location);
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
            return projection.ProjectUnscaled(location) <= rect;
        }
        /// <summary>
        /// Overrides <see cref="CADability.GeoObject.IGeoObjectImpl.HitTest (Projection.PickArea, bool)"/>
        /// </summary>
        /// <param name="area"></param>
        /// <param name="onlyInside"></param>
        /// <returns></returns>
        public override bool HitTest(Projection.PickArea area, bool onlyInside)
        {
            return BoundingCube.UnitBoundingCube.Contains(area.ToUnitBox * location);
        }
        /// <summary>
        /// Overrides <see cref="CADability.GeoObject.IGeoObjectImpl.Position (GeoPoint, GeoVector, double)"/>
        /// </summary>
        /// <param name="fromHere"></param>
        /// <param name="direction"></param>
        /// <param name="precision"></param>
        /// <returns></returns>
        public override double Position(GeoPoint fromHere, GeoVector direction, double precision)
        {
            GeoPoint p = Geometry.DropPL(location, fromHere, direction);
            if ((p | location) <= 10 * precision) return Geometry.LinePar(fromHere, direction, p);
            else return double.MaxValue;
        }
        #endregion
        #region IColorDef Members
        public ColorDef ColorDef
        {
            get
            {
                return colorDef;
            }
            set
            {
                SetColorDef(ref colorDef, value);
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
        #region ISerializable Members
        /// <summary>
        /// Constructor required by deserialization
        /// </summary>
        /// <param name="info">SerializationInfo</param>
        /// <param name="context">StreamingContext</param>
        protected Point(SerializationInfo info, StreamingContext context)
            : base(info, context)
        {
            location = (GeoPoint)info.GetValue("Location", typeof(GeoPoint));
            symbol = (PointSymbol)info.GetValue("Symbol", typeof(PointSymbol));
            colorDef = (ColorDef)info.GetValue("ColorDef", typeof(ColorDef));
            size = (double)info.GetValue("Size", typeof(double));
            try //Needed because old files didn't contain the name of the point.
            {
                name = (string)info.GetValue("Name", typeof(string));
            }
            catch (SerializationException)
            {
                name = null;
            }
            if (Constructed != null) Constructed(this);
        }
        /// <summary>
        /// Implements <see cref="ISerializable.GetObjectData"/>
        /// </summary>
        /// <param name="info">The <see cref="System.Runtime.Serialization.SerializationInfo"/> to populate with data.</param>
        /// <param name="context">The destination (<see cref="System.Runtime.Serialization.StreamingContext"/>) for this serialization.</param>
        public override void GetObjectData(SerializationInfo info, StreamingContext context)
        {
            base.GetObjectData(info, context);
            info.AddValue("Location", location);
            info.AddValue("Symbol", symbol);
            info.AddValue("ColorDef", colorDef);
            info.AddValue("Size", size);
            info.AddValue("Name", name);
        }
        public override void GetObjectData(IJsonWriteData data)
        {
            base.GetObjectData(data);
            data.AddProperty("Location", location);
            data.AddProperty("Symbol", symbol);
            if (colorDef != null) data.AddProperty("ColorDef", colorDef);
            data.AddProperty("Size", size);
            data.AddProperty("Name", name);
        }
        public override void SetObjectData(IJsonReadData data)
        {
            base.SetObjectData(data);
            location = data.GetProperty<GeoPoint>("Location");
            symbol = data.GetProperty<PointSymbol>("Symbol");
            colorDef = data.GetPropertyOrDefault<ColorDef>("ColorDef");
            size = data.GetProperty<double>("Size");
            name = data.GetProperty<string>("Name");
        }

        public override BoundingRect GetExtent(Projection projection, ExtentPrecision extentPrecision)
        {
            return new BoundingRect(projection.ProjectUnscaled(location));
        }

        internal static Point MakePoint(GeoPoint position, PointSymbol symbol=PointSymbol.Cross)
        {
            Point res = Point.Construct();
            res.location= position;
            res.symbol= symbol;
            return res;
        }
        #endregion
    }
    internal class PointSymbolAttribute : INamedAttribute
    {   // wird nur verwendet bei mehreren selektierten Punkten
        public PointSymbolAttribute()
        {
        }
        #region INamedAttribute Members

        string INamedAttribute.Name
        {
            get
            {
                throw new NotImplementedException();
            }
            set
            {
                throw new NotImplementedException();
            }
        }

        IAttributeList INamedAttribute.Parent
        {
            get
            {
                throw new NotImplementedException();
            }
            set
            {
                throw new NotImplementedException();
            }
        }

        IPropertyEntry INamedAttribute.GetSelectionProperty(string key, Project project, GeoObjectList geoObjectList)
        {
            GeoObjectList pointList = new GeoObjectList();
            foreach (IGeoObject go in geoObjectList)
            {
                if (go is Point) pointList.Add(go);
            }
            return new PointSymbolSelectionProperty(pointList, "Point.PointSymbol");
        }

        #endregion
    }
    internal class QuadTreePoint : IQuadTreeInsertableZ
    {
        IGeoObject point;
        double z;
        GeoPoint2D loc;
        public QuadTreePoint(Point point, Projection projection)
        {
            this.point = point;
            GeoPoint p = projection.UnscaledProjection * point.Location;
            loc = p.To2D();
            z = p.z;
        }
        #region IQuadTreeInsertableZ Members

        double IQuadTreeInsertableZ.GetZPosition(GeoPoint2D p)
        {
            return z;
        }

        #endregion

        #region IQuadTreeInsertable Members

        BoundingRect IQuadTreeInsertable.GetExtent()
        {
            return new BoundingRect(loc);
        }

        bool IQuadTreeInsertable.HitTest(ref BoundingRect rect, bool includeControlPoints)
        {
            return loc <= rect;
        }

        object IQuadTreeInsertable.ReferencedObject
        {
            get { return point; }
        }

        #endregion
    }
}
