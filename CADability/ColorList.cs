using CADability.GeoObject;
using CADability.UserInterface;
using System;
using System.Collections;
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
    /// Eine Tabelle von benannten Farben. Die Tabelle ist serialisierbar und wird gewöhnlich
    /// in einem "Settings" Objekt gespeichert. Die Tabelle kann in der EigenschaftenAnzeige
    /// interaktiv manipuliert werden.
    /// </summary>
    [Serializable()]
    public class ColorList : PropertyEntryImpl, ISerializable,
        INotifyModification, ICollection, IAttributeList, ICommandHandler, IDeserializationCallback, IJsonSerialize, IJsonSerializeDone
    {
        // die Implementierung als ArrayListe von einem gewissen Typ ist wichtig, um eine
        // (auch nicht-alphabetische) Reihenfolge beizubehalten.
        private ArrayList namedColors;
        private IAttributeListContainer owner; // Besitzer, entweder Projekt oder Settings
        private IPropertyEntry[] subItems;
        internal string menuResourceId;

        private ColorDef current;
        public enum StaticFlags { allowNone = 0x00, allowFromParent = 0x01, allowFromStyle = 0x02, allowAll = 0x03, allowUndefined = 0x04 };
        // nicht alle referentiellen ColorDef können von allen Objekte benutzt werder
        // allowAll ist nicht allow undefined
        private StaticFlags staticFlags;
        /// <summary>
        /// Erzeugt eine leere ColorList.
        /// </summary>
        public ColorList()
        {
            namedColors = new ArrayList();
            resourceIdInternal = "ColorList";
        }


        public static ColorList GetDefault()
        {
            ColorList res = new ColorList();
            string defaultColorList = StringTable.GetString("ColorList.Default");
            string[] split = defaultColorList.Split(new char[] { defaultColorList[0] });
            foreach (string cdef in split)
            {
                if (cdef.Length > 0)
                {
                    int ind = cdef.IndexOf('(');
                    if (ind > 0)
                    {
                        string Name = cdef.Substring(0, ind);
                        int ind1 = cdef.IndexOf(')', ind);
                        if (ind1 > ind)
                        {
                            string rgb = cdef.Substring(ind + 1, ind1 - ind - 1);
                            string[] colors = rgb.Split(',');
                            if (colors.Length == 3)
                            {
                                try
                                {
                                    res.AddColor(Name, Color.FromArgb(int.Parse(colors[0]), int.Parse(colors[1]), int.Parse(colors[2])));
                                }
                                catch (FormatException)
                                {	// läuft nicht, wenn falsch formatiert
                                }
                            }
                        }
                    }
                }
            }
            res.Current = res[0];// Es muss immer eine aktuelle Farbe geben
            return res;
        }
        internal ColorList clone()
        {
            ColorList res = new ColorList();
            for (int i = 0; i < namedColors.Count; ++i)
            {
                res.Add(((ColorDef)namedColors[i]).Clone());
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
        public IAttributeListContainer Owner
        {
            get { return owner; }
            set { owner = value; }
        }
        /// <summary>
        /// Liefert den Index zu dem gegebenen Namen.
        /// </summary>
        /// <param name="Name">Der Name der gesuchten Farbe.</param>
        /// <returns>Index der gesuchten Farbe oder -1, wenn keine Farbe mit diesem Namen in der Tabelle existiert.</returns>
        /// <summary>
        /// Fügt eine Farbe hinzu, oder überschreibt eine bestehende, wenn der Name bereits existiert.
        /// </summary>
        /// <param name="Name">Bezeichnung der Farbe.</param>
        /// <param name="Color">RGB (oder auch ARGB) Wert der Farbe.</param>
        /// <returns>Index des neuen oder veränderten Eintrags.</returns>
        public int AddColor(string Name, Color Color)
        {
            int ind = IndexOf(Name);
            ColorDef cd = new ColorDef(Name, Color);
            cd.Source = ColorDef.ColorSource.fromName;
            if (ind >= 0)
            {
                namedColors[ind] = cd;
            }
            else
            {
                namedColors.Add(cd);
                ind = namedColors.Count - 1;
            }
            cd.Parent = this;
            if (DidModifyEvent != null) DidModifyEvent(this, null);
            return ind;
        }
        private void RemoveAll() // nur intern verwenden, da nicht RemovingItem aufgerufen wird
        {
            namedColors.Clear();
            subItems = null;
            if (propertyPage != null) propertyPage.OpenSubEntries(this, false); // zuklappen
        }

        public void Add(ColorDef cd)
        {
            if (IndexOf(cd.Name) >= 0)
                throw new NameAlreadyExistsException(this, cd, cd.Name);
            namedColors.Add(cd);
            cd.Parent = this;
            if (current == null)
                current = namedColors[0] as ColorDef;
            if (DidModifyEvent != null) DidModifyEvent(null, null);
        }
        private void Remove(ColorDef cd)
        {
            int ind = IndexOf(cd.Name);
            if (ind >= 0) RemoveAt(ind);
        }
        /// <summary>
        /// Entfernt die Farbe mit dem gegebenen Index.
        /// </summary>
        /// <param name="Index">Der Index.</param>
        public void RemoveAt(int Index)
        {
            if (owner != null)
            {
                if (!owner.RemovingItem(this, namedColors[Index] as ColorDef, "ColorList")) return;
            }
            (namedColors[Index] as ColorDef).Parent = null;
            namedColors.RemoveAt(Index);
            this.subItems = null;
            if (DidModifyEvent != null) DidModifyEvent(this, null);
        }
        /// <summary>
        /// Liefert den Namen zu dem gegebenen Index.
        /// </summary>
        /// <param name="Index">Der Index.</param>
        /// <returns>Der Name.</returns>
        public int IndexOf(string Name)
        {
            int i;
            for (i = 0; i < namedColors.Count; ++i)
            {
                if (((ColorDef)namedColors[i]).Name == Name) return i;
            }
            if ((staticFlags & StaticFlags.allowFromParent) > 0)
                if (ColorDef.CDfromParent.Name == Name)
                    return i;
                else i++;
            if ((staticFlags & StaticFlags.allowFromStyle) > 0)
                if (ColorDef.CDfromStyle.Name == Name)
                    return i;
            return -1;
        }
        public string GetName(int Index)
        {
            if (Index < namedColors.Count)
                return ((ColorDef)namedColors[Index]).Name;
            switch (Index - namedColors.Count)
            {
                case 0:
                    if ((staticFlags & StaticFlags.allowFromParent) > 0)
                        return ColorDef.CDfromParent.Name;
                    else if ((staticFlags & StaticFlags.allowFromStyle) > 0)
                        return ColorDef.CDfromStyle.Name;
                    break;
                case 1:
                    if ((staticFlags & StaticFlags.allowFromParent) > 0 && (staticFlags & StaticFlags.allowFromStyle) > 0)
                        return ColorDef.CDfromStyle.Name;
                    break;
            }
            return null;
        }
        public bool IsStatic(int idx)
        {
            if (idx >= namedColors.Count && idx < namedColors.Count + 3)
                return true;
            return false;
        }
        internal bool isValidIndex(int val)
        {
            if (val < 0) return false;
            if (val < namedColors.Count) return true;
            int offset = 0;
            if ((staticFlags & StaticFlags.allowFromParent) != 0) offset++;
            if ((staticFlags & StaticFlags.allowFromStyle) != 0) offset++;
            if (val - offset < namedColors.Count) return true;
            return false;
        }
        public int CurrentIndex
        {
            get { return IndexOf(current.Name); }
            set
            {
                if (!isValidIndex(value)) return;
                current = this[value];
            }
        }
        public ColorDef Current
        {
            get
            {
                if (current == null && namedColors.Count > 0)
                {
                    current = namedColors[0] as ColorDef;
                }
                return current;
            }
            set
            {
                if (value != null)
                {
                    if (!namedColors.Contains(value))
                        Add(value);
                    current = value;

                }
            }
        }
        /// <summary>
        /// Liefert die Farbe zu dem gegebenen Index.
        /// </summary>
        /// <param name="Index">Der Index.</param>
        /// <returns>Die Farbe.</returns>
        public Color GetColor(int Index)
        {
            if (Index < 0 || Index >= namedColors.Count)
                return Color.Blue;
            return ((ColorDef)namedColors[Index]).Color;
        }
        /// <summary>
        /// Liefert das ColorDef Objekt zu dem gegebenen Index. Den Index zu einem Namen 
        /// bekommt man mit der Methode <seealso cref="IndexOf"/>.
        /// </summary>
        /// <param name="Index">Der Index des gesuchten Objektes</param>
        /// <returns></returns>
        /*public ColorDef this[int Index]
        {
            string name = GetName(Index);
            return GetColorDef(name);
        }
        */
        public ColorDef this[int Index]
        {
            get
            {
                string name = GetName(Index);
                return Find(name);
            }
        }

        public ColorDef FindColor(Color rgb)
        {
            foreach (ColorDef cd in namedColors)
            {
                if (cd.Color == rgb) return cd;
            }
            return null;
        }
        public ColorDef CreateOrFind(string Name, Color Color)
        {
            if (Name == null) Name = GetNewName();
            ColorDef res = Find(Name);
            if (res != null) return res;
            ColorDef cd = new ColorDef(Name, Color);
            cd.Source = ColorDef.ColorSource.fromName;
            Add(cd);
            return cd;
        }
        public ColorDef CreateOrModify(string Name, Color Color)
        {
            ColorDef res = Find(Name);
            if (res != null)
            {
                res.Color = Color;
                return res;
            }
            ColorDef cd = new ColorDef(Name, Color);
            cd.Source = ColorDef.ColorSource.fromName;
            Add(cd);
            return cd;
        }
        /// <summary>
        /// Implements <see cref="CADability.Attribute.IAttributeList.Find (string)"/>
        /// </summary>
        /// <param name="Name"></param>
        /// <returns></returns>
        public ColorDef Find(string Name)
        {
            for (int i = 0; i < namedColors.Count; ++i)
            {
                if (((ColorDef)namedColors[i]).Name == Name)
                    return (ColorDef)namedColors[i];
            }
            /*			if((staticFlags & StaticFlags.allowFromLayer )>0)
                            if( ColorDef.CDfromLayer.Name == Name)
                                return  ColorDef.CDfromLayer;
            */
            if ((staticFlags & StaticFlags.allowFromParent) > 0)
                if (ColorDef.CDfromParent.Name == Name)
                    return ColorDef.CDfromParent;
            if ((staticFlags & StaticFlags.allowFromStyle) > 0)
                if (ColorDef.CDfromStyle.Name == Name)
                    return ColorDef.CDfromStyle;
            return null;
        }
        public int FindIndex(ColorDef cd)
        {
            for (int i = 0; i < namedColors.Count; ++i)
            {
                if (namedColors[i] == cd) return i;
            }
            return -1;
        }

        public ColorDef GetNextColorDef(string Name)
        {
            for (int i = 0; i < namedColors.Count - 1; ++i)
            {
                if (((ColorDef)namedColors[i]).Name == Name)
                    return namedColors[i + 1] as ColorDef;
            }
            /*           if( ColorDef.CDfromLayer.Name == Name)
                           if(( staticFlags & StaticFlags.allowFromLayer )>0)
                               return  ColorDef.CDfromParent;
                           else  */
            if ((staticFlags & StaticFlags.allowFromStyle) > 0)
                return ColorDef.CDfromStyle;
            if (ColorDef.CDfromParent.Name == Name)
                if ((staticFlags & StaticFlags.allowFromStyle) > 0)
                    return ColorDef.CDfromStyle;
            return namedColors[0] as ColorDef;
        }
        /// <summary>
        /// Verändert den Namen bei gegebenem Index. 
        /// </summary>
        /// <param name="Index">Index des zu verändernden Wertes.</param>
        /// <param name="Name">Neuer Name.</param>
        public void SetName(int Index, string Name)
        {
            if (Index < namedColors.Count)
            {
                if ((this as IAttributeList).MayChangeName((ColorDef)(namedColors[Index]), Name))
                {
                    ((ColorDef)namedColors[Index]).Name = Name;
                    if (DidModifyEvent != null) DidModifyEvent(null, null);
                }
            }
        }
        /// <summary>
        /// Verändert die Farbe bei gegebenem Index. 
        /// </summary>
        /// <param name="Index">Index des zu verändernden Wertes.</param>
        /// <param name="Color">Neuer Farbwert.</param>
        public void SetColor(int Index, Color Color)
        {
            ((ColorDef)namedColors[Index]).Color = Color;
            if (DidModifyEvent != null) DidModifyEvent(null, null);
        }
        public string[] Names
        {
            get
            {
                int len = namedColors.Count;
                if ((staticFlags & StaticFlags.allowFromParent) > 0) len++;
                if ((staticFlags & StaticFlags.allowFromStyle) > 0) len++;

                string[] result = new string[len];
                int i;
                for (i = 0; i < namedColors.Count; ++i)
                {
                    result[i] = ((ColorDef)namedColors[i]).Name;
                }
                //if(( staticFlags & StaticFlags.allowFromLayer)>0)result[i++] = ColorDef.CDfromLayer.Name ;
                if ((staticFlags & StaticFlags.allowFromParent) > 0) result[i++] = ColorDef.CDfromParent.Name;
                if ((staticFlags & StaticFlags.allowFromStyle) > 0) result[i] = ColorDef.CDfromStyle.Name;
                return result;
            }
        }

        public new StaticFlags Usage
        {
            get
            {
                return staticFlags;
            }
            set
            {
                staticFlags = value;
            }
        }
        public string GetNewName(string startWith)
        {
            string NewColorName = startWith;
            int MaxNr = 0;
            for (int i = 0; i < namedColors.Count; ++i)
            {
                string Name = GetName(i);
                if (Name != null && Name.StartsWith(NewColorName))
                {
                    try
                    {
                        int nr = int.Parse(Name.Substring(NewColorName.Length));
                        if (nr > MaxNr) MaxNr = nr;
                    }
                    catch (ArgumentNullException) { } // hat garkeine Nummer
                    catch (FormatException) { } // hat was anderes als nur Ziffern
                    catch (OverflowException) { } // zu viele Ziffern
                }
            }
            MaxNr += 1; // nächste freie Nummer
            NewColorName += MaxNr.ToString();
            return NewColorName;
        }
        public string GetNewName()
        {
            return GetNewName(StringTable.GetString("ColorList.NewColorName"));
            //string NewColorName = StringTable.GetString("ColorList.NewColorName");
            //int MaxNr = 0;
            //for (int i = 0; i < namedColors.Count; ++i)
            //{
            //    string Name = GetName(i);
            //    if (Name !=null && Name.StartsWith(NewColorName))
            //    {
            //        try
            //        {
            //            int nr = int.Parse(Name.Substring(NewColorName.Length));
            //            if (nr > MaxNr) MaxNr = nr;
            //        }
            //        catch (ArgumentNullException) { } // hat garkeine Nummer
            //        catch (FormatException) { } // hat was anderes als nur Ziffern
            //        catch (OverflowException) { } // zu viele Ziffern
            //    }
            //}
            //MaxNr += 1; // nächste freie Nummer
            //NewColorName += MaxNr.ToString();
            //return NewColorName;
        }
        private void OnNewColor()
        {
            string NewColorName = GetNewName();
            AddColor(NewColorName, Color.Black);
            subItems = null;
            propertyPage.Refresh(this);
            propertyPage.OpenSubEntries(this, true);
            propertyPage.StartEditLabel(SubItems[SubItems.Length - 1]);
        }


        #region PropertyEntry Members

        public override PropertyEntryType Flags
        {
            get
            {
                return PropertyEntryType.ContextMenu | PropertyEntryType.Selectable | PropertyEntryType.GroupTitle | PropertyEntryType.HasSubEntries;
            }
        }
        /// <summary>
        /// Overrides <see cref="IShowPropertyImpl.SubEntries"/>, 
        /// returns the subentries in this property view.
        /// </summary>
        public override IPropertyEntry[] SubItems
        {
            get
            {
                if (subItems == null)
                {
                    subItems = new IPropertyEntry[namedColors.Count];
                    for (int i = 0; i < namedColors.Count; ++i)
                    {
                        subItems[i] = new ColorListProperty(this, i);
                    }
                }
                return subItems;
            }
        }
        public override MenuWithHandler[] ContextMenu
        {
            get
            {
                if (menuResourceId != null) return MenuResource.LoadMenuDefinition(menuResourceId, false, this);
                else return MenuResource.LoadMenuDefinition("MenuId.ColorList", false, this);
            }
        }
        #endregion

        #region ISerializable Members
        /// <summary>
        /// Constructor required by deserialization
        /// </summary>
        /// <param name="info">SerializationInfo</param>
        /// <param name="context">StreamingContext</param>
        protected ColorList(SerializationInfo info, StreamingContext context)
        {
            namedColors = (ArrayList)(info.GetValue("NamedColors", typeof(ArrayList)));
            current = ColorDef.Read("Current", info, context);
            resourceIdInternal = "ColorList";
        }
        /// <summary>
        /// Implements <see cref="ISerializable.GetObjectData"/>
        /// </summary>
        /// <param name="info">The <see cref="System.Runtime.Serialization.SerializationInfo"/> to populate with data.</param>
        /// <param name="context">The destination (<see cref="System.Runtime.Serialization.StreamingContext"/>) for this serialization.</param>
        public void GetObjectData(SerializationInfo info, StreamingContext context)
        {
            info.AddValue("NamedColors", namedColors, namedColors.GetType());
            info.AddValue("Current", current, typeof(ColorDef));
        }
        #endregion
        #region IDeserializationCallback Members
        void IDeserializationCallback.OnDeserialization(object sender)
        {
            foreach (ColorDef cd in namedColors)
            {
                cd.Parent = this;
            }
        }
        #endregion


        #region INotifyModification Members

        public event CADability.DidModifyDelegate DidModifyEvent;

        #endregion

        #region ICollection Members

        public bool IsSynchronized
        {
            get
            {
                return namedColors.IsSynchronized;
            }
        }

        public int Count
        {
            get
            {
                return namedColors.Count;
            }
        }

        public void CopyTo(Array array, int index)
        {
            namedColors.CopyTo(array, index);
        }

        public object SyncRoot
        {
            get
            {
                return namedColors.SyncRoot;
            }
        }

        #endregion

        #region IEnumerable Members

        public IEnumerator GetEnumerator()
        {
            return namedColors.GetEnumerator();
        }

        #endregion


        #region IAttributeList Members

        void IAttributeList.Add(INamedAttribute toAdd)
        {
            if (toAdd is ColorDef && Find(toAdd.Name) == null)
                Add((ColorDef)toAdd);
        }

        INamedAttribute IAttributeList.Item(int Index)
        {
            return namedColors[Index] as INamedAttribute;
        }
        void IAttributeList.AttributeChanged(INamedAttribute attribute, ReversibleChange change)
        {
            if (owner != null) owner.AttributeChanged(this, attribute, change);
        }
        bool IAttributeList.MayChangeName(INamedAttribute attribute, string newName)
        {
            ColorDef cd = attribute as ColorDef;
            if (cd.Name == newName) return true;
            return Find(newName) == null;
        }
        void IAttributeList.NameChanged(INamedAttribute Attribute, string oldName)
        {

        }
        void IAttributeList.Initialize()
        {
            namedColors.Clear();
            string defaultColorList = StringTable.GetString("ColorList.Default");
            string[] split = defaultColorList.Split(new char[] { defaultColorList[0] });
            foreach (string cdef in split)
            {
                if (cdef.Length > 0)
                {
                    int ind = cdef.IndexOf('(');
                    if (ind > 0)
                    {
                        string Name = cdef.Substring(0, ind);
                        int ind1 = cdef.IndexOf(')', ind);
                        if (ind1 > ind)
                        {
                            string rgb = cdef.Substring(ind + 1, ind1 - ind - 1);
                            string[] colors = rgb.Split(',');
                            if (colors.Length == 3)
                            {
                                try
                                {
                                    AddColor(Name, Color.FromArgb(int.Parse(colors[0]), int.Parse(colors[1]), int.Parse(colors[2])));
                                }
                                catch (FormatException)
                                {	// läuft nicht, wenn falsch formatiert
                                }
                            }
                        }
                    }
                }
            }
        }

        IAttributeList IAttributeList.Clone() { return clone() as IAttributeList; }
        void IAttributeList.Update(bool AddMissingToList) { }
        void IAttributeList.Update(IGeoObject Object2Update)
        {
            if (Object2Update is IColorDef)
            {
                ColorDef oldC = (Object2Update as IColorDef).ColorDef;
                if (oldC == null) return;
                ColorDef c = Find(oldC.Name);
                if (c == null)
                {
                    Add(oldC);
                }
                else
                {
                    (Object2Update as IColorDef).SetTopLevel(c, true);
                }
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
                return this[CurrentIndex] as INamedAttribute;
            }
        }
        #endregion

        #region ICommandHandler Members
        private void OnAddFromGlobal()
        {
            foreach (ColorDef ds in Settings.GlobalSettings.ColorList.namedColors)
            {
                if (Find(ds.Name) == null)
                {
                    this.Add(ds.Clone());
                }
            }
            if (propertyPage != null) propertyPage.Refresh(this);
        }
        private void OnMakeGlobal()
        {
            Settings.GlobalSettings.ColorList.RemoveAll();
            foreach (ColorDef ds in namedColors)
            {
                Settings.GlobalSettings.ColorList.Add(ds.Clone());
            }
        }
        private void OnUpdateFromProject()
        {
            Hashtable used = GetAllColorDefs();
            foreach (ColorDef ds in used.Keys)
            {
                if (this.Find(ds.Name) == null)
                {
                    this.Add(ds);
                }
            }
            if (propertyPage != null) propertyPage.Refresh(this);
        }
        private void OnRemoveUnused()
        {
            Hashtable used = GetAllColorDefs();
            bool found = false;
            do
            {
                found = false;
                if (namedColors.Count > 1)
                {
                    foreach (ColorDef ds in namedColors)
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
            if (propertyPage != null) propertyPage.Refresh(this);
        }
        private void GetAllColorDefs(Hashtable collect, IGeoObject go)
        {
            if (go is IColorDef cd && cd.ColorDef != null)
            {
                collect[cd.ColorDef] = null; // Hashtable wird als set verwendet
            }
            else if (go is Block)
            {
                foreach (IGeoObject child in (go as Block).Children)
                    GetAllColorDefs(collect, child);
            }
        }
        private Hashtable GetAllColorDefs()
        {
            Hashtable res = new Hashtable();
            foreach (Model m in Frame.Project)
            {
                foreach (IGeoObject go in m) GetAllColorDefs(res, go);
            }
            return res;
        }
        bool ICommandHandler.OnCommand(string MenuId)
        {
            switch (MenuId)
            {
                case "MenuId.ColorList.New":
                    OnNewColor();
                    return true;
                case "MenuId.ColorList.RemoveUnused":
                    OnRemoveUnused();
                    return true;
                case "MenuId.ColorList.UpdateFromProject":
                    OnUpdateFromProject();
                    return true;
                case "MenuId.ColorList.AddFromGlobal":
                    OnAddFromGlobal();
                    return true;
                case "MenuId.ColorList.MakeGlobal":
                    OnMakeGlobal();
                    return true;
            }
            return false;
        }
        bool ICommandHandler.OnUpdateCommand(string MenuId, CommandState CommandState)
        {
            // TODO:  Add ColorList.OnUpdateCommand implementation
            return false;
        }
        void ICommandHandler.OnSelected(MenuWithHandler selectedMenuItem, bool selected) { }

        public void SerializationDone(JsonSerialize jsonSerialize)
        {
            foreach (ColorDef cd in namedColors)
            {
                cd.Parent = this;
            }
        }

        public void GetObjectData(IJsonWriteData data)
        {
            data.AddProperty("NamedColors", namedColors);
            data.AddProperty("Current", current);
        }

        public void SetObjectData(IJsonReadData data)
        {
            namedColors = data.GetProperty<ArrayList>("NamedColors");
            current = data.GetProperty<ColorDef>("Current");
        }

        #endregion

    }
}
