using CADability.GeoObject;
using CADability.UserInterface;
using System;
using System.Collections;
using System.Globalization;
using System.Runtime.Serialization;


namespace CADability.Attribute
{
    /// <summary>
    /// 
    /// </summary>
    [Serializable()]
    public class LineWidthList : IShowPropertyImpl, IAttributeList, ISerializable, ICommandHandler, IDeserializationCallback
    {
        private ArrayList entries; // alphabetisch oder nicht, das ist noch nicht geklärt
        private LineWidth current; // die aktuelle
        private IAttributeListContainer owner; // Besitzer, entweder Projekt oder Settings
        internal string menuResourceId;

        private bool showSorted;
        /// <summary>
        /// Constructs an empty LineWidthList. Usually you dont have to construct a LineWidthList
        /// since on construction of a new <see cref="Project"/> the global LineWidthList is
        /// cloned and set as the projects LineWidthList.
        /// </summary>
        public LineWidthList()
        {
            entries = new ArrayList();
            showSorted = true;
        }

        /// <summary>
        /// Adds an LineWidth object to the list. Throws a <see cref="NameAlreadyExistsException"/>
        /// if there is a LineWidth with the given name in the list. This also prevents the same
        /// object added twice to the list.
        /// </summary>
        /// <param name="lineWidthToAdd">LineWidth to Add</param>
        public void Add(LineWidth lineWidthToAdd)
        {
            for (int i = 0; i < entries.Count; ++i)
            {
                if ((entries[i] as LineWidth).Name == lineWidthToAdd.Name)
                    throw new NameAlreadyExistsException(this, lineWidthToAdd, lineWidthToAdd.Name);
            }
            lineWidthToAdd.Parent = this;
            entries.Add(lineWidthToAdd);
            if (DidModifyEvent != null) DidModifyEvent(this, null); // ist das ok? der Modelview brauchts
        }
        /// <summary>
        /// Removes an entry from the LineWidth list. Depending on the context and global settings
        /// there might be a warning if the LineWidth is beeing used by an IGeoObject belonging to the 
        /// Project. If the LineWidth is not in the list, nothing happens.
        /// </summary>
        /// <param name="lineWidthToRemove">LineWidth to remove</param>
        public void Remove(LineWidth lineWidthToRemove)
        {
            int ind = entries.IndexOf(lineWidthToRemove);
            if (ind >= 0)
            {
                if (owner != null)
                {
                    if (!owner.RemovingItem(this, lineWidthToRemove, "LineWidthList")) return;
                }
                entries.RemoveAt(ind);
                lineWidthToRemove.Parent = null;
            }
            if (lineWidthToRemove == current)
            {
                if (entries.Count > 0)
                    current = entries[0] as LineWidth;
                else
                    current = null;
            }
            if (propertyTreeView != null)
            {   // d.h. es wird gerade angezeigt
                subEntries = null;
                propertyTreeView.Refresh(this);
            }
            if (DidModifyEvent != null) DidModifyEvent(this, null); // ist das ok? der Modelview brauchts
        }
        private void RemoveAll() // nur intern verwenden, da nicht RemovingItem aufgerufen wird
        {
            entries.Clear();
            subEntries = null;
            if (propertyTreeView != null) propertyTreeView.OpenSubEntries(this, false); // zuklappen
            if (DidModifyEvent != null) DidModifyEvent(this, null); // ist das ok? der Modelview brauchts
        }
        /// <summary>
        /// Gets or sets the current LineWidth. The current LineWidth is used when a new
        /// GeoObject is constructed interactively.
        /// </summary>
        public LineWidth Current
        {
            get
            {
                if (current == null && entries.Count > 0)
                {
                    current = entries[0] as LineWidth;
                }
                return current;
            }
            set
            {
                current = value;
                if (propertyTreeView != null)
                {   // d.h. es wird gerade angezeigt
                    propertyTreeView.Refresh(this);
                }
            }
        }
        /// <summary>
        /// Returns the <see cref="LineWidth"/> with the given name or null if not found.
        /// </summary>
        /// <param name="name">Name of the requsetd LineWidth</param>
        /// <returns></returns>
        public LineWidth Find(string name)
        {
            foreach (LineWidth lw in entries)
            {
                if (lw.Name == name) return lw;
            }
            return null;
        }
        // Hilfsfunktion für import
        // liefert die erste LineWidth mit der angegeben Breite/Scaling oder generiert die gefordete LineWidth
        internal LineWidth CreateOrFind(double width, LineWidth.Scaling scale)
        {
            foreach (LineWidth lw in entries)
            {
                if (lw.Width == width && lw.Scale == scale) return lw;
            }
            LineWidth res = new LineWidth();
            res.Width = width;
            res.Scale = scale;
            Add(res);
            res.Name = scale.ToString() + width.ToString();
            return res;
        }
        public LineWidth CreateOrFind(string Name, double width)
        {
            LineWidth res = Find(Name);
            if (res != null) return res;
            res = new LineWidth(Name, width);
            Add(res);
            return res;
        }
        public LineWidth CreateOrModify(string Name, double width)
        {
            LineWidth res = Find(Name);
            if (res != null)
            {
                res.Width = width;
                return res;
            }
            res = new LineWidth(Name, width);
            Add(res);
            return res;
        }
        /// <summary>
		/// Returns the index of the given linewidth in this list
		/// </summary>
		/// <param name="lw">linewidth for which the index is requested</param>
		/// <returns>the index found or -1 if this list does not contain lw</returns>
		public int FindIndex(LineWidth lw)
        {
            for (int i = 0; i < entries.Count; ++i)
            {
                if (entries[i] == lw) return i;
            }
            return -1;
        }
        /// <summary>
        /// Gets the LineWidth with the given index.
        /// </summary>
        public LineWidth this[int index]
        {
            get { return entries[index] as LineWidth; }
        }
        /// <summary>
        /// Creates a default LineWidthList.
        /// </summary>
        /// <returns></returns>
        public static LineWidthList GetDefault()
        {
            LineWidthList res = new LineWidthList();
            string defaultLineWidthList = StringTable.GetString("LineWidthList.Default");
            // z.B.: "|Standard (0.7 mm):0.7L|dünn:0.0D|0.3 mm:0.3W"
            string[] split = defaultLineWidthList.Split(new char[] { defaultLineWidthList[0] });
            foreach (string substr in split)
            {
                if (substr.Length > 0)
                {
                    string[] pos = substr.Split(':'); // halt fest am : splitten
                    if (pos.Length == 2) // es müssen genau zwei sein
                    {
                        LineWidth lw = new LineWidth();
                        lw.Name = pos[0];
                        int ind = pos[1].LastIndexOfAny("0123456789.".ToCharArray());
                        string widthstr;
                        if (ind > 0) widthstr = pos[1].Substring(0, ind + 1);
                        else widthstr = pos[1];
                        try
                        {
                            if (ind > 0 && ind < pos[1].Length - 1)
                            {
                                switch (pos[1][ind + 1])
                                {
                                    default: // ist Layout
                                    case 'L': lw.Scale = LineWidth.Scaling.Layout; break;
                                    case 'W': lw.Scale = LineWidth.Scaling.World; break;
                                    case 'D': lw.Scale = LineWidth.Scaling.Device; break;
                                }
                            }
                            lw.Name = pos[0];
                            lw.Width = double.Parse(widthstr, NumberFormatInfo.InvariantInfo);
                            res.Add(lw);
                        }
                        catch (FormatException) { } // dann halt nicht zufügen
                        catch (NameAlreadyExistsException) { } // macht auch nix
                    }
                }
            }
            return res;
        }
        /// <summary>
        /// Returns a copy of this LineWidthList. The entries are cloned so the copy is independant.
        /// </summary>
        /// <returns></returns>
        public LineWidthList Clone()
        {
            LineWidthList res = new LineWidthList();
            for (int i = 0; i < entries.Count; ++i)
            {
                res.Add((entries[i] as LineWidth).Clone());
            }
            if (current != null)
            {
                res.current = res.Find(current.Name);
            }
            else
            {
                res.current = null;
            }
            return res;
        }
        public event CADability.DidModifyDelegate DidModifyEvent;
        public event RemovingFromListDelegate RemovingFromListEvent;
        #region IShowProperty Members (IShowPropertyImpl)
        private IShowProperty[] subEntries;
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
        /// <summary>
        /// Overrides <see cref="IShowPropertyImpl.LabelType"/>
        /// </summary>
        public override ShowPropertyLabelFlags LabelType
        {
            get
            {
                return ShowPropertyLabelFlags.ContextMenu | ShowPropertyLabelFlags.ContextMenu | ShowPropertyLabelFlags.Selectable;
            }
        }
        /// <summary>
        /// Overrides <see cref="IShowPropertyImpl.Added"/>
        /// </summary>
        /// <param name="propertyTreeView"></param>
        public override void Added(IPropertyPage pp)
        {
            base.resourceIdInternal = "LineWidthList";
            base.Added(pp);
        }
        public override void Removed(IPropertyPage pp)
        {
            base.Removed(pp);
            subEntries = null;
        }
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
                    subEntries = new IShowProperty[entries.Count];
                    for (int i = 0; i < entries.Count; ++i)
                    {
                        subEntries[i] = entries[i] as IShowProperty;
                    }
                    if (showSorted)
                        Array.Sort(subEntries, new NamedAttributeComparer());
                }
                return subEntries;
            }
        }
        /// <summary>
        /// Overrides <see cref="IShowPropertyImpl.SubEntriesCount"/>, 
        /// returns the number of subentries in this property view.
        /// </summary>
        public override int SubEntriesCount
        {
            get
            {
                return SubEntries.Length;
            }
        }
        public override MenuWithHandler[] ContextMenu
        {
            get
            {
                if (menuResourceId != null)
                    return MenuResource.LoadMenuDefinition(menuResourceId, false, this);
                return MenuResource.LoadMenuDefinition("MenuId.LineWidthList", false, this);
            }
        }
#endregion
#region IAttributeList Members

        void IAttributeList.Add(INamedAttribute toAdd)
        {
            if (toAdd is LineWidth && Find(toAdd.Name) == null)
                Add((LineWidth)toAdd);
        }
        /// <summary>
        /// Gets the number of entities in this list.
        /// </summary>
        public int Count
        {
            get
            {
                return entries.Count;
            }
        }
        INamedAttribute IAttributeList.Item(int Index)
        {
            return entries[Index] as INamedAttribute;
        }

        IAttributeListContainer IAttributeList.Owner
        {
            get
            {
                return owner;
            }
            set
            {
                owner = value;
            }
        }

        void IAttributeList.AttributeChanged(INamedAttribute attribute, ReversibleChange change)
        {
            if (owner != null) owner.AttributeChanged(this, attribute, change);
        }

        bool IAttributeList.MayChangeName(INamedAttribute attribute, string newName)
        {
            LineWidth l = attribute as LineWidth;
            if (l.Name == newName) return true; // garkeine Änderung
            for (int i = 0; i < entries.Count; ++i)
            {
                if ((entries[i] as LineWidth).Name == newName) return false;
            }
            return true;
        }

        void IAttributeList.NameChanged(INamedAttribute Attribute, string oldName)
        {

        }
        void IAttributeList.Initialize()
        {
            string defaultLineWidthList = StringTable.GetString("LineWidthList.Default");
            // z.B.: "|Standard (0.7 mm):0.7L|dünn:0.0D|0.3 mm:0.3W"
            string[] split = defaultLineWidthList.Split(new char[] { defaultLineWidthList[0] });
            foreach (string substr in split)
            {
                if (substr.Length > 0)
                {
                    string[] pos = substr.Split(':'); // halt fest am : splitten
                    if (pos.Length == 2) // es müssen genau zwei sein
                    {
                        LineWidth lw = new LineWidth();
                        lw.Name = pos[0];
                        int ind = pos[1].LastIndexOfAny("0123456789.".ToCharArray());
                        string widthstr;
                        if (ind > 0) widthstr = pos[1].Substring(0, ind + 1);
                        else widthstr = pos[1];
                        try
                        {
                            if (ind > 0)
                            {
                                switch (pos[1][ind + 1])
                                {
                                    default: // ist Layout
                                    case 'L': lw.Scale = LineWidth.Scaling.Layout; break;
                                    case 'W': lw.Scale = LineWidth.Scaling.World; break;
                                    case 'D': lw.Scale = LineWidth.Scaling.Device; break;
                                }
                            }
                            lw.Name = pos[0];
                            lw.Width = double.Parse(widthstr, NumberFormatInfo.InvariantInfo);
                            Add(lw);
                        }
                        catch (FormatException) { } // dann halt nicht zufügen
                        catch (NameAlreadyExistsException) { } // macht auch nix
                    }
                }
            }
        }
        IAttributeList IAttributeList.Clone() { return Clone() as IAttributeList; }
        void IAttributeList.Update(bool AddMissingToList) { }
        void IAttributeList.Update(IGeoObject Object2Update)
        {
            if (Object2Update is ILineWidth)
            {
                LineWidth oldLW = (Object2Update as ILineWidth).LineWidth;
                if (oldLW == null) return;
                LineWidth lw = Find(oldLW.Name);
                if (lw == null)
                    Add(oldLW);
                else
                    (Object2Update as ILineWidth).LineWidth = lw;
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
#region ISerializable Members
        public static LineWidthList Read(string name, SerializationInfo info, StreamingContext context)
        {
            try
            {
                return info.GetValue(name, typeof(LineWidthList)) as LineWidthList;
            }
            catch (SerializationException)
            {
                return null;
            }
            catch (InvalidCastException)
            {
                return null;
            }
        }
        /// <summary>
        /// Constructor required by deserialization
        /// </summary>
        /// <param name="info">SerializationInfo</param>
        /// <param name="context">StreamingContext</param>
        protected LineWidthList(SerializationInfo info, StreamingContext context)
        {
            entries = (ArrayList)info.GetValue("Entries", typeof(ArrayList));
            current = (LineWidth)(info.GetValue("Current", typeof(LineWidth)));
        }
        /// <summary>
        /// Implements <see cref="ISerializable.GetObjectData"/>
        /// </summary>
        /// <param name="info">The <see cref="System.Runtime.Serialization.SerializationInfo"/> to populate with data.</param>
        /// <param name="context">The destination (<see cref="System.Runtime.Serialization.StreamingContext"/>) for this serialization.</param>
        public void GetObjectData(SerializationInfo info, StreamingContext context)
        {
            info.AddValue("Entries", entries);
            info.AddValue("Current", current);
        }
#endregion
#region IDeserializationCallback Members
        void IDeserializationCallback.OnDeserialization(object sender)
        {
            for (int i = 0; i < entries.Count; ++i)
            {
                (entries[i] as LineWidth).Parent = this;
            }
        }
#endregion
#region ICommandHandler Members
        private string GetNewName()
        {
            string NewName = StringTable.GetString("LineWidthList.NewName");
            int MaxNr = 0;
            foreach (LineWidth lw in entries)
            {
                string Name = lw.Name;
                if (Name.StartsWith(NewName))
                {
                    try
                    {
                        int nr = int.Parse(Name.Substring(NewName.Length));
                        if (nr > MaxNr) MaxNr = nr;
                    }
                    catch (ArgumentNullException) { } // hat garkeine Nummer
                    catch (FormatException) { } // hat was anderes als nur Ziffern
                    catch (OverflowException) { } // zu viele Ziffern
                }
            }
            MaxNr += 1; // nächste freie Nummer
            NewName += MaxNr.ToString();
            return NewName;
        }
        private void OnAddFromGlobal()
        {
            foreach (LineWidth ds in Settings.GlobalSettings.LineWidthList.entries)
            {
                if (Find(ds.Name) == null)
                {
                    this.Add(ds.Clone());
                }
            }
            subEntries = null;
            if (propertyTreeView != null) propertyTreeView.Refresh(this);
        }
        private void OnMakeGlobal()
        {
            Settings.GlobalSettings.LineWidthList.RemoveAll();
            foreach (LineWidth ds in entries)
            {
                try
                {
                    Settings.GlobalSettings.LineWidthList.Add(ds.Clone());
                }
                catch (NameAlreadyExistsException) { }
            }
        }
        private void OnUpdateFromProject()
        {
            Hashtable used = GetAllLineWidths();
            foreach (LineWidth ds in used.Keys)
            {
                if (!entries.Contains(ds))
                {
                    try
                    {
                        Add(ds);
                    }
                    catch (NameAlreadyExistsException) { }
                }
            }
            subEntries = null;
            if (propertyTreeView != null) propertyTreeView.Refresh(this);
        }
        private void OnRemoveUnused()
        {
            Hashtable used = GetAllLineWidths();
            bool found = false;
            do
            {
                found = false;
                if (entries.Count > 1)
                {
                    foreach (LineWidth ds in entries)
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
        private void GetAllLineWidths(Hashtable collect, IGeoObject go)
        {
            if (go is ILineWidth)
            {
                collect[(go as ILineWidth).LineWidth] = null; // Hastable wird als set verwendet
            }
            else if (go is Block)
            {
                foreach (IGeoObject child in (go as Block).Children)
                    GetAllLineWidths(collect, child);
            }
        }
        private Hashtable GetAllLineWidths()
        {
            Hashtable res = new Hashtable();
            foreach (Model m in Frame.Project)
            {
                foreach (IGeoObject go in m) GetAllLineWidths(res, go);
            }
            return res;
        }
        bool ICommandHandler.OnCommand(string MenuId)
        {
            switch (MenuId)
            {
                case "MenuId.LineWidthList.New":
                    {
                        LineWidth lw = new LineWidth();
                        lw.Name = GetNewName();
                        Add(lw);
                        subEntries = null;
                        propertyTreeView.Refresh(this);
                        propertyTreeView.OpenSubEntries(this, true);
                        propertyTreeView.StartEditLabel(SubEntries[entries.Count - 1] as IPropertyEntry); // der letzte
                        return true;
                    }
                case "MenuId.LineWidthList.RemoveUnused":
                    OnRemoveUnused();
                    return true;
                case "MenuId.LineWidthList.UpdateFromProject":
                    OnUpdateFromProject();
                    return true;
                case "MenuId.LineWidthList.AddFromGlobal":
                    OnAddFromGlobal();
                    return true;
                case "MenuId.LineWidthList.MakeGlobal":
                    OnMakeGlobal();
                    return true;
            }
            return false;
        }
        bool ICommandHandler.OnUpdateCommand(string MenuId, CommandState CommandState)
        {
            // TODO:  Add LineWidthList.OnUpdateCommand implementation
            return false;
        }
        void ICommandHandler.OnSelected(MenuWithHandler selectedMenuItem, bool selected) { }
        #endregion

    }
}
