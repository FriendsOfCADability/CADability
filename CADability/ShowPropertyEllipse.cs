using CADability.Actions;
using CADability.GeoObject;
using System;
using System.Collections;
using System.Collections.Generic;

namespace CADability.UserInterface
{
    /// <summary>
    /// Shows the properties of an ellipse
    /// </summary>

    public class ShowPropertyEllipse : PropertyEntryImpl, IDisplayHotSpots, ICommandHandler, IGeoObjectShowProperty
    {
        private readonly Ellipse ellipse;
        private readonly GeoPointProperty centerProperty;
        private readonly LengthProperty majorRadiusProperty;
        private readonly LengthHotSpot[] majorRadiusHotSpot;
        private readonly LengthProperty minorRadiusProperty;
        private readonly LengthHotSpot[] minorRadiusHotSpot;
        private readonly GeoVectorProperty majorAxisProperty;
        private readonly GeoVectorHotSpot[] majorAxisHotSpot;
        private readonly GeoVectorProperty minorAxisProperty;
        private readonly GeoVectorHotSpot[] minorAxisHotSpot;
        private readonly AngleProperty startAngleProperty;
        private readonly AngleProperty endAngleProperty;
        private readonly AngleHotSpot startAngleHotSpot;
        private readonly AngleHotSpot endAngleHotSpot;
        private readonly BooleanProperty directionProperty;
        private readonly GeoPointProperty startPointProperty;
        private readonly GeoPointProperty endPointProperty;

        private IPropertyEntry[] subEntries;
        private IPropertyEntry[] attributeProperties; // Anzeigen für die Attribute (Ebene, Farbe u.s.w)

        public ShowPropertyEllipse(Ellipse ellipse, IFrame frame) : base(frame)
        {
            this.ellipse = ellipse;

            centerProperty = new GeoPointProperty(Frame, "Ellipse.Center");
            centerProperty.OnSetValue = OnSetCenter;
            centerProperty.OnGetValue = OnGetCenter;
            centerProperty.ModifyWithMouse += ModifyCenterWithMouse;
            centerProperty.PropertyEntryChangedStateEvent += OnStateChanged;

            majorRadiusProperty = new LengthProperty(Frame, "Ellipse.MajorRadius");
            majorRadiusProperty.OnSetValue = OnSetMajorRadius;
            majorRadiusProperty.OnGetValue = OnGetMajorRadius;
            majorRadiusProperty.ModifyWithMouse += ModifyMajorRadiusWithMouse;
            majorRadiusProperty.PropertyEntryChangedStateEvent += OnStateChanged;
            majorRadiusHotSpot = new LengthHotSpot[2];
            majorRadiusHotSpot[0] = new LengthHotSpot(majorRadiusProperty);
            majorRadiusHotSpot[0].Position = this.ellipse.Center + 2.0 / 3.0 * this.ellipse.MajorAxis;
            majorRadiusHotSpot[1] = new LengthHotSpot(majorRadiusProperty);
            majorRadiusHotSpot[1].Position = this.ellipse.Center - 2.0 / 3.0 * this.ellipse.MajorAxis;

            minorRadiusProperty = new LengthProperty(Frame, "Ellipse.MinorRadius");
            minorRadiusProperty.OnSetValue = OnSetMinorRadius;
            minorRadiusProperty.OnGetValue = OnGetMinorRadius;
            minorRadiusProperty.ModifyWithMouse += ModifyMinorRadiusWithMouse;
            minorRadiusProperty.PropertyEntryChangedStateEvent += OnStateChanged;
            minorRadiusHotSpot = new LengthHotSpot[2];
            minorRadiusHotSpot[0] = new LengthHotSpot(minorRadiusProperty);
            minorRadiusHotSpot[0].Position = this.ellipse.Center + 2.0 / 3.0 * this.ellipse.MinorAxis;
            minorRadiusHotSpot[1] = new LengthHotSpot(minorRadiusProperty);
            minorRadiusHotSpot[1].Position = this.ellipse.Center - 2.0 / 3.0 * this.ellipse.MinorAxis;

            majorAxisProperty = new GeoVectorProperty(Frame, "Ellipse.MajorAxis");
            majorAxisProperty.SetHotspotPosition(this.ellipse.Center + this.ellipse.MajorAxis);
            majorAxisProperty.ModifyWithMouse += ModifyMajorAxisWithMouse;
            majorAxisProperty.OnSetValue = OnSetMajorAxis;
            majorAxisProperty.OnGetValue = OnGetMajorAxis;
            majorAxisProperty.PropertyEntryChangedStateEvent += OnStateChanged;
            majorAxisHotSpot = new GeoVectorHotSpot[2];
            majorAxisHotSpot[0] = new GeoVectorHotSpot(majorAxisProperty);
            majorAxisHotSpot[0].Position = this.ellipse.Center + this.ellipse.MajorAxis;
            majorAxisHotSpot[1] = new GeoVectorHotSpot(majorAxisProperty);
            majorAxisHotSpot[1].Position = this.ellipse.Center - this.ellipse.MajorAxis;

            minorAxisProperty = new GeoVectorProperty(Frame, "Ellipse.MinorAxis");
            minorAxisProperty.SetHotspotPosition(this.ellipse.Center + this.ellipse.MinorAxis);
            minorAxisProperty.ModifyWithMouse += ModifyMinorAxisWithMouse;
            minorAxisProperty.OnSetValue = OnSetMinorAxis;
            minorAxisProperty.OnGetValue = OnGetMinorAxis;
            minorAxisProperty.PropertyEntryChangedStateEvent += OnStateChanged;
            minorAxisHotSpot = new GeoVectorHotSpot[2];
            minorAxisHotSpot[0] = new GeoVectorHotSpot(minorAxisProperty);
            minorAxisHotSpot[0].Position = this.ellipse.Center + this.ellipse.MinorAxis;
            minorAxisHotSpot[1] = new GeoVectorHotSpot(minorAxisProperty);
            minorAxisHotSpot[1].Position = this.ellipse.Center - this.ellipse.MinorAxis;

            if (ellipse.IsArc)
            {
                startAngleProperty = new AngleProperty(Frame, "Ellipse.StartAngle");
                startAngleProperty.OnGetValue = OnGetStartAngle;
                startAngleProperty.OnSetValue = OnSetStartAngle;
                startAngleProperty.ModifyWithMouse += ModifyStartAngleWithMouse;
                startAngleProperty.PropertyEntryChangedStateEvent += OnStateChanged;
                startAngleHotSpot = new AngleHotSpot(startAngleProperty);
                startAngleHotSpot.Position = this.ellipse.StartPoint;

                endAngleProperty = new AngleProperty(Frame, "Ellipse.EndAngle");
                endAngleProperty.OnGetValue = OnGetEndAngle;
                endAngleProperty.OnSetValue = OnSetEndAngle;
                endAngleProperty.ModifyWithMouse += ModifyEndAngleWithMouse;
                endAngleProperty.PropertyEntryChangedStateEvent += OnStateChanged;
                endAngleHotSpot = new AngleHotSpot(endAngleProperty);
                endAngleHotSpot.Position = this.ellipse.EndPoint;

                directionProperty = new BooleanProperty("Ellipse.Direction", "Arc.Direction.Values");
                directionProperty.BooleanValue = this.ellipse.SweepParameter > 0.0;
                directionProperty.GetBooleanEvent += OnGetDirection;
                directionProperty.SetBooleanEvent += OnSetDirection;
                // hat keinen Hotspot
                startPointProperty = new GeoPointProperty(Frame, "Ellipse.StartPoint");
                startPointProperty.OnGetValue = OnGetStartPoint;
                startPointProperty.ReadOnly = true;
                startPointProperty.PropertyEntryChangedStateEvent += OnStateChanged;
                endPointProperty = new GeoPointProperty(Frame, "Ellipse.EndPoint");
                endPointProperty.OnGetValue = OnGetEndPoint;
                endPointProperty.ReadOnly = true;
                endPointProperty.PropertyEntryChangedStateEvent += OnStateChanged;
                resourceIdInternal = "EllipseArc.Object";
            }
            else
            {
                if (Settings.GlobalSettings.GetBoolValue("CircleShowStartPointProperty", false))
                {
                    startAngleProperty = new AngleProperty(Frame, "Ellipse.StartAngle");
                    startAngleProperty.OnGetValue = OnGetStartAngle;
                    startAngleProperty.OnSetValue = OnSetStartAngle;
                    startAngleProperty.ModifyWithMouse += ModifyStartAngleWithMouse;
                    startAngleProperty.PropertyEntryChangedStateEvent += OnStateChanged;
                    startAngleHotSpot = new AngleHotSpot(startAngleProperty);
                    startAngleHotSpot.Position = this.ellipse.StartPoint;

                    directionProperty = new BooleanProperty("Ellipse.Direction", "Arc.Direction.Values");
                    directionProperty.BooleanValue = this.ellipse.SweepParameter > 0.0;
                    directionProperty.GetBooleanEvent += OnGetDirection;
                    directionProperty.SetBooleanEvent += OnSetDirection;
                    // hat keinen Hotspot
                    startPointProperty = new GeoPointProperty(Frame, "Ellipse.StartPoint");
                    startPointProperty.OnGetValue = OnGetStartPoint;
                    startPointProperty.ReadOnly = true;
                    startPointProperty.PropertyEntryChangedStateEvent += OnStateChanged;
                }
                resourceIdInternal = "Ellipse.Object";
            }

            attributeProperties = this.ellipse.GetAttributeProperties(Frame);
        }
        private void OnGeoObjectDidChange(IGeoObject sender, GeoObjectChange change)
        {
            centerProperty.GeoPointChanged();
            majorRadiusProperty.LengthChanged();
            majorRadiusHotSpot[0].Position = ellipse.Center + 2.0 / 3.0 * ellipse.MajorAxis;
            majorRadiusHotSpot[1].Position = ellipse.Center - 2.0 / 3.0 * ellipse.MajorAxis;
            minorRadiusProperty.LengthChanged();
            minorRadiusHotSpot[0].Position = ellipse.Center + 2.0 / 3.0 * ellipse.MinorAxis;
            minorRadiusHotSpot[1].Position = ellipse.Center - 2.0 / 3.0 * ellipse.MinorAxis;
            majorAxisProperty.GeoVectorChanged();
            majorAxisHotSpot[0].Position = ellipse.Center + ellipse.MajorAxis;
            majorAxisHotSpot[1].Position = ellipse.Center - ellipse.MajorAxis;
            minorAxisProperty.GeoVectorChanged();
            minorAxisHotSpot[0].Position = ellipse.Center + ellipse.MinorAxis;
            minorAxisHotSpot[1].Position = ellipse.Center - ellipse.MinorAxis;
            if (startAngleProperty != null)
            {
                startAngleProperty.AngleChanged();
                startAngleHotSpot.Position = ellipse.StartPoint;
                startPointProperty.Refresh();
                if (endAngleProperty != null)
                {
                    endAngleProperty.AngleChanged();
                    endAngleHotSpot.Position = ellipse.EndPoint;
                    endPointProperty.Refresh();
                }
            }
            if (directionProperty != null)
            {
                directionProperty.Refresh();
            }
        }

        #region PropertyEntryImpl Overrides
        /// <summary>
        /// Overrides <see cref="PropertyEntryImpl.Added"/>
        /// </summary>
        /// <param name="page"></param>
        public override void Added(IPropertyPage page)
        {
            base.Added(page);
            ellipse.DidChangeEvent += OnGeoObjectDidChange;
            ellipse.UserData.UserDataAddedEvent += OnUserDataAdded;
            ellipse.UserData.UserDataRemovedEvent += OnUserDataAdded;
        }
        public override void Removed(IPropertyPage page)
        {
            base.Removed(page);
            ellipse.DidChangeEvent -= OnGeoObjectDidChange;
            ellipse.UserData.UserDataAddedEvent -= OnUserDataAdded;
            ellipse.UserData.UserDataRemovedEvent -= OnUserDataAdded;
        }
        void OnUserDataAdded(string name, object value)
        {
            subEntries = null;
            attributeProperties = ellipse.GetAttributeProperties(Frame);
            propertyPage.Refresh(this);
        }

        public override string LabelText
        {
            get
            {
                if (ellipse.IsArc)
                    return StringTable.GetString("EllipseArc.Object.Label");
                else
                    return StringTable.GetString("Ellipse.Object.Label");
            }
        }
        public override PropertyEntryType Flags => PropertyEntryType.Selectable | PropertyEntryType.ContextMenu | PropertyEntryType.GroupTitle | PropertyEntryType.HasSubEntries;

        public override MenuWithHandler[] ContextMenu
        {
            get
            {
                List<MenuWithHandler> items = new List<MenuWithHandler>(MenuResource.LoadMenuDefinition("MenuId.Object.Ellipse", false, this));
                CreateContextMenueEvent?.Invoke(this, items);
                ellipse.GetAdditionalContextMenue(this, Frame, items);
                return items.ToArray();
            }
        }

        public override IPropertyEntry[] SubItems
        {
            get
            {
                if (subEntries != null) 
                    return subEntries;

                ArrayList prop = new ArrayList
                {
                    centerProperty,
                    majorRadiusProperty,
                    minorRadiusProperty,
                    majorAxisProperty,
                    minorAxisProperty
                };
                centerProperty.GeoPointChanged();
                majorRadiusProperty.LengthChanged();
                minorRadiusProperty.LengthChanged();
                majorAxisProperty.GeoVectorChanged();
                minorAxisProperty.GeoVectorChanged();
                if (startAngleProperty != null)
                {
                    prop.Add(startAngleProperty);
                    startAngleProperty.AngleChanged();
                    if (endAngleProperty != null)
                    {
                        prop.Add(endAngleProperty);
                        endAngleProperty.AngleChanged();
                        prop.Add(endPointProperty);
                    }
                    prop.Add(directionProperty);
                    prop.Add(startPointProperty);
                }
                IPropertyEntry[] mainProps = (IPropertyEntry[])prop.ToArray(typeof(IPropertyEntry));
                subEntries = Concat(mainProps, attributeProperties);
                return subEntries;
            }
        }

        public override void Opened(bool isOpen)
        {
            if (HotspotChangedEvent != null)
            {
                if (isOpen)
                {
                    if (startAngleHotSpot != null)
                    {
                        HotspotChangedEvent(startAngleHotSpot, HotspotChangeMode.Visible);
                    }
                    if (endAngleHotSpot != null)
                    {
                        HotspotChangedEvent(endAngleHotSpot, HotspotChangeMode.Visible);
                    }
                    HotspotChangedEvent(centerProperty, HotspotChangeMode.Visible);
                    HotspotChangedEvent(majorRadiusHotSpot[0], HotspotChangeMode.Visible);
                    HotspotChangedEvent(majorRadiusHotSpot[1], HotspotChangeMode.Visible);
                    HotspotChangedEvent(minorRadiusHotSpot[0], HotspotChangeMode.Visible);
                    HotspotChangedEvent(minorRadiusHotSpot[1], HotspotChangeMode.Visible);
                    HotspotChangedEvent(majorAxisHotSpot[0], HotspotChangeMode.Visible);
                    HotspotChangedEvent(majorAxisHotSpot[1], HotspotChangeMode.Visible);
                    HotspotChangedEvent(minorAxisHotSpot[0], HotspotChangeMode.Visible);
                    HotspotChangedEvent(minorAxisHotSpot[1], HotspotChangeMode.Visible);
                }
                else
                {
                    if (startAngleHotSpot != null)
                    {
                        HotspotChangedEvent(startAngleHotSpot, HotspotChangeMode.Invisible);
                    }
                    if (endAngleHotSpot != null)
                    {
                        HotspotChangedEvent(endAngleHotSpot, HotspotChangeMode.Invisible);
                    }
                    HotspotChangedEvent(centerProperty, HotspotChangeMode.Invisible);
                    HotspotChangedEvent(majorRadiusHotSpot[0], HotspotChangeMode.Invisible);
                    HotspotChangedEvent(majorRadiusHotSpot[1], HotspotChangeMode.Invisible);
                    HotspotChangedEvent(minorRadiusHotSpot[0], HotspotChangeMode.Invisible);
                    HotspotChangedEvent(minorRadiusHotSpot[1], HotspotChangeMode.Invisible);
                    HotspotChangedEvent(majorAxisHotSpot[0], HotspotChangeMode.Invisible);
                    HotspotChangedEvent(majorAxisHotSpot[1], HotspotChangeMode.Invisible);
                    HotspotChangedEvent(minorAxisHotSpot[0], HotspotChangeMode.Invisible);
                    HotspotChangedEvent(minorAxisHotSpot[1], HotspotChangeMode.Invisible);
                }
            }
            base.Opened(isOpen);
        }
        #endregion

        #region IDisplayHotSpots Members

        public event HotspotChangedDelegate HotspotChangedEvent;

        public void ReloadProperties()
        {
            // nicht notwendig, die properties bleiben immer die selben
        }

        #endregion
        private void ModifyCenterWithMouse(IPropertyEntry sender, bool startModifying)
        {
            GeneralGeoPointAction gpa = new GeneralGeoPointAction(centerProperty, ellipse);
            Frame.SetAction(gpa);
        }
        private void ModifyMajorAxisWithMouse(IPropertyEntry sender, bool startModifying)
        {
            GeneralGeoVectorAction gva = new GeneralGeoVectorAction(majorAxisProperty, ellipse.Center, ellipse);
            Frame.SetAction(gva);
        }
        private void OnSetMajorAxis(GeneralGeoPointAction sender, GeoPoint newValue)
        {
            GeoVector majax = newValue - ellipse.Center; // ändert auch den Radius
            GeoVector minax = ellipse.Plane.Normal ^ majax; // oder umgekehrt
            if (Precision.IsNullVector(minax) || Precision.IsNullVector(majax)) return; // nix zu tun
            minax.Norm();
            minax = ellipse.MinorRadius * minax;
            majax.Norm();
            majax = ellipse.MajorRadius * majax;
            ellipse.SetEllipseCenterAxis(ellipse.Center, majax, minax);
            majorAxisProperty.SetHotspotPosition(ellipse.Center + ellipse.MajorAxis);
            minorAxisProperty.SetHotspotPosition(ellipse.Center + ellipse.MinorAxis);
        }
        private void ModifyMinorAxisWithMouse(IPropertyEntry sender, bool startModifying)
        {
            GeneralGeoPointAction gpa = new GeneralGeoPointAction(ellipse.Center, ellipse);
            gpa.SetGeoPointEvent += OnSetMinorAxis;
            Frame.SetAction(gpa);
        }
        private void OnSetMinorAxis(GeneralGeoPointAction sender, GeoPoint newValue)
        {
            GeoVector minax = newValue - ellipse.Center; // ändert auch den Radius
            double s = minax * ellipse.MinorAxis;
            if (s < 0) minax = -minax; // sonst kippt die Ellipse hier um
            GeoVector majax = minax ^ ellipse.Plane.Normal; // oder umgekehrt
            if (Precision.IsNullVector(minax) || Precision.IsNullVector(majax)) return; // nix zu tun
            majax.Norm();
            majax = ellipse.MajorRadius * majax;
            minax.Norm();
            minax = ellipse.MinorRadius * minax;
            ellipse.SetEllipseCenterAxis(ellipse.Center, majax, minax);
            majorAxisProperty.SetHotspotPosition(ellipse.Center + ellipse.MajorAxis);
            minorAxisProperty.SetHotspotPosition(ellipse.Center + ellipse.MinorAxis);
        }
        private GeoPoint OnGetEndPoint()
        {
            return ellipse.EndPoint;
        }
        private GeoPoint OnGetStartPoint()
        {
            return ellipse.StartPoint;
        }
        private void OnSetCenter(GeoPoint p)
        {
            ellipse.Center = p;
        }
        private GeoPoint OnGetCenter()
        {
            return ellipse.Center;
        }
        private void OnSetMajorRadius(double l)
        {
            ellipse.MajorRadius = l;
        }
        private double OnGetMajorRadius()
        {
            return ellipse.MajorRadius;
        }
        private void ModifyMajorRadiusWithMouse(IPropertyEntry sender, bool startModifying)
        {
            GeneralLengthAction gla = new GeneralLengthAction(majorRadiusProperty, ellipse.Center, ellipse);
            Frame.SetAction(gla);
        }
        private void OnSetMinorRadius(double l)
        {
            ellipse.MinorRadius = l;
        }
        private double OnGetMinorRadius()
        {
            return ellipse.MinorRadius;
        }
        private void ModifyMinorRadiusWithMouse(IPropertyEntry sender, bool startModifying)
        {
            GeneralLengthAction gla = new GeneralLengthAction(minorRadiusProperty, ellipse.Center, ellipse);
            Frame.SetAction(gla);
        }
        private void OnSetMajorAxis(GeoVector v)
        {
            GeoVector majax = v; // ändert auch den Radius
            GeoVector minax = ellipse.Plane.Normal ^ majax; // oder umgekehrt
            if (Precision.IsNullVector(minax) || Precision.IsNullVector(majax)) return; // nix zu tun
            minax.Norm();
            minax = ellipse.MinorRadius * minax;
            majax.Norm();
            majax = ellipse.MajorRadius * majax;
            ellipse.SetEllipseCenterAxis(ellipse.Center, majax, minax);
        }
        private GeoVector OnGetMajorAxis()
        {
            return ellipse.MajorAxis;
        }
        private void OnSetMinorAxis(GeoVector v)
        {
            GeoVector minax = v; // ändert auch den Radius
            GeoVector majax = minax ^ ellipse.Plane.Normal; // oder umgekehrt
            if (Precision.IsNullVector(minax) || Precision.IsNullVector(minax)) return; // nix zu tun
            minax.Norm();
            minax = ellipse.MinorRadius * minax;
            majax.Norm();
            majax = ellipse.MajorRadius * majax;
            ellipse.SetEllipseCenterAxis(ellipse.Center, majax, minax);
        }
        private GeoVector OnGetMinorAxis()
        {
            return ellipse.MinorAxis;
        }
        private Angle OnGetStartAngle()
        {
            return new Angle(ellipse.StartParameter);
        }
        private void OnSetStartAngle(Angle a)
        {
            double endParameter = ellipse.StartParameter + ellipse.SweepParameter;
            SweepAngle sa = new SweepAngle(a, new Angle(endParameter), ellipse.SweepParameter > 0.0);
            using (Frame.Project.Undo.UndoFrame)
            {
                ellipse.StartParameter = a.Radian;
                if (ellipse.IsArc) ellipse.SweepParameter = sa.Radian;
            }
        }
        private void ModifyStartAngleWithMouse(IPropertyEntry sender, bool startModifying)
        {
            GeneralAngleAction gaa = new GeneralAngleAction(startAngleProperty, ellipse.Center);
            gaa.CalculateAngleEvent += OnCalculateEllipseParameter;
            Frame.SetAction(gaa);
        }
        private Angle OnGetEndAngle()
        {
            return new Angle(ellipse.StartParameter + ellipse.SweepParameter);
        }
        private void OnSetEndAngle(Angle a)
        {
            SweepAngle sa = new SweepAngle(new Angle(ellipse.StartParameter), a, ellipse.SweepParameter > 0.0);
            ellipse.SweepParameter = sa.Radian;
        }
        private void ModifyEndAngleWithMouse(IPropertyEntry sender, bool startModifying)
        {
            GeneralAngleAction gaa = new GeneralAngleAction(endAngleProperty, ellipse.Center);
            gaa.CalculateAngleEvent += OnCalculateEllipseParameter;
            Frame.SetAction(gaa);
        }
        private bool OnGetDirection()
        {
            return ellipse.SweepParameter > 0.0;
        }
        private void OnSetDirection(bool val)
        {
            if (val)
            {
                if (ellipse.SweepParameter < 0.0) (ellipse as ICurve).Reverse();
            }
            else
            {
                if (ellipse.SweepParameter > 0.0) (ellipse as ICurve).Reverse();
            }
        }
        private double OnCalculateEllipseParameter(GeoPoint mousePosition)
        {
            GeoPoint2D p = ellipse.Plane.Project(mousePosition);
            p.x /= ellipse.MajorRadius;
            p.y /= ellipse.MinorRadius;
            return Math.Atan2(p.y, p.x);
        }
        private void OnStateChanged(IPropertyEntry sender, StateChangedArgs args)
        {
            if (HotspotChangedEvent == null) 
                return;
            switch (args.EventState)
            {
                case StateChangedArgs.State.Selected:
                case StateChangedArgs.State.SubEntrySelected:
                {
                    if (sender == startPointProperty)
                    {
                        HotspotChangedEvent(startPointProperty, HotspotChangeMode.Selected);
                    }
                    else if (sender == endPointProperty)
                    {
                        HotspotChangedEvent(endPointProperty, HotspotChangeMode.Selected);
                    }
                    if (sender == startAngleProperty)
                    {
                        HotspotChangedEvent(startAngleHotSpot, HotspotChangeMode.Selected);
                    }
                    else if (sender == endAngleProperty)
                    {
                        HotspotChangedEvent(endAngleHotSpot, HotspotChangeMode.Selected);
                    }
                    else if (sender == centerProperty)
                    {
                        HotspotChangedEvent(centerProperty, HotspotChangeMode.Selected);
                    }
                    else if (sender == majorRadiusProperty)
                    {
                        foreach (var t in majorRadiusHotSpot)
                        {
                            HotspotChangedEvent(t, HotspotChangeMode.Selected);
                        }
                    }
                    else if (sender == minorRadiusProperty)
                    {
                        foreach (var t in minorRadiusHotSpot)
                        {
                            HotspotChangedEvent(t, HotspotChangeMode.Selected);
                        }
                    }
                    else if (sender == majorAxisProperty)
                    {
                        foreach (var t in majorAxisHotSpot)
                        {
                            HotspotChangedEvent(t, HotspotChangeMode.Selected);
                        }
                    }
                    else if (sender == minorAxisProperty)
                    {
                        foreach (var t in minorAxisHotSpot)
                        {
                            HotspotChangedEvent(t, HotspotChangeMode.Selected);
                        }
                    }

                    break;
                }
                case StateChangedArgs.State.UnSelected:
                {
                    if (sender == startPointProperty)
                    {
                        HotspotChangedEvent(startPointProperty, HotspotChangeMode.Deselected);
                    }
                    else if (sender == endPointProperty)
                    {
                        HotspotChangedEvent(endPointProperty, HotspotChangeMode.Deselected);
                    }
                    if (sender == startAngleProperty)
                    {
                        HotspotChangedEvent(startAngleHotSpot, HotspotChangeMode.Deselected);
                    }
                    else if (sender == endAngleProperty)
                    {
                        HotspotChangedEvent(endAngleHotSpot, HotspotChangeMode.Deselected);
                    }
                    else if (sender == centerProperty)
                    {
                        HotspotChangedEvent(centerProperty, HotspotChangeMode.Deselected);
                    }
                    else if (sender == majorRadiusProperty)
                    {
                        foreach (var t in majorRadiusHotSpot)
                        {
                            HotspotChangedEvent(t, HotspotChangeMode.Deselected);
                        }
                    }
                    else if (sender == minorRadiusProperty)
                    {
                        foreach (var t in minorRadiusHotSpot)
                        {
                            HotspotChangedEvent(t, HotspotChangeMode.Deselected);
                        }
                    }
                    else if (sender == majorAxisProperty)
                    {
                        foreach (var t in majorAxisHotSpot)
                        {
                            HotspotChangedEvent(t, HotspotChangeMode.Deselected);
                        }
                    }
                    else if (sender == minorAxisProperty)
                    {
                        foreach (var t in minorAxisHotSpot)
                        {
                            HotspotChangedEvent(t, HotspotChangeMode.Deselected);
                        }
                    }

                    break;
                }
            }
        }
        #region ICommandHandler Members
        public virtual bool OnCommand(string menuId)
        {
            switch (menuId)
            {
                case "MenuId.Reverse":
                    (ellipse as ICurve).Reverse();
                    return true;
                case "MenuId.CurveSplit":
                    Frame.SetAction(new ConstrSplitCurve(ellipse));
                    return true;
                case "MenuId.Approximate":
                    if (Frame.ActiveAction is SelectObjectsAction)
                    {
                        Curves.Approximate(Frame, ellipse);
                    }
                    return true;
                case "MenuId.Aequidist":
                    Frame.SetAction(new ConstructAequidist(ellipse));
                    return true;
            }
            return false;
        }

        public virtual bool OnUpdateCommand(string menuId, CommandState commandState)
        {
            switch (menuId)
            {
                case "MenuId.Reverse":
                    commandState.Enabled = (ellipse.IsArc);
                    return true;
                case "MenuId.CurveSplit":
                    return true;
                case "MenuId.Ellipse.ToLines":
                    commandState.Enabled = (Frame.ActiveAction is SelectObjectsAction);
                    return true;
            }
            return false;
        }
        void ICommandHandler.OnSelected(MenuWithHandler selectedMenuItem, bool selected) { }

        #endregion
        #region IGeoObjectShowProperty Members
        public event CreateContextMenueDelegate CreateContextMenueEvent;
        IGeoObject IGeoObjectShowProperty.GetGeoObject()
        {
            return ellipse;
        }
        string IGeoObjectShowProperty.GetContextMenuId()
        {
            return "MenuId.Object.Ellipse";
        }
        #endregion

    }
}
