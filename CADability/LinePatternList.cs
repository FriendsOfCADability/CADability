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
    public class LinePatternList : IShowPropertyImpl, IAttributeList, ISerializable, ICommandHandler, IDeserializationCallback
    {
        private ArrayList entries; // alphabetisch oder nicht, das ist noch nicht geklärt
        private LinePattern current; // die aktuelle
        private IAttributeListContainer owner; // Besitzer, entweder Projekt oder Settings
        internal string menuResourceId;
        /// <summary>
        /// Constructs an empty LinePatternList. Usually you dont have to construct a LinePatternList
        /// since on construction of a new <see cref="Project"/> the global LinePatternList is
        /// cloned and set as the projects LinePatternList.
        /// </summary>
        public LinePatternList()
        {
            entries = new ArrayList();
        }
        /// <summary>
        /// Adds an LinePattern object to the list. Throws a <see cref="NameAlreadyExistsException"/>
        /// if there is a LinePattern with the given name in the list. This also prevents the same
        /// object added twice to the list.
        /// </summary>
        /// <param name="linePatternToAdd">LinePattern to Add</param>
        public void Add(LinePattern linePatternToAdd)
        {
            for (int i = 0; i < entries.Count; ++i)
            {
                if ((entries[i] as LinePattern).Name == linePatternToAdd.Name)
                    throw new NameAlreadyExistsException(this, linePatternToAdd, linePatternToAdd.Name);
            }
            linePatternToAdd.Parent = this;
            entries.Add(linePatternToAdd);
            if (DidModifyEvent != null) DidModifyEvent(this, null); // ist das ok? der Modelview brauchts
        }
        /// <summary>
        /// Removes an entry from the LinePattern list. Depending on the context and global settings
        /// there might be a warning if the LinePattern is beeing used by an IGeoObject belonging to the 
        /// Project. If the LinePattern is not in the list, nothing happens.
        /// </summary>
        /// <param name="linePatternToRemove">LinePattern to remove</param>
        public void Remove(LinePattern linePatternToRemove)
        {
            int ind = entries.IndexOf(linePatternToRemove);
            if (ind >= 0)
            {
                if (owner != null)
                {
                    if (!owner.RemovingItem(this, linePatternToRemove, "LinePatternList")) return;
                }
                entries.RemoveAt(ind);
                linePatternToRemove.Parent = null;
            }
            if (linePatternToRemove == current)
            {
                if (entries.Count > 0)
                    current = entries[0] as LinePattern;
                else
                    current = null;
            }
            if (propertyTreeView != null)
            {	// d.h. es wird gerade angezeigt
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
        }
        /// <summary>
        /// Gets or sets the current LinePattern. The current LinePattern is used when a new
        /// GeoObject is constructed interactively.
        /// </summary>
        public LinePattern Current
        {
            get
            {
                if (current == null && entries.Count > 0)
                {
                    current = entries[0] as LinePattern;
                }
                return current;
            }
            set
            {
                current = value;
                if (propertyTreeView != null)
                {	// d.h. es wird gerade angezeigt
                    propertyTreeView.Refresh(this);
                }
            }
        }
        /// <summary>
        /// Returns the <see cref="LinePattern"/> with the given name or null if not found.
        /// </summary>
        /// <param name="name">Name of the requsetd LinePattern</param>
        /// <returns></returns>
        public LinePattern Find(string name)
        {
            foreach (LinePattern lw in entries)
            {
                if (lw.Name == name) return lw;
            }
            return null;
        }
        /// <summary>
        /// Returns the index of the given line pattern or -1 if not in the list
        /// </summary>
        /// <param name="lp">line pattern whos index is required</param>
        /// <returns>the index found or -1 if not found</returns>
        public int FindIndex(LinePattern lp)
        {
            for (int i = 0; i < entries.Count; ++i)
            {
                if (entries[i] == lp) return i;
            }
            return -1;
        }
        /// <summary>
        /// Returns the line pattern with the given index.
        /// </summary>
        /// <param name="Index">index of required laine pattern</param>
        /// <returns>line pattern</returns>
        public LinePattern this[int Index]
        {
            get
            {
                if (Index < 0 || Index >= entries.Count) return null;

                return entries[Index] as LinePattern;
            }
        }
        public LinePattern CreateOrFind(string Name, params double[] pattern)
        {
            LinePattern res = Find(Name);
            if (res != null) return res;
            res = new LinePattern(Name, pattern);
            Add(res);
            return res;
        }

        /// <summary>
        /// Creates a default LinePatternList.
        /// </summary>
        /// <returns></returns>
        public static LinePatternList GetDefault()
        {
            LinePatternList res = new LinePatternList();
            string defaultLinePatternList = StringTable.GetString("LinePatternList.Default");
            // z.B.: "|durchgezogen|gestrichelt: 2.0 2.0|strichpunktiert: 4.0 2.0 0.0 2.0|gepunktet: 0.0 2.0"
            string[] split = defaultLinePatternList.Split(new char[] { defaultLinePatternList[0] });
            foreach (string substr in split)
            {
                if (substr.Length > 0)
                {
                    string[] pos = substr.Split(':'); // halt fest am : splitten
                    if (pos.Length == 1) // das ist durchgezogen
                    {
                        LinePattern lw = new LinePattern();
                        lw.Name = pos[0];
                        try
                        {
                            res.Add(lw);
                        }
                        catch (NameAlreadyExistsException) { } // macht auch nix
                    }
                    else if (pos.Length == 2) // es müssen genau zwei sein
                    {
                        LinePattern lw = new LinePattern();
                        lw.Name = pos[0];
                        string[] dbls = pos[1].Split(null); // Whitespace
                        ArrayList a = new ArrayList();
                        for (int i = 0; i < dbls.Length; ++i)
                        {
                            try
                            {
                                double d = double.Parse(dbls[i], NumberFormatInfo.InvariantInfo);
                                a.Add(d);
                            }
                            catch (FormatException) { } // dann halt nicht zufügen
                        }
                        if (a.Count % 2 != 0) a.RemoveAt(a.Count - 1); // ggf letztes wegmachen
                        lw.Pattern = (double[])a.ToArray(typeof(double));
                        try
                        {
                            res.Add(lw);
                        }
                        catch (NameAlreadyExistsException) { } // macht auch nix
                    }
                }
            }
            return res;
        }
        /// <summary>
        /// Returns a copy of this LinePatternList. The entries are cloned so the copy is independant.
        /// </summary>
        /// <returns></returns>
        public LinePatternList Clone()
        {
            LinePatternList res = new LinePatternList();
            for (int i = 0; i < entries.Count; ++i)
            {
                res.Add((entries[i] as LinePattern).Clone());
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
        /// <summary>
        /// Provide a handler here to be notified when when a line pattern is beeing removed from this list.
        /// </summary>
        public event RemovingFromListDelegate RemovingFromListEvent;
        public event CADability.DidModifyDelegate DidModifyEvent;
        //Hilfsfunktion für Import
        internal LinePattern CreateOrFind(double[] pattern, LinePattern.Scaling scale)
        {
            int num = pattern.Length / 2;
            foreach (LinePattern lw in entries)
            {
                if (lw.PatternCount != num) continue;
                if (lw.Scale != scale) continue;
                int i = 0;
                for (; i < num; i++)
                    if (lw.GetStroke(i) != pattern[i * 2] || lw.GetGap(i) != pattern[i * 2 + 1])
                        break;
                if (i == num) return lw;//keine Abweichung gefunden
            }
            LinePattern res = new LinePattern();
            string prefix = "LP";
            for (int i = 0; i < pattern.Length; i++)
            {
                prefix = prefix + pattern[i].ToString() + ":";
            }
            res.Name = GetNewName(prefix);
            res.Pattern = pattern;
            res.Scale = scale;
            Add(res);
            return res;
        }

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
        public override void Added(IPropertyPage pp)
        {
            base.resourceIdInternal = "LinePatternList";
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
                return MenuResource.LoadMenuDefinition("MenuId.LinePatternList", false, this);
            }
        }
        #endregion
        #region IAttributeList Members

        void IAttributeList.Add(INamedAttribute toAdd)
        {
            if (toAdd is LinePattern && Find(toAdd.Name) == null)
                Add((LinePattern)toAdd);
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
            LinePattern l = attribute as LinePattern;
            if (l.Name == newName) return true; // garkeine Änderung
            for (int i = 0; i < entries.Count; ++i)
            {
                if ((entries[i] as LinePattern).Name == newName) return false;
            }
            return true;
        }

        void IAttributeList.NameChanged(INamedAttribute Attribute, string oldName)
        {

        }
        void IAttributeList.Initialize()
        {
            string defaultLinePatternList = StringTable.GetString("LinePatternList.Default");
            // z.B.: "|durchgezogen|gestrichelt: 2.0 2.0|strichpunktiert: 4.0 2.0 0.0 2.0|gepunktet: 0.0 2.0"
            string[] split = defaultLinePatternList.Split(new char[] { defaultLinePatternList[0] });
            foreach (string substr in split)
            {
                if (substr.Length > 0)
                {
                    string[] pos = substr.Split(':'); // halt fest am : splitten
                    if (pos.Length == 1) // das ist durchgezogen
                    {
                        LinePattern lw = new LinePattern();
                        lw.Name = pos[0];
                        try
                        {
                            Add(lw);
                        }
                        catch (NameAlreadyExistsException) { } // macht auch nix
                    }
                    else if (pos.Length == 2) // es müssen genau zwei sein
                    {
                        LinePattern lw = new LinePattern();
                        lw.Name = pos[0];
                        string[] dbls = pos[1].Split(null); // Whitespace
                        ArrayList a = new ArrayList();
                        for (int i = 0; i < dbls.Length; ++i)
                        {
                            try
                            {
                                double d = double.Parse(dbls[i], NumberFormatInfo.InvariantInfo);
                                a.Add(d);
                            }
                            catch (FormatException) { } // dann halt nicht zufügen
                        }
                        if (a.Count % 2 != 0) a.RemoveAt(a.Count - 1); // ggf letztes wegmachen
                        lw.Pattern = (double[])a.ToArray(typeof(double));
                        try
                        {
                            Add(lw);
                        }
                        catch (NameAlreadyExistsException) { } // macht auch nix
                    }
                }
            }
        }
        IAttributeList IAttributeList.Clone() { return Clone() as IAttributeList; }
        void IAttributeList.Update(bool AddMissingToList) { }
        void IAttributeList.Update(IGeoObject Object2Update)
        {
            if (Object2Update is ILinePattern)
            {
                LinePattern oldLP = (Object2Update as ILinePattern).LinePattern;
                if (oldLP == null) return;
                LinePattern lp = Find(oldLP.Name);
                if (lp == null)
                    Add(oldLP);
                else
                    (Object2Update as ILinePattern).LinePattern = lp;

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
        internal static LinePatternList Read(string name, SerializationInfo info, StreamingContext context)
        {
            try
            {
                return info.GetValue(name, typeof(LinePatternList)) as LinePatternList;
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
        protected LinePatternList(SerializationInfo info, StreamingContext context)
        {
            entries = (ArrayList)info.GetValue("Entries", typeof(ArrayList));
            current = (LinePattern)(info.GetValue("Current", typeof(LinePattern)));
        }
        void ISerializable.GetObjectData(SerializationInfo info, StreamingContext context)
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
                (entries[i] as LinePattern).Parent = this;
            }
        }
#endregion
#region ICommandHandler Members
        private string GetNewName()
        {
            string NewName = StringTable.GetString("LinePatternList.NewName");
            int MaxNr = 0;
            foreach (LinePattern lw in entries)
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
        private string GetNewName(string prefix)
        {
            string NewName = prefix;
            int MaxNr = 0;
            foreach (LinePattern lw in entries)
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
            foreach (LinePattern ds in Settings.GlobalSettings.LinePatternList.entries)
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
            Settings.GlobalSettings.LinePatternList.RemoveAll();
            foreach (LinePattern ds in entries)
            {
                try
                {
                    Settings.GlobalSettings.LinePatternList.Add(ds.Clone());
                }
                catch (NameAlreadyExistsException) { }
            }
        }
        private void OnUpdateFromProject()
        {
            Hashtable used = GetAllLinePatterns();
            foreach (LinePattern ds in used.Keys)
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
            Hashtable used = GetAllLinePatterns();
            bool found = false;
            do
            {
                found = false;
                if (entries.Count > 1)
                {
                    foreach (LinePattern ds in entries)
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
        private void GetAllLinePatterns(Hashtable collect, IGeoObject go)
        {
            if (go is ILinePattern)
            {
                collect[(go as ILinePattern).LinePattern] = null; // Hastable wird als set verwendet
            }
            else if (go is Block)
            {
                foreach (IGeoObject child in (go as Block).Children)
                    GetAllLinePatterns(collect, child);
            }
        }
        private Hashtable GetAllLinePatterns()
        {
            Hashtable res = new Hashtable();
            foreach (Model m in Frame.Project)
            {
                foreach (IGeoObject go in m) GetAllLinePatterns(res, go);
            }
            return res;
        }
        bool ICommandHandler.OnCommand(string MenuId)
        {
            switch (MenuId)
            {
                case "MenuId.LinePatternList.New":
                    {
                        LinePattern lw = new LinePattern();
                        lw.Name = GetNewName();
                        Add(lw);
                        subEntries = null;
                        propertyTreeView.Refresh(this);
                        propertyTreeView.OpenSubEntries(this, true);
                        propertyTreeView.StartEditLabel(SubEntries[entries.Count - 1] as IPropertyEntry); // der letzte
                        return true;
                    }
                case "MenuId.LinePatternList.RemoveUnused":
                    OnRemoveUnused();
                    return true;
                case "MenuId.LinePatternList.UpdateFromProject":
                    OnUpdateFromProject();
                    return true;
                case "MenuId.LinePatternList.AddFromGlobal":
                    OnAddFromGlobal();
                    return true;
                case "MenuId.LinePatternList.MakeGlobal":
                    OnMakeGlobal();
                    return true;
            }
            return false;
        }
        bool ICommandHandler.OnUpdateCommand(string MenuId, CommandState CommandState)
        {
            // TODO:  Add LinePatternList.OnUpdateCommand implementation
            return false;
        }
        void ICommandHandler.OnSelected(MenuWithHandler selectedMenuItem, bool selected) { }
        #endregion

    }
}
