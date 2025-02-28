using CADability.Actions;
using CADability.GeoObject;
using System.Collections.Generic;

namespace CADability.UserInterface
{
    /// <summary>
    /// Shows the properties of a line.
    /// </summary>

    public class ShowPropertyLine : PropertyEntryImpl, IDisplayHotSpots, ICommandHandler, IGeoObjectShowProperty
    {
        private Line line; // die dargestellte Linie
        private GeoPointProperty startPointProperty; // Anzeige Startpunkt, gleichzeitig HotSpot
        private GeoPointProperty endPointProperty; // Anzeige Endpunkt, gleichzeitig HotSpot
        private LengthProperty lengthProperty; // Anzeige Länge
        private LengthHotSpot lengthHotSpot; // Hotspot für Länge
        private GeoVectorProperty directionProperty; // Anzeige Richtung
        private GeoVectorHotSpot directionHotSpot; // Hotspot für Richtung
        private IPropertyEntry [] subEntries;
        private IPropertyEntry[] attributeProperties; // Anzeigen für die Attribute (Ebene, Farbe u.s.w)
        ~ShowPropertyLine()
        {
            // aus irgend einem Grund wird ShowPropertyLine ewig behalten. 
            // Der Grund ist unklar, muss aber dringend noch erforscht werden...
        }
        public ShowPropertyLine(Line line, IFrame frame): base(frame)
        {
            this.line = line;

            startPointProperty = new GeoPointProperty("Line.StartPoint", Frame, true);
            startPointProperty.GetGeoPointEvent += new CADability.UserInterface.GeoPointProperty.GetGeoPointDelegate(OnGetStartPoint);
            startPointProperty.SetGeoPointEvent += new CADability.UserInterface.GeoPointProperty.SetGeoPointDelegate(OnSetStartPoint);
            startPointProperty.ModifyWithMouseEvent += new ModifyWithMouseDelegate(ModifyStartPointWithMouse);
            startPointProperty.PropertyEntryChangedStateEvent += new PropertyEntryChangedStateDelegate(OnStateChanged);

            endPointProperty = new GeoPointProperty("Line.EndPoint", Frame, true);
            endPointProperty.GetGeoPointEvent += new CADability.UserInterface.GeoPointProperty.GetGeoPointDelegate(OnGetEndPoint);
            endPointProperty.SetGeoPointEvent += new CADability.UserInterface.GeoPointProperty.SetGeoPointDelegate(OnSetEndPoint);
            endPointProperty.ModifyWithMouseEvent += new ModifyWithMouseDelegate(ModifyEndPointWithMouse);
            endPointProperty.PropertyEntryChangedStateEvent += new PropertyEntryChangedStateDelegate(OnStateChanged);
            lengthProperty = new LengthProperty("Line.Length", Frame, true);
            lengthProperty.GetLengthEvent += new CADability.UserInterface.LengthProperty.GetLengthDelegate(OnGetLength);
            lengthProperty.SetLengthEvent += new CADability.UserInterface.LengthProperty.SetLengthDelegate(OnSetLength);
            lengthProperty.ModifyWithMouseEvent += new ModifyWithMouseDelegate(ModifyLengthWithMouse);
            lengthProperty.PropertyEntryChangedStateEvent += new PropertyEntryChangedStateDelegate(OnStateChanged);
            lengthHotSpot = new LengthHotSpot(lengthProperty);
            lengthHotSpot.Position = line.PointAt(2.0 / 3.0);

            directionProperty = new GeoVectorProperty("Line.Direction", Frame, false);
            directionProperty.IsNormedVector = true;
            directionProperty.IsAngle = true;
            directionProperty.GetGeoVectorEvent += new CADability.UserInterface.GeoVectorProperty.GetGeoVectorDelegate(OnGetDirection);
            directionProperty.SetGeoVectorEvent += new CADability.UserInterface.GeoVectorProperty.SetGeoVectorDelegate(OnSetDirection);
            directionProperty.ModifyWithMouseEvent += new ModifyWithMouseDelegate(ModifyDirectionWithMouse);
            directionProperty.PropertyEntryChangedStateEvent += new PropertyEntryChangedStateDelegate(OnStateChanged);
            directionHotSpot = new GeoVectorHotSpot(directionProperty);
            directionHotSpot.Position = line.PointAt(1.0 / 3.0);

            IPropertyEntry[] sp = line.GetAttributeProperties(Frame); // change signature of GetAttributeProperties
            attributeProperties = new IPropertyEntry[sp.Length];
            for (int i = 0; i < sp.Length; i++)
            {
                attributeProperties[i] = sp[i] as IPropertyEntry;
            }

            base.resourceIdInternal = "Line.Object";
        }
        private void OnGeoObjectDidChange(IGeoObject Sender, GeoObjectChange Change)
        {   // wird bei Änderungen von der Linie aufgerufen, Abgleich der Anzeigen
            startPointProperty.Refresh();
            endPointProperty.Refresh();
            lengthProperty.Refresh();
            lengthHotSpot.Position = line.PointAt(2.0 / 3.0); // sitzt bei 2/3 der Linie
            directionProperty.Refresh();
            directionHotSpot.Position = line.PointAt(1.0 / 3.0); // sitzt bei 1/3 der Linie
            if (HotspotChangedEvent != null)
            {
                HotspotChangedEvent(startPointProperty, HotspotChangeMode.Moved);
                HotspotChangedEvent(endPointProperty, HotspotChangeMode.Moved);
                HotspotChangedEvent(lengthHotSpot, HotspotChangeMode.Moved);
                HotspotChangedEvent(directionHotSpot, HotspotChangeMode.Moved);
            }
        }

#region IShowPropertyImpl Overrides
        public override void Added(IPropertyPage pp)
        {   // die events müssen in Added angemeldet und in Removed wieder abgemeldet werden,
            // sonst bleibt die ganze ShowProperty für immer an der Linie hängen
            line.DidChangeEvent += new ChangeDelegate(OnGeoObjectDidChange);
            line.UserData.UserDataAddedEvent += new UserData.UserDataAddedDelegate(OnUserDataAdded);
            line.UserData.UserDataRemovedEvent += new UserData.UserDataRemovedDelegate(OnUserDataAdded);
            base.Added(pp);
            OnGeoObjectDidChange(line, null); // einmal die Hotspots reaktivieren, falls eine
                                              // andere Zwischenänderung dran war
        }

        void OnUserDataAdded(string name, object value)
        {
            this.subEntries = null;
            IPropertyEntry[] sp = line.GetAttributeProperties(Frame); // change signature of GetAttributeProperties
            attributeProperties = new IPropertyEntry[sp.Length];
            for (int i = 0; i < sp.Length; i++)
            {
                attributeProperties[i] = sp[i] as IPropertyEntry;
            }
            propertyPage.Refresh(this);
        }
        public override void Removed(IPropertyPage pp)
        {
            line.DidChangeEvent -= new ChangeDelegate(OnGeoObjectDidChange);
            line.UserData.UserDataAddedEvent -= new UserData.UserDataAddedDelegate(OnUserDataAdded);
            line.UserData.UserDataRemovedEvent -= new UserData.UserDataRemovedDelegate(OnUserDataAdded);
            base.Removed(pp);
        }
        public override PropertyEntryType Flags => PropertyEntryType.ContextMenu | PropertyEntryType.Selectable | PropertyEntryType.GroupTitle | PropertyEntryType.HasSubEntries;
        public override MenuWithHandler[] ContextMenu
        {
            get
            {
                List<MenuWithHandler> items = new List<MenuWithHandler>(MenuResource.LoadMenuDefinition("MenuId.Object.Line", false, this));
                CreateContextMenueEvent?.Invoke(this, items);
                line.GetAdditionalContextMenue(this, Frame, items);
                return items.ToArray();
            }
        }
        public override IPropertyEntry[] SubItems
        {
            get
            {
                if (subEntries == null)
                {
                    // hier die Anzeigen auffrischen, wurde vor allem beim erstenmal noch nicht gemacht
                    startPointProperty.GeoPointChanged();
                    endPointProperty.GeoPointChanged();
                    lengthProperty.LengthChanged();
                    directionProperty.GeoVectorChanged();

                    IPropertyEntry[] mainProps = {
                                                     startPointProperty,
                                                     endPointProperty,
                                                     lengthProperty,
                                                     directionProperty
                                                 };
                    subEntries = Concat(mainProps, attributeProperties);
                }
                return subEntries;
            }
        }
        public override void Opened(bool IsOpen)
        {   // dient dazu, die Hotspots anzuzeigen bzw. zu verstecken wenn die SubProperties
            // aufgeklappt bzw. zugeklappt werden. Wenn also mehrere Objekte markiert sind
            // und diese Linie aufgeklappt wird.
            if (HotspotChangedEvent != null)
            {
                if (IsOpen)
                {
                    HotspotChangedEvent(startPointProperty, HotspotChangeMode.Visible);
                    HotspotChangedEvent(endPointProperty, HotspotChangeMode.Visible);
                    HotspotChangedEvent(lengthHotSpot, HotspotChangeMode.Visible);
                    HotspotChangedEvent(directionHotSpot, HotspotChangeMode.Visible);
                }
                else
                {
                    HotspotChangedEvent(startPointProperty, HotspotChangeMode.Invisible);
                    HotspotChangedEvent(endPointProperty, HotspotChangeMode.Invisible);
                    HotspotChangedEvent(lengthHotSpot, HotspotChangeMode.Invisible);
                    HotspotChangedEvent(directionHotSpot, HotspotChangeMode.Invisible);
                }
            }
            base.Opened(IsOpen);
        }
#endregion

#region IDisplayHotSpots Members
        public event CADability.HotspotChangedDelegate HotspotChangedEvent;
        public void ReloadProperties()
        {
            // TODO:  Add ShowPropertyLine.ReloadProperties implementation
            // ich glaube, hier muss man nix machen, da sich ja nie was ändert, oder?
            // TODO: aus dem IDisplayHotSpots interface entfernen und 
            if (propertyPage != null) propertyPage.Refresh(this);
        }

#endregion

#region Events der Properties

        // ModifyXxxWithMouse melden die IShowProperties, wenn im deren Contextmenue "mit der Maus"
        // ausgewählt wurde. Ebenso wird beim Ziehen eines Hotspots über IHotSpot.StartDrag
        // die entsprechende IShowProperty zu diesem Event veranlasst. Es werden GeneralXxxActions
        // gestartet, die über die IShowProperties kommunizieren.
        private void ModifyStartPointWithMouse(IPropertyEntry sender, bool StartModifying)
        {
            GeneralGeoPointAction gpa = new GeneralGeoPointAction(startPointProperty, line);
            Frame.SetAction(gpa);
        }

        private void ModifyEndPointWithMouse(IPropertyEntry sender, bool StartModifying)
        {
            GeneralGeoPointAction gpa = new GeneralGeoPointAction(endPointProperty, line);
            Frame.SetAction(gpa);
        }
        private void ModifyLengthWithMouse(IPropertyEntry sender, bool StartModifying)
        {
            GeneralLengthAction gla = new GeneralLengthAction(lengthProperty, line.StartPoint, line);
            Frame.SetAction(gla);
        }

        private void ModifyDirectionWithMouse(IPropertyEntry sender, bool StartModifying)
        {   // wird entweder durch Menueauswahl in der GeoVectorProperty oder durch ziehen am HotSpot
            // (läuft auch über die GeoVectorProperty) ausgelöst. Die GeneralGeoVectorAction arbeitet
            // direkt über die GeoVectorProperty, so dass keine weiteren Events nötig sind.
            GeneralGeoVectorAction gva = new GeneralGeoVectorAction(sender as GeoVectorProperty, line.StartPoint, line);
            Frame.SetAction(gva);
        }

        // OnGetXxx OnSetXxx dient der Kommunikation mit den IShowProperties und somit auch
        // mit den GeneralXxxActions, die ja den Weg über die IShowProperties nehmen.
        private GeoPoint OnGetStartPoint(GeoPointProperty sender)
        {
            return line.StartPoint;
        }

        private void OnSetStartPoint(GeoPointProperty sender, GeoPoint p)
        {
            line.StartPoint = p;
        }

        private GeoPoint OnGetEndPoint(GeoPointProperty sender)
        {
            return line.EndPoint;
        }

        private void OnSetEndPoint(GeoPointProperty sender, GeoPoint p)
        {
            line.EndPoint = p;
        }

        private double OnGetLength(LengthProperty sender)
        {
            return line.Length;
        }

        private void OnSetLength(LengthProperty sender, double l)
        {
            if (l != 0.0) line.Length = l;
        }
        private GeoVector OnGetDirection(GeoVectorProperty sender)
        {
            GeoVector res = line.StartDirection;
            // res.Norm(); warum? Wenn Start- und Endpunkt identisch sind gibts eine Exception, die nicht gefangen wird
            return res;
        }

        private void OnSetDirection(GeoVectorProperty sender, GeoVector v)
        {
            v.Norm();
            line.EndPoint = line.StartPoint + line.Length * v;
        }

        // OnStateChanged wird gleichermaßen für alle IShowProperties angemeldet. Deshalb muss
        // hier nach dem "sender" unterschieden werden. Hiermit wird geregelt welcher Hotspot
        // als selektierter Hotspot dargestellt werden soll. Bei MultiGeoPointProperty muss
        // man hier einen etwas anderen Weg gehen (siehe z.B. ShowPropertxBSpline)
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
                    else if (sender == lengthProperty)
                    {
                        HotspotChangedEvent(lengthHotSpot, HotspotChangeMode.Selected);
                    }
                    else if (sender == directionProperty)
                    {
                        HotspotChangedEvent(directionHotSpot, HotspotChangeMode.Selected);
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
                    else if (sender == lengthProperty)
                    {
                        HotspotChangedEvent(lengthHotSpot, HotspotChangeMode.Deselected);
                    }
                    else if (sender == directionProperty)
                    {
                        HotspotChangedEvent(directionHotSpot, HotspotChangeMode.Deselected);
                    }
                }
            }
        }
#endregion

#region ICommandHandler Members
        bool ICommandHandler.OnCommand(string MenuId)
        {
            switch (MenuId)
            {
                case "MenuId.Reverse":
                    (line as ICurve).Reverse();
                    return true;
                case "MenuId.CurveSplit":
                    Frame.SetAction(new ConstrSplitCurve(line));
                    return true;
            }
            return false;
        }
        bool ICommandHandler.OnUpdateCommand(string MenuId, CommandState CommandState)
        {
            switch (MenuId)
            {
                case "MenuId.Reverse":
                    CommandState.Enabled = true; // naja isses ja immer
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
            return line;
        }
        string IGeoObjectShowProperty.GetContextMenuId()
        {
            return "MenuId.Object.Line";
        }
#endregion
    }
}


