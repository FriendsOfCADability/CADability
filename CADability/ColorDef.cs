using CADability.GeoObject;
using CADability.UserInterface;
using System;
#if WEBASSEMBLY
using CADability.WebDrawing;
#else
using System.Drawing;
#endif
using System.Globalization;
using System.Runtime.Serialization;

namespace CADability.Attribute
{


    public class ChangeEventArgs : EventArgs
    {
        public ChangeEventArgs()
        {
        }
        public ChangeEventArgs(string propertyName, object currentValue)
        {
            PropertyName = propertyName;
            CurrentValue = currentValue;
        }
        public string PropertyName;
        public object CurrentValue;
        //	public bool StopChanging;
        public ChangeEventArgs PreviuosEvent;
    }

    public delegate void AttributeChangeDelegate(object sender, ChangeEventArgs eventArguments);
    /// <summary>
    /// Summary description for ColorDef.
    /// </summary>
    [Serializable()]
    public class ColorDef : ISerializable, INamedAttribute, IJsonSerialize
    {
        public enum ColorSource { fromObject, fromParent, fromName, fromStyle };
        private ColorSource source;
        public event AttributeChangeDelegate ColorWillChangeEvent, ColorDidChangeEvent;
        private string name;
        private ColorList parent;
        private void FireDidChange(string propertyName, object propertyOldValue)
        {
            if (parent != null)
            {
                ReversibleChange change = new ReversibleChange(this, propertyName, propertyOldValue);
                (parent as IAttributeList).AttributeChanged(this, change);
            }
        }
        internal ColorList Parent
        {
            get { return parent; }
            set { parent = value; }
        }
        IAttributeList INamedAttribute.Parent
        {
            get { return parent; }
            set { parent = value as ColorList; }
        }
        IPropertyEntry INamedAttribute.GetSelectionProperty(string key, Project project, GeoObjectList geoObjectList)
        {
            return null;
        }
        private Color color;
        /*		public static ColorDef CDfromLayer = new ColorDef(StringTable.GetString("ColorDef.fromLayer"),SystemColors.WindowText  ,ColorSource.fromLayer);
        */
        public static ColorDef CDfromParent = new ColorDef(StringTable.GetString("ColorDef.fromParent"), Color.FromArgb(0,0,0), ColorSource.fromParent);
        public static ColorDef CDfromStyle = new ColorDef(StringTable.GetString("ColorDef.fromStyle"), Color.FromArgb(0, 0, 0), ColorSource.fromStyle);
        public ColorDef()
        {
            source = ColorSource.fromName;
            color = Color.FromArgb(0, 0, 0);
        }
        public ColorDef(string pName, Color pColor, ColorSource cs)
        {
            name = pName;
            color = pColor;
            source = cs;
            if (name == null)
                switch (source)
                {
                    case ColorSource.fromParent: name = CDfromParent.name; break;
                    case ColorSource.fromStyle: name = CDfromStyle.name; break;
                        //case ColorSource.fromLayer: name = CDfromLayer.name;break;
                }
        }

        public ColorDef(string pName, Color pColor)
        {
            name = pName;
            color = pColor;
            source = ColorSource.fromName;
        }
        static public ColorDef GetDefault()
        {
            ColorList cl = new ColorList();
            (cl as IAttributeList).Initialize();
            return cl[0];
        }
        public static ColorDef Read(SerializationInfo info, StreamingContext context)
        {
            return ColorDef.Read("ColorDef", info, context);
        }
        public static ColorDef Read(string name, SerializationInfo info, StreamingContext context)
        {
            try
            {
                return info.GetValue(name, typeof(ColorDef)) as ColorDef;
            }
            catch (SerializationException)
            {
                return null;
            }
            catch (InvalidCastException)
            {
                return null;
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
                if (parent != null && !(parent as IAttributeList).MayChangeName(this, value))
                {
                    throw new NameAlreadyExistsException(parent, this, value, name);
                }
                string OldName = name;
                name = value;
                FireDidChange("Name", OldName);
            }
        }

        public ColorDef Clone()
        {
            return new ColorDef(name, color, source);
        }

        public Color Color
        {
            get
            {
                //ToDo: throw Exception für fromBlock und fromLayer
                return color;
            }
            set
            {
                if (color.ToArgb() != value.ToArgb())
                {
                    Color oldColor = color;
                    if (ColorWillChangeEvent != null) ColorWillChangeEvent(this, new ChangeEventArgs("ColorDef.Color", color));
                    color = value;
                    if (ColorDidChangeEvent != null) ColorDidChangeEvent(this, new ChangeEventArgs("ColorDef.Color", color));
                    FireDidChange("Color", oldColor);
                }
            }
        }

        public ColorSource Source
        {
            get { return source; }
            set
            {
                if (value <= ColorSource.fromStyle)
                {
                    if (value != source)
                    {
                        if (ColorWillChangeEvent != null) ColorWillChangeEvent(this, new ChangeEventArgs("ColorDef.Source", source));
                        source = value;
                        if (ColorDidChangeEvent != null) ColorWillChangeEvent(this, new ChangeEventArgs("ColorDef.Source", source));
                    }
                }
            }
        }

        public Color Dimm(double percent)
        {
            if (percent > 1) percent = 1;
            int b = (int)(color.B * percent);
            int g = (int)(color.G * percent);
            int r = (int)(color.R * percent);
            return Color.FromArgb(color.A, r, g, b);
        }

#region ISerializable Members
        /// <summary>
        /// Constructor required by deserialization
        /// </summary>
        /// <param name="info">SerializationInfo</param>
        /// <param name="context">StreamingContext</param>
        public ColorDef(SerializationInfo info, StreamingContext context)
        {
            name = (string)info.GetValue("Name", typeof(string));
            source = (ColorSource)info.GetValue("Source", typeof(ColorSource));
            object dbg = info.GetValue("Color", typeof(object));
            try
            {
                color = (Color)info.GetValue("Color", typeof(Color));
            }
            catch (Exception)
            {
                color = Color.Black;
            }

        }
        /// <summary>
        /// Implements <see cref="ISerializable.GetObjectData"/>
        /// </summary>
        /// <param name="info">The <see cref="System.Runtime.Serialization.SerializationInfo"/> to populate with data.</param>
        /// <param name="context">The destination (<see cref="System.Runtime.Serialization.StreamingContext"/>) for this serialization.</param>
        public void GetObjectData(SerializationInfo info, StreamingContext context)
        {
            info.AddValue("Name", name);
            info.AddValue("Source", source);
            info.AddValue("Color", color);
        }
#endregion
#region IJsonSerialize
        // Needs an empty constructor or an constructor with IJsonSerialize as a parameter
        public void GetObjectData(IJsonWriteData data)
        {
            data.AddProperty("Name", name);
            data.AddProperty("Source", source);
            data.AddProperty("Color", color.ToArgb());
        }

        public void SetObjectData(IJsonReadData dict)
        {
            name = dict.GetProperty("Name") as string;
            source = dict.GetProperty<ColorSource>("Source");
            color = Color.FromArgb(dict.GetIntProperty("Color"));
        }
#endregion
        internal void MakeStepStyle(int msb, ExportStep export)
        {
            //#23=COLOUR_RGB('Colour',0.,0.501960784314,1.) ;
            //#24=FILL_AREA_STYLE_COLOUR(' ',#23) ;
            //#25=FILL_AREA_STYLE(' ',(#24)) ;
            //#26=SURFACE_STYLE_FILL_AREA(#25) ;
            //#27=SURFACE_SIDE_STYLE(' ',(#26)) ;
            //#28=SURFACE_STYLE_USAGE(.BOTH.,#27) ;
            //#29=PRESENTATION_STYLE_ASSIGNMENT((#28)) ;
            //#30=STYLED_ITEM(' ',(#29),#22) ;
            //#60 = DRAUGHTING_PRE_DEFINED_COLOUR('red');
            //#61 = CURVE_STYLE('',#62,POSITIVE_LENGTH_MEASURE(0.1),#60);
            //#62 = DRAUGHTING_PRE_DEFINED_CURVE_FONT('continuous');
            //#63 = MECHANICAL_DESIGN_GEOMETRIC_PRESENTATION_REPRESENTATION('',(#64),
            if (!export.StyledItems.TryGetValue(this, out int psa))
            {
                int clr = export.WriteDefinition("COLOUR_RGB('" + Name + "'," + export.ToString(color.R / 255.0) + ","
                    + export.ToString(color.G / 255.0) + "," + export.ToString(color.B / 255.0) + ")");
                int cnt = export.WriteDefinition("DRAUGHTING_PRE_DEFINED_CURVE_FONT('continuous')");
                int cs = export.WriteDefinition("CURVE_STYLE(' ',#" + cnt.ToString() + ",POSITIVE_LENGTH_MEASURE(0.1),#" + clr.ToString() + ")");
                int fac = export.WriteDefinition("FILL_AREA_STYLE_COLOUR(' ',#" + clr.ToString() + ")");
                int fas = export.WriteDefinition("FILL_AREA_STYLE(' ',(#" + fac.ToString() + "))");
                int ssf = export.WriteDefinition("SURFACE_STYLE_FILL_AREA(#" + fas.ToString() + ")");
                int sss = export.WriteDefinition("SURFACE_SIDE_STYLE(' ',(#" + ssf.ToString() + "))");
                int ssu = export.WriteDefinition("SURFACE_STYLE_USAGE(.BOTH.,#" + sss.ToString() + ")");
                psa = export.WriteDefinition("PRESENTATION_STYLE_ASSIGNMENT((#" + ssu.ToString() + "))");
                export.StyledItems[this] = psa;
            }
            export.WriteDefinition("STYLED_ITEM(' ',(#" + psa.ToString() + "),#" + msb.ToString() + ")");
        }
    }
    /// <summary>
    /// Interface to handle the colors of IGeoObject objects
    /// </summary>

    public interface IColorDef
    {
        /// <summary>
        /// Sets and gets the color of the object
        /// </summary>
        ColorDef ColorDef
        {
            get;
            set;
        }
        /// <summary>
        /// Sets the color of only the top level object (in case of the object contains children)
        /// No changing events are issued. Not intended for public use.
        /// </summary>
        /// <param name="newValue"></param>
        void SetTopLevel(ColorDef newValue);
        /// <summary>
        /// Same as <see cref="SetTopLevel"/>, but also overwrites the color of child objects if it is null. Child objects that already have colors remain unchanged.
        /// </summary>
        /// <param name="newValue"></param>
        /// <param name="overwriteChildNullColor"></param>
        void SetTopLevel(ColorDef newValue, bool overwriteChildNullColor);
    }
    /*Standard Implementierung von IColorDef:
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
                using (new ChangingAttribute(this,"ColorDef",colorDef))
                {
                    colorDef = value;
                }
            }
        }
#endregion
    */
}
