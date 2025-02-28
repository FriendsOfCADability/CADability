using CADability.GeoObject;
using CADability.UserInterface;
using System;
using System.Collections;
using System.Collections.Generic;
#if WEBASSEMBLY
using CADability.WebDrawing;
using Point = CADability.WebDrawing.Point;
#else
using System.Drawing;
using Point = System.Drawing.Point;
#endif
using System.Runtime.Serialization;


namespace CADability.Attribute
{
    /// <summary>
    /// List of <see cref="HatchStyle"/> derived objects. Typically a <see cref="Project"/>
    /// or the <see cref="Settings"/> maintain such a list.
    /// The list is serializable and can be shown and modified in the <see cref="ControlCenter"/>.
    /// There can not be two hatch-styles with the same name.
    /// </summary>
    [Serializable]
    public class HatchStyleList : PropertyEntryImpl, ISerializable, ICommandHandler, IAttributeList, IDeserializationCallback
    {
        private HatchStyle[] unsortedEntries;
        private SortedList entries;
        private HatchStyle current;
        private IAttributeListContainer owner; // the owner of this list, either the project or the settings
        internal string menuResourceId;
        private bool needsUpdate;

        IPropertyEntry[] showProperties;

        public delegate HatchStyleList CreateHatchStyleListDelegate();
        public static event CreateHatchStyleListDelegate CreateHatchStyleListEvent;
        public static HatchStyleList CreateHatchStyleList()
        {
            if (CreateHatchStyleListEvent != null) return CreateHatchStyleListEvent();
            return new HatchStyleList();
        }
        public HatchStyleList()
        {
            entries = new SortedList();
            resourceIdInternal = "HatchStyleList";
            needsUpdate = false;
        }

        /// <summary>
        /// </summary>
        /// <returns></returns>
        public static HatchStyleList GetDefault(IAttributeListContainer container)
        {
            HatchStyleList res = new HatchStyleList();
            HatchStyleSolid sol = new HatchStyleSolid();
            sol.Name = StringTable.GetString("HatchStyleList.DefaultSolid");
            sol.Color = container.ColorList.Current;
            res.Add(sol);
            HatchStyleLines lin = new HatchStyleLines();
            lin.Name = StringTable.GetString("HatchStyleList.DefaultLines");
            lin.LineDistance = 10.0;
            lin.LineAngle = new Angle(1.0, 1.0); // 45°
            lin.ColorDef = container.ColorList.Current;
            lin.LineWidth = container.LineWidthList.Current;
            lin.LinePattern = container.LinePatternList.Current;
            res.Add(lin);
            return res;
        }

        /// <summary>
        /// Adds an <see cref="HatchStyle"/> to the list. If there is already an hatchstyle
        /// with that name, an <see cref="NameAlreadyExistsException"/> exception will be thrown.
        /// </summary>
        /// <param name="ToAdd">The hatchstyle to add</param>
        public void Add(HatchStyle ToAdd)
        {
            if (ToAdd.Name == null) return;
            if (entries.Contains(ToAdd.Name))
            {
                throw new NameAlreadyExistsException(this, ToAdd, ToAdd.Name);
            }
            entries.Add(ToAdd.Name, ToAdd);
            ToAdd.Parent = this;
            if (entries.Count == 1)
                current = this[0];
            if (DidModifyEvent != null) DidModifyEvent(this, null); // ist das ok? der Modelview brauchts
            showProperties = null; // damit bei einem Refresh auch neu generiert wird
            this.Refresh();
        }
        /// <summary>
        /// Returns the hatchstyle with the given name from the list.
        /// If there is no such hatchstyle null will be returned.
        /// </summary>
        /// <param name="Name">Name of the hatchstyle</param>
        /// <returns>The hatchstyle found or null</returns>
        public HatchStyle Find(string Name)
        {
            if (Name == null) return null;
            int ind = entries.IndexOfKey(Name);
            if (ind >= 0) return entries.GetByIndex(ind) as HatchStyle;
            else return null;
        }
        public HatchStyle this[int Index]
        {
            get
            {
                if (Index < 0 || Index >= entries.Count) return null;
                return entries.GetByIndex(Index) as HatchStyle;
            }
        }
        /// <summary>
        /// Gets the number of hatchstyles in the list.
        /// </summary>
        public int Count
        {
            get { return entries.Count; }
        }
        /// <summary>
        /// Removes the given <see cref="HatchStyle"/> from the list. No action is taken
        /// if the hatchstyle is not in the list.
        /// </summary>
        /// <param name="ToRemove">The hatchstyle to remove</param>
        public void Remove(HatchStyle ToRemove)
        {
            if (entries.Contains(ToRemove.Name))
            {
                if (owner != null)
                {
                    if (!owner.RemovingItem(this, ToRemove, "HatchStyleList")) return;
                }
                entries.Remove(ToRemove.Name);
                showProperties = null; // damit bei einem Refresh auch neu generiert wird
                this.Refresh();
            }
            if (DidModifyEvent != null) DidModifyEvent(this, null); // ist das ok? der Modelview brauchts
        }
        private void RemoveAll() // only use internally, RemovingItem will not be called
        {
            entries.Clear();
            showProperties = null;
            if (propertyPage != null) propertyPage.OpenSubEntries(this, false); // close
        }
        public HatchStyleList Clone()
        {
            HatchStyleList res = new HatchStyleList();
            foreach (DictionaryEntry de in entries)
            {
                HatchStyle cloned = (de.Value as HatchStyle).Clone();
                res.Add(cloned);
                if ((de.Value as HatchStyle) == current)
                {
                    res.current = cloned;
                }
            }
            return res;
        }
        public HatchStyle Current
        {
            get
            {
                if (current == null && entries.Count > 0)
                {
                    current = entries.GetByIndex(0) as HatchStyle;
                }
                return current;
            }
            set
            {
                if (value != null)
                {
                    current = value;
                    if (!entries.ContainsKey(value.Name))
                        Add(value);
                }
            }
        }
        public IAttributeListContainer Owner
        {
            get { return owner; }
            set { owner = value; }
        }
        public event RemovingFromListDelegate RemovingFromListEvent;
        public event CADability.DidModifyDelegate DidModifyEvent;
        #region IPropertyEntry Members
        public override PropertyEntryType Flags
        {
            get
            {
                return PropertyEntryType.ContextMenu | PropertyEntryType.Selectable | PropertyEntryType.GroupTitle | PropertyEntryType.HasSubEntries;
            }
        }
        public override IPropertyEntry[] SubItems
        {
            get
            {
                if (showProperties == null)
                {
                    List<IPropertyEntry> list = new List<IPropertyEntry>();
                    for (int i = 0; i < entries.Count; i++)
                    {
                        HatchStyle hst = entries.GetByIndex(i) as HatchStyle;
                        // HatchStyles that end with "[DimensionStyleFillSolid]" are automatically created HatchStyles by DimensionStyles
                        if (!hst.Name.EndsWith("[DimensionStyleFillSolid]")) list.Add(hst);
                        showProperties = list.ToArray();
                    }
                }
                return showProperties;
            }
        }
        public override MenuWithHandler[] ContextMenu
        {
            get
            {
                if (menuResourceId != null)
                    return MenuResource.LoadMenuDefinition(menuResourceId, false, this);
                return MenuResource.LoadMenuDefinition("MenuId.HatchStyleList", false, this);
            }
        }
        public override void Refresh()
        {
            if (propertyPage != null) propertyPage.Refresh(this);
        }
        public override void UnSelected(IPropertyEntry nowSelected)
        {
            if (needsUpdate && Settings.GlobalSettings.GetBoolValue("HatchStyle.AutoUpdate", true)) OnUpdateAllHatchs();
            needsUpdate = false;
        }
        #endregion
        #region ISerializable Members
        /// <summary>
        /// Constructor required by deserialization
        /// </summary>
        /// <param name="info">SerializationInfo</param>
        /// <param name="context">StreamingContext</param>
        protected HatchStyleList(SerializationInfo info, StreamingContext context)
        {
            try
            {
                unsortedEntries = info.GetValue("UnsortedEntries", typeof(HatchStyle[])) as HatchStyle[];
            }
            catch (SerializationException)
            {   // alte Dateien beinhalten entries direkt
                entries = (SortedList)info.GetValue("Entries", typeof(SortedList));
            }
            current = (HatchStyle)InfoReader.Read(info, "Current", typeof(HatchStyle));
            resourceIdInternal = "HatchStyleList";
        }
        void ISerializable.GetObjectData(SerializationInfo info, StreamingContext context)
        {
            // info.AddValue("Entries",entries);
            // SortedList abspeichern macht u.U. Probleme bei Versionswechsel von .NET
            // deshalb jetzt nur Array abspeichern (23.9.2011)
            HatchStyle[] hatchStyleArray = new HatchStyle[entries.Count];
            for (int i = 0; i < entries.Count; i++)
            {
                hatchStyleArray[i] = (HatchStyle)entries.GetByIndex(i);
            }
            info.AddValue("UnsortedEntries", hatchStyleArray);
            info.AddValue("Current", current);
        }
        #endregion
        #region IDeserializationCallback Members
        void IDeserializationCallback.OnDeserialization(object sender)
        {
            if (unsortedEntries != null)
            {
                entries = new SortedList();
                for (int i = 0; i < unsortedEntries.Length; i++)
                {
                    entries.Add(unsortedEntries[i].Name, unsortedEntries[i]);
                }
                unsortedEntries = null;
            }
            foreach (DictionaryEntry de in entries)
            {
                (de.Value as HatchStyle).Parent = this;
            }
            if (current != null)
            {
                int ind = entries.IndexOfKey(current.Name);
                if (ind >= 0) current = entries.GetByIndex(ind) as HatchStyle;
            }
        }
        #endregion
        #region ICommandHandler Members
        private void OnAddFromGlobal()
        {
            foreach (HatchStyle ds in Settings.GlobalSettings.HatchStyleList.entries.Values)
            {
                if (!entries.ContainsKey(ds.Name))
                {
                    this.Add(ds.Clone());
                }
            }
        }
        private void OnMakeGlobal()
        {
            Settings.GlobalSettings.HatchStyleList.RemoveAll();
            foreach (HatchStyle ds in entries.Values)
            {
                Settings.GlobalSettings.HatchStyleList.Add(ds.Clone());
            }
        }
        private void OnUpdateFromProject()
        {
            Hashtable used = GetAllHatchStyles();
            foreach (HatchStyle ds in used.Keys)
            {
                if (!entries.ContainsKey(ds.Name))
                {
                    this.Add(ds);
                }
            }
        }
        private void OnRemoveUnused()
        {
            Hashtable used = GetAllHatchStyles();
            bool found = false;
            do
            {
                found = false;
                if (entries.Count > 1)
                {
                    foreach (HatchStyle ds in entries.Values)
                    {
                        if (!used.ContainsKey(ds))
                        {
                            Remove(ds);
                            found = true;
                            break;
                        }
                    }
                }
            } while (found);
        }
        private void GetAllHatchStyles(Hashtable collect, IGeoObject go)
        {
            if (go is Hatch)
            {
                Hatch dim = go as Hatch;
                HatchStyle dimst = dim.HatchStyle;
                collect[dimst] = null; // Hastable wird als set verwendet
            }
            else if (go is Block)
            {
                foreach (IGeoObject child in (go as Block).Children)
                    GetAllHatchStyles(collect, child);
            }
        }
        private Hashtable GetAllHatchStyles()
        {
            Hashtable res = new Hashtable();
            foreach (Model m in Frame.Project)
            {
                foreach (IGeoObject go in m) GetAllHatchStyles(res, go);
            }
            return res;
        }
        private void UpdateAllHatchs(IGeoObject go)
        {
            if (go is Hatch)
            {
                Hatch hatch = go as Hatch;
                HatchStyle hatchst = hatch.HatchStyle;
                if (hatchst != null && entries.Contains(hatchst.Name))
                {
                    hatch.Update();
                }
            }
            else if (go is Block)
            {
                foreach (IGeoObject child in (go as Block).Children)
                    UpdateAllHatchs(child);
            }
        }
        private void OnUpdateAllHatchs()
        {
            for (int j = 0; j < Frame.Project.GetModelCount(); j++)
            {
                Model m = Frame.Project.GetModel(j);
                for (int i = 0; i < m.Count; i++)
                {
                    UpdateAllHatchs(m[i]);
                }
            }
            // alter Code gab merkwürdigerweise einen Iterationsfehler:
            //foreach (Model m in Frame.Project)
            //{
            //    foreach (IGeoObject go in m) UpdateAllHatchs(go);
            //}
        }
        private void MakeNewHatchStyle(HatchStyle hst)
        {
            if (Frame != null)
            {
                hst.Init(Frame.Project);
            }
            string NewHatchStyleName = StringTable.GetString("HatchStyleList.NewHatchStyleName"); ;
            if (hst is HatchStyleSolid)
            {
                NewHatchStyleName = StringTable.GetString("HatchStyleNameSolid");
            }
            if (hst is HatchStyleLines)
            {
                NewHatchStyleName = StringTable.GetString("HatchStyleNameLines");
            }
            if (hst is HatchStyleContour)
            {
                NewHatchStyleName = StringTable.GetString("HatchStyleNameContour");
            }
            int MaxNr = 0;
            foreach (DictionaryEntry de in entries)
            {
                string Name = de.Key.ToString();
                if (Name.StartsWith(NewHatchStyleName))
                {
                    try
                    {
                        int nr = int.Parse(Name.Substring(NewHatchStyleName.Length));
                        if (nr > MaxNr) MaxNr = nr;
                    }
                    catch (ArgumentNullException) { } // has no number
                    catch (FormatException) { } // has something else as extension
                    catch (OverflowException) { } // too many digits
                }
            }
            MaxNr += 1; // next available number
            NewHatchStyleName += MaxNr.ToString();
            hst.Name = NewHatchStyleName;
            Add(hst);

            showProperties = null;
            propertyPage.Refresh(this);
            propertyPage.OpenSubEntries(this, true);
            propertyPage.StartEditLabel(showProperties[entries.IndexOfKey(NewHatchStyleName)] as IPropertyEntry);
        }
        bool ICommandHandler.OnCommand(string MenuId)
        {
            switch (MenuId)
            {
                case "MenuId.HatchStyleList.UpdateAllHatchs":
                    OnUpdateAllHatchs();
                    return true;
                case "MenuId.HatchStyleList.NewSolid":
                    MakeNewHatchStyle(new HatchStyleSolid());
                    return true;
                case "MenuId.HatchStyleList.NewLines":
                    MakeNewHatchStyle(new HatchStyleLines());
                    return true;
                case "MenuId.HatchStyleList.NewContour":
                    MakeNewHatchStyle(new HatchStyleContour());
                    return true;
                case "MenuId.HatchStyleList.RemoveUnused":
                    OnRemoveUnused();
                    return true;
                case "MenuId.HatchStyleList.UpdateFromProject":
                    OnUpdateFromProject();
                    return true;
                case "MenuId.HatchStyleList.AddFromGlobal":
                    OnAddFromGlobal();
                    return true;
                case "MenuId.HatchStyleList.MakeGlobal":
                    OnMakeGlobal();
                    return true;
            }
            return false;
        }
        bool ICommandHandler.OnUpdateCommand(string MenuId, CommandState CommandState)
        {
            // enabled ist immer OK
            switch (MenuId)
            {
                case "MenuId.HatchStyleList.NewSolid":
                    return true;
                case "MenuId.HatchStyleList.NewLines":
                    return true;
                case "MenuId.HatchStyleList.NewContour":
                    return true;
            }
            return false;
        }
        void ICommandHandler.OnSelected(MenuWithHandler selectedMenuItem, bool selected) { }
        #endregion
        #region IAttributeList Members

        void IAttributeList.Add(INamedAttribute toAdd)
        {
            if (toAdd is HatchStyle && Find(toAdd.Name) == null)
                Add((HatchStyle)toAdd);
        }

        INamedAttribute IAttributeList.Item(int Index)
        {
            return this[Index];
        }

        void IAttributeList.AttributeChanged(INamedAttribute attribute, ReversibleChange change)
        {
            if (owner != null) owner.AttributeChanged(this, attribute, change);
            if (owner is Project) needsUpdate = true;
        }
        bool IAttributeList.MayChangeName(INamedAttribute attribute, string newName)
        {
            if (attribute.Name == newName) return true;
            return Find(newName) == null;
        }
        void IAttributeList.NameChanged(INamedAttribute Attribute, string oldName)
        {
            HatchStyle hs = Attribute as HatchStyle;
            entries.Remove(oldName);
            entries.Add(hs.Name, hs);
        }
        void IAttributeList.Initialize()
        {
            HatchStyleSolid sol = new HatchStyleSolid();
            sol.Name = StringTable.GetString("HatchStyleList.DefaultSolid");
            sol.Color = new ColorDef(sol.Name, Color.Red, ColorDef.ColorSource.fromStyle);
            Add(sol);
            HatchStyleLines lin = new HatchStyleLines();
            lin.Name = StringTable.GetString("HatchStyleList.DefaultLines");
            lin.LineDistance = 10.0;
            lin.LineAngle = new Angle(1.0, 1.0); // 45°
            Add(lin);
        }

        IAttributeList IAttributeList.Clone() { return Clone() as IAttributeList; }
        void IAttributeList.Update(bool AddMissingToList)
        {
            foreach (HatchStyle hs in entries.Values)
                hs.Update(AddMissingToList);
        }
        void IAttributeList.Update(IGeoObject Object2Update)
        {
            if (Object2Update is IHatchStyle)
            {
                HatchStyle oldHS = (Object2Update as IHatchStyle).HatchStyle;
                if (oldHS == null) return;
                HatchStyle hs = Find(oldHS.Name);
                if (hs == null)
                    Add(oldHS);
                else
                    (Object2Update as IHatchStyle).HatchStyle = hs;

            }

        }

        INamedAttribute IAttributeList.Find(string Name)
        {
            return Find(Name) as INamedAttribute;
        }

        INamedAttribute IAttributeList.Current
        {
            get
            {
                return current as INamedAttribute;
            }
        }
        #endregion
    }
}
