using CADability.Actions;
using CADability.Attribute;
using CADability.Curve2D;
using MathNet.Numerics.LinearAlgebra.Double;
using CADability.Shapes;
using CADability.UserInterface;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.Serialization;
using System.Text;
using Wintellect.PowerCollections;
using MathNet.Numerics.LinearAlgebra.Factorization;
#if WEBASSEMBLY
using CADability.WebDrawing;
using Point = CADability.WebDrawing.Point;
#else
using System.Drawing;
using Point = System.Drawing.Point;
#endif

namespace CADability.GeoObject
{
    internal class ShowPropertyShell : PropertyEntryImpl, ICommandHandler, IGeoObjectShowProperty
    {
        Shell shell;
        private IPropertyEntry[] attributeProperties; // Anzeigen für die Attribute (Ebene, Farbe u.s.w)
        public ShowPropertyShell(Shell shell, IFrame frame)
            : base(frame)
        {
            resourceIdInternal = "Shell.Object";
            this.shell = shell;
            attributeProperties = shell.GetAttributeProperties(frame);
        }
        public override PropertyEntryType Flags => PropertyEntryType.GroupTitle | PropertyEntryType.HasSubEntries | PropertyEntryType.ContextMenu | PropertyEntryType.Selectable;
        private IPropertyEntry[] subEntries;
        public override IPropertyEntry[] SubItems
        {
            get
            {
                if (subEntries == null)
                {
                    List<IPropertyEntry> se = new List<IPropertyEntry>();
                    se.Add(new NameProperty(this.shell, "Name", "Solid.Name"));
                    foreach (Face face in shell.Faces)
                    {
                        IPropertyEntry sp = face.GetShowProperties(base.Frame);
                        sp.ReadOnly = true;
                        se.Add(sp);
                    }
                    se.AddRange(attributeProperties);
                    subEntries = se.ToArray();
                }
                return subEntries;
            }
        }
        /// <summary>
        /// Overrides <see cref="PropertyEntryImpl.Opened (bool)"/>
        /// </summary>
        /// <param name="IsOpen"></param>
        public override void Opened(bool IsOpen)
        {
            base.Opened(IsOpen);
            if (IsOpen && subEntries != null)
            {
                // warum, oben ist doch schon ReadOnly gesetzt
                //for (int i = 0; i < subEntries.Length; ++i)
                //{
                //    subEntries[i].ReadOnly = true;
                //}
            }
        }
        public override MenuWithHandler[] ContextMenu
        {
            get
            {
                List<MenuWithHandler> items = new List<MenuWithHandler>(MenuResource.LoadMenuDefinition("MenuId.Object.Shell", false, this));
                CreateContextMenueEvent?.Invoke(this, items);
                shell.GetAdditionalContextMenue(this, Frame, items);
                return items.ToArray();
            }
        }
        #region IGeoObjectShowProperty Members

        public event CADability.GeoObject.CreateContextMenueDelegate CreateContextMenueEvent;

        IGeoObject IGeoObjectShowProperty.GetGeoObject()
        {
            return shell;
        }

        string IGeoObjectShowProperty.GetContextMenuId()
        {
            return "MenuId.Object.Shell";
        }

        #endregion

        #region ICommandHandler Members

        bool ICommandHandler.OnCommand(string MenuId)
        {
            switch (MenuId)
            {
                case "MenuId.Explode":
                    if (Frame.ActiveAction is SelectObjectsAction)
                    {
                        using (Frame.Project.Undo.UndoFrame)
                        {
                            IGeoObjectOwner addTo = shell.Owner;
                            if (addTo == null) addTo = Frame.ActiveView.Model;
                            GeoObjectList toSelect = shell.Decompose();
                            addTo.Remove(shell);
                            for (int i = 0; i < toSelect.Count; ++i)
                            {
                                addTo.Add(toSelect[i]);
                            }
                            SelectObjectsAction soa = Frame.ActiveAction as SelectObjectsAction;
                            soa.SetSelectedObjects(toSelect); // alle Teilobjekte markieren
                        }
                    }
                    return true;
                case "MenuId.Offset":
                    Frame.SetAction(new ConstructShellOffset(shell));
                    return true;
                case "MenuId.ThickShell":
                    Frame.SetAction(new ConstructThickShell(shell));
                    return true;
                case "MenuId.Solid.ShellToSolid":
                    if (shell.OpenEdges.Length == 0)
                    {
                        using (Frame.Project.Undo.UndoFrame)
                        {
                            IGeoObjectOwner addTo = shell.Owner;
                            if (addTo == null) addTo = Frame.ActiveView.Model;
                            Solid sld = Solid.MakeSolid(shell);
                            if (sld != null)
                            {
                                addTo.Remove(shell);
                                sld.CopyAttributes(shell);
                                addTo.Add(sld);
                                if (Frame.ActiveAction is SelectObjectsAction)
                                {
                                    SelectObjectsAction soa = Frame.ActiveAction as SelectObjectsAction;
                                    soa.SetSelectedObjects(new GeoObjectList(sld)); // alle Teilobjekte markieren
                                }
                            }
                        }
                    }
                    return true;
            }
            return false;
        }

        bool ICommandHandler.OnUpdateCommand(string MenuId, CommandState CommandState)
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
    }

    [Serializable()]
    public class Shell : IGeoObjectImpl, ISerializable, IColorDef, IGetSubShapes, IGeoObjectOwner, IDeserializationCallback, IExportStep, IJsonSerialize, IJsonSerializeDone
    {
        private Face[] faces; // the faces that make the shell
        private Edge[] edges; // all edges of the faces, the faces are connected so we do not have duplicate edges
        private string name; // the name of the shell, may be null
        private List<ParametricProperty> parametricProperties;
        private bool orientedAndSeamless; // soll bedeuten, dass alle Faces "outwardoriented" sind und es keine Säume gibt (Kanten, die ein Face mit sich verbinden)
        [Flags]
        public enum ShellFlags
        {
            NonPeriodicFaces = 0x1,
            FacesCombined = 0x2,
            EdgesCombined = 0x4
        }
        public ShellFlags State;
        private GeoObjectList featureAxis;
        #region polymorph construction
        public delegate Shell ConstructionDelegate();
        public static ConstructionDelegate Constructor;
        public static Shell Construct()
        {
            if (Constructor != null) return Constructor();
            return new Shell();
        }
        public delegate void ConstructedDelegate(Shell justConstructed);
        public static ConstructedDelegate Constructed;
        protected Shell()
            : base()
        {
            if (Constructed != null) Constructed(this);
        }
        #endregion
        public static Shell FromFaces(params Face[] faces)
        {
            Shell res = Shell.Construct();
            res.SetFaces(faces);
            return res;
        }
        public static Shell FromFaces(Face[] faces, bool connect = false)
        {
            Shell res = Shell.Construct();
            if (connect) connectFaces(faces, Precision.eps);
            res.SetFaces(faces);
            return res;
        }
        /// <summary>
        /// Returns all the edges of this Shell. Each egde is unique in the array 
        /// but may belong to two different faces.
        /// </summary>
        public Edge[] Edges
        {
            get
            {
                if (edges == null)
                {
                    Set<Edge> edgelist = new Set<Edge>();
                    foreach (Face fc in Faces)
                    {
                        foreach (Edge ed in fc.AllEdges)
                        {
                            edgelist.Add(ed);
                        }
                    }
                    edges = edgelist.ToArray();
                }
                return edges;
            }
        }

        internal bool ShowFeatureAxis
        {
            get
            {
                if (FeatureAxis.Count > 0) return FeatureAxis[0].IsVisible;
                return false;
            }
            set
            {
                using (new ChangingAttribute(this, "ShowFeatureAxis", !value))
                {
                    for (int i = 0; i < FeatureAxis.Count; i++)
                    {
                        FeatureAxis[i].IsVisible = value;
                    }
                }
            }
        }

        /// <summary>
        /// Returns all <see cref="Face">Faces</see> of this shell. Do not modify the returned array since it is
        /// (for better performance) the original array contained in this Shell.
        /// </summary>
        public Face[] Faces
        {
            get
            {
                return faces;
            }
        }
        public Vertex[] Vertices
        {
            get
            {
                HashSet<Vertex> res = new HashSet<Vertex>();
                foreach (Face fc in Faces)
                {
                    res.UnionWith(fc.Vertices);
                }
                return res.ToArray();
            }
        }
        public double Volume(double precision)
        {
            double sum = 0.0;
            double corr = 0.0;
            foreach (Face fc in Faces)
            {
                fc.GetTriangulation(precision, out GeoPoint[] trianglePoint, out GeoPoint2D[] triangleUVPoint, out int[] triangleIndex, out BoundingCube triangleExtent);
                if (triangleIndex == null) continue;
                // tried to use normals for correction, but the distance from plane had better results
                //GeoVector[] triangleNormals = new GeoVector[triangleUVPoint.Length];
                //for (int i = 0; i < triangleUVPoint.Length; i++)
                //{
                //    triangleNormals[i] = fc.Surface.GetNormal(triangleUVPoint[i]).Normalized;
                //}
                for (int i = 0; i < triangleIndex.Length; i += 3)
                {
                    // use the signed volume of the tetrahedron from the origin to the triangle 
                    sum += trianglePoint[triangleIndex[i]].x * trianglePoint[triangleIndex[i + 1]].y * trianglePoint[triangleIndex[i + 2]].z -
                           trianglePoint[triangleIndex[i]].x * trianglePoint[triangleIndex[i + 1]].z * trianglePoint[triangleIndex[i + 2]].y -
                           trianglePoint[triangleIndex[i]].y * trianglePoint[triangleIndex[i + 1]].x * trianglePoint[triangleIndex[i + 2]].z +
                           trianglePoint[triangleIndex[i]].y * trianglePoint[triangleIndex[i + 1]].z * trianglePoint[triangleIndex[i + 2]].x +
                           trianglePoint[triangleIndex[i]].z * trianglePoint[triangleIndex[i + 1]].x * trianglePoint[triangleIndex[i + 2]].y -
                           trianglePoint[triangleIndex[i]].z * trianglePoint[triangleIndex[i + 1]].y * trianglePoint[triangleIndex[i + 2]].x;
                    // if the triangle is not planar (a point in the middle has some distance to the triangle) then we use a correction value
                    // which was experimentally set to 3/4 of the "thick triangle". The calculation can certainly be made easier 
                    try
                    {
                        Plane pln = new Plane(trianglePoint[triangleIndex[i]], trianglePoint[triangleIndex[i + 1]] - trianglePoint[triangleIndex[i]], trianglePoint[triangleIndex[i + 2]] - trianglePoint[triangleIndex[i]]);
                        double d = pln.Distance(fc.Surface.PointAt(new GeoPoint2D(triangleUVPoint[triangleIndex[i]], triangleUVPoint[triangleIndex[i + 1]], triangleUVPoint[triangleIndex[i + 2]])));
                        GeoVector cp = (trianglePoint[triangleIndex[i + 1]] - trianglePoint[triangleIndex[i]]) ^ (trianglePoint[triangleIndex[i + 2]] - trianglePoint[triangleIndex[i]]);
                        double a = cp.Length / 2.0; // area of the triangle
                        corr += a * d * 3 / 4; // 3/4 is a good value for spheres and cylinders
                    }
                    catch (PlaneException) { }
                }
            }
            return sum / 6 + corr;
        }

        internal List<ParametricProperty> ParametricProperties { get { return parametricProperties; } }

        internal void AddParametricProperty(ParametricProperty parametricProperty)
        {
            if (parametricProperties == null) parametricProperties = new List<ParametricProperty>();
            parametricProperties.Add(parametricProperty);
            // need to sort according to dependencies and affection
        }

        internal void RemoveParametricProperty(string name)
        {   // there may be multiple properties with the same name
            if (parametricProperties != null)
            {
                for (int i = parametricProperties.Count - 1; i >= 0; i--)
                {
                    if (parametricProperties[i].Name == name) parametricProperties.RemoveAt(i);
                }
            }
        }
        internal double GetParametricValue(string name)
        {
            for (int i = 0; i < parametricProperties.Count; i++)
            {
                if (parametricProperties[i].Name == name) return parametricProperties[i].Value;
            }
            return double.MaxValue;
        }
        internal List<IPropertyEntry> GetParameterProperties(IFrame frame, GroupProperty groupProperty)
        {
            List<IPropertyEntry> res = new List<IPropertyEntry>();
            IView activeView = frame.ActiveView;
            if (ParametricProperties != null)
            {
                HashSet<string> ppnames = new HashSet<string>();
                for (int i = 0; i < ParametricProperties.Count; i++)
                {
                    ppnames.Add(ParametricProperties[i].Name); // there may be duplicate names
                }
                foreach (string name in ppnames)
                {
                    LengthProperty pp = new LengthProperty(frame); // maybe we also will need an AngleProperty depending on the property type
                    pp.LabelText = name;
                    string capturedName = name;
                    pp.OnGetValue = delegate () { return GetParametricValue(capturedName); };
                    pp.OnSetValue = delegate (double val) { SetParametricValue(capturedName, val); };
                    pp.PropertyEntryChangedStateEvent += ParameterPropertyEntryChangedStateEvent;
                    MenuWithHandler menu = new MenuWithHandler("MenuId.ParametricProperty.Remove");
                    menu.OnCommand = delegate (string id)
                    {
                        RemoveParametricProperty(capturedName);
                        List<IPropertyEntry> items = new List<IPropertyEntry>(groupProperty.SubItems);
                        for (int i = 0; i < items.Count; i++)
                        {
                            if (items[i] is PropertyEntryImpl impl && impl.LabelText == capturedName)
                            {
                                items.RemoveAt(i);
                                break; // name are unique
                            }
                        }
                        groupProperty.SetSubEntries(items.ToArray());
                        return true;
                    };
                    pp.PrependContextMenu = new MenuWithHandler[] { menu };
                    var paintHandler = new PaintView(delegate (Rectangle Extent, IView View, IPaintTo3D PaintTo3D)
                    {
                        PaintParametricFeedback(capturedName, View, PaintTo3D);
                    });
                    pp.PropertyEntryChangedStateEvent += delegate (IPropertyEntry sender, StateChangedArgs args)
                    {
                        System.Diagnostics.Trace.WriteLine("State changed: " + capturedName + ", " + args.EventState.ToString());
                        switch (args.EventState)
                        {
                            case StateChangedArgs.State.Selected:
                                activeView.SetPaintHandler(PaintBuffer.DrawingAspect.Select, paintHandler);
                                activeView.Invalidate(PaintBuffer.DrawingAspect.Select, activeView.DisplayRectangle);
                                break;
                            case StateChangedArgs.State.UnSelected:
                            case StateChangedArgs.State.Removed:
                                activeView.RemovePaintHandler(PaintBuffer.DrawingAspect.Select, paintHandler);
                                break;
                        }
                    };
                    res.Add(pp);
                }
            }
            return res;
        }

        private void ParameterPropertyEntryChangedStateEvent(IPropertyEntry sender, StateChangedArgs args)
        {
            switch (args.EventState)
            {
                case StateChangedArgs.State.Added:
                    for (int i = 0; i < ParametricProperties.Count; i++)
                    {
                        if (sender.Label == ParametricProperties[i].Name) ParametricProperties[i].PropertyEntry = sender;
                    }
                    break;
                case StateChangedArgs.State.Removed:
                    for (int i = 0; i < ParametricProperties.Count; i++)
                    {
                        if (sender.Label == ParametricProperties[i].Name) ParametricProperties[i].PropertyEntry = null;
                    }
                    break;
                case StateChangedArgs.State.EditEnded:
                    if (sender is LengthProperty lp) lp.Refresh();
                    break;
            }
        }

        private void PaintParametricFeedback(string name, IView view, IPaintTo3D paintTo3D)
        {
            paintTo3D.PushState();
            bool oldSelect = paintTo3D.SelectMode;
            paintTo3D.SelectMode = true;
            paintTo3D.SelectColor = Color.FromArgb(128, Color.Yellow);
            int wobbleWidth = -1; // i.e. also show faces which are behind other faces
            if ((paintTo3D.Capabilities & PaintCapabilities.ZoomIndependentDisplayList) != 0)
            {
                paintTo3D.OpenList("select-context");
                for (int i = 0; i < ParametricProperties.Count; i++)
                {
                    if (ParametricProperties[i].Name == name) // there may be duplicate names
                    {
                        GeoObjectList feedback = ParametricProperties[i].GetFeedback(view.Projection);
                        if (feedback != null)
                        {
                            foreach (IGeoObject geoObject in feedback)
                            {
                                geoObject.PaintTo3D(paintTo3D);
                            }
                        }
                    }
                }
                IPaintTo3DList displayList = paintTo3D.CloseList();
                paintTo3D.SelectedList(displayList, wobbleWidth);
            }
            paintTo3D.SelectMode = oldSelect;
            paintTo3D.PopState();
        }

        internal void SetParametricValue(string name, double val)
        {
            HashSet<ParametricProperty> parametricPropertiesToCheck = new HashSet<ParametricProperty>();
            Parametric pm = new Parametric(this);
            for (int i = 0; i < parametricProperties.Count; i++)
            {   // there may be multiple parametric properties with the same name and value
                if (parametricProperties[i].Name == name)
                {
                    parametricProperties[i].Value = val;
                    parametricProperties[i].Execute(pm);
                    parametricPropertiesToCheck.Add(parametricProperties[i]);
                }
            }
            // We could use dependencies in the following meaning:
            // The parametric(s) that was just executed may have changed (e.g.) an edge which ist the "fromHere" object of another parametric in the same shell
            // This would be a dependent parametric, i.e. its startpoint has changed, but its value is still there in the editbox the old value, but in the
            // modified shell, the value has changed.
            // We could argue that this value has to be kept unchanged and therefore we execute this dependent parametric with the unchanged od value from the edit box.
            // the following code does exactely that, with one part missing: The calculation of the actual value in the dependent parametric relies on the original shell
            // and not on the modified shall in the parametric. We would have to use the parametrics dictionaries to acces the modified edges.
            // This is not difficult. e.g. in the ParametricDistanceProperty we would have to pass the parametric to the GetDistanceVector method and use the modified edges
            // (or faces or vertices) there.
            // But do we really want this behaviour? In a parametric we can specify more faces to modify to establish such a dependency there.
            // So the following code is disabled
            //Dictionary<ParametricProperty,List<ParametricProperty>> dependencies = new Dictionary<ParametricProperty,List<ParametricProperty>>();
            //foreach (ParametricProperty pp in parametricProperties)
            //{
            //    HashSet<object> affectedObjects = new HashSet<object>(pp.GetAffectedObjects());
            //    dependencies[pp] = new List<ParametricProperty>();
            //    foreach (ParametricProperty ppp in parametricProperties)
            //    {
            //        if (pp == ppp) continue;
            //        if (affectedObjects.Contains(ppp.GetAnchor())) dependencies[pp].Add(ppp);
            //    }
            //}
            //HashSet<ParametricProperty> appliedParametricProperties = new HashSet<ParametricProperty>();
            //while (true)
            //{
            //    HashSet<ParametricProperty> nextLevel = new HashSet<ParametricProperty>();
            //    foreach (ParametricProperty pp in parametricPropertiesToCheck)
            //    {
            //        if (appliedParametricProperties.Contains(pp)) continue;
            //        appliedParametricProperties.Add(pp);
            //        foreach (ParametricProperty ppp in dependencies[pp])
            //        {
            //            if (appliedParametricProperties.Contains(ppp)) continue;
            //            ppp.Execute(pm);
            //            nextLevel.Add(ppp);
            //        }
            //    }
            //    if (nextLevel.Count > 0)
            //    {
            //        parametricPropertiesToCheck = nextLevel;
            //    }
            //    else break;
            //}
            Shell modified = pm.Result();
            // now we need to copy the geometry back onto the faces, edges, vertices
            if (modified != null)
            {
                using (new Changing(this))
                {
                    pm.GetDictionaries(out Dictionary<Face, Face> faceDict, out Dictionary<Edge, Edge> edgeDict, out Dictionary<Vertex, Vertex> vertexDict);
                    foreach (Face face in Faces)
                    {
                        if (faceDict.TryGetValue(face, out Face modifiedFace))
                        {
                            face.CopyGeometryNoEdges(modifiedFace);
                        }
                    }
                    foreach (Edge edge in Edges)
                    {
                        if (edgeDict.TryGetValue(edge, out Edge modifiedEdge))
                        {
                            edge.Curve3D = modifiedEdge.Curve3D;
                            edge.PrimaryCurve2D = modifiedEdge.PrimaryCurve2D;
                            edge.SecondaryCurve2D = modifiedEdge.SecondaryCurve2D;
                        }
                    }
                    foreach (Vertex vertex in Vertices)
                    {
                        if (vertexDict.TryGetValue(vertex, out Vertex modifiedVertex))
                        {
                            vertex.Position = modifiedVertex.Position;
                        }
                    }
                }
                for (int i = 0; i < parametricProperties.Count; i++)
                {
                    if (parametricProperties[i].Name != name)
                    {   // refresh the values of the other parameters
                        if (parametricProperties[i].PropertyEntry is PropertyEntryImpl pe) pe.Refresh();
                    }
                }
            }
            else
            {
                for (int i = 0; i < parametricProperties.Count; i++)
                {
                    if (parametricProperties[i].Name == name)
                    {   // refresh this values (because the value entered is not possible
                        if (parametricProperties[i].PropertyEntry is PropertyEntryImpl pe) pe.Refresh();
                    }
                }
            }
        }

        internal List<IPropertyEntry> GetFeatureProperties(IFrame frame, GroupProperty groupProperty)
        {
            List<IPropertyEntry> res = new List<IPropertyEntry>();
            IView activeView = frame.ActiveView;
            Dictionary<string, HashSet<Face>> features = GetFeatures();
            foreach (KeyValuePair<string, HashSet<Face>> feature in features)
            {
                List<IPropertyEntry> faceProperties = new List<IPropertyEntry>();
                foreach (Face face in feature.Value)
                {
                    faceProperties.Add(face.GetShowProperties(frame));
                }
                GroupProperty featureProperty = new GroupProperty("", faceProperties.ToArray());
                featureProperty.LabelText = feature.Key;
                featureProperty.SetFlags(featureProperty.Flags | PropertyEntryType.LabelEditable);
                res.Add(featureProperty);
            }
            return res;
        }

        public Dictionary<string, HashSet<Face>> GetFeatures()
        {
            Dictionary<string, HashSet<Face>> features = new Dictionary<string, HashSet<Face>>();
            foreach (Face face in Faces)
            {
                HashSet<string> featureNames = face.UserData["CADability.Feature"] as HashSet<string>;
                if (featureNames != null)
                {
                    foreach (string featureName in featureNames)
                    {
                        if (!features.TryGetValue(featureName, out HashSet<Face> faces))
                        {
                            faces = new HashSet<Face>();
                            features[featureName] = faces;
                        }
                        faces.Add(face);
                    }
                }
            }
            return features;
        }
        public void AddFeature(string name, IEnumerable<Face> faces)
        {
            foreach (Face face in faces)
            {
                HashSet<string> featureNames = face.UserData["CADability.Feature"] as HashSet<string>;
                if (featureNames == null)
                {
                    face.UserData["CADability.Feature"] = new SerializableHashSetString(new string[] { name });
                }
                else
                {
                    featureNames.Add(name);
                }
            }
        }
        public void RemoveFeature(string name)
        {
            foreach (Face face in faces)
            {
                HashSet<string> featureNames = face.UserData["CADability.Feature"] as HashSet<string>;
                if (featureNames != null)
                {
                    featureNames.Remove(name);
                }
            }
        }
        public string GetNewFeatureName()
        {
            int maxNameindex = 0;
            Dictionary<string, HashSet<Face>> features = GetFeatures();
            string nn = StringTable.GetString("Solid.NewFeatureName");
            foreach (string name in features.Keys)
            {
                if (name.StartsWith(nn))
                {
                    string nni = name.Substring(nn.Length);
                    if (int.TryParse(nni, out int index))
                    {
                        maxNameindex = Math.Max(maxNameindex, index);
                    }
                }
            }
            return nn + (maxNameindex + 1).ToString();
        }
        internal double GetGauge(Face startHere, out HashSet<Face> frontSide, out HashSet<Face> backSide)
        {
            double thickness = double.MaxValue;
            frontSide = new HashSet<Face>();
            backSide = new HashSet<Face>();
            HashSet<Face> alreadyChecked = new HashSet<Face>();
            // look for all faces, which are parallel
            foreach (Face face in faces)
            {
                if (face == startHere) continue;
                if (face.IsOppositeParallel(startHere) != double.MaxValue)
                {
                    backSide.Add(face);
                }
            }
            if (backSide.Count == 0) return double.MaxValue;
            if (backSide.Count > 1)
            {   // find the best parallel face (which may be arbitrary)
                // special case for plane faces
                if (startHere.Surface is PlaneSurface ps)
                {   // with parallel planar faces we can project the outlines of the faces onto the "startHere" face, and look at the size of the intersection area
                    // select the face with the biggest intersection area
                    Face bestFace = null;
                    double maxArea = 0.0;
                    foreach (Face face in backSide)
                    {
                        List<ICurve2D> outline = new List<ICurve2D>();
                        for (int i = 0; i < face.OutlineEdges.Length; i++)
                        {
                            ICurve2D projected = ps.GetProjectedCurve(face.OutlineEdges[i].Curve3D, 0.0);
                            if (!face.OutlineEdges[i].Forward(face)) projected.Reverse();
                            outline.Add(projected);
                        }
                        Border faceOutline = new Border(outline.ToArray());
                        try
                        {
                            BorderOperation bo = new BorderOperation(startHere.Area.Outline, faceOutline);
                            if (bo.Position != BorderOperation.BorderPosition.disjunct)
                            {
                                double a;
                                if (bo.Position == BorderOperation.BorderPosition.identical) a = double.MaxValue; // best case: identical parallel faces
                                else a = bo.Intersection().Area;
                                if (a > maxArea)
                                {
                                    maxArea = a;
                                    bestFace = face;
                                }
                            }
                        }
                        catch (Exception) { }
                    }
                    backSide.Clear();
                    if (bestFace != null) backSide.Add(bestFace);
                }
                else
                {   // this is not perfect
                    HashSet<Face> goodFaces = new HashSet<Face>();
                    GeoPoint2D sp = startHere.Area.GetSomeInnerPoint();
                    GeoPoint lstart = startHere.Surface.PointAt(sp);
                    GeoVector ldir = startHere.Surface.GetNormal(sp);
                    foreach (Face face in backSide)
                    {
                        GeoPoint2D[] pos = face.Surface.GetLineIntersection(lstart, ldir);
                        for (int i = 0; i < pos.Length; i++)
                        {
                            if (face.Area.Outline.GetPosition(pos[i]) == Border.Position.Inside)
                            {
                                goodFaces.Add(face);
                                break;
                            }
                        }
                    }
                    backSide.Clear();
                    if (goodFaces.Any())
                    {
                        backSide.Add(goodFaces.First());
                    }
                }
            }
            frontSide.Add(startHere);
            if (backSide.Count == 1)
            {
                thickness = startHere.IsOppositeParallel(backSide.First());
                bool found = false;
                do
                {
                    found = false;
                    HashSet<Face> frontTangential = new HashSet<Face>();
                    HashSet<Face> backTangential = new HashSet<Face>();
                    foreach (Face face in frontSide)
                    {
                        foreach (Edge edge in face.Edges)
                        {
                            if (edge.IsTangentialEdge() && !frontSide.Contains(edge.OtherFace(face)) && !backSide.Contains(edge.OtherFace(face)))
                            {
                                frontTangential.Add(edge.OtherFace(face));
                            }
                        }
                    }
                    foreach (Face face in backSide)
                    {
                        foreach (Edge edge in face.Edges)
                        {
                            if (edge.IsTangentialEdge() && !backSide.Contains(edge.OtherFace(face)) && !frontSide.Contains(edge.OtherFace(face)))
                            {
                                backTangential.Add(edge.OtherFace(face));
                            }
                        }
                    }
                    foreach (Face front in frontTangential)
                    {
                        foreach (Face back in backTangential)
                        {
                            if (Math.Abs(Math.Abs(front.IsOppositeParallel(back)) - Math.Abs(thickness)) < Precision.eps)
                            {
                                backTangential.Remove(back);
                                backSide.Add(back);
                                frontSide.Add(front);
                                found = true;
                                break;
                            }
                        }
                    }
                } while (found);
            }
            return thickness;
        }
        internal int GetFaceDistances(Face distanceFrom, out List<Face> distanceTo, out List<double> distance, out List<GeoPoint> pointsFrom, out List<GeoPoint> pointsTo)
        {
            distanceTo = new List<Face>();
            distance = new List<double>();
            pointsFrom = new List<GeoPoint>();
            pointsTo = new List<GeoPoint>();
            foreach (Face face in Faces)
            {
                if (face == distanceFrom) continue;
                if (Surfaces.ParallelDistance(distanceFrom.Surface, distanceFrom.Domain, face.Surface, face.Domain, out GeoPoint2D uv1, out GeoPoint2D uv2))
                {
                    GeoPoint pFrom = distanceFrom.Surface.PointAt(uv1);
                    GeoPoint pTo = face.Surface.PointAt(uv2);
                    double dist = pFrom | pTo;
                    if (dist > Precision.eps)
                    {
                        distanceTo.Add(face);
                        distance.Add(dist);
                        pointsFrom.Add(pFrom);
                        pointsTo.Add(pTo);
                    }
                }
            }
            return distanceTo.Count;
        }

        /// <summary>
        /// The name of the shell. 
        /// </summary>
        public string Name
        {
            get
            {
                return name;
            }
            set
            {
                using (new Changing(this, false, true, "Name", name))
                {
                    name = value;
                }
            }
        }
        public string NameOrEmpty
        {
            get
            {
                if (name == null) return "";
                else return name;
            }
        }
        /// <summary>
        /// Returns the curves that result from a planar intersection of this shell with the provided plane.
        /// The curves are properly clipped.
        /// </summary>
        /// <param name="pl">The plane to intersect with</param>
        /// <returns>Array of intersection curves</returns>
        public ICurve[] GetPlaneIntersection(PlaneSurface pl)
        {
            List<ICurve> res = new List<ICurve>();
            foreach (Face face in Faces)
            {
                res.AddRange(face.GetPlaneIntersection(pl));
            }
            return res.ToArray();
        }
        public GeoPoint[] GetLineIntersection(GeoPoint location, GeoVector direction)
        {
            List<GeoPoint> res = new List<GeoPoint>();
            foreach (Face fc in faces)
            {
                res.AddRange(fc.GetLineIntersection(location, direction));
            }
            return res.ToArray();
        }
        /// <summary>
        /// Sets the faces of a shell. The faces must all be connected to form a single shell, they may have free edges.
        /// This is not checked. This method does not accumulate the faces.
        /// </summary>
        /// <param name="faces">The new faces that build the shell</param>
        public void SetFaces(Face[] faces)
        {
            this.faces = faces.Clone() as Face[]; // shallow copy
            for (int i = 0; i < faces.Length; ++i)
            {
                faces[i].Owner = this;
            }
            edges = null;
        }
        public void SetFaces(Shell sh, Face[] faces)
        {   // nur wg. undo
            if (sh != this) return;
            this.faces = faces.Clone() as Face[]; // shallow copy
            for (int i = 0; i < faces.Length; ++i)
            {
                faces[i].Owner = this;
            }
            edges = null;
        }
        public void MakeRegularSurfaces(double maxError)
        {
            Set<Edge> recalcEdges = new Set<Edge>(); // hier alle edges sammeln, die neu berechnet werden müssen
            Dictionary<Face, ModOp2D> FaceModOps = new Dictionary<Face, ModOp2D>();
            for (int i = 0; i < faces.Length; i++)
            {
                faces[i].Orient(); // die Orientierung muss gesetzt sein, sonst werden die 2D Kurven falsch
            }
            for (int i = 0; i < faces.Length; i++)
            {
                ModOp2D mop = faces[i].MakeRegularSurface(maxError, recalcEdges);
                FaceModOps[faces[i]] = mop;
            }
            foreach (Edge edge in recalcEdges)
            {
                edge.RecalcFromSurface(FaceModOps, maxError);
            }
            for (int i = 0; i < faces.Length; i++)
            {   // nicht sicher ob das notwendig ist
                faces[i].CheckPeriodic(); // macht wie Area
                                          //SimpleShape forceArea = faces[i].Area;
                faces[i].ForceTriangulation(-1.0);
            }
        }
#if DEBUG
        public void MakeRegularEdges()
#else
        internal void MakeRegularEdges()
#endif
        {
            Set<Face> affectedFaces = new Set<Face>();
            foreach (Edge edg in Edges)
            {
                edg.MakeRegular(affectedFaces);
            }
            foreach (Face face in affectedFaces)
            {
                face.InvalidateArea();
                SimpleShape ss = face.Area;
            }
        }

        /// <summary>
        /// Returns a SimpleShape with the outline and holes of the shadow of the projection of the shell perpendicular on the provided plane.
        /// </summary>
        /// <param name="onThisPlane"></param>
        /// <returns></returns>
        public SimpleShape GetShadow(Plane onThisPlane)
        {
#if DEBUG
            GeoObjectList dbg = new GeoObjectList();
            ColorDef cd1 = new Attribute.ColorDef("cd1", Color.Red);
            ColorDef cd2 = new Attribute.ColorDef("cd2", Color.Blue);
            Dictionary<SimpleShape, Face> dict = new Dictionary<SimpleShape, Face>();
#endif
            BoundingRect ext = BoundingRect.EmptyBoundingRect;
            List<SimpleShape> sortedShapes = new List<SimpleShape>();
            double prec = 0.0;
            for (int i = 0; i < faces.Length; i++)
            {
                SimpleShape sh = faces[i].GetShadow(onThisPlane);
                if (sh != null)
                {
#if DEBUG
                    dict[sh] = faces[i];
                    if (!sh.Outline.IsClosed)
                    {

                    }
#endif
                    sortedShapes.Add(sh); // einfachste Art das größte zuesrt zu haben
                    ext.MinMax(sh.GetExtent());
                    prec = Math.Max(prec, ext.Size);
                }
            }
            prec *= 1e-6; // to remove extremely small shapes
            for (int i = sortedShapes.Count - 1; i >= 0; --i)
            {
                SimpleShape sh = sortedShapes[i];
                sh.Reduce(prec);
                if (!sh.Outline.IsClosed || sh.Area < prec * prec) sortedShapes.RemoveAt(i);
            }

            sortedShapes.Sort(new Comparison<SimpleShape>(delegate (SimpleShape s1, SimpleShape s2) { return s2.Area.CompareTo(s1.Area); }));

            // Area ist in den Borders gecached, das größte zuerst

            CompoundShape res = null;
            for (int i = 0; i < sortedShapes.Count; i++)
            {   // 227
                SimpleShape sh = sortedShapes[i];
#if DEBUG
                Face dbgface = dict[sh];
                SimpleShape dbgsh = dbgface.GetShadow(onThisPlane);
#endif
                if (sh.Area > 0.0)
                {
                    if (res == null) res = new CompoundShape(sh);
                    else
                    {
                        CompoundShape tmp = CompoundShape.Union(res, new CompoundShape(sh), prec);
                        // die neue Fläche kann ja eigentlich nicht kleiner sein, wenn doch ist es ein Fehler (oder Rundungsgenauigkeit bei identischen Flächen)
                        if (tmp.Area >= res.Area * 0.9999 && tmp.Area <= (res.Area + sh.Area) * 1.0001) res = tmp;
                        else
                        {

                        }
                    }

#if DEBUG
                    if (i < 50)
                    {
                        Path[] pths = sh.MakePaths(Plane.XYPlane);
                        double pathleft = (new GeoObjectList(pths)).GetExtent().Xmin;
                        double left = 0.0;
                        if (dbg.Count > 0) left = dbg.GetExtent().Xmax;
                        ModOp mv = ModOp.Translate(left - pathleft, 0, 0);
                        for (int j = 0; j < pths.Length; j++)
                        {
                            pths[j].Modify(mv);
                            pths[j].ColorDef = cd1;
                            dbg.Add(pths[j]);
                        }
                        pths = res.MakePaths(Plane.XYPlane);
                        pathleft = (new GeoObjectList(pths)).GetExtent().Xmin;
                        left = dbg.GetExtent().Xmax;
                        mv = ModOp.Translate(left - pathleft, 0, 0);
                        for (int j = 0; j < pths.Length; j++)
                        {
                            pths[j].Modify(mv);
                            pths[j].ColorDef = cd2;
                            dbg.Add(pths[j]);
                        }
                        Text text = Text.Construct();
                        text.TextString = i.ToString();
                        text.Font = "Arial";
                        text.Location = new GeoPoint(left, dbg.GetExtent().Ymin - 12, 0);
                        text.LineDirection = GeoVector.XAxis;
                        text.GlyphDirection = GeoVector.YAxis;
                        text.TextSize = 12.0;
                        dbg.Add(text);
                    }
#endif
                }
            }
            if (res != null)
            {   // liefere im Zweifelsfall das größte. Eigentlich sollte es immer nur eines sein
                double maxArea = 0.0;
                int ind = -1;
                bool united = true;
                while (united)
                {
                    united = false;
                    for (int i = 0; i < res.SimpleShapes.Length; i++)
                    {
                        for (int j = i + 1; j < res.SimpleShapes.Length; j++)
                        {
                            CompoundShape cs = SimpleShape.Unite(res.SimpleShapes[i], res.SimpleShapes[j], prec);
                            if (cs.SimpleShapes.Length == 1)
                            {
                                List<SimpleShape> lss = new List<SimpleShape>();
                                for (int k = 0; k < res.SimpleShapes.Length; k++)
                                {
                                    if (k != i && k != j) lss.Add(res.SimpleShapes[k]);
                                }
                                lss.Add(cs.SimpleShapes[0]);
                                res = new CompoundShape(lss.ToArray());
                                united = true;
                                break;
                            }
                        }
                        if (united) break;
                    }
                }
                for (int i = 0; i < res.SimpleShapes.Length; i++)
                {
                    double a = res.SimpleShapes[i].Area;
                    if (a > maxArea)
                    {
                        maxArea = a;
                        ind = i;
                    }
                }
                if (ind >= 0)
                {
                    res.SimpleShapes[ind].Reduce(prec);
                    return res.SimpleShapes[ind];
                }
            }
            return null; // verschwindet in der Projektion, oder Fehler
        }

        public bool SameGeometry(Shell other)
        {
            if (Faces.Length != other.Faces.Length) return false;
            if (Vertices.Length != other.Vertices.Length) return false;
            if (edges.Length != other.Edges.Length) return false;
            // now remains the hard work, which has still to be implemented
            double precision = this.GetExtent(0.0).Size * 1e-3;
            if (Math.Abs(Volume(precision) - other.Volume(precision)) > precision) return false; // this is the lazy version. We would have to at least fit all vertices
            return true;
        }

        public bool TestIntersection(List<ICurve> toTestWith)
        {
            for (int i = 0; i < Faces.Length; i++)
            {
                for (int j = 0; j < toTestWith.Count; j++)
                {
                    Faces[i].Intersect(toTestWith[j], out GeoPoint[] ip, out GeoPoint2D[] uvOnFace, out double[] uOnCurve);
                    for (int k = 0; k < uOnCurve.Length; k++)
                    {
                        if (0 <= uOnCurve[k] && uOnCurve[k] <= 1.0) return true;
                    }
                }
            }
            return false;
        }
#if DEBUG
        public SimpleShape GetShadowX(Plane onThisPlane)
        {
#if DEBUG
            GeoObjectList dbg = new GeoObjectList();
            ColorDef cd1 = new Attribute.ColorDef("cd1", Color.Red);
            ColorDef cd2 = new Attribute.ColorDef("cd2", Color.Blue);
            Dictionary<SimpleShape, Face> dict = new Dictionary<SimpleShape, Face>();
#endif
            BoundingRect ext = BoundingRect.EmptyBoundingRect;
            List<SimpleShape> sortedShapes = new List<SimpleShape>();
            for (int i = 0; i < faces.Length; i++)
            {
                SimpleShape sh = faces[i].GetShadow(onThisPlane);
                if (sh != null)
                {
#if DEBUG
                    dict[sh] = faces[i];
#endif
                    sortedShapes.Add(sh); // einfachste Art das größte zuesrt zu haben
                    ext.MinMax(sh.GetExtent());
                }
            }
            double prec = ext.Size * 1e-5;
            for (int i = sortedShapes.Count - 1; i >= 0; --i)
            {
                SimpleShape sh = sortedShapes[i];
                sh.Reduce(prec);
                if (!sh.Outline.IsClosed || sh.Area < prec * prec) sortedShapes.RemoveAt(i);
            }

            sortedShapes.Sort(new Comparison<SimpleShape>(delegate (SimpleShape s1, SimpleShape s2) { return s2.Area.CompareTo(s1.Area); }));

            // Area ist in den Borders gecached, das größte zuerst

            CompoundShape res = null;
            for (int i = 0; i < sortedShapes.Count; i++)
            {   // 227
                SimpleShape sh = sortedShapes[i];
#if DEBUG
                Face dbgface = dict[sh];
#endif
                if (sh.Area > 0.0)
                {
                    if (res == null) res = new CompoundShape(sh);
                    else res = CompoundShape.UnionX(res, new CompoundShape(sh), prec);

#if DEBUG
                    if (i < 50)
                    {
                        Path[] pths = sh.MakePaths(Plane.XYPlane);
                        double pathleft = (new GeoObjectList(pths)).GetExtent().Xmin;
                        double left = 0.0;
                        if (dbg.Count > 0) left = dbg.GetExtent().Xmax;
                        ModOp mv = ModOp.Translate(left - pathleft, 0, 0);
                        for (int j = 0; j < pths.Length; j++)
                        {
                            pths[j].Modify(mv);
                            pths[j].ColorDef = cd1;
                            dbg.Add(pths[j]);
                        }
                        pths = res.MakePaths(Plane.XYPlane);
                        pathleft = (new GeoObjectList(pths)).GetExtent().Xmin;
                        left = dbg.GetExtent().Xmax;
                        mv = ModOp.Translate(left - pathleft, 0, 0);
                        for (int j = 0; j < pths.Length; j++)
                        {
                            pths[j].Modify(mv);
                            pths[j].ColorDef = cd2;
                            dbg.Add(pths[j]);
                        }
                        Text text = Text.Construct();
                        text.TextString = i.ToString();
                        text.Font = "Arial";
                        text.Location = new GeoPoint(left, dbg.GetExtent().Ymin - 12, 0);
                        text.LineDirection = GeoVector.XAxis;
                        text.GlyphDirection = GeoVector.YAxis;
                        text.TextSize = 12.0;
                        dbg.Add(text);
                    }
#endif
                }
            }
            if (res != null)
            {   // liefere im Zweifelsfall das größte. Eigentlich sollte es immer nur eines sein
                double maxArea = 0.0;
                int ind = -1;
                bool united = true;
                while (united)
                {
                    united = false;
                    for (int i = 0; i < res.SimpleShapes.Length; i++)
                    {
                        for (int j = i + 1; j < res.SimpleShapes.Length; j++)
                        {
                            CompoundShape cs = SimpleShape.Unite(res.SimpleShapes[i], res.SimpleShapes[j], prec);
                            if (cs.SimpleShapes.Length == 1)
                            {
                                List<SimpleShape> lss = new List<SimpleShape>();
                                for (int k = 0; k < res.SimpleShapes.Length; k++)
                                {
                                    if (k != i && k != j) lss.Add(res.SimpleShapes[k]);
                                }
                                lss.Add(cs.SimpleShapes[0]);
                                res = new CompoundShape(lss.ToArray());
                                united = true;
                                break;
                            }
                        }
                        if (united) break;
                    }
                }
                for (int i = 0; i < res.SimpleShapes.Length; i++)
                {
                    double a = res.SimpleShapes[i].Area;
                    if (a > maxArea)
                    {
                        maxArea = a;
                        ind = i;
                    }
                }
                if (ind >= 0)
                {
                    res.SimpleShapes[ind].Reduce(prec);
                    return res.SimpleShapes[ind];
                }
            }
            return null; // verschwindet in der Projektion, oder Fehler
        }
#endif

        #region IGeoObject Members
        /// <summary>
        /// Overrides <see cref="CADability.GeoObject.IGeoObjectImpl.Modify (ModOp)"/>
        /// </summary>
        /// <param name="m"></param>
        public override void Modify(ModOp m)
        {   // the surfaces of the faces get modified as well as the 3d curves of the edges and the vertex positions
            using (new Changing(this, "ModifyInverse", m))
            {
                for (int i = 0; i < faces.Length; ++i)
                {
                    faces[i].ModifySurface(m);
                }
                // Edges groß, damit sie sicher existieren
                for (int i = 0; i < Edges.Length; ++i)
                {
                    IGeoObject go = edges[i].Curve3D as IGeoObject;
                    if (go != null) go.Modify(m);
                    edges[i].SetVerticesPositions();
                    edges[i].ReflectModification(); // ProjectedCurves must be updated
                }
            }
        }

        internal bool Simplify(double precision)
        {
            bool simplified = false;
            for (int i = 0; i < faces.Length; i++)
            {
                simplified |= faces[i].Simplify(precision);
            }
            return simplified;
        }
        /// <summary>
        /// Clones the shell and fills the dictionaries with original to cloned references
        /// </summary>
        /// <param name="clonedEdges"></param>
        /// <param name="clonedVertices"></param>
        /// <param name="clonedFaces"></param>
        /// <returns></returns>
        internal Shell Clone(Dictionary<Edge, Edge> clonedEdges, Dictionary<Vertex, Vertex> clonedVertices = null, Dictionary<Face, Face> clonedFaces = null)
        {   // hier kann es sich um ein unabhängiges oder um ein von einem Solid abhängiges Shell handeln.
            // im letzteren Fall bleiben die edges undefiniert
            if (clonedVertices == null) clonedVertices = new Dictionary<Vertex, Vertex>();
            if (clonedFaces == null && parametricProperties != null) clonedFaces = new Dictionary<Face, Face>();
            Shell res = Shell.Construct();
            res.CopyAttributes(this); // damit die Faces ihre Attribute beibehalten
            res.Name = Name;
            res.faces = new Face[faces.Length];
            for (int i = 0; i < faces.Length; ++i)
            {
                res.faces[i] = faces[i].Clone(clonedEdges, clonedVertices);
                res.faces[i].Owner = res;
                if (clonedFaces != null) clonedFaces[faces[i]] = res.faces[i];
            }
            res.State = State;
#if DEBUG
            bool ok = res.CheckConsistency();
#endif
            if (parametricProperties != null)
            {
                for (int i = 0; i < parametricProperties.Count; i++)
                {
                    res.AddParametricProperty(parametricProperties[i].Clone(clonedFaces, clonedEdges, clonedVertices));
                }
            }
            return res;
        }
        internal List<Face> ClonePart(IEnumerable<Face> toClone)
        {
            Dictionary<Edge, Edge> clonedEdges = new Dictionary<Edge, Edge>();
            Dictionary<Vertex, Vertex> clonedVertices = new Dictionary<Vertex, Vertex>();
            List<Face> res = new List<Face>();
            foreach (Face face in toClone)
            {
                Face clone = face.Clone(clonedEdges, clonedVertices);
                res.Add(clone);
            }
            return res;
        }
        /// <summary>
        /// Overrides <see cref="CADability.GeoObject.IGeoObjectImpl.Clone ()"/>
        /// </summary>
        /// <returns></returns>
        public override IGeoObject Clone()
        {   // ein unabhängiges Shell wird gekloned
            Dictionary<Edge, Edge> clonedEdges = new Dictionary<Edge, Edge>();

            Shell res = Clone(clonedEdges); // dort findet auch CopyAttributes statt, damit die Faces ihre Farbe beibehalten

            res.edges = new Edge[clonedEdges.Values.Count];
            clonedEdges.Values.CopyTo(res.edges, 0);
            res.orientedAndSeamless = orientedAndSeamless;
            res.State = State;

            return res;
        }
        /// <summary>
        /// Overrides <see cref="CADability.GeoObject.IGeoObjectImpl.CopyGeometry (IGeoObject)"/>
        /// </summary>
        /// <param name="toCopyFrom"></param>
        public override void CopyGeometry(IGeoObject toCopyFrom)
        {
            Shell cc = toCopyFrom as Shell;
            //using (new Changing(this))
            //{
            //    CopyGeometryNoEdges(cc);
            //    for (int i = 0; i < Edges.Length; ++i)
            //    {
            //        edges[i].RecalcCurve3D();
            //    }
            //}
            // we expect a totally synchronous face "toCopyFrom"
            using (new Changing(this))
            {
                HashSet<Edge> usedEdges = new HashSet<Edge>();
                for (int i = 0; i < faces.Length; ++i)
                {
                    faces[i].Surface.CopyData(cc.Faces[i].Surface);
                    for (int j = 0; j < faces[i].AllEdges.Length; j++)
                    {
                        Edge edg = faces[i].AllEdges[j];
                        if (!usedEdges.Contains(edg))
                        {
                            usedEdges.Add(edg);
                            edg.CopyGeometry(cc.faces[i].AllEdges[j]);
                        }
                    }
                    faces[i].InvalidateSecondaryData();
                }
            }
        }
        internal void CopyGeometryNoEdges(Shell toCopyFrom)
        {
            for (int i = 0; i < faces.Length; ++i)
            {
                faces[i].CopyGeometryNoEdges(toCopyFrom.faces[i]);
            }
        }
        /// <summary>
        /// Overrides <see cref="CADability.GeoObject.IGeoObjectImpl.GetShowProperties (IFrame)"/>
        /// </summary>
        /// <param name="Frame"></param>
        /// <returns></returns>
        public override IPropertyEntry GetShowProperties(IFrame Frame)
        {
            return new ShowPropertyShell(this, Frame);
        }
        /// <summary>
        /// Overrides <see cref="CADability.GeoObject.IGeoObjectImpl.FindSnapPoint (SnapPointFinder)"/>
        /// </summary>
        /// <param name="spf"></param>
        public override void FindSnapPoint(SnapPointFinder spf)
        {
            if (!spf.Accept(this)) return;
            for (int i = 0; i < Edges.Length; ++i)
            {
                IGeoObject go = Edges[i].Curve3D as IGeoObject;
                if (go != null)
                {
                    go.FindSnapPoint(spf);
                }
            }
            if (spf.SnapToFaceSurface)
            {
                for (int i = 0; i < faces.Length; ++i)
                {
                    faces[i].FindSnapPoint(spf);
                }
            }
        }
        /// <summary>
        /// Overrides <see cref="CADability.GeoObject.IGeoObjectImpl.GetBoundingCube ()"/>
        /// </summary>
        /// <returns></returns>
        public override BoundingCube GetBoundingCube()
        {
            BoundingCube res = BoundingCube.EmptyBoundingCube;
            for (int i = 0; i < faces.Length; ++i)
            {
                if (faces[i].Surface is NurbsSurface)
                {
                    bool totallyInside = true;
                    NurbsSurface ns = faces[i].Surface as NurbsSurface;
                    for (int j = 0; j < ns.Poles.GetLength(0); j++)
                    {
                        for (int k = 0; k < ns.Poles.GetLength(1); k++)
                        {
                            if (!res.Contains(ns.Poles[j, k]))
                            {
                                totallyInside = false;
                                break;
                            }
                        }
                        if (!totallyInside) break;
                    }
                    if (totallyInside) continue;
                }
                res.MinMax(faces[i].GetBoundingCube());
            }
            return res;
        }

        private static double findCylinder(GeoPoint[] points, GeoVector[] normals, out GeoPoint axisPoint, out GeoVector axisDir, out double radius)
        {
            // points must be a multipe of 3, each triple describes a triangle, for each triangle there is a normal
            // typically there is one side of the triangles, which is parallel to the axis. So let us try to find parallel triengla edges
            throw new NotImplementedException();

            //Unreachable code
            /*
            int nTriple = points.Length / 3;
            Matrix m = new DenseMatrix(nTriple + 1, 3);
            Vector b = new DenseVector(nTriple + 1);
            for (int i = 0; i < points.Length; i += 3)
            {
                GeoVector n = ((points[i + 1] - points[i]) ^ (points[i + 2] - points[i])).Normalized;
                m[i, 0] = n.x;
                m[i, 1] = n.y;
                m[i, 2] = n.z;
                b[i] = 0;
            }
            m[nTriple, 0] = m[nTriple, 0] = m[nTriple, 0] = 1;
            b[nTriple] = 1;
            QR<double> qrd = m.QR();
            if (qrd.IsFullRank)
            {
                Vector x = (Vector)qrd.Solve(b);
                if (x.IsValid())
                {
                    axisDir = new GeoVector(x[0], x[1], x[2]);
                    Plane pln = new Plane(GeoPoint.Origin, axisDir);
                    GeoPoint2D[] points2d = new GeoPoint2D[points.Length];
                    for (int i = 0; i < points.Length; i++)
                    {
                        points2d[i] = pln.Project(points[i]);
                    }
                    double err = Geometry.CircleFitLs(points2d, out GeoPoint2D c2d, out radius);
                    axisPoint = pln.ToGlobal(c2d);
                    double maxerr = 0.0;
                    for (int i = 0; i < points.Length; i++)
                    {
                        double d = Geometry.DistPL(points[i], axisPoint, axisDir);
                        if (Math.Abs(d - radius) > maxerr) maxerr = Math.Abs(d - radius);
                    }
                    return maxerr;
                }
            }
            */
        }
        
#if DEBUG
        public
#else
        private
#endif
        bool ReconstructSurfaces(double precision)
        {   // STL: Dreiecke in Flächen verwandeln ...
            double maxbend = 10.0 / 180 * Math.PI; // two connected faces which are inclined by less than 10° are checked to contain to the same surface
            RecalcVertices();
            foreach (Face fc in Faces)
            {
                if (fc.OutlineEdges.Length > 4 || !(fc.Surface is PlaneSurface)) return false;
            }
            //Shell sh = this.Clone() as Shell;
            //ModOp m = ModOp.Rotate(GeoVector.XAxis, SweepAngle.Deg(30)) * ModOp.Rotate(GeoVector.ZAxis, SweepAngle.Deg(15)); // schief machen zum Debuggen
            //sh.Modify(m);
            //sh.RecalcVertices();
            Set<Face> allFaces = new Set<Face>(Faces);
            List<Set<Face>> connected = new List<Set<Face>>();
            int clrdbg = 1;
            Random rnd = new Random();

            while (allFaces.Count > 0)
            {
                Face fc = allFaces.GetAny();
                allFaces.Remove(fc);
                Set<Face> c = CollectFaces(fc, allFaces, maxbend);
                connected.Add(c);

                Color clr = Color.FromArgb((int)(rnd.NextDouble() * 255), (int)(rnd.NextDouble() * 255), (int)(rnd.NextDouble() * 255));
                clrdbg += 1;
                ColorDef cd = new ColorDef(clr.ToString(), clr);
                foreach (Face fcc in c)
                {
                    fcc.ColorDef = cd;
                }
            }
            List<Face> created = new List<Face>();
            foreach (Set<Face> faces in connected)
            {
                List<Face> fccon = FindCommonSurfaceFaces(faces, null, this.GetExtent(0.0), new Set<Type>(), precision);
                created.AddRange(fccon);
            }
#if DEBUG
            GeoObjectList dbgCreated = new GeoObjectList();
            foreach (Face fc in created)
            {
                dbgCreated.Add(fc.Clone());
            }
#endif

            return false;

            //Unreachable code
            /*
            Set<Face> usedFaces = new Set<Face>();
            List<Face> createdFaces = new List<Face>();
            Dictionary<Edge, Edge> newEdges = new Dictionary<Edge, Edge>();
            foreach (Edge edg in Edges)
            {
                if (usedFaces.Contains(edg.PrimaryFace) || usedFaces.Contains(edg.SecondaryFace)) continue;
                GeoVector n1 = (edg.PrimaryFace.Surface as PlaneSurface).Normal;
                GeoVector n2 = (edg.SecondaryFace.Surface as PlaneSurface).Normal;
                SweepAngle sw = new SweepAngle(n1, n2);
                if (Math.Abs(sw.Radian) < maxbend)
                {
                    if (Math.Abs(sw.Radian) < 1e-7)
                    {
                        // the two faces are in a common plane
                        Face fc = CollectPlanarFaces(edg, usedFaces, newEdges);
                        if (fc != null) createdFaces.Add(fc);
                    }
                }
            }
            return false;
            */
        }

        private class VectorInOctTree : IOctTreeInsertable
        {
            public GeoPoint v;
            public object o;
            public VectorInOctTree(GeoPoint p, object o)
            {
                this.v = p;
                this.o = o;
            }
            public VectorInOctTree(GeoVector v, object o)
            {
                this.v = GeoPoint.Origin + v.Normalized;
                this.o = o;
            }

            BoundingCube IOctTreeInsertable.GetExtent(double precision)
            {
                return new BoundingCube(v, precision);
            }

            bool IOctTreeInsertable.HitTest(ref BoundingCube cube, double precision)
            {
                return cube.Contains(v);
            }

            bool IOctTreeInsertable.HitTest(Projection projection, BoundingRect rect, bool onlyInside)
            {
                throw new NotImplementedException();
            }

            bool IOctTreeInsertable.HitTest(Projection.PickArea area, bool onlyInside)
            {
                throw new NotImplementedException();
            }

            double IOctTreeInsertable.Position(GeoPoint fromHere, GeoVector direction, double precision)
            {
                throw new NotImplementedException();
            }
        }
        private class PointInQuadTree : IQuadTreeInsertable
        {
            GeoPoint2D p;
            Face fc;
            public PointInQuadTree(GeoPoint2D p, Face fc)
            {
                this.p = p;
                this.fc = fc;
            }

            object IQuadTreeInsertable.ReferencedObject => fc;

            BoundingRect IQuadTreeInsertable.GetExtent()
            {
                return new BoundingRect(p);
            }

            bool IQuadTreeInsertable.HitTest(ref BoundingRect rect, bool includeControlPoints)
            {
                return rect.Contains(p);
            }
        }
        private List<Face> FindCommonSurfaceFaces(Set<Face> faces, Set<Face> subsetForTest, BoundingCube extent, Set<Type> dontCheck, double precision)
        {
            if (dontCheck == null) dontCheck = new Set<Type>(); // empty set
            if (subsetForTest == null) subsetForTest = faces; // use all faces for the test
#if DEBUG
            // faces is connected via edges and verteces with the whole shell, which might be huge (stl import). To show faces in the DebuggerContainer
            // we individually clone them to get rid of these connections
            GeoObjectList dbgfaces = new GeoObjectList();
            HashSet<Vertex> dbgvtx = new HashSet<Vertex>();
            HashSet<Edge> dbgedg = new HashSet<Edge>();
            BoundingCube dbgext = BoundingCube.EmptyBoundingCube;
            foreach (Face fc in subsetForTest)
            {
                dbgfaces.Add(fc.Clone());
                foreach (Vertex vertex in fc.Vertices)
                {
                    dbgvtx.Add(vertex);
                    dbgext.MinMax(vertex.Position);
                }
                foreach (Edge edg in fc.Edges)
                {
                    dbgedg.Add(edg);
                }
            }
            //EdgeCloudToPlanes ep = new EdgeCloudToPlanes(dbgedg, dbgext);
            //foreach (HashSet<Edge> item in ep.GetBestPlanes())
            //{
            //    List<Edge> dbglvtx = new List<Edge>(item);
            //}
            //VertexCloudToPlanes vp = new VertexCloudToPlanes(dbgvtx, dbgext);
            //foreach (HashSet<Vertex> item in vp.GetBestPlanes())
            //{
            //    List<Vertex> dbglvtx = new List<Vertex>(item);
            //}
#endif
            // "faces" contains planar triangles with almost tangential connections. There may be one or more common standard surfaces for these triangles.
            // If there are more surfaces, this algoritm first tries to elemenate the easy ones and recursively calls this method to proceed with the more difficult ones.
            // We test for PlanarSurface, CylindricalSurface, ConicalSurface, SphereicalSurface and ToriodalSurface
            Dictionary<Edge, Edge> newEdges = new Dictionary<Edge, Edge>();
            List<Face> res = new List<Face>();
            OctTree<VectorInOctTree> normals = new OctTree<VectorInOctTree>(new BoundingCube(new GeoPoint(1e-5, 1e-5, 1e-5), 1.1), 1e-6); // octtree around the unit cube, 
                                                                                                                                          // to collect the normals of all faces. This help to classify the faces
                                                                                                                                          // a little bit excentric to collect vetors close to the unit (axis, e.g.: (0,0,1)) in a single OctTree box.
            List<GeoPoint> ndirs = new List<GeoPoint>(); // all normals (on the unit sphere) to check, whether there is a cylinder or cone
            BoundingCube next = BoundingCube.EmptyBoundingCube;
            foreach (Face fc in subsetForTest)
            {
                GeoPoint pus = GeoPoint.Origin + (fc.Surface as PlaneSurface).Normal.Normalized; // normal as a point on the unit sphere
                normals.AddObject(new VectorInOctTree(pus, fc));
                ndirs.Add(pus);
                next.MinMax(pus);
            }

            Plane cylConeTest = Plane.FromPoints(ndirs.ToArray(), out double maxNDist, out bool isNLinear);
            // if faces contains only a cylinder or cone and not multiple surfaces, the normals build a circle or arc on this plane
            if (!isNLinear && next.Size > 0.1 && maxNDist < 1e-3)
            {
                HashSet<Vertex> usedVertices = new HashSet<Vertex>();
                List<GeoPoint> allPoints = new List<GeoPoint>();
                foreach (Face fc in subsetForTest)
                {
                    GeoPoint pus = GeoPoint.Origin + (fc.Surface as PlaneSurface).Normal.Normalized; // normal as a point on the unit sphere
                    normals.AddObject(new VectorInOctTree(pus, fc));
                    ndirs.Add(pus);
                    next.MinMax(pus);
                    for (int i = 0; i < fc.Vertices.Length; i++)
                    {
                        if (!usedVertices.Add(fc.Vertices[i])) allPoints.Add(fc.Vertices[i].Position);
                    }
                }

                // it is a good guess that we might have a cylinder or cone (or some extruded curve or some conical extruded curve)
                if (cylConeTest.Distance(GeoPoint.Origin) < 1e-3)
                {
                    // this could ba a cylinder
                    GeoPoint2D[] cyldirs = new GeoPoint2D[ndirs.Count];
                    for (int i = 0; i < ndirs.Count; i++)
                    {
                        cyldirs[i] = cylConeTest.Project(ndirs[i]);
                    }
                    Set<Vertex> vtcs = new Set<Vertex>();
                    foreach (Face fc in subsetForTest)
                    {
                        vtcs.AddMany(fc.Vertices);
                    }
                    List<GeoPoint2D> cylPoints = new List<GeoPoint2D>();
                    foreach (Vertex vtx in vtcs)
                    {
                        cylPoints.Add(cylConeTest.Project(vtx.Position));
                    }
                    double prec = Geometry.CircleFitLs(cylPoints.ToArray(), out GeoPoint2D cnt2d, out double r);
                    double maxError = GaussNewtonMinimizer.CylinderFit(new ListToIArray<GeoPoint>(allPoints), cylConeTest.ToGlobal(cnt2d), cylConeTest.Normal, r, precision * 100, out CylindricalSurface gncs);
                    maxError = GaussNewtonMinimizer.QuadricFit(new ListToIArray<GeoPoint>(allPoints));
                    if (prec < extent.Size * 1e-3)
                    {
                        prec = Geometry.LineFit(cylPoints.ToArray(), out GeoPoint2D mloc, out GeoVector2D mdir); // this line is the main direction of the cylinder points
                                                                                                                 // if prec is in the size of r, we have a full cylinder, if it is less than a half circle, all faces are on one side
                        GeoPoint center = cylConeTest.ToGlobal(cnt2d);
                        CylindricalSurface cs = new CylindricalSurface(center, r * cylConeTest.DirectionX, r * cylConeTest.DirectionY, cylConeTest.Normal);
                        // split the faces into two parts to make sure, there will be no seam
                        Set<Face> part1 = new Set<Face>();
                        Set<Face> part2 = new Set<Face>();
                        Set<Face> subsetPart1 = new Set<Face>();
                        Set<Face> subsetPart2 = new Set<Face>();
                        foreach (Face fc in faces) // use all faces and split them in two parts: left and right of a plane going through the cylinder axis
                        {
                            bool onLeftSide = true;
                            foreach (Vertex vtx in fc.Vertices)
                            {
                                onLeftSide &= Geometry.OnLeftSide(cylConeTest.Project(vtx.Position), cnt2d, mdir);
                            }
                            if (onLeftSide) part1.Add(fc);
                            else part2.Add(fc);
                        }
                        if (subsetForTest != faces)
                        {
                            subsetPart1 = part1.Intersection(subsetForTest);
                            subsetPart2 = part2.Intersection(subsetForTest);
                        }
                        else
                        {
                            subsetPart1 = part1;
                            subsetPart2 = part2;
                        }
                        while (!subsetPart1.IsEmpty())
                        {
                            Face created = CreateFaceOnCommonSurface(cs.Clone(), subsetPart1.GetAny(), part1, newEdges, out Set<Face> usedFaces, this.GetExtent(0.0).Size * 1e-6);
                            if (created != null)
                            {
                                res.Add(created);
                                part1.RemoveMany(usedFaces);
                                subsetPart1.RemoveMany(usedFaces);
                                faces.RemoveMany(usedFaces);
                            }
                            else continue;
                        }
                        while (!subsetPart2.IsEmpty())
                        {
                            Face created = CreateFaceOnCommonSurface(cs.Clone(), subsetPart2.GetAny(), part2, newEdges, out Set<Face> usedFaces, this.GetExtent(0.0).Size * 1e-6);
                            if (created != null)
                            {
                                res.Add(created);
                                part2.RemoveMany(usedFaces);
                                subsetPart2.RemoveMany(usedFaces);
                                faces.RemoveMany(usedFaces);
                            }
                            else continue;
                        }
                        return res;
                    }
                }
            }
            // now we will test for planes conatined in faces. there might be multiple planar faces, which then will be removed from the set
            // and with the remaining objects we well reiterate this process
            if (!dontCheck.Contains(typeof(PlaneSurface)))
            {
                Set<Face> copyOfFaces;
                if (subsetForTest != faces)
                {
                    copyOfFaces = new Set<Face>(subsetForTest);
                }
                else
                {
                    copyOfFaces = new Set<Face>(faces);
                }
                while (!copyOfFaces.IsEmpty())
                {
                    Face startWith = copyOfFaces.GetAny();
                    Set<Face> c = CollectFaces(startWith, copyOfFaces, 0.01 / 180 * Math.PI); // hardcoded 0.01° precision for planar surfaces?
                    if (c.Count > 3 || (subsetForTest != faces && c.Count > 1)) // more than two triangles: cylindrical and conical faces often consist of pairs of coplanar triangles
                    {
                        Set<Vertex> vtcs = new Set<Vertex>();
                        foreach (Face fc in c)
                        {
                            vtcs.AddMany(fc.Vertices);
                        }
                        List<GeoPoint> pnts = new List<GeoPoint>(vtcs.Count);
                        foreach (Vertex vtx in vtcs)
                        {
                            pnts.Add(vtx.Position);
                        }
                        Plane pln = Plane.FromPoints(pnts.ToArray(), out double maxDist, out bool isLinear);
                        BoundingCube pntsExt = new BoundingCube(pnts.ToArray()); // as a meassure for precision
                        bool ok;
                        if (pnts.Count < 6)
                        {
                            // only two or three triangles
                            ok = maxDist < pntsExt.Size * 1e-6; // must be more precise, because only a few trianges, which could also come from a smooth approximation of some other surface
                        }
                        else
                        {   // more triangles connected
                            ok = maxDist < pntsExt.Size * 1e-5;
                        }
                        if (!isLinear && ok)
                        {
                            PlaneSurface ps = new PlaneSurface(pln);
                            Face created = CreateFaceOnCommonSurface(ps, startWith, faces, newEdges, out Set<Face> usedFaces, this.GetExtent(0.0).Size * 1e-6);
                            faces.RemoveMany(usedFaces);
                            res.Add(created);
                        }
                    }
                }
#if DEBUG
                GeoObjectList dbgres = new GeoObjectList();
                for (int i = 0; i < res.Count; i++)
                {
                    dbgres.Add(res[i].Clone());
                }
#endif
                if (res.Count > 0)
                {
                    // we have used some triangles from "faces" to build planar faces. Now we repeat the whole process
                    // with the remaining unused faces.
                    dontCheck.Add(typeof(PlaneSurface)); // dont check for planes any more
                    while (faces.Count > 0)
                    {
                        Set<Face> facesSubSet = CollectFaces(faces.GetAny(), faces, 10.0 / 180 * Math.PI);
                        res.AddRange(FindCommonSurfaceFaces(facesSubSet, null, extent, dontCheck, precision));
                    }
                    return res;
                }
            }

            // Torus/sphere test
            if (subsetForTest.Count > 4)
            {   // the inner parts of sphere/torus stl triangulation usually include the longitude/latitude curves.
                // This is not the case at the border and of course all triangles could be at the border
                // Here we try to find longitude/latitude curves. 

                // First filter all edges at close to 90° triangles
                Set<Edge> rectangularEdges = new Set<Edge>();
                foreach (Face fc in subsetForTest)
                {
                    double minDiff90 = double.MaxValue;
                    int besti = -1;
                    for (int i = 0; i < fc.OutlineEdges.Length; i++)
                    {
                        int j = (i + 1) % fc.OutlineEdges.Length; // which is of course 3
                        GeoVector endDir, startDir; // where the edges meet
                        if (fc.OutlineEdges[i].Forward(fc)) endDir = fc.OutlineEdges[i].Curve3D.EndDirection;
                        else endDir = -fc.OutlineEdges[i].Curve3D.StartDirection;
                        if (fc.OutlineEdges[j].Forward(fc)) startDir = fc.OutlineEdges[j].Curve3D.StartDirection;
                        else startDir = -fc.OutlineEdges[j].Curve3D.EndDirection;
                        Angle a = new Angle(endDir, startDir);
                        double diff90 = Math.Abs(a.Radian - Math.PI / 2.0);
                        if (diff90 < 0.3 && diff90 < minDiff90) // ca. 60° .. 120°, experimental
                        {
                            minDiff90 = diff90;
                            besti = i;
                        }
                    }
                    if (besti >= 0)
                    {
                        rectangularEdges.Add(fc.OutlineEdges[besti]);
                        rectangularEdges.Add(fc.OutlineEdges[(besti + 1) % fc.OutlineEdges.Length]);
                    }
                }
                // then try to connect them to almost straight curves
                List<List<Vertex>> orthoCurves = new List<List<Vertex>>();
                while (!rectangularEdges.IsEmpty())
                {
                    Edge startWith = rectangularEdges.GetAndRemoveAny();
                    List<Vertex> orthoCurve = CollectConnectedEdges(startWith, rectangularEdges, Math.PI / 4.0);
                    if (orthoCurve.Count >= 3) orthoCurves.Add(orthoCurve);
                }
                // these cures are now longitude or latitude curves
                // longitudes should have about the same radius, latitudes about the same axis
                // if there are multiple surfaces like torus and spheres, it is difficult to tell them apart, even when we can tell to which vertices each curve belongs:
                // toruses and spheres may be connected
#if DEBUG
                GeoObjectList dbgOrthoCurves = new GeoObjectList();
                for (int i = 0; i < orthoCurves.Count; i++)
                {
                    List<GeoPoint> pnts = new List<GeoPoint>();
                    for (int j = 0; j < orthoCurves[i].Count; j++)
                    {
                        pnts.Add(orthoCurves[i][j].Position);
                    }
                    Polyline poln = Polyline.Construct();
                    poln.SetPoints(pnts.ToArray(), false);
                    dbgOrthoCurves.Add(poln);
                }
#endif
                // now we try to find circles (circular arcs) in the orthoCurves:
                // if the (reconstructed) model consists of planar and cylindrical faces and rounded edges (e.g. a cuboid with a round hole and all edges rounded)
                // the we typically have long orthoCurves combining circular parts and linear parts.
                List<Tripel<double, GeoPoint, GeoVector>> circles = new List<Tripel<double, GeoPoint, GeoVector>>();
                List<Vertex[]> circleVertices = new List<Vertex[]>();
                for (int i = 0; i < orthoCurves.Count; i++)
                {
                    for (int j = 0; j < orthoCurves[i].Count - 3; j++)
                    {
                        List<GeoPoint> pnts = new List<GeoPoint>(); // the next 4 points
                        for (int k = 0; k < 4; k++)
                        {
                            pnts.Add(orthoCurves[i][j + k].Position);
                        }
                        Plane pln = Plane.FromPoints(pnts.ToArray(), out double maxDistance, out bool isLinear);
                        if (!isLinear && maxDistance < extent.Size * 1e-5)
                        {
                            GeoPoint2D[] pnts2d = pln.Project(pnts.ToArray());
                            double prec = Geometry.CircleFitLs(pnts2d, out GeoPoint2D cnt2d, out double r);
                            while (prec < extent.Size * 1e-4 && orthoCurves[i].Count > j + pnts.Count)
                            {
                                pnts.Add(orthoCurves[i][j + pnts.Count].Position);
                                pln = Plane.FromPoints(pnts.ToArray(), out maxDistance, out isLinear);
                                if (maxDistance > extent.Size * 1e-5)
                                {
                                    pnts.RemoveAt(pnts.Count - 1);
                                    break;
                                }
                                pnts2d = pln.Project(pnts.ToArray());
                                prec = Geometry.CircleFitLs(pnts2d, out cnt2d, out r);
                                if (prec > extent.Size * 1e-4)
                                {
                                    pnts.RemoveAt(pnts.Count - 1);
                                    break;
                                }
                            }
                            if (pnts.Count > 4)
                            {
                                pln = Plane.FromPoints(pnts.ToArray(), out maxDistance, out isLinear);
                                pnts2d = pln.Project(pnts.ToArray());
                                prec = Geometry.CircleFitLs(pnts2d, out cnt2d, out r);
                                circles.Add(new Tripel<double, GeoPoint, GeoVector>(r, pln.ToGlobal(cnt2d), pln.Normal));
                                Vertex[] usedvertices = new Vertex[pnts.Count];
                                orthoCurves[i].CopyTo(j, usedvertices, 0, pnts.Count);
                                circleVertices.Add(usedvertices);
                                j += pnts.Count;
                            }
                        }
                    }
                }

                List<List<int>> circleCluster = new List<List<int>>();
                Set<int> usedCircles = new Set<int>();
                while (usedCircles.Count < circleVertices.Count)
                {
                    int ind = -1;
                    for (int i = 0; i < circleVertices.Count; i++)
                    {
                        if (!usedCircles.Contains(i))
                        {
                            ind = i;
                            break;
                        }
                    }
                    usedCircles.Add(ind);
                    Set<Vertex> currentVtx = new Set<Vertex>();
                    currentVtx = new Set<Vertex>(circleVertices[ind]);
                    circleCluster.Add(new List<int>());
                    circleCluster[circleCluster.Count - 1].Add(ind);
                    for (int i = 0; i < circleVertices.Count; i++)
                    {
                        if (usedCircles.Contains(i)) continue;
                        if (currentVtx.IsDisjointFrom(new Set<Vertex>(circleVertices[i]))) continue;
                        usedCircles.Add(i);
                        currentVtx.AddMany(circleVertices[i]);
                        circleCluster[circleCluster.Count - 1].Add(i);
                    }
                }
#if DEBUG
                GeoObjectList dbgcircles = new GeoObjectList();
                foreach (Tripel<double, GeoPoint, GeoVector> circle in circles)
                {
                    Ellipse e = Ellipse.Construct();
                    e.SetCirclePlaneCenterRadius(new Plane(circle.Second, circle.Third), circle.Second, circle.First);
                    // dbgcircles.Add(e);
                }
                Random rnd = new Random();
                foreach (List<int> item in circleCluster)
                {
                    Color clr = Color.FromArgb((int)(rnd.NextDouble() * 255), (int)(rnd.NextDouble() * 255), (int)(rnd.NextDouble() * 255));
                    ColorDef cd = new ColorDef(clr.ToString(), clr);
                    for (int i = 0; i < item.Count; i++)
                    {
                        Ellipse e = Ellipse.Construct();
                        e.SetCirclePlaneCenterRadius(new Plane(circles[item[i]].Second, circles[item[i]].Third), circles[item[i]].Second, circles[item[i]].First);
                        e.ColorDef = cd;
                        dbgcircles.Add(e);
                    }
                }
#endif

                if (circles.Count > 2)
                {
                    circles.Sort(delegate (Tripel<double, GeoPoint, GeoVector> c1, Tripel<double, GeoPoint, GeoVector> c2) { return c1.First.CompareTo(c2.First); });
                    // sort by radius. Now youn dont know which kind is smaller, but longitudes should have almost the same radius, although two latitudes could also have the same radius
                    double eps = circles[0].First * 1e-4; // some precision guess
                    int lower = -1;
                    int upper = -1;
                    for (int i = 0; i < circles.Count - 1; i++)
                    {
                        if (circles[i + 1].First - circles[i].First < eps)
                        {
                            if (!Precision.SameDirection(circles[i + 1].Third, circles[i].Third, true)) // not two latitudes
                            {
                                if (lower == -1) lower = i;
                                if (circles[i + 1].First - circles[lower].First < eps) upper = i + 1;
                            }
                        }
                    }
                    // between lower and (including) upper there are the longitudes, all others should be latitudes
                    if (lower >= 0 && upper >= 0)
                    {
                        bool ok = true;
                        GeoVector axisDir = GeoVector.NullVector;
                        GeoPoint axisPoint = GeoPoint.Invalid;
                        if (lower == 0 && upper < circles.Count - 1)
                        {
                            axisDir = circles[upper + 1].Third;
                            axisPoint = circles[upper + 1].Second;
                        }
                        else if (lower > 0)
                        {
                            axisDir = circles[0].Third;
                            axisPoint = circles[upper + 1].Second;
                        }
                        else ok = false;
                        // the latitudes must have the same normals
                        for (int i = 0; i < circles.Count - 1; i++)
                        {
                            if (!ok) break;
                            if (i < lower || i > upper) ok = Precision.SameDirection(axisDir, circles[i].Third, true);
                        }
                        // the normals of the longitudes must be perpendicular to the normals of the latitudes
                        for (int i = lower; i <= upper; i++)
                        {
                            if (!ok) break;
                            double d = axisDir * circles[i].Third;
                            ok = Math.Abs(d) < 1e-3;
                        }
                        if (ok)
                        {
                            axisPoint = Geometry.LinePos(axisPoint, axisDir, circles[lower].Second); // center of an arbitrary longitued projected on the axis
                            double R = axisPoint | circles[lower].Second;
                            ISurface surface;
                            Plane axisPlane = new Plane(axisPoint, axisDir);
                            if (R < eps)
                            {
                                surface = new SphericalSurface(axisPoint, circles[lower].First * axisPlane.DirectionX, circles[lower].First * axisPlane.DirectionY, axisDir);
                            }
                            else
                            {
                                surface = new ToroidalSurface(axisPoint, axisPlane.DirectionX, axisPlane.DirectionY, axisDir, R, circles[lower].First);
                            }
                            Face created = CreateFaceOnCommonSurface(surface, subsetForTest.GetAny(), faces, newEdges, out Set<Face> usedFaces, this.GetExtent(0.0).Size * 1e-4);
                            faces.RemoveMany(usedFaces);
                            res.Add(created);
                            return res;
                        }
                    }
                    GeoObjectList dbgc = new GeoObjectList();
                    for (int i = 0; i < circles.Count; i++)
                    {
                        Ellipse e = Ellipse.Construct();
                        e.SetCirclePlaneCenterRadius(new Plane(circles[i].Second, circles[i].Third), circles[i].Second, circles[i].First);
                        dbgc.Add(e);
                    }
                }
            }
            if (faces.Count > 100)
            {
                // probably a mix of tangentially connected surfaces.
                // try to categorize the triangles and use similar triangles as a subset
                List<Pair<double, Face>> areas = new List<Pair<double, Face>>();
                List<Pair<double, Face>> ratios = new List<Pair<double, Face>>();
                foreach (Face fc in faces)
                {
                    GeoPoint pus = GeoPoint.Origin + (fc.Surface as PlaneSurface).Normal.Normalized;

                    if (fc.Vertices.Length == 3)
                    {
                        double circ = 0;
                        for (int i = 0; i < 3; i++)
                        {
                            int j = (i + 1) % 3;
                            double d = fc.Vertices[i].Position | fc.Vertices[j].Position;
                            circ += d;
                        }
                        double area = ((fc.Vertices[1].Position - fc.Vertices[0].Position) ^ (fc.Vertices[2].Position - fc.Vertices[0].Position)).Length / 2;
                        areas.Add(new Pair<double, Face>(area, fc));
                        ratios.Add(new Pair<double, Face>(area / circ, fc));
                    }
                    else
                    {
                        // should not happen
                    }
                }
                areas.Sort(delegate (Pair<double, Face> p1, Pair<double, Face> p2) { return p1.First.CompareTo(p2.First); });
                ratios.Sort(delegate (Pair<double, Face> p1, Pair<double, Face> p2) { return p1.First.CompareTo(p2.First); });
                // search for the smalles 1% intervall of the ratios
                int intv = ratios.Count / 100;
                do
                {
                    double mindist = double.MaxValue;
                    int besti = -1;
                    for (int i = intv; i < ratios.Count; i++)
                    {
                        double d = (ratios[i].First - ratios[i - intv].First);
                        if (d < mindist)
                        {
                            mindist = d;
                            besti = i - intv;
                        }
                    }

                    Set<Face> sameRatio = new Set<Face>();
                    for (int i = besti; i < besti + intv; i++)
                    {
                        sameRatio.Add(ratios[i].Second); // clone, weil sonst der Debugger die ganze Shell mit serialisiert
                    }
                    Set<Face> biggestConnected = new Set<Face>();
                    while (!sameRatio.IsEmpty())
                    {
                        Set<Face> c = CollectFaces(sameRatio.GetAny(), sameRatio, Math.PI);
                        if (c.Count > biggestConnected.Count) biggestConnected = c;
                    }
                    if (biggestConnected.Count > 3)
                    {
                        List<Face> created = FindCommonSurfaceFaces(faces, biggestConnected, extent, dontCheck, precision);
                        res.AddRange(created);
                        if (faces.Count > 2) res.AddRange(FindCommonSurfaceFaces(faces, biggestConnected, extent, dontCheck, precision));
                        return res;
                    }
                    intv *= 2; // search for less similar
                } while (intv < ratios.Count / 3);
            }

            return res;

        }

        private List<Vertex> CollectConnectedEdges(Edge startWith, Set<Edge> toUse, double maxDerivation)
        {
            Vertex v1 = startWith.Vertex1;
            Vertex v2 = startWith.Vertex2;
            List<Vertex> res = new List<Vertex>();
            res.Add(v2);
            res.Add(v1);
            foreach (Vertex v in new Vertex[] { v1, v2 })
            {
                Edge currentEdge = startWith;
                Vertex currentVertex = v;
                bool found = true;
                while (found)
                {
                    found = false;
                    Set<Edge> connected = currentVertex.ConditionalEdgesSet(delegate (Edge e) { return toUse.Contains(e); });
                    double bestAngle = double.MaxValue;
                    Edge bestEdge = null;
                    foreach (Edge cn in connected)
                    {
                        Vertex cv = cn.OtherVertex(currentVertex);
                        Vertex sv = currentEdge.OtherVertex(currentVertex);
                        Angle a = new Angle(cv.Position - currentVertex.Position, sv.Position - currentVertex.Position);
                        double adir = Math.PI - a.Radian;
                        if (adir < maxDerivation && adir < bestAngle)
                        {
                            bestEdge = cn;
                            bestAngle = adir;
                        }
                    }
                    if (bestEdge != null)
                    {
                        toUse.Remove(bestEdge);
                        currentEdge = bestEdge;
                        currentVertex = bestEdge.OtherVertex(currentVertex);
                        if (v == v1) res.Add(currentVertex);
                        else res.Insert(0, currentVertex);
                        found = true;
                    }
                }
            }
            return res;
        }

        private Face CreateFaceOnCommonSurface(ISurface surface, Face startWith, Set<Face> toUse, Dictionary<Edge, Edge> newEdges, out Set<Face> usedFaces, double eps)
        {
            // the two faces connected to the provided edge are in a plane. Find all connected faces
            List<Edge> edges = new List<Edge>(startWith.Edges);
            List<Edge> outline = new List<Edge>();
            Set<Edge> toConnect = new Set<Edge>(startWith.Edges);
            usedFaces = new Set<Face>();
            for (int i = 0; i < edges.Count; i++)
            {
                Edge clone = edges[i].CloneWithVertices();
                newEdges[edges[i]] = clone;
                outline.Add(clone);
            }
            usedFaces.Add(startWith);
            Face fc = Face.MakeFace(surface, outline.ToArray());

            while (toConnect.Count > 0)
            {
                Edge tst = toConnect.GetAny();
                toConnect.Remove(tst);
                Face toConnectWith;
                if (usedFaces.Contains(tst.PrimaryFace)) toConnectWith = tst.SecondaryFace;
                else toConnectWith = tst.PrimaryFace;
                if (usedFaces.Contains(toConnectWith)) continue;
                if (!toUse.Contains(toConnectWith)) continue;
                Set<Vertex> vpoints = new Set<Vertex>(toConnectWith.Vertices);
                vpoints.Remove(tst.Vertex1);
                vpoints.Remove(tst.Vertex2);
                double maxerr = 0.0;
                foreach (Vertex vtx in vpoints)
                {
                    GeoPoint2D uv = surface.PositionOf(vtx.Position);
                    double err = vtx.Position | surface.PointAt(uv);
                    if (err > maxerr) maxerr = err;
                }

                if (maxerr > eps) continue; // not in same surface
                usedFaces.Add(toConnectWith);
                Edge[] o = toConnectWith.OutlineEdges;
                int io = Array.IndexOf<Edge>(o, tst);
                Edge[] toInsert = new Edge[o.Length - 1];
                for (int i = 0; i < o.Length - 1; i++)
                {
                    Edge oo = o[(io + i + 1) % o.Length];
                    toInsert[i] = oo.CloneWithVertices();
                    newEdges[oo] = toInsert[i];
                    toConnect.Add(oo);
                    toInsert[i].SetPrimary(fc, oo.Forward(toConnectWith));
                    SurfaceHelper.AdjustPeriodic(surface, fc.Domain, toInsert[i].PrimaryCurve2D);
                }
                int ii = outline.IndexOf(newEdges[tst]); // must exist
                outline.RemoveAt(ii);
                outline.InsertRange(ii, toInsert);
                fc.SetOutline(outline.ToArray());
            }
            // TODO:
            // 1. in sich selbst zurückkehrende edges entfernen
            // 2. doppelt vorhandene Edges entfernen und das innere Stück in ein Loch verwandeln

            foreach (Edge edg in fc.Edges)
            {
                List<Edge> connecting = new List<Edge>(Vertex.ConnectingEdges(edg.Vertex1, edg.Vertex2));
                if (connecting.Count > 1)
                {
                    for (int i = 0; i < connecting.Count; i++)
                    {
                        if (connecting[i].PrimaryFace == fc && connecting[i] != edg)
                        {

                        }
                    }
                }
            }
            return fc;
        }

        private Set<Face> CollectFaces(Face fc, Set<Face> allFaces, double maxbend)
        {
            Set<Face> res = new Set<Face>();
            res.Add(fc);
            allFaces.Remove(fc);
            Set<Edge> toFollow = fc.AllEdgesSet;
            while (toFollow.Count > 0)
            {
                Edge edg = toFollow.GetAny();
                toFollow.Remove(edg);
                Face toCheck;
                if (res.Contains(edg.PrimaryFace)) toCheck = edg.SecondaryFace;
                else toCheck = edg.PrimaryFace;
                if (toCheck == null || !allFaces.Contains(toCheck)) continue;
                GeoVector n1 = (edg.OtherFace(toCheck).Surface as PlaneSurface).Normal;
                GeoVector n2 = (toCheck.Surface as PlaneSurface).Normal;
                SweepAngle sw = new SweepAngle(n1, n2);
                if (Math.Abs(sw.Radian) < maxbend)
                {
                    res.Add(toCheck);
                    allFaces.Remove(toCheck);
                    foreach (Edge e in toCheck.Edges)
                    {
                        Face otherFace = e.OtherFace(toCheck);
                        if (otherFace != null && allFaces.Contains(otherFace)) toFollow.Add(e);
                    }
                }
            }
            return res;
        }
        private Face CollectPlanarFaces(Edge edg, Set<Face> usedFaces, Dictionary<Edge, Edge> newEdges)
        {
            // the two faces connected to the provided edge are in a plane. Find all connected faces
            PlaneSurface ps = edg.PrimaryFace.Surface.Clone() as PlaneSurface;
            List<Edge> edges = new List<Edge>();
            Edge[] o1 = edg.PrimaryFace.OutlineEdges;
            int i1 = Array.IndexOf<Edge>(o1, edg);
            Edge[] o2 = edg.SecondaryFace.OutlineEdges;
            int i2 = Array.IndexOf<Edge>(o2, edg);
            List<Edge> outline = new List<Edge>();
            Set<Edge> toConnect = new Set<Edge>();
            for (int i = 1; i < o1.Length; i++)
            {
                Edge clone = o1[(i1 + i) % o1.Length].CloneWithVertices();
                newEdges[o1[(i1 + i) % o1.Length]] = clone;
                toConnect.Add(o1[(i1 + i) % o1.Length]);
                outline.Add(clone);
            }
            for (int i = 1; i < o2.Length; i++)
            {
                Edge clone = o2[(i2 + i) % o2.Length].CloneWithVertices();
                newEdges[o2[(i2 + i) % o2.Length]] = clone;
                toConnect.Add(o2[(i2 + i) % o2.Length]);
                outline.Add(clone);
            }
            usedFaces.Add(edg.PrimaryFace);
            usedFaces.Add(edg.SecondaryFace);
            Face fc = Face.MakeFace(ps, outline.ToArray());
            while (toConnect.Count > 0)
            {
                Edge tst = toConnect.GetAny();
                toConnect.Remove(tst);
                Face toConnectWith;
                if (usedFaces.Contains(tst.PrimaryFace)) toConnectWith = tst.SecondaryFace;
                else toConnectWith = tst.PrimaryFace;
                if (usedFaces.Contains(toConnectWith)) continue;
                GeoVector n1 = (fc.Surface as PlaneSurface).Normal;
                GeoVector n2 = (toConnectWith.Surface as PlaneSurface).Normal;
                SweepAngle sw = new SweepAngle(n1, n2);
                if (Math.Abs(sw.Radian) > 1e-7) continue; // not in same plane
                usedFaces.Add(toConnectWith);
                Edge[] o = toConnectWith.OutlineEdges;
                int io = Array.IndexOf<Edge>(o, tst);
                Edge[] toInsert = new Edge[o.Length - 1];
                for (int i = 0; i < o.Length - 1; i++)
                {
                    Edge oo = o[(io + i + 1) % o.Length];
                    toInsert[i] = oo.CloneWithVertices();
                    newEdges[oo] = toInsert[i];
                    toConnect.Add(oo);
                    toInsert[i].SetPrimary(fc, oo.Forward(toConnectWith));
                }
                int ii = outline.IndexOf(newEdges[tst]); // must exist
                outline.RemoveAt(ii);
                outline.InsertRange(ii, toInsert);
                fc.SetOutline(outline.ToArray());
            }
            return fc;
        }

        /// <summary>
        /// Overrides <see cref="CADability.GeoObject.IGeoObjectImpl.PaintTo3DList (IPaintTo3D, ICategorizedDislayLists)"/>
        /// </summary>
        /// <param name="paintTo3D"></param>
        /// <param name="lists"></param>
        public override void PaintTo3DList(IPaintTo3D paintTo3D, ICategorizedDislayLists lists)
        {   // verschiedene layer der Faces nicht berücksichtigt...
            if (Layer != null && Layer.Transparency > 0)
                lists.Add(Layer, false, false, this); // in PaintTo3D wirds dann richtig gemacht
            else lists.Add(Layer, true, true, this); // in PaintTo3D wirds dann richtig gemacht
                                                     // paintTo3D.PaintSurfaces und paintTo3D.PaintEdges muss richtig eingestellt und zweimal aufgerufen werden
        }
        public delegate bool PaintTo3DDelegate(Shell toPaint, IPaintTo3D paintTo3D);
        public static PaintTo3DDelegate OnPaintTo3D;
        /// <summary>
        /// Overrides <see cref="CADability.GeoObject.IGeoObjectImpl.PaintTo3D (IPaintTo3D)"/>
        /// </summary>
        /// <param name="paintTo3D"></param>
        public override void PaintTo3D(IPaintTo3D paintTo3D)
        {
            if (OnPaintTo3D != null && OnPaintTo3D(this, paintTo3D)) return;
            if (paintTo3D.PaintSurfaces)
            {
                for (int i = 0; i < faces.Length; ++i)
                {
                    faces[i].PaintFaceTo3D(paintTo3D); // does not paint edges
                }
            }
            if (paintTo3D.PaintEdges && paintTo3D.PaintSurfaceEdges)
            {
                if (paintTo3D.SelectMode)
                {
                    // paintTo3D.SetColor(paintTo3D.SelectColor);
                }
                else
                {
                    if (colorDef != null) paintTo3D.SetColor(colorDef.Color);
                }
                paintTo3D.SetLinePattern(null);
                paintTo3D.SetLineWidth(null);
                for (int i = 0; i < Edges.Length; ++i)
                {
                    edges[i].PaintTo3D(paintTo3D);
                }
            }
            for (int i = 0; i < FeatureAxis.Count; i++)
            {
                if (FeatureAxis[i].IsVisible) FeatureAxis[i].PaintTo3D(paintTo3D);
            }
        }
        /// <summary>
        /// Overrides <see cref="CADability.GeoObject.IGeoObjectImpl.PrepareDisplayList (double)"/>
        /// </summary>
        /// <param name="precision"></param>
        public override void PrepareDisplayList(double precision)
        {
            for (int i = 0; i < Edges.Length; ++i)
            {
                if (!edges[i].IsSeam())
                {
                    IGeoObjectImpl go = edges[i].Curve3D as IGeoObjectImpl;
                    if (go != null)
                    {
                        go.PrepareDisplayList(precision);
                    }
                }
            }
            for (int i = 0; i < Faces.Length; ++i)
            {
                faces[i].PrepareDisplayList(precision);
            }
        }
        public override Style.EDefaultFor PreferredStyle
        {
            get
            {
                return Style.EDefaultFor.Solids;
            }
        }
        /// <summary>
        /// Overrides <see cref="CADability.GeoObject.IGeoObjectImpl.GetExtent (Projection, ExtentPrecision)"/>
        /// </summary>
        /// <param name="projection"></param>
        /// <param name="extentPrecision"></param>
        /// <returns></returns>
        public override BoundingRect GetExtent(Projection projection, ExtentPrecision extentPrecision)
        {
            BoundingRect res = BoundingRect.EmptyBoundingRect;
            for (int i = 0; i < Faces.Length; ++i)
            {
                res.MinMax(faces[i].GetExtent(projection, extentPrecision));
            }
            return res;
        }
        /// <summary>
        /// Overrides <see cref="CADability.GeoObject.IGeoObjectImpl.GetQuadTreeItem (Projection, ExtentPrecision)"/>
        /// </summary>
        /// <param name="projection"></param>
        /// <param name="extentPrecision"></param>
        /// <returns></returns>
        public override IQuadTreeInsertableZ GetQuadTreeItem(Projection projection, ExtentPrecision extentPrecision)
        {
            QuadTreeCollection res = new QuadTreeCollection(this, projection);
            if (projection.ShowFaces)
            {   // mit Faces
                for (int i = 0; i < Faces.Length; ++i)
                {
                    res.Add(new Face.FaceQuadTree(faces[i], projection));
                }
            }
            // Edges kommen immer
            for (int i = 0; i < Edges.Length; ++i)
            {
                if (edges[i].Curve3D != null)
                {
                    res.Add((edges[i].Curve3D as IGeoObject).GetQuadTreeItem(projection, extentPrecision));
                }
            }
            return res;
        }
        public override Layer Layer
        {
            get
            {
                return base.Layer;
            }
            set
            {
                base.Layer = value;
                if (faces != null)
                {
                    for (int i = 0; i < faces.Length; ++i)
                    {
                        faces[i].Layer = value;
                    }
                }
            }
        }
        public override IGeoObject[] OwnedItems
        {
            get
            {
                List<IGeoObject> res = new List<IGeoObject>(Faces.Length + Edges.Length);
                for (int i = 0; i < faces.Length; ++i)
                {
                    res.Add(faces[i]);
                }
                for (int i = 0; i < edges.Length; ++i)
                {
                    if (edges[i].Curve3D is IGeoObject go) res.Add(go);
                }
                for (int i = 0; i < FeatureAxis.Count; i++)
                {
                    res.Add(FeatureAxis[i]);
                }
                return res.ToArray();
            }
        }
        /// <summary>
        /// Overrides <see cref="CADability.GeoObject.IGeoObjectImpl.Decompose ()"/>
        /// </summary>
        /// <returns></returns>
        public override GeoObjectList Decompose()
        {
            Face[] faces = this.Faces;
            GeoObjectList decomp = new GeoObjectList();
            for (int i = 0; i < faces.Length; ++i)
            {
                IGeoObject go = faces[i].Clone();
                // Attribut nur kopieren, wenn es keines gibt
                // das kann man allgemein nicht feststellen
                // CopyNullAttributes oder so...
                //go.CopyAttributes(faces[i]); // geändert von this nach faces[i]: eine shell mit einem face in anderer Farbe wird zerlegt. Das Faces soll seine Farbe beibehalten.
                // obiges auskommentiert, da Face.Clone() das sowieso schon tut
                go.PropagateAttributes(this.Layer, this.colorDef); // das überschreibt nur, wenn null oder byparent
                decomp.Add(go);
            }
            return decomp;
        }
        #endregion
        #region IOctTreeInsertable members
        /// <summary>
        /// Overrides <see cref="CADability.GeoObject.IGeoObjectImpl.GetExtent (double)"/>
        /// </summary>
        /// <param name="precision"></param>
        /// <returns></returns>
        public override BoundingCube GetExtent(double precision)
        {
            return GetBoundingCube();
        }
        /// <summary>
        /// Overrides <see cref="CADability.GeoObject.IGeoObjectImpl.HitTest (ref BoundingCube, double)"/>
        /// </summary>
        /// <param name="cube"></param>
        /// <param name="precision"></param>
        /// <returns></returns>
        public override bool HitTest(ref BoundingCube cube, double precision)
        {
            foreach (Face fc in Faces)
            {
                if (fc.HitTest(ref cube, precision)) return true;
            }
            // Edges braucht man doch nicht auch noch, oder?
            return false;
        }
        /// <summary>
        /// Overrides <see cref="CADability.GeoObject.IGeoObjectImpl.HitTest (Projection, BoundingRect, bool)"/>
        /// </summary>
        /// <param name="projection"></param>
        /// <param name="rect"></param>
        /// <param name="onlyInside"></param>
        /// <returns></returns>
        public override bool HitTest(Projection projection, BoundingRect rect, bool onlyInside)
        {
            if (onlyInside)
            {
                foreach (Face fc in Faces)
                {
                    if (!fc.HitTest(projection, rect, onlyInside)) return false;
                }
                return true;
            }
            else
            {
                foreach (Face fc in Faces)
                {
                    if (fc.HitTest(projection, rect, onlyInside)) return true;
                }
                return false;
            }
        }
        /// <summary>
        /// Overrides <see cref="CADability.GeoObject.IGeoObjectImpl.HitTest (Projection.PickArea, bool)"/>
        /// </summary>
        /// <param name="area"></param>
        /// <param name="onlyInside"></param>
        /// <returns></returns>
        public override bool HitTest(Projection.PickArea area, bool onlyInside)
        {
            if (onlyInside)
            {
                foreach (Face fc in Faces)
                {
                    if (!fc.HitTest(area, onlyInside)) return false;
                }
                return true;
            }
            else
            {
                foreach (Face fc in Faces)
                {
                    if (fc.HitTest(area, onlyInside)) return true;
                }
                return false;
            }
        }
        /// <summary>
        /// Overrides <see cref="CADability.GeoObject.IGeoObjectImpl.Position (GeoPoint, GeoVector, double)"/>
        /// </summary>
        /// <param name="fromHere"></param>
        /// <param name="direction"></param>
        /// <param name="precision"></param>
        /// <returns></returns>
        public override double Position(GeoPoint fromHere, GeoVector direction, double precision)
        {
            double res = double.MaxValue;
            foreach (Face fc in Faces)
            {
                double d = fc.Position(fromHere, direction, precision);
                if (d < res) res = d;
            }
            return res;
        }
        #endregion
        #region ISerializable Members
        protected Shell(SerializationInfo info, StreamingContext context)
            : base(info, context)
        {
            //faces = info.GetValue("Faces", typeof(Face[])) as Face[];
            //colorDef = ColorDef.Read(info, context);
            SerializationInfoEnumerator e = info.GetEnumerator();
            while (e.MoveNext())
            {
                switch (e.Name)
                {   // um Exceptions zu verhindern und Def bzw. Colordef gleichermaßen zu lesen
                    default:
                        base.SetSerializationValue(e.Name, e.Value);
                        break;
                    case "Faces":
                        faces = e.Value as Face[];
                        break;
                    case "Def": // hieß früher dummerweise "Def"
                    case "ColorDef":
                        colorDef = e.Value as ColorDef;
                        break;
                    case "Name":
                        name = e.Value as string;
                        break;
                    case "OrientedAndSeamless":
                        orientedAndSeamless = (bool)e.Value;
                        break;
                }
            }
        }
        void ISerializable.GetObjectData(SerializationInfo info, StreamingContext context)
        {
            base.GetObjectData(info, context);
            info.AddValue("Faces", faces, typeof(Face[]));
            info.AddValue("ColorDef", colorDef);
            if (name != null) info.AddValue("Name", name);
            info.AddValue("OrientedAndSeamless", orientedAndSeamless);
        }
        #endregion
        #region IDeserializationCallback Members
        void IDeserializationCallback.OnDeserialization(object sender)
        {
            for (int i = 0; i < faces.Length; ++i)
            {
                faces[i].Owner = this;
            }
            if (Constructed != null) Constructed(this);
        }
        #endregion
        public override void GetObjectData(IJsonWriteData data)
        {
            base.GetObjectData(data);
            data.AddProperty("Faces", faces);
            if (colorDef != null) data.AddProperty("ColorDef", colorDef);
            if (name != null) data.AddProperty("Name", name);
            data.AddProperty("State", State);
            if (parametricProperties != null) data.AddProperty("ParametricProperties", parametricProperties);
        }

        public override void SetObjectData(IJsonReadData data)
        {
            base.SetObjectData(data);
            faces = data.GetProperty<Face[]>("Faces");
            colorDef = data.GetPropertyOrDefault<ColorDef>("ColorDef");
            name = data.GetPropertyOrDefault<string>("Name");
            State = data.GetProperty<ShellFlags>("State");
            parametricProperties = data.GetPropertyOrDefault<List<ParametricProperty>>("ParametricProperties");
            data.RegisterForSerializationDoneCallback(this);
        }
        void IJsonSerializeDone.SerializationDone(JsonSerialize jsonSerialize)
        {
            for (int i = 0; i < faces.Length; ++i)
            {
                faces[i].Owner = this;
                // what about:
                //faces[i].WillChangeEvent += new ChangeDelegate(OnWillChange);
                //faces[i].DidChangeEvent += new ChangeDelegate(OnDidChange);
                // like in Solid or Block?
            }
        }
#if DEBUG
#endif
        public void FreeCachedMemory()
        {
            for (int i = 0; i < faces.Length; i++)
            {
                faces[i].FreeCachedMemory();
            }
        }
        #region IColorDef Members
        private ColorDef colorDef;
        public ColorDef ColorDef
        {
            get
            {
                return colorDef;
            }
            set
            {
                using (new ChangingAttribute(this, "ColorDef", colorDef))
                {
                    colorDef = value;
                    if (faces != null)
                    {
                        // alle Faces auf die selbe Farbe setzen, denn die Faces
                        // bestimmen die Farbe der edges. Sollten Faces und Edges unabhängige Farben
                        // haben, dann wäre die Farbe des Shells bedeutungslos
                        for (int i = 0; i < faces.Length; ++i)
                        {
                            faces[i].ColorDef = value;
                        }
                    }
                }
            }
        }
        void IColorDef.SetTopLevel(ColorDef newValue)
        {
            colorDef = newValue;
        }
        void IColorDef.SetTopLevel(ColorDef newValue, bool overwriteChildNullColor)
        {
            colorDef = newValue;
            if (overwriteChildNullColor)
            {
                if (faces != null)
                {
                    for (int i = 0; i < faces.Length; ++i)
                    {
                        if (faces[i].ColorDef == null) faces[i].ColorDef = newValue;
                    }
                }
            }
        }
        #endregion
        /// <summary>
        /// Returns true if there is an edge that belongs to a single face. If there are no free edges the shell
        /// can be used for a solid.
        /// </summary>
        /// <returns>true if there are free edges</returns>
        internal bool HasFreeEdges()
        {
            foreach (Edge edge in Edges)
            {
                if (edge.SecondaryFace == null) return true;
            }
            return false;
        }
        internal bool IsValid
        {
            get
            {
                Set<Edge> edgeset = new Set<Edge>();
                foreach (Face fc in faces)
                {
                    foreach (Edge ed in fc.AllEdges)
                    {
                        edgeset.Add(ed);
                    }
                }
                foreach (Face fc in faces)
                {
                    Set<Edge> all = new Set<Edge>(fc.AllEdges);
                    if (!all.IsSubsetOf(edgeset)) return false;
                }
                Set<Face> faceset = new Set<Face>(faces);
                foreach (Edge ed in edgeset)
                {
                    if (!faceset.Contains(ed.PrimaryFace)) return false;
                    if (ed.SecondaryFace != null && !faceset.Contains(ed.SecondaryFace)) return false;
                }
                // Orientierung auch noch testen
                return true;
            }
        }
        /// <summary>
        /// Checks whether the shall is closed, i.e. all edges of all faces connect two faces of this shell.
        /// </summary>
        public bool IsClosed
        {
            get
            {
                Set<Edge> edgeset = new Set<Edge>();
                foreach (Face fc in faces)
                {
                    edgeset.AddMany(fc.AllEdges);
                }
                Set<Face> facesset = new Set<Face>(faces);
                foreach (Edge e in edgeset)
                {
                    if (e.SecondaryFace == null) return false;
                    if (!facesset.Contains(e.PrimaryFace)) return false;
                    if (!facesset.Contains(e.SecondaryFace)) return false;
                }
                return true;
            }
        }
        /// <summary>
        /// Returns all edges, which have only a <see cref="Edge.PrimaryFace"/>. If there are edges with references to faces, which do not belong to this shell,
        /// these references are removed and the edges are treated as open edges.
        /// </summary>
        public Edge[] OpenEdges
        {
            get
            {
                Set<Edge> edgeset = new Set<Edge>();
                foreach (Face fc in faces)
                {
                    edgeset.AddMany(fc.AllEdges);
                }
                Set<Face> facesset = new Set<Face>(faces);
                Set<Edge> res = new Set<Edge>();
                foreach (Edge e in edgeset)
                {
                    if (e.SecondaryFace == null) res.Add(e);
                    // remove references to faces not contained in this shell. this is explicitly requested by BRepOperation
                    if (!facesset.Contains(e.SecondaryFace))
                    {
                        e.RemoveSecondaryFace();
                        res.Add(e);
                    }
                    if (!facesset.Contains(e.PrimaryFace))
                    {
                        e.RemovePrimaryFace();
                        res.Add(e);
                    }
                }
                return res.ToArray();
            }
        }
        public GeoObjectList FeatureAxis
        {
            get
            {
                if (featureAxis == null)
                {
                    featureAxis = new GeoObjectList();
                    HashSet<Face> usedFaces = new HashSet<Face>();
                    foreach (Face face in Faces)
                    {
                        if (usedFaces.Contains(face)) continue; // already used
                        HashSet<Face> other = face.GetCylindricalConnected();
                        if (other.Any())
                        {
                            if (face.Surface is CylindricalSurface cyl)
                            {
                                GeoPoint ll = cyl.PointAt(face.Domain.GetLowerLeft());
                                GeoPoint ur = cyl.PointAt(face.Domain.GetUpperRight());
                                GeoPoint sp = Geometry.DropPL(ll, cyl.Location, cyl.ZAxis);
                                GeoPoint ep = Geometry.DropPL(ur, cyl.Location, cyl.ZAxis);
                                Line ax = Line.TwoPoints(sp, ep);
                                ax.Length *= 1.1;
                                (ax as ICurve).Reverse();
                                ax.Length *= 1.1;
                                ax.UserData.Add("CADability.AxisOf", face);
                                featureAxis.Add(ax);
                                ax.IsVisible = false;
                                ax.Owner = this;
                            }
                            usedFaces.UnionWith(other);
                        }
                    }
                }
                return featureAxis;
            }
        }
        public bool HasOpenEdgesExceptPoles()
        {
            Edge[] open = OpenEdges;
            bool ok = true;
            for (int i = 0; i < open.Length; i++)
            {
                if (open[i].Vertex1 != open[i].Vertex2)
                {
                    ok = false;
                    break;
                }
            }
            return !ok;
        }
        public Edge[] OpenEdgesExceptPoles
        {
            get
            {
                List<Edge> open = new List<Edge>(OpenEdges);
                for (int i = open.Count - 1; i >= 0; --i)
                {
                    if (open[i].Vertex1 == open[i].Vertex2)
                    {
                        open.RemoveAt(i);
                    }
                }
                return open.ToArray();
            }
        }
        /// <summary>
        /// Close all open edges by adding new faces
        /// </summary>
        /// <returns>true, if the Shell is now closed, false otherwise</returns>
        public bool CloseOpenEdges(Dictionary<Edge, ISurface> proposedSurfaces = null)
        {
            using (new Changing(this, "SetFaces", faces.Clone()))
            {
                HashSet<Edge> openEdges = new HashSet<Edge>(OpenEdges);
                if (!openEdges.Any()) return true; // there are no open edges
                while (openEdges.Any())
                {
                    List<Edge> sortedEdges = new List<Edge>();
                    Edge startWith = openEdges.First();
                    sortedEdges.Add(startWith);
                    openEdges.Remove(startWith);
                    Vertex startVertex = startWith.StartVertex(startWith.PrimaryFace);
                    Vertex currentVertex = startWith.EndVertex(startWith.PrimaryFace);
                    do
                    {
                        List<Edge> toFollow = currentVertex.ConditionalEdges(delegate (Edge e)
                        {
                            return openEdges.Contains(e) && currentVertex == e.StartVertex(e.PrimaryFace);
                        });
                        if (toFollow.Count == 1)
                        {
                            sortedEdges.Add(toFollow[0]);
                            currentVertex = toFollow[0].EndVertex(toFollow[0].PrimaryFace);
                            openEdges.Remove(toFollow[0]);
                            if (currentVertex == startVertex) break;
                        }
                        else
                        {
                            break;
                        }
                    } while (openEdges.Any());
                    if (startVertex == currentVertex)
                    {
                        HashSet<Face> closed = CloseEdgeLoop(sortedEdges, true);
                        if (closed != null)
                        {
                            closed.UnionWith(faces);
                            faces = closed.ToArray();
                            edges = null;
                        }
                    }
                }
                // CombineConnectedFaces(); still problems in 06_PN_4648_S_1185_1_I15_A13_AS_P100-1.stp !!!
            }
            return OpenEdges.Length == 0;
        }
        /// <summary>
        /// Takes a (closed) loop of edges of this shell as parameter <paramref name="sortedEdges"/> and tries to create a shell, which connects the edges.
        /// It heals the hole, described by <paramref name="sortedEdges"/>. The edges are sorted so that following the edges they go clockwise around the hole to fix.
        /// </summary>
        /// <param name="sortedEdges">Edges, which only have the <see cref="Edge.PrimaryFace"/> set (the <see cref="Edge.SecondaryFace"/> must be null)</param>
        /// <returns></returns>
        public HashSet<Face> CloseEdgeLoop(List<Edge> sortedEdges, bool addToShell)
        {
            if (sortedEdges.Count == 1 && sortedEdges[0].Curve3D == null)
            {
                return null;
            }
            if (sortedEdges.Count == 1 && sortedEdges[0].Vertex1 == sortedEdges[0].Vertex2 && sortedEdges[0].Curve3D.Length < Precision.eps)
            {
                sortedEdges[0].PrimaryFace.RemoveEdge(sortedEdges[0]);
                return null;
            }
            if (sortedEdges.Count == 2 && sortedEdges[0].Curve3D == null && sortedEdges[1].Curve3D == null)
            {
                // this is a pole: connect the two edges
                sortedEdges[1].PrimaryFace.ReplaceEdge(sortedEdges[1], sortedEdges[0]);
                return null;
            }
            if (sortedEdges.Count == 2 && sortedEdges[0].Curve3D != null && sortedEdges[1].Curve3D != null)
            {
                if (sortedEdges[0].StartVertex(sortedEdges[0].PrimaryFace) == sortedEdges[1].EndVertex(sortedEdges[1].PrimaryFace)
                    && sortedEdges[0].EndVertex(sortedEdges[0].PrimaryFace) == sortedEdges[1].StartVertex(sortedEdges[1].PrimaryFace)
                    && Curves.SameGeometry(sortedEdges[0].Curve3D, sortedEdges[1].Curve3D, Precision.eps, out bool reversed))
                {
                    sortedEdges[1].PrimaryFace.ReplaceEdge(sortedEdges[1], sortedEdges[0]);
                    return null;
                }
            }
            // test for simplest case: all edges share a common surface
            ISurface surface = null;
            BoundingRect firstDomain = BoundingRect.EmptyBoundingRect;
            foreach (Edge edge in sortedEdges)
            {
                if (surface == null)
                {
                    surface = edge.PrimaryFace.Surface;
                    firstDomain = edge.PrimaryFace.Domain;
                }
                else
                {
                    if (!surface.SameGeometry(firstDomain, edge.PrimaryFace.Surface, edge.PrimaryFace.Domain, Precision.eps, out ModOp2D dumy))
                    {
                        surface = null;
                        break;
                    }
                }
            }
            if (surface != null)
            {
                // there is a common surface to all open edges
                sortedEdges.Reverse();
                Face newFace = Face.Construct();
                newFace.Surface = surface.Clone();
                for (int i = 0; i < sortedEdges.Count; i++)
                {
                    if (addToShell)
                    {
                        sortedEdges[i].SetSecondary(newFace, !sortedEdges[i].Forward(sortedEdges[i].PrimaryFace));
                    }
                    else
                    {
                        Edge newEdge = new Edge(newFace, sortedEdges[i].Curve3D); // do we need a clone of Curve3D?
                        newEdge.SetPrimary(newFace, !sortedEdges[i].Forward(sortedEdges[i].PrimaryFace));
                        sortedEdges[i] = newEdge;
                    }
                }
                newFace.Set(surface.Clone(), sortedEdges, new List<List<Edge>>());
                if (newFace.Area.Area > Precision.eps) return new HashSet<Face>(new Face[] { newFace });
            }
            ISurface surfaceOfEdges = null; // we look for a surface that all edges share
            ICurve[] crvs = new ICurve[sortedEdges.Count];
            List<int> arcs = new List<int>();
            List<int> ellipticArcs = new List<int>();
            List<int> lines = new List<int>();
            List<GeoPoint> pointsForGaussNewton = new List<GeoPoint>();
            for (int i = 0; i < sortedEdges.Count; i++)
            {
                crvs[i] = sortedEdges[i].Curve3D;
                if (sortedEdges[i].Curve3D is Ellipse elli)
                {
                    if (elli.IsCircle) arcs.Add(i);
                    else ellipticArcs.Add(i);
                }
                else if (sortedEdges[i].Curve3D is Line) lines.Add(i);
                pointsForGaussNewton.Add(sortedEdges[i].Curve3D.StartPoint);
                pointsForGaussNewton.Add(sortedEdges[i].Curve3D.PointAt(0.333));
                pointsForGaussNewton.Add(sortedEdges[i].Curve3D.PointAt(0.666));
            }
            if (Curves.GetCommonPlane(crvs, out Plane commonPlane))
            {
                surfaceOfEdges = new PlaneSurface(commonPlane);
            }
            else
            {
                GeoPoint cnt = new GeoPoint(pointsForGaussNewton.ToArray());
                BoundingCube ext = new BoundingCube(pointsForGaussNewton.ToArray());
                double bestPrecision = double.MaxValue;
                double error = GaussNewtonMinimizer.CylinderFit(new ListToIArray<GeoPoint>(pointsForGaussNewton), cnt, GeoVector.XAxis, ext.Size, Precision.eps, out CylindricalSurface cyx);
                if (error < Precision.eps && error < bestPrecision)
                {
                    surfaceOfEdges = cyx;
                    bestPrecision = error;
                }
                error = GaussNewtonMinimizer.CylinderFit(new ListToIArray<GeoPoint>(pointsForGaussNewton), cnt, GeoVector.YAxis, ext.Size, Precision.eps, out CylindricalSurface cyy);
                if (error < Precision.eps && error < bestPrecision)
                {
                    surfaceOfEdges = cyy;
                    bestPrecision = error;
                }
                double dz = GaussNewtonMinimizer.CylinderFit(new ListToIArray<GeoPoint>(pointsForGaussNewton), cnt, GeoVector.ZAxis, ext.Size, Precision.eps, out CylindricalSurface cyz);
                if (dz < Precision.eps && dz < bestPrecision)
                {
                    surfaceOfEdges = cyz;
                    bestPrecision = dz;
                }
                error = GaussNewtonMinimizer.SphereFit(new ListToIArray<GeoPoint>(pointsForGaussNewton), cnt, ext.Size, Precision.eps, out SphericalSurface ss);
                if (error < Precision.eps && error < bestPrecision)
                {
                    surfaceOfEdges = ss;
                    bestPrecision = error;
                }
                if (lines.Count > 1)
                {
                    if (sortedEdges[lines[0]].Curve3D is Line l1 && sortedEdges[lines[1]].Curve3D is Line l2)
                    {
                        try
                        {
                            GeoPoint ip = Geometry.IntersectLL(l1.StartPoint, l1.StartDirection, l2.StartPoint, l2.StartDirection);
                            cnt = ip;
                        }
                        catch { }
                    }
                }
                if (arcs.Count > 0 && sortedEdges[arcs[0]].Curve3D is Ellipse elli)
                {
                    error = BoxedSurfaceExtension.ConeFit(new ListToIArray<GeoPoint>(pointsForGaussNewton), cnt, elli.Normal, Math.PI / 2.1, Precision.eps, out ConicalSurface csn);
                    // error = GaussNewtonMinimizer.ConeFit(new ListToIArray<GeoPoint>(pointsForGaussNewton), cnt, elli.Normal, Math.PI / 2.1, Precision.eps, out ConicalSurface csn);
                    if (error < Precision.eps && error < bestPrecision)
                    {
                        surfaceOfEdges = csn;
                        bestPrecision = error;
                    }
                }
                error = BoxedSurfaceExtension.ConeFit(new ListToIArray<GeoPoint>(pointsForGaussNewton), cnt, GeoVector.XAxis, Math.PI / 4.0, Precision.eps, out ConicalSurface csx);
                if (error < Precision.eps && error < bestPrecision)
                {
                    surfaceOfEdges = csx;
                    bestPrecision = error;
                }
                error = BoxedSurfaceExtension.ConeFit(new ListToIArray<GeoPoint>(pointsForGaussNewton), cnt, GeoVector.YAxis, Math.PI / 4.0, Precision.eps, out ConicalSurface csy);
                if (error < Precision.eps && error < bestPrecision)
                {
                    surfaceOfEdges = csy;
                    bestPrecision = error;
                }
                error = BoxedSurfaceExtension.ConeFit(new ListToIArray<GeoPoint>(pointsForGaussNewton), cnt, GeoVector.ZAxis, Math.PI / 4.0, Precision.eps, out ConicalSurface csz);
                if (error < Precision.eps && error < bestPrecision)
                {
                    surfaceOfEdges = csz;
                    bestPrecision = error;
                }
            }
            // following crashes with 04_PN_1013_S_1205_2_I06_A06_AS_P806_E1_E10.stp
            if (surfaceOfEdges != null)
            {   // there is a single surface which contains all the edges
                if (surfaceOfEdges is ConicalSurface cs) surfaceOfEdges = new ConicalSurfaceNP(cs.Location, cs.XAxis, cs.YAxis, cs.ZAxis, cs.OpeningAngle / 2.0);
                sortedEdges.Reverse();
                Face newFace = Face.Construct();
                newFace.Surface = surfaceOfEdges;
                BoundingRect domain = BoundingRect.EmptyBoundingRect;
                for (int i = 0; i < sortedEdges.Count; i++)
                {
                    if (addToShell)
                    {
                        sortedEdges[i].SetSecondary(newFace, !sortedEdges[i].Forward(sortedEdges[i].PrimaryFace));
                    }
                    else
                    {
                        Edge newEdge = new Edge(newFace, sortedEdges[i].Curve3D); // do we need a clone of Curve3D?
                        newEdge.SetPrimary(newFace, !sortedEdges[i].Forward(sortedEdges[i].PrimaryFace));
                        sortedEdges[i] = newEdge;
                    }
                    if (!domain.IsEmpty()) SurfaceHelper.AdjustPeriodic(surfaceOfEdges, domain, sortedEdges[i].Curve2D(newFace));
                    domain.MinMax(sortedEdges[i].Curve2D(newFace).GetExtent());
                }
                ICurve2D[] testOrientation = new ICurve2D[sortedEdges.Count];
                for (int i = 0; i < sortedEdges.Count; i++)
                {
                    testOrientation[i] = sortedEdges[i].Curve2D(newFace);
                }
                if (!Border.CounterClockwise(testOrientation))
                {
                    ModOp2D reverse = newFace.Surface.ReverseOrientation();
                    for (int i = 0; i < testOrientation.Length; i++)
                    {
                        ICurve2D reversed = testOrientation[i].GetModified(reverse);
                        if (sortedEdges[i].PrimaryFace == newFace) sortedEdges[i].PrimaryCurve2D = reversed;
                        else sortedEdges[i].SecondaryCurve2D = reversed;
                    }
                }
                newFace.Set(newFace.Surface, sortedEdges, new List<List<Edge>>());
                return new HashSet<Face>(new Face[] { newFace });
            }
            // now we have to check whether there are only two or three adjacent surfaces and we can extend them to fill the open edges loop

            // if nothing works: "ear clipping" and adding ruled surfaces
            return null;
        }
        public void ReverseOrientation()
        {
            foreach (Face fc in faces)
            {
                fc.ReverseOrientation();
            }
        }
        public void AssertOutwardOrientation()
        {
#if DEBUG
            double ll = this.GetExtent(0.0).Size * 0.01;
            DebuggerContainer dc = new DebuggerContainer();
            ColorDef cd = new ColorDef("debug", Color.Red);
            foreach (Face fc in faces)
            {
                SimpleShape ss = fc.Area;
                GeoPoint2D c = ss.GetExtent().GetCenter();
                GeoPoint pc = fc.Surface.PointAt(c);
                GeoVector nc = fc.Surface.GetNormal(c);
                Line l = Line.TwoPoints(pc, pc + ll * nc.Normalized);
                l.ColorDef = cd;
                dc.Add(l);
            }
#endif
            Vertex[] vertices = Vertices; // damit sie bestimmt werden und die Orientierung stimmt
            foreach (Face fc in faces)
            {
                fc.MakeTopologicalOrientation();
            }
            Face correctOriented = null;
            foreach (Edge e in Edges)
            {
                e.Orient();
            }
            bool innerPointFound = false;
            foreach (Face fc in faces)
            {
                SimpleShape ss = fc.Area;
                GeoPoint2D c = ss.GetExtent().GetCenter();
                if (ss.Contains(c, false))
                {
                    innerPointFound = true;
                    GeoVector normal = fc.Surface.GetNormal(c);
                    GeoPoint location = fc.Surface.PointAt(c);
                    GeoPoint[] ip = GetLineIntersection(location, normal);
                    int n = 0;
                    for (int i = 0; i < ip.Length; ++i)
                    {
                        if (Geometry.LinePar(location, normal, ip[i]) > 1e-10)
                        {
                            ++n;
                        }
                    }
                    if (n == 0)
                    {
                        correctOriented = fc;
                        break;
                    }
                }
            }
            if (!innerPointFound)
            {
                foreach (Face fc in faces)
                {
                    SimpleShape ss = fc.Area;
                    GeoPoint2D c = ss.GetSomeInnerPoint();
                    if (ss.Contains(c, false))
                    {
                        GeoVector normal = fc.Surface.GetNormal(c);
                        GeoPoint location = fc.Surface.PointAt(c);
                        GeoPoint[] ip = GetLineIntersection(location, normal);
                        int n = 0;
                        for (int i = 0; i < ip.Length; ++i)
                        {
                            if (Geometry.LinePar(location, normal, ip[i]) > 1e-10)
                            {
                                ++n;
                            }
                        }
                        if (n == 0)
                        {
                            correctOriented = fc;
                            break;
                        }
                    }
                }
            }
            if (correctOriented == null)
            {
                BoundingCube ext = GetExtent(0.0);
                ext.Expand(ext.Size * 0.1);
                GeoPoint[] extpnts = ext.Points;
                GeoPoint cnt = ext.GetCenter();
                for (int i = 0; i < extpnts.Length; i++)
                {
                    GeoPoint pe = extpnts[i];
                    GeoVector dir = cnt - pe;
                    double minpar = double.MaxValue;
                    bool samepar = false; // zwei Schnittpunkte mit fas dem selben parameter deuten auf eine Ecke oder Kante hin, und das ist schlecht.
                    Face faceFound = null;
                    GeoPoint2D uvFound = GeoPoint2D.Origin;
                    foreach (Face fc in faces)
                    {
                        GeoPoint2D[] ips = fc.GetLineIntersection2D(pe, dir);
                        int bestInd = -1;
                        for (int j = 0; j < ips.Length; j++)
                        {
                            GeoPoint ip = fc.Surface.PointAt(ips[j]);
                            double par = Geometry.LinePar(pe, dir, ip);
                            if (Math.Abs(minpar - par) < 1e-4) samepar = true;
                            if (par < minpar)
                            {
                                if (minpar - par > 1e-4) samepar = false;
                                minpar = par;
                                bestInd = j;
                            }
                        }
                        if (bestInd >= 0)
                        {
                            uvFound = ips[bestInd];
                            faceFound = fc;
                        }
                    }
                    if (faceFound != null && !samepar)
                    {
                        GeoVector normal = faceFound.Surface.GetNormal(uvFound);
                        if (normal * dir > Precision.eps)
                        {
                            faceFound.MakeInverseOrientation();
                            correctOriented = faceFound;
                        }
                        else if (normal * dir < Precision.eps)
                        {
                            correctOriented = faceFound;
                        }
                    }
                    if (correctOriented != null) break;
                }
            }
            if (correctOriented == null)
            {   // Alle falschrum
                correctOriented = faces[0];
                correctOriented.MakeInverseOrientation();
            }
            if (correctOriented != null)
            {
                Set<Face> correctFaces = new Set<Face>();
                correctFaces.Add(correctOriented);
                SortedDictionary<Edge, Face> toOrient = new SortedDictionary<Edge, Face>(new EdgeLengthComparer());
                foreach (Edge e in correctOriented.AllEdges)
                {
                    if (!e.IsPeriodicEdge && !e.IsSingular())
                    {
                        toOrient[e] = e.OtherFace(correctOriented);
                        //OrientFace(correctFaces, e.OtherFace(correctOriented), e);
                    }
                }
                while (toOrient.Count > 0) OrientFaces(correctFaces, toOrient);
            }
            foreach (Face fc in faces) fc.OrientedOutward = true; // die sind jetzt alle richtig, egal was vorher drinstand

        }
#if DEBUG
        private class TriangleVertex : IOctTreeInsertable
        {
            public GeoPoint p3d;
            public List<GeoPoint2D> uv;
            public List<Face> face;
            public TriangleVertex(GeoPoint p3d, GeoPoint2D uv, Face face)
            {
                this.p3d = p3d;
                this.uv = new List<CADability.GeoPoint2D>();
                this.uv.Add(uv);
                this.face = new List<GeoObject.Face>();
                this.face.Add(face);
            }

            BoundingCube IOctTreeInsertable.GetExtent(double precision)
            {
                return new BoundingCube(p3d);
            }

            bool IOctTreeInsertable.HitTest(ref BoundingCube cube, double precision)
            {
                return cube.Contains(p3d);
            }

            bool IOctTreeInsertable.HitTest(Projection projection, BoundingRect rect, bool onlyInside)
            {
                throw new NotImplementedException();
            }

            bool IOctTreeInsertable.HitTest(Projection.PickArea area, bool onlyInside)
            {
                throw new NotImplementedException();
            }

            double IOctTreeInsertable.Position(GeoPoint fromHere, GeoVector direction, double precision)
            {
                throw new NotImplementedException();
            }
        }
        private class TriangleEdge : IOctTreeInsertable
        {
            public int i1, i2;
            List<GeoPoint> alltrianglePoints; // nur Referenz nach außen
            public TriangleEdge(int i1, int i2, List<GeoPoint> alltrianglePoints, GeoPoint2D uv, Face face)
            {
                this.i1 = i1;
                this.i2 = i2;
                this.alltrianglePoints = alltrianglePoints;
            }

            BoundingCube IOctTreeInsertable.GetExtent(double precision)
            {
                return new CADability.BoundingCube(alltrianglePoints[i1], alltrianglePoints[i2]);
            }

            bool IOctTreeInsertable.HitTest(Projection.PickArea area, bool onlyInside)
            {
                throw new NotImplementedException();
            }

            bool IOctTreeInsertable.HitTest(ref BoundingCube cube, double precision)
            {
                GeoPoint p1 = alltrianglePoints[i1];
                GeoPoint p2 = alltrianglePoints[i2];
                return cube.Interferes(ref p1, ref p2);
            }

            bool IOctTreeInsertable.HitTest(Projection projection, BoundingRect rect, bool onlyInside)
            {
                throw new NotImplementedException();
            }

            double IOctTreeInsertable.Position(GeoPoint fromHere, GeoVector direction, double precision)
            {
                throw new NotImplementedException();
            }
        }
        public Shell GetTriangulatedShell(double precision)
        {
            int baseIndex = 0;
            List<GeoPoint> alltrianglePoints = new List<GeoPoint>();
            List<Tripel<int, int, int>> allFaces = new List<Tripel<int, int, int>>();
            foreach (Face fc in faces)
            {
                GeoPoint[] trianglePoint;
                GeoPoint2D[] triangleUVPoint;
                int[] triangleIndex;
                BoundingCube triangleExtent;
                fc.GetTriangulation(precision, out trianglePoint, out triangleUVPoint, out triangleIndex, out triangleExtent);
                if (trianglePoint != null)
                {
                    alltrianglePoints.AddRange(trianglePoint);
                    Set<Pair<int, int>> openEdges = new Set<Pair<int, int>>();
                    for (int i = 0; i < triangleIndex.Length; i += 3)
                    {
                        // jede innere Kante kommt einmal vorwärts und einmal rückwärts vor
                        if (!openEdges.Remove(new Pair<int, int>(triangleIndex[i + 1], triangleIndex[i]))) openEdges.Add(new Pair<int, int>(triangleIndex[i], triangleIndex[i + 1]));
                        if (!openEdges.Remove(new Pair<int, int>(triangleIndex[i + 2], triangleIndex[i + 1]))) openEdges.Add(new Pair<int, int>(triangleIndex[i + 1], triangleIndex[i + 2]));
                        if (!openEdges.Remove(new Pair<int, int>(triangleIndex[i], triangleIndex[i + 2]))) openEdges.Add(new Pair<int, int>(triangleIndex[i + 2], triangleIndex[i]));
                        allFaces.Add(new Tripel<int, int, int>(triangleIndex[i] + baseIndex, triangleIndex[i + 1] + baseIndex, triangleIndex[i + 2] + baseIndex));
                    }
                    Set<int> openVertices = new Set<int>();
                    // die vertices muss man dann noch unterscheiden zwischen denen, wo zwei fremde dreiecke aneinanderstoßen, die also mit einer Pipe gefüllt werden müssen, und denen, 
                    // die echte vertices der Shell sind, wo also eine kuge eingefüllt wertden muss. Und dann noch die selbstüberschneidungen
                }
            }
            return null;
        }
#endif
        public bool IsInside(GeoPoint toTest)
        {
            return Contains(toTest);
        }
        public bool Contains(GeoPoint toTest)
        {
            // suche einen Face-Mittelpunkt, so dass kein Schnittpunkt zwischen toTest und dem Facemittelpunkt liegt
            // wenn gefunden, dann bestimme ob der Strahl von toTest und diesem Punkt von innen oder außen scheidet (Orientierung vorausgesetzt)
            // Wenn es keinen solchen gibt, dann müsste noch genauer geprüft werden (noch nicht implementiert)
            foreach (Face fc in faces)
            {
                SimpleShape ss = fc.Area;
                GeoPoint2D c = ss.GetExtent().GetCenter();
                if (ss.Contains(c, false))
                {
                    GeoPoint location = fc.Surface.PointAt(c);
                    GeoVector dir = location - toTest;

                    GeoPoint[] ip = GetLineIntersection(location, dir);
                    int n = 0;
                    for (int i = 0; i < ip.Length; ++i)
                    {
                        double pos = Geometry.LinePar(toTest, dir, ip[i]);
                        if (pos > 0.0 && pos < 1.0 - Precision.eps)
                        {
                            double d = fc.Distance(ip[i]);
                            if (Math.Abs(d) > Precision.eps) ++n;
                        }
                    }
                    if (n == 0)
                    {   // kein Schnittpunkt zwischen Testpunkt und Facepunkt
                        GeoVector normal = fc.Surface.GetNormal(c);
                        double s = (normal * dir) / (normal.Length * dir.Length);
                        if (Math.Abs(s) > 1e-8) return s > 0;
                    }
                }
            }
            return false;
        }
        internal void PreCalcTriangulation(double precisiton)
        {
            for (int i = 0; i < faces.Length; ++i)
            {
                faces[i].PreCalcTriangulation(precisiton);
            }
        }

        private Shell GetRawOffset(double dist, Dictionary<Edge, Edge> parallelEdges = null)
        {
            BoundingCube ext = GetExtent(0.0);
            ext.Expand(dist);
            double prec = ext.Size * 1e-6;
            Edge[] allEdges = Edges;
            bool splitted = false;
            for (int i = 0; i < allEdges.Length; i++)
            {
                if (allEdges[i].Curve3D is IExplicitPCurve3D)
                {
                    IExplicitPCurve3D iepc = (allEdges[i].Curve3D as IExplicitPCurve3D);
                    if (allEdges[i].Curve3D is BSpline)
                    {
                        BSpline dbgbsp = (allEdges[i].Curve3D.Clone() as BSpline);
                        dbgbsp.ReducePoles(prec);
                        dbgbsp.reparametrize(-1000, 1000);
                        iepc = dbgbsp as IExplicitPCurve3D;
                        for (int j = 0; j < 100; j++)
                        {
                            System.Diagnostics.Trace.WriteLine(j.ToString() + " Radius: " + (1.0 / dbgbsp.CurvatureAt(j / 99.0)).ToString());
                        }
                    }
                    ExplicitPCurve3D epc = iepc.GetExplicitPCurve3D();
                    double[] splitPos = epc.GetCurvaturePositions(dist);
                    if (splitPos.Length > 0)
                    {
                        SortedList<double, Vertex> sortedVertices = new SortedList<double, CADability.Vertex>();
                        for (int j = 0; j < splitPos.Length; j++)
                        {
                            GeoPoint p = epc.PointAt(splitPos[j]);
                            double pos = allEdges[i].Curve3D.PositionOf(p); // die Parametersysteme stimmen nicht überein
                            sortedVertices[pos] = new Vertex(p);
                        }
                        //allEdges[i].Split(sortedVertices, Precision.eps);
                        //splitted = true;
                    }
                }
            }
            if (splitted) edges = null;
            List<Face> fcs = new List<Face>();
            OctTree<Vertex> vertexOctTree = new OctTree<Vertex>(ext, ext.Size * 1e-8);
            Dictionary<Pair<Face, Vertex>, Vertex> orgToOffsetVtx = new Dictionary<Pair<Face, Vertex>, Vertex>();
            // 1. alle Offset-Faces erzeugen, gleichzeitig die Vertices sammeln
            foreach (Face face in Faces)
            {
                // face.CheckConsitency();
                Vertex[] vtx = face.Vertices;
                Dictionary<Vertex, Vertex> offsetvtx = new Dictionary<Vertex, Vertex>();
                foreach (Vertex v in vtx)
                {
                    GeoPoint2D pf = v.GetPositionOnFace(face);
                    GeoVector normal = face.Surface.GetNormal(pf);
                    if (!normal.IsNullVector())
                    {
                        GeoPoint p = v.Position + dist * normal.Normalized;
                        Vertex vn = new Vertex(p);
                        offsetvtx[v] = vn;
                        orgToOffsetVtx[new Pair<Face, Vertex>(face, v)] = vn;
                    }
                }
                Face offsetFace = face.GetOffset(dist, offsetvtx);
                if (offsetFace != null)
                {   // Faces können degenerieren als Offset, dann sollen sie nicht verwendet werden (Kugel wird zum Punkt etc.)
#if DEBUG
                    GeoPoint[] trianglePoint;
                    GeoPoint2D[] triangleUVPoint;
                    int[] triangleIndex;
                    BoundingCube triangleExtent;
                    offsetFace.GetTriangulation(0.9, out trianglePoint, out triangleUVPoint, out triangleIndex, out triangleExtent);
#endif
                    if (parallelEdges != null)
                    {   // die beiden sind synchron
                        Edge[] offedg = offsetFace.AllEdges;
                        Edge[] orgedg = face.AllEdges;
                        for (int i = 0; i < offedg.Length; i++)
                        {
                            parallelEdges[orgedg[i]] = offedg[i];
                        }
                    }
                    fcs.Add(offsetFace);
                    vertexOctTree.AddMany(offsetvtx.Values);
                }
            }
            // 2. zu allen Kanten die Ausrundungsflächen erzeugen, dabei auf die bestehenden Kanten zurückgreifen
            allEdges = Edges;
            foreach (Edge edg in allEdges)
            {
                if (edg.Curve3D != null && edg.SecondaryFace != null)
                {
                    Face pf = edg.PrimaryFace;
                    Face sf = edg.SecondaryFace;
                    Vertex startHere = edg.StartVertex(edg.PrimaryFace);
                    Vertex endHere = edg.EndVertex(edg.PrimaryFace);
                    GeoVector pfn;
                    GeoVector sfn;
                    if (edg.Forward(edg.PrimaryFace))
                    {
                        pfn = edg.PrimaryFace.Surface.GetNormal(startHere.GetPositionOnFace(edg.PrimaryFace)).Normalized;
                        sfn = edg.SecondaryFace.Surface.GetNormal(startHere.GetPositionOnFace(edg.SecondaryFace)).Normalized;
                    }
                    else
                    {
                        pfn = edg.PrimaryFace.Surface.GetNormal(endHere.GetPositionOnFace(edg.PrimaryFace)).Normalized;
                        sfn = edg.SecondaryFace.Surface.GetNormal(endHere.GetPositionOnFace(edg.SecondaryFace)).Normalized;
                    }
                    bool tangentialFaces = false;
                    if (Precision.OppositeDirection(pfn, sfn))
                    {
                        // weiteres Problem: die beiden Flächen sind an der Kante tangential aber rückläufig, z.B. eine Schneide
                        // wir brauchen einen Punkt etwas weiter "innen"
                        tangentialFaces = true;
                        double dd = Math.Min(edg.PrimaryFace.GetExtent(0.0).Size, edg.SecondaryFace.GetExtent(0.0).Size) * 0.01;
                        GeoPoint testPoint;
                        if (edg.Forward(edg.PrimaryFace))
                        {
                            testPoint = edg.Curve3D.PointAt(0.5) - dd * (edg.Curve3D.DirectionAt(0.5).Normalized ^ pfn);
                        }
                        else
                        {
                            testPoint = edg.Curve3D.PointAt(0.5) + dd * (edg.Curve3D.DirectionAt(0.5).Normalized ^ pfn);
                        }
                        // wir schneiden jetzt die beiden Faces an der Linie aus dem Testpunkt (der etwas innerhalb liegt) und der Normalen 
                        // (die ja bei beiden nur gegensätzliche Richtungen haben)
                        GeoPoint2D[] intpf = edg.PrimaryFace.Surface.GetLineIntersection(testPoint, pfn);
                        GeoPoint2D[] intsf = edg.SecondaryFace.Surface.GetLineIntersection(testPoint, pfn);
                        if (intpf.Length > 0 && intsf.Length > 0)
                        {
                            int i0 = -1, j0 = -1;
                            double minDist = double.MaxValue;
                            for (int i = 0; i < intpf.Length; i++)
                            {
                                double d = edg.PrimaryFace.Surface.PointAt(intpf[i]) | testPoint;
                                if (d < minDist)
                                {
                                    minDist = d;
                                    i0 = i;
                                }
                            }
                            minDist = double.MaxValue;
                            for (int i = 0; i < intsf.Length; i++)
                            {
                                double d = edg.SecondaryFace.Surface.PointAt(intsf[i]) | testPoint;
                                if (d < minDist)
                                {
                                    minDist = d;
                                    j0 = i;
                                }
                            }
                            // hier werden die genau gegengerichteten Normalen an der neuen, etwas nach innen verschobenen Position neu berechnet
                            // in der hoffnung, dass sie dann nicht mehr genau gegengerichtet sind
                            pfn = edg.PrimaryFace.Surface.GetNormal(intpf[i0]).Normalized;
                            sfn = edg.SecondaryFace.Surface.GetNormal(intsf[j0]).Normalized;
                        }
                    }
                    GeoVector pn = dist * pfn;
                    GeoVector sn = dist * sfn;
                    // Ist diese Kante eine konvexe Kante? (nur für die müssen wir Ausrundungsflächen erzeugen)
                    // Kriterium: die 3d Kurve, auf der Fläche vorwärts orientiert, im Kreuzprodukt mit dem Normalenvektor zeigt nach außen
                    // Überlegung: gehe im Sinne der Fläche auf der Kante vorwärts. Der Normalenvektor zeigt nach oben, dann ist links die Fläche, rechts ist außerhalb.
                    // Der Normalenvektor der anderen Fläche muss auf die selbe Seite zeigen, wie "rechts von der Kurve". Das gleiche gilt mit vertauschten
                    // Rollen. por und sor müssen immer das gleiche Vorzeichen haben, sonst ist die Shell nicht richtig orientiert
                    // Probleme entstehen, wenn Kanten an einem Ende konvex, am anderen konkav sind. Solche Kanten müssen (ggf. mehrfach) gesplittet werden
                    // und der folgende Test muss in der Mitte durchgeführt werden
                    // (z.Z. wird der Test am startHere, also StartPunkt im Sinne des Primaryfaces ausgeführt)
                    GeoVector pToRight, sToRight;
                    if (edg.Forward(edg.PrimaryFace))
                    {
                        pToRight = edg.Curve3D.StartDirection ^ pfn;
                        sToRight = (-edg.Curve3D.StartDirection) ^ sfn;
                    }
                    else
                    {
                        pToRight = (-edg.Curve3D.StartDirection) ^ pfn;
                        sToRight = (edg.Curve3D.StartDirection) ^ sfn;
                    }
                    double por = sToRight * pfn;
                    double sor = pToRight * sfn;
#if DEBUG
                    DebuggerContainer dccond = new CADability.DebuggerContainer();
                    ColorDef cd1 = new ColorDef("red", Color.Red);
                    ColorDef cd2 = new ColorDef("blue", Color.Blue);
                    Line l1 = Line.TwoPoints(edg.Curve3D.StartPoint, edg.Curve3D.StartPoint + pfn);
                    l1.ColorDef = cd1;
                    dccond.Add(l1);
                    Line l11 = Line.TwoPoints(edg.Curve3D.StartPoint, edg.Curve3D.StartPoint + pToRight);
                    l11.ColorDef = cd1;
                    dccond.Add(l11);
                    Line dl2 = Line.TwoPoints(edg.Curve3D.StartPoint, edg.Curve3D.StartPoint + sfn);
                    dl2.ColorDef = cd2;
                    dccond.Add(dl2);
                    Line l22 = Line.TwoPoints(edg.Curve3D.StartPoint, edg.Curve3D.StartPoint + sToRight);
                    l22.ColorDef = cd2;
                    dccond.Add(l22);
                    dccond.Add(edg.PrimaryFace);
                    dccond.Add(edg.SecondaryFace);
#endif
                    if (!Precision.IsEqual(pn, sn) && por > 0 && sor > 0)
                    {
                        // pn und sn sind die beiden nach außen zeigenden Normalenvektoren der Flächen.

                        // Erzeuge die Ellipse so, dass die "Naht" genau in Gegenrichtung der beiden Vektoren zeigt
                        // das ist nicht unbedingt sicher
                        Ellipse elli = Ellipse.Construct();
                        Plane pln = new Plane(startHere.Position, -(pn + sn), pn - sn);
                        elli.SetCirclePlaneCenterRadius(pln, startHere.Position, dist);
                        // Ellipse endCurve; // die Ellipse am Ende der Pipe
                        ISurface gsc = MakePipe(dist, edg.Curve3D, edg.Forward(edg.PrimaryFace),
                            x => -(pf.Surface.GetNormal(pf.Surface.PositionOf(edg.Curve3D.PointAt(x))).Normalized +
                            sf.Surface.GetNormal(sf.Surface.PositionOf(edg.Curve3D.PointAt(x))).Normalized));
#if DEBUG
                        GeoObjectList dbgl = (gsc as ISurfaceImpl).DebugGrid;
                        //dbgl = gsc.DebugGrid;
                        //dbgl = gsc.DebugAlong;
                        //dbgl = gsc.DebugForDerivation;
                        //double[] ll = new double[10];
                        //for (int i = 0; i < 10; i++)
                        //{
                        //    ll[i] = gsc.FixedV(i / 10.0, 0.0, 1.0).Length;
                        //}
                        // DebuggerContainer dc = gsc.BoxedSurfaceEx.Debug;
#endif
                        Vertex v1 = orgToOffsetVtx[new Pair<Face, Vertex>(edg.PrimaryFace, startHere)];
                        Vertex v2 = orgToOffsetVtx[new Pair<Face, Vertex>(edg.PrimaryFace, endHere)];
                        Vertex v3 = orgToOffsetVtx[new Pair<Face, Vertex>(edg.SecondaryFace, endHere)];
                        Vertex v4 = orgToOffsetVtx[new Pair<Face, Vertex>(edg.SecondaryFace, startHere)];
                        // die entstandene Fläche muss tangential an die beiden Ausgangsflächen anschließen. Sie kann dies auf zweierlei Art tun: in die gleiche
                        // Richtung oder in die Gegenrichtung. Ausrundungsflächen, die rückläufig sind, müssen nicht in Betracht gezogen werden. Sie fallen beim Berechnen
                        // der SelfIntersection ohnehin weg, und erzeugen dort nur zusätzlichen Aufwand, indem sie unnötige Schnittkanten berechnen lassen.
                        // Die Bedingung ist: Die Normalenvektoren and den 4 Ecken müssen in die gleiche Richtung zeigen, nicht in Gegenrichtung.
                        // Anschaulich bedeutet das, dass die Kante "konvex" ist
                        // Es sind (bei unregelmäßigen Nurbsflächen) Situationen denkbar, wo die Kanten am Ende konvex ist, in der Mitte aber konkav. 
                        // Diese werden hier unterschlagen.
                        double or1 = edg.PrimaryFace.Surface.GetNormal(startHere.GetPositionOnFace(edg.PrimaryFace)).Normalized * gsc.GetNormal(gsc.PositionOf(v1.Position)).Normalized;
                        double or2 = edg.PrimaryFace.Surface.GetNormal(endHere.GetPositionOnFace(edg.PrimaryFace)).Normalized * gsc.GetNormal(gsc.PositionOf(v2.Position)).Normalized;
                        double or3 = edg.SecondaryFace.Surface.GetNormal(endHere.GetPositionOnFace(edg.SecondaryFace)).Normalized * gsc.GetNormal(gsc.PositionOf(v3.Position)).Normalized;
                        double or4 = edg.SecondaryFace.Surface.GetNormal(startHere.GetPositionOnFace(edg.SecondaryFace)).Normalized * gsc.GetNormal(gsc.PositionOf(v4.Position)).Normalized;
                        if (or1 < 0 && or2 < 0 && or3 < 0 && or4 < 0)
                        {
                            // die Fläche ist falschrum orientiert
                            gsc.ReverseOrientation(); // da wir noch keine Kanten haben, ist die Ergebnis-ModOp2D irrelevant
                        }
                        Edge e1 = FindOffsetEdge(Vertex.ConnectingEdges(v1, v2), edg.PrimaryFace, edg, dist);
                        Edge e3 = FindOffsetEdge(Vertex.ConnectingEdges(v3, v4), edg.SecondaryFace, edg, dist);
                        if (e1 == null || e3 == null) continue;
#if DEBUG
                        //GeoVector pe = dist * edg.PrimaryFace.Surface.GetNormal(endHere.GetPositionOnFace(edg.PrimaryFace)).Normalized;
                        //GeoVector se = dist * edg.SecondaryFace.Surface.GetNormal(endHere.GetPositionOnFace(edg.SecondaryFace)).Normalized;
                        //GeoVector ps1 = gsc.GetNormal(gsc.PositionOf(v1.Position));
                        //GeoVector pe1 = gsc.GetNormal(gsc.PositionOf(v2.Position));
                        //GeoVector ss1 = gsc.GetNormal(gsc.PositionOf(v4.Position));
                        //GeoVector se1 = gsc.GetNormal(gsc.PositionOf(v3.Position));

                        DebuggerContainer dce = new DebuggerContainer();
                        dce.Add(v1.DebugPoint, 1);
                        dce.Add(v2.DebugPoint, 2);
                        dce.Add(v3.DebugPoint, 3);
                        dce.Add(v4.DebugPoint, 4);
                        dce.Add(e1.Curve3D as IGeoObject, 11);
                        dce.Add(e3.Curve3D as IGeoObject, 33);
#endif
                        //double pos1 = elli.PositionOf(v4.Position);
                        //double pos2 = elli.PositionOf(v1.Position);
                        //ICurve c3d1 = elli.Clone() as ICurve; // das ist der Startkreis, muss im 2d eine Linie sein
                        //c3d1.Trim(pos2, pos1);
                        //ICurve c3d2 = endCurve; // das ist der Endkreis, muss im 2d eine Linie sein
                        //double pos3 = c3d2.PositionOf(v3.Position);
                        //double pos4 = c3d2.PositionOf(v2.Position);
                        //c3d2.Trim(pos4, pos3);
                        Face connectingFace = Face.Construct();
                        ICurve2D c2de1;
                        if (gsc is GeneralSweptCurve)
                            c2de1 = (gsc as GeneralSweptCurve).GetProjectedCurveAlongV(e1.Curve3D);
                        else
                            c2de1 = gsc.GetProjectedCurve(e1.Curve3D, 0.0);
                        // if (!e1.Forward(e1.PrimaryFace)) c2de1.Reverse();

                        ICurve2D c2de3;
                        if (gsc is GeneralSweptCurve)
                            c2de3 = (gsc as GeneralSweptCurve).GetProjectedCurveAlongV(e3.Curve3D);
                        else
                            c2de3 = gsc.GetProjectedCurve(e3.Curve3D, 0.0);
#if DEBUG
                        ICurve dbge1 = gsc.Make3dCurve(c2de1); // Rückprojektion
                        ICurve dbge3 = gsc.Make3dCurve(c2de3);
#endif
                        //                        if (!e3.Forward(e3.PrimaryFace)) c2de3.Reverse();
                        bool uperiodic = true; // beim Zylinder ist die Peiode für den Kreisbogen, der die Ausrundung macht, in u, beim Torus in v
                        double period = Math.PI * 2;
                        if (gsc is ToroidalSurface)
                        {
                            uperiodic = false;
                            // beim Torus kann auch noch in der U-Periode falsch Positioniert sein (dei anderen sind nur in einer Richtung periodisch
                            GeoPoint2D m1 = c2de1.PointAt(0.5);
                            GeoPoint2D m3 = c2de3.PointAt(0.5);
                            if (Math.Abs(m1.x - m3.x) > Math.PI)
                            {
                                if (m1.x < m3.x) c2de1.Move(Math.PI * 2, 0);
                                else c2de3.Move(Math.PI * 2, 0);
                            }
                        }
                        else if (gsc is NurbsSurface)
                        {
                            period = 1;
                        }
                        // jetzt die beiden 2d Kurven so periodisch verschieben und in ihrer Richtung umdrehen,
                        // dass die dazwischenliegende Fläche kleiner als die Hälfte der periode ist
                        if (uperiodic)
                        {   // die v-Enden müssen gleich sein
                            if (Math.Abs(c2de1.StartPoint.y - c2de3.StartPoint.y) + Math.Abs(c2de1.EndPoint.y - c2de3.EndPoint.y) < Math.Abs(c2de1.StartPoint.y - c2de3.EndPoint.y) + Math.Abs(c2de1.EndPoint.y - c2de3.StartPoint.y))
                                c2de3.Reverse();
                        }
                        else
                        {
                            if (Math.Abs(c2de1.StartPoint.x - c2de3.StartPoint.x) + Math.Abs(c2de1.EndPoint.x - c2de3.EndPoint.x) < Math.Abs(c2de1.StartPoint.x - c2de3.EndPoint.x) + Math.Abs(c2de1.EndPoint.x - c2de3.StartPoint.x))
                                c2de3.Reverse();
                        }
                        // jetzt sind die beiden gegenläufig
                        // es soll jetzt gelten: verbinde c2de1.EndPoint mit c2de3.StartPoint mit einer (2d)Linie bzw. (3d)Kreisbogen
                        // im Folgenden werden die kleineren "Halbrohre" verwendet, indem ggf. um die Periode verschoben wird.
                        // Das ist problematisch bei tangentialen flächen, wo beide gleich sind
                        if (tangentialFaces)
                        {
                            GeoPoint2D insidepos = gsc.PositionOf(elli.PointAt(0.5));
                            if (uperiodic)
                            {
                                if (c2de1.EndPoint.x < c2de3.StartPoint.x)
                                {
                                    if (insidepos.x > c2de3.StartPoint.x || insidepos.x < c2de1.EndPoint.x) c2de1.Move(period, 0);
                                }
                                else
                                {
                                    if (insidepos.x < c2de3.StartPoint.x || insidepos.x > c2de1.EndPoint.x) c2de1.Move(-period, 0);
                                }
                            }
                            else
                            {
                                if (c2de1.EndPoint.y < c2de3.StartPoint.y)
                                {
                                    if (insidepos.y > c2de3.StartPoint.y || insidepos.y < c2de1.EndPoint.y) c2de1.Move(0, period);
                                }
                                else
                                {
                                    if (insidepos.y < c2de3.StartPoint.y || insidepos.y > c2de1.EndPoint.y) c2de1.Move(0, -period);
                                }
                            }
                        }
                        else
                        {
                            if (uperiodic)
                            {
                                if (c2de1.EndPoint.x < c2de3.StartPoint.x)
                                {
                                    if ((c2de3.StartPoint.x - c2de1.EndPoint.x) > period / 2) c2de1.Move(period, 0);
                                }
                                else
                                {
                                    if ((c2de1.EndPoint.x - c2de3.StartPoint.x) > period / 2) c2de1.Move(-period, 0);
                                }
                            }
                            else
                            {
                                if (c2de1.EndPoint.y < c2de3.StartPoint.y)
                                {
                                    if ((c2de3.StartPoint.y - c2de1.EndPoint.y) > period / 2) c2de1.Move(0, period);
                                }
                                else
                                {
                                    if ((c2de1.EndPoint.y - c2de3.StartPoint.y) > period / 2) c2de1.Move(0, -period);
                                }
                            }
                        }
                        e1.SetSecondary(connectingFace, c2de1, true);
                        e3.SetSecondary(connectingFace, c2de3, false); // Richtung müsste überprüft werden, oder?
                        Line2D l2 = new Line2D(c2de3.EndPoint, c2de1.StartPoint);
                        Line2D l4 = new Line2D(c2de1.EndPoint, c2de3.StartPoint);
                        Edge e2 = new Edge(null, gsc.Make3dCurve(l2));
                        e2.SetPrimary(connectingFace, new Line2D(c2de3.EndPoint, c2de1.StartPoint), true);
                        Edge e4 = new Edge(null, gsc.Make3dCurve(l4));
                        e4.SetPrimary(connectingFace, new Line2D(c2de1.EndPoint, c2de3.StartPoint), false);
#if DEBUG
                        DebuggerContainer dco = new DebuggerContainer();
                        dco.Add(e1.SecondaryCurve2D, Color.Red, 1);
                        dco.Add(e3.SecondaryCurve2D, Color.Red, 3);
                        dco.Add(e2.PrimaryCurve2D, Color.Red, 2);
                        dco.Add(e4.PrimaryCurve2D, Color.Red, 4);
                        dce.Add(e2.Curve3D as IGeoObject, 22);
                        dce.Add(e4.Curve3D as IGeoObject, 44);
                        DebuggerContainer dcp = new DebuggerContainer();
                        dcp.Add(gsc.Make3dCurve(e1.SecondaryCurve2D) as IGeoObject, 11);
                        dcp.Add(gsc.Make3dCurve(e3.SecondaryCurve2D) as IGeoObject, 33);
                        dcp.Add(gsc.Make3dCurve(e2.PrimaryCurve2D) as IGeoObject, 22);
                        dcp.Add(gsc.Make3dCurve(e4.PrimaryCurve2D) as IGeoObject, 44);
#endif
                        // die Frage nach der Reihenfolge ist noch offen:
                        Polyline2D pl2d = new Polyline2D(new GeoPoint2D[] { c2de3.EndPoint, c2de1.StartPoint, c2de1.EndPoint, c2de3.StartPoint });
                        if (pl2d.GetArea() < 0)
                            connectingFace.Set(gsc, new Edge[] { e4, e3, e2, e1 }, new Edge[0][]);
                        else
                            connectingFace.Set(gsc, new Edge[] { e1, e2, e3, e4 }, new Edge[0][]);
                        e2.UseVerticesForce(v1, v2, v3, v4); // muss nach Set kommen, da sonst face.surface nicht gesetzt ist
                        e4.UseVerticesForce(v1, v2, v3, v4);
                        connectingFace.ClearVertices();
                        e1.Orient();
                        e2.Orient();
                        e3.Orient();
                        e4.Orient();
                        fcs.Add(connectingFace);
#if DEBUG
                        foreach (Face dfc in v1.Faces)
                        {
                            GeoVector dbgnorm = dfc.Surface.GetNormal(v1.GetPositionOnFace(dfc));
                        }
                        GeoVector ps1 = gsc.GetNormal(gsc.PositionOf(v1.Position));
                        //pe1 = gsc.GetNormal(gsc.PositionOf(v2.Position));
                        //ss1 = gsc.GetNormal(gsc.PositionOf(v4.Position));
                        //se1 = gsc.GetNormal(gsc.PositionOf(v3.Position));

                        GeoPoint[] trianglePoint;
                        GeoPoint2D[] triangleUVPoint;
                        int[] triangleIndex;
                        BoundingCube triangleExtent;
                        connectingFace.GetTriangulation(0.1, out trianglePoint, out triangleUVPoint, out triangleIndex, out triangleExtent);
#endif
                    }
                    else
                    {   // keine Verbindung, die beiden Kanten sind tangential, geschlossen wird weiter unden
                    }
                }
            }
            // 3: jetzt Kugeln über die Ecken machen
            foreach (Vertex v in Vertices)
            {
                List<Vertex> vl = new List<Vertex>();
                foreach (Face f in v.Faces)
                {
                    Vertex vf = orgToOffsetVtx[new Pair<Face, Vertex>(f, v)];
                    if (vf != null)
                    {
                        bool found = false;
                        for (int i = 0; i < vl.Count; i++)
                        {
                            if ((vl[i].Position | vf.Position) < prec)
                            {
                                found = true;
                                break;
                            }
                        }
                        if (!found) vl.Add(vf);
                    }
                }
                if (vl.Count > 2)
                {
                    List<Edge> openEdges = new List<Edge>();
                    for (int i = 0; i < vl.Count; i++)
                    {
                        for (int j = i + 1; j < vl.Count; j++)
                        {
                            foreach (Edge e in Vertex.ConnectingEdges(vl[i], vl[j]))
                            {
                                if (e.SecondaryFace == null)
                                {
                                    openEdges.Add(e);
                                }
                            }

                        }
                    }
                    if (openEdges.Count > 2)
                    {   // je nach dem, wie viele Faces in "v" zusammenkommen, gibt es hier mehrere Edges, meist 3
                        // gesucht ist nun eine Kugelfläche, deren Pole und Naht möglichst nicht in dem zu erzeugenden Kugelface liegen.
                        // die openEdges sind immer Kreise, wenn auch u.U. als NURBS.
                        // "vl" sind die Endpunkte dieser Kreise.
                        // 1. suche den größten Abstand
                        int i0 = -1, j0 = -1; // die Indizes der am weitesten voneinder entfernten Vertices
                        double maxDist = 0.0;
                        for (int i = 0; i < vl.Count - 1; i++)
                        {
                            for (int j = i + 1; j < vl.Count; j++)
                            {
                                double d = vl[i].Position | vl[j].Position;
                                if (d > maxDist)
                                {
                                    maxDist = d;
                                    i0 = i;
                                    j0 = j;
                                }
                            }
                        }
                        Plane polePln = new Plane(v.Position, vl[i0].Position - vl[j0].Position);
                        // in dieser Ebene liegen die beiden Pole der Kugel. 
                        // Gesucht ist jetzt die Achse der Kugel. Da es maximal nur eine Halbkugel werden kann (Nadelspitze, bei der alle Faces sich tangential in der Spitze treffen)
                        // suchen wir jetzt eine Linie durch den Mittelpunkt, so dass alle vls und auch die Zwischenpunkte der Bögen auf der selben Seite liegen.
                        List<GeoPoint2D> projPnts = new List<GeoPoint2D>();
                        //for (int i = 0; i < vl.Count ; i++)
                        //{
                        //    projPnts.Add(polePln.Project(vl[i].Position));
                        //}
                        for (int i = 0; i < openEdges.Count; i++)
                        {   // die Mittelpunkte der Kanten
                            projPnts.Add(polePln.Project(openEdges[i].Curve3D.PointAt(0.5)));
                        }
                        // suche die Linie
                        GeoPoint2D allRight = GeoPoint2D.Origin;
                        GeoPoint2D allLeft = GeoPoint2D.Origin;
                        for (int i = 0; i < projPnts.Count; i++)
                        {   // der Mittelpunkt ist ja (0,0) (von v.Position)
                            double dmin = 0, dmax = 0;
                            for (int j = 0; j < projPnts.Count; j++)
                            {
                                if (i != j)
                                {
                                    double d = Geometry.DistPL(projPnts[j], GeoPoint2D.Origin, projPnts[i]);
                                    if (d < dmin) dmin = d;
                                    if (d > dmax) dmax = d;
                                }
                            }
                            if (dmin > -Precision.eps)
                            {   // alle Ergebnisse waren positiv, also auf der linken Seite
                                allLeft = projPnts[i];
                            }
                            if (dmax < Precision.eps)
                            {
                                allRight = projPnts[i];
                            }
                        }
                        // jetzt sollten wir 2 Linien haben, (0,0)->allLeft: alle Punkte liegen links dieser linie und
                        // (0,0)->allRight: alle Punkte liegen rechts
                        GeoVector2D axis = allRight - allLeft;
                        GeoVector2D toSeem = axis.ToLeft();
                        GeoVector dirx = polePln.ToGlobal(toSeem);
                        GeoVector diry = polePln.Normal;
                        GeoVector dirz = polePln.ToGlobal(axis);
                        dirx.Length = dist; // der Radius der Kugel
                        diry.Length = dist;
                        dirz.Length = dist;
                        SphericalSurface ss = new SphericalSurface(v.Position, dirx, diry, dirz);
#if DEBUG
                        Face dbgsph = Face.MakeFace(ss, new SimpleShape(Border.MakeRectangle(Math.PI / 2.0, 3.0 * Math.PI / 2.0, -1.5, 1.5))); // Halbkugel, an den Polen fehlt ein bisschen
#endif
                        //GeoVector toOutside = new GeoVector(0, 0, 0);
                        //for (int i = 0; i < vl.Count; i++) toOutside += (vl[i].Position - v.Position);
                        //// toOutside zeigt jetzt von v nach außen
                        //// wir brauchen eine Kugel, deren X-Achse genau in die andere Richtung zeigt
                        //GeoVector dirx = toOutside;
                        //GeoVector diry = dirx ^ (vl[0].Position - v.Position); // eigentlich beliebig
                        //if (diry.IsNullVector())
                        //{
                        //    if (Math.Abs(dirx.x) < Math.Abs(dirx.y))
                        //    {
                        //        if (Math.Abs(dirx.z) < Math.Abs(dirx.x))
                        //        {   // z am kleinsten
                        //            diry = dirx ^ GeoVector.ZAxis;
                        //        }
                        //        else
                        //        {   // x am kleinsten
                        //            diry = dirx ^ GeoVector.XAxis;
                        //        }
                        //    }
                        //    else
                        //    {
                        //        if (Math.Abs(dirx.z) < Math.Abs(dirx.y))
                        //        {   // z am kleinsten
                        //            diry = dirx ^ GeoVector.ZAxis;
                        //        }
                        //        else
                        //        {   // y am kleinsten
                        //            diry = dirx ^ GeoVector.YAxis;
                        //        }
                        //    }
                        //}
                        //GeoVector dirz = dirx ^ diry;
                        //dirx.Length = dist;
                        //diry.Length = dist;
                        //dirz.Length = dist;
                        //SphericalSurface ss = new SphericalSurface(v.Position, dirx, diry, dirz);

                        Face sf = Face.Construct();
                        foreach (Edge e in openEdges)
                        {
                            e.SetSecondary(sf, ss.GetProjectedCurve(e.Curve3D, 0.0), !e.Forward(e.PrimaryFace));
                        }
                        sf.SetSurfaceAndEdges(ss, openEdges.ToArray());
                        fcs.Add(sf);
                    }
                }
                else if (vl.Count == 2)
                {   // nur zwei Punkte. hier ist ein tangentialer Übergang, wird weiter unten geschlossen
                }
            }
            // 4. noch offene Kanten schließen
            for (int i = 0; i < fcs.Count; i++)
            {
                foreach (Edge edg in fcs[i].Edges)
                {
                    if (edg.SecondaryFace == null)
                    {
                        bool replaced = false;
                        Vertex svedg = edg.StartVertex(edg.PrimaryFace);
                        Vertex evedg = edg.EndVertex(edg.PrimaryFace);
                        Vertex[] sv = vertexOctTree.GetObjectsFromBox(new BoundingCube(svedg.Position, 2 * prec));
                        Vertex[] ev = vertexOctTree.GetObjectsFromBox(new BoundingCube(evedg.Position, 2 * prec));
                        for (int j = 0; j < sv.Length; j++)
                        {
                            for (int k = 0; k < ev.Length; k++)
                            {
                                if ((sv[j].Position | svedg.Position) < prec && (ev[k].Position | evedg.Position) < prec)
                                {
                                    foreach (Edge e in Vertex.ConnectingEdges(sv[j], ev[k]))
                                    {
                                        if (e != edg && edg.Curve3D.DistanceTo(e.Curve3D.PointAt(0.5)) < prec)
                                        {
                                            svedg.MergeWith(sv[j]);
                                            evedg.MergeWith(ev[k]);
                                            e.PrimaryFace.ReplaceEdge(e, edg);
                                            replaced = true;
                                            break;
                                        }
                                    }
                                }
                                if (replaced) break;
                            }
                            if (replaced) break;
                        }
                    }
                }
            }

            Shell res = MakeShell(fcs.ToArray());
            // res.AssertOutwardOrientation(); die Orientierung ist genau so wie das Original, kann bei offenen Shells ambiguous sein
            // Edge[] oe = res.OpenEdges; 
            res.RecalcEdges(); // wichtig, da egde und verex Koordinatenmäßig ungenau sein kann. Evtl an besser geeigneter Stelle sicherstellen, oder?
            return res;
        }

        public Shell[] GetOffset(double dist, bool allowOpenEdges = true, Dictionary<Edge, Edge> parallelEdges = null)
        {

            AssertOutwardOrientation();
            SplitPeriodicFaces();
            RecalcVertices();
            RecalcEdges(); // muss noch ausgebaut werden. Es muss aber stimmig sein, sonst Probleme beim Schneiden
                           // Edges, deren Krümmungsradius unter dist liegt. aufschneiden, an den Stellen, wo der Krümmungsradius genau dist ist.
                           // das hilft selbstüberschneidungen zu vermeiden
            Shell res = GetRawOffset(dist, parallelEdges);
            BRepSelfIntersection si = new BRepSelfIntersection(res, this, dist);
            return si.Result(allowOpenEdges);
        }
        public Solid MakeThick(double innerDist, double outerDist)
        {
            if (innerDist == outerDist) return null;
            AssertOutwardOrientation(); // diesen Vorspann durch Flags in der Shell nur machen, wenn noch nicht gemacht
            SplitPeriodicFaces();
            RecalcVertices();
            RecalcEdges();

            if (innerDist > outerDist)
            {
                double tmp = innerDist;
                innerDist = outerDist;
                outerDist = tmp;
            }
            Dictionary<Edge, Edge> innerEdges = new Dictionary<Edge, Edge>();
            Dictionary<Edge, Edge> outerEdges = new Dictionary<Edge, Edge>();
            Shell inner, outer;
            if (innerDist == 0.0) inner = Clone(innerEdges);
            else inner = GetRawOffset(innerDist, innerEdges);
            if (outerDist == 0.0) outer = Clone(outerEdges);
            else outer = GetRawOffset(outerDist, outerEdges);
            inner.ReverseOrientation(); // die ganze Shell muss andersrum zeigen
            if (inner != null && outer != null)
            {
                List<Face> allfaces = new List<Face>();
                allfaces.AddRange(inner.Faces);
                allfaces.AddRange(outer.Faces);
                // finde jetzt "parallele" Edges, die mit einem Face zusammengeführt werden
                Edge[] oe = this.OpenEdges;
                // hier erstmal lineare Suche
                for (int i = 0; i < oe.Length; i++)
                {
                    Edge innerParallel, outerParallel;
                    if (innerEdges.TryGetValue(oe[i], out innerParallel) && outerEdges.TryGetValue(oe[i], out outerParallel))
                    {
                        Edge e1, e2; // die beiden Verbindungslinien
                        Vertex v1 = outerParallel.Vertex1;
                        Vertex v2 = outerParallel.Vertex2;
                        Vertex v3 = innerParallel.Vertex1;
                        Vertex v4 = innerParallel.Vertex2;
                        Face connectingFace = Face.Construct();
                        Plane pln;
                        if (Curves.GetCommonPlane(innerParallel.Curve3D, outerParallel.Curve3D, out pln))
                        {
                            PlaneSurface pls = new GeoObject.PlaneSurface(pln);
                            e1 = new Edge(connectingFace, Line.MakeLine(innerParallel.StartVertex(innerParallel.PrimaryFace).Position, outerParallel.EndVertex(outerParallel.PrimaryFace).Position));
                            e2 = new Edge(connectingFace, Line.MakeLine(outerParallel.StartVertex(outerParallel.PrimaryFace).Position, innerParallel.EndVertex(innerParallel.PrimaryFace).Position));
                            Face fc = Face.MakeFace(pls, new Edge[] { innerParallel, e2, outerParallel, e1 });
                            allfaces.Add(fc);
                        }
                        else
                        {
                            ICurve c1 = outerParallel.Curve3D.Clone();
                            ICurve c2 = innerParallel.Curve3D.Clone();
                            if (!outerParallel.Forward(outerParallel.PrimaryFace)) c1.Reverse();
                            if (innerParallel.Forward(innerParallel.PrimaryFace)) c2.Reverse();
                            RuledSurface rs = new CADability.RuledSurface(c1, c2);
#if DEBUG
                            DebuggerContainer dc = new CADability.DebuggerContainer();
                            Face dbgfc = Face.MakeFace(rs, new SimpleShape(Border.MakeRectangle(0, 1, 0, 1)));
                            ICurve2D dbgc2do = rs.GetProjectedCurve(outerParallel.Curve3D, 0.0);
                            ICurve2D dbgc2di = rs.GetProjectedCurve(innerParallel.Curve3D, 0.0);
#endif
                            outerParallel.SetSecondary(connectingFace, new Line2D(new GeoPoint2D(0, 0), new GeoPoint2D(1, 0)), !outerParallel.Forward(outerParallel.PrimaryFace));
                            innerParallel.SetSecondary(connectingFace, new Line2D(new GeoPoint2D(1, 1), new GeoPoint2D(0, 1)), !innerParallel.Forward(innerParallel.PrimaryFace));
                            e1 = new Edge(connectingFace, Line.MakeLine(c2.EndPoint, c1.EndPoint), connectingFace, new Line2D(new GeoPoint2D(1, 0), new GeoPoint2D(1, 1)), true);
                            e2 = new Edge(connectingFace, Line.MakeLine(c1.StartPoint, c2.StartPoint), connectingFace, new Line2D(new GeoPoint2D(0, 1), new GeoPoint2D(0, 0)), true);
                            connectingFace.Set(rs, new Edge[] { outerParallel, e1, innerParallel, e2 }, null, false);
                            connectingFace.ReverseOrientation();
                            allfaces.Add(connectingFace);
                        }
                    }
                }
                connectFaces(allfaces.ToArray(), Precision.eps);
                Shell res = MakeShell(allfaces.ToArray());
                // Shell[] res = Make3D.SewFaces(allfaces.ToArray());
                if (res.OpenEdges.Length == 0)
                {
                    Solid sld = Solid.Construct();
                    sld.SetShell(res);
                    return sld;
                }
            }
            return null;
        }
        private static ISurface MakePipe(double radius, ICurve along, bool forward, Func<double, GeoVector> seem)
        {
            if (along is Line)
            {
                if (forward)
                {
                    // endCurve = (elli as ICurve).CloneModified(ModOp.Translate(along.StartDirection)) as Ellipse;
                    GeoVector dir = along.StartDirection;
                    GeoVector majorAxis = radius * seem(0.5).Normalized;
                    GeoVector minorAxis = radius * (dir ^ majorAxis).Normalized;
                    return new CylindricalSurface(along.StartPoint, majorAxis, minorAxis, dir);
                }
                else
                {
                    // endCurve = (elli as ICurve).CloneModified(ModOp.Translate(-along.StartDirection)) as Ellipse;
                    GeoVector dir = along.StartDirection;
                    GeoVector majorAxis = radius * seem(0.5).Normalized;
                    GeoVector minorAxis = radius * (dir ^ majorAxis).Normalized;
                    return new CylindricalSurface(along.StartPoint, majorAxis, minorAxis, -dir);
                }
            }
            if (along is Ellipse && (along as Ellipse).IsCircle)
            {
                Ellipse bigCircle = (along.Clone() as Ellipse);
                if (!forward) (bigCircle as ICurve).Reverse();
                // bigCircle ist ein Kreisbogen
                // endCurve = (elli as ICurve).CloneModified(ModOp.Rotate(bigCircle.Center, bigCircle.Plane.Normal, bigCircle.SweepParameter)) as Ellipse;
                // der Torus soll beim KreisbogenStart anfangen
                GeoVector dirx = (bigCircle.StartPoint - bigCircle.Center).Normalized;
                GeoVector dirz = (bigCircle.SweepParameter > 0) ? bigCircle.Plane.Normal : -bigCircle.Plane.Normal;
                GeoVector diry = dirz ^ dirx; // vertauscht, da Orientierung falsch war, nicht sicher ob es jetzt immer richtig ist, oder anders getestet werden muuu
#if DEBUG
                GeoObjectList dbgl = new GeoObjectList();
                //                dbgl.Add(elli);
                // dbgl.Add(endCurve);
                dbgl.Add(bigCircle);
                dbgl.Add(Line.TwoPoints(bigCircle.Center, bigCircle.Center + dirx));
                dbgl.Add(Line.TwoPoints(bigCircle.Center, bigCircle.Center + diry));
                dbgl.Add(Line.TwoPoints(bigCircle.Center, bigCircle.Center + dirz));

#endif
                // der Torus ist nicht eingeschränkt, und beginnt auch nicht bei elli. Das sollte aber keine Auswirkung haben
                ToroidalSurface res = new ToroidalSurface(bigCircle.Center, dirx, diry, dirz, bigCircle.MajorRadius, radius);
                return res;
            }

#if DEBUG
            if (along is IExplicitPCurve3D)
            {
                ExplicitPCurve3D epc3d = (along as IExplicitPCurve3D).GetExplicitPCurve3D();
                double[] splitpos = epc3d.GetCurvaturePositions(radius);
            }
#endif
            {
                ICurve falong = along.Clone();
                // if (!forward) falong.Reverse();
                Ellipse[] ellis = new Ellipse[80]; // erstmal fix
                for (int i = 0; i < ellis.Length; i++)
                {
                    double u = (double)i / (ellis.Length - 1);
                    if (!forward) u = 1.0 - u;
                    GeoVector diru = along.DirectionAt(u).Normalized;
                    GeoPoint pos = along.PointAt(u);
                    ellis[i] = Ellipse.Construct();
                    GeoVector majorAxis = radius * seem(u).Normalized;
                    GeoVector minorAxis = radius * (majorAxis ^ diru).Normalized;
                    Plane pln = new Plane(pos, majorAxis, minorAxis);
                    ellis[i].SetCirclePlaneCenterRadius(pln, pos, radius);
                }
                // endCurve = ellis[ellis.Length - 1];
                NurbsSurface ns = new NurbsSurface(ellis);
#if DEBUG
                Face dbgf = Face.MakeFace(ns, new SimpleShape(Border.MakeRectangle(0.25, 0.75, 0, 1)));
#endif
                return ns;
            }
        }
#if DEBUG
        public static Face[] PipeTest(ICurve crv, double radius)
        {
            List<Face> res = new List<Face>();
            if (crv is IExplicitPCurve3D)
            {
                GeoPoint[] pnts = new GeoPoint[20];
                for (int j = 0; j < pnts.Length; j++)
                {
                    pnts[j] = crv.PointAt((double)j / (pnts.Length - 1));
                }
                double maxDist;
                bool isLinear;
                Plane pln = Plane.FromPoints(pnts, out maxDist, out isLinear);
                ExplicitPCurve3D epc3d = (crv as IExplicitPCurve3D).GetExplicitPCurve3D();
                double[] splitpos = epc3d.GetCurvaturePositions(radius);
                Array.Sort(splitpos);
                double sp = 0.0;
                for (int i = 0; i < splitpos.Length + 1; i++)
                {
                    ICurve crvtr = crv.Clone();
                    double ep;
                    if (i < splitpos.Length) ep = crv.PositionOf(epc3d.PointAt(splitpos[i]));
                    else ep = 1.0;
                    crvtr.Trim(sp, ep);
                    sp = ep;
                    GeoVector startDir = crvtr.StartDirection.Normalized;
                    Ellipse elli = Ellipse.Construct();
                    GeoVector dirx = startDir ^ pln.Normal;
                    GeoVector diry = dirx ^ startDir;
                    elli.SetCirclePlaneCenterRadius(new Plane(crvtr.StartPoint, dirx, diry), crvtr.StartPoint, radius);
                    ISurface pipe = MakePipe(radius, crvtr, true, u => crvtr.DirectionAt(u) ^ pln.Normal);
                    res.Add(Face.MakeFace(pipe, new SimpleShape(Border.MakeRectangle(0.25, 0.75, 0, 1))));
                }
            }
            return res.ToArray();
        }
#endif
        private static Edge FindOffsetEdge(IEnumerable<Edge> edges, Face onFace, Edge toEdge, double dist)
        {   // eigentlich ist klar, welches die OffsetEdge ist, da die beiden Vertices gegeben sind. Aber es kann sein
            // dass es mehrere Edges gibt, die die beiden Vertices verbinden. In diesem Fall hier noch die richtige aussuchen.
            // Das liese sich aber auch einfacher durch die Richtung der Edge machen!!!
            Edge res = null;
            int count = 0;
            foreach (Edge edg in edges)
            {
                res = edg;
                ++count;
            }
            if (count == 1) return res; // dann ist es ja eindeutig
            if (count == 0) return null;
            double bestDist = double.MaxValue;
            foreach (Edge edg in edges)
            {
                GeoPoint mp = edg.Curve3D.PointAt(0.5);
                GeoPoint2D[] fpts = onFace.Surface.PerpendicularFoot(mp);
                ICurve2D c2d = toEdge.Curve2D(onFace);
                for (int i = 0; i < fpts.Length; i++)
                {
                    SurfaceHelper.AdjustPeriodic(onFace.Surface, onFace.Area.GetExtent(), ref fpts[i]);
                    double md = c2d.MinDistance(fpts[i]);
                    if (md < bestDist)
                    {
                        bestDist = md;
                        res = edg;
                    }
                }
            }
            return res;
        }

        /// <summary>
        /// Preliminary: Returns the enclosed part of the shell which is inside the border
        /// </summary>
        /// <param name="closedBorder"></param>
        /// <param name="precision"></param>
        /// <returns></returns>
        public Shell GetEnclosedPart(Path closedBorder, double precision)
        {
            SplitShellWithCurves ssc = new SplitShellWithCurves(this, closedBorder, precision);
            return ssc.GetInsidePart();

        }
        private class EdgeLengthComparer : IComparer<Edge>
        {
            public int Compare(Edge e1, Edge e2)
            {
                int res = e2.Curve3D.Length.CompareTo(e1.Curve3D.Length);
                if (res == 0) res = e1.GetHashCode().CompareTo(e2.GetHashCode()); // never return "equal" except for identical edges
                return res;
            }
        }
        private void OrientFaces(Set<Face> correctFaces, SortedDictionary<Edge, Face> toOrient)
        {
            List<KeyValuePair<Edge, Face>> l = new List<KeyValuePair<Edge, Face>>(toOrient);
            toOrient.Clear();
            foreach (KeyValuePair<Edge, Face> item in l)
            {
                if (item.Value != null && !correctFaces.Contains(item.Value))
                {
                    correctFaces.Add(item.Value);
                    if (!item.Key.IsOrientedConnection)
                    {
                        item.Value.MakeInverseOrientation();
                        foreach (Edge e in item.Value.AllEdges)
                        {
                            e.Orient();
                        }
                    }
                    for (int i = 0; i < item.Value.AllEdges.Length; i++)
                    {
                        if (item.Value.AllEdges[i].Curve3D != null)
                        {
                            Face fc = item.Value.AllEdges[i].OtherFace(item.Value);
                            if (!correctFaces.Contains(fc)) toOrient[item.Value.AllEdges[i]] = fc;
                        }
                    }
                }
            }
        }
#if DEBUG
        static public Stack<Face> orienting = new Stack<Face>();
#endif
        private void OrientFace(Set<Face> correctFaces, Face face, Edge correctEdge)
        {
            if (face == null) return;
#if DEBUG
            orienting.Push(face);
#endif
            correctFaces.Add(face);
            if (!correctEdge.IsOrientedConnection)
            {
                face.MakeInverseOrientation();
                foreach (Edge e in face.AllEdges)
                {
                    e.Orient();
                }
            }
            List<Edge> edgesByLength = new List<Edge>();
            for (int i = 0; i < face.AllEdges.Length; i++)
            {
                if (face.AllEdges[i].Curve3D != null) edgesByLength.Add(face.AllEdges[i]);
            }
            edgesByLength.Sort(delegate (Edge e1, Edge e2)
            {
                return e2.Curve3D.Length.CompareTo(e1.Curve3D.Length);
            });
            // start with the longest edges, these are more stable concerning the orientation
            foreach (Edge e in edgesByLength)
            {
                Face other = e.OtherFace(face);
                if (!correctFaces.Contains(other))
                {
                    OrientFace(correctFaces, other, e);
                }
            }
#if DEBUG
            orienting.Pop();
#endif
        }
        #region IGetSubShapes Members
        IGeoObject IGetSubShapes.GetEdge(int[] id, int index)
        {
            for (int i = 0; i < Edges.Length; ++i)
            {   // nicht über faces iterieren, denn beim Darstellen wird auch nicht über faces iteriert
                IGeoObject go = edges[i].Curve3D as IGeoObject;
                if (go != null && go.UniqueId == id[index])
                    return go;
            }
            return null; // sollte nicht vorkommen
        }
        IGeoObject IGetSubShapes.GetFace(int[] id, int index)
        {
            for (int i = 0; i < faces.Length; ++i)
            {
                if (faces[i].UniqueId == id[index])
                    return faces[i];
            }
            return null; // sollte nicht vorkommen
        }
        #endregion
        #region IGeoObjectOwner Members
        void IGeoObjectOwner.Remove(IGeoObject toRemove)
        {
            // Remove sollte nicht drankommen, es sei denn beim Zerlegen.
            // dann könnte es ein Problem mit dem Undo geben. Dort sollte besser gecloned werden
        }
        void IGeoObjectOwner.Add(IGeoObject toAdd)
        {
            // Add machen wir nur selbst, wenn das Objekt erzeugt wird, da bleibt hier nichts zu tun
        }
        #endregion
#if DEBUG
        internal DebuggerContainer DebugOrientation
        {
            get
            {
                DebuggerContainer res = new DebuggerContainer();
                ColorDef cd = new ColorDef("normal", Color.BlueViolet);
                BoundingCube bc = this.GetExtent(0.1);
                foreach (Face fc in Faces)
                {
                    res.Add(fc, fc.GetHashCode());
                    SimpleShape ss = fc.Area;
                    GeoPoint2D c = ss.GetExtent().GetCenter();
                    GeoPoint sp = fc.Surface.PointAt(c);
                    GeoPoint ep = sp + 0.05 * bc.Size * fc.Surface.GetNormal(c).Normalized;
                    Line l = Line.Construct();
                    l.SetTwoPoints(sp, ep);
                    l.ColorDef = cd;
                    res.Add(l, fc.GetHashCode());
                }
                return res;
            }
        }
        internal GeoObjectList DebugOpenEdges
        {
            get
            {
                Edge[] oe = OpenEdges;
                GeoObjectList res = new GeoObjectList();
                for (int i = 0; i < oe.Length; i++)
                {
                    if (oe[i].Curve3D != null) res.Add(oe[i].Curve3D as IGeoObject);
                }
                return res;
            }
        }
        public bool isOK()
        {
            AssertOutwardOrientation();
            return HasFreeEdges();
        }
        public new void Debug()
        {
            Edge[] oe = OpenEdges;
            Set<Edge> edgeset = new Set<Edge>();
            foreach (Face fc in faces)
            {
                edgeset.AddMany(fc.AllEdges);
            }
            edgeset.RemoveMany(oe);

            GeoObjectList res = new GeoObjectList();
            for (int i = 0; i < oe.Length; i++)
            {
                if (oe[i].Curve3D != null)
                {
                    foreach (Edge edge in edgeset)
                    {
                        if (edge.Curve3D != null)
                        {
                            bool rev;
                            if (Curves.SameGeometry(oe[i].Curve3D, edge.Curve3D, 1, out rev))
                            {

                            }
                        }
                    }
                }
            }
        }
#endif
        internal void ReplaceFace(int index, List<Face> replaceBy, double precision)
        {
            ReplaceFace(faces[index], replaceBy.ToArray(), precision);
        }
        internal void ReplaceFace(Face toReplace, Face[] replaceBy, double precision)
        {
            Set<Face> affectedFaces = new Set<Face>();
            Edge[] allEdges = toReplace.AllEdges;
            List<Edge> disconnectedEdges = new List<Edge>(); // das sind die aufgebrochenen Kanten
            foreach (Edge edg in allEdges)
            {
                if (edg.PrimaryFace == null) continue;
                edg.RemoveFace(toReplace); // damit wird ggf. Secondary zu Primary
                                           // ggf. bei Säumen entsteht ein leeres edge
                if (edg.PrimaryFace != null && edg.PrimaryFace != toReplace)
                {
                    affectedFaces.Add(edg.PrimaryFace);
                    disconnectedEdges.Add(edg);
                }
                //if (edg.PrimaryFace==null)
                //{   // geht nicht, da Vertex1 den Vertex macht
                //    if (edg.Vertex1 != null) edg.Vertex1.RemoveEdge(edg);
                //    if (edg.Vertex2 != null) edg.Vertex2.RemoveEdge(edg);
                //}
            }
            affectedFaces.AddMany(replaceBy);
#if DEBUG
            DebuggerContainer dcf = new CADability.DebuggerContainer();
            foreach (Face fce in affectedFaces)
            {
                dcf.Add(fce, fce.GetHashCode());
            }
#endif
            connectFaces(affectedFaces.ToArray(), precision); // jetzt sind alle Kanten verbunden
                                                              // aber die Kanten in replaceBy, die aufgebrochen wurden, haben noch innere Punkte, die nicht verbunden sind
                                                              // auf der einen Seite haben wir die aufgebrochenen Kanten, die nicht wieder verbunden wurden, weli ihr Gegenstück
                                                              // geteilt wurde, auf der anderen Seite die offenen kanten von replaceby, das sind die geteilten
            Set<Vertex> splitVertices = new Set<Vertex>(); // das sollen alle vertices sein, die in replaceBy sind, aber nicht in affected
            foreach (Face fc in replaceBy)
            {
                foreach (Edge edg in fc.Edges)
                {
                    if (edg.SecondaryFace == null)
                    {
                        splitVertices.Add(edg.Vertex1); // zunächst alle vertices von den offenen kanten hinzufügen
                        splitVertices.Add(edg.Vertex2);
                    }
                }
            }
            for (int i = disconnectedEdges.Count - 1; i >= 0; --i)
            {
                if (disconnectedEdges[i].SecondaryFace != null) disconnectedEdges.RemoveAt(i); // alle rausnehmen, die wieder verbunden wurden
                else
                {
                    splitVertices.Remove(disconnectedEdges[i].Vertex1);
                    splitVertices.Remove(disconnectedEdges[i].Vertex2);
                }
            }
#if DEBUG
            DebuggerContainer dcd = new DebuggerContainer();
            for (int i = 0; i < disconnectedEdges.Count; i++)
            {
                dcd.Add(disconnectedEdges[i].Curve3D as IGeoObject, disconnectedEdges[i].GetHashCode());
            }
            foreach (Vertex vtx in splitVertices)
            {
                Point pt = Point.Construct();
                pt.Symbol = PointSymbol.Cross;
                pt.Location = vtx.Position;
                dcd.Add(pt, vtx.GetHashCode());
            }
#endif
            SortedList<double, Vertex>[] edgesToSplit = new SortedList<double, CADability.Vertex>[disconnectedEdges.Count]; // paralleles Array: für jede kante eine Liste der Aufbrechpunkte
            for (int i = 0; i < edgesToSplit.Length; i++)
            {
                edgesToSplit[i] = new SortedList<double, CADability.Vertex>();
            }
            foreach (Vertex vtx in splitVertices)
            {
                List<int> closeEdges = new List<int>();
                for (int i = 0; i < disconnectedEdges.Count; i++)
                {
                    if (disconnectedEdges[i].Curve3D != null)
                    {
                        double d = disconnectedEdges[i].Curve3D.DistanceTo(vtx.Position);
                        if (d < precision)
                        {
                            closeEdges.Add(i);
                        }
                    }
                }
                for (int i = 0; i < closeEdges.Count; i++)
                {
                    double pos = disconnectedEdges[closeEdges[i]].Curve3D.PositionOf(vtx.Position);
                    if (pos > 1e-6 && pos < 1 - 1e-6)
                    {
                        edgesToSplit[closeEdges[i]].Add(pos, vtx);
                    }
                }
            }
            for (int i = 0; i < edgesToSplit.Length; i++)
            {
                if (edgesToSplit[i].Count > 0)
                {
                    disconnectedEdges[i].Split(edgesToSplit[i], precision);
                }
            }
#if DEBUG
            foreach (Vertex vtx in splitVertices)
            {
                // 
                DebuggerContainer dcsv = new CADability.DebuggerContainer();
                Edge[] edgs = vtx.Edges;
                for (int i = 0; i < edgs.Length; i++)
                {
                    if (edgs[i].Curve3D != null) dcsv.Add(edgs[i].Curve3D as IGeoObject, edgs[i].GetHashCode());
                }
            }
#endif
            foreach (Vertex vtx in splitVertices)
            {   // das sind ja die Zwischenpunkte, die bereits zusammenhängen, die Kanten müssen jetzt noch zusammen finden
                Edge[] edgs = vtx.Edges;
                for (int i = 0; i < edgs.Length; i++)
                {
                    if (edgs[i].SecondaryFace == null)
                    {
                        for (int j = i + 1; j < edgs.Length; j++)
                        {
                            if (edgs[j].SecondaryFace == null)
                            {
                                if (edgs[i].OtherVertex(vtx) == edgs[j].OtherVertex(vtx))
                                {   // auch die gegenüberliegenden Kanten stimmen überein, es können aber immer noch zwei (geometrisch) unterschiedeliche Kanten sein
                                    if ((edgs[j].Curve3D == null && edgs[i].Curve3D == null) || edgs[j].Curve3D.DistanceTo(edgs[i].Curve3D.PointAt(0.5)) < precision) // SameGeometry ist schlecht
                                    {
                                        edgs[j].PrimaryFace.ReplaceEdge(edgs[j], edgs[i]);
                                    }
                                    else
                                    {

                                    }
                                }
                            }
                        }
                    }
                }
            }

            List<Face> allfaces = new List<Face>(faces);
            allfaces.Remove(toReplace);
            allfaces.AddRange(replaceBy);
            this.faces = allfaces.ToArray(); // changing muss außerhalb passieren

            //List<Edge> edges = new List<Edge>(toReplace.AllEdges);
            //BoundingCube ext = toReplace.GetExtent(0.0);
            //OctTree<Vertex> ovtx = new OctTree<Vertex>(ext, ext.Size * 1e-6);
            //Vertex[] vtx = toReplace.Vertices;
            //for (int i = 0; i < vtx.Length; i++)
            //{
            //    ovtx.AddObject(vtx[i]);
            //}
            //List<Vertex> newVertices = new List<Vertex>();
            //for (int i = 0; i < replaceBy.Count; i++)
            //{
            //    vtx = replaceBy[i].Vertices;
            //    for (int j = 0; j < vtx.Length; j++)
            //    {
            //        Vertex[] hits = ovtx.GetObjectsCloseTo(vtx[j]);
            //        bool merged = false;
            //        for (int k = 0; k < hits.Length; k++)
            //        {
            //            if ((hits[k].Position | vtx[j].Position)<ovtx.precision)
            //            {
            //                hits[k].MergeWith(vtx[j]);
            //                merged = true;
            //                break;
            //            }
            //        }
            //        if (!merged) newVertices.Add(vtx[j]);
            //    }
            //}
        }
        internal static void connectFaces(Face[] toConnect, double precision)
        {
            Set<Vertex> toAdd = new Set<Vertex>(); // alle vertices von offenen Kanten
            BoundingCube ext = BoundingCube.EmptyBoundingCube;
            for (int i = 0; i < toConnect.Length; i++)
            {
                foreach (Edge edg in toConnect[i].Edges)
                {
                    if (edg.SecondaryFace == null)
                    {
                        toAdd.Add(edg.Vertex1);
                        toAdd.Add(edg.Vertex2);
                        ext.MinMax(edg.Vertex1.Position);
                        ext.MinMax(edg.Vertex2.Position);
                    }
                }
            }
            OctTree<Vertex> ovtx = new OctTree<Vertex>(ext, ext.Size * 1e-8);
            foreach (Vertex vtx in toAdd)
            {
                ovtx.AddObject(vtx);
            }
            Set<Vertex> allVertices = new Set<Vertex>(ovtx.GetAllObjects());
            Set<Vertex> mergedVertices = new Set<Vertex>();
            while (allVertices.Count > 0)
            {
                Vertex vtx = allVertices.GetAny();
                allVertices.Remove(vtx);
                Vertex[] hits = ovtx.GetObjectsFromBox(new BoundingCube(vtx.Position, precision), null);
                for (int i = 0; i < hits.Length; i++)
                {
                    if (hits[i] != vtx && (hits[i].Position | vtx.Position) < precision)
                    {
                        vtx.MergeWith(hits[i]);
                        allVertices.Remove(hits[i]);
                        mergedVertices.Add(vtx);
                    }
                }
            }
#if DEBUG
            DebuggerContainer dc = new DebuggerContainer();
            for (int i = 0; i < toConnect.Length; i++)
            {
                dc.Add(toConnect[i].Clone(), toConnect[i].GetHashCode()); // clone, so it doesn't change the color
            }
            foreach (Vertex vtx in mergedVertices)
            {
                Point pt = Point.Construct();
                pt.Symbol = PointSymbol.Cross;
                pt.Location = vtx.Position;
                dc.Add(pt, vtx.GetHashCode());
            }
            DebuggerContainer dc1 = new DebuggerContainer();
            foreach (Vertex vtx in toAdd)
            {
                Set<Edge> toShow = new Set<Edge>();
                foreach (Edge edg in vtx.AllEdges)
                {
                    toShow.Add(edg);
                }
                foreach (Edge edg in toShow)
                {
                    if (edg.Curve3D != null) dc1.Add(edg.Curve3D as IGeoObject, edg.GetHashCode());
                }
            }
#endif
            // jetzt sind alle vertices mit gleicher Position zusammengefasst
            // die so zusammengefassten können jetzt für die Zusammenfassung von Edges verwendet werden
            foreach (Vertex vtx in mergedVertices)
            {
                Edge[] allEdges = vtx.Edges;
#if DEBUG
                DebuggerContainer dcae = new DebuggerContainer();
                for (int i = 0; i < allEdges.Length; i++)
                {
                    if (allEdges[i].Curve3D != null) dcae.Add(allEdges[i].Curve3D as IGeoObject, allEdges[i].GetHashCode());
                }
#endif
                for (int i = 0; i < allEdges.Length; i++)
                {
                    if (allEdges[i].SecondaryFace != null) continue; // ist schon verbunden
                    for (int j = i + 1; j < allEdges.Length; j++)
                    {
                        if (allEdges[j].SecondaryFace != null) continue; // ist schon verbunden
                        if (allEdges[i].OtherVertex(vtx) == allEdges[j].OtherVertex(vtx))
                        {
                            if (allEdges[i].Curve3D == null && allEdges[j].Curve3D == null) allEdges[j].PrimaryFace.ReplaceEdge(allEdges[j], allEdges[i]);
                            // else if (allEdges[i].Curve3D.SameGeometry(allEdges[j].Curve3D, precision)) // SameGeometry ist schlecht, Ellipse erwartet Ellipse als Partner, akzeptiert nicht BSpline
                            else if (allEdges[i].Curve3D != null && allEdges[j].Curve3D != null && allEdges[i].Curve3D.DistanceTo(allEdges[j].Curve3D.PointAt(0.5)) < precision)
                            {
                                allEdges[j].PrimaryFace.ReplaceEdge(allEdges[j], allEdges[i]);
                            }
                            else
                            {
                            }
                        }
                    }
                }
            }
#if DEBUG
            DebuggerContainer dc2 = new DebuggerContainer();
            foreach (Vertex vtx in toAdd)
            {
                Set<Edge> toShow = new Set<Edge>();
                foreach (Edge edg in vtx.AllEdges)
                {
                    toShow.Add(edg);
                }
                foreach (Edge edg in toShow)
                {
                    if (edg.SecondaryFace == null && edg.Curve3D != null) dc2.Add(edg.Curve3D as IGeoObject, edg.GetHashCode());
                }
            }
#endif

        }
        internal void TryConnectOpenEdges()
        {
            Set<Edge> openEdges = new Set<Edge>(OpenEdges);
            foreach (Edge openEdge in openEdges)
            {
                if (openEdge.PrimaryFace == null) continue; // has already been merged
                if (openEdge.Vertex1 == openEdge.Vertex2)
                {
                    if (openEdge.Curve3D != null && openEdge.Curve3D.Length < Precision.eps)
                    {
                        openEdge.PrimaryFace.RemoveEdge(openEdge);
                    }
                    continue; // don't connect poles
                }
                foreach (Edge edg in Vertex.ConnectingEdges(openEdge.Vertex1, openEdge.Vertex2))
                {
                    if (edg == openEdge) continue;
                    if (!openEdges.Contains(edg)) continue;
                    if ((edg.PrimaryFace == openEdge.PrimaryFace || edg.SecondaryFace == openEdge.PrimaryFace) && BRepOperation.SameEdge(edg, openEdge, Precision.eps))
                    {
                        edg.RemoveFace(openEdge.PrimaryFace);
                        openEdge.PrimaryFace.ReplaceEdge(openEdge, edg);
                        break;
                    }
                    else if (edg.SecondaryFace == null && openEdge.SecondaryFace == null && BRepOperation.SameEdge(edg, openEdge, Precision.eps))
                    {
                        openEdge.MergeWith(edg);
                        edg.DisconnectFromFace(openEdge.SecondaryFace);
                        break;
                    }
                }
            }
        }
        public void SplitPeriodicFaces()
        {   // die BReps gehen viel einfacher, wenn es keine inneren Nähte gibt
            // das soll nach BRepIntersection...
            using (new Changing(this, "SetFaces", faces.Clone()))
            {
                double precision = GetExtent(0).Size * 1e-5;
                for (int i = faces.Length - 1; i >= 0; --i)
                {
                    List<Face> splittedFaces = faces[i].SplitPeriodicFace();
                    if (splittedFaces != null)
                    {
                        ReplaceFace(i, splittedFaces, precision);
#if DEBUG
                        foreach (Edge edg in OpenEdges)
                        {
                            if (edg.Curve3D != null)
                            {
                            }
                        }
#endif
                    }
                }
                this.edges = null;
            }
        }

        internal void SplitSingularFaces()
        {   // die BReps gehen viel einfacher, wenn es keine inneren Nähte gibt
            // das soll nach BRepIntersection...
            using (new Changing(this, "SetFaces", faces.Clone()))
            {
                double precision = GetExtent(0).Size * 1e-6;
                for (int i = faces.Length - 1; i >= 0; --i)
                {
                    List<Face> splittedFaces = faces[i].SplitSingularFace();
                    if (splittedFaces != null)
                    {
                        ReplaceFace(i, splittedFaces, precision);
                    }
                }
                this.edges = null;
#if DEBUG
                foreach (Edge edg in OpenEdges)
                {
                    if (edg.Curve3D != null)
                    {
                    }
                }
#endif
            }
        }
        //internal void SplitPeriodicFaces()
        //{   // die BReps gehen viel einfacher, wenn es keine inneren Nähte gibt
        //    // das soll nach BRepIntersection...
        //    List<Face> res = null;
        //    for (int i = faces.Length - 1; i >= 0; --i)
        //    {
        //        List<Face> splitted = faces[i].SplitSeam();
        //        if (splitted != null)
        //        {
        //            if (res == null) res = new List<Face>(faces);
        //            res.RemoveAt(i);
        //            res.AddRange(splitted);
        //        }
        //    }
        //    if (res != null)
        //    {
        //        faces = res.ToArray();
        //        edges = null;
        //    }
        //}
        internal void OrientAndSplit()
        {
            if (!orientedAndSeamless)
            {
                List<Face> res = new List<Face>();
                bool splitted = false;
                for (int i = faces.Length - 1; i >= 0; --i)
                {
                    List<Face> splittedFaces = faces[i].SplitPeriodicFace();
                    if (splittedFaces != null)
                    {
                        res.AddRange(splittedFaces);
                        splitted = true;
                    }
                    else
                    {
                        res.Add(faces[i].Clone() as Face);
                    }
                }
                if (splitted)
                {
                    GeoObjectList sewed = Make3D.SewFacesAndShells(new GeoObjectList(res.ToArray()));
                    if (sewed != null && sewed.Count == 1)
                    {
                        if (sewed[0] is Shell)
                        {
                            SetFaces((sewed[0] as Shell).Faces);
                        }
                        if (sewed[0] is Solid)
                        {
                            SetFaces((sewed[0] as Solid).Shells[0].Faces);
                        }
                    }
                }

            }
            orientedAndSeamless = true;
        }
        internal void ReduceFaces(double precision)
        {
            Set<Face> facesset = new Set<Face>(faces); // die Faces ändern sich ggf.
                                                       // zuerst mal degenerierte Edges entfernen:
            Edge[] alledges = this.Edges;
            OrderedMultiDictionary<DoubleVertexKey, Edge> dict = new OrderedMultiDictionary<DoubleVertexKey, Edge>(true);
            foreach (Edge e in alledges)
            {
                dict.Add(new DoubleVertexKey(e.Vertex1, e.Vertex2), e);
            }
            // Wenn eine Verbindung zweier Vertices öfter vorkommt, dann testen, ob geometrisch identisch
            foreach (KeyValuePair<DoubleVertexKey, ICollection<Edge>> kv in dict)
            {
                if (kv.Value.Count > 1)
                {
                    List<Edge> duplicatEdges = new List<Edge>(kv.Value);
                    for (int i = 0; i < duplicatEdges.Count - 1; i++)
                    {
                        for (int j = i + 1; j < duplicatEdges.Count; j++)
                        {
                            if (Edge.IsGeometricallyEqual(duplicatEdges[i], duplicatEdges[j], true, false, precision))
                            {   // zwei Kanten sind identisch:
                                // sie haben ein gemeinsames face und zwei andere. Das gemeinsame Face wird
                                // eliminiert 
                                Face common, first, second;
                                if (duplicatEdges[i].PrimaryFace == duplicatEdges[j].PrimaryFace)
                                {
                                    common = duplicatEdges[j].PrimaryFace;
                                    first = duplicatEdges[i].SecondaryFace;
                                    second = duplicatEdges[j].SecondaryFace;
                                }
                                else if (duplicatEdges[i].PrimaryFace == duplicatEdges[j].SecondaryFace)
                                {
                                    common = duplicatEdges[j].SecondaryFace;
                                    first = duplicatEdges[i].SecondaryFace;
                                    second = duplicatEdges[j].PrimaryFace;
                                }
                                else if (duplicatEdges[i].SecondaryFace == duplicatEdges[j].PrimaryFace)
                                {
                                    common = duplicatEdges[j].PrimaryFace;
                                    first = duplicatEdges[i].PrimaryFace;
                                    second = duplicatEdges[j].SecondaryFace;
                                }
                                else if (duplicatEdges[i].SecondaryFace == duplicatEdges[j].SecondaryFace)
                                {
                                    common = duplicatEdges[j].SecondaryFace;
                                    first = duplicatEdges[i].PrimaryFace;
                                    second = duplicatEdges[j].PrimaryFace;
                                }
                                else throw new ApplicationException("strange duplicate edges");
                                duplicatEdges[i].ReplaceFace(common, second);
                                // nicht sicher ob die Orientierung stimmt...
                                duplicatEdges[i].RecalcCurve2D(second);
                                second.ReplaceEdge(duplicatEdges[j], new Edge[] { duplicatEdges[i] });
                                facesset.Remove(common);
                            }
                        }
                    }
                }
            }
            faces = facesset.ToArray();
            edges = null;
            // return; // der rest muss noch besser implementiert werden
            // TODO: hier wird nicht berücksichtigt, dass auch ein Face and das Loch eines anderen
            // von innen her anstossen kann...
            Set<Edge> removededges = new Set<Edge>();
            for (int i = 0; i < alledges.Length; ++i)
            {
                Edge e = alledges[i];
                if (removededges.Contains(e)) continue; // schon beseitigt
                ModOp2D firstToSecond;
                if (e.SecondaryFace != null && e.PrimaryFace.Surface.SameGeometry(e.PrimaryFace.GetUVBounds(), e.SecondaryFace.Surface, e.SecondaryFace.GetUVBounds(), precision, out firstToSecond))
                {
                    ModOp2D secondToFirst = firstToSecond.GetInverse();
                    Set<Edge> prim = new Set<Edge>(e.PrimaryFace.OutlineEdges);
                    Set<Edge> secd = new Set<Edge>(e.SecondaryFace.OutlineEdges);
                    Set<Edge> cmn = prim.Intersection(secd); // gemeinsame edges zwischen den beiden
                    if (cmn.Count > 0)
                    {
                        // Alle aus den beiden outlineedges, die nicht in cmn sind
                        // nach dem Startvertex sortieren und eine zusammenhängende outline erzeugen.
                        // beim Iterieren über alledges ist allerdings noch zu beachten, dass bereits entfernte nicht mehr verwendet werden
                        removededges.AddMany(cmn); // die nicht mehr testen, schon entfernt
                        OrderedMultiDictionary<Vertex, Edge> sortedEdges = new OrderedMultiDictionary<Vertex, Edge>(false);
                        foreach (Edge ee in prim)
                        {
                            if (!cmn.Contains(ee)) sortedEdges.Add(ee.StartVertex(e.PrimaryFace), ee);
                        }
                        foreach (Edge ee in secd)
                        {
                            if (!cmn.Contains(ee)) sortedEdges.Add(ee.StartVertex(e.SecondaryFace), ee);
                        }
                        List<List<Edge>> outlines = new List<List<Edge>>();
                        while (sortedEdges.Count > 0)
                        {
                            List<Edge> ol = new List<Edge>();
                            KeyValuePair<Vertex, Edge> first = sortedEdges.FirstItem;
                            Edge toAdd = first.Value;
                            Vertex endVertex = first.Key;
                            while (toAdd != null)
                            {
                                sortedEdges.Remove(endVertex);
                                if (prim.Contains(toAdd))
                                {
                                    endVertex = toAdd.EndVertex(e.PrimaryFace);
                                }
                                else
                                {
                                    endVertex = toAdd.EndVertex(e.SecondaryFace);
                                    toAdd.ModifyCurve2D(e.SecondaryFace, null, secondToFirst);
                                    toAdd.ReplaceFace(e.SecondaryFace, e.PrimaryFace);
                                }
                                ol.Add(toAdd);
                                toAdd = sortedEdges.Item(endVertex); // kann auch null geben
                            }
                            outlines.Add(ol);
                        }
                        if (outlines.Count > 1)
                        {
                            throw new NotImplementedException("holes in Reducefaces");
                        }
                        e.PrimaryFace.Set(e.PrimaryFace.Surface, outlines[0].ToArray(), null);
                        // TODO: Holes zusammenstellen: alle holes von e.Primaryface und e.SecondaryFace und die
                        // holes von oulines
                        facesset.Remove(e.SecondaryFace);
                    }

                }
            }
            if (this.faces.Length > facesset.Count)
            {
                this.SetFaces(facesset.ToArray());
            }
        }

        internal bool CheckConsistency()
        {
            for (int i = 0; i < Faces.Length; i++)
            {
                if (!Faces[i].CheckConsistency()) return false;

            }
            return true;
        }
        /// <summary>
        /// Removes the provided <paramref name="face"/> from the shell. Connecting edges are seperated
        /// </summary>
        /// <param name="face"></param>
        internal void RemoveFace(Face face)
        {
            // heraustrennen des Faces: die betreffenden Kanten dieser Shell werden zu offenen Kanten
            using (new Changing(this, "AddFace", face))
            {
                Set<Face> remainingFaces = new Set<Face>(faces);
                remainingFaces.Remove(face);
                faces = remainingFaces.ToArray();
                Edge[] dumy = OpenEdges; // this disconnects the common edges
                edges = null; // to force recalculation
            }
        }
        internal void AddFace(Face face)
        {
            // das neue Face hat eine oder mehrere Kanten gemeinsam mit der Shell
            // es kann sich auch um Teilstücke der Kanten handeln
            // aber eine Kante des neuen Faces kann nicht mehrere Kanten der Shell überdecken
            using (new Changing(this, "RemoveFace", face))
            {
                // zuerst schauen, ob irgendwelche Kanten gesplittet werden müssen
                // bei SplitCommonFace ist das nicht der Fall, da bereits BRepoperation die Kanten splittet
                // somit ist der folgende Abschnitt noch nicht getestet
                Set<Edge> freeEdges = new Set<Edge>(OpenEdges); // ggf. als OctTree, ber Edge ist nicht OctTreeInsertable
                Dictionary<Edge, SortedList<double, Vertex>> edgesToSplit = new Dictionary<Edge, SortedList<double, Vertex>>();
                foreach (Edge edge in face.Edges)
                {
                    foreach (Edge freeEdge in freeEdges)
                    {
                        double from1, to1, from2, to2;
                        if (Curves.Overlapping(edge.Curve3D, freeEdge.Curve3D, Precision.eps, out from1, out to1, out from2, out to2))
                        {
                            // from1 und to1 müssen immer 0 oder 1 sein, da "face" mit einer Kante nicht über 2 Kanten geht
                            if ((from2 > 1e-6) && (from2 < 1.0 - 1e-6))
                            {
                                SortedList<double, Vertex> listToinsert;
                                if (!edgesToSplit.TryGetValue(freeEdge, out listToinsert))
                                {
                                    edgesToSplit[freeEdge] = listToinsert = new SortedList<double, Vertex>();
                                }
                                listToinsert[from2] = edge.Vertex1;
                            }
                            if ((to2 > 1e-6) && (to2 < 1.0 - 1e-6))
                            {
                                SortedList<double, Vertex> listToinsert;
                                if (!edgesToSplit.TryGetValue(freeEdge, out listToinsert))
                                {
                                    edgesToSplit[freeEdge] = listToinsert = new SortedList<double, Vertex>();
                                }
                                listToinsert[to2] = edge.Vertex2;
                            }
                        }
                    }
                }
                foreach (KeyValuePair<Edge, SortedList<double, Vertex>> kv in edgesToSplit)
                {
                    kv.Key.Split(kv.Value, Precision.eps);
                }
                // jetzt entprechen sich Kanten entweder komplett oder garnicht
                freeEdges = new Set<Edge>(OpenEdges); // ggf. als OctTree, ber Edge ist nicht OctTreeInsertable
                foreach (Edge edge in face.Edges)
                {
                    foreach (Edge freeEdge in freeEdges)
                    {
                        double from1, to1, from2, to2;
                        if (Curves.Overlapping(edge.Curve3D, freeEdge.Curve3D, Precision.eps, out from1, out to1, out from2, out to2))
                        {
                            // die Richtung hängt davon ab. ob die Faces gleich oder verschieden orientiert sind
                            // die erste muss die kürzere sein
                            if ((from2 == 0.0) && (to2 == 1.0))
                            {
                                freeEdge.PrimaryFace.collateEdges(freeEdge, edge, face);
                                freeEdges.Remove(freeEdge);
                                break;
                            }
                            else if ((from2 == 1.0) && (to2 == 0.0))
                            {   // vorwärts/rückwwärts, was gibt es da zu beachten?
                                freeEdge.PrimaryFace.collateEdges(freeEdge, edge, face);
                                freeEdges.Remove(freeEdge);
                                break;
                            }
                            // fehlen noch die Aufteilungen
                        }
                    }
                }
                Face[] newFaces = new Face[faces.Length + 1];
                faces.CopyTo(newFaces, 0);
                newFaces[newFaces.Length - 1] = face;
                faces = newFaces;
                edges = null;
            }
        }
        internal void Repair()
        {
            foreach (Face fc in Faces)
            {
                fc.Repair();
            }
        }
        internal void Repair(double precision)
        {
            // RecalcVertices(); // this doesn't change coordinates
            // we assume everything is topologically ok, i.e. all edges are connected and have common vertices
            foreach (Vertex vtx in Vertices)
            {
                Set<Face> involvedFaces = new Set<Face>();
                foreach (Edge edg in vtx.AllEdges)
                {
                    if (edg.PrimaryFace != null) involvedFaces.Add(edg.PrimaryFace);
                    if (edg.SecondaryFace != null) involvedFaces.Add(edg.SecondaryFace);
                }
                bool needsFixing = false;
                foreach (Face fce in involvedFaces)
                {
                    GeoPoint2D uv = fce.Surface.PositionOf(vtx.Position);
                    if ((fce.Surface.PointAt(uv) | vtx.Position) > precision) needsFixing = true;
                }
                if (needsFixing)
                {
                    if (involvedFaces.Count > 2)
                    {   // in most cases, there are 3 Faces involved. 
                        Face[] fa = involvedFaces.ToArray();
                        GeoPoint ip = vtx.Position;
                        if (Surfaces.NewtonIntersect(fa[0].Surface, fa[0].Domain, fa[1].Surface, fa[1].Domain, fa[2].Surface, fa[2].Domain, ref ip, out GeoPoint2D uv0, out GeoPoint2D uv1, out GeoPoint2D uv2))
                        {
                            vtx.Position = ip;
                            vtx.AddPositionOnFace(fa[0], uv0); // also overwrites existing value
                            vtx.AddPositionOnFace(fa[1], uv1);
                            vtx.AddPositionOnFace(fa[2], uv2);
                        }
                        if (fa.Length > 3)
                        {   // more than 3 faces involved (like in the tip of a four sided pyramid): 
                            // here we would need a more sophisticated approach to find the best solution. Maybe even to modify a surface
                        }
                    }
                }
            }
            foreach (Edge edg in this.Edges)
            {
                if (edg.Curve3D != null)
                {
                    double d = 0.0;
                    for (int i = 0; i < 6; i++)
                    {
                        GeoPoint2D uv = edg.Curve2D(edg.PrimaryFace).PointAt(i / 5.0);
                        d += edg.Curve3D.DistanceTo(edg.PrimaryFace.Surface.PointAt(uv));
                        if (edg.SecondaryFace != null)
                        {
                            uv = edg.Curve2D(edg.SecondaryFace).PointAt(i / 5.0);
                            d += edg.Curve3D.DistanceTo(edg.SecondaryFace.Surface.PointAt(uv));
                        }
                    }
                    if (d > precision && edg.SecondaryFace != null)
                    {
                        // the curves of the edge need to be fixed
                        GeoPoint[] pnts = new GeoPoint[10];
                        for (int i = 1; i < 9; i++)
                        {
                            pnts[i] = edg.Curve3D.PointAt(i / 9.0);
                        }
                        pnts[0] = edg.Vertex1.Position;
                        pnts[9] = edg.Vertex2.Position;
                        InterpolatedDualSurfaceCurve ipdsc = new InterpolatedDualSurfaceCurve(edg.PrimaryFace.Surface, edg.PrimaryFace.Domain, edg.SecondaryFace.Surface, edg.SecondaryFace.Domain, pnts);
                        edg.Curve3D = ipdsc;
                        edg.PrimaryCurve2D = ipdsc.CurveOnSurface1;
                        edg.SecondaryCurve2D = ipdsc.CurveOnSurface2;
                        if (!edg.Forward(edg.PrimaryFace)) edg.PrimaryCurve2D.Reverse();
                        if (!edg.Forward(edg.SecondaryFace)) edg.SecondaryCurve2D.Reverse();
                    }
                }
            }
        }
        public void RecalcVertices()
        {
            BoundingCube ext = GetExtent(0.0);
            ext.Expand(ext.Size * 1e-3);
            double prec = ext.Size * 1e-8;
            OctTree<Vertex> vertexOctTree = new OctTree<Vertex>(ext, prec);
            Vertex[] allVertices = this.Vertices;
            for (int i = 0; i < allVertices.Length; i++)
            {
                bool duplicateFound = false;
                Vertex[] close = vertexOctTree.GetObjectsFromBox(new BoundingCube(allVertices[i].Position, prec));
                for (int j = 0; j < close.Length; j++)
                {
                    if ((allVertices[i].Position | close[j].Position) < prec)
                    {
                        close[j].MergeWith(allVertices[i]);
                        duplicateFound = true;
                        break;
                    }
                }
                if (!duplicateFound) vertexOctTree.AddObject(allVertices[i]);
            }
            foreach (Face fc in Faces)
            {
                fc.RepairConnected();
            }
        }
        internal void RecalcEdges()
        {   // hier sollten alle Edges neu justiert werden. 
            // Erstmal nur einen Fehler beheben
            foreach (Edge edg in Edges)
            {
                if (edg.PrimaryFace != null && edg.SecondaryFace != null)
                {
                    if (edg.PrimaryFace.Surface is PlaneSurface && edg.SecondaryFace.Surface is PlaneSurface)
                    {
                        if (!(edg.Curve3D is Line && edg.PrimaryCurve2D is Line2D && edg.SecondaryCurve2D is Line2D))
                        {
                            if (!Precision.SameDirection(edg.PrimaryFace.Surface.GetNormal(edg.Vertex1.GetPositionOnFace(edg.PrimaryFace)), edg.SecondaryFace.Surface.GetNormal(edg.Vertex1.GetPositionOnFace(edg.SecondaryFace)), false))
                            {
                                edg.Curve3D = Line.TwoPoints(edg.Vertex1.Position, edg.Vertex2.Position);
                                edg.RecalcCurve2D(edg.PrimaryFace);
                                edg.RecalcCurve2D(edg.SecondaryFace);
                            }
                        }
                    }
                }
                if (edg.Curve3D != null)
                {
                    if (edg.SecondaryFace != null && (edg.Forward(edg.PrimaryFace) == edg.Forward(edg.SecondaryFace)))
                    {
                        edg.Orient();
                    }

                    if ((edg.Curve3D.StartPoint | edg.Vertex1.Position) > Precision.eps)
                    {
                        edg.Curve3D.StartPoint = edg.Vertex1.Position;
                    }
                    if ((edg.Curve3D.EndPoint | edg.Vertex2.Position) > Precision.eps)
                    {
                        edg.Curve3D.EndPoint = edg.Vertex2.Position;
                    }
                    if (edg.Curve3D is InterpolatedDualSurfaceCurve)
                    {
                        (edg.Curve3D as InterpolatedDualSurfaceCurve).Repair(edg.PrimaryFace.Area.GetExtent(), edg.SecondaryFace.Area.GetExtent());
                    }
                }
            }
        }
        /// <summary>
        /// Adjacent faces that share a common surface are replaced by a single face
        /// </summary>
        /// <returns>Number of faces that have been combined</returns>
        public int CombineConnectedFaces()
        {
#if DEBUG
            if (!CheckConsistency()) { }
#endif
            RecalcVertices();
#if DEBUG
            if (!CheckConsistency()) { }
#endif
            IGeoObjectImpl changingObject = this;
            if (Owner is Solid) changingObject = Owner as IGeoObjectImpl;
            // hier wird ein deep clone der Faces gemacht, denn copyGeometry geht nicht als undo, da die Anzahl der Faces sich ändert
            Face[] clonedFaces = (Face[])faces.Clone();
            Dictionary<Edge, Edge> clonedEdges = new Dictionary<Edge, Edge>();
            Dictionary<Vertex, Vertex> clonedVertices = new Dictionary<Vertex, Vertex>();
            for (int i = 0; i < clonedFaces.Length; i++)
            {
                clonedFaces[i] = clonedFaces[i].Clone(clonedEdges, clonedVertices);
            }
            Set<Face> allFaces = new Set<Face>(faces);
#if DEBUG
            Face dbgface = null;
            foreach (Face fcdbg in allFaces)
            {
                // if (fcdbg.GetHashCode() == 5879) dbgface = fcdbg;
                if (fcdbg.Vertices.Length != fcdbg.AllEdges.Length)
                {
                    DebuggerContainer dc = new DebuggerContainer();
                    for (int i = 0; i < fcdbg.AllEdges.Length; i++)
                    {
                        dc.Add(fcdbg.AllEdges[i].Curve3D as IGeoObject, fcdbg.AllEdges[i].GetHashCode());
                    }
                    for (int i = 0; i < fcdbg.Vertices.Length; i++)
                    {
                        dc.Add(fcdbg.Vertices[i].DebugPoint, fcdbg.Vertices[i].GetHashCode());
                    }
                }
            }
#endif
            using (new Changing(changingObject, "SetFaces", this, clonedFaces))
            {
                double precision = this.GetExtent(0.0).Size * 1e-6;
                bool combined = true;
                int res = 0;
                Set<Edge> edges = new Set<Edge>(Edges);
                while (combined)
                {
                    combined = false;
                    Set<Edge> toRemove = null;
                    Set<Edge> notSameSurface = new Set<Edge>();
                    foreach (Edge edge in edges)
                    {
                        ModOp2D firstToSecond;
                        if (edge.SecondaryFace != null && edge.PrimaryFace.Surface!=null && edge.SecondaryFace.Surface != null &&
                            edge.SecondaryFace.Surface.SameGeometry(edge.SecondaryFace.GetUVBounds(), edge.PrimaryFace.Surface, edge.PrimaryFace.GetUVBounds(), precision, out firstToSecond) &&
                            edge.PrimaryFace != edge.SecondaryFace && firstToSecond.Determinant > 0) // firstToSecond.IsIsogonal: non periodic surfaces or spheres with different axis are not implemented yet
                        {
                            if (edge.PrimaryFace.Surface is CylindricalSurfaceNP || edge.PrimaryFace.Surface is SphericalSurfaceNP || edge.PrimaryFace.Surface is ConicalSurfaceNP) continue;
#if DEBUG
                            if (edge.GetHashCode() == 1227) { }
                            foreach (Edge dbgedg in edge.PrimaryFace.Edges)
                            {
                                if (edge.Curve3D is InterpolatedDualSurfaceCurve)
                                {
                                    (edge.Curve3D as InterpolatedDualSurfaceCurve).CheckSurfaceParameters();
                                }
                            }
#endif
                            if (edge.PrimaryFace.Surface.IsUPeriodic || edge.PrimaryFace.Surface.IsVPeriodic)
                            {   // do not combine two faces, which are periodic and in the combination fill the whole period
                                // these faces are explicitly kept separate
                                // firstToSecond may move by a whole period. This is not respected here. but with the non periodic surfaces we want to get rid of periodic surfaces anyhow.
                                Border outline = edge.PrimaryFace.Area.Outline; // three lines to check performance
                                try
                                {
                                    ModOp2D inverse = firstToSecond.GetInverse();
                                    Border firstoutline = outline.GetModified(inverse);
                                    BoundingRect ext = firstoutline.Extent;
                                    ext.MinMax(edge.SecondaryFace.Area.Outline.Extent);
                                    if (edge.SecondaryFace.Surface.IsUPeriodic && ext.Width >= edge.SecondaryFace.Surface.UPeriod * 0.75)
                                    {   // special case for cylinder: maybe firstToSecond should be +2.0*Math.Pi or -2.0*Math.Pi 
                                        if (firstoutline.Extent.Left > edge.SecondaryFace.Area.Outline.Extent.Left) firstToSecond[0, 2] += 2.0 * Math.PI;
                                        else firstToSecond[0, 2] -= 2.0 * Math.PI;
                                        inverse = firstToSecond.GetInverse();
                                        firstoutline = outline.GetModified(inverse);
                                        // Border firstoutline = edge.PrimaryFace.Area.Outline.GetModified(firstToSecond.GetInverse()); // GetInverse is correct
                                        ext = firstoutline.Extent;
                                        ext.MinMax(edge.SecondaryFace.Area.Outline.Extent);
                                        if (ext.Width >= edge.SecondaryFace.Surface.UPeriod * 0.75) continue;
                                    }
                                    if (edge.SecondaryFace.Surface.IsVPeriodic && ext.Height >= edge.SecondaryFace.Surface.VPeriod * 0.75) continue;
                                }
                                catch { continue; }
                            }
                            if (edge.PrimaryFace.Surface is SurfaceOfRevolution) continue; // this must be fixed (e.g. in "1_Assembly_Light.stp")

                            toRemove = edge.SecondaryFace.CombineWith(edge.PrimaryFace, firstToSecond);
                            // isolate the face, which will no longer be used:
                            Face faceToRemove = edge.PrimaryFace;
                            Face combinedFace = edge.SecondaryFace;
                            foreach (Edge edg in toRemove)
                            {
                                edg.DisconnectFromFace(edg.SecondaryFace);
                                edg.DisconnectFromFace(edg.PrimaryFace);
                            }
                            foreach (Edge edg in faceToRemove.Edges)
                            {
                                if (edg.Vertex1 != null) edg.Vertex1.RemovePositionOnFace(faceToRemove);
                                if (edg.Vertex2 != null) edg.Vertex2.RemovePositionOnFace(faceToRemove);

                            }
                            foreach (Edge edge1 in combinedFace.Edges)
                            {
                                edge1.UpdateInterpolatedDualSurfaceCurve();
                            }
#if DEBUG
                            if (dbgface != null && !dbgface.CheckConsistency())
                            {

                            }
#endif
                            if (toRemove != null && toRemove.Count > 0)
                            {
                                combined = true;
                                edges.RemoveMany(toRemove);
                                allFaces.Remove(faceToRemove);
#if DEBUG
                                if (!combinedFace.CheckConsistency())
                                { }
#endif
                                ++res;
                                break;
                            }
                            else
                            {
                                // sollte nicht vorkommen: gleiche Surface, gemeinsame kante, kann aber nicht vereinfacht werden
                            }
                        }
                        else
                        {
                            notSameSurface.Add(edge);
                        }
                    }
                    edges.RemoveMany(notSameSurface); // die müssen wir nicht nochmal testen
                }
                faces = allFaces.ToArray();
                foreach (Face face in faces)
                {
#if DEBUG
                    if (!face.CheckConsistency())
                    {
                    }
                    int dbgedgecount = face.AllEdgesCount;
#endif
#if DEBUG
                    if (dbgface != null && !dbgface.CheckConsistency())
                    {

                    }
#endif
                    face.CombineConnectedSameSurfaceEdges();
#if DEBUG
                    if (!face.CheckConsistency())
                    {
                    }
#endif
#if DEBUG
                    if (dbgface != null && !dbgface.CheckConsistency())
                    {

                    }
#endif
                }
                this.edges = null;
                Edge[] allEdges = Edges;
                State |= ShellFlags.EdgesCombined;
                State |= ShellFlags.FacesCombined;
                return res;
            }
        }
        /// <summary>
        /// Returns true, if this shell and the other shell describe the same form. The two shells may be at different positions in 3d space, but may not be rotated,
        /// i.e. must have the same orientation in 3d space.
        /// </summary>
        /// <param name="other">the other shell</param>
        /// <param name="precision">maximum difference in the geometry of the two shells accepted as still beeing equal</param>
        /// <param name="translation">how to move the other shell to coincide with this shell</param>
        /// <returns></returns>
        public bool SameForm(Shell other, double precision, out ModOp translation)
        {
            BoundingCube ext1 = this.GetExtent(precision / 2.0);
            BoundingCube ext2 = other.GetExtent(precision / 2.0);
            translation = ModOp.Translate(ext1.GetCenter() - ext2.GetCenter()); // sh2 wird verschoben und passt damit auf sh1
            if (Math.Abs(ext1.XDiff - ext2.XDiff) > precision) return false;
            if (Math.Abs(ext1.YDiff - ext2.YDiff) > precision) return false;
            if (Math.Abs(ext1.ZDiff - ext2.ZDiff) > precision) return false;

            // Clone, da Änderungen vorgenommen werden
            Shell sh1 = this.Clone(new Dictionary<Edge, Edge>());
            Shell sh2 = other.Clone(new Dictionary<Edge, Edge>());
            sh2.Modify(translation); // jetzt sollten sie identisch sein
            Vertex[] toInit = sh1.Vertices;
            toInit = sh2.Vertices;
            // zusammenhängende Faces werden zusammengefasst
            sh1.CombineConnectedFaces();
            sh2.CombineConnectedFaces();
            foreach (Face fc1 in sh1.Faces)
            {
                fc1.InvalidateArea();
                BoundingRect br1 = fc1.Area.GetExtent();
                CompoundShape.Signature sig1 = fc1.Area.CalculateSignature();
                bool matched = false;
                foreach (Face fc2 in sh2.Faces)
                {
                    ModOp2D firstToSecond;
                    if (fc2.Surface.SameGeometry(fc2.Area.GetExtent(), fc1.Surface, br1, precision, out firstToSecond))
                    {
                        // die gleiche Fläche, aber möglicherweise verschiedene uv-Systeme
                        SimpleShape fc2m = fc2.Area.GetModified(firstToSecond);
                        ModOp2D xTo1;
                        if (fc2m.isCongruent(fc1.Area, sig1, out xTo1, precision))
                        {
                            if (Math.Abs(xTo1.Factor - 1.0) < 1e-6)
                            {
                                matched = true;
                                break;
                            }
                        }
                    }
                }
                if (!matched) return false;
            }
            return true;
        }
        public enum FindSameFormSearchMode { searchX, searchY, searchZ, searchAny };
        /// <summary>
        /// Find other instances of the "form" given by the parameter form. The faces in form must be a subset of faces of this shell.
        /// Returned are other subsets of faces of this shell that describe the same form.
        /// </summary>
        /// <param name="form"></param>
        /// <param name="precision"></param>
        /// <param name="mode"></param>
        /// <returns></returns>
        public Face[][] FindSameForm(Face[] form, double precision, FindSameFormSearchMode mode)
        {   // für FST (Montanari) geschrieben
            List<Face[]> res = new List<Face[]>();
            Set<Face> formSet = new Set<Face>(form); // zum schnellen entscheiden, ob in der Form oder nicht
            BoundingCube ext = GetExtent(precision);
            OctTree<Face> octtree = new OctTree<Face>(ext, precision); // mit allen faces außer den in form gegebenen
            for (int i = 0; i < faces.Length; i++)
            {
                if (formSet.Contains(faces[i])) continue;
                octtree.AddObject(faces[i]);
            }
            Face biggestFace = null;
            BoundingCube currentExt = BoundingCube.EmptyBoundingCube;
            double maxSize = 0.0;
            for (int i = 0; i < form.Length; i++)
            {
                BoundingCube e = form[i].GetExtent(precision);
                double size = e.Size; // mit dem Volumen macht keinen Sinn, denn ebene Faces haben Volumen 0.0
                if (size > maxSize)
                {
                    maxSize = size;
                    biggestFace = form[i];
                    currentExt = e;
                }
            }
            Face[] candidates;
            switch (mode)
            {
                case FindSameFormSearchMode.searchX:
                    candidates = octtree.GetObjectsFromLine(currentExt.GetCenter(), GeoVector.XAxis, 0);
                    break;
                case FindSameFormSearchMode.searchY:
                    candidates = octtree.GetObjectsFromLine(currentExt.GetCenter(), GeoVector.XAxis, 0);
                    break;
                case FindSameFormSearchMode.searchZ:
                    candidates = octtree.GetObjectsFromLine(currentExt.GetCenter(), GeoVector.XAxis, 0);
                    break;
                default:
                case FindSameFormSearchMode.searchAny:
                    candidates = octtree.GetAllObjects();
                    break;
            }
            List<Face> startWith = new List<Face>();
            List<GeoVector> translation = new List<CADability.GeoVector>(); // synchron zu startWith
            BoundingRect areaExt = biggestFace.Area.GetExtent(); // nur einmal berechnen
            CompoundShape.Signature signature = biggestFace.Area.CalculateSignature();
            GeoObjectList dbg = new GeoObject.GeoObjectList();
            for (int i = 0; i < candidates.Length; i++)
            {
                dbg.Add(candidates[i]);
                BoundingCube cext = candidates[i].GetExtent(precision);
                double size = cext.Size;
                if (Math.Abs(size - maxSize) < 3 * precision)
                {
                    GeoVector trans = cext.GetCenter() - currentExt.GetCenter();
                    Face translated = biggestFace.Clone() as Face;
                    translated.Modify(ModOp.Translate(trans));
                    if (translated.SameForm(candidates[i], areaExt, signature, precision))
                    {
                        bool ok = false;
                        switch (mode)
                        {
                            case FindSameFormSearchMode.searchX:
                                ok = Math.Abs(trans.y) < precision && Math.Abs(trans.z) < precision;
                                break;
                            case FindSameFormSearchMode.searchY:
                                ok = Math.Abs(trans.x) < precision && Math.Abs(trans.z) < precision;
                                break;
                            case FindSameFormSearchMode.searchZ:
                                ok = Math.Abs(trans.x) < precision && Math.Abs(trans.y) < precision;
                                break;
                            default:
                            case FindSameFormSearchMode.searchAny:
                                ok = true;
                                break;
                        }
                        startWith.Add(candidates[i]);
                        translation.Add(trans);
                    }
                }
            }
            // startWith enthält gute Startkandiddaten
            for (int i = 0; i < startWith.Count; i++)
            {
                List<Face> fcs = new List<Face>();
                ModOp transi = ModOp.Translate(translation[i]);
                fcs.Add(startWith[i]); // das ist das Pendant zu biggestFace
                for (int j = 0; j < form.Length; j++)
                {
                    if (form[j] == biggestFace) continue;
                    BoundingCube bcform = form[j].GetBoundingCube();
                    Face[] close = octtree.GetObjectsFromBox(bcform.Modify(translation[i]));
#if DEBUG
                    GeoObjectList dbgcl = new GeoObject.GeoObjectList(close);
#endif
                    BoundingRect fjext = form[j].Area.GetExtent();
                    CompoundShape.Signature fjsig = form[j].Area.CalculateSignature();
                    bool added = false;
                    Face translated = form[j].Clone() as Face;
                    translated.Modify(transi);
                    for (int k = 0; k < close.Length; k++)
                    {
                        if (translated.SameForm(close[k], fjext, fjsig, precision))
                        {
                            if (((close[k].GetBoundingCube().GetCenter() - bcform.GetCenter()) - translation[i]).Length < precision)
                            {
                                fcs.Add(close[k]);
                                added = true;
                                break;
                            }
                        }
                    }
                    if (!added) break;
                }
                if (fcs.Count == form.Length) // für jedes Originalface ein passendes gefunden
                {
                    res.Add(fcs.ToArray());
                }
            }
            return res.ToArray();
        }

        internal static Shell MakeShell(Face[] faces, bool tryToConnectOpenEdges = false)
        {
            Shell res = Shell.Construct();
            if (faces.Length > 0) res.colorDef = faces[0].ColorDef;
            res.SetFaces(faces);
            if (tryToConnectOpenEdges)
            {
                Set<Edge> open = new Set<Edge>(res.OpenEdges);
                while (open.Count > 0)
                {
                    Edge startWith = open.GetAny();
                    open.Remove(startWith);
                    foreach (Edge edg in Vertex.ConnectingEdges(startWith.Vertex1, startWith.Vertex2))
                    {
                        if (open.Contains(edg))
                        {
                            if (startWith.Curve3D != null && edg.Curve3D != null && startWith.Curve3D.DistanceTo(edg.Curve3D.PointAt(0.5)) < Precision.eps)
                            {
                                startWith.PrimaryFace.ReplaceEdge(startWith, edg);
                                open.Remove(edg);
                            }
                        }
                    }

                }
            }
            return res;
        }

        internal void ClearVertices()
        {
            for (int i = 0; i < faces.Length; i++)
            {
                faces[i].ClearVertices();
            }
        }

        internal void DisconnectSingularEdges()
        {
            foreach (Edge edg in Edges)
            {
                if (edg.Curve3D == null && edg.PrimaryFace != edg.SecondaryFace && edg.SecondaryFace != null)
                {
                    // eine singuläre Kante verbindet zwei faces. Diese verbindung aufheben, denn es handelt sich um eine "Überstülpunkg", z.B. das Innere eines sich
                    // selbst überschneidenden Turos
                    Face sf = edg.SecondaryFace;
                    Edge newEdge = new Edge(sf, null, sf, edg.SecondaryCurve2D, edg.Forward(sf));
                    newEdge.UseVertices(edg.Vertex1); // gibt ja nur einen
                    edg.RemoveSecondaryFace();
                    sf.ReplaceEdge(edg, newEdge);
                    newEdge.RemoveSecondaryFace();
                }
            }
            edges = null;
        }

        /// <summary>
        /// Tries to find a feature like a drill hole or a part sticking out of the shell starting with this face.
        /// The rule is that faces connected with the face toStartWith are combined until the open edges have the same underlying surface. There might be one
        /// or two loops of such open edges (two loops for a hole going through the body).
        /// </summary>
        /// <param name="toStartWith"></param>
        /// <returns></returns>
        public Shell FeatureFromFace(Face toStartWith)
        {
            try
            {
                Set<Face> featureFaces = new Set<Face>();
                featureFaces.Add(toStartWith); // start with the provided face as part of the feature
                while (featureFaces.Count < Faces.Length) // not all faces used
                {
                    Set<Edge> openFeatureEdges = new Set<Edge>(); // open edges of the feature
                    foreach (Face fc in featureFaces)
                    {
                        foreach (Edge edg in fc.OutlineEdges)
                        {
                            if (!featureFaces.Contains(edg.PrimaryFace) || !featureFaces.Contains(edg.SecondaryFace)) openFeatureEdges.Add(edg);
                        }
                    }
                    // try to find a loop from one of the open edges
                    List<Edge[]> loops = new List<Edge[]>();
                    List<Set<Face>> outsideFaces = new List<Set<Face>>();
                    foreach (Edge edg in openFeatureEdges)
                    {
                        Set<Face> loopFaces = new Set<Face>(); // faces connecting to the loop but not part of the feature
                        Edge[] loop = FindHole(edg, featureFaces, loopFaces);
                        if (loop != null)
                        {
                            // found a loop making a hole on the loopfaces and beeing connected to one of the open edges of the feature
                            // make sure not to add a loop already created
                            Set<Edge> thisLoop = new Set<Edge>(loop);
                            bool foundloop = false;
                            for (int i = 0; i < loops.Count; i++)
                            {
                                if (thisLoop.ContainsAll(loops[i]))
                                {
                                    foundloop = true;
                                    break;
                                }
                            }
                            if (!foundloop)
                            {
                                loops.Add(loop);
                                outsideFaces.Add(loopFaces);
                            }
                        }
                    }
                    if (loops.Count > 0)
                    {
                        Set<Edge> barrier = new Set<Edge>(); // barrier is a set of edges which should isolate the feature, it consist of one ore more loops
                        for (int i = 0; i < loops.Count; i++)
                        {
                            barrier.AddMany(loops[i]);
                        }
                        Set<Face> testFeature = new Set<Face>(featureFaces);
                        if (CollectFaces(testFeature, barrier))
                        {   // there is a true subset of this shell isolated by the barrier, which makes the feature
                            Dictionary<Edge, Edge> clonedEdges = new Dictionary<Edge, Edge>();
                            Dictionary<Vertex, Vertex> clonedVertices = new Dictionary<Vertex, Vertex>();
                            List<Face> clonedFeature = new List<Face>(); // clone the result to make it independat from this shell
                            foreach (Face fc in testFeature)
                            {
                                clonedFeature.Add(fc.Clone(clonedEdges, clonedVertices));
                            }
                            for (int j = 0; j < loops.Count; j++) // for each loop construct a face which closes the open loop of the feature
                            {
                                Edge[] lidEdges = new Edge[loops[j].Length];
                                for (int i = 0; i < loops[j].Length; i++)
                                {
                                    lidEdges[i] = clonedEdges[loops[j][i]];
                                }
                                Face lid = Face.Construct();
                                Face outside = outsideFaces[j].GetAny();
                                ISurface srf = outside.Surface.Clone(); // all cutoff faces are asumed to have (geometrically) the same surface
                                BoundingRect domain = outside.Area.GetExtent();
                                for (int i = 0; i < lidEdges.Length; i++)
                                {
                                    if (lidEdges[i].Curve3D is InterpolatedDualSurfaceCurve)
                                    {
                                        InterpolatedDualSurfaceCurve dsc = lidEdges[i].Curve3D as InterpolatedDualSurfaceCurve;
                                        ISurface oldSurface;
                                        ModOp2D oldToNew;
                                        if (dsc.Surface1 == lidEdges[i].PrimaryFace.Surface)
                                        {
                                            oldSurface = dsc.Surface2;
                                            oldSurface.SameGeometry(domain, srf, domain, Precision.eps, out oldToNew);
                                        }
                                        else
                                        {
                                            oldSurface = dsc.Surface1;
                                            oldSurface.SameGeometry(domain, srf, domain, Precision.eps, out oldToNew);
                                        }
                                        dsc.ReplaceSurface(oldSurface, srf, oldToNew);
                                    }
                                    lidEdges[i].SetSecondary(lid, srf.GetProjectedCurve(lidEdges[i].Curve3D, 0.0), !lidEdges[i].Forward(lidEdges[i].PrimaryFace));
                                }
                                Array.Reverse(lidEdges);
                                lid.Set(srf, new Edge[][] { lidEdges });
                                lid.UserData.Add("Feature.Lid", true);
                                clonedFeature.Add(lid);
                            }
                            Shell res = Shell.MakeShell(clonedFeature.ToArray());
                            res.AssertOutwardOrientation();
                            if (res.Faces.Length <= 3)
                            {   // it might be a face as a simple piece of a plane with the same face as a lid. We dont want that.
                                loops.Clear(); // to find faces outside this loop
                            }
                            else if (!res.HasOpenEdgesExceptPoles()) return res;
                        }
                    }
                    bool found = false;
                    Set<Edge> loopEdges = new Set<Edge>();
                    for (int i = 0; i < loops.Count; i++) loopEdges.AddMany(loops[i]);
                    foreach (Edge edg in openFeatureEdges)
                    {
                        if (loopEdges.Contains(edg)) continue;
                        if (!featureFaces.Contains(edg.PrimaryFace))
                        {
                            featureFaces.Add(edg.PrimaryFace);
                            found = true;
                        }
                        else if (edg.SecondaryFace != null && !featureFaces.Contains(edg.SecondaryFace))
                        {
                            featureFaces.Add(edg.SecondaryFace);
                            found = true;
                        }
                        if (found) break;
                    }
                    if (!found) return null; // no more faces to test left
                }
            }
            catch { } // this should not happen and should be examined. For now, no feature is returned from this face
            return null;
        }

        private bool CollectFaces(Set<Face> faces, Set<Edge> barrier)
        {
            do
            {
                Set<Face> toAdd = new Set<Face>();
                foreach (Face fc in faces)
                {
                    foreach (Edge edg in fc.AllEdges)
                    {
                        if (!barrier.Contains(edg))
                        {
                            if (!faces.Contains(edg.PrimaryFace)) toAdd.Add(edg.PrimaryFace);
                            if (!faces.Contains(edg.SecondaryFace)) toAdd.Add(edg.SecondaryFace);
                        }
                    }
                }
                if (toAdd.Count == 0) break;
                faces.AddMany(toAdd);
            } while (true);
            return (faces.Count < this.faces.Length);
        }

        private static Edge[] FindHole(Edge toStartWith, Set<Face> toAvoid, Set<Face> loopFaces)
        {
            List<Edge> res = new List<Edge>();
            Face onThisFace;
            if (!toAvoid.Contains(toStartWith.PrimaryFace)) onThisFace = toStartWith.PrimaryFace;
            else if (!toAvoid.Contains(toStartWith.SecondaryFace)) onThisFace = toStartWith.SecondaryFace;
            else return null;
            loopFaces.Add(onThisFace);
            Vertex startVertex = toStartWith.StartVertex(onThisFace);
            Vertex endVertex = toStartWith.EndVertex(onThisFace);
            List<ICurve2D> loop = new List<ICurve2D>();
            loop.Add(toStartWith.Curve2D(onThisFace));
            res.Add(toStartWith);
            while (startVertex != endVertex)
            {
                Edge goOnWith = null;
                Face goOnFace = null;
                ModOp2D toMainFace = ModOp2D.Identity;
                foreach (Edge edg in endVertex.AllEdges)
                {
                    if (edg.ConnectsSameSurfaces()) continue;
                    if (res.Contains(edg)) continue;
                    if (edg.PrimaryFace == onThisFace)
                    {
                        goOnWith = edg;
                        goOnFace = edg.PrimaryFace;
                        toMainFace = ModOp2D.Identity;
                    }
                    else if (edg.SecondaryFace == onThisFace)
                    {
                        goOnWith = edg;
                        goOnFace = edg.SecondaryFace;
                        toMainFace = ModOp2D.Identity;
                    }
                    else if (onThisFace.SameSurface(edg.PrimaryFace, out toMainFace))
                    {
                        goOnWith = edg;
                        goOnFace = edg.PrimaryFace;
                    }
                    else if (onThisFace.SameSurface(edg.SecondaryFace, out toMainFace))
                    {
                        goOnWith = edg;
                        goOnFace = edg.SecondaryFace;
                    }
                    if (toAvoid.Contains(goOnFace))
                    {
                        goOnWith = null;
                        goOnFace = null;
                    }
                    if (goOnWith != null)
                    {
                        ICurve2D c2d = goOnWith.Curve2D(goOnFace).Clone(); // Clone is needed because of interpolatedDualSurfaceCurve.ProjectedCurve.GetModified, which changes the 3d curve
                        if (!toMainFace.IsIdentity) c2d = c2d.GetModified(toMainFace);
                        loop.Add(c2d);
                        res.Add(goOnWith);
                        loopFaces.Add(goOnFace);
                        endVertex = goOnWith.EndVertex(goOnFace);
                        break;
                    }
                }
                if (goOnWith == null) return null; // result: there is no such loop
            }
            if (!Border.CounterClockwise(loop)) return res.ToArray();
            return null;
        }
        /// <summary>
        /// edgeLoop must be a closed set of edges on this shell. The shell will be splitted at this loop.
        /// All edges in edgeLoop join one of the faces in facesToCutoff. These parts of the shell will not be used.
        /// At this loop, which remains open in the first step, the resulting shell will be closed by faces with surfaces from facesToCutOff.
        /// </summary>
        /// <param name="edge"></param>
        /// <param name="face"></param>
        /// <returns></returns>
        public Shell FeatureFromEdges(Edge[] edgeLoop, Face[] facesToCutOff)
        {
            Dictionary<Edge, Edge> clonedEdges = new Dictionary<Edge, Edge>();
            Shell res = Clone(clonedEdges);
            // res and this contain the same faces in the same order
            Set<Face> originalOutsideFaces = new Set<Face>(facesToCutOff); // Original-Faces not belonging to the feature
            Set<Face> outsideFaces = new Set<Face>(); // Faces not belonging to the feature (in the clone)
            Set<Face> featureFaces = new Set<Face>(); // Faces belonging to the feature (in the clone)
            Dictionary<Face, int> faceToIndex = new Dictionary<Face, int>();
            for (int i = 0; i < Faces.Length; i++)
            {
                faceToIndex[Faces[i]] = i;
            }
            for (int i = 0; i < res.Faces.Length; i++)
            {
                faceToIndex[res.Faces[i]] = i;
            }
            for (int i = 0; i < facesToCutOff.Length; i++)
            {
                outsideFaces.Add(res.Faces[faceToIndex[facesToCutOff[i]]]);
            }
            for (int i = 0; i < edgeLoop.Length; i++)
            {
                if (originalOutsideFaces.Contains(edgeLoop[i].PrimaryFace))
                {
                    outsideFaces.Add(res.Faces[faceToIndex[edgeLoop[i].PrimaryFace]]);
                    featureFaces.Add(res.Faces[faceToIndex[edgeLoop[i].SecondaryFace]]);
                }
                else
                {
                    outsideFaces.Add(res.Faces[faceToIndex[edgeLoop[i].SecondaryFace]]);
                    featureFaces.Add(res.Faces[faceToIndex[edgeLoop[i].PrimaryFace]]);
                }
            }
            Set<Face> saveFeatureFaces = new Set<Face>(featureFaces); // maybe we need this later, when the feature is not closed
            foreach (Face fce in outsideFaces)
            {
                res.RemoveFace(fce);
            }
            // Make the lid from the edge loop
            Edge[] lidEdges = new Edge[edgeLoop.Length];
            for (int i = 0; i < edgeLoop.Length; i++)
            {
                lidEdges[i] = clonedEdges[edgeLoop[i]];
            }
            Face lid = Face.Construct();
            ISurface srf = facesToCutOff[0].Surface.Clone(); // all cutoff faces are asumed to have (geometrically) the same surface
            BoundingRect domain = facesToCutOff[0].Area.GetExtent();
            for (int i = 0; i < lidEdges.Length; i++)
            {
                if (lidEdges[i].Curve3D is InterpolatedDualSurfaceCurve)
                {
                    InterpolatedDualSurfaceCurve dsc = lidEdges[i].Curve3D as InterpolatedDualSurfaceCurve;
                    ISurface oldSurface;
                    ModOp2D oldToNew;
                    if (dsc.Surface1 == lidEdges[i].PrimaryFace.Surface)
                    {
                        oldSurface = dsc.Surface2;
                        oldSurface.SameGeometry(domain, srf, domain, Precision.eps, out oldToNew);
                    }
                    else
                    {
                        oldSurface = dsc.Surface1;
                        oldSurface.SameGeometry(domain, srf, domain, Precision.eps, out oldToNew);
                    }
                    dsc.ReplaceSurface(oldSurface, srf, oldToNew);
                }
                lidEdges[i].SetSecondary(lid, srf.GetProjectedCurve(lidEdges[i].Curve3D, 0.0), !lidEdges[i].Forward(lidEdges[i].PrimaryFace));
            }
            Array.Reverse(lidEdges);
            lid.Set(srf, new Edge[][] { lidEdges });
            lid.UserData.Add("Feature.Lid", true);
            AccumulateConnectedFaces(featureFaces);
            Shell res1 = Shell.MakeShell(featureFaces.ToArray());
            if (res1.HasOpenEdgesExceptPoles())
            {
                // there must be a hole in the shell, like a drilling hole going through the whole body
                // if the open side is a single surface, then we can close it
                saveFeatureFaces.Add(lid);
                Face toCloseWith = null;
                bool singleSurface = true;
                Set<Edge> openEdges = new Set<Edge>();
                foreach (Face fce in saveFeatureFaces)
                {
                    foreach (Edge edg in fce.Edges)
                    {
                        if (!saveFeatureFaces.Contains(edg.PrimaryFace))
                        {
                            openEdges.Add(edg);
                            if (toCloseWith == null) toCloseWith = edg.PrimaryFace;
                            else if (!toCloseWith.SameSurface(edg.PrimaryFace))
                            {
                                singleSurface = false;
                                break;
                            }
                        }
                        if (!saveFeatureFaces.Contains(edg.SecondaryFace))
                        {
                            openEdges.Add(edg);
                            if (toCloseWith == null) toCloseWith = edg.SecondaryFace;
                            else if (!toCloseWith.SameSurface(edg.SecondaryFace))
                            {
                                singleSurface = false;
                                break;
                            }
                        }
                    }
                    if (!singleSurface) break;
                }
                if (toCloseWith != null && singleSurface)
                {
                    Face bottom = Face.Construct(); // closing Face on the bottom
                    srf = toCloseWith.Surface;
                    domain = toCloseWith.Area.GetExtent();
                    foreach (Edge edg in openEdges)
                    {
                        if (edg.Curve3D is InterpolatedDualSurfaceCurve)
                        {
                            InterpolatedDualSurfaceCurve dsc = edg.Curve3D as InterpolatedDualSurfaceCurve;
                            ISurface oldSurface;
                            ModOp2D oldToNew;
                            if (dsc.Surface1 == edg.PrimaryFace.Surface)
                            {
                                oldSurface = dsc.Surface2;
                                oldSurface.SameGeometry(domain, srf, domain, Precision.eps, out oldToNew);
                            }
                            else
                            {
                                oldSurface = dsc.Surface1;
                                oldSurface.SameGeometry(domain, srf, domain, Precision.eps, out oldToNew);
                            }
                            dsc.ReplaceSurface(oldSurface, srf, oldToNew);
                        }
                        if (!saveFeatureFaces.Contains(edg.PrimaryFace))
                        {
                            edg.RemovePrimaryFace();
                        }
                        else
                        {
                            edg.RemoveSecondaryFace();
                        }
                        edg.SetSecondary(bottom, srf.GetProjectedCurve(edg.Curve3D, Precision.eps), !edg.Forward(edg.PrimaryFace));
                    }
                    bottom.Set(srf, openEdges.ToArray(), new Edge[0][], true);
                    saveFeatureFaces.Add(bottom);
                    bottom.UserData.Add("Feature.Bottom", true);
                    Shell res2 = MakeShell(saveFeatureFaces.ToArray());
                    if (!res2.HasOpenEdgesExceptPoles())
                    {
                        res2.AssertOutwardOrientation();
                        return res2;
                    }
                }
            }
            else
            {
                res1.AssertOutwardOrientation();
                return res1;
            }
            return null;
        }
        private static HashSet<Edge> UnconnectedEdges(IEnumerable<Face> faces)
        {
            HashSet<Edge> edges = new HashSet<Edge>();
            foreach (Face face in faces)
            {
                foreach (Edge edge in face.Edges)
                {
                    if (edges.Contains(edge)) continue;
                    Face otherFace = edge.OtherFace(face);
                    if (otherFace != null && faces.Contains(otherFace)) continue;
                    edges.Add(edge);
                }
            }
            return edges;
        }
        private static List<Edge> FindCommonSurfaceLoop(Edge startHere, Face toExclude)
        {
            Vertex v = startHere.Vertex1;
            // to continue, work in progress
            return null;
        }
        private static Face CommonFace(IEnumerable<Edge> edges, bool acceptSameGeometry)
        {
            Face face = null;
            if (!edges.Any()) return null;
            Edge edge1 = edges.First();
            Edge edge2 = edges.Last();
            if (edge1 == edge2) return null; // only a single edge
            if (edge1.PrimaryFace == edge2.PrimaryFace) face = edge1.PrimaryFace;
            else if (edge1.PrimaryFace == edge2.SecondaryFace) face = edge1.PrimaryFace;
            else if (edge1.SecondaryFace == edge2.PrimaryFace) face = edge1.SecondaryFace;
            else if (edge1.SecondaryFace == edge2.SecondaryFace) face = edge1.SecondaryFace;
            if (face == null && acceptSameGeometry)
            {
                if (edge1.PrimaryFace.SameSurface(edge2.PrimaryFace)) face = edge1.PrimaryFace;
                else if (edge1.PrimaryFace.SameSurface(edge2.SecondaryFace)) face = edge1.PrimaryFace;
                else if (edge1.SecondaryFace.SameSurface(edge2.PrimaryFace)) face = edge1.SecondaryFace;
                else if (edge1.SecondaryFace.SameSurface(edge2.SecondaryFace)) face = edge1.SecondaryFace;
            }
            if (face != null)
            {
                foreach (Edge edge in edges)
                {
                    if (edge == edge1) continue;
                    if (edge == edge2) continue;
                    if (edge.PrimaryFace == face || edge.SecondaryFace == face) continue;
                    if (acceptSameGeometry)
                    {
                        if (face.SameSurface(edge.PrimaryFace) || face.SameSurface(edge.SecondaryFace)) continue;
                    }
                    face = null;
                    break;
                }
            }
            return face;
        }
        /// <summary>
        /// Returns "features" of the shell. A feature is a subset of the faces of the shell, which is seperated by the provided <paramref name="loops"/>. Since such a feature
        /// has open edges, there is also a face or multiple faces, which close the feature to make it to a solid.
        /// </summary>
        /// <param name="bounds"></param>
        public void FeaturesFromEdges(List<Edge[]> loops, List<IEnumerable<Face>> features, List<IEnumerable<Face>> lids, IEnumerable<Face> startWith)
        {
            HashSet<Edge> margin = new HashSet<Edge>();
            for (int i = 0; i < loops.Count; i++)
            {
                margin.UnionWith(loops[i]);
            }
            HashSet<Face> collection = new HashSet<Face>(startWith);
            HashSet<Edge> edgeToConnect = new HashSet<Edge>();
            foreach (Face face in collection)
            {
                foreach (Edge edge in face.Edges)
                {
                    if (margin.Contains(edge)) continue;
                    if (edge.IsPartOfHole(edge.OtherFace(face)))
                    {
                        List<Edge[]> loopsCloned = new List<Edge[]>(loops);
                        Edge[] hole = edge.OtherFace(face).GetHole(edge);
                        if (hole != null)
                        {
                            loopsCloned.Add(hole);
                            FeaturesFromEdges(loopsCloned, features, lids, collection); // maybe adds some more features, which are delimited by the hole
                        }
                    }
                    edgeToConnect.Add(edge);
                }
            }
            do
            {
                HashSet<Edge> unconnected = UnconnectedEdges(collection);
                unconnected.ExceptWith(margin);
                Face commonFace = CommonFace(unconnected, true);
                if (commonFace != null && !collection.Contains(commonFace)) features.Add(new HashSet<Face>(collection));
                HashSet<Face> found = new HashSet<Face>();
                foreach (Edge edge in unconnected)
                {
                    if (!collection.Contains(edge.PrimaryFace)) found.Add(edge.PrimaryFace);
                    if (!collection.Contains(edge.SecondaryFace)) found.Add(edge.SecondaryFace);
                }
                if (!found.Any()) break;
                collection.UnionWith(found);
            } while (true);
        }
        public List<Shell> FeaturesFromEdges(ICollection<Edge> bounds)
        {
            List<Shell> res = new List<Shell>();
            List<List<Edge>> loops = new List<List<Edge>>(); // a list of loops from bounds (most cases: there is only one loop)
            HashSet<Edge> toUse = new HashSet<Edge>(bounds); // copy of the set to not destroy the provided set
            while (toUse.Any())
            {
                List<Edge> loop = new List<Edge>();
                Edge edge = toUse.First();
                toUse.Remove(edge);
                loop.Add(edge);
                Vertex comingFrom = edge.Vertex1;
                Vertex connectTo = edge.Vertex2;
                do
                {
                    edge = toUse.Intersect(connectTo.AllEdges).FirstOrDefault();
                    if (edge != null)
                    {
                        toUse.Remove(edge);
                        loop.Add(edge);
                        if (connectTo == edge.Vertex1) connectTo = edge.Vertex2;
                        else connectTo = edge.Vertex1;
                    }
                    else break;
                } while (true);
                loops.Add(loop);
            }
            HashSet<Face> usedFaces = new HashSet<Face>();
            List<HashSet<Face>> subsets = new List<HashSet<Face>>();
            foreach (Edge edg in bounds)
            {
                if (usedFaces.Contains(edg.PrimaryFace) || usedFaces.Contains(edg.SecondaryFace)) continue;
                HashSet<Face> connectedToPrimary = new HashSet<Face>();
                edg.PrimaryFace.CollectConnectedFaces(connectedToPrimary, bounds);
                HashSet<Face> connectedToSecondary = new HashSet<Face>();
                edg.SecondaryFace.CollectConnectedFaces(connectedToSecondary, bounds);
                if (connectedToPrimary.Intersect(connectedToSecondary).Any())
                {   // the two sets should be disjunct, otherwise the bounds do not seperate two parts
                    return res; // which is probably empty
                }
                subsets.Add(connectedToPrimary);
                subsets.Add(connectedToSecondary);
                usedFaces.UnionWith(connectedToPrimary);
                usedFaces.UnionWith(connectedToSecondary);
            }
            foreach (HashSet<Face> subset in subsets)
            {   // now each subset has one or more loops of open edges, which must be closed by a new face
                List<Face> caps = new List<Face>();
                foreach (List<Edge> loop in loops)
                {
                    HashSet<Face> connectedFaces = new HashSet<Face>();
                    if (subset.Contains(loop[0].PrimaryFace) || subset.Contains(loop[0].SecondaryFace))
                    {
                        for (int i = 0; i < loop.Count; i++)
                        {
                            if (subset.Contains(loop[i].PrimaryFace)) connectedFaces.Add(loop[i].SecondaryFace);
                            else connectedFaces.Add(loop[i].PrimaryFace);
                        }
                        ISurface surface = null;
                        if (connectedFaces.Count == 1)
                        {
                            surface = connectedFaces.First().Surface;
                        }
                        else if (connectedFaces.Count == 2)
                        {   // maybe two faces with the same surface
                            Face face0 = connectedFaces.ElementAt(0);
                            Face face1 = connectedFaces.ElementAt(1);
                            if (face0.Surface.SameGeometry(face0.Domain, face1.Surface, face1.Domain, 0.0, out ModOp2D _)) surface = face0.Surface;
                        }
                        if (surface != null)
                        {
                            List<ICurve2D> curve2Ds = new List<ICurve2D>(loop.Count);
                            for (int i = 0; i < loop.Count; i++)
                            {
                                curve2Ds.Add(surface.GetProjectedCurve(loop[i].Curve3D, 0.0));
                            }
                            Border bdr = Border.FromUnorientedList(curve2Ds.ToArray(), false);
                            if (bdr != null && bdr.IsClosed && bdr.Area > 0)
                            {
                                Face cap = Face.MakeFace(surface, new SimpleShape(bdr));
                                if (cap != null && cap.CheckConsistency()) caps.Add(cap);
                            }
                        }
                    }
                }
                if (caps.Count > 0)
                {
                    List<Face> feature = ClonePart(subset);
                    feature.AddRange(caps);
                    Shell.connectFaces(feature.ToArray(), Precision.eps);
                    Shell fs = Shell.MakeShell(feature.ToArray(), false);
                    fs.AssertOutwardOrientation();
                    res.Add(fs);
                }
            }
            return res;
        }
        private static void AccumulateConnectedFaces(Set<Face> connectedFaces)
        {
            Set<Edge> edgesToTest = new Set<Edge>();
            foreach (Face fce in connectedFaces)
            {
                edgesToTest.AddMany(fce.AllEdges);
            }
            Set<Edge> handledEdges = new Set<Edge>();
            while (edgesToTest.Count > 0)
            {
                Set<Edge> newEdges = new Set<Edge>();
                foreach (Edge edg in edgesToTest)
                {
                    if (handledEdges.Contains(edg)) continue;
                    if (!connectedFaces.Contains(edg.PrimaryFace))
                    {
                        connectedFaces.Add(edg.PrimaryFace);
                        newEdges.AddMany(edg.PrimaryFace.AllEdges);
                    }
                    if (edg.SecondaryFace != null && !connectedFaces.Contains(edg.SecondaryFace))
                    {
                        connectedFaces.Add(edg.SecondaryFace);
                        newEdges.AddMany(edg.SecondaryFace.AllEdges);
                    }
                    handledEdges.Add(edg);
                }
                edgesToTest = newEdges.Difference(handledEdges);
            }
        }

        internal static Shell CollectConnected(Set<Face> allFaces)
        {
            Set<Face> connected = new Set<Face>();
            Set<Edge> toCheck = new Set<Edge>();
            Face startWith = allFaces.GetAndRemoveAny();
            connected.Add(startWith);
            toCheck.AddMany(startWith.AllEdges);
            while (!toCheck.IsEmpty())
            {
                Edge edg = toCheck.GetAny();
                if (!connected.Contains(edg.PrimaryFace) && allFaces.Contains(edg.PrimaryFace))
                {
                    connected.Add(edg.PrimaryFace);
                    toCheck.AddMany(edg.PrimaryFace.AllEdges);
                    allFaces.Remove(edg.PrimaryFace);
                }
                if (edg.SecondaryFace != null && !connected.Contains(edg.SecondaryFace) && allFaces.Contains(edg.SecondaryFace))
                {
                    connected.Add(edg.SecondaryFace);
                    toCheck.AddMany(edg.SecondaryFace.AllEdges);
                    allFaces.Remove(edg.SecondaryFace);
                }
                toCheck.Remove(edg);
            }
            Shell shell = Shell.Construct();
            shell.SetFaces(connected.ToArray());
            return shell;
        }

        int IExportStep.Export(ExportStep export, bool topLevel)
        {
            // #31=CLOSED_SHELL('Closed Shell',(#71,#102,#133,#155,#167,#179)) ;
            StringBuilder faceList = new StringBuilder();
            for (int i = 0; i < faces.Length; i++)
            {
                int facenr = (faces[i] as IExportStep).Export(export, false);
                if (i == 0) faceList.Append("#");
                else faceList.Append(",#");
                faceList.Append(facenr.ToString());
#if DEBUG
                //if (i == 100) break;
#endif
            }
            int cs;
            if (HasOpenEdgesExceptPoles())
            {
                cs = export.WriteDefinition("OPEN_SHELL('" + NameOrEmpty + "',(" + faceList.ToString() + "))");
            }
            else
            {
                cs = export.WriteDefinition("CLOSED_SHELL('" + NameOrEmpty + "',(" + faceList.ToString() + "))");
            }
            if (Owner == null || (Owner is IColorDef && (Owner as IColorDef).ColorDef != colorDef))
            {
                if (colorDef == null) colorDef = new ColorDef("Black", Color.Black);
                colorDef.MakeStepStyle(cs, export);
            }
            if (topLevel)
            {   // SHELL_BASED_SURFACE_MODEL is a Geometric_Representation_Item
                int sbsm = export.WriteDefinition("SHELL_BASED_SURFACE_MODEL('',(#" + cs.ToString() + "))");
                int product = export.WriteDefinition("PRODUCT( '" + NameOrEmpty + "','" + NameOrEmpty + "','',(#2))");
                int pdf = export.WriteDefinition("PRODUCT_DEFINITION_FORMATION_WITH_SPECIFIED_SOURCE( ' ', 'NONE', #" + product.ToString() + ", .NOT_KNOWN. )");
                int pd = export.WriteDefinition("PRODUCT_DEFINITION( 'NONE', 'NONE', #" + pdf.ToString() + ", #3 )");
                int pds = export.WriteDefinition("PRODUCT_DEFINITION_SHAPE( 'NONE', 'NONE', #" + pd.ToString() + " )");
                int sr = export.WriteDefinition("ADVANCED_BREP_SHAPE_REPRESENTATION('" + NameOrEmpty + "', ( #" + sbsm.ToString() + "), #4 )");
                export.WriteDefinition("SHAPE_DEFINITION_REPRESENTATION( #" + pds.ToString() + ", #" + sr.ToString() + ")");
                return sr;
            }
            else
            {
                return cs;
            }
        }

        private class ShellToSolid : ICommandHandler
        {
            private readonly Shell shell;
            private readonly IFrame frame;
            public ShellToSolid(Shell shell, IFrame frame)
            {
                this.shell = shell;
                this.frame = frame;
            }

            bool ICommandHandler.OnCommand(string MenuId)
            {
                if (MenuId == "MenuId.Shell.ToSolid")
                {
                    IGeoObjectOwner owner = shell.Owner;
                    Solid newSolid = null;
                    if (shell.OpenEdges.Length > 0)
                    {
                        if (shell.CloseOpenEdges())
                        {
                            newSolid = Solid.MakeSolid(shell);
                        }
                    }
                    else
                    {
                        newSolid = Solid.MakeSolid(shell);
                    }
                    // ActiveFrame.Frame;
                    if (newSolid != null)
                    {
                        using (frame.Project.Undo.UndoFrame)
                        {
                            owner.Remove(shell);
                            shell.Owner = newSolid; // unfortunately owner.Remove(shell) sets the shell.owner to null
                            owner.Add(newSolid);
                        }
                    }
                }
                return false;
            }

            void ICommandHandler.OnSelected(MenuWithHandler selectedMenu, bool selected)
            {

            }

            bool ICommandHandler.OnUpdateCommand(string MenuId, CommandState CommandState)
            {
                return MenuId == "MenuId.Shell.ToSolid";
            }
        }
        /// <summary>
        /// Returns a menu, which is shown when there is a right click on the face
        /// </summary>
        /// <param name="frame"></param>
        /// <returns></returns>
        internal MenuWithHandler[] GetContextMenu(IFrame frame)
        {
            List<MenuWithHandler> res = new List<MenuWithHandler>();
            if (OpenEdges.Length == 0)
            {
                MenuWithHandler mhsol = new MenuWithHandler();
                mhsol.ID = "MenuId.Shell.ToSolid";
                mhsol.Text = StringTable.GetString("MenuId.Shell.ToSolid", StringTable.Category.label);
                mhsol.Target = new ShellToSolid(this, frame);
                res.Add(mhsol);
            }
            else
            {
                MenuWithHandler mhsol = new MenuWithHandler();
                mhsol.ID = "MenuId.Shell.ToSolid";
                mhsol.Text = StringTable.GetString("MenuId.Shell.CloseToSolid", StringTable.Category.label);
                mhsol.Target = new ShellToSolid(this, frame);
                res.Add(mhsol);
            }
            MenuWithHandler mhsel = new MenuWithHandler();
            mhsel.ID = "MenuId.Selection.Set";
            mhsel.Text = StringTable.GetString("MenuId.Selection.Set", StringTable.Category.label);
            mhsel.Target = new SetSelection(this, frame.ActiveAction as SelectObjectsAction);
            res.Add(mhsel);
            MenuWithHandler mhadd = new MenuWithHandler();
            mhadd.ID = "MenuId.Selection.Add";
            mhadd.Text = StringTable.GetString("MenuId.Selection.Add", StringTable.Category.label);
            mhadd.Target = new SetSelection(this, frame.ActiveAction as SelectObjectsAction);
            res.Add(mhadd);
            return res.ToArray();
        }

    }
}
