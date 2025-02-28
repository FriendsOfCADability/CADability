using CADability.Attribute;
using CADability.Curve2D;
using CADability.Shapes;
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using Wintellect.PowerCollections;

namespace CADability.GeoObject
{
    /// <summary>
    /// A class with static methods to create and combine <see cref="Solid"/>s, <see cref="Shell"/>s or <see cref="Face"/>s.
    /// </summary>

    public class Make3D
    {
        public enum SolidModellingMode { unite, subtract, unchanged };
        internal static Make3D MakeFace(Path path)
        {
            throw new NotImplementedException();
        }
        internal static IGeoObject MakeFace(Path p, Project project, bool splitCircle)
        {
            // Achtung: wenn der Path aus nur einem Kreis besteht, dann entsteht z.Z. daraus eine NURBS Fläche,
            // die aber angeblich nicht periodisch ist. Das macht ein Problem. Deshlab machen wir hier einen
            // Pfad aus zwei Halbkreisen. Wenn die Torusfläche implementiert ist, sollte diese Einschränkung
            // wieder fallen
            // Diese Methode wieder entfernen, wenn es den Torus gibt.
            if (p.CurveCount == 1)   // nur ein Objekt und das ist geschlossen
            {
                Path subst = Path.Construct();
                subst.Set(p.Curves[0].Split(0.5));
                if (subst.CurveCount == 2) p = subst;
            }
            if (p.GetPlanarState() == PlanarState.Planar)
            {
                Plane pln = p.GetPlane();
                PlaneSurface ps = new PlaneSurface(pln);
                Path2D p2d = p.GetProjectedCurve(pln) as Path2D;
                SimpleShape ss = new SimpleShape(p2d.MakeBorder());
                return Face.MakeFace(ps, ss);
            }
            else
            {   // we should try to find cylinder, sphere cone or torus from the provided path
                return null;
            }
            throw new NotImplementedException();
        }
        internal static Make3D MakeRevolution(Path path, GeoPoint location, GeoVector direction, double sweep)
        {   // nicht mehr verwenden, ersetzt durch MakeRevolution(IGeoObject...) (siehe unten)
            throw new NotImplementedException();
        }
        /// <summary>
        /// PRELIMINARY: Creates a solid as an offset to the provided <paramref name="solid"/>. Positiv values for <paramref name="offset"/>
        /// expand the solid, negative values contract it. Might be changed in future, especially the result 
        /// will be an array, because contracting might produce several solids.
        /// </summary>
        /// <param name="solid">Solid to expand or contract</param>
        /// <param name="offset">Offset for the operation</param>
        /// <returns>The resulting solid</returns>
        public static Solid MakeOffset(Solid solid, double offset)
        {
            throw new NotImplementedException();
        }
        public static IGeoObject MakeOffset(Shell shell, double offset)
        {
            throw new NotImplementedException();
        }
        /// <summary>
        /// Creates a <see cref="Solid"/>, a <see cref="Shell"/> or a <see cref="Face"/> by rotating a <see cref="Path"/>, a <see cref="Face"/>
        /// or a <see cref="Shell"/> around a given axis. Rotating a path yields a shell, rotating a face or a shell
        /// returns a <see cref="Solid"/>. Rotating a different type of <see cref="IGeoObject"/> returns null.
        /// </summary>
        /// <param name="faceShellOrPath">object to rotate</param>
        /// <param name="location">a point on the axis</param>
        /// <param name="direction">direction of the axis</param>
        /// <param name="sweep">amount to rotate (2*Math.PI is a full rotation)</param>
        /// <param name="project">the <see cref="Project"/> to set default styles for the result</param>
        /// <returns>the created solid or shell</returns>
        public static IGeoObject MakeRevolution(IGeoObject faceShellOrPath, GeoPoint location, GeoVector direction, double sweep, Project project)
        {
#if DEBUG
            if (faceShellOrPath is Face)
            {
                Path pth = Path.Construct();
                Face fc = (faceShellOrPath as Face);
                for (int i = 0; i < fc.OutlineEdges.Length; i++)
                {
                    if (fc.OutlineEdges[i].Forward(fc)) pth.Add(fc.OutlineEdges[i].Curve3D.Clone());
                    else
                    {
                        ICurve crv = fc.OutlineEdges[i].Curve3D.Clone();
                        crv.Reverse();
                        pth.Add(crv);
                    }
                }
                IGeoObject res = Rotate(pth, new Axis(location, direction), sweep, SweepAngle.Deg(0.0), project);
                if (res != null) return res;
            }
#endif
            throw new NotImplementedException();
        }
        public static Solid MakePrismMiter(SimpleShape outline, GeoPoint[] path, GeoVector topDirection, GeoVector mainDirection, GeoVector startMiter, GeoVector endMiter)
        {
            return null;
        }
        /// <summary>
        /// Creates a prism or profile as an extrusion of the provided shape. The position of the prism
        /// is defined by location and mainDirection. The shape is a 2D object. The 2d origin is moved along
        /// the axis defined by location and main direction. The topDirection vector corresponds to the
        /// 2d y-axis of the shape. The beginning and ending of the extrusion is a miter cut that allows
        /// the same profile be connected to the starting or ending point where mainDirection and startMiter
        /// are exchanged and topDirection is kept.
        /// </summary>
        /// <param name="shape">2d shape to be extruded, the 2d origin is moved along mainDirection</param>
        /// <param name="location">Starting point of the resulting profile, corresponds to the 2d origin</param>
        /// <param name="topDirection">The "up" direction for the resulting profile, corresponds to the y-axis of the 2d shape</param>
        /// <param name="mainDirection">Direction of the prism, the length of this vector defines the length of the prism</param>
        /// <param name="startMiter">Direction of the connecting profile at the starting point</param>
        /// <param name="endMiter">Direction of the connecting profile at the ending point</param>
        /// <returns>The resulting solid</returns>
        public static Solid MakePrismMiter(SimpleShape shape, GeoPoint location, GeoVector topDirection, GeoVector mainDirection, GeoVector startMiter, GeoVector endMiter)
        {
            GeoVector xdir = topDirection ^ mainDirection;
            GeoVector ydir = mainDirection ^ xdir;
            Plane mainPlane = new Plane(location, xdir, ydir);
            Path p3d = shape.Outline.MakePath(mainPlane);
            // SplitArcs(p3d);
            ICurve[] curves = p3d.Curves;
            Edge[] extruded = new Edge[curves.Length]; // die Kanten parallel zu mainDirection
            Edge[] startEdges = new Edge[curves.Length]; // die Kanten auf der Startebene
            Edge[] endEdges = new Edge[curves.Length]; // die Kanten auf der Endebene
            ICurve[][] holes = new ICurve[shape.NumHoles][];
            Edge[][] startHolesEdges = new Edge[shape.NumHoles][];
            Edge[][] endHolesEdges = new Edge[shape.NumHoles][];
            for (int i = 0; i < shape.NumHoles; i++)
            {
                p3d = shape.Hole(i).MakePath(mainPlane);
                // SplitArcs(p3d);
                (p3d as ICurve).Reverse(); // Löcher umdrehen
                startHolesEdges[i] = new Edge[p3d.CurveCount];
                endHolesEdges[i] = new Edge[p3d.CurveCount];
                holes[i] = p3d.Curves;
            }
            Plane startMiterPlane = new Plane(location, startMiter ^ mainDirection, GeoVector.Bisector(mainDirection, startMiter));
            startMiterPlane.Reverse();
            Plane endMiterPlane = new Plane(location + mainDirection, endMiter ^ mainDirection, GeoVector.Bisector(-mainDirection, endMiter));
            Face[] sides = new Face[curves.Length];
            GeoPoint[] src = new GeoPoint[] { mainPlane.Location, mainPlane.ToGlobal(new GeoPoint(1, 0, 0)), mainPlane.ToGlobal(new GeoPoint(0, 1, 0)) };
            GeoPoint[] dst = new GeoPoint[] { startMiterPlane.Intersect(src[0], mainDirection), startMiterPlane.Intersect(src[1], mainDirection), startMiterPlane.Intersect(src[2], mainDirection) };
            ModOp toStartMiter = ModOp.Fit(src, dst, true);
            dst = new GeoPoint[] { endMiterPlane.Intersect(src[0], mainDirection), endMiterPlane.Intersect(src[1], mainDirection), endMiterPlane.Intersect(src[2], mainDirection) };
            ModOp toEndMiter = ModOp.Fit(src, dst, true);
            Face lower = Face.Construct();
            Face upper = Face.Construct();
            List<Face> allFaces = new List<Face>();
#if DEBUG
            DebuggerContainer dc = new DebuggerContainer();
#endif
            for (int i = 0; i < curves.Length; i++)
            {
                sides[i] = Face.Construct();
                Line line = Line.Construct();
                line.SetTwoPoints(toStartMiter * curves[i].StartPoint, toEndMiter * curves[i].StartPoint);
                extruded[i] = new Edge(sides[i], line);
                startEdges[i] = new Edge(lower, curves[i].CloneModified(toStartMiter));
                endEdges[i] = new Edge(upper, curves[i].CloneModified(toEndMiter));
                allFaces.Add(sides[i]);
#if DEBUG
                dc.Add(startEdges[i].Curve3D as IGeoObject);
                dc.Add(endEdges[i].Curve3D as IGeoObject);
#endif
            }
            for (int i = 0; i < curves.Length; i++)
            {
                int i1 = i + 1;
                if (i1 >= curves.Length) i1 = 0;
                ISurface surface = ExtrudedSurface(curves[i], mainDirection);
                if (surface is CylindricalSurface)
                {
                    // um eine NonPeriodicCylindricalSurface zu machen brauchen wir ein vmin
                    // Eine Ebene durch mainDirection und die Senkrechte zu mainDirection und Ellipsenebene
                    // in die wird die Ellipse projiziert. 
                    if (startEdges[i].Curve3D is Ellipse)
                    {   // muss ja wohl so sein
                        Plane pln = new Plane((startEdges[i].Curve3D as Ellipse).Center, mainDirection, (startEdges[i].Curve3D as Ellipse).Normal);
                        ICurve2D c2d = startEdges[i].Curve3D.GetProjectedCurve(pln);
                        BoundingRect ext = c2d.GetExtent();
                        double vmin = ext.Left; // die Ebene geht mit ihrer x-Achse in mainDirection
                        double vmax = mainDirection.Length; // stimmt zwar nicht, ist aber egal
                        surface = new NonPeriodicCylindricalSurface(surface as CylindricalSurface, vmin, vmax);
                    }
                }
                sides[i].Set(surface, new Edge[] { extruded[i], startEdges[i], extruded[i1], endEdges[i] }, null);
                extruded[i].SetFace(sides[i], false);
                extruded[i1].SetFace(sides[i], true);
                endEdges[i].SetFace(sides[i], false);
                startEdges[i].SetFace(sides[i], true);
                if (surface.IsUPeriodic || surface.IsVPeriodic) sides[i].CheckPeriodic();
            }
            for (int j = 0; j < shape.NumHoles; j++)
            {
                extruded = new Edge[holes[j].Length];
                sides = new Face[holes[j].Length];
                for (int i = 0; i < holes[j].Length; i++)
                {
                    sides[i] = Face.Construct();
                    Line line = Line.Construct();
                    line.SetTwoPoints(toStartMiter * holes[j][i].StartPoint, toEndMiter * holes[j][i].StartPoint);
                    extruded[i] = new Edge(sides[i], line);

                    startHolesEdges[j][i] = new Edge(lower, holes[j][i].CloneModified(toStartMiter));
                    endHolesEdges[j][i] = new Edge(upper, holes[j][i].CloneModified(toEndMiter));
#if DEBUG
                    dc.Add(startHolesEdges[j][i].Curve3D as IGeoObject);
                    dc.Add(endHolesEdges[j][i].Curve3D as IGeoObject);
#endif
                    allFaces.Add(sides[i]);
                }
                for (int i = 0; i < holes[j].Length; i++)
                {
                    int i1 = i + 1;
                    if (i1 >= holes[j].Length) i1 = 0;
                    ISurface surface = ExtrudedSurface(holes[j][i], mainDirection);
                    if (surface is CylindricalSurface)
                    {
                        // um eine NonPeriodicCylindricalSurface zu machen brauchen wir ein vmin
                        // Eine Ebene durch mainDirection und die Senkrechte zu mainDirection und Ellipsenebene
                        // in die wird die Ellipse projiziert. 
                        if (startHolesEdges[j][i].Curve3D is Ellipse)
                        {   // muss ja wohl so sein
                            Plane pln = new Plane((startHolesEdges[j][i].Curve3D as Ellipse).Center, mainDirection, mainDirection ^ (startHolesEdges[j][i].Curve3D as Ellipse).Normal);
                            ICurve2D c2d = startHolesEdges[j][i].Curve3D.GetProjectedCurve(pln);
                            BoundingRect ext = c2d.GetExtent();
                            double vmin = ext.Left; // die Ebene geht mit ihrer x-Achse in mainDirection
                            double vmax = mainDirection.Length; // stimmt zwar nicht, ist aber egal
                            surface = new NonPeriodicCylindricalSurface(surface as CylindricalSurface, vmin, vmax);
                        }
                    }
                    sides[i].Set(surface, new Edge[] { extruded[i], startHolesEdges[j][i], extruded[i1], endHolesEdges[j][i] }, null);
                    extruded[i].SetFace(sides[i], false);
                    extruded[i1].SetFace(sides[i], true);
                    endHolesEdges[j][i].SetFace(sides[i], false);
                    startHolesEdges[j][i].SetFace(sides[i], true);
                    if (surface.IsUPeriodic || surface.IsVPeriodic) sides[i].CheckPeriodic();
                }
            }
            lower.Set(new PlaneSurface(startMiterPlane), startEdges, startHolesEdges);
            upper.Set(new PlaneSurface(endMiterPlane), endEdges, endHolesEdges);
            for (int i = 0; i < startEdges.Length; i++)
            {
                startEdges[i].SetFace(lower, false);
                endEdges[i].SetFace(upper, true);
            }
            for (int i = 0; i < startHolesEdges.Length; i++)
            {
                for (int j = 0; j < startHolesEdges[i].Length; j++)
                {
                    startHolesEdges[i][j].SetFace(lower, false);
                    endHolesEdges[i][j].SetFace(upper, true);
                }
            }
            allFaces.Add(lower);
            allFaces.Add(upper);
            Shell sh = Shell.Construct();
            sh.SetFaces(allFaces.ToArray());
            sh.AssertOutwardOrientation(); // das könnte men sich sparen, wenn man gleich auf die Orientierung achten würde
            Solid sld = Solid.Construct();
            sld.SetShell(sh);

            return sld;
        }

        private static void SplitArcs(Path p3d)
        {   // für die extrusion dürfen Bögen nicht über die Naht gehen
            List<ICurve> curves = new List<ICurve>(p3d.Curves);
            bool splitted = false;
            for (int i = curves.Count - 1; i >= 0; --i)
            {
                if (curves[i] is Ellipse)
                {
                    Ellipse[] e = (curves[i] as Ellipse).SplitAtZero();
                    if (e != null)
                    {
                        curves.RemoveAt(i);
                        curves.InsertRange(i, e);
                        splitted = true;
                    }
                }
            }
            if (splitted)
            {
                p3d.Set(curves.ToArray());
            }
        }
        /// <summary>
        /// Creates a <see cref="Solid"/>, a <see cref="Shell"/> or a <see cref="Face"/> by moving a <see cref="Path"/>, a <see cref="Face"/>
        /// or a <see cref="Shell"/> along a given vector. Moving a path yields a face or a shell, moving a face or a shell
        /// returns a <see cref="Solid"/>. Moving a different type of <see cref="IGeoObject"/> returns null.
        /// </summary>
        /// <param name="faceShellOrPath">object to move</param>
        /// <param name="vect">vector along which the object is to be moved</param>
        /// <param name="project">the <see cref="Project"/> to set default styles for the result</param>
        /// <returns>the created solid, face or shell</returns>
        public static IGeoObject MakePrism(IGeoObject faceShellOrPath, GeoVector extrusion, Project project, bool pathToShell = true)
        {
            // ohne OpenCascade einige Fälle implementieren. Ziel ist es alle Fälle ohne OpenCascade zu implementieren
            if (faceShellOrPath is ICurve)
            {
                if ((faceShellOrPath as ICurve).GetPlanarState() == PlanarState.Planar && (faceShellOrPath as ICurve).IsClosed)
                {
                    if (faceShellOrPath is Path) (faceShellOrPath as Path).Flatten();
                    // Polylinien sollten so auch gehen
                    ICurve curve = (faceShellOrPath.Clone() as ICurve); // wir drehen evtl die Richtung um, deshalb Clone
                    if (curve.SubCurves != null && curve.SubCurves.Length > 0)
                    {
                        if (curve.SubCurves.Length == 1)
                        {   // eine einzige geschlossene Kurve, das ist schlecht wg. der Kanten
                            ICurve[] splitted = curve.SubCurves[0].Split(0.5);
                            Path path = Path.Construct();
                            path.Set(splitted);
                            curve = path;
                        }
                        List<Edge> mainEdges = new List<Edge>();
                        Face lower, upper;
                        lower = Face.Construct();
                        upper = Face.Construct();
                        Plane pln = curve.GetPlane();
                        // Orientierung testen:
                        if (pln.Normal * extrusion > 0)
                        {   // Normalenvektor der Ebene und Auszugsrichtung zeigen in die selbe Richtung, 
                            // zum Schluss drehen wir das Face um
                        }
                        else
                        {
                            pln.Reverse();
                        }
                        ICurve2D projected = curve.GetProjectedCurve(pln);
                        bool ccw = Border.CounterClockwise(new ICurve2D[] { projected });
                        if (!ccw) curve.Reverse();
                        PlaneSurface pls = new PlaneSurface(pln);
                        for (int i = 0; i < curve.SubCurves.Length; ++i)
                        {
                            Edge e = new Edge(lower, curve.SubCurves[i].Clone(), lower, pls.GetProjectedCurve(curve.SubCurves[i], Precision.eps), true);
                            mainEdges.Add(e);
                        }
                        lower.Set(pls, mainEdges.ToArray(), null);
                        // lower normalenvektor zeigt jetzt in Richtung von extrusion, also nach innen. Wird nachher umgedreht.
                        upper = lower.Clone() as Face;
                        upper.Modify(ModOp.Translate(extrusion));
                        // ist die outline hier und dort synchron?
                        Edge[] extruded = new Edge[lower.OutlineEdges.Length];
                        Face[] sides;
                        if (pathToShell) sides = new Face[lower.OutlineEdges.Length]; // nur Hülle
                        else sides = new Face[lower.OutlineEdges.Length + 2]; // oben und unten noch dazu
                        for (int i = 0; i < lower.OutlineEdges.Length; ++i)
                        {
                            sides[i] = Face.Construct();
                            Line l = Line.Construct();
                            l.SetTwoPoints(lower.OutlineEdges[i].Curve3D.EndPoint, upper.OutlineEdges[i].Curve3D.EndPoint);
                            extruded[i] = new Edge(sides[i], l);
                        }
                        for (int i = 0; i < lower.OutlineEdges.Length; ++i)
                        {
                            int i1 = i - 1;
                            if (i1 < 0) i1 = lower.OutlineEdges.Length - 1;
                            ISurface surface = ExtrudedSurface(lower.OutlineEdges[i].Curve3D, extrusion);
                            sides[i].Set(surface, new Edge[] { extruded[i1], lower.OutlineEdges[i], extruded[i], upper.OutlineEdges[i] }, null);
                            lower.OutlineEdges[i].SetFace(sides[i], true);
                            upper.OutlineEdges[i].SetFace(sides[i], false);
                            // die senkrechten Linien. Beim Zylinder auf der 0/2pi Naht gibts manchmal Fehler
                            GeoPoint2D sp = lower.OutlineEdges[i].Curve2D(sides[i]).StartPoint;
                            GeoPoint2D ep = upper.OutlineEdges[i].Curve2D(sides[i]).EndPoint;
                            extruded[i1].SetFace(sides[i], new Line2D(sp, ep), false);
                            sp = lower.OutlineEdges[i].Curve2D(sides[i]).EndPoint;
                            ep = upper.OutlineEdges[i].Curve2D(sides[i]).StartPoint;
                            extruded[i].SetFace(sides[i], new Line2D(sp, ep), true);
                        }
                        lower.MakeInverseOrientation(); // die untere Fläche war ja falschrum
                        lower.OrientedOutward = true; // das MakeInverseOrientation setzt OrientedOutward auf false, in unserem Fall ist das falsch
                        if (!pathToShell)
                        {
                            sides[lower.OutlineEdges.Length] = lower;
                            sides[lower.OutlineEdges.Length + 1] = upper;
                        }
                        Shell sh = Shell.Construct();
                        sh.SetFaces(sides);
                        if (pathToShell)
                        {
                            if (project != null) project.SetDefaults(sh);
                            return sh;
                        }
                        else
                        {
                            // sh.AssertOutwardOrientation(); // das könnte men sich sparen, wenn man gleich auf die Orientierung achten würde
                            Solid sld = Solid.Construct();
                            sld.SetShell(sh);
                            if (project != null) project.SetDefaults(sld);
                            return sld;
                        }
                    }
                }
            }
            else if (faceShellOrPath is Face fc)
            {
                if (fc.Area.HasSingleSegmentBorder())
                {
                    // nur eine geschlossene Kante. Besser zwei Kanten machen
                    SimpleShape ss = fc.Area.Clone();
                    ss.RemoveSingleSegmentBorders();
                    fc = Face.MakeFace(fc.Surface.Clone(), ss);
                }
                fc.MakeTopologicalOrientation();
                bool mustBeCloned = false;
                foreach (Edge edg in fc.AllEdges)
                {
                    if (edg.SecondaryFace != null) mustBeCloned = true;
                }
                if (mustBeCloned) fc = fc.Clone() as Face; // neue Edges
                
                return Solid.MakeSolid(MakeBrutePrism(fc, extrusion));

                //Unreachable code
                /*
                GeoVector normal = fc.Surface.GetNormal(fc.Area.GetExtent().GetCenter());
                if (normal * extrusion > 0)
                {
                    // zum Schluss drehen wir das Face um
                }
                else
                {   // das Face umdrehen, damit die Richtungen der Kanten stimmen
                    fc.MakeInverseOrientation();
                    fc.OrientedOutward = true;
                    // am Ende wird das face wieder umgedreht
                }
                // beide Faces sind jetzt erstmal gleich orientiert, das "untere" ist somit falschrum und wird erst ganz zum Schluss umgedreht
                foreach (Edge edg in fc.AllEdges)
                {
                    if (!edg.Forward(fc))
                    {
                        edg.Curve3D.Reverse();
                        edg.ReverseOrientation(fc);
                    }
                }
                Edge[] lowerEdges = fc.AllEdges;
                Vertex[] lowerVertices = fc.Vertices;
                for (int i = 0; i < lowerEdges.Length; ++i)
                {
                    lowerEdges[i].Orient();
                }
                // lower normalenvektor zeigt jetzt in Richtung von extrusion, also nach innen. Wird nachher umgedreht.
                Face upper = fc.Clone() as Face;
                upper.Modify(ModOp.Translate(extrusion));
                Edge[] upperEdges = upper.AllEdges;
                Vertex[] upperVertices = upper.Vertices;
                for (int i = 0; i < upperEdges.Length; ++i)
                {
                    upperEdges[i].Orient();
                }
                // die Vertices sind nicht synchron, wohl aber die Edges
                Dictionary<Vertex, Edge> extruded = new Dictionary<Vertex, Edge>();
                Face[] sides = new Face[lowerEdges.Length + 2]; // pro neuer Kante eine neue Seite plus Boden und Deckel
                // die neuen Kanten erzeugen, gleich mit den richtigen Vertices
#if DEBUG
                DebuggerContainer dc = new DebuggerContainer();
#endif
                for (int i = 0; i < lowerEdges.Length; ++i)
                {
                    Line l = Line.Construct();
                    Vertex sp, ep;
                    sp = lowerEdges[i].EndVertex(fc);
                    ep = upperEdges[i].EndVertex(upper);
                    l.SetTwoPoints(sp.Position, ep.Position);
                    extruded[sp] = new Edge(null, l);
                    extruded[sp].Vertex1 = sp;
                    extruded[sp].Vertex2 = ep;
#if DEBUG
                    dc.Add(l, i);
#endif
                }
                // die neuen Seiten erzeugen, richtig orientiert
                for (int i = 0; i < lowerEdges.Length; ++i)
                {
                    Face side = Face.Construct();
                    sides[i] = side;
                    ISurface surface = ExtrudedSurface(lowerEdges[i].Curve3D, extrusion);
                    // if (surface is SurfaceOfLinearExtrusion) surface.SetBounds(new BoundingRect(0.0, 0.0, 1.0, 1.0)); wird bei ExtrudedSurface schon gemacht
                    // obige Zeile, damit es bei PositioOf, SetFace oder wenn immer die BoxedSurface gebraucht wird kein Problem gibt
                    //Vertex lsv = lowerEdges[i].StartVertex(fc);
                    //Vertex lev = lowerEdges[i].EndVertex(fc);
                    //Vertex usv = upperEdges[i].StartVertex(upper);
                    //Vertex uev = upperEdges[i].EndVertex(upper);
                    //GeoPoint2D pp1 = surface.PositionOf(lsv.Position);
                    //GeoPoint2D pp2 = surface.PositionOf(lev.Position);
                    //GeoPoint2D pp3 = surface.PositionOf(usv.Position);
                    //GeoPoint2D pp4 = surface.PositionOf(uev.Position);
                    //side.Set(surface, new Edge[] { extruded[lowerEdges[i].StartVertex(fc)], lowerEdges[i], extruded[lowerEdges[i].EndVertex(fc)], upperEdges[i] }, null, true);
#if DEBUG
                    Face dbgside = Face.MakeFace(surface, new SimpleShape(Border.MakeRectangle(BoundingRect.UnitBoundingRect)));
#endif


                    bool fwd = lowerEdges[i].Forward(fc);
                    GeoVector curveDir;
                    GeoPoint startPoint;
                    if (fwd)
                    {
                        curveDir = lowerEdges[i].Curve3D.StartDirection;
                        startPoint = lowerEdges[i].Curve3D.StartPoint;
                    }
                    else
                    {
                        curveDir = -lowerEdges[i].Curve3D.EndDirection;
                        startPoint = lowerEdges[i].Curve3D.EndPoint;
                    }
                    GeoVector norm = fc.Surface.GetNormal(lowerEdges[i].Curve2D(fc).StartPoint);
                    GeoVector toOutside = curveDir ^ norm;
                    bool reverse = surface.GetNormal(surface.PositionOf(startPoint)) * toOutside < 0;
                    if (reverse) surface.ReverseOrientation();
                    side.Set(surface, new Edge[] { extruded[lowerEdges[i].StartVertex(fc)], lowerEdges[i], extruded[lowerEdges[i].EndVertex(fc)], upperEdges[i] }, null, false);
                    //extruded[lowerEdges[i].StartVertex(fc)].SetFace(side, new Line2D(new GeoPoint2D(0.0, 0.0), new GeoPoint2D(0.0, 1.0)), false);
                    //extruded[lowerEdges[i].EndVertex(fc)].SetFace(side, new Line2D(new GeoPoint2D(1.0, 0.0), new GeoPoint2D(1.0, 1.0)), true);
                    // stimmte nicht, bei Zylinder nicht von 0 bis 1!
                    if (reverse)
                    {
                        lowerEdges[i].SetFace(side, false);
                        upperEdges[i].SetFace(side, true);
                    }
                    else
                    {
                        lowerEdges[i].SetFace(side, true);
                        upperEdges[i].SetFace(side, false);
                    }
                    // die senkrechten Linien. Beim Zylinder auf der 0/2pi Naht gibts manchmal Fehler
                    GeoPoint2D sp = lowerEdges[i].Curve2D(side).StartPoint;
                    GeoPoint2D ep = upperEdges[i].Curve2D(side).EndPoint;
                    extruded[lowerEdges[i].StartVertex(fc)].SetFace(side, new Line2D(sp, ep), false);
                    sp = lowerEdges[i].Curve2D(side).EndPoint;
                    ep = upperEdges[i].Curve2D(side).StartPoint;
                    extruded[lowerEdges[i].EndVertex(fc)].SetFace(side, new Line2D(sp, ep), true);
                    if (surface.IsUPeriodic || surface.IsVPeriodic) side.CheckPeriodic(); // vermutlich nicht mehr nötig, mit Zylinder testen
                    side.Orient();
                    //if (lowerEdges[i].EndVertex(side)== extruded[lowerEdges[i].StartVertex(fc)].EndVertex(side))
                    //{
                    //    side.ReverseOrientation();
                    //    //lowerEdges[i].Curve2D(side).Reverse();
                    //    //lowerEdges[i].ReverseOrientation(side);
                    //}
                    //if (upperEdges[i].EndVertex(side) == extruded[lowerEdges[i].EndVertex(fc)].EndVertex(side))
                    //{
                    //    //upperEdges[i].Curve2D(side).Reverse();
                    //    //upperEdges[i].ReverseOrientation(side);
                    //}
#if DEBUG
                    if (!side.CheckConsistency())
                    {
                        Vertex v1 = extruded[lowerEdges[i].StartVertex(fc)].StartVertex(side);
                        Vertex v2 = extruded[lowerEdges[i].StartVertex(fc)].EndVertex(side);
                        Vertex v3 = lowerEdges[i].StartVertex(side);
                        Vertex v4 = lowerEdges[i].EndVertex(side);
                        Vertex v5 = extruded[lowerEdges[i].EndVertex(fc)].StartVertex(side);
                        Vertex v6 = extruded[lowerEdges[i].EndVertex(fc)].EndVertex(side);
                        Vertex v7 = upperEdges[i].StartVertex(side);
                        Vertex v8 = upperEdges[i].EndVertex(side);
                    }
#endif
                }
                fc.MakeInverseOrientation(); // die untere Fläche war ja falschrum
                fc.OrientedOutward = true; // das MakeInverseOrientation setzt OrientedOutward auf false, in unserem Fall ist das falsch
                sides[sides.Length - 2] = fc;
                sides[sides.Length - 1] = upper;
                Shell sh = Shell.Construct();
                sh.SetFaces(sides);
#if DEBUG
                bool ok = sh.CheckConsistency();
#endif
                sh.AssertOutwardOrientation(); // das könnte men sich sparen, wenn man gleich auf die Orientierung achten würde
                // die Kanten der Löcher erzeugen falsch orientierte faces!
#if DEBUG
                ok = sh.CheckConsistency();
#endif
                Solid sld = Solid.Construct();
                sld.SetShell(sh);
                if (project != null) project.SetDefaults(sld);
                return sld; // Testweise wieder mit OCAS, da ein Problem bei ???
                */
            }
            return null;
        }

        private static Shell MakeBrutePrism(Face lower, GeoVector extrusion)
        {
            List<Face> res = new List<Face>();
            double minLen = extrusion.Length;
            foreach (Edge edg in lower.AllEdges)
            {
                if (edg.Curve3D != null)
                {
                    minLen = Math.Min(minLen, edg.Curve3D.Length);
                    Face fc = ExtrudeCurveToFace(edg.Curve3D, extrusion, null, null, null, null, true, true, true, true);
                    res.Add(fc);
                }
            }
            res.Add(lower);
            Face upper = lower.Clone() as Face;
            upper.Modify(ModOp.Translate(extrusion));
            res.Add(upper);
            Face[] fcs = res.ToArray();
            Shell.connectFaces(fcs, minLen * 1e-3);
            Shell shell = Shell.Construct();
            shell.SetFaces(fcs);
            shell.RecalcVertices();
            shell.TryConnectOpenEdges();
            shell.AssertOutwardOrientation();
            return shell;
        }

        private static ISurface ExtrudedSurface(ICurve iCurve, GeoVector vect)
        {
            if (iCurve is Line)
            {
                return new PlaneSurface(new Plane(iCurve.StartPoint, iCurve.EndPoint, iCurve.StartPoint + vect));
            }
            else if (iCurve is Ellipse)
            {
                if (Precision.SameDirection((iCurve as Ellipse).Plane.Normal, vect, false))
                {
                    Ellipse elli = (iCurve as Ellipse);
                    // Problem: die Kurve kann über die Naht des Cylinders gehen, bitte berücksichtigen
                    return new CylindricalSurface(elli.Center, elli.MajorAxis, elli.MinorAxis, vect.Normalized);
                    // dummerweise ist hier nichts über den v-Bereich bekannt, so dass 
                    // return new NonPeriodicCylindricalSurface(cs, -vect.Length, vect.Length);
                }
            }
            SurfaceOfLinearExtrusion sle = new SurfaceOfLinearExtrusion(iCurve, vect, 0.0, 1.0);
            // die Grenzen werden für BoxedSurface gebraucht und sind ja so auch erstmal richtig
            sle.SetBounds(BoundingRect.UnitBoundingRect);
            return sle;
        }
        /// <summary>
        /// Returns the union of the two given <see cref="Solid"/>s. If the solids are disjunct, null will be returned
        /// </summary>
        /// <param name="s1">first solid</param>
        /// <param name="s2">second solid</param>
        /// <param name="project">the project to find attributes</param>
        /// <returns>union of the two solids or null</returns>
        public static Solid Union(Solid s1, Solid s2)
        {
            return Solid.Unite(s1, s2);
        }
        /// <summary>
        /// Returns the difference of the two given <see cref="Solid"/>s. If the solids are disjunct, null will be returned.
        /// s2 will be subtracted from s1.
        /// </summary>
        /// <param name="s1">first solid</param>
        /// <param name="s2">second solid</param>
        /// <param name="project">the project to find attributes</param>
        /// <returns>difference of the two solids or null</returns>
        public static Solid[] Difference(Solid s1, Solid s2)
        {
            return Solid.Subtract(s1, s2);
        }
        /// <summary>
        /// Returns the remains of a shell when a solid is subtracted.
        /// </summary>
        /// <param name="shl">Shell to be modified</param>
        /// <param name="sld">Solid which is subtracted</param>
        /// <returns>One ore more shells</returns>
        public static Shell[] Difference(Shell shl, Solid sld)
        {
            throw new NotImplementedException();
        }
        /// <summary>
        /// Returns the remains of a face when a solid is subtracted.
        /// </summary>
        /// <param name="fce">Face to be modified</param>
        /// <param name="sld">Solid which is subtracted</param>
        /// <returns>One ore more faces</returns>
        public static Face[] Difference(Face fce, Solid sld)
        {
            throw new NotImplementedException();
        }
        public static Solid[] Difference(Solid sld, Face fce)
        {
            throw new NotImplementedException();
        }
        /// <summary>
        /// Connects the two wires with a ruled surface. The two wires must have the same number of segments.
        /// Each segment of the first path is connected with the segment with the same index of the second path.
        /// Both pathes must be closed. A solid is returned bound by the faces of the two pathes and the ruled
        /// surfaces. If there is an error null will be returned
        /// </summary>
        /// <param name="firstPath">the first path</param>
        /// <param name="secondPath">teh second path</param>
        /// <param name="project">the project to find attributes</param>
        /// <returns>the generated solid or null</returns>
        public static Solid MakeRuledSolid(Path firstPath, Path secondPath, Project project)
        {
            IGeoObject res = MakeRuledShellX(firstPath, secondPath, true, project);
            if (res is Solid) return res as Solid;

            throw new NotImplementedException();
        }
        /// <summary>
        /// Connects the two wires with a ruled surface. The two wires must have the same number of segments.
        /// Each segment of the first path is connected with the segment with the same index of the second path.
        /// The pathes may be open or closed. the ruled surfaces will be returned as a shell.
        /// If there is an error null will be returned
        /// </summary>
        /// <param name="firstPath">the first path</param>
        /// <param name="secondPath">teh second path</param>
        /// <param name="project">the project to find attributes</param>
        /// <returns>the generated shell or null</returns>
        public static IGeoObject MakeRuledShell(Path firstPath, Path secondPath, Project project)
        {
            try
            {
                IGeoObject res = MakeRuledShellX(firstPath, secondPath, false, project);
                if (res != null) return res;
            }
            catch (Exception ex)
            {
                if (ex is ThreadAbortException) throw (ex);
            }
            throw new NotImplementedException();
        }
        internal static IGeoObject MakeRuledShellX(Path firstPath, Path secondPath, bool makeSolid, Project project) // ersetzt später obiges
        {
            firstPath.Flatten(); // Flatten hopefully removes too short segments
            secondPath.Flatten(); // and splits polylines to its segments
            if (firstPath.CurveCount != secondPath.CurveCount)
            {   // if one of the paths has only a single segment, we split it, so there are the same number of segments
                // We could also split it according to the length
                if (firstPath.Count == 1)
                {
                    double dp = 1.0 / secondPath.CurveCount;
                    ICurve[] splitted = new ICurve[secondPath.CurveCount];
                    for (int i = 0; i < secondPath.CurveCount; ++i)
                    {
                        splitted[i] = firstPath.Curves[0].Clone();
                        splitted[i].Trim(i * dp, (i + 1) * dp);
                    }
                    firstPath.Set(splitted);
                }
                else if (secondPath.CurveCount == 1)
                {
                    double dp = 1.0 / firstPath.CurveCount;
                    ICurve[] splitted = new ICurve[firstPath.CurveCount];
                    for (int i = 0; i < firstPath.CurveCount; ++i)
                    {
                        splitted[i] = secondPath.Curves[0].Clone();
                        splitted[i].Trim(i * dp, (i + 1) * dp);
                    }
                    secondPath.Set(splitted);
                }
                else
                {   // two path with different number of segments
                    // we would need a general rule how to split the paths
                    // if the user needs more control, she should split the two paths in part so we always have a single segment path for firstPath or second path
                    // (firstPath as ICurve).PositionAtLength(0);
                    // (firstPath as ICurve).Split()
                    return null;
                }
            }
            if (firstPath.IsClosed && firstPath.Count == 1)
            {
                firstPath.Set(firstPath.Split(0.5));
            }
            if (secondPath.IsClosed && secondPath.Count == 1)
            {
                secondPath.Set(secondPath.Split(0.5));
            }
            if (makeSolid && firstPath.IsClosed && secondPath.IsClosed && firstPath.GetPlanarState() == PlanarState.Planar && secondPath.GetPlanarState() == PlanarState.Planar)
            {
                Plane firstpln = firstPath.GetPlane();
                Plane secondpln = secondPath.GetPlane();
                if (firstpln.Normal * secondpln.Normal < 0) secondpln.Reverse();
                Plane pln = new Plane(new GeoPoint(firstpln.Location, secondpln.Location), new GeoVector(firstpln.Normal, secondpln.Normal));
                double a1 = Border.FromOrientedList((firstPath.GetProjectedCurve(pln) as Path2D).SubCurves, true).Area;
                double a2 = Border.FromOrientedList((secondPath.GetProjectedCurve(pln) as Path2D).SubCurves, true).Area;
                if (Math.Sign(a1) != Math.Sign(a2)) (secondPath as ICurve).Reverse();
            }
            if (firstPath.IsClosed && secondPath.IsClosed)
            {   // find the shortest connection of the vertices of the two paths. 
                // 
                double mindist = double.MaxValue;
                int secondStartInd = -1;
                for (int i = 0; i < firstPath.CurveCount; ++i)
                {
                    double d = 0.0;
                    for (int j = 0; j < firstPath.CurveCount; ++j)
                    {
                        int ind = i + j;
                        if (ind >= firstPath.CurveCount) ind -= firstPath.CurveCount;
                        d += firstPath.Curves[j].StartPoint | secondPath.Curves[ind].StartPoint;
                    }
                    if (d < mindist)
                    {
                        mindist = d;
                        secondStartInd = i;
                    }
                }
                // firstPath.CyclicalPermutation(firstStartInd);
                secondPath.CyclicalPermutation(secondStartInd);
            }

            Edge[] edges = new Edge[firstPath.CurveCount + 1];
            for (int i = 0; i < firstPath.CurveCount; ++i)
            {
                Line l3d = Line.Construct();
                l3d.SetTwoPoints(firstPath.Curve(i).StartPoint, secondPath.Curve(i).StartPoint);
                edges[i] = new Edge(null, l3d);
            }
            if (firstPath.IsClosed && secondPath.IsClosed)
            {
                edges[edges.Length - 1] = edges[0];
            }
            else
            {
                Line l3d = Line.Construct();
                l3d.SetTwoPoints(firstPath.Curve(firstPath.CurveCount - 1).EndPoint, secondPath.Curve(firstPath.CurveCount - 1).EndPoint);
                edges[edges.Length - 1] = new Edge(null, l3d);
            }
            Shell shell = Shell.Construct();
            List<Face> faces = new List<Face>(firstPath.CurveCount);
            List<Edge> upperEdges = new List<Edge>();
            List<Edge> lowerEdges = new List<Edge>();

            for (int i = 0; i < firstPath.CurveCount; ++i)
            {
                ISurface rs = MakeRuledSurface(firstPath.Curves[i].Clone(), secondPath.Curves[i].Clone());
                //RuledSurface rs = new RuledSurface(firstPath.Curves[i].Clone(), secondPath.Curves[i].Clone());
                Face face = Face.Construct();
                face.Surface = rs;
                Edge upper = new Edge(null, secondPath.Curves[i].Clone());
                Edge lower = new Edge(null, firstPath.Curves[i].Clone());
                upperEdges.Add(upper);
                lowerEdges.Add(lower);
                ICurve2D[] cvs2d = new ICurve2D[4];
                cvs2d[1] = rs.GetProjectedCurve(upper.Curve3D, 0.0);
                BoundingRect domian = cvs2d[1].GetExtent(); // in case the ruled surface is periodic (typically cylinder or cone)
                cvs2d[0] = rs.GetProjectedCurve(edges[i + 1].Curve3D, 0.0);
                SurfaceHelper.AdjustPeriodic(rs, domian, cvs2d[0]);
                cvs2d[2] = rs.GetProjectedCurve(edges[i].Curve3D, 0.0);
                SurfaceHelper.AdjustPeriodic(rs, domian, cvs2d[2]);
                cvs2d[3] = rs.GetProjectedCurve(lower.Curve3D, 0.0);
                SurfaceHelper.AdjustPeriodic(rs, domian, cvs2d[3]);
                cvs2d[1].Reverse();
                cvs2d[2].Reverse();
                if (Border.SignedArea(cvs2d) < 0.0)
                {
                    ModOp2D ro = rs.ReverseOrientation();
                    for (int j = 0; j < cvs2d.Length; j++)
                    {
                        cvs2d[j] = cvs2d[j].GetModified(ro);
                    }
                }
                edges[i + 1].SetFace(face, cvs2d[0], true);
                upper.SetFace(face, cvs2d[1].CloneReverse(true), false); // .CloneReverse(true) is necessary, since SetFace reverses it again due to "forward==false"
                edges[i].SetFace(face, cvs2d[2].CloneReverse(true), false);
                lower.SetFace(face, cvs2d[3], true);
                face.Set(rs, new Edge[] { edges[i + 1], upper, edges[i], lower }, null);
                faces.Add(face);
            }
            if (makeSolid && firstPath.IsClosed && secondPath.IsClosed && firstPath.GetPlanarState() == PlanarState.Planar && secondPath.GetPlanarState() == PlanarState.Planar)
            {
                Plane upln = secondPath.GetPlane();
                PlaneSurface ups = new PlaneSurface(upln);
                Face upper = Face.Construct();
                for (int i = 0; i < upperEdges.Count; i++)
                {
                    upperEdges[i].SetSecondary(upper, upperEdges[i].Curve3D.GetProjectedCurve(upln), true);
                }
                upper.Set(ups, upperEdges.ToArray(), null);
                Plane lpln = firstPath.GetPlane();
                PlaneSurface lps = new PlaneSurface(lpln);
                Face lower = Face.Construct();
                for (int i = 0; i < lowerEdges.Count; i++)
                {
                    lowerEdges[i].SetSecondary(lower, lowerEdges[i].Curve3D.GetProjectedCurve(lpln), true);
                }
                lower.Set(lps, lowerEdges.ToArray(), null);
                faces.Add(upper);
                faces.Add(lower);
                shell.SetFaces(faces.ToArray());
                shell.AssertOutwardOrientation();
                Solid sld = Solid.Construct();
                sld.SetShell(shell);
                if (project != null) project.SetDefaults(sld);
                return sld;
            }
            shell.SetFaces(faces.ToArray());
            if (project != null) project.SetDefaults(shell);
            return shell;
        }

        private static ISurface MakeRuledSurface(ICurve curve1, ICurve curve2)
        {
            if (curve1 is Line && curve2 is Line)
            {
                if (Precision.SameDirection(curve1.StartDirection, curve2.StartDirection, false))
                {   // two lines in a common plane
                    return new PlaneSurface(curve1.StartPoint, curve1.EndPoint, curve2.StartPoint);
                }
            }
            else if (curve1 is Ellipse && curve2 is Ellipse)
            {
                Ellipse e1 = curve1 as Ellipse;
                Ellipse e2 = curve2 as Ellipse;
                if (e1.IsCircle && e2.IsCircle && Precision.SameDirection(e1.Normal, e2.Normal, false) && Precision.SameDirection(e1.Normal, (e2.Center - e1.Center), false))
                {   // two circles or circular arcs on a common axis
                    if (Math.Abs(e1.Radius - e2.Radius) < Precision.eps)
                    {   // a cylinder
                        return new CylindricalSurface(e1.Center, e1.MajorAxis, e1.MinorAxis, e1.Normal);
                    }
                    else
                    {
                        Line l1 = Line.TwoPoints(e1.Center, e2.Center);
                        Line l2 = Line.TwoPoints(e1.StartPoint, e2.StartPoint);
                        if (Curves.GetCommonPlane(new ICurve[] { l1, l2 }, out Plane pln))
                        {
                            double[] ips = Curves.Intersect(l1, l2, false);
                            if (ips.Length == 1)
                            {
                                GeoPoint apex = l1.PointAt(ips[0]); // returns points in the prolongation of l1
                                Angle semiAngle = new Angle((e1.Center - apex), (e1.StartPoint - apex));
                                Plane epln = e1.Plane;
                                epln.Reverse();
                                return new ConicalSurface(apex, epln.DirectionX, epln.DirectionY, epln.Normal, semiAngle);
                            }
                        }
                    }
                }
            }
            return new RuledSurface(curve1, curve2);
        }

        public static IGeoObject MakePipe(IGeoObject faceShellOrPath, Path along, Project project)
        {
            if (faceShellOrPath is Path)
            {
                Path path = (faceShellOrPath as Path);
                if (!path.IsClosed) return null;
                ICurve[] crvs = path.Curves;
                if (crvs.Length == 0) return null;
                if (crvs.Length == 1)
                {
                    crvs = crvs[0].Split(0.5); // do not use a single closed curve (circle) because we do not want periodic faces
                }
                if (Curves.GetCommonPlane(crvs, out Plane commonPlane))
                {   // if it is not planar, we dont know how to make a face
                    PlaneSurface ps = new PlaneSurface(commonPlane);
                    ICurve2D[] crvs2d = new ICurve2D[crvs.Length];
                    for (int i = 0; i < crvs.Length; i++)
                    {
                        crvs2d[i] = crvs[i].GetProjectedCurve(commonPlane);
                    }
                    Border bdr = Border.FromOrientedList(crvs2d);
                    Face fc = Face.MakeFace(ps, new SimpleShape(bdr));
                    faceShellOrPath = fc;
                }
            }
            if (faceShellOrPath is Face)
            {
                Face fc = faceShellOrPath.Clone() as Face; // disconnect from shell, if it is part of a shell
                fc.SplitSingleOutlines();
                Shell shell = Shell.MakeShell(new Face[] { faceShellOrPath as Face });
                faceShellOrPath = shell;
            }
            if (faceShellOrPath is Shell)
            {   // with a valid faceShellOrPath we end up here.
                Shell shell = faceShellOrPath as Shell;
                foreach (Face fc in shell.Faces)
                {
                    fc.SplitSingleOutlines(); // no single closed edge in outlines or holes
                }
                Edge[] openEdges = shell.OpenEdges;
                ICurve[] alongParts = along.Curves;
                // assume the path is smooth, no sharp bends
                // with sharp bends we must split the path at these points, make partial pipes and unite them all.
                // plus add "wedges" at the sharp bends
                List<Face> pipeFaces = new List<Face>();
                ModOp fromStartToEnd = ModOp.Identity;
                for (int i = 0; i < alongParts.Length; i++)
                {
                    for (int j = 0; j < openEdges.Length; j++)
                    {
                        Face fc = ExtrudeCurveToFace(openEdges[j].Curve3D.CloneModified(fromStartToEnd), alongParts[i]);
                        if (fc != null) pipeFaces.Add(fc);
                    }
                    ModOp m;
                    if (Precision.SameNotOppositeDirection(alongParts[i].StartDirection.Normalized, alongParts[i].EndDirection.Normalized))
                    {
                        // simple move 
                        m = ModOp.Translate(alongParts[i].EndPoint - alongParts[i].StartPoint);
                    }
                    else
                    {
                        GeoVector normal = alongParts[i].StartDirection ^ alongParts[i].EndDirection;
                        if (Precision.IsNullVector(normal))
                        {
                            if (alongParts[i].GetPlanarState() == PlanarState.Planar)
                            {
                                normal = alongParts[i].GetPlane().Normal;
                            }
                        }
                        normal.Norm();
                        m = ModOp.Fit(alongParts[i].StartPoint, new GeoVector[] { alongParts[i].StartDirection.Normalized, normal, normal ^ alongParts[i].StartDirection.Normalized },
                            alongParts[i].EndPoint, new GeoVector[] { alongParts[i].EndDirection.Normalized, normal, normal ^ alongParts[i].EndDirection.Normalized });
                    }
                    fromStartToEnd = m * fromStartToEnd;
                }
                if (!along.IsClosed || !Precision.SameDirection(along.StartDirection, along.EndDirection, false))
                {
                    pipeFaces.AddRange(shell.Faces);
                    Shell sc = shell.Clone() as Shell;
                    sc.Modify(fromStartToEnd);
                    pipeFaces.AddRange(sc.Faces);
                }
                Shell[] res = SewFaces(pipeFaces.ToArray());
                if (res.Length == 1) return res[0];
                return null;
            }
            throw new NotImplementedException();
        }
        /// <summary>
        /// Extrude a curve along another curve. E.g. if you extrude a circle along a line (which is perpendicular to the cirle plane) you will get a cylindrical face.
        /// If the curve <paramref name="along"/> is planar, the curve <paramref name="toExtrude"/> will be only rotated around the plane's normal (and moved of course),
        /// If the curve <paramref name="along"/> is not planar, there is no well difined way how to rotate the curve <paramref name="toExtrude"/>.
        /// </summary>
        /// <param name="toExtrude"></param>
        /// <param name="along"></param>
        /// <returns></returns>
        public static Face ExtrudeCurveToFace(ICurve toExtrude, ICurve along)
        {
            if (along.GetPlanarState() == PlanarState.UnderDetermined) // a line or a linear spline
            {
                if (toExtrude.GetPlanarState() == PlanarState.UnderDetermined) // also linear
                {   // two lines make a plane surface
                    PlaneSurface ps = new PlaneSurface(toExtrude.StartPoint, toExtrude.EndPoint - toExtrude.StartPoint, along.EndPoint - along.StartPoint);
                    Face res = Face.MakeFace(ps, BoundingRect.UnitBoundingRect);
                    return res;
                }
                else if (toExtrude is Ellipse && Precision.SameDirection((toExtrude as Ellipse).Plane.Normal, along.StartDirection, false) && (toExtrude as Ellipse).IsCircle)
                {   // a circular arc extruded along a line, which is parallel to the arcs normal: makes a cylindrical surface
                    Ellipse arc = (toExtrude as Ellipse); // a circular arc
                    CylindricalSurface cs = new CylindricalSurface(arc.Center, arc.MajorAxis, arc.MinorAxis, arc.Plane.Normal);
                    ICurve2D c0 = cs.GetProjectedCurve(arc, 0.0); // this curve also determins the periodic domain, which is beeing used for this Face
                    // c0 must also be a line in 2d
                    GeoPoint p1 = toExtrude.StartPoint + (along.EndPoint - along.StartPoint); // the endpoint of the linear Edge at the startpoint of the arc
                    GeoPoint2D p12d = cs.PositionOf(p1);
                    SurfaceHelper.AdjustPeriodic(cs, c0.GetExtent(), ref p12d); // Move to periodic domain
                    GeoVector2D dir = p12d - c0.StartPoint;
                    ICurve2D c1 = new Line2D(c0.EndPoint, c0.EndPoint + dir);
                    ICurve2D c2 = c0.CloneReverse(true);
                    c2.Move(dir.x, dir.y); // dirx must be 0.0
                    ICurve2D c3 = new Line2D(p12d, c0.StartPoint);
                    Border bdr = Border.FromOrientedList(new ICurve2D[] { c0, c1, c2, c3 });
                    Face res = Face.MakeFace(cs, new SimpleShape(bdr));
                    return res;
                }
                else
                {   // general case: some curve beeing extrude along "along": a SurfaceOfLinearExtrusion
                    GeoVector dir = along.EndPoint - along.StartPoint;
                    SurfaceOfLinearExtrusion sl = new SurfaceOfLinearExtrusion(toExtrude, dir, 0.0, 1.0);
                    Line line1 = Line.MakeLine(toExtrude.StartPoint + dir, toExtrude.StartPoint);
                    Line line2 = Line.MakeLine(toExtrude.EndPoint, toExtrude.StartPoint + dir);
                    ICurve2D c0 = sl.GetProjectedCurve(line1, 0.0); // a horizontal line
                    ICurve2D c2 = sl.GetProjectedCurve(line2, 0.0); // a vertical line
                    ICurve2D c1 = new Line2D(c0.EndPoint, c2.StartPoint);
                    ICurve2D c3 = new Line2D(c2.EndPoint, c1.StartPoint);
                    Border bdr = Border.FromOrientedList(new ICurve2D[] { c0, c1, c2, c3 });
                    if (bdr != null)
                    {
                        Face res = Face.MakeFace(sl, new SimpleShape(bdr));
                        return res;
                    }

                }
            }
            else if (along is Ellipse && (along as Ellipse).IsCircle)
            {   // rotating around an axis, which is the axis of the circular arc
                Ellipse arc = (along as Ellipse);
                if (toExtrude.GetPlanarState() == PlanarState.UnderDetermined) // a line 
                {
                    if (Precision.SameDirection(toExtrude.StartDirection, arc.Plane.Normal, false))
                    {   // rotate a line which is parallel to the rotation axis: make a cylinder
                        Ellipse arc1 = arc.Clone() as Ellipse;
                        GeoPoint cnt = Geometry.DropPL(toExtrude.StartPoint, arc1.Center, arc1.Plane.Normal);
                        arc1.Radius = toExtrude.StartPoint | cnt;
                        arc1.Center = cnt;
                        arc1.StartParameter = arc1.ParameterOf(toExtrude.StartPoint);
                        CylindricalSurface cs = new CylindricalSurface(arc1.Center, arc1.MajorAxis, arc1.MinorAxis, arc1.Plane.Normal);
                        // now arc1 and toExtrude are oth on the CylindricalSurface
                        ICurve2D c0 = cs.GetProjectedCurve(arc1, 0.0); // this must be a horizontal line and determins the periodic domain
                        GeoVector2D dirx = c0.EndPoint - c0.StartPoint;
                        ICurve2D c3 = cs.GetProjectedCurve(toExtrude, 0.0);
                        GeoVector2D diry = c3.EndPoint - c3.StartPoint;
                        SurfaceHelper.AdjustPeriodic(cs, c0.GetExtent(), c3); // adjusst to periodic domain
                        ICurve2D c1 = c3.Clone();
                        c3.Reverse();
                        c1.Move(dirx.x, dirx.y); // dirx.y must be 0.0
                        ICurve2D c2 = c0.CloneReverse(true);
                        c2.Move(diry.x, diry.y);
                        Border bdr = Border.FromOrientedList(new ICurve2D[] { c0, c1, c2, c3 });
                        Face res = Face.MakeFace(cs, new SimpleShape(bdr));
                        return res;
                    }
                    else if (Precision.IsPerpendicular(toExtrude.StartDirection, arc.Plane.Normal, false))
                    {   // a line which is parallel to the arcs plane
                        PlaneSurface ps = new PlaneSurface(toExtrude.StartPoint, toExtrude.StartDirection, arc.Normal ^ toExtrude.StartDirection);
                        Ellipse arc1 = arc.Clone() as Ellipse;
                        arc1.Center = Geometry.DropPL(toExtrude.StartPoint, arc.Center, arc.Normal);
                        arc1.Radius = arc1.Center | toExtrude.StartPoint;
                        arc1.StartParameter = arc1.ParameterOf(toExtrude.StartPoint); // now arc1 is one of the bounds of the Face
                        Ellipse arc2 = arc.Clone() as Ellipse;
                        arc2.Center = Geometry.DropPL(toExtrude.EndPoint, arc.Center, arc.Normal);
                        arc2.Radius = arc2.Center | toExtrude.EndPoint;
                        arc2.StartParameter = arc2.ParameterOf(toExtrude.EndPoint); // now arc1 is one of the bounds of the Face
                        ICurve2D c0 = ps.GetProjectedCurve(arc1, 0.0);
                        ICurve2D c2 = ps.GetProjectedCurve(arc2, 0.0);
                        BoundingRect periodicDomain = c0.GetExtent();
                        c2.Reverse();
                        ICurve2D c1 = new Line2D(c0.EndPoint, c2.StartPoint);
                        ICurve2D c3 = new Line2D(c2.EndPoint, c0.StartPoint);
                        Border bdr = Border.FromOrientedList(new ICurve2D[] { c0, c1, c2, c3 });
                        Face res = Face.MakeFace(ps, new SimpleShape(bdr));
                        return res;
                    }
                    else if (Geometry.DistLL(arc.Center, arc.Normal, toExtrude.StartPoint, toExtrude.EndPoint - toExtrude.StartPoint, out double par1, out double par2) < Precision.eps)
                    {   // rotate a line which intersects the rotation axis: make a cone
                        // make sure, the extrusion line doesn't intersect with the axis, this would be a double cone
                        if (par2 < 0 || par2 > 1)
                        {
                            Angle a = new Angle(arc.Normal, toExtrude.StartDirection);
                            if (a > Math.PI / 2.0) a = Math.PI - a; // Angle between 0 and 90°
                            ConicalSurface cs = new ConicalSurface(arc.Center + par1 * arc.Normal, arc.MajorAxis.Normalized, arc.MinorAxis.Normalized, arc.Normal.Normalized, a);
                            Ellipse arc1 = arc.Clone() as Ellipse;
                            arc1.Center = Geometry.DropPL(toExtrude.StartPoint, arc.Center, arc.Normal);
                            arc1.Radius = arc1.Center | toExtrude.StartPoint;
                            arc1.StartParameter = arc1.ParameterOf(toExtrude.StartPoint); // now arc1 is one of the bounds of the Face
                            Ellipse arc2 = arc.Clone() as Ellipse;
                            arc2.Center = Geometry.DropPL(toExtrude.EndPoint, arc.Center, arc.Normal);
                            arc2.Radius = arc2.Center | toExtrude.EndPoint;
                            arc2.StartParameter = arc2.ParameterOf(toExtrude.EndPoint); // now arc1 is one of the bounds of the Face
                            // ConicalSurface cs1 = ConicalSurface.FromTwoCircles(arc1, arc2); is the same as above
                            ICurve2D c0 = cs.GetProjectedCurve(arc1, 0.0);
                            ICurve2D c2 = cs.GetProjectedCurve(arc2, 0.0);
                            BoundingRect periodicDomain = c0.GetExtent();
                            c2.Reverse();
                            SurfaceHelper.AdjustPeriodic(cs, periodicDomain, c2); // Move into the same domain
                            ICurve2D c1 = new Line2D(c0.EndPoint, c2.StartPoint);
                            ICurve2D c3 = new Line2D(c2.EndPoint, c0.StartPoint);
                            Border bdr = Border.FromOrientedList(new ICurve2D[] { c0, c1, c2, c3 });
                            Face res = Face.MakeFace(cs, new SimpleShape(bdr));
                            return res;
                        }
                    }
                }
                else if (toExtrude is Ellipse && (toExtrude as Ellipse).IsCircle)
                {   // rotating a circle
                    Ellipse toRotate = toExtrude as Ellipse;
                    if (Precision.IsDirectionInPlane(arc.Normal, toRotate.Plane) && Precision.IsPointOnPlane(arc.Center, toRotate.Plane))
                    {   // a circular arc is rotated about an axis which resides in its plane: a torus
                        GeoPoint cnt = Geometry.DropPL(toRotate.Center, arc.Center, arc.Normal);
                        ToroidalSurface ts = new ToroidalSurface(cnt, arc.MajorAxis.Normalized, arc.MinorAxis.Normalized, arc.Normal.Normalized, cnt | toRotate.Center, toRotate.Radius);
                        Ellipse arc1 = arc.Clone() as Ellipse;
                        arc1.Center = Geometry.DropPL(toExtrude.StartPoint, arc.Center, arc.Normal);
                        arc1.Radius = arc1.Center | toExtrude.StartPoint;
                        arc1.StartParameter = arc1.ParameterOf(toExtrude.StartPoint); // now arc1 is one of the bounds of the Face
                        Ellipse arc2 = arc.Clone() as Ellipse;
                        arc2.Center = Geometry.DropPL(toExtrude.EndPoint, arc.Center, arc.Normal);
                        arc2.Radius = arc2.Center | toExtrude.EndPoint;
                        arc2.StartParameter = arc2.ParameterOf(toExtrude.EndPoint); // now arc1 is one of the bounds of the Face
                        ICurve2D c0 = ts.GetProjectedCurve(arc1, 0.0); // a horizontal line
                        ICurve2D c3 = ts.GetProjectedCurve(toExtrude, 0.0); // a vertical line
                        GeoPoint2D sp0 = c0.StartPoint;
                        GeoPoint2D sp3 = c3.StartPoint; // these two points may only differ by uperiod or vperiod
                        if (sp0 != sp3) c3.Move(sp0.x - sp3.x, sp0.y - sp3.y);
                        BoundingRect periodicDomain = c0.GetExtent();
                        periodicDomain.MinMax(c3.GetExtent());
                        ICurve2D c2 = ts.GetProjectedCurve(arc2, 0.0);
                        c2.Reverse();
                        SurfaceHelper.AdjustPeriodic(ts, periodicDomain, c2); // Move into the same domain
                        ICurve2D c1 = new Line2D(c0.EndPoint, c2.StartPoint);
                        c3 = new Line2D(c2.EndPoint, c0.StartPoint);
                        Border bdr = Border.FromOrientedList(new ICurve2D[] { c0, c1, c2, c3 });
                        Face res = Face.MakeFace(ts, new SimpleShape(bdr));
                        return res;
                    }
                }
                else
                {
                    SurfaceOfRevolution sr = new SurfaceOfRevolution(toExtrude, arc.Center, arc.Normal);
                    Ellipse arc1 = arc.Clone() as Ellipse;
                    arc1.Center = Geometry.DropPL(toExtrude.StartPoint, arc.Center, arc.Normal);
                    arc1.Radius = arc1.Center | toExtrude.StartPoint;
                    arc1.StartParameter = arc1.ParameterOf(toExtrude.StartPoint); // now arc1 is one of the bounds of the Face
                    Ellipse arc2 = arc.Clone() as Ellipse;
                    arc2.Center = Geometry.DropPL(toExtrude.EndPoint, arc.Center, arc.Normal);
                    arc2.Radius = arc2.Center | toExtrude.EndPoint;
                    arc2.StartParameter = arc2.ParameterOf(toExtrude.EndPoint); // now arc1 is one of the bounds of the Face
                    ICurve2D c0 = sr.GetProjectedCurve(arc1, 0.0); // a horizontal line
                    ICurve2D c3 = sr.GetProjectedCurve(toExtrude, 0.0); // a vertical line
                    GeoPoint2D sp0 = c0.StartPoint;
                    GeoPoint2D sp3 = c3.StartPoint; // these two points may only differ by uperiod or vperiod
                    if (sp0 != sp3) c3.Move(sp0.x - sp3.x, sp0.y - sp3.y);
                    BoundingRect periodicDomain = c0.GetExtent();
                    periodicDomain.MinMax(c3.GetExtent());
                    ICurve2D c2 = sr.GetProjectedCurve(arc2, 0.0);
                    c2.Reverse();
                    SurfaceHelper.AdjustPeriodic(sr, periodicDomain, c2); // Move into the same domain
                    ICurve2D c1 = new Line2D(c0.EndPoint, c2.StartPoint);
                    // ICurve2D c3 = new Line2D(c2.EndPoint, c1.StartPoint);
                    Border bdr = Border.FromOrientedList(new ICurve2D[] { c0, c1, c2, c3 });
                    Face res = Face.MakeFace(sr, new SimpleShape(bdr));
                    return res;
                }
            }
            else
            {
                return null; // not yet implemented for other curves

            }
            return null; // not yet implemented for other curves
        }

        internal static Shell MakePipe(ICurve along, double radius, GeoVector seam)
        {   // erzeugt ein Rohr mit gegebenem Radius entland der Kurve als Mittelachse und der Nahtstelle in Richtung seam
            if (along is Line)
            {
                GeoVector dirz = along.StartDirection.Normalized;
                GeoVector diry = radius * (seam ^ dirz).Normalized;
                GeoVector dirx = radius * (dirz ^ diry).Normalized;
                CylindricalSurface cs = new CylindricalSurface(along.StartPoint, dirx, diry, dirz);
                Border bdr = Border.MakeRectangle(0.0, 2.0 * Math.PI, 0.0, along.Length);
                Face fc = Face.MakeFace(cs, new SimpleShape(bdr));
                Shell res = Shell.Construct();
                res.SetFaces(new Face[] { fc });
                return res;
            }
            if (along is Ellipse && (along as Ellipse).IsCircle)
            {
                Ellipse e = (along as Ellipse);
                ToroidalSurface ts = new ToroidalSurface(along.StartPoint, e.Plane.DirectionX, e.Plane.DirectionY, e.Plane.Normal, e.Radius, e.MinorRadius);
                Border bdr = Border.MakeRectangle(0.0, e.SweepParameter, 0.0, 2.0 * Math.PI);
                Face fc = Face.MakeFace(ts, new SimpleShape(bdr));
                Shell res = Shell.Construct();
                res.SetFaces(new Face[] { fc });
                return res;
            }
            // Pfade besser vorher aufteilen wegen der Richtungen
            //else if (along is Path)
            //{
            //    Path path = (along as Path);
            //    for (int i = 0; i < path.CurveCount; i++)
            //    {
            //        path.Curves[i]
            //    }
            //}
            else
            {
                Ellipse circ = Ellipse.Construct();
                GeoVector dirz = along.StartDirection.Normalized;
                GeoVector diry = radius * (seam ^ dirz).Normalized;
                GeoVector dirx = radius * (dirz ^ diry).Normalized;
                circ.SetCirclePlaneCenterRadius(new Plane(along.StartPoint, dirx, diry), along.StartPoint, radius);
                // ISurface orient = new SurfaceOfLinearExtrusion(along, seam, 0.0, 1.0);
                // CurveMovement mm = new CurveMovement(new Line2D(GeoPoint2D.Origin, new GeoPoint2D(1, 0)), orient);
                // das wurde noch nicht getestet. Die 2d Linie (0,0)->(1,0) entspricht der 3d Kurve.
                GeneralSweptCurve gs = new GeneralSweptCurve(circ, along, GeoVector.NullVector);
                Border bdr = Border.MakeRectangle(0.0, 2.0 * Math.PI, 0.0, along.Length);
                Face fc = Face.MakeFace(gs, new SimpleShape(bdr));
                Shell res = Shell.Construct();
                res.SetFaces(new Face[] { fc });
                return res;
            }
        }
        public static Face MakeFace(Path path, Project project)
        {
            return MakeFace(path, project, true) as Face;
        }
        /// <summary>
        /// Creates a Solid by winding the closed path around the axis. The axisDirection also specifies the pitch
        /// </summary>
        /// <param name="axisLocation"></param>
        /// <param name="axisDirection"></param>
        /// <param name="plane"></param>
        /// <param name="path"></param>
        /// <param name="numTurns"></param>
        /// <returns></returns>
        internal static Solid MakeHelicalSolid(GeoPoint2D axisLocation, GeoVector2D axisDirection, Plane plane, Path2D path, double numTurns, bool rightHanded)
        {
            if (!path.IsClosed) return null;
            List<Face> faces = new List<Face>();
            double pitch = axisDirection.Length;
            if (!rightHanded) pitch = -pitch;
            GeoVector axisDirection3D = plane.ToGlobal(axisDirection);
            GeoVector pitchOffset = pitch * axisDirection3D.Normalized;
            GeoPoint axisLocation3D = plane.ToGlobal(axisLocation);

            Edge[] startEdges = new Edge[path.SubCurves.Length]; // Edges created by the path at the beginning of the rotation
            Edge[] endEdges = new Edge[path.SubCurves.Length]; // Edges created by the path at the end of the rotation
            for (int turn = 0; turn < numTurns; turn++)
            {
                double rotationAngle = Math.PI * 2.0;
                if (numTurns - turn <= 1.0) rotationAngle = (Math.PI * 2.0) * (numTurns - turn);
                // the span of a turn is 2*pi except for the last turn, where it may be less
                for (int i = 0; i < path.SubCurves.Length; i++)
                {
                    ICurve crv = path.SubCurves[i].MakeGeoObject(plane) as ICurve;
                    if (turn > 0) crv = crv.CloneModified(ModOp.Translate(turn * pitchOffset));
                    HelicalSurface hs = new HelicalSurface(crv, axisLocation3D, axisDirection3D, pitch, 0, 1);
                    Face fc = Face.MakeFace(hs, new BoundingRect(0, 0, rotationAngle, 1));
                    // fc.OutlineEdges 0: bottom, 1: right (end), 2: top, 3: left (start) side
                    if (turn == 0)
                    {
                        startEdges[i] = fc.OutlineEdges[3]; // collect the start edges of the first turn
                    }
                    else
                    {
                        fc.ReplaceEdge(fc.OutlineEdges[3], endEdges[i]); // replace the start edges with the end edges of the previous turn
                    }
                    if (i > 0)
                    {
                        Face.ConnectTwoFaces(fc, faces[faces.Count - 1], Precision.eps);
                        if (i == path.SubCurves.Length - 1)
                        {
                            Face.ConnectTwoFaces(fc, faces[faces.Count - path.SubCurves.Length + 1], Precision.eps);
                        }
                    }
                    endEdges[i] = fc.OutlineEdges[1];
                    faces.Add(fc);
                }
            }
            SimpleShape cs = new SimpleShape(Border.FromOrientedList(path.SubCurves));
            PlaneSurface ps = new PlaneSurface(plane);
            Face lowerCap = Face.MakeFace(ps, cs);
            double lastRotationAngle = (Math.PI * 2.0) * (numTurns - Math.Truncate(numTurns));
            Face upperCap = lowerCap.Clone() as Face;
            upperCap.Modify(ModOp.Translate(numTurns * pitchOffset) * ModOp.Rotate(axisLocation3D, axisDirection3D, new SweepAngle(lastRotationAngle)));
            for (int i = 0; i < startEdges.Length; i++)
            {
                lowerCap.UseEdge(startEdges[i]);
            }
            for (int i = 0; i < endEdges.Length; i++)
            {
                upperCap.UseEdge(endEdges[i]);
            }
            faces.Add(lowerCap);
            faces.Add(upperCap);
            Shell sh = Shell.Construct();
            sh.SetFaces(faces.ToArray());
            sh.AssertOutwardOrientation();
            Solid res = Solid.Construct();
            res.SetShell(sh);

            return res;
        }
        /// <summary>
        /// Creates a thred (screw) from a profile (path) and a point in a plane. The path must be open and the pitch and direction is determined by
        /// the start- and endpoint of the path. The radius of the thread is the distance of the axisLocation to the connection of start- and enpoints of the path.
        /// Cylindrical segments form the start and endcaps.
        /// </summary>
        /// <param name="axisLocation">Location of the axis in the plane</param>
        /// <param name="plane">The plane, in which path and axisLocation are defined</param>
        /// <param name="path">The profile of the thread</param>
        /// <param name="numTurns">Number of turns</param>
        /// <returns>A Solid with the thread</returns>
        internal static Solid MakeHelicalSolid(GeoPoint2D axisLocation, Plane plane, Path2D path, double numTurns, bool rightHanded)
        {
            List<Face> faces = new List<Face>();
            GeoVector2D axisDirection = path.EndPoint - path.StartPoint;
            double pitch = axisDirection.Length;
            if (!rightHanded) pitch = -pitch;
            GeoVector axisDirection3D = plane.ToGlobal(axisDirection);
            GeoVector pitchOffset = pitch * axisDirection3D.Normalized;
            GeoPoint axisLocation3D = plane.ToGlobal(axisLocation);

            // Front and back side of the thread: connect start- and enpoint of path with a line and split the path when necessary
            Line2D baseLine = new Line2D(path.StartPoint, path.EndPoint);
            ModOp2D toHorizontal;
            if (baseLine.Distance(axisLocation) < 0.0)
            {
                toHorizontal = ModOp2D.Rotate(new SweepAngle(baseLine.StartDirection, GeoVector2D.XAxis));
            }
            else
            {
                toHorizontal = ModOp2D.Rotate(new SweepAngle(baseLine.StartDirection, -GeoVector2D.XAxis));
            }
            Path2D horPath = (path as ICurve2D).GetModified(toHorizontal) as Path2D;
            double y = horPath.GetExtent().Bottom;
            baseLine = new Line2D(horPath.StartPoint, horPath.EndPoint);
            if (y < baseLine.StartPoint.y)
            {
                baseLine.Move(0, y - baseLine.StartPoint.y);
            }
            baseLine = baseLine.GetModified(toHorizontal.GetInverse()) as Line2D;
            // baseline now doesn't intersect path, is parallel to path.StartPoint, path.EndPoint and is closer to the axis than any part of the path.
            // maybe we nned an extra connection from path.StartPoint to baseLine.StartPoint and the same for EndPoint

            // create threads for each round
            Face topface = null;
            Edge[] startEdges = new Edge[path.SubCurves.Length]; // Edges created by the path at the beginning of the rotation
            Edge[] endEdges = new Edge[path.SubCurves.Length]; // Edges created by the path at the end of the rotation
            Edge topEdge = null; // top edge of each segment
            for (int turn = 0; turn < numTurns; turn++)
            {
                double rotationAngle = Math.PI * 2.0;
                if (numTurns - turn <= 1.0) rotationAngle = (Math.PI * 2.0) * (numTurns - turn);
                // the span of a turn is 2*pi except for the last turn, where it may be less
                for (int i = 0; i < path.SubCurves.Length; i++)
                {
                    ICurve crv = path.SubCurves[i].MakeGeoObject(plane) as ICurve;
                    if (turn > 0) crv = crv.CloneModified(ModOp.Translate(turn * pitchOffset));
                    HelicalSurface hs = new HelicalSurface(crv, axisLocation3D, axisDirection3D, pitch, 0, 1);
                    Face fc = Face.MakeFace(hs, new BoundingRect(0, 0, rotationAngle, 1));
                    // fc.OutlineEdges 0: bottom, 1: right (end), 2: top, 3: left (start) side
                    if (turn == 0)
                    {
                        startEdges[i] = fc.OutlineEdges[3]; // collect the start edges of the first turn
                    }
                    else
                    {
                        fc.ReplaceEdge(fc.OutlineEdges[3], endEdges[i]); // replace the start edges with the end edges of the previous turn
                    }
                    endEdges[i] = fc.OutlineEdges[1];
                    faces.Add(fc);
                    if (i == 0 && rotationAngle < Math.PI * 2.0 && topface != null)
                    {
                        // last and incomplete turn, first face: the End of the first edge splits
                        // the third edge of the topface of the previous turn
                        GeoPoint splitPoint = fc.OutlineEdges[0].Curve3D.EndPoint;
                        Edge splitEdge = topface.OutlineEdges[2];
                        double pos = splitEdge.Curve3D.PositionOf(splitPoint);
                        SortedList<double, Vertex> splitPos = new SortedList<double, Vertex>();
                        splitPos[pos] = new Vertex(splitPoint);
                        splitEdge.Split(splitPos, 0.0);
                        topEdge = topface.OutlineEdges[3];
                        fc.ReplaceEdge(fc.OutlineEdges[0], topEdge);
                    }
                    else
                    {
                        if (topEdge != null) fc.ReplaceEdge(fc.OutlineEdges[0], topEdge);

                    }
                    topface = fc;
                    topEdge = fc.OutlineEdges[2];
                }
            }
            // create front and back side
            // connect edge index to path subcurves, because Border.FromOrientedList could reverse the order of the segments
            for (int i = 0; i < path.SubCurvesCount; i++)
            {
                path.SubCurves[i].UserData.Add("MakeHelicalSolid.CurveToEdgeindex", i);
            }
            if (!Precision.IsEqual(path.StartPoint, baseLine.StartPoint))
            {

            }

            path.Append(new Line2D(path.EndPoint, path.StartPoint));
            SimpleShape cs = new SimpleShape(Border.FromOrientedList(path.SubCurves));
            PlaneSurface ps = new PlaneSurface(plane);
            Face lowerCap = Face.MakeFace(ps, cs);
            Edge lowerCapBaseLine = null;
            for (int i = 0; i < cs.Outline.Segments.Length; i++)
            {
                int ind = -1;
                if (cs.Outline.Segments[i].UserData.Contains("MakeHelicalSolid.CurveToEdgeindex")) ind = (int)cs.Outline.Segments[i].UserData.GetData("MakeHelicalSolid.CurveToEdgeindex");
                if (ind >= 0) lowerCap.ReplaceEdge(lowerCap.OutlineEdges[i], startEdges[ind]);
                else lowerCapBaseLine = lowerCap.OutlineEdges[i];
            }
            faces.Add(lowerCap);
            double lastRotationAngle = (Math.PI * 2.0) * (numTurns - Math.Truncate(numTurns));
            Face upperCap = lowerCap.Clone() as Face;
            upperCap.Modify(ModOp.Translate(numTurns * pitchOffset) * ModOp.Rotate(axisLocation3D, axisDirection3D, new SweepAngle(lastRotationAngle)));
            Edge upperCapBaseLine = null;
            for (int i = 0; i < cs.Outline.Segments.Length; i++)
            {
                int ind = -1;
                if (cs.Outline.Segments[i].UserData.Contains("MakeHelicalSolid.CurveToEdgeindex")) ind = (int)cs.Outline.Segments[i].UserData.GetData("MakeHelicalSolid.CurveToEdgeindex");
                if (ind >= 0) upperCap.ReplaceEdge(upperCap.OutlineEdges[i], endEdges[ind]);
                else upperCapBaseLine = upperCap.OutlineEdges[i];
            }
            faces.Add(upperCap);

            // the lowest face has an open edge, to which we want to connect a cylinder
            // we split this edge into two parts
            Edge lowestEdge = faces[0].OutlineEdges[0];
            Vertex splLow = new Vertex(lowestEdge.Curve3D.PointAt(0.5));
            Edge[] lowestEdges = lowestEdge.Split(0.5);

            // eithere there is a single uppermost face with an open edge, or two upper faces with one open edge each
            Edge[] candidates = upperCapBaseLine.StartVertex(upperCap).Edges;
            Edge[] upperEdges = new CADability.Edge[2];
            for (int i = 0; i < candidates.Length; i++)
            {
                if (candidates[i].PrimaryFace.Surface is HelicalSurface && candidates[i].SecondaryFace == null)
                {
                    upperEdges[0] = candidates[i];
                    break;
                }
            }
            candidates = upperCapBaseLine.EndVertex(upperCap).Edges;
            for (int i = 0; i < candidates.Length; i++)
            {
                if (candidates[i].PrimaryFace.Surface is HelicalSurface && candidates[i].SecondaryFace == null && upperEdges[0] != candidates[i])
                {
                    upperEdges[1] = candidates[i];
                    break;
                }
            }
            if (upperEdges[1] == null)
            {
                // there is only one upper face, this happens when numTurns is a integer value
                upperEdges = upperEdges[0].Split(0.5);
            }

            // make the cylindrical surface for two lower cylindrical parts and two upper cylindrical parts
            GeoVector dirx, diry;
            GeoPoint sp = plane.ToGlobal(path.StartPoint);
            diry = (sp - axisLocation3D) ^ axisDirection3D;
            dirx = diry ^ axisDirection3D;
            double radius = Geometry.DistPL(sp, axisLocation3D, axisDirection3D);
            dirx.Length = radius;
            diry.Length = radius;
            CylindricalSurface cyls = new CylindricalSurface(axisLocation3D, dirx, diry, axisDirection3D.Normalized);

            GeoPoint2D sp0 = cyls.PositionOf(lowestEdges[0].StartVertex(faces[0]).Position);
            GeoPoint2D ep0 = cyls.PositionOf(lowestEdges[0].EndVertex(faces[0]).Position);
            if (ep0.x < sp0.x) ep0.x += Math.PI * 2;
            double yc = sp0.y - pitch;
            Border bdr = Border.MakePolygon(new GeoPoint2D(sp0.x, yc), new GeoPoint2D(ep0.x, yc), ep0, sp0);
            Face fc0 = Face.MakeFace(cyls.Clone(), new SimpleShape(bdr));
            fc0.ReplaceEdge(fc0.OutlineEdges[2], lowestEdges[0]);
            faces.Add(fc0);
            GeoPoint2D sp1 = cyls.PositionOf(lowestEdges[1].StartVertex(faces[0]).Position);
            GeoPoint2D ep1 = cyls.PositionOf(lowestEdges[1].EndVertex(faces[0]).Position);
            if (ep1.x < sp1.x) ep1.x += Math.PI * 2;
            bdr = Border.MakePolygon(new GeoPoint2D(sp1.x, yc), new GeoPoint2D(ep1.x, yc), ep1, sp1);
            Face fc1 = Face.MakeFace(cyls.Clone(), new SimpleShape(bdr));
            fc1.ReplaceEdge(fc1.OutlineEdges[2], lowestEdges[1]);
            fc1.ReplaceEdge(fc1.OutlineEdges[3], fc0.OutlineEdges[1]);
            Vertex spl = lowestEdges[0].StartVertex(faces[0]);
            double posspl = fc1.OutlineEdges[1].Curve3D.PositionOf(spl.Position);
            fc1.OutlineEdges[1].Split(posspl);
            fc1.ReplaceEdge(fc1.OutlineEdges[2], lowerCap.OutlineEdges[lowerCap.OutlineEdges.Length - 1]);
            fc1.ReplaceEdge(fc1.OutlineEdges[1], fc0.OutlineEdges[3]);
            faces.Add(fc1);

            GeoPoint2D sp2 = cyls.PositionOf(upperEdges[0].StartVertex(upperEdges[0].PrimaryFace).Position);
            GeoPoint2D ep2 = cyls.PositionOf(upperEdges[0].EndVertex(upperEdges[0].PrimaryFace).Position);
            if (ep2.x > sp2.x) ep2.x -= Math.PI * 2;
            yc = sp2.y + pitch;
            bdr = Border.MakePolygon(new GeoPoint2D(sp2.x, yc), new GeoPoint2D(ep2.x, yc), ep2, sp2);
            Face fc2 = Face.MakeFace(cyls.Clone(), new SimpleShape(bdr));
            Face.ConnectTwoFaces(fc2, upperEdges[0].PrimaryFace, Precision.eps);
            faces.Add(fc2);
            GeoPoint2D sp3 = cyls.PositionOf(upperEdges[1].StartVertex(upperEdges[1].PrimaryFace).Position);
            GeoPoint2D ep3 = cyls.PositionOf(upperEdges[1].EndVertex(upperEdges[1].PrimaryFace).Position);
            if (ep3.x > sp3.x) ep3.x -= Math.PI * 2;
            bdr = Border.MakePolygon(new GeoPoint2D(sp3.x, yc), new GeoPoint2D(ep3.x, yc), ep3, sp3);
            Face fc3 = Face.MakeFace(cyls.Clone(), new SimpleShape(bdr));
            Face.ConnectTwoFaces(fc3, upperEdges[1].PrimaryFace, Precision.eps);
            // split the longer line of fc3 
            Edge fc3LongLine = null;
            for (int i = 0; i < fc3.OutlineEdges.Length; i++)
            {
                if (fc3.OutlineEdges[i].Curve3D is Line)
                {
                    if (fc3LongLine == null) fc3LongLine = fc3.OutlineEdges[i];
                    else if (fc3LongLine.Curve3D.Length < fc3.OutlineEdges[i].Curve3D.Length) fc3LongLine = fc3.OutlineEdges[i];
                }
            }
            double pos1 = fc3LongLine.Curve3D.PositionOf(upperCapBaseLine.Vertex1.Position);
            double pos2 = fc3LongLine.Curve3D.PositionOf(upperCapBaseLine.Vertex2.Position);
            if (Math.Abs(pos1 - 0.5) > Math.Abs(pos2 - 0.5)) pos1 = pos2;
            fc3LongLine.Split(pos1);
            Face.ConnectTwoFaces(fc3, upperCap, Precision.eps);
            Face.ConnectTwoFaces(fc3, fc2, Precision.eps);
            faces.Add(fc3);

            // make the bottom and the top cover to close the solid
            Edge e0 = null, e1 = null;
            foreach (Edge edg in fc0.Edges)
            {
                if (edg.Curve3D is Ellipse)
                {
                    e0 = edg;
                    break;
                }
            }
            foreach (Edge edg in fc1.Edges)
            {
                if (edg.Curve3D is Ellipse)
                {
                    e1 = edg;
                    break;
                }
            }
            PlaneSurface psb = new PlaneSurface(e0.Curve3D.GetPlane());
            bdr = Border.FromOrientedList(new ICurve2D[] { psb.GetProjectedCurve(e0.Curve3D, 0.0), psb.GetProjectedCurve(e1.Curve3D, 0.0) });
            Face bfc = Face.MakeFace(psb, new SimpleShape(bdr));
            Face.ConnectTwoFaces(bfc, fc0, Precision.eps);
            Face.ConnectTwoFaces(bfc, fc1, Precision.eps);
            faces.Add(bfc);

            Edge e2 = null, e3 = null;
            foreach (Edge edg in fc2.Edges)
            {
                if (edg.Curve3D is Ellipse)
                {
                    e2 = edg;
                    break;
                }
            }
            foreach (Edge edg in fc3.Edges)
            {
                if (edg.Curve3D is Ellipse)
                {
                    e3 = edg;
                    break;
                }
            }
            PlaneSurface pst = new PlaneSurface(e2.Curve3D.GetPlane());
            bdr = Border.FromOrientedList(new ICurve2D[] { pst.GetProjectedCurve(e2.Curve3D, 0.0), psb.GetProjectedCurve(e3.Curve3D, 0.0) });
            Face tfc = Face.MakeFace(pst, new SimpleShape(bdr));
            Face.ConnectTwoFaces(tfc, fc2, Precision.eps);
            Face.ConnectTwoFaces(tfc, fc3, Precision.eps);
            faces.Add(tfc);

            Shell sh = Shell.Construct();
            sh.SetFaces(faces.ToArray());
            sh.AssertOutwardOrientation();
            Solid res = Solid.Construct();
            res.SetShell(sh);

            return res;
        }
        internal static Face MakeHelicoid(GeoPoint2D startpoint, GeoPoint2D endpoint, double convolutionHeight, double totalHight)
        {
            // mal ein Versuch, der erfoglversprechend aussieht: die Verschraubung einer linie (in der x/y Ebene um die Z-Achse
            // es entsteht eine NURBS-Schraubenfläche durch eine Spirale in einer Richtung (Spirale ist Kreis bei gleichmäßigem
            // hochziehen in Z) und einer Linie in der anderen Richtung. Die 45° Punkte der Helix sind mit Wurzel2 multipiziert,
            // damit es die Ecken des Quadrates als Kontrollpunkte des Kreises werden.

            // Vermutlich versagt diese Lösung in folgender Hinsicht: Der NURBS Kreis, auf dem die Spirale basiert dreht
            // nicht gleichmäßig, die Höhe nimmt aber wohl gleichmäßig zu. Somit entsteht keine gleichmäßige Spirale.
            // was man machen muss ist eine NURBS Annäherung an die echte Spirale (oder an den Kreis mit gleichmäßigem
            // Parameter u). Diese Annäherung ist allerdings nie exakt. Allerdings könnte die zu drehende Kontur
            // exakte NURBS Bögen enthalten. ICurve(2D?) bräuchte eine Methode MakeNURBS, die einen NURBS liefert.
            // dessen Pole müssen dann irgendwie mit den Polen des Spiralennurbs gemischt werden, wahrscheinlich mit einem
            // Scale wie m2 es ist. Da ist aber noch Testarbeit nötig
            // Im Buch Seite 380 GlobalSurfInterp liefert eigentlich die ganze Geschichte, nur dass wir in der einen
            // Richtung immer die selbe Kurve haben und die muss nicht interpoliert werden, sondern liegt direkt vor
            int numTurns = (int)Math.Ceiling(totalHight / convolutionHeight);
            GeoPoint[,] nurbsHelix = new GeoPoint[2, numTurns * 8 + 1];
            ModOp2D m1 = ModOp2D.Rotate(Math.PI / 4.0);
            ModOp2D m2 = ModOp2D.Scale(Math.Sqrt(2.0));
            for (int i = 0; i < numTurns; ++i)
            {
                GeoPoint2D p0 = startpoint;
                nurbsHelix[0, i * 8 + 0] = new GeoPoint(p0, i * convolutionHeight);
                p0 = m1 * p0;
                nurbsHelix[0, i * 8 + 1] = new GeoPoint(m2 * p0, i * convolutionHeight + 1 * convolutionHeight / 8.0);
                p0 = m1 * p0;
                nurbsHelix[0, i * 8 + 2] = new GeoPoint(p0, i * convolutionHeight + 2 * convolutionHeight / 8.0);
                p0 = m1 * p0;
                nurbsHelix[0, i * 8 + 3] = new GeoPoint(m2 * p0, i * convolutionHeight + 3 * convolutionHeight / 8.0);
                p0 = m1 * p0;
                nurbsHelix[0, i * 8 + 4] = new GeoPoint(p0, i * convolutionHeight + 4 * convolutionHeight / 8.0);
                p0 = m1 * p0;
                nurbsHelix[0, i * 8 + 5] = new GeoPoint(m2 * p0, i * convolutionHeight + 5 * convolutionHeight / 8.0);
                p0 = m1 * p0;
                nurbsHelix[0, i * 8 + 6] = new GeoPoint(p0, i * convolutionHeight + 6 * convolutionHeight / 8.0);
                p0 = m1 * p0;
                nurbsHelix[0, i * 8 + 7] = new GeoPoint(m2 * p0, i * convolutionHeight + 7 * convolutionHeight / 8.0);
                p0 = endpoint;
                nurbsHelix[1, i * 8 + 0] = new GeoPoint(p0, i * convolutionHeight);
                p0 = m1 * p0;
                nurbsHelix[1, i * 8 + 1] = new GeoPoint(m2 * p0, i * convolutionHeight + 1 * convolutionHeight / 8.0);
                p0 = m1 * p0;
                nurbsHelix[1, i * 8 + 2] = new GeoPoint(p0, i * convolutionHeight + 2 * convolutionHeight / 8.0);
                p0 = m1 * p0;
                nurbsHelix[1, i * 8 + 3] = new GeoPoint(m2 * p0, i * convolutionHeight + 3 * convolutionHeight / 8.0);
                p0 = m1 * p0;
                nurbsHelix[1, i * 8 + 4] = new GeoPoint(p0, i * convolutionHeight + 4 * convolutionHeight / 8.0);
                p0 = m1 * p0;
                nurbsHelix[1, i * 8 + 5] = new GeoPoint(m2 * p0, i * convolutionHeight + 5 * convolutionHeight / 8.0);
                p0 = m1 * p0;
                nurbsHelix[1, i * 8 + 6] = new GeoPoint(p0, i * convolutionHeight + 6 * convolutionHeight / 8.0);
                p0 = m1 * p0;
                nurbsHelix[1, i * 8 + 7] = new GeoPoint(m2 * p0, i * convolutionHeight + 7 * convolutionHeight / 8.0);
            }
            nurbsHelix[0, numTurns * 8] = new GeoPoint(startpoint, numTurns * convolutionHeight);
            nurbsHelix[1, numTurns * 8] = new GeoPoint(endpoint, numTurns * convolutionHeight);
            double[,] weights = new double[2, numTurns * 8 + 1];
            for (int i = 0; i < numTurns * 8 + 1; ++i)
            {
                if ((i & 0x1) == 1) weights[0, i] = Math.Sqrt(2.0) / 2.0;
                else weights[0, i] = 1.0;
                weights[1, i] = weights[0, i];
            }
            double[] uknots = new double[] { 0, 1 };
            double[] vknots = new double[numTurns * 4 + 1];
            for (int i = 0; i < vknots.Length; ++i)
            {
                vknots[i] = i * 0.25;
            }
            int[] umults = new int[] { 2, 2 };
            int[] vmults = new int[vknots.Length];
            for (int i = 1; i < vknots.Length - 1; ++i)
            {
                vmults[i] = 2;
            }
            vmults[0] = 3; vmults[vmults.Length - 1] = 3;
            NurbsSurface ns = new NurbsSurface(nurbsHelix, weights, uknots, vknots, umults, vmults, 1, 2, false, false);
            Face face = Face.Construct();
            double bound = totalHight / convolutionHeight;
            Edge e1 = new Edge(face, null, face, new Line2D(new GeoPoint2D(0.0, 0.0), new GeoPoint2D(1.0, 0.0)), true);
            Edge e2 = new Edge(face, null, face, new Line2D(new GeoPoint2D(1.0, 0.0), new GeoPoint2D(1.0, bound)), true);
            Edge e3 = new Edge(face, null, face, new Line2D(new GeoPoint2D(1.0, bound), new GeoPoint2D(0.0, bound)), true);
            Edge e4 = new Edge(face, null, face, new Line2D(new GeoPoint2D(0.0, bound), new GeoPoint2D(0.0, 0.0)), true);
            face.Set(ns, new Edge[] { e1, e2, e3, e4 }, new Edge[0][]);
            e1.RecalcCurve3D();
            e2.RecalcCurve3D();
            e3.RecalcCurve3D();
            e4.RecalcCurve3D();
            return face;
        }
        public static Make3D PlaneIntersection(Face[] faces, Plane plane)
        {
            throw new NotImplementedException();
        }
        public static IGeoObject[] MakeFillet(Edge[] edges, double radius, out IGeoObject[] affectedShellsOrSolids)
        {
            Shell sh = edges[0].PrimaryFace.Owner as Shell;
            if (sh != null)
            {
                Shell res = BRepOperation.RoundEdges(sh, edges, radius);
                affectedShellsOrSolids = new IGeoObject[] { sh };
                if (res != null) return new IGeoObject[] { res };
            }
            affectedShellsOrSolids = null;
            return null;
        }
        public static IGeoObject[] MakeChamfer(Face primaryFace, Edge[] edges, double primaryDist, double secondaryDist, out IGeoObject[] affectedShellsOrSolids)
        {
            Shell sh = primaryFace.Owner as Shell;
            if (sh != null)
            {
                Shell res = BRepOperation.ChamferEdges(primaryFace, edges, primaryDist, secondaryDist);
                affectedShellsOrSolids = new IGeoObject[] { sh };
                if (res != null) return new IGeoObject[] { res };
            }
            affectedShellsOrSolids = null;
            return null;
        }
        /// <summary>
        /// Takes all faces and shells from the given list and tries to sew them together. When two or more faces have
        /// common edges they are connected to shells. Shells with no free edges are converted to solids.
        /// </summary>
        /// <param name="select">faces and shells to sew</param>
        /// <returns>resulting objects</returns>
        public static GeoObjectList SewFacesAndShells(GeoObjectList select)
        {
            List<Face> faces = new List<GeoObject.Face>();
            for (int i = 0; i < select.Count; i++)
            {
                if (select[i] is Face) faces.Add(select[i] as Face);
                else if (select[i] is Shell) faces.AddRange((select[i] as Shell).Faces);
            }
            if (faces.Count > 1)
            {
                Shell[] res = SewFaces(faces.ToArray());
                return new GeoObjectList(res);
            }
            else return null;
        }
        public static Shell[] SewFaces(Face[] faces, bool edgesArUnambiguous = false)
        {
            // isolate all faces from other faces by making all edges open edges (only PrimaryFace is set)
            for (int i = 0; i < faces.Length; i++)
            {
                foreach (Edge edg in faces[i].AllEdges)
                {
                    edg.Vertex1.RemoveAllEdges();
                    edg.Vertex2.RemoveAllEdges();
                    if (edg.PrimaryFace != faces[i])
                    {
                        Edge nedge = new Edge(faces[i], edg.Curve3D, faces[i], edg.SecondaryCurve2D, edg.Forward(faces[i]));
                        faces[i].SubstitudeEdge(edg, nedge);
                    }
                    else if (edg.SecondaryFace != null)
                    {
                        Edge nedge = new Edge(edg.SecondaryFace, edg.Curve3D, faces[i], edg.SecondaryCurve2D, edg.Forward(edg.SecondaryFace));
                        edg.SecondaryFace.SubstitudeEdge(edg, nedge);
                        edg.RemoveFace(edg.SecondaryFace);
                    }
                }
            }
            // make all the vertices to only know the edges of faces (from parameter)
            for (int i = 0; i < faces.Length; i++)
            {
                foreach (Vertex vtx in faces[i].Vertices)
                {
                    vtx.RemoveAllEdges();
                }
            }
            for (int i = 0; i < faces.Length; i++)
            {
                foreach (Edge edg in faces[i].AllEdges)
                {
                    edg.Vertex1.AddEdge(edg);
                    edg.Vertex2.AddEdge(edg);
                }
            }
#if DEBUG
            for (int i = 0; i < faces.Length; i++)
            {
                foreach (Edge edg in faces[i].AllEdges)
                {
                    System.Diagnostics.Debug.Assert(edg.SecondaryFace == null);
                }
                bool ok = faces[i].CheckConsistency();
                if (!ok)
                { }
            }
#endif
            List<Vertex> allVertices = new List<Vertex>();
            BoundingCube ext = BoundingCube.EmptyBoundingCube;
            for (int i = 0; i < faces.Length; i++)
            {
                Vertex[] v = faces[i].Vertices;
                allVertices.AddRange(v);
                for (int j = 0; j < v.Length; j++)
                {
                    ext.MinMax(v[j].Position);
                }
            }
            double precision = ext.Size * 1e-6;
            OctTree<Vertex> vtxs = new OctTree<Vertex>(ext, precision);
            for (int i = 0; i < allVertices.Count; i++)
            {
                bool merged = false;
                if (i > 0)
                {   // den leeren OctTree darf man nicht fragen
                    Vertex[] close = vtxs.GetObjectsCloseTo(allVertices[i]);
                    for (int k = 0; k < close.Length; k++)
                    {
                        if ((close[k].Position | allVertices[i].Position) < precision)
                        {
                            close[k].MergeWith(allVertices[i]);
                            merged = true;
                            break;
                        }
                    }
                }
                if (!merged)
                {
                    vtxs.AddObject(allVertices[i]);
                }
            }
            // der OctTree enthält jetzt alle zu verwendenden Vertices
            for (int i = 0; i < faces.Length; i++)
            {
                foreach (Edge edg in faces[i].AllEdges)
                {
                    if (edg.SecondaryFace == null)
                    {
                        foreach (Edge ce in Vertex.ConnectingEdges(edg.Vertex1, edg.Vertex2))
                        {
                            if (edg != ce && ce.SecondaryFace == null)
                            {
                                // zwei verschiedene edges verbinden die selben vertices
                                if (edg.Curve3D != null && ce.Curve3D != null)
                                {
                                    // SameGeometry too expensive, DistanceTo for the middle point is a sufficient condition
                                    //if (edg.Curve3D.SameGeometry(ce.Curve3D, precision))
                                    //{
                                    //    Face f = ce.PrimaryFace;
                                    //    f.ReplaceEdge(ce, edg);
                                    //    ce.Vertex1.RemoveEdge(ce);
                                    //    ce.Vertex2.RemoveEdge(ce);
                                    //    break;
                                    //}
                                    //else
                                    //{
                                    double dd = edg.Curve3D.DistanceTo(ce.Curve3D.PointAt(0.5));
                                    if (dd < precision)
                                    {
                                        Face f = ce.PrimaryFace;
                                        f.ReplaceEdge(ce, edg);
                                        ce.Vertex1.RemoveEdge(ce);
                                        ce.Vertex2.RemoveEdge(ce);
                                        break;
                                    }
                                    //}
                                }
                            }
                        }
                    }
                }
            }
            Set<Face> allFaces = new Set<Face>(faces);
            List<Shell> res = new List<Shell>();
            while (allFaces.Count > 0)
            {
                Set<Face> sf = new Set<Face>();
                extractConnectedFaces(allFaces, allFaces.GetAny(), sf);
#if DEBUG
                foreach (Face fc in sf)
                {
                    bool ok = fc.CheckConsistency();
                }
#endif
                Shell shell = Shell.MakeShell(sf.ToArray());
                shell.AssertOutwardOrientation();
                res.Add(shell);
#if DEBUG
                // System.Diagnostics.Debug.Assert(shell.OpenEdges.Length == 0);
#endif
            }
            return res.ToArray();
        }
        private static void extractConnectedFaces(Set<Face> allFaces, Face startWith, Set<Face> result)
        {
            result.Add(startWith);
            allFaces.Remove(startWith);
            foreach (Edge edge in startWith.Edges)
            {
                if (allFaces.Contains(edge.PrimaryFace))
                {
                    extractConnectedFaces(allFaces, edge.PrimaryFace, result);
                }
                if (allFaces.Contains(edge.SecondaryFace))
                {
                    extractConnectedFaces(allFaces, edge.SecondaryFace, result);
                }
            }
        }

        public IGeoObject GetShape(Project project)
        {
            IGeoObject[] res = GetShapes(project);
            if (res.Length >= 1) return res[0];
            return null;
        }
        public IGeoObject[] GetShapes(Project project)
        {
            throw new NotImplementedException();
        }
        internal static void MakeCanonicalForms(Edge[] edges)
        {
            Dictionary<Face, ModOp2D> canonical = new Dictionary<Face, ModOp2D>();
            for (int i = 0; i < edges.Length; ++i)
            {
                ModOp2D m = ModOp2D.Identity;
                if (!canonical.ContainsKey(edges[i].PrimaryFace))
                {
                    m = edges[i].PrimaryFace.Surface.MakeCanonicalForm();
                    canonical[edges[i].PrimaryFace] = m;
                }
                else
                {
                    m = canonical[edges[i].PrimaryFace];
                }
                if (!m.IsIdentity)
                {
                    edges[i].PrimaryCurve2D = edges[i].PrimaryCurve2D.GetModified(m);
                    if (Math.Abs(edges[i].PrimaryCurve2D.StartPoint.y) < 1e-8)
                    {
                        edges[i].PrimaryCurve2D.StartPoint = new GeoPoint2D(edges[i].PrimaryCurve2D.StartPoint.x, 0.0);
                    }
                    if (Math.Abs(edges[i].PrimaryCurve2D.EndPoint.y) < 1e-8)
                    {
                        edges[i].PrimaryCurve2D.EndPoint = new GeoPoint2D(edges[i].PrimaryCurve2D.EndPoint.x, 0.0);
                    }
                    edges[i].PrimaryFace.InvalidateArea();
                }
                if (edges[i].SecondaryFace != null)
                {
                    m = ModOp2D.Identity;
                    if (!canonical.ContainsKey(edges[i].SecondaryFace))
                    {
                        m = edges[i].SecondaryFace.Surface.MakeCanonicalForm();
                        canonical[edges[i].SecondaryFace] = m;
                    }
                    else
                    {
                        m = canonical[edges[i].SecondaryFace];
                    }
                    if (!m.IsIdentity)
                    {
                        edges[i].SecondaryCurve2D = edges[i].SecondaryCurve2D.GetModified(m);
                        if (Math.Abs(edges[i].SecondaryCurve2D.StartPoint.y) < 1e-8)
                        {
                            edges[i].SecondaryCurve2D.StartPoint = new GeoPoint2D(edges[i].SecondaryCurve2D.StartPoint.x, 0.0);
                        }
                        if (Math.Abs(edges[i].SecondaryCurve2D.EndPoint.y) < 1e-8)
                        {
                            edges[i].SecondaryCurve2D.EndPoint = new GeoPoint2D(edges[i].SecondaryCurve2D.EndPoint.x, 0.0);
                        }
                        edges[i].SecondaryFace.InvalidateArea();
                    }
                }
            }
        }
        internal static GeoObjectList Union(GeoObjectList select)
        {
            throw new NotImplementedException();
        }
        /// <summary>
        /// Returns the intersection of the two given <see cref="Solid"/>s. If the solids are disjunct, null will be returned.
        /// s1 and s2 will be intersected, the common solid is returned, which may consist of several solids
        /// </summary>
        /// <param name="s1">first solid</param>
        /// <param name="s2">second solid</param>
        /// <param name="project">the project to find attributes</param>
        /// <returns>intersection of the two solids or null</returns>
        public static Solid[] Intersection(Solid solid1, Solid solid2)
        {
            throw new NotImplementedException();
        }
        public static Shell[] Intersection(Shell shell1, Solid solid2)
        {
            throw new NotImplementedException();
        }
        public static IGeoObject[] Intersection(Face face1, Face face2)
        {
            throw new NotImplementedException();
        }
        static public Solid MakeBox(GeoPoint location, GeoVector directionX, GeoVector directionY, GeoVector directionZ)
        {
            // the three provided vectors must be in a right handed system
            GeoVector dirz = directionX ^ directionY;
            Angle a = new Angle(directionZ, dirz);
            bool rh = a.Radian < Math.PI / 2.0;
            if (!rh)
            {
                // simply exchange directionX and directionY to make it right handed
                dirz = directionX;
                directionX = directionY;
                directionY = dirz;
            }
            GeoPoint p1 = location;
            GeoPoint p2 = p1 + directionX;
            GeoPoint p3 = p1 + directionX + directionY;
            GeoPoint p4 = p1 + directionY;
            GeoPoint p5 = location + directionZ;
            GeoPoint p6 = p5 + directionX;
            GeoPoint p7 = p5 + directionX + directionY;
            GeoPoint p8 = p5 + directionY;
            // construct the planes so that the normal vectors are outward oriented
            PlaneSurface pls1 = new PlaneSurface(p1, p4, p2);
            PlaneSurface pls2 = new PlaneSurface(p1, p2, p5);
            PlaneSurface pls3 = new PlaneSurface(p2, p3, p6);
            PlaneSurface pls4 = new PlaneSurface(p1, p5, p4);
            PlaneSurface pls5 = new PlaneSurface(p3, p4, p7);
            PlaneSurface pls6 = new PlaneSurface(p5, p6, p8);
            Face fc1 = Face.Construct();
            Face fc2 = Face.Construct();
            Face fc3 = Face.Construct();
            Face fc4 = Face.Construct();
            Face fc5 = Face.Construct();
            Face fc6 = Face.Construct();
            Shell sh = Shell.Construct();
            // could improve the following, but probably wouldn't make a big difference
            Line l1 = Line.MakeLine(p1, p2);
            Edge e1 = new Edge(fc1, l1, fc1, pls1.GetProjectedCurve(l1, 0.0).CloneReverse(true), false, fc2, pls2.GetProjectedCurve(l1, 0.0), true);
            Line l2 = Line.MakeLine(p2, p3);
            Edge e2 = new Edge(fc1, l2, fc1, pls1.GetProjectedCurve(l2, 0.0).CloneReverse(true), false, fc3, pls3.GetProjectedCurve(l2, 0.0), true);
            Line l3 = Line.MakeLine(p3, p4);
            Edge e3 = new Edge(fc1, l3, fc1, pls1.GetProjectedCurve(l3, 0.0).CloneReverse(true), false, fc5, pls5.GetProjectedCurve(l3, 0.0), true);
            Line l4 = Line.MakeLine(p4, p1);
            Edge e4 = new Edge(fc1, l4, fc1, pls1.GetProjectedCurve(l4, 0.0).CloneReverse(true), false, fc4, pls4.GetProjectedCurve(l4, 0.0), true);
            Line l5 = Line.MakeLine(p5, p6);
            Edge e5 = new Edge(fc6, l5, fc6, pls6.GetProjectedCurve(l5, 0.0), true, fc2, pls2.GetProjectedCurve(l5, 0.0).CloneReverse(true), false);
            Line l6 = Line.MakeLine(p6, p7);
            Edge e6 = new Edge(fc6, l6, fc6, pls6.GetProjectedCurve(l6, 0.0), true, fc3, pls3.GetProjectedCurve(l6, 0.0).CloneReverse(true), false);
            Line l7 = Line.MakeLine(p7, p8);
            Edge e7 = new Edge(fc6, l7, fc6, pls6.GetProjectedCurve(l7, 0.0), true, fc5, pls5.GetProjectedCurve(l7, 0.0).CloneReverse(true), false);
            Line l8 = Line.MakeLine(p8, p5);
            Edge e8 = new Edge(fc6, l8, fc6, pls6.GetProjectedCurve(l8, 0.0), true, fc4, pls4.GetProjectedCurve(l8, 0.0).CloneReverse(true), false);
            Line l9 = Line.MakeLine(p1, p5);
            Edge e9 = new Edge(fc4, l9, fc4, pls4.GetProjectedCurve(l9, 0.0), true, fc2, pls2.GetProjectedCurve(l9, 0.0).CloneReverse(true), false);
            Line l10 = Line.MakeLine(p2, p6);
            Edge e10 = new Edge(fc2, l10, fc2, pls2.GetProjectedCurve(l10, 0.0), true, fc3, pls3.GetProjectedCurve(l10, 0.0).CloneReverse(true), false);
            Line l11 = Line.MakeLine(p3, p7);
            Edge e11 = new Edge(fc3, l11, fc3, pls3.GetProjectedCurve(l11, 0.0), true, fc5, pls5.GetProjectedCurve(l11, 0.0).CloneReverse(true), false);
            Line l12 = Line.MakeLine(p4, p8);
            Edge e12 = new Edge(fc5, l12, fc5, pls5.GetProjectedCurve(l12, 0.0), true, fc4, pls4.GetProjectedCurve(l12, 0.0).CloneReverse(true), false);
            fc1.Set(pls1, new Edge[] { e1, e4, e3, e2 }, null);
            fc2.Set(pls2, new Edge[] { e1, e10, e5, e9 }, null);
            fc3.Set(pls3, new Edge[] { e2, e11, e6, e10 }, null);
            fc4.Set(pls4, new Edge[] { e4, e9, e8, e12 }, null);
            fc5.Set(pls5, new Edge[] { e3, e12, e7, e11 }, null);
            fc6.Set(pls6, new Edge[] { e5, e6, e7, e8 }, null);
            //fc1.RepairConnected();
            //fc2.RepairConnected();
            //fc3.RepairConnected();
            //fc4.RepairConnected();
            //fc5.RepairConnected();
            //fc6.RepairConnected();
            //fc1.RepairConnected();
            // sh = Shell.MakeShell(new Face[] { fc1, fc2, fc3, fc4, fc5, fc6 });
            sh.SetFaces(new Face[] { fc1, fc2, fc3, fc4, fc5, fc6 });
            sh.RecalcVertices(); // maybe we have duplicate vertices
            Solid res = Solid.Construct();
            res.SetShell(sh);
            return res;
        }
#if USENONPRIODICSURFACES
        static public Solid MakeCylinder(GeoPoint location, GeoVector directionX, GeoVector directionZ)
        {
            GeoVector directionY = directionZ ^ directionX;
            directionY = directionX.Length * directionY.Normalized;
            Plane pln1 = new Plane(location, directionX, -directionY); // is oriented away from directionZ
            PlaneSurface psbottom = new PlaneSurface(pln1);
            Plane pln2 = new Plane(location + directionZ, directionX, directionY); // normal is directionZ
            PlaneSurface pstop = new PlaneSurface(pln2);
            Ellipse eb = Ellipse.Construct();
            eb.SetCirclePlaneCenterRadius(psbottom.Plane, location, directionX.Length);
            Ellipse et = Ellipse.Construct();
            et.SetCirclePlaneCenterRadius(pstop.Plane, location + directionZ, directionX.Length);

            CylindricalSurfaceNP cs = new CylindricalSurfaceNP(location, directionX.Length, directionZ, true, new ICurve[] { eb, et });
            Border outline = new Border(cs.GetProjectedCurve(eb, 0.0));
            Border hole = new Border(cs.GetProjectedCurve(et, 0.0).CloneReverse(true));
            SimpleShape ss = new SimpleShape(outline, hole);
            Face cyl = Face.MakeFace(cs, ss);

            Face bottom = Face.MakeFace(psbottom, new SimpleShape(new Border(psbottom.GetProjectedCurve(eb, 0.0))));
            Face top = Face.MakeFace(pstop, new SimpleShape(new Border(pstop.GetProjectedCurve(et, 0.0))));
            Shell shell = Shell.FromFaces(cyl, bottom, top);
            return Solid.MakeSolid(shell);
        }
#else
        static public Solid MakeCylinder(GeoPoint location, GeoVector directionX, GeoVector directionZ)
        {
            GeoVector directionY = directionZ ^ directionX;
            directionY = directionX.Length * directionY.Normalized;
            CylindricalSurface cs = new CylindricalSurface(location, directionX, directionY, directionZ.Normalized);
            //Face cf1 = Face.MakeFace(cs.Clone(), new SimpleShape(Border.MakeRectangle(0.0, Math.PI, 0.0, directionZ.Length)));
            //Face cf2 = Face.MakeFace(cs.Clone(), new SimpleShape(Border.MakeRectangle(Math.PI, 2.0*Math.PI, 0.0, directionZ.Length)));
            Plane pln1 = new Plane(location, directionX, directionY);
            PlaneSurface psbottom = new PlaneSurface(pln1);
            Plane pln2 = new Plane(location + directionZ, directionX, directionY);
            PlaneSurface pstop = new PlaneSurface(pln2);
            Line l1 = Line.Construct();
            l1.SetTwoPoints(location + directionX, location + directionZ + directionX);
            Line l2 = Line.Construct();
            l2.SetTwoPoints(location - directionX, location + directionZ - directionX);
            Ellipse eb1 = Ellipse.Construct();
            eb1.SetArcPlaneCenterRadiusAngles(psbottom.Plane, location, directionX.Length, 0.0, Math.PI);
            Ellipse eb2 = Ellipse.Construct();
            eb2.SetArcPlaneCenterRadiusAngles(psbottom.Plane, location, directionX.Length, Math.PI, Math.PI);
            Ellipse et1 = Ellipse.Construct();
            et1.SetArcPlaneCenterRadiusAngles(pstop.Plane, location + directionZ, directionX.Length, 0.0, Math.PI);
            Ellipse et2 = Ellipse.Construct();
            et2.SetArcPlaneCenterRadiusAngles(pstop.Plane, location + directionZ, directionX.Length, Math.PI, Math.PI);

            Shell sh = Shell.Construct();
            Face cf1 = Face.Construct();
            Face cf2 = Face.Construct();
            Face btn = Face.Construct();
            Face top = Face.Construct();
            sh.SetFaces(new Face[] { cf1, cf2, btn, top });
            double height = directionZ.Length;
            Line2D l2d1a = new Line2D(new GeoPoint2D(0.0, height), new GeoPoint2D(0.0, 0.0));
            Line2D l2d1b = new Line2D(new GeoPoint2D(2.0 * Math.PI, 0.0), new GeoPoint2D(2.0 * Math.PI, height));
            Line2D l2d2 = new Line2D(new GeoPoint2D(Math.PI, 0.0), new GeoPoint2D(Math.PI, height));
            Line2D leb1 = new Line2D(new GeoPoint2D(0.0, 0.0), new GeoPoint2D(Math.PI, 0.0));
            Line2D leb2 = new Line2D(new GeoPoint2D(Math.PI, 0.0), new GeoPoint2D(Math.PI * 2.0, 0.0));
            Line2D let1 = new Line2D(new GeoPoint2D(Math.PI, height), new GeoPoint2D(0.0, height));
            Line2D let2 = new Line2D(new GeoPoint2D(Math.PI * 2.0, height), new GeoPoint2D(Math.PI, height));
            Edge e1 = new Edge(sh, l1, cf1, l2d1a, false, cf2, l2d1b, true);
            Edge e2 = new Edge(sh, l2, cf1, l2d2, true, cf2, l2d2.CloneReverse(true), false); // we cannot use the same 2d curve in two different edges
            Edge e3 = new Edge(sh, eb1, cf1, leb1, false, btn, psbottom.GetProjectedCurve(eb1, 0.0), true);
            Edge e4 = new Edge(sh, eb2, cf2, leb2, false, btn, psbottom.GetProjectedCurve(eb2, 0.0), true);
            Edge e5 = new Edge(sh, et1, cf1, let1, false, top, pstop.GetProjectedCurve(et1, 0.0), true);
            Edge e6 = new Edge(sh, et2, cf2, let2, false, top, pstop.GetProjectedCurve(et2, 0.0), true);

            cf1.Set(cs.Clone(), new Edge[] { e3, e2, e5, e1 }, null);
            cf2.Set(cs.Clone(), new Edge[] { e1, e6, e2, e4 }, null);
            btn.Set(psbottom, new Edge[] { e3, e4 }, null);
            top.Set(pstop, new Edge[] { e5, e6 }, null);
            // besser wäre es, die Kanten und Flächen gleich in der richtigen Orientierung zu erzeugen
            // aber das gibt einen Knoten im Hirn (es sei denn man nimmt Papier und Stift), deshalb hier brutal 
            // richtig machen durch Area (Problem ist die untere Ebene, die muss verkehrtrum!)
            SimpleShape makeOrientation = cf1.Area;
            makeOrientation = cf2.Area;
            makeOrientation = btn.Area;
            makeOrientation = top.Area;
            sh.AssertOutwardOrientation();
            sh.RecalcVertices();

            Solid res = Solid.Construct();
            res.SetShell(sh);
            return res;

            //directionY.Length = directionX.Length;
            //Import.ImportOCAS imp = Import.ImportOCAS.MakeCylinder(location.ToCndHlp(), directionX.ToCndHlp(), directionY.ToCndHlp(), directionZ.ToCndHlp());
            //if (imp == null) return null;
            //Make3D m3d = new Make3D(imp);
            //IGeoObject shp = m3d.GetShape(null);
            //return shp as Solid;
        }
#endif
        static public Shell MakeCylinderShell(GeoPoint location, GeoVector directionX, GeoVector directionZ)
        {
            GeoVector directionY = directionZ ^ directionX;
            directionY = directionX.Length * directionY.Normalized;
            CylindricalSurface cs = new CylindricalSurface(location, directionX, directionY, directionZ.Normalized);
            //Face cf1 = Face.MakeFace(cs.Clone(), new SimpleShape(Border.MakeRectangle(0.0, Math.PI, 0.0, directionZ.Length)));
            //Face cf2 = Face.MakeFace(cs.Clone(), new SimpleShape(Border.MakeRectangle(Math.PI, 2.0*Math.PI, 0.0, directionZ.Length)));
            Plane pln1 = new Plane(location, directionX, directionY);
            PlaneSurface psbottom = new PlaneSurface(pln1);
            Plane pln2 = new Plane(location + directionZ, directionX, directionY);
            PlaneSurface pstop = new PlaneSurface(pln2);
            Line l1 = Line.Construct();
            l1.SetTwoPoints(location + directionX, location + directionZ + directionX);
            Line l2 = Line.Construct();
            l2.SetTwoPoints(location - directionX, location + directionZ - directionX);
            Ellipse eb1 = Ellipse.Construct();
            eb1.SetArcPlaneCenterRadiusAngles(psbottom.Plane, location, directionX.Length, 0.0, Math.PI);
            Ellipse eb2 = Ellipse.Construct();
            eb2.SetArcPlaneCenterRadiusAngles(psbottom.Plane, location, directionX.Length, Math.PI, Math.PI);
            Ellipse et1 = Ellipse.Construct();
            et1.SetArcPlaneCenterRadiusAngles(pstop.Plane, location + directionZ, directionX.Length, 0.0, Math.PI);
            Ellipse et2 = Ellipse.Construct();
            et2.SetArcPlaneCenterRadiusAngles(pstop.Plane, location + directionZ, directionX.Length, Math.PI, Math.PI);

            Shell sh = Shell.Construct();
            Face cf1 = Face.Construct();
            Face cf2 = Face.Construct();
            sh.SetFaces(new Face[] { cf1, cf2 });
            double height = directionZ.Length;
            Line2D l2d1a = new Line2D(new GeoPoint2D(0.0, 0.0), new GeoPoint2D(0.0, height));
            Line2D l2d1b = new Line2D(new GeoPoint2D(2.0 * Math.PI, 0.0), new GeoPoint2D(2.0 * Math.PI, height));
            Line2D l2d2 = new Line2D(new GeoPoint2D(Math.PI, 0.0), new GeoPoint2D(Math.PI, height));
            Line2D leb1 = new Line2D(new GeoPoint2D(0.0, 0.0), new GeoPoint2D(Math.PI, 0.0));
            Line2D leb2 = new Line2D(new GeoPoint2D(Math.PI, 0.0), new GeoPoint2D(Math.PI * 2.0, 0.0));
            Line2D let1 = new Line2D(new GeoPoint2D(Math.PI, height), new GeoPoint2D(0.0, height));
            Line2D let2 = new Line2D(new GeoPoint2D(Math.PI * 2.0, height), new GeoPoint2D(Math.PI, height));
            Edge e1 = new Edge(sh, l1, cf1, l2d1a, false, cf2, l2d1b, true);
            Edge e2 = new Edge(sh, l2, cf1, l2d2.Clone(), true, cf2, l2d2, false); // es darf nicht die selbe 2d Kurve 2mal verwendet werden
                                                                                   //Edge e3 = new Edge(sh, eb1, cf1, cs.GetProjectedCurve(eb1, 0.0), true, btn, psbottom.GetProjectedCurve(eb1, 0.0), true);
                                                                                   //Edge e4 = new Edge(sh, eb2, cf2, cs.GetProjectedCurve(eb2, 0.0), true, btn, psbottom.GetProjectedCurve(eb2, 0.0), true);
                                                                                   //Edge e5 = new Edge(sh, et1, cf1, cs.GetProjectedCurve(et1, 0.0), false, top, pstop.GetProjectedCurve(et1, 0.0), true);
                                                                                   //Edge e6 = new Edge(sh, et2, cf2, cs.GetProjectedCurve(et2, 0.0), false, top, pstop.GetProjectedCurve(et2, 0.0), true);
            Edge e3 = new Edge(sh, eb1, cf1, leb1, true);
            Edge e4 = new Edge(sh, eb2, cf2, leb2, true);
            Edge e5 = new Edge(sh, et1, cf1, let1, false);
            Edge e6 = new Edge(sh, et2, cf2, let2, false);

            cf1.Set(cs.Clone(), new Edge[] { e3, e2, e5, e1 }, null);
            cf2.Set(cs.Clone(), new Edge[] { e1, e6, e2, e4 }, null);
            // besser wäre es, die Kanten und Flächen gleich in der richtigen Orientierung zu erzeugen
            // aber das gibt einen Knoten im Hirn (es sei denn man nimmt Papier und Stift), deshalb hier brutal 
            // richtig machen durch Area
            SimpleShape makeOrientation = cf1.Area;
            makeOrientation = cf2.Area;
            sh.AssertOutwardOrientation();

            return sh;
        }

        /// <summary>
        /// Creates a cone. The cone is defined by two parallel discs with two different radii. The first disc is centered
        /// at location, the second at location + directionZ. directionZ is also the normal vector of both discs.
        /// directionX must be perpendicular to directionZ and specifies the startpoint of the circular edge.
        /// </summary>
        /// <param name="location">location of the cone</param>
        /// <param name="directionX">startpoint of edge</param>
        /// <param name="directionZ">axis of the cone</param>
        /// <param name="radius1">radius at location</param>
        /// <param name="radius2">radius at location + directionZ</param>
        /// <returns></returns>
        static public Solid MakeCone(GeoPoint location, GeoVector directionX, GeoVector directionZ, double radius1, double radius2)
        {
            if (radius1 == radius2) return null;
            // directionX must be perpendicular to directionZ
            directionX = ((directionZ ^ directionX) ^ directionZ).Normalized;
            GeoVector directionY = (directionZ ^ directionX).Normalized;
            GeoVector coneSurfaceLine = (location + radius1 * directionX) - (location + directionZ + radius2 * directionX);
            GeoPoint apex = Geometry.IntersectLL(location, directionZ, location + radius1 * directionX, coneSurfaceLine);
            Angle halfOpening = new Angle(directionZ, coneSurfaceLine);
            if (halfOpening > Math.PI / 2.0) halfOpening = Math.PI - halfOpening;
            ConicalSurface cs = new ConicalSurface(apex, directionX, directionY, directionZ.Normalized, halfOpening);
            double vmin = cs.PositionOf(location).y;
            double vmax = cs.PositionOf(location + directionZ).y;
            if (vmin > vmax)
            {
                double tmp = vmin;
                vmin = vmax;
                vmax = tmp;
            }
            Face f1 = Face.MakeFace(cs, new SimpleShape(Border.MakeRectangle(0, Math.PI, vmin, vmax)));
            Face f2 = Face.MakeFace(cs, new SimpleShape(Border.MakeRectangle(Math.PI, Math.PI * 2, vmin, vmax)));
            Plane pln1 = new Plane(location, directionX, directionY);
            Plane pln2 = new Plane(location + directionZ, directionX, directionY);
            Border bdr1 = Border.MakeCircle(GeoPoint2D.Origin, radius1);
            Border bdr2 = Border.MakeCircle(GeoPoint2D.Origin, radius2);
            bdr1.SplitSingleCurve(); // makes two half circles, splitted at 180°
            bdr2.SplitSingleCurve();
            Face f3 = Face.MakeFace(new PlaneSurface(pln1), new SimpleShape(bdr1));
            Face f4 = Face.MakeFace(new PlaneSurface(pln2), new SimpleShape(bdr2));
            Shell[] sh = SewFaces(new Face[] { f1, f2, f3, f4 });
            if (sh.Length == 1) return Solid.MakeSolid(sh[0]);
            return null;
        }
        static public Solid MakeSphere(GeoPoint location, double radius)
        {
            SphericalSurface ss = new SphericalSurface(location, radius * GeoVector.XAxis, radius * GeoVector.YAxis, radius * GeoVector.ZAxis);
            Border bdr = Border.MakeRectangle(0, Math.PI, -Math.PI / 2, Math.PI / 2);
            Face f1 = Face.MakeFace(ss, new SimpleShape(Border.MakeRectangle(0, Math.PI, -Math.PI / 2, Math.PI / 2)));
            Face f2 = Face.MakeFace(ss, new SimpleShape(Border.MakeRectangle(Math.PI, Math.PI * 2, -Math.PI / 2, Math.PI / 2)));
            Shell[] sh = SewFaces(new Face[] { f1, f2 });
            if (sh.Length == 1) return Solid.MakeSolid(sh[0]);
            return null;
        }
        static public Solid MakeTorus(GeoPoint location, GeoVector normal, double radius1, double radius2)
        {
            GeoVector other;
            if (Math.Abs(normal.x) <= Math.Abs(normal.y) && Math.Abs(normal.x) <= Math.Abs(normal.z))
            {
                other = GeoVector.XAxis;
            }
            else if (Math.Abs(normal.y) <= Math.Abs(normal.x) && Math.Abs(normal.y) <= Math.Abs(normal.z))
            {
                other = GeoVector.YAxis;
            }
            else
            {
                other = GeoVector.ZAxis;
            }
            GeoVector directionX = normal ^ other;
            GeoVector directionY = normal ^ directionX;
            ToroidalSurface ts = new ToroidalSurface(location, directionX, directionY, normal, radius1, radius2);
            Face[] fcs = new Face[4];
            fcs[0] = Face.MakeFace(ts, new BoundingRect(0, 0, Math.PI, Math.PI));
            fcs[1] = Face.MakeFace(ts, new BoundingRect(0, Math.PI, Math.PI, 2 * Math.PI));
            fcs[2] = Face.MakeFace(ts, new BoundingRect(Math.PI, 0, 2 * Math.PI, Math.PI));
            fcs[3] = Face.MakeFace(ts, new BoundingRect(Math.PI, Math.PI, 2 * Math.PI, 2 * Math.PI));
            Shell[] shell = SewFaces(fcs);
            if (shell.Length == 1) return Solid.MakeSolid(shell[0]);
            return null;
            //throw new NotImplementedException();
        }
        static internal Solid MakeTetraeder(GeoPoint p1, GeoPoint p2, GeoPoint p3, GeoPoint p4)
        {
            PlaneSurface pls1 = new PlaneSurface(p1, p2, p3);
            GeoPoint ptest = pls1.Plane.ToLocal(p4);
            if (Math.Abs(ptest.z) < Precision.eps) return null;
            if (ptest.z > 0.0)
            {   // es soll so orientiert sein, dass p1,p2,p3 ein Dreieck bilden, linksrum orientiert und dann p4 unten liegt
                ptest = p3;
                p3 = p4;
                p4 = ptest;
                pls1 = new PlaneSurface(p1, p2, p3);
            }
            PlaneSurface pls2 = new PlaneSurface(p2, p4, p3);
            PlaneSurface pls3 = new PlaneSurface(p1, p3, p4);
            PlaneSurface pls4 = new PlaneSurface(p1, p4, p2);
            Face fc1 = Face.Construct();
            Face fc2 = Face.Construct();
            Face fc3 = Face.Construct();
            Face fc4 = Face.Construct();
            Shell sh = Shell.Construct();
            // Schneller machen: GetProjectedCurve könnte aufgelöst werden, Face.Set könnte gleich die Area
            // mitgeliefert bekommen, das ist dort unnötig aufwendig.
            sh.SetFaces(new Face[] { fc1, fc2, fc3, fc4 });
            Line l1 = Line.MakeLine(p3, p4);
            Edge e1 = new Edge(sh, l1, fc2, pls2.GetProjectedCurve(l1, 0.0), false, fc3, pls3.GetProjectedCurve(l1, 0.0), true);
            Line l2 = Line.MakeLine(p1, p2);
            Edge e2 = new Edge(sh, l2, fc1, pls1.GetProjectedCurve(l2, 0.0), true, fc4, pls4.GetProjectedCurve(l2, 0.0), false);
            Line l3 = Line.MakeLine(p2, p4);
            Edge e3 = new Edge(sh, l3, fc2, pls2.GetProjectedCurve(l3, 0.0), true, fc4, pls4.GetProjectedCurve(l3, 0.0), false);
            Line l4 = Line.MakeLine(p2, p3);
            Edge e4 = new Edge(sh, l4, fc1, pls1.GetProjectedCurve(l4, 0.0), true, fc2, pls2.GetProjectedCurve(l4, 0.0), false);
            Line l5 = Line.MakeLine(p1, p3);
            Edge e5 = new Edge(sh, l5, fc1, pls1.GetProjectedCurve(l5, 0.0), true, fc3, pls3.GetProjectedCurve(l5, 0.0), false);
            Line l6 = Line.MakeLine(p1, p4);
            Edge e6 = new Edge(sh, l6, fc4, pls4.GetProjectedCurve(l6, 0.0), true, fc3, pls3.GetProjectedCurve(l6, 0.0), false);
            fc1.Set(pls1, new Edge[] { e4, e5, e2 }, null);
            fc2.Set(pls2, new Edge[] { e1, e4, e3 }, null);
            fc3.Set(pls3, new Edge[] { e1, e6, e5 }, null);
            fc4.Set(pls4, new Edge[] { e3, e2, e6 }, null);
            Solid res = Solid.Construct();
            res.SetShell(sh);
            return res;
        }
        /// <summary>
        /// INTERN! Macht aus einer Kurve und einer Richtung ein Face. Es entstehen 4 Edges: 0: die Kurve curve selbst, 1: Endpunkt
        /// der Kurve plus extrusion, 2: versetzte rükwärtige Kurve, 3: inverse zu 1, endet am Startpunkt von curve. Evtl. können
        /// einige der Edges bereits existieren und sollen dann verwendet werden, so spart man sich das nachträgliche zusammenfügen 
        /// einzelner Faces zu einer Shell. Die Parameter forward0 bis forward3 geben an, ob die 3D Kurve in der Edge die
        /// richtige Richtung hat oder nicht.
        /// </summary>
        /// <param name="curve"></param>
        /// <param name="extrusion"></param>
        /// <param name="edge0"></param>
        /// <param name="edge1"></param>
        /// <param name="edge2"></param>
        /// <param name="edge3"></param>
        /// <returns></returns>
        static Face ExtrudeCurveToFace(ICurve curve, GeoVector extrusion,
            Edge edge0, Edge edge1, Edge edge2, Edge edge3,
            bool forward0, bool forward1, bool forward2, bool forward3)
        {
            // 1. surface bestimmen
            Face res = Face.Construct();
            if (curve is Line)
            {
                Line line = curve as Line;
                if (Precision.SameDirection(extrusion, line.StartDirection, false)) return null;
                Plane pln = new Plane(line.StartPoint, line.StartDirection, extrusion);
                PlaneSurface surface = new PlaneSurface(pln);
                res.Surface = surface;
            }
            else if (curve is Ellipse)
            {
                Ellipse elli = curve as Ellipse;
                if (Precision.SameDirection(extrusion, elli.Plane.Normal, false) && elli.IsCircle)
                {   // den Zylinder so orientieren, dass es nicht über die Naht geht
                    GeoPoint p0 = elli.PointAt(0.5);
                    GeoVector maj = -(p0 - elli.Center);
                    GeoVector min = extrusion.Normalized ^ maj;
                    CylindricalSurface cyl = new CylindricalSurface(elli.Center, maj, min, extrusion.Normalized);
                    res.Surface = cyl;
                }
            }
            if (res.internalSurface == null)
            {
                SurfaceOfLinearExtrusion le = new SurfaceOfLinearExtrusion(curve, extrusion, 0.0, 1.0);
                res.Surface = le;
            }
            res.area = new SimpleShape(Border.MakeRectangle(0, 1, 0, 1)); // Area wird ja nacher neu gemacht, aber der Zugriff auf Surface geht sonst nicht
            Edge[] edges = new Edge[4];
            if (edge0 == null)
            {
                ICurve curve0 = curve.Clone();
                edges[0] = new Edge(res, curve0, res, null, true);
            }
            else
            {
                edge0.SetFace(res, forward0);
                edges[0] = edge0;
            }
            if (edge1 == null)
            {
                Line l3d = Line.Construct();
                l3d.SetTwoPoints(curve.EndPoint, curve.EndPoint + extrusion);
                edges[1] = new Edge(res, l3d, res, null, true);
            }
            else
            {
                edge1.SetFace(res, forward1);
                edges[1] = edge1;
            }
            if (edge2 == null)
            {
                ModOp move = ModOp.Translate(extrusion);
                ICurve curve2 = curve.CloneModified(move);
                edges[2] = new Edge(res, curve2, res, null, false);
            }
            else
            {
                edge2.SetFace(res, forward2);
                edges[2] = edge2;
            }
            if (edge3 == null)
            {
                Line l3d = Line.Construct();
                l3d.SetTwoPoints(curve.StartPoint + extrusion, curve.StartPoint);
                edges[3] = new Edge(res, l3d, res, null, true);
            }
            else
            {
                edge3.SetFace(res, forward3);
                edges[3] = edge3;
            }
            ICurve2D[] c2ds = new ICurve2D[4];
            for (int i = 0; i < 4; i++)
            {
                c2ds[i] = edges[i].Curve2D(res);
            }
            if (Border.SignedArea(c2ds) < 0.0)
            {
                for (int i = 0; i < 4; i++)
                {
                    edges[i].ReverseOrientation(res);
                    edges[i].Curve2D(res).Reverse();
                }
                Array.Reverse(edges);
            }
            res.Set(res.internalSurface, edges, new Edge[0][]); // surface sitzt ja schon, wurde bereits gebraucht, aber area noch nicht!
            res.area = null;
            SimpleShape dbg = res.Area;
            return res;
        }
        /// <summary>
        /// Extrudes the provided object <paramref name="faceShellPathCurve"/> along the <paramref name="extension"/>.
        /// </summary>
        /// <param name="faceShellPathCurve">Object to extrude, may be a <see cref="Face"/>, <see cref="Shell"/>, <see cref="Path"/> or <see cref="ICurve"/> object</param>
        /// <param name="extension">Direction and length of extension</param>
        /// <param name="project">A project which is used to set default attributes to the result, may be null</param>
        /// <returns>The extruded face, shell or solid, null if extrusion not possible</returns>
        static public IGeoObject Extrude(IGeoObject faceShellPathCurve, GeoVector extension, Project project)
        {
            if (faceShellPathCurve is Path || faceShellPathCurve is Polyline)
            {   // Pfad vor der Kurve abhandeln, denn Path ist auch ICurve
                Path path;
                if (faceShellPathCurve is Polyline)
                {
                    path = Path.Construct();
                    path.Add(faceShellPathCurve as ICurve);
                }
                else if (faceShellPathCurve is Ellipse) // Vollkreis
                {
                    path = Path.Construct();
                    ICurve cv = faceShellPathCurve as ICurve;
                    path.Set(cv.Split(0.5));
                }
                else
                {
                    path = faceShellPathCurve.Clone() as Path;
                }
                path.Flatten(); // Flatten wirft zu kurze segmente hoffentlich raus
                Edge[] edges = new Edge[path.CurveCount + 1];
                for (int i = 0; i < path.CurveCount; ++i)
                {
                    Line l3d = Line.Construct();
                    l3d.SetTwoPoints(path.Curve(i).StartPoint, path.Curve(i).StartPoint + extension);
                    edges[i] = new Edge(null, l3d);
                }
                if (path.IsClosed)
                {
                    edges[edges.Length - 1] = edges[0];
                }
                else
                {
                    Line l3d = Line.Construct();
                    l3d.SetTwoPoints(path.Curve(path.CurveCount - 1).EndPoint, path.Curve(path.CurveCount - 1).EndPoint + extension);
                    edges[edges.Length - 1] = new Edge(null, l3d);
                }
                Shell shell = Shell.Construct();
                List<Face> faces = new List<Face>(path.CurveCount);
                for (int i = 0; i < path.CurveCount; ++i)
                {
                    Face face = ExtrudeCurveToFace(path.Curve(i), extension, null, edges[i + 1], null, edges[i], true, true, true, false);
                    if (face != null) faces.Add(face);
                    else
                    {
                        edges[i + 1] = edges[i]; // die beiden edges fallen zusammen, da kein Face erzeugt wurde
                    }
                }
                shell.SetFaces(faces.ToArray());
                if (project != null) project.SetDefaults(shell);
                return shell;
            }
            else if (faceShellPathCurve is Ellipse && !(faceShellPathCurve as Ellipse).IsArc)
            {
                Ellipse elli = faceShellPathCurve as Ellipse;
                return MakeCylinderShell(elli.Center, elli.MajorAxis, extension); // geht nur wenn senkrecht, oder?
            }
            else if (faceShellPathCurve is ICurve)
            {   // eine Kurve, die kein Pfad ist
                Face face = ExtrudeCurveToFace(faceShellPathCurve as ICurve, extension, null, null, null, null, true, true, true, true);
                if (project != null) project.SetDefaults(face);
                return face;
            }
            else if (faceShellPathCurve is Face face)
            {
                Shell shell = MakeBrutePrism(face, extension);
                if (shell != null)
                {
                    if (shell.OpenEdges.Length == 0)
                    {
                        Solid sld = Solid.MakeSolid(shell);
                        if (project != null) project.SetDefaults(sld);
                        return sld;
                    }
                    else
                    {
                        if (project != null) project.SetDefaults(shell);
                        return shell;
                    }
                }
            }
            return null;
        }

        static public IGeoObject Rotate(IGeoObject faceShellPathCurve, Axis axis, SweepAngle rotation, SweepAngle offset, Project project)
        {
            Face originalFace = faceShellPathCurve as Face;
            if (faceShellPathCurve is Face)
            {
                // erstmal nur outline, weiteres später

                Path pth = Path.Construct();
                ICurve[] crvs = new ICurve[(faceShellPathCurve as Face).OutlineEdges.Length];
                for (int i = 0; i < (faceShellPathCurve as Face).OutlineEdges.Length; i++)
                {
                    crvs[i] = (faceShellPathCurve as Face).OutlineEdges[i].Curve3D;
                    if (!(faceShellPathCurve as Face).OutlineEdges[i].Forward(faceShellPathCurve as Face)) crvs[i].Reverse();
                }
                pth.Set(crvs);
                faceShellPathCurve = pth;
            }
            if (faceShellPathCurve is Ellipse && !(faceShellPathCurve as Ellipse).IsArc)
            {
                Path pth = Path.Construct();
                pth.Set((faceShellPathCurve as ICurve).Split(0.5));
                faceShellPathCurve = pth;
            }
            else if (faceShellPathCurve is ICurve && !(faceShellPathCurve is Path))
            {
                if ((faceShellPathCurve as ICurve).IsClosed)
                {
                    Path pth = Path.Construct();
                    pth.Set((faceShellPathCurve as ICurve).Split(0.5));
                    faceShellPathCurve = pth;
                }
                else
                {
                    Path pth = Path.Construct();
                    pth.Add((faceShellPathCurve as ICurve));
                    faceShellPathCurve = pth;
                }
            }
            if (faceShellPathCurve is Path || faceShellPathCurve is Polyline)
            {   // Pfad vor der Kurve abhandeln, denn Path ist auch ICurve
                Path path;
                if (faceShellPathCurve is Polyline)
                {
                    path = Path.Construct();
                    path.Add(faceShellPathCurve as ICurve);
                }
                else if (faceShellPathCurve is Ellipse) // Vollkreis
                {
                    path = Path.Construct();
                    ICurve cv = faceShellPathCurve as ICurve;
                    path.Set(cv.Split(0.5));
                }
                else
                {
                    path = faceShellPathCurve.Clone() as Path;
                }
                path.Flatten(); // Flatten wirft zu kurze segmente hoffentlich raus
                bool fullRotation = rotation.IsCloseTo(Math.PI * 2.0);
                if (originalFace == null)
                {
                    if (path.GetPlanarState() == PlanarState.Planar && path.IsClosed)
                    {
                        Plane pln = path.GetPlane();
                        ICurve2D crv2d = path.GetProjectedCurve(pln);
                        double a = crv2d.GetArea();
                        if (a < 0)
                        {
                            (path as ICurve).Reverse();
                            crv2d = path.GetProjectedCurve(pln);
                            a = crv2d.GetArea();
                        }
                        Border bdr = new Border(crv2d);
                        originalFace = Face.MakeFace(new PlaneSurface(pln), new SimpleShape(bdr));
                    }
                }

                if (originalFace == null) return null;

                Edge[][] sideEdges;
                SweepAngle[] sweepAngles;
                SweepAngle[] offsetAngles;
                List<Face> faces = new List<Face>(path.CurveCount + 2);
                List<Edge> allSideEdges = new List<Edge>();
                if (!fullRotation)
                {
                    Face side1 = originalFace.Clone() as Face;
                    Face side2 = originalFace.Clone() as Face;
                    side1.Modify(ModOp.Rotate(axis.Location, axis.Direction, offset + rotation));
                    side2.Modify(ModOp.Rotate(axis.Location, axis.Direction, offset));
                    faces.Add(side1);
                    faces.Add(side2);
                    sideEdges = new Edge[][] { side1.AllEdges, side2.AllEdges };
                    allSideEdges.AddRange(side1.AllEdges);
                    allSideEdges.AddRange(side2.AllEdges);
                    sweepAngles = new SweepAngle[] { rotation };
                    offsetAngles = new SweepAngle[] { offset };
                }
                else
                {   // full rotation: no side faces, but rotation split into two parts
                    Face side1 = originalFace.Clone() as Face;
                    Face side2 = originalFace.Clone() as Face;
                    side1.Modify(ModOp.Rotate(axis.Location, axis.Direction, offset + Math.PI));
                    side2.Modify(ModOp.Rotate(axis.Location, axis.Direction, offset));
                    sideEdges = new Edge[][] { side1.AllEdges, side2.AllEdges, side1.AllEdges };
                    allSideEdges.AddRange(side1.AllEdges);
                    allSideEdges.AddRange(side2.AllEdges);
                    side1.DisconnectAllEdges();
                    side2.DisconnectAllEdges();
                    sweepAngles = new SweepAngle[] { Math.PI, Math.PI };
                    offsetAngles = new SweepAngle[] { offset, offset + Math.PI };
                }
                List<Vertex> allvertices = Edge.RecalcVertices(allSideEdges);
                for (int k = 0; k < sweepAngles.Length; k++)
                {   // one or two passes, depending on full rotation or not
                    Edge[] s1 = sideEdges[k];
                    Edge[] s2 = sideEdges[k + 1]; // rotation goes from s2 edges to s1 edges
                    Edge[] edges = new Edge[path.CurveCount + 1];

                    ModOp rot = ModOp.Rotate(axis.Location, axis.Direction, sweepAngles[k]);
                    ModOp roffset = ModOp.Rotate(axis.Location, axis.Direction, offsetAngles[k]);
                    for (int i = 0; i < path.CurveCount; ++i)
                    {
                        Ellipse a3d = Ellipse.Construct();
                        GeoPoint cnt = Geometry.DropPL(path.Curve(i).StartPoint, axis.Location, axis.Direction);
                        GeoPoint sp = roffset * path.Curve(i).StartPoint;
                        if (Precision.IsEqual(cnt, sp))
                        {   // a point on the axis doesn't create an edge, it is a pole or shrinks to some useless edge
                            edges[i] = new Edge(null, null); // a pole, edge will be completed later
                            edges[i].SetVertices(s1[i].Vertex1, s1[i].Vertex1);
                        }
                        else
                        {
                            GeoVector diry = axis.Direction ^ (sp - cnt);
                            Plane pln = new Plane(cnt, sp - cnt, diry);
                            GeoPoint startPoint = roffset * path.Curve(i).StartPoint;
                            GeoPoint endPoint = rot * startPoint;
                            a3d.SetArcPlaneCenterStartEndPoint(pln, GeoPoint2D.Origin, pln.Project(startPoint), pln.Project(endPoint), pln, rotation.Radian > 0);
                            edges[i] = new Edge(null, a3d);
                            edges[i].SetVertices(s2[i].Vertex1, s1[i].Vertex1);
                        }
                    }
                    if (path.IsClosed)
                    {
                        edges[edges.Length - 1] = edges[0];
                    }
                    else
                    {
                        Ellipse a3d = Ellipse.Construct();
                        GeoPoint cnt = Geometry.DropPL(path.Curve(path.CurveCount - 1).EndPoint, axis.Location, axis.Direction);
                        Plane pln = new Plane(cnt, axis.Direction);
                        GeoPoint2D startPoint = pln.Project(roffset * path.Curve(path.CurveCount - 1).EndPoint);
                        GeoPoint2D endPoint = pln.Project(rot * path.Curve(path.CurveCount - 1).EndPoint);
                        a3d.SetArcPlaneCenterStartEndPoint(pln, GeoPoint2D.Origin, startPoint, endPoint, pln, rotation.Radian > 0);
                        edges[edges.Length - 1] = new Edge(null, a3d);
                    }
#if DEBUG
                    DebuggerContainer dc = new DebuggerContainer();
                    for (int i = 0; i < s1.Length; i++)
                    {
                        if (s1[i].Curve3D != null) dc.Add(s1[i].Curve3D as IGeoObject, System.Drawing.Color.Red, i); // to here
                        if (s2[i].Curve3D != null) dc.Add(s2[i].Curve3D as IGeoObject, System.Drawing.Color.Blue, i); // from here
                    }
                    for (int i = 0; i < edges.Length; i++)
                    {
                        if (edges[i].Curve3D != null) dc.Add(edges[i].Curve3D as IGeoObject, System.Drawing.Color.Green, i); // connecting arcs
                    }
#endif
                    for (int i = 0; i < path.CurveCount; i++)
                    {
                        ISurface surface = null;
                        Plane pln;
                        if (path.Curve(i) is Line)
                        {
                            Line l = path.Curve(i) as Line;
                            if (Precision.SameDirection(l.StartDirection, axis.Direction, false))
                            {
                                // a line parallel to the axis: cylindrical surface
                                GeoVector dirx = l.StartPoint - Geometry.DropPL(l.StartPoint, axis.Location, axis.Direction);
                                if (dirx.Length > Precision.eps)
                                {
                                    GeoVector diry = axis.Direction ^ dirx;
                                    diry.Length = dirx.Length;
                                    surface = new CylindricalSurface(axis.Location, dirx, diry, axis.Direction);
                                }
                            }
                            else if (Precision.IsPerpendicular(axis.Direction, l.StartDirection, false))
                            {   // a line, perpendicular to the rotation axis makes a plane
                                if (edges[i] != null || edges[i + 1] != null)
                                {
                                    Plane pls;
                                    if (edges[i].Curve3D != null) pls = (edges[i].Curve3D as ICurve).GetPlane();
                                    else pls = (edges[i + 1].Curve3D as ICurve).GetPlane();
                                    surface = new PlaneSurface(pls);
                                }
                            }
                            else if (Geometry.CommonPlane(l.StartPoint, l.StartDirection, axis.Location, axis.Direction, out pln))
                            {
                                // a line which is not skew to the axis: conical surface
                                GeoVector dirx = (l.StartDirection ^ axis.Direction).Normalized;
                                GeoVector diry = (axis.Direction ^ dirx).Normalized;
                                GeoPoint apex = Geometry.IntersectLL(l.StartPoint, l.StartDirection, axis.Location, axis.Direction);
                                //Angle a = new Angle(l.StartDirection, axis.Direction);
                                //if (a > Math.PI / 2.0) a = Math.PI - a;
                                // surface = new ConicalSurface(apex, dirx, diry, axis.Direction, a, 0.0);
                                try
                                {
                                    ModOp m = ModOp.Fit(new GeoPoint[] { l.StartPoint, ModOp.Rotate(axis.Location, axis.Direction, Math.PI / 2.0) * l.StartPoint, ModOp.Rotate(axis.Location, axis.Direction, Math.PI) * l.StartPoint, apex }, new GeoPoint[] { new GeoPoint(1, 0, 1), new GeoPoint(0, 1, 1), new GeoPoint(-1, 0, 1), GeoPoint.Origin }, true);
                                    surface = new ConicalSurface(m.GetInverse());
                                }
                                catch (ModOpException)
                                {
                                    Angle a = new Angle(l.StartDirection, axis.Direction);
                                    if (a > Math.PI / 2.0) a = Math.PI - a;
                                    surface = new ConicalSurface(apex, dirx, diry, axis.Direction.Normalized, a, 0.0);
                                }
                            }
                            else
                            {
                                surface = new SurfaceOfRevolution(l, axis.Location, axis.Direction);
                            }
                        }
                        else if (path.Curve(i) is Ellipse && (path.Curve(i) as Ellipse).IsCircle)
                        {
                            Ellipse e = (path.Curve(i) as Ellipse);
                            if (e.Plane.Distance(axis.Location) < Precision.eps && Precision.IsPerpendicular(e.Plane.Normal, axis.Direction, false))
                            {
                                if (Geometry.DistPL(e.Center, axis) < Precision.eps)
                                {   // a sphere
                                    GeoVector dirx = (e.StartPoint - e.Center).Normalized;
                                    GeoVector diry = (axis.Direction ^ dirx).Normalized;
                                    dirx = (diry ^ axis.Direction).Normalized;
                                    surface = new SphericalSurface(e.Center, e.Radius * dirx, e.Radius * diry, e.Radius * axis.Direction.Normalized);
                                }
                                else
                                // circle in the same plane as axis: toriodal surface
                                {
                                    GeoVector dirx = ((axis.Location - e.Center) ^ axis.Direction).Normalized;
                                    GeoVector diry = (axis.Direction ^ dirx).Normalized;
                                    GeoPoint cnt = Geometry.DropPL(e.Center, axis.Location, axis.Direction);
                                    surface = new ToroidalSurface(cnt, dirx, diry, axis.Direction.Normalized, e.Center | cnt, e.Radius);
                                }
                            }
                            else
                            {
                                try
                                {
                                    surface = new SurfaceOfRevolution(e, axis.Location, axis.Direction);
                                }
                                catch (SurfaceOfRevolutionException) { } // e.g. a Line identical with the axis
                            }
                        }
                        else
                        {
                            try
                            {
                                surface = new SurfaceOfRevolution(path.Curve(i), axis.Location, axis.Direction);
                            }
                            catch (SurfaceOfRevolutionException) { } // e.g. a Line identical with the axis
                        }
                        if (surface != null)
                        {
                            // in all rotated surfaces (CylindricalSurface, ConicalSurface, SphericalSurface, ToroidalSurface, SurfaceOfRevolution) the u parameter
                            // goes along with our rotation, which is from offset to 0 to sweepangle
                            // only the plane has independant parameters.
                            // If there is a pole in a rotated surface, we must make a 2d horizontal line as the 2d curve. s1[i] should have u==0, s2[i] should have u==sweepAngle
                            // so we can construct the 2d lines here
                            // if we have rotated surface, edges[i] and edges[i+1] will be horizontal lines in 2d of the surface
                            // if we have a plane, edges[i] and edges[i+1] will be arcs
                            ICurve2D s12d = surface.GetProjectedCurve(s1[i].Curve3D, 0.0);
                            ICurve2D s22d = surface.GetProjectedCurve(s2[i].Curve3D, 0.0);
                            Face fc = Face.Construct(); // empty face to be filled with data
                            s1[i].SetFace(fc, s12d, true);
                            s2[i].SetFace(fc, s22d, true);
                            ICurve2D e02d, e12d; // 2d curves of edges[i], edges[i+1]
                            if (surface is PlaneSurface)
                            {
                                // the 2 edges of the original face or path beeing roteted plus two arcs build the face
                                // there might be one of the arcs missing, when it is a pole
                                if (edges[i].Curve3D != null) e02d = surface.GetProjectedCurve(edges[i].Curve3D, 0.0);
                                else e02d = null;
                                if (edges[i + 1].Curve3D != null) e12d = surface.GetProjectedCurve(edges[i + 1].Curve3D, 0.0);
                                else e12d = null;
                                if (e02d != null && e12d != null)
                                {   // normal case, all 4 edges are defined
                                    edges[i].SetFace(fc, e02d, true);
                                    edges[i + 1].SetFace(fc, e12d, true);
                                    edges[i + 1].Reverse(fc); // one of the arcs must be reversed
                                    s2[i].Reverse(fc);
                                    // now the 2d curves should be connected, but not sure ccw
                                    double area = Border.SignedArea(new ICurve2D[] { s2[i].Curve2D(fc), edges[i].Curve2D(fc), s1[i].Curve2D(fc), edges[i + 1].Curve2D(fc) });
                                    if (area < 0)
                                    {   // reverse all 2d curves
                                        s1[i].Reverse(fc);
                                        s2[i].Reverse(fc);
                                        edges[i].Reverse(fc);
                                        edges[i + 1].Reverse(fc);
                                        fc.Set(surface, new Edge[] { s1[i], edges[i], s2[i], edges[i + 1] }, null, false);
                                    }
                                    else
                                    {
                                        fc.Set(surface, new Edge[] { s2[i], edges[i], s1[i], edges[i + 1] }, null, false);
                                    }
                                }
                                else if (e02d != null)
                                {   // edges[i+1] is a pole
                                    edges[i].SetFace(fc, e02d, true);
                                    s2[i].Reverse(fc);
                                    // now the 2d curves should be connected, but not sure ccw
                                    double area = Border.SignedArea(new ICurve2D[] { s1[i].Curve2D(fc), edges[i].Curve2D(fc), s2[i].Curve2D(fc) });
                                    if (area < 0)
                                    {   // reverse all 2d curves
                                        s1[i].Reverse(fc);
                                        s2[i].Reverse(fc);
                                        edges[i].Reverse(fc);
                                        fc.Set(surface, new Edge[] { s1[i], edges[i], s2[i] }, null, false);
                                    }
                                    else
                                    {
                                        fc.Set(surface, new Edge[] { s2[i], edges[i], s1[i] }, null, false);
                                    }
                                }
                                else if (e12d != null)
                                {   //edges[i] is a pole
                                    edges[i + 1].SetFace(fc, e12d, true);
                                    edges[i + 1].Reverse(fc); // one of the arcs must be reversed
                                    s2[i].Reverse(fc);
                                    // now the 2d curves should be connected, but not sure ccw
                                    double area = Border.SignedArea(new ICurve2D[] { s1[i].Curve2D(fc), s2[i].Curve2D(fc), edges[i + 1].Curve2D(fc) });
                                    if (area < 0)
                                    {   // reverse all 2d curves
                                        s1[i].Reverse(fc);
                                        s2[i].Reverse(fc);
                                        edges[i + 1].Reverse(fc);
                                        fc.Set(surface, new Edge[] { s1[i], s2[i], edges[i + 1] }, null, false);
                                    }
                                    else
                                    {
                                        fc.Set(surface, new Edge[] { s2[i], s1[i], edges[i + 1] }, null, false);
                                    }
                                }
                                else
                                {
                                    fc = null;
                                }
                            }
                            else
                            {
                                // a rotated surface: the u-parameter goes from 0 to sweepAngle
                                GeoPoint2D uv1 = surface.PositionOf(s2[i].Curve3D.StartPoint); // from here
                                GeoPoint2D uv2 = surface.PositionOf(s1[i].Curve3D.StartPoint); // to here
                                if (uv2.x < uv1.x) uv2.x += Math.PI * 2.0;
                                BoundingRect domain = BoundingRect.EmptyBoundingRect;
                                domain.MinMax(s12d.GetExtent());
                                domain.MinMax(s22d.GetExtent());
                                domain.Left = uv1.x;
                                domain.Right = uv2.x;
                                SurfaceHelper.AdjustPeriodic(surface, domain, s12d);
                                SurfaceHelper.AdjustPeriodic(surface, domain, s22d);
                                SurfaceHelper.AdjustPeriodic(surface, domain, ref uv1);
                                SurfaceHelper.AdjustPeriodic(surface, domain, ref uv2);
                                e02d = new Line2D(uv1, uv2);
                                uv1 = surface.PositionOf(s2[i].Curve3D.EndPoint);
                                uv2 = surface.PositionOf(s1[i].Curve3D.EndPoint);
                                SurfaceHelper.AdjustPeriodic(surface, domain, ref uv1);
                                SurfaceHelper.AdjustPeriodic(surface, domain, ref uv2);
                                e12d = new Line2D(uv1, uv2);
                                edges[i].SetFace(fc, e02d, true);
                                edges[i + 1].SetFace(fc, e12d, true);

                                s2[i].Reverse(fc);
                                edges[i + 1].Reverse(fc);
                                // now the 2d curves should be connected, but not sure ccw
                                double area = Border.SignedArea(new ICurve2D[] { s2[i].Curve2D(fc), edges[i].Curve2D(fc), s1[i].Curve2D(fc), edges[i + 1].Curve2D(fc) });
                                if (area < 0)
                                {   // reverse all 2d curves
                                    s1[i].Reverse(fc);
                                    s2[i].Reverse(fc);
                                    edges[i].Reverse(fc);
                                    edges[i + 1].Reverse(fc);
                                    fc.Set(surface, new Edge[] { s1[i], edges[i], s2[i], edges[i + 1] }, null, false);
                                }
                                else
                                {
                                    fc.Set(surface, new Edge[] { s2[i], edges[i], s1[i], edges[i + 1] }, null, false);
                                }
                            }
                            if (fc != null) faces.Add(fc.Clone() as Face); // erstmal clone, damit die edges unabhängig sind. Sonst gibts bei sewfaces einen fehler. Allerdings muss SewFaces gefixt werden
#if DEBUG
                            bool ccok = faces[faces.Count - 1].CheckConsistency();
                            faces[faces.Count - 1].GetTriangulation(0.01, out GeoPoint[] tp, out GeoPoint2D[] uv, out int[] ind, out BoundingCube bc);
                            DebuggerContainer dc3 = faces[faces.Count - 1].DebugTriangulation3D;
                            DebuggerContainer dc1 = faces[faces.Count - 1].DebugTriangulation;
#endif
                        }
                    }
                }
                Shell[] shells = SewFaces(faces.ToArray());
                if (shells.Length == 1)
                {
                    if (shells[0].HasOpenEdgesExceptPoles()) return shells[0];
                    else return Solid.MakeSolid(shells[0]);
                }
            }
            return null;
        }
        static public Solid[] ImportSTL(string fileName)
        {
            using (BinaryReader reader = new BinaryReader(File.Open(fileName, FileMode.Open)))
            {
                byte[] head = reader.ReadBytes(5);
                if (head[0] == (byte)('s')
                    && head[1] == (byte)('o')
                    && head[2] == (byte)('l')
                    && head[3] == (byte)('i')
                    && head[4] == (byte)('d'))
                {
                    // is ASCII
                }
                else
                {
                    reader.ReadBytes(75);
                    int numTriangles = reader.ReadInt32();
                    for (int i = 0; i < numTriangles; i++)
                    {
                        GeoVector normal = new GeoVector(reader.ReadSingle(), reader.ReadSingle(), reader.ReadSingle());
                        GeoPoint p1 = new GeoPoint(reader.ReadSingle(), reader.ReadSingle(), reader.ReadSingle());
                        GeoPoint p2 = new GeoPoint(reader.ReadSingle(), reader.ReadSingle(), reader.ReadSingle());
                        GeoPoint p3 = new GeoPoint(reader.ReadSingle(), reader.ReadSingle(), reader.ReadSingle());
                        short attribute = reader.ReadInt16();
                    }
                }
            }
            throw new NotImplementedException();
        }
        static public string GetDtkFileType(string filename)
        {
            return "error: dtk is not installed";
        }

        static public IGeoObject[] ImportDtk(string filename, Project pr, bool useProgress = false, bool makeCompounds = false)
        {
            throw new NotImplementedException();
        }
        static public Shell Curtail(Path path, Shell shell)
        {
            path.Flatten();
            GeoPoint[] vert = path.Vertices; // path wird u.U. geändert, hier also die Punkte merken
            Plane pln;
            if (path.GetPlanarState() == PlanarState.Planar)
            {
                pln = path.GetPlane();
            }
            else
            {
                double maxdist;
                bool islinear;
                pln = Plane.FromPoints(vert, out maxdist, out islinear);
                ICurve2D c2d = path.GetProjectedCurve(pln);
                path = c2d.MakeGeoObject(pln) as Path; // jetzt sicher eben
                if (path == null) return null;
            }
            BoundingCube ext = shell.GetExtent(0.0);
            ModOp moveback = ModOp.Translate(-ext.Size * pln.Normal);
            path.Modify(moveback);
            Solid sld = MakePrism(path, ext.Size * 2 * pln.Normal, null, false) as Solid;
            if (sld == null) return null;
            Shell[] res = Intersection(shell, sld);
            if (res == null || res.Length == 0) return null;
            if (res.Length == 1) return res[0];
            // es gibt mehrere Shells, hier das suchen, welches am nächsten zum ursprünglichen Pfad liegt
            int bestind = -1;
            double mindist = double.MaxValue;
            for (int i = 0; i < res.Length; i++)
            {
                double d = MinDist(res[i].OpenEdges, vert);
                if (d < mindist)
                {
                    mindist = d;
                    bestind = i;
                }
            }
            if (bestind >= 0) return res[bestind];
            return null;
        }
#if DEBUG
        static public Shell[] RemoveBoundaryOffset(Shell shell, double dist)
        {
            throw new NotImplementedException();
        }
#endif
        private static double MinDist(Edge[] edge, GeoPoint[] vert)
        {
            double res = double.MaxValue;
            for (int i = 0; i < edge.Length; i++)
            {
                for (int j = 0; j < vert.Length; j++)
                {
                    if (edge[i].Curve3D != null)
                    {
                        double d = edge[i].Curve3D.StartPoint | vert[j];
                        if (d < res) res = d;
                    }
                }
            }
            return res;
        }
        private static bool STLGetNextTriangle(TextReader tr, out GeoPoint p1, out GeoPoint p2, out GeoPoint p3)
        {
            string line;
            p1 = p2 = p3 = GeoPoint.Origin; // für den compiler
            while ((line = tr.ReadLine()) != null)
            {
                string[] splitted = line.Split(new char[] { ' ', '\t' }, StringSplitOptions.RemoveEmptyEntries);
                if (splitted.Length > 0)
                {
                    if (splitted[0].ToLower() == "facet")
                    {
                        break;
                    }
                    if (splitted[0].ToLower() == "endsolid")
                    {
                        return false; // nächstes solid
                    }
                }
            }
            if (line == null) return false; // zu ende
            int vertexnum = 0;
            while ((line = tr.ReadLine()) != null)
            {
                string[] splitted = line.Split(new char[] { ' ', '\t' }, StringSplitOptions.RemoveEmptyEntries);
                if (splitted.Length > 3)
                {
                    if (splitted[0].ToLower() == "vertex")
                    {
                        switch (vertexnum)
                        {
                            case 0:
                                p1 = new GeoPoint(double.Parse(splitted[1], System.Globalization.NumberFormatInfo.InvariantInfo), double.Parse(splitted[2], System.Globalization.NumberFormatInfo.InvariantInfo), double.Parse(splitted[3], System.Globalization.NumberFormatInfo.InvariantInfo));
                                break;
                            case 1:
                                p2 = new GeoPoint(double.Parse(splitted[1], System.Globalization.NumberFormatInfo.InvariantInfo), double.Parse(splitted[2], System.Globalization.NumberFormatInfo.InvariantInfo), double.Parse(splitted[3], System.Globalization.NumberFormatInfo.InvariantInfo));
                                break;
                            case 2:
                                p3 = new GeoPoint(double.Parse(splitted[1], System.Globalization.NumberFormatInfo.InvariantInfo), double.Parse(splitted[2], System.Globalization.NumberFormatInfo.InvariantInfo), double.Parse(splitted[3], System.Globalization.NumberFormatInfo.InvariantInfo));
                                break;
                        }
                        ++vertexnum;
                        if (vertexnum == 3) return true;
                    }
                }
            }
            return false;
        }
        private static string STLGetNextSolid(TextReader tr)
        {
            string line;
            while ((line = tr.ReadLine()) != null)
            {
                string[] splitted = line.Split(new char[] { ' ', '\t' }, StringSplitOptions.RemoveEmptyEntries);
                if (splitted.Length > 1)
                {
                    if (splitted[0].ToLower() == "solid")
                    {
                        return splitted[1];
                    }
                }
                if (splitted.Length == 1)
                {
                    return "noname";
                }
            }
            return null;
        }
        //static public Solid[] SplitSolid(Solid s, Plane pln)
        //{
        //    return null;

        //}

        /// <summary>
        /// Checks whether the two provided solids have overlapping faces. If this is the case, one of the
        /// two faces or both faces are split in a way that the modified solids have common, identical
        /// faces which have the same outlines
        /// </summary>
        /// <param name="s1">The first solid</param>
        /// <param name="s2">The second solid</param>
        /// <param name="splittedOnS1">Splitted faces on s1</param>
        /// <param name="splittedOnS2">Splitted faces on s2</param>
        /// <returns>Returns true if common overlapping faces were found.</returns>
        ////static public bool SplitCommonFace(Solid s1, Solid s2, out Face[] splittedOnS1, out Face[] splittedOnS2)
        ////{
        ////    List<Face> sps1 = new List<Face>();
        ////    List<Face> sps2 = new List<Face>();
        ////    BRepOperationOld bro = new BRepOperationOld(s1.Shells[0], s2.Shells[0], BRepOperationOld.Operation.commonface);
        ////    Face[] onShell1;
        ////    Face[] onShell2;
        ////    ModOp2D[] firstToSecond;
        ////    bro.GetOverlappingFaces(out onShell1, out onShell2, out firstToSecond);
        ////    bool splittingDone = true; // bei Überlappung zweier Faces mit mehr als einer gemeinsamen Fläche
        ////                               // wird zunächst nur eine Überlappung gerechnet, die Methode muss nochmal aufgerufen werden
        ////    for (int i = 0; i < onShell1.Length; i++)
        ////    {
        ////        SimpleShape ss1 = onShell1[i].Area;
        ////        SimpleShape ss2 = onShell2[i].Area.GetModified(firstToSecond[i].GetInverse());
        ////        SimpleShape.Position pos = SimpleShape.GetPosition(ss1, ss2);
        ////        switch (pos)
        ////        {
        ////            case SimpleShape.Position.identical:
        ////                {
        ////                }
        ////                break;
        ////            case SimpleShape.Position.intersecting:
        ////                {
        ////                    CompoundShape cs = SimpleShape.Intersect(ss1, ss2);
        ////                    if (!cs.Empty)
        ////                    {   // das sollte laut Lukasz nicht vorkommen
        ////                        // im Allgemeinen kommt es aber schon vor, in den Beispielen von Lukasz auch!
        ////                        CompoundShape csi = SimpleShape.Intersect(ss1, ss2);
        ////                        if (csi.SimpleShapes.Length > 0 && csi.SimpleShapes[0].Area > Precision.eps)
        ////                        {
        ////                            sps1.AddRange(onShell1[i].SplitAndReplace(csi.SimpleShapes[0]));
        ////                            sps2.AddRange(onShell2[i].SplitAndReplace(csi.SimpleShapes[0].GetModified(firstToSecond[i])));
        ////                        }
        ////                        if (csi.SimpleShapes.Length > 1) splittingDone = false;
        ////                    }
        ////                }
        ////                break;
        ////            case SimpleShape.Position.firstcontainscecond:
        ////                {
        ////                    sps1.AddRange(onShell1[i].SplitAndReplace(ss2));
        ////                }
        ////                break;
        ////            case SimpleShape.Position.secondcontainsfirst:
        ////                {
        ////                    sps2.AddRange(onShell2[i].SplitAndReplace(ss1.GetModified(firstToSecond[i].GetInverse())));
        ////                }
        ////                break;
        ////        }
        ////    }
        ////    splittedOnS1 = sps1.ToArray();
        ////    splittedOnS2 = sps2.ToArray();
        ////    return splittingDone;
        ////}
        /// <summary>
        /// Copies all Userdata from Faces in <paramref name="copyFrom"/ to Faces in <paramref name="addTo"/>
        /// when the faces shaer common parts, i.e. are on the same surface
        /// </summary>
        /// <param name="copyFrom"></param>
        /// <param name="addTo"></param>
        public static void CloneFaceUserData(Solid[] copyFrom, Solid[] addTo)
        {
            OctTree<Face> original = new OctTree<Face>(copyFrom[0].GetExtent(Precision.eps), Precision.eps);
            for (int i = 0; i < copyFrom.Length; i++)
            {
                for (int j = 0; j < copyFrom[i].Shells.Length; j++)
                {
                    for (int k = 0; k < copyFrom[i].Shells[j].Faces.Length; k++)
                    {
                        original.AddObject(copyFrom[i].Shells[j].Faces[k]);
                    }
                }
            }
            for (int i = 0; i < addTo.Length; i++)
            {
                for (int j = 0; j < addTo[i].Shells.Length; j++)
                {
                    for (int k = 0; k < addTo[i].Shells[j].Faces.Length; k++)
                    {
                        Face fc = addTo[i].Shells[j].Faces[k];
                        Face[] closeFaces = original.GetObjectsCloseTo(fc);
                        for (int l = 0; l < closeFaces.Length; l++)
                        {
                            ModOp2D firstToSecond;
                            if (closeFaces[l].Surface.SameGeometry(closeFaces[l].GetUVBounds(), fc.Surface, fc.GetUVBounds(), Precision.eps, out firstToSecond))
                            {
                                SimpleShape ss = closeFaces[l].Area.GetModified(firstToSecond);

                                if (SimpleShape.GetPosition(ss, fc.Area) != SimpleShape.Position.disjunct)
                                {
                                    fc.UserData.CloneFrom(closeFaces[l].UserData);
                                }
                            }
                        }
                    }
                }
            }

        }
    }
}
