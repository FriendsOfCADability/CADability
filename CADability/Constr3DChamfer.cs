using CADability.GeoObject;
using System.Collections.Generic;

namespace CADability.Actions
{
    internal class Constr3DChamfer : ConstructAction
    {
        private GeoObjectInput faceInput;
        private GeoObjectInput edgesInput;
        private LengthInput dist1Input;
        private LengthInput dist2Input;
        private List<Edge> edges;
        private Face theFace;
        private double dist1, dist2;
        private bool firstClickClearsAll;

        public Constr3DChamfer()
        {
            edges = new List<Edge>();
        }
        public override string GetID()
        {
            return "Constr.Chamfer";
        }
        public override void OnSetAction()
        {
            base.TitleId = "Constr.Chamfer";

            faceInput = new GeoObjectInput("Constr.Chamfer.Face");
            faceInput.FacesOnly = true;
            faceInput.MultipleInput = false;
            faceInput.MouseOverGeoObjectsEvent += new GeoObjectInput.MouseOverGeoObjectsDelegate(OnMouseOverFace);

            edgesInput = new GeoObjectInput("Constr.Chamfer.Edges");
            edgesInput.EdgesOnly = true;
            edgesInput.MultipleInput = true;
            edgesInput.MouseOverGeoObjectsEvent += new GeoObjectInput.MouseOverGeoObjectsDelegate(OnMouseOverEdges);
            edgesInput.Optional = true; // ein Face muss gegeben werden. Dann werden alle Kanten genommen
            // und man kann damit schon fertig machen. Wenn man einzelne kanten haben will, kann man immer noch
            // auf einzelne kanten klicken

            dist1Input = new LengthInput("Constr.Chamfer.Dist1");
            dist1Input.SetLengthEvent += new LengthInput.SetLengthDelegate(OnSetDist1);
            dist1Input.GetLengthEvent += new LengthInput.GetLengthDelegate(OnGetDist1);
            dist1Input.DefaultLength = ConstrDefaults.DefaultCutOffLength;
            dist1Input.ForwardMouseInputTo = edgesInput;

            dist2Input = new LengthInput("Constr.Chamfer.Dist2");
            dist2Input.SetLengthEvent += new LengthInput.SetLengthDelegate(OnSetDist2);
            dist2Input.GetLengthEvent += new LengthInput.GetLengthDelegate(OnGetDist2);

            base.SetInput(faceInput, edgesInput, dist1Input, dist2Input);
            base.OnSetAction();
            firstClickClearsAll = false;
        }
        double OnGetDist1()
        {
            return dist1;
        }
        bool OnSetDist1(double Length)
        {
            if (Length > 0.0)
            {
                dist1 = Length;
                if (dist2 == 0.0) dist2 = dist1;
                return true;
            }
            return false;
        }
        double OnGetDist2()
        {
            return dist2;
        }
        bool OnSetDist2(double Length)
        {
            if (Length > 0.0)
            {
                dist2 = Length;
                return true;
            }
            return false;
        }
        bool OnMouseOverEdges(ConstructAction.GeoObjectInput sender, IGeoObject[] TheGeoObjects, bool up)
        {
            if (theFace == null) return false;
            bool ok = false;
            Edge toSelect = null;
            for (int i = 0; i < TheGeoObjects.Length; ++i)
            {
                if (TheGeoObjects[i].Owner is Edge)
                {
                    Edge edge = TheGeoObjects[i].Owner as Edge;
                    if (edge.PrimaryFace == theFace || edge.SecondaryFace == theFace)
                    {
                        ok = true;
                        if (up)
                        {
                            if (firstClickClearsAll)
                            {
                                firstClickClearsAll = false;
                                foreach (Edge e in edges)
                                {
                                    IGeoObject go = e.Curve3D;
                                    if (go != null)
                                    {
                                        base.FeedBack.RemoveSelected(go);
                                    }
                                }
                                edges.Clear();
                            }
                            if (edges.Contains(edge))
                            {
                                edges.Remove(edge);
                                base.FeedBack.RemoveSelected(edge.Curve3D);
                            }
                            else
                            {
                                toSelect = edge;
                                edges.Add(edge);
                                base.FeedBack.AddSelected(edge.Curve3D);
                                edgesInput.Optional = true;
                            }
                        }
                    }
                }
            }
            if (up)
            {
                List<IGeoObject> sel = new List<IGeoObject>();
                for (int i = 0; i < edges.Count; ++i)
                {
                    sel.Add(edges[i].Curve3D);
                }
                if (toSelect != null)
                    edgesInput.SetGeoObject(sel.ToArray(), toSelect.Curve3D);
                else
                    edgesInput.SetGeoObject(sel.ToArray(), null);
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
                        theFace = (TheGeoObjects[i] as Face);
                        foreach (Edge edge in edges)
                        {
                            base.FeedBack.RemoveSelected(edge.Curve3D);
                        }
                        edges.Clear();
                        edges.AddRange(theFace.AllEdges);
                        List<IGeoObject> geoObjects = new List<IGeoObject>();
                        foreach (Edge edge in edges)
                        {
                            IGeoObject go = edge.Curve3D;
                            if (go != null)
                            {
                                base.FeedBack.AddSelected(go);
                                geoObjects.Add(go);
                            }
                        }
                        edgesInput.SetGeoObject(geoObjects.ToArray(), null);
                        firstClickClearsAll = true;
                    }
                }
            }
            return ok;
        }

        public override void OnDone()
        {
            if (edges.Count > 0)
            {
                IGeoObject[] affected;
                IGeoObject[] modified = Make3D.MakeChamfer(theFace, edges.ToArray(), dist1, dist2, out affected);
                if (affected.Length > 0)
                {
                    using (Frame.Project.Undo.UndoFrame)
                    {
                        IGeoObjectOwner owner = null;
                        for (int i = 0; i < affected.Length; ++i)
                        {
                            if (owner == null || affected[i].Owner is Model)
                            {
                                owner = affected[i].Owner;
                            }
                            affected[i].Owner.Remove(affected[i]);
                        }
                        for (int i = 0; i < modified.Length; ++i)
                        {
                            owner.Add(modified[i]);
                        }
                    }
                }
            }
            base.OnDone();
        }
    }
}
