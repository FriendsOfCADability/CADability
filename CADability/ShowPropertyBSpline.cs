using CADability.Actions;
using CADability.GeoObject;
using System.Collections.Generic;


namespace CADability.UserInterface
{
    /// <summary>
    /// Shows the properties of the BSpline.
    /// </summary>

    public class ShowPropertyBSpline : PropertyEntryImpl, IDisplayHotSpots, ICommandHandler, IGeoObjectShowProperty
    {
        private BSpline bSpline;
        private IPropertyEntry[] attributeProperties; // Anzeigen für die Attribute (Ebene, Farbe u.s.w)
        private IPropertyEntry[] subEntries;
        private MultiGeoPointProperty polesProperty;
        private MultiGeoPointProperty throughPointsProperty;
        private BooleanProperty closedProperty;
        private class PolesIndexedGeoPoint : IIndexedGeoPoint
        {
            ShowPropertyBSpline showPropertyBSpline; // nach außen
            public PolesIndexedGeoPoint(ShowPropertyBSpline showPropertyBSpline)
            {
                this.showPropertyBSpline = showPropertyBSpline;
            }
            #region IIndexedGeoPoint Members
            public void SetGeoPoint(int Index, GeoPoint ThePoint)
            {
                showPropertyBSpline.bSpline.SetPole(Index, ThePoint);
                // showPropertyBSpline.ReloadProperties();
            }
            public GeoPoint GetGeoPoint(int Index)
            {
                return showPropertyBSpline.bSpline.GetPole(Index);
            }
            public void InsertGeoPoint(int Index, GeoPoint ThePoint)
            {
                if (showPropertyBSpline.throughPointsProperty != null && showPropertyBSpline.propertyPage != null) showPropertyBSpline.propertyPage.OpenSubEntries(showPropertyBSpline.throughPointsProperty, false);
                showPropertyBSpline.bSpline.InsertPole(Index, true);
                showPropertyBSpline.polesProperty.Refresh();
                showPropertyBSpline.ReloadProperties();
                showPropertyBSpline.polesProperty.SubEntries[Index].SetFocus();
            }
            public void RemoveGeoPoint(int Index)
            {
                // TODO:  Add PolesIndexedGeoPoint.RemoveGeoPoint implementation
                showPropertyBSpline.ReloadProperties();
            }
            public int GetGeoPointCount()
            {
                // TODO:  Add PolesIndexedGeoPoint.GetGeoPointCount implementation
                return showPropertyBSpline.bSpline.PoleCount;
            }
            bool IIndexedGeoPoint.MayInsert(int Index)
            {   // einen Pol einfügen oder löschen hätte ja auch einfluss auf
                // die Knoten. Man müsste im BSpline "CurveKnotIns" verwenden..
                // man müsste wissen, auf welchen Parameterwert sich der Index bezieht ...
                return Index > 0;
            }
            bool IIndexedGeoPoint.MayDelete(int Index)
            {
                return false;
            }
            #endregion
        }
        private class ThroughPointsIndexedGeoPoint : IIndexedGeoPoint
        {
            ShowPropertyBSpline showPropertyBSpline; // nach außen
            public ThroughPointsIndexedGeoPoint(ShowPropertyBSpline showPropertyBSpline)
            {
                this.showPropertyBSpline = showPropertyBSpline;
            }
            #region IIndexedGeoPoint Members
            public void SetGeoPoint(int Index, GeoPoint ThePoint)
            {
                showPropertyBSpline.bSpline.SetThroughPoint(Index, ThePoint);
            }
            public GeoPoint GetGeoPoint(int Index)
            {
                return showPropertyBSpline.bSpline.GetThroughPoint(Index);
            }
            public void InsertGeoPoint(int Index, GeoPoint ThePoint)
            {
                List<GeoPoint> tp = new List<GeoPoint>(showPropertyBSpline.bSpline.ThroughPoint);
                tp.Insert(Index, ThePoint);
                int deg = showPropertyBSpline.bSpline.degree;
                bool closed = showPropertyBSpline.bSpline.IsClosed;
                showPropertyBSpline.bSpline.ThroughPoints(tp.ToArray(), deg, closed);
                showPropertyBSpline.throughPointsProperty.Refresh();
                showPropertyBSpline.throughPointsProperty.SubEntries[Index].SetFocus();
            }
            public void RemoveGeoPoint(int Index)
            {
                List<GeoPoint> tp = new List<GeoPoint>(showPropertyBSpline.bSpline.ThroughPoint);
                tp.RemoveAt(Index);
                int deg = showPropertyBSpline.bSpline.degree;
                bool closed = showPropertyBSpline.bSpline.IsClosed;
                showPropertyBSpline.bSpline.ThroughPoints(tp.ToArray(), deg, closed);
                showPropertyBSpline.throughPointsProperty.Refresh();
            }
            public int GetGeoPointCount()
            {
                return showPropertyBSpline.bSpline.ThroughPointCount;
            }
            bool IIndexedGeoPoint.MayInsert(int Index)
            {
                if (Index == 0 || Index == -1) return false;
                return true;
            }
            bool IIndexedGeoPoint.MayDelete(int Index)
            {
                return showPropertyBSpline.bSpline.ThroughPointCount > 2;
            }

            #endregion
        }
        public ShowPropertyBSpline(BSpline bSpline, IFrame frame): base(frame)
        {
            this.bSpline = bSpline;

            InitSubEntries();
            base.resourceIdInternal = "BSpline.Object";
        }
        private void InitSubEntries()
        {
            subEntries = null; // damit nicht noch die alten verwendet werden
            if (polesProperty != null)
            {
                polesProperty.ModifyWithMouseEvent -= new CADability.UserInterface.MultiGeoPointProperty.ModifyWithMouseIndexDelegate(OnModifyPolesWithMouse);
                polesProperty.GeoPointSelectionChangedEvent -= new CADability.UserInterface.GeoPointProperty.SelectionChangedDelegate(OnPointsSelectionChanged);
                polesProperty.PropertyEntryChangedStateEvent-= new PropertyEntryChangedStateDelegate(OnPolesPropertyStateChanged);
            }
            polesProperty = new MultiGeoPointProperty(new PolesIndexedGeoPoint(this), "BSpline.Poles", this.Frame);
            polesProperty.ModifyWithMouseEvent += new CADability.UserInterface.MultiGeoPointProperty.ModifyWithMouseIndexDelegate(OnModifyPolesWithMouse);
            polesProperty.GeoPointSelectionChangedEvent += new CADability.UserInterface.GeoPointProperty.SelectionChangedDelegate(OnPointsSelectionChanged);
            polesProperty.PropertyEntryChangedStateEvent += new PropertyEntryChangedStateDelegate(OnPolesPropertyStateChanged);

            if (bSpline.ThroughPoints3dExist)
            {
                if (throughPointsProperty != null)
                {	// es nach einem Refresh wird er neu gemacht, der alte muss die Events hergeben
                    throughPointsProperty.ModifyWithMouseEvent -= new CADability.UserInterface.MultiGeoPointProperty.ModifyWithMouseIndexDelegate(OnModifyThroughPointsWithMouse);
                    throughPointsProperty.GeoPointSelectionChangedEvent -= new CADability.UserInterface.GeoPointProperty.SelectionChangedDelegate(OnPointsSelectionChanged);
                    throughPointsProperty.PropertyEntryChangedStateEvent -= new PropertyEntryChangedStateDelegate(OnThroughPointsPropertyStateChanged);
                }
                throughPointsProperty = new MultiGeoPointProperty(new ThroughPointsIndexedGeoPoint(this), "BSpline.ThroughPoints", this.Frame);
                throughPointsProperty.ModifyWithMouseEvent += new CADability.UserInterface.MultiGeoPointProperty.ModifyWithMouseIndexDelegate(OnModifyThroughPointsWithMouse);
                throughPointsProperty.GeoPointSelectionChangedEvent += new CADability.UserInterface.GeoPointProperty.SelectionChangedDelegate(OnPointsSelectionChanged);
                throughPointsProperty.PropertyEntryChangedStateEvent += new PropertyEntryChangedStateDelegate(OnThroughPointsPropertyStateChanged);
                throughPointsProperty.GetInsertionPointEvent += new MultiGeoPointProperty.GetInsertionPointDelegate(OnThroughPointsGetInsertionPoint);
            }
            else
            {
                throughPointsProperty = null;
            }

            if (closedProperty != null)
            {
                closedProperty.GetBooleanEvent -= new CADability.UserInterface.BooleanProperty.GetBooleanDelegate(OnGetClosed);
                closedProperty.SetBooleanEvent -= new CADability.UserInterface.BooleanProperty.SetBooleanDelegate(OnSetClosed);
            }
            closedProperty = new BooleanProperty("Constr.BSpline.Mode", "Constr.BSpline.Mode.Values");
            closedProperty.GetBooleanEvent += new CADability.UserInterface.BooleanProperty.GetBooleanDelegate(OnGetClosed);
            closedProperty.SetBooleanEvent += new CADability.UserInterface.BooleanProperty.SetBooleanDelegate(OnSetClosed);
            closedProperty.BooleanValue = bSpline.IsClosed;
            attributeProperties = bSpline.GetAttributeProperties(Frame);
        }
        #region PropertyEntryImpl Overrides

        public override IPropertyEntry[] SubItems
        {
            get
            {
                if (subEntries == null)
                {
                    IPropertyEntry[] mainProperties;
                    if (throughPointsProperty != null)
                    {
                        mainProperties = new IPropertyEntry[] { polesProperty, throughPointsProperty, closedProperty };
                    }
                    else
                    {
                        mainProperties = new IPropertyEntry[] { polesProperty, closedProperty };
                    }
                    subEntries = PropertyEntryImpl.Concat(mainProperties, attributeProperties);
                }
                return subEntries;
            }
        }
        public override PropertyEntryType Flags => PropertyEntryType.ContextMenu | PropertyEntryType.Selectable | PropertyEntryType.GroupTitle | PropertyEntryType.HasSubEntries;
        public override MenuWithHandler[] ContextMenu
        {
            get
            {
                List<MenuWithHandler> items = new List<MenuWithHandler>(MenuResource.LoadMenuDefinition("MenuId.Object.Spline", false, this));
                CreateContextMenueEvent?.Invoke(this, items);
                bSpline.GetAdditionalContextMenue(this, Frame, items);
                return items.ToArray();
            }
        }
        public override void Added(IPropertyPage propertyPage)
        {	// die events müssen in Added angemeldet und in Removed wieder abgemeldet werden,
            bSpline.DidChangeEvent += new ChangeDelegate(OnGeoObjectDidChange);
            bSpline.UserData.UserDataAddedEvent += new UserData.UserDataAddedDelegate(OnUserDataAdded);
            bSpline.UserData.UserDataRemovedEvent += new UserData.UserDataRemovedDelegate(OnUserDataAdded);
            base.Added(propertyPage);
        }
        void OnUserDataAdded(string name, object value)
        {
            this.subEntries = null;
            attributeProperties = bSpline.GetAttributeProperties(Frame);
            propertyPage.Refresh(this);
        }
        public override void Removed(IPropertyPage propertyPage)
        {
            bSpline.DidChangeEvent -= new ChangeDelegate(OnGeoObjectDidChange);
            bSpline.UserData.UserDataAddedEvent -= new UserData.UserDataAddedDelegate(OnUserDataAdded);
            bSpline.UserData.UserDataRemovedEvent -= new UserData.UserDataRemovedDelegate(OnUserDataAdded);
            base.Removed(propertyPage);
        }
        private void OnGeoObjectDidChange(IGeoObject Sender, GeoObjectChange Change)
        {	// wird bei Änderungen von der Linie aufgerufen, Abgleich der Anzeigen
            if (Change.MethodOrPropertyName == "ModifyInverse")
            {   // während der Selektion kann das Objekt per DragAndDrop verschoben werden. Dann kommen wir hier hin und updaten alles.
                polesProperty.Refresh();
                if (throughPointsProperty != null && !bSpline.ThroughPoints3dExist) throughPointsProperty.Refresh();
            }
        }
        //public override void ChildSelected(IPropertyEntry theSelectedChild)
        //{
        //    base.ChildSelected(theSelectedChild);
        //    if (throughPointsProperty != null && !bSpline.ThroughPoints3dExist)
        //    {   // wenn man poles editiert, dann wird throupoints ungültig
        //        // hier werden die subentries neu berechnet, die poles wieder aufgemacht und der richtige Eintrag selektiert
        //        bool isopen = propertyPage.IsOpen(polesProperty);
        //        IPropertyEntry cs = propertyPage.GetCurrentSelection();
        //        int selindex = -1;
        //        if (cs is GeoPointProperty)
        //        {
        //            if ((cs as GeoPointProperty).UserData.Contains("Index"))
        //            {
        //                selindex = (int)(cs as GeoPointProperty).UserData.GetData("Index");
        //            }
        //        }
        //        InitSubEntries();
        //        propertyPage.Refresh(this);
        //        if (isopen)
        //        {
        //            propertyPage.OpenSubEntries(polesProperty, true);
        //            if (selindex >= 0 && polesProperty.SubEntries != null && selindex < polesProperty.SubEntriesCount)
        //            {
        //                propertyPage.SelectEntry(polesProperty.SubEntries[selindex] as IPropertyEntry);
        //            }
        //        }
        //    }
        //}
        #endregion
        private GeoPoint OnThroughPointsGetInsertionPoint(IPropertyEntry sender, int index, bool after)
        {
            if (index == 0 && !after)
            {
                return bSpline.ThroughPoint[0];
            }
            else if (index == -1 && after)
            {
                return bSpline.ThroughPoint[bSpline.ThroughPoint.Length - 1];
            }
            else
            {
                double par;
                if (after)
                {
                    par = (bSpline.ThroughPointsParam[index - 1] + bSpline.ThroughPointsParam[index]) / 2.0;
                }
                else
                {
                    par = (bSpline.ThroughPointsParam[index - 1] + bSpline.ThroughPointsParam[index]) / 2.0;
                }
                return bSpline.PointAtParam(par);
            }
        }
        private bool OnModifyPolesWithMouse(IPropertyEntry sender, int index)
        {
            GeneralGeoPointAction gpa = new GeneralGeoPointAction(bSpline.GetPole(index), bSpline);
            gpa.UserData.Add("Mode", "Pole");
            gpa.UserData.Add("Index", index);
            gpa.GeoPointProperty = sender as GeoPointProperty;
            gpa.SetGeoPointEvent += new CADability.Actions.GeneralGeoPointAction.SetGeoPointDelegate(OnSetPole);
            Frame.SetAction(gpa);
            return false;
        }
        private bool OnModifyThroughPointsWithMouse(IPropertyEntry sender, int index)
        {
            GeneralGeoPointAction gpa = new GeneralGeoPointAction(bSpline.GetThroughPoint(index), bSpline);
            gpa.UserData.Add("Mode", "ThroughPoint");
            gpa.UserData.Add("Index", index);
            gpa.GeoPointProperty = sender as GeoPointProperty;
            gpa.SetGeoPointEvent += new CADability.Actions.GeneralGeoPointAction.SetGeoPointDelegate(OnSetThroughPoint);
            Frame.SetAction(gpa);
            return false;
        }

        private void OnSetPole(GeneralGeoPointAction sender, GeoPoint NewValue)
        {
            if (throughPointsProperty != null && propertyPage != null) propertyPage.OpenSubEntries(throughPointsProperty, false);
            int Index = (int)sender.UserData.GetData("Index");
            bSpline.SetPole(Index, NewValue);
        }

        private void OnSetThroughPoint(GeneralGeoPointAction sender, GeoPoint NewValue)
        {
            int Index = (int)sender.UserData.GetData("Index");
            bSpline.SetThroughPoint(Index, NewValue);
        }


        #region IDisplayHotSpots Members

        public event CADability.HotspotChangedDelegate HotspotChangedEvent;
        public void ReloadProperties()
        {
            // es kann sein, dass hier die ThroughPoints nicht mehr gelten, obwohl sie
            // vorher gegolten haben...
            bool IsPole = false;
            bool IsThroughPoint = false;
            int currentPolesIndex = -1;
            int currentThroughIndex = -1;
            // hier feststellen, welche Anzeige gerade selektiert ist,
            // um den gleichen Zustand wiederherzustellen
            PropertyEntryImpl current = propertyPage.GetCurrentSelection() as PropertyEntryImpl;
            if (current != null)
            {
                PropertyEntryImpl currentParent = propertyPage.GetParent(current) as PropertyEntryImpl;
                //if (currentParent == polesProperty) IsPole = true;
                //if (currentParent == throughPointsProperty) IsThroughPoint = true;
                IUserData ud = current as IUserData;
                if (ud != null && ud.UserData.ContainsData("Index"))
                {
                    if (current.ResourceId.Contains("ThroughPoints"))
                        currentThroughIndex = (int)ud.UserData.GetData("Index");
                    else
                        currentPolesIndex = (int)ud.UserData.GetData("Index");
                }
            }
            IsPole = propertyPage.IsOpen(polesProperty);
            IsThroughPoint = throughPointsProperty != null && propertyPage.IsOpen(throughPointsProperty);
            // Zumachen, damit die Hotspots weggehen
            if (polesProperty != null) propertyPage.OpenSubEntries(polesProperty, false);
            if (throughPointsProperty != null) propertyPage.OpenSubEntries(throughPointsProperty, false);
            InitSubEntries();
            propertyPage.Refresh(this);
            if (IsPole)
            {
                propertyPage.OpenSubEntries(polesProperty, true);
                if (currentPolesIndex >= 0) propertyPage.SelectEntry(polesProperty.SubEntries[currentPolesIndex] as IPropertyEntry);
            }
            if (IsThroughPoint && throughPointsProperty != null)
            {
                propertyPage.OpenSubEntries(throughPointsProperty, true);
                if (currentThroughIndex >= 0) propertyPage.SelectEntry(throughPointsProperty.SubEntries[currentThroughIndex] as IPropertyEntry);
            }
        }

        #endregion

        private void OnPointsSelectionChanged(GeoPointProperty sender, bool isSelected)
        {
            if (HotspotChangedEvent != null)
            {
                if (isSelected) HotspotChangedEvent(sender, HotspotChangeMode.Selected);
                else HotspotChangedEvent(sender, HotspotChangeMode.Deselected);
            }
        }

        private void OnPolesPropertyStateChanged(IPropertyEntry sender, StateChangedArgs args)
        {
            if (HotspotChangedEvent != null)
            {
                if (args.EventState == StateChangedArgs.State.OpenSubEntries)
                {
                    for (int i = 0; i < polesProperty.SubEntries.Length; ++i)
                    {
                        IHotSpot hsp = polesProperty.SubEntries[i] as IHotSpot;
                        if (hsp != null) HotspotChangedEvent(hsp, HotspotChangeMode.Visible);
                    }
                }
                else if (args.EventState == StateChangedArgs.State.CollapseSubEntries)
                {
                    for (int i = 0; i < polesProperty.SubEntries.Length; ++i)
                    {
                        IHotSpot hsp = polesProperty.SubEntries[i] as IHotSpot;
                        if (hsp != null) HotspotChangedEvent(hsp, HotspotChangeMode.Invisible);
                    }
                }
            }
        }

        private void OnThroughPointsPropertyStateChanged(IPropertyEntry sender, StateChangedArgs args)
        {
            if (HotspotChangedEvent != null)
            {
                if (args.EventState == StateChangedArgs.State.OpenSubEntries)
                {
                    for (int i = 0; i < throughPointsProperty.SubEntries.Length; ++i)
                    {
                        IHotSpot hsp = throughPointsProperty.SubEntries[i] as IHotSpot;
                        if (hsp != null) HotspotChangedEvent(hsp, HotspotChangeMode.Visible);
                    }
                }
                else if (args.EventState == StateChangedArgs.State.CollapseSubEntries)
                {
                    for (int i = 0; i < throughPointsProperty.SubEntries.Length; ++i)
                    {
                        IHotSpot hsp = throughPointsProperty.SubEntries[i] as IHotSpot;
                        if (hsp != null) HotspotChangedEvent(hsp, HotspotChangeMode.Invisible);
                    }
                }
            }
        }

        private bool OnGetClosed()
        {
            return bSpline.IsClosed;
        }

        private void OnSetClosed(bool val)
        {
            bSpline.IsClosed = val;
        }
        #region ICommandHandler Members

        virtual public bool OnCommand(string MenuId)
        {
            switch (MenuId)
            {
                case "MenuId.Reverse":
                    (bSpline as ICurve).Reverse();
                    ReloadProperties();
                    //polesProperty.ShowOpen(false);
                    //if (throughPointsProperty!=null) throughPointsProperty.ShowOpen(false);
                    return true;
                case "MenuId.CurveSplit":
                    Frame.SetAction(new ConstrSplitCurve(bSpline));
                    return true;
                case "MenuId.Approximate":
                    if (Frame.ActiveAction is SelectObjectsAction)
                    {
                        Curves.Approximate(Frame, bSpline);
                    }
                    return true;
            }
            return false;
        }

        virtual public bool OnUpdateCommand(string MenuId, CommandState CommandState)
        {
            switch (MenuId)
            {
                case "MenuId.Reverse":
                    CommandState.Enabled = true; // naja isses ja immer
                    return true;
                case "MenuId.Approximate":
                    CommandState.Enabled = (Frame.ActiveAction is SelectObjectsAction);
                    return true;
            }
            return false;
        }
        void ICommandHandler.OnSelected(MenuWithHandler selectedMenuItem, bool selected) { }

        #endregion
        #region IGeoObjectShowProperty Members
        public event CADability.GeoObject.CreateContextMenueDelegate CreateContextMenueEvent;
        IGeoObject IGeoObjectShowProperty.GetGeoObject()
        {
            return bSpline;
        }
        string IGeoObjectShowProperty.GetContextMenuId()
        {
            return "MenuId.Object.Spline";
        }
        #endregion

    }
}
