using CADability.Attribute;
using CADability.GeoObject;

namespace CADability.UserInterface
{
    /// <summary>
    /// Property entry which lets the user choose a <see cref="LineWidth"/> from a <see cref="LineWidthList"/> in a drop-down
    /// list, optionally including an "undefined" entry. When constructed for an object implementing <see cref="ILineWidth"/>,
    /// the selection is applied to that object and the entry follows changes of the object's line width; otherwise the selection
    /// is only reported via <see cref="LineWidthChangedEvent"/>.
    /// </summary>

    public class LineWidthSelectionProperty : MultipleChoiceProperty
    {
        private LineWidthList lineWidthList;
        private IGeoObject toWatch;
        private ILineWidth iLineWidth;
        /// <summary>
        /// Creates an entry without the "undefined" choice which is not connected to an object; the selection is reported via
        /// <see cref="LineWidthChangedEvent"/>.
        /// </summary>
        /// <param name="ResourceId">Resource id of the label text</param>
        /// <param name="lineWidthList">The list providing the choices</param>
        /// <param name="select">The initially selected line width, or null for no selection</param>
        public LineWidthSelectionProperty(string ResourceId, LineWidthList lineWidthList, LineWidth select) : this(ResourceId, lineWidthList, select, false)
        {
        }
        /// <summary>
        /// Creates an entry which is not connected to an object; the selection is reported via
        /// <see cref="LineWidthChangedEvent"/>.
        /// </summary>
        /// <param name="ResourceId">Resource id of the label text</param>
        /// <param name="lineWidthList">The list providing the choices</param>
        /// <param name="select">The initially selected line width, or null for "undefined" resp. no selection</param>
        /// <param name="includeUndefined">true: an "undefined" entry (resource "LineWidth.Undefined") is offered as the first choice</param>
        public LineWidthSelectionProperty(string ResourceId, LineWidthList lineWidthList, LineWidth select, bool includeUndefined)
        {
            this.lineWidthList = lineWidthList;
            resourceIdInternal = ResourceId;
            if (includeUndefined)
            {
                choices = new string[lineWidthList.Count + 1];
                for (int i = 0; i < lineWidthList.Count; ++i)
                {
                    choices[i + 1] = lineWidthList[i].Name;
                }
                string undef = StringTable.GetString("LineWidth.Undefined");
                // sollte es den Namen schon geben, werden solange - davor und dahintergemacht, bis es den Namen mehr gibt
                while (lineWidthList.Find(undef) != null) undef = "-" + undef + "-";
                choices[0] = undef;
                if (select != null)
                {
                    base.SelectedText = select.Name;
                }
                else
                {
                    base.SelectedText = undef;
                }
            }
            else
            {
                choices = new string[lineWidthList.Count];
                for (int i = 0; i < lineWidthList.Count; ++i)
                {
                    choices[i] = lineWidthList[i].Name;
                }
                if (select != null)
                {
                    base.SelectedText = select.Name;
                }
            }
        }
        /// <summary>
        /// Creates an entry which controls the line width of <paramref name="iLineWidth"/>. The current line width of the
        /// object is preselected; if the object is an <see cref="IGeoObject"/>, the entry follows changes of its line width
        /// or style.
        /// </summary>
        /// <param name="ResourceId">Resource id of the label text</param>
        /// <param name="lineWidthList">The list providing the choices</param>
        /// <param name="iLineWidth">The object whose line width is edited</param>
        /// <param name="includeUndefined">true: an "undefined" entry (resource "LineWidth.Undefined") is offered as the first choice</param>
        public LineWidthSelectionProperty(string ResourceId, LineWidthList lineWidthList, ILineWidth iLineWidth, bool includeUndefined)
        {
            this.lineWidthList = lineWidthList;
            resourceIdInternal = ResourceId;
            LineWidth select = iLineWidth.LineWidth;
            if (includeUndefined)
            {
                choices = new string[lineWidthList.Count + 1];
                for (int i = 0; i < lineWidthList.Count; ++i)
                {
                    choices[i + 1] = lineWidthList[i].Name;
                }
                string undef = StringTable.GetString("LineWidth.Undefined");
                // sollte es den Namen schon geben, werden solange - davor und dahintergemacht, bis es den Namen mehr gibt
                while (lineWidthList.Find(undef) != null) undef = "-" + undef + "-";
                choices[0] = undef;
                if (select != null)
                {
                    base.SelectedText = select.Name;
                }
                else
                {
                    base.SelectedText = undef;
                }
            }
            else
            {
                choices = new string[lineWidthList.Count];
                for (int i = 0; i < lineWidthList.Count; ++i)
                {
                    choices[i] = lineWidthList[i].Name;
                }
                if (select != null)
                {
                    base.SelectedText = select.Name;
                }
            }
            this.iLineWidth = iLineWidth;
            toWatch = iLineWidth as IGeoObject;
        }
        /// <summary>
        /// Delegate for <see cref="LineWidthChangedEvent"/>.
        /// </summary>
        /// <param name="selected">The selected line width, or null when the "undefined" entry was selected</param>
        public delegate void LineWidthChangedDelegate(LineWidth selected);
        /// <summary>
        /// Raised when the user selects a line width, before it is applied to the connected object (if any).
        /// </summary>
        public event LineWidthChangedDelegate LineWidthChangedEvent;
        /// <inheritdoc/>
        protected override void OnSelectionChanged(string selected)
        {
            base.OnSelectionChanged(selected);
            // und weiterleiten
            if (LineWidthChangedEvent != null) LineWidthChangedEvent(lineWidthList.Find(selected));
            if (iLineWidth != null) iLineWidth.LineWidth = lineWidthList.Find(selected);
        }
        /// <inheritdoc/>
        public override void Added(IPropertyPage propertyPage)
        {
            base.Added(propertyPage);
            if (toWatch != null) toWatch.DidChangeEvent += new ChangeDelegate(GeoObjectDidChange);
        }
        /// <summary>
        /// Overrides <see cref="IShowPropertyImpl.Removed(IPropertyPage)"/>
        /// </summary>
        /// <param name="propertyPage">the IPropertyTreeView from which it was removed</param>
        public override void Removed(IPropertyPage propertyPage)
        {
            base.Removed(propertyPage);
            if (toWatch != null) toWatch.DidChangeEvent -= new ChangeDelegate(GeoObjectDidChange);
        }
        private void GeoObjectDidChange(IGeoObject Sender, GeoObjectChange Change)
        {
            if (Sender == toWatch && Change.OnlyAttributeChanged && propertyPage != null)
            {
                if ((Change as GeoObjectChange).MethodOrPropertyName == "LineWidth" ||
                    (Change as GeoObjectChange).MethodOrPropertyName == "Style")
                {
                    if ((toWatch as ILineWidth).LineWidth != null) base.SelectedText = (toWatch as ILineWidth).LineWidth.Name;
                    else base.SelectedText = null;
                    propertyPage.Refresh(this);
                }
            }
        }
        /// <summary>
        /// The <see cref="IGeoObject"/> whose line width is controlled by this entry. Setting it connects the entry to the new
        /// object (which must implement <see cref="ILineWidth"/>): its line width is changed when the user selects an entry,
        /// and the entry is refreshed when the line width or style of the object changes.
        /// </summary>
        public IGeoObject Connected
        {   // mit dieser Property kann man das kontrollierte Geoobjekt ändern
            get { return toWatch; }
            set
            {
                if (base.propertyPage != null)
                {   // dann ist diese Property schon Added und nicht removed
                    if (toWatch != null) toWatch.DidChangeEvent -= new ChangeDelegate(GeoObjectDidChange);
                }
                toWatch = value;
                iLineWidth = value as ILineWidth;
                if (toWatch != null) toWatch.DidChangeEvent += new ChangeDelegate(GeoObjectDidChange);
            }
        }
    }
}
