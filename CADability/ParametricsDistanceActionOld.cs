using CADability.Actions;
using CADability.UserInterface;
using CADability.GeoObject;
using CADability.Attribute;
using System.Collections.Generic;
using System;
#if WEBASSEMBLY
using CADability.WebDrawing;
using Point = CADability.WebDrawing.Point;
#else
using System.Drawing;
using Point = System.Drawing.Point;
#endif

namespace CADability
{
    /// <summary>
    /// Should be replaced by ParametricsDistanceAction
    /// Action to modify the distance between two objects.
    /// The objects may be faces or edges or vertices (also a mix of types).
    /// First we calculate the offset vector: vertex->face: perpendicular foot point, vertex->edge: perpendicular foot point, vertex->vertex: connection
    /// edge->edge: minimum distance, edge->face: minimum distance, face->face: parallel or minimum distance. (objects with axis should also offer their axis for distance)
    /// </summary>
    internal class ParametricsDistanceActionOld : ConstructAction
    {
        private object distanceFromHere, distanceToHere; // the two objects which define the distance (may be vertices, edges or faces)
        private GeoVector originalOffset; // the offset vector between the facesToMove and facesToKeep
        private GeoVector currentOffset; // the additional offset vector between the facesToMove and facesToKeep
        private Shell shell; // the shell containing the edge
        private List<Face> facesToMove; // list of the faces, which have to be moved
        private List<Face> facesToKeep; // list of the faces, which should stay in place (ore been moved in opposite direction when symmetric or backward is chosen)
        private bool facesToMoveIsFixed; // facesToMove has been set in the constructor, no need to calculate
        private Line axisToMove; // an axis, if there is one
        private bool validResult;
        private IFrame frame;
        private GeoObjectInput fromObjectInput; // input field specifying the object from which the distance should be set
        private GeoObjectInput toObjectInput; // input field specifying the object to which the distance should be set
        private LengthInput distanceInput; // input filed of the distance
        private MultipleChoiceInput modeInput; // input field for the mode "forward", backward", "symmetric"
        private GeoObjectInput moreForwardFacesInput; // input field for more faces to modify in forward direction
        private GeoObjectInput moreBackwardFacesInput; // input field for more faces to modify in bacward direction
        private HashSet<Face> moreForwardFaces = new HashSet<Face>(); // set of faces to also modify in forward direction
        private HashSet<Face> moreBackwardFaces = new HashSet<Face>(); // set of faces to also modify in backward direction
        private enum Mode { forward, symmetric, backward };
        private Mode mode;
        private GeoPoint offsetStartPoint;
        private GeoObjectList offsetFeedBack; // an arrow to indicate the distance
        private Plane feedbackPlane; // the plane in which the feedback arrow is placed
        private StringInput nameInput;
        private string parametricsName;
        private ParametricDistanceProperty parametricProperty;

        public ParametricsDistanceActionOld(object distanceFromHere, IFrame frame)
        {
            this.distanceFromHere = distanceFromHere;
            feedbackPlane = frame.ActiveView.Projection.ProjectionPlane; // TODO: find a better plane!
            IGeoObject owner;
            if (distanceFromHere is Vertex vtx)
            {
                owner = vtx.Edges[0].Curve3D as IGeoObject;
                offsetStartPoint = vtx.Position;
            }
            else if (distanceFromHere is Edge edge)
            {
                owner = edge.PrimaryFace;
                offsetStartPoint = edge.Curve3D.PointAt(0.5);
            }
            else if (distanceFromHere is Face fc)
            {
                owner = fc;
                GeoPoint2D inner = fc.Area.GetSomeInnerPoint();
                offsetStartPoint = fc.Surface.PointAt(inner);
            }
            else throw new ApplicationException("ParametricsDistance must be called with a vertex, edge or face");
            offsetFeedBack = frame.ActiveView.Projection.MakeArrow(offsetStartPoint, offsetStartPoint, feedbackPlane, Projection.ArrowMode.circleArrow);
            while (owner != null && !(owner is Shell)) owner = owner.Owner as IGeoObject;
            shell = owner as Shell; // this is the shell to be modified
            this.frame = frame;
            facesToMove = new List<Face>();
            facesToKeep = new List<Face>();
            facesToMoveIsFixed = false;
        }

        public ParametricsDistanceActionOld(IEnumerable<Face> facesToMove, IFrame frame)
        {
            this.frame = frame;
            this.facesToMove = new List<Face>(facesToMove);
            this.facesToKeep = new List<Face>();
            facesToMoveIsFixed = true;
            shell = this.facesToMove[0].Owner as Shell;
            distanceFromHere = null;
        }

        public ParametricsDistanceActionOld(IEnumerable<Face> facesToMove, Line axis, IFrame frame)
        {
            this.frame = frame;
            this.facesToMove = new List<Face>(facesToMove);
            facesToMoveIsFixed = true;
            this.facesToKeep = new List<Face>();
            axisToMove = axis;
            shell = this.facesToMove[0].Owner as Shell;

            distanceToHere = axis;
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
        public ParametricsDistanceActionOld(Edge fromHere, Edge toHere, Line feedback, Plane plane, IFrame frame)
        {
            distanceFromHere = fromHere;
            distanceToHere = toHere;
            offsetStartPoint = feedback.StartPoint;
            offsetFeedBack = frame.ActiveView.Projection.MakeArrow(feedback.StartPoint, feedback.EndPoint, plane, Projection.ArrowMode.circleArrow);
            feedbackPlane = plane;
            originalOffset = feedback.EndPoint - feedback.StartPoint;
            shell = fromHere.PrimaryFace.Owner as Shell;
            facesToMove = new List<Face>();
            facesToKeep = new List<Face>();
            if (toHere != null)
            {
                if (!toHere.PrimaryFace.Surface.IsExtruded(originalOffset)) facesToMove.Add(toHere.PrimaryFace);
                if (!toHere.SecondaryFace.Surface.IsExtruded(originalOffset)) facesToMove.Add(toHere.SecondaryFace);
                facesToMoveIsFixed = true;
            }
            if (fromHere != null)
            {
                if (!fromHere.PrimaryFace.Surface.IsExtruded(originalOffset)) facesToKeep.Add(fromHere.PrimaryFace);
                if (!fromHere.SecondaryFace.Surface.IsExtruded(originalOffset)) facesToKeep.Add(fromHere.SecondaryFace);
            }
        }
        public ParametricsDistanceActionOld(IEnumerable<Face> part1, IEnumerable<Face> part2, GeoPoint point1, GeoPoint point2, IFrame frame)
        {
            //distanceFromHere = fromHere;
            //distanceToHere = toHere;
            offsetStartPoint = point1;
            offsetFeedBack = frame.ActiveView.Projection.MakeArrow(point1, point2, frame.ActiveView.Projection.DrawingPlane, Projection.ArrowMode.circleArrow);
            feedbackPlane = frame.ActiveView.Projection.DrawingPlane;
            originalOffset = point2 - point1;
            facesToMove = new List<Face>(part1);
            facesToKeep = new List<Face>(part2);
            shell = facesToMove[0].Owner as Shell;
            facesToMoveIsFixed = true;
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
                    frame.SetAction(this); // this is the way this action comes to life
                    return true;
            }
            return false;
        }
        public override void OnSetAction()
        {
            base.TitleId = "Constr.Parametrics.DistanceTo";
            if (offsetFeedBack != null) FeedBack.AddSelected(offsetFeedBack);
            if (facesToMove != null) FeedBack.AddSelected(facesToMove);
            base.ActiveObject = shell.Clone();
            if (shell.Layer != null) shell.Layer.Transparency = 128;
            List<InputObject> actionInputs = new List<InputObject>();
            if (distanceToHere == null)
            {
                if (facesToMoveIsFixed) toObjectInput = new GeoObjectInput("DistanceTo.FeatureObject");
                else toObjectInput = new GeoObjectInput("DistanceTo.ToObject");
                toObjectInput.FacesOnly = true;
                toObjectInput.EdgesOnly = true;
                toObjectInput.Points = true;
                toObjectInput.MouseOverGeoObjectsEvent += ToObject_MouseOverGeoObjectsEvent;
                toObjectInput.GeoObjectSelectionChangedEvent += ToObject_GeoObjectSelectionChangedEvent;
                actionInputs.Add(toObjectInput);
            }
            if (distanceFromHere == null)
            {
                fromObjectInput = new GeoObjectInput("DistanceTo.FromObject");
                fromObjectInput.FacesOnly = true;
                fromObjectInput.EdgesOnly = true;
                fromObjectInput.Points = true;
                fromObjectInput.MouseOverGeoObjectsEvent += FromObject_MouseOverGeoObjectsEvent;
                fromObjectInput.GeoObjectSelectionChangedEvent += ToObject_GeoObjectSelectionChangedEvent;
                actionInputs.Add(fromObjectInput);
            }
            distanceInput = new LengthInput("DistanceTo.Distance");
            distanceInput.GetLengthEvent += DistanceInput_GetLengthEvent;
            distanceInput.SetLengthEvent += DistanceInput_SetLengthEvent;
            actionInputs.Add(distanceInput);

            moreForwardFacesInput = new GeoObjectInput("DistanceTo.MoreForwardObjects");
            moreForwardFacesInput.MultipleInput = true;
            moreForwardFacesInput.FacesOnly = true;
            moreForwardFacesInput.Optional = true;
            moreForwardFacesInput.MouseOverGeoObjectsEvent += new GeoObjectInput.MouseOverGeoObjectsDelegate(OnMouseOverForwardFaces);
            actionInputs.Add(moreForwardFacesInput);

            moreBackwardFacesInput = new GeoObjectInput("DistanceTo.MoreBackwardObjects");
            moreBackwardFacesInput.MultipleInput = true;
            moreBackwardFacesInput.FacesOnly = true;
            moreBackwardFacesInput.Optional = true;
            moreBackwardFacesInput.MouseOverGeoObjectsEvent += new GeoObjectInput.MouseOverGeoObjectsDelegate(OnMouseOverBackwardFaces);
            actionInputs.Add(moreBackwardFacesInput);

            modeInput = new MultipleChoiceInput("DistanceTo.Mode", "DistanceTo.Mode.Values", 0);
            modeInput.SetChoiceEvent += ModeInput_SetChoiceEvent;
            actionInputs.Add(modeInput);
            //modeInput.GetChoiceEvent += ModeInput_GetChoiceEvent;

            SeparatorInput separator = new SeparatorInput("DistanceTo.AssociateParametric");
            actionInputs.Add(separator);
            nameInput = new StringInput("DistanceTo.ParametricsName");
            nameInput.SetStringEvent += NameInput_SetStringEvent;
            nameInput.GetStringEvent += NameInput_GetStringEvent;
            nameInput.Optional = true;
            actionInputs.Add(nameInput);

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
                if (geoObject is Face face && !face.Surface.IsExtruded(originalOffset)) faces.Add(face);
            }
            if (faces.Count > 0)
            {
                if (up)
                {
                    foreach (Face face in faces)
                    {
                        if (moreFaces.Contains(face)) moreFaces.Remove(face);
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
            return OnMouseOverMoreFaces(sender, theGeoObjects, up, moreBackwardFaces);
        }

        private bool OnMouseOverForwardFaces(GeoObjectInput sender, IGeoObject[] theGeoObjects, bool up)
        {
            return OnMouseOverMoreFaces(sender, theGeoObjects, up, moreForwardFaces);
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
            if (facesToMove.Count > 0 && (facesToKeep.Count > 0 || axisToMove != null))
            {
                FeedBack.ClearSelected();
                GeoPoint startPoint = offsetStartPoint;
                GeoPoint endPoint = startPoint + originalOffset + currentOffset;
                switch (mode)
                {
                    case Mode.forward:
                        startPoint = offsetStartPoint;
                        endPoint = startPoint + originalOffset + currentOffset;
                        offsetFeedBack = Frame.ActiveView.Projection.MakeArrow(startPoint, endPoint, feedbackPlane, Projection.ArrowMode.circleArrow);
                        break;
                    case Mode.backward:
                        startPoint = offsetStartPoint + originalOffset;
                        endPoint = startPoint - originalOffset - currentOffset;
                        offsetFeedBack = Frame.ActiveView.Projection.MakeArrow(startPoint, endPoint, feedbackPlane, Projection.ArrowMode.circleArrow);
                        break;
                    case Mode.symmetric:
                        startPoint = offsetStartPoint + 0.5 * originalOffset;
                        offsetFeedBack = Frame.ActiveView.Projection.MakeArrow(startPoint, startPoint + 0.5 * (originalOffset + currentOffset), feedbackPlane, Projection.ArrowMode.circleArrow);
                        offsetFeedBack.AddRange(Frame.ActiveView.Projection.MakeArrow(startPoint, startPoint - 0.5 * (originalOffset + currentOffset), feedbackPlane, Projection.ArrowMode.circleArrow));
                        break;
                }
                offsetFeedBack.AddRange(facesToMove);
                offsetFeedBack.AddRange(moreForwardFaces);
                FeedBack.AddSelected(offsetFeedBack);
                Shell sh = null;
                for (int m = 0; m <= 1; m++)
                {   // first try without moving connected faces, if this yields no result, try with moving connected faced
                    Parametric parametric = new Parametric(shell);
                    Dictionary<Face, GeoVector> allFacesToMove = new Dictionary<Face, GeoVector>();
                    switch (mode)
                    {
                        case Mode.forward:
                            foreach (Face face in Extensions.Combine(facesToMove, moreForwardFaces))
                            {
                                allFacesToMove[face] = currentOffset;
                            }
                            foreach (Face face in Extensions.Combine(facesToKeep, moreBackwardFaces))
                            {
                                allFacesToMove[face] = GeoVector.NullVector;
                            }
                            break;
                        case Mode.symmetric:
                            foreach (Face face in Extensions.Combine(facesToMove, moreForwardFaces))
                            {
                                allFacesToMove[face] = 0.5 * currentOffset;
                            }
                            foreach (Face face in Extensions.Combine(facesToKeep, moreBackwardFaces))
                            {
                                allFacesToMove[face] = -0.5 * currentOffset;
                            }
                            break;
                        case Mode.backward:
                            foreach (Face face in Extensions.Combine(facesToKeep, moreBackwardFaces))
                            {
                                allFacesToMove[face] = -currentOffset;
                            }
                            foreach (Face face in Extensions.Combine(facesToMove, moreForwardFaces))
                            {
                                allFacesToMove[face] = GeoVector.NullVector;
                            }
                            break;
                    }
                    parametric.MoveFaces(allFacesToMove, currentOffset, m == 1);
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
                            object fromHere = null, toHere = null;
                            if (distanceFromHere is Face fromFace) fromHere = faceDict[fromFace];
                            if (distanceFromHere is Edge fromEdge) fromHere = edgeDict[fromEdge];
                            if (distanceFromHere is Vertex fromVertex) fromHere = vertexDict[fromVertex];
                            if (distanceToHere is Face toFace) toHere = faceDict[toFace];
                            if (distanceToHere is Edge toEdge) toHere = edgeDict[toEdge];
                            if (distanceToHere is Vertex toVertex) toHere = vertexDict[toVertex];
                            if (mode == Mode.backward)
                            {
                                parametricProperty = new ParametricDistanceProperty("", Extensions.LookUp(Extensions.Combine(facesToMove, moreForwardFaces), faceDict),
                                    Extensions.LookUp(Extensions.Combine(facesToKeep, moreBackwardFaces), faceDict),
                                    parametric.GetAffectedObjects(), pmode, toHere, fromHere);
                            }
                            else
                            {
                                parametricProperty = new ParametricDistanceProperty("", Extensions.LookUp(Extensions.Combine(facesToKeep, moreBackwardFaces), faceDict),
                                    Extensions.LookUp(Extensions.Combine(facesToMove, moreForwardFaces), faceDict),
                                    parametric.GetAffectedObjects(), pmode, fromHere, toHere);
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
            if (facesToMove.Count > 0 && (facesToKeep.Count > 0 || axisToMove != null))
            {
                currentOffset = originalOffset;
                currentOffset.Length = length - originalOffset.Length;
                return Refresh();
            }
            return false;
        }

        private double DistanceInput_GetLengthEvent()
        {
            return (originalOffset + currentOffset).Length;
        }

        private bool SetFacesAndOffset(bool testOnly, object distanceFromHere, object distanceToHere)
        {
            if (distanceFromHere == null || distanceToHere == null) return true; // because this means, you can use the object
            List<Face> fromHere = new List<Face>();
            List<Face> toHere = new List<Face>();
            Vertex vtx1 = distanceFromHere as Vertex;
            Vertex vtx2 = distanceToHere as Vertex;
            Edge edg1 = distanceFromHere as Edge;
            Edge edg2 = distanceToHere as Edge;
            Face fc1 = distanceFromHere as Face;
            Face fc2 = distanceToHere as Face;
            ICurve crv1 = distanceFromHere as ICurve;
            ICurve crv2 = distanceToHere as ICurve;
            originalOffset = GeoVector.NullVector;
            if (distanceToHere is GeoObject.Point pnt && vtx2 == null)
            {
                vtx2 = new Vertex(pnt.Location);
            }
            if (vtx1 != null && vtx2 != null)
            {
                originalOffset = vtx1.Position - vtx2.Position;
            }
            else if ((vtx1 != null && edg2 != null) || (vtx2 != null && edg1 != null))
            {
                Vertex vtx;
                Edge edg;
                List<Face> lv, le;
                if (vtx1 != null) { vtx = vtx1; edg = edg2; lv = fromHere; le = toHere; }
                else { vtx = vtx2; edg = edg1; lv = toHere; le = fromHere; }
                double cpos = edg.Curve3D.PositionOf(vtx.Position);
                originalOffset = vtx.Position - edg.Curve3D.PointAt(cpos);
                if (vtx1 == null) originalOffset.Reverse();
            }
            else if ((vtx1 != null && fc2 != null) || (vtx2 != null && fc1 != null))
            {
                Vertex vtx;
                Face fc;
                List<Face> lv, le;
                if (vtx1 != null) { vtx = vtx1; fc = fc2; lv = fromHere; le = toHere; }
                else { vtx = vtx2; fc = fc1; lv = toHere; le = fromHere; }
                GeoPoint2D[] pfs = fc.Surface.PerpendicularFoot(vtx.Position);
                for (int i = 0; i < pfs.Length; i++)
                {
                    if (fc.Contains(ref pfs[i], true))
                    {
                        originalOffset = vtx.Position - fc.Surface.PointAt(pfs[i]);
                        if (vtx1 == null) originalOffset.Reverse();
                        break;
                    }
                }
            }
            else if ((edg1 != null || crv1 != null) && (edg2 != null || crv2 != null))
            {
                double pos1 = 0.5, pos2 = 0.5;
                if (crv1 == null) crv1 = edg1.Curve3D;
                if (crv2 == null) crv2 = edg2.Curve3D;
                if (crv1 is Line l1 && crv2 is Line l2)
                {
                    if (Precision.SameDirection(l1.StartDirection, l2.StartDirection, false))
                    {
                        double u = crv1.PositionOf(crv2.StartPoint);
                        originalOffset = crv2.StartPoint - crv1.PointAt(u);
                        offsetStartPoint = crv1.PointAt(u);
                    }
                    else
                    {
                        Geometry.DistLL(l1.StartPoint, l1.StartDirection, l2.StartPoint, l2.StartDirection, out double par1, out double par2);
                        originalOffset = l2.PointAt(par2) - l1.PointAt(par1);
                        offsetStartPoint = l1.PointAt(par1);
                    }
                }
                else if (crv1.GetPlanarState() == PlanarState.Planar && crv2.GetPlanarState() == PlanarState.Planar)
                {   // probably parallel planes, so newton could find anything 
                    GeoPoint foot = crv1.GetPlane().ToLocal(crv2.StartPoint);
                    originalOffset = crv2.StartPoint - foot;
                    offsetStartPoint = foot;
                }
                else if (Curves.NewtonMinDist(crv1, ref pos1, crv2, ref pos2))
                {
                    originalOffset = crv2.PointAt(pos2) - crv1.PointAt(pos1);
                    offsetStartPoint = crv1.PointAt(pos1);
                }
                // else no distance between two curves, maybe check some more cases, which make a usable offset
            }
            else if ((edg1 != null && fc2 != null) || (edg2 != null && fc1 != null))
            {
                Edge edg;
                Face fc;
                List<Face> lv, le;
                if (edg1 != null) { edg = edg1; fc = fc2; lv = fromHere; le = toHere; }
                else { edg = edg2; fc = fc1; lv = toHere; le = fromHere; }
                if (fc.Surface is PlaneSurface pls && edg.Curve3D is Line line)
                {
                    if (Precision.IsPerpendicular(pls.Normal, line.StartDirection, false))
                    {

                        GeoPoint pl = line.PointAt(0.5);
                        GeoPoint2D[] fps = pls.PerpendicularFoot(pl);
                        if (fps.Length == 1)
                        {
                            originalOffset = pls.PointAt(fps[0]) - pl;
                        }
                    }
                }
                else if (fc.Surface.GetExtremePositions(fc.Domain, edg.Curve3D, out List<Tuple<double, double, double>> positions) > 0)
                {
                    originalOffset = edg.Curve3D.PointAt(positions[0].Item3) - fc.Surface.PointAt(new GeoPoint2D(positions[0].Item1, positions[0].Item2));
                }
                if (edg1 != null) originalOffset.Reverse();
            }
            else if (fc1 != null && fc2 != null)
            {
                if (fc1.Surface.GetExtremePositions(fc1.Domain, fc2.Surface, fc2.Domain, out List<Tuple<double, double, double, double>> extremePositions) > 0)
                {
                    double mindist = double.MaxValue;
                    for (int i = 0; i < extremePositions.Count; i++)
                    {
                        GeoPoint2D uv1 = new GeoPoint2D(extremePositions[i].Item1, extremePositions[i].Item2);
                        GeoPoint2D uv2 = new GeoPoint2D(extremePositions[i].Item3, extremePositions[i].Item4);
                        GeoVector diff = fc1.Surface.PointAt(uv1) - fc2.Surface.PointAt(uv2);
                        if (diff.Length < mindist)
                        {
                            mindist = diff.Length;
                            originalOffset = diff;
                        }
                    }
                }
            }
            else if (axisToMove != null)
            {
                if (edg2 != null) crv2 = edg2.Curve3D;
                if (crv2 != null)
                {
                    if (crv2 is Line line)
                    {
                        if (!Precision.SameDirection(line.StartDirection, axisToMove.StartDirection, false))
                        {
                            if (Geometry.DistLL(line.StartPoint, line.StartDirection, axisToMove.StartPoint, axisToMove.StartDirection, out double par1, out double par2) != double.MaxValue)
                            {
                                offsetStartPoint = line.StartPoint + par1 * line.StartDirection;
                                originalOffset = (axisToMove.StartPoint + par2 * axisToMove.StartDirection) - offsetStartPoint;
                            }
                        }
                        else
                        {
                            offsetStartPoint = Geometry.DropPL(axisToMove.StartPoint, line.StartPoint, line.StartDirection);
                            originalOffset = axisToMove.StartPoint - offsetStartPoint;
                        }
                    }
                    else if (crv2.GetPlanarState() == PlanarState.Planar)
                    {
                        Plane pln = edg2.Curve3D.GetPlane();
                        if (Precision.IsPerpendicular(axisToMove.StartDirection, pln.Normal, false))
                        {
                            offsetStartPoint = pln.ToLocal(axisToMove.PointAt(0.5));
                            originalOffset = axisToMove.PointAt(0.5) - offsetStartPoint;
                        }
                    }

                }
                if (vtx2 != null)
                {
                    GeoPoint p0 = Geometry.DropPL(vtx2.Position, axisToMove.StartPoint, axisToMove.StartDirection);
                    offsetStartPoint = vtx2.Position;
                    originalOffset = p0 - vtx2.Position;
                }
            }

            if (originalOffset.Length < Precision.eps)
            {
                if (!testOnly)
                {
                    facesToKeep.Clear();
                    if (!facesToMoveIsFixed) facesToMove.Clear();
                }
                return false;
            }
            FeedBack.ClearSelected();
            GeoPoint startPoint = offsetStartPoint;
            GeoPoint endPoint = startPoint + originalOffset;
            if (feedbackPlane.IsValid())
            {
                offsetFeedBack = frame.ActiveView.Projection.MakeArrow(startPoint, endPoint, feedbackPlane, Projection.ArrowMode.circleArrow);
                FeedBack.AddSelected(offsetFeedBack);
            }
            if (vtx1 != null)
            {
                foreach (Face fc in vtx1.InvolvedFaces)
                {
                    if (!fc.Surface.IsExtruded(originalOffset)) fromHere.Add(fc);
                }
            }
            if (vtx2 != null)
            {
                foreach (Face fc in vtx2.InvolvedFaces)
                {
                    if (!fc.Surface.IsExtruded(originalOffset)) toHere.Add(fc);
                }
            }
            if (edg1 != null)
            {
                if (!edg1.PrimaryFace.Surface.IsExtruded(originalOffset)) fromHere.Add(edg1.PrimaryFace);
                if (!edg1.SecondaryFace.Surface.IsExtruded(originalOffset)) fromHere.Add(edg1.SecondaryFace);
            }
            if (edg2 != null)
            {
                if (!edg2.PrimaryFace.Surface.IsExtruded(originalOffset)) toHere.Add(edg2.PrimaryFace);
                if (!edg2.SecondaryFace.Surface.IsExtruded(originalOffset)) toHere.Add(edg2.SecondaryFace);
            }
            if (fc1 != null && !fc1.Surface.IsExtruded(originalOffset)) fromHere.Add(fc1);
            if (fc2 != null && !fc2.Surface.IsExtruded(originalOffset)) toHere.Add(fc2);
            if (!testOnly)
            {
                if (!facesToMoveIsFixed) facesToMove = toHere; // facesToMove is already set in the constructor
                facesToKeep = fromHere;
            }
            if (axisToMove != null) return !originalOffset.IsNullVector();
            else return fromHere.Count > 0 && (toHere.Count > 0 || facesToMove.Count > 0);
        }
        private void ToObject_GeoObjectSelectionChangedEvent(GeoObjectInput sender, IGeoObject selectedGeoObject)
        {
            bool ok = SetFacesAndOffset(false, distanceFromHere, selectedGeoObject);
            if (ok)
            {
                distanceToHere = selectedGeoObject;
                currentOffset = GeoVector.NullVector;
                distanceInput.ForceValue(originalOffset.Length);
            }
            //if (feedbackIndex >= 0) FeedBack.RemoveSelected(feedbackIndex);
            //feedbackIndex = FeedBack.AddSelected(otherObject);
        }

        private bool ToObject_MouseOverGeoObjectsEvent(GeoObjectInput sender, IGeoObject[] geoObjects, bool up)
        {   // we need to implement more cases here, resulting in faceToMove, faceToKeep (maybe null) and a reference point from where to calculate foot-points for the offset vector

            Projection.PickArea pa = CurrentMouseView.Projection.GetPickSpace(new Rectangle(sender.currentMousePoint.X - 5, sender.currentMousePoint.Y - 5, 10, 10));
            for (int i = 0; i < geoObjects.Length; i++)
            {
                double z = geoObjects[i].Position(pa.FrontCenter, pa.Direction, CurrentMouseView.Projection.Precision);
                object candidate = geoObjects[i];
                if (geoObjects[i].Owner is Edge edg) candidate = edg;
                bool ok = true;
                if (facesToMoveIsFixed)
                {   // only allow object from facesToMove to be the target for the movement (position feature)
                    HashSet<Edge> edges = new HashSet<Edge>();
                    foreach (Face f in facesToMove) edges.UnionWith(f.Edges);
                    ok = (candidate is Face face && facesToMove.Contains(face)) || (candidate is Edge edge && edges.Contains(edge)) ||
                        (candidate is ICurve && (candidate as IGeoObject).UserData.GetData("CADability.AxisOf") is Face face1 && facesToMove.Contains(face1));
                }
                if (ok)
                {
                    ok = SetFacesAndOffset(!up, distanceFromHere, candidate);
                    if (up && ok)
                    {
                        distanceToHere = candidate;
                        currentOffset = GeoVector.NullVector;
                        distanceInput.ForceValue(originalOffset.Length);
                        List<IGeoObject> currentTargets = new List<IGeoObject>();
                        IGeoObject[] ct = sender.GetGeoObjects();
                        if (ct != null) currentTargets.AddRange(ct);
                        currentTargets.Add(geoObjects[i]);
                        sender.SetGeoObject(currentTargets.ToArray(), geoObjects[i]);
                    }
                }
                if (ok)
                {
                    return true;
                }
            }
            return false;
        }

        private void FromObject_GeoObjectSelectionChangedEvent(GeoObjectInput sender, IGeoObject selectedGeoObject)
        {
            bool ok = SetFacesAndOffset(false, selectedGeoObject, distanceToHere);
            if (ok)
            {
                distanceFromHere = selectedGeoObject;
                currentOffset = GeoVector.NullVector;
                distanceInput.ForceValue(originalOffset.Length);
            }
        }
        private bool FromObject_MouseOverGeoObjectsEvent(GeoObjectInput sender, IGeoObject[] geoObjects, bool up)
        {   // we need to implement more cases here, resulting in faceToMove, faceToKeep (maybe null) and a reference point from where to calculate foot-points for the offset vector

            Projection.PickArea pa = CurrentMouseView.Projection.GetPickSpace(new Rectangle(sender.currentMousePoint.X - 5, sender.currentMousePoint.Y - 5, 10, 10));
            for (int i = 0; i < geoObjects.Length; i++)
            {
                double z = geoObjects[i].Position(pa.FrontCenter, pa.Direction, CurrentMouseView.Projection.Precision);
                object candidate = geoObjects[i];
                if (geoObjects[i].Owner is Edge edg) candidate = edg;
                bool ok = SetFacesAndOffset(!up, candidate, distanceToHere);
                if (up && ok)
                {
                    distanceFromHere = candidate;
                    if (!feedbackPlane.IsValid() && geoObjects[i] is ICurve crv && crv.GetPlanarState() == PlanarState.Planar) feedbackPlane = crv.GetPlane();
                    currentOffset = GeoVector.NullVector;
                    distanceInput.ForceValue(originalOffset.Length);
                    List<IGeoObject> currentTargets = new List<IGeoObject>();
                    IGeoObject[] ct = sender.GetGeoObjects();
                    if (ct != null) currentTargets.AddRange(ct);
                    currentTargets.Add(geoObjects[i]);
                    sender.SetGeoObject(currentTargets.ToArray(), geoObjects[i]);
                }
                if (ok)
                {
                    return true;
                }
            }
            return false;
        }
    }
}