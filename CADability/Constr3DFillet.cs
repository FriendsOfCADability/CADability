using CADability.GeoObject;
using System.Collections.Generic;
using System.Linq;

namespace CADability.Actions
{
    public class Constr3DFillet : ConstructAction
    {
        private GeoObjectInput geoObjectInput;
        private GeoObjectInput faceInput;
        private GeoObjectInput shellInput;
        private LengthInput radiusInput;
        private List<Edge> edges;
        private double radius;

        public Constr3DFillet()
        {
            edges = new List<Edge>();
        }

        public Constr3DFillet(IEnumerable<Edge> preselected)
        {
            edges = new List<Edge>(preselected);
        }

        public override void OnSetAction()
        {
            base.TitleId = "Constr.Fillet";

            geoObjectInput = new GeoObjectInput("Constr.Fillet.Edges");
            geoObjectInput.EdgesOnly = true;
            geoObjectInput.MultipleInput = true;
            geoObjectInput.MouseOverGeoObjectsEvent += new GeoObjectInput.MouseOverGeoObjectsDelegate(OnMouseOverEdges);
            // we would need a mass selection (rectangular selection) for GeoObjectInput

            faceInput = new GeoObjectInput("Constr.Fillet.Face");
            faceInput.FacesOnly = true;
            faceInput.Optional = true;
            faceInput.MultipleInput = false;
            faceInput.MouseOverGeoObjectsEvent += new GeoObjectInput.MouseOverGeoObjectsDelegate(OnMouseOverFace);

            shellInput = new GeoObjectInput("Constr.Fillet.Shell");
            shellInput.Optional = true;
            shellInput.MultipleInput = false;
            shellInput.MouseOverGeoObjectsEvent += new GeoObjectInput.MouseOverGeoObjectsDelegate(OnMouseOverShell);

            radiusInput = new LengthInput("Constr.Fillet.Radius");
            radiusInput.SetLengthEvent += new LengthInput.SetLengthDelegate(OnSetRadius);
            radiusInput.GetLengthEvent += new LengthInput.GetLengthDelegate(OnGetRadius);
            radiusInput.DefaultLength = ConstrDefaults.DefaultRoundRadius;
            radiusInput.ForwardMouseInputTo = geoObjectInput;

            base.SetInput(geoObjectInput, faceInput, shellInput, radiusInput);
            base.OnSetAction();

            if (edges.Count > 0)
            {
                geoObjectInput.SetGeoObject(edges.Select(e => e.Curve3D as IGeoObject).ToArray(), edges[0].Curve3D as IGeoObject);
                geoObjectInput.Fixed = true;
                SetFocus(radiusInput, false);
            }
        }

        double OnGetRadius()
        {
            return radius;
        }

        bool OnSetRadius(double Length)
        {
            if (Length > 0.0)
            {
                radius = Length;
                return true;
            }
            return false;
        }

        bool OnMouseOverEdges(ConstructAction.GeoObjectInput sender, IGeoObject[] TheGeoObjects, bool up)
        {
            bool ok = false;
            Edge toSelect = null;
            for (int i = 0; i < TheGeoObjects.Length; ++i)
            {
                if (TheGeoObjects[i].Owner is Edge)
                {
                    ok = true;
                    if (up)
                    {
                        Edge edge = TheGeoObjects[i].Owner as Edge;
                        if (edges.Contains(edge))
                        {
                            edges.Remove(edge);
                            base.FeedBack.RemoveSelected(edge.Curve3D as IGeoObject);
                        }
                        else
                        {
                            toSelect = edge;
                            edges.Add(edge);
                            base.FeedBack.AddSelected(edge.Curve3D as IGeoObject);
                            geoObjectInput.Optional = true;
                        }
                    }
                }
            }
            if (up)
            {
                List<IGeoObject> sel = new List<IGeoObject>();
                for (int i = 0; i < edges.Count; ++i)
                {
                    sel.Add(edges[i].Curve3D as IGeoObject);
                }
                if (toSelect != null)
                    geoObjectInput.SetGeoObject(sel.ToArray(), toSelect.Curve3D as IGeoObject);
                else
                    geoObjectInput.SetGeoObject(sel.ToArray(), null);
            }
            return ok;
        }

        bool OnMouseOverFace(ConstructAction.GeoObjectInput sender, IGeoObject[] TheGeoObjects, bool up)
        {
            bool ok = false;
            for (int i = 0; i < TheGeoObjects.Length; ++i)
            {
                if (TheGeoObjects[i] is Face)
                {
                    ok = true;
                    if (up)
                    {
                        Face theFace = (TheGeoObjects[i] as Face);
                        List<Edge> edgesTemp = new List<Edge>(theFace.AllEdges);
                        List<IGeoObject> geoObjects = new List<IGeoObject>();
                        foreach (Edge edge in edgesTemp)
                        {
                            IGeoObject go = edge.Curve3D as IGeoObject;
                            if (go != null)
                            {
                                // base.FeedBack.AddSelected(go);
                                if (edges.Contains(edge))
                                {
                                    edges.Remove(edge);
                                    base.FeedBack.RemoveSelected(edge.Curve3D as IGeoObject);
                                }
                                else
                                {
                                    edges.Add(edge);
                                    base.FeedBack.AddSelected(edge.Curve3D as IGeoObject);
                                    geoObjectInput.Optional = true;
                                }
                                geoObjects.Add(go);
                            }
                        }
                        geoObjectInput.SetGeoObject(geoObjects.ToArray(), null);
                    }
                }
            }
            return ok;
        }

        bool OnMouseOverShell(ConstructAction.GeoObjectInput sender, IGeoObject[] TheGeoObjects, bool up)
        {
            bool ok = false;
            for (int i = 0; i < TheGeoObjects.Length; ++i)
            {
                if ((TheGeoObjects[i] is Solid) || (TheGeoObjects[i] is Shell))
                {
                    ok = true;
                    if (up)
                    {
                        List<Edge> edgesTemp;
                        if (TheGeoObjects[i] is Solid)
                        {
                            Solid theSolid = (TheGeoObjects[i] as Solid);
                            edgesTemp = new List<Edge>(theSolid.Edges);
                        }
                        else
                        {
                            Shell theShell = (TheGeoObjects[i] as Shell);
                            edgesTemp = new List<Edge>(theShell.Edges);

                        }
                        List<IGeoObject> geoObjects = new List<IGeoObject>();
                        foreach (Edge edge in edgesTemp)
                        {
                            IGeoObject go = edge.Curve3D as IGeoObject;
                            if (go != null)
                            {
                                // base.FeedBack.AddSelected(go);
                                if (edges.Contains(edge))
                                {
                                    edges.Remove(edge);
                                    base.FeedBack.RemoveSelected(edge.Curve3D as IGeoObject);
                                }
                                else
                                {
                                    if (edge.SecondaryFace != null)
                                    {
                                        edges.Add(edge);
                                        base.FeedBack.AddSelected(edge.Curve3D as IGeoObject);
                                        geoObjectInput.Optional = true;
                                    }
                                }
                                geoObjects.Add(go);
                            }
                        }
                        geoObjectInput.SetGeoObject(geoObjects.ToArray(), null);
                    }
                }
            }
            return ok;
        }

        public override string GetID()
        {
            return "Constr.Fillet";
        }
        public override void OnDone()
        {
            if (edges.Count > 0)
            {
                // sort the edges into groups of connected edges, then process each group seperately
                HashSet<Shell> shells = new HashSet<Shell>(); // all Shells of the edges (usually only one)
                List<List<Edge>> connectedEdges = new List<List<Edge>>();
                foreach (Edge edge in edges)
                {
                    shells.AddIfNotNull(edge.Owner?.Owner as Shell); // Edge->Face->Shell

                    List<Edge> group = connectedEdges.FirstOrDefault(g =>
                           g.Any(e => e.Vertex1 == edge.Vertex1 || e.Vertex1 == edge.Vertex2 || e.Vertex2 == edge.Vertex1 || e.Vertex2 == edge.Vertex2));

                    if (group != null)
                    {
                        group.Add(edge);
                    }
                    else
                    {
                        connectedEdges.Add(new List<Edge> { edge });
                    }
                }
                foreach (Shell shell in shells) { shell.RememberFaces(); }
                Dictionary<Shell, Shell> modifiedShell = new Dictionary<Shell, Shell>();
                using (Frame.Project.Undo.UndoFrame)
                {
                    for (int k = 0; k < connectedEdges.Count; k++)
                    {
                        Shell affected;
                        Shell owningShell = connectedEdges[k][0].Owner.Owner as Shell;
                        if (modifiedShell.TryGetValue(owningShell, out Shell m))
                        {
                            for (int i = 0; i < connectedEdges[k].Count; i++)
                            {
                                connectedEdges[k][i] = m.GetCorrespondingEdge(connectedEdges[k][i], m.GetRememberedMultiDictionary(owningShell));
                            }
                        }
                        Shell modified = Make3D.MakeFillet(connectedEdges[k].ToArray(), radius, out affected);
                        if (affected != null)
                        {
                            modifiedShell[owningShell] = modified;
                            IGeoObjectOwner owner = affected.Owner; // should be a solid if the affected shell is part of a solid, or owner should be the model
                                                                    // only the edges of a single shell should be rounded!
                            if (owner is Solid sld)
                            {
                                sld.SetShell(modified); // removes the old shell and replaces it by the new one
                            }
                            else if (owner != null)
                            {   // a Shell as part of the model or a blck
                                owner.Remove(affected);
                                owner.Add(modified);
                            }
                        }
                    }
                }
                foreach (Shell shell in shells) { shell.ForgetRememberedFaces(); }
            }
            base.OnDone();
        }
    }
}
