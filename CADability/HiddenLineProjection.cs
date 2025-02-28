using CADability.Attribute;
using CADability.Curve2D;
using CADability.GeoObject;
using CADability.Shapes;
using System;
using System.Collections.Generic;

namespace CADability
{
    class HiddenLineProjection
    {
        Style outlineStyle, innerEdgeStyle, hiddenLineStyle;
        bool removeHiddenLines;
        Projection projection;
        GeoObjectList input;

        // QuadTree<

        class ProjectedFace : IQuadTreeInsertable
        {
            SimpleShape shape;
            Face face;

            public ProjectedFace(Face face, Projection projection)
            {   // hier muss sichergestellt sein, dass das Face keine Umrandungskante hat, also nicht 
                // "nach hinten gebogen" ist.
                // Exception, falls was nicht geht fehlt noch
                this.face = face;
                List<ICurve2D> outline2d = new List<ICurve2D>();
                foreach (Edge edge in face.OutlineEdges)
                {
                    if (edge.Curve3D != null)
                    {
                        ICurve2D c2d = edge.Curve3D.GetProjectedCurve(projection.ProjectionPlane);
                        if (c2d != null) outline2d.Add(c2d);
                    }
                }
                Border bdr = Border.FromOrientedList(outline2d.ToArray());
                List<Border> holes = new List<Border>();
                for (int i = 0; i < face.HoleCount; i++)
                {
                    List<ICurve2D> holecurves = new List<ICurve2D>();
                    foreach (Edge edge in face.HoleEdges(i))
                    {
                        if (edge.Curve3D != null)
                        {
                            ICurve2D c2d = edge.Curve3D.GetProjectedCurve(projection.ProjectionPlane);
                            if (c2d != null) holecurves.Add(c2d);
                        }
                    }
                    Border hole = Border.FromOrientedList(holecurves.ToArray());
                    holes.Add(hole);
                }
                shape = new SimpleShape(bdr, holes.ToArray());
            }
            #region IQuadTreeInsertable Members

            BoundingRect IQuadTreeInsertable.GetExtent()
            {
                return shape.GetExtent();
            }

            bool IQuadTreeInsertable.HitTest(ref BoundingRect rect, bool includeControlPoints)
            {
                return shape.HitTest(ref rect);
            }

            object IQuadTreeInsertable.ReferencedObject
            {
                get { return this; }
            }

            #endregion
        }

        class ProjectedEdge : IQuadTreeInsertable
        {
            public ProjectedEdge(Edge edge, Projection projection)
            {
            }

            #region IQuadTreeInsertable Members

            BoundingRect IQuadTreeInsertable.GetExtent()
            {
                throw new NotImplementedException();
            }

            bool IQuadTreeInsertable.HitTest(ref BoundingRect rect, bool includeControlPoints)
            {
                throw new NotImplementedException();
            }

            object IQuadTreeInsertable.ReferencedObject
            {
                get { throw new NotImplementedException(); }
            }

            #endregion
        }

        public HiddenLineProjection(GeoObjectList input, Projection projection)
        {
            this.input = input;
            this.projection = projection;
        }
        public Style OutlineStyle
        {
            get
            {
                return outlineStyle;
            }
            set
            {
                outlineStyle = value;
            }
        }
        public Style InnerEdgeStyle
        {
            get
            {
                return innerEdgeStyle;
            }
            set
            {
                innerEdgeStyle = value;
            }
        }
        public Style HiddenLineStyle
        {
            get
            {
                return hiddenLineStyle;
            }
            set
            {
                hiddenLineStyle = value;
            }
        }
        public bool RemoveHiddenLines
        {
            get
            {
                return removeHiddenLines;
            }
            set
            {
                removeHiddenLines = value;
            }
        }
        public GeoObjectList GetResult()
        {
            return null;
        }
    }
}
