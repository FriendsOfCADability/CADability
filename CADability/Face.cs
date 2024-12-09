using CADability.Actions;
using CADability.Attribute;
using CADability.Curve2D;
using MathNet.Numerics.LinearAlgebra.Double;
using CADability.Shapes;
using CADability.UserInterface;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Runtime.Serialization;
using System.Text;
using System.Threading;
using Wintellect.PowerCollections;
#if WEBASSEMBLY
using CADability.WebDrawing;
using Point = CADability.WebDrawing.Point;
#else
using System.Drawing;
using Point = System.Drawing.Point;
#endif

namespace CADability.GeoObject
{
    internal class ShowPropertyFace : PropertyEntryImpl, IGeoObjectShowProperty, ICommandHandler
    {
        Face face; // represented;
        private IPropertyEntry[] attributeProperties; // Anzeigen für die Attribute (Ebene, Farbe u.s.w)
        public ShowPropertyFace(Face face, IFrame frame)
            : base(frame)
        {
            resourceId = "Face.Object";
            this.face = face;
            attributeProperties = face.GetAttributeProperties(frame);
        }
        public override PropertyEntryType Flags
        {
            get
            {
                return PropertyEntryType.GroupTitle | PropertyEntryType.ContextMenu | PropertyEntryType.Selectable | PropertyEntryType.HasSubEntries;
            }
        }
        public override MenuWithHandler[] ContextMenu
        {
            get
            {
                List<MenuWithHandler> menuWithHandlers = new List<MenuWithHandler>();

                face.GetAdditionalContextMenue(this, Frame, menuWithHandlers);
                return menuWithHandlers.ToArray();
            }
        }
        private IPropertyEntry[] subItems;
        public override IPropertyEntry[] SubItems
        {
            get
            {
                if (subItems == null)
                {
                    List<IPropertyEntry> se = new List<IPropertyEntry>();
#if DEBUG
                    IntegerProperty dbghashcode = new IntegerProperty(face.GetHashCode(), "Debug.Hashcode");
                    se.Add(dbghashcode);
#endif
                    se.Add(new NameProperty(this.face, "Name", "Face.Name"));
                    if (face.Surface.GetPropertyEntry(this.Frame) != null)
                    {
                        se.Add(face.Surface.GetPropertyEntry(Frame));
                    }
                    SimplePropertyGroup edgeprops = new SimplePropertyGroup("Face.Edge");
                    foreach (Edge edge in face.AllEdges)
                    {
                        if (edge.Curve3D != null && edge.Curve3D is IGeoObject)
                        {
                            IPropertyEntry sp = (edge.Curve3D as IGeoObject).GetShowProperties(base.Frame);
                            sp.ReadOnly = true;
                            edgeprops.Add(sp);
                        }
                    }
                    se.Add(edgeprops);
                    for (int i = 0; i < attributeProperties.Length; i++)
                    {
                        se.Add(attributeProperties[i] as IPropertyEntry);
                    }
                    subItems = se.ToArray();
                }
                return subItems;
            }
        }

        #region IGeoObjectShowProperty Members

        public event CreateContextMenueDelegate CreateContextMenueEvent;

        public IGeoObject GetGeoObject()
        {
            return face;
        }

        public string GetContextMenuId()
        {
            return "MenuId.Object.Face";
        }

        #endregion

        #region ICommandHandler Members

        bool ICommandHandler.OnCommand(string MenuId)
        {
            switch (MenuId)
            {
                case "MenuId.Outline":
                    if (Frame.ActiveAction is SelectObjectsAction)
                    {
                        using (Frame.Project.Undo.UndoFrame)
                        {
                            IGeoObjectOwner addTo = face.Owner;
                            if (!(addTo is Shell))
                            {
                                if (addTo == null) addTo = Frame.ActiveView.Model;
                                GeoObjectList toSelect = new GeoObjectList();
                                addTo.Remove(face);
                                for (int i = 0; i < face.AllEdges.Length; i++)
                                {
                                    IGeoObject go = face.AllEdges[i].Curve3D as IGeoObject;
                                    if (go != null)
                                    {
                                        go.CopyAttributes(face);
                                        addTo.Add(go);
                                        toSelect.Add(go);
                                    }
                                }
                                SelectObjectsAction soa = Frame.ActiveAction as SelectObjectsAction;
                                soa.SetSelectedObjects(toSelect); // alle Teilobjekte markieren
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
                case "MenuId.Outline":
                    CommandState.Enabled = true; // naja isses ja immer
                    return true;
            }
            return false;
        }
        void ICommandHandler.OnSelected(MenuWithHandler selectedMenuItem, bool selected) { }

        #endregion
    }

    internal interface IGetSubShapes
    {
        IGeoObject GetEdge(int[] id, int index);
        IGeoObject GetFace(int[] id, int index);
    }

    /// <summary>
    /// A face is a finite piece of a surface. It is bounded by a <see cref="SimpleShape"/> in the 2-dimensional u/v space
    /// of the surface. A face is also a <see cref="IGeoObject"/>. The bounding curves of a face are implemented as <see cref="Edge"/>s.
    /// If a face is contained in a <see cref="Model"/> it doesnt share its edges with other faces. If a face is part of a <see cref="Shell"/>
    /// (which maybe a part of a <see cref="Solid"/>) it shares some or all of its edges with other faces.
    /// </summary>
    [Serializable()]
    [DebuggerDisplayAttribute("Face, hc = {hashCode.ToString()}")]
    public class Face : IGeoObjectImpl, ISerializable, IColorDef, IGetSubShapes, IDeserializationCallback, IJsonSerialize, IJsonSerializeDone, IExportStep
    {
        /*
         * Die Konsistenz der Daten im Zusammenhang mit OpenCascade:
         * Face, Shell bzw. Solid haben jeweils ein Helper Objekt, welches nach dem Deserialisieren null ist.
         * Die Edges haben auch ein null Helper Objekt. Ein Problem entsteht daraus, dass es nicht möglich ist
         * (bzw. ich nichts gefunden habe) ein Edge im Sinne on ocas zu erzeugen, welches also eine 3D Curve hat
         * und gleichzeitig auf 2 Faces liegt (2 PCurves) Offensichtlich geht das nur wieder von oben herab:
         * Für ein einzelnes unabhängiges Face: BRepBuilderAPI.MakeFace, zuvor mit MakeEdge alle edges erzeugen,
         * dann mit MakeWire die Wires, dann  mit MakeFace das Face. Solcherart erzeugte faces sind unabhängig,
         * d.h. sie müssen ggf. erst mit BRepAlgo.Sewing oder mit BRepOffsetAPI.Sewing zusammengesetzt werden.
         * Nach diesem zusammensetzen wird wieder das ganze Ding analysiert und von oben herab neu erzeugt, d.h.
         * alte Faces und Edges werden weggeschmissen und die neuen stattdessen verwendet, wenn es ein Solid oder 
         * Shell war. Es wäre ja schön, man könnte seine Faces wiederfinden, aber wie sollte das gehen?
         * 
         */
        private ISurface surface;
        private Edge[] outline; // die Kanten des Umrisses in richtiger Reihenfolge, die jeweilige Richtung steht in Edge
        private Edge[][] holes; // die Kanten der Löcher in richtiger Reihenfolge, die jeweilige Richtung steht in Edge
        private bool orientedOutward; // wenn das Face Bestandteil eines solid ist, dann kann hier festgestellt werden

        internal IEnumerable<Face> GetSameSurfaceConnected()
        {
            HashSet<Face> res = new HashSet<Face>();
            for (int i = 0; i < outline.Length; i++)
            {
                if (SameSurface(outline[i].OtherFace(this))) res.Add(outline[i].OtherFace(this));
            }
            return res;
        }

        // ob es so orientiert ist, dass der Normalenvektor nach außen zeigt
        internal static int hashCodeCounter = 0; // jedes Face bekommt eine Nummer, damit ist es für den HasCode Algorithmus einfach
        private int hashCode;
        internal Face isPartialFaceOf; // Teilface von diesem wird nicht mehr benutzt
        private BoundingCube extent;
        private Vertex[] vertices;
        private string name;

        #region polymorph construction
        public delegate Face ConstructionDelegate();
        public static ConstructionDelegate Constructor;
        public static Face Construct()
        {
            if (Constructor != null) return Constructor();
            return new Face();
        }
        public delegate void ConstructedDelegate(Face justConstructed);
        public static ConstructedDelegate Constructed;
        #endregion
        protected Face()
            : base()
        {
            lockTriangulationRecalc = new object();
            lockTriangulationData = new object();
            hashCode = hashCodeCounter++;
            extent = BoundingCube.EmptyBoundingCube;
            if (Constructed != null) Constructed(this);
#if DEBUG
            if (hashCode == 1087)
            {

            }

#endif
        }
        /// <summary>
        /// Internal use only
        /// </summary>
        /// <returns></returns>
        public override int GetHashCode()
        {
            return hashCode;
        }

        /// <summary>
        /// Make a face (or maybe multiple faces in case of periodic surfaces) from the definition in a step file
        /// </summary>
        /// <param name="surface">the surface as defined in the step file</param>
        /// <param name="loops">the loops of 3d edges, both outlines and holes, for this face</param>
        /// <param name="sameSense">orientation of the surface so that the normal points outward</param>
        /// <returns></returns>
        internal static Face[] MakeFacesFromStepAdvancedFace(ISurface surface, List<List<StepEdgeDescriptor>> loops, bool sameSense, double precision)
        {
            List<object> toLock = new List<object>();
            if (Settings.GlobalSettings.GetBoolValue("StepImport.Parallel", false))
            {
                List<StepEdgeDescriptor> allEdgeDescriptors = new List<StepEdgeDescriptor>();
                {
                    for (int i = 0; i < loops.Count; i++)
                    {
                        for (int j = loops[i].Count - 1; j >= 0; --j)
                        {
                            allEdgeDescriptors.Add(loops[i][j]);
                        }
                    }
                    // we need exclusive access to all edges of the loop, because they might be modified during the creation
                    allEdgeDescriptors.Sort(delegate (StepEdgeDescriptor s1, StepEdgeDescriptor s2) { return s1.locker.original.id.CompareTo(s2.locker.original.id); });
                    // each StepEdgeDescriptor describes an edge in respect to a distinct face. Typically we have two different StepEdgeDescriptors for a single edge,
                    // one for each face, but they contain the same (maybe empty) list "createdEdges", and that is the object we need to lock here
                    for (int i = 0; i < allEdgeDescriptors.Count; i++)
                    {
                        toLock.Add(allEdgeDescriptors[i].locker);
                    }
                }
            }
            using (ParallelHelper.LockMultipleObjects(toLock.ToArray())) // toLock is empty in case of not parallel
            {
#if DEBUG
                DebuggerContainer dcLoops = new DebuggerContainer();
                for (int i = 0; i < loops.Count; i++)
                {
                    for (int j = 0; j < loops[i].Count; ++j)
                    {
                        if (loops[i][j].createdEdges != null && loops[i][j].createdEdges.Count > 0)
                        {
                            if (loops[i][j].curve != null) dcLoops.Add(loops[i][j].curve.Clone() as IGeoObject, System.Drawing.Color.Green, i * 1000 + j);
                            for (int k = 0; k < loops[i][j].createdEdges.Count; k++)
                            {
                                if (loops[i][j].createdEdges[k] != null) dcLoops.Add(loops[i][j].createdEdges[k].Curve3D.Clone() as IGeoObject, System.Drawing.Color.Blue, i * 1000 + j * 10 + k);
                            }
                        }
                        else
                        {
                            if (loops[i][j].curve != null) dcLoops.Add(loops[i][j].curve.Clone() as IGeoObject, System.Drawing.Color.Red, i * 1000 + j);
                        }
                    }
                }
#endif
                if (!sameSense)
                {   // in CADability the surfaces can be oriented both ways, in step, the standard surfaces (cylinder, sphere, torus, cone) are always "outward" oriented
                    // and if necessary the sameSense is false. Here we convert the surface to the CADability requirements
                    sameSense = true;
                    surface = surface.Clone();
                    surface.ReverseOrientation(); // 2d modification is not relevant here
                }
                BoundingCube loopExtend = BoundingCube.EmptyBoundingCube;
                for (int i = loops.Count - 1; i >= 0; --i)
                {
                    if (loops[i].Count == 1 && loops[i][0].curve == null && loops[i][0].vertex1 == loops[i][0].vertex1)
                    {   // this is probably a pole of a cone (or sphere) created as a point. We don't need that
                        loops[i].RemoveAt(0);
                    }
                    for (int j = loops[i].Count - 1; j >= 0; --j)
                    {
                        loopExtend.MinMax(loops[i][j].vertex1.Position);
                        int next = j - 1;
                        if (next < 0) next = loops[i].Count - 1;
                        if (loops[i][next].vertex1 == loops[i][j].vertex1 && loops[i][next].vertex2 == loops[i][j].vertex2 && loops[i][j].forward != loops[i][next].forward
                            && loops[i][j].curve.SameGeometry(loops[i][next].curve, precision))
                        {   // two adjacent edges are exactly opposite curves: makes no sense, remove both (e.g. "171_Sch-AufsORIG.stp")
                            if (j == 0)
                            {
                                loops[i].RemoveAt(next);
                                loops[i].RemoveAt(j);
                            }
                            else
                            {
                                loops[i].RemoveAt(j);
                                loops[i].RemoveAt(next);
                                --j;
                            }
                        }
                    }
                    if (loops[i].Count == 0) loops.RemoveAt(i);
                }
                // if a loop contains two identical edges, which are back and forth, we remove this pair and split the loop into two parts
                double vertexDiff = loopExtend.Size * 1e-6; // consider vertices identical if closer than this value
                for (int i = loops.Count - 1; i >= 0; --i)
                {
                    bool seamFound = false;
                    for (int j = 0; j < loops[i].Count; j++)
                    {
                        for (int k = j + 1; k < loops[i].Count; k++)
                        {
                            if (loops[i][j].curve.SameGeometry(loops[i][k].curve, precision))
                            {
                                if ((loops[i][k].vertex1 == loops[i][j].vertex1 && loops[i][k].vertex2 == loops[i][j].vertex2) ||
                                    (loops[i][k].vertex1 == loops[i][j].vertex2 && loops[i][k].vertex2 == loops[i][j].vertex1))
                                {
                                    loops[i][j].isSeam = loops[i][k].isSeam = true;
                                    seamFound = true;
                                }
                                else
                                {   // maybe different vertices with identical positions as in "1242_14_PUNZONE.stp"
                                    if ((loops[i][k].vertex1.Position | loops[i][j].vertex1.Position) < vertexDiff && (loops[i][k].vertex2.Position | loops[i][j].vertex2.Position) < vertexDiff)
                                    {
                                        loops[i][k].vertex1.MergeWith(loops[i][j].vertex1);
                                        loops[i][k].vertex2.MergeWith(loops[i][j].vertex2);
                                        loops[i][j].isSeam = loops[i][k].isSeam = true;
                                        seamFound = true;
                                    }
                                    else if ((loops[i][k].vertex1.Position | loops[i][j].vertex2.Position) < vertexDiff && (loops[i][k].vertex2.Position | loops[i][j].vertex1.Position) < vertexDiff)
                                    {
                                        loops[i][k].vertex1.MergeWith(loops[i][j].vertex2);
                                        loops[i][k].vertex2.MergeWith(loops[i][j].vertex1);
                                        loops[i][j].isSeam = loops[i][k].isSeam = true;
                                        seamFound = true;
                                    }
                                }
                            }
                        }
                    }
                    if (seamFound)
                    {   // we cannot assume, there is only one seam, see "Pos 3.stp"
                        //List<StepEdgeDescriptor>[] newLoops = new List<StepEdgeDescriptor>[] { new List<StepEdgeDescriptor>(), new List<StepEdgeDescriptor>() };
                        //int ind = 0;
                        //for (int j = 0; j < loops[i].Count; j++)
                        //{
                        //    if (loops[i][j].isSeam)
                        //    {
                        //        ind = 1 - ind;
                        //        continue;
                        //    }
                        //    newLoops[ind].Add(loops[i][j]);
                        //}
                        //loops.RemoveAt(i);
                        //if (newLoops[0].Count > 0) loops.Add(newLoops[0]);
                        //if (newLoops[1].Count > 0) loops.Add(newLoops[1]);
                        List<List<StepEdgeDescriptor>> newLoops = new List<List<StepEdgeDescriptor>>();
                        for (int j = 0; j < loops[i].Count; j++)
                        {
                            if (!loops[i][j].isSeam)
                            {   // find a loop to connect this edge to, or create a new loop
                                bool added = false;
                                for (int k = 0; k < newLoops.Count; k++)
                                {
                                    if (newLoops[k][newLoops[k].Count - 1].LastVertex == loops[i][j].FirstVertex)
                                    {
                                        newLoops[k].Add(loops[i][j]);
                                        added = true;
                                        break;
                                    }
                                    else if (newLoops[k][0].FirstVertex == loops[i][j].LastVertex)
                                    {
                                        newLoops[k].Insert(0, loops[i][j]);
                                        added = true;
                                        break;
                                    }
                                }
                                if (!added)
                                {
                                    List<StepEdgeDescriptor> lse = new List<StepEdgeDescriptor>();
                                    lse.Add(loops[i][j]);
                                    newLoops.Add(lse);
                                }
                            }
                        }
                        loops.RemoveAt(i);
                        loops.AddRange(newLoops);
                    }
                }
                if (loops.Count == 0) return null; // there is only a seam (in some cases a very narrow face, which is smaller than precision)
                // simply remove loop edges which are too short
                for (int i = 0; i < loops.Count; i++)
                {
                    for (int j = loops[i].Count - 1; j >= 0; --j)
                    {
                        if (loops[i][j].curve != null && loops[i][j].curve.Length < precision / 2.0)
                        {
                            //loops[i].RemoveAt(j);
                        }
                    }
                }
                // Some problem arise, because the curves may be not very precise. We try to adjust start- and endpoints
                // so the connections are precise
                bool orientationChanged = false;
                for (int i = 0; i < loops.Count; i++)
                {
                    if (loops[i].Count > 1)
                    {
                        for (int j = 0; j < loops[i].Count; j++)
                        {
                            int jn = (j + 1) % loops[i].Count;
                            if (loops[i][j].curve != null && loops[i][jn].curve != null)
                            {
                                GeoPoint p1s, p1e, p2s, p2e;
                                if (loops[i][j].forward)
                                {
                                    p1s = loops[i][j].curve.StartPoint;
                                    p1e = loops[i][j].curve.EndPoint;
                                }
                                else
                                {
                                    p1s = loops[i][j].curve.EndPoint;
                                    p1e = loops[i][j].curve.StartPoint;
                                }
                                if (loops[i][jn].forward)
                                {
                                    p2s = loops[i][jn].curve.StartPoint;
                                    p2e = loops[i][jn].curve.EndPoint;
                                }
                                else
                                {
                                    p2s = loops[i][jn].curve.EndPoint;
                                    p2e = loops[i][jn].curve.StartPoint;
                                }
                                double d1 = p1e | p2s;
                                double d2 = p1e | p2e;
                                double d3 = p1s | p2s;
                                double d4 = p1s | p2e;
                                double dmin = Math.Min(Math.Min(d1, d2), Math.Min(d3, d4));
                                if (dmin == d1)
                                {
                                    // everything OK, this is what we expect
                                }
                                else if (dmin == d2)
                                {
                                    loops[i][jn].forward = !loops[i][jn].forward;
                                    orientationChanged = true;
                                }
                                else if (dmin == d3)
                                {
                                    loops[i][j].forward = !loops[i][j].forward;
                                    orientationChanged = true;
                                }
                                else
                                {   // maybe only two curves and the other connection is a little bit better: we don't need to reverse
                                    if (loops[i].Count > 2)
                                    {
                                        loops[i][j].forward = !loops[i][j].forward;
                                        loops[i][jn].forward = !loops[i][jn].forward;
                                        orientationChanged = true;
                                    }
                                }
                            }
                        }
                    }
                }
                double minCurveLength = double.MaxValue;
                for (int i = 0; i < loops.Count; i++)
                {
                    if (loops[i].Count > 1)
                    {
                        for (int j = 0; j < loops[i].Count; j++)
                        {
                            int jn = (j + 1) % loops[i].Count;
                            if (loops[i][j].curve != null && loops[i][jn].curve != null)
                                Connect3dCurves(loops[i][j].curve, loops[i][j].forward, loops[i][jn].curve, loops[i][jn].forward);
                            if (loops[i][j].curve != null) minCurveLength = Math.Min(minCurveLength, loops[i][j].curve.Length);
                        }
                    }
                    else
                    {
                        if (loops[i][0].curve != null) minCurveLength = Math.Min(minCurveLength, loops[i][0].curve.Length);
                    }
                    if (loops[i].Count == 1 && loops[i][0].curve is Ellipse && loops[i][0].curve.IsClosed && loops[i][0].vertex1 == loops[i][0].vertex2)
                    {
                        // a single circle must correspond to its vertex with its start/endpoint. 
                        // This is not always given in step files, because they don't care about start/endpoints of circles
                        Ellipse elli = loops[i][0].curve as Ellipse;
                        if ((elli.StartPoint | loops[i][0].vertex1.Position) > Precision.eps)
                        {
                            double sp = elli.PositionOf(loops[i][0].vertex1.Position);
                            elli.StartParameter = sp * Math.PI * 2.0;
                        }
                    }
                }
                // self intersecting loop. Of course a loop cannot intersect itself, but in some files they do (83855_elp11b.stp)
                // We try here to remove smaller parts
                double vprec = Math.Min(precision, minCurveLength / 10.0);
                BoundingCube vertexExtent = BoundingCube.EmptyBoundingCube;
                Set<Vertex> allVertices = new Set<Vertex>();
                for (int i = 0; i < loops.Count; i++)
                {
                    Set<Vertex> vertices = new Set<Vertex>();
                    for (int j = 0; j < loops[i].Count; j++)
                    {
                        vertexExtent.MinMax(loops[i][j].vertex1.Position);
                        vertexExtent.MinMax(loops[i][j].vertex2.Position);
                        bool duplicateFound = false;
                        foreach (Vertex vtx in vertices)
                        {
                            if (vtx != loops[i][j].vertex1 && (vtx.Position | loops[i][j].vertex1.Position) < vprec)
                            {
                                vtx.MergeWith(loops[i][j].vertex1);
                                loops[i][j].vertex1 = vtx;
                                duplicateFound = true;
                                break;
                            }
                        }
                        if (!duplicateFound) vertices.Add(loops[i][j].vertex1);
                        duplicateFound = false;
                        foreach (Vertex vtx in vertices)
                        {
                            if (vtx != loops[i][j].vertex2 && (vtx.Position | loops[i][j].vertex2.Position) < vprec)
                            {
                                vtx.MergeWith(loops[i][j].vertex2);
                                loops[i][j].vertex2 = vtx;
                                duplicateFound = true;
                                break;
                            }
                        }
                        if (!duplicateFound) vertices.Add(loops[i][j].vertex2);
                    }
                    allVertices.AddMany(vertices);
                    if (vertices.Count > loops[i].Count)
                    {
                        // there must be different vertices with the same coordinate, because the loop must always be closed (in 3d)
                    }
                    else if (vertices.Count < loops[i].Count)
                    {
                        // there must be a self intersection
                        Dictionary<Vertex, int> vertexUsage = new Dictionary<Vertex, int>(); // find the vertices used more than twice
                                                                                             // we could do this in the loop above, but this part is very rarely executed, soo we keep the first part fast
                        for (int j = 0; j < loops[i].Count; j++)
                        {
                            if (!vertexUsage.ContainsKey(loops[i][j].vertex1)) vertexUsage[loops[i][j].vertex1] = 0;
                            ++vertexUsage[loops[i][j].vertex1];
                            if (!vertexUsage.ContainsKey(loops[i][j].vertex2)) vertexUsage[loops[i][j].vertex2] = 0;
                            ++vertexUsage[loops[i][j].vertex2];
                        }
                        Set<Vertex> selfIntersections = new Set<Vertex>();
                        foreach (KeyValuePair<Vertex, int> item in vertexUsage)
                        {
                            if (item.Value > 2) selfIntersections.Add(item.Key);
                        }
                        List<List<StepEdgeDescriptor>> subloops = new List<List<StepEdgeDescriptor>>();
                        subloops.Add(loops[i]);
                        // assuming each selfIntersection vertex is only used twice
                        foreach (Vertex vtx in selfIntersections)
                        {
                            for (int k = 0; k < subloops.Count; k++)
                            {
                                int first = -1, second = -1;
                                for (int j = 0; j < subloops[k].Count; j++)
                                {
                                    if (first < 0 && ((subloops[k][j].forward && subloops[k][j].vertex1 == vtx) || (!subloops[k][j].forward && subloops[k][j].vertex2 == vtx))) first = j;
                                    if (first >= 0 && ((subloops[k][j].forward && subloops[k][j].vertex2 == vtx) || (!subloops[k][j].forward && subloops[k][j].vertex1 == vtx))) second = j;
                                    if (second >= 0) break;
                                }
                                if (first >= 0 && second >= 0) // respecting the case that the first edge in the loop is closed
                                {
                                    List<StepEdgeDescriptor> part1 = subloops[k].GetRange(first, second - first + 1);
                                    subloops[k].RemoveRange(first, second - first + 1);
                                    subloops.Add(part1);
                                    break; // the vertex <vtx> has been handled
                                }
                            }
                        }
                        for (int k = subloops.Count - 1; k >= 0; --k)
                        {
                            if (subloops[k].Count == 1 && subloops[k][0].vertex1 == subloops[k][0].vertex2 && subloops[k][0].curve.Length < vertexExtent.Size * 1e-2)
                                subloops.RemoveAt(k);
                        }
                        loops.RemoveAt(i);
                        loops.InsertRange(i, subloops);
                        // now a self intersecting loop has been split in multiple non-self-intersecting loops
                        // We should now remove those parts, which are incorrect oriented.
                    }
                    // of course there could also be a self intersection of edge curves inside the curves. This is not checked here
                    if (loops.Count > 1000) throw new ApplicationException("error in splitting loops");
                }
                for (int i = loops.Count - 1; i >= 0; --i)
                {   // there is a seam, which we don't want here
                    if (loops[i].Count == 2 && loops[i][0].curve != null && loops[i][0].curve.SameGeometry(loops[i][1].curve, precision))
                    {
                        loops.RemoveAt(i);
                    }
                }
                if (loops.Count == 0)
                {
                    return new Face[0];
                }
                // if a loop-curve has already a created edge and this edge has already a primary and a secondary face, i.e. the edge is already used by two faces
                // we must create a new edge for the new face we are going to make
                for (int i = 0; i < loops.Count; i++)
                {
                    for (int j = 0; j < loops[i].Count; j++)
                    {
                        if (loops[i][j].createdEdges != null && loops[i][j].createdEdges.Count > 0)
                        {
                            if (loops[i][j].createdEdges[0].PrimaryFace != null && loops[i][j].createdEdges[0].SecondaryFace != null)
                            {
                                loops[i][j].createdEdges.Clear();
                            }
                        }
                    }
                }

                // if a loop-curve has already created edges (i.e. this is the second usage of this loop-curve)
                // and there has more than one edge been created (a previous face was split)
                // then we split this loop curve into two loop curves to make further processing easier
                for (int i = 0; i < loops.Count; i++)
                {
                    bool needsResort = false; // maybe there is a rule to make this superfluous, but I don't know
                    for (int j = 0; j < loops[i].Count; j++)
                    {
                        if (loops[i][j].createdEdges != null && loops[i][j].createdEdges.Count > 1)
                        {
                            double vertexDist = minCurveLength / 10.0;
                            for (int k = 0; k < loops[i][j].createdEdges.Count; k++)
                            {
                                vertexDist = Math.Min(vertexDist, loops[i][j].createdEdges[k].Curve3D.Length / 10.0);
                            }
                            List<StepEdgeDescriptor> toInsert = new List<StepEdgeDescriptor>();
                            for (int k = 0; k < loops[i][j].createdEdges.Count; k++)
                            {
                                Set<Vertex> toUse = new Set<Vertex>();
                                toUse.Add(loops[i][j].vertex1);
                                toUse.Add(loops[i][j].vertex2);
                                loops[i][j].createdEdges[k].UseVertices(toUse, vertexDist);
                                StepEdgeDescriptor se = new StepEdgeDescriptor(loops[i][j].createdEdges[k], loops[i][j].forward);
                                toInsert.Add(se);
                            }
                            loops[i].RemoveAt(j);
                            loops[i].InsertRange(j, toInsert);
                            needsResort = true;
                        }
                    }
                    if (needsResort)
                    {
                        Set<StepEdgeDescriptor> loopcurves = new Set<StepEdgeDescriptor>(loops[i]);
                        loops[i].Clear();
                        StepEdgeDescriptor se = loopcurves.GetAny();
                        loopcurves.Remove(se);
                        loops[i].Add(se);
                        while (loopcurves.Count > 0)
                        {
                            Vertex lastVertex;
                            if (loops[i][loops[i].Count - 1].forward) lastVertex = loops[i][loops[i].Count - 1].vertex2;
                            else lastVertex = loops[i][loops[i].Count - 1].vertex1;
                            bool found = false;
                            foreach (StepEdgeDescriptor item in loopcurves)
                            {
                                if ((item.forward && item.vertex1 == lastVertex) || (!item.forward && item.vertex2 == lastVertex))
                                {
                                    loops[i].Add(item);
                                    loopcurves.Remove(item);
                                    found = true;
                                    break;
                                }
                            }
                            if (!found)
                            {   // it should be connected, maybe a precision issue about the vertices
                                foreach (StepEdgeDescriptor item in loopcurves)
                                {
                                    //item.createdEdges[0].UseVertices(loops[i][j].vertex1, loops[i][j].vertex2);
                                    if ((item.forward && (item.vertex1.Position | lastVertex.Position) < minCurveLength * 1e-5) || (!item.forward && (item.vertex2.Position | lastVertex.Position) < minCurveLength * 1e-5))
                                    {
                                        loops[i].Add(item);
                                        loopcurves.Remove(item);
                                        found = true;
                                        break;
                                    }
                                }
                                if (!found) return null; // loop is not connected logically
                            }
                        }
                    }
                }
                // split loops containing seams. (like in 2472g.stp)
                // A loop may contain an edge which is used twice in different directions. Remove this edge and split the loop into two loops:
                // NO!!! we don't do this, if there is a real seam, the face will be split, and splitting can deal with it
                // we only check to find the seam edges
                for (int i = loops.Count - 1; i >= 0; --i)
                {
                    for (int j = 0; j < loops[i].Count - 1; j++)
                    {
                        for (int k = j + 1; k < loops[i].Count; k++)
                        {
                            bool sameEdge = false;
                            if (loops[i][j].forward != loops[i][k].forward && loops[i][j].vertex1 == loops[i][k].vertex1 && loops[i][j].vertex2 == loops[i][k].vertex2) sameEdge = true;
                            if (loops[i][j].forward == loops[i][k].forward && loops[i][j].vertex1 == loops[i][k].vertex2 && loops[i][j].vertex2 == loops[i][k].vertex1) sameEdge = true;

                            if (sameEdge && loops[i][j].curve.SameGeometry(loops[i][k].curve, Precision.eps))
                            {
                                // two edges going in opposite directions 
                                loops[i][j].isSeam = loops[i][k].isSeam = true;
                            }
                        }
                    }
                }
                // there are loop curves (polylines as bsplines), which have a common starting and ending part. These parts seem to be seams, like in N13_MASCHIO_3D_FILO.stp
                // we try to remove these parts and substitute with trimmed curves
                for (int i = 0; i < loops.Count; ++i)
                {
                    for (int j = 0; j < loops[i].Count; j++)
                    {
                        if (loops[i][j].createdEdges == null || loops[i][j].createdEdges.Count == 0)
                        {
                            if (loops[i][j].curve is BSpline && (loops[i][j].curve as BSpline).degree == 1)
                            {
                                BSpline polygon = loops[i][j].curve as BSpline;
                                if (Precision.IsEqual(polygon.Poles[0], polygon.Poles[polygon.Poles.Length - 1]))
                                {
                                    List<GeoPoint> ppoles = new List<GeoPoint>(polygon.Poles);
                                    while (ppoles.Count > 2 && Precision.IsEqual(ppoles[0], ppoles[ppoles.Count - 1]) && Precision.IsEqual(ppoles[1], ppoles[ppoles.Count - 2]))
                                    {
                                        ppoles.RemoveAt(ppoles.Count - 1);
                                        ppoles.RemoveAt(0);
                                    }
                                    if (ppoles.Count > 1)
                                    {
                                        ppoles.RemoveAt(ppoles.Count - 1); // polyline will be flagged as closed, so we can remove last point
                                        Polyline pl = Polyline.Construct();
                                        pl.SetPoints(ppoles.ToArray(), true);
                                        loops[i][j].curve = pl;
                                        loops[i][j].vertex1 = loops[i][j].vertex2 = new Vertex(ppoles[0]);
                                    }
                                    else
                                    {   //should not happen: a polyline going forth and back
                                        loops[i][j].curve = null;
                                        loops[i][j].vertex1 = loops[i][j].vertex2 = new Vertex(ppoles[0]); // a pole (in N13_MASCHIO_3D_FILO.stp)
                                    }
                                }
                                else
                                {
                                    Polyline pl = Polyline.Construct();
                                    pl.SetPoints(polygon.Poles, false); // make polygon from bspline with degree 1
                                    loops[i][j].curve = pl;
                                }
                            }

                        }
                    }
                }

                double surfacePrecision = Math.Min(precision, vertexExtent.Size * 1e-6);
                if (!(surface is PlaneSurface))
                {   // introduced because of a conical surface with opening angle of almost 180° and curves residing in the plane between the two
                    // parts of the cone. In 2d this turns out to be difficult, because points are mapped on different parts of the cone.
                    // file "1528_Einsätze_DS-oben.stp"
                    // test before canonical
                    GeoPoint[] vtxpnts = new GeoPoint[allVertices.Count];
                    int vii = 0;
                    foreach (Vertex vtx in allVertices)
                    {
                        vtxpnts[vii] = vtx.Position;
                        ++vii;
                    }
                    Plane pln = Plane.FromPoints(vtxpnts, out double maxDist, out bool isLinear);
                    if (maxDist < surfacePrecision)
                    {
                        bool curvesAreInPlane = true;
                        for (int i = 0; i < loops.Count; i++)
                        {
                            for (int j = 0; j < loops[i].Count; j++)
                            {
                                if (loops[i][j].curve != null)
                                {
                                    if (!loops[i][j].curve.IsInPlane(pln))
                                    {
                                        curvesAreInPlane = false;
                                        break;
                                    }
                                }
                            }
                            if (!curvesAreInPlane) break;
                        }
                        if (curvesAreInPlane) surface = new PlaneSurface(pln);
                    }
                }
                ISurface canonical = surface.GetCanonicalForm(surfacePrecision);
                if (canonical is ToroidalSurface ts)
                {
                    if (ts.MinorRadius > ts.XAxis.Length * 10) canonical = null; // torus with poles are not very stable, especially when they almost resemble a sphere
                }
                if (canonical != null)
                {
                    surface = canonical; // they have different uv systems, but this doesn't matter here
                    GeoVector move = GeoVector.NullVector;
                    foreach (Vertex vtx in allVertices)
                    {
                        GeoVector diff = vtx.Position - surface.PointAt(surface.PositionOf(vtx.Position));
                        move += diff;
                    }
                    move = (1.0 / allVertices.Count) * move;
                    surface.Modify(ModOp.Translate(move));
                    move = GeoVector.NullVector;
                    foreach (Vertex vtx in allVertices)
                    {
                        GeoVector diff = vtx.Position - surface.PointAt(surface.PositionOf(vtx.Position));
                        move += diff;
                    }
                }
                if (surface is SurfaceOfLinearExtrusion sle) sle.Simplify();
                if (surface is CylindricalSurface cyl)
                {
                    double lambda = 0.0;
                    foreach (Vertex vtx in allVertices)
                    {
                        lambda += Geometry.LinePar(cyl.Location, cyl.Axis, vtx.Position);
                    }
                    lambda /= allVertices.Count;
                    // some cylinders 2d system is too far away from their points
                    if (Math.Abs(lambda) > 100) surface = new CylindricalSurface(cyl.Location + lambda * cyl.Axis, cyl.XAxis, cyl.YAxis, cyl.ZAxis);
                }
                if (Settings.GlobalSettings.GetBoolValue("StepImport.PreferNonPeriodic", false))
                {
                    if (surface.IsUPeriodic || surface.IsVPeriodic || surface.GetUSingularities().Length > 0 || surface.GetVSingularities().Length > 0)
                    {
                        List<ICurve> orientedCurves = new List<ICurve>();
                        for (int i = 0; i < loops.Count; i++)
                        {
                            for (int j = 0; j < loops[i].Count; j++)
                            {
                                if (loops[i][j].curve != null)
                                {
                                    if (loops[i][j].forward) orientedCurves.Add(loops[i][j].curve);
                                    else
                                    {
                                        ICurve clone = loops[i][j].curve.Clone();
                                        clone.Reverse();
                                        orientedCurves.Add(clone);
                                    }
                                }
                            }
                        }
                        try
                        {
                            ISurface nonperiodic = surface.GetNonPeriodicSurface(orientedCurves.ToArray());
                            if (nonperiodic != null) surface = nonperiodic;
                        }
                        catch (ApplicationException) { } // e.g. nurbs surface has two poles
                    }
                }
                if (surface is SphericalSurface && (surface as SphericalSurface).IsRealSphere)
                {
                    // typically the spherical surfaces are oriented so that the spheres axis is the z-Axis (if it was not created as a surface of revolution)
                    // it is easier and faster if the loops don't include one of the poles. This is not always possible, but we try it here:
                    SphericalSurface sphericalSurface = surface as SphericalSurface;
                    GeoPoint loc = sphericalSurface.Location;
                    int loopind = 0; // the longest loop is considered to be the outer loop, which is true in most cases
                    if (loops.Count > 1)
                    {
                        double maxlen = 0.0;
                        for (int i = 0; i < loops.Count; i++)
                        {
                            double ll = 0.0;
                            for (int j = 0; j < loops[i].Count; j++)
                            {
                                if (loops[i][j].curve != null) ll += loops[i][j].curve.Length;
                            }
                            if (ll > maxlen)
                            {
                                loopind = i;
                                maxlen = ll;
                            }
                        }
                    }
                    double radius = sphericalSurface.RadiusX;
                    bool done = false;
                    // the following idea (making the first circle in the outline to a latitude) produces bad orientations for a spherical octant
                    //for (int i = 0; i < loops[loopind].Count; i++)
                    //{
                    //    if (loops[loopind][i].curve is Ellipse elli)
                    //    {   // if the outer loop contains a circle, then orient the sphere so that the circle is a latitude
                    //        surface = new SphericalSurface(loc, radius * elli.Plane.DirectionX, radius * elli.Plane.DirectionY, radius * elli.Plane.Normal);
                    //        done = true;
                    //    }
                    //}
                    List<GeoPoint> vtxpoints = new List<GeoPoint>();
                    for (int i = 0; i < loops[loopind].Count; i++)
                    {
                        if (loops[loopind][i].curve != null)
                        {
                            if (loops[loopind][i].vertex1 != null) vtxpoints.Add(loops[loopind][i].vertex1.Position);
                        }
                    }
                    Plane pln = Plane.FromPoints(vtxpoints.ToArray(), out double maxdist, out bool islinear);
                    if (!islinear)
                    {
                        GeoVector axdir = (sphericalSurface.Axis ^ pln.Normal);
                        axdir.NormIfNotNull();
                        if (axdir.IsNullVector()) axdir = GeoVector.XAxis;
                        // if all points are in one half of the sphere, this axis should be good
                        Plane tstpln = new Plane(loc, axdir);
                        List<ICurve2D> projectedLoop = new List<ICurve2D>();
                        for (int i = 0; i < loops[loopind].Count; i++)
                        {
                            if (loops[loopind][i].curve != null)
                            {
                                ICurve2D c2d = loops[loopind][i].curve.GetProjectedCurve(tstpln);
                                if (c2d == null) continue;
                                if (!loops[loopind][i].forward) c2d.Reverse();
                                projectedLoop.Add(c2d);
                            }
                        }
                        ICurve2D[] pla = projectedLoop.ToArray();

                        if (!Border.IsPointOnOutline(pla, GeoPoint2D.Origin, precision) && Border.IsInside(pla, GeoPoint2D.Origin) != Border.CounterClockwise(pla))
                        {
                            GeoVector dirx = axdir ^ GeoVector.ZAxis;
                            GeoVector diry = dirx ^ axdir;
                            dirx.Norm();
                            diry.Norm();
                            surface = new SphericalSurface(loc, radius * dirx, radius * diry, radius * axdir);
                            done = true;
                        }
                    }
                    if (!done)
                    {
                        GeoVector axdir = GeoVector.NullVector;
                        for (int k = 0; k < 10; k++)
                        {
                            double a = ((k + 1) % Math.PI);
                            double b = Math.PI / 2.0 - ((k + 10) % Math.PI); // quasi random angles
                            axdir = new GeoVector(new Angle(a), new Angle(b));
                            Plane tstpln = new Plane(loc, axdir);
                            List<ICurve2D> projectedLoop = new List<ICurve2D>();
                            for (int i = 0; i < loops[loopind].Count; i++)
                            {
                                if (loops[loopind][i].curve != null)
                                {
                                    ICurve2D c2d = loops[loopind][i].curve.GetProjectedCurve(tstpln);
                                    if (!loops[loopind][i].forward) c2d.Reverse();
                                    projectedLoop.Add(c2d);
                                }
                            }
                            ICurve2D[] pla = projectedLoop.ToArray();

                            if (Border.IsPointOnOutline(pla, GeoPoint2D.Origin, precision)) continue;
                            if (Border.IsInside(pla, GeoPoint2D.Origin) != Border.CounterClockwise(pla))
                            {
                                break;
                            }
                        }
                        GeoVector dirx = axdir ^ GeoVector.ZAxis;
                        GeoVector diry = dirx ^ axdir;
                        dirx.Norm();
                        diry.Norm();
                        surface = new SphericalSurface(loc, radius * dirx, radius * diry, radius * axdir);
                    }
                }
                {
                    if (surface is ConicalSurface cs) cs.MakeCanonicalForm(); // remove v-offset here, before 2d curves are calculated
                }
                if (surface is CylindricalSurface || surface is ConicalSurface || surface is ToroidalSurface || surface is SurfaceOfRevolution)
                {   // avoid vertices close to 0 or 180°, because here the surface might be split and cause very short edges which might make problems
                    List<double> uvalues = new List<double>();
                    foreach (Vertex vtx in allVertices)
                    {
                        GeoPoint2D uv = surface.PositionOf(vtx.Position);
                        uvalues.Add(uv.x % Math.PI);
                    }
                    uvalues.Sort();
                    double bestu = 0.0;
                    double maxdist = 0.0;
                    for (int i = 0; i < uvalues.Count; i++)
                    {
                        double nu;
                        if (i + 1 == uvalues.Count) nu = uvalues[0] + Math.PI;
                        else nu = uvalues[i + 1];
                        double d = nu - uvalues[i];
                        if (d > maxdist)
                        {
                            maxdist = d;
                            bestu = (nu + uvalues[i]) / 2.0;
                        }
                    }
                    if (bestu != 0.0)
                    {
                        GeoVector axis = GeoVector.NullVector;
                        GeoPoint location = GeoPoint.Origin;
                        if (surface is CylindricalSurface)
                        {
                            location = (surface as CylindricalSurface).Location;
                            axis = (surface as CylindricalSurface).ZAxis;
                        }
                        if (surface is ConicalSurface)
                        {
                            location = (surface as ConicalSurface).Location;
                            axis = (surface as ConicalSurface).ZAxis;
                        }
                        if (surface is ToroidalSurface)
                        {
                            location = (surface as ToroidalSurface).Location;
                            axis = (surface as ToroidalSurface).ZAxis;
                        }
                        if (surface is SurfaceOfRevolution)
                        {
                            location = (surface as SurfaceOfRevolution).Location;
                            axis = (surface as SurfaceOfRevolution).Axis;
                        }
                        ModOp rotation = ModOp.Rotate(location, axis, new SweepAngle(bestu));
                        surface.Modify(rotation);
                    }
                }
#if DEBUG
                // Show the 3d loops for this face
                DebuggerContainer dc = new DebuggerContainer();
                for (int i = 0; i < loops.Count; i++)
                {
                    for (int j = 0; j < loops[i].Count; j++)
                    {
                        if (loops[i][j].curve != null)
                        {
                            GeoPoint ccnt;
                            if (loops[i][j].forward)
                            {
                                dc.Add(loops[i][j].curve as IGeoObject, System.Drawing.Color.Blue, i * 100 + j);
                                ccnt = loops[i][j].curve.PointAt(0.5);
                                GeoVector cdir = loops[i][j].curve.DirectionAt(0.5);
                                cdir.NormIfNotNull();
                                Line dl = Line.TwoPoints(ccnt, ccnt + loops[i][j].curve.Length * 0.05 * cdir);
                                dc.Add(dl, System.Drawing.Color.Red, i * 100 + j);
                            }
                            else
                            {
                                ICurve crv = loops[i][j].curve.Clone();
                                crv.Reverse();
                                dc.Add(crv as IGeoObject, System.Drawing.Color.Blue, i * 100 + j);
                                ccnt = crv.PointAt(0.5);
                                GeoVector cdir = crv.DirectionAt(0.5).Normalized;
                                Line dl = Line.TwoPoints(ccnt, ccnt + crv.Length * 0.05 * cdir);
                                dc.Add(dl, System.Drawing.Color.Red, i * 100 + j);
                            }
                            try
                            {
                                GeoVector ndir = surface.GetNormal(surface.PositionOf(ccnt)).Normalized;
                                Line dn = Line.TwoPoints(ccnt, ccnt + loops[i][j].curve.Length * 0.05 * ndir);
                                dc.Add(dn, System.Drawing.Color.Green, i * 100 + j);
                            }
                            catch { }
                            Point pnt = Point.Construct();
                            pnt.Symbol = PointSymbol.Cross;
                            pnt.Location = loops[i][j].vertex1.Position;
                            dc.Add(pnt, loops[i][j].vertex1.GetHashCode());
                            if (loops[i][j].vertex2 != loops[i][j].vertex1)
                            {
                                pnt = Point.Construct();
                                pnt.Symbol = PointSymbol.Cross;
                                pnt.Location = loops[i][j].vertex2.Position;
                                dc.Add(pnt, loops[i][j].vertex2.GetHashCode());
                            }
                        }
                        else
                        {
                            Point pnt = Point.Construct();
                            pnt.Symbol = PointSymbol.Plus;
                            pnt.Location = loops[i][j].vertex1.Position;
                            dc.Add(pnt, loops[i][j].vertex1.GetHashCode());
                        }
                    }
                }
                DebuggerContainer dced = new DebuggerContainer();
                for (int i = 0; i < loops.Count; i++)
                {
                    for (int j = 0; j < loops[i].Count; j++)
                    {
                        for (int k = 0; k < loops[i][j].createdEdges.Count; k++)
                        {
                            dced.Add(loops[i][j].createdEdges[k].Curve3D as IGeoObject, i * 100 + j);
                        }
                    }
                }
                DebuggerContainer dc2dx = new DebuggerContainer();
                for (int i = 0; i < loops.Count; i++)
                {
                    for (int j = 0; j < loops[i].Count; j++)
                    {
                        if (loops[i][j].curve != null)
                        {
                            ICurve2D c2d = surface.GetProjectedCurve(loops[i][j].curve, 0.0);
                            if (!loops[i][j].forward) c2d.Reverse();
                            dc2dx.Add(c2d, System.Drawing.Color.Red, i * 100 + j);
                            GeoPoint2D po;
                            if (loops[i][j].forward) po = surface.PositionOf(loops[i][j].curve.StartPoint);
                            else po = surface.PositionOf(loops[i][j].curve.EndPoint);
                            dc2dx.Add(po, System.Drawing.Color.Red, i * 100 + j);
                        }
                    }
                }
                // *** check 3d loops, raw 2d projection, already created edges
#endif
                for (int i = 0; i < loops.Count; i++)
                {
                    for (int j = 0; j < loops[i].Count; j++)
                    {
                        if (loops[i][j].createdEdges.Count > 1)
                        {   // this edge has been split
                            Vertex sv = null;
                            if (loops[i][j].forward) sv = loops[i][j].vertex1;
                            else sv = loops[i][j].vertex2;
                            if (loops[i][j].createdEdges[0].EndVertex(loops[i][j].createdEdges[0].PrimaryFace) == sv)
                            {
                                // nothing to do, createdEdges is in correct order
                            }
                            else
                            {
                                loops[i][j].createdEdges.Reverse();
                            }
                        }
                    }
                }
                // Poles (typically with sphere, cone and sometimes nurbs) lead to missing 2d connections.
                // these edges are inserted here with no 3d curve but valid vertices
                double[] us = surface.GetUSingularities();
                double[] vs = surface.GetVSingularities();
                // the following three lists are synchronous
                List<GeoPoint> poles = new List<GeoPoint>();
                List<GeoPoint2D> poles2d = new List<GeoPoint2D>();
                List<bool> poleIsInU = new List<bool>();
                for (int i = 0; i < us.Length; i++)
                {
                    poles.Add(surface.PointAt(new GeoPoint2D(us[i], 0.0)));
                    poles2d.Add(new GeoPoint2D(us[i], 0.0));
                    poleIsInU.Add(false);
                }
                for (int i = 0; i < vs.Length; i++)
                {
                    poles.Add(surface.PointAt(new GeoPoint2D(0.0, vs[i])));
                    poles2d.Add(new GeoPoint2D(0.0, vs[i]));
                    poleIsInU.Add(true);
                }
                //for (int i = 0; i < poles.Count; i++)
                //{
                //    foreach (Vertex vtx in allVertices)
                //    {
                //        if ((vtx.Position|poles[i])<precision)
                //        {

                //        }
                //    }
                //}
                List<Pair<StepEdgeDescriptor, List<StepEdgeDescriptor>>> splittedByPoles = new List<Pair<StepEdgeDescriptor, List<StepEdgeDescriptor>>>();
                if (poles.Count > 0)
                {
                    // check whether a loop-curve goes through a pole. In this case we have to split the loop-curve into parts in order to be able to insert 
                    // the loop-pole-curve (which is a point in 3d and a line in 2d) in between these two parts
                    // not implemented yet: the loop edge already has been splitted (multiple createdEdges)!!!
                    for (int i = 0; i < loops.Count; i++)
                    {
                        Dictionary<Vertex, int> vertexToPole = new Dictionary<Vertex, int>();
                        for (int j = loops[i].Count - 1; j >= 0; --j) // backward loop, because maybe an entry will be splitted
                        {
                            if (loops[i][j].curve != null)
                            {
                                for (int k = 0; k < poles.Count; k++)
                                {
                                    if (loops[i][j].curve.DistanceTo(poles[k]) < Precision.eps)
                                    {
                                        if (Precision.IsEqual(loops[i][j].vertex1.Position, poles[k]) || Precision.IsEqual(loops[i][j].vertex2.Position, poles[k])) continue;
                                        double pos = loops[i][j].curve.PositionOf(poles[k]);
                                        if (pos >= 1 - 1e-3 || pos <= 1e-3) continue; // this is difficult, don't make short parts with identical start- and end vertex
                                        ICurve[] parts = loops[i][j].curve.Split(pos);
                                        StepEdgeDescriptor se = loops[i][j];
                                        loops[i].RemoveAt(j);
                                        List<StepEdgeDescriptor> toInsert = new List<StepEdgeDescriptor>();
                                        Vertex lastEnd = null;
                                        for (int l = 0; l < parts.Length; l++)
                                        {
                                            Vertex sv;
                                            if (lastEnd != null) sv = lastEnd;
                                            else sv = se.vertex1;
                                            Vertex ev;
                                            if (l < parts.Length - 1) ev = new Vertex(parts[l].EndPoint);
                                            else ev = se.vertex2;
                                            lastEnd = ev;
                                            StepEdgeDescriptor sedpart = new StepEdgeDescriptor(parts[l], sv, ev, se.forward);
                                            toInsert.Add(sedpart);
                                        }
                                        if (!se.forward) toInsert.Reverse();
                                        loops[i].InsertRange(j, toInsert);
                                        splittedByPoles.Add(new Pair<StepEdgeDescriptor, List<StepEdgeDescriptor>>(se, toInsert));
                                    }
                                }
                            }
                        }
                        for (int j = 0; j < loops[i].Count; j++)
                        {
                            for (int k = 0; k < poles.Count; k++)
                            {
                                if ((loops[i][j].vertex1.Position | poles[k]) < vprec) vertexToPole[loops[i][j].vertex1] = k; // changed to vprec, because in 06_PN_4648_S_1185_1_I15_A13_AS_P100-1.stp, face 12018 two vertices are found
                                if ((loops[i][j].vertex2.Position | poles[k]) < vprec) vertexToPole[loops[i][j].vertex2] = k;
                            }
                        }
                        if (vertexToPole.Count > 1)
                        {   // very rare case: two different vertices for the same pole
                            List<Vertex> keys = new List<Vertex>(vertexToPole.Keys);
                            for (int vi = 0; vi < keys.Count - 1; vi++)
                            {
                                for (int vj = vi + 1; vj < keys.Count; vj++)
                                {
                                    if (vertexToPole.ContainsKey(keys[vi]) && vertexToPole.ContainsKey(keys[vj]) && vertexToPole[keys[vi]] == vertexToPole[keys[vj]])
                                    {   // different vertices, same pole: replace vertex item2.Key by item1.Key
                                        keys[vi].MergeWith(keys[vj]); // updates all existing edges, but not the parameter loops
                                        for (int j = 0; j < loops[i].Count; j++)
                                        {
                                            if (loops[i][j].vertex1 == keys[vj]) loops[i][j].vertex1 = keys[vi];
                                            if (loops[i][j].vertex2 == keys[vj]) loops[i][j].vertex2 = keys[vi];
                                        }
                                        vertexToPole.Remove(keys[vj]);
                                    }
                                }
                            }
                        }
                        if (vertexToPole.Count > 0)
                        {
                            bool poleInserted = false;
                            for (int j = 0; j < loops[i].Count; j++)
                            {
                                int j1 = (j + 1) % loops[i].Count;
                                if (j != j1)
                                {
                                    Vertex endVertex;
                                    if (loops[i][j].forward) endVertex = loops[i][j].vertex2;
                                    else endVertex = loops[i][j].vertex1;
                                    if (vertexToPole.ContainsKey(endVertex) && ((endVertex == loops[i][j1].vertex1) || (endVertex == loops[i][j1].vertex2)))
                                    {   // insert a pole between j and j+1, 2d curve will be created later
                                        loops[i].Insert(j1, new StepEdgeDescriptor((ICurve)null, endVertex, endVertex, false));
                                        poleInserted = true;
                                        ++j;
                                    }
                                }
                            }
                            if (poleInserted)
                            {
                                // permute cyclicaly so that the list starts with a pole followed by the longest path without a pole.
                                // This is only important for the domain calculation
                                List<int> poleIndices = new List<int>();
                                for (int j = 0; j < loops[i].Count; j++) if (loops[i][j].curve == null) poleIndices.Add(j);
                                int bestPoleIndex = poleIndices[poleIndices.Count - 1];
                                int maxSpan = loops[i].Count - bestPoleIndex + poleIndices[0] - 1;
                                for (int j = 0; j < poleIndices.Count - 1; j++)
                                {
                                    int span = poleIndices[j + 1] - poleIndices[j] - 1;
                                    if (span > maxSpan)
                                    {
                                        maxSpan = span;
                                        bestPoleIndex = poleIndices[j];
                                    }
                                }
                                if (bestPoleIndex != 0)
                                {   // rotate the list so it begins with a pole (followed by a maximum number of non-poles
                                    List<StepEdgeDescriptor> rotated = new List<StepEdgeDescriptor>(loops[i].Count);
                                    for (int j = 0; j < loops[i].Count; j++)
                                    {
                                        rotated.Add(loops[i][(j + bestPoleIndex) % loops[i].Count]);
                                    }
                                    loops[i] = rotated;
                                }
                            }

                        }
                    }
                }

                // STEP allows faces to have periodic surfaces, which are bounded by two outer loops, like a piece of a cylinder, bounded by two circles or ellipses.
                // In CADability, each face must have a single outer bound in u/v space (and any number of holes). Also we do not like "seam"-edges, i.e. edges,
                // which are used twice in both directions on a single face. (Although it is still allowed and causes much debugging effort)
                // create the 2d projections of the edges on the surface.
                double uperiod = 0.0, vperiod = 0.0;
                if (surface.IsUPeriodic) uperiod = surface.UPeriod;
                if (surface.IsVPeriodic) vperiod = surface.VPeriod;
#if DEBUG
                // *** check correct 2d projection
                DebuggerContainer dccrv2d = new DebuggerContainer();
#endif
                // in case of periodic surfaces, we need the curves to be as close together as possible
                // so we start with the first curve to set a domain for the periodic space
                BoundingRect[] loopExt = new BoundingRect[loops.Count];
                for (int i = 0; i < loops.Count; i++)
                {
                    loopExt[i] = BoundingRect.EmptyBoundingRect;
                    GeoPoint2D lastEndPoint = GeoPoint2D.Invalid;
                    for (int j = 0; j < loops[i].Count; j++)
                    {
                        if (loops[i][j].curve != null)
                        {
                            double len = loops[i][j].curve.Length;
                            ICurve2D crv2d;
                            //if (len < Precision.eps * 100) // changed to surfacePrecision
                            if (len < surfacePrecision * 100)
                            {
                                GeoPoint2D sp = surface.PositionOf(loops[i][j].curve.StartPoint);
                                GeoPoint2D ep = surface.PositionOf(loops[i][j].curve.EndPoint);
                                if (!lastEndPoint.IsValid)
                                {
                                    lastEndPoint = sp;
                                }
                                SurfaceHelper.AdjustPeriodicStartPoint(surface, lastEndPoint, ref sp);
                                SurfaceHelper.AdjustPeriodicStartPoint(surface, lastEndPoint, ref ep);
                                crv2d = new Line2D(sp, ep);
                            }
                            else crv2d = surface.GetProjectedCurve(loops[i][j].curve, surfacePrecision);
                            if (!loops[i][j].forward) crv2d.Reverse();
                            if (!lastEndPoint.IsValid)
                            {
                                if (i != 0) SurfaceHelper.AdjustPeriodic(surface, loopExt[0], crv2d); // else crv2d is the first 2d curve of this loops and determins the domain
                            }
                            else
                            {
                                SurfaceHelper.AdjustPeriodicStartPoint(surface, lastEndPoint, crv2d);
                            }
                            if (crv2d != null) lastEndPoint = crv2d.EndPoint;
#if DEBUG
                            dccrv2d.Add(crv2d, System.Drawing.Color.Red, i * 100 + j);
#endif
                            crv2d.UserData["EdgeDescriptor"] = loops[i][j]; // if we need to split, we need this information
                            loops[i][j].curve2d = crv2d;
                            loopExt[i].MinMax(crv2d.GetExtent());
                        }
                    }
                }
                BoundingRect ext = BoundingRect.EmptyBoundingRect;
                for (int i = 0; i < loops.Count; i++)
                {
                    ext.MinMax(loopExt[i]);
                }
                if (ext.IsEmpty())
                {   // rare case, only poles, e.g. a full sphere
                    surface.GetNaturalBounds(out ext.Left, out ext.Right, out ext.Bottom, out ext.Top);
                }
                bool removeEmptyLoops = false;
                for (int i = 0; i < loops.Count; i++)
                {
                    for (int j = 0; j < loops[i].Count; j++)
                    {
                        if (loops[i][j].curve == null)
                        {
                            // probably a pole
                            if (loops[i][j].vertex1 == loops[i][j].vertex2 && poles.Count > 0)
                            {
                                int poleIndex = -1;
                                double mindist = double.MaxValue;
                                for (int k = 0; k < poles.Count; k++)
                                {
                                    double d = poles[k] | loops[i][j].vertex1.Position;
                                    if (d < mindist)
                                    {
                                        mindist = d;
                                        poleIndex = k;
                                    }
                                }
                                if (poleIndex >= 0)
                                {
                                    // if it is a vertex loop, like the tip of a cone, then this is the only edge, and you dont't know the orientation
                                    if (poleIsInU[poleIndex])
                                    {
                                        loops[i][j].curve2d = new Line2D(new GeoPoint2D(ext.Left, poles2d[poleIndex].y), new GeoPoint2D(ext.Right, poles2d[poleIndex].y));
                                    }
                                    else
                                    {
                                        loops[i][j].curve2d = new Line2D(new GeoPoint2D(poles2d[poleIndex].x, ext.Bottom), new GeoPoint2D(poles2d[poleIndex].x, ext.Top));
                                    }
                                    int j0 = (j + loops[i].Count - 1) % loops[i].Count;
                                    int j1 = (j + 1) % loops[i].Count;
                                    GeoPoint2D lastEndPoint = loops[i][j0].curve2d.EndPoint;
                                    GeoPoint2D nextStartPoint = loops[i][j1].curve2d.StartPoint;
                                    if ((loops[i][j].curve2d.EndPoint | lastEndPoint) + (loops[i][j].curve2d.StartPoint | nextStartPoint) <
                                        (loops[i][j].curve2d.StartPoint | lastEndPoint) + (loops[i][j].curve2d.EndPoint | nextStartPoint)) loops[i][j].curve2d.Reverse();
#if DEBUG
                                    dccrv2d.Add(loops[i][j].curve2d, System.Drawing.Color.Green, i * 100 + j);
#endif
                                    ext.MinMax(loops[i][j].curve2d.GetExtent());
                                }
                            }
                            if (loops[i][j].curve2d == null)
                            {
                                loops[i][j] = null;
                                removeEmptyLoops = true;
                            }
                        }
                    }
                }
                if (removeEmptyLoops)
                {   // Sometimes there are loops with only empty curves. Typically a pole
                    for (int i = loops.Count - 1; i >= 0; --i)
                    {
                        if (loops[i].Count == 1 && loops[i][0] == null)
                        {
                            loops.RemoveAt(i);
                        } // no other case encountered yet (one of more edges is null)
                    }
                }
                // *** check correct 2d projection and poles
                int outerLoop = 0;
                List<int> openLoops = new List<int>();
                for (int i = 0; i < loops.Count; i++)
                {
                    if (i > 0 && loopExt[i].Size > loopExt[outerLoop].Size) outerLoop = i; // OK, there could be bounds with the same size, but do you have an example?
                    if (!Precision.IsEqual(loops[i][0].curve2d.StartPoint, loops[i][loops[i].Count - 1].curve2d.EndPoint))
                    {   // a loop might be open, such as the top and bottom of a cylinder, which has two outer loops
                        if ((surface.IsUPeriodic && Math.Abs(loops[i][0].curve2d.StartPoint.x - loops[i][loops[i].Count - 1].curve2d.EndPoint.x) > surface.UPeriod / 2) ||
                            (surface.IsVPeriodic && Math.Abs(loops[i][0].curve2d.StartPoint.y - loops[i][loops[i].Count - 1].curve2d.EndPoint.y) > surface.VPeriod / 2))
                        {
                            openLoops.Add(i); // open loops are always outer loop
                        }
                    }
                }
                if (openLoops.Count > 0) outerLoop = -1; // none of the closed loops is an outer loop, we have one ore more open loops
                bool needsToSplit = false;
                List<int> loopsToRemove = new List<int>();
                // in most step files, the loops are oriented correctly, but in some (e.g. 2472g.stp) we have to repair
                // the loop of the outerLoop must be counterclockwise, the others must be clockwise
                int reverseCount = 0;
                List<int> loopsToreverse = new List<int>(); // this is a repair machanism. In correct step files there should be no loops the have to be reversed
                double sumArea = 0.0;
                for (int i = loops.Count - 1; i >= 0; --i)
                {
                    if (openLoops.Contains(i)) continue;
                    ICurve2D[] crvs = new ICurve2D[loops[i].Count];
                    for (int j = 0; j < crvs.Length; j++)
                    {
                        crvs[j] = loops[i][j].curve2d;
                    }
                    double area = Border.SignedArea(crvs);
#if DEBUG
                    bool ccwdbg = Border.CounterClockwise(crvs);
                    if (ccwdbg != (area > 0))
                    {

                    }
#endif
                    bool closed = true;
                    if (loops[i][0].curve != null && loops[i][loops[i].Count - 1].curve != null)
                    {
                        GeoPoint sp, ep;
                        if (loops[i][0].forward) sp = loops[i][0].curve.StartPoint;
                        else sp = loops[i][0].curve.EndPoint;
                        int last = loops[i].Count - 1;
                        if (loops[i][last].forward) ep = loops[i][last].curve.EndPoint;
                        else ep = loops[i][last].curve.StartPoint;
                        // I am not sure, why we would want to remove those open loops.
                        // they are closed on the vertices, so maybe the 3d curves are just imprecise (as in 011cor.stp)
                        // if ((sp | ep) > precision * 1000) closed = false; // added *100 for item id 10484 in 06_PN_4648_S_1185_1_I15_A13_AS_P100-1.stp
                    }
                    if (Math.Abs(area) < (ext.Width + ext.Height) * 1e-8 || !closed)
                    {   // probably a seam: two curves going forth and back on the same path
                        loopsToRemove.Add(i);
                    }
                    else
                    {
                        sumArea += Math.Abs(area);
                        bool ccw = area > 0.0;
                        if (Math.Abs(area) < (ext.Width + ext.Height) * 1e-4 && ((i == outerLoop && !ccw) || (i != outerLoop && ccw)))
                        {   // very slim shape for the area, step requires outer loops to be ccw and inner loops to be cw, 
                            // so if we should really revers a loop we better double check
                            ccw = Border.CounterClockwise(crvs);
                        }
                        if ((i == outerLoop && !ccw) || (i != outerLoop && ccw))
                        {
                            loopsToreverse.Add(i);
                            ++reverseCount;
                        }
                    }
                }
                if (openLoops.Count == 0 && sumArea < (ext.Width + ext.Height) * 1e-8) return null; // empty face
                if (loops.Count == 1 && openLoops.Count == 1)
                {
                    // only a single open loop (like the equator on the sphere): there must be a pole which belongs to the surface
                    // we must assume that the surface and loops are correct oriented, otherwise it is not possible to decide which pole to use
                    int poleIndex = -1;
                    for (int i = 0; i < poles2d.Count; i++)
                    {
                        double area = 0.0;
                        for (int j = 0; j < loops[0].Count; j++)
                        {
                            area += loops[0][j].curve2d.GetAreaFromPoint(poles2d[i]);
                        }
                        if (area > 0) poleIndex = i;
                    }
                    if (poleIndex < 0 && poles2d.Count == 1)
                    {   // e.g. conical, we reverse the loop and use the only pole
                        for (int j = 0; j < loops[0].Count; j++)
                        {
                            loops[0][j].forward = !loops[0][j].forward;
                            loops[0][j].curve2d.Reverse();
                        }
                        loops[0].Reverse();
                        poleIndex = 0;
                    }
                    if (poleIndex >= 0)
                    {
                        Vertex vtxpole = new Vertex(poles[poleIndex]);
                        StepEdgeDescriptor se = new StepEdgeDescriptor((ICurve)null, vtxpole, vtxpole, true);
                        List<StepEdgeDescriptor> loop = new List<StepEdgeDescriptor>();
                        loop.Add(se);
                        loops.Add(loop);
                        if (poleIsInU[poleIndex])
                        {
                            se.curve2d = new Line2D(new GeoPoint2D(ext.Left, poles2d[poleIndex].y), new GeoPoint2D(ext.Right, poles2d[poleIndex].y));
                        }
                        else
                        {
                            se.curve2d = new Line2D(new GeoPoint2D(poles2d[poleIndex].x, ext.Bottom), new GeoPoint2D(poles2d[poleIndex].x, ext.Top));
                        }
                        SurfaceHelper.AdjustPeriodic(surface, ext, se.curve2d);
                        if (se.curve2d.GetAreaFromPoint(loops[0][0].curve2d.StartPoint) < 0.0) se.curve2d.Reverse();
#if DEBUG
                        dccrv2d.Add(se.curve2d, System.Drawing.Color.Green, 1);
#endif
                        ext.MinMax(se.curve2d.GetExtent());
                        openLoops.Add(1);

                    }
                }
                GeoPoint2D cnt = ext.GetCenter();
                if (openLoops.Count > 0)
                {   // open loops limit parts of closed surfaces. E.g. two circles bound a segment of a cylinder
                    // the orientation of these curves is sometimes wrong, so we have to test
                    // there must be exactly two loops
                    if (openLoops.Count == 2)
                    {
                        int ii = openLoops[0];
                        int oppii = openLoops[1]; // "oppii" is the other open loop one
                                                  // calculating the area is a bad idea
                                                  // file 0816.5.001.stp with step index 160309 is a example, where the area doesn't work
                        if (Geometry.InnerIntersection(loops[ii][loops[ii].Count - 1].curve2d.EndPoint, loops[oppii][0].curve2d.StartPoint, loops[oppii][loops[oppii].Count - 1].curve2d.EndPoint, loops[ii][0].curve2d.StartPoint))
                        {
                            for (int i = 0; i < loops.Count; i++)
                            {
                                if (loops[i].Count == 1 && loops[i][0].curve == null) // a pole from a point, reverse the curve2d
                                {
                                    loops[i][0].curve2d.Reverse();
                                    break;
                                }
                            }
                        }
                        bool reversedii = false;
                        if (Geometry.InnerIntersection(loops[ii][loops[ii].Count - 1].curve2d.EndPoint, loops[oppii][0].curve2d.StartPoint, loops[oppii][loops[oppii].Count - 1].curve2d.EndPoint, loops[ii][0].curve2d.StartPoint))
                        {   // still self-intersecting
                            loops[ii].Reverse();
                            reversedii = true;
                            for (int i = 0; i < loops[ii].Count; i++)
                            {
                                loops[ii][i].forward = !loops[ii][i].forward;
                                loops[ii][i].curve2d.Reverse();
                            }
                        }
                        Line2D l1 = new Line2D(loops[ii][loops[ii].Count - 1].curve2d.EndPoint, loops[oppii][0].curve2d.StartPoint);
                        Line2D l2 = new Line2D(loops[oppii][loops[oppii].Count - 1].curve2d.EndPoint, loops[ii][0].curve2d.StartPoint);
                        List<ICurve2D> testDirections = new List<ICurve2D>();
                        for (int i = 0; i < loops[ii].Count; i++) testDirections.Add(loops[ii][i].curve2d);
                        testDirections.Add(l1);
                        for (int i = 0; i < loops[oppii].Count; i++) testDirections.Add(loops[oppii][i].curve2d);
                        testDirections.Add(l2);

                        double area = Border.SignedArea(testDirections); // test, whether the open loops together with the connecting seams build a ccw outline (which they should)
                        if (area < 0.0) // was:" && reversedii)", in Hembra.stp, face 18940 the orientation of both loops is simply wrong, but we didn't reverse ii, so && reversedii has been removed
                        {   // ii has been reversed above, we also could have reversed oppii, it was just a guess, in this case, the guess was wrong, we reverse both loops
                            loops[ii].Reverse();
                            for (int i = 0; i < loops[ii].Count; i++)
                            {
                                loops[ii][i].forward = !loops[ii][i].forward;
                                loops[ii][i].curve2d.Reverse();
                            }
                            loops[oppii].Reverse();
                            for (int i = 0; i < loops[oppii].Count; i++)
                            {
                                loops[oppii][i].forward = !loops[oppii][i].forward;
                                loops[oppii][i].curve2d.Reverse();
                            }
                            // recalc the test loop
                            l1 = new Line2D(loops[ii][loops[ii].Count - 1].curve2d.EndPoint, loops[oppii][0].curve2d.StartPoint);
                            l2 = new Line2D(loops[oppii][loops[oppii].Count - 1].curve2d.EndPoint, loops[ii][0].curve2d.StartPoint);
                            testDirections = new List<ICurve2D>();
                            for (int i = 0; i < loops[ii].Count; i++) testDirections.Add(loops[ii][i].curve2d);
                            testDirections.Add(l1);
                            for (int i = 0; i < loops[oppii].Count; i++) testDirections.Add(loops[oppii][i].curve2d);
                            testDirections.Add(l2);
                            area = Border.SignedArea(testDirections);
                        }
                        if (area < 0.0)
                        {
                            if (surface.IsUPeriodic && surface.IsVPeriodic)
                            {
                                // when on a torus (or similar surface) and the face is bound by two open curves (in 2d, closed curves in 3d) then it is not clear, which part of the torus is used
                                GeoVector2D md = loops[ii][loops[ii].Count - 1].curve2d.EndPoint - loops[ii][0].curve2d.StartPoint;
                                if (Math.Abs(md.x) > Math.Abs(md.y))
                                {   // the open loop is in u direction, change the v-domain of the second loop
                                    double dv;
                                    if (loops[ii][0].curve2d.StartPoint.y < loops[oppii][0].curve2d.StartPoint.y) dv = -surface.VPeriod;
                                    else dv = surface.VPeriod;
                                    for (int i = 0; i < loops[oppii].Count; i++)
                                    {
                                        loops[oppii][i].curve2d.Move(0, dv);
                                    }
                                }
                                else
                                {
                                    double du;
                                    if (loops[ii][0].curve2d.StartPoint.x < loops[oppii][0].curve2d.StartPoint.x) du = -surface.UPeriod;
                                    else du = surface.UPeriod;
                                    for (int i = 0; i < loops[oppii].Count; i++)
                                    {
                                        loops[oppii][i].curve2d.Move(du, 0);
                                    }
                                }
                            }
                            else if (surface.IsVPeriodic && Math.Abs(l1.StartDirection.x) < Math.Abs(l1.StartDirection.y))
                            {
                                double dv;
                                if (loops[ii][0].curve2d.StartPoint.y < loops[oppii][0].curve2d.StartPoint.y) dv = -surface.VPeriod;
                                else dv = surface.VPeriod;
                                for (int i = 0; i < loops[oppii].Count; i++)
                                {
                                    loops[oppii][i].curve2d.Move(0, dv);
                                }
                            }
                            else if (surface.IsUPeriodic && Math.Abs(l1.StartDirection.x) > Math.Abs(l1.StartDirection.y))
                            {
                                double du;
                                if (loops[ii][0].curve2d.StartPoint.x < loops[oppii][0].curve2d.StartPoint.x) du = -surface.UPeriod;
                                else du = surface.UPeriod;
                                for (int i = 0; i < loops[oppii].Count; i++)
                                {
                                    loops[oppii][i].curve2d.Move(du, 0);
                                }
                            }
                            else
                            {   // this happens with _L.001.050_4.stp: the inner hole item 3327 a wrong oriented cylinder
                                loopsToreverse.Add(ii);
                                loopsToreverse.Add(oppii);
                                reverseCount += 2;
                            }
                            ext = BoundingRect.EmptyBoundingRect; // recalculate the extent since 2d curves have been moved
                            loopExt = new BoundingRect[loops.Count]; // maybe loops.Count has changed
                            for (int i = 0; i < loops.Count; i++)
                            {
                                loopExt[i] = BoundingRect.EmptyBoundingRect;
                                for (int j = 0; j < loops[i].Count; j++)
                                {
                                    loopExt[i].MinMax(loops[i][j].curve2d.GetExtent());
                                }
                                ext.MinMax(loopExt[i]);
                            }
                        }
                        if (Geometry.IntersectLLparInside(loops[ii][loops[ii].Count - 1].curve2d.EndPoint, loops[oppii][0].curve2d.StartPoint, loops[oppii][loops[oppii].Count - 1].curve2d.EndPoint, loops[ii][0].curve2d.StartPoint, out double pos1, out double pos2))
                        {
                            if (loopsToreverse.Contains(ii)) loopsToreverse.Remove(ii);
                            else loopsToreverse.Add(ii);
                            reverseCount++;
                        }
                        else
                        {
                        }
                    }
                    else
                    {
                        return null; // no way to deal with more than 2 open loops
                    }
                    //for (int i = 0; i < openLoops.Count; i++)
                    //{
                    //    int ii = openLoops[i];
                    //    int oppii = (ii + 1) % loops.Count; // there must be more than one loop and oppii is another one
                    //    // calculate the area of this loop in 2d with the center of the extent
                    //    // The center isn't necessary a good choice, the center of the opposite side of ext would be better, but more work to find this side
                    //    cnt = loops[oppii][loops[oppii].Count / 2].curve2d.PointAt(0.5);
                    //    double area = 0.0;
                    //    for (int j = 0; j < loops[ii].Count; j++)
                    //    {
                    //        area += loops[ii][j].curve2d.GetAreaFromPoint(cnt);
                    //    }
                    //    if (area < 0)
                    //    {
                    //        if (loops[ii].Count > 1 || loops[ii][0].curve != null) // otherwise it is a vertex loop with yet unknown orientation
                    //            ++reverseCount;
                    //        //loops[ii].Reverse();
                    //        //for (int j = 0; j < loops[ii].Count; j++)
                    //        //{
                    //        //    loops[ii][j].curve2d.Reverse();
                    //        //    loops[ii][j].forward = !loops[i][j].forward;
                    //        //}
                    //    }
                    //}
                }
                if (reverseCount > 0)
                {
                    if (loops.Count == reverseCount)
                    {
                        // all loops are reversed, we reverse the surface
                        ModOp2D mreverse = surface.ReverseOrientation();
#if DEBUG
                        //GeoObjectList dbggrd = (surface as ISurfaceImpl).DebugGrid;
#endif
                        ext = BoundingRect.EmptyBoundingRect;
                        for (int i = 0; i < loops.Count; i++)
                        {
                            loopExt[i] = BoundingRect.EmptyBoundingRect;
                            for (int j = 0; j < loops[i].Count; j++)
                            {
                                if (loops[i][j].curve2d is ProjectedCurve pc)
                                {
                                    loops[i][j].curve2d = surface.GetProjectedCurve(loops[i][j].curve, surfacePrecision);
                                    if (!loops[i][j].forward) loops[i][j].curve2d.Reverse();
                                }
                                else
                                {
                                    loops[i][j].curve2d = loops[i][j].curve2d.GetModified(mreverse);
                                }
                                loopExt[i].MinMax(loops[i][j].curve2d.GetExtent());
                            }
                            ext.MinMax(loopExt[i]);
                            // loops[i].Reverse(); we do not need to reverse, because the curve2ds are reflected (mirrored)
                        }
                    }
                    else
                    {
                        // reverse only some loops: here we leave the surface unchanged and only reverse those loops
                        for (int i = 0; i < loops.Count; i++)
                        {
                            if (loopsToreverse.Contains(i))
                            {
                                for (int j = 0; j < loops[i].Count; j++)
                                {
                                    loops[i][j].forward = !loops[i][j].forward;
                                    loops[i][j].curve2d.Reverse();
                                }
                                loops[i].Reverse();
                            }
                            // loops[i].Reverse(); we do not need to reverse, because the curve2ds are reflected (mirrored)
                        }
                    }
                }
                // Orient vertex loops: a vertex loop consists of a single line over the period and it's orientation is unknown when created
                cnt = ext.GetCenter();
                for (int i = 0; i < loops.Count; i++)
                {
                    if (loops[i].Count == 1 && loops[i][0].curve == null)
                    {   // a pole across the whole domain
                        double area = loops[i][0].curve2d.GetAreaFromPoint(cnt);
                        if (area < 0) loops[i][0].curve2d.Reverse();
                    }
                }
                if (uperiod > 0 && ext.Width >= uperiod * 0.999999) needsToSplit = true;
                if (vperiod > 0 && ext.Height >= vperiod * 0.999999) needsToSplit = true;
                for (int i = 0; i < loopsToRemove.Count; i++)
                {
                    loops.RemoveAt(loopsToRemove[i]); // loopsToRemove is sorted from back to front
                }
                if (!needsToSplit)
                {   // this is the eays case, where there is a single outline and 0 or more holes and the face is not periodically closed (wrapped around)
                    // simply create the necessary edges
                    Face fc = Face.Construct();
                    Edge[][] edges = new Edge[loops.Count][];

                    for (int i = 0; i < loops.Count; i++)
                    {
                        List<Edge> createdEdges = new List<Edge>();
                        // edges might be used more than twice in step files. It is difficult then to create proper shells. 
                        // here the third usage of an edge simply creates a new edge.
                        for (int j = 0; j < loops[i].Count; j++)
                        {
                            if (loops[i][j].createdEdges.Count == 0)
                            {
                                Edge created = new Edge(fc, loops[i][j].curve);
                                created.UseVerticesForce(loops[i][j].vertex1, loops[i][j].vertex2);
                                if (loops[i][j].curve2d != null)
                                {
                                    created.SetPrimary(fc, loops[i][j].curve2d, loops[i][j].forward);
                                }
                                loops[i][j].createdEdges.Add(created);
                                createdEdges.Add(created);
                            }
                            else
                            {
                                if (loops[i][j].createdEdges.Count > 1)
                                {
                                    // maybe the edges are in the wrong order
                                    bool reverse = false;
                                    if (loops[i][j].forward)
                                    {
                                        if (loops[i][j].vertex1 != loops[i][j].createdEdges[0].Vertex1 && loops[i][j].vertex1 != loops[i][j].createdEdges[0].Vertex2) reverse = true;
                                    }
                                    else
                                    {
                                        if (loops[i][j].vertex2 != loops[i][j].createdEdges[0].Vertex1 && loops[i][j].vertex2 != loops[i][j].createdEdges[0].Vertex2) reverse = true;
                                    }
                                    if (reverse) loops[i][j].createdEdges.Reverse();
#if DEBUG
                                    bool ok = true;
                                    if (loops[i][j].forward)
                                    {
                                        if (loops[i][j].vertex2 != loops[i][j].createdEdges[loops[i][j].createdEdges.Count - 1].Vertex1
                                            && loops[i][j].vertex2 != loops[i][j].createdEdges[loops[i][j].createdEdges.Count - 1].Vertex2) ok = false;
                                    }
                                    else
                                    {
                                        if (loops[i][j].vertex1 != loops[i][j].createdEdges[loops[i][j].createdEdges.Count - 1].Vertex1
                                            && loops[i][j].vertex1 != loops[i][j].createdEdges[loops[i][j].createdEdges.Count - 1].Vertex2) ok = false;
                                    }
                                    // System.Diagnostics.Debug.Assert(OK); this may happen and is valid
#endif
                                }
                                for (int k = 0; k < loops[i][j].createdEdges.Count; k++)
                                {   // in most cases this is only a single edge, unless it has been splitted before
                                    // the curve is never null here if count >1
                                    if (loops[i][j].createdEdges[k].Curve3D != null && loops[i][j].createdEdges[k].SecondaryFace == null)
                                    {
                                        ICurve2D crv2d = surface.GetProjectedCurve(loops[i][j].createdEdges[k].Curve3D, 0.0);
                                        if (!loops[i][j].forward) crv2d.Reverse();
                                        SurfaceHelper.AdjustPeriodic(surface, ext, crv2d);
                                        if (loops[i][j].createdEdges.Count == 1) crv2d = loops[i][j].curve2d; // already claculated
                                        loops[i][j].createdEdges[k].SetSecondary(fc, crv2d, loops[i][j].forward);
                                        createdEdges.Add(loops[i][j].createdEdges[k]);
                                    }
                                    else
                                    {   // make a new edge
                                        Edge created = new Edge(fc, loops[i][j].curve);
                                        created.UseVerticesForce(loops[i][j].vertex1, loops[i][j].vertex2);
                                        if (loops[i][j].curve != null)
                                        {
                                            created.SetPrimary(fc, loops[i][j].curve2d, loops[i][j].forward);
                                        }
                                        loops[i][j].createdEdges.Add(created);
                                        createdEdges.Add(created);
                                    }
                                }
                            }
                        }
                        // add the missing poles, which connect the ends of two adjacent edges in 2d
                        for (int j = 0; j < loops[i].Count; j++)
                        {
                            if (loops[i][j].curve == null)
                            {   // a pole (singularity)
                                int next = (j + 1) % loops[i].Count;
                                int last = (j + loops[i].Count - 1) % loops[i].Count;
                                if (loops[i][j].createdEdges.Count == 1)
                                {   // this must be the case
                                    ICurve2D singularCurve2d = new Line2D(loops[i][last].createdEdges[0].Curve2D(fc).EndPoint, loops[i][next].createdEdges[0].Curve2D(fc).StartPoint);
                                    if (loops[i][j].createdEdges[0].PrimaryCurve2D == null)
                                    {
                                        loops[i][j].createdEdges[0].SetPrimary(fc, singularCurve2d, true);
                                    }
                                    else
                                    {
                                        loops[i][j].createdEdges[0].SetSecondary(fc, singularCurve2d, true);
                                    }
                                }
                                else
                                {

                                }
                            }
                        }
                        edges[i] = createdEdges.ToArray();
                    }
#if DEBUG
                    // show the 2d bounds of the face (with orientation)
                    DebuggerContainer dc2d = new DebuggerContainer();
                    double arrowsize = ext.Size / 100.0;
                    List<List<ICurve2D>> orientationTest = new List<List<ICurve2D>>();
                    for (int i = 0; i < edges.Length; i++)
                    {
                        List<ICurve2D> outline = new List<ICurve2D>();
                        for (int j = 0; j < edges[i].Length; j++)
                        {
                            dc2d.Add(edges[i][j].Curve2D(fc), System.Drawing.Color.Blue, i * 100 + j);
                            outline.Add(edges[i][j].Curve2D(fc));
                            try
                            {
                                GeoPoint2D[] arrowpnts = new GeoPoint2D[3];
                                GeoVector2D dir = edges[i][j].Curve2D(fc).DirectionAt(0.5).Normalized;
                                arrowpnts[1] = edges[i][j].Curve2D(fc).PointAt(0.5);
                                arrowpnts[0] = arrowpnts[1] - arrowsize * dir + arrowsize * dir.ToLeft();
                                arrowpnts[2] = arrowpnts[1] - arrowsize * dir + arrowsize * dir.ToRight();
                                Polyline2D pl2d = new Polyline2D(arrowpnts);
                                dc2d.Add(pl2d, System.Drawing.Color.Red, 0);
                            }
                            catch (Polyline2DException) { }
                            catch (GeoVectorException) { }
                        }
                        orientationTest.Add(outline);
                    }
                    // check, whether the orientations are coorect
                    double maxarea = 0.0;
                    int biggest = -1;
                    for (int i = 0; i < orientationTest.Count; i++)
                    {
                        double a = Border.SignedArea(orientationTest[i]);
                        if (Math.Abs(a) > maxarea)
                        {
                            biggest = i;
                            maxarea = Math.Abs(a);
                        }
                    }
                    if (Border.CounterClockwise(orientationTest[biggest]) != sameSense)
                    {
                        // wrong orientation
                    }
                    for (int i = 0; i < orientationTest.Count; i++)
                    {
                        if (i != biggest)
                        {
                            if (Border.CounterClockwise(orientationTest[i]) == sameSense)
                            {
                                // wrong orientation
                            }
                        }
                    }
                    if (edges[0].Length == 1)
                    {

                    }
#endif
                    // here we can do better: everything is correctly sorted, we must find the outline versus holes
                    fc.Set(surface, edges);
                    SimpleShape dbgarea = fc.Area;
                    // if (as in rare cases) a loop curve going through a pole had to be splitted
                    // we have to propagate the partial edges to the original loop curve
                    for (int i = splittedByPoles.Count - 1; i >= 0; --i)
                    {
                        StepEdgeDescriptor original = splittedByPoles[i].First;
                        List<StepEdgeDescriptor> replacedBy = splittedByPoles[i].Second;
                        if (original.createdEdges.Count > 0) // there is only one
                        {
                            Edge[] replacingEdges = new Edge[replacedBy.Count];
                            for (int j = 0; j < replacedBy.Count; j++)
                            {
                                replacingEdges[j] = replacedBy[j].createdEdges[0]; // it must be exactely one
                                ICurve2D c2d = original.createdEdges[0].PrimaryFace.surface.GetProjectedCurve(replacedBy[j].curve, 0.0);
                                bool forward = original.createdEdges[0].Forward(original.createdEdges[0].PrimaryFace);
                                if (!forward) c2d.Reverse();
                                replacingEdges[j].SetSecondary(original.createdEdges[0].PrimaryFace, c2d, forward);
                            }
                            Vertex svr = replacingEdges[0].StartVertex(original.createdEdges[0].PrimaryFace);
                            Vertex svo = original.createdEdges[0].StartVertex(original.createdEdges[0].PrimaryFace);
                            if (svr != svo) Array.Reverse(replacingEdges);
                            original.createdEdges[0].PrimaryFace.ReplaceEdge(original.createdEdges[0], replacingEdges, true);
                        }
                        else
                        {
                            // this loop-curve has not been used yet, so move all the newly created edges there
                            for (int j = 0; j < replacedBy.Count; j++)
                            {
                                original.createdEdges.Add(replacedBy[j].createdEdges[0]); // it must be exactely one
                            }
                        }
                    }
#if DEBUG
                    if (!fc.CheckConsistency()) { }
#endif
                    fc.ReduceVertices(); // in rare cases the vertices are defined multiple times in STEP. We need to have unique vertices
                    fc.MakeArea();
                    return new Face[] { fc };
                }
                else
                {   // we must split this periodic face into two ore more parts
                    // we divide the 2d area into two parts (only one parameter is periodic) or into 4 parts (both u and v are periodic)
                    // for each periodic parameter there will be 3 lines (2d) e.g. cylinder: u = 0, pi, 2*pi, v infinite

                    // maybe a surface is periodic in both parameters, but one parameter doesn't touch both sides
                    if (uperiod > 0)
                    {
                        if ((ext.Right - ext.Left) < uperiod * 0.9) uperiod = 0;
                    }
                    if (vperiod > 0)
                    {
                        if ((ext.Top - ext.Bottom) < vperiod * 0.9) vperiod = 0;
                    }
                    double umin, umax, vmin, vmax;
                    umin = ext.Left;
                    umax = ext.Right;
                    vmin = ext.Bottom;
                    vmax = ext.Top;
                    if (uperiod > 0.0)
                    {
                        umin = 0.0;
                        umax = uperiod;
                        if (vperiod > 0)
                        {
                            vmin = 0.0;
                            vmax = vperiod;
                        }
                        else
                        {   // are there poles in the middle of the surface?
                            // if so, we would have to split the surface here as well (e. a conic, which contains the waist)
                            // REMOVED: poles are included as 2d curves and in ext
                            // we still need these pole points (83855_elp11b.stp, face 1242)
                            if (vs != null && vs.Length > 0)
                            {
                                for (int i = 0; i < vs.Length; i++)
                                {
                                    vmin = Math.Min(vmin, vs[i]);
                                    vmax = Math.Max(vmax, vs[i]);
                                }
                            }
                        }
                    }
                    if (vperiod > 0.0)
                    {
                        vmin = 0.0;
                        vmax = vperiod;
                        if (uperiod > 0)
                        {
                            umin = 0.0;
                            umax = uperiod;
                        }
                        else
                        {   // are their poles in the middle of the surface?
                            // if so, we would have to split the surface here as well (e. a conic, which contains the waist)
                            // REMOVED: poles are included as 2d curves and in ext
                        }
                    }
                    // in most cases we can use one plane (sphere, cylinder, cone) or two planes (torus) to split the periodic space in two resp. four parts.
                    // working in 3d is probably more accurate. If there should be a case, where this is not possible (strangely distorted periodic nurbs surface)
                    // we would have to implement a 2d intersection mechanism as well.
                    Plane uSplitPlane = Plane.XYPlane, vSplitPlane = Plane.XYPlane;
                    bool uPlaneValid = false, vPlaneValid = false;
                    double[] uSplitPositions = null, vSplitPositions = null;
                    if (uperiod > 0)
                    {
                        ICurve cv1 = surface.FixedU(0.0, vmin, vmax);
                        ICurve cv2 = surface.FixedU(uperiod / 2.0, vmin, vmax);
                        uPlaneValid = Curves.GetCommonPlane(cv1, cv2, out uSplitPlane);
                        if (!uPlaneValid)
                        {   // we cannot simply use a plane to cut the face, because the curves at 0 and period/2 don't reside in a plane
                            // instead of this simple approach, which is OK for cones cylinders, tori, spheres, rotated 2d curves and most nurbs surfaces,
                            // we have to split the periodic parameter range into period/2 segments at 0, period/2, period, 3/2*period and so on
                            List<double> lPositions = new List<double>();
                            double left = ext.Left + uperiod * 1e-6;
                            double right = ext.Right - uperiod * 1e-6;
                            int n = 0;
                            do
                            {
                                double u = n * uperiod / 2.0;
                                if (-u <= left && u >= right) break;
                                if (u > left && u < right) lPositions.Add(u);
                                if (n > 0 && -u > left && -u < right) lPositions.Insert(0, -u);
                                ++n;
                            } while (true);
                            uSplitPositions = lPositions.ToArray();
                        }
                    }
                    if (vperiod > 0)
                    {
                        ICurve cv1 = surface.FixedV(0.0, umin, umax);
                        ICurve cv2 = surface.FixedV(vperiod / 2.0, umin, umax);
                        vPlaneValid = Curves.GetCommonPlane(cv1, cv2, out vSplitPlane);
                        if (!vPlaneValid)
                        {
                            List<double> lPositions = new List<double>();
                            double bottom = ext.Bottom + vperiod * 1e-8;
                            double top = ext.Top - vperiod * 1e-8;
                            int n = 0;
                            do
                            {
                                double v = n * vperiod / 2.0;
                                if (-v <= bottom && v >= top) break;
                                if (v > bottom && v < top) lPositions.Add(v);
                                if (n > 0 && -v > bottom && -v < top) lPositions.Insert(0, -v);
                                ++n;
                            } while (true);
                            vSplitPositions = lPositions.ToArray();
                        }
                    }
                    // if edges already exist AND already have been split because the other face also was periodic
                    // then we make two edge descriptors in order to prevent the same edge being split differently on two faces
                    for (int i = 0; i < loops.Count; i++)
                    {
                        for (int j = 0; j < loops[i].Count; j++)
                        {
                            if (loops[i][j].createdEdges != null && loops[i][j].createdEdges.Count > 1)
                            {
                                // I cannot imagine a case, where these edges would not be connected
                                // and the orientation must be reverse
                                StepEdgeDescriptor se = loops[i][j];
                                BoundingRect domain = se.curve2d.GetExtent();
                                loops[i].RemoveAt(j);
                                for (int k = 0; k < se.createdEdges.Count; k++)
                                {
                                    StepEdgeDescriptor separt = new StepEdgeDescriptor(se.createdEdges[k], se.forward);
                                    ICurve2D crv2d = surface.GetProjectedCurve(separt.curve, 0.0);
                                    if (!se.forward) crv2d.Reverse();
                                    SurfaceHelper.AdjustPeriodic(surface, domain, crv2d);
                                    separt.curve2d = crv2d;
                                    // not sure whether directions and vertex1/2 are correct
                                    loops[i].Insert(j, separt);
                                }
                            }
                        }
                    }
                    List<ICurve2D> crvs2d = new List<ICurve2D>(); // all 2d curves (split and unsplit)
                    List<ICurve> crvs3d = new List<ICurve>(); // synchronous list of 3d curves
                    List<int> loopSpan = new List<int>(); // indices in crvs2d where a new loop begins
                    allVertices = new Set<Vertex>();
                    double minLength = double.MaxValue;
                    for (int i = 0; i < loops.Count; i++)
                    {
                        int firstCrv2d = crvs2d.Count;
                        loopSpan.Add(crvs2d.Count); // here a new loop begins
                        Vertex lastIntersection = null;
                        for (int j = 0; j < loops[i].Count; j++)
                        {
                            if (loops[i][j].isSeam && loops[i][j].curve != null)
                            {
                                minLength = Math.Min(minLength, loops[i][j].curve.Length);
                            }
                            if (loops[i][j].curve != null && !loops[i][j].isSeam)
                            {
                                allVertices.Add(loops[i][j].vertex1);
                                allVertices.Add(loops[i][j].vertex2);
                                List<double> ispPos = new List<double>();
                                // cut the edges by the plane(s) to make non-periodic parts
                                ICurve forwardOrientedCurve = loops[i][j].curve.Clone();
                                if (!loops[i][j].forward) forwardOrientedCurve.Reverse(); // this 3d curve is forward oriented for this surface
                                                                                          // we need the orientation to get the 2d curves in the correct order
                                if (uPlaneValid) ispPos.AddRange(forwardOrientedCurve.GetPlaneIntersection(uSplitPlane));
                                else if (uSplitPositions != null)
                                {
                                    for (int ui = 0; ui < uSplitPositions.Length; ui++)
                                    {
                                        GeoPoint2DWithParameter[] ip2d = loops[i][j].curve2d.Intersect(new GeoPoint2D(uSplitPositions[ui], ext.Bottom), new GeoPoint2D(uSplitPositions[ui], ext.Top));
                                        for (int ipi = 0; ipi < ip2d.Length; ipi++)
                                        {
                                            if (ip2d[ipi].par1 > 0 && ip2d[ipi].par1 < 1)
                                            {
                                                ispPos.Add(forwardOrientedCurve.PositionOf(surface.PointAt(ip2d[ipi].p)));
                                            }
                                        }
                                    }

                                }
                                if (vPlaneValid) ispPos.AddRange(forwardOrientedCurve.GetPlaneIntersection(vSplitPlane));
                                else if (vSplitPositions != null)
                                {
                                    for (int vi = 0; vi < vSplitPositions.Length; vi++)
                                    {
                                        GeoPoint2DWithParameter[] ip2d = loops[i][j].curve2d.Intersect(new GeoPoint2D(ext.Left, vSplitPositions[vi]), new GeoPoint2D(ext.Right, vSplitPositions[vi]));
                                        for (int ipi = 0; ipi < ip2d.Length; ipi++)
                                        {
                                            if (ip2d[ipi].par1 > 0 && ip2d[ipi].par1 < 1)
                                            {
                                                ispPos.Add(forwardOrientedCurve.PositionOf(surface.PointAt(ip2d[ipi].p)));
                                            }
                                        }
                                    }

                                }
                                for (int k = ispPos.Count - 1; k >= 0; --k)
                                {
                                    if (ispPos[k] <= 1e-5 || ispPos[k] >= 1 - 1e-5 || double.IsNaN(ispPos[k])) ispPos.RemoveAt(k); // avoid intersectionpoints at the very beginning and end
                                }

                                ispPos.Add(0.0);
                                ispPos.Add(1.0);
                                ispPos.Sort();
                                GeoPoint2D sp2d, ep2d;
                                sp2d = surface.PositionOf(loops[i][j].vertex1.Position);
                                ep2d = surface.PositionOf(loops[i][j].vertex2.Position);
                                SurfaceHelper.AdjustPeriodic(surface, ext, ref sp2d);
                                SurfaceHelper.AdjustPeriodic(surface, ext, ref ep2d);
                                for (int ii = 0; ii < ispPos.Count - 1; ii++)
                                {
                                    if (ispPos[ii + 1] - ispPos[ii] > 1e-6)
                                    {
                                        ICurve crv3d = forwardOrientedCurve.Clone();
                                        bool unsplitted = false;
                                        Vertex splitVertex = null;
                                        if (ispPos[ii] == 0.0 && ispPos[ii + 1] == 1.0)
                                        {
                                            unsplitted = true;
                                            lastIntersection = null;
                                        }
                                        else
                                        {
                                            crv3d.Trim(ispPos[ii], ispPos[ii + 1]);
                                            // the new split points will be new vertices. In order that UseVertices doesn't use a close by vertex
                                            // we create the exact vertex here. We don't need to check the startpoint, since it already was the endpoint of the previous segment.
                                            if (ii < ispPos.Count - 1)
                                            {
                                                splitVertex = new Vertex(crv3d.EndPoint);
                                                allVertices.Add(splitVertex);
                                            }
                                        }
                                        minLength = Math.Min(minLength, crv3d.Length);
                                        crvs3d.Add(crv3d);
                                        ICurve2D crv2d;
                                        if (unsplitted)
                                        {
                                            crv2d = loops[i][j].curve2d;
                                        }
                                        else
                                        {
                                            crv2d = surface.GetProjectedCurve(crv3d, 0.0); // if crv3d we must mark this projected curve as reversed
                                            SurfaceHelper.AdjustPeriodic(surface, loops[i][j].curve2d.GetExtent(), crv2d); // move into the same domain as the original curve2d
                                        }
                                        lastIntersection = splitVertex;
                                        if (!loops[i][j].forward)
                                        {
                                            crv3d.Reverse(); // crv3d was forward oriented, now it is oriented according to loop[i][j].forward again
                                            if (crv2d is ProjectedCurve pc) pc.IsReverse = true; // mark this projected curve as reversed
                                        }
                                        crv2d.UserData["EdgeDescriptor"] = loops[i][j];
                                        crv2d.UserData["Curves3DIndex"] = crvs3d.Count - 1;
                                        crv2d.UserData["Unsplitted"] = unsplitted;
                                        if (uperiod > 0)
                                        {
                                            while (crv2d.PointAt(0.5).x > uperiod) crv2d.Move(-uperiod, 0);
                                            while (crv2d.PointAt(0.5).x < 0.0) crv2d.Move(uperiod, 0);
                                        }
                                        if (vperiod > 0)
                                        {
                                            while (crv2d.PointAt(0.5).y > vperiod) crv2d.Move(0, -vperiod);
                                            while (crv2d.PointAt(0.5).y < 0.0) crv2d.Move(0, vperiod);
                                        }
                                        else if (surface.IsVPeriodic)
                                        {
                                            double extcv = (ext.Bottom + ext.Top) / 2.0;
                                            while (crv2d.PointAt(0.5).y > extcv + surface.VPeriod / 2.0) crv2d.Move(0, -surface.VPeriod);
                                            while (crv2d.PointAt(0.5).y < extcv - surface.VPeriod / 2.0) crv2d.Move(0, surface.VPeriod);
                                        }
                                        crvs2d.Add(crv2d);
                                        ext.MinMax(crv2d.GetExtent()); // ext should already reflect the correct extend, but splitting a curve may yield more precise points
                                    }
                                    //else
                                    //{

                                    //}
                                }
                            }
                            else if (loops[i][j].curve == null && loops[i][j].vertex1 == loops[i][j].vertex2)
                            {
                                // maybe we need to split a pole
                                // splitting a pole when not the plane but a list of parameter positions is specified is not yet implemented. No example found.
                                if ((uPlaneValid) || uSplitPositions != null) // omitted: && Precision.IsPointOnPlane(loops[i][j].vertex1.Position, uSplitPlane)
                                {
                                    // poles are always lines, we have to cut this (horizontal) line in uperiod/2 pieces
                                    Line2D l2d = loops[i][j].curve2d as Line2D;
                                    double y = l2d.StartPoint.y;
                                    List<ICurve2D> splitted = new List<ICurve2D>();
                                    if (l2d.StartPoint.x < l2d.EndPoint.x)
                                    {
                                        double upos = Math.Floor(l2d.StartPoint.x / (uperiod / 2) + 1) * (uperiod / 2);
                                        double x = l2d.StartPoint.x;
                                        while (upos < l2d.EndPoint.x)
                                        {
                                            Line2D ppart = new Line2D(new GeoPoint2D(x, y), new GeoPoint2D(upos, y));
                                            if (ppart.Length > uperiod * 1e-6) splitted.Add(ppart);
                                            x = upos;
                                            upos += uperiod / 2;
                                        }
                                        Line2D last = new Line2D(new GeoPoint2D(x, y), new GeoPoint2D(l2d.EndPoint.x, y));
                                        if (last.Length > uperiod * 1e-6) splitted.Add(last);
                                    }
                                    else
                                    {
                                        double upos = Math.Ceiling(l2d.StartPoint.x / (uperiod / 2) - 1) * (uperiod / 2);
                                        double x = l2d.StartPoint.x;
                                        while (upos > l2d.EndPoint.x)
                                        {
                                            Line2D ppart = new Line2D(new GeoPoint2D(x, y), new GeoPoint2D(upos, y));
                                            if (ppart.Length > uperiod * 1e-6) splitted.Add(ppart);
                                            x = upos;
                                            upos -= uperiod / 2;
                                        }
                                        Line2D last = new Line2D(new GeoPoint2D(x, y), new GeoPoint2D(l2d.EndPoint.x, y));
                                        if (last.Length > uperiod * 1e-6) splitted.Add(last);
                                    }
                                    for (int k = 0; k < splitted.Count; k++)
                                    {
                                        crvs3d.Add(null); // the pole has no 3d curve
                                        splitted[k].UserData["EdgeDescriptor"] = loops[i][j];
                                        splitted[k].UserData["Curves3DIndex"] = crvs3d.Count - 1;
                                        splitted[k].UserData["Unsplitted"] = false;
                                        if (uperiod > 0)
                                        {
                                            while (splitted[k].PointAt(0.5).x > uperiod) splitted[k].Move(-uperiod, 0);
                                            while (splitted[k].PointAt(0.5).x < 0.0) splitted[k].Move(uperiod, 0);
                                        }
                                        crvs2d.Add(splitted[k]);
                                        ext.MinMax(splitted[k].GetExtent()); // ext should already reflect the correct extend, but splitting a curve may yield more precise points
                                    }
                                }
                                if ((vPlaneValid) || vSplitPositions != null) // omitted: && Precision.IsPointOnPlane(loops[i][j].vertex1.Position, vSplitPlane)
                                {
                                    // poles are always lines, we have to cut this (vertical) line in vperiod/2 pieces
                                    Line2D l2d = loops[i][j].curve2d as Line2D;
                                    double x = l2d.StartPoint.x;
                                    List<ICurve2D> splitted = new List<ICurve2D>();
                                    if (l2d.StartPoint.y < l2d.EndPoint.y)
                                    {
                                        double vpos = Math.Floor(l2d.StartPoint.y / (vperiod / 2) + 1) * (vperiod / 2);
                                        double y = l2d.StartPoint.y;
                                        while (vpos < l2d.EndPoint.y)
                                        {
                                            Line2D ppart = new Line2D(new GeoPoint2D(x, y), new GeoPoint2D(x, vpos));
                                            if (ppart.Length > vperiod * 1e-6) splitted.Add(ppart);
                                            y = vpos;
                                            vpos += vperiod / 2;
                                        }
                                        Line2D last = new Line2D(new GeoPoint2D(x, y), new GeoPoint2D(x, l2d.EndPoint.y));
                                        if (last.Length > uperiod * 1e-6) splitted.Add(last);
                                    }
                                    else
                                    {
                                        double vpos = Math.Ceiling(l2d.StartPoint.y / (vperiod / 2) - 1) * (vperiod / 2);
                                        double y = l2d.StartPoint.y;
                                        while (vpos > l2d.EndPoint.y)
                                        {
                                            Line2D ppart = new Line2D(new GeoPoint2D(x, y), new GeoPoint2D(x, vpos));
                                            if (ppart.Length > vperiod * 1e-6) splitted.Add(ppart);
                                            y = vpos;
                                            vpos -= vperiod / 2;
                                        }
                                        Line2D last = new Line2D(new GeoPoint2D(x, y), new GeoPoint2D(x, l2d.EndPoint.y));
                                        if (last.Length > uperiod * 1e-6) splitted.Add(last);
                                    }
                                    for (int k = 0; k < splitted.Count; k++)
                                    {
                                        crvs3d.Add(null); // the pole has no 3d curve
                                        splitted[k].UserData["EdgeDescriptor"] = loops[i][j];
                                        splitted[k].UserData["Curves3DIndex"] = crvs3d.Count - 1;
                                        splitted[k].UserData["Unsplitted"] = false;
                                        if (vperiod > 0)
                                        {
                                            while (splitted[k].PointAt(0.5).y > vperiod) splitted[k].Move(0, -vperiod);
                                            while (splitted[k].PointAt(0.5).y < 0.0) splitted[k].Move(0, vperiod);
                                        }
                                        crvs2d.Add(splitted[k]);
                                    }
                                }
                                allVertices.Add(loops[i][j].vertex1);
                            }
                        }
                    }
                    loopSpan.Add(crvs2d.Count);
#if DEBUG
                    // show the 2d splitted curves and their directions
                    DebuggerContainer dc2d = new DebuggerContainer();
                    double arrowsize = ext.Size / 100.0;
                    for (int i = 0; i < crvs2d.Count; i++)
                    {
                        dc2d.Add(crvs2d[i], System.Drawing.Color.Blue, i);
                        GeoPoint2D[] arrowpnts = new GeoPoint2D[3];
                        GeoVector2D dir = crvs2d[i].DirectionAt(0.5);
                        if (!dir.IsNullVector())
                        {
                            dir = dir.Normalized;
                            arrowpnts[1] = crvs2d[i].PointAt(0.5);
                            arrowpnts[0] = arrowpnts[1] - arrowsize * dir + arrowsize * dir.ToLeft();
                            arrowpnts[2] = arrowpnts[1] - arrowsize * dir + arrowsize * dir.ToRight();
                            Polyline2D pl2d = new Polyline2D(arrowpnts);
                            dc2d.Add(pl2d, System.Drawing.Color.Red, 0);
                        }
                    }
                    for (int i = 0; i < loops.Count; i++)
                    {
                        for (int j = 0; j < loops[i].Count; j++)
                        {
                            if (loops[i][j].vertex1 != null)
                            {
                                Point pnt = Point.Construct();
                                pnt.Symbol = PointSymbol.Cross;
                                GeoPoint2D p2d = surface.PositionOf(loops[i][j].vertex1.Position);
                                SurfaceHelper.AdjustPeriodic(surface, ext, ref p2d);
                                pnt.Location = new GeoPoint(p2d);
                                dc2d.Add(pnt);
                            }
                            if (loops[i][j].vertex2 != null)
                            {
                                Point pnt = Point.Construct();
                                pnt.Symbol = PointSymbol.Cross;
                                GeoPoint2D p2d = surface.PositionOf(loops[i][j].vertex2.Position);
                                SurfaceHelper.AdjustPeriodic(surface, ext, ref p2d);
                                pnt.Location = new GeoPoint(p2d);
                                dc2d.Add(pnt);
                            }
                        }
                    }
                    foreach (Vertex vtx in allVertices)
                    {
                        Point pnt = Point.Construct();
                        pnt.Symbol = PointSymbol.Plus;
                        GeoPoint2D p2d = surface.PositionOf(vtx.Position);
                        SurfaceHelper.AdjustPeriodic(surface, ext, ref p2d);
                        pnt.Location = new GeoPoint(p2d);
                        dc2d.Add(pnt);
                    }
                    if (uperiod == 0)
                    {
                        umin = ext.Left;
                        umax = ext.Right;
                        // we still need these pole points (83855_elp11b.stp, face 1242)
                        if (us != null && us.Length > 0)
                        {
                            for (int i = 0; i < us.Length; i++)
                            {
                                umin = Math.Min(umin, us[i]);
                                umax = Math.Max(umax, us[i]);
                            }
                        }
                    }
                    if (vperiod == 0)
                    {
                        vmin = ext.Bottom;
                        vmax = ext.Top;
                        // we still need these pole points (83855_elp11b.stp, face 1242)
                        if (vs != null && vs.Length > 0)
                        {
                            for (int i = 0; i < vs.Length; i++)
                            {
                                vmin = Math.Min(vmin, vs[i]);
                                vmax = Math.Max(vmax, vs[i]);
                            }
                        }
                    }
                    GeoPoint2D[] boundpnts = new GeoPoint2D[5];
                    boundpnts[4] = boundpnts[0] = new GeoPoint2D(umin, vmin);
                    boundpnts[1] = new GeoPoint2D(umax, vmin);
                    boundpnts[2] = new GeoPoint2D(umax, vmax);
                    boundpnts[3] = new GeoPoint2D(umin, vmax);
                    Polyline2D bnd2d = new Polyline2D(boundpnts);
                    dc2d.Add(bnd2d, System.Drawing.Color.Green, 0);
                    // *** check 2d split curves and directions
#endif
                    // now we make two or four sets of curves (corresponding to the non periodic sub-patches of the surface) and make faces from each set
                    Set<int>[,] part = new Set<int>[2, 2];
                    part[0, 0] = new Set<int>();
                    part[0, 1] = new Set<int>();
                    part[1, 0] = new Set<int>();
                    part[1, 1] = new Set<int>();
                    // distribute the 2d curves into the appropriate patch
                    for (int i = 0; i < crvs2d.Count; i++)
                    {
                        GeoPoint2D mp = crvs2d[i].PointAt(0.5);
                        int ui = 0, vi = 0;
                        if (uperiod > 0) ui = Math.Max(0, Math.Min(1, (int)Math.Floor((mp.x / uperiod * 2))));
                        if (vperiod > 0) vi = Math.Max(0, Math.Min(1, (int)Math.Floor((mp.y / vperiod * 2))));
                        part[ui, vi].Add(i);
                    }
                    // and for each set we calculate a list of parameters for the counterclockwise rectangle, starting at the lower left point of the bounding rectangle
                    // for each patch collect the parts of the 2d curves of this patch together with the appropriate parts of the bounding rectangle
                    Set<Face> res = new Set<Face>();
                    Dictionary<DoubleVertexKey, Edge> seams = new Dictionary<DoubleVertexKey, Edge>();
                    for (int ui = 0; ui < 2; ui++)
                        for (int vi = 0; vi < 2; vi++)
                        {
                            double left, right, bottom, top;
                            if (uperiod > 0)
                            {
                                left = ui * uperiod / 2;
                                right = (ui + 1) * uperiod / 2;
                            }
                            else
                            {
                                left = umin;
                                right = umax;
                            }
                            if (vperiod > 0)
                            {
                                bottom = vi * vperiod / 2;
                                top = (vi + 1) * vperiod / 2;
                            }
                            else
                            {
                                bottom = vmin;
                                top = vmax;
                            }
                            BoundingRect patch = new BoundingRect(left, bottom, right, top);
                            Set<int> s = part[ui, vi];
                            if (s.Count == 0) continue;
                            List<List<ICurve2D>> looplist = new List<List<ICurve2D>>();
                            Set<ICurve2D> seamLines = new Set<ICurve2D>();
                            BoundingRect precisionExt = ext;
                            precisionExt.Inflate(ext.Width * 10, ext.Height * 10); // this is for precision only
                                                                                   // we get problems with almost tangential intersections
                            Set<int> entering = new Set<int>();
                            Set<int> leaving = new Set<int>();
                            for (int i = 0; i < crvs2d.Count; i++)
                            {
                                int ni = NextInSameLoop(i, loopSpan);
                                if (s.Contains(i) && !s.Contains(ni))
                                {
                                    leaving.Add(i);
                                }
                                if (!s.Contains(i) && s.Contains(ni))
                                {
                                    entering.Add(ni);
                                }
                            }
                            int maxCount = s.Count + leaving.Count + 4;
                            while (s.Count > 0)
                            {
                                List<ICurve2D> loop = new List<ICurve2D>();
                                int currentInd = s.GetAny();
                                int startInd = currentInd;
                                loop.Add(crvs2d[currentInd]);
                                s.Remove(currentInd);
                                while (true) // stopped by break, when the first curve is reached
                                {
                                    int nextind = -1;
                                    // ind ist the index of the last added curve2d
                                    if (!leaving.Contains(currentInd))
                                    {   // connected next curve can be added to the loop
                                        nextind = NextInSameLoop(currentInd, loopSpan);
                                        // nextind must be in s
                                    }
                                    else
                                    {   // we must insert a line (or sometimes more: torus) from the bounds of the patch
                                        double pos = patch.PositionOf(crvs2d[currentInd].EndPoint);
                                        double minDist = double.MaxValue;
                                        nextind = -1;
                                        foreach (int ind2 in entering)
                                        {
                                            double pos2 = patch.PositionOf(crvs2d[ind2].StartPoint);
                                            double d = pos2 - pos;
                                            if (d < 0.0) d += 4.0;
                                            if (d < minDist)
                                            {
                                                minDist = d;
                                                nextind = ind2;
                                            }
                                        }
                                        // there must be a valid nexind
                                        GeoPoint2D[] trimmed = patch.GetLines(pos, (pos + minDist) % 4.0); // the vertices of a polyline from pos to pos+minDist
                                        for (int j = 0; j < trimmed.Length - 1; j++) // could be more than two points when we have 4 parts (torus-like surface)
                                        {
                                            Line2D l2d = new Line2D(trimmed[j], trimmed[j + 1]);
                                            if (l2d.Length > (uperiod + vperiod) * 1e-6)
                                            {
                                                loop.Add(l2d);
                                                seamLines.Add(l2d);
                                            }
                                        }
                                    }
                                    if (nextind == startInd) break; // loop is closed
                                    loop.Add(crvs2d[nextind]);
                                    s.Remove(nextind);
                                    currentInd = nextind;
                                    if (loop.Count > maxCount + 4) return null; // there is an endless loop! Should never happen. 
                                }
                                // there are some strange cases (e.g. "17044100P011 Kein Volumenmodell.stp", step index: 2561) where the seam is *part* of an edge
                                // here we get two identical curves going for and back.
                                for (int j = loop.Count - 1; j > 0; --j)
                                {
                                    if ((loop[j].EndPoint | loop[j - 1].StartPoint) + (loop[j].StartPoint | loop[j - 1].EndPoint) < Precision.eps)
                                    {
                                        if (loop[j].Distance(loop[j - 1].PointAt(0.5)) < Precision.eps)
                                        {
                                            loop.RemoveAt(j);
                                            loop.RemoveAt(j - 1);
                                            --j;
                                        }
                                    }
                                }
                                looplist.Add(loop);
                            }
                            // Now the looplist contains one or more outer loops (counterclock) and maybe some inner loops (clockwise)
                            // for this (nonperiodic) patch of the surface. In very rare cases there are multiple outer loops. then we have to find which inner loop
                            // resides inside which outer loop
#if DEBUG
                            DebuggerContainer dcloops = new DebuggerContainer();
                            arrowsize = ext.Size / 100.0;
                            for (int i = 0; i < looplist.Count; i++)
                            {
                                for (int j = 0; j < looplist[i].Count; j++)
                                {
                                    dcloops.Add(looplist[i][j], System.Drawing.Color.Blue, i * 100 + j);
                                    GeoPoint2D[] arrowpnts = new GeoPoint2D[3];
                                    GeoVector2D dir = looplist[i][j].DirectionAt(0.5).Normalized;
                                    arrowpnts[1] = looplist[i][j].PointAt(0.5);
                                    arrowpnts[0] = arrowpnts[1] - arrowsize * dir + arrowsize * dir.ToLeft();
                                    arrowpnts[2] = arrowpnts[1] - arrowsize * dir + arrowsize * dir.ToRight();
                                    Polyline2D pl2d = new Polyline2D(arrowpnts);
                                    dcloops.Add(pl2d, System.Drawing.Color.Red, i * 100 + j);
                                }
                            }
                            // *** check 2d curves in this patch (ui,vi)
#endif
                            List<int> outlines = new List<int>();
                            List<int> holes = new List<int>();
                            for (int i = 0; i < looplist.Count; i++)
                            {
                                if (Border.CounterClockwise(looplist[i]))
                                {   // an outline loop may have two consecutive lines, which belong to the same seam (very rare case, but see 10163_SF51_01_091118.stp)
                                    // in this case we must combine the lines
                                    // NO: dont combine because of 83855_elp11b.stp (the borderpos points have been adapted)
                                    //for (int j = 0; j < looplist[i].Count; j++)
                                    //{
                                    //    int next = (j + 1) % looplist[i].Count;
                                    //    if (seamLines.Contains(looplist[i][j]) && seamLines.Contains(looplist[i][next]) && Precision.SameDirection(looplist[i][j].EndDirection, looplist[i][next].StartDirection, false))
                                    //    {
                                    //        // two seam lines with same direction: combine to a single one:
                                    //        looplist[i][j].EndPoint = looplist[i][next].EndPoint; // it is two lines, so no problem here
                                    //        looplist[i].RemoveAt(next);
                                    //        --j; // same test once more
                                    //    }
                                    //}
                                    outlines.Add(i);
                                }
                                else holes.Add(i);
                            }
                            if (outlines.Count > 1 && holes.Count > 0)
                            {   // we have multiple outlines and at least one hole. To which outline belongs the hole?
                                // no case found until now
                                return null;
                                // throw new NotImplementedException("to implement: multiple outlines with holes in face splitting (step import)");
                            }
                            else
                            {
                                for (int i = 0; i < outlines.Count; i++)
                                {
                                    Face fc = Face.Construct();
                                    List<Edge[]> outlineAndHoles = new List<Edge[]>();
                                    List<Edge> ledge = new List<Edge>();
                                    double area = 0.0;
                                    for (int j = 0; j < looplist[outlines[i]].Count; j++)
                                    {
                                        StepEdgeDescriptor sed = null;
                                        int crvs3dind = -1;
                                        bool unsplitted = false;
                                        if (looplist[outlines[i]][j].UserData.Contains("EdgeDescriptor"))
                                        {
                                            sed = looplist[outlines[i]][j].UserData["EdgeDescriptor"] as StepEdgeDescriptor;
                                            crvs3dind = (int)looplist[outlines[i]][j].UserData["Curves3DIndex"];
                                            unsplitted = (bool)looplist[outlines[i]][j].UserData["Unsplitted"];
                                        }
                                        if (crvs3dind >= 0)
                                        {
                                            // part of an StepEdgeDescriptor

                                            Edge edg;
                                            if (sed.createdEdges.Count == 1 && unsplitted)
                                            {
                                                sed.createdEdges[0].SetSecondary(fc, looplist[outlines[i]][j], sed.forward);
                                                edg = sed.createdEdges[0];
                                            }
                                            else
                                            {
                                                edg = new Edge(fc, crvs3d[crvs3dind], fc, looplist[outlines[i]][j], sed.forward);
                                                if (edg.Curve3D != null) edg.UseVertices(allVertices, minLength * 0.1);
                                                else
                                                {
                                                    // a pole, find the best vertex
                                                    GeoPoint pl = surface.PointAt(looplist[outlines[i]][j].StartPoint);
                                                    Vertex poleVertex = null;
                                                    double minDist = double.MaxValue;
                                                    foreach (Vertex vtx in allVertices)
                                                    {
                                                        double d = pl | vtx.Position;
                                                        if (d < minDist)
                                                        {
                                                            minDist = d;
                                                            poleVertex = vtx;
                                                        }
                                                    }
                                                    if (poleVertex != null) edg.Vertex1 = edg.Vertex2 = poleVertex;
                                                }
                                                sed.createdEdges.Add(edg);
                                            }
                                            // don't use null edges
                                            if (edg.Vertex1 != edg.Vertex2 || (edg.Curve3D != null && edg.Curve3D.Length < minLength / 2) || (edg.Curve3D == null))
                                            {
                                                if (edg.Curve3D == null && ledge.Count > 0 && ledge[ledge.Count - 1].Curve3D == null)
                                                {   // two adjacent poles is not allowed
                                                    // skip this pole
                                                }
                                                else
                                                {
                                                    ledge.Add(edg);
                                                }
                                            }
                                            area += looplist[outlines[i]][j].GetArea();
                                        }
                                        else
                                        {
                                            // a seam or a pole
                                            ICurve crv = surface.Make3dCurve(looplist[outlines[i]][j]);
                                            Edge edg = new Edge(fc, crv, fc, looplist[outlines[i]][j], true);
                                            if (crv != null) edg.UseVertices(allVertices, minLength * 0.1);
                                            else
                                            {
                                                // a pole, find the best vertex
                                                GeoPoint pl = surface.PointAt(looplist[outlines[i]][j].StartPoint);
                                                Vertex poleVertex = null;
                                                double minDist = double.MaxValue;
                                                foreach (Vertex vtx in allVertices)
                                                {
                                                    double d = pl | vtx.Position;
                                                    if (d < minLength * 0.1 && d < minDist)
                                                    {
                                                        minDist = d;
                                                        poleVertex = vtx;
                                                    }
                                                }
                                                if (poleVertex != null) edg.Vertex1 = edg.Vertex2 = poleVertex;
                                            }
                                            if (seams.ContainsKey(new DoubleVertexKey(edg.Vertex1, edg.Vertex2)))
                                            {   // this seam has already been generated, now use the reverse oriented first edge on this face
                                                edg = seams[new DoubleVertexKey(edg.Vertex1, edg.Vertex2)];
                                                edg.SetSecondary(fc, looplist[outlines[i]][j], false);
                                            }
                                            else
                                            {
                                                seams[new DoubleVertexKey(edg.Vertex1, edg.Vertex2)] = edg;
                                            }
                                            // don't use null edges
                                            if (edg.Vertex1 != edg.Vertex2 || (edg.Curve3D != null && edg.Curve3D.Length < minLength / 2)) ledge.Add(edg);
                                            area += looplist[outlines[i]][j].GetArea();
                                            // need to combine seam edges!
                                        }
                                    }
                                    if (area > ext.Size * 1e-6) outlineAndHoles.Add(ledge.ToArray());
                                    else
                                    {
                                        for (int k = 0; k < ledge.Count; k++)
                                        {
                                            if (ledge[k].SecondaryFace == fc)
                                            {
                                                ledge[k].RemoveFace(fc);
                                            }
                                            if (ledge[k].PrimaryFace == fc)
                                            {
                                                ledge[k].RemoveFace(fc);
                                            }
                                            for (int l = 0; l < loops.Count; l++)
                                            {
                                                for (int m = 0; m < loops[l].Count; m++)
                                                {
                                                    if (loops[l][m].createdEdges.Contains(ledge[k])) loops[l][m].createdEdges.Remove(ledge[k]);
                                                }
                                            }
                                        }
                                    }
                                    for (int k = 0; k < holes.Count; k++)
                                    {   // all holes are inside this (single) outline
                                        // holes cannot have seams (or poles?)
                                        ledge.Clear();
                                        area = 0.0;
                                        for (int j = 0; j < looplist[holes[k]].Count; j++)
                                        {

                                            StepEdgeDescriptor sed = null;
                                            int crvs3dind = -1;
                                            bool unsplitted = false;
                                            if (looplist[holes[k]][j].UserData.Contains("EdgeDescriptor"))
                                            {
                                                sed = looplist[holes[k]][j].UserData["EdgeDescriptor"] as StepEdgeDescriptor;
                                                crvs3dind = (int)looplist[holes[k]][j].UserData["Curves3DIndex"];
                                                unsplitted = (bool)looplist[holes[k]][j].UserData["Unsplitted"];
                                            }
                                            if (crvs3dind >= 0)
                                            {
                                                // part of an StepEdgeDescriptor

                                                Edge edg;
                                                if (sed.createdEdges.Count == 1 && unsplitted)
                                                {
                                                    sed.createdEdges[0].SetSecondary(fc, looplist[holes[k]][j], sed.forward);
                                                    edg = sed.createdEdges[0];
                                                }
                                                else
                                                {
                                                    edg = new Edge(fc, crvs3d[crvs3dind], fc, looplist[holes[k]][j], sed.forward);
                                                    edg.UseVertices(allVertices, minLength * 0.1);
                                                    sed.createdEdges.Add(edg);
                                                }
                                                if (edg.Vertex1 != edg.Vertex2 || (edg.Curve3D != null && edg.Curve3D.Length < minLength / 2) || (edg.Curve3D == null)) ledge.Add(edg);
                                                area += looplist[holes[k]][j].GetArea();
                                            }
                                            else
                                            {   // this should never happen
                                                ledge.Clear();
                                                break;
                                                // throw new ApplicationException("error in splitting face with step import");
                                            }
                                        }
                                        if (ledge.Count > 0 && area < -ext.Size * 1e-6) outlineAndHoles.Add(ledge.ToArray());
                                    }
                                    if (outlineAndHoles.Count > 0)
                                    {
                                        fc.Set(surface.Clone(), outlineAndHoles.ToArray()); // we need to clone the surface so that two faces don't have the same surface (in case we will revers or modify it)
                                        res.Add(fc);
                                    }
                                    else
                                    {

                                    }
                                }
                            }
                        }
                    if (splittedByPoles.Count > 0)
                    {
                        // still to implement: splitted by poles, see above
                    }
                    for (int i = 0; i < loops.Count; i++)
                    {
                        for (int j = 0; j < loops[i].Count; j++)
                        {
                            if (loops[i][j].createdEdges != null && loops[i][j].createdEdges.Count > 2)
                            {
                                // this edge has been split and maybe existed before as a single edge
                                // this single edge must now be replaced by its split parts
                                // only the first edge in createdEdges can belong to a different face 
                                // the new created edges must use the vertices of the already defined edges
                                if (!res.Contains(loops[i][j].createdEdges[0].PrimaryFace))
                                {
                                    Edge onOtherFace = loops[i][j].createdEdges[0]; // this edges belongs to an other Face not to this split faces
                                    List<Edge> replacementEdges = new List<Edge>();
                                    Set<Vertex> toUse = new Set<Vertex>();
                                    toUse.Add(onOtherFace.Vertex1);
                                    toUse.Add(onOtherFace.Vertex2);
                                    for (int k = 0; k < loops[i][j].createdEdges.Count - 1; k++)
                                    {
                                        replacementEdges.Add(loops[i][j].createdEdges[k + 1]);
                                        int kk = replacementEdges.Count - 1;
                                        replacementEdges[kk].UseVertices(toUse, precision);
                                        if (replacementEdges[kk].Vertex1 == replacementEdges[kk].Vertex2)
                                        {
                                            replacementEdges.RemoveAt(kk);
                                        }
                                        else
                                        {
                                            toUse.Add(replacementEdges[kk].Vertex1);
                                            toUse.Add(replacementEdges[kk].Vertex2);
                                            ICurve2D projC2d = onOtherFace.PrimaryFace.Surface.GetProjectedCurve(replacementEdges[kk].Curve3D, 0.0);
                                            SurfaceHelper.AdjustPeriodic(onOtherFace.PrimaryFace.Surface, onOtherFace.PrimaryFace.Area.GetExtent(), projC2d);
                                            bool forward = !replacementEdges[kk].Forward(replacementEdges[kk].PrimaryFace);
                                            if (!forward) projC2d.Reverse();
                                            double pos1 = replacementEdges[kk].Curve3D.PositionOf(onOtherFace.PrimaryFace.Surface.PointAt(projC2d.StartPoint));
                                            double pos2 = replacementEdges[kk].Curve3D.PositionOf(onOtherFace.PrimaryFace.Surface.PointAt(projC2d.EndPoint));
                                            forward = pos1 < pos2;
                                            replacementEdges[kk].SetSecondary(onOtherFace.PrimaryFace, projC2d, forward);
                                        }
                                    }
                                    if (replacementEdges.Count > 1)
                                    {
                                        Edge[] replacementEdgesA = replacementEdges.ToArray();
                                        if (!SortEdges(onOtherFace.PrimaryFace, onOtherFace.StartVertex(onOtherFace.PrimaryFace), onOtherFace.EndVertex(onOtherFace.PrimaryFace), replacementEdgesA))
                                        {   // maybe we have a imprecision with the vertices
                                            BoundingCube vext = BoundingCube.EmptyBoundingCube;
                                            foreach (Vertex vertex in allVertices)
                                            {
                                                vext.MinMax(vertex.Position);
                                            }
                                            double minDist = double.MaxValue;
                                            Vertex mv1 = null, mv2 = null;
                                            foreach (Vertex v1 in toUse)
                                            {
                                                foreach (Vertex v2 in toUse)
                                                {
                                                    if (v1 != v2)
                                                    {
                                                        double d = v1.Position | v2.Position;
                                                        if (d < minDist)
                                                        {
                                                            minDist = d;
                                                            mv1 = v1;
                                                            mv2 = v2;
                                                        }
                                                    }
                                                }
                                            }
                                            if (minDist < vext.Size * 1e-3)
                                            {
                                                mv1.MergeWith(mv2);
                                                bool ok = SortEdges(onOtherFace.PrimaryFace, onOtherFace.StartVertex(onOtherFace.PrimaryFace), onOtherFace.EndVertex(onOtherFace.PrimaryFace), replacementEdgesA);
                                            }
                                        }
                                        replacementEdges = new List<Edge>(replacementEdgesA);
                                    }
                                    GeoVector2D dir1 = replacementEdges[0].Curve2D(replacementEdges[0].SecondaryFace).StartDirection;
                                    GeoVector2D dir2 = onOtherFace.Curve2D(onOtherFace.PrimaryFace).StartDirection;
                                    GeoPoint2D sp1 = replacementEdges[0].Curve2D(replacementEdges[0].SecondaryFace).StartPoint;
                                    GeoPoint2D ep1 = replacementEdges[0].Curve2D(replacementEdges[0].SecondaryFace).EndPoint;
                                    GeoPoint2D sp2 = onOtherFace.Curve2D(onOtherFace.PrimaryFace).StartPoint;
                                    if ((ep1 | sp2) < (sp1 | sp2))
                                    {
                                        for (int k = 0; k < replacementEdges.Count; k++)
                                        {
                                            replacementEdges[k].Reverse(replacementEdges[k].SecondaryFace);
                                        }
                                    }
                                    // following obsolete, vertex test already done above
                                    //for (int k = 0; k < replacementEdges.Count; k++)
                                    //{
                                    //    if (replacementEdges[k].Vertex1 == replacementEdges[k].Vertex2)
                                    //    {
                                    //        List<Edge> shortened = new List<Edge>(replacementEdges);
                                    //        shortened.RemoveAt(k);
                                    //        replacementEdges = shortened;
                                    //    }
                                    //}
                                    onOtherFace.PrimaryFace.ReplaceEdge(onOtherFace, replacementEdges.ToArray(), true); // bug in RAMPS-mount-1.step Face 3
#if DEBUG
                                    bool cok = onOtherFace.PrimaryFace.CheckConsistency();
#endif
                                }
                                else
                                {   // sort the edges in createdEdges so that they start with loops[i][j].vertex1, are connected and end with loops[i][j].vertex2
                                    Vertex toStartWith = loops[i][j].vertex1;
                                    for (int k = 0; k < loops[i][j].createdEdges.Count; k++)
                                    {
                                        for (int l = k; l < loops[i][j].createdEdges.Count; l++)
                                        {
                                            if (loops[i][j].createdEdges[l].Vertex1 == toStartWith)
                                            {
                                                if (l != k)
                                                {
                                                    Edge tmp = loops[i][j].createdEdges[l];
                                                    loops[i][j].createdEdges[l] = loops[i][j].createdEdges[k];
                                                    loops[i][j].createdEdges[k] = tmp;
                                                }
                                                toStartWith = loops[i][j].createdEdges[k].Vertex2;
                                                break;
                                            }
                                        }
                                    }
                                }
                            }
                        }
                    }
                    foreach (Face fc in res)
                    {
                        fc.ReduceVertices(); // in rare cases the vertices are defined multiple times in STEP. We need to have unique vertices
                        fc.MakeArea();
                    }
                    return res.ToArray(); // the split parts of the face. The parts have no seams, no edges, that are used twice in a single face.
                }
            } // end of locking all the edges in case of parallel creation of faces
        }

        internal double IsOppositeParallel(Face other)
        {
            double res;
            if ((res = Surface.IsParallel(Domain, other.Surface, other.Domain)) != double.MaxValue)
            {
                // we need to check, whether ther surfaces are opposite oriented
                GeoPoint2D ip = other.Area.GetSomeInnerPoint();
                GeoPoint p = other.Surface.PointAt(ip);
                GeoVector dir = other.Surface.GetNormal(ip);
                GeoPoint2D[] ips = Surface.GetLineIntersection(p, dir);
                for (int i = 0; i < ips.Length; i++)
                {
                    if (Precision.OppositeDirection(dir, Surface.GetNormal(ips[i])) && Geometry.LinePar(p, dir, Surface.PointAt(ips[i])) < 0)
                    {
                        return res;
                    }
                }
            }
            return double.MaxValue;
        }

        internal void SetOutline(Edge[] outline)
        {
            this.outline = outline;
        }

        /// <summary>
        /// permutate the order of the edges so that the first edge starts at fromVertex and the last edge ends at toVertex and the edges are connected
        /// </summary>
        /// <param name="onThisFace"></param>
        /// <param name="fromVertex"></param>
        /// <param name="toVertex"></param>
        /// <param name="edges"></param>
        private static bool SortEdges(Face onThisFace, Vertex fromVertex, Vertex toVertex, Edge[] edges)
        {
            bool ok = true;
            List<Edge> sorted = new List<Edge>(edges);
            Vertex current = fromVertex;
            for (int ind = 0; ind < edges.Length; ind++)
            {
                for (int i = 0; i < sorted.Count; i++)
                {
                    if (sorted[i].StartVertex(onThisFace) == current)
                    {
                        edges[ind] = sorted[i];
                        current = sorted[i].EndVertex(onThisFace);
                        sorted.RemoveAt(i);
                        break;
                    }
                }
                if (sorted.Count + ind + 1 != edges.Length)
                {
                    ok = false;
                    break;
                }
            }
            if (edges[edges.Length - 1].EndVertex(onThisFace) != toVertex) ok = false;
            if (!ok)
            {   // try the other way round
                Vertex v = fromVertex;
                fromVertex = toVertex;
                toVertex = v;
                sorted = new List<Edge>(edges);
                current = fromVertex;
                for (int ind = 0; ind < edges.Length; ind++)
                {
                    for (int i = 0; i < sorted.Count; i++)
                    {
                        if (sorted[i].StartVertex(onThisFace) == current)
                        {
                            edges[ind] = sorted[i];
                            current = sorted[i].EndVertex(onThisFace);
                            sorted.RemoveAt(i);
                            break;
                        }
                    }
                    if (sorted.Count + ind + 1 != edges.Length) return false; // Unable to sort edges
                }
                if (edges[edges.Length - 1].EndVertex(onThisFace) != toVertex) return false; //Unable to sort edges
            }
            return true;
        }

        /// <summary>
        /// Try to make a Face from the provided objects, using the curves to make a planar face
        /// </summary>
        /// <param name="sel"></param>
        /// <returns></returns>
        internal static Face MakeFace(GeoObjectList list)
        {
            list.DecomposeAll();
            CompoundShape cs = CompoundShape.CreateFromList(list, Precision.eps, out Plane commonplane);
            if (cs != null)
            {
                if (cs.SimpleShapes.Length == 1 && cs.SimpleShapes[0].Area > Precision.eps)
                {
                    return Face.MakeFace(new PlaneSurface(commonplane), cs.SimpleShapes[0]);
                }
            }
            return null;
        }

        /// <summary>
        /// If there is a outline (or hole) consisting of a single curve (e.g. closed circle) then split this Edge into two parts
        /// </summary>
        internal void SplitSingleOutlines()
        {
            if (outline.Length == 1)
            {
                outline[0].Split(0.5); // is automatically replaced in Face(s)
            }
            for (int i = 0; i < holes.Length; i++)
            {
                if (holes[i].Length == 1)
                {
                    holes[i][0].Split(0.5);
                }
            }
        }

        private static int NextInSameLoop(int ind, List<int> loopSpan)
        {   // loopSpan contains startIndices of loops and also the last possible index+1, typically 0,a,b,c
            // which means ther loop: 1.: [0,a-1], 2: [a,b-1], 3: [b,c-1]
            int res = ind + 1;
            for (int i = 1; i < loopSpan.Count; i++)
            {
                if (loopSpan[i] == res)
                {
                    res = loopSpan[i - 1];
                    break;
                }
                if (loopSpan[i] > res) break;
            }
            return res;
        }

        private void ReduceVertices(bool force = true)
        {   // make sure that an edge ends with the same vertex as the next edge starts with.
            double minDist = double.MaxValue;
            for (int i = 0; i < outline.Length; i++)
            {
                if (outline[i].Vertex1 != outline[i].Vertex2) minDist = Math.Min(outline[i].Vertex1.Position | outline[i].Vertex2.Position, minDist);
            }
            minDist /= 2.0; // half of the smallest distance of vertices
            for (int i = 0; i < outline.Length; i++)
            {
                int next = (i + 1) % outline.Length;
                if (outline[i].EndVertex(this) != outline[next].StartVertex(this))
                {
                    if ((outline[i].EndVertex(this).Position | outline[next].StartVertex(this).Position) < minDist || force)
                    {
                        outline[i].EndVertex(this).MergeWith(outline[next].StartVertex(this));
                    }
                    else
                    {
                        // this should not happen, this face is invalid
                    }
                }
            }
            for (int j = 0; j < holes.Length; j++)
            {
                for (int i = 0; i < holes[j].Length; i++)
                {
                    int next = (i + 1) % holes[j].Length;
                    if (holes[j][i].EndVertex(this) != holes[j][next].StartVertex(this))
                    {
                        if ((holes[j][i].EndVertex(this).Position | holes[j][next].StartVertex(this).Position) < minDist || force)
                        {
                            holes[j][i].EndVertex(this).MergeWith(holes[j][next].StartVertex(this));
                        }
                        else
                        {
                            // this should not happen, this face is invalid
                        }
                    }
                }
            }
        }

        internal Face[] SplitUv(GeoPoint2D uv1)
        {
            List<Face> res = new List<Face>();
            BoundingRect ext = Area.GetExtent();
            ext.Inflate(ext.Size * 0.01);
            // Border.Position pos = Area.GetPosition(uv1);
            Line2D l2d = new Line2D(new GeoPoint2D(ext.Left, uv1.y), new GeoPoint2D(ext.Right, uv1.y));
            Border bdr = new Border(l2d);
            CompoundShape cs = Area.Split(bdr);
            l2d = new Line2D(new GeoPoint2D(uv1.x, ext.Bottom), new GeoPoint2D(uv1.x, ext.Top));
            bdr = new Border(l2d);
            cs = cs.Split(bdr);
            for (int i = 0; i < cs.SimpleShapes.Length; i++)
            {
                res.Add(Face.MakeFace(surface, cs.SimpleShapes[i]));
            }
            return res.ToArray();
        }

        private static void Connect3dCurves(ICurve crv1, bool forward1, ICurve crv2, bool forward2)
        {
            if (crv1 is Ellipse && crv2 is Ellipse) return; // maybe some special ellipse modification?
                                                            // it is difficult to manipulate endpoints of arcs, because there are so many degrees of freedom, what you can change
            GeoPoint p1;
            if (forward1) p1 = crv1.EndPoint;
            else p1 = crv1.StartPoint;
            GeoPoint p2;
            if (forward2) p2 = crv2.StartPoint;
            else p2 = crv2.EndPoint;
            if ((p1 | p2) == 0.0) return;
            if (crv1 is Ellipse)
            {
                if (forward2) crv2.StartPoint = p1;
                else crv2.EndPoint = p1;
            }
            else if (crv2 is Ellipse)
            {
                if (forward1) crv1.EndPoint = p2;
                else crv1.StartPoint = p2;
            }
            else
            {   // manipulate both endpoints
                GeoPoint p = new GeoPoint(p1, p2); // the middle point
                if (forward1) crv1.EndPoint = p;
                else crv1.StartPoint = p;
                if (forward2) crv2.StartPoint = p;
                else crv2.EndPoint = p;
            }
        }

        private static void SetPeriodicStartPoint(double uperiod, double vperiod, ICurve2D crv2d, GeoPoint2D p)
        {
            GeoPoint2D sp = crv2d.StartPoint;
            if (uperiod > 0)
            {
                while (p.x - uperiod / 2 > sp.x) p.x -= uperiod;
                while (p.x + uperiod / 2 < sp.x) p.x += uperiod;
            }
            if (vperiod > 0)
            {
                while (p.y - vperiod / 2 > sp.y) p.y -= vperiod;
                while (p.y + vperiod / 2 < sp.y) p.y += vperiod;
            }
            crv2d.StartPoint = p;
        }
        private static void SetPeriodicEndPoint(double uperiod, double vperiod, ICurve2D crv2d, GeoPoint2D p)
        {
            GeoPoint2D ep = crv2d.EndPoint;
            if (uperiod > 0)
            {
                while (p.x - uperiod / 2 > ep.x) p.x -= uperiod;
                while (p.x + uperiod / 2 < ep.x) p.x += uperiod;
            }
            if (vperiod > 0)
            {
                while (p.y - vperiod / 2 > ep.y) p.y -= vperiod;
                while (p.y + vperiod / 2 < ep.y) p.y += vperiod;
            }
            crv2d.EndPoint = p;
        }

        /// <summary>
        /// Creates a face by specification of the geometrical surface and the outline in the parametric space
        /// on that surface.
        /// </summary>
        /// <param name="surface">The surface</param>
        /// <param name="outline">The outline</param>
        /// <returns>The created face</returns>
        public static Face MakeFace(ISurface surface, SimpleShape outline)
        {
            Face res = Face.Construct();
            res.SetSurface(surface);
            Path2D p2d = outline.Outline.AsPath();
            p2d.Flatten();
            res.surface.SetBounds(p2d.GetExtent());

            res.outline = new Edge[p2d.SubCurvesCount];
            for (int i = 0; i < p2d.SubCurvesCount; ++i)
            {
                res.outline[i] = Edge.MakeEdge(res, surface, p2d.SubCurves[i]);
                if (res.outline[i].Curve3D != null)
                {
                    if (i > 0) res.outline[i].Vertex1 = res.outline[i - 1].Vertex2;
                    else res.outline[i].Vertex1 = new Vertex(res.outline[i].Curve3D.StartPoint);
                    if (i < p2d.SubCurvesCount - 1) res.outline[i].Vertex2 = new Vertex(res.outline[i].Curve3D.EndPoint);
                    else res.outline[i].Vertex2 = res.outline[0].Vertex1;
                }
                else
                {
                    res.outline[i].Vertex1 = res.outline[i].Vertex2 = new Vertex(surface.PointAt(p2d.SubCurves[i].StartPoint));
                }
                res.outline[i].Vertex1.AddEdge(res.outline[i], p2d.SubCurves[i].StartPoint);
                res.outline[i].Vertex2.AddEdge(res.outline[i], p2d.SubCurves[i].EndPoint);
            }
            res.holes = new Edge[outline.NumHoles][];
            for (int i = 0; i < outline.NumHoles; ++i)
            {
                p2d = outline.Holes[i].AsPath();
                // this 2d path is counterclockwise, because all borders are counterclockwise. It is a design error, that in a SimpleShape the holes are counterclockwise
                // this is why we must reverse it here
                p2d = p2d.CloneReverse(true) as Path2D;
                res.holes[i] = new Edge[p2d.SubCurvesCount];
                for (int j = 0; j < p2d.SubCurvesCount; ++j)
                {
                    res.holes[i][j] = Edge.MakeEdge(res, surface, p2d.SubCurves[j]);
                    if (res.holes[i][j].Curve3D != null)
                    {
                        if (j > 0) res.holes[i][j].Vertex1 = res.holes[i][j - 1].Vertex2;
                        else res.holes[i][j].Vertex1 = new Vertex(res.holes[i][j].Curve3D.StartPoint);
                        if (j < p2d.SubCurvesCount - 1) res.holes[i][j].Vertex2 = new Vertex(res.holes[i][j].Curve3D.EndPoint);
                        else res.holes[i][j].Vertex2 = res.holes[i][0].Vertex1;
                    }
                    else
                    {
                        res.holes[i][j].Vertex1 = res.holes[i][j].Vertex2 = new Vertex(surface.PointAt(p2d.SubCurves[j].StartPoint));
                    }
                    res.holes[i][j].Vertex1.AddEdge(res.holes[i][j], p2d.SubCurves[j].StartPoint);
                    res.holes[i][j].Vertex2.AddEdge(res.holes[i][j], p2d.SubCurves[j].EndPoint);
                }
            }
            res.orientedOutward = true;
            return res;
        }

        public static Face MakeFace(ISurface surface, BoundingRect br)
        {
            SimpleShape ss = new SimpleShape(br.ToBorder());
            (surface as ISurfaceImpl).usedArea = br;
            return Face.MakeFace(surface, ss);
        }

        public static Face MakeFace(ISurface surface, List<List<ICurve2D>> outlineAndHoles)
        {
            Face res = Face.Construct();
            res.SetSurface(surface);

            res.outline = new Edge[outlineAndHoles[0].Count];
            for (int i = 0; i < outlineAndHoles[0].Count; ++i)
            {
                res.outline[i] = Edge.MakeEdge(res, surface, outlineAndHoles[0][i]);
            }
            res.holes = new Edge[outlineAndHoles.Count - 1][];
            for (int i = 1; i < outlineAndHoles.Count; ++i)
            {
                res.holes[i - 1] = new Edge[outlineAndHoles[i].Count];
                for (int j = 0; j < outlineAndHoles[i].Count; ++j)
                {
                    res.holes[i - 1][j] = Edge.MakeEdge(res, surface, outlineAndHoles[i][j]);
                }
            }
            res.orientedOutward = true;
            return res;
        }
        internal static Face MakeFace(ISurface surface, List<Edge[]> edges, Face toReplace)
        {
            Face res = Face.Construct();
            res.SetSurface(surface);
            // die Kanten beziehen sich auf das neue Face, sie müssen sich vorher auf toReplace bezogen haben
            for (int j = 0; j < edges.Count; j++)
            {
                for (int i = 0; i < edges[j].Length; i++)
                {
                    edges[j][i].ReplaceFace(toReplace, res);
                }
            }
            res.outline = edges[0]; // Area?
            res.holes = new Edge[edges.Count - 1][];
            for (int i = 1; i < edges.Count; i++)
            {
                res.holes[i - 1] = edges[i];
            }
            res.orientedOutward = true;
            SimpleShape forceArea = res.Area;
            res.Orient();
            return res;
        }
        /// <summary>
        /// Creates a face by connecting curves. The objects in <paramref name="bounds"/> must describe a closed border
        /// which may contain some holes.
        /// </summary>
        /// <param name="bounds"></param>
        /// <returns></returns>
        public static Face MakePlanarFace(IGeoObject[] bounds)
        {
            Plane pln;
            CompoundShape cs = CompoundShape.CreateFromList(new GeoObjectList(bounds), Precision.eps, out pln);
            if (cs == null || cs.SimpleShapes.Length != 1) return null;
            PlaneSurface ps = new PlaneSurface(pln);
            return MakeFace(ps, cs.SimpleShapes[0]);
        }
        protected virtual void SetSurface(ISurface surface)
        {   // nur intern zu verwenden
            this.surface = surface;
            extent = BoundingCube.EmptyBoundingCube;
        }
        protected virtual void SetArea(SimpleShape outline)
        {
            Path2D p2d = outline.Outline.AsPath();
            p2d.Flatten();
            this.outline = new Edge[p2d.SubCurvesCount];
            for (int i = 0; i < p2d.SubCurvesCount; ++i)
            {
                this.outline[i] = Edge.MakeEdge(this, surface, p2d.SubCurves[i]);
            }
            this.holes = new Edge[outline.NumHoles][];
            for (int i = 0; i < outline.NumHoles; ++i)
            {
                p2d = outline.Holes[i].AsPath();
                this.holes[i] = new Edge[p2d.SubCurvesCount];
                for (int j = 0; j < p2d.SubCurvesCount; ++j)
                {
                    this.holes[i][j] = Edge.MakeEdge(this, surface, p2d.SubCurves[j]);
                }
            }
        }
        public void Set(ISurface surface, Edge[][] edges, bool checkOutline = false)
        {
            this.surface = surface;
            double uperiod = 0.0, vperiod = 0.0;
            if (surface.IsUPeriodic) uperiod = surface.UPeriod;
            if (surface.IsVPeriodic) vperiod = surface.VPeriod;
            int outlineInd = -1;
            double maxArea = 0.0;
            for (int i = 0; i < edges.Length; i++)
            {
                if (checkOutline) CheckOutlineDirection(this, edges[i], uperiod, vperiod, null);
                BoundingRect ext = BoundingRect.EmptyBoundingRect;
                for (int j = 0; j < edges[i].Length; j++)
                {
                    ext.MinMax(edges[i][j].Curve2D(this).GetExtent());
                }
                if (ext.Size > maxArea)
                {
                    maxArea = ext.Size;
                    outlineInd = i;
                }
            }
            Edge[][] holes = new Edge[edges.Length - 1][];
            for (int i = 0; i < edges.Length; i++)
            {
                if (i < outlineInd) holes[i] = edges[i];
                else if (i > outlineInd) holes[i - 1] = edges[i];
            }
            Set(surface, edges[outlineInd], holes, false);
        }

        internal void Set(ISurface surface, List<Edge> outline, List<List<Edge>> holes, bool sortEdges = false)
        {
            Edge[][] holesa = new Edge[holes.Count][];
            for (int i = 0; i < holes.Count; i++)
            {
                holesa[i] = holes[i].ToArray();
            }
            Set(surface, outline.ToArray(), holesa, sortEdges);
        }
        internal void Set(ISurface surface, Edge[] outline, Edge[][] holes, bool sortEdges = false)
        {
            if (sortEdges)
            {
                List<Edge> le = new List<Edge>();
                le.Add(outline[0]);
                while (true)
                {
                    Vertex ve = le[le.Count - 1].EndVertex(this);
                    bool done = false;
                    for (int i = 0; i < outline.Length; i++)
                    {
                        if (outline[i].StartVertex(this) == ve)
                        {
                            if (le[0] == outline[i]) done = true;
                            else le.Add(outline[i]);
                            break;
                        }
                    }
                    if (done) break;
                }
                outline = le.ToArray();
            }
#if DEBUG
            for (int i = 0; i < outline.Length; i++)
            {
                int i1 = (i + 1) % outline.Length;
                // System.Diagnostics.Debug.Assert(outline[i].EndVertex(this) == outline[i1].StartVertex(this) || Precision.IsEqual(outline[i].EndVertex(this).Position, outline[i1].StartVertex(this).Position));
            }
#endif
            // Changing? wird nur als Nachbrenner nach leerer Konstruktion verwendet
            this.surface = surface;
            this.outline = outline;
            if (holes == null) this.holes = new Edge[0][];
            else this.holes = holes;
            foreach (Edge e in Edges)
            {
                e.Owner = this;
            }
            extent = BoundingCube.EmptyBoundingCube;
            orientedOutward = true;
            if (sortEdges)
            {
                double uperiod = 0.0, vperiod = 0.0;
                if (surface.IsUPeriodic) uperiod = surface.UPeriod;
                if (surface.IsVPeriodic) vperiod = surface.VPeriod;
                MakeAreaFromSortedEdges(uperiod, vperiod);
            }
            foreach (Edge edge in Edges)
            {
                if (edge.Curve3D is IGeoObject go) go.Style = EdgeStyle;
            }
            if (surface is ISurfaceImpl si) si.usedArea = Domain;
        }
        internal static Face MakeFace(ISurface surface, Edge[] outline)
        {
            Face res = Face.Construct();
            res.surface = surface;
            double uperiod = 0.0, vperiod = 0.0;
            if (surface.IsUPeriodic) uperiod = surface.UPeriod;
            if (surface.IsVPeriodic) vperiod = surface.VPeriod;
            BoundingRect domain = BoundingRect.EmptyBoundingRect;
            for (int i = 0; i < outline.Length; i++)
            {
                ICurve2D c2d = surface.GetProjectedCurve(outline[i].Curve3D, 0.0);
                if (i > 0) SurfaceHelper.AdjustPeriodic(surface, domain, c2d);
                domain.MinMax(c2d.GetExtent());
                if (outline[i].PrimaryFace == null)
                {
                    outline[i].SetPrimary(res, c2d, true);
                }
                else
                {
                    outline[i].SetSecondary(res, c2d, true);
                }
            }
            CheckOutlineDirection(res, outline, uperiod, vperiod, null);
            res.outline = outline;
            res.holes = new Edge[0][]; // keine Löcher
            SimpleShape forceArea = res.Area; // das SimpleShape wird hier erstmalig berechnet
            foreach (Edge edge in res.AllEdgesIterated())
            {
                if (edge.Curve3D is IGeoObject go) go.Style = EdgeStyle;
            }
            if (res.surface is ISurfaceImpl si) si.usedArea = res.Domain;

            return res;
        }
        internal static Face MakeFace(ISurface surface, Edge[][] outlineAndHoles)
        {
            Face res = Face.Construct();
            res.surface = surface;
            double uperiod = 0.0, vperiod = 0.0;
            if (surface.IsUPeriodic) uperiod = surface.UPeriod;
            if (surface.IsVPeriodic) vperiod = surface.VPeriod;
            for (int j = 0; j < outlineAndHoles.Length; j++)
            {
                for (int i = 0; i < outlineAndHoles[j].Length; i++)
                {
                    if (outlineAndHoles[j][i].PrimaryFace == null)
                    {
                        outlineAndHoles[j][i].SetPrimary(res, surface.GetProjectedCurve(outlineAndHoles[j][i].Curve3D, 0.0), true);
                    }
                    else
                    {
                        outlineAndHoles[j][i].SetSecondary(res, surface.GetProjectedCurve(outlineAndHoles[j][i].Curve3D, 0.0), true);
                    }
                }
                CheckOutlineDirection(res, outlineAndHoles[j], uperiod, vperiod, null);
            }
            if (outlineAndHoles.Length == 1)
            {
                res.outline = outlineAndHoles[0];
                res.holes = new Edge[0][]; // keine Löcher
            }
            else
            {
                double maxSize = 0.0;
                int maxInd = -1;
                for (int i = 0; i < outlineAndHoles.Length; i++)
                {
                    BoundingRect ext = BoundingRect.EmptyBoundingRect;
                    for (int j = 0; j < outlineAndHoles[i].Length; j++)
                    {
                        ext.MinMax(outlineAndHoles[i][j].Curve2D(res).GetExtent());
                    }
                    if (ext.Size > maxSize)
                    {
                        maxSize = ext.Size;
                        maxInd = i;
                    }
                }
                res.outline = outlineAndHoles[maxInd];
                res.holes = new Edge[outlineAndHoles.Length - 1][];
                for (int i = 0; i < outlineAndHoles.Length; i++)
                {
                    if (i < maxInd) res.holes[i] = outlineAndHoles[i];
                    if (i > maxInd) res.holes[i - 1] = outlineAndHoles[i];
                }
            }
            SimpleShape forceArea = res.Area; // das SimpleShape wird hier erstmalig berechnet
            if (res.surface is ISurfaceImpl si) si.usedArea = res.Domain;
            return res;
        }
        internal void SetSurfaceAndEdges(ISurface surface, Edge[] outline)
        {
            this.surface = surface;
            double uperiod = 0.0, vperiod = 0.0;
            if (surface.IsUPeriodic) uperiod = surface.UPeriod;
            if (surface.IsVPeriodic) vperiod = surface.VPeriod;
            CheckOutlineDirection(this, outline, uperiod, vperiod, null);
            orientedOutward = true; // wird von Shell.GetSimpleOffset so erwartet
            this.outline = outline;
            this.holes = new Edge[0][]; // keine Löcher
            SimpleShape forceArea = Area; // das SimpleShape wird hier erstmalig berechnet
            if (surface is ISurfaceImpl si) si.usedArea = Domain;
        }

        internal void CheckPeriodic()
        {
            // Surface und edges sind bereits gesetzt, aber die 2d Kurven können periodisch versetzt sein
            // entnommen aus der Berechnung von Area, nur FromUnorientedList wird hier auch periodisch getestet
            double uperiod = 0.0;
            double vperiod = 0.0;
            if (surface.IsUPeriodic) uperiod = surface.UPeriod;
            if (surface.IsVPeriodic) vperiod = surface.VPeriod;
            BoundingRect ext = BoundingRect.EmptyBoundingRect;
            List<ICurve2D> ls = new List<ICurve2D>();
            ICurve2D[] segments = new ICurve2D[outline.Length];
            for (int i = 0; i < outline.Length; ++i)
            {
                segments[i] = outline[i].Curve2D(this, segments);
                if (segments[i] != null) ls.Add(segments[i]);
                ext.MinMax(segments[i].GetExtent());
            }
            segments = ls.ToArray();
#if DEBUG
            DebuggerContainer dc = new DebuggerContainer();
            for (int i = 0; i < segments.Length; i++)
            {
                dc.Add(segments[i], System.Drawing.Color.Blue, i);
            }
#endif
            // kommen null-Linien in den Segments überhaupt vor?
            // für nach dem Urlaub: bei 1427 in program.cdb wird hier die Richtung eines Splines nicht
            // umgedreht sondern der Endpunkt gesetzt und das ist falsch!!!
            Border boutline = Border.FromUnorientedList(segments, true, uperiod, vperiod, ext.Left, ext.Right, ext.Bottom, ext.Top);
            // FromUnorientedList dreht u.U. die Liste um, das muss natürlich auch in der Reihenfolge der Outlines
            // berücksichtigt werden
            if (outline.Length > 1)
            {
                if (boutline.Segments[0] == segments[segments.Length - 1])
                {
                    Array.Reverse(outline);
                    Set<Edge> exchanged = new Set<Edge>();
                    // ganz vertrackt: bei periodic edges wird immer eine bestimmte 2D Curve zuerst geliefert
                    // auch das muss umgedreht werden.
                    for (int i = 0; i < outline.Length; ++i)
                    {
                        if (outline[i].IsPeriodicEdge && !exchanged.Contains(outline[i]))
                        {
                            outline[i].ExchangeFaces();
                            exchanged.Add(outline[i]); // nur einmal umdrehen
                        }
                    }
                }
            }
            List<Border> prholes = new List<Border>();
            for (int i = 0; i < holes.Length; ++i)
            {
                ICurve2D[] holecurves = new ICurve2D[holes[i].Length];
                for (int j = 0; j < holes[i].Length; j++)
                {
                    holecurves[j] = holes[i][j].Curve2D(this, holecurves);
                    holecurves[j].UserData.Add("CADability.Edge", holes[i][j]); // mal versuchsweise die zugehörige Kante merken
                }
                Border hole = Border.FromUnorientedList(holecurves, true, uperiod, vperiod, ext.Left, ext.Right, ext.Bottom, ext.Top);
                if (hole != null)
                {   // die Löcher können jetzt hier periodisch versetzt außerhalb von boutline liegen
                    // un müssen hier ggf. noch verschoben werden
                    if (uperiod != 0.0 || vperiod != 0.0)
                    {
                        double moveu = 0.0, movev = 0.0;
                        if (uperiod != 0.0)
                        {
                            if (boutline.GetPosition(hole.StartPoint) == Border.Position.Outside)
                            {
                                GeoPoint2D tst = hole.StartPoint;
                                tst.x -= uperiod;
                                if (boutline.GetPosition(tst) == Border.Position.Inside)
                                {
                                    moveu = -uperiod;
                                }
                                else
                                {
                                    tst.x = hole.StartPoint.x + uperiod;
                                    if (boutline.GetPosition(tst) == Border.Position.Inside)
                                    {
                                        moveu = uperiod;
                                    }
                                }
                            }
                        }
                        if (vperiod != 0.0)
                        {
                            if (boutline.GetPosition(hole.StartPoint) == Border.Position.Outside)
                            {
                                GeoPoint2D tst = hole.StartPoint;
                                tst.y -= vperiod;
                                if (boutline.GetPosition(tst) == Border.Position.Inside)
                                {
                                    movev = -vperiod;
                                }
                                else
                                {
                                    tst.y = hole.StartPoint.y + vperiod;
                                    if (boutline.GetPosition(tst) == Border.Position.Inside)
                                    {
                                        movev = vperiod;
                                    }
                                }
                            }
                        }
                        if (moveu != 0.0 || movev != 0.0)
                        {
                            hole.Move(moveu, movev); // die Kurven sind echt in hole, deshalb genügt es hole zu verschieben
                        }
                    }
                    if (hole.Segments[0] != holecurves[0]) Array.Reverse(holes[i]); // wenn FromUnorientedList die Reihenfolge umgedreht hat, dann müssen wir auch umdrehen
                    prholes.Add(hole);
                }
            }
            area = new SimpleShape(boutline, prholes.ToArray()).Clone();
            // Die Methode FromUnorientedList hat nun alle segmente linksrum orientiert.
            // wir wollen aber die Löcher rechtsrum haben und drehen jetzt die 2d Kurven der Löcher um
            // area ist ja ein Clone und wird dadurch nicht verändert
            for (int i = 0; i < holes.Length; ++i)
            {
                ICurve2D[] holecurves = new ICurve2D[holes[i].Length];
                // holecurves wird noch benötigt wg. Saumkanten
                for (int j = 0; j < holes[i].Length; j++)
                {
                    holecurves[j] = holes[i][j].Curve2D(this, holecurves);
                    holecurves[j].Reverse();
                }
                Array.Reverse(holes[i]);
            }
        }

        internal void RemoveEdge(Edge edge, bool mayHaveDifferntVertices = false)
        {
            if (!mayHaveDifferntVertices && edge.Vertex1 != edge.Vertex2) return;
            // only to remove edges with identical start- and endvertices and small length
            for (int i = 0; i < outline.Length; i++)
            {
                if (outline[i] == edge)
                {
                    List<Edge> l = new List<Edge>(outline);
                    l.RemoveAt(i);
                    outline = l.ToArray();
                    return; // done
                }
            }
            for (int j = 0; j < holes.Length; j++)
            {
                for (int i = 0; i < holes[j].Length; ++i)
                {
                    if (holes[j][i] == edge)
                    {
                        List<Edge> l = new List<Edge>(holes[j]);
                        l.RemoveAt(i);
                        holes[j] = l.ToArray();
                        return; // done
                    }

                }
            }
        }
#if DEBUG
#endif
        public void FreeCachedMemory()
        {
            foreach (Edge edge in AllEdgesIterated())
            {
                edge.FreeCachedMemory();
            }
        }
        internal void InvalidateArea()
        {
            area = null;
        }
        internal void InvalidateSecondaryData()
        {
            area = null;
            ClearTriangulation();
        }
        internal static GeoObjectList SortForWire(GeoObjectList ToSort)
        {   // der WireMaker benötigt die Objete in der richtigen Reihenfolge, 
            // nicht jedoch richtig orientiert
            // noch unoptimiert, sortiert nur geschlossene
            GeoObjectList res = new GeoObjectList(ToSort.Count);
            if (ToSort.Count == 0) return res;
            GeoObjectList ToRemove = new GeoObjectList(ToSort);
            double maxdist = 1e-6; // TODO: zu verbessern mit der Ausdehnung
                                   // Ausdehnung in 3D (GeoObject, GeoObjectList)?
            int found = 0;
            GeoPoint LastEndPoint = new GeoPoint();
            GeoPoint BestPoint = new GeoPoint();
            while (found >= 0)
            {
                found = -1;
                double mindist = double.MaxValue;
                for (int i = 0; i < ToRemove.Count; ++i)
                {
                    ICurve c = ToRemove[i] as ICurve;
                    if (c != null)
                    {
                        if (res.Count == 0)
                        {
                            found = i;
                            BestPoint = c.EndPoint;
                            break;
                        }
                        else
                        {
                            double d = Geometry.Dist(LastEndPoint, c.StartPoint);
                            if (d < mindist)
                            {
                                mindist = d;
                                found = i;
                                BestPoint = c.EndPoint;
                            }
                            d = Geometry.Dist(LastEndPoint, c.EndPoint);
                            if (d < mindist)
                            {
                                mindist = d;
                                found = i;
                                BestPoint = c.StartPoint;
                            }
                        }
                    }
                }
                if (found >= 0)
                {
                    res.Add(ToRemove[found]);
                    ToRemove.Remove(found);
                    LastEndPoint = BestPoint;
                }
            }
            return res;
        }
        /// <summary>
        /// Returns the curves that result from a planar intersection of this face with the provided plane.
        /// The curves are clipped to the outline (and holes) of the face.
        /// </summary>
        /// <param name="pl">The plane to intersect with</param>
        /// <returns>Array of intersection curves</returns>
        public ICurve[] GetPlaneIntersection(PlaneSurface pl)
        {

            List<ICurve> res = new List<ICurve>();
            BoundingRect ext = Area.GetExtent();
            IDualSurfaceCurve[] int2d = surface.GetPlaneIntersection(pl, ext.Left, ext.Right, ext.Bottom, ext.Top, 1e-4);

            for (int i = 0; i < int2d.Length; ++i)
            {
                //ICurve2D dbgc2d = int2d[i].GetCurveOnSurface(pl);
                //this.Owner.Add(dbgc2d.MakeGeoObject(new Plane(pl.Location, pl.DirectionX, pl.DirectionY))); // DEBUG!!
                // this.Owner.Add(int2d[i].Curve3D as IGeoObject); // DEBUG!!
                // continue; // DEBUG
                ICurve2D c2d = int2d[i].GetCurveOnSurface(surface);
                if (c2d != null)
                {
                    // bei periodischen Flächen kann es sein, dass man die Kurve um die Periode verschieben muss
                    // um einen passenden Abschnitt zu bekommen
                    int imin = 0, imax = 1;
                    int jmin = 0, jmax = 1;
                    if (surface.IsUPeriodic)
                    {
                        BoundingRect cext = c2d.GetExtent();
                        while (cext.Left + imax * surface.UPeriod < ext.Right) ++imax;
                        while (cext.Right + (imin - 1) * surface.UPeriod > ext.Left) --imin;
                        // while (cext.Left < ext.Left + imin * surface.UPeriod) --imin;
                        // while (cext.Right > ext.Right + (imax-1) * surface.UPeriod) ++imax;
                    }
                    if (surface.IsVPeriodic)
                    {
                        BoundingRect cext = c2d.GetExtent();
                        while (cext.Bottom + jmax * surface.VPeriod < ext.Top) ++jmax;
                        while (cext.Top + (jmin - 1) * surface.VPeriod > ext.Bottom) --jmin;
                    }
                    for (int ii = imin; ii < imax; ++ii)
                    {
                        for (int jj = jmin; jj < jmax; ++jj)
                        {
                            SimpleShape areaToUse;
                            if (ii == 0 && jj == 0)
                            {
                                areaToUse = Area;
                                // per = c2d;
                            }
                            else
                            {
                                areaToUse = Area.GetModified(ModOp2D.Translate(-ii * surface.UPeriod, -jj * surface.VPeriod));
                                // per = c2d.GetModified(ModOp2D.Translate(ii * surface.UPeriod, jj * surface.VPeriod));
                            }
                            double[] clipped = areaToUse.Clip(c2d, true);
                            if (clipped.Length == 0) continue; // ganz außerhalb
                            if (clipped[0] == 0.0 && clipped[1] == 1.0)
                            {
                                res.Add(surface.Make3dCurve(c2d));
                            }
                            else
                            {
                                for (int j = 0; j < clipped.Length; j += 2)
                                {
                                    if (clipped[j + 1] > clipped[j])
                                    {
                                        ICurve2D cc2d = c2d.Trim(clipped[j], clipped[j + 1]);
                                        ICurve make3d = surface.Make3dCurve(cc2d);
                                        res.Add(make3d);
                                    }
                                    else
                                    {

                                    }
                                }
                            }
                        }
                    }
                }
                else
                {   // ungeklippt dazunehmen, when ocas keine pcurve berechnet hat
                    GeoPoint2D sp = surface.PositionOf(int2d[i].Curve3D.StartPoint);
                    GeoPoint2D ep = surface.PositionOf(int2d[i].Curve3D.EndPoint);
                    GeoPoint2D mp = surface.PositionOf(int2d[i].Curve3D.PointAt(0.5));
                    if (Area.Contains(mp, true))
                    {
                        res.Add(int2d[i].Curve3D);
                    }
                }
            }

            //PlaneSurface ps = new PlaneSurface(pl);
            //CndHlp2D.Entity2D[] pcurves;
            //CndHlp3D.Edge[] edges = CndHlp3DBuddy.IntersectWithPlane(ps.Helper as CndHlp3D.PlaneSurface, out pcurves);
            //for (int i = 0; i < edges.Length; i++)
            //{
            //    IGeoObject go = IGeoObjectImpl.FromHlp3DEdge(edges[i]);
            //    if (go is ICurve)
            //        res.Add(go as ICurve);
            //}
            // leider sind die Kanten von OCas nicht richtig geklippt. Deshalb hier nochmal die 2d
            // PCurves verwenden und am Rand klippen. Wenn klippen unnötig war, dann kann man ja die OCas edge
            // nehmen, sonst muss man erst selbst eine machen
            //for (int i = 0; i < pcurves.Length; ++i)
            //{
            //    ICurve2D c2d = GeneralCurve2D.FromCndHlp2D(pcurves[i]);
            //    double [] clipped = Area.Clip(c2d, true);
            //    //if (clipped.Length == 0) continue;
            //    if (clipped.Length == 0 || (clipped[0] == 0.0 && clipped[1] == 1.0))
            //    {
            //        IGeoObject go = IGeoObjectImpl.FromHlp3DEdge(edges[i]);
            //        if (go is ICurve) res.Add(go as ICurve);
            //    }
            //    else
            //    {
            //        for (int j = 0; j < clipped.Length; j += 2)
            //        {
            //            ICurve2D cc2d = c2d.Trim(clipped[j], clipped[j + 1]);
            //            ICurve make3d = surface.Make3dCurve(cc2d);
            //            res.Add(make3d);
            //        }
            //    }
            //}
            for (int i = 0; i < res.Count; i++)
            {
                (res[i] as IGeoObject).UserData.Add("CADability.PlaneIntersection.Face", this);
            }
            return res.ToArray();
        }
        internal void CheckPeriodicEdges()
        {
            if (surface.IsUPeriodic)
            {
                for (int i = 0; i < outline.Length; ++i)
                {
                    int previ = i - 1;
                    if (previ < 0) previ = outline.Length - 1;
                }
            }
        }
        internal SimpleShape area; // outline und holes als SimpleShape, wird nur bei Bedarf erzeugt
        private void MakeArea()
        {
            // in public SimpleShape Area there is an old, weird algorithm, probably due to some bugs when importing step files with the old OpenCascade step import
            // When the edges are correct (end there are no seam edges), there should be no adjustments necessary to create the SimpeShape
            ICurve2D[] soutline = new ICurve2D[outline.Length];
            for (int i = 0; i < outline.Length; i++)
            {
                soutline[i] = outline[i].Curve2D(this).Clone();
            }
            Border boutline = new Border(soutline);
            Border[] bholes = new Border[holes.Length];
            // unfortunately there is a design bug: Borders are always counterclockwise. The holes should be clockwise. This must be changed some day...
            for (int i = 0; i < holes.Length; ++i)
            {
                int n = holes[i].Length;
                ICurve2D[] holecurves = new ICurve2D[n];
                for (int j = 0; j < n; j++)
                {   // order in the array will be reverse
                    holecurves[n - 1 - j] = holes[i][j].Curve2D(this).CloneReverse(true); // for the Border we have to clone this curve, because it will be reversed
                }
                bholes[i] = new Border(holecurves);
            }
            area = new SimpleShape(boutline, bholes);
        }
        /// <summary>
        /// Returns the two dimensional shape of the outline of this face in the parametric (u/v) space of the surface.
        /// </summary>
        public SimpleShape Area
        {   // This is old code, which had to do a lot with incorrect imported faces from OpenCascade and with seam edges, which don't exist any more
            // And it has to deal with a design error: all borders are counterclockwise oriented. 
            // But in the 2d system of a face, the holes are clockwise. In a SimpleShape the holes are borders and thus counterclockwise (which was a bad idea)
            // Now we would need a new kind of border, which has no orientation preference and a new class SimpleShape, which has counterclockwise outline and clockwise holes.
            get
            {
                if (area == null)
                {   // everything should be correct oriented and thus it is straight forward to construct the SimpleShape

                    ICurve2D[] clonedOutline = new ICurve2D[outline.Length];
                    for (int i = 0; i < outline.Length; i++)
                    {
                        clonedOutline[i] = outline[i].Curve2D(this).Clone();
                    }
                    Border soutline = new Border(out bool reversed, clonedOutline);
                    bool ok = !reversed;
                    int hl = 0;
                    if (holes != null) hl = holes.Length;
                    Border[] sholes = new Border[hl];
                    for (int i = 0; i < hl; i++)
                    {
                        ICurve2D[] clondeHole = new ICurve2D[holes[i].Length];
                        for (int j = 0; j < holes[i].Length; j++)
                        {
                            clondeHole[holes[i].Length - j - 1] = holes[i][j].Curve2D(this).CloneReverse(true);
                        }
                        sholes[i] = new Border(out reversed, clondeHole);
                        if (reversed) ok = false;
                    }
                    if (ok) area = new SimpleShape(soutline, sholes);   // the area has clones of the curves, because the holes are reverse oriented to the 2d curves of the face
                    // it should always be OK here, if not, something went wrong with the construction of the face and should be fixed there
                }
                if (area == null)
                {
                    // periodic ist hier bereits erledigt (von wem???)
                    List<ICurve2D> ls = new List<ICurve2D>();
                    ICurve2D[] segments = new ICurve2D[outline.Length];
                    for (int i = 0; i < outline.Length; ++i)
                    {
                        segments[i] = outline[i].Curve2D(this, segments);
                        if (segments[i] != null) ls.Add(segments[i]);
                        // segments[i].UserData.Add("CADability.Edge", outline[i]); // mal versuchsweise die zugehörige Kante merken
                    }
                    segments = ls.ToArray();
                    if (surface is ISurfaceImpl && ((surface as ISurfaceImpl).usedArea.IsEmpty() || (surface as ISurfaceImpl).usedArea.IsInfinite))
                    {
                        BoundingRect ext = BoundingRect.EmptyBoundingRect;
                        for (int i = 0; i < segments.Length; i++)
                        {
                            ext.MinMax(segments[i].GetExtent());
                        }
                        (surface as ISurfaceImpl).usedArea = ext;
                    }
#if DEBUG
                    //DebuggerContainer dc = new DebuggerContainer();
                    //dc.Add(segments);
#endif

                    // kommen null-Linien in den Segments überhaupt vor?
                    // Border boutline = Border.FromOrientedList(segments);
                    Border boutline = null;
                    if (boutline == null)
                    {
                        boutline = Border.FromOrientedList(segments); // 1. Versuch: schon richtig orientiert
                        bool testReverse = false;
                        if (boutline == null)
                        {
                            boutline = Border.FromUnorientedList(segments, true); // 2. Versuch: sortieren
                                                                                  // FromUnorientedList dreht u.U. die Liste um, das muss natürlich auch in der Reihenfolge der Outlines
                                                                                  // berücksichtigt werden
                            testReverse = true;
                        }
                        if (boutline == null) return null;
                        if (outline.Length > 1)
                        {
                            if (boutline.Segments[0] == segments[segments.Length - 1])
                            {
                                Array.Reverse(outline);
                                Set<Edge> exchanged = new Set<Edge>();
                                // ganz vertrackt: bei periodic edges wird immer eine bestimmte 2D Curve zuerst geliefert
                                // auch das muss umgedreht werden.
                                for (int i = 0; i < outline.Length; ++i)
                                {
                                    if (outline[i].IsPeriodicEdge && !exchanged.Contains(outline[i]))
                                    {
                                        outline[i].ExchangeFaces();
                                        exchanged.Add(outline[i]); // nur einmal umdrehen
                                    }
                                }
                            }
                        }
                    }
                    List<Border> prholes = new List<Border>();
                    for (int i = 0; i < holes.Length; ++i)
                    {
                        ICurve2D[] holecurves = new ICurve2D[holes[i].Length];
                        for (int j = 0; j < holes[i].Length; j++)
                        {
                            holecurves[j] = holes[i][j].Curve2D(this, holecurves);
                            holecurves[j].UserData.Add("CADability.Edge", holes[i][j]); // mal versuchsweise die zugehörige Kante merken
                        }
                        //Border hole = Border.FromOrientedList(holecurves);
                        Border hole = null;
                        bool testReverse = false;
                        if (hole == null)
                        {
                            hole = Border.FromOrientedList(holecurves); // 1. Versuch: schon richtig verbunden
                            if (hole == null)
                            {
                                hole = Border.FromUnorientedList(holecurves, true); // 2. Versuch: orientieren!
                                testReverse = true;
                            }
                        }
                        if (hole != null)
                        {
                            // hier gibt es ein Problem!!! Wann muss umgedreht werden?
                            if (hole.Segments[0] != holecurves[0]) Array.Reverse(holes[i]); // wenn FromUnorientedList die Reihenfolge umgedreht hat, dann müssen wir auch umdrehen
                            prholes.Add(hole);
                        }
                    }
                    area = new SimpleShape(boutline, prholes.ToArray()).Clone();
                    // Die Methode FromUnorientedList hat nun alle segmente linksrum orientiert.
                    // wir wollen aber die Löcher rechtsrum haben und drehen jetzt die 2d Kurven der Löcher um
                    // area ist ja ein Clone und wird dadurch nicht verändert
                    for (int i = 0; i < holes.Length; ++i)
                    {
                        ICurve2D[] holecurves = new ICurve2D[holes[i].Length];
                        // holecurves wird noch benötigt wg. Saumkanten
                        for (int j = 0; j < holes[i].Length; j++)
                        {
                            holecurves[j] = holes[i][j].Curve2D(this, holecurves);
                            holecurves[j].Reverse();
                        }
                        Array.Reverse(holes[i]);
                    }
                    surface.SetBounds(boutline.Extent);
                    try
                    {
                        foreach (Vertex v in Vertices)
                        {   // die uv Werte stimmen nicht mehr sicher. (z.B. wenn die Orientierung des faces geändert wurde)
                            // besser wäre vermutlich, für die verschiedenen Situationen "InvalidadteArea" oder "InvalidateVertices" etc. zu machen
                            v.RemovePositionOnFace(this);
                        }
                    }
                    catch (ApplicationException)
                    {
                        // conical surface v-offset
                    }
                }
                return area;
            }
        }
        /// <summary>
        /// A bounding rectangle which restricts the parametric space of the <see cref="Surface"/>.
        /// </summary>
        public BoundingRect Domain // alias for AreaExtent
        {
            get
            {
                return Area.GetExtent(); // is cached in Border
            }
        }

        /// <summary>
        /// The name of the face. 
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

        //internal SimpleShape Area alte Version
        //{
        //    get
        //    {
        //        if (area == null)
        //        {
        //            ICurve2D[] c = new ICurve2D[outline.Length];
        //            Dictionary<ICurve2D, int> usedPeriodic = new Dictionary<ICurve2D, int>();
        //            bool compactCurves = false;
        //            // hier ist doch schon alles richtigrumgedreht und weiter nix mehr zu tun
        //            for (int i = 0; i < c.Length; i++)
        //            {
        //                c[i] = outline[i].Curve2D(this,c);
        //                if (c[i] != null)
        //                {
        //                    if (outline[i].IsPeriodicEdge)
        //                    {
        //                        if (usedPeriodic.ContainsKey(c[i]))
        //                        {
        //                            usedPeriodic[c[i]] = i;
        //                            c[i] = outline[i].SecondaryCurve2D;
        //                        }
        //                        else
        //                        {
        //                            usedPeriodic[c[i]] = i;
        //                        }
        //                    }
        //                }
        //                else
        //                {
        //                    compactCurves = true;
        //                }
        //            }
        //            if (compactCurves)
        //            {   // es gibt null-Kurven, die stören bei Border.FromUnorientedList()
        //                List<ICurve2D> list = new List<ICurve2D>();
        //                for (int i = 0; i < c.Length; i++)
        //                {
        //                    if (c[i] != null)
        //                    {
        //                        list.Add(c[i]);
        //                    }
        //                }
        //                c = list.ToArray();
        //            }
        //            Border o;
        //            if (usedPeriodic.Count > 0)
        //            {   // es gibt Edges, die mehrfach vorkommen, d.h. beim Zylined, Kugel u.s.w
        //                // kommen geschlossese Nähte vor, also eine Kante in doppelter Funktion.
        //                // dabei ist der u oder v Parameter um das periodische Intervall zu versetzen
        //                // die Kurve wird hier echt verändert
        //                bool uperiodic = surface.IsUPeriodic;
        //                bool vperiodic = surface.IsVPeriodic;
        //                double uperiod = 0.0;
        //                double vperiod = 0.0;
        //                //if (uperiodic) uperiod = surface.UPeriod;
        //                //if (vperiodic) vperiod = surface.VPeriod;
        //                // die Bedingung scheint nicht immer zu genügen, deshalb vorläufig mal ohne:
        //                uperiod = surface.UPeriod;
        //                vperiod = surface.VPeriod;
        //                double umin, umax, vmin, vmax;
        //                this.GetUVBounds(out umin, out umax, out vmin, out vmax);
        //                o = Border.FromUnorientedList(c, true, uperiod, vperiod, umin, umax, vmin, vmax);
        //            }
        //            else
        //            {
        //                o = Border.FromUnorientedList(c, true);
        //            }
        //            List<Border> prholes = new List<Border>();
        //            for (int i = 0; i < holes.Length; ++i)
        //            {
        //                List<ICurve2D> holecurves = new List<ICurve2D>();
        //                for (int j = 0; j < holes[i].Length; j++)
        //                {
        //                    holecurves.Add(holes[i][j].Curve2D(this, holecurves.ToArray()));
        //                }
        //                Border hole = Border.FromUnorientedList(holecurves.ToArray(), true);
        //                prholes.Add(hole);
        //            }
        //            area = new SimpleShape(o, prholes.ToArray());
        //        }
        //        return area;
        //    }
        //}
        private static double CurveDist(ICurve2D c1, ICurve2D c2, double du, double dv)
        {   // wie gut hängen die beiden Kurven aneinander
            GeoVector2D offset = new GeoVector2D(du, dv);
            double d1 = Geometry.Dist(c1.StartPoint, c2.StartPoint + offset);
            double d2 = Geometry.Dist(c1.StartPoint, c2.EndPoint + offset);
            double d3 = Geometry.Dist(c1.EndPoint, c2.StartPoint + offset);
            double d4 = Geometry.Dist(c1.EndPoint, c2.EndPoint + offset);
            return Math.Min(Math.Min(d1, d2), Math.Min(d3, d4));
        }
        /// <summary>
        /// Returns all edges that surround this face (also the holes in this face)
        /// </summary>
        public Edge[] AllEdges
        {
            get
            {
                List<Edge> res = new List<Edge>(outline);
                for (int i = 0; i < holes.Length; i++)
                {
                    res.AddRange(holes[i]);
                }
                return res.ToArray();
            }
        }
        [Obsolete("renamed to Edges")]
        public IEnumerable<Edge> AllEdgesIterated()
        {
            for (int i = 0; i < outline.Length; ++i)
            {
                yield return outline[i];
            }
            for (int i = 0; i < holes.Length; i++)
            {
                for (int j = 0; j < holes[i].Length; ++j)
                {
                    yield return holes[i][j];
                }
            }
        }
        public IEnumerable<Edge> Edges
        {
            get
            {
                for (int i = 0; i < outline.Length; ++i)
                {
                    yield return outline[i];
                }
                for (int i = 0; i < holes.Length; i++)
                {
                    for (int j = 0; j < holes[i].Length; ++j)
                    {
                        yield return holes[i][j];
                    }
                }
            }
        }
        public Set<Edge> AllEdgesSet
        {
            get
            {
                Set<Edge> res = new Set<Edge>(outline);
                for (int i = 0; i < holes.Length; i++)
                {
                    res.AddMany(holes[i]);
                }
                return res;
            }
        }

        internal int AllEdgesCount
        {
            get
            {
                int res = outline.Length;
                for (int i = 0; i < holes.Length; i++) res += holes[i].Length;
                return res;
            }
        }
        /// <summary>
        /// Gets all edges that represent the outline of this face (not the holes).
        /// The order of the edges is counterclockwise when you look at the face from the outside.
        /// </summary>
        public Edge[] OutlineEdges
        {
            get
            {
                if (outline != null) return outline.Clone() as Edge[];
                else return null;
            }
        }
        /// <summary>
        /// Returns all edges that belong to the hole with the provided <paramref name="index"/>.
        /// The order of the edges is counterclockwise when you look at the face from the outside.
        /// </summary>
        /// <param name="index">Index of the hole to get</param>
        /// <returns>The requested hole</returns>
        public Edge[] HoleEdges(int index)
        {
            return holes[index].Clone() as Edge[];
        }
        /// <summary>
        /// Gets the number of holes in this face.
        /// </summary>
        public int HoleCount
        {
            get
            {
                if (holes == null) return 0;
                return holes.Length;
            }
        }
        private Vertex GetCommonVertex(Edge e1, Edge e2)
        {
            if (e1.Vertex1 == e2.Vertex1) return e1.Vertex1;
            if (e1.Vertex1 == e2.Vertex2) return e1.Vertex1;
            if (e1.Vertex2 == e2.Vertex1) return e1.Vertex2;
            if (e1.Vertex2 == e2.Vertex2) return e1.Vertex2;
            return null;
        }
        private void UseVertices(List<Vertex> toUse)
        {
            Vertex[] aToUse = toUse.ToArray();
            foreach (Edge edge in AllEdges)
            {
                edge.UseVertices(aToUse);
            }
        }
        public Vertex[] Vertices
        {
            get
            {
                if (vertices == null)
                {
                    Set<Vertex> res = new Set<Vertex>();
                    foreach (Edge edge in AllEdges)
                    {
                        edge.MakeVertices();
                        res.Add(edge.Vertex1);
                        res.Add(edge.Vertex2);
                    }
                    vertices = res.ToArray();
                }
                return vertices;
            }
        }
        public void RecalcVertices()
        {
            Set<Vertex> res = new Set<Vertex>();
            foreach (Edge edge in AllEdges)
            {
                edge.MakeVertices();
                res.Add(edge.Vertex1);
                res.Add(edge.Vertex2);
            }
            vertices = res.ToArray();
        }
        internal Vertex[] OutlineVertices
        {
            get
            {
                Set<Vertex> res = new Set<Vertex>();
                foreach (Edge edge in outline)
                {
                    edge.MakeVertices();
                    res.Add(edge.Vertex1);
                    res.Add(edge.Vertex2);
                }
                return res.ToArray();
            }
        }
        internal void Orient()
        {
            foreach (Edge edge in AllEdges)
            {
                edge.MakeVertices();
                edge.Orient();
            }
        }
        private SimpleShape[] splitTangential(GeoVector viewDirection)
        {
            double umin, umax, vmin, vmax;
            //CndHlp3DBuddy.GetUVBounds(out umin, out umax, out vmin, out vmax);
            GetUVBounds(out umin, out umax, out vmin, out vmax);
            //double numin, numax, nvmin, nvmax;
            //surface.GetNaturalBounds(out numin, out numax, out nvmin, out nvmax);
            //umin = Math.Max(umin, numin);
            //vmin = Math.Max(vmin, nvmin);
            //umax = Math.Min(umax, numax);
            //vmax = Math.Min(vmax, nvmax);
            double du = (umax - umin) * 0.001;
            double dv = (vmax - vmin) * 0.001;
            umin -= du; umax += du;
            vmin -= dv; vmax += dv; // ein bisschen vergrößern, denn sonst schneiden u.U. die Endpunkte der gefundenen Linien
                                    // die Area, und das macht Probleme
            ICurve2D[] tangents = surface.GetTangentCurves(viewDirection, umin, umax, vmin, vmax);
            if (tangents == null || tangents.Length == 0) return null;
            BoundingRect text = BoundingRect.EmptyBoundingRect;
            for (int i = 0; i < tangents.Length; i++)
            {
                text.MinMax(tangents[i].GetExtent());
            }
            BoundingRect aext = Area.GetExtent();
            if (!text.Overlaps(aext)) return null;
            CompoundShape res = null;
            for (int i = 0; i < tangents.Length; i++)
            {
                Border splitWith = new Border(tangents[i]);
                if (res == null) res = Area.Split(splitWith);
                else res = res.Split(splitWith);
            }
            return res.SimpleShapes;
        }

        internal List<Edge> FindEdges(GeoPoint2D uv)
        {   // find the edge(s) that are close to this 2d point on the surface
            List<Edge> res = new List<Edge>();
            GeoPoint p = Surface.PointAt(uv);
            foreach (Edge edg in AllEdgesIterated())
            {
                ICurve2D c2d = edg.Curve2D(this);
                double d = c2d.MinDistance(uv);
                if (d < Precision.eps || (edg.Curve3D != null && edg.Curve3D.DistanceTo(p) < Precision.eps))
                {
                    res.Add(edg);
                }
            }
            return res;
        }

        public SimpleShape GetShadow(Plane onThisPlane)
        {
            return GetShadow(onThisPlane, null);
        }

        private SimpleShape GetShadow(Plane onThisPlane, Face original)
        {
            // original: wenn ein face gesplittet wurde, ist hier das original, um die angrenzenden Faces zu finden
            BoundingRect aext = this.Area.GetExtent();
            Projection pr = new Projection(-onThisPlane.Normal, onThisPlane.DirectionY);
            if (surface.IsVanishingProjection(pr, aext.Left, aext.Right, aext.Bottom, aext.Top)) return null;
            SimpleShape[] splitted = null; // nur splitten, wenn original==null, also noch nicht gesplitted wurde
            if (original == null) splitted = splitTangential(onThisPlane.Normal);
            // es kommt vor, dass nur ein Ergebnis geliefert wird, wenn die Tangentialkante genau auf dem Rand des faces liegt
            // Dann ohne splitting arbeiten, sonst Endlosschleife
            if (original == null && splitted == null)
            {
                bool splitu = false; bool splitv = false;
                for (int i = 0; i < outline.Length; ++i)
                {
                    if (outline[i].IsPeriodicEdge && !outline[i].IsSingular())
                    {
                        if (surface.IsUPeriodic & !surface.IsVPeriodic) splitu = true;
                        else if (surface.IsVPeriodic & !surface.IsUPeriodic) splitv = true;
                        else
                        {
                            ICurve2D c2d = outline[i].Curve2D(this);
                            if (c2d != null)
                            {
                                GeoVector2D md = c2d.MiddleDirection;
                                if (Math.Abs(md.x) < Math.Abs(md.y)) splitu = true;
                                else splitv = true;
                            }
                        }
                        break;
                    }
                }
                if (splitu || splitv)
                {
                    BoundingRect br = area.GetExtent();
                    br.Inflate(br.Size * 0.001);
                    Border splitBorder = null;
                    if (splitu)
                    {
                        splitBorder = new Border(new Line2D(br.GetLowerMiddle(), br.GetUpperMiddle()));
                    }
                    else
                    {
                        splitBorder = new Border(new Line2D(br.GetMiddleLeft(), br.GetMiddleRight()));
                    }
                    CompoundShape cs = area.Split(splitBorder);
                    splitted = cs.SimpleShapes;
                }
            }
            if (splitted != null && splitted.Length > 1)
            {
                CompoundShape cres = null;
                for (int i = 0; i < splitted.Length; i++)
                {
                    Face fc = Face.MakeFace(surface, splitted[i]);
                    SimpleShape ss = fc.GetShadow(onThisPlane, this);
                    // die Kurven in ss müssen an Linien und Bögen von Zylindern und Ebenen angepasste werden
                    // Dazu die Kreisbögen und Linien in 2d bestimmen und dann BSpline2Ds abgleichen 
                    if (ss != null)
                    {
                        if (cres == null) cres = new CompoundShape(ss);
                        // else cres = CompoundShape.UnionX(cres, new CompoundShape(ss), 0.0);
                        else cres = CompoundShape.Union(cres, new CompoundShape(ss));
                    }
                }
                if (cres == null || cres.SimpleShapes.Length == 0) return null;
                int ind = -1;
                double maxArea = 0.0;
                for (int i = 0; i < cres.SimpleShapes.Length; i++)
                {   // es sollte eigentlich nur eine geben, im Zweifel, die größte liefern
                    double a = cres.SimpleShapes[i].Area;
                    if (a > maxArea)
                    {
                        maxArea = a;
                        ind = i;
                    }
                }
                if (ind >= 0) return cres.SimpleShapes[ind];
                return null;
            }


            List<ICurve2D> o2d = new List<ICurve2D>();
#if DEBUG
            DebuggerContainer dc = new DebuggerContainer();
#endif
            for (int i = 0; i < outline.Length; i++)
            {
                if (outline[i].Curve3D != null)
                {
                    ICurve2D c2d = outline[i].Curve3D.GetProjectedCurve(onThisPlane);
                    if (c2d != null && c2d.Length > Precision.eps)
                    {   // die 2d Kurven sind zwar in der richtigen Reihenfolge, aber u.U. falsch orientiert
                        // das könnte man auch mit outline[i].Forward und der normalenrichtung der surface rauskriegen
                        if (c2d is BSpline2D)
                        {
                            Face otherFace = outline[i].OtherFace(this);
                            if (otherFace == null && original != null)
                            {
                                Edge e = original.findEdgeFromCurve2D(Precision.eps * 10, outline[i].Curve2D(this));
                                if (e != null) otherFace = e.OtherFace(original);
                            }
                            if (otherFace != null && (otherFace.surface is PlaneSurface || otherFace.surface is CylindricalSurface))
                            {
                                BoundingRect oext = otherFace.Area.GetExtent();
                                if (otherFace.Surface.IsVanishingProjection(pr, oext.Left, oext.Right, oext.Bottom, oext.Top))
                                {   // die Kurve ist zwar ein BSpline, aber gleichzeitig die Kante einer Ebene oder eines Zylinders
                                    if (otherFace.surface is PlaneSurface)
                                    {
                                        c2d = new Line2D(c2d.StartPoint, c2d.EndPoint); // dann ist es eine einfache Linie
                                    }
                                    if (otherFace.surface is CylindricalSurface)
                                    {
                                        CylindricalSurface cs = (otherFace.surface as CylindricalSurface);
                                        GeoPoint2D cnt = onThisPlane.Project(cs.Location);
                                        if (cs.RadiusX == cs.RadiusY)
                                        {
                                            Arc2D a2d = new Arc2D(cnt, cs.RadiusX, c2d.StartPoint, c2d.EndPoint, true);
                                            if (Math.Abs(c2d.MinDistance(a2d.PointAt(0.5))) > cs.RadiusX / 2.0)
                                            {
                                                a2d = new Arc2D(cnt, cs.RadiusX, c2d.StartPoint, c2d.EndPoint, false);
                                            }
                                            c2d = a2d; // wird ersetzt durch diesen Kreisbogen
                                        }
                                    }
                                }
                            }
                        }
                        if (o2d.Count == 1)
                        {
                            // Orientierung herstellen
                            double d1 = o2d[0].StartPoint | c2d.StartPoint;
                            double d2 = o2d[0].StartPoint | c2d.EndPoint;
                            double d3 = o2d[0].EndPoint | c2d.StartPoint;
                            double d4 = o2d[0].EndPoint | c2d.EndPoint;
                            double d = Math.Min(Math.Min(d1, d2), Math.Min(d3, d4));
                            if (d == d1)
                            {
                                o2d[0].Reverse();
                            }
                            else if (d == d2)
                            {
                                o2d[0].Reverse();
                                c2d.Reverse();
                            }
                            else if (d == d3)
                            {
                            }
                            else if (d == d4)
                            {
                                c2d.Reverse();
                            }
                        }
                        else if (o2d.Count > 1)
                        {
                            double d3 = o2d[o2d.Count - 1].EndPoint | c2d.StartPoint;
                            double d4 = o2d[o2d.Count - 1].EndPoint | c2d.EndPoint;
                            if (d3 > d4)
                            {
                                c2d.Reverse();
                            }
                        }
                        o2d.Add(c2d);
                    }
#if DEBUG
                    if (c2d != null) dc.Add(c2d, System.Drawing.Color.Red, i);
#endif
                }
            }
            Border bo = new Border(o2d.ToArray(), true);
            List<Border> ho = new List<Border>();
            for (int i = 0; i < HoleCount; i++)
            {
                List<ICurve2D> h2d = new List<ICurve2D>();
                for (int j = 0; j < holes[i].Length; j++)
                {
                    if (holes[i][j].Curve3D != null)
                    {
                        ICurve2D c2d = holes[i][j].Curve3D.GetProjectedCurve(onThisPlane);
                        if (c2d != null)
                        {
                            if (c2d is BSpline2D)
                            {
                                Face otherFace = holes[i][j].OtherFace(this);
                                if (otherFace == null && original != null)
                                {
                                    Edge e = original.findEdgeFromCurve2D(Precision.eps * 10, holes[i][j].Curve2D(this));
                                    if (e != null) otherFace = e.OtherFace(original);
                                }
                                if (otherFace != null && (otherFace.surface is PlaneSurface || otherFace.surface is CylindricalSurface))
                                {
                                    BoundingRect oext = otherFace.Area.GetExtent();
                                    if (otherFace.Surface.IsVanishingProjection(pr, oext.Left, oext.Right, oext.Bottom, oext.Top))
                                    {   // die Kurve ist zwar ein BSpline, aber gleichzeitig die Kante einer Ebene oder eines Zylinders
                                        if (otherFace.surface is PlaneSurface)
                                        {
                                            c2d = new Line2D(c2d.StartPoint, c2d.EndPoint); // dann ist es eine einfache Linie
                                        }
                                        if (otherFace.surface is CylindricalSurface)
                                        {   // die Kurve ist ein Kreisbogen
                                            CylindricalSurface cs = (otherFace.surface as CylindricalSurface);
                                            GeoPoint2D cnt = onThisPlane.Project(cs.Location);
                                            if (cs.RadiusX == cs.RadiusY)
                                            {   // welcher der beiden Kreisbögen? Die Abfrage mit halbem radius war schlecht
                                                // es wird einfach geprüft, welcher besser passt
                                                Arc2D a2d1 = new Arc2D(cnt, cs.RadiusX, c2d.StartPoint, c2d.EndPoint, true);
                                                Arc2D a2d2 = new Arc2D(cnt, cs.RadiusX, c2d.StartPoint, c2d.EndPoint, false);
                                                if (Math.Abs(c2d.MinDistance(a2d1.PointAt(0.5))) > Math.Abs(c2d.MinDistance(a2d2.PointAt(0.5))))
                                                {
                                                    c2d = a2d2;
                                                }
                                                else
                                                {
                                                    c2d = a2d1;
                                                }
                                            }
                                        }
                                    }
                                }
                            }
                            if (h2d.Count == 1)
                            {
                                // Orientierung herstellen
                                double d1 = h2d[0].StartPoint | c2d.StartPoint;
                                double d2 = h2d[0].StartPoint | c2d.EndPoint;
                                double d3 = h2d[0].EndPoint | c2d.StartPoint;
                                double d4 = h2d[0].EndPoint | c2d.EndPoint;
                                double d = Math.Min(Math.Min(d1, d2), Math.Min(d3, d4));
                                if (d == d1)
                                {
                                    h2d[0].Reverse();
                                }
                                else if (d == d2)
                                {
                                    h2d[0].Reverse();
                                    c2d.Reverse();
                                }
                                else if (d == d3)
                                {
                                }
                                else if (d == d4)
                                {
                                    c2d.Reverse();
                                }
                            }
                            else if (h2d.Count > 1)
                            {
                                double d3 = h2d[h2d.Count - 1].EndPoint | c2d.StartPoint;
                                double d4 = h2d[h2d.Count - 1].EndPoint | c2d.EndPoint;
                                if (d3 > d4)
                                {
                                    c2d.Reverse();
                                }
                            }
                            h2d.Add(c2d);
                        }
                    }
                }
                Border bh = new Border(h2d.ToArray(), true);
                ho.Add(bh);
            }
            SimpleShape res = new SimpleShape(bo, ho.ToArray());
            return res;
        }

        internal void ClosePeriodic()
        {
            for (int i = 0; i < outline.Length; i++)
            {
                for (int j = i + 1; j < outline.Length; j++)
                {
                    if (outline[i].Curve3D != null && outline[i].SecondaryFace == null && outline[j].SecondaryFace == null)
                    {
                        if (outline[i].Vertex1 == outline[j].Vertex2 && outline[i].Vertex2 == outline[j].Vertex1)
                        {
                            if (outline[j].Curve3D.DistanceTo(outline[i].Curve3D.PointAt(0.5)) < Precision.eps)
                            {
                                outline[i].SetSecondary(this, outline[j].PrimaryCurve2D, outline[j].Forward(this));
                                outline[j] = outline[i];
                            }
                        }
                    }
                }
            }
        }

        internal void SubstitudeEdge(Edge oldEdge, Edge newEgde)
        {
            for (int i = 0; i < outline.Length; ++i)
            {
                if (outline[i] == oldEdge)
                {
                    outline[i] = newEgde;
                    return;
                }
            }
            for (int j = 0; j < holes.Length; ++j)
            {
                for (int i = 0; i < holes[j].Length; ++i)
                {
                    if (holes[j][i] == oldEdge)
                    {
                        holes[j][i] = newEgde;
                        return;
                    }
                }
            }
        }

        private Edge findEdgeFromCurve2D(double precision, ICurve2D c2d)
        {   // suche die Kante, die c2d entspricht. c2d kann auch nur ein Teil der Kante sein oder auch garnicht zu den Kanten gehören (aber dann nicht schneiden
            foreach (Edge edg in AllEdgesIterated())
            {
                ICurve2D e2d = edg.Curve2D(this);
                double d = Math.Abs(e2d.MinDistance(c2d.StartPoint));
                if (d > precision) continue;
                d = Math.Abs(e2d.MinDistance(c2d.EndPoint));
                if (d > precision) continue;
                d = Math.Abs(e2d.MinDistance(c2d.PointAt(0.5)));
                if (d > precision) continue;
                return edg;
            }
            return null;
        }
        /// <summary>
        /// Returns the uv representations of the provided edges on the surface of this face
        /// </summary>
        /// <param name="edges"></param>
        /// <returns></returns>
        internal ICurve2D[] Get2DCurves(List<Edge> edges)
        {
            ICurve2D[] res = new ICurve2D[edges.Count];
            for (int i = 0; i < res.Length; i++)
            {
                res[i] = edges[i].Curve2D(this);
            }
            return res;
        }

#if DEBUG
        internal DebuggerContainer DebugEdges3D
        {
            get
            {
                DebuggerContainer res = new DebuggerContainer();
                for (int i = 0; i < outline.Length; ++i)
                {
                    if (outline[i].Curve3D != null) res.Add(outline[i].Curve3D as IGeoObject, outline[i].GetHashCode());
                }
                for (int j = 0; j < holes.Length; ++j)
                {
                    for (int i = 0; i < holes[j].Length; ++i)
                    {
                        if (holes[j][i].Curve3D != null) res.Add(holes[j][i].Curve3D as IGeoObject, holes[j][i].GetHashCode());
                    }
                }
                return res;
            }
        }
        internal DebuggerContainer DebugOrderedEdges3D
        {
            get
            {
                DebuggerContainer res = new DebuggerContainer();
                for (int i = 0; i < outline.Length; ++i)
                {
                    if (outline[i].Curve3D != null) res.Add(outline[i].Curve3D as IGeoObject, i);
                }
                int offset = outline.Length;
                for (int j = 0; j < holes.Length; ++j)
                {
                    for (int i = 0; i < holes[j].Length; ++i)
                    {
                        if (holes[j][i].Curve3D != null) res.Add(holes[j][i].Curve3D as IGeoObject, i + offset);
                    }
                    offset += holes[j].Length;
                }
                return res;
            }
        }
        internal DebuggerContainer DebugForwardOrientedEdges3D
        {
            get
            {
                DebuggerContainer res = new DebuggerContainer();
                for (int i = 0; i < outline.Length; ++i)
                {
                    if (outline[i].Curve3D != null)
                    {
                        if (outline[i].Forward(this)) res.Add(outline[i].Curve3D as IGeoObject, i);
                        else
                        {
                            ICurve crv = outline[i].Curve3D.Clone();
                            crv.Reverse();
                            res.Add(crv as IGeoObject, i);
                        }
                    }
                }
                int offset = outline.Length;
                for (int j = 0; j < holes.Length; ++j)
                {
                    for (int i = 0; i < holes[j].Length; ++i)
                    {
                        if (holes[j][i].Curve3D != null)
                        {
                            if (holes[j][i].Forward(this)) res.Add(holes[j][i].Curve3D as IGeoObject, i);
                            else
                            {
                                ICurve crv = holes[j][i].Curve3D.Clone();
                                crv.Reverse();
                                res.Add(crv as IGeoObject, i);
                            }
                        }
                    }
                    offset += holes[j].Length;
                }
                return res;
            }
        }

        internal DebuggerContainer DebugEdges2D
        {
            get
            {
                DebuggerContainer res = new DebuggerContainer();
                ICurve2D[] segments = new ICurve2D[outline.Length];
                for (int i = 0; i < outline.Length; ++i)
                {
                    segments[i] = outline[i].Curve2D(this, segments);
                    res.Add(segments[i], System.Drawing.Color.Red, outline[i].GetHashCode());
                }
                for (int j = 0; j < holes.Length; ++j)
                {
                    for (int i = 0; i < holes[j].Length; ++i)
                    {
                        res.Add(holes[j][i].Curve2D(this), System.Drawing.Color.Blue, holes[j][i].GetHashCode());
                    }
                }
                return res;
            }
        }
        internal DebuggerContainer DebugTriangulation
        {
            get
            {
                DebuggerContainer res = new DebuggerContainer();
                for (int i = 0; i < triangleIndex.Length; i = i + 3)
                {
                    Line2D l1 = new Line2D(triangleUVPoint[triangleIndex[i]], triangleUVPoint[triangleIndex[i + 1]]);
                    Line2D l2 = new Line2D(triangleUVPoint[triangleIndex[i + 1]], triangleUVPoint[triangleIndex[i + 2]]);
                    Line2D l3 = new Line2D(triangleUVPoint[triangleIndex[i + 2]], triangleUVPoint[triangleIndex[i]]);
                    res.Add(l1, System.Drawing.Color.Red, i);
                    res.Add(l2, System.Drawing.Color.Red, i);
                    res.Add(l3, System.Drawing.Color.Red, i);
                }
                return res;
            }
        }
        internal DebuggerContainer DebugTriangulation3D
        {
            get
            {
                DebuggerContainer res = new DebuggerContainer();
                for (int i = 0; i < triangleIndex.Length; i = i + 3)
                {
                    Line l1 = Line.TwoPoints(trianglePoint[triangleIndex[i]], trianglePoint[triangleIndex[i + 1]]);
                    Line l2 = Line.TwoPoints(trianglePoint[triangleIndex[i + 1]], trianglePoint[triangleIndex[i + 2]]);
                    Line l3 = Line.TwoPoints(trianglePoint[triangleIndex[i + 2]], trianglePoint[triangleIndex[i]]);
                    res.Add(l1, i);
                    res.Add(l2, i);
                    res.Add(l3, i);
                }
                return res;
            }
        }

        internal DebuggerContainer DebugWithNormal
        {
            get
            {
                DebuggerContainer res = new DebuggerContainer();
                double ll = this.GetExtent(0.0).Size * 0.01;
                ColorDef cd = new ColorDef("debug", System.Drawing.Color.Red);
                SimpleShape ss = Area;
                GeoPoint2D c = ss.GetExtent().GetCenter();
                GeoPoint pc = Surface.PointAt(c);
                GeoVector nc = Surface.GetNormal(c);
                Line l = Line.TwoPoints(pc, pc + ll * nc.Normalized);
                l.ColorDef = cd;
                res.Add(l);
                res.Add(this);
                return res;
            }
        }
#endif
        internal string DebugString
        {
            get
            {
                return "Face: " + hashCode.ToString();
            }
        }
        /// <summary>
        /// Returns the geometrical surface on which this face resides.
        /// </summary>
        public ISurface Surface
        {
            get
            {
                return surface;
            }
            internal set
            {
                surface = value;
                if (surface is ISurfaceImpl si && outline != null) si.usedArea = Domain;
                extent = BoundingCube.EmptyBoundingCube;
            }
        }
        internal ISurface internalSurface
        {   // nur ein zwischenzeitlicher Notbehelf, da wir hier oft Area noch nicht bestimmen können. Muss anders geregelt werden
            get
            {
                return surface;
            }
        }

        /// <summary>
        /// Returnes the minimum and maximum values of coordinates in the parametric space used by this face.
        /// </summary>
        /// <param name="umin">Minimum in u direction</param>
        /// <param name="umax">Maximum in u direction</param>
        /// <param name="vmin">Minimum in v direction</param>
        /// <param name="vmax">Maximum in v direction</param>
        public void GetUVBounds(out double umin, out double umax, out double vmin, out double vmax)
        {
            BoundingRect ext = BoundingRect.EmptyBoundingRect;
            for (int i = 0; i < outline.Length; ++i)
            {
                ICurve2D c2d = outline[i].Curve2D(this);
                if (c2d != null) ext.MinMax(c2d.GetExtent());
            }
            umin = ext.Left; umax = ext.Right;
            vmin = ext.Bottom; vmax = ext.Top;
            // nicht ocas aufrufen, geht während der Darstellung im Hintergrund nicht
            // CndHlp3DBuddy.GetUVBounds(out umin, out umax, out vmin, out vmax);
        }
        /// <summary>
        /// Returnes the minimum and maximum values of coordinates in the parametric space used by this face.
        /// </summary>
        /// <returns>Extent in parametric space (u/v)</returns>
        public BoundingRect GetUVBounds()
        {
            BoundingRect ext = BoundingRect.EmptyBoundingRect;
            for (int i = 0; i < outline.Length; ++i)
            {
                ICurve2D c2d = outline[i].Curve2D(this);
                if (c2d != null) ext.MinMax(c2d.GetExtent());
            }
            return ext;
        }
        #region IGeoObject Members
        /// <summary>
        /// Overrides <see cref="CADability.GeoObject.IGeoObjectImpl.CopyGeometry (IGeoObject)"/>
        /// </summary>
        /// <param name="ToCopyFrom"></param>
        public override void CopyGeometry(IGeoObject ToCopyFrom)
        {
            Face copyface = ToCopyFrom as Face;
            // bei einem Modify werden ja nicht die Kurven sondern nur die surface verändert
            // so muss auch nur das rückgängig gemacht werden
            using (new Changing(this))
            {
                CopyGeometryNoEdges(copyface);
                foreach (Edge edge in AllEdges)
                {
                    edge.RecalcCurve3D();
                }
            }
        }
        internal void CopyGeometryNoEdges(Face copyface)
        {
            // bei einem Modify werden ja nicht die Kurven sondern nur die surface verändert
            // so muss auch nur das rückgängig gemacht werden
            this.surface.CopyData(copyface.surface);
            extent = BoundingCube.EmptyBoundingCube;
            ClearTriangulation();
        }
        /// <summary>
        /// Overrides <see cref="CADability.GeoObject.IGeoObjectImpl.GetShowProperties (IFrame)"/>
        /// </summary>
        /// <param name="Frame"></param>
        /// <returns></returns>
        public override IPropertyEntry GetShowProperties(IFrame Frame)
        {
            return new ShowPropertyFace(this, Frame);
        }
        public Face Clone(Dictionary<Edge, Edge> clonedEdges, Dictionary<Vertex, Vertex> clonedVertices)
        {   // das kann von einer Shell aufgerufen werden, so dass die Kanten ggf nur einmal gekloned werden
            Face res = Face.Construct();
            res.surface = surface.Clone();
            res.outline = new Edge[outline.Length];
            res.orientedOutward = orientedOutward;
            ICurve2D[] alreadyUsed = new ICurve2D[outline.Length];
            for (int i = 0; i < outline.Length; ++i)
            {
                if (clonedEdges.ContainsKey(outline[i]))
                {   // die Kante gibts schon
                    // die beiden Curve2d können von der selben kante kommen, aber da sie zweimal gekloned
                    // werden, sind sie ja unabhängig
                    ICurve2D toClone = outline[i].Curve2D(this, alreadyUsed);
                    clonedEdges[outline[i]].SetSecondary(res, toClone.Clone(), outline[i].Forward(this));
                    res.outline[i] = clonedEdges[outline[i]];
                    alreadyUsed[i] = toClone;
                    // hier InterpolatedDualSurfaceCurve updaten!!!
                    if (res.outline[i].Curve3D is InterpolatedDualSurfaceCurve)
                    {
                        InterpolatedDualSurfaceCurve idsc = (res.outline[i].Curve3D as InterpolatedDualSurfaceCurve);
                        bool exchangeFaces = (bool)(idsc).UserData.GetData("Edge.Clone.ReversePrimarySecondary");
                        (res.outline[i].Curve3D as InterpolatedDualSurfaceCurve).UserData.RemoveUserData("Edge.Clone.ReversePrimarySecondary");
                        if (exchangeFaces)
                        {
                            (res.outline[i].Curve3D as InterpolatedDualSurfaceCurve).SetSurfaces(res.outline[i].PrimaryFace.internalSurface, res.outline[i].SecondaryFace.internalSurface, true);
                            res.outline[i].PrimaryCurve2D = (res.outline[i].Curve3D as InterpolatedDualSurfaceCurve).CurveOnSurface1;
                            res.outline[i].SecondaryCurve2D = (res.outline[i].Curve3D as InterpolatedDualSurfaceCurve).CurveOnSurface2;
                            if ((res.outline[i].PrimaryCurve2D.StartPoint | outline[i].SecondaryCurve2D.StartPoint) + (res.outline[i].PrimaryCurve2D.EndPoint | outline[i].SecondaryCurve2D.EndPoint) >
                                (res.outline[i].PrimaryCurve2D.StartPoint | outline[i].SecondaryCurve2D.EndPoint) + (res.outline[i].PrimaryCurve2D.EndPoint | outline[i].SecondaryCurve2D.StartPoint))
                            {
                                res.outline[i].PrimaryCurve2D.Reverse();
                            }
                            if ((res.outline[i].SecondaryCurve2D.StartPoint | outline[i].PrimaryCurve2D.StartPoint) + (res.outline[i].SecondaryCurve2D.EndPoint | outline[i].PrimaryCurve2D.EndPoint) >
                                (res.outline[i].SecondaryCurve2D.StartPoint | outline[i].PrimaryCurve2D.EndPoint) + (res.outline[i].SecondaryCurve2D.EndPoint | outline[i].PrimaryCurve2D.StartPoint))
                            {
                                res.outline[i].SecondaryCurve2D.Reverse();
                            }
                        }
                        else
                        {
                            (res.outline[i].Curve3D as InterpolatedDualSurfaceCurve).SetSurfaces(res.outline[i].PrimaryFace.internalSurface, res.outline[i].SecondaryFace.internalSurface, false);
                            res.outline[i].PrimaryCurve2D = (res.outline[i].Curve3D as InterpolatedDualSurfaceCurve).CurveOnSurface1;
                            res.outline[i].SecondaryCurve2D = (res.outline[i].Curve3D as InterpolatedDualSurfaceCurve).CurveOnSurface2;
                            if ((res.outline[i].PrimaryCurve2D.StartPoint | outline[i].PrimaryCurve2D.StartPoint) + (res.outline[i].PrimaryCurve2D.EndPoint | outline[i].PrimaryCurve2D.EndPoint) >
                                (res.outline[i].PrimaryCurve2D.StartPoint | outline[i].PrimaryCurve2D.EndPoint) + (res.outline[i].PrimaryCurve2D.EndPoint | outline[i].PrimaryCurve2D.StartPoint))
                            {
                                res.outline[i].PrimaryCurve2D.Reverse();
                            }
                            if ((res.outline[i].SecondaryCurve2D.StartPoint | outline[i].SecondaryCurve2D.StartPoint) + (res.outline[i].SecondaryCurve2D.EndPoint | outline[i].SecondaryCurve2D.EndPoint) >
                                (res.outline[i].SecondaryCurve2D.StartPoint | outline[i].SecondaryCurve2D.EndPoint) + (res.outline[i].SecondaryCurve2D.EndPoint | outline[i].SecondaryCurve2D.StartPoint))
                            {
                                res.outline[i].SecondaryCurve2D.Reverse();
                            }
                        }
                    }
                }
                else
                {
                    Edge e = outline[i].Clone(clonedVertices);
                    clonedEdges[outline[i]] = e;
                    if (e.Curve3D is InterpolatedDualSurfaceCurve)
                    {
                        (e.Curve3D as InterpolatedDualSurfaceCurve).UserData.Add("Edge.Clone.ReversePrimarySecondary", outline[i].SecondaryFace == this);
#if DEBUG
                        if ((outline[i].Curve3D as InterpolatedDualSurfaceCurve).Surface1.GetType() != outline[i].PrimaryFace.Surface.GetType())
                        {

                        }
#endif
                    }
                    e.SetPrimary(res, outline[i].Curve2D(this).Clone(), outline[i].Forward(this));
                    // wenn e.curve3d InterpolatedDualSurfaceCurve ist, dann wird diese mit surface1==res.surface überschrieben
                    // damit wären u.U. beide surfaces gleich, das darf nicht sein.
                    res.outline[i] = e;
                    alreadyUsed[i] = outline[i].Curve2D(this);
                    e.Owner = res; // welches face ist i.A. der Owner einer Kante? Das ist nicht eindeutig
                }
            }
            res.holes = new Edge[holes.Length][];
            for (int j = 0; j < holes.Length; ++j)
            {
                res.holes[j] = new Edge[holes[j].Length];
                for (int i = 0; i < holes[j].Length; ++i)
                {
                    if (clonedEdges.ContainsKey(holes[j][i]))
                    {   // die Kante gibts schon
                        clonedEdges[holes[j][i]].SetSecondary(res, holes[j][i].Curve2D(this).Clone(), holes[j][i].Forward(this));
                        res.holes[j][i] = clonedEdges[holes[j][i]];
                        if (res.holes[j][i].Curve3D is InterpolatedDualSurfaceCurve)
                        {
                            InterpolatedDualSurfaceCurve idsc = (res.holes[j][i].Curve3D as InterpolatedDualSurfaceCurve);
                            bool exchangeFaces = (bool)(idsc).UserData.GetData("Edge.Clone.ReversePrimarySecondary");
                            (res.holes[j][i].Curve3D as InterpolatedDualSurfaceCurve).UserData.RemoveUserData("Edge.Clone.ReversePrimarySecondary");
                            if (exchangeFaces)
                            {
                                (res.holes[j][i].Curve3D as InterpolatedDualSurfaceCurve).SetSurfaces(res.holes[j][i].PrimaryFace.internalSurface, res.holes[j][i].SecondaryFace.internalSurface, true);
                                res.holes[j][i].PrimaryCurve2D = (res.holes[j][i].Curve3D as InterpolatedDualSurfaceCurve).CurveOnSurface1;
                                res.holes[j][i].SecondaryCurve2D = (res.holes[j][i].Curve3D as InterpolatedDualSurfaceCurve).CurveOnSurface2;
                                if ((res.holes[j][i].PrimaryCurve2D.StartPoint | holes[j][i].SecondaryCurve2D.StartPoint) + (res.holes[j][i].PrimaryCurve2D.EndPoint | holes[j][i].SecondaryCurve2D.EndPoint) >
                                    (res.holes[j][i].PrimaryCurve2D.StartPoint | holes[j][i].SecondaryCurve2D.EndPoint) + (res.holes[j][i].PrimaryCurve2D.EndPoint | holes[j][i].SecondaryCurve2D.StartPoint))
                                {
                                    res.holes[j][i].PrimaryCurve2D.Reverse();
                                }
                                if ((res.holes[j][i].SecondaryCurve2D.StartPoint | holes[j][i].PrimaryCurve2D.StartPoint) + (res.holes[j][i].SecondaryCurve2D.EndPoint | holes[j][i].PrimaryCurve2D.EndPoint) >
                                    (res.holes[j][i].SecondaryCurve2D.StartPoint | holes[j][i].PrimaryCurve2D.EndPoint) + (res.holes[j][i].SecondaryCurve2D.EndPoint | holes[j][i].PrimaryCurve2D.StartPoint))
                                {
                                    res.holes[j][i].SecondaryCurve2D.Reverse();
                                }
                            }
                            else
                            {
                                (res.holes[j][i].Curve3D as InterpolatedDualSurfaceCurve).SetSurfaces(res.holes[j][i].PrimaryFace.internalSurface, res.holes[j][i].SecondaryFace.internalSurface, false);
                                res.holes[j][i].PrimaryCurve2D = (res.holes[j][i].Curve3D as InterpolatedDualSurfaceCurve).CurveOnSurface1;
                                res.holes[j][i].SecondaryCurve2D = (res.holes[j][i].Curve3D as InterpolatedDualSurfaceCurve).CurveOnSurface2;
                                if ((res.holes[j][i].PrimaryCurve2D.StartPoint | holes[j][i].PrimaryCurve2D.StartPoint) + (res.holes[j][i].PrimaryCurve2D.EndPoint | holes[j][i].PrimaryCurve2D.EndPoint) >
                                    (res.holes[j][i].PrimaryCurve2D.StartPoint | holes[j][i].PrimaryCurve2D.EndPoint) + (res.holes[j][i].PrimaryCurve2D.EndPoint | holes[j][i].PrimaryCurve2D.StartPoint))
                                {
                                    res.holes[j][i].PrimaryCurve2D.Reverse();
                                }
                                if ((res.holes[j][i].SecondaryCurve2D.StartPoint | holes[j][i].SecondaryCurve2D.StartPoint) + (res.holes[j][i].SecondaryCurve2D.EndPoint | holes[j][i].SecondaryCurve2D.EndPoint) >
                                    (res.holes[j][i].SecondaryCurve2D.StartPoint | holes[j][i].SecondaryCurve2D.EndPoint) + (res.holes[j][i].SecondaryCurve2D.EndPoint | holes[j][i].SecondaryCurve2D.StartPoint))
                                {
                                    res.holes[j][i].SecondaryCurve2D.Reverse();
                                }
                            }
                        }

                    }
                    else
                    {
                        Edge e = holes[j][i].Clone(clonedVertices);
                        clonedEdges[holes[j][i]] = e;
                        if (e.Curve3D is InterpolatedDualSurfaceCurve)
                        {
                            (e.Curve3D as InterpolatedDualSurfaceCurve).UserData.Add("Edge.Clone.ReversePrimarySecondary", holes[j][i].SecondaryFace == this);
                        }
                        e.SetPrimary(res, holes[j][i].Curve2D(this).Clone(), holes[j][i].Forward(this));
                        res.holes[j][i] = e;
                        e.Owner = res; // welches face ist i.A. der Owner einer Kante? Das ist nicht eindeutig
                    }
                }
            }
            res.CopyAttributes(this);
            res.extent = this.extent; // struct, wird kopiert
            return res;
        }

        internal bool Simplify(double precision)
        {
            ISurface canonical = surface.GetCanonicalForm(precision, this.Area.GetExtent());
            if (canonical != null)
            {
                this.surface = canonical;
                BoundingRect bounds = BoundingRect.EmptyBoundingRect;
                foreach (Edge edg in OutlineEdges)
                {
                    GeoPoint2D pos = canonical.PositionOf(edg.StartVertex(this).Position);
                    if (!bounds.IsEmpty()) SurfaceHelper.AdjustPeriodic(canonical, bounds, ref pos);
                    bounds.MinMax(pos);
                }
                if (canonical.IsUPeriodic)
                {
                    double umin = (bounds.Left + bounds.Right) / 2.0 - canonical.UPeriod / 2.0;
                    double umax = umin + canonical.UPeriod;
                    bounds.Left = umin;
                    bounds.Right = umax;
                }
                else
                {
                    //bounds.Left = double.MinValue;
                    //bounds.Right = double.MaxValue;
                }
                if (canonical.IsVPeriodic)
                {
                    double vmin = (bounds.Bottom + bounds.Top) / 2.0 - canonical.VPeriod / 2.0;
                    double vmax = vmin + canonical.VPeriod;
                    bounds.Bottom = vmin;
                    bounds.Top = vmax;
                }
                else
                {
                    //bounds.Bottom = double.MinValue;
                    //bounds.Top = double.MaxValue;
                }
                canonical.SetBounds(bounds); // need finite bounds for boxedsurface
                foreach (Edge edg in AllEdgesIterated())
                {
                    if (edg.Curve3D != null)
                    {
                        Face otherFace = edg.OtherFace(this);
                        if (otherFace != null)
                        {
                            ICurve[] crvs = canonical.Intersect(bounds, otherFace.surface, otherFace.GetUVBounds());
                            ICurve found = null;
                            for (int i = 0; i < crvs.Length; i++)
                            {
                                double spos = crvs[i].PositionOf(edg.StartVertex(this).Position);
                                double epos = crvs[i].PositionOf(edg.EndVertex(this).Position);
                                if (spos >= -1e-6 && spos <= 1 + 1e-6 && epos >= -1e-6 && epos <= 1 + 1e-6)
                                {
                                    found = crvs[i];
                                    if (spos < epos) found.Trim(spos, epos);
                                    else
                                    {
                                        found.Trim(epos, spos);
                                        found.Reverse();
                                    }
                                    if (edg.PrimaryFace == this) edg.PrimaryCurve2D = canonical.GetProjectedCurve(found, precision);
                                    else edg.SecondaryCurve2D = canonical.GetProjectedCurve(found, precision);
                                    if (!edg.Forward(this)) found.Reverse();
                                    edg.Curve3D = found;
                                    break;
                                }
                            }
                            if (found == null)
                            {
                                ICurve2D crv2d = canonical.GetProjectedCurve(edg.Curve3D, precision);
                                if (!edg.Forward(this)) crv2d.Reverse();
                                if (edg.PrimaryFace == this) edg.PrimaryCurve2D = crv2d;
                                else edg.SecondaryCurve2D = crv2d;
                            }
                        }
                    }
                }
                return true;
            }
            return false;
        }
        /// <summary>
        /// Find a chain of edges from the Vertex <paramref name="from"/> to the Vertex <paramref name="to"/>.
        /// </summary>
        /// <param name="from"></param>
        /// <param name="to"></param>
        /// <returns></returns>
        internal Edge[] FindEdgeChain(Vertex from, Vertex to)
        {
            Set<Edge> fromEdges = from.AllEdges.Intersection(this.AllEdgesSet); // use this intersection, because there might be intersection edges, which don't belong to this outline and holes edges
            Edge startWith = null;
            foreach (Edge edge in fromEdges)
            {
                if (edge.StartVertex(this) == from)
                {
                    startWith = edge;
                    break;
                }
            }
            List<Edge> res = new List<Edge>();
            if (startWith != null) // there must be one
            {
                res.Add(startWith);
                while (startWith.EndVertex(this) != to)
                {
                    if (startWith.EndVertex(this) == from)
                    {   // loop is closed without hitting "to"
                        return new Edge[0];
                    }
                    startWith = GetNextEdge(startWith);
                    res.Add(startWith);
                }
                return res.ToArray();
            }
            return new Edge[0]; // should not happen

        }

        public void ModifySurface(ISurface surface, ModOp2D reparameterize)
        {
            this.surface = surface.Clone();
            BoundingRect modifiedBounds = Domain.GetModified(reparameterize);
            foreach (Edge edg in Edges)
            {
                //edg.PrimaryCurve2D = this.surface.GetProjectedCurve(edg.Curve3D, 0.0);
                //bool done = false;
                //if (edg.Curve3D is InterpolatedDualSurfaceCurve idsc)
                //{
                //    ISurface toReplace = null;
                //    if (idsc.Surface1.SameGeometry(Domain, surface, Domain, Precision.eps, out ModOp2D dumy)) toReplace = idsc.Surface1;
                //    else if (idsc.Surface2.SameGeometry(Domain, surface, Domain, Precision.eps, out dumy)) toReplace = idsc.Surface2;
                //    if (toReplace != null)
                //    {
                //        idsc.ReplaceSurface(toReplace, this.surface, reparameterize);
                //        done = true;
                //    }
                //}
                edg.ModifyCurve2D(this, null, reparameterize);
                SurfaceHelper.AdjustPeriodic(this.surface, modifiedBounds, edg.Curve2D(this));
            }
            foreach (Edge edg in Edges)
            {
                edg.Orient(); // ModifyCurve2D unsets the "oriented"-Flag of the edge. Maybe the edge has already been oriented, so ReverseOrientation(this) doesn't help
                if (edg.Curve3D is InterpolatedDualSurfaceCurve idsc)
                {
                    if (edg.PrimaryFace == this)
                    {
                        ISurface otherSurface = edg.SecondaryFace != null ? edg.SecondaryFace.surface : null;
                        BoundingRect otherBounds = BoundingRect.EmptyBoundingRect;
                        if (edg.SecondaryFace != null) otherBounds = edg.SecondaryFace.Domain;
                        if (otherSurface == null)
                        {
                            otherBounds = idsc.CurveOnSurface1.GetExtent();
                            if (idsc.Surface1.SameGeometry(otherBounds, this.surface, modifiedBounds, Precision.eps, out ModOp2D dumy)) otherSurface = idsc.Surface2;
                            else
                            {
                                otherBounds = idsc.CurveOnSurface2.GetExtent();
                                otherSurface = idsc.Surface1;
                            }
                        }
                        edg.Curve3D = new InterpolatedDualSurfaceCurve(this.surface, modifiedBounds, otherSurface, otherBounds, idsc.BasePoints);
                        edg.PrimaryCurve2D = (edg.Curve3D as InterpolatedDualSurfaceCurve).CurveOnSurface1;
                        if (!edg.Forward(edg.PrimaryFace)) edg.PrimaryCurve2D.Reverse();
                        edg.SecondaryCurve2D = (edg.Curve3D as InterpolatedDualSurfaceCurve).CurveOnSurface2;
                        if (!edg.Forward(edg.SecondaryFace)) edg.SecondaryCurve2D.Reverse();
                    }
                    else
                    {
                        ISurface otherSurface = edg.PrimaryFace != null ? edg.PrimaryFace.surface : null;
                        BoundingRect otherBounds = BoundingRect.EmptyBoundingRect;
                        if (edg.PrimaryFace != null) otherBounds = edg.PrimaryFace.Domain;
                        if (otherSurface == null)
                        {
                            otherBounds = idsc.CurveOnSurface1.GetExtent();
                            if (idsc.Surface1.SameGeometry(otherBounds, this.surface, modifiedBounds, Precision.eps, out ModOp2D dumy)) otherSurface = idsc.Surface2;
                            else
                            {
                                otherBounds = idsc.CurveOnSurface2.GetExtent();
                                otherSurface = idsc.Surface1;
                            }
                        }
                        edg.Curve3D = new InterpolatedDualSurfaceCurve(otherSurface, otherBounds, this.surface, modifiedBounds, idsc.BasePoints);
                        edg.PrimaryCurve2D = (edg.Curve3D as InterpolatedDualSurfaceCurve).CurveOnSurface1;
                        if (!edg.Forward(edg.PrimaryFace)) edg.PrimaryCurve2D.Reverse();
                        edg.SecondaryCurve2D = (edg.Curve3D as InterpolatedDualSurfaceCurve).CurveOnSurface2;
                        if (!edg.Forward(edg.SecondaryFace)) edg.SecondaryCurve2D.Reverse();
                    }
                }
            }

            if (reparameterize.Determinant < 0)
            {
                Array.Reverse(outline);
                for (int i = 0; i < outline.Length; i++)
                {
                    outline[i].Curve2D(this).Reverse();
                }
                if (holes != null)
                {
                    for (int j = 0; j < holes.Length; j++)
                    {
                        for (int i = 0; i < holes[j].Length; i++)
                        {
                            holes[j][i].Curve2D(this).Reverse();
                        }
                    }
                }
            }
            ForceAreaRecalc();
            Orient();
        }

        internal Edge[] GetHole(Edge edge)
        {
            for (int k = 0; k < HoleCount; k++)
            {
                for (int j = 0; j < holes[k].Length; j++)
                {
                    if (holes[k][j] == edge) return holes[k];
                }
            }
            return null;
        }

        /// <summary>
        /// Find a connection from the provided startVertex up to the first vertex in stopVertices
        /// </summary>
        /// <param name="startVertex"></param>
        /// <param name="stopVertices"></param>
        /// <returns></returns>
        internal List<Edge> FindConnection(Vertex startVertex, Set<Vertex> stopVertices)
        {
            List<Edge> res = new List<Edge>();
            Edge startWith = startVertex.FindOutgoing(this);
            if (startWith == null) return res;
            Edge next = startWith;
            res.Add(startWith);
            do
            {
                if (stopVertices.Contains(next.EndVertex(this))) break;
                next = GetNextEdge(next);
                res.Add(next);
            } while (next != startWith);
            return res;
        }

        /// <summary>
        /// Overrides <see cref="CADability.GeoObject.IGeoObjectImpl.Clone ()"/>
        /// </summary>
        /// <returns></returns>
        public override IGeoObject Clone()
        {
            Face res = this.Clone(new Dictionary<Edge, Edge>(), new Dictionary<Vertex, Vertex>());
            // res.area = Area.Clone();
            // SimpleShape forceArea = res.Area; // dauert zu lange, ist area.clone OK? es sind dann nicht die 2d
            Vertex[] vtxs = res.Vertices;
            res.CopyAttributes(this);
            return res;
        }
        internal Face CloneWithVertices()
        {
            Dictionary<Edge, Edge> ed = new Dictionary<Edge, Edge>();
            Face res = this.Clone(ed, new Dictionary<Vertex, Vertex>());
            foreach (KeyValuePair<Edge, Edge> kv in ed)
            {
                kv.Value.UseVertices(kv.Key.Vertex1, kv.Key.Vertex2);
            }
            Vertex[] vtxs = res.Vertices;
            res.CopyAttributes(this);
#if DEBUG
            //System.Diagnostics.Trace.Assert(res.CheckConsitency());
#endif
            return res;
        }
        /// <summary>
        /// This surface modification leaves the face in an invalid state, the edges muste be modified accordingly
        /// </summary>
        /// <param name="m"></param>
        internal void ModifySurfaceOnly(ModOp m)
        {
#if DEBUG
            if (hashCode == 406)
            { }
#endif
            BoundingRect ext = (surface as ISurfaceImpl).usedArea;
            surface = surface.GetModified(m);
            (surface as ISurfaceImpl).usedArea = ext; // needed for BoxedSurface
            extent = BoundingCube.EmptyBoundingCube;
        }
        public void ModifySurface(ModOp m)
        {
            // usually called from Shell, which modifies the edges separately
#if DEBUG
            if (hashCode == 406)
            { }
            int tc0 = System.Environment.TickCount;
#endif
            using (new Changing(this, false)) // no undo necessary
            {   // not sure, why changing is needed here
                BoundingRect ext = (surface as ISurfaceImpl).usedArea;
                surface = surface.GetModified(m);
                area = null; // ProjectedCurves might be wrong after surface modification
                (surface as ISurfaceImpl).usedArea = ext; // needed for BoxedSurface
                                                          // we don't modify the surface directly but get a new copy of the modified surface
                                                          // Edges with InterpolatedDualSurfaceCurves (hopefully) can deal with this
                                                          // The caller must ensure that the edges ReflectModification is beeing called
                lock (lockTriangulationData)
                {
                    if (trianglePoint != null)
                    {
                        for (int i = 0; i < trianglePoint.Length; ++i)
                        {
                            trianglePoint[i] = m * trianglePoint[i];
                        }
                        triangleExtent = BoundingCube.EmptyBoundingCube;
                    }
                }
            }
#if DEBUG
            int tc1 = System.Environment.TickCount;
            //System.Diagnostics.Trace.WriteLine("ModifySurface: " + this.hashCode.ToString() + ", " + (tc1 - tc0).ToString());
#endif
            extent = BoundingCube.EmptyBoundingCube;
        }
        /// <summary>
        /// Overrides <see cref="CADability.GeoObject.IGeoObjectImpl.Modify (ModOp)"/>
        /// </summary>
        /// <param name="m"></param>
        public override void Modify(ModOp m)
        {
#if DEBUG
            if (hashCode == 406)
            { }
#endif
            using (new Changing(this, "ModifyInverse", m))
            {
                ModifySurface(m);
                foreach (Edge edge in AllEdges)
                {
                    edge.Modify(m);
                    edge.ReflectModification();
                }
                // clear all vertices, will be recalculated on request
                //foreach (Vertex vtx in Vertices)
                //{
                //    vtx.Modify(m);
                //}
                vertices = null;
                extent = BoundingCube.EmptyBoundingCube;
            }
        }
        /// <summary>
        /// Overrides <see cref="CADability.GeoObject.IGeoObjectImpl.FindSnapPoint (SnapPointFinder)"/>
        /// </summary>
        /// <param name="spf"></param>
        public override void FindSnapPoint(SnapPointFinder spf)
        {
            if (!spf.Accept(this)) return;
            for (int i = 0; i < outline.Length; i++)
            {
                if (outline[i].Curve3D is IGeoObject) // kann null sein
                {
                    (outline[i].Curve3D as IGeoObject).FindSnapPoint(spf);
                }
            }
            for (int i = 0; i < holes.Length; i++)
            {
                for (int j = 0; j < holes[i].Length; j++)
                {
                    if (holes[i][j].Curve3D is IGeoObject) // kann null sein
                    {
                        (holes[i][j].Curve3D as IGeoObject).FindSnapPoint(spf);
                    }
                }
            }
            if (spf.SnapToFaceSurface)
            {
                GeoPoint2D[] sp = surface.GetLineIntersection(spf.SourceBeam.Location, spf.SourceBeam.Direction);

                for (int i = 0; i < sp.Length; ++i)
                {
                    if (Contains(ref sp[i], true))
                    {
                        GeoPoint p = surface.PointAt(sp[i]);
                        double linepos = Geometry.LinePar(spf.SourceBeam.Location, spf.SourceBeam.Direction, p);
                        if (linepos < spf.faceDist)
                        {
                            spf.faceDist = linepos;
                            spf.Check(spf.SourceBeam.Location + linepos * spf.SourceBeam.Direction, this, SnapPointFinder.DidSnapModes.DidSnapToFaceSurface);
                        }
                    }
                }
            }
        }

        internal void DisconnectAllEdges()
        {
            foreach (Edge edg in Edges)
            {
                edg.DisconnectFromFace(this);
            }
        }

        /// <summary>
        /// Overrides <see cref="CADability.GeoObject.IGeoObjectImpl.GetBoundingCube ()"/>
        /// </summary>
        /// <returns></returns>
        public override BoundingCube GetBoundingCube()
        {
            if (extent.IsEmpty && surface != null)
            {
                // we need to check both the outline and the holes: a (non periodic) cylinder may have two cricles as edges
                // we must consider both edges (one of which is a hole)
                extent = BoundingCube.EmptyBoundingCube;
                foreach (Edge edge in Edges)
                {
                    if (edge.Curve3D != null)
                    {
                        extent.MinMax(edge.Curve3D.GetExtent());
                    }
                }
                // a surface extreme point in axis direction is also necessary to check
                GeoPoint2D[] extrema = surface.GetExtrema();
                for (int i = 0; i < extrema.Length; ++i)
                {
                    if (Contains(ref extrema[i], true)) // respects periodicity
                    {
                        extent.MinMax(surface.PointAt(extrema[i]));
                    }
                }
            }
            return extent;
        }
        private object lockTriangulationRecalc;
        private object lockTriangulationData;
        private GeoPoint[] trianglePoint;
        private GeoPoint2D[] triangleUVPoint;
        private int[] triangleIndex;
        private double trianglePrecision;
        private BoundingCube triangleExtent;
        private class TraingleOctTree : IOctTreeInsertable
        {
            public int Index;
            public Face thisFace;   // Rückverweis
            public TraingleOctTree(Face thisFace, int index)
            {
                this.thisFace = thisFace;
                this.Index = index;
            }
            #region IOctTreeInsertable Members

            BoundingCube IOctTreeInsertable.GetExtent(double precision)
            {
                BoundingCube res = BoundingCube.EmptyBoundingCube;
                lock (thisFace.lockTriangulationData)
                {
                    res.MinMax(thisFace.trianglePoint[thisFace.triangleIndex[Index]]);
                    res.MinMax(thisFace.trianglePoint[thisFace.triangleIndex[Index + 1]]);
                    res.MinMax(thisFace.trianglePoint[thisFace.triangleIndex[Index + 2]]);
                }
                return res;
            }

            bool IOctTreeInsertable.HitTest(ref BoundingCube cube, double precision)
            {
                lock (thisFace.lockTriangulationData)
                {
                    return cube.Interferes(
                        ref thisFace.trianglePoint[thisFace.triangleIndex[Index]],
                        ref thisFace.trianglePoint[thisFace.triangleIndex[Index + 1]],
                        ref thisFace.trianglePoint[thisFace.triangleIndex[Index + 2]]);
                }
            }

            bool IOctTreeInsertable.HitTest(Projection projection, BoundingRect rect, bool onlyInside)
            {
                lock (thisFace.lockTriangulationData)
                {
                    GeoPoint2D p1 = projection.ProjectUnscaled(thisFace.trianglePoint[thisFace.triangleIndex[Index]]);
                    GeoPoint2D p2 = projection.ProjectUnscaled(thisFace.trianglePoint[thisFace.triangleIndex[Index + 1]]);
                    GeoPoint2D p3 = projection.ProjectUnscaled(thisFace.trianglePoint[thisFace.triangleIndex[Index + 2]]);
                    if (onlyInside)
                    {
                        return p1 <= rect && p2 <= rect && p3 <= rect;
                    }
                    else
                    {
                        ClipRect clr = new ClipRect(ref rect);
                        return clr.TriangleHitTest(p1, p2, p3);
                    }
                }
            }

            double IOctTreeInsertable.Position(GeoPoint fromHere, GeoVector direction, double precision)
            {
                double res = double.MaxValue;
                lock (thisFace.lockTriangulationData)
                {
                    GeoPoint p1 = thisFace.trianglePoint[thisFace.triangleIndex[Index]];
                    GeoPoint p2 = thisFace.trianglePoint[thisFace.triangleIndex[Index + 1]];
                    GeoPoint p3 = thisFace.trianglePoint[thisFace.triangleIndex[Index + 2]];
                    // dirx = p2-p1, diry = p3-p1, dirz = direction, org = p1
                    // org + x*dirx + y*diry+z = fromHere + z*direction
                    // x>0, y>0, x+y<1: getroffen
                    Matrix m = DenseMatrix.OfColumnArrays(p2 - p1, p3 - p1, -direction);
                    Vector b = new DenseVector(fromHere - p1);
                    Vector x = (Vector)m.Solve(b); // liefert gleichzeitig die Bedingung für innen und den Abstand
                    if (x != null)
                    {
                        if (x[0] >= 0.0 && x[1] >= 0.0 && (x[0] + x[1]) <= 1.0)
                        {
                            if (x[2] < res)
                            {
                                res = x[2];
                            }
                        }
                    }
                    return res;
                }
            }
            bool IOctTreeInsertable.HitTest(Projection.PickArea area, bool onlyInside)
            {
                throw new Exception("The method or operation is not implemented.");
            }
            #endregion
        }
        public GeoPoint2D[][] GetUVOutline(double precision)
        {   // kopiert aus Triangulate, liefert dei uv Umrandung zu der gegebenen Auflösung als Array von Polylinien
            List<GeoPoint2D[]> polylines = new List<GeoPoint2D[]>();
            List<GeoPoint2D> polyoutline = new List<GeoPoint2D>();

            ICurve2D[] usedCurves = new ICurve2D[outline.Length];
            for (int i = 0; i < outline.Length; ++i)
            {
                usedCurves[i] = outline[i].Curve2D(this, usedCurves);
                GeoPoint2D[] points = outline[i].GetTriangulationBasis(this, precision, usedCurves[i]);
                if (outline.Length == 1 && points.Length < 3)
                {   // das würde ja bedeuten, nur zwei Punkte auf der Outline, also nur ein Strich, hin- und zurück
                    points = new GeoPoint2D[3];
                    points[0] = usedCurves[i].PointAt(0.0);
                    points[1] = usedCurves[i].PointAt(1.0 / 3.0);
                    points[2] = usedCurves[i].PointAt(2.0 / 3.0);
                }
                polyoutline.AddRange(points);
            }
            GeoPoint2D[] tmp = polyoutline.ToArray();
            BoundingRect ext = new BoundingRect(tmp);
            double eps = Math.Min((ext.Width * ext.Height) * 1e-6, precision);
            // Problem mit der Genauigkeit: eine sehr große Fläche mit kleinen Löchern führt dazu, dass eps zu groß wird
            // und die Löcher verschwinden
            bool removed = false;
            for (int i = polyoutline.Count - 2; i >= 0; --i)
            {
                if ((polyoutline[i] | polyoutline[i + 1]) < eps)
                {
                    polyoutline.RemoveAt(i);
                    removed = true;
                }
            }
            if (polyoutline.Count > 0 && (polyoutline[0] | polyoutline[polyoutline.Count - 1]) < eps)
            {
                polyoutline.RemoveAt(polyoutline.Count - 1);
                removed = true;
            }
            if (surface is NurbsSurface)
            {   // bei NURBS Flächen Punkte außerhalb des Definitionsbereices in den Definitionsbereich schieben
                double umin, umax, vmin, vmax;
                surface.GetNaturalBounds(out umin, out umax, out vmin, out vmax);
                if (!surface.IsUPeriodic)
                {
                    for (int i = polyoutline.Count - 1; i >= 0; --i)
                    {
                        // nicht verwerfen sondern reinschieben
                        if (polyoutline[i].x > umax)
                        {
                            polyoutline[i] = new GeoPoint2D(umax, polyoutline[i].y);
                            removed = true; // damit polyoutline übernommen wird
                        }
                        if (polyoutline[i].x < umin)
                        {
                            polyoutline[i] = new GeoPoint2D(umin, polyoutline[i].y);
                            removed = true;
                        }
                    }
                }
                if (!surface.IsVPeriodic)
                {
                    for (int i = polyoutline.Count - 1; i >= 0; --i)
                    {
                        //if (polyoutline[i].y > vmax || polyoutline[i].y < vmin)
                        //{
                        //    polyoutline.RemoveAt(i);
                        //    removed = true;
                        //}
                        // nicht verwerfen sondern reinschieben
                        if (polyoutline[i].y > vmax)
                        {
                            polyoutline[i] = new GeoPoint2D(polyoutline[i].x, vmax);
                            removed = true; // damit polyoutline übernommen wird
                        }
                        if (polyoutline[i].y < vmin)
                        {
                            polyoutline[i] = new GeoPoint2D(polyoutline[i].x, vmin);
                            removed = true;
                        }
                    }
                }
            }
            if (removed) tmp = polyoutline.ToArray();
            if (GeoPoint2D.Area(tmp) < eps)
            {   // kommt manchmal vor: zwei entgegenlaufende Linien, hier also leeres Polygon oder falschrum
                return polylines.ToArray(); // das ist leer
            }
            polylines.Add(tmp);
            polyoutline.Clear();
            for (int i = 0; i < holes.Length; ++i)
            {
                for (int j = 0; j < holes[i].Length; ++j)
                {
                    GeoPoint2D[] points = holes[i][j].GetTriangulationBasis(this, precision, holes[i][j].Curve2D(this)); // letzter Punkt fehlt
                    polyoutline.AddRange(points);
                }
                if (polyoutline.Count == 1) polyoutline.Clear();
                // polyoutline.Reverse();
                tmp = polyoutline.ToArray();
                // Doppelpunkte entfernen
                removed = false;
                for (int j = polyoutline.Count - 2; j >= 0; --j)
                {
                    if ((polyoutline[j] | polyoutline[j + 1]) < eps)
                    {
                        // polyoutline.RemoveAt(j + 1); nicht j+1, sonst frisst sich die polyline von hinten her auf
                        // wenn alle Abstände zu klein sind
                        polyoutline.RemoveAt(j);
                        removed = true;
                    }
                }
                if (polyoutline.Count > 1)
                {
                    if ((polyoutline[0] | polyoutline[polyoutline.Count - 1]) < eps)
                    {
                        polyoutline.RemoveAt(polyoutline.Count - 1);
                        removed = true;
                    }
                }
                if (removed) tmp = polyoutline.ToArray();
                if (tmp.Length > 2) polylines.Add(tmp); // geändert auf "2", denn es müssen mehr als 2 Punkte sein, sonst nur ein Schlitz (siehe VM4421.10.147_TM1.stp)
                polyoutline.Clear();
            }
            return polylines.ToArray();
        }

        private OctTree<TraingleOctTree> triangleOctTree;
        private void Triangulate(double precision)
        {
            SimpleShape ss = Area; // damit es sicher bestimmt ist
                                   // wenn die Fläche Knicke hat, dann entlang der Knicke aufteilen
            ICurve2D[] discontinuities;
            if (false)
            // if (surface.HasDiscontinuousDerivative(out discontinuities))
            {
                // Das Face an den Knicken aufteilen. Die erste Aufteilung, die wirklich zwei Teilfaces
                // liefert, ruft rekursiv die Triangulierung auf. Es muss sichergestellt werden
                // dass die Rekursion zu Ende kommt: Einmal an einer Kante geteilt liefert Unterflächen,
                // die an dieser Kante nicht mehr teilbar sind.
                // Wenn es hier garnicht zur Aufteilung kommt, dann wird der Normalfall durchgeführt

                // ein weiteres noch ungelöstes Problem an dieser Stelle ist, dass der Normalenvektor
                // an der Kante eigentlich undefiniert ist bzw. zwei Werte hat.
                // Deshalb sehen die Flächen oft nicht kantig genug aus.
                for (int i = 0; i < discontinuities.Length; i++)
                {
                    Border splitWith = new Border(discontinuities[i]);
                    CompoundShape splitted = Area.Split(splitWith);
                    if (splitted.SimpleShapes.Length > 1)
                    {
                        List<GeoPoint> lsttrianglePoint = new List<GeoPoint>();
                        List<GeoPoint2D> lsttriangleUVPoint = new List<GeoPoint2D>();
                        List<int> lsttriangleIndex = new List<int>();
                        for (int j = 0; j < splitted.SimpleShapes.Length; j++)
                        {
                            CompoundShape shrink = splitted.SimpleShapes[j].Shrink(splitted.SimpleShapes[j].GetExtent().Size * 1e-6);
                            SimpleShape sss;
                            if (shrink.SimpleShapes.Length == 1) sss = shrink.SimpleShapes[0];
                            else sss = splitted.SimpleShapes[j];
                            Face fc = Face.MakeFace(surface, sss);
                            GeoPoint[] tmptrianglePoint;
                            GeoPoint2D[] tmptriangleUVPoint;
                            int[] tmptriangleIndex;
                            BoundingCube tmptriangleExtent;
                            fc.GetTriangulation(precision, out tmptrianglePoint, out tmptriangleUVPoint, out tmptriangleIndex, out tmptriangleExtent);
                            for (int k = 0; k < tmptriangleIndex.Length; k++)
                            {
                                tmptriangleIndex[k] += lsttrianglePoint.Count;
                            }
                            lsttrianglePoint.AddRange(tmptrianglePoint);
                            lsttriangleUVPoint.AddRange(tmptriangleUVPoint);
                            lsttriangleIndex.AddRange(tmptriangleIndex);
                        }
                        lock (lockTriangulationData)
                        {
                            trianglePoint = lsttrianglePoint.ToArray();
                            triangleUVPoint = lsttriangleUVPoint.ToArray();
                            triangleIndex = lsttriangleIndex.ToArray();
                        }
                        return;
                    }
                }

            }
#if DEBUG
            // System.Diagnostics.Trace.WriteLine("Triangulate: " + hashCode.ToString() + ", prec: " + precision.ToString() + ", " + (System.Environment.TickCount / 100).ToString());
#endif
            int tc0 = System.Environment.TickCount;
            // First step: split area into polylines
            List<GeoPoint2D[]> polylines = new List<GeoPoint2D[]>();
            List<GeoPoint2D> polyoutline = new List<GeoPoint2D>();

            ICurve2D[] usedCurves = new ICurve2D[outline.Length];
#if DEBUG
            if (hashCode == 1087)
            { }
            if (UserData.ContainsData("StepImport.ItemNumber"))
            {
                IntegerProperty ip = UserData["StepImport.ItemNumber"] as IntegerProperty;
                if (ip.IntegerValue == 3672)
                { }
            }
#endif
            for (int i = 0; i < outline.Length; ++i)
            {
                usedCurves[i] = outline[i].Curve2D(this, usedCurves);
                if (outline[i].Curve3D != null)
                {   // singuläre Kanten nicht verwenden. Damit wird erreicht, dass ein singulärer Punkt nur einmal vorkommt
                    // nämlich bei der Kante, die vom singulären Punkt wegführt
                    GeoPoint2D[] points = outline[i].GetTriangulationBasis(this, precision, usedCurves[i]);
                    if (outline.Length == 1 && points.Length < 3)
                    {   // das würde ja bedeuten, nur zwei Punkte auf der Outline, also nur ein Strich, hin- und zurück
                        points = new GeoPoint2D[3];
                        points[0] = usedCurves[i].PointAt(0.0);
                        points[1] = usedCurves[i].PointAt(1.0 / 3.0);
                        points[2] = usedCurves[i].PointAt(2.0 / 3.0);
                    }
                    polyoutline.AddRange(points);
                }
                else
                {
                    polyoutline.Add(usedCurves[i].StartPoint);
                }
            }
            GeoPoint2D[] tmp = polyoutline.ToArray();
            BoundingRect ext = new BoundingRect(tmp);
            double eps = Math.Min((ext.Width * ext.Height) * 1e-6, precision);
            // Problem mit der Genauigkeit: eine sehr große Fläche mit kleinen Löchern führt dazu, dass eps zu groß wird
            // und die Löcher verschwinden
            bool removed = false;
            for (int i = polyoutline.Count - 2; i >= 0; --i)
            {
                if ((polyoutline[i] | polyoutline[i + 1]) < eps)
                {
                    polyoutline.RemoveAt(i);
                    removed = true;
                }
            }
            if (polyoutline.Count > 0 && (polyoutline[0] | polyoutline[polyoutline.Count - 1]) < eps)
            {
                polyoutline.RemoveAt(polyoutline.Count - 1);
                removed = true;
            }
            if (surface is NurbsSurface)
            {   // bei NURBS Flächen Punkte außerhalb des Definitionsbereices in den Definitionsbereich schieben
                double umin, umax, vmin, vmax;
                surface.GetNaturalBounds(out umin, out umax, out vmin, out vmax);
                if (!surface.IsUPeriodic)
                {
                    for (int i = polyoutline.Count - 1; i >= 0; --i)
                    {
                        if (polyoutline[i].x > umax)
                        {
                            polyoutline[i] = new GeoPoint2D(umax, polyoutline[i].y);
                            removed = true; // damit polyoutline übernommen wird
                        }
                        if (polyoutline[i].x < umin)
                        {
                            polyoutline[i] = new GeoPoint2D(umin, polyoutline[i].y);
                            removed = true;
                        }
                    }
                }
                if (!surface.IsVPeriodic)
                {
                    for (int i = polyoutline.Count - 1; i >= 0; --i)
                    {
                        if (polyoutline[i].y > vmax)
                        {
                            polyoutline[i] = new GeoPoint2D(polyoutline[i].x, vmax);
                            removed = true; // damit polyoutline übernommen wird
                        }
                        if (polyoutline[i].y < vmin)
                        {
                            polyoutline[i] = new GeoPoint2D(polyoutline[i].x, vmin);
                            removed = true;
                        }
                    }
                }
            }
            if (removed) tmp = polyoutline.ToArray();
            if (Math.Abs(GeoPoint2D.Area(tmp)) < eps) // warum gehen manche rechtsrum?
            {   // kommt manchmal vor: zwei entgegenlaufende Linien, hier also leeres Polygon oder falschrum
#if DEBUG
                DebuggerContainer dc = new DebuggerContainer();
                for (int i = 0; i < tmp.Length; ++i)
                {
                    int next = i + 1;
                    if (i == tmp.Length - 1)
                        next = 0;
                    GeoPoint2D sp = new GeoPoint2D(tmp[i].x / ext.Width, tmp[i].y / ext.Height);
                    GeoPoint2D ep = new GeoPoint2D(tmp[next].x / ext.Width, tmp[next].y / ext.Height);
                    Line2D l2d = new Line2D(sp, ep);
                    dc.Add(l2d, System.Drawing.Color.Red, i);
                }
#endif
                lock (lockTriangulationData)
                {
                    triangleUVPoint = new GeoPoint2D[0];
                    trianglePoint = new GeoPoint[0];
                    triangleIndex = new int[0];
                }
                return;
            }
            if (GeoPoint2D.Area(tmp) < 0) Array.Reverse(tmp);
            polylines.Add(tmp);
            polyoutline.Clear();
            //for (int i = 0; i < ss.NumHoles; ++i)
            //{
            //    for (int j = 0; j < ss.Hole(i).Count; ++j)
            //    {
            //        Edge e = ss.Hole(i)[j].UserData.GetData("CADability.Edge") as Edge;
            //        GeoPoint2D[] points = e.GetTriangulationBasis(this, precision, ss.Hole(i)[j]); // letzter Punkt fehlt
            //        polyoutline.AddRange(points);
            //    }
            //    if (polyoutline.Count == 1) polyoutline.Clear();
            //    polyoutline.Reverse();
            //    tmp = polyoutline.ToArray();
            //    if (GeoPoint2D.Area(tmp) < -eps) // ist ja andersrum
            //    {
            //        polylines.Add(tmp);
            //    }
            //    polyoutline.Clear();
            //}
            for (int i = 0; i < holes.Length; ++i)
            {
                for (int j = 0; j < holes[i].Length; ++j)
                {

                    GeoPoint2D[] points = holes[i][j].GetTriangulationBasis(this, precision, holes[i][j].Curve2D(this)); // letzter Punkt fehlt
                    polyoutline.AddRange(points);
                }
                if (polyoutline.Count == 1) polyoutline.Clear();
                // polyoutline.Reverse();
                tmp = polyoutline.ToArray();
                // Doppelpunkte entfernen
                removed = false;
                for (int j = polyoutline.Count - 2; j >= 0; --j)
                {
                    if ((polyoutline[j] | polyoutline[j + 1]) < eps)
                    {
                        // polyoutline.RemoveAt(j + 1); nicht j+1, sonst frisst sich die polyline von hinten her auf
                        // wenn alle Abstände zu klein sind
                        polyoutline.RemoveAt(j);
                        removed = true;
                    }
                }
                if (polyoutline.Count > 1)
                {
                    if ((polyoutline[0] | polyoutline[polyoutline.Count - 1]) < eps)
                    {
                        polyoutline.RemoveAt(polyoutline.Count - 1);
                        removed = true;
                    }
                }
                if (removed) tmp = polyoutline.ToArray();
                if (tmp.Length > 2) polylines.Add(tmp); // geändert auf "2", denn es müssen mehr als 2 Punkte sein, sonst nur ein Schlitz (siehe VM4421.10.147_TM1.stp)
                polyoutline.Clear();
            }
            try
            {

#if DEBUG
                DebuggerContainer dc = new DebuggerContainer();
                for (int j = 0; j < polylines.Count; ++j)
                {
                    for (int i = 0; i < polylines[j].Length; ++i)
                    {
                        int next = i + 1;
                        if (i == polylines[j].Length - 1)
                            next = 0;
                        GeoPoint2D sp = new GeoPoint2D(polylines[j][i].x / ext.Width, polylines[j][i].y / ext.Height);
                        GeoPoint2D ep = new GeoPoint2D(polylines[j][next].x / ext.Width, polylines[j][next].y / ext.Height);

                        Line2D l2d = new Line2D(sp, ep);
                        dc.Add(l2d);
                    }
                }
                // double dbga = this.area.Area;
#endif
                Triangulation t = new Triangulation(polylines.ToArray(), surface, precision * 5.0, 0.17);
                // precision*5: inside the face we don't need the same hight precision as on the bounds
                if (!t.innerIntersection)
                {
                    GeoPoint2D[] tmpTriUv;
                    GeoPoint[] tmpTriPoint;
                    int[] tmpTriInd;
                    t.GetSimpleTriangles(out tmpTriUv, out tmpTriPoint, out tmpTriInd, true);
                    int tc1 = System.Environment.TickCount - tc0;
                    if (Surface is IRestrictedDomain rd)
                    {   // in this case, where we have self intersecting polygons, the polygon intersection points may be outside the defined area of the surface.
                        // This is especially the case, when there is a cylindrical surface, which has usually a round inner hole. When this hole is split, 
                        // we get points far outside the domain of the cylinder, which look ugly. Here we restrict these points into the domain
                        for (int j = 0; j < tmpTriUv.Length; j++)
                        {
                            if (!rd.IsInside(tmpTriUv[j])) tmpTriPoint[j] = rd.RestrictedPointAt(tmpTriUv[j]);
                        }
                    }

                    lock (lockTriangulationData)
                    {
                        triangleUVPoint = tmpTriUv;
                        trianglePoint = tmpTriPoint;
                        triangleIndex = tmpTriInd;
                    }
#if DEBUG
                    DebuggerContainer dc3d = new DebuggerContainer();
                    DebuggerContainer dc2d = new DebuggerContainer();
                    BoundingCube bc = BoundingCube.EmptyBoundingCube;
                    for (int i = 0; i < trianglePoint.Length; i++)
                    {
                        bc.MinMax(trianglePoint[i]);
                    }
                    if (bc.Xmax > 200)
                    { }
                    for (int i = 0; i < triangleIndex.Length; i += 3)
                    {
                        GeoPoint2D tr1 = new GeoPoint2D(triangleUVPoint[triangleIndex[i]].x / ext.Width, triangleUVPoint[triangleIndex[i]].y / ext.Height);
                        GeoPoint2D tr2 = new GeoPoint2D(triangleUVPoint[triangleIndex[i + 1]].x / ext.Width, triangleUVPoint[triangleIndex[i + 1]].y / ext.Height);
                        GeoPoint2D tr3 = new GeoPoint2D(triangleUVPoint[triangleIndex[i + 2]].x / ext.Width, triangleUVPoint[triangleIndex[i + 2]].y / ext.Height);
                        dc2d.Add(new Line2D(tr1, tr2), System.Drawing.Color.Blue, i);
                        dc2d.Add(new Line2D(tr2, tr3), System.Drawing.Color.Blue, i);
                        dc2d.Add(new Line2D(tr3, tr1), System.Drawing.Color.Blue, i);
                        GeoPoint2D cnt = new GeoPoint2D(tr1, tr2, tr3);
                        if ((new GeoVector(tr2 - tr1) ^ new GeoVector(tr3 - tr1)).z > 0) dc2d.Add(cnt, System.Drawing.Color.Red, i);
                        else dc2d.Add(cnt, System.Drawing.Color.Green, i);
                        Line ln = Line.Construct();
                        ln.SetTwoPoints(trianglePoint[triangleIndex[i]], trianglePoint[triangleIndex[i + 1]]);
                        dc3d.Add(ln, System.Drawing.Color.Blue, triangleIndex[i]);
                        ln = Line.Construct();
                        ln.SetTwoPoints(trianglePoint[triangleIndex[i + 1]], trianglePoint[triangleIndex[i + 2]]);
                        dc3d.Add(ln, System.Drawing.Color.Blue, triangleIndex[i + 1]);
                        ln = Line.Construct();
                        ln.SetTwoPoints(trianglePoint[triangleIndex[i + 2]], trianglePoint[triangleIndex[i]]);
                        dc3d.Add(ln, System.Drawing.Color.Blue, triangleIndex[i + 2]);
                        GeoPoint cnt3d = new GeoPoint(trianglePoint[triangleIndex[i]], trianglePoint[triangleIndex[i + 1]], trianglePoint[triangleIndex[i + 2]]);
                        GeoVector n = (trianglePoint[triangleIndex[i + 1]] - trianglePoint[triangleIndex[i]]) ^ (trianglePoint[triangleIndex[i + 2]] - trianglePoint[triangleIndex[i]]);
                        n.Length = bc.Size * 0.1;
                        ln = Line.Construct();
                        ln.SetTwoPoints(cnt3d, cnt3d + n);
                        dc3d.Add(ln, System.Drawing.Color.Red, triangleIndex[i]);
                    }
#endif
                }
                else
                {   // there are inner intersections: we need to split the outline and holes to make several non intersecting areas
#if DEBUG
                    DebuggerContainer dc1 = new DebuggerContainer();
                    for (int j = 0; j < polylines.Count; ++j)
                    {
                        if (polylines[j].Length > 5000)
                        { }
                        for (int i = 0; i < polylines[j].Length; ++i)
                        {
                            int next = i + 1;
                            if (i == polylines[j].Length - 1)
                                next = 0;
                            GeoPoint2D sp = new GeoPoint2D(polylines[j][i].x / ext.Width, polylines[j][i].y / ext.Height);
                            GeoPoint2D ep = new GeoPoint2D(polylines[j][next].x / ext.Width, polylines[j][next].y / ext.Height);
                            Line2D l2d = new Line2D(sp, ep);
                            dc1.Add(l2d);
                        }
                    }
                    //lock (lockTriangulationData)
                    //{
                    //    triangleUVPoint = new GeoPoint2D[0];
                    //    trianglePoint = new GeoPoint[0];
                    //    triangleIndex = new int[0];
                    //}
                    //return;
#endif
                    List<GeoPoint2D> sumTriUv = new List<GeoPoint2D>();
                    List<GeoPoint> sumTriPoint = new List<GeoPoint>();
                    List<int> sumTriInd = new List<int>();
                    GeoPoint2D[][][] multiPolyLines = SubdevidePolylines(polylines.ToArray(), eps);
                    for (int i = 0; i < multiPolyLines.Length; ++i)
                    {
                        GeoPoint2D[] tmpTriUv;
                        GeoPoint[] tmpTriPoint;
                        int[] tmpTriInd;
                        t = new Triangulation(multiPolyLines[i], surface, precision, 0.17);
                        if (!t.innerIntersection)
                        {
                            t.GetSimpleTriangles(out tmpTriUv, out tmpTriPoint, out tmpTriInd, true);
                            for (int j = 0; j < tmpTriInd.Length; ++j)
                            {
                                tmpTriInd[j] += sumTriPoint.Count;
                            }
                            sumTriPoint.AddRange(tmpTriPoint);
                            sumTriUv.AddRange(tmpTriUv);
                            sumTriInd.AddRange(tmpTriInd);
                            if (Surface is IRestrictedDomain rd)
                            {   // in this case, where we have self intersecting polygons, the polygon intersection points may be outside the defined area of the surface.
                                // This is especially the case, when there is a cylindrical surface, which has usually a round inner hole. When this hole is split, 
                                // we get points far outside the domain of the cylinder, which look ugly. Here we restrict these points into the domain
                                for (int j = 0; j < tmpTriUv.Length; j++)
                                {
                                    if (!rd.IsInside(tmpTriUv[j])) tmpTriPoint[j] = rd.RestrictedPointAt(tmpTriUv[j]);
                                }
                            }
#if DEBUG
                            BoundingCube bc = BoundingCube.EmptyBoundingCube;
                            for (int k = 0; k < tmpTriPoint.Length; k++)
                            {
                                bc.MinMax(tmpTriPoint[k]);
                            }
                            if (bc.Xmax > 10)
                            { }
#endif
                        }
                    }
                    //HashSet<int> invalidIndices = new HashSet<int>();
                    //for (int j = 0; j < sumTriUv.Count; j++)
                    //{
                    //    GeoPoint2D pp = sumTriUv[j];
                    //    if (!this.Contains(ref pp, true))
                    //    {
                    //        invalidIndices.Add(j);
                    //    }
                    //}
                    //if (invalidIndices.Count > 0)
                    //{
                    //    for (int j = sumTriInd.Count - 1; j >= 0; j -= 3)
                    //    {
                    //        if (invalidIndices.Contains(sumTriInd[j]) || invalidIndices.Contains(sumTriInd[j] - 1) || invalidIndices.Contains(sumTriInd[j - 2]))
                    //        {
                    //            sumTriInd.RemoveRange(j - 2, 3);
                    //        }
                    //    }
                    //}
                    lock (lockTriangulationData)
                    {
                        triangleUVPoint = sumTriUv.ToArray();
                        trianglePoint = sumTriPoint.ToArray();
                        triangleIndex = sumTriInd.ToArray();
                    }
                }
            }
            catch (ApplicationException)
            {   // something went wrong with the triangulation. This should not happen and needs to be debugged and fixed
            }
            triangleExtent = BoundingCube.EmptyBoundingCube; // needs to be recalculated
        }
        /// <summary>
        /// Returns true, when the <paramref name="otherFace"/> has a geometrically equal surface
        /// </summary>
        /// <param name="otherFace"></param>
        /// <returns></returns>
        public bool SameSurface(Face otherFace)
        {
            if (otherFace == null) return false;
            ModOp2D fts;
            return surface.SameGeometry(Area.GetExtent(), otherFace.Surface, otherFace.Area.GetExtent(), Precision.eps, out fts);
        }

        internal bool SameSurface(Face otherFace, out ModOp2D fts)
        {
            return surface.SameGeometry(Area.GetExtent(), otherFace.Surface, otherFace.Area.GetExtent(), Precision.eps, out fts);
        }

        internal void ForceAreaRecalc()
        {
            area = null;
            area = Area;
            if (surface is ISurfaceImpl si) si.usedArea = Domain;
        }

        internal void ClearVertices()
        {
            vertices = null;
        }
        /// <summary>
        /// Find inner intersection curves of two faces. This is only used to find intersection curves, which do not cross the edges of the faces. (but might also find and return such curves)
        /// </summary>
        /// <param name="other">the other face</param>
        /// <returns>Array of inner intersection curves</returns>
        public ICurve[] GetInnerIntersection(Face other)
        {
            if (!this.GetBoundingCube().Interferes(other.GetBoundingCube())) return new ICurve[0];
            if (surface is PlaneSurface)
            {   // there is no inner intersection with a plane and one of these surfaces (i.e. all intersection curves must also intersect
                if (other.surface is CylindricalSurface || other.surface is ConicalSurface || other.surface is SurfaceOfLinearExtrusion || other.surface is SurfaceOfRevolution) return new ICurve[0];
            }
            // and vice versa
            if (other.surface is PlaneSurface)
            {   // there is no inner intersection with a plane and one of these surfaces (i.e. all intersection curves must also intersect
                if (surface is CylindricalSurface || surface is ConicalSurface || surface is SurfaceOfLinearExtrusion || surface is SurfaceOfRevolution) return new ICurve[0];
            }
            // restriction on the Area of the faces are not calculated
            // there is no clipping done, since we do expect the curve to be totally inside both faces
            ICurve[] cvs = this.surface.Intersect(this.Area.GetExtent(), other.Surface, other.Area.GetExtent());
            List<ICurve> res = new List<ICurve>();
            if (cvs != null)
            {
                for (int i = 0; i < cvs.Length; i++)
                {
                    GeoPoint2D sp = surface.PositionOf(cvs[i].StartPoint);
                    if (Contains(cvs[i].StartPoint, true) && other.Contains(cvs[i].StartPoint, true)) res.Add(cvs[i]);
                }
            }
            return res.ToArray();
        }

        [Obsolete("Has been renamed to \"GetInnerIntersection\"")]
        public ICurve[] Intersect(Face other)
        {
            return GetInnerIntersection(other);
        }

        public void GetSimpleTriangulation(double precision, bool noInnerPoints, out GeoPoint[] trianglePoint, out GeoPoint2D[] triangleUVPoint, out int[] triangleIndex, out int[] edgeIndizes)
        {
            edgeIndizes = new int[0]; // damit es gesetzt ist;

            SimpleShape ss = Area; // damit es sicher bestimmt ist
                                   // 1. Area in Polygone aufteilen
            List<GeoPoint2D[]> polylines = new List<GeoPoint2D[]>();
            List<GeoPoint2D> polyoutline = new List<GeoPoint2D>();

            ICurve2D[] usedCurves = new ICurve2D[outline.Length];
            for (int i = 0; i < outline.Length; ++i)
            {
                usedCurves[i] = outline[i].Curve2D(this, usedCurves);
                GeoPoint2D[] points = outline[i].GetTriangulationBasis(this, precision, usedCurves[i]);
                if (outline.Length == 1 && points.Length < 3)
                {   // das würde ja bedeuten, nur zwei Punkte auf der Outline, also nur ein Strich, hin- und zurück
                    points = new GeoPoint2D[3];
                    points[0] = usedCurves[i].PointAt(0.0);
                    points[1] = usedCurves[i].PointAt(1.0 / 3.0);
                    points[2] = usedCurves[i].PointAt(2.0 / 3.0);
                }
                polyoutline.AddRange(points);
            }
            GeoPoint2D[] tmp = polyoutline.ToArray();
            BoundingRect ext = new BoundingRect(tmp);
            double eps = Math.Min((ext.Width * ext.Height) * 1e-6, precision);
            // Problem mit der Genauigkeit: eine sehr große Fläche mit kleinen Löchern führt dazu, dass eps zu groß wird
            // und die Löcher verschwinden
            bool removed = false;
#if DEBUG
            Polyline2D pl2d = new Polyline2D(polyoutline.ToArray());
            DebuggerContainer dc1 = new DebuggerContainer();
            dc1.Add(pl2d, System.Drawing.Color.Red, 0);
#endif
            for (int i = polyoutline.Count - 2; i >= 0; --i)
            {
                if ((polyoutline[i] | polyoutline[i + 1]) < eps)
                {
                    polyoutline.RemoveAt(i);
                    removed = true;
                }
            }
            if (polyoutline.Count > 0 && (polyoutline[0] | polyoutline[polyoutline.Count - 1]) < eps)
            {
                polyoutline.RemoveAt(polyoutline.Count - 1);
                removed = true;
            }
            if (surface is NurbsSurface)
            {   // bei NURBS Flächen Punkte außerhalb des Definitionsbereices in den Definitionsbereich schieben
                double umin, umax, vmin, vmax;
                surface.GetNaturalBounds(out umin, out umax, out vmin, out vmax);
                if (!surface.IsUPeriodic)
                {
                    for (int i = polyoutline.Count - 1; i >= 0; --i)
                    {
                        //if (polyoutline[i].x > umax || polyoutline[i].x < umin)
                        //{
                        //    polyoutline.RemoveAt(i);
                        //    removed = true;
                        //}
                        // nicht verwerfen sondern reinschieben
                        if (polyoutline[i].x > umax)
                        {
                            polyoutline[i] = new GeoPoint2D(umax, polyoutline[i].y);
                            removed = true; // damit polyoutline übernommen wird
                        }
                        if (polyoutline[i].x < umin)
                        {
                            polyoutline[i] = new GeoPoint2D(umin, polyoutline[i].y);
                            removed = true;
                        }
                    }
                }
                if (!surface.IsVPeriodic)
                {
                    for (int i = polyoutline.Count - 1; i >= 0; --i)
                    {
                        //if (polyoutline[i].y > vmax || polyoutline[i].y < vmin)
                        //{
                        //    polyoutline.RemoveAt(i);
                        //    removed = true;
                        //}
                        // nicht verwerfen sondern reinschieben
                        if (polyoutline[i].y > vmax)
                        {
                            polyoutline[i] = new GeoPoint2D(polyoutline[i].x, vmax);
                            removed = true; // damit polyoutline übernommen wird
                        }
                        if (polyoutline[i].y < vmin)
                        {
                            polyoutline[i] = new GeoPoint2D(polyoutline[i].x, vmin);
                            removed = true;
                        }
                    }
                }
            }
            if (removed) tmp = polyoutline.ToArray();
#if DEBUG
            pl2d = new Polyline2D(polyoutline.ToArray());
            DebuggerContainer dc2 = new DebuggerContainer();
            dc2.Add(pl2d, System.Drawing.Color.Red, 0);
#endif
            if (GeoPoint2D.Area(tmp) < eps)
            {   // kommt manchmal vor: zwei entgegenlaufende Linien, hier also leeres Polygon oder falschrum
                if (GeoPoint2D.Area(tmp) < -eps)
                {
                    Array.Reverse(tmp);
                }
                else
                {
                    lock (lockTriangulationData)
                    {
                        triangleUVPoint = new GeoPoint2D[0];
                        trianglePoint = new GeoPoint[0];
                        triangleIndex = new int[0];
                        edgeIndizes = new int[0];
                    }
                    return;
                }
            }
            polylines.Add(tmp);
            polyoutline.Clear();
            for (int i = 0; i < holes.Length; ++i)
            {
                for (int j = 0; j < holes[i].Length; ++j)
                {
                    GeoPoint2D[] points = holes[i][j].GetTriangulationBasis(this, precision, holes[i][j].Curve2D(this)); // letzter Punkt fehlt
                    polyoutline.AddRange(points);
                }
                if (polyoutline.Count == 1) polyoutline.Clear();
                // polyoutline.Reverse();
                tmp = polyoutline.ToArray();
                // Doppelpunkte entfernen
                removed = false;
                for (int j = polyoutline.Count - 2; j >= 0; --j)
                {
                    if ((polyoutline[j] | polyoutline[j + 1]) < eps)
                    {
                        // polyoutline.RemoveAt(j + 1); nicht j+1, sonst frisst sich die polyline von hinten her auf
                        // wenn alle Abstände zu klein sind
                        polyoutline.RemoveAt(j);
                        removed = true;
                    }
                }
                if (polyoutline.Count > 1)
                {
                    if ((polyoutline[0] | polyoutline[polyoutline.Count - 1]) < eps)
                    {
                        polyoutline.RemoveAt(polyoutline.Count - 1);
                        removed = true;
                    }
                }
                if (removed) tmp = polyoutline.ToArray();
                if (tmp.Length > 2) polylines.Add(tmp); // geändert auf "2", denn es müssen mehr als 2 Punkte sein, sonst nur ein Schlitz (siehe VM4421.10.147_TM1.stp)
                polyoutline.Clear();
            }
            try
            {

#if DEBUG
                DebuggerContainer dc = new DebuggerContainer();
                for (int j = 0; j < polylines.Count; ++j)
                {
                    for (int i = 0; i < polylines[j].Length; ++i)
                    {
                        int next = i + 1;
                        if (i == polylines[j].Length - 1)
                            next = 0;
                        GeoPoint2D sp = new GeoPoint2D(polylines[j][i].x / ext.Width, polylines[j][i].y / ext.Height);
                        GeoPoint2D ep = new GeoPoint2D(polylines[j][next].x / ext.Width, polylines[j][next].y / ext.Height);
                        Line2D l2d = new Line2D(sp, ep);
                        dc.Add(l2d);
                    }
                }
#endif
                int tc0 = System.Environment.TickCount;
                int esum = 0;
                edgeIndizes = new int[polylines.Count];
                for (int j = 0; j < polylines.Count; ++j)
                {
                    esum += polylines[j].Length;
                    edgeIndizes[j] = esum;
                }
                Triangulation t = new Triangulation(polylines.ToArray(), surface, precision * 5.0, 0.17);
                // Genauigkeit für innere Punkte nur halbsoviel wie für den Rand, dort fallen die Knicke nicht so auf
                GeoPoint2D[] innerPoints = null;
                if (surface is NurbsSurface)
                {
                    double[] intu, intv;
                    double umin, umax, vmin, vmax;
                    surface.GetNaturalBounds(out umin, out umax, out vmin, out vmax);
                    surface.GetSafeParameterSteps(umin, umax, vmin, vmax, out intu, out intv);
                    List<GeoPoint2D> iplist = new List<GeoPoint2D>();
                    // erste und letzte weglassen, es interessieren nur die inneren
                    for (int i = 1; i < intu.Length - 1; i++)
                    {
                        for (int j = 1; j < intv.Length - 1; j++)
                        {
                            GeoPoint2D ip2d = new GeoPoint2D(intu[i], intv[j]);
                            if (ss.Contains(ip2d, false))
                            {
                                iplist.Add(ip2d);
                            }
                            else
                            {
                            }
                        }
                    }
                    innerPoints = iplist.ToArray();
                }
                t.GetSimpleTriangles(out triangleUVPoint, out trianglePoint, out triangleIndex, !noInnerPoints);
                int tc1 = System.Environment.TickCount - tc0;
                // System.Diagnostics.Trace.WriteLine("Triangulierung: " + this.hashCode.ToString() + ", " + tc1.ToString());
#if DEBUG
                BoundingCube bc = BoundingCube.EmptyBoundingCube;
                for (int i = 0; i < trianglePoint.Length; i++)
                {
                    bc.MinMax(trianglePoint[i]);
                }
                for (int i = 0; i < triangleIndex.Length; i += 3)
                {
                    GeoPoint2D tr1 = new GeoPoint2D(triangleUVPoint[triangleIndex[i]].x / ext.Width, triangleUVPoint[triangleIndex[i]].y / ext.Height);
                    GeoPoint2D tr2 = new GeoPoint2D(triangleUVPoint[triangleIndex[i + 1]].x / ext.Width, triangleUVPoint[triangleIndex[i + 1]].y / ext.Height);
                    GeoPoint2D tr3 = new GeoPoint2D(triangleUVPoint[triangleIndex[i + 2]].x / ext.Width, triangleUVPoint[triangleIndex[i + 2]].y / ext.Height);
                    dc.Add(new Line2D(tr1, tr2));
                    dc.Add(new Line2D(tr2, tr3));
                    dc.Add(new Line2D(tr3, tr1));
                }
#endif
            }
            catch (ApplicationException)
            {   // Selbstüberschneidungen von Löchern und Rand oder in den Rändern selbst
                List<GeoPoint2D> sumTriUv = new List<GeoPoint2D>();
                List<GeoPoint> sumTriPoint = new List<GeoPoint>();
                List<int> sumTriInd = new List<int>();
                try
                {
                    GeoPoint2D[][][] multiPolyLines = SubdevidePolylines(polylines.ToArray(), eps);
                    for (int i = 0; i < multiPolyLines.Length; ++i)
                    {
                        try
                        {
                            GeoPoint2D[] tmpTriUv;
                            GeoPoint[] tmpTriPoint;
                            int[] tmpTriInd;
                            Triangulation t = new Triangulation(multiPolyLines[i], surface, precision, 0.17);
                            GeoPoint2D[] innerPoints = null;
                            if (surface is NurbsSurface)
                            {
                                double[] intu, intv;
                                double umin, umax, vmin, vmax;
                                surface.GetNaturalBounds(out umin, out umax, out vmin, out vmax);
                                surface.GetSafeParameterSteps(umin, umax, vmin, vmax, out intu, out intv);
                                List<GeoPoint2D> iplist = new List<GeoPoint2D>();
                                // erste und letzte weglassen, es interessieren nur die inneren
                                for (int k = 1; k < intu.Length - 1; k++)
                                {
                                    for (int j = 1; j < intv.Length - 1; j++)
                                    {
                                        GeoPoint2D ip2d = new GeoPoint2D(intu[k], intv[j]);
                                        if (ss.Contains(ip2d, false))
                                        {
                                            iplist.Add(ip2d);
                                        }
                                    }
                                }
                                innerPoints = iplist.ToArray();
                            }
                            t.GetSimpleTriangles(out tmpTriUv, out tmpTriPoint, out tmpTriInd, !noInnerPoints);
                            for (int j = 0; j < tmpTriInd.Length; ++j)
                            {
                                tmpTriInd[j] += sumTriPoint.Count;
                            }
                            sumTriPoint.AddRange(tmpTriPoint);
                            sumTriUv.AddRange(tmpTriUv);
                            sumTriInd.AddRange(tmpTriInd);
                        }
                        catch (ApplicationException)
                        {
                        }
                    }
                }
                catch (ApplicationException)
                {
                }
                catch (IndexOutOfRangeException)
                {
                }
                triangleUVPoint = sumTriUv.ToArray();
                trianglePoint = sumTriPoint.ToArray();
                triangleIndex = sumTriInd.ToArray();
            }
        }
        private GeoPoint2D[][][] SubdevidePolylines(GeoPoint2D[][] polylines, double eps)
        {
            CompoundShape cs;
            Border bdr = new Border(polylines[0]);
            double[] sis = bdr.GetSelfIntersection(Precision.eps);
            if (sis.Length > 0)
            {
                // multiple of three values: parameter1, parameter2, crossproduct of intersection direction
                // there can only be one intersection
                cs = new CompoundShape();
                List<double> splitpos = new List<double>();
                for (int i = 0; i < sis.Length; i += 3)
                {
                    ICurve2D[] part1 = bdr.GetPart(sis[i], sis[i + 1], sis[i + 2] < 0);
                    Border bdr1 = new Border(part1);
                    cs.UniteDisjunct(bdr1);
                }
            }
            else
            {
                cs = new CompoundShape(new SimpleShape(new Border(polylines[0])));
            }
            double shrink = cs.GetExtent().Size * 1e-6;
            for (int i = 1; i < polylines.Length; ++i)
            {
                Array.Reverse(polylines[i]);
                if (polylines[i].Length > 0)
                {
                    SimpleShape hole = new SimpleShape(new Border(polylines[i]));
                    cs = cs - hole.Shrink(shrink); // Löcher etwas verkleinern... hilft bei "ASW02880-330000-002 05.stp"
                                                   // also bei Faces, die Löcher haben, die den Rand berühren
                                                   // cs.Subtract(hole);
                }
            }
            List<GeoPoint2D[][]> res = new List<GeoPoint2D[][]>();
            for (int i = 0; i < cs.SimpleShapes.Length; ++i)
            {
                List<GeoPoint2D[]> pol = new List<GeoPoint2D[]>();
                SimpleShape ss = cs.SimpleShapes[i];
                GeoPoint2D[] outline = ss.Outline.Vertices;
                if (GeoPoint2D.Area(outline) > eps)
                {
                    pol.Add(outline);
                    for (int j = 0; j < ss.NumHoles; ++j)
                    {
                        GeoPoint2D[] hole = ss.Holes[j].Vertices;
                        Array.Reverse(hole);
                        if (GeoPoint2D.Area(hole) < -eps && !GeoPoint2D.InnerIntersection(hole))
                        {
                            pol.Add(hole);
                        }
                    }
                    res.Add(pol.ToArray());
                }
            }
            return res.ToArray();
        }
        private void ClearTriangulation()
        {
            if (trianglePoint != null)
            {
                lock (trianglePoint)
                {
                    trianglePoint = null;
                    triangleUVPoint = null;
                    triangleIndex = null;
                    trianglePrecision = 0.0;
                }
            }
        }
        private void TryAssureTriangles(double precision)
        {   // es muss so gehen: der Zugriff auf trianglePoint etc muss zusätzlich gelocked werden.
            // wenn es eine schlechte Triangulierung gibt und gerade eine bessere in Arbeit ist, dann soll man 
            // halt solange die schlechte nehmen
            if (Monitor.TryEnter(lockTriangulationRecalc))
            {
                try
                {
                    AssureTriangles(precision);
                }
                finally
                {
                    Monitor.Exit(lockTriangulationRecalc);
                }
            }
            else
            {
                if (trianglePoint == null)
                {   // hier muss es sein
                    AssureTriangles(precision);
                }
            }
        }
        static int maxtime = 0;
        internal void AssureTriangles(double precision)
        {
            lock (lockTriangulationRecalc)
            {   // hier gelocked, damit der der warten muss dann das richtige Ergebnis bekommt
                // ein Problem allerdings, wenn Triangulate sehr lange dauert, das müsste irgendwie abbrechbar sein
                // oder selbst drauf achten, dass es sich nicht verläuft
                if (precision == 0.0)
                {
                    if (trianglePrecision > 0.0) return; // dann wird die bereits bestehende Triangulierung genommen
                    precision = this.GetBoundingCube().Size / 10; // wenn noch nie dargestellt und mit precision 0.0 aufgerufen, dann halt irgendwas nehmen
                                                                  // kommt bei LTB vor.
                                                                  // throw new ApplicationException("internal error"); // darf nicht vorkommen
                }
                if (surface == null) return;
                if (trianglePoint != null && precision >= trianglePrecision / 2.0) return;
                Triangulate(precision);
                trianglePrecision = precision;
                // triangleOctTree is implemented but not used. Could maybe used in Face.HitTest
                //if (trianglePoint != null)
                //{
                //    triangleExtent = BoundingCube.EmptyBoundingCube;
                //    for (int i = 0; i < trianglePoint.Length; ++i)
                //    {
                //        triangleExtent.MinMax(trianglePoint[i]);
                //    }
                //    triangleOctTree = new OctTree<TraingleOctTree>(triangleExtent, precision);
                //    for (int i = 0; i < triangleIndex.Length; i = i + 3)
                //    {
                //        triangleOctTree.AddObject(new TraingleOctTree(this, i));
                //    }
                //}
                return;
            }

        }
        internal void PaintFaceTo3D(IPaintTo3D paintTo3D)
        {
            // if DontRecalcTriangulation is true, then work with the already existing triangulation, as long as there is one
            if (!paintTo3D.DontRecalcTriangulation || trianglePoint == null)
                TryAssureTriangles(paintTo3D.Precision);
            lock (lockTriangulationData)
            {
                if (trianglePoint != null)
                {
                    if (paintTo3D.SelectMode)
                    {
                        paintTo3D.SetColor(paintTo3D.SelectColor); // was disabled, why?
                    }
                    else
                    {
                        if (colorDef != null)
                        {
                            if (Layer != null && Layer.Transparency > 0)
                                paintTo3D.SetColor(Color.FromArgb(255 - Layer.Transparency, colorDef.Color));
                            else
                                paintTo3D.SetColor(colorDef.Color);
                        }
                    }
                    GeoVector[] normals = new GeoVector[trianglePoint.Length];
                    for (int i = 0; i < trianglePoint.Length; ++i)
                    {
                        normals[i] = surface.GetNormal(triangleUVPoint[i]);
                        normals[i].NormIfNotNull();

                    }
                    paintTo3D.Triangle(trianglePoint, normals, triangleIndex);
                }
            }
        }
        public delegate bool PaintTo3DDelegate(Face toPaint, IPaintTo3D paintTo3D);
        public static PaintTo3DDelegate OnPaintTo3D;
        /// <summary>
        /// Overrides <see cref="CADability.GeoObject.IGeoObjectImpl.PaintTo3D (IPaintTo3D)"/>
        /// </summary>
        /// <param name="paintTo3D"></param>
        public override void PaintTo3D(IPaintTo3D paintTo3D)
        {
            if (surface == null) return; // has not been fully initialized
            if (OnPaintTo3D != null && OnPaintTo3D(this, paintTo3D)) return;
            if (paintTo3D.PaintSurfaces)
            {
                PaintFaceTo3D(paintTo3D);
            }
            if (paintTo3D.PaintEdges && paintTo3D.PaintSurfaceEdges)
            {
                paintTo3D.SetLineWidth(null);
                for (int i = 0; i < outline.Length; ++i)
                {
                    outline[i].PaintTo3D(paintTo3D);
                }
                for (int i = 0; i < holes.Length; ++i)
                {
                    for (int j = 0; j < holes[i].Length; ++j)
                    {
                        holes[i][j].PaintTo3D(paintTo3D);
                    }
                }
            }
        }
        /// <summary>
        /// Overrides <see cref="CADability.GeoObject.IGeoObjectImpl.PaintTo3DList (IPaintTo3D, ICategorizedDislayLists)"/>
        /// </summary>
        /// <param name="paintTo3D"></param>
        /// <param name="lists"></param>
        public override void PaintTo3DList(IPaintTo3D paintTo3D, ICategorizedDislayLists lists)
        {
            if (Layer != null && Layer.Transparency > 0)
                lists.Add(Layer, false, false, this); // so kommts in die transparentliste
            else
                lists.Add(Layer, true, true, this);
            // es wird in beide Listen eingefügt, dann wird PaintTo3D zweimal aufgerufen, einmal mit
            // paintTo3D.PaintSurfaces und einmal mit paintTo3D.PaintEdges
        }
        /// <summary>
        /// Overrides <see cref="CADability.GeoObject.IGeoObjectImpl.PrepareDisplayList (double)"/>
        /// </summary>
        /// <param name="precision"></param>
        public override void PrepareDisplayList(double precision)
        {
            TryAssureTriangles(precision);
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
            if (extentPrecision == ExtentPrecision.Raw)
            {   // when we have closed edges, e.g. a non periodic cylinder defined by two circles, then testing vertices only is not a good idea
                if (trianglePrecision <= projection.Precision * 10 && trianglePrecision > 0.0)
                {   // if we already have a reasonable triangulation, use it, because it is probably faster
                    lock (lockTriangulationData)
                    {
                        if (trianglePoint != null)
                        {
                            for (int i = 0; i < trianglePoint.Length; ++i)
                            {
                                res.MinMax(projection.ProjectUnscaled(trianglePoint[i]));
                            }
                        }
                    }
                }
                else
                {
                    for (int i = 0; i < Vertices.Length; i++)
                    {
                        res.MinMax(projection.ProjectUnscaled(Vertices[i].Position));
                    }
                }
            }
            else
            {
                TryAssureTriangles(projection.Precision);
                lock (lockTriangulationData)
                {
                    if (trianglePoint != null)
                    {
                        for (int i = 0; i < trianglePoint.Length; ++i)
                        {
                            res.MinMax(projection.ProjectUnscaled(trianglePoint[i]));
                        }
                    }
                }
            }
            return res;
        }
        internal class FaceQuadTree : IQuadTreeInsertableZ
        {   // vermutlich nicht mehr in Gebrauch
            Face face;
            Projection projection;
            BoundingRect ext;
            GeoPoint2D[] trianglePoints;
            int[] triangleIndex;
            public FaceQuadTree(Face face, Projection projection)
            {
                this.face = face;
                this.projection = projection;
                ext = BoundingRect.EmptyBoundingRect;
                face.AssureTriangles(projection.Precision);
                if (face.trianglePoint != null)
                {
                    trianglePoints = new GeoPoint2D[face.trianglePoint.Length];
                    triangleIndex = (int[])face.triangleIndex.Clone();
                    for (int i = 0; i < face.trianglePoint.Length; ++i)
                    {
                        trianglePoints[i] = projection.ProjectUnscaled(face.trianglePoint[i]);
                        ext.MinMax(trianglePoints[i]);
                    }
                }
            }
            #region IQuadTreeInsertable Members
            BoundingRect IQuadTreeInsertable.GetExtent()
            {
                return ext;
            }
            bool IQuadTreeInsertable.HitTest(ref BoundingRect rect, bool includeControlPoints)
            {
                if (BoundingRect.Disjoint(ext, rect)) return false;
                if (trianglePoints != null)
                {
                    ClipRect clr = new ClipRect(rect);
                    for (int i = 0; i < triangleIndex.Length; i += 3)
                    {
                        if (clr.TriangleHitTest(trianglePoints[triangleIndex[i]], trianglePoints[triangleIndex[i + 1]], trianglePoints[triangleIndex[i + 2]])) return true;
                    }
                }
                return false;
            }
            object IQuadTreeInsertable.ReferencedObject
            {
                get { return face; }
            }
            #endregion
            #region IQuadTreeInsertableZ Members
            double IQuadTreeInsertableZ.GetZPosition(GeoPoint2D p)
            {   // das ist quick and dirty: besser mit QuadTree 
                double z = double.MinValue;
                face.AssureTriangles(projection.Precision);
                if (face.trianglePoint != null)
                {
                    for (int i = 0; i < face.triangleIndex.Length; i += 3)
                    {
                        GeoPoint2D p1 = projection.ProjectUnscaled(face.trianglePoint[face.triangleIndex[i]]);
                        GeoPoint2D p2 = projection.ProjectUnscaled(face.trianglePoint[face.triangleIndex[i + 1]]);
                        GeoPoint2D p3 = projection.ProjectUnscaled(face.trianglePoint[face.triangleIndex[i + 2]]);
                        // p1 + l1*(p2-p1) + l2*(p3-p1) = p
                        // Mit dieser Gleichung kann man gleichzeitig feststellen ob innerhalb und den Z-Wert
                        double l1, l2;
                        if (Geometry.FastSolve22(p2.x - p1.x, p3.x - p1.x, p2.y - p1.y, p3.y - p1.y, p.x - p1.x, p.y - p1.y, out l1, out l2))
                        {
                            if (l1 >= 0 && l2 >= 0 && l1 + l2 <= 1.0)
                            {   // im Dreieck
                                GeoPoint pz = face.trianglePoint[face.triangleIndex[i]] + l1 * (face.trianglePoint[face.triangleIndex[i + 1]] - face.trianglePoint[face.triangleIndex[i]]) + l2 * (face.trianglePoint[face.triangleIndex[i + 2]] - face.trianglePoint[face.triangleIndex[i]]);
                                pz = projection.UnscaledProjection * pz;
                                if (pz.z > z) z = pz.z;
                            }
                        }
                    }
                }
                return z;
            }
            #endregion
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
                res.Add(new FaceQuadTree(this, projection));
            }
            // Edges kommen immer
            Edge[] allEdges = AllEdges;
            for (int i = 0; i < allEdges.Length; ++i)
            {
                if (allEdges[i].Curve3D != null)
                {
                    res.Add((allEdges[i].Curve3D as IGeoObject).GetQuadTreeItem(projection, extentPrecision));
                }
            }
            return res;
        }
        public override IGeoObject[] OwnedItems
        {
            get
            {   // schwierig, wie soll man this und die Edges liefern?
                return null;
            }
        }
        public override string Description
        {
            get
            {
                return StringTable.GetString("Face.Description");
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
            // if (trianglePoint == null) AssureTriangles(precision); // eine muss es geben
            return GetBoundingCube();
            //if (trianglePoint == null) AssureTriangles(precision); // eine muss es geben
            //lock (lockTriangulationRecalc)
            //{
            //    if (triangleExtent.IsEmpty)
            //    {
            //        lock (lockTriangulationData)
            //        {
            //            if (trianglePoint != null)
            //            {
            //                for (int i = 0; i < trianglePoint.Length; ++i)
            //                {
            //                    triangleExtent.MinMax(trianglePoint[i]);
            //                }
            //            }
            //        }
            //    }
            //    BoundingCube dbg = GetBoundingCube();
            //    if (dbg.Size > triangleExtent.Size*1.1)
            //    {
            //    }
            //    return triangleExtent;
            //}
        }
        /// <summary>
        /// Checks whether the provided 2d point in the parameter space of the surface is inside the bounds of this face.
        /// If the surface is periodic then it will also be checked whether the point with its periodic offset
        /// is contained in the face. In this case, the 2d coordinates of the point are updated to reflect the correct
        /// period in which it is inside the face
        /// </summary>
        /// <param name="p">The point to check</param>
        /// <param name="acceptOnCurve">Also accept points on the outline</param>
        /// <returns>true if the point is inside the face</returns>
        public bool Contains(ref GeoPoint2D p, bool acceptOnCurve)
        {
            if (!surface.IsUPeriodic && !surface.IsVPeriodic)
            {
                return Area.Contains(p, acceptOnCurve);
            }
            double u1 = p.x;
            double v1 = p.y;
            double a = surface.UPeriod;
            double b = surface.VPeriod;
            double uMin, uMax, vMin, vMax;
            GetUVBounds(out uMin, out uMax, out vMin, out vMax);
            if (!surface.IsUPeriodic)
            {
                double v2 = (int)((v1 - vMin) / b);
                v1 -= v2 * b;
                while (v1 <= vMax + Precision.eps)
                {
                    if (Area.Contains(new GeoPoint2D(u1, v1), acceptOnCurve))
                    {
                        p = new GeoPoint2D(u1, v1);
                        return true;
                    }
                    v1 += b;
                }
                return false;
            }
            if (!surface.IsVPeriodic)
            {
                double u2 = (int)((u1 - uMin) / a);
                u1 -= u2 * a;
                while (u1 <= uMax + Precision.eps)
                {
                    if (Area.Contains(new GeoPoint2D(u1, v1), acceptOnCurve))
                    {
                        p = new GeoPoint2D(u1, v1);
                        return true;
                    }
                    u1 += a;
                }
                return false;
            }
            else
            {
                double u2 = (int)((u1 - uMin) / a);
                u1 -= u2 * a;
                double v2 = (int)((v1 - vMin) / b);
                v1 -= v2 * b;
                while (u1 <= uMax + Precision.eps)
                {
                    double temp = v1;
                    while (v1 <= vMax + Precision.eps)
                    {
                        if (Area.Contains(new GeoPoint2D(u1, v1), acceptOnCurve))
                        {
                            p = new GeoPoint2D(u1, v1);
                            return true;
                        }
                        v1 += b;
                    }
                    v1 = temp;
                    u1 += a;
                }
                return false;
            }
        }
        internal bool Contains(GeoPoint p, bool acceptOnCurve)
        {
            GeoPoint2D pos = surface.PositionOf(p);
            return Contains(ref pos, acceptOnCurve);
        }

        public bool HitBoundingCube(BoundingCube bc)
        {
            return HitBoundingCube(bc, GetBoundingCube());
        }
        public bool HitBoundingCube(BoundingCube bc, BoundingCube fbc)
        {
            //  any vertex in the cube?
            Vertex[] v = Vertices;
            for (int i = 0; i < v.Length; ++i)
            {
                if (bc.Contains(v[i].Position))
                    return true;
            }

            // argument of the face being connected ?? this looks strange!!!
            //if ((bc.Xmin <= fbc.Xmin && fbc.Xmax <= bc.Xmax
            //        && bc.Ymin <= fbc.Ymin && fbc.Ymax <= bc.Ymax)
            //    || (bc.Xmin <= fbc.Xmin && fbc.Xmax <= bc.Xmax
            //        && bc.Zmin <= fbc.Zmin && fbc.Zmax <= bc.Zmax)
            //    || (bc.Zmin <= fbc.Zmin && fbc.Zmax <= bc.Zmax
            //        && bc.Ymin <= fbc.Ymin && fbc.Ymax <= bc.Ymax))
            //{
            //    return true;
            //}
            // any edge interfering the cube?
            Edge[] e = AllEdges;
            for (int i = 0; i < e.Length; ++i)
            {
                ICurve c = e[i].Curve3D;
                if (c != null && c.HitTest(bc))
                    return true;
            }

            // face is connected & cube is convex
            // & cube doesn't hit any edge  (otherwise true)
            // & cube hits surface in at least one point 
            //                              (otherwise false)
            // outside of the face          (otherwise true)
            //  =>  cube doesn't hit the face
#if DEBUG
            if (hashCode == 3061) { }
#endif
            GeoPoint2D uv;
            return (Surface.HitTest(bc, out uv) && Contains(ref uv, true));
        }

        /// <summary>
        /// Overrides <see cref="CADability.GeoObject.IGeoObjectImpl.HitTest (ref BoundingCube, double)"/>
        /// </summary>
        /// <param name="cube"></param>
        /// <param name="precision"></param>
        /// <returns></returns>
        public override bool HitTest(ref BoundingCube cube, double precision)
        {
            // if (!GetExtent(precision).Interferes(cube)) return false;
            SimpleShape forecArea = Area;
            return HitBoundingCube(cube);
            ////if (surface is OffsetSurface || surface is NurbsSurface) // das führt zu extrem feinen Triangulierungen
            ////{
            ////    AssureTriangles(precision);
            ////}
            //if (trianglePoint == null || trianglePrecision > precision * 2)
            //{   // das ist die bessere da exakte Methode, das mit der Triangulierung sollte abgeschafft werden
            //    // macht aber ein paar Probleme bei z.B. bei 1362241.stp
            //    return HitBoundingCube(cube);
            //}
            //lock (lockTriangulationData)
            //{
            //    if (trianglePoint != null)
            //    {
            //        return cube.Interferes(trianglePoint, triangleIndex);
            //    }
            //}
            //return false;
        }
        // Hit-test for the interior of the face. the edges have already been tested
        internal bool HitTestWithoutEdges(ref BoundingCube cube, double precision)
        {
            // since the edges and vertices have already been tested we only have to test whether the cube interferes with the surface at all
            // and if so, whether an arbitrary uv point inside the cube is inside the bounds of the face.
            GeoPoint2D uv;
            return (Surface.HitTest(cube, out uv) && Contains(ref uv, false));
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
            if (trianglePoint == null) return false; // this is only for selecting with the mouse
            ClipRect clr = new ClipRect(ref rect);
            if (onlyInside)
            {
                lock (lockTriangulationData)
                {
                    for (int i = 0; i < triangleIndex.Length; i += 3)
                    {
                        GeoPoint2D p1 = projection.ProjectUnscaled(trianglePoint[triangleIndex[i]]);
                        GeoPoint2D p2 = projection.ProjectUnscaled(trianglePoint[triangleIndex[i + 1]]);
                        GeoPoint2D p3 = projection.ProjectUnscaled(trianglePoint[triangleIndex[i + 2]]);
                        if (!clr.TriangleHitTest(p1, p2, p3)) return false;
                    }
                }
                return true;
            }
            else
            {
                lock (lockTriangulationData)
                {
                    for (int i = 0; i < triangleIndex.Length; i += 3)
                    {
                        GeoPoint2D p1 = projection.ProjectUnscaled(trianglePoint[triangleIndex[i]]);
                        GeoPoint2D p2 = projection.ProjectUnscaled(trianglePoint[triangleIndex[i + 1]]);
                        GeoPoint2D p3 = projection.ProjectUnscaled(trianglePoint[triangleIndex[i + 2]]);
                        if (clr.TriangleHitTest(p1, p2, p3)) return true;
                    }
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
        {   // this is the hit test when selecting
            // there are two different approaches: let the "beam" of the pick area intersect the surface of this face and test
            // whether the intersection belongs to the face or use the triangulation.
            // Maybe we should decide by the kind of surface, which is faster
            // it was wrong to use the following: many faces, especially those faces, which  degenerate to lines in the PickArea projection, werent found
            //GeoPoint2D[] lip = Surface.GetLineIntersection(area.FrontCenter, area.Direction);
            //for (int i = 0; i < lip.Length; i++)
            //{
            //    if (Contains(ref lip[i], true)) return true;
            //}
            // return false; // no! when the center beam doesn't hit this face, it doesn't mean the PickArea doesn't contain the face
            // use the triangles (currently not executed)
            if (trianglePoint == null) AssureTriangles(area.Projection.Precision); // eingefügt, da in GDI Ansichten Hatch Objekte als Faces nicht trianguliert und somit nicht pickbar sind
            if (onlyInside)
            {
                lock (lockTriangulationData)
                {
                    for (int i = 0; i < triangleIndex.Length; i += 3)
                    {
                        if (!BoundingCube.UnitBoundingCube.Contains(area.ToUnitBox * trianglePoint[triangleIndex[i]])) return false;
                        if (!BoundingCube.UnitBoundingCube.Contains(area.ToUnitBox * trianglePoint[triangleIndex[i + 1]])) return false;
                        if (!BoundingCube.UnitBoundingCube.Contains(area.ToUnitBox * trianglePoint[triangleIndex[i + 2]])) return false;
                    }
                }
                return true;
            }
            else
            {
                lock (lockTriangulationData)
                {
                    for (int i = 0; i < triangleIndex.Length; i += 3)
                    {
                        // das folgende stimmt zwar im Prinzip, jedoch scheint irgendwas mit der Projektion im Argen zu sein
                        // so dass es erstmal nicht benutzt wird. Auch müsste Beam... statt Axis verwendet werden, damit
                        // keine rückwärtigen Objekte gefunden werden.
                        //if (Geometry.AxisThroughTriangle(area.FrontCenter, area.Direction, trianglePoint[triangleIndex[i]], trianglePoint[triangleIndex[i + 1]], trianglePoint[triangleIndex[i + 2]]))
                        //{   // diese Methode ist sicherer und noch relativ schnell, wenn die Zentralachse
                        //    // von area durch das Dreieck geht. Bei der folgenden Methode gibt es Ausfälle
                        //    // vermutlich wg. Rechengenauigkeit bei sehr ungleichmäßigen Dreiecken und extremen Projektionen (Preload23.cdb)
                        //    return true;
                        //}
                        GeoPoint p1 = area.ToUnitBox * trianglePoint[triangleIndex[i]];
                        GeoPoint p2 = area.ToUnitBox * trianglePoint[triangleIndex[i + 1]];
                        GeoPoint p3 = area.ToUnitBox * trianglePoint[triangleIndex[i + 2]];
                        if (BoundingCube.UnitBoundingCube.Interferes(ref p1, ref p2, ref p3)) return true;
                    }
                }
                return false;
            }
        }
        /// <summary>
        /// Returns the smallest distance from the provided point to the face. It will be the distance to
        /// a perpendicular footpoint on the face. If there is no such perpendicular footpoint 
        /// double.MaxValue will be returned. This method does not compute the distance to edges or vertices.
        /// If you need those distances use <see cref="ICurve.PositionOf"/> and <see cref="ICurve.PointAt"/>.
        /// </summary>
        /// <param name="fromHere">Point from which the distance is calculated</param>
        /// <returns>The signed distance, positive is to the outside, negative to the inside</returns>
        public double Distance(GeoPoint fromHere)
        {
            GeoPoint2D[] pos = surface.PerpendicularFoot(fromHere);
            GeoPoint foot = GeoPoint.Origin;
            GeoVector n = GeoVector.ZAxis;
            double res = double.MaxValue;
            for (int i = 0; i < pos.Length; i++)
            {
                if (Contains(ref pos[i], true))
                {
                    GeoPoint f = surface.PointAt(pos[i]);
                    double d = f | fromHere;
                    if (d < res)
                    {
                        res = d;
                        n = surface.GetNormal(pos[i]);
                        foot = f;
                    }
                }
            }
            if (res != double.MaxValue)
            {
                // der Abstand hat noch nicht das richtige Vorzeichen
                if (n * (fromHere - foot) < 0.0) res = -res;
            }
            //return res; // double.MaxValue, wenn kein Fußpunkt

            if (res == double.MaxValue)
            {   // wenn es einen Fußpunkt auf die Fläche gibt, dann ist der näher als die Kanten
                // stimmt das im Allgemeinen???
                foreach (Edge edg in AllEdgesIterated())
                {
                    if (edg.Curve3D != null)
                    {
                        double cpos = edg.Curve3D.PositionOf(fromHere);
                        if (cpos > 0.0 && cpos < 1.0)
                        {   // eine echte Senkrechte auf die Kurve
                            GeoPoint f = edg.Curve3D.PointAt(cpos);
                            GeoPoint2D spos = surface.PositionOf(f);
                            double d = f | fromHere;
                            if (d < res)
                            {
                                res = d;
                                n = surface.GetNormal(spos);
                                foot = f;
                            }
                        }
                    }
                }
            }
            if (res == double.MaxValue)
            {
                for (int i = 0; i < Vertices.Length; i++)
                {
                    double d = vertices[i].Position | fromHere;
                    if (d < res)
                    {
                        res = d;
                        GeoPoint2D spos = vertices[i].GetPositionOnFace(this);
                        GeoPoint f;
                        GeoVector du, dv;
                        surface.DerivationAt(spos, out f, out du, out dv);
                        foot = f;
                        n = du ^ dv;
                    }
                }
            }
#if DEBUG
            DebuggerContainer dc = new DebuggerContainer();
            dc.Add(this);
            Line dbgl = Line.Construct();
            dbgl.SetTwoPoints(foot, foot + n);
            dc.Add(dbgl);
            dc.Add(fromHere, System.Drawing.Color.Black, 0);
#endif
            Angle a = new Angle(n, fromHere - foot);
            if (a > Math.PI / 2.0) res = -res;
            return res;
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
            if (trianglePoint == null) AssureTriangles(precision);
            // hier nicht auf triangulierung festlegen, wenns eine gibt dann isses gut!
            // hier bräuchte man einen OctTree über die Dreiecke, damit man schneller testen kann
            lock (lockTriangulationData)
            {
                if (trianglePoint != null)
                {
                    for (int i = 0; i < triangleIndex.Length; i += 3)
                    {
                        GeoPoint p1 = trianglePoint[triangleIndex[i]];
                        GeoPoint p2 = trianglePoint[triangleIndex[i + 1]];
                        GeoPoint p3 = trianglePoint[triangleIndex[i + 2]];
                        // dirx = p2-p1, diry = p3-p1, dirz = direction, org = p1
                        // org + x*dirx + y*diry+z = fromHere + z*direction
                        // x>0, y>0, x+y<1: getroffen
                        Matrix m = DenseMatrix.OfColumnArrays(p2 - p1, p3 - p1, -direction);
                        Vector b = new DenseVector(fromHere - p1);
                        Vector x = (Vector)m.Solve(b); // liefert gleichzeitig die Bedingung für innen und den Abstand
                        if (x != null)
                        {
                            if (x[0] >= 0.0 && x[1] >= 0.0 && (x[0] + x[1]) <= 1.0)
                            {
                                if (x[2] < res)
                                {
                                    res = x[2];
                                }
                            }
                        }
                    }
                }
            }
            return res;
        }
        #endregion
#if DEBUG
#endif
#if DEBUG
#endif
        internal static ColorDef EdgeColor;
        internal static LineWidth EdgeLineWidth;
        internal static LinePattern EdgeLinePattern;
        internal static Style EdgeStyle;
        static Face()
        {
            EdgeColor = new ColorDef("", Color.Black);
            EdgeLineWidth = new LineWidth(); // dünne ohne Name
            EdgeLinePattern = new LinePattern();
            EdgeStyle = new Style("CADability.EdgeStyle");
            EdgeStyle.ColorDef = EdgeColor;
            EdgeStyle.LineWidth = EdgeLineWidth;
            EdgeStyle.LinePattern = EdgeLinePattern;
        }
        internal void SetEdgeAttributes(IGeoObject go)
        {
            go.Style = EdgeStyle;
            go.Layer = this.Layer;
        }
        internal static bool CheckOutlineDirection(Face fc, Edge[] outline, double uperiod, double vperiod, OrderedMultiDictionary<double, int>[] selections)
        {
            try
            {
                if (selections == null)
                {
                    // die Bedeutung von selections:
                    // in dem bereits richtig sortierten array der outlines werden nacheinander die
                    // einzelnen segmente umgedreht oder um die Periode verschoben, damit eine zusammenhängende Kurve
                    // entsteht. Dazu werden die Abststände zwischen Endpunkt i und Startpunkt i+1 der verschiedenen 
                    // Möglichkeiten überprüft und die beste Möglichkeit wird genommen. Manchmal ist es jedoch nicht
                    // zu entscheiden, welche Möglichkeit die beste ist, und wenn man die falsche nimmt, läuft
                    // die Kurve aus dem Ruder. Selections gibtdie Liste der berechneten Abstände und ihre Codierungen an
                    // 
                    selections = new OrderedMultiDictionary<double, int>[outline.Length - 1];
                }
                // Beste Reglung: so wier hier verfahren (ohne das Verschieben in u bei zwei gleichen Linien
                // wenn man am Ende nicht zusammen ist, dann eine neue Routine aufrufen, alle Kurven in ihrer Periode
                // verdoppeln (einmal +, inmal -) und dann irgendwelche zusammenhängenden geschlossenen Kurven suchen
                // in der lediglich der Index der reihenfolge stimmt. Diese Ausnahmeroutine sollte nur selten drankommen
                // eigentlich müssten nur die Kanten verschoben werden, die genau auf einer Naht liegen, also
                // bei denen x==0.0 oder x==uperiod (y, vperiod analog) für Anfangspunkt und Endpunkt. Alle anderen kanten
                // müsste man höchstens umdrehen. OCas liefert vermutlich keine kanten, die über eine Periodengrenze gehen
                // solche kanten kommen auch immer paarweise vor und sollten zuerst behandelt werden.
                // Allein der Sonderfall, dass es ein Rechteck ist, macht ggf. ein Problem.
                ICurve2D[] segments = new ICurve2D[outline.Length];
                ICurve2D[] clonedSegments = new ICurve2D[outline.Length];
                BoundingRect ext = BoundingRect.EmptyBoundingRect;
                //SortedList<double, int> dist = new SortedList<double, int>(20);
                for (int i = 0; i < outline.Length; ++i)
                {
                    if (outline[i] != null)
                    {
                        segments[i] = outline[i].Curve2D(fc, segments);
                        clonedSegments[i] = segments[i].Clone();
                        if (i == 0)
                        {   // wenn es nur ein geschlossenes Objekt ist, dann sind Stert- und Endpunkt identisch
                            // und wir müssen die gesamte Ausdehnung nehmen
                            ext.MinMax(segments[i].GetExtent());
                        }
                        else
                        {   // geht bei vielen Kurven schneller als wenn man das gesamte Objekt beachtet
                            ext.MinMax(segments[i].StartPoint);
                            ext.MinMax(segments[i].EndPoint);
                        }
                    }
                }

                // precision geändert: von /10 auf /1000. Es wird verwendet, um Zusammenhänge als solche zu erkennen
                // und dazu ist /1000 noch recht großzügig, oder?
                double precision = (ext.Width + ext.Height) / 100;
                if (outline.Length == 1 && segments[0] is Circle2D) precision = (segments[0] as Circle2D).Radius / 1000;
                if (outline.Length == 1 && segments[0] is Ellipse2D) precision = ((segments[0] as Ellipse2D).minrad + (segments[0] as Ellipse2D).majrad) / 1000;
                if (uperiod > 0.0) precision = Math.Min(precision, uperiod / 1000);
                if (vperiod > 0.0) precision = Math.Min(precision, vperiod / 1000);

                // Sonderfall: segments beinhaltet zwei identische Linien. z.B. ein rechteck bei einem Zylinder.
                // Dann entsteht ein "Z" und am Ende wird das zurechtgebogen und es kracht.
                // VORLÄUFIGE LÖSUNG: (eilt wg. Italien)
                // solche Linien suchen und die eine Linie um uperiod versetzen
                /*if (uperiod != 0.0)
                {
                    int i0 = -1; 
                    int i1 = -1;
                    BoundingRect ext = BoundingRect.EmptyBoundingRect;
                    for (int i = 0; i < segments.Length-1; ++i)
                    {
                        ext.MinMax(segments[i].StartPoint);
                        ext.MinMax(segments[i].EndPoint);
                        if (segments[i] is Line2D && segments[i].StartPoint.x == segments[i].EndPoint.x && i0==-1)
                        {
                            for (int j = i + 1; j < segments.Length; ++j)
                            {
                                if (segments[j] is Line2D && segments[j].StartPoint.x == segments[j].EndPoint.x)
                                {
                                    if ((Precision.IsEqual(segments[i].StartPoint, segments[j].StartPoint) && Precision.IsEqual(segments[i].EndPoint, segments[j].EndPoint)) ||
                                        (Precision.IsEqual(segments[i].EndPoint, segments[j].StartPoint) && Precision.IsEqual(segments[i].StartPoint, segments[j].EndPoint)))
                                    {
                                        i0 = i;
                                        i1 = j;
                                        break;
                                    }
                                }
                            }
                        }
                        // if (i0 >= 0) break; hier kein break, sonst stimmt ext nicht
                    }
                    if (i0 >= 0)
                    {
                        // hier ist völlig unklar, welcher verschoben werden muss
                        // wenn man den falschen verschiebt, stimmt die reihenfolge nicht mehr
                        // Es muss eine allgemeinere Lösung her!
                        if (segments[i0].StartPoint.x < (ext.Left + ext.Right) / 2.0)
                        {
                            segments[i0].Move(uperiod, 0.0);
                        }
                        else
                        {
                            segments[i0].Move(-uperiod, 0.0);
                        }
                    }
                }*/

                // Manchmal läuft das hier aus dem Ruder: so endet die Kurve um zweimal die Periode versetzt zu dem Startpunkt
                // es scheint immer gefährlich zu sein mit einer Kurve zu beginnen, deren beide Endpunkte auf periodischen Grenzen
                // liegen. Da segments nur ein Array von referenzen ist, kann man es beliebig rotieren, ohne an der Lösung
                // etwas zu ändern. Man ändert nur die Stelle, bei der man anfängt.
                // Diese Umsortierung ist nicht ganz die beste Lösung. Eine Ideale Lösung würde so aussehen: Wenn der Endpunkt
                // und der Anfangspunkt um eine oder mehrere Perioden versetzt sind, dann muss man mit dem ersten segment
                // wieder neu beginnen und durch versetzten (allerdings um mehrere Perioden) und umdrehen versuchen den
                // Kurvenzug zu schließen. 
                /*int startWith = -1;
                if (uperiod != 0.0 && vperiod!=0.0)
                {
                    for (int i = 0; i < segments.Length; ++i)
                    {
                        double dsu = Math.IEEERemainder(segments[i].StartPoint.x, uperiod);
                        double deu = Math.IEEERemainder(segments[i].EndPoint.x, uperiod);
                        double dsv = Math.IEEERemainder(segments[i].StartPoint.y, vperiod);
                        double dev = Math.IEEERemainder(segments[i].EndPoint.y, vperiod);
                        if (Math.Abs(dsu) > 1e-6 && Math.Abs(deu) > 1e-6 && Math.Abs(dsv) > 1e-6 && Math.Abs(dev) > 1e-6)
                        {
                            startWith = i;
                            break;
                        }
                    }
                }
                else if (uperiod != 0.0)
                {
                    for (int i = 0; i < segments.Length; ++i)
                    {
                        double ds = Math.IEEERemainder(segments[i].StartPoint.x , uperiod);
                        double de = Math.IEEERemainder(segments[i].EndPoint.x , uperiod);
                        if (Math.Abs(ds) > 1e-6 && Math.Abs(de) > 1e-6)
                        {
                            startWith = i;
                            break;
                        }
                    }
                }
                else if (vperiod != 0.0)
                {
                    for (int i = 0; i < segments.Length; ++i)
                    {
                        double ds = Math.IEEERemainder(segments[i].StartPoint.y, vperiod);
                        double de = Math.IEEERemainder(segments[i].EndPoint.y, vperiod);
                        if (Math.Abs(ds) > 1e-6 && Math.Abs(de) > 1e-6)
                        {
                            startWith = i;
                            break;
                        }
                    }
                }
                if (startWith > 0)
                {   // umsortieren
                    ICurve2D[] segmentssort = (ICurve2D[])segments.Clone();
                    for (int i = 0; i < segments.Length; ++i)
                    {
                        segments[i] = segmentssort[(i+startWith)%segments.Length];
                    }
                }*/
#if DEBUG
                DebuggerContainer dc = new DebuggerContainer();
                for (int i = 0; i < segments.Length; ++i)
                {
                    if (segments[i] != null) dc.Add(segments[i], System.Drawing.Color.Red, i);
                }
#endif
                int counter = 0;
                while (true) // alle verschiedenen Möglichkeiten durchprobieren
                {
                    ++counter;
                    if (counter == 100 && (uperiod != 0.0 || vperiod != 0.0))
                    {   // neu anfangen mit dem Versuch alle Kurven in das Standardintervall der Periode zu bringen
                        selections = new OrderedMultiDictionary<double, int>[outline.Length - 1];
                        if (uperiod != 0.0)
                        {
                            for (int i = 0; i < clonedSegments.Length; i++)
                            {
                                while (clonedSegments[i].StartPoint.x < 0 || clonedSegments[i].EndPoint.x < 0)
                                {
                                    clonedSegments[i].Move(uperiod, 0.0);
                                }
                                while (clonedSegments[i].StartPoint.x > uperiod || clonedSegments[i].EndPoint.x > uperiod)
                                {
                                    clonedSegments[i].Move(-uperiod, 0.0);
                                }
                            }
                        }
                        if (vperiod != 0.0)
                        {
                            for (int i = 0; i < clonedSegments.Length; i++)
                            {
                                while (clonedSegments[i].StartPoint.y < 0 || clonedSegments[i].EndPoint.y < 0)
                                {
                                    clonedSegments[i].Move(0.0, vperiod);
                                }
                                while (clonedSegments[i].StartPoint.y > vperiod || clonedSegments[i].EndPoint.y > vperiod)
                                {
                                    clonedSegments[i].Move(0.0, -vperiod);
                                }
                            }
                        }
                    }
                    if (segments.Length >= 2)
                    {   // für die ersten beiden gibt es 4 Möglichkeiten
                        // es ist noch die Frage, ob besser das segment 0 oder besser Nr 1 zu verschieben ist
                        // beim ersten Mal unnötig:
                        for (int i = 0; i < segments.Length; i++)
                        {
                            if (segments[i] != null) segments[i].Copy(clonedSegments[i]);
                        }
                        if (selections[0] == null)
                        {
                            OrderedMultiDictionary<double, int> dist = new Wintellect.PowerCollections.OrderedMultiDictionary<double, int>(true);
                            if (segments[0] != null && segments[1] != null)
                            {
                                if (uperiod != 0.0)
                                {   // segments[0] verschieben
                                    GeoVector2D offset = new GeoVector2D(uperiod, 0.0);
                                    dist.Add(Geometry.Dist(segments[0].StartPoint + offset, segments[1].StartPoint), 4);
                                    dist.Add(Geometry.Dist(segments[0].EndPoint + offset, segments[1].StartPoint), 5);
                                    dist.Add(Geometry.Dist(segments[0].StartPoint + offset, segments[1].EndPoint), 6);
                                    dist.Add(Geometry.Dist(segments[0].EndPoint + offset, segments[1].EndPoint), 7);
                                    dist.Add(Geometry.Dist(segments[0].StartPoint - offset, segments[1].StartPoint), 8);
                                    dist.Add(Geometry.Dist(segments[0].EndPoint - offset, segments[1].StartPoint), 9);
                                    dist.Add(Geometry.Dist(segments[0].StartPoint - offset, segments[1].EndPoint), 10);
                                    dist.Add(Geometry.Dist(segments[0].EndPoint - offset, segments[1].EndPoint), 11);
                                }
                                if (vperiod != 0.0)
                                {   // segments[0] verschieben
                                    GeoVector2D offset = new GeoVector2D(0.0, vperiod);
                                    dist.Add(Geometry.Dist(segments[0].StartPoint + offset, segments[1].StartPoint), 12);
                                    dist.Add(Geometry.Dist(segments[0].EndPoint + offset, segments[1].StartPoint), 13);
                                    dist.Add(Geometry.Dist(segments[0].StartPoint + offset, segments[1].EndPoint), 14);
                                    dist.Add(Geometry.Dist(segments[0].EndPoint + offset, segments[1].EndPoint), 15);
                                    dist.Add(Geometry.Dist(segments[0].StartPoint - offset, segments[1].StartPoint), 16);
                                    dist.Add(Geometry.Dist(segments[0].EndPoint - offset, segments[1].StartPoint), 17);
                                    dist.Add(Geometry.Dist(segments[0].StartPoint - offset, segments[1].EndPoint), 18);
                                    dist.Add(Geometry.Dist(segments[0].EndPoint - offset, segments[1].EndPoint), 19);
                                }
                                // hinten angestellt, denn es soll die vorherigen bei Gleichheit überschreiben
                                // leider genügt das mit der Gleichheit nicht wg. Rechengenauigkeit, deshalb -Precision.eps
                                dist.Add(Geometry.Dist(segments[0].StartPoint, segments[1].StartPoint) - Precision.eps, 0);
                                dist.Add(Geometry.Dist(segments[0].EndPoint, segments[1].StartPoint) - Precision.eps, 1);
                                dist.Add(Geometry.Dist(segments[0].StartPoint, segments[1].EndPoint) - Precision.eps, 2);
                                dist.Add(Geometry.Dist(segments[0].EndPoint, segments[1].EndPoint) - Precision.eps, 3);
                                // das "-Precision.eps" fehlte am 18.1.11, wieder reingemacht wg. NEED_REGULARIZATION.stp
                                selections[0] = dist;
                            }
                        }
                        if (selections[0] == null || selections[0].TotalCount == 0 || selections[0].FirstItem.Key > precision) return false;
                        switch (selections[0].FirstItem.Value % 4) // das ist der Fall für den kleinsten Abstand
                        {
                            case 0:
                                segments[0].Reverse();
                                break;
                            case 1: // beide richtig rum
                                break;
                            case 2:
                                segments[0].Reverse();
                                segments[1].Reverse();
                                break;
                            case 3:
                                segments[1].Reverse();
                                break;
                        }
                        switch (selections[0].FirstItem.Value / 4)
                        {
                            case 0: break; // nix, kein offset
                            case 1:
                                // hier gibt es zwei Optionen, segment[0] verschieben oder segment[1]
                                // wenn eines nicht auf periodischen Grenzen endet, dann besser das andere verschieben
                                if (Math.Abs(Math.IEEERemainder(segments[1].StartPoint.x, uperiod)) < 1e-6 &&
                                    Math.Abs(Math.IEEERemainder(segments[1].EndPoint.x, uperiod)) < 1e-6)
                                {   // Nr. 1 liegt ganz auf der Grenze
                                    segments[1].Move(-uperiod, 0.0);
                                }
                                else
                                {
                                    segments[0].Move(uperiod, 0.0);
                                }
                                break;
                            case 2:
                                if (Math.Abs(Math.IEEERemainder(segments[1].StartPoint.x, uperiod)) < 1e-6 &&
                                    Math.Abs(Math.IEEERemainder(segments[1].EndPoint.x, uperiod)) < 1e-6)
                                {   // Nr. 1 liegt ganz auf der Grenze
                                    segments[1].Move(uperiod, 0.0);
                                }
                                else
                                {
                                    segments[0].Move(-uperiod, 0.0);
                                }
                                break;
                            case 3:
                                if (Math.Abs(Math.IEEERemainder(segments[1].StartPoint.y, vperiod)) < 1e-6 &&
                                    Math.Abs(Math.IEEERemainder(segments[1].EndPoint.y, vperiod)) < 1e-6)
                                {   // Nr. 1 liegt ganz auf der Grenze
                                    segments[1].Move(0.0, -vperiod);
                                }
                                else
                                {
                                    segments[0].Move(0.0, vperiod);
                                }
                                break;
                            case 4:
                                if (Math.Abs(Math.IEEERemainder(segments[1].StartPoint.y, vperiod)) < 1e-6 &&
                                    Math.Abs(Math.IEEERemainder(segments[1].EndPoint.y, vperiod)) < 1e-6)
                                {   // Nr. 1 liegt ganz auf der Grenze
                                    segments[1].Move(0.0, vperiod);
                                }
                                else
                                {
                                    segments[0].Move(0.0, -vperiod);
                                }
                                break;
                        }
                        // if ((segments[1].StartPoint | segments[0].EndPoint) > Precision.eps * 1000) return false;
                        // if (!Precision.IsEqual(segments[1].StartPoint, segments[0].EndPoint))
                        // die Bedingung mal weggelassen, sonst machen manche Daten Probleme, z.B. "3745911.stp"
                        segments[1].StartPoint = segments[0].EndPoint;
                    }
                    bool broken = false;
                    for (int i = 2; i < segments.Length; ++i)
                    {
                        if (selections[i - 1] == null)
                        {
                            OrderedMultiDictionary<double, int> dist = new Wintellect.PowerCollections.OrderedMultiDictionary<double, int>(true);
                            if (segments[i - 1] != null && segments[i] != null)
                            {
                                if (uperiod != 0.0)
                                {   // segments[0] verschieben
                                    GeoVector2D offset = new GeoVector2D(uperiod, 0.0);
                                    dist.Add(Geometry.Dist(segments[i - 1].EndPoint, segments[i].StartPoint + offset), 2);
                                    dist.Add(Geometry.Dist(segments[i - 1].EndPoint, segments[i].EndPoint + offset), 3);
                                    dist.Add(Geometry.Dist(segments[i - 1].EndPoint, segments[i].StartPoint - offset), 4);
                                    dist.Add(Geometry.Dist(segments[i - 1].EndPoint, segments[i].EndPoint - offset), 5);
                                }
                                if (vperiod != 0.0)
                                {   // segments[0] verschieben
                                    GeoVector2D offset = new GeoVector2D(0.0, vperiod);
                                    dist.Add(Geometry.Dist(segments[i - 1].EndPoint, segments[i].StartPoint + offset), 6);
                                    dist.Add(Geometry.Dist(segments[i - 1].EndPoint, segments[i].EndPoint + offset), 7);
                                    dist.Add(Geometry.Dist(segments[i - 1].EndPoint, segments[i].StartPoint - offset), 8);
                                    dist.Add(Geometry.Dist(segments[i - 1].EndPoint, segments[i].EndPoint - offset), 9);
                                }
                                // hinten angestellt, denn es soll die vorherigen bei Gleichheit überschreiben
                                dist.Add(Geometry.Dist(segments[i - 1].EndPoint, segments[i].StartPoint) - Precision.eps, 0);
                                dist.Add(Geometry.Dist(segments[i - 1].EndPoint, segments[i].EndPoint) - Precision.eps, 1);
                                selections[i - 1] = dist;
                                //System.Diagnostics.Trace.WriteLine("Adding: " + (i - 1).ToString());
                            }
                        }
                        if (selections[i - 1].TotalCount == 0 || selections[i - 1].FirstItem.Key > precision)
                        {
                            // System.Diagnostics.Trace.WriteLine("Removing: " + (i - 1).ToString());
                            selections[i - 1] = null;
                            selections[i - 2].Remove(selections[i - 2].FirstItem.Key, selections[i - 2].FirstItem.Value);
                            broken = true;
                            break;
                        }
                        if (selections[i - 1].FirstItem.Value % 2 == 1)
                        {
                            segments[i].Reverse();
                        }
                        switch (selections[i - 1].FirstItem.Value / 2)
                        {
                            case 0: break; // nix, kein offset
                            case 1:
                                segments[i].Move(uperiod, 0.0);
                                break;
                            case 2:
                                segments[i].Move(-uperiod, 0.0);
                                break;
                            case 3:
                                segments[i].Move(0.0, vperiod);
                                break;
                            case 4:
                                segments[i].Move(0.0, -vperiod);
                                break;
                        }
                        //if ((segments[i].StartPoint | segments[i - 1].EndPoint) > Precision.eps * 1000) return false;
                        // es nutzt nichts das Erzeugen eines Face Objektes scheitern zu lassen, da eine andere
                        // edge doch auf dieses face verweist und es somit weiterlebt...
                        // besseres konzept: die beiden Kanten kennzeichnen und nach dem Einlesen
                        // versuchen sie aufeinander zu bringen Oft sind wohl 3 Faces beteiligt und da gilt es den
                        // Schnittpunkt zu finden
                        // bei "802_p10a.stp" sieht es so aus, als ob das eine Kante dabei wäre, die garnichts mit der Umrandung
                        // zu tun hat, die vorne und hinten nicht anschließbar ist
                        // if (!Precision.IsEqual(segments[i].StartPoint, segments[i - 1].EndPoint))
                        // s.o.
                        segments[i].StartPoint = segments[i - 1].EndPoint;
                    }
                    if (broken)
                    {
#if DEBUG
                        DebuggerContainer dc1 = new DebuggerContainer();
                        for (int i = 0; i < segments.Length; ++i)
                        {
                            if (segments[i] != null) dc1.Add(segments[i], System.Drawing.Color.Red, i);
                        }
#endif
                        // return false; // eingeführt wg. "elettrodo-cilindrico.sat"
                        if (counter > 150) return false;
                        continue;
                    }
#if DEBUG
                    DebuggerContainer dc2 = new DebuggerContainer();
                    for (int i = 0; i < segments.Length; ++i)
                    {
                        if (segments[i] != null) dc2.Add(segments[i], System.Drawing.Color.Red, i);
                    }
#endif
                    if ((segments[0].StartPoint | segments[segments.Length - 1].EndPoint) < precision)
                    {
                        if (segments.Length > 1) segments[0].StartPoint = segments[segments.Length - 1].EndPoint;
                        else if (segments[0] is Arc2D)
                        {
                            (segments[0] as Arc2D).Close();
                        }
                        else if (segments[0] is EllipseArc2D)
                        {
                            (segments[0] as EllipseArc2D).Close();
                        }
                        if (segments.Length == 2)
                        {   // Sonderfall: nur zwei Kurven. Die können verdreht gefunden werden, d.h. ggf. wurden beide umgedreht und jetzt geht die Border falschrum
                            bool reversed;
                            Border bdr = new Border(out reversed, new ICurve2D[] { segments[0].Clone(), segments[1].Clone() }); // Clone, da diese ggf verändert werden
                            if (reversed)
                            {   // bei zweien kann man jedes umdrehen und der Zusammenhang bleibt bestehen
                                segments[0].Reverse();
                                segments[1].Reverse();
                            }
                        }
                        else if (segments.Length == 1)
                        {
                            bool reversed;
                            Border bdr = new Border(out reversed, new ICurve2D[] { segments[0].Clone() }); // Clone, da diese ggf verändert werden
                            if (reversed)
                            {   // bei einem wird dieses hiermit in Gegenrichtung gepolt
                                segments[0].Reverse();
                            }
                        }
                        else
                        {

                        }
                        return true;
                    }
                    else
                    {
                        if (segments.Length < 2) return false; // eine nicht geschlossene Kurve
                        selections[segments.Length - 2].Remove(selections[segments.Length - 2].FirstItem.Key, selections[segments.Length - 2].FirstItem.Value);
                    }
                }
                return false;
            }
            catch (Exception)
            {
                return false;
            }
        }
#if DEBUG
#endif
#if DEBUG
#endif
#if DEBUG
#endif
#if DEBUG
#endif
        internal static Face FromEdges(ISurface surface, ICurve[][] outlines)
        {   // Erzeugung aus DXF/DWG
            // zuerstmal ohne Periodizität
            List<Border> bdrlist = new List<Border>();
            for (int i = 0; i < outlines.Length; i++)
            {
                List<ICurve2D> curves = new List<ICurve2D>();
                for (int j = 0; j < outlines[i].Length; j++)
                {
                    ICurve2D c2d = surface.GetProjectedCurve(outlines[i][j], Precision.eps);
                    if (c2d != null)
                    {
                        curves.Add(c2d);
                        c2d.UserData.Add("3d curve", outlines[i][j]);
                    }
                }
#if DEBUG
                DebuggerContainer dc = new DebuggerContainer();
                dc.Add(curves.ToArray());
#endif
                Border bdr = Border.FromUnorientedList(curves.ToArray(), true);
                // ggF umdrehen oder? if (bdr.Area<0.0)
                bdrlist.Add(bdr);
            }
            // ggF borderlist sortieren nach Größe
            SimpleShape ss = new SimpleShape(bdrlist[0], bdrlist.GetRange(1, bdrlist.Count - 1).ToArray());
            return Face.MakeFace(surface, ss);

        }
        #region IJsonSerialize Members
        public override void GetObjectData(IJsonWriteData data)
        {
            base.GetObjectData(data);
            data.AddProperty("Surface", surface);
            data.AddProperty("Outline", outline);
            data.AddProperty("Holes", holes);
            data.AddProperty("ColorDef", colorDef);
            data.AddProperty("OrientedOutward", orientedOutward);
        }

        public override void SetObjectData(IJsonReadData data)
        {
            base.SetObjectData(data);
            surface = data.GetProperty<ISurface>("Surface");
            outline = data.GetProperty<Edge[]>("Outline");
            holes = data.GetProperty<Edge[][]>("Holes");
            colorDef = data.GetProperty<ColorDef>("ColorDef");
            orientedOutward = (bool)data.GetProperty("OrientedOutward");
            data.RegisterForSerializationDoneCallback(this);
        }

        void IJsonSerializeDone.SerializationDone(JsonSerialize jsonSerialize)
        {
            if (outline != null)
            {   // in alten files gibt es solche faces
                for (int i = 0; i < outline.Length; ++i)
                {
                    outline[i].Owner = this;
                }
                for (int i = 0; i < holes.Length; i++)
                {
                    for (int j = 0; j < holes[i].Length; ++j)
                    {
                        holes[i][j].Owner = this;
                    }
                }
                try
                {
                    Surface.SetBounds(Area.GetExtent());
                }
                catch { }
            }
        }
        #endregion
        #region ISerializable Members
        /// <summary>
        /// Constructor required by deserialization
        /// </summary>
        /// <param name="info">SerializationInfo</param>
        /// <param name="context">StreamingContext</param>
        protected Face(SerializationInfo info, StreamingContext context)
                : base(info, context)
        {
            surface = info.GetValue("Surface", typeof(ISurface)) as ISurface;
            outline = (Edge[])info.GetValue("Outline", typeof(Edge[]));
            holes = (Edge[][])info.GetValue("Holes", typeof(Edge[][]));
            colorDef = ColorDef.Read(info, context);
            try
            {
                orientedOutward = (bool)info.GetValue("OrientedOutward", typeof(bool));
            }
            catch (SerializationException)
            {
                orientedOutward = true;
            }
            hashCode = hashCodeCounter++;
            extent = BoundingCube.EmptyBoundingCube;
            lockTriangulationRecalc = new object();
            lockTriangulationData = new object();
        }
        /// <summary>
        /// Implements <see cref="ISerializable.GetObjectData"/>
        /// </summary>
        /// <param name="info">The <see cref="System.Runtime.Serialization.SerializationInfo"/> to populate with data.</param>
        /// <param name="context">The destination (<see cref="System.Runtime.Serialization.StreamingContext"/>) for this serialization.</param>
        public override void GetObjectData(SerializationInfo info, StreamingContext context)
        {
            base.GetObjectData(info, context);
            info.AddValue("Surface", surface);
            info.AddValue("Outline", outline);
            info.AddValue("Holes", holes);
            info.AddValue("ColorDef", colorDef);
            info.AddValue("OrientedOutward", orientedOutward);
        }
        #endregion
        #region IDeserializationCallback Members
        void IDeserializationCallback.OnDeserialization(object sender)
        {
            if (outline != null)
            {   // in alten files gibt es solche faces
                for (int i = 0; i < outline.Length; ++i)
                {
                    outline[i].Owner = this;
                }
                for (int i = 0; i < holes.Length; i++)
                {
                    for (int j = 0; j < holes[i].Length; ++j)
                    {
                        holes[i][j].Owner = this;
                    }
                }
            }
            // conflicts with Shell.OnDeserialization:
            //for (int i = 0; i < outline.Length; ++i)
            //{
            //    int next = i + 1;
            //    if (next >= outline.Length) next = 0;
            //    Edge.Connect(outline[i], outline[next], this);
            //}
            //for (int i = 0; i < holes.Length; i++)
            //{
            //    for (int j = 0; j < holes[i].Length; ++j)
            //    {
            //        int next = j + 1;
            //        if (next >= holes[i].Length) next = 0;
            //        Edge.Connect(holes[i][j], holes[i][next], this);
            //    }
            //}
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
                    if (Owner is Shell)
                    {
                        IGeoObjectImpl toFireChangeEvent;
                        if ((Owner as Shell).Owner is Solid)
                        {
                            toFireChangeEvent = (Owner as Shell).Owner as IGeoObjectImpl;
                        }
                        else
                        {
                            toFireChangeEvent = Owner as IGeoObjectImpl;
                        }
                        using (new Changing(toFireChangeEvent, true, true, null, new object[0])) // no undo, attributeonly
                        {
                            // Hirmit ruft die Shell oder das Solid, von welchem dieses Face ein Teil ist
                            // WillChange und DidChange auf um eine neue Darstellung zu erzwingen
                            // im Prinzip müsste das geliech in Shell passieren, aber
                            // dort kann man die Farbe nicht ändern
                            colorDef = value;
                        }
                    }
                    else
                    {   // Face ist nicht Teil einer Shell oder eines Solid
                        colorDef = value;
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
            (this as IColorDef).SetTopLevel(newValue);
        }
        #endregion
        internal SimpleShape GetProjectedArea(Projection p)
        {
            // PROBLEM:
            // Der Rand einer projizierten Fläche wird aus den 3D Kurven gemacht. 
            // Es könnte Ränder geben, die sich selbst überschneiden (Schraubenflächen)
            // Aber z.Z. ist das größere Problem etwas, das z.B. beim Kegelstumpf auftritt, wenn er keine
            // Umrisskante hat. Dann ist die Umrandung durch zwei Ellipsen und zwei (identischen) Linien gegeben.
            // Es müsste daraus ein Simple Shape mit einem Loch entstehen, das ist aber z.Z. nicht der Fall.
            // Das macht vor allem Probleme beim verdecken. 


            // muss null liefern wenn in der Projektion die Fläche verschwindet, also zur Kante wird.
            double umin, umax, vmin, vmax;
            GetUVBounds(out umin, out umax, out vmin, out vmax);
            if (this.surface.IsVanishingProjection(p, umin, umax, vmin, vmax))
            {
                return null;
            }
            // die Orientierung der Borders kann auch verkehrtrum sein. Wird das im Border Konstruktor gecheckt?
            List<ICurve2D> outlinecurves = new List<ICurve2D>();
            GeoPoint2D lastEndPoint = GeoPoint2D.Origin;
            for (int i = 0; i < outline.Length; ++i)
            {
                if (outline[i].Curve3D != null)
                {
                    if (outline[i].Curve3D is Ellipse && !(outline[i].Curve3D as Ellipse).IsArc)
                    {   // Vollkreise und -Ellipsen machen Probleme, da sie zwar einen Startparameter haben, 
                        // dieser aber beim umwandeln nach 2D ignoriert wird. Der Startparameter ist aber
                        // wichtig, weil hier die anderen Kurven ansetzen (z.B. bei einem Kegelstumpf)
                        // deshalb werden die Vollkreise in zwei halbkreise umgewandelt und als solche 
                        // verwendet
                        ICurve[] parts = outline[i].Curve3D.Split(0.5);
                        for (int j = 0; j < parts.Length; ++j)
                        {
                            ICurve2D prcurve = parts[j].GetProjectedCurve(p.ProjectionPlane);
                            if (prcurve != null)
                            {
                                outlinecurves.Add(prcurve);
                            }
                        }
                    }
                    else
                    {
                        ICurve2D prcurve = outline[i].Curve3D.GetProjectedCurve(p.ProjectionPlane);
                        if (prcurve != null && prcurve.Length > Precision.eps)
                        {
                            outlinecurves.Add(prcurve);
                        }
                    }
                }
            }
            List<Border> prholes = new List<Border>();
            for (int i = 0; i < holes.Length; ++i)
            {
                List<ICurve2D> holecurves = new List<ICurve2D>();
                for (int j = 0; j < holes[i].Length; j++)
                {
                    if (holes[i][j].Curve3D != null)
                    {
                        ICurve2D prcurve = holes[i][j].Curve3D.GetProjectedCurve(p.ProjectionPlane);
                        if (prcurve != null && prcurve.Length > Precision.eps)
                        {
                            holecurves.Add(prcurve);
                        }
                    }
                }
                Border hole = Border.FromUnorientedList(holecurves.ToArray(), true);
                prholes.Add(hole);
            }
            try
            {
                SimpleShape res = new SimpleShape(Border.FromUnorientedList(outlinecurves.ToArray(), true), prholes.ToArray());
                return res;
            }
            catch (BorderException)
            {
                return null;
            }
        }
        internal Face[] Split(Edge[] tan)
        {   // hat mit Tangenten nix mehr zu tun, aber mit dem splitten von Faces mit Saum
            if (tan.Length == 0) return null;

            CompoundShape splitted = new CompoundShape(Area);
            // jede Edge in tan ist eine Umrisskurve, die hängen nicht zusammen, d.h. sie zerstückeln u.U. mehrfach
            for (int i = 0; i < tan.Length; ++i)
            {
                ICurve2D curve = tan[i].Curve2D(this);
                if (curve != null)
                {
                    Border tanborder = new Border(curve);
                    splitted = splitted.Split(tanborder);
                }
            }
            for (int i = 0; i < splitted.SimpleShapes.Length; ++i)
            {
                if (splitted.SimpleShapes[i].Outline == null) return null;
            }
            Face[] res = new Face[splitted.SimpleShapes.Length];

            // das ist sauschade, dass an der simpleShape outline nicht mehr erkennbar ist, welche
            // Edge es mal war. Die meisten dürften unverändert bleiben und müssten nicht neu gemacht werden
            // bei anderen würde es genügen, die Trimmstellen zu bestimmen und die Kanten zu trimmen. Nur die 
            // Tangenten sind echte neue Kanten.

            for (int i = 0; i < splitted.SimpleShapes.Length; ++i)
            {
                res[i] = surface.MakeFace(splitted.SimpleShapes[i]);
                for (int j = 0; j < tan.Length; j++)
                {
                    res[i].ReplaceEdge(tan[j], this);
                }
            }
            return res;
        }
        /// <summary>
        /// The provided edge is removed from this face and replaced by an identical edge. Both edges now only have primaryFace set
        /// </summary>
        /// <param name="edge"></param>
        internal void SeperateEdge(Edge edge)
        {
            Edge newEdge = edge.CloneWithVertices();
            newEdge.SetPrimary(this, edge.Curve2D(this), edge.Forward(this));
            edge.DisconnectFromFace(this);
            for (int i = 0; i < outline.Length; i++)
            {
                if (outline[i] == edge)
                {
                    outline[i] = newEdge;
                    return;
                }
            }
            for (int i = 0; i < holes.Length; i++)
            {
                for (int j = 0; j < holes[i].Length; j++)
                {
                    if (holes[i][j] == edge)
                    {
                        holes[i][j] = newEdge;
                        return;
                    }
                }
            }
        }
        /// <summary>
        /// die gegebene edge soll in dieses Face eingebaut werden. Eine Umrisskante wird zunächst auf dem
        /// ganzen Face erzeugt und dann in die beiden betroffenen split Faces eingebaut
        /// </summary>
        /// <param name="edge"></param>
        /// <param name="RemoveFromThis"></param>
        private void ReplaceEdge(Edge edge, Face RemoveFromThis)
        {
            for (int i = 0; i < outline.Length; i++)
            {
                if (outline[i].IsSame(edge))
                {
                    if (edge.PrimaryFace == RemoveFromThis)
                    {
                        edge.CopyPrimary(outline[i], this);
                    }
                    else
                    {
                        edge.CopySecondary(outline[i], this);
                    }
                    outline[i] = edge;
                }
            }
        }
        internal GeoPoint2D[] GetLineIntersection2D(GeoPoint sp, GeoVector direction)
        {
            GeoPoint2D[] all = this.surface.GetLineIntersection(sp, direction);
            List<GeoPoint2D> res = new List<GeoPoint2D>();
            BoundingRect ext = Area.GetExtent();
            for (int i = 0; i < all.Length; i++)
            {
                if (Area.Contains(all[i], true))
                {
                    res.Add(all[i]);
                }
                else
                {
                    // Problem: bei einer zyklischen Oberfläche kann der Schnittpunkt in einem anderen
                    // Zyklusbereich liegen als die Area. Hier wird ggf. der Punkt in den passenden zyklus verschoben
                    // und erneut getestet
                    if (surface.IsUPeriodic)
                    {
                        if (all[i].x > ext.Right)
                        {
                            while (all[i].x > ext.Left)
                            {
                                all[i].x -= surface.UPeriod;
                                if (Area.Contains(all[i], true))
                                {
                                    res.Add(all[i]);
                                    break;
                                }
                            }
                        }
                        if (all[i].x < ext.Left)
                        {
                            while (all[i].x < ext.Right)
                            {
                                all[i].x += surface.UPeriod;
                                if (Area.Contains(all[i], true))
                                {
                                    res.Add(all[i]);
                                    break;
                                }
                            }
                        }
                    }
                    if (surface.IsVPeriodic)
                    {
                        if (all[i].y > ext.Top)
                        {
                            while (all[i].y > ext.Bottom)
                            {
                                all[i].y -= surface.VPeriod;
                                if (Area.Contains(all[i], true))
                                {
                                    res.Add(all[i]);
                                    break;
                                }
                            }
                        }
                        if (all[i].y < ext.Bottom)
                        {
                            while (all[i].y < ext.Top)
                            {
                                all[i].y += surface.VPeriod;
                                if (Area.Contains(all[i], true))
                                {
                                    res.Add(all[i]);
                                    break;
                                }
                            }
                        }
                    }
                }
            }
            return res.ToArray();
        }
        internal GeoPoint[] GetLineIntersection(GeoPoint sp, GeoVector direction)
        {
            GeoPoint2D[] all = this.surface.GetLineIntersection(sp, direction);
            List<GeoPoint> res = new List<GeoPoint>();
            for (int i = 0; i < all.Length; i++)
            {
                if (Contains(ref all[i], true)) // also tests for periodic cases and moves the point into the correct periodic domain
                {
                    res.Add(surface.PointAt(all[i]));
                }
            }
            return res.ToArray();
        }
        internal void GetZMinMax(Projection p, out double zMin, out double zMax)
        {
            zMin = double.MaxValue;
            zMax = double.MinValue;
            double umin, umax, vmin, vmax;
            GetUVBounds(out umin, out umax, out vmin, out vmax);
            surface.GetZMinMax(p, umin, umax, vmin, vmax, ref zMin, ref zMax);
        }
        /// <summary>
        /// Returns the orientation of this face. this is only meaningful if this face is part of a closed shell
        /// which defines a solid. If true, the normal vector of the surface of this face points outward of the solid,
        /// if false it points into the inside.
        /// </summary>
        public bool OrientedOutward
        {
            get
            {
                return orientedOutward;
            }
            internal set
            {
                orientedOutward = value;
            }
        }
        internal bool IsValid
        {
            get
            {
                for (int i = 0; i < outline.Length; ++i)
                {
                    if (outline[i].PrimaryFace != this && outline[i].SecondaryFace != this) return false;
                }
                for (int j = 0; j < holes.Length; ++j)
                {
                    for (int i = 0; i < holes[j].Length; ++i)
                    {
                        if (holes[j][i].PrimaryFace != this && holes[j][i].SecondaryFace != this) return false;
                    }
                }
                return true;
            }
        }
        #region IGetSubShapes Members
        IGeoObject IGetSubShapes.GetEdge(int[] id, int index)
        {
            if (index >= id.Length) return null; // kommt vor, wenn eine Fläche getroffen wurde und keine Kante
            Edge[] allEdges = AllEdges;
            for (int i = 0; i < allEdges.Length; ++i)
            {
                IGeoObject go = allEdges[i].Curve3D as IGeoObject;
                if (go != null)
                {
                    if (go.UniqueId == id[index])
                    {
                        return go;
                    }
                }
            }
            return null; // sollte nicht vorkommen
        }
        IGeoObject IGetSubShapes.GetFace(int[] id, int index)
        {
            if (this.UniqueId == id[index]) return this;
            return null;
        }
        #endregion

        /// <summary>
        /// Returns the triangulation of this face to the provided precision.
        /// <paramref name="trianglePoint"/> and <paramref name="triangleUVPoint"/> are two arrays of the same length specifying the
        /// vertices of the triangle in 3d or 2d surface coordinates. <paramref name="triangleIndex"/> is a list of indizes to
        /// trianglePoint and triangleUVPoint where each triple of indices describes one triangle. The length of triangleIndex is a multiple of 3.
        /// </summary>
        /// <param name="precision">Required precision</param>
        /// <param name="trianglePoint">Resulting 3d points</param>
        /// <param name="triangleUVPoint">Resulting 2d points</param>
        /// <param name="triangleIndex">Triangle indizes</param>
        /// <param name="triangleExtent">Extent of the triangles</param>
        public void GetTriangulation(double precision, out GeoPoint[] trianglePoint, out GeoPoint2D[] triangleUVPoint, out int[] triangleIndex, out BoundingCube triangleExtent)
        {
            AssureTriangles(precision);
            trianglePoint = this.trianglePoint;
            triangleUVPoint = this.triangleUVPoint;
            triangleIndex = this.triangleIndex;
            triangleExtent = GetExtent(precision);
        }

        /// <summary>
        /// PRELIMINARY!
        /// Tries to use a more simple surface for this face. E.g. a NurbsSurface will be reduced to a torodial surface
        /// if the data allows it. The followind replacements are currently performed
        /// <list type="">
        /// <item>NurbsSurface -> PlanarSurface</item>
        /// <item>NurbsSurface -> TorodialSurface</item>
        /// <item>in work:</item>
        /// <item>NurbsSurface -> CylindricalSurface</item>
        /// <item>NurbsSurface -> SphericalSurface</item>
        /// <item>NurbsSurface -> ConicalSurface</item>
        /// <item>SurfaceOfRevolution -> CylindricalSurface</item>
        /// <item>SurfaceOfRevolution -> TorodialSurface</item>
        /// <item>SurfaceOfRevolution -> ConicalSurface</item>
        /// <item>SurfaceOfLinearExtrusion -> PlanarSurface</item>
        /// <item>SurfaceOfLinearExtrusion -> CylindricalSurface</item>
        /// </list>
        /// </summary>
        /// <param name="maxError">The maximum aberration of the surfaces</param>
        /// <returns>true if surface could be simplyfied</returns>
        public bool MakeRegularSurface(double maxError)
        {
            if (surface is NurbsSurface)
            {
                ISurface simpleSurface;
                ModOp2D modify;
                if ((surface as NurbsSurface).GetSimpleSurface(maxError, out simpleSurface, out modify))
                {
                    foreach (Edge edge in AllEdgesIterated())
                    {
                        if (edge.PrimaryFace == this)
                        {
                            edge.PrimaryCurve2D = edge.PrimaryCurve2D.GetModified(modify);
                        }
                        if (edge.SecondaryFace == this)
                        {
                            edge.SecondaryCurve2D = edge.SecondaryCurve2D.GetModified(modify);
                        }
                    }
                    this.surface = simpleSurface;
                    this.area = null;
                    SimpleShape forceArea = Area;
                    return true;
                }
            }
            return false;
        }
        internal ModOp2D MakeRegularSurface(double maxError, Set<Edge> recalcEdges)
        {
            if (surface is NurbsSurface)
            {
                ISurface simpleSurface;
                ModOp2D modify;
                if ((surface as NurbsSurface).GetSimpleSurface(maxError, out simpleSurface, out modify))
                {   // die Kanten werden grundlegend neu berechnet, die 2D Kurven werden neu gemacht, es ist hier also
                    // keine Modifikation der 2D Kurven nötig.
                    recalcEdges.AddMany(AllEdgesIterated());
                    this.surface = simpleSurface;
                    this.area = null; // aber noch nicht neu berechnen
                    return modify;
                }
#if DEBUG
                //this.colorDef = new ColorDef("DEBUG", System.Drawing.Color.Red);
                //trianglePoint = null;
#endif
            }
            return ModOp2D.Identity;
        }
        internal void PreCalcTriangulation(double precision)
        {
            AssureTriangles(precision);
        }
        internal void IntersectAndPosition(Edge edg, out GeoPoint[] ip, out GeoPoint2D[] uvOnFace, out double[] uOnCurve3D, out Border.Position[] position, double prec = 0.0)
        {
            if (prec == 0.0) prec = Precision.eps;
            GeoPoint[] ips;
            GeoPoint2D[] uvOnFaces;
            double[] uOnCurve3Ds;
            surface.Intersect(edg.Curve3D, this.GetUVBounds(), out ips, out uvOnFaces, out uOnCurve3Ds);
            if (ips.Length == 0 && surface.GetDistance(edg.Curve3D.StartPoint) < prec && surface.GetDistance(edg.Curve3D.EndPoint) < prec && surface.GetDistance(edg.Curve3D.PointAt(0.5)) < prec)
            {
                // the curve resides in the surface
                ips = new GeoPoint[] { edg.Curve3D.StartPoint, edg.Curve3D.EndPoint };
                uvOnFaces = new GeoPoint2D[] { surface.PositionOf(edg.Curve3D.StartPoint), surface.PositionOf(edg.Curve3D.EndPoint) };
                uOnCurve3Ds = new double[] { 0.0, 1.0 };
            }
            List<GeoPoint> lip = new List<GeoPoint>();
            List<GeoPoint2D> luvOnFace = new List<GeoPoint2D>();
            List<double> luOnCurve3D = new List<double>();
            List<Border.Position> lposition = new List<Shapes.Border.Position>();
            for (int i = 0; i < ips.Length; ++i)
            {
                if (Contains(ref uvOnFaces[i], true) && uOnCurve3Ds[i] >= -1e-6 && uOnCurve3Ds[i] <= 1.0 + 1e-6) // geändert auf 1e-6, da wir in BRepIntersection das so brauchen
                {
                    lip.Add(ips[i]);
                    luvOnFace.Add(uvOnFaces[i]);
                    luOnCurve3D.Add(uOnCurve3Ds[i]);
                    lposition.Add(Area.GetPosition(uvOnFaces[i], prec));
                }
                else if (uOnCurve3Ds[i] >= -1e-6 && uOnCurve3Ds[i] <= 1.0 + 1e-6) // check on border or edge
                {
                    bool added = false;
                    foreach (Vertex vtx in Vertices)
                    {
                        if ((vtx.Position | ips[i]) < prec)
                        {
                            lip.Add(ips[i]);
                            luvOnFace.Add(uvOnFaces[i]);
                            luOnCurve3D.Add(uOnCurve3Ds[i]);
                            lposition.Add(Border.Position.OnCurve);
                            added = true;
                            break;
                        }
                    }
                    if (!added)
                    {   // BRepOperation needs the point if it is close egnough to an edge
                        foreach (Edge edge in AllEdgesIterated())
                        {
                            if (edge.Curve3D != null)
                            {
                                if (edge.Curve3D.DistanceTo(ips[i]) < prec)
                                {
                                    lip.Add(ips[i]);
                                    luvOnFace.Add(uvOnFaces[i]);
                                    luOnCurve3D.Add(uOnCurve3Ds[i]);
                                    lposition.Add(Border.Position.OnCurve);
                                    break;
                                }
                            }
                        }
                    }
                }

            }
            ip = lip.ToArray();
            uvOnFace = luvOnFace.ToArray();
            uOnCurve3D = luOnCurve3D.ToArray();
            position = lposition.ToArray();
        }

        internal void Intersect(Edge edg, out GeoPoint[] ip, out GeoPoint2D[] uvOnFace, out double[] uOnCurve3D)
        {
            GeoPoint[] ips;
            GeoPoint2D[] uvOnFaces;
            double[] uOnCurve3Ds;
            // Schade, dass man im folgenden die triangulierung nicht weitergeben kann
            // aber in den Randbereichen ist sie evtl. ungenügend.
            surface.Intersect(edg.Curve3D, this.GetUVBounds(), out ips, out uvOnFaces, out uOnCurve3Ds);
            List<GeoPoint> lip = new List<GeoPoint>();
            List<GeoPoint2D> luvOnFace = new List<GeoPoint2D>();
            List<double> luOnCurve3D = new List<double>();
            for (int i = 0; i < ips.Length; ++i)
            {
                if (Contains(ref uvOnFaces[i], true) && uOnCurve3Ds[i] >= -1e-6 && uOnCurve3Ds[i] <= 1.0 + 1e-6) // geändert auf 1e-6, da wir in BRepIntersection das so brauchen
                {
                    lip.Add(ips[i]);
                    luvOnFace.Add(uvOnFaces[i]);
                    luOnCurve3D.Add(uOnCurve3Ds[i]);
                }
            }
            ip = lip.ToArray();
            uvOnFace = luvOnFace.ToArray();
            uOnCurve3D = luOnCurve3D.ToArray();
        }
        internal void Intersect(ICurve curve, out GeoPoint[] ip, out GeoPoint2D[] uvOnFace, out double[] uOnCurve)
        {
            GeoPoint[] ips;
            GeoPoint2D[] uvOnFaces;
            double[] uOnCurve3Ds;
            // Schade, dass man im folgenden die triangulierung nicht weitergeben kann
            // aber in den Randbereichen ist sie evtl. ungenügend.
            surface.Intersect(curve, this.GetUVBounds(), out ips, out uvOnFaces, out uOnCurve3Ds);
            List<GeoPoint> lip = new List<GeoPoint>();
            List<GeoPoint2D> luvOnFace = new List<GeoPoint2D>();
            List<double> luOnCurve3D = new List<double>();
            for (int i = 0; i < ips.Length; ++i)
            {
                if (Contains(ref uvOnFaces[i], true) && uOnCurve3Ds[i] >= 0.0 && uOnCurve3Ds[i] <= 1.0)
                {
                    lip.Add(ips[i]);
                    luvOnFace.Add(uvOnFaces[i]);
                    luOnCurve3D.Add(uOnCurve3Ds[i]);
                }
            }
            ip = lip.ToArray();
            uvOnFace = luvOnFace.ToArray();
            uOnCurve = luOnCurve3D.ToArray();
        }
        internal Edge[] FindAdjacentEdges(Edge edge)
        {   // sehr ineffizient, viel besser wäre es, die Kanten beim Erzeugen
            // oder auf Bedarf in einem Face zu verbinden, also Edge.Previous(Face) bzw. Edge.Next(Face) zu implementieren
            if (outline != null)
            {
                for (int i = 0; i < outline.Length; ++i)
                {
                    if (outline[i] == edge)
                    {
                        if (outline.Length == 1) return new Edge[0]; // es gibt keine angrenzenden außer es selbst
                        int i0 = i - 1;
                        int i1 = i + 1;
                        if (i0 < 0) i0 = outline.Length - 1;
                        if (i1 > outline.Length - 1) i1 = 0;
                        if (i1 == i0) return new Edge[] { outline[i0] };
                        return new Edge[] { outline[i0], outline[i1] };
                    }
                }
            }
            if (holes != null)
            {
                for (int j = 0; j < holes.Length; ++j)
                {
                    for (int i = 0; i < holes[j].Length; ++i)
                    {
                        if (holes[j][i] == edge)
                        {
                            if (holes[j].Length == 1) return new Edge[0]; // es gibt keine angrenzenden außer es selbst
                            int i0 = i - 1;
                            int i1 = i + 1;
                            if (i0 < 0) i0 = holes[j].Length - 1;
                            if (i1 > holes[j].Length - 1) i1 = 0;
                            if (i1 == i0) return new Edge[] { holes[j][i0] };
                            return new Edge[] { holes[j][i0], holes[j][i1] };
                        }
                    }
                }
            }
            return new Edge[0];
        }
        /// <summary>
        /// Collect all faces connected to this face without traversing the edges in <paramref name="bounds"/>
        /// </summary>
        /// <param name="collection"></param>
        /// <param name="bounds"></param>
        internal void CollectConnectedFaces(HashSet<Face> collection, ICollection<Edge> bounds)
        {
            if (!collection.Contains(this))
            {
                collection.Add(this);
                foreach (Edge edge in AllEdgesIterated())
                {
                    if (!bounds.Contains(edge)) edge.OtherFace(this).CollectConnectedFaces(collection, bounds);
                }
            }
        }
        internal void MakeTopologicalOrientation()
        {   // die Richtung der Löcher wird umgedreht, es wäre wohl besser immer mit den so orientierten Löchern zu arbeiten
            // man darf sie natürlich nicht ein zweites mal umdrehen
            //for (int i = 0; i < holes.Length; ++i)
            //{
            //    for (int j = 0; j < holes[i].Length; ++j)
            //    {
            //        holes[i][j].Reverse(this);
            //    }
            //    Array.Reverse(holes[i]);
            //}
            // umgestellt, es ist jetzt immer richtig orientiert, wenn Area berechnet ist
            if (area == null) area = Area;
            if (!orientedOutward)
            {
                ModOp2D m = surface.ReverseOrientation();
                ICurve2D[] segments = new ICurve2D[outline.Length];
#if DEBUG
                DebuggerContainer dc = new DebuggerContainer();
                for (int i = 0; i < outline.Length; ++i)
                {
                    dc.Add(outline[i].Curve2D(this), System.Drawing.Color.Blue, i);
                }
#endif
                for (int i = 0; i < outline.Length; ++i)
                {
                    segments[i] = outline[i].ModifyCurve2D(this, segments, m);
                    segments[i].Reverse(); // 11.3.15
                }
#if DEBUG
                for (int i = 0; i < outline.Length; ++i)
                {
                    dc.Add(segments[i], System.Drawing.Color.Red, i);
                }

#endif
                Array.Reverse(outline);
                Set<Edge> seam = new Set<Edge>(); // die Saumkurven ansammeln (jede kommt zweimal vor
                for (int i = 0; i < outline.Length; ++i)
                {
                    if (outline[i].IsSeam()) seam.Add(outline[i]);
                }
                foreach (Edge e in seam)
                {   // die Reihenfolge von Saumkurven wird durch Reverse des Arrays nicht vertauscht
                    // deshalb hier die beiden Kurven vertauschen
                    ICurve2D tmp = e.PrimaryCurve2D;
                    e.PrimaryCurve2D = e.SecondaryCurve2D;
                    e.SecondaryCurve2D = tmp;
                }
                for (int j = 0; j < holes.Length; ++j)
                {
                    for (int i = 0; i < holes[j].Length; ++i)
                    {
#if DEBUG
                        ICurve2D dbg = holes[j][i].Curve2D(this);
                        dc.Add(dbg);
                        double l = dbg.Length;
#endif
                        ICurve2D modified = holes[j][i].ModifyCurve2D(this, null, m);
                        modified.Reverse();
#if DEBUG
                        dbg = holes[j][i].Curve2D(this);
                        dc.Add(dbg);
                        if (Math.Abs(l - dbg.Length) > 1)
                        {

                        }
#endif
                    }
                    Array.Reverse(holes[j]);
                }
                area = null;
                area = Area; // neu bestimmen
                orientedOutward = true;
            }
        }
        internal void Repair()
        {
            List<Edge> ordered = new List<Edge>();
            Edge startWith = outline[0];
            ordered.Add(startWith);
            Edge edg = GetNextEdge(startWith);
            while (edg != startWith)
            {
                ordered.Add(edg);
                edg = GetNextEdge(edg);
                if (ordered.Count > outline.Length) return;
            }
            outline = ordered.ToArray();
        }
        internal void RepairConnected()
        {
            for (int i = 0; i < outline.Length; i++)
            {
                int next = i + 1;
                if (next >= outline.Length) next = 0;
                if (outline[i].EndVertex(this) != outline[next].StartVertex(this))
                {
                    if (outline[next].IsSeam() || outline[i].IsSeam()) continue; // start and endvertex of a seam is ambiguous
                    GeoPoint p = outline[next].StartVertex(this).Position;
                    outline[i].EndVertex(this).MergeWith(outline[next].StartVertex(this));
                    outline[i].EndVertex(this).AdjustCoordinate(p);
                }
            }
            for (int i = 0; i < holes.Length; i++)
            {
                for (int j = 0; j < holes[i].Length; j++)
                {
                    int next = j + 1;
                    if (next >= holes[i].Length) next = 0;
                    if (holes[i][j].EndVertex(this) != holes[i][next].StartVertex(this))
                    {
                        if (holes[i][next].IsSeam() || holes[i][j].IsSeam()) continue; // start and endvertex of a seam is ambiguous
                        GeoPoint p = holes[i][next].StartVertex(this).Position;
                        holes[i][j].EndVertex(this).MergeWith(holes[i][next].StartVertex(this));
                        holes[i][j].EndVertex(this).AdjustCoordinate(p);
                    }
                }
            }
        }

        internal Edge OtherEgde(Vertex vtx, Edge edg)
        {
            List<Edge> edgs = vtx.EdgesOnFace(this);
            for (int i = 0; i < edgs.Count; i++)
            {
                if (edgs[i] != edg) return edgs[i];
            }
            return null;
        }

        /// <summary>
        /// Reverses the orientation of this Face. the normal vector on any point of the surface will point to 
        /// the opposite direction
        /// </summary>
        public void ReverseOrientation()
        {
            // die Verwendung von "orientedOutward" ist ziemlich unklar:
            // Für die boolschen Operationen soll eine Shell so invertiert werden, dass es eine gültige Shell bleibt,
            // die genau das Inverse zur bestehenden Shell ist

            // ACHTUNG: das muss vereinhetlicht werden. MakeReverseOrientation macht eigentlich genau das selbe, nur dass orientedOutward berücksichtigt wird.
            // orientedOutward muss abgeschafft werden!!

#if DEBUG
            DebuggerContainer dc = new CADability.DebuggerContainer();
            ICurve2D[] sss = new ICurve2D[outline.Length];
            for (int i = 0; i < outline.Length; ++i)
            {
                sss[i] = outline[i].Curve2D(this, sss);
                dc.Add(sss[i], System.Drawing.Color.Red, outline[i].GetHashCode());
            }
            for (int j = 0; j < holes.Length; ++j)
            {
                for (int i = 0; i < holes[j].Length; ++i)
                {
                    dc.Add(holes[j][i].Curve2D(this), System.Drawing.Color.Blue, holes[j][i].GetHashCode());
                }
            }
            GeoPoint2D dbgv1 = surface.PositionOf(Vertices[0].Position);
#endif
            BoundingRect modifiedBounds = Area.GetExtent();
            ModOp2D m = surface.ReverseOrientation();
            modifiedBounds.Modify(m);
            ICurve2D[] segments = new ICurve2D[outline.Length];
            for (int i = 0; i < outline.Length; ++i)
            {
                segments[i] = outline[i].ModifyCurve2D(this, segments, m);
                segments[i].Reverse();
            }
            Array.Reverse(outline);
            Set<Edge> seam = new Set<Edge>(); // die Saumkurven ansammeln (jede kommt zweimal vor
            for (int i = 0; i < outline.Length; ++i)
            {
                if (outline[i].IsSeam()) seam.Add(outline[i]);
            }
            foreach (Edge e in seam)
            {   // die Reihenfolge von Saumkurven wird durch Reverse des Arrays nicht vertauscht
                // deshalb hier die beiden Kurven vertauschen
                ICurve2D tmp = e.PrimaryCurve2D;
                e.PrimaryCurve2D = e.SecondaryCurve2D;
                e.SecondaryCurve2D = tmp;
            }
            for (int j = 0; j < holes.Length; ++j)
            {
                for (int i = 0; i < holes[j].Length; ++i)
                {
                    ICurve2D modified = holes[j][i].ModifyCurve2D(this, null, m);
                    modified.Reverse();
                }
                Array.Reverse(holes[j]);
            }
            foreach (Vertex vtx in Vertices)
            {
                vtx.RemovePositionOnFace(this);
            }
            foreach (Edge edg in AllEdgesIterated())
            {
                edg.Orient(); // ModifyCurve2D unsets the "oriented"-Flag of the edge. Maybe the edge has already been oriented, so ReverseOrientation(this) doesn't help
                if (edg.Curve3D is InterpolatedDualSurfaceCurve)
                {
                    if (edg.PrimaryFace == this) edg.Curve3D = new InterpolatedDualSurfaceCurve(this.surface, modifiedBounds, edg.SecondaryFace.surface, edg.SecondaryFace.Area.GetExtent(), (edg.Curve3D as InterpolatedDualSurfaceCurve).BasePoints);
                    else edg.Curve3D = new InterpolatedDualSurfaceCurve(edg.PrimaryFace.surface, edg.PrimaryFace.Area.GetExtent(), this.surface, modifiedBounds, (edg.Curve3D as InterpolatedDualSurfaceCurve).BasePoints);
                    edg.PrimaryCurve2D = (edg.Curve3D as InterpolatedDualSurfaceCurve).CurveOnSurface1;
                    if (!edg.Forward(edg.PrimaryFace)) edg.PrimaryCurve2D.Reverse();
                    edg.SecondaryCurve2D = (edg.Curve3D as InterpolatedDualSurfaceCurve).CurveOnSurface2;
                    if (!edg.Forward(edg.SecondaryFace)) edg.SecondaryCurve2D.Reverse();
                }
            }
            area = null;
            SimpleShape ss = Area; // force area recalc
#if DEBUG
            GeoPoint2D dbgv2 = surface.PositionOf(Vertices[0].Position);
            GeoPoint2D dbgv3 = m * dbgv1;
            sss = new ICurve2D[outline.Length];
            for (int i = 0; i < outline.Length; ++i)
            {
                sss[i] = outline[i].Curve2D(this, sss);
                dc.Add(sss[i], System.Drawing.Color.Green, outline[i].GetHashCode());
            }
            for (int j = 0; j < holes.Length; ++j)
            {
                for (int i = 0; i < holes[j].Length; ++i)
                {
                    dc.Add(holes[j][i].Curve2D(this), System.Drawing.Color.Violet, holes[j][i].GetHashCode());
                }
            }
            for (int i = 0; i < sss.Length; i++)
            {
                dc.Add(surface.Make3dCurve(sss[i]) as IGeoObject);
            }
            //GeoPoint[] trianglePoint;
            //GeoPoint2D[] triangleUVPoint;
            //int[] triangleIndex;
            //BoundingCube triangleExtent;
            //Face deser = (Face)Project.SerializeDeserialize(this);
            //deser.GetTriangulation(0.01, out trianglePoint, out triangleUVPoint, out triangleIndex, out triangleExtent);
            //DebuggerContainer dctr2d = new CADability.DebuggerContainer();
            //DebuggerContainer dctr3d = new CADability.DebuggerContainer();
            //for (int ti = 0; ti < triangleIndex.Length; ti += 3)
            //{
            //    Line2D l1 = new Line2D(triangleUVPoint[triangleIndex[ti]], triangleUVPoint[triangleIndex[ti + 1]]);
            //    Line2D l2 = new Line2D(triangleUVPoint[triangleIndex[ti + 1]], triangleUVPoint[triangleIndex[ti + 2]]);
            //    Line2D l3 = new Line2D(triangleUVPoint[triangleIndex[ti + 2]], triangleUVPoint[triangleIndex[ti]]);
            //    dctr2d.Add(l1, System.Drawing.Color.Red, ti);
            //    dctr2d.Add(l2, System.Drawing.Color.Red, ti);
            //    dctr2d.Add(l3, System.Drawing.Color.Red, ti);
            //    Line l11 = Line.MakeLine(trianglePoint[triangleIndex[ti]], trianglePoint[triangleIndex[ti + 1]]);
            //    Line l12 = Line.MakeLine(trianglePoint[triangleIndex[ti + 1]], trianglePoint[triangleIndex[ti + 2]]);
            //    Line l13 = Line.MakeLine(trianglePoint[triangleIndex[ti + 2]], trianglePoint[triangleIndex[ti]]);
            //    dctr3d.Add(l11, ti);
            //    dctr3d.Add(l12, ti + 1);
            //    dctr3d.Add(l13, ti + 2);
            //}
#endif
        }
        internal void MakeInverseOrientation()
        {   // Das Innere wird nach außen gekehrt
            // man darf sie natürlich nicht ein zweites mal umdrehen
            // Das Umdrehen der Löcher darf nicht mehr ausgeführt werden, sie müssen immer richtigrum sein
            //for (int i = 0; i < holes.Length; ++i)
            //{
            //    for (int j = 0; j < holes[i].Length; ++j)
            //    {
            //        holes[i][j].Reverse(this);
            //    }
            //    Array.Reverse(holes[i]);
            //}
            if (orientedOutward)
            {
                ReverseOrientation(); // das ist identisch mit diesem hier!!
                return;
                SimpleShape ss = Area;
                BoundingRect ext = ss.GetExtent();
                GeoPoint2D c = ss.GetExtent().GetCenter();
                ModOp2D m = surface.ReverseOrientation();
                ext.Modify(m);
                ICurve2D[] segments = new ICurve2D[outline.Length];
                for (int i = 0; i < outline.Length; ++i)
                {
                    if (outline[i].Curve3D is InterpolatedDualSurfaceCurve && outline[i].SecondaryFace != null)
                    {
                        GeoPoint[] bp = (outline[i].Curve3D as InterpolatedDualSurfaceCurve).BasePoints;
                        if (outline[i].PrimaryFace == this)
                        {
                            outline[i].Curve3D = new InterpolatedDualSurfaceCurve(surface, ext, outline[i].SecondaryFace.Surface, outline[i].SecondaryFace.Area.GetExtent(), bp);
                        }
                        else
                        {
                            outline[i].Curve3D = new InterpolatedDualSurfaceCurve(outline[i].PrimaryFace.Surface, outline[i].PrimaryFace.Area.GetExtent(), surface, ext, bp);
                        }
                        segments[i] = surface.GetProjectedCurve(outline[i].Curve3D, 0.0);
                        if (outline[i].Forward(this)) segments[i].Reverse();
                        if (outline[i].PrimaryFace == this) outline[i].PrimaryCurve2D = segments[i];
                        else outline[i].SecondaryCurve2D = segments[i];
                    }
                    else
                    {
                        segments[i] = outline[i].ModifyCurve2D(this, segments, m);
                        segments[i].Reverse(); // 11.3.15 wg. 97871_086103_032.cdb
                    }
                }
                Array.Reverse(outline);
                Set<Edge> seam = new Set<Edge>(); // die Saumkurven ansammeln (jede kommt zweimal vor
                for (int i = 0; i < outline.Length; ++i)
                {
                    if (outline[i].IsSeam()) seam.Add(outline[i]);
                }
                foreach (Edge e in seam)
                {   // die Reihenfolge von Saumkurven wird durch Reverse des Arrays nicht vertauscht
                    // deshalb hier die beiden Kurven vertauschen
                    ICurve2D tmp = e.PrimaryCurve2D;
                    e.PrimaryCurve2D = e.SecondaryCurve2D;
                    e.SecondaryCurve2D = tmp;
                }
                for (int j = 0; j < holes.Length; ++j)
                {
                    for (int i = 0; i < holes[j].Length; ++i)
                    {
                        if (holes[j][i].Curve3D is InterpolatedDualSurfaceCurve && holes[j][i].SecondaryFace != null)
                        {
                            GeoPoint[] bp = (holes[j][i].Curve3D as InterpolatedDualSurfaceCurve).BasePoints;
                            if (holes[j][i].PrimaryFace == this)
                            {
                                holes[j][i].Curve3D = new InterpolatedDualSurfaceCurve(surface, ext, holes[j][i].SecondaryFace.Surface, holes[j][i].SecondaryFace.Area.GetExtent(), bp);
                            }
                            else
                            {
                                holes[j][i].Curve3D = new InterpolatedDualSurfaceCurve(holes[j][i].PrimaryFace.Surface, holes[j][i].PrimaryFace.Area.GetExtent(), surface, ext, bp);
                            }
                            ICurve2D modified = surface.GetProjectedCurve(holes[j][i].Curve3D, 0.0);
                            if (holes[j][i].Forward(this)) modified.Reverse();
                            if (holes[j][i].PrimaryFace == this) holes[j][i].PrimaryCurve2D = modified;
                            else holes[j][i].SecondaryCurve2D = modified;
                        }
                        else
                        {
                            ICurve2D modified = holes[j][i].ModifyCurve2D(this, null, m);
                            modified.Reverse(); // 11.3.15
                        }
                    }
                    Array.Reverse(holes[j]);
                }
                area = null;
                ss = Area; // DEBUG

                orientedOutward = false;
            }
            else
            {
                Array.Reverse(outline);
                Set<Edge> seam = new Set<Edge>(); // die Saumkurven ansammeln (jede kommt zweimal vor
                for (int i = 0; i < outline.Length; ++i)
                {
                    if (outline[i].IsSeam()) seam.Add(outline[i]);
                }
                foreach (Edge e in seam)
                {   // die Reihenfolge von Saumkurven wird durch Reverse des Arrays nicht vertauscht
                    // deshalb hier die beiden Kurven vertauschen
                    ICurve2D tmp = e.PrimaryCurve2D;
                    e.PrimaryCurve2D = e.SecondaryCurve2D;
                    e.SecondaryCurve2D = tmp;
                }
                for (int j = 0; j < holes.Length; ++j)
                {
                    Array.Reverse(holes[j]);
                }
            }
            area = null;
            area = Area; // neu bestimmen
            Orient();
        }
        /// <summary>
        /// Mache ein neues Face, welches diesem entspricht und durch die neue outline bzw. holes gegeben ist
        /// Die Kanten wandern von diesem Face zum neuen, d.h. das primaryFace bzw. secondaryFace der Kanten wird geändert.
        /// Somit wird dieses Face ungültig.
        /// </summary>
        /// <param name="outlines"></param>
        /// <param name="holes"></param>
        /// <returns></returns>
        internal Face Split(List<Edge> outlines, List<List<Edge>> holes)
        {   // komisch, die holes sind rechtsrum, warum funktioniert es trotzdem?
            Face res = new Face(); // construct? wozu?
            res.surface = surface.Clone();
            res.outline = new Edge[outlines.Count];
            if (holes != null)
            {
                res.holes = new Edge[holes.Count][];
            }
            else res.holes = new Edge[0][];
            for (int i = 0; i < outlines.Count; ++i)
            {
                res.outline[i] = outlines[i];
                res.outline[i].ReplaceFace(this, res);
                res.outline[i].SetNotOriented();
            }
            for (int i = 0; i < res.holes.Length; ++i)
            {
                res.holes[i] = new Edge[holes[i].Count];
                for (int j = 0; j < res.holes[i].Length; ++j)
                {
                    res.holes[i][j] = holes[i][j];
                    res.holes[i][j].ReplaceFace(this, res);
                    res.holes[i][j].SetNotOriented();
                }
            }
            return res;
        }
        public List<Face> SplitPeriodicFace()
        {
            if (!surface.IsUPeriodic && !surface.IsVPeriodic) return null;
            bool splitu;
            splitu = surface.IsUPeriodic; // kann aber beides sein
            Edge seamEdge = null;
            for (int i = 0; i < outline.Length; ++i)
            {
                if (outline[i].IsPeriodicEdge && !outline[i].IsSingular())
                {
                    seamEdge = outline[i];
                    break;
                }
            }
            if (seamEdge == null) return null;
            SimpleShape dbgss = this.Area; // ohne diesen Aufruf gehts manchmal schief
            BoundingRect ext = GetUVBounds();
            List<Face> res = new List<Face>();
            if (surface.IsVPeriodic && surface.IsUPeriodic)
            {
                GeoVector2D sdir = seamEdge.Curve2D(this).StartDirection;
                splitu = Math.Abs(sdir.y) > Math.Abs(sdir.x); // die Richtung des Saums geht nach v, also u splitten
                GeoPoint2D ssp1 = seamEdge.PrimaryCurve2D.StartPoint; // beide Kurven sind ja auf diesem Face
                GeoPoint2D ssp2 = seamEdge.SecondaryCurve2D.EndPoint; // aber entgegengesetzt orientiert
                double dx = Math.Abs(ssp1.x - ssp2.x);
                double dy = Math.Abs(ssp1.y - ssp2.y);
                // einer der beiden Werte sollte jetzt die Periode sein, der andere 0
                if (dx > dy)
                {
                    splitu = true;
                }
                else
                {
                    splitu = false;
                }
            }
            GeoPoint2D sp, ep;
            double halfPeriod;
            if (!splitu)
            {
                double v = (ext.Bottom + ext.Top) / 2.0;
                sp = new GeoPoint2D(ext.Left, v);
                ep = new GeoPoint2D(ext.Right, v);
                halfPeriod = surface.VPeriod / 2.0;
                //if (ext.Height < surface.VPeriod * 0.999) return null; Es muss geteilt werden, der Saum muss verschwinden
            }
            else
            {
                double u = (ext.Right + ext.Left) / 2.0;
                sp = new GeoPoint2D(u, ext.Bottom - ext.Size);
                ep = new GeoPoint2D(u, ext.Top + ext.Size);
                halfPeriod = surface.UPeriod / 2.0;
                //if (ext.Width < surface.UPeriod * 0.999) return null;
            }
            Line2D intersectWith = new Line2D(sp, ep);
            CompoundShape cs = dbgss.Split(new Border(intersectWith)); // das kann jetzt viele Stücke geben
            if (cs.SimpleShapes.Length < 2)
            {   // das sollte nicht vorkommen, kommt aber bei dummen Grenzfällen vor, weil BorderOperation nicht gut funktioniert
                if (!splitu)
                {
                    intersectWith.Move(0, halfPeriod * 1e-3);
                }
                else
                {
                    intersectWith.Move(halfPeriod * 1e-3, 0);
                }
                cs = dbgss.Split(new Border(intersectWith)); // das kann jetzt viele Stücke geben
            }
            for (int i = 0; i < cs.SimpleShapes.Length; i++)
            {
                res.Add(Face.MakeFace(surface.Clone(), cs.SimpleShapes[i])); // surface muss gecloned werden, denn die wird u.U. geändert (invertiert)
            }
            // noch rekursiv aufteilen wenn beides der Fall ist
            if (surface.IsVPeriodic && surface.IsUPeriodic)
            {

            }
            return res;
        }

        public List<Face> SplitSingularFace()
        {
            double[] us = surface.GetUSingularities();
            double[] vs = surface.GetVSingularities();
            if ((us == null || us.Length == 0) && (vs == null || vs.Length == 0)) return null;

            SimpleShape dbgss = this.Area; // ohne diesen Aufruf gehts manchmal schief
            BoundingRect ext = GetUVBounds();
            List<Face> res = new List<Face>();

            CompoundShape cs = new CompoundShape(Area.Clone());
            if (us != null)
            {
                for (int i = 0; i < us.Length; i++)
                {
                    GeoPoint2D sp = new GeoPoint2D(us[i], ext.Bottom);
                    GeoPoint2D ep = new GeoPoint2D(us[i], ext.Top);
                    SurfaceHelper.AdjustPeriodic(surface, ext, ref sp);
                    SurfaceHelper.AdjustPeriodic(surface, ext, ref ep);
                    Line2D l2d = new Line2D(sp, ep);
                    cs = cs.Split(new Border(l2d));
                }
            }
            if (vs != null)
            {
                for (int i = 0; i < vs.Length; i++)
                {
                    GeoPoint2D sp = new GeoPoint2D(ext.Left, vs[i]);
                    GeoPoint2D ep = new GeoPoint2D(ext.Right, vs[i]);
                    SurfaceHelper.AdjustPeriodic(surface, ext, ref sp);
                    SurfaceHelper.AdjustPeriodic(surface, ext, ref ep);
                    Line2D l2d = new Line2D(sp, ep);
                    cs = cs.Split(new Border(l2d));
                }
            }
            if (cs.SimpleShapes.Length > 1)
            {
                if (us != null)
                {
                    for (int i = 0; i < us.Length; i++)
                    {
                        cs.AdjustVerticalLines(us[i]); // genau einrasten, damit null-Edges erzeugt werden
                    }
                }
                if (vs != null)
                {
                    for (int i = 0; i < vs.Length; i++)
                    {
                        cs.AdjustHorizontalLines(vs[i]);
                    }
                }
                for (int i = 0; i < cs.SimpleShapes.Length; i++)
                {
                    res.Add(Face.MakeFace(surface.Clone(), cs.SimpleShapes[i])); // surface muss gecloned werden, denn die wird u.U. geändert (invertiert)
                }
                return res;
            }
            return null; // gibt zwar Singularitäten, aber nicht innerhalb
        }

        internal List<Face> SplitSeam()
        {
            // Faces, die in sich geschlossen sind, also einen Saum, eine innere Kante haben, sollen hier aufgeteilt
            // werden, so dass die innere Kante verschwindet. Das ist hier ziemlich umständlich implementiert und wäre
            // vermutlich besser auf der reinen 2D Ebene zu machen, wenn die 2d Kurven nur ihre Edges kennen
            // würden. Die benachbarrten Faces müssen natürlich konsistent bleiben...

            //1427 in Program.cdb hängt hier
            if (!surface.IsUPeriodic && !surface.IsVPeriodic) return null;
            bool splitu;
            splitu = surface.IsUPeriodic; // kann aber beides sein
            Edge seamEdge = null;
            for (int i = 0; i < outline.Length; ++i)
            {
                if (outline[i].IsPeriodicEdge && !outline[i].IsSingular())
                {
                    seamEdge = outline[i];
                    break;
                }
            }
            if (seamEdge == null) return null;
            SimpleShape dbgss = this.Area; // ohne diesen Aufruf gehts manchmal schief
            BoundingRect ext = GetUVBounds();
            List<Face> res = new List<Face>();
            if (surface.IsVPeriodic && surface.IsUPeriodic)
            {
                GeoVector2D sdir = seamEdge.Curve2D(this).StartDirection;
                splitu = Math.Abs(sdir.y) > Math.Abs(sdir.x); // die Richtung des Saums geht nach v, also u splitten
            }
            GeoPoint2D sp, ep;
            double halfPeriod;
            if (!splitu)
            {
                double v = (ext.Bottom + ext.Top) / 2.0;
                sp = new GeoPoint2D(ext.Left, v);
                ep = new GeoPoint2D(ext.Right, v);
                halfPeriod = surface.VPeriod / 2.0;
                if (ext.Height < surface.VPeriod * 0.999) return null;
            }
            else
            {
                double u = (ext.Right + ext.Left) / 2.0;
                sp = new GeoPoint2D(u, ext.Bottom);
                ep = new GeoPoint2D(u, ext.Top);
                halfPeriod = surface.UPeriod / 2.0;
                if (ext.Width < surface.UPeriod * 0.999) return null;
            }
            Line2D intersectWith = new Line2D(sp, ep);
            Edge[] all = AllEdges;
            for (int i = 0; i < all.Length; ++i) all[i].Orient();
            SortedList<double, Vertex> splitPoints = new SortedList<double, Vertex>();
            // Alle Kanten werden an den Schnittpunkten mit der horizontalen oder vertikalen u/v Linie
            // aufgetrennt. Die betreffenden Kanten werden entfernt und neue Kanten werden eingeführt.
            // zusätzliche neue Kanten entstehen durch die Verbindungen der neuen Vertices.
#if DEBUG
            DebuggerContainer dc = new DebuggerContainer();
#endif
            ICurve2D[] used = new ICurve2D[all.Length];
            for (int i = 0; i < all.Length; ++i)
            {
                ICurve2D c2d = all[i].Curve2D(this, used);
                used[i] = c2d;
                GeoPoint2DWithParameter[] ips = c2d.Intersect(sp, ep);
                SortedList<double, Vertex> sortedVertices = new SortedList<double, Vertex>(ips.Length);

                for (int j = 0; j < ips.Length; ++j)
                {
                    if (all[i].Curve3D != null)
                    {
                        double pos = all[i].Curve3D.PositionOf(surface.PointAt(ips[j].p));
                        if (pos > 0.0 && pos < 1.0)
                        {
                            Vertex v = new Vertex(surface.PointAt(ips[j].p));
                            v.AddPositionOnFace(this, ips[j].p);
                            sortedVertices[pos] = v;
                            splitPoints[ips[j].par2] = v;
                        }
                    }
                }
                if (sortedVertices.Count > 0)
                {
                    Edge[] splitted = all[i].Split(sortedVertices, Precision.eps);
#if DEBUG
                    for (int j = 0; j < splitted.Length; ++j)
                    {
                        dc.Add(splitted[j].Curve2D(this), System.Drawing.Color.Blue, j);
                    }
#endif
                    // diese kante aus den vertices entfernen
                    all[i].Vertex1.RemoveEdge(all[i]);
                    all[i].Vertex2.RemoveEdge(all[i]);
                }
#if DEBUG
                else
                {
                    dc.Add(c2d, System.Drawing.Color.Yellow, i);
                }
#endif
            }
            // jetzt werden neue faces erzeugt, indem man einmal die neuen Edges nur vorwärts und einmal nur rückwärts benutzt
            // die neuen Kanten:
            Set<Edge> intersectionEdges = new Set<Edge>();
            // TODO: wenn splitPoints.Count nicht gerade ist, dann andere Position nehmen: 
            // whileschleife, die nicht abbricht oder so...
            for (int i = 0; i < splitPoints.Count - 1; i = i + 2)
            {
                Line2D l2d = intersectWith.Trim(splitPoints.Keys[i], splitPoints.Keys[i + 1]) as Line2D;
                ICurve c3d = surface.Make3dCurve(l2d);
                Edge spl = new Edge(this, c3d);
                spl.SetPrimary(this, l2d, true);
                spl.SetSecondary(this, l2d.CloneReverse(true), false);
                spl.MakeVertices(splitPoints.Values[i], splitPoints.Values[i + 1]);
                intersectionEdges.Add(spl);
#if DEBUG
                dc.Add(l2d, System.Drawing.Color.Turquoise, i);
#endif
            }
            foreach (Edge e in intersectionEdges)
            {
                e.Orient();
            }
            // wie werden die periodic edges verwendet? Hier erstmal alle sammeln
            Set<Edge> periodicEdges = new Set<Edge>();
            for (int i = 0; i < all.Length; ++i)
            {
                if (all[i].IsPeriodicEdge) periodicEdges.Add(all[i]);
            }
            // Im folgenden werden die intersection edges einmal vorwärts und einmal rückwärts verwendet.
            // Aber die Richtung der Saumkanten ist nicht eindeutig und wird durch forwardOnPrimaryFace
            // gegeben, obwohl beide faces gleich sind. Deshalb werden in der 1. Schleife alle periodic Edges
            // so umgedreht, dass sie für den 1. teil richtig sind. Für den 2. Teil werden dann alle amgedreht
            // so dass man in der 2. Schleife davon ausgehen kann, dass sie schon richtig sind
            Set<Edge> intersectionEdgesCopy = new Set<Edge>(intersectionEdges);
            while (intersectionEdges.Count > 0)
            {
                List<Edge> outline = new List<Edge>();
                Edge startWith = intersectionEdges.GetAny();
                intersectionEdges.Remove(startWith);
                outline.Add(startWith);
                Vertex stopAt = startWith.Vertex1;
                Vertex current = startWith.Vertex2;
                GeoPoint2D currentPosition = startWith.EndPosition(this);
                while (current != stopAt)
                {
                    Predicate<Edge> leftOfSplit = delegate (Edge e)
                    {   // liefert alle Kanten, die von current ausgehen
                        // if (outline.Contains(e)) return false;
                        if (intersectionEdgesCopy.Contains(e)) return e.Vertex1 == current; // intersection edge
                        else if (e.IsPeriodicEdge && !outline.Contains(e))
                        {
                            return (e.Vertex1 == current) || (e.Vertex2 == current); // wer dreht ihn dann um??
                        }
                        else if (e.StartVertex(this) == current)
                        {   // hier noch ausschließen, dass eine naht Kante in der richtigen Richtung
                            // betrachtet wird und dass kein Verbindungsweg über die Naht hinaus gewählt wird
                            return (e.StartPosition(this) | currentPosition) < halfPeriod;
                        }
                        return false;
                    };
                    List<Edge> edges = current.ConditionalEdges(leftOfSplit);
                    // es muss immer genau einen geben, Mehrdeutigkeiten sollte es nicht geben
                    // wenn doch, mit anderer Schnittposition versuchen
                    if (edges.Count == 1)
                    {
                        startWith = edges[0];
                        if (intersectionEdges.Contains(startWith))
                        {
                            current = startWith.Vertex2;
                            intersectionEdges.Remove(startWith);
                            currentPosition = startWith.EndPosition(this);
                        }
                        else
                        {
                            if (startWith.IsPeriodicEdge)
                            {   // die ist u.U. falschrum
                                if (startWith.StartVertex(this) == current)
                                {
                                    current = startWith.EndVertex(this);
                                    currentPosition = startWith.EndPosition(this);
                                }
                                else
                                {
                                    // diese Saum-Kante wird in ihrer zweiten und nicht in ihrer ersten Form verwendet
                                    startWith.ExchangeFaces();
                                    current = startWith.EndVertex(this);
                                    currentPosition = startWith.EndPosition(this);
                                }
                            }
                            else
                            {
                                current = startWith.EndVertex(this);
                                currentPosition = startWith.EndPosition(this);
                            }
                        }
                        outline.Add(startWith);
                    }
                    else break; // sollte nicht vorkommen
                }
                // TODO: Löcher berücksichtigen
                Face fc = this.Split(outline, null); // Löcher noch unberücksichtigt
                res.Add(fc); // das sind die links von der Schnittcurve
            }
            foreach (Edge e in periodicEdges)
            {
                e.ExchangeFaces(); // jetzt werden alle periodic edges so gedreht, das das primaryface relevant ist
                                   // damit sind sie für den 2. Durchlauf richtigrum
            }
            intersectionEdges.Clear(); // sollte eh leer sein, wenn nicht, exception
            intersectionEdges.AddMany(intersectionEdgesCopy); // leichter so zu schreiben
            while (intersectionEdges.Count > 0)
            {
                List<Edge> outline = new List<Edge>();
                Edge startWith = intersectionEdges.GetAny();
                intersectionEdges.Remove(startWith);
                outline.Add(startWith);
                Vertex stopAt = startWith.Vertex2; // jetzt rückwärts auf denselben
                Vertex current = startWith.Vertex1;
                GeoPoint2D currentPosition = startWith.EndPosition(this);
                while (current != stopAt)
                {
                    Predicate<Edge> leftOfSplit = delegate (Edge e)
                    {   // liefert alle Kanten, die von current ausgehen
                        if (intersectionEdgesCopy.Contains(e)) return e.Vertex2 == current; // die laufen rückwärts
                        if (e.IsPeriodicEdge) return e.EndVertex(this) == current;
                        if (e.StartVertex(this) == current)
                        {
                            return (e.StartPosition(this) | currentPosition) < halfPeriod;
                        }
                        return false;
                    };
                    List<Edge> edges = current.ConditionalEdges(leftOfSplit);
                    // es muss immer genau einen geben, Mehrdeutigkeiten sollte es nicht geben
                    // wenn doch, mit anderer Schnittposition versuchen
                    if (edges.Count == 1)
                    {
                        startWith = edges[0];
                        if (intersectionEdges.Contains(startWith))
                        {
                            current = startWith.Vertex1;
                            intersectionEdges.Remove(startWith);
                            currentPosition = startWith.StartPosition(this);
                        }
                        else
                        {
                            current = startWith.EndVertex(this);
                            currentPosition = startWith.EndPosition(this);
                        }
                        outline.Add(startWith);
                    }
                    else break; // sollte nicht vorkommen
                }
                Face fc = this.Split(outline, null); // Löcher noch unberücksichtigt
                res.Add(fc); // das sind die links von der Schnittcurve
            }
            return res;
        }

        internal void CombineConnectedSameSurfaceEdges()
        {
#if DEBUG
            if (2712 == hashCode)
            { }
#endif
            for (int i = 0; i < outline.Length; i++)
            {
                if (outline[i].SecondaryFace == null) continue;
                int j = i + 1;
                if (j >= outline.Length) j = 0;
                if (outline[j].SecondaryFace == null) continue;
                if (i == j) continue;
                if (((outline[i].PrimaryFace == outline[j].PrimaryFace) && (outline[i].SecondaryFace == outline[j].SecondaryFace)) ||
                    ((outline[i].SecondaryFace == outline[j].PrimaryFace) && (outline[i].PrimaryFace == outline[j].SecondaryFace)))
                {
                    if (combineEdges(outline[i], outline[j])) --i; // outline[j] will be removed, iteration must stay at i
                }
            }
            for (int k = 0; k < holes.Length; k++)
            {
                for (int i = 0; i < holes[k].Length; i++)
                {
                    int j = i + 1;
                    if (j >= holes[k].Length) j = 0;
                    if (i == j) continue;
                    if (((holes[k][i].PrimaryFace == holes[k][j].PrimaryFace) && (holes[k][i].SecondaryFace == holes[k][j].SecondaryFace)) ||
                        ((holes[k][i].SecondaryFace == holes[k][j].PrimaryFace) && (holes[k][i].PrimaryFace == holes[k][j].SecondaryFace)))
                    {
                        if (combineEdges(holes[k][i], holes[k][j])) --i; // holes[k][j] will be removed, iteration must stay at i
                    }
                }
            }
        }
        /// <summary>
        /// Combine two edges into a single edge, if they are connected and have the same pair of faces
        /// </summary>
        /// <param name="edg1"></param>
        /// <param name="edg2"></param>
        /// <returns></returns>
        internal bool combineEdges(Edge edg1, Edge edg2)
        {
            Face otherface = edg1.OtherFace(this);
            if (edg2.OtherFace(this) != otherface) return false; // the other face of both edges must be the same
            if (edg1.EndVertex(this) != edg2.StartVertex(this)) return false; // edg2 must be the follower of edg1
            if (!edg1.Forward(this)) edg1.ReverseCurve3D();
            if (!edg2.Forward(this)) edg2.ReverseCurve3D();
            // do single closed edges make problems? For parametric operations we would prefer them
            // not sure, how BRepOperation can deal with it
            // if ((edg1.EndVertex(this) == edg2.StartVertex(this)) && (edg1.StartVertex(this) == edg2.EndVertex(this))) return false; // do not create closed edges

            // now edg1 and edg2 are both forward oriented on this face and edg1 precedes edg2
            ICurve combined = Curves.Combine(edg1.Curve3D, edg2.Curve3D, Precision.eps);
            if (combined == null)
            {
                Vertex v1 = edg1.StartVertex(this);
                Vertex v2 = edg1.EndVertex(this);
                Vertex v3 = edg2.EndVertex(otherface);
                GeoVector n1t = Surface.GetNormal(v1.GetPositionOnFace(this));
                GeoVector n1o = otherface.Surface.GetNormal(v1.GetPositionOnFace(otherface));
                double n1 = (n1t * n1o) / (n1t.Length * n1o.Length);
                GeoVector n2t = Surface.GetNormal(v2.GetPositionOnFace(this));
                GeoVector n2o = otherface.Surface.GetNormal(v2.GetPositionOnFace(otherface));
                double n2 = (n2t * n2o) / (n2t.Length * n2o.Length);
                GeoVector n3t = Surface.GetNormal(v3.GetPositionOnFace(this));
                GeoVector n3o = otherface.Surface.GetNormal(v3.GetPositionOnFace(otherface));
                double n3 = (n3t * n3o) / (n3t.Length * n3o.Length);
                if (n1 < 0.9 && n2 < 0.9 && n3 < 0.9) // do not combine when close to tangential
                {
                    InterpolatedDualSurfaceCurve dsc = new InterpolatedDualSurfaceCurve(this.Surface, this.Domain, otherface.Surface, otherface.Domain,
                        new GeoPoint[] { edg1.StartVertex(this).Position, edg1.Curve3D.PointAt(0.5), edg1.EndVertex(this).Position, edg2.Curve3D.PointAt(0.5), edg2.EndVertex(this).Position });
                    combined = dsc;
                }
                else
                { // the two surfaces are almost tangential. InterpolatedDualSurfaceCurve is not good here 

                }
            }
            if (combined == null && (edg1.Curve3D is BSpline || edg2.Curve3D is BSpline)) return false; // BSplines need to be implemented in Curves.Combine, but make problems in the else case
            if (combined == null) return false; // there is a bug with a NURBS surface which has a sharp bend (in 1264_14_M_el.stp) when we try to connect a spline with a line
            if (combined != null)
            {
                ICurve2D c2dThis = this.surface.GetProjectedCurve(combined, Precision.eps);
                SurfaceHelper.AdjustPeriodic(this.surface, this.Area.GetExtent(), c2dThis);
                Edge edge;
                if (otherface != null)
                {
                    ICurve2D c2dOther = otherface.surface.GetProjectedCurve(combined, Precision.eps);
                    c2dOther.Reverse();

                    SurfaceHelper.AdjustPeriodic(otherface.surface, otherface.Area.GetExtent(), c2dOther);
                    edge = new Edge(this, combined, this, c2dThis, true, otherface, c2dOther, false);
                }
                else
                {
                    edge = new Edge(this, combined, this, c2dThis, true);
                }

                this.ReplaceCombinedEdges(edg1, edg2, edge);
                if (otherface != null) otherface.ReplaceCombinedEdges(edg1, edg2, edge);
                // edge.UseVertices(edg1.StartVertex(this), edg2.EndVertex(this)); makes precision problems
                edge.SetVertices(edg1.StartVertex(this), edg2.EndVertex(this));
                edge.Orient();
                edg1.Vertex1.RemoveEdge(edg1); // disconnect edg1 and edg2
                edg1.Vertex2.RemoveEdge(edg1);
                edg2.Vertex1.RemoveEdge(edg2);
                edg2.Vertex2.RemoveEdge(edg2);
                vertices = null;
                area = null;
                return true;
            }
            else if (otherface != null)
            {
                // There might be a orientation bug in the following
                // Test: are the surfaces tangential. Then we cannot combine the edges
                GeoPoint2D uv1 = edg1.EndVertex(this).GetPositionOnFace(this);
                GeoPoint2D uv2 = edg1.EndVertex(this).GetPositionOnFace(otherface);
                GeoPoint2D uvs1 = edg1.StartVertex(this).GetPositionOnFace(this);
                GeoPoint2D uve1 = edg2.EndVertex(this).GetPositionOnFace(this);
                GeoPoint2D uve2 = edg1.StartVertex(this).GetPositionOnFace(otherface);
                GeoPoint2D uvs2 = edg2.EndVertex(this).GetPositionOnFace(otherface);
                if (Precision.SameDirection(this.surface.GetNormal(uv1), otherface.surface.GetNormal(uv2), false)) return false;
                List<GeoPoint> points = new List<GeoPoint>(2);
                points.Add(edg1.StartVertex(this).Position);
                points.Add(edg2.EndVertex(this).Position);
                ICurve[] crvs3d;
                ICurve2D[] crvsOnSurface1;
                ICurve2D[] crvsOnSurface2;
                double[,] params3d;
                double[,] params2dsurf1;
                double[,] params2dsurf2;
                GeoPoint2D[] paramsuvsurf1;
                GeoPoint2D[] paramsuvsurf2;
                if (Surfaces.Intersect(this.Surface, this.Area.GetExtent(), otherface.Surface, otherface.Area.GetExtent(), points, out crvs3d, out crvsOnSurface1,
                    out crvsOnSurface2, out params3d, out params2dsurf1, out params2dsurf2, out paramsuvsurf1, out paramsuvsurf2, Precision.eps))
                {
                    if (crvs3d.Length == 1 && params3d[0, 0] != double.MinValue && params3d[0, 1] != double.MinValue)
                    {
                        bool forward = params3d[0, 0] < params3d[0, 1];
                        ICurve c3d = crvs3d[0];
                        ICurve2D c2dThis, c2dOther;
                        int j1;
                        if (forward)
                        {
                            c3d.Trim(params3d[0, 0], params3d[0, 1]);
                            c2dThis = crvsOnSurface1[0].Trim(params2dsurf1[0, 0], params2dsurf1[0, 1]);
                            c2dOther = crvsOnSurface2[0].Trim(params2dsurf2[0, 0], params2dsurf2[0, 1]);
                            j1 = 0;
                        }
                        else
                        {
                            c3d.Trim(params3d[0, 1], params3d[0, 0]);
                            c2dThis = crvsOnSurface1[0].Trim(params2dsurf1[0, 1], params2dsurf1[0, 0]);
                            c2dOther = crvsOnSurface2[0].Trim(params2dsurf2[0, 1], params2dsurf2[0, 0]);
                            j1 = 1;
                        }
                        forward = ((this.Surface.GetNormal(paramsuvsurf1[j1]) ^ otherface.Surface.GetNormal(paramsuvsurf2[j1])) * c3d.StartDirection) > 0;
                        //if (forward) c2dOther.Reverse();
                        //else c2dThis.Reverse();
                        if ((uvs1 | c2dThis.StartPoint) + (uve1 | c2dThis.EndPoint) > (uvs1 | c2dThis.EndPoint) + (uve1 | c2dThis.StartPoint)) c2dThis.Reverse();
                        if ((uvs2 | c2dOther.StartPoint) + (uve2 | c2dOther.EndPoint) > (uvs2 | c2dOther.EndPoint) + (uve2 | c2dOther.StartPoint)) c2dOther.Reverse();
                        SurfaceHelper.AdjustPeriodic(this.surface, this.Area.GetExtent(), c2dThis);
                        SurfaceHelper.AdjustPeriodic(otherface.surface, otherface.Area.GetExtent(), c2dOther);
                        if (c3d is InterpolatedDualSurfaceCurve)
                        {
                            c2dThis = (c3d as InterpolatedDualSurfaceCurve).CurveOnSurface1;
                            if (!forward) c2dThis.Reverse();
                            c2dOther = (c3d as InterpolatedDualSurfaceCurve).CurveOnSurface2;
                            if (forward) c2dOther.Reverse();
                        }
                        Edge edge = new Edge(this, c3d, this, c2dThis, forward, otherface, c2dOther, !forward);
                        this.ReplaceCombinedEdges(edg1, edg2, edge);
                        otherface.ReplaceCombinedEdges(edg1, edg2, edge);
                        // edge.UseVertices(edg1.StartVertex(this), edg2.EndVertex(this)); makes precision problems
                        if (forward) edge.SetVertices(edg1.StartVertex(this), edg2.EndVertex(this));
                        else edge.SetVertices(edg2.EndVertex(this), edg1.StartVertex(this));
                        edge.Orient();
                        edg1.Vertex1.RemoveEdge(edg1); // disconnect edg1 and edg2
                        edg1.Vertex2.RemoveEdge(edg1);
                        edg2.Vertex1.RemoveEdge(edg2);
                        edg2.Vertex2.RemoveEdge(edg2);
                        vertices = null;
                        area = null;
                        return true;
                    }
                }
            }
            return false;
        }

        private void ReplaceCombinedEdges(Edge edg1, Edge edg2, Edge replaceWith)
        {
            for (int i = 0; i < outline.Length; i++)
            {
                if (outline[i] == edg1)
                {
                    int ni = (i + 1);
                    if (ni >= outline.Length)
                    {
                        ni = 0;
                    }
                    if (outline[ni] != edg2)
                    {
                        ni = i - 1;
                        if (ni < 0) ni = outline.Length - 1;
                    }
                    if (outline[ni] != edg2) return; // edg1 und edg2 müssen zwei aufeinanderfolgende Kanten sein
                    if (i > ni)
                    {
                        int tmp = i;
                        i = ni;
                        ni = tmp;
                    }
                    List<Edge> newOutline = new List<Edge>(outline);
                    newOutline[i] = replaceWith;
                    newOutline.RemoveAt(ni);
                    outline = newOutline.ToArray();
                    area = null;
                    return; // fertig
                }
            }
            for (int j = 0; j < holes.Length; j++)
            {
                for (int i = 0; i < holes[j].Length; i++)
                {
                    if (holes[j][i] == edg1)
                    {
                        int ni = (i + 1);
                        if (ni >= holes[j].Length)
                        {
                            ni = 0;
                        }
                        if (holes[j][ni] != edg2)
                        {
                            ni = i - 1;
                            if (ni < 0) ni = holes[j].Length - 1;
                        }
                        if (holes[j][ni] != edg2) return; // edg1 und edg2 müssen zwei aufeinanderfolgende Kanten sein
                        if (i > ni)
                        {
                            int tmp = i;
                            i = ni;
                            ni = tmp;
                        }
                        // i immer kleiner als ni
                        List<Edge> newHole = new List<Edge>(holes[j]);
                        newHole[i] = replaceWith;
                        newHole.RemoveAt(ni);
                        holes[j] = newHole.ToArray();
                        area = null;
                        return; // fertig
                    }
                }
            }
        }

        internal void collateEdges(Edge toKeep, Edge collateWith, Face otherFace)
        {
            // toKeep: auf diesem Face, collateWith: auf anderem Face
            if (toKeep.SecondaryFace != null) throw new ApplicationException("Internal error in collateEdges: 'toKeep' is not open edge");
            // sind die beiden 3D Kurven gleich
            bool reverse;
            if (!Curves.SameGeometry(toKeep.Curve3D, collateWith.Curve3D, Precision.eps, out reverse))
            {
                throw new ApplicationException("Internal error in collateEdges: different curve3d");
            }
            if (reverse)
            {
                toKeep.SetSecondary(otherFace, collateWith.Curve2D(otherFace), !collateWith.Forward(otherFace));
            }
            else
            {
                toKeep.SetSecondary(otherFace, collateWith.Curve2D(otherFace), collateWith.Forward(otherFace));
            }
            otherFace.exchangeEdge(collateWith, toKeep);
        }

        internal void exchangeEdge(Edge oldOne, Edge newOne)
        {
            for (int i = 0; i < outline.Length; ++i)
            {
                if (outline[i] == oldOne)
                {
                    outline[i] = newOne;
                    return; // fertig
                }
            }
            for (int i = 0; i < holes.Length; i++)
            {
                for (int j = 0; j < holes[i].Length; ++j)
                {
                    if (holes[i][j] == oldOne)
                    {
                        holes[i][j] = newOne;
                        return; // fertig
                    }
                }
            }
        }
        /// <summary>
        /// Tries to use the provided edge if this face has a geometrical identical edge
        /// </summary>
        /// <param name="toUse"></param>
        /// <returns>true, if edge was replaced</returns>
        internal bool UseEdge(Edge toUse)
        {
            if (toUse.Curve3D == null) return false;
            GeoPoint startPoint = toUse.Curve3D.StartPoint;
            GeoPoint endPoint = toUse.Curve3D.EndPoint;
            foreach (Edge edg in AllEdgesIterated())
            {
                if ((edg.Vertex1.Position | startPoint) + (edg.Vertex2.Position | endPoint) < Precision.eps || (edg.Vertex2.Position | startPoint) + (edg.Vertex1.Position | endPoint) < Precision.eps)
                {
                    if (toUse.Curve3D.DistanceTo(edg.Curve3D.PointAt(0.5)) < Precision.eps)
                    {
                        ReplaceEdge(edg, toUse);
                        return true;
                    }
                }
            }
            return false;
        }
        internal void ReplaceEdge(Edge toReplace, Edge replaceWith)
        {
#if DEBUG
            System.Diagnostics.Debug.Assert(toReplace.SecondaryFace == null);
#endif
            bool fw = toReplace.Forward(this);
            bool sameDirection = false;
            if (replaceWith.Curve3D != null && toReplace.Curve3D != null)
            {
                //if ((replaceWith.Curve3D.StartPoint | toReplace.Curve3D.StartPoint) + (replaceWith.Curve3D.EndPoint | toReplace.Curve3D.EndPoint) >
                //    (replaceWith.Curve3D.StartPoint | toReplace.Curve3D.EndPoint) + (replaceWith.Curve3D.EndPoint | toReplace.Curve3D.StartPoint)) fw = !fw;
                // die beiden 3d Kurven, die eigentlich identisch sein müssen haben verschiedene Richtung. 
                // Punktvergleich geht schief bei geschlossenen Kurven, deshalb Richtungsvergleich in der Mitte
                if (replaceWith.Curve3D.DirectionAt(0.5) * toReplace.Curve3D.DirectionAt(0.5) < 0) fw = !fw;
                else sameDirection = true;
            }
            if (replaceWith.SecondaryFace == null || replaceWith.SecondaryFace == toReplace.PrimaryFace)
            {
                replaceWith.SetSecondary(toReplace.PrimaryFace, toReplace.PrimaryCurve2D, fw);
            }
            else
            if (replaceWith.PrimaryFace == toReplace.PrimaryFace)
                replaceWith.SetPrimary(toReplace.PrimaryFace, toReplace.PrimaryCurve2D, fw);
            if (sameDirection)
            {
                replaceWith.Vertex1.MergeWith(toReplace.Vertex1);
                replaceWith.Vertex2.MergeWith(toReplace.Vertex2);
            }
            else
            {
                replaceWith.Vertex1.MergeWith(toReplace.Vertex2);
                replaceWith.Vertex2.MergeWith(toReplace.Vertex1);
            }
            toReplace.Vertex1.RemoveEdge(toReplace);    // aus den Vertices entfernen, denn toreplace hat auch kein Face mehr
            toReplace.Vertex2.RemoveEdge(toReplace);
            vertices = null;
            replaceWith.UpdateInterpolatedDualSurfaceCurve();
            for (int i = 0; i < outline.Length; ++i)
            {
                if (outline[i] == toReplace)
                {
                    outline[i] = replaceWith;
                    return;
                }
            }
            for (int i = 0; i < holes.Length; ++i)
            {
                for (int j = 0; j < holes[i].Length; ++j)
                {
                    if (holes[i][j] == toReplace)
                    {
                        holes[i][j] = replaceWith;
                        return;
                    }
                }
            }
        }
        /// <summary>
        /// Replace an edge by an array of edges. If <paramref name="sortEdges"/> is false, the edges must be sorted correctly
        /// </summary>
        /// <param name="toReplace"></param>
        /// <param name="replaceBy"></param>
        /// <param name="sortEdges"></param>
        internal void ReplaceEdge(Edge toReplace, Edge[] replaceBy, bool sortEdges = false)
        {
            Vertex v1 = toReplace.StartVertex(this);
            Vertex v2 = toReplace.EndVertex(this);
            if (sortEdges)
            {   // edges may be in arbitrary order and may have no reference to this face. Also the vertices may be uninitialized.
                Vertex startHere = v1;
                for (int i0 = 0; i0 < replaceBy.Length; i0++)
                {
                    for (int i = i0; i < replaceBy.Length; i++)
                    {
                        if (replaceBy[i].StartVertex(this) == startHere)
                        {
                            if (i != i0)
                            {
                                Edge tmp = replaceBy[i];
                                replaceBy[i] = replaceBy[i0];
                                replaceBy[i0] = tmp;
                            }
                            startHere = replaceBy[i0].EndVertex(this);
                            break;
                        }
                    }
                }
            }
            if (sortEdges)
            {
                List<Edge> sortedEdges = new List<Edge>();
                Vertex startHere = v1;
                while (sortedEdges.Count < replaceBy.Length)
                {
                    bool found = false;
                    for (int i = 0; i < replaceBy.Length; i++)
                    {
                        if (replaceBy[i].StartVertex(this) == startHere)
                        {
                            sortedEdges.Add(replaceBy[i]);
                            startHere = replaceBy[i].EndVertex(this);
                            found = true;
                            break;
                        }
                    }
                    if (!found) break;
                }
                if (sortedEdges.Count == replaceBy.Length) replaceBy = sortedEdges.ToArray();
            }

            Vertex v3 = replaceBy[0].StartVertex(this);
            Vertex v4 = replaceBy[replaceBy.Length - 1].EndVertex(this);
            vertices = null; // invalidate cache 

            if (!toReplace.Forward(this) && !sortEdges)
            {
                Edge[] subst = replaceBy.Clone() as Edge[];
                Array.Reverse(subst);
                // this is needed by Shell.ReduceFaces. Not sure whether it is ok in this way in all other cases
                if (subst[0].StartVertex(this) != toReplace.StartVertex(this))
                {
                    if (subst[0].StartVertex(this) != toReplace.EndVertex(this))
                    {
                        throw new ApplicationException("Error on orientation of edge");
                    }
                    for (int i = 0; i < subst.Length; i++)
                    {
                        subst[i].Reverse(this);
                    }
                }
                replaceBy = subst;
                v3 = subst[0].StartVertex(this);
                v4 = subst[replaceBy.Length - 1].EndVertex(this);
            }
            for (int i = 0; i < outline.Length; ++i)
            {
                if (outline[i] == toReplace)
                {
                    List<Edge> subst = new List<Edge>(outline);
                    subst.RemoveAt(i);
                    subst.InsertRange(i, replaceBy);
                    outline = subst.ToArray();
                    area = null;
#if DEBUG
                    CheckConsistency();
#endif
                    return;
                }
            }
            for (int i = 0; i < holes.Length; ++i)
            {
                for (int j = 0; j < holes[i].Length; ++j)
                {
                    if (holes[i][j] == toReplace)
                    {
                        List<Edge> subst = new List<Edge>(holes[i]);
                        subst.RemoveAt(j);
                        subst.InsertRange(j, replaceBy);
                        holes[i] = subst.ToArray();
                        area = null;
#if DEBUG
                        CheckConsistency();
#endif
                        return;
                    }
                }
            }
        }

        internal bool SubstituteEdge(Edge oldEdge, Edge newEdge)
        {
            for (int i = 0; i < outline.Length; i++)
            {
                if (outline[i] == oldEdge)
                {
                    outline[i] = newEdge;
                    return true;
                }
            }
            if (holes != null)
            {
                for (int j = 0; j < holes.Length; j++)
                {
                    for (int i = 0; i < holes[j].Length; i++)
                    {
                        if (holes[j][i] == oldEdge)
                        {
                            holes[j][i] = newEdge;
                            return true;
                        }
                    }
                }
            }
            return false;
        }
        internal Face[] SplitAndReplace(SimpleShape ss)
        {   // ss soll von diesem Face abgezogen werden. 
            // Es entstehen neue Faces, die das bestehende ersetzen sollen
            // vor allem die Kanten müssen gehandhabt werden
            // ss kann ganz innerhalb liegen oder auch gemeinsame Außenkanten haben
            List<Face> res = new List<Face>();
            CompoundShape s1 = SimpleShape.Subtract(this.Area, ss);
            Face[] newFaces = new Face[s1.SimpleShapes.Length];
            for (int i = 0; i < newFaces.Length; i++)
            {
                newFaces[i] = Face.MakeFace(Surface, s1.SimpleShapes[i]);
                newFaces[i].orientedOutward = this.orientedOutward;
            }
            Face hole = Face.MakeFace(Surface, ss);
            hole.orientedOutward = this.orientedOutward;
            Shell shell = this.Owner as Shell;
            if (shell != null)
            {
                shell.RemoveFace(this);
                for (int i = 0; i < newFaces.Length; i++)
                {
                    shell.AddFace(newFaces[i]);
                }
                shell.AddFace(hole);
                shell.AssertOutwardOrientation();
            }
            res.AddRange(newFaces);
            res.Add(hole);
            return res.ToArray();
        }
        /// <summary>
        /// Creates a new face representing a tringle in space
        /// </summary>
        /// <param name="p1"></param>
        /// <param name="p2"></param>
        /// <param name="p3"></param>
        /// <returns></returns>
        public static Face MakeFace(GeoPoint p1, GeoPoint p2, GeoPoint p3)
        {
            Plane pln = new Plane(p1, p2 - p1, p3 - p1);
            GeoPoint2D p11 = pln.Project(p1);
            GeoPoint2D p22 = pln.Project(p2);
            GeoPoint2D p33 = pln.Project(p3);
            Border bdr = new Border(new GeoPoint2D[] { p11, p22, p33 });
            SimpleShape ss = new SimpleShape(bdr);
            return Face.MakeFace(new PlaneSurface(pln), ss);
        }
        internal static Face MakeFace(GeoPoint p1, GeoPoint p2, GeoPoint p3, GeoPoint p4)
        {
            Plane pln = new Plane(p1, p2 - p1, p3 - p1);
            GeoPoint2D p11 = pln.Project(p1);
            GeoPoint2D p22 = pln.Project(p2);
            GeoPoint2D p33 = pln.Project(p3);
            GeoPoint2D p44 = pln.Project(p4);
            Border bdr = new Border(new GeoPoint2D[] { p11, p22, p33, p44 });
            SimpleShape ss = new SimpleShape(bdr);
            return Face.MakeFace(new PlaneSurface(pln), ss);
        }

        internal void CheckEdges()
        {   // wenn das face nicht zu einer Shell gehört, dann  muss es die kanten alleine besitzen
            if (!(Owner is Shell))
            {
                for (int i = 0; i < outline.Length; ++i)
                {
                    if (outline[i].SecondaryFace != null && outline[i].PrimaryFace != outline[i].SecondaryFace)
                    {
                        ICurve curve = null;
                        if (outline[i].Curve3D != null) curve = outline[i].Curve3D.Clone();
                        outline[i] = new Edge(this, curve, this, outline[i].Curve2D(this).Clone(), outline[i].Forward(this));
                    }
                }
                for (int j = 0; j < holes.Length; ++j)
                {
                    for (int i = 0; i < holes[j].Length; ++i)
                    {
                        if (holes[j][i].SecondaryFace != null && holes[j][i].PrimaryFace != holes[j][i].SecondaryFace)
                        {
                            holes[j][i] = new Edge(this, holes[j][i].Curve3D.Clone(), this, holes[j][i].Curve2D(this).Clone(), holes[j][i].Forward(this));
                        }
                    }
                }
            }
        }
        internal bool Check2DBounds()
        {
            return Area.CheckTopology();
        }

#if DEBUG
        public void Debug(ICurve intersectWith)
        {   // zum DEbuggen, kann man immer wieder ändern
        }

        public string neighbours
        {
            get
            {
                Set<int> n = new Set<int>();
                for (int i = 0; i < outline.Length; i++)
                {
                    if (outline[i].PrimaryFace != this) n.Add(outline[i].PrimaryFace.hashCode);
                    if (outline[i].SecondaryFace != null && outline[i].SecondaryFace != this) n.Add(outline[i].SecondaryFace.hashCode);
                }
                StringBuilder sb = new StringBuilder();
                foreach (int i in n)
                {
                    if (sb.Length > 0) sb.Append(", ");
                    sb.Append(i.ToString());
                }
                return sb.ToString();
            }
        }
#endif
        internal void ForceTriangulation(double precision)
        {
            trianglePoint = null;
            if (precision < 0) precision = trianglePrecision;
            AssureTriangles(precision);
        }

        internal static Edge[] GetCommonEdges(Face f1, Face f2)
        {
            List<Edge> res = new List<Edge>();
            Edge[] all = f1.AllEdges;
            for (int i = 0; i < all.Length; i++)
            {
                if (all[i].OtherFace(f1) == f2) res.Add(all[i]);
            }
            return res.ToArray();
        }
        /// <summary>
        /// Combine this face with the provided other face. The faces must have geometrical identical surfaces.
        /// </summary>
        /// <param name="other">the other face</param>
        /// <param name="toOtherSurface">the ModOp2D, which transforms the 2d system of this surface to the other surface when appropriate, otherwise ModOp2D.Null</param>
        /// <returns>the removed edges</returns>
        internal Set<Edge> CombineWith(Face other, ModOp2D toOtherSurface)
        {
            this.vertices = null;
            other.vertices = null;
            Vertex[] dbg1 = this.Vertices;
            Vertex[] dbg2 = other.Vertices;

            Set<Edge> onThis = new Set<Edge>(AllEdgesIterated());
            Set<Edge> onOther = new Set<Edge>(other.AllEdgesIterated());
            Set<Edge> usableEdges = onThis.SymmetricDifference(onOther); // all edges of the resulting face, which belong to one of the faces but not to both
            Set<Edge> commonEdges = onThis.Intersection(onOther); // these will be removed
            List<List<Edge>> loops = new List<List<Edge>>(); // the loops, one of them is the outline, the others are holes
            if (this.surface is IRestrictedDomain rd)
            {
                List<ICurve> curves = new List<ICurve>();
                foreach (Edge edg in usableEdges)
                {
                    curves.Add(edg.Curve3D);
                }
                surface = rd.AdaptToCurves(curves); // makes a new surface, which can handle the edges of both faces (e.g. for CylindricalSurfaceNP)
                foreach (Edge edg in onThis)
                {   // adapt the 2d curves on this (new) surface
                    if (!commonEdges.Contains(edg))
                    {
                        if (edg.PrimaryFace == this)
                        {
                            edg.PrimaryCurve2D = surface.GetProjectedCurve(edg.Curve3D, 0.0);
                            if (!edg.Forward(this)) edg.PrimaryCurve2D.Reverse();
                        }
                        else
                        {
                            edg.SecondaryCurve2D = surface.GetProjectedCurve(edg.Curve3D, 0.0);
                            if (!edg.Forward(this)) edg.SecondaryCurve2D.Reverse();
                        }
                    }
                }
            }
            while (usableEdges.Count > 0)
            {
                Edge startWith = usableEdges.GetAny();
                Vertex startVertex;
                if (startWith.PrimaryFace == this || startWith.SecondaryFace == this) startVertex = startWith.StartVertex(this);
                else startVertex = startWith.StartVertex(other);
                List<Edge> loop = new List<Edge>();
                while (true)
                {
                    loop.Add(startWith);
                    usableEdges.Remove(startWith);
                    Face onFace;
                    if (startWith.PrimaryFace == this || startWith.SecondaryFace == this) onFace = this;
                    else onFace = other;
                    // onFace is unique, we only use edges which are not on both faces here
                    Vertex endVertex = startWith.EndVertex(onFace);
                    if (endVertex == startVertex) break; // done, the loop is closed
                    Edge next = null;
                    // how to proceed? this is maybe not well defined when four edges meet in a single vertex. In this case we choose an arbitrary path and get ether a single hole
                    // which touches itself in a vertex or two holes with a common vertex. Both should be ok
                    foreach (Edge e in endVertex.AllEdges)
                    {
                        if (usableEdges.Contains(e))
                        {   // use only the unused edges to avoid infinite loops
                            if (e.PrimaryFace == this || e.SecondaryFace == this)
                            {
                                if (e.StartVertex(this) == endVertex)
                                {
                                    next = e;
                                    break;
                                }
                            }
                            else
                            {
                                if (e.StartVertex(other) == endVertex)
                                {
                                    next = e;
                                    break;
                                }
                            }
                        }
                    }
                    if (next == null) return new Set<CADability.Edge>(); // should never happen
                    startWith = next;
                }
                loops.Add(loop);
            }
            double maxArea = 0;
            int outerLoop = -1;
            ModOp2D toThisSurface = ModOp2D.Null;
            if (!toOtherSurface.IsNull) toThisSurface = toOtherSurface.GetInverse();
            for (int i = 0; i < loops.Count; i++)
            {
                List<ICurve2D> segments = new List<ICurve2D>();
                for (int j = 0; j < loops[i].Count; j++)
                {
                    Edge edg = loops[i][j];
                    if (edg.PrimaryFace == other || edg.SecondaryFace == other)
                    {
                        edg.ReplaceFace(other, this, toThisSurface);
                    }
                    segments.Add(edg.Curve2D(this).Clone());
                }
                Border bdr = Border.FromOrientedList(segments.ToArray(), true);
                bdr.forceConnect(true);
                double a = bdr.Area;
                if (a > maxArea)
                {
                    maxArea = a;
                    outerLoop = i;
                }
            }
            if (outerLoop < 0) return new Set<CADability.Edge>(); // sollte nicht vorkommen
            outline = loops[outerLoop].ToArray();
            List<Edge[]> lholes = new List<Edge[]>();
            for (int i = 0; i < loops.Count; i++)
            {
                if (i != outerLoop) lholes.Add(loops[i].ToArray());
            }
            holes = lholes.ToArray();
            InvalidateArea();
            this.vertices = null;
            this.extent = BoundingCube.EmptyBoundingCube;
            // this.ForceTriangulation(trianglePrecision);
            trianglePoint = null;
            SimpleShape ss = Area;
            return commonEdges;
        }

        private void MakeAreaFromSortedEdges(double uPeriod, double vPeriod)
        {
            List<ICurve2D> ls = new List<ICurve2D>();
            ICurve2D[] segments = new ICurve2D[outline.Length];
            for (int i = 0; i < outline.Length; ++i)
            {
                segments[i] = outline[i].Curve2D(this, segments);
                if (segments[i] != null) ls.Add(segments[i]);
            }
            segments = ls.ToArray();
#if DEBUG
            DebuggerContainer dc = new DebuggerContainer();
            dc.Add(segments);
#endif

            Border boutline = null;
            if (boutline == null)
            {
                boutline = Border.FromOrientedList(segments); // 1. Versuch: schon richtig orientiert
                if (boutline == null) boutline = Border.FromOrientedListPeriodic(segments, uPeriod, vPeriod); // 2. Versuch: mit periodischem Versatz
                if (outline.Length > 1)
                {
                    if (boutline.Segments[0] == segments[segments.Length - 1])
                    {
                        Array.Reverse(outline);
                        Set<Edge> exchanged = new Set<Edge>();
                        // ganz vertrackt: bei periodic edges wird immer eine bestimmte 2D Curve zuerst geliefert
                        // auch das muss umgedreht werden.
                        for (int i = 0; i < outline.Length; ++i)
                        {
                            if (outline[i].IsPeriodicEdge && !exchanged.Contains(outline[i]))
                            {
                                outline[i].ExchangeFaces();
                                exchanged.Add(outline[i]); // nur einmal umdrehen
                            }
                        }
                    }
                }
            }
            List<Border> prholes = new List<Border>();
            for (int i = 0; i < holes.Length; ++i)
            {
                ICurve2D[] holecurves = new ICurve2D[holes[i].Length];
                for (int j = 0; j < holes[i].Length; j++)
                {
                    holecurves[j] = holes[i][j].Curve2D(this, holecurves);
                }
                Border hole = null;
                if (hole == null)
                {
                    hole = Border.FromOrientedList(holecurves); // 1. Versuch: schon richtig verbunden
                    if (hole == null) hole = Border.FromOrientedListPeriodic(holecurves, uPeriod, vPeriod); // 2. Versuch: periodischer Versatz
                }
                if (hole != null)
                {
                    if (hole.Segments[0] != holecurves[0]) Array.Reverse(holes[i]); // wenn FromUnorientedList die Reihenfolge umgedreht hat, dann müssen wir auch umdrehen
                    prholes.Add(hole);
                }
            }
            area = new SimpleShape(boutline, prholes.ToArray()).Clone();
            // Die Methode FromUnorientedList hat nun alle segmente linksrum orientiert.
            // wir wollen aber die Löcher rechtsrum haben und drehen jetzt die 2d Kurven der Löcher um
            // area ist ja ein Clone und wird dadurch nicht verändert
            for (int i = 0; i < holes.Length; ++i)
            {
                ICurve2D[] holecurves = new ICurve2D[holes[i].Length];
                // holecurves wird noch benötigt wg. Saumkanten
                for (int j = 0; j < holes[i].Length; j++)
                {
                    holecurves[j] = holes[i][j].Curve2D(this, holecurves);
                    holecurves[j].Reverse();
                }
                Array.Reverse(holes[i]);
            }
            surface.SetBounds(boutline.Extent);

        }

        internal Edge GetNextEdge(Edge edge)
        {
            Vertex v = edge.EndVertex(this);
            if (v == null) return null;
            List<Edge> onThisFace = v.EdgesOnFace(this);
            if (onThisFace.Count < 2) return edge; // there is only one (closed) edge. This should not be the case with proper (non periodic) faces
            if (onThisFace.Count > 2)
            {   // this could happen when a vertex is multiply used, but this is very rare. Another situation might be that during brep operations
                // there are additional edges created (intersection edges) which are assiciated with this face (as primary or secondary face) but 
                // do not belong to the outline or holes of this face. Such edges are dismissed here
                int lasti = outline.Length - 1;
                for (int i = 0; i < outline.Length; i++)
                {
                    if (edge == outline[lasti]) return outline[i];
                    lasti = i;
                }
                for (int j = 0; j < holes.Length; j++)
                {
                    lasti = holes[j].Length - 1;
                    for (int i = 0; i < holes[j].Length; i++)
                    {
                        if (edge == holes[j][lasti]) return holes[j][i];
                        lasti = i;
                    }
                }
                return null;
            }
            // normal case: two edges for vertex v on this face. We return the other edge
            if (onThisFace[0] == edge) return onThisFace[1];
            else return onThisFace[0];
        }

        private bool isOuterEdge(Edge edge)
        {
            for (int i = 0; i < outline.Length; i++)
            {
                if (edge == outline[i]) return true;
            }
            return false;
        }

        internal bool CheckConsistency()
        {   // Konsistenzcheck
            for (int i = 0; i < outline.Length; i++)
            {
                int next = i + 1;
                if (next >= outline.Length) next = 0;
                if (GetNextEdge(outline[i]) != outline[next])
                    return false;
            }
            for (int i = 0; i < holes.Length; i++)
            {
                for (int j = 0; j < holes[i].Length; j++)
                {
                    int next = j + 1;
                    if (next >= holes[i].Length) next = 0;
                    if (GetNextEdge(holes[i][j]) != holes[i][next]) return false;
                }
            }
            foreach (Edge edg in Edges)
            {
                if (edg.PrimaryFace != this && edg.SecondaryFace != this) return false;
            }
            // sind die 2d Kurven richtig orientiert?
            foreach (Edge edg in Edges)
            {
                // die Richtung der 2d Kurve ist so, dass auf der rechten Seite das Innere liegt
                ICurve2D c2d = edg.Curve2D(this);
                GeoPoint sp, ep;
                sp = surface.PointAt(c2d.StartPoint);
                ep = surface.PointAt(c2d.EndPoint);
                if ((sp | edg.StartVertex(this).Position) > 1e-5)
                {
                    // return false;
                }
                if ((ep | edg.EndVertex(this).Position) > 1e-5)
                {
                    // return false;
                }

                GeoPoint loc;
                GeoVector diru, dirv;
                surface.DerivationAt(c2d.StartPoint, out loc, out diru, out dirv);
                GeoVector normal = diru ^ dirv;
                if (normal.Length > Precision.eps)
                {
                    ModOp fromUnitPlane = new ModOp(diru, dirv, normal, loc);
                    ModOp toUnitPlane = fromUnitPlane.GetInverse();
                    GeoVector forward = fromUnitPlane * new GeoVector(c2d.StartDirection);
                    GeoVector toRight = fromUnitPlane * new GeoVector(c2d.StartDirection.ToRight());
                    double d = normal * (toRight ^ forward);
                    if (d < 0) return false;
                    GeoVector forward3d;
                    if (edg.Curve3D != null)
                    {
                        if (edg.Forward(this))
                        {
                            forward3d = edg.Curve3D.StartDirection;
                        }
                        else
                        {
                            forward3d = -edg.Curve3D.EndDirection;
                        }
                        //edg.Orient();
                        d = forward3d * forward;
                    }
                }
                //if (d < 0) return false;
            }
            return true;
        }

        public bool IsConnectedWith(Face fc1)
        {
            foreach (Edge edge in AllEdgesIterated())
            {
                if (edge.PrimaryFace == this && edge.SecondaryFace == fc1) return true;
                if (edge.SecondaryFace == this && edge.PrimaryFace == fc1) return true;
            }
            return false;
        }
        /// <summary>
        /// Returns true, if this face can be considered as a fillet between two other faces
        /// </summary>
        /// <returns></returns>
        public bool IsFillet()
        {
            if (Surface is ISurfaceOfArcExtrusion sae)
            {
                HashSet<Face> tangentialFaces = new HashSet<Face>();
                for (int i = 0; i < outline.Length; i++)
                {
                    if (outline[i].IsTangentialEdge())
                    {
                        if (outline[i].Curve2D(this).DirectionAt(0.5).IsMoreHorizontal != sae.ExtrusionDirectionIsV)
                            tangentialFaces.Add(outline[i].OtherFace(this)); // OK, this is not necessarily the case
                    }
                }
                if (tangentialFaces.Count == 2) return true;
            }
            else if (Surface is ICylinder cy)
            {
                HashSet<Face> tangentialFaces = new HashSet<Face>();
                for (int i = 0; i < outline.Length; i++)
                {
                    if (outline[i].IsTangentialEdge())
                    {
                        if (Precision.SameDirection(outline[i].Curve3D.DirectionAt(0.5), cy.Axis.Direction, false))
                            tangentialFaces.Add(outline[i].OtherFace(this));
                    }
                }
                if (tangentialFaces.Count == 2) return true;
            }
            else if (Surface is SphericalSurface sph)
            {
                HashSet<Face> tangentialFaces = new HashSet<Face>();
                for (int i = 0; i < outline.Length; i++)
                {
                    if (outline[i].IsTangentialEdge())
                    {
                        if (outline[i].OtherFace(this).Surface is ISurfaceOfArcExtrusion && outline[i].OtherFace(this).IsFillet())
                            tangentialFaces.Add(outline[i].OtherFace(this));
                    }
                }
                // a spherical face can be the connection of three or more fillets
                if (tangentialFaces.Count > 2) return true;
            }
            return false;
        }
        public bool IsClosedSurface()
        {
            if (Surface is ISurfaceOfArcExtrusion sae)
            {
                for (int i = 0; i < outline.Length; i++)
                {
                    if (outline[i].IsTangentialEdge())
                    {
                        Face otherFace = outline[i].OtherFace(this);
                        ModOp2D fts;
                        if (surface.SameGeometry(Area.GetExtent(), otherFace.Surface, otherFace.Area.GetExtent(), Precision.eps, out fts))
                        {
                            HashSet<Edge> edges = new HashSet<Edge>(otherFace.OutlineEdges);
                            edges.IntersectWith(OutlineEdges);
                            if (edges.Count >= 2) return true; // OK, this test is not perfect, we would have to unite the two domains and see whether it is a full period
                        }
                    }
                }
            }
            return false;
        }
        /// <summary>
        /// Returns all other directly connected faces which have a common ISurfaceOfArcExtrusion surface
        /// </summary>
        /// <returns></returns>
        public HashSet<Face> GetCylindricalConnected()
        {
            HashSet<Face> res = new HashSet<Face>();
            if (Surface is ISurfaceOfArcExtrusion sae)
            {
                for (int i = 0; i < outline.Length; i++)
                {
                    if (outline[i].IsTangentialEdge())
                    {
                        Face otherFace = outline[i].OtherFace(this);
                        ModOp2D fts;
                        if (surface.SameGeometry(Area.GetExtent(), otherFace.Surface, otherFace.Area.GetExtent(), Precision.eps, out fts))
                        {
                            HashSet<Edge> edges = new HashSet<Edge>(otherFace.OutlineEdges);
                            edges.IntersectWith(OutlineEdges);
                            if (edges.Count >= 2) res.Add(otherFace);
                        }
                    }
                }
            }
            return res;
        }
        internal bool RepairFromOcasEdges()
        {
            // outline und holes sind schon richtig spezifiziert, vorausgesetzt, die 2d Kurven haben einigermaßen die richtige Geometrie
            // andererseits kommen wir hierher, wenn CheckOutlineDirection nicht richtig gearbeitet hat.
#if DEBUG
#endif
#if DEBUG
#endif
            return false;
        }
        /// <summary>
        /// Returns a face, that has the provided offset to this face. When dist>0 the offset will be to the outside, when dist&lt0, the offset will be to the inside
        /// Self intersection will not be checked. 
        /// </summary>
        /// <param name="dist"></param>
        /// <returns></returns>
        public Face GetOffset(double dist, Dictionary<Vertex, Vertex> vertices)
        {
            // das kopieren der 2d Kurven in diese doppelte Liste muss gemacht werden, um die Reihenfolge beizubehalten
            List<List<ICurve2D>> outlineAndHoles = new List<List<ICurve2D>>();
            List<ICurve2D> crvo = new List<ICurve2D>();
            for (int i = 0; i < outline.Length; i++)
            {
                crvo.Add(outline[i].Curve2D(this));
            }
            outlineAndHoles.Add(crvo);
            for (int i = 0; i < holes.Length; i++)
            {
                List<ICurve2D> crvh = new List<ICurve2D>();
                for (int j = 0; j < holes[i].Length; j++)
                {
                    crvh.Add(holes[i][j].Curve2D(this));
                }
                outlineAndHoles.Add(crvh);
            }
            ISurface offsetSurface;
            ModOp2D mod = ModOp2D.Null;
            if (surface is ConicalSurface) // da gibts vielleicht noch andere Fälle?
            {
                offsetSurface = (surface as ConicalSurface).GetOffsetSurface(dist, out mod);
            }
            else
            {
                offsetSurface = surface.GetOffsetSurface(dist);
            }
            if (offsetSurface == null) return null;
            if (!mod.IsNull)
            {
                for (int i = 0; i < outlineAndHoles.Count; i++)
                {
                    for (int j = 0; j < outlineAndHoles[i].Count; j++)
                    {
                        outlineAndHoles[i][j] = outlineAndHoles[i][j].GetModified(mod);
                    }
                }
            }
            Face res = MakeFace(offsetSurface, outlineAndHoles);
            Edge[] oldEdges = AllEdges;
            Edge[] newEdges = res.AllEdges; // die beiden Arrays müssen synchron sein!
#if DEBUG
            System.Diagnostics.Debug.Assert(oldEdges.Length == newEdges.Length);
            DebuggerContainer dc = new DebuggerContainer();
            foreach (Vertex vtx in Vertices)
            {
                GeoPoint2D uv = vtx.GetPositionOnFace(this);
                GeoPoint p0 = surface.PointAt(uv);
                GeoVector d0 = surface.GetNormal(uv);
                GeoPoint p1 = p0 + dist * d0.Normalized;
                Line l = Line.TwoPoints(p0, p1);
                dc.Add(l);
            }
            dc.Add(this);
            dc.Add(res);
#endif
            for (int i = 0; i < oldEdges.Length; i++)
            {
                Vertex v1 = vertices[oldEdges[i].Vertex1];
                Vertex v2 = vertices[oldEdges[i].Vertex2];
                if ((v1.Position | newEdges[i].Vertex1.Position) < (v1.Position | newEdges[i].Vertex2.Position))
                {   // gleiche Orientierung
                    newEdges[i].ReplaceVertex(newEdges[i].Vertex1, v1);
                    newEdges[i].ReplaceVertex(newEdges[i].Vertex2, v2);
                }
                else
                {   // entgegengesetzt
                    newEdges[i].ReplaceVertex(newEdges[i].Vertex1, v2);
                    newEdges[i].ReplaceVertex(newEdges[i].Vertex2, v1);
                }
            }
            for (int i = 0; i < newEdges.Length; i++) newEdges[i].Orient();

            return res;
        }
        static public bool SameForm(Face[] faces1, Face[] faces2, double precision, out ModOp translation)
        {
            if (faces1.Length != faces2.Length)
            {
                translation = ModOp.Identity;
                return false;
            }
            BoundingCube ext1 = BoundingCube.EmptyBoundingCube;
            BoundingCube ext2 = BoundingCube.EmptyBoundingCube;
            for (int i = 0; i < faces1.Length; i++)
            {
                ext1.MinMax(faces1[i].GetExtent(precision / 2.0));
                ext2.MinMax(faces2[i].GetExtent(precision / 2.0));
            }
            translation = ModOp.Translate(ext1.GetCenter() - ext2.GetCenter()); // sh2 wird verschoben und passt damit auf sh1
            if (Math.Abs(ext1.XDiff - ext2.XDiff) > precision) return false;
            if (Math.Abs(ext1.YDiff - ext2.YDiff) > precision) return false;
            if (Math.Abs(ext1.ZDiff - ext2.ZDiff) > precision) return false;

            for (int i = 0; i < faces2.Length; i++)
            {
                faces2[i].Modify(translation);
            }
            foreach (Face fc1 in faces1)
            {
                fc1.InvalidateArea();
                BoundingRect br1 = fc1.Area.GetExtent();
                CompoundShape.Signature sig1 = fc1.Area.CalculateSignature();
                bool matched = false;
                foreach (Face fc2 in faces2)
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
        internal bool SameForm(Face other, BoundingRect areaExt, CompoundShape.Signature signature, double precision)
        {
            ModOp2D firstToSecond;
            if (other.Surface.SameGeometry(other.Area.GetExtent(), this.Surface, areaExt, precision, out firstToSecond))
            {
                // die gleiche Fläche, aber möglicherweise verschiedene uv-Systeme
                SimpleShape fc2m = other.Area.GetModified(firstToSecond);
                ModOp2D xTo1;
                if (fc2m.isCongruent(this.Area, signature, out xTo1, precision))
                {
                    if (Math.Abs(xTo1.Factor - 1.0) < 1e-6)
                    {
                        return true;
                    }
                }
            }
            return false;
        }
        /// <summary>
        /// Connect two faces by geometrically identical edges. Return true if at least one pair of edges has been combined (to a single edge).
        /// Doesn't combine singular edges.
        /// </summary>
        /// <param name="face1"></param>
        /// <param name="face2"></param>
        /// <param name="precision"></param>
        /// <returns></returns>
        public static bool ConnectTwoFaces(Face face1, Face face2, double precision)
        {
            // find vertex pairs (one from each face) which have the same position
            Dictionary<Vertex, Vertex> vertexPairs = new Dictionary<Vertex, Vertex>();
            foreach (Vertex v1 in face1.Vertices)
            {
                foreach (Vertex v2 in face2.Vertices)
                {
                    if ((v1.Position | v2.Position) < precision)
                    {
                        vertexPairs[v1] = v2;
                        break;
                    }
                }
            }
            // collect pairs of edge, which are geomatrically identical
            // we collect the edges rather than combining them immediately, because we are iterating over the edges
            List<Pair<Edge, Edge>> edgePairs = new List<Pair<Edge, Edge>>();
            foreach (Edge edg in face1.AllEdgesIterated())
            {
                if (vertexPairs.ContainsKey(edg.Vertex1) && vertexPairs.ContainsKey(edg.Vertex2))
                {
                    List<Edge> other = vertexPairs[edg.Vertex1].ConditionalEdges(delegate (Edge e)
                    {
                        return (e.Vertex1 == vertexPairs[edg.Vertex2] || e.Vertex2 == vertexPairs[edg.Vertex2]);
                    });
                    for (int i = 0; i < other.Count; i++)
                    {
                        // edg and other[i] connect vertices with the same position
                        if (edg.Curve3D != null && other[i].Curve3D != null && other[i].PrimaryFace == face2) // only check primary, because secondary should be null
                        {
                            if (edg.Curve3D.DistanceTo(other[i].Curve3D.PointAt(0.5)) < precision)
                            {   // ther middle points also have the same position
                                edgePairs.Add(new Pair<Edge, Edge>(edg, other[i]));
                            }
                        }
                    }
                }
            }
            for (int i = 0; i < edgePairs.Count; i++)
            {
                face1.ReplaceEdge(edgePairs[i].First, edgePairs[i].Second);
            }
            return edgePairs.Count > 0;

        }

        /// <summary>
        /// Gets the Surface.PositionOf(p) in the correct periodic domain
        /// </summary>
        /// <param name="p"></param>
        /// <returns></returns>
        public GeoPoint2D PositionOf(GeoPoint p)
        {
            GeoPoint2D res = surface.PositionOf(p);
            if (surface.IsUPeriodic || surface.IsVPeriodic)
            {
                if (surface is ISurfaceImpl && (surface as ISurfaceImpl).usedArea != BoundingRect.EmptyBoundingRect)
                    SurfaceHelper.AdjustPeriodic(surface, (surface as ISurfaceImpl).usedArea, ref res);
                else
                    SurfaceHelper.AdjustPeriodic(surface, Area.GetExtent(), ref res);
            }
            return res;
        }
        /// <summary>
        /// Returns the on the surface projected curve in the correct periodic domain. Reverses the curve when requested
        /// </summary>
        /// <param name="crv">the curve to project</param>
        /// <param name="forward">true: same direction as <paramref name="crv"/> false: reverse direction</param>
        /// <returns>the projected curve</returns>
        internal ICurve2D GetProjectedCurve(ICurve crv, bool forward)
        {
            ICurve2D res = Surface.GetProjectedCurve(crv, 0.0);
            if (!forward) res.Reverse();
            SurfaceHelper.AdjustPeriodic(surface, Area.GetExtent(), res);
            return res;
        }
        private int StepBound(ExportStep export, Edge[] edges)
        {
            // #65=EDGE_LOOP('',(#66,#67,#68,#69)) ;
            // #70=FACE_OUTER_BOUND('',#65,.T.);
            StringBuilder edgloop = new StringBuilder();
            for (int i = 0; i < edges.Length; i++)
            {
                int nr = (edges[i] as IExportStep).Export(export, edges[i].Forward(this)); // second parameter misused for orientation
                if (nr == -1) continue; // a pole
                if (edgloop.Length == 0) edgloop.Append("#" + nr.ToString());
                else edgloop.Append(",#" + nr.ToString());
            }
            int nel = export.WriteDefinition("EDGE_LOOP('',(" + edgloop.ToString() + "))");
            return export.WriteDefinition("FACE_OUTER_BOUND('',#" + nel.ToString() + ",.T.)");
        }
        int IExportStep.Export(ExportStep export, bool topLevel)
        {
            if (topLevel)
            {
                Shell sh = Shell.Construct();
                sh.SetFaces(new Face[] { this.Clone() as Face });
                sh.CopyAttributes(this);
                return (sh as IExportStep).Export(export, true);
            }
            else
            {
#if DEBUG
                IntegerProperty ip = UserData.GetData("StepImport.ItemNumber") as IntegerProperty;
                int iv = 0;
                if (ip != null) iv = ip.IntegerValue;
#endif
                StringBuilder boundList = new StringBuilder();
                int outlnr = StepBound(export, outline);
                boundList.Append("#" + outlnr.ToString());
                for (int i = 0; i < holes.Length; i++)
                {
                    int holenr = StepBound(export, holes[i]);
                    boundList.Append(",#" + holenr.ToString());
                }
                int surfacenr = (Surface as IExportStep).Export(export, topLevel);
                // STEP cannot write cylinders etc. in inverse orientation. So these surfaces return a negative number if the surface is oriented to the inside, while they write a
                // normal surface, which is always oriented to the outside.
                string orient;
                if (surfacenr < 0)
                {
                    surfacenr = -surfacenr;
                    orient = ".F.";
                }
                else orient = ".T.";
#if DEBUG
                int af = export.WriteDefinition("ADVANCED_FACE('" + iv.ToString() + "',(" + boundList.ToString() + "),#" + surfacenr.ToString() + "," + orient + ")");
#else
                int af = export.WriteDefinition("ADVANCED_FACE('" + this.NameOrEmpty + "',(" + boundList.ToString() + "),#" + surfacenr.ToString() + "," + orient + ")");
#endif
                if (colorDef != null && Owner is IColorDef && (Owner as IColorDef).ColorDef != colorDef)
                {
                    colorDef.MakeStepStyle(af, export);
                }
                return af;
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
            MenuWithHandler mhdist = new MenuWithHandler();
            mhdist.ID = "MenuId.Parametrics.DistanceTo";
            mhdist.Text = StringTable.GetString("MenuId.Parametrics.DistanceTo", StringTable.Category.label);
            mhdist.Target = new ParametricsDistanceActionOld(this, frame);
            res.Add(mhdist);
            MenuWithHandler mhsel = new MenuWithHandler();
            mhsel.ID = "MenuId.Selection.Set";
            mhsel.Text = StringTable.GetString("MenuId.Selection.Set", StringTable.Category.label);
            mhsel.Target = new SetSelection(this, frame.ActiveAction as SelectObjectsAction);
            MenuWithHandler mhadd = new MenuWithHandler();
            mhadd.ID = "MenuId.Selection.Add";
            mhadd.Text = StringTable.GetString("MenuId.Selection.Add", StringTable.Category.label);
            mhadd.Target = new SetSelection(this, frame.ActiveAction as SelectObjectsAction);
            res.Add(mhsel);
            res.Add(mhadd);
            return res.ToArray();
        }
    }
}

