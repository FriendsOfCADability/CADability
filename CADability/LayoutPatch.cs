#if !WEBASSEMBLY
using CADability.Actions;
using CADability.Attribute;
using CADability.GeoObject;
using CADability.Shapes;
using CADability.UserInterface;
using System;
using System.Collections;
using System.Runtime.Serialization;

namespace CADability
{
#region Konzept
    /*
	 * Wir betrachten für einen Patch hier nur rechteckige Ausschnitte auf dem Papier
	 * obwohl das Layout Objekt beliebige Borders zulässt
	 */
#endregion
    /// <summary>
    /// Darstellung eines Patches als ShowProperty
    /// </summary>
    [Serializable]
    internal class LayoutPatch : IShowPropertyImpl, ICommandHandler, ISerializable
    {
        // die zu speichernden Daten
        private Model model;
        public Projection Projection; // hier nur unscaledProjection von Bedeutung
        // enthält auch die Sichtbarkeitsanforderungen (Drahtmodell, verdeckte Kanten)
        public Border Area; // wenn null, dann die ganze Seite
        public Hashtable visibleLayers;
        private string name;

        // Daten, die währen der Manipulation benötigt werden:
        private Layout layout; // das Layout, auf welches wir uns hier beziehen
        private Project project; // das Projekt, wir müssen wissen, welche modelle es gibt
        private LayoutView layoutView; // der LayoutView wegen refresh

        private VisibleLayers visibleLayersSp; // die sichtbaren Ebenen
        private StringProperty scalingProperty; // Maßstab
        private DoubleProperty horPos;	// Position des Modell-Ursprungs auf dem Papier
        private DoubleProperty verPos;
        private DoubleProperty left;	// linker Rand auf dem Papier
        private DoubleProperty bottom; // unterer Rand auf dem Papier
        private DoubleProperty width;	// linker Rand auf dem Papier
        private DoubleProperty height; // unterer Rand auf dem Papier
        private GeoVectorProperty projectionDirection;
        private BooleanProperty showHiddenLines;
        private ModifyLayoutPatch action;

        private BoundingRect section; // Ausschnitt auf dem Papier als Rechteck

        public LayoutPatch(Model model, Projection projection, Border area)
        {
            Model = model;
            Projection = projection;
            Projection.Precision = model.Extent.Size / 1000;
            Area = area;
            visibleLayers = new Hashtable();
        }
        public Model Model
        {
            get
            {
                return model;
            }
            set
            {
                if (model != null)
                {
                    model.GeoObjectAddedEvent -= new Model.GeoObjectAdded(OnModelChanged);
                    model.GeoObjectRemovedEvent -= new Model.GeoObjectRemoved(OnModelChanged);
                    model.GeoObjectDidChangeEvent -= new CADability.GeoObject.ChangeDelegate(OnModelChanged);
                }
                model = value;
                if (model != null && propertyTreeView != null) // nämlich nur, wenn es angezeigt wird
                {
                    model.GeoObjectAddedEvent += new Model.GeoObjectAdded(OnModelChanged);
                    model.GeoObjectRemovedEvent += new Model.GeoObjectRemoved(OnModelChanged);
                    model.GeoObjectDidChangeEvent += new CADability.GeoObject.ChangeDelegate(OnModelChanged);
                }
            }
        }
        void OnModelChanged(IGeoObject go)
        {
            InvalidateRepresentation();
        }
        void OnModelChanged(IGeoObject Sender, GeoObjectChange Change)
        {
            InvalidateRepresentation();
        }
        void OnProjectionChanged(Projection sender, EventArgs args)
        {
            InvalidateRepresentation();
        }
        private void InvalidateRepresentation()
        {
        }
        public void Connect(Layout layout, Project project, LayoutView layoutView)
        {   // das kommt dran, sobald der layoutview in der Liste im Treeview steht
            this.layout = layout;
            this.project = project;
            this.layoutView = layoutView;
            if (name == null)
            {
                int j = 1;
                bool invalidName = true;
                while (invalidName)
                {
                    invalidName = false;
                    string newname = StringTable.GetFormattedString("Layout.Patch.Label", j);
                    for (int i = 0; i < layout.Patches.Length; ++i)
                    {
                        if (layout.Patches[i].name == newname)
                        {
                            invalidName = true;
                            ++j;
                        }
                    }
                    name = newname;
                }
            }
            base.LabelText = name;
            if (Area == null)
            {
                section = new BoundingRect(0.0, 0.0, layout.PaperWidth, layout.PaperHeight);
                Area = Border.MakeRectangle(section);
            }
            else
            {
                section = Area.Extent;
            }
            base.resourceIdInternal = "Layout.Patch";
            project.LayerList.DidModifyEvent += new DidModifyDelegate(OnLayerListModifed);

            model.GeoObjectAddedEvent += new Model.GeoObjectAdded(OnModelChanged);
            model.GeoObjectRemovedEvent += new Model.GeoObjectRemoved(OnModelChanged);
            model.GeoObjectDidChangeEvent += new CADability.GeoObject.ChangeDelegate(OnModelChanged);
            Projection.ProjectionChangedEvent += new Projection.ProjectionChangedDelegate(OnProjectionChanged);
        }
        public void Disconnect(Project project)
        {
            project.LayerList.DidModifyEvent -= new DidModifyDelegate(OnLayerListModifed);
            model.GeoObjectAddedEvent -= new Model.GeoObjectAdded(OnModelChanged);
            model.GeoObjectRemovedEvent -= new Model.GeoObjectRemoved(OnModelChanged);
            model.GeoObjectDidChangeEvent -= new CADability.GeoObject.ChangeDelegate(OnModelChanged);
            Projection.ProjectionChangedEvent -= new Projection.ProjectionChangedDelegate(OnProjectionChanged);
            subEntries = null;
        }
        void OnLayerListModifed(object sender, EventArgs args)
        {
            if (visibleLayersSp != null) visibleLayersSp.Refresh();
        }
#region IShowPropertyImpl
        /// <summary>
        /// Overrides <see cref="IShowPropertyImpl.LabelType"/>
        /// </summary>
        public override ShowPropertyLabelFlags LabelType
        {
            get
            {
                return ShowPropertyLabelFlags.Selectable | ShowPropertyLabelFlags.Editable | ShowPropertyLabelFlags.ContextMenu;
            }
        }
        public override void LabelChanged(string NewText)
        {
            bool ok = true;
            for (int i = 0; i < layout.Patches.Length; ++i)
            {
                if (layout.Patches[i] != this && layout.Patches[i].name == NewText)
                {
                    ok = false;
                }
            }
            if (ok)
            {
                name = NewText;
                LabelText = name;
            }
            else
            {   // nicht umbenennen, alten Wert anzeigen
                propertyTreeView.Refresh(this);
            }
        }
        IShowProperty[] subEntries;
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
        public override IShowProperty[] SubEntries
        {
            get
            {
                if (subEntries == null)
                {
                    visibleLayersSp = new VisibleLayers("ModelView.VisibleLayers", Frame.Project.LayerList);
                    visibleLayersSp.IsLayerVisibleEvent += new CADability.VisibleLayers.IsLayerVisibleDelegate(IsLayerVisible);
                    visibleLayersSp.LayerVisibilityChangedEvent += new CADability.VisibleLayers.LayerVisibilityChangedDelegate(OnLayerVisibilityChanged);
                    scalingProperty = new StringProperty("", "Layout.Scale");
                    scalingProperty.SetContextMenu("MenuId.Layout.Scale", this);
                    scalingProperty.GetStringEvent += new CADability.UserInterface.StringProperty.GetStringDelegate(OnGetScalingString);
                    scalingProperty.SetStringEvent += new CADability.UserInterface.StringProperty.SetStringDelegate(OnSetScalingString);
                    scalingProperty.Refresh();
                    horPos = new DoubleProperty("Layout.HorizontalPosition", Frame);
                    horPos.GetDoubleEvent += new CADability.UserInterface.DoubleProperty.GetDoubleDelegate(OnGetHorPos);
                    horPos.SetDoubleEvent += new CADability.UserInterface.DoubleProperty.SetDoubleDelegate(OnSetHorPos);
                    horPos.Refresh();
                    horPos.SetContextMenu("MenuId.Layout.Horizontal", this);
                    verPos = new DoubleProperty("Layout.VerticalPosition", Frame);
                    verPos.GetDoubleEvent += new CADability.UserInterface.DoubleProperty.GetDoubleDelegate(OnGetVerPos);
                    verPos.SetDoubleEvent += new CADability.UserInterface.DoubleProperty.SetDoubleDelegate(OnSetVerPos);
                    verPos.Refresh();
                    verPos.SetContextMenu("MenuId.Layout.Vertical", this);
                    left = new DoubleProperty("Layout.Left", Frame);
                    left.GetDoubleEvent += new CADability.UserInterface.DoubleProperty.GetDoubleDelegate(OnGetLeftPos);
                    left.SetDoubleEvent += new CADability.UserInterface.DoubleProperty.SetDoubleDelegate(OnSetLeftPos);
                    left.Refresh();
                    bottom = new DoubleProperty("Layout.Bottom", Frame);
                    bottom.GetDoubleEvent += new CADability.UserInterface.DoubleProperty.GetDoubleDelegate(OnGetBottomPos);
                    bottom.SetDoubleEvent += new CADability.UserInterface.DoubleProperty.SetDoubleDelegate(OnSetBottomPos);
                    bottom.Refresh();
                    width = new DoubleProperty("Layout.Width", Frame);
                    width.GetDoubleEvent += new CADability.UserInterface.DoubleProperty.GetDoubleDelegate(OnGetWidth);
                    width.SetDoubleEvent += new CADability.UserInterface.DoubleProperty.SetDoubleDelegate(OnSetWidth);
                    width.Refresh();
                    height = new DoubleProperty("Layout.Height", Frame);
                    height.GetDoubleEvent += new CADability.UserInterface.DoubleProperty.GetDoubleDelegate(OnGetHeight);
                    height.SetDoubleEvent += new CADability.UserInterface.DoubleProperty.SetDoubleDelegate(OnSetHeight);
                    height.Refresh();
                    string[] modelNames = new string[project.GetModelCount()];
                    for (int i = 0; i < project.GetModelCount(); ++i)
                    {
                        modelNames[i] = project.GetModel(i).Name;
                    }
                    string activeModel = project.GetActiveModel().Name;
                    MultipleChoiceProperty modelSelection = new MultipleChoiceProperty("Layout.Model", modelNames, activeModel);
                    modelSelection.ValueChangedEvent += new ValueChangedDelegate(ModelSelectionChanged);

                    projectionDirection = new GeoVectorProperty("Layout.Projection", Frame, false);
                    projectionDirection.GetGeoVectorEvent += new CADability.UserInterface.GeoVectorProperty.GetGeoVectorDelegate(OnGetProjectionDirection);
                    projectionDirection.SetGeoVectorEvent += new CADability.UserInterface.GeoVectorProperty.SetGeoVectorDelegate(OnSetProjectionDirection);
                    projectionDirection.IsAngle = false;
                    projectionDirection.IsNormedVector = false;
                    projectionDirection.FilterCommandEvent += new GeoVectorProperty.FilterCommandDelegate(FilterCommandProjectionDirection);
                    projectionDirection.ContextMenuId = "MenuId.Projection.Direction";
                    //MenuItemWithID[] mid = new MenuItemWithID[project.ModelViewCount];
                    //for (int i = 0; i < project.ModelViewCount; i++)
                    //{
                    //    string name = project.GetModelViewName(i);
                    //    mid[i] = new MenuItemWithID();
                    //    mid[i].ID = "FromModelView_" + name;
                    //    mid[i].Text = StringTable.GetFormattedString("ModelView", name);
                    //    mid[i].Target = this;
                    //}
                    // to implement

                    showHiddenLines = new BooleanProperty("ModelView.ShowHiddenLines", "ModelView.ShowHiddenLines.Values");
                    showHiddenLines.BooleanValue = Projection.ShowFaces;
                    showHiddenLines.BooleanChangedEvent += new BooleanChangedDelegate(OnShowHiddenLinesChanged);

                    subEntries = new IShowProperty[] {visibleLayersSp,scalingProperty,modelSelection,projectionDirection,
                                                        showHiddenLines,horPos,verPos,left,bottom,width,height};
                }
                return subEntries;
            }
        }

        void OnShowHiddenLinesChanged(object sender, bool NewValue)
        {
            Projection.ShowFaces = NewValue;
            layoutView.Repaint();
        }

        public override MenuWithHandler[] ContextMenu
        {
            get
            {
                return MenuResource.LoadMenuDefinition("MenuId.LayoutPatch", false, this);
            }
        }
        void FilterCommandProjectionDirection(GeoVectorProperty sender, string menuId, CommandState commandState, ref bool handled)
        {
            GeoVector viewDirection = sender.GetGeoVector();
            bool viewDirectionModified = false;
            if (commandState != null)
            {
                switch (menuId)
                {
                    case "MenuId.Projection.Direction.FromTop":
                        commandState.Checked = Precision.SameNotOppositeDirection(Projection.Direction, -GeoVector.ZAxis);
                        break;
                    case "MenuId.Projection.Direction.FromFront":
                        commandState.Checked = Precision.SameNotOppositeDirection(Projection.Direction, GeoVector.YAxis);
                        break;
                    case "MenuId.Projection.Direction.FromBack":
                        commandState.Checked = Precision.SameNotOppositeDirection(Projection.Direction, -GeoVector.YAxis);
                        break;
                    case "MenuId.Projection.Direction.FromLeft":
                        commandState.Checked = Precision.SameNotOppositeDirection(Projection.Direction, GeoVector.XAxis);
                        break;
                    case "MenuId.Projection.Direction.FromRight":
                        commandState.Checked = Precision.SameNotOppositeDirection(Projection.Direction, -GeoVector.XAxis);
                        break;
                    case "MenuId.Projection.Direction.FromBottom":
                        commandState.Checked = Precision.SameNotOppositeDirection(Projection.Direction, GeoVector.ZAxis);
                        break;
                    case "MenuId.Projection.Direction.Isometric":
                        GeoVector vd = new GeoVector(-1, -1, -1);
                        vd.Norm();
                        commandState.Checked = Precision.SameNotOppositeDirection(Projection.Direction, vd);
                        break;
                    case "MenuId.Projection.Direction.Perspective":
                        commandState.Enabled = false;
                        break;
                }
            }
            else
            {
                switch (menuId)
                {
                    case "MenuId.Projection.Direction.FromTop":
                        Projection.SetDirection(-GeoVector.ZAxis, GeoVector.YAxis, model.Extent);
                        viewDirectionModified = true;
                        handled = true;
                        break;
                    case "MenuId.Projection.Direction.FromFront":
                        Projection.SetDirection(GeoVector.YAxis, GeoVector.ZAxis, model.Extent);
                        viewDirectionModified = true;
                        handled = true;
                        break;
                    case "MenuId.Projection.Direction.FromBack":
                        Projection.SetDirection(-GeoVector.YAxis, GeoVector.ZAxis, model.Extent);
                        viewDirectionModified = true;
                        handled = true;
                        break;
                    case "MenuId.Projection.Direction.FromLeft":
                        Projection.SetDirection(GeoVector.XAxis, GeoVector.ZAxis, model.Extent);
                        viewDirectionModified = true;
                        handled = true;
                        break;
                    case "MenuId.Projection.Direction.FromRight":
                        Projection.SetDirection(-GeoVector.XAxis, GeoVector.ZAxis, model.Extent);
                        viewDirectionModified = true;
                        handled = true;
                        break;
                    case "MenuId.Projection.Direction.FromBottom":
                        Projection.SetDirection(GeoVector.ZAxis, GeoVector.YAxis, model.Extent);
                        viewDirectionModified = true;
                        handled = true;
                        break;
                    case "MenuId.Projection.Direction.Isometric":
                        GeoVector vd = new GeoVector(-1, -1, -1);
                        vd.Norm();
                        Projection.SetDirection(vd, GeoVector.YAxis, model.Extent);
                        viewDirectionModified = true;
                        handled = true;
                        break;
                }
                if (viewDirectionModified)
                {
                    OnSetProjectionDirection(sender, Projection.Direction);
                    propertyTreeView.SelectEntry(projectionDirection);
                    projectionDirection.Refresh();
                }
            }
        }
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
        internal void RefreshShowProperties()
        {
            if (Area == null)
            {
                section = new BoundingRect(0.0, 0.0, layout.PaperWidth, layout.PaperHeight);
            }
            else
            {
                section = Area.Extent;
            }
            scalingProperty.Refresh();
            horPos.Refresh();
            verPos.Refresh();
            left.Refresh();
            bottom.Refresh();
            width.Refresh();
            height.Refresh();
        }
#endregion
        private string OnGetScalingString(StringProperty sender)
        {
            double factor, dx, dy;
            Projection.GetPlacement(out factor, out dx, out dy);
            if (factor == 1.0) return "1:1";
            if (factor < 1.0)
            {
                double sci = (1.0 / factor);
                return "1:" + sci.ToString();
            }
            else
            {
                return factor.ToString() + ":1";
            }
        }
        private void OnSetScalingString(StringProperty sender, string newValue)
        {
            string[] parts = newValue.Split(':');
            if (parts.Length == 2)
            {	// n:m Eingabe
                try
                {
                    double n = double.Parse(parts[0]);
                    double m = double.Parse(parts[1]);
                    layout.CenterPatch(this, n / m, Layout.HorizontalCenter.unchanged, Layout.VerticalCenter.unchanged);
                }
                catch (FormatException) { }
                catch (OverflowException) { }
                layoutView.Repaint();
            }
            else
            {
                try
                {
                    double scale = double.Parse(newValue);
                    layout.CenterPatch(this, scale, Layout.HorizontalCenter.unchanged, Layout.VerticalCenter.unchanged);
                }
                catch (FormatException) { }
                catch (OverflowException) { }
                layoutView.Repaint();
            }
            horPos.Refresh();
            verPos.Refresh();
        }
        private double OnGetHorPos(DoubleProperty sender)
        {
            BoundingRect ext = Model.GetExtent(Projection);
            if (ext.IsEmpty()) ext.MinMax(new GeoPoint2D(0.0, 0.0));
            double factor, dx, dy;
            Projection.GetPlacement(out factor, out dx, out dy);
            return ext.Left * factor + dx;
        }
        private void OnSetHorPos(DoubleProperty sender, double l)
        {
            BoundingRect ext = Model.GetExtent(Projection);
            if (ext.IsEmpty()) ext.MinMax(new GeoPoint2D(0.0, 0.0));
            double factor, dx, dy;
            Projection.GetPlacement(out factor, out dx, out dy);
            Projection.SetPlacement(factor, l - ext.Left * factor, dy);
            layoutView.Repaint();
        }
        private double OnGetVerPos(DoubleProperty sender)
        {
            BoundingRect ext = Model.GetExtent(Projection);
            if (ext.IsEmpty()) ext.MinMax(new GeoPoint2D(0.0, 0.0));
            double factor, dx, dy;
            Projection.GetPlacement(out factor, out dx, out dy);
            return ext.Bottom * factor + dy;
        }
        private void OnSetVerPos(DoubleProperty sender, double l)
        {
            BoundingRect ext = Model.GetExtent(Projection);
            if (ext.IsEmpty()) ext.MinMax(new GeoPoint2D(0.0, 0.0));
            double factor, dx, dy;
            Projection.GetPlacement(out factor, out dx, out dy);
            Projection.SetPlacement(factor, dx, l - ext.Bottom * factor);
            layoutView.Repaint();
        }
        private void ModelSelectionChanged(object sender, object NewValue)
        {
            Model m = project.FindModel(NewValue as string);
            if (m != null)
            {
                Model = m;
                layoutView.Repaint();
            }
        }
        private GeoVector OnGetProjectionDirection(GeoVectorProperty sender)
        {
            return Projection.Direction;
        }
        private void OnSetProjectionDirection(GeoVectorProperty sender, GeoVector v)
        {
            if (Precision.SameDirection(v, GeoVector.ZAxis, false))
            {
                Projection.SetDirection(v, GeoVector.YAxis, model.Extent);
            }
            else
            {   // Richtung setzen unter Beibehaltung der Rochtung der Z-Achse (vorausgesetzt sie ist senkrecht, sonst macht es keinen Sinn)
                GeoVector2D updown = Projection.Project2D(new GeoPoint(0, 0, 1)) - Projection.Project2D(GeoPoint.Origin);
                if (updown.y <= 0) Projection.SetDirection(v, new GeoVector(0.0, 0.0, 1.0), model.Extent);
                else Projection.SetDirection(v, new GeoVector(0.0, 0.0, -1.0), model.Extent);
                // Projection.SetDirection(v, GeoVector.ZAxis, model.Extent);
            }
            layoutView.Repaint();
        }
#region ICommandHandler Members
        private void Scale(double f)
        {
            layout.CenterPatch(this, f, Layout.HorizontalCenter.center, Layout.VerticalCenter.center);
            scalingProperty.Refresh();
            layoutView.Repaint();
            horPos.Refresh();
            verPos.Refresh();
        }
        private void Center(Layout.HorizontalCenter hor, Layout.VerticalCenter ver)
        {
            layout.CenterPatch(this, 0.0, hor, ver);
            scalingProperty.Refresh();
            layoutView.Repaint();
            horPos.Refresh();
            verPos.Refresh();
        }
        virtual public bool OnCommand(string MenuId)
        {
            if (MenuId.StartsWith("FromModelView_"))
            {
                string name = MenuId.Substring("FromModelView_".Length);
                for (int i = 0; i < project.ModelViewCount; i++)
                {
                    if (project.GetProjectedModel(i).Name == name)
                    {
                        Projection = project.GetProjectedModel(i).Projection.Clone();
                        projectionDirection.SetGeoVector(Projection.Direction);
                        propertyTreeView.SelectEntry(projectionDirection);
                        projectionDirection.Refresh();
                        Projection = project.GetProjectedModel(i).Projection.Clone();
                        // und noch zentrieren (von unten kopiert)
                        double f = 1.0;
                        BoundingRect br = model.GetExtent(Projection);
                        f = Math.Min(section.Width / br.Width, section.Height / br.Height) * 0.9;
                        layout.CenterPatch(this, f, Layout.HorizontalCenter.center, Layout.VerticalCenter.center);
                        layoutView.Repaint();
                        scalingProperty.Refresh();

                        return true;
                    }
                }
            }
            switch (MenuId)
            {
                case "MenuId.Layout.Scale.1To1":
                    Scale(1.0);
                    return true;
                case "MenuId.Layout.Scale.1To5":
                    Scale(1.0 / 5.0);
                    return true;
                case "MenuId.Layout.Scale.1To10":
                    Scale(1.0 / 10.0);
                    return true;
                case "MenuId.Layout.Scale.1To50":
                    Scale(1.0 / 50.0);
                    return true;
                case "MenuId.Layout.Scale.1To100":
                    Scale(1.0 / 100.0);
                    return true;
                case "MenuId.Layout.Scale.1To1000":
                    Scale(1.0 / 1000.0);
                    return true;
                case "MenuId.Layout.Scale.10To1":
                    Scale(10.0);
                    return true;
                case "MenuId.Layout.Scale.100To1":
                    Scale(100.0);
                    return true;
                case "MenuId.Layout.Scale.Fit":
                    {
                        double f = 1.0;
                        BoundingRect br = model.GetExtent(Projection);
                        f = Math.Min(section.Width / br.Width, section.Height / br.Height);
                        layout.CenterPatch(this, f, Layout.HorizontalCenter.center, Layout.VerticalCenter.center);
                        layoutView.Repaint();
                        scalingProperty.Refresh();
                    }
                    return true;
                case "MenuId.Layout.Horizontal.Left":
                    Center(Layout.HorizontalCenter.left, Layout.VerticalCenter.unchanged);
                    return true;
                case "MenuId.Layout.Horizontal.Center":
                    Center(Layout.HorizontalCenter.center, Layout.VerticalCenter.unchanged);
                    return true;
                case "MenuId.Layout.Horizontal.Right":
                    Center(Layout.HorizontalCenter.right, Layout.VerticalCenter.unchanged);
                    return true;
                case "MenuId.Layout.Vertical.Bottom":
                    Center(Layout.HorizontalCenter.unchanged, Layout.VerticalCenter.bottom);
                    return true;
                case "MenuId.Layout.Vertical.Center":
                    Center(Layout.HorizontalCenter.unchanged, Layout.VerticalCenter.center);
                    return true;
                case "MenuId.Layout.Vertical.Top":
                    Center(Layout.HorizontalCenter.unchanged, Layout.VerticalCenter.top);
                    return true;
                case "MenuId.LayoutPatch.Rename":
                    propertyTreeView.StartEditLabel(this);
                    return true;
                case "MenuId.LayoutPatch.Remove":
                    layout.RemovePatch(this);
                    layoutView.Refresh();
                    return true;
                case "MenuId.LayoutPatch.FillLayout":
                    section.Left = 0;
                    section.Right = layout.PaperWidth;
                    section.Bottom = 0;
                    section.Top = layout.PaperHeight;
                    SetSection();
                    return true;
                case "MenuId.LayoutPatch.Center":
                    Center(Layout.HorizontalCenter.center, Layout.VerticalCenter.center);
                    return true;
            }
            return false;
        }
        virtual public bool OnUpdateCommand(string MenuId, CommandState CommandState)
        {
            switch (MenuId)
            {
                case "MenuId.LayoutView.Show":
                    CommandState.Checked = (base.Frame.ActiveView == this);
                    return true;
                case "MenuId.LayoutPatch.Remove":
                    CommandState.Enabled = layout.Patches.Length > 1;
                    return true;
            }
            return false;
        }
        void ICommandHandler.OnSelected(MenuWithHandler selectedMenuItem, bool selected) { }
#endregion
        private void OnFocusChanged(IPropertyTreeView sender, IShowProperty NewFocus, IShowProperty OldFocus)
        {
            if (sender.FocusEntered(this, OldFocus, NewFocus))
            {
                if (action != null) action.RemoveThisAction();
                action = null;
                if (Frame.ActiveView == layoutView)
                {
                    action = new ModifyLayoutPatch(this, layoutView);
                    Frame.SetAction(action);
                }
            }
            else if (sender.FocusLeft(this, OldFocus, NewFocus))
            {
                if (action != null) action.RemoveThisAction();
                action = null;
            }
            if (sender.FocusLeft(visibleLayersSp, OldFocus, NewFocus))
            {
                layoutView.Repaint();
            }
        }
        private void SetSection()
        {
            this.Area = section.ToBorder();
            layoutView.Repaint();
        }
        private double OnGetLeftPos(DoubleProperty sender)
        {
            return section.Left;
        }
        private void OnSetLeftPos(DoubleProperty sender, double l)
        {
            double w = section.Width;
            section.Left = l;
            section.Right = l + w;
            SetSection();
        }
        private double OnGetBottomPos(DoubleProperty sender)
        {
            return section.Bottom;
        }
        private void OnSetBottomPos(DoubleProperty sender, double l)
        {
            double h = section.Height;
            section.Bottom = l;
            section.Top = l + h;
            SetSection();
        }
        private double OnGetWidth(DoubleProperty sender)
        {
            return section.Width;
        }
        private void OnSetWidth(DoubleProperty sender, double l)
        {
            section.Right = section.Left + l;
            SetSection();
        }
        private double OnGetHeight(DoubleProperty sender)
        {
            return section.Height;
        }
        private void OnSetHeight(DoubleProperty sender, double l)
        {
            section.Top = section.Bottom + l;
            SetSection();
        }
        public bool IsLayerVisible(Layer l)
        {
            if (l == null) return true;
            return visibleLayers.ContainsKey(l);
        }
        private void OnLayerVisibilityChanged(Layer l, bool isVisible)
        {
            if (isVisible) visibleLayers[l] = null;
            else visibleLayers.Remove(l);
            InvalidateRepresentation();
            layoutView.Repaint();
        }
#region ISerializable Members
        protected LayoutPatch(SerializationInfo info, StreamingContext context)
        {
            Model = (Model)info.GetValue("Model", typeof(Model));
            Projection = (Projection)info.GetValue("Projection", typeof(Projection));
            Area = (Border)info.GetValue("Area", typeof(Border));
            try
            {
                visibleLayers = info.GetValue("VisibleLayers", typeof(Hashtable)) as Hashtable;
            }
            catch (SerializationException)
            {
                visibleLayers = new Hashtable();
            }
            try
            {
                name = info.GetValue("Name", typeof(string)) as string;
            }
            catch (SerializationException)
            {
                name = null;
            }
        }
        /// <summary>
        /// Implements <see cref="ISerializable.GetObjectData"/>
        /// </summary>
        /// <param name="info">The <see cref="System.Runtime.Serialization.SerializationInfo"/> to populate with data.</param>
        /// <param name="context">The destination (<see cref="System.Runtime.Serialization.StreamingContext"/>) for this serialization.</param>
        public void GetObjectData(SerializationInfo info, StreamingContext context)
        {
            info.AddValue("Model", Model, typeof(Model));
            info.AddValue("Projection", Projection, typeof(Projection));
            info.AddValue("Area", Area, typeof(Border));
            info.AddValue("VisibleLayers", visibleLayers, typeof(Hashtable));
            info.AddValue("Name", name, typeof(string));
        }
#endregion
    }
}
#endif