using CADability.Actions;
using CADability.GeoObject;
using System;
using System.Collections.Generic;
using MouseEventArgs = CADability.Substitutes.MouseEventArgs;
#if WEBASSEMBLY
using CADability.WebDrawing;
using Point = CADability.WebDrawing.Point;
#else
using System.Drawing;
using Point = System.Drawing.Point;
#endif

namespace CADability.UserInterface
{
    /// <summary>
    /// 
    /// </summary>
    internal class ShowPropertyDimension : PropertyEntryImpl, IDisplayHotSpots, IIndexedGeoPoint, ICommandHandler, IGeoObjectShowProperty
    {
        private Dimension dimension; // diese wird dargestellt
        private MultiGeoPointProperty points; // die Punkte der Bemaßung
        private GeoPointProperty dimLineRef; // der DimLineRef
        private GeoVectorProperty direction; // dimLineDirection
        private DoubleHotSpot[] textPosHotSpot; // die Textpositionen als Hotspot
        private DoubleProperty[] textPos;
        private StringProperty[] dimText; // der Text der Bemaßung
        private StringProperty[] tolPlusText; // der hochgestellte Text
        private StringProperty[] tolMinusText; // der tiefgestellte Text
        private StringProperty[] prefix; // der Prefix
        private StringProperty[] postfix; // der Postfix
        private StringProperty[] postfixAlt; // der Postfix alternativ
        private DoubleProperty radiusProperty; // für Radius und Durchmesserbemaßung
        private GeoPointProperty centerProperty; // für Radius-, Durchmesser-, Winkelbemaßung
        private GeoVectorProperty startAngle; // für Winkelbemaßung
        private GeoVectorProperty endAngle; // für Winkelbemaßung
        private GeoVectorHotSpot startAngleHotSpot;
        private GeoVectorHotSpot endAngleHotSpot;
        private IPropertyEntry[] subProperties;
        private bool ignoreChange;

        public ShowPropertyDimension(Dimension dimension, IFrame frame): base(frame)
        {
            dimension.SortPoints();
            this.dimension = dimension;
            switch (dimension.DimType)
            {
                case Dimension.EDimType.DimPoints: base.resourceIdInternal = "Dimension.DimPoints"; break;
                case Dimension.EDimType.DimCoord: base.resourceIdInternal = "Dimension.DimCoord"; break;
                case Dimension.EDimType.DimAngle: base.resourceIdInternal = "Dimension.DimAngle"; break;
                case Dimension.EDimType.DimRadius: base.resourceIdInternal = "Dimension.DimRadius"; break;
                case Dimension.EDimType.DimDiameter: base.resourceIdInternal = "Dimension.DimDiameter"; break;
                case Dimension.EDimType.DimLocation: base.resourceIdInternal = "Dimension.DimLocation"; break;
            }

            Init();

            SelectObjectsAction selAct = Frame.ActiveAction as SelectObjectsAction;
            if (selAct != null)
            {
                selAct.ClickOnSelectedObjectEvent += new CADability.Actions.SelectObjectsAction.ClickOnSelectedObjectDelegate(OnClickOnSelectedObject);
            }
            ignoreChange = false;
        }

        private void Init()
        {
            // Initialisiert den gesammten Inhalt des TreeViews
            // danach sollte meist propertyPage.Refresh(this); aufgerufen werden
            subProperties = null;

            if (dimension.DimType == Dimension.EDimType.DimPoints || dimension.DimType == Dimension.EDimType.DimCoord)
            {
                points = new MultiGeoPointProperty(this, "Dimension.Points", this.Frame);
                points.ModifyWithMouseEvent += new CADability.UserInterface.MultiGeoPointProperty.ModifyWithMouseIndexDelegate(OnModifyPointWithMouse);
                points.PropertyEntryChangedStateEvent += new PropertyEntryChangedStateDelegate(OnPointsStateChanged);
                points.GeoPointSelectionChangedEvent += new CADability.UserInterface.GeoPointProperty.SelectionChangedDelegate(OnPointsSelectionChanged);
                direction = new GeoVectorProperty("Dimension.Direction", Frame, true);
                direction.GetGeoVectorEvent += new CADability.UserInterface.GeoVectorProperty.GetGeoVectorDelegate(OnGetDirection);
                direction.SetGeoVectorEvent += new CADability.UserInterface.GeoVectorProperty.SetGeoVectorDelegate(OnSetDirection);
            }
            if (dimension.DimType == Dimension.EDimType.DimRadius)
            {
                radiusProperty = new DoubleProperty("Dimension.Radius", Frame);
                radiusProperty.SetDoubleEvent += new CADability.UserInterface.DoubleProperty.SetDoubleDelegate(OnSetRadius);
                radiusProperty.GetDoubleEvent += new CADability.UserInterface.DoubleProperty.GetDoubleDelegate(OnGetRadius);
                radiusProperty.ModifyWithMouseEvent += new ModifyWithMouseDelegate(ModifyRadiusWithMouse);
                radiusProperty.DoubleChanged();
            }
            if (dimension.DimType == Dimension.EDimType.DimDiameter)
            {
                radiusProperty = new DoubleProperty("Dimension.Diameter", Frame);
                radiusProperty.SetDoubleEvent += new CADability.UserInterface.DoubleProperty.SetDoubleDelegate(OnSetRadius);
                radiusProperty.GetDoubleEvent += new CADability.UserInterface.DoubleProperty.GetDoubleDelegate(OnGetRadius);
                radiusProperty.ModifyWithMouseEvent += new ModifyWithMouseDelegate(ModifyRadiusWithMouse);
                radiusProperty.DoubleChanged();
            }
            if (dimension.DimType == Dimension.EDimType.DimDiameter ||
                dimension.DimType == Dimension.EDimType.DimRadius ||
                dimension.DimType == Dimension.EDimType.DimLocation ||
                dimension.DimType == Dimension.EDimType.DimAngle)
            {   // haben alle einen Mittelpunkt als Punkt 0
                centerProperty = new GeoPointProperty("Dimension.Center", Frame, true);
                centerProperty.GetGeoPointEvent += new CADability.UserInterface.GeoPointProperty.GetGeoPointDelegate(OnGetCenter);
                centerProperty.SetGeoPointEvent += new CADability.UserInterface.GeoPointProperty.SetGeoPointDelegate(OnSetCenter);
                centerProperty.ModifyWithMouseEvent += new ModifyWithMouseDelegate(ModifyCenterWithMouse);
            }
            if (dimension.DimType == Dimension.EDimType.DimAngle)
            {   // start- und Endwinkel
                startAngle = new GeoVectorProperty("Dimension.Startangle", Frame, true);
                startAngle.GetGeoVectorEvent += new CADability.UserInterface.GeoVectorProperty.GetGeoVectorDelegate(OnGetStartAngle);
                startAngle.SetGeoVectorEvent += new CADability.UserInterface.GeoVectorProperty.SetGeoVectorDelegate(OnSetStartAngle);
                startAngle.ModifyWithMouseEvent += new ModifyWithMouseDelegate(ModifyStartAngleWithMouse);
                startAngle.SetHotspotPosition(dimension.GetPoint(1));

                endAngle = new GeoVectorProperty("Dimension.Endangle", Frame, true);
                endAngle.GetGeoVectorEvent += new CADability.UserInterface.GeoVectorProperty.GetGeoVectorDelegate(OnGetEndAngle);
                endAngle.SetGeoVectorEvent += new CADability.UserInterface.GeoVectorProperty.SetGeoVectorDelegate(OnSetEndAngle);
                endAngle.ModifyWithMouseEvent += new ModifyWithMouseDelegate(ModifyEndAngleWithMouse);
                endAngle.SetHotspotPosition(dimension.GetPoint(2));
                startAngleHotSpot = new GeoVectorHotSpot(startAngle);
                endAngleHotSpot = new GeoVectorHotSpot(endAngle);
            }
            dimLineRef = new GeoPointProperty("Dimension.DimLineRef", Frame, true);
            dimLineRef.GetGeoPointEvent += new CADability.UserInterface.GeoPointProperty.GetGeoPointDelegate(OnGetDimLineRef);
            dimLineRef.SetGeoPointEvent += new CADability.UserInterface.GeoPointProperty.SetGeoPointDelegate(OnSetDimLineRef);
            dimLineRef.ModifyWithMouseEvent += new ModifyWithMouseDelegate(ModifyDimLineRef);
            dimLineRef.PropertyEntryChangedStateEvent += new PropertyEntryChangedStateDelegate(OnStateChanged);

            // die String Eingabefelder:
            int numprop = 1;
            if (dimension.DimType == Dimension.EDimType.DimPoints || dimension.DimType == Dimension.EDimType.DimCoord)
            {
                numprop = dimension.PointCount - 1;
            }
            textPosHotSpot = new DoubleHotSpot[numprop];
            textPos = new DoubleProperty[numprop];
            dimText = new StringProperty[numprop];
            tolPlusText = new StringProperty[numprop];
            tolMinusText = new StringProperty[numprop];
            prefix = new StringProperty[numprop];
            postfix = new StringProperty[numprop];
            postfixAlt = new StringProperty[numprop];
            for (int i = 0; i < numprop; ++i)
            {
                textPos[i] = new DoubleProperty("Dimension.TextPos", Frame);
                textPos[i].UserData.Add("Index", i);
                textPos[i].GetDoubleEvent += new CADability.UserInterface.DoubleProperty.GetDoubleDelegate(OnGetTextPos);
                textPos[i].SetDoubleEvent += new CADability.UserInterface.DoubleProperty.SetDoubleDelegate(OnSetTextPos);
                textPos[i].DoubleChanged();
                textPos[i].ModifyWithMouseEvent += new ModifyWithMouseDelegate(ModifyTextPosWithMouse);
                textPos[i].PropertyEntryChangedStateEvent += new PropertyEntryChangedStateDelegate(OnStateChanged);

                textPosHotSpot[i] = new DoubleHotSpot(textPos[i]);
                textPosHotSpot[i].Position = dimension.GetTextPosCoordinate(i, Frame.ActiveView.Projection);

                dimText[i] = new StringProperty(dimension.GetDimText(i), "Dimension.DimText");
                dimText[i].UserData.Add("Index", i);
                dimText[i].SetStringEvent += new CADability.UserInterface.StringProperty.SetStringDelegate(OnSetDimText);
                dimText[i].GetStringEvent += new CADability.UserInterface.StringProperty.GetStringDelegate(OnGetDimText);

                tolPlusText[i] = new StringProperty(dimension.GetTolPlusText(i), "Dimension.TolPlusText");
                tolPlusText[i].UserData.Add("Index", i);
                tolPlusText[i].SetStringEvent += new CADability.UserInterface.StringProperty.SetStringDelegate(OnSetTolPlusText);
                tolPlusText[i].GetStringEvent += new CADability.UserInterface.StringProperty.GetStringDelegate(OnGetTolPlusText);

                tolMinusText[i] = new StringProperty(dimension.GetTolMinusText(i), "Dimension.TolMinusText");
                tolMinusText[i].UserData.Add("Index", i);
                tolMinusText[i].SetStringEvent += new CADability.UserInterface.StringProperty.SetStringDelegate(OnSetTolMinusText);
                tolMinusText[i].GetStringEvent += new CADability.UserInterface.StringProperty.GetStringDelegate(OnGetTolMinusText);

                prefix[i] = new StringProperty(dimension.GetPrefix(i), "Dimension.Prefix");
                prefix[i].UserData.Add("Index", i);
                prefix[i].SetStringEvent += new CADability.UserInterface.StringProperty.SetStringDelegate(OnSetPrefix);
                prefix[i].GetStringEvent += new CADability.UserInterface.StringProperty.GetStringDelegate(OnGetPrefix);

                postfix[i] = new StringProperty(dimension.GetPostfix(i), "Dimension.Postfix");
                postfix[i].UserData.Add("Index", i);
                postfix[i].SetStringEvent += new CADability.UserInterface.StringProperty.SetStringDelegate(OnSetPostfix);
                postfix[i].GetStringEvent += new CADability.UserInterface.StringProperty.GetStringDelegate(OnGetPostfix);

                postfixAlt[i] = new StringProperty(dimension.GetPostfixAlt(i), "Dimension.PostfixAlt");
                postfixAlt[i].UserData.Add("Index", i);
                postfixAlt[i].SetStringEvent += new CADability.UserInterface.StringProperty.SetStringDelegate(OnSetPostfixAlt);
                postfixAlt[i].GetStringEvent += new CADability.UserInterface.StringProperty.GetStringDelegate(OnGetPostfixAlt);
            }
        }
        /// <summary>
        /// Overrides <see cref="PropertyEntryImpl.Added"/>
        /// </summary>
        /// <param name="propertyPage"></param>
        public override void Added(IPropertyPage propertyPage)
        {
            dimension.DidChangeEvent += new ChangeDelegate(DimensionDidChange);
            dimension.UserData.UserDataAddedEvent += new UserData.UserDataAddedDelegate(OnUserDataAdded);
            dimension.UserData.UserDataRemovedEvent += new UserData.UserDataRemovedDelegate(OnUserDataAdded);
            base.Added(propertyPage);
        }
        public override void Removed(IPropertyPage propertyPage)
        {
            dimension.DidChangeEvent -= new ChangeDelegate(DimensionDidChange);
            dimension.UserData.UserDataAddedEvent -= new UserData.UserDataAddedDelegate(OnUserDataAdded);
            dimension.UserData.UserDataRemovedEvent -= new UserData.UserDataRemovedDelegate(OnUserDataAdded);
            base.Removed(propertyPage);
        }
        void OnUserDataAdded(string name, object value)
        {
            propertyPage.Refresh(this);
        }
        public override PropertyEntryType Flags => PropertyEntryType.GroupTitle | PropertyEntryType.HasSubEntries | PropertyEntryType.ContextMenu | PropertyEntryType.Selectable;
        public override MenuWithHandler[] ContextMenu
        {
            get
            {
                List<MenuWithHandler> items = new List<MenuWithHandler>(MenuResource.LoadMenuDefinition("MenuId.Object.Dimension", false, this));
                CreateContextMenueEvent?.Invoke(this, items);
                dimension.GetAdditionalContextMenue(this, Frame, items);
                return items.ToArray();
            }
        }
        public override IPropertyEntry[] SubItems
        {
            get
            {
                int numpoints = 1;
                int numprop = 0;
                if (dimension.DimType == Dimension.EDimType.DimPoints || dimension.DimType == Dimension.EDimType.DimCoord)
                {
                    numpoints = dimension.PointCount - 1;
                    numprop = 3;
                }
                else if (dimension.DimType == Dimension.EDimType.DimRadius || dimension.DimType == Dimension.EDimType.DimDiameter)
                {
                    numprop = 3;
                }
                else if (dimension.DimType == Dimension.EDimType.DimAngle)
                {
                    numprop = 4;
                }
                else if (dimension.DimType == Dimension.EDimType.DimLocation)
                {
                    numprop = 2; //???
                }
                if (subProperties == null)
                {
                    subProperties = new IPropertyEntry[numprop + 7 * numpoints];
                }
                if (dimension.DimType == Dimension.EDimType.DimPoints || dimension.DimType == Dimension.EDimType.DimCoord)
                {
                    subProperties[0] = points;
                    subProperties[1] = dimLineRef;
                    subProperties[2] = direction;
                }
                else if (dimension.DimType == Dimension.EDimType.DimRadius || dimension.DimType == Dimension.EDimType.DimDiameter)
                {
                    subProperties[0] = centerProperty;
                    subProperties[1] = radiusProperty;
                    subProperties[2] = dimLineRef;
                }
                else if (dimension.DimType == Dimension.EDimType.DimAngle)
                {
                    subProperties[0] = centerProperty;
                    subProperties[1] = startAngle;
                    subProperties[2] = endAngle;
                    subProperties[3] = dimLineRef;
                }
                else if (dimension.DimType == Dimension.EDimType.DimLocation)
                {
                    subProperties[0] = centerProperty;
                    subProperties[1] = dimLineRef;
                }
                for (int i = 0; i < numpoints; ++i)
                {
                    subProperties[numprop + 7 * i + 0] = textPos[i];
                    subProperties[numprop + 7 * i + 1] = dimText[i];
                    subProperties[numprop + 7 * i + 2] = tolPlusText[i];
                    subProperties[numprop + 7 * i + 3] = tolMinusText[i];
                    subProperties[numprop + 7 * i + 4] = prefix[i];
                    subProperties[numprop + 7 * i + 5] = postfix[i];
                    subProperties[numprop + 7 * i + 6] = postfixAlt[i];
                }
                return PropertyEntryImpl.Concat(subProperties, dimension.GetAttributeProperties(Frame));
            }
        }
        public override void Opened(bool IsOpen)
        {
            if (HotspotChangedEvent != null)
            {
                if (IsOpen)
                {
                    HotspotChangedEvent(dimLineRef, HotspotChangeMode.Visible);
                    for (int i = 0; i < textPosHotSpot.Length; ++i)
                    {
                        HotspotChangedEvent(textPosHotSpot[i], HotspotChangeMode.Visible);
                    }
                    if (startAngle != null)
                    {
                        HotspotChangedEvent(startAngleHotSpot, HotspotChangeMode.Visible);
                        HotspotChangedEvent(endAngleHotSpot, HotspotChangeMode.Visible);
                    }
                    if (centerProperty != null)
                    {
                        HotspotChangedEvent(centerProperty, HotspotChangeMode.Visible);
                    }
                }
                else
                {
                    HotspotChangedEvent(dimLineRef, HotspotChangeMode.Invisible);
                    for (int i = 0; i < textPosHotSpot.Length; ++i)
                    {
                        HotspotChangedEvent(textPosHotSpot[i], HotspotChangeMode.Invisible);
                    }
                    if (startAngle != null)
                    {
                        HotspotChangedEvent(startAngleHotSpot, HotspotChangeMode.Invisible);
                        HotspotChangedEvent(endAngleHotSpot, HotspotChangeMode.Invisible);
                    }
                }
            }
            base.Opened(IsOpen);
        }


        #region IDisplayHotSpots Members

        public event CADability.HotspotChangedDelegate HotspotChangedEvent;

        public void ReloadProperties()
        {
            // TODO:  Add ShowPropertyDimension.ReloadProperties implementation
        }

        #endregion

        #region IIndexedGeoPoint Members

        public void SetGeoPoint(int Index, GeoPoint ThePoint)
        {
            dimension.SetPoint(Index, ThePoint);
        }

        public GeoPoint GetGeoPoint(int Index)
        {
            return dimension.GetPoint(Index);
        }

        public void InsertGeoPoint(int Index, GeoPoint ThePoint)
        {
            if (Index == -1) dimension.AddPoint(ThePoint);
            else throw new NotImplementedException("Dimension: InsertGeoPoint");
            Opened(false);
            Init();
            propertyPage.Refresh(this);
            propertyPage.OpenSubEntries(points, true);
        }

        public void RemoveGeoPoint(int Index)
        {
            ignoreChange = true;
            dimension.RemovePoint(Index);
            ignoreChange = false;
            subProperties = null;
            propertyPage.Refresh(this);
            propertyPage.OpenSubEntries(points, true);
            SelectObjectsAction selAct = Frame.ActiveAction as SelectObjectsAction;
            if (selAct != null)
            {
                selAct.RemoveSelectedObject(dimension);
                selAct.AddSelectedObject(dimension);
            }
        }

        public int GetGeoPointCount()
        {
            return dimension.PointCount;
        }

        bool IIndexedGeoPoint.MayInsert(int Index)
        {
            // return Index==-1;
            return false;
            // Punkte einfügen geht einfach mit neuer Bemaßung machen.
            // hierbei würde nur der letzte Punkt verdoppelt und das sieht erstmal blöd aus
        }
        bool IIndexedGeoPoint.MayDelete(int Index)
        {
            return dimension.PointCount > 2;
        }
        #endregion

        private GeoPoint OnGetDimLineRef(GeoPointProperty sender)
        {
            return dimension.DimLineRef;
        }

        private void OnSetDimLineRef(GeoPointProperty sender, GeoPoint p)
        {
            dimension.DimLineRef = p;
        }

        private GeoVector OnGetDirection(GeoVectorProperty sender)
        {
            return dimension.DimLineDirection;
        }

        private void OnSetDirection(GeoVectorProperty sender, GeoVector v)
        {
            dimension.DimLineDirection = v;
        }

        private bool OnModifyPointWithMouse(IPropertyEntry sender, int index)
        {
            GeneralGeoPointAction gpa = new GeneralGeoPointAction(dimension.GetPoint(index), dimension);
            gpa.UserData.Add("Mode", "Point");
            gpa.UserData.Add("Index", index);
            gpa.GeoPointProperty = sender as GeoPointProperty;
            gpa.SetGeoPointEvent += new CADability.Actions.GeneralGeoPointAction.SetGeoPointDelegate(OnSetPoint);
            gpa.ActionDoneEvent += new CADability.Actions.GeneralGeoPointAction.ActionDoneDelegate(OnSetPointDone);
            Frame.SetAction(gpa);
            return false;
        }

        private void OnSetPoint(GeneralGeoPointAction sender, GeoPoint NewValue)
        {
            int Index = (int)sender.UserData.GetData("Index");
            dimension.SetPoint(Index, NewValue);
        }

        private void OnSetPointDone(GeneralGeoPointAction sender)
        {
            dimension.SortPoints();
            subProperties = null;
            propertyPage.Refresh(this);
            propertyPage.OpenSubEntries(points, true);
        }

        private void OnSetDimText(StringProperty sender, string newValue)
        {
            int ind = (int)sender.UserData.GetData("Index");
            if (dimension.GetDimText(ind) != newValue)
            {
                if (newValue.Length == 0)
                {
                    dimension.SetDimText(ind, null);
                    sender.SetString(dimension.GetDimText(ind));
                }
                else dimension.SetDimText(ind, newValue);
            }
        }
        private string OnGetDimText(StringProperty sender)
        {
            int ind = (int)sender.UserData.GetData("Index");
            return dimension.GetDimText(ind);
        }

        private void OnSetTolPlusText(StringProperty sender, string newValue)
        {
            int ind = (int)sender.UserData.GetData("Index");
            if (dimension.GetTolPlusText(ind) != newValue)
            {
                if (newValue.Length == 0) dimension.SetTolPlusText(ind, null);
                else dimension.SetTolPlusText(ind, newValue);
            }
        }
        private string OnGetTolPlusText(StringProperty sender)
        {
            int ind = (int)sender.UserData.GetData("Index");
            return dimension.GetTolPlusText(ind);
        }

        private void OnSetTolMinusText(StringProperty sender, string newValue)
        {
            int ind = (int)sender.UserData.GetData("Index");
            if (dimension.GetTolMinusText(ind) != newValue)
            {
                if (newValue.Length == 0) dimension.SetTolMinusText(ind, null);
                else dimension.SetTolMinusText(ind, newValue);
            }
        }
        private string OnGetTolMinusText(StringProperty sender)
        {
            int ind = (int)sender.UserData.GetData("Index");
            return dimension.GetTolMinusText(ind);
        }

        private void OnSetPrefix(StringProperty sender, string newValue)
        {
            int ind = (int)sender.UserData.GetData("Index");
            if (dimension.GetPrefix(ind) != newValue)
            {
                if (newValue.Length == 0) dimension.SetPrefix(ind, null);
                else dimension.SetPrefix(ind, newValue);
            }
        }
        private string OnGetPrefix(StringProperty sender)
        {
            int ind = (int)sender.UserData.GetData("Index");
            return dimension.GetPrefix(ind);
        }

        private void OnSetPostfix(StringProperty sender, string newValue)
        {
            int ind = (int)sender.UserData.GetData("Index");
            if (dimension.GetPostfix(ind) != newValue)
            {
                if (newValue.Length == 0) dimension.SetPostfix(ind, null);
                else dimension.SetPostfix(ind, newValue);
            }
        }
        private string OnGetPostfix(StringProperty sender)
        {
            int ind = (int)sender.UserData.GetData("Index");
            return dimension.GetPostfix(ind);
        }

        private void OnSetPostfixAlt(StringProperty sender, string newValue)
        {
            int ind = (int)sender.UserData.GetData("Index");
            if (dimension.GetPostfixAlt(ind) != newValue)
            {
                if (newValue.Length == 0) dimension.SetPostfixAlt(ind, null);
                else dimension.SetPostfixAlt(ind, newValue);
            }
        }
        private string OnGetPostfixAlt(StringProperty sender)
        {
            int ind = (int)sender.UserData.GetData("Index");
            return dimension.GetPostfixAlt(ind);
        }

        private double OnGetTextPos(DoubleProperty sender)
        {
            int ind = (int)sender.UserData.GetData("Index");
            return dimension.GetTextPos(ind);
        }

        private void OnSetTextPos(DoubleProperty sender, double l)
        {
            int ind = (int)sender.UserData.GetData("Index");
            dimension.SetTextPos(ind, l);
        }

        private void ModifyDimLineRef(IPropertyEntry sender, bool StartModifying)
        {
            GeneralGeoPointAction gpa = new GeneralGeoPointAction(dimension.DimLineRef, dimension);
            gpa.GeoPointProperty = sender as GeoPointProperty;
            Frame.SetAction(gpa);
        }

        private void OnPointsStateChanged(IPropertyEntry sender, StateChangedArgs args)
        {
            if (HotspotChangedEvent != null)
            {
                if (args.EventState == StateChangedArgs.State.OpenSubEntries)
                {
                    for (int i = 0; i < points.SubEntriesCount; ++i)
                    {
                        IHotSpot hsp = points.SubEntries[i] as IHotSpot;
                        if (hsp != null) HotspotChangedEvent(hsp, HotspotChangeMode.Visible);
                    }
                }
                else if (args.EventState == StateChangedArgs.State.CollapseSubEntries)
                {
                    for (int i = 0; i < points.SubEntriesCount; ++i)
                    {
                        IHotSpot hsp = points.SubEntries[i] as IHotSpot;
                        if (hsp != null) HotspotChangedEvent(hsp, HotspotChangeMode.Invisible);
                    }
                }
            }
        }

        private void ModifyTextPosWithMouse(IPropertyEntry sender, bool StartModifying)
        {
            int ind = (int)(sender as DoubleProperty).UserData.GetData("Index");
            GeoPoint p = dimension.GetTextPosCoordinate(ind, Frame.ActiveView.Projection);
            GeneralGeoPointAction gpa = new GeneralGeoPointAction(p, dimension);
            gpa.UserData.Add("Index", ind);
            gpa.SetGeoPointEvent += new GeneralGeoPointAction.SetGeoPointDelegate(OnSetTextPos);
            Frame.SetAction(gpa);
        }

        private void OnSetTextPos(GeneralGeoPointAction sender, GeoPoint p)
        {
            int ind = (int)sender.UserData.GetData("Index");
            dimension.SetTextPosCoordinate(ind, Frame.ActiveView.Projection, p);
        }

        private void DimensionDidChange(IGeoObject Sender, GeoObjectChange Change)
        {
            if (ignoreChange) return; // es wird. z.B. gerade ein Punkt entfernt 
                                      // und die Properties müssen erst wieder neu Refresht werden

            if (HotspotChangedEvent != null)
            {   // mitführen der TextHotSpots. Die Hotspots der Punkte werden automatisch
                // mitgeführt, ebenso der für die DimLineRef
                for (int i = 0; i < textPosHotSpot.Length; ++i)
                {
                    textPos[i].DoubleChanged();
                    textPosHotSpot[i].Position = dimension.GetTextPosCoordinate(i, Frame.ActiveView.Projection);
                    HotspotChangedEvent(textPosHotSpot[i], HotspotChangeMode.Moved);
                }
                if (startAngle != null)
                {
                    startAngle.GeoVectorChanged();
                    endAngle.GeoVectorChanged();
                    startAngle.SetHotspotPosition(dimension.GetPoint(1));
                    endAngle.SetHotspotPosition(dimension.GetPoint(2));
                    HotspotChangedEvent(startAngleHotSpot, HotspotChangeMode.Moved);
                    HotspotChangedEvent(endAngleHotSpot, HotspotChangeMode.Moved);
                }
                if (centerProperty != null)
                {
                    centerProperty.GeoPointChanged(); // Refresh
                    HotspotChangedEvent(centerProperty, HotspotChangeMode.Moved);
                }
                if (radiusProperty != null)
                {
                    radiusProperty.DoubleChanged();
                }
            }
            int numprop = 1;
            if (dimension.DimType == Dimension.EDimType.DimPoints || dimension.DimType == Dimension.EDimType.DimCoord)
            {
                numprop = dimension.PointCount - 1;
            }
            if (numprop != dimText.Length)
            {
                Opened(false); // alle hotspots abmelden
                Init();
                if (propertyPage != null)
                {
                    propertyPage.Refresh(this);
                    propertyPage.OpenSubEntries(points, true);
                }
            }
        }

        private void OnSetRadius(DoubleProperty sender, double l)
        {
            if (dimension.DimType == Dimension.EDimType.DimDiameter)
            {
                dimension.Radius = l / 2.0;
            }
            else
            {
                dimension.Radius = l;
            }
        }

        private double OnGetRadius(DoubleProperty sender)
        {
            if (dimension.DimType == Dimension.EDimType.DimDiameter)
            {
                return dimension.Radius * 2.0;
            }
            else
            {
                return dimension.Radius;
            }
        }

        private void OnSetRadiusPoint(GeneralGeoPointAction sender, GeoPoint NewValue)
        {
            dimension.Radius = Geometry.Dist(dimension.GetPoint(0), NewValue);
        }

        private void ModifyRadiusWithMouse(IPropertyEntry sender, bool StartModifying)
        {
            GeneralGeoPointAction gpa = new GeneralGeoPointAction(dimension.GetPoint(0), dimension);
            gpa.GeoPointProperty = sender as GeoPointProperty;
            gpa.SetGeoPointEvent += new CADability.Actions.GeneralGeoPointAction.SetGeoPointDelegate(OnSetRadiusPoint);
            Frame.SetAction(gpa);
        }

        private GeoPoint OnGetCenter(GeoPointProperty sender)
        {
            return dimension.GetPoint(0);
        }
        private void OnSetCenter(GeoPointProperty sender, GeoPoint p)
        {
            dimension.SetPoint(0, p);
        }

        private void OnSetCenterPoint(GeneralGeoPointAction sender, GeoPoint NewValue)
        {
            dimension.SetPoint(0, NewValue);
        }

        private void ModifyCenterWithMouse(IPropertyEntry sender, bool StartModifying)
        {
            GeneralGeoPointAction gpa = new GeneralGeoPointAction(dimension.GetPoint(0), dimension);
            gpa.GeoPointProperty = sender as GeoPointProperty;
            gpa.SetGeoPointEvent += new CADability.Actions.GeneralGeoPointAction.SetGeoPointDelegate(OnSetCenterPoint);
            Frame.SetAction(gpa);
        }

        private GeoVector OnGetStartAngle(GeoVectorProperty sender)
        {
            GeoVector dir = dimension.GetPoint(1) - dimension.GetPoint(0);
            dir.Norm();
            return dir;
        }

        private void OnSetStartAngle(GeoVectorProperty sender, GeoVector v)
        {
            double r = Geometry.Dist(dimension.GetPoint(1), dimension.GetPoint(0));
            GeoVector dir = dimension.GetPoint(1) - dimension.GetPoint(0);
            dir.Norm();
            dimension.SetPoint(1, dimension.GetPoint(0) + r * dir);
        }

        private void ModifyStartAngleWithMouse(IPropertyEntry sender, bool StartModifying)
        {
            GeneralGeoPointAction gpa = new GeneralGeoPointAction(dimension.GetPoint(1), dimension);
            gpa.GeoPointProperty = sender as GeoPointProperty;
            gpa.SetGeoPointEvent += new CADability.Actions.GeneralGeoPointAction.SetGeoPointDelegate(OnSetStartAnglePoint);
            Frame.SetAction(gpa);
        }

        private void OnSetStartAnglePoint(GeneralGeoPointAction sender, GeoPoint NewValue)
        {
            dimension.SetPoint(1, NewValue);
        }

        private GeoVector OnGetEndAngle(GeoVectorProperty sender)
        {
            GeoVector dir = dimension.GetPoint(2) - dimension.GetPoint(0);
            dir.Norm();
            return dir;
        }

        private void OnSetEndAngle(GeoVectorProperty sender, GeoVector v)
        {
            double r = Geometry.Dist(dimension.GetPoint(2), dimension.GetPoint(0));
            GeoVector dir = dimension.GetPoint(2) - dimension.GetPoint(0);
            dir.Norm();
            dimension.SetPoint(2, dimension.GetPoint(0) + r * dir);
        }

        private void ModifyEndAngleWithMouse(IPropertyEntry sender, bool StartModifying)
        {
            GeneralGeoPointAction gpa = new GeneralGeoPointAction(dimension.GetPoint(2), dimension);
            gpa.GeoPointProperty = sender as GeoPointProperty;
            gpa.SetGeoPointEvent += new CADability.Actions.GeneralGeoPointAction.SetGeoPointDelegate(OnSetEndAnglePoint);
            Frame.SetAction(gpa);
        }

        private void OnSetEndAnglePoint(GeneralGeoPointAction sender, GeoPoint NewValue)
        {
            dimension.SetPoint(2, NewValue);
        }

        private void OnClickOnSelectedObject(IGeoObject selected, IView vw, MouseEventArgs e, ref bool handled)
        {
            if (!handled && propertyPage != null)
            {
                GeoPoint dp = vw.Projection.DrawingPlanePoint(new Point(e.X, e.Y));
                int index;
                Dimension.HitPosition hp = dimension.GetHitPosition(vw.Projection, vw.Projection.ProjectUnscaled(dp), out index);
                if ((hp & Dimension.HitPosition.Text) != 0)
                {
                    propertyPage.MakeVisible(dimText[index]);
                    propertyPage.SelectEntry(dimText[index]);
                }
                else if ((hp & Dimension.HitPosition.LowerText) != 0)
                {
                    propertyPage.MakeVisible(tolMinusText[index]);
                    propertyPage.SelectEntry(tolMinusText[index]);
                }
                else if ((hp & Dimension.HitPosition.PostFix) != 0)
                {
                    propertyPage.MakeVisible(postfix[index]);
                    propertyPage.SelectEntry(postfix[index]);
                }
                else if ((hp & Dimension.HitPosition.PostFixAlt) != 0)
                {
                    propertyPage.MakeVisible(postfixAlt[index]);
                    propertyPage.SelectEntry(postfixAlt[index]);
                }
                else if ((hp & Dimension.HitPosition.Prefix) != 0)
                {
                    propertyPage.MakeVisible(prefix[index]);
                    propertyPage.SelectEntry(prefix[index]);
                }
                else if ((hp & Dimension.HitPosition.UpperText) != 0)
                {
                    propertyPage.MakeVisible(tolPlusText[index]);
                    propertyPage.SelectEntry(tolPlusText[index]);
                }
                else if ((hp & Dimension.HitPosition.AltText) != 0)
                {
                    // den gibts noch nicht
                }

                // es wird nicht gehandled hier, damit der Text-Hotspot noch geht

                // der Texteditor macht noch zu große Probleme hier
                // er ist in der Dimension auch erst für DimText mit Index 0 implementiert ...
                //				Text text = dimension.EditText(vw.Projection,0,Dimension.EditingMode.editDimText);
                //				editor = new TextEditor(text,dimText[0],Frame);
                //				editor.OnFilterMouseMessages(SelectObjectsAction.MouseAction.MouseDown, e,vw,ref handled);
                //				if (handled)
                //				{
                //					text.DidChange += new ChangeDelegate(OnTextEdited);
                //				}
            }
        }

        private void OnTextEdited(IGeoObject Sender, GeoObjectChange Change)
        {
            dimension.SetDimText(0, (Sender as Text).TextString);
        }

        private void OnPointsSelectionChanged(GeoPointProperty sender, bool isSelected)
        {
            if (HotspotChangedEvent != null)
            {
                if (isSelected) HotspotChangedEvent(sender, HotspotChangeMode.Selected);
                else HotspotChangedEvent(sender, HotspotChangeMode.Deselected);
            }
        }
        #region ICommandHandler Members

        virtual public bool OnCommand(string MenuId)
        {
            switch (MenuId)
            {
                case "MenuId.Explode":
                    if (Frame.ActiveAction is SelectObjectsAction)
                    {
                        using (Frame.Project.Undo.UndoFrame)
                        {
                            IGeoObjectOwner addTo = dimension.Owner;
                            if (addTo == null) addTo = Frame.ActiveView.Model;
                            GeoObjectList toSelect = dimension.Decompose();
                            addTo.Remove(dimension);
                            for (int i = 0; i < toSelect.Count; ++i)
                            {
                                addTo.Add(toSelect[i]);
                            }
                            SelectObjectsAction soa = Frame.ActiveAction as SelectObjectsAction;
                            soa.SetSelectedObjects(toSelect); // alle Teilobjekte markieren
                        }
                    }
                    return true;
            }
            return false;
        }

        virtual public bool OnUpdateCommand(string MenuId, CommandState CommandState)
        {
            switch (MenuId)
            {
                case "MenuId.Explode":
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
            return dimension;
        }
        string IGeoObjectShowProperty.GetContextMenuId()
        {
            return "MenuId.Object.Dimension";
        }
        #endregion

        private void OnStateChanged(IPropertyEntry sender, StateChangedArgs args)
        {
            if (HotspotChangedEvent == null) return;

            if (sender is DoubleProperty)
            {
                for (int i = 0; i < textPos.Length; ++i)
                {
                    if (sender == textPos[i])
                    {
                        if (args.EventState == StateChangedArgs.State.Selected)
                        {
                            HotspotChangedEvent(textPosHotSpot[i], HotspotChangeMode.Selected);
                        }
                        else if (args.EventState == StateChangedArgs.State.UnSelected)
                        {
                            HotspotChangedEvent(textPosHotSpot[i], HotspotChangeMode.Deselected);
                        }
                    }
                }
            }
            if (sender == dimLineRef)
            {
                if (args.EventState == StateChangedArgs.State.Selected || args.EventState == StateChangedArgs.State.SubEntrySelected)
                {
                    HotspotChangedEvent(dimLineRef, HotspotChangeMode.Selected);
                }
                else if (args.EventState == StateChangedArgs.State.UnSelected || args.EventState == StateChangedArgs.State.SubEntrySelected)
                {
                    HotspotChangedEvent(dimLineRef, HotspotChangeMode.Deselected);
                }
            }
        }
    }
}
