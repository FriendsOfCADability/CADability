using CADability.GeoObject;
using CADability.UserInterface;
using System;
using System.Collections;
using System.Runtime.InteropServices;
using System.Runtime.Serialization;



namespace CADability.Attribute
{
    /// <summary>
    /// Interface to be implemented by objects that have a (changable) Style property
    /// </summary>

    public interface IStyle
    {
        /// <summary>
        /// The Style property
        /// </summary>
        Style Style
        {
            get;
            set;
        }
    }

    /// <summary>
    /// A Style is a collection of several attributes like <see cref="Layer"/>, <see cref="LineWidth"/>
    /// that can be collectively set to an GeoObject.
    /// </summary>
    [Serializable()]
    [ComVisible(true)]
    [GuidAttribute("5FC3B97D-716E-496B-AAB6-2891ABF1F248")]
    [ClassInterface(ClassInterfaceType.AutoDual)]
    public class Style : PropertyEntryImpl, ICloneable, ISerializable, INamedAttribute, IColorDef, ILayer, ILineWidth, ILinePattern, IHatchStyle, IDimensionStyle, ICommandHandler, IJsonSerialize
    {
        private StyleList parent;
        private ColorDef colorDef;
        private Layer layer;
        private DimensionStyle dimStyle;
        private string styleName;
        private HatchStyle hatchStyle;
        private LinePattern linePattern;
        private LineWidth lineWidth;
        /// <summary>
        /// Category of objects to provide different default styles
        /// </summary>
        public enum EDefaultFor { Curves = 1, Solids = 2, Text = 4, Dimension = 8, HiddenSolids = 16, Axis = 32 }
        private EDefaultFor defaultFor;
        private LineWidthSelectionProperty selectLineWidth;
        private HatchStyleSelectionProperty selectHatchStyle;
        private LinePatternSelectionProperty selectLinePattern;
        private DimensionStyleSelectionProperty selectDimStyle;
        public Style()
        {
        }
        public Style(string name)
            : this()
        {
            styleName = name;
        }
        internal static Style GetFromGeoObject(IGeoObject go)
        {   // wird nur verwendet um bei Konstruktionsaktionen den zuletzt verwendeten Stil zu merken
            // if (go.Style != null && go.Style.Check(go)) return go.Style; // der Stil stimmt
            // die Abfrage war schlecht: der Stil stimmte, die konkrete Ausführung an dem Objekt war aber anders
            Style res = new Style();
            res.layer = go.Layer;
            IColorDef icolorDef = go as IColorDef;
            if (icolorDef != null)
            {
                res.colorDef = icolorDef.ColorDef;
            }
            ILineWidth ilineWidth = go as ILineWidth;
            if (ilineWidth != null)
            {
                res.lineWidth = ilineWidth.LineWidth;
            }
            ILinePattern ilinePattern = go as ILinePattern;
            if (ilinePattern != null)
            {
                res.linePattern = ilinePattern.LinePattern;
            }
            IHatchStyle ihatchStyle = go as IHatchStyle;
            if (ihatchStyle != null)
            {
                res.hatchStyle = ihatchStyle.HatchStyle;
            }
            IDimensionStyle idimensionStyle = go as IDimensionStyle;
            if (idimensionStyle != null)
            {
                res.dimStyle = idimensionStyle.DimensionStyle;
            }
            res.Name = ""; // Name extra leer, damit der Stil nicht gesetzt wird
            return res;
        }
        /// <summary>
        /// Returns a default (empty) Style. Use this function if you need a Style
        /// and dont have access to a <see cref="StyleList"/>.
        /// </summary>
        /// <returns>The default Stalye</returns>
        public static Style GetDefault()
        {
            Style res = new Style(StringTable.GetString("Style.DefaultName"));
            return res;
        }
        /// <summary>
        /// Applies this style to the given GeoObject. Since the style knows better about the different
        /// kinds of interfaces and attributes, this is implemented in the Style class not in IGeoObjectImpl.
        /// </summary>
        /// <param name="go">the GeoObject which is to be modified</param>
        public void Apply(IGeoObject go)
        {
            if (layer != null)
            {	// alle Objekte haben Layer
                go.Layer = layer;
            }
            IColorDef icolorDef = go as IColorDef;
            if (colorDef != null && icolorDef != null)
            {
                if (icolorDef.ColorDef != colorDef)
                    icolorDef.ColorDef = colorDef;
            }
            ILineWidth ilineWidth = go as ILineWidth;
            if (lineWidth != null && ilineWidth != null)
            {
                if (ilineWidth.LineWidth != lineWidth)
                    ilineWidth.LineWidth = lineWidth;
            }
            ILinePattern ilinePattern = go as ILinePattern;
            if (linePattern != null && ilinePattern != null)
            {
                if (ilinePattern.LinePattern != linePattern)
                    ilinePattern.LinePattern = linePattern;
            }
            IHatchStyle ihatchStyle = go as IHatchStyle;
            if (hatchStyle != null && ihatchStyle != null)
            {
                if (ihatchStyle.HatchStyle != hatchStyle)
                    ihatchStyle.HatchStyle = hatchStyle;
            }
            IDimensionStyle idimensionStyle = go as IDimensionStyle;
            if (dimStyle != null && idimensionStyle != null)
            {
                if (idimensionStyle.DimensionStyle != dimStyle)
                    idimensionStyle.DimensionStyle = dimStyle;
            }
        }
        /// <summary>
        /// Checks whether the given GeoObject accords to this style
        /// </summary>
        /// <param name="go">the GeoObject to check</param>
        /// <returns>true, if all attributes are the same as given in the style</returns>
        public bool Check(IGeoObject go)
        {
            if (layer != null)
            {	// alle Objekte haben Layer
                if (go.Layer != layer) return false;
            }
            IColorDef icolorDef = go as IColorDef;
            if (colorDef != null && icolorDef != null)
            {
                if (icolorDef.ColorDef != colorDef) return false;
            }
            ILineWidth ilineWidth = go as ILineWidth;
            if (lineWidth != null && ilineWidth != null)
            {
                if (ilineWidth.LineWidth != lineWidth) return false;
            }
            ILinePattern ilinePattern = go as ILinePattern;
            if (linePattern != null && ilinePattern != null)
            {
                if (ilinePattern.LinePattern != linePattern) return false;
            }
            IHatchStyle ihatchStyle = go as IHatchStyle;
            if (hatchStyle != null && ihatchStyle != null)
            {
                if (ihatchStyle.HatchStyle != hatchStyle) return false;
            }
            IDimensionStyle idimensionStyle = go as IDimensionStyle;
            if (dimStyle != null && idimensionStyle != null)
            {
                if (idimensionStyle.DimensionStyle != dimStyle) return false;
            }
            return true;
        }
        public Style Clone()
        {
            Style res = new Style();
            res.styleName = styleName;
            // res.parent = parent;
            res.colorDef = colorDef;
            res.layer = layer;
            res.dimStyle = dimStyle;
            res.hatchStyle = hatchStyle;
            res.linePattern = linePattern;
            res.LineWidth = lineWidth;
            res.defaultFor = defaultFor;
            return res;
        }
        public EDefaultFor DefaultFor
        {
            get
            {
                return defaultFor;
            }
            set
            {
                defaultFor = value;
            }
        }
        public void SetDefaultFor(EDefaultFor toSet)
        {
            defaultFor |= toSet;
        }
        public void RemoveDefaultFor(EDefaultFor toRemove)
        {
            defaultFor &= ~toRemove;
        }
        public bool IsDefaultFor(EDefaultFor toTest)
        {
            return (defaultFor & toTest) != 0;
        }
        internal void Initialize()
        {
            if (parent == null || parent.Owner == null)
                return;
            ColorList cl = parent.Owner.ColorList;
            if (cl != null)
                colorDef = cl[0];
            LayerList ll = parent.Owner.LayerList;
            if (ll != null)
                layer = ll[0];
            DimensionStyleList dsl = parent.Owner.DimensionStyleList;
            if (dsl != null)
                dimStyle = dsl[0];
            HatchStyleList hsl = parent.Owner.HatchStyleList;
            if (hsl != null)
                hatchStyle = hsl[0];
            LinePatternList lpl = parent.Owner.LinePatternList;
            if (lpl != null)
                linePattern = lpl[0];
            LineWidthList lwl = parent.Owner.LineWidthList;
            if (lwl != null)
                lineWidth = lwl[0];


        }
        internal void Update(bool AddMissingToList)
        {
            if (parent == null || parent.Owner == null)
                return;
            if (colorDef != null)
            {
                ColorList cl = parent.Owner.ColorList;
                if (cl != null)
                {
                    ColorDef cd = cl.Find(colorDef.Name);
                    if (cd != null)
                        colorDef = cd;
                    else if (AddMissingToList)
                        cl.Add(colorDef);
                }
            }
            if (layer != null)
            {
                LayerList ll = parent.Owner.LayerList;
                if (ll != null)
                {
                    Layer l = ll.Find(layer.Name);
                    if (l != null)
                        layer = l;
                    else if (AddMissingToList)
                        ll.Add(layer);
                }

            }
            if (dimStyle != null)
            {
                DimensionStyleList dsl = parent.Owner.DimensionStyleList;
                if (dsl != null)
                {
                    DimensionStyle ds = dsl.Find(dimStyle.Name);
                    if (ds != null)
                        dimStyle = ds;
                    else if (AddMissingToList)
                        dsl.Add(dimStyle);
                }
            }
            if (hatchStyle != null)
            {
                HatchStyleList hsl = parent.Owner.HatchStyleList;
                if (hsl != null)
                {
                    HatchStyle hs = hsl.Find(hatchStyle.Name);
                    if (hs != null)
                        hatchStyle = hs;
                    else if (AddMissingToList)
                        hsl.Add(hatchStyle);
                }
            }
            if (linePattern != null)
            {
                LinePatternList lpl = parent.Owner.LinePatternList;
                if (lpl != null)
                {
                    LinePattern lp = lpl.Find(linePattern.Name);
                    if (lp != null)
                        linePattern = lp;
                    else if (AddMissingToList)
                        lpl.Add(linePattern);
                }
            }
            if (lineWidth != null)
            {
                LineWidthList lwl = parent.Owner.LineWidthList;
                if (lwl != null)
                {
                    LineWidth lw = lwl.Find(lineWidth.Name);
                    if (lw != null)
                        lineWidth = lw;
                    else if (AddMissingToList)
                        lwl.Add(lineWidth);
                }
            }
        }

        #region Properties
        /// <summary>
        /// The name of the Style. Different Styles in the same list must have different names.
        /// </summary>
        public string Name
        {
            get { return styleName; }
            set
            {
                if (parent != null && !(parent as IAttributeList).MayChangeName(this, value))
                {
                    throw new NameAlreadyExistsException(parent, this, value, styleName);
                }
                string OldName = styleName;
                styleName = value;
                // FireDidChange("Name",OldName);
                if (parent != null) (parent as IAttributeList).NameChanged(this, OldName);
            }
        }
        /// <summary>
        /// The <see cref="StyleList"/> that contains this Style. May be null.
        /// </summary>
        public new StyleList Parent
        {
            get
            {
                return parent;
            }
            set
            {
                parent = value;
            }
        }
        IAttributeList INamedAttribute.Parent
        {
            get { return parent; }
            set { parent = value as StyleList; }
        }
        IPropertyEntry INamedAttribute.GetSelectionProperty(string key, Project project, GeoObjectList geoObjectList)
        {
            return null;
        }
        #endregion
        #region PropertyEntryImpl Overrides
        /// <summary>
        /// Implements <see cref="PropertyEntryImpl.LabelText"/>.
        /// </summary>
        public override string LabelText
        {
            get
            {
                return styleName;
            }
            set
            {
                Name = value;
            }
        }
        public override PropertyEntryType Flags
        {
            get
            {
                PropertyEntryType flags = PropertyEntryType.LabelEditable | PropertyEntryType.Selectable | PropertyEntryType.ContextMenu | PropertyEntryType.GroupTitle | PropertyEntryType.HasSubEntries;
                if ((parent != null) && (parent.Current == this))
                    flags |= PropertyEntryType.Bold;
                return flags;
            }
        }
        private IPropertyEntry[] subEntries;
        /// <summary>
        /// Overrides <see cref="PropertyEntryImpl.SubItems"/>,
        /// returns the subentries in this property view.
        /// </summary>
        public override IPropertyEntry[] SubItems
        {
            get
            {
                if (subEntries == null)
                {
                    subEntries = new IPropertyEntry[6];
                    parent.Owner.LayerList.DidModifyEvent += new DidModifyDelegate(OnListDidModify);
                    subEntries[0] = new LayerSelectionProperty(this, "Style.Layer", parent.Owner.LayerList, true);
                    parent.Owner.ColorList.DidModifyEvent += new DidModifyDelegate(OnListDidModify);
                    subEntries[1] = new ColorSelectionProperty(this, "Style.ColorDef", parent.Owner.ColorList, ColorList.StaticFlags.allowFromParent | ColorList.StaticFlags.allowUndefined);
                    // Color und Layer events gehen über Reflection
                    parent.Owner.LineWidthList.DidModifyEvent += new DidModifyDelegate(OnListDidModify);
                    selectLineWidth = new LineWidthSelectionProperty("Style.LineWidth", parent.Owner.LineWidthList, lineWidth, true);
                    selectLineWidth.LineWidthChangedEvent += new CADability.UserInterface.LineWidthSelectionProperty.LineWidthChangedDelegate(OnLineWidthChanged);
                    subEntries[2] = selectLineWidth;
                    parent.Owner.LinePatternList.DidModifyEvent += new DidModifyDelegate(OnListDidModify);
                    selectLinePattern = new LinePatternSelectionProperty("Style.LinePattern", parent.Owner.LinePatternList, linePattern, true);
                    selectLinePattern.LinePatternChangedEvent += new CADability.UserInterface.LinePatternSelectionProperty.LinePatternChangedDelegate(OnLinePatternChanged);
                    subEntries[3] = selectLinePattern;
                    parent.Owner.HatchStyleList.DidModifyEvent += new DidModifyDelegate(OnListDidModify);
                    selectHatchStyle = new HatchStyleSelectionProperty("Style.HatchStyle", parent.Owner.HatchStyleList, hatchStyle, true);
                    selectHatchStyle.HatchStyleChangedEvent += new CADability.UserInterface.HatchStyleSelectionProperty.HatchStyleChanged(OnHatchStyleChanged);
                    subEntries[4] = selectHatchStyle;
                    parent.Owner.DimensionStyleList.DidModifyEvent += new DidModifyDelegate(OnListDidModify);
                    selectDimStyle = new DimensionStyleSelectionProperty("Style.DimensionStyle", parent.Owner.DimensionStyleList, this, CADability.GeoObject.Dimension.EDimType.DimAll, true);
                    // DimensionStyle event geht über IDimensionStyle
                    subEntries[5] = selectDimStyle;
                }
                return subEntries;
            }
        }

        void LineWidthList_RemovingFromListEvent(IAttributeList Sender, RemovingFromListEventArgs EventArg)
        {
            throw new Exception("The method or operation is not implemented.");
        }

        void OnListDidModify(object sender, EventArgs args)
        {
            if (propertyPage != null)
            {
                if (parent.Owner != null)
                {
                    parent.Owner.ColorList.DidModifyEvent -= new DidModifyDelegate(OnListDidModify);
                    parent.Owner.LineWidthList.DidModifyEvent -= new DidModifyDelegate(OnListDidModify);
                    parent.Owner.LinePatternList.DidModifyEvent -= new DidModifyDelegate(OnListDidModify);
                    parent.Owner.HatchStyleList.DidModifyEvent -= new DidModifyDelegate(OnListDidModify);
                    parent.Owner.DimensionStyleList.DidModifyEvent -= new DidModifyDelegate(OnListDidModify);
                }
                if (selectLineWidth != null)
                    selectLineWidth.LineWidthChangedEvent -= new CADability.UserInterface.LineWidthSelectionProperty.LineWidthChangedDelegate(OnLineWidthChanged);
                if (selectLinePattern != null)
                    selectLinePattern.LinePatternChangedEvent -= new CADability.UserInterface.LinePatternSelectionProperty.LinePatternChangedDelegate(OnLinePatternChanged);
                if (selectHatchStyle != null)
                    selectHatchStyle.HatchStyleChangedEvent -= new CADability.UserInterface.HatchStyleSelectionProperty.HatchStyleChanged(OnHatchStyleChanged);
                this.subEntries = null;
                propertyPage.Refresh(this);
            }
        }

        /// <summary>
        /// Overrides <see cref="PropertyEntryImpl.Added"/>
        /// </summary>
        /// <param name="propertyPage"></param>
        public override void Added(IPropertyPage propertyPage)
        {
            base.Added(propertyPage);
            base.resourceIdInternal = "StyleName";
        }
        public override void EndEdit(bool aborted, bool modified, string newValue)
        {
            try
            {
                Name = newValue;
                if (propertyPage != null) propertyPage.Refresh(this);
            }
            catch (NameAlreadyExistsException) { }
        }
        public override MenuWithHandler[] ContextMenu
        {
            get
            {
                return MenuResource.LoadMenuDefinition("MenuId.StyleEntry", false, this);
            }
        }
        #endregion
        #region ICloneable Members
        object ICloneable.Clone()
        {
            return this.Clone();
        }
        #endregion
        #region ISerializable Members
        /// <summary>
        /// Constructor required by deserialization
        /// </summary>
        /// <param name="info">SerializationInfo</param>
        /// <param name="context">StreamingContext</param>
        protected Style(SerializationInfo info, StreamingContext context)
        {
            styleName = info.GetValue("Name", typeof(string)) as string;
            layer = info.GetValue("Layer", typeof(Layer)) as Layer;
            colorDef = info.GetValue("ColorDef", typeof(ColorDef)) as ColorDef;
            dimStyle = info.GetValue("DimStyle", typeof(DimensionStyle)) as DimensionStyle;
            hatchStyle = info.GetValue("HatchStyle", typeof(HatchStyle)) as HatchStyle;
            parent = info.GetValue("Parent", typeof(StyleList)) as StyleList;
            try
            {
                linePattern = info.GetValue("LinePattern", typeof(LinePattern)) as LinePattern;
                lineWidth = info.GetValue("LineWidth", typeof(LineWidth)) as LineWidth;
                defaultFor = (EDefaultFor)info.GetValue("DefaultFor", typeof(EDefaultFor));
            }
            catch (SerializationException)
            {
                linePattern = null;
                lineWidth = null;
                defaultFor = (EDefaultFor)0;
            }
        }
        /// <summary>
        /// Implements <see cref="ISerializable.GetObjectData"/>
        /// </summary>
        /// <param name="info">The <see cref="System.Runtime.Serialization.SerializationInfo"/> to populate with data.</param>
        /// <param name="context">The destination (<see cref="System.Runtime.Serialization.StreamingContext"/>) for this serialization.</param>
        public void GetObjectData(SerializationInfo info, StreamingContext context)
        {
            info.AddValue("Name", styleName);
            info.AddValue("Layer", layer);
            info.AddValue("ColorDef", colorDef);
            info.AddValue("LinePattern", linePattern);
            info.AddValue("LineWidth", lineWidth);
            info.AddValue("DimStyle", dimStyle);
            info.AddValue("HatchStyle", hatchStyle);
            info.AddValue("Parent", parent);
            info.AddValue("DefaultFor", defaultFor);
        }
        public void GetObjectData(IJsonWriteData data)
        {
            data.AddProperty("Name", styleName);
            if (layer != null) data.AddProperty("Layer", layer);
            if (colorDef != null) data.AddProperty("ColorDef", colorDef);
            if (linePattern != null) data.AddProperty("LinePattern", linePattern);
            if (lineWidth != null) data.AddProperty("LineWidth", lineWidth);
            if (dimStyle != null) data.AddProperty("DimStyle", dimStyle);
            if (hatchStyle != null) data.AddProperty("HatchStyle", hatchStyle);
            if (parent != null) data.AddProperty("Parent", parent);
            data.AddProperty("DefaultFor", defaultFor);
        }

        public void SetObjectData(IJsonReadData data)
        {
            styleName = data.GetPropertyOrDefault<string>("Name");
            layer = data.GetPropertyOrDefault<Layer>("Layer");
            colorDef = data.GetPropertyOrDefault<ColorDef>("ColorDef");
            linePattern = data.GetPropertyOrDefault<LinePattern>("LinePattern");
            lineWidth = data.GetPropertyOrDefault<LineWidth>("LineWidth");
            dimStyle = data.GetPropertyOrDefault<DimensionStyle>("DimStyle");
            hatchStyle = data.GetPropertyOrDefault<HatchStyle>("HatchStyle");
            parent = data.GetPropertyOrDefault<StyleList>("Parent");
            defaultFor = data.GetPropertyOrDefault<EDefaultFor>("DefaultFor");
        }
        #endregion
        #region AttributeInterface Members
        public ColorDef ColorDef
        {
            get
            {
                return colorDef;
            }
            set
            {
                object[] param = new object[] { colorDef };
                colorDef = value;
                if (parent != null)
                    (parent as IAttributeList).AttributeChanged(this, new ReversibleChange(this, "ColorDef", param));
            }
        }
        void IColorDef.SetTopLevel(ColorDef newValue)
        {
            throw new ApplicationException("Style.IColorDef.SetTopLevel Should not be called");
        }
        void IColorDef.SetTopLevel(ColorDef newValue, bool overwriteChildNullColor)
        {
            throw new ApplicationException("Style.IColorDef.SetTopLevel Should not be called");
        }

        public Layer Layer
        {
            get
            {
                // TODO:  Add Style.Layer getter implementation
                return layer;
            }
            set
            {
                object[] param = new object[] { layer };
                layer = value;
                if (parent != null)
                    (parent as IAttributeList).AttributeChanged(this, new ReversibleChange(this, "Layer", param));
            }
        }

        public LineWidth LineWidth
        {
            get
            {
                return lineWidth;
            }
            set
            {
                object[] param = new object[] { lineWidth };
                lineWidth = value;
                if (parent != null)
                    (parent as IAttributeList).AttributeChanged(this, new ReversibleChange(this, "LineWidth", param));

            }
        }

        public LinePattern LinePattern
        {
            get
            {
                return linePattern;
            }
            set
            {
                object[] param = new object[] { linePattern };
                linePattern = value;
                if (parent != null)
                    (parent as IAttributeList).AttributeChanged(this, new ReversibleChange(this, "LinePattern", param));

            }
        }
        public HatchStyle HatchStyle
        {
            get
            {
                return hatchStyle;
            }
            set
            {
                object[] param = new object[] { hatchStyle };
                hatchStyle = value;
                if (parent != null)
                    (parent as IAttributeList).AttributeChanged(this, new ReversibleChange(this, "HatchStyle", param));
            }
        }
        public DimensionStyle DimensionStyle
        {
            get { return dimStyle; }
            set
            {
                object[] param = new object[] { dimStyle };
                dimStyle = value;
                if (parent != null)
                    (parent as IAttributeList).AttributeChanged(this, new ReversibleChange(this, "DimensionStyle", param));
            }
        }
        #endregion
        private void OnLineWidthChanged(LineWidth selected)
        {
            LineWidth = selected;
        }
        private void OnHatchStyleChanged(HatchStyle SelectedHatchstyle)
        {
            HatchStyle = SelectedHatchstyle;
        }
        private void OnLinePatternChanged(LinePattern selected)
        {
            LinePattern = selected;
        }
        #region ICommandHandler Members
        bool ICommandHandler.OnCommand(string MenuId)
        {
            switch (MenuId)
            {
                case "MenuId.StyleEntry.Delete":
                    parent.Remove(this);
                    return true;
                case "MenuId.StyleEntry.Edit":
                    propertyPage.StartEditLabel(this); // muss ja offen sein
                    return true;
                case "MenuId.StyleEntry.DefaultForCurves":
                    if (IsDefaultFor(EDefaultFor.Curves)) RemoveDefaultFor(EDefaultFor.Curves);
                    else SetDefaultFor(EDefaultFor.Curves);
                    parent.CheckDefault(this);
                    return true;
                case "MenuId.StyleEntry.DefaultForSolids":
                    if (IsDefaultFor(EDefaultFor.Solids)) RemoveDefaultFor(EDefaultFor.Solids);
                    else SetDefaultFor(EDefaultFor.Solids);
                    parent.CheckDefault(this);
                    return true;
                case "MenuId.StyleEntry.DefaultForText":
                    if (IsDefaultFor(EDefaultFor.Text)) RemoveDefaultFor(EDefaultFor.Text);
                    else SetDefaultFor(EDefaultFor.Text);
                    parent.CheckDefault(this);
                    return true;
                case "MenuId.StyleEntry.DefaultForDimension":
                    if (IsDefaultFor(EDefaultFor.Dimension)) RemoveDefaultFor(EDefaultFor.Dimension);
                    else SetDefaultFor(EDefaultFor.Dimension);
                    parent.CheckDefault(this);
                    return true;
                case "MenuId.StyleEntry.DefaultForHiddenSolids":
                    if (IsDefaultFor(EDefaultFor.HiddenSolids)) RemoveDefaultFor(EDefaultFor.HiddenSolids);
                    else SetDefaultFor(EDefaultFor.HiddenSolids);
                    parent.CheckDefault(this);
                    return true;
                case "MenuId.StyleEntry.DefaultForAxis":
                    if (IsDefaultFor(EDefaultFor.Axis)) RemoveDefaultFor(EDefaultFor.Axis);
                    else SetDefaultFor(EDefaultFor.Axis);
                    parent.CheckDefault(this);
                    return true;
            }
            return false;
        }
        bool ICommandHandler.OnUpdateCommand(string MenuId, CommandState CommandState)
        {
            switch (MenuId)
            {
                case "MenuId.StyleEntry.DefaultForCurves":
                    CommandState.Checked = IsDefaultFor(EDefaultFor.Curves);
                    return true;
                case "MenuId.StyleEntry.DefaultForSolids":
                    CommandState.Checked = IsDefaultFor(EDefaultFor.Solids);
                    return true;
                case "MenuId.StyleEntry.DefaultForText":
                    CommandState.Checked = IsDefaultFor(EDefaultFor.Text);
                    return true;
                case "MenuId.StyleEntry.DefaultForDimension":
                    CommandState.Checked = IsDefaultFor(EDefaultFor.Dimension);
                    return true;
                case "MenuId.StyleEntry.DefaultForHiddenSolids":
                    CommandState.Checked = IsDefaultFor(EDefaultFor.HiddenSolids);
                    return true;
                case "MenuId.StyleEntry.DefaultForAxis":
                    CommandState.Checked = IsDefaultFor(EDefaultFor.Axis);
                    return true;
            }
            return false;
        }
        void ICommandHandler.OnSelected(MenuWithHandler selectedMenuItem, bool selected) { }
        #endregion
    }


    public class StyleSelectionProperty : MultipleChoiceProperty
    {
        private IStyle objectWithStyle;
        private StyleList styleList;
        private IGeoObject toWatch;
        public StyleSelectionProperty(IStyle objectWithStyle, string resourceId, StyleList styleList)
        {
            this.objectWithStyle = objectWithStyle;
            base.resourceIdInternal = resourceId;
            this.styleList = styleList;
            choices = styleList.Names;
            if (objectWithStyle != null && objectWithStyle.Style != null)
            {
                base.SelectedText = objectWithStyle.Style.Name;
            }
            toWatch = objectWithStyle as IGeoObject;
            if (toWatch != null && objectWithStyle.Style != null)
            {
                if (objectWithStyle.Style.Check(toWatch))
                {
                    base.unselectedText = null;
                }
                else
                {
                    base.SelectedText = null;
                    base.unselectedText = (toWatch as IStyle).Style.Name;
                }
            }
        }
        protected override void OnSelectionChanged(string selected)
        {
            base.OnSelectionChanged(selected);
            if (selected == null) return;
            Style stl = styleList.Find(selected);
            if (objectWithStyle != null) objectWithStyle.Style = stl;
            if (StyleChangedEvent != null) StyleChangedEvent(stl);
        }
        /// <summary>
        /// Overrides <see cref="PropertyEntryImpl.Added"/>
        /// </summary>
        /// <param name="propertyPage"></param>
        public override void Added(IPropertyPage propertyPage)
        {
            base.Added(propertyPage);
            if (toWatch != null) toWatch.DidChangeEvent += new ChangeDelegate(GeoObjectDidChange);
        }
        /// <summary>
        /// Overrides <see cref="PropertyEntryImpl.Removed"/>
        /// </summary>
        /// <param name="propertyPage">the IPropertyTreeView from which it was removed</param>
        public override void Removed(IPropertyPage propertyPage)
        {
            base.Removed(propertyPage);
            if (toWatch != null) toWatch.DidChangeEvent -= new ChangeDelegate(GeoObjectDidChange);
        }
        private void GeoObjectDidChange(IGeoObject Sender, GeoObjectChange Change)
        {
            if (Sender == toWatch && Change.OnlyAttributeChanged && propertyPage != null)
            {
                if ((toWatch as IStyle).Style != null)
                {
                    if ((toWatch as IStyle).Style.Check(toWatch))
                    {
                        base.SelectedText = (toWatch as IStyle).Style.Name;
                        base.unselectedText = null;
                    }
                    else
                    {
                        base.unselectedText = (toWatch as IStyle).Style.Name;
                        base.SelectedText = null;
                    }
                    propertyPage.Refresh(this);
                }
            }
        }
        public IGeoObject Connected
        {   // mit dieser Property kann man das kontrollierte Geoobjekt ändern
            get { return toWatch; }
            set
            {
                if (base.propertyPage != null)
                {   // dann ist diese Property schon Added und nicht removed
                    if (toWatch != null) toWatch.DidChangeEvent -= new ChangeDelegate(GeoObjectDidChange);
                }
                toWatch = value;
                objectWithStyle = value as IStyle;
                if (toWatch != null) toWatch.DidChangeEvent += new ChangeDelegate(GeoObjectDidChange);
            }
        }
        public delegate void StyleChangedDelegate(Style selected);
        public event StyleChangedDelegate StyleChangedEvent;
    }

    [Serializable()]
    public class StyleList : PropertyEntryImpl, ICloneable, ISerializable, IAttributeList, ICommandHandler, IDeserializationCallback, IJsonSerialize, IJsonSerializeDone
    {
        private Style[] unsortedEntries;
        protected SortedList styles;
        private IAttributeListContainer owner;
        private Style current;
        private IPropertyEntry[] showProperties;
        internal string menuResourceId;

        public StyleList()
        {
            styles = new SortedList();
            resourceIdInternal = "StyleList";
        }
        public static StyleList GetDefault(IAttributeListContainer container)
        {
            StyleList res = new StyleList();

            string defaultStyleList = StringTable.GetString("StyleList.Default");
            string[] split = defaultStyleList.Split(new char[] { defaultStyleList[0] });
            int def = 0;
            foreach (string styledesc in split)
            {
                if (styledesc.Length > 0)
                {
                    string[] desc = styledesc.Split(':');
                    if (desc.Length == 2)
                    {
                        string[] attr = desc[1].Split(',');
                        if (attr.Length == 4)
                        {
                            Style style = new Style(desc[0]);
                            if (def < 4)
                                style.DefaultFor = (Style.EDefaultFor)(1 << def);
                            else
                                style.DefaultFor = (Style.EDefaultFor)(0);
                            style.Layer = container.LayerList.Find(attr[0]);
                            style.ColorDef = container.ColorList.Find(attr[1]);
                            style.LineWidth = container.LineWidthList.Find(attr[2]);
                            style.LinePattern = container.LinePatternList.Find(attr[3]);
                            res.Add(style);
                        }
                    }
                    ++def;
                }
            }
            return res;
        }
        public IAttributeListContainer Owner
        {
            get { return owner; }
            set { owner = value; }
        }
        public void Add(Style ToAdd)
        {
            ToAdd.Parent = this;
            styles.Add(ToAdd.Name, ToAdd);
            if (styles.Count == 1)
                current = ToAdd;
        }
        public void Remove(Style toRemove)
        {
            if (styles.Contains(toRemove.Name))
            {
                if (owner != null)
                {
                    if (!owner.RemovingItem(this, toRemove, "StyleList")) return;
                }
                styles.Remove(toRemove.Name);
                toRemove.Parent = null;
                showProperties = null;
                // if (DidModify!=null) DidModify(null,null);
                if (propertyPage != null)
                {
                    showProperties = null;
                    propertyPage.Refresh(this);
                }
            }
        }
        private void RemoveAll() // nur intern verwenden, da nicht RemovingItem aufgerufen wird
        {
            styles.Clear();
            showProperties = null;
            if (propertyPage != null) propertyPage.OpenSubEntries(this, false); // zuklappen
        }
        /// <summary>
        /// Implements <see cref="CADability.Attribute.IAttributeList.Find (string)"/>
        /// </summary>
        /// <param name="Name"></param>
        /// <returns></returns>
        public Style Find(string Name)
        {
            return styles[Name] as Style;
        }
        public Style Current
        {
            get { return current; }
            set
            {
                current = value;
                if (!styles.ContainsKey(value.Name))
                    styles.Add(value.Name, value);
                if (propertyPage != null) propertyPage.Refresh(this);
            }
        }
        public Style GetDefault(Style.EDefaultFor defaultFor)
        {
            foreach (Style st in styles.Values)
            {
                if (((int)st.DefaultFor & (int)defaultFor) != 0)
                {
                    return st;
                }
            }
            return null;
        }
        public string[] Names
        {
            get
            {
                string[] res = new string[styles.Count];
                styles.Keys.CopyTo(res, 0);
                return res;
            }
        }
        /// <summary>
        /// Implements <see cref="CADability.Attribute.IAttributeList.Clone ()"/>
        /// </summary>
        /// <returns></returns>
        public StyleList Clone()
        {
            StyleList res = new StyleList();
            foreach (DictionaryEntry de in styles)
            {
                Style st = de.Value as Style;
                Style clone = st.Clone() as Style;
                clone.Parent = res;
                res.styles.Add(clone.Name, clone);
                if (st == current)
                {
                    res.current = clone;
                }
            }
            return res;
        }
        #region Properties
        public LayerList LayerList
        {
            get
            {
                if (owner != null)
                    return owner.LayerList;
                return null;
            }
        }
        public ColorList ColorList
        {
            get
            {
                if (owner != null)
                    return owner.ColorList;
                return null;
            }
        }
        public HatchStyleList HatchStyleList
        {
            get
            {
                if (owner != null)
                    return owner.HatchStyleList;
                return null;
            }
        }
        public DimensionStyleList DimensionStyleList
        {
            get
            {
                if (owner != null)
                    return owner.DimensionStyleList;
                return null;
            }
        }

        #endregion
        #region PropertyEntryImpl Overrides
        public override PropertyEntryType Flags => PropertyEntryType.GroupTitle | PropertyEntryType.HasSubEntries | PropertyEntryType.ContextMenu | PropertyEntryType.Selectable;
        /// <summary>
        /// Overrides <see cref="PropertyEntryImpl.SubItems"/>,
        /// returns the subentries in this property view.
        /// </summary>
        public override IPropertyEntry[] SubItems
        {
            get
            {
                if (showProperties == null)
                {
                    showProperties = new IPropertyEntry[styles.Count];
                    for (int i = 0; i < styles.Count; i++)
                    {
                        Style st = styles.GetByIndex(i) as Style;
                        showProperties[i] = st;
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
                return MenuResource.LoadMenuDefinition("MenuId.StyleList", false, this);
            }
        }

        #endregion
        #region ICloneable Members
        object ICloneable.Clone()
        {
            return this.Clone();
        }
        #endregion
        public void GetObjectData(IJsonWriteData data)
        {
            Style[] styleArray = new Style[styles.Count];
            for (int i = 0; i < styles.Count; i++)
            {
                styleArray[i] = (Style)styles.GetByIndex(i);
            }
            data.AddProperty("UnsortedEntries", styleArray);
            data.AddProperty("Current", current);
        }

        public void SetObjectData(IJsonReadData data)
        {
            unsortedEntries = data.GetProperty<Style[]>("UnsortedEntries");
            current = data.GetProperty("Current") as Style;
            data.RegisterForSerializationDoneCallback(this);
        }

        void IJsonSerializeDone.SerializationDone(JsonSerialize jsonSerialize)
        {
            if (unsortedEntries != null)
            {   // entries als SortedList sollte so nicht mehr verwendet werden, besser
                // Set<DimensionStyle,EqualityComparer>
                // The items are enumerated in sorted order. So heißt es im Set
                styles = new SortedList();
                for (int i = 0; i < unsortedEntries.Length; i++)
                {
                    styles.Add(unsortedEntries[i].Name, unsortedEntries[i]);
                }
                unsortedEntries = null;
            }
            foreach (Style st in styles.Values) st.Parent = this;
            if (current == null && styles.Count > 0) current = styles.GetByIndex(0) as Style;
        }

        #region ISerializable Members
        /// <summary>
        /// Constructor required by deserialization
        /// </summary>
        /// <param name="info">SerializationInfo</param>
        /// <param name="context">StreamingContext</param>
        protected StyleList(SerializationInfo info, StreamingContext context)
        {
            try
            {
                unsortedEntries = info.GetValue("UnsortedEntries", typeof(Style[])) as Style[];
            }
            catch (SerializationException)
            {   // alte Dateien beinhalten entries direkt
                styles = (SortedList)info.GetValue("Styles", typeof(SortedList));
            }
            current = InfoReader.Read(info, "Current", typeof(Style)) as Style;
            resourceIdInternal = "StyleList";
        }
        /// <summary>
        /// Implements <see cref="ISerializable.GetObjectData"/>
        /// </summary>
        /// <param name="info">The <see cref="System.Runtime.Serialization.SerializationInfo"/> to populate with data.</param>
        /// <param name="context">The destination (<see cref="System.Runtime.Serialization.StreamingContext"/>) for this serialization.</param>
        public void GetObjectData(SerializationInfo info, StreamingContext context)
        {
            // info.AddValue("Styles",styles);
            // SortedList abspeichern macht u.U. Probleme bei Versionswechsel von .NET
            // deshalb jetzt nur Array abspeichern (23.9.2011)
            Style[] styleArray = new Style[styles.Count];
            for (int i = 0; i < styles.Count; i++)
            {
                styleArray[i] = (Style)styles.GetByIndex(i);
            }
            info.AddValue("UnsortedEntries", styleArray);
            info.AddValue("Current", current);
        }
        #endregion
        #region IDeserializationCallback Members
        void IDeserializationCallback.OnDeserialization(object sender)
        {
            if (unsortedEntries != null)
            {   // entries als SortedList sollte so nicht mehr verwendet werden, besser
                // Set<DimensionStyle,EqualityComparer>
                // The items are enumerated in sorted order. So heißt es im Set
                styles = new SortedList();
                for (int i = 0; i < unsortedEntries.Length; i++)
                {
                    styles.Add(unsortedEntries[i].Name, unsortedEntries[i]);
                }
                unsortedEntries = null;
            }
            foreach (Style st in styles.Values) st.Parent = this;
            if (current == null && styles.Count > 0) current = styles.GetByIndex(0) as Style;
        }
        #endregion
        #region IAttributeList Members

        void IAttributeList.Add(INamedAttribute toAdd)
        {
            if (toAdd is Style && Find(toAdd.Name) == null)
                Add((Style)toAdd);
        }

        int IAttributeList.Count
        {
            get
            {
                return styles.Count;
            }
        }

        INamedAttribute IAttributeList.Item(int Index)
        {
            if (Index > 0 && Index < styles.Count)
                return styles.GetByIndex(Index) as INamedAttribute;
            return null;
        }
        void IAttributeList.AttributeChanged(INamedAttribute attribute, ReversibleChange change)
        {
            if (owner != null) owner.AttributeChanged(this, attribute, change);
        }
        bool IAttributeList.MayChangeName(INamedAttribute attribute, string newName)
        {
            if (attribute.Name == newName) return true;
            return !styles.ContainsKey(newName);
        }

        void IAttributeList.NameChanged(INamedAttribute Attribute, string oldName)
        {
            styles.Remove(oldName);
            styles[Attribute.Name] = Attribute;
        }
        void IAttributeList.Initialize()
        {
            if (styles.Count == 0)
            {
                Add(Style.GetDefault());
            }
            current = styles.GetByIndex(0) as Style;
            current.Initialize();
        }

        IAttributeList IAttributeList.Clone() { return Clone(); }
        void IAttributeList.Update(bool AddMissingToList)
        {
            foreach (Style s in styles.Values)
                s.Update(AddMissingToList);
        }

        void IAttributeList.Update(IGeoObject Object2Update)
        {
            if (Object2Update is IStyle)
            {
                Style oldS = (Object2Update as IStyle).Style;
                if (oldS == null) return;
                Style s = Find(oldS.Name);
                if (s == null)
                    Add(oldS);
                else
                {
                    if ((Object2Update as IStyle).Style.Check(Object2Update))
                    {   // nur wenn der Style auch passt, macht sonst bei DragAndDrop Probleme
                        (Object2Update as IStyle).Style = s;
                    }
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
                return current as INamedAttribute;
            }
        }
        #endregion
        private void OnNewStyle()
        {
            string NewStyleName = StringTable.GetString("StyleList.NewStyleName");
            int MaxNr = 0;
            foreach (DictionaryEntry de in styles)
            {
                string Name = de.Key.ToString();
                if (Name.StartsWith(NewStyleName))
                {
                    try
                    {
                        int nr = int.Parse(Name.Substring(NewStyleName.Length));
                        if (nr > MaxNr) MaxNr = nr;
                    }
                    catch (ArgumentNullException) { } // no number
                    catch (FormatException) { } // something else
                    catch (OverflowException) { } // to many digits
                }
            }
            MaxNr += 1; // next available number
            NewStyleName += MaxNr.ToString();
            Add(new Style(NewStyleName));

            showProperties = null;
            showProperties = SubItems; // to make sure they exist
            propertyPage.OpenSubEntries(this, true);
            propertyPage.Refresh(this);
            propertyPage.StartEditLabel(showProperties[styles.IndexOfKey(NewStyleName)] as IPropertyEntry);
        }
        #region ICommandHandler Members
        private void OnAddFromGlobal()
        {
            foreach (Style l in Settings.GlobalSettings.StyleList.styles.Values)
            {
                if (!styles.ContainsKey(l.Name))
                {
                    this.Add(l.Clone() as Style);
                }
            }
        }
        private void OnMakeGlobal()
        {
            Settings.GlobalSettings.StyleList.RemoveAll();
            foreach (Style l in styles.Values)
            {
                Settings.GlobalSettings.StyleList.Add(l.Clone() as Style);
            }
        }
        private void OnUpdateFromProject()
        {
            Hashtable used = GetAllStyle();
            foreach (Style ds in used.Keys)
            {
                if (!styles.ContainsKey(ds.Name))
                {
                    this.Add(ds);
                }
            }
        }
        private void OnRemoveUnused()
        {
            Hashtable used = GetAllStyle();
            bool found = false;
            do
            {
                found = false;
                if (styles.Count > 1)
                {
                    foreach (Style ds in styles.Values)
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
        private void GetAllStyle(Hashtable collect, IGeoObject go)
        {
            if (go.Style != null)
            {
                collect[go.Style] = null; // Hastable wird als set verwendet
            }
            else if (go is Block)
            {
                foreach (IGeoObject child in (go as Block).Children)
                    GetAllStyle(collect, child);
            }
        }
        private Hashtable GetAllStyle()
        {
            Hashtable res = new Hashtable();
            foreach (Model m in Frame.Project)
            {
                foreach (IGeoObject go in m) GetAllStyle(res, go);
            }
            return res;
        }
        private void UpdateObjects(IGeoObject go)
        {
            if (go is Block)
            {
                foreach (IGeoObject child in (go as Block).Children)
                    UpdateObjects(child);
            }
            Style st = go.Style;
            if (st != null && styles.Contains(st.Name))
            {
                st.Apply(go);
            }
        }
        private void OnUpdateAllObjects()
        {
            foreach (Model m in Frame.Project)
            {
                foreach (IGeoObject go in m) UpdateObjects(go);
            }
        }
        bool ICommandHandler.OnCommand(string MenuId)
        {
            switch (MenuId)
            {
                case "MenuId.StyleList.New":
                    OnNewStyle();
                    return true;
                case "MenuId.StyleList.RemoveUnused":
                    OnRemoveUnused();
                    return true;
                case "MenuId.StyleList.UpdateFromProject":
                    OnUpdateFromProject();
                    return true;
                case "MenuId.StyleList.AddFromGlobal":
                    OnAddFromGlobal();
                    return true;
                case "MenuId.StyleList.MakeGlobal":
                    OnMakeGlobal();
                    return true;
                case "MenuId.StyleList.UpdateAllObjects":
                    OnUpdateAllObjects();
                    return true;
            }
            return false;
        }
        bool ICommandHandler.OnUpdateCommand(string MenuId, CommandState CommandState)
        {
            // TODO:  Add StyleList.OnUpdateCommand implementation
            return false;
        }
        void ICommandHandler.OnSelected(MenuWithHandler selectedMenuItem, bool selected) { }
        #endregion
        internal void CheckDefault(Style style)
        {
            foreach (DictionaryEntry de in styles)
            {
                Style st = de.Value as Style;
                if (st != style)
                {
                    if (style.IsDefaultFor(Style.EDefaultFor.Curves)) st.RemoveDefaultFor(Style.EDefaultFor.Curves);
                    if (style.IsDefaultFor(Style.EDefaultFor.Dimension)) st.RemoveDefaultFor(Style.EDefaultFor.Dimension);
                    if (style.IsDefaultFor(Style.EDefaultFor.Solids)) st.RemoveDefaultFor(Style.EDefaultFor.Solids);
                    if (style.IsDefaultFor(Style.EDefaultFor.Text)) st.RemoveDefaultFor(Style.EDefaultFor.Text);
                    if (style.IsDefaultFor(Style.EDefaultFor.HiddenSolids)) st.RemoveDefaultFor(Style.EDefaultFor.HiddenSolids);
                    if (style.IsDefaultFor(Style.EDefaultFor.Axis)) st.RemoveDefaultFor(Style.EDefaultFor.Axis);
                }
            }
        }
        public int FindIndex(Style style)
        {
            return styles.IndexOfKey(style.Name);
        }
        internal bool Contains(Style style)
        {
            if (style == null || style.Name == null || style.Name.Length == 0) return false;
            return styles[style.Name] == style;
        }

    }
}
