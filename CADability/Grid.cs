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
        /// <summary>
        /// How the grid is displayed in a view: dots (a dot at each grid point), marks (a small cross at each grid point), lines
        /// (horizontal and vertical lines through the grid points) or fields (the grid cells shaded like a checkerboard).
        /// </summary>
        public enum Appearance { dots, marks, lines, fields };
        private Appearance displayMode;
        private bool show;
        /// <summary>
        /// Signature of the handlers of <see cref="GridChangedEvent"/>, which is raised when the user changes the appearance, the
        /// visibility or the distances of the grid in the property page.
        /// </summary>
        /// <param name="sender">The grid that changed</param>
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
        /// <inheritdoc/>
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
        /// <inheritdoc/>
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
        /// Writes the data of this object for the legacy binary serialization (<see cref="System.Runtime.Serialization.ISerializable"/>).
        /// Files are written in the JSON format (<see cref="IJsonSerialize"/>) now; the binary format is still read.
        /// </summary>
        /// <param name="info">receives the data</param>
        /// <param name="context">the destination of the serialization</param>
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
