using CADability.Attribute;
using CADability.Curve2D;
using CADability.GeoObject;
using CADability.UserInterface;
using System;
using System.Collections;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Runtime.Serialization;
using System.Threading;
using System.Threading.Tasks;
using Wintellect.PowerCollections;

namespace CADability
{
    /// <summary>
    /// How objects are selected or picked
    /// </summary>
    public enum PickMode
    {
        /// <summary>
        /// Return one or more top level GeoObjects
        /// </summary>
        normal,
        /// <summary>
        /// Return only one (the topmost according to the view) GeoObject
        /// </summary>
        single,
        /// <summary>
        /// Return only edges of faces, shells or solids
        /// </summary>
        onlyEdges,
        /// <summary>
        /// Return only faces which are either top level GeoObjects or parts of shells or solids
        /// </summary>
        onlyFaces,
        /// <summary>
        /// Return only a single edge, which is closest to the viewer
        /// </summary>
        singleEdge,
        /// <summary>
        /// Return only a single face, which is closest to the viewer
        /// </summary>
        singleFace,
        /// <summary>
        /// Return GeoObjects at the lowest level
        /// </summary>
        children,
        /// <summary>
        /// If a block is selected return only the selected GeoObjects at the lowest level
        /// </summary>
        blockchildren,
        /// <summary>
        /// If a block is selected return only the selected GeoObjects at the highest level below the block
        /// </summary>
        singleChild
    }

    /// <summary>
    ///
    /// </summary>
    [Serializable]
    public class Model : IShowPropertyImpl, ISerializable, IGeoObjectOwner,
            IEnumerable, ICommandHandler, ICategorizedDislayLists, IDeserializationCallback, IJsonSerialize, IJsonSerializeDone
    {
        private BoundingCube? extent; // Die Ausdehnung, noch nicht klar wie die benutzt werden soll
        private BoundingCube minExtend; // die Mindestausdehnung, die z.B. für das Raster oder für die leere Darstellung verwendet wird
        private GeoObjectList geoObjects; // die Liste aller Objekte
        private string name; // der Name des Modells
        private bool noSingleAddEvents;
        private DriveList allDrives;
        private ScheduleList allSchedules;
        private UserData userData;
        private readonly object extentLock = new object();

        /// <summary>
        /// Units
        /// </summary>
        public enum Units
        {
            /// <summary>
            /// Model units are millimeters
            /// </summary>
            millimeter,
            /// <summary>
            /// Model units are cenitmeters
            /// </summary>
            cenitmeter,
            /// <summary>
            /// Model units are meters
            /// </summary>
            meter,
            /// <summary>
            /// Model units are kilometers
            /// </summary>
            kilometer,
            /// <summary>
            /// Model units are inches
            /// </summary>
            inch,
            /// <summary>
            /// Model units are foot
            /// </summary>
            foot
        };
        private Units unit; // die Einheiten des Weltkoordinatensystems
        private double defaultScale; // Vorzugsmaßstab Welt*Maßstab = Papier, 1:1000 = 0.001
        private double lineStyleScale; // für LinePattern und LineWidth
        private Hashtable projectionToExtent;
        private Set<IGeoObject> continousChanges;
        // Displaylisten für alle Objekte nach Layern aufgelistet
        internal LayerToDisplayListDictionary layerFaceDisplayList; // Alle Faces, geordnet nach Layern
        internal LayerToDisplayListDictionary layerTransparentDisplayList; // Alle transparenten, geordnet nach Layern
        internal LayerToDisplayListDictionary layerCurveDisplayList; // alle Curves etc. geordnet nach Layern
        internal Dictionary<Layer, GeoObjectList> layerUnscaledObjects; // die Liste aller UnscaledObjects
        internal Dictionary<Layer, List<IGeoObject>> layerFaceObjects; // temporär Alle Faces, geordnet nach Layern
        internal Dictionary<Layer, List<IGeoObject>> layerTransparentObjects; // temporär Alle transparenten, geordnet nach Layern
        internal Dictionary<Layer, List<IGeoObject>> layerCurveObjects; // temporär alle Curves etc. geordnet nach Layern
        internal bool showTransparentFaceEdges; // zeige die Kanten von transparenten Faces
        internal double displayListPrecision;
        internal Layer nullLayer; // key für obige Listen, wenn kein Layer gegeben ist
        private volatile bool displayListsDirty; // wird bei Zugriff nicht gelockt
        internal OctTree<IGeoObject> octTree; // erstmal internal, mal sehen...
        private HashSet<string> runningThreads; //List with the IDs (go.UniqueID+precision) of the geo objects being recomputed in the background.
        private EventWaitHandle recalcDone;
        Thread manageBackgroundRecalc; //Background thread that queue the computation of the display list in the thread pool.
        double backgroundRecalcPrecision; //Precision used by the background thread to compute the display lists.
        CancellationTokenSource cancellationTokenSource = null; //To cancel the background threads used to compute the display lists of the geo objects.

        private void RecalcGeoObject(object state)
        {   // wird aus dem ThreadPool heraus gestartet.
            // Wenn er noch in der Liste runningThreads steht,
            // dann dort die thread id eintragen und anschließend rechnen
            // am Ende aus der Liste entfernen.
            // Wurde die berechnung abgebrochen, so sollte runningThreads leer sein.
            RecalcGeoObjectData data = state as RecalcGeoObjectData;
            if (!data.CancellationToken.IsCancellationRequested) //If the computation with this precision was canceled by another request end immediately this thread from the pool.
            {
                IGeoObject go = data.GeoObject as IGeoObject;
                lock (go) //To avoid starting the computation of the display list of a geo obejct with a new precision before the computation with the old value is ended.
                {
                    if (!data.CancellationToken.IsCancellationRequested) //If canceled during waiting for the lock I end it immediately here.
                    {
                        // hier wird die eigentliche Arbeit gemacht
                        go.PrepareDisplayList(data.Precision);
                        lock (runningThreads)
                        {
                            runningThreads.Remove(data.RecalcID); // wirft keine Exception wenn nicht drin
                                                                  // recalcDone hat einen Wert, wenn DoBackgroundRecalcDisplayList alle Objekte in die
                                                                  // Queue gesteckt hat und auf das Ende wartet
                            if (recalcDone != null && runningThreads.Count == 0)
                            {   // wenn der letzte thread fertig ist, nachdem alle in der Liste stehen
                                // wird recalcDone "signalisiert", d.h. alle Objekte sind jetzt berechnet
                                // und eine neue Displayliste kann schnell hergestellt werden
                                recalcDone.Set();
                            }
                        }
                    }
                }
            }
        }

        private class RecalcGeoObjectData
        {
            IGeoObject _geoObject;
            double _precision;
            string _recalcID;
            CancellationToken _cancellationToken;


            public RecalcGeoObjectData(IGeoObject geoObject, double precision, CancellationToken cancellationToken)
            {
                _geoObject = geoObject;
                _precision = precision;
                _cancellationToken = cancellationToken;
                _recalcID = $"{geoObject.UniqueId}[{precision}]";
            }

            public IGeoObject GeoObject
            {
                get { return _geoObject; }
            }

            public double Precision
            {
                get { return _precision; }
            }

            public string RecalcID
            {
                get { return _recalcID; }
            }

            public CancellationToken CancellationToken
            {
                get { return _cancellationToken; }
            }
        }

        private void InsertRecalcGeoObject(IGeoObject toInsert, double precision, CancellationToken cancellationToken)
        {   // In die Queue von ThreadPool.QueueUserWorkItem einfügen.
            // Gleichzeitig runningThreads aktuell halten.
            // Hier kann man noch überlegen, wie man mit Blöcken, Blockreferenzen
            // Schraffuren, Bemaßungen umgehen soll. 3D Objekte werden auf Faces heruntergebrochen
            // da oft nur ein einziges Solid mit vielen Faces ein Modell ausmacht
            if (toInsert is Solid solid)
            {
                for (int i = 0; i < solid.Shells.Length; ++i)
                {
                    if (cancellationToken.IsCancellationRequested) //Stops queueing if the background thread was aborted.
                    {
                        break;
                    }
                    InsertRecalcGeoObject(solid.Shells[i], precision, cancellationToken);
                }
            }
            else if (toInsert is Shell shell)
            {
                for (int i = 0; i < shell.Faces.Length; ++i)
                {
                    if (cancellationToken.IsCancellationRequested) //Stops queueing if the background thread was aborted.
                    {
                        break;
                    }
                    InsertRecalcGeoObject(shell.Faces[i], precision, cancellationToken);
                }
            }
            else if (toInsert is Block block)
            {
                for (int i = 0; i < block.NumChildren; ++i)
                {
                    if (cancellationToken.IsCancellationRequested) //Stops queueing if the background thread was aborted.
                    {
                        break;
                    }
                    InsertRecalcGeoObject(block.Child(i), precision, cancellationToken);
                }
            }
            else
            {
                if (!cancellationToken.IsCancellationRequested) //Dont queue if the background thread was aborted.
                {
                    lock (runningThreads)
                    {
                        RecalcGeoObjectData rcod = new RecalcGeoObjectData(toInsert, precision, cancellationToken);
                        runningThreads.Add(rcod.RecalcID);
                        ThreadPool.QueueUserWorkItem(new WaitCallback(RecalcGeoObject), rcod);
                    }
                }
            }
        }

        private class DoBackgroundRecalcDisplayListData
        {
            public double Precision;
            public CancellationToken CancellationToken;
        }

        internal void DoBackgroundRecalcDisplayList(object parameter)
        {   // wird von RecalcDisplayLists  in einem neuen Thread aufgerufen.
            // Die Aufgabe dieser Methode, die ja schon im Hintergrund läüft, ist es für jedes GeoObject (rekursiv)
            // einen Thread zu starten, der die Auflösung berechnet. Die Threads werden mit dem ThreadPool
            // in einer Warteschlange verwaltet. Parallel dazu gibt es die runningThreads, worin die gequeueten
            // Threads eingetragen sind und sie sich selbst alle wieder austragen. Wenn der letzte sich
            // austrägt geht es hier weiter
            DoBackgroundRecalcDisplayListData data = (DoBackgroundRecalcDisplayListData)parameter;
            if (runningThreads == null) runningThreads = new HashSet<string>();
            backgroundRecalcPrecision = data.Precision;
            for (int i = 0; i < geoObjects.Count; ++i)
            {
                if (data.CancellationToken.IsCancellationRequested)
                {
                    break;
                }
                // Beim zufügen und entfernen muss diese Aktion abgebrochen werden, damit man nicht über den Index hinausgeht
                // dafür sorgt das Modell bei Add und Remove
                InsertRecalcGeoObject(geoObjects[i], data.Precision, data.CancellationToken);
            }
            //System.Diagnostics.Trace.WriteLine("Objekte eingefügt, Genauigkeit: " + precision.ToString());
            if (!data.CancellationToken.IsCancellationRequested) //Don't even start waiting the completition of the pool threads if the background computation was aborted during queueing.
            {
                lock (runningThreads)
                {   // wenn noch welche laufen, dann warten bis alle zu Ende sind
                    //System.Diagnostics.Trace.WriteLine("runningThreads.Count : " + runningThreads.Count.ToString());
                    if (runningThreads.Count > 0)
                    {
                        recalcDone = new EventWaitHandle(false, EventResetMode.ManualReset);
                        // jetzt muss der letzte Thread mit Set()
                        // das Ding wieder freigeben
                    }
                }
                if (recalcDone != null)
                {
                    //System.Diagnostics.Trace.WriteLine("vor recalcDone.WaitOne");
                    recalcDone.WaitOne(); // warte bis alle threads zu Ende sind
                                          //System.Diagnostics.Trace.WriteLine("nach recalcDone.WaitOne");
                }
                if (!data.CancellationToken.IsCancellationRequested) //If the background thread was aborted during waiting the pool threads, this precision was not computed til the end, ignore it.
                {
                    displayListPrecision = data.Precision;
                    displayListsDirty = true; // damits einen Repaint gibt
                                              // System.Diagnostics.Trace.WriteLine("NewDisplaylistAvailableEvent, Genauigkeit: " + precision.ToString());
                    if (NewDisplaylistAvailableEvent != null) NewDisplaylistAvailableEvent(this);
                    // die ProjectedModels können ein Invalidate machen
                    // es hat alles geklappt
                }
            }
            lock (runningThreads)
            {
                runningThreads.Clear(); // sollte hier eh immer leer sein
                cancellationTokenSource.Dispose();
                cancellationTokenSource = null;
                manageBackgroundRecalc = null; // dieser Thread, signalisiert dass noch eine Hintergrund Berechnung läuft
                recalcDone = null; // WaitEvent wird nicht mehr gebraucht, signalisiert dass die Queue fertig gefüllt ist
            }
        }

        internal void RecalcDisplayLists(IPaintTo3D paintTo3D)
        {   // wird vom Paint in ProjectedModel aufgerufen, wenn dieser "dirty" ist
            // kann aber nacheinander von mehreren ProjectedModels aufgerufen werden und soll nur einmal berechnet werden
            if (paintTo3D.Precision < displayListPrecision && !paintTo3D.DontRecalcTriangulation)
            {
                double recalcPrecision = paintTo3D.Precision / 2.0; // /2.0, damit es nicht sooft drankommt
                if (manageBackgroundRecalc != null)
                {
                    if (recalcPrecision >= backgroundRecalcPrecision) //If zooming a lot after a while the precision doesn't change anymore.
                    {
                        return;
                    }

                    // abbrechen und warten
                    Thread t = manageBackgroundRecalc;
                    if (t != null)
                    {
                        //System.Diagnostics.Trace.WriteLine("Background Task abgebrochen, Genauigkeit: " + paintTo3D.Precision.ToString());
                        AbortBackgroundRecalc();
                        t.Join(); // manageBackgroundRecalc kann hier drin null werden
                    }
                }
                cancellationTokenSource = new CancellationTokenSource();
                manageBackgroundRecalc = new Thread(new ParameterizedThreadStart(DoBackgroundRecalcDisplayList));
                manageBackgroundRecalc.Start(new DoBackgroundRecalcDisplayListData() { Precision = recalcPrecision, CancellationToken = cancellationTokenSource.Token }); // /2.0, damit es nicht sooft drankommt
                                                                                                                                                                          //System.Diagnostics.Trace.WriteLine("Background Task gestartet, Genauigkeit: " + paintTo3D.Precision.ToString());
            }
            if (displayListsDirty) // nur wenn kein BackgroundRecalc läuft
            {
                // System.Diagnostics.Trace.WriteLine("displayListsDirty, Genauigkeit: " + paintTo3D.Precision.ToString());
                if (displayListPrecision == 0.0 || (paintTo3D.Precision < displayListPrecision && !paintTo3D.DontRecalcTriangulation))
                {   // damit der Wert displayListPrecision zuverlässig gesetzt ist
                    displayListPrecision = paintTo3D.Precision;
                }
                try
                {
                    layerFaceDisplayList.Clear();
                    layerTransparentDisplayList.Clear();
                    layerCurveDisplayList.Clear();
                    layerUnscaledObjects.Clear();
                    for (int i = 0; i < geoObjects.Count; ++i)
                    {
                        if (geoObjects[i].IsVisible)
                        {
                            geoObjects[i].PrePaintTo3D(paintTo3D); // hier können noch Listen gemacht werden
                            geoObjects[i].PaintTo3DList(paintTo3D, this); // hier wird einsortiert
                        }
                    }
                    // jetzt sind layerFaceObjects und layerCurveObjects gefüllt und die Displaylisten müssen gemacht werden
                    paintTo3D.PaintFaces(PaintTo3D.PaintMode.FacesOnly);
                    foreach (KeyValuePair<Layer, List<IGeoObject>> kv in layerFaceObjects)
                    {
                        paintTo3D.OpenList("model-layerFaceObjects");
                        foreach (IGeoObject go in kv.Value)
                        {
                            go.PaintTo3D(paintTo3D); // hier können keine Listen gemacht werden
                        }
                        layerFaceDisplayList[kv.Key] = paintTo3D.CloseList();
                    }
                    foreach (KeyValuePair<Layer, List<IGeoObject>> kv in layerTransparentObjects)
                    {
                        paintTo3D.OpenList("model-layerTransparentObjects");
                        paintTo3D.Blending(true);
                        foreach (IGeoObject go in kv.Value)
                        {
                            go.PaintTo3D(paintTo3D); // hier können keine Listen gemacht werden
                        }
                        paintTo3D.Blending(false);
                        layerTransparentDisplayList[kv.Key] = paintTo3D.CloseList();
                    }

                    paintTo3D.PaintFaces(PaintTo3D.PaintMode.CurvesOnly);
                    foreach (KeyValuePair<Layer, List<IGeoObject>> kv in layerCurveObjects)
                    {
                        paintTo3D.OpenList("model-layerCurveObjects");
                        foreach (IGeoObject go in kv.Value)
                        {
                            go.PaintTo3D(paintTo3D);
                        }
                        layerCurveDisplayList[kv.Key] = paintTo3D.CloseList();
                    }
                    layerFaceObjects.Clear(); // die werden nicht mehr gebraucht
                    layerTransparentObjects.Clear();
                    layerCurveObjects.Clear();
                    displayListsDirty = false;
                }
                catch (PaintTo3DOutOfMemory)
                {
                    // hier wirklich Collect aufrufen, damit die OpenGL Listen freigegeben werden
                    layerFaceObjects.Clear(); // die werden nicht mehr gebraucht
                    layerTransparentObjects.Clear();
                    layerCurveObjects.Clear();
                    System.GC.Collect();
                    System.GC.WaitForPendingFinalizers(); // nicht entfernen! kein Debug
                    paintTo3D.FreeUnusedLists();
                    // RecalcDisplayLists(paintTo3D); // nochmal wiederholen, und was wenns da auch nicht geht???
                    // nicht wiederholen, einfach mit den unvollständigen Listen weitermachen
                }
                // ein völlig neuer OctTree wird hier erzeugt. Das sollte genau dann passieren
                // wenn eine neue Auflösung berechnet wurde. z.Z. passiert das nur beim ersten mal
                if (octTree == null)
                {
                    int tcstart = Environment.TickCount;
                    if (!extent.HasValue || extent.Value.IsEmpty)
                    {
                        extent = new BoundingCube(GeoPoint.Origin, 100); // Notlösung, wie machen das die anderen?
                    }

                    octTree = new OctTree<IGeoObject>(Extent, displayListPrecision);
                    //for (int i = 0; i < geoObjects.Count; ++i)
                    //{
                    //    AddOctreeObjects(geoObjects[i], octTree);
                    //}
                    try
                    {
                        Parallel.For(0, geoObjects.Count, i =>
                        {
                            AddOctreeObjectsParallel(geoObjects[i], octTree);
                        });
                    }
                    catch (NullReferenceException)
                    {   // mysterious exception when opening a simple dxf file from the recent files menu
                        octTree = new OctTree<IGeoObject>(Extent, displayListPrecision);
                        for (int i = 0; i < geoObjects.Count; ++i)
                        {
                            AddOctreeObjects(geoObjects[i], octTree);
                        }
                    }
                    int time = Environment.TickCount - tcstart;
                }
            }
        }

        private void AbortBackgroundRecalc()
        {
            if (cancellationTokenSource != null)
            {
                cancellationTokenSource.Cancel();
            }
            if (recalcDone != null)
            {
                recalcDone.Set();
            }
        }

        private void AddOctreeObjects(IGeoObject go, OctTree<IGeoObject> octTree)
        {   // hier werden die EInzelteile eingehängt, damit das Picken von Face oder Edge
            // schnell geht. Oft hat man ja nur ein einziges solid mit vielen Faces
            if (octTree == null || go == null) return;
            IGeoObject[] subEntities = go.OwnedItems;
            if (subEntities != null && subEntities.Length > 0)
            {
                for (int i = 0; i < subEntities.Length; ++i)
                {
                    AddOctreeObjects(subEntities[i], octTree);
                }
            }
            else if (go is Face && !(go.Owner is Shell))
            {   // Faces, die nicht Teile von Shells sind liefern bei OwnedItems nichts, da sie
                // nicht sich selbst und die edges liefern können, da es sonst eine endlose Recursion gibt.
                // deshalb hier als Sonderfall behandelt
                octTree.AddObject(go);
                Edge[] edges = (go as Face).AllEdges;
                for (int i = 0; i < edges.Length; ++i)
                {
                    if (edges[i].Curve3D != null)
                    {
                        octTree.AddObject(edges[i].Curve3D as IGeoObject);
                    }
                }
            }
            else
            {
                octTree.AddObject(go);
            }
        }
        private void AddOctreeObjectsParallel(IGeoObject go, OctTree<IGeoObject> octTree)
        {   // hier werden die Einzelteile eingehängt, damit das Picken von Face oder Edge
            // schnell geht. Oft hat man ja nur ein einziges solid mit vielen Faces
            if (octTree == null) return;
            IGeoObject[] subEntities = go.OwnedItems;
            if (subEntities != null && subEntities.Length > 0)
            {   // we do this parallel, because adding a complex solid, which might be the only object beeing added should work in parallel mode
                Parallel.For(0, subEntities.Length, i =>
                 {
                     AddOctreeObjectsParallel(subEntities[i], octTree);
                 });
            }
            else if (go is Face && !(go.Owner is Shell))
            {   // Faces, die nicht Teile von Shells sind liefern bei OwnedItems nichts, da sie
                // nicht sich selbst und die edges liefern können, da es sonst eine endlose Recursion gibt.
                // deshalb hier als Sonderfall behandelt
                octTree.AddObjectAsync(go);
                Edge[] edges = (go as Face).AllEdges;
                for (int i = 0; i < edges.Length; ++i)
                {
                    if (edges[i].Curve3D != null)
                    {
                        octTree.AddObjectAsync(edges[i].Curve3D as IGeoObject);
                    }
                }
            }
            else
            {
                octTree.AddObjectAsync(go);
            }
        }
        private void RemoveOctreeObjects(IGeoObject go, OctTree<IGeoObject> octTree)
        {   // hier werden die EInzelteile eingehängt, damit das Picken von Face oder Edge
            // schnell geht. Oft hat man ja nur ein einziges solid mit vielen Faces
            if (octTree == null) return;
            IGeoObject[] subEntities = go.OwnedItems;
            if (subEntities != null)
            {
                for (int i = 0; i < subEntities.Length; ++i)
                {
                    RemoveOctreeObjects(subEntities[i], octTree);
                }
            }
            else if (go is Face && !(go.Owner is Shell))
            {   // Faces, die nicht Teile von Shells sind liefern bei OwnedItems nichts, da sie
                // nicht sich selbst und die edges liefern können, da es sonst eine endlose Recursion gibt.
                // deshalb hier als Sonderfall behandelt
                octTree.RemoveObject(go);
                Edge[] edges = (go as Face).AllEdges;
                for (int i = 0; i < edges.Length; ++i)
                {
                    if (edges[i].Curve3D != null)
                    {
                        octTree.RemoveObject(edges[i].Curve3D as IGeoObject);
                    }
                }
            }
            else
            {
                octTree.RemoveObject(go);
            }
        }
        internal void InitOctTree()
        {
            if (octTree == null)
            {
                if (!extent.HasValue || extent.Value.IsEmpty)
                {
                    extent = new BoundingCube(GeoPoint.Origin, 100); // Notlösung, wie machen das die anderen?
                }

                octTree = new OctTree<IGeoObject>(Extent, displayListPrecision);
                Parallel.For(0, geoObjects.Count, i =>
                {
                    AddOctreeObjectsParallel(geoObjects[i], octTree);
                });
            }
        }
        /// <summary>
        /// Delegate definition for the <see cref="ImportingObjectsEvent"/>. The handler may modify <paramref name="importedObjects"/>
        /// </summary>
        /// <param name="importedObjects">List of objects beeing imported</param>
        /// <param name="fileName">Name of imported file</param>
        public delegate void ImportingObjectsDelegate(GeoObjectList importedObjects, string fileName);
        /// <summary>
        /// Delegate definition for the <see cref="AddingGeoObjectEvent"/>. Setting <paramref name="cancel"/> to true prevents
        /// the object beeing added to the model.
        /// </summary>
        /// <param name="go">Object to be added</param>
        /// <param name="cancel">Parameter to prevent the adding of the object</param>
        public delegate void AddingGeoObject(IGeoObject go, ref bool cancel);
        /// <summary>
        /// Delegate definition for the <see cref="RemovingGeoObjectEvent"/>. Setting <paramref name="cancel"/> to true prevents
        /// the object beeing removed from the model.
        /// </summary>
        /// <param name="go">Object to be added</param>
        /// <param name="cancel">Parameter to prevent the adding of the object</param>
        public delegate void RemovingGeoObject(IGeoObject go, ref bool cancel);
        /// <summary>
        /// Delegate definition for the <see cref="GeoObjectAddedEvent"/>.
        /// </summary>
        /// <param name="go">GeoObject the has been added to the model</param>
        public delegate void GeoObjectAdded(IGeoObject go);
        /// <summary>
        /// Delegate definition for the <see cref="GeoObjectRemovedEvent"/>.
        /// </summary>
        /// <param name="go">GeoObject that hes been removed from the model</param>
        public delegate void GeoObjectRemoved(IGeoObject go);
        /// <summary>
        /// Delegate definition for the <see cref="AddingGeoObjectsEvent"/>.
        /// </summary>
        /// <param name="go">Array of objects beeing added</param>
        public delegate void AddingGeoObjects(IGeoObject[] go);
        /// <summary>
        /// Delegate definition for the <see cref="RemovingGeoObjectsEvent"/>.
        /// </summary>
        /// <param name="go">Array of objects beeing removed</param>
        public delegate void RemovingGeoObjects(IGeoObject[] go);
        /// <summary>
        /// Delegate definition for the <see cref="GeoObjectsAddedEvent"/>.
        /// </summary>
        /// <param name="go">Array of objects that have been added</param>
        public delegate void GeoObjectsAdded(IGeoObject[] go);
        /// <summary>
        /// Delegate definition for the <see cref="GeoObjectsRemovedEvent"/>.
        /// </summary>
        /// <param name="go">Array of objects that have been removed</param>
        public delegate void GeoObjectsRemoved(IGeoObject[] go);
        /// <summary>
        /// Delegate definition for the <see cref="ExtentChangedEvent"/>.
        /// </summary>
        /// <param name="newExtent">The new extend of the model</param>
        public delegate void ExtentChangedDelegate(BoundingCube newExtent);
        /// <summary>
        /// Delegate definition for the <see cref="NameChangedEvent"/>.
        /// </summary>
        /// <param name="sender">Model which name has changed</param>
        /// <param name="newName">new name of the model</param>
        public delegate void NameChangedDelegate(Model sender, string newName);
        internal delegate void NewDisplaylistAvailableDelegate(Model sender);
        /// <summary>
        /// This event will be fired when objects imported from another file are added to the model.
        /// The event handler can modify the list of objects, e.g. add or remove objects or combine the objects to a <see cref="Block"/>.
        /// </summary>
        public event ImportingObjectsDelegate ImportingObjectsEvent;
        /// <summary>
        /// This event will be fired before an object is added to this model. The event handler can prevent the object beeing added to the model
        /// </summary>
        public event AddingGeoObject AddingGeoObjectEvent;
        /// <summary>
        /// This event will be fired before an object is removed from this model. The event handler can prevent the object beeing added to the model
        /// </summary>
        public event RemovingGeoObject RemovingGeoObjectEvent;
        /// <summary>
        /// This event will be fired when an object is added to this model.
        /// </summary>
        public event GeoObjectAdded GeoObjectAddedEvent;
        /// <summary>
        /// This event will be fired before multiple objects are beeing added. There is no sense in modifying
        /// the provided array of objects. There will be an additional call to <see cref="AddingGeoObjectEvent"/> and <see cref="GeoObjectAddedEvent"/>
        /// for each objects (where you can prevent the object beeing added) and a final call to <see cref="GeoObjectsAddedEvent"/>.
        /// </summary>
        public event AddingGeoObjects AddingGeoObjectsEvent;
        /// <summary>
        /// This event will be fired before multiple objects are beeing removed. There is no sense in modifying
        /// the provided array of objects. There will be an additional call to <see cref="RemovingGeoObjectEvent"/> and <see cref="GeoObjectRemovedEvent"/>
        /// for each objects (where you can prevent the object beeing added) and a final call to <see cref="GeoObjectsRemovedEvent"/>.
        /// </summary>
        public event RemovingGeoObjects RemovingGeoObjectsEvent;
        /// <summary>
        /// This event will be fired after multiple GeoObjects have been added, <see cref="AddingGeoObjectsEvent"/>
        /// has been fired before.
        /// </summary>
        public event GeoObjectsAdded GeoObjectsAddedEvent;
        /// <summary>
        /// This event will be fired when an object is removed from this model.
        /// </summary>
        public event GeoObjectRemoved GeoObjectRemovedEvent;
        /// <summary>
        /// This event will be fired when removing multiple objects from this model is finished.
        /// </summary>
        public event GeoObjectsRemoved GeoObjectsRemovedEvent;
        /// <summary>
        /// This event will be fired when an object contained in this model is about to change.
        /// </summary>
        public event ChangeDelegate GeoObjectWillChangeEvent;
        /// <summary>
        /// This event will be fired when an object contained in this model did change.
        /// </summary>
        public event ChangeDelegate GeoObjectDidChangeEvent;
        /// <summary>
        /// This event will be fired when objects have been added, removed or modified which resulted
        /// in a different extent (of the bounding box) of the model.
        /// </summary>
        public event ExtentChangedDelegate ExtentChangedEvent;
        /// <summary>
        /// This event will be fired when the name of the model changed.
        /// </summary>
        public event NameChangedDelegate NameChangedEvent;
        internal event NewDisplaylistAvailableDelegate NewDisplaylistAvailableEvent;
        /// <summary>
        /// Creates an empty model.
        /// </summary>
        public Model()
        {
            geoObjects = new GeoObjectList();
            name = StringTable.GetString("Model.DefaultName");
            unit = Units.millimeter;
            defaultScale = 1.0;
            lineStyleScale = 1.0;
            projectionToExtent = new Hashtable();
            base.resourceIdInternal = "ModelName";
            extent = null;
            layerFaceDisplayList = new LayerToDisplayListDictionary();
            layerTransparentDisplayList = new LayerToDisplayListDictionary();
            layerCurveDisplayList = new LayerToDisplayListDictionary();
            layerUnscaledObjects = new Dictionary<Layer, GeoObjectList>();
            layerFaceObjects = new Dictionary<Layer, List<IGeoObject>>();
            layerTransparentObjects = new Dictionary<Layer, List<IGeoObject>>();
            layerCurveObjects = new Dictionary<Layer, List<IGeoObject>>();

            nullLayer = new Layer("NullLayer");
            displayListsDirty = true;
            minExtend = BoundingCube.EmptyBoundingCube;
            noSingleAddEvents = false;
            showTransparentFaceEdges = Settings.GlobalSettings.GetBoolValue("Display.ShowTransparentFaceEdges", false);
        }
        ~Model()
        {
        }
        /// <summary>
        /// The name of the model.
        /// </summary>
        public string Name
        {
            get
            {
                return name;
            }
            set
            {
                name = value;
            }
        }
        public UserData UserData
        {
            get
            {
                if (userData == null) userData = new UserData();
                return userData;
            }
        }
        /// <summary>
        /// Set to true, if you don't want <see cref="AddingGeoObjectEvent"/> rsp. <see cref="GeoObjectAddedEvent"/> beeing fired
        /// after <see cref="AddingGeoObjectsEvent"/> rsp. <see cref="GeoObjectsAddedEvent"/> has been called. False is the default value.
        /// </summary>
        public bool NoSingleAddEvents
        {
            set
            {
                noSingleAddEvents = value;
            }
        }
        private UndoRedoSystem undoRedoSystem;
        /// <summary>
        /// Gets or sets the undo-system. Usually this is the undosystem of the <see cref="Project"/>
        /// containing this model.
        /// </summary>
        public UndoRedoSystem Undo
        {
            get
            {
                if (undoRedoSystem == null) undoRedoSystem = new UndoRedoSystem(); // sollte nicht drankommen
                                                                                   // es soll immer eines geben, damit das mit dem using einfach zu schreiben ist
                return undoRedoSystem;
            }
            set
            {
                if (undoRedoSystem != null)
                {
                    undoRedoSystem.BeginContinousChangesEvent -= new UndoRedoSystem.BeginContinousChangesDelegate(OnBeginContinousChanges);
                    undoRedoSystem.EndContinousChangesEvent -= new UndoRedoSystem.EndContinousChangesDelegate(OnEndContinousChanges);
                }
                undoRedoSystem = value;
                if (undoRedoSystem != null)
                {
                    undoRedoSystem.BeginContinousChangesEvent += new UndoRedoSystem.BeginContinousChangesDelegate(OnBeginContinousChanges);
                    undoRedoSystem.EndContinousChangesEvent += new UndoRedoSystem.EndContinousChangesDelegate(OnEndContinousChanges);
                }
            }
        }
        /// <summary>
        /// Access to the <see cref="OctTree"/> containing all geometrical objects of the model. Do not modify the octTree to avoid inconsistencies
        /// between model and octTree. Use the octTree for fast access to the objects in the model from geometrical constraints.
        /// </summary>
        public OctTree<IGeoObject> OctTree
        {
            get
            {
                return octTree;
            }
        }
        void OnEndContinousChanges(object source)
        {
            // System.Diagnostics.Trace.WriteLine("OnEndContinousChanges " + source.ToString());
            if (continousChanges != null)
            {
                foreach (IGeoObject go in continousChanges)
                {
                    // System.Diagnostics.Trace.WriteLine(go.ToString());
                    AddOctreeObjects(go, octTree);
                }
            }
            continousChanges = null;
        }
        void OnBeginContinousChanges(object source)
        {
            // System.Diagnostics.Trace.WriteLine("OnBeginContinousChanges " + source.ToString());
            continousChanges = new Set<IGeoObject>();
        }

        /// <summary>
        /// Unites the given solid with all other solids in the model. If there are no
        /// other solids or the solid is disjoint with all other solids this solid will simply bee added.
        /// </summary>
        /// <param name="solid">the solid to unite</param>
        /// <param name="onlySameLayer">if true: check only with solids on the same layer</param>
        public bool UniteSolid(Solid solid, bool onlySameLayer)
        {
            bool res = false;
            using (Undo.UndoFrame)
            {
                // TODO: besser nicht erst entfernen und dann wieder zufügen sondern wie bei RemoveSolid
                List<Solid> solids = new List<Solid>();
                for (int i = geoObjects.Count - 1; i >= 0; --i)
                {
                    if (geoObjects[i] is Solid)
                    {
                        solids.Add((geoObjects[i] as Solid));
                        Remove(geoObjects[i]);
                    }
                }
                while (solids.Count > 0)
                {
                    bool disjunct = true;
                    foreach (Solid s in solids)
                    {
                        Solid u = Make3D.Union(s, solid);
                        if (u != null)
                        {
                            solids.Remove(s);
                            solid = u;
                            disjunct = false;
                            res = true; // es gab eine vereinigung
                            break;
                        }
                    }
                    if (disjunct) break;
                }
                Add(solid);
                foreach (Solid s in solids) Add(s);
            }
            return res;
        }
        /// <summary>
        /// Removes the given solid from all other solids in the model. If there are no solids in the model
        /// or this solid is disjoint with all other solids in the model there will be no effect.
        /// </summary>
        /// <param name="solid">the solid to remove</param>
        /// <param name="onlySameLayer">if true: check only with solids on the same layer</param>
        public bool RemoveSolid(Solid solid, bool onlySameLayer)
        {
            bool res = false;
            using (Undo.UndoFrame)
            {
                List<Solid> solids = new List<Solid>();
                List<Solid> toAdd = new List<Solid>();
                for (int i = geoObjects.Count - 1; i >= 0; --i)
                {
                    if (geoObjects[i] is Solid)
                    {
                        solids.Add((geoObjects[i] as Solid));
                    }
                }
                for (int i = solids.Count - 1; i >= 0; --i)
                {
                    Solid[] u = Make3D.Difference(solids[i], solid);
                    if (u.Length > 0)
                    {
                        toAdd.AddRange(u);
                        res = true; // es hat zumindest einmal geklappt
                    }
                    else
                    {
                        solids.RemoveAt(i);
                    }
                }
                foreach (Solid s in solids) Remove(s);
                foreach (Solid s in toAdd) Add(s);
            }
            return res;
        }
        public void MoveToFront(IGeoObject iGeoObject)
        {
            geoObjects.MoveToFront(iGeoObject);
        }
        public void MoveToBack(IGeoObject iGeoObject)
        {
            geoObjects.MoveToBack(iGeoObject);
        }
        /// <summary>
        /// Adds an object to this model. If the model is beeing displayed the object will appear in the view
        /// immediately.
        /// </summary>
        /// <param name="ObjectToAdd">The GeoObject to add</param>
        public void Add(IGeoObject ObjectToAdd)
        {
            if (!ObjectToAdd.HasValidData()) return; // die Objekte überprüfen sich hier selbst
            bool cancel = false;
            if (AddingGeoObjectEvent != null) AddingGeoObjectEvent(ObjectToAdd, ref cancel);
            if (cancel) return; // der Anwender hat was dagegen
            AbortBackgroundRecalc(); // wenn gerade im Hintergrund was läüft, abbrechen
                                     // da der Zugriff auf die Liste geoObjects nicht gewährleistst ist
            if (geoObjects.Count == 0) extent = null; // damit der Extent, der von -100 bis 100 geht neu berechnet wird
            if (ExtentChangedEvent != null)
            {
                BoundingCube extn = ObjectToAdd.GetBoundingCube();
                if (geoObjects.Count == 0)
                {
                    extent = extn;
                    ExtentChangedEvent(extn);
                }
                else
                {
                    BoundingCube ext = this.Extent;
                    if (!ext.Contains(extn))
                    {
                        ext.MinMax(extn);
                        extent = ext;
                        ExtentChangedEvent(extent.Value);
                    }
                }
            }
            Undo.AddUndoStep(new ReversibleChange(this, "Remove", ObjectToAdd));
            if (ObjectToAdd.Owner != null) ObjectToAdd.Owner.Remove(ObjectToAdd);
            geoObjects.Add(ObjectToAdd);
            ObjectToAdd.Owner = this;
            if (GeoObjectAddedEvent != null) GeoObjectAddedEvent(ObjectToAdd);
            ObjectToAdd.WillChangeEvent += new ChangeDelegate(OnGeoObjectWillChange);
            ObjectToAdd.DidChangeEvent += new ChangeDelegate(OnGeoObjectDidChange);
            if (ExtentChangedEvent == null) extent = null;
            projectionToExtent.Clear();
            displayListsDirty = true;
            if (octTree != null) AddOctreeObjects(ObjectToAdd, octTree);
        }
        /// <summary>
        /// Adds multiple GeoObjects to this model.
        /// </summary>
        /// <param name="ListToAdd">List of the GeoObjects to add</param>
        public void Add(GeoObjectList ListToAdd)
        {
            if (ListToAdd.Count == 0) return;
            if (geoObjects.Count == 0) extent = null;
            if (ExtentChangedEvent != null)
            {
                BoundingCube extn = ListToAdd.GetExtent();
                if (geoObjects.Count == 0)
                {
                    extent = extn;
                    ExtentChangedEvent(extn);
                }
                else
                {
                    BoundingCube ext = this.Extent;
                    if (!ext.Contains(extn))
                    {
                        ext.MinMax(extn);
                        extent = ext;
                        ExtentChangedEvent(extent.Value);
                    }
                }
            }
            Undo.AddUndoStep(new ReversibleChange(this, "Remove", new object[] { ListToAdd.Clone() }));
            if (AddingGeoObjectsEvent != null) AddingGeoObjectsEvent(ListToAdd);
            if (octTree != null && octTree.IsEmpty && extent != null) octTree = new OctTree<IGeoObject>(extent.Value, displayListPrecision);
            for (int i = 0; i < ListToAdd.Count; ++i)
            {   // hier z.Z. keine einzelnen Events, evtl. einstellbar wg.PFOCAD
                IGeoObject go = ListToAdd[i];
                if (go.Owner != null) go.Owner.Remove(go);
                geoObjects.Add(go);
                go.Owner = this;
                if (!noSingleAddEvents && GeoObjectAddedEvent != null) GeoObjectAddedEvent(go);
                go.WillChangeEvent += new ChangeDelegate(OnGeoObjectWillChange);
                go.DidChangeEvent += new ChangeDelegate(OnGeoObjectDidChange);
                if (ExtentChangedEvent == null) extent = null;
                if (octTree != null) AddOctreeObjects(go, octTree);
            }
            // Add(go);
            projectionToExtent.Clear();
            displayListsDirty = true;
            if (GeoObjectsAddedEvent != null) GeoObjectsAddedEvent(ListToAdd);
        }
        /// <summary>
        /// Adds multiple GeoObjects to this model.
        /// </summary>
        /// <param name="ListToAdd">Array of the GeoObjects to add</param>
        public void Add(IGeoObject[] ListToAdd)
        {
            Add(new GeoObjectList(ListToAdd));
        }
        internal void Import(GeoObjectList importedObjects, string fileName)
        {
            if (ImportingObjectsEvent != null) ImportingObjectsEvent(importedObjects, fileName);
            // folgende Schleife wäre unnötig, wenn Face.Hittest auch ohne triangulierung ordentlich funktionieren würde
            // ist aber z.B. bei "1362241.stp" nicht der Fall
            for (int i = 0; i < importedObjects.Count; ++i)
            {
                if (importedObjects[i] is Solid)
                {
                    (importedObjects[i] as Solid).PreCalcTriangulation(displayListPrecision);
                }
                else if (importedObjects[i] is Shell)
                {
                    (importedObjects[i] as Shell).PreCalcTriangulation(displayListPrecision);
                }
                else if (importedObjects[i] is Face)
                {
                    (importedObjects[i] as Face).PreCalcTriangulation(displayListPrecision);
                }
            }
            Add(importedObjects);
        }
        /// <summary>
        /// Returns the count of GeoObjects in this model.
        /// </summary>
        public int Count
        {
            get
            {
                return geoObjects.Count;
            }
        }
        /// <summary>
        /// Removes an array of GeoObjects from this model. Objects that dont belong to the model will be ignored.
        /// </summary>
        /// <param name="ToRemove">Array of geoObjects to remove</param>
        public void Remove(IGeoObject[] ToRemove)
        {
            using (Undo.UndoFrame)
            {
                if (RemovingGeoObjectsEvent != null) RemovingGeoObjectsEvent(ToRemove);
                for (int i = ToRemove.Length - 1; i >= 0; i--)
                {
                    Remove(ToRemove[i]);
                }
                if (GeoObjectsRemovedEvent != null) GeoObjectsRemovedEvent(ToRemove);
            }
        }
        /// <summary>
        /// Removes a list of GeoObjects from this model. Objects that dont belong to the model will be ignored.
        /// </summary>
        /// <param name="ToRemove">List of GeoObjects to be removed</param>
        public void Remove(GeoObjectList ToRemove)
        {
            // den Aufruf von RemovingGeoObjectsEvent an den Anfang gesetzt, damit man darin einen UndoFrame machen kann
            // den man bei GeoObjectsRemovedEvent wieder zu macht. Ob das stört?
            if (RemovingGeoObjectsEvent != null) RemovingGeoObjectsEvent(ToRemove);
            Undo.AddUndoStep(new ReversibleChange(this, "Add", new object[] { ToRemove.Clone() }));
            // kein Undoframe, denn Remove wird selbst von Undo aufgerufen
            for (int i = ToRemove.Count - 1; i >= 0; i--)
            {
                RemoveNoUndo(ToRemove[i]);
            }
            if (GeoObjectsRemovedEvent != null) GeoObjectsRemovedEvent(ToRemove);
        }
        public void Remove(HashSet<IGeoObject> HToRemove)
        {
            AbortBackgroundRecalc(); // wenn gerade im Hintergrund was läüft, abbrechen
            using (Undo.UndoFrame)
            {
                List<IGeoObject> lToRemove = new List<IGeoObject>();
                foreach (IGeoObject go in HToRemove)
                {
                    lToRemove.Add(go);
                }
                GeoObjectList ToRemove = new GeoObjectList(lToRemove);
                Undo.AddUndoStep(new ReversibleChange(this, "Add", ToRemove));
                if (RemovingGeoObjectsEvent != null) RemovingGeoObjectsEvent(ToRemove);
                for (int i = geoObjects.Count - 1; i >= 0; --i)
                {
                    if (HToRemove.Contains(geoObjects[i]))
                    {
                        geoObjects[i].Owner = null;
                        geoObjects[i].WillChangeEvent -= new ChangeDelegate(OnGeoObjectWillChange);
                        geoObjects[i].DidChangeEvent -= new ChangeDelegate(OnGeoObjectDidChange);
                        if (octTree != null) RemoveOctreeObjects(geoObjects[i], octTree);

                        geoObjects.Remove(i);
                    }
                }
                projectionToExtent.Clear();
                displayListsDirty = true;
                if (GeoObjectsRemovedEvent != null) GeoObjectsRemovedEvent(ToRemove);
            }
        }

        /// <summary>
        /// Removes a GeoObject from the model. If the GeoObject doesnt belong to this model it will be ignored.
        /// </summary>
        /// <param name="ToRemove">GeoObject to remove</param>
        public void Remove(IGeoObject ToRemove)
        {
            AbortBackgroundRecalc(); // wenn gerade im Hintergrund was läüft, abbrechen
            bool cancel = false;
            if (RemovingGeoObjectEvent != null) RemovingGeoObjectEvent(ToRemove, ref cancel);
            if (cancel) return;
            Undo.AddUndoStep(new ReversibleChange(this, "Add", ToRemove));
            int ind = geoObjects.IndexOf(ToRemove);
            if (ind >= 0)
            {
                geoObjects.Remove(ind);
                ToRemove.Owner = null;
            }
            ToRemove.WillChangeEvent -= new ChangeDelegate(OnGeoObjectWillChange);
            ToRemove.DidChangeEvent -= new ChangeDelegate(OnGeoObjectDidChange);
            if (GeoObjectRemovedEvent != null) GeoObjectRemovedEvent(ToRemove);
            projectionToExtent.Clear();
            displayListsDirty = true;
            if (octTree != null) RemoveOctreeObjects(ToRemove, octTree);
        }
        /// <summary>
        /// Remove all objects from this model.
        /// </summary>
        public void RemoveAll()
        {
            using (Undo.UndoFrame)
            {
                for (int i = geoObjects.Count - 1; i >= 0; i--)
                {
                    Remove(geoObjects[i]);
                }
            }
        }
        private void RemoveNoUndo(IGeoObject ToRemove)
        {
            AbortBackgroundRecalc(); // wenn gerade im Hintergrund was läüft, abbrechen
            bool cancel = false;
            if (RemovingGeoObjectEvent != null) RemovingGeoObjectEvent(ToRemove, ref cancel);
            if (cancel) return;
            int ind = geoObjects.IndexOf(ToRemove);
            if (ind >= 0)
            {
                geoObjects.Remove(ind);
                ToRemove.Owner = null;
            }
            ToRemove.WillChangeEvent -= new ChangeDelegate(OnGeoObjectWillChange);
            ToRemove.DidChangeEvent -= new ChangeDelegate(OnGeoObjectDidChange);
            if (GeoObjectRemovedEvent != null) GeoObjectRemovedEvent(ToRemove);
            projectionToExtent.Clear();
            displayListsDirty = true;
            if (octTree != null) RemoveOctreeObjects(ToRemove, octTree);
        }
        /// <summary>
        /// Indexer to access the GeoObjects owned by this model by index.
        /// </summary>
        /// <param name="Index">Index of the required GeoObject</param>
        /// <returns></returns>
        public IGeoObject this[int Index]
        {
            get { return geoObjects[Index]; }
        }
        /// <summary>
        /// Returns a list of all objects owned by this model. Removing or adding objects from or to the returned list doesn't remove or add
        /// the objects from or to the model.
        /// </summary>
        public GeoObjectList AllObjects
        {
            get { return geoObjects.Clone(); }
        }
        /// <summary>
        /// Returns the extent of the model, i.e. a bounding cube enclosing all objects.
        /// </summary>
        public BoundingCube Extent
        {
            get
            {
                lock (extentLock)
                {
                    if (extent == null || !extent.HasValue || extent.Value.IsEmpty)
                    {
                        extent = CalculateExtent();
                        if (extent.Value.IsEmpty) extent = new BoundingCube(GeoPoint.Origin, 100); // damites auch bei leeren Modellen einen Wert gibt
                    }
                }
                return extent.Value;
            }
            set
            {
                lock (extentLock)
                {
                    extent = value;
                }
            }
        }
        /// <summary>
        /// A minimal extend of the Model. This will be the extend of the Model if the Model is empty.
        /// This is also used in other circumstances (e.g. dieplay of the grid)
        /// </summary>
        public BoundingCube MinExtend
        {
            get
            {
                return minExtend;
            }
            set
            {
                minExtend = value;
            }
        }
        /// <summary>
        /// Returns a 2 dimensional bounding rctangle enclosing the projection of all GeoObjects of the model.
        /// </summary>
        /// <param name="pr">The projection to be applied to the GeoObjects</param>
        /// <returns></returns>
        public BoundingRect GetExtent(Projection pr)
        {
            if (projectionToExtent.ContainsKey(pr)) return (BoundingRect)projectionToExtent[pr];
            BoundingRect res = BoundingRect.EmptyBoundingRect;
            foreach (IGeoObject go in geoObjects)
            {
                res.MinMax(go.GetExtent(pr, ExtentPrecision.Raw));
            }
            if (res.IsEmpty())
            {
                res = new BoundingRect(0, 0, 100, 100);
            }
            projectionToExtent[pr] = res;
            return res;
        }
        /// <summary>
        /// Delegate definition for <see cref="CalculateExtentForZoomTotalEvent"/>. If handeled must return the
        /// extent which is considered as the zoom total extent.
        /// Standard implementation will return <c>this.Extent.Project(pr)</c>
        /// </summary>
        /// <param name="m">The Model</param>
        /// <param name="pr">The Projection</param>
        /// <returns>The extent projected to 2D</returns>
        public delegate BoundingRect CalculateExtentForZoomTotalDelegate(Model m, Projection pr);
        public event CalculateExtentForZoomTotalDelegate CalculateExtentForZoomTotalEvent;
        private void ParallelTriangulation(IGeoObject go, double precision)
        {
            if (go is Solid sld)
            {
                for (int i = 0; i < sld.Shells.Length; i++)
                {
                    ParallelTriangulation(sld.Shells[i], precision);
                }
            }
            else if (go is Shell shl)
            {
                Parallel.For(0, shl.Faces.Length, i =>
                {
                    try
                    {
                        shl.Faces[i].AssureTriangles(precision);
                    }
                    catch { }
                });
            }
            else if (go is Face fc)
            {
                try
                {
                    fc.AssureTriangles(precision);
                }
                catch { }
            }
            else if (go is Block blk)
            {
                Parallel.For(0, blk.NumChildren, i =>
                {
                    ParallelTriangulation(blk.Child(i), precision);
                });
            }
        }
        public void ParallelTriangulation(double precision)
        {
            Parallel.For(0, geoObjects.Count, i =>
            {
                ParallelTriangulation(geoObjects[i], precision);
            });
        }
        private void SerialTriangulation(IGeoObject go, double precision)
        {
            if (go is Solid sld)
            {
                for (int i = 0; i < sld.Shells.Length; i++)
                {
                    SerialTriangulation(sld.Shells[i], precision);
                }
            }
            else if (go is Shell shl)
            {
                for (int i = 0; i < shl.Faces.Length; ++i)
                {
                    try
                    {
                        shl.Faces[i].AssureTriangles(precision);
                    }
                    catch { }
                };
            }
            else if (go is Face fc)
            {
                try
                {
                    fc.AssureTriangles(precision);
                }
                catch { }
            }
            else if (go is Block blk)
            {
                for (int i = 0; i < blk.NumChildren; ++i)
                {
                    SerialTriangulation(blk.Child(i), precision);
                };
            }
        }
        public void SerialTriangulation(double precision)
        {
            for (int i = 0; i < geoObjects.Count; ++i)
            {
                SerialTriangulation(geoObjects[i], precision);
            };
        }
        internal BoundingRect GetExtentForZoomTotal(Projection pr)
        {
            if (CalculateExtentForZoomTotalEvent != null) return CalculateExtentForZoomTotalEvent(this, pr);
            BoundingRect zoomTo = GetExtent(pr);
            if (zoomTo.Width == 0 && zoomTo.Height == 0) zoomTo.Inflate(50); // if this is only a point or a line in direction of the projection, make the area bigger
            return zoomTo;
            // return Extent.Project(pr); war vorher so, warum?
        }
        /// <summary>
        /// Returns the list of all <see cref="IDrive">dirves</see> of this model. Only used in connection with <see cref="AnimatedView"/>.
        /// </summary>
        public DriveList AllDrives
        {
            get
            {
                if (allDrives == null) allDrives = new DriveList();
                return allDrives;
            }
        }
        /// <summary>
        /// Returns the list of all <see cref="Schedule"/>s of this model. Only used in connection with <see cref="AnimatedView"/>.
        /// </summary>
        public ScheduleList AllSchedules
        {
            get
            {
                if (allSchedules == null) allSchedules = new ScheduleList();
                return allSchedules;
            }
        }
        internal BoundingCube CalculateExtent()
        {   // muss noch mit cache versehen werden
            BoundingCube res = BoundingCube.EmptyBoundingCube;
            foreach (IGeoObject go in geoObjects)
            {
                res.MinMax(go.GetBoundingCube());
            }
            return res;
        }
        /// <summary>
        /// Determins whether an attribute (e.g. <see cref="Layer"/>, <see cref="LinePattern"/>) is used by any GeoObjects of this model.
        /// </summary>
        /// <param name="Attribute">Attribut to test</param>
        /// <returns>True, if attribute is used, false otherwise.</returns>
        public bool IsAttributeUsed(object Attribute)
        {
            for (int i = 0; i < geoObjects.Count; ++i)
            {
                if (geoObjects[i].IsAttributeUsed(Attribute)) return true;
            }
            return false;
        }
        #region ISerializable Members
        /// <summary>
        /// Constructor required by deserialization
        /// </summary>
        /// <param name="info">SerializationInfo</param>
        /// <param name="context">StreamingContext</param>
        protected Model(SerializationInfo info, StreamingContext context)
        {
            geoObjects = (GeoObjectList)info.GetValue("GeoObjectList", typeof(GeoObjectList));
            try
            {
                name = (string)info.GetValue("Name", typeof(string));
                unit = (Units)info.GetValue("Unit", typeof(Units));
                defaultScale = (double)info.GetValue("DefaultScale", typeof(double));
                lineStyleScale = (double)info.GetValue("LineStyleScale", typeof(double));
            }
            catch (SerializationException)
            {   // später hinzugefügt, also initialisieren
                name = StringTable.GetString("Model.DefaultName");
                unit = Units.millimeter;
                defaultScale = 1.0;
                lineStyleScale = 1.0;
            }
            try
            {
                minExtend = (BoundingCube)info.GetValue("MinExtend", typeof(BoundingCube));
            }
            catch (SerializationException)
            {   // später hinzugefügt, also initialisieren
                minExtend = BoundingCube.EmptyBoundingCube;
            }
            try
            {
                allDrives = info.GetValue("AllDrives", typeof(DriveList)) as DriveList; ;
            }
            catch (SerializationException)
            {
                allDrives = new DriveList();
            }
            try
            {
                allSchedules = info.GetValue("AllSchedules", typeof(ScheduleList)) as ScheduleList; ;
            }
            catch (SerializationException)
            {
                allSchedules = new ScheduleList();
            }

            projectionToExtent = new Hashtable();
            displayListsDirty = true;
            layerFaceDisplayList = new LayerToDisplayListDictionary();
            layerTransparentDisplayList = new LayerToDisplayListDictionary();
            layerCurveDisplayList = new LayerToDisplayListDictionary();
            layerUnscaledObjects = new Dictionary<Layer, GeoObjectList>();
            layerFaceObjects = new Dictionary<Layer, List<IGeoObject>>();
            layerTransparentObjects = new Dictionary<Layer, List<IGeoObject>>();
            layerCurveObjects = new Dictionary<Layer, List<IGeoObject>>();
            nullLayer = new Layer("NullLayer");
            noSingleAddEvents = false;
            try
            {
                userData = info.GetValue("UserData", typeof(UserData)) as UserData;
            }
            catch (SerializationException)
            {
                userData = new UserData();
            }
        }
        void ISerializable.GetObjectData(SerializationInfo info, StreamingContext context)
        {
            info.AddValue("GeoObjectList", geoObjects, typeof(GeoObjectList));
            info.AddValue("Name", name, typeof(string));
            info.AddValue("Unit", unit, typeof(Units));
            info.AddValue("DefaultScale", defaultScale, typeof(double));
            info.AddValue("LineStyleScale", lineStyleScale, typeof(double));
            info.AddValue("MinExtend", minExtend, typeof(BoundingCube));
            info.AddValue("AllDrives", allDrives, typeof(DriveList));
            info.AddValue("AllSchedules", allSchedules, typeof(ScheduleList));
            info.AddValue("UserData", userData, typeof(UserData));

        }
        void IJsonSerialize.GetObjectData(IJsonWriteData data)
        {
            data.AddProperty("GeoObjectList", geoObjects);
            data.AddProperty("Name", name);
            data.AddProperty("Unit", unit);
            data.AddProperty("DefaultScale", defaultScale);
            data.AddProperty("LineStyleScale", lineStyleScale);
            data.AddProperty("MinExtend", minExtend);
            data.AddProperty("AllDrives", allDrives);
            data.AddProperty("AllSchedules", allSchedules);
            data.AddProperty("UserData", userData);
        }

        void IJsonSerialize.SetObjectData(IJsonReadData data)
        {
            geoObjects = data.GetProperty<GeoObjectList>("GeoObjectList");
            name = data.GetProperty<string>("Name");
            unit = data.GetProperty<Units>("Unit");
            defaultScale = data.GetProperty<double>("DefaultScale");
            lineStyleScale = data.GetProperty<double>("LineStyleScale");
            minExtend = data.GetProperty<BoundingCube>("MinExtend");
            allDrives = data.GetProperty<DriveList>("AllDrives");
            allSchedules = data.GetProperty<ScheduleList>("AllSchedules");
            userData = data.GetProperty<UserData>("UserData");
            data.RegisterForSerializationDoneCallback(this);
        }
        void IJsonSerializeDone.SerializationDone(JsonSerialize jsonSerialize)
        {
            for (int i = 0; i < geoObjects.Count; ++i)
            {
                geoObjects[i].Owner = this;
                geoObjects[i].WillChangeEvent += new ChangeDelegate(OnGeoObjectWillChange);
                geoObjects[i].DidChangeEvent += new ChangeDelegate(OnGeoObjectDidChange);
            }
        }
        #endregion
        #region IDeserializationCallback Members
        void IDeserializationCallback.OnDeserialization(object sender)
        {
            (geoObjects as IDeserializationCallback).OnDeserialization(sender);
            for (int i = 0; i < geoObjects.Count; ++i)
            {
                geoObjects[i].Owner = this;
                geoObjects[i].WillChangeEvent += new ChangeDelegate(OnGeoObjectWillChange);
                geoObjects[i].DidChangeEvent += new ChangeDelegate(OnGeoObjectDidChange);
            }
        }
        #endregion
        #region IShowProperty
        /// <summary>
        /// Overrides <see cref="IShowPropertyImpl.EntryType"/>,
        /// returns <see cref="ShowPropertyEntryType.GroupTitle"/>.
        /// </summary>
        public override ShowPropertyEntryType EntryType
        {
            get
            {
                return ShowPropertyEntryType.GroupTitle;
            }
        }
        /// <summary>
        /// Overrides <see cref="IShowPropertyImpl.LabelType"/>,
        /// </summary>
        public override ShowPropertyLabelFlags LabelType
        {
            get
            {
                return ShowPropertyLabelFlags.Selectable | ShowPropertyLabelFlags.ContextMenu | ShowPropertyLabelFlags.Editable;
            }
        }
        public override string LabelText
        {
            get
            {
                return name;
            }
            set
            {   // sollte nicht vorkommen
                base.LabelText = value;
            }
        }
        public override MenuWithHandler[] ContextMenu
        {
            get
            {
                return MenuResource.LoadMenuDefinition("MenuId.Model", false, this);
            }
        }
        /// <summary>
        /// Overrides <see cref="CADability.UserInterface.IShowPropertyImpl.LabelChanged (string)"/>
        /// </summary>
        /// <param name="NewText"></param>
        public override void LabelChanged(string NewText)
        {
            Project project = FindProject();
            bool ok = true;
            for (int i = 0; i < project.GetModelCount(); ++i)
            {
                Model m = project.GetModel(i);
                if (m != this && m.name == NewText)
                {
                    ok = false;
                }
            }
            if (ok)
            {
                this.Name = NewText;
                if (NameChangedEvent != null) NameChangedEvent(this, Name);
            }
            else
            {   // nicht umbenennen, alten Wert anzeigen
                propertyTreeView.Refresh(this);
            }
        }
        private Project FindProject()
        {
            ModelsProperty mp = propertyTreeView.GetParent(this) as ModelsProperty;
            if (mp != null)
            {
                return mp.Project;
            }
            return null;
        }
        IShowProperty[] subEntries;
        /// <summary>
        /// Overrides <see cref="IShowPropertyImpl.SubEntriesCount"/>,
        /// returns the number of subentries in this property view.
        /// </summary>
        public override int SubEntriesCount
        {
            get
            {
                return SubEntries.Length;
            }
        }
        /// <summary>
        /// Overrides <see cref="IShowPropertyImpl.SubEntries"/>,
        /// returns the subentries in this property view.
        /// </summary>
        public override IShowProperty[] SubEntries
        {
            get
            {
                if (subEntries == null)
                {   // einmalig erzeugen
                    MultipleChoiceProperty unitProp = new MultipleChoiceProperty("Model.Unit", (int)unit);
                    unitProp.ValueChangedEvent += new ValueChangedDelegate(UnitChanged);
                    // TODO: DoubleProperty durch scaleProperty ersetzen (wird öfter gebraucht)
                    DoubleProperty defaultScaleProp = new DoubleProperty("Model.DefaultScale", this.Frame);
                    defaultScaleProp.GetDoubleEvent += new CADability.UserInterface.DoubleProperty.GetDoubleDelegate(OnGetDefaultScale);
                    defaultScaleProp.SetDoubleEvent += new CADability.UserInterface.DoubleProperty.SetDoubleDelegate(OnSetDefaultScale);
                    DoubleProperty lineStyleScaleProp = new DoubleProperty("Model.LineStyleScale", this.Frame);
                    lineStyleScaleProp.GetDoubleEvent += new CADability.UserInterface.DoubleProperty.GetDoubleDelegate(OnGetLineStyleScale);
                    lineStyleScaleProp.SetDoubleEvent += new CADability.UserInterface.DoubleProperty.SetDoubleDelegate(OnSetLineStyleScale);
                    subEntries = new IShowProperty[] { unitProp, defaultScaleProp, lineStyleScaleProp };
                }
                return subEntries;
            }
        }
        #endregion
        private void OnGeoObjectWillChange(IGeoObject Sender, GeoObjectChange Change)
        {
            AbortBackgroundRecalc(); // wenn gerade im Hintergrund was läüft, abbrechen
                                     // man könnte hier differenzierter arbeiten, also nur genau dieses Objekt aus der Liste nehmen
                                     // und bei didChange wieder reintun, aber das scheint mir vorläufig zu unübersichtlich
            if (!Change.NoUndoNecessary) Undo.AddUndoStep(Change);
            if (GeoObjectWillChangeEvent != null) GeoObjectWillChangeEvent(Sender, Change);
            if (continousChanges != null)
            {
                if (!Change.OnlyAttributeChanged && !continousChanges.Add(Sender))
                {
                    RemoveOctreeObjects(Sender, octTree);
                }
            }
            else
            {
                if (!Change.OnlyAttributeChanged)
                {
                    RemoveOctreeObjects(Sender, octTree);
                }
            }
        }
        private void OnGeoObjectDidChange(IGeoObject Sender, GeoObjectChange Change)
        {
            AbortBackgroundRecalc(); // wenn gerade im Hintergrund was läüft, abbrechen
            projectionToExtent.Clear();
            extent = null;
            displayListsDirty = true;
            if (GeoObjectDidChangeEvent != null) GeoObjectDidChangeEvent(Sender, Change);
            if (continousChanges == null || continousChanges.Count == 0) // Added "|| continousChanges.Count == 0"
            {
                if (!Change.OnlyAttributeChanged)
                {
                    AddOctreeObjects(Sender, octTree);
                }
            }
        }
        private void UnitChanged(object sender, object NewValue)
        {
            MultipleChoiceProperty mcp = sender as MultipleChoiceProperty;
            int sel = mcp.CurrentIndex;
            if (sel >= 0)
            {
                unit = (Units)sel;
            }
        }
        private double OnGetDefaultScale(DoubleProperty sender)
        {
            return defaultScale;
        }
        private void OnSetDefaultScale(DoubleProperty sender, double l)
        {
            defaultScale = l;
        }
        private double OnGetLineStyleScale(DoubleProperty sender)
        {
            return lineStyleScale;
        }
        private void OnSetLineStyleScale(DoubleProperty sender, double l)
        {
            lineStyleScale = l;
        }
        #region IEnumerable
        IEnumerator IEnumerable.GetEnumerator()
        {
            return (geoObjects as IEnumerable).GetEnumerator();
        }
        #endregion
        internal GeoObjectList UniteSelectedBodies(GeoObjectList selectedObjects)
        {
            List<Solid> solids = new List<Solid>();
            for (int i = 0; i < selectedObjects.Count; ++i)
            {
                if (selectedObjects[i] is Solid)
                    solids.Add(selectedObjects[i] as Solid);
            }
            int ii = 0;
            List<Solid> toRemove = new List<Solid>();
            List<bool> toInsert = new List<bool>(solids.Count); // synchron zu solids
            for (int i = 0; i < solids.Count; ++i) toInsert.Add(false);
            while (ii < solids.Count - 1)
            {
                bool united = false;
                for (int j = ii + 1; j < solids.Count; ++j)
                {
                    Solid u = Make3D.Union(solids[ii], solids[j]);
                    if (u != null)
                    {
                        if (!toInsert[ii]) toRemove.Add(solids[ii]);
                        toRemove.Add(solids[j]);
                        solids.RemoveAt(j);
                        toInsert.RemoveAt(j);
                        solids[ii] = u;
                        united = true;
                        break;
                    }
                }
                if (united)
                {
                    toInsert[ii] = true; // nur solche werden ins Modell eingefügt
                }
                else
                {
                    ++ii;
                }
            }
            GeoObjectList res = new GeoObjectList();
            using (Undo.UndoFrame)
            {
                for (int i = 0; i < toRemove.Count; ++i)
                {
                    this.Remove(toRemove[i]);
                }
                for (int i = 0; i < solids.Count; ++i)
                {
                    if (toInsert[i])
                    {
                        this.Add(solids[i]);
                        res.Add(solids[i]);
                    }
                }
            }
            return res;
        }
        #region ICommandHandler Members
        bool ICommandHandler.OnCommand(string MenuId)
        {
            switch (MenuId)
            {
                case "MenuId.Model.Rename":
                    propertyTreeView.StartEditLabel(this);
                    return true;
                case "MenuId.Model.Remove":
                    {
                        Project project = FindProject();
                        if (project != null)
                        {
                            project.RemoveModel(this);
                            ModelsProperty mp = propertyTreeView.GetParent(this) as ModelsProperty;
                            if (mp != null)
                            {
                                mp.Refresh();
                            }
                        }
                    }
                    return true;
            }
            return false;
        }
        bool ICommandHandler.OnUpdateCommand(string MenuId, CommandState CommandState)
        {
            return false;
        }
        void ICommandHandler.OnSelected(MenuWithHandler selectedMenuItem, bool selected) { }
        #endregion
        #region ICategorizedDislayLists Members
        void ICategorizedDislayLists.Add(Layer layer, bool addToFace, bool addToLinear, IGeoObject go)
        {
            if (layer == null) layer = nullLayer;
            if (go is UnscaledGeoObject)
            {
                GeoObjectList list;
                if (!layerUnscaledObjects.TryGetValue(layer, out list))
                {
                    list = new GeoObjectList();
                    layerUnscaledObjects[layer] = list;
                }
                list.Add(go);
                return; // nicht weiter verarbeiten
            }
            if (addToFace)
            {
                List<IGeoObject> list;
                if (!layerFaceObjects.TryGetValue(layer, out list))
                {
                    list = new List<IGeoObject>();
                    layerFaceObjects[layer] = list;
                }
                list.Add(go);
            }
            if (addToLinear)
            {
                List<IGeoObject> list;
                if (!layerCurveObjects.TryGetValue(layer, out list))
                {
                    list = new List<IGeoObject>();
                    layerCurveObjects[layer] = list;
                }
                list.Add(go);
            }
            if (!addToFace && !addToLinear)
            {
                List<IGeoObject> list;
                if (!layerTransparentObjects.TryGetValue(layer, out list))
                {
                    list = new List<IGeoObject>();
                    layerTransparentObjects[layer] = list;
                }
                list.Add(go);
                // zugefügt, damit transparente Faces auch Kanten haben (4.11.2015)
                if (showTransparentFaceEdges)
                {
                    if (!layerCurveObjects.TryGetValue(layer, out list))
                    {
                        list = new List<IGeoObject>();
                        layerCurveObjects[layer] = list;
                    }
                    list.Add(go);
                }

            }
        }
        #endregion
        /// <summary>
        /// Deprecated, still public for legacy reasons.
        /// </summary>
        public void ClearDisplayLists()
        {
            displayListsDirty = true;
            octTree = null; // PFOCAD braucht das
        }
        #region Query Objects
        /// <summary>
        /// Returns all objects of the model that are touched by the <paramref name="pickrect"/>, whos layers are in the
        /// <paramref name="visibleLayers"/> set and which are accepted by the <paramref name="filterList"/>.
        /// </summary>
        /// <param name="pickrect">Area that specifies which objects are beeing tested</param>
        /// <param name="projection">The projection in which the pickrect is defined</param>
        /// <param name="visibleLayers">Set of layers which are visible (and hence should be used for the test)</param>
        /// <param name="pickMode">Single or multiple objects</param>
        /// <param name="filterList">List of conditions</param>
        /// <returns>List of objects that fulfill all conditions</returns>
        public GeoObjectList GetObjectsFromRect(BoundingRect pickrect, Projection projection, Set<Layer> visibleLayers, PickMode pickMode, FilterList filterList)
        {
            if (visibleLayers == null) visibleLayers = new Set<Layer>(); // um nicht immer nach null fragen zu müssen
            GeoObjectList res = new GeoObjectList();
            if (octTree == null) InitOctTree();

            List<IGeoObject> octl = new List<IGeoObject>(octTree.GetObjectsFromRect(projection, pickrect, false));
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
                case CADability.PickMode.onlyEdges:
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
                case CADability.PickMode.singleEdge:
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
                                    double z = go.Position(center, projection.Direction, displayListPrecision);
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
                case CADability.PickMode.onlyFaces:
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
                case CADability.PickMode.singleFace:
                    foreach (IGeoObject go in oct)
                    {
                        if (go is Face && go.HitTest(projection, pickrect, false))
                        {
                            if ((filterList == null || filterList.Accept(go)) &&
                                (visibleLayers.Count == 0 || go.Layer == null || visibleLayers.Contains(go.Layer)))
                            {
                                double z = go.Position(center, projection.Direction, displayListPrecision);
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
                case CADability.PickMode.normal:
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
                case CADability.PickMode.single:
                    foreach (IGeoObject go in oct)
                    {
                        if (go.HitTest(projection, pickrect, false))
                        {
                            double z = go.Position(center, projection.Direction, displayListPrecision);
                            if (z < zmin)
                            {
                                IGeoObject toInsert = go;
                                while (toInsert.Owner is IGeoObject) toInsert = (toInsert.Owner as IGeoObject);
                                // die Frage ist hier: soll das elementare Objekt oder der es enthaltende Block
                                // mit der FilterList überprüft werden. In ERSACAD hat der Block keinen Layer
                                // jedoch die einzelnen Objekte schon. Deshalb wurde in der Abfrage
                                // "|| filterList.Accept(go) " ergänzt
                                if ((filterList == null || filterList.Accept(toInsert) || filterList.Accept(go)) &&
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
                case CADability.PickMode.children:
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
                case CADability.PickMode.blockchildren:
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
        /// <summary>
        /// Returns all objects of the model that are touched by the <paramref name="area"/>, whos layers are in the
        /// <paramref name="visibleLayers"/> set and which are accepted by the <paramref name="filterList"/>.
        /// </summary>
        /// <param name="area">Area that specifies which objects are beeing tested</param>
        /// <param name="visibleLayers">Set of layers which are visible (and hence should be used for the test)</param>
        /// <param name="pickMode">Single or multiple objects</param>
        /// <param name="filterList">List of conditions</param>
        /// <param name="toAvoid">List of objects not to select</param>
        /// <returns>List of objects that fulfill all conditions</returns>
        public GeoObjectList GetObjectsFromRect(Projection.PickArea area, Set<Layer> visibleLayers, PickMode pickMode, FilterList filterList, GeoObjectList toAvoid = null)
        {
            GeoObjectList res = new GeoObjectList();
            if (toAvoid == null) toAvoid = new GeoObjectList();
            if (area == null) return res; // kommt vor, wenn ein Fenster nicht sichtbar ist, aber trotzem irgendwie MouseMoves bekommt
            if (visibleLayers == null) visibleLayers = new Set<Layer>(); // um nicht immer nach null fragen zu müssen
            if (octTree == null) InitOctTree();

            List<IGeoObject> octl = new List<IGeoObject>(octTree.GetObjectsFromRect(area, false));
            for (int i = octl.Count - 1; i >= 0; --i)
            {   // Unsichtbare ausblenden
                if (!octl[i].IsVisible) octl.Remove(octl[i]);
            }
            IGeoObject[] oct = octl.ToArray();
            double zmin = double.MaxValue;
            IGeoObject singleObject = null;
            switch (pickMode)
            {
                case CADability.PickMode.onlyEdges:
                    foreach (IGeoObject go in oct)
                    {
                        if (go.HitTest(area, false))
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
                case CADability.PickMode.singleEdge:
                    foreach (IGeoObject go in oct)
                    {
                        if (go.HitTest(area, false))
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
                                    double z = go.Position(area.FrontCenter, area.Direction, displayListPrecision);
                                    if (z <= zmin && !toAvoid.Contains(go))
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
                case CADability.PickMode.onlyFaces:
                    foreach (IGeoObject go in oct)
                    {
                        if (go is Face && go.HitTest(area, false))
                        {
                            if ((filterList == null || filterList.Accept(go)) &&
                                (visibleLayers.Count == 0 || go.Layer == null || visibleLayers.Contains(go.Layer)))
                            {
                                res.AddUnique(go);
                            }
                        }
                    }
                    return res;
                case CADability.PickMode.singleFace:
                    foreach (IGeoObject go in oct)
                    {
                        if (go is Face && go.HitTest(area, false))
                        {
                            if ((filterList == null || filterList.Accept(go)) &&
                                (visibleLayers.Count == 0 || go.Layer == null || visibleLayers.Contains(go.Layer)))
                            {
                                double z = go.Position(area.FrontCenter, area.Direction, displayListPrecision);
                                if (z <= zmin && !toAvoid.Contains(go))
                                {
                                    zmin = z;
                                    singleObject = go;
                                }
                            }
                        }
                    }
                    if (singleObject != null) res.Add(singleObject);
                    return res;
                case CADability.PickMode.normal:
                    {
                        Set<IGeoObject> set = new Set<IGeoObject>(new GeoObjectComparer());
                        foreach (IGeoObject go in oct)
                        {
                            if (go.HitTest(area, false))
                            {
                                IGeoObject toInsert = go;
                                while (toInsert.Owner is IGeoObject) toInsert = (toInsert.Owner as IGeoObject);
                                if (toInsert.Owner is Model)
                                {   // sonst werden auch edges gefunden, was hier bei single click nicht gewünscht
                                    if ((filterList == null || filterList.Accept(toInsert) || filterList.Accept(go)) &&
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
                case CADability.PickMode.single:
                    foreach (IGeoObject go in oct)
                    {
                        if (go.HitTest(area, false))
                        {
                            double z = go.Position(area.FrontCenter, area.Direction, displayListPrecision);
                            if (z <= zmin + Precision.eps)
                            {
                                IGeoObject toInsert = go;
                                while (toInsert.Owner is IGeoObject) toInsert = (toInsert.Owner as IGeoObject);
                                // die Frage ist hier: soll das elementare Objekt oder der es enthaltende Block
                                // mit der FilterList überprüft werden. In ERSACAD hat der Block keinen Layer
                                // jedoch die einzelnen Objekte schon. Deshalb wurde in der Abfrage
                                // "|| filterList.Accept(go) " ergänzt
                                if ((filterList == null || filterList.Accept(toInsert) || filterList.Accept(go)) &&
                                    (visibleLayers.Count == 0 || toInsert.Layer == null || visibleLayers.Contains(toInsert.Layer) || visibleLayers.Contains(go.Layer)))
                                {
                                    if (toInsert.Owner is Model)
                                    {   // sonst werden auch edges gefunden, was hier bei single click nicht gewünscht
                                        if (!toAvoid.Contains(toInsert) || z < zmin - Precision.eps)
                                        {   // if nothing else is closest, use the object ignoring toAvoid, if an object has the same distance as objects in toAvoid, but is not in toAvoid, use this object
                                            zmin = z;
                                            singleObject = toInsert;
                                        }
                                    }
                                }
                            }
                        }
                    }
                    if (singleObject != null) res.Add(singleObject);
                    return res;
                case CADability.PickMode.singleChild: // wie single, nur wird nicht parent bestimmt
                    foreach (IGeoObject go in oct)
                    {
                        if (!(go.Owner is Edge) && go.HitTest(area, false)) // Kanten gelten nicht
                        {
                            double z = go.Position(area.FrontCenter, area.Direction, displayListPrecision);
                            if (z <= zmin && !toAvoid.Contains(go))
                            {
                                IGeoObject toInsert = go;
                                if ((filterList == null || filterList.Accept(toInsert) || filterList.Accept(go)) &&
                                    (visibleLayers.Count == 0 || toInsert.Layer == null || visibleLayers.Contains(toInsert.Layer) || visibleLayers.Contains(go.Layer)))
                                {   // hier werden auch edges gefunden, es wird aber hinterher ja meist zum Face oder Shell hochgegangen
                                    zmin = z;
                                    singleObject = toInsert;
                                }
                            }
                        }
                    }
                    if (singleObject != null) res.Add(singleObject);
                    return res;
                case CADability.PickMode.children:
                    foreach (IGeoObject go in oct)
                    {
                        if (!(go.Owner is Edge) && go.HitTest(area, false))  // Kanten gelten nicht
                        {
                            if ((filterList == null || filterList.Accept(go)) &&
                                (visibleLayers.Count == 0 || go.Layer == null || visibleLayers.Contains(go.Layer)))
                            {
                                res.AddUnique(go);
                            }
                        }
                    }
                    return res;
                case CADability.PickMode.blockchildren:
                    foreach (IGeoObject go in oct)
                    {
                        if (go.HitTest(area, false))
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
        /// <summary>
        /// Returns all objects of the model that are inside ore close to the provided box.
        /// </summary>
        /// <param name="box">Box from which to seek objects</param>
        /// <returns>List of objects in or close to the box</returns>
        public GeoObjectList GetObjectsFromBox(BoundingCube box)
        {
            if (octTree == null) InitOctTree();
            Set<IGeoObject> res = new Set<IGeoObject>();
            IGeoObject[] found = octTree.GetObjectsFromBox(box);
            for (int i = 0; i < found.Length; ++i)
            {
                IGeoObject toAdd = found[i];
                while (toAdd.Owner is IGeoObject) toAdd = toAdd.Owner as IGeoObject;
                res.Add(toAdd);
            }
            return new GeoObjectList(res.ToArray());
        }
        /// <summary>
        /// Adjusts the point defined by <paramref name="spf"/> (<see cref="SnapPointFinder.SourcePoint"/> and <see cref="SnapPointFinder.Projection"/>)
        /// according to the settings of <paramref name="spf"/> (<see cref="SnapPointFinder.SnapModes"/> and other properties)
        /// by checking all objects in the <paramref name="visibleLayers"/>.
        /// As a result <see cref="SnapPointFinder.SnapPoint"/> and <see cref="SnapPointFinder.DidSnap"/> will be set.
        /// </summary>
        /// <param name="spf">Point to be adjusted and mode how to adjust</param>
        /// <param name="projection">Projection</param>
        /// <param name="visibleLayers">Visible layers to consider</param>
        public void AdjustPoint(SnapPointFinder spf, Projection projection, Set<Layer> visibleLayers)
        {   // alle relevanten Objekte zunächst im Quadtree suchen
            //BoundingRect br = new BoundingRect(spf.SourceBeam, spf.MaxDist, spf.MaxDist);
            //GeoObjectList l = this.GetObjectsFromRect(br, PickMode.normal, null);
            GeoObjectList l = GetObjectsFromRect(spf.pickArea, visibleLayers, PickMode.normal, null);
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
                double gridx = projection.Grid.XDistance;
                double gridy = projection.Grid.YDistance;
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
        #endregion

        internal void InvalidateProjectionCache(Projection pr)
        {
            if (projectionToExtent.ContainsKey(pr)) projectionToExtent.Remove(pr);
        }
        /// <summary>
        /// Searches for the best point in this model which corresponds to the mouse position and the
        /// active snap modes. Mouse position and snap modes are contained in the <paramref name="spf"/>
        /// parameter, where also the result is returned
        /// </summary>
        /// <param name="spf">SnapPointFinder class which contains the snap mode and the mouse position</param>
        /// <param name="visibleLayers">Layers to be included in the search</param>
        public void AdjustPoint(SnapPointFinder spf, Set<Layer> visibleLayers)
        {   // alle relevanten Objekte zunächst im Quadtree suchen
            //BoundingRect br = new BoundingRect(spf.SourceBeam, spf.MaxDist, spf.MaxDist);
            //GeoObjectList l = this.GetObjectsFromRect(br, PickMode.normal, null);
            GeoObjectList l = this.GetObjectsFromRect(spf.pickArea, visibleLayers, PickMode.normal, null);
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
                double gridx = spf.Projection.Grid.XDistance;
                double gridy = spf.Projection.Grid.YDistance;
                if (gridx > 0.0 && gridy > 0.0)
                {
                    GeoPoint2D p0 = spf.SourcePoint;
                    p0 = spf.Projection.DrawingPlane.Project(spf.Projection.DrawingPlanePoint(p0));
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


    }
}
