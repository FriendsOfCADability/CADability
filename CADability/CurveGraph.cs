using CADability.Actions;
using CADability.Attribute;
using CADability.Curve2D;
using CADability.GeoObject;
using System;
using System.Collections;
using System.Collections.Generic;
using Wintellect.PowerCollections;

namespace CADability.Shapes
{
    /// <summary>
    /// A connection point. The curve `curve` starts or ends here. 
    /// It is used in the cluster, which generally consists of multiple such connection points.
    /// </summary>
    internal class Joint : IComparable
    {
        public ICurve2D curve; // The curve
        public Cluster StartCluster; // From here
        public Cluster EndCluster; // To there (remove isStartPoint again)
        public double tmpAngle; // An angle for sorting
        public bool forwardUsed; // This edge was used
        public bool reverseUsed;
        public Joint() { }
        public override string ToString()
        {
            return "Joint: " + curve.ToString();
        }
#if DEBUG
        GeoObjectList Debug
        {
            get
            {
                GeoObjectList res = new GeoObjectList();
                IGeoObject go = curve.MakeGeoObject(Plane.XYPlane);
                (go as IColorDef).ColorDef = new ColorDef("Main", System.Drawing.Color.Black);
                res.Add(go);
                if (StartCluster != null)
                {
                    for (int i = 0; i < StartCluster.Joints.Count; ++i)
                    {
                        go = StartCluster.Joints[i].curve.MakeGeoObject(Plane.XYPlane);
                        (go as IColorDef).ColorDef = new ColorDef("Start", System.Drawing.Color.Green);
                        res.Add(go);
                    }
                }
                if (EndCluster != null)
                {
                    for (int i = 0; i < EndCluster.Joints.Count; ++i)
                    {
                        go = EndCluster.Joints[i].curve.MakeGeoObject(Plane.XYPlane);
                        (go as IColorDef).ColorDef = new ColorDef("Start", System.Drawing.Color.Red);
                        res.Add(go);
                    }
                }
                return res;
            }
        }
#endif
        #region IComparable Members
        // zum Sortieren nach tmpAngle
        public int CompareTo(object obj)
        {
            Joint other = obj as Joint;
            return tmpAngle.CompareTo(other.tmpAngle);
        }

        #endregion
    }

/// <summary>
/// One or more joints that are located very close together 
/// and are considered identical points.
/// </summary>
internal class Cluster : IQuadTreeInsertable
{
    public GeoPoint2D center; // The center point of all associated joints
    public List<Joint> Joints; // List of Joint objects
        public Cluster()
        {
            Joints = new List<Joint>();
        }
#if DEBUG
        GeoObjectList Debug
        {
            get
            {
                GeoObjectList res = new GeoObjectList();
                for (int i = 0; i < Joints.Count; ++i)
                {
                    IGeoObject go = Joints[i].curve.MakeGeoObject(Plane.XYPlane);
                    (go as IColorDef).ColorDef = new ColorDef("Start", System.Drawing.Color.Green);
                    res.Add(go);
                }
                return res;
            }
        }
#endif
        #region IQuadTreeInsertable Members

        public BoundingRect GetExtent()
        {
            BoundingRect res = BoundingRect.EmptyBoundingRect;
            foreach (Joint lp in Joints)
            {
                GeoPoint2D p;
                if (lp.StartCluster == this) p = lp.curve.StartPoint;
                else p = lp.curve.EndPoint;
                res.MinMax(p);
            }
            return res;
        }

        public bool HitTest(ref BoundingRect Rect, bool IncludeControlPoints)
        {
            foreach (Joint lp in Joints)
            {
                GeoPoint2D p;
                if (lp.StartCluster == this) p = lp.curve.StartPoint;
                else p = lp.curve.EndPoint;
                if (p > Rect) return false;
            }
            return true;
        }
        public object ReferencedObject
        {
            get
            {
                return this;
            }
        }

        #endregion
        public override string ToString()
        {
            string res = "Cluster:\n";
            for (int i = 0; i < Joints.Count; ++i) res += Joints[i].ToString() + "\n";
            return res;
        }
    }

    internal class CurveGraphException : ApplicationException
    {
        public CurveGraphException(string msg) : base(msg)
        {
        }
    }
    /// <summary>
    /// INTERNAL:
    /// Used for generating a Border and SimpleShape/CompoundShape from a list of 
    /// ICurve2D. Intersections are not considered; they must be created beforehand,
    /// and the ICurve2D objects must be split accordingly.
    /// </summary>
    internal class CurveGraph
    {
        private double clusterSize; // Maximum cluster size, depending on the extent of all objects
        private double maxGap; // Maximum gap to be closed
        private QuadTree clusterTree; // QuadTree of all clusters
        private UntypedSet clusterSet; // Set of all clusters (parallel to the QuadTree)
        /// <summary>
        /// List of unusable objects that were generated during the creation of a CompoundShape
        /// </summary>
        public List<IGeoObject> DeadObjects { get; } = new List<IGeoObject>();

        static public CurveGraph CrackCurves(GeoObjectList l, Plane plane, double maxGap)
        {   // All curves in l are projected onto the plane. This is just a first approach.
            // One could also find common planes and so on.
            ArrayList curves = new ArrayList();
            BoundingRect ext = BoundingRect.EmptyBoundingRect;
            foreach (IGeoObject go in l)
            {
                ICurve cv = go as ICurve;
                if (cv != null)
                {
                    ICurve2D cv2 = cv.GetProjectedCurve(plane);
                    if (cv2 != null)
                    {
                        // "3d" is only used to determine the plane from the original curves
                        // into which everything will be transformed back. It would probably be better
                        // to work with "plane," as it is provided here.
                        if (cv2 is Path2D && (cv2 as Path2D).GetSelfIntersections().Length > 0)
                        {   // A self-intersecting path must be resolved
                            ICurve2D[] sub = (cv2 as Path2D).SubCurves;
                            curves.AddRange(sub);
                            for (int i = 0; i < sub.Length; ++i)
                            {
                                sub[i].UserData.Add("3d", cv);
                            }
                            ext.MinMax(cv2.GetExtent());
                        }
                        else
                        {
                            cv2.UserData.Add("3d", cv);
                            curves.Add(cv2);
                            ext.MinMax(cv2.GetExtent());
                        }
                    }
                }
            }
            if (curves.Count == 0) return null;
            QuadTree qt = new QuadTree(ext);
            qt.MaxDeepth = 8;
            qt.MaxListLen = 3;
            for (int i = 0; i < curves.Count; ++i)
            {
                qt.AddObject(curves[i] as ICurve2D);
            }
            // Now intersect all with all and put the fragments into another list
            ArrayList snippet = new ArrayList();
            for (int i = 0; i < curves.Count; ++i)
            {
                ICurve2D cv1 = curves[i] as ICurve2D;
                ArrayList intersectionPoints = new ArrayList(); // double
                ICollection closecurves = qt.GetObjectsCloseTo(cv1);
                foreach (ICurve2D cv2 in closecurves)
                {
                    if (cv2 != cv1)
                    {
                        //if ((cv1 is Line2D && (cv1 as Line2D).Length > 10 && (cv1 as Line2D).Length < 15) ||
                        //    (cv2 is Line2D && (cv2 as Line2D).Length > 10 && (cv2 as Line2D).Length < 15))
                        //{
                        //}
                        GeoPoint2DWithParameter[] isp = cv1.Intersect(cv2);
                        for (int k = 0; k < isp.Length; ++k)
                        {
                            if (cv2.IsParameterOnCurve(isp[k].par2) && 0.0 < isp[k].par1 && isp[k].par1 < 1.0)
                            {
                                intersectionPoints.Add(isp[k].par1);
                            }
                        }
                    }
                }
                if (intersectionPoints.Count == 0)
                {
                    snippet.Add(cv1);
                }
                else
                {
                    intersectionPoints.Add(0.0);
                    intersectionPoints.Add(1.0); // This ensures there are at least 3
                    double[] pps = (double[])intersectionPoints.ToArray(typeof(double));
                    Array.Sort(pps);
                    for (int ii = 1; ii < pps.Length; ++ii)
                    {
                        if (pps[ii - 1] < pps[ii])
                        {
                            ICurve2D cv3 = cv1.Trim(pps[ii - 1], pps[ii]);
                            if (cv3 != null)
                            {
#if DEBUG
                                GeoPoint2D dbg1 = cv1.PointAt(pps[ii - 1]);
                                GeoPoint2D dbg2 = cv1.PointAt(pps[ii]);
                                GeoPoint2D dbg3 = cv3.StartPoint;
                                GeoPoint2D dbg4 = cv3.EndPoint;
                                double d1 = dbg1 | dbg3;
                                double d2 = dbg2 | dbg4;
#endif
                                cv3.UserData.Add("3d", cv1.UserData.GetData("3d"));
                                snippet.Add(cv3);
                            }
                        }
                    }
                }
            }
            // snippet is now the list of all fragments
            return new CurveGraph((ICurve2D[])snippet.ToArray(typeof(ICurve2D)), maxGap);
        }

        public CurveGraph(ICurve2D[] curves, double maxGap)
        {   // A cluster list is created from the ICurve2D (start and end points)
            this.maxGap = maxGap;
            BoundingRect ext = BoundingRect.EmptyBoundingRect;
            for (int i = 0; i < curves.Length; ++i)
            {
                ext.MinMax(curves[i].GetExtent());
            }
            // clusterSize = (ext.Width + ext.Height) * 1e-8;
            clusterSize = maxGap;
            clusterTree = new QuadTree(ext);
            clusterTree.MaxDeepth = 8;
            clusterTree.MaxListLen = 3;
            clusterSet = new UntypedSet();
            for (int i = 0; i < curves.Length; ++i)
            {
                if (curves[i].Length > clusterSize)
                {
                    Insert(curves[i]);
                }
            }
        }

        private void Insert(ICurve2D curve)
        {   // The start or end point of a curve is added to the cluster list
            Joint lp = new Joint();
            lp.curve = curve;

            GeoPoint2D p = curve.StartPoint;
            BoundingRect CheckSp = new BoundingRect(p, clusterSize, clusterSize);
            ICollection StartCluster = clusterTree.GetObjectsFromRect(CheckSp);
            Cluster InsertInto = null;
            foreach (Cluster cl in StartCluster)
            {
                if (Geometry.Dist(cl.center, p) < clusterSize)
                {
                    InsertInto = cl;
                    clusterTree.RemoveObject(cl); // Remove it, as it might get larger and will be reinserted below
                    break;
                }
            }
            if (InsertInto == null)
            {
                InsertInto = new Cluster();
                clusterSet.Add(InsertInto);
            }
            InsertInto.Joints.Add(lp);
            lp.StartCluster = InsertInto;
            double x = 0.0;
            double y = 0.0;
            for (int i = 0; i < InsertInto.Joints.Count; ++i)
            {
                GeoPoint2D pp;
                if ((InsertInto.Joints[i]).StartCluster == InsertInto)
                {
                    pp = (InsertInto.Joints[i]).curve.StartPoint;
                }
                else
                {
                    pp = (InsertInto.Joints[i]).curve.EndPoint;
                }
                x += pp.x;
                y += pp.y;
            }
            InsertInto.center = new GeoPoint2D(x / InsertInto.Joints.Count, y / InsertInto.Joints.Count);
            clusterTree.AddObject(InsertInto);

            // The same applies to the endpoint:
            p = curve.EndPoint;
            CheckSp = new BoundingRect(p, clusterSize, clusterSize);
            StartCluster = clusterTree.GetObjectsFromRect(CheckSp);
            InsertInto = null;
            foreach (Cluster cl in StartCluster)
            {
                if (Geometry.Dist(cl.center, p) < clusterSize)
                {
                    InsertInto = cl;
                    clusterTree.RemoveObject(cl); // Remove it, as it might get larger and will be reinserted below
                    break;
                }
            }
            if (InsertInto == null)
            {
                InsertInto = new Cluster();
                clusterSet.Add(InsertInto);
            }
            InsertInto.Joints.Add(lp);
            lp.EndCluster = InsertInto;
            x = 0.0;
            y = 0.0;
            for (int i = 0; i < InsertInto.Joints.Count; ++i)
            {
                GeoPoint2D pp;
                if ((InsertInto.Joints[i]).StartCluster == InsertInto)
                {
                    pp = (InsertInto.Joints[i]).curve.StartPoint;
                }
                else
                {
                    pp = (InsertInto.Joints[i]).curve.EndPoint;
                }
                x += pp.x;
                y += pp.y;
            }
            InsertInto.center = new GeoPoint2D(x / InsertInto.Joints.Count, y / InsertInto.Joints.Count);
            clusterTree.AddObject(InsertInto);

        }

        private Cluster FindCluster(ICurve2D curve, GeoPoint2D p, bool RemovePoint)
        {   // Finds a cluster that contains the point p and the curve curve
            BoundingRect clip = new BoundingRect(p, clusterSize, clusterSize);
            ICollection col = clusterTree.GetObjectsFromRect(clip);
            foreach (Cluster cl in col)
            {
                if (cl.HitTest(ref clip, false))
                {
                    for (int i = 0; i < cl.Joints.Count; ++i)
                    {
                        if ((cl.Joints[i]).curve == curve)
                        {
                            if (RemovePoint) cl.Joints.RemoveAt(i);
                            return cl;
                        }
                    }
                }
            }
            return null;
        }
        private void RemoveAllDeadEnds()
        {
            ArrayList ClusterToRemove = new ArrayList(); // So that the loop can iterate over clusterSet
            foreach (Cluster cl in clusterSet)
            {
                if (cl.Joints.Count < 2)
                {
                    ClusterToRemove.Add(cl);
                }
            }
            foreach (Cluster cl in ClusterToRemove) RemoveDeadEnd(cl);
        }
        private void RemoveDeadEnd(Cluster cl)
        {
            Cluster NextCluster = cl;
            while (NextCluster != null && NextCluster.Joints.Count < 2)
            {
                clusterTree.RemoveObject(NextCluster);
                clusterSet.Remove(NextCluster);
                if (NextCluster.Joints.Count > 0)
                {
                    DeadObjects.Add(NextCluster.Joints[0].curve.MakeGeoObject(Plane.XYPlane));

                    Joint lp = NextCluster.Joints[0]; // There is exactly one
                    if (lp.StartCluster == NextCluster) NextCluster = lp.EndCluster;
                    else NextCluster = lp.StartCluster;
                    if (NextCluster != null)
                    {
                        for (int i = 0; i < NextCluster.Joints.Count; ++i)
                        {
                            if ((NextCluster.Joints[i]) == lp)
                            {
                                NextCluster.Joints.RemoveAt(i);
                                break; // Extracted this path
                            }
                        }
                    }
                }
                else break;
            }
        }
        //		private void FindCurve(Cluster StartCluster, int PointIndex, Cluster EndCluster, Set UsedClusters, ArrayList result)
        //		{
        //			// The cluster StartHere has more than two connections. The goal is to find all paths that lead from StartHere
        //          // to EndHere. The result can be found in the parameter 'result', which contains one or more ArrayLists,
        //          // each representing a sequence of clusters and indices.
        //			Cluster LastCluster = StartCluster;
        //			while (PointIndex>=0)
        //			{
        //				SingleCurve.Add(LastCluster);
        //				SingleCurve.Add(PointIndex);
        //				UsedClusters.Add(LastCluster);
        //				Joint lp = (Joint)LastCluster.Points[PointIndex];
        //				Cluster cl;
        //				if (lp.isStartPoint) cl = FindCluster(lp.curve,lp.curve.EndPoint,false);
        //				else cl = FindCluster(lp.curve,lp.curve.StartPoint,false);
        //				if (cl==null) break; // Nothing found; this should actually never happen
        //				if (cl==EndCluster)
        //				{	// fertig
        //					result.Add(SingleCurve);
        //					break;
        //				}
        //				if (UsedClusters.Contains(cl)) break; // Internal short circuit, does not lead to the start
        //				if (cl.Points.Count==2)
        //				{	// It clearly continues
        //					if (((Joint)cl.Points[0]).curve==lp.curve) PointIndex = 1;
        //					else PointIndex = 0;
        //					LastCluster = cl;
        //					// And on we go
        //				} 
        //				else
        //				{	// There is more than one continuation, as clusters with a single point should not exist
        //					// Therefore, different continuations are being searched for here
        //					for (int i=0; i<cl.Points.Count; ++i)
        //					{
        //						if (((Joint)cl.Points[i]).curve!=lp.curve)
        //						{
        //							ArrayList NextSegment = new ArrayList(); // The new result
        //							FindCurve(cl,i,EndCluster,UsedClusters.Clone(),NextSegment);
        //							for (int j=0; j<NextSegment.Count; ++j)
        //							{
        //								ArrayList NextCurve = new ArrayList(SingleCurve); // Always the same beginning
        //								NextCurve.AddRange((ArrayList)NextSegment[j]); // And different endings
        //								result.Add(NextCurve);
        //							}
        //						}
        //					}
        //					break; // Done
        //				}
        //			}
        //		}
        private void RemoveJoint(Joint lp, Cluster cl)
        {   // Removes a joint from a cluster. If the cluster is empty, it is completely removed
            clusterTree.RemoveObject(cl);
            cl.Joints.Remove(lp);
            if (cl.Joints.Count > 0) clusterTree.AddObject(cl);
            else clusterSet.Remove(cl);
        }
        private void RemoveCurve(ICurve2D ToRemove)
        {
            ExtractCurve(ToRemove, true);
            ExtractCurve(ToRemove, false);
        }
        private Cluster ExtractCurve(ICurve2D ToRemove, bool RemoveStartPoint)
        {
            GeoPoint2D p;
            if (RemoveStartPoint) p = ToRemove.StartPoint;
            else p = ToRemove.EndPoint;
            BoundingRect tst = new BoundingRect(p, clusterSize, clusterSize);
            ICollection cllist = clusterTree.GetObjectsFromRect(tst);
            Cluster foundCluster = null;
            Joint foundJoint = null;
            foreach (Cluster cl in cllist)
            {
                if (cl.HitTest(ref tst, false))
                {
                    foreach (Joint lp in cl.Joints)
                    {
                        if (lp.curve == ToRemove)
                        {
                            foundCluster = cl;
                            foundJoint = lp;
                            break;
                        }
                    }
                    if (foundCluster != null) break;
                }
            }
            if (foundCluster == null) return null; // Should not occur
            RemoveJoint(foundJoint, foundCluster);
            return foundCluster;
        }
        //		private Border FindNextBorder()
        //		{
        //			// All clusters contain two points. Search for a joint whose connected
        //			// ICurve2D is longer than maxGap (why actually?)
        //			// Iterate through the clusters until the first point is reached again
        //			Joint StartWith = null;
        //			foreach (Cluster cl in clusterSet)
        //			{
        //				foreach (Joint lp in cl.Points)
        //				{
        //					if (lp.curve.Length>maxGap)
        //					{
        //						StartWith = lp;
        //						RemoveJoint(lp,cl);
        //						break;
        //					}
        //				}
        //				if (StartWith!=null) break;
        //			}
        //			if (StartWith==null) return null; // No beginning found
        //			Joint LastPoint = StartWith; 
        //			Cluster goon = null;
        //			BorderBuilder makeBorder = new BorderBuilder();
        //			makeBorder.Precision = clusterSize;
        //			while ((goon = ExtractCurve(LastPoint.curve,!LastPoint.isStartPoint))!=null)
        //			{
        //				makeBorder.AddSegment(LastPoint.curve.CloneReverse(!LastPoint.isStartPoint));
        //				if (goon.Points.Count==0) break; // Encountered the last and first point
        //				LastPoint = (Joint)goon.Points[0]; // There should only be this one
        //				RemoveJoint(LastPoint,goon); // This should make this cluster disappear
        //			}
        //			return makeBorder.BuildBorder();
        //		}
        //		private bool FindSimpleBorder(Set clusterSet, ArrayList AllBorders, Set UsedJoints, ICurve2D startWith, bool forward)
        //		{
        //			Set tmpUsedJoints = new Set();
        //			tmpUsedJoints.Add(new UsedJoint(startWith,forward));
        //			// Starting edge found, what’s next
        //			BorderBuilder bb = new BorderBuilder();
        //			bb.Precision = clusterSize;
        //			if (forward) bb.AddSegment(startWith.Clone());
        //			else bb.AddSegment(startWith.CloneReverse(true));
        //			
        //			while (!bb.IsClosed)
        //			{
        //				Cluster cl = FindCluster(startWith, bb.EndPoint, false);
        //				if (cl==null) return false; // A started border does not continue, this should not happen as there are no dead ends
        //				int ind = -1;
        //				double sa = -1.0;
        //				for (int i=0; i<cl.Points.Count; ++i)
        //				{
        //					Joint j = cl.Points[i] as Joint;
        //					if (j.curve==startWith) continue; // Do not move backward immediately
        //					UsedJoint uj = new UsedJoint(j.curve,j.isStartPoint);
        //					if (!UsedJoints.Contains(uj) && !tmpUsedJoints.Contains(uj))
        //					{
        //						SweepAngle d;
        //						if (j.isStartPoint) d = new SweepAngle(bb.EndDirection,j.curve.StartDirection);
        //						else d = new SweepAngle(bb.EndDirection,j.curve.EndDirection.Opposite());
        //						// d zwischen -PI und +PI
        //						if (d+Math.PI > sa)
        //						{	// The further to the left, the larger d becomes
        //							sa = d+Math.PI;
        //							ind = i;
        //						}
        //					}
        //				}
        //				if (ind>=0)
        //				{
        //					Joint j = cl.Points[ind] as Joint;
        //					if (j.isStartPoint) bb.AddSegment(j.curve.Clone());
        //					else bb.AddSegment(j.curve.CloneReverse(true));
        //					tmpUsedJoints.Add(new UsedJoint(j.curve,j.isStartPoint));
        //					startWith = j.curve;
        //				}
        //				else
        //				{
        //					return false; // No further progression possible
        //				}
        //			}
        //			if (bb.IsOriented)
        //			{
        //				Border bdr = bb.BuildBorder();
        //				AllBorders.Add(bdr);
        //				foreach (UsedJoint uj in tmpUsedJoints) 
        //				{
        //					if (!UsedJoints.Contains(uj))
        //					{
        //						UsedJoints.Add(uj);
        //					}
        //					else
        //					{
        //						int dbg = 0;
        //					}
        //				}
        //				return true;
        //			}
        //			return false;
        //		}
        //		private bool FindSimpleBorder(Set clusterSet, ArrayList AllBorders, Set UsedJoints)
        //		{
        //          // A minimal border is being searched: starting from any cluster, 
        //          // always move to the left until you are back at the start. 
        //          // UsedJoints contains UsedJoint objects to determine 
        //          // whether an edge has already been used or not.
        //			ICurve2D startWith = null; 
        //			bool forward = false;
        //			foreach (Cluster cl in clusterSet)
        //			{
        //				for (int i=0; i<cl.Points.Count; ++i)
        //				{
        //					UsedJoint uj = new UsedJoint();
        //					Joint j = cl.Points[i] as Joint;
        //					uj.curve = j.curve;
        //					uj.forward = true;
        //					if (!UsedJoints.Contains(uj))
        //					{
        //						forward = j.isStartPoint;
        //						startWith = j.curve;
        //						if (FindSimpleBorder(clusterSet,AllBorders,UsedJoints,startWith,forward))
        //							return true;
        //					}
        //					uj.forward = false;
        //					if (!UsedJoints.Contains(uj))
        //					{
        //						forward = !j.isStartPoint;
        //						startWith = j.curve;
        //						if (FindSimpleBorder(clusterSet,AllBorders,UsedJoints,startWith,forward))
        //							return true;
        //					}
        //				}
        //			}
        //			return false;
        //		}
        private Joint[] SortCluster()
        {   // Sorts the edges (joints) in a cluster counterclockwise
            // Returns all edges

            // Discarding identical edges:
            // Two edges in a cluster that share the same "opposite cluster"
            // are suspected to be identical. Their center points are checked
            // for identity, and the edges are removed if necessary.
            foreach (Cluster cl in clusterSet)
            {
                for (int i = 0; i < cl.Joints.Count - 1; ++i)
                {
                    int duplicate = -1;
                    for (int j = i + 1; j < cl.Joints.Count; ++j)
                    {
                        Cluster cl1;
                        Cluster cl2;
                        Joint j1 = cl.Joints[i] as Joint;
                        Joint j2 = cl.Joints[j] as Joint;
                        if (j1.StartCluster == cl) cl1 = j1.EndCluster;
                        else cl1 = j1.StartCluster;
                        if (j2.StartCluster == cl) cl2 = j2.EndCluster;
                        else cl2 = j2.StartCluster;
                        if (cl1 == cl2)
                        {   // Two edges connect the same clusters. They might be identical.
                            ICurve2D curve1 = j1.curve.CloneReverse(j1.StartCluster != cl);
                            ICurve2D curve2 = j2.curve.CloneReverse(j2.StartCluster != cl);
                            // curve1 and curve2 now have the same direction
                            GeoPoint2D p1 = curve1.PointAt(0.5);
                            GeoPoint2D p2 = curve2.PointAt(0.5);
                            if (Geometry.Dist(p1, p2) < clusterSize)
                            {
                                duplicate = j;
                                break;
                            }
                        }
                    }
                    if (duplicate > 0)
                    {
                        cl.Joints.RemoveAt(duplicate);
                    }
                }
            }
            // Joints that are too short will be removed
            foreach (Cluster cl in clusterSet)
            {
                for (int i = cl.Joints.Count - 1; i >= 0; --i)
                {
                    Joint j1 = cl.Joints[i] as Joint;
                    if (j1.curve.Length < this.clusterSize)
                    {
                        cl.Joints.RemoveAt(i);
                    }
                }
            }

            UntypedSet allJoints = new UntypedSet();
            foreach (Cluster cl in clusterSet)
            {
                if (cl.Joints.Count < 3)
                {
                    foreach (Joint j in cl.Joints)
                    {
                        if (!allJoints.Contains(j)) allJoints.Add(j);
                    }
                    continue;
                }
                // Two points in the cluster do not need to be sorted
                double minDist = double.MaxValue;
                foreach (Joint j in cl.Joints)
                {
                    if (!allJoints.Contains(j)) allJoints.Add(j);
                    GeoPoint2D p;
                    if (j.StartCluster == cl) p = j.EndCluster.center;
                    else
                    {
                        if (j.EndCluster != cl) throw new CurveGraphException("SortCluster");
                        p = j.StartCluster.center;
                    }
                    double d = Geometry.Dist(cl.center, p);
                    if (d == 0.0)
                    {
                        if (j.StartCluster == j.EndCluster)
                        {
                            continue;
                        }
                        throw new CurveGraphException("SortCluster");
                    }
                    if (d < minDist) minDist = d;
                }
                // Kreis um cl mit halber Entfernung zum nächsten Knoten als Radius 
                Circle2D c2d = new Circle2D(cl.center, minDist / 2.0);
                foreach (Joint j in cl.Joints)
                {
                    GeoPoint2DWithParameter[] ip = c2d.Intersect(j.curve);
                    if (ip.Length > 0)
                    {
                        for (int i = 0; i < ip.Length; ++i)
                        {
                            if (j.curve.IsParameterOnCurve(ip[i].par2))
                            {
                                Angle a = new Angle(ip[i].p, cl.center);
                                j.tmpAngle = a.Radian;
                                break;
                            }
                        }
                    }
                    else
                    {
                        // Should not occur: an edge does not intersect the circle around 
                        // the node with half the radius to the next node.
                        // The sorting value remains 0.0, but such edges 
                        // should be removed...
                        // It happens; the problem lies with rule4!!!
                        if (j.StartCluster == cl)
                        {   // Curve starts here
                            j.tmpAngle = j.curve.StartDirection.Angle;
                        }
                        else
                        {
                            j.tmpAngle = j.curve.EndDirection.Opposite().Angle;
                        }
                    }
                }
                cl.Joints.Sort(); // Sorting is done based on tmpAngle
            }
            Joint[] res = new Joint[allJoints.Count];
            int ii = 0;
            foreach (Joint j in allJoints)
            {   // Precisely align the curve
                try
                {
                    j.curve.StartPoint = j.StartCluster.center;
                    j.curve.EndPoint = j.EndCluster.center;
                }
                catch (Curve2DException) { } // For example, set the endpoint of circles
                res[ii] = j;
                ++ii;
            }
            return res;
        }
        private BorderBuilder FindBorder(Joint startWith, bool forward)
        {
            BorderBuilder bb = new BorderBuilder();
            bb.Precision = clusterSize;
            if (startWith.curve.Length == 0.0) return null; // Should not occur, but it does
            bb.AddSegment(startWith.curve.CloneReverse(!forward));
            Cluster cl;
            Cluster startCluster;
            if (forward)
            {
                cl = startWith.EndCluster;
                startCluster = startWith.StartCluster;
                // An exception should be thrown here if already used!!
                if (startWith.forwardUsed) return null; // Already used, should not occur
                startWith.forwardUsed = true;
            }
            else
            {
                cl = startWith.StartCluster;
                startCluster = startWith.EndCluster;
                if (startWith.reverseUsed) return null; // Already used, should not occur
                startWith.reverseUsed = true;
            }
            while (cl != startCluster)
            {
                int ind = -1;
                for (int i = 0; i < cl.Joints.Count; ++i)
                {
                    if (cl.Joints[i] == startWith)
                    {
                        ind = i - 1;
                        if (ind < 0) ind = cl.Joints.Count - 1;
                        break;
                    }
                }
                startWith = cl.Joints[ind] as Joint;
                forward = (startWith.StartCluster == cl);
                if (startWith.curve.Length == 0.0) return null; // Should not happen, but it does
                bb.AddSegment(startWith.curve.CloneReverse(!forward));
                if (forward)
                {
                    cl = startWith.EndCluster;
                    if (startWith.forwardUsed) return null; // Already used, inner loop
                    startWith.forwardUsed = true;
                }
                else
                {
                    cl = startWith.StartCluster;
                    if (startWith.reverseUsed) return null; // Already used, inner loop
                    startWith.reverseUsed = true; // Here too, throw an exception if it was already used!!
                }
            }
            return bb;
        }
        private Border[] FindAllBorders(Joint[] AllJoints, bool inner)
        {
            ArrayList res = new ArrayList();
            for (int i = 0; i < AllJoints.Length; ++i)
            {
                if (!AllJoints[i].forwardUsed)
                {
                    BorderBuilder bb = FindBorder(AllJoints[i], true);
                    if (bb != null && bb.IsOriented == inner && bb.IsClosed) res.Add(bb.BuildBorder(true));
                }
                if (!AllJoints[i].reverseUsed)
                {
                    BorderBuilder bb = FindBorder(AllJoints[i], false);
                    if (bb != null && bb.IsOriented == inner && bb.IsClosed) res.Add(bb.BuildBorder(true));
                }
            }
            return (Border[])res.ToArray(typeof(Border));
        }
#if DEBUG
        GeoObjectList Debug
        {
            get
            {
                GeoObjectList res = new GeoObjectList();
                Set<ICurve2D> c2d = new Set<ICurve2D>();
                foreach (Cluster cl in clusterSet)
                {
                    GeoPoint p = new GeoPoint(cl.center);
                    Point pnt = Point.Construct();
                    pnt.Location = p;
                    pnt.Symbol = PointSymbol.Circle;
                    if (cl.Joints.Count > 1) pnt.Symbol = PointSymbol.Square;
                    res.Add(pnt);
                    foreach (Joint j in cl.Joints)
                    {
                        c2d.Add(j.curve);
                    }
                }
                foreach (ICurve2D c in c2d)
                {
                    res.Add(c.MakeGeoObject(Plane.XYPlane));
                }
                return res;
            }
        }
        Joint[] DebugJoints;
        GeoObjectList Joints
        {
            get
            {
                GeoObjectList res = new GeoObjectList();
                for (int i = 0; i < DebugJoints.Length; ++i)
                {
                    IGeoObject go = DebugJoints[i].curve.MakeGeoObject(Plane.XYPlane);
                    if (DebugJoints[i].forwardUsed && DebugJoints[i].reverseUsed)
                    {
                        (go as IColorDef).ColorDef = new ColorDef("bothUsed", System.Drawing.Color.Black);
                    }
                    else if (DebugJoints[i].forwardUsed)
                    {
                        (go as IColorDef).ColorDef = new ColorDef("forwardUsed", System.Drawing.Color.Red);
                    }
                    else if (DebugJoints[i].reverseUsed)
                    {
                        (go as IColorDef).ColorDef = new ColorDef("reverseUsed", System.Drawing.Color.Green);
                    }
                    else
                    {
                        (go as IColorDef).ColorDef = new ColorDef("unUsed", System.Drawing.Color.Blue);
                    }
                    res.Add(go);
                }
                return res;
            }

        }
#endif
        public CompoundShape CreateCompoundShape(bool useInnerPoint, GeoPoint2D innerPoint, ConstrHatchInside.HatchMode mode, bool partInPart)
        {
            // 1. Connect open ends with other open ends if the distance is less than maxGap.
            // 2. Remove all remaining dead ends (collect all single points and "consume" them from there).
            // 3. This results in connected point sets.
            // 4. Determine the edges and set references to their clusters (this step might be integrated into one of the previous steps).
            // 5. Sort the edges counterclockwise within each cluster:
            //    At this point, we have one or more non-overlapping graphs, where finding the border is easy by walking counterclockwise.
            //    Additionally, for each graph, the hull can be found. It can be identified as it follows a clockwise direction.
            // 6. If useInnerPoint == false && partInPart == true, then nested parts are also searched for and returned.

            // Insert gap fillers, specifically the shortest possible ones, 
            // and only at open ends.
            ArrayList CurvesToInsert = new ArrayList();
            foreach (Cluster cl in clusterSet)
            {
                if (cl.Joints.Count < 2)
                {
                    double minDist = maxGap;
                    ICurve2D BestCurve = null;
                    ICollection found = clusterTree.GetObjectsInsideRect(new BoundingRect(cl.center, maxGap, maxGap));
                    foreach (Cluster clnear in found)
                    {
                        if (clnear != cl &&
                            clnear.Joints.Count < 2 &&
                            Geometry.Dist(cl.center, clnear.center) < minDist)
                        {
                            minDist = Geometry.Dist(cl.center, clnear.center);
                            BestCurve = new Line2D(cl.center, clnear.center);
                        }
                    }
                    if (BestCurve != null) CurvesToInsert.Add(BestCurve);
                }
            }
            foreach (ICurve2D curve in CurvesToInsert)
            {
                Insert(curve);
            }

            // Here, additional joints could optionally be added using ICurve2D.MinDistance,
            // which could serve as true gap closers, e.g., closing a bottleneck formed
            // by two circular arcs.

            // Remove all dead ends
            RemoveAllDeadEnds();

            Joint[] AllJoints = SortCluster();
#if DEBUG
            DebugJoints = AllJoints;
#endif

            bool inner = (mode != ConstrHatchInside.HatchMode.hull);
            Border[] AllBorders = FindAllBorders(AllJoints, inner);
            Array.Sort(AllBorders, new BorderAreaComparer());
            // Sort by size, starting with the smallest

            if (useInnerPoint)
            {
                int bestBorder = -1;
                if (inner)
                {	// Find the smallest surrounding boundary:
                    for (int i = 0; i < AllBorders.Length; ++i)
                    {
                        if (AllBorders[i].GetPosition(innerPoint) == Border.Position.Inside)
                        {
                            bestBorder = i;
                            break;
                        }
                    }
                }
                else
                {	// Find the largest surrounding boundary:
                    // Iterate backwards through the array
                    for (int i = AllBorders.Length - 1; i >= 0; --i)
                    {
                        if (AllBorders[i].GetPosition(innerPoint) == Border.Position.Inside)
                        {
                            bestBorder = i;
                            break;
                        }
                    }
                }
                if (bestBorder >= 0)
                {
                    if (mode == ConstrHatchInside.HatchMode.excludeHoles)
                    {
                        // Only consider the smaller borders, as the larger ones cannot be holes
                        SimpleShape ss = new SimpleShape(AllBorders[bestBorder]);
                        CompoundShape cs = new CompoundShape(ss);
                        for (int j = 0; j < bestBorder; ++j)
                        {
                            cs.Subtract(new SimpleShape(AllBorders[j]));
                            //BorderOperation bo = new BorderOperation(AllBorders[bestBorder], AllBorders[j]);
                            //if (bo.Position == BorderOperation.BorderPosition.b1coversb2)
                            //{
                            //    holes.Add(AllBorders[j]);
                            //}
                        }
                        return cs;
                    }
                    else
                    {
                        SimpleShape ss = new SimpleShape(AllBorders[bestBorder]);
                        CompoundShape cs = new CompoundShape(ss);
                        return cs;
                    }
                }
            }
            else
            {
                // If "useInnerPoint" is not enabled, return the first (largest) border

                if (AllBorders.Length == 0)
                    return null;
                
                if (AllBorders.Length == 1)                    
                    return new CompoundShape(new SimpleShape(AllBorders[0]));                    
                
                // If there is more than one border
                Array.Reverse(AllBorders); // The largest one first
                List<Border> toIterate = new List<Border>(AllBorders);

                if (!partInPart) // Here, parts that lie within other parts are removed
                {
                    CompoundShape res = new CompoundShape();
                    while (toIterate.Count > 0)
                    {
                        SimpleShape ss = new SimpleShape(toIterate[0]);
                        CompoundShape cs = new CompoundShape(ss);
                        // The first is the edge, the following are the holes
                        for (int i = toIterate.Count - 1; i > 0; --i)
                        {
                            SimpleShape ss1 = new SimpleShape(toIterate[i]);
                            if (SimpleShape.GetPosition(ss, ss1) == SimpleShape.Position.firstcontainscecond)
                            {
                                cs.Subtract(ss1);
                                toIterate.RemoveAt(i);
                            }
                        }
                        toIterate.RemoveAt(0);
                        res = CompoundShape.Union(res, cs);
                    }
                    return res;
                }
                else // Here, parts within other parts are returned as a new SimpleShape
                {
                    CompoundShape cs = new CompoundShape(new SimpleShape(toIterate[0]));
                    // From large to small
                    for (int i = 1; i < toIterate.Count; i++)
                    {
                        SimpleShape innerShape = new SimpleShape(toIterate[i]);

                        // Determine the position of the innerShape
                        var shapePos = SimpleShape.GetPosition(cs.SimpleShapes[0], innerShape);

                        switch (shapePos)
                        {
                            case SimpleShape.Position.firstcontainscecond:
                                // Cut innerShape from outerShape because it is completely inside
                                cs.Subtract(innerShape); 
                                break;
                            case SimpleShape.Position.intersecting:
                                // If parts overlap, they are merged
                                cs = CompoundShape.Union(cs, new CompoundShape(innerShape)); 
                                break;                                
                            case SimpleShape.Position.disjunct:
                                // The parts are completely independent
                                
                                bool shapeHandled = false;
                                // But maybe the part is inside one of the other SimpleShapes?
                                for (int j = 1; j < cs.SimpleShapes.Length; j++)
                                {
                                    var shapePos2 = SimpleShape.GetPosition(cs.SimpleShapes[j], innerShape);
                                    if (shapePos2 == SimpleShape.Position.firstcontainscecond)
                                    {
                                        cs.Subtract(innerShape);
                                        shapeHandled = true;
                                        break;
                                    }
                                    else if (shapePos2 == SimpleShape.Position.intersecting)
                                    {
                                        cs = CompoundShape.Union(cs, new CompoundShape(innerShape));
                                        shapeHandled = true;
                                        break;
                                    }
                                }
                                if (!shapeHandled)
                                    cs.UniteDisjunct(innerShape);
                                break;
                        }
                    }
                    return cs;
                }
            }
            // What should we return if useInnerPoint is not provided and multiple borders were found?
            return null;
        }
    }
}
