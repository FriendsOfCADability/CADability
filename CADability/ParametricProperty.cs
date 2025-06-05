using System;
using System.Collections.Generic;
using System.Text;
using CADability;
using System.Xml.Linq;
using CADability.GeoObject;
using CADability.UserInterface;
using CADability.Actions;
using MathNet.Numerics;
using System.Linq;

namespace CADability
{
    /// <summary>
    /// The ParametricProperty is the base class for all parametric properties of a shell. The different derived classes know which parts of the shell
    /// (Faces, Edges, Vertices) define the property and what to do, when the value of that property changes.
    /// For the <see cref="ParametricProperty.Execute(Parametric)"/> and <see cref="ParametricProperty.GetResult(Parametric)"/> we need a <see cref="Parametric"/> object,
    /// which can handle succesive parametric modifications on a clone of the shell.
    /// The derived classes (ParametricXxxProperty) are typically created by an Action (ParametricXxxAction), which allows to specify faces, Edges and 
    /// Vertices of the shell and other data.
    /// </summary>
    public abstract class ParametricProperty : IJsonSerialize
    {
        public string Name { get; set; }
        public bool Preserve { get; set; }
        public abstract double Value { get; set; } // the value of the property is kept here
        public ParametricProperty(string name)
        {
            Name = name;
        }
        /// <summary>
        /// When the shell is beeing modified, we have the chance here to update the parametric property if necessary
        /// </summary>
        /// <param name="m"></param>
        public abstract void Modify(ModOp m);
        /// <summary>
        /// When the shell is cloned, we need to replace the faces, edges and vertices with their clones
        /// </summary>
        /// <param name="clonedFaces"></param>
        /// <param name="clonedEdges"></param>
        /// <param name="clonedVertices"></param>
        /// <returns></returns>
        public abstract ParametricProperty Clone(Dictionary<Face, Face> clonedFaces, Dictionary<Edge, Edge> clonedEdges, Dictionary<Vertex, Vertex> clonedVertices);
        public ParametricProperty Clone(Parametric parametric)
        {
            parametric.GetDictionaries(out Dictionary<Face, Face> faceDict, out Dictionary<Edge, Edge> edgeDict, out Dictionary<Vertex, Vertex> vertexDict);
            return Clone(faceDict, edgeDict, vertexDict);
        }
        /// <summary>
        /// Execute the parametrics. This will change the shell and the resulting shell may be inconsistent. But maybe further applications of other parametrics to the shell
        /// make it consistent again
        /// </summary>
        /// <param name="parametrics"></param>
        public abstract bool Execute(Parametric parametrics);
        /// <summary>
        /// Returns the object on which serves as a anchorpoint for the operation
        /// </summary>
        /// <returns></returns>
        public abstract object GetAnchor();
        /// <summary>
        /// Returns the faces, which will be modified by this parametric property
        /// </summary>
        /// <returns></returns>
        public abstract IEnumerable<object> GetAffectedObjects();
        public Shell GetResult(Parametric parametrics)
        {
            return parametrics.Result();
        }
        /// <summary>
        /// Returns a list of objects that can be used as a feedback for this property (usually an arrow)
        /// </summary>
        /// <param name="projection"></param>
        /// <returns></returns>
        public abstract GeoObjectList GetFeedback(Projection projection);
        public IPropertyEntry PropertyEntry { get; set; }
        public virtual void GetObjectData(IJsonWriteData data)
        {
            data.AddProperty("Name", Name);
            data.AddProperty("Preserve", Preserve);
        }
        public virtual void SetObjectData(IJsonReadData data)
        {
            Name = data.GetStringProperty("Name");
            Preserve = data.GetPropertyOrDefault<bool>("Preserve");
        }

        protected List<object> CloneWithDictionary(List<object> list, Dictionary<Face, Face> clonedFaces, Dictionary<Edge, Edge> clonedEdges, Dictionary<Vertex, Vertex> clonedVertices)
        {
            List<object> clonedObjects = new List<object>();
            for (int i = 0; i < list.Count; i++)
            {
                if (list[i] is Face face && clonedFaces.TryGetValue(face, out Face clonedFace)) clonedObjects.Add(clonedFace);
                if (list[i] is Edge edge && clonedEdges.TryGetValue(edge, out Edge clonedEdge)) clonedObjects.Add(clonedEdge);
                if (list[i] is Vertex vtx && clonedVertices.TryGetValue(vtx, out Vertex clonedVertex)) clonedObjects.Add(clonedVertex);
                if (list[i] is Line line)
                {   // this must be an axis
                    Face faceOfAxis = line.UserData.GetData("CADability.AxisOf") as Face;
                    if (faceOfAxis != null && faceOfAxis.Surface is ISurfaceOfRevolution sr && clonedFaces.TryGetValue(faceOfAxis, out Face clonedFaceOfAxis))
                    {
                        Line ax = clonedFaceOfAxis.GetAxisOfRevolution(); // UserData already set
                        clonedObjects.Add(ax);
                    }
                }
            }
            return clonedObjects;
        }
        protected List<Face> CloneFacesWithDictionary(List<Face> list, Dictionary<Face, Face> clonedFaces)
        {
            List<Face> substFaces = new List<Face>();
            for (int i = 0; i < list.Count; i++)
            {
                if (clonedFaces.TryGetValue(list[i], out Face cf)) substFaces.Add(cf);
            }
            return substFaces;
        }

        protected void ModifyAxis(List<object> objects, ModOp m)
        {
            foreach (object obj in objects)
            {
                if (obj is Line line)
                {   // this must be an axis
                    Face faceOfAxis = line.UserData.GetData("CADability.AxisOf") as Face;
                    if (faceOfAxis != null)
                    {
                        Line ax = faceOfAxis.GetAxisOfRevolution(); // UserData already set
                        line.CopyGeometry(ax);
                    }
                }
            }
        }
    }
    public class ParametricDistanceProperty : ParametricProperty, IJsonSerialize, IJsonSerializeDone
    {
        private List<Face> facesToKeep, facesToMove; // if mode is symmetric, both faces will be moved
        private List<object> affectedObjects; // faces which are affected by this parametric
        private object fromHere, toHere; // vertices, edges or faces. they specify the direction, in which to move and the position of the arrow

        [Flags]
        public enum Mode
        {
            connected = 1, // call parametric.MoveFaces with moveConnected==true
            symmetric = 2, // use facesToKeep with -0.5 and facesToMove with +0.5 factor to the direction
            fromAxis = 4, // fromHere is a face of which the axis is used
            toAxis = 8, // toHere is a face of which the axis is used
            fromFurthest = 16, // fromHere is a face or edge (curve). Normally we use the closest connection to toHere, with this flag set we use the furthest
            toFurthest = 32,// toHere is a face or edge (curve). Normally we use the closest connection to toHere, with this flag set we use the furthest
        }
        private Mode mode;

        public ParametricDistanceProperty(string name, IEnumerable<Face> facesToKeep, IEnumerable<Face> facesToMove, IEnumerable<object> affectedObjects, Mode mode, object fromHere, object toHere) : base(name)
        {
            this.facesToMove = new List<Face>(facesToMove);
            this.facesToKeep = new List<Face>(facesToKeep);
            this.affectedObjects = new List<object>(affectedObjects);
            this.mode = mode;
            this.fromHere = fromHere;
            this.toHere = toHere;
            GetDistanceVector(out GeoPoint startPoint, out GeoPoint endPoint);
            Value = startPoint | endPoint;
        }

        public override ParametricProperty Clone(Dictionary<Face, Face> clonedFaces, Dictionary<Edge, Edge> clonedEdges, Dictionary<Vertex, Vertex> clonedVertices)
        {
            List<Face> facesToKeepCloned = new List<Face>();
            List<Face> facesToMoveCloned = new List<Face>();
            List<object> affectedObjectsCloned = new List<object>();
            object fromHereCloned, toHereCloned;
            for (int i = 0; i < facesToKeep.Count; i++)
            {
                if (clonedFaces.TryGetValue(facesToKeep[i], out Face clone)) facesToKeepCloned.Add(clone);
            }
            for (int i = 0; i < facesToMove.Count; i++)
            {
                if (clonedFaces.TryGetValue(facesToMove[i], out Face clone)) facesToMoveCloned.Add(clone);
            }
            for (int i = 0; i < affectedObjects.Count; i++)
            {
                if (affectedObjects[i] is Face face && clonedFaces.TryGetValue(face, out Face clonedFace)) affectedObjectsCloned.Add(clonedFace);
                if (affectedObjects[i] is Edge edge && clonedEdges.TryGetValue(edge, out Edge clonedEdge)) affectedObjectsCloned.Add(clonedEdge);
                if (affectedObjects[i] is Vertex vtx && clonedVertices.TryGetValue(vtx, out Vertex clonedVertex)) affectedObjectsCloned.Add(clonedVertex);
            }
            {
                if (fromHere is GeoPoint pnt) fromHereCloned = pnt;
                else if (fromHere is Vertex vtx) fromHereCloned = clonedVertices[vtx];
                else if (fromHere is Edge edg) fromHereCloned = clonedEdges[edg];
                else if (fromHere is Face face) fromHereCloned = clonedFaces[face];
                else fromHereCloned = null; // should not happen
            }
            {
                if (toHere is GeoPoint pnt) toHereCloned = pnt;
                else if (toHere is Vertex vtx) toHereCloned = clonedVertices[vtx];
                else if (toHere is Edge edg) toHereCloned = clonedEdges[edg];
                else if (toHere is Face face) toHereCloned = clonedFaces[face];
                else toHereCloned = null; // should not happen
            }
            return new ParametricDistanceProperty(Name, facesToKeepCloned, facesToMoveCloned, affectedObjectsCloned, mode, fromHereCloned, toHereCloned);
        }
        public override void Modify(ModOp m)
        {   // all referred objects like facesToMove or fromHere are part of the shell and already modified
            // with fromHere and toHere as GeoPoints we need to modify these points along with the modification of the Shell containing this parametric
            if (fromHere is GeoPoint point1)
            {
                fromHere = m * point1;
            }
            if (toHere is GeoPoint point2)
            {
                toHere = m * point2;
            }
            if (!ExtrusionDirection.IsNullVector()) ExtrusionDirection = m * ExtrusionDirection; // change the direction, e.g. after a rotation of the shell
        }
        private double currentValue;
        public override double Value
        {
            get
            {
                GetDistanceVector(out GeoPoint startPoint, out GeoPoint endPoint);
                if (!ExtrusionDirection.IsNullVector())
                {
                    Plane pln = new Plane(GeoPoint.Origin, ExtrusionDirection); // a plane perpendicular to this direction
                    double d1 = pln.Distance(startPoint);
                    double d2 = pln.Distance(endPoint);
                    // return Math.Abs(d1 + d2); not sure whether to use abs here
                    return d1 - d2;
                }
                return startPoint | endPoint;
            }
            set
            {
                currentValue = value;
            }
        }
        /// <summary>
        /// if != NullVector, this is the direction of the extrusion. This is used by the distance calculation.
        /// </summary>
        public GeoVector ExtrusionDirection { get; set; } = GeoVector.NullVector;
        public override bool Execute(Parametric parametric)
        {
            GetDistanceVector(out GeoPoint startPoint, out GeoPoint endPoint);
            GeoVector delta = (endPoint - startPoint);
            if (!ExtrusionDirection.IsNullVector())
            {
                Plane pln = new Plane(GeoPoint.Origin, ExtrusionDirection); // a plane perpendicular to this direction
                double d1 = pln.Distance(startPoint);
                double d2 = pln.Distance(endPoint);
                delta = (d1 - d2) * ExtrusionDirection.Normalized;
            }
            double diff = currentValue - delta.Length;
            delta = diff * delta.Normalized;
            Dictionary<Face, GeoVector> facesToAffect = new Dictionary<Face, GeoVector>();
            if (mode.HasFlag(Mode.symmetric))
            {
                foreach (Face face in facesToKeep)
                {
                    facesToAffect[face] = -0.5 * delta;
                }
                foreach (Face face in facesToMove)
                {
                    facesToAffect[face] = 0.5 * delta;
                }
            }
            else
            {
                foreach (Face face in facesToMove)
                {
                    facesToAffect[face] = delta;
                }
            }
            parametric.MoveFaces(facesToAffect, delta, mode.HasFlag(Mode.connected));
            return parametric.Apply(); // the result may be inconsistent, but maybe further parametric operations make it consistent again
        }
        private void GetDistanceVector(out GeoPoint startPoint, out GeoPoint endPoint)
        {
            startPoint = endPoint = GeoPoint.Invalid;
            if (fromHere is GeoPoint point1 && toHere is GeoPoint point2)
            {   // I think, all other cases should be eliminated
                startPoint = point1;
                endPoint = point2;
                return;
            }
            if (fromHere is Vertex vtx)
            {
                if (toHere is Vertex vtx2)
                {
                    startPoint = vtx.Position;
                    endPoint = vtx2.Position;
                    return;
                }
                else if (toHere is Edge edge2)
                {
                    double pos = edge2.Curve3D.PositionOf(vtx.Position);
                    if (pos >= -1e-6 && pos <= 1 + 1e-6)
                    {
                        startPoint = vtx.Position;
                        endPoint = edge2.Curve3D.PointAt(pos);
                        return;
                    }
                }
                else if (toHere is Face fc2)
                {
                    GeoPoint2D[] fps = fc2.Surface.PerpendicularFoot(vtx.Position);
                    if (fps != null && fps.Length > 0)
                    {
                        endPoint = startPoint = vtx.Position;
                        double minDist = double.MaxValue;
                        for (int i = 0; i < fps.Length; i++)
                        {
                            GeoPoint ep = fc2.Surface.PointAt(fps[i]);
                            double d = ep | startPoint;
                            if (d < minDist)
                            {
                                minDist = d;
                                endPoint = ep;
                            }
                        }
                        return;
                    }
                }
            }
            else if (fromHere is Edge edge)
            {
                if (toHere is Vertex vtx2)
                {
                    double pos = edge.Curve3D.PositionOf(vtx2.Position);
                    if (pos >= -1e-6 && pos <= 1 + 1e-6)
                    {
                        endPoint = vtx2.Position;
                        startPoint = edge.Curve3D.PointAt(pos);
                        return;
                    }
                }
                else if (toHere is Edge edge2)
                {
                    if (edge.Curve3D is Line l1 && edge2.Curve3D is Line l2)
                    {
                        if (Precision.SameDirection(l1.StartDirection, l2.StartDirection, false))
                        {   // two parallel lines
                            double spos = Geometry.LinePar(l1.StartPoint, l1.EndPoint, l2.StartPoint);
                            double epos = Geometry.LinePar(l1.StartPoint, l1.EndPoint, l2.EndPoint);
                            if (epos < spos) (spos, epos) = (epos, spos);
                            if (epos < 0)
                            {
                                spos = (epos + 0.0) / 2.0;
                                epos = 0.0;
                            }
                            else if (spos > 1)
                            {
                                epos = (spos + 1.0) / 2.0;
                                spos = 1.0;
                            }
                            else
                            {
                                spos = Math.Max(0.0, spos);
                                epos = Math.Min(epos, 1.0);
                            }
                            startPoint = Geometry.LinePos(l1.StartPoint, l1.EndPoint, (spos + epos) / 2.0);
                            endPoint = Geometry.DropPL(startPoint, l2.StartPoint, l2.EndPoint);
                            return;
                        }
                        else
                        {   // two skewed lines
                            Geometry.ConnectLines(l1.StartPoint, l1.StartDirection, l2.StartPoint, l2.StartDirection, out double pos1, out double pos2);
                            startPoint = Geometry.LinePos(l1.StartPoint, l1.EndPoint, pos1);
                            endPoint = Geometry.LinePos(l2.StartPoint, l2.EndPoint, pos2);
                            return;
                        }
                    }
                    // more cases ...
                }
                else if (toHere is Face fc2)
                {
                    // to fill out
                }
            }
            else if (fromHere is Face face)
            {
                // to fill out
            }

        }
        public override GeoObjectList GetFeedback(Projection projection)
        {
            GetDistanceVector(out GeoPoint startPoint, out GeoPoint endPoint);
            if (mode.HasFlag(Mode.symmetric))
            {
                GeoObjectList res = new GeoObjectList();
                GeoPoint middlePoint = new GeoPoint(startPoint, endPoint);
                res.AddRange(projection.MakeArrow(middlePoint, startPoint, projection.ProjectionPlane, Projection.ArrowMode.circleArrow));
                res.AddRange(projection.MakeArrow(middlePoint, endPoint, projection.ProjectionPlane, Projection.ArrowMode.circleArrow));
                return res;
            }
            else return projection.MakeArrow(startPoint, endPoint, projection.ProjectionPlane, Projection.ArrowMode.circleArrow);
        }
        protected ParametricDistanceProperty() : base("") { } // empty constructor for Json
        public override void GetObjectData(IJsonWriteData data)
        {
            base.GetObjectData(data);

            data.AddProperty("FacesToMove", facesToMove);
            data.AddProperty("FacesToKeep", facesToKeep);
            data.AddProperty("AffectedObjects", affectedObjects);
            data.AddProperty("Mode", mode);
            data.AddProperty("FromHere", fromHere);
            data.AddProperty("ToHere", toHere);
            data.AddProperty("ExtrusionDirection", ExtrusionDirection);
        }

        public override void SetObjectData(IJsonReadData data)
        {
            base.SetObjectData(data);
            facesToMove = data.GetProperty<List<Face>>("FacesToMove");
            facesToKeep = data.GetProperty<List<Face>>("FacesToKeep");
            affectedObjects = data.GetPropertyOrDefault<List<object>>("AffectedObjects");
            mode = data.GetProperty<Mode>("Mode");
            fromHere = data.GetProperty("FromHere");
            toHere = data.GetProperty("ToHere");
            // there is a problem with objects been serialized as "JsonVersion.serializeAsStruct". fromHere and toHere should always be GeoPoints in future versions
            if (fromHere is List<object> fh && fh.Count == 3 && fh[0] is double) fromHere = data.GetProperty<GeoPoint>("FromHere");
            if (fromHere is double[]) fromHere = data.GetProperty<GeoPoint>("FromHere");
            if (toHere is List<object> th && th.Count == 3 && th[0] is double) toHere = data.GetProperty<GeoPoint>("ToHere");
            if (toHere is double[]) toHere = data.GetProperty<GeoPoint>("ToHere");
            ExtrusionDirection = data.GetPropertyOrDefault<GeoVector>("ExtrusionDirection");
            data.RegisterForSerializationDoneCallback(this);
        }

        public override object GetAnchor()
        {
            return fromHere;
        }

        public override IEnumerable<object> GetAffectedObjects()
        {
            return affectedObjects;
        }

        void IJsonSerializeDone.SerializationDone(JsonSerialize jsonSerialize)
        {
            GetDistanceVector(out GeoPoint startPoint, out GeoPoint endPoint);
            Value = endPoint | startPoint;
        }

#if DEBUG
        public DebuggerContainer DebugAffected
        {
            get
            {

                DebuggerContainer dc = new DebuggerContainer();
                if (affectedObjects != null)
                {
                    foreach (var obj in affectedObjects)
                    {
                        if (obj is Edge edge) dc.Add(edge.Curve3D as IGeoObject, edge.GetHashCode());
                        if (obj is Vertex vtx) dc.Add(vtx.Position, System.Drawing.Color.Red, vtx.GetHashCode());
                        if (obj is Face face) dc.Add(face, face.GetHashCode());
                    }
                }
                return dc;
            }
        }
#endif
    }

    public class ParametricRadiusProperty : ParametricProperty, IJsonSerialize
    {
        private List<Face> facesToModify; // the faces with the radius to modify
        private List<object> affectedObjects; // faces which are affected by this parametric
        private bool useDiameter; // the proerty refers to the diameter, not to the radius

        public ParametricRadiusProperty(string name, IEnumerable<Face> facesToModify, bool useDiameter, IEnumerable<object> affectedObjects) : base(name)
        {
            this.facesToModify = new List<Face>(facesToModify);
            this.affectedObjects = new List<object>(affectedObjects);
            this.useDiameter = useDiameter;
            Value = (this.facesToModify[0].Surface as ISurfaceWithRadius).Radius;
            if (useDiameter) { Value *= 2; }
        }

        public override ParametricProperty Clone(Dictionary<Face, Face> clonedFaces, Dictionary<Edge, Edge> clonedEdges, Dictionary<Vertex, Vertex> clonedVertices)
        {
            List<object> affectedObjectsCloned = new List<object>();
            List<Face> facesToModifyCloned = new List<Face>();
            for (int i = 0; i < facesToModify.Count; i++)
            {
                if (clonedFaces.TryGetValue(facesToModify[i], out Face clone)) facesToModifyCloned.Add(clone);
            }
            if (affectedObjects != null) for (int i = 0; i < affectedObjects.Count; i++)
                {
                    if (affectedObjects[i] is Face face && clonedFaces.TryGetValue(face, out Face clonedFace)) affectedObjectsCloned.Add(clonedFace);
                    if (affectedObjects[i] is Edge edge && clonedEdges.TryGetValue(edge, out Edge clonedEdge)) affectedObjectsCloned.Add(clonedEdge);
                    if (affectedObjects[i] is Vertex vtx && clonedVertices.TryGetValue(vtx, out Vertex clonedVertex)) affectedObjectsCloned.Add(clonedVertex);
                }
            return new ParametricRadiusProperty(Name, facesToModifyCloned, useDiameter, affectedObjectsCloned);
        }
        public override void Modify(ModOp m)
        {   // there is nothing to do, all referred objects like facesToMove or fromHere are part of the shell and already modified
        }
        private double currentValue;
        public override double Value
        {
            get
            {
                double r = (facesToModify[0].Surface as ISurfaceWithRadius).Radius;
                if (useDiameter) return r * 2;
                else return r;
            }
            set
            {
                currentValue = value;
            }
        }
        public override bool Execute(Parametric parametric)
        {
            double r = currentValue;
            if (useDiameter) r /= 2.0;
            parametric.ModifyRadius(facesToModify, r);
            return parametric.Apply(); // the result may be inconsistent, but maybe further parametric operations make it consistent again
        }

        public override GeoObjectList GetFeedback(Projection projection)
        {
            double r = (facesToModify[0].Surface as ISurfaceWithRadius).Radius;
            GeoPoint2D cnt2d = facesToModify[0].Area.GetExtent().GetCenter();
            SurfaceHelper.MinMaxCurvature(facesToModify[0].Surface, cnt2d, out ICurve minCurvature, out ICurve maxCurvature);
            if (minCurvature is Ellipse elli)
            {
                if (useDiameter)
                {
                    return projection.MakeArrow(elli.StartPoint, elli.PointAt(0.5), projection.ProjectionPlane, Projection.ArrowMode.twoArrows);
                }
                else
                {
                    return projection.MakeArrow(elli.Center, elli.StartPoint, projection.ProjectionPlane, Projection.ArrowMode.circleArrow);
                }
            }
            return null;
        }
        protected ParametricRadiusProperty() : base("") { } // empty constructor for Json
        public override void GetObjectData(IJsonWriteData data)
        {
            base.GetObjectData(data);

            data.AddProperty("FacesToModify", facesToModify);
            data.AddProperty("UseDiameter", useDiameter);
        }

        public override void SetObjectData(IJsonReadData data)
        {
            base.SetObjectData(data);
            facesToModify = data.GetProperty<List<Face>>("FacesToModify");
            useDiameter = data.GetProperty<bool>("UseDiameter");
        }

        public override object GetAnchor()
        {
            return GeoPoint.Origin;
        }

        public override IEnumerable<object> GetAffectedObjects()
        {
            return affectedObjects;
        }

#if DEBUG
        public DebuggerContainer DebugAffected
        {
            get
            {

                DebuggerContainer dc = new DebuggerContainer();
                if (affectedObjects != null)
                {
                    foreach (var obj in affectedObjects)
                    {
                        if (obj is Edge edge) dc.Add(edge.Curve3D as IGeoObject, edge.GetHashCode());
                        if (obj is Vertex vtx) dc.Add(vtx.Position, System.Drawing.Color.Red, vtx.GetHashCode());
                        if (obj is Face face) dc.Add(face, face.GetHashCode());
                    }
                }
                return dc;
            }
        }
#endif
    }

    public class ParametricRotationProperty : ParametricProperty, IJsonSerialize, IJsonSerializeDone
    {
        private List<Face> backwardFaces, forwardFaces; // if mode is symmetric, both faces will be moved
        private List<object> affectedObjects; // faces which are affected by this parametric
        private object axis; // see following possibilities:
        // - a line-edge: the two common faces must be one in backwardFaces and one in forwardFaces. The angle is defined at the center of this line.
        // - a Face with an axis and a Face with a normal: the angle between those vectors ais the value. May not be parallel, or we additionally need
        //     a 2d vector in the uv space of the second face
        // - two faces with an axisi: the angle in between

        [Flags]
        public enum Mode
        {
            symmetric = 1, // use both backwardFaces and forwardFaces with half of the offset angle
        }
        private Mode mode;

        public ParametricRotationProperty(string name, IEnumerable<Face> backwardFaces, IEnumerable<Face> forwardFaces, IEnumerable<object> affectedObjects, Mode mode, object axis) : base(name)
        {
            this.backwardFaces = new List<Face>(backwardFaces);
            this.forwardFaces = new List<Face>(forwardFaces);
            this.affectedObjects = new List<object>(affectedObjects);
            this.mode = mode;
            GetAngleVectors(out GeoPoint location, out GeoVector fromHere, out GeoVector toHere);
            Value = new Angle(fromHere, toHere);
        }

        public override ParametricProperty Clone(Dictionary<Face, Face> clonedFaces, Dictionary<Edge, Edge> clonedEdges, Dictionary<Vertex, Vertex> clonedVertices)
        {
            List<Face> forwardFacesCloned = new List<Face>();
            List<Face> backwardFacesCloned = new List<Face>();
            List<object> affectedObjectsCloned = new List<object>();
            object fromHereCloned, toHereCloned;
            for (int i = 0; i < forwardFaces.Count; i++)
            {
                if (clonedFaces.TryGetValue(forwardFaces[i], out Face clone)) forwardFacesCloned.Add(clone);
            }
            for (int i = 0; i < forwardFaces.Count; i++)
            {
                if (clonedFaces.TryGetValue(forwardFaces[i], out Face clone)) backwardFacesCloned.Add(clone);
            }
            for (int i = 0; i < affectedObjects.Count; i++)
            {
                if (affectedObjects[i] is Face face && clonedFaces.TryGetValue(face, out Face clonedFace)) affectedObjectsCloned.Add(clonedFace);
                if (affectedObjects[i] is Edge edge && clonedEdges.TryGetValue(edge, out Edge clonedEdge)) affectedObjectsCloned.Add(clonedEdge);
                if (affectedObjects[i] is Vertex vtx && clonedVertices.TryGetValue(vtx, out Vertex clonedVertex)) affectedObjectsCloned.Add(clonedVertex);
            }
            object clonedAxis = null;
            if (axis is Edge edg && clonedEdges.TryGetValue(edg, out Edge clEdge)) clonedAxis = clEdge; // other cases to implement!
            return new ParametricRotationProperty(Name, forwardFacesCloned, backwardFacesCloned, affectedObjectsCloned, mode, clonedAxis);
        }
        public override void Modify(ModOp m)
        {   // all referred objects like facesToMove or fromHere are part of the shell and already modified
            // axis is currently only implemented as an edge, so nothing to do here
        }
        private double currentValue;
        public override double Value
        {
            get
            {
                GetAngleVectors(out GeoPoint location, out GeoVector fromHere, out GeoVector toHere);
                return new Angle(fromHere, toHere);
            }
            set
            {
                currentValue = value;
            }
        }
        /// <summary>
        /// if != NullVector, this is the direction of the extrusion. This is used by the distance calculation.
        /// </summary>
        public override bool Execute(Parametric parametric)
        {
            GetAngleVectors(out GeoPoint location, out GeoVector fromHere, out GeoVector toHere);
            GeoVector axdir = fromHere ^ toHere;
            double d = new Angle(fromHere, toHere);
            SweepAngle sw = new SweepAngle(currentValue - d);
            Dictionary<Face, ModOp> facesToAffect = new Dictionary<Face, ModOp>();
            if (mode.HasFlag(Mode.symmetric))
            {
                ModOp forward = ModOp.Rotate(location, axdir, sw / 2.0);
                ModOp backward = ModOp.Rotate(location, axdir, -sw / 2.0);
                foreach (Face face in forwardFaces)
                {
                    facesToAffect[face] = forward;
                }
                foreach (Face face in backwardFaces)
                {
                    facesToAffect[face] = backward;
                }
            }
            else
            {
                ModOp forward = ModOp.Rotate(location, axdir, sw);
                foreach (Face face in forwardFaces)
                {
                    facesToAffect[face] = forward;
                }
            }
            parametric.RotateFaces(facesToAffect, new Axis(location, axdir));
            return parametric.Apply(); // the result may be inconsistent, but maybe further parametric operations make it consistent again
        }
        private void GetAngleVectors(out GeoPoint location, out GeoVector fromHere, out GeoVector toHere)
        {
            if (axis is Edge edge)
            {
                if (forwardFaces.Contains(edge.PrimaryFace) && backwardFaces.Contains(edge.SecondaryFace))
                {
                    (Axis axis, GeoVector fh, GeoVector th) = Actions.ParametricsAngleAction.GetRotationAxis(edge.PrimaryFace, edge.SecondaryFace, Axis.InvalidAxis, edge);
                    fromHere = fh;
                    toHere = th;
                    location = axis.Location;
                }
            }
            location = GeoPoint.Invalid;
            fromHere = toHere = GeoVector.NullVector;
        }
        public override GeoObjectList GetFeedback(Projection projection)
        {
            GetAngleVectors(out GeoPoint location, out GeoVector fromHere, out GeoVector toHere);
            if (mode.HasFlag(Mode.symmetric))
            {
                GeoObjectList res = new GeoObjectList();
                // TODO: add doble arrow heads for MakeRotationArrow
                return projection.MakeRotationArrow(new Axis(location, fromHere ^ toHere), fromHere, toHere);
            }
            else return projection.MakeRotationArrow(new Axis(location, fromHere ^ toHere), fromHere, toHere);
        }
        protected ParametricRotationProperty() : base("") { } // empty constructor for Json
        public override void GetObjectData(IJsonWriteData data)
        {
            base.GetObjectData(data);

            data.AddProperty("ForwardFaces", forwardFaces);
            data.AddProperty("BackwardFaces", backwardFaces);
            data.AddProperty("AffectedObjects", affectedObjects);
            data.AddProperty("Mode", mode);
            data.AddProperty("Axis", axis);
        }

        public override void SetObjectData(IJsonReadData data)
        {
            base.SetObjectData(data);
            forwardFaces = data.GetProperty<List<Face>>("ForwardFaces");
            backwardFaces = data.GetProperty<List<Face>>("BackwardFaces");
            affectedObjects = data.GetPropertyOrDefault<List<object>>("AffectedObjects");
            mode = data.GetProperty<Mode>("Mode");
            axis = data.GetProperty("Axis");
            data.RegisterForSerializationDoneCallback(this);
        }

        public override object GetAnchor()
        {
            GetAngleVectors(out GeoPoint location, out GeoVector fromHere, out GeoVector toHere);
            return location;
        }

        public override IEnumerable<object> GetAffectedObjects()
        {
            return affectedObjects;
        }

        void IJsonSerializeDone.SerializationDone(JsonSerialize jsonSerialize)
        {
            GetAngleVectors(out GeoPoint location, out GeoVector fromHere, out GeoVector toHere);
            Value = new Angle(fromHere, toHere);
        }

#if DEBUG
        public DebuggerContainer DebugAffected
        {
            get
            {

                DebuggerContainer dc = new DebuggerContainer();
                if (affectedObjects != null)
                {
                    foreach (var obj in affectedObjects)
                    {
                        if (obj is Edge edge) dc.Add(edge.Curve3D as IGeoObject, edge.GetHashCode());
                        if (obj is Vertex vtx) dc.Add(vtx.Position, System.Drawing.Color.Red, vtx.GetHashCode());
                        if (obj is Face face) dc.Add(face, face.GetHashCode());
                    }
                }
                return dc;
            }
        }
#endif
    }

    public class ParametricCenterProperty : ParametricProperty, IJsonSerialize, IJsonSerializeDone
    {
        private List<Face> facesToCenter; // These faces should be centered according to objectsToCenterOn
        private List<object> objectsToCenterOn; // e.g. two vertices, two faces, two edges, a rotational face
        private List<object> referenceForCentering; // this could be a vertex, edge, face or axis from facesToCenter, if null, it will center to the extent of facestocenter
        // facesToCenter may fall into several groups of unconnected faces. These groups are centered independently. E.g. many holes between two planes
        // for each group there should be one object in referenceForCentering (groups not implemented yet
        private double ratio; // ratio for centering, usually 0.5
        private List<object> affectedObjects; // faces which are affected by this parametric


        public ParametricCenterProperty(string name, IEnumerable<Face> facesToCenter, IEnumerable<object> objectsToCenterOn, IEnumerable<object> referenceForCentering, IEnumerable<object> affectedObjects, double ratio) : base(name)
        {
            this.facesToCenter = new List<Face>(facesToCenter);
            this.objectsToCenterOn = new List<object>(objectsToCenterOn);
            this.referenceForCentering = new List<object>(referenceForCentering);
            this.ratio = ratio;
            this.affectedObjects = new List<object>(affectedObjects);
        }

        public override ParametricProperty Clone(Dictionary<Face, Face> clonedFaces, Dictionary<Edge, Edge> clonedEdges, Dictionary<Vertex, Vertex> clonedVertices)
        {
            List<Face> facesToCenterCloned = CloneFacesWithDictionary(facesToCenter, clonedFaces);
            List<object> objectsToCenterOnCloned = CloneWithDictionary(objectsToCenterOn, clonedFaces, clonedEdges, clonedVertices);
            List<object> referenceForCenteringCloned = CloneWithDictionary(referenceForCentering, clonedFaces, clonedEdges, clonedVertices);
            List<object> affectedObjectsCloned = CloneWithDictionary(affectedObjects, clonedFaces, clonedEdges, clonedVertices);
            return new ParametricCenterProperty(Name, facesToCenterCloned, objectsToCenterOnCloned, referenceForCenteringCloned, affectedObjectsCloned, ratio);
        }
        public override void Modify(ModOp m)
        {   // all referred objects like facesToMove or fromHere are part of the shell and already modified
            // axis is currently only implemented as an edge, so nothing to do here
            ModifyAxis(objectsToCenterOn, m);
        }
        private double currentValue;
        public override double Value
        {
            get
            {
                return ratio;
            }
            set
            {
                ratio = value;
            }
        }
        /// <summary>
        /// if != NullVector, this is the direction of the extrusion. This is used by the distance calculation.
        /// </summary>
        public override bool Execute(Parametric parametric)
        {
            GeoVector translation = ParametricsCenterAction.GetTranslationForCentering(facesToCenter, referenceForCentering.First(), objectsToCenterOn, ratio);
            if (!translation.IsNullVector())
            {
                Dictionary<Face, GeoVector > facesToMove = new Dictionary<Face, GeoVector>();
                foreach (Face face in facesToCenter)
                {
                    facesToMove[face] = translation;
                }
                parametric.MoveFaces(facesToMove, translation);
                return parametric.Apply(); // the result may be inconsistent, but maybe further parametric operations make it consistent again
            }
            return false;
        }
        public override GeoObjectList GetFeedback(Projection projection)
        {
            return new GeoObjectList(facesToCenter);
        }
        protected ParametricCenterProperty() : base("") { } // empty constructor for Json
        public override void GetObjectData(IJsonWriteData data)
        {
            base.GetObjectData(data);
            
            data.AddProperty("FacesToCenter", facesToCenter);
            data.AddProperty("ObjectsToCenterOn", objectsToCenterOn);
            data.AddProperty("ReferenceForCentering", referenceForCentering);
            data.AddProperty("AffectedObjects", affectedObjects);
            data.AddProperty("Ratio", ratio);
        }

        public override void SetObjectData(IJsonReadData data)
        {
            base.SetObjectData(data);
            facesToCenter = data.GetProperty<List<Face>>("FacesToCenter");
            objectsToCenterOn = data.GetProperty<List<object>>("ObjectsToCenterOn");
            referenceForCentering = data.GetProperty<List<object>>("ReferenceForCentering");
            affectedObjects = data.GetProperty<List<object>>("AffectedObjects");
            ratio = data.GetDoubleProperty("Ratio");
            data.RegisterForSerializationDoneCallback(this);
        }

        public override object GetAnchor()
        {
            BoundingCube ext = new BoundingCube(facesToCenter);
            return ext.GetCenter();
        }

        public override IEnumerable<object> GetAffectedObjects()
        {
            return affectedObjects;
        }

        void IJsonSerializeDone.SerializationDone(JsonSerialize jsonSerialize)
        {
            Value = ratio;
        }

#if DEBUG
        public DebuggerContainer DebugAffected
        {
            get
            {

                DebuggerContainer dc = new DebuggerContainer();
                if (affectedObjects != null)
                {
                    foreach (var obj in affectedObjects)
                    {
                        if (obj is Edge edge) dc.Add(edge.Curve3D as IGeoObject, edge.GetHashCode());
                        if (obj is Vertex vtx) dc.Add(vtx.Position, System.Drawing.Color.Red, vtx.GetHashCode());
                        if (obj is Face face) dc.Add(face, face.GetHashCode());
                    }
                }
                return dc;
            }
        }
#endif
    }

}

