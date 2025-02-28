using CADability.GeoObject;
using CADability.UserInterface;
using System;
using System.Collections;
using System.Runtime.Serialization;

namespace CADability.Attribute
{
    /// <summary>
    /// 
    /// </summary>
    [Serializable()]
    public class LinePattern : PropertyEntryImpl, INamedAttribute, ISerializable, ICommandHandler, IJsonSerialize
    {
        private string name;
        private double[] pattern;
        /// <summary>
        /// Scaling system of the line pattern. 
        /// <list type="bullet">
        /// <item><term>Device</term>
        /// <description>Pattern specifies the number of pixel, zoom independant</description>
        /// </item>
        /// <item><term>World</term>
        /// <description>Pattern specifies the pattern of the line in the world coordinate system</description>
        /// </item>
        /// <item><term>Layout</term>
        /// <description>Pattern specifies the pattern of the line in the layout system (mm on the paper when printed)</description>
        /// </item>
        /// </list>
        /// </summary>
        public enum Scaling { Device, World, Layout }
        private Scaling scale;
        private LinePatternList parent;
        /// <summary>
        /// Creates an empty and unnamed LinePattern
        /// </summary>
        public LinePattern()
        {
            scale = Scaling.Layout;
            pattern = new double[0];
            name = "";
        }
        public LinePattern(string name, params double[] pattern)
        {
            scale = Scaling.Layout;
            this.pattern = pattern;
            this.name = name;
        }
        /// <summary>
		/// Returns a clone of this LinePattern
		/// </summary>
		/// <returns></returns>
		public LinePattern Clone()
        {
            LinePattern res = new LinePattern();
            res.name = name;
            res.pattern = (double[])pattern.Clone();
            res.scale = scale;
            return res;
        }
        public double[] Pattern
        {
            get { return pattern; }
            set
            {
                double[] oldPattern = (double[])pattern.Clone();
                if (value == null) pattern = new double[0];
                else if ((value.Length % 2) != 0)
                {
                    throw new ArgumentException("the number of elements in the pattern must be even", "value");
                }
                else
                {
                    pattern = (double[])value.Clone();
                }
                FireDidChange("Pattern", oldPattern);
            }
        }
        public int PatternCount
        {
            get { return pattern.Length / 2; } // muss immer gerade sein
        }
        public bool Solid
        {
            get { return pattern.Length == 0; }
            set
            {
                double[] oldPattern = (double[])pattern.Clone();
                pattern = new double[0];
                FireDidChange("Pattern", oldPattern);
            }
        }
        public void SetPattern(int index, double stroke, double gap)
        {
            double[] oldPattern = (double[])pattern.Clone();
            pattern[index * 2] = stroke;
            pattern[index * 2 + 1] = gap;
            FireDidChange("Pattern", oldPattern);
        }
        public double GetStroke(int index)
        {
            return pattern[index * 2];
        }
        public double GetGap(int index)
        {
            return pattern[index * 2 + 1];
        }
        /// <summary>
        /// Gets or sets the <see cref="Scaling"/> of this LinePattern.
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
                PropertyEntryType flags = PropertyEntryType.ContextMenu | PropertyEntryType.Selectable | PropertyEntryType.LabelEditable | PropertyEntryType.GroupTitle | PropertyEntryType.HasSubEntries;
                if (parent != null && parent.Current == this)
                    flags |= PropertyEntryType.Bold;
                return flags;
            }
        }
        public override void Added(IPropertyPage pp)
        {
            base.Added(pp);
            base.resourceIdInternal = "LinePatternName";
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
        public override bool EditTextChanged(string newValue)
        {
            return true;
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
                if (subEntries == null)
                {
                    subEntries = new IPropertyEntry[pattern.Length + 1];
                    string[] choices = StringTable.GetSplittedStrings("LinePattern.Scaling");
                    string choice = "";
                    if ((int)scale < choices.Length)
                    {
                        choice = choices[(int)scale];
                    }
                    MultipleChoiceProperty mcp = new MultipleChoiceProperty("LinePattern.Scale", choices, choice);
                    mcp.ValueChangedEvent += new ValueChangedDelegate(ScalingChanged);
                    subEntries[0] = mcp;
                    for (int i = 0; i < pattern.Length; ++i)
                    {
                        string rid;
                        if (i % 2 == 0) rid = "LinePattern.Stroke";
                        else rid = "LinePattern.Gap";
                        DoubleProperty dp = new DoubleProperty(propertyPage.ActiveView.Canvas.Frame, rid);
                        dp.UserData.Add("Index", i);
                        dp.GetDoubleEvent += new CADability.UserInterface.DoubleProperty.GetDoubleDelegate(OnGetPattern);
                        dp.SetDoubleEvent += new CADability.UserInterface.DoubleProperty.SetDoubleDelegate(OnSetPattern);
                        dp.DoubleChanged();
                        subEntries[i + 1] = dp;
                    }
                }
                return subEntries;
            }
        }
        public override MenuWithHandler[] ContextMenu
        {
            get
            {
                return MenuResource.LoadMenuDefinition("MenuId.LinePatternEntry", false, this);
            }
        }
        private void ScalingChanged(object sender, object NewValue)
        {
            MultipleChoiceProperty mcp = sender as MultipleChoiceProperty;
            int ind = mcp.ChoiceIndex(NewValue as string);
            if (ind >= 0) Scale = (Scaling)ind;
        }
        private double OnGetPattern(DoubleProperty sender)
        {
            int ind = (int)sender.UserData.GetData("Index");
            return pattern[ind];
        }
        private void OnSetPattern(DoubleProperty sender, double l)
        {
            double[] oldPattern = (double[])pattern.Clone();
            int ind = (int)sender.UserData.GetData("Index");
            pattern[ind] = l;
            FireDidChange("Pattern", oldPattern);
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
        public IAttributeList Parent
        {
            get
            {
                return parent;
            }
            set
            {
                parent = (LinePatternList)value; // muss so sein
            }
        }
        IPropertyEntry INamedAttribute.GetSelectionProperty(string key, Project project, GeoObjectList geoObjectList)
        {
            return null;
        }
#endregion
#region ISerializable Members
        public static LinePattern Read(string name, SerializationInfo info, StreamingContext context)
        {
            try
            {
                return info.GetValue(name, typeof(LinePattern)) as LinePattern;
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
        protected LinePattern(SerializationInfo info, StreamingContext context)
        {
            name = (string)info.GetValue("Name", typeof(string));
            pattern = (double[])info.GetValue("Pattern", typeof(double[]));
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
            info.AddValue("Pattern", pattern);
            info.AddValue("Scale", scale);
            // parent wird vom Besitzer verwaltet
        }
        #endregion
        #region IJsonSerialize
        public void GetObjectData(IJsonWriteData data)
        {
            data.AddProperty("Name", name);
            data.AddProperty("Pattern", pattern);
            data.AddProperty("Scale", scale);
        }

        public void SetObjectData(IJsonReadData data)
        {
            name = data.GetStringProperty("Name");
            pattern = data.GetProperty<double[]>("Pattern");
            scale = data.GetProperty<Scaling>("Scale");
        }
        #endregion
        #region ICommandHandler Members
        bool ICommandHandler.OnCommand(string MenuId)
        {
            switch (MenuId)
            {
                case "MenuId.LinePatternEntry.Delete":
                    parent.Remove(this);
                    return true;
                case "MenuId.LinePatternEntry.Edit":
                    propertyPage.StartEditLabel(this); // is opened
                    return true;
                case "MenuId.LinePatternEntry.Current":
                    parent.Current = this;
                    return true;
            }
            return false;
        }
        bool ICommandHandler.OnUpdateCommand(string MenuId, CommandState CommandState)
        {
            // TODO:  Add LinePattern.OnUpdateCommand implementation
            return false;
        }
        void ICommandHandler.OnSelected(MenuWithHandler selectedMenuItem, bool selected) { }
        #endregion

    }


    public interface ILinePattern
    {
        LinePattern LinePattern { get; set; }
    }
    /* Default Implementierung
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
			using (new ChangingAttribute(this,"LinePattern",linePattern))
			{
				linePattern = value;
			}
		}
	}
#endregion
	*/

}
