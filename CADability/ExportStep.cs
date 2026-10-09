using CADability.Attribute;
using CADability.GeoObject;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;

namespace CADability
{

    /*


#1=APPLICATION_CONTEXT('automotive design');
#2=PRODUCT_CONTEXT(' ',#1,'mechanical');
#3=PRODUCT_DEFINITION_CONTEXT('part definition',#1,' ');
#4=APPLICATION_PROTOCOL_DEFINITION('international standard','automotive_design',2001,#1);
#5=PRODUCT('%6','','',(#2));
#6=PRODUCT_DEFINITION_FORMATION_WITH_SPECIFIED_SOURCE('',' ',#5,.NOT_KNOWN.);
#7=PRODUCT_CATEGORY('part','specification');
#8=PRODUCT_RELATED_PRODUCT_CATEGORY('part',$,(#5));
#9=PRODUCT_CATEGORY_RELATIONSHIP(' ',' ',#7,#8);
#10=PRODUCT_DEFINITION('',' ',#6,#3);
#11=PRODUCT_DEFINITION_SHAPE(' ',' ',#10);
#12=(LENGTH_UNIT()NAMED_UNIT(*)SI_UNIT(.MILLI.,.METRE.));
#13=(NAMED_UNIT(*)PLANE_ANGLE_UNIT()SI_UNIT($,.RADIAN.));
#15=(NAMED_UNIT(*)SI_UNIT($,.STERADIAN.)SOLID_ANGLE_UNIT());
#16=UNCERTAINTY_MEASURE_WITH_UNIT(LENGTH_MEASURE(0.000001),#12,'distance_accuracy_value','CONFUSED CURVE UNCERTAINTY');
#17=(GEOMETRIC_REPRESENTATION_CONTEXT(3)GLOBAL_UNCERTAINTY_ASSIGNED_CONTEXT((#16))GLOBAL_UNIT_ASSIGNED_CONTEXT((#12,#13,#15))REPRESENTATION_CONTEXT(' ',' '));
#18=CARTESIAN_POINT(' ',(0.,0.,0.));
#19=AXIS2_PLACEMENT_3D(' ',#18,$,$);
#20=SHAPE_REPRESENTATION(' ',(#19),#17);
#21=SHAPE_DEFINITION_REPRESENTATION(#11,#20);";

#22=ADVANCED_BREP_SHAPE_REPRESENTATION('',(%3),#17);
#23=SHAPE_REPRESENTATION_RELATIONSHIP(' ',' ',#20, #22);
#24=MECHANICAL_DESIGN_GEOMETRIC_PRESENTATION_REPRESENTATION(' ',(%5),#17);
     */

    public interface IExportStep
    {
        int Export(ExportStep export, bool topLevel);
    }
    public class ExportStep
    {
        readonly string header =
@"ISO-10303-21;
HEADER;
FILE_DESCRIPTION(('CADability STEP Exchange'),'2;1');
FILE_NAME('%0','%1', ('none'),('none'),'CADability Version %2','CADability STEP AP214','none');
 FILE_SCHEMA(('AUTOMOTIVE_DESIGN { 1 0 10303 214 1 1 1 1 }'));
ENDSEC;
/* file written by CADability (see http://www.cadability.de) */
DATA;
#1 = APPLICATION_CONTEXT( 'core data for automotive mechanical design processes' );
#2 = PRODUCT_CONTEXT( '', #1, 'mechanical' );
#3 = PRODUCT_DEFINITION_CONTEXT( '', #1, 'design' );
#4 =  ( GEOMETRIC_REPRESENTATION_CONTEXT( 3 )GLOBAL_UNCERTAINTY_ASSIGNED_CONTEXT( ( #5 ) )GLOBAL_UNIT_ASSIGNED_CONTEXT( ( #6, #7, #8 ) )REPRESENTATION_CONTEXT( 'NONE', 'WORKSPACE' ) );
#5 = UNCERTAINTY_MEASURE_WITH_UNIT( LENGTH_MEASURE( 0.00100000000000000 ), #6, '', '' );
#6 =  ( CONVERSION_BASED_UNIT( 'MILLIMETRE', #11 )LENGTH_UNIT(  )NAMED_UNIT( #9 ) );
#7 =  ( NAMED_UNIT( #10 )PLANE_ANGLE_UNIT(  )SI_UNIT( $, .RADIAN. ) );
#8 =  ( NAMED_UNIT( #10 )SI_UNIT( $, .STERADIAN. )SOLID_ANGLE_UNIT(  ) );
#9 = DIMENSIONAL_EXPONENTS( 1.00000000000000, 0.000000000000000, 0.000000000000000, 0.000000000000000, 0.000000000000000, 0.000000000000000, 0.000000000000000 );
#10 = DIMENSIONAL_EXPONENTS( 0.000000000000000, 0.000000000000000, 0.000000000000000, 0.000000000000000, 0.000000000000000, 0.000000000000000, 0.000000000000000 );
#11 = LENGTH_MEASURE_WITH_UNIT( LENGTH_MEASURE( 1.00000000000000 ), #12 );
#12 = ( LENGTH_UNIT( )NAMED_UNIT( #9 )SI_UNIT( .MILLI., .METRE. ) );";
        readonly string footer =
@"ENDSEC;
END-ISO-10303-21;"; // %0: filename, %1: date, %2: version, %3: List of MANIFOLD_SOLID_BREP, %5: List of STYLED_ITEM, %6: part name
        // #14=PLANE_ANGLE_MEASURE_WITH_UNIT(PLANE_ANGLE_MEASURE(0.0174532925199),#13); removed


        private StreamWriter stream;
        private int currentItem { get; set; }
        public double Precision { get; internal set; }

        public Dictionary<Vertex, int> VertexToDefInd;
        public Dictionary<Edge, int> EdgeToDefInd;
        public Dictionary<ColorDef, int> StyledItems;
        public ExportStep()
        {
            VertexToDefInd = new Dictionary<Vertex, int>();
            EdgeToDefInd = new Dictionary<Edge, int>();
            StyledItems = new Dictionary<ColorDef, int>();
            Precision = 1e-6;
        }

        public byte[] WriteToByteArray(Project pr)
        {
            var memoryStream = new MemoryStream();
            stream = new StreamWriter(memoryStream);
            stream.WriteLine(header.Replace("%0", pr.FileName).Replace("%1", DateTime.Now.ToString("O")));
            currentItem = 25;
            StringBuilder lst = new StringBuilder();
            int mainAxis = WriteAxis2Placement3d(GeoPoint.Origin, GeoVector.ZAxis, GeoVector.XAxis);
            List<int> representationItems = new List<int>();
            foreach (IGeoObject go in pr.GetActiveModel())
            {
                if (go is IExportStep)
                {
                    int n = (go as IExportStep).Export(this, true);
                    if (lst.Length == 0) lst.Append("#" + n.ToString());
                    else lst.Append(",#" + n.ToString());

                    int axis = WriteAxis2Placement3d(GeoPoint.Origin, GeoVector.ZAxis, GeoVector.XAxis);
                    int repMap = WriteDefinition("REPRESENTATION_MAP(#" + mainAxis.ToString() + ",#" + n.ToString() + ")");
                    int mappedItem = WriteDefinition("MAPPED_ITEM( '', #" + repMap.ToString() + ",#" + axis.ToString() + ")");
                    representationItems.Add(mappedItem);

                }
            }
            representationItems.Add(mainAxis);
            string Name = pr.FileName;
            Name = "";
            int sr = WriteDefinition("SHAPE_REPRESENTATION('" + Name + "',(" + ToString(representationItems.ToArray(), true) + "),#4)");
            int product = WriteDefinition("PRODUCT( '" + Name + "','" + Name + "','',(#2))");
            int pdf = WriteDefinition("PRODUCT_DEFINITION_FORMATION_WITH_SPECIFIED_SOURCE( ' ', 'NONE', #" + product.ToString() + ", .NOT_KNOWN. )");
            int pd = WriteDefinition("PRODUCT_DEFINITION( 'NONE', 'NONE', #" + pdf.ToString() + ", #3 )");
            int pds = WriteDefinition("PRODUCT_DEFINITION_SHAPE( 'NONE', 'NONE', #" + pd.ToString() + " )");
            WriteDefinition("SHAPE_DEFINITION_REPRESENTATION( #" + pds.ToString() + ", #" + sr.ToString() + ")");

            StringBuilder ssi = new StringBuilder();
            foreach (int item in StyledItems.Values)
            {
                if (ssi.Length == 0) ssi.Append("#");
                else ssi.Append(",#");
                ssi.Append(item.ToString());
            }
            stream.WriteLine(footer); //.Replace("%3", lst.ToString()).Replace("%5", ssi.ToString()));
            stream.Close();

            return memoryStream.ToArray();
        }

        public void WriteToFile(string fileName, Project pr)
        {

            stream = File.CreateText(fileName);
            stream.WriteLine(header.Replace("%0", pr.FileName).Replace("%1", DateTime.Now.ToString("O")));
            currentItem = 25;
            StringBuilder lst = new StringBuilder();
            int mainAxis = WriteAxis2Placement3d(GeoPoint.Origin, GeoVector.ZAxis, GeoVector.XAxis);
            List<int> representationItems = new List<int>();
            foreach (IGeoObject go in pr.GetActiveModel())
            {
                if (go is IExportStep)
                {
                    int n = (go as IExportStep).Export(this, true);
                    if (lst.Length == 0) lst.Append("#" + n.ToString());
                    else lst.Append(",#" + n.ToString());

                    int axis = WriteAxis2Placement3d(GeoPoint.Origin, GeoVector.ZAxis, GeoVector.XAxis);
                    int repMap = WriteDefinition("REPRESENTATION_MAP(#" + mainAxis.ToString() + ",#" + n.ToString() + ")");
                    int mappedItem = WriteDefinition("MAPPED_ITEM( '', #" + repMap.ToString() + ",#" + axis.ToString() + ")");
                    representationItems.Add(mappedItem);

                }
            }
            representationItems.Add(mainAxis);
            string Name = pr.FileName;
            Name = "";
            int sr = WriteDefinition("SHAPE_REPRESENTATION('" + Name + "',(" + ToString(representationItems.ToArray(), true) + "),#4)");
            int product = WriteDefinition("PRODUCT( '" + Name + "','" + Name + "','',(#2))");
            int pdf = WriteDefinition("PRODUCT_DEFINITION_FORMATION_WITH_SPECIFIED_SOURCE( ' ', 'NONE', #" + product.ToString() + ", .NOT_KNOWN. )");
            int pd = WriteDefinition("PRODUCT_DEFINITION( 'NONE', 'NONE', #" + pdf.ToString() + ", #3 )");
            int pds = WriteDefinition("PRODUCT_DEFINITION_SHAPE( 'NONE', 'NONE', #" + pd.ToString() + " )");
            WriteDefinition("SHAPE_DEFINITION_REPRESENTATION( #" + pds.ToString() + ", #" + sr.ToString() + ")");

            StringBuilder ssi = new StringBuilder();
            foreach (int item in StyledItems.Values)
            {
                if (ssi.Length == 0) ssi.Append("#");
                else ssi.Append(",#");
                ssi.Append(item.ToString());
            }
            stream.WriteLine(footer); //.Replace("%3", lst.ToString()).Replace("%5", ssi.ToString()));
            stream.Close();
        }
        public int WriteDefinition(string s)
        {
            int nr = currentItem;
            ++currentItem;
            stream.WriteLine("#" + nr.ToString() + "=" + s + ";"); // maybe split the line when s is too long?
            return nr;
        }
        public string ToString(double d)
        {
            string res = d.ToString("G", CultureInfo.InvariantCulture);
            if (res.IndexOf('.') == -1)
            {
                int ind;
                if ((ind = res.IndexOf('E')) >= 0) res = res.Insert(ind, ".");
                else res = res + ".";
            }
            return res;
        }
        public int WriteAxis2Placement3d(GeoPoint location, GeoVector normal, GeoVector xdir)
        {
            int nl = (location as IExportStep).Export(this, false);
            int n = (normal.Normalized as IExportStep).Export(this, false);
            int nx = (xdir.Normalized as IExportStep).Export(this, false);
            return WriteDefinition("AXIS2_PLACEMENT_3D('',#" + nl.ToString() + ",#" + n.ToString() + ",#" + nx.ToString() + ")");
        }
        public int WriteAxis1Placement3d(GeoPoint location, GeoVector normal)
        {
            int nl = (location as IExportStep).Export(this, false);
            int n = (normal.Normalized as IExportStep).Export(this, false);
            return WriteDefinition("AXIS1_PLACEMENT('',#" + nl.ToString() + ",#" + n.ToString() + ")");
        }
        /// <summary>
        /// Writes the geometry of <paramref name="curve"/> (not as a top level object) and returns its entity number.
        /// A curve that cannot write itself, e.g. a <see cref="GeoObject.Path"/> or a curve that is only defined numerically,
        /// is written as a B_SPLINE_CURVE_WITH_KNOTS, which approximates it within <see cref="Precision"/>, see <see cref="ToBSpline"/>.
        /// </summary>
        internal int WriteCurve(ICurve curve)
        {
            if (curve is IExportStep exportable) return exportable.Export(this, false);
            BSpline bsp = ToBSpline(curve, Precision);
            if (bsp != null) return (bsp as IExportStep).Export(this, false);
            return (Line.TwoPoints(curve.StartPoint, curve.EndPoint) as IExportStep).Export(this, false); // a degenerate curve
        }
        /// <summary>
        /// Approximates <paramref name="curve"/> by a single cubic BSpline with the maximum deviation <paramref name="precision"/>.
        /// The segments of a <see cref="GeoObject.Path"/> are approximated one by one and joined with knots of multiplicity 3 (the degree),
        /// so the BSpline passes through all vertices of the path and keeps its corners, and a segment which is a line is
        /// reproduced exactly. A single BSpline is used rather than a COMPOSITE_CURVE, because an EDGE_CURVE needs a single
        /// curve geometry, and BSplines are supported by STEP readers far more widely than composite curves.
        /// Returns null, if the curve cannot be approximated (e.g. it has no length).
        /// </summary>
        internal static BSpline ToBSpline(ICurve curve, double precision)
        {
            const int degree = 3;
            List<ICurve> segments = new List<ICurve>();
            void Flatten(ICurve c)
            {
                if (c is GeoObject.Path path) foreach (ICurve sub in path.Curves) Flatten(sub);
                else if (c is Polyline polyline)
                {   // a polyline has corners, which a single cubic piece cannot follow
                    GeoPoint[] v = polyline.Vertices;
                    for (int i = 0; i < v.Length - 1; i++) Flatten(Line.TwoPoints(v[i], v[i + 1]));
                    if (polyline.IsClosed) Flatten(Line.TwoPoints(v[v.Length - 1], v[0]));
                }
                else if (c.Length > CADability.Precision.eps) segments.Add(c); // omit degenerate segments, the neighbours touch each other
            }
            Flatten(curve);
            List<BSpline> pieces = new List<BSpline>();
            foreach (ICurve segment in segments)
            {
                BSpline piece = BSpline.Approximate(segment.PointAt, precision);
                if (piece == null || piece.Degree != degree || piece.HasWeights) { pieces = null; break; }
                pieces.Add(piece);
            }
            if (pieces == null || pieces.Count == 0)
            {   // a degenerate curve or a segment, which cannot be approximated: approximate the curve as a whole
                return BSpline.Approximate(curve.PointAt, precision);
            }
            if (pieces.Count == 1) return pieces[0];
            // Concatenate the clamped pieces: the last pole of a piece and the first pole of the next one coincide, so it is
            // used only once. The knots of a piece are mapped to an interval as long as the segment, so that the parameter
            // runs roughly proportional to the arc length.
            List<GeoPoint> poles = new List<GeoPoint>();
            List<double> knots = new List<double>();
            List<int> multiplicities = new List<int>();
            double offset = 0.0;
            for (int i = 0; i < pieces.Count; i++)
            {
                double[] kn = pieces[i].Knots;
                int[] mu = pieces[i].Multiplicities;
                double factor = segments[i].Length / (kn[kn.Length - 1] - kn[0]);
                for (int j = 0; j < kn.Length - 1; j++)
                {
                    if (j == 0 && i > 0) multiplicities[multiplicities.Count - 1] = degree; // the joint, the knot is already there
                    else
                    {
                        knots.Add(offset + (kn[j] - kn[0]) * factor);
                        multiplicities.Add(mu[j]);
                    }
                }
                offset += segments[i].Length;
                knots.Add(offset);
                multiplicities.Add(mu[mu.Length - 1]);
                GeoPoint[] pl = pieces[i].Poles;
                for (int j = i == 0 ? 0 : 1; j < pl.Length; j++) poles.Add(pl[j]);
            }
            BSpline res = BSpline.Construct();
            res.SetData(degree, poles.ToArray(), null, knots.ToArray(), multiplicities.ToArray(), false);
            return res;
        }

        internal string ToString(int[] ints, bool makeReference)
        {
            StringBuilder res = new StringBuilder();
            for (int i = 0; i < ints.Length; i++)
            {
                if (i == 0)
                {
                    if (makeReference) res.Append("#");
                }
                else
                {
                    if (makeReference) res.Append(",#");
                    else res.Append(",");
                }
                res.Append(ints[i].ToString());
            }
            return res.ToString();
        }
        internal string ToString(double[] vals)
        {
            StringBuilder res = new StringBuilder();
            for (int i = 0; i < vals.Length; i++)
            {
                if (i != 0)
                {
                    res.Append(",");
                }
                res.Append(ToString(vals[i]));
            }
            return res.ToString();
        }

        internal string Write(GeoPoint[] p)
        {
            StringBuilder res = new StringBuilder();
            for (int i = 0; i < p.Length; i++)
            {
                if (i == 0) res.Append("#");
                else res.Append(",#");
                res.Append((p[i] as IExportStep).Export(this, false).ToString());
            }
            return res.ToString();
        }
    }
}
