/// -----------------------------------------------------------------------------------------------------------------------------------------
/// How to use Debugger Visualizers for Visual Studio 2022:
/// Copy all files from ...\CADability\CADability.DebuggerVisualizers\bin\Debug folder to
/// ...\My Documents\Visual Studio 2022\Visualizers
/// No need to create any subfolders!
/// See: https://learn.microsoft.com/en-us/visualstudio/debugger/how-to-install-a-visualizer?view=vs-2022
/// This applies to Visual Studio 2022 and 2019

using System;
using System.Collections.Generic;
using Microsoft.VisualStudio.DebuggerVisualizers;
using CADability.Shapes;
using CADability.UserInterface;
using CADability.Curve2D;
using CADability.GeoObject;
using CADability.Attribute;
using System.Reflection;
using System.IO;
using System.Drawing;
using Point = CADability.GeoObject.Point;

#region "Types to be visualized"
//Border
[assembly: System.Diagnostics.DebuggerVisualizer(typeof(CADability.DebuggerVisualizers.BorderVisualizer), typeof(VisualizerObjectSource),
Target = typeof(CADability.Shapes.Border), Description = "CADability Border Visualizer")]

//GeoObjectList
[assembly: System.Diagnostics.DebuggerVisualizer(typeof(CADability.DebuggerVisualizers.GeoObjectListVisualizer), typeof(VisualizerObjectSource),
Target = typeof(CADability.GeoObject.GeoObjectList), Description = "CADability GeoObjectList Visualizer")]

//Simple Shape
[assembly: System.Diagnostics.DebuggerVisualizer(typeof(CADability.DebuggerVisualizers.SimpleShapeVisualizer), typeof(VisualizerObjectSource),
Target = typeof(CADability.Shapes.SimpleShape), Description = "CADability Simple Shape Visualizer")]

//Compound Shape
[assembly: System.Diagnostics.DebuggerVisualizer(typeof(CADability.DebuggerVisualizers.CompoundShapeVisualizer), typeof(VisualizerObjectSource),
Target = typeof(CADability.Shapes.CompoundShape), Description = "CADability Compound Shape Visualizer")]

//GeoPoint2D
[assembly: System.Diagnostics.DebuggerVisualizer(typeof(CADability.DebuggerVisualizers.GeoPoint2DVisualizer), typeof(VisualizerObjectSource),
Target = typeof(CADability.GeoPoint2D), Description = "CADability GeoPoint2D Visualizer")]

//GeoPoint
[assembly: System.Diagnostics.DebuggerVisualizer(typeof(CADability.DebuggerVisualizers.GeoPointVisualizer), typeof(VisualizerObjectSource),
Target = typeof(CADability.GeoPoint), Description = "CADability GeoPoint Visualizer")]

//ICurve2D Implementations
[assembly: System.Diagnostics.DebuggerVisualizer(typeof(CADability.DebuggerVisualizers.Curve2DVisualizer), typeof(VisualizerObjectSource),
Target = typeof(CADability.Curve2D.GeneralCurve2D), Description = "CADability GeneralCurve2D Visualizer")]

[assembly: System.Diagnostics.DebuggerVisualizer(typeof(CADability.DebuggerVisualizers.Curve2DVisualizer), typeof(VisualizerObjectSource),
Target = typeof(CADability.Curve2DAspect), Description = "CADability Curve2DAspect Visualizer")]

//IGeoObject Implementations
[assembly: System.Diagnostics.DebuggerVisualizer(typeof(CADability.DebuggerVisualizers.GeoObjectVisualizer), typeof(VisualizerObjectSource),
Target = typeof(CADability.GeoObject.IGeoObjectImpl), Description = "CADability IGeoObject Visualizer")]

//ICurve Implementations
[assembly: System.Diagnostics.DebuggerVisualizer(typeof(CADability.DebuggerVisualizers.CurveVisualizer), typeof(VisualizerObjectSource),
Target = typeof(CADability.GeoObject.BSpline), Description = "CADability ICurve Visualizer")]

[assembly: System.Diagnostics.DebuggerVisualizer(typeof(CADability.DebuggerVisualizers.CurveVisualizer), typeof(VisualizerObjectSource),
Target = typeof(CADability.GeoObject.Ellipse), Description = "CADability ICurve Visualizer")]

[assembly: System.Diagnostics.DebuggerVisualizer(typeof(CADability.DebuggerVisualizers.CurveVisualizer), typeof(VisualizerObjectSource),
Target = typeof(CADability.GeoObject.GeneralCurve), Description = "CADability ICurve Visualizer")]

[assembly: System.Diagnostics.DebuggerVisualizer(typeof(CADability.DebuggerVisualizers.CurveVisualizer), typeof(VisualizerObjectSource),
Target = typeof(CADability.GeoObject.Line), Description = "CADability ICurve Visualizer")]

[assembly: System.Diagnostics.DebuggerVisualizer(typeof(CADability.DebuggerVisualizers.CurveVisualizer), typeof(VisualizerObjectSource),
Target = typeof(CADability.GeoObject.Path), Description = "CADability ICurve Visualizer")]

[assembly: System.Diagnostics.DebuggerVisualizer(typeof(CADability.DebuggerVisualizers.CurveVisualizer), typeof(VisualizerObjectSource),
Target = typeof(CADability.GeoObject.Polyline), Description = "CADability ICurve Visualizer")]

//IDebuggerVisualizer Implementations
[assembly: System.Diagnostics.DebuggerVisualizer(typeof(CADability.DebuggerVisualizers.GeneralDebuggerVisualizer), typeof(VisualizerObjectSource),
Target = typeof(CADability.BRepItem), Description = "CADability IDebuggerVisualizer Visualizer")]

[assembly: System.Diagnostics.DebuggerVisualizer(typeof(CADability.DebuggerVisualizers.GeneralDebuggerVisualizer), typeof(VisualizerObjectSource),
Target = typeof(CADability.Curve2D.BSpline2D), Description = "CADability IDebuggerVisualizer Visualizer")]

[assembly: System.Diagnostics.DebuggerVisualizer(typeof(CADability.DebuggerVisualizers.GeneralDebuggerVisualizer), typeof(VisualizerObjectSource),
Target = typeof(CADability.GeoObject.BoxedSurfaceEx.ParEpi), Description = "CADability IDebuggerVisualizer Visualizer")]

[assembly: System.Diagnostics.DebuggerVisualizer(typeof(CADability.DebuggerVisualizers.GeneralDebuggerVisualizer), typeof(VisualizerObjectSource),
Target = typeof(CADability.DebuggerContainer), Description = "CADability DebuggerContainer Visualizer")]

#endregion

namespace CADability.DebuggerVisualizers
{
    internal class Trace
    {
        static public void Clear()
        {
            if (File.Exists(@"C:\Temp\CADability.Trace.txt"))
                File.Delete(@"C:\Temp\CADability.Trace.txt");
        }
        static public void WriteLine(string text)
        {
            lock (typeof(Trace))
            {
                using (StreamWriter w = File.AppendText(@"C:\Temp\CADability.Trace.txt"))
                {
                    w.WriteLine(text);
                }
            }
        }
    }

    internal class CheckInstanceCounters
    {
        public static void Check()
        {
            Assembly ThisAssembly = Assembly.GetExecutingAssembly();
            Type[] types = ThisAssembly.GetTypes();
            System.Diagnostics.Trace.WriteLine("--- Instance Counters ---");
            System.GC.Collect();
            System.GC.WaitForPendingFinalizers();
            System.GC.Collect();
            System.GC.WaitForPendingFinalizers();
            long mem = System.GC.GetTotalMemory(true);
            System.Diagnostics.Trace.WriteLine("memory used: " + mem.ToString());
            for (int i = 0; i < types.Length; ++i)
            {
                FieldInfo fi = types[i].GetField("InstanceCounter", BindingFlags.Static | BindingFlags.NonPublic);
                if (fi != null)
                {
                    object val = fi.GetValue(null);
                    try
                    {
                        int n = (int)val;
                        System.Diagnostics.Trace.WriteLine(types[i].Name + ": " + n.ToString());
                    }
                    catch (InvalidCastException)
                    {
                    }
                }
            }
            System.Diagnostics.Trace.WriteLine("--- End End   End End ---");
        }
    }

    /// <summary>
    /// Creates a DebugForm as defined in CADability.Forms. 
    /// CADability.Forms.exe must be accessible at runtime to be able to debug
    /// </summary>
    static class CF
    {
        /// <summary>
        /// Load the assembly of CADability.Forms and instantiate the class
        /// </summary>
        public static IDebugForm DebugForm
        {
            get
            {
                Assembly cf = Assembly.Load("CADability.DebuggerVisualizers");
                Type tp = cf.GetType("CADability.DebuggerVisualizers.DebugForm");
                if (tp is null)
                    throw new ApplicationException("Failed to get type: CADability.DebuggerVisualizers.DebugForm");
                else
                {
                    ConstructorInfo ci = tp.GetConstructor(new Type[0]);
                    if (ci is null)
                        throw new ApplicationException("Failed to get Constructor of CADability.DebuggerVisualizers.DebugForm");
                    else
                    {
                        object df = ci.Invoke(new object[0]);
                        return df as IDebugForm;
                    }
                }                
            }
        }
    }
    public static class DebuggerExtensions
    {
        public static DebuggerContainer Show(this IEnumerable<object> obj)
        {
            return DebuggerContainer.Show(obj);
        }
    }

    /* So benutzt man den DebuggerVisualizer aus dem Command Window:
     * ? GeneralDebuggerVisualizer.TestShowVisualizer(res.DebugEdges3D);
     */
    public class GeneralDebuggerVisualizer : DialogDebuggerVisualizer
    {
        protected override void Show(IDialogVisualizerService windowService, IVisualizerObjectProvider objectProvider)
        {
            IDebugForm form = CF.DebugForm;
            Model m = form.Model;
            object o = objectProvider.GetObject();
            if (o is IDebuggerVisualizer)
            {
                IDebuggerVisualizer dv = (IDebuggerVisualizer)objectProvider.GetObject();
                m.Add(dv.GetList());
            }
            else if (o is IGeoObject)
            {
                m.Add(o as IGeoObject);
            }
            form.ShowDialog(windowService);
        }
        public static void TestShowVisualizer(object objectToVisualize)
        {
            VisualizerDevelopmentHost visualizerHost = new VisualizerDevelopmentHost(objectToVisualize, typeof(GeneralDebuggerVisualizer));
            visualizerHost.ShowVisualizer();
        }
        // ? GeneralDebuggerVisualizer.TestShowVisualizer(face.DebugEdges3D); // aus Command Window
    }
    class VisualizerHelper
    {
        static private ColorDef pointColor = null;
        static public ColorDef PointColor
        {
            get
            {
                if (pointColor == null)
                {
                    pointColor = new ColorDef("auto point", Color.Brown);
                }
                return pointColor;
            }
        }
        static private ColorDef curveColor = null;
        static public ColorDef CurveColor
        {
            get
            {
                if (curveColor == null)
                {
                    curveColor = new ColorDef("auto point", Color.DarkCyan);
                }
                return curveColor;
            }
        }
        static private ColorDef faceColor = null;
        static public ColorDef FaceColor
        {
            get
            {
                if (faceColor == null)
                {
                    faceColor = new ColorDef("auto point", Color.GreenYellow);
                }
                return faceColor;
            }
        }
        static public IGeoObject AssertColor(IGeoObject go)
        {
            if (go is IColorDef cd && cd.ColorDef == null)
            {
                if (go is GeoObject.Point) cd.ColorDef = PointColor;
                if (go is ICurve) cd.ColorDef = CurveColor;
                if (go is Face) cd.ColorDef = FaceColor;
                if (go is Shell) cd.ColorDef = FaceColor;
                if (go is Solid) cd.ColorDef = FaceColor;
            }
            return go;
        }
        static public GeoObjectList AssertColor(GeoObjectList list)
        {
            foreach (IGeoObject go in list)
            {
                if (go is IColorDef cd && cd.ColorDef == null)
                {
                    if (go is GeoObject.Point) cd.ColorDef = PointColor;
                    if (go is ICurve) cd.ColorDef = CurveColor;
                    if (go is Face) cd.ColorDef = FaceColor;
                    if (go is Shell) cd.ColorDef = FaceColor;
                    if (go is Solid) cd.ColorDef = FaceColor;
                }
            }
            return list;
        }

    }
    internal class GeoObjectVisualizer : DialogDebuggerVisualizer
    {
        protected override void Show(IDialogVisualizerService windowService, IVisualizerObjectProvider objectProvider)
        {
            IDebugForm form = CF.DebugForm;
            Model m = form.Model;

            IGeoObjectImpl go = (IGeoObjectImpl)objectProvider.GetObject();
            m.Add(VisualizerHelper.AssertColor(go));

            form.ShowDialog(windowService);
        }

        /// <summary>
        /// Damit kann man den Visualizer zum Debuggen im Context von CADability aufrufen, sonst läuft er immer im
        /// Context des Debuggers
        /// </summary>
        /// <param name="objectToVisualize">The object to display in the visualizer.</param>
        public static void TestShowVisualizer(object objectToVisualize)
        {
            VisualizerDevelopmentHost visualizerHost = new VisualizerDevelopmentHost(objectToVisualize, typeof(GeoObjectVisualizer));
            visualizerHost.ShowVisualizer();
        }
    }

    internal class GeoObjectListVisualizer : DialogDebuggerVisualizer
    {
        protected override void Show(IDialogVisualizerService windowService, IVisualizerObjectProvider objectProvider)
        {
            IDebugForm form = CF.DebugForm;
            Model m = form.Model;

            GeoObjectList list = (GeoObjectList)objectProvider.GetObject();

            if (list.Count > 0)
            {
                for (int i = 0; i < list.Count; ++i)
                {
                    IntegerProperty ip = new IntegerProperty(i, "Debug.Hint");
                    list[i].UserData.Add("ListIndex", ip);
                    m.Add(VisualizerHelper.AssertColor(list[i]));
                }
                m.Add(list);
            }
            form.ShowDialog(windowService);
        }
        public static void TestShowVisualizer(object objectToVisualize)
        {
            VisualizerDevelopmentHost visualizerHost = new VisualizerDevelopmentHost(objectToVisualize, typeof(GeoObjectListVisualizer));
            visualizerHost.ShowVisualizer();
        }
    }

    public class BorderVisualizer : DialogDebuggerVisualizer
    {
        protected override void Show(IDialogVisualizerService windowService, IVisualizerObjectProvider objectProvider)
        {
            IDebugForm form = CF.DebugForm;
            Model m = form.Model;

            Border bdr = (Border)objectProvider.GetObject();
            for (int i = 0; i < bdr.DebugList.Count; ++i)
            {
                IGeoObject toAdd = bdr.DebugList[i];
                IntegerProperty ip = new IntegerProperty(i, "Debug.Hint");
                toAdd.UserData.Add("Debug", ip);
                VisualizerHelper.AssertColor(toAdd);
                m.Add(toAdd);
            }

            form.ShowDialog(windowService);
        }

        public static void TestBorderVisualizer(object objectToVisualize)
        {
            VisualizerDevelopmentHost myHost = new VisualizerDevelopmentHost(objectToVisualize, typeof(BorderVisualizer));
            myHost.ShowVisualizer();
        }
    }

    internal class Curve2DVisualizer : DialogDebuggerVisualizer
    {
        protected override void Show(IDialogVisualizerService windowService, IVisualizerObjectProvider objectProvider)
        {
            IDebugForm form = CF.DebugForm;
            Model m = form.Model;

            ICurve2D gc2d = (ICurve2D)objectProvider.GetObject();
            m.Add(VisualizerHelper.AssertColor(gc2d.MakeGeoObject(Plane.XYPlane)));

            form.ShowDialog(windowService);
        }
    }

    internal class CurveVisualizer : DialogDebuggerVisualizer
    {
        protected override void Show(IDialogVisualizerService windowService, IVisualizerObjectProvider objectProvider)
        {
            IDebugForm form = CF.DebugForm;
            Model m = form.Model;

            IGeoObject go = (IGeoObject)objectProvider.GetObject();
            VisualizerHelper.AssertColor(go);
            m.Add(go);

            form.ShowDialog(windowService);
        }
    }

    internal class GeoPoint2DVisualizer : DialogDebuggerVisualizer
    {
        protected override void Show(IDialogVisualizerService windowService, IVisualizerObjectProvider objectProvider)
        {
            IDebugForm form = CF.DebugForm;
            Model m = form.Model;

            GeoPoint2D p = (GeoPoint2D)objectProvider.GetObject();
            Point pnt = Point.Construct();
            pnt.Location = new GeoPoint(p);
            pnt.Symbol = PointSymbol.Cross;
            VisualizerHelper.AssertColor(pnt);
            m.Add(pnt);

            form.ShowDialog(windowService);
        }
    }

    internal class GeoPointVisualizer : DialogDebuggerVisualizer
    {
        protected override void Show(IDialogVisualizerService windowService, IVisualizerObjectProvider objectProvider)
        {
            IDebugForm form = CF.DebugForm;
            Model m = form.Model;

            GeoPoint p = (GeoPoint)objectProvider.GetObject();
            Point pnt = Point.Construct();
            pnt.Location = p;
            pnt.Symbol = PointSymbol.Cross;
            VisualizerHelper.AssertColor(pnt);
            m.Add(pnt);

            form.ShowDialog(windowService);
        }
    }

    internal class CompoundShapeVisualizer : DialogDebuggerVisualizer
    {
        protected override void Show(IDialogVisualizerService windowService, IVisualizerObjectProvider objectProvider)
        {
            IDebugForm form = CF.DebugForm;
            Model m = form.Model;

            CompoundShape compoundShape = (CompoundShape)objectProvider.GetObject();
            m.Add(VisualizerHelper.AssertColor(compoundShape.DebugList));

            form.ShowDialog(windowService);
        }
    }

    internal class SimpleShapeVisualizer : DialogDebuggerVisualizer
    {
        protected override void Show(IDialogVisualizerService windowService, IVisualizerObjectProvider objectProvider)
        {
            IDebugForm form = CF.DebugForm;
            Model m = form.Model;

            SimpleShape simpleShape = (SimpleShape)objectProvider.GetObject();
            m.Add(VisualizerHelper.AssertColor(simpleShape.DebugList));

            form.ShowDialog(windowService);
        }

        /// <summary>
        /// Damit kann man den Visualizer zum Debuggen im Context von CADability aufrufen, sonst läuft er immer im
        /// Context des Debuggers
        /// </summary>
        /// <param name="objectToVisualize">The object to display in the visualizer.</param>
        public static void TestShowVisualizer(object objectToVisualize)
        {
            VisualizerDevelopmentHost visualizerHost = new VisualizerDevelopmentHost(objectToVisualize, typeof(SimpleShapeVisualizer));
            visualizerHost.ShowVisualizer();
        }
    }
}