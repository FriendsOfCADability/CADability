using CADability.Actions;
using CADability.Substitutes;

namespace CADability.UserInterface
{
    /// <summary>
    ///
    /// </summary>

    public class DrawingPlaneProperty : IShowPropertyImpl, ICommandHandler
    {
        private Projection projection;
        private IFrame frame;
        private IShowProperty[] subEntries;
        GeoPointProperty planeOrigin;
        GeoVectorProperty planeDirectionX;
        GeoVectorProperty planeDirectionY;
        GeoVector dirX, dirY;
        public DrawingPlaneProperty(Projection projection, IFrame frame)
        {
            this.projection = projection;
            this.frame = frame;
            subEntries = new IShowProperty[3];
            planeOrigin = new GeoPointProperty(this, "PlanePoint", "DrawingPlane.Location", frame, true);
            planeOrigin.ForceAbsolute = true; // Immer absolut darstellen, egal was der Anwender in den Formatierungen gewählt hat
            planeOrigin.ModifiedByActionEvent += new CADability.UserInterface.GeoPointProperty.ModifiedByActionDelegate(OnOriginModifiedByAction);
            planeDirectionX = new GeoVectorProperty(this, "PlaneDirectionX", "DrawingPlane.DirectionX", frame, false);
            planeDirectionY = new GeoVectorProperty(this, "PlaneDirectionY", "DrawingPlane.DirectionY", frame, false);
            planeDirectionX.IsAngle = false;
            planeDirectionY.IsAngle = false;
            dirX = projection.DrawingPlane.DirectionX; // lokale Kopien, erst setzen beim Verlassen
            dirY = projection.DrawingPlane.DirectionY;
            planeDirectionX.ModifiedByActionEvent += new CADability.UserInterface.GeoVectorProperty.ModifiedByActionDelegate(OnDirectionModifiedByAction);
            subEntries[0] = planeOrigin;
            subEntries[1] = planeDirectionX;
            subEntries[2] = planeDirectionY;
            resourceIdInternal = "DrawingPlane";
        }
        public GeoPoint PlanePoint
        {
            get
            {
                return projection.DrawingPlane.Location;
            }
            set
            {   // Achtung: Plane ist ein value-type!
                Plane pln = projection.DrawingPlane;
                pln.Location = value;
                projection.DrawingPlane = pln;
            }
        }
        public GeoVector PlaneDirectionX
        {
            get
            {
                return dirX;
            }
            set
            {
                dirX = value;
            }
        }
        public GeoVector PlaneDirectionY
        {
            get
            {
                return dirY;
            }
            set
            {
                dirY = value;
            }
        }
        #region IShowProperty Members
        public override ShowPropertyLabelFlags LabelType
        {
            get
            {
                return ShowPropertyLabelFlags.ContextMenu | ShowPropertyLabelFlags.Selectable | ShowPropertyLabelFlags.ContextMenu;
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
        /// <summary>
        /// Overrides <see cref="IShowPropertyImpl.SubEntriesCount"/>,
        /// returns the number of subentries in this property view.
        /// </summary>
        public override int SubEntriesCount
        {
            get
            {
                return subEntries.Length;
            }
        }
        /// <summary>
        /// Overrides <see cref="IShowPropertyImpl.SubEntries"/>,
        /// returns the subentries in this property view.
        /// </summary>
        public override IShowProperty[] SubEntries
        {
            get
            {
                return subEntries;
            }
        }
        public override MenuWithHandler[] ContextMenu
        {
            get
            {
                return MenuResource.LoadMenuDefinition("MenuId.DrawingPlane", false, this);
            }
        }
        #endregion

        #region ICommandHandler Members
        bool ICommandHandler.OnCommand(string MenuId)
        {
            switch (MenuId)
            {
                case "MenuId.DrawingPlane.StandardXY":
                    if (!projection.Direction.IsPerpendicular(new GeoVector(0, 0, 1)))
                        projection.DrawingPlane = new Plane(Plane.StandardPlane.XYPlane, 0.0);
                    else
                        Frame.UIService.ShowMessageBox(StringTable.GetString("Error.DrawingPlane.Impossible"), StringTable.GetString("Errormessage.Title.InvalidInput"), MessageBoxButtons.OK);
                    return true;
                case "MenuId.DrawingPlane.StandardXY.Offset":
                    if (!projection.Direction.IsPerpendicular(new GeoVector(0, 0, 1)))
                    {
                        ConstrDrawingPlaneOffset cdpo = new Actions.ConstrDrawingPlaneOffset(new Plane(Plane.StandardPlane.XYPlane, 0.0), projection);
                        cdpo.ActionDoneEvent += new CADability.Actions.ConstructAction.ActionDoneDelegate(OnDrawingPlaneDone);
                        frame.SetAction(cdpo);
                    }
                    else
                        Frame.UIService.ShowMessageBox(StringTable.GetString("Error.DrawingPlane.Impossible"), StringTable.GetString("Errormessage.Title.InvalidInput"), MessageBoxButtons.OK);
                    return true;
                case "MenuId.DrawingPlane.StandardXZ":
                    if (!projection.Direction.IsPerpendicular(new GeoVector(0, 1, 0)))
                        projection.DrawingPlane = new Plane(Plane.StandardPlane.XZPlane, 0.0);
                    else
                        Frame.UIService.ShowMessageBox(StringTable.GetString("Error.DrawingPlane.Impossible"), StringTable.GetString("Errormessage.Title.InvalidInput"), MessageBoxButtons.OK);
                    return true;
                case "MenuId.DrawingPlane.StandardXZ.Offset":
                    if (!projection.Direction.IsPerpendicular(new GeoVector(0, 1, 0)))
                    {
                        ConstrDrawingPlaneOffset cdpo = new Actions.ConstrDrawingPlaneOffset(new Plane(Plane.StandardPlane.XZPlane, 0.0), projection);
                        cdpo.ActionDoneEvent += new CADability.Actions.ConstructAction.ActionDoneDelegate(OnDrawingPlaneDone);
                        frame.SetAction(cdpo);
                    }
                    else
                        Frame.UIService.ShowMessageBox(StringTable.GetString("Error.DrawingPlane.Impossible"), StringTable.GetString("Errormessage.Title.InvalidInput"), MessageBoxButtons.OK);
                    return true;
                case "MenuId.DrawingPlane.StandardYZ":
                    if (!projection.Direction.IsPerpendicular(new GeoVector(1, 0, 0)))
                        projection.DrawingPlane = new Plane(Plane.StandardPlane.YZPlane, 0.0);
                    else
                        Frame.UIService.ShowMessageBox(StringTable.GetString("Error.DrawingPlane.Impossible"), StringTable.GetString("Errormessage.Title.InvalidInput"), MessageBoxButtons.OK);
                    return true;
                case "MenuId.DrawingPlane.StandardYZ.Offset":
                    if (!projection.Direction.IsPerpendicular(new GeoVector(1, 0, 0)))
                    {
                        ConstrDrawingPlaneOffset cdpo = new Actions.ConstrDrawingPlaneOffset(new Plane(Plane.StandardPlane.YZPlane, 0.0), projection);
                        cdpo.ActionDoneEvent += new CADability.Actions.ConstructAction.ActionDoneDelegate(OnDrawingPlaneDone);
                        frame.SetAction(cdpo);
                    }
                    else
                        Frame.UIService.ShowMessageBox(StringTable.GetString("Error.DrawingPlane.Impossible"), StringTable.GetString("Errormessage.Title.InvalidInput"), MessageBoxButtons.OK);
                    return true;
                case "MenuId.DrawingPlane.Three.Points":
                    ConstructPlane cp = new ConstructPlane("Construct.DrawingPlane");
                    cp.ActionDoneEvent += new CADability.Actions.ConstructAction.ActionDoneDelegate(OnDrawingPlaneDone);
                    frame.SetAction(cp);
                    frame.ShowPropertyDisplay("Action");
                    return true;
                case "MenuId.DrawingPlane.Tangential":
                    ConstructTangentialPlane ct = new ConstructTangentialPlane("Construct.DrawingPlane");
                    ct.ActionDoneEvent += new CADability.Actions.ConstructAction.ActionDoneDelegate(OnDrawingPlaneDone);
                    frame.SetAction(ct);
                    frame.ShowPropertyDisplay("Action");
                    return true;
                case "MenuId.DrawingPlane.Show":
                    projection.ShowDrawingPlane = !projection.ShowDrawingPlane;
                    frame.ActiveView.InvalidateAll();
                    return true;
            }
            ICommandHandler ch = frame as ICommandHandler; // das sollte immer klappen
            if (ch != null) return ch.OnCommand(MenuId);
            return false;
        }
        bool ICommandHandler.OnUpdateCommand(string MenuId, CommandState CommandState)
        {
            switch (MenuId)
            {
                case "MenuId.DrawingPlane.StandardXY":
                case "MenuId.DrawingPlane.StandardXY.Offset":
                    CommandState.Enabled = !projection.Direction.IsPerpendicular(new GeoVector(0, 0, 1));
                    return true;
                case "MenuId.DrawingPlane.StandardXZ":
                case "MenuId.DrawingPlane.StandardXZ.Offset":
                    CommandState.Enabled = !projection.Direction.IsPerpendicular(new GeoVector(0, 1, 0));
                    return true;
                case "MenuId.DrawingPlane.StandardYZ":
                case "MenuId.DrawingPlane.StandardYZ.Offset":
                    CommandState.Enabled = !projection.Direction.IsPerpendicular(new GeoVector(1, 0, 0));
                    return true;
                case "MenuId.DrawingPlane.Three.Points":
                    // immer Enabled
                    return true;
                case "MenuId.DrawingPlane.Show":
                    CommandState.Checked = projection.ShowDrawingPlane;
                    return true;
            }
            return false;
        }
        void ICommandHandler.OnSelected(MenuWithHandler selectedMenuItem, bool selected) { }
        #endregion

        private void OnDrawingPlaneDone(ConstructAction ca, bool success)
        {
            if (!success) return;
            frame.ShowPropertyDisplay("View");
            if (propertyTreeView != null) propertyTreeView.SelectEntry(this);
            Plane pln = Plane.XYPlane;
            ConstructPlane cp = ca as ConstructPlane;
            if (ca is ConstructPlane)
            {
                pln = (ca as ConstructPlane).ConstructedPlane;
            }
            else if (ca is ConstructTangentialPlane)
            {
                pln = (ca as ConstructTangentialPlane).ConstructedPlane;
            }
            else return;
            try
            {
                if (!Precision.IsPerpendicular(projection.Direction, pln.Normal, false))
                {

                    projection.DrawingPlane = pln;
                }
                else
                {
                    Frame.UIService.ShowMessageBox(StringTable.GetString("Error.DrawingPlane.Impossible"), StringTable.GetString("Errormessage.Title.InvalidInput"), MessageBoxButtons.OK);
                }
            }
            catch (ConstructPlane.ConstructPlaneException)
            {   // hat aus irgend einem Grund nicht geklappt
                Frame.UIService.ShowMessageBox(StringTable.GetString("Error.DrawingPlane.Impossible"), StringTable.GetString("Errormessage.Title.InvalidInput"), MessageBoxButtons.OK);
            }
            planeOrigin.Refresh();
            planeDirectionX.Refresh();
            planeDirectionY.Refresh();
            frame.ActiveView.InvalidateAll();
            frame.SetControlCenterFocus("View", null, false, false);
        }

        private void OnOriginModifiedByAction(GeoPointProperty sender)
        {
            planeOrigin.Refresh();
            frame.ActiveView.InvalidateAll();
            frame.SetControlCenterFocus("View", null, false, false);
        }

        private void OnDirectionModifiedByAction(GeoVectorProperty sender)
        {
            planeDirectionX.Refresh();
            planeDirectionY.Refresh();
            frame.ActiveView.InvalidateAll();
            frame.SetControlCenterFocus("View", null, false, false);
        }

        private void OnFocusChanged(IPropertyTreeView sender, IShowProperty NewFocus, IShowProperty OldFocus)
        {   // bei der Änderung der Richtungen soll erst beim Verlassen des Fokus
            // eine Neuberechnung stattfinden
            // propertyTreeView kann hier null sein, da der Focus auch beim
            // zusammenklappen des Eintrages verschwindet
            if (sender.FocusLeft(planeDirectionX, OldFocus, NewFocus))
            {
                Plane pln = projection.DrawingPlane;
                try
                {
                    projection.DrawingPlane = new Plane(pln.Location, dirX, pln.DirectionY);
                    dirX = projection.DrawingPlane.DirectionX;
                    dirY = projection.DrawingPlane.DirectionY;
                    planeDirectionX.Refresh();
                    planeDirectionY.Refresh();
                }
                catch (PlaneException)
                {   // wenns nicht geht, dann halt nicht
                }
                frame.ActiveView.InvalidateAll();
            }
            if (sender.FocusLeft(planeDirectionY, OldFocus, NewFocus))
            {
                Plane pln = projection.DrawingPlane;
                try
                {
                    projection.DrawingPlane = new Plane(pln.Location, pln.DirectionX, dirY);
                    dirX = projection.DrawingPlane.DirectionX;
                    dirY = projection.DrawingPlane.DirectionY;
                    planeDirectionX.Refresh();
                    planeDirectionY.Refresh();
                }
                catch (PlaneException)
                {   // wenns nicht geht, dann halt nicht
                }
                frame.ActiveView.InvalidateAll();
            }
            if (sender.FocusLeft(planeOrigin, OldFocus, NewFocus))
            {
                frame.ActiveView.InvalidateAll();
            }
        }
    }
}
