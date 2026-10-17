using System;
using System.Collections.Generic;
using MathNet.Numerics;
using System.Text.RegularExpressions;
using System.Xml;
using CADability;
using CADability.GeoObject;
using System.Linq;
using CADability.Curve2D;
using System.Runtime.InteropServices.ComTypes;
using CADability.Attribute;
using MathNet.Numerics.LinearAlgebra.Factorization;
using static CADability.PrintToGDI;
using System.Numerics;

namespace CADability
{
    /// <summary>
    /// Reads simple SVG files and converts their elements (lines, rectangles, circles, ellipses, polylines, polygons
    /// and paths with Bézier and elliptical arc segments) into <see cref="IGeoObject"/>s. The transformations of nested
    /// elements are accumulated on a stack of <see cref="ModOp2D"/>s; the virtual CreateXxx methods may be overridden
    /// in a derived class to customize the objects that are created. Use <see cref="Import(string)"/> to import a file.
    /// </summary>
    public class ImportSVG
    {
        /// <summary>
        /// A 2d point or vector in SVG user coordinates (single precision), as passed to the path related CreateXxx methods.
        /// Addition and subtraction are provided for the relative path commands.
        /// </summary>
        protected struct Vector2
        {
            /// <summary>
            /// Creates the vector with the given components.
            /// </summary>
            /// <param name="x">the x component</param>
            /// <param name="y">the y component</param>
            public Vector2(float x, float y)
            {
                this.x = x;
                this.y = y;
            }
            /// <summary>
            /// The x and y coordinates in SVG user units.
            /// </summary>
            public float x, y;
            /// <summary>
            /// Adds the components; used to turn the relative coordinates of a path command into absolute ones.
            /// </summary>
            /// <param name="v1">first summand</param>
            /// <param name="v2">second summand</param>
            /// <returns>the component-wise sum</returns>
            public static Vector2 operator +(Vector2 v1, Vector2 v2)
            {
                return new Vector2(v1.x + v2.x, v1.y + v2.y);
            }
            /// <summary>
            /// Subtracts the components of <paramref name="v2"/> from <paramref name="v1"/>.
            /// </summary>
            /// <param name="v1">the minuend</param>
            /// <param name="v2">the subtrahend</param>
            /// <returns>the component-wise difference</returns>
            public static Vector2 operator -(Vector2 v1, Vector2 v2)
            {
                return new Vector2(v1.x - v2.x, v1.y - v2.y);
            }
            /// <inheritdoc/>
            public override bool Equals(object obj)
            {
                if (obj is Vector2 v2) return x == v2.x && y == v2.y;
                else return base.Equals(obj);
            }
            /// <inheritdoc/>
            public override int GetHashCode()
            {
                return x.GetHashCode() | y.GetHashCode();
            }
        }
        private readonly Stack<ModOp2D> _transformStack;
        /// <summary>
        /// The lists the created objects are collected in. The bottom entry collects the result of <see cref="Import(string)"/>;
        /// <see cref="CreatePath"/> pushes a list for each sub-path and pops it when the sub-path is complete, so the CreateXxx
        /// methods always add to the list on top (<c>listStack.Peek()</c>). Overriding methods should do the same.
        /// </summary>
        public Stack<GeoObjectList> listStack;
        Dictionary<string, string> styles; // current element styles
        Dictionary<string, ColorDef> FillStyles = new Dictionary<string, ColorDef>();
        /// <summary>
        /// Creates an importer with the identity transformation and an empty result list. Call <see cref="Import(string)"/> to
        /// read a file.
        /// </summary>
        public ImportSVG()
        {
            _transformStack = new Stack<ModOp2D>();
            _transformStack.Push(ModOp2D.Identity);
            listStack = new Stack<GeoObjectList>();
            listStack.Push(new GeoObjectList());
        }

        /// <summary>
        /// Imports the SVG file and calls the corresponding CreateXxx method for each element found. The result is
        /// mirrored at a horizontal axis through its center, because the y axis of SVG points downwards.
        /// </summary>
        /// <param name="fileName">path of the SVG file</param>
        /// <returns>the imported objects, or null if the file could not be read</returns>
        public GeoObjectList Import(string fileName)
        {
            try
            {
                using (XmlReader reader = XmlReader.Create(fileName))
                {
                    while (reader.Read())
                    {
                        if (reader.NodeType == XmlNodeType.Element)
                        {
                            ImportElement(reader);
                        }
                    }
                }
                GeoObjectList result = listStack.Pop();
                BoundingCube ext = result.GetExtent();
                ModOp reflect = ModOp.ReflectPlane(new Plane(new GeoPoint(0, (ext.Ymax + ext.Ymin) / 2.0, 0), GeoVector.YAxis));
                result.Modify(reflect);
                return result;
            }
            catch (Exception ex)
            {
                // TODO: Fehlerbehandlung erweitern (Logging etc.)
                Console.Error.WriteLine(ex.Message);
                return null;
            }
        }

        private void ImportElement(XmlReader reader)
        {
            if (reader.NodeType != XmlNodeType.Element)
                return;

            bool isEmpty = reader.IsEmptyElement;
            string transformAttr = reader.GetAttribute("transform");
            if (!string.IsNullOrEmpty(transformAttr))
            {
                ModOp2D t = ParseTransform(transformAttr);
                ModOp2D current = _transformStack.Peek();
                _transformStack.Push(t * current);
            }
            styles = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            string styleAttr = reader.GetAttribute("style");
            if (!string.IsNullOrEmpty(styleAttr))
            {
                var declarations = styleAttr.Split(';');
                foreach (var decl in declarations)
                {
                    var kv = decl.Split(new[] { ':' }, 2);
                    if (kv.Length == 2)
                    {
                        var name = kv[0].Trim();
                        var value = kv[1].Trim();
                        if (name.Length > 0)
                            styles[name] = value;
                    }
                }
            }

            // Aktuelle Transformationsmatrix
            ModOp2D currentTransform = _transformStack.Peek();

            // Gruppeneinstieg
            if (reader.Name.Equals("g", StringComparison.OrdinalIgnoreCase))
            {
                EnterGroup(currentTransform, styles);
            }
            // Element-Typ prüfen und Aufruf generieren
            switch (reader.Name)
            {
                case "line":
                    float x1 = ParseFloat(reader.GetAttribute("x1"));
                    float y1 = ParseFloat(reader.GetAttribute("y1"));
                    float x2 = ParseFloat(reader.GetAttribute("x2"));
                    float y2 = ParseFloat(reader.GetAttribute("y2"));
                    CreateLine(x1, y1, x2, y2, currentTransform);
                    break;

                case "rect":
                    float x = ParseFloat(reader.GetAttribute("x"));
                    float y = ParseFloat(reader.GetAttribute("y"));
                    float width = ParseFloat(reader.GetAttribute("width"));
                    float height = ParseFloat(reader.GetAttribute("height"));
                    CreateRect(x, y, width, height, currentTransform);
                    break;

                case "circle":
                    float cx = ParseFloat(reader.GetAttribute("cx"));
                    float cy = ParseFloat(reader.GetAttribute("cy"));
                    float r = ParseFloat(reader.GetAttribute("r"));
                    CreateCircle(cx, cy, r, currentTransform);
                    break;

                case "ellipse":
                    float ecx = ParseFloat(reader.GetAttribute("cx"));
                    float ecy = ParseFloat(reader.GetAttribute("cy"));
                    float rx = ParseFloat(reader.GetAttribute("rx"));
                    float ry = ParseFloat(reader.GetAttribute("ry"));
                    CreateEllipse(ecx, ecy, rx, ry, currentTransform);
                    break;

                case "polyline":
                    string pointsPL = reader.GetAttribute("points");
                    CreatePolyline(pointsPL, currentTransform);
                    break;

                case "polygon":
                    string pointsPG = reader.GetAttribute("points");
                    CreatePolygon(pointsPG, currentTransform);
                    break;

                case "path":
                    string d = reader.GetAttribute("d");
                    CreatePath(d, currentTransform);
                    break;

                    // Weitere Elemente je nach Bedarf hinzufügen
            }

            // Falls keine Selbstschluss-Elemente, rekursiv Kinder verarbeiten
            if (!isEmpty)
            {
                while (reader.Read())
                {
                    if (reader.NodeType == XmlNodeType.Whitespace) continue;
                    if (reader.NodeType == XmlNodeType.Element)
                    {
                        ImportElement(reader);
                    }
                    else if (reader.NodeType == XmlNodeType.EndElement)
                    {
                        break;
                    }
                }
            }

            // Gruppenende
            if (reader.Name.Equals("g", StringComparison.OrdinalIgnoreCase))
            {
                ExitGroup();
            }
            // Nach Verlassen des Elements Transformationsmatrix zurücksetzen
            if (!string.IsNullOrEmpty(transformAttr))
            {
                _transformStack.Pop();
            }
        }
        #region Stubs für Gruppierung
        /// <summary>
        /// Called when a <c>g</c> element is entered, before its children are processed. Does nothing; override it to handle
        /// group properties, e.g. to start a <see cref="Block"/> that collects the children, and override <see cref="ExitGroup"/>
        /// to finish it.
        /// </summary>
        /// <param name="transform">the accumulated transformation of the group, including those of all enclosing elements</param>
        /// <param name="styles">the style declarations of the group element (name to value, case-insensitive names); may be empty</param>
        protected virtual void EnterGroup(ModOp2D transform, Dictionary<string, string> styles)
        {
            // TODO: Gruppeneigenschaften verarbeiten
        }

        /// <summary>
        /// Called when a <c>g</c> element has been processed completely, after <see cref="EnterGroup"/> and the children. Does
        /// nothing; override it together with <see cref="EnterGroup"/>.
        /// </summary>
        protected virtual void ExitGroup()
        {
            // TODO: Gruppenschluss verarbeiten
        }
        #endregion

        #region Stub-Methoden zum Überschreiben
        private void Add(ICurve2D curve, ModOp2D transform)
        {
            listStack.Peek().Add(curve.GetModified(transform).MakeGeoObject(Plane.XYPlane));
        }

        /// <summary>
        /// Called for a <c>line</c> element, for the straight segments of a path and for its closing segment (<c>Z</c>). Creates a
        /// <see cref="Line"/> in the x/y plane and adds it to the current list; override it to create something else.
        /// </summary>
        /// <param name="x1">x coordinate of the start point in SVG user units</param>
        /// <param name="y1">y coordinate of the start point</param>
        /// <param name="x2">x coordinate of the end point</param>
        /// <param name="y2">y coordinate of the end point</param>
        /// <param name="transform">the transformation from the local coordinates of the element to the coordinates of the drawing; it is applied to the curve before the curve is added</param>
        protected virtual void CreateLine(float x1, float y1, float x2, float y2, ModOp2D transform)
        {
            Line2D l2d = new Line2D(new GeoPoint2D(x1, y1), new GeoPoint2D(x2, y2));
            Add(l2d, transform);
        }

        /// <summary>
        /// Called for a <c>rect</c> element. Creates a closed <see cref="Polyline"/> through the four corners (rounded corners are
        /// ignored) and adds it to the current list; override it to create something else.
        /// </summary>
        /// <param name="x">x coordinate of the corner with the smallest coordinates, in SVG user units</param>
        /// <param name="y">y coordinate of that corner</param>
        /// <param name="width">extent in x</param>
        /// <param name="height">extent in y</param>
        /// <param name="transform">the transformation from the local coordinates of the element to the coordinates of the drawing; it is applied to the curve before the curve is added</param>
        protected virtual void CreateRect(float x, float y, float width, float height, ModOp2D transform)
        {
            Polyline2D p2d = new Polyline2D(new GeoPoint2D[] { new GeoPoint2D(x, y), new GeoPoint2D(x + width, y), new GeoPoint2D(x + width, y + height), new GeoPoint2D(x, y + height), new GeoPoint2D(x, y) });
            Add(p2d, transform);
        }

        /// <summary>
        /// Called for a <c>circle</c> element. Creates a full circle (an <see cref="Ellipse"/>) and adds it to the current list;
        /// override it to create something else.
        /// </summary>
        /// <param name="cx">x coordinate of the center in SVG user units</param>
        /// <param name="cy">y coordinate of the center</param>
        /// <param name="r">the radius</param>
        /// <param name="transform">the transformation from the local coordinates of the element to the coordinates of the drawing; it is applied to the curve before the curve is added</param>
        protected virtual void CreateCircle(float cx, float cy, float r, ModOp2D transform)
        {
            Circle2D c2d = new Circle2D(new GeoPoint2D(cx, cy), r);
            Add(c2d, transform);
        }

        /// <summary>
        /// Called for an <c>ellipse</c> element. Creates an axis-parallel <see cref="Ellipse"/> and adds it to the current list;
        /// override it to create something else.
        /// </summary>
        /// <param name="cx">x coordinate of the center in SVG user units</param>
        /// <param name="cy">y coordinate of the center</param>
        /// <param name="rx">the radius in x direction</param>
        /// <param name="ry">the radius in y direction</param>
        /// <param name="transform">the transformation from the local coordinates of the element to the coordinates of the drawing; it is applied to the curve before the curve is added</param>
        protected virtual void CreateEllipse(float cx, float cy, float rx, float ry, ModOp2D transform)
        {
            Ellipse2D e2d = new Ellipse2D(new GeoPoint2D(cx, cy), new GeoVector2D(rx, 0), new GeoVector2D(0, ry));
            Add(e2d, transform);
        }

        /// <summary>
        /// Called for a <c>polyline</c> element. Parses the <c>points</c> attribute (x/y pairs separated by commas or white space),
        /// creates an open <see cref="Polyline"/> and adds it to the current list; override it to create something else.
        /// </summary>
        /// <param name="points">the <c>points</c> attribute of the element</param>
        /// <param name="transform">the transformation from the local coordinates of the element to the coordinates of the drawing; it is applied to the curve before the curve is added</param>
        protected virtual void CreatePolyline(string points, ModOp2D transform)
        {
            var matches = Regex.Matches(points, @"(-?\d*\.?\d+(?:[eE][+-]?\d+)?)");
            var tokens = new List<string>();
            foreach (Match m in matches)
                tokens.Add(m.Value);
            int i = 0;
            List<GeoPoint2D> pointList = new List<GeoPoint2D>();
            while (i < tokens.Count)
            {
                float x = ParseFloat(tokens[i++]);
                float y = ParseFloat(tokens[i++]);
                pointList.Add(new GeoPoint2D(x, y));
            }
            Polyline2D pl2d = new Polyline2D(pointList.ToArray());
            Add(pl2d, transform);
        }

        /// <summary>
        /// Called for a <c>polygon</c> element. Parses the <c>points</c> attribute (x/y pairs separated by commas or white space),
        /// creates a closed <see cref="Polyline"/> (the first point is repeated at the end) and adds it to the current list;
        /// override it to create something else.
        /// </summary>
        /// <param name="points">the <c>points</c> attribute of the element</param>
        /// <param name="transform">the transformation from the local coordinates of the element to the coordinates of the drawing; it is applied to the curve before the curve is added</param>
        protected virtual void CreatePolygon(string points, ModOp2D transform)
        {
            var matches = Regex.Matches(points, @"(-?\d*\.?\d+(?:[eE][+-]?\d+)?)");
            var tokens = new List<string>();
            foreach (Match m in matches)
                tokens.Add(m.Value);
            int i = 0;
            List<GeoPoint2D> pointList = new List<GeoPoint2D>();
            while (i < tokens.Count)
            {
                float x = ParseFloat(tokens[i++]);
                float y = ParseFloat(tokens[i++]);
                pointList.Add(new GeoPoint2D(x, y));
            }
            pointList.Add(pointList[0]);
            Polyline2D pl2d = new Polyline2D(pointList.ToArray());
            Add(pl2d, transform);
        }

        /// <summary>
        /// Called for the <c>C</c> and <c>S</c> commands of a path. Creates a cubic Bézier curve (a <see cref="BSpline"/> of degree
        /// 3 with the four points as poles) and adds it to the current list. When a control point coincides with an end point or
        /// with the other control point, the curve is quadratic and <see cref="CreateQuadraticBezier"/> is called instead.
        /// </summary>
        /// <param name="start">the current point, start of the curve</param>
        /// <param name="control1">the first control point</param>
        /// <param name="control2">the second control point</param>
        /// <param name="end">the end point of the curve</param>
        /// <param name="transform">the transformation from the local coordinates of the element to the coordinates of the drawing; it is applied to the curve before the curve is added</param>
        protected virtual void CreateCubicBezier(Vector2 start, Vector2 control1, Vector2 control2, Vector2 end, ModOp2D transform)
        {
            if (control2.x == end.x && control2.y == end.y) CreateQuadraticBezier(start, control1, end, transform);
            else if (control2.x == control1.x && control2.y == control1.y) CreateQuadraticBezier(start, control1, end, transform);
            else if (start.x == control1.x && start.y == control1.y) CreateQuadraticBezier(start, control2, end, transform);
            else
            {
                BSpline2D bsp2d = new BSpline2D(new GeoPoint2D[] { new GeoPoint2D(start.x, start.y), new GeoPoint2D(control1.x, control1.y), new GeoPoint2D(control2.x, control2.y), new GeoPoint2D(end.x, end.y) },
                    new double[] { 1.0, 1.0, 1.0, 1.0 }, new double[] { 0.0, 1.0 }, new int[] { 4, 4 }, 3, false, 0.0, 1.0);
                Add(bsp2d, transform);
            }
        }

        /// <summary>
        /// Called for the <c>Q</c> and <c>T</c> commands of a path. Creates a quadratic Bézier curve (a <see cref="BSpline"/> of
        /// degree 2 with the three points as poles) and adds it to the current list; override it to create something else.
        /// </summary>
        /// <param name="start">the current point, start of the curve</param>
        /// <param name="control">the control point</param>
        /// <param name="end">the end point of the curve</param>
        /// <param name="transform">the transformation from the local coordinates of the element to the coordinates of the drawing; it is applied to the curve before the curve is added</param>
        protected virtual void CreateQuadraticBezier(Vector2 start, Vector2 control, Vector2 end, ModOp2D transform)
        {
            BSpline2D bsp2d = new BSpline2D(new GeoPoint2D[] { new GeoPoint2D(start.x, start.y), new GeoPoint2D(control.x, control.y), new GeoPoint2D(end.x, end.y) },
                new double[] { 1.0, 1.0, 1.0 }, new double[] { 0.0, 1.0 }, new int[] { 3, 3 }, 2, false, 0.0, 1.0);
            Add(bsp2d, transform);
        }

        /// <summary>
        /// Called for the <c>A</c> command of a path. Computes the center and the axes of the ellipse from the end point
        /// parameterization of SVG (SVG 1.1, appendix F.6), creates the elliptical arc from <paramref name="start"/> to
        /// <paramref name="end"/> and adds it to the current list. Radii that are too small to reach the end point are scaled up,
        /// as the specification requires.
        /// </summary>
        /// <param name="start">the current point, start of the arc</param>
        /// <param name="frx">the radius in x direction (before the rotation)</param>
        /// <param name="fry">the radius in y direction (before the rotation)</param>
        /// <param name="xAxisRotation">the rotation of the x axis of the ellipse in degrees</param>
        /// <param name="largeArcFlag">true to take the arc of more than 180° of the two candidates</param>
        /// <param name="sweepFlag">true to run in the direction of increasing angles (clockwise on the screen, as the y axis of SVG points downwards)</param>
        /// <param name="end">the end point of the arc</param>
        /// <param name="transform">the transformation from the local coordinates of the element to the coordinates of the drawing; it is applied to the curve before the curve is added</param>
        protected virtual void CreateEllipticalArc(Vector2 start, float frx, float fry, float xAxisRotation, bool largeArcFlag, bool sweepFlag, Vector2 end, ModOp2D transform)
        {
            double rx = frx;
            double ry = fry;
            // Winkel in Radiant
            double phi = xAxisRotation * (Math.PI / 180.0);

            // Schritt 1: Koordinaten in Ellipsen-Raum transformieren
            double dx2 = (start.x - end.x) / 2f;
            double dy2 = (start.y - end.y) / 2f;
            double x1p = (Math.Cos(phi) * dx2 + Math.Sin(phi) * dy2);
            double y1p = (-Math.Sin(phi) * dx2 + Math.Cos(phi) * dy2);

            // Schritt 2: Radien anpassen
            double rxSq = rx * rx;
            double rySq = ry * ry;
            double x1pSq = x1p * x1p;
            double y1pSq = y1p * y1p;
            double lambda = x1pSq / rxSq + y1pSq / rySq;
            if (lambda > 1)
            {
                double factor = Math.Sqrt(lambda);
                rx *= factor;
                ry *= factor;
                rxSq = rx * rx;
                rySq = ry * ry;
            }

            // Schritt 3: Mittelpunkt in Rotiertem Raum
            double sign = (largeArcFlag == sweepFlag) ? -1f : 1f;
            double num = rxSq * rySq - rxSq * y1pSq - rySq * x1pSq;
            double denom = rxSq * y1pSq + rySq * x1pSq;
            double coef = sign * Math.Sqrt(Math.Max(0, num / denom));
            double cxp = coef * ((rx * y1p) / ry);
            double cyp = coef * (-(ry * x1p) / rx);

            // Schritt 4: zurücktransformieren
            double cx = (Math.Cos(phi) * cxp - Math.Sin(phi) * cyp + (start.x + end.x) / 2f);
            double cy = (Math.Sin(phi) * cxp + Math.Cos(phi) * cyp + (start.y + end.y) / 2f);

            // Achsen-Vektoren
            var majorAxis = new GeoVector2D((rx * Math.Cos(phi)), (rx * Math.Sin(phi)));
            var minorAxis = new GeoVector2D((-ry * Math.Sin(phi)), (ry * Math.Cos(phi)));

            // Punkte
            var center = new GeoPoint2D(cx, cy);
            var startPoint = new GeoPoint2D(start.x, start.y);
            var endPoint = new GeoPoint2D(end.x, end.y);

            // sweepFlag: true = CW → counterClock = false
            bool counterClockwise = sweepFlag;

            // Transform auf Punkte/Achsen anwenden
            // Erzeugung
            EllipseArc2D ea = EllipseArc2D.Create(center, majorAxis, minorAxis, startPoint, endPoint, counterClockwise);
            Add(ea, transform);
        }

        /// <summary>
        /// Called for a <c>path</c> element. Parses the path data (the commands M, L, H, V, C, S, Q, T, A and Z, absolute and
        /// relative) and calls <see cref="CreateLine"/>, <see cref="CreateCubicBezier"/>, <see cref="CreateQuadraticBezier"/> and
        /// <see cref="CreateEllipticalArc"/> for the segments. The segments of each sub-path (started by M) are then joined to a
        /// <see cref="Path"/>: when the element has a <c>fill</c> style, a planar <see cref="Face"/> in that colour is made from
        /// the closed outline, and the path itself is added only when there is a <c>stroke</c> style too; without a fill the path
        /// is added. Nothing is created for empty path data.
        /// </summary>
        /// <param name="data">the <c>d</c> attribute of the element</param>
        /// <param name="transform">the transformation from the local coordinates of the element to the coordinates of the drawing; it is applied to the curve before the curve is added</param>
        protected virtual void CreatePath(string data, ModOp2D transform)
        {
            if (string.IsNullOrWhiteSpace(data))
                return;
            listStack.Push(new GeoObjectList());
            List<GeoObjectList> subPaths = new List<GeoObjectList>();
            // Tokenize: Befehle und Zahlen
            var matches = Regex.Matches(data, @"([MmZzLlHhVvCcQqAaSsTt])|(-?\d*\.?\d+(?:[eE][+-]?\d+)?)");

            var tokens = new List<string>();
            foreach (Match m in matches)
                tokens.Add(m.Value);

            int i = 0;
            char cmd = ' ';
            char prevCmd = ' ';

            Vector2 current = new Vector2(0.0f, 0.0f);
            Vector2 startPoint = new Vector2();
            Vector2 lastCp = new Vector2();


            while (i < tokens.Count)
            {
                string token = tokens[i++];
                if (Regex.IsMatch(token, "[MmZzLlHhVvCcQqAaSsTt]"))
                {
                    cmd = token[0];
                }
                else
                {
                    --i;
                    if (cmd == 'M') cmd = 'L'; // nach Move automatisch Line
                    if (cmd == 'm') cmd = 'l';
                }

                bool isRelative = char.IsLower(cmd);
                char uc = char.ToUpper(cmd);
                switch (uc)
                {
                    case 'M':
                        float x = ParseFloat(tokens[i++]);
                        float y = ParseFloat(tokens[i++]);
                        var p = new Vector2(x, y);
                        if (isRelative) p += current;
                        current = p;
                        startPoint = p;
                        // lastControl zurücksetzen, da kein vorheriger Cubic
                        lastCp = current;
                        if (listStack.Peek().Count > 0)
                        {
                            subPaths.Add(listStack.Pop());
                            listStack.Push(new GeoObjectList());
                        }
                        break;

                    case 'L':
                        x = ParseFloat(tokens[i++]);
                        y = ParseFloat(tokens[i++]);
                        p = new Vector2(x, y);
                        if (isRelative) p += current;
                        CreateLine(current.x, current.y, p.x, p.y, transform);
                        current = p;
                        break;

                    case 'H':
                        x = ParseFloat(tokens[i++]);
                        p = new Vector2(isRelative ? current.x + x : x, current.y);
                        CreateLine(current.x, current.y, p.x, p.y, transform);
                        current = p;
                        break;

                    case 'V':
                        y = ParseFloat(tokens[i++]);
                        p = new Vector2(current.x, isRelative ? current.y + y : y);
                        CreateLine(current.x, current.y, p.x, p.y, transform);
                        current = p;
                        break;

                    case 'C':
                        float x1 = ParseFloat(tokens[i++]);
                        float y1 = ParseFloat(tokens[i++]);
                        float x2 = ParseFloat(tokens[i++]);
                        float y2 = ParseFloat(tokens[i++]);
                        x = ParseFloat(tokens[i++]);
                        y = ParseFloat(tokens[i++]);
                        var cp1 = new Vector2(x1, y1);
                        var cp2 = new Vector2(x2, y2);
                        p = new Vector2(x, y);
                        if (isRelative)
                        {
                            cp1 += current;
                            cp2 += current;
                            p += current;
                        }
                        lastCp = cp2;
                        CreateCubicBezier(current, cp1, cp2, p, transform);
                        current = p;
                        break;
                    case 'S': // smooth cubic
                              // Berechne ersten Kontrollpunkt als Spiegelung:
                        Vector2 reflected;
                        // Spiegelung: reflektiere lastControl über current
                        reflected = current + (current - lastCp);
                        // 2) Lese (x2,y2) und (x,y) (ggf. relativ addieren)
                        x1 = ParseFloat(tokens[i++]);
                        y1 = ParseFloat(tokens[i++]);
                        lastCp = new Vector2(x1, y1);
                        x = ParseFloat(tokens[i++]);
                        y = ParseFloat(tokens[i++]);
                        p = new Vector2(x, y);
                        if (isRelative)
                        {
                            lastCp += current;
                            p += current;
                        }
                        if (prevCmd == 'C' || prevCmd == 'c' || prevCmd == 'S' || prevCmd == 's')
                        {
                            CreateCubicBezier(current, reflected, lastCp, p, transform);
                        }
                        else
                        {
                            CreateQuadraticBezier(current, lastCp, p, transform);
                        }
                        current = p;
                        break;
                    case 'Q':
                        x1 = ParseFloat(tokens[i++]);
                        y1 = ParseFloat(tokens[i++]);
                        x = ParseFloat(tokens[i++]);
                        y = ParseFloat(tokens[i++]);
                        lastCp = new Vector2(x1, y1);
                        p = new Vector2(x, y);
                        if (isRelative)
                        {
                            lastCp += current;
                            p += current;
                        }
                        CreateQuadraticBezier(current, lastCp, p, transform);
                        current = p;
                        break;

                    case 'T':
                        x = ParseFloat(tokens[i++]);
                        y = ParseFloat(tokens[i++]);
                        lastCp = current + (current - lastCp);
                        p = new Vector2(x, y);
                        if (isRelative)
                        {
                            p += current;
                        }
                        CreateQuadraticBezier(current, lastCp, p, transform);
                        current = p;
                        break;

                    case 'A':
                        float rx = ParseFloat(tokens[i++]);
                        float ry = ParseFloat(tokens[i++]);
                        float angle = ParseFloat(tokens[i++]);
                        bool largeArc = tokens[i++] == "1";
                        bool sweep = tokens[i++] == "1";
                        x = ParseFloat(tokens[i++]);
                        y = ParseFloat(tokens[i++]);
                        p = new Vector2(x, y);
                        if (isRelative)
                            p += current;
                        CreateEllipticalArc(current, rx, ry, angle, largeArc, sweep, p, transform);
                        current = p;
                        break;

                    case 'Z':
                        CreateLine(current.x, current.y, startPoint.x, startPoint.y, transform);
                        //if (!isRelative) current = startPoint;
                        current = startPoint;
                        break;

                    default:
                        // Unhandled
                        break;
                }
                prevCmd = cmd;
            }

            GeoObjectList list = listStack.Pop();
            if (list.Count > 0) subPaths.Add(list);
            // wir müssen die Paths der Größe nach sortieren und überprüfen, welches Inseln sind und entsprechende SimpleShapes erzeugen
            for (int j = 0; j < subPaths.Count; j++)
            {
                List<ICurve> lgo = new List<ICurve>(subPaths[j].OfType<ICurve>());
                Path path = Path.FromSegments(lgo, true);
                bool wasClosed = path.IsClosed;
                double prec = path.GetExtent(0.0).Size * 0.002;
                path.RemoveShortSegments(prec);
                if (!path.IsClosed && wasClosed)
                {
                    if ((path.EndPoint | path.StartPoint) < prec)
                    {   // this fixes some paths which have a small intersection at the end
                        GeoPoint mp = new GeoPoint(path.EndPoint, path.StartPoint);
                        path.StartPoint = mp;
                        path.EndPoint = mp;
                    }
                    else
                    {
                        List<ICurve> curves = new List<ICurve>(path.Curves);
                        curves.Add(Line.TwoPoints(path.EndPoint, path.StartPoint));
                        path = Path.FromSegments(curves, true);
                    }
                }
                if (!path.IsClosed) { }
                bool added = false;
                if (styles.TryGetValue("fill", out string color))
                {
                    System.Drawing.Color clr = ParseSvgColor(color);
                    if (!clr.IsEmpty)
                    {
                        if (!FillStyles.TryGetValue("SVG+" + clr.Name, out ColorDef cd))
                        {
                            cd = new ColorDef("SVG+" + clr.Name, clr);
                            FillStyles["SVG+" + clr.Name] = cd;
                        }
                        List<ICurve2D> segments = new List<ICurve2D>();
                        for (int k = 0; k < lgo.Count; k++)
                        {
                            ICurve2D c2d = lgo[k].GetProjectedCurve(Plane.XYPlane);
                            if (c2d.Length > 1e-3) segments.Add(c2d);
                        }
                        Shapes.Border bdr = Shapes.Border.FromUnorientedList(segments.ToArray(), true);
                        if (bdr != null)
                        {
                            Face fc = Face.MakeFace(new PlaneSurface(Plane.XYPlane), new Shapes.SimpleShape(bdr));
                            fc.ColorDef = cd;
                            listStack.Peek().Add(fc);
                        }
                        added = true;
                    }
                }
                if (!added || styles.ContainsKey("stroke")) listStack.Peek().Add(path);
            }
        }
        #endregion

        private static float ParseFloat(string s)
        {
            return string.IsNullOrEmpty(s) ? 0f : float.Parse(s, System.Globalization.CultureInfo.InvariantCulture);
        }
        /// <summary>
        /// Converts an SVG colour value to a <see cref="System.Drawing.Color"/>: the <c>#rgb</c> and <c>#rrggbb</c> notations,
        /// <c>rgb(r,g,b)</c> and <c>rgba(r,g,b,a)</c> with absolute or percent components, and the named colours of HTML.
        /// </summary>
        /// <param name="value">the colour text, e.g. the value of a <c>fill</c> or <c>stroke</c> style</param>
        /// <returns>the colour, or <see cref="System.Drawing.Color.Empty"/> for null, an empty string, <c>none</c> and an unknown
        /// colour name; malformed <c>rgb()</c> components throw a <see cref="FormatException"/></returns>
        public static System.Drawing.Color ParseSvgColor(string value)
        {
            if (string.IsNullOrEmpty(value) || value.Equals("none", StringComparison.OrdinalIgnoreCase))
                return System.Drawing.Color.Empty;
            value = value.Trim();
            // hex #RGB or #RRGGBB or #RRGGBBAA or named color
            if (value.StartsWith("#"))
            {
                return System.Drawing.ColorTranslator.FromHtml(value);
            }
            // rgb() or rgba()
            if (value.StartsWith("rgb(", StringComparison.OrdinalIgnoreCase) || value.StartsWith("rgba(", StringComparison.OrdinalIgnoreCase))
            {
                string inner = value.Substring(value.IndexOf('(') + 1).TrimEnd(')');
                var parts = inner.Split(',');
                int r = ParseComponent(parts[0]);
                int g = ParseComponent(parts[1]);
                int b = ParseComponent(parts[2]);
                int a = 255;
                if (parts.Length == 4)
                {
                    if (parts[3].Trim().EndsWith("%"))
                    {
                        float p = float.Parse(parts[3].Trim().TrimEnd('%'), System.Globalization.CultureInfo.InvariantCulture) / 100f;
                        a = (int)(p * 255);
                    }
                    else
                    {
                        float fa = float.Parse(parts[3], System.Globalization.CultureInfo.InvariantCulture);
                        a = (int)(fa <= 1 ? fa * 255 : fa);
                    }
                }
                return System.Drawing.Color.FromArgb(a, r, g, b);
            }
            // named color: the colors of CSS, independent of the colors the runtime knows (it has no "grey" spellings, and
            // rebeccapurple only since .NET 8)
            if (CssNamedColors.TryGetValue(value, out int rgb))
            {
                System.Drawing.Color known = System.Drawing.Color.FromName(value);
                if (known.A == 255 && (known.ToArgb() & 0xFFFFFF) == rgb) return known; // e.g. Color.Navy, as before (an unknown name gives alpha 0)
                return System.Drawing.Color.FromArgb(255, (rgb >> 16) & 0xFF, (rgb >> 8) & 0xFF, rgb & 0xFF);
            }
            try
            {
                return System.Drawing.ColorTranslator.FromHtml(value);
            }
            catch
            {
                return System.Drawing.Color.Empty;
            }
        }
        /// <summary>
        /// The 148 named colors of CSS (CSS Color Module Level 4), case insensitive, as 0xRRGGBB.
        /// </summary>
        private static readonly Dictionary<string, int> CssNamedColors = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase)
        {
            ["aliceblue"] = 0xF0F8FF, ["antiquewhite"] = 0xFAEBD7, ["aqua"] = 0x00FFFF, ["aquamarine"] = 0x7FFFD4,
            ["azure"] = 0xF0FFFF, ["beige"] = 0xF5F5DC, ["bisque"] = 0xFFE4C4, ["black"] = 0x000000,
            ["blanchedalmond"] = 0xFFEBCD, ["blue"] = 0x0000FF, ["blueviolet"] = 0x8A2BE2, ["brown"] = 0xA52A2A,
            ["burlywood"] = 0xDEB887, ["cadetblue"] = 0x5F9EA0, ["chartreuse"] = 0x7FFF00, ["chocolate"] = 0xD2691E,
            ["coral"] = 0xFF7F50, ["cornflowerblue"] = 0x6495ED, ["cornsilk"] = 0xFFF8DC, ["crimson"] = 0xDC143C,
            ["cyan"] = 0x00FFFF, ["darkblue"] = 0x00008B, ["darkcyan"] = 0x008B8B, ["darkgoldenrod"] = 0xB8860B,
            ["darkgray"] = 0xA9A9A9, ["darkgreen"] = 0x006400, ["darkgrey"] = 0xA9A9A9, ["darkkhaki"] = 0xBDB76B,
            ["darkmagenta"] = 0x8B008B, ["darkolivegreen"] = 0x556B2F, ["darkorange"] = 0xFF8C00, ["darkorchid"] = 0x9932CC,
            ["darkred"] = 0x8B0000, ["darksalmon"] = 0xE9967A, ["darkseagreen"] = 0x8FBC8F, ["darkslateblue"] = 0x483D8B,
            ["darkslategray"] = 0x2F4F4F, ["darkslategrey"] = 0x2F4F4F, ["darkturquoise"] = 0x00CED1, ["darkviolet"] = 0x9400D3,
            ["deeppink"] = 0xFF1493, ["deepskyblue"] = 0x00BFFF, ["dimgray"] = 0x696969, ["dimgrey"] = 0x696969,
            ["dodgerblue"] = 0x1E90FF, ["firebrick"] = 0xB22222, ["floralwhite"] = 0xFFFAF0, ["forestgreen"] = 0x228B22,
            ["fuchsia"] = 0xFF00FF, ["gainsboro"] = 0xDCDCDC, ["ghostwhite"] = 0xF8F8FF, ["gold"] = 0xFFD700,
            ["goldenrod"] = 0xDAA520, ["gray"] = 0x808080, ["green"] = 0x008000, ["greenyellow"] = 0xADFF2F,
            ["grey"] = 0x808080, ["honeydew"] = 0xF0FFF0, ["hotpink"] = 0xFF69B4, ["indianred"] = 0xCD5C5C,
            ["indigo"] = 0x4B0082, ["ivory"] = 0xFFFFF0, ["khaki"] = 0xF0E68C, ["lavender"] = 0xE6E6FA,
            ["lavenderblush"] = 0xFFF0F5, ["lawngreen"] = 0x7CFC00, ["lemonchiffon"] = 0xFFFACD, ["lightblue"] = 0xADD8E6,
            ["lightcoral"] = 0xF08080, ["lightcyan"] = 0xE0FFFF, ["lightgoldenrodyellow"] = 0xFAFAD2, ["lightgray"] = 0xD3D3D3,
            ["lightgreen"] = 0x90EE90, ["lightgrey"] = 0xD3D3D3, ["lightpink"] = 0xFFB6C1, ["lightsalmon"] = 0xFFA07A,
            ["lightseagreen"] = 0x20B2AA, ["lightskyblue"] = 0x87CEFA, ["lightslategray"] = 0x778899, ["lightslategrey"] = 0x778899,
            ["lightsteelblue"] = 0xB0C4DE, ["lightyellow"] = 0xFFFFE0, ["lime"] = 0x00FF00, ["limegreen"] = 0x32CD32,
            ["linen"] = 0xFAF0E6, ["magenta"] = 0xFF00FF, ["maroon"] = 0x800000, ["mediumaquamarine"] = 0x66CDAA,
            ["mediumblue"] = 0x0000CD, ["mediumorchid"] = 0xBA55D3, ["mediumpurple"] = 0x9370DB, ["mediumseagreen"] = 0x3CB371,
            ["mediumslateblue"] = 0x7B68EE, ["mediumspringgreen"] = 0x00FA9A, ["mediumturquoise"] = 0x48D1CC, ["mediumvioletred"] = 0xC71585,
            ["midnightblue"] = 0x191970, ["mintcream"] = 0xF5FFFA, ["mistyrose"] = 0xFFE4E1, ["moccasin"] = 0xFFE4B5,
            ["navajowhite"] = 0xFFDEAD, ["navy"] = 0x000080, ["oldlace"] = 0xFDF5E6, ["olive"] = 0x808000,
            ["olivedrab"] = 0x6B8E23, ["orange"] = 0xFFA500, ["orangered"] = 0xFF4500, ["orchid"] = 0xDA70D6,
            ["palegoldenrod"] = 0xEEE8AA, ["palegreen"] = 0x98FB98, ["paleturquoise"] = 0xAFEEEE, ["palevioletred"] = 0xDB7093,
            ["papayawhip"] = 0xFFEFD5, ["peachpuff"] = 0xFFDAB9, ["peru"] = 0xCD853F, ["pink"] = 0xFFC0CB,
            ["plum"] = 0xDDA0DD, ["powderblue"] = 0xB0E0E6, ["purple"] = 0x800080, ["rebeccapurple"] = 0x663399,
            ["red"] = 0xFF0000, ["rosybrown"] = 0xBC8F8F, ["royalblue"] = 0x4169E1, ["saddlebrown"] = 0x8B4513,
            ["salmon"] = 0xFA8072, ["sandybrown"] = 0xF4A460, ["seagreen"] = 0x2E8B57, ["seashell"] = 0xFFF5EE,
            ["sienna"] = 0xA0522D, ["silver"] = 0xC0C0C0, ["skyblue"] = 0x87CEEB, ["slateblue"] = 0x6A5ACD,
            ["slategray"] = 0x708090, ["slategrey"] = 0x708090, ["snow"] = 0xFFFAFA, ["springgreen"] = 0x00FF7F,
            ["steelblue"] = 0x4682B4, ["tan"] = 0xD2B48C, ["teal"] = 0x008080, ["thistle"] = 0xD8BFD8,
            ["tomato"] = 0xFF6347, ["turquoise"] = 0x40E0D0, ["violet"] = 0xEE82EE, ["wheat"] = 0xF5DEB3,
            ["white"] = 0xFFFFFF, ["whitesmoke"] = 0xF5F5F5, ["yellow"] = 0xFFFF00, ["yellowgreen"] = 0x9ACD32,
        };

        private static int ParseComponent(string s)
        {
            s = s.Trim();
            if (s.EndsWith("%"))
            {
                float p = float.Parse(s.TrimEnd('%'), System.Globalization.CultureInfo.InvariantCulture) / 100f;
                return (int)(p * 255);
            }
            return int.Parse(s, System.Globalization.CultureInfo.InvariantCulture);
        }

        private static ModOp2D ParseTransform(string transform)
        {
            var result = ModOp2D.Identity;
            // Matcht Funktionen wie "translate(10,20)" oder "rotate(45)"
            var regex = new Regex("(\\w+)\\([^)]*\\)", RegexOptions.Compiled);
            foreach (Match m in regex.Matches(transform))
            {
                string fn = m.Groups[1].Value;
                // Innerhalb der Klammern: Zahlen, durch , oder Leerzeichen getrennt
                string inner = transform.Substring(m.Index + fn.Length + 1, m.Length - fn.Length - 2);
                var parts = Regex.Split(inner, "[,\\s]+");
                var args = new List<float>();
                foreach (var p in parts)
                    if (!string.IsNullOrWhiteSpace(p))
                        args.Add(ParseFloat(p));

                switch (fn)
                {
                    case "matrix":
                        // args: a, b, c, d, e, f
                        float a = args[0], b = args[1], c = args[2], d = args[3], e = args[4], f = args[5];
                        // TODO: Ersetze YourMatrix mit deinem Matrix-Konstruktor
                        result = new ModOp2D(a, c, e, b, d, f);
                        break;
                    case "translate":
                        float tx = args[0];
                        float ty = args.Count > 1 ? args[1] : 0;
                        result = ModOp2D.Translate(tx, ty);
                        break;
                    case "scale":
                        float sx = args[0];
                        float sy = args.Count > 1 ? args[1] : sx;
                        result = ModOp2D.Scale(sx, sy);
                        break;
                    case "rotate":
                        float angle = args[0];
                        if (args.Count > 2)
                        {
                            float cx = args[1], cy = args[2];
                            result = ModOp2D.Rotate(new GeoPoint2D(cx, cy), SweepAngle.Deg(angle));
                        }
                        else
                        {
                            result = ModOp2D.Rotate(SweepAngle.Deg(angle));
                        }
                        break;
                    case "skewX":
                        float ax = args[0];
                        result = ModOp2D.Scale(ax, 1);
                        break;
                    case "skewY":
                        float ay = args[0];
                        result = ModOp2D.Scale(1, ay);
                        break;
                    default:
                        // Unbekanntes Transform-Element ignorieren
                        break;
                }
            }
            return result;
        }
    }
}