using CADability.Attribute;
using CADability.Curve2D;
using CADability.GeoObject;
using System;
using System.Collections;
using System.Collections.Generic;
#if WEBASSEMBLY
using CADability.WebDrawing;
#else
using System.Drawing;
#endif
using System.Runtime.Serialization;
using Wintellect.PowerCollections;

namespace CADability
{
    internal class NeedsRepaintEventArg : EventArgs
    {
        // private Rectangle extent; entfernt, da nicht mehr notwendig und zuviel Rechenzeit
        public bool needSynchronize; // der NeedsRepaintDelegate Aufruf kommt aus einem anderen Thread, es muss erst synchronisiert werden
        public NeedsRepaintEventArg()
        {
            // extent = Extent;
            needSynchronize = false;
        }
    }
    internal delegate void NeedsRepaintDelegate(object Sender, NeedsRepaintEventArg Arg);

    /* KONZEPT Sichtbarkeit für Kanten:
     *
     * Das ProjectedModel sollte alle Objekte halten, die mit der Kantensichtbarkeit zu tun haben, da diese
     * Objekte mit dem ProjectedModel die gleiche Lebensdauer haben:
     *
     * Face->Edge[] ContourEdges
     * Edge->ProjectedEdge Sichtbarkeit einer Kante
     *
     * ProjectedModel bekommt einen Aufruf RecalcVisible, mit dem diese Dictionaries erzeugt werden.
     * Dabei werden alle Faces untersucht, und wenn noch nicht vorhanden die tangentialen Konturkanten berechnet.
     * Aus diesen und den echten Umrissen eines Faces werden für jedes Face ein CompoundShape in u/v und ein solches in x/y
     * berechnet. Jede Kante gehört zu einem oder zwei SimpleShapes. Kanten von Faces mit tangentialen Konturkanten
     * müssen aufgeteilt werden, also brauchen wir noch ein Dictionary von Kante auf aufgeteilte Kante.
     *
     * Die einzelnen Kanten werden dann durch diese Flächen (SimpleShapes) verdeckt, natürlich nur durch die Flächen, an
     * denen sie selbst nicht beteilgt sind. Dann werden sie entsprechend ihrer Sichtbarkeit dargestellt.
     *
     * Wenn man das verdecken von SimpleShapes miteinander zulässt, dann kann man die Flächen auch im Inneren pickbar machen.
     *
     */

    /// <summary>
    /// INTERN: eine 2D Kure aus einer projizierten Kante entstanden, ggf mit Sichtbarkeitsberechnung.
    /// </summary>
    internal class ProjectedEdge
    {
        public enum Kind { HardEdge, SoftEdge, ContourEdge }
        public Kind kind;
        public Edge edge;
        public ICurve2D edge2d;
    }


    /// <summary>
    ///
    /// </summary>
    [Serializable]
    public class ProjectedModel : ISerializable, IDeserializationCallback, IJsonSerialize
    {
        #region Konzept:
        /*	Konzept zum ProjectedModel:
         * Die Klasse ProjectedModel hält einen QuadTree, der die Objekte für die 2D Darstellung enthält.
         * Diese Objekte vom Typ I2DRepresentation sind "Ableger" der GeoObjekte. Mit einer HashTabelle
         * geoObjects2D kommt man von einem GeoObjekt zu seinem oder seinen I2DRepresentation Objekten.
         * Diese sind alleine Ausschlaggebend für die Darstellung. Sie beinhalten bereits die Projektion
         * (als UnscaledProjection) und müssen oft garnicht mehr auf ihr zugehöriges GeoObjekt zur
         * Darstellung zurückgreifen.
         * Wenn sich die Geometrie eines GeoObjects ändert, so wird es bei WillChange aus dem QuadTree entfernt
         * und bei DidChange wieder dort eingefügt.
         * Wenn sich nur das Attribut ändert, so dass das Objekt im QuadTree verbleiben kann
         * dann ...fehlt noch das Konzept...
         */
        #endregion
        private Dictionary<int, IGeoObject> geoObjects;
        // die beiden folgenden werden im Hintergrundthread gesetzt

        private readonly List<IPaintTo3DList> allFaces = new List<IPaintTo3DList>(); // a cache of the current display list per layer and kind of object (curve, face, transparent, unscaled)
        private readonly List<IPaintTo3DList> allTransparent = new List<IPaintTo3DList>();
        private readonly List<IPaintTo3DList> allCurves = new List<IPaintTo3DList>();
        private readonly List<IPaintTo3DList> allUnscaled = new List<IPaintTo3DList>();
        private double currentUnscaledScale;

        private string name; // Name des ProjectedModel
        private Model model; // Rückverweis auf das Modell, welches dargestellt werden soll
        private Projection projection; // Rückverweis auf die Projektion, mit der es projiziert wird
        internal GeoPoint fixPoint; // verdoppelt von ModelView, da dieses hier gespeichert wird
        internal double distance;  // verdoppelt von ModelView
        private BoundingRect extent; // die Ausdehnung in der 2D Welt, wenn leer, dann noch nicht berechnet
        private BoundingRect workSpace; // wird z.B.von PfoCAD verwendet, um eine Arbeitsfläche zu definieren
        private bool useLineWidth; // !"nur dünne Linien", muss noch implementiert werden
        internal bool supressAutoRegeneration;
        private int dbg;
        static private int dbgcnt = 0;

        //private Dictionary<Layer, object> visibleLayers;
        private HashSet<Layer> visibleLayers;
        private ArrayList tmpVisibleLayers; // nur temporär um Probleme beim Einlesen zu vermeiden
        private bool recalcVisibility;
        private bool dirty; // zugefügt, gelöscht oder geändert, die Displaylisten stimmen nicht mehr
        private bool connected;

        //internal void CollectFacesAndEdges(IGeoObject toCheck)
        //{
        //    // Solid, Shell und Face liefern hier Edges ab, ICurve wird in eigener Liste gehalten
        //    if (toCheck is Block)
        //    {
        //        Block blk = toCheck as Block;
        //        for (int i = 0; i < blk.NumChildren; ++i)
        //        {
        //            CollectFacesAndEdges(blk.Child(i));
        //        }
        //    }
        //    else if (toCheck is Face)
        //    {
        //        Face face = toCheck as Face;
        //        faces.Add(face);
        //        // Faces mit Tangenten müssen aufgespalten werden in SubFaces
        //        Edge[] c = face.GetTangentEdges(projection);
        //        List<I2DRepresentation> list = new List<I2DRepresentation>();
        //        list.AddRange(geoObjects2D[toCheck]);
        //        for (int i = 0; i < c.Length; i++)
        //        {
        //            I2DRepresentation[] rep = (c[i].Curve3D as IGeoObject).Get2DRepresentation(projection, gdiResources);
        //            list.AddRange(rep);
        //            for (int j = 0; j < rep.Length; j++)
        //            {
        //                rep[j].IsVisible = true;
        //                rep[j].GeoObject = toCheck;
        //                quadTree.AddObject(rep[j]);
        //            }
        //            countourEdges[c[i]] = new double[] { 0.0, 1.0 }; // zunächst ganz sichtbar
        //        }
        //        geoObjects2D[toCheck] = list.ToArray();
        //        Edge[] edges = face.AllEdges;
        //        for (int i = 0; i < edges.Length; i++)
        //        {
        //            normalEdges[edges[i]] = new double[] { 0.0, 1.0 }; // zunächst ganz sichtbar
        //        }
        //    }
        //    else if (toCheck is ICurve)
        //    {
        //        curves[toCheck as ICurve] = new double[] { 0.0, 1.0 }; // initialisiert als sichtbar
        //    }
        //}

        public ProjectedModel(Model m, Projection pr)
        {
            model = m;
            projection = pr;
            supressAutoRegeneration = false;
            geoObjects = new Dictionary<int, IGeoObject>();
            extent = BoundingRect.EmptyBoundingRect;
            model.GeoObjectAddedEvent += new CADability.Model.GeoObjectAdded(GeoObjectAddedEvent);
            model.GeoObjectRemovedEvent += new CADability.Model.GeoObjectRemoved(GeoObjectRemovedEvent);
            model.ExtentChangedEvent += new Model.ExtentChangedDelegate(OnModelExtentChanged);
            model.NewDisplaylistAvailableEvent += new Model.NewDisplaylistAvailableDelegate(OnNewDisplaylistAvailable);
            ColorSetting cs = Settings.GlobalSettings.GetValue("Select.SelectColor") as ColorSetting;
            useLineWidth = Settings.GlobalSettings.GetBoolValue("View.UseLineWidth", true);
            visibleLayers = new HashSet<Layer>();

            if (pr.Precision == 0.0)
            {
                pr.Precision = m.Extent.Size / 1000;
            }

            // alle Objekt müssen sich hier melden
            for (int i = 0; i < m.Count; ++i)
            {
                IGeoObject go = m[i];
                Add(go);
            }
            dbgcnt++;
            dbg = dbgcnt;
            Settings.GlobalSettings.SettingChangedEvent -= new SettingChangedDelegate(OnSettingChanged);
            //Settings.GlobalSettings.SettingChangedEvent += new SettingChangedDelegate(OnSettingChanged);
            recalcVisibility = false;
            dirty = true;
        }
        ~ProjectedModel()
        {
        }
        void OnNewDisplaylistAvailable(Model sender)
        {
            dirty = true; // damit RecalcDisplayLists aufgerufen wurd. Bei mehreren projectedModels von einem Model
            // laufen die folgenden Aufrufe leer durch
            recalcVisibility = true; // damit die Displaylisten auch verwertet werden
            if (NeedsRepaintEvent != null) NeedsRepaintEvent(this, new NeedsRepaintEventArg());
        }

        void OnModelExtentChanged(BoundingCube newExtent)
        {   // der Grund: In ein Modell wird was großes eingefügt. Anschließend wird
            // die Traingulation aufgrund der Precision gemacht: Die kann ewig dauern, wenn hier
            // nicht ein passender Wert steht
            double d = newExtent.Size / 1000;
            projection.Precision = d;
        }
        public void SetTopViewProjection()
        {
            // TODO: überprüfen: wo kommt das dran, da wird eine neue Projection gesetzt, das
            // ist gefährlich, da ModelView z.B. den Grid der Projection im PropertyTreeView hat
            // und das ggf. nicht mitkriegt...
            projection = new Projection(Projection.StandardProjection.FromTop);
            extent.MakeEmpty();
        }
        public void SetViewDirection(GeoVector dir, bool temporary)
        {
            if (Precision.IsNullVector(dir ^ new GeoVector(0.0, 0.0, 1.0)))
            {
                projection.SetDirection(dir, new GeoVector(0.0, 1.0, 0.0), model.Extent);
            }
            else
            {
                GeoVector2D updown = projection.Project2D(new GeoPoint(0, 0, 1)) - projection.Project2D(GeoPoint.Origin);
                if (updown.y <= 0) projection.SetDirection(dir, new GeoVector(0.0, 0.0, 1.0), model.Extent);
                else projection.SetDirection(dir, new GeoVector(0.0, 0.0, -1.0), model.Extent);
            }
            extent.MakeEmpty();
            if (!temporary) RecalcAll(temporary);
        }
        public void SetViewDirection(GeoPoint fromHere, GeoVector direction, GeoPoint fixPoint, bool temporary)
        {
            projection.SetPerspective(fromHere, direction, model.Extent, fixPoint);
            extent.MakeEmpty();
            if (!temporary) RecalcAll(temporary);
        }
        public void SetViewDirection(ModOp p, bool temporary)
        {
            projection.SetUnscaledProjection(p);
            extent.MakeEmpty();
            if (!temporary) RecalcAll(temporary);
        }
        public void ForceRecalc()
        {
            dirty = true;
            extent.MakeEmpty();
            RecalcAll(false);
        }
        public void Connect(PaintBuffer paintBuffer)
        {
            // sollte nicht mehr drankommen
            // paintBuffer.RepaintDrawingEvent += new CADability.PaintBuffer.Repaint(RepaintDrawing);
        }
        private IPaintTo3D paintTo3D;
        public void Connect(IPaintTo3D paintTo3D)
        {
            this.paintTo3D = paintTo3D;

            if (!connected)
            {
                connected = true;
                extent = BoundingRect.EmptyBoundingRect;
                model.GeoObjectAddedEvent += new CADability.Model.GeoObjectAdded(GeoObjectAddedEvent);
                model.GeoObjectRemovedEvent += new CADability.Model.GeoObjectRemoved(GeoObjectRemovedEvent);
                model.ExtentChangedEvent += new Model.ExtentChangedDelegate(OnModelExtentChanged);
                model.NewDisplaylistAvailableEvent += new Model.NewDisplaylistAvailableDelegate(OnNewDisplaylistAvailable);
                //geoObjects2D = new Dictionary<IGeoObject, I2DRepresentation[]>();
                // gdiResources = new GDIResources(); GDIResources bitte eliminieren
                ColorSetting cs = Settings.GlobalSettings.GetValue("Select.SelectColor") as ColorSetting;
                //if (cs != null) gdiResources.SelectColor = cs.Color;
                //else gdiResources.SelectColor = Color.DarkGray;

                if (projection.Precision == 0.0)
                {
                    projection.Precision = model.Extent.Size / 1000;
                }

                // alle Objekt müssen sich hier melden
                for (int i = 0; i < model.Count; ++i)
                {
                    IGeoObject go = model[i];
                    Add(go);
                }
                //TODO: OnSettingChanged wird nicht richtig abgemeldet, führt daher zu memory leaks, deshalb z.Z. deaktiviert
                Settings.GlobalSettings.SettingChangedEvent -= new SettingChangedDelegate(OnSettingChanged);
                //Settings.GlobalSettings.SettingChangedEvent += new SettingChangedDelegate(OnSettingChanged);
                recalcVisibility = false;
                dirty = true;
            }
        }
        public void Disconnect(IPaintTo3D paintTo3D)
        {
            foreach (IPaintTo3DList plist in allCurves) plist.Dispose();
            foreach (IPaintTo3DList plist in allFaces) plist.Dispose();
            foreach (IPaintTo3DList plist in allTransparent) plist.Dispose();
            foreach (IPaintTo3DList plist in allUnscaled) plist.Dispose();
            this.paintTo3D = null;
            Settings.GlobalSettings.SettingChangedEvent -= new SettingChangedDelegate(OnSettingChanged);
        }
        public void Disconnect(PaintBuffer paintBuffer)
        {
            // sollte nicht mehr drankommen
            //paintBuffer.RepaintDrawingEvent -= new CADability.PaintBuffer.Repaint(RepaintDrawing);
            Settings.GlobalSettings.SettingChangedEvent -= new SettingChangedDelegate(OnSettingChanged);
        }
        internal event NeedsRepaintDelegate NeedsRepaintEvent;

        public void ForceExtentTo(BoundingRect newVal)
        {
            workSpace = newVal;
            extent = newVal;
        }
        public BoundingRect GetWorldExtent()
        {	// hier besser über alles aus dem QuadTree iterieren, oder?
            if (extent.IsEmpty())
            {
                for (int i = 0; i < model.Count; ++i)
                {
                    extent.MinMax(GetDisplayExtent(model[i]));
                }
            }
            return extent;
        }
        //private BoundingRect GetDisplayExtent(I2DRepresentation[] rep)
        //{
        //    BoundingRect res = BoundingRect.EmptyBoundingRect;
        //    if (rep != null) for (int i = 0; i < rep.Length; ++i)
        //        {
        //            res.MinMax(rep[i].GetDisplayExtent(projection.WorldToDeviceFactor));
        //        }
        //    return res;
        //}
        public BoundingRect GetDisplayExtent(IGeoObject geoObject)
        {
            if (projection.Precision == 0.0)
                projection.Precision = Model.Extent.Size * 1e-3; // naja, das muss woanders gesetzt werden
            return geoObject.GetExtent(projection, ExtentPrecision.Raw);
        }
        public BoundingRect GetDisplayExtent(GeoObjectList list)
        {
            BoundingRect res = BoundingRect.EmptyBoundingRect;
            for (int i = 0; i < list.Count; ++i)
            {
                res.MinMax(GetDisplayExtent(list[i]));
            }
            return res;
        }
        public Rectangle GetDeviceExtent(IGeoObject geoObject)
        {
            return projection.DeviceRect(GetDisplayExtent(geoObject));
        }
        public BoundingRect GetDisplayExtent()
        {
            BoundingRect ext = GetWorldExtent();
            double f, dx, dy;
            if (ext.IsEmpty()) return ext;
            projection.GetPlacement(out f, out dx, out dy);
            return new BoundingRect(ext.Left * f + dx, -ext.Bottom * f + dy, ext.Right * f + dx, -ext.Top * f + dy);
        }
        /// <summary>
        /// Sets the projection to display the <see cref="Model.Extent"/> inside
        /// the given rectangle
        /// </summary>
        /// <param name="ClientRect">The client rectangle of the CondorControl</param>
        /// <param name="Factor">Additional Factor to show more or less</param>
        public void ZoomToModelExtent(Rectangle ClientRect, double Factor)
        {
            BoundingRect ext = model.GetExtent(this.projection);
            //			ext = BoundingRect.EmptyBoundingRect;
            //			ext.MinMax(projection.ProjectUnscaled(new GeoPoint(model.Extent.Xmin,model.Extent.Ymin,model.Extent.Zmin)));
            //			ext.MinMax(projection.ProjectUnscaled(new GeoPoint(model.Extent.Xmax,model.Extent.Ymin,model.Extent.Zmin)));
            //			ext.MinMax(projection.ProjectUnscaled(new GeoPoint(model.Extent.Xmin,model.Extent.Ymax,model.Extent.Zmin)));
            //			ext.MinMax(projection.ProjectUnscaled(new GeoPoint(model.Extent.Xmax,model.Extent.Ymax,model.Extent.Zmin)));
            //			ext.MinMax(projection.ProjectUnscaled(new GeoPoint(model.Extent.Xmin,model.Extent.Ymin,model.Extent.Zmax)));
            //			ext.MinMax(projection.ProjectUnscaled(new GeoPoint(model.Extent.Xmax,model.Extent.Ymin,model.Extent.Zmax)));
            //			ext.MinMax(projection.ProjectUnscaled(new GeoPoint(model.Extent.Xmin,model.Extent.Ymax,model.Extent.Zmax)));
            //			ext.MinMax(projection.ProjectUnscaled(new GeoPoint(model.Extent.Xmax,model.Extent.Ymax,model.Extent.Zmax)));
            if (ext.Width + ext.Height == 0.0) ext.Inflate(1, 1);
            ext = ext * Factor;
            projection.SetPlacement(ClientRect, ext);
        }
        public void ZoomToRect(Rectangle ClientRect, BoundingRect visibleRect)
        {
            projection.SetPlacement(ClientRect, visibleRect);
        }
        public void ZoomTotal(Rectangle ClientRect, double Factor)
        {
            BoundingRect ext = GetWorldExtent();
            if (!ext.IsEmpty() && (ext.Width > 0.0 || ext.Height > 0.0))
            {
                ext = ext * Factor;
                projection.SetPlacement(ClientRect, ext);
            }
            else
            {
                // leeres modell, Standardgröße verwenden (Würfel 100*100*100)
                ext = BoundingRect.EmptyBoundingRect;
                ext.MinMax(projection.ProjectUnscaled(new GeoPoint(0.0, 0.0, 0.0)));
                ext.MinMax(projection.ProjectUnscaled(new GeoPoint(0.0, 0.0, 100.0)));
                ext.MinMax(projection.ProjectUnscaled(new GeoPoint(0.0, 100.0, 0.0)));
                ext.MinMax(projection.ProjectUnscaled(new GeoPoint(0.0, 100.0, 100.0)));
                ext.MinMax(projection.ProjectUnscaled(new GeoPoint(100.0, 0.0, 0.0)));
                ext.MinMax(projection.ProjectUnscaled(new GeoPoint(100.0, 0.0, 100.0)));
                ext.MinMax(projection.ProjectUnscaled(new GeoPoint(100.0, 100.0, 0.0)));
                ext.MinMax(projection.ProjectUnscaled(new GeoPoint(100.0, 100.0, 100.0)));
                ext = ext * Factor;
                projection.SetPlacement(ClientRect, ext);
            }
        }
        public void Scroll(double dx, double dy)
        {
            projection.MovePlacement(dx, dy);
        }
        //private void RepaintDrawing(System.Drawing.Rectangle Extent, PaintToGDI PaintToDrawing)
        //{
        //    System.Diagnostics.Trace.WriteLine("RepaintDrawing: " + Extent.ToString()+", "+System.Environment.TickCount.ToString());
        //    if (recalcVisibility)
        //    {
        //        bool forceVisible = visibleLayers.Count == 0; // keine visible layers: alles sichtbar!
        //        foreach (KeyValuePair<IGeoObject, I2DRepresentation[]> de in geoObjects2D)
        //        {
        //            I2DRepresentation[] rep = de.Value;
        //            for (int i = 0; i < rep.Length; ++i)
        //            {
        //                if (rep[i].Layer != null && !forceVisible)
        //                {
        //                    rep[i].IsVisible = visibleLayers.ContainsKey(rep[i].Layer);
        //                }
        //                else
        //                {
        //                    rep[i].IsVisible = true;
        //                }
        //            }
        //        }
        //    }
        //    recalcVisibility = false;
        //    PaintToDrawing.SetMapping(projection);
        //    if (quadTree != null)
        //    {
        //        BoundingRect ext = projection.BoundingRectWorld2d(Extent.Left, Extent.Right, Extent.Bottom, Extent.Top);
        //        ICollection Objects = quadTree.GetObjectsFromRect(ext);
        //        foreach (I2DRepresentation i2d in Objects)
        //        {
        //            if (i2d.IsVisible)
        //            {
        //                i2d.Paint(PaintToDrawing);
        //            }
        //        }
        //    }
        //    else
        //    {
        //        foreach (I2DRepresentation[] reps in geoObjects2D.Values)
        //        {
        //            foreach (I2DRepresentation rep in reps)
        //            {
        //                if (rep.IsVisible)
        //                {
        //                    rep.Paint(PaintToDrawing);
        //                }
        //            }
        //        }
        //    }

        //    // DEBUG:
        //    //Dictionary<Type, int> AllTypes = new Dictionary<Type, int>(); // debug
        //    //foreach (I2DRepresentation [] rep2da in geoObjects2D.Values)
        //    //{
        //    //    foreach (I2DRepresentation rep2d in rep2da)
        //    //    {

        //    //        Edge2D e2d = rep2d as Edge2D;
        //    //        if (e2d != null)
        //    //        {
        //    //            if (!AllTypes.ContainsKey(e2d.curve2d.GetType()))
        //    //            {
        //    //                AllTypes[e2d.curve2d.GetType()] = 0;
        //    //            }
        //    //            AllTypes[e2d.curve2d.GetType()] = AllTypes[e2d.curve2d.GetType()] + 1;
        //    //        }
        //    //    }
        //    //}
        //}
        public Model Model { get { return model; } set { model = value; } }
        public string Name
        {
            get { return name; }
            set { name = value; }
        }
        public bool UseLineWidth
        {
            get
            {
                return useLineWidth;
            }
            set
            {
                useLineWidth = value;
            }
        }
        public Projection Projection
        {
            get
            {
                return projection;
            }
            set
            {
                projection = value.Clone();
            }
        }
        /// <summary>
        /// Marks the given Layer as visible in the context of this ProjectedModel.
        /// </summary>
        /// <param name="l">The layer</param>
        public void AddVisibleLayer(Layer l)
        {
            visibleLayers.Add(l);
            recalcVisibility = true;
            if (NeedsRepaintEvent != null) NeedsRepaintEvent(this, new NeedsRepaintEventArg());
        }
        /// <summary>
        /// Marks the given Layer as invisible in the context of this ProjectedModel.
        /// </summary>
        /// <param name="l">The layer</param>
        public void RemoveVisibleLayer(Layer l)
        {
            visibleLayers.Remove(l);
            recalcVisibility = true;
            if (NeedsRepaintEvent != null) NeedsRepaintEvent(this, new NeedsRepaintEventArg());
        }
        /// <summary>
        /// Determins whether the given <see cref="Layer"/> is marked visible in the context of this ProjectedModel.
        /// </summary>
        /// <param name="l">The layer</param>
        public bool IsLayerVisible(Layer l)
        {
            if (l == null) return true;
            return visibleLayers.Contains(l);
        }
        public Layer[] GetVisibleLayers()
        {
            Layer[] res = new Layer[visibleLayers.Count];
            visibleLayers.CopyTo(res, 0);
            return res;
        }
        internal void RecalcAll(bool temporary)
        {
            if (FrameImpl.MainFrame != null && FrameImpl.MainFrame.ActiveView != null && FrameImpl.MainFrame.ActiveView.Canvas != null)
                FrameImpl.MainFrame.ActiveView.Canvas.Cursor = "WaitCursor";

            for (int i = 0; i < model.Count; ++i)
            {
                IGeoObject go = model[i];
                go.WillChangeEvent -= new ChangeDelegate(GeoObjectWillChange);
                go.DidChangeEvent -= new ChangeDelegate(GeoObjectDidChange);
                Add(go);
            }

        }

        public void AdjustPoint(SnapPointFinder spf)
        {	// find relevant objects in octtree
            GeoObjectList l = model.GetObjectsFromRect(spf.pickArea, new Set<Layer>(GetVisibleLayers()), PickMode.singleFaceAndCurve, null);
            for (int i = 0; i < l.Count; ++i)
            {
                if (spf.IgnoreList != null && spf.IgnoreList.Contains(l[i])) continue;
                l[i].FindSnapPoint(spf);
            }
            // die Überprüfung der Schnittpunkte erfolgt hier in einer Doppelschleife
            // das wird nicht den einzelnen Objekten überlassen, da die nichts von
            // den andern Objekten wissen.
            if (spf.SnapToIntersectionPoint)
            {
                for (int i = 0; i < l.Count - 1; ++i)
                {
                    for (int j = i; j < l.Count; ++j)
                    {
                        ICurve c1 = l[i] as ICurve;
                        ICurve c2 = l[j] as ICurve;
                        if (c1 != null && c2 != null)
                        {
                            Plane pln;
                            if (Curves.GetCommonPlane(c1, c2, out pln))
                            {
                                ICurve2D c21 = c1.GetProjectedCurve(pln);
                                ICurve2D c22 = c2.GetProjectedCurve(pln);
                                GeoPoint2DWithParameter[] isp = c21.Intersect(c22);
                                for (int k = 0; k < isp.Length; ++k)
                                {
                                    if (c21.IsParameterOnCurve(isp[k].par1) && c22.IsParameterOnCurve(isp[k].par2))
                                    {
                                        spf.Check(pln.ToGlobal(isp[k].p), l[i], SnapPointFinder.DidSnapModes.DidSnapToIntersectionPoint);
                                    }
                                }
                            }
                        }
                    }
                }
            }
            if (spf.AdjustOrtho && spf.BasePointValid)
            {
                // der orthogonalmodus hat nichts mit anderen Objekten zu tun und
                // wird nur gerechnet, wenn noch kein Fangpunkt gefunden wurde
                Plane pln = new Plane(spf.BasePoint, spf.Projection.DrawingPlane.DirectionX, spf.Projection.DrawingPlane.DirectionY);
                // pln ist DrawingPlane durch den BasePoint
                GeoPoint p0 = spf.Projection.ProjectionPlane.ToGlobal(spf.SourcePoint);
                GeoVector dir = spf.Projection.Direction;
                GeoPoint p1 = pln.Intersect(p0, dir); // Punkt in pln;
                double dx = Geometry.DistPL(p1, spf.BasePoint, spf.Projection.DrawingPlane.DirectionX);
                double dy = Geometry.DistPL(p1, spf.BasePoint, spf.Projection.DrawingPlane.DirectionY);
                if (dx < dy) spf.Check(Geometry.DropPL(p1, spf.BasePoint, spf.Projection.DrawingPlane.DirectionX), null, SnapPointFinder.DidSnapModes.DidAdjustOrtho);
                else spf.Check(Geometry.DropPL(p1, spf.BasePoint, spf.Projection.DrawingPlane.DirectionY), null, SnapPointFinder.DidSnapModes.DidAdjustOrtho);
            }
            if (spf.SnapToGridPoint)
            {
                double gridx = this.projection.Grid.XDistance;
                double gridy = this.projection.Grid.YDistance;
                if (gridx > 0.0 && gridy > 0.0)
                {
                    GeoPoint2D p0 = spf.SourcePoint;
                    p0 = projection.DrawingPlane.Project(projection.DrawingPlanePoint(p0));
                    p0.x = Math.Round(p0.x / gridx) * gridx;
                    p0.y = Math.Round(p0.y / gridy) * gridy;
                    GeoPoint p1 = spf.Projection.DrawingPlane.ToGlobal(p0);
                    spf.Check(p1, null, SnapPointFinder.DidSnapModes.DidSnapToGridPoint);
                }
            }
            if (spf.SnapGlobalOrigin)
            {
                spf.Check(GeoPoint.Origin, null, SnapPointFinder.DidSnapModes.DidSnapToAbsoluteZero);
            }
            if (spf.SnapLocalOrigin)
            {
                spf.Check(spf.Projection.DrawingPlane.ToGlobal(GeoPoint.Origin), null, SnapPointFinder.DidSnapModes.DidSnapToLocalZero);
            }
        }
        /// <summary>
        /// Returns all GeoObjects that coincide with the given BoundingRect in the projection
        /// of this ProjectedModel. If parameter <paramref name="childOfThis"/> is null, this function
        /// will return the topmost parents of the objects else it will return direct children
        /// of "childOfthis".
        /// </summary>
        /// <param name="pickrect">bounding rectangle in this projection</param>
        /// <param name="childOfThis">if null returns topmost parent else return directchild of this</param>
        /// <returns>GeoObjects found</returns>
        public GeoObjectList GetObjectsFromRect(BoundingRect pickrect, IGeoObject childOfThis)
        {
            GeoObjectList test = GetObjectsFromRect(pickrect, PickMode.normal, null);
            GeoObjectList res = new GeoObjectList();
            for (int i = 0; i < test.Count; ++i)
            {
                IGeoObject go = test[i];
                IGeoObject owner = go.Owner as IGeoObject;
                while (owner != null)
                {
                    if (owner == childOfThis)
                    {
                        res.AddUnique(go);
                        break;
                    }
                    go = owner;
                    owner = go.Owner as IGeoObject;
                }
                if (owner == null) res.AddUnique(go);
            }
            return res;
        }
        public GeoObjectList GetObjectsFromRect(BoundingRect pickrect, PickMode pickMode, FilterList filterList)
        {
            GeoObjectList res = new GeoObjectList();
            if (model.octTree == null) return res; // hier ggf. lineare Suche...

            List<IGeoObject> octl = new List<IGeoObject>(model.octTree.GetObjectsFromRect(this.projection, pickrect, false));
            for (int i = octl.Count - 1; i >= 0; --i)
            {   // Unsichtbare ausblenden
                if (!octl[i].IsVisible) octl.Remove(octl[i]);
            }
            IGeoObject[] oct = octl.ToArray();
            double zmin = double.MaxValue;
            IGeoObject singleObject = null;
            GeoPoint center = projection.UnProjectUnscaled(pickrect.GetCenter());
            switch (pickMode)
            {
                case PickMode.onlyEdges:
                    foreach (IGeoObject go in oct)
                    {
                        if (go.HitTest(projection, pickrect, false))
                        {
                            // wenn Kanten gesucht werden, dann sollen auch Kurven geliefert werden, die
                            // keine eigentlichen kanten sind. Oder?
                            if (go.Owner is Edge || go is ICurve)
                            {
                                Layer l = go.Layer;
                                if (l == null && go.Owner is Edge)
                                {
                                    if ((go.Owner as Edge).Owner is Face)
                                        l = ((go.Owner as Edge).Owner as Face).Layer;
                                }
                                if ((filterList == null || filterList.Accept(go)) &&
                                    (visibleLayers.Count == 0 || (l == null || visibleLayers.Contains(l))))
                                {
                                    res.AddUnique(go);
                                }
                            }
                        }
                    }
                    return res;
                case PickMode.singleEdge:
                    foreach (IGeoObject go in oct)
                    {
                        if (go.HitTest(projection, pickrect, false))
                        {
                            if (go.Owner is Edge || go is ICurve)
                            {
                                Layer l = go.Layer;
                                if (l == null && go.Owner is Edge)
                                {
                                    if ((go.Owner as Edge).Owner is Face)
                                        l = ((go.Owner as Edge).Owner as Face).Layer;
                                }
                                if ((filterList == null || filterList.Accept(go)) &&
                                    (visibleLayers.Count == 0 || l == null || visibleLayers.Contains(l)))
                                {
                                    double z = go.Position(center, projection.Direction, model.displayListPrecision);
                                    if (z < zmin)
                                    {
                                        zmin = z;
                                        singleObject = go;
                                    }
                                }
                            }
                        }
                    }
                    if (singleObject != null) res.Add(singleObject);
                    return res;
                case PickMode.onlyFaces:
                    foreach (IGeoObject go in oct)
                    {
                        if (go is Face && go.HitTest(projection, pickrect, false))
                        {
                            if ((filterList == null || filterList.Accept(go)) &&
                                (visibleLayers.Count == 0 || go.Layer == null || visibleLayers.Contains(go.Layer)))
                            {
                                res.AddUnique(go);
                            }
                        }
                    }
                    return res;
                case PickMode.singleFace:
                    foreach (IGeoObject go in oct)
                    {
                        if (go is Face && go.HitTest(projection, pickrect, false))
                        {
                            if ((filterList == null || filterList.Accept(go)) &&
                                (visibleLayers.Count == 0 || go.Layer == null || visibleLayers.Contains(go.Layer)))
                            {
                                double z = go.Position(center, projection.Direction, model.displayListPrecision);
                                if (z < zmin)
                                {
                                    zmin = z;
                                    singleObject = go;
                                }
                            }
                        }
                    }
                    if (singleObject != null) res.Add(singleObject);
                    return res;
                case PickMode.normal:
                    {
                        Set<IGeoObject> set = new Set<IGeoObject>(new GeoObjectComparer());
                        foreach (IGeoObject go in oct)
                        {
                            if (go.HitTest(projection, pickrect, false))
                            {
                                IGeoObject toInsert = go;
                                while (toInsert.Owner is IGeoObject) toInsert = (toInsert.Owner as IGeoObject);
                                if (toInsert.Owner is Model)
                                {   // sonst werden auch edges gefunden, was hier bei single click nicht gewünscht
                                    if ((filterList == null || filterList.Accept(toInsert)) &&
                                        (visibleLayers.Count == 0 || toInsert.Layer == null || visibleLayers.Contains(toInsert.Layer)))
                                    {
                                        set.Add(toInsert);
                                    }
                                    // der set ist gigantisch viel schneller als die GeoObjectList, wenn es sehr viele
                                    // Objekte sind
                                    // res.AddUnique(toInsert);
                                }
                            }
                        }
                        res.AddRange(set);
                    }
                    return res;
                case PickMode.single:
                    foreach (IGeoObject go in oct)
                    {
                        if (go.HitTest(projection, pickrect, false))
                        {
                            double z = go.Position(center, projection.Direction, model.displayListPrecision);
                            if (z < zmin)
                            {
                                IGeoObject toInsert = go;
                                while (toInsert.Owner is IGeoObject) toInsert = (toInsert.Owner as IGeoObject);
                                if ((filterList == null || filterList.Accept(toInsert)) &&
                                    (visibleLayers.Count == 0 || toInsert.Layer == null || visibleLayers.Contains(toInsert.Layer)))
                                {
                                    if (toInsert.Owner is Model)
                                    {   // sonst werden auch edges gefunden, was hier bei single click nicht gewünscht
                                        zmin = z;
                                        singleObject = toInsert;
                                    }
                                }
                            }
                        }
                    }
                    if (singleObject != null) res.Add(singleObject);
                    return res;
                case PickMode.children:
                    foreach (IGeoObject go in oct)
                    {
                        if (go.HitTest(projection, pickrect, false))
                        {
                            if ((filterList == null || filterList.Accept(go)) &&
                                (visibleLayers.Count == 0 || go.Layer == null || visibleLayers.Contains(go.Layer)))
                            {
                                res.AddUnique(go);
                            }
                        }
                    }
                    return res;
                case PickMode.blockchildren:
                    foreach (IGeoObject go in oct)
                    {
                        if (go.HitTest(projection, pickrect, false))
                        {
                            if ((filterList == null || filterList.Accept(go)) &&
                                (visibleLayers.Count == 0 || go.Layer == null || visibleLayers.Contains(go.Layer)))
                            {
                                if (go.Owner is Block)
                                {   // beim Block die Kinder liefern
                                    res.AddUnique(go);
                                }
                                else if (go.Owner is IGeoObject)
                                {   // z.B. beim Pfad, Bemaßung das ganze Objekt
                                    res.AddUnique(go.Owner as IGeoObject);
                                }
                                else
                                {   // nicht geblockte Objekte (was ist mit Edges?)
                                    res.AddUnique(go);
                                }
                            }
                        }
                    }
                    return res;
            }
            return res;
        }
        //public GeoObjectList GetObjectsFromRectXxx(BoundingRect pickrect, PickMode pickMode)
        //{
        //    // DEBUG:
        //    if (model.octTree != null)
        //    {
        //        GeoObjectList dbg = GetObjectsFromRectXxx(pickrect, pickMode);
        //    }
        //    // END DEBUG
        //    GeoObjectList res = new GeoObjectList();
        //    ICollection co = quadTree.GetObjectsFromRect(pickrect);
        //    if (pickMode == PickMode.onlyEdges || pickMode == PickMode.singleEdge)
        //    {   // single noch implementieren
        //        foreach (IQuadTreeInsertableZ qi in co)
        //        {
        //            if (qi.ReferencedObject is ICurve && qi.HitTest(ref pickrect, false))
        //            {
        //                IGeoObject go = (qi.ReferencedObject as IGeoObject);
        //                if (go.Owner is Edge)
        //                {
        //                    res.AddUnique(go);
        //                }
        //            }
        //            else if (qi is QuadTreeCollection)
        //            {
        //                IQuadTreeInsertable[] sub = (qi as QuadTreeCollection).GetObjectsFromRect(ref pickrect);
        //                for (int i = 0; i < sub.Length; ++i)
        //                {
        //                    if (sub[i].HitTest(ref pickrect, false))
        //                    {
        //                        // DEBUG:
        //                        // sub[i].HitTest(ref pickrect, false);
        //                        IGeoObject go = (sub[i].ReferencedObject as IGeoObject);
        //                        if (go is ICurve && go.Owner is Edge)
        //                        {
        //                            res.AddUnique(go);
        //                        }
        //                    }
        //                }
        //            }
        //        }
        //    }
        //    else if (pickMode == PickMode.onlyFaces)
        //    {
        //        foreach (IQuadTreeInsertableZ qi in co)
        //        {

        //            if (qi.ReferencedObject is Face && qi.HitTest(ref pickrect, false))
        //            {
        //                res.AddUnique(qi.ReferencedObject as IGeoObject);
        //            }
        //            else if (qi is QuadTreeCollection)
        //            {
        //                IQuadTreeInsertable[] sub = (qi as QuadTreeCollection).GetObjectsFromRect(ref pickrect);
        //                for (int i = 0; i < sub.Length; ++i)
        //                {
        //                    if (sub[i].HitTest(ref pickrect, false))
        //                    {
        //                        if (sub[i].ReferencedObject is Face)
        //                        {
        //                            res.AddUnique(sub[i].ReferencedObject as IGeoObject);
        //                        }
        //                    }
        //                }
        //            }
        //        }
        //    }
        //    else if (pickMode == PickMode.singleFace)
        //    {
        //        GeoPoint2D center = pickrect.GetCenter();
        //        Face singleFace = null;
        //        double z = double.MinValue;
        //        foreach (IQuadTreeInsertableZ qi in co)
        //        {
        //            if (qi.ReferencedObject is Face && qi.HitTest(ref pickrect, false))
        //            {
        //                double zz = qi.GetZPosition(center);
        //                if (zz > z)
        //                {
        //                    z = zz;
        //                    singleFace = qi.ReferencedObject as Face;
        //                }
        //            }
        //            else if (qi is QuadTreeCollection)
        //            {
        //                IQuadTreeInsertableZ[] sub = (qi as QuadTreeCollection).GetObjectsFromRect(ref pickrect);
        //                for (int i = 0; i < sub.Length; ++i)
        //                {
        //                    if (sub[i].HitTest(ref pickrect, false))
        //                    {
        //                        if (sub[i].ReferencedObject is Face)
        //                        {
        //                            double zz = sub[i].GetZPosition(center);
        //                            if (zz > z)
        //                            {
        //                                z = zz;
        //                                singleFace = sub[i].ReferencedObject as Face;
        //                            }
        //                        }
        //                    }
        //                }
        //            }
        //        }
        //        if (singleFace != null) res.AddUnique(singleFace);
        //    }
        //    else if (pickMode == PickMode.normal)
        //    {
        //        foreach (IQuadTreeInsertableZ qi in co)
        //        {

        //            if (qi.HitTest(ref pickrect, false))
        //            {
        //                IGeoObject go = (qi.ReferencedObject as IGeoObject);
        //                while (go.Owner is IGeoObject) go = (go.Owner as IGeoObject);
        //                res.AddUnique(go);
        //            }
        //        }
        //    }
        //    else if (pickMode == PickMode.single)
        //    {
        //        GeoPoint2D center = pickrect.GetCenter();
        //        IGeoObject singleObject = null;
        //        double z = double.MinValue;
        //        foreach (IQuadTreeInsertableZ qi in co)
        //        {
        //            if (qi.HitTest(ref pickrect, false))
        //            {
        //                double zz = qi.GetZPosition(center);
        //                if (zz > z)
        //                {
        //                    z = zz;
        //                    singleObject = qi.ReferencedObject as IGeoObject;
        //                }
        //            }
        //        }
        //        if (singleObject != null) res.AddUnique(singleObject);
        //    }
        //    else if (pickMode == PickMode.children)
        //    {
        //        foreach (IQuadTreeInsertableZ qi in co)
        //        {

        //            if (qi is QuadTreeCollection)
        //            {
        //                IQuadTreeInsertable[] sub = (qi as QuadTreeCollection).GetObjectsFromRect(ref pickrect);
        //                for (int i = 0; i < sub.Length; ++i)
        //                {
        //                    if (sub[i].HitTest(ref pickrect, false))
        //                    {
        //                        IGeoObject go = (sub[i].ReferencedObject as IGeoObject);
        //                        res.AddUnique(go);
        //                    }
        //                }
        //            }
        //            else if (qi.HitTest(ref pickrect, false))
        //            {
        //                IGeoObject go = (qi.ReferencedObject as IGeoObject);
        //                res.AddUnique(go);
        //            }
        //        }
        //    }
        //    else if (pickMode == PickMode.blockchildren)
        //    {
        //        foreach (IQuadTreeInsertableZ qi in co)
        //        {

        //            if (qi is QuadTreeCollection && qi.ReferencedObject is Block)
        //            {
        //                IQuadTreeInsertable[] sub = (qi as QuadTreeCollection).GetObjectsFromRect(ref pickrect);
        //                // das liefert auf unterster Ebene,
        //                // wir wollen hier aber Pfade und andere "NichtBlöcke" zusammenhalten
        //                for (int i = 0; i < sub.Length; ++i)
        //                {
        //                    if (sub[i].HitTest(ref pickrect, false))
        //                    {
        //                        IGeoObject go = (sub[i].ReferencedObject as IGeoObject);
        //                        if (go.Owner is Block)
        //                        {
        //                            res.AddUnique(go);
        //                        }
        //                        else if (go.Owner is IGeoObject)
        //                        {
        //                            res.AddUnique(go.Owner as IGeoObject);
        //                        }
        //                    }
        //                }
        //            }
        //            else if (qi.HitTest(ref pickrect, false))
        //            {
        //                IGeoObject go = (qi.ReferencedObject as IGeoObject);
        //                res.AddUnique(go);
        //            }
        //        }
        //    }
        //    return res;
        //}
        /// <summary>
        /// Get all GeoObjects that coincide with the given BoundingRect or are close to it.
        /// This method is faster than GetObjectsFromRect but does not check the overlap
        /// of the ractangle and the GeoObject
        /// </summary>
        /// <param name="pickrect">the BoundingRect</param>
        /// <returns>List of all GeoObjects close to the BoundingRect</returns>
        public GeoObjectList GetObjectsNearRect(BoundingRect pickrect)
        {
            GeoObjectList res = new GeoObjectList();
            if (model.octTree == null) return res; // hier ggf. lineare Suche...
            res.AddRange(model.octTree.GetObjectsFromRect(this.projection, pickrect, false));
            return res;
        }
        private void Add(IGeoObject go)
        {
            go.WillChangeEvent += new ChangeDelegate(GeoObjectWillChange);
            go.DidChangeEvent += new ChangeDelegate(GeoObjectDidChange);

        }
        private void Remove(IGeoObject ToRemove)
        {
            ToRemove.WillChangeEvent -= new ChangeDelegate(GeoObjectWillChange);
            ToRemove.DidChangeEvent -= new ChangeDelegate(GeoObjectDidChange);
        }
        private void GeoObjectAddedEvent(IGeoObject go)
        {
            dirty = true;
            Add(go);
            if (NeedsRepaintEvent != null)
            {
                NeedsRepaintEvent(this, new NeedsRepaintEventArg());
            }
            extent.MakeEmpty();
        }
        private void GeoObjectRemovedEvent(IGeoObject go)
        {
            if (NeedsRepaintEvent != null)
            {	// hier müsste es noch drin sein
                NeedsRepaintEvent(this, new NeedsRepaintEventArg());
            }
            dirty = true;
            Remove(go);
            extent.MakeEmpty();
        }
        private void GeoObjectWillChange(IGeoObject Sender, GeoObjectChange Change)
        {
            //if (NeedsRepaintEvent != null && geoObjects2D.ContainsKey(Sender))
            if (NeedsRepaintEvent != null)
            {
                NeedsRepaintEvent(this, new NeedsRepaintEventArg());
            }
        }
        private void GeoObjectDidChange(IGeoObject Sender, GeoObjectChange Change)
        {
            if (NeedsRepaintEvent != null)
            {
                NeedsRepaintEvent(this, new NeedsRepaintEventArg());
            }
            dirty = true;
            extent.MakeEmpty();
        }
        #region Helper Methods
        public enum IntersectionMode { OnlyInside, BothExtensions, StartExtension, EndExtension, InsideAndSelfIntersection }
        /// <summary>
        /// Returns the parameters for all intersection point of this curve with other
        /// curves in the model. The curve must be planar. To get the 3D intersection point
        /// call <see cref="ICurve.PointAt"/>. If CheckExtension is true, there will also be
        /// intersection parameters in the extension of the curve (if the curve can extend)
        /// </summary>
        /// <param name="Curve">Curve to test for intersection points</param>
        /// <param name="CheckExtension">Also check the extended curve</param>
        /// <returns>Array of parameters</returns>
        public double[] GetIntersectionParameters(ICurve Curve, IntersectionMode mode)
        {
            ICurve[] targetCurves;
            return GetIntersectionParameters(Curve, mode, out targetCurves);
        }

        public double[] GetIntersectionParameters(ICurve Curve, IntersectionMode mode, out ICurve[] targetCurves)
        {   // gehört hier nicht (mehr) hin, soll ins Modell
            try
            {
                ICurve2D c2d = Curve.GetProjectedCurve(Projection.ProjectionPlane);
                GeoObjectList l = new GeoObjectList();
                if (mode != IntersectionMode.OnlyInside && mode != IntersectionMode.InsideAndSelfIntersection)
                {
                    // Achtung, hier muss IExtentedableCurve implementiert werden
                    if (Curve is IExtentedableCurve)
                    {
                        // hier noch IntersectionMode genauer berücksichtigen, muss allerdings in Path und Polyline entsprechend implementiert werden
                        l.AddRange(model.octTree.GetObjectsCloseTo((Curve as IExtentedableCurve).GetExtendedCurve(ExtentedableCurveDirection.both)));
                    }
                    else
                    {
                        l.AddRange(model.octTree.GetObjectsCloseTo(Curve as IOctTreeInsertable));
                    }
                }
                else
                {
                    l.AddRange(model.octTree.GetObjectsCloseTo(Curve as IOctTreeInsertable));
                }
                l.Remove(Curve as IGeoObject);
                l.DecomposeBlocks();
                // wenn Curve ein Path ist, dann enthält l alle Unterobjekte, und die müssen raus
                // selbstüberschneidende Path Objekte werden noch Probleme machen
                l.RemoveChildrenOf(Curve as IGeoObject);
                List<Pair<double, ICurve>> resCurves = new List<Pair<double, ICurve>>();
                foreach (IGeoObject go in l)
                {
                    if (go.Layer != null && !IsLayerVisible(go.Layer)) continue; // 04.16 wg. Nürnberger
                    ICurve cv = go as ICurve;
                    if (cv != null)
                    {
                        Plane pl;
                        if (Curves.GetCommonPlane(Curve, cv, out pl))
                        {
                            ICurve2D c1 = Curve.GetProjectedCurve(pl);
                            ICurve2D c2 = cv.GetProjectedCurve(pl);
                            if (c1 == null || c2 == null) continue;
                            GeoPoint2DWithParameter[] pp = c1.Intersect(c2);
                            for (int i = 0; i < pp.Length; ++i)
                            {
                                // bei Kreisbögen ist die Frage bei einem Schnittpunkt außerhalb
                                // ob man diesen als Schnittpunkt vor dem Anfang oder nach dem Ende
                                // betrachten soll. Wenn IntersectionMode.StartExtension oder
                                // IntersectionMode.EndExtension gegeben sind, dann werden solche Punkte
                                // hier noch korrigiert. (c1 ist die Ausgangskurve, deshalb par1)
                                if (mode == IntersectionMode.StartExtension)
                                {
                                    if (pp[i].par1 > 1.0) c1.ReinterpretParameter(ref pp[i].par1);
                                }
                                if (mode == IntersectionMode.EndExtension)
                                {
                                    if (pp[i].par1 < 0.0) c1.ReinterpretParameter(ref pp[i].par1);
                                }
                                bool add;
                                if (mode != IntersectionMode.OnlyInside && mode != IntersectionMode.InsideAndSelfIntersection)
                                {
                                    add = c2.IsParameterOnCurve(pp[i].par2);
                                }
                                else
                                {
                                    add = c1.IsParameterOnCurve(pp[i].par1) && c2.IsParameterOnCurve(pp[i].par2);
                                }
                                if (add)
                                {
                                    resCurves.Add(new Pair<double, ICurve>(Curve.PositionOf(pl.ToGlobal(pp[i].p), 0.5), cv));
                                }
                            }
                        }
                    }
                    if (go is Face)
                    {
                        GeoPoint[] ip;
                        GeoPoint2D[] uvOnFace;
                        double[] uOnCurve;
                        (go as Face).Intersect(Curve, out ip, out uvOnFace, out uOnCurve);
                        for (int i = 0; i < uOnCurve.Length; i++)
                        {
                            resCurves.Add(new Pair<double, ICurve>(uOnCurve[i], null));
                        }
                    }
                }
                if (mode == IntersectionMode.InsideAndSelfIntersection)
                {
                    double[] sintp = Curve.GetSelfIntersections();
                    for (int i = 0; i < sintp.Length; i++)
                    {
                        resCurves.Add(new Pair<double, ICurve>(sintp[i], Curve));
                    }
                }
                resCurves.Sort(new Comparison<Pair<double, ICurve>>(
                                        delegate (Pair<double, ICurve> pi1, Pair<double, ICurve> pi2)
                                        {
                                            return pi1.First.CompareTo(pi2.First);
                                        }));
                // following commented out because of right circle in Test_Trim_cerchio.cdb
                // if (resCurves.Count > 0 && resCurves[0].First < 1e-8) resCurves.RemoveAt(0); // no intersection points at the beginning (this is used for trimming)
                // if (resCurves.Count > 0 && resCurves[resCurves.Count-1].First >1- 1e-8) resCurves.RemoveAt(resCurves.Count - 1); // no intersection points at the beginning (this is used for trimming)
                double[] res = new double[resCurves.Count];
                targetCurves = new ICurve[resCurves.Count];
                for (int i = 0; i < resCurves.Count; i++)
                {
                    res[i] = resCurves[i].First;
                    targetCurves[i] = resCurves[i].Second;
                }
                return res;
            }
            catch (CurveException)
            {
                targetCurves = null;
                return new double[0];
            }
        }
        public bool HitTest(IGeoObject go, BoundingRect rect)
        {
            return go.HitTest(projection, rect, false);
        }
        #endregion
        private void OnSettingChanged(string Name, object NewValue)
        {
            if (Name == "Select.SelectColor")
            {
                //gdiResources.SelectColor = (NewValue as ColorSetting).Color;
            }
        }
        //private void DisplayListToLayer(IGeoObject go, IPaintTo3D paintTo3D, Dictionary<Layer, List<IPaintTo3DList>> layerDisplayList)
        //{
        //    if (go is BlockRef)
        //    {
        //        Block blk = (go as BlockRef).Flattened;
        //        // was passiert beim Picken, der Block blk ist nur temporär
        //        for (int i = 0; i < blk.NumChildren; ++i)
        //        {
        //            DisplayListToLayer(blk.Child(i), paintTo3D, layerDisplayList);
        //        }
        //    }
        //    else if (go.HasChildren())
        //    {
        //        for (int i = 0; i < go.NumChildren; ++i)
        //        {
        //            DisplayListToLayer(go.Child(i), paintTo3D, layerDisplayList);
        //        }
        //    }
        //    else
        //    {
        //        Layer l = go.Layer;
        //        IPaintTo3DList list = go.GetDisplayList(paintTo3D);
        //        if (l == null) l = nullLayer;
        //        List<IPaintTo3DList> toInsert;
        //        if (!layerDisplayList.TryGetValue(l,out toInsert))
        //        {
        //            layerDisplayList[l] = new List<IPaintTo3DList>();
        //        }
        //        if (list!=null) layerDisplayList[l].Add(list);
        //    }
        //}
        internal void Paint(IPaintTo3D paintTo3D)
        {
            // Nur das Modell darstellen. Die anderen Aspekte (Raster, aktive Objekte, Selektion) werden von
            // ModelView abgehandelt

            // recalcVisibility ist true, wenn die Sichtbarkeit der Layer umgeschaltet wurde
            // PaintFacesCache bzw PaintCurvesCache werden immer gelöscht, wenn ein Objekt zugefügt, verändert
            // oder gelöscht wird. Dann müssen die kompletten Listen neu zusammengestellt werden.
            // die einzelnen GeoObjects können ihre Displaylisten in einem Cache halten, damit das neu erzeugen
            // schneller geht.

            paintTo3D.PaintFaces(PaintTo3D.PaintMode.All);
            if (dirty || (!supressAutoRegeneration && model.displayListPrecision > paintTo3D.Precision))
            {   // die Dictionaries Layer->Displaylist neu machen
                model.RecalcDisplayLists(paintTo3D);
                dirty = true;
            }
            //System.Diagnostics.Trace.WriteLine("ProjectedModel.Paint: dirty == " + dirty.ToString());
            if (dirty || recalcVisibility)
            {
                allFaces.Clear();
                foreach (KeyValuePair<Layer, IPaintTo3DList> kv in model.layerFaceDisplayList)
                {
                    if (kv.Key == model.nullLayer || visibleLayers.Contains(kv.Key) || visibleLayers.Count == 0)
                    {
                        allFaces.Add(kv.Value);
                    }
                }

                allTransparent.Clear();
                foreach (KeyValuePair<Layer, IPaintTo3DList> kv in model.layerTransparentDisplayList)
                {
                    if (kv.Key == model.nullLayer || visibleLayers.Contains(kv.Key) || visibleLayers.Count == 0)
                    {
                        allTransparent.Add(kv.Value);
                    }
                }
                allCurves.Clear();
                foreach (KeyValuePair<Layer, IPaintTo3DList> kv in model.layerCurveDisplayList)
                {
                    if (kv.Key == model.nullLayer || visibleLayers.Contains(kv.Key) || visibleLayers.Count == 0)
                    {
                        allCurves.Add(kv.Value);
                    }
                }
            }
            if (dirty || recalcVisibility || paintTo3D.PixelToWorld != currentUnscaledScale)
            {
                currentUnscaledScale = paintTo3D.PixelToWorld;
                allUnscaled.Clear();
                foreach (KeyValuePair<Layer, GeoObjectList> kv in model.layerUnscaledObjects)
                {
                    if (kv.Key == model.nullLayer || visibleLayers.Contains(kv.Key) || visibleLayers.Count == 0)
                    {
                        paintTo3D.OpenList("unscaled_" + kv.Key.Name);
                        foreach (IGeoObject go in kv.Value)
                        {
                            go.PaintTo3D(paintTo3D);
                        }
                        allUnscaled.Add(paintTo3D.CloseList());
                    }
                }
            }

            paintTo3D.PaintFaces(PaintTo3D.PaintMode.CurvesOnly); // moves the curves a small distance to the front
            foreach (IPaintTo3DList plist in allCurves) paintTo3D.List(plist);
            if (projection.ShowFaces)
            {
                paintTo3D.PaintFaces(PaintTo3D.PaintMode.FacesOnly); // moves the faces a small distance to the back
                foreach (IPaintTo3DList plist in allFaces) paintTo3D.List(plist);
                paintTo3D.Blending(true);
                foreach (IPaintTo3DList plist in allTransparent) paintTo3D.List(plist);
                paintTo3D.Blending(false);
            }
            paintTo3D.PaintFaces(PaintTo3D.PaintMode.CurvesOnly); // macht den Faces versatz wieder rückgängig
            foreach (IPaintTo3DList plist in allUnscaled) paintTo3D.List(plist);

            dirty = false;
            recalcVisibility = false;

            //    if (recalcVisibility && layerListGenerated)
            //    {   // hier wird aus den existierenden LayerListen die Liste aller sichtbaren faces und aller
            //        // sichtbaren edges (Kurven) berechnet und
            //        recalcVisibility = false;
            //        List<IPaintTo3DList> allGeoObjects = new List<IPaintTo3DList>();
            //        foreach (KeyValuePair<Layer,IPaintTo3DList> kv in layerFaceDisplayList)
            //        {
            //            if (kv.Key == nullLayer || visibleLayers.ContainsKey(kv.Key) || visibleLayers.Count == 0)
            //            {
            //                allGeoObjects.Add(kv.Value);
            //            }
            //        }
            //        model.PaintFacesCache[paintTo3D] = paintTo3D.MakeList(allGeoObjects);
            //        allGeoObjects.Clear();
            //        foreach (KeyValuePair<Layer, IPaintTo3DList> kv in layerCurveDisplayList)
            //        {
            //            if (kv.Key == nullLayer || visibleLayers.ContainsKey(kv.Key) || visibleLayers.Count==0)
            //            {
            //                allGeoObjects.Add(kv.Value);
            //            }
            //        }
            //        model.PaintCurvesCache[paintTo3D] = paintTo3D.MakeList(allGeoObjects);
            //    }
            //    // Hat das Modell bereits feritge Darstellungen?
            //    if (projection.ShowFaces)
            //    {   // beim Drahtmodell keine Faces
            //        paintTo3D.PaintFaces(true);
            //        if (!model.PaintFacesCache.TryGetValue(paintTo3D, out list))
            //        {
            //            List<IPaintTo3DList> allGeoObjects = new List<IPaintTo3DList>();
            //            Dictionary<Layer, List<IPaintTo3DList>> layerDisplayList = new Dictionary<Layer, List<IPaintTo3DList>>();
            //            for (int i = 0; i < model.Count; ++i)
            //            {
            //                DisplayListToLayer(model[i], paintTo3D, layerDisplayList);
            //            }
            //            foreach (KeyValuePair<Layer, List<IPaintTo3DList>> kv in layerDisplayList)
            //            {
            //                list = paintTo3D.MakeList(kv.Value);
            //                layerFaceDisplayList[kv.Key] = list;
            //                if (kv.Key == nullLayer || visibleLayers.ContainsKey(kv.Key) || visibleLayers.Count == 0)
            //                {
            //                    allGeoObjects.Add(list);
            //                }
            //            }
            //            list = paintTo3D.MakeList(allGeoObjects);
            //            model.PaintFacesCache[paintTo3D] = list;
            //            layerListGenerated = true;
            //        }
            //        paintTo3D.List(list); // das ganze Modell auf einen Schlag
            //    }
            //    paintTo3D.PaintFaces(false);
            //    if (!model.PaintCurvesCache.TryGetValue(paintTo3D, out list))
            //    {
            //        List<IPaintTo3DList> allGeoObjects = new List<IPaintTo3DList>();
            //        Dictionary<Layer, List<IPaintTo3DList>> layerDisplayList = new Dictionary<Layer, List<IPaintTo3DList>>();
            //        for (int i = 0; i < model.Count; ++i)
            //        {
            //            DisplayListToLayer(model[i], paintTo3D, layerDisplayList);
            //        }
            //        foreach (KeyValuePair<Layer, List<IPaintTo3DList>> kv in layerDisplayList)
            //        {
            //            list = paintTo3D.MakeList(kv.Value);
            //            layerCurveDisplayList[kv.Key] = list;
            //            if (kv.Key == nullLayer || visibleLayers.ContainsKey(kv.Key) || visibleLayers.Count == 0)
            //            {
            //                allGeoObjects.Add(list);
            //            }
            //        }
            //        list = paintTo3D.MakeList(allGeoObjects);
            //        model.PaintCurvesCache[paintTo3D] = list;
            //    }
            //    paintTo3D.List(list); // das ganze Modell auf einen Schlag
            //}
        }

        #region ISerializable Members
        protected ProjectedModel(SerializationInfo info, StreamingContext context)
        {
            name = (string)info.GetValue("Name", typeof(string));
            model = (Model)info.GetValue("Model", typeof(Model));
            projection = (Projection)info.GetValue("Projection", typeof(Projection));
            tmpVisibleLayers = (ArrayList)info.GetValue("VisibleLayers", typeof(ArrayList));
            dbgcnt++;
            dbg = dbgcnt;
            //visibleLayers = (Dictionary<Layer, object>)info.GetValue("VisibleLayers", typeof(Dictionary<Layer, object>));
            // geht nicht, da Template exakte Version benötigt
            try
            {   // später dazugekommen
                fixPoint = (GeoPoint)info.GetValue("FixPoint", typeof(GeoPoint));
                distance = info.GetDouble("Distance");
            }
            catch (SerializationException)
            {
                fixPoint = GeoPoint.Origin;
                distance = 0.0;
            }
            dirty = true;
        }
        void ISerializable.GetObjectData(SerializationInfo info, StreamingContext context)
        {
            info.AddValue("Name", name);
            info.AddValue("Model", model);
            info.AddValue("Projection", projection);
            //info.AddValue("VisibleLayers",visibleLayers);
            // das Abspeichern von einem Template Objekt (Dictionary) erwartet beim Einlesen exakt die selbe
            // Version der Assembly und das ist nicht zu gewährleisten
            ArrayList al = new ArrayList();
            al.AddRange(visibleLayers.ToArray());
            info.AddValue("VisibleLayers", al);
            // Probleme beim Speichern von visiblelayers???
            info.AddValue("FixPoint", fixPoint);
            info.AddValue("Distance", distance);
        }
        #endregion
        #region IDeserializationCallback Members
        void IDeserializationCallback.OnDeserialization(object sender)
        {
            visibleLayers = new HashSet<Layer>();
            for (int i = 0; i < tmpVisibleLayers.Count; ++i)
            {
                visibleLayers.Add(tmpVisibleLayers[i] as Layer);
            }
            tmpVisibleLayers = null; // wieder freigeben
        }
        #endregion
        #region IJsonSerialize
        protected ProjectedModel()
        {   // empty constructor for Json
        }

        public void GetObjectData(IJsonWriteData data)
        {
            data.AddProperty("Name", name);
            data.AddProperty("Model", model);
            data.AddProperty("Projection", projection);
            data.AddProperty("VisibleLayers", new List<Layer>(visibleLayers)); // save as List<Layer>
            data.AddProperty("FixPoint", fixPoint);
            data.AddProperty("Distance", distance);
        }
        public void SetObjectData(IJsonReadData data)
        {
            name = data.GetStringProperty("Name");
            model = data.GetProperty<Model>("Model");
            projection = data.GetProperty<Projection>("Projection");
            List<Layer> tmpVisibleLayers = data.GetProperty<List<Layer>>("VisibleLayers");
            visibleLayers = new HashSet<Layer>(tmpVisibleLayers);
            fixPoint = data.GetProperty<GeoPoint>("FixPoint");
            distance = data.GetDoubleProperty("Distance");
            dirty = true;
        }
        #endregion
    }
}
