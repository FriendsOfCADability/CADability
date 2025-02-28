using CADability.Attribute;
using CADability.GeoObject;

namespace CADability.UserInterface
{
    /// <summary>
    /// 
    /// </summary>

    public class HatchStyleSelectionProperty : MultipleChoiceProperty
    {
        private HatchStyleList hatchStyleList;
        private IHatchStyle iHatchStyle;
        private IGeoObject toWatch;
        public delegate void HatchStyleChanged(HatchStyle SelectedHatchstyle);
        public event HatchStyleChanged HatchStyleChangedEvent;
        public HatchStyleSelectionProperty(string ResourceId, HatchStyleList hsl, IHatchStyle iHatchStyle, bool includeUndefined) :
            this(ResourceId, hsl, iHatchStyle.HatchStyle, includeUndefined)
        {
            this.iHatchStyle = iHatchStyle;
            toWatch = iHatchStyle as IGeoObject;
        }
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
        /// <summary>
        /// Overrides <see cref="CADability.UserInterface.IShowPropertyImpl.Added (IPropertyTreeView)"/>
        /// </summary>
        /// <param name="propertyTreeView"></param>
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
        /// <summary>
        /// Overrides <see cref="CADability.UserInterface.IShowPropertyImpl.Removed (IPropertyTreeView)"/>
        /// </summary>
        /// <param name="propertyTreeView"></param>
        public override void Removed(IPropertyPage propertyTreeView)
        {
            base.Removed(propertyTreeView);
            if (toWatch != null) toWatch.DidChangeEvent -= new ChangeDelegate(ToWatchDidChange);
        }
    }
}
