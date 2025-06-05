using CADability.Curve2D;
using CADability.GeoObject;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;

namespace CADability.Actions
{
    public class ParametricsCenterAction : ConstructAction
    {
        public Shell shell; // the shell beeing manipulated by this action
        public HashSet<Face> facesToBeCentered; // the faces beeing centered
        public HashSet<object> centerOntoObjects; // the objects (faces, edges or vertices) onto which it should be ceneterd
        public object centerPosition; // if null, the extent of facesToBeCentered is centered, if bRep object this object defines the center of facesToBeCentered
        private double ratio = 0.5;

        private BRepObjectInput centerOnObjects;
        private GeoObjectInput facesToCenter;
        private BRepObjectInput definingCenterPosition; // what is to be centered? a vertex, an axis or the extent of the faces?
        private DoubleInput ratioInput; // normally 0.5, bot may be between 0 and 1
        private StringInput nameInput; // Name for the Parametrics when added to the shell
        private BooleanInput preserveInput; // preserve this value, when other parametrics are applied
        private string name = "";

        private Parametric parametric;
        private bool validResult = false;

        public override string GetID()
        {
            return "Constr.Parametrics.Center";
        }

        public ParametricsCenterAction(IEnumerable<Face> faces)
        {
            shell = faces.First().Owner as Shell;
            if (shell == null) throw new ApplicationException("Faces must be part of a shell");
            facesToBeCentered = new HashSet<Face>(faces);
            centerOntoObjects = new HashSet<object>();
        }

        public override void OnSetAction()
        {
            base.TitleId = "Constr.Parametrics.Center";
            base.ActiveObject = shell.Clone();
            if (shell.Layer != null) shell.Layer.Transparency = 128;

            List<InputObject> actionInputs = new List<InputObject>();

            facesToCenter = new GeoObjectInput("Center.FacesToCenter");
            facesToCenter.FacesOnly = true;
            facesToCenter.MultipleInput = true;
            facesToCenter.MouseOverGeoObjectsEvent += FacesToCenter_MouseOverGeoObjectsEvent;
            facesToCenter.Fixed = true;
            actionInputs.Add(facesToCenter);

            centerOnObjects = new BRepObjectInput("Center.CenterOnTo");
            centerOnObjects.MultipleInput = true;
            centerOnObjects.MouseOverBRepObjectsEvent += CenterOnObjects_MouseOverBRepObjectsEvent;
            actionInputs.Add(centerOnObjects);

            ratioInput = new DoubleInput("Center.Ratio");
            ratioInput.GetDoubleEvent += RatioInput_GetDoubleEvent;
            ratioInput.SetDoubleEvent += RatioInput_SetDoubleEvent;
            ratioInput.Fixed = true;
            actionInputs.Add(ratioInput);

            SeparatorInput separator = new SeparatorInput("Parametrics.AssociateParametric");
            actionInputs.Add(separator);
            nameInput = new StringInput("Parametrics.ParametricsName");
            nameInput.SetStringEvent += NameInput_SetStringEvent;
            nameInput.GetStringEvent += NameInput_GetStringEvent;
            nameInput.Optional = true;
            actionInputs.Add(nameInput);

            preserveInput = new BooleanInput("Parametrics.PreserveValue", "YesNo.Values", false);
            actionInputs.Add(preserveInput);

            SetInput(actionInputs.ToArray());
            base.OnSetAction();

            facesToCenter.SetGeoObject(facesToBeCentered.ToArray(), null);

            FeedBack.SelectOutline = false;

        }
        public override void OnDone()
        {
            if (validResult)
            {
                string parametricsName = nameInput.Content;
                Solid sld = shell.Owner as Solid;
                if (sld != null)
                {   // the shell was part of a Solid
                    IGeoObjectOwner owner = sld.Owner; // Model or Block
                    using (Frame.Project.Undo.UndoFrame)
                    {
                        owner.Remove(sld);
                        Solid replacement = Solid.MakeSolid(ActiveObject as Shell);
                        replacement.CopyAttributes(sld);
                        owner.Add(replacement);
                        if (!string.IsNullOrEmpty(parametricsName))
                        {
                            ParametricCenterProperty parametricProperty = new ParametricCenterProperty(parametricsName, facesToBeCentered, centerOntoObjects, 
                                new List<object> { centerPosition } , new List<object>(), ratio);
                            parametricProperty.Name = parametricsName;
                            parametricProperty.Preserve = preserveInput.Value;
                            replacement.Shells[0].AddParametricProperty(parametricProperty.Clone(parametric)); // since replacement contains a cloned shell, we also need 
                            // the parametric to be a clone in order to refer to the same BRep objects
                        }
                    }
                }
                else
                {
                    IGeoObjectOwner owner = shell.Owner;
                    using (Frame.Project.Undo.UndoFrame)
                    {
                        owner.Remove(shell);
                        owner.Add(ActiveObject);
                    }
                }
            }
            ActiveObject = null;
            base.OnDone();
        }
        public static GeoVector GetTranslationForCentering(IEnumerable<Face> facesToBeCentered, object centerPosition, IEnumerable<object> centerOntoObjects, double ratio)
        {   // currently only extent centering implemented
            GeoPoint pointToCenter; // from where to center the faces
            GeoVector normal = GeoVector.NullVector; // this vector defines a plane for the movement
            GeoPoint startPoint = GeoPoint.Invalid, endPoint = GeoPoint.Invalid;
            // preliminary: only extent implemented
            BoundingCube ext = new BoundingCube(facesToBeCentered);
            pointToCenter = ext.GetCenter();
            if (centerOntoObjects.Count() == 1)
            {   // if we have a single axis, this is the center, regardless of ratio
                object f = centerOntoObjects.First();
                if (f is Edge edg && edg.Curve3D is Ellipse ellipse)
                {
                    normal = ellipse.Plane.Normal;
                    Plane plane = new Plane(pointToCenter, normal);
                    startPoint = endPoint = plane.ToGlobal(plane.Project(ellipse.Center)); // the center of the ellipse projected into the movement-plane
                }
                if (f is Face face && face.Surface is ISurfaceOfRevolution sr)
                {
                    normal = sr.Axis.Direction;
                    Plane plane = new Plane(pointToCenter, normal);
                    startPoint = endPoint = plane.ToGlobal(plane.Project(sr.Axis.Location)); // the axis intersection with the movement-plane
                }
            }
            else if (centerOntoObjects.Count() > 1)
            {   // if we have multiple objects there are many cases (which have to be implemented)
                // two edges in a common plane
                IEnumerable<Edge> edges = centerOntoObjects.OfType<Edge>();
                IEnumerable<Vertex> vertices = centerOntoObjects.OfType<Vertex>();
                IEnumerable<Face> faces = centerOntoObjects.OfType<Face>();
                if (edges.Count() > 1)
                {
                    Edge edge = edges.First();
                    Plane commonPlane = Plane.XYPlane;
                    Edge secondEdge = centerOntoObjects.Skip(1).FirstOrDefault(o =>
                    {
                        if (o is Edge edge1 && Curves.GetCommonPlane(edge.Curve3D, edge1.Curve3D, out commonPlane)) return true;
                        return false;
                    }) as Edge;
                    if (secondEdge != null)
                    {
                        ICurve2D c1 = edge.Curve3D.GetProjectedCurve(commonPlane);
                        ICurve2D c2 = secondEdge.Curve3D.GetProjectedCurve(commonPlane);
                        GeoPoint2D p1 = GeoPoint2D.Invalid, p2 = GeoPoint2D.Invalid;
                        if (c1 is Line2D && c2 is Line2D && Precision.SameDirection(c1.StartDirection, c2.StartDirection, false))
                        {
                            GeoPoint2D c = commonPlane.Project(pointToCenter);
                            p1 = c1.PerpendicularFoot(c)[0];
                            p2 = c2.PerpendicularFoot(c)[0];
                        }
                        else
                        {
                            double dist = Curves2D.SimpleMinimumDistance(c1, c2, out p1, out p2);
                        }
                        Plane movingPlane = new Plane(pointToCenter, commonPlane.Normal);
                        if (p1.IsValid && p2.IsValid)
                        {
                            startPoint = movingPlane.ToGlobal(movingPlane.Project(commonPlane.ToGlobal(p1)));
                            endPoint = movingPlane.ToGlobal(movingPlane.Project(commonPlane.ToGlobal(p2)));
                        }
                    }
                }
                else if (faces.Count() > 1)
                {
                    Face face = faces.First();
                    Face otherFace = faces.ElementAt(1);
                    //if (Surfaces.ParallelDistance(face.Surface, face.Domain, otherFace.Surface, otherFace.Domain, out GeoPoint2D uv1, out GeoPoint2D uv2))
                    //{
                    //    GeoPoint p1 = face.Surface.PointAt(uv1);
                    //    GeoPoint p2 = face.Surface.PointAt(uv2);
                    //    if (face.Surface is PlaneSurface && otherFace.Surface is PlaneSurface)
                    //    {
                    //        // we are free to move the two points so that the line goes through pointToCenter
                    //        GeoPoint closest = Geometry.DropPL(pointToCenter, p1, p2);
                    //        GeoVector translate = pointToCenter - closest;
                    //        startPoint = p1 + translate;
                    //        endPoint = p2 + translate;
                    //    }
                    //    else
                    //    {   // with two parallel cylinders we still have a degree of freedom, tilted cylinder define p1, p2 exactely

                    //    }
                    //}
                }
                else if (vertices.Count() > 1)
                {
                    Vertex v1 = vertices.ElementAt(0);
                    Vertex v2 = vertices.ElementAt(1);
                    startPoint = v1.Position;
                    endPoint = v2.Position;
                }
                // else more cases to implement. The result should be startPoint and endPoint
            }
            if (startPoint.IsValid && endPoint.IsValid)
            {
                GeoPoint toMoveTo = startPoint + ratio * (endPoint - startPoint);
                Dictionary<Face, GeoVector> toMove = new Dictionary<Face, GeoVector>();
                return toMoveTo - pointToCenter;
            }
            return GeoVector.NullVector;
        }
        private bool Recalc()
        {
            validResult = false;
            GeoVector translation = GetTranslationForCentering(facesToBeCentered, centerPosition, centerOntoObjects, ratio);
            if (translation.IsNullVector()) return false;
            parametric = new Parametric(shell);
            Dictionary<Face, GeoVector> toMove = new Dictionary<Face, GeoVector>();
            foreach (Face fc in facesToBeCentered) toMove[fc] = translation;
            parametric.MoveFaces(toMove, translation);
            if (parametric.Apply())
            {
                Shell modified = parametric.Result();
                if (modified != null)
                {
                    ActiveObject = modified;
                    validResult = true;
                }
            }
            FeedBack.ClearSelected();
            parametric.GetDictionaries(out Dictionary<Face, Face> faceDict, out Dictionary<Edge, Edge> edgeDict, out Dictionary<Vertex, Vertex> vertexDict);
            IEnumerable<Face> feedbackFaces = facesToBeCentered.Select(f => faceDict[f]);
            FeedBack.AddSelected(feedbackFaces);
            return true;
        }

        private bool RatioInput_SetDoubleEvent(double val)
        {
            if (val > 0 && val < 1)
            {
                ratio = val;
                Recalc();
                return true;
            }
            return false;
        }

        private double RatioInput_GetDoubleEvent()
        {
            return ratio;
        }

        private bool CenterOnObjects_MouseOverBRepObjectsEvent(BRepObjectInput sender, object[] bRepObjects, bool up)
        {
            if (up)
            {
                bool found = false;
                IEnumerable<Vertex> vertices = bRepObjects.OfType<Vertex>();
                if (vertices.Any())
                {   // prefer vertices, because they always come together with edges and faces
                    foreach (Vertex vtx in vertices)
                    {
                        if (centerOntoObjects.Contains(vtx)) centerOntoObjects.Remove(vtx);
                        else centerOntoObjects.Add(vtx);
                        sender.SetBRepObject(centerOntoObjects.ToArray(), null);
                        found = true;
                    }
                }
                else
                {
                    IEnumerable<Edge> edges = bRepObjects.OfType<Edge>();
                    if (edges.Any())
                    {
                        foreach (Edge edge in edges)
                        {
                            if (centerOntoObjects.Contains(edge)) centerOntoObjects.Remove(edge);
                            else centerOntoObjects.Add(edge);
                            sender.SetBRepObject(centerOntoObjects.ToArray(), null);
                            found = true;
                        }
                    }
                    else // only faces
                    {
                        for (int i = 0; i < bRepObjects.Length; i++)
                        {
                            if (bRepObjects[i] is Vertex || bRepObjects[i] is Face || bRepObjects[i] is Edge)
                            {
                                if (centerOntoObjects.Contains(bRepObjects[i])) centerOntoObjects.Remove(bRepObjects[i]);
                                else centerOntoObjects.Add(bRepObjects[i]);
                                sender.SetBRepObject(centerOntoObjects.ToArray(), null);
                                found = true;
                            }
                        }
                    }
                }
                if (found && Recalc()) sender.Fixed = true;
                return found;
            }
            else
            {
                for (int i = 0; i < bRepObjects.Length; i++)
                {
                    if (bRepObjects[i] is Vertex) return true;
                    if (bRepObjects[i] is Face) return true;
                    if (bRepObjects[i] is Edge) return true;
                }
            }
            return false;
        }

        private bool FacesToCenter_MouseOverGeoObjectsEvent(GeoObjectInput sender, IGeoObject[] geoObjects, bool up)
        {
            if (up)
            {
                bool found = false;
                for (int i = 0; i < geoObjects.Length; i++)
                {
                    if (geoObjects[i] is Face face)
                    {
                        if (facesToBeCentered.Contains(face)) facesToBeCentered.Remove(face);
                        else facesToBeCentered.Add(face);
                        sender.SetGeoObject(facesToBeCentered.ToArray(), null);
                        found = true;
                    }
                }
                return found;
            }
            else
            {
                for (int i = 0; i < geoObjects.Length; i++)
                {
                    if (geoObjects[i] is Face) return true;
                }
            }
            return false;
        }
        private string NameInput_GetStringEvent()
        {
            return name;
        }

        private void NameInput_SetStringEvent(string val)
        {
            name = val;
        }
    }
}
