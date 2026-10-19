using CADability.GeoObject;
using CADability.UserInterface;
using System;
using System.Collections;
using System.Drawing;
using Point = System.Drawing.Point;
using System.Runtime.Serialization;

namespace CADability.Attribute
{

    /// <summary>
    /// A table of named colours (<see cref="ColorDef"/> objects). The table is serializable and is usually kept in a
    /// <see cref="Project"/> or in the global <see cref="Settings"/>. It can be edited interactively in the property grid,
    /// where it also handles the context menu commands for its entries.
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
        /// <summary>
        /// Flags that say which of the special, non-RGB entries a colour selection may offer in addition to the named colours
        /// of the list (see <see cref="Usage"/> and <see cref="ColorSelectionProperty"/>). Not every object may use every
        /// kind of colour: only objects inside a <see cref="Block"/> can take the colour of their parent, for example.
        /// </summary>
        public enum StaticFlags
        {
            /// <summary>Only the named colours of the list.</summary>
            allowNone = 0x00,
            /// <summary>The placeholder <see cref="ColorDef.CDfromParent"/> ("of the block") is offered as well.</summary>
            allowFromParent = 0x01,
            /// <summary>The placeholder <see cref="ColorDef.CDfromStyle"/> ("of the style") is offered as well.</summary>
            allowFromStyle = 0x02,
            /// <summary>Both placeholders are offered (<see cref="allowFromParent"/> and <see cref="allowFromStyle"/>); this does not include <see cref="allowUndefined"/>.</summary>
            allowAll = 0x03,
            /// <summary>The selection offers an additional "undefined" entry, which stands for no colour (null).</summary>
            allowUndefined = 0x04
        };
        // nicht alle referentiellen ColorDef können von allen Objekte benutzt werder
        // allowAll ist nicht allow undefined
        private StaticFlags staticFlags;
        /// <summary>
        /// Creates an empty ColorList.
        /// </summary>
        public ColorList()
        {
            namedColors = new ArrayList();
            resourceIdInternal = "ColorList";
        }


        /// <summary>
        /// Creates a colour list with the colours defined by the string resource "ColorList.Default" (red, green and blue in
        /// the standard resources); the first colour is the current one. Used for the global settings when no colour list has
        /// been saved yet.
        /// </summary>
        /// <returns>the default colour list</returns>
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
        /// <summary>
        /// The <see cref="Project"/> or <see cref="Settings"/> that holds this list. It is set by the owner when the list is
        /// added to it; the list asks it before removing a colour (see <see cref="RemoveAt(int)"/>) and forwards changes of
        /// its colours to it.
        /// </summary>
        public IAttributeListContainer Owner
        {
            get { return owner; }
            set { owner = value; }
        }
        /// <summary>
        /// Adds a color, or overwrites an existing one if the name already exists.
        /// </summary>
        /// <param name="Name">Name of the color.</param>
        /// <param name="Color">RGB (or ARGB) value of the color.</param>
        /// <returns>Index of the new or modified entry.</returns>
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

        /// <summary>
        /// Adds a <see cref="ColorDef"/> to the end of the list and makes this list its parent. The first colour added becomes
        /// the current colour. Raises the <see cref="DidModifyEvent"/>.
        /// </summary>
        /// <param name="cd">the colour to add</param>
        /// <exception cref="NameAlreadyExistsException">when the list already contains an entry with the same name</exception>
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
        /// Removes the colour at the given index. If the list belongs to a project or to the settings, the owner is asked first
        /// and may refuse the removal (e.g. when the colour is still in use). Raises the <see cref="DidModifyEvent"/>.
        /// </summary>
        /// <param name="Index">Index of the colour to remove</param>
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
        /// Returns the index of the given name.
        /// </summary>
        /// <param name="Name">The name of the color to look for.</param>
        /// <returns>Index of the color or -1, if there is no color with this name in the table.</returns>
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
        /// <summary>
        /// Returns the name of the given index.
        /// </summary>
        /// <param name="Index">The index.</param>
        /// <returns>The name.</returns>
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
        /// <summary>
        /// Tells whether the given index addresses one of the static placeholder entries (<see cref="ColorDef.CDfromParent"/>,
        /// <see cref="ColorDef.CDfromStyle"/>), which follow the named colours in the indexing of <see cref="GetName(int)"/>
        /// and the indexer when enabled by <see cref="Usage"/>. Those entries cannot be renamed or recoloured.
        /// </summary>
        /// <param name="idx">the index to test</param>
        /// <returns>true for an index beyond the named colours</returns>
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
        /// <summary>
        /// The index of the current colour (see <see cref="Current"/>) in the indexing of <see cref="GetName(int)"/>. Setting
        /// it makes the entry at that index current; an index outside the valid range is ignored.
        /// </summary>
        public int CurrentIndex
        {
            get { return IndexOf(current.Name); }
            set
            {
                if (!isValidIndex(value)) return;
                current = this[value];
            }
        }
        /// <summary>
        /// The current colour: the colour that newly constructed objects get (see <see cref="Project.SetDefaults(IGeoObject)"/>),
        /// shown in bold in the control center. When no current colour has been set, the first colour of the list is used.
        /// Setting a ColorDef that is not yet in the list adds it; null is ignored.
        /// </summary>
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
        /// Returns the RGB colour of the entry at the given index. For an index outside the list the colour blue is returned.
        /// </summary>
        /// <param name="Index">Index of the entry</param>
        /// <returns>The colour</returns>
        public Color GetColor(int Index)
        {
            if (Index < 0 || Index >= namedColors.Count)
                return Color.Blue;
            return ((ColorDef)namedColors[Index]).Color;
        }
        /// <summary>
        /// Returns the <see cref="ColorDef"/> object at the given index. The index of a name can be found with the
        /// <seealso cref="IndexOf"/> method.
        /// </summary>
        /// <param name="Index">Index of the requested object</param>
        /// <returns>The ColorDef at that index</returns>
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

        /// <summary>
        /// Returns the first named colour of the list whose <see cref="ColorDef.Color"/> equals the given colour. Note that
        /// the equality of <c>Color</c> also compares whether it is a known (named) colour, so e.g. <c>Color.Red</c> does not
        /// match an entry created from the values (255, 0, 0).
        /// </summary>
        /// <param name="rgb">the colour value to look for</param>
        /// <returns>the ColorDef, or null when no entry has this colour</returns>
        public ColorDef FindColor(Color rgb)
        {
            foreach (ColorDef cd in namedColors)
            {
                if (cd.Color == rgb) return cd;
            }
            return null;
        }
        /// <summary>
        /// Returns the named colour with the given name; when there is none, a new <see cref="ColorDef"/> with that name and
        /// the given colour value is added to the list and returned. The colour value of an existing entry is left as it is
        /// (see <see cref="CreateOrModify(string, Color)"/>). This is the usual way to get a colour for an object:
        /// <c>project.ColorList.CreateOrFind("red", Color.Red)</c>.
        /// </summary>
        /// <param name="Name">the name of the colour; null for a new, automatically named entry (see <see cref="GetNewName()"/>)</param>
        /// <param name="Color">the colour value for a newly created entry</param>
        /// <returns>the existing or newly created ColorDef</returns>
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
        /// <summary>
        /// Returns the named colour with the given name, setting its colour value to <paramref name="Color"/>; when there is
        /// none, a new <see cref="ColorDef"/> with that name and colour is added to the list and returned.
        /// </summary>
        /// <param name="Name">the name of the colour</param>
        /// <param name="Color">the colour value</param>
        /// <returns>the existing (modified) or newly created ColorDef</returns>
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
        /// <inheritdoc/>
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
        /// <summary>
        /// Returns the index of the given ColorDef object in the list.
        /// </summary>
        /// <param name="cd">the ColorDef to look for (compared by reference)</param>
        /// <returns>the index, or -1 when the object is not in this list</returns>
        public int FindIndex(ColorDef cd)
        {
            for (int i = 0; i < namedColors.Count; ++i)
            {
                if (namedColors[i] == cd) return i;
            }
            return -1;
        }

        /// <summary>
        /// Returns the entry following the entry with the given name, to cycle through the colours of the list: after the
        /// last named colour comes <see cref="ColorDef.CDfromStyle"/> when <see cref="Usage"/> allows it, then the first
        /// entry again. Not used by CADability itself.
        /// </summary>
        /// <param name="Name">the name of the current entry</param>
        /// <returns>the next ColorDef; the first entry when the name is not found</returns>
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
        /// Changes the name of the entry at the given index. The change is rejected when the new name is already used by
        /// another entry of the list. Raises the <see cref="DidModifyEvent"/>.
        /// </summary>
        /// <param name="Index">Index of the entry to change</param>
        /// <param name="Name">The new name</param>
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
        /// Changes the RGB colour of the entry at the given index. Raises the <see cref="DidModifyEvent"/>.
        /// </summary>
        /// <param name="Index">Index of the entry to change</param>
        /// <param name="Color">The new colour value</param>
        public void SetColor(int Index, Color Color)
        {
            ((ColorDef)namedColors[Index]).Color = Color;
            if (DidModifyEvent != null) DidModifyEvent(null, null);
        }
        /// <summary>
        /// The names of all entries in the order of the list, followed by the names of the placeholders
        /// <see cref="ColorDef.CDfromParent"/> and <see cref="ColorDef.CDfromStyle"/> when <see cref="Usage"/> allows them.
        /// This is the list of choices of a <see cref="ColorSelectionProperty"/>.
        /// </summary>
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

        /// <summary>
        /// Controls whether the placeholders <see cref="ColorDef.CDfromParent"/> and <see cref="ColorDef.CDfromStyle"/> are
        /// visible through <see cref="Names"/>, <see cref="Find(string)"/>, <see cref="IndexOf(string)"/>,
        /// <see cref="GetName(int)"/> and the indexer, as if they were entries at the end of the list. A
        /// <see cref="ColorSelectionProperty"/> sets this temporarily to the flags it was constructed with.
        /// </summary>
        public StaticFlags Usage
        {
            get => staticFlags;
            set => staticFlags = value;
        }
        /// <summary>
        /// Builds a name that is not yet used in the list: <paramref name="startWith"/> followed by a number that is one
        /// greater than the highest number any existing name with this prefix ends with (e.g. "Color3" when "Color1" and
        /// "Color2" exist).
        /// </summary>
        /// <param name="startWith">the prefix of the name</param>
        /// <returns>the new name</returns>
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
        /// <summary>
        /// Builds a name for a new colour from the string resource "ColorList.NewColorName" ("Color" in the English
        /// resources) and a number, see <see cref="GetNewName(string)"/>. Used when a colour is created without a name.
        /// </summary>
        /// <returns>the new name</returns>
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

        /// <inheritdoc/>
        public override PropertyEntryType Flags
        {
            get
            {
                return PropertyEntryType.ContextMenu | PropertyEntryType.Selectable | PropertyEntryType.GroupTitle | PropertyEntryType.HasSubEntries;
            }
        }
        /// <inheritdoc/>
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
        /// <inheritdoc/>
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
        /// Writes the data of this object for the legacy binary serialization (<see cref="System.Runtime.Serialization.ISerializable"/>).
        /// Files are written in the JSON format (<see cref="IJsonSerialize"/>) now; the binary format is still read.
        /// </summary>
        /// <param name="info">receives the data</param>
        /// <param name="context">the destination of the serialization</param>
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

        /// <summary>
        /// Raised when a colour is added, removed, renamed or recoloured through the methods of this list
        /// (<see cref="INotifyModification"/>). Changes made directly on a <see cref="ColorDef"/> are forwarded to the
        /// <see cref="Owner"/> instead.
        /// </summary>
        public event CADability.DidModifyDelegate DidModifyEvent;

        #endregion

        #region ICollection Members

        /// <summary>
        /// Implements <see cref="ICollection.IsSynchronized"/>. The list is not synchronized, the value is false.
        /// </summary>
        public bool IsSynchronized
        {
            get
            {
                return namedColors.IsSynchronized;
            }
        }

        /// <summary>
        /// The number of named colours in the list; the placeholders enabled by <see cref="Usage"/> are not counted.
        /// </summary>
        public int Count
        {
            get
            {
                return namedColors.Count;
            }
        }

        /// <summary>
        /// Copies the named colours, in the order of the list, to the given array (<see cref="ICollection.CopyTo(Array, int)"/>).
        /// </summary>
        /// <param name="array">the destination array, which must be large enough</param>
        /// <param name="index">the index in <paramref name="array"/> at which copying begins</param>
        public void CopyTo(Array array, int index)
        {
            namedColors.CopyTo(array, index);
        }

        /// <summary>
        /// An object that can be used to lock the list for synchronized access (<see cref="ICollection.SyncRoot"/>).
        /// </summary>
        public object SyncRoot
        {
            get
            {
                return namedColors.SyncRoot;
            }
        }

        #endregion

        #region IEnumerable Members

        /// <summary>
        /// Enumerates the named colours in the order of the list, so <c>foreach (ColorDef cd in colorList)</c> can be used.
        /// The placeholders enabled by <see cref="Usage"/> are not included.
        /// </summary>
        /// <returns>an enumerator over the <see cref="ColorDef"/> objects</returns>
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

        /// <summary>
        /// Callback of the JSON serialization (<see cref="IJsonSerializeDone"/>): after all objects have been read, the back
        /// references of the colours to this list, which are not stored in the file, are restored.
        /// </summary>
        /// <param name="jsonSerialize">the serializer</param>
        public void SerializationDone(JsonSerialize jsonSerialize)
        {
            foreach (ColorDef cd in namedColors)
            {
                cd.Parent = this;
            }
        }

        /// <summary>
        /// Writes the colours and the current colour for the JSON serialization (<see cref="IJsonSerialize"/>).
        /// </summary>
        /// <param name="data">receives the data</param>
        public void GetObjectData(IJsonWriteData data)
        {
            data.AddProperty("NamedColors", namedColors);
            data.AddProperty("Current", current);
        }

        /// <summary>
        /// Restores the colours and the current colour from the JSON data written by <see cref="GetObjectData(IJsonWriteData)"/>
        /// (<see cref="IJsonSerialize"/>).
        /// </summary>
        /// <param name="data">the data to read from</param>
        public void SetObjectData(IJsonReadData data)
        {
            namedColors = data.GetProperty<ArrayList>("NamedColors");
            current = data.GetProperty<ColorDef>("Current");
            data.RegisterForSerializationDoneCallback(this);
        }

        #endregion

    }
}
