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
        private readonly Dimension dimension; // this is displayed
        private MultiGeoPointProperty points; // the points of the dimensioning
        private GeoPointProperty dimLineRef; // the DimLineRef
        private GeoVectorProperty direction; // dimLineDirection
        private DoubleHotSpot[] textPosHotSpot; // the text positions as hotspot
        private DoubleProperty[] textPos;
        private StringProperty[] dimText; // the text of the dimensioning
        private StringProperty[] tolPlusText; // the superscript text
        private StringProperty[] tolMinusText; // the subscript text
        private StringProperty[] prefix; // the prefix
        private StringProperty[] postfix; // the postfix
        private StringProperty[] postfixAlt; // the alternative postfix
        private DoubleProperty radiusProperty; // for radius and diameter dimensioning
        private GeoPointProperty centerProperty; // for radius, diameter, and angle dimensioning
        private GeoVectorProperty startAngle; // for angle dimensioning
        private GeoVectorProperty endAngle; // for angle dimensioning
        private GeoVectorHotSpot startAngleHotSpot;
        private GeoVectorHotSpot endAngleHotSpot;
        private IPropertyEntry[] subProperties;
        private bool ignoreChange;

        public ShowPropertyDimension(Dimension dimension, IFrame frame) : base(frame)
        {
            dimension.SortPoints();
            this.dimension = dimension;
            switch (dimension.DimType)
            {
                case Dimension.EDimType.DimPoints: resourceIdInternal = "Dimension.DimPoints"; break;
                case Dimension.EDimType.DimCoord: resourceIdInternal = "Dimension.DimCoord"; break;
                case Dimension.EDimType.DimAngle: resourceIdInternal = "Dimension.DimAngle"; break;
                case Dimension.EDimType.DimRadius: resourceIdInternal = "Dimension.DimRadius"; break;
                case Dimension.EDimType.DimDiameter: resourceIdInternal = "Dimension.DimDiameter"; break;
                case Dimension.EDimType.DimLocation: resourceIdInternal = "Dimension.DimLocation"; break;
            }

            Init();

            if (Frame.ActiveAction is SelectObjectsAction selAct)
            {
                selAct.ClickOnSelectedObjectEvent += OnClickOnSelectedObject;
            }
            ignoreChange = false;
        }

        private void Init()
        {
            // Initializes the entire content of the TreeView  
            // after that, propertyPage.Refresh(this) should usually be called  
            subProperties = null;

            if (dimension.DimType == Dimension.EDimType.DimPoints || dimension.DimType == Dimension.EDimType.DimCoord)
            {
                points = new MultiGeoPointProperty(this, "Dimension.Points", Frame);
                points.ModifyWithMouseEvent += OnModifyPointWithMouse;
                points.PropertyEntryChangedStateEvent += OnPointsStateChanged;
                points.GeoPointSelectionChangedEvent += OnPointsSelectionChanged;
                direction = new GeoVectorProperty(Frame, "Dimension.Direction");
                direction.OnGetValue = OnGetDirection;
                direction.OnSetValue = OnSetDirection;
            }
            if (dimension.DimType == Dimension.EDimType.DimRadius)
            {
                radiusProperty = new DoubleProperty(Frame, "Dimension.Radius");
                radiusProperty.OnSetValue = OnSetRadius;
                radiusProperty.OnGetValue = OnGetRadius;
                radiusProperty.ModifyWithMouse += ModifyRadiusWithMouse;
                radiusProperty.DoubleChanged();
            }
            if (dimension.DimType == Dimension.EDimType.DimDiameter)
            {
                radiusProperty = new DoubleProperty(Frame, "Dimension.Diameter");
                radiusProperty.OnSetValue = OnSetRadius;
                radiusProperty.OnGetValue = OnGetRadius;
                radiusProperty.ModifyWithMouse += ModifyRadiusWithMouse;
                radiusProperty.DoubleChanged();
            }
            if (dimension.DimType == Dimension.EDimType.DimDiameter ||
                dimension.DimType == Dimension.EDimType.DimRadius ||
                dimension.DimType == Dimension.EDimType.DimLocation ||
                dimension.DimType == Dimension.EDimType.DimAngle)
            {
                // haben alle einen Mittelpunkt als Punkt 0
                centerProperty = new GeoPointProperty(Frame, "Dimension.Center");
                centerProperty.OnGetValue = OnGetCenter;
                centerProperty.OnSetValue = OnSetCenter;
                centerProperty.ModifyWithMouse += ModifyCenterWithMouse;
            }
            if (dimension.DimType == Dimension.EDimType.DimAngle)
            {   // start- und Endwinkel
                startAngle = new GeoVectorProperty(Frame, "Dimension.Startangle");
                startAngle.OnGetValue = OnGetStartAngle;
                startAngle.OnSetValue = OnSetStartAngle;
                startAngle.ModifyWithMouse += ModifyStartAngleWithMouse;
                startAngle.SetHotspotPosition(dimension.GetPoint(1));

                endAngle = new GeoVectorProperty(Frame, "Dimension.Endangle");
                endAngle.OnGetValue = OnGetEndAngle;
                endAngle.OnSetValue = OnSetEndAngle;
                endAngle.ModifyWithMouse += ModifyEndAngleWithMouse;
                endAngle.SetHotspotPosition(dimension.GetPoint(2));
                startAngleHotSpot = new GeoVectorHotSpot(startAngle);
                endAngleHotSpot = new GeoVectorHotSpot(endAngle);
            }
            dimLineRef = new GeoPointProperty(Frame, "Dimension.DimLineRef");
            dimLineRef.OnGetValue = OnGetDimLineRef;
            dimLineRef.OnSetValue = OnSetDimLineRef;
            dimLineRef.ModifyWithMouse += ModifyDimLineRef;
            dimLineRef.PropertyEntryChangedStateEvent += OnStateChanged;

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
                //Captured for the lambda expression
                int currentIndex = i;

                textPos[i] = new DoubleProperty(Frame, "Dimension.TextPos");
                textPos[i].OnGetValue = () => dimension.GetTextPos(currentIndex);
                textPos[i].OnSetValue = (value) => dimension.SetTextPos(currentIndex, value);
                textPos[i].DoubleChanged();
                textPos[i].ModifyWithMouse += ModifyTextPosWithMouse;
                textPos[i].PropertyEntryChangedStateEvent += OnStateChanged;

                textPosHotSpot[i] = new DoubleHotSpot(textPos[i]);
                textPosHotSpot[i].Position = dimension.GetTextPosCoordinate(i, Frame.ActiveView.Projection);

                dimText[i] = new StringProperty(dimension.GetDimText(i), "Dimension.DimText");
                dimText[i].OnGetValue = () => dimension.GetDimText(currentIndex);
                dimText[i].OnSetValue = value =>
                {
                    dimension.SetDimText(currentIndex, value);
                    dimText[currentIndex].SetString(dimension.GetDimText(currentIndex));
                };

                tolPlusText[i] = new StringProperty(dimension.GetTolPlusText(i), "Dimension.TolPlusText");
                tolPlusText[i].OnGetValue = () => dimension.GetTolPlusText(currentIndex);
                tolPlusText[i].OnSetValue = value => dimension.SetTolPlusText(currentIndex, value);

                tolMinusText[i] = new StringProperty(dimension.GetTolMinusText(i), "Dimension.TolMinusText");
                tolMinusText[i].OnGetValue = () => dimension.GetTolMinusText(currentIndex);
                tolMinusText[i].OnSetValue = value => dimension.SetTolMinusText(currentIndex, value);

                prefix[i] = new StringProperty(dimension.GetPrefix(i), "Dimension.Prefix");
                prefix[i].OnGetValue = () => dimension.GetPrefix(currentIndex);
                prefix[i].OnSetValue = value => dimension.SetPrefix(currentIndex, value);

                postfix[i] = new StringProperty(dimension.GetPostfix(i), "Dimension.Postfix");
                postfix[i].OnGetValue = () => dimension.GetPostfix(currentIndex);
                postfix[i].OnSetValue = value => dimension.SetPostfix(currentIndex, value);

                postfixAlt[i] = new StringProperty(dimension.GetPostfixAlt(i), "Dimension.PostfixAlt");
                postfixAlt[i].OnGetValue = () => dimension.GetPostfixAlt(currentIndex);
                postfixAlt[i].OnSetValue = value => dimension.SetPostfixAlt(currentIndex, value);
            }
        }
        /// <summary>
        /// Overrides <see cref="PropertyEntryImpl.Added"/>
        /// </summary>
        /// <param name="page"></param>
        public override void Added(IPropertyPage page)
        {
            dimension.DidChangeEvent += DimensionDidChange;
            dimension.UserData.UserDataAddedEvent += OnUserDataAdded;
            dimension.UserData.UserDataRemovedEvent += OnUserDataAdded;
            base.Added(page);
        }
        public override void Removed(IPropertyPage page)
        {
            dimension.DidChangeEvent -= DimensionDidChange;
            dimension.UserData.UserDataAddedEvent -= OnUserDataAdded;
            dimension.UserData.UserDataRemovedEvent -= OnUserDataAdded;
            base.Removed(page);
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
                return Concat(subProperties, dimension.GetAttributeProperties(Frame));
            }
        }
        public override void Opened(bool isOpen)
        {
            if (HotspotChangedEvent != null)
            {
                if (isOpen)
                {
                    HotspotChangedEvent(dimLineRef, HotspotChangeMode.Visible);
                    foreach (var t in textPosHotSpot)
                    {
                        HotspotChangedEvent(t, HotspotChangeMode.Visible);
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
                    foreach (var t in textPosHotSpot)
                    {
                        HotspotChangedEvent(t, HotspotChangeMode.Invisible);
                    }
                    if (startAngle != null)
                    {
                        HotspotChangedEvent(startAngleHotSpot, HotspotChangeMode.Invisible);
                        HotspotChangedEvent(endAngleHotSpot, HotspotChangeMode.Invisible);
                    }
                }
            }
            base.Opened(isOpen);
        }


        #region IDisplayHotSpots Members

        public event HotspotChangedDelegate HotspotChangedEvent;

        public void ReloadProperties()
        {
            // TODO:  Add ShowPropertyDimension.ReloadProperties implementation
        }

        #endregion

        #region IIndexedGeoPoint Members

        public void SetGeoPoint(int index, GeoPoint thePoint)
        {
            dimension.SetPoint(index, thePoint);
        }

        public GeoPoint GetGeoPoint(int index)
        {
            return dimension.GetPoint(index);
        }

        public void InsertGeoPoint(int index, GeoPoint thePoint)
        {
            if (index == -1) dimension.AddPoint(thePoint);
            else throw new NotImplementedException("Dimension: InsertGeoPoint");
            Opened(false);
            Init();
            propertyPage.Refresh(this);
            propertyPage.OpenSubEntries(points, true);
        }

        public void RemoveGeoPoint(int index)
        {
            ignoreChange = true;
            dimension.RemovePoint(index);
            ignoreChange = false;
            subProperties = null;
            propertyPage.Refresh(this);
            propertyPage.OpenSubEntries(points, true);
            if (Frame.ActiveAction is SelectObjectsAction selAct)
            {
                selAct.RemoveSelectedObject(dimension);
                selAct.AddSelectedObject(dimension);
            }
        }

        public int GetGeoPointCount()
        {
            return dimension.PointCount;
        }

        bool IIndexedGeoPoint.MayInsert(int index)
        {
            return false;
            // Punkte einfügen geht einfach mit neuer Bemaßung machen.
            // hierbei würde nur der letzte Punkt verdoppelt und das sieht erstmal blöd aus
        }
        bool IIndexedGeoPoint.MayDelete(int index)
        {
            return dimension.PointCount > 2;
        }
        #endregion

        private GeoPoint OnGetDimLineRef()
        {
            return dimension.DimLineRef;
        }

        private void OnSetDimLineRef(GeoPoint p)
        {
            dimension.DimLineRef = p;
        }

        private GeoVector OnGetDirection()
        {
            return dimension.DimLineDirection;
        }

        private void OnSetDirection(GeoVector v)
        {
            dimension.DimLineDirection = v;
        }

        private bool OnModifyPointWithMouse(IPropertyEntry sender, int index)
        {
            GeneralGeoPointAction gpa = new GeneralGeoPointAction(dimension.GetPoint(index), dimension);
            gpa.UserData.Add("Mode", "Point");
            gpa.UserData.Add("Index", index);
            gpa.GeoPointProperty = sender as GeoPointProperty;
            gpa.SetGeoPointEvent += OnSetPoint;
            gpa.ActionDoneEvent += OnSetPointDone;
            Frame.SetAction(gpa);
            return false;
        }

        private void OnSetPoint(GeneralGeoPointAction sender, GeoPoint newValue)
        {
            int index = (int)sender.UserData.GetData("Index");
            dimension.SetPoint(index, newValue);
        }

        private void OnSetPointDone(GeneralGeoPointAction sender)
        {
            dimension.SortPoints();
            subProperties = null;
            propertyPage.Refresh(this);
            propertyPage.OpenSubEntries(points, true);
        }
        
        private void ModifyDimLineRef(IPropertyEntry sender, bool startModifying)
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
                        if (points.SubEntries[i] is IHotSpot hsp) HotspotChangedEvent(hsp, HotspotChangeMode.Visible);
                    }
                }
                else if (args.EventState == StateChangedArgs.State.CollapseSubEntries)
                {
                    for (int i = 0; i < points.SubEntriesCount; ++i)
                    {
                        if (points.SubEntries[i] is IHotSpot hsp) HotspotChangedEvent(hsp, HotspotChangeMode.Invisible);
                    }
                }
            }
        }

        private void ModifyTextPosWithMouse(IPropertyEntry sender, bool startModifying)
        {
            int ind = (int)(sender as DoubleProperty).UserData.GetData("Index");
            GeoPoint p = dimension.GetTextPosCoordinate(ind, Frame.ActiveView.Projection);
            GeneralGeoPointAction gpa = new GeneralGeoPointAction(p, dimension);
            gpa.UserData.Add("Index", ind);
            gpa.SetGeoPointEvent += OnSetTextPos;
            Frame.SetAction(gpa);
        }

        private void OnSetTextPos(GeneralGeoPointAction sender, GeoPoint p)
        {
            int ind = (int)sender.UserData.GetData("Index");
            dimension.SetTextPosCoordinate(ind, Frame.ActiveView.Projection, p);
        }

        private void DimensionDidChange(IGeoObject sender, GeoObjectChange change)
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

        private void OnSetRadius(double l)
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

        private double OnGetRadius()
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

        private void OnSetRadiusPoint(GeneralGeoPointAction sender, GeoPoint newValue)
        {
            dimension.Radius = Geometry.Dist(dimension.GetPoint(0), newValue);
        }

        private void ModifyRadiusWithMouse(IPropertyEntry sender, bool startModifying)
        {
            GeneralGeoPointAction gpa = new GeneralGeoPointAction(dimension.GetPoint(0), dimension);
            gpa.GeoPointProperty = sender as GeoPointProperty;
            gpa.SetGeoPointEvent += OnSetRadiusPoint;
            Frame.SetAction(gpa);
        }

        private GeoPoint OnGetCenter()
        {
            return dimension.GetPoint(0);
        }
        private void OnSetCenter(GeoPoint p)
        {
            dimension.SetPoint(0, p);
        }

        private void OnSetCenterPoint(GeneralGeoPointAction sender, GeoPoint newValue)
        {
            dimension.SetPoint(0, newValue);
        }

        private void ModifyCenterWithMouse(IPropertyEntry sender, bool startModifying)
        {
            GeneralGeoPointAction gpa = new GeneralGeoPointAction(dimension.GetPoint(0), dimension);
            gpa.GeoPointProperty = sender as GeoPointProperty;
            gpa.SetGeoPointEvent += OnSetCenterPoint;
            Frame.SetAction(gpa);
        }

        private GeoVector OnGetStartAngle()
        {
            GeoVector dir = dimension.GetPoint(1) - dimension.GetPoint(0);
            dir.Norm();
            return dir;
        }

        private void OnSetStartAngle(GeoVector v)
        {
            double r = Geometry.Dist(dimension.GetPoint(1), dimension.GetPoint(0));
            GeoVector dir = dimension.GetPoint(1) - dimension.GetPoint(0);
            dir.Norm();
            dimension.SetPoint(1, dimension.GetPoint(0) + r * dir);
        }

        private void ModifyStartAngleWithMouse(IPropertyEntry sender, bool startModifying)
        {
            GeneralGeoPointAction gpa = new GeneralGeoPointAction(dimension.GetPoint(1), dimension);
            gpa.GeoPointProperty = sender as GeoPointProperty;
            gpa.SetGeoPointEvent += OnSetStartAnglePoint;
            Frame.SetAction(gpa);
        }

        private void OnSetStartAnglePoint(GeneralGeoPointAction sender, GeoPoint newValue)
        {
            dimension.SetPoint(1, newValue);
        }

        private GeoVector OnGetEndAngle()
        {
            GeoVector dir = dimension.GetPoint(2) - dimension.GetPoint(0);
            dir.Norm();
            return dir;
        }

        private void OnSetEndAngle(GeoVector v)
        {
            double r = Geometry.Dist(dimension.GetPoint(2), dimension.GetPoint(0));
            GeoVector dir = dimension.GetPoint(2) - dimension.GetPoint(0);
            dir.Norm();
            dimension.SetPoint(2, dimension.GetPoint(0) + r * dir);
        }

        private void ModifyEndAngleWithMouse(IPropertyEntry sender, bool startModifying)
        {
            GeneralGeoPointAction gpa = new GeneralGeoPointAction(dimension.GetPoint(2), dimension);
            gpa.GeoPointProperty = sender as GeoPointProperty;
            gpa.SetGeoPointEvent += OnSetEndAnglePoint;
            Frame.SetAction(gpa);
        }

        private void OnSetEndAnglePoint(GeneralGeoPointAction sender, GeoPoint newValue)
        {
            dimension.SetPoint(2, newValue);
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

        private void OnTextEdited(IGeoObject sender, GeoObjectChange change)
        {
            dimension.SetDimText(0, (sender as Text).TextString);
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

        public virtual bool OnCommand(string menuId)
        {
            switch (menuId)
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
                            foreach (var t in toSelect)
                            {
                                addTo.Add(t);
                            }
                            SelectObjectsAction soa = Frame.ActiveAction as SelectObjectsAction;
                            soa.SetSelectedObjects(toSelect); // alle Teilobjekte markieren
                        }
                    }
                    return true;
            }
            return false;
        }

        public virtual bool OnUpdateCommand(string menuId, CommandState commandState)
        {
            switch (menuId)
            {
                case "MenuId.Explode":
                    commandState.Enabled = true;
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
