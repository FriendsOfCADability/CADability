using CADability.Actions;
using CADability.Attribute;
using CADability.Substitutes;
using CADability.UserInterface;
using MathNet.Numerics;
using System;
using System.Collections.Generic;
using System.Runtime.InteropServices.ComTypes;
using System.Runtime.Serialization;
using System.Threading.Tasks;

namespace CADability.GeoObject
{
    class ShowPropertySolid : PropertyEntryImpl, IGeoObjectShowProperty, ICommandHandler
    {
        private Solid solid;
        private IPropertyEntry[] attributeProperties; // Anzeigen für die Attribute (Ebene, Farbe u.s.w)
        public ShowPropertySolid(Solid solid, IFrame frame)
            : base(frame)
        {
            this.solid = solid;
            resourceIdInternal = "Solid.Object";
            attributeProperties = solid.GetAttributeProperties(frame);
        }
        #region IShowPropertyImpl overrides
        public override PropertyEntryType Flags => PropertyEntryType.ContextMenu | PropertyEntryType.Selectable | PropertyEntryType.GroupTitle | PropertyEntryType.HasSubEntries;
        private IPropertyEntry[] subEntries;
        public override IPropertyEntry[] SubItems
        {
            get
            {
                if (subEntries == null)
                {
                    List<IPropertyEntry> se = new List<IPropertyEntry>();
#if DEBUG
                    IntegerProperty dbghashcode = new IntegerProperty(solid.UniqueId, "Debug.Hashcode");
                    se.Add(dbghashcode);
#endif
                    se.Add(new NameProperty(this.solid, "Name", "Solid.Name"));
                    DoubleProperty vol = new DoubleProperty(base.Frame, "Solid.Volume");
                    vol.SetDouble(0.0);
                    vol.ReadOnly = true;
                    se.Add(vol);
                    // parametric properties are part of the shell. But we show them at the solid, which seems more natural
                    List<IPropertyEntry> pps = new List<IPropertyEntry>();
                    GroupProperty gpParametricProperties = new GroupProperty("Solid.ParametricProperties", pps.ToArray());
                    foreach (Shell shell in solid.Shells)
                    {
                        List<IPropertyEntry> shellProperties = shell.GetParameterProperties(Frame, gpParametricProperties);
                        pps.AddRange(shellProperties);
                    }
                    if (pps.Count > 0)
                    {
                        gpParametricProperties.SetSubEntries(pps.ToArray());
                        se.Add(gpParametricProperties);
                    }
                    pps = new List<IPropertyEntry>();
                    GroupProperty gpFeatureProperties = new GroupProperty("Solid.FeatureProperties", pps.ToArray());
                    foreach (Shell shell in solid.Shells)
                    {
                        List<IPropertyEntry> shellFeatureProperties = shell.GetFeatureProperties(Frame, gpFeatureProperties);
                        pps.AddRange(shellFeatureProperties);
                    }
                    if (pps.Count > 0)
                    {
                        gpFeatureProperties.SetSubEntries(pps.ToArray());
                        se.Add(gpFeatureProperties);
                    }
                    foreach (Shell shell in solid.Shells)
                    {
                        IPropertyEntry sp = shell.GetShowProperties(base.Frame);
                        sp.ReadOnly = true;
                        se.Add(sp);
                    }
                    se.AddRange(attributeProperties);
                    subEntries = se.ToArray();
                    for (int i = 0; i < subEntries.Length; i++)
                    {
                        subEntries[i].Parent = this;
                    }
                    Task task = Task.Run(() => { vol.SetDouble(solid.Volume(0.0)); });
                }
                return subEntries;
            }
        }

        public override MenuWithHandler[] ContextMenu
        {
            get
            {
                List<MenuWithHandler> items = new List<MenuWithHandler>(MenuResource.LoadMenuDefinition("MenuId.Object.Solid", false, this));
                CreateContextMenueEvent?.Invoke(this, items);
                solid.GetAdditionalContextMenue(this, Frame, items);
                return items.ToArray();
            }
        }
        #endregion
        #region IGeoObjectShowProperty Members

        public event CreateContextMenueDelegate CreateContextMenueEvent;
        IGeoObject IGeoObjectShowProperty.GetGeoObject()
        {
            return solid;
        }

        string IGeoObjectShowProperty.GetContextMenuId()
        {
            return "MenuId.Object.Solid";
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
                            IGeoObjectOwner addTo = solid.Owner;
                            if (addTo == null) addTo = Frame.ActiveView.Model;
                            GeoObjectList toSelect = solid.Decompose();
                            addTo.Remove(solid);
                            for (int i = 0; i < toSelect.Count; ++i)
                            {
                                addTo.Add(toSelect[i]);
                            }
                            SelectObjectsAction soa = Frame.ActiveAction as SelectObjectsAction;
                            soa.SetSelectedObjects(toSelect); // alle Teilobjekte markieren
                        }
                    }
                    return true;
                case "MenuId.SplitByPlane":
                    {
                        Frame.SetAction(new SplitSolidByPlane(solid));
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
                case "MenuId.SplitByPlane":
                    CommandState.Enabled = true; // naja isses ja immer
                    return true;
            }
            return false;
        }
        void ICommandHandler.OnSelected(MenuWithHandler selectedMenuItem, bool selected) { }

        #endregion
    }


    /// <summary>
    /// A Solid is a <see cref="IGeoObject"/> implementation that represents a solid body.
    /// Its main data is a collection of oriented faces. The normal vector on any point of the face
    /// points to the outside of the body. Solids have one set of faces that represent the outer hull
    /// and any number of cavities that reside totally inside the outer hull. All cavyties are disjoint.
    /// </summary>
    [Serializable()]
    public class Solid : IGeoObjectImpl, ISerializable, IColorDef, IGetSubShapes, IGeoObjectOwner, IDeserializationCallback, IJsonSerialize, IJsonSerializeDone, IExportStep
    {
        internal static bool octTreeAlsoCheckInside = false; // global setting: auch das innere der Solids für den octTree überprüfen (Anforderung von Hilgers)
        private Shell[] shells; // List of Shells defining this Solid. The first (and in most cases only) entry is the outer hull, the following are non intersecting inner holes (faces oriented to point into the inside of the hole)
        private string name; // aus STEP oder IGES kommen benannte solids (Shells, Faces?)
        [FlagsAttribute] internal enum Flags { oriented = 1, unchanged = 2 };
        internal Flags flags;
        private Edge[] edges; // secondary data, not serialized
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
                    HashSet<Edge> edgelist = new HashSet<Edge>();
                    foreach (Shell shell in shells)
                    {
                        foreach (Face fc in shell.Faces)
                        {
                            foreach (Edge ed in fc.AllEdges)
                            {
                                edgelist.Add(ed);
                            }
                        }
                    }
                    edges = new Edge[edgelist.Count];
                    edgelist.CopyTo(edges, 0);
                }
                return edges;
            }
        }
        #region polymorph construction
        public delegate Solid ConstructionDelegate();
        public static ConstructionDelegate Constructor;
        public static Solid Construct()
        {
            if (Constructor != null) return Constructor();
            return new Solid();
        }
        public delegate void ConstructedDelegate(Solid justConstructed);
        public static ConstructedDelegate Constructed;
        #endregion
        protected Solid()
            : base()
        {
            if (Constructed != null) Constructed(this);
        }
        /// <summary>
        /// Returns a list of all shells that bound this solid. In most cases there is only a single shell.
        /// If there is more than one shell the shell with index 0 is the outer hull and all subsequent shells
        /// describe cavyties that reside totally inside the outer hull.
        /// </summary>
        public Shell[] Shells
        {
            get
            {
                return shells;
            }
        }
        private void OnWillChange(IGeoObject Sender, GeoObjectChange Change)
        {
            if (isChanging == 0)
            {
                FireWillChange(Change);
            }
        }
        private void OnDidChange(IGeoObject Sender, GeoObjectChange Change)
        {
            if (isChanging == 0)
            {
                FireDidChange(Change);
            }
        }
        public void SetShell(Shell sh)
        {
            if (shells != null)
            {
                for (int i = 0; i < shells.Length; i++)
                {
                    shells[i].WillChangeEvent -= new ChangeDelegate(OnWillChange);
                    shells[i].DidChangeEvent -= new ChangeDelegate(OnDidChange);
                }
            }
            if (Shells != null && Shells.Length > 0)
            {
                using (new Changing(this, "SetShell", Shells[0]))
                {
                    shells = new Shell[] { sh };
                    sh.Owner = this;
                    edges = null;
                }
            }
            else
            {
                shells = new Shell[] { sh };
                sh.Owner = this;
                edges = null;
            }
            for (int i = 0; i < shells.Length; i++)
            {
                shells[i].WillChangeEvent += new ChangeDelegate(OnWillChange);
                shells[i].DidChangeEvent += new ChangeDelegate(OnDidChange);
            }
        }
        protected void RemoveShell(Shell sh)
        {
            if (Shells.Length > 0 && sh == Shells[0])
            {
                sh.WillChangeEvent -= new ChangeDelegate(OnWillChange);
                sh.DidChangeEvent -= new ChangeDelegate(OnDidChange);
                using (new Changing(this, "SetShell", sh))
                {
                    shells = new Shell[0];
                    edges = null;
                }
            }
        }
        public static Solid MakeSolid(Shell sh)
        {
            Solid res = Solid.Construct();
            res.SetShell(sh);
            res.colorDef = sh.ColorDef;
            return res;
        }
        public void SetFaces(Shell sh, Face[] faces)
        {   // nur wg. Undo
            Face[] clonedFaces = (Face[])sh.Faces.Clone();
            Dictionary<Edge, Edge> clonedEdges = new Dictionary<Edge, Edge>();
            Dictionary<Vertex, Vertex> clonedVertices = new Dictionary<Vertex, Vertex>();
            for (int i = 0; i < clonedFaces.Length; i++)
            {
                clonedFaces[i] = clonedFaces[i].Clone(clonedEdges, clonedVertices);
            }
            using (new Changing(this, "SetFaces", sh, clonedFaces))
            {
                sh.SetFaces(faces);
            }
        }
        /// <summary>
        /// The name of the solid. 
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
            foreach (Shell shell in shells)
            {
                res.AddRange(shell.GetPlaneIntersection(pl));
            }
            return res.ToArray();
        }
        /// <summary>
        /// Returns the volume of this solid. the calculation is based on a triangulation with at least the provided <paramref name="precision"/>.
        /// </summary>
        /// <param name="precision">The precision of the triangulation</param>
        public double Volume(double precision)
        {
            double v = 0.0;
            for (int i = 0; i < shells.Length; i++)
            {
                v += shells[i].Volume(precision);
            }
            return v;
        }
        #region IGeoObject Members
        /// <summary>
        /// Overrides <see cref="CADability.GeoObject.IGeoObjectImpl.GetShowProperties (IFrame)"/>
        /// </summary>
        /// <param name="Frame"></param>
        /// <returns></returns>
        public override IPropertyEntry GetShowProperties(IFrame Frame)
        {
            return new ShowPropertySolid(this, Frame);
        }
        /// <summary>
        /// Overrides <see cref="CADability.GeoObject.IGeoObjectImpl.Modify (ModOp)"/>
        /// </summary>
        /// <param name="m"></param>
        public override void Modify(ModOp m)
        {
            // wir gehen hier davon aus, dass die einzelnen Shells voneinander unabhängig sind,
            // d.h. keine gemeinsamen Kanten oder Faces besitzen. Wäre dem aber so,
            // so würden die Kanten oder Faces mehrfach modifiziert, was natürlich falsch wäre
            using (new Changing(this, "ModifyInverse", m))
            {
                for (int i = 0; i < shells.Length; ++i)
                {
                    shells[i].Modify(m);
                }
            }
        }

        /// <summary>
        /// Overrides <see cref="CADability.GeoObject.IGeoObjectImpl.Clone ()"/>
        /// </summary>
        /// <returns></returns>
        public override IGeoObject Clone()
        {
            // auch hier gehen wir von unabhängigen Shells aus, die keine gemeinsamen Faces oder Edges haben
            // das sollte immer so gewährleistet sein!
            Solid res = Solid.Construct();
            res.CopyAttributes(this); // shells gibt es hier noch nicht, damit sollten die ihre Farbe behalten
            if (shells != null)
            {
                res.shells = new Shell[shells.Length];
                for (int i = 0; i < shells.Length; ++i)
                {
                    res.shells[i] = shells[i].Clone() as Shell;
                    res.shells[i].Owner = res;
                }
            }
            res.name = name;
            return res;
        }
        /// <summary>
        /// Overrides <see cref="CADability.GeoObject.IGeoObjectImpl.CopyGeometry (IGeoObject)"/>
        /// </summary>
        /// <param name="ToCopyFrom"></param>
        public override void CopyGeometry(IGeoObject ToCopyFrom)
        {
            Solid cc = ToCopyFrom as Solid;
            using (new Changing(this))
            {
                for (int i = 0; i < shells.Length; ++i)
                {
                    shells[i].CopyGeometryNoEdges(cc.shells[i]);
                }
                for (int i = 0; i < Edges.Length; ++i)
                {
                    edges[i].RecalcCurve3D();
                }
            }
        }
        /// <summary>
        /// Overrides <see cref="CADability.GeoObject.IGeoObjectImpl.FindSnapPoint (SnapPointFinder)"/>
        /// </summary>
        /// <param name="spf"></param>
        public override void FindSnapPoint(SnapPointFinder spf)
        {
            foreach (Shell shell in shells)
            {
                shell.FindSnapPoint(spf);
            }
        }
        /// <summary>
        /// Overrides <see cref="CADability.GeoObject.IGeoObjectImpl.GetBoundingCube ()"/>
        /// </summary>
        /// <returns></returns>
        public override BoundingCube GetBoundingCube()
        {
            BoundingCube res = BoundingCube.EmptyBoundingCube;
            for (int i = 0; i < shells.Length; ++i)
            {
                res.MinMax(shells[i].GetBoundingCube());
            }
            return res;
        }
        /// <summary>
        /// Overrides <see cref="CADability.GeoObject.IGeoObjectImpl.PaintTo3DList (IPaintTo3D, ICategorizedDislayLists)"/>
        /// </summary>
        /// <param name="paintTo3D"></param>
        /// <param name="lists"></param>
        public override void PaintTo3DList(IPaintTo3D paintTo3D, ICategorizedDislayLists lists)
        {   // wenn einzelne Faces verschiedene layer haben, dann...?
            if (Layer != null && Layer.Transparency > 0)
                lists.Add(Layer, false, false, this); // in PaintTo3D wirds dann richtig gemacht
            else
                lists.Add(Layer, true, true, this); // in PaintTo3D wirds dann richtig gemacht
            // paintTo3D.PaintSurfaces und paintTo3D.PaintEdges muss richtig eingestellt und zweimal aufgerufen werden
        }
        public delegate bool PaintTo3DDelegate(Solid toPaint, IPaintTo3D paintTo3D);
        public static PaintTo3DDelegate OnPaintTo3D;
        /// <summary>
        /// Overrides <see cref="CADability.GeoObject.IGeoObjectImpl.PaintTo3D (IPaintTo3D)"/>
        /// </summary>
        /// <param name="paintTo3D"></param>
        public override void PaintTo3D(IPaintTo3D paintTo3D)
        {
            // hier könnte man noch das "Culling" einschalten
            if (OnPaintTo3D != null && OnPaintTo3D(this, paintTo3D)) return;
            for (int i = 0; i < shells.Length; ++i)
            {
                shells[i].PaintTo3D(paintTo3D);
            }
        }
        /// <summary>
        /// Overrides <see cref="CADability.GeoObject.IGeoObjectImpl.PrepareDisplayList (double)"/>
        /// </summary>
        /// <param name="precision"></param>
        public override void PrepareDisplayList(double precision)
        {
            for (int i = 0; i < Shells.Length; ++i)
            {
                shells[i].PrepareDisplayList(precision);
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
            for (int i = 0; i < Shells.Length; ++i)
            {
                res.MinMax(shells[i].GetExtent(projection, extentPrecision));
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
            for (int i = 0; i < Shells.Length; ++i)
            {
                res.Add(shells[i].GetQuadTreeItem(projection, extentPrecision));
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
                if (shells != null)
                {
                    for (int i = 0; i < shells.Length; ++i)
                    {
                        shells[i].Layer = value;
                    }
                }
            }
        }
        public override IGeoObject[] OwnedItems
        {
            get
            {
                return Shells;
            }
        }
        public override string Description
        {
            get
            {
                //if (this.Name != null)
                //{
                //    return StringTable.GetFormattedString("Solid.NamedObject", this.Name);
                //}
                return StringTable.GetString("Solid.Object");
            }
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
            foreach (Shell sh in Shells)
            {
                if (sh.HitTest(ref cube, precision)) return true;
            }
            if (octTreeAlsoCheckInside)
            {   // es könnte sein, dass die Box ganz innerhalb des Solids liegt
                if (GetBoundingCube().Contains(cube))
                {
                    if (shells[0].Contains(cube.GetCenter())) return true;
                }
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
                foreach (Shell sh in Shells)
                {
                    if (!sh.HitTest(projection, rect, onlyInside)) return false;
                }
                return true;
            }
            else
            {
                foreach (Shell sh in Shells)
                {
                    if (sh.HitTest(projection, rect, onlyInside)) return true;
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
                foreach (Shell sh in Shells)
                {
                    if (!sh.HitTest(area, onlyInside)) return false;
                }
                return true;
            }
            else
            {
                foreach (Shell sh in Shells)
                {
                    if (sh.HitTest(area, onlyInside)) return true;
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
            foreach (Shell sh in Shells)
            {
                double d = sh.Position(fromHere, direction, precision);
                if (d < res) res = d;
            }
            return res;
        }
        #endregion
#if DEBUG
        internal new void Debug()
        {
            // DEBUG
            System.GC.Collect();
            System.GC.WaitForPendingFinalizers();
            DebuggerContainer dbg = new DebuggerContainer();
            for (int i = 0; i < shells.Length; ++i)
            {
                for (int j = 0; j < shells[i].Faces.Length; ++j)
                {
                    Face face = shells[i].Faces[j];
                    dbg.Add(face);
                    double umin, umax, vmin, vmax;
                    face.GetUVBounds(out umin, out umax, out vmin, out vmax);
                    GeoPoint2D uv = new GeoPoint2D((umin + umax) / 2.0, (vmin + vmax) / 2.0);
                    GeoPoint p0 = face.Surface.PointAt(uv);
                    GeoVector d0 = face.Surface.GetNormal(uv);
                    if (!face.OrientedOutward) d0 = -d0;
                    dbg.Add(p0, d0, 10, System.Drawing.Color.Red);
                }
            }
            // DebuggerVisualizer.TestShowVisualizer(dbg);
            // END DEBUG
        }
#endif
        public void FreeCachedMemory()
        {
            for (int i = 0; i < shells.Length; i++)
            {
                shells[i].FreeCachedMemory();
            }
        }
        /// <summary>
        /// Unites the two solids and returns the union. If the solids are disjunct, null is returned.
        /// </summary>
        /// <param name="solid1">first solid</param>
        /// <param name="solid2">second solid</param>
        /// <returns>union or null</returns>
        static public Solid Unite(Solid solid1, Solid solid2)
        {
            BRepOperation bro = new BRepOperation(solid1.shells[0], solid2.shells[0], BRepOperation.Operation.union);
            Shell[] res = bro.Result();
            if (res.Length == 1)
            {
                return MakeSolid(res[0]);
            }
            else
            {
                return null;
            }
        }
        /// <summary>
        /// Returns the intersection of two solids, i.e. those parts that are common to both solids. If the 
        /// solids are disjunct, an empty array is returned. There may be more than one solid body as a restult.
        /// </summary>
        /// <param name="solid1">first solid</param>
        /// <param name="solid2">second solid</param>
        /// <returns>array of common parts</returns>
        static public Solid[] Intersect(Solid solid1, Solid solid2)
        {
            // hier fehlt noch der Löcherkäse: Solids mit mehreren Shells
            BRepOperation bro = new BRepOperation(solid1.shells[0], solid2.shells[0], BRepOperation.Operation.intersection);
            Shell[] res = bro.Result();
            Solid[] sres = new Solid[res.Length];
            for (int i = 0; i < res.Length; i++)
            {
                sres[i] = MakeSolid(res[i]);
            }
            return sres;
        }
        /// <summary>
        /// Returns the difference of two solids. The second solid is removed from the first solid. If the
        /// solids are disjunct, (a clone of the) the first solid is returned. If the second solid contains the
        /// first solid, an empty array is returned.
        /// </summary>
        /// <param name="first">first solid</param>
        /// <param name="second">second solid</param>
        /// <returns>the difference</returns>
        static public Solid[] Subtract(Solid first, Solid second)
        {
            // hier fehlt noch der Löcherkäse: Solids mit mehreren Shells
            BRepOperation bro = new BRepOperation(first.shells[0], second.shells[0], BRepOperation.Operation.difference);
            Shell[] res = bro.Result();
            Solid[] sres = new Solid[res.Length];
            for (int i = 0; i < res.Length; i++)
            {
                sres[i] = MakeSolid(res[i]);
            }
            if (sres.Length == 1 && bro.Unchanged) sres[0].flags |= Flags.unchanged;
            return sres;
        }
        /// <summary>
        /// Overrides <see cref="CADability.GeoObject.IGeoObjectImpl.Decompose ()"/>
        /// </summary>
        /// <returns></returns>
        public override GeoObjectList Decompose()
        {
            Shell[] shells = this.Shells;
            GeoObjectList decomp = new GeoObjectList();
            for (int i = 0; i < shells.Length; ++i)
            {
                IGeoObject go = shells[i].Clone();
                go.CopyAttributes(this);
                decomp.Add(go);
            }
            return decomp;
        }
        internal void PreCalcTriangulation(double precisiton)
        {
            for (int i = 0; i < shells.Length; ++i)
            {
                shells[i].PreCalcTriangulation(precisiton);
            }
        }
        internal bool Simplify(double precision)
        {
            bool simplified = false;
            for (int i = 0; i < shells.Length; i++)
            {
                simplified |= shells[i].Simplify(precision);
            }
            return simplified;
        }
        public Solid[] SplitByPlane(Plane pln)
        {
            BRepOperation brepOp = new BRepOperation(Shells[0], pln);
            Shell[] parts = brepOp.Result();
            Solid[] res = new Solid[parts.Length];
            for (int i = 0; i < parts.Length; i++)
            {
                res[i] = Solid.Construct();
                res[i].SetShell(parts[i]);
            }
            return res;
        }
        public GeoPoint[] GetLineIntersection(GeoPoint location, GeoVector direction)
        {
            List<GeoPoint> result = new List<GeoPoint>();
            for (int i = 0; i < shells.Length; i++)
            {
                result.AddRange(shells[i].GetLineIntersection(location, direction));
            }
            return result.ToArray();
        }
        internal bool ShowFeatureAxis
        {
            get
            {
                if (Shells.Length == 0) return false;
                return Shells[0].ShowFeatureAxis;
            }
            set
            {
                using (new ChangingAttribute(this, "ShowFeatureAxis", !value))
                {
                    for (int i = 0; i < Shells.Length; i++)
                    {
                        Shells[i].ShowFeatureAxis = value;
                    }
                }
            }
        }

        #region ISerializable Members
        protected Solid(SerializationInfo info, StreamingContext context)
            : base(context)
        {
            SerializationInfoEnumerator e = info.GetEnumerator();
            while (e.MoveNext())
            {
                switch (e.Name)
                {   // um Exceptions zu verhindern und Def bzw. Colordef gleichermaßen zu lesen
                    default:
                        base.SetSerializationValue(e.Name, e.Value);
                        break;
                    case "Shells":
                        shells = e.Value as Shell[];
                        break;
                    case "Def": // hieß früher dummerweise "Def"
                    case "ColorDef":
                        colorDef = e.Value as ColorDef;
                        break;
                    case "Name":
                        name = e.Value as string;
                        break;
                }
            }
            //shells = info.GetValue("Shells", typeof(Shell[])) as Shell[];
            //colorDef = ColorDef.Read(info, context);
            //if (colorDef == null)
            //{
            //    colorDef = ColorDef.Read("Def", info, context);
            //}
            //try
            //{
            //    name = info.GetString("Name");
            //}
            //catch (SerializationException)
            //{
            //    name = null;
            //}
        }
        void ISerializable.GetObjectData(SerializationInfo info, StreamingContext context)
        {
            base.GetObjectData(info, context);
            info.AddValue("Shells", shells, typeof(Shell[]));
            info.AddValue("ColorDef", colorDef);
            info.AddValue("Name", name);
        }
        public override void SetObjectData(IJsonReadData data)
        {
            base.SetObjectData(data);
            shells = data.GetProperty<Shell[]>("Shells");
            colorDef = data.GetPropertyOrDefault<ColorDef>("ColorDef");
            name = data.GetPropertyOrDefault<string>("Name");
            flags = data.GetPropertyOrDefault<Flags>("Flags");
            data.RegisterForSerializationDoneCallback(this);
        }
        public override void GetObjectData(IJsonWriteData data)
        {
            base.GetObjectData(data);
            data.AddProperty("Shells", shells);
            data.AddProperty("ColorDef", colorDef);
            data.AddProperty("Name", name);
            data.AddProperty("Flags", flags);
        }
        #endregion
        #region IDeserializationCallback Members
        void IDeserializationCallback.OnDeserialization(object sender)
        {
            for (int i = 0; i < shells.Length; ++i)
            {
                shells[i].Owner = this;
                shells[i].Layer = this.Layer;
                shells[i].WillChangeEvent += new ChangeDelegate(OnWillChange);
                shells[i].DidChangeEvent += new ChangeDelegate(OnDidChange);
            }
            if (Constructed != null) Constructed(this);
        }
        void IJsonSerializeDone.SerializationDone(JsonSerialize jsonSerialize)
        {
            for (int i = 0; i < shells.Length; ++i)
            {
                shells[i].Owner = this;
                shells[i].Layer = this.Layer;
                shells[i].WillChangeEvent += new ChangeDelegate(OnWillChange);
                shells[i].DidChangeEvent += new ChangeDelegate(OnDidChange);
            }
            if (Constructed != null) Constructed(this);
        }
        #endregion
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
                    if (shells != null)
                    {
                        for (int i = 0; i < shells.Length; ++i)
                        {
                            shells[i].ColorDef = value;
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
                if (shells != null)
                {
                    for (int i = 0; i < shells.Length; ++i)
                    {
                        if (shells[i].ColorDef == null) (shells[i] as IColorDef).SetTopLevel(newValue, true);
                    }
                }
            }
        }
        #endregion
        #region IGetSubShapes Members
        IGeoObject IGetSubShapes.GetEdge(int[] id, int index)
        {
            for (int i = 0; i < shells.Length; ++i)
            {
                if (shells[i].UniqueId == id[index])
                    return (shells[i] as IGetSubShapes).GetEdge(id, index + 1);
            }
            return null; // sollte nicht vorkommen
        }
        IGeoObject IGetSubShapes.GetFace(int[] id, int index)
        {
            for (int i = 0; i < shells.Length; ++i)
            {
                if (shells[i].UniqueId == id[index])
                    return (shells[i] as IGetSubShapes).GetFace(id, index + 1);
            }
            return null; // sollte nicht vorkommen
        }
        #endregion
        #region IGeoObjectOwner Members
        void IGeoObjectOwner.Remove(IGeoObject toRemove)
        {
            if (toRemove is Shell shell && shell == Shells[0])
            {
                using (new Changing(this, "SetShell", Shells[0]))
                {
                    shells = new Shell[0];
                }
            }
        }
        void IGeoObjectOwner.Add(IGeoObject toAdd)
        {
            if (toAdd is Shell shell)
            {
                using (new Changing(this, "RemoveShell", shell))
                {
                    if (Shells == null || Shells.Length == 0) shells = new Shell[1];
                    Shells[0] = shell;
                    shell.Owner = this;
                }
            }
        }
        #endregion

        internal void AssertOutwardOrientation()
        {
            shells[0].AssertOutwardOrientation(); // there must be at least one Shell (and in most cases this is the only one)
            for (int i = 1; i < shells.Length; ++i)
            {   // all inner Shells are reverse oriented, i.e. the (normals of the) faces point away from the solid, into the inner part of the holes
                shells[i].AssertOutwardOrientation();
                shells[i].ReverseOrientation();
            }
            flags |= Flags.oriented;
        }

        int IExportStep.Export(ExportStep export, bool topLevel)
        {   // MANIFOLD_SOLID_BREP is a Geometric_Representation_Item
            if (string.IsNullOrEmpty(shells[0].Name)) shells[0].Name = NameOrEmpty;
            int shellnr = (shells[0] as IExportStep).Export(export, false);
            int msb = export.WriteDefinition("MANIFOLD_SOLID_BREP('" + NameOrEmpty + "',#" + shellnr.ToString() + ")");
            if (colorDef != null)
            {
                colorDef.MakeStepStyle(msb, export);
            }
            //#731 = PRODUCT( 'A0501_SASIL_plus_00_50_185_3_polig', 'A0501_SASIL_plus_00_50_185_3_polig', 'PART-A0501_SASIL_plus_00_50_185_3_polig-DESC', ( #2 ) );
            //#1442 = PRODUCT_DEFINITION_FORMATION_WITH_SPECIFIED_SOURCE( ' ', 'NONE', #731, .NOT_KNOWN. );
            //#732 = PRODUCT_DEFINITION( 'NONE', 'NONE', #1442, #3 );
            //#253 = PRODUCT_DEFINITION_SHAPE( 'NONE', 'NONE', #732 );
            //#254 = ADVANCED_BREP_SHAPE_REPRESENTATION( 'A0501_SASIL_plus_00_50_185_3_polig', ( #733 ), #4 );
            //#9 = SHAPE_DEFINITION_REPRESENTATION( #253, #254 );
            int product = export.WriteDefinition("PRODUCT( '" + NameOrEmpty + "','" + NameOrEmpty + "','',(#2))");
            int pdf = export.WriteDefinition("PRODUCT_DEFINITION_FORMATION_WITH_SPECIFIED_SOURCE( ' ', 'NONE', #" + product.ToString() + ", .NOT_KNOWN. )");
            int pd = export.WriteDefinition("PRODUCT_DEFINITION( 'NONE', 'NONE', #" + pdf.ToString() + ", #3 )");
            int pds = export.WriteDefinition("PRODUCT_DEFINITION_SHAPE( 'NONE', 'NONE', #" + pd.ToString() + " )");
            int brep = export.WriteDefinition("ADVANCED_BREP_SHAPE_REPRESENTATION('" + NameOrEmpty + "', ( #" + msb.ToString() + "), #4 )");
            export.WriteDefinition("SHAPE_DEFINITION_REPRESENTATION( #" + pds.ToString() + ", #" + brep.ToString() + ")");
            return brep;
        }

        private class BRepOpAction : ConstructAction
        {
            private Solid solid;

            public BRepOpAction(Solid solid)
            {
                this.solid = solid;
            }
            public override string GetID()
            {
                throw new NotImplementedException();
            }
            public override void OnSetAction()
            {
                base.OnSetAction();
            }

            public override bool OnCommand(string MenuId)
            {
                return base.OnCommand(MenuId);
            }
            public override bool OnUpdateCommand(string MenuId, CommandState CommandState)
            {
                return base.OnUpdateCommand(MenuId, CommandState);
            }
        }

        public bool SameGeometry(Solid other)
        {
            if (this.Shells.Length != other.Shells.Length) return false;
            for (int i = 0; i < this.Shells.Length; i++)
            {
                if (!Shells[i].SameGeometry(other.Shells[i])) return false;
            }
            return true;
        }

        /// <summary>
        /// Returns a menu, which is shown when there is a right click on the solid
        /// </summary>
        /// <param name="frame"></param>
        /// <returns></returns>
        internal MenuWithHandler[] GetContextMenu(IFrame frame)
        {
            List<MenuWithHandler> res = new List<MenuWithHandler>();
            Model owner = this.Owner as Model;
            if (owner != null)
            {
                GeoObjectList fromBox = owner.GetObjectsFromBox(this.GetExtent(0.0));
                List<Solid> otherSolids = new List<Solid>();
                for (int i = 0; i < fromBox.Count; i++)
                {
                    if (fromBox[i] is Solid sld && sld != this) otherSolids.Add(sld);
                }
                if (otherSolids.Count > 0)
                {   // there are other solids close to this solid, it is not guaranteed that these other solids interfere with this solid 
                    MenuWithHandler mhSubtractFrom = new MenuWithHandler();
                    mhSubtractFrom.ID = "MenuId.Solid.RemoveFrom";
                    mhSubtractFrom.Text = StringTable.GetString("MenuId.Solid.RemoveFrom", StringTable.Category.label);
                    mhSubtractFrom.Target = new BRepOpAction(this);
                    res.Add(mhSubtractFrom);
                    MenuWithHandler mhSubtractFromAll = new MenuWithHandler();
                    mhSubtractFromAll.ID = "MenuId.Solid.RemoveFromAll";
                    mhSubtractFromAll.Text = StringTable.GetString("MenuId.Solid.RemoveFromAll", StringTable.Category.label);
                    mhSubtractFromAll.Target = new BRepOpWith(this, otherSolids, frame);
                    res.Add(mhSubtractFromAll);
                    MenuWithHandler mhSubtractAllFromThis = new MenuWithHandler();
                    mhSubtractAllFromThis.ID = "MenuId.Solid.RemoveAll";
                    mhSubtractAllFromThis.Text = StringTable.GetString("MenuId.Solid.RemoveAll", StringTable.Category.label);
                    mhSubtractAllFromThis.Target = new BRepOpWith(this, otherSolids, frame);
                    res.Add(mhSubtractAllFromThis);
                    MenuWithHandler mhUniteWith = new MenuWithHandler();
                    mhUniteWith.ID = "MenuId.Solid.UniteWith";
                    mhUniteWith.Text = StringTable.GetString("MenuId.Solid.UniteWith", StringTable.Category.label);
                    mhUniteWith.Target = new BRepOpAction(this);
                    res.Add(mhUniteWith);
                    MenuWithHandler mhUniteWithAll = new MenuWithHandler();
                    mhUniteWithAll.ID = "MenuId.Solid.UniteWithAll";
                    mhUniteWithAll.Text = StringTable.GetString("MenuId.Solid.UniteWithAll", StringTable.Category.label);
                    mhUniteWithAll.Target = new BRepOpWith(this, otherSolids, frame);
                    res.Add(mhUniteWithAll);
                    MenuWithHandler mhIntersectWith = new MenuWithHandler();
                    mhIntersectWith.ID = "MenuId.Solid.IntersectWith";
                    mhIntersectWith.Text = StringTable.GetString("MenuId.Solid.IntersectWith", StringTable.Category.label);
                    mhIntersectWith.Target = new BRepOpAction(this);
                    res.Add(mhIntersectWith);
                    MenuWithHandler mhSplitWith = new MenuWithHandler();
                    mhSplitWith.ID = "MenuId.Solid.SplitWith";
                    mhSplitWith.Text = StringTable.GetString("MenuId.Solid.SplitWith", StringTable.Category.label);
                    mhSplitWith.Target = new BRepOpAction(this);
                    res.Add(mhSplitWith);
                    MenuWithHandler mhSplitWithAll = new MenuWithHandler();
                    mhSplitWithAll.ID = "MenuId.Solid.SplitWithAll";
                    mhSplitWithAll.Text = StringTable.GetString("MenuId.Solid.SplitWithAll", StringTable.Category.label);
                    mhSplitWithAll.Target = new BRepOpWith(this, otherSolids, frame);
                    res.Add(mhSplitWithAll);
                }
            }
            MenuWithHandler mhtr = new MenuWithHandler();
            if (this.Layer != null && this.Layer.Name == "CADability.Transparent")
            {
                mhtr.ID = "MenuId.MakeOpaque";
                mhtr.Text = StringTable.GetString("MenuId.MakeOpaque", StringTable.Category.label);
                mhtr.Target = SimpleMenuCommand.HandleCommand((menuId) =>
                {   // reset the layer of this solid
                    Layer layer = UserData.GetData("CADability.OriginalLayer") as Layer;
                    if (layer != null)
                    {
                        Layer = layer;
                        UserData.RemoveUserData("CADability.OriginalLayer");
                    }
                    else
                    {
                        Style sldstl = frame.Project.StyleList.GetDefault(Style.EDefaultFor.Solids);
                        if (sldstl != null && sldstl.Layer != null) Layer = sldstl.Layer;
                    }
                    return true;
                });
                res.Add(mhtr);
            }
            else
            {
                mhtr.ID = "MenuId.MakeTransparent";
                mhtr.Text = StringTable.GetString("MenuId.MakeTransparent", StringTable.Category.label);
                mhtr.Target = SimpleMenuCommand.HandleCommand((menuId) =>
                {   // reset the layer of this solid
                    UserData.Add("CADability.OriginalLayer", Layer);
                    Layer layer = frame.Project.LayerList.CreateOrFind("CADability.Transparent");
                    layer.Transparency = 128; // should be configurable
                    Layer = layer;
                    return true;
                });
                res.Add(mhtr);
            }
            MenuWithHandler mhhide = new MenuWithHandler();
            mhhide.ID = "MenuId.Solid.Hide"; // hide this solid
            mhhide.Text = StringTable.GetString("MenuId.Solid.Hide", StringTable.Category.label);
            mhhide.Target = SimpleMenuCommand.HandleCommand((menuId) =>
            {
                if (!UserData.ContainsData("CADability.OriginalLayer"))
                {
                    if (Layer == null)
                    {
                        Style sldstl = frame.Project.StyleList.GetDefault(Style.EDefaultFor.Solids);
                        if (sldstl != null && sldstl.Layer != null) Layer = sldstl.Layer;
                    }
                    UserData.Add("CADability.OriginalLayer", Layer);
                }
                Layer layer = frame.Project.LayerList.CreateOrFind("CADability.Hidden");
                Layer = layer;
                if (frame.ActiveView is ModelView mv) mv.SetLayerVisibility(layer, false);
                return true;
            });
            res.Add(mhhide);
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
            MenuWithHandler mhremove = new MenuWithHandler();
            mhremove.ID = "MenuId.Remove";
            mhremove.Text = StringTable.GetString("MenuId.Remove", StringTable.Category.label);
            mhremove.Target = SimpleMenuCommand.HandleCommand((menuId) =>
            {
                Model model = Owner as Model;
                if (model != null) model.Remove(this);
                return true;
            });
            res.Add(mhremove);
            return res.ToArray();
        }
    }
    public class BRepOpWith : ICommandHandler
    {
        private Solid solid;
        private List<Solid> other;
        private IFrame frame;
        public BRepOpWith(Solid solid, List<Solid> other, IFrame frame)
        {
            this.solid = solid;
            this.other = other;
            this.frame = frame;
        }

        bool ICommandHandler.OnCommand(string MenuId)
        {
            // make a collision detection of the solid with the other solids here. Maybe this object was constructed without ever beeing used as a command target
            // so the collision detection in the constructor would be unnecessary.
            // Collision detection is not reliable when the two solid only share a common face, i.e. we want to glue it together
            //for (int i = other.Count-1; i >=0; --i)
            //{
            //    CollisionDetection cd = new CollisionDetection(solid.Shells[0], other[i].Shells[0]);
            //    if (!cd.GetResult(Precision.eps, out _)) other.RemoveAt(i);
            //}

            switch (MenuId)
            {
                case "MenuId.Solid.RemoveFromAll":
                    using (frame.Project.Undo.UndoFrame)
                    {
                        foreach (Solid sld in other)
                        {
                            Solid[] res = Solid.Subtract(sld, solid);
                            if (res != null)
                            {
                                if (res.Length != 1 || !res[0].flags.HasFlag(Solid.Flags.unchanged))
                                {
                                    IGeoObjectOwner owner = sld.Owner;
                                    owner.Remove(sld);
                                    for (int i = 0; i < res.Length; i++)
                                    {
                                        owner.Add(res[i]);
                                    }
                                }
                            }
                        }
                        solid.Owner.Remove(solid);
                    }
                    break;
                case "MenuId.Solid.UniteWithAll":
                    {
                        using (frame.Project.Undo.UndoFrame)
                        {
                            Solid accumulate = solid;
                            int count = 0;
                            foreach (Solid sld in other)
                            {
                                Solid tmp = Solid.Unite(sld, accumulate);
                                if (tmp != null)
                                {
                                    accumulate = tmp;
                                    count++;
                                    sld.Owner.Remove(sld);
                                }
                            }
                            IGeoObjectOwner owner = solid.Owner;
                            if (owner != null)
                            {
                                owner.Remove(solid);
                                owner.Add(accumulate);
                            }
                        }
                        // if (frame != null) frame.UIService.ShowMessageBox(StringTable.GetString("Menu.NotImplemented", StringTable.Category.label), StringTable.GetString("Error"), MessageBoxButtons.OK);
                    }
                    break;
                case "MenuId.Solid.RemoveAll":
                    using (frame.Project.Undo.UndoFrame)
                    {
                        List<Solid> fragments = new List<Solid>();
                        fragments.Add(solid);
                        foreach (Solid sld in other)
                        {
                            List<Solid> newfragments = new List<Solid>();
                            for (int i = 0; i < fragments.Count; i++)
                            {
                                Solid[] res = Solid.Subtract(fragments[i], sld);
                                if (res != null && res.Length > 0)
                                {
                                    newfragments.AddRange(res);
                                }
                                else
                                {
                                    newfragments.Add(fragments[i]);
                                }
                            }
                            fragments = newfragments;
                        }
                        IGeoObjectOwner owner = solid.Owner;
                        owner.Remove(solid);
                        foreach (Solid sld in other)
                        {
                            owner.Remove(sld);
                        }
                        for (int i = 0; i < fragments.Count; i++)
                        {
                            owner.Add(fragments[i]);
                        }
                    }
                    break;
                case "MenuId.Solid.SplitWithAll":
                    {
                        List<Solid> fragments = new List<Solid>();
                        fragments.Add(solid);
                        foreach (Solid sld in other)
                        {
                            List<Solid> newfragments = new List<Solid>();
                            for (int i = 0; i < fragments.Count; i++)
                            {
                                Solid[] res1 = Solid.Subtract(fragments[i], sld);
                                Solid[] res2 = Solid.Subtract(sld, fragments[i]);
                                Solid[] res3 = Solid.Intersect(fragments[i], sld);
                                if (res1 != null && res1.Length > 0)
                                {
                                    if (res1 != null) newfragments.AddRange(res1);
                                    if (res2 != null) newfragments.AddRange(res2);
                                    if (res3 != null) newfragments.AddRange(res3);
                                }
                                else
                                {
                                    newfragments.Add(fragments[i]);
                                }
                            }
                            fragments = newfragments;
                        }
                        IGeoObjectOwner owner = solid.Owner;
                        owner.Remove(solid);
                        foreach (Solid sld in other)
                        {
                            owner.Remove(sld);
                        }
                        for (int i = 0; i < fragments.Count; i++)
                        {
                            owner.Add(fragments[i]);
                        }
                    }
                    break;
                case "MenuId.Solid.IntersectWithAll":
                    {
                        List<Solid> fragments = new List<Solid>();
                        fragments.Add(solid);
                        foreach (Solid sld in other)
                        {
                            List<Solid> newfragments = new List<Solid>();
                            for (int i = 0; i < fragments.Count; i++)
                            {
                                Solid[] res = Solid.Intersect(fragments[i], sld);
                                if (res != null && res.Length > 0)
                                {
                                    newfragments.AddRange(res);
                                }
                                else
                                {
                                    newfragments.Add(fragments[i]);
                                }
                            }
                            fragments = newfragments;
                        }
                        IGeoObjectOwner owner = solid.Owner;
                        owner.Remove(solid);
                        foreach (Solid sld in other)
                        {
                            owner.Remove(sld);
                        }
                        for (int i = 0; i < fragments.Count; i++)
                        {
                            owner.Add(fragments[i]);
                        }
                    }
                    break;
                default:
                    frame.UIService.ShowMessageBox(StringTable.GetString("Menu.NotImplemented", StringTable.Category.label), StringTable.GetString("Error"), MessageBoxButtons.OK);
                    break;
            }
            return false;
        }

        void ICommandHandler.OnSelected(MenuWithHandler selectedMenu, bool selected)
        {
        }

        bool ICommandHandler.OnUpdateCommand(string MenuId, CommandState CommandState)
        {
            return true;
        }
    }
}
