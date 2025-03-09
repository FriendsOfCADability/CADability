using CADability.GeoObject;
using CADability.UserInterface;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using System.Runtime.Serialization;
using CheckState = CADability.Substitutes.CheckState;

namespace CADability.Attribute
{
    /// <summary>
    /// Shows a list of Attributes as checkboxes.
    /// </summary>

    public class CheckedAttributes : IShowPropertyImpl, ICommandHandler
    {
        IAttributeList attributeList;
        Filter filter;
        public CheckedAttributes(string resourceId, IAttributeList attributeList, Filter filter)
        {
            base.resourceIdInternal = resourceId;
            this.attributeList = attributeList;
            this.filter = filter;
        }
        #region IShowProperty Members
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
                return ShowPropertyLabelFlags.ContextMenu | ShowPropertyLabelFlags.Selectable;
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
        IShowProperty[] subEntries;
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
                    subEntries = new IShowProperty[attributeList.Count];
                    for (int i = 0; i < attributeList.Count; ++i)
                    {
                        CheckProperty cp = new CheckProperty(this, "Checked", "CheckedAttribute.Entry"); // the methods GetChecked and SteChecked
                        cp.CheckStateChangedEvent += new CADability.UserInterface.CheckProperty.CheckStateChangedDelegate(OnCheckStateChanged);
                        cp.LabelText = attributeList.Item(i).Name;
                        subEntries[i] = cp;
                    }
                }
                return subEntries;
            }
        }
        public bool GetChecked(string name)
        {
            INamedAttribute na = attributeList.Find(name);
            if (na != null) return filter.Contains(na);
            return false;
        }
        public void SetChecked(string name, bool check)
        {
            INamedAttribute na = attributeList.Find(name);
            if (na != null)
            {
                if (check)
                {
                    filter.Add(na);
                }
                else
                {
                    filter.Remove(na);
                }
            }
        }
        List<IPropertyPage> propertyPages = new List<IPropertyPage>(); // the same filter is in the project and in the action propertypage
        public override void Added(IPropertyPage pp)
        {
            base.Added(pp);
            propertyPages.Add(pp);
        }
        public override void Removed(IPropertyPage pp)
        {
            base.Removed(pp);
            propertyPages.Remove(pp);
        }
        public override MenuWithHandler[] ContextMenu
        {
            get
            {
                return MenuResource.LoadMenuDefinition("MenuId.AttributeFilter", false, this);
            }
        }
        #endregion
        private void OnCheckStateChanged(string label, CheckState state)
        {
            if (state == CheckState.Checked)
            {
                filter.Add(attributeList.Find(label));
            }
            else
            {
                filter.Remove(attributeList.Find(label));
            }
        }
        #region ICommandHandler Members
        virtual public bool OnCommand(string MenuId)
        {
            switch (MenuId)
            {
                case "MenuId.AttributeFilter.SelectAll":
                    for (int i = 0; i < attributeList.Count; ++i)
                    {
                        filter.Add(attributeList.Item(i));
                    }
                    for (int i = 0; i < propertyPages.Count; i++)
                    {
                        for (int j = 0; j < SubEntries.Length; j++)
                        {
                            propertyPages[i].Refresh(SubEntries[j] as IPropertyEntry); // if they are not visible, this is ok
                        }
                    }
                    return true;
                case "MenuId.AttributeFilter.SelectNone":
                    for (int i = 0; i < attributeList.Count; ++i)
                    {
                        filter.Remove(attributeList.Item(i));
                    }
                    for (int i = 0; i < propertyPages.Count; i++)
                    {
                        for (int j = 0; j < SubEntries.Length; j++)
                        {
                            propertyPages[i].Refresh(SubEntries[j] as IPropertyEntry); // if they are not visible, this is ok
                        }
                    }
                    return true;
            }
            return false;
        }

        virtual public bool OnUpdateCommand(string MenuId, CommandState CommandState)
        {
            // TODO:  Add CheckedAttributes.OnUpdateCommand implementation
            return false;
        }
        void ICommandHandler.OnSelected(MenuWithHandler selectedMenuItem, bool selected) { }

        #endregion
    }

    internal class CheckedList : IShowPropertyImpl, ICommandHandler
    {
        bool[] states;
        object[] assoziatedObjects;
        string[] names;

        public CheckedList(string resourceId, bool[] states, object[] assoziatedObjects, string[] names)
        {
            base.resourceIdInternal = resourceId;
            this.states = states;
            this.assoziatedObjects = assoziatedObjects;
            this.names = names;
        }
        public delegate void ItemStateChangedDelegate(object assoz, bool state);
        public event ItemStateChangedDelegate ItemStateChangedEvent;
        #region IShowProperty Members
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
                return ShowPropertyLabelFlags.ContextMenu | ShowPropertyLabelFlags.Selectable;
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
        CheckProperty[] subEntries;
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
                    subEntries = new CheckProperty[states.Length];
                    for (int i = 0; i < states.Length; ++i)
                    {
                        CheckProperty cp = new CheckProperty(this, "Checked", "CheckedAttribute.Entry"); // the methods GetChecked and SteChecked
                        cp.CheckStateChangedEvent += new CADability.UserInterface.CheckProperty.CheckStateChangedDelegate(OnCheckStateChanged);
                        cp.LabelText = names[i];
                        subEntries[i] = cp;
                    }
                }
                Array.Sort<CheckProperty>(subEntries, delegate (CheckProperty cp1, CheckProperty cp2)
                {
                    return cp1.LabelText.CompareTo(cp2.LabelText);
                });
                return subEntries;
            }
        }
        public bool GetChecked(string name)
        {
            object selected = null;
            int ind = -1;
            for (int i = 0; i < names.Length; i++)
            {
                if (name == names[i])
                {
                    selected = assoziatedObjects[i];
                    ind = i;
                    break;
                }
            }
            if (ind>=0) return states[ind];
            return false;
        }
        public void SetChecked(string name, bool check)
        {
            object selected = null;
            int ind = -1;
            for (int i = 0; i < names.Length; i++)
            {
                if (name == names[i])
                {
                    selected = assoziatedObjects[i];
                    ind = i;
                    break;
                }
            }
            if (check)
            {
                ItemStateChangedEvent(selected, true);
                states[ind] = true;
            }
            else
            {
                ItemStateChangedEvent(selected, false);
                states[ind] = false;
            }
        }
        List<IPropertyPage> propertyPages = new List<IPropertyPage>(); // the same filter is in the project and in the action propertypage
        public override void Added(IPropertyPage pp)
        {
            base.Added(pp);
            propertyPages.Add(pp);
        }
        public override void Removed(IPropertyPage pp)
        {
            base.Removed(pp);
            propertyPages.Remove(pp);
        }
        public override MenuWithHandler[] ContextMenu
        {
            get
            {
                return MenuResource.LoadMenuDefinition("MenuId.AttributeFilter", false, this);
            }
        }
        #endregion
        private void OnCheckStateChanged(string label, CheckState state)
        {
            if (ItemStateChangedEvent != null)
            {
                object selected = null;
                int ind = -1;
                for (int i = 0; i < names.Length; i++)
                {
                    if (label == names[i])
                    {
                        selected = assoziatedObjects[i];
                        ind = i;
                        break;
                    }
                }
                if (state == CheckState.Checked)
                {
                    ItemStateChangedEvent(selected, true);
                    states[ind] = true;
                }
                else
                {
                    ItemStateChangedEvent(selected, false);
                    states[ind] = false;
                }
            }
        }
        #region ICommandHandler Members
        virtual public bool OnCommand(string MenuId)
        {
            switch (MenuId)
            {
                case "MenuId.AttributeFilter.SelectAll":
                    for (int i = 0; i < assoziatedObjects.Length; ++i)
                    {
                        if (ItemStateChangedEvent != null)
                        {
                            ItemStateChangedEvent(assoziatedObjects[i], true);
                        }
                        states[i] = true;
                    }
                    for (int i = 0; i < propertyPages.Count; i++)
                    {
                        for (int j = 0; j < SubEntries.Length; j++)
                        {
                            propertyPages[i].Refresh(SubEntries[j] as IPropertyEntry); // if they are not visible, this is ok
                        }
                    }
                    return true;
                case "MenuId.AttributeFilter.SelectNone":
                    for (int i = 0; i < assoziatedObjects.Length; ++i)
                    {
                        if (ItemStateChangedEvent != null)
                        {
                            ItemStateChangedEvent(assoziatedObjects[i], true);
                        }
                        states[i] = false;
                    }
                    for (int i = 0; i < propertyPages.Count; i++)
                    {
                        for (int j = 0; j < SubEntries.Length; j++)
                        {
                            propertyPages[i].Refresh(SubEntries[j] as IPropertyEntry); // if they are not visible, this is ok
                        }
                    }
                    return true;
            }
            return false;
        }

        virtual public bool OnUpdateCommand(string MenuId, CommandState CommandState)
        {
            // TODO:  Add CheckedAttributes.OnUpdateCommand implementation
            return false;
        }
        void ICommandHandler.OnSelected(MenuWithHandler selectedMenuItem, bool selected) { }

        #endregion
    }

    /// <summary>
    /// This class filters <see cref="IGeoObject"/>s according to their attributes or other properties.
    /// A <see cref="Project"/> contains a <see cref="FilterList"/> which contains Filters. The task of a filter
    /// is to decide, whether a IGeoObject is accepted by this filter or not. The <see cref="FilterList"/> is
    /// used by the <see cref="SelectObjectsAction"/> to decide, whether an object may be selected or not.
    /// Filters may also be used for other purposes. In this standard implementation the attributes of a IGeoObject
    /// and the type of the object are used to accept or reject an object. 
    /// You may derive a class from this Filter class and handle the static <see cref="Constructor"/> delegate
    /// by constructing your own filter. In your derived class you can use any criteria of a IGeoObject
    /// for filtering, e.g. the contents of <see cref="IGeoObject.UserData"/>.
    /// </summary>
    [Serializable]
    public class Filter : IShowPropertyImpl, ISerializable, ICommandHandler
    {
        // Die Daten für die Filter sind folgendermaßen organisiert:
        // für jedes AttributInterface (z.B. ILayer) gibt es eine 
        // HashTable, die als Keys nur die zugelassenen Attribute enthält
        // Values sind alle null. Mit "Contains" kann dann in der HashTable
        // gesucht werden. Ist eine HashTable leer, so wird sie nicht
        // verwendet, dieses Attribut wird also nicht überprüft.
        // Offen bleibt die Frage nach dem Verhalten für ein Objekt, welches
        // ein bestimmtes Attribut garnicht hat (z.B. IHatchStyle).
        private Hashtable acceptedLayers; // Liste der akzeptierten Layer
        private Hashtable acceptedColorDefs;
        private Hashtable acceptedLineWidths;
        private Hashtable acceptedLinePatterns;
        private Hashtable acceptedDimensionStyles;
        private Hashtable acceptedHatchStyles;
        private Hashtable acceptedTypes;
        private FilterList parent; // müsste immer in einer Liste sein, oder? Zugriff auf alle Attribute
        private string name;
        private bool isActive;
        private static Dictionary<Type, string> allGeoObjects = FindAllGeoObjects();
        private static Dictionary<Type, string> FindAllGeoObjects()
        {   // kommt nur einmal dran, es müssen dann schon alle Assemblies geladen sein, sonst werden
            // in den Assemblies der Anwender die Objekte nicht gefunden.
            try
            {
                Assembly ass = Assembly.GetEntryAssembly();
                //Module[] mod = ass.GetModules();
                List<AssemblyName> names = new List<AssemblyName>(ass.GetReferencedAssemblies());
                AssemblyName cadabilityAssembly = Assembly.GetAssembly(typeof(IGeoObject)).GetName();
                if (!names.Contains(cadabilityAssembly)) names.Add(cadabilityAssembly);
                List<Type> allTypes = new List<Type>();
                for (int i = 0; i < names.Count; i++)
                {
                    Assembly loaded = Assembly.Load(names[i]);
                    allTypes.AddRange(loaded.GetTypes());
                }
                List<Type> allGeoObjectTypes = new List<Type>();
                for (int i = 0; i < allTypes.Count; i++)
                {
                    Type tp = allTypes[i].GetInterface("IGeoObject");
                    if (tp != null)
                    {
                        bool defined = StringTable.IsStringDefined(allTypes[i].FullName + ".FilterType");
                        if (defined) allGeoObjectTypes.Add(allTypes[i]);
                    }
                }
                Dictionary<Type, string> res = new Dictionary<Type, string>();
                for (int i = 0; i < allGeoObjectTypes.Count; i++)
                {
                    if (allGeoObjectTypes[i] == typeof(IGeoObjectImpl)) continue;
                    bool defined = StringTable.IsStringDefined(allGeoObjectTypes[i].FullName + ".FilterType");
                    if (!defined)
                    {
                        res[allGeoObjectTypes[i]] = allGeoObjectTypes[i].FullName;
                    }
                    else
                    {
                        string name = StringTable.GetString(allGeoObjectTypes[i].FullName + ".FilterType");
                        if (name != "-")
                        {   // der Wert "-" ist die Möglichkeit Objekte auszuschließen
                            res[allGeoObjectTypes[i]] = name;
                        }
                    }
                }
                return res;
            }
            catch (System.Reflection.ReflectionTypeLoadException)
            {
                // MessageBox.Show(e.Message);
                return new Dictionary<Type, string>();
            }
            catch
            {
                return new Dictionary<Type, string>(); // leer, tritt manchmal in DebuggerViewer auf
            }
        }
#region polymorph construction
        /// <summary>
        /// Delegate definition for overridable constructor. Used only for custom filters.
        /// </summary>
        /// <returns>The new constructed Filter-derived class</returns>
        public delegate Filter ConstructionDelegate();
        /// <summary>
        /// Set this static delegate to your own method which creates your Filter-derived class.
        /// </summary>
        public static ConstructionDelegate Constructor;
        /// <summary>
        /// Constructs a ne Filter or Filter-derived object
        /// </summary>
        /// <returns>The newly constructed Filter</returns>
        public static Filter Construct()
        {
            if (Constructor != null) return Constructor();
            return new Filter();
        }
#endregion
        /// <summary>
        /// Constructs an empty filter which accepts everything
        /// </summary>
        protected Filter()
        {
            acceptedLayers = new Hashtable();
            acceptedColorDefs = new Hashtable();
            acceptedLineWidths = new Hashtable();
            acceptedLinePatterns = new Hashtable();
            acceptedDimensionStyles = new Hashtable();
            acceptedHatchStyles = new Hashtable();
            acceptedTypes = new Hashtable();
            base.resourceIdInternal = "Filter";
        }
        internal new FilterList Parent
        {
            set => parent = value;
        }
        /// <summary>
        /// Name of the Filter
        /// </summary>
        public string Name
        {
            get { return name; }
            set
            {
                if (parent != null && !(parent as INameChange).MayChangeName(this, value))
                {
                    throw new NameAlreadyExistsException(parent, this, value, name);
                }
                string OldName = name;
                name = value;
                if (parent != null) (parent as INameChange).NameChanged(this, OldName);
                // FireDidChange("Name",OldName); wohin?
            }
        }
        /// <summary>
        /// Gets or sets the active flag. Only active filters in a <see cref="FilterList"/> are used for filtering.
        /// </summary>
        public bool IsActive
        {
            get { return isActive; }
            set
            {
                isActive = value;
                if (propertyTreeView != null)
                {
                    propertyTreeView.Refresh(this);
                }
            }
        }
        /// <summary>
        /// Checks whether an <see cref="IGeoObject"/> is accepted by this filter.
        /// To realize custom filters, override this method.
        /// </summary>
        /// <param name="go">The object being tested</param>
        /// <returns>true if accepted, false otherwise</returns>
        public virtual bool Accept(IGeoObject go)
        {
            // jetzt so implementiert: wenn ein Attribut verlangt wird und ein Objekt nicht das
            // passende Interface hat, dann wird es nicht akzeptiert
            // wenn z.B. der Layer eines Objektes null ist, dann wird dieses von keinem Filter anerkannt
            if (acceptedLayers.Count > 0)
            {
                ILayer ilayer = go as ILayer;
                if (ilayer == null) return false;
                if (ilayer.Layer == null) return false;
                if (!acceptedLayers.ContainsKey(ilayer.Layer)) return false;
            }
            if (acceptedColorDefs.Count > 0)
            {
                IColorDef iColorDef = go as IColorDef;
                if (iColorDef == null) return false;
                if (iColorDef.ColorDef == null) return false;
                if (!acceptedColorDefs.ContainsKey(iColorDef.ColorDef)) return false;
            }
            if (acceptedLineWidths.Count > 0)
            {
                ILineWidth iLineWidth = go as ILineWidth;
                if (iLineWidth == null) return false;
                if (iLineWidth.LineWidth == null) return false;
                if (!acceptedLineWidths.ContainsKey(iLineWidth.LineWidth)) return false;
            }
            if (acceptedLinePatterns.Count > 0)
            {
                ILinePattern iLinePattern = go as ILinePattern;
                if (iLinePattern == null) return false;
                if (iLinePattern.LinePattern == null) return false;
                if (!acceptedLinePatterns.ContainsKey(iLinePattern.LinePattern)) return false;
            }
            if (acceptedDimensionStyles.Count > 0)
            {
                IDimensionStyle iDimensionStyle = go as IDimensionStyle;
                if (iDimensionStyle == null) return false;
                if (iDimensionStyle.DimensionStyle == null) return false;
                if (!acceptedDimensionStyles.ContainsKey(iDimensionStyle.DimensionStyle)) return false;
            }
            if (acceptedHatchStyles.Count > 0)
            {
                IHatchStyle iHatchStyle = go as IHatchStyle;
                if (iHatchStyle == null) return false;
                if (iHatchStyle.HatchStyle == null) return false;
                if (!acceptedHatchStyles.ContainsKey(iHatchStyle.HatchStyle)) return false;
            }
            if (acceptedTypes.Count > 0)
            {
                if (!acceptedTypes.ContainsKey(go.GetType())) return false;
            }
            return true;
        }
        /// <summary>
        /// Adds the provided attribute to the list of accepted attributes.
        /// CADability knows the following attributes: <see cref="Layer"/>, <see cref="Layer"/>, <see cref="ColorDef"/>, <see cref="LineWidth"/>, <see cref="LinePattern"/>,
        /// <see cref="DimensionStyle"/>, <see cref="HatchStyle"/>.
        /// If you have your own attributes which are unknown to CADability, you need to override this method to handle those
        /// attributes.
        /// </summary>
        /// <param name="atr">Attribute to be accepted</param>
        /// <returns>true, if attribute was known, false otherwise</returns>
        public virtual bool Add(INamedAttribute atr)
        {
            if (atr is Layer) acceptedLayers[atr as Layer] = null;
            else if (atr is ColorDef) acceptedColorDefs[atr as ColorDef] = null;
            else if (atr is LineWidth) acceptedLineWidths[atr as LineWidth] = null;
            else if (atr is LinePattern) acceptedLinePatterns[atr as LinePattern] = null;
            else if (atr is DimensionStyle) acceptedDimensionStyles[atr as DimensionStyle] = null;
            else if (atr is HatchStyle) acceptedHatchStyles[atr as HatchStyle] = null;
            else return false;
            return true;
        }
        /// <summary>
        /// Removes this attribute from the list of accepted attributes. For custom attributes see <see cref="Accept"/>
        /// </summary>
        /// <param name="atr">Attribute to be removed</param>
        /// <returns>true, if attribute was known, false otherwise</returns>
        public virtual bool Remove(INamedAttribute atr)
        {
            if (atr is Layer) acceptedLayers.Remove(atr as Layer);
            else if (atr is ColorDef) acceptedColorDefs.Remove(atr as ColorDef);
            else if (atr is LineWidth) acceptedLineWidths.Remove(atr as LineWidth);
            else if (atr is LinePattern) acceptedLinePatterns.Remove(atr as LinePattern);
            else if (atr is DimensionStyle) acceptedDimensionStyles.Remove(atr as DimensionStyle);
            else if (atr is HatchStyle) acceptedHatchStyles.Remove(atr as HatchStyle);
            else return false;
            return true;
        }
        /// <summary>
        /// Checks whether the provided attribute would be accepted
        /// </summary>
        /// <param name="atr">The attribute to check</param>
        /// <returns>true, if objects with this attribute would be accepted by this filter, false otherwise</returns>
        public virtual bool Contains(INamedAttribute atr)
        {
            if (atr is Layer) return acceptedLayers.ContainsKey(atr as Layer);
            else if (atr is ColorDef) return acceptedColorDefs.ContainsKey(atr as ColorDef);
            else if (atr is LineWidth) return acceptedLineWidths.ContainsKey(atr as LineWidth);
            else if (atr is LinePattern) return acceptedLinePatterns.ContainsKey(atr as LinePattern);
            else if (atr is DimensionStyle) return acceptedDimensionStyles.ContainsKey(atr as DimensionStyle);
            else if (atr is HatchStyle) return acceptedHatchStyles.ContainsKey(atr as HatchStyle);
            else return false;
        }
        /// <summary>
        /// Adds or removes the provided type to or from the list of accepted types. The type
        /// must be derived from IGeoObject.
        /// </summary>
        /// <param name="type">the type to add or remove</param>
        /// <param name="doAccept">true: accept this type, false: reject this type.</param>
        public virtual void AcceptType(Type type, bool doAccept)
        {
            if (doAccept)
            {
                acceptedTypes.Add(type, null);
            }
            else
            {
                acceptedTypes.Remove(type);
            }
        }
#region IShowProperty Members
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
                ShowPropertyLabelFlags res = ShowPropertyLabelFlags.ContextMenu | ShowPropertyLabelFlags.Selectable | ShowPropertyLabelFlags.Editable;
                if (isActive) res |= ShowPropertyLabelFlags.Bold;
                return res;
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
        IShowProperty[] subEntries;
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
                    subEntries = new IShowProperty[7];
                    subEntries[0] = new CheckedAttributes("Filter.LayerList", parent.AttributeListContainer.LayerList, this);
                    subEntries[1] = new CheckedAttributes("Filter.ColorList", parent.AttributeListContainer.ColorList, this);
                    subEntries[2] = new CheckedAttributes("Filter.LineWidthList", parent.AttributeListContainer.LineWidthList, this);
                    subEntries[3] = new CheckedAttributes("Filter.LinePatternList", parent.AttributeListContainer.LinePatternList, this);
                    subEntries[4] = new CheckedAttributes("Filter.HatchStyleList", parent.AttributeListContainer.HatchStyleList, this);
                    subEntries[5] = new CheckedAttributes("Filter.DimensionStyleList", parent.AttributeListContainer.DimensionStyleList, this);
                    bool[] val = new bool[allGeoObjects.Count];
                    object[] types = new object[allGeoObjects.Count];
                    string[] names = new string[allGeoObjects.Count];
                    int i = 0;
                    foreach (Type tp in allGeoObjects.Keys)
                    {
                        val[i] = acceptedTypes.ContainsKey(tp);
                        types[i] = tp;
                        names[i] = allGeoObjects[tp];
                        ++i;
                    }
                    CheckedList cl = new CheckedList("Filter.ObjectType", val, types, names);
                    cl.ItemStateChangedEvent += new CheckedList.ItemStateChangedDelegate(OnTypeItemStateChanged);
                    subEntries[6] = cl;
                }
                return subEntries;
            }
        }

        void OnTypeItemStateChanged(object type, bool state)
        {
            if (state)
            {
                if (!acceptedTypes.ContainsKey(type)) acceptedTypes.Add(type, null);
            }
            else
            {
                acceptedTypes.Remove(type);
            }
        }
        public override void StartEdit(bool editValue)
        {   // nothing to do here
        }
        public override void EndEdit(bool aborted, bool modified, string newValue)
        {
            if (!aborted && !string.IsNullOrWhiteSpace(newValue))
            {   // check, whether there already exists another filter with the same name
                try
                {
                    Name = newValue;
                }
                catch (NameAlreadyExistsException) { }
            }
        }
        public override string LabelText { get => Name; set => Name = value; }
        public override MenuWithHandler[] ContextMenu
        {
            get
            {
                return MenuResource.LoadMenuDefinition("MenuId.Filter", false, this);
            }
        }
        /// <summary>
        /// Overrides <see cref="IShowPropertyImpl.Added"/>
        /// </summary>
        /// <param name="propertyTreeView"></param>
        public override void Added(IPropertyPage propertyTreeView)
        {
            base.Added(propertyTreeView);
            base.LabelText = name;
            base.resourceIdInternal = "FilterName";
        }
        public override void LabelChanged(string NewText)
        {
            try
            {
                Name = NewText;
            }
            catch (NameAlreadyExistsException)
            {
            }
        }
#endregion
#region ISerializable Members
        /// <summary>
        /// Constructor required by deserialization
        /// </summary>
        /// <param name="info">SerializationInfo</param>
        /// <param name="context">StreamingContext</param>
        protected Filter(SerializationInfo info, StreamingContext context)
        {
            acceptedLayers = info.GetValue("AcceptedLayers", typeof(Hashtable)) as Hashtable;
            acceptedColorDefs = info.GetValue("AcceptedColorDefs", typeof(Hashtable)) as Hashtable;
            acceptedLineWidths = info.GetValue("AcceptedLineWidths", typeof(Hashtable)) as Hashtable;
            acceptedLinePatterns = info.GetValue("AcceptedLinePatterns", typeof(Hashtable)) as Hashtable;
            acceptedDimensionStyles = info.GetValue("AcceptedDimensionStyles", typeof(Hashtable)) as Hashtable;
            acceptedHatchStyles = info.GetValue("AcceptedHatchStyles", typeof(Hashtable)) as Hashtable;
            try
            {
                acceptedTypes = info.GetValue("AcceptedTypes", typeof(Hashtable)) as Hashtable;
            }
            catch (SerializationException)
            {
                acceptedTypes = new Hashtable();
            }
            name = info.GetValue("Name", typeof(string)) as string;
            isActive = (bool)info.GetValue("IsActive", typeof(bool));
        }
        /// <summary>
        /// Implements <see cref="ISerializable.GetObjectData"/>
        /// </summary>
        /// <param name="info">The <see cref="System.Runtime.Serialization.SerializationInfo"/> to populate with data.</param>
        /// <param name="context">The destination (<see cref="System.Runtime.Serialization.StreamingContext"/>) for this serialization.</param>
        public virtual void GetObjectData(SerializationInfo info, StreamingContext context)
        {
            info.AddValue("AcceptedLayers", acceptedLayers);
            info.AddValue("AcceptedColorDefs", acceptedColorDefs);
            info.AddValue("AcceptedLineWidths", acceptedLineWidths);
            info.AddValue("AcceptedLinePatterns", acceptedLinePatterns);
            info.AddValue("AcceptedDimensionStyles", acceptedDimensionStyles);
            info.AddValue("AcceptedHatchStyles", acceptedHatchStyles);
            info.AddValue("AcceptedTypes", acceptedTypes);
            info.AddValue("Name", name);
            info.AddValue("IsActive", isActive);
        }
#endregion
#region ICommandHandler Members
        bool ICommandHandler.OnCommand(string MenuId)
        {
            switch (MenuId)
            {
                case "MenuId.Filter.Active":
                    IsActive = !IsActive;
                    return true;
                case "MenuId.Filter.OnlyThis":
                    parent.DeactivateAll();
                    IsActive = true;
                    return true;
                case "MenuId.Filter.Rename":
                    if (propertyTreeView != null)
                    {
                        propertyTreeView.StartEditLabel(this);
                    }
                    return true;
                case "MenuId.Filter.Delete":
                    parent.Remove(this);
                    return true;
                default: return false;
            }
        }
        bool ICommandHandler.OnUpdateCommand(string MenuId, CommandState CommandState)
        {
            switch (MenuId)
            {
                case "MenuId.Filter.Active":
                    CommandState.Checked = IsActive;
                    return true;
                case "MenuId.Filter.OnlyThis":
                    return true;
                case "MenuId.Filter.Rename":
                    return true;
                case "MenuId.Filter.Delete":
                    return true;
                default: return false;
            }
        }
        void ICommandHandler.OnSelected(MenuWithHandler selectedMenuItem, bool selected) { }
        #endregion
    }
}
