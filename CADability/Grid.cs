using CADability.UserInterface;
using System;
using System.Runtime.Serialization;

namespace CADability
{
    /// <summary>
    /// The Grid settings, used only in class <see cref="Projection"/>.
    /// </summary>
    [Serializable]
    public class Grid : IShowPropertyImpl, ISerializable
    {
        private double dx;
        private double dy;
        public enum Appearance { dots, marks, lines, fields };
        private Appearance displayMode;
        private bool show;
        public delegate void GridChangedDelegate(Grid sender);
        public event GridChangedDelegate GridChangedEvent;
        public Grid()
        {
            dx = dy = 10.0; // damit es nicht 0.0 ist
            displayMode = Appearance.dots;
            show = false;
            base.resourceIdInternal = "Grid";
        }
        public double XDistance
        {
            get
            {
                return dx;
            }
            set
            {
                dx = value;
            }
        }
        public double YDistance
        {
            get
            {
                return dy;
            }
            set
            {
                dy = value;
            }
        }
        public Appearance DisplayMode
        {
            set
            {
                displayMode = value;
            }
            get
            {
                return displayMode;
            }
        }
        public bool Show
        {
            get
            {
                return show;
            }
            set
            {
                show = value;
            }
        }
        #region IShowPropertyImpl
        /// <summary>
        /// Overrides <see cref="IShowPropertyImpl.EntryType"/>,
        /// returns <see cref="ShowPropertyEntryType.GroupTitle"/>.
        /// </summary>
        public override ShowPropertyEntryType EntryType
        {
            get
            {
                return ShowPropertyEntryType.GroupTitle;
            }
        }
        private IShowProperty[] subEntries;
        private DoubleProperty dxProperty;
        private DoubleProperty dyProperty;
        private MultipleChoiceProperty displayModeProperty;
        /// <summary>
        /// Overrides <see cref="IShowPropertyImpl.SubEntries"/>,
        /// returns the subentries in this property view.
        /// </summary>
        public override IShowProperty[] SubEntries
        {
            get
            {
                if (subEntries == null)
                {
                    subEntries = new IShowProperty[3];
                    dxProperty = new DoubleProperty(base.Frame,"Grid.XDistance");
                    dyProperty = new DoubleProperty(base.Frame, "Grid.YDistance");
                    dxProperty.OnGetValue = () => XDistance;
                    dxProperty.OnSetValue = l => dx = l;
					dyProperty.OnGetValue = () => YDistance;
                    dyProperty.OnSetValue = l => dy = l;
                    dxProperty.Refresh();
                    dyProperty.Refresh();
                    int initial = (int)displayMode + 1;
                    if (!show) initial = 0;
                    displayModeProperty = new MultipleChoiceProperty("Grid.DisplayMode", initial);
                    displayModeProperty.ValueChangedEvent += OnDisplayModeValueChanged;
                    subEntries[0] = dxProperty;
                    subEntries[1] = dyProperty;
                    subEntries[2] = displayModeProperty;

                }
                return subEntries;
            }
        }
        /// <summary>
        /// Overrides <see cref="IShowPropertyImpl.SubEntriesCount"/>,
        /// returns the number of subentries in this property view.
        /// </summary>
        public override int SubEntriesCount => SubEntries.Length;

        #endregion
        #region ISerializable Members
        /// <summary>
        /// Constructor required by deserialization
        /// </summary>
        /// <param name="info">SerializationInfo</param>
        /// <param name="context">StreamingContext</param>
        protected Grid(SerializationInfo info, StreamingContext context)
            : this()
        {
            dx = (double)info.GetValue("XDistance", typeof(double));
            dy = (double)info.GetValue("YDistance", typeof(double));
            displayMode = (Appearance)info.GetValue("DisplayMode", typeof(Appearance));
            show = (bool)info.GetValue("Show", typeof(bool));
        }
        /// <summary>
        /// Implements <see cref="ISerializable.GetObjectData"/>
        /// </summary>
        /// <param name="info">The <see cref="System.Runtime.Serialization.SerializationInfo"/> to populate with data.</param>
        /// <param name="context">The destination (<see cref="System.Runtime.Serialization.StreamingContext"/>) for this serialization.</param>
        public void GetObjectData(SerializationInfo info, StreamingContext context)
        {
            info.AddValue("XDistance", dx);
            info.AddValue("YDistance", dy);
            info.AddValue("DisplayMode", displayMode, typeof(Appearance));
            info.AddValue("Show", show);
        }

        #endregion

        private void OnDisplayModeValueChanged(object sender, object NewValue)
        {
            if (displayModeProperty.CurrentIndex == 0)
            {
                show = false;
            }
            else
            {
                displayMode = (Appearance)(displayModeProperty.CurrentIndex - 1);
                show = true;
            }
            if (GridChangedEvent != null) GridChangedEvent(this);
        }

        private void OnFocusChanged(IPropertyTreeView sender, IShowProperty NewFocus, IShowProperty OldFocus)
        {
            // hier nur feuern wenn der Abstand verlassen wird,
            // displaymode wird bei jeder Änderung gefeurt
            if ((OldFocus == dxProperty && NewFocus != dyProperty) ||
                (OldFocus == dyProperty && NewFocus != dxProperty))
            {
                if (GridChangedEvent != null) GridChangedEvent(this);
            }
        }
    }
}
