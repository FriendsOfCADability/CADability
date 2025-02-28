using CADability.Attribute;
using CADability.GeoObject;

namespace CADability.UserInterface
{
    /// <summary>
    /// 
    /// </summary>

    public class LinePatternSelectionProperty : MultipleChoiceProperty
    {
        private LinePatternList linePatternList;
        private IGeoObject toWatch;
        private ILinePattern iLinePattern;
        public LinePatternSelectionProperty(string ResourceID, LinePatternList linePatternList, LinePattern select) :
            this(ResourceID, linePatternList, select, false)
        {
        }
        public LinePatternSelectionProperty(string ResourceID, LinePatternList linePatternList, LinePattern select, bool includeUndefined)
        {
            this.linePatternList = linePatternList;
            resourceIdInternal = ResourceID;
            if (includeUndefined)
            {
                choices = new string[linePatternList.Count + 1];
                for (int i = 0; i < linePatternList.Count; ++i)
                {
                    choices[i + 1] = linePatternList[i].Name;
                }
                string undef = StringTable.GetString("LinePattern.Undefined");
                // sollte es den Namen schon geben, werden solange - davor und dahintergemacht, bis es den Namen mehr gibt
                while (linePatternList.Find(undef) != null) undef = "-" + undef + "-";
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
                choices = new string[linePatternList.Count];
                for (int i = 0; i < linePatternList.Count; ++i)
                {
                    choices[i] = linePatternList[i].Name;
                }
                if (select != null)
                {
                    base.SelectedText = select.Name;
                }
            }
        }
        public LinePatternSelectionProperty(string ResourceID, LinePatternList linePatternList, ILinePattern iLinePattern, bool includeUndefined)
        {
            this.linePatternList = linePatternList;
            resourceIdInternal = ResourceID;
            LinePattern select = iLinePattern.LinePattern;
            if (includeUndefined)
            {
                choices = new string[linePatternList.Count + 1];
                for (int i = 0; i < linePatternList.Count; ++i)
                {
                    choices[i + 1] = linePatternList[i].Name;
                }
                string undef = StringTable.GetString("LinePattern.Undefined");
                // sollte es den Namen schon geben, werden solange - davor und dahintergemacht, bis es den Namen mehr gibt
                while (linePatternList.Find(undef) != null) undef = "-" + undef + "-";
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
                choices = new string[linePatternList.Count];
                for (int i = 0; i < linePatternList.Count; ++i)
                {
                    choices[i] = linePatternList[i].Name;
                }
                if (select != null)
                {
                    base.SelectedText = select.Name;
                }
            }
            this.iLinePattern = iLinePattern;
            toWatch = iLinePattern as IGeoObject;
        }
        public delegate void LinePatternChangedDelegate(LinePattern selected);
        public event LinePatternChangedDelegate LinePatternChangedEvent;
        protected override void OnSelectionChanged(string selected)
        {
            base.OnSelectionChanged(selected);
            // und weiterleiten
            if (LinePatternChangedEvent != null) LinePatternChangedEvent(linePatternList.Find(selected));
            if (iLinePattern != null) iLinePattern.LinePattern = linePatternList.Find(selected);
        }
        /// <summary>
        /// Overrides <see cref="IShowPropertyImpl.Added"/>
        /// </summary>
        /// <param name="propertyPage"></param>
        public override void Added(IPropertyPage propertyPage)
        {
            base.Added(propertyPage);
            if (toWatch != null) toWatch.DidChangeEvent += new ChangeDelegate(GeoObjectDidChange);
        }
        /// <summary>
        /// Overrides <see cref="IShowPropertyImpl.Removed"/>
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
                if ((Change as GeoObjectChange).MethodOrPropertyName == "LinePattern" ||
                    (Change as GeoObjectChange).MethodOrPropertyName == "Style")
                {
                    if ((toWatch as ILinePattern).LinePattern != null) base.SelectedText = (toWatch as ILinePattern).LinePattern.Name;
                    else base.SelectedText = null;
                    propertyPage.Refresh(this);
                }
            }
        }
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
                iLinePattern = value as ILinePattern;
                if (toWatch != null) toWatch.DidChangeEvent += new ChangeDelegate(GeoObjectDidChange);
            }
        }
    }
}
