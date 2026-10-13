using CADability.GeoObject;
using CADability.UserInterface;
using System;
using System.Runtime.Serialization;

namespace CADability.Attribute
{
    /// <summary>
    /// Describes a line width. The "onedimensional" GeoObjects (e.g. <see cref="Line"/>, <see cref="Ellipse"/>,
    /// <see cref="BSpline"/>) have a line with as an attribute. The main propertie of the line width
    /// are the width and the scaling system (<see cref="Scaling"/>), which defines how to interpret the width.
    /// 
    /// </summary>
    [Serializable()]
    public class LineWidth : PropertyEntryImpl, INamedAttribute, ISerializable, ICommandHandler, IJsonSerialize
    {
        private string name;
        private double width;
        /// <summary>
        /// Scaling system of the line width. 
        /// <list type="bullet">
        /// <item><term>Device</term>
        /// <description>Width specifies the number of pixel, zoom independant</description>
        /// </item>
        /// <item><term>World</term>
        /// <description>Width specifies the width of the line in the world coordinate system</description>
        /// </item>
        /// <item><term>Layout</term>
        /// <description>Width specifies the width of the line in the layout system (mm on the paper when printed)</description>
        /// </item>
        /// </list>
        /// </summary>
        public enum Scaling
        {
            /// <summary>The width is a number of pixels on the screen, independent of the zoom factor.</summary>
            Device,
            /// <summary>The width is a length in the world coordinate system of the model and scales with the zoom factor.</summary>
            World,
            /// <summary>The width is a length in the layout, i.e. in mm on the paper when printed; the default of the parameterless constructor.</summary>
            Layout
        }
        private Scaling scale;
        private LineWidthList parent;
        /// <summary>
        /// Creates an empty and unnamed LineWidth
        /// </summary>
        public LineWidth()
        {
            scale = Scaling.Layout;
            name = "";
            width = 0.0;
        }
        /// <summary>
        /// Creates a line width with the given name and width. The <see cref="Scale"/> is <see cref="Scaling.Device"/> (the
        /// default value of the enumeration); set it explicitly when the width is meant in world or layout units. The object
        /// does not belong to a list yet, see <see cref="LineWidthList.Add(LineWidth)"/>.
        /// </summary>
        /// <param name="name">the name, unique within the <see cref="LineWidthList"/> it is added to</param>
        /// <param name="width">the width, interpreted according to <see cref="Scale"/></param>
        public LineWidth(string name, double width)
        {
            this.name = name;
            this.width = width;
        }
        /// <summary>
        /// Returns a clone of this LineWidth
        /// </summary>
        /// <returns></returns>
        public LineWidth Clone()
        {
            LineWidth res = new LineWidth();
            res.name = name;
            res.width = width;
            res.scale = scale;
            return res;
        }
        /// <summary>
        /// Gets or sets the line width of  this object.
        /// </summary>
        public double Width
        {
            get { return width; }
            set
            {
                if (width != value)
                {
                    double oldWidth = width;
                    width = value;
                    FireDidChange("Width", oldWidth);
                }
            }
        }
        /// <summary>
        /// Gets or sets the <see cref="Scaling"/> of this LineWidth.
        /// </summary>
        public Scaling Scale
        {
            get { return scale; }
            set
            {
                if (scale != value)
                {
                    Scaling oldScale = scale;
                    scale = value;
                    FireDidChange("Scale", oldScale);
                }
            }
        }
        /// <summary>
        /// A line width of 0, which is the thinnest line the output device can draw (one pixel on the screen, 0.1 mm when
        /// printed). It is a shared instance without a name that does not belong to any list; CADability itself no longer
        /// uses it.
        /// </summary>
        static public LineWidth ThinLine = new LineWidth();
        private void FireDidChange(string propertyName, object propertyOldValue)
        {   // Eine Eigenschaft hat sich geändert.
            // Wird an parent weitergeleitet
            if (parent != null)
            {
                ReversibleChange change = new ReversibleChange(this, propertyName, propertyOldValue);
                (parent as IAttributeList).AttributeChanged(this, change);
            }
        }

        #region PropertyEntryImpl
        private IPropertyEntry[] subEntries;
        /// <inheritdoc/>
        public override PropertyEntryType Flags
        {
            get
            {
                PropertyEntryType flags = PropertyEntryType.GroupTitle | PropertyEntryType.HasSubEntries | PropertyEntryType.ContextMenu | PropertyEntryType.Selectable | PropertyEntryType.LabelEditable;
                if (parent.Current == this)
                    flags |= PropertyEntryType.Bold;
                return flags;
            }
        }
        /// <inheritdoc/>
        public override void Added(IPropertyPage pp)
        {
            base.Added(pp);
            subEntries = new IPropertyEntry[1];
            string[] choices = StringTable.GetSplittedStrings("LineWidth.Scaling");
            string choice = "";
            if ((int)scale < choices.Length)
            {
                choice = choices[(int)scale];
            }
            MultipleChoiceProperty mcp = new MultipleChoiceProperty("LineWidth.Scale", choices, choice);
            mcp.ValueChangedEvent += new ValueChangedDelegate(ScalingChanged);
            subEntries[0] = mcp;
            base.resourceIdInternal = "LineWidthName";
        }
        /// <inheritdoc/>
        public override string LabelText
        {
            get
            {
                return name;
            }
            set
            {
                Name = value;
            }
        }
        /// <inheritdoc/>
        public override void EndEdit(bool aborted, bool modified, string newValue)
        {
            try
            {
                Name = newValue;
            }
            catch (NameAlreadyExistsException)
            {
            }
        }
        /// <inheritdoc/>
        public override void Removed(IPropertyPage pp)
        {
            base.Removed(pp);
            if (subEntries != null && subEntries[0] is MultipleChoiceProperty mcp)
            {
                mcp.ValueChangedEvent -= new ValueChangedDelegate(ScalingChanged);
            }
        }
        /// <inheritdoc/>
        public override IPropertyEntry[] SubItems
        {
            get
            {
                return subEntries;
            }
        }
        /// <inheritdoc/>
        public override MenuWithHandler[] ContextMenu
        {
            get
            {
                return MenuResource.LoadMenuDefinition("MenuId.LineWidthEntry", false, this);
            }
        }
        private void ScalingChanged(object sender, object NewValue)
        {
            MultipleChoiceProperty mcp = sender as MultipleChoiceProperty;
            int ind = mcp.ChoiceIndex(NewValue as string);
            if (ind >= 0) Scale = (Scaling)ind;
        }
#endregion
#region INamedAttribute Members
        /// <summary>
        /// The name of the line width, unique within its <see cref="LineWidthList"/> and shown in the control center.
        /// Setting a name that is already used by another entry of the list throws a <see cref="NameAlreadyExistsException"/>;
        /// otherwise the change is forwarded to the owner of the list (the project), which records it for undo.
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
        /// The <see cref="LineWidthList"/> this line width belongs to, or null (<see cref="INamedAttribute.Parent"/>). It is
        /// set by the list when the line width is added or removed; the line width forwards its changes to it. Hides
        /// <see cref="PropertyEntryImpl.Parent"/>, the parent of the entry in the control center.
        /// </summary>
        public new IAttributeList Parent
        {
            get
            {
                return parent;
            }
            set
            {
                parent = (LineWidthList)value; // muss so sein
            }
        }
        IPropertyEntry INamedAttribute.GetSelectionProperty(string key, Project project, GeoObjectList geoObjectList)
        {
            return null;
        }
#endregion
#region ISerializable Members
        /// <summary>
        /// Reads the LineWidth stored under the given name from the legacy binary serialization data
        /// (<see cref="System.Runtime.Serialization.ISerializable"/>). For use in the deserialization constructors of objects
        /// that have a line width; missing or incompatible entries yield null instead of an exception, so that older files
        /// can be read.
        /// </summary>
        /// <param name="name">the name the LineWidth was stored under</param>
        /// <param name="info">the serialization data</param>
        /// <param name="context">the source of the serialization</param>
        /// <returns>the LineWidth, or null when there is no entry with that name or it is not a LineWidth</returns>
        public static LineWidth Read(string name, SerializationInfo info, StreamingContext context)
        {
            // Versuch einer sicheren allgemeinen Implementierung von Read aus einer SerializationInfo
            // Dies könnte vielleicht per Template für alle serialisierbaren Objekte (nicht value types,
            // dort aber ähnlich) implementiert werden
            // oder in eine globalen Klasse Read, also Read.LineWidth(name) (gute Idee, oder?)
            try
            {
                return info.GetValue(name, typeof(LineWidth)) as LineWidth;
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
        /// Constructor required by deserialization
        /// </summary>
        /// <param name="info">SerializationInfo</param>
        /// <param name="context">StreamingContext</param>
        protected LineWidth(SerializationInfo info, StreamingContext context)
        {
            name = (string)info.GetValue("Name", typeof(string));
            width = (double)info.GetValue("Width", typeof(double));
            scale = (Scaling)info.GetValue("Scale", typeof(Scaling));
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
            info.AddValue("Width", width);
            info.AddValue("Scale", scale);
            // parent wird vom Besitzer verwaltet
        }
#endregion
#region ICommandHandler Members
        bool ICommandHandler.OnCommand(string MenuId)
        {
            switch (MenuId)
            {
                case "MenuId.LineWidthEntry.Delete":
                    parent.Remove(this);
                    return true;
                case "MenuId.LineWidthEntry.Edit":
                    propertyPage.StartEditLabel(this);
                    return true;
                case "MenuId.LineWidthEntry.Current":
                    parent.Current = this;
                    return true;
            }
            return false;
        }
        bool ICommandHandler.OnUpdateCommand(string MenuId, CommandState CommandState)
        {
            // TODO:  Add LineWidth.OnUpdateCommand implementation
            return false;
        }
        void ICommandHandler.OnSelected(MenuWithHandler selectedMenuItem, bool selected) { }
        #endregion
        #region IJsonSerialize
        /// <summary>
        /// Writes the name, width and scaling of this line width for the JSON serialization (<see cref="IJsonSerialize"/>).
        /// </summary>
        /// <param name="data">receives the data</param>
        public void GetObjectData(IJsonWriteData data)
        {
            data.AddProperty("Name", name);
            data.AddProperty("Width", width);
            data.AddProperty("Scale", scale);
        }

        /// <summary>
        /// Restores this line width from the JSON data written by <see cref="GetObjectData(IJsonWriteData)"/> (<see cref="IJsonSerialize"/>).
        /// </summary>
        /// <param name="data">the data to read from</param>
        public void SetObjectData(IJsonReadData data)
        {
            name = data.GetStringProperty("Name");
            width = data.GetProperty<double>("Width");
            scale = data.GetProperty<Scaling>("Scale");
        }
#endregion
    }


    /// <summary>
    /// Implemented by GeoObjects that have a <see cref="LineWidth"/> attribute, e.g. <see cref="Line"/>, <see cref="Ellipse"/>,
    /// <see cref="BSpline"/>, <see cref="Polyline"/> and <see cref="Path"/>, and by <see cref="Style"/>. The
    /// <see cref="LineWidthList"/> of the project uses it to find and update the line widths in use.
    /// </summary>
    public interface ILineWidth
    {
        /// <summary>
        /// The line width of the object, or null when none is set. For GeoObjects setting it raises the change events and is
        /// undoable.
        /// </summary>
        LineWidth LineWidth { get; set; }
    }
    /* Default Implementierung
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
			using (new ChangingAttribute(this,"LineWidth",lineWidth))
			{
				lineWidth = value;
			}
		}
	}
#endregion
	*/

}
