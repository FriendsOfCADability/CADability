using CADability.Attribute;
using CADability.GeoObject;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;

namespace CADability.UserInterface
{
    internal class MultiObjectsProperties
    {
        GeoObjectList selectedObjects;
        IFrame frame;
        public SimplePropertyGroup attributeProperties;
        private LinePatternSelectionProperty multiLinePattern; // LinePattern für mehrere Objekte
        private LineWidthSelectionProperty multiLineWidth; // LineWidth für mehrere Objekte
        private LayerSelectionProperty multiLayer; // Layer für mehrere Objekte
        private ColorSelectionProperty multiColorDef; // Layer für mehrere Objekte
        private StyleSelectionProperty multiStyle; // Style für mehrere Objekte
        private IntegerProperty objectCount;
        private Dictionary<string, IPropertyEntry> multiAttributes; // Liste aller custom SelectionProperties
        private Dictionary<string, IPropertyEntry> multiUserData; // Liste aller Userdata mit IMultiObjectUserData interface
        public bool isChangingMultipleAttributes; // es werden gerade Attribute der Objekte geändert
        public MultiObjectsProperties(IFrame frame, GeoObjectList selectedObjects)
        {
            this.selectedObjects = selectedObjects;
            this.frame = frame;

            attributeProperties = new SimplePropertyGroup("Select.Attributes");

            if (objectCount == null)
            {
                objectCount = new IntegerProperty(selectedObjects.Count, "Select.NumObjects");
                objectCount.ReadOnly = true;
            }
            attributeProperties.Add(objectCount);

            if (multiLinePattern == null)
            {	// nur einmal erzeugen, steht halt dumm rum, wenn nur ein Objekt markiert ist
                multiLinePattern = new LinePatternSelectionProperty("Select.LinePattern", frame.Project.LinePatternList, null);
                multiLinePattern.LinePatternChangedEvent += new CADability.UserInterface.LinePatternSelectionProperty.LinePatternChangedDelegate(OnMultiLinePatternSelectionChanged);
                multiLinePattern.SetUnselectedText("Select.MultipleLinePattern");
            }
            attributeProperties.Add(multiLinePattern);

            if (multiLineWidth == null)
            {	// nur einmal erzeugen, steht halt dumm rum, wenn nur ein Objekt markiert ist
                multiLineWidth = new LineWidthSelectionProperty("Select.LineWidth", frame.Project.LineWidthList, null);
                multiLineWidth.LineWidthChangedEvent += new CADability.UserInterface.LineWidthSelectionProperty.LineWidthChangedDelegate(OnMultiLineWidthSelectionChanged);
                multiLineWidth.SetUnselectedText("Select.MultipleLineWidth");
            }
            attributeProperties.Add(multiLineWidth);

            if (multiLayer == null)
            {	// nur einmal erzeugen, steht halt dumm rum, wenn nur ein Objekt markiert ist
                multiLayer = new LayerSelectionProperty("Select.Layer", frame.Project.LayerList, null);
                multiLayer.LayerChangedEvent += new CADability.Attribute.LayerSelectionProperty.LayerChangedDelegate(OnMultiLayerSelectionChanged);
                multiLayer.SetUnselectedText("Select.MultipleLayer");
            }
            attributeProperties.Add(multiLayer);

            if (multiColorDef == null)
            {	// nur einmal erzeugen, steht halt dumm rum, wenn nur ein Objekt markiert ist
                multiColorDef = new ColorSelectionProperty("Select.ColorDef", frame.Project.ColorList, null, ColorList.StaticFlags.allowNone);
                multiColorDef.ColorDefChangedEvent += new ColorSelectionProperty.ColorDefChangedDelegate(OnMultiColorDefSelectionChanged);
                multiColorDef.SetUnselectedText("Select.MultipleColorDef");
            }
            attributeProperties.Add(multiColorDef);

            if (multiStyle == null)
            {	// nur einmal erzeugen, steht halt dumm rum, wenn nur ein Objekt markiert ist
                multiStyle = new StyleSelectionProperty(null, "Select.Style", frame.Project.StyleList);
                multiStyle.StyleChangedEvent += new StyleSelectionProperty.StyleChangedDelegate(OnMultiStyleChanged);
                multiStyle.SetUnselectedText("Select.MultipleColorDef");
            }
            attributeProperties.Add(multiStyle);

            // gemeinsame custom Attribute sammeln und zufügen
            multiAttributes = new Dictionary<string, IPropertyEntry>();
            for (int i = 0; i < selectedObjects.Count; ++i)
            {
                string[] nasp = selectedObjects[i].CustomAttributeKeys; // hierrüber den Punkt nach dem Symbol fragen
                for (int j = 0; j < nasp.Length; ++j)
                {
                    if (!multiAttributes.ContainsKey(nasp[j]))
                    {   // es wird nur einmal erzeugt
                        string key = nasp[j];
                        IPropertyEntry ina = selectedObjects[i].GetNamedAttribute(key).GetSelectionProperty(key, frame.Project, selectedObjects);
                        if (ina != null)
                        {
                            multiAttributes[key] = ina;
                        }
                    }
                }
            }
            foreach (IPropertyEntry sp in multiAttributes.Values)
            {
                attributeProperties.Add(sp);
            }

            // gemeinsame Userdata mit IMultiObjectUserData interface sammeln und zufügen
            multiUserData = new Dictionary<string, IPropertyEntry>();
            for (int i = 0; i < selectedObjects.Count; ++i)
            {
                string[] na = selectedObjects[i].UserData.AllItems;
                for (int j = 0; j < na.Length; ++j)
                {
                    object data = selectedObjects[i].UserData.GetData(na[j]);
                    if (data is IMultiObjectUserData && !multiUserData.ContainsKey(na[j]))
                    {   // es wird nur einmal erzeugt
                        string key = na[j];
                        multiUserData[key] = (data as IMultiObjectUserData).GetShowProperty(selectedObjects);
                    }
                }
            }
            foreach (IPropertyEntry sp in multiUserData.Values)
            {
                attributeProperties.Add(sp);
            }

            //If all objects are the same, you can change common attributes
            Type firstObjectType = selectedObjects[0].GetType();
            bool isAllSameType = selectedObjects.All(o => o.GetType() == firstObjectType);

            if (isAllSameType)
            {
                IGeoObject geoObject = selectedObjects[0];
                IPropertyEntry sp = geoObject.GetShowProperties(frame);
                //Add all common properties to the attributeProperties
                foreach (var prop in sp.SubItems)
                {
                    switch (prop)
                    {
                        //LengthProperty e.g. Radius, Diameter, arc length
                        case LengthProperty lp:
                            if (lp.ReadOnly) continue; //Only add writeable properties
                            LengthProperty commonPropLength = new LengthProperty(frame, prop.ResourceId);
                            commonPropLength.OnSetValue = delegate (double l) { CommonProp_SetLengthEvent(commonPropLength.ResourceId, l); };
                            commonPropLength.OnGetValue = delegate () { return CommonProp_GetLengthEvent(commonPropLength.ResourceId); };
                            attributeProperties.Add(commonPropLength);
                            break;
                            //TODO: Add more cases
                    }
                }
            }

            InitMultiAttributeSelection();
        }

        /// <summary>
        /// Find the common length for all selectedObjects otherwise return 0
        /// </summary>
        /// <param name="resourceId">ResourceId</param>
        /// <returns>Common Length</returns>
        private double GetCommonLength(string resourceId)
        {
            double firstLength = GetLengthValueFromResourceValue(selectedObjects[0], resourceId);

            foreach (var geo in selectedObjects)
                if (firstLength != GetLengthValueFromResourceValue(geo, resourceId))
                    return 0.0d;

            return firstLength;
        }

        private double GetLengthValueFromResourceValue(IGeoObject geo, string resourceId)
        {
            IPropertyEntry objectSp = geo.GetShowProperties(frame);
            IPropertyEntry modSp = objectSp.FindSubItem(resourceId);
            return ((LengthProperty)modSp).GetLength();
        }

        private double CommonProp_GetLengthEvent(string resourceId)
        {
            return GetCommonLength(resourceId);
        }

        private void CommonProp_SetLengthEvent(string resourceId, double l)
        {
            isChangingMultipleAttributes = true;
            using (frame.Project.Undo.UndoFrame)
            {
                foreach (IGeoObject geo in selectedObjects)
                {
                    IPropertyEntry objectSp = geo.GetShowProperties(frame);
                    IPropertyEntry modSp = objectSp.FindSubItem(resourceId);
                    if (modSp != null)
                        ((LengthProperty)modSp).SetLength(l);
                }
            }
            //Refresh all other properties because they could depend on each other
            //e.g if you change the diameter the radius will change too
            attributeProperties.Refresh();
            
            isChangingMultipleAttributes = false;
            MultiChangeDone();
        }

        public void InitMultiAttributeSelection()
        {
            if (multiLinePattern == null) return;
            LinePattern commonLinePattern = null;
            bool linePatternValid = true;
            for (int i = 0; i < selectedObjects.Count; ++i)
            {
                ILinePattern ilp = selectedObjects[i] as ILinePattern;
                if (linePatternValid && ilp != null)
                {
                    if (commonLinePattern == null) commonLinePattern = ilp.LinePattern;
                    else linePatternValid = (ilp.LinePattern != null) && (commonLinePattern == ilp.LinePattern);
                }
            }
            if (linePatternValid && commonLinePattern != null)
            {
                multiLinePattern.SetSelection(frame.Project.LinePatternList.FindIndex(commonLinePattern));
            }
            else
            {
                multiLinePattern.SetSelection(-1);
            }

            LineWidth commonLineWidth = null;
            bool lineWidthValid = true;
            for (int i = 0; i < selectedObjects.Count; ++i)
            {
                ILineWidth ilp = selectedObjects[i] as ILineWidth;
                if (lineWidthValid && ilp != null)
                {
                    if (commonLineWidth == null) commonLineWidth = ilp.LineWidth;
                    else lineWidthValid = (ilp.LineWidth != null) && (commonLineWidth == ilp.LineWidth);
                }
            }
            if (lineWidthValid && commonLineWidth != null)
            {
                multiLineWidth.SetSelection(frame.Project.LineWidthList.FindIndex(commonLineWidth));
            }
            else
            {
                multiLineWidth.SetSelection(-1);
            }

            Layer commonLayer = null;
            bool LayerValid = true;
            for (int i = 0; i < selectedObjects.Count; ++i)
            {
                ILayer ily = selectedObjects[i] as ILayer;
                if (LayerValid && ily != null)
                {
                    if (commonLayer == null) commonLayer = ily.Layer;
                    else LayerValid = (ily.Layer != null) && (commonLayer == ily.Layer);
                }
            }
            if (LayerValid && commonLayer != null)
            {
                multiLayer.SetSelection(frame.Project.LayerList.FindIndex(commonLayer));
            }
            else
            {
                multiLayer.SetSelection(-1);
            }

            ColorDef commonColorDef = null;
            bool ColorDefValid = true;
            for (int i = 0; i < selectedObjects.Count; ++i)
            {
                IColorDef ily = selectedObjects[i] as IColorDef;
                if (ColorDefValid && ily != null)
                {
                    if (commonColorDef == null) commonColorDef = ily.ColorDef;
                    else ColorDefValid = (ily.ColorDef != null) && (commonColorDef == ily.ColorDef);
                }
            }
            if (ColorDefValid && commonColorDef != null)
            {
                multiColorDef.SetSelection(frame.Project.ColorList.FindIndex(commonColorDef));
            }
            else
            {
                multiColorDef.SetSelection(-1);
            }

            Style commonStyle = null;
            bool StyleValid = true;
            for (int i = 0; i < selectedObjects.Count; ++i)
            {
                IStyle ily = selectedObjects[i] as IStyle;
                if (StyleValid && ily != null)
                {
                    if (commonStyle == null) commonStyle = ily.Style;
                    else StyleValid = (ily.Style != null) && (commonStyle == ily.Style);
                }
            }
            if (StyleValid && commonStyle != null)
            {
                multiStyle.SetSelection(frame.Project.StyleList.FindIndex(commonStyle));
            }
            else
            {
                multiStyle.SetSelection(-1);
            }

        }
        private void OnMultiLinePatternSelectionChanged(LinePattern selected)
        {
            isChangingMultipleAttributes = true;
            using (frame.Project.Undo.UndoFrame)
            {
                for (int i = 0; i < selectedObjects.Count; ++i)
                {
                    ILinePattern ilp = selectedObjects[i] as ILinePattern;
                    if (ilp != null)
                    {
                        ilp.LinePattern = selected;
                    }
                }
            }
            isChangingMultipleAttributes = false;
            MultiChangeDone();
        }
        private void OnMultiLineWidthSelectionChanged(LineWidth selected)
        {
            isChangingMultipleAttributes = true;
            using (frame.Project.Undo.UndoFrame)
            {
                for (int i = 0; i < selectedObjects.Count; ++i)
                {
                    ILineWidth ilw = selectedObjects[i] as ILineWidth;
                    if (ilw != null)
                    {
                        ilw.LineWidth = selected;
                    }
                }
            }
            isChangingMultipleAttributes = false;
            MultiChangeDone();
        }
        private void OnMultiLayerSelectionChanged(Layer selected)
        {
            isChangingMultipleAttributes = true;
            using (frame.Project.Undo.UndoFrame)
            {
                for (int i = 0; i < selectedObjects.Count; ++i)
                {
                    ILayer ily = selectedObjects[i] as ILayer;
                    if (ily != null)
                    {
                        ily.Layer = selected;
                    }
                }
            }
            isChangingMultipleAttributes = false;
            MultiChangeDone();
        }
        private void OnMultiColorDefSelectionChanged(ColorDef selected)
        {
            isChangingMultipleAttributes = true;
            using (frame.Project.Undo.UndoFrame)
            {
                for (int i = 0; i < selectedObjects.Count; ++i)
                {
                    IColorDef icd = selectedObjects[i] as IColorDef;
                    if (icd != null)
                    {
                        icd.ColorDef = selected;
                    }
                }
            }
            isChangingMultipleAttributes = false;
            MultiChangeDone();
        }
        private void OnMultiStyleChanged(Style selected)
        {
            isChangingMultipleAttributes = true;
            using (frame.Project.Undo.UndoFrame)
            {
                for (int i = 0; i < selectedObjects.Count; ++i)
                {
                    IStyle ist = selectedObjects[i] as IStyle;
                    if (ist != null)
                    {
                        ist.Style = selected;
                    }
                }
            }
            isChangingMultipleAttributes = false;
            InitMultiAttributeSelection(); // der Stil verändert eben auch die anderen Attribute
            MultiChangeDone();
        }
        private void MultiChangeDone()
        {	// das ist nötig, da die Änderungen in SelectObjectAction nicht ausgeführt wurden
            foreach (IView vw in frame.AllViews)
            {
                vw.Invalidate(PaintBuffer.DrawingAspect.Select, vw.DisplayRectangle);
            }
        }
    }
    /// <summary>
    /// Eigenschaftsanzeige für die Markierten Objekte. Diese Anzeige wird bei Änderung
    /// der Markierung neu generiert. Die Anzeige ermöglicht für jedes markierte Objekt
    /// Änderungen der Eigenschaften.
    /// </summary>

    public class SelectedObjectsProperty : PropertyEntryImpl, IDisplayHotSpots
    {
        protected GeoObjectList selectedObjects;
        private IPropertyEntry[] showProperties;
        private MultiObjectsProperties multiObjectsProperties;
        public IGeoObject focusedSelectedObject; // eines kann fokusiert sein
        internal bool IsChanging
        {
            get
            {
                return multiObjectsProperties != null && multiObjectsProperties.isChangingMultipleAttributes;
            }
        }
        internal IGeoObject ContextMenuSource; // dieses Objekt ist verantwortlich für das aktuelle Contextmenue
                                               // aufgrund einer Auswahl in einer der ComboBoxen
        public override void Refresh()
        {
            showProperties = null;
            if (propertyPage != null)
            {
                propertyPage.Refresh(this);
                if (multiObjectsProperties != null) propertyPage.OpenSubEntries(multiObjectsProperties.attributeProperties, true);
            }
        }
        #region polymorph construction
        public delegate SelectedObjectsProperty ConstructionDelegate(IFrame Frame);
        public delegate void FocusedObjectChangedDelegate(SelectedObjectsProperty sender, IGeoObject focused);
        public event FocusedObjectChangedDelegate FocusedObjectChangedEvent;
        public static ConstructionDelegate Constructor;
        public static SelectedObjectsProperty Construct(IFrame Frame)
        {
            if (Constructor != null) return Constructor(Frame);
            return new SelectedObjectsProperty(Frame);
        }
        #endregion
        protected SelectedObjectsProperty(IFrame Frame)
            : base(Frame)
        {
            selectedObjects = new GeoObjectList(); // zuerst eine leere Liste
            resourceIdInternal = "SelectedObjects.Title";
        }
        public void SetGeoObjectList(GeoObjectList SelectedObjects)
        {
            for (int i = 0; i < selectedObjects.Count; ++i)
            {
                selectedObjects[i].DidChangeEvent -= new CADability.GeoObject.ChangeDelegate(GeoObjectDidChange);
            }
            selectedObjects = SelectedObjects;
            for (int i = 0; i < selectedObjects.Count; ++i)
            {
                selectedObjects[i].DidChangeEvent += new CADability.GeoObject.ChangeDelegate(GeoObjectDidChange);
            }
            if (showProperties != null)
            {
                for (int i = 0; i < showProperties.Length; ++i)
                {
                    IDisplayHotSpots hsp = showProperties[i] as IDisplayHotSpots;
                    if (hsp != null) hsp.HotspotChangedEvent -= new HotspotChangedDelegate(OnSubHotspotChanged);
                    IGeoObjectShowProperty gsp = showProperties[i] as IGeoObjectShowProperty;
                    if (gsp != null)
                    {
                        gsp.CreateContextMenueEvent -= new CreateContextMenueDelegate(OnCreateSubContextMenue);
                    }
                }
            }
            showProperties = null;
            focusedSelectedObject = null;
            if (propertyPage != null)
            {
                propertyPage.Refresh(this);
                // TODO: muss woanders hin, geht hier nicht!
                if (multiObjectsProperties != null) propertyPage.OpenSubEntries(multiObjectsProperties.attributeProperties, true);
            }
        }
        #region PropertyEntryImpl overrides
        public override PropertyEntryType Flags
        {
            get
            {
                return PropertyEntryType.ContextMenu | PropertyEntryType.Selectable | PropertyEntryType.ContextMenu | PropertyEntryType.GroupTitle | PropertyEntryType.HasSubEntries;
            }
        }
        #endregion
        #region PropertyEntryImpl overrides
        public override IPropertyEntry[] SubItems
        {
            get
            {
                if (showProperties == null)
                {
                    List<IPropertyEntry> items = new List<IPropertyEntry>();
                    items.Add(Frame.Project.FilterList);
                    multiObjectsProperties = null;
                    if (selectedObjects.Count > 1)
                    {
                        multiObjectsProperties = new MultiObjectsProperties(Frame, selectedObjects);
                        items.Add(multiObjectsProperties.attributeProperties);
                    }
                    // if (selectedObjects.Count < 100)
                    int mcce = Settings.GlobalSettings.GetIntValue("Select.MaxControlCenterEntries", 0);
                    bool showSubEntries = !(mcce > 0 && selectedObjects.Count > mcce);
                    if (showSubEntries)
                    {   // bei etwa 400 Objekten steigt er sonst aus...
                        for (int i = 0; i < selectedObjects.Count; ++i)
                        {
                            IPropertyEntry sp = selectedObjects[i].GetShowProperties(Frame);
                            IDisplayHotSpots hsp = sp as IDisplayHotSpots;
                            if (hsp != null) hsp.HotspotChangedEvent += new HotspotChangedDelegate(OnSubHotspotChanged);
                            items.Add(sp as IPropertyEntry);
                            sp.PropertyEntryChangedStateEvent += new PropertyEntryChangedStateDelegate(OnShowPropertyStateChanged);
                            IGeoObjectShowProperty gsp = sp as IGeoObjectShowProperty;
                            if (gsp != null)
                            {
                                gsp.CreateContextMenueEvent += new CreateContextMenueDelegate(OnCreateSubContextMenue);
                            }
                        }
                    }
                    else
                    {
                        SeperatorProperty sp = new SeperatorProperty("Select.TooManyObjects");
                        sp.LabelText = StringTable.GetFormattedString("Select.TooManyObjects", selectedObjects.Count);
                        items.Add(sp);
                    }
                    showProperties = items.ToArray();
                }
                return showProperties;
            }
        }
        /// <summary>
        /// Will be called when the state of an entry in the ControlCenter changes. this implementation must be called by a class overriding this method.
        /// </summary>
        /// <param name="sender">The ShowProperty that changed its state</param>
        /// <param name="args">The new state</param>
        protected void OnShowPropertyStateChanged(IPropertyEntry sender, StateChangedArgs args)
        {
            if (sender is IGeoObjectShowProperty)
            {
                if (args.EventState == StateChangedArgs.State.Selected)
                {
                    focusedSelectedObject = (sender as IGeoObjectShowProperty).GetGeoObject();
                    if (FocusedObjectChangedEvent != null) FocusedObjectChangedEvent(this, focusedSelectedObject);
                }
                else if (args.EventState == StateChangedArgs.State.UnSelected)
                {
                    if (focusedSelectedObject == (sender as IGeoObjectShowProperty).GetGeoObject())
                        focusedSelectedObject = null;
                    if (FocusedObjectChangedEvent != null) FocusedObjectChangedEvent(this, focusedSelectedObject);
                }
                else return;
                foreach (IView vw in base.Frame.AllViews)
                {
                    vw.Invalidate(PaintBuffer.DrawingAspect.Select, vw.DisplayRectangle);
                }
            }
        }
        public override MenuWithHandler[] ContextMenu
        {
            get
            {
                return MenuResource.LoadMenuDefinition("MenuId.SelectedObjects", false, Frame.CommandHandler);
            }
        }
        /// <summary>
        /// Overrides <see cref="CADability.UserInterface.IShowPropertyImpl.OnSpecialKey (IShowProperty, KeyEventArgs)"/>
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="args"></param>
#endregion
        public void ShowOpen(IShowProperty toShow)
        {
            if (propertyPage != null) propertyPage.OpenSubEntries(toShow as IPropertyEntry, true);
        }
        protected void OnSubHotspotChanged(IHotSpot sender, HotspotChangeMode mode)
        {
            if (HotspotChangedEvent != null) HotspotChangedEvent(sender, mode);
        }
        public void ShowSelected(IShowProperty ToSelect)
        {
            propertyPage.SelectEntry(ToSelect as IPropertyEntry);
        }
        public void OpenSubEntries()
        {   // bei einem Objekt: die Darstellung des Objektes aufklappen,
            // bei mehreren Objekten: die 
            if (propertyPage == null) return; // kein ControlCenter
            if (showProperties != null && propertyPage != null)
            {
                if (selectedObjects.Count == 1)
                {
                    for (int i = 0; i < showProperties.Length; ++i)
                    {
                        propertyPage.OpenSubEntries(showProperties[i] as IPropertyEntry, true);
                    }
                }
                else
                {
                    for (int i = 0; i < showProperties.Length; ++i)
                    {
                        if (multiObjectsProperties != null && multiObjectsProperties.attributeProperties == showProperties[i])
                        {
                            propertyPage.OpenSubEntries(showProperties[i] as IPropertyEntry, true);
                        }
                    }

                }
            }
            propertyPage.SelectEntry(this);
        }
        #region IDisplayHotSpots Members

        public event CADability.HotspotChangedDelegate HotspotChangedEvent;
        /// <summary>
        /// Implements <see cref="CADability.IDisplayHotSpots.ReloadProperties ()"/>
        /// </summary>
		public void ReloadProperties()
        {
            if (showProperties != null)
            {
                for (int i = 0; i < showProperties.Length; ++i)
                {
                    IDisplayHotSpots dhsp = showProperties[i] as IDisplayHotSpots;
                    if (dhsp != null) dhsp.ReloadProperties();
                }
            }
        }

        #endregion
        #region Attribute für mehrere Objekte

        private void GeoObjectDidChange(CADability.GeoObject.IGeoObject Sender, CADability.GeoObject.GeoObjectChange Change)
        {
            if (Change.OnlyAttributeChanged && multiObjectsProperties != null && !multiObjectsProperties.isChangingMultipleAttributes)
            {
                multiObjectsProperties.InitMultiAttributeSelection();
            }
        }
        #endregion
        /// <summary>
        /// Will be called when a context menu of a sub entry has been generated. This implementation adds the standard menu entries for selected objects.
        /// </summary>
        /// <param name="sender">The ControlCenter entry that has its context menu created</param>
        /// <param name="toManipulate">The context menu, which may be manipulated</param>
        protected void OnCreateSubContextMenue(IGeoObjectShowProperty sender, List<MenuWithHandler> toManipulate)
        {
            ContextMenuSource = sender.GetGeoObject();
            toManipulate.Add(MenuWithHandler.Separator);
            toManipulate.AddRange(MenuResource.LoadMenuDefinition("MenuId.SelectedObject", false, Frame.CommandHandler));
        }

        internal void Focus(IGeoObject go)
        {
            for (int i = 0; i < SubItems.Length; i++)
            {
                if (SubItems[i] is IGeoObjectShowProperty)
                {
                    if ((SubItems[i] as IGeoObjectShowProperty).GetGeoObject() == go)
                    {
                        propertyPage.SelectEntry(SubItems[i]);
                        break;
                    }
                }
            }
        }
    }
}
