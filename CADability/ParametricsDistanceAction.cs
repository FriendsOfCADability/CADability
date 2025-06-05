using CADability.GeoObject;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Text;
using static CADability.Actions.ConstructAction;

namespace CADability.Actions
{
    public class ParametricsDistanceAction : ConstructAction
    {
        private GeoPoint point1; // the first point of the line defining the distance
        private GeoPoint point2; // the second point of the line defining the distance. these points are not changed but together with distance they define how facesToMove and facesToKeep have to be moved
        private double distance; // the actual distance as provided by distanceInput
        private Shell shell; // the shell containing the edge
        private HashSet<Face> forwardFaces; // list of the faces in the forward direction
        private HashSet<Face> backwardFaces; // list of the faces in the backward direction
        private Line axis = null; // if an axis is provided, we need another object for the distance
        private bool validResult;
        private LengthInput distanceInput; // input filed of the distance
        private MultipleChoiceInput modeInput; // input field for the mode "forward", backward", "symmetric"
        private GeoObjectInput forwardFacesInput; // input field for more faces to modify in forward direction
        private GeoObjectInput backwardFacesInput; // input field for more faces to modify in bacward direction
        private enum Mode { forward, symmetric, backward };
        private Mode mode;
        private GeoObjectList offsetFeedBack; // an arrow to indicate the distance
        private Plane feedbackPlane; // the plane in which the feedback arrow is placed
        private StringInput nameInput;
        private string parametricsName;
        private BooleanInput preserveInput;
        private ParametricDistanceProperty parametricProperty;

        //public ParametricsDistanceAction(object distanceFromHere, IFrame frame)
        //{
        //    this.distanceFromHere = distanceFromHere;
        //    feedbackPlane = frame.ActiveView.Projection.ProjectionPlane; // TODO: find a better plane!
        //    IGeoObject owner;
        //    if (distanceFromHere is Vertex vtx)
        //    {
        //        owner = vtx.Edges[0].Curve3D as IGeoObject;
        //        offsetStartPoint = vtx.Position;
        //    }
        //    else if (distanceFromHere is Edge edge)
        //    {
        //        owner = edge.PrimaryFace;
        //        offsetStartPoint = edge.Curve3D.PointAt(0.5);
        //    }
        //    else if (distanceFromHere is Face fc)
        //    {
        //        owner = fc;
        //        GeoPoint2D inner = fc.Area.GetSomeInnerPoint();
        //        offsetStartPoint = fc.Surface.PointAt(inner);
        //    }
        //    else throw new ApplicationException("ParametricsDistance must be called with a vertex, edge or face");
        //    offsetFeedBack = frame.ActiveView.Projection.MakeArrow(offsetStartPoint, offsetStartPoint, feedbackPlane, Projection.ArrowMode.circleArrow);
        //    while (owner != null && !(owner is Shell)) owner = owner.Owner as IGeoObject;
        //    shell = owner as Shell; // this is the shell to be modified
        //    this.frame = frame;
        //    faces2 = new List<Face>();
        //    faces1 = new List<Face>();
        //    facesToMoveIsFixed = false;
        //}

        //public ParametricsDistanceAction(IEnumerable<Face> facesToMove, IFrame frame)
        //{
        //    this.frame = frame;
        //    this.faces2 = new List<Face>(facesToMove);
        //    this.faces1 = new List<Face>();
        //    facesToMoveIsFixed = true;
        //    shell = this.faces2[0].Owner as Shell;
        //    distanceFromHere = null;
        //}

        public ParametricsDistanceAction(IEnumerable<Face> facesToMove, Line axis, IFrame frame) : base()
        {
            forwardFaces = new HashSet<Face>(facesToMove);
            backwardFaces = new HashSet<Face>();
            shell = forwardFaces.First().Owner as Shell;
            this.axis = axis;
            try
            {
                feedbackPlane = new Plane(axis.StartPoint, axis.StartDirection, axis.StartDirection ^ frame.ActiveView.Projection.DrawingPlane.Normal);
            }
            catch (PlaneException)
            {
                feedbackPlane = frame.ActiveView.Projection.DrawingPlane;
            }
            offsetFeedBack = new GeoObjectList(axis);
        }
        //public ParametricsDistanceAction(Edge fromHere, Edge toHere, Line feedback, Plane plane, IFrame frame)
        //{
        //    distanceFromHere = fromHere;
        //    distanceToHere = toHere;
        //    offsetStartPoint = feedback.StartPoint;
        //    offsetFeedBack = frame.ActiveView.Projection.MakeArrow(feedback.StartPoint, feedback.EndPoint, plane, Projection.ArrowMode.circleArrow);
        //    feedbackPlane = plane;
        //    originalOffset = feedback.EndPoint - feedback.StartPoint;
        //    shell = fromHere.PrimaryFace.Owner as Shell;
        //    faces2 = new List<Face>();
        //    faces1 = new List<Face>();
        //    if (toHere != null)
        //    {
        //        if (!toHere.PrimaryFace.Surface.IsExtruded(originalOffset)) faces2.Add(toHere.PrimaryFace);
        //        if (!toHere.SecondaryFace.Surface.IsExtruded(originalOffset)) faces2.Add(toHere.SecondaryFace);
        //        facesToMoveIsFixed = true;
        //    }
        //    if (fromHere != null)
        //    {
        //        if (!fromHere.PrimaryFace.Surface.IsExtruded(originalOffset)) faces1.Add(fromHere.PrimaryFace);
        //        if (!fromHere.SecondaryFace.Surface.IsExtruded(originalOffset)) faces1.Add(fromHere.SecondaryFace);
        //    }
        //}
        public ParametricsDistanceAction(IEnumerable<Face> part1, IEnumerable<Face> part2, GeoPoint point1, GeoPoint point2, IFrame frame) : base()
        {
            //distanceFromHere = fromHere;
            //distanceToHere = toHere;
            offsetFeedBack = frame.ActiveView.Projection.MakeArrow(point1, point2, frame.ActiveView.Projection.DrawingPlane, Projection.ArrowMode.circleArrow);
            feedbackPlane = frame.ActiveView.Projection.DrawingPlane;
            forwardFaces = new HashSet<Face>(part2);
            backwardFaces = new HashSet<Face>(part1);
            shell = forwardFaces.First().Owner as Shell;
            this.point2 = point2;
            this.point1 = point1;
        }

        public override string GetID()
        {
            return "Constr.Parametrics.DistanceTo";
        }
        public override bool OnCommand(string MenuId)
        {
            switch (MenuId)
            {
                case "MenuId.Parametrics.DistanceTo":
                    Frame.SetAction(this); // this is the way this action comes to life
                    return true;
            }
            return false;
        }
        public override void OnSetAction()
        {
            base.TitleId = "Constr.Parametrics.DistanceTo";
            if (offsetFeedBack != null) FeedBack.AddSelected(offsetFeedBack);
            if (forwardFaces != null) FeedBack.AddSelected(forwardFaces);
            base.ActiveObject = shell.Clone();
            if (shell.Layer != null) shell.Layer.Transparency = 128;
            List<InputObject> actionInputs = new List<InputObject>();

            if (axis != null) // we have no object for 
            {
                GeoObjectInput toObjectInput = new GeoObjectInput("DistanceTo.ToObject"); // to which object do we calculate the distance of the axis
                toObjectInput.FacesOnly = true;
                toObjectInput.EdgesOnly = true;
                toObjectInput.Points = true;
                toObjectInput.MouseOverGeoObjectsEvent += ToObject_MouseOverGeoObjectsEvent;
                //toObjectInput.GeoObjectSelectionChangedEvent += ToObject_GeoObjectSelectionChangedEvent;
                actionInputs.Add(toObjectInput);
            }

            distance = point1 | point2;
            distanceInput = new LengthInput("DistanceTo.Distance");
            distanceInput.GetLengthEvent += DistanceInput_GetLengthEvent;
            distanceInput.SetLengthEvent += DistanceInput_SetLengthEvent;
            actionInputs.Add(distanceInput);

            forwardFacesInput = new GeoObjectInput("DistanceTo.MoreForwardObjects");
            forwardFacesInput.MultipleInput = true;
            forwardFacesInput.FacesOnly = true;
            forwardFacesInput.Optional = true;
            forwardFacesInput.MouseOverGeoObjectsEvent += new GeoObjectInput.MouseOverGeoObjectsDelegate(OnMouseOverForwardFaces);
            actionInputs.Add(forwardFacesInput);

            backwardFacesInput = new GeoObjectInput("DistanceTo.MoreBackwardObjects");
            backwardFacesInput.MultipleInput = true;
            backwardFacesInput.FacesOnly = true;
            backwardFacesInput.Optional = true;
            backwardFacesInput.MouseOverGeoObjectsEvent += new GeoObjectInput.MouseOverGeoObjectsDelegate(OnMouseOverBackwardFaces);
            actionInputs.Add(backwardFacesInput);

            modeInput = new MultipleChoiceInput("DistanceTo.Mode", "DistanceTo.Mode.Values", 0);
            modeInput.SetChoiceEvent += ModeInput_SetChoiceEvent;
            actionInputs.Add(modeInput);
            //modeInput.GetChoiceEvent += ModeInput_GetChoiceEvent;

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

            FeedBack.SelectOutline = false;
            validResult = false;
        }

        private string NameInput_GetStringEvent()
        {
            if (parametricsName == null) return string.Empty;
            else return parametricsName;
        }

        private void NameInput_SetStringEvent(string val)
        {
            parametricsName = val;
        }

        private bool OnMouseOverMoreFaces(GeoObjectInput sender, IGeoObject[] theGeoObjects, bool up, HashSet<Face> moreFaces)
        {
            List<Face> faces = new List<Face>();
            foreach (IGeoObject geoObject in theGeoObjects)
            {
                if (geoObject is Face face && !face.Surface.IsExtruded(point2 - point1)) faces.Add(face);
            }
            if (faces.Count > 0)
            {
                if (up)
                {
                    foreach (Face face in faces)
                    {
                        if (moreFaces.Contains(face)) moreFaces.Remove(face); // not sure whethwer to remove
                        else moreFaces.Add(face);
                    }
                    sender.SetGeoObject(moreFaces.ToArray(), null);
                    Refresh();
                }
                return faces.Count > 0;
            }
            return false;

        }
        private bool OnMouseOverBackwardFaces(GeoObjectInput sender, IGeoObject[] theGeoObjects, bool up)
        {
            return OnMouseOverMoreFaces(sender, theGeoObjects, up, backwardFaces);
        }

        private bool OnMouseOverForwardFaces(GeoObjectInput sender, IGeoObject[] theGeoObjects, bool up)
        {
            return OnMouseOverMoreFaces(sender, theGeoObjects, up, forwardFaces);
        }
        private bool DistanceFromAxis(IGeoObject[] geoObjects)
        {
            double distance = 0.0;
            for (int i = 0; i < geoObjects.Length; i++)
            {
                object candidate = geoObjects[i];
                if (geoObjects[i].Owner is Edge edg) candidate = edg;
                if (candidate is Face fc)
                {   // is it possible to specify a distance between this face and the axis?
                    if (fc.Surface is PlaneSurface ps && Precision.IsPerpendicular(ps.Normal, axis.StartDirection, false))
                    {   // we can use the distance to the plane 
                        point1 = axis.PointAt(0.5);
                        point2 = ps.Plane.ToGlobal(ps.Plane.Project(point1));
                        distance = point1 | point2;
                    }
                }
                Line lineToCheck = null;
                if (candidate is Edge edge && edge.Curve3D is Line line)
                {
                    lineToCheck = line;
                }
                if (candidate is Line axl && axl.UserData.Contains("CADability.AxisOf"))
                {
                    lineToCheck = axl;
                }
                if (lineToCheck != null)
                {
                    double dist = Geometry.DistLL(axis.StartPoint, axis.StartDirection, lineToCheck.StartPoint, lineToCheck.StartDirection, out double par1, out double par2);
                    if (par1 != double.MaxValue && dist > 0.0)
                    {
                        point1 = axis.StartPoint + par1 * axis.StartDirection;
                        point2 = lineToCheck.StartPoint + par2 * lineToCheck.StartDirection;
                        distance = point1 | point2;
                    }
                }
                if (distance > 0.0) return true; // we have found a suitable object
            }
            return false;
        }
        private bool ToObject_MouseOverGeoObjectsEvent(GeoObjectInput sender, IGeoObject[] geoObjects, bool up)
        {   // we need to implement more cases here, resulting in faceToMove, faceToKeep (maybe null) and a reference point from where to calculate foot-points for the offset vector

            Projection.PickArea pa = CurrentMouseView.Projection.GetPickSpace(new Rectangle(sender.currentMousePoint.X - 5, sender.currentMousePoint.Y - 5, 10, 10));
            if (axis != null && DistanceFromAxis(geoObjects))
            {
                return true;
            }
            return false;
        }

        private int ModeInput_GetChoiceEvent()
        {
            return (int)mode;
        }

        private void ModeInput_SetChoiceEvent(int val)
        {
            mode = (Mode)val;
            Refresh();
        }

        public override void OnDone()
        {
            if (validResult && ActiveObject != null)
            {
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
                        if (!string.IsNullOrEmpty(parametricsName) && parametricProperty != null)
                        {
                            parametricProperty.Name = parametricsName;
                            parametricProperty.Preserve = preserveInput.Value;
                            replacement.Shells[0].AddParametricProperty(parametricProperty);
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
        public override void OnRemoveAction()
        {
            if (shell.Layer != null) shell.Layer.Transparency = 0; // make the layer opaque again
            base.OnRemoveAction();
        }
        private bool Refresh()
        {
            validResult = false;
            if (forwardFaces.Count > 0 && backwardFaces.Count > 0)
            {
                FeedBack.ClearSelected();
                GeoPoint startPoint = point1;
                GeoPoint endPoint = point2;
                GeoVector dir = (point2 - point1).Normalized;
                double originalDistance = point2 | point1;
                switch (mode)
                {
                    case Mode.forward:
                        endPoint = point2 + (distance - originalDistance) * dir;
                        offsetFeedBack = Frame.ActiveView.Projection.MakeArrow(startPoint, endPoint, feedbackPlane, Projection.ArrowMode.circleArrow);
                        break;
                    case Mode.backward:
                        startPoint = point1 - (distance - originalDistance) * dir;
                        offsetFeedBack = Frame.ActiveView.Projection.MakeArrow(endPoint, startPoint, feedbackPlane, Projection.ArrowMode.circleArrow);
                        break;
                    case Mode.symmetric:
                        startPoint = point1 - 0.5 * (distance - originalDistance) * dir;
                        endPoint = point2 + 0.5 * (distance - originalDistance) * dir;
                        GeoPoint mp = new GeoPoint(startPoint, endPoint);
                        offsetFeedBack = Frame.ActiveView.Projection.MakeArrow(mp, startPoint, feedbackPlane, Projection.ArrowMode.circleArrow);
                        offsetFeedBack.AddRange(Frame.ActiveView.Projection.MakeArrow(mp, endPoint, feedbackPlane, Projection.ArrowMode.circleArrow));
                        break;
                }
                offsetFeedBack.AddRange(forwardFaces);
                offsetFeedBack.AddRange(backwardFaces);
                FeedBack.AddSelected(offsetFeedBack);
                Shell sh = null;
                for (int m = 0; m <= 1; m++)
                {   // first try without moving connected faces, if this yields no result, try with moving connected faced
                    Parametric parametric = new Parametric(shell);
                    Dictionary<Face, GeoVector> allFacesToMove = new Dictionary<Face, GeoVector>();
                    GeoVector offset = (distance - originalDistance) * dir;
                    switch (mode)
                    {
                        case Mode.forward:
                            foreach (Face face in forwardFaces)
                            {
                                allFacesToMove[face] = offset;
                            }
                            foreach (Face face in backwardFaces)
                            {
                                allFacesToMove[face] = GeoVector.NullVector;
                            }
                            break;
                        case Mode.symmetric:
                            foreach (Face face in forwardFaces)
                            {
                                allFacesToMove[face] = 0.5 * offset;
                            }
                            foreach (Face face in backwardFaces)
                            {
                                allFacesToMove[face] = -0.5 * offset;
                            }
                            break;
                        case Mode.backward:
                            foreach (Face face in backwardFaces)
                            {
                                allFacesToMove[face] = -offset;
                            }
                            foreach (Face face in forwardFaces)
                            {
                                allFacesToMove[face] = GeoVector.NullVector;
                            }
                            break;
                    }
                    parametric.MoveFaces(allFacesToMove, offset, m == 1);
                    if (parametric.Apply())
                    {
                        sh = parametric.Result();
                        if (sh != null)
                        {
                            ParametricDistanceProperty.Mode pmode = 0;
                            if (m == 1) pmode |= ParametricDistanceProperty.Mode.connected;
                            if (mode == Mode.symmetric) pmode |= ParametricDistanceProperty.Mode.symmetric;
                            // create the ParametricDistanceProperty here, because here we have all the information
                            parametric.GetDictionaries(out Dictionary<Face, Face> faceDict, out Dictionary<Edge, Edge> edgeDict, out Dictionary<Vertex, Vertex> vertexDict);
                            // facesToKeep etc. contains original objects of the shell, affectedObjects contains objects of the sh = pm.Result()
                            // the parametricProperty will be applied to sh, so we need the objects from sh
                            if (mode == Mode.backward)
                            {
                                parametricProperty = new ParametricDistanceProperty("", Extensions.LookUp(forwardFaces, faceDict),
                                    Extensions.LookUp(backwardFaces, faceDict),
                                    parametric.GetAffectedObjects(), pmode, point2, point1);
                            }
                            else
                            {
                                parametricProperty = new ParametricDistanceProperty("", Extensions.LookUp(backwardFaces, faceDict),
                                    Extensions.LookUp(forwardFaces, faceDict),
                                    parametric.GetAffectedObjects(), pmode, point1, point2);
                            }
                            break;
                        }
                    }
                }
                if (sh != null)
                {
                    ActiveObject = sh;
                    validResult = true;
                    return true;
                }
                else
                {
                    ActiveObject = shell.Clone();
                    return false;
                }

            }
            return false;

        }
        private bool DistanceInput_SetLengthEvent(double length)
        {
            validResult = false;
            distance = length;
            return Refresh();
            return false;
        }

        private double DistanceInput_GetLengthEvent()
        {
            return distance;
        }

    }
}

