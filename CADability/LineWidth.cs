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
        public enum Scaling { Device, World, Layout }
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
        public override void Removed(IPropertyPage pp)
        {
            base.Removed(pp);
            if (subEntries != null && subEntries[0] is MultipleChoiceProperty mcp)
            {
                mcp.ValueChangedEvent -= new ValueChangedDelegate(ScalingChanged);
            }
        }
        public override IPropertyEntry[] SubItems
        {
            get
            {
                return subEntries;
            }
        }
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
        /// Implements <see cref="ISerializable.GetObjectData"/>
        /// </summary>
        /// <param name="info">The <see cref="System.Runtime.Serialization.SerializationInfo"/> to populate with data.</param>
        /// <param name="context">The destination (<see cref="System.Runtime.Serialization.StreamingContext"/>) for this serialization.</param>
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
        public void GetObjectData(IJsonWriteData data)
        {
            data.AddProperty("Name", name);
            data.AddProperty("Width", width);
            data.AddProperty("Scale", scale);
        }

        public void SetObjectData(IJsonReadData data)
        {
            name = data.GetStringProperty("Name");
            width = data.GetProperty<double>("Width");
            scale = data.GetProperty<Scaling>("Scale");
        }
#endregion
    }


    public interface ILineWidth
    {
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
