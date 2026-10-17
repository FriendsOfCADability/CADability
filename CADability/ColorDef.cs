using CADability.GeoObject;
using CADability.UserInterface;
using System;
using System.Drawing;
using System.Globalization;
using System.Runtime.Serialization;

namespace CADability.Attribute
{


    /// <summary>
    /// Arguments of the <see cref="AttributeChangeDelegate"/> events, with which an attribute (currently only
    /// <see cref="ColorDef"/>) announces the change of one of its properties: <see cref="PropertyName"/> says which
    /// property changes and <see cref="CurrentValue"/> holds its value at the time the event is raised.
    /// </summary>
    public class ChangeEventArgs : EventArgs
    {
        /// <summary>
        /// Creates empty event arguments; <see cref="PropertyName"/> and <see cref="CurrentValue"/> have to be set afterwards.
        /// </summary>
        public ChangeEventArgs()
        {
        }
        /// <summary>
        /// Creates the event arguments for the change of the given property.
        /// </summary>
        /// <param name="propertyName">name of the property that changes, e.g. "ColorDef.Color"</param>
        /// <param name="currentValue">the value of the property when the event is raised</param>
        public ChangeEventArgs(string propertyName, object currentValue)
        {
            PropertyName = propertyName;
            CurrentValue = currentValue;
        }
        /// <summary>
        /// The name of the property that is changing, qualified by the type, e.g. "ColorDef.Color" or "ColorDef.Source".
        /// </summary>
        public string PropertyName;
        /// <summary>
        /// The value of the property at the time the event is raised: the old value in the "will change" event, the new
        /// value in the "did change" event.
        /// </summary>
        public object CurrentValue;
        //	public bool StopChanging;
        /// <summary>
        /// Intended to chain the arguments of a preceding event. It is never set by CADability and is always null.
        /// </summary>
        public ChangeEventArgs PreviuosEvent;
    }

    /// <summary>
    /// Handler for the change events of attributes, see <see cref="ColorDef.ColorWillChangeEvent"/> and
    /// <see cref="ColorDef.ColorDidChangeEvent"/>.
    /// </summary>
    /// <param name="sender">the attribute that is changing (the <see cref="ColorDef"/>)</param>
    /// <param name="eventArguments">which property changes and its value</param>
    public delegate void AttributeChangeDelegate(object sender, ChangeEventArgs eventArguments);
    /// <summary>
    /// Summary description for ColorDef.
    /// </summary>
    [Serializable()]
    public class ColorDef : ISerializable, INamedAttribute, IJsonSerialize
    {
        /// <summary>
        /// Says where the colour of an object with this ColorDef comes from. Ordinary entries of a <see cref="ColorList"/>
        /// are <see cref="fromName"/>; the static <see cref="CDfromParent"/> and <see cref="CDfromStyle"/> are placeholders
        /// whose own <see cref="Color"/> value is not meaningful.
        /// </summary>
        public enum ColorSource
        {
            /// <summary>The colour is defined by the object itself. Not used by CADability.</summary>
            fromObject,
            /// <summary>The object takes the colour of its parent, i.e. of the <see cref="Block"/> it belongs to (see <see cref="CDfromParent"/>).</summary>
            fromParent,
            /// <summary>A named colour from the <see cref="ColorList"/> with its own RGB value. This is the normal case.</summary>
            fromName,
            /// <summary>The colour is determined by the <see cref="Style"/> of the object (see <see cref="CDfromStyle"/>).</summary>
            fromStyle
        };
        private ColorSource source;
        /// <summary>
        /// <see cref="ColorWillChangeEvent"/> is raised before and <see cref="ColorDidChangeEvent"/> after the
        /// <see cref="Color"/> (or the <see cref="Source"/>) of this ColorDef changes; the arguments name the property and
        /// carry its old resp. new value. Objects that derive their colour from this ColorDef (e.g. a <see cref="Block"/>
        /// whose children are coloured "from parent") subscribe to update themselves.
        /// </summary>
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
        /// <summary>
        /// The placeholder colour "of the block" (<see cref="ColorSource.fromParent"/>): an object with this ColorDef is
        /// painted in the colour of the <see cref="Block"/> it belongs to. When such an object is added to a block, the block
        /// replaces it by its own clone, which carries the block's actual colour value. The name comes from the string
        /// resource "ColorDef.fromParent".
        /// </summary>
        public static ColorDef CDfromParent = new ColorDef(StringTable.GetString("ColorDef.fromParent"), Color.FromArgb(0,0,0), ColorSource.fromParent);
        /// <summary>
        /// The placeholder colour "of the style" (<see cref="ColorSource.fromStyle"/>): it stands for a colour that is
        /// determined by the <see cref="Style"/> of the object rather than by this ColorDef. The name comes from the string
        /// resource "ColorDef.fromStyle".
        /// </summary>
        public static ColorDef CDfromStyle = new ColorDef(StringTable.GetString("ColorDef.fromStyle"), Color.FromArgb(0, 0, 0), ColorSource.fromStyle);
        /// <summary>
        /// Creates a ColorDef without a name, with source <see cref="ColorSource.fromName"/> and the colour black. Needed by
        /// the JSON deserialization; otherwise use <see cref="ColorDef(string, Color)"/> or
        /// <see cref="ColorList.CreateOrFind(string, Color)"/>.
        /// </summary>
        public ColorDef()
        {
            source = ColorSource.fromName;
            color = Color.FromArgb(0, 0, 0);
        }
        /// <summary>
        /// Creates a ColorDef with the given name, colour value and <see cref="ColorSource"/>. When <paramref name="pName"/>
        /// is null and the source is <see cref="ColorSource.fromParent"/> or <see cref="ColorSource.fromStyle"/>, the name
        /// of the corresponding placeholder (<see cref="CDfromParent"/>, <see cref="CDfromStyle"/>) is used.
        /// </summary>
        /// <param name="pName">the name, unique within the <see cref="ColorList"/> it is added to</param>
        /// <param name="pColor">the RGB value</param>
        /// <param name="cs">where the colour comes from</param>
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

        /// <summary>
        /// Creates an ordinary named colour (<see cref="ColorSource.fromName"/>). The ColorDef does not belong to a list yet;
        /// add it with <see cref="ColorList.Add(ColorDef)"/>, or use <see cref="ColorList.CreateOrFind(string, Color)"/> instead.
        /// </summary>
        /// <param name="pName">the name, unique within the <see cref="ColorList"/> it is added to</param>
        /// <param name="pColor">the RGB value</param>
        public ColorDef(string pName, Color pColor)
        {
            name = pName;
            color = pColor;
            source = ColorSource.fromName;
        }
        /// <summary>
        /// Returns the first colour of a freshly created default <see cref="ColorList"/> (red, see the string resource
        /// "ColorList.Default"). The returned ColorDef belongs to that temporary list and not to a project; it serves as a
        /// fallback where a colour is needed before a project is available, e.g. for a new <see cref="DimensionStyle"/>.
        /// </summary>
        /// <returns>the default colour</returns>
        static public ColorDef GetDefault()
        {
            ColorList cl = new ColorList();
            (cl as IAttributeList).Initialize();
            return cl[0];
        }
        /// <summary>
        /// Reads the ColorDef stored under the name "ColorDef" from the legacy binary serialization data; same as
        /// <see cref="Read(string, SerializationInfo, StreamingContext)"/> with that name.
        /// </summary>
        /// <param name="info">the serialization data</param>
        /// <param name="context">the source of the serialization</param>
        /// <returns>the ColorDef, or null when the data contains no such entry</returns>
        public static ColorDef Read(SerializationInfo info, StreamingContext context)
        {
            return ColorDef.Read("ColorDef", info, context);
        }
        /// <summary>
        /// Reads the ColorDef stored under the given name from the legacy binary serialization data
        /// (<see cref="System.Runtime.Serialization.ISerializable"/>). For use in the deserialization constructors of objects
        /// that have a colour; missing or incompatible entries yield null instead of an exception, so that older files can
        /// be read.
        /// </summary>
        /// <param name="name">the name the ColorDef was stored under</param>
        /// <param name="info">the serialization data</param>
        /// <param name="context">the source of the serialization</param>
        /// <returns>the ColorDef, or null when there is no entry with that name or it is not a ColorDef</returns>
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
        /// <summary>
        /// The name of the colour, unique within its <see cref="ColorList"/> and shown in the colour selection of the control
        /// center. Setting a name that is already used by another entry of the list throws a
        /// <see cref="NameAlreadyExistsException"/>; otherwise the change is forwarded to the owner of the list (the project),
        /// which records it for undo.
        /// </summary>
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

        /// <summary>
        /// Creates a copy with the same name, colour value and <see cref="Source"/>. The copy does not belong to a
        /// <see cref="ColorList"/> and has no event subscribers.
        /// </summary>
        /// <returns>the copy</returns>
        public ColorDef Clone()
        {
            return new ColorDef(name, color, source);
        }

        /// <summary>
        /// The RGB (or ARGB) value of the colour. For the placeholders <see cref="CDfromParent"/> and <see cref="CDfromStyle"/>
        /// the value is not meaningful. Setting a different value raises <see cref="ColorWillChangeEvent"/> before and
        /// <see cref="ColorDidChangeEvent"/> after the change, and forwards the change to the owner of the list (undoable);
        /// all objects referring to this ColorDef are displayed in the new colour.
        /// </summary>
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

        /// <summary>
        /// Says where the colour of an object with this ColorDef comes from (see <see cref="ColorSource"/>); normally
        /// <see cref="ColorSource.fromName"/>. Setting a different value raises the colour change events; unlike
        /// <see cref="Color"/> the change is not forwarded to the list and is not undoable.
        /// </summary>
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
                        if (ColorDidChangeEvent != null) ColorDidChangeEvent(this, new ChangeEventArgs("ColorDef.Source", source));
                    }
                }
            }
        }

        /// <summary>
        /// Returns a darkened version of the colour value: the red, green and blue components are multiplied by
        /// <paramref name="percent"/>, the alpha component is kept. This ColorDef itself is not changed.
        /// </summary>
        /// <param name="percent">factor between 0.0 (black) and 1.0 (the unchanged colour); greater values are treated as 1.0</param>
        /// <returns>the darkened colour</returns>
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
        /// Writes the data of this object for the legacy binary serialization (<see cref="System.Runtime.Serialization.ISerializable"/>).
        /// Files are written in the JSON format (<see cref="IJsonSerialize"/>) now; the binary format is still read.
        /// </summary>
        /// <param name="info">receives the data</param>
        /// <param name="context">the destination of the serialization</param>
        public void GetObjectData(SerializationInfo info, StreamingContext context)
        {
            info.AddValue("Name", name);
            info.AddValue("Source", source);
            info.AddValue("Color", color);
        }
#endregion
#region IJsonSerialize
        // Needs an empty constructor or an constructor with IJsonSerialize as a parameter
        /// <summary>
        /// Writes the name, source and colour value of this ColorDef for the JSON serialization (<see cref="IJsonSerialize"/>).
        /// </summary>
        /// <param name="data">receives the data</param>
        public void GetObjectData(IJsonWriteData data)
        {
            data.AddProperty("Name", name);
            data.AddProperty("Source", source);
            data.AddProperty("Color", color.ToArgb());
        }

        /// <summary>
        /// Restores this ColorDef from the JSON data written by <see cref="GetObjectData(IJsonWriteData)"/> (<see cref="IJsonSerialize"/>).
        /// </summary>
        /// <param name="dict">the data to read from</param>
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
        /// Same as <see cref="SetTopLevel(ColorDef)"/>, but also overwrites the color of child objects if it is null. Child objects that already have colors remain unchanged.
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
