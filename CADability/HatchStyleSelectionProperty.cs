using CADability.Attribute;
using CADability.GeoObject;

namespace CADability.UserInterface
{
    /// <summary>
    /// Property entry that lets the user choose a <see cref="HatchStyle"/> from a <see cref="HatchStyleList"/> in a
    /// drop-down list, optionally including an "undefined" entry. It either sets the hatch style of the
    /// <see cref="IHatchStyle"/> object (typically a <see cref="Hatch"/>) it was constructed for, following changes of
    /// that object (e.g. by undo), or it only reports the selection via the <see cref="HatchStyleChangedEvent"/>.
    /// </summary>

    public class HatchStyleSelectionProperty : MultipleChoiceProperty
    {
        private HatchStyleList hatchStyleList;
        private IHatchStyle iHatchStyle;
        private IGeoObject toWatch;
        /// <summary>
        /// Delegate for <see cref="HatchStyleChangedEvent"/>.
        /// </summary>
        /// <param name="SelectedHatchstyle">The selected hatch style, or null when the "undefined" entry was selected</param>
        public delegate void HatchStyleChanged(HatchStyle SelectedHatchstyle);
        /// <summary>
        /// Raised when the user selects a hatch style, before it is applied to the connected <see cref="IHatchStyle"/> object
        /// (if any).
        /// </summary>
        public event HatchStyleChanged HatchStyleChangedEvent;
        /// <summary>
        /// Creates an entry which controls the hatch style of <paramref name="iHatchStyle"/>. The current hatch style of the
        /// object is preselected; if the object is an <see cref="IGeoObject"/>, the entry follows changes of the object (e.g.
        /// by undo).
        /// </summary>
        /// <param name="ResourceId">Resource id of the label text</param>
        /// <param name="hsl">The list providing the choices</param>
        /// <param name="iHatchStyle">The object whose hatch style is edited</param>
        /// <param name="includeUndefined">true: an "undefined" entry (resource "HatchStyle.Undefined") is offered as the first choice</param>
        public HatchStyleSelectionProperty(string ResourceId, HatchStyleList hsl, IHatchStyle iHatchStyle, bool includeUndefined) :
            this(ResourceId, hsl, iHatchStyle.HatchStyle, includeUndefined)
        {
            this.iHatchStyle = iHatchStyle;
            toWatch = iHatchStyle as IGeoObject;
        }
        /// <summary>
        /// Creates an entry which is not connected to an object; the selection is reported via
        /// <see cref="HatchStyleChangedEvent"/>.
        /// </summary>
        /// <param name="ResourceId">Resource id of the label text</param>
        /// <param name="hsl">The list providing the choices</param>
        /// <param name="preselect">The initially selected hatch style, or null for "undefined" resp. no selection</param>
        /// <param name="includeUndefined">true: an "undefined" entry (resource "HatchStyle.Undefined") is offered as the first choice</param>
        public HatchStyleSelectionProperty(string ResourceId, HatchStyleList hsl, HatchStyle preselect, bool includeUndefined)
        {
            hatchStyleList = hsl;
            resourceIdInternal = ResourceId;
            if (includeUndefined)
            {
                base.choices = new string[hatchStyleList.Count + 1];
                for (int i = 0; i < hatchStyleList.Count; ++i)
                {
                    HatchStyle hst = hatchStyleList[i];
                    choices[i + 1] = hst.Name;
                }
                string undef = StringTable.GetString("HatchStyle.Undefined");
                // sollte es den Namen schon geben, werden solange "-" davor und dahintergemacht, bis es den Namen mehr gibt
                while (hatchStyleList.Find(undef) != null) undef = "-" + undef + "-";
                choices[0] = undef;
                if (preselect != null) SelectedText = preselect.Name;
                else SelectedText = undef;
            }
            else
            {
                base.choices = new string[hatchStyleList.Count];
                for (int i = 0; i < hatchStyleList.Count; ++i)
                {
                    HatchStyle hst = hatchStyleList[i];
                    choices[i] = hst.Name;
                }
                if (preselect != null) SelectedText = preselect.Name;
            }
        }
        /// <summary>
        /// Sets the current selection to the given hatch style without raising the event and without changing the connected
        /// object.
        /// </summary>
        /// <param name="hatchStyle">The hatch style to select, or null to clear the selection</param>
        public void SetSelection(HatchStyle hatchStyle)
        {
            if (hatchStyle == null)
            {
                base.SelectedText = null;
            }
            else
            {
                base.SelectedText = hatchStyle.Name;
            }
        }
        /// <inheritdoc/>
        protected override void OnSelectionChanged(string selected)
        {
            if (HatchStyleChangedEvent != null)
            {
                HatchStyleChangedEvent(hatchStyleList.Find(selected));
            }
            if (iHatchStyle != null)
            {
                HatchStyle sel = hatchStyleList.Find(selected);
                if (iHatchStyle.HatchStyle != sel)
                    iHatchStyle.HatchStyle = sel;
            }
            base.OnSelectionChanged(selected);
        }
        /// <inheritdoc/>
        public override void Added(IPropertyPage propertyTreeView)
        {   // bei Undo kann sich der HatchStyle ändern und muss in der Auswahl 
            base.Added(propertyTreeView);
            if (toWatch != null) toWatch.DidChangeEvent += new ChangeDelegate(ToWatchDidChange);
        }
        void ToWatchDidChange(IGeoObject Sender, GeoObjectChange Change)
        {
            if (Sender == toWatch)
            {
                if (iHatchStyle.HatchStyle != null)
                {
                    int ind = base.ChoiceIndex(iHatchStyle.HatchStyle.Name);
                    if (ind != base.CurrentIndex)
                        base.SetSelection(ind);
                }
            }
        }
        /// <inheritdoc/>
        public override void Removed(IPropertyPage propertyTreeView)
        {
            base.Removed(propertyTreeView);
            if (toWatch != null) toWatch.DidChangeEvent -= new ChangeDelegate(ToWatchDidChange);
        }
    }
}
