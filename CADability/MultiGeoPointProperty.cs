using System;


namespace CADability.UserInterface
{
    /// <summary>
    /// Interface for the treatment of a list or array of GeoPoints. This interface is used
    /// for the communication of user-interface objects (e.g. MultiGeoPointProperty) with
    /// various containers of GeoPoints.
    /// </summary>

    public interface IIndexedGeoPoint
    {
        /// <summary>
        /// Sets the GeoPoint with the given index
        /// </summary>
        /// <param name="Index">Index of the GeoPoint</param>
        /// <param name="ThePoint">Value to set</param>
        void SetGeoPoint(int Index, GeoPoint ThePoint);
        /// <summary>
        /// Yields the value of the GeoPoint with the given index.
        /// </summary>
        /// <param name="Index">Index of the GeoPoint</param>
        /// <returns>Value of the GeoPoint</returns>
        GeoPoint GetGeoPoint(int Index);
        /// <summary>
        /// Inserts a new GeoPoint before the given index. Index==-1: Append
        /// </summary>
        /// <param name="Index">Where to insert</param>
        /// <param name="ThePoint">Value to insert</param>
        void InsertGeoPoint(int Index, GeoPoint ThePoint);
        /// <summary>
        /// Removes the GeoPoint at the given Index.
        /// </summary>
        /// <param name="Index">Index of the point to be removed, -1: LastPoint</param>
        void RemoveGeoPoint(int Index);
        /// <summary>
        /// Yields the number of GeoPoints in the list or array.
        /// </summary>
        /// <returns>Number of GeoPoints</returns>
        int GetGeoPointCount();
        /// <summary>
        /// Asks, whether a point may be inserted before the given index.
        /// </summary>
        /// <param name="Index">Index where insertion is requested, -1: append</param>
        /// <returns></returns>
        bool MayInsert(int Index);
        /// <summary>
        /// Asks, whether the point with the given index may be deleted.
        /// </summary>
        /// <param name="Index">Index of the point</param>
        /// <returns></returns>
        bool MayDelete(int Index);
    }
    /// <summary>
    /// A <see cref="IShowProperty"/> implementation that displays a list of <see cref="GeoPoint"/>s.
    /// The communication with the object that owns that list is performed via a <see cref="IIndexedGeoPoint"/>
    /// interface, which must be provided in the constructor. This show property lets the user add
    /// and remove GeoPoints to or from the list or modify existing GeoPoints in the list.
    /// </summary>

    public class MultiGeoPointProperty : PropertyEntryImpl
    {
        private IIndexedGeoPoint controlledObject;
        private MenuWithHandler[] prependContextMenue;
        private bool displayZComponent;
        private IPropertyEntry[] subEntries; 
        /// <summary>
        /// Creates a MultiGeoPointProperty. The parameter "controlledObject" provides the owner
        /// of the list.
        /// </summary>
        /// <param name="controlledObject">owner of the list</param>
        /// <param name="resourceId">the resource id to specify a string from the StringTable.
        /// ResourceId+".Label": the Label left of the
        /// edit box. ResourceId+".ShortInfo": a short tooltip text ResourceId+"DetailedInfo":
        /// a longer tooltip text.
        /// </param>
        public MultiGeoPointProperty(IIndexedGeoPoint controlledObject, string resourceId, IFrame frame)
        {
            this.Frame = frame;
            this.resourceId = resourceId;
            this.controlledObject = controlledObject;
            prependContextMenue = null;
            MultipleChoiceSetting formattingZValue = Settings.GlobalSettings.GetValue("Formatting.Coordinate.ZValue") as MultipleChoiceSetting;
            if (formattingZValue != null && formattingZValue.CurrentSelection >= 0)
            {
                displayZComponent = formattingZValue.CurrentSelection == 0;
            }
            else
            {
                displayZComponent = true;
            }
        }
        public bool DisplayZComponent
        {
            get
            {
                return displayZComponent;
            }
            set
            {
                displayZComponent = value;
            }
        }
        public MenuWithHandler[] PrependContextMenue
        {
            get
            {
                return prependContextMenue;
            }
            set
            {
                prependContextMenue = value;
            }
        }
        /// <summary>
        /// Refreshes the display of the point with the given index.
        /// </summary>
        /// <param name="Index">index of point to refresh</param>
        public void Refresh(int Index)
        {
            if (subEntries != null && Index < subEntries.Length)
            {
                GeoPointProperty gpp = subEntries[Index] as GeoPointProperty;
                if (gpp != null) gpp.Refresh();
            }
        }
        public override void Refresh()
        {
            if (propertyPage == null) return;
            bool wasOpen = false;
            if (subEntries != null && propertyPage.IsOpen(this))
            {
                wasOpen = true;
                propertyPage.OpenSubEntries(this, false);
            }
            if (subEntries != null && controlledObject.GetGeoPointCount() != subEntries.Length)
            {
                for (int i = 0; i < subEntries.Length; i++)
                {
                    (subEntries[i] as IPropertyEntry).Removed(propertyPage);
                }
                subEntries = null;
                IShowProperty[] se = SubEntries;
                for (int i = 0; i < subEntries.Length; i++)
                {
                    (subEntries[i] as IPropertyEntry).Added(propertyPage);
                }
            }
            for (int i = 0; i < subEntries.Length; i++)
            {
                //subEntries[i].Refresh();
            }
            if (wasOpen)
            {
                propertyPage.OpenSubEntries(this, true);
            }
        }
        /// <summary>
        /// Appends a point to the list of points
        /// </summary>
        /// <param name="initialValue">initial value of the new point</param>
        public void Append(GeoPoint initialValue)
        {
            if (propertyPage.Selected != null && propertyPage.Selected is GeoPointProperty gpp) propertyPage.SelectEntry(this);
            subEntries = null; // damit werden diese ungültig
            controlledObject.InsertGeoPoint(-1, initialValue);
            if (propertyPage != null)
            {
                propertyPage.Refresh(this); // damit werden neue subEntries erzeugt
                propertyPage.OpenSubEntries(this, false);
                propertyPage.OpenSubEntries(this, true);
            }
        }
        //public bool MouseEnabled(int Index)
        //{
        //    GeoPointProperty gpp = subEntries[Index] as GeoPointProperty;
        //    return gpp.IsModifyingWithMouse;
        //}
        /// <summary>
        /// Opens the subentries in the treeview.
        /// </summary>
        /// <param name="open">true: open, false: close</param>
        public void ShowOpen(bool open)
        {
            if (propertyPage != null) propertyPage.OpenSubEntries(this, open);
        }
        public void SetFocusToIndex(int index)
        {
            if (propertyPage != null && subEntries != null && subEntries.Length > index)
            {
                if (propertyPage.IsOpen(this))
                {
                    propertyPage.SelectEntry(subEntries[index] as IPropertyEntry);
                }
            }
        }
        //public void EnableMouse(int Index)
        //{
        //    if (propertyPage!=null) 
        //    {
        //        GeoPointProperty gpp = subEntries[Index] as GeoPointProperty;
        //        gpp.SetMouseButton(MouseButtonMode.MouseActive);
        //    }
        //}
#region IShowPropertyImpl Overrides
        public override void Added(IPropertyPage propertyPage)
        {
            base.Added(propertyPage);
            IShowProperty[] sub = SubEntries;
            for (int i = 0; i < sub.Length; ++i)
            {
                GeoPointProperty gpp = sub[i] as GeoPointProperty;
                if (gpp != null)
                {
                    gpp.Refresh();
                }
            }
        }
        public override IPropertyEntry[] SubItems
        {
            get
            {
                if (subEntries == null)
                {
                    subEntries = new IPropertyEntry[controlledObject.GetGeoPointCount()];
                    for (int i = 0; i < subEntries.Length; ++i)
                    {
                        GeoPointProperty gpp = new GeoPointProperty(resourceId + ".Point", this.Frame, false);
                        gpp.UserData["Index"] = i;
                        gpp.SetGeoPointEvent += new CADability.UserInterface.GeoPointProperty.SetGeoPointDelegate(OnSetGeoPoint);
                        gpp.GetGeoPointEvent += new CADability.UserInterface.GeoPointProperty.GetGeoPointDelegate(OnGetGeoPoint);
                        //gpp.OnSpecialKeyDownEvent += new Condor.UserInterface.ManagedKeysTextbox.OnSpecialKeyDownDelegate(OnSpecialKeyDown);
                        gpp.ModifyWithMouseEvent += new ModifyWithMouseDelegate(ModifyWithMouse);
                        gpp.SelectionChangedEvent += new CADability.UserInterface.GeoPointProperty.SelectionChangedDelegate(OnPointSelectionChanged);
                        gpp.FilterCommandEvent += new CADability.UserInterface.GeoPointProperty.FilterCommandDelegate(OnFilterSinglePointCommand);
                        gpp.ContextMenuId = "MenuId.IndexedPoint";
                        gpp.PrependContextMenu = prependContextMenue; // zusätzliches Menue
                        gpp.DisplayZComponent = displayZComponent;
                        string lt = StringTable.GetString(resourceId + ".Point.Label");
                        if (lt.IndexOf("{0") >= 0)
                        {
                            try
                            {
                                gpp.LabelText = string.Format(lt, i + 1);
                            }
                            catch (FormatException) { } // geht halt nicht, Original bleibt
                        }
                        subEntries[i] = gpp;
                    }
                }
                return subEntries;

            }
        }
        public override PropertyEntryType Flags => PropertyEntryType.GroupTitle | PropertyEntryType.HasSubEntries;
        #endregion
        private void OnSetGeoPoint(GeoPointProperty sender, GeoPoint p)
        {
            int index = (int)sender.UserData["Index"];
            controlledObject.SetGeoPoint(index, p);
        }
        private GeoPoint OnGetGeoPoint(GeoPointProperty sender)
        {
            int index = (int)sender.UserData["Index"];
            if (index < controlledObject.GetGeoPointCount())
            {
                return controlledObject.GetGeoPoint(index);
            }
            else
            {	// hier sollte man ja nicht hinkommen. Allerdings gibt es Situationen,
                // bei denen der letzte Punkt entfernt wurde aber die Anzeige immer noch
                // einen Punkt mehr enthält. Deshalb liefern wir hier den letzten Punkt.
                if (controlledObject.GetGeoPointCount() > 0)
                {
                    return controlledObject.GetGeoPoint(controlledObject.GetGeoPointCount() - 1);
                }
                else
                {   // und das ist der Notausgang, kommt dran, wenn eine Aktion aktiviert wird
                    // und noch garkein Punkt in der Liste ist
                    return new GeoPoint();
                }
            }
        }
        /// <summary>
        /// Delegate definition for <see cref="ModifyWithMouseEvent"/>.
        /// </summary>
        /// <param name="sender">this object</param>
        /// <param name="index">index of the point that is modified</param>
        /// <returns>true: accepted, false: not accepted</returns>
        public delegate bool ModifyWithMouseIndexDelegate(IPropertyEntry sender, int index);
        /// <summary>
        /// Provide a method here if you need to notified about modification of any point
        /// in this list with the mouse
        /// </summary>
        public event ModifyWithMouseIndexDelegate ModifyWithMouseEvent;
        private void ModifyWithMouse(IPropertyEntry sender, bool StartModifying)
        {
            if (ModifyWithMouseEvent != null)
            {
                GeoPointProperty gpp = sender as GeoPointProperty;
                int index = (int)gpp.UserData["Index"];
                if (!ModifyWithMouseEvent(sender, index))
                {	// der Anwender will es nicht
                    //gpp.SetMouseButton(MouseButtonMode.MouseInactive);
                }
            }
        }
        /// <summary>
        /// Provide a method here if you need to be notified about when the selection
        /// of the points in the subtree changed
        /// </summary>
        public event GeoPointProperty.SelectionChangedDelegate GeoPointSelectionChangedEvent;
        private void OnPointSelectionChanged(GeoPointProperty sender, bool isSelected)
        {
            if (GeoPointSelectionChangedEvent != null) GeoPointSelectionChangedEvent(sender, isSelected);
        }
        private void OnFilterSinglePointCommand(GeoPointProperty sender, string menuId, CommandState commandState, ref bool handled)
        {
            int index = (int)sender.UserData["Index"];
            if (commandState != null)
            {	// es wird nur nach der Menuedarstellung gefragt
                switch (menuId)
                {
                    case "MenuId.IndexedPoint.InsertAfter":
                        if (index == controlledObject.GetGeoPointCount() - 1) index = -1;
                        else index += 1;
                        commandState.Enabled = controlledObject.MayInsert(index);
                        handled = true;
                        break;
                    case "MenuId.IndexedPoint.InsertBefore":
                        commandState.Enabled = controlledObject.MayInsert(index);
                        handled = true;
                        break;
                    case "MenuId.IndexedPoint.Delete":
                        commandState.Enabled = controlledObject.MayDelete(index);
                        handled = true;
                        break;
                }
            }
            else
            {
                try
                {
                    GeoPoint p = controlledObject.GetGeoPoint(index);
                    switch (menuId)
                    {
                        case "MenuId.IndexedPoint.InsertAfter":
                            if (index == controlledObject.GetGeoPointCount() - 1) index = -1;
                            else index += 1;
                            if (GetInsertionPointEvent != null)
                                p = GetInsertionPointEvent(this, index, true);
                            else if (index > 0)
                                p = new GeoPoint(controlledObject.GetGeoPoint(index - 1), controlledObject.GetGeoPoint(index));
                            controlledObject.InsertGeoPoint(index, p);
                            handled = true;
                            break;
                        case "MenuId.IndexedPoint.InsertBefore":
                            if (GetInsertionPointEvent != null)
                                p = GetInsertionPointEvent(this, index, false);
                            else if (index > 0)
                                p = new GeoPoint(controlledObject.GetGeoPoint(index - 1), controlledObject.GetGeoPoint(index));
                            controlledObject.InsertGeoPoint(index, p);
                            handled = true;
                            break;
                        case "MenuId.IndexedPoint.Delete":
                            controlledObject.RemoveGeoPoint(index);
                            handled = true;
                            break;
                        case "MenuId.Point.NameVariable":
                            if (Frame != null)
                            {
                                Frame.Project.SetNamedValue(null, p);
                                handled = true;
                            }
                            break;
                    }
                }
                catch (IndexOutOfRangeException)
                {
                }
            }
        }
        /// <summary>
        /// Delegate definition for <see cref="GetInsertionPointEvent"/>
        /// </summary>
        /// <param name="sender">This property</param>
        /// <param name="index">Where to insert</param>
        /// <param name="after">true: insert after this index, false: insert before this index</param>
        /// <returns>The new point to be inserted</returns>
        public delegate GeoPoint GetInsertionPointDelegate(IPropertyEntry sender, int index, bool after);
        /// <summary>
        /// When a point is about to be inserted this property needs some initial value.
        /// The default initial value is the same point as the first/last point, when inserted before the first
        /// or after the last point, and the middle point of the interval where the point is to be inserted.
        /// If you wish another behavior add a handler to this event and return the appropriate point.
        /// </summary>
        public GetInsertionPointDelegate GetInsertionPointEvent;

    }
}
