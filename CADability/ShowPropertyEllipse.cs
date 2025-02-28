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
        private Ellipse ellipse;
        private GeoPointProperty centerProperty;
        private LengthProperty majorRadiusProperty;
        private LengthHotSpot[] majorRadiusHotSpot;
        private LengthProperty minorRadiusProperty;
        private LengthHotSpot[] minorRadiusHotSpot;
        private GeoVectorProperty majorAxisProperty;
        private GeoVectorHotSpot[] majorAxisHotSpot;
        private GeoVectorProperty minorAxisProperty;
        private GeoVectorHotSpot[] minorAxisHotSpot;
        private AngleProperty startAngleProperty;
        private AngleProperty endAngleProperty;
        private AngleHotSpot startAngleHotSpot;
        private AngleHotSpot endAngleHotSpot;
        private BooleanProperty directionProperty;
        private GeoPointProperty startPointProperty;
        private GeoPointProperty endPointProperty;

        private IPropertyEntry[] subEntries;
        private IPropertyEntry[] attributeProperties; // Anzeigen für die Attribute (Ebene, Farbe u.s.w)

        public ShowPropertyEllipse(Ellipse Ellipse, IFrame frame): base(frame)
        {
            this.ellipse = Ellipse;

            centerProperty = new GeoPointProperty("Ellipse.Center", Frame, true);
            centerProperty.SetGeoPointEvent += new CADability.UserInterface.GeoPointProperty.SetGeoPointDelegate(OnSetCenter);
            centerProperty.GetGeoPointEvent += new CADability.UserInterface.GeoPointProperty.GetGeoPointDelegate(OnGetCenter);
            centerProperty.ModifyWithMouseEvent += new ModifyWithMouseDelegate(ModifyCenterWithMouse);
            centerProperty.PropertyEntryChangedStateEvent+= new PropertyEntryChangedStateDelegate(OnStateChanged);

            majorRadiusProperty = new LengthProperty("Ellipse.MajorRadius", Frame, true);
            majorRadiusProperty.SetLengthEvent += new CADability.UserInterface.LengthProperty.SetLengthDelegate(OnSetMajorRadius);
            majorRadiusProperty.GetLengthEvent += new CADability.UserInterface.LengthProperty.GetLengthDelegate(OnGetMajorRadius);
            majorRadiusProperty.ModifyWithMouseEvent += new ModifyWithMouseDelegate(ModifyMajorRadiusWithMouse);
            majorRadiusProperty.PropertyEntryChangedStateEvent+= new PropertyEntryChangedStateDelegate(OnStateChanged);
            majorRadiusHotSpot = new LengthHotSpot[2];
            majorRadiusHotSpot[0] = new LengthHotSpot(majorRadiusProperty);
            majorRadiusHotSpot[0].Position = ellipse.Center + 2.0 / 3.0 * ellipse.MajorAxis;
            majorRadiusHotSpot[1] = new LengthHotSpot(majorRadiusProperty);
            majorRadiusHotSpot[1].Position = ellipse.Center - 2.0 / 3.0 * ellipse.MajorAxis;

            minorRadiusProperty = new LengthProperty("Ellipse.MinorRadius", Frame, true);
            minorRadiusProperty.SetLengthEvent += new CADability.UserInterface.LengthProperty.SetLengthDelegate(OnSetMinorRadius);
            minorRadiusProperty.GetLengthEvent += new CADability.UserInterface.LengthProperty.GetLengthDelegate(OnGetMinorRadius);
            minorRadiusProperty.ModifyWithMouseEvent += new ModifyWithMouseDelegate(ModifyMinorRadiusWithMouse);
            minorRadiusProperty.PropertyEntryChangedStateEvent+= new PropertyEntryChangedStateDelegate(OnStateChanged);
            minorRadiusHotSpot = new LengthHotSpot[2];
            minorRadiusHotSpot[0] = new LengthHotSpot(minorRadiusProperty);
            minorRadiusHotSpot[0].Position = ellipse.Center + 2.0 / 3.0 * ellipse.MinorAxis;
            minorRadiusHotSpot[1] = new LengthHotSpot(minorRadiusProperty);
            minorRadiusHotSpot[1].Position = ellipse.Center - 2.0 / 3.0 * ellipse.MinorAxis;

            majorAxisProperty = new GeoVectorProperty("Ellipse.MajorAxis", Frame, true);
            majorAxisProperty.SetHotspotPosition(ellipse.Center + ellipse.MajorAxis);
            majorAxisProperty.ModifyWithMouseEvent += new ModifyWithMouseDelegate(ModifyMajorAxisWithMouse);
            majorAxisProperty.SetGeoVectorEvent += new CADability.UserInterface.GeoVectorProperty.SetGeoVectorDelegate(OnSetMajorAxis);
            majorAxisProperty.GetGeoVectorEvent += new CADability.UserInterface.GeoVectorProperty.GetGeoVectorDelegate(OnGetMajorAxis);
            majorAxisProperty.PropertyEntryChangedStateEvent+= new PropertyEntryChangedStateDelegate(OnStateChanged);
            majorAxisHotSpot = new GeoVectorHotSpot[2];
            majorAxisHotSpot[0] = new GeoVectorHotSpot(majorAxisProperty);
            majorAxisHotSpot[0].Position = ellipse.Center + ellipse.MajorAxis;
            majorAxisHotSpot[1] = new GeoVectorHotSpot(majorAxisProperty);
            majorAxisHotSpot[1].Position = ellipse.Center - ellipse.MajorAxis;

            minorAxisProperty = new GeoVectorProperty("Ellipse.MinorAxis", Frame, true);
            minorAxisProperty.SetHotspotPosition(ellipse.Center + ellipse.MinorAxis);
            minorAxisProperty.ModifyWithMouseEvent += new ModifyWithMouseDelegate(ModifyMinorAxisWithMouse);
            minorAxisProperty.SetGeoVectorEvent += new CADability.UserInterface.GeoVectorProperty.SetGeoVectorDelegate(OnSetMinorAxis);
            minorAxisProperty.GetGeoVectorEvent += new CADability.UserInterface.GeoVectorProperty.GetGeoVectorDelegate(OnGetMinorAxis);
            minorAxisProperty.PropertyEntryChangedStateEvent+= new PropertyEntryChangedStateDelegate(OnStateChanged);
            minorAxisHotSpot = new GeoVectorHotSpot[2];
            minorAxisHotSpot[0] = new GeoVectorHotSpot(minorAxisProperty);
            minorAxisHotSpot[0].Position = ellipse.Center + ellipse.MinorAxis;
            minorAxisHotSpot[1] = new GeoVectorHotSpot(minorAxisProperty);
            minorAxisHotSpot[1].Position = ellipse.Center - ellipse.MinorAxis;

            if (Ellipse.IsArc)
            {
                startAngleProperty = new AngleProperty("Ellipse.StartAngle", Frame, true);
                startAngleProperty.GetAngleEvent += new AngleProperty.GetAngleDelegate(OnGetStartAngle);
                startAngleProperty.SetAngleEvent += new AngleProperty.SetAngleDelegate(OnSetStartAngle);
                startAngleProperty.ModifyWithMouseEvent += new ModifyWithMouseDelegate(ModifyStartAngleWithMouse);
                startAngleProperty.PropertyEntryChangedStateEvent+= new PropertyEntryChangedStateDelegate(OnStateChanged);
                startAngleHotSpot = new AngleHotSpot(startAngleProperty);
                startAngleHotSpot.Position = ellipse.StartPoint;

                endAngleProperty = new AngleProperty("Ellipse.EndAngle", Frame, true);
                endAngleProperty.GetAngleEvent += new AngleProperty.GetAngleDelegate(OnGetEndAngle);
                endAngleProperty.SetAngleEvent += new AngleProperty.SetAngleDelegate(OnSetEndAngle);
                endAngleProperty.ModifyWithMouseEvent += new ModifyWithMouseDelegate(ModifyEndAngleWithMouse);
                endAngleProperty.PropertyEntryChangedStateEvent+= new PropertyEntryChangedStateDelegate(OnStateChanged);
                endAngleHotSpot = new AngleHotSpot(endAngleProperty);
                endAngleHotSpot.Position = ellipse.EndPoint;

                directionProperty = new BooleanProperty("Ellipse.Direction", "Arc.Direction.Values");
                directionProperty.BooleanValue = ellipse.SweepParameter > 0.0;
                directionProperty.GetBooleanEvent += new CADability.UserInterface.BooleanProperty.GetBooleanDelegate(OnGetDirection);
                directionProperty.SetBooleanEvent += new CADability.UserInterface.BooleanProperty.SetBooleanDelegate(OnSetDirection);
                // hat keinen Hotspot
                startPointProperty = new GeoPointProperty("Ellipse.StartPoint", Frame, false);
                startPointProperty.GetGeoPointEvent += new CADability.UserInterface.GeoPointProperty.GetGeoPointDelegate(OnGetStartPoint);
                startPointProperty.ReadOnly = true;
                startPointProperty.PropertyEntryChangedStateEvent += new PropertyEntryChangedStateDelegate(OnStateChanged);
                endPointProperty = new GeoPointProperty("Ellipse.EndPoint", Frame, false);
                endPointProperty.GetGeoPointEvent += new CADability.UserInterface.GeoPointProperty.GetGeoPointDelegate(OnGetEndPoint);
                endPointProperty.ReadOnly = true;
                endPointProperty.PropertyEntryChangedStateEvent += new PropertyEntryChangedStateDelegate(OnStateChanged);
                base.resourceIdInternal = "EllipseArc.Object";
            }
            else
            {
                if (Settings.GlobalSettings.GetBoolValue("CircleShowStartPointProperty", false))
                {
                    startAngleProperty = new AngleProperty("Ellipse.StartAngle", Frame, true);
                    startAngleProperty.GetAngleEvent += new AngleProperty.GetAngleDelegate(OnGetStartAngle);
                    startAngleProperty.SetAngleEvent += new AngleProperty.SetAngleDelegate(OnSetStartAngle);
                    startAngleProperty.ModifyWithMouseEvent += new ModifyWithMouseDelegate(ModifyStartAngleWithMouse);
                    startAngleProperty.PropertyEntryChangedStateEvent += new PropertyEntryChangedStateDelegate(OnStateChanged);
                    startAngleHotSpot = new AngleHotSpot(startAngleProperty);
                    startAngleHotSpot.Position = ellipse.StartPoint;

                    directionProperty = new BooleanProperty("Ellipse.Direction", "Arc.Direction.Values");
                    directionProperty.BooleanValue = ellipse.SweepParameter > 0.0;
                    directionProperty.GetBooleanEvent += new CADability.UserInterface.BooleanProperty.GetBooleanDelegate(OnGetDirection);
                    directionProperty.SetBooleanEvent += new CADability.UserInterface.BooleanProperty.SetBooleanDelegate(OnSetDirection);
                    // hat keinen Hotspot
                    startPointProperty = new GeoPointProperty("Ellipse.StartPoint", Frame, false);
                    startPointProperty.GetGeoPointEvent += new CADability.UserInterface.GeoPointProperty.GetGeoPointDelegate(OnGetStartPoint);
                    startPointProperty.ReadOnly = true;
                    startPointProperty.PropertyEntryChangedStateEvent += new PropertyEntryChangedStateDelegate(OnStateChanged);
                }
                base.resourceIdInternal = "Ellipse.Object";
            }

            attributeProperties = ellipse.GetAttributeProperties(Frame);
        }
        private void OnGeoObjectDidChange(IGeoObject Sender, GeoObjectChange Change)
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
        /// <param name="propertyPage"></param>
        public override void Added(IPropertyPage propertyPage)
        {
            base.Added(propertyPage);
            ellipse.DidChangeEvent += new ChangeDelegate(OnGeoObjectDidChange);
            ellipse.UserData.UserDataAddedEvent += new UserData.UserDataAddedDelegate(OnUserDataAdded);
            ellipse.UserData.UserDataRemovedEvent += new UserData.UserDataRemovedDelegate(OnUserDataAdded);
        }
        public override void Removed(IPropertyPage propertyPage)
        {
            base.Removed(propertyPage);
            ellipse.DidChangeEvent -= new ChangeDelegate(OnGeoObjectDidChange);
            ellipse.UserData.UserDataAddedEvent -= new UserData.UserDataAddedDelegate(OnUserDataAdded);
            ellipse.UserData.UserDataRemovedEvent -= new UserData.UserDataRemovedDelegate(OnUserDataAdded);
        }
        void OnUserDataAdded(string name, object value)
        {
            this.subEntries = null;
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
                if (subEntries == null)
                {
                    ArrayList prop = new ArrayList();
                    prop.Add(centerProperty);
                    prop.Add(majorRadiusProperty);
                    prop.Add(minorRadiusProperty);
                    prop.Add(majorAxisProperty);
                    prop.Add(minorAxisProperty);
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
                    subEntries = PropertyEntryImpl.Concat(mainProps, attributeProperties);
                }
                return subEntries;
            }
        }

        public override void Opened(bool IsOpen)
        {
            if (HotspotChangedEvent != null)
            {
                if (IsOpen)
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
            base.Opened(IsOpen);
        }
#endregion

#region IDisplayHotSpots Members

        public event CADability.HotspotChangedDelegate HotspotChangedEvent;

        public void ReloadProperties()
        {
            // nicht notwendig, die properties bleiben immer die selben
        }

#endregion
        private void ModifyCenterWithMouse(IPropertyEntry sender, bool StartModifying)
        {
            GeneralGeoPointAction gpa = new GeneralGeoPointAction(centerProperty, ellipse);
            Frame.SetAction(gpa);
        }
        private void ModifyMajorAxisWithMouse(IPropertyEntry sender, bool StartModifying)
        {
            GeneralGeoVectorAction gva = new GeneralGeoVectorAction(majorAxisProperty, ellipse.Center, ellipse);
            Frame.SetAction(gva);
        }
        private void OnSetMajorAxis(GeneralGeoPointAction sender, GeoPoint NewValue)
        {
            GeoVector majax = NewValue - ellipse.Center; // ändert auch den Radius
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
        private void ModifyMinorAxisWithMouse(IPropertyEntry sender, bool StartModifying)
        {
            GeneralGeoPointAction gpa = new GeneralGeoPointAction(ellipse.Center, ellipse);
            gpa.SetGeoPointEvent += new CADability.Actions.GeneralGeoPointAction.SetGeoPointDelegate(OnSetMinorAxis);
            Frame.SetAction(gpa);
        }
        private void OnSetMinorAxis(GeneralGeoPointAction sender, GeoPoint NewValue)
        {
            GeoVector minax = NewValue - ellipse.Center; // ändert auch den Radius
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
        private GeoPoint OnGetEndPoint(GeoPointProperty sender)
        {
            return ellipse.EndPoint;
        }
        private GeoPoint OnGetStartPoint(GeoPointProperty sender)
        {
            return ellipse.StartPoint;
        }
        private void OnSetCenter(GeoPointProperty sender, GeoPoint p)
        {
            ellipse.Center = p;
        }
        private GeoPoint OnGetCenter(GeoPointProperty sender)
        {
            return ellipse.Center;
        }
        private void OnSetMajorRadius(LengthProperty sender, double l)
        {
            ellipse.MajorRadius = l;
        }
        private double OnGetMajorRadius(LengthProperty sender)
        {
            return ellipse.MajorRadius;
        }
        private void ModifyMajorRadiusWithMouse(IPropertyEntry sender, bool StartModifying)
        {
            GeneralLengthAction gla = new GeneralLengthAction(majorRadiusProperty, ellipse.Center, ellipse);
            Frame.SetAction(gla);
        }
        private void OnSetMinorRadius(LengthProperty sender, double l)
        {
            ellipse.MinorRadius = l;
        }
        private double OnGetMinorRadius(LengthProperty sender)
        {
            return ellipse.MinorRadius;
        }
        private void ModifyMinorRadiusWithMouse(IPropertyEntry sender, bool StartModifying)
        {
            GeneralLengthAction gla = new GeneralLengthAction(minorRadiusProperty, ellipse.Center, ellipse);
            Frame.SetAction(gla);
        }
        private void OnSetMajorAxis(GeoVectorProperty sender, GeoVector v)
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
        private GeoVector OnGetMajorAxis(GeoVectorProperty sender)
        {
            return ellipse.MajorAxis;
        }
        private void OnSetMinorAxis(GeoVectorProperty sender, GeoVector v)
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
        private GeoVector OnGetMinorAxis(GeoVectorProperty sender)
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
        private void ModifyStartAngleWithMouse(IPropertyEntry sender, bool StartModifying)
        {
            GeneralAngleAction gaa = new GeneralAngleAction(startAngleProperty, ellipse.Center);
            gaa.CalculateAngleEvent += new CADability.Actions.GeneralAngleAction.CalculateAngleDelegate(OnCalculateEllipseParameter);
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
        private void ModifyEndAngleWithMouse(IPropertyEntry sender, bool StartModifying)
        {
            GeneralAngleAction gaa = new GeneralAngleAction(endAngleProperty, ellipse.Center);
            gaa.CalculateAngleEvent += new CADability.Actions.GeneralAngleAction.CalculateAngleDelegate(OnCalculateEllipseParameter);
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
        private double OnCalculateEllipseParameter(GeoPoint MousePosition)
        {
            GeoPoint2D p = ellipse.Plane.Project(MousePosition);
            p.x /= ellipse.MajorRadius;
            p.y /= ellipse.MinorRadius;
            return Math.Atan2(p.y, p.x);
        }
        private void OnStateChanged(IPropertyEntry sender, StateChangedArgs args)
        {
            if (HotspotChangedEvent != null)
            {
                if (args.EventState == StateChangedArgs.State.Selected || args.EventState == StateChangedArgs.State.SubEntrySelected)
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
                        for (int i = 0; i < majorRadiusHotSpot.Length; i++)
                        {
                            HotspotChangedEvent(majorRadiusHotSpot[i], HotspotChangeMode.Selected);
                        }
                    }
                    else if (sender == minorRadiusProperty)
                    {
                        for (int i = 0; i < minorRadiusHotSpot.Length; i++)
                        {
                            HotspotChangedEvent(minorRadiusHotSpot[i], HotspotChangeMode.Selected);
                        }
                    }
                    else if (sender == majorAxisProperty)
                    {
                        for (int i = 0; i < majorAxisHotSpot.Length; i++)
                        {
                            HotspotChangedEvent(majorAxisHotSpot[i], HotspotChangeMode.Selected);
                        }
                    }
                    else if (sender == minorAxisProperty)
                    {
                        for (int i = 0; i < minorAxisHotSpot.Length; i++)
                        {
                            HotspotChangedEvent(minorAxisHotSpot[i], HotspotChangeMode.Selected);
                        }
                    }
                }
                else if (args.EventState == StateChangedArgs.State.UnSelected)
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
                        for (int i = 0; i < majorRadiusHotSpot.Length; i++)
                        {
                            HotspotChangedEvent(majorRadiusHotSpot[i], HotspotChangeMode.Deselected);
                        }
                    }
                    else if (sender == minorRadiusProperty)
                    {
                        for (int i = 0; i < minorRadiusHotSpot.Length; i++)
                        {
                            HotspotChangedEvent(minorRadiusHotSpot[i], HotspotChangeMode.Deselected);
                        }
                    }
                    else if (sender == majorAxisProperty)
                    {
                        for (int i = 0; i < majorAxisHotSpot.Length; i++)
                        {
                            HotspotChangedEvent(majorAxisHotSpot[i], HotspotChangeMode.Deselected);
                        }
                    }
                    else if (sender == minorAxisProperty)
                    {
                        for (int i = 0; i < minorAxisHotSpot.Length; i++)
                        {
                            HotspotChangedEvent(minorAxisHotSpot[i], HotspotChangeMode.Deselected);
                        }
                    }
                }
            }
        }
#region ICommandHandler Members
        virtual public bool OnCommand(string MenuId)
        {
            switch (MenuId)
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

        virtual public bool OnUpdateCommand(string MenuId, CommandState CommandState)
        {
            switch (MenuId)
            {
                case "MenuId.Reverse":
                    CommandState.Enabled = (ellipse.IsArc);
                    return true;
                case "MenuId.CurveSplit":
                    return true;
                case "MenuId.Ellipse.ToLines":
                    CommandState.Enabled = (Frame.ActiveAction is SelectObjectsAction);
                    return true;
            }
            return false;
        }
        void ICommandHandler.OnSelected(MenuWithHandler selectedMenuItem, bool selected) { }

        #endregion
        #region IGeoObjectShowProperty Members
        public event CADability.GeoObject.CreateContextMenueDelegate CreateContextMenueEvent;
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
