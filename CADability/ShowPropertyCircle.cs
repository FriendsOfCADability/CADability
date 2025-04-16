using CADability.Actions;
using CADability.GeoObject;
using System.Collections;
using System.Collections.Generic;

namespace CADability.UserInterface
{
    /// <summary>
    /// Shows the properties of a circle.
    /// </summary>
    public class ShowPropertyCircle : PropertyEntryImpl, IDisplayHotSpots, ICommandHandler, IGeoObjectShowProperty
    {
        private readonly Ellipse circle;
        private readonly GeoPointProperty centerProperty;
        private readonly LengthProperty radiusProperty;
        private readonly LengthProperty diameterProperty;
        private readonly LengthHotSpot[] radiusHotSpots;
        private readonly AngleProperty startAngleProperty;
        private readonly AngleProperty endAngleProperty;
        private readonly GeoPointProperty startPointProperty;
        private readonly GeoPointProperty endPointProperty;
        private readonly LengthProperty arcLengthProperty;
        private readonly BooleanProperty directionProperty;
        private readonly AngleHotSpot startAngleHotSpot;
        private readonly AngleHotSpot endAngleHotSpot;
        private IPropertyEntry[] subEntries;
        private IPropertyEntry[] attributeProperties; 

        public ShowPropertyCircle(Ellipse circle, IFrame frame): base(frame)
        {
            this.circle = circle;
            centerProperty = new GeoPointProperty(Frame, "Circle.Center");
            centerProperty.OnGetValue = OnGetCenter;
            centerProperty.OnSetValue = OnSetCenter;
            centerProperty.ModifyWithMouse += ModifyCenterWithMouse;
            centerProperty.PropertyEntryChangedStateEvent+= OnStateChanged;

            radiusProperty = new LengthProperty(Frame, "Circle.Radius");
            radiusProperty.OnGetValue = OnGetRadius;
			radiusProperty.OnSetValue = OnSetRadius;
            radiusProperty.ModifyWithMouse += ModifyRadiusWithMouse;
            radiusProperty.PropertyEntryChangedStateEvent+= OnStateChanged;
            radiusHotSpots = new LengthHotSpot[4];
            for (int i = 0; i < 4; ++i)
            {
                radiusHotSpots[i] = new LengthHotSpot(radiusProperty);
                switch (i)
                {
                    case 0:
                        radiusHotSpots[i].Position = circle.Center + circle.MajorAxis;
                        break;
                    case 1:
                        radiusHotSpots[i].Position = circle.Center - circle.MajorAxis;
                        break;
                    case 2:
                        radiusHotSpots[i].Position = circle.Center + circle.MinorAxis;
                        break;
                    case 3:
                        radiusHotSpots[i].Position = circle.Center - circle.MinorAxis;
                        break;
                }
            }

            diameterProperty = new LengthProperty(Frame, "Circle.Diameter");
            diameterProperty.OnGetValue = OnGetDiameter;
			diameterProperty.OnSetValue = OnSetDiameter;
            diameterProperty.ModifyWithMouse += ModifyRadiusWithMouse;
            diameterProperty.PropertyEntryChangedStateEvent+= OnStateChanged;

            if (circle.IsArc)
            {
                startAngleProperty = new AngleProperty(Frame, "Arc.StartAngle");
                startAngleProperty.OnGetValue = OnGetStartAngle;
                startAngleProperty.OnSetValue = OnSetStartAngle;
                startAngleProperty.ModifyWithMouse += ModifyStartAngleWithMouse;
                startAngleProperty.PropertyEntryChangedStateEvent+= OnStateChanged;
                startAngleHotSpot = new AngleHotSpot(startAngleProperty);
                startAngleHotSpot.Position = circle.StartPoint;

                endAngleProperty = new AngleProperty(Frame, "Arc.EndAngle");
				endAngleProperty.OnGetValue = OnGetEndAngle;
                endAngleProperty.OnSetValue = OnSetEndAngle;
                endAngleProperty.ModifyWithMouse += ModifyEndAngleWithMouse;
                endAngleProperty.PropertyEntryChangedStateEvent+= OnStateChanged;
                endAngleHotSpot = new AngleHotSpot(endAngleProperty);
                endAngleHotSpot.Position = circle.EndPoint;
                base.resourceIdInternal = "CircleArc.Object";

                directionProperty = new BooleanProperty("Arc.Direction", "Arc.Direction.Values");
                directionProperty.BooleanValue = circle.SweepParameter > 0.0;
                directionProperty.GetBooleanEvent += OnGetDirection;
                directionProperty.SetBooleanEvent += OnSetDirection;
                // hat keinen Hotspot
                startPointProperty = new GeoPointProperty(Frame, "Arc.StartPoint");
                startPointProperty.OnGetValue = OnGetStartPoint;
                startPointProperty.ReadOnly = true;
                startPointProperty.PropertyEntryChangedStateEvent+= OnStateChanged;                
                endPointProperty = new GeoPointProperty(Frame, "Arc.EndPoint");
                endPointProperty.OnGetValue = OnGetEndPoint;
                endPointProperty.ReadOnly = true;
                endPointProperty.PropertyEntryChangedStateEvent+= OnStateChanged;
            }
            else
            {
                if (Settings.GlobalSettings.GetBoolValue("CircleShowStartPointProperty", false))
                {
                    startAngleProperty = new AngleProperty(Frame, "Arc.StartAngle");
                    startAngleProperty.OnGetValue = OnGetStartAngle;
					startAngleProperty.OnSetValue = OnSetStartAngle;
                    startAngleProperty.ModifyWithMouse += ModifyStartAngleWithMouse;
                    startAngleProperty.PropertyEntryChangedStateEvent+= OnStateChanged;
                    startAngleHotSpot = new AngleHotSpot(startAngleProperty);
                    startAngleHotSpot.Position = circle.StartPoint;

                    directionProperty = new BooleanProperty("Arc.Direction", "Arc.Direction.Values");
                    directionProperty.BooleanValue = circle.SweepParameter > 0.0;
                    directionProperty.GetBooleanEvent += OnGetDirection;
                    directionProperty.SetBooleanEvent += OnSetDirection;
                    // hat keinen Hotspot
                    startPointProperty = new GeoPointProperty(Frame, "Arc.StartPoint");
                    startPointProperty.OnGetValue = OnGetStartPoint;
                    startPointProperty.ReadOnly = true;
                    startPointProperty.PropertyEntryChangedStateEvent+= OnStateChanged;
                }
                base.resourceIdInternal = "Circle.Object";
            }
            arcLengthProperty = new LengthProperty(Frame, "Circle.ArcLength");
            arcLengthProperty.OnGetValue = OnGetArcLength;
            arcLengthProperty.ReadOnly = true;
            attributeProperties = circle.GetAttributeProperties(Frame);
        }
        private void OnGeoObjectDidChange(IGeoObject sender, GeoObjectChange change)
        {
            centerProperty.GeoPointChanged();
            radiusProperty.LengthChanged();
            diameterProperty.LengthChanged();
            if (radiusHotSpots != null)
            {
                radiusHotSpots[0].Position = circle.Center + circle.MajorAxis;
                radiusHotSpots[1].Position = circle.Center - circle.MajorAxis;
                radiusHotSpots[2].Position = circle.Center + circle.MinorAxis;
                radiusHotSpots[3].Position = circle.Center - circle.MinorAxis;
                if (HotspotChangedEvent != null)
                {
                    HotspotChangedEvent(radiusHotSpots[0], HotspotChangeMode.Moved);
                    HotspotChangedEvent(radiusHotSpots[1], HotspotChangeMode.Moved);
                    HotspotChangedEvent(radiusHotSpots[2], HotspotChangeMode.Moved);
                    HotspotChangedEvent(radiusHotSpots[3], HotspotChangeMode.Moved);
                }
            }
            if (startAngleProperty != null)
            {
                startAngleProperty.AngleChanged();
                endAngleProperty?.AngleChanged();
                startPointProperty.Refresh();
                if (endAngleProperty != null) endPointProperty.Refresh();
                startAngleHotSpot.Position = circle.StartPoint;
                if (endAngleProperty != null) endAngleHotSpot.Position = circle.EndPoint;
                if (HotspotChangedEvent != null)
                {
                    HotspotChangedEvent(startAngleHotSpot, HotspotChangeMode.Moved);
                    if (endAngleProperty != null) HotspotChangedEvent(endAngleHotSpot, HotspotChangeMode.Moved);
                }
            }
            arcLengthProperty?.Refresh();
            directionProperty?.Refresh();
        }

        #region PropertyEntryImpl Overrides
        public override PropertyEntryType Flags => PropertyEntryType.ContextMenu | PropertyEntryType.Selectable | PropertyEntryType.GroupTitle | PropertyEntryType.HasSubEntries;

        public override IPropertyEntry[] SubItems
        {
            get
            {
                if (subEntries == null)
                {
                    ArrayList prop = new ArrayList();
                    prop.Add(centerProperty);
                    prop.Add(radiusProperty);
                    prop.Add(diameterProperty);
                    centerProperty.GeoPointChanged();
                    radiusProperty.LengthChanged();
                    diameterProperty.LengthChanged();
                    if (startAngleProperty != null)
                    {
                        prop.Add(startAngleProperty);
                        startAngleProperty.AngleChanged();
                        prop.Add(startPointProperty);
                        if (endAngleProperty != null)
                        {
                            prop.Add(endAngleProperty);
                            endAngleProperty.AngleChanged();
                            prop.Add(endPointProperty);
                        }
                        prop.Add(directionProperty);
                    }
                    prop.Add(arcLengthProperty);
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
                    if (startAngleHotSpot != null) HotspotChangedEvent(startAngleHotSpot, HotspotChangeMode.Visible);
                    if (endAngleHotSpot != null) HotspotChangedEvent(endAngleHotSpot, HotspotChangeMode.Visible);
                    HotspotChangedEvent(centerProperty, HotspotChangeMode.Visible);
                    for (int i = 0; i < 4; ++i)
                        HotspotChangedEvent(radiusHotSpots[i], HotspotChangeMode.Visible);
                }
                else
                {
                    if (startAngleHotSpot != null) HotspotChangedEvent(startAngleHotSpot, HotspotChangeMode.Invisible);
                    if (endAngleHotSpot != null) HotspotChangedEvent(endAngleHotSpot, HotspotChangeMode.Invisible);
                    HotspotChangedEvent(centerProperty, HotspotChangeMode.Invisible);
                    for (int i = 0; i < 4; ++i)
                        HotspotChangedEvent(radiusHotSpots[i], HotspotChangeMode.Invisible);
                }
            }
            base.Opened(IsOpen);
        }
        /// <summary>
        /// Overrides <see cref="PropertyEntryImpl.Removed"/>
        /// </summary>
        /// <param name="propertyTreeView">the IPropertyTreeView from which it was removed</param>
        public override void Removed(IPropertyPage propertyTreeView)
        {
            this.circle.DidChangeEvent -= OnGeoObjectDidChange;
            circle.UserData.UserDataAddedEvent -= OnUserDataAdded;
            circle.UserData.UserDataRemovedEvent -= OnUserDataAdded;
            base.Removed(propertyTreeView);
        }
        public override void Added(IPropertyPage propertyTreeView)
        {
            this.circle.DidChangeEvent += OnGeoObjectDidChange;
            circle.UserData.UserDataAddedEvent += OnUserDataAdded;
            circle.UserData.UserDataRemovedEvent += OnUserDataAdded;
            base.Added(propertyTreeView);
        }
        void OnUserDataAdded(string name, object value)
        {
            this.subEntries = null;
            attributeProperties = circle.GetAttributeProperties(Frame);
            propertyPage.Refresh(this);
        }

        public override MenuWithHandler[] ContextMenu
        {
            get
            {
                List<MenuWithHandler> items = new List<MenuWithHandler>(MenuResource.LoadMenuDefinition("MenuId.Object.Ellipse", false, this));
                CreateContextMenueEvent?.Invoke(this, items);
                circle.GetAdditionalContextMenue(this, Frame, items);
                return items.ToArray();
            }
        }



#endregion

#region IDisplayHotSpots Members

        public event CADability.HotspotChangedDelegate HotspotChangedEvent;

        public void ReloadProperties()
        {
            // TODO:  Add ShowPropertyCircle.ReloadProperties implementation
        }

#endregion

        private GeoPoint OnGetEndPoint()
        {
            return circle.EndPoint;
        }
        private GeoPoint OnGetStartPoint()
        {
            return circle.StartPoint;
        }
        private GeoPoint OnGetCenter()
        {
            return circle.Center;
        }
        private void OnSetCenter(GeoPoint p)
        {
            circle.Center = p;
        }
        private void ModifyCenterWithMouse(IPropertyEntry sender, bool startModifying)
        {
            GeneralGeoPointAction gpa = new GeneralGeoPointAction(centerProperty, circle);
            Frame.SetAction(gpa);
        }
        private double OnGetRadius()
        {
            return circle.Radius;
        }
        private double OnGetDiameter()
        {
            return circle.Radius * 2.0;
        }
        private double OnGetArcLength()
        {
            return circle.Length;
        }
        private void OnSetRadius(double l)
        {
            circle.Radius = l;
        }
        private void OnSetDiameter(double l)
        {
            circle.Radius = l / 2.0;
        }
        private void ModifyRadiusWithMouse(IPropertyEntry sender, bool startModifying)
        {
            GeneralLengthAction gla = new GeneralLengthAction(radiusProperty, circle.Center, circle);
            Frame.SetAction(gla);
        }
        private Angle OnGetStartAngle()
        {
            return new Angle(circle.StartParameter);
        }
        private void OnSetStartAngle(Angle a)
        {
            circle.StartParameter = a.Radian;
        }
        private void ModifyStartAngleWithMouse(IPropertyEntry sender, bool startModifying)
        {
            GeneralAngleAction gaa = new GeneralAngleAction(startAngleProperty, circle.Plane);
            Frame.SetAction(gaa);
        }
        private Angle OnGetEndAngle()
        {
            return new Angle(circle.StartParameter + circle.SweepParameter);
        }
        private void OnSetEndAngle(Angle a)
        {
            SweepAngle sa = new SweepAngle(new Angle(circle.StartParameter), a, circle.SweepParameter > 0.0);
            circle.SweepParameter = sa.Radian;
        }
        private bool OnGetDirection()
        {
            return circle.SweepParameter > 0.0;
        }
        private void OnSetDirection(bool val)
        {
            if (val)
            {
                if (circle.SweepParameter < 0.0) (circle as ICurve).Reverse();
            }
            else
            {
                if (circle.SweepParameter > 0.0) (circle as ICurve).Reverse();
            }
        }
        private void ModifyEndAngleWithMouse(IPropertyEntry sender, bool startModifying)
        {
            GeneralAngleAction gaa = new GeneralAngleAction(endAngleProperty, circle.Plane);
            Frame.SetAction(gaa);
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
                    else if (sender == radiusProperty)
                    {
                        for (int i = 0; i < radiusHotSpots.Length; i++)
                        {
                            HotspotChangedEvent(radiusHotSpots[i], HotspotChangeMode.Selected);
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
                    else if (sender == radiusProperty)
                    {
                        for (int i = 0; i < radiusHotSpots.Length; i++)
                        {
                            HotspotChangedEvent(radiusHotSpots[i], HotspotChangeMode.Deselected);
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
                    (circle as ICurve).Reverse();
                    return true;
                case "MenuId.CurveSplit":
                    Frame.SetAction(new ConstrSplitCurve(circle));
                    return true;
                case "MenuId.Approximate":
                    if (Frame.ActiveAction is SelectObjectsAction && (Frame.GetIntSetting("Approximate.Mode", 0) == 0))
                    {
                        Curves.Approximate(Frame, circle);
                    }
                    return true;
                case "MenuId.Aequidist":
                    Frame.SetAction(new ConstructAequidist(circle));
                    return true;
            }
            return false;
        }

        virtual public bool OnUpdateCommand(string MenuId, CommandState CommandState)
        {
            switch (MenuId)
            {
                case "MenuId.Reverse":
                    CommandState.Enabled = (circle.IsArc);
                    return true;
                case "MenuId.CurveSplit":
                    return true;
                case "MenuId.Approximate":
                    CommandState.Enabled = (Frame.ActiveAction is SelectObjectsAction) && (Frame.GetIntSetting("Approximate.Mode", 0) == 0);
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
            return circle;
        }
        string IGeoObjectShowProperty.GetContextMenuId()
        {
            return "MenuId.Object.Ellipse";
        }
#endregion
    }

}
