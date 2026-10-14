using CADability.GeoObject;
using CADability.UserInterface;
using System;
using System.Runtime.Serialization;

namespace CADability.Attribute
{
    /// <summary>
    /// A named attribute which defines a dash pattern for the display of curves: an array of alternating stroke and gap lengths
    /// (<see cref="Pattern"/>) and a <see cref="Scaling"/> which says whether these lengths are pixels, world units or layout
    /// units (mm on paper). An empty pattern means a solid line. Line patterns are kept in the <see cref="LinePatternList"/> of
    /// the project, are referenced by objects implementing <see cref="ILinePattern"/> and can be edited in the property grid.
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
        /// <description>Pattern specifies the number of pixel, zoom independent</description>
        /// </item>
        /// <item><term>World</term>
        /// <description>Pattern specifies the pattern of the line in the world coordinate system</description>
        /// </item>
        /// <item><term>Layout</term>
        /// <description>Pattern specifies the pattern of the line in the layout system (mm on the paper when printed)</description>
        /// </item>
        /// </list>
        /// </summary>
        public enum Scaling
        {
            /// <summary>The stroke and gap lengths are numbers of pixels on the screen, independent of the zoom factor.</summary>
            Device,
            /// <summary>The stroke and gap lengths are lengths in the world coordinate system of the model and scale with the zoom factor.</summary>
            World,
            /// <summary>The stroke and gap lengths are lengths in the layout, i.e. in mm on the paper when printed. This is the default.</summary>
            Layout
        }
        private Scaling scale;
        private LinePatternList parent;
        /// <summary>
        /// Creates an empty and unnamed LinePattern
        /// </summary>
        public LinePattern()
        {
            scale = Scaling.Layout;
            pattern = Array.Empty<double>();
            name = "";
        }
        /// <summary>
        /// Creates a line pattern with the given name and stroke/gap lengths, with <see cref="Scaling.Layout"/> scaling. The
        /// array is taken over as it is; it should have an even number of elements (see <see cref="Pattern"/>), an empty
        /// array gives a solid line. The object does not belong to a list yet, see <see cref="LinePatternList.Add(LinePattern)"/>.
        /// </summary>
        /// <param name="name">the name, unique within the <see cref="LinePatternList"/> it is added to</param>
        /// <param name="pattern">alternating stroke and gap lengths, e.g. <c>2.0, 2.0</c> for a dashed line</param>
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
        /// <summary>
        /// The dash pattern as an array of alternating stroke and gap lengths in the units given by <see cref="Scale"/>:
        /// element 0 is the length of the first stroke, element 1 the length of the following gap, and so on; the pattern
        /// repeats along the curve. An empty array means a solid line. The getter returns the internal array, which must not
        /// be modified; the setter stores a copy (null is treated as empty), throws an <see cref="ArgumentException"/> for an
        /// odd number of elements, and forwards the change to the owner of the list (undoable).
        /// </summary>
        public double[] Pattern
        {
            get { return pattern; }
            set
            {
                double[] oldPattern = (double[])pattern.Clone();
                if (value == null) pattern = Array.Empty<double>();
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
        /// <summary>
        /// The number of stroke/gap pairs in <see cref="Pattern"/>; 0 for a solid line.
        /// </summary>
        public int PatternCount
        {
            get { return pattern.Length / 2; } // muss immer gerade sein
        }
        /// <summary>
        /// True when the pattern is empty, i.e. the line is drawn continuously. Setting the property, whatever the value,
        /// removes the pattern and makes the line solid (undoable).
        /// </summary>
        public bool Solid
        {
            get { return pattern.Length == 0; }
            set
            {
                double[] oldPattern = (double[])pattern.Clone();
                pattern = Array.Empty<double>();
                FireDidChange("Pattern", oldPattern);
            }
        }
        /// <summary>
        /// Sets the stroke and gap lengths of the pair at the given index. The change is forwarded to the owner of the list
        /// (undoable).
        /// </summary>
        /// <param name="index">index of the pair, 0 to <see cref="PatternCount"/>-1</param>
        /// <param name="stroke">the length of the stroke in the units of <see cref="Scale"/></param>
        /// <param name="gap">the length of the gap that follows the stroke</param>
        /// <exception cref="IndexOutOfRangeException">when the index is out of range</exception>
        public void SetPattern(int index, double stroke, double gap)
        {
            double[] oldPattern = (double[])pattern.Clone();
            pattern[index * 2] = stroke;
            pattern[index * 2 + 1] = gap;
            FireDidChange("Pattern", oldPattern);
        }
        /// <summary>
        /// Returns the length of the stroke of the pair at the given index.
        /// </summary>
        /// <param name="index">index of the pair, 0 to <see cref="PatternCount"/>-1</param>
        /// <returns>the stroke length in the units of <see cref="Scale"/></returns>
        public double GetStroke(int index)
        {
            return pattern[index * 2];
        }
        /// <summary>
        /// Returns the length of the gap of the pair at the given index, i.e. the gap that follows the stroke of that pair.
        /// </summary>
        /// <param name="index">index of the pair, 0 to <see cref="PatternCount"/>-1</param>
        /// <returns>the gap length in the units of <see cref="Scale"/></returns>
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
        /// <inheritdoc/>
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
        /// <inheritdoc/>
        public override void Added(IPropertyPage pp)
        {
            base.Added(pp);
            base.resourceIdInternal = "LinePatternName";
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
        public override bool EditTextChanged(string newValue)
        {
            return true;
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
                mcp.ValueChangedEvent -= ScalingChanged;
            }
        }
        /// <inheritdoc/>
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
                    mcp.ValueChangedEvent += ScalingChanged;
                    subEntries[0] = mcp;
                    for (int i = 0; i < pattern.Length; ++i)
                    {
	                    int currentIndex = i;
	                    string rid = i % 2 == 0 ? "LinePattern.Stroke" : "LinePattern.Gap";
                        DoubleProperty dp = new DoubleProperty(propertyPage.ActiveView.Canvas.Frame, rid);
						dp.OnGetValue = () => pattern[currentIndex];
                        dp.OnSetValue = (d) => OnSetPattern(currentIndex, d);
                        dp.DoubleChanged();
                        subEntries[i + 1] = dp;
                    }
                }
                return subEntries;
            }
        }
        /// <inheritdoc/>
        public override MenuWithHandler[] ContextMenu => MenuResource.LoadMenuDefinition("MenuId.LinePatternEntry", false, this);

        private void ScalingChanged(object sender, object newValue)
        {
            MultipleChoiceProperty mcp = sender as MultipleChoiceProperty;
            int ind = mcp.ChoiceIndex(newValue as string);
            if (ind >= 0) Scale = (Scaling)ind;
        }

        private void OnSetPattern(int index, double l)
        {
            double[] oldPattern = (double[])pattern.Clone();
            pattern[index] = l;
            FireDidChange("Pattern", oldPattern);
        }
#endregion
#region INamedAttribute Members
        /// <summary>
        /// The name of the line pattern, unique within its <see cref="LinePatternList"/> and shown in the control center.
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
        /// The <see cref="LinePatternList"/> this line pattern belongs to, or null (<see cref="INamedAttribute.Parent"/>). It
        /// is set by the list when the line pattern is added or removed; the line pattern forwards its changes to it. Hides
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
                parent = (LinePatternList)value; // muss so sein
            }
        }
        IPropertyEntry INamedAttribute.GetSelectionProperty(string key, Project project, GeoObjectList geoObjectList)
        {
            return null;
        }
#endregion
#region ISerializable Members
        /// <summary>
        /// Reads the LinePattern stored under the given name from the legacy binary serialization data
        /// (<see cref="System.Runtime.Serialization.ISerializable"/>). For use in the deserialization constructors of objects
        /// that have a line pattern; missing or incompatible entries yield null instead of an exception, so that older files
        /// can be read.
        /// </summary>
        /// <param name="name">the name the LinePattern was stored under</param>
        /// <param name="info">the serialization data</param>
        /// <param name="context">the source of the serialization</param>
        /// <returns>the LinePattern, or null when there is no entry with that name or it is not a LinePattern</returns>
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
        /// Writes the data of this object for the legacy binary serialization (<see cref="System.Runtime.Serialization.ISerializable"/>).
        /// Files are written in the JSON format (<see cref="IJsonSerialize"/>) now; the binary format is still read.
        /// </summary>
        /// <param name="info">receives the data</param>
        /// <param name="context">the destination of the serialization</param>
        public void GetObjectData(SerializationInfo info, StreamingContext context)
        {
            info.AddValue("Name", name);
            info.AddValue("Pattern", pattern);
            info.AddValue("Scale", scale);
            // parent wird vom Besitzer verwaltet
        }
        #endregion
        #region IJsonSerialize
        /// <summary>
        /// Writes the name, pattern and scaling of this line pattern for the JSON serialization (<see cref="IJsonSerialize"/>).
        /// </summary>
        /// <param name="data">receives the data</param>
        public void GetObjectData(IJsonWriteData data)
        {
            data.AddProperty("Name", name);
            data.AddProperty("Pattern", pattern);
            data.AddProperty("Scale", scale);
        }

        /// <summary>
        /// Restores this line pattern from the JSON data written by <see cref="GetObjectData(IJsonWriteData)"/> (<see cref="IJsonSerialize"/>).
        /// </summary>
        /// <param name="data">the data to read from</param>
        public void SetObjectData(IJsonReadData data)
        {
            name = data.GetStringProperty("Name");
            pattern = data.GetProperty<double[]>("Pattern");
            scale = data.GetProperty<Scaling>("Scale");
        }
        #endregion
        #region ICommandHandler Members
        bool ICommandHandler.OnCommand(string menuId)
        {
            switch (menuId)
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
        bool ICommandHandler.OnUpdateCommand(string menuId, CommandState commandState)
        {
            // TODO:  Add LinePattern.OnUpdateCommand implementation
            return false;
        }
        void ICommandHandler.OnSelected(MenuWithHandler selectedMenuItem, bool selected) { }
        #endregion

    }

    /// <summary>
    /// Implemented by GeoObjects that have a <see cref="LinePattern"/> attribute, e.g. <see cref="Line"/>, <see cref="Ellipse"/>,
    /// <see cref="BSpline"/>, <see cref="Polyline"/> and <see cref="Path"/>, and by <see cref="Style"/>. The
    /// <see cref="LinePatternList"/> of the project uses it to find and update the line patterns in use.
    /// </summary>
    public interface ILinePattern
    {
        /// <summary>
        /// The line pattern of the object, or null when none is set. For GeoObjects setting it raises the change events and is
        /// undoable.
        /// </summary>
        LinePattern LinePattern { get; set; }
    }
}
