using CADability.Actions;
using CADability.Substitutes;

namespace CADability.UserInterface
{
    /// <summary>
    /// Property page entry for the drawing plane of a <see cref="Projection"/>, the plane in which 2d constructions
    /// take place. It shows the origin and the x and y directions of the plane as editable sub entries and offers the
    /// context menu "MenuId.DrawingPlane" to select one of the standard planes (optionally with an offset), to define
    /// the plane by three points, by a curve or tangential to a surface, and to show or hide the plane.
    /// </summary>

    public class DrawingPlaneProperty : IShowPropertyImpl, ICommandHandler
    {
        private Projection projection;
        private IFrame frame;
        private IShowProperty[] subEntries;
        GeoPointProperty planeOrigin;
        GeoVectorProperty planeDirectionX;
        GeoVectorProperty planeDirectionY;
        GeoVector dirX, dirY; // the directions typed in the sub entries, valid while dirXModified or dirYModified
        bool dirXModified, dirYModified; // a typed direction is not yet applied to the drawing plane
        bool originModified; // the origin was changed, the view is not yet repainted
        /// <summary>
        /// Creates the entry for the drawing plane of <paramref name="projection"/>, the projection of the view the entry
        /// belongs to.
        /// </summary>
        /// <param name="projection">The projection whose <see cref="Projection.DrawingPlane"/> is edited</param>
        /// <param name="frame">The frame, used to start the actions of the context menu and to access the project</param>
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
            planeDirectionX.ModifiedByActionEvent += new CADability.UserInterface.GeoVectorProperty.ModifiedByActionDelegate(OnDirectionModifiedByAction);
            subEntries[0] = planeOrigin;
            subEntries[1] = planeDirectionX;
            subEntries[2] = planeDirectionY;
            resourceIdInternal = "DrawingPlane";
        }
        /// <summary>
        /// The origin of the drawing plane. Setting it moves the plane of the projection. The "DrawingPlane.Location" sub
        /// entry accesses it via reflection.
        /// </summary>
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
                originModified = true;
            }
        }
        /// <summary>
        /// The x direction of the drawing plane as edited in the "DrawingPlane.DirectionX" sub entry (which accesses it via
        /// reflection). Setting it only stores the value; the plane of the projection is updated when the selection in the
        /// control center leaves the sub entry. Until then the stored value is returned, otherwise the direction of the plane.
        /// </summary>
        public GeoVector PlaneDirectionX
        {
            get
            {
                return dirXModified ? dirX : projection.DrawingPlane.DirectionX;
            }
            set
            {
                dirX = value;
                dirXModified = true;
            }
        }
        /// <summary>
        /// The y direction of the drawing plane as edited in the "DrawingPlane.DirectionY" sub entry (which accesses it via
        /// reflection). Setting it only stores the value; the plane of the projection is updated when the selection in the
        /// control center leaves the sub entry. Until then the stored value is returned, otherwise the direction of the plane.
        /// </summary>
        public GeoVector PlaneDirectionY
        {
            get
            {
                return dirYModified ? dirY : projection.DrawingPlane.DirectionY;
            }
            set
            {
                dirY = value;
                dirYModified = true;
            }
        }
        #region IShowProperty Members
        /// <inheritdoc/>
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
        /// <inheritdoc/>
        public override int SubEntriesCount
        {
            get
            {
                return subEntries.Length;
            }
        }
        /// <inheritdoc/>
        public override IShowProperty[] SubEntries
        {
            get
            {
                return subEntries;
            }
        }
        /// <inheritdoc/>
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
                case "MenuId.DrawingPlane.OfCurve":
                    ConstructPlaneOfCurve cpc = new ConstructPlaneOfCurve("Construct.DrawingPlane");
                    cpc.ActionDoneEvent += new CADability.Actions.ConstructAction.ActionDoneDelegate(OnDrawingPlaneDone);
                    frame.SetAction(cpc);
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
                    return true;
                case "MenuId.DrawingPlane.OfCurve":
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
            if (ca is IConstructPlane icp)
            {
                pln = icp.ConstructedPlane;
            }
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

        /// <summary>
        /// Starts listening to the selection of the page the entry is shown in, see <see cref="OnSelectionChanged"/>.
        /// </summary>
        /// <param name="pp">the property page the entry was added to</param>
        public override void Added(IPropertyPage pp)
        {
            if (propertyPage != null) propertyPage.OnSelectionChanged -= OnSelectionChanged;
            base.Added(pp);
            pp.OnSelectionChanged += OnSelectionChanged;
        }
        /// <summary>
        /// Stops listening to the selection and applies the changes that are still pending, because the selection will
        /// not leave the sub entries any more.
        /// </summary>
        /// <param name="pp">the property page the entry was removed from</param>
        public override void Removed(IPropertyPage pp)
        {
            if (propertyPage != null) propertyPage.OnSelectionChanged -= OnSelectionChanged;
            ApplyPendingChanges(null, false); // no refresh: the page is just updating its list of entries
            base.Removed(pp);
        }
        private void OnSelectionChanged(IPropertyEntry unselected, IPropertyEntry selected)
        {
            ApplyPendingChanges(selected, true);
        }
        /// <summary>
        /// A typed direction is applied to the drawing plane only when the selection leaves its entry, so the plane is not
        /// recomputed for every keystroke or for the intermediate vector while the components are typed one after the
        /// other. The same holds for the repaint after the origin was changed.
        /// </summary>
        /// <param name="selected">the newly selected entry, null to apply everything</param>
        /// <param name="refreshEntries">true to show the resulting directions in the sub entries</param>
        private void ApplyPendingChanges(IPropertyEntry selected, bool refreshEntries)
        {
            bool repaint = false;
            if (dirXModified && !PropertyEntryTree.IsWithin(selected, planeDirectionX))
            {
                dirXModified = false;
                Plane pln = projection.DrawingPlane;
                try
                {
                    projection.DrawingPlane = new Plane(pln.Location, dirX, pln.DirectionY);
                }
                catch (PlaneException)
                {   // invalid direction (e.g. parallel to the y direction): the plane stays as it is
                }
                repaint = true;
            }
            if (dirYModified && !PropertyEntryTree.IsWithin(selected, planeDirectionY))
            {
                dirYModified = false;
                Plane pln = projection.DrawingPlane;
                try
                {
                    projection.DrawingPlane = new Plane(pln.Location, pln.DirectionX, dirY);
                }
                catch (PlaneException)
                {   // invalid direction (e.g. parallel to the x direction): the plane stays as it is
                }
                repaint = true;
            }
            if (originModified && !PropertyEntryTree.IsWithin(selected, planeOrigin))
            {
                originModified = false;
                repaint = true;
            }
            if (repaint)
            {
                // show the directions of the plane as they are now (the plane may have adjusted the other direction, or
                // rejected an invalid one)
                if (refreshEntries && !dirXModified) planeDirectionX.Refresh();
                if (refreshEntries && !dirYModified) planeDirectionY.Refresh();
                frame.ActiveView?.InvalidateAll();
            }
        }
    }
}
